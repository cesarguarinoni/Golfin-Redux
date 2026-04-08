# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Diagnose & Fix Fairway Width Shrinkage

**Problem:** The middle fairway section (fairway #1, the large region)
is significantly thinner in Unity than in the zone illustration. Other
thin sections of the fairway are fine — this is localized to one area.

**Key discovery:** The zone grid is 2596×3124 pixels (0.20 m/px), NOT
the 794×956 I originally assumed. At this resolution, RDP epsilon=3.0
removes points within 15 pixels of the simplification line. On a corridor
that's only 50-100 pixels wide, that's devastating.

### Step 1: Run the diagnostic script

```
cd Tools/UHoleLite
node scripts/diagnose-fairway.mjs lomond-country-club 1
```

This compares the raw zone grid fairway width vs the smoothed contour
width at each Z position. Look for rows where the difference is large
(marked `*** BIG DIFF`). This tells us exactly WHERE the shrinkage
happens and by HOW MUCH.

**Paste the output into TellCode.md** so the architect can analyze it.

### Step 2: Based on diagnostic results

**If the issue is RDP (contour has too few points in the problem area):**
The fix is to reduce RDP epsilon for fairways. The grid is 0.2m/px
so epsilon=3.0 means 15px tolerance — way too aggressive. Try
epsilon=1.0 (5px tolerance) which is enough to remove collinear points
but preserves corridor shape.

Change in `export-hole.mjs`:
```javascript
const fairways = extractZoneContours(zonesData, terrainMeta, 1, 30, 1.0, 3);
```

**If the issue is Chaikin (plenty of points but they're pulled inward):**
Reduce Chaikin to 2 passes:
```javascript
const fairways = extractZoneContours(zonesData, terrainMeta, 1, 30, 1.0, 2);
```

**If the issue is the border trace itself (traceBorder shortcutting):**
This would require fixing the 8-connected walk algorithm.

### Step 3: Re-export and re-import to verify

```
node scripts/export-hole.mjs lomond-country-club 1
```
Then re-import in Unity and compare the middle section width.

---

### Do NOT

- Change fringe ring direction (stays inward)
- Modify bunker, green, or water pipelines
- Apply uniform dilation (bloats wide sections)

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes + fringe ring
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ DONE: 2026-04-08 — Tee border ring with gradient texture
✅ DONE: 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ DONE: 2026-04-08 — Fairway width fix: RDP epsilon 3.0→1.0, Chaikin 3→2 passes. z=50 diff: -5.4m→-1.2m, z=150: -4.2m→-2.7m. One BIG DIFF remains at z=-5 (tip narrowing, -5.2m) — likely source data.

### Diagnostic output (after fix: epsilon=1.0, smoothPasses=2)

Fairway #1: 132713 pixels, 108 contour points (was 128 with old params)

Smoothed Contour Width vs Grid:
```
localZ   | widthM | gridWidthM | diff
    -5.0 |    7.9 |       13.1 |  -5.2 *** BIG DIFF (fairway tip)
     0.0 |   17.4 |       17.3 |   0.0
    50.0 |   48.2 |       49.4 |  -1.2  (was -5.4)
   150.0 |    7.2 |        9.9 |  -2.7  (was -4.2)
```
Most other rows within ±1.5m. The z=-5 BIG DIFF is at the narrow tip where the fairway transitions — may need source data adjustment.
