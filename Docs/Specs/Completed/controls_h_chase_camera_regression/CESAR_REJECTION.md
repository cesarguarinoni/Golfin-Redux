# Cesar Rejection — iter-7 amendment (2026-05-08)

**Iteration rejected:** iter-7 (Aim-framing restoration)
**Verdict:** REJECTED at Lesson O visual gate
**Prior iter-5 rejection note** — preserved in git history (commit prior to fadaa8aa); superseded by this note.

## Symptom (Cesar, 2026-05-08 chat)

In PhysicsLab play mode after iter-7:

- **Cannot activate side-move camera.** Camera-pan input is being swallowed.
- **Cannot click the ball to open the Shoot debug menu.** Ball click is intercepted.
- **Any click anywhere on the screen activates the club handle.** The club handle is greedily consuming every click on the screen, regardless of where the click lands.

## Likely cause (hypotheses to investigate — implementer should triage)

The iter-7 changes that could plausibly cause this:

1. **Bootstrap Aim pose in `PhysicsLabController.Start()` (§D)** — the one-time snap to Aim pose (dist=8, height=3, looking +3m / +0.5m up from `_ballSpawnPoint`) may have moved the camera such that the club-handle collider/UI now occupies the entire viewport, or its raycast region.
2. **Per-frame `SetAiming(!isPlaying)` in `HandleCameraOrbit` (§C)** — running every frame *before* the `if (isPlaying) return` early-out. If the order of operations relative to `HandleCameraOrbit`'s click consumption changed, side-move camera input may be shadowed.
3. **`_isAiming = true` default in `ChaseCamera`** — if the club-handle hover/click logic checks the camera's framing mode and treats Aim-mode as "always engage handle," every click would route to the handle.
4. **Aim pose look-target offset** — look-target is `focus + _launchDir·3 + up·0.5`. If the club handle is anchored to the focus point and the new look-direction puts it dead-center under the cursor at default mouse position, raycasts would land on it on every click.
5. **Outside iter-7 scope but worth checking** — confirm the click hijacking didn't already exist on iter-6 (`5f18d197`). If it did, this is a separate bug, not an iter-7 regression. Reproduce by checking out iter-6, running PhysicsLab, and comparing.

## What the implementer needs to do

1. Reproduce in PhysicsLab — confirm the exact symptom.
2. Determine whether the regression is in iter-7 or older. Check iter-6 (commit `5f18d197`) for the same symptom; if it reproduces there, scope of fix expands.
3. Identify which of §A/§B/§C/§D introduced the click hijacking (or which older code the iter-7 framing surfaces).
4. Fix without losing the iter-7 Aim framing requirement OR the iter-6 single-writer guarantee.
5. Re-verify Cases 1–5 visually before handing back, plus the three click-routing checks:
   - Side-move camera pan responds to drag input.
   - Clicking the ball opens the Shoot debug menu.
   - Clicking outside the club handle does not activate it.

## Iter-6 single-writer guarantee — DO NOT regress

The fix MUST NOT reintroduce multi-writer chaos on `ChaseCamera`. The bootstrap Aim snap in `Start()` is the only documented exception. Any new writer needs an equally explicit comment.

## Test gate

After the fix, the EditMode test gate (`Golfin.Physics.Tests`) must still report 248/248 PASS, 0 IGNORED. New tests for click-routing are welcome but not required.

## Routing

STATUS = `CESAR_REJECTED`. Implementer reads SPEC_ITER7_AMENDMENT.md, ARCHITECT_REVIEW.md (iter-7 ADDENDUM), and this rejection note, then iterates.
