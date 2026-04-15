import { createServer } from "node:http";
import { readFile, stat, writeFile, mkdir } from "node:fs/promises";
import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const host = "127.0.0.1";
const port = Number(process.env.PORT || 4175);

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
  let courseJson;
  try {
    courseJson = JSON.parse(await readFile(path.join(courseDir, "course.json"), "utf8"));
  } catch {
    // Try to copy from UHoleLite if available
    const liteCourse = path.join(root, "..", "UHoleLite", "output", courseId, "course.json");
    if (await fileExists(liteCourse)) {
      const data = await readFile(liteCourse, "utf8");
      await mkdir(courseDir, { recursive: true });
      await writeFile(path.join(courseDir, "course.json"), data, "utf8");
      courseJson = JSON.parse(data);
    } else {
      throw new Error("course.json not found");
    }
  }

  const holes = [];
  for (let i = 1; i <= 18; i++) {
    const pad = String(i).padStart(2, "0");
    const holeDir = path.join(courseDir, "holes", pad);
    const exportDir = path.join(courseDir, "export", `hole-${pad}`);
    const hole = { number: i };

    try { hole.holeBounds = JSON.parse(await readFile(path.join(holeDir, "hole-bounds.json"), "utf8")); } catch {}
    try { hole.tees = JSON.parse(await readFile(path.join(holeDir, "tees.json"), "utf8")); } catch {}
    try { hole.terrainMeta = JSON.parse(await readFile(path.join(holeDir, "terrain-meta.json"), "utf8")); } catch {}
    try {
      const zj = JSON.parse(await readFile(path.join(holeDir, "zones.json"), "utf8"));
      hole.zoneStats = zj.zone_stats || null;
    } catch {}
    try { hole.manifest = JSON.parse(await readFile(path.join(exportDir, "hole-manifest.json"), "utf8")); } catch {}
    try { hole.anchors = JSON.parse(await readFile(path.join(exportDir, "anchors.json"), "utf8")); } catch {}

    hole.hasSatellite = await fileExists(path.join(holeDir, "satellite.png"));
    hole.hasZonesPng = await fileExists(path.join(holeDir, "zones.png"));
    hole.hasZonesPainted = await fileExists(path.join(holeDir, "zones-painted.png"));
    hole.hasHoleBounds = await fileExists(path.join(holeDir, "hole-bounds.json"));

    holes.push(hole);
  }

  // Load config
  let config = null;
  try {
    config = JSON.parse(await readFile(path.join(root, "config", `${courseId}.json`), "utf8"));
  } catch {}

  return { course: courseJson, holes, config };
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

  // --- API: Get/save hole bounds ---
  if (url.pathname === "/api/hole-bounds") {
    const courseId = url.searchParams.get("course") || "lomond-country-club";
    const hole = Number(url.searchParams.get("hole"));
    const pad = String(hole).padStart(2, "0");
    const holeDir = path.join(root, "output", courseId, "holes", pad);
    const boundsPath = path.join(holeDir, "hole-bounds.json");

    if (req.method === "GET") {
      try {
        const data = await readFile(boundsPath, "utf8");
        sendJson(res, 200, JSON.parse(data));
      } catch {
        sendJson(res, 404, { ok: false, message: "hole-bounds.json not found" });
      }
      return;
    }

    if (req.method === "POST") {
      try {
        const body = JSON.parse(await readBody(req));
        await mkdir(holeDir, { recursive: true });
        await writeFile(boundsPath, JSON.stringify(body, null, 2) + "\n", "utf8");
        sendJson(res, 200, { ok: true });
      } catch (err) {
        sendJson(res, 500, { ok: false, message: err.message });
      }
      return;
    }
  }

  // --- API: Fetch satellite tiles ---
  if (req.method === "POST" && url.pathname === "/api/fetch-satellite") {
    try {
      const body = JSON.parse(await readBody(req));
      const courseId = body.courseId || "lomond-country-club";
      const hole = Number(body.holeNumber);

      console.log(`[fetch] Fetching satellite tiles for hole ${hole}...`);
      const result = await runScript(
        path.join(root, "scripts", "fetch-satellite.mjs"),
        [courseId, String(hole)]
      );

      // Always print script output so we can see crops/tile-range in the terminal
      if (result.stdout) console.log(result.stdout);
      if (result.stderr) console.error(result.stderr);

      if (result.ok) {
        console.log(`[fetch] Satellite tiles fetched for hole ${hole}.`);
        sendJson(res, 200, {
          ok: true,
          message: `Satellite tiles fetched for hole ${hole}`,
          output: result.stdout.trim(),
        });
      } else {
        sendJson(res, 500, {
          ok: false,
          message: "Fetch failed: " + (result.stderr || "").split('\n').pop().slice(0, 200),
          stdout: result.stdout,
          stderr: result.stderr,
        });
      }
    } catch (err) {
      sendJson(res, 500, { ok: false, message: err.message });
    }
    return;
  }

  // --- API: Satellite image ---
  if (req.method === "GET" && url.pathname === "/api/satellite") {
    const courseId = url.searchParams.get("course") || "lomond-country-club";
    const hole = Number(url.searchParams.get("hole"));
    const pad = String(hole).padStart(2, "0");
    const imgPath = path.join(root, "output", courseId, "holes", pad, "satellite.png");

    try {
      const data = await readFile(imgPath);
      res.writeHead(200, { "Content-Type": "image/png", "Cache-Control": "no-cache" });
      res.end(data);
    } catch {
      res.writeHead(404);
      res.end("Satellite image not found");
    }
    return;
  }

  // --- API: Heightmap as grayscale PNG ---
  if (req.method === "GET" && url.pathname === "/api/heightmap") {
    const courseId = url.searchParams.get("course") || "lomond-country-club";
    const hole = Number(url.searchParams.get("hole"));
    const pad = String(hole).padStart(2, "0");
    let rawPath = path.join(root, "output", courseId, "export", `hole-${pad}`, "heightmap.raw");
    if (!await fileExists(rawPath)) {
      rawPath = path.join(root, "output", courseId, "holes", pad, "heightmap.raw");
    }

    try {
      const rawBytes = await readFile(rawPath);
      const pixelCount = rawBytes.length / 2;
      const rawRes = Math.round(Math.sqrt(pixelCount));
      // The raw file is stored south-first (Unity terrain convention). For the
      // GUI viewer we want north-up to match the satellite/zones overlay, so
      // we read rows in reverse.
      const pixels = Buffer.alloc(rawRes * rawRes);
      for (let y = 0; y < rawRes; y++) {
        const srcY = rawRes - 1 - y;
        for (let x = 0; x < rawRes; x++) {
          const srcIdx = (srcY * rawRes + x) * 2;
          const val = (rawBytes[srcIdx] << 8) | rawBytes[srcIdx + 1];
          pixels[y * rawRes + x] = Math.round((val / 65535) * 255);
        }
      }

      const sharp = (await import("sharp")).default;
      const pngBuffer = await sharp(pixels, { raw: { width: rawRes, height: rawRes, channels: 1 } })
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
      const result = {
        width: zonesJson.source_dimensions.width,
        height: zonesJson.source_dimensions.height,
        grid: zonesJson.grid,
      };
      if (zonesJson.terrain_grid) result.terrain_grid = zonesJson.terrain_grid;
      if (zonesJson.trees_mask) result.trees_mask = zonesJson.trees_mask;
      if (zonesJson.ob_mask) result.ob_mask = zonesJson.ob_mask;
      if (zonesJson.cart_path_mask) result.cart_path_mask = zonesJson.cart_path_mask;
      sendJson(res, 200, result);
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

      await mkdir(holeDir, { recursive: true });

      let existing = {};
      try { existing = JSON.parse(await readFile(zonesPath, "utf8")); } catch {}

      existing.grid = body.grid;
      existing.source_dimensions = { width: body.width, height: body.height };
      existing.hole_number = body.holeNumber;

      if (body.terrain_grid) existing.terrain_grid = body.terrain_grid;
      if (body.trees_mask) existing.trees_mask = body.trees_mask;
      if (body.ob_mask) existing.ob_mask = body.ob_mask;
      if (body.cart_path_mask) existing.cart_path_mask = body.cart_path_mask;

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

      // Add zone_index if missing
      if (!existing.zone_index) {
        existing.zone_index = {};
        for (let i = 0; i < zoneNames.length; i++) existing.zone_index[String(i)] = zoneNames[i];
      }
      existing.grid_encoding = "base64_uint8";

      await writeFile(zonesPath, JSON.stringify(existing, null, 2) + "\n", "utf8");

      // Write zones.png visualization
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

  // --- API: Save tees ---
  if (req.method === "POST" && url.pathname === "/api/tees") {
    try {
      const body = JSON.parse(await readBody(req));
      const courseId = body.courseId || "lomond-country-club";
      const pad = String(body.holeNumber).padStart(2, "0");
      const holeDir = path.join(root, "output", courseId, "holes", pad);
      const teesPath = path.join(holeDir, "tees.json");

      await mkdir(holeDir, { recursive: true });

      let existing = {};
      try { existing = JSON.parse(await readFile(teesPath, "utf8")); } catch {}
      existing.tees = body.tees;
      await writeFile(teesPath, JSON.stringify(existing, null, 2) + "\n", "utf8");
      sendJson(res, 200, { ok: true });
    } catch (err) {
      sendJson(res, 500, { ok: false, message: err.message });
    }
    return;
  }

  // --- API: Regenerate heightmap + export ---
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

  // --- API: Initialize empty zone grid for a hole ---
  if (req.method === "POST" && url.pathname === "/api/init-zones") {
    try {
      const body = JSON.parse(await readBody(req));
      const courseId = body.courseId || "lomond-country-club";
      const hole = Number(body.holeNumber);
      const width = body.width;
      const height = body.height;
      const pad = String(hole).padStart(2, "0");
      const holeDir = path.join(root, "output", courseId, "holes", pad);
      const zonesPath = path.join(holeDir, "zones.json");

      await mkdir(holeDir, { recursive: true });

      // Create an all-rough grid
      const gridSize = width * height;
      let gridBin = "";
      for (let i = 0; i < gridSize; i++) gridBin += String.fromCharCode(4); // rough
      const gridB64 = Buffer.from(gridBin, "binary").toString("base64");

      const emptyMask = Buffer.alloc(gridSize).toString("base64");

      const zoneNames = [
        "background", "fairway", "green", "semi_rough", "rough",
        "trees", "bunker", "water", "cart_path", "ob", "tee_box",
      ];
      const zoneIndex = {};
      for (let i = 0; i < zoneNames.length; i++) zoneIndex[String(i)] = zoneNames[i];

      const zonesData = {
        hole_number: hole,
        source_dimensions: { width, height },
        zone_index: zoneIndex,
        zone_stats: { rough: { pixel_count: gridSize, percentage: 100.0 } },
        grid_encoding: "base64_uint8",
        grid: gridB64,
        terrain_grid: gridB64,
        trees_mask: emptyMask,
        ob_mask: emptyMask,
        cart_path_mask: emptyMask,
      };

      await writeFile(zonesPath, JSON.stringify(zonesData, null, 2) + "\n", "utf8");
      sendJson(res, 200, { ok: true, width, height });
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
  console.log(`UHole Geo GUI → http://${host}:${port}`);
});
