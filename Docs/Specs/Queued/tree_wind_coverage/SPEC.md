# SPEC — `tree_wind_coverage`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

`QUEUED — BLOCKED on a Cesar decision` (see §Decision required). Filed 2026-08-25 by the
Architect (Cowork session), found while auditing trees for `bridge_transplant`.

## Goal

Make the hand-placed trees respond to the hole's wind the way the terrain trees already do.
Today they do not, and the mismatch is visible: every hand-placed spruce on the course is
pinned at MAXIMUM sway regardless of hole wind, while the terrain trees correctly scale
0 → 0.4 with `WindContext.SpeedMph`.

Independent of `bridge_transplant` and `scenery_transplant` — no scene edits, no bakes.

## The finding

Asked: do the hand-placed trees need updating to use the wind shader like the other trees?
**Answer: they are on a different shader entirely, and separately, nothing drives wind for
scene-placed trees at all. Two independent gaps.**

`TreeWindDriver` (`Assets/Scripts/Gameplay/UI/ShotUI/HUD/TreeWindDriver.cs`) maps
`WindContext.SpeedMph` onto the shader float `WindSpeedFloat1`, scaling 0 mph → 0 and
11 mph → 0.4. It finds materials by walking **`terrain.terrainData.treePrototypes` only**,
and skips any material whose shader is not `Custom/Vegetation`.

| Population | Shader | Wind property | Driven by game wind? |
|---|---|---|---|
| Terrain trees (JapaneseBlack, Metasequoia, ScottishPine, Fir) | `Custom/Vegetation` (`Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader`) | `WindSpeedFloat1`, authored 0.25–0.35 | **Yes** |
| Hand-placed `Spruce 1` / `Spruce 3` — 13 702 across 15 holes | `Leaves_URP.shadergraph` (`Assets/Realistic Tree/Shader/URP/`) | `Vector1_b0ddedae341d4c7ba1d429299f3078ea` ("Wind Speed"), authored **0.4** | **No** |
| The 5 Video-only trees (Pine 03, Poplar 01, Old 03, Ash 02, Fir 04) | `Custom/Vegetation` — correct shader | `WindSpeedFloat1` 0.35 | **No** — scene GameObjects are not terrain prototypes |
| Grass (`grass1`/`grass2`/`Grass_3`) | `SimpleFoliage.shadergraph` (`Assets/Packs/Pine Trees Vegetation Pack/Shader/`) | none exposed to the driver | **No** |

The visible consequence: **0.4 is exactly `TreeWindDriver.MaxTreeWindSpeed`**, so every
hand-placed spruce on the course is permanently pinned at maximum sway while the terrain
trees correctly scale with the hole's wind. On a calm hole (6.4 mph → terrain trees at
0.23) the spruces sway hardest; at 0 mph the terrain trees go still and the spruces keep
going. Hole 17 is the sharpest case: it is the windiest hole in `HoleDatabase.csv` and the one
`MaxWindMph = 11f` is calibrated against, so its terrain trees reach exactly 0.4 — and as
of 2026-08-25 Cesar planted 829 spruces there, which sit at 0.4 too. Hole 17 is therefore
the ONE hole where the two populations agree, and it is the worst place to eyeball the
bug. Verify on a calm hole instead.

## Decision required before implementation

**Cesar picks route A or B.** This spec stays `QUEUED` until then — do not start.

- **Route A — extend the driver.** `TreeWindDriver.Apply()` additionally walks the
  `StandaloneTrees` / `PaintedTrees` containers of the active scene and writes BOTH
  property names (`WindSpeedFloat1` and `Vector1_b0ddedae341d4c7ba1d429299f3078ea`).
  ⚠️ `TreeWindDriverEditorGuard` must be extended in the same change to cache and restore
  the Realistic-Tree authored values, or play-mode writes bake into the `.mat` assets on
  disk — the exact hazard the existing class docs warn about, now across a second material
  family. Also: the two shaders' sway curves are not the same function, so 0.4 on one does
  not look like 0.4 on the other; the mapping needs a feel pass, not a straight copy.
- **Route B — re-material the spruces** onto `Custom/Vegetation`. Simpler at runtime, one
  property, one code path. Cost: an art change to `Spruce_1.mat` / `Spruce_2.mat` /
  `Spruce.mat` (bark has no wind property at all), with visual-regression risk on 15 holes
  and 13 702 trees, and the Realistic-Tree leaves shader may carry lighting/translucency
  behaviour that `Custom/Vegetation` does not reproduce.

Whichever route: the acceptance test is the same — **at `WindContext.SpeedMph = 0` every
tree on the hole is visually static, and at the hole's authored wind the two populations
sway in agreement.** Capture a calm hole and a windy hole, side by side, before and after.

## Out of scope

- Grass. `SimpleFoliage.shadergraph` is a third shader with no exposed wind hook; leave it.
- Moving, adding or re-baking any tree. That is `scenery_transplant`.
- `MaxWindMph` / `MaxTreeWindSpeed` re-tuning beyond what the chosen route forces.
- `git commit` from the Cowork session — Code commits (WORKFLOW_NOTES).

