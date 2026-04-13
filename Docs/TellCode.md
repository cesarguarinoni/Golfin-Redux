# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Issue — Cart Path Intersections Still Overshoot

### Problem
Cart path strip meshes overshoot past their endpoints at intersections.
Paths that should meet cleanly at a junction extend too far beyond it.

### What Was Done (2026-04-13 session)

**Export pipeline (`Tools/UHoleLite/scripts/export-hole.mjs` → `extractCartPathContours`):**

1. **OB feature export fix** ✅ — Trees/cart paths in OB zones were lost because
   the merged grid gives OB priority. Now uses separate `trees_mask` and
   `cart_path_mask` overlays.

2. **Skeleton pixel clipping** ✅ — Extended tee-only clipping to exclude fairway
   (zone 1), bunker (6), water (7), tee (10) using `terrain_grid`. Fixed bug
   where `cart_path_mask` stamping overwrote original zones.

3. **Spine nudging (`nudgeSpinesFromContours`)** ✅ — Iterative geometry-based push
   (10 passes, progressive smoothing) ensures 2.5m strip doesn't overlap
   fairway/bunker/tee/water contour polygons. 15/18 holes fully clean.

4. **dsFactor cap** ✅ — Downsampling was based on `area/longerAxis` which for
   branching networks gave dsFactor=27 (merged close parallel paths). Now capped
   by actual path width so parallel paths stay separate.

5. **Chain merging at 2-way junctions** ✅ — If a junction has exactly 2 chain
   endpoints, those chains are merged into one continuous path. Hole 18: 7→4 paths.

6. **Orphan endpoint snapping** ✅ — After merging, endpoints within 10m of another
   spine's interior get extended to touch it. Hole 18 CP#4 start: 5.6m→0.8m from CP#3.

**Unity importer (`HoleLiteImporter.cs`):**

7. **Splatmap painting** ✅ — Full cart path texture painted under strip mesh
   (100% interior, 85% anti-alias on outer 1px, polygon 0.2m wider than mesh).
   `BuildSpinePolygon` subdivides spine to 0.5m spacing for smooth splatmap edges.

8. **Junction disc patches** ❌ REMOVED — Created octagonal discs at junction
   points but they were visible, clipped through terrain, looked wrong.

9. **Strip endpoint extension** ❌ REMOVED — Extended strip mesh by 2.5m past
   each endpoint. Caused every path to overshoot past its natural end.

### What Still Doesn't Work
**Cart path strip meshes overshoot at intersections.** The endpoint snapping
(step 6) adds a connection point to the spine data, but the strip mesh still
extends past the junction. The overshoot is visible at every path endpoint,
not just junctions.

### Root Cause Analysis
The spine points end at the correct locations, but either:
- The strip mesh `CreateSpineStripMesh` generates geometry past the last point
- The endpoint snap point is past the intersection (snaps to nearest point on
  the *segment* between two spine vertices, which may be between them)
- The spine smoothing/simplification moves endpoints away from the ideal junction

### Key Files
- `Tools/UHoleLite/scripts/export-hole.mjs` — `extractCartPathContours()` (~lines 332-850)
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` — `CreateSpineStripMesh()` (~line 3893)
- `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs` — `CartPathRegionData` class

### Data for Debugging (Hole 18)
```
4 cart paths after merging (was 7 before, 9 before dsFactor fix)
CP#1: 56pts  — short branch
CP#2: 196pts — medium branch
CP#3: 464pts — main long path
CP#4: 22pts  — short branch, start snapped to 0.8m from CP#3
Junctions at: (180.4,-232.6), (144.9,-291.2), (-77.2,117.0) [in export coords]
```

---

## Completed Tasks
✅ 2026-04-13 — Node.js residual ramp (60-cell smoothstep) + Unity-side boundary height propagation
✅ 2026-04-13 — Unity-side boundary height propagation (smoothstep ramp from play height to blurred DEM)
✅ 2026-04-13 — Cart path depression: 3-strategy fix (0.50m inset + smoothstep ramp + 2px splatmap edge paint)
✅ 2026-04-13 — Natural OB↔Rough transition: reuse rough texture with tint + 4px boundary blend
✅ 2026-04-13 — "Smooth OB" button in UHole Lite (vectorize → RDP → Chaikin → rasterize)
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix (OffsetContourOutward + spine polygon)
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-11 — Grid draping iterations, Y=0 origin pattern
✅ 2026-04-10 — Tree placement (mixed mode: terrain + standalone)
✅ 2026-04-10 — Bunker v1-v5 iterations
✅ All earlier tasks (water, bunkers, greens, textures, cart paths, etc.)
