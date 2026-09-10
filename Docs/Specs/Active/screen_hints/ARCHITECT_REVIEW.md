# ARCHITECT_REVIEW — `screen_hints`

**Reviewer:** golfin-reviewer (iter-1)
**Verdict:** FORWARD_TO_REDTEAM (`READY_FOR_REDTEAM`)
**When:** 2026-09-10 15:50 JST
**Canonical screenshot reviewed:** `screenshots/roster_hint_1of4.png` (1170x2532, real play — CaptureCore sidecar `realPlay: true`)
**Figma reference reviewed:** `reference/figma_14263-109304_roster_hint_1of4.png` (mirrored to `screenshots/figma-reference.png`); companions `reference/figma_14263-109672_home_hint_1of2.png`, `reference/figma_14263-109883_ingame_hint_1of6.png`, `reference/figma_14266-109661_roster_hint_4of4_last.png`

---

## Independent visual scan (Step 0 — pixels first, no prior verdicts)

Portrait 1170x2532 frame with the real Roster screen behind a scrim-dimmed
screen-hint modal. Top bar carries the live R currency chip 6.228, the golf
ticket chip 1.015 with a small plus, and the gear top-right; the ROSTER tab is
active; the character carousel behind shows JAMES unlocked at Lv 16 and five
LOCKED cards at Lv 10 / 120 / 80 / 160 / 80. Sitting over the roster is a
rounded navy plate with a pale silver rim and a soft drop shadow: gold
`PRO TIP` title top-centre, gold `1/4` counter top-right, a thin gold divider
under the title, a 3-line body in white SemiBold — `DIFFERENT RARITIES BESTOW /
DIFFERENT INITIAL STATS AND MAX / LEVEL` — with `RARITIES` and `MAX LEVEL` in
gold, then a stack of six rarity pills (SUPREME purple / LEGENDARY orange /
MYTHIC yellow / RARE green / UNCOMMON blue / COMMON silver) each carrying a
coloured left edge, its coloured name on the left and `MAX LEVEL {N}` in white
on the right; a gold CONTINUE button alone below the stack. No BACK on this
hint. Below the modal the tail of the bio ("scouts agree the foundations are
tour-calibre."), COMPARE / LEND / SELECTED buttons, and the bottom nav (home /
cards / tee / bag / profile) bleed out from under the plate. The scrim dims
the roster without hiding it.

## Bbox verification

The spec makes no explicit "text inside box" containment claim; the modal
plate auto-sizes to its content via the loading card's own `VerticalLayoutGroup`
plus `ContentSizeFitter` (per SPEC.md §4). Every element (title, counter,
divider, body, image, buttons) sits inside the plate on every frame I opened
(`roster_hint_1of4.png`, `roster_hint_4of4.jpg`, `generalshop_hint_single.jpg`,
`settings_controls_hint.jpg`, `gameplay_hint_1of6.png`, `ja_home_hint_2of2.jpg`).
No programmatic bbox check required.

## Scene-mutation audit

```
$ git diff --stat HEAD -- Assets/Scenes/ShellScene.unity Assets/Scenes/Physics/LabScaffold.unity
(empty)
```

Both scenes at HEAD; no drift. Task commit `807c6d1f3` staged the two prefab
instances + the presenter on `PersistentUI` cleanly (118 + 103 lines per report
per the isolate-drift protocol). The five uncommitted `M` docs paths declared
in HEARTBEAT's iter-1 baseline are other-session drift (`ARCHITECTURE_AUDIT.md`,
`MONETIZATION_PLAN.md`, `weekly_rotation_*/SPEC.md`, `TellCode.md`) — none from
this task.

## Rule 21 lint (re-run by me this pass)

`Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab("Assets/Prefabs/UI/Modals/ScreenHintModal.prefab", null)`:

```
UI FIDELITY LINT: Assets/Prefabs/UI/Modals/ScreenHintModal.prefab
[WARN] DimBackground  ::flat-fill::  Image has no sprite — flat #000000EB fill with sharp corners. Verify intended, not a fabricated placeholder.
— 0 FAIL, 1 WARN, 0 INFO —
RESULT: PASS (health)
```

`fail == 0`, warn count `1` (the intentional cloned scrim). Matches the JSON
in `Docs/Diagnostics/_capture/ScreenHintModal_lint.json`.

## Clone provenance read-back (Rule 19, re-run by me this pass)

Live prefab sprite/font dump via `PrefabUtility.LoadPrefabContents`:

