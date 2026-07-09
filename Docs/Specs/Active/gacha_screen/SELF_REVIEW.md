# Self Review — gacha_screen (Stage 0, iter-7)

Reviewer: golfin-self-reviewer
Timestamp: 2026-07-08 19:41 CEST
Iteration count for this review chain: N=7 (many prior FAILs recorded; MEASUREMENTS.md is authoritative and was introduced 2026-07-08).
Canonical screenshot reviewed: `screenshots/gacha_iter7_canonical.png` (2070×1912; embeds Unity editor chrome — noted, not sole fail basis).

---

## Visual diff notes (Step 1 — pixel-first, before consulting spec)

Inner iPhone-14 game view (chrome ignored):
- Top navy bar: R-coin "73,900" pill left; gold coin "999" + gold "+" center-right; white gear far right.
- "REWARDS CENTER" centered under top bar.
- Small silver-square chip with a clock icon at TOP-LEFT, tucked between the top bar and the tab strip.
- Tab bar reads "GACHA | STORE | GIFTS" with vertical dividers. STORE is highlighted gold (active). GACHA/GIFTS silver.
- Central banner card inside a navy wrap panel; peek copies of the same card visible left and right at reduced scale.
- Banner top: solid blue rectangle carrying "STANDARD CLUB 1" (white, thick strokes) top-left, dark "ENDS IN: 1d 5h 25m 05 s" pill below the title, tiny silver "!" chip top-right with a faded "RULES & RATES" fragment below-right of it.
- Banner middle/bottom: magenta "GET Drivers, Woods, Irons" band, blue upper art with two clubs (Fyloe/Royal Swing) and "MAX POWER" text, "CHANCE TO GET LEGENDARY GEAR!" band, then a green-field lower half with two more clubs (Mieo/Tifto) and "MAX POWER".
- **Directly under the banner art there is an opaque near-black navy strip spanning wall-to-wall of the wrap panel** carrying two "Guaranteed …" text rows + two right-aligned "99 pulls" navy pills, then a small centered "Common/Uncommon…" disclaimer.
- Thin separator line above the COST row.
- COST row: left cell "COST [ticket-icon] x1", right cell "COST [ticket-icon] x10", each visually centered over the button below.
- Two gold buttons "PULL x1" and "PULL x10" side by side with a visible gap.
- Five small circle dots under the buttons.
- Bottom nav untouched.

---

## Figma fidelity (per-element numeric table vs MEASUREMENTS.md)

Node reconciled: `4065:6730`. Reference render: `reference/gacha_screen_FULL_reference.png`. Font-size PT read directly from `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab` (`fontSize:` values matched by `m_Name:` context). fontStyle=0 (Normal) on every TMP — weight comes from font asset selection (Rubik-SemiBold SDF or Rubik-VariableFont Medium). Font-asset GUID cross-check not deep-audited here; implementer table cites `39fb7824…` SemiBold and `0e84913c…` Medium and no Bold-vs-SemiBold visual mismatch is apparent at cap-height in the canonical.

