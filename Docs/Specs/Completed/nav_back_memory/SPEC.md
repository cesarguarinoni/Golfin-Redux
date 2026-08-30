# SPEC — `nav_back_memory`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-30 by the Architect (Cowork) after a code sweep of every `ShowScreen` call site, every back/close/cancel control, and every `OnEnable` reset in `Assets/Scripts/UI/`.

## Goal

Two rules, applied everywhere in the shell:

1. **BACK returns to the screen you actually came from.** Today every back/close is a hard-coded target (`_backScreen`, `_returnTarget`, `ShowScreen(ScreenId.X)`), which is wrong whenever a screen has more than one way in.
2. **Re-entering a screen restores where you were** (last tab, last filter, last selected card) — and the nav bar follows the same rule: a nav slot reopens the **last screen you were on inside that pillar**, not always the pillar's root.

Decisions taken by Cesar (2026-08-30), not open for re-litigation by the implementer:

- **D1** Tapping the nav slot of the pillar you are already in → go to that pillar's **root** (iOS tab-bar convention). Tapping a *different* pillar's slot → its **last screen** (root the first time).
- **D2** In-game QUIT (`InGameSettingsModalController`) **keeps landing on Home**. Do not touch the gameplay exit paths.
- **D3** Compare mode (Roster / Clubs / Balls) is a transient action, not a place: **exit it when the screen is left**.

## Findings (current behaviour — what the sweep found)

| # | Where | What happens today | Verdict |
|---|---|---|---|
| F1 | `MissionSelectionScreenController.OnBackClicked` → `_openedFrom` | `OpenFrom()` is **never called** by any entry point (`ModeSelectScreenController` L241, `ModeCarouselController` L539, `DailyMissionPillController` L441 all call `ShowScreen(MissionSelection)` directly), so `_openedFrom` is always its default `Home`. BACK from Missions entered via Mode Select lands on Home. | **Bug** |
| F2 | `TournamentHoleSelectionScreenController.OpenLeaderboard` → `TournamentLeaderboard`, whose `Close()` goes to serialized `_backScreen = TournamentSelection` | Hole Selection → Leaderboard → CLOSE skips back over Hole Selection to Tournament Selection. | **Bug** |
| F3 | `GachaTabController.ApplyPendingOrDefaultTab` (OnEnable) | Rewards Center resets to the GACHA tab on **every** entry unless the one-shot `RequestStoreTab()` was set. Player on STORE → Inventory → Gacha slot → back on GACHA. | **Reset — fix** |
| F4 | `RankingsScreenController.OnEnable` sets `_activePeriod = Daily` | Leaderboard period tab resets every entry. | **Reset — fix** |
| F5 | `PersistentUIManager.NavigateTo` | Every slot is a fixed root: MainPlay → ModeSelection, Gacha → GeneralShop, Characters → Roster. On Hole Selection, tap Inventory, tap Play → Mode Select, not Hole Selection. | **Change (D1)** |
| F6 | `CompareController` / `ClubCompareController` / `BallCompareController` | `_isCompareMode` survives `OnDisable`; the enter/exit fade coroutines are killed mid-way when the screen is disabled. | **Change (D3)** |
| F7 | Android hardware/gesture back | No `Escape` handling anywhere in `Assets/Scripts` (legacy `Input` or `InputSystem`). The back gesture does nothing on Android. | **Gap — fix** |
| F8 | `RankingsScreenController.OpenFrom(returnScreen)` (`HomeScreenController` L169, `HoleSelectionScreenController` L76, `MissionSelectionScreenController` L114) | Works, but is the only screen with per-entry back tracking — a one-off pattern. Superseded by the history stack below; keep the method as a thin wrapper so callers don't change. | Fold in |
| F9 | `StaminaShopSelectionScreenController._returnTarget = Roster`, `StaminaShopDetailScreenController._backTarget = StaminaShopSelection`, `GachaHistoryScreenController.OnClose` → GeneralShop, `GachaPrizesScreenController.OnBack` → GeneralShop, `TournamentHoleSelectionScreenController._backScreen` | Correct today because each has exactly one way in. Route through the stack anyway with the serialized value as the **fallback**, so a second entry point later can't silently break them. | Fold in |
| F10 | `InventoryScreenController` | `ShowTab(0)` runs in `Start()` only → the active tab already persists across leave/return. `ClubFilterBar` selection and `GeneralShopScreenController._activeCategory` are plain fields → persist. `MissionSelectionScreenController` tier tab persists by design (documented at L70). `HoleSelectionScreenController` course/tee pills persist; the list re-centres on the NEXT hole by design. | **Already correct — acceptance-test only** |
| F11 | `ModeSelectScreenController`, `TournamentSelectionScreenController`, `StaminaShopSelectionScreenController` scroll → top on rebuild | Short lists; leave as is. | No change |
| F12 | `SettingsController.CloseSettings` collapses the accordion | Overlay, deliberate. | No change |
| F13 | Same-screen `ShowScreen` dedupe (`ScreenManager` L181) | Keeps a lit nav slot from re-fading its own screen. Keep. | No change |

