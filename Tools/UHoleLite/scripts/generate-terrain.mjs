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

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

const RES = 129; // heightmap resolution

// Zone indices (must match classify-zones.mjs)
const ZONES = {
  background: 0, fairway: 1, green: 2, semi_rough: 3, rough: 4,
  trees: 5, bunker: 6, water: 7, cart_path: 8, ob: 9, tee_box: 10,
};

// Japanese description terrain hints
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
// Generate heightmap for one hole
// ---------------------------------------------------------------------------

function generateHeightmap(holeNumber, holeData, teesData, zonesData, extractMeta, config) {
  const seed = holeNumber * 1337;
  const terrainDefaults = config.terrain_defaults;

  // --- Terrain sizing ---
  const backYards = holeData.tees.back.yards;
  const backMeters = backYards * 0.9144;
  const terrainLength = backMeters * 1.3;
  const aspectRatio = extractMeta.aspect_ratio || 1.2;
  const terrainWidth = terrainLength / aspectRatio;

  // --- Find green centroid from zone grid ---
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
    : { x: 0.5, y: 0.15 }; // fallback: green near top

  // --- Find tee centroid ---
  const foundTees = teesData.tees.filter(t => t.normalized);
  const teeCentroid = foundTees.length > 0
    ? {
        x: foundTees.reduce((s, t) => s + t.normalized.x, 0) / foundTees.length,
        y: foundTees.reduce((s, t) => s + t.normalized.y, 0) / foundTees.length,
      }
    : { x: 0.5, y: 0.85 }; // fallback: tees near bottom

  // --- Parse terrain hints ---
  const hints = parseHints(holeData.description_jp);
  const hintTypes = new Set(hints.map(h => h.type));

  // Determine slope drop
  let slopeDrop = 6.0; // default: gentle downhill tee→green
  if (hintTypes.has('downhill')) slopeDrop = 12.0;
  if (hintTypes.has('uphill')) slopeDrop = -5.0; // green is higher
  const [dropMin, dropMax] = terrainDefaults.tee_to_green_drop_range_m;
  if (slopeDrop > 0) slopeDrop = Math.min(slopeDrop, dropMax);

  const noiseFreq = terrainDefaults.noise_frequency;
  const noiseAmp = terrainDefaults.base_undulation_m;

  // --- Build heightmap ---
  const heightmap = new Float64Array(RES * RES);

  for (let hy = 0; hy < RES; hy++) {
    for (let hx = 0; hx < RES; hx++) {
      const nx = hx / (RES - 1); // 0-1
      const ny = hy / (RES - 1); // 0-1

      // Layer 1: Base slope (tee→green)
      const teeY = teeCentroid.y;
      const greenY = greenCentroid.y;
      const span = teeY - greenY;
      const t = span !== 0 ? Math.max(0, Math.min(1, (ny - greenY) / span)) : 0.5;
      let baseSlope = slopeDrop * t; // 0 at green, slopeDrop at tee

      // Two-tier fairway: add a step at midpoint
      if (hintTypes.has('two_tier')) {
        const midT = 0.5;
        if (t > midT) {
          baseSlope += 2.0; // step up of 2m in the tee half
        }
      }

      // Layer 2: Perlin noise
      let noise = perlin2D(hx * noiseFreq + seed, hy * noiseFreq + seed * 0.7) * noiseAmp;

      // Layer 3: Zone-based modifiers
      const zoneX = Math.min(zw - 1, Math.floor(nx * (zw - 1)));
      const zoneY = Math.min(zh - 1, Math.floor(ny * (zh - 1)));
      const zone = zoneGrid[zoneY * zw + zoneX];

      let heightMod = 0;
      let isWater = false;

      switch (zone) {
        case ZONES.green:
          noise *= terrainDefaults.green_flatness; // 0.15
          break;
        case ZONES.fairway:
          noise *= 0.45;
          break;
        case ZONES.tee_box:
          noise *= 0.10;
          break;
        case ZONES.bunker:
          heightMod -= terrainDefaults.bunker_depth_m; // -1.5
          noise *= 0.3;
          break;
        case ZONES.water:
          isWater = true;
          break;
        case ZONES.trees:
          heightMod += terrainDefaults.tree_ridge_m; // +3.0
          break;
        case ZONES.cart_path:
          noise *= 0.55;
          break;
        // semi_rough, rough, ob, background: full noise
      }

      let totalHeight = baseSlope + noise + heightMod;

      // Mark water for post-processing
      if (isWater) {
        totalHeight = -9999; // sentinel, will be clamped later
      }

      heightmap[hy * RES + hx] = totalHeight;
    }
  }

  // Layer 4: Green slope (subtle drainage)
  // Default: slight front-to-back tilt of 0.5m
  applyGreenSlope(heightmap, zoneGrid, zw, zh, greenCentroid, holeData.description_jp);

  // Find min ignoring water sentinels
  let globalMin = Infinity, globalMax = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] > -9000) {
      if (heightmap[i] < globalMin) globalMin = heightmap[i];
      if (heightmap[i] > globalMax) globalMax = heightmap[i];
    }
  }

  // Clamp water to minimum
  const waterLevel = globalMin - 2.0;
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] < -9000) heightmap[i] = waterLevel;
  }
  globalMin = waterLevel;

  // Layer 6: Smooth transitions (blur, then restore water)
  const waterMask = new Uint8Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    if (heightmap[i] <= waterLevel + 0.01) waterMask[i] = 1;
  }

  const smoothed = blur2D(heightmap, RES, RES, 2);

  // Restore water pixels
  for (let i = 0; i < RES * RES; i++) {
    if (waterMask[i]) smoothed[i] = waterLevel;
  }

  // Recalculate min/max after blur
  globalMin = Infinity; globalMax = -Infinity;
  for (let i = 0; i < RES * RES; i++) {
    if (smoothed[i] < globalMin) globalMin = smoothed[i];
    if (smoothed[i] > globalMax) globalMax = smoothed[i];
  }

  // Layer 5: Normalize to 0-65535
  const range = globalMax - globalMin || 1;
  const uint16Data = new Uint16Array(RES * RES);
  for (let i = 0; i < RES * RES; i++) {
    const normalized = (smoothed[i] - globalMin) / range;
    uint16Data[i] = Math.round(Math.max(0, Math.min(65535, normalized * 65535)));
  }

  // Verify full range usage
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

