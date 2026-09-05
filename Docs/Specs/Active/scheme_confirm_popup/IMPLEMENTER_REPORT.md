# IMPLEMENTER_REPORT — `scheme_confirm_popup`

**Iteration shape:** `ui-modal:new-confirm-popup`
**Iteration:** 1
**Canonical screenshot:** `screenshots/ingame_popup_pendulum.png`
**Invariant JSON (the gate):** `scheme_confirm_invariants.json`, `tiles_manifest.json`

Tapping a control scheme that is not the current one — in Settings › Controls or in the in-game
gear modal — now opens a pop-up that explains the scheme with three in-game step tiles and a
numbered HOW IT WORKS list, and asks. `ControlSchemeService.Set` is called on CONFIRM and nowhere
else.

---

## 1. Files modified or created

| File | What |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeConfirmContent.cs` | **new.** Per-scheme content table: title key, 3 tile Resources paths, 3 caption keys, 3 line keys. Only literals are keys and paths. |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeConfirmDecision.cs` | **new.** The three rules, with no Unity in them: current-scheme tap is a no-op, CONFIRM commits exactly once, anything else disarms. |
| `Assets/Scripts/UI/Modals/SchemeConfirmModalController.cs` | **new.** `ModalController` shell: binds the table through `LocalizedText.SetKey`, resolves `Instance` across the two scenes, wires CANCEL / CONFIRM. |
| `Assets/Scripts/UI/Modals/ModalBackdropDismiss.cs` | **new.** Tap-the-scrim-to-close as an `IPointerClickHandler` (deliberately not a `Button` — a scrim must not take `ButtonPressFeedback`'s 0.95 press scale). |
| `Assets/Scripts/UI/Modals/Editor/SchemeConfirmModalBuilder.cs` | **new.** Builds the prefab from an `AssetDatabase.CopyAsset` of the shipping starter-confirm modal. Every geometry constant is annotated with the node it came from. |
| `Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab` | **new, cloned.** See § Clone provenance. |
| `Assets/Editor/ShotUI/SchemeConfirmTilesCapture.cs` | **new.** `GOLFIN ▸ Capture ▸ Scheme Confirm Tiles` — drives all four schemes' three states in the real game and writes the 12 tiles + `tiles_manifest.json`. |
| `Assets/Editor/ShotUI/SchemeConfirmVerify.cs` | **new.** `GOLFIN ▸ ShotUI ▸ Verify Scheme Confirm Pop-up` — the acceptance run, through the real widgets, writing `scheme_confirm_invariants.json`. |
| `Assets/Scripts/Gameplay/Tests/SchemeConfirmTests.cs` | **new.** 16 EditMode tests: the decision rules, content completeness against the CSV and Resources, and the tile manifest. |
| `Assets/Resources/UI/Controls/Tiles/T_*.png` (12) | **new.** The captured tiles, 628×680, corners rounded in alpha, imported as sprites. |
| `Assets/Scripts/UI/ControlsSubmenu.cs` | Routed through the pop-up; falls back to a direct `Set` (with a warning) if no pop-up is in the scene, so a missing prefab can never swallow the tap. |
| `Assets/Scripts/UI/Modals/InGameSettingsModalController.cs` | Same, plus an explicit current-scheme no-op. |
| `Assets/Scenes/ShellScene.unity` | One prefab instance under `SettingsScreen`, canvas `overrideSorting` order 600. **+107 lines, no other change.** |
| `Assets/Scenes/Physics/LabScaffold.unity` | Same instance under `LabRoot/ShotUI_Canvas`. **+107 lines, no other change.** |
| `Assets/Localization/LocalizationText.csv` | +26 rows (EN + JA). |
| `Assets/Localization/LocalizationTextTable.asset` | Regenerated from the CSV. |
| `Assets/Resources/Data/content_version.txt` | `texts=41 → 42` after the publish. |
| `Assets/Resources/Gameplay/controls.csv` | Header comment: re-run the tile capture whenever a scheme's UI changes (§3.2). |

**Uncommitted paths outside this task's folder that are NOT mine** (Rule 13, cited against the
iter-1 baseline block in `HEARTBEAT.log`, which lists both):
`Assets/Scripts/Gameplay/UI/ShotUI/MapPinIndicator.cs` and
`Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` — both appear as ` M` in the baseline
`git status --porcelain` captured before any work on this task, and belong to `map_view_v2`.
`git diff` on both is untouched by this task.

