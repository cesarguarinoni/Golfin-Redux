# IMPLEMENTER_REPORT — `screen_hints`

**Iteration shape:** `screen_hints:first-entry-modal`
**Iteration:** iter-1
**Canonical screenshot:** `screenshots/roster_hint_1of4.png` (1170×2532, real play — boot → Splash StartButton → nav-bar Roster; CaptureCore sidecar `realPlay: true`)
**Baseline:** `HEARTBEAT.log` — HEAD `b5d98857d`, DIRTY block lists the other sessions' uncommitted docs at kickoff (quoted per file below).

---

## Implementation summary

The first entry into each screen with a row in `ScreenHints.csv` opens that screen's Loading tips as
a modal, one at a time: `PRO TIP`, the tip's diagram and text, an `n/X` counter when there is more
than one, a gold CONTINUE that reads CLOSE on the last (or only) hint, and a silver BACK from hint 2
onwards that is simply not there on hint 1. `ScreenHintPresenter` on `PersistentUI` listens to
`ScreenManager.ScreenChanged`; the shot view (`GameplaySceneLoader` step 7) and Settings › Controls
(`ControlsSubmenu.OnEnable`) call one static. `ScreenHintResolver` is pure and drops inactive tips
and keys another screen already showed; `ScreenHintStore` keeps `screenhints.state` in PlayerPrefs
per device. The modal prefab is a `CopyAsset` of `SchemeConfirmModal.prefab` whose content is the
loading card's own `TipContent` subtree, instantiated into the plate.

