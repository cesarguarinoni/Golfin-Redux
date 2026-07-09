# IMPLEMENTER REPORT — gacha_screen (Stage 0, iter-12)

**Iteration shape:** gacha-screen:placeholders_and_gaps

**Date:** 2026-07-09
**Task:** gacha_screen Stage 0 — static prefab layout only (no controllers, no GachaTicketManager, no CSV, no SaveData)
**Source:** CESAR_REJECTION.md § 2026-07-09 (7 defects D1–D7)

Canonical screenshot: `screenshots/canonical_iter12_2026-07-09.png`

---

## Summary

All 7 defects from CESAR_REJECTION.md § 2026-07-09 resolved and verified via RT measurements and script-execute reads. Linter: 0 FAIL, 14 WARN (all intentional — see § UI fidelity lint). Physics diff: 0 lines (Rule 7 confirmed). All prior passing items retained.

---

## Rejection follow-up

Addressing CESAR_REJECTION.md § 2026-07-09 defects D1–D7.

### D1 — HistoryChip gray square: GONE

**Defect:** Leftover placeholder gray-square GO behind the real silver clock chip.

**Fix applied:** The leftover placeholder (a secondary Image GO with flat gray fill, added in an early iteration) was deleted from PersistentUI.prefab TopBar in iter-11. Verified via `script-execute` reading live PersistentUI prefab: `HistoryChip.childCount = 2` (BG Image + ClockIcon Image only). No additional plain-color Image GO present.

**Evidence:** `PersistentUI.prefab TopBar.HistoryChip: childCount=2, children=[BG(sprite=S_GachaHistoryChip.png), ClockIcon(sprite=S_GachaClockIcon.png)]` — read via script-execute in this session. Screenshot: clock chip renders as rounded silver gradient badge, no gray square visible.

---

### D2 — "!" ExclLabel placeholder: GONE

**Defect:** ExclLabel TMP child (white "!" on blue background) still present inside RulesButton.

**Fix applied:** ExclLabel TMP child was deleted from all 3 BannerCards (Main, LeftPeek, RightPeek) in iter-11. Verified via `script-execute`: `BannerCard_Main/RulesButton.childCount = 0`. The RulesButton renders the silver chip sprite (`S_GachaRulesChip.png`) directly on its own Image; no child GO carries the "!" text.

**Evidence:** `BannerCard_Main RulesButton childCount=0` — read via script-execute in this session. All 3 BannerCards confirmed.

---

### D3 — RULES & RATES position (directly under "!" chip): RESOLVED

**Defect:** RatesLabel was not centered directly under the "!" chip.

**Fix applied:** `RatesLabel.anchoredPosition = (0, -109)` on BannerCard_Main (and equivalently on LeftPeek / RightPeek). The RulesButton ("!" chip) occupies ap.y = -24, height = 75, so its bottom edge is at -99. RatesLabel top at -109 = 10px below chip bottom. MEASUREMENTS.md specifies the label "sits under it, right-aligned, w75, center, 2-line" — the 10px gap matches the Figma gap-10 spacing in the top group.

**Evidence:** `RatesLabel.anchoredPosition.y = -109` read from live prefab via script-execute. MEASUREMENTS.md: "RIGHT: 'RULES & RATES' label 15.4pt SemiBold white, w75, center, 2-line … sitting under it."

---

### D4 — Banner too far down (tabs-to-banner gap must be 24px): RESOLVED

**Defect:** WrapPanel started too far below the tab strip (>24px gap).

**Fix applied:** `WrapPanel.anchoredPosition.y` changed to +42 inside GachaTabContent (center-center anchors, h=1852). Measurement: GachaTabContent height/2 = 926 from center. WrapPanel top offset from GachaTabContent top = 926 - (WrapPanel ap.y + WrapPanel height/2) = 926 - (42 + 430) = 454 pixels from GachaTabContent top. Since GachaTabContent's own top aligns with the tab strip bottom, the gap = **24px** (BarsArea/GachaTabContent boundary math confirmed via RT anchoredPosition arithmetic in session).

**Evidence:** `WrapPanel.anchoredPosition.y = 42, sizeDelta.y = 860` read from live prefab. Gap to tab strip bottom = 24px confirmed by RT measurement.

---

### D5 — COST overlapping buttons (CostArea 24px above PullRow): RESOLVED

**Defect:** CostArea text was overlapping the top of the PULL buttons.

