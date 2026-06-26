QUEUED — implementation blocked on T2

# STATUS — tournament_local_backend (T4)

> **2026-06-26 — Spec authored** against the real DTOs + T3's shipped API (`BotFieldGenerator.RollField`/`Project`, `BotProjection`) + the Implementation-Plan T4 scope. All reuse handles verified on disk. Boundary locked: **T4 = logic only; T5 owns the save schema; T9 owns the screen binding; T6 owns the round loop.**

**Task:** `LocalTournamentBackend` implements all 8 `ITournamentBackend` methods (replaces `StubTournamentBackend`). Headless `Golfin.Tournaments` C# via constructor-injected seams (clock, entry store, RP, items, par) — same purity/test discipline as T3. State derivation · Register (RP debit + char lock + idempotent) · SubmitHoleResult (append+persist+Finished) · GetLeaderboard (merge T3 bots + player, §6 ranking + tie ladder + DNF, provisional/final) · GetResults+ClaimPrize (resolve gate, split-pool prizes, claim-once).

**Tier:** FULL PIPELINE — gated by an EditMode invariant/unit suite (Rule 3). No clone table → Rule 8 N/A.

## Dependency / ordering
- **T1 ✓** · **T3 ✓** · **T2 (Order 503) — Queued, NOT implemented.** T4 *spec* is ready now, but **implementation is blocked on T2** (needs loaded `TournamentDefinition`/`PrizeTable`/`BotFieldConfig`). Not blocked on T5 — persistence is an injected seam (in-memory v1; T5 swaps the save-backed impl).
- Implementation order: **fire T2 → then T4.**

## Staging (SPEC §7)
- [ ] **Stage 1** — skeleton + 4 seams + in-memory fakes + state derivation + GetTournaments/GetTournament + tests.
- [ ] **Stage 2** — Register / GetMyEntry / SubmitHoleResult + tests.
- [ ] **Stage 3** — GetLeaderboard: T3 merge + §6 ranking/ties/DNF + provisional/final *(meaty)* + tests.
- [ ] **Stage 4** — GetResults + ClaimPrize: resolve gate + split-pool + claim-once + tests.

## ⚠ Decisions for Cesar (SPEC §8)
- **D1** resolveDelay: `TournamentDefinition` lacks `ResolveDelayMinutes` → T2 adds it (rec) vs T4 const. *Confirm with T2.*
- **D2** "Ending" badge threshold (rec: last 1h of window).
- **D3** provisional ranking: score-to-par-so-far (rec) vs raw revealed strokes.
- Out of scope: cancel→refund (no Cancel method in the interface).

## Kickoff (after T2 lands)
```
Use the golfin-implementer subagent on "tournament_local_backend"
```
