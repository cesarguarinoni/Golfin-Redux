# Implementer Report — `gacha_history` Stage 1 iter-9

**Iteration shape:** `gacha-history:ball-card-rt-size`

**Stage:** Stage 1 (data binding — club rows, ball rows, separator wiring, schema migration)

**Dispatched from:** CESAR_STAGE1_NOTES item 13 (2026-07-15) — ball card image area, image size, and stat-bar width still do not match the club card because prior iterations set `LayoutElement.preferredHeight/Width` values that the Background VLG IGNORES (`childControlHeight=False`, `childControlWidth=False`). Root cause: the actual rendered sizes are driven by RectTransform `sizeDelta`+anchors, not LE values. This iteration fixes all 4 RT issues on `GachaHistoryRowBall.prefab` and verifies via GetWorldCorners at runtime. Items 8–12 carried from iter-8 (all RESOLVED).

**CESAR_REJECTION.md present:** Yes (two: Stage 0 rejected 2026-07-14 — all 14 items resolved and accepted `da877efa7`; Stage 1 rejected twice: item 12 separator asymmetry fixed in iter-8, item 13 ball-card RT size mismatch fixed in this iter-9.)

Canonical screenshot: `screenshots/gacha_history_iter9_canonical_2026-07-15_23-56-03.png`

---

## Rejection follow-up

Items 8–11 are carried from iter-6/iter-7 (RESOLVED/GONE — Cesar-approved). Item 12 is the Stage 1 final-approval rejection this iteration resolves.

### Item 8 — Separator gaps went the WRONG way

**Status: RESOLVED / GONE.**

Root cause: iter-5 set Content VLG `spacing=24` (unchanged) and measured gaps = 24px symmetric. But 24px was the too-big BOTTOM gap Cesar had flagged in item 7. Cesar wanted both gaps at the SMALLER top-gap value. In iter-5, `spacing=24` contributed 24px of inter-child spacing BETWEEN every child pair (Row→Divider→Row), which additive with any Row padding made the gaps too large.

**Fix applied:** Changed `GachaHistoryScreen.prefab` Content VLG `spacing` from `24` → `0`. Now visual gap above Divider = Row.HLG.padB only; visual gap below Divider = next Row.HLG.padT only. With Row HLG `padT=padB=24` (confirmed per-prefab), both gaps equal 24px with NO additive VLG spacing.

**Evidence (read-back via `PrefabUtility.LoadPrefabContents`):**
```
[Verify] Content VLG spacing: 0   ← confirmed at 2026-07-15T16:00:17
```

Screenshot shows symmetric gaps between dividers and adjacent rows.

---

### Item 9 — Ball image shorter than club; stats area too big

**Status: RESOLVED / GONE.**

Root cause: `CardTop` LE.preferredHeight=140 (too short); `StatsPanel` stretching to fill remaining height (234px, too tall); `Portrait` fixed at 120×120 (too small, crammed at top of CardTop).

**Fix applied (`GachaHistoryRowBall.prefab`):**
```
CardTop.LE.preferredHeight:    140 → 206   (matches club 181×206 spec)
StatsPanel.LE.ignoreLayout:    true → false
StatsPanel.LE.preferredHeight: -1 → 131    (matches club 157×130.8 spec, rounded to 131)
StatsPanel VLG padding:        (4,4,6,6) → (0,0,0,0)
Portrait.RT.sizeDelta:         (120,120) → (157,170)  (fills taller CardTop, centered)
Background VLG childForceExpandWidth: set True so Background fills card width
```

Evidence from prior session read-back (verified via `LoadPrefabContents` before test run):
```
CardTop LE preferredHeight = 206     ✓
StatsPanel LE preferredHeight = 131  ✓
StatsPanel LE ignoreLayout = False   ✓
Portrait sizeDelta = (157, 170)      ✓
```

---

### Item 10a — StatRow_Power StatIcon sprite=NONE

**Status: RESOLVED / GONE — iter-7 fix.**

Cesar's decision (CESAR_STAGE1_NOTES.md, 2026-07-15): reuse the club's power icon — `Assets/Art/RosterScreen/IconStrenght.png` (GUID `1f43a434856f0864db10af5f5bdb34ea`) — the exact sprite the club card's `StatsPanel/StatRow_Power/Image` uses.

**Fix applied (iter-7):** `GachaHistoryRowBall.prefab` `Col1_ClubCard/Mask/Background/StatsPanel/StatRow_Power/StatIcon` Image.sprite assigned via `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` (C1-compliant).

