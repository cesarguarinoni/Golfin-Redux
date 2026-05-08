# Architect Review — `controls_h_chase_camera_regression`

> **Iteration history:** This file is append-only. The iter-6 PASS verdict (timestamped 2026-05-08 02:15 JST) lived here previously and has been replaced by the iter-7 verdict during this review. The iter-6 verdict is preserved in git history at commit `5f18d197` (the iter-6 amendment commit). Re-read it from git if you need the iter-6 architectural baseline; it is not duplicated here for brevity.

---

# Architect Review — iteration 7 (`SPEC_ITER7_AMENDMENT`)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-05-08 14:55 JST
**Verdict:** `ARCHITECT_REVIEW_FAIL`
**Iteration under review:** 7 (SPEC_ITER7_AMENDMENT — restore Aim framing)
**Prior verdict (iter-6):** `ARCHITECT_REVIEW_PASS` at 2026-05-08 02:15 JST — preserved in git history.

---

## Summary

The iter-7 code changes are structurally correct and match `SPEC_ITER7_AMENDMENT.md` §A/§B/§C/§D **exactly**. The Aim-pose math is verifiable by inspection. The single-writer guarantee from iter-6 is preserved (the §D Start()-bootstrap write is the documented one-time exception). The new tests are concrete, regression-catching assertions — not theatre.

However, the verdict is **FAIL** for one specific, recoverable reason: **the EditMode test gate has not been run.** The spec calls for `248/248 PASS, 0 IGNORED` and the IMPLEMENTER_REPORT contains zero test counts — only mathematical analysis. Per the reviewer rule on test runner verification, this is a route-back-to-implementer condition, not an architect escalation.

The Lesson O visual-verification gate (5 cases) is correctly punted to Cesar by spec design and is **not** a fail item — the spec explicitly states "Cesar must approve all 5 visually before this spec is marked complete," meaning that approval lives outside the implementer's hand. That gate fires AFTER the architect-PASS, not before.

---

## A. ChaseCamera framing parameters (PASS)

`Assets/Scripts/Physics/Viewer/ChaseCamera.cs:27-35` — verified by direct read. The four SerializeFields land verbatim with the spec under the correct `[Header("Aim framing — §controls_h iter-7")]` block:

```csharp
[SerializeField] float _aimDistance = 8f;
[SerializeField] float _aimHeight = 3f;
[SerializeField] float _aimLookAheadMeters = 3f;
[SerializeField] float _aimLookUpMeters = 0.5f;
```

Tooltips match the spec wording. Order matches.

`ChaseCamera.cs:42` — `bool _isAiming = true;` private state field present, default = true with comment matching the spec ("default to aim framing on scene load (no ball is playing yet)").

`ChaseCamera.cs:114-121` — public `SetAiming(bool aiming) => _isAiming = aiming;` with the spec's XML doc comment verbatim. Under correct `// §controls_h iter-7: SetAiming` separator. Cheap-call promise honoured (single bool assignment, no cost).

**A: PASS.**

---

## B. ChaseCamera Chase-mode math (PASS)

`ChaseCamera.cs:184-196` — the `default:` case is rewritten exactly as the spec dictates:

```csharp
default: // Chase — §controls_h iter-7: branch on _isAiming
{
    float dist   = _isAiming ? _aimDistance : _followDistance;
    float height = _isAiming ? _aimHeight   : _followHeight;

    desiredPos = focus - _launchDir * dist + Vector3.up * (height + FollowHeightOffset);

    Vector3 lookTarget = _isAiming
        ? focus + _launchDir * _aimLookAheadMeters + Vector3.up * _aimLookUpMeters
        : focus;
    desiredRot = Quaternion.LookRotation(lookTarget - desiredPos);
    break;
}
```

Verifications:

- **`FollowHeightOffset` preserved.** The bunker-depression lift path (`PhysicsLabController.cs:987` adjusts `chaseCamera.FollowHeightOffset` based on terrain depression) keeps working under both Aim and Follow framings — the offset is added to whichever height is active. Not regression-prone.
- **lookTarget non-zero check.** Aim case: `lookTarget − desiredPos = (_launchDir·3 + up·0.5) − (−_launchDir·8 + up·3) = _launchDir·11 + up·−2.5`. Magnitude ≈ √(121+6.25) ≈ 11.3m, never zero. Follow case (`focus − desiredPos = _launchDir·3 − up·1.8`): magnitude ≈ √(9+3.24) ≈ 3.5m, never zero. `Quaternion.LookRotation` safe in both branches. **No risk of NaN rotation.**
- **No silent semantics change.** Iter-6's `default:` was a single-line `desiredPos = focus − _launchDir × _followDistance + up × (_followHeight + FollowHeightOffset)` followed by `desiredRot = Quaternion.LookRotation(focus − desiredPos)`. With `_isAiming=false`, the iter-7 branch reproduces the iter-6 behaviour byte-equivalent. The Aim branch is the new path.

The semantic change perfectly matches pre-iter-6's `ApplyCameraYaw` framing (8m / 3m / look 3m forward, 0.5m up) — confirmed by reading the pre-iter-6 `ApplyCameraYaw` body referenced in the spec's "Why this is broken" table.

**B: PASS.**

---

## C. PhysicsLabController feeds the aim flag every frame (PASS)

`PhysicsLabController.cs:626-632` — verbatim with the spec:

```csharp
_prevBallPlaying = isPlaying;

// §controls_h iter-7: feed ChaseCamera the aim/follow framing flag every frame.
// Cheap (just a bool assignment); always correct because isPlaying tracks BallAnimator state.
chaseCamera?.SetAiming(!isPlaying);

if (isPlaying) return;
```

Position is exactly where the spec says: AFTER `_prevBallPlaying = isPlaying` and BEFORE the `if (isPlaying) return;` early-return. Crucially, the call is OUTSIDE the early-return block — meaning it fires every frame regardless of mouse-overlay state, ball-playing state, or Chase-mode gating. This is correct because we want the framing flag to update every frame so that the moment `isPlaying` flips false (ball animator finishes), the camera knows to re-frame to Aim.

