# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — High-Res SVG Rasterization (4096×4096)

**Problem:** Rasterizing SVG at the current zone grid resolution
(~1024×1024) reintroduces jaggies. At 4096×4096, each pixel is ~0.1m —
invisible at gameplay camera distance.

**Fix:** Change the SVG import to rasterize at 4096×4096 instead of
`zoneGridW × zoneGridH`. The zone grid becomes 4096×4096. Downstream
pipeline handles any grid size — splatmap resamples to 256×256 anyway,
contour tracing works on any resolution, water mask extraction scales.

**File:** `Tools/UHoleLite/app/app.js`

---

### What to change

In the SVG import handler (`svg-file-input` change event), change:

```javascript
const targetW = zoneGridW || 1024;
const targetH = zoneGridH || 1024;
```

To:

```javascript
const targetW = 4096;
const targetH = 4096;
```

That's it.

---

### Verification

- [ ] Import an SVG — zone grid becomes 4096×4096
- [ ] Zone boundaries visibly smoother than 1024×1024
- [ ] Save works (zones.json will be larger — ~16MB base64 grid)
- [ ] Export still works: `node scripts/export-hole.mjs lomond-country-club 1`
- [ ] Unity import still works (splatmap resamples to 256×256)
- [ ] Contours (bunkers, greens) are smoother

### Concern

The zones.json base64 grid at 4096×4096 = 16M pixels = ~21MB base64.
If this is too large for the pipeline, try 2048×2048 (4M pixels, ~5MB)
as a compromise.

### Do NOT

- Modify export pipeline or Unity importer
- Remove PNG import button

✅ DONE: 2026-04-08 — SVG rasterization now at 4096×4096
