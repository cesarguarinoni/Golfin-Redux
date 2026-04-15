#!/usr/bin/env node
/**
 * generate-terrain.mjs — Heightmap generation for UHole Geo
 *
 * Simplified version of UHole Lite's generate-terrain.mjs:
 *   - NO affine transform — uses direct bounding box → lat/lon mapping
 *   - NO 90° CCW rotation of the heightmap
 *   - Reads hole-bounds.json instead of geo-align.json
 *
 * Usage:
 *   node scripts/generate-terrain.mjs lomond-country-club 7       # single hole
 *   node scripts/generate-terrain.mjs lomond-country-club --all   # all 18
 *   node scripts/generate-terrain.mjs lomond-country-club 7 --perlin  # force Perlin
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { perlin2D, blur2D, monotoneCubicSpline } from './lib/terrain.mjs';
import { readDem5aTile, sampleDem5a } from './lib/dem5a.mjs';
import { latToTileY, lonToTileX, tileBounds } from './lib/tiles.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

const RES = 2049; // heightmap resolution (matches Unity terrain)

// Zone indices (must match classify-zones.mjs)
const ZONES = {
  background: 0, fairway: 1, green: 2, semi_rough: 3, rough: 4,
  trees: 5, bunker: 6, water: 7, cart_path: 8, ob: 9, tee_box: 10,
};

const DEM_ZOOM = 15;

// ---------------------------------------------------------------------------
// Haversine distance
// ---------------------------------------------------------------------------

function haversine(lat1, lon1, lat2, lon2) {
  const R = 6371000; // Earth radius in meters
  const toRad = (deg) => deg * Math.PI / 180;
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a = Math.sin(dLat / 2) ** 2 +
            Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

// ---------------------------------------------------------------------------
// DEM5A loading and sampling
// ---------------------------------------------------------------------------

async function loadDemTiles(courseId, bounds) {
  // Look for DEM tiles in UHole's basemap directory (shared with Lite)
  const demDir = path.join(ROOT, '..', 'UHole', 'output',
    courseId, 'basemap', 'gsi-dem5a-z15');

  const minTX = lonToTileX(bounds.west, DEM_ZOOM);
  const maxTX = lonToTileX(bounds.east, DEM_ZOOM);
  const minTY = latToTileY(bounds.north, DEM_ZOOM);
  const maxTY = latToTileY(bounds.south, DEM_ZOOM);

  const tiles = [];
  for (let ty = minTY; ty <= maxTY; ty++) {
    for (let tx = minTX; tx <= maxTX; tx++) {
      const filePath = path.join(demDir, `${DEM_ZOOM}-${tx}-${ty}.png`);
      try {
        const grid = await readDem5aTile(filePath);
        tiles.push({ bounds: tileBounds(tx, ty, DEM_ZOOM), grid });
      } catch {
        console.warn(`  DEM tile ${DEM_ZOOM}-${tx}-${ty} not found`);
      }
    }
  }
  return tiles;
}

function sampleDemSafe(demTiles, lat, lon, fallback) {
  const val = sampleDem5a(demTiles, lat, lon);
  return (val !== null && !isNaN(val)) ? val : fallback;
}

// ---------------------------------------------------------------------------
// Quadratic surface fitting
// ---------------------------------------------------------------------------

/**
 * Least-squares quadratic surface fit:
 *   h = a*x^2 + b*y^2 + c*x*y + d*x + e*y + f
 * Solves 6 unknowns via normal equations with Gaussian elimination.
 * @param {{x:number, y:number, h:number}[]} points
 * @returns {{a:number, b:number, c:number, d:number, e:number, f:number}}
 */
