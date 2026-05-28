# SPEC — green_slope_authoring_tool

**Authored:** 2026-05-28 12:56 CEST (Architect)
**Tier:** TELLCODE / Code-task (standalone Node + browser tool; no Unity, no Figma, no review-chain fidelity gate — Cesar is the visual gate by running it).
**Status:** SPEC_READY
**Kickoff for Code:** `Use the golfin-implementer subagent on "green_slope_authoring_tool"`

---

## Why this exists

`lomond_greens_authoring_batch` was Cesar-rejected twice on 2026-05-28 (see that folder's `CESAR_REJECTION.md`). Both rejections trace to one root cause: **Code cannot reliably read the PDF's discrete slope arrows.** Rejection #2 was literally "the overlay arrows don't match the PDF — wrong count, wrong directions, several outside the polygon."

This tool puts the **human in the arrow-reading loop.** Cesar opens the real PDF green panel as a backdrop over the real green polygon, drops discrete arrows that match what he sees 1:1, draws the ridge, and exports. The exported JSON then feeds Code's dense-grid bake. The auto-PDF-read step that keeps failing is removed entirely.

It mirrors the existing `Tools/UHoleGeo/` browser-tool pattern (standalone Node dev-server + `app/` static UI), on **port 4178**.

---

## What already exists on disk (do NOT rewrite)

`Tools/GreenSlope/scripts/dev-server.mjs` is already written and is the contract. Build the rest to match it exactly. Its three API routes:

1. `GET /api/contour?hole=N` → reads `Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/greens.json`, returns:
   ```json
   { "hole": N, "contour": [[x,z],...], "heightM": 0.15, "center": <center_local>, "size": <size_m> }
   ```
   `contour` is `greens[0].contour` mapped to world-XZ `[x,z]` pairs (the real green polygon).
2. `GET /api/panel?hole=N` → reads `Tools/GreenSlope/data/panels.json` and returns `panels[hole].panel`. **So `data/panels.json` MUST be shaped `{ "<hole>": { "panel": { "w":…, "h":…, "b64":… } } }`** — each hole entry wraps the image in a `.panel` object. Getting this shape wrong breaks the panel route.
3. `POST /api/save` → writes `output/hole_NN_slope_authoring.json`, and reads `payload.hole`, `payload.regionCount`, `payload.ridgePresent` for its log line. The export payload MUST include those three fields.

Directory tree (`app/`, `data/`, `output/`, `scripts/`) exists; `app/` and `data/` are empty.

**Do NOT modify `dev-server.mjs`'s existing routes.** If a new endpoint is genuinely needed, add it; don't change the three above.

---

## Deliverables (4)

### 1. `Tools/GreenSlope/package.json`
```json
{
  "name": "green-slope",
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "gui": "node scripts/dev-server.mjs",
    "extract": "node scripts/extract-panels.mjs"
  },
  "dependencies": {
    "pdfjs-dist": "^4.0.0",
    "sharp": "^0.33.0"
  }
}
```

### 2. `Tools/GreenSlope/scripts/extract-panels.mjs`
- Source PDF: `Docs/Specs/Queued/green_topology_and_pin_authoring/A4_ホール攻略冊子.pdf` (**page N+1 = hole N**).
- For each hole 1–18: render the page at ≥300 DPI (`pdfjs-dist`), crop to the green-detail panel region of the page, downscale to ~680 px on the long edge, JPEG-encode (`sharp`), base64.
- Write `data/panels.json` shaped exactly: `{ "1": { "panel": { "w":W, "h":H, "b64":"…" } }, "2": {…}, … }` (string keys, `.panel` wrapper — matches `/api/panel`).
- Idempotent: skip if `data/panels.json` already exists unless `--force`. `dev-server` should also trigger this on launch if the file is missing (small guard in the launcher or a check in the server is fine — keep server route changes minimal; preferred is the launcher runs `npm run extract` first if `data/panels.json` is absent).
- The green-panel crop box per page may need a small per-hole offset table; pilot H07, tune the crop, then generalize. If the booklet's panel position is uniform, one crop box covers all 18.

### 3. `Tools/GreenSlope/app/index.html`
Self-contained UI (inline CSS/JS is fine; three.js r128 via CDN). **Utilitarian survey-instrument aesthetic** — function over polish, high-contrast, readable readouts.

Core surface:
- **Hole selector** (1–18). Loads `/api/contour?hole=N` (draw the green polygon, filled, as the authoring canvas) + `/api/panel?hole=N` (PDF crop as a draggable backdrop).
- **PDF backdrop alignment:** opacity slider + scale + rotate + **drag-to-move**. **Drive the backdrop transform with CSS `transform: translate(px,px) rotate(deg) scale(s)` — NOT `left/top`.** (Left/top drift was a known failure last session.) Cesar visually aligns the PDF panel onto the polygon; that alignment IS the panel↔world transform.
- **Arrow authoring (the whole point):** Cesar drops **discrete** arrows by dragging base→tip (tip = downhill). One arrow per printed PDF arrow, placed on top of it. **No auto dense-grid sampling in the tool** — discrete, human-placed, 1:1 with the PDF. Erase individual arrows.
- **Ridge tool:** draw a dashed polyline for the tier divide. Erase.
- **Region readout:** live region-count + a 2-tier indicator (derived from ridge presence / arrow clustering) so Cesar can confirm `regionCount==2` on the known 2-tier holes (3/7/11/18).
- **FLAT / 3D toggle:** FLAT = orthographic top-down authoring view. 3D = three.js r128 orbit camera showing relief **synthesized from the authored arrows** (not from real heightmap). **On return to FLAT, hide the WebGL canvas (`#gl`)** — leaving it visible over the 2D canvas was a known failure last session.
- **Export → `POST /api/save`.** Schema below.

### 4. Launchers (mirror `Tools/UHoleGeo/` conventions)
- `Tools/GreenSlope/Launch GUI.command` (macOS, **chmod +x**): `cd` to tool dir, `npm install` if `node_modules` missing, run `npm run extract` if `data/panels.json` missing, `npm run gui`, open `http://127.0.0.1:4178`. (UHoleGeo has no `.command` — create fresh.)
- `Tools/GreenSlope/Launch GUI.bat` (Windows): mirror `Tools/UHoleGeo/Launch GUI.bat` verbatim, swapping the script/port to GreenSlope / 4178.

---

## Export schema (`POST /api/save` payload → `output/hole_NN_slope_authoring.json`)

```jsonc
{
  "hole": 7,
  "regionCount": 2,
  "ridgePresent": true,
  "editorBackdrop": {                       // lets Code reconstruct panel↔world transform T
    "panelW": 680, "panelH": 540,
    "transform": { "tx": …, "ty": …, "rotDeg": …, "scale": … },  // CSS transform applied to backdrop
    "correspondence": [                      // optional: 3–4 panel-px ↔ world-XZ pairs if Cesar marks them
      { "panelPx": [x,y], "worldXZ": [x,z] }
    ]
  },
  "arrows": [                                // discrete, human-placed; world-XZ
    { "baseXZ": [x,z], "tipXZ": [x,z], "region": 0 }
  ],
  "ridge": [ [x,z], [x,z], … ],              // polyline, world-XZ; [] if ridgePresent=false
  "regions": [ { "id": 0, "label": "upper" }, { "id": 1, "label": "lower" } ]
}
```
`editorBackdrop` mirrors Phase 2 Q3's `editorBackdrop` mechanism — runtime `green.json` ignores unknown fields, so Code can carry these correspondence points into `green.json` for later re-authoring. **The tool authors slope intent; it does NOT bake the dense grid** — Code's bake step consumes this JSON and expands to the 0.5 m grid.

---

## Scope & sequence

- **Pilot H07 first** (the hole with real tier structure — proves arrow placement, ridge, 2-region readout, and the FLAT/3D toggle on a hard case). Get Cesar's eyeball approval on H07 before generalizing.
- Then the hole selector covers all 18.
- Genuinely flat holes (e.g. H05) author as a single region; still verified by Cesar in-tool.

## Hard rules

1. Do NOT modify the three existing `dev-server.mjs` routes (extend only if unavoidable).
2. Do NOT modify any `greens.json` or any Unity asset — this tool only reads `greens.json` and writes to `Tools/GreenSlope/output/`.
3. Do NOT bake the dense grid in the tool. Output is human authoring intent; Code's separate bake step consumes it.
4. Backdrop transform via CSS `transform`, never `left/top`. Hide `#gl` on return to FLAT.
5. Arrows are discrete and human-placed (1:1 with the PDF), never an auto dense-grid.
6. `data/panels.json` shape is `{ "<hole>": { "panel": {w,h,b64} } }` — must match `/api/panel`.

## Definition of done

- `npm run extract` produces `data/panels.json` with all 18 holes in the correct shape; `/api/panel?hole=7` returns a valid image.
- `Launch GUI.command` (Mac) brings up the tool at `:4178` with H07's polygon + PDF backdrop loaded.
- Cesar can: align the backdrop (drag/scale/rotate via CSS transform), drop discrete arrows matching the PDF, draw the ridge, toggle FLAT↔3D cleanly (no leftover `#gl`), and Export.
- Export writes `output/hole_07_slope_authoring.json` with the schema above; server log shows `regions` + `ridge`.
- Cesar eyeball-approves the H07 authoring against the PDF panel, then the selector is confirmed working for the other 17.
