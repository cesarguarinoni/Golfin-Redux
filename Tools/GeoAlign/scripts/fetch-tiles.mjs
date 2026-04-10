/**
 * fetch-tiles.mjs
 *
 * Pre-downloads GSI z18 satellite tiles for the course area.
 *
 * Usage:
 *   node scripts/fetch-tiles.mjs lomond-country-club
 */

import { mkdir, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { tileRangeForRadius, enumerateTiles } from "./lib/tiles.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const cacheDir = path.join(root, "cache", "gsi-photo-z18");

const COURSE_CENTER_LAT = 34.91318;
const COURSE_CENTER_LON = 136.44164;
const COVERAGE_RADIUS_M = 2000;
const ZOOM = 18;
const RATE_LIMIT_MS = 100;
const TILE_URL = "https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg";

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

async function fileExists(p) {
  try { await stat(p); return true; } catch { return false; }
}

async function main() {
  const courseId = process.argv[2] || "lomond-country-club";
  console.log(`Fetching GSI z${ZOOM} tiles for ${courseId}`);
  console.log(`Center: ${COURSE_CENTER_LAT}, ${COURSE_CENTER_LON}`);
  console.log(`Radius: ${COVERAGE_RADIUS_M}m`);

  await mkdir(cacheDir, { recursive: true });

  const range = tileRangeForRadius(COURSE_CENTER_LAT, COURSE_CENTER_LON, ZOOM, COVERAGE_RADIUS_M);
  const tiles = enumerateTiles(range, ZOOM);

  console.log(`Tile range: X[${range.minX}..${range.maxX}] Y[${range.minY}..${range.maxY}]`);
  console.log(`Total tiles: ${tiles.length}`);

  let downloaded = 0;
  let skipped = 0;
  let failed = 0;

  for (const tile of tiles) {
    const cachePath = path.join(cacheDir, `${tile.z}-${tile.x}-${tile.y}.jpg`);

    if (await fileExists(cachePath)) {
      skipped++;
      continue;
    }

    const url = TILE_URL
      .replace("{z}", tile.z)
      .replace("{x}", tile.x)
      .replace("{y}", tile.y);

    try {
      const resp = await fetch(url);
      if (!resp.ok) {
        console.warn(`  FAIL ${tile.z}/${tile.x}/${tile.y}: HTTP ${resp.status}`);
        failed++;
        continue;
      }
      const buf = Buffer.from(await resp.arrayBuffer());
      await writeFile(cachePath, buf);
      downloaded++;

      if (downloaded % 50 === 0) {
        console.log(`  Downloaded ${downloaded} tiles...`);
      }

      await sleep(RATE_LIMIT_MS);
    } catch (err) {
      console.warn(`  FAIL ${tile.z}/${tile.x}/${tile.y}: ${err.message}`);
      failed++;
    }
  }

  console.log(`\nDone: ${downloaded} downloaded, ${skipped} cached, ${failed} failed.`);
}

main();
