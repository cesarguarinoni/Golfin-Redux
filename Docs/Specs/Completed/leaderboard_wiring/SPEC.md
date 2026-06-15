# SPEC — `leaderboard_wiring` (Phase 1)

- **Order:** TBD (Architect to file in Notion — next after 353)
- **Tier:** TELLCODE Tier 2 (existing UI hierarchy + established patterns: CSV-first data, SaveData, event-driven UI, ScreenManager). No spatial math, no visual-fidelity rebuild.
- **Status:** SPEC_READY
- **Author:** Architect
- **Written:** 2026-06-15 14:09 JST
- **Kickoff:** `Use the implementer subagent on "leaderboard_wiring"`

---

## 0. One-paragraph summary

The Leaderboard screen is **already fully built** in Unity (`Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` + `RankingsCards.prefab` row + `RankingsCardUser.prefab` pinned row). It has **no controller and no data**. This task adds the data layer and binds it to the existing hierarchy — **do not rebuild any UI**. It ranks players by **Reward Points earned in a period** across four tabs (Daily / Weekly / Monthly / Historic), populated by a shared roster of **100+ fake players** plus the real player. Two header entry icons (Home + Hole Select) open the screen. The same fake-player roster will later feed 1v1 matchmaking opponents (**Phase 2, separate spec — out of scope here**).

---

## 1. Locked design decisions (Cesar, 2026-06-15)

1. **One leaderboard.** Title is **"LEADERBOARD"** (drop the mock "MISSIONS LEADERBOARD"). Metric = **RP earned in the active period**.
2. **All four period tabs in v1:** Daily, Weekly, Monthly (rolling, reset) + **Historic = lifetime RP earned** (monotonic, never resets).
3. **League band is a static label** ("DIAMOND LEAGUE"), data-driven string. **No** promotion/relegation logic.
4. **Reset boundaries are UTC**, sourced from **network time** (device clock is untrusted — cheatable). Fallback to device-UTC only when offline.
5. **100+ fake players** in a single shared roster. **The same roster feeds 1v1 opponents in Phase 2.**
6. **Banner** = reuse the Home Screen banner; the layout **must also work with the banner absent** (list expands to fill).
7. **Reuse the existing design as-is. Only fix a node if wiring genuinely requires it.** Bind real data to the prefab's existing children.

---

## 2. Out of scope (do NOT build here)

- **Phase 2 — matchmaking unification:** repoint `MatchmakingModalController` to pull opponents from the shared fake-player roster (replaces its ephemeral `fakeOpponentUsernames` array). Separate spec after Phase 1 ships.
- Real league tiers / promotion / relegation.
- Any change to the Missions mode (it does not exist; "Missions" is gone from the title).
- Backend integration — but the data path **must** go through a provider interface (§5) so the partner-app backend can replace the fake source later without touching UI.

---

## 3. Existing assets (the build target — inspect via Unity, do not recreate)

`Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` — confirmed child nodes:

- `Banner` — promo strip (reuse Home banner; toggleable).
- `TabBar` with `DailyTab`, `WeeklyTab`, `MonthlyTab`, `HistoryTab`, an `ActiveIndicator` + `Sliding Area` (tab-select visuals already present).
- `League` — the "DIAMOND LEAGUE" band; has a `Label`.
- `Reset` — the "RESETS IN: …" countdown; has a `Label`.
- `Top3` — podium holding the #1 / #2 / #3 cards.
- `ScrollArea` → `Viewport` → `GridContent` — the scrolling list parent for `RankingsCards` rows.
- The pinned player row (`RankingsCardUser` prefab) — bottom, always-visible.

`RankingsCards.prefab` / `RankingsCardUser.prefab` — confirmed fields per card: `Rank`, `Portrait`, `NameLabel`, `LevelLabel`, `RartityLabel` (sic — keep the existing name), `RewardPoints`.

> **NOTE (implementer):** Confirm exact transform paths + component types via Unity MCP before binding. Treat the above names as the contract; if a node is missing a needed sub-element, flag it back rather than restructuring the prefab.

---

## 4. Data model

### 4.1 SaveData additions (`Assets/Scripts/Save/SaveData.cs`)

Add the period accumulators and bump `schemaVersion` 1 → 2:

