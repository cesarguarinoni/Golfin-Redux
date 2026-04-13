#!/usr/bin/env node
/**
 * export-hole.mjs — Step 6: Assemble final export package for Unity import
 *
 * Usage:
 *   node scripts/export-hole.mjs lomond-country-club 1      # single hole
 *   node scripts/export-hole.mjs lomond-country-club --all   # all 18
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

/**
 * Trace the outer border of a connected region of pixels.
 * Returns ordered array of [x, y] pixel coordinates forming the boundary.
 */
/**
 * Trace the outer border of a connected region.
 * Direction-aware walk: at each step, prefer continuing in the same
 * direction (or close to it) as the previous step. This robustly follows
 * the contour without the backtrack-direction bugs of a naive Moore trace.
 */
function traceBorder(grid, w, h, pixels, zoneValue) {
  const pixelSet = new Set();
  for (const [px, py] of pixels) {
    pixelSet.add(py * w + px);
  }

  if (pixels.length === 0) return [];

  const isInRegion = (x, y) => {
    if (x < 0 || x >= w || y < 0 || y >= h) return false;
    return pixelSet.has(y * w + x);
  };

  // Find all border pixels (4-connected: has a non-region neighbor)
  const borderSet = new Set();
  let startX = w, startY = h;

  for (const [px, py] of pixels) {
    const nb = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
    if (nb.some(([nx, ny]) => !isInRegion(nx, ny))) {
      borderSet.add(py * w + px);
      if (py < startY || (py === startY && px < startX)) {
        startX = px;
        startY = py;
      }
    }
  }

  if (borderSet.size <= 1) return borderSet.size === 1 ? [[startX, startY]] : [];

  // 8-connected directions CW from E
  const dx = [1, 1, 0, -1, -1, -1,  0,  1];
  const dy = [0, 1, 1,  1,  0, -1, -1, -1];

  const visited = new Set();
  visited.add(startY * w + startX);
  const ordered = [[startX, startY]];
  let cx = startX, cy = startY;
  let prevDir = 0; // start heading east

  const totalBorder = borderSet.size;

  for (let step = 0; step < totalBorder + 10; step++) {
    // Among 8-connected unvisited border neighbors, pick the one
    // closest to continuing in the same direction
    let bestDir = -1;
    let bestScore = -Infinity;

    for (let i = 0; i < 8; i++) {
      const nx = cx + dx[i];
      const ny = cy + dy[i];
      const key = ny * w + nx;
      if (!borderSet.has(key) || visited.has(key)) continue;

      // Prefer smallest angular difference from previous direction
      let diff = i - prevDir;
      if (diff > 4) diff -= 8;
      if (diff < -4) diff += 8;
      const score = -Math.abs(diff);

      if (score > bestScore) {
        bestScore = score;
        bestDir = i;
      }
    }

    if (bestDir === -1) break;

    cx = cx + dx[bestDir];
    cy = cy + dy[bestDir];
    visited.add(cy * w + cx);
    ordered.push([cx, cy]);
    prevDir = bestDir;
  }

  return ordered;
}

/**
 * Ramer-Douglas-Peucker line simplification.
 */
function simplifyPolygon(points, epsilon) {
  if (points.length <= 2) return points;

  let maxDist = 0;
  let maxIdx = 0;
  const start = points[0];
  const end = points[points.length - 1];

  for (let i = 1; i < points.length - 1; i++) {
    const d = perpendicularDistance(points[i], start, end);
    if (d > maxDist) {
      maxDist = d;
      maxIdx = i;
    }
  }

  if (maxDist > epsilon) {
    const left = simplifyPolygon(points.slice(0, maxIdx + 1), epsilon);
    const right = simplifyPolygon(points.slice(maxIdx), epsilon);
    return left.slice(0, -1).concat(right);
  } else {
    return [start, end];
  }
}

function perpendicularDistance(point, lineStart, lineEnd) {
  const dx = lineEnd.x - lineStart.x;
  const dz = lineEnd.z - lineStart.z;
  const lenSq = dx * dx + dz * dz;

  if (lenSq === 0) {
    const ex = point.x - lineStart.x;
    const ez = point.z - lineStart.z;
    return Math.sqrt(ex * ex + ez * ez);
  }

  const num = Math.abs(dx * (lineStart.z - point.z) - (lineStart.x - point.x) * dz);
  return num / Math.sqrt(lenSq);
}

/**
 * Chaikin's corner-cutting subdivision for smoothing polygons.
 * Each iteration replaces each edge with two new points at 25%/75%,
 * rounding off corners. For closed polygons.
 */
function smoothPolygon(polygon, iterations = 2) {
  let pts = polygon;
  for (let iter = 0; iter < iterations; iter++) {
    const smoothed = [];
    const n = pts.length;
    for (let i = 0; i < n; i++) {
      const curr = pts[i];
      const next = pts[(i + 1) % n];
      // Q = 75% curr + 25% next
      smoothed.push({
        x: parseFloat((0.75 * curr.x + 0.25 * next.x).toFixed(2)),
        z: parseFloat((0.75 * curr.z + 0.25 * next.z).toFixed(2)),
      });
      // R = 25% curr + 75% next
      smoothed.push({
        x: parseFloat((0.25 * curr.x + 0.75 * next.x).toFixed(2)),
        z: parseFloat((0.25 * curr.z + 0.75 * next.z).toFixed(2)),
      });
    }
    pts = smoothed;
  }
  return pts;
}

/**
 * Chaikin smoothing for an OPEN polyline (does not wrap around).
 * Preserves the first and last endpoints.
 */
function smoothPolyline(polyline, iterations = 2) {
  let pts = polyline;
  for (let iter = 0; iter < iterations; iter++) {
    if (pts.length < 2) break;
    const smoothed = [pts[0]]; // keep first endpoint
    for (let i = 0; i < pts.length - 1; i++) {
      const curr = pts[i];
      const next = pts[i + 1];
      smoothed.push({
        x: parseFloat((0.75 * curr.x + 0.25 * next.x).toFixed(2)),
        z: parseFloat((0.75 * curr.z + 0.25 * next.z).toFixed(2)),
      });
      smoothed.push({
        x: parseFloat((0.25 * curr.x + 0.75 * next.x).toFixed(2)),
        z: parseFloat((0.25 * curr.z + 0.75 * next.z).toFixed(2)),
      });
    }
    smoothed.push(pts[pts.length - 1]); // keep last endpoint
    pts = smoothed;
  }
  return pts;
}

/**
 * Ensure polygon has counter-clockwise winding (shoelace formula).
 */
function ensureCCW(polygon) {
  let area = 0;
  for (let i = 0; i < polygon.length; i++) {
    const j = (i + 1) % polygon.length;
    area += polygon[i].x * polygon[j].z;
    area -= polygon[j].x * polygon[i].z;
  }
  if (area > 0) polygon.reverse();
  return polygon;
}

function extractZoneContours(zonesData, terrainMeta, targetZone, minPixels = 8, rdpEpsilon = 2.0, smoothPasses = 2, gridKey = null, gridBuffer = null) {
  const grid = gridBuffer || Buffer.from(
    gridKey ? zonesData[gridKey] : (zonesData.terrain_grid || zonesData.grid),
    'base64'
  );
  const w = zonesData.source_dimensions.width;
  const h = zonesData.source_dimensions.height;
  const visited = new Uint8Array(w * h);

  const tw = terrainMeta.terrain_width_m;
  const tl = terrainMeta.terrain_length_m;

  // Flood-fill to find connected regions
  function floodFill(startX, startY) {
    const pixels = [];
    const stack = [[startX, startY]];
    while (stack.length > 0) {
      const [x, y] = stack.pop();
      if (x < 0 || x >= w || y < 0 || y >= h) continue;
      const idx = y * w + x;
      if (visited[idx] || grid[idx] !== targetZone) continue;
      visited[idx] = 1;
      pixels.push([x, y]);
      stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
    return pixels;
  }

  const regions = [];

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (grid[y * w + x] === targetZone && !visited[y * w + x]) {
        const pixels = floodFill(x, y);
        if (pixels.length < minPixels) continue;

        let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
        for (const [px, py] of pixels) {
          if (px < minX) minX = px;
          if (px > maxX) maxX = px;
          if (py < minY) minY = py;
          if (py > maxY) maxY = py;
        }

        // Center in normalized coordinates (0-1 range within zone grid)
        const normCX = (minX + maxX) / 2 / (w - 1);
        const normCY = (minY + maxY) / 2 / (h - 1);

        // Size in normalized coordinates
        const normW = (maxX - minX + 1) / w;
        const normH = (maxY - minY + 1) / h;

        // Convert to local meter coordinates (same system as anchors)
        const localX = parseFloat(((normCX - 0.5) * tw).toFixed(2));
        const localZ = parseFloat(((normCY - 0.5) * tl).toFixed(2));
        const sizeX = parseFloat((normW * tw).toFixed(2));
        const sizeZ = parseFloat((normH * tl).toFixed(2));

        // --- Trace contour ---
        const borderPixels = traceBorder(grid, w, h, pixels, targetZone);

        // Convert border pixels to local meter coordinates
        let contourMeters = borderPixels.map(([bx, by]) => ({
          x: parseFloat(((bx / (w - 1) - 0.5) * tw).toFixed(2)),
          z: parseFloat(((by / (h - 1) - 0.5) * tl).toFixed(2)),
        }));

        // Simplify then smooth
        // Close the polygon for RDP so the start/end seam gets simplified
        // (otherwise RDP anchors the first & last point, creating a pointy tip)
        const closed = [...contourMeters, contourMeters[0]];  // duplicate first pt
        let simplified = simplifyPolygon(closed, rdpEpsilon);
        // Remove the duplicate closing point
        if (simplified.length > 1 &&
            simplified[0].x === simplified[simplified.length - 1].x &&
            simplified[0].z === simplified[simplified.length - 1].z) {
          simplified = simplified.slice(0, -1);
        }
        contourMeters = smoothPolygon(simplified, smoothPasses);
        contourMeters = ensureCCW(contourMeters);

        regions.push({
          id: regions.length + 1,
          pixel_count: pixels.length,
          contour: contourMeters,
          center_local: { x: localX, z: localZ },
          size_m: { x: sizeX, z: sizeZ },
          center_normalized: {
            x: parseFloat(normCX.toFixed(4)),
            y: parseFloat(normCY.toFixed(4)),
          },
          size_normalized: {
            w: parseFloat(normW.toFixed(4)),
            h: parseFloat(normH.toFixed(4)),
          },
        });
      }
    }
  }

  // Sort by size (largest first)
  regions.sort((a, b) => b.pixel_count - a.pixel_count);
  // Re-assign IDs after sort
  regions.forEach((b, i) => { b.id = i + 1; });

  return regions;
}

