# hole6_tree_collision_profiles — Hole 6's 434 trees run on the generic `default` collision profile

**Status:** Queued · **Found:** 2026-08-04 (during the tree-wind investigation) · **Severity:** P2 — silent physics mis-tuning on a shipping hole

---

## The defect

Hole 6's trees bake with `profileName` = `Fir_01` … `Fir_06`, but
`Assets/Resources/Data/tree_collision_profiles.csv` has **no rows for any `Fir_*` prefab**.

`TreeObstacleLoader.GetProfile()` falls back to the `default` row **silently — no warning**
(`Assets/Scripts/Physics/Runtime/TreeObstacleLoader.cs:95-97`):

```csharp
if (profiles.TryGetValue(prefabName, out var p)) return p;
if (profiles.TryGetValue("default", out var d)) return d;   // <- Hole 6 lands here, silently
```

So **all 434 trees on Hole 6** currently collide as the generic default cylinder:

| | trunkRadius | trunkHeight | canopyRadius | canopyTop |
|---|---|---|---|---|
| `default` (what Hole 6 uses today) | 0.25 | 3.0 | 3.0 | 9.0 |
| tuned conifers, for scale (`Mesh_Metasequoia`) | 0.30 | 4.5 | 2.0 | 13.0 |

These are tall firs; a 3 m trunk / 9 m canopy top under-represents them.

## Evidence

```
$ head -3 Assets/Resources/HoleData/lomond-country-club/Hole_06/tree_obstacles.csv
# bake_hash=a953ea8e
worldX,worldZ,baseY,scale,profileName
-109.9479,-48.2089,9.3666,0.8894,Fir_02

$ grep -c "^Fir_" Assets/Resources/Data/tree_collision_profiles.csv
0
```

Profiles that DO exist: `default`, `MESH_01Cedar`, `MESH_JapaneseBlack_01`,
`MESH_JapaneseBlack_01_Var1`, `Mesh_Metasequoia`, `MESH_ScottishPine_01`, `Spruce_1`, `Spruce_3`.
Hole 6 is the only shipping hole using the BSP Fir prototypes, which is why it is the only one affected.

## Blast radius — this is not cosmetic

The same profile table drives **ball physics** *and* **bot trunk-avoidance**:

- `ITreeObstacleProvider.TestSegment` uses `TrunkRadius` / `CanopyRadius` from the profile.
- `VersusBot` (multiplayer) → `BotTreeProbe.TryFindTrunkClearAim(...)` (`VersusBot.cs:643`),
  and `BotDriver` via the same shared helper (`BotDriver.cs:747`).

A too-thin trunk means the ball under-collides **and** the bot under-avoids. Because trunk
avoidance rarely fires on straight tee→pin play, the regression is quiet — normal smoke won't catch it.

## Fix

1. Add six rows to `Assets/Resources/Data/tree_collision_profiles.csv` for `Fir_01` … `Fir_06`,
   measured from the actual prefab meshes (`Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/BPS/Fir 0N.prefab`)
   rather than guessed. Note the name normalisation: spaces → underscores (`Fir 03` → `Fir_03`).
2. No re-bake of `tree_obstacles.csv` is required — positions/scales/`profileName` are unchanged,
   so `bake_hash` stays `a953ea8e`. Only the profile lookup table changes.
3. Verify with a bot run on Hole 6 that trunk hits register at the corrected radii.

## Hardening (recommended, separate)

Two silent-failure gaps surfaced while diagnosing this — both worth closing regardless:

- **`GetProfile()` falls back with no warning.** A `Debug.LogWarning` on an unmatched
  `profileName` would have surfaced this the first time Hole 6 loaded.
- **Nothing validates `bake_hash` at runtime.** The baker writes it and uses it to skip
  redundant bakes, but no runtime check compares it against the scene, so a forgotten
  re-bake yields silently stale collisions (ball bounces off trees that are no longer there).

---

## Resolution — DONE 2026-08-04

Fixed in `c1d38e280`; test-hygiene follow-up in `641687406`. Approved by Cesar.

### What shipped

Six measured rows in `Assets/Resources/Data/tree_collision_profiles.csv`:

| prefab | trunkR | trunkH | canopyR | canopyTop |
|---|---|---|---|---|
| `default` (what Hole 6 used) | 0.25 | 3.0 | 3.0 | 9.0 |
| `Fir_01` | 0.72 | 7.21 | 8.07 | 44.50 |
| `Fir_02` | 0.86 | 10.39 | 11.08 | 43.68 |
| `Fir_03` | 0.61 | 15.23 | 6.86 | 34.39 |
| `Fir_04` | 0.57 | 4.20 | 8.05 | 28.72 |
| `Fir_05` | 0.64 | 5.44 | 7.92 | 32.78 |
| `Fir_06` | 0.73 | 6.29 | 9.98 | 40.74 |