| Element | MEASUREMENTS spec | Built (read-back) | Result |
|---|---|---|---|
| STANDARD CLUB 1 title (font+weight) | 46.2pt SemiBold, tracking -1.35, px24 left inset | fontSize=46.2 fontStyle=0, BannerTitle offsetMin.x=24 (per report) | PASS |
| ENDS IN countdown text | 23.1pt Medium | fontSize=23.1 fontStyle=0 (CountdownLabel) | PASS (weight rendered via font asset) |
| ENDS IN pill sprite | 9-sliced navy stadium (spec: reuse Tournament pill; report uses `RP Amount Container` GUID `25ffeb0c` ppu=6) | Sprite present, rounded ends visible in canonical | PASS (deviation from named source noted — same shape family, ppu=6 avoids corner collapse) |
| "!" glyph on rules chip | 36.9pt SemiBold | fontSize=36.9 fontStyle=0 (ExclLabel) | PASS |
| RULES & RATES label — font size | 15.4pt SemiBold | fontSize=15.4 fontStyle=0 (RatesLabel, this-iter fix from 8) | PASS numerically |
| RULES & RATES label — visibility/position | 2-line, right-aligned, w75, OUTSIDE the "!" chip | Sibling to RulesButton at 15.4pt; canonical shows faint/legible-but-washed-out fragment below chip. Position matches reference | OBSERVATION (visible but visibly fainter than reference; not called a hard FAIL — legibility can be re-checked in a clean-chrome recapture) |
| Guaranteed A/S rows | 23.1pt Medium, positioned OVER banner art (green-field) | fontSize=23.1 fontStyle=0 (PityLabel); **PitySection anchored below ArtImage bounds (see below) — text is NOT over the art** | **FAIL** — position violates MEASUREMENTS.md L69 |
| "99" pity counter | 23.1pt Medium | fontSize=23.1 fontStyle=0 (PityCount) | PASS |
| " pulls" pill text | 15.4pt SemiBold | fontSize=15.4 fontStyle=0 (per prefab) | PASS |
| 99-pulls pill sprite (×2) | Rankings RP pill `#RP Amount Container` GUID `25ffeb0c`, 9-sliced, 158×40 | Report cites RP Amount Container sprite, sizeDelta=(158,40); canonical shows rounded stadium | PASS |
| Disclaimer text | 15.4pt SemiBold, w882, center — OVER the art | fontSize=15.4 fontStyle=0 (PrizePreviewText); currently a child of PitySection which sits BELOW ArtImage | **FAIL** — position violates MEASUREMENTS.md L76 |
| **PitySection background** | **No background fill — content sits directly over banner art** (Cesar #2, MEASUREMENTS.md L69/L76) | **Opaque `#050D1FFF` fill (linter finding on all 3 banner copies); ArtImage anchorMin.y=0.137, PitySection anchoredPos.y=-1135 sd.y=130 → PitySection lives in the ~14% strip BELOW the ART bounds, painted opaque near-black navy** | **FAIL — hard, Cesar-#2 recurrence** |
| Separator | ~2px line between banner and COST | Separator GO present in GeneralShopScreen prefab; visible in canonical | PASS |
| COST / x1 / x10 | 34.6pt SemiBold, order COST→icon→x1, each cell centered over its PULL | fontSize=34.6 fontStyle=0 (CostText/CountLabel); 2 cells, each ≈centered over PULL button in canonical | PASS |
| PULL x1 / PULL x10 text | 50.8pt SemiBold, color `#321506` | fontSize=50.8 fontStyle=0 (Label ×2); dark-brown text on gold visible | PASS |
| PULL buttons sprite | Reuse gold Main Button (`Play Button` GUID `cff37a7f`), w387 h120 | Report cites Play Button sprite, sizeDelta=(387,120); gold visible | PASS |
| Wrap panel — sprite/geometry | Navy gradient, radius 20, w882, pb48 | Sprite `BackgroundCardsContainer`, w=882 | PASS-with-deviation |
| **Wrap panel — 3px white-90% border** | **Explicit 3px `rgba(255,255,255,0.9)` border, radius 20** | **BackgroundCardsContainer is not 9-sliced; linter WARN nonuniform-stretch 39%; the 3px crisp border is NOT visibly resolved as a discrete stroke in the canonical** | **FAIL** — MEASUREMENTS.md L56 explicit spec token absent (same failure class as `1v1_ingame_ui` Rule 18 miss) |
| Tab bar sprite/geometry | Navy gradient, 3px white-90% border, radius 20, w1074 | Present; STORE reads gold (Stage-0 known limitation — no controller yet) | PASS (STORE-active is documented Stage-0 gap, not a fail basis) |
| Tab labels GACHA/STORE/GIFTS | 23.1pt Medium | Report cites 23.1pt Medium Normal | PASS |
| Only 3 tabs (no STORE filter rows on Gacha) | GACHA/STORE/GIFTS only | GachaTabContent has no BarsArea/filter children (verified via linter path listing) | PASS |
| History chip position | Absolute (48, 252), 75×75, top-left | anchor=(0,1), pivot=(0,1), pos=(48,-252), sd=(75,75) | PASS |
| History chip content | Silver chip + clock icon 60×60, NO "HISTORY" text | Sprite `Background#2` GUID `3dea690e`, ClockIcon child 60×60, no text label | PASS |
| Carousel dots | 5 dots, 16px active center / 12px inactive | 5 Dot GOs present; report cites sd=(16,16) active / (12,12) inactive | PASS |
| Top-bar ticket counter | Pill `#122C47`, "999" 30pt SemiBold, ticket icon | `RP Amount Container` sprite tinted, TicketCountText 30pt fontStyle=0 (fix in ShellScene this iter); canonical shows "999" and a small gold coin next to it | PASS |
| Shop+ button | 54×54 gold gradient, `ButtonPlus` GUID `ce078d73` | Report cites correct sprite/size; visible in canonical to right of "999" | PASS |
| **fontStyle everywhere = Normal (weight from font asset — never Bold)** | SemiBold via Rubik-SemiBold SDF; Medium via Rubik-VariableFont | Every TMP in GachaBannerCard has fontStyle=0 (verified) | PASS |
| Canonical resolution ≥ 900px | Long edge ≥ 900 | 2070×1912; long edge 2070 | PASS on the ≥900 gate; **captured game view embeds Unity editor chrome — a clean chrome-free 1170×2532 capture is still owed for final sign-off** |

---

## Clone provenance verification (Rule 19 read-back)

| Element | Cited source | Verified? |
|---|---|---|
| CountdownPill sprite | `RP Amount Container` GUID `25ffeb0c` | Cited in report + linter path; real reused sprite. PASS |
| PityCounter pills ×2 | Same `RP Amount Container` | Same source; PASS |
| WrapPanel bg | `BackgroundCardsContainer` GUID `99e72b8115aea1e45b65cd1a24f2784e` | Cited real sprite; sprite present. PASS (but is not 9-sliced — see wrap-panel border FAIL row) |
| RulesButton chip | `Background#2` GUID `3dea690e` | Linter path confirms sprite in use. PASS |
| HistoryChip sprite | `Background#2` GUID `3dea690e` | Linter path confirms sprite in use. PASS |
| PULL ×1 / ×10 | `Play Button` GUID `cff37a7f` | Linter path confirms sprite in use. PASS |
| TicketCountBG | `RP Amount Container` GUID `25ffeb0c` | Heartbeat + report cite. PASS |
| ShopPlusButton | `ButtonPlus` GUID `ce078d73` | Heartbeat + report cite. PASS |
| Dots ×5 | `Dot Active` GUID `de2e147a` | Heartbeat + report cite. PASS |
| **PitySection background** | **N/A — no sprite (flat `#050D1FFF` fill)** | **This element should not exist as an opaque fill (per Cesar #2 / MEASUREMENTS.md L69/L76). Rule 19 clone check: N/A because the design does not call for a fill. FAIL by content, not by provenance.** |

No fabricated clone provenance. No critical Rule 6 fabrication finding.

---

## UI fidelity lint re-check (Rule 21)

Reviewer did not re-execute the linter (no direct Unity write access from this seat), but re-read the JSON:
- `GachaBannerCard_lint.json` fail=0 warn=5 (RulesButton nonuniform-stretch acceptable — square silver chip is design intent; PitySection flat-fill flagged; BG 9-slice cap-kink cosmetic; PULL 9-slice cap-kink cosmetic).
- `GeneralShopScreen_lint.json` fail=0 warn=14 (all 3 banner copies show `PitySection flat #050D1FFF fill with sharp corners` — the linter is corroborating the visible defect; other WARNs are pre-existing store elements or cosmetic).

Both prefabs pass the linter's fail-count gate, but linter WARNs on `PitySection flat-fill` explicitly triangulate the primary FAIL below.

---

## Rejection follow-up cross-check

Reading `CESAR_REJECTION.md` items 1–11 against iter-7:

| # | Cesar demand | Verdict against canonical |
|---|---|---|
| 1 | ENDS IN pill = clone Tournament time pill | Different source used (`RP Amount Container`); same stadium shape — acceptable variance, PASS |
| **2** | **Guaranteed text directly over banner — NO blue background** | **FAIL — opaque `#050D1FFF` strip painted behind pity rows; implementer rebuts as "not blue in RGB sense," but Cesar rejected the pattern (colored fill behind pity text) categorically, and MEASUREMENTS.md L69/L76 pin the pity content OVER the ART bounds. This is the exact recurrence Cesar rejected on sight after iter-3.** |
| 3 | 99-pulls pills = Rankings RP-pill | PASS |
| 4 | Whole banner + buttons inside a blue panel | PASS |
| 5 | Rules & rates asset from Figma | Superseded by MEASUREMENTS.md silver "!" chip + label pattern; PASS |
| 6 | RULES & RATES text OUTSIDE the button | PASS (sibling GO), visibility fainter than reference but position correct |
| 7 | STANDARD CLUB 1 no left spill | PASS (offsetMin.x=24) |
| 8 | History chip top-left silver + clock only | PASS |
| 9 | Each COST over its button | PASS |
| 10 | Dots (2026-07-08 resolution: KEEP 5) | PASS |
| 11 | No scrollbar | PASS |

10 of 11 resolved. Item **#2 is a hard recurrence** and self-graded RESOLVED with a rebuttal that overrides Cesar's explicit direction on a color-technicality — that is precisely the class of self-serving self-grade this reviewer exists to catch (§ Hard rules, "Be willing to OVERRIDE PASS to FAIL").

---

## Bbox verification (Step 6 — containment claim under review)

Claim under review: "PityText/PityCounter/Disclaimer sit OVER the banner art bounds" (MEASUREMENTS.md L69/L76).

Read from `GachaBannerCard.prefab` YAML:
- `ArtImage`: anchorMin=(0, 0.137), anchorMax=(1, 1) — occupies top ~86% of banner card; the bottom ~14% strip is OUTSIDE ArtImage bounds.
- `PitySection`: anchorMin=(0,1), anchorMax=(1,1), anchoredPosition=(0, -1135), sizeDelta=(0, 130) — a 130px tall bar anchored to the top of the banner card, pushed 1135px down. Given typical banner card heights (~1265–1300px), PitySection lands in the bottom 130px strip — the same region that is BELOW `ArtImage.anchorMin.y=0.137`.

Conclusion: `inside(PitySection ⊂ ArtImage bounds)` = **false**. Pity content is in the below-ART strip, painted opaque `#050D1FFF`. Direct spec violation (MEASUREMENTS.md L69/L76).

Reviewer did not run a live `script-execute` bbox log this pass because the numerical evaluation from the prefab YAML is deterministic and matches the visible canonical. A live bbox log (if requested) would confirm the same.

---

## Capture-helper compliance (Step 5)

- Method: `CaptureCore.SnapPlayModeSafe("gacha_iter7_final")` in play mode with ShellScene loaded and GeneralShop navigated via NavGachaButton — sanctioned per `CaptureHelper` rules. PASS on capture method.
- **Chrome present**: canonical is 2070×1912 with Scene/Game tabs + toolbar band. Per CLAUDE.md screenshot rules (long-edge ≥ 900) the file clears the resolution gate; per the current review instructions the chrome is a known SnapPlayModeSafe window-grab artifact, tolerated for this Stage-0 iteration but a chrome-free 1170×2532 recapture is still owed before final architect/red-team sign-off. Note only — not a sole-fail basis.
- New context under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` = none introduced this task. Rule N/A.

---

## Scene-mutation audit (Step 7)

Report declares `Assets/Scenes/ShellScene.unity` modified for TicketCountText direct-scene fontSize=30 fontStyle=Normal fix (documented, in-scope). Report also lists new untracked assets under `Assets/Art/Gacha/` and `Assets/Resources/Prefabs/Gacha/`, all named and in scope. No hidden mutations flagged.

(A live `git diff -- Assets/Scenes/ShellScene.unity` for `m_IsActive: 0` / sizeDelta drift was not run this pass because the current visible defect is scoped to the GachaBannerCard prefab, not ShellScene. If the redo touches ShellScene layout, the next self-review should run it.)

---

## Production-flow capture (Step 8)

Canonical was captured from the real ShellScene navigated via NavGachaButton — production-flow representative. Sufficient for Stage-0 layout review.

---

## Summary of Overrides

| Item | Implementer verdict | Reviewer verdict | Reason |
|---|---|---|---|
| Cesar #2 (guaranteed over banner, no blue bg) | RESOLVED | **OVERRIDE-FAIL** | PitySection has opaque `#050D1FFF` fill and lives BELOW ArtImage bounds. This is the pattern Cesar rejected verbatim; MEASUREMENTS.md L69/L76 explicitly requires OVER-the-art positioning. Implementer's "not blue in RGB sense" rebuttal is a color-technicality that overrides an explicit direction. |
| Wrap panel border (3px white-90%, radius 20) | PASS* (deviation) | **OVERRIDE-FAIL** | MEASUREMENTS.md L56 is an explicit spec token. `BackgroundCardsContainer` (non-9-sliced) does not render a discrete 3px stroke; linter WARN nonuniform-stretch 39%. Rule 18 pattern — an explicit spec token rendered absent = FAIL, same failure class as `1v1_ingame_ui`. |

All other items CONFIRM-PASS or CONFIRM-observation (non-fail).

---

## Verdict

**BACK_TO_IMPLEMENTER** (`SELF_REVIEW_FAIL`)

### Fix list (concrete, one instruction per defect)

1. **Remove the PitySection opaque background fill on all 3 banner copies AND reposition pity content OVER the banner art.**
   - On `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab` and every embedded copy in `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` (`BannerCard_Main`, `BannerCard_LeftPeek`, `BannerCard_RightPeek`), set the `PitySection` Image's `color.a = 0` (or remove the Image component entirely) — no visible background.
   - Reposition `PitySection` so its RectTransform is contained within `ArtImage` bounds (ArtImage anchorMin.y = 0.137). Concretely: change PitySection anchoring so its rect sits inside the ART's lower green-field region (e.g. anchoredPos.y in the range that overlaps the bottom of ArtImage; sd.y sized so pity + pills + disclaimer all fit over the art). Verify via a live `script-execute` bbox check that every PityText / PityCounter / Disclaimer child's `GetWorldCorners` are strictly inside `ArtImage.GetWorldCorners`.
   - This is Cesar #2 verbatim + MEASUREMENTS.md L69/L76. It has been rejected once already; do not re-litigate the color — remove the fill.

2. **Render the wrap panel's explicit 3px white-90% border (MEASUREMENTS.md L56).**
   - Either (a) replace `BackgroundCardsContainer` with a 9-sliced navy-gradient sprite whose native border is 3px `#FFFFFF` @ 90% and radius=20, tuned via `pixelsPerUnitMultiplier` so the border stays crisp at w=882 (Rule 21 render-health), OR (b) composite an `Outline`-style child stroke sprite that renders a discrete 3px white-90% border on top of the current fill. Option (a) preferred to match the Figma token exactly.
   - Confirm with linter re-run: no nonuniform-stretch WARN on WrapPanel, and the built canonical shows a crisp discrete white stroke matching the Figma reference (visible as a bright thin border on the reference render).

3. **Capture a clean chrome-free 1170×2532 canonical for the next review.**
   - The iter-7 canonical carries Unity editor Scene/Game tab band + toolbar. Use `CaptureHelper.SnapGameView()` (or the fake-state preset flow) so the output is the pure 1170×2532 game view without editor UI. Same production-flow launch path is fine; the picker just needs the clean render target.

4. **(Optional but recommended) Re-verify RULES & RATES label visibility.**
   - The RatesLabel is numerically correct (15.4pt SemiBold Normal) but reads visibly fainter/smaller than the reference render in the current canonical. After the clean recapture (fix #3), do a side-by-side crop A/B against `reference/gacha_screen_FULL_reference.png` — if it still reads faint, check TMP color/alpha, extra tracking, or unintended parent CanvasGroup dimming.

No other items open. Cesar's dots override remains KEEP-5 (confirmed).

---

## Files touched by this review

| Path | Status | Change |
|---|---|---|
| `Docs/Specs/Active/gacha_screen/SELF_REVIEW.md` | Overwritten | iter-7 self-review verdict = BACK_TO_IMPLEMENTER (fix list of 3 hard items + 1 recommended) |
| `Docs/Specs/Active/gacha_screen/STATUS.md` | Updated | READY_FOR_SELF_REVIEW → SELF_REVIEW_FAIL |
