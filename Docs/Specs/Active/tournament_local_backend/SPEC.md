# Tournament Local Backend (T4) — Spec

> **Order:** EPIC 500 Phase B · class `LocalTournamentBackend` implements `ITournamentBackend` (the 8 methods), replacing `StubTournamentBackend`. **Critical path** (`T1 → T4 → T6 → UI`).
> **Depends:** T1 ✓ (DTOs) · **T2** (`tournament_csv_loaders` — definitions/prizes/bot-fields; *still Queued, Order 503*) · T3 ✓ (`BotFieldGenerator`). **NOT T5** — persistence is an injected seam; T5 (`tournament_save_entry`) provides the `Golfin.Save`-backed impl later.
> **Design source:** `Docs/Game Design/Tournaments_GDD.md` §3 (lifecycle), §5 (scoring), §6 (ties + split-pool), §7 (organic reveal), §10 (prizes); `Tournaments_Implementation_Plan.md` T4.
> **Tier:** FULL PIPELINE — headless deterministic logic, gated by an **EditMode invariant/unit suite** (`PIPELINE_HARDENING` Rule 3), *not* visuals. No clone table → Rule 8 N/A.

---

## 0. What T4 is — and the scope boundary
`LocalTournamentBackend` is the v1 implementation of every `ITournamentBackend` method: state derivation, registration + RP debit, hole submission + persistence, the merged provisional/final leaderboard (projected T3 bots + the local player, §6 ranking), and result/prize resolution. Pure C# in `Golfin.Tournaments` (**no UnityEngine dependency in the logic** — same headless-testable discipline as T3), wired to the engine only through **injected seams**.

**Boundary (do not cross):**
- **T5 owns the save schema.** T4 must **not** add `PersistedTournamentEntry` or touch `SaveData`/`SaveSchemaMigrator`. T4 persists through an injected `ITournamentEntryStore`; ship an **in-memory** impl for v1/tests; T5 swaps in the `Golfin.Save`-backed impl. (Plan: T4 ⟂ T5, both from T1, converge at T6.)
- **T9 owns the leaderboard screen binding.** T4 only *produces* `TournamentLeaderboardEntry[]`; the swap of the screen's fill source from `LeaderboardManager` to `GetLeaderboard(id)` is T9 Stage 2.
- **T6 owns the round loop.** T4 exposes `SubmitHoleResult`; driving holes/stamina is T6.

---

## 1. Reuse handles (verified on disk 2026-06-26)
| Need | Concrete handle | Note |
|---|---|---|
| Interface to implement | `Assets/Scripts/Tournaments/ITournamentBackend.cs` (8 methods) | replace `StubTournamentBackend.cs` (shows exact DTO construction). |
| DTOs (return/consume) | `TournamentDefinition`, `EntryState`, `TournamentResult`, `TournamentLeaderboardEntry` (struct), `PrizeTable`/`PrizeBand`, `HoleResult`, `TournamentState`/`EntryStatus` | all in `Assets/Scripts/Tournaments/`. **Shapes are frozen — T4 fills them.** |
| Bot field (T3 ✓) | `BotFieldGenerator` — ctor `(IReadOnlyList<FakePlayerRow> roster, IReadOnlyList<BotScoreBracketRow> scoreBrackets)`; `RollField(def, cfg, holePars) → IReadOnlyList<BotCard>`; `Project(card, now) → BotProjection{Thru, RevealedStrokes, Complete}` | parsers `FakePlayerRosterParser.Parse(csv)` / `BotScoreBracketsParser.Parse(csv)`; CSVs `fake_players.csv` + `bot_score_brackets.csv`. |
| Clock (T1 ✓) | `ITournamentClock` / `TimeProviderClock(ITimeProvider)` wrapping `NetworkTimeProvider` (`Golfin.UI.Rankings.Core`) | **all** window/state/projection reads `clock.UtcNow` — never `DateTime.UtcNow`. |
| RP debit/grant | `RewardPointsManager.Instance` — `int GetPoints()`, `bool CanAfford(int)`, `bool SpendPoints(int)`, `void EarnPoints(int)` | **MonoBehaviour singleton** → wrap behind `IRewardPointsService` seam (T4 stays headless). ⚠ RP API is `int`; `EntryFeeRP`/`RpReward` are `long` → bridge with a guarded cast. |
| Per-hole par (RollField input) | `Assets/Scripts/UI/HoleDatabaseLoader.cs` → `HoleData.par` | wrap behind `IHoleParProvider` (resolve `def.ClubId` + `def.HoleSet` → `IReadOnlyList<int> holePars`); inject fixed pars in tests. |
| Definitions/prizes/bot-fields | **T2** `tournament_csv_loaders` → `TournamentDefinition`/`PrizeTable`/`BotFieldConfig` | T4 takes loaded data via an `ITournamentDataSource` seam (or constructor lists). |
| Item-reward grant | `SaveData.itemQuantities` (`Dictionary<string,int>`) via `SaveDataHost` | wrap behind `IItemRewardService.Grant(itemId, qty)`; in-memory in tests; real impl increments `itemQuantities` (may land with T5). |
| Player per-hole shape | `HoleResult` — **confirm fields** `holeIndex / strokes / timeMs / rngSeed / inputLog?` (GDD §12) | drives player countback + time in §6. Implementer cites the exact field names (Rule 8 spirit). |

