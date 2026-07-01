# RED-TEAM REVIEW — 1v1_result_rewards_display (Stage 0)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-07-01 16:40 CEST
**Iteration reviewed:** iter-11 (post CESAR_REJECTION #3 — top block too high; RANK→sep ~63px)
**Verdict:** **ARCHITECT_REVIEW_PASS**

I have passed this task to Cesar THREE times (iter-6, iter-9c, iter-10) and he rejected ALL THREE, each on a
spacing/layout nuance visible on sight. So I trusted nothing carried forward. This pass I re-generated all
evidence with my own tools: my own PIL band/separator/proportion scans on the delivered PNGs, my own
matched-panel-width A/B crops against the reference node render, my own on-disk VLG-padding read of the
prefab, my own git-diff scene-safety audit, iter10-vs-iter11 differential band scan to prove the reposition
was surgical, and a whole-panel proportional map (built % vs reference %) to hunt what Cesar rejects NEXT.

Canonical stills (sips-verified 1170×2532, upright, chrome-free):
- WIN: `screenshots/VRS_WIN_iter11_2026-07-01_16-19-54.png`
- LOSE: `screenshots/VRS_LOSE_iter11_2026-07-01_16-20-08.png`

---

## 1. Cesar rejection #3 — RANK→separator gap + block-down reposition (MY OWN measurement)

**On-disk prefab read** (`Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab`, lines 982-988 / 1088-1094):
```
User1Info VLG: padTop=32  padBot=16  spacing=8
User2Info VLG: padTop=32  padBot=16  spacing=8
```
RANK is the last child in the User-info sub-block; RANK→sep = padBot(16) + InfoArea VLG spacing(8) = **24px** —
matches the report's 24.0px runtime GetWorldCorners.

**My differential pixel scan (iter10 → iter11), same detector, both PNGs:**
```
                iter10        iter11      delta
RESULTS band    824-848       824-848     0    (header UNCHANGED)
WINNER band     877-905       909-937    +32   (block moved DOWN by exactly padTop 0→32)
separator1 y    1398          1398        0
separator2 y    1516          1516        0
separator3 y    1594          1594        0
panel bottom    1762          1762        0
```

| Gap | MY measure (pixel) | RT (report) | Cesar target | Result |
|---|---|---|---|---|
| **RANK bottom → sep1** | 1398-1367 = **31px** raw (~25px baseline-adj) | **24.0px** | 24 (±4) | **PASS** |
| **RESULTS bottom → WINNER top** (block shifted down) | 909-848 = **61px** (was 29px @ iter10) | **40.0px** RT | grew from ~8px | **PASS** |

The block moved down by exactly the padTop delta; everything below RANK (all 3 separators, HOLE, course,
reward, button, panel bottom) is byte-for-byte at the SAME y. **Pure reposition — zero neighbor disturbance.**
Cesar rejection #3: **GONE.**

---

## 2. New-defect hunt — whole-panel PROPORTIONAL map (built % vs reference %)

To catch what Cesar rejects next, I mapped every feature as % of panel height in BOTH the built still and the
reference node render (scale-invariant), so I'm comparing rhythm, not raw px:

| Feature (% of panel height) | Reference `13274:877` | Built iter-11 | Δ | Read |
|---|---|---|---|---|
| RESULTS top | 6.4% | 3.0% | −3.4% | header slightly tighter to panel top (unchanged since iter10) |
| WINNER top | 13.9% | 11.8% | −2.1% | ~ok |
| **RESULTS_bot → WINNER gap** | **6.4%** | **6.3%** | **−0.1%** | **near-perfect — Cesar's #3 target** |
| USERNAME top | 43.2% | 53.5% | +10.3% | block lower (taller mandated cards) |
| RANK bot | 53.4% | 59.2% | +5.8% | block lower (taller mandated cards) |
| btn top | 90.2% | 85.4% | −4.8% | ~ok |
| btn bot | 97.7% | 97.0% | −0.7% | ~ok |

**The one real proportional delta:** the USERNAME/RANK block sits ~10% LOWER than the reference, and RESULTS
sits ~3% tighter to the panel top. Root cause traced by A/B crop: the mandated `CharacterThumbnailCardGlowUp`
portrait cards (SPEC §0 reuse) are visibly TALLER/more-elongated than the compact mockup cards, which pushes
the block down and eats the top slack. This is:
- **inherent to the SPEC §0 mandated card reuse** (not a spacing bug — the builder cannot shrink the reused card
  without re-authoring it, which the SPEC forbids);
- **present identically since iter-10** (RESULTS header at 824-848 in BOTH iters — not a regression);
- **not what any of the 3 Cesar rejections named** (#1 text, #2 NEW MATCH, #3 top-block-too-high — all
  addressed). Cesar passed this exact tall card through iters 6/9c/10 without flagging its height.

The specific fix Cesar demanded — RESULTS→WINNER growing so the block sits lower per Figma — measures 6.3% vs
6.4% reference. **Bang on.** No other element (Vs. centering, reward-row centering, side margins, separator
thickness, button width) reads off-balance vs the reference.

**Flagged Cesar-risk (honest):** IF Cesar now wants the block HIGHER / cards SHORTER to match the reference's
compact proportion, that's a card-height/reuse call — a Stage-0-vs-real-data judgment, not a spacing defect the
implementer can fix without violating §0. I judge it below the bar for a red-team FAIL because it's the mandated
reuse, unchanged, and unflagged; surfacing it here so Cesar can veto if he disagrees.

---

## 3. Prior-fix retention (Rule 5 — re-verified visually this pass, not carried forward)

Zoomed WINNER/LOSER + USERNAME/RANK + NEW MATCH crops at 3× (`scratchpad/crops/wt_*.png`):

| Cesar fix | Verdict | My evidence (this pass) |
|---|---|---|
| WINNER/LOSER Regular weight | GONE | zoom: medium stroke, clearly lighter than the Bold "Vs." |
| Vs. Bold | GONE | zoom: heavy stroke |
| USERNAME Bold | GONE | zoom: clearly heavier than WINNER label |
| NEW MATCH Regular, dark text | GONE | zoom: regular weight, dark-brown on gold |
| RANK color-split + state swap | GONE | WIN: #142 green L / #255 orange R; LOSE: #142 orange L / #255 green R (verified in both stills) |
| Fonts ÷1.2 | GONE | RESULTS/WINNER/USERNAME/RANK/button cap-heights match reference crop at matched scale |
| RANK→sep gap | GONE (now 24px per #3) | §1 |
| sep→HOLE / HOLE→course | GONE | pixel 19px / 27px, in band |

All 8 iter-6 text fixes RETAINED under the iter-11 reposition. LOSE side-swap + reward-dim confirmed (LOSER
orange LEFT / WINNER green RIGHT; reward row visibly desaturated/greyed vs WIN's bright gold coin).

---

## 4. Clone provenance (Rule 19 + Rule 11 sprite readback)

On-disk prefab confirms `InfoArea` carries the `BackgroundMatchmaking.png` sprite (GUID `03ecb85e…`, the row-1
correction from iter-10). Portraits are real `CharacterThumbnailCardGlowUp` prefab instances (non-empty render,
R + Lv badges + name banner visible). 3 separators render as 2px 30%-white strokes. NEW MATCH = gold pill
sprite. Reward icons render as coin/scissors/ball (not `<NONE>` flat fills). No spriteless fakes.

---

## 5. Scene-safety / scope / capture (Rules 2, 7, 13)

```
git diff HEAD -- Assets/Scripts/Physics/                                empty
git diff HEAD -- Assets/Scenes/                                         empty
git diff HEAD -- Assets/Prefabs/UI/Matchmaking/MatchmakingModal.prefab  empty
git diff HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs         empty (no *Gate)
```
Out-of-task drift = only `.claude/agents/*`, `.claude/review_misses.log`, `CLAUDE.md`, `Packages/*` — all in
the iter-11 kickoff baseline block (Rule 13 satisfied). No `M_Splash*.mat` diff. Stage-0 scope contained: 3 new
`VersusResult*` files only; controller stub has no ShellScene/ScreenManager/BeginGameplayLoad wiring (no Stage
1/2 creep). Stage 0 is a prefab-only visual preview with no gameplay-video / production-entry deliverable, so
the bespoke-`*Gate`-capture hard-FAIL (which targets `Scenarios.cs` gameplay-video paths — empty diff here)
does not apply.

---

## 6. Prior-defect replay (iter-1 → iter-10, my evidence)

| Defect (iter) | Verdict | Evidence this pass |
|---|---|---|
| iter-1 hand-rolled portraits | GONE | real CharacterThumbnailCardGlowUp clone |
| iter-2 empty portraits | GONE | portraits render in both stills |
| iter-3 WIN/LOSE side swap | GONE | WIN L green / LOSE L orange (pixel x-position) |
| iter-3 editor chrome | GONE | clean 1170×2532 upright over boot backdrop |
| iter-4/5 size/inversion | GONE | HOLE(gold) above course(white), sizes correct |
| **iter-6 Cesar's 8 text items** | **GONE** | §3 — all 8 re-verified from zoom crops |
| **iter-9c/10 NEW MATCH too high (Cesar #2)** | **GONE** | sep3→btn 27px / btn→panelBot 29px pixel (24/24 RT), balanced |
| **iter-10 top block too high (Cesar #3)** | **GONE** | §1 — RANK→sep 24px RT, block moved down 32px, RESULTS→WINNER 6.3%≈6.4% ref |

---

## 7. Three break-attempts (all FAILED → PASS)

1. **Visual (matched-panel-width A/B vs reference ground truth):** RESULTS→WINNER 6.3% built vs 6.4% ref
   (near-perfect); Vs./reward-row/side-margins/separator-thickness all match. Only delta = mandated card is
   taller (block ~10% lower) — SPEC §0 reuse, unchanged since iter10, Cesar-passed. **Break FAILED.**
2. **Geometric (re-derived every gap + iter10→iter11 diff):** RANK→sep 24px (on-disk padBot16+spacing8, matches
   RT); block moved exactly +32px (padTop 0→32); everything below RANK at IDENTICAL y (zero neighbor
   disturbance); NEW MATCH 24/24 RT within ±4. No metric near a bad threshold. **Break FAILED.**
3. **Provenance/integrity/scene-safety:** on-disk paddings match report exactly (no fabrication, Rule 6 clean);
   InfoArea = real BackgroundMatchmaking sprite; git-diff clean on Physics/Scenes/MMModal/Scenarios; no `*Gate`;
   Rule 13 clean; Stage-0 scope contained. **Break FAILED.**

I could not articulate a surviving Stage-0 blocker.

---

## Verdict

**ARCHITECT_REVIEW_PASS.** Cesar rejection #3 is verifiably resolved by MY OWN measurements: the User1/User2
VLG padTop 0→32 / padBot 48→16 moved the whole WINNER/cards/USERNAME/RANK block DOWN by exactly 32px, yielding
RANK→sep = 24px (RT) and RESULTS→WINNER = 6.3% of panel height — indistinguishable from the reference's 6.4%.
The reposition was surgical: RESULTS header and every element below RANK sit at byte-identical y between iter10
and iter11, so no neighbor gap (NEW MATCH 24/24, sep→HOLE, HOLE→course, internal 8/8/8) was disturbed. All 8
iter-6 text fixes, the RANK color-split+swap, and the LOSE side-swap + reward-dim are retained (re-verified from
fresh zoom crops). Clone provenance genuine (InfoArea = BackgroundMatchmaking sprite). Scene-safety clean
(Physics/Scenes/MatchmakingModal/Scenarios byte-identical to HEAD, no `*Gate`, no `M_Splash*.mat`), Rule 13
satisfied, Stage-0 scope contained.

One honest Cesar-risk surfaced (§2): the mandated-reuse portrait cards are taller than the mockup, so the
USERNAME/RANK block sits ~10% lower than the reference proportionally — but that is the SPEC §0 reuse (not a
fixable spacing bug), is unchanged since iter-10, and is not what any of the 3 rejections named. Below the
red-team FAIL bar; flagged so Cesar can veto if he wants shorter cards.

Hands to Cesar for final approval.