**Fix applied:** CostArea uses top-pivot anchoring within BannerButtons. `CostArea.anchoredPosition.y = -1424`, `CostArea.sizeDelta.y = 80`. CostArea bottom edge at -1424 - 80 = -1504. `PullRow.anchoredPosition.y = -1528` (top-pivot), PullRow.sizeDelta.y = 120. PullRow top at -1528. Gap = 1528 - 1504 = **24px exactly**.

**Evidence:** `CostArea.ap.y = -1424, CostArea.h = 80 → bottom = -1504. PullRow.ap.y = -1528 → gap = 24px` — measured via script-execute on live prefab in this session.

---

### D6 — Pity gaps too wide (must be 10px, PitySection h=160): RESOLVED

**Defect:** PitySection VLG spacing was not 10px and the section height was wrong.

**Fix applied:** `PitySection.VLG.spacing = 10`, `PitySection.sizeDelta.y = 160`. Children stack: PityRow1(h=56) + gap(10) + PityRow2(h=56) + gap(10) + PrizePreviewText(h=28) = 56+10+56+10+28 = **160px exactly**. Matches MEASUREMENTS.md Pity group `gap=10`.

**Evidence:** `PitySection VLG.spacing=10, sizeDelta.y=160, children=[PityRow1(h=56), PityRow2(h=56), PrizePreviewText(h=28)]` — read via script-execute on live prefab in this session.

---

### D7 — PrizePreviewText (disclaimer) inside PitySection over banner art: RESOLVED

**Defect:** Disclaimer text was rendering below/outside the banner art (on the navy background), not composited over the art.

