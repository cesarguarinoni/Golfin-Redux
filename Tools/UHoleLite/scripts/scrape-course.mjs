#!/usr/bin/env node
/**
 * scrape-course.mjs — Step 1: Download hole map GIFs + build course.json
 *
 * Usage: node scripts/scrape-course.mjs lomond-country-club
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import fetch from 'node-fetch';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

// ---------------------------------------------------------------------------
// Hardcoded yardage data (fallback if scraping fails)
// ---------------------------------------------------------------------------

const YARDAGE_DATA = [
  // OUT
  { number: 1,  par: 5, hdcp: 9,  back: 531, regular: 509, front: 488, ladies: 458 },
  { number: 2,  par: 4, hdcp: 3,  back: 403, regular: 391, front: 378, ladies: 366 },
  { number: 3,  par: 4, hdcp: 15, back: 368, regular: 349, front: 315, ladies: 285 },
  { number: 4,  par: 3, hdcp: 13, back: 138, regular: 126, front: 124, ladies: 106 },
  { number: 5,  par: 4, hdcp: 1,  back: 425, regular: 383, front: 372, ladies: 366 },
  { number: 6,  par: 3, hdcp: 7,  back: 193, regular: 173, front: 149, ladies: 129 },
  { number: 7,  par: 4, hdcp: 5,  back: 430, regular: 410, front: 375, ladies: 335 },
  { number: 8,  par: 5, hdcp: 17, back: 562, regular: 480, front: 469, ladies: 452 },
  { number: 9,  par: 4, hdcp: 11, back: 434, regular: 403, front: 376, ladies: 357 },
  // IN
  { number: 10, par: 4, hdcp: 10, back: 392, regular: 368, front: 346, ladies: 328 },
  { number: 11, par: 3, hdcp: 16, back: 179, regular: 162, front: 138, ladies: 120 },
  { number: 12, par: 4, hdcp: 4,  back: 435, regular: 359, front: 348, ladies: 269 },
  { number: 13, par: 5, hdcp: 8,  back: 597, regular: 559, front: 534, ladies: 508 },
  { number: 14, par: 4, hdcp: 2,  back: 420, regular: 392, front: 373, ladies: 357 },
  { number: 15, par: 3, hdcp: 14, back: 184, regular: 169, front: 146, ladies: 121 },
  { number: 16, par: 4, hdcp: 18, back: 356, regular: 327, front: 316, ladies: 292 },
  { number: 17, par: 4, hdcp: 6,  back: 410, regular: 379, front: 353, ladies: 328 },
  { number: 18, par: 5, hdcp: 12, back: 567, regular: 548, front: 521, ladies: 502 },
];

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function downloadFile(url, destPath, retries = 3) {
  for (let attempt = 1; attempt <= retries; attempt++) {
    try {
      const res = await fetch(url);
      if (!res.ok) throw new Error(`HTTP ${res.status} for ${url}`);
      const buffer = Buffer.from(await res.arrayBuffer());
      fs.writeFileSync(destPath, buffer);
      return true;
    } catch (err) {
      if (attempt === retries) {
        console.error(`  FAILED after ${retries} attempts: ${err.message}`);
        return false;
      }
      console.warn(`  Attempt ${attempt} failed, retrying...`);
      await new Promise(r => setTimeout(r, 1000 * attempt));
    }
  }
}

async function fetchText(url, retries = 3) {
  for (let attempt = 1; attempt <= retries; attempt++) {
    try {
      const res = await fetch(url);
      if (!res.ok) throw new Error(`HTTP ${res.status} for ${url}`);
      return await res.text();
    } catch (err) {
      if (attempt === retries) {
        console.error(`  FAILED to fetch ${url}: ${err.message}`);
        return null;
      }
      await new Promise(r => setTimeout(r, 1000 * attempt));
    }
  }
}

/**
 * Try to parse hole descriptions from the official course page HTML.
 * Returns a Map<number, string> of hole number -> Japanese description.
 */
