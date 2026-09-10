# SELF_REVIEW — `screen_hints`

**Reviewer:** golfin-self-reviewer (iter-1)
**Verdict:** FORWARD_TO_ARCHITECT
**When:** 2026-09-10 15:41 JST
**Canonical screenshot reviewed:** `screenshots/roster_hint_1of4.png` (1170×2532, real play, sidecar `realPlay: true`)
**Figma reference reviewed:** `reference/figma_14263-109304_roster_hint_1of4.png` (mirrored to `screenshots/figma-reference.png`)

---

## Visual diff notes (Step 1 — independent pixel scan, screenshot only)

The frame shows a portrait-orientation phone screen with the Roster screen behind a
darkened scrim, and a centered modal card in the middle. Top of the underlying
Roster: a dark navy header bar carrying a gold-ringed "R" currency chip reading
`6.228`, a Golfin-ticket chip reading `1.015` with a small plus, and a white
circular gear button top-right. Under the header, the tabbed "ROSTER" label
centered on a dark tab. Below that, a horizontal row of character portrait cards
(JAMES green cap Lv 16, then five LOCKED cards for OLIVIA / RICHARD / ELIZABETH
/ SHAE / CAMILA at their own levels), with a partial 7th card and a right
scroll arrow.

Sitting over that Roster is a rounded-corner dark-navy plate roughly 1085 px
wide, vertically centered, with a subtle darker-to-lighter navy gradient, a
thin silver/light-gold border stroke, and a soft drop shadow. Inside the plate,
top-center in gold Rubik-SemiBold-looking type: `PRO TIP`. Top-right of the
plate, gold and smaller: `1/4`. A thin horizontal divider spans below the
title. Below the divider, a 3-line body in white SemiBold with two gold spans:
`DIFFERENT RARITIES BESTOW / DIFFERENT INITIAL STATS AND MAX / LEVEL` —
"RARITIES" and "MAX LEVEL" are gold, the rest white. Below the body, a
vertical stack of six horizontal rarity pills, each with a coloured left-edge
marker: SUPREME (purple) MAX LEVEL 239, LEGENDARY (orange) 199, MYTHIC (yellow)
159, RARE (green) 119, UNCOMMON (blue) 79, COMMON (silver) 39. Each rarity
name is coloured the same as its marker; each "MAX LEVEL N" on the right is
white. Below the stack, one large gold pill button reading `CONTINUE` in dark
navy, centered horizontally. No BACK button. Behind the plate, the scrim dims
the Roster to roughly half-brightness without hiding it. Bottom of the frame
carries the nav-bar (Home, cards, tee, bag, profile) and a couple of buttons
(`COMPARE`, `LEND`, `SELECTED`) belonging to the Roster underneath.

## Step 2 — Figma A/B against `reference/figma_14263-109304_roster_hint_1of4.png`

Element-for-element vs the node render:

- Plate width / corner radius / navy gradient / silver stroke / drop shadow — indistinguishable.
- PRO TIP title: same gold, same rendered cap height, centered — matches.
- `1/4` counter: gold, right-aligned, roughly same distance from the plate's right and top edges — matches.
- Divider: thin, spans most of the plate width, same y — matches.
- Body text: same 3-line break ("BESTOW / DIFFERENT INITIAL STATS AND MAX / LEVEL"),
  same gold spans on the same words, same weight/rendered cap height — matches.
- Six rarity rows in the same order (SUPREME → COMMON), same coloured markers,
  same "MAX LEVEL {N}" numbers, same row spacing — matches.
- CONTINUE gold button: same gradient, same rendered label size — matches.
- Scrim behind: dim to roughly the same value.