Verified end to end by an Editor bot (`GOLFIN ▸ Hints ▸ Run verify bot`) that boots the real app,
taps real widgets, presses the modal's own buttons and writes every observation to
`screenshots/verify_en.log` / `verify_ja.log`.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Resources/Data/ScreenHints.csv` | created — 36 rows / 18 screens, exactly the §2.2 table |
| `Assets/Resources/Data/ScreenHints.csv.meta` | created — TextAsset importer meta |
| `Assets/Scripts/UI/Hints.meta` | created — folder meta |
| `Assets/Scripts/UI/Hints/Editor.meta` | created — folder meta |
| `Assets/Scripts/UI/Hints/ScreenHintCatalog.cs` | created — `ScreenHint` struct, `Load`/`LoadFromResources`/`Parse` (`#` comments, header, malformed rows warn + drop), `GameplayScreen` / `SettingsControlsScreen`, `IdFor` |
| `Assets/Scripts/UI/Hints/ScreenHintCatalog.cs.meta` | created |
| `Assets/Scripts/UI/Hints/ScreenHintResolver.cs` | created — pure `HintsFor(screen, hints, tips, state)` |
| `Assets/Scripts/UI/Hints/ScreenHintResolver.cs.meta` | created |
| `Assets/Scripts/UI/Hints/ScreenHintStore.cs` | created — `screenhints.state` PlayerPrefs JSON, `Load`/`Save`/`Clear`/`With`, corrupt → default |
| `Assets/Scripts/UI/Hints/ScreenHintStore.cs.meta` | created |
| `Assets/Scripts/UI/Hints/ScreenHintModalController.cs` | created — `ModalController` subclass: `Show(hints, onFinished, onHintShown)`, `Bind(n)`, ProTipCard's swap + height tween copied, counter Bump, swap guard, two-scene `Instance`, sorting 600 |
| `Assets/Scripts/UI/Hints/ScreenHintModalController.cs.meta` | created |
| `Assets/Scripts/UI/Hints/ScreenHintPresenter.cs` | created — on `PersistentUI`; `ScreenChanged` + `NotifyScreenEntered`; one-frame wait, `ModalStackEmptied` wait, cancel on a different screen, tee-glow restart on Gameplay CLOSE |
| `Assets/Scripts/UI/Hints/ScreenHintPresenter.cs.meta` | created |
| `Assets/Scripts/UI/Hints/Editor/ScreenHintModalBuilder.cs` | created — `GOLFIN ▸ Build ▸ Screen Hint Modal`: CopyAsset of SchemeConfirmModal → scratch → populate → save over target (GUID kept) |
| `Assets/Scripts/UI/Hints/Editor/ScreenHintModalBuilder.cs.meta` | created |
| `Assets/Scripts/UI/Hints/Editor/ScreenHintMenu.cs` | created — `GOLFIN ▸ Hints ▸ Reset seen` and `Reset loading tip position` |
| `Assets/Scripts/UI/Hints/Editor/ScreenHintMenu.cs.meta` | created |
| `Assets/Scripts/UI/Hints/Editor/ScreenHintVerifyBot.cs` | created — the acceptance driver (EN / JA), logs + captures under `screenshots/` |
| `Assets/Scripts/UI/Hints/Editor/ScreenHintVerifyBot.cs.meta` | created |
| `Assets/Prefabs/UI/Modals/ScreenHintModal.prefab` | created by the builder — GUID `954144599248046619417f0a9ed85cc2`, 34-entry `tipSprites`, `animateShow: 1` |
| `Assets/Prefabs/UI/Modals/ScreenHintModal.prefab.meta` | created by the builder |
| `Assets/Scenes/ShellScene.unity` | modified — prefab instance under `Canvas` right after `SchemeConfirmModal` (index 10 of 12); `ScreenHintPresenter` on `PersistentUI` with both CSVs wired. Committed content = HEAD + those two inserts only (118 lines); the working tree additionally carries this session's save drift (auto-size `m_fontSize`, prefab-override `value:` lines), left unstaged per `reference_isolate_scene_save_drift_partial_stage` |
| `Assets/Scenes/Physics/LabScaffold.unity` | modified — prefab instance under `LabRoot/ShotUI_Canvas` at index 20, directly after `SchemeConfirmModal` (19) and `InGameSettingsModal` (18). Same staging treatment (103 clean lines committed; drift unstaged) |
| `Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` | modified — one static call at LoadCoroutine step 7, after `FinishLoadingCoroutine` + `TeeIdleGlowController.NotifyOtherInteraction()` |
| `Assets/Scripts/UI/ControlsSubmenu.cs` | modified — one static call at the end of `OnEnable` |
| `Assets/Tests/EditMode/ScreenHintCatalogTests.cs` | created — 7 tests + the shared `Hints` reflection helper |
| `Assets/Tests/EditMode/ScreenHintCatalogTests.cs.meta` | created |
| `Assets/Tests/EditMode/ScreenHintResolverTests.cs` | created — 10 tests on the shipped catalogs + a flipped-`TIP_STORE` fixture |
| `Assets/Tests/EditMode/ScreenHintResolverTests.cs.meta` | created |
| `Assets/Tests/EditMode/ScreenHintStoreTests.cs` | created — 6 tests; saves/restores the developer's own key |
| `Assets/Tests/EditMode/ScreenHintStoreTests.cs.meta` | created |
| `Assets/Tests/EditMode/LoadingTipCatalogTests.cs` | modified — sprite-table assertion extracted to `AssertSpriteTable`; new `ScreenHintModalPrefab_CarriesTheSameThirtyFourSprites` |
| `Assets/Localization/LocalizationText.csv` | modified — `HINT_CONTINUE` / `HINT_CLOSE` / `HINT_BACK` added (EN+JA), `TIP_RP` rewritten (EN+JA); no key retired |
| `Assets/Localization/LocalizationTextTable.asset` | modified — bundled floor rebuilt (`Tools ▸ Localization ▸ Import Text CSV`, 1205 rows, the four rows verified in the asset) |
| `Assets/Resources/Data/content_version.txt` | modified — `texts=53` → `texts=54` after publish |
| `Docs/Architecture/UI_HIERARCHY.md` | modified — `ScreenHintModal` section (two instances) + `ScreenHintPresenter` |
| `Docs/AI_CONTEXT.md` | modified — session entry |
| `Docs/Architecture/ARCHITECTURE_AUDIT.md` | pre-existing at kickoff — regenerated by session startup; in the HEARTBEAT baseline DIRTY block as `Docs/Architecture/ARCHITECTURE_AUDIT.md`; not staged by this task |
| `Docs/Economy/MONETIZATION_PLAN.md` | pre-existing at kickoff — another session's edit, in the baseline DIRTY block as `Docs/Economy/MONETIZATION_PLAN.md`; untouched, not staged |
| `Docs/Specs/Active/weekly_rotation_admin/SPEC.md` | pre-existing at kickoff — baseline DIRTY block lists `Docs/Specs/Active/weekly_rotation_admin/SPEC.md`; untouched, not staged |
| `Docs/Specs/Active/weekly_rotation_client/SPEC.md` | pre-existing at kickoff — baseline DIRTY block lists `Docs/Specs/Active/weekly_rotation_client/SPEC.md`; untouched, not staged |
| `Docs/TellCode.md` | pre-existing at kickoff — baseline DIRTY block lists `Docs/TellCode.md`; untouched, not staged |
| `Claude outputs/screen_hints_scrim_vs_blur.png` | pre-existing at kickoff — the Architect's scrim-vs-blur mock, baseline DIRTY block lists `Claude outputs/screen_hints_scrim_vs_blur.png`; untouched, not staged |
| `Docs/Specs/Active/economy_telemetry/SPEC.md` | pre-existing at kickoff — another task, baseline DIRTY block lists `Docs/Specs/Active/economy_telemetry/SPEC.md`; untouched, not staged |
| `Docs/Specs/Active/economy_telemetry/STATUS.md` | pre-existing at kickoff — another task, baseline DIRTY block lists `Docs/Specs/Active/economy_telemetry/STATUS.md`; untouched, not staged |
| `Docs/Specs/Active/loans_ops/SPEC.md` | pre-existing at kickoff (then under Queued) — baseline DIRTY block lists `Docs/Specs/Queued/loans_ops/SPEC.md`; another session moved the folder to Active mid-run; untouched, not staged |
| `Docs/Specs/Active/loans_ops/STATUS.md` | pre-existing at kickoff (then under Queued) — baseline DIRTY block lists `Docs/Specs/Queued/loans_ops/STATUS.md`; another session moved the folder to Active mid-run; untouched, not staged |
| `Docs/Specs/Quick/hole_selection_first_card_gap.md` | pre-existing at kickoff — another task's quick spec, baseline DIRTY block lists `Docs/Specs/Quick/hole_selection_first_card_gap.md`; untouched, not staged |

