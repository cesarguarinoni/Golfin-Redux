# Quick spec — `result_screen_nav_bars`

**Filed:** 2026-09-14, from Cesar: *"After playing any hole, there is no way from the Result screen
to go back to the main menu. Add both nav bars (top and bottom) so players can navigate outside."*

Mid-run correction (first take, 2026-09-14): *"Top nav bar is being drawn under the game UI"* —
see § Iteration.

## Problem

The hole-complete result (`Canvas/HoleCompleteModal/HoleCompleteWidget`, the two-card §2d screen)
offered REPLAY / RETRY and PLAY NEXT and nothing else. The Figma frame it was built from
(`12988:5223`) has the shared top bar with a **RESULTS** title and the five-slot bottom nav under the
cards — both were deferred at build time ("Q3 lock — full-implementation phase") and never came back.
In-game QUIT lives in the shot view's gear, which is gone once the ball is in the cup.

Three things stood in the way of just calling `ShowBars()`:

| | State at HEAD | Consequence |
|---|---|---|
| Sorting | `HoleCompleteModal` canvas override **900**, the widget's own nested canvas **32767**; `PersistentUI` is **0**, `SettingsScreen` **100** | the 92 % scrim drew over the bars and its raycast ate every tap; the gear's Settings overlay would have opened *under* the result |
| Gameplay HUD | `LabRoot/ShotUI_Canvas` (0) and `LabCanvas` (10) stay active at hole-out (player card, hole card, in-game gear) | anything sorted under 0 gets the HUD painted over it |
| Leaving | `ScreenManager.ShowScreen` swaps shell screens in place | a nav tap would put Home up with `LabScaffold` + `Hole_NN_Geo` still loaded behind it and the result still in front; the hole's progression write + reward grant live on the REPLAY / PLAY NEXT handlers, so leaving any other way paid nothing |

## Fix

**`Assets/Scripts/UI/PersistentUIManager.cs`** — `ShowBars(string centerTitleKey)`: both bars with
full chrome and a localized centre title (`RESULT_RESULTS` = "RESULTS" / "リザルト", already in the
table — nothing to publish). The key is remembered (`_centerTitleKeyOverride`) so the language toggle
re-resolves it (`RefreshTopBarCenterText`); the next `HighlightScreen` — any real navigation — clears
it. `ApplyTopBarCenterText` was split into the screen-keyed caller + `ApplyCenterTitle(text)`.

**`Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs`**
- Canvas override at **-1** (`ResultSortingOrder`, the shell screens' own order) and the widget's
  nested canvas override switched OFF in `Awake` — code already owned this z-order at runtime, so no
  scene edit. The bars (0) draw over the scrim and win the raycast; Settings (100) opens over it.
- `ShowChrome()` after `_widget.Show(...)` and before the mission-cards branch (both card kinds get
  the bars): `HideGameplayHud()` deactivates every **root canvas** of the loaded `LabScaffold`
  (they sort above -1 *and* above the bars), then `PersistentUIManager.ShowBars("RESULT_RESULTS")`.
  Nothing is restored — every way off the result reloads a hole or unloads the scene.
- `SettleRound()` (= `WriteProgressionIfSuccess` + `GrantRewards`, the two calls REPLAY and PLAY NEXT
  already made) and `OnGameplayExiting()`: on `GameplaySceneLoader.GameplayExiting`, if the result
  is showing, settle and hide. A nav-bar exit therefore pays and unlocks exactly like PLAY NEXT.

**`Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs`** — `IsGameplayLoaded` (LabScaffold
loaded), `IsExiting`, the static `GameplayExiting` event (raised in `ExitRoutine` under the curtain,
BEFORE the unload; wrapped), `ClearRunState()` (= `GameSession.ResetSession()` + `HoleContext.Reset()`,
the Stage D MENU contract, now shared), and `ExitToScreen` ignores a second request while one is in
flight (first tap keeps its target).

**`Assets/Scripts/UI/ScreenManager.cs`** — the gameplay-exit gate in `Navigate`, after the other
gates: any target but `Loading` while `IsGameplayLoaded` → `loader.ExitToScreen(target, ClearRunState)`
and return. `Loading` is exempt (the loader's own preload shows it while the previous hole is still
loaded — REPLAY / PLAY NEXT / next mission); the loader's own `ShowScreen(target, instant)` runs after
the unload, so the gate passes it through. Covers the five nav slots, the ticket "+", and
Settings ▸ LOG OUT with one rule (PATTERNS §14).

**`Assets/Scripts/UI/Modals/InGameSettingsModalController.cs`** — QUIT passes `ClearRunState` instead
of its own copy of the two calls.

Not chosen: raising `PersistentUI`'s order while the result is up (Settings at 100 would then sit
under the bars); per-button guards in `PersistentUIManager` (LOG OUT and any future route would have
kept the bare swap); paying out at show time (an economy-timing change Cesar did not ask for — REPLAY /
PLAY NEXT keep their behaviour, the new exit joins them).

## Iteration

