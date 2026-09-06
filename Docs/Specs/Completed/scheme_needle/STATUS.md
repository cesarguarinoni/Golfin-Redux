DONE

scheme_needle — Tap Timing. Approved by Cesar 2026-09-05.

Implementation landed in d54468b6c; this folder is the evidence.

- needle_invariants.json: 133/133 PASS, 0 FAIL @ 1170x2532, driven through the real entry path
  (in-game gear -> InGameSettingsModalController.schemeButtons[2].onClick -> real NeedleHandle
  pointer events -> real NeedleTapCatcher pointer-down).
- EditMode: 2588 tests, 2585 pass / 0 fail / 3 pre-existing skips (2530 baseline + 58 new).
- UI fidelity lint: Docs/Diagnostics/_capture/SchemeRoot_Needle_lint.json, fail 0.
- ShotController.cs: ZERO diff. Flick and Pendulum unchanged in behaviour.

screenshots/ and videos/ are gitignored, so they stay local. The captioned clip is also at
Docs/Reports/Media/2026-09-05_scheme_needle.mp4.
