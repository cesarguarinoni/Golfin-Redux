# SPEC — `ingame_settings_modal`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` (starts `SPEC_READY`).

## Goal

Give the in-game gear button (gameplay HUD only) a real function: it opens a new **In-Game Settings** overlay with (1) a Sound Settings card (SFX + Music volume sliders) and (2) a "PLAYING" card showing the current course/hole/par, hole map, strategy text, hole rewards, and BACK / QUIT buttons. QUIT (solo play only) asks for confirmation ("you will not get any rewards") and tears down gameplay back to Home. In the same change, **remove the cheat currently wired to that gear**: the §2f GreenTuningPanel debug widget toggles open on gear tap today. The shell/menu settings gear (`PersistentUIManager` / `SettingsScreen.prefab`) is untouched.

## Reference

- **Figma frames:** `In-game Settings` id `13873:33610` and `In-game Settings - Confirm` id `13905:6678`, file `5gEAHjl6xAtW8iYY7NMvWd` (Golfin Game Redux)
- **Node renders dropped to `reference/`:**
  - `reference/ingame_settings_base.png` — modal open: Sound Settings card + PLAYING card + BACK/QUIT
  - `reference/ingame_settings_confirm.png` — quit-confirm state: settings dimmed behind, centered "ARE YOU SURE?" card with BACK/CONFIRM
- **Placeholder vs canonical content notes:** "Lomond Country Club - Hole 6 - Par 5", the strategy blurb (with typo "Sslopping"), the hole-map images, and the `x10 / x10 / x10` rewards are MOCKUP DATA — at runtime all of it binds to the live hole (HoleContext + HoleData). Slider positions are placeholder; they bind to saved AudioManager volumes. The background is a blurred gameplay shot — implement as the live gameplay scene behind a dim backdrop (no blur pass).

## Figma Fidelity (enumerate EVERY element — Rule 18)

Canvas reference: 1170×2532. All Figma coords below are in that space.

