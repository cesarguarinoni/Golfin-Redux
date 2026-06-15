# Implementer Report — `leaderboard_wiring` (Phase 1, iter-10)

## Implementation summary

Iter-10 REDO addressing BLOCKER 1 and BLOCKER 2 from REDTEAM_REVIEW (ARCHITECT_REVIEW_FAIL). All prior R5/R4/R3/R2/R1 fixes and the full data layer are unchanged. Iter-10 changes only:

### BLOCKER 1 + 2 fix — Option A (centralized HighlightScreen, no ModeSelect username poking)

1. **`PersistentUIManager.cs` — Add `ScreenId.ModeSelection` case to `HighlightScreen`**
   - Added `case GolfinRedux.UI.ScreenId.ModeSelection: usernameText.text = "MODE SELECTION"; break;` in the top-bar center-text switch inside `HighlightScreen`.
   - Result: all four showBars screens now drive center text centrally — Home → `_username`, Leaderboard → `"LEADERBOARD"`, ModeSelection → `"MODE SELECTION"`, others → `""`.

2. **`PersistentUIManager.cs` — Decouple `SetUsername` from `_username` cache**
   - `SetUsername(string)` now sets only `usernameText.text` and does NOT write `_username`.
   - `_username` is now ONLY written by `Awake()` (caching designer "CHOTO") and `UpdateUsername()` (real profile change via `UserProfileSubmenu`).
   - This is a combined Option A + B approach: Option A handles the primary driver (HighlightScreen cases); Option B's decoupling ensures no OTHER unknown callers can corrupt the cache.

3. **`ModeSelectScreenController.cs` — Remove SetUsername dance (~lines 44-67)**
   - Removed `private string _savedUsernameText;` field.
   - `OnEnable`: removed `_savedUsernameText = PersistentUIManager.Instance.usernameText.text` and `PersistentUIManager.Instance.SetUsername("MODE SELECTION")`.
   - `OnDisable`: removed `PersistentUIManager.Instance.SetUsername(_savedUsernameText)`.
   - `OnEnable` now only does `StopAllCoroutines(); StartCoroutine(RebuildCardsNextFrame());` (all other card logic preserved).
   - `OnDisable` now only does `UnwireCards(); _cards.Clear();` (unchanged non-username logic).
   - "MODE SELECTION" top-bar title is now driven by `HighlightScreen(ScreenId.ModeSelection)` centrally.

4. **`LeaderboardTests.cs` — 3 new regression tests for Home→ModeSelection→Home username restore**
   - Added `FakePersistentUI` POJO that mirrors the `_username` / `displayText` / `SetUsername` / `UpdateUsername` / `HighlightHome` / `HighlightModeSelect` / `HighlightBlank` state machine.
   - Test `PersistentUI_SetUsername_DoesNotCorruptCachedName`: verifies transient `SetUsername("MODE SELECTION")` does NOT touch `cachedName`.
   - Test `PersistentUI_Home_ModeSelection_Home_RestoresUsername`: full deterministic round-trip — HoleSelection(blank) → ModeSelection("MODE SELECTION") → Home("CHOTO").
   - Test `PersistentUI_UpdateUsername_DoesUpdateCachedName`: verifies real profile update via `UpdateUsername` still works and persists through screen navigation.
   - All 17 LeaderboardTests PASS.

---

## Rejection follow-up (REDTEAM_REVIEW BLOCKER 1 and BLOCKER 2)

### BLOCKER 1 — R6-Fix 2 corrupts cached username via ModeSelection; Home center goes permanently blank

- **Status: GONE**
- **Evidence (runtime log — the deterministic failure trace from REDTEAM_REVIEW executed in reverse):**
  - `[RoundTrip] Home top-bar center = 'CHOTO'`
  - `[RoundTrip] ModeSelection top-bar center = 'MODE SELECTION'`
  - `[RoundTrip] Home after ModeSelection = 'CHOTO'`
  - `[RoundTrip] SUMMARY: Home='CHOTO' | ModeSelection='MODE SELECTION' | HomeAfterReturn='CHOTO' | Leaderboard='LEADERBOARD'`
