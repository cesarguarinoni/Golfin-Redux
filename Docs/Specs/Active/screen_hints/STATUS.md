READY_FOR_ARCHITECT_REVIEW

Task: screen_hints
Updated: 2026-09-10 by Claude Code (direct implementation in the main thread, as the kickoff asked)
Iteration: iter-1. Everything on the acceptance list is green except ONE item marked FAIL on purpose:
the stacking order vs TournamentResultPresenter (it waits 1.0 s, the hint waits one frame, so the hint
opens first) — an Architect decision, not a defect in the mechanism, which is proven with a stacked
SchemeConfirmModal. Hence the ARCHITECT_REVIEW path rather than SELF_REVIEW.
Gates: Unity EditMode 3042 total / 3039 pass / 0 fail / 3 skip (pre-existing skips); UI fidelity lint fail 0.
Evidence: screenshots/verify_en.log + verify_ja.log (the bot's per-step log) and 30 real-play frames.
Texts published: texts v54; export_content.py --check clean; bundled table rebuilt (1205 rows).
Notion: GOLFIN_Roadmap 2238 (deferrals 2239–2241)
Figma: page `Tutorial` — overlay kit `14263:39325`, Roster `14263:109304`, Home `14263:109672`, In-game `14263:109883`, Roster 4/4 `14266:109661`

## Log

- 2026-09-10 — SPEC_READY. Cesar decisions: screen table as proposed; shot view in scope (all six gameplay tips); per-device PlayerPrefs; image + text + counter + CONTINUE; new Figma page `Tutorial`. Same session: `TIP_RP` copy rewritten (drops "ONLY" / "NEVER FOR SALE") — applies to the loading screen too; BACK button from hint 2 onwards (absent on hint 1 / single hints, never disabled); gold button reads CLOSE on the last / single hint; backdrop = dark scrim + opaque plate (translucent plate and blurred-screen options mocked and rejected).
- 2026-09-10 — READY_FOR_ARCHITECT_REVIEW (iter-1). Built, verified by the `GOLFIN ▸ Hints ▸ Run verify bot` real-flow driver in EN and JA, texts v54 published. One FAIL row (stacking order) needs the Architect's call — see IMPLEMENTER_REPORT.md § Known FAIL items.