/**
 * Extract cart path contours with minimum width enforcement.
 * If a region is narrower than minWidthM, dilate it until it reaches
 * the minimum. This prevents thin hand-painted paths from producing
 * degenerate contours.
 */
function extractCartPathContours(zonesData, terrainMeta, minWidthM = 2.5, minPixels = 15, rdpEpsilon = 1.0, smoothPasses = 2) {
  // Merged grid gives OB priority over cart paths, so zone 8 pixels under OB
  // become zone 9. Use cart_path_mask overlay to recover them.
  const mergedGrid = Buffer.from(zonesData.grid, 'base64');
  const cpMaskBuf = zonesData.cart_path_mask
    ? Buffer.from(zonesData.cart_path_mask, 'base64') : null;
  const grid = Buffer.from(mergedGrid);  // copy
  if (cpMaskBuf) {
    for (let i = 0; i < grid.length; i++) {
      if (cpMaskBuf[i] && grid[i] !== 8) grid[i] = 8;
    }
  }
  // Base zone grid (no overlays) for detecting zone overlaps.
  // cart_path_mask stamping above overwrites original zones with 8, so we
  // need the untouched terrain_grid to know the real underlying zone.
  const terrainGridBuf = zonesData.terrain_grid
    ? Buffer.from(zonesData.terrain_grid, 'base64') : null;
  // Zones where cart paths should NOT overlap — skeleton extraction will
  // clip these pixels so the spine routes around them.
  const overlapZones = new Set([1, 6, 7, 10]); // fairway, bunker, water, tee

  const w = zonesData.source_dimensions.width;
  const h = zonesData.source_dimensions.height;
  const tw = terrainMeta.terrain_width_m;
  const tl = terrainMeta.terrain_length_m;
  const targetZone = 8; // cart path

  // Meters per pixel
  const mppX = tw / w;
  const mppY = tl / h;
  const mpp = (mppX + mppY) / 2;
  const minWidthPx = Math.ceil(minWidthM / mpp);

  // Step 1: Find all cart path pixels, flood-fill into regions
  const visited = new Uint8Array(w * h);
  const regions = [];

  function floodFill(startX, startY) {
    const pixels = [];
    const stack = [[startX, startY]];
    while (stack.length > 0) {
      const [x, y] = stack.pop();
      if (x < 0 || x >= w || y < 0 || y >= h) continue;
      const idx = y * w + x;
      if (visited[idx] || grid[idx] !== targetZone) continue;
      visited[idx] = 1;
      pixels.push([x, y]);
      stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
    return pixels;
  }

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (grid[y * w + x] === targetZone && !visited[y * w + x]) {
        const pixels = floodFill(x, y);
        if (pixels.length >= minPixels) {
          regions.push(pixels);
        }
      }
    }
  }

  // Step 2: For each region, check width and dilate if needed
  const results = [];

  for (const originalPixels of regions) {
    let pixelSet = new Set();
    for (const [px, py] of originalPixels) {
      pixelSet.add(py * w + px);
    }

    // Estimate width: area / longer bbox axis
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    for (const [px, py] of originalPixels) {
      if (px < minX) minX = px;
      if (px > maxX) maxX = px;
      if (py < minY) minY = py;
      if (py > maxY) maxY = py;
    }
    const bboxW = maxX - minX + 1;
    const bboxH = maxY - minY + 1;
    const longerAxis = Math.max(bboxW, bboxH);
    const estWidthPx = originalPixels.length / longerAxis;

    // Dilate if too narrow
    let currentPixels = originalPixels;
    if (estWidthPx < minWidthPx) {
      const dilateRadius = Math.ceil((minWidthPx - estWidthPx) / 2);
      console.log(`    Cart path region: est width ${(estWidthPx * mpp).toFixed(1)}m < ${minWidthM}m, dilating by ${dilateRadius}px`);

      // Only dilate into safe zones (rough, semi-rough, trees, background, OB)
      const safeZones = new Set([0, 3, 4, 5, 9]);
      const dilated = new Set(pixelSet);

      for (let r = 0; r < dilateRadius; r++) {
        const frontier = [];
        for (const key of dilated) {
          const py = Math.floor(key / w);
          const px = key % w;
          const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
          for (const [nx, ny] of neighbors) {
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            const nKey = ny * w + nx;
            if (dilated.has(nKey)) continue;
            if (safeZones.has(grid[nKey])) {
              frontier.push(nKey);
            }
          }
        }
        for (const key of frontier) {
          dilated.add(key);
        }
      }

      currentPixels = [];
      for (const key of dilated) {
        currentPixels.push([key % w, Math.floor(key / w)]);
      }
      pixelSet = dilated;

      // Recompute bounding box after dilation
      minX = Infinity; maxX = -Infinity; minY = Infinity; maxY = -Infinity;
      for (const [px, py] of currentPixels) {
        if (px < minX) minX = px;
        if (px > maxX) maxX = px;
        if (py < minY) minY = py;
        if (py > maxY) maxY = py;
      }
    }

    // Step 3: Run standard contour pipeline on this region
    const borderPixels = traceBorder(grid, w, h, currentPixels, targetZone);

    let contourMeters = borderPixels.map(([bx, by]) => ({
      x: parseFloat(((bx / (w - 1) - 0.5) * tw).toFixed(2)),
      z: parseFloat(((by / (h - 1) - 0.5) * tl).toFixed(2)),
    }));

    if (contourMeters.length < 3) continue;

    // RDP + Chaikin
    const closed = [...contourMeters, contourMeters[0]];
    let simplified = simplifyPolygon(closed, rdpEpsilon);
    if (simplified.length > 1 &&
        simplified[0].x === simplified[simplified.length - 1].x &&
        simplified[0].z === simplified[simplified.length - 1].z) {
      simplified = simplified.slice(0, -1);
    }
    contourMeters = smoothPolygon(simplified, smoothPasses);
    contourMeters = ensureCCW(contourMeters);

    const normCX = (minX + maxX) / 2 / (w - 1);
    const normCY = (minY + maxY) / 2 / (h - 1);
    const normW = (maxX - minX + 1) / w;
    const normH = (maxY - minY + 1) / h;

    // --- Skeleton-based spine extraction with overlap clipping ---
    // Downsample the region mask for cleaner skeletonization.
    // Wide paths (~30px+) produce noisy skeletons at full res.
    // Target skeleton input width ~6-10px for clean results.
    //
    // IMPORTANT: estWidthPx is area/longerAxis — for branching networks
    // this overestimates massively (e.g. 82px for a 7px-wide path network).
    // Cap dsFactor so actual path width (~minWidthPx) stays ≥3px after DS.
    const dsTarget = 3;
    const dsFromEst = Math.round(estWidthPx / dsTarget);
    const dsFromActual = Math.floor(minWidthPx / dsTarget); // preserve actual path width
    const dsFactor = Math.max(1, Math.min(dsFromEst, dsFromActual));
    const dsW = Math.ceil(w / dsFactor);
    const dsH = Math.ceil(h / dsFactor);

    // Build downsampled binary mask, clipping cart path pixels that
    // overlap fairway, bunker, or tee zones. Uses terrain_grid (base
    // zones without overlay masks) because cart_path_mask stamping
    // overwrites the original zone with 8 in the working grid.
    // This prevents spines from running under these zone meshes while
    // preserving junction structure (unlike clipping post-thinning).
    const dsMask = new Uint8Array(dsW * dsH);
    for (const [px, py] of currentPixels) {
      // Skip pixels that overlap forbidden zones in the base terrain grid
      if (terrainGridBuf && overlapZones.has(terrainGridBuf[py * w + px])) continue;
      const dsx = Math.floor(px / dsFactor);
      const dsy = Math.floor(py / dsFactor);
      if (dsx < dsW && dsy < dsH) dsMask[dsy * dsW + dsx] = 1;
    }

    // Also clear DS pixels where the majority of underlying full-res
    // pixels are overlap zones — prevents bleed-through at DS boundaries
    if (terrainGridBuf) {
      for (let dsy = 0; dsy < dsH; dsy++) {
        for (let dsx = 0; dsx < dsW; dsx++) {
          if (!dsMask[dsy * dsW + dsx]) continue;
          let overlapCount = 0, total = 0;
          for (let fy = dsy * dsFactor; fy < Math.min((dsy + 1) * dsFactor, h); fy++) {
            for (let fx = dsx * dsFactor; fx < Math.min((dsx + 1) * dsFactor, w); fx++) {
              if (overlapZones.has(terrainGridBuf[fy * w + fx])) overlapCount++;
              total++;
            }
          }
          if (overlapCount > total * 0.5) dsMask[dsy * dsW + dsx] = 0;
        }
      }
    }

    // Thin to 1-pixel-wide skeleton (on downsampled mask)
    const skeleton = thinSkeleton(dsMask, dsW, dsH);

    // Prune short spur branches (noise from thinning)
    // Use 3x the downsampled path width as threshold — short spurs are edge noise
    const dsEstWidth = Math.max(Math.ceil(estWidthPx / dsFactor), 2);
    const spurThreshold = Math.max(Math.ceil(dsEstWidth * 3), 8);
    pruneSpurs(skeleton, dsW, dsH, spurThreshold);

    // Trace skeleton into separate chains (in downsampled coords)
    const dsChains = traceSkeletonChains(skeleton, dsW, dsH);

    // Convert chains back to full-res pixel coords
    const chains = dsChains.map(chain =>
      chain.map(({ x, y }) => ({
        x: Math.min(x * dsFactor + Math.floor(dsFactor / 2), w - 1),
        y: Math.min(y * dsFactor + Math.floor(dsFactor / 2), h - 1),
      }))
    );

    const regionId = results.length + 1;

    // Minimum spine length in downsampled pixels — skip fragments shorter
    // than ~2x the downsampled path width (prevents noise but keeps real branches)
    const minSpinePixels = Math.max(Math.ceil(dsEstWidth * 2), 5);

    // Filter out short fragment chains before deciding whether the skeleton is usable
    const significantChains = chains.filter(c => c.length >= minSpinePixels);

    // If skeleton produced too many significant chains, it's noise — fall back
    // to the original contour-based farthest-pair spine extraction.
    // Complex cart path networks can have 8-10+ real branches (e.g. hole 18).
    const maxExpectedBranches = 12;
    if (significantChains.length > maxExpectedBranches) {
      console.log(`    Skeleton noisy (${significantChains.length} chains), falling back to contour spine`);
      let spine = extractPathSpine(contourMeters);
      if (spine.length >= 2) {
        const first = spine[0], last = spine[spine.length - 1];
        const ddx = first.x - last.x, ddz = first.z - last.z;
        if (Math.sqrt(ddx * ddx + ddz * ddz) < 1.0) spine = spine.slice(0, -1);
      }
      spine = simplifyPolygon(spine, rdpEpsilon);
      spine = smoothPolyline(spine, smoothPasses);

      results.push({
        id: regionId,
        pixel_count: currentPixels.length,
        contour: contourMeters,
        spine,
        width_m: minWidthM,
        center_local: {
          x: parseFloat(((normCX - 0.5) * tw).toFixed(2)),
          z: parseFloat(((normCY - 0.5) * tl).toFixed(2)),
        },
        size_m: {
          x: parseFloat((normW * tw).toFixed(2)),
          z: parseFloat((normH * tl).toFixed(2)),
        },
        center_normalized: { x: parseFloat(normCX.toFixed(4)), y: parseFloat(normCY.toFixed(4)) },
        size_normalized: { w: parseFloat(normW.toFixed(4)), h: parseFloat(normH.toFixed(4)) },
        dilated: estWidthPx < minWidthPx,
      });
    } else if (significantChains.length === 0) {
      // Fallback: no skeleton chains (e.g., very small region) — use contour centroid as degenerate spine
      results.push({
        id: regionId,
        pixel_count: currentPixels.length,
        contour: contourMeters,
        spine: [{ x: parseFloat(((normCX - 0.5) * tw).toFixed(2)), z: parseFloat(((normCY - 0.5) * tl).toFixed(2)) }],
        width_m: minWidthM,
        center_local: {
          x: parseFloat(((normCX - 0.5) * tw).toFixed(2)),
          z: parseFloat(((normCY - 0.5) * tl).toFixed(2)),
        },
        size_m: {
          x: parseFloat((normW * tw).toFixed(2)),
          z: parseFloat((normH * tl).toFixed(2)),
        },
        center_normalized: { x: parseFloat(normCX.toFixed(4)), y: parseFloat(normCY.toFixed(4)) },
        size_normalized: { w: parseFloat(normW.toFixed(4)), h: parseFloat(normH.toFixed(4)) },
        dilated: estWidthPx < minWidthPx,
      });
    } else {
      // --- Identify shared junction pixels across chains ---
      // Collect all chain endpoint pixels. Pixels shared by 2+ chain
      // endpoints are junction points. After smoothing we snap spine
      // endpoints back to the exact junction meter coordinate so that
      // branches connect seamlessly.
      const endpointCount = new Map(); // "x,y" → count of chain endpoints here
      for (const chain of significantChains) {
        for (const pt of [chain[0], chain[chain.length - 1]]) {
          const key = `${pt.x},${pt.y}`;
          endpointCount.set(key, (endpointCount.get(key) || 0) + 1);
        }
      }

      // Junctions: pixels where 2+ chain endpoints meet
      const sharedJunctions = new Map();
      for (const [key, count] of endpointCount) {
        if (count >= 2) {
          const [px, py] = key.split(',').map(Number);
          sharedJunctions.set(key, {
            mx: parseFloat(((px / (w - 1) - 0.5) * tw).toFixed(2)),
            mz: parseFloat(((py / (h - 1) - 0.5) * tl).toFixed(2)),
            px, py,
          });
        }
      }

      // --- Merge chains at 2-way junctions ---
      // If a junction has exactly 2 chain endpoints, those chains form a
      // continuous path — merge them into one chain (avoids visible seam).
      let mergedSomething = true;
      while (mergedSomething) {
        mergedSomething = false;
        // Recount endpoints after each merge
        const epCount2 = new Map();
        for (let ci = 0; ci < significantChains.length; ci++) {
          const chain = significantChains[ci];
          for (const pt of [chain[0], chain[chain.length - 1]]) {
            const key = `${pt.x},${pt.y}`;
            if (!epCount2.has(key)) epCount2.set(key, []);
            epCount2.get(key).push(ci);
          }
        }

        for (const [jKey, chainIndices] of epCount2) {
          if (chainIndices.length !== 2) continue;
          const [ai, bi] = chainIndices;
          if (ai === bi) continue; // same chain loops back
          const chainA = significantChains[ai];
          const chainB = significantChains[bi];
          if (!chainA || !chainB) continue;

          // Determine which ends connect at this junction
          const aEnd = `${chainA[chainA.length - 1].x},${chainA[chainA.length - 1].y}` === jKey;
          const aStart = `${chainA[0].x},${chainA[0].y}` === jKey;
          const bEnd = `${chainB[chainB.length - 1].x},${chainB[chainB.length - 1].y}` === jKey;
          const bStart = `${chainB[0].x},${chainB[0].y}` === jKey;

          let merged = null;
          if (aEnd && bStart) {
            // A's end → B's start: concat A + B (skip duplicate junction point)
            merged = [...chainA, ...chainB.slice(1)];
          } else if (aEnd && bEnd) {
            // A's end → B's end: concat A + reversed B
            merged = [...chainA, ...chainB.slice(0, -1).reverse()];
          } else if (aStart && bStart) {
            // A's start → B's start: reverse A + B
            merged = [...chainA.slice(1).reverse(), ...chainB];
          } else if (aStart && bEnd) {
            // B's end → A's start: concat B + A
            merged = [...chainB, ...chainA.slice(1)];
          }

          if (merged) {
            significantChains[ai] = merged;
            significantChains.splice(bi, 1);
            // Remove this junction since it's no longer a branch point
            sharedJunctions.delete(jKey);
            mergedSomething = true;
            break; // restart — indices changed
          }
        }
      }

      // Track which chains have a shared junction in their interior
      // (merged main chains that pass through a branch junction).
      // For each such chain, record the interior pixel index closest
      // to the junction so we can snap the smoothed spine vertex.
      const junctionRadius = dsFactor + 1; // full-res pixels tolerance (1 DS pixel + margin)
      const interiorJunctions = new Map(); // chainIndex → [{junctionKey, interiorIdx}]
      for (let ci = 0; ci < significantChains.length; ci++) {
        const chain = significantChains[ci];
        const firstKey = `${chain[0].x},${chain[0].y}`;
        const lastKey = `${chain[chain.length - 1].x},${chain[chain.length - 1].y}`;

        for (const [jKey, jpt] of sharedJunctions) {
          // Skip if this junction is already at this chain's endpoint
          if (jKey === firstKey || jKey === lastKey) continue;

          // Check interior pixels for proximity to this junction
          let bestDist = Infinity, bestIdx = -1;
          for (let pi = 1; pi < chain.length - 1; pi++) {
            const ddx = chain[pi].x - jpt.px;
            const ddy = chain[pi].y - jpt.py;
            const d = ddx * ddx + ddy * ddy;
            if (d < bestDist) { bestDist = d; bestIdx = pi; }
          }
          if (bestIdx >= 0 && bestDist <= junctionRadius * junctionRadius) {
            if (!interiorJunctions.has(ci)) interiorJunctions.set(ci, []);
            interiorJunctions.get(ci).push({ jKey, interiorIdx: bestIdx });
          }
        }
      }

      // Convert each chain to a spine entry
      for (let ci = 0; ci < significantChains.length; ci++) {
        const chain = significantChains[ci];

        // Convert pixel coords to meter coords
        let spine = chain.map(({ x: cx, y: cy }) => ({
          x: parseFloat(((cx / (w - 1) - 0.5) * tw).toFixed(2)),
          z: parseFloat(((cy / (h - 1) - 0.5) * tl).toFixed(2)),
        }));

        // Find nearest shared junction for each endpoint (fuzzy match
        // within junctionRadius to handle DS→full rounding differences)
        const firstPt = chain[0], lastPt = chain[chain.length - 1];
        let snapFirst = sharedJunctions.get(`${firstPt.x},${firstPt.y}`);
        let snapLast = sharedJunctions.get(`${lastPt.x},${lastPt.y}`);

        if (!snapFirst || !snapLast) {
          for (const [, jpt] of sharedJunctions) {
            if (!snapFirst) {
              const ddx = firstPt.x - jpt.px, ddy = firstPt.y - jpt.py;
              if (ddx * ddx + ddy * ddy <= junctionRadius * junctionRadius) {
                snapFirst = jpt;
              }
            }
            if (!snapLast) {
              const ddx = lastPt.x - jpt.px, ddy = lastPt.y - jpt.py;
              if (ddx * ddx + ddy * ddy <= junctionRadius * junctionRadius) {
                snapLast = jpt;
              }
            }
          }
        }

        // Simplify + smooth the spine (open polyline)
        spine = simplifyPolygon(spine, rdpEpsilon);
        spine = smoothPolyline(spine, smoothPasses);

        if (spine.length < 2) continue;

        // Snap endpoints back to exact junction coordinates
        if (snapFirst) {
          spine[0] = { x: snapFirst.mx, z: snapFirst.mz };
        }
        if (snapLast) {
          spine[spine.length - 1] = { x: snapLast.mx, z: snapLast.mz };
        }

        // For junctions in this chain's INTERIOR (merged main chain case):
        // ensure the spine has a vertex at the junction point so branches
        // can connect to it. Find the closest spine vertex and snap it.
        const intJunctions = interiorJunctions.get(ci);
        if (intJunctions) {
          for (const { jKey } of intJunctions) {
            const jpt = sharedJunctions.get(jKey);
            if (!jpt) continue;

            // Find nearest vertex on the smoothed spine
            let bestDist = Infinity, bestIdx = -1;
            for (let si = 0; si < spine.length; si++) {
              const ddx = spine[si].x - jpt.mx;
              const ddz = spine[si].z - jpt.mz;
              const d = ddx * ddx + ddz * ddz;
              if (d < bestDist) { bestDist = d; bestIdx = si; }
            }
            // Replace the nearest vertex with the exact junction point
            if (bestIdx >= 0 && bestDist < 400) { // within ~20m
              spine[bestIdx] = { x: jpt.mx, z: jpt.mz };
            }
          }
        }

        results.push({
          id: results.length + 1,
          pixel_count: currentPixels.length,
          contour: contourMeters,
          spine,
          width_m: minWidthM,
          parent_region: regionId,
          junctions: null, // filled below after all chains processed
          center_local: {
            x: parseFloat(((normCX - 0.5) * tw).toFixed(2)),
            z: parseFloat(((normCY - 0.5) * tl).toFixed(2)),
          },
          size_m: {
            x: parseFloat((normW * tw).toFixed(2)),
            z: parseFloat((normH * tl).toFixed(2)),
          },
          center_normalized: { x: parseFloat(normCX.toFixed(4)), y: parseFloat(normCY.toFixed(4)) },
          size_normalized: { w: parseFloat(normW.toFixed(4)), h: parseFloat(normH.toFixed(4)) },
          dilated: estWidthPx < minWidthPx,
        });
      }

      // Collect junction points (shared by 2+ chain endpoints) and attach
      // to every branch in this region so Unity can create fill patches.
      const junctionList = [];
      for (const [, jpt] of sharedJunctions) {
        junctionList.push({ x: jpt.mx, z: jpt.mz });
      }
      if (junctionList.length > 0) {
        for (const r of results) {
          if (r.parent_region === regionId) r.junctions = junctionList;
        }
      }
    }
  }

  // --- Snap orphan endpoints to nearest point on other spines ---
  // After chain merging, some branch endpoints are near another spine's
  // interior but not connected. Extend the branch to touch the other spine.
  const snapRadius = minWidthM * 4; // search within 4× path width (10m)
  for (let ai = 0; ai < results.length; ai++) {
    const spineA = results[ai].spine;
    if (!spineA || spineA.length < 2) continue;

    for (const endIdx of [0, spineA.length - 1]) {
      const ep = spineA[endIdx];

      // Find nearest point on any OTHER spine
      let bestDist = Infinity, bestPoint = null;
      for (let bi = 0; bi < results.length; bi++) {
        if (bi === ai) continue;
        if (results[ai].parent_region !== results[bi].parent_region) continue;
        const spineB = results[bi].spine;
        if (!spineB || spineB.length < 2) continue;

        for (let si = 0; si < spineB.length - 1; si++) {
          // Nearest point on segment spineB[si]→spineB[si+1]
          const ax = spineB[si].x, az = spineB[si].z;
          const bx = spineB[si + 1].x, bz = spineB[si + 1].z;
          const dx = bx - ax, dz = bz - az;
          const lenSq = dx * dx + dz * dz;
          if (lenSq < 1e-10) continue;
          let t = ((ep.x - ax) * dx + (ep.z - az) * dz) / lenSq;
          t = Math.max(0, Math.min(1, t));
          const nx = ax + t * dx, nz = az + t * dz;
          const d = Math.sqrt((ep.x - nx) ** 2 + (ep.z - nz) ** 2);
          if (d < bestDist) {
            bestDist = d;
            bestPoint = { x: parseFloat(nx.toFixed(2)), z: parseFloat(nz.toFixed(2)) };
          }
        }
      }

      if (bestPoint && bestDist > 0.5 && bestDist < snapRadius) {
        // Extend spine to connect: add the snap point at the endpoint
        if (endIdx === 0) {
          spineA.unshift(bestPoint);
        } else {
          spineA.push(bestPoint);
        }
      }
    }
  }

  // --- Pull back snapped endpoints by halfWidth ---
  // At T-junctions, the branch strip extends ±halfWidth from its spine
  // center. If the endpoint sits on the target spine's centerline, the
  // strip overshoots by halfWidth past the far edge. Pull the endpoint
  // back along the approach direction by halfWidth so the strip EDGE
  // (not center) lands at the target centerline. The overlap with the
  // main strip covers the junction area.
  const halfWidth = minWidthM / 2;
  for (const cp of results) {
    if (!cp.spine || cp.spine.length < 3) continue;

    for (const endIdx of [0, cp.spine.length - 1]) {
      const ep = cp.spine[endIdx];

      // Check if this endpoint is near another spine's interior
      let isSnapped = false;
      for (const other of results) {
        if (other === cp) continue;
        if (cp.parent_region !== other.parent_region) continue;
        if (!other.spine || other.spine.length < 2) continue;

        for (let si = 1; si < other.spine.length - 1; si++) {
          const dx = ep.x - other.spine[si].x;
          const dz = ep.z - other.spine[si].z;
          const d = Math.sqrt(dx * dx + dz * dz);
          if (d < halfWidth * 2) {
            isSnapped = true;
            break;
          }
        }
        if (isSnapped) break;
      }

      if (!isSnapped) continue;

      // Pull back: find the point on the spine that is halfWidth
      // away from the endpoint, measured along the spine arc
      if (endIdx === 0) {
        // Pull back from start: remove leading points until we've
        // traveled halfWidth along the spine, then interpolate
        let accumulated = 0;
        let cutIdx = 0;
        for (let i = 0; i < cp.spine.length - 2; i++) {
          const dx = cp.spine[i + 1].x - cp.spine[i].x;
          const dz = cp.spine[i + 1].z - cp.spine[i].z;
          const segLen = Math.sqrt(dx * dx + dz * dz);
          if (accumulated + segLen >= halfWidth) {
            // Interpolate the new start point
            const remaining = halfWidth - accumulated;
            const t = remaining / segLen;
            cp.spine[i] = {
              x: parseFloat((cp.spine[i].x + t * dx).toFixed(2)),
              z: parseFloat((cp.spine[i].z + t * dz).toFixed(2)),
            };
            cutIdx = i;
            break;
          }
          accumulated += segLen;
          cutIdx = i + 1;
        }
        if (cutIdx > 0) {
          cp.spine.splice(0, cutIdx);
        }
      } else {
        // Pull back from end: remove trailing points
        let accumulated = 0;
        let cutIdx = cp.spine.length - 1;
        for (let i = cp.spine.length - 1; i > 0; i--) {
          const dx = cp.spine[i].x - cp.spine[i - 1].x;
          const dz = cp.spine[i].z - cp.spine[i - 1].z;
          const segLen = Math.sqrt(dx * dx + dz * dz);
          if (accumulated + segLen >= halfWidth) {
            const remaining = halfWidth - accumulated;
            const t = remaining / segLen;
            cp.spine[i] = {
              x: parseFloat((cp.spine[i].x - t * dx).toFixed(2)),
              z: parseFloat((cp.spine[i].z - t * dz).toFixed(2)),
            };
            cutIdx = i;
            break;
          }
          accumulated += segLen;
          cutIdx = i - 1;
        }
        if (cutIdx < cp.spine.length - 1) {
          cp.spine.splice(cutIdx + 1);
        }
      }
    }
  }

  // Sort by size (largest first), re-assign IDs
  results.sort((a, b) => b.pixel_count - a.pixel_count);
  results.forEach((r, i) => { r.id = i + 1; });

  return results;
}