// ---------------------------------------------------------------------------
// Apply subtle slope across the green zone
// ---------------------------------------------------------------------------

function applyGreenSlope(heightmap, zoneGrid, zw, zh, greenCentroid, descJp) {
  // Determine slope direction from description
  let slopeDir = 'front'; // default: slopes toward the front (approach side)
  if (descJp) {
    if (descJp.includes('右から傾斜') || descJp.includes('右サイド')) slopeDir = 'right';
    if (descJp.includes('左から傾斜') || descJp.includes('左サイド')) slopeDir = 'left';
    if (descJp.includes('受けグリーン')) slopeDir = 'front';
  }

  const slopeAmount = 0.5; // meters across the green

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

// ---------------------------------------------------------------------------
// Write heightmap.raw (uint16 big-endian)
// ---------------------------------------------------------------------------

function writeHeightmapRaw(outputPath, uint16Data) {
  const buffer = Buffer.alloc(RES * RES * 2);
  for (let i = 0; i < RES * RES; i++) {
    buffer.writeUInt16BE(uint16Data[i], i * 2);
  }
  fs.writeFileSync(outputPath, buffer);
}

// ---------------------------------------------------------------------------
// Process one hole
// ---------------------------------------------------------------------------

async function processHole(courseId, holeNumber, courseJson, config) {
  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);

  // Load required data
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

  // Generate
  const result = generateHeightmap(holeNumber, holeData, teesData, zonesData, extractMeta, config);

  // Write heightmap.raw
  const rawPath = path.join(holeDir, 'heightmap.raw');
  writeHeightmapRaw(rawPath, result.uint16Data);

  // Write terrain-meta.json
  const meta = {
    hole_number: holeNumber,
    heightmap_file: 'heightmap.raw',
    format: 'uint16be',
    resolution: RES,
    terrain_width_m: result.terrainWidth,
    terrain_length_m: result.terrainLength,
    min_elevation_m: result.minElevation,
    max_elevation_m: result.maxElevation,
    slope_direction: result.slopeDrop >= 0 ? 'tee_to_green' : 'green_to_tee',
    slope_drop_m: result.slopeDrop,
    noise_amplitude_m: result.noiseAmp,
    seed: result.seed,
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

  fs.writeFileSync(
    path.join(holeDir, 'terrain-meta.json'),
    JSON.stringify(meta, null, 2),
    'utf-8'
  );

  return { meta, uint16Min: result.uint16Min, uint16Max: result.uint16Max };
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const courseId = process.argv[2];
  const holeArg = process.argv[3];

  if (!courseId || !holeArg) {
    console.error('Usage: node scripts/generate-terrain.mjs <course-id> <hole-number|--all>');
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

  console.log(`Generating terrain for ${holes.length} hole(s)\n`);

  for (const h of holes) {
    process.stdout.write(`Hole ${h}/18 ... `);
    const result = await processHole(courseId, h, courseJson, config);
    if (!result) { console.log('FAILED'); continue; }

    const m = result.meta;
    const hints = m.hints.length > 0 ? ` [${m.hints.join(', ')}]` : '';
    console.log(`OK  ${m.terrain_width_m}×${m.terrain_length_m}m  elev=${m.max_elevation_m}m  drop=${m.slope_drop_m}m  u16=[${result.uint16Min},${result.uint16Max}]${hints}`);
  }

  console.log('\nDone.');
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
