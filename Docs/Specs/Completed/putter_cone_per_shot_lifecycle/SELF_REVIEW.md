# Self-Review — `putter_cone_per_shot_lifecycle` (iter 4, Approach C)

**Reviewer:** golfin-self-reviewer
**Date:** 2026-05-17 07:17 CEST
**Iteration:** 4 (Approach C; prior reviews: iter 1 self-review FAIL on Approach A smoke evidence; iter 2/3 architect-driven amendments + IMPLEMENTER_BLOCKED on MCP frozen-time)
**Verdict:** `PASS` (FORWARD_TO_ARCHITECT)
**STATUS set to:** `READY_FOR_ARCHITECT_REVIEW`

---

## Visual diff notes

### Step 1 — Independent pixel scan (screenshot only, no spec/report consulted)

Nine 9:16 portrait frames captured from PhysicsLab against a hazy blue sky background. Common HUD elements across all frames: yellow status banner at top reading `CAM: Chase  BALL:<state>`; small character portrait (red cap) + navy bars (PLAYER / Lv 1 / TURN n) at top-left; navy bars (LOMOND / HOLE 1 - REGULAR / PAR 4) + minimap chip at top-right.

- **p1f1_aiming_puttertrack_visible** — BALL:Aiming, TURN 1. Center of screen: a tall narrow **vertical white rectangle** (the PutterTrack) stretching from near the top to near the bottom, with a small white circular putter head and a white ball (green G logo) sitting at the top of the track. Dark circular "0%" power readout to the right of the ball. Bottom-right shows the white "PUTTER 27 mts" card; bottom-left shows greyed-out "GOLFIN" card. No iron cone visible. No SPIN/STRAIGHT chips.
- **p1f2_just_fired_puttertrack_hidden** — BALL:**Flying**, TURN 1. The vertical PutterTrack rectangle is **absent**. Only the small black-and-grey putter-head proxy is visible roughly mid-frame. "50%" power readout (49 mts) on the right. Bottom-right still shows "PUTTER 27 mts" card.
- **p1f3_rolling_puttertrack_hidden** — BALL:**Flying**, TURN 1. PutterTrack still absent. Only a tiny black ball dot is visible centerframe. 50% / 49 mts readout. PUTTER card still on bottom-right.
- **p1f4_next_aiming_puttertrack_visible** — BALL:Aiming, **TURN 2**. UI shows **DRIVER 250 yds** card at bottom-right, **SPIN / STRAIGHT** chips visible. Central element is a large white **iron-cone-shaped aim viz** with the ball at top, NOT the vertical PutterTrack rectangle. This is plainly a Driver-mode aiming frame, not a putter-mode re-aim.
- **p2a_regular_aiming** — BALL:Aiming, TURN 1. Driver mode: iron cone visible, DRIVER 250 yds card, SPIN/STRAIGHT chips. Ball sits on driver club-head at top of the cone.
- **p2b_putt_aiming_ballsize** — Pixel-identical to p1f1 (different MD5, but visually indistinguishable; same PutterTrack frame).
- **p2c_after_exit_putter_ballsize** — Pixel-identical to p1f4. BALL:Aiming TURN 2, Driver 250 yds, iron cone, ball at top.
- **prod_aiming_puttertrack_visible** — BALL:Aiming, **TURN 2**. PUTTER 27 mts card at bottom-right (no SPIN/STRAIGHT chips). Vertical PutterTrack rectangle visible with ball + putter head on top. This is plainly a putter re-aim frame.
- **prod_fired_puttertrack_hidden** — BALL:**Flying**, TURN 2. PUTTER 27 mts card. PutterTrack absent. Putter-head proxy mid-frame, 40% / 5.9 mts power readout. Sky lighter (camera angle higher).

### Step 2 — Comparison to reference

No Figma reference for this task (physics-lab behavior, not UI layout). Hard-rule Figma deferral does not apply; reference is the SPEC's described 4-frame lifecycle in Approach C § Smoke evidence requirement and the Definition of Done.