---

## 2. Architecture — constructor-injected seams
`LocalTournamentBackend` takes its dependencies by constructor (production wiring composes real adapters; tests compose fakes). Keeps the logic deterministic + headless, exactly like T3.

```
LocalTournamentBackend(
    ITournamentDataSource data,     // T2-loaded definitions + prize tables + bot-field configs
    BotFieldGenerator      botGen,  // T3 (built from parsed roster + bot_score_brackets)
    ITournamentClock       clock,   // T1 — UtcNow seam
    ITournamentEntryStore  store,   // NEW seam — v1 in-memory; T5 = Golfin.Save-backed
    IRewardPointsService   rp,      // NEW seam — wraps RewardPointsManager.Instance
    IItemRewardService     items,   // NEW seam — wraps SaveData.itemQuantities
    IHoleParProvider       pars)    // NEW seam — wraps HoleDatabaseLoader → HoleData.par
```

**New seams T4 defines (interfaces + in-memory fakes; real adapters are thin):**
- `ITournamentEntryStore { EntryState? Load(string tid); void Save(EntryState e); }` — **the T4/T5 boundary.**
- `IRewardPointsService { long Balance { get; } bool TrySpend(long rp); void Grant(long rp); }` — adapter casts to the int `RewardPointsManager` API (guard `> int.MaxValue`).
- `IItemRewardService { void Grant(string itemId, int qty); }`.
- `IHoleParProvider { IReadOnlyList<int> ParsFor(string clubId, IReadOnlyList<string> holeSet); }`.

---

## 3. State derivation + resolve gate (read `clock.UtcNow`)
`DeriveState(def, now, hasEntry, entryStatus) → TournamentState`:
- `now < StartUtc` → **Upcoming**
- `StartUtc ≤ now < EndUtc` & no entry → **Open**
- `StartUtc ≤ now < EndUtc` & entry InProgress → **Playing**
- within the *ending threshold* of `EndUtc` (window still open) → **Ending** (badge overlay — see **D2**)
- `now ≥ EndUtc` & no entry → **Closed**
- `now ≥ EndUtc` & had entry → **Ended**

**Resolve gate** (separate from the badge): `IsResolved(def, now) = now ≥ EndUtc + resolveDelay`. `GetResults`/`ClaimPrize` return null / no-op until resolved; `GetLeaderboard` is `IsProvisional = !IsResolved`. **resolveDelay source = D1.**

---