```csharp
// ── Leaderboard: RP earned per rolling period (UTC) ──
public long lifetimeRpEarned;   // Historic tab (monotonic; never reset)
public long rpDaily;
public long rpWeekly;
public long rpMonthly;

// Period keys the accumulators currently belong to (UTC). On earn/read,
// if the computed current key != stored key, the accumulator resets to 0
// and the key updates (lazy rollover — no scheduler needed).
public long dailyPeriodKey;     // UTC day index   (floor(utcUnixSeconds / 86400))
public long weeklyPeriodKey;    // UTC week index  (Monday-anchored)
public long monthlyPeriodKey;   // year*12 + (month-1), UTC
```

> **NOTE:** Verify `SaveDataHost` migration path handles a `schemaVersion` bump gracefully (new fields default to 0, which is correct — a fresh period with 0 earned). If `SaveDataHost` has explicit per-version migration, add the v2 step there.

### 4.2 RP accumulation hook (`RewardPointsManager.EarnPoints`)

In `EarnPoints(int amount)`, after the existing balance write-through, also:
1. Roll over any stale period (compare stored key vs current UTC key from the time provider §5.3; reset to 0 on mismatch, update key).
2. Add `amount` to `lifetimeRpEarned`, `rpDaily`, `rpWeekly`, `rpMonthly`.
3. `MarkDirty()`.

`SpendPoints` does **not** touch these (earned ≠ balance — spending must not lower a leaderboard score).

> **NOTE:** Rollover also runs lazily when the leaderboard is opened (§5), so a player who hasn't earned since the boundary still shows a correct 0 for the new period.

### 4.3 Fake-player roster CSV (`Assets/Resources/Data/fake_players.csv`)

**Identity only** (scores are generated, not stored — see §4.4). ≥100 rows.

```
id,username,characterId,level
fp_001,Frodo,<characterId>,160
fp_002,Gandalf,<characterId>,200
...
```

- `id` — stable unique key (drives deterministic score seeding).
- `username` — display name (curated pool; design uses fantasy names e.g. FRODO, GANDALF, GALADRIEL, BOROMIR, ARWEN…). Provide ≥100 distinct names.
- `characterId` — an existing roster character (`CharacterDatabaseCSV`) used **only** for portrait + rarity art. Many fakes may share a `characterId`; usernames stay unique. Pull valid ids from the roster — do not invent.
- `level` — display level (1–240).

> **NOTE:** Generating 100+ rows: the implementer may write a small editor helper to emit the CSV by pairing a curated name list with cycling valid `characterId`s and randomized levels, then commit the resulting CSV. The CSV is the source of truth at runtime.

### 4.4 Scoring & drift (generated, deterministic)

Fake scores are **not** persisted — they are derived so they're stable within a period but differ per tab and drift over time:

```
score(fake, period) = seededBase(fake.id, period) + drift(fake.id, periodElapsedFraction)
```

- `seededBase` — deterministic from a hash of `(fake.id, periodKey)`; gives each fake a stable score for the current period, distributed across a believable range (e.g. a few hundred to ~tens-of-thousands of RP; tune so the real player can realistically place mid-pack early and climb).
- `drift` — slowly increases the score as the period elapses (so the board feels alive between sessions); deterministic from `fake.id` + fraction of period elapsed.
- On period rollover the `periodKey` changes → all fakes reseed → board reshuffles. Historic uses a fixed lifetime-style large base per fake so it reads as an established all-time board.

The **real player's** score for a tab = the corresponding SaveData accumulator (`rpDaily/rpWeekly/rpMonthly/lifetimeRpEarned`). Player is merged into the fake list, sorted desc by score.

### 4.5 Ranking & ties

- Sort all entries (fakes + player) by score descending.
- **Tie handling matches the design:** equal scores share a rank with a `T` prefix (e.g. `T11`, `T11`), and the next distinct score skips ranks (`…T11, T11, 14`).
- Player appears **inline at true rank** in the list **and** in the **pinned `RankingsCardUser` row** at the bottom (always visible even when their rank is off-screen).

---

## 5. Provider seam + time source

### 5.1 `ILeaderboardProvider` (new)

