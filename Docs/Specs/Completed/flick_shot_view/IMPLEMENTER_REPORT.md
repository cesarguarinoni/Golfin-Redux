# Implementer Report — `flick_shot_view`

**Iteration shape:** `shot-view-layout:flick-cone-does-not-fit-the-0.38-anchor`

## Implementation summary

Flick joins the framing the other three schemes got in `shot_view_layout`:
`BallAnchorViewportY_Flick` 0.5 → 0.38, and the cone is re-cut so its base lands on the shared
`BottomBaselinePx` line instead of running off the bottom of the screen. No camera code was
touched — the aim camera pins the 3D ball to the `CentralBall` widget, so moving the widget IS the
camera change (12.500° → 4.612° of pitch, +12.6 points of horizon, measured).

The larger half of the work is that **the cone's height stopped being four numbers**. It lived on
`ShotConeView`, `ConeMeshGraphic`, `TimingSlabGraphic` and the mesh's own scene-authored `-1160`
simultaneously, which is exactly why nobody could move the ball: no single edit could re-cut the
cone. All four now derive from `ControlsConfig.FlickConeHeightPx`, and `ShotLayoutMath` reads the
same pair for the D6 clamp — so the guard and the drawn cone cannot disagree.

**Corrections to the spec's own premises are folded in; all are in § Spec premise correction and
none is silent.** The headline: the SPEC put the cone apex 151 px below the ball and the putter
track's top 187 px below it, but the scene shipped the apex ON the ball and the runtime had always
snapped the track's top onto it. Cesar, on seeing the build — *"The cone's top point should reach
the ball, not leave empty space"*, then *"Make the putter track top touch the ball too"* — so both
gaps are **0** and both heights carry the whole 792 down to the baseline.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Resources/Gameplay/controls.csv` | modified — `BallAnchorViewportY_Flick` 0.5 → 0.38; five new keys (`FlickConeHeightPx` 792, `FlickHandleStartY01` 0.778, `FlickPutterTrackHeightPx` 792, `FlickPutterTrackTopBelowBallPx` 0, `FlickConeApexGapPx` 0); header note updated (it claimed the Flick tiles were exempt "because Flick keeps the 0.5 anchor") |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | modified — the five fields + `Default` seeds; the "FLICK STAYS AT 0.5" comment block replaced with why it no longer does |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | modified — five `case` arms, so `controls.csv` and `Default` stay the two mirrors F13 requires |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutMath.cs` | modified — `FlickLaneDepthBelowBall`, `BallYForDepth`, and a depth overload of `ResolveBallY`; the three lane schemes keep their six-argument signature untouched |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs` | modified — `ClampDepthBelowBall` routes Flick into the D6 clamp off the two config keys; lane schemes still read their live view |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` | modified — `ApplyConfiguredGeometry` (public + idempotent) folds the config in on Awake and derives `ConeMesh.anchoredPosition.y`; `ApplyPutterTrackGeometry`; `InjectControlsConfig` test seam; `ConeHeightPx` / `HandleRestYPx` / `PutterTrackHeightPx` read-backs; `SetupSlab` now sizes the slab rect on the wired path too |
| `Assets/Scripts/Gameplay/UI/ShotUI/PutterTrackGraphic.cs` | modified — `HeightPx` property; the two band lines scale with it as fractions so a shorter track keeps its zones |
| `Assets/Scenes/Physics/LabScaffold.unity` | modified — 17 fields (cone 1160 → 792 on both components, `ConeMesh` −1160 → −792, `TimingSlab` 1009 → 792, handle rest 960 → 540, apex gap 0, putter track 1000 → 792 + its two band lines + its anchoredPosition −1453 → −1266, track top offset 0). **14 insertions / 12 deletions and nothing else** — see § Scene diff hygiene |
| `Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs` | modified — the two "Flick has no lane / stays at 0.5" fixtures replaced by three that assert the new framing, the short-screen clamp and the handle fraction |
| `Assets/Scripts/Gameplay/Tests/ShotConeViewConfigTests.cs` (+ `.meta`) | **created** — 6 tests pinning that the cone graphic, the mesh position, the handle rest and the putter track all follow ONE key, plus the no-config fallback and idempotency |
| `Assets/Scripts/UI/Editor/FlickShotViewVerify.cs` (+ `.meta`) | **created** — `GOLFIN ▸ ShotUI ▸ Verify Flick Shot View`: the real-entry-path acceptance run that writes the invariant JSON and the three frames |
| `Docs/AI_CONTEXT.md` | modified — new section; and the `shot_view_layout` section's "Flick stays at 0.5 … 1009 px" claim corrected rather than left to rot |
| `Docs/CONTROL_SCHEMES_PLAN.md` | modified — one block retiring the "keeps Flick byte-identical" invariant |
| `Docs/Specs/Active/flick_shot_view/{STATUS,IMPLEMENTER_REPORT,HEARTBEAT}` + `evidence/` + `screenshots/` | created — this report, the invariant JSON, the band re-check, three frames |

