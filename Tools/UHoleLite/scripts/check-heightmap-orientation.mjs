#!/usr/bin/env node
/**
 * check-heightmap-orientation.mjs — Diagnostic: verify heightmap vs terrain alignment
 *
 * Reads heightmap.raw (post-rotation) and zones.json, then checks whether
 * known features (green, tee, water, bunkers) have the expected relative
 * elevations at their expected positions.
 *
 * Usage: node scripts/check-heightmap-orientation.mjs lomond-country-club 1
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

const ZONES = {
  0: 'background', 1: 'fairway', 2: 'green', 3: 'semi_rough', 4: 'rough',
  5: 'trees', 6: 'bunker', 7: 'water', 8: 'cart_path', 9: 'ob', 10: 'tee_box',
};

function main() {
  const courseId = process.argv[2];
  const holeNumber = parseInt(process.argv[3], 10);
  if (!courseId || isNaN(holeNumber)) {
    console.error('Usage: node scripts/check-heightmap-orientation.mjs <course-id> <hole>');
    process.exit(1);
  }

  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
  const exportDir = path.join(ROOT, 'output', courseId, 'export', `hole-${nn}`);

  // Read terrain-meta
  const meta = JSON.parse(fs.readFileSync(path.join(holeDir, 'terrain-meta.json'), 'utf-8'));
  const RES = meta.resolution;

  // Read heightmap.raw (post-rotation, uint16be)
  const rawBytes = fs.readFileSync(path.join(exportDir, 'heightmap.raw'));
  const heightmap = new Uint16Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    heightmap[i] = (rawBytes[i * 2] << 8) | rawBytes[i * 2 + 1];
  }

  // Read zones.json
  const zonesData = JSON.parse(fs.readFileSync(path.join(holeDir, 'zones.json'), 'utf-8'));
  const zoneGrid = Buffer.from(zonesData.grid, 'base64');
  const zw = zonesData.source_dimensions.width;
  const zh = zonesData.source_dimensions.height;

  console.log(`\n=== Heightmap Orientation Check: Hole ${holeNumber} ===`);
  console.log(`Heightmap: ${RES}x${RES}, Zone grid: ${zw}x${zh}`);
  console.log(`Terrain: ${meta.terrain_width_m}x${meta.terrain_length_m}m`);
  console.log(`Green centroid (norm): x=${meta.green_centroid_normalized.x}, y=${meta.green_centroid_normalized.y}`);
  console.log(`Tee centroid (norm):   x=${meta.tee_centroid_normalized.x}, y=${meta.tee_centroid_normalized.y}`);
  console.log();

  // --- Check 1: Sample heightmap at zone centroids ---
  // For each zone, find the centroid in zone grid, map to heightmap coords,
  // and read the elevation.
  
  // Accumulate zone centroids from zone grid
  const zoneCentroids = {};
  const zoneCounts = {};
  for (let zy = 0; zy < zh; zy++) {
    for (let zx = 0; zx < zw; zx++) {
      const z = zoneGrid[zy * zw + zx];
      if (!zoneCentroids[z]) { zoneCentroids[z] = { sx: 0, sy: 0 }; zoneCounts[z] = 0; }
      zoneCentroids[z].sx += zx;
      zoneCentroids[z].sy += zy;
      zoneCounts[z]++;
    }
  }

  console.log('--- Zone centroids (in zone-grid space) and heightmap samples ---');
  console.log('Zone grid coordinate system: (0,0) = top-left of illustration');
  console.log();

  // The key question: how does the heightmap pixel [hy][hx] map to the zone grid?
  // 
  // In generate-terrain.mjs, BEFORE rotation:
  //   heightmap[hy * RES + hx] corresponds to zone grid at:
  //     nx = hx / (RES-1), ny = hy / (RES-1)
  //     zx = floor(nx * (zw-1)), zy = floor(ny * (zh-1))
  //
  // The "rotation" does: rotated[x * RES + y] = original[y * RES + x]
  // This is a TRANSPOSE: rotated[hy'][hx'] where hy'=old_x, hx'=old_y
  //
  // So after rotation, heightmap[hy][hx] corresponds to:
  //   The cell that was originally at (hx, hy) in pre-rotation space
  //   Which means: old_hx = hy, old_hy = hx  (because rotated swaps)
  //   Wait, let me be precise:
  //   original[y * RES + x] → rotated[x * RES + y]
  //   So rotated[dstRow][dstCol] = original[srcRow][srcCol]
  //   where dstRow = srcCol = x, dstCol = srcRow = y
  //   
  //   To read rotated[hy][hx], we have:
  //     hy = old_x (srcCol), hx = old_y (srcRow)
  //   Which means the zone mapping for rotated[hy][hx] should use:
  //     old_nx = hy / (RES-1)  (was old hx)
  //     old_ny = hx / (RES-1)  (was old hy)
  //   Zone lookup: zx = floor(old_nx * (zw-1)), zy = floor(old_ny * (zh-1))

  // The Unity importer then does a SECOND swap in the zone lookup:
  //   int gx = normZ * (smW-1)  and int gy = normX * (smH-1)
  //   Where normX = hx/(actualRes-1), normZ = hz/(actualRes-1)
  //   So gx = hz/(res-1) * (zw-1), gy = hx/(res-1) * (zh-1)
  //   Meaning: gx corresponds to row index of heightmap, gy to col index

  // Let's just do it empirically. For each zone centroid in zone-grid space,
  // try MULTIPLE mappings to heightmap space and see which one gives
  // consistent results.

  const mappings = [
    {
      name: 'Direct (no swap)',
      map: (zx, zy) => {
        const hx = Math.round(zx / (zw - 1) * (RES - 1));
        const hy = Math.round(zy / (zh - 1) * (RES - 1));
        return [hx, hy];
      }
    },
    {
      name: 'Swap X/Y',
      map: (zx, zy) => {
        const hx = Math.round(zy / (zh - 1) * (RES - 1));
        const hy = Math.round(zx / (zw - 1) * (RES - 1));
        return [hx, hy];
      }
    },
    {
      name: 'Swap + flip X',
      map: (zx, zy) => {
        const hx = RES - 1 - Math.round(zy / (zh - 1) * (RES - 1));
        const hy = Math.round(zx / (zw - 1) * (RES - 1));
        return [hx, hy];
      }
    },
    {
      name: 'Swap + flip Y',
      map: (zx, zy) => {
        const hx = Math.round(zy / (zh - 1) * (RES - 1));
        const hy = RES - 1 - Math.round(zx / (zw - 1) * (RES - 1));
        return [hx, hy];
      }
    },
    {
      name: 'Flip X only',
      map: (zx, zy) => {
        const hx = RES - 1 - Math.round(zx / (zw - 1) * (RES - 1));
        const hy = Math.round(zy / (zh - 1) * (RES - 1));
        return [hx, hy];
      }
    },
    {
      name: 'Flip Y only',
      map: (zx, zy) => {
        const hx = Math.round(zx / (zw - 1) * (RES - 1));
        const hy = RES - 1 - Math.round(zy / (zh - 1) * (RES - 1));
        return [hx, hy];
      }
    },
    {
      name: 'Swap + flip both',
      map: (zx, zy) => {
        const hx = RES - 1 - Math.round(zy / (zh - 1) * (RES - 1));
        const hy = RES - 1 - Math.round(zx / (zw - 1) * (RES - 1));
        return [hx, hy];
      }
    },
    {
      name: 'Flip both',
      map: (zx, zy) => {
        const hx = RES - 1 - Math.round(zx / (zw - 1) * (RES - 1));
        const hy = RES - 1 - Math.round(zy / (zh - 1) * (RES - 1));
        return [hx, hy];
      }
    },
  ];

  // --- Check 2: For the "correct" mapping, green/tee elevations should match slope ---
  // If slope_direction is tee_to_green and slope_drop is positive,
  // tee elevation > green elevation
  
  // Sample 5x5 average around each zone centroid for robustness
  function sampleAvg(hx, hy, r = 2) {
    let sum = 0, count = 0;
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        const sx = hx + dx, sy = hy + dy;
        if (sx >= 0 && sx < RES && sy >= 0 && sy < RES) {
          sum += heightmap[sy * RES + sx];
          count++;
        }
      }
    }
    return count > 0 ? sum / count : 0;
  }

  // Get green and tee centroids
  const greenC = zoneCentroids[2] ? {
    x: zoneCentroids[2].sx / zoneCounts[2],
    y: zoneCentroids[2].sy / zoneCounts[2],
  } : null;
  const teeC = zoneCentroids[10] ? {
    x: zoneCentroids[10].sx / zoneCounts[10],
    y: zoneCentroids[10].sy / zoneCounts[10],
  } : null;

  if (greenC) console.log(`Green centroid in zone grid: (${greenC.x.toFixed(1)}, ${greenC.y.toFixed(1)})`);
  if (teeC) console.log(`Tee centroid in zone grid:   (${teeC.x.toFixed(1)}, ${teeC.y.toFixed(1)})`);
  console.log(`Slope: ${meta.slope_direction}, drop=${meta.slope_drop_m}m`);
  console.log();

  // --- Check 3: Variance analysis ---
  // For the correct mapping, the heightmap values sampled at zone pixels
  // should have LOW variance within flat zones (greens, tees) and
  // HIGHER variance in rough terrain.
  // But more importantly: green/tee zone cells should have
  // CONSISTENT elevation, while randomly sampling from a WRONG mapping
  // would give mixed zones → higher variance.

  console.log('--- Mapping test: Green & Tee elevation ---');
  
  for (const mapping of mappings) {
    // Sample all green pixels through this mapping
    let greenSum = 0, greenCount = 0;
    let teeSum = 0, teeCount = 0;
    let greenVariance = 0, teeVariance = 0;
    
    // First pass: means
    for (let zy = 0; zy < zh; zy += 3) { // subsample for speed
      for (let zx = 0; zx < zw; zx += 3) {
        const zone = zoneGrid[zy * zw + zx];
        const [hx, hy] = mapping.map(zx, zy);
        if (hx < 0 || hx >= RES || hy < 0 || hy >= RES) continue;
        const val = heightmap[hy * RES + hx];
        if (zone === 2) { greenSum += val; greenCount++; }
        if (zone === 10) { teeSum += val; teeCount++; }
      }
    }
    
    const greenMean = greenCount > 0 ? greenSum / greenCount : 0;
    const teeMean = teeCount > 0 ? teeSum / teeCount : 0;
    
    // Second pass: variance
    for (let zy = 0; zy < zh; zy += 3) {
      for (let zx = 0; zx < zw; zx += 3) {
        const zone = zoneGrid[zy * zw + zx];
        const [hx, hy] = mapping.map(zx, zy);
        if (hx < 0 || hx >= RES || hy < 0 || hy >= RES) continue;
        const val = heightmap[hy * RES + hx];
        if (zone === 2) greenVariance += (val - greenMean) ** 2;
        if (zone === 10) teeVariance += (val - teeMean) ** 2;
      }
    }
    
    const greenStd = greenCount > 1 ? Math.sqrt(greenVariance / (greenCount - 1)) : 0;
    const teeStd = teeCount > 1 ? Math.sqrt(teeVariance / (teeCount - 1)) : 0;
    
    const slopeOk = meta.slope_drop_m >= 0 ? teeMean >= greenMean : teeMean <= greenMean;
    
    console.log(`  ${mapping.name.padEnd(20)} | green: mean=${greenMean.toFixed(0)} std=${greenStd.toFixed(0)} (n=${greenCount}) | tee: mean=${teeMean.toFixed(0)} std=${teeStd.toFixed(0)} (n=${teeCount}) | slope ${slopeOk ? 'OK' : 'WRONG'} | combinedStd=${((greenStd + teeStd) / 2).toFixed(0)}`);
  }

  console.log();
  console.log('Best mapping = lowest combined std (green + tee should be flat)');
  console.log('Slope should be OK (tee > green for downhill holes)');

  // --- Check 4: Corner analysis ---
  // Sample the 4 corners of the heightmap and compare
  console.log('\n--- Heightmap corner values (raw uint16) ---');
  const corners = [
    { name: 'Top-Left     [0,0]',       val: heightmap[0] },
    { name: 'Top-Right    [0,RES-1]',   val: heightmap[RES - 1] },
    { name: 'Bottom-Left  [RES-1,0]',   val: heightmap[(RES - 1) * RES] },
    { name: 'Bottom-Right [RES-1,RES-1]', val: heightmap[(RES - 1) * RES + (RES - 1)] },
  ];
  for (const c of corners) {
    console.log(`  ${c.name}: ${c.val}`);
  }

  // --- Check 5: What Unity's importer actually does ---
  console.log('\n--- Unity importer mapping analysis ---');
  console.log('Unity reads: heights[y, x] = raw[y * RES + x]');
  console.log('Unity terrain: position = (-terrainX/2, -, -terrainZ/2)');
  console.log('Where terrainX = terrain_length_m, terrainZ = terrain_width_m (swapped!)');
  console.log(`  terrainX = ${meta.terrain_length_m}m, terrainZ = ${meta.terrain_width_m}m`);
  console.log('Unity zone lookup: gx = normZ * (zw-1), gy = normX * (zh-1)');
  console.log('  This means: heightmap row (Y) → zone X, heightmap col (X) → zone Y');
  console.log();
  
  // So the effective mapping Unity does is:
  // heightmap[hy][hx] → zone(gx=hy/(RES-1)*(zw-1), gy=hx/(RES-1)*(zh-1))
  // Which means the "Swap X/Y" mapping should be correct
  
  // Verify: check what zone the heightmap corners correspond to
  console.log('--- Unity corner-to-zone mapping ---');
  const unityCorners = [
    { hy: 0, hx: 0 },
    { hy: 0, hx: RES - 1 },
    { hy: RES - 1, hx: 0 },
    { hy: RES - 1, hx: RES - 1 },
  ];
  for (const c of unityCorners) {
    // Unity zone lookup
    const normX = c.hx / (RES - 1);
    const normZ = c.hy / (RES - 1);
    const gx = Math.round(normZ * (zw - 1));
    const gy = Math.round(normX * (zh - 1));
    const zone = (gx >= 0 && gx < zw && gy >= 0 && gy < zh) ? zoneGrid[gy * zw + gx] : -1;
    console.log(`  h[${c.hy},${c.hx}] → zone[${gx},${gy}] = ${zone} (${ZONES[zone] || '?'}) | elev=${heightmap[c.hy * RES + c.hx]}`);
  }
}

main();
