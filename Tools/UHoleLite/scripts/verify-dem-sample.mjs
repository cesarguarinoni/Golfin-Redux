#!/usr/bin/env node
/**
 * verify-dem-sample.mjs — Directly sample DEM at specific lat/lon and compare
 * with what ended up in the heightmap at the corresponding position.
 *
 * Usage: node scripts/verify-dem-sample.mjs lomond-country-club 7
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readDem5aTile, sampleDem5a } from './lib/dem5a.mjs';
import { latToTileY, lonToTileX, tileBounds } from './lib/tiles.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const RES = 2049;
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

async function main() {
  const courseId = process.argv[2];
  const holeNumber = parseInt(process.argv[3], 10);
  if (!courseId || isNaN(holeNumber)) {
    console.error('Usage: node scripts/verify-dem-sample.mjs <course-id> <hole>');
    process.exit(1);
  }

  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
  const exportDir = path.join(ROOT, 'output', courseId, 'export', `hole-${nn}`);

  const meta = JSON.parse(fs.readFileSync(path.join(holeDir, 'terrain-meta.json'), 'utf-8'));
  const geoAlign = JSON.parse(fs.readFileSync(path.join(holeDir, 'geo-align.json'), 'utf-8'));
  const { a, b, c, d, tx, ty } = geoAlign.transform.coefficients;
  const illW = geoAlign.illustration_dimensions.width;
  const illH = geoAlign.illustration_dimensions.height;

  // Load DEM tiles
  const bounds = geoAlign.terrain_bounds_latlon;
  const demTiles = await loadDemTiles(courseId, bounds);
  console.log(`Loaded ${demTiles.length} DEM tiles\n`);

  // Read post-rotation heightmap
  const rawBytes = fs.readFileSync(path.join(exportDir, 'heightmap.raw'));
  const heightmap = new Uint16Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    heightmap[i] = (rawBytes[i * 2] << 8) | rawBytes[i * 2 + 1];
  }

  // Sample a grid of points: for each, get:
  // 1. The lat/lon via affine transform
  // 2. The real DEM elevation at that lat/lon
  // 3. The heightmap uint16 value at that position
  //
  // If DEM and heightmap track together, the relative ordering should match.
  // If there's a flip, they'll be inverted.

  console.log('=== DEM vs Heightmap comparison ===');
  console.log('For each sample point: direct DEM elevation (meters ASL) vs heightmap uint16');
  console.log('If these track together (both high or both low), DEM mapping is correct.');
  console.log('If they diverge (one high, other low), there is a flip.\n');

  // Sample along the center column (hx=RES/2, varying hz)
  // This is the north→south cross-section
  const centerHx = Math.floor(RES / 2);
  console.log(`--- North→South cross-section (hx=${centerHx}) ---`);
  console.log('  hz     | lat       lon       | DEM (m ASL) | heightmap | match?');
  console.log('  -------|-----------|-----------|-------------|-----------|-------');
  
  const steps = 20;
  let mismatches = 0;
  const demValues = [];
  const hmValues = [];
  
  for (let s = 0; s <= steps; s++) {
    const hz = Math.floor(s / steps * (RES - 1));
    
    // After transpose: heightmap[hz][hx] was originally at original[hx][hz]
    // Original sampling: ix = hx/(RES-1)*(illW-1), iy = hz/(RES-1)*(illH-1)
    // Wait — let me re-derive this carefully.
    //
    // generate-terrain.mjs builds original[hy][hx] by sampling DEM at:
    //   ix = hx/(RES-1) * (illW-1)
    //   iy = hy/(RES-1) * (illH-1)
    //
    // Then: rotated[dstRow=x][dstCol=y] = original[srcRow=y][srcCol=x]
    //   i.e., rotated[x][y] = original[y][x]
    //
    // So rotated[hz][hx] = original[hx][hz]
    //   The original value at (hy=hx, hx=hz):
    //     ix = hz/(RES-1) * (illW-1)    ← hz maps to illustration X!
    //     iy = hx/(RES-1) * (illH-1)    ← hx maps to illustration Y!

    const ix = hz / (RES - 1) * (illW - 1);
    const iy = centerHx / (RES - 1) * (illH - 1);
    const lon_ = a * ix + b * iy + tx;
    const lat_ = c * ix + d * iy + ty;
    
    const demElev = sampleDem5a(demTiles, lat_, lon_);
    const hmVal = heightmap[hz * RES + centerHx];
    
    demValues.push(demElev);
    hmValues.push(hmVal);
    
    console.log(`  ${String(hz).padStart(5)} | ${lat_.toFixed(5)} ${lon_.toFixed(5)} | ${demElev !== null ? demElev.toFixed(1).padStart(8) : '    null'} m  | ${String(hmVal).padStart(9)} |`);
  }

  // Check correlation: are they monotonically similar?
  let concordant = 0, discordant = 0;
  for (let i = 0; i < demValues.length - 1; i++) {
    for (let j = i + 1; j < demValues.length; j++) {
      if (demValues[i] === null || demValues[j] === null) continue;
      const demDiff = demValues[j] - demValues[i];
      const hmDiff = hmValues[j] - hmValues[i];
      if ((demDiff > 0 && hmDiff > 0) || (demDiff < 0 && hmDiff < 0)) concordant++;
      else if ((demDiff > 0 && hmDiff < 0) || (demDiff < 0 && hmDiff > 0)) discordant++;
    }
  }
  const kendall = (concordant - discordant) / (concordant + discordant);
  console.log(`\n  Kendall tau = ${kendall.toFixed(3)} (1.0 = perfect match, -1.0 = perfectly inverted)`);

  // Also do the center row (hz=RES/2, varying hx) — left→right
  const centerHz = Math.floor(RES / 2);
  const demValuesLR = [];
  const hmValuesLR = [];
  
  console.log(`\n--- Left→Right cross-section (hz=${centerHz}) ---`);
  console.log('  hx     | lat       lon       | DEM (m ASL) | heightmap | match?');
  console.log('  -------|-----------|-----------|-------------|-----------|-------');
  
  for (let s = 0; s <= steps; s++) {
    const hx = Math.floor(s / steps * (RES - 1));
    const ix = centerHz / (RES - 1) * (illW - 1);
    const iy = hx / (RES - 1) * (illH - 1);
    const lon_ = a * ix + b * iy + tx;
    const lat_ = c * ix + d * iy + ty;
    
    const demElev = sampleDem5a(demTiles, lat_, lon_);
    const hmVal = heightmap[centerHz * RES + hx];
    
    demValuesLR.push(demElev);
    hmValuesLR.push(hmVal);
    
    console.log(`  ${String(hx).padStart(5)} | ${lat_.toFixed(5)} ${lon_.toFixed(5)} | ${demElev !== null ? demElev.toFixed(1).padStart(8) : '    null'} m  | ${String(hmVal).padStart(9)} |`);
  }

  let concordantLR = 0, discordantLR = 0;
  for (let i = 0; i < demValuesLR.length - 1; i++) {
    for (let j = i + 1; j < demValuesLR.length; j++) {
      if (demValuesLR[i] === null || demValuesLR[j] === null) continue;
      const demDiff = demValuesLR[j] - demValuesLR[i];
      const hmDiff = hmValuesLR[j] - hmValuesLR[i];
      if ((demDiff > 0 && hmDiff > 0) || (demDiff < 0 && hmDiff < 0)) concordantLR++;
      else if ((demDiff > 0 && hmDiff < 0) || (demDiff < 0 && hmDiff > 0)) discordantLR++;
    }
  }
  const kendallLR = (concordantLR - discordantLR) / (concordantLR + discordantLR);
  console.log(`\n  Kendall tau = ${kendallLR.toFixed(3)}`);

  console.log('\n=== Summary ===');
  console.log(`North-South axis: tau = ${kendall.toFixed(3)} ${Math.abs(kendall) > 0.5 ? (kendall > 0 ? '✓ CORRECT' : '✗ INVERTED') : '? UNCLEAR'}`);
  console.log(`Left-Right axis:  tau = ${kendallLR.toFixed(3)} ${Math.abs(kendallLR) > 0.5 ? (kendallLR > 0 ? '✓ CORRECT' : '✗ INVERTED') : '? UNCLEAR'}`);
}

main().catch(err => { console.error(err); process.exit(1); });
