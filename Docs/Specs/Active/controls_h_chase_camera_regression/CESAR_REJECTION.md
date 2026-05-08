# Cesar Rejection — iteration 5 (2026-05-08)

Iter-4 self-reviewer escalated two questions to Cesar. Cesar's chat-side
rulings + a freshly-surfaced regression are all captured in SPEC.md
§ "Iteration 5 amendments" — read that section before doing any work.

## Quick recap of the rulings

- **Q1 → Option B.** Camera must chase the ball through the visual
  roll-out. Downrange cinematic releases at touchdown, then Rolling stays
  in Chase. The current behaviour ("snaps to ground violently and stays
  parked while the ball rolls away") is unacceptable.
- **Q2 → manual visual verification.** No new screenshot files required.
  Cesar verifies live in chat. The off-screen-RT capture path keeps
  producing temporally misaligned frames; not worth iterating on.

## What changed in SPEC

- Hard rule 2 loosened: `LoopCameraDirector.cs` and `ChaseCamera.cs` are
  now editable.
- Hard rule 6 superseded: only R3-revised + R5 descriptions required in
  IMPLEMENTER_REPORT § Visual Verification.
- DoD updated: dropped screenshot artefacts; added R3-revised, R5, and
  associated tests.

## What's still broken in real play

Per Cesar's 2026-05-08 chat:

1. **Downrange → ground snap is violent.** After Chase phase the camera
   slams to a static ground-parked pose and stays there while the ball
   rolls past it. R3-revised + R6 in SPEC.
2. **Aiming-for-second-shot sideways pan is broken.** First-shot pan
   works (iter-3 R4 fix carried). Second shot's sideways pan does not
   respond at all. R5 in SPEC.

## What stays from prior iterations

- Code Fixes A, B, C, D, E, F, G — keep.
- ChaseCamera follow-distance / height tuning from iter-3 (R1) — keep.
- AtRest staying in Chase mode (iter-3 ModeMap addition) — keep, will
  flow naturally from the R3-revised logic.
- Iter-3 Start() priming via `GetDefaultLookDirection()` (R4) — keep.
- 110-leaf / 244-tree-node EditMode gate from iter-4 — keep as the
  baseline; new tests will bump the count.
- Two-consecutive-shots instance-ID log evidence — keep.

## Out of scope (still)

- Wood club-head asset bug — already filed at
  `Docs/Specs/Quick/wood_club_head_asset.md`.

## Routing

STATUS = `CESAR_REJECTED`. Implementer reads SPEC § "Iteration 5
amendments" + this rejection note + iter-4 SELF_REVIEW.md (for the
architectural analysis the self-reviewer did) and iterates.
