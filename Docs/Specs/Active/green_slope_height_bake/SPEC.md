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

The terrain under the green is already depressed 0.40 m (`DepressTerrainUnderOverlays`) and `BakedHeightProvider` already treats the **mesh vertex Ys** as authoritative — so deforming the mesh gives both the visible undulation and correct ball height for free, with no z-fighting.

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
4. Touch only: `bake-green.mjs` (new), `GreenTopology.cs` (additive v2), `HoleLiteImporter` green-mesh path, and the 18 `green.json` outputs. No other Unity assets, no `greens.json`.
5. Break stays grid-force. No gravity/collider putt changes.
6. `green.json` base64 must match `GreenTopology` byte layout exactly: slope grid float32 ×3 `(dirX,dirZ,magPct)` row-major `[z][x]`; height grid float32 ×1 row-major `[z][x]`.

## Definition of done

- `bake-green.mjs --hole 7` writes v2 `green.json`; `GreenTopology.LoadFromResources(7)` loads with no errors; `TrySampleSlope` + `TrySampleHeight` return sane values inside the polygon, zeros outside.
- Reimport H07: green mesh visibly undulates (upper tier higher, ridge a crisp ramp), ball rests on the surface (no float/clip), collar blends to terrain, no z-fight.
- A putt across H07 breaks consistent with the visible slope.
- `bake_report.txt` shows all H07 arrows inside the polygon, region counts, no NaN.
- Cesar in-engine sign-off on H07 against the PDF panel + ShotNavi heatmap.
- `--all` writes 18; 2-tier holes (3/11/18) render two tiers; no regression on unauthored/flat holes.
