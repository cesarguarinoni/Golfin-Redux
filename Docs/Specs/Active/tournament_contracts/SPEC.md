# Tournament Contracts (T1) — Code-Proof Implementation Spec

> **Order:** T1 `tournament_contracts` (Implementation Plan Phase A). **Class:** FULL PIPELINE (new asmdef = risk).
> **Authority:** `Docs/Game Design/Tournaments_GDD.md` §6 (tie ladder), §7 (bots), §8 (backend seam), §9 (CSV data) **+ §17 Addendum** (read §17 first — it overrides earlier sections). Implementation Plan §Phase A/B.
> **Goal:** Lock the `Golfin.Tournaments` namespace, leaf asmdef, DTOs, and `ITournamentBackend` interface so everything downstream (T2 loaders, T3 bot field, T4 backend, T5 save, UI binding) compiles against a frozen shape. **Contracts only — zero logic.**

---

## 0. Rules Code MUST follow

1. **CONTRACTS ONLY.** DTOs, enums, and interfaces. **No** CSV parsing (T2), **no** bot rolling/projection (T3), **no** ranking/prize/tie math (T4), **no** save writes (T5), **no** UI. If a method needs a body, it's out of scope — declare the interface, not the implementation. The only concrete code is (a) plain DTO/struct definitions and (b) one in-memory stub backend so downstream compiles (see §5).
2. **REUSE existing infra — never duplicate (HARD).** This is the whole point of the correction: the bot population already exists. See the reuse map §4. Specifically:
   - **Bot identities** come from the existing `Assets/Resources/Data/fake_players.csv` (`id,username,characterId,level`) — the **same 100+ roster** that feeds Rankings + 1v1. The tournament bot field draws identities from here; it does **not** define new players.
   - **Skill brackets** reuse the `Assets/Resources/Data/bot_difficulty.csv` bracketing concept for the strokes distribution (GDD §7).
   - **Clock** reuses the existing network-time seam (`ITimeProvider` / `NetworkTimeProvider`, currently in `Golfin.UI.Rankings`) — device clock is untrusted (anti-cheat), same rule as Rankings. Do **not** add a second time source.
   - **Leaderboard entry shape** mirrors the existing `LeaderboardEntry` struct (`Golfin.UI.Rankings`) — but **strokes-based** (see `TournamentLeaderboardEntry`, §2).
3. **Forward-compat (GDD §8):** every DTO is identical for `LocalTournamentBackend` (v1) and `RemoteTournamentBackend` (later). Anti-cheat slots (`rngSeed`, `inputLog`) are designed in now and ignored in v1 — so no save-schema break later.
4. **Determinism:** the bot field + leaderboard are a **pure function of (seed, now)** (GDD §7). T1 only defines the shapes that make that possible (`BotCard`, `BotFieldConfig`, `ITournamentClock`); the projection itself is T3/T4.

---

## 1. asmdef

- New **leaf** asmdef **`Golfin.Tournaments`** at `Assets/Scripts/Tournaments/`.
- **Depends on:** `Golfin.Save`, `Golfin.Roster` (character/economy), the CSV/data util asmdef, and the time seam (`ITimeProvider`).
- **Nothing existing depends on this** — it's a leaf. **UI binds to it later** via `ITournamentBackend` (UI → Tournaments, never the reverse).
- ⚠️ **FLAG (plan inconsistency):** the Implementation Plan note says the asmdef "depends on … UI." That's a cycle risk — UI must depend on contracts, not vice versa. The only pull toward UI is `ITimeProvider`/`NetworkTimeProvider` living under `Assets/Scripts/UI/Rankings/`. **Decision needed (§7 flags):** either (a) reference the Rankings asmdef just for the time seam (pragmatic, but a UI dep), or (b) extract `ITimeProvider`/`NetworkTimeProvider` into a shared leaf util (cleaner). **Rec: (b)** if cheap, else (a) with a TODO. Do not make `Golfin.Tournaments` depend on a screen/controller.

