# RED-TEAM REVIEW — 1v1_result_rewards_display (Stage 3, iter-2) — FINAL STAGE

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-07-02 10:16 CEST
**Iteration:** Stage 3 iter-2 (`polish:tie-label-and-reward-centering`) — CESAR_REJECTED fix pass
**Verdict:** **ARCHITECT_REVIEW_FAIL** — CESAR_REJECTED defect #2 (reward icon+amount not centered) is STILL PRESENT.

I re-derived every claim from code + my own pixel measurement, not from the reviewer's PASS.
The reward-centering fix does NOT center the visible content; it only re-centers the container
RectTransform. Both the implementer's live `GetWorldCorners` (0.0px) and the reviewer's pixel
span (-0.5px) measured the wrong thing.

---

## BLOCKER — Fix #2 (reward icon + amount not centered) is STILL PRESENT

Cesar's rejection #2: *"The prize icon + amount is not centered in the reward row … The single
active slot sits off-center instead of centered under the HOLE line."* This is NOT fixed.

**My independent pixel measurement** (Python/PIL, both full-res 1170×2532 captures). I first
located the modal panel edges (navy fill scan, rows y=1000/1150): panel x=[99,1070], **panel
center = 584.5px**. I then measured the horizontal midpoint of every centered element in the
lower modal by scanning tight Y bands for non-navy content:

| Element | Y band | pixel span x | midpoint | offset vs panel center 584.5 |
|---|---|---|---|---|
| "HOLE" gold label | 1425–1470 | 527–643 | **585.0** | +0.5 ✓ centered |
| "Lomond Country Club - Hole 1" | 1480–1525 | ~220–951 | **584.5** | 0.0 ✓ centered |
| **coin + "x200" reward** | 1540–1580 | **535–712** | **623.5** | **+39.0 ✗ OFF-CENTER RIGHT** |
| "NEW MATCH" button | 1650–1720 | 350–819 | **584.5** | 0.0 ✓ centered |

Identical numbers in BOTH `stage3_iter2_win_2026-07-02.png` and `stage3_iter2_tie_2026-07-02.png`
(coin block x=[535,712], mid=623.5 in both).

**Visual proof** (center-line overlay, `scratchpad/win_centerline.png`, `tie_centerline.png`):
a RED line at panel center 584.5 bisects HOLE, Lomond, and NEW MATCH cleanly, but the coin+"x200"
cluster sits visibly to its RIGHT — the coin straddles the red line and the "x200" text runs ~39px
past it. A CYAN line at 623.5 (the coin-block true midpoint) is clearly right of every other element.

**Why the pipeline missed it.** The pivot change `(1,1)→(0.5,0.5)` on `Reward Row1` (a 978px-wide
RectTransform) re-centered that CONTAINER — so `Row1 world midX = 585` (implementer's live
`GetWorldCorners`) and the reviewer's "row RectTransform" reads 585. But the visible coin+x200
CONTENT is laid out off-center INSIDE that 978px row (nested inner layout / content alignment), so
the RectTransform center ≠ the visible-content center. The reviewer's cited pixel span x=[373,796]
(→584.5) was contaminated by the much wider "Lomond Country Club - Hole 1" text line and the two
separator rules that sit in the same broad Y window; a tight Y band isolating only the coin+x200
row gives mid=623.5, +39px right. Cesar explicitly warned to measure "the active slot's icon+amount
vs the reward-row container center," not the container's own RectTransform — that step was not done
correctly by either gate.

**Fix instruction (implementer):** center the VISIBLE coin+amount content, not the container. Use
the golfin-ui-fidelity measure→root-cause→validate loop:
1. `GetWorldCorners` on the actual `Icon`+`Amount` child GameObjects (the coin Image and the "x200"
   TMP), NOT on `Reward Row1`. Confirm their combined bounding-box midX ≠ Rewards container center
   (it is currently ~623.5 vs ~584.5, i.e. ~+39px in screen px at Match-0 canvas scale).
2. Root-cause the inner offset: check Row1's own child layout — a nested HorizontalLayoutGroup with a
   non-center `childAlignment`, a left/right padding asymmetry, the coin/amount slot's own
   pivot/anchoredPosition, or a `childForceExpandWidth` cell that left-aligns the pair. Fix the real
   cause so the coin+amount pair centers inside Row1.
3. Re-verify by the SAME pixel method used here: coin+x200 tight-Y-band midpoint must equal the HOLE /
   Lomond / NEW MATCH centerline (584.5 ± a few px), in BOTH win and tie captures.