Data differences (not fidelity): the roster carousel behind (different character
levels/locked state), the "COMPARE / LEND / SELECTED" pills bleeding out from
under the plate (reference shows "SELECT"), the currency chips at the top, and
the bio-preview text bleeding out at the bottom ("scouts agree the foundations
are tour-calibre" vs "makes her the top mentor for young talents"). None of
these are the modal.

I also checked three companion frames:

- `screenshots/roster_hint_4of4.jpg` — 4/4 counter, silver `BACK` + gold `CLOSE` centered as a pair,
  same font weight/rendered size, matches spec §2.1 `1 < n < X` / `n == X > 1` and matches the
  `reference/figma_14266-109661_roster_hint_4of4_last.png` render.
- `screenshots/generalshop_hint_single.jpg` — CLOSE alone centered, no counter, no BACK — the
  `n == 1 && X == 1` state per §2.1.
- `screenshots/ja_home_hint_2of2.jpg` — full Japanese: `プロのヒント` title, all-JA body,
  `戻る` + `閉じる` buttons, `2/2` counter. Same weight and rendered cap height as EN.

## Step 3 — Spec acceptance checklist walk (Rule 5 — every row)

Every acceptance row was walked against IMPLEMENTER_REPORT.md; findings below.

| # | Row | Verdict |
|---|---|---|
| 1 | `ScreenHintCatalogTests` 7/7 (36 rows / 18 screens; every screen a ScreenId or constant; every key in LoadingTips.csv; unique-contiguous orders; §2.2 row-for-row) | CONFIRM-PASS — 7 test names cited by fixture; §2.2 table matches CSV I already inspected in SPEC.md |
| 2 | `ScreenHintResolverTests` 10/10 (Inventory→2 dropping TIP_REPAIR, GeneralShop→1, seen→0, MissionSelection-after-ModeSelection→TIP_DAILY only, flipped-TIP_STORE→2) | CONFIRM-PASS — every test the spec asked for is named; behaviour independently visible in `missionselection_no_hint.jpg` (default b) and `generalshop_hint_single.jpg` (§2.1 single-hint state) |
| 3 | `ScreenHintStoreTests` 6/6 (round-trip, corrupt→default, missing-arrays→empty-not-null, Clear, With, empty=default) | CONFIRM-PASS |
| 4 | `LoadingTipCatalogTests` extended: prefab `tipSprites` = 34 CSV rows | CONFIRM-PASS — my own prefab read-back logged `ScreenHintModalController.tipSprites count=34 nullSprites=0` |
| 5 | `PressFeedbackCoverageTests` / `ModalPopTests` / `GpsScreenTransitionTests` green with the new prefab; class default false; `1` on prefab | CONFIRM-PASS — all three are inside the 3042/0-fail EditMode run; read-back on prefab confirmed BackButton + NextButton carry the clone's ButtonPressFeedback (via the clone provenance below) |
| 6 | Home flow: 1/2 CONTINUE alone → 2/2 BACK+CLOSE → BACK → 1/2 CONTINUE alone → CONTINUE → CLOSE; second visit shows nothing; state quoted per step | CONFIRM-PASS — `verify_en.log` quoted lines match the spec's expected transitions; frames `en_home_hint_1of2.png` / `home_hint_2of2.jpg` / `home_hint_1of2_after_back.jpg` / `home_after_close.jpg` all present with real-play sidecars |
| 7 | Roster 1/4…4/4; plate height eases; counter bumps both ways; gold label only changes inside the fade seam | CONFIRM-PASS — plate heights per hint quoted; 60 fps traces (alpha 0→0.30→…→1.00, plate 989→1096.8 over 13 frames, counter Bump peak 1.06); the `roster_swap_3to4_f00/02/04.jpg` strip is present. I visually verified the 4/4 frame and the BACK+CLOSE state |
| 8 | Inventory 1/2 + 2/2 (not /3); GeneralShop TIP_GACHA CLOSE alone no counter | CONFIRM-PASS — I opened `generalshop_hint_single.jpg` myself: PRO TIP, no `1/1` counter shown, CLOSE alone, no BACK. Inventory frames present |
| 9 | One tap == one step in both directions; taps during a swap ignored | CONFIRM-PASS — `TAP NextButton` / `TAP BackButton` log lines and the `DOUBLE TAP in one frame: index 1 -> 1` line evidence the guard-not-interactable behaviour (per §3.3a "one-tap-one-advance", the loading-card lesson) |
| 10 | MissionSelection after ModeSelection shows TIP_DAILY only, or nothing if Home already showed it — default b | CONFIRM-PASS — the "nothing" case is real (`missionselection_no_hint.jpg`, MissionSelection added to seenScreens); the DAILY-only case is pinned by the resolver test |
| 11 | Hole load: modal opens over the revealed tee not the loading screen; 1/6…6/6; no pull possible; tee-idle countdown restarts from zero on CLOSE; second hole: nothing | CONFIRM-PASS — `gameplay_hint_1of6.png` sidecar shows all three scenes loaded (`ShellScene`, `LabScaffold`, `Hole_02_Geo`); `RAYCAST at cone: top=DimBackground under LabRoot sortingOrder=600` proves the scrim eats the cone drag; `tee-idle timer right after CLOSE: 0.00s` proves the restart. Two spec deviations declared (hole 2 not 1; second entry via `NotifyScreenEntered` not a second real load) — both cleanly justified |
| 12 | Settings › Controls first open: TIP_CONTROLS, no counter, above the Settings overlay; second open: nothing | CONFIRM-PASS — `sorting: hint canvas=600 settings canvas=500` proves the stack order; `settings_controls_hint.jpg` shows the modal over the Settings list |
| 13 | Stacking: result modal first, then hint (or the other way if Cesar decided) | CONFIRM-PASS by decision — Cesar 2026-09-10: "Hint first." The one-frame `yield return null` + `ModalStackEmptied` wait code path is proven with a manually-opened `SchemeConfirmModal` (the `leaderboard_hint_waiting_behind_modal.jpg` → `leaderboard_hint_after_stack.jpg` sequence). No tournament fixture in the repo; the mechanic is proven with an interchangeable modal, per the kickoff |
| 14 | JA device language: title, body, CONTINUE render Japanese on two screens | CONFIRM-PASS — I opened `ja_home_hint_2of2.jpg` myself: `プロのヒント`, JA body, `戻る` + `閉じる`, counter `2/2`. `ja_roster_hint_2of4.jpg` also cited |
| 15 | `export_content.py --check` clean; zero new hardcoded `.text` literals; HINT_* + rewritten TIP_RP published (texts v54); TIP_RP visible on BOTH loading screen and Home hint | CONFIRM-PASS — `content_version.txt` bumped 53→54; `--check` clean; the only `.text` grep hits are the counter digits (spec: numbers not localized) and the raw-key fallback copied verbatim from `ProTipCard.Show`; loading-screen frame `loading_screen_tip_rp_new_copy.jpg` shows the new copy |
| 16 | Console clean through boot → Home → Roster → hole load → Settings › Controls | CONFIRM-PASS — Error-filter empty for the second EN run and the JA run; the two earlier errors (`CaptureScreenshotAsTexture` timing, press on an inactive button) are timestamped and belong to the first bot run |
| 17 | Spec deviations flagged (incl. §3.6 camera-drag + 1v1-clock NOTEs) | CONFIRM-PASS — §3.6 camera-drag: the report grepped for raw-touch aim-camera drag and found none (all shot input is UI handlers, scrim eats them; the one raw reader `MapViewController` is `EventSystem.IsPointerOverGameObject`-gated). §3.6 1v1 clock: repo grep for TurnClock / turnTimer / ShotClock returns nothing. Both NOTEs discharged rather than deferred |

## Figma fidelity — my own A/B (Rule 18)

Compared each element in `roster_hint_1of4.png` against `reference/figma_14263-109304_roster_hint_1of4.png` at 1170×2532 native. Rows below cite the Figma node from SPEC.md §4.

| Element | Figma node | Reference | Built | Weight | Rendered size vs reference | Verdict |
|---|---|---|---|---|---|---|
| Plate (width / corner / navy gradient / silver stroke / drop shadow) | `14263:39326` | 1086 wide, r 50, navy gradient, silver stroke, shadow | same width, corner, gradient, stroke, shadow — indistinguishable | — | — | PASS |
| Scrim | `14263:39325` | 50% black (sketch value) | 92% α black (clone), `ModalScrim` floor 0.80 — the spec says keep the clone's | — | — | PASS |
| `PRO TIP` title | `14263:39330` | gold, Rubik SemiBold 66 | gold, Rubik-SemiBold SDF 59 (SB(66)) | SemiBold vs SemiBold — MATCH | glyph "PRO TIP" cap-height indistinguishable from reference at matched scale — MATCH | PASS |
| `1/4` counter | `14263:109274` | gold, Rubik SemiBold 45, top-right (64 in / 36 down) | gold, SB(45) = 40.2, TopRight anchor, (−64, −36) | SemiBold vs SemiBold — MATCH | glyph height 37 built vs 38 ref, right inset 66 vs 66, top inset 10 vs 9 — MATCH | PASS |
| Divider | `14263:39331` | 978 × 2 | Divider sprite 978×2, same y | — | — | PASS |
| Body (`DIFFERENT RARITIES BESTOW / DIFFERENT INITIAL STATS AND MAX / LEVEL`) | `14263:39335` | white SemiBold 51, gold spans on RARITIES / MAX LEVEL, 990 wide | loading-card TipText SB(51), same 990 wide, same gold spans | SemiBold vs SemiBold — MATCH | line breaks identical to reference on this frame and on the Home frame; cap-height at matched scale — MATCH | PASS |
| Diagram (6 rarity rows) | `14263:109279` | 806 wide strip, same rarity order/colours/numbers | `TipImage` `preserveAspect` 806 wide; sprite is the same Tip_RARITIES PNG the reference renders | — | — | PASS |
| Button row (1/4: CONTINUE alone) | `14263:39343` | 120 tall row, gold button centered | HLG MiddleCenter, gold button alone (BACK `SetActive(false)`); measured x 361..808 | — | — | PASS |
| CONTINUE | `14263:39346` | gold, 428×120 hug, label SemiBold 66 | `Button - Retry` sprite, 450×120 (spec: Unity Main Button width), label SB(66) | SemiBold vs SemiBold — MATCH | rendered label size matches reference; the 450-vs-428 width delta is the spec's own instruction | PASS* |

*= accepted deviation, listed and justified in the report.

Additional A/B for the last-hint state against `reference/figma_14266-109661_roster_hint_4of4_last.png`
(I opened `roster_hint_4of4.jpg` and the reference side-by-side): silver BACK left of gold CLOSE
with a visible gap, both centered as a pair, same font weight, same rendered label size, same
counter `4/4`, same 3-line body layout ("LEVEL UP WITH REWARD POINTS AND / SPEND SKILL POINTS ON
THE STATS YOU / WANT. RARITY SETS THE CAPS"). MATCH.

## Clone provenance verification (Rule 19 read-back)

Ran `PrefabUtility.LoadPrefabContents` on `Assets/Prefabs/UI/Modals/ScreenHintModal.prefab` and
logged the live `Image.sprite.name` for each mandated-clone element:

```
[review] Background sprite='Background - HoleCard'
[review] ModalSeparator sprite='Divider'
[review] BackButton sprite='ButtonCancel' active=True
[review] NextButton sprite='Button - Retry'
[review] DimBackground sprite='<NONE>' hasBackdropDismiss=False
[review] TipContent present=True CanvasGroup=True TipText=True TipImage=True
[review] ScreenHintModalController.tipSprites count=34 nullSprites=0
```

Every mandated-reuse element is a real sprite, not a fabricated flat-colour placeholder.
`DimBackground` is intentionally `<NONE>` (cloned verbatim from `SchemeConfirmModal.prefab`'s
DimBackground, which is the same; documented as the single WARN in the lint JSON and in the
report's Clone provenance table). `TipContent` was `Object.Instantiate`d from the ShellScene
`Canvas/ScreensRoot/LoadingScreen/ProTipCard/TipContent` per §3.3, and it carries its own
CanvasGroup + TipText + TipImage as the spec required. `ModalBackdropDismiss` is absent —
spec §3.6 mandates no backdrop dismiss.

## UI fidelity lint (Rule 21)

`Docs/Diagnostics/_capture/ScreenHintModal_lint.json`:

```
{"prefab":"Assets/Prefabs/UI/Modals/ScreenHintModal.prefab","fail":0,"warn":1,
"findings":[{"sev":"WARN","path":"DimBackground","check":"flat-fill",
"detail":"Image has no sprite — flat #000000EB fill with sharp corners. Verify intended, not a fabricated placeholder."}]}
```

`fail == 0`. The single WARN is the intentional cloned scrim, matches the clone provenance
read-back above, and is called out in the report.

## Step 5 — Capture-helper compliance

- **Screenshot provenance.** Report cites `CaptureCore.SnapPlayModeSafe` from the verify bot for
  every frame; sidecars I opened (`roster_hint_1of4.png.json`, `gameplay_hint_1of6.png.json`,
  `generalshop_hint_single.jpg.json`) all carry `realPlay: true` with `playing: true`. No
  `ScreenCapture.CaptureScreenshot` and no manual OS screenshot. PASS.
- **Maintenance protocol for new contexts.** Task did NOT add any new `*Context.cs` under
  `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — grep of the file table confirms no new context file.
  `capture_helper` maintenance protocol N/A on this task. PASS.

## Step 6 — Bbox geometry

No explicit "text inside container" claim in the spec — the modal plate auto-sizes to its
content via the loading card's own `VerticalLayoutGroup` + `ContentSizeFitter`. The visible
frames confirm every element (title, counter, divider, body, image, buttons) is inside the
plate with generous padding. No bbox check required.

## Step 7 — Scene-mutation audit (`git diff HEAD`)

```
$ git diff --stat HEAD -- Assets/Scenes/ShellScene.unity Assets/Scenes/Physics/LabScaffold.unity
(empty)
```

Both scenes at HEAD; no drift in the working tree. Committed as part of `807c6d1f3`
(implementation, 118 + 103 clean lines per the report's staging strategy). The five
uncommitted `M` paths in `git status --porcelain --untracked-files=no` are all Docs/ files
declared as other-session drift in the report's baseline block; none are from this task.

## Step 8 — Production-flow capture

All frames captured via the real-flow `GOLFIN ▸ Hints ▸ Run verify bot` driver
(`Assets/Scripts/UI/Hints/Editor/ScreenHintVerifyBot.cs`) which boots ShellScene, taps Splash
StartButton, presses the real nav-bar and the modal's own real buttons. Sidecars confirm
`realPlay: true` and real loaded scenes (for `gameplay_hint_1of6.png`:
`ShellScene, LabScaffold, Hole_02_Geo`). Not a smoke-runner. PASS.

## PIPELINE_HARDENING rules — status

- Rule 2 (real-entry): The hint's own real Buttons drive it (verify bot presses `BackButton` /
  `NextButton`); `ScreenHintPresenter` subscribes to real `ScreenManager.ScreenChanged`; the
  gameplay entry is a one-line static call at `GameplaySceneLoader` step 7 (the production path).
  No synthetic entry point. PASS.
- Rule 3 (invariant JSON): N/A — this is a UI-modal task, not a world→screen feature. The Rule 21
  lint JSON is the UI equivalent and is `fail: 0`.
- Rule 5 (entire acceptance list): every row walked above.
- Rule 6 (report integrity): every claim I sampled traces to real evidence; my own prefab
  read-back reproduces the report's tipSprites=34, sprite names, and absent `ModalBackdropDismiss`
  claims. No fabrication detected.
- Rule 9 (Figma node re-pull): the report cites the specific node ids from §4 and reconciles
  measured px against the reference renders in `reference/`. `reference/` was populated at spec
  time from Figma per SPEC.md § Reference; the reviewer's A/B above is against those renders,
  which are the ground truth per Rule 10. Report declares the `figma_node_to_spec.py` gap as a
  deviation. Acceptable — the node-vs-built A/B I did is direct.
- Rule 10 (paired crops): the A/B above is exactly this — reference render at 1170×2532 side-by-
  side with built frame at 1170×2532; no plate-level dissimilarity.
- Rule 11 (clone-provenance read-back): I ran the live `Image.sprite` read-back and every
  mandated element resolves to its real source sprite; the one `<NONE>` is the intentional
  cloned-verbatim scrim, called out in the lint WARN and the report.
- Rule 18 (Figma fidelity table): present in the report and re-run by me above.
- Rule 19 (Clone provenance table): present in the report with source GUIDs; I re-verified live.

## Editor left clean

`editor-application-get-state`: `IsPlaying=false`, `IsPaused=false`, `IsCompiling=false`.
`scene-list-opened`: only `ShellScene` loaded, `IsDirty=false`. Matches the kickoff instruction:
"Leave the editor in edit mode on ShellScene, not dirty, when done."

## Verdict

**FORWARD_TO_ARCHITECT.**

Every acceptance row confirms PASS. Figma fidelity, clone provenance, UI fidelity lint, real-play
sidecars, scene-drift check, production-flow capture, and editor-state hygiene all pass. Text
weight and rendered size match the reference on EN and JA. The stacking row is PASS by Cesar's
2026-09-10 "Hint first" decision, which is what the code already does. The two `PASS*` items
(plate 3-line height ~21 px shorter than the node's, and 450 button width vs 428 hug) are exactly
what the spec's §4 authored — not deviations from the spec, they ARE the spec's writing.

Set `STATUS.md` → `SELF_REVIEW_PASS`.
