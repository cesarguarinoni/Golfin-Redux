#!/usr/bin/env node
/**
 * generate-terrain.mjs — Step 5: Procedural heightmap generation
 *
 * Usage:
 *   node scripts/generate-terrain.mjs lomond-country-club 1      # single hole
 *   node scripts/generate-terrain.mjs lomond-country-club --all   # all 18
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { perlin2D, blur2D } from './lib/terrain.mjs';
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

const TERRAIN_HINTS = [
  { pattern: '打ち上げ',     type: 'uphill' },
  { pattern: '打ち下ろし',   type: 'downhill' },
  { pattern: '二段',         type: 'two_tier' },
  { pattern: '左ドッグレッグ', type: 'dogleg_left' },
  { pattern: '右ドッグレッグ', type: 'dogleg_right' },
  { pattern: 'ドッグレッグ',   type: 'dogleg' },
  { pattern: '傾斜',         type: 'slope' },
  { pattern: '池',           type: 'pond' },
  { pattern: '谷',           type: 'valley' },
];

function parseHints(descriptionJp) {
  if (!descriptionJp) return [];
  return TERRAIN_HINTS.filter(h => descriptionJp.includes(h.pattern));
}

// ---------------------------------------------------------------------------
// DEM5A loading and sampling
// ---------------------------------------------------------------------------

const DEM_ZOOM = 15;

async function loadDemTiles(courseId, bounds) {
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
// Zone-aware smoothing
// ---------------------------------------------------------------------------

function buildZoneMask(zoneGrid, zw, zh, targetZone, res) {
  const mask = new Uint8Array(res * res);
  for (let hy = 0; hy < res; hy++) {
    for (let hx = 0; hx < res; hx++) {
      const nx = hx / (res - 1);
      const ny = hy / (res - 1);
      const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      if (zoneGrid[zy * zw + zx] === targetZone) {
        mask[hy * res + hx] = 1;
      }
    }
  }
  return mask;
}

function zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, targetZone, passes, res) {
  const mask = buildZoneMask(zoneGrid, zw, zh, targetZone, res);

  let src = new Float64Array(heightmap);
  for (let p = 0; p < passes; p++) {
    const dst = new Float64Array(src);
    for (let hy = 0; hy < res; hy++) {
      for (let hx = 0; hx < res; hx++) {
        if (!mask[hy * res + hx]) continue;
        let sum = 0, weight = 0;
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            const nx = hx + dx, ny = hy + dy;
            if (nx < 0 || nx >= res || ny < 0 || ny >= res) continue;
            const w = (dx === 0 && dy === 0) ? 4 :
                      (dx === 0 || dy === 0) ? 2 : 1;
            sum += src[ny * res + nx] * w;
            weight += w;
          }
        }
        dst[hy * res + hx] = sum / weight;
      }
    }
    src = dst;
  }

  for (let i = 0; i < res * res; i++) {
    if (mask[i]) heightmap[i] = src[i];
  }
}

function flattenZone(heightmap, zoneGrid, zw, zh, targetZone, res) {
  const mask = buildZoneMask(zoneGrid, zw, zh, targetZone, res);

  let sum = 0, count = 0;
  for (let i = 0; i < res * res; i++) {
    if (mask[i]) { sum += heightmap[i]; count++; }
  }
  if (count === 0) return;
  const avg = sum / count;

  for (let i = 0; i < res * res; i++) {
    if (mask[i]) heightmap[i] = avg;
  }
}

// ---------------------------------------------------------------------------
// Plane-fitting per zone (replaces blur-based smoothing)
// ---------------------------------------------------------------------------

/**
 * Least-squares plane fit: h = a*x + b*y + c
 * Uses Cramer's rule on 3x3 normal equations.
 * @param {{x:number, y:number, h:number}[]} points
 * @returns {{a:number, b:number, c:number}}
 */