## 4. The 8 methods
1. **`GetTournaments()`** — all `TournamentDefinition` from `data`; state derived per §3. (DTO carries no state field; callers derive via a helper or T4 exposes `DeriveState`.)
2. **`GetTournament(id)`** — single; `KeyNotFoundException` if absent.
3. **`Register(id, entryPaymentRP, characterId)`** — **idempotent**: if an entry exists, return it (no re-charge). Else: if `entryPaymentRP > 0` require `rp.TrySpend(entryPaymentRP)` (fail → throw/`InsufficientRP`); create `EntryState{Status=InProgress, CharacterId=characterId (locked), PerHole=[], StartedUtc=now}`; `store.Save`. (maxEntrants dropped per T2 §0 — ignore.)
4. **`GetMyEntry(id)`** → `store.Load(id)` (null if unregistered) — resume source.
5. **`SubmitHoleResult(id, result)`** — append to `PerHole`, recompute `LastHoleUtc=now`; if `PerHole.Count == def.HoleSet.Count` → `Status=Finished`; `store.Save`; return updated. (Reject duplicate hole / post-`EndUtc` submit.)
6. **`GetLeaderboard(id)`** → §5.
7. **`GetResults(id)`** — null unless `IsResolved`. Resolve the player's final standing from the §5 final board → `TournamentResult{FinalRank, IsTie, PrizeRP, ItemRewardId, Claimed}` (PrizeRP/Item from §5 split-pool). Null if player never entered.
8. **`ClaimPrize(id)`** — no-op if `Claimed` or not resolved; else `rp.Grant(PrizeRP)`, `items.Grant(ItemRewardId,1)` if non-null, set `Claimed=true` via store. **Idempotent** (claim-once guard).

---

## 5. Leaderboard — merge, rank, ties (the meaty part)
**Build the field:** `botGen.RollField(def, cfg, pars.ParsFor(def.ClubId, def.HoleSet))` → bot cards; for each, `botGen.Project(card, clock.UtcNow)` → `(Thru, RevealedStrokes, Complete)`. Merge the local player's `EntryState` (its `PerHole`).

**Provisional (window open, `!IsResolved`):**
- Bots show `Thru` + `RevealedStrokes`; player shows their completed holes.
- **Ranking key = score-to-par over completed holes, ascending** (`strokes − Σ par(completedHoles)`), so a player/bot *through 3 at −1* leads one *through 9 at E* — authentic live-leaderboard behavior. **(D3)** `Thru` is carried for the reveal; raw-strokes sort would be apples-to-oranges across different `Thru`.
- `IsProvisional = true`; banner "LIVE/PROVISIONAL".

