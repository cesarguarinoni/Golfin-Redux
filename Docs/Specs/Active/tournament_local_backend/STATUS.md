READY — all deps met (T1 ✓ T2 ✓ T3 ✓); fire now

# STATUS — tournament_local_backend (T4)

> **2026-06-26 — Spec authored** against the real DTOs + T3's shipped API (`BotFieldGenerator.RollField`/`Project`, `BotProjection`) + the Implementation-Plan T4 scope. All reuse handles verified on disk. Boundary locked: **T4 = logic only; T5 owns the save schema; T9 owns the screen binding; T6 owns the round loop.**

**Task:** `LocalTournamentBackend` implements all 8 `ITournamentBackend` methods (replaces `StubTournamentBackend`). Headless `Golfin.Tournaments` C# via constructor-injected seams (clock, entry store, RP, items, par) — same purity/test discipline as T3. State derivation · Register (RP debit + char lock + idempotent) · SubmitHoleResult (append+persist+Finished) · GetLeaderboard (merge T3 bots + player, §6 ranking + tie ladder + DNF, provisional/final) · GetResults+ClaimPrize (resolve gate, split-pool prizes, claim-once).

**Tier:** FULL PIPELINE — gated by an EditMode invariant/unit suite (Rule 3). No clone table → Rule 8 N/A.

## Dependency / ordering
- **T1 ✓** · **T2 ✓** (`TournamentCsvLoader` + 3 CSVs + 81/81 tests, `5671b9840`/`69075753d`) · **T3 ✓**. **All deps met — T4 is unblocked and ready to fire.** Not blocked on T5 — persistence is an injected seam (in-memory v1; T5 swaps the save-backed impl).
- D1 resolved: T2 shipped `ResolveDelayMinutes` on `TournamentDefinition` (CSVs = 30).

## Staging (SPEC §7)
- [ ] **Stage 1** — skeleton + 4 seams + in-memory fakes + state derivation + GetTournaments/GetTournament + tests.
- [ ] **Stage 2** — Register / GetMyEntry / SubmitHoleResult + tests.
- [ ] **Stage 3** — GetLeaderboard: T3 merge + §6 ranking/ties/DNF + provisional/final *(meaty)* + tests.
- [ ] **Stage 4** — GetResults + ClaimPrize: resolve gate + split-pool + claim-once + tests.

## ⚠ Decisions for Cesar (SPEC §8)
- **D1 ✅ resolved** — T2 shipped `ResolveDelayMinutes`; T4 reads it. No action.
- **D2** "Ending" badge threshold (rec: last 1h of window).
- **D3** provisional ranking: score-to-par-so-far (rec) vs raw revealed strokes.
- Out of scope: cancel→refund (no Cancel method in the interface).

## Kickoff (deps met — fire now)
```
Use the golfin-implementer subagent on "tournament_local_backend"
```
