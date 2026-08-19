ARCHITECT_REVIEW_PASS

# STATUS — tournament_restrictions

Architect verdict: **APPROVED** (2026-08-19) — see `ARCHITECT_REVIEW.md`. Spot-checked against the
working tree, not just the report. Nothing to change in the implementation.

- Server half LIVE in prod 2026-08-18 (migration + list_golfin + enter enforcement). Unchanged.
- Client half implemented per SPEC.md §1–§3. See `IMPLEMENTER_REPORT.md` / `ARCHITECT_HANDOFF.md`.
- Rarity renders as its coloured single letter (C/U/R/M/L/S via `RarityHelper`), per Cesar 2026-08-19.
- Category tag **dropped, not deferred**: the SPONSOR line carries it (GOLFIN = hardcore).
- Full EditMode suite green: **1478 total / 1475 passed / 0 failed / 3 skipped** (the 3 skips are
  pre-existing `HoleCompleteDriverTests` Stage-C1 skips, untouched by this task).
- Open questions Q1–Q4 all ruled on in `ARCHITECT_REVIEW.md`; none required a code change.

## Remaining before DONE

1. **A2 dashboard restrictions editor** (Architect, in progress) — ships with `gear_rule=supplied`
   disabled for authoring per the Q3 ruling.
2. **One authored restricted tournament** (rarity band + level band + max_players; NOT
   `gear=supplied`) → **live round trip**: RULES render from `list_golfin`, ineligible CONFIRM toast,
   server `ineligible` on a forced mismatch, `full` at cap.
3. **JA native review** of the 16 new localization rows.
4. **RULES visual fit on device** — pixel check of the worst line in the real body box.

`DONE` and the move to `Docs/Specs/Completed/` are Cesar's call, after the live round trip.