| Element | Figma node | Property → value (size/pos/fill/border/font/content) |
|---|---|---|
| Dim backdrop | (frame bg) | full-screen Image over the whole HUD, black ~55–65% alpha; raycast target ON (blocks all gameplay input behind the modal) |
| Sound Settings card | `13873:33672` | 978×736 at x=96, y≈466 (screen space); dark navy card w/ vertical gradient + light 3px rounded border — REUSE the existing modal card background sprite (same family as `TournamentSignupModal.prefab` / Pop-Up cards), do NOT author a new one |
| Header row | `13873:33675` | speaker icon 72×72 (reuse existing Settings/sound icon sprite) + title "SOUND SETTINGS" — localize with existing key `SETTINGS_SOUND`, force uppercase via TMP; grey-white #C7CDD6-ish, bold, ~50px |
| SOUND label | `13873:33686` | "SOUND" white bold ~52px, left-aligned inset ~96px from card edge. NEW key `INGAME_SFX_LABEL` (this slider is SFX) |
| SOUND slider | `13873:33690` | width 882, tapered blue fill (bright #2D7DE0→dark track), round white/silver ball knob ~64px. Controls **SFX volume**. REUSE the Slider structure/graphics from `SettingsScreen.prefab`'s SoundSettingsSubmenu; only re-skin if its visuals visibly differ from the render |
| MUSIC label | `13873:33694` | "MUSIC" same style as SOUND — reuse key `SETTINGS_MUSIC`, TMP uppercase |
| MUSIC slider | `13873:33698` | identical to SOUND slider; controls **Music volume** |
| PLAYING card | `13873:35605` | 978×820.5 at x=96, y≈1226; same card background family as above |
| PLAYING title | `13873:35608` | "PLAYING" centered, grey-white bold ~48px. NEW key `INGAME_PLAYING` |
| Course subtitle | `13873:35610` | "Lomond Country Club - Hole 6 - Par 5" centered white bold ~44px → bind from `HoleContext.CourseName` (localized course name) + `HoleContext.HoleNumber` + `HoleContext.Par`. Reuse the exact composition format the hole-select expanded card uses if one exists (NOTE: verify; else compose `{course} - Hole {n} - Par {p}` with localized "Hole"/"Par" fragments) |
| Separator lines | `13873:35611` etc. | thin 1px light lines full card width — reuse `Divider.prefab` / existing separator sprite |
| Green thumbnail | `13873:35615` | 94×95 rounded rect, green close-up image, top-left of map block — reuse the same green/map sprites the hole-select card shows for the current hole |
| Hole map | `13873:35617` | 155×288 vertical hole-map image → current hole's map sprite (same source as `HoleCardWidget._holeMaps` / `HoleData.holeImageName`) |
| Strategy text | `13873:35624` | 404 wide, left column text ~34px line-wrapped; white with **gold/yellow (#E8C55A-ish) emphasis spans**. Bind `HoleData.descriptionKey` via LocalizationManager. Rich-text color tags: only if the localized string already carries them — do NOT hand-author emphasis (NOTE: check how hole-select renders this text; match it) |
| Rewards row | `13873:35629` | 3 entries centered: icon 42×42 + "x10" white bold ~44px each → bind current `HoleData.rewards` (icon per `RewardType`, `x{amount}`), same icon mapping as `HoleCardController.PopulateRewards`/`GetRewardIcon`. Show only actual reward count (design shows 3) |
| BACK button | `13873:35656` | 359×120 silver/white pill w/ bevel, label "BACK" dark navy bold — REUSE the silver button (signup modal `CancelButton` graphics + `ButtonPressFeedback`). NEW key `INGAME_BACK` ("BACK" / 戻る) |
| QUIT button | `13873:35658` | 249×120 gold pill, label "QUIT" dark brown bold — REUSE `Assets/Prefabs/UI/Common/GoldPrimaryButton.prefab`. NEW key `INGAME_QUIT` ("QUIT" / やめる). **Hidden when not solo (see §Behavior)** |
| Confirm dialog card | (13905 frame, confirm overlay) | centered card ~1010×430 at y≈1050 screen space; same card bg family; sits OVER a second dim layer that dims the settings cards behind it (see `ingame_settings_confirm.png`) |
| Confirm title | — | "ARE YOU SURE?" centered white bold ~52px. NEW key `INGAME_QUIT_CONFIRM_TITLE` |
| Confirm body | — | "YOU WILL NOT GET ANY REWARDS WHEN QUITTING." centered white bold ~44px, 2 lines. NEW key `INGAME_QUIT_CONFIRM_BODY` |
| Confirm BACK | — | silver pill ~350×110, same silver button reuse, key `INGAME_BACK` |
| Confirm CONFIRM | — | gold pill ~390×110, `GoldPrimaryButton` reuse, label from existing key `MODAL_CONFIRM` |

## Architecture context

- **Scene:** the gameplay scene is `Assets/Scenes/LabScaffold.unity` (loaded additively by `GameplaySceneLoader`, `GAMEPLAY_SCENE_NAME = "LabScaffold"`). The HUD canvas is `ShotUI_Canvas`; the gear is `ShotUI_Canvas/SettingsButton`.
- **The cheat to remove:** `GreenTuningPanel` (`Assets/Scripts/Physics/Viewer/GreenTuningPanel.cs`) has `[SerializeField] Button toggleButton` wired in LabScaffold to that same gear Button ("deliberately the REAL settings wheel" per its comment). Gear tap currently toggles the green-tuning debug sliders.
- **Existing code referenced:**
  - `SettingsButton.cs` (`Golfin.Gameplay.UI.ShotUI`) — currently only `Debug.Log("[Settings] tapped")`
  - `ModalController` (`Golfin.UI.Modals`, `Assets/Scripts/UI/Modals/ModalController.cs`) — base class: show/hide + fade, backdrop, closeButton, `OpenModalCount` stack tracking
  - `SoundSettingsSubmenu.cs` (`Golfin.UI`) — the menu-side sliders; copy its AudioManager binding pattern (slider 0–1 ↔ AudioManager 0–100)
  - `AudioManager` (`Golfin.Audio`, singleton): `GetMusicVolume()`, `GetSFXVolume()`, `SetMusicVolume(float)`, `SetSFXVolume(float)` — persistence lives inside AudioManager; do not add PlayerPrefs handling here
  - `HoleContext` (`Golfin.Gameplay.UI.HUD`, static): `CourseName`, `HoleNumber`, `Par`, `OnChanged`
  - `HoleDatabaseLoader.RuntimeDatabase` + `HoleData` (`Assets/Scripts/UI/HoleData.cs`): `courseNameKey`, `descriptionKey`, `holeImageName`, `rewards` (NOTE: use the database's actual lookup method for the current hole — verify name in `HoleDatabase`)
  - `GameplaySceneLoader.Instance.UnloadGameplay()` (coroutine) — the sanctioned teardown; call it exactly like `VersusResultModalController.NewMatchRoutine()` does (yield inside a coroutine, then post-unload state reset)
  - `GameSession.IsVersus` (`Golfin.Gameplay.Loop`), `TournamentRoundContext.IsActive` — mode gates for QUIT
  - `ButtonPressFeedback` (`Assets/Scripts/UI/ButtonPressFeedback.cs`) — on every new button
  - `LocalizationManager.Get(key)` — all user-facing strings
- **Existing assets referenced:** `Assets/Prefabs/UI/Common/GoldPrimaryButton.prefab`, silver button graphics from `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` (CancelButton), card backgrounds/separators from the Modals prefab family, sliders from `Assets/Prefabs/UI/SettingsScreen.prefab`, hole map/green sprites already used by hole selection / `HoleCardWidget`.
- **Asmdef note:** `Assets/Scripts/UI/**` is Assembly-CSharp (no asmdef) and can see everything (`Golfin.Gameplay.UI` is autoReferenced). `Golfin.Gameplay.UI` canNOT reference Assembly-CSharp — so the new controller lives in `Assets/Scripts/UI/Modals/` and takes the gear Button as a scene-wired `[SerializeField]` reference (Inspector wiring crosses assemblies fine). Do not add asmdef references.

## Implementation

1. **New prefab `Assets/Prefabs/UI/Modals/InGameSettingsModal.prefab`**, instanced in `LabScaffold.unity` under `ShotUI_Canvas` as a top-most sibling (above all HUD widgets), root inactive by default:

   ```
   InGameSettingsModal            [InGameSettingsModalController : ModalController]
   ├── Backdrop                   [Image, black ~60%, raycast ON]          ← backdrop
   ├── SoundCard                  [reused card bg]
   │   ├── Header (icon + "SOUND SETTINGS")
   │   ├── SfxRow    ("SOUND" label + Slider)
   │   └── MusicRow  ("MUSIC" label + Slider)
   ├── PlayingCard                [reused card bg]
   │   ├── Title "PLAYING" / CourseSubtitle
   │   ├── Separator
   │   ├── MapBlock (green thumb + hole map) + StrategyText
   │   ├── Separator + RewardsRow (3 icon+amount slots) + Separator
   │   └── ButtonsRow: BackButton (silver) + QuitButton (gold)
   └── ConfirmDialog              [starts inactive]
       ├── ConfirmBackdrop        [second dim layer over the cards]
       ├── Card: "ARE YOU SURE?" / body text
       └── ButtonsRow: BackButton (silver) + ConfirmButton (gold)
   ```

2. **New script `Assets/Scripts/UI/Modals/InGameSettingsModalController.cs`** (`Golfin.UI.Modals`), extends `ModalController`:
   - `[SerializeField] Button gearButton` — wired in LabScaffold to `ShotUI_Canvas/SettingsButton`'s Button. On click → `Show()` (toggle: if visible, `Hide()`).
   - On `Show()`: bind sliders from `AudioManager.Instance.GetSFXVolume()/GetMusicVolume()` (÷100, `SetValueWithoutNotify`), bind PLAYING card from `HoleContext` + current `HoleData` (subtitle, map sprites, strategy text, rewards), and set `QuitButton.SetActive(isSolo)` where `isSolo = !GameSession.IsVersus && !TournamentRoundContext.IsActive`. When QUIT is hidden, center BACK in the row.
   - Slider `onValueChanged` → `AudioManager.Instance.SetSFXVolume(v*100)` / `SetMusicVolume(v*100)` live (same as `SoundSettingsSubmenu`). Subscribe in `OnEnable`, unsubscribe in `OnDisable` (project convention).
   - BACK (and base-class backdrop/close behavior) → `Hide()`, gameplay resumes untouched. Do NOT touch `Time.timeScale`.
   - QUIT → show `ConfirmDialog`. Confirm-BACK → hide dialog (settings still open). CONFIRM → hide modal, then `StartCoroutine`: `yield return GameplaySceneLoader.Instance.UnloadGameplay();` — mirror `VersusResultModalController.NewMatchRoutine()` (null-guard the Instance; frame-gap yield after Hide). No rewards, RP, or stamina are granted or refunded — the round is simply discarded. NOTE: verify against the existing solo hole-exit path ("Stage D MENU button") and mirror any session/HUD state reset it performs (`GameSession`, `HoleContext.Reset()` etc.) so re-entering a hole from Home is clean. If no such solo path exists yet, the `TournamentRoundHandler` teardown (line ~145) is the reference for what must be reset.
   - Register in the `ModalController` stack automatically via base `Show()/Hide()` (keeps `OpenModalCount` honest for `ModalStackEmptied` consumers).

3. **`SettingsButton.cs`:** delete the `Debug.Log` listener (the class can stay as a marker component, or be removed from the GO — implementer's choice; the modal controller owns the click now via its serialized reference. If keeping the script AND adding the listener from the controller, make sure the button ends with exactly one behavior: open/toggle the modal).

4. **Remove the cheat (LabScaffold scene edit):** on the `GreenTuningPanel` component, clear the `toggleButton` reference (set to None). Keep the component, `panelRoot`, and the class itself — the Physics Lab / DashboardUI path still uses them. Result: gear never opens the tuning panel in gameplay; the tuning panel simply has no gameplay entry point anymore.
   - NOTE: `SmokeRunner2fHost` captures "controls_2f_tuning_panel_open" (S3) — check whether it opens the panel via the gear Button; if so, point it at `GreenTuningPanel.TogglePanel()` (or its own injected button) so the smoke capture keeps working.

5. **Localization — add to `Assets/Localization/LocalizationText.csv`:**

   | key | English | Japanese |
   |---|---|---|
   | `INGAME_SFX_LABEL` | SOUND | サウンド |
   | `INGAME_PLAYING` | PLAYING | プレイ中 |
   | `INGAME_BACK` | BACK | 戻る |
   | `INGAME_QUIT` | QUIT | やめる |
   | `INGAME_QUIT_CONFIRM_TITLE` | ARE YOU SURE? | 本当によろしいですか？ |
   | `INGAME_QUIT_CONFIRM_BODY` | YOU WILL NOT GET ANY REWARDS WHEN QUITTING. | 途中で終了すると報酬は獲得できません。 |

   Reused existing keys: `SETTINGS_SOUND` (card title, TMP uppercase), `SETTINGS_MUSIC` (TMP uppercase), `MODAL_CONFIRM`.

6. **Shot-in-progress interaction:** the HUD fades/disables buttons during a live shot (`OtherButtonsFader` / `ShotInProgressUiGate`). Verify the gear participates in that gating (it should already); if it doesn't, add it so the modal can't open mid-shot. Opening while the ball is at rest is the only supported entry.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Gear tap in gameplay opens the settings overlay; second tap (or BACK, or backdrop per ModalController) closes it and gameplay continues exactly where it was
- [ ] GreenTuningPanel NEVER appears on gear tap anymore (the cheat is gone); Physics Lab usage of GreenTuningPanel still compiles/works
- [ ] Menu-side settings (nav gear / SettingsScreen) completely unaffected
- [ ] SOUND slider changes SFX volume live; MUSIC slider changes music volume live; values persist via AudioManager across close/reopen and across a quit-to-home → new round
- [ ] PLAYING card shows the REAL current course name, hole number, par, hole map, strategy text, and that hole's actual rewards (not the Figma placeholders)
- [ ] QUIT visible in solo only; hidden in 1v1 versus and tournament rounds (BACK re-centers)
- [ ] QUIT → confirm card matches `reference/ingame_settings_confirm.png`; confirm-BACK returns to settings; CONFIRM unloads gameplay to Home with no rewards granted and no orphaned state (re-entering a hole afterwards works)
- [ ] All new buttons use existing button prefabs/graphics + ButtonPressFeedback; both cards use existing card backgrounds — zero newly-authored button/card art
- [ ] All strings via LocalizationManager; JP renders correctly for every new key
- [ ] Figma fidelity table reproduced with PASS/FAIL per row against `reference/ingame_settings_base.png`
- [ ] No white-box placeholders visible in the screenshot
- [ ] All `[SerializeField]` references wired in the Inspector
- [ ] Unity Console has no errors related to this task

## Files / hierarchy this task touches

- `Assets/Scripts/UI/Modals/InGameSettingsModalController.cs` — NEW
- `Assets/Prefabs/UI/Modals/InGameSettingsModal.prefab` — NEW
- `Assets/Scenes/LabScaffold.unity` — instance the modal under ShotUI_Canvas; wire gearButton; clear GreenTuningPanel.toggleButton
- `Assets/Scripts/Gameplay/UI/ShotUI/SettingsButton.cs` — remove debug log listener
- `Assets/Localization/LocalizationText.csv` — 6 new keys
- `Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs` — only if its S3 capture drove the gear button

## Smoke evidence

Human-in-the-loop play-and-confirm (Lesson O): load a solo hole in Editor play mode → tap gear → screenshot vs `reference/ingame_settings_base.png` → drag both sliders (hear the change) → QUIT → screenshot vs `reference/ingame_settings_confirm.png` → CONFIRM → land on Home → start another hole and confirm volumes stuck and the round starts clean. Repeat entry in a versus/tournament round to show QUIT hidden. Describe all of it in IMPLEMENTER_REPORT.md.

## Out of scope (do NOT do these)

- Quit/forfeit rules for tournament rounds and 1v1 versus (QUIT is hidden there). **Flagged for a future task:** a player can still kill the app mid-tournament-round — abandoned-round handling (stamina already spent, hole marked, board consistency) needs its own spec.
- The shell/menu settings screen, language, profile, or any other settings content — this modal is sound-only + quit.
- Background blur (dim only), pausing physics/Time.timeScale, Android back-button handling.
- Any GreenTuningPanel feature changes beyond unwiring the gear.
- Granting/refunding rewards, RP, or stamina on quit.
