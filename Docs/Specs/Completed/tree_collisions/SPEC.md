# SPEC — `tree_collisions`

**Notion:** Order 348 (P2, Gameplay Polish) · **Phase 2 (deferred):** tree-aware bot (Order 351)
**Tier:** FULL PIPELINE (deterministic spatial math in the sim core + importer-pipeline bake + editor save hook)
**Prepared:** 2026-06-11 (Architect; design Cesar-approved 2026-06-11)

---

## 1. Why

Trees have zero collision today — the ball flies straight through trunks and canopies on every hole. Trees exist via THREE placement paths in TWO storage types (verified):

| Source | Storage | Tool |
|---|---|---|
| Importer terrain trees | `TerrainData.treeInstances` | `TreePlacer.cs` (prefabs WITH LODGroup) |
| Importer standalone trees | GameObjects under `StandaloneTrees` container | `TreePlacer.cs` (prefabs without LODGroup / forced) |
| Brush-painted trees | Both — terrain instances + GameObjects under `PaintedTrees` container | `TreeBrushTool.cs` |

The ball is NOT a Unity rigidbody — flight/bounce/roll/putt all run inside the deterministic fp sim (`BallSimulation`). Unity colliders on tree prefabs (82 of 594 in `Trees2025_Prefabs` have them) are irrelevant to ball physics. **The unified solution is a bake:** harvest all tree positions from all three sources into per-hole data the sim consumes — placement source stops mattering at runtime.

## 2. Locked design (Cesar-approved 2026-06-11)

- **D1 — Two-cylinder tree model.** Per tree: a **trunk** cylinder (ground → trunkHeight, radius trunkRadius) and a **canopy** cylinder (trunkHeight → canopyTop, radius canopyRadius). Vertical axes, fp math, deterministic.
- **D2 — Trunk = hard collision (Layer 1 style).** Segment crosses trunk wall → reflect velocity about the cylinder's outward XZ normal at the hit point, scale by `trunkRestitution` (CSV, low — ball drops nearly dead). Same within-step interpolation pattern as the M5b ground level-detector.
- **D3 (REVISED 2026-06-12, Cesar playtest rejection of v1) — Canopy = discrete leaf-strike impulse (Layer 2 overlay, documented).** When a step segment **enters** the canopy cylinder (`!IsInsideCanopy(p0) && IsInsideCanopy(p1)`), scale velocity ONCE by `canopyHitDamping` (CSV, default **0.40**). **No per-step force while inside** — after the cut, normal ballistics (gravity, drag, magnus) resume immediately and the ball falls out of the tree at natural speed. No deflection, no RNG — deterministic. Each fresh entry crossing (re-entry, second tree) fires its own cut.
  - **Why v1 was rejected:** v1's `canopyDampingPerStep = 0.92` per RK4 step = exponential decay at sim rate → velocity collapses to ~13% in ~0.1s, gravity gain is damped too, terminal creep ≈ 0.5 m/s → the ball drifts down through the canopy band for 10+ seconds and exits at near-zero speed. Reads as slow-motion all the way to the ground. Continuous drag is the wrong model for a projectile; the industry pattern (PGA-style sims, arcade golf) is a **discrete impulse at contact**, then normal physics.
- **D4 — ALL sim phases.** The tree test runs in airborne RK4 stepping, every bounce arc (free — each bounce re-enters `SimulateAirborne`), `RunRollPhase`, and `RunPuttPhase` (trunk-only in roll/putt; canopy is above a rolling ball by construction). All four phases already step `pos → posNext`, so ONE shared segment-vs-tree helper covers everything.
- **D5 — Auto re-bake on scene save.** If a hole's tree state changed since the last bake, saving the scene triggers a re-bake automatically — or at minimum a prompt ("Trees changed since last bake — re-bake now?"). Silent staleness is NOT acceptable.
- **D6 — Bot stays tree-blind in this spec.** Tree-aware bot targeting is **Phase 2 (Order 351)**, layered on H2 later. The existing recovery handles a ball dropped short by a tree.
- **D7 — Dormant packs untouched.** `Assets/Packs/*`, `Realistic Tree`, etc. are unused by the tools and unreferenced in Lomond scenes (name-grep verified); leave them.

## 3. Data (CSV-first)

### 3a. `Assets/Resources/Data/tree_collision_profiles.csv` (+meta) — per prefab TYPE

