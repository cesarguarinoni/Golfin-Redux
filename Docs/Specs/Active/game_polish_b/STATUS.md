IMPLEMENTER_WORKING

# STATUS — `game_polish_b`

**Current:** `IMPLEMENTER_WORKING` (2026-09-08). Notion 2111, slice b of three.

**Every design section §D0–§D7 is implemented, compile-verified and gated by tests or the
probe.** Evidence is substantially in: the §D2 parity gate (6 traces, fail 0), the §D1 modal
gate (14 modals, fail 0), five captioned A4 clips, and the §D3 count-DOWN proven frame by frame.

**A3, A6, A7 and A8 are now measured too** — and A3 found a real defect: three early returns in
TournamentLeaderboard that ended a paint without clearing the shimmer. That was the second
defect of one shape, so every `Shimmer` call site was audited and two more were fixed.

Still outstanding, per item in `IMPLEMENTER_REPORT.md` § What is NOT done: the A5 per-site
table, cold frames for five of the six shimmer sites (they need a backend provider with an empty
first response — not obtainable in this session), A11 (arguably N/A — no Figma node), and two of
A4's seven subjects. NOTE: A4 (b)'s recorded reason was WRONG and is corrected in the report —
the shop catalog is a TAB that was never opened, not a balance problem.

This task has NOT been submitted for review and must not be treated as ready for one.

| Date | State | Note |
|---|---|---|
| 2026-09-05 | `SPEC_READY` | Written while the audit runs; 13 modals (SchemeConfirm added since the map). |
| 2026-09-08 | `IMPLEMENTER_WORKING` | D0/D1.1/D1.3/D2/D3/D5. Modal count measured at **15**, not 13. D2 parity gate closed. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | D4 + the rest of D6. 6 shimmer hosts, 0 active at rest. Two sites moved on evidence. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | D1.4 + D7. Result choreography, skippable. Probe: modals **14 / fail 0**. Found and fixed a pre-existing bug — a duplicate presenter was destroying the tournament result modal at boot. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | **Evidence.** 5 captioned clips at 1170x2532; RP count-DOWN proven at 6.148→6.143→6.140→6.139. Three clips were discarded and re-taken because the caption did not match the frame. |
| 2026-09-08 | `IMPLEMENTER_WORKING` | **A3/A6/A7/A8.** Rest parity measured over 11 screens with every difference opened and attributed. A3 found a real defect (an arm that ends a wait must clear the shimmer); the shape was audited across all 5 sites and 2 more fixed. A6's cold cycle captured end to end for `missions.daily`. EditMode **2863 / 0 failed**. |
