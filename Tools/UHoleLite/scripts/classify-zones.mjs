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

const ZONE_RES = 2048; // Zone grid resolution (longest side)

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

  // 1. Near-black → background (illustration margins)
  if (l < 0.08) return ZONES.background;

  // 2. Bunker — pink/light red (#fbd9de: h≈351, s≈0.81, l≈0.92)
  if (l > 0.78 && s > 0.20 && (h > 330 || h < 25)) return ZONES.bunker;

  // 3. Cart path — purple/lilac (#e1abe0: h≈301, s≈0.47, l≈0.78)
  if (h >= 260 && h <= 330 && s > 0.20 && l > 0.55) return ZONES.cart_path;

  // 4. OB — white overlay surrounding OB areas (washed-out terrain)
  if (l > 0.78 && s < 0.25) return ZONES.ob;

  // 5. Blue → water
  if (h >= 180 && h <= 260 && s > 0.25) return ZONES.water;

  // 6. Green hue range — main classification area
  if (h >= 55 && h <= 185) {
    // Very dark green → trees (#1a3310: h≈103, s≈0.52, l≈0.13)
    if (l < 0.18) return ZONES.trees;

    // Putting green — yellow-green hue, bright, high sat (#43bd0a: h≈101, s≈0.90, l≈0.39)
    if (h < 115 && s > 0.65 && l > 0.28) return ZONES.green;

    // Rough — dark pure green (#028505: h≈121, s≈0.97, l≈0.27)
    if (h >= 115 && s > 0.70 && l < 0.29) return ZONES.rough;

    // Fairway — medium pure green (#049707/#02a603: h≈121, s≈0.95, l≈0.30-0.33)
    if (h >= 115 && s > 0.70 && l >= 0.29) return ZONES.fairway;

    // Semi-rough — lower saturation greens
    if (s > 0.25 && l >= 0.18 && l <= 0.50) return ZONES.semi_rough;

    return ZONES.rough;
  }

  // 7. Light, low saturation outside green hue → OB
  if (l > 0.70 && s < 0.30) return ZONES.ob;

  // 8. Default
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
// Morphological close — smooth zone boundaries
// ---------------------------------------------------------------------------

/**
 * Morphological close (dilate → erode) on a per-zone basis.
 * Smooths jagged boundaries without significantly changing zone shapes.
 * Circular kernel for isotropic smoothing.
 */
function morphClose(grid, width, height, radius = 3) {
  const result = new Uint8Array(width * height);
  result.fill(0);

  // Process zones in priority order (higher priority overwrites lower)
  const zonePriority = [4, 5, 3, 1, 10, 2, 6, 7, 8, 9];

  for (const zone of zonePriority) {
    const mask = new Uint8Array(width * height);
    let count = 0;
    for (let i = 0; i < width * height; i++) {
      if (grid[i] === zone) { mask[i] = 1; count++; }
    }
    if (count === 0) continue;

    // Dilate
    const dilated = new Uint8Array(width * height);
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        if (!mask[y * width + x]) continue;
        for (let dy = -radius; dy <= radius; dy++) {
          for (let dx = -radius; dx <= radius; dx++) {
            if (dx * dx + dy * dy > radius * radius) continue;
            const nx = x + dx, ny = y + dy;
            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
              dilated[ny * width + nx] = 1;
          }
        }
      }
    }

    // Erode
    const closed = new Uint8Array(width * height);
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        if (!dilated[y * width + x]) continue;
        let allSet = true;
        for (let dy = -radius; dy <= radius && allSet; dy++) {
          for (let dx = -radius; dx <= radius && allSet; dx++) {
            if (dx * dx + dy * dy > radius * radius) continue;
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height ||
                !dilated[ny * width + nx])
              allSet = false;
          }
        }
        if (allSet) closed[y * width + x] = 1;
      }
    }

    for (let i = 0; i < width * height; i++) {
      if (closed[i]) result[i] = zone;
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
  const rawPath = path.join(holeDir, 'illustration.png');

  if (!fs.existsSync(rawPath)) {
    console.error(`  illustration.png not found for hole ${holeNumber}`);
    return null;
  }

  // Read pixel data — upscale to ZONE_RES on longest side for finer boundaries
  const metadata = await sharp(rawPath).metadata();
  const scale = ZONE_RES / Math.max(metadata.width, metadata.height);
  const targetW = Math.round(metadata.width * scale);
  const targetH = Math.round(metadata.height * scale);

  const { data, info } = await sharp(rawPath)
    .resize(targetW, targetH, { kernel: 'lanczos3' })
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

  // Phase 2b2: Morphological close — smooth zone boundaries
  grid = morphClose(grid, width, height, 3);

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
    source: 'illustration.png',
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
