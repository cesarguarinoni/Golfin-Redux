# STATUS — leaderboard_wiring

- **State:** SPEC_READY → awaiting kickoff
- **Tier:** TELLCODE Tier 2
- **Written:** 2026-06-15 14:09 JST
- **Kickoff:** `Use the implementer subagent on "leaderboard_wiring"`

## Phase
- **Phase 1 (this spec):** data layer (shared 100+ fake roster CSV, `ILeaderboardProvider`, 4 UTC-period RP accumulators, network UTC time) + wire the existing `RankingsScreen` prefab + header entry icons (Home + Hole Select).
- **Phase 2 (separate spec, after P1 ships):** repoint `MatchmakingModalController` 1v1 opponents to the shared fake roster.

## Decisions locked (Cesar 2026-06-15)
1. One board, title "LEADERBOARD", metric = RP earned in period.
2. All 4 tabs (Daily/Weekly/Monthly + Historic=lifetime).
3. League band static ("DIAMOND LEAGUE").
4. UTC boundaries via network time (device clock untrusted); offline → device-UTC fallback.
5. 100+ fake players, shared roster (also feeds Phase 2 matchmaking).
6. Reuse Home banner; layout must survive banner-absent.
7. Reuse existing prefab design; wire data, fix only if necessary.

## Key handoff notes for Code
- Confirm prefab transform paths via Unity MCP before binding.
- Locate + reuse the existing rarity color helper (do not hardcode).
- Network-time host choice must be flagged in the report.
- `SaveData.schemaVersion` 1→2; verify `SaveDataHost` migration.
