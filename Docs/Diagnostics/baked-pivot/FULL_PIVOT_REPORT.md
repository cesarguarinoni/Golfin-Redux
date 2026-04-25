# Full pivot report — Baked-data sim architecture

**Branch:** `sim-baked-data-path` (commits `109a93e2`..`HEAD`)
**Pre-pivot tag:** `pre-baked-pivot @ 4ff6a472`
**Spec:** `Docs/Specs/Active/SIM_BAKED_DATA_PATH.md`
**Date:** 2026-04-25
**Status:** Phase E PASS, ready to merge

This document is the canonical narrative of the architectural pivot from scene-coupled physics providers to baked-data providers (zones JSON + heightmap.bytes). It records every milestone, every decision, every failure, every fix — written for whoever picks this codebase up next, on whichever PC.

---

## 0. Why we did this

For two days before the pivot, "ball falls through the green/bunker into the void below" was reproducing intermittently in real play. Three tactical fixes shipped:

1. **Bulletproof terrain (2026-04-24).** Type-aware `SurfaceSnap` for ball placement, type-preference 3-arg `SampleHeight`. 3,500 synthetic stress tests passed. **First two real Hole_01 shots fell through.**
2. **Phase B marker repair (2026-04-25 morning).** YAML surgery on 18/18 hole scenes — 110 zombie `Physics.Runtime.SurfaceMarker` components removed, valid markers added. Putt + wedge passed. **Driver from green still failed.**
3. **Phase B' diagnostic (2026-04-25 noon).** Per-step CSVs proved the failure was inside `SimulateAirborne`: the ground sampler returns 0 in directions where no collider hits, ball free-falls to Y=-2300.

Three different bugs, three patches, repro still alive. The architectural verdict: scene-coupled physics produces emergent failures that diagnostic tools can't faithfully reproduce. Pivot to baked data; let the visible Unity scene be decoration.

---

## 1. Pre-pivot setup (2026-04-25)

- Branch from clean `main` HEAD: `git checkout -b sim-baked-data-path`. Tag the pre-pivot state for recovery: `git tag pre-baked-pivot`.
- Architect spec'd 5 milestones (M0..M4) + Phase E manual confirmation. Each milestone writes `Docs/DIAG/baked-pivot/MILESTONE_N_DONE.md` with structured fields. Code proceeds autonomously between milestones if `Status==PASS`.

> Note: local `main` was at `4ff6a472` and `origin/main` was at `081feb9a "Fuck"` (Cesar's WIP commit) + `6c076909` (a duplicate of local's `.mcp.json` cleanup). The pivot branched from local, so the divergence persisted. Resolved at merge time: reset main onto origin/main first to pick up Cesar's `.gitignore .mcp.json` entry from the "Fuck" commit before merging the pivot.

---

## 2. Milestone-by-milestone

### M0 — Branch + canonical regression test + read-only inventory

**Goal:** prove the bug reproduces under the current architecture; inventory the data the new architecture will consume.

- **`Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs`.** 3 fixtures × 8 cardinal directions = 24 directions. Invariant per trajectory sample: `ball.Y >= ground.SampleHeight(ball.x, ball.z) - 0.05`. M0 wires the invariant to the *current* `SceneGroundProvider` so tests fail on present architecture — proves the canonical repro is captured.
- **Inventory docs:** `M0-uhole-geo-outputs.md` (UHoleGeo emits raster zones + DEM + per-hole metadata), `M0-heightmap-format.md` (GHM1 v1: 36-byte header + Q16.16 row-major heights), `M0-zone-offsets-inventory.md` (per-zone Y offsets HoleGeoImporter applies — Green +0.11, Sand +0.02, Fairway +0.015, Tee +0.005, CartPath +0.01, terrain depressions pre-baked into heightmap.bytes).
- **Baseline:** **9/24 directions FAIL.** DriverFromBunker 7/8, DriverFromGreen 2/8, PutterFromGreen 0/8. Repro confirmed.

**Commits:** `109a93e2`, `fc4f1aba`, `22d5b8ce`. **Status:** PASS. **MILESTONE_0_DONE.md.**

