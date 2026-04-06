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

  // --- Copy files ---
  fs.copyFileSync(path.join(holeDir, 'heightmap.raw'), path.join(exportDir, 'heightmap.raw'));
  fs.copyFileSync(path.join(holeDir, 'illustration.png'), path.join(exportDir, 'texture.png'));
  fs.copyFileSync(path.join(holeDir, 'zones.json'), path.join(exportDir, 'zones.json'));

  return {
    manifest,
    anchorCount: anchors.length,
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
      console.log(`OK  export/hole-${nn}/  par=${m.par}  ${m.championship_yards}yd  ${m.terrain.terrain_width_m}×${m.terrain.terrain_length_m}m  ${result.anchorCount} anchors`);
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