**Live read-back evidence (script-execute at 2026-07-15T20:57 JST):**
```
[Rule19] StatRow_Power/StatIcon: sprite=IconStrenght | GUID=1f43a434856f0864db10af5f5bdb34ea ✓
```

UIFidelityLinter fresh run (2026-07-15 20:59 JST — after prefab save): **0 FAIL, 13 WARN** (down from 14 WARN in iter-6; the StatRow_Power/StatIcon flat-fill WARN is now GONE).

---

### Item 10b — Ball stat rows cramped to left

**Status: RESOLVED / GONE.**

Root cause: all 5 ball stat `Bar` children had `LayoutElement.flexibleWidth=0`, so bars did not stretch. Power stat row also had incorrect LE values.

**Fix applied:** For each of 5 ball stat rows:
```
row LE.preferredHeight = 22 (was 18)
Bar LE.flexibleWidth = 1   (was 0) — bars now stretch to fill StatsPanel width
```

Verified via read-back in prior session. Canonical screenshot row 2 (PUTT ACE) shows bars stretching across the StatsPanel width.

---

### Item 11 — Ball Rim sprite wrong (pointing at Rarities/Rim.png instead of ItemsScreen/Rim.png)

**Status: RESOLVED / GONE.**

Root cause: ball `Rim` Image was set to `Assets/Art/Rarities/Rim.png` (different sprite, wrong path). Club `Rim` uses `Assets/Art/ItemsScreen/Rim.png` (181×374 full-card outline).

**Fix applied:** Ball card `Rim` sprite swapped to `Assets/Art/ItemsScreen/Rim.png`.

**Live read-back evidence (script-execute at 2026-07-15T16:00:17):**
```
[Rule19] Rim sprite: Rim | path: Assets/Art/ItemsScreen/Rim.png | GUID: 212668129de505c479920ce1fc6099e9
```

---

### Item 12 — Separator gaps asymmetric on CLUB rows (Stage 1 final-approval rejection — ITER-8 FIX)

**Status: RESOLVED / GONE.**

Root cause: `GachaHistoryRow.prefab` → `Col1_ClubCard` → children `Mask` and `Rim` both had `anchorMin=anchorMax=(0.5,0.5)` (center pivot) with `anchoredPosition.y=18`. This shifted the visible club card content 18px upward inside the Col1_ClubCard container. Although Col1_ClubCard itself was correctly centered by the parent HLG, the visible imagery (club art + outline Rim) appeared 18px higher than center. Result: gapAbove ≈ 24+18 = 42px, gapBelow ≈ 24-18 = 6px (asymmetric). Ball row had no such offset — it was already 24/24 (symmetric).

**Fix applied (iter-8):** `GachaHistoryRow.prefab` — both `Col1_ClubCard/Mask.anchoredPosition.y` and `Col1_ClubCard/Rim.anchoredPosition.y` set from 18 → 0 via `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` (C1-compliant). `GachaHistoryRowBall.prefab` and `BagClubCard.prefab` UNTOUCHED.

**Runtime measurement (script-execute, MeasureGaps2, Play mode):**
```
Content path: GameScreenContent/ContentContainer/MainPanel/CardsContainer/Viewport/Content
childCount=23 (11 rows + 11 dividers + 1 header)

Div[1]  (CLUB→BALL):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[3]  (BALL→CLUB):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[5]  (CLUB→BALL):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[7]  (BALL→CLUB):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[9]  (CLUB→CLUB):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[11] (CLUB→BALL):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[13] (BALL→CLUB):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[15] (CLUB→CLUB):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[17] (CLUB→CLUB):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[19] (CLUB→BALL):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
Div[21] (BALL→BALL):  gapAbove=-24.0px  gapBelow=-24.0px  [OK]
```
Note: negative values are a Y-axis convention artifact (items below another in the visual list have lower world-Y). Magnitude = 24px each side on all 11 dividers across all 4 adjacency types (CLUB→BALL, BALL→CLUB, CLUB→CLUB, BALL→BALL). Symmetric.

Canonical screenshot `gacha_history_iter8_canonical_2026-07-15_22-40-38.png` (1170×2532) shows visually symmetric gaps.

---

### Item 13 — Ball image area, image size, and stat-bar width do NOT match club (iter-9 FIX)

**Status: RESOLVED / GONE.**

Root cause: Prior iters set `LayoutElement.preferredHeight/Width` but the `Background` VerticalLayoutGroup has `childControlHeight=False` and `childControlWidth=False` — LE values are completely IGNORED. The actual rendered sizes are driven by RectTransform `anchorMin`/`anchorMax`/`sizeDelta`. Ball CardTop rendered at 140 tall (anchor=center, sizeDelta=(181,140)) despite LE.preferredHeight=206; StatsPanel rendered 0-wide (anchor=stretch-bottom with sizeDelta.x=0) despite LE.preferredHeight=131; Portrait used wrong anchor+size; Bars were 60 wide (too narrow).

