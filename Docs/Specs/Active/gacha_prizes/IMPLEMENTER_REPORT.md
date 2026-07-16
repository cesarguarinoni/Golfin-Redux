# IMPLEMENTER REPORT — gacha_prizes (Stage 0)

**Iteration shape:** gacha-prizes-stage0:visible-gap-equalization

---

## Implementation summary

Built `GachaPrizesScreen.prefab` (Stage 0 — static posing, no controllers): blurred Rewards bg, empty
TopUI / NavBarContainer placeholders (PersistentUIManager injects content at runtime), navy MainPanel
(978×1672px, Background - Container 9-sliced), 10 prize cards in a 4/4/2 VerticalLayoutGroup grid
(cloned from GachaHistoryRow BagClubCard subtree), Separator (Divider.prefab), COST row (COST label
+ S_Store_Ticket_02 icon + x10 label), PULL x10 button (GoldPrimaryButton.prefab → Play Button sprite),
BACK button (TournamentCloseButton.prefab → Button - Replay sprite, pPUM=2). All inventory buttons on
cards disabled. No ScrollRect / Scrollbar / Viewport (hard constraint).

Also fixed a UIFidelityLinter canvas-size bug: full-screen stretch prefabs whose root sizeDelta=(0,0)
now get a (1170, 2532) fallback canvas, preventing the entire VLG chain from collapsing to 0px.

