# SPEC — `scenery_transplant`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

`QUEUED`. Filed 2026-08-25 by the Architect (Cowork session). **Scheduled after
`bridge_transplant` — Cesar's call, 2026-08-25: "Split, bridges first, trees later."**

## Goal

Move the remaining Video-only scenery from
`Assets/Golf/Courses/lomond-country-club/Generated/Video/Hole_NN_Geo.unity` into the live
`Generated/Hole_NN_Geo.unity` scenes: five hand-placed trees, 1 841 grass tufts, 6 rocks
and 14 wooden signs, across 8 holes (01, 02, 03, 05, 06, 14, 16, 18) — plus the grass on
the 5 bridge holes if `bridge_transplant` has not already visited them.

This is the second half of a split. `bridge_transplant`
(`Docs/Specs/Active/bridge_transplant/`) moves the 7 bridges and builds the bridge
collision; **it must be DONE and reviewed before this starts**, because both specs edit the
same five hole scenes and both are gated on the same `tree_obstacles.csv` hash invariant.

Read `bridge_transplant`'s "Architecture context" section before starting — Facts 1–4
there (Unity colliders are dead weight; the heightmap must not be re-baked; zone priority;
the `TreeObstacleBaker` save hook) apply here unchanged and are not repeated.

## Ground truth — the Video-only scenery

Full audit of all 18 hole pairs, done 2026-08-25 (hole 17 re-counted the same day, after
Cesar planted it). **The direction of the move matters: it is Video → live, and the
live scenes are the RICHER set.** Hand-placed trees already exist in the live scenes and
are already baked; almost nothing tree-shaped is missing.

**Hand-placed trees already in the LIVE scenes** — unpacked GameObjects under a
`StandaloneTrees` root, `Spruce 1_N` / `Spruce 3_N`, present on 15 holes:

| Hole | Spruce 1 | Spruce 3 | total | Hole | Spruce 1 | Spruce 3 | total |
|---|---|---|---|---|---|---|---|
| 03 | 445 | 321 | 766 | 12 | 911 | 610 | 1521 |
| 04 | 76 | 56 | 132 | 13 | 997 | 684 | 1681 |
| 05 | 1021 | 650 | 1671 | 14 | 835 | 580 | 1415 |
| 07 | 393 | 284 | 677 | 15 | 184 | 108 | 292 |
| 08 | 1195 | 763 | 1958 | 16 | 258 | 171 | 429 |
| 09 | 230 | 141 | 371 | 17 | 494 | 335 | 829 |
| 10 | 468 | 282 | 750 | 18 | 435 | 286 | 721 |
| 11 | 311 | 178 | 489 | | | | **13 702 total** |

Holes **01, 02, 06** have no `StandaloneTrees` container. Cross-checked against
`tree_obstacles.csv`: hole 7's 1343 rows = 677 standalone + 666 terrain, so the container
count and the bake agree exactly. **These trees are already placed, already baked, and
this task must not touch them.**

**Hole 17, resolved 2026-08-25:** it previously had no `StandaloneTrees` container, no
terrain trees, and therefore no `tree_obstacles.csv` at all — the only hole shipping with
zero tree collision. Cesar planted it that morning: the live scene now carries 829
standalone spruces (494 + 335) plus 834 terrain trees, and the `sceneSaving` hook
auto-baked `tree_obstacles.csv` at 04:04 (hash `79f0eae4`, 1663 rows). **All 18 holes now
have tree collision.** Hole 17 still appears below because it has 97 Video-only grass
tufts and a bridge — but its tree hash is now live data that this task must not disturb.

**What actually exists only in the Video scenes**, beyond the 7 bridges:

| Hole | Content | Count | Parent in Video scene |
|---|---|---|---|
| 01 | `RocksPebbles` | 4 | **`PaintedTrees`** |
| 01 | `MESH_WoodenSigns_Mid` | 3 | scene root |
| 02 | `Pine 03` | 2 | scene root |
| 02 | `Poplar 01` | 1 | scene root |
| 02 | `Rock01`, `Rock02` | 1 each | scene root |
| 03 | `MESH_WoodenSigns_Mid` | 3 | scene root |
| 03 | `Old 03` | 1 | scene root |
| 05 | `MESH_WoodenSigns_Mid` | 3 | scene root |
| 06 | `MESH_WoodenSigns_Mid` | 5 | scene root |
| 07 | `grass1` 46 / `grass2` 44 / `Grass_3` 52 | 142 | **`PaintedTrees`** |
| 08 | `grass1` 287 / `grass2` 277 / `Grass_3` 276 | 840 | **`PaintedTrees`** |
| 09 | `grass1` 118 / `grass2` 113 / `Grass_3` 89 | 320 | **`PaintedTrees`** |
| 12 | `grass1` 22 / `grass2` 17 / `Grass_3` 23 | 62 | **`PaintedTrees`** |
| 14 | `Ash 02` | 1 | scene root |
| 16 | `Fir 04` | 1 | scene root |
| 17 | `grass1` 51 / `grass2` 46 | 97 | **`PaintedTrees`** |
| 18 | `grass1` 135 / `grass2` 128 / `Grass_3` 117 | 380 | **`PaintedTrees`** |

