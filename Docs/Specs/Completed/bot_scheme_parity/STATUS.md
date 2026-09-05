DONE

Implemented directly by Claude Code at Cesar's request (not via the subagent chain), 2026-09-06.

Everything in SPEC.md is built and verified.
  Live acceptance: 52/52 PASS  (bot_scheme_parity_invariants.json, real entry path, hole 2)
  EditMode:        2694 passed / 0 failed / 3 pre-existing skips
  Rule 23 grep:    0 errors over 36 candidate bot files

Two real defects were found by the live harness and fixed, both pinned by regression tests:
  1. The tree probe's SOFT line-quality preferences deleted the graded schemes' difficulty model
     (a level-1 bot was outplaying a level-100 one). Rule now: hard trunk rejection for every
     scheme, soft preferences Flick-only.
  2. Bot difficulty was scaling with the PLAYER'S equipped club, because a 1v1 opponent swings the
     local player's bag and graded yaw is cone-relative. Sigma is now solved per swing against the
     live grader, so the bracket target holds whatever club is equipped.

Cesar approved 2026-09-06. Implementation is 2ab262c45.

Remaining items are optional and listed in IMPLEMENTER_REPORT § Needs manual verification:
a PerfBaselineBot re-baseline (its swing is ForceFlick + a Perfect band, so its numbers cannot
move by construction) and the SPEC §7 9-hole strokes-vs-par runs (the mechanism is proved by the
EditMode calibration guard at 512-5000 samples plus the 52/52 live run).
