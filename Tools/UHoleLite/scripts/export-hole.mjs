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
function traceBorder(grid, w, h, pixels, zoneValue) {
  const pixelSet = new Set();
  for (const [px, py] of pixels) {
    pixelSet.add(py * w + px);
  }

  // A pixel is a border pixel if it has at least one 4-connected neighbor NOT in the set
  const border = [];
  for (const [px, py] of pixels) {
    const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
    const isBorder = neighbors.some(([nx, ny]) => {
      if (nx < 0 || nx >= w || ny < 0 || ny >= h) return true;
      return !pixelSet.has(ny * w + nx);
    });
    if (isBorder) border.push([px, py]);
  }

  if (border.length === 0) return [];

  // Order by walking the perimeter (8-connected)
  border.sort((a, b) => a[1] - b[1] || a[0] - b[0]);

  const borderSet = new Set(border.map(([x, y]) => y * w + x));
  const ordered = [border[0]];
  const visited = new Set();
  visited.add(border[0][1] * w + border[0][0]);

  const dirs8 = [[-1,-1],[-1,0],[-1,1],[0,-1],[0,1],[1,-1],[1,0],[1,1]];

  let current = border[0];
  for (let step = 0; step < border.length * 2; step++) {
    let found = false;
    for (const [dx, dy] of dirs8) {
      const nx = current[0] + dx;
      const ny = current[1] + dy;
      const key = ny * w + nx;
      if (borderSet.has(key) && !visited.has(key)) {
        visited.add(key);
        ordered.push([nx, ny]);
        current = [nx, ny];
        found = true;
        break;
      }
    }
    if (!found) break;
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

function extractZoneContours(zonesData, terrainMeta, targetZone, minPixels = 8, rdpEpsilon = 2.0, smoothPasses = 2) {
  const grid = Buffer.from(zonesData.grid, 'base64');
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
 * Extract water regions as rasterized masks (no contour simplification).
 * Each region gets a bbox-cropped binary mask for pixel-perfect Unity import.
 */
function extractWaterMasks(zonesData, terrainMeta, minPixels = 50) {
  const grid = Buffer.from(zonesData.grid, 'base64');
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
    zone_contours_file: 'zone-contours.json',
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
  const fairways = extractZoneContours(zonesData, terrainMeta, 1, 30, 3.0, 3);
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
  const cartPaths = extractZoneContours(zonesData, terrainMeta, 8, 15, 1.5, 3);
  // epsilon 1.5 = preserve the path shape, 3 Chaikin passes = smooth curves

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

  if (cartPaths.length > 0) {
    const contourStats = cartPaths.map(c =>
      `#${c.id}: ${c.contour.length}pts`
    ).join(', ');
    console.log(`  Cart path contours: ${contourStats}`);
  }

  // --- Build water.json ---
  const water = extractWaterMasks(zonesData, terrainMeta, 50);

  const waterOutput = {
    schema_version: '2.0.0',
    hole_number: holeNumber,
    water_count: water.length,
    water: water,
  };

  fs.writeFileSync(
    path.join(exportDir, 'water.json'),
    JSON.stringify(waterOutput, null, 2),
    'utf-8'
  );

  // Log water mask stats
  if (water.length > 0) {
    const maskStats = water.map(w =>
      `#${w.id}: ${w.mask_width}x${w.mask_height}px (${w.pixel_count}px)`
    ).join(', ');
    console.log(`  Water masks: ${maskStats}`);
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
        `${result.greenCount} greens  ${result.waterCount} water`);
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
