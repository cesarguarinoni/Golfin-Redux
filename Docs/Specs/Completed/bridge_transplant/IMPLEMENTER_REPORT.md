# IMPLEMENTER_REPORT — `bridge_transplant`

**Iteration shape:** `course-scene:bridge-transplant-and-deck-zone`
**Scope, iteration 4:** all four remaining holes batched — **8, 9, 12 and 17**. All 7 bridges from
the SPEC's ground-truth table are now transplanted, decked, zoned and obstacle-baked. See
§ Iteration 4 below; it found two more baker defects that only the x-tilt could reach, and a third
model whose colliders do not seal its own deck.

**Scope, iteration 3:** Cesar's call on the two open decisions — *"Add bots + bridges, author
railing boxes, explain hole 12, re-run tree drift and then commit if everything checks out."*
`VersusBot.IsAvoidSurface` now includes `Bridge` (the one authorised edit to the bot), and
`bridgeLODs.fbx` gets real railing/pier collision. Still hole 7 only for baked output; holes
8/9/12/17 are not transplanted.

**Scope, iteration 2:** SPEC **Stage A + Stage B + Stage C on HOLE 7 ONLY**. Iteration 1 was
A+B; Cesar then asked to *"make sure all collisions work correctly for any transplanted trees and
the bridge, and that they are all baked for the bots to take them into account"*, which required
Stage C (railings and piers), because A+B gave the deck a surface but left the railings and piers
with **no collision at all**.

**Original scope, iteration 1:** SPEC **Stage A + Stage B on HOLE 7 ONLY**, per Cesar's kickoff
("Do Stage A+B on HOLE 7 ONLY and stop for review before batching holes 8/9/12/17") and the SPEC's
own gate ("Stage A + B on hole 7 alone is a complete, verifiable increment — get that reviewed
before batching the other four holes"). **Stage C (BridgeObstacleData / Provider / Loader / Baker,
BallSimulation wiring, PhysicsLabController), Stage D verification, and holes 8/9/12/17 are NOT in
this iteration.**

Baseline: `HEARTBEAT.log` iter-1 block (HEAD `cb48171d5`, DIRTY porcelain, and the md5 of every
protected file *before* any work).

Canonical screenshot: `screenshots/hole07_bridge_collision_crosssection.png` (1500×860)
Supporting: `screenshots/hole07_bridge_deck_zone_overlay.png`,
`screenshots/hole07_bridge_collision_volumes.png` (both 1600×900)

---

## What shipped

| # | Thing | Where |
|---|---|---|
| A | Transplant tool — copies `^[Bb]ridge` scene-root objects from the Video scene into the live scene under a scene-root `Bridges` container, **preserving the full property-modification set** | `Assets/Scripts/Editor/CourseImporter/BridgeTransplantTool.cs` |
| B7 | Deck-mesh generator — per bridge, a `Deck_Collision` child (MeshFilter + `SurfaceMarker(Bridge)`, **no MeshRenderer**) over a saved mesh asset | same file |
| — | **Tracked instance catalog** (NOT in the SPEC — see Deviation 2) | `Assets/Scripts/Editor/CourseImporter/BridgeInstanceCatalog.cs` |
| B1 | `SurfaceType.Bridge = 11` | `Assets/Scripts/Physics/Core/SurfaceType.cs` |
| B2 | `SurfaceCoefficients[11]` → `[12]` + Bridge row | `Assets/Scripts/Physics/Core/SurfaceConfig.cs` |
| B3 | Bridge putt row | `Assets/Scripts/Physics/Core/PuttConfig.cs` |
| B4 | `Priority(Bridge) = 95` | `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` |
| B5 | `YOffsets[Bridge] = 0f` | `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` |
| — | Stage-B tests | `Assets/Scripts/Physics/Tests/BridgeCollisionTests.cs` |

Baked outputs for hole 7: `Resources/HoleData/lomond-country-club/Hole_07/zones.json` (one new
`Bridge` group), `Data/hole-07-geo/MESH_BridgeDeck_Hole07_00_Bridge_withLODs.asset`,
`Data/hole-07-geo/bridge_instances.json`.

---

## Acceptance checklist

Iteration 2 closed the Stage C rows. The remaining open item is the bot A/B, which did not come
out the way the SPEC predicted — recorded as a finding, not as a pass.

- [x] **PASS** — *All 7 bridges present at the exact world TRS.* Hole 7's one instance only (scope).
  Tool output: `'Bridge_withLODs' pos=(75.7200, 23.2000, -53.1100) rot=(0, 0.7301623, 0, 0.6832738)
  eulerHint=(0.00, 93.80, 0.00) lossyScale=(1.0000, 1.0000, 1.3700)` — matches the SPEC ground-truth
  row and the Video-scene YAML quaternion verbatim. The gate is not that log line but the
  hierarchy diff the tool also prints: **`110 transform(s) compared, 0 unmatched; max |Δpos|=0.000000 m,
  max Δangle=0.000000°, max |Δscale|=0.000000`** against the live Video-scene source.
