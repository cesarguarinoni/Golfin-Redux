/**
 * extract-panels.mjs
 * Renders each hole's green-panel crop from the Lomond booklet PDF,
 * downscales to ~680 px long edge, JPEG-encodes via sharp, and writes
 * data/panels.json in the shape expected by /api/panel.
 *
 * Page mapping: hole N = PDF page N+1
 * (page 1 = course overview; pages 2–19 = holes 1–18; page 20 = back cover)
 *
 * Rendering strategy: PyMuPDF (fitz) rasterises the PDF, because pdfjs-dist v4
 * needs a browser-side canvas that is not compatible with node-canvas on this
 * booklet's CMap path. sharp handles resize + JPEG.
 *
 * Robustness: this script FAILS LOUD (non-zero exit, clear remediation message)
 * if python / PyMuPDF is missing or any page fails to render — it never writes a
 * partial or empty panels.json. The "Launch GUI" launchers bootstrap PyMuPDF
 * automatically. Rendering is invoked via execFileSync (no shell), so it works
 * on macOS and Windows alike and is safe with the non-ASCII PDF filename.
 *
 * Usage:
 *   node scripts/extract-panels.mjs            # skips if data/panels.json exists
 *   node scripts/extract-panels.mjs --force    # always re-extract
 */

import { execFileSync } from "node:child_process";
import { readFile, writeFile, mkdir, access, unlink } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");           // Tools/GreenSlope
const repoRoot = path.resolve(root, "..", "..");       // GolfinRedux

const PDF_PATH = path.join(
  repoRoot,
  "Docs/Specs/Queued/green_topology_and_pin_authoring/A4_ホール攻略冊子.pdf"
);
const OUT_JSON = path.join(root, "data", "panels.json");
const DATA_DIR = path.join(root, "data");
const TMP_PNG = path.join(DATA_DIR, "_tmp_panel.png");
const TMP_PY  = path.join(os.tmpdir(), `greenslope_render_${process.pid}.py`);

const FORCE = process.argv.includes("--force");
const TARGET_LONG_EDGE = 680;

// ---------------------------------------------------------------------------
// Crop box — PDF point coordinates (72 dpi), applied to each page.
//
// The booklet pages are A4 landscape (1190.55 × 841.89 pt).
// Each page has a "GREEN攻略法" panel in the lower-right area. We capture the
// ENTIRE bordered diagram box (dark-green frame at x: 658–886, y: 590–818 PDF
// pt) — green shape + apron + slope arrows + dashed ridge + the 29/43 rulers +
// bunkers. The frame is in the same spot on all 18 pages, so capturing the full
// box guarantees every hole's green fits inside with margin (no clipping).
// History: x:748 then x:696 both still clipped the green's left lobe; the box
// crop fixes it for good.
//
// Per-hole offsets:  add { x, y, w, h } deltas here if a hole drifts.
// ---------------------------------------------------------------------------
const BASE_CROP_PT = { x: 658, y: 590, w: 228, h: 228 };

const HOLE_CROP_OVERRIDES = {
  // e.g.  3: { y: -5 }  — shift hole 3 up by 5 pt
};

function cropForHole(hole) {
  const ov = HOLE_CROP_OVERRIDES[hole] || {};
  return {
    x: BASE_CROP_PT.x + (ov.x || 0),
    y: BASE_CROP_PT.y + (ov.y || 0),
    w: BASE_CROP_PT.w + (ov.w || 0),
    h: BASE_CROP_PT.h + (ov.h || 0),
  };
}

async function exists(p) {
  try { await access(p); return true; } catch { return false; }
}

async function cleanup() {
  if (await exists(TMP_PNG)) await unlink(TMP_PNG).catch(() => {});
  if (await exists(TMP_PY))  await unlink(TMP_PY).catch(() => {});
}

// Resolve a working Python 3 interpreter. Tries python3 then python.
// Throws a clear, actionable error if neither exists.
function resolvePython() {
  for (const cmd of ["python3", "python"]) {
    try { execFileSync(cmd, ["--version"], { stdio: "pipe" }); return cmd; }
    catch { /* try next */ }
  }
  throw new Error(
    "No python3/python interpreter found on PATH.\n" +
    "Install Python 3 from https://www.python.org/downloads/ and retry."
  );
}