## Architecture context

- **Asmdef:** everything touched is `Assembly-CSharp` (`ScreenManager`, `PersistentUIManager`, the screen controllers). No new asmdef.
- **Existing code referenced:**
  `Assets/Scripts/UI/ScreenManager.cs` (`ShowScreen`, `ApplyScreen`, `_currentScreen`, `ScreenChanged`, the `showBars` list),
  `Assets/Scripts/UI/PersistentUIManager.cs` (`enum Screen`, `NavigateTo`, `HighlightScreen`, `OnShopPlusButtonClick`),
  `Assets/Scripts/UI/Gacha/GachaTabController.cs` (`ApplyPendingOrDefaultTab`, `RequestStoreTab`, `_activeTab`, `ShowGachaTab/ShowStoreTab/ShowGiftsTab`),
  `Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs` (`ReturnToRewardsCenter`),
  `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` (`OpenFrom`, `_returnScreen`, `_activePeriod`, `UpdateTabIndicators`),
  `Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs` / `TournamentHoleSelectionScreenController.cs` (`Close`, `_backScreen`),
  `Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs` (`OnBackClicked`, `OpenFrom`, `_openedFrom`),
  `Assets/Scripts/UI/Shop/StaminaShop*ScreenController.cs` (`OnCancelClicked`),
  `Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs` (`OnClose`), `GachaPrizesScreenController.cs` (`OnBack`),
  `Assets/Scripts/UI/Roster/UI/CompareController.cs`, `Assets/Scripts/UI/Inventory/ClubCompareController.cs`, `BallCompareController.cs` (`_isCompareMode`, `ForceExitImmediate`),
  `Assets/Scripts/UI/SettingsController.cs` (`Instance`, `settingsPanel`, `CloseSettings`),
  `Assets/Scripts/UI/Modals/ModalController.cs` (`OpenModalCount`).
- **Not touched:** `GameplaySceneLoader.ExitToScreen` and its callers (D2), `AuthGate`, `DemoGate`, account screens, `LoadingScreenController`.

## Design

### 1. Pillar model (`ScreenManager`)

Add to `ScreenManager`:

```csharp
/// Which bottom-nav pillar a shell screen belongs to. null = not a pillar screen
/// (Logo/Splash/Loading/account/starter) — those never enter history or pillar memory.
public static PersistentUIManager.Screen? PillarOf(ScreenId id)
```

Move the `ScreenId → Screen` switch that lives inline in `PersistentUIManager.HighlightScreen` into this one static method and have `HighlightScreen` call it — one mapping, not two. Mapping is exactly today's:

| Pillar | Root | Members |
|---|---|---|
| Home | `Home` | `Home` |
| — (none) | — | `Leaderboard` — no slot today, opened from Home / HoleSelection / MissionSelection. `PillarOf` returns **null** for it; it rides the history stack and is never a pillar's remembered screen (see §2). |
| MainPlay | `ModeSelection` | `ModeSelection`, `HoleSelection`, `MissionSelection`, `TournamentSelection`, `TournamentHoleSelection`, `TournamentLeaderboard` |
| Characters | `Roster` | `Roster`, `StaminaShopSelection`, `StaminaShopDetail` |
| Inventory | `Inventory` | `Inventory` |
| Gacha | `GeneralShop` | `GeneralShop`, `GachaHistory`, `GachaPrizes` |

