READY

# tournament_screens_live_bind — STATUS

- **State:** READY (specced, not fired)
- **Tier:** TELLCODE (bind live data to existing screens) — EditMode mapping test + bot-video gate
- **Slug / kickoff:** `Use the golfin-implementer subagent on "tournament_screens_live_bind"`
- **Depends:** tournament_backend_bootstrap ✓
- **Unblocks:** visible end-to-end proof of the live backend; clears the path to T6
- **Notion order:** 510 (Queued)

## Why
Backend is live but nothing consumes it. Bind the two existing screens (Selection + Leaderboard) to
`TournamentService.Instance.Backend` — real tournaments, real states, real standings. Adds the shared
`SelectedTournamentId` handoff + `GetTopPrizeRP` (both reused by T6).

## Done when
Selection shows the 6 CSV tournaments with correct badges/fees/rewards + real filter; tapping a card sets
`SelectedTournamentId` and opens the leaderboard on live `GetLeaderboard` data; `MapCardState` unit test
green; bot video captured. Registration is NOT wired here (deferred to T6).
