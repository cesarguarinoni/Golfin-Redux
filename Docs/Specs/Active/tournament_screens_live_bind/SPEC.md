# tournament_screens_live_bind — Bind Selection + Leaderboard to the live backend

**Tier:** TELLCODE — bind live data into existing screens ("don't rebuild, bind"). No new hierarchy.
Gated by: compiles + an EditMode test for the pure state→CardState mapping + a **bot-recorded video**
(selection shows 6 live tournaments; leaderboard shows live standings).
**Depends:** `tournament_backend_bootstrap` ✓ (`TournamentService.Instance.Backend`).
**This is Stage 2 of the existing T7 (selection) + T9 (leaderboard) scaffolds.**

---

## 0. Boundary
**In:** Selection screen → live `GetTournaments()`/`DeriveState()`/`GetMyEntry()`; real state badges, real
filter-by-state, CTA carries the tournament id. Leaderboard screen → live `GetLeaderboard(id)`. The shared
`SelectedTournamentId` handoff + a `GetTopPrizeRP` accessor.
**Out (do NOT touch):** the round loop / "play" (T6 — CTA just navigates to the still-scaffold hole-select);
**registration is NOT triggered here** (no `Register` on tap — that's T6's entry/confirm flow, to avoid
charging RP on a tap); results/claim screen; Notify-me + expand-chevron affordances; full provisional
projection polish.

---

## 1. Shared plumbing (small, additive — both reused by T6)
On **`TournamentService`** (`Assets/Scripts/TournamentsRuntime/TournamentService.cs`):
- `public string? SelectedTournamentId { get; set; }` — set by a card CTA before navigating; read by the
  Leaderboard (and later T6 hole-select). Null-guard consumers.
- `public long GetTopPrizeRP(string tournamentId)` — the card's headline reward. `Compose()` already loads
  the prize tables (currently handed only to the backend) — **also cache them on the service** and resolve:
  look up `def.PrizeTableId` → the band covering rank 1 → `band.RpReward`. Return 0 if absent. *(No change
  to the headless backend — keep the churn in the runtime layer.)*

---

## 2. Selection screen — `TournamentSelectionScreenController`
Replace the static path; keep the entire view/prefab/tab machinery.

- **Delete/bypass** `StaticCards[]`. In `RebuildCards()`, iterate
  `TournamentService.Instance.Backend.GetTournaments()`. For each `def`:
  `var entry = backend.GetMyEntry(def.Id); var state = backend.DeriveState(def, DateTime.UtcNow);`
  then compute the card fields and call the **existing `card.BindStatic(...)`** (no new bind method needed)
  + set `card.TournamentId = def.Id` (add this field — see below).
- **State → `CardState` (pure static fn `MapCardState(state, entryStatus, nowPastEnd)` — EditMode-tested):**

  | Condition | CardState | CTA |
  |---|---|---|
  | `now < StartUtc` | `Upcoming` | "UPCOMING" (disabled) |
  | entry != null, `Status==InProgress`, in window | `EnteredActive` | "CONTINUE" (LIVE) |
  | entry != null, `Status==Finished` | `EnteredFinished` | "LEADERBOARD" |
  | entry != null, `now >= EndUtc` | `EnteredFinished` | "LEADERBOARD" |
  | no entry, `DeriveState==Ending` | `Ending` | "SIGN UP" |
  | no entry, `DeriveState==Open` | `Open` | "SIGN UP" |
  | no entry, `now >= EndUtc` (Closed) | `Ended` | "LEADERBOARD" |

- **Field derivations:** `name` = `def.NameKey` (raw for v1; localization later); `clubLine` =
  `$"{clubDisplay} - {def.HoleSet.Count} Holes"` *(club display name — use ClubId for v1, prettify later)*;
  `dateLine` = state-formatted (`"Starts in 8d"` / `"Jun 20 — Jun 27"` / `"Ends in 3d 04h"` /
  `"Round in progress — Hole {entry.PerHole.Count} of {N}"` / `"Round complete"` / `"Ended {date}"`);
  `isFreeEntry` = `def.EntryFeeRP == 0`; `entryRp` = `(int)def.EntryFeeRP`; `rewardRp` =
  `(int)TournamentService.Instance.GetTopPrizeRP(def.Id)`; `ctaText` per the table.
