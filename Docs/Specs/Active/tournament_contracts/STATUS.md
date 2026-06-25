# STATUS — tournament_contracts (T1)

**Task:** Lock the `Golfin.Tournaments` leaf asmdef + DTOs + `ITournamentBackend`/`ITournamentClock` interfaces so the whole tournament chain (T2 loaders → T3 bot field → T4 backend → T5 save → UI binding) compiles against a frozen shape. **Contracts only — zero logic.**

**Tier:** FULL PIPELINE (new asmdef = architectural risk; leaf-boundary + reuse discipline must be reviewed).

**Updated:** 2026-06-25 JST

## Progress
- [x] GDD §6/§7/§8/§9 + §17 read; `ITournamentBackend` signatures lifted verbatim from §8.
- [x] Existing reuse targets verified on disk (2026-06-25): `fake_players.csv`, `bot_difficulty.csv`, `bot_clubs.csv`, `ILeaderboardProvider`/`LeaderboardEntry`, `LeaderboardManager` (singleton `_provider = new LocalFakeLeaderboardProvider()`), `NetworkTimeProvider`/`ITimeProvider`.
- [x] Confirmed the Tournament Leaderboard **already renders the real roster** via `LeaderboardManager.GetRanking(Daily)` (STROKES-relabeled) — so T4 swaps the fill source, it does **not** add bots (SPEC §4.1).
- [x] SPEC authored — `SPEC.md` (§0 rules + reuse mandate, §1 asmdef + leaf-boundary flag, §2 DTOs, §3 interfaces, §4 reuse map + §4.1 leaderboard-already-bound, §5 EditMode compile-gate stub, §6 out-of-scope, §7 flags). **Ready for Code handoff.**
- [ ] Stage 1 (impl): define asmdef + DTOs + interfaces + `StubTournamentBackend` compile-gate. Implementer.
- [ ] Self-review: leaf boundary holds (no UI dep except the flagged time seam); no logic leaked in; DTOs match GDD §8 verbatim; `rngSeed`/`inputLog` present.
- [ ] Architect review.

## Decisions to resolve during impl (SPEC §7)
- `holeSet` = explicit hole-id list (rec).
- Time seam: reference Rankings asmdef vs extract shared util (rec: extract if cheap).
- D-Tie indivisible-item rule → duplicate-to-each (rec), recorded as doc-comment for T4.
- `inputLog` → minimal `ShotCommand` struct now (rec).

## Unblocks
T2 `tournament_csv_loaders` → T3 `tournament_bot_field` → T4 `local_tournament_backend` → then `tournament_screens` **Stage 2** (swap leaderboard fill to `GetLeaderboard(id)`; bind hole selection to real entry/progress).