function fitPlane(points) {
  const n = points.length;
  if (n < 3) {
    const avg = points.reduce((s, p) => s + p.h, 0) / (n || 1);
    return { a: 0, b: 0, c: avg };
  }

  let sx = 0, sy = 0, sh = 0;
  let sxx = 0, syy = 0, sxy = 0;
  let sxh = 0, syh = 0;

  for (const p of points) {
    sx += p.x;   sy += p.y;   sh += p.h;
    sxx += p.x * p.x;
    syy += p.y * p.y;
    sxy += p.x * p.y;
    sxh += p.x * p.h;
    syh += p.y * p.h;
  }

  const det =
    sxx * (syy * n - sy * sy) -
    sxy * (sxy * n - sy * sx) +
    sx  * (sxy * sy - syy * sx);

  if (Math.abs(det) < 1e-12) {
    return { a: 0, b: 0, c: sh / n };
  }

  function det3(m) {
    return m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1])
         - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
         + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]);
  }

  const a = det3([
    [sxh, sxy, sx],
    [syh, syy, sy],
    [sh,  sy,  n ],
  ]) / det;

  const b = det3([
    [sxx, sxh, sx],
    [sxy, syh, sy],
    [sx,  sh,  n ],
  ]) / det;

  const c = det3([
    [sxx, sxy, sxh],
    [sxy, syy, syh],
    [sx,  sy,  sh ],
  ]) / det;

  return { a, b, c };
}

/**
 * Flood-fill connected components in a binary mask.
 * Returns array of region masks, each a Uint8Array of same size.
 */
function floodFillRegions(mask, res) {
  const visited = new Uint8Array(res * res);
  const regions = [];

  for (let i = 0; i < res * res; i++) {
    if (!mask[i] || visited[i]) continue;

    const regionMask = new Uint8Array(res * res);
    const stack = [i];
    while (stack.length > 0) {
      const idx = stack.pop();
      if (visited[idx]) continue;
      visited[idx] = 1;
      regionMask[idx] = 1;

      const x = idx % res;
      const y = (idx - x) / res;
      if (x > 0       && mask[idx - 1]   && !visited[idx - 1])   stack.push(idx - 1);
      if (x < res - 1 && mask[idx + 1]   && !visited[idx + 1])   stack.push(idx + 1);
      if (y > 0       && mask[idx - res] && !visited[idx - res]) stack.push(idx - res);
      if (y < res - 1 && mask[idx + res] && !visited[idx + res]) stack.push(idx + res);
    }

    regions.push(regionMask);
  }

  return regions;
}

/**
 * Apply a fitted plane to the heightmap, blending in a fraction of the
 * residual (raw DEM minus plane) for natural variation.
 * If residualFraction > 0, the residual is blurred (2 zone-masked passes)
 * before blending to produce smooth rolling hills.
 */
function applyPlaneWithResidual(heightmap, rawDem, regionMask, plane,
                                 residualFraction, res) {
  if (residualFraction <= 0) {
    // Pure plane — no residual
    for (let hy = 0; hy < res; hy++) {
      for (let hx = 0; hx < res; hx++) {
        const idx = hy * res + hx;
        if (!regionMask[idx]) continue;
        heightmap[idx] = plane.a * hx + plane.b * hy + plane.c;
      }
    }
    return;
  }

  // Build residual array
  const residual = new Float64Array(res * res);
  for (let hy = 0; hy < res; hy++) {
    for (let hx = 0; hx < res; hx++) {
      const idx = hy * res + hx;
      if (!regionMask[idx]) continue;
      const planeH = plane.a * hx + plane.b * hy + plane.c;
      residual[idx] = rawDem[idx] - planeH;
    }
  }

  // Blur residual (2 zone-masked passes) for smooth rolling hills
  let src = residual;
  for (let p = 0; p < 2; p++) {
    const dst = new Float64Array(src);
    for (let hy = 0; hy < res; hy++) {
      for (let hx = 0; hx < res; hx++) {
        const idx = hy * res + hx;
        if (!regionMask[idx]) continue;
        let sum = 0, weight = 0;
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            const nx = hx + dx, ny = hy + dy;
            if (nx < 0 || nx >= res || ny < 0 || ny >= res) continue;
            const w = (dx === 0 && dy === 0) ? 4 :
                      (dx === 0 || dy === 0) ? 2 : 1;
            sum += src[ny * res + nx] * w;
            weight += w;
          }
        }
        dst[idx] = sum / weight;
      }
    }
    src = dst;
  }

  // Apply plane + blurred residual
  for (let hy = 0; hy < res; hy++) {
    for (let hx = 0; hx < res; hx++) {
      const idx = hy * res + hx;
      if (!regionMask[idx]) continue;
      const planeH = plane.a * hx + plane.b * hy + plane.c;
      heightmap[idx] = planeH + src[idx] * residualFraction;
    }
  }
}