- **Course image:** index-by-CSV-order is fragile. Add a serialized `[{string id, Sprite sprite}]` map and
  look up by `def.Id`; fall back to the existing `_courseImages[i]` order if unmapped.
- **Filter tabs:** unchanged — cards already carry `CardState`; `ApplyFilter()`/`Matches()` stay as-is
  (now driven by real states).
- **CTA (`HandleCtaClicked`):** set `TournamentService.Instance.SelectedTournamentId = card.TournamentId`
  **first**, then the existing nav switch (gold Open/Ending/EnteredActive → hole-select [sign-up/continue —
  the actual entry+`Register` is T6]; silver EnteredFinished/Ended → leaderboard; Upcoming → no-op).

**`TournamentSelectionCard`** — additive only: `public string TournamentId { get; private set; }`, set inside
`BindStatic` (add a param) or a tiny `SetTournamentId(id)`. Nothing else changes.

---

## 3. Leaderboard screen — `TournamentLeaderboardScreenController`
The scaffold's `PopulateBots()` already binds the podium/rows/sticky via `Top3CardWidget`/`RankingsCardWidget`
+ `SetStrokes` — **reuse that exact widget-bind pattern, swap only the data source.**

- Source: `var board = TournamentService.Instance.Backend.GetLeaderboard(TournamentService.Instance.SelectedTournamentId);`
  Guard null/empty id → log + early-return (normal nav always sets it).
- Each `TournamentLeaderboardEntry` (Rank, DisplayName, CharacterId, Level, Strokes, Thru, IsPlayer, IsDNF,
  IsProvisional, IsTie) → bind:
  - **Art/rarity:** resolve the entry's `CharacterId` (bots = `BotId`) against the **same fake-player roster
    the bots were generated from** (the roster `LeaderboardManager` already exposes from `fake_players.csv`)
    to get a `LeaderboardEntry` with art; then override `Rank` + the strokes pill from the tournament entry.
    *(Implementer: cite the `LeaderboardManager`/`LeaderboardEntry` handles — the scaffold already uses them.)*
  - Podium = top-3 ranked; rows (`TournamentRankingRow*`) = rank 4+; **sticky** = the `IsPlayer` row
    (`Strokes` total; rank = `"--"` while `IsProvisional`/unfinished, else `Rank`).
  - `SetStrokes(card, entry.Strokes)` replaces the hardcoded 68/70/71/82.
- **v1 simplicity:** render whatever `GetLeaderboard` returns (it already handles provisional vs final, DNF,
  ties internally). No extra projection logic here. DNF player row → keep the `"--"`/DNF treatment.

---

## 4. Acceptance
- **EditMode:** `MapCardState(...)` unit test covers all 7 rows of the table above.
- Compiles (TournamentsRuntime + UI assemblies).
- **Bot-recorded video (visual gate):** (a) Selection screen shows the **6 CSV tournaments** with correct
  badge/fee/reward, tabs filter by real state; (b) tap a card → leaderboard opens for **that** tournament and
  shows live `GetLeaderboard` standings (real bot names/art + the player sticky row).
- Manual note in IMPLEMENTER_REPORT: no exceptions; `SelectedTournamentId` round-trips selection→leaderboard.

## 5. Baked choices (rec — proceed, no sign-off needed)
Handoff via `TournamentService.SelectedTournamentId`; reward via `TournamentService.GetTopPrizeRP`; course
image id-keyed (fallback to order); **registration deferred to T6** (CTA only navigates); `NameKey`/`ClubId`
shown raw in v1 (localize/prettify later).

## Source links
Selection: `UI/Tournaments/TournamentSelectionScreenController.cs` (`StaticCards`/`RebuildCards`/`HandleCtaClicked`),
`TournamentSelectionCard.cs` (`BindStatic`/`CardState`). Leaderboard: `TournamentLeaderboardScreenController.cs`
(`PopulateBots`/`BindCard`/`SetStrokes`). Backend: `LocalTournamentBackend.DeriveState`/`GetMyEntry`/`GetLeaderboard`,
`TournamentService.cs`. State enum: `TournamentEnums.cs` (`TournamentState`/`EntryStatus`).
