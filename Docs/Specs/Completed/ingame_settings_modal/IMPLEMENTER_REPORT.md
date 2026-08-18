# IMPLEMENTER_REPORT — `ingame_settings_modal`

**Iteration shape:** `ingame_settings_modal:initial-build`
**Date:** 2026-08-18
**Baseline:** HEAD `d2c601417334d62edd940e0a49c5c30808e2a0cf` (branch `main`)
**Mode:** Direct implementation at Cesar's request — the subagent chain (implementer → self-review → reviewer → red-team) was **not** run. Every gate below was executed by hand in this session; treat the report as self-graded.

**Canonical screenshot:** `screenshots/01_gameplay_settings_open_solo.png` (1170×2532, real play mode, Hole 6, driven through the real gear `Button.onClick`)
**Canonical video:** `videos/ingame_settings_modal_demo.mp4` (1170×2532, 48.3s, 9.5 MB, captioned) — also copied to `Docs/Reports/Media/ingame_settings_modal_demo.mp4`

---

## 1. What shipped

The gameplay HUD gear (`LabRoot/ShotUI_Canvas/SettingsButton`) now opens a real In-Game Settings overlay — SOUND SETTINGS card (SFX + Music sliders bound live to `AudioManager`) and a PLAYING card bound to the live hole (course/hole/par, hole map, strategy text, that hole's actual rewards) with BACK and a solo-only confirm-gated QUIT. The GreenTuningPanel debug cheat that used to open on that same gear is unwired.

---

## 2. Files modified or created

| File | Status | What changed |
|---|---|---|
| `Assets/Scripts/UI/Modals/InGameSettingsModalController.cs` | NEW | `ModalController` subclass: gear toggle, AudioManager slider binding, PLAYING-card data binding, solo-only QUIT gate, confirm dialog, quit teardown |
| `Assets/Prefabs/UI/Modals/InGameSettingsModal.prefab` | NEW | The overlay: backdrop + SoundCard + PlayingCard + ConfirmDialog, built entirely from existing sprites/cloned buttons. Green-thumbnail slot removed at close-out (§8). |
| `Assets/Scenes/Physics/LabScaffold.unity` | MODIFIED | Instanced the prefab as the last child of `ShotUI_Canvas`; wired `gearButton`; **cleared `GreenTuningPanel.toggleButton`** (the cheat) |
| `Assets/Scripts/Gameplay/UI/ShotUI/SettingsButton.cs` | MODIFIED | Removed the `Debug.Log("[Settings] tapped")` listener; class kept as a typed marker so no scene component is orphaned |
| `Assets/Localization/LocalizationText.csv` | MODIFIED | +6 keys (EN + JP) per SPEC §5 |
| `Assets/Scripts/UI/Editor/InGameSettingsDemoRecorder.cs` | NEW | Editor-only demo recorder for the report video, built on the existing `GameplayLocalizationDemoRecorder` pattern (same deferred start + render-state lock). Drives the real player path and stamps caption marks to a sidecar. |
| `Assets/Localization/LocalizationTextTable.asset` | MODIFIED | Auto-regenerated table — append-only, contains exactly the 6 new keys and nothing else (`git diff` = 21 insertions, 0 deletions) |

`Docs/TellCode.md` was already modified in the working tree at the baseline commit (`M Docs/TellCode.md` in `git status` before any work started) and was **not** touched by this task.

**Not modified (spec anticipated it might be):** `Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs`. Its S3 capture opens the tuning panel by reflecting on the private `panelRoot` field and calling `SetActive(true)` directly (lines 321-343) — it never went through the gear Button, so clearing `toggleButton` cannot break it.

---

## 3. Clone provenance

| Element | Source | Verified |
|---|---|---|
| SoundCard / PlayingCard / ConfirmCard background | `Assets/Art/ResultScreen/Background - HoleCard.png` (Sliced, ppum 1, border 50/50/50/50) — the same sprite `TournamentSignupModal.prefab > Panel > Background` uses | PASS — read back from the saved prefab |
| BACK buttons (×2) | `Object.Instantiate` of `TournamentSignupModal.prefab > Panel/ButtonsRow/CancelButton` (sprite `Assets/Art/RosterScreen/ButtonCancel.png`, Sliced ppum 1.25, border 25) | PASS |
| QUIT / CONFIRM buttons | `Object.Instantiate` of `TournamentSignupModal.prefab > Panel/ButtonsRow/ConfirmButton` (sprite `Assets/Art/ResultScreen/Button - Retry.png`, Sliced ppum 0.8, border 16) | PASS |
| Sliders (×2) | `Object.Instantiate` of `ShellScene > SettingsScreen/SettingsPanel/SettingsList/SoundSettingsRow/SoundSettingsSubmenu/MusicVolumeSection/MusicVolumeSlider` | PASS |
| Slider track / wedge / knob sprites | `Assets/Art/Settings/Volume Background.png`, `Volume Bar.png` (both **882×180 — exactly the Figma Slider node size**), `Volume Knob.png` (116×119) | PASS |
| Separators (×5) | `Assets/Art/HomeScreen/Divider.png` — the sprite `TournamentSignupModal > Separator1` uses | PASS |
| Header speaker icon | `Assets/Art/Original UI/SettingsScreen/S_Settings_Icon_Sound.png` (72×72 = Figma icon size) | PASS |
| Reward icons | `Assets/Art/HomeScreen/Reward Points.png` / `Reward Repair.png` / `Reward Ball.png` — the exact sprites serialized on `HoleCard.prefab`'s `HoleCardController` | PASS |
| Silver title gradient | `Golfin.Utilities.TextGradients.ApplySilver` (white → `#818EA1`, matches the Figma gradient) | PASS |
| Button feedback | `Golfin.UI.Polish.ButtonPressFeedback` present on all four buttons (inherited from the clones; re-asserted by the builder) | PASS |

**Zero newly-authored button or card art.** Every `Image` in the prefab either carries one of the sprites above or is a deliberate flat scrim / a slot bound at runtime.

**One deviation from the SPEC:** the spec's §Figma Fidelity rows say QUIT/CONFIRM should reuse `Assets/Prefabs/UI/Common/GoldPrimaryButton.prefab`. I cloned the signup modal's `ConfirmButton` instead. Reason: `GoldPrimaryButton` uses `Play Button.png` at `Image.Type.Simple` (not 9-sliced), so resizing it to the Figma QUIT width of 249px would stretch the corner radius non-uniformly — the exact Rule-21 corner-distortion trap. The signup `ConfirmButton` is the same gold pill family, is properly 9-sliced, and is already the natural pair for the silver `CancelButton` at the Figma sizes. Flagging for Cesar in case he wants the other prefab anyway.

---

## 4. Figma fidelity

Node re-pulled at step 0 (Rule 9): `get_metadata` + `get_design_context` on `13873:33610` / `13873:33672` / `13873:35605` (base) and `13905:6678` / `13905:6714` (confirm), file `5gEAHjl6xAtW8iYY7NMvWd`. Geometry taken **1:1** from the node (`ShotUI_Canvas` is CanvasScaler ScaleWithScreenSize, ref 1170×2532, match 0 → scaleFactor 1). Fonts converted **÷1.2** per the shell-canvas rule, then A/B'd against the `reference/` renders at matched scale. Figma letter-spacing applied per element as `tracking_px / font_px × 100`.

Built-vs-reference A/B was done on a full-res 1170×2532 overlay of `reference/ingame_settings_base.png` against the prefab render, then re-confirmed on the live play-mode capture.

| # | Element | Figma (node) | Built | Weight | Rendered size vs reference | Verdict |
|---|---|---|---|---|---|---|
| 1 | Dim backdrop | full-screen, raycast on | 1170×2532 stretch, `#000000` α0.60, raycastTarget on | — | — | PASS — probe render (opaque-red substitution) proved it covers every HUD widget; restored to α0.60 |
| 2 | Sound card | 978×736 @ (96, 475.75) | 978×736 @ (96, 475.75) | — | corner A/B at 2× zoom: radius + border + gradient match | PASS |
| 3 | Header icon | 72×72 @ card (48, 48) | 72×72, same sprite at native size | — | 1:1 | PASS |
| 4 | "SOUND SETTINGS" | Rubik SemiBold 48 / lh63 / track −0.93, silver gradient, @ card (136, 52.5) | TMP 40 (48÷1.2), Rubik-SemiBold + Bold, `TextGradients.Silver`, track −1.94, UpperCase | SemiBold→Bold | matches reference cap-height | PASS |
| 5 | "SOUND" | Rubik SemiBold 48 white @ card (84, 154) | TMP 40 white Bold, track −1.94, @ (84, 154) | SemiBold→Bold | matches | PASS |
| 6 | SFX slider | Slider instance 882×180 @ card (36, 229); visible bar inset 96, knob travel 154–728 | 882×180 @ (36, 229); `Volume Background` 882×180 native; knob 116×119 native; slide area inset 154 | — | knob and wedge geometry match the reference | PASS |
| 7 | "MUSIC" | Rubik SemiBold 48 white @ card (84, 457) | TMP 40 white Bold @ (84, 457) | SemiBold→Bold | matches | PASS |
| 8 | Music slider | 882×180 @ card (36, 532) | identical to #6 @ (36, 532) | — | matches | PASS |
| 9 | Playing card | 978×820.5 @ (96, 1235.75) | 978×820.5 @ (96, 1235.75) | — | matches | PASS |
| 10 | "PLAYING" | Rubik SemiBold 45 / track −0.69, silver gradient, centred, @ card (16, 24) 946×60 | TMP 37.5, Bold + silver gradient, track −1.53, centred | SemiBold→Bold | matches | PASS |
| 11 | Course subtitle | Rubik SemiBold 39 / track −0.24 white centred @ card (16, 94) 946×54 | TMP 32.5 Bold white centred, track −0.62 | SemiBold→Bold | matches | PASS — bound at runtime, see §5 |
| 12 | Separators ×3 | 1px lines @ card y=164 / 532.5 / 652.5, 978 wide | `Divider.png` 978×2 at exactly those y | — | matches | PASS |
| 13 | Green thumbnail | 94×94.9 rounded @ card (114.19, 204) | **dropped** — Cesar 2026-08-18: *"No green thumbnail needed. Same images used for hole selection are fine."* Slot and its resolution code removed; the hole-select image already carries the card (row 14) | — | intentionally absent | **N/A (descoped)** |
| 14 | Hole map | 155.61×288.5 @ card (208.19, 204) | 155.61×288.5 @ (208.19, 204), `preserveAspect` | — | correct box; the *image* differs because the reference shows Figma's Hole-1 placeholder and the build shows the live hole | PASS |
| 15 | Strategy text | Rubik **Medium** 30 / lh36 / track −0.5, white + `#EEDC9A` spans, 404×216 @ card (411.8, 228) | TMP 25 Rubik-SemiBold **Normal** (closest shipped face to Medium — no Rubik-Medium SDF asset exists), track −1.67, lineSpacing 6, 404×216 @ (411.8, 228); gold spans come from the localized string's own `<color=#EEDC9A>` tags | Medium→SemiBold-Normal | matches reference weight after the swap (Rubik-Variable Regular read visibly too light) | PASS |
| 16 | Rewards row | 3 × (icon 42 + gap 6 + "x10"), gap 32, `pl-32`, centred in 914 @ card (32, 556.5) | HorizontalLayoutGroup spacing 32, padLeft 32, MiddleCenter; slots 134×68, icon 42×42 @ (0,13), amount @ (48,1) NoWrap | — | 3-reward row lands at the Figma x-offsets (240/406/572); 2-reward holes re-centre | PASS |
| 17 | "x10" amounts | Rubik SemiBold 51 / track −1.29 white | TMP 42.5 Bold white, track −2.53, NoWrap | SemiBold→Bold | matches | PASS |
| 18 | BACK (playing card) | 359×120 silver pill @ card (161, 676.5), label Rubik SemiBold 66 `#1E293B` | cloned CancelButton 359×120, label TMP 55 (66÷1.2) — inherited from the clone | SemiBold→Bold (clone) | matches | PASS |
| 19 | QUIT | 249×120 gold pill @ card (568, 676.5), gap 48, label `#321506` | cloned ConfirmButton resized 249×120, HLG gap 48 | SemiBold→Bold (clone) | matches | PASS |
| 20 | Confirm second scrim | full-screen dim over the settings cards | 1170×2532 `#000000` α0.55 | — | matches | PASS |
| 21 | Confirm card | 1042×436 @ (64, 1048) | 1042×436 @ (64, 1048) | — | matches | PASS |
| 22 | "ARE YOU SURE?" | Rubik SemiBold 45 / track −0.69 silver gradient centred @ card (32, 24) | TMP 37.5 Bold + silver gradient, track −1.53 | SemiBold→Bold | matches | PASS |
| 23 | Confirm body | Rubik SemiBold 39 / track −0.24 white, centred, **2 lines** breaking after "WHEN" | TMP 32.5 Bold, track −0.62, box narrowed to 800 (centred at x=121) to reproduce the authored 2-line break — at 978 wide the ÷1.2 font fits on one line and leaves the card half empty | SemiBold→Bold | matches | PASS |
| 24 | Confirm separators ×2 | @ card y=100 / 268, 978 wide @ x=32 | `Divider.png` 978×2 at exactly those coords | — | matches | PASS |
| 25 | Confirm BACK / CONFIRM | 359×120 + 391×120, gap 48, @ card (122, 292) | cloned Cancel/Confirm at those exact sizes, HLG gap 48 centred | SemiBold→Bold (clone) | matches | PASS |

24 PASS / 1 descoped (row 13, dropped by Cesar). No FAILs.

---

## 5. UI fidelity lint

`Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab("Assets/Prefabs/UI/Modals/InGameSettingsModal.prefab", null)`

```
— 0 FAIL, 13 WARN, 0 INFO —
RESULT: PASS (health)
```

All 13 warnings triaged, none are defects:

- **6 × `flat-fill`** — `Backdrop` and `ConfirmBackdrop` are intentional dim scrims (no sprite by design); `HoleMap` and the three reward `Icon`s are runtime-bound slots. Verified live in play mode: `HoleMap.sprite = Hole_06` / `Hole_01`, reward icons = `Reward Points` / `Reward Repair` / `Reward Ball`. No fabricated placeholder is ever visible.
- **3 × `9slice-cap-kink`** — inherited from the reused `Background - HoleCard.png` (identical usage to the shipping `TournamentSignupModal`). Checked with a 2× corner A/B against `reference/ingame_settings_base.png`: radius, border weight and gradient all match; no kink.
- **4 × `unlocalized-text`** — `CourseSubtitle` and the three reward `Amount`s are composed at runtime from hole data, not from a localization key. The subtitle's course/hole half **is** localized (via `HoleData.courseNameKey`) — see §6.

---

## 6. Acceptance checklist

Every row below was exercised in Editor play mode, booted through ShellScene and entered via `GameplaySceneLoader.BeginGameplayLoad` (never a direct LabScaffold load). Every gear/BACK/QUIT/CONFIRM interaction was driven through the **real widget's `Button.onClick`** — no synthetic test button exists anywhere in this change.

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| 1 | Gear tap opens the overlay; second tap / BACK / backdrop closes it and gameplay continues | **PASS** | `gear.onClick.Invoke()` → `IsVisible=True`, Panel+Backdrop active. BACK → `IsVisible=False`, `OpenModalCount=0`. Gear again → open, again → closed. No `Time.timeScale` touch anywhere in the controller. |
| 2 | GreenTuningPanel never appears on gear tap; Physics Lab usage still works | **PASS** | After ~10 gear taps in one session: `panelRoot.activeSelf=False`, `toggleButton=null`. `git diff` on the scene is a single removed line: `-  toggleButton: {fileID: 1156248958}`. `panelRoot` reference and `TogglePanel()` both intact; `SmokeRunner2fHost` S3 drives `panelRoot` by reflection, not the gear. |
| 3 | Menu-side settings unaffected | **PASS** | Zero edits to `PersistentUIManager`, `SettingsScreen.prefab`, `SettingsController` or `SoundSettingsSubmenu`. The slider was **copied** out of ShellScene, not moved — ShellScene ends the session `dirty=False` and unmodified in `git status`. |
| 4 | Sliders change SFX/Music live; values persist across close/reopen and across quit→new round | **PASS** | Drag to 0.35 / 0.95 → `AudioManager` reports 35 / 95 immediately. Close + reopen → sliders restore 0.35 / 0.95. After a full QUIT→teardown and a fresh `BeginGameplayLoad(1)` → still 0.35 / 0.95. Also survived an Editor play-session restart (session 2 opened showing session 1's values). |
| 5 | PLAYING card shows the real course/hole/par/map/strategy/rewards | **PASS** | Hole 6: `"Lomond Country Club  - Hole 6 - Par 3"`, map `Hole_06`, strategy = live `HOLE_LOMOND_6_DESC` with its own gold spans, rewards Points×20 + RepairKit×30 and the third slot correctly hidden (hole 6 ships 2 rewards). Hole 1 after re-entry: Par 5, map `Hole_01`, Points×10 / RepairKit×10 / Ball×5. None of it is the Figma placeholder. |
| 6 | QUIT solo-only; hidden in versus/tournament, BACK re-centres | **PASS** | `IsVersus=true` → QUIT inactive, BACK world-centre **x = 585.0** = exactly the card centre. Solo → QUIT active, BACK centre 436.5. Screenshot `04_quit_hidden_versus.png`. |
| 7 | QUIT → confirm matches the reference; confirm-BACK returns; CONFIRM unloads to Home, no rewards, clean re-entry | **PASS** | Confirm card matches `reference/ingame_settings_confirm.png` (`02_quit_confirm.png`). Confirm-BACK → dialog closes, settings still open. CONFIRM → `Hole_06_Geo` + `LabScaffold` both unloaded, `GameSession` cleared (hole 0, IsVersus/IsTournament false), `TournamentRoundContext.IsActive=false`, `HoleContext` reset, `OpenModalCount=0`, bottom nav restored. **Home landing confirmed on the authenticated session** (§11) — `CurrentScreen=Home`, screenshot `05_home_after_quit.png` and the tail of the demo video. No reward/RP/stamina call anywhere in the path: RP read 158 before the round and 148 after the quit, i.e. the 10-RP Practice entry fee was charged at entry and correctly **not** refunded. Re-entry afterwards is clean. |
| 8 | All buttons use existing prefabs/graphics + ButtonPressFeedback; both cards use existing backgrounds; zero new art | **PASS** | §3 provenance table; linter reports 0 fabricated fills. |
| 9 | All strings via LocalizationManager; JP renders for every new key | **PASS** | JP verified live: サウンド設定 / サウンド / ミュージック / プレイ中 / ロモンドカントリークラブ  - ホール1 - Par 5 / 戻る / やめる / 本当によろしいですか？ / 途中で終了すると報酬は獲得できません。 / 確認. Screenshot `03_japanese_confirm.png` — everything fits, no clipping. *Note:* the " - Par {n}" fragment stays English in JP because no `PAR` key exists; this matches every other surface in the app (`HoleCardController`, `HoleCompleteCardWidget`, `TournamentHoleSelectionScreenController` all hardcode it). |
| 10 | Figma fidelity table with PASS/FAIL per row | **PASS** | §4, 25 rows. |
| 11 | No white-box placeholders in the screenshot | **PASS** | Canonical screenshot inspected at full res; the green-thumb slot is hidden rather than showing an empty white box. |
| 12 | All `[SerializeField]` references wired | **PASS** | Read back from the saved prefab and the live scene instance: the only null object references are `closeButton` (an optional base-class slot this modal does not use — BACK is wired explicitly) and `gearButton`, which is wired on the scene instance rather than the prefab. |
| 13 | No Unity Console errors related to this task | **PASS** | 0 `Exception` / `NullReferenceException` / `MissingReferenceException` in the whole play session. EditMode suite **1435 total / 1432 passed / 0 failed / 3 pre-existing intentional skips**. |
| 14 | Modal cannot open mid-shot (SPEC §6) | **PASS** | The gear is *not* in `ShotInProgressUiGate._hideDuringShot` in LabScaffold (verified in the scene YAML), so `Show()` guards on `ShotInProgressUiGate.ShotInProgress` instead — a code-only fix with no scene mutation, which also keeps the gear visible mid-shot rather than making it vanish. |

---

## 7. Defects found and fixed during verification

Two real bugs surfaced only because the flow was driven end-to-end in play mode. Both are fixed and re-verified.

**A. Stale fade coroutine blanked a re-opened modal.**
`ModalController.Hide()` ends its 0.2s fade-out by deactivating `modalPanel`/`backdrop`. Re-opening inside that window left the old coroutine running, and it blanked the modal a few frames after it was re-shown — `IsVisible()==true` with `Panel.activeSelf==false`. Because the gear is a *toggle*, tap-tap-tap hit this every time; the first confirm-dialog screenshot caught it (cards gone, confirm floating over raw gameplay). Fixed in the subclass (`StopAllCoroutines()` in `Show()`/`Hide()`) rather than in the shared base class, to avoid changing behaviour for every other modal. Re-verified: rapid triple-tap → `IsVisible=True, Panel=True, Backdrop=True`, still true 2s later.

**B. The quit teardown was hosted on an object the teardown destroys.**
`StartCoroutine(QuitRoutine())` ran on the modal, which lives in LabScaffold — `UnloadGameplay()` destroys it mid-coroutine, so everything after the unload (`GameSession.ResetSession()`, `HoleContext.Reset()`, `ShowScreen(Home)`) would silently never run. `VersusResultModalController` doesn't hit this because it lives in ShellScene. Fixed by hosting the routine on `GameplaySceneLoader` (ShellScene-resident) and making it `static` so it holds no reference to the destroyed modal. Re-verified: the log shows the routine running to completion past the unload, all the way to the `ShowScreen(Home)` call.

---

## 8. Green thumbnail — descoped

The Figma card showed a 94×95 green close-up left of the hole map, and no such asset exists anywhere in the project. It was surfaced rather than fabricated, and Cesar resolved it on 2026-08-18: *"No green thumbnail needed. Same images used for hole selection are fine."*

The slot, its serialized fields and the `HoleImages/<course>/Green_NN` resolution helper have all been removed — the PLAYING card now shows exactly the hole-select image (`HoleImages/<course>/Hole_NN`) and nothing else. Re-verified after the removal: compile clean, prefab has no dangling references (only `closeButton` and the scene-wired `gearButton` are null, both by design), the scene instance kept its `gearButton` wiring and stayed undirty, UI-fidelity lint 0 FAIL / 13 WARN, and the re-rendered card is unchanged apart from the absent thumbnail.

## 9. Needs manual on-device verification

Down to three after the authenticated pass (§11):

1. **Audible slider effect.** Verified numerically (`AudioManager.GetSFXVolume()`/`GetMusicVolume()` track the sliders live, and the demo video shows the wedge + knob following a real `onValueChanged` sweep) but not confirmed by ear.
2. **Touch input on device.** Every interaction here was driven via `Button.onClick.Invoke()`; real finger taps through the `GraphicRaycaster` — including the backdrop swallowing taps meant for the HUD behind it — still want a device pass.
3. **QUIT hidden in a real tournament round.** Verified via `GameSession.IsVersus`; `TournamentRoundContext.IsActive` is the other half of the same expression but was not exercised through a live tournament entry.

## 10. Editor state at hand-off

Play mode off. Only `ShellScene` open, `dirty=False`. `LabScaffold` saved and closed. No auto-run scripts, no leftover scene mutations, no capture canvases. Scene diff is surgical: 119 added lines (the prefab instance) and **one** removed line (the cheat) — 0 `m_IsActive`, `m_SizeDelta`, `m_AnchoredPosition` or anchor churn.

---

## 11. Authenticated end-to-end pass (2026-08-18, second session)

Cesar signed the editor session in, which unblocked the two things the first pass could not reach: the **full real player path in**, and the **Home landing on quit**.

Boot went straight to `Home` (no splash gate). From there, every step was the real widget's own `onClick` — no `BeginGameplayLoad` seeding, no synthetic buttons:

```
Home → PRACTICE card PLAY → HoleSelection → Hole 1 card PLAY → matchmaking → LabScaffold + Hole_01_Geo
```

`GameSession` came out seeded by the real path (`hole=1, IsVersus=False, char='char_james'`) — nothing this task writes. Then: gear → settings (Hole 1 / Par 5 / real map / real rewards) → QUIT → confirm → CONFIRM.

Result: `CurrentScreen=Home`, only `ShellScene` loaded, `GameSession` hole 0 with both mode flags false, `TournamentRoundContext.IsActive=false`, `HoleContext` reset, `OpenModalCount=0`, bottom nav restored. RP 158 → 148 across the round: the Practice entry fee was charged on entry and not refunded on quit, exactly as the spec requires. Screenshot: `screenshots/05_home_after_quit.png`.

## 12. Report video

`videos/ingame_settings_modal_demo.mp4` — 1170×2532, 48.3s, 9.5 MB, 12 burned-in captions. Copied to `Docs/Reports/Media/ingame_settings_modal_demo.mp4`.

Recorded by `Assets/Scripts/UI/Editor/InGameSettingsDemoRecorder.cs` (`GOLFIN > Demos > Record In-Game Settings Demo Video`), which follows the shipping `GameplayLocalizationDemoRecorder` pattern: Unity Recorder, GameView input at the pinned iPhone-14 size, render state locked before `StartRecording` (the BotVideoRecorder Y-flip guard), recording deferred until the hole is stable so the load screens stay out of the clip. Captions are burned in a separate ffmpeg pass using the `textfile=` drawtext idiom (never inline text), timed off a `record_info.json` sidecar the runner stamps on the recorder clock.

Covered in the clip, all through real `onClick`s: the gear opening **settings instead of the old debug cheat panel** → a live SOUND sweep → a live MUSIC sweep → BACK closing it with gameplay untouched → re-open showing the volumes persisted → the PLAYING card bound to the live hole → Japanese → QUIT confirm-gate → confirm-BACK → English → QUIT → CONFIRM → teardown → Home.

Verified after encoding: upright (no Y-flip; checked on consecutively-decoded frames, not `-ss` keyframe samples), full resolution, all 12 caption marks present, captions sitting in the empty gap between the card and the bottom action buttons so they never cover the modal. Zero exceptions in the run.