## Screenshot

- **Canonical screenshot:** `screenshots/roster_hint_1of4.png` — Roster, hint 1/4 (TIP_RARITIES), CONTINUE alone, 1170×2532
- **Captured at:** `screenshots/roster_hint_1of4.png` (CaptureCore `SnapPlayModeSafe` from the verify bot, sidecar `roster_hint_1of4.png.json` = `realPlay: true`)
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` (boot → Splash → Home → nav-bar Roster)
- **Play mode:** Yes
- **Hole loaded (if applicable):** n/a for the canonical; `gameplay_hint_1of6.png` was taken over `Hole_02_Geo` (Lomond hole 2 — the hole card the bot's ACTION tap landed on)

Other frames (JPG, same run, each with a `realPlay: true` sidecar): `en_home_hint_1of2.png`, `home_hint_2of2.jpg`,
`home_hint_1of2_after_back.jpg`, `home_after_close.jpg`, `roster_hint_2of4.jpg`, `roster_hint_3of4.jpg`,
`roster_hint_4of4.jpg` (BACK + CLOSE), `roster_hint_3of4_after_back.jpg`, `roster_swap_1to2_f00/02/04.jpg`,
`roster_swap_3to4_f00/02/04.jpg`, `inventory_hint_1of2.jpg`, `inventory_hint_2of2.jpg`,
`generalshop_hint_single.jpg` (CLOSE alone, no counter), `modeselection_hint_1of3.jpg`,
`missionselection_no_hint.jpg`, `leaderboard_hint_waiting_behind_modal.jpg`, `leaderboard_hint_after_stack.jpg`,
`settings_controls_hint.jpg`, `holeselection_hint_1of2.jpg`, `loading_screen_tip_rp_new_copy.jpg`,
`gameplay_hint_1of6.png`, `gameplay_hint_6of6.jpg`, `gameplay_after_close.jpg`, `ja_home_hint_1of2.jpg`,
`ja_home_hint_2of2.jpg`, `ja_roster_hint_1of4.jpg`, `ja_roster_hint_2of4.jpg`.

## Figma fidelity

Reference renders in `reference/` (1170×2532). Measured with a pixel script on the reference and the built
frame (plate stroke rows/columns, gold glyph bboxes); `SB(x)` = `x × 59/66`, the project's SemiBold calibration.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Overlay / scrim | `14263:39325` | black 50 % (sketch value) | the clone's `DimBackground`, black α 0.92 (`#000000EB`), `ModalScrim.Apply` floor 0.80 — spec says keep the copy's | PASS |
| Plate | `14263:39326` | x 42, 1086 wide, opaque navy gradient, silver stroke | `Background - HoleCard` sliced sprite (GUID `064cba0b0bc85154995fa70dd470817b`), α 1; measured stroke columns x 42..1127 = 1086 on every built frame and every reference | PASS |
| Plate height / y | `14263:109627` · `14266:109664` | Roster 1/4 1158 tall, 4/4 1010 tall, vertically centred | Roster 1/4: same stroke-feature span 1013 (ref) vs 1014 (built); 4/4 plate 989 vs node 1010 (−21, the 3-line body's line height — see Body text); centred via `ContentSizeFitter` + centre anchor (plate centre y 1237 vs ref 1227) | PASS* |
| Column | `14263:39327` | gap 24, padding-bottom 32 | Panel VLG bottom 32 (clone), TipContent VLG spacing 24, ButtonsRow top pad 24 (clone) | PASS |
| Title | `14263:39330` | `PRO TIP`, Rubik SemiBold 66, `#EEDC9A`-family gold, centred, row 120 | clone `TitleText`: Rubik-SemiBold SDF 59 = SB(66), `#F5D66E` (the clone's authored gold, node render (245,214,110)), key `TIP_HEADER`; glyph bbox x 466..718 (ref) vs 467..720 (built) — same rendered width, i.e. same size and weight | PASS |
| Counter | `14263:109274` | `1/4`, Rubik SemiBold 45, gold, right-aligned, 64 in / 36 down | clone of TitleText at SB(45) = 40.2, TopRight, anchor/pivot (1,1), anchoredPosition (−64, −36); measured right inset 66 (ref) vs 66 (built), top inset 9 vs 10, glyph height 38 vs 37; hidden when X = 1 (`generalshop_hint_single.jpg`, `settings_controls_hint.jpg`) | PASS |
| Separator | `14263:39331` | 978 wide, 2 thick, y 120 | clone `ModalSeparator` `Divider` 978×2; measured 113 (ref) vs 112 (built) below the plate stroke | PASS |
| Body text | `14263:39335` | Rubik SemiBold 51 / lh 66, white + `#EEDC9A` spans, centred, 990 wide, 12 top | the loading card's own `TipText` (Rubik-SemiBold SDF 45, the card's authored SB(51); white; `<color=#EEDC9A>` spans from the texts row), `LayoutElement` 990, TipContent padding 48/48/12/0; line breaks identical to the node on Home ("…GAME'S / CURRENCY. … HOLE, / MISSION AND ROUND") and Roster ("…BESTOW / …AND MAX / LEVEL"); TMP's default line height is ~7 px/line tighter than the node's 66 → 3-line plates come out ~21 px shorter | PASS* |
| Diagram | `14263:109279` · `14263:109863` · `14263:110032` | 806 wide, height per sprite, centred, 24 above/below | the card's `TipImage`, `preserveAspect`, `LayoutElement` 806; the Tip_RARITIES strip and the Tip_SWING cone render at the node's own pixels (same PNG exports) | PASS |
| Button row | `14263:39343` · `14266:109702` | centred, 120 tall; hint 1 CONTINUE alone, last hint BACK + CLOSE gap 48 | clone `ButtonsRow` HLG spacing 48 MiddleCenter; `roster_hint_1of4.png` gold alone centred (x 361..808), `roster_hint_4of4.jpg` BACK + CLOSE | PASS |
| BACK | `14267:32682` | silver, 428×120, label 66; only from hint 2 | clone `CancelButton` renamed, `ButtonCancel` sprite (`6021c639e9c124b44a06c8ccd977896f`), 450×120 (spec: keep the Unity width), label SB(66), key `HINT_BACK`; `SetActive(n > 1)` | PASS* |
| CONTINUE / CLOSE | `14263:39346` · `14266:109704` | gold, 428×120, label 66 | clone `ConfirmButton` renamed, `Button - Retry` (`aee5ccf2ef2d6b24ca9143186a08aa50`), 450×120 measured 448 wide (ref 424 — the node's hug), key swaps `HINT_CONTINUE` ↔ `HINT_CLOSE` per §2.1 | PASS* |
| Text weight (all runs) | — | SemiBold everywhere | title, counter, body, both labels all `Rubik-SemiBold SDF`; JA frames render the same face | PASS |

`PASS*` = accepted deviation, listed under § Spec deviations.

## Clone provenance

| Element | Cloned / rebound from | Live sprite / asset |
|---|---|---|
| Prefab root, Canvas (600), GraphicRaycaster, `DimBackground` scrim, `Panel` (VLG + ContentSizeFitter) | `AssetDatabase.CopyAsset` of `Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab` (GUID `5938b387de1da42d992bf0226a45d141`) | scrim = the clone's own flat black α 0.92 Image (lint WARN, intentional) |
| `Panel/Background` plate | the clone's `Background` | `Assets/Art/UI/Background - HoleCard.png` sprite GUID `064cba0b0bc85154995fa70dd470817b`, sliced, white α 1 |
| `TitleRow/TitleText` | the clone's `TitleText` (font, material, gold, tracking) | `Assets/Fonts/Rubik-SemiBold SDF.asset`; key re-pointed to `TIP_HEADER` |
| `SeparatorRow/ModalSeparator` | the clone's `ModalSeparator` | `Divider` sprite GUID `332237826c3743344947e9828762c2ae`, 978×2 |
| `Counter` | `Object.Instantiate` of the clone's `TitleText` inside the same prefab | same font asset/material as the title; size SB(45), TopRight |
| `TipContent` + `TipText` + `TipImage` (+ CanvasGroup, LocalizedText) | `Object.Instantiate` of ShellScene `Canvas/ScreensRoot/LoadingScreen/ProTipCard/TipContent` (`Assets/Scenes/ShellScene.unity`) | `Assets/Fonts/Rubik-SemiBold SDF.asset` on TipText; TipImage sprite bound per hint from the table below |
| `ButtonsRow/BackButton` | the clone's `CancelButton` renamed | `ButtonCancel` sprite GUID `6021c639e9c124b44a06c8ccd977896f`, `ButtonPressFeedback` 0.95 / 0.12 from the clone |
| `ButtonsRow/NextButton` | the clone's `ConfirmButton` renamed | `Button - Retry` sprite GUID `aee5ccf2ef2d6b24ca9143186a08aa50`, `ButtonPressFeedback` from the clone |
| `tipSprites` ×34 | `Assets/Art/LoadingScreen/Tip_<KEY>.png` for every `LoadingTips.csv` row | 34/34 non-null on the prefab (`LoadingTipCatalogTests.ScreenHintModalPrefab_CarriesTheSameThirtyFourSprites`) |

Read-back on the built prefab (script-execute dump): `Background` sprite=`Background - HoleCard`, `ModalSeparator` sprite=`Divider`, `BackButton` sprite=`ButtonCancel`, `NextButton` sprite=`Button - Retry`, `tipSprites=34, null sprites=0`.

## UI fidelity lint

`UIFidelityLinter.LintPrefab("Assets/Prefabs/UI/Modals/ScreenHintModal.prefab", null)` (render-health; no node spec.json was generated — see § Spec deviations):

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| `ScreenHintModal.prefab` | `Docs/Diagnostics/_capture/ScreenHintModal_lint.json` | 0 | 1 (`DimBackground` flat-fill — the cloned scrim, no sprite by design) |

## Test evidence

Unity Test Runner, EditMode, whole mode (`tests-run`): **3042 total / 3039 pass / 0 fail / 3 skip** (the 3 skips
are the long-standing `HoleCompleteDriverTests` Stage-C1 skips, unrelated to this task). `ScreenHintResolverTests` re-run filtered:
10/10 pass. New fixtures: `ScreenHintCatalogTests` 7, `ScreenHintResolverTests` 10, `ScreenHintStoreTests` 6,
`LoadingTipCatalogTests` +1 (8). `PressFeedbackCoverageTests`, `ModalPopTests`, `GpsScreenTransitionTests` are in
the same EditMode run (`Golfin.UI.Polish.Tests`) — green with the new prefab.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `ScreenHintCatalogTests` (EditMode): 36 rows / 18 screens; every screen a ScreenId or constant; every key in LoadingTips.csv; (screen, order) unique and contiguous | PASS | 7/7 green in the 3042-test EditMode run: `ShippedCsv_Has36RowsOver18Screens`, `EveryScreen_IsAScreenIdName_OrOneOfTheTwoConstants`, `EveryKey_ExistsInLoadingTips`, `OrdersAreUniqueAndContiguousFromOne_PerScreen`, `ShippedTable_IsTheOneCesarApproved` (row-for-row §2.2), `CommentsAndHeader_AreSkipped_AndAMalformedRowWarnsOnce`, `IdFor_IsTheEnumName` |
| `ScreenHintResolverTests`: Inventory → 2; GeneralShop → 1; seen screen → 0; MissionSelection after ModeSelection → TIP_DAILY only; TIP_STORE flipped → GeneralShop → 2 | PASS | 10/10 green (filtered re-run quoted in § Test evidence): `Inventory_ShowsTwo_RepairIsInactiveAndNotCounted`, `GeneralShop_ShowsGachaAlone_StoreIsInactive`, `ASeenScreen_ShowsNothing_EvenWithUnseenKeys`, `MissionSelection_AfterModeSelection_ShowsDailyOnly`, `FlippingStoreActive_GivesGeneralShopTwo_WithNoCatalogChange` + 5 more |
| `ScreenHintStoreTests`: save → load round-trips both arrays; corrupt blob → default | PASS | 6/6 green: `SaveThenLoad_RoundTripsBothArrays`, `CorruptBlob_LoadsAsDefault_AndWarnsOnce` (LogAssert on the `[ScreenHintStore]` warning), `ABlobWithMissingArrays_LoadsWithEmptyArrays_NotNull`, `Clear_IsAFreshInstall`, `With_AppendsOnce_AndIsPure`, `EmptyStore_IsTheDefault_WithNonNullArrays` |
| `LoadingTipCatalogTests` extended: the prefab's `tipSprites` has 34 entries matching the CSV | PASS | `ScreenHintModalPrefab_CarriesTheSameThirtyFourSprites` green — same `AssertSpriteTable` the ShellScene card gets (34 entries, every name on the CSV, every sprite's file = its name); builder dump confirms `tipSprites=34, null sprites=0` |
| `PressFeedbackCoverageTests` green with the new prefab; `ModalPopTests` / `GpsScreenTransitionTests` green (class default false; `1` on this prefab) | PASS | all three fixtures inside the 3042/0-fail EditMode run; prefab dump `animateShow=True` after the builder re-asserts the flag (it is lost when the clone's controller is replaced); `ModalController.animateShow` field default untouched |
| Editor, cleared PlayerPrefs: Home `1/2` TIP_RP CONTINUE alone → `2/2` TIP_DAILY BACK + CLOSE → BACK → `1/2` CONTINUE alone → CONTINUE → CLOSE closes; second visit shows nothing; state quoted per step | PASS | `verify_en.log`: `HOME hint 1: n=1/2 key=TIP_RP counter='1/2' back=ABSENT gold='CONTINUE'` state `{"seenScreens":["Logo","Splash","Home"],"seenKeys":["TIP_RP"]}` → `HOME hint 2: n=2/2 key=TIP_DAILY back=PRESENT gold='CLOSE'` seenKeys `["TIP_RP","TIP_DAILY"]` → `HOME hint 1 after BACK: n=1/2 back=ABSENT gold='CONTINUE'` (seenKeys unchanged, default e) → CLOSE → `modal hidden=True openModals=0`; the later nav-bar Home tap (t=46 s) opened nothing (`SETTINGS opened: hint visible=False`). Frames `en_home_hint_1of2.png`, `home_hint_2of2.jpg`, `home_hint_1of2_after_back.jpg`, `home_after_close.jpg` |
| Roster `1/4 … 4/4`; plate height eases RARITIES → STATS (strip, before/after quoted); counter bumps both ways; gold label only changes inside the fade seam (3/4 → 4/4 strip) | PASS | plate heights per hint 1137.0 / 1147.6 / 1097.6 / 989.0 px; 60 fps traces in `verify_en.log`: 4→3 eases 989.0 → 1009.5 → 1026.8 → … → 1096.8 over 13 frames (`UiMotion.EntryDur`), alpha 0.00 → 0.30 → 0.53 → … → 1.00 over 8 frames (`FadeDur`), counter scale 1.000 → 1.042 → 1.058 → 1.059 → 1.018 → 1.002 → 1.000 (`Bump` peak 1.06) — the Home 1→2 and 2→1 traces show the same shape forward and back. Seam: in every strip the rebind (counter, key, BACK, gold label) lands on the frame where alpha is 0.00 and re-appears at 0.29–0.30 — `roster_swap_3to4`: f00 alpha 0.00 gold `CONTINUE`, f01 gold `CLOSE` with the new key. Frames `roster_swap_1to2_f00/02/04.jpg`, `roster_swap_3to4_f00/02/04.jpg` (f00 = content at alpha 0) |
| Inventory shows `1/2`, `2/2` (not `/3`); GeneralShop shows TIP_GACHA with CLOSE alone, no BACK, no counter | PASS | `INVENTORY hint 1: n=1/2 key=TIP_CLUBSTATS counter='1/2'`, `hint 2: n=2/2 key=TIP_BALLS gold='CLOSE'`; `GENERALSHOP single hint: n=1/1 key=TIP_GACHA counter=HIDDEN back=ABSENT gold='CLOSE'`. Frames `inventory_hint_1of2.jpg`, `inventory_hint_2of2.jpg`, `generalshop_hint_single.jpg` |
| One tap == one step both ways (log); taps during a swap ignored | PASS | every `TAP NextButton`/`TAP BackButton` line advances the index by exactly one (e.g. `index 0 -> 0 (swap running=True)` then `n=2/2` after the seam); `DOUBLE TAP in one frame: index 1 -> 1` followed by `ROSTER hint 3 after double tap: n=3/4` — two presses, one step; the guard is `_swapping`, never `interactable` |
| MissionSelection entered after ModeSelection shows TIP_DAILY only (or nothing if Home already showed it) — default b | PASS | Home had shown TIP_DAILY and ModeSelection TIP_MISSIONS, so `MISSIONSELECTION: modal visible=False` with `MissionSelection` added to seenScreens (`missionselection_no_hint.jpg`); the DAILY-only case is pinned by `MissionSelection_AfterModeSelection_ShowsDailyOnly` |
| Hole load: the modal opens over the revealed tee, not the loading screen; `1/6 … 6/6`; no pull possible while up; after `6/6` CONTINUE the tee-idle countdown starts from zero; second hole: nothing | PASS | real path Home PLAY → HoleSelection (its own 1/2 hint closed first) → hole card ACTION: `GAMEPLAY: hint visible after 15.4s; loading screen active=False; scenes=ShellScene,LabScaffold,Hole_02_Geo`; hints TIP_SWING…TIP_FORECAST `1/6`…`6/6` (`scene=LabScaffold` instance); `RAYCAST at cone: top=DimBackground under LabRoot sortingOrder=600` (shot input is UI handlers, the scrim is the top raycast hit); `tee-idle timer right after CLOSE: 0.00s`, `1s later: 1.01s`; second entry through the same static (`NotifyScreenEntered(Gameplay)`): `hint visible=False`. Frames `gameplay_hint_1of6.png`, `gameplay_hint_6of6.jpg`, `gameplay_after_close.jpg`. Deviations: hole 2 not hole 1; the second-hole check is the entry-point call, not a second real load (§ Spec deviations) |
| Settings › Controls first open: TIP_CONTROLS, no counter, above the Settings overlay; second open: nothing | PASS | `SETTINGS opened: hint visible=False` (the gear alone opens nothing), Controls row tapped → `SETTINGS CONTROLS hint: n=1/1 key=TIP_CONTROLS counter=HIDDEN back=ABSENT gold='CLOSE'`, `sorting: hint canvas=600 settings canvas=500`; second expand: `hint visible=False`. Frame `settings_controls_hint.jpg` (the modal over the Settings list) |
| Stacking: with a tournament result pending on Home (`TournamentResultPresenter` fixture), the result modal opens first and the hint opens after it closes — order quoted | PASS | **Cesar 2026-09-10: "Hint first."** — the hint opens one frame after the screen (Architect default c) and any modal already up when it arrives is waited out. Proven with a modal open BEFORE the screen change: `STACK: 1.5s after Leaderboard: hint visible=False openModals=1` → `SchemeConfirmModal hidden` → `modal visible for Leaderboard after stack after 0.02s` (`leaderboard_hint_waiting_behind_modal.jpg`, `leaderboard_hint_after_stack.jpg`). With `TournamentResultPresenter` the order is the accepted one: it presents 1.0 s after `ScreenChanged`, finds the hint up, and re-presents on `ModalStackEmptied` after CLOSE — hint first, result second. No tournament-result fixture exists in the repo to run the real result modal |
| JA device language: title, body and CONTINUE render Japanese on two screens | PASS | `verify_ja.log`: Home `gold='続ける'` then `'閉じる'`, Roster `'続ける'`; frames `ja_home_hint_1of2.jpg` (プロのヒント / リワードポイントがゲームの通貨…), `ja_home_hint_2of2.jpg`, `ja_roster_hint_1of4.jpg`, `ja_roster_hint_2of4.jpg` (戻る + 続ける) |
| `export_content.py --check` clean; zero new hardcoded `.text` literals; HINT_* published; TIP_RP rewritten EN + JA and visible on BOTH the loading screen and the Home hint | PASS | importer plan `texts 3 add / 1 change / 1201 same / 0 conflict` → `--apply` → `content_publish` → **texts v54** → `export_content.py --check`: "clean — no file would change, no catalog has drifted"; `content_version.txt` `texts=54`; bundled table rebuilt, 1205 rows, `HINT_CONTINUE/CLOSE/BACK` + new `TIP_RP` read back. `grep -n '\.text\s*=' Assets/Scripts/UI/Hints/*.cs` → 2 hits: the counter digits (`counterText.text = (n+1)+"/"+x`, numbers, no key by spec) and the raw-key fallback copied verbatim from `ProTipCard.Show`; every string goes through `LocalizedText.SetKey`. Loading screen: `LOADING SCREEN: card key=TIP_RP text='…ARE THE GAME'S CURRENCY. EARN THEM ON EVERY HOLE, MISSION AND ROUND'` (`loading_screen_tip_rp_new_copy.jpg`); Home hint `en_home_hint_1of2.png` |
| Unity Console clean through boot → Home → Roster → hole load → Settings › Controls | PASS | `console-get-logs` Error filter over the second EN run and the JA run: no entries; the only errors of the session are the first bot run's own `CaptureScreenshotAsTexture` timing (fixed with `WaitForEndOfFrame`) and its press on an inactive button — both 15:11–15:13, before the runs cited here |
| Spec deviations flagged (incl. the §3.6 camera-drag and 1v1-clock NOTEs) | PASS | § Spec deviations below |

## Known FAIL items

None. The stacking row was a FAIL in the first cut of this report (the spec asserted result-first); Cesar
decided "Hint first" on 2026-09-10, which is what the code already does — no change made.

## Spec deviations

- **Hole loaded for the gameplay check was Lomond hole 2, not hole 1** — the bot taps the hole card the
  HoleSelection carousel presents by default (`ActionButton`), and that was hole 2 on this profile. Nothing in the
  hint path depends on the hole.
- **"Second hole: nothing" proven through the entry point, not a second real hole load.** After CLOSE the bot
  called `ScreenHintPresenter.NotifyScreenEntered(GameplayScreen)` — the exact call `GameplaySceneLoader` step 7
  makes — and no modal opened (`Gameplay` is in `seenScreens`). A second real load would need the hole played out.
- **Non-nav-bar screens were entered with `ScreenManager.ShowScreen`** (GeneralShop, ModeSelection,
  MissionSelection, Leaderboard). Roster / Inventory / Home / Settings / HoleSelection / the hole card / the
  modal's own buttons were real widgets. The presenter listens to `ScreenChanged`, which `ShowScreen` fires the
  same way as any button.
- **Body text line height:** `TipText` is the loading card's copy at its authored 45 px with TMP's default
  line spacing, per §4 "copied from the loading card (same style)". The node's 66 px line height makes its
  3-line plates ~21 px taller (Roster 4/4: node 1010, built 989). Same breaks, same size, same weight; the two
  surfaces stay identical to each other. Flagged rather than adding a line-spacing override the card does not have.
- **Button width 450 (node hug 428)** — as the spec's §4 instructs (the Unity Main Button width).
- **Rule 21 node-spec:** the lint ran render-health only (`fail 0`); `figma_node_to_spec.py` needs a
  `get_metadata` XML + `get_design_context` JSX pull and a name map, and the Figma MCP was not exercised in this
  session. The per-element A/B above was done against the `reference/` renders with pixel measurements instead.
- **§3.6 camera-drag NOTE:** there is no raw-touch aim-camera drag in the shot view — `ChaseCamera` reads no
  input, and all shot input (`ClubHandleDragger`, the scheme drivers, `SelectorDragRouter`) is UI event handlers,
  which the scrim blocks (raycast at the cone: `DimBackground`). The one raw reader, `MapViewController`
  (`Touchscreen.current` / `Mouse.current`), is already gated on `EventSystem.IsPointerOverGameObject`, which
  the scrim satisfies, and its screen cannot be opened under the scrim anyway. No `OpenModalCount` line added.
- **§3.6 1v1-clock NOTE:** no turn clock exists — `grep -rln "TurnClock|turnTimer|ShotClock|turn clock"
  Assets/Scripts` returns nothing and `1v1_match_flow`'s SPEC has no clock section. Nothing runs under the modal;
  Notion 2241 has nothing to pause today.
- **Loading-screen TIP_RP capture:** the loading screen was held (`minLoadingTime` widened at runtime, the
  `LoadingTipsDemoRecorder` instrument) and the real card was tapped through `OnPointerClick` until the
  sequencer reached TIP_RP (24 taps). The card, text and sprite are the shipping ones.
- **Frame strips with captures are slower than 60 fps** (each 1170×2532 PNG encode is ~150 ms), so the snapped
  strips show the swap completing in 1–3 frames; the 60 fps numeric traces (Home 1→2, 2→1, Roster 4→3, snap-free)
  are the motion evidence, the snapped f00/f02/f04 frames the pictures.

## Console output

Second EN run + JA run (15:14–15:19), `console-get-logs` Error filter: empty. Representative Log lines from the feature:

```
[ScreenHint] Home entered for the first time — 2 hint(s); state={"seenScreens":["Logo","Splash","Home"],"seenKeys":[]}
[ScreenHint] bind 1/2 TIP_RP back=off gold=CONTINUE
[Modal] ScreenHintModal shown
[ScreenHint] shown TIP_RP on Home; state={"seenScreens":["Logo","Splash","Home"],"seenKeys":["TIP_RP"]}
[ScreenHint] plate height 879.0 -> 859.0
[ScreenHint] bind 2/2 TIP_DAILY back=on gold=CLOSE
[ScreenHint] CLOSE on 2/2
[Modal] ScreenHintModal hidden
```

## Open questions for Architect

- ~~Hint vs tournament result order~~ — decided by Cesar 2026-09-10: hint first (default c as written).
- `seenScreens` also collects `Logo`, `Splash`, `Loading`, `HoleSelection` etc. — every screen entered is marked
  (spec §3.5 "even when hints is empty"). Harmless, but the blob grows by one entry per screen the player ever
  opens; say if you would rather mark only screens with rows.
- The HoleSelection hints (AUTOCLUB, SURFACES) open on the way to every first hole; the bot found the ACTION
  card only reachable after closing them, which is the intended scrim behaviour — confirming this is what you want
  before the first practice round.