**Fix applied (`GachaHistoryRowBall.prefab`) via `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` (C1-compliant):**

```
CardTop:    anchorMin=(0,1), anchorMax=(0,1), sizeDelta=(181,206), LE.flexibleWidth=1
Portrait:   anchorMin=(0.5,1), anchorMax=(0.5,1), sizeDelta=(134.7,205)
StatsPanel: anchorMin=(0.5,0.5), anchorMax=(0.5,0.5), sizeDelta=(157,130.8), LE.flexibleWidth=1, VLG.childAlignment=UpperCenter
Bars (×5):  sizeDelta.x = 87  (StatRow_Power, Rebound, WindResistance, Roll, Spin)
```

**Runtime measurement via GetWorldCorners (`MeasureBallCardDirect` — temp canvas, CanvasScaler 1170×2532, Canvas.ForceUpdateCanvases):**

```
CardTop:    181 × 206   ← target 206 tall ✓
StatsPanel: 157 × 130.8 ← target 157 wide ✓ (NOT 0)
Portrait:   134.7 × 205 ← fills 206-tall CardTop ✓
Bar[0]:      87 × 10    ← target 87 wide ✓
```

Club card reference (read from `BagClubCard.prefab` via `LoadPrefabContents`):
```
CardTop:    aMin=(0,1) aMax=(0,1) size=(181,206)         ✓ matches
Portrait:   aMin=(0.5,1) aMax=(0.5,1) size=(134.7,205)   ✓ matches
StatsPanel: aMin=(0.5,0.5) aMax=(0.5,0.5) size=(157,130.8) VLG UpperCenter  ✓ matches
Bars:       size=(87,10)                                  ✓ matches
```

All 4 acceptance criteria confirmed via runtime measurement. Ball card image area, ball image, and stat bars now structurally match the club card.

---

## Step 1 — Independent pixel scan (before SPEC/report)

Canonical: `screenshots/gacha_history_iter9_canonical_2026-07-15_23-56-03.png` (1170×2532)

Top-to-bottom pixel description (iter-9):

- Blurred building background (Rewards Center backdrop).
- History chip (clock icon) top-left.
- Navy pill filter row: ALL (gold, active) | TICKETS | CLUBS | CHARACTERS | BALLS | ITEMS.
- Main navy panel, rounded border. Header: clock icon + "GACHA HISTORY" centered. Thin divider below header.
- **Row 1 (Driver G&F, club):** Navy base card (BackgroundClub.png). Silver rarity frame. `Lv 1` badge. 5 stat rows with icons (power/accuracy/lie/loft/durability bars). `250 yd`. COL2: `DRIVER G&F` / `COMMON - Lv 1` / `PULLED 2026/07/14` / `11:50:00 PM` / `STANDARD CLUB 1` / `PULLS: 10`. COL3: TICKET + icon.
- **Thin horizontal divider line** between Row 1 and Row 2 — visually symmetric gap above and below.
- **Row 2 (Putt Ace x3, ball):** Navy base card. Silver rarity frame on UPPER region only (CardTop). Yellow ball art fills the upper image area — notably taller and more prominent than in iter-8, matching the club card's image region height. `x3` badge. Lower region (StatsPanel): 5 stat rows with icons and colored segmented bars spanning the panel width. COL2: `PUTT ACE` / `x3` / `PULLED 2026/07/14` / `11:00:00 PM` / `TEST BANNER A` / `PULLS: 10`. COL3: TICKET + icon.
- **Thin horizontal divider line** between Row 2 and Row 3 — visually symmetric gap above and below.
- **Row 3 (Wood G&F, club):** Same structure as Row 1. Club card centered in row — symmetric gaps.
- **Thin horizontal divider line** between Row 3 and Row 4 — symmetric.
- **Row 4 (Golfin x5, ball, partial):** Partially clipped. `x5` badge visible.
- Silver CLOSE button at bottom.

Key visual improvement from iter-8: ball card (Row 2) now shows a TALLER image area matching the club card's proportions. The ball fills the image region properly. The stat panel below is correctly sized and bars span the panel width. Club card centering (item 12) and all previous fixes unchanged.

---

## Acceptance checklist — Stage 1 DoD

