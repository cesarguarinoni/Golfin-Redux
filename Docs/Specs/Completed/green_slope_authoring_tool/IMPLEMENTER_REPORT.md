# IMPLEMENTER REPORT — green_slope_authoring_tool

**Iteration:** 1
**Date:** 2026-05-28
**Implementer:** golfin-implementer (claude-sonnet-4-6)
**Tier:** TELLCODE (no Unity, no Figma, no review-chain)

---

## Summary

All 4 deliverables built and smoke-tested. The tool is ready for Cesar's in-browser eyeball gate.

---

## Files modified or created

| Path | Purpose |
|---|---|
| `Tools/GreenSlope/package.json` | Exact SPEC § Deliverable 1 JSON (name green-slope, type module, scripts gui+extract, deps pdfjs-dist ^4 + sharp ^0.33) |
| `Tools/GreenSlope/scripts/extract-panels.mjs` | Renders PDF via PyMuPDF (python3 fitz), crops green-panel region, resizes to 680 px long edge, JPEG via sharp, writes data/panels.json in `{"<hole>": {"panel": {w,h,b64}}}` shape |
| `Tools/GreenSlope/app/index.html` | Self-contained UI (Three.js r128 CDN); hole selector 1–18; draggable PDF backdrop (CSS transform, not left/top); arrow tool (drag base→tip); ridge polyline tool; FLAT/3D toggle (#gl hidden on FLAT); Export → POST /api/save |
| `Tools/GreenSlope/Launch GUI.command` | macOS launcher: npm install if needed, npm run extract if panels.json missing, npm run gui, opens http://127.0.0.1:4178. chmod +x applied. |
| `Tools/GreenSlope/Launch GUI.bat` | Windows launcher mirroring Tools/UHoleGeo/Launch GUI.bat, port=4178 |
| `Tools/GreenSlope/.gitignore` | Excludes node_modules/, output/, data/panels.json (regenerable), mirrors UHoleGeo conventions |
| `Docs/Specs/Active/green_slope_authoring_tool/screenshots/h07_panel_crop.jpg` | Tuned H07 panel crop (367×680 px JPEG) — reference for Cesar's eyeball gate |

**Not committed:** all of the above are working-tree additions. Commit deferred to Cesar/orchestrator per instructions.

---

## Verification evidence

### 1. npm run extract — panels.json

```
Extracting green panels from: ../../Docs/Specs/Queued/green_topology_and_pin_authoring/A4_ホール攻略冊子.pdf
Crop box: x=748, y=590, w=132, h=245 pt
  H 1 (page 2) … ok (367×680, 48 KB b64)
  H 2 (page 3) … ok (367×680, 55 KB b64)
  H 3 (page 4) … ok (367×680, 64 KB b64)
  ...
  H 7 (page 8) … ok (367×680, 67 KB b64)
  ...
  H18 (page 19) … ok (367×680, 57 KB b64)
Wrote data/panels.json (18 holes)
```

Shape verified: `{ "7": { "panel": { "w": 367, "h": 680, "b64": "..." } } }` — string keys, `.panel` wrapper, matches `/api/panel`.

Idempotency: second run printed "data/panels.json already exists — skipping." ✓

### 2. H07 panel crop quality

`screenshots/h07_panel_crop.jpg` (367×680): green shape fully visible, both tiers separated by dashed white ridge line, all slope arrows (5 black arrows on upper tier, 1 on lower), distance rulers (29 m, 43 m), bunkers at bottom. Minimal page chrome — thin brown footer strip only. Consistent framing across H03, H11, H18 (2-tier holes) verified during tuning.

### 3. Smoke test — curl responses

```
GET /api/contour?hole=7  →  { hole:7, contour:[32 pts], center:{x:177.66,z:-30.73}, size:{x:27.42,z:31.78}, heightM:0.15 }  ✓
GET /api/panel?hole=7    →  { w:367, h:680, b64:"<69016 chars>" }  ✓
GET /                    →  <!DOCTYPE html>…  (index.html served)  ✓
POST /api/save (schema-valid H07 payload: regionCount=2, ridgePresent=true, 2 arrows, ridge [[177,-20],[177,-35]])
  →  {"ok":true,"file":"output/hole_07_slope_authoring.json"}  ✓
  server log: [save] wrote output/hole_07_slope_authoring.json — 2 regions, ridge=true  ✓
```

Sample output file deleted after route verification — output/ is clean.

### 4. Code-verified behaviors

- **Backdrop via CSS transform:** `backdropEl.style.transform = \`translate(${tx}px, ${ty}px) rotate(${rotDeg}deg) scale(${scale})\`` — never `left/top`. ✓
- **#gl hidden on return to FLAT:** `glCanvas.style.display = "none"` is set explicitly before restoring flat-view. ✓
- **Arrow tool:** drag base→tip on canvas2d mousedown/mouseup; minimum drag 8px to register. World-XZ captured via `canvasToWorld`. ✓
- **Ridge tool:** click-to-add-points, double-click-to-finish. Erase Last removes last point or clears whole ridge. ✓
- **Region count:** 1 if no ridge or no arrows; 2 if ridge.length >= 2. 2-tier indicator turns green at regionCount==2. ✓
- **Region assignment:** arrow midpoints are assigned region 0 or 1 based on signed cross-product vs nearest ridge segment. ✓
- **Export schema:** all required fields present — hole, regionCount, ridgePresent, editorBackdrop{panelW,panelH,transform,correspondence[]}, arrows[{baseXZ,tipXZ,region}], ridge[[x,z]...], regions[{id,label}]. ✓
- **Auto-loads hole 7** on window load (pilot hole). ✓
- **3D scene:** Three.js r128 via CDN; synthesized height from authored arrows using inverse-distance weighting; orbit via left-drag; zoom via scroll. Returns to FLAT by hiding #gl. ✓
- **Hard rules:** dev-server.mjs not modified ✓ | greens.json not touched ✓ | no dense-grid bake ✓ | panels.json shape correct ✓

### 5. Behaviors that require Cesar's in-browser eyeball (cannot be headless-verified)

- **Backdrop drag feel:** Alt+drag moves the backdrop; opacity/scale/rotate sliders update in real time. Correct by code inspection but requires Cesar to exercise.
- **Visual alignment quality:** whether the PDF panel aligns well onto the green polygon for H07 after manual drag/scale/rotate.
- **Arrow placement UX:** drag base→tip, numbered labels, erase-last flow.
- **3D relief visualization:** whether the synthesized height field looks intuitively useful as a slope check.
- **FLAT↔3D toggle cycle:** no leftover #gl visible (code-verified, but visual confirmation needed).
- **Export → browser alert → output/ file written** in the full round-trip.

---

## Deviations from spec

1. **PDF rendering via PyMuPDF instead of pdfjs-dist:** pdfjs-dist v4 + canvas npm package has a `drawImage(transparentCanvas)` crash when rendering this PDF (the CMap-heavy Japanese document triggers a code path in pdfjs v4's CanvasGraphics that calls `ctx.drawImage` on an internal canvas that node-canvas does not recognize as a valid image). PyMuPDF (already available via `python3 -c "import fitz"`) produces correct renders at 300 DPI and is called via `execSync`. The `sharp` step (resize + JPEG encode) is unchanged. `pdfjs-dist` is kept in `package.json` per spec. If the intent was strictly pdfjs-dist rendering, that's a known blocker with the current environment.

2. **`canvas` package not in final package.json:** I tested it during development but since extract-panels.mjs doesn't import it, I reverted to the spec-specified deps only.

---

## Launch instructions for Cesar

**macOS:** double-click `Tools/GreenSlope/Launch GUI.command` in Finder (or `bash "Tools/GreenSlope/Launch GUI.command"` in Terminal). The script installs deps, extracts panels if needed, starts the server, and opens http://127.0.0.1:4178 in the default browser.

**Manual:** `cd Tools/GreenSlope && npm run gui` → open http://127.0.0.1:4178

**Tool opens on H07 automatically.** Use the hole selector to switch holes.

---

## Open questions for Architect

None. All spec items are addressed or documented as deviations above.
