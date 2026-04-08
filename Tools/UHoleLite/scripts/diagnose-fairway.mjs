#!/usr/bin/env node
/**
 * diagnose-fairway.mjs — Diagnose fairway width issues
 * 
 * Compares raw zone grid width vs smoothed contour width at each row.
 * Run: node scripts/diagnose-fairway.mjs lomond-country-club 1
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

const courseId = process.argv[2] || 'lomond-country-club';
const holeNum = parseInt(process.argv[3] || '1');
const nn = String(holeNum).padStart(2, '0');

const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
const exportDir = path.join(ROOT, 'output', courseId, 'export', `hole-${nn}`);

// Read zone grid
const zonesData = JSON.parse(fs.readFileSync(path.join(holeDir, 'zones.json'), 'utf-8'));
const grid = Buffer.from(zonesData.grid, 'base64');
const w = zonesData.source_dimensions.width;
const h = zonesData.source_dimensions.height;

const terrainMeta = JSON.parse(fs.readFileSync(path.join(holeDir, 'terrain-meta.json'), 'utf-8'));
const tw = terrainMeta.terrain_width_m;
const tl = terrainMeta.terrain_length_m;
const mPerPxW = tw / w;
const mPerPxH = tl / h;

console.log(`Zone grid: ${w}x${h} (${mPerPxW.toFixed(3)} m/px width, ${mPerPxH.toFixed(3)} m/px height)`);

// Read smoothed contour
const fwData = JSON.parse(fs.readFileSync(path.join(exportDir, 'fairway-contours.json'), 'utf-8'));
const fw1 = fwData.fairways[0]; // largest fairway

console.log(`\nFairway #1: ${fw1.pixel_count} pixels, ${fw1.contour.length} contour points`);
console.log(`Center: (${fw1.center_local.x}, ${fw1.center_local.z}), Size: ${fw1.size_m.x}x${fw1.size_m.z}m`);

// Measure zone grid fairway width at each row
console.log('\n=== Zone Grid Fairway Width (every 50 rows) ===');
console.log('row  | normY | localZ   | minX | maxX | widthPx | widthM');
console.log('-'.repeat(65));

const gridWidths = [];
for (let y = 0; y < h; y++) {
  let minX = w, maxX = -1;
  for (let x = 0; x < w; x++) {
    if (grid[y * w + x] === 1) {
      if (x < minX) minX = x;
      if (x > maxX) maxX = x;
    }
  }
  const widthPx = maxX >= minX ? (maxX - minX + 1) : 0;
  const widthM = widthPx * mPerPxW;
  const normY = y / (h - 1);
  const localZ = (normY - 0.5) * tl;
  gridWidths.push({ y, normY, localZ, minX, maxX, widthPx, widthM });
  
  if (y % 50 === 0 && widthPx > 0) {
    console.log(`${String(y).padStart(4)} | ${normY.toFixed(3)} | ${localZ.toFixed(1).padStart(8)} | ${String(minX).padStart(4)} | ${String(maxX).padStart(4)} | ${String(widthPx).padStart(7)} | ${widthM.toFixed(1).padStart(6)}`);
  }
}

// Measure smoothed contour width at corresponding Z positions
console.log('\n=== Smoothed Contour Width (sampled at grid Z positions) ===');
console.log('localZ   | leftX  | rightX | widthM | gridWidthM | diff');
console.log('-'.repeat(65));

// Sample the contour at various Z values
const contour = fw1.contour;
for (let sampleZ = -10; sampleZ <= 155; sampleZ += 5) {
  // Find contour X extents at this Z by checking which contour edges cross this Z
  let minCX = Infinity, maxCX = -Infinity;
  let crossings = 0;
  
  for (let i = 0; i < contour.length; i++) {
    const j = (i + 1) % contour.length;
    const p1 = contour[i];
    const p2 = contour[j];
    
    // Does edge (p1→p2) cross Z=sampleZ?
    if ((p1.z <= sampleZ && p2.z > sampleZ) || (p2.z <= sampleZ && p1.z > sampleZ)) {
      // Interpolate X at this Z
      const t = (sampleZ - p1.z) / (p2.z - p1.z);
      const crossX = p1.x + t * (p2.x - p1.x);
      if (crossX < minCX) minCX = crossX;
      if (crossX > maxCX) maxCX = crossX;
      crossings++;
    }
  }
  
  if (crossings < 2) continue;
  
  const contourWidth = maxCX - minCX;
  
  // Find corresponding grid width
  // localZ → normY: normY = localZ/tl + 0.5
  const normY = sampleZ / tl + 0.5;
  const gridRow = Math.round(normY * (h - 1));
  const gw = gridRow >= 0 && gridRow < h ? gridWidths[gridRow] : null;
  const gridWidth = gw ? gw.widthM : 0;
  const diff = contourWidth - gridWidth;
  
  const marker = Math.abs(diff) > 5 ? ' *** BIG DIFF' : '';
  console.log(`${sampleZ.toFixed(1).padStart(8)} | ${minCX.toFixed(1).padStart(6)} | ${maxCX.toFixed(1).padStart(6)} | ${contourWidth.toFixed(1).padStart(6)} | ${gridWidth.toFixed(1).padStart(10)} | ${diff.toFixed(1).padStart(5)}${marker}`);
}

// === Check traceBorder completeness ===
console.log('\n=== Border Trace Completeness ===');

// Count border pixels directly from grid
const fairwayPixels = [];
for (let y = 0; y < h; y++) {
  for (let x = 0; x < w; x++) {
    if (grid[y * w + x] === 1) fairwayPixels.push([x, y]);
  }
}

const pixelSet = new Set();
for (const [px, py] of fairwayPixels) {
  pixelSet.add(py * w + px);
}

let totalBorderPixels = 0;
for (const [px, py] of fairwayPixels) {
  const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
  const isBorder = neighbors.some(([nx, ny]) => {
    if (nx < 0 || nx >= w || ny < 0 || ny >= h) return true;
    return !pixelSet.has(ny * w + nx);
  });
  if (isBorder) totalBorderPixels++;
}

// Now do the 8-connected walk (same as traceBorder)
const border = [];
for (const [px, py] of fairwayPixels) {
  const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
  const isBorder = neighbors.some(([nx, ny]) => {
    if (nx < 0 || nx >= w || ny < 0 || ny >= h) return true;
    return !pixelSet.has(ny * w + nx);
  });
  if (isBorder) border.push([px, py]);
}

border.sort((a, b) => a[1] - b[1] || a[0] - b[0]);
const borderSet = new Set(border.map(([x, y]) => y * w + x));
const ordered = [border[0]];
const visitedBorder = new Set();
visitedBorder.add(border[0][1] * w + border[0][0]);

const dirs8 = [[-1,-1],[-1,0],[-1,1],[0,-1],[0,1],[1,-1],[1,0],[1,1]];
let current = border[0];
for (let step = 0; step < border.length * 2; step++) {
  let found = false;
  for (const [dx, dy] of dirs8) {
    const nx = current[0] + dx;
    const ny = current[1] + dy;
    const key = ny * w + nx;
    if (borderSet.has(key) && !visitedBorder.has(key)) {
      visitedBorder.add(key);
      ordered.push([nx, ny]);
      current = [nx, ny];
      found = true;
      break;
    }
  }
  if (!found) break;
}

const tracedCount = ordered.length;
const completionPct = (tracedCount / totalBorderPixels * 100).toFixed(1);

console.log(`Total border pixels: ${totalBorderPixels}`);
console.log(`Traced by 8-walk:    ${tracedCount}`);
console.log(`Completion:           ${completionPct}%`);
console.log(`Missing:              ${totalBorderPixels - tracedCount} pixels`);

if (tracedCount < totalBorderPixels * 0.95) {
  console.log('\n*** INCOMPLETE TRACE DETECTED ***');
  console.log('The 8-connected walk is NOT completing the full perimeter!');
  console.log('This is likely causing the fairway width shrinkage.');

  const startPx = ordered[0];
  const endPx = ordered[ordered.length - 1];
  const startLocal = {
    x: ((startPx[0] / (w-1) - 0.5) * tw).toFixed(1),
    z: ((startPx[1] / (h-1) - 0.5) * tl).toFixed(1)
  };
  const endLocal = {
    x: ((endPx[0] / (w-1) - 0.5) * tw).toFixed(1),
    z: ((endPx[1] / (h-1) - 0.5) * tl).toFixed(1)
  };
  console.log(`Walk started at pixel (${startPx[0]}, ${startPx[1]}) = local (${startLocal.x}, ${startLocal.z})`);
  console.log(`Walk ended   at pixel (${endPx[0]}, ${endPx[1]}) = local (${endLocal.x}, ${endLocal.z})`);

  const dx = endPx[0] - startPx[0];
  const dy = endPx[1] - startPx[1];
  const gapPx = Math.sqrt(dx*dx + dy*dy);
  console.log(`Gap between start/end: ${gapPx.toFixed(1)} pixels (${(gapPx * mPerPxW).toFixed(1)}m)`);
} else {
  console.log('\nTrace appears complete (>95% of border pixels traced).');
}

console.log('\n=== Walk start/end info ===');
console.log(`Walk start: pixel (${ordered[0][0]}, ${ordered[0][1]})`);
console.log(`Walk end:   pixel (${ordered[ordered.length-1][0]}, ${ordered[ordered.length-1][1]})`);