---

## 2. DTOs / enums (define exactly these shapes)

**`TournamentDefinition`** (one row of `tournaments.csv`, GDD §9):
`id`, `nameKey`, `clubId`, `holeSet` (explicit hole-id list — see flag), `startUtc`, `endUtc`, `entryFeeRP` (long; 0 = free), `prizeTableId`, `botFieldId`, `sponsorKey`, `leagueKey`.

**`TournamentState`** (derived enum; maps to the Figma `13386:1758` badges):
`Upcoming` (UPCOMING) · `Open` (OPEN) · `Playing` (player entered, in window) · `Ending` (ENDING — near endUtc) · `Closed` (CLOSED — window over, not entered) · `Ended` (ENDED — over, was entered). Derived from `(startUtc, endUtc, now, hasEntry, entryStatus)` — **derivation is T4**, T1 just defines the enum.

**`EntryState`:** `tournamentId`, `characterId` (locked at sign-up, GDD S1), `perHole` (`HoleResult[]`), `startedUtc`, `lastHoleUtc`, `status` (`EntryStatus`: `NotEntered`/`InProgress`/`Finished`/`DNF`).

**`HoleResult`:** `holeId`, `strokes` (int), `timeSeconds` (float), `completedUtc`, **`rngSeed`** (anti-cheat), **`inputLog`** (shot-command list; opaque v1). `perHole[]` already gives countback data for the tie ladder (GDD §6 note — no extra field).

