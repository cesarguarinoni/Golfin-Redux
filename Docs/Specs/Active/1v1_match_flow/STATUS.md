# STATUS — `1v1_match_flow` (Order 344)

- **State:** SPEC_READY (Phase 2a). Awaiting Cesar's glance, then kickoff to Code.
- **Tier:** FULL PIPELINE
- **Scope this pass (2a):** turn-flow state machine, win/tie/draw resolution, persistent winner banner, basic runtime bot (no error injection), RP grant on win via event bridge. Phase 2b (difficulty model) deferred — see SPEC §13.
- **Follows:** `1v1_ingame_ui` Phase 1 (Order 343, shipped `756ab280`).
- **Spec written:** 2026-06-09 15:58 JST (Architect).
- **Open for Cesar:** sub-calls A–E in SPEC §2 (per-player model in MatchContext; RP/result via GameSession event bridge; 2a ends on banner with 1v1 result modal deferred to 2c; safety cap par+5; 2b written after 2a). Veto any → amend before kickoff.
- **Kickoff:** `Use the implementer subagent on "1v1_match_flow"`