/**
 * Nudge cart path spines so the strip (center ± halfWidth + margin) stays
 * outside all forbidden contour polygons (fairway, bunker, tee).
 *
 * For each spine vertex whose strip edge falls inside a forbidden polygon,
 * compute the nearest contour edge and push the spine point perpendicular
 * to that edge until the strip edge clears it.
 *
 * @param {Array} cartPaths - cart path regions with .spine arrays
 * @param {Array} forbiddenContours - array of {contour: [{x,z},...]} polygons
 * @param {number} halfWidth - strip half-width in meters
 * @param {number} margin - extra clearance beyond half-width (meters)
 */
function nudgeSpinesFromContours(cartPaths, forbiddenContours, halfWidth, margin = 0.5) {
  if (forbiddenContours.length === 0) return;

  // Point-in-polygon (ray casting)
  function pip(px, pz, contour) {
    let inside = false;
    for (let i = 0, j = contour.length - 1; i < contour.length; j = i++) {
      const xi = contour[i].x, zi = contour[i].z;
      const xj = contour[j].x, zj = contour[j].z;
      if (((zi > pz) !== (zj > pz)) && (px < (xj - xi) * (pz - zi) / (zj - zi) + xi))
        inside = !inside;
    }
    return inside;
  }

  // Nearest point on segment AB to point P, returns {x, z, dist}
  function nearestOnSegment(ax, az, bx, bz, px, pz) {
    const dx = bx - ax, dz = bz - az;
    const lenSq = dx * dx + dz * dz;
    if (lenSq < 1e-10) {
      const d = Math.sqrt((px - ax) ** 2 + (pz - az) ** 2);
      return { x: ax, z: az, dist: d };
    }
    let t = ((px - ax) * dx + (pz - az) * dz) / lenSq;
    t = Math.max(0, Math.min(1, t));
    const nx = ax + t * dx, nz = az + t * dz;
    const d = Math.sqrt((px - nx) ** 2 + (pz - nz) ** 2);
    return { x: nx, z: nz, dist: d };
  }

  // Find nearest point on contour boundary to a given point
  function nearestContourPoint(px, pz, contour) {
    let best = null;
    for (let i = 0; i < contour.length; i++) {
      const j = (i + 1) % contour.length;
      const np = nearestOnSegment(contour[i].x, contour[i].z, contour[j].x, contour[j].z, px, pz);
      if (!best || np.dist < best.dist) best = np;
    }
    return best;
  }

  const clearance = halfWidth + margin;
  let totalNudged = 0;

  // Iterative nudge-then-smooth: each pass may leave residual overlaps
  // because smoothing pulls points back. Repeat until clean or max iters.
  const maxPasses = 10;

  for (const cp of cartPaths) {
    if (!cp.spine || cp.spine.length < 2) continue;
    const spine = cp.spine;

    for (let pass = 0; pass < maxPasses; pass++) {
      let passNudged = 0;

      for (let i = 0; i < spine.length; i++) {
        const pt = spine[i];

        // Compute tangent and perpendicular at this spine vertex
        const prev = spine[Math.max(i - 1, 0)];
        const next = spine[Math.min(i + 1, spine.length - 1)];
        const tx = next.x - prev.x, tz = next.z - prev.z;
        const tLen = Math.sqrt(tx * tx + tz * tz) || 1;
        const perpX = tz / tLen, perpZ = -tx / tLen;

        let bestNewX = pt.x, bestNewZ = pt.z;
        let moved = false;

        for (const fc of forbiddenContours) {
          const contour = fc.contour;
          const centerInside = pip(bestNewX, bestNewZ, contour);

          const leftX = bestNewX - perpX * clearance;
          const leftZ = bestNewZ - perpZ * clearance;
          const rightX = bestNewX + perpX * clearance;
          const rightZ = bestNewZ + perpZ * clearance;
          const leftInside = pip(leftX, leftZ, contour);
          const rightInside = pip(rightX, rightZ, contour);

          if (!centerInside && !leftInside && !rightInside) continue;

          const nearest = nearestContourPoint(bestNewX, bestNewZ, contour);
          if (!nearest) continue;

          if (centerInside) {
            // Center is inside polygon. Move it to just outside the boundary
            // at clearance distance from the edge.
            // Direction: from center TOWARD nearest boundary, then PAST it.
            let toEdgeX = nearest.x - bestNewX;
            let toEdgeZ = nearest.z - bestNewZ;
            const toEdgeLen = Math.sqrt(toEdgeX * toEdgeX + toEdgeZ * toEdgeZ);
            if (toEdgeLen < 1e-6) {
              toEdgeX = leftInside ? perpX : -perpX;
              toEdgeZ = leftInside ? perpZ : -perpZ;
            } else {
              toEdgeX /= toEdgeLen;
              toEdgeZ /= toEdgeLen;
            }
            // Place center at: boundary point + clearance in same direction
            bestNewX = nearest.x + toEdgeX * (clearance + 0.3);
            bestNewZ = nearest.z + toEdgeZ * (clearance + 0.3);
            moved = true;
          } else {
            // Only edge inside — push center away from boundary
            const gap = clearance - nearest.dist + 0.3;
            if (gap <= 0) continue;
            let awayX = bestNewX - nearest.x;
            let awayZ = bestNewZ - nearest.z;
            const awayLen = Math.sqrt(awayX * awayX + awayZ * awayZ);
            if (awayLen < 1e-6) {
              awayX = leftInside ? perpX : -perpX;
              awayZ = leftInside ? perpZ : -perpZ;
            } else {
              awayX /= awayLen;
              awayZ /= awayLen;
            }
            bestNewX += awayX * gap;
            bestNewZ += awayZ * gap;
            moved = true;
          }
        }

        if (moved) {
          spine[i] = {
            x: parseFloat(bestNewX.toFixed(2)),
            z: parseFloat(bestNewZ.toFixed(2)),
          };
          passNudged++;
          totalNudged++;
        }
      }

      if (passNudged === 0) break; // converged

      // Light smoothing pass on nudged spine to avoid jerky kinks.
      // Progressively reduce smoothing strength so later passes converge.
      // Skip last 2 passes entirely so final nudges aren't pulled back.
      if (pass < maxPasses - 2 && spine.length >= 3) {
        // Reduce neighbor weight as passes increase (0.2 → 0.1 → 0.05...)
        const neighborW = Math.max(0.05, 0.2 / (1 + pass * 0.5));
        const selfW = 1 - 2 * neighborW;
        const smoothed = spine.map((p, i) => {
          if (i === 0 || i === spine.length - 1) return p;
          const prev = spine[i - 1], next = spine[i + 1];
          return {
            x: parseFloat((p.x * selfW + prev.x * neighborW + next.x * neighborW).toFixed(2)),
            z: parseFloat((p.z * selfW + prev.z * neighborW + next.z * neighborW).toFixed(2)),
          };
        });
        for (let i = 0; i < spine.length; i++) spine[i] = smoothed[i];
      }
    }
  }

  if (totalNudged > 0) {
    console.log(`    Spine nudge: ${totalNudged} point(s) pushed away from fairway/bunker/tee contours`);
  }
}