**`TournamentLeaderboardEntry`** (mirror of `Golfin.UI.Rankings.LeaderboardEntry`, **strokes-based**): `rank` (1-based), `isTie`, `displayName`, `characterId` (portrait/rarity art), `level`, **`strokes`** (replaces `Score`), `thru` (holes completed — drives "thru 7" organic reveal), `timeSeconds` (human tiebreak, GDD §17 ruling #3), `isPlayer`, `isDNF`, `isProvisional` (board still live vs final).

**`PrizeTable` / `PrizeBand`** (`tournament_prizes.csv`, GDD §10): band = `rankFrom`, `rankTo`, `rpReward` (long), `itemRewardId` (nullable). Split-pool/tie resolution is **T4**.

**`BotFieldConfig`** (`tournament_bot_fields.csv`, GDD §7): `botFieldId`, `botCount`, skill-bracket weights, pace-spread params (start-offset range + per-hole spread).

**`BotCard`** (pre-rolled per bot, GDD §7 — shape only, rolling is T3): `botId` (→ `fake_players.csv` identity), `perHoleStrokes[]`, `totalStrokes`, `startOffsetSeconds`, `perHoleCompletionUtc[]` (seeded schedule for organic reveal).

---

## 3. Interfaces

**`ITournamentBackend`** — exact signatures from GDD §8 (return types use §2 DTOs):
```
IReadOnlyList<TournamentDefinition> GetTournaments();          // + derived state via T4
TournamentDefinition GetTournament(string id);
EntryState Register(string id, long entryPaymentRP, string characterId);  // debit RP if needed, lock character
EntryState GetMyEntry(string id);                              // resumable
EntryState SubmitHoleResult(string id, HoleResult result);     // append + persist (local) / POST (remote)
IReadOnlyList<TournamentLeaderboardEntry> GetLeaderboard(string id);  // provisional (projected) or final
TournamentResult GetResults(string id);                        // final rank + prize (gated by state)
void ClaimPrize(string id);                                    // §17 #6 auto-claim modal calls this
```
- `TournamentResult` DTO: `finalRank`, `isTie`, `prizeRP`, `itemRewardId`, `claimed` (bool).

**`ITournamentClock`** — `DateTime UtcNow { get; }`. **Wraps the existing `NetworkTimeProvider`** (don't reimplement). The leaderboard's organic reveal reads `UtcNow` (GDD §7).

---

## 4. Reuse map — existing assets the contracts point at (verified 2026-06-25)

| Need | REUSE this existing asset | Note |
|---|---|---|
| Bot identities | `Assets/Resources/Data/fake_players.csv` (`id,username,characterId,level`) | same roster as Rankings/1v1; `BotCard.botId` references these ids |
| Skill brackets | `Assets/Resources/Data/bot_difficulty.csv` | strokes-distribution bracketing (GDD §7) |
| Bot club data | `Assets/Resources/Data/bot_clubs.csv` | if strokes model needs club/loadout |
| Network time | `NetworkTimeProvider` / `ITimeProvider` (`Assets/Scripts/UI/Rankings/`) | `ITournamentClock` wraps it — see §1 flag |
| Entry-struct template | `LeaderboardEntry` (`Golfin.UI.Rankings`, `ILeaderboardProvider.cs`) | `TournamentLeaderboardEntry` mirrors it, strokes-based |
| RP debit on entry | `RewardPointsManager` → `SaveDataHost` | called by `Register` impl (T4), not T1 |
| Player entry persistence | `Golfin.Save` host | save schema is T5; T1 only defines `EntryState` |

### 4.1 — The Tournament Leaderboard ALREADY renders the real roster (do not "add bots")
The Stage-1 `TournamentLeaderboardScreenController` already fills podium / rows / sticky from the **real fake-bot roster** via `LeaderboardManager.Instance.GetRanking(LeaderboardPeriod.Daily)` — the same `fake_players.csv` identities (FRODO, GANDALF…), bound through the existing `Top3CardWidget`/`RankingsCardWidget` so character art + rarity match — with the pill relabeled `"<n> STROKES"`. **So the bots are real on-screen today.** The only stopgap is that the **order is by RP** and the **numbers are RP-relabeled**, not real tournament strokes/`thru`.

Implication for the chain: **T4 does NOT add a bot population.** It swaps the controller's fill source from `LeaderboardManager.GetRanking(Daily)` → `ITournamentBackend.GetLeaderboard(id)`, so the **same roster** gets real per-seed strokes, strokes-ordering, `thru` projection (GDD §7), and a real sticky "you" row. Expose the backend through a **singleton seam mirroring `LeaderboardManager`** (`private ILeaderboardProvider _provider = new LocalFakeLeaderboardProvider();` is the exact pattern to clone for `ITournamentBackend`). T1 just defines the interface that singleton will hold.

---

## 5. Tests (EditMode) — compile-gate only

- An in-memory **`StubTournamentBackend : ITournamentBackend`** (returns empty/fixed data) so T2–T4 + UI can compile and reference the seam before the real backend exists. No game logic.
- DTO sanity: round-trip construct each DTO; assert `TournamentState`/`EntryStatus` enums are exhaustive; assert `HoleResult` carries `rngSeed`/`inputLog`.
- Real ranking/projection/prize tests land in **T3/T4** (this order proves shape, not behavior).

---

## 6. Out of scope (explicit — do NOT do here)
CSV parsing/validation (**T2**) · bot pre-roll + schedule projection (**T3**) · ranking, tie ladder, split-pool prizes, state derivation (**T4**) · save schema (**T5**) · round flow/stamina (**T6**) · any UI binding (T8b/T9/T7).

## 7. Flags / decisions
- **`holeSet` representation** — explicit hole-id list vs count+range. **Rec: explicit hole-id list** (Lomond holes exist; other clubs placeholder) so a tournament can use any subset.
- **Time-seam asmdef** — see §1 flag (reference Rankings asmdef vs extract a shared util). Rec: extract if cheap.
- **D-Tie indivisible-item rule** (GDD §6.4) — default *duplicate to each tied player*; record as an XML-doc note on `PrizeBand`/`TournamentResult` so T4 honors it.
- **Anti-cheat `inputLog` type** — opaque list (e.g. `IReadOnlyList<string>` or a small `ShotCommand` struct). Rec: minimal `ShotCommand` struct now so the shape is stable; v1 never reads it.
