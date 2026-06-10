DONE

# STATUS — `1v1_match_flow` (Order 344) — DONE

- **State:** DONE — Cesar approved 2026-06-10 (accepted evidence as-is). Phase 2a complete.
- **What shipped:** turn-flow state machine, win/tie/draw + courtesy resolution (§10), persistent winner banner, basic runtime bot (distance-aware, no error injection), RP grant via `GameSession.OnMatchComplete` → `VersusResultHandler`, CSV-driven safety cap.
- **Review items all resolved:** Defect 1 (aiming HUD over banner → `HideShotUI`), Defect 2 (card TURN labels → Strokes mirrored to TurnCount), BUG A (real-tee seed), BUG B (distance-aware first stroke; Iron7 at ~107m, Driver >180m unchanged), BUG C (terrain fall-through — fixed separately, commit `1648db3b`).
- **§15 visual gate:** satisfied by the two-clip capture (a full real tee-to-cup match is ~55–60s, beyond the safe GPU recording window): Clip A `videos/versus_full_match_flow_iter9_clipA.mp4` (real-tee flow + BUG B) + Clip B `videos/versus_resolution_clip_clean_banner.mp4` (sink → courtesy → clean DRAW banner). Canonical still `screenshots/clipB_t28s.png`.
- **Close-out housekeeping:** iter-10c diagnostic logging in `ConeAlphaController.cs` reverted before commit.
- **Phase 2b (difficulty model):** deferred — see SPEC §13.