/**
 * Zhang-Suen morphological thinning — reduces a binary mask to a 1-pixel-wide skeleton.
 * Preserves topology including branches and junctions.
 * @param {Uint8Array} mask - binary mask (1 = foreground, 0 = background)
 * @param {number} w - width
 * @param {number} h - height
 * @returns {Uint8Array} modified mask with skeleton pixels = 1
 */
function thinSkeleton(mask, w, h) {
  const out = new Uint8Array(mask);

  // Neighbor offsets: P2..P9 in Zhang-Suen order (N, NE, E, SE, S, SW, W, NW)
  const dy = [-1, -1,  0,  1, 1, 1, 0, -1];
  const dx = [ 0,  1,  1,  1, 0, -1, -1, -1];

  function getP(x, y, i) {
    const nx = x + dx[i], ny = y + dy[i];
    if (nx < 0 || nx >= w || ny < 0 || ny >= h) return 0;
    return out[ny * w + nx];
  }

  let changed = true;
  while (changed) {
    changed = false;

    // Sub-iteration 1
    const toRemove1 = [];
    for (let y = 1; y < h - 1; y++) {
      for (let x = 1; x < w - 1; x++) {
        if (!out[y * w + x]) continue;

        const p = [];
        for (let i = 0; i < 8; i++) p.push(getP(x, y, i));

        // B(P1) = number of non-zero neighbors
        const B = p.reduce((s, v) => s + v, 0);
        if (B < 2 || B > 6) continue;

        // A(P1) = number of 0→1 transitions in the ordered sequence P2..P9..P2
        let A = 0;
        for (let i = 0; i < 8; i++) {
          if (p[i] === 0 && p[(i + 1) % 8] === 1) A++;
        }
        if (A !== 1) continue;

        // Conditions for sub-iteration 1: P2*P4*P6=0 and P4*P6*P8=0
        if (p[0] * p[2] * p[4] !== 0) continue;
        if (p[2] * p[4] * p[6] !== 0) continue;

        toRemove1.push(y * w + x);
      }
    }
    for (const idx of toRemove1) { out[idx] = 0; changed = true; }

    // Sub-iteration 2
    const toRemove2 = [];
    for (let y = 1; y < h - 1; y++) {
      for (let x = 1; x < w - 1; x++) {
        if (!out[y * w + x]) continue;

        const p = [];
        for (let i = 0; i < 8; i++) p.push(getP(x, y, i));

        const B = p.reduce((s, v) => s + v, 0);
        if (B < 2 || B > 6) continue;

        let A = 0;
        for (let i = 0; i < 8; i++) {
          if (p[i] === 0 && p[(i + 1) % 8] === 1) A++;
        }
        if (A !== 1) continue;

        // Conditions for sub-iteration 2: P2*P4*P8=0 and P2*P6*P8=0
        if (p[0] * p[2] * p[6] !== 0) continue;
        if (p[0] * p[4] * p[6] !== 0) continue;

        toRemove2.push(y * w + x);
      }
    }
    for (const idx of toRemove2) { out[idx] = 0; changed = true; }
  }

  return out;
}