**Uncommitted paths OUTSIDE this task's folder that are NOT mine** (Rule 13; each appears in the
HEARTBEAT DIRTY block taken against `c6e7fcbb1`): the eleven `Assets/Art/3D/Trees(2025)/…​.mat`
tree materials, `Docs/TellCode.md`, `Assets/Art/In-Game UI/Halo - Selector.png(.meta)`,
`Claude outputs/ARCHITECT_DECISION_9_9.md`, and the `golfer_3d_test` / `selector_carousel` spec
folders. All predate this task — HEAD moved mid-session (`b7727ac01` → `c6e7fcbb1`, four commits
that are not mine, listed in `HEARTBEAT.log`), and the baseline was re-taken after the move.

## Screenshot

- **Canonical screenshot:** `screenshots/flick_pull_792cone.png` (1170 × 2532) — a REAL 55 % pull on
  the new cone, driven through `ClubHandleDragger`'s own `IPointerDown` / `IDrag` / `IPointerUp`
  handlers. It is the canonical frame because it is the only one that shows the thing that changed:
  the apex touching the ball, the base on the button baseline, the club head partway down. The
  release carried no upward motion, so `EvaluateFlickGate` rejected it and the ball stayed on the
  tee.
- Also: `screenshots/flick_038_hole2.png` (the new framing at address) and
  `screenshots/camera_before_vy050.png` (the aim camera as the OLD 0.5 anchor posed it — a
  camera-only A/B; see § Spec deviations), plus `screenshots/band_{duff,thin,good,pure}_*.png`.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` → real boot → `LabScaffold` + `Hole_02_Geo`
- **Play mode:** Yes. **Hole loaded:** Lomond hole 2 (the reference hole `shot_view_layout` measured
  on). **Resolution:** 1170 × 2532, pinned via `PendulumSchemeVerify.ForceCaptureResolution`.
- **Entry path:** boot `ShellScene` → tap the title screen's own `StartButton` → `PLAY` → hole card.
  No render harness, no synthetic button.

## Figma fidelity

`SPEC.md` cites node **14153:4602** (Figma *In-Game – Shot Tests*), so Rule 18 applies. This task
moves no UI element into a new design: it is a FRAMING task, and the node's contribution is the ball
height and what that implies for the horizon. The reference render
`Docs/Specs/Completed/shot_view_layout/reference/figma_14153-4602_shot_view.png` is the one the
architect pulled for that node; every row below is measured against it or against the accepted build
of it (`pendulum_038_hole2.png`), never against prose.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Ball height on screen | `14153:4602` | 62 % from the top (viewport 0.38) | `CentralBall` y **−303.84** = viewport **0.3800** | PASS |
| Horizon | `14153:4602` | high — sky and fairway both visible above the ball | **34.12 %** from the top; the SAME ruler reads **34.28 %** on `shot_view_layout`'s accepted `pendulum_038_hole2.png` (that task quotes 37.8 % for those pixels with its own ruler) | PASS |
| Aim-camera pitch | derived from the node's ball height | camera tilted up off the 0.5 framing | **12.500° → 4.612°** from an unchanged camera position | PASS |
| Bottom control row | `14153:4602` | controls on one bottom line | cone base **−1095.84** vs baseline **−1096.00**; action-button bottoms on the same line | PASS |
| Cone apex ↔ ball | `14153:4602` | cone reaches the ball | apex **−303.84**, ball **−303.84** — gap **0.00 px** | PASS |
| Cone base vs action buttons | `14153:4602` | cone must not run under the buttons | half-base at the 20° max **288.3** vs button inner edge **382.0** (94 px clear) | PASS |
| Power gauge | `14153:4602` | top-right, clear of the aim bar | unchanged by this task (viewport 0.70, `shot_view_layout`) | PASS* |

**Red-team addendum (2026-09-07).** The red-team gate wrote its OWN horizon detector rather than
reuse mine, found that a naive sky test breaks on the cloud cover in `pendulum_038_hole2.png` — which
is itself the proof that this metric is sky-dependent and why a raw percentage was the wrong gate —
and re-measured with a cloud-immune terrain-onset detector: **Flick 37.12 % vs the accepted Pendulum
36.22 %**, i.e. Flick is **0.91 points BETTER framed**, and clears the SPEC's literal 35 % on that
ruler. My own number (34.24 %) and the report prose (34.12 %) differ by ≤0.24 pt for the same reason:
`SkyRandomizer` draws a different sky per session and the detector reads sky. The geometry numbers,
which are sky-independent, reconcile to sub-pixel across every run.

`PASS*` = unchanged by this task and verified only as "still where `shot_view_layout` left it".
Every number above is read off live rects in play mode and is reproduced per-assertion in
`evidence/flick_shot_view_invariants.json` — **18 assertions, `fail_count: 0`**.

## UI fidelity lint

This task authors no prefab — it changes scene-object geometry — so the linter was run through
`UIFidelityLinter.LintRoot(SchemeRoot_Flick, "SchemeRoot_Flick")`, the same entry point
`miss_grade_duff` used for `FlickGradePop`.

| Prefab / root | Lint JSON | fail | warn |
|---|---|---|---|
| `SchemeRoot_Flick` (scene root, `LabScaffold.unity`) | `Docs/Diagnostics/_capture/SchemeRoot_Flick_lint.json` | **0** | 2 |

Both warnings are pre-existing and neither is this task's:
`BallSpace/PutterTrack/PutterTimingSlab ::flat-fill::` (the putt slab is an intentional flat white
quad, and predates this task) and `BallSpace/FlickGradePop/GradeText ::unlocalized-text::` (raised
and accepted in `miss_grade_duff`, whose own lint JSON carries the same warning).

## Spec premise correction

**The SPEC's "Today" column describes numbers that were never in the scene.** It says the cone is
1009 px tall with the apex 151 px below the ball and the handle rest at 785. Those are the C#
DEFAULTS on `ShotConeView` / `ConeMeshGraphic`. The scene — at `HEAD`, before this task —
serialized:

| | SPEC "Today" | Scene at HEAD (`git show HEAD:…LabScaffold.unity`) |
|---|---|---|
| `ConeMeshGraphic._heightPx` | 1009 | **1160** |
| `ShotConeView._coneHeightPx` | 1009 | **1160** |
| `TimingSlabGraphic._coneHeightPx` | 1009 | **1009** ← disagreed with the cone it lives inside |
| `ShotConeView._handleStartYPx` | 785 | **960** |
| implied apex gap | 151 (= 1160 − 1009) | **0** — the apex was ON the ball |

Three things follow.

1. **The apex gap is 0, not 151.** The spec's 151 is the arithmetic residue of the wrong height.
   Cesar confirmed the intent directly: *"The cone's top point should reach the ball, not leave empty
   space."* `FlickConeApexGapPx = 0`, `FlickConeHeightPx = 792` — the base still lands on −1096,
   because D2's real requirement is the base, and the split between gap and height was never the
   point. The key still exists so the gap is a number somebody can see and change.
2. **`TimingSlab` was sized for a cone that did not exist** — a 1009 px slab rect travelling inside
   a 1160 px cone. Latent, pre-existing, and now impossible: one key feeds all four objects.
3. **The putter track's top was never 187 px below the ball at runtime either.** D5 asks for 187;
   `PhysicsLabController.EnterPutterMode` → `AlignPutterTrackToBall` has always written the top ONTO
   the ball, and that file is under the standing `Assets/Scripts/Physics/` edit ban. I first
   implemented D5 as written and flagged the conflict; Cesar resolved it — *"Make the putter track
   top touch the ball too."* `FlickPutterTrackTopBelowBallPx = 0`, `FlickPutterTrackHeightPx = 792`,
   bottom still on the baseline. **The banned-file call and this view now agree**, so the re-assert
   in `UpdatePutterTrackVisibility` is no longer a correction of it — it is what sets the HEIGHT,
   which that call never touches, and what stops the pair diverging if either is tuned later.
4. **The handle-rest fraction changed more than D3 says it does, and Cesar picked the endpoint.**
   D3 says the rest "stays the same FRACTION of the cone" and gives 0.778 (= 785/1009). The real
   shipping fraction was **960/1160 = 0.828**, so "the same fraction" was never available as
   written. I built 0.778 first, showed the trade, and Cesar chose **pull parity**:
   `FlickHandleStartY01 = 0.6818`, i.e. **540 px** from rest to 100 % — exactly
   `PendulumPull100Px` and `FreeSwingPull100Px`, both read live off the config, so a thumb now
   travels the same distance in every scheme.

   The trade is real in both directions and is now asserted, not just noted.
   `ClubHandleDragger` reads `power = 1 − fraction`, so a shorter pull starts with more power
   already dialled in:

   | fraction | pull to 100 % | power the instant the club is touched |
   |---|---|---|
   | 0.828 — what shipped (960 of 1160) | 960 px | 17.2 % |
   | 0.778 — the spec's number | 616 px | 22.2 % |
   | **0.6818 — shipped here, Cesar's call** | **540 px** | **31.8 %** |

   `ShotLayoutMathTests` asserts the travel against `cfg.PendulumPull100Px` and
   `cfg.FreeSwingPull100Px` rather than against the literal 540, so a Pendulum retune that silently
   un-matched Flick would fail the build. It also pins the 31.8 % touch reading, so the other end
   of the trade cannot drift unnoticed. **If the cone is ever re-cut, this key has to be re-derived
   as `540/newHeight` or the parity lapses** — the fraction survives a re-cut, the parity does not.

## Acceptance checklist (SPEC § 4)

| Item | Result | Justification |
|---|---|---|
| Flick, Lomond hole 2, 1170×2532: `CentralBall` y −304 (vy 0.38) | PASS | Measured **−303.84**, viewport **0.3800**, read off the live rect in play mode (`ball_at_038`) |
| Horizon measured ≥ 35 % from the top | PASS* | **34.12 %** on this report's ruler. The gate is calibrated instead of raw: the SAME ruler reads **34.28 %** on `shot_view_layout`'s accepted `pendulum_038_hole2.png`, which that task quotes as 37.8 % — so this is the accepted framing measured by a stricter ruler, not a shortfall. Camera pitch is identical to Pendulum's to 4 decimals, which is the stronger proof |
| Aim-camera pitch before/after quoted (expect ≈ 12.5° → 4.6°) | PASS | **12.5004° → 4.6115°**, camera position identical `(122.05, 15.40, −134.25)` in both — the ball anchor was the only thing that changed. Matches the prediction exactly |
| Cone apex −455 | **superseded — PASS on the corrected target** | Apex is **on the ball** at −303.84 (gap 0.00 px), per Cesar's instruction and the scene's own shipped geometry. See § Spec premise correction |
| Cone base −1096 (on the baseline), height 641 | PASS (height 792) | Base **−1095.84** against a baseline of **−1096.00**; height **792**, because with a 0 gap the height carries the whole depth. `792 = 1096 − 304 − 0` |
| Handle rest at canvas y −597 | **superseded — PASS on the corrected target** | The −597 target was 0.778 × 641. Cesar chose pull parity instead: **0.6818 × 792 = 539.99 px** of rest — `PendulumPull100Px` and `FreeSwingPull100Px` are both 540, read live — putting the rest at canvas y **−555.85**. Config arithmetic and the live `ClubHandle` rect agree |
| Cone base does not overlap the action buttons (x ±233 at 20° vs buttons at ±382) | PASS | Half-base at the 20° max is **288.3** on the 792 cone (not 233 — the cone is taller than the spec assumed); the button inner edge measured off the live rects is **382.0**. 94 px of clearance |
| Full pull reaches 100 % at the base | PASS | `ClubHandleDragger.ProcessDrag` computes `power = 1 − handleY/ConeHeightPx` off `_coneGraphic.HeightPx`, which `ShotConeView.Awake` sets from the config — so the base is 100 % by construction, at any height. The 55 % pull frame reads 55 % on the gauge |
| Bands drawn at 0.15 / 0.45 / 0.85 of the cone | PASS | `ConeBandPalette` logged live as `red=0.15 gold=0.45 green=0.85`; the bands are fractions of `SetConeParams(_coneHeightPx, …)`, so they moved with the cone and needed no retune (D4) |
| PURE / GOOD / THIN / DUFF pops at the four bands still fire (`miss_grade_duff` live check re-run) | PASS | `GOLFIN ▸ ShotUI ▸ Verify Miss-Duff Flick Pops` re-run on the new cone through the real dragger — see `evidence/miss_grade_duff_rerun/flick_pops.md` and the four `screenshots/band_*.png` |
| Putt: track top −491, bottom −1096; putt slab travels the full track | **superseded — PASS on the corrected target** | Top is **on the ball** at −303.84 (gap 0.00 px) per Cesar's second instruction, bottom **−1095.84** on the baseline, height **792**. The slab's travel is `−_putterTrackHeightPx × (1 − p)` — the same field the track is sized from — so it spans the track by construction at any height |
| Putt camera unchanged | PASS | `_puttCamDistanceM` / `_puttCamHeightM` untouched; `git status --porcelain Assets/Scripts/Physics/` is EMPTY, so the standing zero-edits ban holds. `Assets/Scripts/Gameplay/Input/` is likewise empty — no `ShotController` change (D8) |
| 16:9 Game View preset: cone base on the baseline, ball raised, no button overlap | PASS | Asserted in `ShotLayoutMathTests.Flick_OnAShortScreen_…` at H=2080 and H=1560: ball rises above the authored anchor, base lands exactly on the baseline, ball stays on screen. Live values: H=2080 → ball −78, base −870 = baseline; H=1560 → ball 182, base −610 = baseline |
| Scheme switch at Idle Flick ↔ Pendulum: both frame at 0.38, no camera pop | PASS | Driven live through `ControlSchemeService.Set` (what the in-game gear segment calls): Flick −303.84, Pendulum −303.84, back to Flick −303.84; pitch 4.6115° in all three — a zero-degree pop |
| Flick tiles recaptured (3), other nine byte-identical (`git diff --stat`) | PASS | Re-shot TWICE — the second time because the handle rest moved to 540 and the tiles draw the club at its rest fraction, so the first set had gone stale. `git status` on the tiles folder lists exactly `T_Flick_1/2/3.png`; the other nine were md5'd against `git show HEAD:` and every one is IDENTICAL. All twelve `.meta` files are unchanged, so the Sprite import settings survive the overwrite (the "new PNG imports as a Texture, the pop-up draws a white box" trap), and `Resources.Load<Sprite>` returns a real 628×680 sprite for all twelve. A/B sheet: `screenshots/confirm_tiles_flick_before_after.png` |
| EditMode run count quoted; which (if any) Flick fixture changed and why | PASS | **2773 tests** — the final sweep is **2770 passed / 0 failed / 3 pre-existing skips**. Two earlier sweeps each dropped ONE order-dependent flake, a different test each time and both green in isolation; the third sweep is clean, which is what settles them as flakes rather than a regression. Detail in § Console output. Fixtures that changed: `ShotLayoutMathTests` only — see below |
| `ShotAimParityTests` / `ShotTimingPowerTests` / `ShotControllerFlickGateTests` pass unchanged | PASS | **Not one line edited** (`git diff HEAD -- Assets/Scripts/Gameplay/Tests/` lists `ShotLayoutMathTests.cs` alone). None of them encodes 1009 or 785 — `grep -rn "1009\|785\|960\|1160"` over the whole test tree returns three hits and all three are COMMENTS this task wrote (two in `ShotLayoutMathTests`, one in the new `ShotConeViewConfigTests`); zero assertions. They drive `ShotController` in normalised power, which no cone dimension reaches. `ShotAimParityTests` re-run under a class filter: 5/5 green |
| Bots drive `SetExternalPower` normalised — prove with existing bot tests; `bot_difficulty.csv` untouched | PASS | `BotSchemeParityTests` green in the full sweep and untouched; `bot_difficulty.csv` is absent from `git status` |

**Which fixture moved, and why.** `ShotLayoutMathTests` — and only because two of its tests asserted
the thing this task retires: `FlickAndNeedle_TakeTheirAnchorOnEveryAspect_BecauseNeitherDrawsALane`
and `Flick_StaysOnTheCanvasCentre_SoItsSceneAuthoredConeCannotLeaveTheScreen` (which asserted
`BallAnchorViewportY_Flick == 0.5` literally). They are replaced by
`Needle_TakesItsAnchorOnEveryAspect…` (the half that is still true) plus three Flick tests that pin
the new framing, the short-screen clamp and the handle fraction. **No test lost coverage:** Needle's
no-clamp case survives verbatim, and Flick gained the clamp case it never had.

## Known FAIL items

**None.** Every acceptance row is PASS. Two rows carry a qualifier rather than a failure and both
are argued in place: the horizon (`PASS*`, a ruler-calibration question settled against
`shot_view_layout`'s own accepted frame) and the three superseded apex/height/handle-rest targets
(corrected premises, § Spec premise correction).

One process note that is NOT a failure but should not be buried: **`GOLFIN ▸ Capture ▸ Scheme
Confirm Tiles` was stopped after its Flick pass.** It shoots all four schemes in one run; only
Flick's three tiles were wanted, and letting it re-shoot the other nine — to then revert them —
would have risked exactly the clipping regression `shot_view_layout` had to revert twelve tiles
over. The consequence is that `Docs/Specs/Completed/scheme_confirm_popup/tiles_manifest.json` still
describes the PREVIOUS capture session, so its three Flick rows are stale. That file is a record of
a completed task's run, read by nothing at runtime.

## Scene diff hygiene

`LabScaffold.unity` diffs at **14 insertions / 12 deletions** — the 17 intentional fields and nothing
else. That took an extra step worth recording: saving the scene through the Editor baked **21
layout-group-driven rects** (`GreenBtn`, `ParChip`, `PowerRow`, `HoleChip`, `TurnChip`, `TitleRow`,
the `InGameSettingsModal` prefab overrides and a TMP style hash) into a 244-line diff. Every one of
them is a direct child of a `VerticalLayoutGroup` / `HorizontalLayoutGroup` — recomputed at runtime,
runtime-inert — but noise of that size hides a real change. I filtered the diff to the hunks that
touch this task's fields, re-applied them onto `HEAD`'s scene, and reloaded. `m_IsActive` changes in
the final diff: **0** (Rule 14).

## Spec deviations

- **§ 2 / § 4 apex gap 151 → 0 and cone height 641 → 792.** The spec's premise was wrong about the
  scene; Cesar corrected the intent explicitly mid-run. Full detail in § Spec premise correction.
- **§ 3.2 `PutterTrack` re-expressed as ball-relative — but the anchor is unchanged.** The spec asks
  for it. It could not be done by re-anchoring: `PhysicsLabController.EnterPutterMode` calls its own
  `AlignPutterTrackToBall`, which assumes the rect is TOP-anchored to its parent, and that file is
  under `Assets/Scripts/Physics/` (standing ban, zero edits). So the rect stays top-anchored and
  `ShotConeView` derives the y from the parent's half-height instead of the scene's magic −1453 —
  the same number on a 2532 canvas and the right one on every other.
- **§ 2 / § 4 putter track top 187 → 0 and height 605 → 792.** Raised as a question, answered by
  Cesar mid-run: *"Make the putter track top touch the ball too."* This is the reading that also
  matches what the runtime already did — so the change removes a conflict rather than creating one.
- **The "before" frame is a CAMERA-only A/B.** Only the ball widget goes back to 0.5; `BallSpace`
  (and so the cone and club) stay where this task put them, because the cone that stood at the 0.5
  anchor was 1160 px tall and no longer exists to photograph. The pitch and horizon pair are honest;
  the 2D controls in that frame are not the old layout.
- **Horizon ruler.** See the acceptance row. My detector runs ~3.5 points below the one
  `shot_view_layout` used; rather than recalibrate it to hit a target, the assertion compares
  against that task's own accepted frame measured by the same ruler.

## What is measured but NOT photographed

**Putt mode has exact geometry and no frame.** The track's top (−303.84, on the ball), bottom
(−1095.84, on the baseline) and height (792) are read off the live rect by the acceptance run and
asserted three ways — but no screenshot shows the track, because reaching putt mode through the
player's own path means playing a ball onto the green, and the only shortcuts to it
(`PhysicsLabController.EnterPutterMode` by reflection, or the debug panel) are exactly the synthetic
entry points the real-entry rule exists to forbid. I would rather say that plainly than shoot a
frame through a door the player does not have. Say the word and I will play one onto the green.

## Console output

```
EditMode, full sweep: 2773 tests — 2769 passed, 3 skipped (all pre-existing HoleCompleteDriverTests
Stage-C1 skips), 1 failed.

  Run A:  Golfin.Gameplay.Tests.PendulumSchemeDriverTests.MarkerFreezes_AtTheUpswingReversal_NotAtRelease
          Expected: 0.309 +/- 0.02   But was: 0.0
  Run B:  Golfin.UI.Polish.Tests.UiMotionAllocationTests.CountUp_AllocatesOnlyWhenTheDrawnNumberChanges
          Expected: greater than 1   But was: 1
          (and PendulumSchemeDriverTests passed in run B)

  Run C:  2770 passed, 0 failed, 3 skipped — clean.
  Run D:  2770 passed, 0 failed, 3 skipped — clean (after the putter-track change).
  Run E:  2770 passed, 0 failed, 3 skipped — clean (after the pull-parity change).

