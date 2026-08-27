# Trees & Generated Scenes — per-machine scenes, tracked tree data

**Read this before touching hole trees, and before blaming a hole scene for "wrong" trees.**

Written 2026-08-27 after `hole02_tree_bake_drift`: Hole 02 collided with 1,495 Spruce that the
scene never drew.

---

## The one-paragraph version

**Generated hole scenes are per-machine.** `Assets/Golf/Courses/*/Generated/*.unity` is gitignored —
every machine builds its own. Trees survive that only because they live in TRACKED files:

| What | Where | Tracked? |
|---|---|---|
| Terrain trees (the terrain tree system) | `Data/hole-NN-geo/TerrainData_HoleNNGeo.asset` | **yes** |
| Standalone trees (`StandaloneTrees` GameObjects) | `Data/hole-NN-geo/standalone_trees.csv` | **yes** (new) |
| Collision bake read by physics | `Resources/HoleData/<course>/Hole_NN/tree_obstacles.csv` | **yes** |
| The hole scene itself | `Generated/Hole_NN_Geo.unity` | **no — gitignored** |

**After pulling changes under `Data/` or `Resources/HoleData/`:**

1. Open the hole scene → `Import/Standalone Trees/Rebuild Current Hole` → **save the scene**.
2. `Import/Bake Tree Obstacles/Validate All Holes` → expect 18/18 PASS.
3. Then build.

**Never re-import a hole to fix trees.** See "Why not just re-import" below.

---

## Why this exists — the Hole 02 drift

`tree_obstacles.csv` for Hole 02 (committed `4b0054069`, 2026-07-29) held **2,983 rows**: 1,488
terrain trees plus **1,495 standalone Spruce_1/Spruce_3**. The Mac's `Hole_02_Geo.unity` was
generated 2026-06-01 — *before* that placement pass — and had **no `StandaloneTrees` container at
all**.

Physics reads the committed bake. Rendering reads the local scene. They disagreed, so the player
collided with 1,495 trees that were never drawn.

Terrain trees never had this problem: `TerrainData` is a tracked asset, so it travels with the
repo. Standalone trees lived *only* in the gitignored scene, so nothing carried them between
machines. `standalone_trees.csv` closes that hole.

The other 17 holes were checked and matched — Hole 02 was the only casualty, and only because its
scene predated a re-import that every other machine picked up.

---

## The tracked file

`Assets/Golf/Courses/<course>/Data/hole-NN-geo/standalone_trees.csv`

```
# Tracked standalone tree placement — see StandaloneTreeCatalog.cs
prefab,worldX,worldY,worldZ,yawDeg,scale
Spruce 1,24.4774,3.7594,-171.5545,229.4820,0.9761
```

- One row per `StandaloneTrees` child, **in sibling order**. Order is load-bearing:
  `TreeObstacleBaker` harvests children in sibling order, so a rebuild must re-instantiate in file
  order for the bake to round-trip.
- `prefab` is the prefab asset name with spaces intact (`Spruce 1`). The baker is what maps
  `Spruce 1` → profile `Spruce_1`, not this file.
- `worldY` is the **tree transform's** Y, which sits `SinkOffset` (0.30 m) *below* the terrain
  surface so trunk bases don't float on slopes. The bake's `baseY` is the terrain height itself —
  the two differ by exactly the sink offset. Don't conflate them.
- Written with explicit `\n` and no BOM, so the file doesn't churn between macOS and Windows.
- A hole with **no** standalone trees still gets a header-only file. "File absent" therefore always
  means "never exported", never "this hole legitimately has none" — the drift gate depends on that.
  Holes 01 and 06 are the header-only ones.

---

## Menu items

| Menu | Does |
|---|---|
| `Import/Standalone Trees/Export Current Hole` | Writes `standalone_trees.csv` from the open scene |
| `Import/Standalone Trees/Export All Holes` | Same, for all 18 (opens each additively, restores your scene setup) |
| `Import/Standalone Trees/Rebuild Current Hole` | Deletes the `StandaloneTrees` container and re-instantiates every row as a prefab instance named `{prefab}_{index}`, in file order, under `HoleRoot` |
| `Import/Bake Tree Obstacles/Validate All Holes` | The drift gate — see below |

`TreePlacer.PlaceTrees` and both `TreeBrushTool` write paths **re-export the CSV automatically**, so
any placement or brush pass keeps the tracked file in step with the scene. You only run
`Export Current Hole` by hand if you moved trees some other way.

Rebuild is **not undoable** (it is thousands of objects). Re-run it to get back to the CSV state.

---

## The drift gate

`Import/Bake Tree Obstacles/Validate All Holes` opens each `Hole_NN_Geo` additively and checks two
things per hole, then restores your original scene setup:

1. **bake** — re-harvests the scene with `TreeObstacleBaker`'s *own* harvest function (not a
   reimplementation) and diffs against the committed `tree_obstacles.csv`: per-profile counts, and
   every tree matched to a committed row within **1 cm**.
2. **standalone** — the scene's `StandaloneTrees` children vs `standalone_trees.csv`,
   order-sensitive, within 1 cm / 0.01° / 0.001 scale.

Any mismatch is an error, and a missing scene is an error (a hole that can't render can't ship).

**It is wired into `CIBuild`** — both `BuildIOSDev` and `BuildIOS` run it before
`BuildPipeline.BuildPlayer`, and a mismatch fails the build with the hole and the counts, the same
way the build-stamp guard refuses an upload regression.

Escape hatch: pass `-skipTreeBakeCheck` on the Unity command line. It is logged loudly to both the
Unity console and stderr, because a build made with the gate disarmed is a build whose holes nobody
verified.

---

## Why not just re-import the hole?

Re-importing regenerates `TerrainData` **and** the scene, which wipes authored tree placement — and
also the terrain tree instances and bot baked data. That is why
`project_never_reimport_a_shipped_hole` exists. Repair scenes surgically: `Rebuild Current Hole`
puts the standalone trees back from tracked data without touching `TerrainData`, the terrain trees,
or anything else in the scene.

---

## Gotchas worth knowing

- **`baseY` is re-derived, not stored.** `TreeObstacleBaker` computes a standalone tree's `baseY`
  as `terrain.SampleHeight(x, z)`, ignoring the GameObject's own Y. So a hole rebuilt from an
  existing bake cannot reproduce that bake byte-for-byte: the bake rounds X/Z to 4 decimals, and
  re-sampling at the rounded X/Z flips the 4th decimal of `baseY` on rows whose true height sits
  within ~2.5e-5 m of a rounding boundary. On Hole 02 that was 73 of 2,983 rows, 0.1 mm each. The
  rebuild is otherwise exact, and `rebuild → save → bake` is now a fixed point.
- **Ground-snap raycasts hit trees.** Trees carry tall capsule colliders, so
  `PhysicsLabController.PlaceBallAt` can snap a ball onto foliage tens of metres up instead of the
  ground. If you are scripting ball placement near trees, assert the snapped Y against
  `Terrain.SampleHeight` — see `reference_raycast_ground_snap_traps`.
- **Most of Hole 02's trees are OB.** 1,365 of its 1,495 standalone Spruce (91%) stand outside the
  playable corridor per the baked OB mask in `zones.json`. A ball hit into those tree lines
  terminates `HitOOB` and the loop resets it, so it never appears to move. Only ~130 are in bounds.
  Budget for that when scripting tree-strike tests on this hole.
