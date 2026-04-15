#!/usr/bin/env node
/**
 * run-all.mjs — Orchestrator: runs classify-zones → generate-terrain → export-hole
 *
 * Usage:
 *   node scripts/run-all.mjs lomond-country-club 7        # single hole
 *   node scripts/run-all.mjs lomond-country-club --all     # all 18
 */

import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function runScript(scriptName, args) {
  return new Promise((resolve, reject) => {
    const scriptPath = path.join(__dirname, scriptName);
    console.log(`\n${'='.repeat(60)}`);
    console.log(`Running: ${scriptName} ${args.join(' ')}`);
    console.log('='.repeat(60));

    const child = spawn(process.execPath, [scriptPath, ...args], {
      cwd: path.resolve(__dirname, '..'),
      env: process.env,
      stdio: 'inherit',
    });

    child.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(`${scriptName} exited with code ${code}`));
      } else {
        resolve();
      }
    });
  });
}

async function main() {
  const args = process.argv.slice(2);
  if (args.length < 2) {
    console.log('Usage: node scripts/run-all.mjs <course-id> <hole-number|--all>');
    process.exit(1);
  }

  const courseId = args[0];
  const holeArg = args[1];

  const steps = [
    'classify-zones.mjs',
    'generate-terrain.mjs',
    'export-hole.mjs',
  ];

  for (const step of steps) {
    try {
      await runScript(step, [courseId, holeArg]);
    } catch (err) {
      console.error(`\nFailed at ${step}: ${err.message}`);
      process.exit(1);
    }
  }

  console.log(`\n${'='.repeat(60)}`);
  console.log('All steps completed successfully!');
  console.log('='.repeat(60));
}

main();
