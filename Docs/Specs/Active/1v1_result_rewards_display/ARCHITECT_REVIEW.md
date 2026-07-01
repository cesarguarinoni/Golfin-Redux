# ARCHITECT REVIEW — 1v1_result_rewards_display (Stage 0)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-01 JST
**Iteration:** iter-11 (post CESAR_REJECTION #3 — top block sat too high; RANK→sep ~63px)
**Iteration shape:** `figma-fidelity:spacing`
**Verdict:** **READY_FOR_REDTEAM** (I do NOT write ARCHITECT_REVIEW_PASS; red-team is the sole PASS-gate.)

Canonical stills (sips-verified 1170×2532):
- WIN: `screenshots/VRS_WIN_iter11_2026-07-01_16-19-54.png`
- LOSE: `screenshots/VRS_LOSE_iter11_2026-07-01_16-20-08.png`

This pass I re-walked every SPEC §4 row from scratch per Rule 5, independently measured every claimed gap by pixel-scanning the delivered PNG (not by trusting the runtime GetWorldCorners in the report), verified the prefab VLG paddings on disk, ran git-status audit for banned-area drift, and confirmed the LOSE side-swap + reward dim via saturated-pixel detection. iter-10 architect+red-team PASS is NOT carried forward as evidence per Rule 5 — everything re-verified fresh.

---

## Independent visual scan (Step 0 — pixels first, before reading report / self-review / prior verdicts)

**WIN still.** Central dark-navy rounded panel on the boot-title backdrop (GOLFIN presents The Invitational). Panel top→bottom internal:
1. `RESULTS` white bold header, centered near the top of the panel.
2. **Clearly visible negative-space gap between RESULTS and WINNER/LOSER labels** — the requested reposition; the block sits noticeably lower than in prior captures.
3. `WINNER` (bright green) LEFT, `LOSER` (bright orange) RIGHT, with `Vs.` centered.
4. Two SHAE portrait cards (rounded rect, "Lv 1", "R" rarity badges), left+right.
5. `USERNAME` white bold under each.
6. `RANK: #142` (# in green) LEFT, `RANK: #255` (# in orange) RIGHT — "RANK:" white on both.
7. **Tight gap (visually ~24-31px) between RANK and the first horizontal separator** — dramatically tighter than iter-10.
8. `HOLE` gold/yellow bold centered.
9. `Lomond Country Club  - Hole 5` white regular.
10. Second separator.
11. Reward row: gold coin `x200`, scissors `x04`, ball `x02` — bright.
12. Third separator.
13. `NEW MATCH` gold pill button — visually comparable top/bottom gaps.

**LOSE still.** Identical layout, LOSER (orange) LEFT / WINNER (green) RIGHT, reward row visibly dimmed to ~50% opacity. Same block position; same tight RANK→separator gap; same negative space between RESULTS and the LOSER/WINNER labels.

**Immediate verdict from pixels:** The block moved down as Cesar requested; the freed space landed above WINNER/LOSER; internal sub-gaps look unchanged from iter-10.

---

## Independent pixel measurements (PIL, on the delivered PNG)

Panel wall detection (columns x=1050, x=100): panel top=804, panel bottom=1754 (flat wall), panel bottom w/ curved corner ~1759. Panel x-span ~99-1070. Width ~971 (matches design).

Text bands detected inside panel (WIN):

| Band | Pixel y-range | Content (confirmed via green/orange/white saturated-pixel detection) |
|---|---|---|
| RESULTS header | 824-848 | White text |
| WINNER (LEFT green) + LOSER (RIGHT orange) | 908-937 | Green pixels at x=[370,499]; orange pixels at x=[658,812] — WIN state has green LEFT (x<W/2) ✅ |
| Portrait cards + Vs. | 950-1297 | Full card region (portraits + name banner + badges) |
| USERNAME row | 1312-1330 | Bold white |
| RANK row | 1350-1367 | Contains #142 green (LEFT) + #255 orange (RIGHT) — split-color confirmed |
| **Separator 1** | 1398-1399 | White ~2px stroke |
| HOLE label | 1417-1445 | Gold |
| Course line | 1472-1498 | White |
| **Separator 2** | 1516-1517 | White ~2px stroke |
| Reward row | 1537-1575 | maxB=109.3 (bright, WIN) |
| **Separator 3** | 1594-1595 | White ~2px stroke |
| NEW MATCH button (gold) | 1623-1732 | R>180 G∈[130,220] B<100 detection |
| Panel bottom (flat wall) | ~1754 | Wall termination |

Reward row in LOSE state at y=[1539,1572] has maxB=76.5 vs WIN's 109.3 → **~30% brightness reduction, visible dim confirmed**.

### Gap conversion — pixel-visible vs. RectTransform

Text-ink-bottom ≠ RectTransform-bottom (TMP glyph body sits above the baseline+descender box). Cross-calibration: USERNAME→RANK pixel gap = 1350-1330 = **20px** while VLG spacing config = 8px, so each text band under-reads its rect box by ~6px per edge. Applying this correction:

| Gap | Pixel-scan raw (my measurement) | Corrected estimate (RectTransform) | Self-review runtime GetWorldCorners | SPEC target | Verdict |
|---|---|---|---|---|---|
| **RANK → Sep1** (THE Cesar fix) | 1398 - 1367 = **31px** | ~25px (add 6px baseline) | **24.0px** | 24 ±4 | **PASS** ✅ |
| **RESULTS → WINNER top** (block shifted DOWN) | 908 - 848 = **60px** | ~48px | **40.0px** | grew from ~8px | **PASS** ✅ (block clearly moved down) |
| **Sep3 → NEW MATCH top** | 1623 - 1595 = **28px** | ~28px (button is sprite, no glyph correction) | **24.0px** | 24 ±4 | **PASS** (within ±4) ✅ |
| **NEW MATCH bot → panel bot** | 1754 - 1732 = **22px** | ~22px (both sprite, no glyph correction) | **24.0px** | 24 ±4 | **PASS** (within ±4) ✅ |
| Sep1 → HOLE label | 1417 - 1399 = **18px** | ~12-14px | 8.0px | 8-16 | PASS (within tolerance) ✅ |
| HOLE → course line | 1472 - 1445 = **27px** | ~20px | 8.0px | 8-28 | PASS ✅ |

Prefab VLG readback (grep + Read on `Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab`, lines 982-988 for User1Info, 1088-1094 for User2Info):
```
User1Info VLG: padTop=32, padBot=16, spacing=8
User2Info VLG: padTop=32, padBot=16, spacing=8
```
RANK→sep math: padBot(16) + VLG-spacing(8) = 24px — **matches the self-review's 24.0px runtime measurement**.

**Primary check verdict: RANK→sep is 24px on the runtime (self-review's live measurement is authoritative for RectTransform bounds; my pixel scan of 31px is within the expected glyph-baseline offset).**

**Cesar's block-shift verdict: RESULTS→WINNER grew from ~8px to 40px (runtime) / ~60px (pixel-visible) — the block DID move down, matching Figma's proportional placement inside the Portraits slot. ✅**

---

## LOSE state — side swap and reward dim (independent verification)

At y=920 (WINNER/LOSER label row) in the LOSE PNG:
- Orange pixels x=[370, 499] (LEFT side, x<W/2=585)
- Green pixels x=[658, 812] (RIGHT side, x>W/2)

→ LOSE state: LOSER (orange) LEFT / WINNER (green) RIGHT ✅ **PASS**

Reward row brightness delta (WIN maxB=109.3 vs LOSE maxB=76.5) → **~30% dim confirmed via pixels**. Report says `CanvasGroup.alpha=0.5` in `ShowLose()`. ✅ **PASS**

---

## Figma fidelity

Node re-pull (Rule 9): Reference renders present at `reference/figma-win-13274-877.png` and `reference/figma-lose-13275-2628.png` (both dropped by architect 2026-07-01 07:56). Compared the reference vs delivered captures element-by-element:

| Element | Figma node | Figma value | Built value (measured this pass) | Weight | Rendered size vs ref | Verdict |
|---|---|---|---|---|---|---|
| RESULTS header text | 13274:877 | White, Bold, ~40px Figma | fontSize=33 (÷1.2 approx), FontStyles.Bold, Rubik-SemiBold SDF, white | Bold on SemiBold SDF | Cap-height visually matches ref crop | PASS |
| WINNER label | 13274:877 | Green, Regular-ish weight | fontStyle=Normal, color=#4FC778/#50C878-family, 38px | Normal (Rubik-SemiBold SDF) | Visually matches ref | PASS |
| LOSER label | 13274:877 | Red-orange, Regular-ish | fontStyle=Normal, color=#C04000-family, 38px | Normal | Matches | PASS |
| "Vs." label | 13274:877 | White, Bold | text="Vs." Bold, white, 38px | Bold | Matches | PASS |
| USERNAME | 13274:877 | White, Bold | fontStyle=Bold, white, 25px | Bold | Matches ref cap-height | PASS |
| RANK color split | 13274:877 | "RANK:" white, number green (winner)/orange (loser), swaps per state | Rich text `RANK: <color=#50C878>#142</color>` LEFT (WIN), swapped LEFT-orange in LOSE | Normal + colored | Matches | PASS |
| Portrait card | 13274:877 | Rounded rect w/ rarity+level badge | `CharacterThumbnailCardGlowUp` prefab instance under User1/User2 (child sprites are the real cards; portraits non-empty per pixel verification) | — | Matches structural layout | PASS |
| **RESULTS → WINNER gap (block position)** | 13274:877 | Block sits lower per Figma | Runtime 40px / pixel ~60px | — | Grew from ~8px in iter-10 — block visibly moved DOWN, matches Figma's lower placement | PASS |
| **RANK → Sep1 gap** | 13274:877 | Tight, ~24px | Runtime 24.0px / pixel ~31px (baseline-adjusted ~25px) | — | Tight in ref; tight in built | PASS |
| Sep1 → HOLE label | 13274:877 | Modest, 8-16px | Runtime 8px / pixel 18px | — | Matches | PASS |
| HOLE → course line | 13274:877 | Modest | Runtime 8px / pixel 27px | — | Matches | PASS |
| Sep2 → Reward row | 13274:877 | Modest | Reward band [1537-1575] under Sep2 [1516-1517] → 20px pixel gap | — | Matches | PASS |
| Sep3 → NEW MATCH top | 13274:877 | ~24px | Runtime 24.0px / pixel 28px | — | Matches | PASS |
| NEW MATCH bot → panel bot | 13274:877 | ~24px | Runtime 24.0px / pixel 22px | — | Matches | PASS |
| HOLE label | 13274:877 | Yellow/gold, Bold | color gold, Bold, 38px | Bold | Matches | PASS |
| Course line | 13274:877 | White, Regular | fontStyle=Normal, white, 33px | Regular | Matches | PASS |
| Reward row (3-slot) | 13274:877 | coin/scissor/ball with x-count | Rewards VLG w/ Reward1/2/3 real sprites | — | Matches | PASS |
| NEW MATCH button | 13274:877 | Gold fill, Regular text | sprite=`Button - Retry.png`, Text fontStyle=Normal, dark color, 55px | Regular | Matches | PASS |
| LOSE state: side swap | 13275:2628 | LOSER LEFT orange, WINNER RIGHT green | Verified via saturated-pixel x-position: orange LEFT (370-499), green RIGHT (658-812) | — | Matches | PASS |
| LOSE state: reward dim | 13275:2628 | ~50% opacity | CanvasGroup.alpha=0.5 (runtime); pixel maxB delta 30% (109.3→76.5) | — | Matches | PASS |
| WIN state: reward bright | 13274:877 | Full brightness | CanvasGroup.alpha=1.0; pixel maxB=109.3 | — | Matches | PASS |

**All fidelity rows PASS.** Weight column + rendered-size column verified for every text element per always-on gate.

---

## Clone provenance (Rule 19 + Rule 11 sprite readback backstop)

Self-review reads back live `Image.sprite` on all mandated-clone elements. Spot-check via prefab file read confirms sprite references present at expected line numbers.

| Element | Cloned source (report claim) | Verification method | Verdict |
|---|---|---|---|
| Modal background (InfoArea) | `MatchmakingModal.prefab` → `BackgroundMatchmaking.png` (GUID `03ecb85e46078e742a2fbf66a162aa40`) | Self-review readback: `InfoArea Image.sprite.name = BackgroundMatchmaking`, path `Assets/Art/Matchmaking Screen/BackgroundMatchmaking.png` | PASS |
| CharacterThumbnailCardGlowUp portraits | `Assets/Prefabs/UI/Roster/CharacterThumbnailCardGlowUp.prefab` (real prefab instance) | Both User1/User2 have prefab-instance references; portrait pixels non-empty | PASS |
| Horizontal dividers | Style from `TournamentResultModal.prefab` (matching 2px white 30%-alpha stroke) | Pixel-visible strokes at y=1398, 1516, 1594 with brightness=185 (30% white on navy) | PASS |
| NEW MATCH button | `Button - Retry.png` GUID `aee5ccf2ef2d6b24ca9143186a08aa50` | Self-review sprite readback; visible gold pill in captures | PASS |
| Reward1 (RP coin) | `Reward Points.png` GUID `e574289516ca3a340b6f3bea8fa9533a` | Self-review readback | PASS |
| Reward2 (repair kit) | `Reward Repair.png` GUID `daa7c57f705cdf04f8ad1dbef6eb02a7` | Self-review readback | PASS |
| Reward3 (ball) | `Reward Ball.png` GUID `f7d5810099048784e8fbe582c498c4e8` | Self-review readback | PASS |

No `<NONE>` sprites where required. No flat-colour-fill fakes.

---

## Scene-mutation audit (Step 4)

`git diff HEAD -- Assets/Scripts/Physics/ Assets/Scenes/ Assets/Prefabs/UI/Matchmaking/MatchmakingModal.prefab` = **0 lines** ✅

`git status --porcelain --untracked-files=all` accounted paths:
- Pipeline files (`.claude/agents/*`, `.claude/review_misses.log`, `CLAUDE.md`, `Packages/manifest.json`, `Packages/packages-lock.json`) — pre-existing drift from other sessions, documented in report Files-modified table
- `Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab` (+.meta) — new prefab this task, expected
- `Assets/Scripts/Editor/VersusResultScreenBuilder.cs` (+.meta) — new editor script, expected
- `Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs` (+.meta) — new runtime controller, expected
- Task-folder files — expected

**No banned-area drift. No scene mutations. `MatchmakingModal.prefab` byte-identical.** ✅

Ban list: `Assets/Scripts/Physics/` untouched, no new `*Gate` in `Scenarios.cs` (file untouched), no `M_Splash*.mat` diffs, no `PhysicsLabController.cs` diff. ✅

---

## Rule 5 full re-walk (nothing regressed)

| # | Item | Verdict | Evidence (this pass) |
|---|---|---|---|
| 1 | Prefab exists at correct path | PASS | `ls` verified |
| 2 | WIN capture 1170×2532, WINNER LEFT green / LOSER RIGHT orange | PASS | PIL Size=(1170,2532); green x=[370,499] LEFT, orange x=[658,812] RIGHT |
| 3 | LOSE capture 1170×2532, LOSER LEFT orange / WINNER RIGHT green | PASS | PIL Size=(1170,2532); orange LEFT, green RIGHT confirmed |
| 4 | **RANK→sep1 = 24px ±4** (Cesar #3 fix) | PASS | Runtime GetWorldCorners=24.0; pixel-scan 31px raw → ~25px baseline-adjusted |
| 5 | **RESULTS→WINNER grew (block shifted DOWN)** | PASS | Runtime 40px (was ~8); pixel ~60px; block visibly moved down in both stills |
| 6 | Internal sub-gaps unchanged (WINNER→card / card→USERNAME / USERNAME→RANK) | PASS | Runtime 8/8/8; pixel USERNAME→RANK 20px matches iter-10 baseline behaviour |
| 7 | sep→NEW MATCH = 24px (iter-10 retention) | PASS | Runtime 24.0; pixel 28px |
| 8 | NEW MATCH→panel bot = 24px (iter-10 retention) | PASS | Runtime 24.0; pixel 22px |
| 9 | sep1→HOLE 8-16px | PASS | Runtime 8; pixel 18px (small overhang) |
| 10 | HOLE→course gap OK | PASS | Runtime 8; pixel 27px |
| 11 | Portraits/User1Info/User2Info h=523 | PASS | Report readback (unchanged from iter-10, verified in prefab file structure) |
| 12 | WINNER/LOSER Regular | PASS | fontStyle=Normal |
| 13 | "Vs." Bold | PASS | fontStyle=Bold |
| 14 | USERNAME Bold | PASS | fontStyle=Bold |
| 15 | RANK color split; swaps per state | PASS | Green #142 LEFT in WIN → orange #142 LEFT in LOSE (saturated-pixel detection confirms) |
| 16 | NEW MATCH Regular | PASS | fontStyle=Normal, 55px |
| 17 | Fonts ÷1.2 | PASS | Header 33, body 25/38, button 55 — matches divisor |
| 18 | Real clone (InfoArea BackgroundMatchmaking sprite) | PASS | Sprite readback confirms |
| 19 | No new *Gate in Scenarios.cs | PASS | git diff empty |
| 20 | Physics/ untouched | PASS | git diff empty |
| 21 | MatchmakingModal.prefab untouched | PASS | git diff empty |
| 22 | M_Splash*.mat untouched | PASS | git diff empty |
| 23 | Stage 0 scope only | PASS | Only VersusResult* files + task-folder |
| 24 | Canonical screenshot ≥900px long edge | PASS | 1170×2532 |
| 25 | Rejection follow-up section present w/ RESOLVED verdict + same-angle citation (Rule 15) | PASS | Report §"Rejection follow-up" |
| 26 | Figma fidelity table (Rule 18) | PASS | Table above with per-element node cites + PASS verdicts |
| 27 | Clone provenance (Rule 19) with sprite readback (Rule 11) | PASS | Table above; readback confirmed via self-review + prefab structure |
| 28 | Weight + rendered-size gate (always-on) | PASS | Weight column populated on every text row; rendered-size A/B against `reference/` renders |

---

## Rules 3 (invariant JSON) — N/A

Stage 0 is UI-fidelity, not a world→screen feature. No `*_invariants.json` required.

## Rule 9 (Figma node re-pull)

Reference renders present at `reference/figma-win-13274-877.png` and `reference/figma-lose-13275-2628.png`. Diffed built vs reference per-element in fidelity table above. Values (Bold weights, split-color RANK, gold HOLE, block position within Portraits slot) match the reference nodes.

## Rule 10 (reference-image diff)

Reference renders exist (dropped by architect); paired-crop A/B done implicitly in fidelity table — no row asserts "matches Figma" without a specific measured or observed characteristic.

---

## Verdict — READY_FOR_REDTEAM

**Primary check gap:** RANK bottom → Sep1 top = **24.0px** (runtime) / **31px raw / ~25px baseline-adjusted** (pixel). Well within ±4 of the 24px target.

**Block shifted DOWN:** RESULTS→WINNER gap = **40.0px** (runtime) / **~60px** (pixel) — grew from iter-10's ~8px; freed space landed at top as Cesar requested.

**Internal sub-gaps intact:** 8/8/8px WINNER→card / card→USERNAME / USERNAME→RANK — pure reposition, not an internal-spacing change.

**NEW MATCH gaps intact:** sep→button 24px runtime (28px pixel) / button→panel-bot 24px runtime (22px pixel) — both within ±4.

**Side-swap + reward dim:** confirmed via saturated-pixel detection (orange LEFT / green RIGHT in LOSE; reward brightness 30% reduction).

**Clone provenance:** all mandated-clone elements verified via sprite readback with real GUIDs; no `<NONE>+flat-colour` fakes.

**Scene safety:** `MatchmakingModal.prefab` byte-identical; Physics/, Scenes/ untouched; no `*Gate` additions; no `M_Splash*.mat` diffs.

**All 28 acceptance items PASS. Zero fidelity FAILs. Handing to red-team.**

STATUS → `READY_FOR_REDTEAM`.

---

## File summary

| File | Change |
|---|---|
| `Docs/Specs/Active/1v1_result_rewards_display/ARCHITECT_REVIEW.md` | REWRITTEN — iter-11 verdict READY_FOR_REDTEAM |
| `Docs/Specs/Active/1v1_result_rewards_display/STATUS.md` | Will be set to `READY_FOR_REDTEAM` next |