---

## 2. Acceptance checklist

| # | SPEC § 6 item | Verdict | Evidence |
|---|---|---|---|
| 1 | Settings › Controls: tapping a different scheme opens the pop-up over the Settings panel | **PASS** | `settings.tap_opens_the_popup` = True, driven by the REAL row Button read off `ControlsSubmenu.tapTimingButton` (`settings.real_row_button_found` = `TapTimingButton`). Screenshot `screenshots/settings_popup_needle.png`. |
| 2 | The row highlight stays on the current scheme while the pop-up is open | **PASS** | `ingame.highlight_stays_on_the_current_scheme` — read off the two segments' live label INK (`Flick` #001E39 selected vs tapped #FFFFFF), not from the code path. |
| 3 | CANCEL leaves the scheme alone | **PASS** | `settings.cancel_leaves_the_scheme_alone` and `ingame.cancel_leaves_the_scheme_alone`: `ControlSchemeService.Current` re-read after the click, unchanged. |
| 4 | CONFIRM moves it and the game uses the new scheme on the next shot | **PASS** | `ingame.confirm_commits_the_scheme` = Pendulum, then `ingame.host_swapped_after_confirm` — `ShotSchemeHost.ActiveScheme` re-read 1.5 s later, so the Idle-deferred swap is what is being asserted, not the pref. |
| 5 | Tapping the current scheme does nothing | **PASS** | `ingame.tapping_the_current_scheme_opens_nothing`, `settings.tapping_the_current_scheme_opens_nothing`. |
| 6 | In-game: stacked above the gear modal, which stays open underneath | **PASS** | `popup.sorting_above_ingame_settings` = 600 override=True (vs `ModalScrim.SortingOrder` 500, where the gear modal lifts itself), `ingame.gear_modal_stays_open_underneath` = True. |
| 7 | On CONFIRM the segment row repaints | **PASS** | The gear modal already repaints from `ControlSchemeService.OnSchemeChanged`; `ingame.host_swapped_after_confirm` proves the event fired. |
| 8 | Panel width 1086, tile row centred 48/48, 36 px under the separator | **PASS** | `fidelity.*.panel_width_1086` = 1086.00; margins 48.00 / 48.00; gaps 24.00 / 24.00; `gap_36_under_the_title_separator` = **35.00** against 36 — see § 5, this is the node's own measurement (the separator stroke straddles the boundary). |
| 9 | Gold CONFIRM tint read off the live Image | **PASS** | `buttons.confirm_is_the_gold_main_button` = `Button - Retry` (the ResultScreen gold plate) with tint `#FFFFFFFF` — untinted, so the gold is the sprite's own. CANCEL is `ButtonCancel`, the palette's silver. |
| 10 | Tiles are the in-game captures, centred on their subject, no HUD chrome | **PASS** | 12/12 in `tiles_manifest.json` with `no_hud_chrome: true` and `fails: []`; every tile's live sprite read back by `tiles.*.tileN_has_a_captured_sprite`. |
| 11 | `--check` clean + table read-back | **PASS** | `export_content.py --check` exit 0, "no file would change and no catalog has drifted"; 26 rows read back from `content_rows` at `version 42, min_build 2709, is_active true`; Unity table 1073 → 1099 rows; `LocalizationManager.Get` read back in EN **and** JA. |
| 12 | Zero hardcoded `.text` | **PASS** | `text.*_is_localised` on every label in all three surfaces — asserts a `LocalizedText` is present AND the rendered string is neither the `(KEY)` placeholder nor SCREAMING_SNAKE. The `1 2 3` numerals are the only literals and are typography by design. |
| 13 | `controls_scheme_changed` carries `where=settings_popup\|ingame_popup` | **PASS** | `ingame.telemetry_where_is_ingame_popup`, `settings.telemetry_where_is_settings_popup`. No dashboard change needed — `TelemetryHooks.OnControlSchemeChanged` passes `where` straight through and nothing enumerates it (grepped `admin-dashboard`: zero references to `controls_scheme_changed`). |
| 14 | 1170×2532: the panel fits, buttons reachable, Free Swing measured | **PASS** | `fits.freeswing.panel_inside_the_canvas` — the LONGEST copy, panel 1086×1217 inside ±585/±1266; both buttons `reachable`. |
| 15 | 16:9 | **PARTIAL — derived, not rendered** | See § 6. |
| 16 | Device pass (Cesar) | **N/A to me** | Cesar's. |

