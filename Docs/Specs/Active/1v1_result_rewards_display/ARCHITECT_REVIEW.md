# ARCHITECT REVIEW — 1v1_result_rewards_display (Stage 3, iter-2)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-02 10:14 CEST
**Iteration:** Stage 3 iter-2 (`polish:tie-label-and-reward-centering`) — CESAR_REJECTED fix pass
**Verdict:** **READY_FOR_REDTEAM** (I do NOT write `ARCHITECT_REVIEW_PASS`; red-team is the sole PASS-gate.)

## Independent visual scan (Step 0, before reading verdicts)

**TIE frame:** both label columns above the two portraits read "TIE" in a neutral light grey (matches #CCCCCC), not green/orange. RANK: #116 / #86 rows are neutral (no green/orange tint). A single reward row shows a golden coin icon and "x200" horizontally centered under the "HOLE / Lomond Country Club - Hole 1" band. The reward icon looks visibly dimmer/darker gold than the WIN capture (consistent with a dimming pass). NEW MATCH gold button below.

**WIN frame:** labels read "WINNER" (green) and "LOSER" (orange). RANK #116 tinted green, RANK #86 tinted orange. The coin+"x200" reward row is centered horizontally in the same slot the TIE frame uses; coin looks brighter/more saturated gold than the TIE frame. NEW MATCH gold button unchanged.

## Governing ruling

`CESAR_RULING.md` (2026-07-02) + `CESAR_REJECTION.md` are binding:
- Delta captures only (real-flow already proven Stage 1 iter-3; ModeSelection background waived).
- Banner fix (#3 in rejection) was committed **separately by orchestrator** in `5b72d37fc` (`VersusMatchController.cs`) under a Cesar-authorized Rule-7 exception — **NOT in this iter's diff, do NOT flag its absence/presence.**
- Packages/ MCP env dirt waived.

Scope of this pass: fixes #1 (TIE label) + #2 (reward centering), + no regressions on WIN/LOSE/pop-in/reward-greying/Rule-7.

## Figma fidelity

Figma nodes `13274:877` (WIN) / `13275:2628` (LOSE); reference renders present in `reference/`. TIE state is a CESAR-defined addition with no Figma node (SPEC §5 D2 resolved 2026-07-02 → neutral #CCCCCC).

| Element | Node | Figma value | Built (measured) | Result |
|---|---|---|---|---|
| RESULTS header | 13274:877 | White SemiBold centered | White SemiBold centered | PASS |
| WINNER label | 13274:877 | Green #50C878 | `WinnerColor = 0x50/0xC8/0x78` verified in code + visible bright green in WIN cap | PASS |
| LOSER label | 13275:2628 | Orange-red #C04000 | `LoserColor = 0xC0/0x40/0x00` verified in code + visible orange in WIN cap | PASS |
| TIE label (draw state) | CESAR §5 D2 | "TIE" #CCCCCC both cols | `DrawLabel = "TIE"` + `DrawColor = 0xCC/0xCC/0xCC`; both cols show TIE in neutral grey in TIE cap | PASS |
| Rank line — TIE state neutral | CESAR §5 D2 | Both ranks neutral grey | `DrawColorHex = "#CCCCCC"` applied to both localNumColor and opponentNumColor when `isDraw==true` (`BindRankText`); TIE cap RANK #116/#86 visibly neutral (no green/orange) | PASS |
| Rank line — WIN state green/orange | 13274:877 | Green winner / orange loser | WIN cap #116 green, #86 orange; code `localWon?WinnerColorHex:LoserColorHex` unchanged in non-draw branch | PASS |
| Vs. separator | 13274:877 | White centered | Present centered between portraits | PASS |
| Portrait cards | 13274:877 | `CharacterThumbnailCard` reused | Reused; rarity letter + Lv badge visible on both frames | PASS |
| HOLE label + course line | 13274:877 | Gold "HOLE" + course-hole line | "HOLE" gold, "Lomond Country Club - Hole 1" below | PASS |
| Reward row — WIN bright + centered | 13274:877 | Bright coin+amount centered | Measured: cluster span x=[373,796], midpoint=584.5px, panel center~585px, **offset = -0.5px**. Peak gold pixels present (RGB up to (188,176,73)) | PASS |
| Reward row — TIE dimmed + centered | CESAR §5 D2 | Greyed but visible; centered | Measured: same span x=[373,796], midpoint=584.5px, **offset = -0.5px**. Independently pixel-sampled: ZERO warm gold pixels (r>150, r>b+30) in coin band vs 42 warm pixels in WIN — decisively dimmed. Code: `rewardsBright = localWon` → TIE (isDraw=true, localWon=false) → α=0.5 + `RewardChildDim` (unchanged from Stage 2) | PASS |
| NEW MATCH button | 13274:877 | Gold CTA | Bright gold in both frames (peak lum 240, unchanged) | PASS |

**Text weight / rendered-size gate (standing rule):** no text elements introduced or resized this iter. The delta is a color/label constant change and 4 pivot values — no font weight or size claim to reverify. Stage 2's approved text render is preserved.

## Bbox / centering verification

Implementer cited live `GetWorldCorners`: Row1 midX = 585.0, Rewards centerX = 585.0, offset = 0.0px.

**Independent pixel re-derivation** (Python/PIL over the two 1170×2532 PNGs):

```
WIN:  reward-row bright span x=[373,796], midpoint=584.5, panel_center~585, offset=-0.5px
TIE:  reward-row bright span x=[373,796], midpoint=584.5, panel_center~585, offset=-0.5px
```

Both frames land ≤1px from panel center. Matches the implementer's 0.0px live measurement within pixel-quantization tolerance.

**Multi-slot forward-safety:** HorizontalLayoutGroup on `Rewards` has `childAlignment=MiddleCenter` + `childForceExpandWidth=false` (unchanged per Rule-12 C4 self-cert, verified in prefab diff scope: no HLG mutation this iter). With `pivot=(0.5,0.5)` on Rewards + Row1/2/3, 2- or 3-slot future cases still distribute symmetrically around center. No 1-slot hardcoded position.

## Diff scope audit (Rule 5, Rule 7)

`git diff HEAD -- Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab`:
- 4 `m_Pivot` changes exactly: `{x:1,y:1}` → `{x:0.5,y:0.5}` on `Rewards` (sizeDelta 100×470), `Reward Row1` (978×60), `Reward Row2` (100×470), `Reward Row3` (100×470).
- **Zero** anchor / sizeDelta / anchoredPosition / rotation / hierarchy drift. Mechanically clean.

`git diff HEAD -- Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs`:
- `DrawLabel = "DRAW"` → `"TIE"` (const string change; comment updated).
- New `DrawColor = 0xCC/0xCC/0xCC`, `DrawColorHex = "#CCCCCC"`.
- `ShowResult` derives `isDraw = outcome == MatchOutcome.Draw` and passes to `SetOutcomeLabelsLive(isDraw, leftWon)` + `BindRankText(isDraw, localWon, opp)`.
- `rewardsBright = localWon` — draw is NOT bright (spec-correct; §5 D2 says draw = greyed).
- `MatchOutcome.Draw` enum name **unchanged** (only display string flipped) — verified.

`git diff HEAD -- Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs`:
- Iter-1 pop-in coroutine (header comment update + `_popInCoroutine` + `PopInScaleRoutine`). **Unchanged this iter** — no regression risk introduced.

`git diff HEAD -- Assets/Scripts/Physics/` → **empty**. Rule 7 clean.
`Scenarios.cs` → **empty**. `M_Splash*.mat` → no matches (untouched).
No new UnityEngine.UI.Button → ButtonPressFeedback rule not triggered.

`git status --porcelain` outside task folder: only `Packages/manifest.json` + `Packages/packages-lock.json` (MCP env dirt, per HEARTBEAT iter-1 baseline block, waived per Cesar) + the 3 in-scope files listed above.

**Banner fix** (`VersusMatchController.cs` at commit `5b72d37fc`) is on HEAD as a separate Cesar-authorized commit — not in this iter's uncommitted diff, correctly excluded per this pass's brief. Not flagged.

## Full acceptance re-walk (Rule 5)

| # | §4c item | Verdict | Independent basis |
|---|---|---|---|
| 1 | 3-way outcome switch (win/lose/draw) | PASS | Grep confirmed `isDraw = outcome == MatchOutcome.Draw` in `ShowResult`; propagates to labels + ranks + brightness |
| 2 | TIE state: TIE/TIE grey labels, ranks neutral, reward greyed | PASS | TIE cap visual + `DrawColor`/`DrawColorHex` in code + zero warm-gold pixels in coin band (dimmed) |
| 3 | WIN/LOSE unchanged (regression) | PASS | WIN cap: WINNER green + LOSER orange + green/orange ranks + bright centered coin+x200 (42 warm pixels detected) |
| 4 | Pop-in transition (iter-1) | PASS | `VersusResultModalController.PopInScaleRoutine()` intact; StopCoroutine + `Vector3.one` interrupt guard present; unchanged in iter-2 |
| 5 | Sanctioned CaptureHelper, 1170×2532 | PASS | Both PNGs verified 1170×2532; report cites `CaptureHelper.SnapGameViewWithLabel` |
| 6 | Compile clean | PASS | Report claim; no console errors reported; no schema-breaking edits in diff |
| 7 | Scoped diff, no banned paths | PASS | 4 pivots + 2 script deltas + iter-1 pop-in only; Physics/ empty; Scenarios.cs empty; no M_Splash |

## Regression audit — TIE reward greying

Implementer pixel-sampled WIN(173,152,68) vs TIE(95,88,61) at same 20×20 coord — ~55% brightness ratio. Independent re-check: my scan of the reward-row coin band with a stricter `r>150 && r>b+30` gold threshold finds **42 gold pixels in WIN** and **ZERO in TIE**. Dimming is confirmed via TWO independent methods (implementer's patch avg + my thresholded gold-pixel count). Code path `rewardsBright = localWon` preserved.

## Report integrity (Rule 6)

Every PASS in `IMPLEMENTER_REPORT.md` is backed by tool output or my independent re-derivation:
- Fix 1 (TIE label): git diff excerpt in report + code re-read + TIE cap visual — all consistent.
- Fix 2 (centering): live `GetWorldCorners` in report + my independent pixel measurement (offset -0.5px) — consistent.
- TIE greying: pixel-sampled WIN vs TIE patch in report + my independent warm-pixel count — consistent.
- Physics diff empty: independently verified.
- Prefab diff scope: independently verified (4 pivots, no drift).

No fabrications. No unbacked claims.

## Verdict

**READY_FOR_REDTEAM.**

Both CESAR_REJECTED fixes land clean and are objectively verified from three angles (code diff, pixel measurement of captures, independent gold-pixel count on the reward band). WIN/LOSE regression, TIE greying, pop-in transition, and Rule-7/Rule-19 bans all intact. Diff scope is minimal and free of drift.

Handing off to `golfin-redteam-reviewer`, which is the only agent authorized to advance to `ARCHITECT_REVIEW_PASS`.

## Files touched this review

| Path | Change |
|---|---|
| `Docs/Specs/Active/1v1_result_rewards_display/ARCHITECT_REVIEW.md` | Rewritten for iter-2 |
| `Docs/Specs/Active/1v1_result_rewards_display/STATUS.md` | → `READY_FOR_REDTEAM` |
