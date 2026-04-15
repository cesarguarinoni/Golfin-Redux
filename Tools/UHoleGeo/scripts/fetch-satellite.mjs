#!/usr/bin/env node
/**
 * fetch-satellite.mjs — Download and stitch GSI satellite tiles
 *
 * Usage:
 *   node scripts/fetch-satellite.mjs lomond-country-club 7
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';
import { lonToTileX, latToTileY, tileBounds } from './lib/tiles.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

const TILE_SIZE = 256;
const TILE_URL = 'https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg';
const CACHE_DIR = path.join(ROOT, 'tile-cache');
const REQUEST_DELAY_MS = 250; // Rate limit: 4 req/sec max to GSI

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function fetchTile(z, x, y) {
  // Check disk cache first
  const cachePath = path.join(CACHE_DIR, String(z), `${x}-${y}.jpg`);
  if (fs.existsSync(cachePath)) {
    const cached = fs.readFileSync(cachePath);
    if (cached.length >= 500) return { buf: cached, fromCache: true };
  }

  const url = TILE_URL.replace('{z}', z).replace('{x}', x).replace('{y}', y);
  const fetch = (await import('node-fetch')).default;
  const res = await fetch(url, {
    headers: {
      'User-Agent': 'UHoleGeo/0.1 (golf course tool; non-commercial)',
      'Accept': 'image/jpeg,image/*'
    }
  });
  if (!res.ok) throw new Error(`Tile ${z}/${x}/${y} returned ${res.status}`);
  const buf = Buffer.from(await res.arrayBuffer());
  if (buf.length < 500) throw new Error(`Tile ${z}/${x}/${y} too small (${buf.length} bytes)`);

  // Save to cache
  fs.mkdirSync(path.dirname(cachePath), { recursive: true });
  fs.writeFileSync(cachePath, buf);
  return { buf, fromCache: false };
}

async function fetchSatellite(courseId, holeNumber) {
  const pad = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', pad);
  const boundsPath = path.join(holeDir, 'hole-bounds.json');

  if (!fs.existsSync(boundsPath)) {
    console.error(`hole-bounds.json not found: ${boundsPath}`);
    process.exit(1);
  }

  const holeBounds = JSON.parse(fs.readFileSync(boundsPath, 'utf8'));
  const { bounds, gsi_zoom: zoom, pixel_rect } = holeBounds;
  const { north, south, east, west } = bounds;

  console.log(`Fetching satellite tiles for hole ${holeNumber}...`);
  console.log(`  Bounds: N=${north}, S=${south}, E=${east}, W=${west}`);
  console.log(`  Zoom: ${zoom}`);

  // Prefer Leaflet-projected pixel_rect if supplied (avoids any math drift
  // between server and client). Fall back to computing from bounds.
  let pxLeft, pxTop, pxRight, pxBottom;
  if (pixel_rect) {
    pxLeft = pixel_rect.left;
    pxTop = pixel_rect.top;
    pxRight = pixel_rect.right;
    pxBottom = pixel_rect.bottom;
    console.log(`  Using client-supplied pixel_rect: L=${pxLeft.toFixed(2)} T=${pxTop.toFixed(2)} R=${pxRight.toFixed(2)} B=${pxBottom.toFixed(2)}`);
  } else {
    const nFactor = 2 ** zoom;
    pxLeft = ((west + 180) / 360) * 256 * nFactor;
    pxRight = ((east + 180) / 360) * 256 * nFactor;
    const latToPx = (lat) => {
      const r = lat * Math.PI / 180;
      return (1 - Math.log(Math.tan(r) + 1 / Math.cos(r)) / Math.PI) / 2 * 256 * nFactor;
    };
    pxTop = latToPx(north);
    pxBottom = latToPx(south);
  }

  // Ensure sane ordering (defensive — NW should be top-left, SE bottom-right)
  if (pxRight < pxLeft) [pxLeft, pxRight] = [pxRight, pxLeft];
  if (pxBottom < pxTop) [pxTop, pxBottom] = [pxBottom, pxTop];

  // Tile range from pixel rect
  const minTX = Math.floor(pxLeft / 256);
  const maxTX = Math.max(minTX, Math.floor((pxRight - 1e-6) / 256));
  const minTY = Math.floor(pxTop / 256);
  const maxTY = Math.max(minTY, Math.floor((pxBottom - 1e-6) / 256));

  const numTilesX = maxTX - minTX + 1;
  const numTilesY = maxTY - minTY + 1;
  const totalTiles = numTilesX * numTilesY;
  console.log(`  Tile range: X[${minTX}..${maxTX}] Y[${minTY}..${maxTY}] = ${numTilesX}x${numTilesY} (${totalTiles} tiles)`);

  // Download all tiles (cached tiles don't hit the network)
  const tileBuffers = [];
  let count = 0;
  let downloaded = 0;
  let cached = 0;
  for (let ty = minTY; ty <= maxTY; ty++) {
    for (let tx = minTX; tx <= maxTX; tx++) {
      count++;
      process.stdout.write(`  Tile ${count}/${totalTiles}...\r`);
      let fromCache = false;
      try {
        const r = await fetchTile(zoom, tx, ty);
        fromCache = r.fromCache;
        if (fromCache) cached++; else downloaded++;
        tileBuffers.push({ tx, ty, buf: r.buf });
      } catch (err) {
        console.warn(`  Warning: tile ${zoom}/${tx}/${ty} failed: ${err.message}`);
        const blackTile = await sharp({
          create: { width: TILE_SIZE, height: TILE_SIZE, channels: 3, background: { r: 0, g: 0, b: 0 } }
        }).jpeg().toBuffer();
        tileBuffers.push({ tx, ty, buf: blackTile });
      }
      // Rate-limit only network fetches, not cache reads
      if (!fromCache && count < totalTiles) await sleep(REQUEST_DELAY_MS);
    }
  }
  console.log(`  Tiles: ${downloaded} downloaded, ${cached} from cache.`);

  // Stitch into one image
  const fullW = numTilesX * TILE_SIZE;
  const fullH = numTilesY * TILE_SIZE;

  // Normalize every tile to exactly 256x256 RGB PNG. Cached tiles from older
  // script versions or corrupted JPEGs can have wrong dimensions, which breaks
  // sharp's composite ("Image to composite must have same dimensions or smaller").
  for (let i = 0; i < tileBuffers.length; i++) {
    const { tx, ty, buf } = tileBuffers[i];
    try {
      const meta = await sharp(buf).metadata();
      if (meta.width !== TILE_SIZE || meta.height !== TILE_SIZE) {
        console.warn(`  Tile ${zoom}/${tx}/${ty} size ${meta.width}x${meta.height} — resizing to ${TILE_SIZE}x${TILE_SIZE}`);
      }
      // Always re-encode as 256x256 RGB PNG — guarantees sharp can composite it.
      tileBuffers[i].buf = await sharp(buf)
        .resize(TILE_SIZE, TILE_SIZE, { fit: 'fill' })
        .removeAlpha()
        .png()
        .toBuffer();
    } catch (err) {
      console.warn(`  Tile ${zoom}/${tx}/${ty} invalid (${err.message}) — using black`);
      tileBuffers[i].buf = await sharp({
        create: { width: TILE_SIZE, height: TILE_SIZE, channels: 3, background: { r: 0, g: 0, b: 0 } }
      }).png().toBuffer();
    }
  }

  const composites = tileBuffers.map(({ tx, ty, buf }) => ({
    input: buf,
    left: (tx - minTX) * TILE_SIZE,
    top: (ty - minTY) * TILE_SIZE,
  }));

  // Step 1: Composite tiles into a stitched PNG buffer
  const stitchedBuf = await sharp({
    create: { width: fullW, height: fullH, channels: 3, background: { r: 0, g: 0, b: 0 } }
  }).composite(composites).png().toBuffer();

  // Step 2: Compute crop coords
  const tileOriginX = minTX * 256;
  const tileOriginY = minTY * 256;

  const cropLeft   = Math.round(pxLeft   - tileOriginX);
  const cropTop    = Math.round(pxTop    - tileOriginY);
  const cropRight  = Math.round(pxRight  - tileOriginX);
  const cropBottom = Math.round(pxBottom - tileOriginY);

  const cropW = Math.max(1, Math.min(fullW - cropLeft, cropRight - cropLeft));
  const cropH = Math.max(1, Math.min(fullH - cropTop, cropBottom - cropTop));

  console.log(`  Crop: ${cropLeft},${cropTop} ${cropW}x${cropH} (from ${fullW}x${fullH})`);

  // Ensure painted zones have at least MIN_PAINT_RES detail on the longest side.
  // Only UPSCALES small crops — never downscales (would throw away real detail
  // when fetching at high zoom levels).
  const MIN_PAINT_RES = 2048;
  const longestSide = Math.max(cropW, cropH);
  const scaleFactor = longestSide < MIN_PAINT_RES ? MIN_PAINT_RES / longestSide : 1;
  const finalW = Math.round(cropW * scaleFactor);
  const finalH = Math.round(cropH * scaleFactor);

  const outputPath = path.join(holeDir, 'satellite.png');
  let pipe = sharp(stitchedBuf)
    .extract({ left: cropLeft, top: cropTop, width: cropW, height: cropH });
  if (scaleFactor > 1) {
    pipe = pipe.resize(finalW, finalH, { kernel: 'lanczos3' });
    console.log(`  Upscaling ${cropW}x${cropH} → ${finalW}x${finalH} (factor ${scaleFactor.toFixed(2)})`);
  } else {
    console.log(`  Native resolution kept: ${cropW}x${cropH} (>= ${MIN_PAINT_RES}px)`);
  }
  await pipe.png().toFile(outputPath);

  // Update hole-bounds.json with image dimensions (post-upscale)
  holeBounds.image_file = 'satellite.png';
  holeBounds.image_dimensions = { width: finalW, height: finalH };
  fs.writeFileSync(boundsPath, JSON.stringify(holeBounds, null, 2) + '\n', 'utf8');

  console.log(`  Saved: ${outputPath} (${finalW}x${finalH})`);
  return { width: finalW, height: finalH, outputPath };
}

// --- CLI ---
const args = process.argv.slice(2);
if (args.length < 2) {
  console.log('Usage: node scripts/fetch-satellite.mjs <course-id> <hole-number>');
  process.exit(1);
}

const courseId = args[0];
const holeNum = parseInt(args[1], 10);
fetchSatellite(courseId, holeNum).catch(err => {
  console.error('Error:', err.message);
  process.exit(1);
});
