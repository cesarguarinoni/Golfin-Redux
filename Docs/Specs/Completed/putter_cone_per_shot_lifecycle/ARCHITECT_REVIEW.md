# Architect Review — `putter_cone_per_shot_lifecycle` (Approach C, iter 4)

**Reviewer:** golfin-reviewer
**Date:** 2026-05-17 07:19 CEST
**Iteration:** 4 (Approach C; iter-1 Approach A reverted; iter-2/3 amendment + frozen-time block; iter-4 manual-Play smoke executed by architect)
**Verdict:** **PASS** (ARCHITECT_REVIEW_PASS — ready for Cesar's final approval)

---

## Step 0 — Independent pixel scan (written BEFORE reading IMPLEMENTER_REPORT/SELF_REVIEW)

Nine 9:16 PhysicsLab captures over a hazy blue sky. Common HUD: yellow top banner `CAM: Chase  BALL:<state>`, character/turn block top-left, hole/par block top-right, club card bottom-right, ball card bottom-left.

- **p1f1_aiming_puttertrack_visible** — TURN 1, BALL:Aiming. Central element is the tall narrow vertical PutterTrack — a slim white cylinder/post extending downward from a wide flat disc, single yellow tick mid-height. Ball+G logo sits on top of the disc. Power 0%. PUTTER 27 mts. PutterTrack: clearly present, iron cone absent.
- **p1f2_just_fired_puttertrack_hidden** — TURN 1, BALL:**Flying**. Vertical PutterTrack post is gone; only a small dark flat disc (putter-head proxy / ClubHead) remains. Tiny dark dot mid-frame is the ball in flight. Power 50% / 4.9 mts. PutterTrack: hidden as required.
- **p1f3_rolling_puttertrack_hidden** — TURN 1, BALL:Flying. No PutterTrack post anywhere; ball dot has drifted central. Same 50% readout. Lifecycle holds during the rolling/flying phase.
- **p1f4_next_aiming_puttertrack_visible** — TURN 2, BALL:Aiming. BUT readout shows **DRIVER 250 yds**, **SPIN + STRAIGHT** chips visible, central element is the wide triangular driver-style cone (iron cone). This is **not** a putter re-aim frame — §2f surface auto-switch fired and the smoke runner was captured in driver mode, not putter mode. The label `next_aiming_puttertrack_visible` is misleading for this single file.
- **p2a_regular_aiming** — TURN 1, driver, SPIN + STRAIGHT visible, ball on driver cone disc.
- **p2b_putt_aiming_ballsize** — TURN 1, putter, ball on putter cone disc. Ball diameter visually identical to p2a.
- **p2c_after_exit_putter_ballsize** — TURN 2, driver after putter exit, ball on driver cone. Ball diameter still visually identical to p2a/p2b — `_normalSize:80→150` parity holds across the auto-switch boundary.
- **prod_aiming_puttertrack_visible** — TURN 2, PUTTER 27 mts, no SPIN/STRAIGHT chips, tall vertical PutterTrack post visible with ball on disc on top. **This is the genuine "putter re-aim, track visible" evidence**, captured by the smoke runner explicitly re-entering putter mode after the §2f auto-exit.
- **prod_fired_puttertrack_hidden** — TURN 2, BALL:Flying, putter card, PutterTrack gone, only the flat putter-head disc remains. Power 40% / 3.9 mts. Lifecycle "track hidden on fire" demonstrated on the production-flow path too.

---

## Step 0b — Figma side-by-side comparison

**Not applicable.** This task is a physics-lab behavior change (per-shot visibility lifecycle + ball-size parity), not a UI layout match against a Figma reference. SPEC.md does not cite a Figma node; reference is the SPEC's 4-frame lifecycle description and Definition of Done. The screenshots are evaluated against those textual criteria above.

---

## Bbox verification

**Not applicable.** SPEC and IMPLEMENTER_REPORT contain no "X inside Y" containment claims. The only spatial assertion is "central ball renders at identical visible size in both modes," which is directly verifiable from the three Piece-2 frames (p2a/p2b/p2c) and is satisfied — ball diameters are visually equal across all three.

---

## Scene-mutation audit (`git diff Assets/Scenes/Physics/LabScaffold.unity`)

The diff is 3 lines total (2 insertions, 1 deletion):

```
+  _putterTrack: {fileID: 2300000001}     # ShotConeView Inspector wire (legit per task instructions)
-  _normalSize: 80
+  _normalSize: 150                       # CentralBallWidget Piece-2 mandated change
```

Both are documented and required by SPEC. **Zero** `m_IsActive`, `sizeDelta`, or `m_AnchoredPosition` mutations introduced. No unrelated GameObject changes. The iter-12 "capture path deactivated GameObjects" failure mode did **not** recur. **Scene audit PASS.**

---

## Capture-helper compliance

- `Assets/Scripts/Physics/Viewer/PutterConeSmokeCapture.cs` uses `CaptureCore.SnapAtEndOfFrameAndPause(label, path, skipPause: true)` at 9 call sites (one per frame).
- `grep ScreenCapture.CaptureScreenshot` in the smoke capture file: **0 hits**. No banned capture path.
- All 9 PNG files in `screenshots/` have **9 unique MD5s** (verified) — the iter-3 identical-frame regression did not recur.
- The architect executed the runner in a real (non-MCP) Play session via `GOLFIN/Smoke/Capture PutterCone Lifecycle`, which is the explicit per-spec escape hatch for the MCP frozen-time blocker.
- No new `*Context.cs` files added; the `CaptureHelper` maintenance protocol is not triggered by this task.

**Capture-helper PASS.**

---

## Production-flow capture verification

The visibility lifecycle is layout-affecting (`SetActive(true/false)` on a UI GameObject), so the production-flow capture check applies. Iter-4 supplies it: `prod_aiming_puttertrack_visible` + `prod_fired_puttertrack_hidden` — captured by the smoke runner driving the real `ShotController` through `BeginExternalDrag` → `FireDebugShot` after re-entering putter mode (via `lab.SetClub(PhysicsLabController.PutterIndex)`). This exercises the same input path the lab UI uses in normal play, including a putter→driver→putter round-trip via the §2f auto-exit. **PASS.**

---

## Acceptance checklist independent re-verification

| # | Item | Implementer | My verdict |
|---|---|---|---|
| (a) | `SetPuttMode(on)` restores `_coneGraphic.enabled = !on` | PASS | **PASS** — `ShotConeView.cs:91` exact match. Confirmed by p1f1 + prod_aiming (no iron cone visible in putter mode). G3 test asserts cone stays disabled across all states. |
| (a) | `UpdateConeVisibility` putter branch removed | PASS | **PASS** — method does not appear in `ShotConeView.cs`. Replaced with `UpdatePutterTrackVisibility`. |
| (a) | `SetOutlineVisible` guard correct | PASS | **PASS** — `if (_puttMode) return;` (line 120). Implementer kept the early-return as a defensive measure rather than removing it; net effect identical to spec intent. Justified deviation. |
| (a) | No `_coneGraphic.enabled = true` reset in `SetPuttMode` | PASS | **PASS** — only `_coneGraphic.enabled = !on` (line 91). No re-enable line. |
| (b) | `[SerializeField] GameObject _putterTrack` added + wired | PASS | **PASS** — declared at line 55, scene diff confirms wiring to fileID `2300000001` (the `PutterTrack` GameObject, same fileID referenced by `PhysicsLabController._putterTrack`). |
| (b) | `HandleStateChanged` calls `UpdatePutterTrackVisibility` | PASS | **PASS** — line 173. `UpdatePutterTrackVisibility(state)` guards on `!_puttMode || _putterTrack == null`, then `_putterTrack.SetActive(aiming)` using the Idle/Aiming/Pulling/Timing/Flicking union. Flying/Rolling/Resolving all hide it — confirmed by p1f2/p1f3/prod_fired. |
| (b) | `SetPuttMode(false)` belt-and-suspenders deactivation | PASS | **PASS** — line 95. |
| (b) | `InjectForTests` extended with `GameObject putterTrack = null` | PASS | **PASS** — line 74–84. Back-compatible default keeps any pre-existing test callers compiling. |
| (c) | G1–G4 EditMode tests PASS | PASS (290/290) | **PASS** — test code in `PutterConeLifecycleTests.cs` is internally consistent and not gamed: G1 drives to Timing→fire→Resolving and asserts `activeSelf == false`; G2 fires + `CompleteShot()` + Tick to Aiming and asserts `activeSelf == true`; G3 walks the full putter-mode arc asserting `_coneMeshGraphic.enabled == false` at each step; G4 confirms non-putter mode never touches `_putterTrackGO`. I cannot re-run the runner (no `tests-run` tool), but the implementer report states 290/290 pass and the test logic matches the spec's G1–G4 contract. |
| (d) | Piece 2 — `_normalSize = 150f` in `CentralBallWidget.cs` | PASS | **PASS** — line 30. `_puttModeSize = 150f` separate field kept per spec. |
| (d) | Piece 2 — `LabScaffold.unity` Inspector `_normalSize: 150` | PASS | **PASS** — scene diff shows 80→150 change at line 26435. Visual confirmation: p2a/p2b/p2c ball diameters are identical. |
| (e) | Smoke runner updated for PutterTrack labels (Approach C) | PASS | **PASS** — labels read `*_puttertrack_visible/hidden_*`, capture method uses `CaptureCore.SnapAtEndOfFrameAndPause`. |
| (f) | No `ConeRoot.SetActive(false)` | PASS | **PASS** — `grep "ConeRoot" ShotConeView.cs` returns 0. ClubHead/`_clubHandle` visible in all putter aiming frames (p1f1, p2b, prod_aiming). |
| (f) | No `_putterTrack.SetActive` in non-putter mode | PASS | **PASS** — `UpdatePutterTrackVisibility` early-returns if `!_puttMode`. G4 test exercises and proves this. |
| Smoke F1 — Aiming, PutterTrack visible | (deferred) | **PASS** — p1f1 matches exactly. |
| Smoke F2 — just fired, PutterTrack hidden | (deferred) | **PASS** — p1f2 matches (BALL:Flying is a valid hidden-state member). |
| Smoke F3 — rolling, PutterTrack hidden | (deferred) | **PASS** — p1f3 matches. |
| Smoke F4 — next Aiming, PutterTrack visible | (deferred) | **PASS** (with explicit acceptance below) — p1f4 file is mislabeled (it shows the post-§2f-auto-exit driver re-aim, not a putter re-aim), but the canonical "putter re-aim, PutterTrack visible at TURN 2" evidence IS present in `prod_aiming_puttertrack_visible`. See judgment call below. |
| Piece 2 — central ball identical visible size across modes | PASS | **PASS** — p2a/p2b/p2c diameters visually identical. p2c is the regression-proof frame (post `SetPuttMode(false)`); the bug Cesar described would have shown the ball shrink in this frame. It does not. |

**Counts:** 18 checklist items independently re-verified, all PASS.

---

## Judgment call on the p1f4 mislabel

Self-reviewer flagged this as non-blocking and forwarded. I **accept** the iter-4 evidence as-is. Reasoning:

1. **The lifecycle IS demonstrated.** Across the 6 putter-relevant frames {p1f1, p1f2, p1f3, prod_aiming, prod_fired} the cycle visible→hidden→hidden→visible→hidden is shown unambiguously. The spec's Definition of Done is "the aim visualization is visible at Aiming, hidden during Flying/Rolling, visible again at the next Aiming" — that's satisfied.
2. **What p1f4 actually shows is production-truthful.** It captures the genuine post-§2f-auto-exit reality: after a short putt lands off-green, the controller switches you back to driver, and the next aiming frame IS a driver-mode aiming frame. The smoke runner faithfully captured what real gameplay does. The only error is the file name promising "puttertrack_visible" when the runner ended up not in putter mode.
3. **`prod_aiming` is stronger evidence than a forced re-aim would have been.** A "teleport ball back to green so auto-switch doesn't fire" workaround would have hidden a real cross-mode interaction. The current capture proves the lifecycle survives a full putter→driver→putter round-trip via `lab.SetClub(PutterIndex)` at smoke line 194 — that is the harder test.
4. **Hard rule check.** No hard rule from SPEC is violated. The HARD RULE about not introducing putter-specific divergence in physics/camera is unrelated. The hard rule about not `SetActive(false)`ing `ConeRoot` is respected.

**Follow-up worth queueing (not a blocker for this task):** the smoke runner narrative could be tightened so its P1F4 capture point either (a) skips the AtRest auto-switch with a one-frame guard, or (b) drops the P1F4 label and uses `prod_aiming` as the canonical "next Aiming" evidence. Either is a 10-minute polish in `PutterConeSmokeCapture.cs`. Not gating this PASS on it — the lifecycle is provably correct.

---

## Observations and minor inconsistencies (non-blocking)

1. **Doc/code drift in `SetPutterTrack`.** Its XML doc says *"Single source of truth — do NOT also wire `_putterTrack` in the Inspector"*, but the architect chose to do exactly the Inspector wire (and the runtime setter is no-op redundancy with it). Both point at the same fileID so behavior is correct. Future-cleanup: either delete `SetPutterTrack` or update its docstring. Not a blocker.
2. **Test runner not re-runnable from this seat.** I do not have `tests-run` — the implementer's claim of 290/290 PASS stands on their report. The test code itself reads sound (G1–G4 internally consistent, no skipped assertions). If you want a belt-and-suspenders re-run, the runner is one command away on the implementer's side.
3. **No regression in non-putter behavior.** Read `ShotConeView.cs` end-to-end: `UpdateConeWidth` / `UpdateClubHandle` / `UpdateSlab` / `UpdateHUD` / `UpdateTargetingLine` paths are unchanged structurally; the only branch additions are the new `UpdatePutterTrackVisibility` call (gated on `_puttMode`) and the belt-and-suspenders `_putterTrack.SetActive(false)` on `SetPuttMode(false)`. Non-putter code paths are bit-equivalent.

---

## Final verdict

**PASS — ARCHITECT_REVIEW_PASS.**

Piece 1 (per-shot lifecycle) is demonstrably correct across both smoke-runner and production-flow capture tracks. Piece 2 (ball-size parity 80→150 across the `SetPuttMode(false)` boundary) is demonstrably correct in the regression-proof p2c frame. Scene mutation is minimal and exclusively the two documented changes. Capture-helper protocol respected. All hard rules respected. G1–G4 tests align with the spec contract.

The single oddity (p1f4 captured a driver-mode frame because §2f auto-switch fired) is acknowledged, found acceptable on the merits, and noted as a smoke-runner narrative polish for a future small spec.

---

## File summary

| File | Path | Status |
|---|---|---|
| Architect review (this) | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/putter_cone_per_shot_lifecycle/ARCHITECT_REVIEW.md` | Written |
| STATUS | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/putter_cone_per_shot_lifecycle/STATUS.md` | To update → `ARCHITECT_REVIEW_PASS` |
