DONE

Task: aim_camera_ball_centering
Updated: 2026-08-10 (Cesar approved — "all tasks from yesterday and today are done"; moved to Completed)

Implementation complete, EditMode suite green (1050 tests / 1047 pass / 0 fail / 3 pre-existing skips),
live play-mode verification through the real player entry path on Hole 1.

Pass 2 (Cesar follow-up, same day): BotDriver's duplicated legacy framing removed — it now delegates
to the single production implementation via the new ApplyAimCameraAt seam (ApplyCameraYaw made
internal; MapViewController's reflection still binds, verified). ChaseCamera in-flight follow
tightened 3.0/1.8 -> 2.0/1.2 (uniform x2/3, framing angle unchanged at 30.96 deg): camera->ball
3.499 m -> 2.332 m = 1.50x larger ball. Aim distances untouched per "camera distance is ok".

The two Architect calls from IMPLEMENTER_REPORT.md § Deviations are ACCEPTED AS-IS with the close-out:
  D2 — ball centres at the LIVE CentralBallWidget viewport Y (0.5000), not the mockup's 0.4234.
       The ball-under-2D-ball contract holds exactly; moving the 2D widget stays out of scope.
  D3 — tee clamp settles at d = 6.42 m on Hole 1 (markers ±1.474 m lateral, hFOV 29.88°).
       Reported per spec §4; no unilateral safeFrac/FOV relaxation. Revisit only if stroke-1
       framing bothers on device.