```
Background       sprite='Background - HoleCard' color=FFFFFFFF active=True
ModalSeparator   sprite='Divider'                color=FFFFFFFF active=True
BackButton       sprite='ButtonCancel'           color=FFFFFFFF active=True
NextButton       sprite='Button - Retry'         color=FFFFFFFF active=True
DimBackground    sprite='<NONE>'                 color=000000EB active=False
TipText          font=Rubik-SemiBold SDF size=45 weight=Normal
TitleText        font=Rubik-SemiBold SDF size=59 weight=UpperCase
Counter          font=Rubik-SemiBold SDF size=40.22727 weight=Normal
BackButton       LayoutElement pref=(450,120) min=(450,120)
NextButton       LayoutElement pref=(450,120) min=(450,120)
```

Every mandated-reuse element is a real source sprite, not a fabricated flat-fill.
`DimBackground` `<NONE>` is the cloned-verbatim scrim (`SchemeConfirmModal.prefab`
carries the same). `ModalBackdropDismiss` count on `ScreenHintModal.prefab` = **0**
vs source `SchemeConfirmModal.prefab` = **1** — correctly deleted per §3.6
(hardware / gesture back is a no-op; only the modal's own buttons dismiss).
`animateShow: 1` and `m_SortingOrder: 600` both present in the prefab YAML.
Source `AssetDatabase.CopyAsset` GUID chain confirmed: `SchemeConfirmModal`
`5938b387de1da42d992bf0226a45d141` → `ScreenHintModal`
`954144599248046619417f0a9ed85cc2`.

## Figma fidelity

Re-run against `reference/` node renders and against the live nodes referenced
in SPEC.md §4. Each row cites the node id from the spec, the reference /
measured value, and a PASS/FAIL for weight AND rendered size vs the reference
(standing rule, Cesar 2026-07-01).

| Element | Figma node | Reference value | Built value | Weight | Rendered size vs reference | Result |
|---|---|---|---|---|---|---|
| Overlay / scrim | `14263:39325` | black 50% (sketch) | clone `DimBackground` `#000000EB` (a 0.92), `ModalScrim` runtime floor 0.80 — spec: keep clone's | - | - | PASS |
| Plate (width / radius / gradient / stroke / drop shadow) | `14263:39326` | x=42, 1086 wide, r 50, opaque navy gradient, silver stroke 3, shadow | clone `Background` sprite `Background - HoleCard` (GUID `064cba0b0bc85154995fa70dd470817b`), white a 1, sliced; measured stroke columns x 42..1127 = 1086 on the built frame | - | indistinguishable from node render (shape, gradient, stroke, shadow all match) | PASS |
| Plate height (Roster 1/4) | `14263:109627` | 1158 tall, centre y (2532-1158)/2 | Roster 1/4 plateH=1137.0 (log line 64), centred via ContentSizeFitter+centre anchor | - | -21 px vs node (3-line body ~7 px/line tighter — see body row) | PASS* |
| Plate height (Roster 4/4) | `14266:109664` | 1010 tall | Roster 4/4 plateH=989.0 (log line 108) | - | -21 px vs node, same reason | PASS* |
| Column layout | `14263:39327` | vertical, gap 24, padding-bottom 32 | Panel VLG spacing 24 / bottom 32; TipContent VLG spacing 24; ButtonsRow top pad 24 (clone) | - | - | PASS |
| Title `PRO TIP` | `14263:39330` | Rubik SemiBold 66, `#EEDC9A`, centred, row 120 tall (pad-top 24) | clone `TitleText` Rubik-SemiBold SDF 59 (= 66 x 59/66 project SemiBold calibration), gold `#F5D66E` (node render (245,214,110)); key `TIP_HEADER` | SemiBold vs SemiBold — MATCH | glyph cap-height indistinguishable from reference at matched scale; measured width 251 vs 253 px | PASS |
| Counter `1/4` | `14263:109274` | Rubik SemiBold 45, gold, right-aligned, right inset 64 / top 36; hidden when X=1 | new `Counter` TMP cloned from TitleText, Rubik-SemiBold SDF 40.23 (= SB(45)), TopRight anchor pivot (1,1), anchoredPosition (-64,-36); hidden on `generalshop_hint_single.jpg` and `settings_controls_hint.jpg` (`counter=HIDDEN`, log 172, 202) | SemiBold vs SemiBold — MATCH | glyph height 37 vs 38 (ref), right inset 66 vs 66, top inset 10 vs 9 — MATCH | PASS |
| Separator | `14263:39331` | 978 x 2 px, y 120 | clone `ModalSeparator` sprite `Divider` (GUID `332237826c3743344947e9828762c2ae`), 978 x 2, same y (112 vs 113 below plate stroke) | - | - | PASS |
| Body text | `14263:39335` | Rubik SemiBold 51 / lh 66, white + `#EEDC9A` spans, centred, 990 wide, 12 top | loading-card `TipText` Rubik-SemiBold SDF 45 (= SB(51)), same 990 wide, same gold spans from the texts row; line breaks identical to the node on both frames I checked (Roster: "…BESTOW / DIFFERENT INITIAL STATS AND MAX / LEVEL"; Roster 4/4: "…LEVEL UP WITH REWARD POINTS AND / SPEND SKILL POINTS ON THE STATS YOU / WANT. RARITY SETS THE CAPS") | SemiBold vs SemiBold — MATCH | glyph cap-height indistinguishable from reference at matched scale on Roster 1/4 and 4/4; TMP's default line spacing runs ~7 px/line tighter than the node's 66 px lh, so 3-line plates come out ~21 px shorter overall (see plate-height rows). Flagged as accepted deviation. | PASS* |
| Diagram (Roster 1/4 rarity strip) | `14263:109279` | 806 wide, height per sprite, centred | `TipImage` (clone of loading card's), preserveAspect, LayoutElement 806; the Tip_RARITIES PNG is the same asset the node references | - | pixel-identical to reference (both render the same PNG) | PASS |
| Diagram (In-game 1/6 cone) | `14263:110032` | 806 wide, 560 tall, centred | Tip_SWING sprite rendered at LE 806 preserveAspect in `gameplay_hint_1of6.png` | - | pixel-identical to reference | PASS |
| Button row (hint 1 CONTINUE alone) | `14263:39343` | centred, 120 tall, CONTINUE only | HLG MiddleCenter, BACK SetActive(false), gold button alone centred (measured x 361..808 on the canonical) | - | - | PASS |
| Button row (last hint BACK + CLOSE) | `14266:109702` | BACK + CLOSE, gap 48, centred | HLG spacing 48, both buttons visible on `roster_hint_4of4.jpg` and `ja_home_hint_2of2.jpg`; measured pair centred as a pair | - | - | PASS |
| BACK button | `14267:32682` | silver, 428x120 hug, label 66; only from hint 2 | clone `CancelButton` renamed BackButton, `ButtonCancel` sprite (GUID `6021c639e9c124b44a06c8ccd977896f`), LayoutElement preferredWidth=450 x height=120 (spec §4: keep Unity Main Button width); label SB(66), key `HINT_BACK`; SetActive(n>1) — proven by `back=ABSENT` on hint 1, `back=PRESENT` on hint 2, `back=ABSENT` again after BACK-to-hint-1 (log 4, 29, 54) | SemiBold vs SemiBold — MATCH | rendered label matches reference at matched scale; width 450 vs node 428 is explicit spec instruction (Unity Main Button width) | PASS* |
| CONTINUE / CLOSE button | `14263:39346` / `14266:109704` | gold, 428x120 hug, label 66 | clone `ConfirmButton` renamed NextButton, `Button - Retry` sprite (GUID `aee5ccf2ef2d6b24ca9143186a08aa50`), LayoutElement 450 x 120; label key swaps `HINT_CONTINUE` <-> `HINT_CLOSE` per §2.1 (proven by log line 4 `gold='CONTINUE'` at 1/2, line 29 `gold='CLOSE'` at 2/2, `gold='CLOSE'` on single hints) | SemiBold vs SemiBold — MATCH | rendered label matches reference at matched scale; width delta same as BACK | PASS* |
| Text weight (all runs) | - | SemiBold everywhere | title, counter, body, both labels all Rubik-SemiBold SDF (dump above); JA (`ja_home_hint_2of2.jpg`, `ja_roster_hint_1of4.jpg`) renders the same face | - | - | PASS |

`PASS*` = accepted deviation, listed in report § Spec deviations (plate 3-line height ~21 px shorter, button width 450 vs 428 hug per spec instruction).

**Text-gate (font weight AND rendered size, standing rule):** Title, counter, body, BACK, CLOSE — all Rubik-SemiBold SDF (dump reproduced above); every text row's rendered cap-height was A/B'd against the `reference/` render at matched scale (Roster 1/4 canonical, Roster 4/4, In-game 1/6, JA Home 2/2) and matches. No weight or size failures.

## Rule 5 — entire acceptance list, walked fresh

| # | Row | Verdict | Backing evidence I re-verified this pass |
|---|---|---|---|
| 1 | `ScreenHintCatalogTests` (36 rows / 18 screens; screens are ScreenId or one of 2 constants; keys exist in LoadingTips; unique-contiguous orders) | CONFIRM-PASS | I read `Assets/Resources/Data/ScreenHints.csv`: exactly the 36-row / 18-screen §2.2 table Cesar approved (`awk` count = 36); 7 named tests in the 3042-test EditMode run per report |
| 2 | `ScreenHintResolverTests` (Inventory->2, GeneralShop->1, seen->0, MissionSelection-after-ModeSelection->TIP_DAILY only, TIP_STORE flipped->2) | CONFIRM-PASS | behaviour independently visible in log 163 (`INVENTORY hint 1: n=1/2`), 172 (`GENERALSHOP single hint: n=1/1 key=TIP_GACHA`), 185 (`MISSIONSELECTION: modal visible=False` — Home showed TIP_DAILY, ModeSelection showed TIP_MISSIONS, none left); 10 tests named |
| 3 | `ScreenHintStoreTests` (round-trip, corrupt->default, missing-arrays->empty-not-null, Clear, With, empty=default) | CONFIRM-PASS | 6 tests named; runtime state serialised inline in every log line (`state={"seenScreens":[...],"seenKeys":[...]}`) |
| 4 | `LoadingTipCatalogTests` extended (prefab tipSprites=34 CSV rows) | CONFIRM-PASS | test name pinned (`ScreenHintModalPrefab_CarriesTheSameThirtyFourSprites`); report's Clone provenance table lists the 34 wired sprites; live prefab dump above confirms tipSprites 34 / 0 null |
| 5 | `PressFeedbackCoverageTests` / `ModalPopTests` / `GpsScreenTransitionTests` green with new prefab | CONFIRM-PASS | all three in the 3042/0-fail EditMode run; the new `ScreenHintModal.prefab` inherits both `ButtonPressFeedback`s from the `SchemeConfirmModal` clone (source has two; live dump shows BackButton + NextButton with the clone's feedback per report) |
| 6 | Home flow (cleared PlayerPrefs): 1/2 CONTINUE alone -> 2/2 BACK+CLOSE -> BACK -> 1/2 CONTINUE alone -> CONTINUE -> CLOSE closes; second visit shows nothing; state quoted per step | CONFIRM-PASS | verify_en.log 4-60: exact transitions, seenKeys progression `[]` -> `["TIP_RP"]` -> `["TIP_RP","TIP_DAILY"]` (BACK does not decrement — default e); hint after BACK correctly rebinds to `back=ABSENT gold='CONTINUE'`; frames present |
| 7 | Roster 1/4 to 4/4; plate height eases; counter bumps both ways; gold label only changes inside fade seam (3/4 to 4/4 strip) | CONFIRM-PASS | log 64-160+ shows plate heights 1137.0 / 1147.6 / 1097.6 / 989.0; Home swap traces show counterScale 1.000 -> 1.042 -> 1.058 -> 1.060 -> 1.019 -> 1.000 (Bump peak ~1.06) in BOTH directions (`home_swap_1to2` f01-f05 and `home_swap_2to1` f01-f05); alpha ramps 0.00 -> 0.29 -> 0.53 -> ... -> 1.00 across ~8 frames; rebind (key, counter, gold, back) lands on the f01 zero-alpha seam. I visually verified the 4/4 frame carries BACK + CLOSE |
| 8 | Inventory 1/2 + 2/2 (not /3); GeneralShop TIP_GACHA CLOSE alone no counter | CONFIRM-PASS | log 163, 166 confirm Inventory drops TIP_REPAIR (inactive); log 172 confirms `GENERALSHOP single hint: n=1/1 key=TIP_GACHA counter=HIDDEN back=ABSENT gold='CLOSE'`; I opened `generalshop_hint_single.jpg` myself — no counter, CLOSE alone, no BACK |
| 9 | One tap == one step both ways; taps during swap ignored | CONFIRM-PASS | log 102 `DOUBLE TAP in one frame: index 1 -> 1 (expect +1)` proves the swap guard (spec §3.3a — a bool guard, not interactable) |
| 10 | MissionSelection after ModeSelection shows TIP_DAILY only (or nothing) — default b | CONFIRM-PASS | log 185 shows the "nothing" case (Home already showed TIP_DAILY); DAILY-only case pinned by the resolver test |
| 11 | Hole load: hint over the revealed tee not the loading screen; 1/6 to 6/6; no pull; tee-idle restart from zero after CLOSE; second hole nothing | CONFIRM-PASS | log 220 `GAMEPLAY: hint visible after 15.4s; loading screen active=False (expect false) scenes=ShellScene,LabScaffold,Hole_02_Geo`; log 223 `RAYCAST at cone: top=DimBackground under LabRoot sortingOrder=600 openModals=1` proves scrim eats the drag; log 236/238 `tee-idle timer right after CLOSE: 0.00s -> 1s later: 1.01s`. I confirmed the Gameplay 1/6 frame visually — SPIN/STRAIGHT/GOLFIN/DRIVER buttons all under the scrim, dimmed. Deviation flagged: hole 2 not 1 (bot took the default carousel card); second-load proven via the same `NotifyScreenEntered` static the loader step 7 calls, not a full second load — acceptable |
| 12 | Settings > Controls first open: TIP_CONTROLS, no counter, above Settings overlay; second open: nothing | CONFIRM-PASS | log 202-203 `SETTINGS CONTROLS hint: n=1/1 key=TIP_CONTROLS counter=HIDDEN back=ABSENT gold='CLOSE'` + `sorting: hint canvas=600 settings canvas=500`; I opened `settings_controls_hint.jpg` — modal is over the Settings list, dimmed CLOSE button below the plate |
| 13 | Stacking with a pending modal | CONFIRM-PASS by decision | Cesar 2026-09-10: "Hint first" (commit 68cb8dac0); log 187-192 proves the mechanic with a pre-opened SchemeConfirmModal: `hint visible=False openModals=1` -> `SchemeConfirmModal hidden` -> `modal visible for Leaderboard after stack after 0.03s`. Any modal already up when a screen change fires is waited out; TournamentResultPresenter's 1.0s wait + the hint's one-frame wait means the hint arrives first and re-presents behind the result — the accepted order |
| 14 | JA device language: title, body, CONTINUE render Japanese on two screens | CONFIRM-PASS | verify_ja.log: `gold='続ける'` -> `gold='閉じる'`, `back` toggles PRESENT with `戻る` label; I opened `ja_home_hint_2of2.jpg` — `プロのヒント` title, JA body, `戻る` + `閉じる` buttons, `2/2` counter; same Rubik-SemiBold SDF face as EN, same rendered size |
| 15 | export_content.py --check clean; no new hardcoded `.text` literals; HINT_* published; TIP_RP rewritten EN+JA visible on BOTH loading screen and Home hint | CONFIRM-PASS | I read `Assets/Localization/LocalizationText.csv`: `TIP_RP` line 34 rewritten (no "ONLY" / "NEVER FOR SALE" — matches Cesar decision), `HINT_CONTINUE/CLOSE/BACK` lines 1218-1220 EN + JA present; `content_version.txt` texts=54; grep `\.text` in `Assets/Scripts/UI/Hints/*.cs` returns only counter digits and the raw-key fallback copied verbatim from `ProTipCard.Show` (spec-allowed — comment at line 422 says "a wiring bug, not a reason to write .text"), plus `TextAsset.text` reads (not UI); log 218 `LOADING SCREEN: card key=TIP_RP text='<color=#EEDC9A>REWARD POINTS</color> ARE THE GAME'S CURRENCY. EARN THEM ON EVERY HOLE, MISSION AND ROUND'` on the real loading card; `en_home_hint_1of2.png` shows the same copy on the Home hint |
| 16 | Console clean through boot -> Home -> Roster -> hole load -> Settings > Controls | CONFIRM-PASS | Error filter empty for the second EN run and JA run per report; nothing in the acceptance path logs an Error |
| 17 | Spec deviations flagged (incl. §3.6 camera-drag and 1v1-clock NOTEs) | CONFIRM-PASS | Report § Spec deviations covers all six: hole 2 not 1, second entry via static, non-nav screens via `ScreenManager.ShowScreen`, body line height (7 px/line tighter -> 21 px shorter on 3-line plates), 450 vs 428 button width (spec instruction), no node-spec JSON. §3.6 discharged rather than deferred: no raw-touch aim-camera drag (`ChaseCamera` reads no input; all shot input is UI handlers the scrim blocks; the one raw reader `MapViewController` is `EventSystem.IsPointerOverGameObject`-gated and its screen cannot open under the scrim); no 1v1 turn clock exists in the codebase — grep for TurnClock / turnTimer / ShotClock returns nothing |

## PIPELINE_HARDENING gate status

- Rule 2 (real entry): PASS. `ScreenHintPresenter.OnEnable` subscribes to `ScreenManager.ScreenChanged`; `GameplaySceneLoader.LoadCoroutine` line 200 calls `ScreenHintPresenter.NotifyScreenEntered(ScreenHintCatalog.GameplayScreen)` right after `FinishLoadingCoroutine()` and `TeeIdleGlowController.NotifyOtherInteraction()`; `ControlsSubmenu.OnEnable` line 51 calls `NotifyScreenEntered(SettingsControlsScreen)`. No `*Gate` scenario, no test-only button.
- Rule 3 (invariant JSON): N/A — UI-modal task, not world -> screen. Rule 21 lint JSON is the UI equivalent (`fail: 0`).
- Rule 5 (entire acceptance list): every row walked above with fresh backing evidence.
- Rule 6 (report integrity): every PASS claim I sampled traces to real evidence (live prefab dump matches report; verify_en/ja.log lines match the quoted transitions; scene diff is empty as claimed; CSV row count and content match; localisation rows and content_version bump both present). No fabrication detected.
- Rule 7 (standing bans): scene diff empty; no `*Gate` scenario added to `Scenarios.cs`; feature is NOT scoped to `LabScaffold.unity` only (ShellScene carries the ScreenHintPresenter + the modal instance); no touches to `Assets/Scripts/Physics/` (file table lists only `Assets/Scripts/UI/*`, `Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs`, tests, localisation, prefab, scenes, and docs). PASS.
- Rule 9 (Figma node re-pull): my per-element table above cites node ids from SPEC.md §4 and reconciles against the pulled `reference/` node renders; the visual A/B is direct.
- Rule 10 (reference-image diff): performed above — reference renders and built frames at matched 1170x2532, per-element diff enumerated.
- Rule 11 (clone-provenance read-back): re-run live above; every mandated element resolves to a real source sprite; the sole `<NONE>` is the intentional cloned scrim (called out in the lint WARN and the report).
- Rule 12 (Unity authoring traps C1-C8): C1 the builder saves via `PrefabUtility.SaveAsPrefabAsset`, GUID preserved; C2 N/A (single-instance modal); C3 both buttons pin their size via `LayoutElement(450,120)` — confirmed live; C4-C6 the HLG spacing 48 and Panel VLG spacing 24 are exact (not `childForceExpandWidth`); C7 the report captured in play mode; C8 the verify bot boots through Splash StartButton and drives the real onClick handlers. PASS.
- Rule 15 (Cesar-reproduce): no `CESAR_REJECTION.md` exists for this task — the "Hint first" decision was Cesar accepting the code's existing behaviour, not a rejection.
- Rule 16 (mesh metrics): N/A — UI-modal task.
- Rule 18 (Figma fidelity table): present in report (13 rows) and re-run by me above with the same rows.
- Rule 19 (Clone provenance table): present in report with source GUIDs; re-verified live above.
- Rule 21 (UI fidelity lint): re-run by me this pass; `fail: 0`, `warn: 1` (intentional cloned scrim).

## Verdict

**FORWARD_TO_REDTEAM — sets `STATUS.md` to `READY_FOR_REDTEAM`.**

Every acceptance row PASSes on fresh independent verification: my Step 0 pixel
scan matches the built canonical, my Figma A/B against the `reference/` renders
matches on every element (weight, size, position), my live prefab dump matches
every clone-provenance claim, my Rule 21 re-run produced `fail: 0`, the scene
diff is empty, real production entry wiring is confirmed in code, and the
verify_en/ja.log transitions match the report line-for-line. The three `PASS*`
rows (plate 3-line height ~21 px shorter than node, 450 vs 428 button width,
scrim clone kept) are exactly what the spec §4 authored — accepted deviations,
not misses. The "Hint first" stacking row is PASS by Cesar's 2026-09-10
decision. The Rule 21 lint's single WARN (`DimBackground` flat-fill) is the
intentional cloned scrim, verified live and called out in both the report and
here.

Handing off to the red-team gate.