/**
 * Remove short spur branches from a skeleton.
 * A spur is a branch that starts at an endpoint and ends at a junction,
 * shorter than maxLen pixels. Iteratively removes spurs until none remain.
 * @param {Uint8Array} skeleton - thinned binary mask (modified in place)
 * @param {number} w - width
 * @param {number} h - height
 * @param {number} maxLen - max spur length in pixels to prune
 */
function pruneSpurs(skeleton, w, h, maxLen) {
  const dx = [1, 1, 0, -1, -1, -1, 0, 1];
  const dy = [0, 1, 1, 1, 0, -1, -1, -1];

  function neighborCount(x, y) {
    let c = 0;
    for (let i = 0; i < 8; i++) {
      const nx = x + dx[i], ny = y + dy[i];
      if (nx >= 0 && nx < w && ny >= 0 && ny < h && skeleton[ny * w + nx]) c++;
    }
    return c;
  }

  let changed = true;
  while (changed) {
    changed = false;

    // Find current endpoints
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (!skeleton[y * w + x]) continue;
        if (neighborCount(x, y) !== 1) continue;

        // Walk from endpoint, collecting spur pixels
        const spur = [{ x, y }];
        let cx = x, cy = y;
        for (let step = 0; step < maxLen; step++) {
          let nextX = -1, nextY = -1;
          for (let i = 0; i < 8; i++) {
            const nx = cx + dx[i], ny = cy + dy[i];
            if (nx >= 0 && nx < w && ny >= 0 && ny < h && skeleton[ny * w + nx]) {
              // Don't go back to previous pixel
              if (spur.length >= 2 && nx === spur[spur.length - 2].x && ny === spur[spur.length - 2].y) continue;
              nextX = nx;
              nextY = ny;
              break;
            }
          }
          if (nextX === -1) break; // dead end (isolated pixel)
          spur.push({ x: nextX, y: nextY });
          cx = nextX;
          cy = nextY;

          // Reached a junction? This spur can be pruned.
          if (neighborCount(cx, cy) >= 3) {
            // Remove spur pixels (but NOT the junction pixel)
            for (let s = 0; s < spur.length - 1; s++) {
              skeleton[spur[s].y * w + spur[s].x] = 0;
            }
            changed = true;
            break;
          }
        }
      }
    }
  }
}

/**
 * Collapse adjacent junction pixels (staircase artifacts from Zhang-Suen)
 * into single representative pixels. Two junction pixels that are
 * 8-connected neighbors are part of the same logical junction.
 * Replaces each cluster with its centroid pixel.
 * @param {Uint8Array} skeleton - thinned binary mask (modified in place)
 * @param {number} w - width
 * @param {number} h - height
 */