function fitQuadratic(points) {
  const n = points.length;
  if (n < 6) {
    const avg = points.reduce((s, p) => s + p.h, 0) / (n || 1);
    return { a: 0, b: 0, c: 0, d: 0, e: 0, f: avg };
  }

  // Build A^T*A (6x6 symmetric) and A^T*b (6x1)
  // Row of A: [x^2, y^2, xy, x, y, 1]
  const ATA = Array.from({ length: 6 }, () => new Float64Array(6));
  const ATb = new Float64Array(6);

  for (const p of points) {
    const { x, y, h } = p;
    const row = [x * x, y * y, x * y, x, y, 1];
    for (let i = 0; i < 6; i++) {
      for (let j = 0; j < 6; j++) {
        ATA[i][j] += row[i] * row[j];
      }
      ATb[i] += row[i] * h;
    }
  }

  // Gaussian elimination with partial pivoting
  const aug = ATA.map((row, i) => [...row, ATb[i]]);

  for (let col = 0; col < 6; col++) {
    let maxRow = col;
    let maxVal = Math.abs(aug[col][col]);
    for (let row = col + 1; row < 6; row++) {
      if (Math.abs(aug[row][col]) > maxVal) {
        maxVal = Math.abs(aug[row][col]);
        maxRow = row;
      }
    }
    if (maxVal < 1e-12) {
      const avg = points.reduce((s, p) => s + p.h, 0) / n;
      return { a: 0, b: 0, c: 0, d: 0, e: 0, f: avg };
    }
    [aug[col], aug[maxRow]] = [aug[maxRow], aug[col]];

    for (let row = col + 1; row < 6; row++) {
      const factor = aug[row][col] / aug[col][col];
      for (let j = col; j <= 6; j++) {
        aug[row][j] -= factor * aug[col][j];
      }
    }
  }

  // Back substitution
  const coeffs = new Float64Array(6);
  for (let i = 5; i >= 0; i--) {
    let sum = aug[i][6];
    for (let j = i + 1; j < 6; j++) {
      sum -= aug[i][j] * coeffs[j];
    }
    coeffs[i] = sum / aug[i][i];
  }

  return {
    a: coeffs[0], b: coeffs[1], c: coeffs[2],
    d: coeffs[3], e: coeffs[4], f: coeffs[5],
  };
}

/** Evaluate the quadratic surface at a point. */
function evalQuadratic(q, x, y) {
  return q.a * x * x + q.b * y * y + q.c * x * y +
         q.d * x + q.e * y + q.f;
}

// ---------------------------------------------------------------------------
// Average elevation for a zone (used for slope drop calculation)
// ---------------------------------------------------------------------------

function avgElevForZone(heightmap, zoneGrid, zw, zh, targetZone, res) {
  let sum = 0, count = 0;
  for (let hy = 0; hy < res; hy++) {
    for (let hx = 0; hx < res; hx++) {
      const nx = hx / (res - 1);
      const ny = hy / (res - 1);
      const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      if (zoneGrid[zy * zw + zx] === targetZone && heightmap[hy * res + hx] > -9000) {
        sum += heightmap[hy * res + hx];
        count++;
      }
    }
  }
  return count > 0 ? sum / count : null;
}

// ---------------------------------------------------------------------------
// Write heightmap as big-endian uint16 raw
// ---------------------------------------------------------------------------

function writeHeightmapRaw(outputPath, uint16Data) {
  // Flip rows so row 0 = south (matches Unity's terrain heightmap convention
  // where heights[y=0, x] is the south edge). The internal uint16Data was
  // generated with row 0 = north (satellite PNG convention).
  const buffer = Buffer.alloc(RES * RES * 2);
  for (let srcY = 0; srcY < RES; srcY++) {
    const dstY = RES - 1 - srcY;
    for (let x = 0; x < RES; x++) {
      const srcIdx = srcY * RES + x;
      const dstIdx = dstY * RES + x;
      buffer.writeUInt16BE(uint16Data[srcIdx], dstIdx * 2);
    }
  }
  fs.writeFileSync(outputPath, buffer);
}

// ---------------------------------------------------------------------------
// DEM-based terrain generation (primary path)
// ---------------------------------------------------------------------------

