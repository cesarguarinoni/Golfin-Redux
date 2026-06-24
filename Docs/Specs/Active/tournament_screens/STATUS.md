# STATUS — tournament_screens

**Task:** Code-proof implementation spec for the two new tournament screens (Tournament Hole Selection + Tournament Leaderboard), delivered to Code in controlled, prefab-first stages.

**Tier:** FULL PIPELINE (new UI architecture + spatial layout + multi-stage).

**Updated:** 2026-06-24 JST

## Progress
- [x] Designs finalized in Figma (both screens).
- [x] Cross-referenced locked Tournaments GDD; 7 conflicts surfaced + ruled by Cesar.
- [x] GDD reconciled — §17 Addendum appended (rulings + reuse map + delivery model).
- [x] Clone sources grounded in Unity (HoleSelection, Rankings, Modals/Result, ScreenManager).
- [x] SPEC.md foundation written: rules, clone map, tokens, staged plan, Stage 0 prefab inventory.
- [x] Stage 0 GEOMETRY extracted for all 9 prefabs (get_metadata) + structure + content mapping, written into §4 with node links.
- [ ] Stage 0 TOKENS (font px + exact fills) — blocked: Figma get_design_context endpoint hung (needs MCP restart). Links left in §4 for the pull.
- [ ] Stage 0 handoff to Code (prefabs only).
- [ ] Stage 1 scaffold + nav.
- [ ] Stage 2 bind to LocalTournamentBackend.
- [ ] Stage 3 state polish.
- [ ] Insert new UI order in Tournaments_Implementation_Plan.md for Hole Selection.
- [ ] Confirm empty-state copy (+ JP localization).

## Key node IDs
- Hole Selection root `13414:2936`; Leaderboard root `13414:5598`.
- See SPEC §4 for per-prefab nodes.