function collapseJunctionClusters(skeleton, w, h) {
  const dx = [1, 1, 0, -1, -1, -1, 0, 1];
  const dy = [0, 1, 1, 1, 0, -1, -1, -1];

  function neighborCount(x, y) {
    let c = 0;
    for (let i = 0; i < 8; i++) {
      const nx = x + dx[i], ny = y + dy[i];
      if (nx >= 0 && nx < w && ny >= 0 && ny < h && skeleton[ny * w + nx]) c++;
    }
    return c;
  }

  // Find all junction pixels (3+ skeleton neighbors)
  const junctionPixels = [];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (skeleton[y * w + x] && neighborCount(x, y) >= 3) {
        junctionPixels.push({ x, y });
      }
    }
  }

  if (junctionPixels.length === 0) return;

  // Flood-fill junction pixels into connected clusters (8-connected)
  const jpSet = new Set(junctionPixels.map(p => p.y * w + p.x));
  const visitedJP = new Set();
  const clusters = [];

  for (const jp of junctionPixels) {
    const key = jp.y * w + jp.x;
    if (visitedJP.has(key)) continue;

    const cluster = [];
    const stack = [jp];
    while (stack.length > 0) {
      const p = stack.pop();
      const k = p.y * w + p.x;
      if (visitedJP.has(k)) continue;
      visitedJP.add(k);
      cluster.push(p);

      for (let i = 0; i < 8; i++) {
        const nx = p.x + dx[i], ny = p.y + dy[i];
        if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
          const nk = ny * w + nx;
          if (jpSet.has(nk) && !visitedJP.has(nk)) {
            stack.push({ x: nx, y: ny });
          }
        }
      }
    }

    if (cluster.length > 1) {
      clusters.push(cluster);
    }
  }

  // For each cluster, find non-junction skeleton neighbors (the "arms" leaving this junction)
  // Keep only the centroid pixel and re-connect the arms to it
  for (const cluster of clusters) {
    // Find centroid
    let cx = 0, cy = 0;
    for (const p of cluster) { cx += p.x; cy += p.y; }
    cx = Math.round(cx / cluster.length);
    cy = Math.round(cy / cluster.length);

    // Find all non-junction skeleton pixels adjacent to the cluster
    const clusterSet = new Set(cluster.map(p => p.y * w + p.x));
    const arms = new Set();
    for (const p of cluster) {
      for (let i = 0; i < 8; i++) {
        const nx = p.x + dx[i], ny = p.y + dy[i];
        if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
          const nk = ny * w + nx;
          if (skeleton[nk] && !clusterSet.has(nk)) {
            arms.add(nk);
          }
        }
      }
    }

    // Clear all cluster pixels
    for (const p of cluster) {
      skeleton[p.y * w + p.x] = 0;
    }

    // Place centroid
    skeleton[cy * w + cx] = 1;

    // Draw 1px lines from centroid to each arm pixel (Bresenham)
    for (const armKey of arms) {
      const ax = armKey % w, ay = Math.floor(armKey / w);
      // Simple line drawing
      let lx = cx, ly = cy;
      const steps = Math.max(Math.abs(ax - cx), Math.abs(ay - cy));
      for (let s = 1; s < steps; s++) {
        const t = s / steps;
        const px = Math.round(cx + t * (ax - cx));
        const py = Math.round(cy + t * (ay - cy));
        skeleton[py * w + px] = 1;
      }
    }
  }
}

/**
 * Trace skeleton pixels into separate chains.
 * Finds junctions (3+ neighbors) and endpoints (1 neighbor), then walks
 * each segment between them. After tracing, merges chains through
 * pass-through junctions (junctions with exactly 2 chain connections).
 * @param {Uint8Array} skeleton - thinned binary mask
 * @param {number} w - width
 * @param {number} h - height
 * @returns {{x:number, y:number}[][]} array of chains, each chain = [{x,y}, ...]
 */
function traceSkeletonChains(skeleton, w, h) {
  // Collapse junction clusters into single pixels, then re-prune spurs
  // that the collapse may have created (new endpoints from broken connections).
  // Iterate until stable.
  for (let pass = 0; pass < 3; pass++) {
    collapseJunctionClusters(skeleton, w, h);
    pruneSpurs(skeleton, w, h, 5);
  }

  // 8-connected neighbor offsets
  const dx = [1, 1, 0, -1, -1, -1, 0, 1];
  const dy = [0, 1, 1, 1, 0, -1, -1, -1];

  function neighbors(x, y) {
    const nb = [];
    for (let i = 0; i < 8; i++) {
      const nx = x + dx[i], ny = y + dy[i];
      if (nx >= 0 && nx < w && ny >= 0 && ny < h && skeleton[ny * w + nx]) {
        nb.push({ x: nx, y: ny });
      }
    }
    return nb;
  }

  // Classify pixels
  const endpoints = [];
  const junctions = [];

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (!skeleton[y * w + x]) continue;
      const nb = neighbors(x, y);
      if (nb.length === 1) endpoints.push({ x, y });
      else if (nb.length >= 3) junctions.push({ x, y });
    }
  }

  const visited = new Uint8Array(w * h);
  const chains = [];

  const junctionSet = new Set();
  for (const j of junctions) junctionSet.add(j.y * w + j.x);

  function walkChain(startX, startY) {
    const chain = [{ x: startX, y: startY }];
    visited[startY * w + startX] = 1;

    let cx = startX, cy = startY;
    while (true) {
      const nb = neighbors(cx, cy);
      let next = null;
      for (const n of nb) {
        if (!visited[n.y * w + n.x]) { next = n; break; }
      }
      if (!next) break;

      chain.push(next);
      visited[next.y * w + next.x] = 1;
      cx = next.x;
      cy = next.y;

      // Stop at junctions (include the junction point but allow it to be revisited)
      if (junctionSet.has(cy * w + cx)) {
        visited[cy * w + cx] = 0;
        break;
      }
    }
    return chain;
  }

  // Walk from endpoints first
  for (const ep of endpoints) {
    if (visited[ep.y * w + ep.x]) continue;
    const chain = walkChain(ep.x, ep.y);
    if (chain.length >= 2) chains.push(chain);
  }

  // Walk from junctions for remaining segments
  for (const jp of junctions) {
    const nb = neighbors(jp.x, jp.y);
    for (const n of nb) {
      if (visited[n.y * w + n.x]) continue;
      visited[jp.y * w + jp.x] = 1;
      const chain = [{ x: jp.x, y: jp.y }];
      let cx = n.x, cy = n.y;
      chain.push({ x: cx, y: cy });
      visited[cy * w + cx] = 1;

      while (true) {
        const nb2 = neighbors(cx, cy);
        let next = null;
        for (const nn of nb2) {
          if (!visited[nn.y * w + nn.x]) { next = nn; break; }
        }
        if (!next) break;
        chain.push(next);
        visited[next.y * w + next.x] = 1;
        cx = next.x;
        cy = next.y;
        if (junctionSet.has(cy * w + cx)) {
          visited[cy * w + cx] = 0;
          break;
        }
      }

      if (chain.length >= 2) chains.push(chain);
      visited[jp.y * w + jp.x] = 0;
    }
  }

  // Pick up isolated loops
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (skeleton[y * w + x] && !visited[y * w + x]) {
        const chain = walkChain(x, y);
        if (chain.length >= 2) chains.push(chain);
      }
    }
  }

  // --- Merge chains through pass-through junctions ---
  // A junction connecting exactly 2 chain ends is a pass-through, not a branch.
  if (chains.length > 1) {
    const junctionChains = new Map();

    for (let ci = 0; ci < chains.length; ci++) {
      const chain = chains[ci];
      const startKey = chain[0].y * w + chain[0].x;
      const endKey = chain[chain.length - 1].y * w + chain[chain.length - 1].x;

      if (junctionSet.has(startKey)) {
        if (!junctionChains.has(startKey)) junctionChains.set(startKey, []);
        junctionChains.get(startKey).push({ chainIdx: ci, end: 'start' });
      }
      if (junctionSet.has(endKey)) {
        if (!junctionChains.has(endKey)) junctionChains.set(endKey, []);
        junctionChains.get(endKey).push({ chainIdx: ci, end: 'end' });
      }
    }

    let mergedAny = true;
    while (mergedAny) {
      mergedAny = false;

      for (const [jKey, connections] of junctionChains) {
        if (connections.length !== 2) continue;

        const [a, b] = connections;
        if (a.chainIdx === b.chainIdx) continue;
        if (!chains[a.chainIdx] || !chains[b.chainIdx]) continue;

        let chainA = chains[a.chainIdx].slice();
        let chainB = chains[b.chainIdx].slice();

        if (a.end === 'start') chainA.reverse();
        if (b.end === 'end') chainB.reverse();

        const merged = chainA.concat(chainB.slice(1));
        chains[a.chainIdx] = merged;
        chains[b.chainIdx] = null;

        junctionChains.delete(jKey);

        for (const [otherKey, otherConns] of junctionChains) {
          for (const conn of otherConns) {
            if (conn.chainIdx === b.chainIdx) {
              conn.chainIdx = a.chainIdx;
              const mc = chains[a.chainIdx];
              const mcStartKey = mc[0].y * w + mc[0].x;
              const mcEndKey = mc[mc.length - 1].y * w + mc[mc.length - 1].x;
              if (otherKey === mcStartKey) conn.end = 'start';
              else if (otherKey === mcEndKey) conn.end = 'end';
            }
          }
        }

        mergedAny = true;
        break;
      }
    }

    const mergedChains = chains.filter(c => c !== null);
    chains.length = 0;
    chains.push(...mergedChains);
  }

  return chains;
}

/**
 * Resample a polyline to a target number of equally-spaced points via arc-length interpolation.
 * @param {{x:number, z:number}[]} chain
 * @param {number} targetCount
 * @returns {{x:number, z:number}[]}
 */
