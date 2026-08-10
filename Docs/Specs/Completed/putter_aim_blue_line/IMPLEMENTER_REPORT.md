# IMPLEMENTER REPORT — `putter_aim_blue_line`

**Iteration:** 1 · **Date:** 2026-08-08 JST · **Implementer:** Claude Code (direct, Tier 2 / TellCode)
**Iteration shape:** putter_aim_line:initial-build
**SPEC:** `Docs/Specs/Active/putter_aim_blue_line/SPEC.md` (Rev 2)
**Canonical video:** `videos/putter_aim_blue_line_clip_hole6.mp4` (1170×2532 @ 30 fps, 27.4 s, 24.3 MB)
**Canonical screenshot:** `screenshots/hole6_01_aim_line_visible.png` (1170×2532, frame-extracted from the clip)
**Second video:** `videos/putter_aim_blue_line_clip_hole1.mp4` — same scenario on Hole 1. Kept because
it is the evidence for the buried-cup bug below, NOT because Hole 1 is the better demo.

---

## Baseline (HEARTBEAT.log)

HEAD `3c3f1aec433bb0573b95068fc6d1b8c4e5b82999`. Pre-existing dirty paths at kickoff, none of
them mine and none touched by this task:

```
 M Assets/Scripts/UI/Gacha/GachaCarouselController.cs
 M Assets/Scripts/UI/ModeSelect/ModeCardController.cs
 M Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs
 M Docs/Specs/Completed/shot_ui_translucency_glow/ARCHITECT_REVIEW.md
 M Docs/TellCode.md
```

---

## Files modified or created