```csharp
public static ScreenId RootOf(PersistentUIManager.Screen pillar)
```

`Leaderboard` (`RankingsScreen`) is the one shell screen with no pillar: `PillarOf(Leaderboard)` returns null, so it is never written as a pillar's remembered screen, but it IS history-able (§2) — BACK from it pops to whichever screen opened it. `HighlightScreen` already returns early for Leaderboard (no slot lit), so nothing visible changes.

### 2. Pillar memory + history stack (`ScreenManager`)

```csharp
private readonly Dictionary<PersistentUIManager.Screen, ScreenId> _lastInPillar = new();
private readonly List<ScreenId> _history = new();   // oldest first; cap 16
```

- In `ApplyScreen(id)`, after `_currentScreen = id`: if `PillarOf(id)` is non-null, `_lastInPillar[pillar] = id`.
- `ShowScreen(id, instant)` — the existing public signature, still used by every forward navigation — becomes: `Navigate(id, instant, push: true)`.
- Internal `Navigate(id, instant, push)`:
  - Runs the existing DemoGate / AuthGate / dedupe / fade logic unchanged.
  - Define `IsShell(id)` = `PillarOf(id) != null || id == Leaderboard` (i.e. the `showBars` set). Define `SamePillar(a, b)` = true when either is `Leaderboard`, else `PillarOf(a) == PillarOf(b)`.
  - Before the swap: if `push` and `IsShell(_currentScreen)` and `IsShell(id)` and `SamePillar(_currentScreen, id)`, append `_currentScreen` to `_history` (cap 16, drop oldest).
  - Otherwise (leaving the shell: Loading, Login, …; or a pillar change) **clear `_history`**. Pillar switches are lateral, not forward; gameplay and auth are hard boundaries. Coming back from gameplay (`ExitToScreen` → `ShowScreen(target, instant:true)`) therefore starts with an empty stack and the fallbacks in §3 take over.
- Public:

```csharp
/// BACK. Pops the last same-pillar screen; when the stack is empty uses `fallback`
/// (the screen's serialized target); when that is null uses the pillar root; on a root, Home.
/// Returns false when there was nowhere to go (already on Home).
public bool GoBack(ScreenId? fallback = null, bool instant = false)

/// Nav-bar tap (D1). Same pillar as the current screen → RootOf(pillar).
/// Different pillar → _lastInPillar[pillar] if present, else RootOf(pillar). Never pushes; clears history.
public void NavigateToPillar(PersistentUIManager.Screen pillar)
```

`GoBack` pops entries that are no longer allowed (`DemoGate`/`AuthGate`) or equal to `_currentScreen` and keeps popping. It calls `Navigate(target, instant, push: false)`.

### 3. Route every back/close through `GoBack` (fallback = today's target)

| Screen | Control / method | Change |
|---|---|---|
| MissionSelection | `OnBackClicked()` | `GoBack(ScreenId.Home)`. Delete `_openedFrom` and `OpenFrom` (dead — F1). **NOTE:** the sweep found no scene/prefab reference to `OnBackClicked` under `Canvas/ScreensRoot/MissionSelectionScreen` (only `RankingsButton`, which `missions_rankings_button_removal` is removing). Confirm in the Editor which control is BACK on that screen (it may be a prefab-instance override the YAML sweep does not see); if it is not wired, wire it. Report what you found. |
| TournamentLeaderboard | `Close()` | `GoBack(_backScreen)` — fixes F2 (stack top is `TournamentHoleSelection` when opened from there; empty after a finished round → `_backScreen`). |
| TournamentHoleSelection | `Close()` | `GoBack(_backScreen)`. |
| Rankings (Leaderboard) | `_backButton` listener | `GoBack(_returnScreen)`. Keep `OpenFrom(returnScreen)` — it still sets `_returnScreen` and calls `ShowScreen`, so its three callers don't change. |
| StaminaShopSelection | `OnCancelClicked()` | `GoBack(_returnTarget)`. |
| StaminaShopDetail | `OnCancelClicked()` | `GoBack(_backTarget)`. |
| GachaHistory | `OnClose()` | `GoBack(ScreenId.GeneralShop)`. |
| GachaHistoryTabStrip | `ReturnToRewardsCenter(storeTab)` | keep `RequestStoreTab()` for the store case, add `GachaTabController.RequestGachaTab()` (new one-shot, symmetric) for the gacha case, then `GoBack(ScreenId.GeneralShop)`. Without the explicit request the remembered tab (§4) would win and "tap STORE on the history strip" could land on GACHA. |
| GachaPrizes | `OnBack()` | `GoBack(ScreenId.GeneralShop)`. |
| Everything in `ExitToScreen` callers, `LoginScreenController`, `SignUpScreenController`, `ResetPasswordScreenController`, `EmailConfirmationScreenController`, `CreateUsernameScreenController` | — | **No change** (D2; auth screens are outside the pillar model). |

