READY_FOR_SELF_REVIEW

Task: aim_camera_ball_centering
Updated: 2026-08-10 (implementation complete, main-thread implementer)

Implementation done, EditMode suite green (1050 tests / 1047 pass / 0 fail / 3 pre-existing skips),
live play-mode verification through the real player entry path on Hole 1.

Pass 2 (Cesar follow-up, same day): BotDriver's duplicated legacy framing removed — it now delegates
to the single production implementation via the new ApplyAimCameraAt seam (ApplyCameraYaw made
internal; MapViewController's reflection still binds, verified). ChaseCamera in-flight follow
tightened 3.0/1.8 -> 2.0/1.2 (uniform x2/3, framing angle unchanged at 30.96 deg): camera->ball
3.499 m -> 2.332 m = 1.50x larger ball. Aim distances untouched per "camera distance is ok".

Two Architect calls are pending in IMPLEMENTER_REPORT.md § Deviations — neither is a defect:
  D2 — the LIVE CentralBallWidget sits at viewport Y 0.5000, not the mockup's 0.4234, so the ball
       centres at 50% down rather than ~57.7%. The spec mandates deriving from the live widget, and
       the ball-under-2D-ball contract holds exactly. Moving the 2D widget is out of scope.
  D3 — the tee clamp settles at d = 6.42 m (not the 3 m close-up, but not the ~8 m no-improvement
       trigger either). Measured marker offsets ±1.474 m lateral, horizontal FOV 29.88°. Reported
       per spec §4 rather than fixed unilaterally.