| File | Status | Summary |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/PutterAimLine.cs` | **new** | The feature. World-space 15 m strip from the ball along the live aim yaw, gated on putter aim, rebuilt only on dirty, vertex Y from the shared slope bake. |
| `Assets/Shaders/PutterAimLine.shader` | **new** | Unlit transparent URP strip shader, flat `_Color`, queue `Transparent+1` (3001) so it composites over the grid's 3000. |
| `Assets/Materials/PutterAimLine.mat` | **new** | Material asset on that shader, `_Color = #7AE9FF`. |
| `Assets/Scripts/Physics/Tests/PutterAimLineTests.cs` | **new** | 10 EditMode tests over a synthetic 5×5 m green with an analytic 4% grade. |
| `Assets/Scripts/Physics/Viewer/PutterGreenReader.cs` | modified | **Additive, read-only.** `TrySampleBakedSurfaceY(x, z, out y)` (interpolates the grid's own lattice with the identical triangulation) + `SurfaceYOffset` getter. No behaviour change to the grid. |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | modified | New `PutterAimBlueLineClip` scenario (the video deliverable — production Hole 1, real entry path) + `BlueLineBootToHole1` helper. Also extended `PutterAimWarpedGridOnTestGreen` to gate the aim line and sweep aim/camera. |
| `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` | modified | Registered the `putter_aim_blue_line_clip` scenario key. |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | modified | Menu entry `GOLFIN/Smoke/Loop v2/Putter Aim Blue Line — clip`; arms the recorder with `ArmDeferred()` + a 60 s cap + the task's `videos/` output path. |
| `Assets/Scenes/Physics/LabScaffold.unity` | modified | `PutterAimLine` component added to `LabRoot` (beside `PutterGreenReader`), all four refs wired. Additive-only diff. |
| `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity` | modified | Same component + wiring. `_shotController` is `{fileID: 0}` — parity with the reader in that scene, which has none either. |

### Scene-diff audit (visual review checklist item 4)

`git diff Assets/Scenes/Physics/` is **additive only**: one `m_Component` entry and one
`MonoBehaviour` block per scene. Zero `m_IsActive`, `sizeDelta`, `m_LocalPosition` or
`m_AnchoredPosition` changes (grep count = 0). One extra line appears in TestGreen —
`_ballTrail: {fileID: 0}` on `PhysicsLabController` — which is Unity re-serialising a null field
that already exists in code but predated that scene's last save. No behavioural effect.

---

## Definition of done — §7

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | `PutterAimLine.cs` exists; **zero asmdef edits**; **`PutterTrackGraphic.cs` untouched** | **PASS** | `git status --porcelain --untracked-files=all -- '*.asmdef'` → empty. `git status --porcelain -- '*PutterTrackGraphic*'` → empty. `Golfin.Physics.Viewer.asmdef` already referenced `Golfin.Gameplay.Input`; `Golfin.Physics.Tests.asmdef` already referenced it too, so the tests needed none either. |
| 2 | Line appears on entering putter aim, hides on shot start and on leaving putter mode | **PASS** | **On video, production Hole 1:** the line is absent at `aimline_armed`, appears the instant `BeginExternalDrag()` runs (t≈6 s), and is gone the moment aim is released before the putt (t≈21.8 s — `screenshots/hole1_03_hidden_after_release.png` shows the bare green, no line and no grid). Scenario log: `aim active=True lineVerts=62` then `Aim released — line active=False`. Test `AimLine_VisibilityFollowsPutterAimState` covers the same three transitions unit-side, including `IsPutt = false` → hidden. |
| 3 | Line stays anchored to ball + aim heading while the camera rotates | **PASS** | **On video:** the ±35° aim sweep (t≈7–21 s) shows the line pivoting about a fixed ball while the camera holds still — `hole1_01` (down the pin line) vs `hole1_02` (swung ~44° right, same eye, same ball). Separately, live play-mode on TestGreen with the aim held at 45° and the camera orbited +100°: `RebuildCount` did **not** move (1 → 1) and the line held the same world track (`aimline_01` vs `aimline_02`) — it is world-space, not camera-derived. Test `AimLine_FollowsAimHeadingFromBall` asserts the anchor, the 15 m length, the heading at three yaws, and rib perpendicularity. |
| 4 | No z-fight with the grid or terrain on a sloped green | **PASS** | Measured on the live TestGreen mesh (sinusoidal green, 2401 baked cells): gap-over-grid across all 62 vertices was **min 0.0161 m, max 0.0239 m** — always positive, never near zero. The ±4 mm spread around the nominal 0.02 m is the strip's own ±0.04 m lateral half-width re-sampled against a sloped surface, not error. Belt-and-braces: the material sits at render queue **3001** vs the grid's **3000**, so even with both `ZWrite Off` the line composites last (SPEC §4 § Sorting). No shimmer in any of the three frames. |
| 5 | `SetBallPositionOverride` honoured — captures work with no live ball | **PASS** | Same signature and same priority order as `PutterGreenReader:144`. Test `AimLine_BallPositionOverride_MovesTheLine`. Every capture in this report was produced through it — TestGreen has no live ball feeding the component, which is exactly the failure mode SPEC §3 flags in red. `SetAimYawOverride` was added for the same reason (that scene has no `ShotController` either). |
| 6 | EditMode tests green at the current baseline; report the number | **PASS** | Full EditMode suite: **1033 total, 1030 passed, 0 failed, 3 skipped.** The 3 skips are pre-existing `HoleCompleteDriverTests` Stage-C1 skips. Baseline before this task was 1023 tests; this adds 10. |
| 7 | Zero per-frame GC after first build; mesh rebuilt only on dirty | **PASS** | `GC.GetAllocatedBytesForCurrentThread()` around the live component: **600 steady-state ticks → 0 rebuilds, 0 bytes (0.000 B/frame)**; **200 forced rebuilds → 0 bytes (0.000 B/rebuild)**. Test `AimLine_DirtyCheck_SkipsRebuildWhenNothingMoved` asserts 60 no-op ticks, then that 0.01° of yaw + 4.5 mm of ball drift stay below threshold while 5° triggers exactly one rebuild. |
| 8 | One draw call; unlit, no shadows; height from the slope bake per §8.4 | **PASS** | Live mesh: 31 samples → **62 verts / 60 tris / 1 submesh**, one `MeshRenderer`, one material → one draw call. Renderer: `shadowCastingMode = Off`, `receiveShadows = false`, `lightProbeUsage = Off`, `reflectionProbeUsage = Off`. Shader is unlit — the fragment stage returns a constant colour. Heights come from `PutterGreenReader.TrySampleBakedSurfaceY`, i.e. array reads into the existing 0.5 m bake; **zero `Physics.Raycast` calls in the file** (grep confirms). |

---

## §8.4 off-bake fallback — implementer's choice, as the SPEC asks me to note

A 15 m line frequently runs past the green polygon. The tail **carries the last valid baked Y
forward** (and, before the line has touched the bake at all, uses the ball's own Y). No raycast,
no allocation. Rationale: the line is a direction read, not a terrain read — a flat overhang tail
beyond the collar is visually correct and costs nothing, whereas a raycast tail would reintroduce
exactly the per-rebuild physics query §8.4 exists to avoid. Visible in
`screenshots/aimline_03_aim_pivot_yaw75.png`, where the line continues past the grid's far edge.
Covered by `AimLine_OffBakeTail_HoldsLastBakedHeight_NoRaycast`.

## Why `PutterGreenReader` was touched at all

SPEC §8.4 requires the line and grid to sample Y from the **same** source so the 2 cm gap is true
"by construction". The grid's rendered surface is a linear interpolation across its cell-centre
lattice; sampling the classifier directly instead would be a *different* surface mid-cell, and the
gap would only be exact at cell centres. So the reader now exposes that lattice read-only, using
the identical A-B-C / B-D-C diagonal split `BuildGridMesh` emits. Additive, allocation-free, O(1),
no mutation — the grid's own behaviour is byte-identical. `SurfaceYOffset` is exposed alongside it
so `PutterAimLine` can warn (once) if a future retune of either offset closes the clearance.

## Out of scope, confirmed not built

No iron/driver line (the cone covers those — `AimLine_VisibilityFollowsPutterAimState` asserts the
line stays hidden when `IsPutt == false`); no distance ticks; no cup-aware trimming; no putt-strength
coupling; **no curve prediction** — the strip is straight by construction (`ballPos + dir * t`, one
constant `dir` per rebuild), so the L1 lock cannot be violated by a tuning value.

---

## Video deliverable

`videos/putter_aim_blue_line_clip_hole6.mp4` — 1170×2532 @ 30 fps, 27.4 s, captioned via
`Docs/Scripts/build_bot_video.py` (title clears at 3 s, well before the aim action starts).
Produced by the `putter_aim_blue_line_clip` smoke-bot scenario, hole selected via SessionState
`BlueLineClip.Hole`: `GOLFIN/Smoke/Loop v2/Putter Aim Blue Line — clip (Hole 6)`.

**Hole 6, not Hole 1, and that is deliberate** — see finding 0 below: Hole 1's cup disc is buried
under its own green, so a Hole 1 clip shows a flagstick with no hole at its base. On Hole 6 the cup
renders (`screenshots/hole6_cup_visible_2x.png`, 2× crop at the stick base). A Hole 1 menu entry is
kept, labelled `(Hole 1, cup is buried)`, so the bug stays reproducible.

**Nothing in the clip is forced on by a test seam.** No `SetAimActiveForTest`, no overrides —
real ShellScene boot → Home → Practice → HoleSelection → Hole 1 card → `BeginGameplayLoad(1)`,
then the production `ShotController` path (`IsPutt = true` + `BeginExternalDrag()`). Scenario
verdict: `PASS — lineVerts=62, hidden after the putt`, with 1949 baked green cells.

Beat sheet: ball on the green 3 m from the pin, putter in hand → **line appears** on entering
aim → **±35° aim sweep**, two passes, the line pivoting live about the ball and draping over the
slope grid (343 rebuilds, exactly one per aim-change frame) → aim released, **line and grid both
vanish** → putt struck, ball in the cup, hole-complete modal.

Integrity checks before delivering (Hole 6 clip):
- `ffprobe`: `r_frame_rate=30/1`, 814 frames — a real clip, not a stills slideshow.
- **Y-flip:** six CONSECUTIVE decoded frames (n=330–335), pairwise diffs 1.03–1.16 with no spike.
  Consecutive decode, not `-ss` keyframe sampling, because keyframe sampling misses flips.
- **Motion:** frames 8 s apart during the sweep differ by 4.7; across the putt by 55.8.
- **Cup present:** verified by 2× crop at the flagstick base, not assumed.
- Caption read back from the encoded output (the Hole 1 render overflowed the 1170 px portrait
  frame first time and was re-rendered shorter; the same shortened title is used here).

(Hole 1 clip, for reference: flip diffs 0.44–0.46, motion 2.6 / 65.7, same 814 frames.)

## Needs Cesar / manual verification

1. **Colour + width lock.** `#7AE9FF` at 0.08 m are the SPEC's provisional values. On Hole 1 the
   cyan separates cleanly from the orange slope grid and the green turf at putting camera distance.
   Both are live `[SerializeField]`s, so any tuning is an Inspector nudge, no rebuild.
2. **On-device 60 fps.** Allocation is measured at zero in-Editor, which SPEC §8.2 accepts as
   sufficient; a device Profiler capture remains the bonus evidence.

## Found in passing — not this feature, worth separate Quick tasks

0. **🔴 HOLE 1'S CUP IS BURIED UNDER ITS OWN GREEN.** Cesar spotted this in the first clip: the
   black hole disc never appears at the flagstick base. It is not the aim line hiding it — the
   overlays sit 20–40 mm above the turf and the cup is opaque geometry 1 mm above it; the disc is
   absent from frames where the line and grid are both off (`screenshots/hole1_cup_MISSING_2x.png`).

   Measured, all 18 holes — `greenSurfaceY − cupTopY` by raycast at the cup's own XZ:

   | Hole | green − cup top | cup |
   |---|---|---|
   | **1** | **+23.6 mm** | **BURIED** |
   | 2–18 | −1.3 mm to −6.4 mm | visible |

   `HoleGeoImporter.cs:2840-2847` seats the cup at `pinSeatY + 0.001` and scales it 1 mm thick.
   Hole 1's green mesh sits 23.6 mm **above** that datum at the pin, so the disc is inside the
   turf. Every other hole clears by 1.3–6.4 mm — a ~1 mm margin the greens bake evidently ate on
   Hole 1 only. **The durable fix belongs in the importer** (seat the cup on the actual green mesh
   surface at the pin XZ, not on `pinSeatY`, and give it more than a 1 mm margin) followed by a
   Hole 1 re-import — editing the Generated scene by hand would be erased on the next import.
   Out of scope for this task; flagged rather than silently worked around.

   Hole 1 is the hole every new player sees first, so this is worth its own task.

1. **`PutterAimWarpedGridOnTestGreen` never completes.** It calls
   `SceneManager.LoadSceneAsync(..., Single)`, which destroys the bot's own coroutine host, so the
   scenario stalls right after the load. Pre-existing — my extension to it is all downstream of that
   line and never executed. (The new `putter_aim_blue_line_clip` scenario avoids this entirely by
   staying in ShellScene and going through `BeginGameplayLoad`.)
2. **`Click("PLAY")` is now ambiguous.** `PutterAimGreenReaderVisible` and any other scenario using
   the bare `Click("PLAY")` route matches five buttons since the mode carousel landed and lands on
   the mode card instead of the play action — I hit this on the first take. `ClickModeCardPlay` is
   the API that survived. Those scenarios should be migrated.
3. **HUD club label stays on `DRIVER 229 mts` while putting.** Visible bottom-right throughout the
   clip: `PhysicsLabController.SetClub(PutterIndex)` switches the physics bundle but the HUD club
   widget still reads the driver. Unrelated to the aim line, but it is on screen in every putting
   capture the project produces.
4. **BotVideoRecorder session guard.** Recording was blocked because three full-res clips had
   already run in that 2-day-old Editor session. I restarted Unity (the guard's own prescribed
   remedy) rather than using the override — the guard exists because cumulative encoder load once
   forced a machine reboot. The old editor then hung during shutdown and needed a `kill -9`, which
   cost a full artifact reimport. Worth knowing that the restart path is not free.

## Screenshots

| File | What it shows |
|---|---|
| `screenshots/hole6_01_aim_line_visible.png` | **Canonical.** Production Hole 6, full HUD, ball + putter. Cyan line from the ball over the slope grid. Frame-extracted from the clip. |
| `screenshots/hole6_cup_visible_2x.png` | 2× crop at the flagstick base — the black cup disc rendering correctly on Hole 6. |
| `screenshots/hole1_cup_MISSING_2x.png` | 2× crop, Hole 1, aim line AND grid both off — flagstick base with no cup. The evidence for finding 0. |
| `screenshots/hole1_01_aim_line_visible.png` | Production Hole 1, full HUD. Cyan line from the ball up the pin line over the slope grid. |
| `screenshots/hole1_02_aim_pivoted.png` | Same eye, mid-sweep — the line has swung ~44° right while the ball has not moved (DoD 3). |
| `screenshots/hole1_03_hidden_after_release.png` | Aim released, before the ball drops — line and grid both gone (DoD 2). |
| `screenshots/aimline_01_down_the_line_yaw45.png` | TestGreen lab: down-the-line framing, aim 45°, line draping over the warped grid. |
| `screenshots/aimline_02_camera_orbit_same_aim.png` | TestGreen: camera orbited +100°, aim unchanged — same world track, zero rebuilds. |
| `screenshots/aimline_03_aim_pivot_yaw75.png` | TestGreen: camera unchanged, aim swung 45°→75° — pivots about the ball; off-bake tail past the green visible. |