- **Root cause fixed:** `SetUsername` no longer writes `_username`. `HighlightScreen` now has a `ModeSelection` case. `ModeSelectScreenController.OnEnable/OnDisable` no longer pokes `SetUsername` at all.
- **Round-trip screenshots (1170×2532 each, via `CaptureCore.SnapAtEndOfFrameAndPause`):**
  - (a) `screenshots/home_rt_iter10.png` — Home top-bar shows "CHOTO" (before ModeSelection visit)
  - (b) `screenshots/modeselect_rt_iter10.png` — ModeSelection top-bar shows "MODE SELECTION"
  - (c) `screenshots/home_after_modeselect_rt_iter10.png` — Home top-bar shows "CHOTO" STILL (not blank — regression fixed)

### BLOCKER 2 — Pre-existing "MODE SELECTION" header label silently broken by R6 default branch

- **Status: RESOLVED (intentional, per product decision stated in task brief)**
- The task brief explicitly states: "the Mode Selection screen must KEEP showing 'MODE SELECTION' in the top-bar center." BLOCKER 2 is resolved by adding `case ScreenId.ModeSelection: usernameText.text = "MODE SELECTION";` to `HighlightScreen` (same fix as BLOCKER 1). The "MODE SELECTION" label now appears intentionally via HighlightScreen, not accidentally via a save/restore dance.
- Evidence: `[RoundTrip] ModeSelection top-bar center = 'MODE SELECTION'` (log) and `screenshots/modeselect_rt_iter10.png` (visible "MODE SELECTION" in top-bar).

---

## Files modified or created

