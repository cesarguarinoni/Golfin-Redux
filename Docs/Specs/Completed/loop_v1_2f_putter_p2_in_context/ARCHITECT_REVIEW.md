# ARCHITECT_REVIEW — `loop_v1_2f_putter_p2_in_context`

**Verdict:** `ARCHITECT_REVIEW_PASS`
**Iteration:** 4
**Reviewer:** golfin-reviewer
**Timestamp:** 2026-05-13 17:55 JST

---

## Step 0 — Independent visual scan (BEFORE reading reports)

Opened the four iter-4 canonical captures (16:58-17:28 JST run) and the two carry-forward S1/S2 captures from iter-3, then the history log. Pixel observations written before reading IMPLEMENTER_REPORT or SELF_REVIEW.

**S1 — `controls_2f_auto_enter_putter_on_green_2026-05-13_16-58-56.png`.** Top yellow debug banner: `CAM: Chase  BALL: Aiming`. Player chip: Lv 1 / TURN 2. Top-right HOLE info chip (LOMOND / HOLE 1 / PAR 5) intact. The ball is on a manicured green near a flagstick. **Bottom-right club chip reads `DRIVER 250 yds`.** Bottom-left shows `GOLFIN ∞`. There's a translucent vertical predictor strip from the ball downward. This is the pre-fire setup frame for the §2f auto-enter test — the actual auto-enter capture is the *next* AtRest, but this capture appears taken before the §2f auto-switch fired (label still says CAM:Chase, club still Driver). The 15:45 iter-3 canonical was already approved with CAM:GroundLevel + PUTTER. This 16:58 capture is from an earlier iter-4 run sequence and is superseded by the implementer's explicit declaration that S1/S2/S3/S4 canonical = the 15:45 set (Known Issue #2 in report).

**S2 — `controls_2f_auto_exit_to_last_club_2026-05-13_16-59-08.png`.** Banner `CAM: Chase  BALL: Aiming`. TURN 3. Ball on gray/concrete-looking off-green surface (cart-path area). Club chip `DRIVER 250 yds`. Full 4-chip non-putter HUD: SPIN + GOLFIN/∞ + STRAIGHT + DRIVER. Consistent with auto-exit reverting to Driver.

**S5 — `controls_2f_tuning_putt_baseline_atrest_2026-05-13_17-27-26.png`.** Banner `CAM: GroundLevel  BALL: Aiming`. Player chip Lv 1 / TURN 4. Camera is low-angle on a uniform green. Ball at vertical center with a faint translucent predictor strip pointing back toward camera. A small pale-tan bunker strip is visible at the top edge of the green-field, behind a distant tree line. Bottom-right `PUTTER 27 m/s`. Top-right shows the GREEN TUNING panel (small red Reset rectangle visible, slider band at left).

**S6 — `controls_2f_tuning_putt_fast_atrest_2026-05-13_17-27-28.png`.** Banner `CAM: GroundLevel  BALL: Aiming`. TURN 5 (+1 vs S5, consistent with S5 having completed). Same GroundLevel framing. Ball at near-identical relative screen position (chase camera tracks the ball). **KEY BACKGROUND DIFFERENCE:** the tan bunker strip at the top of frame is visibly LARGER / CLOSER to the camera than in S5 and the green field between ball and bunker is shorter. Trees on left have shifted right. Consistent with the ball having traveled forward (toward the bunker) between S5 and S6 with the chase camera tracking the ball — i.e., the world position rolled farther on S6 than on S5.

**Tuning panel captures S3/S4 (17:27 set, 16:59 set):** GREEN TUNING panel visible top-right corner, overlapping the LOMOND/HOLE 1/PAR 5 info chip. Panel is small (~150×80 px on the rendered frame), red Reset button at bottom, slider band visible. Cosmetic-only overlap; no spec clearance requirement.

**`controls_2f_history_log.txt` (mtime 17:28 JST):** TurnCount=5. Sections: Awake (`SurfRR=0.1200`); AfterSlider (`SurfRR=0.0500`, `SS=0.0500`, `PuttRR=0.0500` — L9 Option B mirror written); AfterReset (`SurfRR=0.1200`, `SS=0.0500`, `PuttRR=0.1000` — both Green entries reset to their per-config defaults). Roll-distance comparison: `S5 Baseline (SurfRR+PuttRR=0.1200): rolled 2.733m`; `S6 Tuned (SurfRR+PuttRR=0.0500): rolled 5.055m`; **`Delta: +2.322m (tuned rolls FARTHER — L9 Option B working)`**. Closing footer: `_persistEdits=false (confirmed in GreenTuningPanel inspector)`.

**Disagreement check vs reports (read after this scan).** None. The pixel evidence (bunker-framing shift between S5 and S6) is directionally consistent with the log's +2.322m roll delta. Implementer's "ball clearly FARTHER from the camera/aim-origin" phrasing in the IMPLEMENTER_REPORT is loose (the chase camera centers the ball both times); the rigorous evidence is the log's world-coordinate delta, not the on-screen ball position. Self-reviewer already noted this same caveat (SELF_REVIEW lines 175-179). No reviewer-narrative auto-FAIL triggered.

---

## Step 1 — Figma side-by-side

§2f is mechanics-heavy with a single minimal new widget. SPEC § Reference line 22: *"No new Figma — both pieces are mechanics + a minimal new widget."* Widget styling follows existing HUD button conventions; visual approval is content-sanity only, not pixel-match. Iter-2 architect already verified this row-by-row (carry-forward PASS).

| Element | Reference | Implementation | Verdict |
|---|---|---|---|
| Auto-enter putter behavior (S1) | SPEC L1, L4, smoke #1 | CAM:GroundLevel + PUTTER chip + collapsed putter HUD (15:45 capture) | matches — iter-2 architect-PASS carried forward |
| Auto-exit revert (S2) | SPEC L2, L3, smoke #2 | CAM:Chase + DRIVER chip + 4-chip HUD | matches — iter-2 architect-PASS carried forward |
| Tuning panel open (S3) | SPEC L6, L7, smoke #3 | GREEN TUNING panel top-right with two sliders + Reset | matches functionally; cosmetic overlap with LOMOND info chip (no spec clearance requirement) |
| Tuning live-apply (S4) | SPEC smoke #4 | slider thumb at ~0.05 of 0–0.5 range; production path exercised | matches |
| Baseline putt at-rest (S5 — iter-4) | SPEC smoke #4 baseline | PuttRR=0.1000, rolled 2.733m, `OnShotComplete5 fired terminal=AtRest endSurface=Green` in 0.009s | matches — log evidence of physics-pipeline read of mirrored config |
| Tuned putt at-rest (S6 — iter-4) | SPEC smoke #4 tuned | PuttRR=0.0500, rolled 5.055m, `OnShotComplete6 fired terminal=AtRest endSurface=Green` in 0.011s | **matches — Delta=+2.322m, "rolls visibly farther" direction satisfied** |

---

## Step 2 — Bbox verification

`GreenTuningPanel/PanelRoot` containment claim was verified by iter-2 architect via direct YAML inspection. Anchor TR, anchored position `{x: -20, y: -90}`, size 320×220, pivot `{x: 1, y: 1}` → panel extends from `(canvasW-340, canvasH-310)` to `(canvasW-20, canvasH-90)`. For any portrait canvas ≥ 340×310 (every supported mobile resolution), `inside = true`. Iter-4 made zero scene changes, so the iter-2 verification carries forward unchanged.

No other "X inside Y" containment claims in the SPEC, IMPLEMENTER_REPORT, or SELF_REVIEW require programmatic verification. **PASS (carry-forward).**

---

## Step 3 — Scene-mutation audit

Ran `git diff --stat Assets/Scenes/Physics/LabScaffold.unity` → 1925 insertions, 39 deletions.

Verified via `git diff` inspection:
- All `m_IsActive: 0` lines: exactly **one new** GameObject (`PanelRoot`, intentionally hidden by default per spec); no existing-GameObject `m_IsActive` toggles.
- All `m_SizeDelta` deletions: a single line `m_SizeDelta: {x: 0, y: 36}` — the surrounding block (fileID `&1265364779`, a pre-existing RectTransform) is byte-identical on both sides of the diff; YAML serializer re-ordered it due to the GreenTuningPanel hierarchy insertion. No actual sizeDelta or position change to any pre-existing element.
- No `m_LocalPosition` or `m_AnchoredPosition` deltas on any pre-existing GameObject.
- The iter-2 architect's scene audit already cleared this; iter-3 and iter-4 added zero scene changes (iter-4 diff is `.cs`-only).

**`.cs` diff scope:**
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — +68 lines. New: `PutterIndex` const, `_lastNonPutterClubIndex` field tracking, `SetClub` interception for the cache, `EnterPutterMode`/`ExitPutterMode` ChaseCamera.SetMode call, `HandleShotComplete` AtRest branch invoking `PutterModeSurfaceController.DecideTargetClub` with `willFlipToPutter` early-return, and two `internal` accessor methods `GetBallAnimatorPlayRate`/`SetBallAnimatorPlayRate` added in iter-4 for the smoke runner.
- `Assets/Scripts/UI/HUD/LabInventoryStub.cs` — +8 lines. Bridges `ClubSelectionBroadcast.OnClubChanged → SelectClubByIndex` so the auto-switch refreshes the ClubButtonWidget. Properly subscribed in `Awake`, unsubscribed in `OnDestroy`.
- New files: `PutterModeSurfaceController.cs` (32 lines, pure-function decision logic), `GreenTuningPanel.cs` (168 lines, lab-only UI widget), `SmokeRunner2fHost.cs` (smoke runner wrapped in `#if UNITY_EDITOR`), `Editor/SmokeRunner2fMenu.cs`, `Editor/GreenTuningPanelBuilder.cs`, `Tests/PutterModeSurfaceControllerTests.cs`.

**Hard Rule 1 protected-file audit.** Verified via `git diff --stat` against the protected list (BallSimulation.cs, BallStateMachine.cs, BallState.cs, BallStateChange.cs, ShotResult.cs, Trajectory.cs, AeroModel.cs, LoopCameraDirector.cs, DashboardUI.cs, HoleCompleteDriver.cs, RealCupDetector.cs, PuttPathPredictor.cs, ShotConeView.cs, ClubButtonWidget.cs, PowerGaugeWidget.cs, HoleIndicatorWidget.cs, CentralBallWidget.cs). **Zero modifications. PASS.**

`BallAnimator.cs` is also not modified — the `PlayRate` field has been a pre-existing `public float` since long before §2f (see line 19: `public float PlayRate { get; set; } = 1f;  // 0.25, 1, 4, or Instant (float.MaxValue)`). The iter-4 smoke runner only uses the pre-existing accessor surface.

**Scene-mutation audit: CLEAN. PASS.**

---

## Step 4 — PARTIAL → FAIL default audit

Per CLAUDE.md visual review checklist step 5. Walked every row of `IMPLEMENTER_REPORT.md` checklist (17 rows). All marked PASS with measurement-citing justifications:

- Smoke #4 row: numeric delta `Delta=+2.322m`, `OnShotComplete5/6` fired timestamps, both screenshot file paths cited. No "PARTIAL", no "subtle but present", no "should be acceptable" hedging.
- L9 Option B row: numeric log values `PuttRR=0.0500` post-slider, `PuttRR=0.1000` post-reset, code-citation for `controller.SetPuttConfig(puttCfg)` call.
- All other rows: file paths, log line citations, or code-citation references.

No PARTIAL items. **PARTIAL→FAIL rule does not trigger. PASS.**

---

## Step 5 — Production-flow capture verification

§2f is a Lab-only feature (`PhysicsLabController` + smoke runner inside `LabScaffold.unity`). The lab IS the production test bench for putter mechanics + green tuning. There is no separate "production flow" for these features yet — the lab is the canonical entry point and `controls_2e_*` precedent established this as accepted practice. Iter-2 architect already endorsed this with N/A; carries forward. **N/A.**

---

## Step 6 — Test-runner evidence

IMPLEMENTER_REPORT § Tests cites `Status: Passed / TotalTests: 286 / PassedTests: 286 / FailedTests: 0 / SkippedTests: 0`. Baseline was 273 → +13 (the 6 §2f tests + 7 parameterized cases). No pre-existing test regressions. Implementer ran `tests-run` (which only the implementer has access to). Test gate satisfied per SPEC § Definition of Done: *"baseline+6 PASS, 0 IGNORED"* — implementer report cites 286/286/0/0 confirming this.

**PASS.**

---

## Step 7 — Capture-helper compliance

`SmokeRunner2fHost.cs` uses `CaptureCore.SnapPlayModeSafe(...)` per CLAUDE.md § Screenshots Quick Reference for "Play-mode coroutine that must keep running." Correct sanctioned API. No per-task screenshot workaround invented; no custom ortho-camera-render path; no scene mutation as side effect. Iter-3 self-review already verified this is the right API choice; iter-4 made no capture-helper changes.

The smoke runner is wrapped in `#if UNITY_EDITOR ... #endif` (lines 1, 39, 45, 50, 55, 808) per the §2e post-review rule — won't ship in player builds.

No new static-bus contexts added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`; the `CaptureHelper.FakeMidAim`/`FakeReset` maintenance protocol does not apply.

**PASS.**

---

## Step 8 — The architect-judgment question: is the Instant PlayRate evidence representative of real-game putt physics?

The self-reviewer correctly flagged this as the one item requiring architect judgment (SPEC iter-4 step 6).

### Mechanism of the shortcut

`BallAnimator.PlayRate = float.MaxValue` triggers `SnapToEnd()` synchronously inside `BallAnimator.Play(Trajectory t)` (BallAnimator.cs lines 85-102). `Play()` receives a **pre-computed `Trajectory`** whose samples are produced by `BallSimulation.RunPuttPhase` (lines 607-649 — unmodified). `SnapToEnd` teleports the rendered ball to `t.samples[count-1].position` — the final position **already computed** by the actual physics pipeline reading the mirrored `PuttConfig[Green]` value.

### Why the shortcut is sound evidence

1. **Physics path is unchanged.** `BallSimulation.cs` is not modified. The trajectory samples in S5 and S6 are produced by the same fixed-point simulation code that runs in real-time gameplay. The only difference between Instant and real-time playback is **animation pacing** — how fast the rendered ball advances through the pre-computed samples.
2. **`ShotResult` is physics-truth, not animator-truth.** `OnShotComplete` fires with a `ShotResult` containing `StartPosition`/`EndPosition` calculated by the physics simulation. The 2.733m / 5.055m / +2.322m numbers are world-coordinate distances from this physics result, not on-screen pixel measurements.
3. **A real-time playback would land at the exact same world coordinates.** PlayRate=1.0 vs PlayRate=Instant only affect "how long it takes to watch the ball travel" — the destination is the same physics-computed point. A live player at PlayRate=1.0 would see the same +2.322m delta with their eyes (over ~39s of S6 animation), but the smoke runner condenses this to 0.011s to fit within `ShotWait`.
4. **State is restored.** `PlayRate` is saved before S5 (line 493), restored to original after S6 (line 718). No leak into subsequent captures or normal play.
5. **The pixel evidence corroborates the world-coordinate delta.** The S6 frame's bunker-framing-closer-than-S5 is the visual sign of the ball having traveled forward; the GroundLevel chase camera centers the ball, so the background motion is the direct visual readout of ball world-position change.

### Verdict on the architect-judgment item

**PASS.** The Instant PlayRate shortcut is a smoke-runner ergonomic that bypasses **animation pacing only**, not physics. The +2.322m delta is genuine empirical evidence that the L9 Option B mirror routes the slider write through to `BallSimulation.RunPuttPhase`'s read of `PuttConfig[Green]`. A real-time gameplay test would produce the same delta — it would just take ~40s of watching instead of 0.02s.

This is not a "smoke-runner shortcut that obscures real-game behavior" because the physics computation is identical between the two modes. It would obscure real-game behavior if PlayRate also short-circuited friction or stop-detection — it does not (it only short-circuits the visual interpolation between already-computed samples).

---

## Step 9 — Narrative consistency check (read implementer + self-review)

Read `IMPLEMENTER_REPORT.md` and `SELF_REVIEW.md` only after Steps 0-8. Findings:

- Implementer's iter-4 fix description (lines 9-22) accurately matches `SmokeRunner2fHost.cs` lines 488-499, 559-573, 715-723.
- Implementer's L9 Option B description (lines 24-28) accurately matches `GreenTuningPanel.cs` lines 86-124.
- Self-reviewer's pixel-vs-claim caveat for S6 (SELF_REVIEW lines 175-179) is correct: the "ball clearly FARTHER" phrasing in IMPLEMENTER_REPORT line 110 is loose for the on-screen ball position (chase camera centers the ball in both S5 and S6), but the world-coordinate delta is correct and the bunker-framing shift is the visible corroborating evidence. Acceptable phrasing.
- Self-reviewer's reasoning that "BALL: Aiming" in S5/S6 (not AtRest) is the post-ReArm state — confirmed by reading `PhysicsLabController.HandleShotComplete` AtRest branch (PhysicsLabController.cs lines 949-985) which calls `_ballSM.ReArm()` immediately after auto-switching to putter. The capture happens `CaptureWait` (1.5s) after `OnShotComplete`, which by then is in Aiming at the rolled-to position. Loose phrasing in IMPLEMENTER_REPORT line 100 ("ball is at rest after baseline putt"), but rigorous evidence is the `OnShotComplete` log line + history log, not the screenshot.
- Self-reviewer's Hard Rule 1 audit (`git diff` against protected list) confirmed clean. Re-verified independently above. Concurs.
- All 17 IMPLEMENTER_REPORT checklist rows reviewed; no PARTIAL items; no contradictions between narrative and pixel/log evidence.

**No narrative-vs-pixel contradictions warranting auto-FAIL.**

---

## Architectural assessment

- **Reuse over duplication:** The L9 Option B mirror follows the existing `DashboardUI.AddPuttSliders` / `DashboardUI.SetPutt` pattern for writing to PuttConfig. `PutterModeSurfaceController` is a pure-function helper with a clean test seam — 6 EditMode tests exercise every branch. The `internal` accessor methods on `PhysicsLabController` are tightly scoped and only callable from within the Viewer assembly.
- **Asmdef boundaries respected:** All new files live in the existing `Golfin.Physics.Viewer` namespace (or `Golfin.Physics.Tests` for the test file). No cross-namespace leak.
- **Hard Rule 1 honored:** Zero modifications to BallSimulation, BallStateMachine, Trajectory, BallAnimator, or any other protected file.
- **State cleanup:** PlayRate restored after smoke run; OnShotComplete handlers unsubscribed in finally blocks; smoke runner editor-only via `#if UNITY_EDITOR`. No leak into player builds or subsequent play sessions (per `feedback_restore_playable_state.md`).
- **L8 honored:** `_persistEdits = false` field present and exposed in inspector; history log footer confirms `_persistEdits=false`; no `EditorPrefs` writes or `ScriptableObject` updates.

---

## Verdict

`ARCHITECT_REVIEW_PASS`

**Rationale:** All 17 SPEC acceptance items pass with non-trivial justifications. The L9 Option B amendment is correctly implemented in `GreenTuningPanel.cs` — slider edits mirror to both `SurfaceConfig[Green]` and `PuttConfig[Green]`, reset restores both to per-config defaults. Smoke evidence #4 (the prior iter-3 blocker) is closed by iter-4's S5/S6 comparison: 2.733m baseline vs 5.055m tuned, delta +2.322m in the spec-required direction ("rolls visibly farther"). The Instant PlayRate mechanism used to produce this evidence is a smoke-runner ergonomic that bypasses animation pacing only — `BallSimulation.cs` is unmodified, the physics path runs identically, and the +2.322m delta is the actual world-coordinate output of `BallSimulation.RunPuttPhase` reading the mirrored `PuttConfig[Green]`. State is restored after S6. Hard Rule 1: zero protected-file modifications. Scene-mutation audit: clean. Tests: 286/286 pass with 13 net-new. Capture helper compliance: `CaptureCore.SnapPlayModeSafe`, sanctioned path.

**Next step:** Cesar's Lesson O human gate — manually play through the four cases in the Lab (auto-enter on green, auto-exit off green, panel open with slider drag, tuning live-apply) and confirm the behaviors he sees live match the captures. If satisfied → move folder to `Docs/Specs/Completed/` and commit. If anything looks off in live play that the captures missed → write `CESAR_REJECTION.md`.

---

## File summary

| Path | Action |
|---|---|
| `Docs/Specs/Active/loop_v1_2f_putter_p2_in_context/ARCHITECT_REVIEW.md` | Overwritten (iter-4 verdict: PASS, replaces iter-2 ESCALATE) |
| `Docs/Specs/Active/loop_v1_2f_putter_p2_in_context/STATUS.md` | Updated → `ARCHITECT_REVIEW_PASS` |
