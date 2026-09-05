READY_FOR_ARCHITECT_REVIEW

scheme_needle — Tap Timing. Built, gated and captured; awaiting review.

- needle_invariants.json: 133/133 PASS, 0 FAIL @ 1170x2532, driven through the real entry path
  (in-game gear -> InGameSettingsModalController.schemeButtons[2].onClick -> real NeedleHandle
  pointer events -> real NeedleTapCatcher pointer-down).
- EditMode: 2588 tests, 2585 pass / 0 fail / 3 pre-existing skips (2530 baseline + 58 new).
- UI fidelity lint: Docs/Diagnostics/_capture/SchemeRoot_Needle_lint.json, fail 0.
- ShotController.cs: ZERO diff. Flick and Pendulum unchanged in behaviour.
- Canonical screenshot: screenshots/needle_result_perfect.png
- Canonical video: videos/scheme_needle_needle.mp4 (1170x2532, 36.9s, captioned)