Serialized `_backScreen` / `_returnTarget` / `_backTarget` fields stay (they are the fallbacks). No prefab or scene edits are needed for this section.

### 4. Nav bar (`PersistentUIManager`)

- `NavigateTo(Screen screen)`: replace the `switch` with `sm.NavigateToPillar(screen)` (keep the null-`sm` warning and the `currentScreen = screen; UpdateScreenHighlight();` lines; `HighlightScreen` re-runs from `ApplyScreen` anyway).
- `OnShopPlusButtonClick`: `GachaTabController.RequestStoreTab(); sm.NavigateToPillar(Screen.Gacha);` — the "+" is a jump to the Gacha pillar with STORE forced, not a forward push. **Poke:** per D1 this means tapping "+" while on `GachaPrizes` lands on the Rewards Center STORE tab (root + forced tab). That is the intended reading of "the + opens the store".
- `HighlightScreen`: use `ScreenManager.PillarOf(screenId)`; keep the `default: return;` behaviour for null.

### 5. Tab memory

- **`GachaTabController`** (F3): keep `_activeTab` as the remembered state. `ApplyPendingOrDefaultTab` becomes: pending store → STORE; pending gacha → GACHA; else **`_activeTab`** (first entry: GACHA, unchanged default — Cesar 2026-07-08). Add `public static void RequestGachaTab()` mirroring `RequestStoreTab()`; both one-shots are consumed in the same place. GIFTS stays disabled; a remembered GIFTS is impossible today, but guard it (fall back to GACHA) so re-enabling the tab later can't strand the screen.
- **`RankingsScreenController`** (F4): delete `_activePeriod = LeaderboardPeriod.Daily;` from `OnEnable`; call `UpdateTabIndicators()` after `RebuildList()` so the remembered tab is lit on re-entry. `Historic` is remembered like any other period.
- **No change** (already persist — F10): Inventory tab, `ClubFilterBar`, store category chip, Missions tier tab, Hole Selection pills. Cover them in acceptance tests so a future refactor can't regress them silently.

### 6. Compare mode exits on leave (D3 / F6)

In `CompareController`, `ClubCompareController`, `BallCompareController` add to `OnDisable()` (keep the existing unsubscribes):

```csharp
if (_isCompareMode) ForceExitImmediate();   // D3: compare is an action, not a place
```

`ForceExitImmediate()` already exists in all three (instant reset, no coroutines). The screen is disabled at that point, so nothing animates. Also stop any in-flight compare coroutines there if `ForceExitImmediate` doesn't already (`StopAllCoroutines()` is acceptable on these controllers — check nothing else long-lived runs on them; report if so).

### 7. Android back (F7)

`ScreenManager.Update()`:

```csharp
if (!BackPressedThisFrame()) return;
if (ModalController.OpenModalCount > 0) return;                 // modals own their dismissal
if (SettingsController.Instance != null && SettingsController.Instance.IsOpen) { SettingsController.Instance.CloseSettings(); return; }
if (!IsShell(_currentScreen)) return;                          // gameplay, auth, loading: not ours
GoBack();                                                       // on Home root: no-op, never quits
```

