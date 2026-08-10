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

Pass 3 (Cesar follow-up, AFTER close-out — commit 5d938c9a8, so it post-dates this folder's move
to Completed): "You did not center the camera during putting." The SPEC had scoped putting OUT
(§3/§5 kept the legacy pose verbatim) and the ball sat ~62% down screen during a putt.
ApplyCameraYaw's putter branch now runs the same solver at its OWN distance/height —
_puttCamDistanceM / _puttCamHeightM default to the legacy 8 m / 3 m on purpose, because the putt
view has to fit the 15 m aim line and the green-reading grid. Measured live on the Hole 1 green
(IsPutt=True, ball 2.63 m from the cup): stand-off 8.544 m unchanged, ball viewport (0.5000, 0.5000)
— dx/dy 0.0000 — cup at (0.5000, 0.5616) directly above. Task stays DONE; see § Pass 3 in
IMPLEMENTER_REPORT.md for the test rewrite and what is still unverified.

Video deliverable (Cesar, "Video or it didn't happen"):
  videos/close_camera_and_putt_centering.mp4 — 27 s, 1170x2532, captioned, flip-verified across all
  1058 consecutive frame pairs. Tee aim 6.6 m -> next lie 3.3 m -> chase 2.3 m -> putt aim centred.
  (videos/ is gitignored by design — local artifact; copy also in Docs/Reports/Media/.)

The two Architect calls from IMPLEMENTER_REPORT.md § Deviations are ACCEPTED AS-IS with the close-out:
  D2 — ball centres at the LIVE CentralBallWidget viewport Y (0.5000), not the mockup's 0.4234.
       The ball-under-2D-ball contract holds exactly; moving the 2D widget stays out of scope.
  D3 — tee clamp settles at d = 6.42 m on Hole 1 (markers ±1.474 m lateral, hFOV 29.88°).
       Reported per spec §4; no unilateral safeFrac/FOV relaxation. Revisit only if stroke-1
       framing bothers on device.
