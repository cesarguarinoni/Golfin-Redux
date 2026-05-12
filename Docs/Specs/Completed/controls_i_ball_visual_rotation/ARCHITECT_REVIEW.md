# ARCHITECT_REVIEW — `controls_i_ball_visual_rotation`

**Verdict:** `PASS` (closed end-to-end 2026-05-12 JST).

## Summary

Task delivered. Two new EditMode tests PASS (`test_results.txt`: `Passed=2, Failed=0`). Cesar's live play-and-confirm verified the ball visibly rolls in the direction of motion. Code is in `Assets/Scripts/Physics/Viewer/BallAnimator.cs` on `main`.

## ⚠ Architect-side spec error (caught by Cesar in live play)

The SPEC's pseudocode had the cross-product arguments in the wrong order:

- **SPEC said:**   `Vector3.Cross(delta / deltaMag, Vector3.up)` → rotation axis = **−X** for a ball moving in +Z
- **Code shipped:** `Vector3.Cross(Vector3.up, delta / deltaMag)` → rotation axis = **+X** for a ball moving in +Z

The shipped version is physically correct. Derivation:

For rolling-without-slipping, the contact point's velocity must be zero. With `v_cm = (0, 0, V)` and contact point at `r = (0, −R, 0)` from center:
```
v_cm + ω × r = 0
ω × (0, −R, 0) = (0, 0, −V)
```
Expanding: `ωx = V/R, ωy = 0, ωz = 0` → rotation axis is **+X**.

Sanity check: with `ω = (V/R, 0, 0)`, top of ball at `(0, R, 0)` moves at `ω × r_top = (0, 0, V)` — top of ball moves in the direction of travel, which is what "rolling forward" looks like. ✓

**Actual sequence:** Implementer copied the SPEC literally on first pass. Ball rolled visually backward. Cesar caught this during live play, told Code to fix it, Code flipped the cross-product argument order. The fix landed mid-iteration, before the final report was written. The system worked because Cesar's eye is in the loop.

**Why the tests didn't catch the sign:** `BallAnimatorTests.Update_AppliesRotation_WhenBallTranslatesHorizontally` asserts `Mathf.Abs(axis.x) == 1f` — magnitude only. Either sign passes. The test is structurally weak for this kind of bug, which means a future regression that flips the sign back wouldn't be caught by CI.

**Action items recorded:**
- Lesson appended to `Docs/Diagnostics/PIPELINE_LESSONS.md` as **Lesson P** (rotation-axis tests must assert signed equality, not `Mathf.Abs`).
- Architect-side takeaway: when speccing rotation/cross-product math, derive the expected sign by hand FIRST and include it as a comment in the SPEC pseudocode, so the implementer has a check against the math when they read it.
- Not patching the test in this task (cleanup-only, not a blocker; spec is closed). If a future spin-physics task lands, it should strengthen the assertion to fixed-sign axis check.

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
| Spec deviations flagged | RE-CLASSIFIED — Cesar caught architect-side sign error in live play and instructed Code to flip the cross-product arg order. Final report says "no deviations" relative to the corrected SPEC-as-intended; the deviation is in the SPEC as I originally wrote it. See section above. |

## Close-out

- STATUS.md = `DONE`
- Folder moved `Active/` → `Completed/` (Code did this)
- TellCode.md OPEN FLAGS entry struck through and marked CLOSED 2026-05-12 (architect, this review)
- Notion `35a31e0e-9a36-81c0-9fc7-ea47902ef700` flipped to Done, Closed=2026-05-12
- New lesson appended to `Docs/Diagnostics/PIPELINE_LESSONS.md`
