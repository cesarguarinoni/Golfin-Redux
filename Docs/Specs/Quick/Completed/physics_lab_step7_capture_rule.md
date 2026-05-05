# Quick task — `physics_lab_step7_capture_rule`

> Spec-authoring guidance, not a code change. Follow-up from `controls_c_fix` ARCHITECT_REVIEW.md (architect note #2, non-blocking).

## What

Spec author (the human Architect via Cesar's claude.ai chat) — when writing `Docs/Specs/Active/controls_c_fairway_rough_tuning/SPEC.md` (Phase B successor to `controls_c_fix`), Step 7 must mandate `CaptureHelper.SnapAtEndOfFrameAndPause("<label>")` for the lab-validation screenshots. **Do not allow `mcp__ai-game-developer__screenshot-game-view` as the primary capture path.**

## Why

`controls_c_fix` Step 7 used `screenshot-game-view` after a wall-clock wait. The Game View render texture didn't refresh between the two captures within the same `script-execute` call, so the two PNGs ended up 0.04% apart in byte size and visually identical (both showed the pre-shot tee frame, not ball-at-rest). The fix passed on stronger evidence (`[PhysicsLab]` readout + EditMode tests), but the visual evidence was load-bearing nothing. Don't repeat the pattern.

`CaptureHelper.SnapAtEndOfFrameAndPause` is the project-sanctioned capture-then-pause pattern (CLAUDE.md screenshot rule #4). It runs in a coroutine that yields to end-of-frame, snaps, then pauses — which guarantees the captured frame reflects ball-at-rest and that subsequent calls don't reuse a stale RT.

## Required spec language

Step 7 of the Phase B SPEC must include verbatim (or equivalent strict wording):

> **Lab validation captures.** For each lab shot, fire from PhysicsLab UI, then trigger capture via a coroutine running `yield return CaptureHelper.SnapAtEndOfFrameAndPause("shotN_<config>_atrest")`. Do **not** use `mcp__ai-game-developer__screenshot-game-view` for the at-rest frame — it does not synchronously refresh between calls in the same script-execute scope. The two captures must be byte-distinct AND visually distinct (ball position visibly different from the pre-shot tee). Self-reviewer will FAIL the task if both PNGs show the pre-shot tee frame, regardless of byte-count delta.

## Acceptance for THIS Quick task

1. The Phase B `controls_c_fairway_rough_tuning` SPEC.md includes the required Step 7 capture-rule language above (or equivalent).
2. This Quick task file moves to `Docs/Specs/Quick/Completed/` once that SPEC has been merged.

## Out of scope

- Don't change `CaptureHelper.cs` itself — the helper already works (it was used during `capture_helper` task back in late April).
- Don't retroactively re-capture `controls_c_fix` screenshots. The task is closed; future tasks pay the rule.