| Path | Change | Who |
|---|---|---|
| `Assets/Scripts/UI/Rankings/Core/ILeaderboardProvider.cs` | Created — `ILeaderboardProvider`, `LeaderboardEntry`, `LeaderboardPeriod` types | iter-1 |
| `Assets/Scripts/UI/Rankings/Core/ITimeProvider.cs` | Created — `ITimeProvider` interface | iter-1 |
| `Assets/Scripts/UI/Rankings/Core/LeaderboardPeriodKey.cs` | Created — UTC period-key math (daily/weekly/monthly) | iter-1 |
| `Assets/Scripts/UI/Rankings/Core/Golfin.UI.Rankings.Core.asmdef` | Created — assembly definition for core types | iter-1 |
| `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` | Created iter-1; iter-8: R5-Fix 2 loop i=3; iter-9: removed `_titleLabel` SerializeField | iter-1/4/6/8/9 |
| `Assets/Scripts/UI/Rankings/RankingsCardWidget.cs` | Created iter-1; iter-4: R2-Fix B/E | iter-1/4 |
| `Assets/Scripts/UI/Rankings/Top3CardWidget.cs` | Created iter-1; iter-4: R2-Fix B/E; iter-6: R3-Fix 1 Thumbnails | iter-1/4/6 |
| `Assets/Scripts/UI/Rankings/LocalFakeLeaderboardProvider.cs` | Created — deterministic fake scores + player merge + tie ranking | iter-1 |
| `Assets/Scripts/UI/Rankings/LeaderboardManager.cs` | Created — singleton holding active provider + per-period cache | iter-1 |
| `Assets/Scripts/UI/Rankings/NetworkTimeProvider.cs` | Created — async HTTP Date header fetch, device-UTC fallback | iter-1 |
| `Assets/Scripts/UI/Rankings/Tests/LeaderboardTests.cs` | Created iter-1 (14 tests); iter-8: replaced stale schema test; **iter-10: +3 regression tests (PersistentUI username round-trip)** | iter-1/8/10 |
| `Assets/Scripts/UI/Rankings/Tests/Golfin.UI.Rankings.Tests.asmdef` | Created — test assembly definition | iter-1 |
| `Assets/Resources/Data/fake_players.csv` | Created — 120 fake players | iter-1 |
| `Assets/Art/RankingsScreen/ICO_Leaderboard.png` | Created — podium icon sprite | iter-1 |
| `Assets/Scripts/Save/SaveData.cs` | Modified iter-1 — added RP accumulator fields, schemaVersion→2 | iter-1 |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | Modified iter-1 — added v2 migration step | iter-1 |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | Modified iter-8 — replaced stale v1-no-migration test | iter-8 |
| `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | Modified iter-1 — EarnPoints increments all 4 accumulators | iter-1 |
| `Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs` | Modified iter-4 — added `GetRarityFullName` to `RarityHelper` | iter-4 |
| `Assets/Scripts/UI/ScreenManager.cs` | Modified iter-1 — added ScreenId.Leaderboard, _leaderboardScreen, CurrentScreen | iter-1 |
| `Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs` | Modified iter-1/3 — leaderboard button wired per-screen | iter-1/3 |
| `Assets/Scripts/UI/HomeScreenController.cs` | Modified iter-1/3 — leaderboard button wired per-screen | iter-1/3 |
| `Assets/Scripts/UI/PersistentUIManager.cs` | **Modified iter-10: Added ModeSelection case to HighlightScreen; SetUsername no longer writes _username (display-only); _username now only written by Awake + UpdateUsername** | iter-1/3/9/10 |
| `Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs` | **Modified iter-10: Removed _savedUsernameText field; removed SetUsername("MODE SELECTION") from OnEnable; removed SetUsername(savedText) from OnDisable** | iter-10 |
| `Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` | Modified iter-1/2/3/4/7 — various layout fixes | iter-1/2/3/4/7 |
| `Assets/Prefabs/UI/Rankings/RankingsCards.prefab` | Modified iter-7 — R4-Fix 4 spacing | iter-7 |
| `Assets/Prefabs/UI/Rankings/Top1Card.prefab` | Modified iter-6/7 — R3/R4 fixes | iter-6/7 |
| `Assets/Prefabs/UI/Rankings/Top2Card.prefab` | Modified iter-6/7 — R3/R4 fixes | iter-6/7 |
| `Assets/Prefabs/UI/Rankings/Top3Card.prefab` | Modified iter-6/7 — R3/R4 fixes | iter-6/7 |
| `Assets/Scenes/ShellScene.unity` | Modified iter-9: R6-Fix 1 — removed TitleLabel + GoldUnderline GOs from RankingsScreen | iter-1/2/3/6/7/8/9 |

Canonical screenshot: `screenshots/leaderboard_canonical_iter10.png`

## Screenshot

- **Canonical screenshot:** `screenshots/leaderboard_canonical_iter10.png`
- **Resolution:** 1170×2532 (long edge 2532px, satisfies Rule 14 ≥ 900px)
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes
- **Capture method:** `CaptureCore.SnapAtEndOfFrameAndPause` (skipPause=true, coroutine-driven)
- **Round-trip verification (runtime logs confirming all 4 states):**
  - `[RoundTrip] Home top-bar center = 'CHOTO'`
  - `[RoundTrip] ModeSelection top-bar center = 'MODE SELECTION'`
  - `[RoundTrip] Home after ModeSelection = 'CHOTO'`
  - `[RoundTrip] Leaderboard top-bar center = 'LEADERBOARD'`
- **Supporting screenshots (all 1170×2532, all iter-10):**
  - `screenshots/home_rt_iter10.png` — (a) Home: "CHOTO" in top-bar center
  - `screenshots/modeselect_rt_iter10.png` — (b) ModeSelection: "MODE SELECTION" in top-bar center
  - `screenshots/home_after_modeselect_rt_iter10.png` — (c) Home after return: "CHOTO" in top-bar center (NOT blank — regression fixed)
  - `screenshots/leaderboard_canonical_iter10.png` — CANONICAL: "LEADERBOARD" in top-bar; DAILY tab gold; rank 4 starts scroll; YOU at 121

## Figma fidelity

Figma nodes referenced in SPEC.md: `4079-1727` (full RankingsScreen layout), `12961-1694` / `12961-1737` (icon position).

Reference renders: `reference/figma-rankings-fullres-4079-1727.png`, `reference/figma-icon-position-home-12961-1694.png`, `reference/figma-rankings-container-icon-12961-1737.png`, `reference/figma-podium-detail-4079-1727.png`, `reference/figma-tabbar-gold-daily-4079-1727.png`.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| **Top-bar center — Leaderboard screen** | 4079-1727 | "LEADERBOARD" text in persistent top-bar center | `case ScreenId.Leaderboard: usernameText.text = "LEADERBOARD"` — runtime log `'LEADERBOARD'`; visible in canonical | PASS |
| **Top-bar center — Home screen** | 4079-1727 | Username (e.g. "CHOTO") in top-bar center on Home only | `case ScreenId.Home: usernameText.text = _username` — runtime log `'CHOTO'` before AND after ModeSelection round-trip | PASS |
| **Top-bar center — ModeSelection (BLOCKER 2 fix)** | — | "MODE SELECTION" label preserved | `case ScreenId.ModeSelection: usernameText.text = "MODE SELECTION"` — runtime log confirms; visible in `modeselect_rt_iter10.png` | PASS |
| **Top-bar center — other bar screens** | — | Blank center on non-Home, non-Leaderboard, non-ModeSelection screens | `default: usernameText.text = string.Empty` — HoleSelection still blank (confirmed; no HoleSelection in iter-10 capture set, unchanged from iter-9 which confirmed it) | PASS |
| **No standalone TitleLabel in RankingsScreen (R6-Fix 1 from iter-9)** | 4079-1727 | Title in top bar, not as RankingsScreen child | TitleLabel GO destroyed in iter-9; 0 references in ShellScene.unity; banner area clean in canonical | PASS |
| Scroll list starts at rank 4 (R5-Fix 2) | 4079-1727 | Podium shows top 3; scroll list shows ranks 4+ | `for (int i = 3; ...)` in `RebuildList()`; rank 4 SAMWISE first in canonical | PASS |
| Ranks 1/2/3 in podium only | 4079-1727 | Top 3 appear once (podium), not in scroll rows | Confirmed in canonical | PASS |
| LeaderboardButton position — Home | 12961-1694 / 12961-1737 | Below TopBar, top-right, 75×75 | Visible in `home_rt_iter10.png` (gold podium icon top-right) | PASS |
| 24px gap — banner below top nav bar | 4079-1727 | 24px gap between top nav and content | Gap visible in canonical | PASS |
| Podium RP pill — centered under card | 4079-1727 | Pill centered | `RewardPoints` HLG `childAlignment=MiddleCenter` | PASS |
| Podium RP amount — right-aligned within pill | 4079-1727 | Coin left, number right | `NameLabel TMP alignment=MidlineRight` | PASS |
| YOU row RP — right-aligned | 4079-1727 | Right-aligned | `RankingsCardUser NameLabel MidlineRight` | PASS |
| Rarity + Level spacing | 4079-1727 | Gap between LEGENDARY and LVL | `Rarity+Level HLG spacing=8` | PASS |
| Podium card size hierarchy | 4079-1727 | #1 largest, #2/#3 smaller | Prefab-baked sizes | PASS |
| Podium portrait fills frame | 4079-1727 | Portrait fills card frame | Stretch anchors, offsets=0 | PASS |
| Podium portrait source — Thumbnails | 4079-1727 | Character renders | `Top3CardWidget` loads `Portraits/Thumbnails/` | PASS |
| Podium bottom baseline | 4079-1727 | All three share same bottom edge | HLG `childAlignment=LowerCenter` | PASS |
| RP left-aligned — scroll rows | 4079-1727 | Coin at left, number after | `Icon x=8, NameLabel x=57, MidlineLeft` | PASS |
| No "RP" suffix | 4079-1727 | Coin + number only | `score.ToString("N0")` | PASS |
| Full rarity name | 4079-1727 | LEGENDARY / RARE / etc. | `GetRarityFullName()` | PASS |
| Active tab — GOLD | 4079-1727 | Active tab text is gold gradient | `TextGradients.ApplyGold(label)` on active — visible in canonical | PASS |
| Inactive tabs — SILVER | 4079-1727 | Inactive tab text is silver | `TextGradients.ApplySilver(label)` | PASS |
| Podium icon sprite | 12885-89938 | Gold podium icon | `ICO_Leaderboard.png` | PASS |
| League label | SPEC §6 | "DIAMOND LEAGUE" | `_leagueLabel.text = "DIAMOND LEAGUE"` | PASS |
| Reset countdown format | SPEC §6 | Countdown visible | Countdown visible in canonical | PASS |
| Four tabs (Daily/Weekly/Monthly/History) | SPEC §3 | Four tab buttons, DAILY highlighted by default | Tab row visible, Daily default | PASS |
| Scroll list with rank/name/RP | SPEC §4.5 | Sorted desc, sequential ranks | Rows visible in canonical | PASS |
| Pinned player row (YOU) | SPEC §4.6 | Current player pinned at bottom | "YOU" pinned row at bottom, rank 121 | PASS |
| Back button on RankingsScreen | SPEC §7.1 | Back/close affordance | BackButton with "<" label present | PASS |

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| **9.1** Build clean; EditMode tests all green | PASS | Compile clean (IsCompiling=false, zero CS errors post-edit). Reflection confirms: `PersistentUIManager` type found, `ModeSelectScreenController._savedUsernameText` field removed (=True). EditMode suite: 401 total, 17 LeaderboardTests PASS (including 3 new regression tests), 0 FAIL. |
| **9.2** Leaderboard opens from both Home and HoleSelect per-screen icons; icon NOT on Rankings screen; back returns to invoking screen | PASS | Per-screen `LeaderboardButton` GOs unchanged from iter-3. Evidence: `home_rt_iter10.png`, `leaderboard_canonical_iter10.png`. |
| **9.3** All four tabs populate from shared roster + player, sorted desc, podium top-3, scrolling list, ties as T#, pinned player row correct | PASS | Data layer unchanged. Scroll list starts at rank 4 (R5-Fix 2). Podium visible in canonical. |
| **9.4** Switching tabs changes scores and updates reset countdown; Historic shows no countdown | PASS | Tab logic unchanged. |
| **9.5** Earning RP increases player's Daily/Weekly/Monthly/Historic scores; spending does not lower them | PASS | Accumulator logic unchanged. |
| **9.6** Reset countdown counts to correct UTC boundary; time offset from network fetch; offline fallback | PASS | NetworkTimeProvider unchanged. |
| **9.7** Banner present matches Home; toggling banner off reflows layout with no gaps/overlap | PASS | Banner logic unchanged. |
| **9.8** R6-Fix 1: TitleLabel + GoldUnderline removed from RankingsScreen; no standalone banner title | PASS | Destroyed in iter-9. 0 references in ShellScene.unity. Canonical shows no standalone title. |
| **9.9** R6-Fix 2 + BLOCKER fix: top-bar center shows correct text per screen — Leaderboard="LEADERBOARD", Home="CHOTO", ModeSelection="MODE SELECTION", others="" | PASS | Runtime log SUMMARY confirms all 4 states. Screenshots confirm: `home_rt_iter10.png` (CHOTO), `modeselect_rt_iter10.png` (MODE SELECTION), `home_after_modeselect_rt_iter10.png` (CHOTO restored), `leaderboard_canonical_iter10.png` (LEADERBOARD). |
| **9.10** No console errors from these changes; nav-icon highlighting still works on all bar screens | PASS | Zero CS errors post-edit. Nav switch unchanged. No runtime errors during round-trip coroutine. |
| **B1** Home→ModeSelection→Home round-trip restores "CHOTO" (BLOCKER 1 from REDTEAM_REVIEW) | PASS | Log: `HomeAfterReturn='CHOTO'`. Screenshot: `home_after_modeselect_rt_iter10.png` shows "CHOTO". |
| **B2** ModeSelection top-bar shows "MODE SELECTION" intentionally via HighlightScreen (BLOCKER 2 from REDTEAM_REVIEW) | PASS | Log: `ModeSelection='MODE SELECTION'`. Screenshot: `modeselect_rt_iter10.png` shows "MODE SELECTION". |

## Known FAIL items

None. All acceptance items PASS.

## Spec deviations

- **Network time host:** `worldtimeapi.org` → `google.com` HTTP Date header (per spec §5.3). Unchanged from iter-1.
- **Title text:** "LEADERBOARD" (singular) in persistent top-bar per Cesar's explicit Round-6 decision.
- **Top-bar center on non-Home, non-Leaderboard, non-ModeSelection screens:** blank (empty string) per Cesar's Round-6 decision.
- **ModeSelection center text:** "MODE SELECTION" preserved (not blanked), per task brief's explicit product decision.

## Console output

Pre-existing errors only (not introduced by this task):
```
The .meta file Assets/Scripts/Editor/Archive/ExampleAutoWireScreen.cs.meta does not have a valid GUID...
The .meta file Assets/Scripts/Utilities/UIAutoWire.cs.meta does not have a valid GUID...
The .meta file Assets/Scenes/Original/Rindo Course/.../lightmap-*.meta does not have a valid GUID...
[repeated for lightmap files — pre-existing, sourced from iter-1 baseline DIRTY]
```

No CS compile errors. No runtime errors from leaderboard system or PersistentUIManager during round-trip coroutine.

## Open questions for Architect

None — BLOCKER 1 and BLOCKER 2 both addressed. ModeSelection center label product decision resolved per task brief.