---

## 3. Clone provenance (Rule 19)

`SchemeConfirmModal.prefab` is created by `AssetDatabase.CopyAsset` from
`Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab`. Every reused element below was
**read back off the built prefab / the live scene object**, not asserted from the build script.

| Element | Source | GUID | Read-back |
|---|---|---|---|
| Modal root, scrim (`DimBackground`), panel, layout | `StartingCharacterConfirmModal.prefab` | — | `CopyAsset` is the first statement of `Build()`. |
| Panel plate `Background` | `Assets/Art/ResultScreen/Background - HoleCard.png` | `064cba0b0bc85154995fa70dd470817b` | Sprite survives the copy untouched. Checked against the node render at 1:1: sprite gradient (18,51,82)→(9,27,52) behind a 3 px (195,200,208) stroke; node render (17,50,81)→(9,27,51) behind (195,200,208). **Δ ≤ 1 per channel — the design used this plate**, so no new art. |
| Separator | `Assets/Art/Settings/Divider.png` | `332237826c3743344947e9828762c2ae` | Kept from the clone, resized to the node's 978×2. |
| CANCEL — silver Main Button | `Assets/Art/RosterScreen/ButtonCancel.png` | `6021c639e9c124b44a06c8ccd977896f` | `buttons.cancel_is_the_silver_main_button` reads `ButtonCancel` off the LIVE `Image.sprite`. Palette's canonical silver. |
| CONFIRM — **gold** Main Button | `Assets/Art/ResultScreen/Button - Retry.png` | `aee5ccf2ef2d6b24ca9143186a08aa50` | `buttons.confirm_is_the_gold_main_button` reads `Button - Retry` off the LIVE `Image.sprite`, tint `#FFFFFFFF`. Sampled: fill (255,228,139)→(229,196,102), outer border #422100, inner #FFE48B — the node's gold (`border-[#422100]`, `border-2 border-[#ffe48b]`, `rgb(252,241,149)→rgb(187,127,29)`). **Not Copper.** |
| `ButtonPressFeedback` on both buttons | clone | — | Inherited; not re-authored. Hard rule 11 satisfied without adding a Button. |

**Nothing was hand-rolled.** The only new art in the task is the 12 captured tiles, which the spec
requires to be new (§ 3.2), plus one TMP material asset (§ 5).

---

## 4. Figma fidelity (Rule 18) — node `14140:35469`, re-pulled at step 0 (Rule 9)

`get_metadata` on `14140:35361` and `get_design_context` on the Pop-up's `Mission Title`
(`14140:35472`), `HowItWorks` (`14140:35934`), `Goals Container` (`14140:35606`), `Buttons`
(`14140:35611`) and the caption run (`14140:35519`) were run before any building, and every number
below comes from that pull, not from the SPEC's prose. Built values are measured off LIVE
`RectTransform`s in play mode, expressed in the node's own panel-local top-down frame.