function resampleChain(chain, targetCount) {
  if (chain.length < 2 || targetCount < 2) return chain.slice();

  const arcLengths = [0];
  for (let i = 1; i < chain.length; i++) {
    const dx = chain[i].x - chain[i - 1].x;
    const dz = chain[i].z - chain[i - 1].z;
    arcLengths.push(arcLengths[i - 1] + Math.sqrt(dx * dx + dz * dz));
  }
  const totalLength = arcLengths[arcLengths.length - 1];
  if (totalLength < 0.001) return chain.slice();

  const result = [];
  for (let i = 0; i < targetCount; i++) {
    const targetDist = (i / (targetCount - 1)) * totalLength;

    let seg = 0;
    while (seg < arcLengths.length - 2 && arcLengths[seg + 1] < targetDist) {
      seg++;
    }

    const segLen = arcLengths[seg + 1] - arcLengths[seg];
    const t = segLen > 0 ? (targetDist - arcLengths[seg]) / segLen : 0;

    result.push({
      x: parseFloat((chain[seg].x + t * (chain[seg + 1].x - chain[seg].x)).toFixed(2)),
      z: parseFloat((chain[seg].z + t * (chain[seg + 1].z - chain[seg].z)).toFixed(2)),
    });
  }

  return result;
}

/**
 * Extract the centerline spine of a narrow elongated polygon (e.g., cart path).
 * Finds the two farthest vertices (path endpoints), splits the contour into
 * left/right chains, resamples both to equal count, and averages corresponding
 * points to produce the medial axis.
 * @param {{x:number, z:number}[]} contour - closed polygon in local meters
 * @returns {{x:number, z:number}[]} spine centerline
 */
function extractPathSpine(contour) {
  const n = contour.length;
  if (n < 4) return contour.slice();

  // Find the pair of vertices with maximum distance (path endpoints)
  let maxDist = 0, iA = 0, iB = 0;
  for (let i = 0; i < n; i++) {
    for (let j = i + 1; j < n; j++) {
      const dx = contour[i].x - contour[j].x;
      const dz = contour[i].z - contour[j].z;
      const d = dx * dx + dz * dz;
      if (d > maxDist) {
        maxDist = d;
        iA = i;
        iB = j;
      }
    }
  }

  // Split into two chains: A→B (forward) and B→A (backward)
  const chainLeft = [];
  for (let i = iA; i !== iB; i = (i + 1) % n) {
    chainLeft.push(contour[i]);
  }
  chainLeft.push(contour[iB]);

  const chainRight = [];
  for (let i = iB; i !== iA; i = (i + 1) % n) {
    chainRight.push(contour[i]);
  }
  chainRight.push(contour[iA]);
  chainRight.reverse(); // so both chains go A→B

  // Resample both chains to the same number of points
  const numSpinePoints = Math.max(chainLeft.length, chainRight.length);
  const leftResampled = resampleChain(chainLeft, numSpinePoints);
  const rightResampled = resampleChain(chainRight, numSpinePoints);

  // Average corresponding points → spine
  const spine = [];
  for (let i = 0; i < numSpinePoints; i++) {
    spine.push({
      x: parseFloat(((leftResampled[i].x + rightResampled[i].x) / 2).toFixed(2)),
      z: parseFloat(((leftResampled[i].z + rightResampled[i].z) / 2).toFixed(2)),
    });
  }

  return spine;
}

/**
 * Extract water regions as rasterized masks (no contour simplification).
 * Each region gets a bbox-cropped binary mask for pixel-perfect Unity import.
 */
function extractWaterMasks(zonesData, terrainMeta, minPixels = 50) {
  const grid = Buffer.from(zonesData.terrain_grid || zonesData.grid, 'base64');
  const w = zonesData.source_dimensions.width;
  const h = zonesData.source_dimensions.height;
  const visited = new Uint8Array(w * h);

  const tw = terrainMeta.terrain_width_m;
  const tl = terrainMeta.terrain_length_m;
  const targetZone = 7; // water

  function floodFill(startX, startY) {
    const pixels = [];
    const stack = [[startX, startY]];
    while (stack.length > 0) {
      const [x, y] = stack.pop();
      if (x < 0 || x >= w || y < 0 || y >= h) continue;
      const idx = y * w + x;
      if (visited[idx] || grid[idx] !== targetZone) continue;
      visited[idx] = 1;
      pixels.push([x, y]);
      stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
    return pixels;
  }

  const regions = [];

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (grid[y * w + x] === targetZone && !visited[y * w + x]) {
        const pixels = floodFill(x, y);
        if (pixels.length < minPixels) continue;

        // Bounding box in pixel coords
        const xs = pixels.map(p => p[0]);
        const ys = pixels.map(p => p[1]);
        const pxMinX = Math.min(...xs);
        const pxMaxX = Math.max(...xs);
        const pxMinY = Math.min(...ys);
        const pxMaxY = Math.max(...ys);

        const maskW = pxMaxX - pxMinX + 1;
        const maskH = pxMaxY - pxMinY + 1;

        // Build binary mask cropped to bbox
        const mask = new Uint8Array(maskW * maskH); // 0 = not water
        for (const [px, py] of pixels) {
          const mx = px - pxMinX;
          const my = py - pxMinY;
          mask[my * maskW + mx] = 1;
        }

        // Convert mask to base64
        const maskBase64 = Buffer.from(mask).toString('base64');

        // Bounding box in local meter coordinates
        // Same coord system as anchors: (normCoord - 0.5) * terrainSize
        const bboxMinX = parseFloat(((pxMinX / (w - 1) - 0.5) * tw).toFixed(2));
        const bboxMaxX = parseFloat(((pxMaxX / (w - 1) - 0.5) * tw).toFixed(2));
        const bboxMinZ = parseFloat(((pxMinY / (h - 1) - 0.5) * tl).toFixed(2));
        const bboxMaxZ = parseFloat(((pxMaxY / (h - 1) - 0.5) * tl).toFixed(2));

        regions.push({
          id: regions.length + 1,
          pixel_count: pixels.length,
          bbox: {
            min_x: bboxMinX,
            max_x: bboxMaxX,
            min_z: bboxMinZ,
            max_z: bboxMaxZ,
          },
          mask: maskBase64,
          mask_width: maskW,
          mask_height: maskH,
        });
      }
    }
  }

  // Sort by size (largest first), re-assign IDs
  regions.sort((a, b) => b.pixel_count - a.pixel_count);
  regions.forEach((r, i) => { r.id = i + 1; });

  return regions;
}

