#!/usr/bin/env node
/**
 * classify-zones.mjs — Zone classification from hand-painted PNGs
 *
 * Unlike UHole Lite (which auto-classifies from illustration colors),
 * this reads manually painted zone PNGs and optional overlay masks.
 *
 * Usage:
 *   node scripts/classify-zones.mjs lomond-country-club 7       # single hole
 *   node scripts/classify-zones.mjs lomond-country-club --all   # all 18
 *
 * Input files (in output/{courseId}/holes/{pad}/):
 *   zones-painted.png       — hand-painted zone colors (required)
 *   trees-mask.png          — optional overlay (white = trees)
 *   cart-path-mask.png      — optional overlay (white = cart path)
 *   ob-mask.png             — optional overlay (white = OB)
 *
 * Output files:
 *   zones.json              — grid data + stats (matches UHole Lite format)
 *   zones.png               — visualization with zone colors
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

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

// Zone colors for matching painted pixels and visualization [R, G, B]
const ZONE_COLORS = [
  [0,   0,   0  ],  // 0 = background
  [0,   204, 0  ],  // 1 = fairway
  [128, 255, 64 ],  // 2 = green
  [102, 136, 51 ],  // 3 = semi_rough
  [51,  102, 34 ],  // 4 = rough
  [26,  51,  16 ],  // 5 = trees
  [221, 204, 136],  // 6 = bunker
  [51,  102, 204],  // 7 = water
  [153, 153, 153],  // 8 = cart_path
  [255, 51,  51 ],  // 9 = ob
  [255, 255, 255],  // 10 = tee_box
];

// Overlay zones — extracted from painted PNG into separate masks,
// and their terrain_grid pixels default to rough (4)
const OVERLAY_ZONES = new Set([ZONES.trees, ZONES.cart_path, ZONES.ob]);

// ---------------------------------------------------------------------------
// Nearest zone color via Euclidean RGB distance
// ---------------------------------------------------------------------------

function classifyPixel(r, g, b) {
  let bestZone = 0;
  let bestDist = Infinity;

  for (let z = 0; z < ZONE_COLORS.length; z++) {
    const [cr, cg, cb] = ZONE_COLORS[z];
    const dr = r - cr;
    const dg = g - cg;
    const db = b - cb;
    const dist = dr * dr + dg * dg + db * db;
    if (dist < bestDist) {
      bestDist = dist;
      bestZone = z;
    }
  }

  return bestZone;
}

// ---------------------------------------------------------------------------
// Load a mask PNG (white = 1, black = 0), returns null if file missing
// ---------------------------------------------------------------------------

async function loadMask(filePath, width, height) {
  if (!fs.existsSync(filePath)) return null;

  const { data, info } = await sharp(filePath)
    .resize(width, height, { kernel: 'nearest' })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });

  const mask = new Uint8Array(width * height);
  const channels = info.channels;

  for (let i = 0; i < width * height; i++) {
    // White = mask active (R > 128)
    mask[i] = data[i * channels] > 128 ? 1 : 0;
  }

  return mask;
}

// ---------------------------------------------------------------------------
// Process one hole
// ---------------------------------------------------------------------------

async function classifyHole(courseId, holeNumber) {
  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
  const paintedPath = path.join(holeDir, 'zones-painted.png');

  if (!fs.existsSync(paintedPath)) {
    console.error(`  zones-painted.png not found for hole ${holeNumber}`);
    return null;
  }

  // Read painted zone image
  const { data, info } = await sharp(paintedPath)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const { width, height, channels } = info;
  const totalPixels = width * height;

  // Classify each pixel by nearest zone color
  const grid = new Uint8Array(totalPixels);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * channels;
      grid[y * width + x] = classifyPixel(data[idx], data[idx + 1], data[idx + 2]);
    }
  }

  // Build terrain grid: overlay zones (trees, cart_path, ob) default to rough
  const terrainGrid = new Uint8Array(totalPixels);
  for (let i = 0; i < totalPixels; i++) {
    terrainGrid[i] = OVERLAY_ZONES.has(grid[i]) ? ZONES.rough : grid[i];
  }

  // Extract overlay masks from the painted classification
  const treesMask = new Uint8Array(totalPixels);
  const cartPathMask = new Uint8Array(totalPixels);
  const obMask = new Uint8Array(totalPixels);

  for (let i = 0; i < totalPixels; i++) {
    if (grid[i] === ZONES.trees)     treesMask[i] = 1;
    if (grid[i] === ZONES.cart_path) cartPathMask[i] = 1;
    if (grid[i] === ZONES.ob)        obMask[i] = 1;
  }

  // Load optional overlay mask PNGs (override painted classification)
  const treesOverlay    = await loadMask(path.join(holeDir, 'trees-mask.png'), width, height);
  const cartPathOverlay = await loadMask(path.join(holeDir, 'cart-path-mask.png'), width, height);
  const obOverlay       = await loadMask(path.join(holeDir, 'ob-mask.png'), width, height);

  // Merge overlay masks: external mask PNGs override the painted ones
  if (treesOverlay) {
    for (let i = 0; i < totalPixels; i++) {
      if (treesOverlay[i]) { treesMask[i] = 1; }
    }
  }
  if (cartPathOverlay) {
    for (let i = 0; i < totalPixels; i++) {
      if (cartPathOverlay[i]) { cartPathMask[i] = 1; }
    }
  }
  if (obOverlay) {
    for (let i = 0; i < totalPixels; i++) {
      if (obOverlay[i]) { obMask[i] = 1; }
    }
  }

  // Build merged grid: start from terrain, then apply overlay masks
  const mergedGrid = new Uint8Array(terrainGrid);
  for (let i = 0; i < totalPixels; i++) {
    if (treesMask[i])    mergedGrid[i] = ZONES.trees;
    if (cartPathMask[i]) mergedGrid[i] = ZONES.cart_path;
    if (obMask[i])       mergedGrid[i] = ZONES.ob;
  }

  // Generate visualization PNG from merged grid
  const outRgb = Buffer.alloc(width * height * 3);
  for (let i = 0; i < totalPixels; i++) {
    const c = ZONE_COLORS[mergedGrid[i]];
    outRgb[i * 3]     = c[0];
    outRgb[i * 3 + 1] = c[1];
    outRgb[i * 3 + 2] = c[2];
  }
  await sharp(outRgb, { raw: { width, height, channels: 3 } })
    .png()
    .toFile(path.join(holeDir, 'zones.png'));

  // Compute zone stats from merged grid
  const zoneCounts = {};
  for (const [name, idx] of Object.entries(ZONES)) {
    const count = mergedGrid.filter(z => z === idx).length;
    zoneCounts[name] = {
      pixel_count: count,
      percentage: parseFloat((count / totalPixels * 100).toFixed(1)),
    };
  }

  // Encode grids and masks as base64
  const output = {
    hole_number: holeNumber,
    source_dimensions: { width, height },
    zone_index: Object.fromEntries(
      Object.entries(ZONES).map(([k, v]) => [String(v), k])
    ),
    zone_stats: zoneCounts,
    grid_encoding: 'base64_uint8',
    grid: Buffer.from(mergedGrid).toString('base64'),
    terrain_grid: Buffer.from(terrainGrid).toString('base64'),
    trees_mask: Buffer.from(treesMask).toString('base64'),
    ob_mask: Buffer.from(obMask).toString('base64'),
    cart_path_mask: Buffer.from(cartPathMask).toString('base64'),
  };

  fs.writeFileSync(
    path.join(holeDir, 'zones.json'),
    JSON.stringify(output, null, 2),
    'utf-8'
  );

  return output;
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
  }

  console.log('\nDone.');
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