So the whole Video-only tree population is **five trees**: `Pine 03` ×2 and `Poplar 01`
(hole 2), `Old 03` (hole 3), `Ash 02` (hole 14), `Fir 04` (hole 16). All five are
scene-root GameObjects. Everything else is 1 841 grass tufts, 6 rocks and 14 wooden signs.

**Note hole 18 has 380 grass tufts and no bridge** — including grass widens this task from
5 holes to 8 (adds 1, 2, 3, 5, 6, 14, 16, 18 for grass/trees/props).

### ⚠️ The `PaintedTrees` container is a phantom-obstacle trap

`TreeObstacleBaker.HarvestContainer` harvests **every child of `PaintedTrees`** as a tree
instance, strips the `_brush_N` suffix, and looks the name up in
`tree_collision_profiles.csv`. That table has rows for the 6 pines/firs/spruces and a
`default` row — and **no rows for `grass1`, `grass2`, `Grass_3` or `RocksPebbles`**.

Transplanting the grass into a container named `PaintedTrees` would therefore silently add
one `default`-profile cylinder per tuft — **0.25 m trunk radius × 3 m tall, with a 3 m
canopy radius reaching 9 m** — 840 of them on hole 8 alone. Ankle-high grass would play as
a forest. This is the exact failure mode the loader's own comment warns about (hole 6
shipped mis-tuned for months because `Fir_*` rows were missing).

**Mitigation, mandatory:** transplanted grass, rocks and signs go into a scene-root
container named **`PaintedGrass`** (and `Props` for the rocks/signs), never `PaintedTrees`
and never `StandaloneTrees`. The `tree_obstacles.csv` bake hash must be unchanged for
every hole afterwards — same gate as the bridges (Fact 4).

The five real trees are different: they SHOULD collide. They go into the existing
`StandaloneTrees` container (creating it on holes 2, 3 and 16 if absent), and each needs a
measured row added to `tree_collision_profiles.csv` — `Pine_03`, `Poplar_01`, `Old_03`,
`Ash_02`, `Fir_04` — using the same measurement method the `Fir_01..Fir_06` block
documents (canopyTop = max vertex Y; trunkHeight = 5th-percentile foliage Y; trunkRadius =
max XZ radius over bark verts in Y=[0.5, 3.0]; canopyRadius = max XZ radius over foliage
verts). `Fir_04` already has a row — reuse it. Adding these five trees WILL change
`tree_obstacles.csv` on holes 2, 3, 14 and 16, and that is the one place a changed tree
hash is expected rather than a bug.


## Implementation

### The transplant

Same transplant tool and the same closed-loop bake gate as `bridge_transplant`. Run only
after that spec is DONE, so a scenery regression is never tangled with a physics one.

**1. Extend `BridgeTransplantTool` into `SceneryTransplantTool`** (or add a second menu
group — do not fork the traversal logic). The selector is no longer a name regex; it is an
explicit allow-list per hole, taken verbatim from the Video-only scenery table above. A
transplant that moves a count other than the tabled count is a failure, not a surprise.

**2. Destination containers — the load-bearing rule.** Every transplanted object is
re-homed by category, at scene root:

| Source | Destination container | Baked as tree obstacles? |
|---|---|---|
| `grass1` / `grass2` / `Grass_3` | **`PaintedGrass`** (new) | **NO** |
| `RocksPebbles`, `Rock01`, `Rock02`, `MESH_WoodenSigns_Mid` | **`Props`** (new) | **NO** |
| `Pine 03`, `Poplar 01`, `Old 03`, `Ash 02`, `Fir 04` | existing `StandaloneTrees` (create on holes 2, 3, 16) | **YES** |

`PaintedGrass` and `Props` are deliberately named so `TreeObstacleBaker`'s
`StandaloneContainer` / `PaintedContainer` constants never match them. Do not rename those
constants to accommodate a different container name — the whole point is that the harvest
list stays a two-item allow-list.