```csharp
public interface ILeaderboardProvider
{
    // Returns the full ranked list for the period, player already merged + ranked.
    IReadOnlyList<LeaderboardEntry> GetRanking(LeaderboardPeriod period);
    LeaderboardEntry GetPlayerEntry(LeaderboardPeriod period);
    DateTime GetPeriodEndUtc(LeaderboardPeriod period); // drives the countdown
}

public enum LeaderboardPeriod { Daily, Weekly, Monthly, Historic }

public struct LeaderboardEntry
{
    public int Rank; public bool IsTie; public string DisplayName;
    public string CharacterId; public int Level; public long Score; public bool IsPlayer;
}
```

`LocalFakeLeaderboardProvider : ILeaderboardProvider` — implements §4 (roster CSV + seeded scores + player merge). The partner backend later implements the same interface; UI is untouched.

### 5.2 `LeaderboardManager` (new singleton, optional but recommended)

Thin holder exposing the active `ILeaderboardProvider` + caching the current ranking per period for the open session. Mirrors the `*.Instance` singleton convention.

### 5.3 Network UTC time (`ITimeProvider` / `NetworkTimeProvider`)

- On app start (or first leaderboard open), fetch authoritative UTC via an **HTTP `Date` header** from a reliable host (HEAD request), compute an **offset** vs device clock, and serve `UtcNow = deviceUtc + offset`.
- **Offline fallback:** if the fetch fails, use device `DateTime.UtcNow` and flag `IsAuthoritative = false` (no hard failure — board still works).
- All period-key math and the reset countdown use this provider, **not** `DateTime.Now`/`DateTime.UtcNow` directly.

> **NOTE:** Keep the fetch async + non-blocking; cache the offset for the session. Pick a stable HTTPS host for the `Date` header (flag the chosen host in the report). This is the only network dependency in Phase 1.

---

## 6. UI wiring contract (`RankingsScreenController`, new)

Bind to the existing prefab nodes (§3). Event-driven per project convention (subscribe in `OnEnable`, unsub in `OnDisable`).

- **Tabs** (`DailyTab/WeeklyTab/MonthlyTab/HistoryTab`): on click → set active period → rebuild list → drive the existing `ActiveIndicator`/`Sliding Area` to the selected tab. Default tab on open = **Daily**.
- **List** (`GridContent`): clear + instantiate one `RankingsCards` per entry for the active period; bind `Rank` (with `T` prefix on ties), `Portrait` (from `characterId` template), `NameLabel`, `LevelLabel`, `RartityLabel` (text + rarity color via the project rarity utility — see NOTE), `RewardPoints` (formatted score).
- **Top3 podium** (`Top3`): bind the top three entries into the existing #1/#2/#3 podium slots (same fields). These podium slots are static children — populate them, don't instantiate.
- **Pinned row** (`RankingsCardUser`): bind the player's entry (true rank + score). Always visible.
- **League** (`League/Label`): set static string "DIAMOND LEAGUE" (data-driven constant; easy to swap later).
- **Reset countdown** (`Reset/Label`): tick down to `GetPeriodEndUtc(activePeriod)` using the time provider; format `Nd Hh Mm Ss`. Historic tab = hide/blank the countdown (no reset).
- **Banner** (`Banner`): reuse the Home Screen banner content. Expose a bool/toggle so the no-banner layout works (list/Top3 area expands to fill the freed space). 

> **NOTE (rarity color):** Project conventions list `RarityHelper` as the canonical rarity utility, but `grep` did not locate `RarityHelper.cs` at the expected path. Implementer: locate the existing rarity→color/label helper (it exists — matchmaking & cards already color rarity) and reuse it. **Do not** hardcode a new rarity color map.

> **NOTE (portrait/rarity resolution):** Mirror the working pattern in `MatchmakingModalController.OpponentScanRoutine`: portrait via `Resources.Load<Sprite>($"Portraits/InGame/{portraitSpriteName}")` (fallback `CharacterDataRuntime.portraitSprite`); rarity bg via `Resources.Load<Sprite>($"Rarities/{rarity}")`. Resolve `characterId → CharacterDataRuntime` through `CharacterDatabaseCSV.Instance` (confirm a by-id getter exists; else build a dict from `GetAllCharacters()`).

---

## 7. Entry points (navigation)

### 7.1 New screen