/**
 * Orchestrator: fit planes per connected region of a zone, apply with residual.
 * @param {Float64Array} heightmap - output heightmap (modified in place)
 * @param {Float64Array} rawDem - original DEM values (before water sentinel)
 * @param {Buffer} zoneGrid - zone classification grid
 * @param {number} zw - zone grid width
 * @param {number} zh - zone grid height
 * @param {number} targetZone - zone index
 * @param {number} residualFraction - 0.0 (pure plane) to 1.0 (pure DEM)
 * @param {number} res - heightmap resolution (1025)
 */
function planeFitZone(heightmap, rawDem, zoneGrid, zw, zh, targetZone,
                       residualFraction, res) {
  const mask = buildZoneMask(zoneGrid, zw, zh, targetZone, res);
  const regions = floodFillRegions(mask, res);

  const zoneName = Object.keys(ZONES).find(k => ZONES[k] === targetZone) || `zone${targetZone}`;

  for (let r = 0; r < regions.length; r++) {
    const regionMask = regions[r];

    // Collect (x, y, h) from raw DEM for this region
    const points = [];
    for (let hy = 0; hy < res; hy++) {
      for (let hx = 0; hx < res; hx++) {
        const idx = hy * res + hx;
        if (!regionMask[idx]) continue;
        const h = rawDem[idx];
        if (h > -9000 && !isNaN(h)) {
          points.push({ x: hx, y: hy, h });
        }
      }
    }

    if (points.length === 0) continue;

    const plane = fitPlane(points);
    applyPlaneWithResidual(heightmap, rawDem, regionMask, plane,
                            residualFraction, res);

    // Log plane stats
    const slopeX = (plane.a * (res - 1)).toFixed(2);  // total elevation change across X
    const slopeY = (plane.b * (res - 1)).toFixed(2);  // total elevation change across Y
    console.log(`    ${zoneName} region ${r + 1}/${regions.length}: ` +
      `${points.length} pts, slope dX=${slopeX}m dY=${slopeY}m, ` +
      `residual=${(residualFraction * 100).toFixed(0)}%`);
  }
}

// ---------------------------------------------------------------------------
// Quadratic surface fitting (whole-hole, replaces single plane)
// ---------------------------------------------------------------------------