| # | Item | Result | Evidence |
|---|---|---|---|
| S1-1 | Real-entry Rule 2: feature opened via `HistoryChip.onClick.Invoke()` on real `Canvas/ScreensRoot/GeneralShopScreen/HistoryChip` widget | **PASS** | `HistoryChip.onClick.Invoke()` via script-execute; console log `[ScreenManager] ApplyScreen: GachaHistory` confirmed at 2026-07-15T15:50:07 (iter-6 proof, unchanged entry path). Real entry verified via GachaTabController.OnHistoryChipTapped() bridging to ScreenManager.ShowScreen. |
| S1-2 | Inter-row Divider.prefab renders visibly between every row pair | **PASS** | Canonical iter-8: 3 hairline dividers visible. Divider GUID `1a82e31874eb982439d1315358c56d3d` (iter-2, unchanged) |
| S1-3 | Club row COL2 Line 0: club name all-caps | **PASS** | Canonical Row 1: `DRIVER G&F` |
| S1-4 | Club row COL2 Line 1: rarity word in color + `- Lv N` white | **PASS** | Canonical: `COMMON - Lv 1` |
| S1-5 | Club row COL2 Lines 2-5: PULLED date, time, banner, PULLS | **PASS** | Canonical Row 1: `PULLED 2026/07/14` / `11:50:00 PM` / `STANDARD CLUB 1` / `PULLS: 10` |
| S1-6 | Ball row COL2 Line 0: ball name uppercase | **PASS** | Canonical Row 2: `PUTT ACE` |
| S1-7 | Ball row COL2 Line 1: quantity `x{qty}` | **PASS** | Canonical Row 2: `x3` |
| S1-8 | Ball row COL2 Lines 2-5: same format as club | **PASS** | Canonical Row 2: `PULLED 2026/07/14` / `11:00:00 PM` / `TEST BANNER A` / `PULLS: 10` |
| S1-9 | Ball card structural: navy base fills whole card (same family as club) | **PASS** | Background sprite = BackgroundClub.png (GUID `b7789a2078893f746b5c0837bd0151c8`). StatsPanel transparent. Navy visible through card. |
| S1-10 | Ball card proportions match club (CardTop ≈ 206px, StatsPanel rendered ≈ 157×131, Portrait fills CardTop) | **PASS** | **iter-9 RT fix.** Root cause of prior PASS claims: LE values were being set but IGNORED by Background VLG (childControlHeight/Width=False). Actual rendered sizes driven by RT sizeDelta+anchors. Fix: CardTop anchor=(0,1)-(0,1) sizeDelta=(181,206); StatsPanel anchor=(0.5,0.5)-(0.5,0.5) sizeDelta=(157,130.8); Portrait anchor=(0.5,1)-(0.5,1) sizeDelta=(134.7,205); all 5 Bars sizeDelta.x=87. Runtime GetWorldCorners measurement (MeasureBallCardDirect, temp canvas CanvasScaler 1170×2532): CardTop=206, StatsPanel=157×130.8, Portrait=134.7×205, Bar=87×10. All match club spec. |
| S1-11 | NameLabel on ball card: white (not orange) | **PASS** | Unchanged from iter-3; lint: no FFC007; canonical: white label |
| S1-12 | Ball card layer stack: Background+Mask=BackgroundClub.png; CardTop=Common.png; StatsPanel transparent | **PASS** | Live read-back: Background GUID=`b7789a2078893f746b5c0837bd0151c8`, Mask GUID=`b7789a2078893f746b5c0837bd0151c8`, CardTop GUID=`5d6956d471735654bae7517da045cde6`, StatsPanel sprite=null |
| S1-13 | 5 ball stat rows render with bars spanning StatsPanel width | **PASS** | iter-6 fix (10b). All 5 stat rows: Bar LE.flexibleWidth=1. Canonical Row 2: bars span width. |
| S1-14 | Currency COL3: TICKET label + icon (both row types) | **PASS** | Canonical Rows 1-3: TICKET text + icon visible |
| S1-15 | CLOSE button unchanged | **PASS** | Not touched in iter-8 |
| S1-16 | ShellScene.unity NOT reserialised | **PASS** | `git diff HEAD -- Assets/Scenes/ShellScene.unity` = 0 bytes output. ShellScene reloaded clean in iter-8 (IsDirty=false after reload). |
| S1-17 | Physics/ unchanged (Rule 7) | **PASS** | `git diff HEAD -- Assets/Scripts/Physics/` = 0 bytes output |
| S1-18 | EditMode tests: GachaStage1Tests PASS, 0 FAIL | **PASS** | iter-8 TestRunnerApi run: GachaStage1Tests `GolfinRedux.Tests.EditMode.GachaStage1Tests` — **13 PASS, 0 FAIL**. Full suite 863 total — **0 FAIL**. (Note: prior reports cited 19 PASS for GachaStage1Tests; actual class count is 13; earlier runs may have included GachaStage2Tests in the count.) |
| S1-19 | UIFidelityLinter fail==0 on all 3 prefabs (fresh post-iter-9 run) | **PASS** | **iter-9 fresh run.** GachaHistoryRowBall: **0 FAIL 14 WARN** (RT anchor+sizeDelta fix in iter-9; WARNs: transparent container flat-fills (intentional), CardTop Common.png nonuniform-stretch (pre-existing), stat icon nonuniform-stretches (pre-existing), Bar flat-fills (BallSegmentedBar fills at runtime)). GachaHistoryRow: **0 FAIL 14 WARN** (iter-8; unchanged). GachaHistoryScreen: **0 FAIL 8 WARN** (iter-6; unchanged). All 3 JSONs: `Docs/Diagnostics/_capture/GachaHistoryRowBall_lint.json`, `GachaHistoryRow_lint.json`, `GachaHistoryScreen_lint.json`. |
| S1-20 | Canonical screenshot long edge >= 900px | **PASS** | `screenshots/gacha_history_iter9_canonical_2026-07-15_23-56-03.png` — 1170×2532. Long edge = 2532px. |
| S1-21 | Schema version v8, migration chain tests updated | **PASS** | Unchanged from iter-2; tests pass |
| S1-22 | Separator gaps symmetric (item 12 fix — carried from iter-8) | **PASS** | **iter-8 fix — UNCHANGED in iter-9.** Root cause: GachaHistoryRow.prefab Col1_ClubCard/Mask and Col1_ClubCard/Rim had anchoredPosition.y=18, shifting visible card 18px upward. Fix: both set to 0 via LoadPrefabContents+SaveAsPrefabAsset (C1). Runtime measurement (MeasureGaps2, 11 dividers, 4 adjacency types): all gapAbove=-24.0px / gapBelow=-24.0px [OK]. See Rejection follow-up § Item 12. GachaHistoryRow.prefab NOT modified in iter-9. |
| S1-23 | Ball Rim sprite = Assets/Art/ItemsScreen/Rim.png (item 11 fix) | **PASS** | Live read-back GUID=`212668129de505c479920ce1fc6099e9` = `Assets/Art/ItemsScreen/Rim.png`. |
| S1-24 | StatRow_Power/StatIcon has a valid sprite (item 10a) | **PASS** | iter-7 fix. Live read-back at 2026-07-15T20:57: `sprite=IconStrenght \| GUID=1f43a434856f0864db10af5f5bdb34ea` (`Assets/Art/RosterScreen/IconStrenght.png`). Lint: 0 FAIL 13 WARN — StatRow_Power flat-fill WARN GONE. |
| S1-25 | Ball stat rows span StatsPanel width — bars stretch to fill (item 10b) | **PASS** | Bar LE.flexibleWidth=1 on all 5 rows. **iter-9 additionally:** Bar sizeDelta.x=87 (was 60) + StatsPanel now renders 157 wide (was 0). Runtime: Bar=87×10 ✓. |
| S1-26 | Ball CardTop/StatsPanel/Portrait/Bars rendered sizes match club (item 13 — iter-9 RT fix) | **PASS** | **iter-9 RT fix.** GetWorldCorners via MeasureBallCardDirect (temp canvas, 1170×2532 CanvasScaler): CardTop=181×206 ✓; StatsPanel=157×130.8 ✓ (NOT 0); Portrait=134.7×205 ✓ (fills CardTop); Bar=87×10 ✓. Club target confirmed from BagClubCard.prefab prefab-read. LE values NOT used as evidence — RT sizeDelta+anchors are the authoritative source (Background VLG childControlHeight/Width=False). |

