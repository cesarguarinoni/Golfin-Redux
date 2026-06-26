# Tournament CSV Loaders (T2) — Code-Proof Implementation Spec

> **Order:** T2 `tournament_csv_loaders` (Plan Phase B). **Class:** FULL PIPELINE.
> **Authority:** GDD §9 (CSV data model) + §10 (prizes). DTOs = the **shipped T1 contracts** in `Assets/Scripts/Tournaments/` (T1 DONE, commit `6f803f437`). **Reuse the existing CSV-loader pattern — do not invent a parser.**
> **Goal:** Author the three tournament CSVs + a loader that parses them into the T1 DTOs (`TournamentDefinition`, `PrizeTable`/`PrizeBand`, `BotFieldConfig`), with EditMode tests. **Data + parsing only — no bot rolling (T3), no backend logic (T4), no UI.**

---

## 0. ⚠ T1 CONTRACT RECONCILIATION — resolve these FIRST
The shipped T1 DTOs don't 1:1 match GDD §9/§10. Each needs a decision before/while writing the loader:

1. **`resolveDelayMinutes` has no home.** GDD §9 lists it per-tournament and `ITournamentBackend.GetResults` doc references `now >= endUtc + resolveDelay`, but `TournamentDefinition` has **no field for it**. → **REC: amend T1 — add `public int ResolveDelayMinutes { get; }` to `TournamentDefinition`** (+ ctor arg + stub). 1-field additive change; re-run T1 tests. *(Alt: a global constant — rejected, GDD says per-tournament.)*
2. **Percentile prize bands unsupported.** GDD §10 has `bandType = Rank | Percentile`; shipped `PrizeBand` is **Rank-only** (`RankFrom`/`RankTo`). → **REC: v1 ships Rank bands only.** Defer percentile to a future T1 amendment; the 3 sample tables (§3) use Rank bands exclusively. Flag in GDD.
3. **Reward model collapsed.** GDD §10 row = `rewardType`/`rewardId`/`quantity` (generic, one reward per row); shipped `PrizeBand` = `RpReward` (long) **+** `ItemRewardId` (string?, one item). → **REC: v1 CSV is one-row-per-band** = `rpReward` + optional `itemRewardId` (covers "5,000 + Ticket"). Multi-item / non-RP-only bands deferred.
4. **`courseId` (GDD/CSV) vs `ClubId` (DTO).** Loader maps `courseId` column → `ClubId`. **REC: keep CSV header `courseId`** per GDD §9; map in the loader.
5. **`sponsorKey` / `leagueKey` are on the DTO but NOT in GDD §9.** They feed the Hole-Selection/Leaderboard identity pills ("SPONSORED BY PUMA", "… DIAMOND LEAGUE"). → **Add both columns to `tournaments.csv`** (extends §9); reconcile GDD.
6. **`entryType` / `maxEntrants` (GDD §9) have no DTO field.** `entryType` is derivable (`Free` ⇔ `entryFeeRP == 0`) — **drop the column**. `maxEntrants` — v1 doesn't cap (local + bots); **omit** (or keep an ignored column). 

