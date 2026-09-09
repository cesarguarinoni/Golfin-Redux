# ARCHITECT_REVIEW — `asset_loans_polish` (RED-TEAM)

**Iteration:** 2
**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Date:** 2026-09-09 18:05 JST
**Verdict:** PASS → `ARCHITECT_REVIEW_PASS`

I tried to break this three ways and could not. Both prior defects are gone by my own
measurement, the invariant gate reproduces on a fourth independent play session, and the
between-caption video frames (where the first defect hid) are clean. Details below.

---

## Evidence I generated myself (not carried from the reviewer)

- **4th probe run**, armed via `SessionState.SetBool("LoanPolishProbe.Armed", true)` +
  `EnterPlaymode()`. Wrote `asset_loans_polish_invariants.json` at `08:55:18Z`:
  **`"fail": 0` over 24 assertions.** Editor exited play mode on its own; ShellScene
  `IsDirty=false` afterward (probe leaked no scene state).
- **7 between-window video frames** extracted at t=5/10/19/27/40/48/54 s (the gaps the
  four captions do NOT cover).
- **EditMode suite re-run by me:** 2975 total, **2972 passed, 0 failed, 3 skipped**
  (2:30). See report-integrity note on the 2972-vs-2971 delta.
- **Scene diff / Rule 13** re-derived from `git` directly.
- Re-read `LoanRibbonView.cs`, `LoanReturnModalController.cs`, `LoanPolishProbe.cs`,
  `UiMotion.cs` (Run/Stop/Register/Then) end-to-end.

## Harshest angle I captured

The reviewer leaned on the club still + A12. My independent angles were (a) the
**between-caption video frames**, because the first defect on this task was invisible in
every still and only showed in a video frame, and (b) a **direct A/B of my club frame vs
the shipped `asset_loans` defect capture**. Both surfaced no new defect.

## Metrics I re-ran (my run vs shipped iter-2)

| id | my run | shipped/report | verdict |
|---|---|---|---|
| PRE_clock | unlocked 0.017s, locked 0.017s → does NOT pin | same (3 prior hosts) | **did NOT flip — iter-1 defect stays dead** |
| A1_ribbon_rises_from_above | firstSample=721.401 > rest 710.400, distinctYs=14 | 721.477 | CONFIRM |
| A5_selected_row_bumps | firstSample=1.042 peak=1.060 (=BumpPeak) settled=1.000 | 1.043 / 1.059 | CONFIRM |
| A6_rows_stagger_rise | profile [0.226 0 0] f1; firstVisible [0:1 1:3 2:5] | [0.218 0 0]; [0:1 1:3 2:5] | CONFIRM |
| A6b_every_row_visible_at_rest | row0 y=0.000 inVp=True; rows 1/2 −112/−224 | same | **defect-1 GONE** |
| A8_pending_midflight | label='…' both dead | same | CONFIRM |
| A11_empty_state_fades_in | first=0.303 → 1.000, distinct=9 | 0.302 | CONFIRM |
| A12_club_ribbon_on_artwork | ribbon/dim leftD=rightD=0.000; w=537.000 | all four 0.000 | **defect-2 GONE** |

Every trace differs in value (fresh per-frame reads) and every predicate holds — a fourth
data point confirming the value-based rewrite is host-independent.

## Prior-rejection replay

- **Cesar: "On loan overlay… spills to the left and does not reach the right border" (clubs).**
  **GONE.** A/B: the shipped `Docs/Specs/Completed/asset_loans/screenshots/loan_clubs_B_on_loan_2026-09-09_15-06-19.png`
  shows the exact defect — ribbon+dim shifted left of the artwork panel with a visible
  bright undimmed strip on the right (short of the divider). My club frame
  (`screenshots/polish_club_lent_ribbon_2026-09-09_17-55-17.png`) and A12 (all four D=0.000,
  ClubImage/LoanRibbon/LentDim all L=−537.000 R=0.000) are flush both edges.
- **Defect-1: first recipient row invisible (found in a video frame, JSON was 21/21).**
  **GONE.** A6b on my run: row 0 at `y=0.000 inVp=True a=1.000`. Video t=15.7 s and the
  modal-rows still both show MARTA/LUCAS/AIKO in order, none clipped.
- **Iter-1: frame-rate-dependent gate (`fail=3` on one host, 0 on another).** **GONE.**
  PRE_clock on my host is the 4th "does NOT pin" reading; every motion assertion is
  same-frame-anchored and passed.

## Three break-attempts (all failed)

1. **Visual.** My 5 re-shot states + 7 between-window frames: lender ribbon+dim, the
   BORROWED ribbon (correctly NO dim, RETURN button — lender-only-dim design), the modal
   stagger/selection, empty state ("Follow players in PLAYLIFE…"), the post-Clear rest
   state, and the club transition. Every frame upright, full 1170×2532, HUD/carousel/nav
   intact, no torn text, no missing element, ribbon flush on club AND roster. No defect.
