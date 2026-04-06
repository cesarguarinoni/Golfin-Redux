#!/usr/bin/env node
/**
 * run-all.mjs — Orchestrator: runs Steps 2-6 for a hole or all 18
 *
 * Usage:
 *   node scripts/run-all.mjs lomond-country-club 1      # single hole
 *   node scripts/run-all.mjs lomond-country-club --all   # all 18
 *
 * Note: Step 1 (scrape) is NOT included — it's a one-time setup step.
 */

import { execSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const STEPS = [
  { name: 'Extract & Upscale',   script: 'extract-hole.mjs' },
  { name: 'Detect Tees',         script: 'detect-tees.mjs' },
  { name: 'Classify Zones',      script: 'classify-zones.mjs' },
  { name: 'Generate Terrain',    script: 'generate-terrain.mjs' },
  { name: 'Export Package',       script: 'export-hole.mjs' },
];

function main() {
  const courseId = process.argv[2];
  const holeArg = process.argv[3];

  if (!courseId || !holeArg) {
    console.error('Usage: node scripts/run-all.mjs <course-id> <hole-number|--all>');
    console.error('  Runs steps 2-6 (extract, detect-tees, classify, terrain, export)');
    console.error('  Step 1 (scrape) must be run separately first.');
    process.exit(1);
  }

  console.log(`\n${'='.repeat(60)}`);
  console.log(`UHole Lite — Full Pipeline for ${courseId} ${holeArg}`);
  console.log(`${'='.repeat(60)}\n`);

  for (const step of STEPS) {
    console.log(`\n--- ${step.name} ---`);
    const scriptPath = path.join(__dirname, step.script);
    try {
      execSync(`node "${scriptPath}" ${courseId} ${holeArg}`, {
        stdio: 'inherit',
        cwd: path.resolve(__dirname, '..'),
      });
    } catch (err) {
      console.error(`\nFATAL: ${step.name} failed with exit code ${err.status}`);
      process.exit(1);
    }
  }

  console.log(`\n${'='.repeat(60)}`);
  console.log('Pipeline complete!');
  console.log(`Export packages: output/${courseId}/export/`);
  console.log(`${'='.repeat(60)}\n`);
}

main();
