import { createServer } from "node:http";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { buildAlignmentRecord } from "./lib/lomond-data.mjs";
import { processHole } from "./compute-transform.mjs";
import { exportHole } from "./export-hole.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const host = "127.0.0.1";
const port = Number(process.env.PORT || 4173);

const contentTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".txt": "text/plain; charset=utf-8"
};

let activeIngest = null;

function resolveTarget(urlPath) {
  const normalizedPath = decodeURIComponent(urlPath.split("?")[0]);
  const relative = normalizedPath === "/" ? "/app/index.html" : normalizedPath;
  const candidate = path.normalize(path.join(root, relative));
  if (!candidate.startsWith(root)) {
    return null;
  }
  return candidate;
}

function readRequestBody(req) {
  return new Promise((resolve, reject) => {
    let body = "";
    req.setEncoding("utf8");
    req.on("data", (chunk) => {
      body += chunk;
    });
    req.on("end", () => resolve(body));
    req.on("error", reject);
  });
}

function runIngestScript() {
  return runNodeScript("scripts/ingest-lomond.mjs");
}

function runBasemapScript(force = false) {
  return runNodeScript("scripts/fetch-gsi-basemap.mjs", force ? ["--force"] : []);
}

function runNodeScript(scriptPath, args = []) {
  if (activeIngest) {
    return activeIngest;
  }

  activeIngest = new Promise((resolve) => {
    const child = spawn(process.execPath, [scriptPath, ...args], {
      cwd: root,
      env: process.env,
      stdio: ["ignore", "pipe", "pipe"]
    });

    let stdout = "";
    let stderr = "";

    child.stdout.on("data", (chunk) => {
      stdout += chunk.toString();
    });

    child.stderr.on("data", (chunk) => {
      stderr += chunk.toString();
    });

    child.on("close", (code) => {
      const result = {
        ok: code === 0,
        code,
        stdout,
        stderr
      };
      activeIngest = null;
      resolve(result);
    });
  });

  return activeIngest;
}

async function initializeAlignmentWorkspace() {
  const holesRoot = path.join(root, "output", "lomond-country-club", "holes");
  await mkdir(holesRoot, { recursive: true });

  for (let holeNumber = 1; holeNumber <= 18; holeNumber += 1) {
    const padded = String(holeNumber).padStart(2, "0");
    const holeDir = path.join(holesRoot, padded);
    await mkdir(holeDir, { recursive: true });
    const alignmentPath = path.join(holeDir, "alignment.json");

    try {
      await stat(alignmentPath);
    } catch {
      const record = buildAlignmentRecord(holeNumber);
      await writeFile(alignmentPath, JSON.stringify(record, null, 2) + "\n", "utf8");
    }
  }

  return {
    ok: true,
    message: "Alignment workspace initialized for 18 holes."
  };
}

async function saveAlignment(bodyText) {
  const payload = JSON.parse(bodyText || "{}");
  const holeNumber = Number(payload.holeNumber);
  if (!Number.isInteger(holeNumber) || holeNumber < 1 || holeNumber > 18) {
    return {
      ok: false,
      message: "Invalid hole number."
    };
  }

  const padded = String(holeNumber).padStart(2, "0");
  const alignmentPath = path.join(root, "output", "lomond-country-club", "holes", padded, "alignment.json");
  const alignment = JSON.parse(await readFile(alignmentPath, "utf8"));

  alignment.control_points = Array.isArray(payload.controlPoints) ? payload.controlPoints : alignment.control_points;
  alignment.anchors = Array.isArray(payload.anchors) ? payload.anchors : alignment.anchors || [];
  alignment.selected_photo_tile = payload.selectedPhotoTile || alignment.selected_photo_tile || null;

  // Persist visual alignment (stacked overlay transform)
  if (payload.visualAlignment) {
    alignment.visual_alignment = payload.visualAlignment;
  }

  if (alignment.control_points.length >= 4) {
    alignment.status = "ready_for_transform";
  } else if (alignment.control_points.length > 0) {
    alignment.status = "control_points_started";
  } else {
    alignment.status = alignment.target_base_map?.path ? "ready_for_control_points" : "needs_base_map";
  }

  alignment.transform = alignment.transform || {};
  alignment.transform.pixel_to_world_ready = Boolean(payload.transformReady);
  alignment.transform.residual_error_m = payload.residualErrorM ?? alignment.transform.residual_error_m ?? null;

  await writeFile(alignmentPath, JSON.stringify(alignment, null, 2) + "\n", "utf8");

  return {
    ok: true,
    message: `Saved alignment for hole ${holeNumber}.`
  };
}