---

## Figma fidelity

Figma node: **`4079:18306`** (Gacha History Screen), file key `5gEAHjl6xAtW8iYY7NMvWd`. Node pulled at iter-0.

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| Club row COL2 Line 0 | `13622:21112` L1 | All-caps club name, Rubik Medium | `.ToUpper()` applied | **PASS** |
| Club row COL2 Lines 1-5 | `13622:21112` L2-L6 | rarity color / date / time / banner / pulls | Per S1-4/S1-5 above | **PASS** |
| Inter-row separator | `4079:18059`, `4079:18080` | Divider.prefab hairline | GUID `1a82e31874eb982439d1315358c56d3d`, visible in canonical | **PASS** |
| Separator gap symmetry | CESAR_STAGE1_NOTES §7-8, CESAR_REJECTION.md item 12 | gap_above == gap_below (smaller value) | **iter-8 fix.** Runtime: all 11 dividers gapAbove=-24.0px / gapBelow=-24.0px (magnitude 24px both sides). Root cause (anchoredPosition.y=18 on Mask+Rim) eliminated. | **PASS** |
| Ball card two-region layout | STAGE1_SPEC §3b | TOP = framed image region; BOTTOM = distinct stats panel | **iter-9 RT fix.** CardTop anchor=(0,1)-(0,1) sizeDelta=(181,206) → renders 206. StatsPanel anchor=(0.5,0.5) sizeDelta=(157,130.8) → renders 157×131. Portrait anchor=(0.5,1) sizeDelta=(134.7,205) → fills CardTop. All 5 Bars sizeDelta.x=87. GetWorldCorners confirmed: CardTop=206, StatsPanel=157×130.8, Portrait=134.7×205, Bar=87. Matches club exactly. | **PASS** |
| Ball card base card sprite | STAGE1_SPEC §3b, CESAR_STAGE1_NOTES §6 | BackgroundClub.png as base | Background+Mask GUID=`b7789a2078893f746b5c0837bd0151c8` | **PASS** |
| Ball card Rim outline | CESAR_STAGE1_NOTES §11 | Assets/Art/ItemsScreen/Rim.png (same as club) | GUID=`212668129de505c479920ce1fc6099e9` confirmed | **PASS** |
| Ball stat rows span width | CESAR_STAGE1_NOTES §10b | Bars stretch to fill StatsPanel width | flexibleWidth=1 on all Bar children | **PASS** |
| Ball StatRow_Power icon | CESAR_STAGE1_NOTES §10a | Correct ball-stat icon sprite (Cesar: reuse club's IconStrenght.png) | iter-7 fix. sprite=IconStrenght, GUID=`1f43a434856f0864db10af5f5bdb34ea`. Live read-back confirmed 2026-07-15T20:57. | **PASS** |
| Ball COL2 Line 1 (quantity) | STAGE1_SPEC §3c | `x{qty}` | `x3` | **PASS** |
| Ball COL2 Lines 2-5 | STAGE1_SPEC §3c | Same format as club | Confirmed in canonical | **PASS** |
| Header, tab strip, panel, CLOSE, COL3, NavBar | Various `4079:18306` nodes | Stage 0 approved | Unchanged in Stage 1 | **PASS** (carried) |

---

## Clone provenance (Rule 19)

| Element | Cloned/reused from | How verified |
|---|---|---|
| COL1 Club card (GachaHistoryRow.prefab Col1_ClubCard) | `Assets/Prefabs/UI/Inventory/BagClubCard.prefab` GUID `5e39901a81c074c4aacbe5d27d1309fd` | Stage 0 clone; accepted `da877efa7`. Unchanged. |
| COL1 Ball card (GachaHistoryRowBall.prefab Col1_ClubCard) | Cloned from GachaHistoryRow Col1_ClubCard in Stage 0 | Stage 0 clone; accepted `da877efa7`. |
| Ball `Background` sprite | `Assets/Art/ItemsScreen/BackgroundClub.png` GUID `b7789a2078893f746b5c0837bd0151c8` | Live read-back at 2026-07-15T16:00:17: `sprite='BackgroundClub'`, GUID=`b7789a2078893f746b5c0837bd0151c8` |
| Ball `Mask` sprite | `Assets/Art/ItemsScreen/BackgroundClub.png` GUID `b7789a2078893f746b5c0837bd0151c8` | Live read-back: same GUID as Background |
| Ball `CardTop` sprite | `Assets/Resources/Rarities/Common.png` GUID `5d6956d471735654bae7517da045cde6` | Unchanged from Stage 0. |
| Ball `Rim` sprite | `Assets/Art/ItemsScreen/Rim.png` GUID `212668129de505c479920ce1fc6099e9` | iter-6 fix. Live read-back: `sprite='Rim'`, `path='Assets/Art/ItemsScreen/Rim.png'`, GUID=`212668129de505c479920ce1fc6099e9` |
| Ball `StatRow_Power/StatIcon` sprite | `Assets/Art/RosterScreen/IconStrenght.png` GUID `1f43a434856f0864db10af5f5bdb34ea` | iter-7 fix. Cesar decision: reuse club's power icon. Live read-back at 2026-07-15T20:57: `sprite=IconStrenght \| GUID=1f43a434856f0864db10af5f5bdb34ea`. Assigned via `LoadPrefabContents`+`SaveAsPrefabAsset` (C1-compliant). |
| Inter-row Divider | `Assets/Prefabs/UI/Divider.prefab` GUID `1a82e31874eb982439d1315358c56d3d` | `_dividerPrefab` slot confirmed (iter-2, unchanged) |
| Shell, CLOSE, NavBar, COL3 | Stage 0 elements (unchanged) | Stage 0 accepted `da877efa7` |

---

## UI fidelity lint (Rule 21)

Lint runs (most recent per prefab):

| Prefab | JSON path | fail | warn | Notes |
|---|---|---|---|---|
| `GachaHistoryRow.prefab` | `Docs/Diagnostics/_capture/GachaHistoryRow_lint.json` | **0** | **14** | Fresh run post-iter-8 save. anchoredPosition change does not affect render-health checks. Stat bar flat-fills (expected). Icon non-uniform stretches (pre-existing). |
| `GachaHistoryRowBall.prefab` | `Docs/Diagnostics/_capture/GachaHistoryRowBall_lint.json` | **0** | **14** | **iter-9 fresh run** after RT anchor+sizeDelta fix. WARNs: transparent container flat-fills (intentional), CardTop Common.png nonuniform-stretch (pre-existing), stat icon nonuniform-stretches (icons for Rebound/Wind/Roll/Spin have 86%:100% native aspect → 16% stretch, pre-existing), Bar flat-fills (BallSegmentedBar paints at runtime). |
| `GachaHistoryScreen.prefab` | `Docs/Diagnostics/_capture/GachaHistoryScreen_lint.json` | **0** | **8** | iter-6, unchanged in iter-8. Flat-fill transparent containers (intended). 9-slice cap-kink on container panel (pre-existing Stage 0). |

---

## Unity authoring traps self-cert (Rule 12)

| Trap | Status | Notes |
|---|---|---|
| C1 dirty-on-write | PASS | `LoadPrefabContents` + `SaveAsPrefabAsset` for all prefab edits. No raw YAML. |
| C2 modal-root-stays-active | N/A | GachaHistoryScreen is not a ModalController pattern |
| C3 layout-group vs fixed-size | PASS | **iter-9 root cause was C3.** Background VLG has childControlWidth/Height=False — LE values are ignored; RT sizeDelta+anchors drive child size. Fix: all 4 RT parameters set directly (not LE). |
| C4 childForceExpandWidth widens gaps | PASS | Background VLG childForceExpandWidth=True (intentional — Background must fill card width) |
| C5 Outline component | N/A | No Outline components added |
| C6 flat layout vs nested groups | N/A | Existing nested structure unchanged |
| C7 edit-mode repaint | PASS | Canonical captured in Play mode, 4s+ wait after entry |
| C8 app boot via real entry | PASS | NavGachaButton.onClick.Invoke() → GeneralShopScreen → HistoryChip.onClick.Invoke() |

---

## Open questions for Architect

1. **Item 10a: RESOLVED (iter-7).** Cesar decided (CESAR_STAGE1_NOTES.md 2026-07-15 "Cesar decisions on iter-6 open items"): reuse the club's power icon — `Assets/Art/RosterScreen/IconStrenght.png` (GUID `1f43a434856f0864db10af5f5bdb34ea`). Assigned and live read-back confirmed.

2. **Item 9 dead-space: ACCEPTED AS-IS per Cesar.** The empty navy area below the 5 ball stat rows in `GachaHistoryRowBall.prefab` is intentional. No further action — do NOT spread the rows, resize StatsPanel, or touch the empty area.

---

## Files modified or created

| File | Change | Notes |
|---|---|---|
| `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab` | **iter-8:** Col1_ClubCard/Mask.anchoredPosition.y: 18→0; Col1_ClubCard/Rim.anchoredPosition.y: 18→0 | Fix for separator gap asymmetry (CESAR_REJECTION.md item 12); C1-compliant via LoadPrefabContents+SaveAsPrefabAsset |
| `Assets/Prefabs/UI/Gacha/GachaHistoryRowBall.prefab` | **iter-9:** CardTop anchor→(0,1)-(0,1) sizeDelta→(181,206); Portrait anchor→(0.5,1)-(0.5,1) sizeDelta→(134.7,205); StatsPanel anchor→(0.5,0.5)-(0.5,0.5) sizeDelta→(157,130.8) LE.flexW=1 VLG.align=UpperCenter; all 5 Bars sizeDelta.x→87. **iter-7:** StatRow_Power/StatIcon Image.sprite assigned to `Assets/Art/RosterScreen/IconStrenght.png` GUID `1f43a434856f0864db10af5f5bdb34ea`. **iter-6:** CardTop LE=206; StatsPanel LE=131, ignoreLayout=false; Portrait=(157,170); all 5 Bar LE.flexibleWidth=1; Rim sprite=ItemsScreen/Rim.png | iter-9 item 13 (RT fix); iter-7 item 10a; iter-6 items 9, 10b, 11. |
| `Assets/Prefabs/UI/Gacha/GachaHistoryScreen.prefab` | Content VLG spacing: 24→0 | iter-6 item 8. Unchanged in iter-8. |
| `Docs/Specs/Active/gacha_history/screenshots/gacha_history_iter9_canonical_2026-07-15_23-56-03.png` | Canonical screenshot iter-9 (1170×2532) — ball card RT-fixed: taller image area, wider stat bars, Portrait fills CardTop | New (iter-9) |
| `Docs/Specs/Active/gacha_history/screenshots/gacha_history_iter8_canonical_2026-07-15_22-40-38.png` | Canonical screenshot iter-8 (1170×2532) — symmetric separator gaps on both CLUB and BALL rows | Historical (iter-8) |
| `Docs/Specs/Active/gacha_history/screenshots/gacha_history_iter7_canonical_2026-07-15_21-09-39.png` | Canonical screenshot iter-7 (1170×2532) — all 5 ball stat icons present | Historical (iter-7) |
| `Docs/Specs/Active/gacha_history/screenshots/gacha_history_iter6_canonical_2026-07-15_15-53-13.png` | Canonical screenshot iter-6 (1170×2532) | Historical |
| `Docs/Specs/Active/gacha_history/test_results_iter6.txt` | EditMode test results: 19 PASS, 0 FAIL | Historical |
| `Assets/Scripts/UI/Gacha/GachaTabController.cs` | Modified in Stage 1 iters 1-2 (history navigation) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/GachaTicketManager.cs` | Modified in Stage 1 (ticket type binding) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/PersistentUIManager.cs` | Modified in Stage 1 (history screen visibility) | Pre-existing Stage 1 change |
| `Assets/Scripts/Save/SaveData.cs` | Schema v8 migration (iter-2) | Pre-existing Stage 1 change |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | Schema v8 migration (iter-2) | Pre-existing Stage 1 change |
| `Assets/Scripts/Save/Tests/GachaTicketTests.cs` | Test suite for ticket schema | Pre-existing Stage 1 change |
| `Assets/Resources/Data/tickets.csv` | Ticket catalog (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/GachaHistoryRecord.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/GachaHistoryStore.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/GachaHistoryRow.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/GachaHistoryRowBall.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/GachaRewardType.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/TicketCatalog.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/UI/Gacha/TicketType.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Tests/EditMode/GachaStage1Tests.cs` | New (Stage 1) | Pre-existing Stage 1 change |
| `Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs` | Updated `schemaVersion` assertion v6→v8 — `gacha_history` Stage 1 bumped `CurrentSchemaVersion` to 8; mirror test must assert 8 | Stage 1 schema v8 migration (carried from iter-2); diff: `Assert.AreEqual(8, data!.schemaVersion, "Post-migration schemaVersion must be 8 (gacha_history Stage 1 bumped CurrentSchemaVersion to 8)")` |
| `Assets/Scripts/Save/Tests/ClubOwnershipTests.cs` | Updated `schemaVersion` assertions v7→v8 (two tests) — migration chain now lands at v8 | Stage 1 schema v8 migration (carried from iter-2) |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | Updated `schemaVersion` assertion v7→v8 — full migration chain comment updated v2→v8 | Stage 1 schema v8 migration (carried from iter-2) |
| `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` | No change from this task | Pre-existing modification present in `gitStatus` system snapshot at session start; MCP/font import artifact not introduced by gacha_history |
| `Assets/Plugins/NuGet/.nuget-installed.json` | No change from this task | Pre-existing modification (MCP plugin update); in `gitStatus` system snapshot at session start |
| `Assets/Plugins/NuGet/McpPlugin.Common.dll` | No change from this task | Pre-existing modification (MCP plugin update); in `gitStatus` system snapshot at session start |
| `Assets/Plugins/NuGet/McpPlugin.dll` | No change from this task | Pre-existing modification (MCP plugin update); in `gitStatus` system snapshot at session start |
| `Packages/manifest.json` | No change from this task | Pre-existing modification (MCP package manifest); in `gitStatus` system snapshot at session start |
| `Packages/packages-lock.json` | No change from this task | Pre-existing modification (MCP package lock); in `gitStatus` system snapshot at session start |