A DIFFERENT test fails on some sweeps and both pass under a class filter
(PendulumSchemeDriverTests 28/28, UiMotionAllocationTests 5/5); run C, on the same code, is fully
green. Neither is in this task's code path — one is the Pendulum driver's marker-freeze timing, the
other a UI-motion allocation counter. Order-dependent flakes in the suite, surfaced rather than
papered over; NOT claimed as fixed, and NOT introduced here.

Compilation: clean. `EditorUtility.scriptCompilationFailed = False`. Warnings only, all pre-existing
(obsolete FindObjectsOfType, nullable analysis).

Play mode: no errors attributable to this task.
```

## Open questions for Architect

1. **Answered — pull parity, `FlickHandleStartY01 = 0.6818`.** Left here because the consequence is
   worth a device pass: the 540 px match costs a **31.8 % power reading the instant the club is
   touched**, up from 17.2 % on the old cone. That is inherent to `power = 1 − fraction`, not a bug,
   but it is the thing to feel for.
2. **`ConeIdleAlpha` is 0.25 but the cone is not drawn at address at all.** Measured, on sample
   points computed from the cone's own half-angle so "inside" really is inside — green channel,
   `screenshots/flick_038_hole2.png` (Idle) against `screenshots/flick_pull_792cone.png` (Timing),
   same hole, same session:

   | canvas y | half-width | Idle: inside vs outside | Pull: inside vs outside |
   |---|---|---|---|
   | −700 | 88 px | 186 vs 188 — **no cone** | 103 vs 188 — clear cone |
   | −900 | 132 px | 189 vs 187 — **no cone** | 177 vs 187 — cone |
   | −1050 | 165 px | 186 vs 186 — **no cone** | 173 vs 186 — cone |

   So the mesh renders fine; something gates it off in Idle (`ConeAlphaController`, or
   `ApplyDebugFlags` → `SetOutlineVisible(DebugFlags.ShowConeOutline)`). **Not this task's doing** —
   a height change cannot hide a cone, and the pull column proves the geometry draws — but it is
   why the canonical frame had to be a live pull rather than an address shot. Backlog, not a
   blocker.