*(If Cesar approves #1, that's a tiny T1 reopen — do it as the first commit of T2 so the loader can populate the field.)*

---

## 1. Scope
1. **Loader** `TournamentCsvLoader` (POCO, in `Golfin.Tournaments` asmdef) — **not** a MonoBehaviour, so EditMode tests call it directly and T4's backend owns lifecycle:
   - `IReadOnlyList<TournamentDefinition> LoadTournaments()`
   - `IReadOnlyDictionary<string, PrizeTable> LoadPrizeTables()`
   - `IReadOnlyDictionary<string, BotFieldConfig> LoadBotFields()`
   - helpers: ISO-8601 UTC parse, `holeSet` expansion (`"1-18"`/`"1,4,7"` → `IReadOnlyList<string>`), `bracketWeights` parse (`"1:0.1;10:0.2;…"` → dict).
   - Loads each `TextAsset` via `Resources.Load<TextAsset>("Data/<name>")` — same as `ModesDatabaseCSV` (§4).
2. **Three sample CSVs** in `Assets/Resources/Data/` (authored content in §3).
3. **EditMode tests** (§5) in the existing `Golfin.Tournaments.Tests` asmdef.

---

## 2. CSV schemas (reconciled — column → DTO field)
**Parse rules (all three):** skip blank lines and `#`-prefixed comment lines (the existing `bot_difficulty.csv` uses `#` headers — match it); first non-comment line = header; map columns by header name (not index order); trim cells; UTF-8.

### `Data/tournaments.csv` → `TournamentDefinition`
| CSV col | → DTO field | parse |
|---|---|---|
| `id` | `Id` | string (also the bot-field seed) |
| `nameKey` | `NameKey` | string (loc key) |
| `courseId` | `ClubId` | string (map name) |
| `holeSet` | `HoleSet` | expand `"1-18"`/`"1,4,7"` → list of hole-id strings |
| `startUtc` / `endUtc` | `StartUtc` / `EndUtc` | ISO-8601 UTC `DateTime` (`DateTimeStyles.AdjustToUniversal`) |
| `resolveDelayMinutes` | `ResolveDelayMinutes` *(pending §0.1)* | int |
| `entryFeeRP` | `EntryFeeRP` | long (0 = free) |
| `botFieldId` | `BotFieldId` | string (→ bot_fields) |
| `prizeTableId` | `PrizeTableId` | string (→ prizes) |
| `sponsorKey` | `SponsorKey` | string (identity pill) |
| `leagueKey` | `LeagueKey` | string (identity pill) |

### `Data/tournament_prizes.csv` → `PrizeTable` / `PrizeBand`
Header: `prizeTableId, rankFrom, rankTo, rpReward, itemRewardId`. Group rows by `prizeTableId` → one `PrizeTable` with a `PrizeBand[]`. `itemRewardId` blank → `null`.

### `Data/tournament_bot_fields.csv` → `BotFieldConfig`
Header: `botFieldId, botCount, bracketWeights, startOffsetMinSec, startOffsetMaxSec, perHoleSpreadSec`. `bracketWeights` = `;`-list of `minLevel:weight` (keys MUST be `bot_difficulty.csv` minLevels: **1,10,25,50,100,180**) → `IReadOnlyDictionary<string,float>`.

---

## 3. Authored sample data (drop verbatim — reproduces the six Figma cards `13386:1758`)
> Dates are concrete around 2026-06-25 so the cards land on their Figma states once T4 derives state from `now`. The loader just loads — **state derivation is T4.**

**`tournaments.csv`**
```
# Tournament definitions — one row per tournament. courseId→ClubId; holeSet expands ranges.
id,nameKey,courseId,holeSet,startUtc,endUtc,resolveDelayMinutes,entryFeeRP,botFieldId,prizeTableId,sponsorKey,leagueKey
kasumigaseki_open,tourn.kasumigaseki,kasumigaseki,1-18,2026-06-23T00:00:00Z,2026-06-29T00:00:00Z,30,0,field_major,prize_major,PUMA,DIAMOND
hirono_invitational,tourn.hirono,hirono,1-18,2026-06-20T00:00:00Z,2026-06-26T12:00:00Z,30,0,field_major,prize_major,GOLFIN,DIAMOND
lomond_championship,tourn.lomond,lomond,1-18,2026-06-24T00:00:00Z,2026-06-27T00:00:00Z,30,0,field_medium,prize_medium,GOLFIN,GOLD
gotemba_masters,tourn.gotemba,gotemba,1-18,2026-06-21T00:00:00Z,2026-06-25T22:00:00Z,30,500,field_major,prize_major,TAIHEIYO,GOLD
kisarazu_cup,tourn.kisarazu,kisarazu,1-18,2026-07-02T00:00:00Z,2026-07-05T00:00:00Z,30,0,field_small,prize_small,GOLFIN,SILVER
kawana_fuji_open,tourn.kawana,kawana,1-18,2026-06-08T00:00:00Z,2026-06-13T00:00:00Z,30,0,field_major,prize_major,GOLFIN,DIAMOND
```

**`tournament_prizes.csv`** (the 3 GDD §10 templates, Rank bands; item ids are placeholders to confirm)
```
# Rank-band prize tables. rpReward = RP; itemRewardId blank = RP only.
prizeTableId,rankFrom,rankTo,rpReward,itemRewardId
prize_small,1,1,3000,
prize_small,2,3,1500,
prize_small,4,10,500,
prize_medium,1,1,5000,ticket_gold
prize_medium,2,3,3000,
prize_medium,4,10,1000,
prize_major,1,1,20000,trophy_major
prize_major,2,3,12000,ticket_gold
prize_major,4,10,5000,
prize_major,11,50,1000,
```

**`tournament_bot_fields.csv`** (weights across the bot_difficulty brackets)
```
# Bot field configs. bracketWeights = minLevel:weight pairs (keys = bot_difficulty.csv minLevels).
botFieldId,botCount,bracketWeights,startOffsetMinSec,startOffsetMaxSec,perHoleSpreadSec
field_small,12,1:0.30;10:0.30;25:0.20;50:0.20,0,7200,600
field_medium,20,10:0.25;25:0.30;50:0.25;100:0.20,0,7200,540
field_major,30,25:0.20;50:0.25;100:0.30;180:0.25,0,10800,480
```

---

## 4. Reuse map
| Need | REUSE | Note |
|---|---|---|
| CSV load + parse pattern | **`ModesDatabaseCSV`** (`Resources.Load<TextAsset>("Data/modes")`, line-split, header parse) | clone the parse, but as a **POCO** not a MonoBehaviour |
| Header-index map / trim convention | `CharacterDatabaseCSV` | same project idiom |
| `#`-comment + header convention | existing `bot_difficulty.csv` | skip `#` + blank lines |
| Bracket ids for `bracketWeights` | `bot_difficulty.csv` minLevels (1/10/25/50/100/180) | keys must match |
| Load targets | shipped T1 DTOs in `Assets/Scripts/Tournaments/` | exact field names per §2 |
| CSV location | `Assets/Resources/Data/` | alongside `fake_players.csv` etc. |

---

## 5. Tests (EditMode, `Golfin.Tournaments.Tests`)
- `LoadTournaments()` → 6 rows; assert `lomond_championship` → `ClubId=lomond`, `EntryFeeRP=0`, `HoleSet.Count=18`, `StartUtc`/`EndUtc` parsed UTC, `SponsorKey=GOLFIN`, `LeagueKey=GOLD`.
- `holeSet` expansion: `"1-18"`→18 ids; `"1,4,7"`→3 ids.
- `LoadPrizeTables()` → 3 tables; `prize_medium` band #1 → `RpReward=5000`, `ItemRewardId=ticket_gold`; band 4-10 → `ItemRewardId=null`.
- `LoadBotFields()` → 3 configs; `field_major` → `BotCount=30`, `BracketWeights` sums to 1.0 (±epsilon), keys ⊂ {1,10,25,50,100,180}.
- **Referential integrity:** every `tournaments.csv` `prizeTableId`/`botFieldId` resolves in the other two CSVs (loader logs a clear error on a dangling id).

---

## 6. Out of scope
Bot pre-roll + schedule projection (**T3**) · ranking/tie/prize resolution + state derivation (**T4**) · save (**T5**) · UI. The loader returns config; it does **not** roll bots or compute leaderboards.

## 7. Flags / decisions
- **§0.1 `ResolveDelayMinutes` T1 amendment** — approve? (rec: yes, do it as T2's first commit). If no, loader parks the value (loses it) — not recommended.
- **Item reward ids** (`ticket_gold`, `trophy_major`, …) — confirm against the real inventory/item ids, or leave as placeholders for T4 grant wiring.
- **`courseId` values** (`kasumigaseki`, `hirono`, `lomond`, …) — confirm against existing Country-Club/Course ids; only `lomond` has real holes today (others placeholder).
- **`nameKey`** — JP/EN loc keys not yet wired; sample uses `tourn.<x>` placeholders.
