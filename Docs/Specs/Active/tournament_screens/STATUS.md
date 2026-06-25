# STATUS — tournament_screens

**Task:** Code-proof implementation spec for the two new tournament screens (Tournament Hole Selection + Tournament Leaderboard), delivered to Code in controlled, prefab-first stages.

**Tier:** FULL PIPELINE (new UI architecture + spatial layout + multi-stage).

**Updated:** 2026-06-24 JST

## Progress
- [x] Designs finalized in Figma (both screens).
- [x] Cross-referenced locked Tournaments GDD; 7 conflicts surfaced + ruled by Cesar.
- [x] GDD reconciled — §17 Addendum appended (rulings + reuse map + delivery model).
- [x] Clone sources grounded in Unity (HoleSelection, Rankings, Modals/Result, ScreenManager).
- [x] Stage 0 GEOMETRY extracted for all 9 prefabs (get_metadata) + structure + content mapping.
- [x] Stage 0 TOKENS extracted (get_design_context, online) — text styles, STROKES pill, LIVE badge, badge colors.
- [x] Existing Unity prefabs pinned for reuse (HoleCard, HoleCompleteWidget, RankingsScreen, RankingsCards, RankingsCardUser).
- [x] SPEC rewritten: reuse-and-modify mandate, exact prefab paths, full tokens. **Stage 0 ready for Code handoff.**
- [x] Stage 0 handoff to Code (prefabs only) — **7 prefabs BUILT + Cesar-APPROVED 2026-06-24** (r1 fixes + LIVE pill applied). See `STAGE0_REPORT.md` + `screenshots/stage0_screen{A,B}_*.png`.
- [x] **Stage 1 scaffold + nav — Cesar-APPROVED 2026-06-25 ("Perfect. Done on both").** Both full screens via the real persistent nav bars (SELECT HOLE / TOURNAMENT LEADERBOARD title in the top bar + bottom nav), 2-row identity pills (sponsor full-width; tournament · ENDS IN), Stage-0 prefabs in place, podium-icon → Leaderboard, Close inside panels, `Selection → Hole Selection ⇄ Leaderboard` wired via ScreenManager (nav verified in play mode). Leaderboard: podium #1>#2/#3 scale + centered STROKES, 24px gaps (numerically verified), populated with the normal-rankings fake-bot roster (varied characters/rarities) via LeaderboardManager + Top3/RankingsCardWidget, STROKES override. 5 review rounds of fidelity corrections applied. See `STAGE1_REPORT.md` + `screenshots/stage1_{hole_selection,leaderboard}.png`. Pushed to origin/main.
- [x] Insert new UI order in Tournaments_Implementation_Plan.md for Hole Selection (**T8b** before T9).
- [ ] Stage 2 bind to LocalTournamentBackend.
- [ ] Stage 3 state polish.
- [ ] Confirm empty-state copy (+ JP localization).
- [ ] Remove temp `TOURNAMENTS (TEMP)` entry on ModeSelection once T7 (tournament_selection_screen) lands.

## Key node IDs
- Hole Selection root `13414:2936`; Leaderboard root `13414:5598`.
- See SPEC §4 for per-prefab nodes.