- Add `public bool IsOpen => settingsPanel != null && settingsPanel.activeSelf;` to `SettingsController`.
- `BackPressedThisFrame()`: `ProjectSettings.activeInputHandler = 1` (Input System package). Use `UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true` — Unity maps the Android back button to `Escape`. **NOTE:** `TapFeedbackController` and `MapViewController` still reference legacy `Input.*`; if the project actually runs with "Both", either path works — pick the one that compiles without a new `using` in a `#if`, and say which in the report.
- Never call `Application.Quit()`; never exit on Home. In-game the modal handles its own back (out of scope).

### 8. Strings

None. No player-facing text is added or changed.

## Out of scope

- Gameplay exit routing (`ExitToScreen` targets stay Home / TournamentHoleSelection / TournamentLeaderboard — D2).
- Persisting pillar memory across launches (session-only, in-memory).
- Scroll-position memory on long lists (Hole Selection's NEXT-hole centring and Missions' NEXT expansion are deliberate).
- Settings accordion memory (F12).
- Any prefab/scene layout change.

## Acceptance tests

EditMode where the seam allows (ScreenManager's pillar/history logic is pure C# over `ScreenId` — test it directly with a `ScreenManager` on a test GameObject and no FadeController so `ShowScreen` is instant); the rest are Editor play-mode checks. Per Cesar's standing rule, **no device pass is required** except A10 (Android back), which the Editor cannot exercise.

| # | Steps | Expected |
|---|---|---|
| A1 | Mode Select → Missions → BACK | Mode Select (was Home — F1). |
| A2 | Home mode carousel → Missions → BACK | Home. |
| A3 | Home daily pill → Missions → BACK | Home. |
| A4 | Tournaments → (entered) Hole Selection → LEADERBOARD → CLOSE | Tournament Hole Selection (was Tournament Selection — F2). Then CLOSE again → Tournament Selection. |
| A5 | Finish a tournament round (`TournamentRoundHandler` → `ExitToScreen(TournamentLeaderboard)`) → CLOSE | Tournament Selection (empty stack → `_backScreen`). |
| A6 | Rewards Center → STORE tab → Inventory slot → Gacha slot | Rewards Center on **STORE** (F3). Then tap the "+" from Roster → STORE; from Home tap Gacha slot after having been on GACHA last → GACHA. |
| A7 | Gacha History → tap STORE on the strip | Rewards Center on STORE. Tap GACHA on the strip → GACHA even if STORE was the remembered tab. |
| A8 | Leaderboard → WEEKLY → Home → Leaderboard | WEEKLY still lit and listed (F4). |
| A9 | Inventory → BALLS tab; Home; Inventory slot | BALLS (regression guard, F10). Same for a club filter, a store chip, a Missions tier tab, a Hole Selection tee pill. |
| A10 | Android build: Hole Selection → hardware back; Home → hardware back; with Settings open → back; with a modal open → back | Mode Select; nothing (app stays); Settings closes; modal unaffected. |
| A11 | Play slot → Hole Selection → Inventory slot → Play slot | Hole Selection (pillar memory). Then tap Play slot again → Mode Select (D1 root). |
| A12 | Roster → COMPARE two characters → Inventory slot → Characters slot | Roster in normal mode, selected character unchanged (D3). Same for Clubs and Balls compare. |
| A13 | Roster → BOOST → Stamina Shop → a shop → CANCEL → CANCEL | Stamina Shop Selection, then Roster with the same character selected. |
| A14 | Hole Selection → PLAY a hole → QUIT from in-game settings | Home (D2, unchanged). Then Play slot → Hole Selection (pillar memory survives gameplay). |
| A15 | EditMode: 20 forward pushes inside one pillar | `_history` never exceeds 16; `GoBack` still returns the most recent entries in order. |
| A16 | EditMode: history entry becomes disallowed (`AuthGate` false) | `GoBack` skips it and lands on the next valid entry / fallback. |

Report must quote: the BACK control found for Mission Selection (§3 NOTE), the input path chosen for §7, and `grep -rn "ShowScreen(_backScreen\|ShowScreen(_returnTarget\|ShowScreen(_backTarget\|ShowScreen(_returnScreen\|ShowScreen(_openedFrom)" Assets/Scripts` returning nothing.