4. Must still lay out symmetrically for 2–3 active slots (Cesar's forward-safety clause) — do not
   hardcode a 1-slot x.

---

## Everything else I attacked (and why it held) — recorded but MOOT given the blocker

### Fix #1 — "DRAW" → "TIE" label: GONE (verified)
`VersusResultScreenController.cs` diff: `private const string DrawLabel = "TIE";` (was `"DRAW"`);
`DrawColor = #CCCCCC`, `DrawColorHex = "#CCCCCC"`. `grep '"DRAW"'` → only two COMMENTS (lines 90, 373),
zero DISPLAYED string literals. `GameSession.MatchOutcome.Draw` enum name unchanged (display-only).
TIE capture: both columns read "TIE" neutral grey, both RANK numbers neutral (no green/orange). PASS.
- *Cosmetic nit (not fail-worthy):* line 373 comment still says "both columns show 'DRAW'" — stale
  wording; the rendered text is "TIE". Worth cleaning in the same pass.

### Fix #3 — banner: correctly EXCLUDED
`git diff HEAD -- Assets/Scripts/Physics/` → empty. Banner lives in commit `5b72d37fc` on HEAD
(a separate Cesar-authorized Rule-7 exception), so it is not in the uncommitted diff. Correctly
absent; not flagged.

### TIE reward greying (regression): GONE (verified independently)
Tie coin+"x200" is visibly muted grey vs WIN's saturated gold (`scratchpad/tie_reward_band.png`
vs `win_reward_band.png`). Tight-band non-navy pixel COUNT: WIN=3869 bright px vs TIE=2674 in the
same coin row — dimmer. Code `rewardsBright = localWon` → TIE (isDraw, !localWon) → α=0.5 + RewardChildDim.
PASS. (Note: the reward is dimmed correctly but STILL off-center — greying ≠ centering.)

### WIN/LOSE regression: labels/ranks OK, coin OFF-CENTER (same defect)
WIN capture: WINNER green (#50C878), LOSER orange (#C04000), RANK #116 green / #86 orange, bright
coin. Labels/ranks unchanged — PASS. BUT the WIN coin is ALSO at mid=623.5 (+39px) — the centering
defect affects WIN too, not just TIE. Covered by the blocker.

### Pop-in (item 5): interrupt-safe, untouched this iter
`VersusResultModalController.PopInScaleRoutine()` ease-out cubic 0.9→1.0 over 0.2s; both `Hide()`
and `ShowResult()` re-entry `StopCoroutine` + force `localScale = Vector3.one`; routine ends at
`Vector3.one`. Diff is the iter-1 pop-in verbatim (Stage 3 never landed — iter-1 was CESAR_REJECTED,
so this is legitimately uncommitted). Not re-broken. PASS.

### Diff scope + bans (item 6): clean
`git diff HEAD --stat`: 3 files only — `VersusResultScreen.prefab` (EXACTLY 4 `m_Pivot`
`(1,1)→(0.5,0.5)` on Rewards + Row1/2/3, zero anchor/size/pos/rotation drift), plus
`VersusResultScreenController.cs` (TIE label + 3-way switch) and `VersusResultModalController.cs`
(iter-1 pop-in). `Assets/Scripts/Physics/` empty; `Scenarios.cs` empty; no `M_Splash*.mat`;
no new `UnityEngine.UI.Button` → ButtonPressFeedback rule not triggered. Packages/ MCP env dirt
waived. Scope is clean — the prefab change is just mechanically insufficient to fix the defect.

### Report integrity (item 7): a measurement error, not a fabrication
The reviewer's and implementer's "centered (0.0 / -0.5px)" claims are backed by real tool output
(a live `GetWorldCorners` on Row1; a PIL span over a contaminated Y window) — the numbers exist,
they just measure the wrong region (container RectTransform / text-contaminated span) and therefore
missed a live 39px offset. This is a rubber-stamped review miss, not an invented tool result, so
NOT logged as CRITICAL fabrication under Rule 6. Logged to `.claude/review_misses.log` as a
red-team catch (both prior gates passed a still-present Cesar-rejected defect).

---

## Verdict

**ARCHITECT_REVIEW_FAIL.** The single most important thing this iteration had to fix — Cesar's
rejection #2, "the reward icon+amount is not centered" — is objectively STILL PRESENT: the coin+"x200"
sits +39px right of the panel/HOLE/NEW-MATCH centerline in both the win and tie captures. The pivot
fix re-centered the 978px Row1 container but not the visible content inside it. Fixes #1 (TIE label)
and TIE greying are correct, but they cannot carry a task whose headline rejection is unresolved.

Route back to implementer with the fix instruction above (measure the coin+amount child bbox, not
the row RectTransform).

## Files touched this review
| Path | Change |
|---|---|
| `Docs/Specs/Active/1v1_result_rewards_display/REDTEAM_REVIEW.md` | Rewritten — iter-2 FAIL verdict |
| `Docs/Specs/Active/1v1_result_rewards_display/STATUS.md` | → `ARCHITECT_REVIEW_FAIL` |
| `.claude/review_misses.log` | Appended red-team catch (centering defect passed 2 gates) |