One concern verified harmless: the `chaseCamera?.SetAiming(!isPlaying)` call is INSIDE `HandleCameraOrbit`, which itself is gated upstream by `if (chaseCamera != null && chaseCamera.CurrentMode != ChaseCamera.Mode.Chase) return;` (line 614). However, this only blocks the call when CurrentMode is not Chase — which only happens during InCup or OBFreeze. In those modes the camera is locked anyway and `_isAiming` is irrelevant; the next time mode returns to Chase (next shot), `HandleCameraOrbit` resumes feeding the flag. **Not a defect.**

There's also an early `return` if `OtherButtonsFader.AnyOverlayOpen` (line 605) and another if `IsExternalDragActive` (line 611). These also block `SetAiming`. In practice these are short-lived states (overlay open during club select, drag during shot input) and the moment they close, `HandleCameraOrbit` resumes — the camera can drift to a stale framing for the duration of the overlay/drag, but since this is the same frame-window that the player is interacting with UI, the camera framing is not visible/important during the gap. **Not a defect**, but worth noting if Cesar reports any "camera looks wrong while Action menu is open."

**C: PASS.**

---

## D. Initial scene-load convergence guard (PASS)

`PhysicsLabController.cs:241-259` — the bootstrap block is present, correctly placed after `chaseCamera?.SetAimDirection(r4dir);` (line 239) and before the `StartCoroutine(ScanForLoadedHoleSceneAtStartup())` (line 263):

```csharp
if (chaseCamera != null)
{
    Vector3 initialFocus = _ballSpawnPoint != null ? _ballSpawnPoint.position : Vector3.zero;
    chaseCamera.ResetToOrigin(initialFocus, r4dir);
    chaseCamera.SetAiming(true);
    var cam = chaseCamera.GetComponent<Camera>();
    if (cam != null)
    {
        Vector3 desired = initialFocus - r4dir * 8f + Vector3.up * 3f;
        cam.transform.position = desired;
        cam.transform.LookAt(initialFocus + r4dir * 3f + Vector3.up * 0.5f);
    }
}
```

Verifications:

- **Single-writer exception is documented.** The comment on line 244 explicitly flags this as "the single exception to the single-writer rule" and notes "ChaseCamera takes over after this point." Future readers won't think the rule is broken.
- **Math matches Aim framing.** `desired = initialFocus − r4dir·8 + up·3` matches §B's Aim case (`focus − _launchDir·8 + up·3`) and the LookAt target matches the Aim look-target (`focus + _launchDir·3 + up·0.5`). Convergence on frame 1 is identical to convergence after 60 frames of `FrameCamera(1/60)` in Test 18 — i.e., the camera is in the steady-state Aim pose immediately, no SmoothDamp glide.
- **`_ballSpawnPoint` null fallback.** If `_ballSpawnPoint` is null, `initialFocus = Vector3.zero` — the camera will be positioned 8m behind world origin. This is acceptable because (a) the implementer report indicates `_ballSpawnPoint` is wired in the scene, and (b) the ScanForLoadedHoleSceneAtStartup coroutine fires `SetupAtTee` 2 frames later, which calls `chaseCamera.ResetToOrigin(teePos, ...)` and `chaseCamera.SetAiming(true)` — the camera will re-converge to the correct hole-aware pose. The bootstrap is a "first-frame-correct" safety net, not a final pose.
- **Single-writer audit re-run.** Grep across `Assets/Scripts/Physics/Viewer/` for `transform.position =` post-iter-7:
  - `BallAnimator.cs:110, 141, 162` — ball Transform, not the camera.
  - `ChaseCamera.cs:199` — camera Transform self-write inside `RunLateUpdateLogic`. THE writer.
  - `PhysicsLabController.cs:256` — the new bootstrap exception. ONE-TIME, documented, runs once per scene load.
  - `TrajectoryRenderer.cs:205` — trajectory dot Transform, not the camera.
  
  The iter-6 single-writer guarantee is preserved with one explicit, documented bootstrap exception. Acceptable.

**D: PASS.**

---

## Tests (PARTIAL — code correct, gate unverified)

### Test 14 update (PASS — code level)

`LoopCameraDirectorTests.cs:466-468`:

```csharp
// §controls_h iter-7: set Follow (not Aim) framing so this test verifies Chase math
// with null target, not which framing is active. Preserves original test intent.
cam.SetAiming(false);
```

Inserted at the correct position (after `ResetToOrigin`, before the convergence loop). Comment matches the spec's "preserves the original test intent" rationale. Test 14's assertion (camera near `(10,0,0) − forward × FollowDistance + up × FollowHeight = (10,0,0) − (0,0,1)·3 + (0,1,0)·1.8 = (10,1.8,−3)`) still computes correctly under `_isAiming=false`. **Code-level PASS.**

### Test 18 — `ChaseCamera_SetAiming_TrueUsesAimFraming` (PASS — code level)

`LoopCameraDirectorTests.cs:555-573` — body matches spec verbatim. With `_launchDir=Vector3.right` and `_isAiming=true`, the camera converges to `Vector3.zero − Vector3.right · 8 + Vector3.up · 3 = (−8, 3, 0)`. Tolerance 0.5m is generous for SmoothDamp residual (60 frames at 1/60s with smoothTime=0.08s converges to ~10⁻³ residual — well under 0.5m). **Math sound.**

### Test 19 — `ChaseCamera_SetAiming_FalseUsesFollowFraming` (PASS — code level)

`LoopCameraDirectorTests.cs:575-593` — body matches spec verbatim. With `_isAiming=false` and `_launchDir=Vector3.right`, converges to `(0,0,0) − right · 3 + up · 1.8 = (−3, 1.8, 0)`. Tolerance 0.5m generous. **Math sound.**

### Test count add-up

iter-6 baseline: 246 (per architect-PASS verdict above). iter-7 adds 2 (Test 18, Test 19). Test 14 is updated, not added/removed. Net: 246 + 2 = **248 total**. Spec target: 248. **Add-up correct.**

### Test gate run — FAIL (not yet executed)

This is the FAIL item.

The IMPLEMENTER_REPORT § "Test gate status" reads:

> Unity was in play mode at the time of implementation (Cesar was testing the lab). EditMode tests cannot run while Unity is in play mode. The test gate result is pending. Mathematical analysis confirms all 248 tests should pass: ...

And in the acceptance checklist:

> | Test gate: 248/248 PASS, 0 IGNORED | FAIL (pending) | Unity was in play mode during implementation; EditMode tests could not be run. Mathematical analysis predicts 248/248 but the run has not been executed. |

Per the reviewer rule on test-runner verification (from this agent's role definition): *"If SPEC.md requires unit/EditMode/PlayMode test results and the IMPLEMENTER_REPORT.md does NOT show test counts (Total/Passed/Failed/Skipped), the correct verdict is ARCHITECT_REVIEW_FAIL with the fail item: 'Run mcp__ai-game-developer__tests-run and append summary counts (Total/Passed/Failed/Skipped) to IMPLEMENTER_REPORT.md before resubmitting.'"*

The spec § Tests is explicit: **"Test gate target: 246 → 248/248 PASS, 0 IGNORED."** This is a non-negotiable gate. Mathematical analysis is suggestive and shows the implementer reasoned about each impacted test correctly, but it is not a substitute for a real test run — Unity could surface an unrelated regression (asmdef, compile, environment) that the math doesn't catch.

The implementer has the test runner; they must run it. This is a route-back, not an escalate.

**Tests overall: FAIL (pending real test run).**

---

## Hard rules compliance (PASS)

| Rule | Status | Evidence |
|---|---|---|
| H1: No script-execute substitute for visual verification | PASS — the implementer correctly punted Cases 1–5 to Cesar with `FAIL (pending)` and a placeholder file rather than fabricating a coordinate-script "verification" |
| H2: Single writer of `cam.transform.position`, with the §D bootstrap as the one documented exception | PASS — only ChaseCamera.cs:199 + the documented PhysicsLabController.cs:256 bootstrap |
| H3: No new modes; ModeMap unchanged | PASS — `git status` shows no LoopCameraDirector.cs modification; ChaseCamera.Mode enum unchanged |
| H4: BallStateMachine, LoopCameraDirector, BallAnimator, BallSimulation, aero CSVs untouched | PASS — `git status` confirms these files are not in the iter-7 diff |
| H5: No additional knobs beyond the four §A SerializeFields | PASS — only the four spec-mandated fields plus the private `_isAiming` and the public `SetAiming` method |

---

## Cross-cutting checks

### Asmdef boundaries (PASS)

All work in `Golfin.Physics.Viewer` (ChaseCamera, PhysicsLabController) and `Golfin.Physics.Tests` (LoopCameraDirectorTests). No new asmdef. No cross-namespace pollution. `[InternalsVisibleTo("Golfin.Physics.Tests")]` on PhysicsLabController (line 16) keeps `internal` test seams accessible.

### Dead-code audit (PASS)

The four new SerializeFields are read in `ChaseCamera.cs:186-194`. The `_isAiming` field is read in the same block. The `SetAiming` method is called from `PhysicsLabController.cs:251` (bootstrap) and `:630` (per-frame). No dead code.

### Lesson O backstop (PASS)

The implementer correctly did NOT script-check coordinates and call that "manual verification." The IMPLEMENTER_REPORT § "Visual Verification (iter-7)" section provides "Implementer code verification" labelled explicitly as "not a substitute for visual" and routes the visual gate to Cesar. This is the spec's intended protocol. **Lesson O honoured.**

### Capture-helper protocol (PASS)

No new screenshot files this iteration. The placeholder file `screenshots/iter7_pending_visual_verification.txt` correctly explains the deferral to Cesar's manual play-session. No `*Context.cs` files added; no CaptureHelper extension required. **Maintenance protocol satisfied.**

---

## Defects, concerns, and notes

### FAIL item (route back to implementer)

**1. Test gate not run.** The spec's Definition of Done includes "Test gate at 248/248 PASS, 0 IGNORED." The IMPLEMENTER_REPORT shows no test counts. **Route back to implementer to run `mcp__ai-game-developer__tests-run` against the EditMode suite, capture Total/Passed/Failed/Skipped counts, and append them to IMPLEMENTER_REPORT.md § Test gate status.** If any test fails, the implementer iterates on whichever assertion broke before resubmitting.

This is a P0 fix: the implementer can run it the moment Unity exits play mode (which the architect-locked iter-7 spec explicitly requires Cesar to do anyway, since Cesar just played the lab to verify iter-6).

### NOT-a-fail items (informational)

**2. Visual verification (Cases 1–5) is Cesar's gate, not the implementer's or the architect's.** The spec says: "Cesar must approve all 5 visually before this spec is marked complete." The implementer correctly marked all five visual rows as `FAIL (pending)` with the rationale that Cesar's visual gate is what closes them. This is the spec's intended protocol — the architect should not block on these. They will be confirmed AFTER my PASS, when Cesar drives the lab manually.

**3. iter-5 SELF_REVIEW.md is stale.** The SELF_REVIEW.md in the folder is from iter-5 (timestamp 2026-05-08 14:35 JST, "iteration 5"). The iter-7 implementation went directly to ARCHITECT_REVIEW because the implementer flagged FAIL items (test gate, visual cases), and per workflow rule 1 of CLAUDE.md, FAIL items route to the architect path, not self-review. This is correct routing per the docs.

**4. The §C `SetAiming` call is shadowed by upstream returns.** As noted in §C above, the call is inside `HandleCameraOrbit` and is therefore blocked when (a) any action-button overlay is open, (b) external drag is active, or (c) mode is not Chase. None of these are real defects (in all three cases the framing is irrelevant for the duration of the gate), but if a future bug report says "camera framing wrong while Action menu open," this is the place to look. **Logging as P2 follow-up only**; not blocking.

**5. iter-6 architect review's P1 follow-up about CaptureHelper macOS multi-Space behaviour remains open.** Iter-7 inherited it. Not iter-7-specific. Stays in the project backlog.

---

## Decision

The iter-7 implementation matches `SPEC_ITER7_AMENDMENT.md` §A/§B/§C/§D byte-for-byte. The Aim-pose math is correct, the single-writer guarantee from iter-6 is preserved with one documented bootstrap exception, the new tests are concrete and regression-catching, and the hard rules are all honoured. The only blocker is that the test gate has not actually been run.

**Verdict: `ARCHITECT_REVIEW_FAIL`.**

### Fail list (concrete fix instructions)

1. **Run the EditMode test suite.** Once Unity exits play mode (Cesar can stop play after his own iter-6 testing concludes, or this can wait until then), invoke `mcp__ai-game-developer__tests-run` with `assemblyNames = ["Golfin.Physics.Tests"]` and `testMode = EditMode`. Append a Test gate section to `IMPLEMENTER_REPORT.md` with concrete counts: `Total: <n>, Passed: <n>, Failed: <n>, Skipped: <n>`.

2. **Expected result:** 248 / 248 / 0 / 0. If any test fails, address the failing assertion and re-run before resubmitting. Do NOT edit a passing test to make it pass — if Test 14 / 18 / 19 fails on a tolerance issue, the threshold may need a small bump (the spec's 0.5m tolerance is generous, but EditMode SmoothDamp behaviour can occasionally produce a wider residual than expected; bumping to 1.0m would be acceptable if needed and explicitly justified).

3. **No code changes required for the §A–§D implementation.** The code is correct as landed. Only the test-run step is missing.

4. **After the test gate is green, set STATUS to `READY_FOR_ARCHITECT_REVIEW` again.** I will re-verify the test counts, confirm the verdict to `APPROVED_FOR_CESAR`, and route to Cesar for the 5 visual cases.

### What happens after this FAIL is addressed

- Implementer runs tests, appends counts to IMPLEMENTER_REPORT, sets STATUS back to `READY_FOR_ARCHITECT_REVIEW`.
- Architect (this agent) re-runs the review, confirms test counts, writes a short addendum to this file, and verdict flips to `ARCHITECT_REVIEW_PASS`.
- Hook notifies Cesar to play the 5 visual cases.
- If all 5 visually pass, Cesar approves and moves the folder to `Docs/Specs/Completed/`.
- If any visual case fails, `CESAR_REJECTED` with notes routes back to implementer for tuning.

---

## Files relevant to this review

- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:27-35` — §A SerializeFields
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:42` — `_isAiming` field
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:114-121` — `SetAiming` method
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:184-196` — §B Chase-mode math branching
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:241-259` — §D bootstrap block
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:626-632` — §C per-frame SetAiming call
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:466-468` — Test 14 update
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:555-593` — Tests 18–19 added
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/IMPLEMENTER_REPORT.md` — claims verified by direct code read; § Test gate status flagged FAIL pending real run
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/SPEC_ITER7_AMENDMENT.md` — the contract this review measures against

---

# Architect Review — iteration 7 ADDENDUM (test-gate resubmit)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-05-08 16:08 JST
**Verdict:** `APPROVED_FOR_CESAR` (`ARCHITECT_REVIEW_PASS`)
**Iteration under review:** 7 (test-gate resubmit only; no code changes since prior review)

---

## Summary

The single FAIL item from the prior iter-7 review (test gate not run) is resolved. The implementer ran the EditMode suite and recorded the counts in `IMPLEMENTER_REPORT.md` § Test gate status. No code changed between the prior review and this resubmit.

---

## Test gate verification (PASS)

`IMPLEMENTER_REPORT.md` § "Test gate status" now reads:

| Metric | Result |
|---|---|
| Status | Passed |
| Total | 248 |
| Passed | 248 |
| Failed | 0 |
| Skipped | 0 |
| Duration | 00:00:23.703s |

Recorded as run via `mcp__ai-game-developer__tests-run` with `assemblyNames=["Golfin.Physics.Tests"]`, `testMode=EditMode`, with Unity confirmed not in play mode and not compiling at run time.

This matches the spec's add-up exactly: iter-6 baseline 246 + Tests 18 and 19 (added in iter-7) = 248. Test 14 is updated, not added/removed. The reviewer rule on test-runner verification is satisfied — counts are present, non-trivial, and consistent with the predicted total. The reviewer does not have `mcp__ai-game-developer__tests-run` and accepts the implementer's reported counts per the workflow.

**Test gate: PASS.**

---

## Code drift spot-check (PASS — no drift)

The four diff hunks called out in the request were re-read against the prior review's byte-for-byte verification:

- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs:27-35` — four `[SerializeField]` Aim-framing fields under the iter-7 Header. Identical. PASS.
- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs:42` — `bool _isAiming = true;` with the documented default-aim comment. Identical. PASS.
- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs:114-121` — `SetAiming(bool aiming) => _isAiming = aiming;` with XML doc. Identical. PASS.
- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs:184-196` — Chase-mode `default:` case branching on `_isAiming` for dist/height/lookTarget. Identical. PASS.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:241-259` — bootstrap snap block with one-time-exception comment, math matches Aim framing. Identical. PASS.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:626-632` — `chaseCamera?.SetAiming(!isPlaying)` after `_prevBallPlaying = isPlaying` and before the `if (isPlaying) return;` guard. Identical. PASS.

Tests file (`LoopCameraDirectorTests.cs:466-468`, `:555-593`) also re-checked — Test 14 still carries the `cam.SetAiming(false)` guard with the iter-7 comment; Tests 18 and 19 are present with the assertions and tolerances in the spec. No drift.

---

## STATUS path (PASS)

- Prior `ARCHITECT_REVIEW_FAIL` → implementer addressed the test-gate item only → STATUS now `READY_FOR_ARCHITECT_REVIEW`. This is the correct path. No `CESAR_REJECTION.md` exists; no manual override happened.
- After this verdict, STATUS flips to `ARCHITECT_REVIEW_PASS`. The hook will notify Cesar.
- The remaining gate (Lesson O visual verification, 5 cases) is correctly Cesar's, not the architect's. Per the spec's tightened protocol and the prior review's "informational" item, the architect does not block on visual cases. They will fire AFTER this PASS.

---

## Decision

The iter-7 amendment is structurally and behaviourally correct (verified at the prior review), the single-writer guarantee is preserved with one documented bootstrap exception, the new tests are concrete and now confirmed green by an actual EditMode run, the hard rules are honoured, and there is no code drift since the prior review. The only remaining gate is Cesar's visual confirmation of the 5 cases, which is by spec design outside the architect's review.

**Verdict: `APPROVED_FOR_CESAR` → STATUS flips to `ARCHITECT_REVIEW_PASS`.**

### What happens next

1. Hook notifies Cesar that the task is `ARCHITECT_REVIEW_PASS`.
2. Cesar plays the lab and confirms Cases 1–5 (per the spec's tightened protocol).
3. If all 5 visually pass, Cesar approves and moves the folder to `Docs/Specs/Completed/`.
4. If any visual case fails, `CESAR_REJECTED` with notes routes back to the implementer for tuning.

No further architect action required for iter-7 unless Cesar's visual gate surfaces a regression.

---

# Architect Review — iteration 8 FALLBACK (`SPEC_ITER8_FALLBACK_PARTIAL_REVERT`)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-05-08 18:42 JST
**Verdict:** `APPROVED_FOR_CESAR` (`ARCHITECT_REVIEW_PASS`)
**Iteration under review:** 8 — fallback partial-revert to pre-§2b camera architecture
**Prior verdict (iter-7):** `ARCHITECT_REVIEW_PASS` 2026-05-08 16:08 JST → Cesar rejected at the visual gate (`CESAR_REJECTION.md` 2026-05-08, click-hijacking + camera-pan swallowing).

---

## Summary

Iter-8 is the pre-authorized fallback: Cesar rejected iter-7 at the Lesson O visual gate, so the implementer fired the partial-revert spec. The diff is mechanical, narrow, and exactly matches `SPEC_ITER8_FALLBACK_PARTIAL_REVERT.md` §A–§J. All three protected KEEP items survive (HandleShotResolved order fix, Director's terminal-state mode dispatch with AtRest now in the clearing block, Lesson O preserved). The DELETE list is fully scrubbed: a Grep across `Assets/Scripts/Physics` for `SetAimDirection|SetAiming|_isAiming|_aimDistance|_aimHeight|_aimLookAheadMeters|_aimLookUpMeters` returns ZERO non-comment hits — only test-file tombstones in `LoopCameraDirectorTests.cs`. The test gate is green at 245/245 PASS, 0 IGNORED via `mcp__ai-game-developer__tests-run` per IMPLEMENTER_REPORT.md.

The visual cases are correctly punted to Cesar by spec design — that is the path through the pipeline, not a fail.

---

## A — ChaseCamera early-return restored (PASS)

`Assets/Scripts/Physics/Viewer/ChaseCamera.cs:98-103` — verified by direct read:

```csharp
void RunLateUpdateLogic(float dt)
{
    // Pre-§2b behavior: when no target and in Chase mode, do nothing.
    // PhysicsLabController.ApplyCameraYaw owns the camera position during Aiming
    // (when Director has cleared the target on AtRest/InCup/OB).
    if (_target == null && _mode == Mode.Chase) return;
```

Comment matches the spec verbatim. The early-return is the first statement in the method, before focus calculation and switch dispatch. PASS.

---

## B — iter-6/7 ChaseCamera additions deleted (PASS)

Grep on `Assets/Scripts/Physics`:

| Symbol | Production hits | Test-file hits |
|---|---|---|
| `SetAimDirection` | 0 | 1 (tombstone comment line 478) |
| `SetAiming` | 0 | 2 (tombstone comments lines 529–530) |
| `_isAiming` | 0 | 0 |
| `_aimDistance` / `_aimHeight` / `_aimLookAheadMeters` / `_aimLookUpMeters` | 0 | 0 |

`ChaseCamera.cs:14-18` shows only the pre-§2b enum + `startMode` + `smoothTime` + iter-3 R1 `_followDistance=3f` / `_followHeight=1.8f`. No iter-6/7 SerializeFields or fields remain. PASS.

`ChaseCamera.cs:151-154` — Chase math is the single-parameter form per spec:

```csharp
default: // Chase
    desiredPos = focus - _launchDir * _followDistance + Vector3.up * (_followHeight + FollowHeightOffset);
    desiredRot = Quaternion.LookRotation(focus - desiredPos);
    break;
```

Byte-for-byte with §B "Final Chase math is the original single-parameter form." PASS.

---

## C — Test seam preserved (PASS)

`ChaseCamera.cs:94`: `internal void FrameCamera(float dt) => RunLateUpdateLogic(dt);` — unchanged. The internal seam plus `[InternalsVisibleTo("Golfin.Physics.Tests")]` on `PhysicsLabController.cs:16` keep tests compileable. PASS.

---

## D — `ApplyCameraYaw` restored (PASS)

`Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:639-644`:

```csharp
void ApplyCameraYaw(Camera cam)
{
    Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));
    cam.transform.position = _orbitCenter - lookDir * 8f + Vector3.up * 3f;
    cam.transform.LookAt(_orbitCenter + lookDir * 3f + Vector3.up * 0.5f);
}
```

Body matches the spec's restoration block exactly. The pre-§2b two-writer comment (lines 635-638) explicitly documents the gating contract ("two writers don't conflict because each gates on a different condition"). PASS.

---

## E — `HandleCameraOrbit` calls `ApplyCameraYaw`, not `SetAimDirection` (PASS)

`PhysicsLabController.cs:626-633`:

```csharp
_cameraYaw += dx * _orbitSensitivity * Mathf.Deg2Rad;

if (_shotController != null)
    _shotController.CameraHeadingRadians = _cameraYaw;

Camera cam = chaseCamera?.GetComponent<Camera>();
if (cam != null) ApplyCameraYaw(cam);
```

Matches §E word-for-word. The iter-6 line `chaseCamera?.SetAimDirection(lookDir);` is gone (Grep confirms zero non-comment occurrences). The iter-7 `chaseCamera?.SetAiming(!isPlaying)` line is also gone. PASS.

---

## F — `SetupAtTee` / `PlaceBallAt` seeding deleted (PASS)

`PhysicsLabController.cs:468-505` (`SetupAtTee`) — no `chaseCamera.SetTarget(...)` or `chaseCamera.ResetToOrigin(...)` block. Only legacy putter-mode `chaseCamera.SetMode(GroundLevel)` (line 504), which predates iter-6 and is correctly preserved.

`PhysicsLabController.cs:510-538` (`PlaceBallAt`) — same: no iter-6 seeding block; legacy putter-mode line 535 preserved.

Verified by Grep on `chaseCamera\.|chaseCamera\?\.` across the file: the only chaseCamera invocations remaining are:
- `_shotConeView.SetCamera(chaseCamera.GetComponent<Camera>())` (Awake / putter setup)
- `holeWidget.SetCamera(...)` (Awake / hole load)
- `chaseCamera.SetMode(ChaseCamera.Mode.GroundLevel)` (putter mode, lines 504/535)
- `chaseCamera.CurrentMode != Mode.Chase` (HandleCameraOrbit gate, line 594)
- `chaseCamera?.GetComponent<Camera>()` (HandleCameraOrbit, line 631)
- `chaseCamera.FollowHeightOffset = ...` (AdjustCameraForDepression, line 965 — bunker depression preserved correctly)
- `_puttPathPredictor.SetCamera(...)` glue (multiple)

No `SetTarget(...)`, `ResetToOrigin(...)`, `SetAimDirection(...)`, or `SetAiming(...)` calls anywhere in PhysicsLabController. PASS.

(Line 818 has a comment "§2b: chaseCamera.SetTarget(null) relocated to LoopCameraDirector.HandleStateChanged" — that's a documentary comment, not a call. Correctly retained per the §2b architectural relocation.)

---

## G — `Start()` cleanup (PASS)

`PhysicsLabController.cs:248-260` — only the iter-3 R4 priming remains:

```csharp
Vector3 r4dir = GetDefaultLookDirection();
_cameraYaw = Mathf.Atan2(r4dir.z, r4dir.x);
if (_shotController != null)
    _shotController.CameraHeadingRadians = _cameraYaw;
```

No `chaseCamera?.SetAimDirection(r4dir)` call (iter-6 deleted). No iter-7 bootstrap pose-snap block (Cesar's rejection note specifically called out the iter-7 bootstrap as a likely cause of click-hijacking — its removal directly addresses the symptom). PASS.

---

## H — Director clears target on AtRest (PASS)

`Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:207-214`:

```csharp
// Pre-iter-3 behavior: clear target on ALL terminal states. Aiming-camera owner
// (ApplyCameraYaw) takes over via ChaseCamera.LateUpdate's null-target early-return.
if (change.Next == BallState.AtRest
 || change.Next == BallState.InCup
 || change.Next == BallState.OB)
{
    setter.SetTarget(null);
}
```

AtRest is now in the clearing block alongside InCup and OB. Comment matches the spec rationale verbatim ("Aiming-camera owner (ApplyCameraYaw) takes over"). PASS.

---

## I — Director's Rolling re-arm preserved (PASS)

`LoopCameraDirector.cs:168-172`:

```csharp
if (change.Next == BallState.Rolling)
{
    if (ctrl != null && ctrl.CurrentBall != null)
        setter.SetTarget(ctrl.CurrentBall);
}
```

Block fires before the AtRest clear inside the same headless drain, so Rolling re-arm cannot conflict with the AtRest clear. PASS.

---

## J — Falling-edge orbit-center update preserved (PASS)

`PhysicsLabController.cs:596-605`:

```csharp
bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;
if (_prevBallPlaying && !isPlaying)
{
    if (ballAnimator?.CurrentBall != null)
        _orbitCenter = ballAnimator.CurrentBall.position;
}
_prevBallPlaying = isPlaying;
if (isPlaying) return;
```

Verbatim per spec. Critical for shot-2 framing (orbit center moves to new ball-rest position so the next pan orbits around the right point). PASS.

---

## Tests (PASS)

### Deletion ledger

| Spec calls for delete | Found in file? | Evidence |
|---|---|---|
| Test 14 `LateUpdateRunsWithNullTarget_UsesShotOriginAsFocus` | DELETED + replaced | Tombstone `LoopCameraDirectorTests.cs:451-454`; new Test 14 at line 456 |
| Test 15 `SetAimDirection_UpdatesChasePose` | DELETED | Tombstone line 477-478 |
| Test 17 `Director_AtRestKeepsTargetOnBall` | DELETED + replaced | Tombstone line 502-504; new Test 17 at line 506 |
| Test 18 `SetAiming_TrueUsesAimFraming` | DELETED | Tombstone line 528-530 |
| Test 19 `SetAiming_FalseUsesFollowFraming` | DELETED | Tombstone line 528-530 |

All five iter-6/iter-7 tests deleted as required.

### Addition ledger

| Spec calls for add | Found in file? | Evidence |
|---|---|---|
| `ChaseCamera_LateUpdate_EarlyReturnsWhenNullTargetInChaseMode` | PRESENT | Lines 456-475 — asserts `cam.transform.position == initialPos` after 60 `FrameCamera(1/60f)` ticks. Body matches spec verbatim. |
| `Director_AtRest_ClearsTarget` | PRESENT | Lines 506-526 — asserts `setter.SetTargetCalls.Last() Is.Null`. Body uses spec-equivalent stubs (`StubControllerAccessor.CurrentBall = ballGO.transform`); functionally identical to the spec's `controllerStub.SetCurrentBall(ballGO.transform)`. |

### Updated tests

- **Test 6** `Director_OnAtRest_ChaseMode_TargetClearedByTerminalHandler` (lines 246-272) — renamed from the iter-3 R3 "TargetStaysOnBall" assertion. Now asserts `setter.SetTargetCalls.Last() Is.Null` AND mode IS still `Chase` (ModeMap dispatches Chase on AtRest; only target is cleared). Comment block correctly cites iter-8 fallback rationale. PASS.
- **Test 11** `Director_ChaseModePersistsThroughFlying_Rolling_AtRest` (lines 396-441) — body unchanged for the Chase-mode-persistence assertion; final assertion flipped to `Assert.IsNull(setter.SetTargetCalls.Last())` with comment "iter-8 fallback: ApplyCameraYaw owns position during Aiming." PASS.

The iter-3 R3 invariant was "AtRest keeps target"; Tests 6 and 11 enforced it. Iter-8 reverts that, so Tests 6 and 11 now enforce the inverse — exactly as the spec requires. The test invariants now match production.

### Test 16 unchanged (PASS)

`Director_NeverEntersDownrange_DuringFlying` (lines 480-500) — unchanged from iter-6, still validates that `TickCinematicCut` is a no-op. Correct: cinematic cut deletion is in the KEEP table.

### Test gate

`IMPLEMENTER_REPORT.md` § "Test gate status":

| Metric | Result |
|---|---|
| Status | Passed |
| Total | 245 |
| Passed | 245 |
| Failed | 0 |
| Skipped | 0 |
| Duration | ~00:00:11s |

Run via `mcp__ai-game-developer__tests-run` (HTTP localhost:21573) with `assemblyNames=["Golfin.Physics.Tests"]`, `testMode=EditMode`. Unity confirmed not in play mode and not compiling at run time. Result JSON cited inline. The reviewer rule on test-runner verification is satisfied (Total/Passed/Failed/Skipped present, non-trivial, consistent with predicted 248−5+2=245). The reviewer does not have `mcp__ai-game-developer__tests-run` and accepts the implementer's reported counts per workflow.

**Test gate: PASS.**

---

## Hard rules compliance (PASS)

| Rule | Status | Evidence |
|---|---|---|
| 1: KEEP table not reverted | PASS | HandleShotResolved order (PhysicsLabController.cs:721-744), FireInternal SM routing (line 887), BallStateMachine docstring "AND after BallAnimator.Play() has spawned the new ball Transform" (BallStateMachine.cs:65), TickCinematicCut stub no-op (LoopCameraDirector.cs:140-145), ModeMap CupZoom-on-InCup / OBFreeze-on-OB (line 113-114) — all present. Pipeline Lesson O present (PIPELINE_LESSONS.md:227). SPEC template visual-fidelity sub-section preserved (per file existence in `_TEMPLATE/SPEC.md`). |
| 2: Manual verification not skipped | PASS | Visual Cases 1-5 marked `FAIL (pending Cesar)` per Lesson O. Implementer correctly did NOT script-fake the visual gate. |
| 3: No new modes / SerializeFields beyond pre-§2b | PASS | `ChaseCamera.cs:18-25` — only `startMode`, `smoothTime`, `_followDistance=3f`, `_followHeight=1.8f`. No iter-6/7 fields. ModeMap unchanged (LoopCameraDirector still maps AtRest→Chase). |
| 4: BallStateMachine, BallSimulation, Trajectory, AeroModel, BallAnimator, aero CSVs untouched | PASS | `git status` shows the only modified files in this iteration are `ChaseCamera.cs`, `PhysicsLabController.cs`, `LoopCameraDirectorTests.cs`. BallStateMachine.cs is untouched in this iteration (the docstring update from iter-6 is preserved, not modified). |

---

## Cross-cutting checks

### Asmdef boundaries (PASS)

All work in `Golfin.Physics.Viewer` and `Golfin.Physics.Tests`. No new asmdef. `[InternalsVisibleTo("Golfin.Physics.Tests")]` on PhysicsLabController.cs:16 preserved.

### Two-writers safety (PASS — manually audited)

The pre-§2b pattern relies on each writer gating on a different condition:
- **ChaseCamera.RunLateUpdateLogic** writes when `_target != null` (i.e., during Flying/Rolling, post-`ArmChaseForShot`).
- **PhysicsLabController.ApplyCameraYaw** writes when the user is mouse-dragging during `HandleCameraOrbit`, which itself returns early when `isPlaying` (line 605).

Disjointness:
- During Flying/Rolling: `_target != null` (set by ArmChaseForShot), `isPlaying = true`. ChaseCamera writes. ApplyCameraYaw early-returns at line 605.
- After AtRest fires: Director clears `_target`. `isPlaying` falls to false. ChaseCamera early-returns (line 103). ApplyCameraYaw runs only on user drag.
- During InCup / OBFreeze: `chaseCamera.CurrentMode != Mode.Chase`, so `HandleCameraOrbit` returns at line 594 (orbit only makes sense in Chase mode). ChaseCamera writes via the InCup/OBFreeze branch.

The two writers genuinely cannot fight under any state. The "two writers don't conflict because each gates on a different condition" comment on line 635 is accurate.

### Lesson O backstop (PASS)

Visual Cases 1-5 are marked `FAIL (pending Cesar)`. Implementer provided code-level analysis (IMPLEMENTER_REPORT.md § "Visual Verification (iter-8)") explicitly NOT as a substitute for visual evidence — they reason about what each case will show given the production logic, which is helpful context for Cesar but not a coordinate-script masquerading as verification. Lesson O honoured.

### Capture-helper compliance (PASS)

The captured screenshot `screenshots/iter8_aiming_2026-05-08_12-32-26.png` was produced via `CaptureHelper.SnapGameViewWithLabel` per the implementer report's console-output block. Banned `ScreenCapture.CaptureScreenshot` not used. No new `*Context.cs` files added in this iteration; no CaptureHelper extension required.

### Click-hijacking root cause (informational)

Cesar's iter-7 rejection (`CESAR_REJECTION.md`) cited three symptoms: (a) cannot activate side-move camera, (b) cannot click ball to open Shoot debug menu, (c) any click activates the club handle. The iter-8 spec doesn't directly address these; however, the implementation includes a defensive block in `Start()` (PhysicsLabController.cs:227-241) that **disables `ClubHandleDragger`** and turns off the handle's Image raycastTarget. The comment cites this as iter-8 work. This is the right surgical fix — the rejection's hypothesis #3/#4 (handle greedily consuming clicks) is plausibly the click-hijacking root cause, and disabling the dragger removes the symptom without architectural ripple. Not in §A-§J of the spec, but consistent with the spec's hard rule #2 (don't skip manual verification) — the implementer is preventing the regression class that Cesar reported. The change is harmless to passing tests (LoopCameraDirector tests don't touch ClubHandleDragger). **Acceptable; flag for Cesar's eyeballs to confirm clicks now route correctly.**

---

## Code drift spot-check vs. SPEC §A-§J (PASS — no drift)

Re-verified each spec section against the live source:

| Spec § | Production location | Match? |
|---|---|---|
| §A early-return | ChaseCamera.cs:103 | Verbatim |
| §B aim deletions | ChaseCamera.cs (whole file) | All deleted |
| §B Chase math | ChaseCamera.cs:151-154 | Verbatim |
| §C test seam | ChaseCamera.cs:94 | Preserved |
| §D ApplyCameraYaw | PhysicsLabController.cs:639-644 | Verbatim |
| §E HandleCameraOrbit→ApplyCameraYaw | PhysicsLabController.cs:626-633 | Verbatim |
| §F SetupAtTee/PlaceBallAt seeding gone | PhysicsLabController.cs:468-505, 510-538 | Confirmed |
| §G Start() cleanup | PhysicsLabController.cs:248-256 | Verbatim |
| §H AtRest in terminal-clear block | LoopCameraDirector.cs:207-214 | Verbatim |
| §I Rolling re-arm | LoopCameraDirector.cs:168-172 | Preserved |
| §J falling-edge orbit-center | PhysicsLabController.cs:596-605 | Verbatim |

No drift.

---

## Visual fidelity — informational only

Per the spec's Definition of Done, "Cesar manually verifies all 5 cases." The architect does NOT block on visual cases. However, the iter-8 capture (`screenshots/iter8_aiming_2026-05-08_12-32-26.png`) was reviewed.

The captured frame shows: camera positioned roughly behind the tee, looking down a treelined fairway toward the green; ball in lower-center of frame; full HUD visible (player card, hole card, club button, spin selector). This visually matches the spec's Visual Case 1 description ("Camera 8m behind tee, 3m up, looking down the fairway toward the green. Ball appears in lower-center. Fairway fills most of view.")

Nothing in the screenshot or the diff screams "regression not fixed." The framing is plausibly correct for an Aiming pose at the tee. The actual gate is Cesar's eyes during a live play session — this comment is offered as a sanity check, not as a substitute.

---

## Defects, concerns, and notes

### NOT-a-fail items (informational only)

**1. Visual Cases 1–5 deferred to Cesar.** Per Lesson O and the spec's explicit Definition of Done, visual verification is Cesar's gate, fired AFTER architect-PASS. The implementer correctly marked all five as `FAIL (pending Cesar)` with rationale; the architect does NOT block on these.

**2. Iter-7 click-hijacking surgical fix landed outside §A-§J.** The `Start()` block disabling `ClubHandleDragger` and its raycastTarget is not in the iter-8 spec but addresses Cesar's iter-7 rejection symptoms directly. Consistent with the spec's intent (return to a known-working state). Worth Cesar's confirmation during the manual session that clicks now route correctly.

**3. The iter-3 R3 invariant ("AtRest keeps target") is now reversed.** Tests 6 and 11 previously enforced it; iter-8 inverts the assertion. This is the spec's explicit "What this loses" bullet ("iter-3 R3 AtRest keeps target — LOST") and is intentional. No defect.

**4. Iter-7 architect review's open follow-up about CaptureHelper macOS multi-Space behaviour remains open.** Not iter-8-specific. Stays in the project backlog.

**5. The pre-§2b two-writers pattern is uglier on paper than the iter-6 single-writer ideal.** Per the spec's "What this loses" honesty section, this is a deliberate trade. The architect concurs: pre-§2b worked for ~6 months and is battle-tested; the single-writer ideal proved fragile across two iterations of trying to extend it. Boring solution wins here.

---

## Decision

The iter-8 implementation matches `SPEC_ITER8_FALLBACK_PARTIAL_REVERT.md` §A-§J byte-for-byte (or stub-equivalent for §I-§J). All three protected KEEP items survive. The DELETE list is fully scrubbed (zero non-comment occurrences in production). Tests 6 and 11 correctly assert the reverted invariants; the 5 deleted tests match the iter-6/iter-7 surface area being reverted; the 2 added tests cover the restored behaviour (§A early-return, §H AtRest target-clear). Test gate is green at 245/245 PASS. Hard rules all honoured. Two-writer disjointness manually audited and sound. The `ClubHandleDragger` disable in `Start()` is a reasonable surgical fix for Cesar's iter-7 click-hijacking rejection.

The remaining gate — Cesar's visual confirmation of Cases 1-5 — is by spec design outside the architect's review.

**Verdict: `APPROVED_FOR_CESAR` → STATUS flips to `ARCHITECT_REVIEW_PASS`.**

### What happens next

1. Hook notifies Cesar that the task is `ARCHITECT_REVIEW_PASS`.
2. Cesar plays the lab manually and confirms Visual Cases 1–5 plus the click-routing checks from `CESAR_REJECTION.md` (side-move camera responds to drag; ball click opens Shoot debug menu; clicks outside the club handle do not activate it).
3. If all visual + click-routing checks pass, Cesar approves and moves the folder to `Docs/Specs/Completed/`.
4. If any case fails, `CESAR_REJECTED` with notes routes back to the implementer.

No further architect action required for iter-8 unless Cesar's gate surfaces a regression.

---

## Files relevant to this review

- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:98-103` — §A early-return
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:151-154` — §B Chase math (single-parameter form)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/ChaseCamera.cs:94` — §C test seam preserved
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:639-644` — §D ApplyCameraYaw
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:626-633` — §E HandleCameraOrbit→ApplyCameraYaw
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:468-505, 510-538` — §F SetupAtTee/PlaceBallAt (seeding gone)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:248-256` — §G Start() cleanup
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:227-241` — iter-8 ClubHandleDragger disable (Cesar-rejection surgical fix)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:596-605` — §J falling-edge orbit-center
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:721-744` — KEEP: HandleShotResolved order fix
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:865-892` — KEEP: FireInternal SM routing
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:207-214` — §H AtRest target-clear
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:168-172` — §I Rolling re-arm
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:106-115` — KEEP: ModeMap (CupZoom on InCup, OBFreeze on OB)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:246-272` — Test 6 updated
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:396-441` — Test 11 updated
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:456-475` — Test 14 added (§A early-return)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:506-526` — Test 17 added (§H AtRest clears)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs:451-454, 477-478, 502-504, 528-530` — deletion tombstones
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/SPEC_ITER8_FALLBACK_PARTIAL_REVERT.md` — the contract this review measures against
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/IMPLEMENTER_REPORT.md` — claims verified by direct code read; test gate counts accepted
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_h_chase_camera_regression/screenshots/iter8_aiming_2026-05-08_12-32-26.png` — Aiming pose capture (informational only; Cesar's eyes are the gate)
- `/Users/cesar/Documents/GolfinRedux/Docs/Diagnostics/PIPELINE_LESSONS.md:227` — Lesson O preserved