- Add `ScreenId.Leaderboard` to the enum in `Assets/Scripts/UI/ScreenManager.cs`.
- Add `[SerializeField] private GameObject _leaderboardScreen;` and toggle it in `ApplyScreen` (`screenId == ScreenId.Leaderboard`).
- **Bars:** include Leaderboard in `showBars` (the design shows the bottom nav). `HighlightScreen(ScreenId.Leaderboard)` will match no nav button → no highlight, which is correct (it's a header-icon destination, not a nav tab).
- Place the `RankingsScreen` prefab instance in `ShellScene` under the screens parent and wire `_leaderboardScreen`.
- **Back:** the controller remembers the invoking screen and returns to it (Home or HoleSelection). Wire the screen's existing close/`Handle` affordance (or add a standard header back) → `ScreenManager.ShowScreen(returnScreen)`.

### 7.2 Header icons (gold 1·2·3 podium badge)

Add the podium icon button to the **top-right of the header** on:
- **Hole Select** (`HoleSelectionScreenController` / its screen) — position per Figma node `12885-89895` (sits where the title banner ends, top-right).
- **Home Screen** (`HomeScreenController` / its screen) — same treatment.

Each button `onClick` → `ScreenManager.Instance.ShowScreen(ScreenId.Leaderboard)` and records the return screen. Use the icon asset from Figma node `12885-89938` (export if not already in the project).

> **NOTE:** If the icon sprite isn't in `Assets/Art/`, export node `12885-89938` from Figma and import it; otherwise reuse the existing sprite.

---

## 8. Files

**Add:**
- `Assets/Resources/Data/fake_players.csv`
- `Assets/Scripts/UI/Rankings/RankingsScreenController.cs`
- `Assets/Scripts/UI/Rankings/RankingsCardWidget.cs` (row binder; if a generic card binder doesn't already exist)
- `Assets/Scripts/UI/Rankings/ILeaderboardProvider.cs` + `LocalFakeLeaderboardProvider.cs` + `LeaderboardManager.cs`
- `Assets/Scripts/UI/Rankings/ITimeProvider.cs` + `NetworkTimeProvider.cs`
- (optional editor) `Assets/Scripts/Editor/FakePlayerRosterGenerator.cs`

**Modify:**
- `Assets/Scripts/Save/SaveData.cs` (accumulators + schemaVersion → 2)
- `Assets/Scripts/Save/SaveDataHost.cs` (only if explicit migration is required)
- `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` (`EarnPoints` accumulation hook)
- `Assets/Scripts/UI/ScreenManager.cs` (`ScreenId.Leaderboard` + container)
- `HomeScreenController` + `HoleSelectionScreenController` (header podium icon buttons)
- `ShellScene.unity` (place RankingsScreen instance + wire icons)

**Do NOT touch:** `MatchmakingModalController` (that's Phase 2), the ball sim, anything under `Physics/`.

---

## 9. Acceptance / gate

1. **Build clean**, EditMode tests green (add tests for: period-key rollover at UTC boundaries; tie ranking with skip; accumulator increments on `EarnPoints` but not `SpendPoints`).
2. **Leaderboard opens** from both Home and Hole Select header icons; back returns to the invoking screen.
3. All **four tabs** populate from the shared roster + player, sorted desc, with podium top-3, scrolling list, ties shown as `T#`, and the pinned player row correct.
4. **Switching tabs** changes scores (each period is distinct) and updates the reset countdown; Historic shows no countdown.
5. **Earning RP** (e.g. complete a hole) increases the player's Daily/Weekly/Monthly/Historic scores; **spending RP does not** lower them.
6. **Reset countdown** counts down to the correct UTC boundary; the time offset comes from the network fetch (note the chosen host in the report); offline fallback works (verify by simulating fetch failure).
7. **Banner present** matches Home; toggling banner off reflows the layout with no gaps/overlap.
8. **Visual gate:** full-size (1170×2532) bot/screenshot capture of the populated board on each tab + the two entry icons. Per project rule, record bot videos full-size.

---

## 10. Open implementer notes

- Confirm exact prefab transform paths before binding (§3 NOTE).
- Locate the existing rarity color helper; reuse it (§6 NOTE).
- Confirm `CharacterDatabaseCSV` by-id lookup (§6 NOTE).
- Confirm `SaveDataHost` schema-migration handling for v2 (§4.1 NOTE).
- Pick + flag the network-time host (§5.3 NOTE).
- Roster CSV `characterId` values must be valid roster ids (§4.3).