async function generateTerrainDEM(courseId, holeNumber, holeBounds, zonesData, config) {
  const bounds = holeBounds.bounds;
  const terrainDefaults = config.terrain_defaults;

  // Compute terrain dimensions from bounding box
  const terrainWidthM = haversine(bounds.north, bounds.west, bounds.north, bounds.east);
  const terrainLengthM = haversine(bounds.north, bounds.west, bounds.south, bounds.west);

  const zoneGrid = Buffer.from(zonesData.grid, 'base64');
  const zw = zonesData.source_dimensions.width;
  const zh = zonesData.source_dimensions.height;

  // Green & tee centroids from zone grid
  let greenSumX = 0, greenSumY = 0, greenCount = 0;
  let teeSumX = 0, teeSumY = 0, teeCount = 0;
  for (let y = 0; y < zh; y++) {
    for (let x = 0; x < zw; x++) {
      const zone = zoneGrid[y * zw + x];
      if (zone === ZONES.green) {
        greenSumX += x; greenSumY += y; greenCount++;
      } else if (zone === ZONES.tee_box) {
        teeSumX += x; teeSumY += y; teeCount++;
      }
    }
  }
  const greenCentroid = greenCount > 0
    ? { x: greenSumX / greenCount / zw, y: greenSumY / greenCount / zh }
    : { x: 0.5, y: 0.15 };
  const teeCentroid = teeCount > 0
    ? { x: teeSumX / teeCount / zw, y: teeSumY / teeCount / zh }
    : { x: 0.5, y: 0.85 };

  // Load DEM tiles
  const demTiles = await loadDemTiles(courseId, bounds);
  if (demTiles.length === 0) {
    console.warn('  No DEM tiles found, falling back to Perlin');
    return null;
  }

  // Sample DEM using DIRECT bounding box mapping (no affine)
  const rawDem = new Float64Array(RES * RES);
  let validSum = 0, validCount = 0;
  let fallback = NaN;

  // First pass: collect valid samples to compute fallback average
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);
      const lat = bounds.north - ny * (bounds.north - bounds.south);
      const lon = bounds.west + nx * (bounds.east - bounds.west);
      const elev = sampleDem5a(demTiles, lat, lon);
      if (elev !== null && !isNaN(elev)) {
        rawDem[hy * RES + hx] = elev;
        validSum += elev;
        validCount++;
      } else {
        rawDem[hy * RES + hx] = NaN;
      }
    }
  }

  if (validCount === 0) {
    console.warn('  DEM returned no valid samples, falling back to Perlin');
    return null;
  }

  const avgElev = validSum / validCount;

  // Fill NaN gaps by propagating from nearest valid neighbours.
  // GSI DEM5A returns NoData for water bodies (photogrammetry can't read
  // water surfaces). Filling those with the global average makes lakes appear
  // RAISED to the average elevation. Instead we iteratively dilate valid
  // values into NaN cells, so water inherits the shoreline elevation (and the
  // Unity importer can then carve it deeper based on painted water zones).
  let nanCount = 0;
  for (let i = 0; i < RES * RES; i++) if (isNaN(rawDem[i])) nanCount++;
  if (nanCount > 0) {
    let pass = 0;
    let remaining = nanCount;
    while (remaining > 0 && pass < 200) {
      const next = new Float64Array(rawDem);
      let filled = 0;
      for (let hy = 0; hy < RES; hy++) {
        for (let hx = 0; hx < RES; hx++) {
          const idx = hy * RES + hx;
          if (!isNaN(rawDem[idx])) continue;
          let sum = 0, count = 0;
          for (let dy = -1; dy <= 1; dy++) {
            for (let dx = -1; dx <= 1; dx++) {
              if (dx === 0 && dy === 0) continue;
              const nx = hx + dx, ny = hy + dy;
              if (nx < 0 || nx >= RES || ny < 0 || ny >= RES) continue;
              const v = rawDem[ny * RES + nx];
              if (!isNaN(v)) { sum += v; count++; }
            }
          }
          if (count > 0) {
            next[idx] = sum / count;
            filled++;
          }
        }
      }
      if (filled === 0) {
        // No more progress — fill remaining with avg as last resort
        for (let i = 0; i < RES * RES; i++) {
          if (isNaN(next[i])) next[i] = avgElev;
        }
        rawDem.set(next);
        remaining = 0;
        break;
      }
      rawDem.set(next);
      remaining -= filled;
      pass++;
    }
    console.log(`  Filled ${nanCount - remaining}/${nanCount} NaN cells via neighbour propagation (${pass} passes)`);
  }

  // Record raw elevation range
  let rawMin = Infinity, rawMax = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (rawDem[i] < rawMin) rawMin = rawDem[i];
    if (rawDem[i] > rawMax) rawMax = rawDem[i];
  }

  // Fit ONE quadratic surface to all playable zones combined
  const playableZones = new Set([
    ZONES.fairway, ZONES.green, ZONES.tee_box, ZONES.bunker,
    ZONES.semi_rough, ZONES.cart_path, ZONES.rough,
  ]);

  const surfacePoints = [];
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);
      const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      const zone = zoneGrid[zy * zw + zx];

      if (playableZones.has(zone)) {
        const h = rawDem[hy * RES + hx];
        if (h > -9000 && !isNaN(h)) {
          surfacePoints.push({ x: hx, y: hy, h });
        }
      }
    }
  }

  const holeSurface = fitQuadratic(surfacePoints);
  const slopeX = (holeSurface.d * (RES - 1)).toFixed(2);
  const slopeY = (holeSurface.e * (RES - 1)).toFixed(2);
  const curveX = (holeSurface.a * (RES - 1) * (RES - 1)).toFixed(2);
  const curveY = (holeSurface.b * (RES - 1) * (RES - 1)).toFixed(2);
  console.log(`  Quadratic surface: ${surfacePoints.length} pts, ` +
    `slope dX=${slopeX}m dY=${slopeY}m, ` +
    `curve dX²=${curveX}m dY²=${curveY}m`);

  // ─── Spline + Quadratic Cross-Axis ───────────────────────────
  const N_SPLINE_POINTS = 20;

  // Tee and green centroids in heightmap coordinates
  const teeHX = teeCentroid.x * (RES - 1);
  const teeHY = teeCentroid.y * (RES - 1);
  const greenHX = greenCentroid.x * (RES - 1);
  const greenHY = greenCentroid.y * (RES - 1);

  // Axis vector and length
  const axDx = greenHX - teeHX;
  const axDy = greenHY - teeHY;
  const axisLen = Math.sqrt(axDx * axDx + axDy * axDy);

  // Unit vectors: along-axis (A) and cross-axis (C)
  const axUx = axisLen > 0 ? axDx / axisLen : 1;
  const axUy = axisLen > 0 ? axDy / axisLen : 0;

  // Sample DEM at N points along the axis
  const splineXs = [];
  const splineYs = [];
  for (let i = 0; i < N_SPLINE_POINTS; i++) {
    const t = i / (N_SPLINE_POINTS - 1); // 0..1
    const along = t * axisLen;
    const hx = teeHX + t * axDx;
    const hy = teeHY + t * axDy;

    // Map heightmap coords back to lat/lon for DEM sampling
    const nx = hx / (RES - 1);
    const ny = hy / (RES - 1);
    const lat = bounds.north - ny * (bounds.north - bounds.south);
    const lon = bounds.west + nx * (bounds.east - bounds.west);

    const elev = sampleDemSafe(demTiles, lat, lon, avgElev);
    splineXs.push(along);
    splineYs.push(elev);
  }

  const spline = monotoneCubicSpline(splineXs, splineYs);

  console.log(`  Spline: ${N_SPLINE_POINTS} DEM samples along axis (${axisLen.toFixed(0)} cells)`);
  console.log(`    Elevations: ${splineYs.map(e => e.toFixed(1)).join(', ')} m ASL`);

  // Build heightmap: spline(along) + quadratic cross-axis residual
  const heightmap = new Float64Array(RES * RES);
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      // Vector from tee to this cell
      const dx = hx - teeHX;
      const dy = hy - teeHY;

      // Project onto axis (clamped to [0, axisLen])
      let along = dx * axUx + dy * axUy;
      along = Math.max(0, Math.min(axisLen, along));

      // Spline elevation at this along-axis position
      const splineH = spline(along);

      // Quadratic at this cell
      const quadHere = evalQuadratic(holeSurface, hx, hy);

      // Quadratic at the axis point (projection of this cell onto axis)
      const projHX = teeHX + along * axUx;
      const projHY = teeHY + along * axUy;
      const quadOnAxis = evalQuadratic(holeSurface, projHX, projHY);

      // Cross-axis residual: how the quadratic varies perpendicular to axis
      const crossResidual = quadHere - quadOnAxis;

      heightmap[hy * RES + hx] = splineH + crossResidual;
    }
  }

  console.log(`  Mode: spline along-axis + quadratic cross-axis`);

  // Normalize — relative elevation
  let minElev = Infinity, maxElev = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] < minElev) minElev = heightmap[i];
    if (heightmap[i] > maxElev) maxElev = heightmap[i];
  }

  const elevRange = maxElev - minElev || 1;
  const MAX_PLAYABLE_RANGE = 25;
  const scaleFactor = elevRange > MAX_PLAYABLE_RANGE
    ? MAX_PLAYABLE_RANGE / elevRange : 1.0;
  const playableRange = elevRange * scaleFactor;

  for (let i = 0; i < RES * RES; i++) {
    heightmap[i] = (heightmap[i] - minElev) * scaleFactor;
  }

  // Single global blur pass
  const smoothed = blur2D(heightmap, RES, RES, 1);

  // Recalculate min/max after smoothing
  let globalMin = Infinity, globalMax = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (smoothed[i] < globalMin) globalMin = smoothed[i];
    if (smoothed[i] > globalMax) globalMax = smoothed[i];
  }

  // Encode to uint16
  const range = globalMax - globalMin || 1;
  const uint16Data = new Uint16Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    const normalized = (smoothed[i] - globalMin) / range;
    uint16Data[i] = Math.round(Math.max(0, Math.min(65535, normalized * 65535)));
  }

  // Slope drop: tee vs green average elevation
  const teeAvg = avgElevForZone(smoothed, zoneGrid, zw, zh, ZONES.tee_box, RES);
  const greenAvg = avgElevForZone(smoothed, zoneGrid, zw, zh, ZONES.green, RES);
  const slopeDrop = (teeAvg !== null && greenAvg !== null) ? teeAvg - greenAvg : 0;

  return {
    uint16Data,
    terrainWidth: parseFloat(terrainWidthM.toFixed(1)),
    terrainLength: parseFloat(terrainLengthM.toFixed(1)),
    minElevation: 0,
    maxElevation: parseFloat(playableRange.toFixed(1)),
    slopeDrop: parseFloat(slopeDrop.toFixed(1)),
    greenCentroid,
    teeCentroid,
    source: 'dem5a',
    demSource: {
      provider: 'gsi',
      dataset: 'dem5a',
      zoom: DEM_ZOOM,
      resolution_m: 5,
      raw_elevation_range_m: {
        min: parseFloat(rawMin.toFixed(1)),
        max: parseFloat(rawMax.toFixed(1)),
      },
      playable_range_m: parseFloat(playableRange.toFixed(1)),
      scale_factor: parseFloat(scaleFactor.toFixed(3)),
    },
  };
}

