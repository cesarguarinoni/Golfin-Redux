import { createServer } from "node:http";
import { readFile, stat, writeFile, mkdir } from "node:fs/promises";
import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const host = "127.0.0.1";
const port = Number(process.env.PORT || 4174);

const contentTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".gif": "image/gif",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".svg": "image/svg+xml",
  ".raw": "application/octet-stream",
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

async function fileExists(p) {
  try { await stat(p); return true; } catch { return false; }
}

// Run a Node script as a child process and return stdout/stderr
function runScript(scriptPath, args = []) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [scriptPath, ...args], {
      cwd: root,
      env: process.env,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (c) => (stdout += c.toString()));
    child.stderr.on("data", (c) => (stderr += c.toString()));
    child.on("close", (code) => {
      resolve({ ok: code === 0, code, stdout, stderr });
    });
  });
}

async function loadCourseData(courseId) {
  const courseDir = path.join(root, "output", courseId);
  const courseJson = JSON.parse(await readFile(path.join(courseDir, "course.json"), "utf8"));

  const holes = [];
  for (let i = 1; i <= 18; i++) {
    const pad = String(i).padStart(2, "0");
    const holeDir = path.join(courseDir, "holes", pad);
    const exportDir = path.join(courseDir, "export", `hole-${pad}`);
    const hole = { number: i };

    try { hole.extractMeta = JSON.parse(await readFile(path.join(holeDir, "extract-meta.json"), "utf8")); } catch {}
    try { hole.tees = JSON.parse(await readFile(path.join(holeDir, "tees.json"), "utf8")); } catch {}
    try { hole.terrainMeta = JSON.parse(await readFile(path.join(holeDir, "terrain-meta.json"), "utf8")); } catch {}
    try {
      const zj = JSON.parse(await readFile(path.join(holeDir, "zones.json"), "utf8"));
      hole.zoneStats = zj.zone_stats || null;
    } catch {}
    try { hole.manifest = JSON.parse(await readFile(path.join(exportDir, "hole-manifest.json"), "utf8")); } catch {}
    try { hole.anchors = JSON.parse(await readFile(path.join(exportDir, "anchors.json"), "utf8")); } catch {}
    try { hole.orientation = JSON.parse(await readFile(path.join(holeDir, "orientation.json"), "utf8")); } catch {
      hole.orientation = { rotation: 0, flipH: false, flipV: false };
    }

    hole.hasIllustration = await fileExists(path.join(holeDir, "illustration.png"));
    hole.hasIllustrationRaw = await fileExists(path.join(holeDir, "illustration_raw.png"));
    hole.hasZonesPng = await fileExists(path.join(holeDir, "zones.png"));

    holes.push(hole);
  }

  return { course: courseJson, holes };
}

async function saveOrientation(courseId, holeNumber, orientation) {
  const pad = String(holeNumber).padStart(2, "0");
  const dir = path.join(root, "output", courseId, "holes", pad);
  await mkdir(dir, { recursive: true });
  await writeFile(path.join(dir, "orientation.json"), JSON.stringify(orientation, null, 2) + "\n", "utf8");
  return { ok: true };
}

