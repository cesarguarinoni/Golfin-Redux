import { createServer } from "node:http";
import { readFile, stat, writeFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const host = "127.0.0.1";
const port = Number(process.env.PORT || 4174); // 4174 to avoid conflict with UHole's 4173

const contentTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".gif": "image/gif",
  ".jpg": "image/jpeg",
  ".svg": "image/svg+xml",
};

function resolveTarget(urlPath) {
  const normalized = decodeURIComponent(urlPath.split("?")[0]);
  const relative = normalized === "/" ? "/app/index.html" : normalized;
  const candidate = path.normalize(path.join(root, relative));
  if (!candidate.startsWith(root)) return null;
  return candidate;
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let body = "";
    req.setEncoding("utf8");
    req.on("data", (c) => (body += c));
    req.on("end", () => resolve(body));
    req.on("error", reject);
  });
}

// Load course.json + per-hole data for the GUI
async function loadCourseData(courseId) {
  const courseDir = path.join(root, "output", courseId);
  const courseJson = JSON.parse(await readFile(path.join(courseDir, "course.json"), "utf8"));

  const holes = [];
  for (let i = 1; i <= 18; i++) {
    const pad = String(i).padStart(2, "0");
    const holeDir = path.join(courseDir, "holes", pad);
    const exportDir = path.join(courseDir, "export", `hole-${pad}`);

    const hole = { number: i };

    // Load what we have
    try { hole.extractMeta = JSON.parse(await readFile(path.join(holeDir, "extract-meta.json"), "utf8")); } catch { }
    try { hole.tees = JSON.parse(await readFile(path.join(holeDir, "tees.json"), "utf8")); } catch { }
    try { hole.terrainMeta = JSON.parse(await readFile(path.join(holeDir, "terrain-meta.json"), "utf8")); } catch { }
    try { hole.zoneStats = JSON.parse(await readFile(path.join(holeDir, "zones.json"), "utf8")); } catch { }
    try { hole.manifest = JSON.parse(await readFile(path.join(exportDir, "hole-manifest.json"), "utf8")); } catch { }
    try { hole.anchors = JSON.parse(await readFile(path.join(exportDir, "anchors.json"), "utf8")); } catch { }

    // Check file existence
    hole.hasIllustration = await fileExists(path.join(holeDir, "illustration.png"));
    hole.hasIllustrationRaw = await fileExists(path.join(holeDir, "illustration_raw.png"));
    hole.hasZonesPng = await fileExists(path.join(holeDir, "zones.png"));
    hole.hasHeightmap = await fileExists(path.join(exportDir, "heightmap.raw"));

    holes.push(hole);
  }

  return { course: courseJson, holes };
}

async function fileExists(p) {
  try { await stat(p); return true; } catch { return false; }
}

// Save orientation overrides per hole
async function saveOrientation(courseId, holeNumber, orientation) {
  const pad = String(holeNumber).padStart(2, "0");
  const overridesDir = path.join(root, "output", courseId, "holes", pad);
  await mkdir(overridesDir, { recursive: true });
  const overridesPath = path.join(overridesDir, "orientation.json");
  await writeFile(overridesPath, JSON.stringify(orientation, null, 2) + "\n", "utf8");
  return { ok: true, message: `Saved orientation for hole ${holeNumber}` };
}

async function loadOrientation(courseId, holeNumber) {
  const pad = String(holeNumber).padStart(2, "0");
  const overridesPath = path.join(root, "output", courseId, "holes", pad, "orientation.json");
  try {
    return JSON.parse(await readFile(overridesPath, "utf8"));
  } catch {
    return { rotation: 0, flipH: false, flipV: false };
  }
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://${host}:${port}`);

  // API: Load course data
  if (req.method === "GET" && url.pathname === "/api/course") {
    const courseId = url.searchParams.get("id") || "lomond-country-club";
    try {
      const data = await loadCourseData(courseId);
      res.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
      res.end(JSON.stringify(data));
    } catch (err) {
      res.writeHead(500, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: false, message: err.message }));
    }
    return;
  }

  // API: Load orientation for a hole
  if (req.method === "GET" && url.pathname === "/api/orientation") {
    const courseId = url.searchParams.get("course") || "lomond-country-club";
    const hole = Number(url.searchParams.get("hole"));
    const data = await loadOrientation(courseId, hole);
    res.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
    res.end(JSON.stringify(data));
    return;
  }

  // API: Save orientation for a hole
  if (req.method === "POST" && url.pathname === "/api/orientation") {
    const body = JSON.parse(await readBody(req));
    const result = await saveOrientation(
      body.courseId || "lomond-country-club",
      body.holeNumber,
      body.orientation
    );
    res.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
    res.end(JSON.stringify(result));
    return;
  }

  // Static files
  const target = resolveTarget(url.pathname);
  if (!target) { res.writeHead(403); res.end("Forbidden"); return; }

  try {
    const s = await stat(target);
    const finalPath = s.isDirectory() ? path.join(target, "index.html") : target;
    const body = await readFile(finalPath);
    const ext = path.extname(finalPath).toLowerCase();
    res.writeHead(200, {
      "Content-Type": contentTypes[ext] || "application/octet-stream",
      "Cache-Control": "no-cache",
    });
    res.end(body);
  } catch {
    res.writeHead(404);
    res.end("Not found");
  }
});

server.listen(port, host, () => {
  console.log(`UHole Lite GUI running at http://${host}:${port}`);
});
