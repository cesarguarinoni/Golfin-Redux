# Director — promote OBFreeze → Chase on OB→Aiming

**Status:** Queued (architect-flagged 2026-05-13 during `loop_v1_2e_next_shot_handoff` review)
**Priority:** P2 (polish — affects camera framing after OB drops, not gameplay correctness)
**Estimate:** ~half day (small Director change + 2-3 EditMode tests + smoke verification)

## Problem

`LoopCameraDirector.ModeMap[BallState.Aiming] = null` ("leave whatever was set"). This means:
- After an OB drop, the §2e flow does `OBDropResolver → RepositionBallWithLookDir → _ballSM.ReArm()` which fires `OnStateChanged(OB → Aiming)`.
- Director receives `(Previous=OB, Next=Aiming)` and applies no mode change because `Aiming → null`.
- Camera stays in `OBFreeze` mode visually after the drop, even though the ball has teleported to the safe drop point and is ready for the next shot.
- The smoke runner for §2e works around this by forcing `chaseCamera.SetMode(Chase)` after the drop — but that's smoke-runner only; live gameplay still shows OBFreeze.

Surface effect in live play: after an OB shot, the player sees the camera frozen at the OB termination point (water/OOB), framing 5m above the OB hit, even though the ball is back at the drop point ready to aim. The chip "TURN 3" updates correctly, but the camera framing is wrong.

## Source references

- `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:109` — `ModeMap[BallState.Aiming] = null`
- `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs:208-214` — comment notes that null-target early-return in `ChaseCamera.LateUpdate` hands control to `ApplyCameraYaw`, but the HUD mode label stays stale
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` `HandleShotComplete` OB branch (§2e) — calls `ApplyCameraYaw(cam)` after `RepositionBallWithLookDir + ReArm`, so the aim-camera framing IS correct in pixels; only the mode label is wrong
- §2e SPEC L7 punted Director changes out-of-scope — this is the follow-up ticket

## Proposed fix (one-of three options, pick during spec)

**Option A — `Aiming → Chase` blanket.** Simplest: `ModeMap[Aiming] = Chase`. Side effect: any other state→Aiming transition (e.g. AtRest→Aiming after manual reset?) would also set Chase. Need to audit which transitions feed into Aiming in production.

**Option B — Previous-aware mapping.** `if (change.Previous == OB && change.Next == Aiming) ApplyMode(Chase)`. Targeted, no side effects, but adds asymmetry to the ModeMap pattern.

**Option C — Move the promotion to PhysicsLabController.HandleShotComplete OB branch.** After `_ballSM.ReArm()`, also call `chaseCamera.SetMode(Chase)` directly (bypassing the Director on this specific path). Couples the controller to the camera mode but keeps the Director's ModeMap clean.

Architect's lean: **Option B** — preserves Director-as-single-owner-of-mode pattern, surgical, easy to test, and the asymmetry is small + well-documented.

## Definition of done

- `LoopCameraDirector.HandleStateChanged` promotes OBFreeze → Chase on `(Previous=OB, Next=Aiming)`.
- 2-3 new EditMode tests in `LoopCameraDirectorTests.cs`:
  - `OBFreezePromotesToChase_OnOBToAiming` — verifies mode change.
  - `OBFreezeDoesNotPromote_OnOBToOther` — verifies non-Aiming-next doesn't trigger.
  - `AimingFromInCup_DoesNotPromote` — sanity for the asymmetric branch.
- Test gate stays green (currently 273/0/0 after §2e).
- Smoke run on Hole_06 OB scenario: live HUD label flips from `OBFreeze` → `Chase` after the drop, visible in screenshot.

## Cross-references

- `Docs/Specs/Completed/loop_v1_2e_next_shot_handoff/` — parent task that surfaced this gap
- `Docs/Specs/Completed/loop_v1_2e_next_shot_handoff/ARCHITECT_REVIEW.md` — reviewer's follow-up #1 flag
- `Docs/Specs/Completed/loop_v1_2e_next_shot_handoff/SELF_REVIEW.md` iter-1 § Step 4 — first surfacing of the OBFreeze stale-label symptom
