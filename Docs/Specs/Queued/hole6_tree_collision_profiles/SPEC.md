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
