# SPEC — Collar↔terrain seam fix: terrain apron ring (green_ship_polish, PASS 2 follow-up)

**Authored:** 2026-06-01 12:30 CEST / 19:30 JST (Architect)
**Status:** SPEC_READY
**Track:** `green_ship_polish` — PASS 2 follow-up. The B1 fitted-plane seat + collar↔fairway CDT weld are ACCEPTED and shipped. This fixes the one remaining ship-blocker: the collar↔**terrain** sawtooth on greens with no bordering fairway.
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (terrain-apron)`
**Scope:** importer-only (`HoleGeoImporter.cs`); **only the 2 terrain-bordered greens (H10, H18)** on this course — but the mechanism is general (any green with no fairway within collar range).

---

## Scope — CONFIRMED, only 2 greens
Cesar-confirmed + Code-computed (against `heightmap.raw`, the pre-depression surface the seat plane fits; cross-validated H16 sink/float vs `reimport_report.txt` exactly):
- **16 of 18 greens are fairway-bordered** → collar↔fairway seam, already welded clean by the CDT-hole-constraint (Cesar-blessed, DO NOT TOUCH).
- **Only H10 and H18 have NO fairway within collar range** (nearest fairway 12.2 m / 22.0 m) → entire perimeter is collar↔terrain → rasterized `SetHoles` carve → T-junction sawtooth.
- Terrain intrusion above the seat plane at the green edge: **H10 ≈ 0.19 m** (terrain proud — carved rim can stand as a visible lip + sawtooth), **H18 ≈ 0.075 m** (near-flush — mostly flat raster sawtooth, minimal lip). The fix MUST handle the H10 proud-rim case, not just H18's flat case.

## Root cause (verified in code)
The collar↔fairway seam is welded (shared CDT boundary verts). The collar↔terrain boundary is NOT a vertex boundary: the terrain hole is a **rasterized `SetHoles` boolean mask** at `terrainData.holesResolution`. That res is **never set** in the importer → defaults to `heightmapResolution` (2049) over a ~2006 m terrain → holes-grid cell ≈ **0.98 m**. So the terrain-hole edge is stair-stepped at ~1 m; the collar mesh outer ring is a smooth polygon; where they meet → T-junctions → ~1 m sawtooth. H7 (and the other 15) hide it because their perimeter is mostly welded fairway; H10/H18 have no fairway so the whole perimeter shows teeth.

**Why not just raise holesResolution:** a ~1 m tooth needs the collar to overhang >1 m to hide it → re-creates the iter-14 mound. Bumping holesResolution only shrinks teeth (never eliminates) and costs ~64 MB/terrain at 8192. Rejected. **Why not re-cut terrain mesh:** overkill for 2 greens; terrain is a Unity heightmap+holemask, not a CDT-able mesh.

## The fix — terrain apron ring (Option C), terrain/rough material

Generate a thin **apron ring mesh** bridging the collar outer ring → terrain, ONLY for terrain-bordered greens. It sits OVER the raster hole so the visible edge is mesh-on-terrain (smooth) instead of mesh-meets-raster (sawtooth). The raster hole stays (terrain genuinely intrudes on both greens — carve is needed); the apron hides its stair-stepped edge.

### Key enabler (verified): the collar outer ring is reproducible by construction
The collar outer ring = `DilateContour(activeContour, GreenCollarWidth)` — computed identically at the carve site (L2643) and in the mesh build (L2840), a pure function of the green contour. So the apron's INNER ring = `DilateContour(activeContour, GreenCollarWidth)` reproduces the collar outer ring **exactly** (same function, same input → coincident verts by construction — the same coincidence trick the blessed fairway weld uses, NO new vertex-snapping, NO Hard-Rule-6 collar↔fairway risk). The apron's OUTER ring = `DilateContour(activeContour, GreenCollarWidth + ApronWidth)`.

### New constant (near the green constants ~L53-72)
```csharp
/// Terrain-apron ring width (m) beyond the collar outer edge, for terrain-bordered
/// greens only. Must exceed the holes-grid cell (~1 m) so the apron always covers
/// the rasterized SetHoles teeth. 1.5 m = ~1.5 cells of cover. terrain-apron 2026-06-01.
private const float GreenTerrainApronWidth = 1.5f;
```

### Change 1 — detect terrain-bordered greens (gate the apron)
A green needs the apron iff NO fairway lies within `GreenCollarWidth` of its perimeter. Reuse the fairway data already loaded for the cut/weld pass. Compute per green (point-to-edge distance from green perimeter samples to fairway polygons, NOT vertex-to-vertex — vertex distance overstates the gap). If min distance > GreenCollarWidth → `isTerrainBordered = true`. On this course that flags exactly H10, H18; keep it computed (not hardcoded) so future terrain-bordered greens are handled. Report per green in `reimport_report.txt`.

### Change 2 — generate the apron ring (terrain-bordered greens only)
After the collar mesh is built (post-`CreateGreenMeshCDT`, ~L2703), if `isTerrainBordered`:
- **Inner ring** = `DilateContour(activeContour, GreenCollarWidth)` — coincident with the collar outer ring (same verts, same XZ).
- **Outer ring** = `DilateContour(activeContour, GreenCollarWidth + GreenTerrainApronWidth)`.
- Triangulate the ring between them (quad strip between corresponding inner/outer verts; or CDT the annulus).
- **Inner-ring Y** = the collar outer-ring formula EXACTLY: `terrainBaseY + perVertTerrainH(innerVert) − GreenSkirtDepth` (L2934) → welds to the collar Y (coincident in XZ AND Y). 
- **Outer-ring Y** = terrain surface at the outer vert: `terrainBaseY + terrain.SampleHeight(outerVert) − GreenSkirtDepth` → apron meets raw terrain coplanar (both smooth → no sawtooth at the outer edge; the ~2 cm `GreenSkirtDepth` lift manages z-fight, same as the collar does today).
- The apron thus grades from the collar edge (which sits at `seatPlane`-derived height) down to terrain over 1.5 m. On H10 that absorbs the ~0.19 m proud rim as a gentle ramp (~0.13 slope, below the tee-skirt 0.35 ceiling — natural); on H18 it's near-flat.

### Change 3 — terrain/rough material + surface classification
- Apron mesh uses the **terrain/rough material** (Cesar-chosen), NOT collar/fringe — so the green's apparent size is unchanged; the apron reads as rough falling away from the green (matches real-course look). Use the same material/shader the rough or terrain-skirt meshes use; confirm which by grepping existing rough/terrain mesh material assignment.
- Tag the apron as its own surface type (its own submesh / GameObject) so green/collar/fairway/**apron(rough)**/terrain stay separately identifiable for physics + rendering. A ball on the apron should play as rough, NOT green/collar.
- The apron sits OVER the raster terrain hole — confirm the hole still carves (terrain genuinely intrudes 0.19/0.075 m → carve needed) and the apron fully covers the carved annulus + teeth (apron width 1.5 m > collar 0.9 m cut, and > ~1 m tooth).

## What must NOT change
- The collar↔fairway CDT weld (`CDTTriangulateWithHoles` L4574, `s_greenCentroids` L89, `CreateFairwayMesh` L4977) — blessed, untouched. The 16 fairway-bordered greens get NO apron and must be byte-identical.
- The B1 fitted-plane seat, `relH`, the collar mesh, `green.json`, schema, bake — all untouched.
- `GreenCollarWidth` (0.9 m), `GreenSkirtDepth` (−0.02). The apron is ADDITIVE; the collar is unchanged.

## Hard rules
1. `HoleGeoImporter.cs` ONLY (LIVE importer; verify `grep MenuItem`).
2. Apron generated ONLY for `isTerrainBordered` greens (H10, H18 here). 16 fairway greens byte-identical — prove it.
3. Do NOT touch the collar↔fairway CDT weld. This is the *terrain* seam — a distinct sub-problem (Cesar Q2 ruling). Hard Rule 6's "no 4th collar↔fairway variation" does NOT apply here, but do not introduce any collar↔fairway change as a side effect.
4. Apron inner ring = `DilateContour(activeContour, GreenCollarWidth)` with the collar outer-ring Y formula → coincident by construction. No independent vertex-snapping pass.
5. Apron = terrain/rough material + own surface type (plays as rough). NOT collar.
6. Raster hole still carves (terrain intrudes on both) — apron covers it, does not replace it.
7. `LESSONS_FRINGE_BORDER_MESHES.md` is mandatory reading before touching this code (CLAUDE.md rule) — apply its lessons.

## Verification — H10 + H18 mandatory before/after
H10 has NOT been visually inspected and (0.19 m vs 0.075 m) is likely worse than H18 — shoot it first.
Per terrain-bordered green, report:
```
Green N: isTerrainBordered=Y  nearestFairway=__m  terrainProudMax=__m  apronWidth=1.50  apronInnerWeldGap=__ (must be ~0)  apronMaterial=rough
```
Gate (runs-per-row at a REALISTIC lighter-pixel threshold, not pure-white — the over-strict pure-white measure gave a false clean once):
- **H10 + H18:** collar↔terrain edge shows **NO sawtooth** (runs/row ≤ 3) from the grazing arc; the ~1 m teeth are covered by the apron.
- **H10 proud rim:** the ~0.19 m terrain rise reads as a gentle apron ramp, NOT a standing carved lip.
- Apron reads as **rough** (terrain material), green apparent size unchanged.
- Ball dropped on the apron plays as **rough**, not green/collar (surface classification).
- **16 fairway greens byte-identical** (no apron emitted; spot-check H7 — its blessed weld + canonical SW seam unchanged vs `b1_merged_h07_canonical_sw.png`).
- Frame-extract the orbit video at native res on the grazing arc and LOOK before captioning (false-clean has slipped twice — N=3 discipline).

If H10 + H18 pass → done. Spot-check H7 (welded, unchanged) + one more fairway green (e.g. H16, nearest-fairway 2.7 m — confirm it correctly did NOT get an apron).

## Files touched
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — `GreenTerrainApronWidth` const; terrain-bordered detection (Change 1); apron ring generation (Change 2); rough material + surface tag (Change 3).
- Regenerated `Generated/Hole_10_Geo.unity` + `Hole_18_Geo.unity` (the other 16 must be byte-identical).
- NO bake, NO `green.json`, NO schema, NO physics-gate change (apron is rough surface; green-interior heights unchanged → `BakedHeightProvider` for the putting surface unaffected; confirm apron isn't sampled into the green height provider).

## Definition of done
- `GreenTerrainApronWidth` const + terrain-bordered detection + apron ring (inner=collar outer ring by construction, rough material, own surface type).
- H10 + H18: no sawtooth (runs/row ≤ 3 realistic threshold), H10 proud rim graded not standing, apron plays/reads as rough — Cesar sign-off from grazing-arc captures (H10 before/after mandatory — first inspection of that hole).
- 16 fairway greens byte-identical (H7 weld unchanged; H16 correctly no apron).
- Importer/green EditMode tests pass (count reported). Physics gate unaffected (prove apron excluded from green height provider).
- IMPLEMENTER_REPORT content-sanity per Lesson O — describe what the H10/H18 grazing captures actually show at the collar↔terrain edge (teeth gone? rim graded?), not "captured."

## Open items to report back
1. H10 vs H18 before/after — is H10 indeed worse (proud rim)? Did the apron grade it cleanly?
2. Apron inner-ring weld gap to collar outer ring (must be ~0; the coincidence-by-construction proof).
3. Confirm the apron is excluded from `BakedHeightProvider` (it's rough, not puttable) — no physics-gate change.
4. Which existing material/shader was reused for rough — confirm it matches surrounding terrain visually (no obvious patch).
5. Any other course (future) — confirm the terrain-bordered detection is data-driven, not hardcoded to {10,18}.