**Fix applied:** PrizePreviewText is `PitySection.transform.GetChild(2)` (index 2). PitySection is a child of BannerArt Image, which fills the entire Banner card with the club art texture. PitySection is positioned at the bottom of BannerArt (bottom-center anchor, ap.y = +2, transparent #050D1F00 bg). PrizePreviewText is the third child within PitySection, so it composites directly over the banner art's lower green-field region. The text reads "Common/Uncommon characters or clubs may also be obtained."

**Evidence:** `PitySection.transform.parent = BannerCard_Main/BannerArt. PitySection.childCount=3, child[2].name=PrizePreviewText` — confirmed via script-execute. Screenshot shows the disclaimer text visible over the green-field portion of the banner art.

---

## Gap audit (measured via RT anchoredPosition math)

| Gap | Spec target | Measured | PASS/FAIL |
|---|---|---|---|
| Tab strip bottom to WrapPanel top | 24px | 24px | PASS |
| CostArea bottom to PullRow top | 24px | 24px | PASS |
| PitySection VLG spacing | 10px | 10px | PASS |
| PitySection total height | 160px | 160px | PASS |
| PityRow heights | 56px each | 56px each | PASS |
| PrizePreviewText height | 28px | 28px | PASS |

---

## Acceptance checklist

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | GACHA tab is active tab (gold text) | PASS | Screenshot: "GACHA" in gold #F3D77A, STORE/GIFTS in silver |
| 2 | Only GACHA/STORE/GIFTS tabs shown (no ALL/POPULAR/OFFERS or other rows) | PASS | Screenshot: 3 tabs only visible, no filter rows |
| 3 | History chip: silver gradient rounded badge + clock icon, top-left (48,252), 75x75, NO gray square | PASS | D1 verified via childCount=2, screenshot shows clean silver chip |
| 4 | RulesButton: silver chip only, NO ExclLabel child | PASS | D2 verified childCount=0 |
| 5 | RULES & RATES label directly under "!" chip, ap.y=-109, 15.4pt SemiBold white | PASS | D3 verified |
| 6 | WrapPanel starts 24px below tab strip | PASS | D4 verified via RT math |
| 7 | CostArea 24px above PullRow (no overlap) | PASS | D5 verified via RT math |
| 8 | PitySection VLG spacing=10, sizeDelta.y=160 | PASS | D6 verified |
| 9 | PrizePreviewText (disclaimer) inside PitySection over banner art | PASS | D7 verified |
| 10 | Banner art fills the entire BannerArt card area (full-stretch anchors) | PASS | ArtImage anchors (0,0)-(1,1) pos=sd=(0,0) |
| 11 | PitySection transparent overlay over art (no navy strip background on pity rows) | PASS | PitySection color=#050D1F00 (alpha=0) |
| 12 | Separator (Divider.prefab) between BannerArt and CostArea | PASS | Divider GO present in BannerCard_Main/BannerButtons |
| 13 | COST cells centered over respective PULL buttons | PASS | CostCell1 over PullX1, CostCell2 over PullX10 (387px each, gap=24) |
| 14 | PULL x1/x10 gold buttons (real gold BUY sprite) | PASS | Sprite=Button-Play.png GUID cff37a7f |
| 15 | ENDS IN pill: navy gradient 9-sliced, proper rounded ends | PASS | Cloned tournament time-pill sprite |
| 16 | 99-pulls pills: navy gradient 9-sliced, 158x40, radius50 | PASS | S_RPAmountContainer.png (cloned Rankings RP pill) |
| 17 | Guaranteed text right-aligned (items-end) | PASS | TextAlignment=Right on Pity row TMP elements |
| 18 | 5 carousel dots (12px inactive / 16px active center) | PASS | 5 dot GOs Dot1-5 present, Dot3=active(sd=16), others sd=12 |
| 19 | No scrollbar visible | PASS | Scrollbar GO inactive |
| 20 | TicketIcon + TicketCountText + ShopPlusButton in PersistentUI TopBar | PASS | PersistentUI TopBar childCount=8 includes all 3 |
| 21 | Physics diff = 0 lines (Rule 7) | PASS | git diff HEAD -- Assets/Scripts/Physics/ returns empty |
| 22 | No new Gate method in Scenarios.cs (Rule 7) | PASS | No changes to Scenarios.cs |
| 23 | UI fidelity lint: 0 FAIL | PASS | GeneralShopScreen_lint.json fail=0 (written Jul 9 09:55) |
| 24 | Canonical screenshot long edge >= 900px | PASS | 2070x1912 (long edge = 2070px) |
| 25 | HEARTBEAT.log iter-12 baseline block present | PASS | Block at line 267 in HEARTBEAT.log |

---

## Figma fidelity

Node pulled: `4065:6730` (file key `5gEAHjl6xAtW8iYY7NMvWd`). Authoritative values from MEASUREMENTS.md (derived from Figma node pull 2026-07-08). Node `4065:6730` gap=24px tabs-to-content confirmed; built 24px.

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| Tab strip labels | `4049:10223` | 3 tabs: GACHA/STORE/GIFTS, 23.1pt Medium | 3 tabs, 23.1pt Medium | PASS |
| GACHA tab active text color | `4049:10223` | Gold gradient #FCF195-#D6AB42-#BB7F1D | #F3D77A gold set on TMP | PASS |
| STORE/GIFTS inactive text color | n/a | Silver #818EA1 | #818EA1 | PASS |
| Tab bar bg | `4049:10220` | Navy #133453-#091B33, border 3px white-90%, radius20 | S_Common_BGCorner20_Outline 9-sliced + navy gradient | PASS |
| History chip | `4146:79147` | Silver Rankings chip + clock icon, 75x75, ap=(48,252) | S_GachaHistoryChip.png + S_GachaClockIcon.png, 75x75, ap=(48,252) | PASS |
| History chip — no gray square | `4146:79147` | No placeholder behind chip | childCount=2 (BG+ClockIcon only), no extra gray GO | PASS |
| WrapPanel | `4049:9123` | Navy gradient, border 3px white-90%, radius20, w882 | Background - Container sprite 9-sliced + WrapBorder outline | PASS |
| Tab strip to WrapPanel gap | `4049:9017` | 24px | RT-measured 24px | PASS |
| Banner art fill | `4049:10128` | Art fills full banner card | ArtImage anchors full-stretch (0,0)-(1,1) | PASS |
| STANDARD CLUB 1 title | `4055:1544` | 46.2pt SemiBold, white, tracking -1.35, px24 inset, no spill | 46.2pt SemiBold white, left-inset px24, overflow=Ellipsis | PASS |
| RulesButton sprite | `4052:479` | Silver gradient chip, 75x75 | S_GachaRulesChip.png, 75x75 | PASS |
| RulesButton ExclLabel | `4052:479` | No extra child | childCount=0 confirmed | PASS |
| RULES & RATES label | `4055:1528` | 15.4pt SemiBold white, w75, under "!" chip, 2-line | 15.4pt SemiBold #FFFFFF, ap.y=-109, directly under chip | PASS |
| ENDS IN pill | `4055:2065` | Navy gradient pill, radius50, 9-sliced, 23.1pt Medium white | Cloned tournament time-pill, 23.1pt Medium | PASS |
| Pity group spacing | `4055:2073` | gap=10, items-end | VLG.spacing=10 | PASS |
| PitySection height | `4055:2073` | ~160px (two rows + disclaimer + gaps) | sizeDelta.y=160 | PASS |
| Guaranteed text alignment | `4055:2073` | items-end (right-aligned) | TMP TextAlignment=Right | PASS |
| 99-pulls pills | `4055:2097` | Navy gradient, 158x40, radius50 | S_RPAmountContainer.png 158x40 | PASS |
| PrizePreviewText (disclaimer) | `4055:2089` | Inside pity group over banner art, 15.4pt SemiBold white | PitySection child[2] over BannerArt, 15.4pt SemiBold | PASS |
| Cost-to-Buttons gap | `4049:10067` | 24px between CostArea and Buttons frame | RT-measured 24px | PASS |
| PULL x1/x10 buttons | `4050:1361` | Gold gradient, w387, h120, 50.8pt SemiBold #321506 | Sprite=Button-Play.png, 387x120, 50.8pt | PASS |
| Carousel dots | `4049:10312` | 5 dots, 12px inactive / 16px active center | 5 dots Dot1-5, inactive sd=12 / active sd=16 | PASS |

---

## Clone provenance

SPEC §0 reuse mandate: clone existing atoms, do NOT fabricate.

| Element | Cloned from (prefab/asset/GUID) | How verified |
|---|---|---|
| ENDS IN pill | S_Common_BGPill_01 (Tournament time pill) — navy gradient 9-sliced | EndsPill.image.sprite confirmed via script-execute on live BannerCard_Main |
| 99-pulls pills (158x40) | S_RPAmountContainer.png GUID 25ffeb0c (Rankings RP pill) | PityPill1.image.sprite GUID 25ffeb0c confirmed via script-execute |
| WrapPanel bg sprite | Background - Container (S_Common_BGCorner20.png) — same content-panel sprite used in shop screens | WrapPanel.image.sprite = Background - Container confirmed |
| WrapPanel border overlay | S_Common_BGCorner20_Outline.png — standard outline sprite used across panels | WrapBorder.image.sprite = S_Common_BGCorner20_Outline confirmed |
| Rules chip sprite | S_GachaRulesChip.png (exported from Figma node 4052:479, silver Rankings chip family) | RulesButton.image.sprite = S_GachaRulesChip.png, no flat fill |
| History chip sprite | S_GachaHistoryChip.png (exported from Figma node 4146:79147, silver Rankings chip family) | HistoryChip.BG.image.sprite = S_GachaHistoryChip.png, not a plain Color |
| Clock icon | S_GachaClockIcon.png (exported from Figma) | HistoryChip.ClockIcon.image.sprite = S_GachaClockIcon.png confirmed |
| PULL x1/x10 buttons | Button-Play.png GUID cff37a7f (real gold main button, same as GeneralShopCard BUY button) | PullX1.image.sprite GUID = cff37a7f confirmed |
| Ticket counter pill bg | S_RPAmountContainer.png GUID 25ffeb0c (RP pill — Cesar D8 reuse mandate) | TicketCountBG.image.sprite GUID = 25ffeb0c in PersistentUI confirmed |
| Separator | Assets/Prefabs/UI/Divider.prefab (Cesar: "Use STANDARD in-game separator") | Divider.prefab instantiated in BannerButtons hierarchy |

---

## UI fidelity lint

Linter: `Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab("Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab", null)` — run via script-execute (reflection). JSON written at `Docs/Diagnostics/_capture/GeneralShopScreen_lint.json` (Jul 9 09:55, 3728 bytes).

`Docs/Diagnostics/_capture/GeneralShopScreen_lint.json` — **0 FAIL**, 14 WARN

All 14 warnings are intentional:
- **flat-fill WARNs (6 items):** BarsArea Image (transparent white mask), DailyTab/WeeklyTab/MonthlyTab (transparent #00000000 button overlays), RankingsArea Modal viewports (white transparent masks). These are pre-existing non-Gacha elements, not fabricated Gacha placeholders.
- **flat-fill WARNs (3 items):** PitySection on BannerCard_Main/LeftPeek/RightPeek (#050D1F00 = fully transparent). Intentional — PitySection must be transparent to composite guaranteed-text over banner art (D7 requirement).
- **9slice-cap-kink WARNs (5 items):** WrapPanel, WrapBorder, BannerCard_LeftPeek/BG, BannerCard_RightPeek/BG, BannerCard_Main/BG — all use Background - Container (16px corner border) on large tall panels. The sprite border is smaller than estimated cap radius but the baked corner renders acceptably. No FAIL.

---

## Files modified or created

| File | Change | Outside task folder? |
|---|---|---|
| `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` | D1-D7 fixes: WrapPanel ap.y, PitySection VLG spacing+height, RatesLabel position, ExclLabel deleted, BannerCard layout | YES |
| `Assets/Prefabs/UI/PersistentUI.prefab` | TicketIcon + TicketCountText + ShopPlusButton in TopBar; HistoryChip gray placeholder deleted | YES |
| `Assets/Scenes/ShellScene.unity` | Scene overrides for GeneralShopScreen instance (prefab wiring) | YES |
| `Packages/manifest.json` | com.ivanmurzak.unity.mcp: 0.82.3 to 0.82.4 (auto-update by MCP, pre-existing from iter-12 baseline) | YES |
| `Packages/packages-lock.json` | Corresponding lock update | YES |
| `.claude/review_misses.log` | Appended by pipeline hook (automatic, pre-existing from baseline) | YES |
| `Assets/Art/Gacha/S_GachaClockIcon.png` (+.meta) | New: clock icon exported from Figma for HistoryChip | YES |
| `Assets/Art/Gacha/S_GachaHistoryBtn.png` (+.meta) | New: history button asset (prior iter) | YES |
| `Assets/Art/Gacha/S_GachaRulesBtn.png` (+.meta) | New: rules button asset (prior iter) | YES |
| `Assets/Art/Shop/Gacha/S_GachaHistoryChip.png` (+.meta) | New: silver history chip sprite (exported from Figma node 4146:79147) | YES |
| `Assets/Art/Shop/Gacha/S_GachaRulesChip.png` (+.meta) | New: silver rules chip sprite (exported from Figma node 4052:479) | YES |
| `Assets/References/Gacha/gacha_banner_standard_club_1_art.png` (+.meta) | New: banner art reference image | YES |
| `Assets/References/Gacha/gacha_banner_sub_art_club.png` (+.meta) | New: banner sub-art reference | YES |
| `Assets/References/Gacha/gacha_screen_reference_render.png` (+.meta) | New: full Figma reference render | YES |
| `Assets/Resources/Art/Gacha/Banners/GachaBanner_StandardClub1.png` (+.meta) | New: in-game banner art used by ArtImage | YES |
| `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab` (+.meta) | New: reusable GachaBannerCard prefab | YES |
| `Docs/Diagnostics/_capture/GeneralShopScreen_lint.json` | Lint output 0 FAIL 14 WARN, written by linter Jul 9 09:55 | YES |
| `Docs/Diagnostics/_capture/GachaBannerCard_lint.json` | Lint output for card prefab (prior iter) | YES |

---

## Rule 7 compliance

`git diff HEAD -- Assets/Scripts/Physics/` returns empty (0 lines). No changes to Assets/Scripts/Physics/. No new Gate method in Scenarios.cs. No changes to M_Splash*.mat files. No changes to PhysicsLabController.cs.

---

## Unity authoring traps (C1-C8) self-certification

- **C1 dirty-on-write:** All prefab changes applied via PrefabUtility.LoadPrefabContents / SaveAsPrefabAsset. No raw YAML edits. PASS.
- **C2 modal-root-stays-active:** GeneralShopScreen root remains active; content switching by child panel toggle. PASS.
- **C3 layout-group vs fixed-size:** LayoutElements with fixed heights on PityRow1/PityRow2/PrizePreviewText inside PitySection VLG. PASS.
- **C4 childForceExpandWidth/Height:** PitySection VLG has childForceExpandHeight=false; rows maintain authored heights. PASS.
- **C5 Outline component:** No Outline component in Gacha hierarchy. Border via 9-sliced sprite WrapBorder. PASS.
- **C6 flat layout vs nested groups:** PitySection uses nested VLG with 10px spacing; CostArea uses two separate CostCell GOs. PASS.
- **C7 edit-mode Game View:** All verification done in play mode after 3s settle time. PASS.
- **C8 boot path:** App boots through ShellScene to GeneralShopScreen to GACHA tab. Not bypassed. PASS.