```csv
# One row per tree prefab used in placement; `default` row is the fallback for any unprofiled prefab.
# Heights/radii in meters at scale=1.0; instance scale multiplies all four.
prefabName,trunkRadius,trunkHeight,canopyRadius,canopyTop,trunkRestitution,canopyHitDamping
default,0.25,3.0,3.0,9.0,0.15,0.40
MESH_01Cedar,0.30,4.0,2.5,12.0,0.15,0.40
MESH_JapaneseBlack_01,0.35,3.5,3.5,10.0,0.15,0.40
MESH_JapaneseBlack_01_Var1,0.35,3.5,3.5,10.0,0.15,0.40
Mesh_Metasequoia,0.30,4.5,2.0,13.0,0.15,0.40
```

> NOTE: starting values are Architect estimates — the implementer SHOULD sanity-check radii/heights against actual prefab bounds (renderer bounds at scale 1) in the bake harness and flag big mismatches in the report. Rows needed only for prefabs that actually appear in baked scenes (the `TreePlacer.DefaultWeights` set + whatever the brush placed); everything else falls back to `default`.

### 3b. `tree_obstacles.csv` per hole — baked instances

Lives beside the hole's existing baked data: `Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/tree_obstacles.csv` (+meta). Columns: `worldX,worldZ,baseY,scale,profileName`. Plus a header comment line carrying the **bake hash** (see §5).

## 4. Sim integration

### 4a. `TreeObstacleProvider` (new, `Golfin.Physics.Core` or `Runtime` — match where `HeightmapData`/`HeightmapLoader` split)

- Loads a hole's `tree_obstacles.csv` (loader mirrors `HeightmapLoader.cs` pattern, `Physics/Runtime/`).
- Builds an **XZ spatial grid** (cell ≈ max canopy diameter) over fp positions for O(neighbors) per-step lookup.
- API sketch: `bool TestSegment(fp3 p0, fp3 p1, out TreeHit hit)` where `TreeHit { fp frac; fp3 hitPos; fp3 normalXZ; bool isTrunk; profile }` — trunk returns the earliest crossing; canopy reports an **entry crossing** (p0 outside, p1 inside) for the one-time impulse. Trunk still outranks canopy (two-pass, unchanged from iter-4).
- **Null/absent CSV → no trees, zero behavior change** (logged once). Solo, Practice, and un-rebaked holes keep working.

### 4b. `BallSimulation` changes

- New optional parameter threaded like `surfaces`/`wind`: `ITreeObstacleProvider trees = null` (or a static-injection seam consistent with how `surfaces` reaches the sim — implementer matches the existing pattern; flag which).
- In each stepping loop, after `posNext` is computed and BEFORE the ground-crossing check:
  - **Trunk crossing** → interpolate to hit (M5b pattern), reflect XZ velocity about `normalXZ`, apply `trunkRestitution`, add a `TerrainHit`-equivalent sample, continue stepping from the hit.
  - **Canopy entry crossing** (airborne only) → `vel *= canopyHitDamping` ONCE at the step where the segment enters the canopy volume. No force while remaining inside; no cut on exit. (D3 revised — v1 per-step damping rejected by Cesar, see D3.)
- Determinism: all tests in fp; iteration order over grid candidates must be stable (sort by index).
- `Trajectory`/diag: log tree hits via the existing `DiagShotLogger` channel (`[TreeHit] trunk/canopy ...`).

> NOTE (ordering): trunk check before ground check within a step; if both would trigger in the same step, earliest `frac` wins. Implementer documents the chosen resolution.

## 5. Bake pipeline

### 5a. Bake harness (editor-only, `Assets/Scripts/Editor/CourseImporter/TreeObstacleBaker.cs`)

Per open hole scene:
1. Harvest `terrain.terrainData.treeInstances` → world pos (normalized→world via terrain pos/size), prototype index → prefab name → profile, `widthScale` as scale.
2. Harvest children of `StandaloneTrees` and `PaintedTrees` containers → transform pos/scale, prefab name (strip `_{n}` suffix — `TreePlacer` names instances `{prefabName}_{count}`).
3. Emit `tree_obstacles.csv` to the hole's `Data/hole-NN-geo/` folder with a **bake hash** header = hash over (count + all positions + scales + names) of the harvested set.
4. Menu item under the existing course-importer menu group.

### 5b. Staleness guard (D5)

