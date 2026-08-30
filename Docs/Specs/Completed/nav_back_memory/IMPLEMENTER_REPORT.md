# Implementer Report — `nav_back_memory`

## Implementation summary

`ScreenManager` grew a pillar model (`PillarOf` / `RootOf` / `IsShell`), a per-pillar
"last screen" dictionary, and a 16-deep same-pillar history stack. `ShowScreen` is now a thin
wrapper over a private `Navigate(id, instant, push)` that does the history bookkeeping before
the swap; two new public entry points — `GoBack(fallback)` and `NavigateToPillar(pillar)` —
serve BACK and the nav bar. Every back/close in the shell now calls `GoBack` with its existing
serialized target as the fallback, the nav bar routes through `NavigateToPillar` (D1), the
Rewards Center and the Leaderboard remember their tab for the session, compare mode exits when
its screen is left, and `ScreenManager.Update()` handles the Android back key.

No prefab or scene edits, no new strings, no new asmdef. `ScreenId`, every serialized
`_backScreen` / `_returnTarget` / `_backTarget` field, and every `ExitToScreen` caller are
untouched.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/UI/ScreenManager.cs` | modified (+220/−1) — pillar model, history stack, per-pillar memory, `GoBack`, `NavigateToPillar`, Android-back `Update()`. |
| `Assets/Scripts/UI/PersistentUIManager.cs` | modified (+23/−49) — `NavigateTo` delegates to `NavigateToPillar`; `HighlightScreen`'s ScreenId→Screen switch replaced by `ScreenManager.PillarOf`; "+" is a pillar jump with STORE forced. |
| `Assets/Scripts/UI/SettingsController.cs` | modified (+7) — `public bool IsOpen` for the back handler. |
| `Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs` | modified (+10/−10) — `OnBackClicked` → `GoBack(Home)`; dead `_openedFrom`/`OpenFrom` deleted (F1). |
| `Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs` | modified (+4/−1) — `Close()` → `GoBack(_backScreen)` (F2). |
| `Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs` | modified (+2/−1) — `Close()` → `GoBack(_backScreen)`. |
| `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` | modified (+9/−3) — back → `GoBack(_returnScreen)`; `_activePeriod` no longer reset in `OnEnable`; `UpdateTabIndicators()` after `RebuildList()` (F4). |
| `Assets/Scripts/UI/Shop/StaminaShopSelectionScreenController.cs` | modified (+2/−1) — `OnCancelClicked` → `GoBack(_returnTarget)`. |
| `Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs` | modified (+2/−1) — `OnCancelClicked` → `GoBack(_backTarget)`. |
| `Assets/Scripts/UI/Gacha/GachaTabController.cs` | modified (+25/−7) — remembers `_activeTab` across entries; adds `RequestGachaTab()`; GIFTS guarded (F3). |
| `Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs` | modified (+2/−1) — `OnClose` → `GoBack(GeneralShop)`. |
| `Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs` | modified (+2/−1) — `OnBack` → `GoBack(GeneralShop)`. |
| `Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs` | modified (+5/−3) — GACHA chip now calls `RequestGachaTab()`; leaves via `GoBack(GeneralShop)`. |
| `Assets/Scripts/UI/Roster/UI/CompareController.cs` | modified (+5) — `OnDisable` exits compare mode (D3/F6). |
| `Assets/Scripts/UI/Inventory/ClubCompareController.cs` | modified (+3) — same. |
| `Assets/Scripts/UI/Inventory/BallCompareController.cs` | modified (+3) — same. |
| `Assets/Tests/EditMode/NavBackMemoryTests.cs` | **created** — 18 EditMode tests over the pillar model, history stack, `GoBack` fallback chain and D1 nav behaviour (A15, A16). |
| `Docs/Specs/Active/nav_back_memory/evidence/*` | **created** — the two acceptance JSONs + the compare/selection diagnostic (`Docs/Diagnostics/_capture/` is gitignored). |

Pre-existing dirty paths outside this task's folder, present at kickoff and NOT touched here:
`Docs/Reports/content_art.txt`, `Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt`
(all three appear in the session-start `git status`; `git diff` on them is unrelated to nav).

## Screenshot

Not applicable — this task has no visual surface. It changes navigation routing only; no
layout, sprite, font or colour is touched, and the SPEC references no Figma node. The gate is
the deterministic acceptance JSON below (SPEC § Acceptance tests), not a rendered frame.

- **Acceptance JSON (play mode, real widgets):** `evidence/nav_back_memory_acceptance.json` — 50/50 rows PASS
- **Back-key JSON (play mode):** `evidence/nav_back_memory_backkey.json` — 10/10 rows PASS
- **Compare / selection diagnostic:** `evidence/nav_diag2.txt`
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes (both JSONs written from a running play session; editor left stopped and the scene not dirty)

## The two things the SPEC asked me to quote

### 1. The Mission Selection BACK control (SPEC §3 NOTE)

**There is none.** The YAML sweep and a live hierarchy dump of the running scene agree, and
nothing on that screen is a prefab instance, so there is no override the sweep could miss.
The complete set of `Button`s under `Canvas/ScreensRoot/MissionSelectionScreen` is:

```
### MissionSelectionScreen
   Content/Filters/FilterRow1/Tab_COURSE
   Content/Filters/FilterRow1/Pill_YAITA___KIKYOU
   Content/Filters/FilterRow2/Tab_BEGINNER
   Content/Filters/FilterRow2/Tab_AMATEUR
   Content/Filters/FilterRow2/Tab_PRO
   Content/Filters/FilterRow2/Tab_LEGEND
   Content/DailyMissionCard/ExpandedContainer/ActionButton
   Content/DailyMissionCard/CardTapButton
   RankingsButton            <- the one `missions_rankings_button_removal` is removing
```

`MissionSelectionScreenController.OnBackClicked` is `public` and has **zero** call sites in
code or scene YAML (`grep -rn "OnBackClicked" Assets` → the declaration only; no
`m_MethodName: OnBackClicked` anywhere). The screen's only exits today are the bottom nav bar
and — new in this change — the Android back key.

I did **not** wire a new BACK button: creating one is a scene/layout change, which this task's
brief ("no prefab or scene edits") and the SPEC's own § Out of scope ("Any prefab/scene layout
change") both exclude, and it would need a position + sprite decision that belongs in Figma.
`OnBackClicked` is now correct (`GoBack(ScreenId.Home)`) and is what A1–A3 exercised. Surfaced
in § Open questions.

### 2. The input path chosen for §7

`UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame`, fully qualified so
no new `using` is added:

```csharp
private static bool BackPressedThisFrame()
{
    var keyboard = UnityEngine.InputSystem.Keyboard.current;
    return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
}
```

The SPEC left this conditional on whether the project runs with "Both". It does not:
`ProjectSettings/ProjectSettings.asset` line 964 reads `activeInputHandler: 1` — Input System
package **only**, so the legacy `UnityEngine.Input` path would throw at runtime and was never
an option. (`TapFeedbackController` in fact already imports `UnityEngine.InputSystem`.)

### 3. The SPEC's mandated grep

```
$ grep -rn "ShowScreen(_backScreen\|ShowScreen(_returnTarget\|ShowScreen(_backTarget\|ShowScreen(_returnScreen\|ShowScreen(_openedFrom)" Assets/Scripts
(no output)
```

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| A1 Mode Select → Missions → BACK ⇒ Mode Select | PASS | Play mode, real widgets: nav PLAY slot → `ModeSelection`, Missions card `ActionButton.onClick` → `MissionSelection`, `OnBackClicked()` → `ModeSelection` (was `Home` before — F1). Rows 1–3 of `nav_back_memory_acceptance.json`. |
| A2 Home carousel → Missions → BACK ⇒ Home | PASS | Home `ModeHomeCard/PlayButton.onClick` → `MissionSelection`; BACK → `Home` (empty stack → the `Home` fallback). |
| A3 Home daily pill → Missions → BACK ⇒ Home | PASS | `DailyMissionPill.onClick` → `MissionSelection`; BACK → `Home`. |
| A4 Tournaments → Hole Selection → LEADERBOARD → CLOSE ⇒ Hole Selection, then Tournament Selection | PASS | `TournamentCloseButton.onClick` #1 → `TournamentHoleSelection` (was `TournamentSelection` — F2 fixed), #2 → `TournamentSelection`. |
| A5 Finished round → CLOSE ⇒ the serialized `_backScreen` | PASS (spec text differs — see below) | With the stack emptied by the `Loading` boundary, CLOSE used the serialized fallback and landed on `Leaderboard`. The SPEC predicted `TournamentSelection`, but `ShellScene.unity` serializes `TournamentLeaderboardScreenController._backScreen: 9` (= `Leaderboard`) **at HEAD** — pre-existing, and identical before this change (`ShowScreen(_backScreen)` went there too). The mechanism under test — empty stack ⇒ serialized fallback — is verified. See § Open questions. |
| A6 Rewards Center tab memory | PASS | STORE tapped → Inventory slot → Gacha slot ⇒ `GeneralShop` on **STORE** (`_activeTab == Store`, F3 fixed). Top-bar "+" from Roster ⇒ STORE forced. After GACHA + a Home round trip, the Gacha slot ⇒ GACHA. 7 rows. |
| A7 Gacha History strip | PASS | History chip → `GachaHistory`; STORE chip → `GeneralShop` on STORE; GACHA chip → `GeneralShop` on **GACHA** even though STORE was the remembered tab (the new `RequestGachaTab()`). 6 rows. |
| A8 Leaderboard period memory | PASS | WEEKLY tapped (`_activePeriod == Weekly`) → BACK → `Home` → re-enter ⇒ still `Weekly` and lit (F4; `UpdateTabIndicators()` now runs even when `RebuildList()` early-returns on an empty ranking). |
| A9 Inventory / filter / chip / tier / pill persistence | PASS | `InventoryScreenController._activeTab` = 2 (BALLS) before Home and 2 after re-entry. Regression guard only — no code touched. |
| A10 Android hardware back | PASS in the Editor / device pass outstanding | All four cases green through the exact seam Android uses (`Keyboard.escapeKey.wasPressedThisFrame`): Hole Selection → back ⇒ `ModeSelection`; Home → back ⇒ stays `Home`, app still running; Settings open → back ⇒ `IsOpen` false, screen unchanged; modal open → back ⇒ screen unchanged and the modal still open. `nav_back_memory_backkey.json`, 10/10. **Only Unity's platform mapping of the Android hardware/gesture back onto Escape is unverified** — the Editor cannot exercise it. |
| A11 Play slot ⇒ last screen, then root | PASS | Practice card → `HoleSelection`; Inventory slot; PLAY slot ⇒ `HoleSelection` (pillar memory); PLAY slot again ⇒ `ModeSelection` (D1 root). |
| A12 Compare exits on leave, selection unchanged | PASS | Roster: compare entered (`IsCompareMode == True`) → Inventory slot → Characters slot ⇒ `Roster`, `IsCompareMode == False`, selected character `char_james` → `char_james`. Clubs and Balls both re-verified the same way (`evidence/nav_diag2.txt`). |
| A13 Roster → BOOST → shop → CANCEL ×2 | PASS | `BoostButton` → `StaminaShopSelection`; a shop card → `StaminaShopDetail`; `StaminaShopCancelButton` → `StaminaShopSelection`; cancel → `Roster` with the same character selected. |
| A14 QUIT ⇒ Home; pillar memory survives gameplay | PASS | `HoleSelection` → `Loading` → `Home` (the `ExitToScreen(Home)` shape; those call sites are untouched per D2) ⇒ PLAY slot then reopens `HoleSelection`. Also covered in EditMode by `PillarMemory_SurvivesLeavingTheShell`. |
| A15 20 pushes inside one pillar; cap 16; newest-first pops | PASS | EditMode `A15_HistoryCapsAt16_AndPopsNewestFirst` — asserts `_history.Count <= 16` after every one of the 20 pushes, that the 16 survivors are the last 16 in order, and walks `GoBack` back through all 16. |
| A16 `GoBack` skips an unusable entry | PASS with a stated limit | EditMode `A16_GoBack_SkipsUnusableEntries_AndLandsOnTheNextValidOne` drives the skip-and-continue loop and lands on the next valid entry. The **gate-blocked** branch of that same loop is not exercisable in EditMode: `DemoGate.IsDemo` is a compile-time `const false` and `AuthGate.HasSession` short-circuits `true` whenever `!Application.isPlaying`, so no `ScreenId` can be made disallowed there. Both gate calls sit in the same `while` loop as the branch that is covered. |

## Known FAIL items

None. A5's literal expected value differs from the SPEC, but the behaviour is correct for the
value the scene actually holds — see the row and § Open questions.

## Spec deviations

- **§3, Mission Selection "if it is not wired, wire it".** Not done: no BACK control exists on
  that screen at all, so "wiring" would mean authoring a new button — a scene/layout change
  the same SPEC puts out of scope and this task's brief forbids. Reported instead (above).
- **A5 expected value.** Reported against the serialized `_backScreen` (`Leaderboard`) rather
  than the SPEC's assumed `TournamentSelection`; the difference is a pre-existing scene value,
  not behaviour introduced here.

## Console output

No exceptions and no `[AuthGate]` / `[DemoGate]` warnings in the 4000 log lines covering the
acceptance run:

```
$ awk 'NR>=314749 && NR<=318749' ~/Library/Logs/Unity/Editor.log | grep -E "Exception|\[Error\]|NullReference"
(no output)
```

The only `error CS` lines in the session log are from two throwaway `script-execute` drafts of
the acceptance driver itself (`The name 'CharacterManager' does not exist in the current
context` — the class is `Golfin.Roster.CharacterManager`, not global as CLAUDE.md states);
both were fixed before the recorded run. Production code compiled clean:
`assets-refresh` → `[Success] Assets refresh completed: AssetDatabase`.

EditMode suite: **2081 tests, 2078 passed, 0 failed, 3 skipped** (the 3 skips are the
pre-existing `HoleCompleteDriverTests` Stage-C1 skips). The new suite was proven to actually
run with a tripwire — a deliberately wrong assertion produced
`FAILED: GolfinRedux.Tests.EditMode.NavBackMemoryTests.RootOf_MatchesTheSpecTable`, then was
reverted and the suite re-run green (`tests-run` ignores class/assembly filters, so a green
total alone is not evidence the new file ran).

## Open questions for Architect

1. **Mission Selection has no BACK control.** Quoted in full above. Should one be authored
   (Figma node + position), or is the nav bar + Android back the intended exit? Note that
   `missions_rankings_button_removal` is removing the only other top-right control there.
2. **`TournamentLeaderboardScreenController._backScreen` is serialized to `Leaderboard`, not
   `TournamentSelection`** (`ShellScene.unity` `_backScreen: 9`, pre-existing at HEAD). So a
   CLOSE off a finished round lands on the generic Rankings screen. The SPEC's A5 assumed
   `TournamentSelection`. One-value scene fix if that is a bug — out of scope here.
   Related: `TournamentHoleSelectionScreenController._backScreen` is `ModeSelection`
   (`_backScreen: 7`), also not the C# default `TournamentSelection`. Both are now only
   fallbacks, so they are invisible whenever the player arrived through a real path.
3. **`RankingsScreen/BackButton` is inactive in the scene** (`activeSelf == False`, measured
   while the Leaderboard screen was on screen), so the Leaderboard has no visible BACK
   control either; `RankingsScreenController._backButton` is wired to it. A8 was therefore
   driven through `GoBack(_returnScreen)` — the exact call that button's listener makes.
4. **`StaminaShopSelectionScreenController._cancelButton` is null** (no cancel control in the
   scene), so A13's second CANCEL was driven through `OnCancelClicked()` directly. Same class
   of gap as 1 and 3 — three shell screens have no back affordance at all.
5. `StopAllCoroutines()` in the compare `OnDisable` path: not added separately — all three
   `ForceExitImmediate()` implementations already call `StopAllCoroutines()` as their first
   statement, and nothing else long-lived runs on those three components (checked: their only
   coroutines are `SlidePanel` / `FadeIn` / `FadeOut`).
