/**
 * dev-server.mjs — GeoAlign dev server
 *
 * Express server on port 3200 that serves the GeoAlign app
 * and provides APIs for GSI tile compositing and alignment I/O.
 */

import express from "express";
import { readFile, writeFile, stat, mkdir, readdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";
import {
  tileRangeForRadius, enumerateTiles,
  tileXToLon, tileYToLat, metersPerPixel
} from "./lib/tiles.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const uholeLiteRoot = path.resolve(root, "..", "UHoleLite");
const cacheDir = path.join(root, "cache", "gsi-photo-z18");

const COURSE_CENTER_LAT = 34.91318;
const COURSE_CENTER_LON = 136.44164;
const ZOOM = 18;
const TILE_SIZE = 256;
const TILE_URL = "https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg";

const port = Number(process.env.PORT || 3200);
const app = express();
app.use(express.json({ limit: "10mb" }));

// ── Helpers ──────────────────────────────────────────────

async function fileExists(p) {
  try { await stat(p); return true; } catch { return false; }
}

function pad2(n) {
  return String(n).padStart(2, "0");
}

function holeDir(courseId, holeNumber) {
  return path.join(uholeLiteRoot, "output", courseId, "holes", pad2(holeNumber));
}

async function fetchAndCacheTile(z, x, y) {
  await mkdir(cacheDir, { recursive: true });
  const cachePath = path.join(cacheDir, `${z}-${x}-${y}.jpg`);
  if (await fileExists(cachePath)) {
    return cachePath;
  }
  const url = TILE_URL.replace("{z}", z).replace("{x}", x).replace("{y}", y);
  const resp = await fetch(url);
  if (!resp.ok) {
    throw new Error(`GSI tile ${z}/${x}/${y}: HTTP ${resp.status}`);
  }
  const buf = Buffer.from(await resp.arrayBuffer());
  await writeFile(cachePath, buf);
  return cachePath;
}

// ── Composite cache ──────────────────────────────────────

const compositeCache = new Map();

async function buildComposite(courseId, holeNumber) {
  const cacheKey = `${courseId}-${holeNumber}`;
  if (compositeCache.has(cacheKey)) return compositeCache.get(cacheKey);

  // 20x20 tile grid centered on course center (~2.5km coverage)
  const gridHalf = 10;
  const centerTileX = Math.floor(((COURSE_CENTER_LON + 180) / 360) * (2 ** ZOOM));
  const radLat = (COURSE_CENTER_LAT * Math.PI) / 180;
  const centerTileY = Math.floor((1 - Math.log(Math.tan(radLat) + 1 / Math.cos(radLat)) / Math.PI) / 2 * (2 ** ZOOM));

  const minX = centerTileX - gridHalf;
  const maxX = centerTileX + gridHalf - 1;
  const minY = centerTileY - gridHalf;
  const maxY = centerTileY + gridHalf - 1;

  const cols = maxX - minX + 1;
  const rows = maxY - minY + 1;
  const compositeW = cols * TILE_SIZE;
  const compositeH = rows * TILE_SIZE;

  // Fetch all tiles
  const overlays = [];
  for (let ty = minY; ty <= maxY; ty++) {
    for (let tx = minX; tx <= maxX; tx++) {
      try {
        const tilePath = await fetchAndCacheTile(ZOOM, tx, ty);
        overlays.push({
          input: tilePath,
          left: (tx - minX) * TILE_SIZE,
          top: (ty - minY) * TILE_SIZE,
        });
      } catch (err) {
        console.warn(`  Missing tile ${ZOOM}/${tx}/${ty}: ${err.message}`);
      }
    }
  }

  // Composite with sharp
  const composite = await sharp({
    create: {
      width: compositeW,
      height: compositeH,
      channels: 3,
      background: { r: 4, g: 9, b: 16 },
    },
  })
    .composite(overlays)
    .jpeg({ quality: 85 })
    .toBuffer();

  // Compute bounds
  const bounds = {
    west: tileXToLon(minX, ZOOM),
    north: tileYToLat(minY, ZOOM),
    east: tileXToLon(maxX + 1, ZOOM),
    south: tileYToLat(maxY + 1, ZOOM),
  };

  const mpp = metersPerPixel(COURSE_CENTER_LAT, ZOOM);

  const result = {
    buffer: composite,
    meta: {
      width: compositeW,
      height: compositeH,
      bounds,
      zoom: ZOOM,
      meters_per_pixel: Math.round(mpp * 1000) / 1000,
    },
  };

  compositeCache.set(cacheKey, result);
  return result;
}

// ── Static files ─────────────────────────────────────────

app.use("/app", express.static(path.join(root, "app")));
app.get("/", (_req, res) => res.redirect("/app/index.html"));

// ── API: Course data ─────────────────────────────────────

app.get("/api/course", async (req, res) => {
  try {
    const courseId = req.query.id || "lomond-country-club";
    const coursePath = path.join(uholeLiteRoot, "output", courseId, "course.json");
    const course = JSON.parse(await readFile(coursePath, "utf8"));
    res.json(course);
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

// ── API: Hole metadata ───────────────────────────────────

app.get("/api/hole/:courseId/:holeNumber", async (req, res) => {
  try {
    const dir = holeDir(req.params.courseId, Number(req.params.holeNumber));
    const result = {};

    try { result.extractMeta = JSON.parse(await readFile(path.join(dir, "extract-meta.json"), "utf8")); } catch {}
    try { result.terrainMeta = JSON.parse(await readFile(path.join(dir, "terrain-meta.json"), "utf8")); } catch {}
    try { result.geoAlign = JSON.parse(await readFile(path.join(dir, "geo-align.json"), "utf8")); } catch {}

    result.hasIllustration = await fileExists(path.join(dir, "illustration.png"));
    res.json(result);
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

// ── API: Illustration image ──────────────────────────────

app.get("/api/illustration/:courseId/:holeNumber", async (req, res) => {
  try {
    const dir = holeDir(req.params.courseId, Number(req.params.holeNumber));
    const imgPath = path.join(dir, "illustration.png");
    if (!await fileExists(imgPath)) {
      return res.status(404).json({ ok: false, message: "Illustration not found" });
    }
    res.sendFile(imgPath);
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

// ── API: Single GSI tile (proxy + cache) ─────────────────

app.get("/api/gsi-tile/:z/:x/:y", async (req, res) => {
  try {
    const { z, x, y } = req.params;
    const tilePath = await fetchAndCacheTile(Number(z), Number(x), Number(y));
    res.sendFile(tilePath);
  } catch (err) {
    res.status(404).json({ ok: false, message: err.message });
  }
});

// ── API: GSI composite metadata ──────────────────────────

app.get("/api/gsi-composite/:courseId/:holeNumber", async (req, res) => {
  try {
    const { courseId, holeNumber } = req.params;
    const { meta } = await buildComposite(courseId, Number(holeNumber));
    res.json({
      image_url: `/api/gsi-composite-image/${courseId}/${holeNumber}`,
      ...meta,
    });
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

// ── API: GSI composite image ─────────────────────────────

app.get("/api/gsi-composite-image/:courseId/:holeNumber", async (req, res) => {
  try {
    const { courseId, holeNumber } = req.params;
    const { buffer } = await buildComposite(courseId, Number(holeNumber));
    res.set("Content-Type", "image/jpeg");
    res.set("Cache-Control", "no-cache");
    res.send(buffer);
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

// ── API: Save alignment ──────────────────────────────────

app.post("/api/save-alignment/:courseId/:holeNumber", async (req, res) => {
  try {
    const dir = holeDir(req.params.courseId, Number(req.params.holeNumber));
    await mkdir(dir, { recursive: true });
    const filePath = path.join(dir, "geo-align.json");
    await writeFile(filePath, JSON.stringify(req.body, null, 2) + "\n", "utf8");
    res.json({ ok: true, path: filePath });
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

// ── API: Load alignment ──────────────────────────────────

app.get("/api/load-alignment/:courseId/:holeNumber", async (req, res) => {
  try {
    const dir = holeDir(req.params.courseId, Number(req.params.holeNumber));
    const filePath = path.join(dir, "geo-align.json");
    if (!await fileExists(filePath)) {
      return res.status(404).json({ ok: false, message: "No alignment found" });
    }
    const data = JSON.parse(await readFile(filePath, "utf8"));
    res.json(data);
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

// ── API: Fetch tiles (re-fetch on demand) ────────────────

const FETCH_RADIUS_M = 2000;
const FETCH_RATE_MS = 100;
let fetchInProgress = false;

async function fetchAllTiles() {
  if (fetchInProgress) return { ok: false, message: "Fetch already in progress" };
  fetchInProgress = true;

  const range = tileRangeForRadius(COURSE_CENTER_LAT, COURSE_CENTER_LON, ZOOM, FETCH_RADIUS_M);
  const tiles = enumerateTiles(range, ZOOM);
  let downloaded = 0, skipped = 0, failed = 0;

  console.log(`[fetch] Fetching ${tiles.length} GSI z${ZOOM} tiles...`);

  for (const tile of tiles) {
    const cachePath = path.join(cacheDir, `${tile.z}-${tile.x}-${tile.y}.jpg`);
    if (await fileExists(cachePath)) { skipped++; continue; }

    const url = TILE_URL.replace("{z}", tile.z).replace("{x}", tile.x).replace("{y}", tile.y);
    try {
      const resp = await fetch(url);
      if (!resp.ok) { failed++; continue; }
      const buf = Buffer.from(await resp.arrayBuffer());
      await writeFile(cachePath, buf);
      downloaded++;
      if (downloaded % 50 === 0) console.log(`[fetch] ${downloaded} downloaded...`);
      await new Promise(r => setTimeout(r, FETCH_RATE_MS));
    } catch { failed++; }
  }

  fetchInProgress = false;
  const msg = `Done: ${downloaded} downloaded, ${skipped} cached, ${failed} failed`;
  console.log(`[fetch] ${msg}`);
  return { ok: true, total: tiles.length, downloaded, skipped, failed, message: msg };
}

async function autoFetchIfEmpty() {
  await mkdir(cacheDir, { recursive: true });
  try {
    const files = await readdir(cacheDir);
    const jpgs = files.filter(f => f.endsWith(".jpg"));
    if (jpgs.length > 0) {
      console.log(`[fetch] Cache has ${jpgs.length} tiles, skipping auto-fetch`);
      return;
    }
  } catch {}
  console.log("[fetch] Cache empty — running first-time tile fetch...");
  await fetchAllTiles();
}

app.get("/api/fetch-tiles", async (_req, res) => {
  try {
    // Clear composite cache so fresh tiles are used
    compositeCache.clear();
    const result = await fetchAllTiles();
    res.json(result);
  } catch (err) {
    res.status(500).json({ ok: false, message: err.message });
  }
});

app.get("/api/fetch-status", (_req, res) => {
  res.json({ in_progress: fetchInProgress });
});

// ── Start ────────────────────────────────────────────────

app.listen(port, "127.0.0.1", async () => {
  console.log(`GeoAlign → http://127.0.0.1:${port}`);
  await autoFetchIfEmpty();
});
