# SPEC — green_slope_height_bake

**Authored:** 2026-05-28 18:19 CEST / 2026-05-29 01:19 JST (Architect)
**Tier:** FULL — visual fidelity + spatial math. Pilot H07 → Cesar in-engine sign-off → all 18.
**Status:** SPEC_READY
**Kickoff:** `Use the golfin-implementer subagent on "green_slope_height_bake"`
**Supersedes:** abandoned `lomond_greens_authoring_batch` (Cesar-rejected ×2, stashed). Consumes the output of `green_slope_authoring_tool`.

---

## Goal

Turn the human-authored slope data (`Tools/GreenSlope/output/hole_NN_slope_authoring.json`, all 18 holes) into shippable greens that **break correctly** and are **visibly undulated** in-engine. Two consumers from one bake: the runtime slope grid (`green.json`, physics + future predictor) and the green **mesh** (visible tiers/ridges + ball height).

This is release work, not a throwaway. Greens must look and play right.

---

## The core correctness rule (the bug we're killing)

A PDF arrow is a **fall-line sample**: a point-sample of the green's gradient field — direction = downhill, length = steepness. It is **NOT** a flat facet that tilts a region. The bake builds **one continuous gradient field per green** by interpolating the arrows, with the **ridge as an interpolation barrier** (upper-tier arrows never average with lower-tier arrows across the divide). Per-arrow facets = the rejected approach. Do not reintroduce them.