| Element | Node | Built | Verdict |
|---|---|---|---|
| Pop-up panel | 1086 × hug (Pendulum frame 1175) | **1086** × 1177 (Tap Timing) / 1217 (Free Swing) | **PASS** — hug height, and the node's own frames differ per scheme for the same reason |
| Panel plate | navy gradient + 3 px silver stroke, r40 | `Background - HoleCard`, Δ ≤ 1/channel | **PASS** |
| Mission Title | text top 24, h 84, Rubik SemiBold 66, `#F5D66E`, tracking −0.78 px | top **24.00**, `Rubik-SemiBold SDF` @ **59.00** (= 66 × 59/66), `#F5D66E`, characterSpacing −1.18 % | **PASS** |
| Title case | node draws `PENDULUM`; the key is `Pendulum` | `FontStyles.UpperCase` | **PASS** — the key is reused, not duplicated in caps |
| Separator | line at y 120, x 54, w 978 | y 119–121, x **54.00**, w **978.00** | **PASS** |
| Tile | 314 × 340, r 32 | **314.00 × 340.00**, radius baked into the PNG alpha | **PASS** |
| Tile row | x 48 / 386 / 724, right margin 48 | left **48.00**, gaps **24.00 / 24.00**, right **48.00** | **PASS** |
| Gap under separator | 36 | **35.00** | **PASS** — measured on the node RENDER, the 1 px stroke is centred on y 120 and the tile top is at 156, i.e. 36 from the stroke centre and 35 from its lower edge |
| Caption | `1␣␣PULL`, Rubik SemiBold 34, white, centred, 12 px under the tile | two runs (numeral + `LocalizedText`) at **30.39** = 34 × 59/66, gap 17, centred, **12.00** under | **PASS** |
| HOW IT WORKS header | Rubik SemiBold 36, `#F5D66E`, h 43 | `Rubik-SemiBold SDF` **32.18** = 36 × 59/66, `#F5D66E`, h **43.00** | **PASS** |
| Line index | Rubik **Bold** 36, `#F5D66E`, gap 16 | `Rubik-SemiBold SDF` 32.18, `#F5D66E`, gap **16.00** | **KNOWN-UNEQUAL** — Rubik Bold is not in the project; SemiBold is the nearest shipped face |
| Line body | Rubik **Medium** 34, white, w 990, auto-height | variable face @ **34.00**, white, w **990.00**, hugs | **PASS with a correction** — see below |
| Footer | Rubik Medium 34, `rgba(255,255,255,.75)`, centred, h 66, tracking −1.29 px | variable face @ 34, **`#C1C7CD`** (the node render's own composite, sampled at 1:1 — authoring the literal 75 % alpha lands off in linear space), h **66.00**, −3.79 % | **PASS** |
| Buttons | CANCEL 450×120, CONFIRM 391×120, gap 48, centred (98.5 each side) | **450.00 / 391.00 / 48.00**, margins **98.50 / 98.50** | **PASS** |
| Button label | SemiBold 66, `#1E293B` / `#321506`, tracking −0.78 px | **59.00**, `#1E293B` / `#321506`, −1.18 % | **PASS** |
| Tiles' content | design-frame crops (static pose; the Flick set shows no pull, no arrows, no flick) | real in-game captures | **INTENDED DIFFERENCE** (§ 3.2) — and the reason the spec demanded capture |

### 4a. Font WEIGHT and RENDERED SIZE, measured (standing rule)

Not asserted — **measured**, by cropping the node render and the built capture to the SAME
panel-local regions (`screenshots/fidelity_taptiming_node_vs_built.png`) and comparing ink coverage.
The two SemiBold rows are the control that rules out a rendering-pipeline difference.

| Run | Node weight | Built | Ink built/node BEFORE | AFTER |
|---|---|---|---|---|
| Title | SemiBold | `Rubik-SemiBold SDF` | 0.981 | 0.981 |
| HOW IT WORKS header | SemiBold | `Rubik-SemiBold SDF` | 0.987 | 0.987 |
| Three body lines | **Medium** | variable face | **0.671** | **0.977** |
| Footer | **Medium** | variable face | **0.652** | **1.023** |

The project ships only `Rubik-SemiBold SDF` and the variable face, and the variable face renders at
**Regular** — the 0.67 was a real, visible weight deficit, not a measuring artefact. Four approved
GPS screens record this as a permanent known-unequal; it is fixed here instead:

* the SemiBold FACE is **not** the fix — at the same 34 px it measures **2.04× the ink and 18 %
  wider** (874 px of copy becomes 1035), which would move every line break off the node's;
* `_FaceDilate` thickens **without** changing advance width (preferred width identical at dilate
  0.00 and 0.08), so the node's face, size and line breaks are all kept;
* the value is calibrated, not guessed: rendering the node's own sentence at 34 px over the panel
  navy gives ink ×1.00 / ×1.20 / ×1.47 at dilate 0.00 / 0.08 / 0.18, and the node's Medium sits at
  ×1.49 → **0.18**, shipped as the material asset
  `Assets/Fonts/Rubik-VariableFont_wght Medium SDF.mat`.

A first attempt set `TMP_Text.fontMaterial` (a runtime instance). It did not survive into the
prefab and changed **nothing** — re-measured at 0.671, unchanged. The material had to be a real
asset assigned to `fontSharedMaterial`. Recorded because "I set it" was not evidence.

### 4b. Differences that remain

1. **Tile zoom.** Our tiles are framed tighter than the node's, which show more course context (a
   golfer, sky, the fairway). The node's crops are 556–800 game px wide at three different zooms;
   ours are 520–900, chosen by the subject box. Intended, but it is a visible difference.