function exportHole(courseId, holeNumber, courseJson) {
  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
  const exportDir = path.join(ROOT, 'output', courseId, 'export', `hole-${nn}`);

  // Check required files
  const required = [
    'terrain-meta.json', 'heightmap.raw', 'illustration.png',
    'tees.json', 'zones.json', 'extract-meta.json',
  ];
  for (const f of required) {
    if (!fs.existsSync(path.join(holeDir, f))) {
      console.error(`  Missing: ${f}`);
      return false;
    }
  }

  fs.mkdirSync(exportDir, { recursive: true });

  // Load source data
  const terrainMeta = JSON.parse(fs.readFileSync(path.join(holeDir, 'terrain-meta.json'), 'utf-8'));
  const teesData = JSON.parse(fs.readFileSync(path.join(holeDir, 'tees.json'), 'utf-8'));
  const extractMeta = JSON.parse(fs.readFileSync(path.join(holeDir, 'extract-meta.json'), 'utf-8'));
  const holeData = courseJson.holes.find(h => h.number === holeNumber);

  // --- Build hole-manifest.json ---
  const manifest = {
    schema_version: '1.0.0',
    pipeline: 'uhole-lite',
    course_id: courseId,
    hole_number: holeNumber,
    par: holeData.par,
    stroke_index: holeData.hdcp,
    championship_yards: holeData.tees.back.yards,
    bounds: null,
    origin: null,
    terrain: {
      heightmap_file: 'heightmap.raw',
      format: terrainMeta.format,
      resolution: terrainMeta.resolution,
      min_elevation_m: terrainMeta.min_elevation_m,
      max_elevation_m: terrainMeta.max_elevation_m,
      terrain_width_m: terrainMeta.terrain_width_m,
      terrain_length_m: terrainMeta.terrain_length_m,
    },
    texture: {
      file: 'texture.png',
      width: extractMeta.final_dimensions.width,
      height: extractMeta.final_dimensions.height,
    },
    aerial: null,
    anchors_file: 'anchors.json',
    zones_file: 'zones.json',
    bunkers_file: 'bunkers.json',
    greens_file: 'greens.json',
    water_file: 'water.json',
    fairway_contours_file: 'fairway-contours.json',
    cart_paths_file: 'cart-paths.json',
    zone_contours_file: 'zone-contours.json',
    tree_zones_file: 'tree-zones.json',
    review_status: 'auto-generated',
  };

  fs.writeFileSync(
    path.join(exportDir, 'hole-manifest.json'),
    JSON.stringify(manifest, null, 2),
    'utf-8'
  );

  // --- Build anchors.json (local meter coordinates) ---
  const tw = terrainMeta.terrain_width_m;
  const tl = terrainMeta.terrain_length_m;

  const anchors = teesData.tees
    .filter(t => t.normalized)
    .map(t => ({
      type: t.type,
      label: `${t.color.charAt(0).toUpperCase() + t.color.slice(1)} Tee (${t.yards}y)`,
      local: {
        x: parseFloat(((t.normalized.x - 0.5) * tw).toFixed(1)),
        z: parseFloat(((t.normalized.y - 0.5) * tl).toFixed(1)),
      },
    }));

  fs.writeFileSync(
    path.join(exportDir, 'anchors.json'),
    JSON.stringify(anchors, null, 2),
    'utf-8'
  );

  // --- Build bunkers.json ---
  const zonesData = JSON.parse(fs.readFileSync(path.join(holeDir, 'zones.json'), 'utf-8'));
  const bunkers = extractZoneContours(zonesData, terrainMeta, 6);  // zone 6 = bunker

  const bunkersOutput = {
    schema_version: '2.0.0',
    hole_number: holeNumber,
    bunker_count: bunkers.length,
    depth_m: 2.0,
    bunkers: bunkers,
  };

  fs.writeFileSync(
    path.join(exportDir, 'bunkers.json'),
    JSON.stringify(bunkersOutput, null, 2),
    'utf-8'
  );

  // Log bunker contour stats
  if (bunkers.length > 0) {
    const contourStats = bunkers.map(b =>
      `#${b.id}: ${b.contour.length}pts`
    ).join(', ');
    console.log(`  Bunker contours: ${contourStats}`);
  }

  // --- Build greens.json ---
  const greens = extractZoneContours(zonesData, terrainMeta, 2, 20);  // zone 2 = green, min 20px

  const greensOutput = {
    schema_version: '1.0.0',
    hole_number: holeNumber,
    green_count: greens.length,
    height_m: 0.15,
    greens: greens,
  };

  fs.writeFileSync(
    path.join(exportDir, 'greens.json'),
    JSON.stringify(greensOutput, null, 2),
    'utf-8'
  );

  // Log green contour stats
  if (greens.length > 0) {
    const contourStats = greens.map(g =>
      `#${g.id}: ${g.contour.length}pts`
    ).join(', ');
    console.log(`  Green contours: ${contourStats}`);
  }

  // --- Build fairway-contours.json ---
  const fairways = extractZoneContours(zonesData, terrainMeta, 1, 30, 1.0, 2);
  // zone 1 = fairway, min 30px, RDP epsilon 3.0, 3 Chaikin passes.
  // NOTE: narrow corridor sections may appear slightly thinner than the zone
  // map due to Chaikin shrinkage — acceptable tradeoff for smooth edges.

  const fairwayOutput = {
    schema_version: '1.0.0',
    hole_number: holeNumber,
    fairway_count: fairways.length,
    fairways: fairways,
  };

  fs.writeFileSync(
    path.join(exportDir, 'fairway-contours.json'),
    JSON.stringify(fairwayOutput, null, 2),
    'utf-8'
  );

  if (fairways.length > 0) {
    const contourStats = fairways.map(f =>
      `#${f.id}: ${f.contour.length}pts (${f.pixel_count}px)`
    ).join(', ');
    console.log(`  Fairway contours: ${contourStats}`);
  }

  // --- Build zone-contours.json (tee, semi-rough) ---
  const tees = extractZoneContours(zonesData, terrainMeta, 10, 15, 1.5, 3);
  // was: epsilon 2.0, 2 passes → now: epsilon 1.5, 3 passes
  const semiRough = extractZoneContours(zonesData, terrainMeta, 3, 30, 3.0, 3);
  const cartPaths = extractCartPathContours(zonesData, terrainMeta, 2.5, 15, 1.0, 2);
  // 2.5m min width, 15 min pixels, RDP epsilon 1.0 (preserve narrow shape), 2 Chaikin passes

  // Extract water contours early so they can be used for spine nudging
  const water = extractZoneContours(zonesData, terrainMeta, 7, 8, 2.0, 2);

  // Nudge cart path spines so the strip mesh doesn't overlap with
  // fairway, bunker, tee, or water contour polygons
  const forbiddenContours = [
    ...fairways.map(f => ({ contour: f.contour })),
    ...bunkers.map(b => ({ contour: b.contour })),
    ...tees.map(t => ({ contour: t.contour })),
    ...water.map(w => ({ contour: w.contour })),
  ];
  nudgeSpinesFromContours(cartPaths, forbiddenContours, 2.5 / 2);

  const zoneContoursOutput = {
    schema_version: '1.0.0',
    hole_number: holeNumber,
    zones: {
      tee: tees,
      semi_rough: semiRough,
      cart_path: cartPaths,
    },
  };

  fs.writeFileSync(
    path.join(exportDir, 'zone-contours.json'),
    JSON.stringify(zoneContoursOutput, null, 2),
    'utf-8'
  );

  if (tees.length > 0 || semiRough.length > 0 || cartPaths.length > 0) {
    console.log(`  Zone contours: ${tees.length} tee(s), ${semiRough.length} semi-rough, ${cartPaths.length} cart path(s)`);
  }

  // --- Build cart-paths.json ---
  const cartPathsOutput = {
    schema_version: '1.0.0',
    hole_number: holeNumber,
    cart_path_count: cartPaths.length,
    min_width_m: 2.5,
    cart_paths: cartPaths,
  };

  fs.writeFileSync(
    path.join(exportDir, 'cart-paths.json'),
    JSON.stringify(cartPathsOutput, null, 2),
    'utf-8'
  );

  if (cartPaths.length > 0) {
    const contourStats = cartPaths.map(c =>
      `#${c.id}: ${c.contour.length}pts, spine ${c.spine ? c.spine.length : 0}pts (${c.pixel_count}px${c.dilated ? ', dilated' : ''}${c.parent_region ? ', branch of R' + c.parent_region : ''})`
    ).join(', ');
    console.log(`  Cart path contours: ${contourStats}`);
  }

  // --- Build water.json ---
  // (water contours already extracted above for spine nudging)

  const waterOutput = {
    schema_version: '3.0.0',
    hole_number: holeNumber,
    water_count: water.length,
    water: water,
  };

  fs.writeFileSync(
    path.join(exportDir, 'water.json'),
    JSON.stringify(waterOutput, null, 2),
    'utf-8'
  );

  // Log water contour stats
  if (water.length > 0) {
    const contourStats = water.map(w =>
      `#${w.id}: ${w.contour.length}pts (${w.pixel_count}px)`
    ).join(', ');
    console.log(`  Water contours: ${contourStats}`);
  }

  // --- Build tree-zones.json ---
  // Trees exist in two places: zone 5 in merged grid, AND trees_mask overlay.
  // The merged grid gives OB priority (zone 9) over trees (zone 5), so trees
  // under OB are lost in 'grid'. Use trees_mask to recover them.
  const maskW = zonesData.source_dimensions.width;
  const maskH = zonesData.source_dimensions.height;
  const mergedBuf = Buffer.from(zonesData.grid, 'base64');
  const treesMaskBuf = zonesData.trees_mask
    ? Buffer.from(zonesData.trees_mask, 'base64') : null;

  // Build a synthetic grid where zone 5 = tree (from merged grid OR trees_mask)
  const treeGrid = Buffer.alloc(maskW * maskH);
  const treeMask = Buffer.alloc(maskW * maskH);
  for (let i = 0; i < mergedBuf.length; i++) {
    const isTree = mergedBuf[i] === 5 || (treesMaskBuf && treesMaskBuf[i]);
    if (isTree) {
      treeGrid[i] = 5;
      treeMask[i] = 1;
    }
  }

  const treeRegions = extractZoneContours(
    zonesData, terrainMeta, 5, 30, 3.0, 2, null, treeGrid
  );

  const treeZonesOutput = {
    schema_version: '1.0.0',
    hole_number: holeNumber,
    mask_width: maskW,
    mask_height: maskH,
    mask_base64: treeMask.toString('base64'),
    meters_per_pixel: {
      x: parseFloat((terrainMeta.terrain_width_m / maskW).toFixed(4)),
      z: parseFloat((terrainMeta.terrain_length_m / maskH).toFixed(4)),
    },
    tree_region_count: treeRegions.length,
    tree_regions: treeRegions.map(r => ({
      id: r.id,
      pixel_count: r.pixel_count,
      area_m2: parseFloat(
        (r.pixel_count *
         (terrainMeta.terrain_width_m / maskW) *
         (terrainMeta.terrain_length_m / maskH)
        ).toFixed(1)
      ),
      contour: r.contour,
      center_local: r.center_local,
      size_m: r.size_m,
    })),
  };

  fs.writeFileSync(
    path.join(exportDir, 'tree-zones.json'),
    JSON.stringify(treeZonesOutput, null, 2),
    'utf-8'
  );

  if (treeRegions.length > 0) {
    const totalArea = treeZonesOutput.tree_regions
      .reduce((sum, r) => sum + r.area_m2, 0);
    console.log(`  Tree zones: ${treeRegions.length} region(s), ` +
      `${totalArea.toFixed(0)} m² total`);
  } else {
    console.log(`  Tree zones: none painted`);
  }

  // --- Copy files ---
  fs.copyFileSync(path.join(holeDir, 'heightmap.raw'), path.join(exportDir, 'heightmap.raw'));
  fs.copyFileSync(path.join(holeDir, 'illustration.png'), path.join(exportDir, 'texture.png'));
  fs.copyFileSync(path.join(holeDir, 'zones.json'), path.join(exportDir, 'zones.json'));

  return {
    manifest,
    anchorCount: anchors.length,
    bunkerCount: bunkers.length,
    greenCount: greens.length,
    waterCount: water.length,
    treeRegionCount: treeRegions.length,
  };
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const courseId = process.argv[2];
  const holeArg = process.argv[3];

  if (!courseId || !holeArg) {
    console.error('Usage: node scripts/export-hole.mjs <course-id> <hole-number|--all>');
    process.exit(1);
  }

  const coursePath = path.join(ROOT, 'output', courseId, 'course.json');
  if (!fs.existsSync(coursePath)) {
    console.error('course.json not found — run scrape-course.mjs first');
    process.exit(1);
  }
  const courseJson = JSON.parse(fs.readFileSync(coursePath, 'utf-8'));

  const holes = holeArg === '--all'
    ? Array.from({ length: 18 }, (_, i) => i + 1)
    : [parseInt(holeArg, 10)];

  if (holes.some(h => isNaN(h) || h < 1 || h > 18)) {
    console.error('Hole number must be 1-18 or --all');
    process.exit(1);
  }

  console.log(`Exporting ${holes.length} hole(s)\n`);

  let successCount = 0;
  for (const h of holes) {
    const nn = String(h).padStart(2, '0');
    process.stdout.write(`Hole ${h}/18 ... `);
    const result = exportHole(courseId, h, courseJson);
    if (result) {
      const m = result.manifest;
      console.log(`OK  export/hole-${nn}/  par=${m.par}  ${m.championship_yards}yd  ` +
        `${m.terrain.terrain_width_m}×${m.terrain.terrain_length_m}m  ` +
        `${result.anchorCount} anchors  ${result.bunkerCount} bunkers  ` +
        `${result.greenCount} greens  ${result.waterCount} water  ` +
        `${result.treeRegionCount} tree regions`);
      successCount++;
    } else {
      console.log('FAILED');
    }
  }

  console.log(`\nDone: ${successCount}/${holes.length} exported.`);
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