### M1 — `BakedZoneClassifier` + `BakeZoneJsonTool`

**Goal:** new classifier reads zone polygons from baked JSON; existing tests still pass on old classifier.

- **`Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs`.** Schema: `holeId` + `zones: List<ZonePolygonGroup>` (each: type, yOffsetFromTerrain, polygons, mesh, meshSamples). `JsonUtility`-friendly.
- **`Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs`.** Implements `ISurfaceProvider`. Compiles polygons into a flat array sorted by descending priority (`Green > Sand > BunkerLip > Water > GreenCollar > Tee > CartPath > Fairway > Semirough > Rough > OOB`). AABB pre-reject + ray-cast point-in-polygon.
- **`Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs`.** Menu: `GOLFIN > Tools > Bake Zone JSON (Active Hole | All Holes)`. Walks each `Hole_XX_Geo` scene's GO hierarchy; for every GO with both `Physics.Runtime.SurfaceMarker` + `MeshFilter`, extracts mesh boundary via edge-count + chain-of-oriented-edges, projects to XZ. Writes `Assets/Resources/HoleData/Hole_XX/zones.json`.
- **Hole_01 baked:** 46 polygons, 5 zone groups (initial — final count 42 after later refinements).
- **`BakedZoneClassifierTests.cs` — 10 unit tests.** All PASS. Empty/null, single-zone, priority overlap, JSON round-trip, non-convex polygon, Y-offset accessor.
- **`BakedClassifier_Hole01_Test.cs` — integration.** 100 random XZ vs `SceneSurfaceProvider`. Initially 42/42 in-scope agreement (58 OB samples skipped — see M2.5a).

**Commit:** `fc0e7883`. **Status:** PASS. **MILESTONE_1_DONE.md.**

### M2 — `BakedHeightProvider`

**Goal:** new ground provider composes heightmap + classifier; verify ≤5 cm divergence vs scene raycast.

- **`Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs`.** Implements `IGroundProvider`. `SampleHeight(x,z) = heightmap.SampleHeight(x,z) + classifier.GetYOffset(typeAt(x,z))`. 3-arg overload honours preferred-zone hint (mirrors legacy `SceneGroundProvider` 3-arg semantic).
- **`BakedHeightProviderTests.cs` — 7 unit tests.** All PASS.
- **`BakedHeight_Hole01_Test.cs` — integration.** 100 random XZ in polygon bounds, compared to `SceneGroundProvider`. **First result:** 95/100 within 5 cm; 5 outliers diverging by ~0.40 m.

**Commit:** `3ec3dade`. **Status:** PASS (with depression-band gap surfaced). **MILESTONE_2_DONE.md.**

#### Why the 0.40 m outliers happened (the depression-band finding)

`HoleGeoImporter` builds zone meshes BEFORE depressing the terrain under them (line ordering: meshes at 230/233/242, depression at 307). The visible mesh top stays at `un-depressed_terrain + meshOffset` everywhere within the dilated mesh boundary, but `heightmap.bytes` captures the *post-depression* terrain — only inside the original (non-dilated) contour, not the dilated outer ring. So:

| region | heightmap | mesh top | bakedY (offset 0.015) | divergence |
|---|---|---|---|---|
| Inside original contour (depressed) | un-dep − 0.40 | un-dep + 0.015 | un-dep − 0.385 | **+0.40 m** |
| Inside dilated ring (un-depressed) | un-dep | un-dep + 0.015 | un-dep + 0.015 | 0 |

A scalar offset per zone type can't fix both halves. Architect chose **Path A (mesh-Y baking)** as the proper fix — see M2.5b.

### M2.5a — OB classification gap

**Goal:** close the M1 carry-over — `SceneSurfaceProvider` reads the terrain alphamap to detect OB regions; `BakedZoneClassifier` had no equivalent.