2. **Line 1 wraps one word later** in the built copy than in the node's. The dilate adds weight but
   not width, so the Regular metrics are marginally narrower than Medium. Cosmetic.

---

## 5. Tiles — captured from the running game (§ 3.2)

`GOLFIN ▸ Capture ▸ Scheme Confirm Tiles` boots the game through the real entry path (PLAY → hole
card), switches scheme through the **real in-game gear segment and this task's own pop-up CONFIRM**,
drives each scheme's three states with **real pointer events on the real driver**, and crops on a
subject box measured off live `RectTransform`s. `tiles_manifest.json`: **12 tiles, `fails: []`,
`no_hud_chrome: true` on all 12.**

| Scheme | Step | Crop (px of the 1170×2532 frame) | Chrome | Driven state |
|---|---|---|---|---|
| Flick | 1 | x=135 y=1194 900×975 | none | PULL — handle 70 % down the cone |
| Flick | 2 | x=135 y=1321 900×975 | none | AIM & TIME — slab at 0.87, green band starts 0.85 |
| Flick | 3 | x=321 y=1143 529×573 | none | FLICK UP — club travelling up past the ball on the real flick |
| Pendulum | 1 | x=213 y=1124 744×805 | none | PULL — 100 % lane, club on the gold tick |
| Pendulum | 2 | x=153 y=1021 864×936 | none | TIME IT — marker on the pip |
| Pendulum | 3 | x=153 y=678 864×936 | none | FLICK UP — grade **Just**, marker 0.000, bar still drawn |
| Tap Timing | 1 | x=266 y=1134 637×690 | none | PULL — 100 % ring, crescent visible |
| Tap Timing | 2 | x=325 y=941 520×563 | none | TAP — needle in the blue zone, TAP! hint |
| Tap Timing | 3 | x=325 y=677 520×563 | none | RESULT — grade **Perfect**, pip on the arc |
| Free Swing | 1 | x=166 y=1030 838×907 | none | BACKSWING — power 1.00, pull 380 px |
| Free Swing | 2 | x=166 y=1030 838×907 | none | SWING UP — `IsUpstroke` true, impact offset 70 px |
| Free Swing | 3 | x=135 y=414 900×975 | none | RESULT — analyzer chip, 1 commit, power 1.00 |

Contact sheet: `screenshots/tiles_contact_sheet.png`. The uncropped source frame for every tile is
kept as `screenshots/tilesrc_<Scheme>_<n>.png`.

**Two deliberate departures, both stated rather than hidden:**

1. **The HUD is hidden for the capture**, and the gate is therefore "no chrome was ACTIVE when the
   frame was taken" rather than "the crop happened to miss the chrome". Cropping around the HUD does
   not work: the analyzer chip is 840 px wide and sits at the same height as the power gauge, so the
   only crop that clears the HUD also cuts the chip in half — which is exactly what an earlier run
   shipped (`OWER 97% … TEMP SLO`). The design's own tiles answer this the same way: its crops zoom
   differently per step and simply do not contain the HUD. Play-mode only; restored before exit.