function sendJson(res, code, data) {
  res.writeHead(code, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-cache" });
  res.end(JSON.stringify(data));
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://${host}:${port}`);

  // --- API: Load course data ---
  if (req.method === "GET" && url.pathname === "/api/course") {
    try {
      const courseId = url.searchParams.get("id") || "lomond-country-club";
      sendJson(res, 200, await loadCourseData(courseId));
    } catch (err) {
      console.error("Failed to load course data:", err.message);
      sendJson(res, 500, { ok: false, message: err.message });
    }
    return;
  }

  // --- API: Save orientation ---
  if (req.method === "POST" && url.pathname === "/api/orientation") {
    try {
      const body = JSON.parse(await readBody(req));
      const result = await saveOrientation(body.courseId || "lomond-country-club", body.holeNumber, body.orientation);
      sendJson(res, 200, result);
    } catch (err) {
      sendJson(res, 500, { ok: false, message: err.message });
    }
    return;
  }

  // --- API: Save tees ---
  if (req.method === "POST" && url.pathname === "/api/tees") {
    try {
      const body = JSON.parse(await readBody(req));
      const courseId = body.courseId || "lomond-country-club";
      const pad = String(body.holeNumber).padStart(2, "0");
      const teesPath = path.join(root, "output", courseId, "holes", pad, "tees.json");

      const existing = JSON.parse(await readFile(teesPath, "utf8"));
      existing.tees = body.tees;
      await writeFile(teesPath, JSON.stringify(existing, null, 2) + "\n", "utf8");
      sendJson(res, 200, { ok: true });
    } catch (err) {
      sendJson(res, 500, { ok: false, message: err.message });
    }
    return;
  }

  // --- API: Heightmap as grayscale PNG ---
  if (req.method === "GET" && url.pathname === "/api/heightmap") {
    const courseId = url.searchParams.get("course") || "lomond-country-club";
    const hole = Number(url.searchParams.get("hole"));
    const pad = String(hole).padStart(2, "0");
    // Try export dir first, fall back to holes dir
    let rawPath = path.join(root, "output", courseId, "export", `hole-${pad}`, "heightmap.raw");
    if (!await fileExists(rawPath)) {
      rawPath = path.join(root, "output", courseId, "holes", pad, "heightmap.raw");
    }

    try {
      const rawBytes = await readFile(rawPath);
      const res129 = 129;
      const pixels = Buffer.alloc(res129 * res129);
      for (let i = 0; i < res129 * res129; i++) {
        const val = (rawBytes[i * 2] << 8) | rawBytes[i * 2 + 1];
        pixels[i] = Math.round((val / 65535) * 255);
      }

      const sharp = (await import("sharp")).default;
      const pngBuffer = await sharp(pixels, { raw: { width: res129, height: res129, channels: 1 } })
        .resize(512, 512, { kernel: "nearest" })
        .png()
        .toBuffer();

      res.writeHead(200, { "Content-Type": "image/png", "Cache-Control": "no-cache" });
      res.end(pngBuffer);
    } catch (err) {
      res.writeHead(404);
      res.end("Heightmap not found: " + err.message);
    }
    return;
  }

  // --- API: Zone grid data ---
  if (req.method === "GET" && url.pathname === "/api/zones-grid") {
    const courseId = url.searchParams.get("course") || "lomond-country-club";
    const hole = Number(url.searchParams.get("hole"));
    const pad = String(hole).padStart(2, "0");
    const zonesPath = path.join(root, "output", courseId, "holes", pad, "zones.json");

    try {
      const zonesJson = JSON.parse(await readFile(zonesPath, "utf8"));
      sendJson(res, 200, {
        width: zonesJson.source_dimensions.width,
        height: zonesJson.source_dimensions.height,
        grid: zonesJson.grid,
      });
    } catch (err) {
      sendJson(res, 404, { ok: false, message: err.message });
    }
    return;
  }

  // --- API: Save painted zones ---
  if (req.method === "POST" && url.pathname === "/api/zones") {
    try {
      const body = JSON.parse(await readBody(req));
      const courseId = body.courseId || "lomond-country-club";
      const pad = String(body.holeNumber).padStart(2, "0");
      const holeDir = path.join(root, "output", courseId, "holes", pad);
      const zonesPath = path.join(holeDir, "zones.json");

      const existing = JSON.parse(await readFile(zonesPath, "utf8"));
      existing.grid = body.grid;
      existing.source_dimensions = { width: body.width, height: body.height };

      const raw = Buffer.from(body.grid, "base64");
      const totalPixels = body.width * body.height;
      const counts = new Array(11).fill(0);
      for (let i = 0; i < raw.length; i++) {
        if (raw[i] < 11) counts[raw[i]]++;
      }

      const zoneNames = [
        "background", "fairway", "green", "semi_rough", "rough",
        "trees", "bunker", "water", "cart_path", "ob", "tee_box",
      ];
      const newStats = {};
      for (let i = 0; i < 11; i++) {
        newStats[zoneNames[i]] = {
          pixel_count: counts[i],
          percentage: parseFloat((counts[i] / totalPixels * 100).toFixed(1)),
        };
      }
      existing.zone_stats = newStats;

      await writeFile(zonesPath, JSON.stringify(existing, null, 2) + "\n", "utf8");

      const sharp = (await import("sharp")).default;
      const ZONE_COLORS = [
        [0, 0, 0], [0, 204, 0], [128, 255, 64], [102, 136, 51],
        [51, 102, 34], [26, 51, 16], [221, 204, 136], [51, 102, 204],
        [153, 153, 153], [255, 51, 51], [255, 255, 255],
      ];
      const rgbBuf = Buffer.alloc(body.width * body.height * 3);
      for (let i = 0; i < raw.length; i++) {
        const c = ZONE_COLORS[raw[i]] || [0, 0, 0];
        rgbBuf[i * 3] = c[0];
        rgbBuf[i * 3 + 1] = c[1];
        rgbBuf[i * 3 + 2] = c[2];
      }
      await sharp(rgbBuf, { raw: { width: body.width, height: body.height, channels: 3 } })
        .png()
        .toFile(path.join(holeDir, "zones.png"));

      sendJson(res, 200, { ok: true, stats: newStats });
    } catch (err) {
      sendJson(res, 500, { ok: false, message: err.message });
    }
    return;
  }

  // --- API: Regenerate heightmap + export for a hole ---
  if (req.method === "POST" && url.pathname === "/api/regen-heightmap") {
    try {
      const body = JSON.parse(await readBody(req));
      const courseId = body.courseId || "lomond-country-club";
      const hole = Number(body.holeNumber);
      if (!hole || hole < 1 || hole > 18) {
        sendJson(res, 400, { ok: false, message: "Invalid hole number" });
        return;
      }

      console.log(`[regen] Regenerating heightmap for hole ${hole}...`);

      // Step 1: Run generate-terrain.mjs
      const terrainResult = await runScript(
        path.join(root, "scripts", "generate-terrain.mjs"),
        [courseId, String(hole)]
      );
      if (!terrainResult.ok) {
        console.error("[regen] generate-terrain failed:", terrainResult.stderr);
        sendJson(res, 500, {
          ok: false,
          message: "Terrain generation failed",
          stdout: terrainResult.stdout,
          stderr: terrainResult.stderr,
        });
        return;
      }

      // Step 2: Run export-hole.mjs
      const exportResult = await runScript(
        path.join(root, "scripts", "export-hole.mjs"),
        [courseId, String(hole)]
      );
      if (!exportResult.ok) {
        console.error("[regen] export-hole failed:", exportResult.stderr);
        sendJson(res, 500, {
          ok: false,
          message: "Export failed (terrain was regenerated)",
          stdout: exportResult.stdout,
          stderr: exportResult.stderr,
        });
        return;
      }

      console.log(`[regen] Hole ${hole} heightmap regenerated + exported.`);
      sendJson(res, 200, {
        ok: true,
        message: `Heightmap regenerated and exported for hole ${hole}`,
        terrainOutput: terrainResult.stdout.trim(),
        exportOutput: exportResult.stdout.trim(),
      });
    } catch (err) {
      sendJson(res, 500, { ok: false, message: err.message });
    }
    return;
  }

  // --- Static files ---
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
    res.end("Not found: " + url.pathname);
  }
});

server.listen(port, host, () => {
  console.log(`UHole Lite GUI → http://${host}:${port}`);
});
