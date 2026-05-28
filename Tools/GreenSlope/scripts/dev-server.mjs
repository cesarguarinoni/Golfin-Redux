import { createServer } from "node:http";
import { readFile, writeFile, mkdir, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

// GreenSlope authoring tool — standalone GUI, matches UHoleGeo conventions.
// Serves app/, reads green contours live from the repo, saves authored
// region JSON to output/. Launch via "Launch GUI" launcher or `npm run gui`.

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");            // Tools/GreenSlope
const repoRoot = path.resolve(root, "..", "..");        // GolfinRedux
const host = "127.0.0.1";
const port = Number(process.env.PORT || 4178);

const contentTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg",
  ".svg": "image/svg+xml",
};

function hh(n) { return String(n).padStart(2, "0"); }

// Read the green contour (world XZ) for a hole from the repo greens.json.
async function readContour(hole) {
  const p = path.join(repoRoot,
    "Assets/Golf/Courses/lomond-country-club/Data",
    `hole-${hh(hole)}-geo`, "greens.json");
  const raw = JSON.parse(await readFile(p, "utf8"));
  const g = raw.greens[0];
  return {
    contour: g.contour.map((c) => [c.x, c.z]),
    heightM: raw.height_m ?? 0.15,
    center: g.center_local, size: g.size_m,
  };
}

// Read bunker polygons (world XZ) for a hole from the repo bunkers.json.
// Each bunker: { id, contour:[[x,z],…], center:[x,z] }. The client filters to
// the green-side bunkers and uses them as alignment landmarks.
async function readBunkers(hole) {
  const p = path.join(repoRoot,
    "Assets/Golf/Courses/lomond-country-club/Data",
    `hole-${hh(hole)}-geo`, "bunkers.json");
  const raw = JSON.parse(await readFile(p, "utf8"));
  return (raw.bunkers || []).map((b) => ({
    id: b.id,
    contour: (b.contour || []).map((c) => [c.x, c.z]),
    center: b.center_local ? [b.center_local.x, b.center_local.z] : null,
  }));
}

function readBody(req) {
  return new Promise((resolve) => {
    let b = ""; req.setEncoding("utf8");
    req.on("data", (c) => (b += c)); req.on("end", () => resolve(b));
  });
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://${host}:${port}`);

  // --- API: contour for a hole ---
  if (url.pathname === "/api/contour") {
    const hole = Number(url.searchParams.get("hole") || 7);
    try {
      const data = await readContour(hole);
      res.writeHead(200, { "Content-Type": "application/json", "Cache-Control": "no-cache" });
      res.end(JSON.stringify({ hole, ...data }));
    } catch (e) {
      res.writeHead(404); res.end("contour not found: " + e.message);
    }
    return;
  }

  // --- API: nearby bunker polygons for a hole (alignment landmarks) ---
  // NEW route (not one of the original three). Tolerant of missing bunkers.json
  // — returns an empty list rather than 404 so the client degrades gracefully.
  if (url.pathname === "/api/bunkers") {
    const hole = Number(url.searchParams.get("hole") || 7);
    let bunkers = [];
    try { bunkers = await readBunkers(hole); } catch (e) { bunkers = []; }
    res.writeHead(200, { "Content-Type": "application/json", "Cache-Control": "no-cache" });
    res.end(JSON.stringify({ hole, bunkers }));
    return;
  }

  // --- API: panel image (base64) for a hole, from bundled data/panels.json ---
  if (url.pathname === "/api/panel") {
    const hole = Number(url.searchParams.get("hole") || 7);
    try {
      const panels = JSON.parse(await readFile(path.join(root, "data", "panels.json"), "utf8"));
      const p = panels[hole] || panels[String(hole)];
      res.writeHead(200, { "Content-Type": "application/json", "Cache-Control": "no-cache" });
      res.end(JSON.stringify(p.panel));
    } catch (e) {
      res.writeHead(404); res.end("panel not found: " + e.message);
    }
    return;
  }

  // --- API: save authored regions to output/ ---
  if (url.pathname === "/api/save" && req.method === "POST") {
    try {
      const body = await readBody(req);
      const payload = JSON.parse(body);
      const hole = payload.hole || 0;
      await mkdir(path.join(root, "output"), { recursive: true });
      const fn = `hole_${hh(hole)}_slope_authoring.json`;
      await writeFile(path.join(root, "output", fn), JSON.stringify(payload, null, 2));
      console.log(`[save] wrote output/${fn} — ${payload.regionCount} regions, ridge=${payload.ridgePresent}`);
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: true, file: `output/${fn}` }));
    } catch (e) {
      res.writeHead(500); res.end("save failed: " + e.message);
    }
    return;
  }

  // --- Static files from app/ ---
  const rel = url.pathname === "/" ? "/app/index.html" : url.pathname;
  const target = path.normalize(path.join(root, rel));
  if (!target.startsWith(root)) { res.writeHead(403); res.end("Forbidden"); return; }
  try {
    const s = await stat(target);
    const finalPath = s.isDirectory() ? path.join(target, "index.html") : target;
    const body = await readFile(finalPath);
    const ext = path.extname(finalPath).toLowerCase();
    res.writeHead(200, { "Content-Type": contentTypes[ext] || "application/octet-stream", "Cache-Control": "no-cache" });
    res.end(body);
  } catch {
    res.writeHead(404); res.end("Not found: " + url.pathname);
  }
});

server.listen(port, host, () => {
  console.log(`GreenSlope GUI → http://${host}:${port}`);
});