Arrows as authored are **total** slope (Cesar traced the real green's printed fall lines, which already include macro + local). Therefore **do not also add terrain macro-tilt** to the gradient or height — that double-counts. Terrain's only role here is seating the green's absolute elevation + the collar ramp (importer side).

---

## Inputs (all verified on disk this session)

1. **Authoring JSON** — `Tools/GreenSlope/output/hole_NN_slope_authoring.json`
   Fields: `arrows[] {baseXZ:[x,z], tipXZ:[x,z], region:int}` (world meters), `ridge[] [[x,z]…]` (world polyline; `[]` if none), `regions[] {id,label}`, `regionCount`, `ridgePresent`. Downhill = `tip − base`. Arrow length is in **world meters**.
2. **Green contour** — `Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/greens.json`
   `greens[0].contour[] {x,z}` (world XZ, already Unity-Z-flipped), `center_local`, `size_m`, `height_m`.
3. **Runtime consumer** — `Assets/Scripts/Course/Runtime/GreenTopology.cs` (schema, below).
4. **Mesh builder** — `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` → `CreateGreenMeshCDT` / `CDTTriangulate` (mesh, below).

---

## Deliverable 1 — Bake script: `Tools/GreenSlope/scripts/bake-green.mjs` (node, no Unity)

Per hole (`--hole N`, and `--all`):

1. **Grid.** AABB of the contour (+0.5 m pad), `cellSize = 0.5`, row-major `[z][x]`. This defines `boundsMin/Max`, `gridWidth/Height` exactly as `GreenTopology` expects.
2. **Region classify.** Each cell center → region 0/1 by which side of the `ridge` polyline it falls (point-vs-polyline side test). No ridge → single region (all arrows).
3. **Gradient field.** For each in-polygon cell, interpolate arrows **of the cell's own region only** (ridge = hard barrier):
   - Direction: inverse-distance-weighted (`1/d²`, clamp d) sum of arrow unit vectors `(tip−base)`, renormalized.
   - Magnitude %: from arrow **world length** → `magPct = clamp(lenM / refLenM, 0,1) · (maxSlopePct − minSlopePct) + minSlopePct`, IDW-blended. World-meter basis keeps steepness consistent across holes.
   - Cells outside the polygon: `(0,0,0)`.
4. **Height field.** Integrate the gradient to height via **Poisson** (`∇²h = ∇·g`), Neumann boundary, **ridge as an internal boundary** so the steep tier transition is preserved (don't relax across it). Iterative Jacobi/Gauss-Seidel relaxation — no external libs, deterministic. Then **subtract the mean** → zero-mean relative meters (green neither sinks nor floats; importer seats absolute).
5. **Pins.** Preserve `pinCandidates` from an existing `green.json` if present; else emit one placeholder at the contour centroid, label `"centroid_placeholder"`, so `GetDefaultPin()` won't throw. NOTE: real pin authoring is a separate task.
6. **Write** `Assets/Resources/HoleData/Hole_NN/green.json` at **schema v2** (below), `sourceTag = "green_slope_height_bake YYYY-MM-DD"`.
7. **QA gate** → print + write `bake_report.txt` per hole, FAIL LOUD on: any arrow base outside the contour (list them — this caught the prior rejection); per-region arrow counts; `regionCount` vs `ridgePresent` mismatch; NaN in either grid; height range implausible (`> size_m · maxSlopePct`).

**Tunables (consts at top, documented):** `minSlopePct` (~0.5), `maxSlopePct` (~5.0), `refLenM` (~4.0). Cesar tunes by eye against the ShotNavi heatmap colours (we have no numeric magnitude — the ShotNavi numbers are distances, not slope).

---

## Deliverable 2 — Schema v2 in `GreenTopology.cs` (additive)

Keep all v1 fields. Add:
- `CurrentSchemaVersion → 2`.
- DTO: `string heightGridBase64` (float32 ×1 per cell, row-major `[z][x]`, zero-mean **relative** meters), `float heightDatumY` (bake writes `0`; reserved).
- `FromDto`: decode `heightGrid`, validate `bytes.Length == gridWidth·gridHeight·sizeof(float)` (mirror the slope-grid check, same LogError pattern).
- New `public bool TrySampleHeight(Vector2 worldXZ, out float relHeightM)` — nearest-cell, same bounds logic as `TrySampleSlope`, returns `false` outside bounds.
- Update XML docs to v2.

Version bump means a hole without a v2 `green.json` returns `null` from `LoadFromResources` → consumers degrade gracefully (no break, flat green) until rebaked. **Verify `GreenTopologyCache.GetForHole` null-handles** so the pilot gap (H07 v2, others unbaked) can't throw.

---

## Deliverable 3 — Mesh height in `HoleLiteImporter` (additive + guarded)

In `CreateGreenMeshCDT` (or a post-triangulation pass), when a **v2 `green.json` with a height grid exists** for the hole:
- Load it (reuse `GreenTopology`). Seat: `greenSeatY = terrain.SampleHeight(centroid) + effectiveYOffset`.
- **Interior** verts: `Y = greenSeatY + TrySampleHeight(vert.xz)` (replaces the flat per-vert `terrain.SampleHeight` base for the green submesh). Keep the existing `GreenRaiseMeters`/collar logic conceptually, but the interior raise now comes from the authored field.
- **Collar** verts: ramp (existing smoothstep) from the **authored green-boundary height** to `terrain.SampleHeight(outer)` — so the green blends to surrounding terrain with no seam.
- **Ridge crease:** pass the authoring `ridge` polyline as an additional **CDT internal constraint** (alongside the existing `innerConstraint: contour`) so triangle edges align to the ridge and the tier transition renders crisply. NOTE: if `CDTTriangulate` can't take a second constraint, inject the ridge points as constraint vertices — flag the exact approach in the implementer report.
- **Density:** for height-authored greens, triangulate at **0.5 m** (pass `0.5f` where `1.0f` is today) for smooth undulation. NOTE perf: more verts + finer collider → validate frame cost on H07 on a mobile-tier profile before committing to 0.5 m for all 18; fall back to 0.75 m if needed.

**Guard:** holes with no v2 `green.json` keep the current behavior exactly (per-vert `terrain.SampleHeight`). Non-breaking.

> **CORRECTION (2026-05-29):** A prior version of this section claimed the terrain under greens is already depressed 0.40 m by `DepressTerrainUnderOverlays`. That is **false** — that function grades fairways/tees/cart-paths/water only. Greens *do*, however, carve a terrain hole at L2502–2522 (`holes[hz,hx] = false` inside a `greenCollarScale × 0.95` contour), so the underlying terrain mesh is deleted under most of the green — see § Amendment 2026-05-29 (iter-5) for what's actually broken and the corrected fix.

---

## Break model (locked for this task)

Break stays the **authored grid lateral force** (`TrySampleSlope` → the Phase-6 integrator's lateral acceleration). The deformed mesh is real geometry the ball rests on; it does **not** drive break via gravity/collider. No putter-physics rework here (leaves Orders 260/270/280 untouched). Gravity-on-mesh break is a separate, later realism task.

---

## Sequence & safety

1. **Pilot H07** (the 2-tier hole). Bake → reimport H07 → Cesar checks in-engine: visible upper/lower tiers, crisp ridge ramp, ball sits on the surface, putts break consistent with what's seen. Sign-off required.
2. Then `bake-green.mjs --all`; reimport. Spot-check 2-tier holes (3 / 11 / 18) show two tiers; flat holes (e.g. 5) stay single-plane.
3. Pilot gap is safe: unbaked holes return `null` → graceful flat, no crash.

## Hard rules

1. Arrows → one continuous interpolated gradient field per region; ridge = barrier. **Never** per-arrow facets.
2. Arrows are **total** slope — do **not** add terrain macro-tilt to gradient/height (no double-count). Terrain only seats absolute + collar ramp.
3. Importer change is **additive + guarded** — holes without v2 `green.json` are byte-for-byte unchanged.
4. Touch only: `bake-green.mjs` (new), `GreenTopology.cs` (additive v2), `HoleLiteImporter` green-mesh path **and fairway-mesh cut path**, the green terrain-hole-carve radius (existing mechanism, polygon swap only), and the 18 `green.json` outputs. No other Unity assets, no `greens.json`, **no `TerrainData` heightmap edits.** **[AMENDED 2026-05-29 iter-5 — see § Amendment 2026-05-29 (iter-5).]**
5. Break stays grid-force. No gravity/collider putt changes.
6. `green.json` base64 must match `GreenTopology` byte layout exactly: slope grid float32 ×3 `(dirX,dirZ,magPct)` row-major `[z][x]`; height grid float32 ×1 row-major `[z][x]`.

## Definition of done

- `bake-green.mjs --hole 7` writes v2 `green.json`; `GreenTopology.LoadFromResources(7)` loads with no errors; `TrySampleSlope` + `TrySampleHeight` return sane values inside the polygon, zeros outside.
- Reimport H07: green mesh visibly undulates (upper tier higher, ridge a crisp ramp), ball rests on the surface (no float/clip), collar blends to terrain, no z-fight.
- A putt across H07 breaks consistent with the visible slope.
- `bake_report.txt` shows all H07 arrows inside the polygon, region counts, no NaN.
- Cesar in-engine sign-off on H07 against the PDF panel + ShotNavi heatmap.
- `--all` writes 18; 2-tier holes (3/11/18) render two tiers; no regression on unauthored/flat holes.

---

## Amendment 2026-05-29 — Green terrain pad (Deliverable 4) — **SUPERSEDED by iter-5 amendment below**

> **SUPERSEDED 2026-05-29 14:35 JST.** Diagnosis below was wrong: the protruding surface in `h07_pad_fixed_uphill.png` is the **fairway mesh**, not terrain. Greens already carve the terrain under them (the L2502–2522 carve), just at an insufficient radius. Pad-grading the heightmap was the wrong fix to the wrong problem. The correct fix is in § Amendment 2026-05-29 (iter-5). Block kept for history.

~~**Why:** Hard Rule 2 (no macro-tilt) seats the green flat, but the terrain beneath it keeps its full ~1.8 m DEM tilt and is **not** graded (greens were never in `DepressTerrainUnderOverlays`). The uphill terrain pokes through the flat green.~~

~~**Hard Rule 4 is relaxed:** the importer **may now modify `TerrainData`** to grade a level pad under **height-baked** greens.~~

~~**Deliverable 4 — green pad in `DepressTerrainUnderOverlays`:** flatten terrain cells under the green footprint to `padTargetY = (green interior min vertex Y) − clearance`, falloff through the collar zone.~~

---

## Amendment 2026-05-29 (iter-5) — Cut green+collar footprint from both underlying surfaces (Deliverable 4, REPLACED)

**Authored:** 2026-05-29 14:35 JST (Architect).
**Why:** Cesar rejected iter-4 (the pad fix above) because the actual culprit was wrong. The protruding surface in `h07_pad_fixed_uphill.png` is the **fairway overlay mesh**, not terrain. And on holes where the green sits directly over rough (no fairway), the **terrain** carve is too small — confirmed in code:
- Green terrain hole-carve at L2502–2522 cuts a multiplicative `greenCollarScale × 0.95 = 1.026×` contour.
- Collar mesh is built by **additive dilation** `DilateContour(contour, collarWidth = 0.6 m)` (L2664).
- For a 12 m green that's a cut radius of ~12.3 m vs a collar reach of ~12.6 m → the outer ~0.3 m of the collar ring sits on un-carved terrain.
- And `CreateFairwayMesh` builds the full fairway polygon with **no green cutout**; `yBoost = 0.02 m` was sized for the original terrain-conforming green, ~45× too small for the iter-2 flat-seat green.

Bunkers, for the record, **do** carve terrain — at L2120–2147, the same `holes[hz,hx] = false` mechanism, using a `0.90×` inward contour. We're now using the same mechanism for greens, just with a properly sized cut.

### Hard Rule 4 reverts to original spirit + small permitted extensions

- ❌ **No `TerrainData` heightmap edits.** Revert iter-4's pad pass entirely.
- ✅ Permitted: change the **polygon** passed to the green terrain-hole-carve (already an importer behavior — just a wider contour).
- ✅ Permitted: drop fairway triangles that fall inside the green/bunker cut contour in `CreateFairwayMesh` (additive filter, guarded to height-baked greens only — see §Guards).

### Shared helper — ONE source of truth for the cut contour

```
cutContour(forGreen) = DilateContour(green.contour, collarWidth − cutMargin)
```
with `collarWidth = 0.6 m`, **`cutMargin = 0.25 m`** (sane bounds 0.20–0.30; tune by eye on H07). The cut sits 0.35 m outside the green edge; the collar extends to +0.60 m, so the collar **overhangs the cut by 0.25 m** on every side — safely above terrain `holesRes ≈ 0.3 m/cell` precision. Putting this in one helper prevents the terrain-carve and the fairway-cut from ever drifting apart.

### Deliverable 4a — Widen the green terrain hole-carve

In `CreateGreenMeshes` at L2502–2522 (the existing carve block): replace the local multiplicative `greenCollarScale × 0.95` cut contour with the shared `cutContour` for the green being built. Same `IsInsideContour` test, same `holes[hz, hx] = false` write, same AABB bounds — just a different polygon. Guarded: only widen when the green has a v2 `green.json`; non-v2 greens keep the original `1.026×` cut (no behavior change).

### Deliverable 4b — Cut greens out of the fairway mesh

In `CreateFairwayMesh` (around L4084) — after triangulation, before assigning to the MeshFilter — drop any triangle whose centroid lies inside any green's `cutContour`. Fairway centroid-in-polygon test already exists in the file (`IsInsideContour`, used by `DepressTerrainUnderOverlays`). The green contours are available: greens build at L233 *before* fairways at L240, so the list of `(holeId, contour)` pairs can be passed forward, or read from `greens.json` the same way `CreateFlatZoneMeshes` does at L4049.

### Deliverable 4c — Cut bunkers out of the fairway mesh (same pass)

For each bunker, drop fairway triangles whose centroid is inside `DilateContour(bunker.contour, bunkerCutMargin = 0.20 m)`. Same defect class as greens-in-fairway, smaller absolute scale — hides until a steep enough hillside. Tees and cart paths are already terrain-depressed 0.40 m, so fairway-over-tee has headroom; **defer those unless a visible defect appears.** Water is its own absolute-Y path; skip.

### Reverts to bundle with the fix (iter-4 cleanup)

- `GreenPadRecord` and the green-pad pass added to `DepressTerrainUnderOverlays` in iter-4 → remove.
- `Assets/Golf/Courses/lomond-country-club/Terrain/TerrainData_Hole07.asset` (+ `.meta`) → revert to its pre-iter-4 state.
- Any other iter-4-only field/const introduced for pad grading → remove.

### Guards (unchanged invariants)

- Non-v2 holes: terrain hole-carve uses original `1.026×` polygon; fairway mesh unchanged. Byte-for-byte identical to today.
- Physics: break stays grid-force; `BakedHeightProvider` continues to read mesh vertex Ys; ball rests on mesh.
- Green mesh and collar geometry: unchanged. The fix is entirely about what's **under** the green/collar, not the green itself.

### Updated DoD additions

- H07 reimport: no fairway poke-through on any green edge — capture Cesar's exact bottom-left angle plus the uphill angle plus an overhead.
- Pick one terrain-only green (no fairway overlap — implementer to identify from the 18 holes; e.g. a par-3 island green if Lomond has one, else closest fit) and confirm no terrain poke-through there either.
- Quantitative diagnostic: print to `reimport_report.txt` (a) zero `true` terrain-hole cells inside any green's `cutContour` after the carve; (b) zero fairway triangles whose centroid is inside any green's `cutContour` after the cut.
- Bunkers-in-fairway: visually confirm on a hole with a fairway bunker (implementer to identify) — no fairway-over-bunker poke-through.
- iter-4 pad code and `TerrainData_Hole07.asset` modifications are reverted, confirmed by `git diff` summary in the implementer report.

### Open items the implementer should report back on

1. The actual hole(s) where the green sits directly on rough/terrain (no fairway underneath) — needed to verify the terrain-carve widening works in isolation.
2. The actual hole(s) with fairway bunkers — needed to verify 4c.
3. If `CreateFairwayMesh` triangulates over a larger polygon than just the fairway contour (e.g. dilated for fringe), confirm the centroid-drop still produces a clean edge under the collar overhang. Flag if the edge needs a small post-cut smoothing pass.