// ---------------------------------------------------------------------------
// Perlin-based terrain generation (fallback when no DEM tiles)
// ---------------------------------------------------------------------------

function generateTerrainPerlin(holeNumber, holeBounds, zonesData, config) {
  const seed = holeNumber * 1337;
  const bounds = holeBounds.bounds;
  const terrainDefaults = config.terrain_defaults;

  const terrainWidthM = haversine(bounds.north, bounds.west, bounds.north, bounds.east);
  const terrainLengthM = haversine(bounds.north, bounds.west, bounds.south, bounds.west);

  const zoneGrid = Buffer.from(zonesData.grid, 'base64');
  const zw = zonesData.source_dimensions.width;
  const zh = zonesData.source_dimensions.height;

  // Green & tee centroids from zone grid
  let greenSumX = 0, greenSumY = 0, greenCount = 0;
  let teeSumX = 0, teeSumY = 0, teeCount = 0;
  for (let y = 0; y < zh; y++) {
    for (let x = 0; x < zw; x++) {
      const zone = zoneGrid[y * zw + x];
      if (zone === ZONES.green) {
        greenSumX += x; greenSumY += y; greenCount++;
      } else if (zone === ZONES.tee_box) {
        teeSumX += x; teeSumY += y; teeCount++;
      }
    }
  }
  const greenCentroid = greenCount > 0
    ? { x: greenSumX / greenCount / zw, y: greenSumY / greenCount / zh }
    : { x: 0.5, y: 0.15 };
  const teeCentroid = teeCount > 0
    ? { x: teeSumX / teeCount / zw, y: teeSumY / teeCount / zh }
    : { x: 0.5, y: 0.85 };

  // Base slope from tee to green
  let slopeDrop = 6.0;
  const [dropMin, dropMax] = terrainDefaults.tee_to_green_drop_range_m;
  slopeDrop = Math.min(slopeDrop, dropMax);

  const noiseFreq = terrainDefaults.noise_frequency;
  const noiseAmp = terrainDefaults.base_undulation_m;
  const heightmap = new Float64Array(RES * RES);

  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);

      // Layer 1: Base slope
      const teeY = teeCentroid.y;
      const greenY = greenCentroid.y;
      const span = teeY - greenY;
      const t = span !== 0 ? Math.max(0, Math.min(1, (ny - greenY) / span)) : 0.5;
      let baseSlope = slopeDrop * t;

      // Layer 2: Multi-octave Perlin noise
      let noise = perlin2D(hx * noiseFreq + seed, hy * noiseFreq + seed * 0.7) * noiseAmp;

      // Layer 3: Zone-based modifiers
      const zoneX = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zoneY = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      const zone = zoneGrid[zoneY * zw + zoneX];

      let heightMod = 0;
      let isWater = false;

      switch (zone) {
        case ZONES.green:
          noise *= terrainDefaults.green_flatness;
          break;
        case ZONES.fairway:
          noise *= 0.45;
          break;
        case ZONES.tee_box:
          noise *= 0.10;
          break;
        case ZONES.water:
          isWater = true;
          break;
        case ZONES.trees:
          heightMod += terrainDefaults.tree_ridge_m;
          break;
        case ZONES.bunker:
          noise *= 0.3;
          break;
        case ZONES.cart_path:
          noise *= 0.55;
          break;
      }

      let totalHeight = baseSlope + noise + heightMod;
      if (isWater) totalHeight = -9999;

      heightmap[hy * RES + hx] = totalHeight;
    }
  }

  // Find min ignoring water sentinels
  let globalMin = Infinity, globalMax = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] > -9000) {
      if (heightmap[i] < globalMin) globalMin = heightmap[i];
      if (heightmap[i] > globalMax) globalMax = heightmap[i];
    }
  }

  const waterLevel = globalMin - 2.0;
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] < -9000) heightmap[i] = waterLevel;
  }
  globalMin = waterLevel;

  const waterMask = new Uint8Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] <= waterLevel + 0.01) waterMask[i] = 1;
  }

  const smoothed = blur2D(heightmap, RES, RES, 2);

  // Restore water
  for (let i = 0; i < RES * RES; i++) {
    if (waterMask[i]) smoothed[i] = waterLevel;
  }

  // Recalculate min/max
  globalMin = Infinity; globalMax = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (smoothed[i] < globalMin) globalMin = smoothed[i];
    if (smoothed[i] > globalMax) globalMax = smoothed[i];
  }

  // Normalize to 0-65535
  const range = globalMax - globalMin || 1;
  const uint16Data = new Uint16Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    const normalized = (smoothed[i] - globalMin) / range;
    uint16Data[i] = Math.round(Math.max(0, Math.min(65535, normalized * 65535)));
  }

  return {
    uint16Data,
    terrainWidth: parseFloat(terrainWidthM.toFixed(1)),
    terrainLength: parseFloat(terrainLengthM.toFixed(1)),
    minElevation: 0,
    maxElevation: parseFloat(range.toFixed(1)),
    slopeDrop: parseFloat(slopeDrop.toFixed(1)),
    noiseAmp,
    seed,
    greenCentroid,
    teeCentroid,
    source: 'perlin',
  };
}