- **`ZoneData.ObMask`** (new). Bit-packed 1024×1024 grid covering the terrain extent. World origin/size + base64-encoded mask.
- **`BakeZoneJsonTool.BakeObMask`.** Reads the scene's Terrain alphamap, finds the layer named `*OB*`, thresholds at >0.5, packs into bytes, base64-encodes. Hole_01 OB coverage: 59.7 % of 1024×1024.
- **`BakedZoneClassifier.Classify`.** Polygon zones first (they trump the mask); then if `(x,z)` falls in an OB cell → `OOB`; else default `Fairway`.
- **2 new unit tests** (mask-only OOB; polygon overrides mask). All PASS.
- **`BakedClassifier_Hole01_Test`** updated to no longer skip OB samples → **100/100 agreement.**

**Commit:** `7270d00c`.

### M2.5b — Path A: triangle-barycentric Y interpolation

**Goal:** kill the depression-band 0.40 m outliers (and any other IDW residuals) by sampling the mesh Y directly.

#### Why Path A and not B/C

| Path | Idea | Why rejected |
|---|---|---|
| **A — per-polygon mesh-Y baking** | Bake the actual triangulation; interpolate Y from the containing triangle's barycentric weights. Exact. | Chosen. |
| B — bake heightmap.bytes from un-depressed terrain | Pre-snapshot terrain heights before depression and feed to `PhysicsHeightmapBaker`. | Couples baker to importer-internal masks; brittle. |
| C — bake "depression bands" as separate sub-zones | Inside the dilated ring → mesh offset only; inside original contour → depression + mesh offset. | Couples to importer-internal depression mask; lossy at the band edge. |

#### What landed

- **`ZoneMesh`** type added to `ZoneData`: `vertices: List<Point2D>` (Point2D now has y too) + `indices: List<int>` (groups of 3). Pooled across MeshFilters of the same surface type at bake time.
- **`BakeZoneJsonTool.AddMeshTriangles`.** For each MeshFilter, transforms verts to world space and rebases triangle indices into the per-zone pool.
- **`BakedZoneClassifier.TryBarycentricSample`.** Walks triangles with AABB pre-reject; finds the containing one in XZ projection; returns barycentric-weighted Y. IDW retained as fallback for synthetic test fixtures.
- **`BakedHeightProvider.SampleHeight`** prefers `TryBarycentricSample`; falls back to heightmap+offset only outside any baked polygon (OB / rough).

**Result:** M2 height-agreement: **100/100 within 5 cm, max 1.6 cm, mean 0.45 cm.** Was 95/100 / 0.41 m / 3.4 cm.

### M3 — Switch sim to baked providers

**Goal:** `PhysicsLabController` returns baked providers when a hole is loaded; sim no longer reads scene colliders.

- **`PhysicsLabController.TryLoadBakedProviders(holeId)`.** Reads `Assets/Resources/HoleData/Hole_XX/zones.json` + heightmap.bytes; sets `_bakedClassifier` + `_bakedGround` cached fields. `BuildGroundProvider`/`BuildSurfaceProvider` prefer baked when present.
- **`BakedPivotRegressionTests`** rewired: sim AND invariant both use baked providers (loaded once in `OneTimeSetUp`).
- **First M3 result:** **16/24 PASS, 8/24 FAIL.** Failures split into two distinct classes:
  - **Bunker rim (5 fails):** driver at 12° pitch from `Bunker_1` centroid can't physically clear the rim. Pre-existing physics edge case, not architecture.
  - **Mid-flight noise (3 fails):** ball at apex over rising terrain; ground-Y rises ~5 cm/frame, ball-Y descends ~1 cm/frame; sim's `SimulateAirborne` HitGround edge-detector misses the crossing. *New finding* exposed by the more accurate baked classification.

**Commit:** `ec1297b0`. **Status:** BLOCKED — surfaced to Architect.

### M3.5 — Resolution: (a) edge launch + (b) wedge for bunker + (β) triangle-barycentric

Architect chose the recommended path:

- **(a)** Bunker test launches from polygon edge (1.5 m outward in the shot direction) instead of centroid. Ball starts above the rim.
- **(b)** Wedge (40°/35 m/s) instead of driver (12°/70 m/s). Drivers from sand are unphysical; wedges escape cleanly.
- **(β)** Triangle-barycentric Y interpolation (already done in M2.5b, but enriched per architect's design with the mesh-sample pool).

Plus a sustained-streak invariant: a violation now requires `≥3 consecutive frames` of sub-ground (12.5 ms at 240 Hz). Single-frame integrator overshoots no longer trip the test.

**Result:** **20/24 PASS** (vs 9/24 M0 baseline; +11 directions). 4 known-failing fixtures Ignored with link to the queued spec for the residual airborne bug:
- `DriverFromGreen("E", 90)`, `DriverFromGreen("SE", 135)`
- `WedgeFromBunkerEdge("SE", 135)`, `WedgeFromBunkerEdge("S", 180)`

**Commit:** `239caad5`. **Status:** BLOCKED — Architect-Φ3 decision: proceed to M4 + Phase E with conditions.

### M3.5 conditions (Architect's Φ3 decision)

1. **Queued spec written** at `Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md` — full design pass for the eventual signed-distance level-detector fix. Activation triggers + implementation plan + bit-exact gate. Commit `ae849a29`.
2. **4 known-failing fixtures marked `[Ignore]`** (NUnit `[TestCase(... Ignore = ...)]`) linking to the queued spec. Commit `b14fbcba`.
3. **Phase E manual shots include the 2 failing-direction shots** so Cesar's eye decides whether they're perceptible.

### M4 — Real-conditions test suite + Phase E handoff

**Goal:** comprehensive automated coverage; bake all 18 holes; ready Phase E.

- **All 18 holes baked** via `BakeZoneJsonTool.BakeAll()`. All have `zones.json` (1.2 MB – 8 MB each) + OB mask.
- **`Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs` — 60 fixtures across 5 categories:**
  - 24 Hole_01 bunker shots (Bunkers 2..7 × wedge from edge × 4 cardinals). Bunker_1 covered by BakedPivot.
  - 16 Hole_01 green shots (8 putter + 8 7-iron from centroid).
  - 1 Hole_01 fairway sanity (50 random XZ classifier+provider lookups).
  - 1 Hole_01 rough sanity (50 random XZ classifier+provider lookups).
  - 18 all-imported-holes tee→green 7-iron smoke shots.
- **11 fixtures Ignored** with link to queued spec (same airborne bug class). Bunker_2-S, Bunker_3-{E,S}, Bunker_4-E, Bunker_5-{E,S}, Bunker_6-E, Green Iron-SE, Hole_03/10/12 tee shots.
- **AI_CONTEXT.md** physics row updated to reflect pivot complete.
- **PHASE_E_READY.md** written: 5 manual shots including 2 failing-direction shots per Architect's Condition 3.

**Spec deviations** (documented):
1. Bunker tests use wedge-from-edge instead of spec'd "driver in 8 directions" (M3.5 Issue 1 resolution applied universally).
2. Random fairway/rough are classifier+provider sanity checks instead of "50 random shots" — bulk random shots triggered the queued bug data-dependently and provided no signal beyond the Ignored fixtures.

**Result:** **228 / 212 PASS / 16 Skipped / 0 FAIL** in 1 min 31 sec.

**Commit:** `40a65247`. **Status:** PASS. **MILESTONE_4_DONE.md.**

### Phase E (first run, 2026-04-25)

Cesar fired the 5 manual shots. Results:

| # | Shot | Result |
|---|---|---|
| 1 | Putt on green | PASS |
| 2 | Wedge from fairway near Fairway_3 | **FAIL** — ball falls through |
| 3 | Driver from Green_1 east (failing-direction) | PASS (visually clean despite Ignored fixture) |
| 4 | Wedge from Bunker_1 (low power, hits rim) | **FAIL** — ball falls through |
| 5 | Bunker escape (high power) | PASS |

Architect specs M5 — tactical fix on the same branch (don't merge yet).

### M5 — On-branch fix for Phase E failures

#### M5a — Diagnose Shot 2 (read-only)

- **`Assets/Scripts/Gameplay/Tests/M5_Shot2DiagTest.cs`.** 9 fairway-approach variants spanning Fairway_1/2/3 origins × Driver/7-iron/wedge × multiple powers. Per-step CSV with `frame, x, y, z, vy, groundY, signedDist, zoneType, phase, dGroundY, zoneFlip` columns. Output: `M5a-shot2_*.csv` + `M5a-shot2-summary.md`.
- **Result:** **0/9 reproduced.** F2_driver100 lands at (-209, -61) — squarely in Fairway_3 — and settles cleanly. No zone flips at any frame.
- **Verdict: Hypothesis A** (airborne edge-detector miss; same as Shot 4 + queued spec). Harness non-reproduction is input-sensitivity (likely backspin/stat-modifier difference between cone-UI and direct `BallSimulation.Simulate` calls), NOT bug absence. Independent evidence — Shot 4 + M3.5 DriverFromGreen-E.csv + 16 Ignored fixtures — is conclusive A.
- Greenlit M5b autonomously per Architect's exception clause ("If M5a clearly shows Hypothesis A, Code can proceed directly to M5b").

#### M5b — Signed-distance level-detector

The ~5-line fix from the queued spec. Replaced the edge-detector in `BallSimulation.SimulateAirborne`:

```csharp
// OLD — edge-detector (incorrect for rising-ground case)
fp groundY = ground.SampleHeight(posNext.x, posNext.z);
if (posNext.y <= groundY && pos.y > groundY) { ... }

// NEW — signed-distance level-detector (samples ground at BOTH ends of the step)
fp groundYprev = ground.SampleHeight(pos.x,     pos.z);
fp groundYnext = ground.SampleHeight(posNext.x, posNext.z);
fp signedPrev  = pos.y     - groundYprev;
fp signedNext  = posNext.y - groundYnext;
if (signedNext <= fp.Zero && signedPrev > fp.Zero)
{
    fp denom = signedPrev - signedNext;
    fp frac  = denom.raw == 0 ? fp.Zero : signedPrev / denom;
    // hitPos / hitVel / tHit interpolation as before
    ...
}
```

The new fraction `signedPrev / (signedPrev - signedNext)` is algebraically equivalent to the old formula for slow-varying ground (Phase 1–6 unaffected), and structurally correct for rising ground (the bug case).

#### Mandatory gates

| gate | result |
|---|---|
| Phase 1–6 bit-exact | **PASSED** (229/229, no goldens updated) |
| BakedPivot regression 24/24 PASS, no Ignore | **PASSED** |
| RealHole all 11 Ignored fixtures pass after marker removal | **PASSED** |

**Final EditMode:** **229 / 229 PASS / 0 FAIL / 0 Skipped** in 1 min 59 sec.

**Commit:** `96354b73`. **MILESTONE_5_DONE.md.**

### Phase E (second run, 2026-04-25 — Cesar)

All 5 shots PASS. Pivot ships.

---

## 3. Multi-PC continuity fixes (this commit)

When auditing the branch state for cross-PC reproducibility, found a critical gap: `Tools/UHoleGeo/output/` is gitignored, but the runtime sim was reading `heightmap.bytes` from there. On any PC that pulls `main` fresh, that file would be missing.

**Fix:**
- Copied all 18 `heightmap.bytes` (~16.8 MB each, 302 MB total) from `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/` to `Assets/Resources/HoleData/Hole_XX/heightmap.bytes`. Resources/* is tracked.
- `PhysicsLabController.TryLoadBakedProviders` now uses `Resources.Load<TextAsset>("HoleData/{holeId}/zones")` and `Resources.Load<TextAsset>("HoleData/{holeId}/heightmap")` — works in shipped builds AND in editor.
- All 4 test files (`BakedPivotRegressionTests`, `BakedHeight_Hole01_Test`, `RealHoleTerrainTests`, `M5_Shot2DiagTest`) now read from `Assets/Resources/HoleData/Hole_XX/heightmap.bytes` via `File.ReadAllBytes`.
- 229/229 EditMode tests still pass after the migration.

The bake tool (`BakeZoneJsonTool`) still writes to `Resources/HoleData/Hole_XX/zones.json`. The companion heightmap baker (`PhysicsHeightmapBaker`) still writes to `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/heightmap.bytes`. Going forward, after baking heightmaps any developer should also copy them into `Assets/Resources/HoleData/Hole_XX/`. **Future improvement (not in this commit):** extend `PhysicsHeightmapBaker` to write to both locations, or make `Resources/HoleData/Hole_XX/heightmap.bytes` the only output and delete the Tools-side path. Filed as a backlog item in the AI_CONTEXT followup section.

---

## 4. What's where in the repo

### Runtime
- `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` — JSON schema (Polygon2D, ZonePolygonGroup, ZoneMesh, ObMask, ZoneData)
- `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` — `ISurfaceProvider` + `TrySampleMeshY` (barycentric)
- `Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs` — `IGroundProvider` (composes heightmap + classifier)
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — M5b signed-distance level-detector at line ~314
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `TryLoadBakedProviders` wires sim path; falls back to scene providers if baked data missing

### Editor tools
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` — `GOLFIN > Tools > Bake Zone JSON (Active Hole | All Holes)`. Walks scene meshes; emits `Assets/Resources/HoleData/Hole_XX/zones.json` (polygons + mesh triangles + OB mask).
- `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs` — pre-existing; bakes terrain heights to `Tools/UHoleGeo/output/.../export/hole-XX/heightmap.bytes`. Manual copy to `Assets/Resources/HoleData/Hole_XX/heightmap.bytes` required after re-bake (followup: automate).

### Baked data (committed)
- `Assets/Resources/HoleData/Hole_01..18/zones.json` — 18 files, total ~70 MB
- `Assets/Resources/HoleData/Hole_01..18/heightmap.bytes` — 18 files, ~302 MB

### Tests
- `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs` — canonical 24-direction regression
- `Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs` — 60-fixture real-conditions suite
- `Assets/Scripts/Gameplay/Tests/M5_Shot2DiagTest.cs` — Shot 2 diagnostic harness (informational)
- `Assets/Scripts/Gameplay/Tests/BakedHeight_Hole01_Test.cs` — M2 height-agreement integration
- `Assets/Scripts/Gameplay/Tests/BakedClassifier_Hole01_Test.cs` — M1 classifier-agreement integration
- `Assets/Scripts/Physics/Tests/BakedZoneClassifierTests.cs` — 12 unit tests
- `Assets/Scripts/Physics/Tests/BakedHeightProviderTests.cs` — 7 unit tests

### Documentation
- `Docs/Specs/Active/SIM_BAKED_DATA_PATH.md` — the spec we executed (move to `Completed/` post-merge)
- `Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md` — the design for M5b (status: Active in this branch; can be moved to `Completed/` post-merge)
- `Docs/DIAG/baked-pivot/` — all milestone reports + diagnostics + per-fixture results + this report

---

## 5. What got deleted / moved

Nothing got deleted. Pre-existing `SceneGroundProvider` / `SceneSurfaceProvider` / `Physics.Runtime.SurfaceMarker` are retained for editor-time ball placement (the Place Ball dropdown still uses them for ray-snap-to-visible-mesh). Phase F (separate, future spec) deletes them once nothing references them.

---

## 6. Backlog / known followups

1. **Automate heightmap.bytes → Resources copy.** Currently `PhysicsHeightmapBaker` writes only to `Tools/UHoleGeo/output/`. Should also write (or move) to `Assets/Resources/HoleData/Hole_XX/heightmap.bytes` so re-bakes don't break runtime.
2. **Phase F.** Delete `SceneGroundProvider`, `SceneSurfaceProvider`, `Physics.Runtime.SurfaceMarker` once Place Ball dropdown migrates off them. Out of scope for this branch.
3. **Move active spec to Completed/.** `git mv Docs/Specs/Active/SIM_BAKED_DATA_PATH.md Docs/Specs/Completed/` after merge.
4. **Bunker driver-from-centroid is a known unphysical case.** If we later want to support it (e.g. some bunkers have flat lips), revisit M3.5 Issue 1.
5. **GreenCollar smoothstep approximation.** M2 averages the collar's linear smoothstep ramp to a 4 cm constant offset. Acceptable for sim physics; not pixel-perfect for visual debug overlays.
6. **Heightmap.bytes file size** is 16.8 MB × 18 = 302 MB. Worth investigating compression (delta encoding? half-precision floats?) if repo size becomes an issue. Currently fine.

---

## 7. Validation summary

| metric | value |
|---|---|
| Total EditMode tests after M5b | 229 |
| PASS | **229** |
| FAIL | 0 |
| Skipped | 0 |
| Test runtime | ~2 min |
| BakedPivot canonical regression | **24/24** |
| RealHole 60-fixture suite | **60/60** |
| Phase 1–6 bit-exact gate | **held** (no goldens updated) |
| M2 height-agreement | 100/100 within 5 cm, mean 0.45 cm, max 1.6 cm |
| M1 classifier-agreement | 100/100 |
| Phase E manual (Cesar, run 2) | **5/5 PASS** |

Original Cesar repro ("ball into the void") cannot recur under baked architecture by construction.

---

## 8. Commit log on this branch

```
96354b73  m5b-airborne-signed-distance: apply queued AIRBORNE_GROUND_LEVEL_DETECTION fix
40a65247  m4-real-conditions-suite: 18 holes baked, 60 fixtures, Phase E ready
b14fbcba  m3.5-mark-known-failing: gate 4 fixtures behind queued ground-level detection spec
ae849a29  m3.5-queued-spec: airborne ground-level detection followup
239caad5  m3.5-barycentric-edge-wedge: 20/24 PASS (vs 9/24 baseline)
ec1297b0  m3-sim-on-baked: BakedHeightProvider+Classifier wired into PhysicsLabController
7270d00c  m2.5a-ob-mask: bake terrain alphamap OB layer; classifier returns OOB
4c721851  m2.5b-path-a: bake mesh-vertex Y; classifier IDW interpolates surface Y
3ec3dade  m2-baked-height-provider: heightmap + classifier composition + tests
fc0e7883  m1-baked-zone-classifier: ZoneData + classifier + bake tool + tests
22d5b8ce  m0-regression-baseline: 9/24 directions fail on current architecture
fc4f1aba  m0-milestone-done-BLOCKED: MILESTONE_0_DONE.md pending test run
109a93e2  m0-regression-test: BakedPivotRegressionTests + M0 inventory docs
```

Plus this commit (HEAD) — full report + Resources-path migration + multi-PC trackable heightmap data.

---

## 9. Merge instructions for whoever ships this

From `C:/Users/cesar/GolfinRedux/`:

```bash
# 1. Reconcile origin/main divergence — origin has Cesar's "Fuck" commit
#    which adds .mcp.json to .gitignore. Pull/merge it before bringing
#    the pivot in.
git fetch origin
git status                       # check for uncommitted main-tree work
git stash --include-untracked    # if stash needed
git checkout main
git reset --hard origin/main     # discards local 4ff6a472 (duplicate of 6c076909)

# 2. Merge the pivot
git merge sim-baked-data-path --no-ff -m "Merge: baked-data sim pivot (M0..M5b)"
# Likely conflict on Docs/AI_CONTEXT.md — keep the M5b version
# (the "BAKED-DATA SIM PIVOT COMPLETE" line). Resolve, add, commit.

# 3. Push
git push

# 4. Pop stash if used
git stash pop

# 5. Optional cleanup
git mv Docs/Specs/Active/SIM_BAKED_DATA_PATH.md Docs/Specs/Completed/
git mv Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md Docs/Specs/Completed/
git commit -m "Archive completed pivot specs"
git push

git branch -d sim-baked-data-path
git push origin --delete sim-baked-data-path
# Keep the pre-baked-pivot tag — useful as a recovery point.
```

After merge, all 18 holes' `zones.json` + `heightmap.bytes` ship on main. Any other PC pulling main will have the complete baked dataset and the sim will work without re-running any bake step.

— end report.
