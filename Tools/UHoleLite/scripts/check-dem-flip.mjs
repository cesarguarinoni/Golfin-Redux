#!/usr/bin/env node
/**
 * check-dem-flip.mjs — Check if DEM elevations are spatially correct
 * 
 * Samples DEM elevation at a few locations and checks if the heightmap
 * has the right relative elevations (e.g., ravine = low, ridge = high).
 *
 * Usage: node scripts/check-dem-flip.mjs lomond-country-club 7
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const RES = 2049;

function main() {
  const courseId = process.argv[2];
  const holeNumber = parseInt(process.argv[3], 10);
  if (!courseId || isNaN(holeNumber)) {
    console.error('Usage: node scripts/check-dem-flip.mjs <course-id> <hole>');
    process.exit(1);
  }

  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
  const exportDir = path.join(ROOT, 'output', courseId, 'export', `hole-${nn}`);

  // Read terrain-meta
  const meta = JSON.parse(fs.readFileSync(path.join(holeDir, 'terrain-meta.json'), 'utf-8'));

  // Read heightmap.raw (post-rotation, uint16be)
  const rawBytes = fs.readFileSync(path.join(exportDir, 'heightmap.raw'));
  const heightmap = new Uint16Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    heightmap[i] = (rawBytes[i * 2] << 8) | rawBytes[i * 2 + 1];
  }

  // Read geo-align
  const geoAlign = JSON.parse(fs.readFileSync(path.join(holeDir, 'geo-align.json'), 'utf-8'));
  const { a, b, c, d, tx, ty } = geoAlign.transform.coefficients;
  const illW = geoAlign.illustration_dimensions.width;
  const illH = geoAlign.illustration_dimensions.height;

  console.log(`\n=== DEM Flip Check: Hole ${holeNumber} ===`);
  console.log(`Terrain: ${meta.terrain_width_m}x${meta.terrain_length_m}m`);
  console.log(`Illustration: ${illW}x${illH}px`);
  console.log(`Affine: a=${a.toExponential(3)}, b=${b.toExponential(3)}, c=${c.toExponential(3)}, d=${d.toExponential(3)}`);
  console.log(`        tx=${tx}, ty=${ty}`);

  // The transpose rotation:  rotated[x][y] = original[y][x]
  // So rotated[hy][hx] = original[hx][hy]
  // meaning rotated[hy][hx] was sampled from illustration pixel:
  //   ix = (hy / (RES-1)) * (illW - 1)   <-- NOTE: hy maps to ix!
  //   iy = (hx / (RES-1)) * (illH - 1)   <-- NOTE: hx maps to iy!
  
  // After Unity's terrain placement with swapped X/Z:
  //   terrainX = terrain_length_m, terrainZ = terrain_width_m
  //   terrain origin at (-terrainX/2, y, -terrainZ/2)
  //   heightmap[hz][hx] → world position:
  //     worldX = hx/(RES-1) * terrainX + (-terrainX/2)
  //     worldZ = hz/(RES-1) * terrainZ + (-terrainZ/2)
  //   Zone lookup (reverse rotation):
  //     gx = hz/(RES-1) * (zoneW-1)
  //     gy = hx/(RES-1) * (zoneH-1)
  
  // So: heightmap cell [hz, hx] → illustration pixel:
  //   ix = hz / (RES-1) * (illW-1)
  //   iy = hx / (RES-1) * (illH-1)
  //   lon = a * ix + b * iy + tx
  //   lat = c * ix + d * iy + ty

  console.log('\n--- Heightmap corner → lat/lon mapping ---');
  const corners = [
    { name: 'h[0,0]',         hz: 0,       hx: 0 },
    { name: 'h[0,RES-1]',     hz: 0,       hx: RES-1 },
    { name: 'h[RES-1,0]',     hz: RES-1,   hx: 0 },
    { name: 'h[RES-1,RES-1]', hz: RES-1,   hx: RES-1 },
    { name: 'h[center]',      hz: Math.floor(RES/2), hx: Math.floor(RES/2) },
  ];

  for (const corner of corners) {
    const ix = corner.hz / (RES - 1) * (illW - 1);
    const iy = corner.hx / (RES - 1) * (illH - 1);
    const lon = a * ix + b * iy + tx;
    const lat = c * ix + d * iy + ty;
    const elev = heightmap[corner.hz * RES + corner.hx];
    console.log(`  ${corner.name.padEnd(18)} → ill(${ix.toFixed(0)}, ${iy.toFixed(0)}) → (${lat.toFixed(5)}, ${lon.toFixed(5)}) | elev=${elev}`);
  }

  // Now let's check: do the terrain bounds match?
  const bounds = geoAlign.terrain_bounds_latlon;
  console.log('\n--- Terrain bounds from geo-align ---');
  console.log(`  N: ${bounds.north}, S: ${bounds.south}`);
  console.log(`  E: ${bounds.east},  W: ${bounds.west}`);

  // Build elevation profile along the center row (hz = RES/2) 
  // This crosses the hole left-to-right
  const centerHz = Math.floor(RES / 2);
  console.log(`\n--- Cross-section at hz=${centerHz} (center row, left→right) ---`);
  console.log('  This should correspond to a west→east cross-section');
  const steps = 20;
  for (let s = 0; s <= steps; s++) {
    const hx = Math.floor(s / steps * (RES - 1));
    const ix = centerHz / (RES - 1) * (illW - 1);
    const iy = hx / (RES - 1) * (illH - 1);
    const lon = a * ix + b * iy + tx;
    const lat = c * ix + d * iy + ty;
    const elev = heightmap[centerHz * RES + hx];
    const bar = '█'.repeat(Math.floor(elev / 65535 * 40));
    console.log(`  hx=${String(hx).padStart(4)} ill(${ix.toFixed(0)},${iy.toFixed(0).padStart(5)}) → (${lat.toFixed(5)}, ${lon.toFixed(5)}) | ${String(elev).padStart(5)} ${bar}`);
  }

  // Build elevation profile along the center column (hx = RES/2)
  // This crosses the hole top-to-bottom  
  const centerHx = Math.floor(RES / 2);
  console.log(`\n--- Cross-section at hx=${centerHx} (center col, top→bottom) ---`);
  console.log('  This should correspond to a north→south cross-section');
  for (let s = 0; s <= steps; s++) {
    const hz = Math.floor(s / steps * (RES - 1));
    const ix = hz / (RES - 1) * (illW - 1);
    const iy = centerHx / (RES - 1) * (illH - 1);
    const lon = a * ix + b * iy + tx;
    const lat = c * ix + d * iy + ty;
    const elev = heightmap[hz * RES + centerHx];
    const bar = '█'.repeat(Math.floor(elev / 65535 * 40));
    console.log(`  hz=${String(hz).padStart(4)} ill(${ix.toFixed(0).padStart(5)},${iy.toFixed(0).padStart(5)}) → (${lat.toFixed(5)}, ${lon.toFixed(5)}) | ${String(elev).padStart(5)} ${bar}`);
  }

  // Also show what the PRE-rotation heightmap looked like
  // Re-read the raw and UN-rotate it to get the original DEM sampling
  console.log('\n--- Pre-rotation DEM: corners of original[hy][hx] ---');
  // rotated[hz][hx] = original[hx][hz]  (transpose)
  // So original[hy][hx] = rotated[hx][hy]
  // original was sampled at: ix = hx/(RES-1) * (illW-1), iy = hy/(RES-1) * (illH-1)
  const origCorners = [
    { name: 'orig[0,0]',         hy: 0,       hx: 0,       desc: 'illustration top-left' },
    { name: 'orig[0,RES-1]',     hy: 0,       hx: RES-1,   desc: 'illustration top-right' },
    { name: 'orig[RES-1,0]',     hy: RES-1,   hx: 0,       desc: 'illustration bottom-left' },
    { name: 'orig[RES-1,RES-1]', hy: RES-1,   hx: RES-1,   desc: 'illustration bottom-right' },
  ];
  for (const oc of origCorners) {
    const ix = oc.hx / (RES - 1) * (illW - 1);
    const iy = oc.hy / (RES - 1) * (illH - 1);
    const lon = a * ix + b * iy + tx;
    const lat = c * ix + d * iy + ty;
    // Get elevation from rotated heightmap: rotated[hx][hy] = original[hy][hx]
    const rotHz = oc.hx;
    const rotHx = oc.hy;
    const elev = heightmap[rotHz * RES + rotHx];
    console.log(`  ${oc.name.padEnd(22)} ${oc.desc.padEnd(28)} ill(${ix.toFixed(0)},${iy.toFixed(0).padStart(5)}) → (${lat.toFixed(5)}, ${lon.toFixed(5)}) | elev=${elev}`);
  }

  // Key check: Compare the ACTUAL DEM elevation at known control points
  // vs what the heightmap has at those positions
  console.log('\n--- Control point verification ---');
  console.log('  For each geo-align control point, find what the heightmap says');
  for (const cp of geoAlign.control_points) {
    const ix = cp.illustration_px.x;
    const iy = cp.illustration_px.y;
    
    // Before rotation: original[hy][hx] where hx = ix/(illW-1)*(RES-1), hy = iy/(illH-1)*(RES-1)
    const origHx = Math.round(ix / (illW - 1) * (RES - 1));
    const origHy = Math.round(iy / (illH - 1) * (RES - 1));
    
    // After rotation: rotated[origHx][origHy]
    const rotHz = origHx;
    const rotHx = origHy;
    const elev = (rotHz >= 0 && rotHz < RES && rotHx >= 0 && rotHx < RES) 
      ? heightmap[rotHz * RES + rotHx] : -1;
    
    console.log(`  CP ${cp.id}: ill(${ix},${iy}) → orig[${origHy},${origHx}] → rot[${rotHz},${rotHx}] | lat=${cp.world.lat.toFixed(5)} lon=${cp.world.lon.toFixed(5)} | elev=${elev}`);
  }
}

main();