const server = createServer(async (req, res) => {
  if (req.method === "POST" && req.url === "/api/ingest/lomond") {
    const body = await readRequestBody(req);
    const payload = body ? JSON.parse(body) : {};
    const result = await runNodeScript("scripts/ingest-lomond.mjs", payload.force ? ["--force"] : []);
    res.writeHead(result.ok ? 200 : 500, {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-cache"
    });
    res.end(JSON.stringify(result));
    return;
  }

  if (req.method === "POST" && req.url === "/api/alignment/init") {
    await readRequestBody(req);
    const result = await initializeAlignmentWorkspace();
    res.writeHead(200, {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-cache"
    });
    res.end(JSON.stringify(result));
    return;
  }

  if (req.method === "POST" && req.url === "/api/basemap/fetch") {
    const body = await readRequestBody(req);
    const payload = body ? JSON.parse(body) : {};
    const result = await runBasemapScript(Boolean(payload.force));
    res.writeHead(result.ok ? 200 : 500, {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-cache"
    });
    res.end(JSON.stringify(result));
    return;
  }

  if (req.method === "POST" && req.url === "/api/alignment/save") {
    const body = await readRequestBody(req);
    const result = await saveAlignment(body);
    res.writeHead(result.ok ? 200 : 500, {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-cache"
    });
    res.end(JSON.stringify(result));
    return;
  }

  if (req.method === "POST" && req.url === "/api/alignment/compute-transform") {
    const body = await readRequestBody(req);
    const payload = body ? JSON.parse(body) : {};
    const holeNumber = Number(payload.holeNumber);
    if (!Number.isInteger(holeNumber) || holeNumber < 1 || holeNumber > 18) {
      res.writeHead(400, { "Content-Type": "application/json; charset=utf-8" });
      res.end(JSON.stringify({ ok: false, message: "Invalid hole number." }));
      return;
    }
    const result = await processHole(holeNumber);
    res.writeHead(result.ok ? 200 : 400, {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-cache"
    });
    res.end(JSON.stringify(result));
    return;
  }

  if (req.method === "POST" && req.url === "/api/export/hole") {
    const body = await readRequestBody(req);
    const payload = body ? JSON.parse(body) : {};
    const holeNumber = Number(payload.holeNumber);
    if (!Number.isInteger(holeNumber) || holeNumber < 1 || holeNumber > 18) {
      res.writeHead(400, { "Content-Type": "application/json; charset=utf-8" });
      res.end(JSON.stringify({ ok: false, message: "Invalid hole number." }));
      return;
    }
    const result = await exportHole(holeNumber);
    res.writeHead(result.ok ? 200 : 400, {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-cache"
    });
    res.end(JSON.stringify(result));
    return;
  }

  const target = resolveTarget(req.url || "/");
  if (!target) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }

  try {
    const targetStat = await stat(target);
    const finalPath = targetStat.isDirectory() ? path.join(target, "index.html") : target;
    const body = await readFile(finalPath);
    const ext = path.extname(finalPath).toLowerCase();
    res.writeHead(200, {
      "Content-Type": contentTypes[ext] || "application/octet-stream",
      "Cache-Control": "no-cache"
    });
    res.end(body);
  } catch {
    res.writeHead(404);
    res.end("Not found");
  }
});

server.listen(port, host, () => {
  console.log(`Course Intake app running at http://${host}:${port}`);
});
