DONE

Cesar approved 2026-09-05. Free Swing is spec 3 of 4 in the control-schemes track.

110/110 acceptance invariants through the real entry path, 619/619 EditMode (Flick, Pendulum and
Tap Timing suites unchanged), UI fidelity lint fail 0, texts published at v41, one captioned
45.8s clip. ShotController.cs zero diff.

Carried forward, filed in Docs/CONTROL_SCHEMES_PLAN.md section 9:
  - on-device tuning of SPEC section 3.5 (ideal tempo, duff floor, path dead zone) -- seeded, not tuned
  - the ball occludes the impact target during the backswing (a design call, not a bug fix)
  - grade SFX; freeswing_path / freeswing_tempo telemetry keys; bot DriveBot (bot_scheme_parity Stage B)