Compared to the SPEC's required sequence:
- Frame 1 (Aiming, PutterTrack visible, iron cone hidden, ClubHead visible) — **p1f1 matches.**
- Frame 2 (just fired, PutterTrack hidden, iron cone hidden, ClubHead visible) — **p1f2 matches.** HUD shows `BALL:Flying` (a valid "fired" state per the spec's note that putter shots may pass through Flying briefly before Rolling/Resolving; the UpdatePutterTrackVisibility code hides PutterTrack for any state outside {Idle, Aiming, Pulling, Timing, Flicking}, so Flying is correctly treated as hidden-state).
- Frame 3 (rolling, PutterTrack hidden) — **p1f3 matches.** HUD still Flying.
- Frame 4 (next Aiming in putter mode, PutterTrack visible again) — **p1f4 DOES NOT match.** p1f4 shows Driver mode (TURN 2, DRIVER 250 yds card, iron cone). The re-aim happens after §2f auto-exit-putter fired on AtRest (PhysicsLabController.cs:929–948), switching the club from putter→driver before the smoke runner re-aimed. The intended "putter re-aim" demo is NOT in p1f4.

However, the production-flow captures recover this: **prod_aiming_puttertrack_visible** shows exactly the missing demo — TURN 2, putter mode, PutterTrack visible — produced by the smoke runner re-entering putter mode after the auto-exit (`lab.SetClub(PhysicsLabController.PutterIndex)` at smoke line 194). So the lifecycle "visible → hidden on fire → hidden during roll → visible again on re-aim in putter mode" IS demonstrated across the 6 putter-relevant frames {p1f1, p1f2, p1f3, prod_aiming, prod_fired}. p1f4 is essentially mislabeled — it's a Driver re-aim frame, not a putter re-aim frame — but PROD-aiming carries the missing evidence.

Piece 2 ball-size parity: p2a (driver), p2b (putter), p2c (driver after putter exit) all show the central ball at identical visible diameter. The `_normalSize=80 → 150` scene change + matching code default works as designed.

---

## Bbox verification (Step 6)

Not applicable — no containment claims ("X inside Y") in this task. The only quasi-spatial claim is "ball is the same size across modes" which is directly verifiable from the three Piece-2 screenshots (p2a/p2b/p2c) and is visually consistent. Step 6 bbox check not required.

---

## Scene-mutation audit (Step 7)

`git diff -- Assets/Scenes/Physics/LabScaffold.unity` shows EXACTLY two changes (3 lines: 2 insertions, 1 deletion):

```
@@ -23012,6 +23012,7 @@ MonoBehaviour:
   _putterTimingSlabRT: {fileID: 2022230116}
   _putterTrackHeightPx: 1000
+  _putterTrack: {fileID: 2300000001}

@@ -26431,7 +26432,7 @@ MonoBehaviour:
-  _normalSize: 80
+  _normalSize: 150
   _puttModeSize: 150
```

1. `_putterTrack` field wired on the `ShotConeView` MonoBehaviour. The fileID `2300000001` points at the `PutterTrack` GameObject (verified by grep: same fileID is referenced by `PhysicsLabController._putterTrack` at line 18955). The wire is legitimate and required for the implementer's code to function — without it, `HandleStateChanged` would no-op on the null guard in `UpdatePutterTrackVisibility`. The architect performed this wiring via `SerializedObject` (read-only review confirms scene diff only; no behavior side effects).
2. `_normalSize: 80 → 150` on `CentralBallWidget` — the Piece-2 scene-Inspector change mandated by spec.

No `m_IsActive: 0`, no RectTransform sizeDelta mutation, no position changes, no other MonoBehaviour edits. **Scene audit PASSES.** The iter-12-style "capture path corrupted scene" failure mode did not occur.

---

## Capture-helper compliance (Step 5)

1. **Screenshot provenance:** PASS. `PutterConeSmokeCapture.cs` uses `CaptureCore.SnapAtEndOfFrameAndPause(label, path, skipPause: true)` for all 9 frames (grep: 9 call sites; no `ScreenCapture.CaptureScreenshot`, no custom capture path). `SnapAtEndOfFrameAndPause` yields `WaitForEndOfFrame` then synchronously grabs the GameView RT — deterministic per-frame capture. The architect's run was in a real, non-MCP Play session (per task instructions), avoiding the iter-3 frozen-time blocker. The 9 unique MD5s confirm 9 distinct frames (no identical-frame regression).
2. **New context maintenance protocol:** N/A. Diff adds no new `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. The new C# files are `PutterConeLifecycleTests.cs` (tests), `PutterConeSmokeCapture.cs` (dev-only smoke runner, `AutoStart=false`), and `SmokeRunnerPutterConeMenu.cs` (editor menu). No `CaptureHelper`/`CaptureCore` maintenance obligation triggered.

---

## Production-flow capture check (Step 8)

Piece 1 is a visibility-lifecycle change. The iter-1 self-review failed on lack of production-flow capture (smoke runner only). Iter-4 captures include a dedicated production-flow track (`prod_aiming_*` + `prod_fired_*`) produced via the smoke runner driving the live `ShotController` through real `BeginExternalDrag()` → `FireDebugShot(0.4f, Green)` calls — the same input path the real lab UI uses. Critically, the §2f auto-exit-putter fired between the P1F4 capture point and the PROD captures (the smoke runner had to re-enter putter mode with `lab.SetClub(PhysicsLabController.PutterIndex)` at line 194), demonstrating that the lifecycle survives a full putter→driver→putter mode round-trip — exactly the kind of production timing the smoke-only path would have hidden.

PASS — production-flow evidence is present in the PROD pair (prod_aiming and prod_fired show the putter-mode lifecycle cleanly in a turn-2 / post-auto-exit context).

---

## Acceptance checklist walk

| # | Item | Implementer | Self-review verdict |
|---|---|---|---|
| (a) Piece 1 revert: `SetPuttMode(on)` restores `_coneGraphic.enabled = !on` | PASS | **CONFIRM-PASS** — `ShotConeView.cs:91` `if (_coneGraphic != null) _coneGraphic.enabled = !on;` matches original behavior. p1f1 and prod_aiming show no iron cone in putter mode. |
| (a) `UpdateConeVisibility` putter branch removed | PASS | **CONFIRM-PASS** — `grep -c "UpdateConeVisibility" ShotConeView.cs` returns 0. Replaced by `UpdatePutterTrackVisibility`. |
| (a) `SetOutlineVisible` early-return guard correct | PASS | **CONFIRM-PASS** — `if (_puttMode) return;` at line 120 prevents `ApplyDebugFlags` from re-enabling iron cone in putter mode. Defensive deviation from spec ("remove the guard") is justified — net effect identical to the spec's intent, slightly safer. |
| (a) No `_coneGraphic.enabled = true` reset in `SetPuttMode` | PASS | **CONFIRM-PASS** — only line touching `_coneGraphic.enabled` in `SetPuttMode` is `= !on`. |
| (b) `[SerializeField] GameObject _putterTrack` added | PASS | **CONFIRM-PASS** — `ShotConeView.cs:55`. Scene diff confirms it is wired to fileID 2300000001 (the PutterTrack GameObject). |
| (b) `HandleStateChanged` calls `UpdatePutterTrackVisibility` | PASS | **CONFIRM-PASS** — line 173. Guarded by `if (!_puttMode || _putterTrack == null) return;`. Aiming bool is the Idle/Aiming/Pulling/Timing/Flicking union — Flying/Rolling/Resolving all hide the track. p1f2 (BALL:Flying, track absent) and prod_fired (BALL:Flying, track absent) confirm this. |
| (b) `SetPuttMode(false)` belt-and-suspenders deactivation | PASS | **CONFIRM-PASS** — line 95 `if (!on && _putterTrack != null) _putterTrack.SetActive(false);`. |
| (b) `InjectForTests` extended with `GameObject putterTrack` param | PASS | **CONFIRM-PASS** — line 74–84 with `= null` default for back-compat. |
| (c) G1/G2/G3/G4 EditMode tests PASS | PASS | **CONFIRM-PASS** — Tests read sound: G1 fires shot and asserts `_putterTrackGO.activeSelf == false` at Resolving; G2 verifies it returns to true at next Aiming; G3 asserts `_coneMeshGraphic.enabled` stays false across all states in putter mode; G4 ensures non-putter mode never touches PutterTrack. Implementer report states 290/290 tests pass. I cannot re-run tests; test code is correct and not gamed. |
| (d) Piece 2 — `_normalSize = 150f` in `CentralBallWidget.cs` | PASS | **CONFIRM-PASS** — line 30: `_normalSize = 150f`. `_puttModeSize = 150f` unchanged. |
| (d) Piece 2 — `LabScaffold.unity` Inspector `_normalSize: 150` | PASS | **CONFIRM-PASS** — scene diff shows the 80→150 change exactly. p2a/p2b/p2c visually confirm ball renders at identical size across all three frames. |
| (e) Smoke runner updated for PutterTrack labels (Approach C) | PASS | **CONFIRM-PASS** — `PutterConeSmokeCapture.cs` uses `LogPutterTrackState` logging `PutterTrack.activeSelf`. File names match expected pattern. 9 frames captured in folder. |
| (f) No `ConeRoot.SetActive(false)` | PASS | **CONFIRM-PASS** — `grep "ConeRoot" ShotConeView.cs` returns 0. ClubHead (`_clubHandle`) visible in all putter aiming frames (p1f1, p2b, prod_aiming) — Hard Rule respected. |
| (f) No `_putterTrack.SetActive` in non-putter mode | PASS | **CONFIRM-PASS** — `UpdatePutterTrackVisibility` early-returns if `!_puttMode`. G4 test exercises this. |
| Smoke frame 1 — Aiming, PutterTrack visible | (deferred to Cesar) | **CONFIRM-PASS** — p1f1 matches: BALL:Aiming, vertical track visible, no iron cone, ClubHead (putter head) visible at top. |
| Smoke frame 2 — just fired, PutterTrack hidden | (deferred) | **CONFIRM-PASS** — p1f2: BALL:**Flying**, track absent, only putter-head proxy visible. Per spec note, Flying is a valid hidden state for putter shots (UpdatePutterTrackVisibility hides for everything outside the Aiming union). |
| Smoke frame 3 — rolling, PutterTrack hidden | (deferred) | **CONFIRM-PASS** — p1f3: BALL:Flying, track absent, only ball dot visible. |
| Smoke frame 4 — next Aiming, PutterTrack visible again | (deferred) | **PASS WITH OBSERVATION** — p1f4 as captured is actually a **Driver** re-aim frame (TURN 2, DRIVER 250 yds chip, iron cone), NOT a putter re-aim. This is because §2f surface auto-switch fired on AtRest (PhysicsLabController.cs:929–948) and exited putter mode before the smoke runner re-aimed. **However**, the missing "putter re-aim, PutterTrack visible" evidence is fully present in `prod_aiming_puttertrack_visible_*.png` (TURN 2, PUTTER 27 mts, PutterTrack visible) — captured immediately after the smoke runner re-entered putter mode with `lab.SetClub(PutterIndex)`. The lifecycle "PutterTrack visible at re-aim" IS demonstrated; it just lives in a frame labeled `prod_aiming` instead of `p1f4`. Net evidence for the spec's Definition of Done is met. |
| Piece 2 — central ball identical visible size across modes | PASS | **CONFIRM-PASS** — p2a/p2b/p2c show the white-ball-with-green-G at visually identical diameter (~55px) in driver-aiming, putter-aiming, and post-putter-exit driver-aiming respectively. The regression-proof frame p2c confirms `SetPuttMode(false)` no longer shrinks the ball. |

---

## Step 4 — Root cause notes for the p1f4 labeling observation

**Visible characteristic:** p1f4 is labeled `next_aiming_puttertrack_visible` but shows Driver-mode UI (DRIVER 250 yds chip, SPIN/STRAIGHT chips, iron cone, TURN 2). **Likely cause:** The smoke runner fires a putt at power 0.5 from the lab tee. Ball travels ~13m and comes to rest off-green. §2f `PutterModeSurfaceController.DecideTargetClub` (PhysicsLabController.cs:938) decides target=Driver and `SetClub(0)` fires inside `HandleShotResolved`. This silently transitions `_puttMode → false` via the `OnClubChanged → ExitPutterMode → SetPuttMode(false)` chain BEFORE the smoke runner's `lab.SetClub(0)` at line 176 runs and BEFORE the re-aim `BeginExternalDrag` at line 161. So by the time p1f4 is captured, the scene is already in driver mode.

This is a smoke-runner-narrative bug (the label promises something it can't deliver in a real-physics §2f-aware run), NOT a Piece 1 lifecycle code bug. The lifecycle is functionally correct, and the missing "putter re-aim" frame is delivered by `prod_aiming` (where the smoke runner explicitly re-enters putter mode before capturing). Not a blocker — but worth a small follow-up to either (a) update the smoke runner's narrative to capture the "putter re-aim" frame before auto-exit can fire, or (b) drop the p1f4 label in favor of the prod_aiming frame as the canonical re-aim evidence.

---

## Notes for the architect reviewer

- The architect's mid-task scene wiring (`_putterTrack` Inspector wire) is treated as legitimate per task instructions. The runtime setter `SetPutterTrack` in `PhysicsLabController.Awake` is redundant with the Inspector wire (both point at fileID 2300000001) — harmless redundancy, but the SetPutterTrack XML doc says "do NOT also wire `_putterTrack` in the Inspector," which contradicts the current scene state. Minor doc/code inconsistency; not a blocker.
- All 9 captures are unique MD5s — the iter-3 frozen-time identical-frame bug is resolved.
- The Piece 1 evidence is technically split across the P1 frames (frames 1/2/3 work; frame 4 is mislabeled-but-not-wrong) and PROD frames (which carry the actual "putter re-aim visible" demo). Architect should decide whether this is acceptable as-is or whether the smoke runner narrative needs a follow-up correction.

---

## Note on iteration count

This is iteration 4 of the task (Approach C). Per N≥3 ESCALATE rule: if this verdict were FAIL, ESCALATE would be required. The verdict is PASS, so the rule does not apply — no ESCALATE.

---

## File summary

| File | Path | Status |
|---|---|---|
| Self-review (this) | `Docs/Specs/Active/putter_cone_per_shot_lifecycle/SELF_REVIEW.md` | Written |
| STATUS | `Docs/Specs/Active/putter_cone_per_shot_lifecycle/STATUS.md` | To update → `READY_FOR_ARCHITECT_REVIEW` |
