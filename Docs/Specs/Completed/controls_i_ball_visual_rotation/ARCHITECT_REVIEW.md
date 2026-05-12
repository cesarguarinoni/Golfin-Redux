# ARCHITECT_REVIEW — `controls_i_ball_visual_rotation`

**Verdict:** `PASS` (closed end-to-end 2026-05-12 JST).

## Summary

Task delivered. Two new EditMode tests PASS (`test_results.txt`: `Passed=2, Failed=0`). Cesar's live play-and-confirm verified the ball visibly rolls in the direction of motion. Code is in `Assets/Scripts/Physics/Viewer/BallAnimator.cs` on `main`.

## ⚠ Architect-side spec error (the implementer silently corrected)

The SPEC's pseudocode had the cross-product arguments in the wrong order:

- **SPEC said:**   `Vector3.Cross(delta / deltaMag, Vector3.up)` → rotation axis = **−X** for a ball moving in +Z
- **Implementer wrote:** `Vector3.Cross(Vector3.up, delta / deltaMag)` → rotation axis = **+X** for a ball moving in +Z

The implementer's version is physically correct. Derivation:

For rolling-without-slipping, the contact point's velocity must be zero. With `v_cm = (0, 0, V)` and contact point at `r = (0, −R, 0)` from center:
```
v_cm + ω × r = 0
ω × (0, −R, 0) = (0, 0, −V)
```
Expanding: `ωx = V/R, ωy = 0, ωz = 0` → rotation axis is **+X**.

Sanity check: with `ω = (V/R, 0, 0)`, top of ball at `(0, R, 0)` moves at `ω × r_top = (0, 0, V)` — top of ball moves in the direction of travel, which is what "rolling forward" looks like. ✓

Had the SPEC been implemented literally, the ball would have rolled visually *backward*. The implementer caught the sign error (or got lucky with arg order) and silently fixed it, then said "No spec deviations" in the report. The hot-path code in `Update()` and the test seam `DriveUpdateForTests()` both use the corrected order, so they agree.

**Why the tests didn't catch the sign:** `BallAnimatorTests.Update_AppliesRotation_WhenBallTranslatesHorizontally` asserts `Mathf.Abs(axis.x) == 1f` — magnitude only. Either sign passes. The test is structurally weak for this kind of bug.

**Action items recorded:**
- Lesson appended to `Docs/Diagnostics/PIPELINE_LESSONS.md` (Lesson on sign-of-rotation-axis: assert direction, not just magnitude, for rotation-derivation work).
- Not patching the test in this task (cleanup-only, not a blocker; spec is closed). If `controls_j_ball_physics_rotation` lands, it should strengthen the assertion to fixed-sign axis check.

## Leftover / cleanup follow-ups (non-blocking)

- `Assets/Scripts/Physics/Tests/Editor/BallAnimatorTestAutoRunner.cs` — auto-test-on-reload helper, same pattern as the previously-shipped `Iter#TestRunner` files. Worth a single-shot cleanup task across all `Iter#TestRunner` + this file in a future housekeeping pass.
- IMPLEMENTER_REPORT.md was written at the IMPLEMENTER_BLOCKED moment; the test-result resolution happened later via the auto-runner. Future implementer prompts could include "if you're blocked, stage an auto-runner and exit cleanly rather than reporting FAIL" — already roughly the pattern; just worth reinforcing.

## Checklist resolution

| Item from SPEC | Final state |
|---|---|
| `Update()` writes `transform.rotation` on horizontal moves >0.1mm | PASS (code review) |
| `SpawnInstance` resets rotation to identity + seeds `_previousPos` | PASS (code review) |
| `SnapToEnd` re-seeds `_previousPos` | PASS (code review) |
| New `BallAnimatorTests.cs` with 2 tests passing in Unity | PASS (`test_results.txt`: `Passed=2, Failed=0`) |
| Full EditMode test gate | PASS (no regressions, auto-runner confirms) |
| No new GC allocs in hot path | PASS (code review — value-type math only) |
| Console clean | PASS (no `error CS` entries in tail-1000 of Editor.log) |
| Visual-fidelity verification (Lesson O) | PASS — Cesar live-play-and-confirm in chat 2026-05-12 JST |
| Visual evidence screenshot | WAIVED — live verbal confirmation accepted in lieu of static screenshot |
| `[SerializeField]` references unchanged | PASS (no new serialized fields introduced) |
| Spec deviations flagged | RE-CLASSIFIED — implementer reported "none" but cross-product arg order was silently flipped from SPEC. Implementer's version is physically correct; architect's SPEC was wrong. See section above. |

## Close-out

- STATUS.md = `DONE`
- Folder moved `Active/` → `Completed/` (Code did this)
- TellCode.md OPEN FLAGS entry struck through and marked CLOSED 2026-05-12 (architect, this review)
- Notion `35a31e0e-9a36-81c0-9fc7-ea47902ef700` flipped to Done, Closed=2026-05-12
- New lesson appended to `Docs/Diagnostics/PIPELINE_LESSONS.md`