// ---------------------------------------------------------------------------
// Process a single hole
// ---------------------------------------------------------------------------

async function processHole(courseId, holeNumber, config, forcePerlin) {
  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);

  const holeBoundsPath = path.join(holeDir, 'hole-bounds.json');
  const zonesPath = path.join(holeDir, 'zones.json');

  if (!fs.existsSync(holeBoundsPath)) {
    console.error(`  Missing hole-bounds.json for hole ${holeNumber}`);
    return null;
  }
  if (!fs.existsSync(zonesPath)) {
    console.error(`  Missing zones.json for hole ${holeNumber}`);
    return null;
  }

  const holeBounds = JSON.parse(fs.readFileSync(holeBoundsPath, 'utf-8'));
  const zonesData = JSON.parse(fs.readFileSync(zonesPath, 'utf-8'));

  // Try DEM path first (unless forced to Perlin)
  let result = null;
  let usedDem = false;

  if (!forcePerlin) {
    result = await generateTerrainDEM(courseId, holeNumber, holeBounds, zonesData, config);
    if (result) usedDem = true;
  }

  // Fallback to Perlin
  if (!result) {
    result = generateTerrainPerlin(holeNumber, holeBounds, zonesData, config);
  }

  // Write heightmap.raw — NO rotation (direct write)
  const rawPath = path.join(holeDir, 'heightmap.raw');
  writeHeightmapRaw(rawPath, result.uint16Data);

  // Write terrain-meta.json
  const meta = {
    hole_number: holeNumber,
    heightmap_file: 'heightmap.raw',
    format: 'uint16be',
    resolution: RES,
    terrain_width_m: result.terrainWidth,
    terrain_length_m: result.terrainLength,
    min_elevation_m: result.minElevation,
    max_elevation_m: result.maxElevation,
    source: result.source,
    green_centroid_normalized: {
      x: parseFloat(result.greenCentroid.x.toFixed(3)),
      y: parseFloat(result.greenCentroid.y.toFixed(3)),
    },
    tee_centroid_normalized: {
      x: parseFloat(result.teeCentroid.x.toFixed(3)),
      y: parseFloat(result.teeCentroid.y.toFixed(3)),
    },
  };

  if (usedDem) {
    meta.dem_source = result.demSource;
    meta.slope_drop_m = result.slopeDrop;
  } else {
    meta.noise_amplitude_m = result.noiseAmp;
    meta.seed = result.seed;
    meta.slope_drop_m = result.slopeDrop;
  }

  fs.writeFileSync(
    path.join(holeDir, 'terrain-meta.json'),
    JSON.stringify(meta, null, 2),
    'utf-8'
  );

  return { meta, usedDem };
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

