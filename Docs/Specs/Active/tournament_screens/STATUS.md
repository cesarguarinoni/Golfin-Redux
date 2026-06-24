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
- [x] Stage 0 handoff to Code (prefabs only) — **7 prefabs BUILT + Cesar-APPROVED 2026-06-24** (r1 fixes + LIVE pill applied). See `STAGE0_REPORT.md` + `screenshots/stage0_screen{A,B}_*.png`. **Next: Stage 1 scaffold + nav (awaiting Cesar go).**
- [ ] Stage 1 scaffold + nav.
- [ ] Stage 2 bind to LocalTournamentBackend.
- [ ] Stage 3 state polish.
- [ ] Insert new UI order in Tournaments_Implementation_Plan.md for Hole Selection.
- [ ] Confirm empty-state copy (+ JP localization).

## Key node IDs
- Hole Selection root `13414:2936`; Leaderboard root `13414:5598`.
- See SPEC §4 for per-prefab nodes.
