# STATUS — 1v1_result_rewards_display (Order 347)

**State:** SPEC_READY — Stage 0 (prefab-only) awaiting kickoff
**Priority:** P2 (activated by Cesar 2026-07-01 13:xx JST from P3-Queued)
**Spec:** `Docs/Specs/Active/1v1_result_rewards_display/SPEC.md`

## Stage ledger
- [ ] **Stage 0** — `VersusResultScreen.prefab` built via editor builder; both win/lose visual
      states demonstrable; Cesar visual-checks vs Figma `13274:877` / `13275:2628`. ← KICKOFF TARGET
- [ ] Stage 1 — present in Shell after banner; bind players/ranks/hole from live match + roster
- [ ] Stage 2 — reward-row binding + NEW MATCH behavior (needs D1 + D3)
- [ ] Stage 3 — polish (win/lose brightness, draw variant D2, transitions)

## Verified facts (2026-07-01)
- Live gap confirmed in `VersusResultHandler.HandleMatchComplete`: reward granted silently, no screen.
- No versus result prefab exists (only `TournamentResultModal` + legacy `MissionResultCard`).
- Figma pulled: 2 states (win `13274:877` / lose `13275:2628`), full-screen RESULTS w/ winner-loser
  portraits + ranks + hole line + 3-item reward row + NEW MATCH.
- Reuse sources located: `MatchMakingModal`/`MatchmakingModalController` (versus pair + roster rank
  binding), `CharacterThumbnailCard`, `TournamentResultModalBuilder` (Stage-0 builder pattern).

## Open decisions (see SPEC §5) — needed before Stage 2, NOT blocking Stage 0
- D1 reward model (3 items ×10 vs flat 200 RP)
- D2 draw visual (no Figma)
- D3 NEW MATCH behavior
- D4 screen vs modal (proposed: screen, like RankingsScreen)
