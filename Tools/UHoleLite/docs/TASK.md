# TASK.md — UHole Lite Instructions for Claude Code

> Claude Code: Read this file at the start of each task.
> After completing, add a status line at the bottom.
> Handoff: `Tools/UHoleLite/docs/TASK.md`

---

## Current Task — Increase Heightmap Resolution to 2049

**Goal:** Change the heightmap resolution constant from 1025 to 2049.

### Change

In `Tools/UHoleLite/scripts/generate-terrain.mjs`, line ~21:

```javascript
// OLD:
const RES = 1025;

// NEW:
const RES = 2049;
```

That's the only change. Everything else in the script scales
automatically with this constant (loop bounds, DEM sampling,
zone mask resampling, raw output size, terrain-meta.json).

### After the Code Change

Re-run the pipeline for Hole 1:

```powershell
cd Tools\UHoleLite
node scripts/generate-terrain.mjs lomond-country-club 1
node scripts/export-hole.mjs lomond-country-club 1
```

### Verification

1. `holes/01/terrain-meta.json` should show `"resolution": 2049`
2. `holes/01/heightmap.raw` should be ~8MB (2049×2049×2 bytes)
3. No errors during generation or export

### Do NOT

- Change zone grid resolution (independent)
- Change texture resolution
- Change contour extraction or export pipeline
- Change any other scripts
