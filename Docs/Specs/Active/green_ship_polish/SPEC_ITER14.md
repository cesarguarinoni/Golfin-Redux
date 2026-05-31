# SPEC ITER-14 — Fairway breaking around the green (issue 2 of 4)

**Authored:** 2026-05-31 08:30 CEST / 15:30 JST (Architect)
**Status:** SPEC_READY
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (iter-14)`
**Parent task:** `green_ship_polish` — four ship-blocker green-fidelity issues, locked order: ridge bumps (iter-13 ✅) → **fairway break (this iter)** → raised ring (iter-15) → off-center (iter-16).
**Scope:** ONE issue. The fairway/green-pad junction on H7 reads as a near-vertical grass **wall** with the carved fairway hole showing through as grey triangles at its toe. Importer-only fix; reuses the existing adaptive-skirt pattern. **No bake, no `green.json` change, no schema change.**

---

## Root cause — VERIFIED in code + data + Cesar's image read (not inferred)

The diagnostic (`ITER14_FAIRWAY_SEAM_DIAGNOSTIC.md`) classified the artifact as overlap/sliver; Cesar corrected it: **the grey triangles at the lip toe are the carved fairway hole showing through** (`iter14_fairway_seam_h07_zoom_lip15.png`). Both symptoms — the grass wall *and* the grey show-through — come from one cause.

In `HoleGeoImporter.cs`:

1. **The whole green pad is seated on ONE flat datum.** Line ~2762:
   `greenSeatY = terrainBaseY + terrain.SampleHeight(CONTOUR CENTROID) + effectiveYOffset`.
   Interior verts = `greenSeatY + relH` (min-shifted, `relH ≥ 0`). So the pad top is planar at the centroid's terrain height.

2. **The collar mesh is dilated by a single uniform scalar.** Line 2702: `DilateContour(contour, collarWidth)` with `collarWidth = GreenCollarWidth = 0.9 m` (L53). Collar ring vertices physically exist **only out to 0.9 m, on every side.**

3. **The collar Y-blend drops the full pad-to-terrain delta over that fixed 0.9 m.** Lines ~2800-2810: `innerBoundaryY = greenSeatY + relH`; `outerRingY = terrainBaseY + perVertTerrainH − GreenSkirtDepth` (≈ terrain + 0.02); `tBlend = 1 − clamp01(d / collarWidth)` smoothstep.

On a green whose **terrain falls steeply from centroid to its leading edge** (H7: Code's raycast shows fairway toe ≈27.5 → pad plateau ≈28.81, ~0.55–0.7 m of *terrain* fall under the W/SW edge), the low-side collar must drop ~0.55 m over a fixed 0.9 m run → a ~31° bank that reads as a **wall**. A near-vertical bank has almost **no horizontal projection in plan**, so the 0.9 m collar fails to cover the carve annulus (`cutContour = dilate(green, 0.65 m)`, L2542, which removes both fairway triangles *and* terrain cells) → the **grey carve-hole triangles peek through** at the toe.

**Why H7 and not the steeper-approach holes (verified):** the defect tracks **centroid→leading-edge terrain drop within the green footprint**, NOT approach steepness or fairway-segment index. H9 (lip 1.32 m) and H14 (1.28 m) are steeper on approach but seat evenly relative to their own centroids → clean. H18 (same Fairway_2 approach) → clean. H7's green.json is structurally unremarkable (relief 0.474 m, *less* than H18's 0.514 m) — the wall is purely the rigid centroid-seat vs local terrain fall, an **importer geometry** issue, not bake data.

## The fix — adaptive collar width (Option B, Cesar-locked)

Reuse the codebase's existing adaptive-skirt pattern (tee skirt L3471-3556 `clamp(1.5·drop / TeeMaxRampSlope, TeeSkirtMeters, TeeMaxSkirtMeters)`; shore L3894). The green collar is the **one** surround still on a hardcoded width. Make it adaptive:

### New constants (top of `HoleGeoImporter.cs`, near the existing green constants L46-72)
```csharp
/// Target max ramp slope (rise/run) for the green collar bank. Gentler than the
/// tee skirt (0.35) — a green surround reads natural ~10°. iter-14.
private const float GreenMaxRampSlope = 0.18f;
/// Upper cap on the adaptive collar dilation (m). Safety ceiling. iter-14.
private const float GreenMaxCollarMeters = 8.0f;
// GreenCollarWidth (0.9 m, L53) is REUSED as the adaptive FLOOR — flat-seated
// greens keep today's 0.9 m collar byte-for-byte.
```

### Step 1 — size the collar envelope per-green, before the CDT build (before L2702)
After `greenSeatY` is computed (L2762) and before `CreateGreenMeshCDT` dilates (L2702): sample terrain at the **original green contour vertices** (the d=0 ring — positions known pre-dilation) and find the worst-case drop:
```
maxDrop  = max over contour verts of: greenSeatY − (terrainBaseY + terrain.SampleHeight(vert) − GreenSkirtDepth)
adaptiveCollarWidth = clamp(maxDrop / GreenMaxRampSlope, GreenCollarWidth, GreenMaxCollarMeters)
```
Use `adaptiveCollarWidth` (not the constant `GreenCollarWidth`) for BOTH:
- the collar mesh dilate at L2702 (`DilateContour(contour, adaptiveCollarWidth)`), so the bank geometry physically exists out to the gentle-ramp distance, AND
- the carve dilate at L2542 (`cutDilate = adaptiveCollarWidth − GreenCutMargin`), so the **uniform** carve always sits inside the widened collar (Option B: one worst-case-sized carve, collar overhangs it by `GreenCutMargin` everywhere). This keeps the shared `cutContour` path a single uniform-dilate — no per-edge variable-offset polygon (that's the iter-5..11 failure family; explicitly NOT entered here).

Flat-seated greens (maxDrop small) clamp to the 0.9 m floor → collar + carve byte-identical to today.

### Step 2 — per-vertex ramp over LOCAL drop (the collar Y-blend, ~L2800-2810)
So flat sides finish their ramp early instead of stretching gently across the whole widened envelope, derive each collar vertex's ramp width from its OWN drop:
```
localDrop      = max(0, innerBoundaryY − outerRingY)               // this vertex
localRampWidth = clamp(localDrop / GreenMaxRampSlope, GreenCollarWidth, adaptiveCollarWidth)
tBlend = 1 − clamp01(d / localRampWidth)
tBlend = tBlend*tBlend*(3 − 2*tBlend)                              // smoothstep (unchanged)
rawVerts[i].y = Mathf.Lerp(outerRingY, innerBoundaryY, tBlend)     // unchanged
```
Result: the low/W side ramps gently over its full ~3 m; flat sides finish at ~0.9 m and the extra collar verts beyond their local ramp sit flat at `outerRingY` (≈ terrain + 0.02) — a flush fringe apron, coplanar with surrounding terrain/fairway, no wall.

### Why this fixes both symptoms at once
- Wall → gentle bank: the drop is now spread over `maxDrop / 0.18` m (H7 ≈ 3 m), a ~10° ramp.
- Grey show-through → covered: a ~10° bank has real horizontal projection, and the carve was dilated by the same `adaptiveCollarWidth`, so the collar overhangs the carve annulus by `GreenCutMargin` on every side, as originally intended.

## What must NOT change
- **The green putting surface.** The `insideGreen` branch (`rawVerts[i].y = greenSeatY + relH`, L2790) is untouched. No terrain macro-tilt injected (Hard Rule 2). Interior relief stays ~0.42 m; physics on the green bit-identical (`BakedHeightProvider` reads unchanged interior verts).
- **The bake.** `bake-green.mjs`, `green.json`, schema v2 byte layout — all untouched. This is importer-only.
- **Flat-seated greens.** maxDrop → floor → 0.9 m collar + 0.65 m carve byte-for-byte as today.

## Files touched
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — 2 new constants; `adaptiveCollarWidth` computed once per green and fed to the collar dilate (L2702) + carve dilate (L2542); per-vertex `localRampWidth` in the collar Y-blend (~L2803). Nothing else.
- Regenerated meshes under `Assets/Golf/Courses/lomond-country-club/Generated/Hole_NN_Geo.unity` (reimport output; not hand-edited).

Nothing else. No bake, no `green.json`, no schema, no `BallSimulation`, no scene hand-edit.

## Hard rules
1. **`HoleGeoImporter.cs` ONLY** (the LIVE importer; `HoleLiteImporter` is deprecated, banner header commit 980cc122 — verify entry via `grep MenuItem` before touching internals).
2. Do **not** touch the `insideGreen` branch / the designed green surface. Hard Rule 2 (no terrain macro-tilt into the green) holds.
3. **One shared `cutContour`/dilate scalar.** The collar mesh dilate and the carve dilate MUST both use `adaptiveCollarWidth`. They can never drift (carry-forward from iter-5 hard rule 8).
4. Carve stays a **uniform** dilate sized to worst-case (Option B). Do NOT build a per-edge variable-offset carve polygon.
5. No bake / `green.json` / schema change. No `TerrainData` heightmap edit (changing the carve *polygon* offset is permitted; rasterizing into the heightmap is not).
6. No scene hand-edit / raw YAML. Reimport via the Geo importer menu item only.
7. Flat-seated greens must come out byte-identical (clamp-to-floor proof in the report).

## Verification — before in-engine
Implementer reports, per green, from the importer's `reimport_report.txt`:
```
Green N: greenSeatY=__  maxDrop=__  adaptiveCollarWidth=__  (floor 0.90 / cap 8.00)  carveDilate=__
```
Expect: H7 `adaptiveCollarWidth` ≈ 3 m (maxDrop ~0.55 / 0.18); H5/H11 and other flat-seated greens clamp to 0.90 (→ byte-identical collar+carve). No green hits the 8 m cap; if one does, report it (likely an over-large maxDrop = a seating bug to surface, not silently clamp).

## In-engine verification
Reimport **H7 first**. Cesar checks from the `iter14_fairway_seam_h07_graze_w_15.png` / `_zoom_lip15.png` camera angles:
- Leading-edge bank reads as a **gentle grass slope**, not a vertical wall.
- **No grey carve-hole triangles** at the toe (collar covers the carve everywhere).
- No green↔fairway gap or z-fight from any grazing angle.
- Green elevation preserved (it's a legitimately raised green — keep the pad, just grade the bank).
- iter-13 ridge fix not regressed (H7 is 2-tier; ridge ramp + tier flats unchanged).

If signed off → reimport the rest. **Spot-check matrix:** H9 + H14 (steepest, were clean — must stay clean), H18 (Fairway_2, was clean), H5 (flattest → clamp-to-floor, must be unchanged), H6 + H12 (next-steepest, never captured — confirm clean), and one more 2-tier (H3 or H11) for ridge non-regression.

## Definition of done
- 2 constants added; `adaptiveCollarWidth` computed per green and fed to BOTH the collar dilate and carve dilate; per-vertex `localRampWidth` in the collar blend.
- `reimport_report.txt` shows the per-green seat/drop/width line; H7 ≈ 3 m, flat greens clamp to 0.90.
- H7 reimport: gentle bank, no grey show-through, no seam, elevation preserved, ridge intact — Cesar sign-off from the iter-14 reference angles.
- Spot-check matrix all clean / unchanged (flat greens byte-identical; steep-clean holes still clean; 2-tier ridge intact).
- Any importer/green EditMode tests still PASS (report the count; bit-exact physics gate unaffected — collar Ys change, green-interior + `BallSimulation` do not).
- IMPLEMENTER_REPORT content-sanity description per Lesson O — describe what each verification capture actually shows at the junction, not "captured."

## Open items the implementer should report back on
1. Final per-green `adaptiveCollarWidth` table (all 18). Flag any hole at the 8 m cap.
2. Does the widened collar visually **overpower** any small green on the flat sides (the iter-8 open-item 3 concern, now live since flat-side collar can extend to the worst-case envelope at `outerRingY`)? If a small green looks like more fringe than green, flag — we may want the flat-side apron clamped tighter (e.g. cap flat-side verts at the floor width, not the envelope).
3. Confirm `maxDrop` sampled at the **original contour ring** (pre-dilate) matches the actual low-side collar drop after dilation within a few cm. If the dilated edge lands on materially different terrain, the envelope may under/over-size — report the delta.
4. Confirm the green-interior verts and `BakedHeightProvider` output are unchanged vs HEAD for H7 (the fix must not move the putting surface).