2. **Geometric.** 24/24 fail=0 on my run, no metric near a fragile threshold; the
   value-based predicates read the synchronous first segment `UiMotion.Run` writes, so no
   host can miss and nothing but a real tween can produce them. No fragile metric.
3. **Spec-intent.** All four mandated atom swaps are genuinely present: `PendingSpend`
   (LEND + RETURN, verified code parity), `UiSelection.Bump` (A5), `UiMotion.Rise`/`Fade`
   (A1/A2), `UiSelection.Indicator` (A9), `GpsPaintMotion.StaggerRise`/`FadeInPanel`
   (A6/A11). No new strings/sprites/geometry except the club fix Cesar asked for. No miss.

## Ruling on the three forwarded notes

1. **`MatchArtworkX` couples to `VerticalLayoutGroup.childAlignment = UpperLeft` — NOT a
   blocker.** The task is correct today; the coupling is to a stable, low-churn surface and
   is documented in both the code comment and the report. The offered guard (throw at build
   if alignment ≠ UpperLeft) gives only PARTIAL protection — `LoanUiBuilder` is editor-only,
   so it fires only if a future task changes the alignment AND re-runs the builder; a change
   without a rebuild still drifts because the anchoring is baked into the scene. Forcing a
   full pipeline iteration for a 4-line guard that doesn't fully close the hole is
   disproportionate to a speculative future risk. **Recommendation (not a gate):** add the
   guard opportunistically when the loan prefabs are next rebuilt, or accept the documented
   coupling. I am not blocking on it.
2. **Absent `_17-37-*.png` (self-review set) — benign.** Verified: each surviving run holds
   a COMPLETE set of all five states (implementer 17-31/32, reviewer 17-46/47, mine
   17-54/55); the pruned 17-37 set was the self-review's complete five. A complete
   superseded set was removed, not a cherry-picked frame. Nothing selective.
3. **`LoanRibbonView` lives on the GameObject it deactivates — genuinely safe.** Confirmed
   from source: `UiMotion.Stop` → `UiMotionRunner.Settle` runs the routine's registered
   finalizer, and `Then(Fade, deactivate)`'s finalizer is
   `() => { innerFinal(); after(); }` (UiMotion.cs:647) — so even a hard scene-teardown
   mid-Clear settles alpha to 0 AND deactivates both objects coherently. The two real paths
   are directly tested (A4 clean fade→inactive; A10 mid-entrance→rest). Safe, not merely
   untested.

## Gaps in the gate I found but judged non-blocking

- **No numeric assertion for the ROSTER ribbon's horizontal flush** — A12 covers only the
  club. The roster ribbon reads flush both edges in every frame I captured, the roster
  LeftPanel has no VLG (so no left-flush overflow bug), and Cesar's rejection was
  club-specific. Low risk; noting for the record.
- **A8 exercises only the LEND pending state, not RETURN.** `LoanReturnModalController`
  uses the identical `PendingSpend.Begin(returnButton, returnButtonText, cancelButton!)`
  scope-before-latch with `returnButtonText` serialized+wired and the spinner inert. A
  measurement gap, not a defect.
- **Badge 0-px rest parity is an edit-mode reasoned claim, not in the JSON.** The badge is
  absolutely-anchored (8,−8) with its own 44×44 rect and no card has a root LayoutGroup, so
  an always-active-at-alpha-0 badge never enters layout flow; the carousel icon layout is
  identical across every capture. Sound.

## Report integrity

Every quoted number I could check reproduces: scene diff 62/6 with zero `m_IsActive`
(re-derived); A12 all-D=0.000; A1/A5/A6/A11 within per-run noise of four runs; video
61.6 s / 30 fps / 1170×2532. **Test-count delta explained:** report says
2975/2971 passed/4 skipped, my run 2975/**2972**/**3**. The one-test difference is exactly
`UiMotionAllocationTests.CountUp_AllocatesOnlyWhenTheDrawnNumberChanges`, which the report
itself documents as self-skipping when the editor clock swallows its tween — on my run the
clock didn't, so it passed. Total 2975 and **0 failures** match; no new failures. Not a
fabrication.

## Editor left clean

`editor-application-get-state`: IsPlaying=false, IsPaused=false, IsCompiling=false.
`scene-list-opened`: one scene, `ShellScene.unity`, `IsDirty=false`. I ran no edit-mode
`GetWorldCorners`/`rect` dump and did not save the scene. Scene diff still 62/6, zero
`m_IsActive`.

## Files touched by this review

| File | Change |
|---|---|
| `Docs/Specs/Active/asset_loans_polish/ARCHITECT_REVIEW.md` | OVERWRITTEN — this red-team verdict |
| `Docs/Specs/Active/asset_loans_polish/STATUS.md` | `READY_FOR_REDTEAM` → `ARCHITECT_REVIEW_PASS` |
| `Docs/Specs/Active/asset_loans_polish/asset_loans_polish_invariants.json` | REWRITTEN by my probe re-run (24 assertions, `"fail": 0`) |
| `Docs/Specs/Active/asset_loans_polish/screenshots/polish_*_17-54-56/17-55-*.png` (5) | NEW — my re-run captures |