function parseHoleDescriptions(html) {
  const descriptions = new Map();
  if (!html) return descriptions;

  // The hole descriptions appear in the page near each hole section.
  // They're typically in <p> or <div> tags after the hole number heading.
  // Pattern: look for text blocks that contain golf-specific Japanese terms.
  // This is best-effort; we use the hardcoded data as primary source.

  // Try to find description blocks — common patterns on Japanese golf sites:
  // Look for blocks between hole markers
  const holePattern = /No\.?\s*(\d{1,2})\s*[\s\S]*?(?:par|PAR|Par)[\s\S]*?<\/(?:div|td|p)>/gi;
  let match;
  while ((match = holePattern.exec(html)) !== null) {
    const holeNum = parseInt(match[1], 10);
    if (holeNum >= 1 && holeNum <= 18) {
      // Extract Japanese text from the block
      const block = match[0];
      const textOnly = block.replace(/<[^>]+>/g, '').trim();
      // Find the Japanese description part (after the hole/par info)
      const jpMatch = textOnly.match(/[\u3000-\u9FFF\uF900-\uFAFF].{10,}/);
      if (jpMatch) {
        descriptions.set(holeNum, jpMatch[0].trim());
      }
    }
  }

  return descriptions;
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const courseId = process.argv[2];
  if (!courseId) {
    console.error('Usage: node scripts/scrape-course.mjs <course-id>');
    console.error('  e.g. node scripts/scrape-course.mjs lomond-country-club');
    process.exit(1);
  }

  // Load config
  const configPath = path.join(ROOT, 'config', `${courseId}.json`);
  if (!fs.existsSync(configPath)) {
    console.error(`Config not found: ${configPath}`);
    process.exit(1);
  }
  const config = JSON.parse(fs.readFileSync(configPath, 'utf-8'));
  console.log(`Course: ${config.display_name} (${config.native_name})`);

  // Create output directories
  const outputDir = path.join(ROOT, 'output', courseId);
  const sourceDir = path.join(outputDir, 'source');
  fs.mkdirSync(sourceDir, { recursive: true });

  // -----------------------------------------------------------------------
  // 1. Download all 18 hole GIFs
  // -----------------------------------------------------------------------
  console.log('\n--- Downloading hole map GIFs ---');
  const urlPattern = config.sources.hole_map_pattern;
  let downloadedCount = 0;

  for (let i = 1; i <= 18; i++) {
    const nn = String(i).padStart(2, '0');
    const url = urlPattern.replace('{NN}', nn);
    const dest = path.join(sourceDir, `course_e${nn}.gif`);
    process.stdout.write(`  Hole ${i}/18 ... `);
    const ok = await downloadFile(url, dest);
    if (ok) {
      const size = fs.statSync(dest).size;
      console.log(`OK (${(size / 1024).toFixed(1)} KB)`);
      downloadedCount++;
    } else {
      console.log('FAILED');
    }
  }
  console.log(`Downloaded ${downloadedCount}/18 hole maps.`);

  // -----------------------------------------------------------------------
  // 2. Download course overview map
  // -----------------------------------------------------------------------
  console.log('\n--- Downloading course overview map ---');
  const overviewUrl = config.sources.course_map_url;
  const overviewDest = path.join(sourceDir, 'course_d01.gif');
  process.stdout.write('  Overview map ... ');
  const overviewOk = await downloadFile(overviewUrl, overviewDest);
  console.log(overviewOk ? 'OK' : 'FAILED');

  // -----------------------------------------------------------------------
  // 3. Try to scrape hole descriptions from official page
  // -----------------------------------------------------------------------
  console.log('\n--- Fetching course page for descriptions ---');
  const officialHtml = await fetchText(config.sources.official_url);
  const descriptions = parseHoleDescriptions(officialHtml);
  console.log(`  Parsed ${descriptions.size} hole descriptions from official page.`);

  // -----------------------------------------------------------------------
  // 4. Build course.json using hardcoded yardage + scraped descriptions
  // -----------------------------------------------------------------------
  console.log('\n--- Building course.json ---');

  const holes = YARDAGE_DATA.map(h => {
    const nn = String(h.number).padStart(2, '0');
    return {
      number: h.number,
      par: h.par,
      hdcp: h.hdcp,
      tees: {
        back:    { yards: h.back,    color: 'blue' },
        regular: { yards: h.regular, color: 'green' },
        front:   { yards: h.front,   color: 'white' },
        ladies:  { yards: h.ladies,  color: 'red' },
      },
      description_jp: descriptions.get(h.number) || null,
      green_dimensions: null, // to be added later (OCR or manual)
      source_gif: `source/course_e${nn}.gif`,
    };
  });

  // Verify totals
  const outBack = holes.slice(0, 9).reduce((s, h) => s + h.tees.back.yards, 0);
  const inBack = holes.slice(9).reduce((s, h) => s + h.tees.back.yards, 0);
  const totalBack = outBack + inBack;
  console.log(`  Yardage totals (back): OUT=${outBack}, IN=${inBack}, Total=${totalBack}`);

  const courseJson = {
    course_id: courseId,
    display_name: config.display_name,
    native_name: config.native_name,
    source_urls: {
      official: config.sources.official_url,
      gora: config.sources.gora_url,
    },
    course_type: 'hilly',
    elevation_profile: 'moderate',
    area_m2: 1370000,
    greens: 'bent',
    holes,
  };

  const courseJsonPath = path.join(outputDir, 'course.json');
  fs.writeFileSync(courseJsonPath, JSON.stringify(courseJson, null, 2), 'utf-8');
  console.log(`  Written: ${courseJsonPath}`);

  // -----------------------------------------------------------------------
  // Summary
  // -----------------------------------------------------------------------
  console.log('\n=== Summary ===');
  console.log(`  Hole maps: ${downloadedCount}/18`);
  console.log(`  Overview:  ${overviewOk ? 'yes' : 'no'}`);
  console.log(`  course.json: written`);
  console.log(`  Back tee total: ${totalBack} yards`);
  if (totalBack !== 7024) {
    console.warn(`  WARNING: Expected 7024 yards, got ${totalBack}`);
  }
  console.log('\nDone.');
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