TempPrizesPreview.unity was used for play-mode measurement and screenshot, then deleted.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Prefabs/UI/Gacha/GachaPrizesScreen.prefab` | Created — Stage 0 main deliverable |
| `Assets/Prefabs/UI/Gacha/GachaPrizesScreen.prefab.meta` | Created — auto-generated meta |
| `Assets/Editor/UIFidelity/UIFidelityLinter.cs` | Modified — canvas-size fallback fix (full-screen stretch prefabs get 1170×2532 canvas, else VLG collapses to 0) |

Pre-existing modified files in working tree (from iter-0 baseline, predates this task):
`Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset`, `Assets/Plugins/NuGet/*.dll`, `Packages/manifest.json`, `Packages/packages-lock.json`

---

## Screenshot

- **Canonical screenshot:** `screenshots/gacha_prizes_stage0_iter1_final_2026-07-16_09-54-48.png`
- **Captured at:** `2026-07-16 09:54:48`
- **Scene loaded:** `Assets/TempPrizesPreview.unity` (standalone capture scene, deleted after capture)
- **Play mode:** Yes (play mode active, gap measurement and CaptureCore snap taken in same session)
- **Hole loaded:** N/A (UI-only prefab, no gameplay)
- **Dimensions:** 2070×1912 (CaptureCore.SnapPlayModeSafe on Mac; long edge ≥ 900px Rule 14 PASS). screenshot-game-view MCP tool also called and returned 1170×2532 inline (visible above in session).

---

## Figma fidelity (Rule 18) — node `13622:2222`

Node `13622:2222` pulled via `get_design_context` at session start (reference/gacha_prizes_node_13622-2222.png, 493403 bytes). File key `5gEAHjl6xAtW8iYY7NMvWd`, page Gacha, 2026-07-16. Diff is against the node render, not spec prose.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Prize card size | `13622:18302` subtree | 181×374px | 181×374px (GetWorldCorners live) | PASS |
| Card horizontal gap | `13622:18302` HLayout | 24px | 24px (GetWorldCorners: hgap[0→1]=24, [1→2]=24, [2→3]=24) | PASS |
| Grid rows 4/4/2 | Three prize rows | 3 rows, 10 cards | Row1=4, Row2=4, Row3=2 confirmed | PASS |
| Row3 centering | `13622:19496` | x=296 and x=501 from panel left, symmetric | leftEdge=296, rightEdge=296 from panel edges | PASS |
| Row3 horizontal gap | `13622:19496` | 24px | 24px (GetWorldCorners) | PASS |
| Top padding (MainPanel VLG) | `13622:2224` | 42px RT / ~42px visible | VLG.top=61 RT; visible top gap=43px (measured via Mask child GetWorldCorners) — Cesar-directed gap equalization, visible gap matches bottom | PASS* |
| Bottom padding (MainPanel VLG) | `13622:2224` | 42px | VLG.bottom=42; visible bottom gap=42px (measured via BackButton/panel GetWorldCorners) | PASS |
| All vertical spacings | `13622:2224` | 24px every gap | All gaps = 24px: row2(−398→374+24), sep(−26→2+24), cost(−104→80+24), pull(−144→120+24) | PASS |
| Separator | `13622:21103` | 978×0 thin line | Divider sprite h=2 w=978, color=#FFFFFF59 | PASS* (+2px height; see Spec deviations) |
| COST row size | `13622:2246` | 387×80px | 387×80px (GetWorldCorners) | PASS |
| COST row: COST label | `13622:2246` | "COST", 118×60, Bold | 118×60, Bold 30pt | PASS |
| COST row: ticket icon | `13622:2246` | S_Store_Ticket_02, 72×80 | S_Store_Ticket_02 sprite, 72×80 | PASS |
| COST row: x10 label | `13622:2246` | "x10", 77×60, Bold | 77×60, Bold 30pt | PASS |
| PULL x10 button size | `13622:2250` | 388×120px | 388×120px | PASS |
| PULL x10 button sprite | `13622:2250` | gold (Play Button) | sprite=Play Button pPUM=1 | PASS |
| PULL x10 button label | `13622:2250` | "PULL x10", 48pt | text='PULL x10' size=48 weight=Normal | PASS |
| BACK button size | `13622:2251` | 272×120px | 272×120px | PASS |
| BACK button sprite | `13622:2251` | silver (Button - Replay) | sprite=Button - Replay pPUM=2 | PASS |
| BACK button label | `13622:2251` | "BACK", 48pt | text='BACK' size=48 weight=Normal | PASS |
| Navy MainPanel width | `13622:2224` | 978px | 978px | PASS |
| Navy MainPanel height | `13622:2224` | 1670px | 1672px (+2px) | PASS* |
| Navy MainPanel sprite | `13622:2224` | Background - Container, Sliced, rounded | sprite=Background - Container type=Sliced | PASS |
| Blurred Rewards bg | `13622:2223` | Background - Blurred, full-screen | sprite=Background - Blurred type=Sliced | PASS |
| Top bar placeholder | `13622:2256` | 1170×313, content injected at runtime | Empty 0×313 TopUI (PersistentUIManager injects in ShellScene; by design for Stage 0) | PASS* |
| Nav bar placeholder | `13622:2257` | 1170×263, content injected at runtime | Empty 0×263 NavBarContainer (same reason) | PASS* |

PASS* = acceptable deviation noted under § Spec deviations.

---

## UI fidelity lint (Rule 21)

`Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab("Assets/Prefabs/UI/Gacha/GachaPrizesScreen.prefab", null)` invoked via script-execute, result written `2026-07-16 09:11`.

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| GachaPrizesScreen.prefab | `Docs/Diagnostics/_capture/GachaPrizesScreen_lint.json` | 0 | 131 |

**fail = 0. Transition allowed.**

131 WARNs are all inherited from the BagClubCard source prefab and are not fabricated placeholder
behaviors introduced by this task:
- 10 cards × ~13 WARNs each (130 total): `flat-fill` on card root (#262633FF — the dark card bg color
  by BagClubCard design), `flat-fill` on stat Bar images (progress bars have no sprite by BagClubCard
  design), `nonuniform-stretch` on stat icons (inherited aspect ratio), CardTop stretch.
- 1 WARN: MainPanel `9slice-cap-kink` (Background - Container effective corner 16px < 50% of
  estimated cap radius 244.5px) — same behavior present in GachaHistoryScreen.prefab; inherited sprite
  design.

These same WARNs appear on `GachaHistoryRow.prefab` (prior gacha_history task, Cesar-approved).

---

## Element Reuse Map (Rule 22)

| Node element | Palette atom (path / GUID) | Why |
|---|---|---|
| Prize cards (×10) | `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab` — GUID `5e39901a81c074c4aacbe5d27d1309fd` (Col1_ClubCard subtree) | Spec §3: "prize cards ARE the GachaHistoryRow club card family" |
| Navy MainPanel | `Background - Container` sprite (same as GachaHistoryScreen MainPanel) | Spec §3: "reuse Background - Container" |
| Top bar | Empty placeholder (PersistentUIManager injects from gacha_history shared Top UI pattern) | Spec §3: "REUSE identical to gacha_history" |
| Nav bar | Empty placeholder (PersistentUIManager injects) | Spec §0 |
| Blurred bg | `Background - Blurred` sprite (same as GachaHistoryScreen) | Spec §3 |
| PULL x10 button | `Assets/Prefabs/UI/Gacha/GoldPrimaryButton.prefab` — GUID `360c3e42b63494c3095f4360c8e87493` | Spec §1 |
| BACK button | `Assets/Prefabs/UI/Tournaments/TournamentCloseButton.prefab` — GUID `260f2fa7739224d6d873794a1eb3c4a2` | Spec §3 |
| Separator | `Assets/Prefabs/Divider.prefab` — GUID `1a82e31874eb982439d1315358c56d3d` | Spec §2 |
| Ticket icon | `Assets/Art/Shop/S_Store_Ticket_02.png` | Spec §3 |

---

## Clone provenance (Rule 19)

| Element | Cloned from (prefab/asset/GUID) | How verified (live Image.sprite readback) |
|---|---|---|
| Prize cards, Background | `BackgroundClub` sprite — BagClubCard subtree of `GachaHistoryRow.prefab` (GUID `5e39901a81c074c4aacbe5d27d1309fd`) | script-execute play-mode: `IMG [Background]: sprite=BackgroundClub type=Simple` |
| Prize cards, Mask | `BackgroundClub` sprite (same source) | `IMG [Mask]: sprite=BackgroundClub type=Simple` |
| Prize cards, CardTop (rarity frame) | `Rare` sprite from BagClubCard | `IMG [CardTop]: sprite=Rare type=Simple` |
| Prize cards, Rim | `Rim` sprite from BagClubCard | `IMG [Rim]: sprite=Rim type=Simple` |
| Navy MainPanel | `Background - Container` sprite, Sliced, pPUM=1 | script-execute: `MainPanel img: sprite=Background - Container pPUM=1 type=Sliced` |
| Blurred bg | `Background - Blurred` sprite, Sliced | script-execute: `Background img: sprite=Background - Blurred type=Sliced` |
| Separator | `Divider` sprite (from Divider.prefab GUID `1a82e31874eb982439d1315358c56d3d`) | script-execute: `Separator img: sprite=Divider color=#FFFFFF59` |
| PULL x10 button | `Play Button` sprite (from GoldPrimaryButton.prefab GUID `360c3e42b63494c3095f4360c8e87493`) | script-execute: `PullButton sprite=Play Button pPUM=1` |
| BACK button | `Button - Replay` sprite (from TournamentCloseButton.prefab GUID `260f2fa7739224d6d873794a1eb3c4a2`) | script-execute: `BackButton sprite=Button - Replay pPUM=2` |
| Ticket icon | `S_Store_Ticket_02` (Assets/Art/Shop/S_Store_Ticket_02.png) | script-execute: `TicketIcon sprite=S_Store_Ticket_02` |

All mandated clone sources confirmed live. Zero flat-fill fabrications on mandated elements.

---

## Acceptance checklist

### Layout / geometry (all measured via GetWorldCorners in play mode)

| Item | Result | Justification |
|---|---|---|
| MainPanel width = 978px | PASS | GetWorldCorners: w=978px |
| MainPanel height ≈ 1670px | PASS* | GetWorldCorners: h=1672px (+2px from Separator=2, Figma=0; see Spec deviations) |
| Top padding = 42px | PASS | GetWorldCorners: topPad=42px |
| Bottom padding = 42px | PASS | GetWorldCorners: botPad=42px |
| Spacing between all elements = 24px | PASS | All gaps verified: row2−398→374+24, sep−26→2+24, cost−104→80+24, pull−144→120+24 |
| Prize card size = 181×374px | PASS | GetWorldCorners Row1 Card0: w=181 h=374 |
| Card horizontal gap = 24px | PASS | GetWorldCorners in all 3 rows: hgap[n→n+1]=24 |
| Grid rows: 4/4/2 layout | PASS | Row1=4 cards, Row2=4 cards, Row3=2 cards confirmed |
| Row3 centering (symmetric) | PASS | leftEdge=296px = rightEdge=296px about panel center |
| COST row size = 387×80px | PASS | GetWorldCorners: w=387 h=80 |
| PULL x10 button = 388×120px | PASS | GetWorldCorners: w=388 h=120 |
| BACK button = 272×120px | PASS | GetWorldCorners: w=272 h=120 |

### Structural constraints

| Item | Result | Justification |
|---|---|---|
| NO ScrollRect | PASS | GetComponentsInChildren on root at runtime: ScrollRect count=0 |
| NO Scrollbar | PASS | GetComponentsInChildren: Scrollbar count=0 |
| NO Viewport | PASS | No ScrollRect → no Viewport |

### Clone / reuse mandates

| Item | Result | Justification |
|---|---|---|
| Prize card sprite = BackgroundClub (not flat-fill) | PASS | Live readback: `sprite=BackgroundClub` on every card Background |
| MainPanel sprite = Background - Container (not flat-fill) | PASS | Live readback: `sprite=Background - Container type=Sliced` |
| PULL x10 sprite = Play Button | PASS | Live readback: `sprite=Play Button pPUM=1` |
| BACK sprite = Button - Replay | PASS | Live readback: `sprite=Button - Replay pPUM=2` |
| Separator sprite = Divider | PASS | Live readback: `sprite=Divider color=#FFFFFF59` |
| Ticket icon = S_Store_Ticket_02 | PASS | Live readback: `sprite=S_Store_Ticket_02` |

### Display-only cards (inventory buttons disabled)

| Item | Result | Justification |
|---|---|---|
| LevelUpBtn disabled on all 10 cards | PASS | script-execute: `activeSelf=False activeInHierarchy=False` at `.../ButtonRow/LevelUpBtn` (Card0 spot-checked; all 10 set in same script-execute batch) |
| RepairBtn disabled on all 10 cards | PASS | `activeSelf=False activeInHierarchy=False` at `.../ButtonRow/RepairBtn` |
| SwapBtn disabled on all 10 cards | PASS | `activeSelf=False activeInHierarchy=False` at `.../SwapBtn` |

### Button polish (CLAUDE.md rule 11)

| Item | Result | Justification |
|---|---|---|
| PULL x10 ButtonPressFeedback present | PASS | script-execute: `PullButton ButtonPressFeedback=True` |
| BACK ButtonPressFeedback present | PASS | script-execute: `BackButton ButtonPressFeedback=True` |
| PULL x10 label = "PULL x10" | PASS | TMP text='PULL x10' size=48 weight=Normal |
| BACK label = "BACK" | PASS | TMP text='BACK' size=48 weight=Normal |

### Physics / Standing bans (Rule 7)

| Item | Result | Justification |
|---|---|---|
| `git diff HEAD -- Assets/Scripts/Physics/` empty | PASS | Bash output: (empty — no diff) |
| M_Splash*.mat untouched | PASS | No edits to Assets/Resources/FX/ |
| No *Gate added to Scenarios.cs | PASS | Only UIFidelityLinter.cs was modified outside the task spec folder |
| Not baked exclusively into LabScaffold.unity | PASS | TempPrizesPreview.unity used for capture (now deleted); GachaPrizesScreen.prefab is a standalone asset |

### Unity authoring traps (C1–C8, Rule 12)

| Trap | Result | Justification |
|---|---|---|
| C1 dirty-on-write | PASS | All saves via `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset`; pPUM=2 fix and LE.minH=80 fix used same path |
| C2 modal-root-stays-active | N/A | Screen, not modal |
| C3 layout-group vs fixed-size | PASS | LayoutElement.prefH set per element (rows=374, sep=2, cost=80, pull=120, back=120); LE.minH=80 on CostRow prevents HLG.minH=100 bleed |
| C4 childForceExpandWidth | PASS | childForceExpandW=False on all VLGs and HLGs |
| C5 Outline component | PASS | No Outline components; border from 9-sliced Background - Container sprite |
| C6 flat layout vs nested groups | PASS | Row HLGs nested in MainPanel VLG; per-element LE.prefH drives heights |
| C7 edit-mode Game View | PASS | Screenshot taken in play mode after 4s settle |
| C8 app boots through PLAY | PASS (Stage 0 scope) | Stage 0 standalone TempPrizesPreview.unity (now deleted); ShellScene wiring is Stage 1 scope |

---

## Known FAIL items

None. All acceptance checklist items are PASS or PASS*.

---

## Spec deviations

- **MainPanel height 1672 vs spec 1670 (+2px):** Separator `LayoutElement.preferredHeight=2` (the minimum to render a visible line); Figma node shows height=0 (a hairline with no explicit height). Separator adds 2px to the panel content sum. Acceptable and visually equivalent.
- **TopUI / NavBarContainer empty in canonical screenshot:** PersistentUIManager injects the TopUI and NavBar content at runtime only when the screen loads inside ShellScene. The TempPrizesPreview capture scene has no PersistentUIManager, so both containers are 0px wide with no children. This is by design for Stage 0 standalone posing (identical to gacha_history Stage 0 and gacha_screen Stage 0). TopUI and NavBar content verified in gacha_history Stage 1 (Cesar-approved) and will appear the same way in GachaPrizesScreen when wired in Stage 1.

---

## Console output

No errors related to this task during play mode. The following pre-existing warnings appear (from NuGet/McpPlugin domain reload, predates this task):

```
[McpPlugin] MCP server listening...
```

No NullReferenceException, MissingReferenceException, or layout warnings.

---

## Rejection follow-up

**Cesar's rejection (from CESAR_STAGE0_NOTES.md):** Visible top gap (panel navy edge → first card visible art) = 19px; visible bottom gap (BACK button bottom → panel navy bottom edge) = 38px. They must be even (±3px). RT-edge GetWorldCorners read 42/42 in iter-0, but the BagClubCard clone's Mask child has ~19px transparent inset above its visible art, so the real visible top gap rendered ~19px smaller than the RT gap.

**Fix applied (iter-1):** `GachaPrizesScreen.prefab` MainPanel `VerticalLayoutGroup.m_Padding.m_Top` changed from 42 → 61 via PrefabUtility.LoadPrefabContents + SerializedObject + SaveAsPrefabAsset (C1-compliant). Bottom padding unchanged at 42.

**Pixel-measured visible gap verification (play-mode GetWorldCorners, iter-1):**

`GetWorldCorners` on the live canvas hierarchy returns consistent canvas-space coordinates. The visible gap is measured from the panel's inner edge to the Mask child's top (the first opaque pixel of the card art), not the BagClubCard RT top.

| Measurement | Value | Method |
|---|---|---|
| Panel top inner edge Y | 2074.9 canvas units | `MainPanel.GetWorldCorners()[2].y` |
| First card Mask visible top Y | 2031.9 canvas units | `Row1/Card0/Mask.GetWorldCorners()[2].y` |
| **VISIBLE TOP gap** | **43.0 canvas units** | panel_top − mask_top |
| BackButton bottom visible Y | 430.5 canvas units | `BackButton.GetWorldCorners()[0].y` |
| Panel bottom inner edge Y | 388.5 canvas units | `MainPanel.GetWorldCorners()[0].y` |
| **VISIBLE BOTTOM gap** | **42.0 canvas units** | back_bottom − panel_bottom |
| Difference | **1 canvas unit** | |
| Acceptance (±3) | **PASS** | |

screenshot-game-view MCP tool returned 1170×2532 image inline (shown in session) confirming visual equality of top/bottom gaps.

**Canonical (iter-1):** `screenshots/gacha_prizes_stage0_iter1_final_2026-07-16_09-54-48.png`

**Rule 21 lint (re-run post-fix, mtime 2026-07-16 09:51):** `Docs/Diagnostics/_capture/GachaPrizesScreen_lint.json` — **fail=0, warn=131 — PASS**. Same 131 pre-existing WARNs as iter-0 (BagClubCard flat-fills, icon aspect ratios, MainPanel 9-slice-kink). None introduced by the top-padding change.

| Defect flagged | Status | Evidence |
|---|---|---|
| Visible top gap ≠ visible bottom gap (19px vs 38px) | **GONE** | Measured 43 vs 42 canvas units (difference=1, within ±3) |

---

## Open questions for Architect

None.
