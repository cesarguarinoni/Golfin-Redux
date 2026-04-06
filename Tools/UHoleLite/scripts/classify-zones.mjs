#!/usr/bin/env node
/**
 * classify-zones.mjs — Step 3: Color-based zone classification
 *
 * Usage:
 *   node scripts/classify-zones.mjs lomond-country-club 1      # single hole
 *   node scripts/classify-zones.mjs lomond-country-club --all   # all 18
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';
import { rgbToHsl } from './lib/colors.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

// ---------------------------------------------------------------------------
// Zone definitions
// ---------------------------------------------------------------------------

const ZONES = {
  background: 0,
  fairway:    1,
  green:      2,
  semi_rough: 3,
  rough:      4,
  trees:      5,
  bunker:     6,
  water:      7,
  cart_path:  8,
  ob:         9,
  tee_box:   10,
};

const ZONE_NAMES = Object.fromEntries(
  Object.entries(ZONES).map(([k, v]) => [v, k])
);

// Visualization colors [R, G, B]
const ZONE_COLORS = {
  0:  [0,   0,   0  ],  // background — black
  1:  [0,   204, 0  ],  // fairway — bright green
  2:  [128, 255, 64 ],  // green — lime/yellow-green
  3:  [102, 136, 51 ],  // semi_rough — olive
  4:  [51,  102, 34 ],  // rough — dark green
  5:  [26,  51,  16 ],  // trees — very dark green
  6:  [221, 204, 136],  // bunker — sandy yellow
  7:  [51,  102, 204],  // water — blue
  8:  [153, 153, 153],  // cart_path — gray
  9:  [255, 51,  51 ],  // ob — red
  10: [255, 255, 255],  // tee_box — white
};

// ---------------------------------------------------------------------------
// Per-pixel classification
// ---------------------------------------------------------------------------

function classifyPixel(r, g, b) {
  const { h, s, l } = rgbToHsl(r, g, b);

  // 1. Near-black → background
  if (l < 0.08) return ZONES.background;

  // 2. Very bright, low saturation → background (illustration border/margin)
  //    The wide light margins around the hole are not playable area.
  if (l > 0.82 && s < 0.18) return ZONES.background;

  // 3. Gray, low saturation → rough (most gray in these illustrations is shadow)
  //    True cart paths are thin — the majority filter will preserve them if enough
  //    pixels are genuinely gray. Ultra-low saturation gray = cart path candidate.
  if (s < 0.05 && l > 0.30 && l < 0.60) return ZONES.cart_path;

  // 4. Blue → water
  if (h >= 180 && h <= 250 && s > 0.25) return ZONES.water;

  // 5. Tan/beige → bunker
  if (h >= 10 && h <= 55 && s > 0.15 && l >= 0.55 && l <= 0.85) return ZONES.bunker;

  // 6. Green hue range — the main classification area
  if (h >= 55 && h <= 185) {
    // Very dark → trees (forest/dark masses)
    if (l < 0.18) return ZONES.trees;
    if (l < 0.25 && s < 0.35) return ZONES.trees;

    // Putting green: very bright, high saturation green — extremely restrictive
    // to avoid capturing bright fairway highlights
    if (s > 0.60 && l > 0.68) return ZONES.green;

    // Fairway: bright, saturated green
    if (s > 0.28 && l >= 0.28 && l <= 0.65) return ZONES.fairway;

    // Semi-rough: medium saturation, moderate lightness
    if (s >= 0.12 && l >= 0.22 && l <= 0.42) return ZONES.semi_rough;

    // Darker green → rough
    return ZONES.rough;
  }

  // 7. Warm tones outside green hue (pinkish/brownish bare earth near bunkers)
  if (h >= 0 && h < 55 && s > 0.12 && l > 0.40 && l < 0.80) return ZONES.bunker;

  // 8. Light desaturated colors → background (margin areas)
  if (l > 0.75 && s < 0.15) return ZONES.background;

  // 9. Moderate lightness, low saturation → rough
  if (s < 0.10 && l >= 0.20 && l < 0.50) return ZONES.rough;

  // 10. Default
  if (l < 0.22) return ZONES.trees;
  return ZONES.rough;
}

// ---------------------------------------------------------------------------
// Majority filter (morphological smoothing)
// ---------------------------------------------------------------------------

function majorityFilter(grid, width, height, radius) {
  const result = new Uint8Array(grid.length);
  const counts = new Uint32Array(11); // zone indices 0-10

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      counts.fill(0);

      const x0 = Math.max(0, x - radius);
      const x1 = Math.min(width - 1, x + radius);
      const y0 = Math.max(0, y - radius);
      const y1 = Math.min(height - 1, y + radius);

      for (let ny = y0; ny <= y1; ny++) {
        for (let nx = x0; nx <= x1; nx++) {
          counts[grid[ny * width + nx]]++;
        }
      }

      // Find majority
      let bestZone = grid[y * width + x];
      let bestCount = 0;
      for (let z = 0; z < 11; z++) {
        if (counts[z] > bestCount) {
          bestCount = counts[z];
          bestZone = z;
        }
      }
      result[y * width + x] = bestZone;
    }
  }
  return result;
}

// ---------------------------------------------------------------------------
// Small region absorption
// ---------------------------------------------------------------------------

function absorbSmallRegions(grid, width, height, minSize) {
  const visited = new Uint8Array(width * height);
  const result = new Uint8Array(grid);

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = y * width + x;
      if (visited[idx]) continue;

      const zone = grid[idx];
      // BFS to find connected component
      const pixels = [];
      const queue = [{ x, y }];
      visited[idx] = 1;
      const neighborZones = new Map(); // neighboring zone → count

      while (queue.length > 0) {
        const p = queue.shift();
        pixels.push(p);

        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
          const nx = p.x + dx, ny = p.y + dy;
          if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
          const nIdx = ny * width + nx;
          if (grid[nIdx] !== zone) {
            neighborZones.set(grid[nIdx], (neighborZones.get(grid[nIdx]) || 0) + 1);
            continue;
          }
          if (visited[nIdx]) continue;
          visited[nIdx] = 1;
          queue.push({ x: nx, y: ny });
        }
      }

      if (pixels.length < minSize && neighborZones.size > 0) {
        // Absorb into largest neighboring zone
        let bestNeighbor = zone;
        let bestCount = 0;
        for (const [z, c] of neighborZones) {
          if (c > bestCount) { bestNeighbor = z; bestCount = c; }
        }
        for (const p of pixels) {
          result[p.y * width + p.x] = bestNeighbor;
        }
      }
    }
  }
  return result;
}

// ---------------------------------------------------------------------------
// Mark tee boxes
// ---------------------------------------------------------------------------

function markTeeBoxes(grid, width, height, tees, radius) {
  for (const tee of tees) {
    if (!tee.pixel) continue;
    const cx = tee.pixel.x, cy = tee.pixel.y;
    for (let dy = -radius; dy <= radius; dy++) {
      for (let dx = -radius; dx <= radius; dx++) {
        if (dx * dx + dy * dy > radius * radius) continue;
        const x = cx + dx, y = cy + dy;
        if (x < 0 || x >= width || y < 0 || y >= height) continue;
        grid[y * width + x] = ZONES.tee_box;
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Process one hole
// ---------------------------------------------------------------------------

async function classifyHole(courseId, holeNumber) {
  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
  const rawPath = path.join(holeDir, 'illustration_raw.png');

  if (!fs.existsSync(rawPath)) {
    console.error(`  illustration_raw.png not found for hole ${holeNumber}`);
    return null;
  }

  // Read pixel data
  const { data, info } = await sharp(rawPath)
    .raw()
    .toBuffer({ resolveWithObject: true });
  const { width, height, channels } = info;
  const totalPixels = width * height;

  // Phase 1: Per-pixel classification
  let grid = new Uint8Array(totalPixels);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * channels;
      grid[y * width + x] = classifyPixel(data[idx], data[idx + 1], data[idx + 2]);
    }
  }

  // Phase 2a: Majority filter (2 passes with radius 2)
  grid = majorityFilter(grid, width, height, 2);
  grid = majorityFilter(grid, width, height, 1);

  // Phase 2b: Absorb small regions (<50 pixels)
  grid = absorbSmallRegions(grid, width, height, 50);

  // Phase 2c: Mark tee boxes from tees.json
  const teesPath = path.join(holeDir, 'tees.json');
  if (fs.existsSync(teesPath)) {
    const teesData = JSON.parse(fs.readFileSync(teesPath, 'utf-8'));
    markTeeBoxes(grid, width, height, teesData.tees, 12);
  }

  // Phase 2d: Green sanity check
  const warnings = [];
  const greenPixels = grid.filter(z => z === ZONES.green).length;
  const greenPct = greenPixels / totalPixels * 100;
  if (greenPct > 5) {
    warnings.push(`green zone is large (${greenPct.toFixed(1)}%) — may include misclassified fairway`);
  }
  if (greenPixels === 0) {
    warnings.push('no green zone detected');
  }

  // Phase 3: Output zone map image
  const outRgb = Buffer.alloc(width * height * 3);
  for (let i = 0; i < totalPixels; i++) {
    const c = ZONE_COLORS[grid[i]];
    outRgb[i * 3]     = c[0];
    outRgb[i * 3 + 1] = c[1];
    outRgb[i * 3 + 2] = c[2];
  }
  await sharp(outRgb, { raw: { width, height, channels: 3 } })
    .png()
    .toFile(path.join(holeDir, 'zones.png'));

  // Compute zone stats
  const zoneCounts = {};
  for (const [name, idx] of Object.entries(ZONES)) {
    const count = grid.filter(z => z === idx).length;
    zoneCounts[name] = {
      pixel_count: count,
      percentage: parseFloat((count / totalPixels * 100).toFixed(1)),
    };
  }

  // Encode grid as base64
  const gridBase64 = Buffer.from(grid).toString('base64');

  const output = {
    hole_number: holeNumber,
    source: 'illustration_raw.png',
    source_dimensions: { width, height },
    zone_index: Object.fromEntries(Object.entries(ZONES).map(([k, v]) => [v, k])),
    zone_stats: zoneCounts,
    grid_encoding: 'base64_uint8',
    grid: gridBase64,
  };

  fs.writeFileSync(
    path.join(holeDir, 'zones.json'),
    JSON.stringify(output, null, 2),
    'utf-8'
  );

  return { ...output, warnings };
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const courseId = process.argv[2];
  const holeArg = process.argv[3];

  if (!courseId || !holeArg) {
    console.error('Usage: node scripts/classify-zones.mjs <course-id> <hole-number|--all>');
    process.exit(1);
  }

  const holes = holeArg === '--all'
    ? Array.from({ length: 18 }, (_, i) => i + 1)
    : [parseInt(holeArg, 10)];

  if (holes.some(h => isNaN(h) || h < 1 || h > 18)) {
    console.error('Hole number must be 1-18 or --all');
    process.exit(1);
  }

  console.log(`Classifying zones for ${holes.length} hole(s)\n`);

  for (const h of holes) {
    process.stdout.write(`Hole ${h}/18 ... `);
    const result = await classifyHole(courseId, h);
    if (!result) { console.log('FAILED'); continue; }

    // Print top zone stats
    const stats = result.zone_stats;
    const topZones = Object.entries(stats)
      .filter(([, v]) => v.percentage > 0.5)
      .sort(([, a], [, b]) => b.percentage - a.percentage)
      .map(([k, v]) => `${k}=${v.percentage}%`)
      .join(', ');
    console.log(`OK  ${topZones}`);

    if (result.warnings.length > 0) {
      for (const w of result.warnings) console.log(`  WARNING: ${w}`);
    }
  }

  console.log('\nDone.');
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
