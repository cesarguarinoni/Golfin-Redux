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

  // Playable zones set — used by the ravine carving block below
  const playableZones = new Set([
    ZONES.fairway, ZONES.green, ZONES.tee_box, ZONES.bunker,
    ZONES.semi_rough, ZONES.cart_path, ZONES.rough,
  ]);

  // Build heightmap as a Gaussian-smoothed rawDem.
  // A large sigma removes 5m DEM grid noise while preserving real macro
  // topography in its actual orientation — no spline axis, no projection,
  // no synthetic surface, no diagonal artifacts.
  const TERRAIN_SMOOTH_SIGMA_M = 10.0;
  const tsmMetersPerCell = ((terrainWidthM + terrainLengthM) / 2) / (RES - 1);
  const tsmSigmaCells = TERRAIN_SMOOTH_SIGMA_M / tsmMetersPerCell;
  const tsmRadius = Math.max(1, Math.ceil(3 * tsmSigmaCells));
  const tsmKernelSize = 2 * tsmRadius + 1;
  const tsmKernel = new Float64Array(tsmKernelSize);
  {
    const s2 = 2 * tsmSigmaCells * tsmSigmaCells;
    let kSum = 0;
    for (let i = 0; i < tsmKernelSize; i++) {
      const x = i - tsmRadius;
      tsmKernel[i] = Math.exp(-(x * x) / s2);
      kSum += tsmKernel[i];
    }
    for (let i = 0; i < tsmKernelSize; i++) tsmKernel[i] /= kSum;
  }

  const heightmap = new Float64Array(RES * RES);
  const tsmTempBuf = new Float64Array(RES * RES);

  // Horizontal pass: rawDem → tsmTempBuf
  for (let hy = 0; hy < RES; hy++) {
    const rowBase = hy * RES;
    for (let hx = 0; hx < RES; hx++) {
      let sum = 0;
      for (let k = -tsmRadius; k <= tsmRadius; k++) {
        let sx = hx + k;
        if (sx < 0) sx = 0;
        else if (sx >= RES) sx = RES - 1;
        sum += rawDem[rowBase + sx] * tsmKernel[k + tsmRadius];
      }
      tsmTempBuf[rowBase + hx] = sum;
    }
  }

  // Vertical pass: tsmTempBuf → heightmap
  for (let hx = 0; hx < RES; hx++) {
    for (let hy = 0; hy < RES; hy++) {
      let sum = 0;
      for (let k = -tsmRadius; k <= tsmRadius; k++) {
        let sy = hy + k;
        if (sy < 0) sy = 0;
        else if (sy >= RES) sy = RES - 1;
        sum += tsmTempBuf[sy * RES + hx] * tsmKernel[k + tsmRadius];
      }
      heightmap[hy * RES + hx] = sum;
    }
  }

  console.log(`  Mode: smoothed DEM (sigma=${TERRAIN_SMOOTH_SIGMA_M}m = ${tsmSigmaCells.toFixed(1)} cells, radius=${tsmRadius})`);

  // ─── Ravine Carving ──────────────────────────────────────────
  //
  // Detect big negative features (ravines, gullies) as connected
  // regions where rawDem is significantly below the synthetic surface.
  // Carve each qualifying region as a smooth Gaussian depression —
  // this gives us visible ravines without importing DEM grid noise.
  //
  // Tunable parameters (safe defaults — see notes at bottom of task)
  const RAVINE_MIN_DEPTH_M       = 3.0;   // cell counts as ravine if >= this deep below surface
  const RAVINE_MIN_AREA_CELLS    = 2000;  // min region size (rejects noise)
  const RAVINE_MAX_AREA_FRAC     = 0.25;  // reject regions bigger than this fraction of hole
  const RAVINE_MAX_PLAYABLE_FRAC = 0.05;  // skip region if more than 5% is in playable zones
  const RAVINE_KERNEL_SIGMA_M    = 8.0;   // Gaussian falloff (softness of carve edges)
  const RAVINE_DEPTH_PERCENTILE  = 0.20;  // use mean of deepest 20% of region cells as target depth

  // Step 1: High-pass filter on rawDem to isolate sharp local features.
  // ravineResidual = rawDem - blur(rawDem, largeRadius)
  // This is independent of the synthetic surface — the spline can't
  // bias the detection.
  //
  // Blur radius tuned to be much larger than ravine width so the
  // ravine is preserved in the residual. ~15m sigma works well:
  // big enough to smooth over ravines (5-15m typical width) so the
  // blur reflects surrounding high-ground elevation, small enough
  // to follow overall hole slope so slope cancels in subtraction.
  const RAVINE_DETECT_SIGMA_M = 15.0;
  const detectMetersPerCell = ((terrainWidthM + terrainLengthM) / 2) / (RES - 1);
  const detectSigmaCells = RAVINE_DETECT_SIGMA_M / detectMetersPerCell;
  const detectRadius = Math.max(1, Math.ceil(3 * detectSigmaCells));
  const detectKernelSize = 2 * detectRadius + 1;
  const detectKernel = new Float64Array(detectKernelSize);
  {
    const s2 = 2 * detectSigmaCells * detectSigmaCells;
    let kSum = 0;
    for (let i = 0; i < detectKernelSize; i++) {
      const x = i - detectRadius;
      detectKernel[i] = Math.exp(-(x * x) / s2);
      kSum += detectKernel[i];
    }
    for (let i = 0; i < detectKernelSize; i++) detectKernel[i] /= kSum;
  }

  // Separable Gaussian blur on rawDem → blurredDem
  const TOTAL_CELLS = RES * RES;
  const blurredDem = new Float64Array(TOTAL_CELLS);
  const tempBuf = new Float64Array(TOTAL_CELLS);

  // Horizontal pass: rawDem → tempBuf
  for (let hy = 0; hy < RES; hy++) {
    const rowBase = hy * RES;
    for (let hx = 0; hx < RES; hx++) {
      let sum = 0;
      for (let k = -detectRadius; k <= detectRadius; k++) {
        let sx = hx + k;
        if (sx < 0) sx = 0;
        else if (sx >= RES) sx = RES - 1;
        sum += rawDem[rowBase + sx] * detectKernel[k + detectRadius];
      }
      tempBuf[rowBase + hx] = sum;
    }
  }

  // Vertical pass: tempBuf → blurredDem
  for (let hx = 0; hx < RES; hx++) {
    for (let hy = 0; hy < RES; hy++) {
      let sum = 0;
      for (let k = -detectRadius; k <= detectRadius; k++) {
        let sy = hy + k;
        if (sy < 0) sy = 0;
        else if (sy >= RES) sy = RES - 1;
        sum += tempBuf[sy * RES + hx] * detectKernel[k + detectRadius];
      }
      blurredDem[hy * RES + hx] = sum;
    }
  }

  // Residual: how much each cell differs from the smoothed DEM baseline
  // Negative = below surroundings (ravines, pits), positive = above (mounds, ridges)
  const ravineResidual = new Float64Array(TOTAL_CELLS);
  for (let i = 0; i < TOTAL_CELLS; i++) {
    ravineResidual[i] = rawDem[i] - blurredDem[i];
  }

  console.log(`  Ravine detection: high-pass against ${RAVINE_DETECT_SIGMA_M}m blur ` +
    `(sigma=${detectSigmaCells.toFixed(1)} cells, radius=${detectRadius})`);

  // Step 2: Build ravine-candidate mask — cells more than RAVINE_MIN_DEPTH_M below surface
  const ravineCandidate = new Uint8Array(TOTAL_CELLS);
  for (let i = 0; i < TOTAL_CELLS; i++) {
    if (ravineResidual[i] < -RAVINE_MIN_DEPTH_M) ravineCandidate[i] = 1;
  }

  // Step 3: Build playable mask at heightmap resolution (for overlap check)
  const isPlayable = new Uint8Array(TOTAL_CELLS);
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);
      const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      if (playableZones.has(zoneGrid[zy * zw + zx])) {
        isPlayable[hy * RES + hx] = 1;
      }
    }
  }

  // Step 4: Flood-fill connected components in the candidate mask (4-connectivity).
  // Push neighbours conditionally to avoid bloating the stack.
  const regionLabel = new Int32Array(TOTAL_CELLS); // 0 = unlabeled, 1+ = region id
  let numRegions = 0;
  const floodStack = [];

  for (let startIdx = 0; startIdx < TOTAL_CELLS; startIdx++) {
    if (!ravineCandidate[startIdx] || regionLabel[startIdx] !== 0) continue;

    numRegions++;
    const label = numRegions;
    floodStack.length = 0;
    floodStack.push(startIdx);
    regionLabel[startIdx] = label;

    while (floodStack.length > 0) {
      const idx = floodStack.pop();
      const hx = idx % RES;
      const hy = (idx - hx) / RES;

      // 4-connectivity
      if (hx > 0) {
        const n = idx - 1;
        if (ravineCandidate[n] && regionLabel[n] === 0) {
          regionLabel[n] = label;
          floodStack.push(n);
        }
      }
      if (hx < RES - 1) {
        const n = idx + 1;
        if (ravineCandidate[n] && regionLabel[n] === 0) {
          regionLabel[n] = label;
          floodStack.push(n);
        }
      }
      if (hy > 0) {
        const n = idx - RES;
        if (ravineCandidate[n] && regionLabel[n] === 0) {
          regionLabel[n] = label;
          floodStack.push(n);
        }
      }
      if (hy < RES - 1) {
        const n = idx + RES;
        if (ravineCandidate[n] && regionLabel[n] === 0) {
          regionLabel[n] = label;
          floodStack.push(n);
        }
      }
    }
  }

  // Step 5: Gather per-region stats and decide which to carve
  const regionStats = new Array(numRegions + 1); // index 0 unused
  for (let r = 1; r <= numRegions; r++) {
    regionStats[r] = { cells: [], depths: [], playableCount: 0 };
  }

  for (let idx = 0; idx < TOTAL_CELLS; idx++) {
    const r = regionLabel[idx];
    if (r === 0) continue;
    regionStats[r].cells.push(idx);
    regionStats[r].depths.push(ravineResidual[idx]);
    if (isPlayable[idx]) regionStats[r].playableCount++;
  }

  const MAX_AREA_CELLS = Math.floor(TOTAL_CELLS * RAVINE_MAX_AREA_FRAC);
  const carvedRegions = [];

  for (let r = 1; r <= numRegions; r++) {
    const stats = regionStats[r];
    const area = stats.cells.length;

    if (area < RAVINE_MIN_AREA_CELLS) continue;
    if (area > MAX_AREA_CELLS) {
      console.log(`    Region #${r}: area=${area} cells — REJECTED (too big, > ${(RAVINE_MAX_AREA_FRAC * 100).toFixed(0)}% of hole)`);
      continue;
    }
    const playableFrac = stats.playableCount / area;
    if (playableFrac > RAVINE_MAX_PLAYABLE_FRAC) {
      console.log(`    Region #${r}: area=${area}, playable=${(playableFrac * 100).toFixed(1)}% — REJECTED (overlaps playable)`);
      continue;
    }

    // Target depth = mean of deepest N% of cells (avoids outliers dominating)
    stats.depths.sort((a, b) => a - b); // ascending (most negative first)
    const deepestCount = Math.max(1, Math.floor(stats.depths.length * RAVINE_DEPTH_PERCENTILE));
    let depthSum = 0;
    for (let i = 0; i < deepestCount; i++) depthSum += stats.depths[i];
    const targetDepth = depthSum / deepestCount; // negative (below surface), meters

    carvedRegions.push({
      id: r,
      area,
      playableFrac,
      targetDepth,
      cells: stats.cells,
    });
  }

  console.log(`  Ravine detection: ${numRegions} candidate regions, ${carvedRegions.length} qualifying for carve`);
  for (const region of carvedRegions) {
    console.log(`    Carve Region #${region.id}: area=${region.area} cells, ` +
      `playable=${(region.playableFrac * 100).toFixed(1)}%, ` +
      `depth=${region.targetDepth.toFixed(1)}m`);
  }

  // Step 6: Carve each qualifying region with a separable Gaussian blur.
  //
  // Approach:
  //   - Build a source field: region cells = targetDepth, everywhere else = 0
  //   - Apply a separable Gaussian blur (horizontal pass then vertical pass)
  //   - Rescale so the deepest point of the blurred field = targetDepth
  //     (the blur inevitably shallows the peak)
  //   - Add to heightmap
  //
  // Two buffers are reused across all regions instead of allocating fresh
  // buffers per region — saves ~32 MB per extra region per blur buffer.

  if (carvedRegions.length > 0) {
    const metersPerCell = ((terrainWidthM + terrainLengthM) / 2) / (RES - 1);
    const sigmaCells = RAVINE_KERNEL_SIGMA_M / metersPerCell;

    // Build 1D Gaussian kernel with radius = ceil(3 * sigma)
    const kernelRadius = Math.max(1, Math.ceil(3 * sigmaCells));
    const kernelSize = 2 * kernelRadius + 1;
    const kernel = new Float64Array(kernelSize);
    {
      const s2 = 2 * sigmaCells * sigmaCells;
      let kSum = 0;
      for (let i = 0; i < kernelSize; i++) {
        const x = i - kernelRadius;
        kernel[i] = Math.exp(-(x * x) / s2);
        kSum += kernel[i];
      }
      for (let i = 0; i < kernelSize; i++) kernel[i] /= kSum;
    }
    console.log(`  Ravine carving: sigma=${RAVINE_KERNEL_SIGMA_M}m (${sigmaCells.toFixed(1)} cells), ` +
      `kernel radius=${kernelRadius} cells, size=${kernelSize}`);

    // Reusable buffers for blur
    const bufA = new Float64Array(TOTAL_CELLS);
    const bufB = new Float64Array(TOTAL_CELLS);

    for (const region of carvedRegions) {
      // Clear bufA and write source field
      bufA.fill(0);
      for (const idx of region.cells) bufA[idx] = region.targetDepth;

      // Horizontal pass: bufA -> bufB
      for (let hy = 0; hy < RES; hy++) {
        const rowBase = hy * RES;
        for (let hx = 0; hx < RES; hx++) {
          let sum = 0;
          for (let k = -kernelRadius; k <= kernelRadius; k++) {
            let sx = hx + k;
            if (sx < 0) sx = 0;
            else if (sx >= RES) sx = RES - 1;
            sum += bufA[rowBase + sx] * kernel[k + kernelRadius];
          }
          bufB[rowBase + hx] = sum;
        }
      }

      // Vertical pass: bufB -> bufA
      for (let hx = 0; hx < RES; hx++) {
        for (let hy = 0; hy < RES; hy++) {
          let sum = 0;
          for (let k = -kernelRadius; k <= kernelRadius; k++) {
            let sy = hy + k;
            if (sy < 0) sy = 0;
            else if (sy >= RES) sy = RES - 1;
            sum += bufB[sy * RES + hx] * kernel[k + kernelRadius];
          }
          bufA[hy * RES + hx] = sum;
        }
      }

      // Find deepest value in blurred field (most negative)
      let minAfter = 0;
      for (let i = 0; i < TOTAL_CELLS; i++) {
        if (bufA[i] < minAfter) minAfter = bufA[i];
      }

      // Rescale so deepest = targetDepth and apply to full heightmap.
      // The Gaussian naturally tapers to ~1% at 3*sigma from the region
      // edge, so the fairway effect is negligible while the OB/rough
      // transition is smooth with no artificial step.
      if (minAfter < -1e-6) {
        const rescale = region.targetDepth / minAfter;
        for (let i = 0; i < TOTAL_CELLS; i++) {
          heightmap[i] += bufA[i] * rescale;
        }
      }
    }
  }
  // ─── End Ravine Carving ──────────────────────────────────────

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