// Verify PyMuPDF (fitz) is importable. Fail loud with the exact install command.
function assertFitz(py) {
  try { execFileSync(py, ["-c", "import fitz"], { stdio: "pipe" }); }
  catch {
    throw new Error(
      `PyMuPDF (fitz) is not installed for "${py}".\n` +
      `Install it with:\n    ${py} -m pip install PyMuPDF\n` +
      `(The "Launch GUI" launcher installs this automatically.)`
    );
  }
}

// Render helper written once to a temp .py file — invoked via execFileSync with
// argv (no shell), so there are zero cross-platform quoting issues.
// argv: pdfPath pageIdx x y w h dpi outPng
const RENDER_PY = `
import fitz, sys
pdf, page = sys.argv[1], int(sys.argv[2])
x, y, w, h, dpi = (float(sys.argv[i]) for i in range(3, 8))
out = sys.argv[8]
doc = fitz.open(pdf)
pg = doc[page]
mat = fitz.Matrix(dpi / 72.0, dpi / 72.0)
clip = fitz.Rect(x, y, x + w, y + h)
pix = pg.get_pixmap(matrix=mat, clip=clip)
pix.save(out)
`.trimStart();

function renderPageRegion(py, pageIdx, crop, dpi = 300) {
  const { x, y, w, h } = crop;
  execFileSync(py, [
    TMP_PY, PDF_PATH, String(pageIdx),
    String(x), String(y), String(w), String(h), String(dpi), TMP_PNG,
  ], { stdio: ["pipe", "pipe", "pipe"] });
}

async function main() {
  if (!FORCE && await exists(OUT_JSON)) {
    console.log("data/panels.json already exists — skipping. Use --force to re-extract.");
    return;
  }
  if (!(await exists(PDF_PATH))) {
    throw new Error(`Source PDF not found:\n    ${PDF_PATH}`);
  }

  const PYTHON = resolvePython();
  assertFitz(PYTHON);

  await mkdir(DATA_DIR, { recursive: true });
  await writeFile(TMP_PY, RENDER_PY);

  console.log(`Extracting green panels from: ${path.relative(process.cwd(), PDF_PATH)}`);
  console.log(`Renderer: ${PYTHON} + PyMuPDF | Crop: x=${BASE_CROP_PT.x} y=${BASE_CROP_PT.y} w=${BASE_CROP_PT.w} h=${BASE_CROP_PT.h} pt`);

  const panels = {};

  for (let hole = 1; hole <= 18; hole++) {
    const pageIdx = hole; // page index (0-based): hole N = page index N (= page number N+1)
    const crop = cropForHole(hole);

    process.stdout.write(`  H${String(hole).padStart(2)} (page ${pageIdx + 1}) … `);

    try {
      renderPageRegion(PYTHON, pageIdx, crop);
    } catch (err) {
      // Fail LOUD — never write a partial panels.json.
      await cleanup();
      throw new Error(`Failed to render hole ${hole} (page ${pageIdx + 1}): ${err.message}`);
    }

    // Resize to ~680 px on the long edge, JPEG-encode
    const pngBuf = await readFile(TMP_PNG);
    const meta = await sharp(pngBuf).metadata();
    const longEdge = Math.max(meta.width, meta.height);
    const resizeFactor = TARGET_LONG_EDGE / longEdge;
    const outW = Math.round(meta.width * resizeFactor);
    const outH = Math.round(meta.height * resizeFactor);

    const jpegBuf = await sharp(pngBuf)
      .resize(outW, outH, { fit: "fill" })
      .jpeg({ quality: 87 })
      .toBuffer();

    const b64 = jpegBuf.toString("base64");
    panels[String(hole)] = { panel: { w: outW, h: outH, b64 } };

    console.log(`ok (${outW}×${outH}, ${(b64.length / 1024).toFixed(0)} KB b64)`);
  }

  const count = Object.keys(panels).length;
  if (count !== 18) {
    await cleanup();
    throw new Error(`Expected 18 panels, produced ${count} — aborting without writing panels.json.`);
  }

  await writeFile(OUT_JSON, JSON.stringify(panels, null, 2));
  await cleanup();
  console.log(`\nWrote data/panels.json (${count} holes)`);
}

main().catch((err) => {
  console.error("\n[extract-panels] " + err.message);
  process.exit(1);
});
