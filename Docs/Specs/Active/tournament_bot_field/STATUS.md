QUEUED

# STATUS — tournament_bot_field (T3)

> **2026-06-25 — Spec authored** against GDD §7 (organic-reveal bot math) + T1 contracts (`BotFieldConfig`/`BotCard`/`TournamentLeaderboardEntry`). Grounded: all reuse handles verified on disk (identities via `LocalFakeLeaderboardProvider`, par via `HoleDatabaseLoader`→`HoleData.par`, cap via `versusStrokeCapOverPar`, bracket tiers via `bot_difficulty.csv`). **No existing stroke-roller** — generator is net-new.

**Task:** Deterministic bot-field generator + organic-reveal projector. Pure `Golfin.Tournaments` C# (System-only, headless-testable): `RollField(def, cfg, holePars) → BotCard[]` (pre-roll per-hole strokes/total/pace from `def.Id` seed) + `Project(card, now) → (thru, revealedStrokes, complete)` (the pure `(seed, now)` fn T4's `GetLeaderboard` calls).

**Tier:** FULL PIPELINE — gated by an **invariant test suite** (Rule 3), not visuals. No clone table → Rule 8 N/A.

## Dependency
- **T1 ✓** (contracts). **Logically independent of T2** — consumes the `BotFieldConfig` *type*, not its loader; build/test against POCO fixtures. **Can fire before or in parallel with T2.**
- Feeds **T4** (`LocalTournamentBackend.GetLeaderboard`).

## Staging (SPEC §8)
- [ ] **Stage 1** — stable hash (FNV/xxHash) + explicit PRNG + per-bot seed streams + hash-vector/determinism tests.
- [ ] **Stage 2** — `bot_score_brackets.csv` + bracket→strokes roll + identity selection + `RollField` + tests.
- [ ] **Stage 3** — pace schedule + `Project` + pace/projection/trickle tests.

## ⚠ Decisions for Cesar before/at dispatch (SPEC §9)
- **D0** PRNG: explicit PCG/xorshift (rec) vs `System.Random`+stable seed.
- **D1** bracket strokes distribution numbers (§3 table) — tune.
- **D2** identity selection: weighted-by-BracketWeights (rec) vs roster-level-only.
- **D3** worst-hole cap: reuse `versusStrokeCapOverPar` (rec, +4).
- **D4** in-progress provisional ranking → likely T4; confirm.

## Kickoff (independent of T2; fire when Code frees from filters)
```
Use the golfin-implementer subagent on "tournament_bot_field"
```