No re-bake — `bake_hash` still `a953ea8e`, as specified.

### How they were measured

LOD0 mesh of each `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/BPS/Fir 0N.prefab`.
Definitions are recorded in the CSV header comment so the next person can reproduce them:

- `canopyTop` = max vertex Y over the whole tree.
- `trunkHeight` = canopy base, 5th percentile of foliage-submesh vertex Y (robust to stray skirt verts;
  raw min is noisy — Fir_04's is 0.69 m vs a p05 of 4.20 m).
- `trunkRadius` = max XZ radius from the prefab origin over bark-submesh verts in the branch-free band
  Y=[0.5, 3.0] m. Below 0.5 m is root flare (up to 2.95 m on Fir_01); above ~3 m the bark material also
  covers branches on Fir_01/02/03/04, so neither band represents the trunk.
- `canopyRadius` = max XZ radius from the prefab origin over foliage-submesh verts.

Two provenance traps worth recording:

1. **There are two Fir prefab sets** — `Assets/Packs/BSP Trees Package/BPS/` and
   `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/BPS/`. Hole 6's `TerrainData_Hole06Geo.asset` is
   binary-serialized, so a text grep for the prefab GUID finds nothing and looks like "no hole uses
   these." Decoding the embedded (nibble-swapped) GUID bytes confirms it is the `Trees(2025)` set.
2. **The FBX vertex data is in centimetres** (`GlobalSettings UnitScaleFactor = 1.0`, importer
   `useFileScale: 1`, `globalScale: 1`) → ÷100 for Unity metres. Independently confirmed: the measured
   mesh extent matches each prefab's Unity-baked `LODGroup.m_Size` to ≤0.01 m on all six.

### Verification

| Check | Before | After |
|---|---|---|
| Hole 6 instances on `default` | 434 / 434 | **0 / 434** |
| Trunk hit, segment offset between old and new radius | 1 / 12 | **12 / 12** |
| Canopy entry at 20 m altitude (old `canopyTop` 9 m) | 0 / 12 | **12 / 12** |
| Full `BallSimulation`, 7 real trees | — | every carry changed; re-run **bit-exact** |
| EditMode suite | 937 / 3 fail | **940 / 0 fail** |
| Tree collision suite | — | **9 / 9** |

Step 3 of the Fix list ("verify with a bot run") was met with deterministic provider-level and
full-sim verification instead of a bot video — sharper for this claim, but there is **no video
artifact**. Outstanding if one is wanted.

### Blast radius — read before tuning further

Canopy footprint over the 434 instances: **10,490 → 89,700 m² (8.6×)**; trunk area **7.7×**.
Hole 6 plays meaningfully tighter. `canopyRadius` is the dial — these use max foliage radius;
p90 is roughly 35% smaller (e.g. Fir_02 11.08 → 8.03).

`TestSegment` cost rose ~1.7× on Hole 6 (20k calls, 491 → 833 ms), partly real work: the corrected
run registers 2727 hits vs 491.

### Hardening

- **Done:** `GetProfile()` warns on an unprofiled name, once per distinct name (it is called per baked
  instance — 434× on Hole 6 without the dedupe).
- **Done, unplanned:** corrected a wrong invariant comment on `TreeObstacleProvider.CellSize`. It claimed
  cell size must be ≥ max canopy diameter (9 m worst case). Canopy diameter is now up to 22 m and the code
  is still correct — grid insertion is radius-aware, so a larger canopy widens candidate coverage rather
  than narrowing it. The real constraint is step length vs the 3×3 gather, which nothing had recorded.
- **Not done — and not buildable as specified:** the runtime `bake_hash` staleness check. The hash is
  written and compared only inside `TreeObstacleBaker` (editor); the runtime loader discards it as a `#`
  comment. A runtime check could only recompute the hash from the rows it just loaded, which catches
  hand-edits, not a forgotten re-bake. Detecting genuine staleness needs the hole's terrain scene, which
  no player build has. The version that would catch the real failure mode is an **editor-side** validator
  that re-harvests each hole scene and diffs the hash. Separate task.

### Follow-ups this surfaced

1. **The other conifer profiles are unvalidated.** `tree_collisions/SPEC.md` §3a states the shipped values
   are *"Architect estimates — the implementer SHOULD sanity-check radii/heights against actual prefab
   bounds (renderer bounds at scale 1) and flag big mismatches"*; that task's report shows no such check.
   `MESH_01Cedar`, `MESH_JapaneseBlack_01(_Var1)`, `Mesh_Metasequoia`, `MESH_ScottishPine_01`, `Spruce_1`,
   `Spruce_3` are still guesses. Hole 6 was only distinctive in having *no* row.
2. **`GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav` is intermittent.** Failed in one full-suite
   run, passes in isolation and in later full runs, untouched by this task. Order- or state-dependent.