- [x] **PASS** — *`Bridges` is a scene-root GameObject; nothing bridge-related under `StandaloneTrees` /
  `PaintedTrees`.* Live scene has 4 roots (`HoleRoot`, `WalkCamera`, `Directional Light`, `Bridges`);
  `Bridges.parent = <none>`, position `(0,0,0)`. Scanned `StandaloneTrees` (677 children):
  `bridgeLike=0`. No `PaintedTrees` container on this hole.
- [x] **PASS** — *`tree_obstacles.csv` bake hash UNCHANGED on all 5 holes after saving.* md5 re-checked
  after every scene save; all five identical to the iter-1 baseline. Hole 17 still
  `4ea…`/`# bake_hash=79f0eae4` / 1663 rows (md5 `7e5bf743c222488f6d8db81cfb6b47a1`). The save hook
  fired and self-skipped: console `[TreeObstacleBaker] Hole 07: tree hash unchanged, skip re-bake.`
- [x] **PASS** — *`heightmap.bytes` byte-identical on all 5 holes.* md5 unchanged for 07/08/09/12/17;
  `git status` shows no `heightmap.bytes` path. Nothing in this iteration calls
  `PhysicsHeightmapBaker`.
- [x] **PASS** — *`SurfaceConfig.Default` length is 12 and returns the specced coefficients.* Verified
  live in the editor (`cfgLen=12`, `bridgeEnum=11`) and by test
  `SurfaceConfig_Default_HasARowForEverySurfaceType` +
  `SurfaceConfig_Bridge_ReturnsTheSpeccedCoefficients` (0.45 / 0.35 / 0.12 / 0.10).
- [x] **PASS** — *`Classify` returns `Bridge` mid-deck on hole 7 and `Water` 5 m off the edge.*
  Measured through the real baked `zones.json`, sampling perpendicular to the deck from its centre
  `(76.375, 23.947, −53.158)`: `Bridge` at ±0.0/1.0/2.0/2.4 m, `Water` at ±3/4/5/7.4/10 m. The deck
  half-width is 2.4 m, so the flip happens exactly at the deck edge.
- [x] **PASS** — *`SampleHeight` at mid-deck returns the deck Y, not the water Y.* `23.902 m` on the
  deck vs `0.218 m` five metres off it; the raw heightmap under the deck reads `−0.081 m`, i.e. the
  gorge floor — a **+23.98 m** correction supplied entirely by the zone mesh (Path β), with
  `heightmap.bytes` untouched. Re-proved with `heightmap == null` in
  `BakedHeightProvider_OnDeck_ReturnsDeckY_WithNoHeightmapAtAll`.
- [x] **PASS** — *`zones.json` gained exactly one `Bridge` group; every other group unchanged.*
  Semantic diff (polygons / mesh verts / mesh tris / yOffset) of the pre-bake copy vs the post-bake
  file: `Bridge (1, 504, 830, 0.0)` **NEW**; `Fairway (7, 7413, 12222)`, `Green (3, 3328, 5188)`,
  `Tee (15, 1378, 2002)`, `Sand (6, 326, 560)`, `CartPath (5, 2205, 2196)`, `Water (2, 505, 938)` all
  **UNCHANGED**, and the `obMask` blob byte-identical. No hidden scene drift is riding along.
- [x] **PASS** — *B6 completeness gate still passes.* Verified by running it, not assumed. The gate
  found the source raster and logged six `§4.2 OK` lines (fairway 114296 px, green 12046, tee_box
  17717, bunker 5326, cart_path 36875, water 2390). `Bridge` is an extra baked type with no
  source-raster counterpart, so the gate is unaffected.
- [x] **PASS** — *`bridge_obstacles.csv` row counts plausible; no row straddles the deck plane.*
  95 boxes for one bridge: 118 railing + 10 pier harvested, 6 deck-slab boxes excluded, 33 LOD
  duplicates dropped. By construction no kept row straddles: a row is kept only when its top is
  above `deckY + 0.02` or below `deckY − 0.15`, with `deckY` sampled per box at its own XZ.
- [x] **PASS** (hole 7) — *ball lands on the deck, rolls, no water penalty.* Approach shots at
  0.0/0.5/1.0/1.5/2.0 m off-centre all end `surface=Bridge`, Y ≈ 23.92–23.98. A 12 m drop onto
  mid-deck ends `Bridge` / `BallStopped`. Holes 8/9/12/17 not yet transplanted.
- [x] **PASS** (hole 7) — *ball fired at a railing deflects; two identical shots are identical.*
  14 perpendicular rolls (2–20 m/s, both directions) all stop on the deck at \|perp\| ≈ 2.00 m;
  the same 14 with `bridges = null` all end in the water. Determinism: 242 samples bit-identical
  on raw `fp`.
- [x] **PASS** — *`BallSimulation` with `bridges = null` reproduces current trajectories.*
  `Sim_BridgesNull_IsBitExactWithThePreStageCPath` compares the new 11-arg entry against the
  pre-existing 10-arg Phase 8 entry sample-for-sample on raw `fp` — identical. This is what keeps
  the other 13 holes untouched.