async function main() {
  const args = process.argv.slice(2);
  const forcePerlin = args.includes('--perlin');
  const filtered = args.filter(a => a !== '--perlin');
  const courseId = filtered[0];
  const holeArg = filtered[1];

  if (!courseId || !holeArg) {
    console.error('Usage: node scripts/generate-terrain.mjs <course-id> <hole-number|--all> [--perlin]');
    process.exit(1);
  }

  const configPath = path.join(ROOT, 'config', `${courseId}.json`);
  if (!fs.existsSync(configPath)) {
    console.error(`Config not found: ${configPath}`);
    process.exit(1);
  }

  const config = JSON.parse(fs.readFileSync(configPath, 'utf-8'));

  const holes = holeArg === '--all'
    ? Array.from({ length: 18 }, (_, i) => i + 1)
    : [parseInt(holeArg, 10)];

  if (holes.some(h => isNaN(h) || h < 1 || h > 18)) {
    console.error('Hole number must be 1-18 or --all');
    process.exit(1);
  }

  if (forcePerlin) console.log('Forcing Perlin noise (--perlin)\n');
  console.log(`Generating terrain for ${holes.length} hole(s)\n`);

  for (const h of holes) {
    process.stdout.write(`Hole ${h}/18 ... `);
    const result = await processHole(courseId, h, config, forcePerlin);
    if (!result) { console.log('FAILED'); continue; }

    const m = result.meta;
    const src = result.usedDem ? 'DEM5A' : 'Perlin';
    const demInfo = result.usedDem && m.dem_source
      ? `  raw=[${m.dem_source.raw_elevation_range_m.min},${m.dem_source.raw_elevation_range_m.max}]m ASL`
      : '';
    console.log(`OK (${src})  ${m.terrain_width_m}x${m.terrain_length_m}m  elev=${m.max_elevation_m}m  drop=${m.slope_drop_m}m${demInfo}`);
  }

  console.log('\nDone.');
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
