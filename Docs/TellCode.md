# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Import Zones from SVG (Inkscape)

**Goal:** Add "Import Zones SVG" button to Hole Viewer. User draws zone
shapes in Inkscape with zone fill colors. App rasterizes SVG to zone
grid — vector edges = zero jaggies. Keep existing PNG import too.

**Also:** Remove all broken straighten code.

**Files:**
- `Tools/UHoleLite/app/app.js` — add SVG import, remove broken straighten
- `Tools/UHoleLite/app/index.html` — replace Straighten button with Import SVG

---

### How it works

1. User traces zone shapes in Inkscape over the hole illustration
2. Each shape's fill = zone color (same palette as paint tool)
3. Click "Import Zones SVG" → app rasterizes SVG to offscreen canvas
   at zone grid resolution → reads pixels → nearest-color match to
   zone index → replaces zone grid

### Implementation

#### 1. HTML — replace Straighten button

```html
<button id="import-svg-btn" title="Import zones from SVG">Import Zones SVG</button>
<input type="file" id="svg-file-input" accept=".svg" style="display:none">
```

#### 2. JS — SVG import logic

```javascript
document.getElementById("import-svg-btn").addEventListener("click", () => {
  document.getElementById("svg-file-input").click();
});

document.getElementById("svg-file-input").addEventListener("change", async (e) => {
  const file = e.target.files[0];
  if (!file) return;

  const svgText = await file.text();
  const targetW = zoneGridW || 1024;
  const targetH = zoneGridH || 1024;

  const blob = new Blob([svgText], { type: "image/svg+xml;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const img = new Image();

  img.onload = () => {
    const tmpCanvas = document.createElement("canvas");
    tmpCanvas.width = targetW;
    tmpCanvas.height = targetH;
    const tmpCtx = tmpCanvas.getContext("2d");

    // Black background = zone 0 (background)
    tmpCtx.fillStyle = "#000000";
    tmpCtx.fillRect(0, 0, targetW, targetH);
    tmpCtx.drawImage(img, 0, 0, targetW, targetH);

    const imageData = tmpCtx.getImageData(0, 0, targetW, targetH);
    const pixels = imageData.data;
    const newGrid = new Uint8Array(targetW * targetH);

    for (let i = 0; i < targetW * targetH; i++) {
      const r = pixels[i * 4];
      const g = pixels[i * 4 + 1];
      const b = pixels[i * 4 + 2];
      newGrid[i] = matchZoneColorNearest(r, g, b);
    }

    if (zoneGrid) {
      zoneUndoStack.push(new Uint8Array(zoneGrid));
      if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();
    }

    zoneGrid = newGrid;
    zoneGridW = targetW;
    zoneGridH = targetH;
    zonePaintDirty = true;
    drawHole();

    URL.revokeObjectURL(url);
    console.log(`Imported zones from SVG: ${targetW}×${targetH}`);
  };

  img.src = url;
  e.target.value = "";
});

// Nearest-match because SVG rasterizer anti-aliases edges
function matchZoneColorNearest(r, g, b) {
  let bestZone = 0;
  let bestDist = Infinity;
  for (let z = 0; z < ZONE_COLORS_RGB.length; z++) {
    const [zr, zg, zb] = ZONE_COLORS_RGB[z];
    const dist = (r - zr) ** 2 + (g - zg) ** 2 + (b - zb) ** 2;
    if (dist < bestDist) { bestDist = dist; bestZone = z; }
  }
  return bestZone;
}
```

#### 3. Remove broken straighten code

Delete all of these if present: `straightenBoundaries()`,
`traceBorderPixels()`, `rdpSimplify()`, `perpDist()`,
`chaikinSmooth()`, `scanlineFill()`, `dilateMask()`, `erodeMask()`,
the Straighten button handler and HTML element.

**Keep** the PNG import button and `matchZoneColor()` (exact match).

---

### Verification

- [ ] "Import Zones SVG" button works in Hole Viewer
- [ ] "Import Zones PNG" button still works
- [ ] SVG rasterized at correct zone grid resolution
- [ ] Anti-aliased edges snap to nearest zone color
- [ ] Undo works
- [ ] Save persists to zones.json + zones.png
- [ ] No broken straighten code remains

### Do NOT

- Modify export pipeline or Unity importer

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Import Zones SVG button + removed all straighten code