- [x] **FAIL → REPORTED** — *bot behaviour before vs after.* Hole 12 is not transplanted yet, so
  the A/B was run on hole 7 instead, over 205 footprint points. It is **not** unchanged:
  `IsPlayableSurface` flips on 26 points (intended) and `IsAvoidSurface` on 20 (not intended — the
  bot stops laying up short of the bridge). See § Bots. The two-bridge-channel deadlock test
  belongs with hole 12 and is still outstanding.
- [x] **PASS** — *No material, shader or prefab asset modified.* `git status --porcelain
  --untracked-files=all` lists no `.mat` / `.shader` / `.prefab` path. `Assets/Packs/` is gitignored,
  so it is also checked by mtime: every file under `Assets/Packs/PBR Bridge/` still dates to its
  import, none to today. The transplant only *instantiates* those prefabs.
- [x] **PASS** — *EditMode suite sweeps per assembly, no new failures.* See § Test sweep below.
- [x] **PASS** — *Unity Console has no errors related to this task.* Only pre-existing CS8632/CS0618
  warnings in unrelated `Assets/Scripts/UI/**` editor scripts; zero `error CS` in `Editor.log`.
- [x] **PASS** — *Bridge surface coefficients flagged for Cesar's feel pass.* See § For Cesar, item 1
  — including a measured correction to the SPEC's own rationale.
- [x] **PASS** — *Spec deviations flagged.* See § Deviations, three of them, one of which is a
  blocker the SPEC did not anticipate.

---

## Deviations from the SPEC

### 1. Deck source is `Main_LOD0`, not `Top_L_*` / `Top_R_*` (SPEC B7)

SPEC B7: *"the deck-top plane is derivable from the `Top_L_*` / `Top_R_*` renderer bounds"*.
Measured on `Bridge_withLODs.prefab`, in prefab-local space:

| Mesh | up-facing tris | Y range | X range | What it actually is |
|---|---|---|---|---|
| `Top_L_LOD0` | 90 | 3.874 – 4.009 | 2.486 – 2.836 | the **35 cm handrail cap** on ONE edge, 3.1 m above the walkway |
| `Main_LOD0` | 48 | **0.702 – 0.793** | **−2.399 – 2.408** | the **4.8 m × 60.9 m deck slab** — the walking surface |