/**
 * Least-squares quadratic surface fit:
 *   h = a*x² + b*y² + c*x*y + d*x + e*y + f
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
  // Row of A: [x², y², xy, x, y, 1]
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
// DEM-based heightmap generation
// ---------------------------------------------------------------------------

async function generateHeightmapDEM(holeNumber, holeData, teesData, zonesData, extractMeta, config, courseId, geoAlign) {
  const terrainDefaults = config.terrain_defaults;

  const backYards = holeData.tees.back.yards;
  const backMeters = backYards * 0.9144;
  const terrainLength = backMeters * 1.3;
  const aspectRatio = extractMeta.aspect_ratio || 1.2;
  const terrainWidth = terrainLength / aspectRatio;

  const zoneGrid = Buffer.from(zonesData.grid, 'base64');
  const zw = zonesData.source_dimensions.width;
  const zh = zonesData.source_dimensions.height;

  // Green & tee centroids (same as Perlin path)
  let greenSumX = 0, greenSumY = 0, greenCount = 0;
  for (let y = 0; y < zh; y++) {
    for (let x = 0; x < zw; x++) {
      if (zoneGrid[y * zw + x] === ZONES.green) {
        greenSumX += x; greenSumY += y; greenCount++;
      }
    }
  }
  const greenCentroid = greenCount > 0
    ? { x: greenSumX / greenCount / zw, y: greenSumY / greenCount / zh }
    : { x: 0.5, y: 0.15 };

  const foundTees = teesData.tees.filter(t => t.normalized);
  const teeCentroid = foundTees.length > 0
    ? {
        x: foundTees.reduce((s, t) => s + t.normalized.x, 0) / foundTees.length,
        y: foundTees.reduce((s, t) => s + t.normalized.y, 0) / foundTees.length,
      }
    : { x: 0.5, y: 0.85 };

  const hints = parseHints(holeData.description_jp);

  // Load DEM tiles
  const bounds = geoAlign.terrain_bounds_latlon;
  const demTiles = await loadDemTiles(courseId, bounds);
  if (demTiles.length === 0) {
    console.warn('  No DEM tiles found, falling back to Perlin');
    return null; // signal caller to use Perlin fallback
  }

  // Affine coefficients
  const { a, b, c, d, tx, ty } = geoAlign.transform.coefficients;
  const illW = geoAlign.illustration_dimensions.width;
  const illH = geoAlign.illustration_dimensions.height;

  // Step 1: Build raw DEM grid
  const heightmap = new Float64Array(RES * RES);
  let validSum = 0, validCount = 0;

  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);

      // Heightmap pixel → illustration pixel
      const ix = nx * (illW - 1);
      const iy = ny * (illH - 1);

      // Affine → world
      const lon = a * ix + b * iy + tx;
      const lat = c * ix + d * iy + ty;

      const elev = sampleDem5a(demTiles, lat, lon);
      if (elev !== null && !isNaN(elev)) {
        heightmap[hy * RES + hx] = elev;
        validSum += elev;
        validCount++;
      } else {
        heightmap[hy * RES + hx] = NaN;
      }
    }
  }

  if (validCount === 0) {
    console.warn('  DEM returned no valid samples, falling back to Perlin');
    return null;
  }

  const avgElev = validSum / validCount;

  // Fill NaN gaps with average
  for (let i = 0; i < RES * RES; i++) {
    if (isNaN(heightmap[i])) heightmap[i] = avgElev;
  }

  // Record raw elevation range
  let rawMin = Infinity, rawMax = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] < rawMin) rawMin = heightmap[i];
    if (heightmap[i] > rawMax) rawMax = heightmap[i];
  }

  // Save raw DEM before water sentinels (plane-fitting needs original values)
  const rawDem = new Float64Array(heightmap);

  // Step 2: Water zones get the quadratic surface value (same as
  // surrounding terrain). The Unity importer handles shore depression.
  // NO water sentinel needed — the old -9999 approach caused water to
  // sink too deep after normalization with the DEM elevation range.

  // Step 3: Fit ONE quadratic surface to all playable zones combined
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

  // Apply the quadratic surface to the ENTIRE heightmap as the base
  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const idx = hy * RES + hx;
      heightmap[idx] = evalQuadratic(holeSurface, hx, hy);
    }
  }

  // Add residual variation ONLY to non-playable zones (trees, OB, background)
  const residualZones = new Set([
    ZONES.trees, ZONES.ob, ZONES.background,
  ]);
  const RESIDUAL_FRACTION = 0.75; // 75% of real DEM hills in tree/OB/background zones

  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const idx = hy * RES + hx;
      if (heightmap[idx] < -9000) continue;

      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);
      const zx = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zy = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      const zone = zoneGrid[zy * zw + zx];

      if (residualZones.has(zone)) {
        const surfH = evalQuadratic(holeSurface, hx, hy);
        const rawH = rawDem[idx];
        const residual = rawH - surfH;
        heightmap[idx] = surfH + residual * RESIDUAL_FRACTION;
      }
    }
  }

  // Smooth residual zones to remove sharp DEM-grid bumps
  for (const z of [ZONES.trees, ZONES.ob, ZONES.background]) {
    zoneMaskedSmooth(heightmap, zoneGrid, zw, zh, z, 5, RES); // 5 passes for smooth rounded hills
  }

  // Step 4: Green slope (same as Perlin path)
  applyGreenSlope(heightmap, zoneGrid, zw, zh, greenCentroid, holeData.description_jp);

  // Step 5: Normalize — relative elevation
  // No water sentinels — all cells have valid heights
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

  // Single global blur pass to soften zone boundaries
  const smoothed = blur2D(heightmap, RES, RES, 1);

  // Recalculate min/max
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

  let uint16Min = 65535, uint16Max = 0;
  for (const v of uint16Data) {
    if (v < uint16Min) uint16Min = v;
    if (v > uint16Max) uint16Max = v;
  }

  // Slope drop: difference between tee and green average elevation
  const teeAvg = avgElevForZone(smoothed, zoneGrid, zw, zh, ZONES.tee_box, RES);
  const greenAvg = avgElevForZone(smoothed, zoneGrid, zw, zh, ZONES.green, RES);
  const slopeDrop = (teeAvg !== null && greenAvg !== null) ? teeAvg - greenAvg : 0;

  return {
    uint16Data,
    terrainWidth: parseFloat(terrainWidth.toFixed(1)),
    terrainLength: parseFloat(terrainLength.toFixed(1)),
    minElevation: 0,
    maxElevation: parseFloat(playableRange.toFixed(1)),
    slopeDrop: parseFloat(slopeDrop.toFixed(1)),
    greenCentroid,
    teeCentroid,
    hints: hints.map(h => h.type),
    uint16Min,
    uint16Max,
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
    smoothing: {
      fairway_passes: 8,
      semi_rough_passes: 4,
      rough_passes: 3,
      trees_passes: 2,
      global_final_pass: 1,
    },
  };
}

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
// Perlin-based heightmap generation (original, fallback)
// ---------------------------------------------------------------------------

function generateHeightmap(holeNumber, holeData, teesData, zonesData, extractMeta, config) {
  const seed = holeNumber * 1337;
  const terrainDefaults = config.terrain_defaults;

  const backYards = holeData.tees.back.yards;
  const backMeters = backYards * 0.9144;
  const terrainLength = backMeters * 1.3;
  const aspectRatio = extractMeta.aspect_ratio || 1.2;
  const terrainWidth = terrainLength / aspectRatio;

  const zoneGrid = Buffer.from(zonesData.grid, 'base64');
  const zw = zonesData.source_dimensions.width;
  const zh = zonesData.source_dimensions.height;

  let greenSumX = 0, greenSumY = 0, greenCount = 0;
  for (let y = 0; y < zh; y++) {
    for (let x = 0; x < zw; x++) {
      if (zoneGrid[y * zw + x] === ZONES.green) {
        greenSumX += x; greenSumY += y; greenCount++;
      }
    }
  }
  const greenCentroid = greenCount > 0
    ? { x: greenSumX / greenCount / zw, y: greenSumY / greenCount / zh }
    : { x: 0.5, y: 0.15 };

  const foundTees = teesData.tees.filter(t => t.normalized);
  const teeCentroid = foundTees.length > 0
    ? {
        x: foundTees.reduce((s, t) => s + t.normalized.x, 0) / foundTees.length,
        y: foundTees.reduce((s, t) => s + t.normalized.y, 0) / foundTees.length,
      }
    : { x: 0.5, y: 0.85 };

  const hints = parseHints(holeData.description_jp);
  const hintTypes = new Set(hints.map(h => h.type));

  let slopeDrop = 6.0;
  if (hintTypes.has('downhill')) slopeDrop = 12.0;
  if (hintTypes.has('uphill')) slopeDrop = -5.0;
  const [dropMin, dropMax] = terrainDefaults.tee_to_green_drop_range_m;
  if (slopeDrop > 0) slopeDrop = Math.min(slopeDrop, dropMax);

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

      if (hintTypes.has('two_tier')) {
        if (t > 0.5) baseSlope += 2.0;
      }

      // Layer 2: Perlin noise
      let noise = perlin2D(hx * noiseFreq + seed, hy * noiseFreq + seed * 0.7) * noiseAmp;

      // Layer 3: Zone-based modifiers (point-sample for non-bunker zones)
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
          noise *= 0.3;   // smooth but not dead flat
          break;
        case ZONES.cart_path:
          noise *= 0.55;
          break;
      }

      let totalHeight = baseSlope + noise + heightMod;

      if (isWater) {
        totalHeight = -9999;
      }

      heightmap[hy * RES + hx] = totalHeight;
    }
  }

  // Layer 4: Green slope
  applyGreenSlope(heightmap, zoneGrid, zw, zh, greenCentroid, holeData.description_jp);

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

  // Restore water fully
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

  let minVal = 65535, maxVal = 0;
  for (const v of uint16Data) {
    if (v < minVal) minVal = v;
    if (v > maxVal) maxVal = v;
  }

  return {
    uint16Data,
    terrainWidth: parseFloat(terrainWidth.toFixed(1)),
    terrainLength: parseFloat(terrainLength.toFixed(1)),
    minElevation: 0,
    maxElevation: parseFloat(range.toFixed(1)),
    slopeDrop: parseFloat(slopeDrop.toFixed(1)),
    noiseAmp,
    seed,
    greenCentroid,
    teeCentroid,
    hints: hints.map(h => h.type),
    uint16Min: minVal,
    uint16Max: maxVal,
  };
}

function applyGreenSlope(heightmap, zoneGrid, zw, zh, greenCentroid, descJp) {
  let slopeDir = 'front';
  if (descJp) {
    if (descJp.includes('右から傾斜') || descJp.includes('右サイド')) slopeDir = 'right';
    if (descJp.includes('左から傾斜') || descJp.includes('左サイド')) slopeDir = 'left';
    if (descJp.includes('受けグリーン')) slopeDir = 'front';
  }

  const slopeAmount = 0.5;

  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1);
      const ny = hy / (RES - 1);
      const zoneX = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zoneY = Math.min(zh - 1, Math.floor(ny * (zh - 1)));

      if (zoneGrid[zoneY * zw + zoneX] !== ZONES.green) continue;

      let slopeMod = 0;
      switch (slopeDir) {
        case 'front':
          slopeMod = (ny - greenCentroid.y) * slopeAmount * 5;
          break;
        case 'right':
          slopeMod = (nx - greenCentroid.x) * slopeAmount * 5;
          break;
        case 'left':
          slopeMod = (greenCentroid.x - nx) * slopeAmount * 5;
          break;
      }
      heightmap[hy * RES + hx] += slopeMod;
    }
  }
}

function writeHeightmapRaw(outputPath, uint16Data) {
  const buffer = Buffer.alloc(RES * RES * 2);
  for (let i = 0; i < RES * RES; i++) {
    buffer.writeUInt16BE(uint16Data[i], i * 2);
  }
  fs.writeFileSync(outputPath, buffer);
}

async function processHole(courseId, holeNumber, courseJson, config, forcePerlin) {
  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);

  const teesPath = path.join(holeDir, 'tees.json');
  const zonesPath = path.join(holeDir, 'zones.json');
  const extractMetaPath = path.join(holeDir, 'extract-meta.json');

  if (!fs.existsSync(teesPath) || !fs.existsSync(zonesPath) || !fs.existsSync(extractMetaPath)) {
    console.error(`  Missing data files for hole ${holeNumber}`);
    return null;
  }

  const teesData = JSON.parse(fs.readFileSync(teesPath, 'utf-8'));
  const zonesData = JSON.parse(fs.readFileSync(zonesPath, 'utf-8'));
  const extractMeta = JSON.parse(fs.readFileSync(extractMetaPath, 'utf-8'));
  const holeData = courseJson.holes.find(h => h.number === holeNumber);

  if (!holeData) {
    console.error(`  Hole ${holeNumber} not found in course.json`);
    return null;
  }

  // Try DEM path if geo-align.json exists and not forced to Perlin
  let result = null;
  let usedDem = false;
  const geoAlignPath = path.join(holeDir, 'geo-align.json');

  if (!forcePerlin && fs.existsSync(geoAlignPath)) {
    try {
      const geoAlign = JSON.parse(fs.readFileSync(geoAlignPath, 'utf-8'));
      if (geoAlign.transform && geoAlign.terrain_bounds_latlon && geoAlign.illustration_dimensions) {
        result = await generateHeightmapDEM(holeNumber, holeData, teesData, zonesData, extractMeta, config, courseId, geoAlign);
        if (result) usedDem = true;
      }
    } catch (err) {
      console.warn(`  geo-align.json error: ${err.message}, falling back to Perlin`);
    }
  }

  // Fallback to Perlin
  if (!result) {
    result = generateHeightmap(holeNumber, holeData, teesData, zonesData, extractMeta, config);
  }

  // Rotate heightmap 90° CCW to match Unity terrain coordinate swap
  // (contours, anchors, texture all get 90° CCW in the importer)
  const rotated = new Uint16Array(RES * RES);
  for (let y = 0; y < RES; y++) {
    for (let x = 0; x < RES; x++) {
      const srcIdx = y * RES + x;
      const dstX = y;              // old Y becomes new X
      const dstY = x;              // old X becomes new Y
      rotated[dstY * RES + dstX] = result.uint16Data[srcIdx];
    }
  }

  const rawPath = path.join(holeDir, 'heightmap.raw');
  writeHeightmapRaw(rawPath, rotated);

  const meta = {
    hole_number: holeNumber,
    heightmap_file: 'heightmap.raw',
    format: 'uint16be',
    resolution: RES,
    terrain_width_m: result.terrainWidth,
    terrain_length_m: result.terrainLength,
    min_elevation_m: result.minElevation,
    max_elevation_m: result.maxElevation,
    source: usedDem ? 'dem5a' : 'perlin',
    slope_direction: result.slopeDrop >= 0 ? 'tee_to_green' : 'green_to_tee',
    slope_drop_m: result.slopeDrop,
    green_centroid_normalized: {
      x: parseFloat(result.greenCentroid.x.toFixed(3)),
      y: parseFloat(result.greenCentroid.y.toFixed(3)),
    },
    tee_centroid_normalized: {
      x: parseFloat(result.teeCentroid.x.toFixed(3)),
      y: parseFloat(result.teeCentroid.y.toFixed(3)),
    },
    hints: result.hints,
  };

  // DEM-specific metadata
  if (usedDem) {
    meta.dem_source = result.demSource;
    meta.geo_align_file = 'geo-align.json';
    meta.smoothing = result.smoothing;
  } else {
    meta.noise_amplitude_m = result.noiseAmp;
    meta.seed = result.seed;
  }

  fs.writeFileSync(
    path.join(holeDir, 'terrain-meta.json'),
    JSON.stringify(meta, null, 2),
    'utf-8'
  );

  return { meta, uint16Min: result.uint16Min, uint16Max: result.uint16Max, usedDem };
}

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
  const coursePath = path.join(ROOT, 'output', courseId, 'course.json');
  if (!fs.existsSync(configPath) || !fs.existsSync(coursePath)) {
    console.error('Config or course.json not found');
    process.exit(1);
  }

  const config = JSON.parse(fs.readFileSync(configPath, 'utf-8'));
  const courseJson = JSON.parse(fs.readFileSync(coursePath, 'utf-8'));

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
    const result = await processHole(courseId, h, courseJson, config, forcePerlin);
    if (!result) { console.log('FAILED'); continue; }

    const m = result.meta;
    const src = result.usedDem ? 'DEM5A' : 'Perlin';
    const hints = m.hints.length > 0 ? ` [${m.hints.join(', ')}]` : '';
    const demInfo = result.usedDem && m.dem_source
      ? `  raw=[${m.dem_source.raw_elevation_range_m.min},${m.dem_source.raw_elevation_range_m.max}]m ASL`
      : '';
    const noGeo = !result.usedDem && !forcePerlin ? '  [no geo-align.json]' : '';
    console.log(`OK (${src})  ${m.terrain_width_m}×${m.terrain_length_m}m  elev=${m.max_elevation_m}m  drop=${m.slope_drop_m}m${demInfo}${noGeo}${hints}`);
  }

  console.log('\nDone.');
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