**Final (`IsResolved`):** every bot `Complete` (by `EndUtc` all are — T3 invariant); rank by **total strokes asc** with the full **§6 tie ladder**:
1. total strokes →
2. **countback** over the ordered `HoleSet`: back-9 → back-6 → back-3 → back-1, then front-9 → front-6 → front-3 → front-1 (works for 9- or 18-hole sets) →
3. total completion time (player: Σ `HoleResult.timeMs`; bot: `lastCompletionUtc − botStart`) →
4. submission timestamp (player: `LastHoleUtc`; bot: last `PerHoleCompletionUtc`).
- **Ties share rank** (`IsTie=true`, "T2" prefix); next entry skips (2-way T2 → next is 4th).
- **DNF** (player didn't finish by `EndUtc`): ranked **below all finishers**, ordered by holes-completed desc then strokes asc; DNF ties use the same ladder over completed holes. **DNF rows hidden from the ranked board** (GDD §17.4); the player's own DNF shows only in the sticky "you" row (`IsDNF=true`, `IsPlayer=true`).

**Prizes (§6.2 split-pool, for `GetResults`):** match final rank → `PrizeBand` (RankFrom..RankTo). For a tie spanning positions *p..p+k*, **pool** the RP of all spanned bands and **split evenly, rounded up** (player-favorable); **indivisible items duplicated to each tied player** (D-Tie locked, recorded on `PrizeBand`/`TournamentResult`). Step-4 timestamp fixes *display order only*, never prize value.

---

## 6. Acceptance — EditMode invariant/unit suite (THE gate; Rule 3)
Headless NUnit in `Golfin.Tournaments.Tests`, all fakes injected (fixed clock, in-memory store/RP/items, fixed pars, seeded bot field). Pass = all green.
- **State derivation:** each of Upcoming/Open/Playing/Ending/Closed/Ended at boundary `now` values; resolve gate flips exactly at `EndUtc + resolveDelay`.
- **Register:** debits exactly once; idempotent re-register returns same entry, no double-charge; insufficient RP rejected, entry not created; free entry (fee 0) skips debit; character locked.
- **SubmitHoleResult:** append + status→Finished on last hole; duplicate/late submit rejected; persisted via store (reload equals).
- **Leaderboard final ranking:** strokes asc; **countback** separates equal-stroke entries (pinned fixtures incl. back-9 then front-9 paths); time then timestamp as the deep fallbacks.
- **Ties:** shared rank + "T" flag; next rank skips correctly (N-way).
- **DNF:** below all finishers; hidden from ranked rows; player DNF visible in sticky row; DNF ordering by holes-done desc then strokes.
- **Provisional:** ranks by score-to-par-so-far (D3 fixture: thru-3 −1 ranks above thru-9 E); `IsProvisional` true pre-resolve, false post-resolve.
- **Prizes / split-pool:** band match; 2-way + N-way tie pools spanned bands, RP rounded-up even split; indivisible item duplicated to each tied player; boundary-straddling tie (spans rank 10|11) pooled correctly.
- **GetResults/ClaimPrize:** null before resolve; correct rank/prize after; **claim-once** (second claim no-ops, RP/items granted exactly once).
- **Determinism via clock:** same `(seed, fixedNow)` ⇒ identical board across runs (T3 purity carried through merge).

---

## 7. Staging
- **Stage 1** — `LocalTournamentBackend` skeleton + the 4 new seams + in-memory fakes + `DeriveState`/resolve gate + `GetTournaments`/`GetTournament` + state/data tests.
- **Stage 2** — `Register` / `GetMyEntry` / `SubmitHoleResult` (RP debit, char lock, idempotent, append+persist, Finished) + tests.
- **Stage 3** — `GetLeaderboard`: build field (T3), project by clock, merge player, §6 ranking + tie ladder + DNF + provisional/final + tests *(the meaty stage)*.
- **Stage 4** — `GetResults` + `ClaimPrize`: resolve gate, band match, §6 split-pool, claim-once idempotent, RP+item grant + tests.
- *(Later: T5 swaps the in-memory store for the `Golfin.Save`-backed impl; T9 binds `GetLeaderboard` to the screen.)*

---

## 8. Decisions for Cesar
- **D1 — resolveDelay source.** `TournamentDefinition` has **no `ResolveDelayMinutes`** (GDD §9 CSV does; T2 §0 already flagged adding it). **Rec:** T2 adds the field, T4 reads `def.ResolveDelayMinutes`; until T2 lands it, T4 uses a const default (5 min). → *confirm T2 adds the field.*
- **D2 — "Ending" badge threshold.** When does Open/Playing flip to **Ending**? **Rec:** last 1 hour of the window (or a per-field config column). Tune.
- **D3 — provisional ranking.** Rank in-progress entries by **score-to-par over completed holes** (rec — authentic live board) vs raw revealed strokes (apples-to-oranges across `Thru`). Final board is always total strokes + §6.
- **Out of scope (noted):** *cancel → RP refund* — `ITournamentBackend` has **no Cancel method** (8 methods, none); v1 has no un-register UI → deferred. RP `int`↔`long` bridging handled by the `IRewardPointsService` adapter (guarded cast).

---

## Source links
Plan: `Docs/Game Design/Tournaments_Implementation_Plan.md` (T4 lines 47–59, dep graph 116–123). GDD: `Tournaments_GDD.md` §3/§5/§6/§7/§10. Contracts: `Assets/Scripts/Tournaments/*.cs`. T3 API: `BotFieldGenerator.cs` / `BotFieldMath.cs`. RP: `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs`. Par: `Assets/Scripts/UI/HoleDatabaseLoader.cs` → `HoleData.par`. Save (T5 boundary): `Assets/Scripts/Save/{SaveData,SaveSchemaMigrator}.cs` (schemaVersion 2).