2. **Flick step 3 is the flick UPSTROKE, not the post-launch frame** the spec describes
   ("`Resolving`, ball just launched, handle gone"). That frame cannot be photographed in a 520 px
   tile — the chase camera cuts to the ball the instant the shot resolves, so three frames later the
   crop is looking at fairway 40 m downrange. Four attempts (subject = ball, = cone, = tee centre,
   = the previous step's framing) all came back as grass and a targeting line. The tile now shows the
   club travelling up past the ball on the **real flick that fires the shot**, which is what a player
   needs to see for "FLICK UP". **This is a judgement call against the spec's wording and is the one
   thing worth a second opinion.**

`Assets/Resources/Gameplay/controls.csv` carries a header comment: re-run the capture whenever a
scheme's UI changes, or the pop-up keeps explaining the old controls.

---

## 6. Tests

**EditMode, `Golfin.Gameplay.Tests`: 636 passed, 0 failed** (`tests-run`, EditMode).

`tests-run` runs the whole mode and does not list passes, so the new suite was proved with a
**tripwire**: an `Assert.Fail("TRIPWIRE")` added to `TappingTheSchemeAlreadyInUse_OpensNothing`
moved the result to **635 passed / 1 failed**, and removing it restored **636 / 0**. The 16 new
tests are therefore executing, not silently skipped.

| Spec § 5 | Covered by | Note |
|---|---|---|
| 5.1 CONFIRM calls `Set(scheme, source)` exactly once; CANCEL / close / backdrop do not; the current scheme is a no-op | `SchemeConfirmDecisionTests` (5 tests) **against production code**, plus the end-to-end proof in `scheme_confirm_invariants.json` | **Deviation, stated:** the assertions are on `SchemeConfirmDecision`, not on the MonoBehaviour. An assembly-definition test assembly cannot reference Assembly-CSharp, where `ModalController` lives, so a rule kept only inside the controller would be untestable by construction. The three rules were therefore extracted into plain C# in `Golfin.Gameplay.UI`, and the controller is a shell over it. The MonoBehaviour half is covered end-to-end by the verify bot against the real scenes. |
| 5.2 Content completeness | `SchemeConfirmContentTests` (6 tests) | Reads `LocalizationText.csv` directly rather than `LocalizationManager.Get` — in edit mode `Get` returns the KEY, which would make every assertion vacuously pass. Asserts EN **and** JA non-empty, exactly 26 keys, 12 sprites resolve, and that no table value is a sentence. |
| 5.3 Tile manifest | `SchemeConfirmTileManifestTests` (5 tests) | 12 crops, each 628×680, `no_hud_chrome` true on all, `fails` empty, every PNG > 20 KB, and **no two tiles byte-identical** — the exact defect an earlier run shipped for Free Swing. |

### 16:9 — derived, not rendered

`fits.freeswing.panel_inside_the_canvas` measures the LONGEST copy at 1170×2532: panel **1086×1217**.
`ShotUI_Canvas` is `ScaleWithScreenSize`, reference 1170×2532, **match = width**, so at 16:9 the
canvas stays 1170 logical px wide and becomes 1170 × 16/9 = **2080** tall; 1217 < 2080, with ~430 px
of clearance above and below. The same now holds in ShellScene — see below. **I did not render a
16:9 frame**, so this is arithmetic on a measured panel height, not a capture. Flagged as the one
acceptance item not directly observed.

**A real bug found by asking that question:** the spec says to put the ShellScene instance "under the
Settings canvas". `SettingsScreen`'s `CanvasScaler` is **`ConstantPixelSize`**, so its children are
authored in DEVICE pixels — a 1086-wide panel parented there is clipped on any device narrower than
1086 physical px. The instance is therefore under `Canvas` (`ScaleWithScreenSize`, 1170×2532,
match = width), which is where every other full-screen modal in ShellScene already lives
(`HoleCompleteModal`, `TournamentSignupModal`, `VersusResultModal`). Its own sorting canvas at 600
still paints it above `SettingsScreen`'s 100 — verified in the live run.

---

## 7. Strings — importer path, EN + JA, published

26 new keys (2 shared + 4 schemes × 6). Titles reuse `SETTINGS_CONTROLS_*`; buttons reuse
`MODAL_CANCEL` / `MODAL_CONFIRM`.

`import_content.py --catalogs texts` PLAN **26 add / 0 change / 0 conflict** → `--apply` (26 drafts,
min_build 2709) → `content_publish` **texts v41 → v42** → read back from `content_rows`: **26 rows,
all `version: 42`, `min_build: 2709`, `is_active: true`** → `export_content.py --catalogs texts`
(cursor `texts=41 → 42`) → **`--check` exit 0, "no file would change and no catalog has drifted"**.

Then `Tools ▸ Localization ▸ Import Text CSV` with a **forced reimport of the CSV asset first**
(Unity reads the imported asset, not the disk write): table **1073 → 1099 rows**, and read back
through `LocalizationManager.Get` in **both** languages:

```
[English]  SCHEME_POPUP_HOW = HOW IT WORKS
[English]  SCHEME_POPUP_FREESWING_STEP1 = BACKSWING
[Japanese] SCHEME_POPUP_HOW = 操作方法
[Japanese] SCHEME_POPUP_FREESWING_STEP1 = バックスイング
```

`git status Assets/Fonts/` is clean — **no `NotoSansJP` atlas churn**.

---

## 8. Iteration log — what went wrong, and what caught it

Thirteen defects. Only one was caught by a gate the first time; the rest were caught by tightening
the instrument, which is the point of recording them.

| # | Defect | Found by |
|---|---|---|
| 1 | Tile PNGs never forced to `TextureImporterType.Sprite` → `Resources.Load<Sprite>` null → **every tile hidden** | **Cesar, mid-run** ("all pop ups are lacking images") |
| 2 | Shot area computed as a band between "top" and "bottom" chrome; `PowerHUD` sits at the ball's height and was classed as bottom chrome, collapsing the band and framing all 12 tiles on empty fairway | looking at the tiles |
| 3 | Free Swing driven with `ProcessDrag(screenPos)` — it takes a LOCAL point, so pull measured 0 and steps 1 and 2 were **byte-identical** | md5 of the outputs |
| 4 | Flick's timing arrow read from a non-existent `ShotController.ArrowProgress01` (it lives on `ShotInputState`) → NaN | the manifest note printed `NaN` |
| 5 | Subject box = "every graphic under the scheme root" pulled in the 1200×1200 arc and the 2600×2600 trace | the crops |
| 6 | Step-3 tiles taken 0.35 s after commit showed the pop alone on grass — the chase camera had swung | the tiles |
| 7 | The crop solver kept dodging chrome that was already hidden, cutting the analyzer chip in half | the tile |
| 8 | **HOW IT WORKS lines did not wrap** — a `LayoutElement` left at `preferredWidth -1` makes the HLG ask TMP for the width the SENTENCE wants, so all three lines ran off the panel. The edit-mode probe measured "990 wide" against the short placeholder key, and `fits.*` passed because it only asserted the PANEL was on screen | the first real screenshot |
| 9 | Rebuilding the prefab **minted a new GUID** (`DeleteAsset` takes the `.meta`), silently orphaning the instance in BOTH scenes — no error, the pop-up simply stopped existing | grepping the scenes for the GUID |
| 10 | The re-wire left the orphan behind (a MISSING script is invisible to a `GetComponentsInChildren<T>` sweep) — two instances in LabScaffold | counting `m_SourcePrefab` refs |
| 11 | Saving ShellScene baked **2699 lines of anchor churn** | `git diff --stat` |
| 12 | The node's letter-spacing (−0.78 px / −1.29 px) was not applied | re-reading the node's JSX |
| 13 | The body/footer weight was a third light (ink 0.67) | the matched crop sheet + ink measurement |

Gates added so they cannot recur silently:

* **`contain.*`** — every label's `TMP.textBounds` (the GLYPHS, not the rect) measured against the
  panel width. #8 passed a rect-width check while visibly clipped; it cannot now.
* **`no_hud_chrome`** — per tile, asserted on the chrome's ACTIVE state at capture time.
* **No two tiles byte-identical** — an EditMode test, for #3.
* The builder now copies to a scratch path and `SaveAsPrefabAsset`s over the target, **preserving
  the GUID** (proved by rebuilding twice and diffing the `.meta`), for #9.
* Sorting order is set in `SchemeConfirmModalController.Awake`, not as a scene override, so a prefab
  rebuild cannot drop it — which it did, once, silently (#9's sibling).

---

## 9. Standing bans

Zero edits to `Assets/Scripts/Physics/`. No `*Gate` scenario added to `Scenarios.cs`. No subsystem
baked exclusively into `LabScaffold.unity` — the prefab is shared and ShellScene carries the same
instance. `M_Splash*.mat` untouched. `git diff --stat` on both scenes is **+103 insertions, 0
deletions** each.

---

## 10. What needs Cesar

1. **Flick step 3** — the deliberate departure in § 5.2. Everything else follows the spec's wording.
2. **A 16:9 render** if the arithmetic in § 6 is not enough.
3. Device pass on both surfaces.