Note the hole-1 rocks are parented to `PaintedTrees` in the Video scene. They must NOT
arrive that way; re-home them to `Props`.

**3. Tree collision profiles.** Add measured rows to
`Assets/Resources/Data/tree_collision_profiles.csv` for `Pine_03`, `Poplar_01`, `Old_03`,
`Ash_02` (`Fir_04` already has a row — reuse it, do not duplicate). Measure with the exact
method documented in the `Fir_01..Fir_06` comment block in that file; the prefabs are in
`Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/BPS/`. **Do not estimate.** Then re-bake
`tree_obstacles.csv` for holes 2, 3, 14 and 16 — the only four holes where a changed tree
hash is expected.

**4. The gate.** Afterwards, `tree_obstacles.csv` must be
**unchanged** on holes 1, 5, 6, 7, 8, 9, 12, 17 and 18 — every hole that received only
grass or props. A changed hash on any of those means grass or a rock leaked into a
harvested container, and the fix is the container name, never a profile row for grass.

**5. Wind.** Transplanted objects are wired exactly as they are in the Video scene —
same prefabs, same materials, no shader or material changes. The wind gap documented in
the Finding section above is real and affects these five trees too, but fixing it is a
separate spec across 14 holes. **Do not "helpfully" re-material anything in this task.**

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Moved EXACTLY the counts in the Video-only scenery table, per hole, per name
- [ ] Grass is under `PaintedGrass`, rocks/signs under `Props`, the 5 trees under `StandaloneTrees` — nothing new under `PaintedTrees`
- [ ] `tree_obstacles.csv` bake hash UNCHANGED on holes 1, 5, 6, 7, 8, 9, 12, 17, 18 (hole 17's is `79f0eae4` / 1663 rows — Cesar's own planting, do not disturb it)
- [ ] `tree_obstacles.csv` re-baked on holes 2, 3, 14, 16 and the row-count delta matches the 5 added trees exactly
- [ ] `tree_collision_profiles.csv` gained measured rows for Pine_03 / Poplar_01 / Old_03 / Ash_02, with the measurement method stated; no duplicate Fir_04 row
- [ ] `heightmap.bytes` byte-identical on every touched hole
- [ ] `zones.json` unchanged on every touched hole (scenery carries no `SurfaceMarker`)
- [ ] No material, shader or prefab asset was modified (the wind gap is `tree_wind_coverage`)
- [ ] Video scenes closed WITHOUT saving
- [ ] EditMode suite sweeps per assembly with no new failures
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification

## Files / hierarchy this task touches

**New**

- `Assets/Scripts/Editor/CourseImporter/SceneryTransplantTool.cs` (may extend `BridgeTransplantTool.cs`)

**Modified**

- `Assets/Golf/Courses/lomond-country-club/Generated/Hole_{01,02,03,05,06,14,16,18}_Geo.unity`
- `Assets/Golf/Courses/lomond-country-club/Generated/Hole_{07,08,09,12,17}_Geo.unity` — grass only
- `Assets/Resources/Data/tree_collision_profiles.csv` — 4 measured rows
- `Assets/Resources/HoleData/lomond-country-club/Hole_{02,03,14,16}/tree_obstacles.csv` — the ONLY expected tree-hash changes

**Read-only / must not change**

- `Generated/Video/*.unity`
- `Resources/HoleData/*/heightmap.bytes`, `*/zones.json`
- `Resources/HoleData/*/tree_obstacles.csv` on every hole except 02, 03, 14, 16
- Every material, shader and tree prefab asset

## Smoke evidence

Numeric: per-hole moved-count log diffed against the ground-truth table; bake-hash
before/after table for all 18 holes; row-count delta on the four re-baked holes.

Visual: human-in-the-loop pass through each of the 8 holes confirming the grass reads as
ground cover (not floating, not scaled wrong) and the five trees sit on the terrain, with
a written content-sanity description per hole.

## Out of scope (do NOT do these)

- Anything in `bridge_transplant` — bridges, `SurfaceType.Bridge`, bridge obstacles.
- Any material, shader or prefab asset change. The trees and grass arrive exactly as they
  are in the Video scenes. The wind gap is `Docs/Specs/Queued/tree_wind_coverage/`.
- Touching the 13 702 hand-placed spruces already in the live scenes.
- Adding collision profiles for grass. Grass must NOT be baked as an obstacle at all.
- Re-baking `heightmap.bytes` or `zones.json`.
- Re-importing any hole. Shipped holes are repaired in place.
- `git commit` from the Cowork session — Code commits (WORKFLOW_NOTES).