Take 1 (22 assertions, 21 PASS): `result.tap_gear` FAILED — the press at the top-bar gear landed on
`LabRoot/ShotUI_Canvas/HoleCard/HoleMapContainer/HoleMap` — and Cesar, watching, said the top bar was
drawn under the game UI. `HideGameplayHud` looked at root *GameObjects* with a Canvas; `ShotUI_Canvas`
hangs under `LabRoot`, so only `LabCanvas` had been hidden. Fixed to enumerate root **canvases**
(`Resources.FindObjectsOfTypeAll<Canvas>()` filtered by scene + `isRootCanvas`); the bot's
`result.hud_hidden` check now enumerates the same way and requires ≥ 2 canvases. Take 2: 22/22.

## Evidence

Driver: `GOLFIN ▸ Result Screen ▸ Run nav-bars verify bot` (`+ clip` records through
`BotVideoRecorder`, deferred start, 30 fps cap) —
`Assets/Scripts/UI/Modals/Result/Editor/ResultScreenNavVerifyBot.cs`. Real boot: StartButton → Home
→ the mode card's `PlayButton` → HoleSelection → the hole card's `ActionButton` → the hole loads →
first-visit hints closed through their own NextButton → **a real putt** through `BotSwing.PlayPerfect`
(the selected scheme was Pendulum) → `HoleCompletionBridge` → `MarkHoleComplete` → the result. The one
non-player step is `PlaceBallAt` 1.2 m from the cup before the putt, logged as `SCAFFOLDING`.

`media/result_screen_nav_bars/verdict.json` (the recorded run, hole 5) — **PASS, 22/22**:

| Assertion | Detail |
|---|---|
| `bars.hidden_during_play` | top bar + bottom nav inactive while the hole is live |
| `holeout.real` | `terminal=InCup strokes=1` from the real putt (no seam) |
| `result.bars_visible` / `result.title` | both bars active; centre text `RESULTS` |
| `result.sorts_under_bars` | modal canvas override, order -1; widget canvas override off; PersistentUI 0 |
| `result.hud_hidden` | `LabCanvas`(10) and `LabRoot/ShotUI_Canvas`(0) inactive |
| `result.tap_home_slot` / `tap_tee_slot` / `tap_gear` | `EventSystem.RaycastAll` at each control's centre → top hit IS the control (`NavHomeButton`, `NavTeeButton`, `TopBar/SettingsButton`, all order 0) |
| `result.tap_replay_still_works` | top hit `…/Card1/ContentRoot/Buttons/ReplayButton` (order -1) |
| `result.gear_opens_settings` / `settings_closed` / `still_showing_after_settings` | Settings opens over the result from the real gear, closes, result still up with the hole loaded |
| `nav.home_reached` / `gameplay_unloaded` / `result_hidden` / `bars_on_home` / `title_restored` / `session_cleared` | after the real `NavHomeButton.onClick`: Home in 0.3 s, `LabScaffold` + `Hole_NN_Geo` gone, widget hidden, bars on, title back to the username, `CurrentHoleNumber=0` |
| `settle.progression_written` / `settle.rewards_granted` | `HasPlayed(5)` false → true, `IsUnlocked(6)` true; `_rewardsGranted` true, RP 6148 → 6158 |

Stills (take 2, `SnapPlayModeSafe`, 1170×2532): `01_gameplay_before_holeout.png`,
`02_result_screen_with_bars.png` (canonical), `03_settings_over_result.png`, `04_home_after_nav.png`.
Clip: `result_screen_nav_bars_demo.mp4` (17 s, captions checked against decoded frames; times in
`captions.json`). Run logs: `verify_run1.log` (the take with the HUD miss), `verify_run2.log`,
`verify.log` (recorded run).

Tests: `HoleCompleteModalControllerTests` 11/11 (+2: `Modal_GameplayExiting_SettlesTheRoundWhenTheResultIsUp`,
`Modal_GameplayExiting_IsANoopWhenNoResultIsUp`); `Golfin.UI.Tests` 11/11 (+4: `IsGameplayLoaded`,
`ClearRunState`, `GameplayExiting` raised before the unload, `ShowBars(titleKey)` owns the title until the
next highlight); `Golfin.HoleCompleteModal.Tests` 18/18; `Golfin.UI.Polish.Tests` 169/170 — the one
failure (`PressFeedbackCoverageTests.EnumeratedTapTargets_HavePressFeedback`) is "Problem detected while
opening the Scene file: 'Assets/Scenes/ShellScene.unity'" on a scene this task does not touch
(`ShellScene.unity` unmodified in the working tree), so it is HEAD's.

## Out of scope / follow-ups

- **1v1 result (`VersusResultModal`, canvas 901)** already calls `ShowBars()`, but its own 50 %-black
  `BG` at 901 sits over them: the bars show dimmed and no slot is tappable; NEW MATCH → Home is the
  only exit. Same shape as this task (sort under the bars, hide the gameplay HUD, close on
  `GameplayExiting`); not done here — decision for Cesar.
- Reward timing: the hole still pays on the way OUT (REPLAY / PLAY NEXT / nav-bar exit), not when
  the result is shown. Paying at show time would let the top-bar RP counter tick up while the card
  says "x10" — a product call, not made here.