`EditorSceneManager.sceneSaving` hook (editor asmdef): when a `Hole_NN_Geo` scene saves, recompute the harvest hash and compare to the CSV header hash. On mismatch → **auto re-bake** (default) with a single log line; if the bake errors, fall back to a modal prompt. Also: `TreePlacer` full re-place and `TreeBrushTool` paint/clear should invalidate eagerly (cheap: they already touch the scene; the save hook catches everything, so explicit invalidation is optional — implementer's call, document it).

## 6. Code anchors (verified 2026-06-11)

| Need | Anchor |
|---|---|
| Stepping loops | `BallSimulation.SimulateAirborne` (`Physics/Core/BallSimulation.cs:336`, RK4, `pos→posNext` + M5b crossing at ~:400); bounce loop re-enters `SimulateAirborne` (:293); `RunRollPhase` (:455) and `RunPuttPhase` (:614) both step `posNext` (:522, :680) |
| Within-step interpolation pattern | M5b signed-distance detector, `BallSimulation.cs` ~:400–420 |
| Per-hole data load pattern | `Physics/Runtime/HeightmapLoader.cs`; provider split `HeightmapData` (Core) |
| fp math | `Golfin.Physics.Math` (`fp`, `fp3`, `fpMath`) |
| Tree placement (terrain + standalone) | `Editor/CourseImporter/TreePlacer.cs` — `StandaloneContainerName="StandaloneTrees"` (:138), instance naming `{name}_{n}` (:767), `SetTreeInstances` (:787) |
| Brush placement | `Editor/CourseImporter/TreeBrushTool.cs` — `PaintedTrees` container (:45), mixed GO/terrain (:403–510) |
| Prefab source of truth | `TreePlacer.TreePrefabFolders` = `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs`; enabled set in `DefaultWeights` |
| Diag channel | `BallSimulation.DiagShotLogger` (:29) |

## 7. Out of scope

- **Tree-aware bot** (Phase 2, Order 351): flight-path tree probe + retarget in `VersusBot` H2.
- Tree-hit VFX/SFX (leaf rustle, thwack) — pairs with `water_splash_fx`/`sound_effects`.
- Deflection in canopy (impulse-only locked, D3 revised). Non-cylindrical canopy shapes. Continuous in-canopy drag of any strength (explicitly rejected).
- Dormant pack cleanup (D7). Taiheiyo bake (course still importing; baker must WORK for any `Hole_NN_Geo` scene, but Taiheiyo holes are baked when that course lands).

## 8. Acceptance checklist (implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] `tree_collision_profiles.csv` ships; bake harness emits `tree_obstacles.csv` for ALL 18 Lomond holes; per-hole counts reported and cross-checked against scene harvest (terrain + StandaloneTrees + PaintedTrees all included — prove with one hole's breakdown).
- [ ] Trunk: a shot aimed at a trunk reflects and drops nearly dead (video); deterministic — same ShotInput twice → identical Trajectory (assert in an EditMode test with a synthetic tree).
- [ ] Canopy: a shot entering a canopy gets ONE velocity cut at entry and lands short vs. the same shot with trees disabled (paired video or trajectory diff in the report).
- [ ] **No slow-motion descent (regression for v1 rejection):** after the entry cut, the ball's fall through and below the canopy band is normal free-fall — EditMode test asserts descent time from canopy entry to ground is within a small factor (≤1.5×) of the same fall with trees disabled, AND that the impulse fires exactly once for a single pass (sample velocity ratio across the entry step ≈ canopyHitDamping; subsequent in-canopy steps unscaled).
- [ ] Roll/putt phase: a rolling ball into a trunk deflects/stops (test or video).
- [ ] Absent `tree_obstacles.csv` → byte-identical sim behavior to today (regression: existing physics EditMode suite green, zero new failures).
- [ ] Save hook: edit trees in a hole, save scene → auto re-bake fires (log line) and CSV hash updates; no re-bake when nothing changed.
- [ ] No change to `VersusBot`, HUD, RP, UI. Diff confined to sim core/runtime additions, the baker, the save hook, CSVs.
- [ ] Performance note: per-step grid lookup cost measured (the sim is batch; a full drive must not regress noticeably — report Simulate() wall time before/after on a tree-dense hole).

## 9. Visual gate

Bot-recorded full-size (1170×2532) videos: (a) trunk strike — ball deflects and drops, (b) canopy strike — ball gets swatted at leaf contact then drops out at NATURAL falling speed (no slow-mo), lands short, (c) control clip of the same shot with trees disabled for contrast. Plus the save-hook demo can be a screenshot of the console log line.

## 10. Tier & kickoff

**FULL PIPELINE** — deterministic spatial math inside the sim core, importer-pipeline bake, editor save hook, regression-sensitive.

Kickoff (Cesar pastes into Claude Code):
```
Use the implementer subagent on "tree_collisions"
```