Following B7 literally would have floated the collision deck 3.1 m above the visible planks and
made it 35 cm wide. The tool therefore prefers `Main_LOD0` → `Main_LOD1` → (SPEC's fallback route)
largest-XZ-footprint renderer. The 0.09 m Y spread is the deck's real camber and is preserved: the
generated mesh is a grid sampled against the source triangles, not a flat quad.

### 2. **BLOCKER the SPEC did not anticipate — `Generated/*.unity` is gitignored**

SPEC Stage E step 6 requires that `git diff --stat` for the hole show
`Generated/Hole_NN_Geo.unity`. **It cannot.** `.gitignore:111` is
`Assets/Golf/Courses/*/Generated/*` — every machine builds its own hole scenes
(`Docs/Pipeline/TREES_AND_GENERATED_SCENES.md`). A bridge that lives only in the scene therefore
reaches no other machine, while `Resources/HoleData/<slug>/Hole_07/zones.json` — which this same
task commits — **does**. On Cesar's Windows box that is a solid `Bridge` deck 24 m above the water
with no bridge drawn under it: precisely the Hole 02 drift, inverted.

Fix, mirroring the pattern that repo already established for exactly this problem
(`StandaloneTreeCatalog` / `standalone_trees.csv`):

- `Assets/Golf/Courses/<slug>/Data/hole-NN-geo/bridge_instances.json` — **tracked**, written
  automatically by every transplant, read by `Import/Transplant Bridges/Rebuild Current Hole
  (from catalog)`.
- Deck-mesh assets moved out of `Generated/BridgeDecks/` (ignored) into
  `Data/hole-NN-geo/` (tracked). The stale ignored folder was deleted.

JSON rather than CSV because a bridge instance is not a homogeneous row: hole 7 carries a
**child**-transform override (`fileID 2560872485283614753`, `m_LocalScale.y = 4.09`,
`m_LocalPosition.y = −6.74` — the `Structure` branch stretched so the piers reach the gorge floor)
on top of the root's own TRS. What is stored is Unity's own property-modification set, so nothing
can be silently dropped. Not to be confused with `bridges.json` (BridgeExporter's cart-path anchor
export, explicitly out of scope, different folder, untouched).

Round-trip proof: destroy the container → rebuild from the tracked catalog → **111 transforms
matched, 0 missing, max |Δpos| = 0.000000 m, max Δangle = 0.000000°, max |Δscale| = 0.000000** —
then regenerate the deck and re-bake, and both `zones.json` (md5 `52ad7fde60ed8bdabca130a3fd39e117`)
and the deck mesh asset (md5 `7c067d5520767c2e126536e821cda66a`) come back **byte-identical**.

### 3. Stage-A clone copies the whole modification set, not just the root TRS

SPEC Stage A step 4 says *"copy `position` / `rotation` / `localScale` verbatim from the source
transform"*. That alone loses the hole-7 child override in Deviation 2. The tool instead does
`InstantiatePrefab` → `SetPropertyModifications(GetPropertyModifications(source))`, and then
**verifies** by walking both hierarchies and reporting the worst world-space divergence, which is
how the 110-transform / 0.000000 evidence above is produced.

### Standing-ban note

`PIPELINE_HARDENING` rule 7 bans edits to `Assets/Scripts/Physics/`. This SPEC mandates five of
them (B1–B4 plus the test file) and names each file in its own § Files table, so they are made
under that explicit spec direction and listed here rather than taken silently.

---

## Test sweep

Per-assembly EditMode sweep (a single unfiltered run reports `No tests found`; an assembly filter
works for 20 of 22 assemblies, and class filters are ignored by the tool).

One genuine failure was found and fixed **in my own new test**, not in production code:
`SurfaceConfig_Bridge_SitsBetweenCartPathAndFairway` asserted `fairway.Restitution < bridge.Restitution`
and got `0.5f` vs `0.449996948f`. The SPEC's prose ("between CartPath 0.70 and Fairway 0.50") is
true of TangentFriction and RollingResistance but **not** of Restitution — the specced 0.45 is below
both, equal to GreenCollar's. The specced numbers were kept (they are an explicit tuning knob); the
assertion was corrected and the discrepancy is raised for the feel pass.

### Per-assembly EditMode sweep — 22 assemblies, **1 962 passed, 0 failed**, 3 skipped (re-run after Stage C)

| Assembly | passed | failed | skipped |
|---|---:|---:|---:|
| Golfin.Auth.Tests † | 45 | 0 | 0 |
| Golfin.Content.Tests † | 132 | 0 | 0 |
| Golfin.Core.Stamina.Tests | 37 | 0 | 0 |
| Golfin.Course.Tests | 26 | 0 | 0 |
| Golfin.Economy.Tests | 104 | 0 | 0 |
| Golfin.EconomyRuntime.Tests | 6 | 0 | 0 |
| Golfin.Gameplay.Tests | 402 | 0 | 0 |
| Golfin.HoleCompleteModal.Tests | 16 | 0 | 0 |
| Golfin.Inventory.Tests | 36 | 0 | 0 |
| Golfin.InventorySync.Tests | 82 | 0 | 0 |
| Golfin.Localization.Tests | 14 | 0 | 0 |
| Golfin.Net.Tests | 18 | 0 | 0 |
| **Golfin.Physics.Tests** | **380** | **0** | 3 ‡ |
| Golfin.Save.Tests | 51 | 0 | 0 |
| Golfin.SceneSnapshot.Tests | 8 | 0 | 0 |
| Golfin.Telemetry.Tests ※ | 18 | 0 | 0 |
| Golfin.Tournaments.Tests | 245 | 0 | 0 |
| Golfin.TournamentsRuntime.Tests | 247 | 0 | 0 |
| Golfin.UI.Polish.Tests | 9 | 0 | 0 |
| Golfin.UI.Rankings.Tests | 52 | 0 | 0 |
| Golfin.UI.Shop.Tests | 27 | 0 | 0 |
| Golfin.UI.Tests | 7 | 0 | 0 |

`Golfin.Gameplay.PlayMode.Tests` and `Golfin.Save.PlayMode.Tests` are PlayMode assemblies and were
not part of this EditMode sweep; nothing in this iteration touches runtime play code.

† `testAssembly` returns "No tests found" for these two even though they carry 45 and 132 `[Test]`
methods; `testNamespace` runs them. A tool quirk, pre-existing, unrelated to this change — recorded
so the next sweep does not read the empty result as "no tests exist".
‡ Three pre-existing `HoleCompleteDriverTests` skips carrying "Stage C1: HandleShotComplete is now a
no-op" — present before this change.
※ First attempt aborted with *"1 open scene(s) have unsaved changes: 'SnapshotTest_TempScene_0'"* —
a leftover in-memory temp scene from the SceneSnapshot suite that ran immediately before it, not a
failure and not from this change. Re-run clean. That scene never touched disk (`ls` confirms no
`Assets/SnapshotTest_TempScene_0.unity`).

**Tripwire — the new suite demonstrably ran** (23 cases after Stage C) (a green sweep is not evidence a new file was
compiled into it). All 11 `BridgeCollisionTests` reported individually, all `Passed`:
`SurfaceConfig_Default_HasARowForEverySurfaceType`,
`SurfaceConfig_Bridge_ReturnsTheSpeccedCoefficients`,
`SurfaceConfig_Bridge_SitsBetweenCartPathAndFairway_OnFrictionAndRoll`,
`PuttConfig_Bridge_IsNotAZeroRestitutionVoid`,
`Classify_OnDeck_ReturnsBridge_NotWater`,
`Classify_FiveMetresOffTheDeckEdge_ReturnsWaterAgain`,
`Priority_BridgeAlsoOutranksSand_ButNotGreen`,
`TrySampleMeshY_OnDeck_ReturnsDeckY_NotWaterY`,
`BakedHeightProvider_OnDeck_ReturnsDeckY_WithNoHeightmapAtAll`,
`GetYOffset_Bridge_IsZero_TheDeckMeshIsTheSurface`,
`Bridge_IsAbsentFromTheBotsPlayableSurfaceSet`.

---

---

## Stage C — railings and piers (iteration 2)

### What was missing

After A+B the deck was a `SurfaceType.Bridge` zone, so a ball could stand on the bridge. But
`bridge_obstacles.csv` did not exist, `IBridgeObstacleProvider` did not exist, and `BallSimulation`
had no bridge parameter: **a ball flew straight through the railings and piers.** Stage C closes
that.

| File | Role |
|---|---|
| `Assets/Scripts/Physics/Core/BridgeObstacleData.cs` | NEW — `BridgeCollisionProfile`, `BridgeBox` (yaw-rotated AABB, baked cos/sin), `BridgeHit`, `IBridgeObstacleProvider` |
| `Assets/Scripts/Physics/Runtime/BridgeObstacleProvider.cs` | NEW — XZ grid mirroring `TreeObstacleProvider` (same cell size, key packing, radius-aware insertion, sorted candidates); 3-slab segment test + containment guard |
| `Assets/Scripts/Physics/Runtime/BridgeObstacleLoader.cs` | NEW — profiles + per-hole CSV, `CourseSlugResolver`-driven, warn-once on unprofiled names |
| `Assets/Scripts/Editor/CourseImporter/BridgeObstacleBaker.cs` | NEW — harvests the prefabs' real `BoxCollider`s, `# bake_hash=` header, `sceneSaving` auto-rebake |
| `Assets/Resources/Data/bridge_collision_profiles.csv` | NEW — `default` / `railing` / `pier` |
| `Assets/Resources/HoleData/…/Hole_07/bridge_obstacles.csv` | NEW — 95 boxes, `bake_hash 16583aa6` |
| `Assets/Scripts/Physics/Core/BallSimulation.cs` | MOD — `bridges` threaded through flight / roll / putt; old overloads forward with `bridges = null` |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | MOD — builds `_bridgeProvider`, passes it on the production shot path (`RunSimFromController`, which is what bot shots use too) |
| `Assets/Scripts/Physics/Viewer/VersusBot.cs` | MOD (iter-3, authorised) — `IsAvoidSurface` includes `Bridge` |

### Three defects found in my own baker — audited as one shape, not one at a time

Per `CLAUDE.md` rule 15, once the second defect of a shape appeared I stopped fixing instances and
enumerated every reduction decision the baker makes. All three are in the *authored-collider →
fixed-point-box* reduction:

1. **Per-collider yaw frame inflated diagonal members.** Reducing each box in its own
   `eulerAngles.y` frame is a valid bound but a badly inflating one for a truss brace, which lies
   diagonally. Result: blocking faces at **+1.820 / −2.484 m** while the railing *art* is
   symmetric at **±2.26 m** — 0.44 m too far inboard one side, 0.22 m too far outboard the other.
   A ball rolling one way bounced off thin air; rolling the other way it fell past the railing.
   **Fixed:** reduce every box in the *bridge's* frame, with yaw derived as
   `atan2(across.z, across.x)` so it matches `BridgeBox.ToLocalXZ` exactly (the transform's
   `eulerAngles.y` has the opposite sign convention and is ill-defined on the x-tilted bridges).
   After: **+2.499 / −2.504**, symmetric.
2. **The ±0.15 m "straddling" band swallowed the KERBS.** The SPEC says a box straddling the deck
   plane is the deck and should be dropped. Measured, the deck-straddling set contains the deck
   slab (top 23.900, *below* the surface), the abutments (23.907) — **and the two kerb boxes, top
   24.007, i.e. only 0.060 m proud of a 23.947 deck.** Dropping the kerbs left the ball free to
   roll off the deck edge at 2.404 m while the nearest railing box did not start until 2.499 m: it
   fell 23.7 m into the water through a 0.095 m gap, having visibly passed *through* the railing
   art. **Fixed:** the above-deck band is now +0.02 m, which separates "is the deck" from "sits on
   the deck" with 0.04 m of margin either way. The kerbs are authored collision — this recovered
   them, it did not invent them.
3. **Every box was baked twice.** These prefabs carry colliders on *both* LOD levels
   (`Main_LOD0` and `Main_LOD1` are byte-identical boxes), so a naive harvest produced 124 rows
   for 93 distinct boxes. Not a correctness bug, but it inflates the grid and the tracked CSV.
   **Fixed:** dedupe on the formatted row.

### Measured after the fixes

Deck half-width **2.404 m**. Ball rolled from the deck centreline, perpendicular, both directions:

| speed | with bridge provider | with `bridges = null` |
|---|---|---|
| 2 / 4 / 6 / 8 / 10 / 14 / 20 m/s, **both directions (14 runs)** | **14/14 end ON the bridge**, Y 23.92–23.98, stopped at \|perp\| ≈ 2.00 m | **14/14 in the Water**, Y 0.24 |

- **Approach shots** fired along the bridge from 30 m short, landing at 0.0 / 0.5 / 1.0 / 1.5 /
  2.0 m off-centre: all five end on the bridge. At 2.3 m the ball lands outboard of the kerb,
  between kerb and railing, and goes OOB — a 0.4 m sliver at the extreme edge.
- **Drop test.** A ball dropped 12 m onto mid-deck: before the fixes it ended at Y 0.22,
  `termination=HitWater`. After: `end=(77.73, 23.92, −53.16) surface=Bridge
  termination=BallStopped`.
- **Determinism.** Two identical shots through the bridge provider are bit-identical — 242
  samples, raw `fp` equal on all three axes.
- **Zero behaviour change off-bridge.** `Sim_BridgesNull_IsBitExactWithThePreStageCPath` compares
  the new 11-arg entry against the pre-existing 10-arg Phase 8 entry sample-for-sample on raw
  `fp`. The other 13 holes cannot move.

### Railing boxes for `bridgeLODs.fbx` — SPEC Risk 2 closed (iteration 3)

Three of the seven instances (hole 8 ×2, hole 9) come from `bridgeLODs.fbx`, which ships **zero
colliders** — confirmed, not assumed. The SPEC offered "author a prefab variant with railing/pier
boxes" or "ship those three deck-only"; Cesar chose to author them.

**A prefab variant is the one route that cannot work.** `Assets/Packs/` is gitignored
(`.gitignore:107`), so a hand-built variant beside the FBX would never leave this machine — the
same class of bug as the gitignored hole scenes that Deviation 2 already had to fix. So the boxes
are derived from the model's own geometry into the **tracked** `bridge_obstacles.csv` instead: no
new asset, nothing to lose at the repo boundary, and it stays correct if the art is re-imported.

`BridgeObstacleBaker` now prefers the model's BoxColliders and falls back to renderer AABBs, one
box per railing/pier member (`Railing_`, `Top_`, `Beams_L_`, `Beams_R_`, `Pier_`, `End_`,
`Bottom_`). The name list only decides *whether* a part collides; the deck-relative rule still
decides railing vs pier. Sparse geometry is deliberately excluded — `StreetLight_Poles_LOD0` is
0.97 × 8.21 × 46.49 m of mostly empty air and one AABB there would be a phantom wall down the whole
bridge; likewise the overhead `Line_`, the 1 mm `Fence_` planes, and `Main_` (the deck, owned by
Stage B).

**Verified on the real FBX**, instantiated at hole 9's ground-truth TRS in a throwaway in-memory
scene (deck asset deleted, scene discarded unsaved):

| | |
|---|---|
| BoxColliders on the model | **0** |
| boxes derived | **12** — 8 railing, 4 pier |
| deck perpendicular edges | −2.404 … +2.404 m |
| railing inner faces | **−2.256 / +2.260 m** |
| contained both sides? | **YES** — the railing sits inside the deck edge, so this model needs no kerb equivalent |

One AABB per member is coarser than per-member colliders: a lattice railing becomes a solid wall
rather than a set of braces. For containment that is the safer error, and it is why these three
instances will not repeat hole 7's kerb problem.

**Regression:** hole 7 re-baked through the refactored path is byte-identical —
md5 `3b23c5dfbe5fd6bd7a0ea03b2638cf98`, `bake_hash 16583aa6`. The collider route is untouched.

### Hole 12 — why it is the stress case

Not transplanted yet; this is what it will exercise.

- **The only hole with two bridges**, and the SPEC asks whether the bot's probe can deadlock in the
  narrow channel between them. **It cannot, structurally**: `TrySafeLanding` is a bounded search —
  walk the aim line back in `LayupStep` increments, then retry over `OffsetDegrees` rotated lines —
  followed by an explicit else branch that logs *"no safe landing found — using original line
  (reactive OBReason will catch)"*. There is no unbounded loop and no stuck state. What iteration
  3's `IsAvoidSurface` change does is make that fallback **fire more often** on hole 12, since both
  bridges now read as hazards. Worth watching when the hole is done; not a hang risk.
- **Both bridges are x-tilted** (5.22° and 1.79°) — the deck is not horizontal. Hole 7 has zero
  tilt, so nothing has yet exercised the tilt handling: the deck generator working in the bridge's
  local frame and letting the instance TRS carry the tilt, and the baker folding each box's tilt
  into its own `BaseY`/`TopY`. Both were written for it; neither is proven.
- **SPEC Risk 1 is worst here.** The height field is 2.5D and the deck now wins over its footprint,
  so a low approach that should sail *under* a bridge will clang off the underside instead. With
  two decks over water, hole 12 is where that is most likely to be visible, and the SPEC asks for a
  report on how bad it looks.

### Trees

**No trees were transplanted by this task** — it is bridges only, and `scenery_transplant`
(5 Video-only trees, grass, rocks, signs) is a separate queued spec. What was verified is that
every hole's tree collision still matches its scene:

`Import/Bake Tree Obstacles/Validate All Holes` — the CI-wired drift gate that re-harvests each
live scene and diffs it against the committed bake row-by-row within 1 cm, plus the
`standalone_trees.csv` order check — **18/18 PASS**, run twice: once before Stage C and again after
every scene save. Hole 07: 1343 baked rows, 677 standalone. All five bridge holes'
`tree_obstacles.csv` and `heightmap.bytes` are md5-identical to the iteration-1 baseline
(hole 17 still `79f0eae4` / 1663 rows).

### Bots — the SPEC's Stage D claim does not hold, measured

SPEC Fact 3 says Bridge being absent from `VersusBot.IsPlayableSurface` gives the requested
"bots avoid bridges" **"with zero change to bot code"**, and Stage D asks for that to be *proved*,
not asserted. Proved, and it is **half true**.

A/B over 205 points on the bridge footprint — the current `zones.json` against the identical bake
with the `Bridge` group removed (i.e. exactly the pre-Stage-B state):

| predicate | points whose verdict changed | consequence |
|---|---:|---|
| `IsPlayableSurface` | 26 / 205 | as intended — the bot will not *target* the deck (those points read `Rough` before, over the abutments) |
| `IsAvoidSurface` | 20 / 205 | **not intended** — those points read `Water` before |

`IsAvoidSurface(s) => s == SurfaceType.Water`, and it is what the **H2 hazard / lay-up** path keys
off (`VersusBot.cs:545`, `:563`, `:575`), *not* `IsPlayableSurface`. So over the 20 gorge points:

- **Before:** deck read `Water` → hazard → the bot laid up short of the bridge.
- **After:** deck reads `Bridge` → not a hazard → the bot flies over, and its H2 landing check no
  longer refuses a landing point on the deck.

**Resolved in iteration 3.** Cesar chose full avoidance, so `VersusBot.IsAvoidSurface` now returns
true for `Bridge` as well as `Water` — the single edit to the bot, authorised explicitly against
the SPEC's out-of-scope list. Decision 2 is now actually implemented rather than half-implemented:
bots neither target a deck (`IsPlayableSurface`) nor fly a shot into one (`IsAvoidSurface`).
`Bots_TreatABridgeAsAHazardAndNeverTargetIt` asserts both predicates, and that `Water`, `Fairway`
and `Sand` are unaffected.

Bot *shots* do get bridge collision: `PhysicsLabController.RunSimFromController` is the production
shot path for player and bot alike, and it now passes `_bridgeProvider`.


---

## Iteration 4 — holes 8, 9, 12, 17

All 6 remaining instances transplanted at **max |Δpos| = 0.000000 m, max Δangle = 0.000000°,
max |Δscale| = 0.000000** against the Video source (33 / 17 / 27 / 27 / 14 transforms compared,
0 unmatched), matching the SPEC ground-truth table row for row including the x-tilts. Every hole's
`zones.json` gained **exactly one** `Bridge` group and nothing else moved — polygon, vertex and
triangle counts unchanged on every other group, `obMask` byte-identical.

| hole | bridges | deck polys | obstacle boxes | source |
|---|---:|---:|---:|---|
| 07 | 1 | 1 | 95 | colliders ×134 |
| 08 | 2 | 2 | 24 | renderer-AABB fallback |
| 09 | 1 | 1 | 12 | renderer-AABB fallback |
| 12 | 2 | 2 | 191 | colliders ×134 each |
| 17 | 1 | 1 | 34 | colliders ×36 + 2 synthesized kerbs |

### Two more baker defects — both reachable only through the x-tilt

Hole 12 is the only tilted hole, and it earned its reputation immediately.

4. **The deck slab was being filed as a railing.** Classification sampled the deck once at each
   box's *centre*. On a 5.22°-tilted bridge a 40.8 m deck-slab collider has its global `maxY` at
   the high **end**, 4.5 m above the deck under its middle — so it read as "4.5 m proud of the
   deck". Three such boxes, `halfX 2.404` (exactly the deck half-width) and `halfZ 20.4`: a solid
   block standing the full length and width of the walkway. **Fixed** by measuring every corner
   against the deck directly beneath *that* corner.
5. **The per-corner sample then fell through to the deck's mean Y** outside the footprint — and the
   slab's corners sit exactly *on* the boundary, so point-in-triangle rejected them and the tilt
   error came straight back, unchanged. **Fixed** by falling back to the nearest deck vertex, which
   carries the tilt with it.

**Regression proof:** holes 7, 8 and 9 have level decks, where per-corner sampling reduces to the
old rule exactly — and all three re-bake **byte-identical** through both fixes
(`16583aa6` / `aac1e956` / `5e91fee7`). Only 12 and 17 changed, and only by shedding misclassified
deck slabs.

### A third model that does not seal its own deck

Whether a model's authored colliders stop a ball *rolling* off its deck varies per model, so it is
now measured per side, per bridge, and repaired only where it fails:

| model | verdict | inner faces vs deck edge |
|---|---|---|
| `Bridge_withLODs` | sealed by model | kerbs ±2.004 inside ±2.404 |
| `bridgeLODs` | sealed by model | railing slab ±2.256 / ±1.128 |
| `Bridge_part_1` | **UNSEALED** | colliders −2.555 / +2.488, *outboard* of ±2.404 |

Hole 17 measured **0 of 8** rolls staying on the deck. The cause is not a missing railing — the
railing *is* baked, inner face 2.269, inboard of the edge. It is that its **base sits at 23.791
over a deck surface of 23.62–23.71**, so it floats 0.10–0.17 m up and a 43 mm ball rolls straight
underneath. `Bridge_part_1` has no kerb; the other two models do, one way or another.

Repaired with **2 synthesized kerb boxes**, spanning deck→railing-base over the railing's own XZ
footprint. **This is the one piece of geometry in this task not taken from the art** — 0.10–0.17 m
tall at the very edge of the deck, invisible in play. It is logged by the baker on every bake and
called out here so it is a decision on the record.

### Containment, all 7 bridges

**8/8** perpendicular rolls each (3 / 6 / 10 / 16 m/s × both directions) end **on the bridge**;
mid-deck classifies as `Bridge`; `SampleHeight` returns the deck. 56 shots, 56 contained.

### Risk 1 — nothing can pass under a deck

Measured deck-over-terrain clearance across each footprint:

| hole | clearance (min … max, mean) | deck below terrain |
|---|---|---:|
| 07 | 4.48 … 24.07 m (15.75) | 0% |
| 08 | 0.17 … 1.68 m | 0% |
| 09 | 0.35 … 1.44 m | 0% |
| 12 #1 | 0.53 … 5.71 m (3.65) | 0% |
| 12 #2 | −0.86 … 5.38 m (3.03) | **7%** |
| 17 | 0.14 … 1.77 m | 0% |

Holes 8, 9 and 17 sit under 1.8 m — nobody was going to play a ball under those, so Risk 1 is moot
there. Holes 7 and 12 have real height, but both span **water**, so a ball that "should" have
passed underneath was going in the water anyway. **Net: Risk 1 is real but benign on all five
holes.**

**Separate finding:** 7% of hole 12's second deck sits *below* the terrain, down to −0.86 m. The
`Bridge` polygon outranks terrain, so that patch reads as a shallow trench at the abutment.
Cosmetic/feel rather than a collision fault — flagged, not touched.


## For Cesar

1. **Bridge surface coefficients are a guess and need your feel pass** (SPEC B2, Risk 3).
   Currently `Restitution 0.45 / TangentFriction 0.35 / RollingResistance 0.12 / StopSpeed 0.10`,
   with a matching putt row at `RollingResistance 0.12 / StopSpeed 0.05`. Note the measured point
   above: on restitution the deck is currently *deader than fairway*, not between fairway and cart
   path as the SPEC's prose describes. Defensible for timber, but it is a choice, not the stated
   rationale.
2. **Hole 7's bridge is 23.7 m above the water.** Deck at Y `23.90`, water surface at `0.218`, gorge
   floor at `−0.081`. That is the Video scene's own authored placement, transplanted verbatim — the
   piers are the `4.09×` stretched `Structure` branch reaching down. Flagging it because it is
   striking, not because it looks wrong: the overlay shows the deck sitting exactly on the planks.
3. **The abutments step.** At the deck's two ends the terrain reads `24.54 m` and `22.58 m` against a
   deck at `23.90 m` — so up to a ~0.6 m step where the deck meets the bank, and the surface goes
   `Bridge → Rough → CartPath`. Worth a look during the feel pass; nothing in Stage A/B smooths it.
4. **`SurfaceType.Bridge` falls to `SfxId.LandFairway`** in `BallAudioEmitter`'s `default:` arm.
   `LandRoad` would suit a timber deck better. Not changed — the SPEC does not ask for it and the
   standing rule is minimal diff.
5. **Bridge part coefficients are also a guess** — `railing 0.35 / 0.75`, `pier 0.45 / 0.85` in
   `Assets/Resources/Data/bridge_collision_profiles.csv`. Anchored against the tree-trunk 0.15 (a
   trunk kills the ball; a steel truss should kick it back onto the deck), but not calibrated.
   Same feel pass as item 1.
6. **Bots now treat a bridge as a hazard** (your call, iteration 3). `VersusBot.IsAvoidSurface`
   includes `Bridge`. Side effect worth knowing: on hole 12 this makes `TrySafeLanding` fail more
   often and fall back to the original line — safe, but it means bots will sometimes just play
   through rather than lay up.
7. **Hole 17 carries 2 synthesized kerb boxes** — the only invented geometry in the task, 0.10–0.17 m
   at the deck edge, because `Bridge_part_1`'s railing floats above its own deck and the model has
   no kerb. Without them hole 17 lost every ball that rolled to the edge. Say the word if you'd
   rather ship it unsealed and fix the art instead.
8. **Hole 12's second bridge has 7% of its deck below the terrain** (down to −0.86 m), which reads
   as a shallow trench at the abutment. Art placement, not collision — flagged for your eye.
9. **Piers are baked but currently unreachable.** 10 pier boxes sit at Y 13.07–23.50, under the
   deck. SPEC Risk 1 stands: the height field is 2.5D and the deck now wins over its own
   footprint, so nothing can pass beneath a bridge to reach them. They cost nothing and become
   live if a 3D collision path ever lands.
10. **Risk 2 (`bridgeLODs.fbx` has no BoxColliders) is untouched and still open** — it bites Stage C
   on holes 8 (×2) and 9, not hole 7. No decision taken; do not read this iteration as having
   chosen the deck-only route.

## Next iteration inherits

Holes 8 / 9 / 12 / 17 (Stage A+B), then Stage C, then Stage D verification. The tool already has
per-hole menu items for all five holes; only hole 7 has been run.

## Editor state

Hole 7 scene saved and clean; the temporary framing camera used for the screenshot was removed and
the scene was **not** saved afterwards. No play-mode session was entered.
