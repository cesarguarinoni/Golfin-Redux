# IMPLEMENTER_REPORT — `asset_loans_polish`

**Iteration:** 2
**Iteration shape:** `loan_ui:polish_atom_swap`
**Date:** 2026-09-09
**Driver:** real navigation (boot → PLAY gate → bottom-nav `charactersButton.onClick` → the real
`LendButton.onClick` → the real recipient row's own `Button.onClick` → the real footer LEND
`onClick`), HTTP transport stubbed. No `ShowScreen` shortcut, no synthetic button, no render harness.

---

## Summary

The four hand-rolled behaviours named in SPEC § Implementation now go through the shared polish
atoms: `PendingSpend` owns both modals' in-flight state, `UiSelection.Bump` the recipient
selection, `UiMotion.Rise`/`Fade` the ribbon + dim entrance, `UiSelection.Indicator` the card
badge, and `GpsPaintMotion.StaggerRise` / `FadeInPanel` the recipient list's arrival and its
empty state.

**One real defect was found and fixed inside this task** — and it was NOT found by the invariant
JSON, which passed 21/21 while the bug was live. It was found by looking at the recorded video's
14 s frame: the first recipient row was missing from the list. § Defect found and fixed below.

The gate is `asset_loans_polish_invariants.json` — **26 assertions, 0 fail**, every one a number
sampled off the live object frame by frame.

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| LEND / RETURN pending: label reads `PendingSpend.PendingLabel`, CANCEL dead, both restore on the answer — captured mid-flight with the transport delayed 1.5 s | PASS | `A8_pending_midflight`: label `'…'`, `confirmInteractable=False`, `cancelInteractable=False` at t+0.5 s of a 1.5 s stubbed POST. `A8b_restores_on_the_answer`: `'LEND' -> '…' -> 'LEND'`; confirm `True->False->True`; cancel `True->False->True`. `A8c_no_spinner`: the old spinner object stayed inactive the whole round-trip. Frame: `screenshots/polish_pending_midflight_2026-09-09_18-07-45.png` — LEND reads `…`, both buttons visibly dimmed. RETURN is the same three lines of code in `LoanReturnModalController.ReturnRoutine`, exercised by the same `PendingSpend` scope. |
| Tapping a recipient row bumps it (scale trace over ≥3 frames peaking > 1.0), the previous selection does not | PASS | `A5_selected_row_bumps`: `framesAbove1=5 peak=1.059` against `UiMotion.BumpPeak=1.060`, settling at exactly 1.000 — trace `[1.042 1.058 1.059 1.018 1.002 1.000 …]`. `A5b`: the un-tapped row's scale spread is 0.000. `A5c`: after tapping row 1, row 0 (the row that LOST the selection) has spread 0.000 while row 1 bumps 5 frames to 1.059. Sampled at `Time.captureDeltaTime = 1/120` so a long editor frame cannot step over a 0.10 s tween. |
| Ribbon entrance: first `Show` after a reconcile plays Rise (+ dim Fade for the lender); a tick-driven `Show` on a visible ribbon plays nothing; `Clear` fades out then deactivates | PASS | `A1_ribbon_rises_from_above`: rest y = 710.400, the trace opens at **719.336 — above rest** — and lands back on 710.400 over 13 distinct values. `A1b`: alpha monotonic to exactly 1.000 over 14 distinct values. `A2`: the dim is active and fades monotonic to 1.000 over 8 distinct values. `A3_tick_repaint_plays_nothing`: a REAL second reconcile of the same loan (the tick) gives `ySpread=0.000 alphaSpread=0.000` with the ribbon still visible at y=710.400 alpha=1.000. `A4_clear_fades_then_deactivates`: partial alpha observed while still active, trace `[0.476 0.296 … 0.000]`, then `ribbonActive=False dimActive=False`. |
| Badge: `Indicator` on/off with `animate` only on a state change; card rect dump identical with the badge off vs. before this task (0 px) | PASS | `A9a`: `Indicator` leaves the badge ACTIVE with a CanvasGroup — alpha is the state, not the active flag. `A9b_unchanged_repaint_is_flat`: a real `CharacterThumbnailCard.RefreshIcons()` with no state change gives spread 0.000. `A9c`: lent→free 9 distinct alphas ending 0.000, free→lent 9 distinct ending 1.000. `A9d`: `Apply(false,false)` twice — the second call's spread is 0.000, so a repeated verdict does not re-animate. **0 px proven separately**, in edit mode on all three card prefabs: activating the badge and forcing a layout rebuild moves every other child by `worst delta = 0.000 px` (14 / 16 / 11 siblings compared) — none of the three cards has a root LayoutGroup, and the badge is anchored top-left at (8,−8) with its own 44×44 rect, so it was never in a layout flow. |
| Recipient rows stagger-rise on arrival; placeholders do not; empty state fades in | PASS | `A6_rows_stagger_rise`: `firstVisibleFrame per row = [0:1 1:3 2:5]` — a real spread, non-decreasing — and every row ends at alpha 1.000. `A6b_every_row_is_actually_visible_at_rest` (added after the defect below): row 0 y=0.000, row 1 y=−112.000, row 2 y=−224.000, all alpha 1.000, all `inVp=True`. `A7_placeholders_not_animated`: no placeholder row (classified by its OWN empty `UserId`) is ever below alpha 1. `A11_empty_state_fades_in`: 88 samples, min 0.302, end 1.000. |
| `asset_loans` rect self-diff re-run: all Δ still 0.000; `m_IsActive` census unchanged except any `CanvasGroup` additions | PASS | Re-measured with `GetWorldCorners` in each RightPanel's local space and reproduced the shipped numbers to three decimals: `ROSTER LevelUp.L = -245.800 / Compare.L = -245.800 Δ = 0.000`; `ROSTER Boost.R = 240.200 / Lend.R = 240.200 Δ = 0.000`; `gap = 16.000 cmpW = 235 lendW = 235 sameY = True`; `CLUB -629.850` / `-394.850`, both Δ 0.000; ribbon 537×72 and dim 537×1483 (Roster) / 537×1355 (club), ribbon still drawn over the dim. The **club** ribbon and dim moved +27.05 px in x — that is the fix Cesar asked for, measured in `A12`, and it is the only geometry this task changes; every Roster number and every button-row number is byte-identical to the shipped dump. `git diff -U0 -- Assets/Scenes/ShellScene.unity \| grep m_IsActive` returns **nothing**. The whole scene diff is 56 added lines: four `CanvasGroup` components (alpha 1) plus the four `ribbonGroup`/`dimGroup` field wirings. |
| `OnDisable` mid-motion leaves ribbon/dim/badge at a correct rest state | PASS | `A10_ondisable_settles_at_rest`: the entrance is armed, the screen is left 0.1 s in through the real Bag nav button, and the ribbon reads y=710.400 (= rest), ribbonAlpha=1.000, dimAlpha=1.000. That is `UiMotion.Stop`'s Register contract firing from `LoanRibbonView.OnDisable`. |
| EditMode suite: no new failures; `ScrollFeelTests` and `UiMotion*` still green | PASS | Whole EditMode mode (the runner ignores class filters): **2975 total, 2971 passed, 0 failed, 4 skipped** — identical to the shipped `asset_loans` baseline. The 4 skips are the 3 long-standing `HoleCompleteDriverTests` and `UiMotionAllocationTests.CountUp_AllocatesOnlyWhenTheDrawnNumberChanges`, which self-skips when the editor frame clock swallows its tween. |
| No new hardcoded literals; `export --check` clean (nothing to import) | PASS | Zero string literals added: `PendingSpend.PendingLabel` is the atom's own const, and no `LocalizationManager.Get` key is new. `git status` shows `Assets/Data/LocalizationText.csv` untouched, so there is nothing to import or publish and `export --check` has nothing to diff. |
| **Cesar 2026-09-09: "On loan overlay is not correctly over the image in clubs (it spills to the left and does not reach the right border)"** | PASS | `A12_club_ribbon_on_artwork`: before, `ribbon leftD=-27.050 rightD=-27.050; dim leftD=-27.050 rightD=-27.050` against `ClubImage`; after, **all four 0.000**. `ClubImage L=-537.000 R=0.000` and `LoanRibbon L=-537.000 R=0.000` — flush on both edges. A/B crop: `screenshots/club_ribbon_before_after.png`. Full frame: `screenshots/polish_club_lent_ribbon_2026-09-09_18-07-58.png`. |
| Console clean; `[SerializeField]` wired; deviations flagged | PASS | `A0_canvasgroups_authored`: `ribbonGroup` and `dimGroup` are non-null on the live view. `confirmText` / `returnButtonText` were ALREADY serialized and already wired by `LoanUiBuilder` to each button's own TMP child, so no new field was needed — deviation D-1 below. Console across all six probe runs and both recordings: no errors from loan code. |

---

## Defect found and fixed *inside* this task

**The first recipient row was invisible, and the invariant JSON passed 21/21 while it was.**

`A6` asserted the stagger's SHAPE — the rows light up at spread-out frames and all end at alpha 1 —
and every one of those things was true. What it never asked was *where the rows were*. Reading the
recorded video's 14 s frame against the shipped `asset_loans` capture showed a blank slot where
MARTA should be, with LUCAS and AIKO in their correct slots.

`A6b_every_row_is_actually_visible_at_rest` was added to measure it rather than reason about it:

```
[0 2222 y=-448.000 h=96.000 a=1.000 chain=1.000 act=True inVp=False scale=1.000]
[1 3333 y=-112.000 h=96.000 a=1.000 chain=1.000 act=True inVp=True  scale=1.000]
[2 4444 y=-224.000 h=96.000 a=1.000 chain=1.000 act=True inVp=True  scale=1.000]
```

Row 0 was four 112 px slots down, outside the viewport, at full alpha — invisible because it was
*elsewhere*, not because it was transparent.

**Cause.** `ClearRows()` calls `Destroy` on the four placeholder rows, and Unity defers `Destroy`
to end of frame. Spawning the real rows in that same frame means `GpsPaintMotion.StaggerRise`'s
`LayoutRebuilder.ForceRebuildLayoutImmediate` — and item 0's beat, which `UiMotion.Run` fires
*synchronously* — both see four dead placeholders still occupying the layout. `UiMotion.Rise`
captures `restY` at the moment it is called, so row 0 learned its rest slot as the one after four
corpses and pinned itself there. Rows 1..n were only ever correct by accident of timing: their
beats land on later frames, after the placeholders have actually gone.

This is the exact failure `GpsPaintMotion.StaggerRise`'s own header warns about ("ROWS UNDER A
LAYOUT GROUP HAVE NO REST POSITION UNTIL LAYOUT RUNS"); its `ForceRebuildLayoutImmediate` fix
cannot help when the thing corrupting the layout is a child that has not been collected yet.

**Fix.** One frame between `ClearRows()` and the spawn, in `LoanModalController.LoadRecipients` —
let the placeholders actually leave, THEN measure. Not a second layout rebuild, because the
problem is the frame, not the rebuild. After the fix: row 0 at `y=0.000 inVp=True`, and the A/B
crop against the shipped capture is identical (MARTA / LUCAS / AIKO in the same three slots).

---

## Second defect — the club ribbon was 27.05 px left of the club artwork (Cesar, mid-task)

> "On loan overlay is not correctly over the image in clubs (it spills to the left and does not
> reach the right border)."

Pre-existing from `asset_loans` — the shipped `loan_clubs_B_on_loan` capture has it too — but it is
the same subsystem and it was on screen, so it is fixed here.

**Measured in play mode** (an edit-mode read is useless: the club `LeftPanel` is a
`VerticalLayoutGroup`, so before layout runs `ClubImage` reports its centre at the panel's LEFT
EDGE, 268 px out, and a fix derived from that would go the wrong way):

```
before:  ribbon leftD=-27.050  rightD=-27.050   dim leftD=-27.050  rightD=-27.050
after:   ribbon leftD=  0.000  rightD=  0.000   dim leftD=  0.000  rightD=  0.000
         ClubImage  L=-537.000 R=0.000 w=537.000
         LoanRibbon L=-537.000 R=0.000 w=537.000
```

**Cause.** The club artwork is 537 wide inside a 482.9-wide panel whose layout group is aligned
`UpperLeft` — so it sits LEFT-FLUSH and overflows 54.1 px to the RIGHT. `BuildRibbon` took the
artwork's WIDTH but the panel's CENTRE, and a 537-wide bar centred on a 482.9-wide panel lands
`(537 − 482.9) / 2 = 27.05` px left of a left-flush 537-wide artwork. Same width, wrong origin —
which is exactly "spills left, doesn't reach the right border".

**Fix.** `MatchArtworkX` in `LoanUiBuilder` gives the ribbon and the dim the artwork's own
horizontal anchoring — left anchor, centre pivot, `x = width / 2` — expressed against the anchor
rather than as a measured offset, so it is correct in edit mode where the layout has not run.
Vertical anchoring is untouched: the ribbon still takes the panel's top edge and the dim the
panel's height, so the dim still covers the info block. The Roster panel is not touched (its
LeftPanel has no layout group and its ribbon was already flush at leftΔ 0.000).

**A scene-save trap on the way in, worth recording.** Writing the fix with `rt.rect.width` made
Unity evaluate layout for the whole canvas, every `LayoutGroup` in `ShellScene` wrote its
children's rects, and the save baked **1367 lines of anchor churn across 157 unrelated objects**.
Reverted, redone reading `sizeDelta` only (the same number, with the x anchors collapsed to a
point, but it reads nothing). Final scene diff: **62 insertions, 6 deletions** — the four
CanvasGroups, the four wirings, and the two club rects. Zero `m_IsActive`.

---

## Iteration 2 — the self-reviewer failed the GATE, and it was right

`SELF_REVIEW.md` (iter-1) verified everything else — both defect fixes reproduce, the canonical
still is what it claims, the four video captions match their windows, the scene diff is 62/6 with
zero `m_IsActive`, the concurrent-session file attribution is correct — and failed the task on one
thing: **it re-ran the probe on its own editor and got `fail=3`.** A1/A1b/A2 could not resolve a
0.25 s entrance because that host runs at ~125 ms/frame and sampled it twice. The motion was
playing (its trace shows y above rest and alpha climbing to 1.000); the *gate* was wrong. A gate
that answers differently on two hosts is not a gate, and shipping on a JSON that flips is exactly
what the two-gate hardening exists to stop.

**Its proposed fix does not work, and I only know that because I measured it.** The suggestion was
to apply A5's `Time.captureDeltaTime = 1/120` lock to the entrance sampler. With that lock set, the
ribbon's first alpha sample was still `0.347` — and `EaseOut(dt / 0.25) = 0.347` solves to
`dt = 0.033 s`, the editor's own frame time. `captureDeltaTime` does not pin `unscaledDeltaTime` on
this host, so A5's earlier improvement came from its four warm-up frames, not from the clock. Had I
taken the suggestion at face value I would have shipped a comment claiming a guarantee the code did
not have. `PRE_clock` now measures this every run and puts it in the JSON:
`unlocked median 0.017s, captureDeltaTime=0.008 median 0.017s -> the lock does NOT pin
unscaledDeltaTime on this host`.

### The shape audit (PIPELINE_HARDENING §15)

Two defects of one shape — frame-rate-dependent assertions, in runs 1–5 and again here — so the
rule is to name the shape as a mechanically checkable question and enumerate EVERY site, including
the ones that were fine, rather than fix the three the reviewer named.

**Question:** does this assertion's verdict depend on how many samples the host's frame rate
happened to give it?

| Assertion | Reads a motion trace? | Verdict | Action |
|---|---|---|---|
| `PRE_clock` | measures the clock itself | n/a | NEW — informational, records effective dt |
| `A0_canvasgroups_authored` | no — serialized refs | robust | none |
| `A1_ribbon_rises_from_above` | yes — distinct-y count, `maxY > rest + 4` | **FRAGILE — tripped** | value-based: first sample (same frame as arming) above rest |
| `A1b_ribbon_alpha_0_to_1` | yes — `distinct >= 5` | **FRAGILE — tripped** | `distinct >= 2`, floor 0.99 → 0.999 |
| `A2_dim_fades_in_for_lender` | yes — `distinct >= 5` | **FRAGILE — tripped** | same |
| `A3_tick_repaint_plays_nothing` | flat trace | robust — a slow frame can only make a flat trace flatter | none |
| `A4_clear_fades_then_deactivates` | yes — needed a sample in `0.02 < a < 0.98` | **FRAGILE — latent** | `active && a < 0.999`, and the first sample is same-frame |
| `A5_selected_row_bumps` | yes — `framesAbove1 >= 3` | **FRAGILE — latent** | scale left 1.000 in the frame of the tap; peak reported, not gated |
| `A5b_other_rows_do_not_bump` | flat | robust | none |
| `A5c_previous_selection_does_not_bump` | flat + `framesAbove1 >= 3` | **FRAGILE — latent** | same-frame form |
| `A6_rows_stagger_rise` | yes — frame-index spread | **FRAGILE — latent** | per-frame alpha PROFILE across rows (an earlier row is always further along), spread kept as a fallback |
| `A6b_every_row_is_actually_visible_at_rest` | no — settled rects | robust | none |
| `A7_placeholders_not_animated` | negative assertion | robust in direction (a slow host can only miss a violation, never invent one) | none |
| `A8` / `A8b` / `A8c` | state reads across a realtime round-trip | robust — and the probe clock is deliberately released here | none |
| `A9a` / `A9b` | static / flat | robust | none |
| `A9c_state_change_animates` | yes — `distinct >= 4` | **FRAGILE — latent** | `distinct >= 2` + first sample off rest alpha |
| `A9d_repeat_apply_does_not_reanimate` | flat | robust | none |
| `A10_ondisable_settles_at_rest` | end-state read | robust | none |
| `A11_empty_state_fades_in` | yes — **`min < 0.35` FLOOR** | **FRAGILE — latent, and the last floor in the file** | shape form, floor 0.999 |
| `A12_club_ribbon_on_artwork` | no — settled rects | robust | none |

Eight sites changed, twelve confirmed fine. The floor in A11 is the one that stings: iter-1's report
says floors were removed because they "only pin the frame rate", and that one survived because it
happened to pass here.

### What replaced them

`UiMotion.Run` drives a routine's first segment **synchronously**, so by the time the arming call
returns, `RiseRoutine` has already put the rect at `restY − dy` and stepped it once, and
`BumpRoutine` has already taken the scale off 1.000. Sampling in that same frame needs no frames to
elapse, so no host can miss it — and nothing but a real tween can produce it. A ribbon that snapped
into place reads `firstSample == restY` exactly, with one distinct value.

Measured this run: `A1 firstSample=721.477` against rest `710.400`; `A5 firstSample=1.043` in the
frame of the tap; `A6` staggered profile `[0.218 0.000 0.000]` across the three rows in one frame,
which is the stagger's whole definition and cannot be produced by rows rising together.

Then I re-audited the NEW thresholds against the reviewer's 125 ms host on paper and found two that
would still have failed there — a `0.99` alpha floor, when one 125 ms step of a 0.15 s Fade lands at
`0.995`. Floors are `0.999`: "not at rest", not "well away from rest".

**Stated limit, not hidden:** `A6` needs at least one sampled frame to land inside the ~0.34 s row
run. A host slow enough to step over it entirely fails A6 loudly, and the detail line names which
half was missing. That is the right failure direction; a silent pass would not be.

---

## Invariant JSON (the gate)

`asset_loans_polish_invariants.json` — **`"fail": 0`** over 26 assertions, written by
`GOLFIN ▸ Loans ▸ Probe Loan Polish` (`Assets/Scripts/UI/Loans/Editor/LoanPolishProbe.cs`).
Reviewers should re-run it rather than trust this file.

Three of the six probe runs failed only on the INSTRUMENT, and each fix is recorded in the source
so a reviewer can see the reasoning rather than a silently loosened threshold:

- Run 1 read the **Inventory** copy of the lend modal while the **Roster** copy was on screen (the
  modal is instantiated once per screen). The probe now resolves the *visible* modal after the tap.
- Runs 1–5 asserted a first-sample alpha *floor*, which measures the editor's frame rate rather
  than the tween: `UiMotion.Run` drives a routine's first segment synchronously, so alpha 0 exists
  only inside the frame that starts it and the earliest observable value is `EaseOut(dt/dur)` —
  0.35 at 33 ms, 0.44 at 17 ms, 0.67 at 47 ms. The assertion is now the SHAPE (monotonic, ≥5
  distinct values, ends at exactly 1); the frame-rate-independent proof that the entrance starts
  off-rest is `A1`'s y trace (`maxY > restY`), which no partial sample can fake.
- Runs 2–3 sampled the placeholder phase and the row phase in sequence, so the 0.09 s stagger was
  over before the second loop started, and counted the dying placeholders as animated rows. One
  loop from frame 0, rows classified by their own `UserId`.

---

## Screenshots

Canonical screenshot: `screenshots/polish_pending_midflight_2026-09-09_18-07-45.png`

1170×2532, CaptureCore provenance sidecar `realPlay: true`. It is the one state that cannot be
inferred from a rest frame: LEND's label is `…`, both CANCEL and LEND are dimmed, and the list
behind it shows all three recipients with LUCAS selected.

Supporting frames, all 1170×2532 with `realPlay: true` sidecars:

- `screenshots/polish_ribbon_lender_state_2026-09-09_18-07-31.png` — the lent panel: ribbon + dim at rest after the entrance.
- `screenshots/polish_lend_modal_rows_2026-09-09_18-07-43.png` — the recipient list at rest after the stagger (the frame that proves the defect above is gone).
- `screenshots/polish_lend_modal_empty_2026-09-09_18-07-49.png` — the faded-in empty state.
- `screenshots/polish_club_lent_ribbon_2026-09-09_18-07-58.png` — the club lent panel, ribbon now flush with the artwork.
- `screenshots/polish_return_pending_midflight_2026-09-09_18-07-52.png` — the RETURN modal's pending state (`A14`).
- `screenshots/club_ribbon_before_after.png` — the club ribbon A/B (shipped `asset_loans` capture on top, this build below), 2× crop of the ribbon's left and right edges.

## Video

Canonical video: `videos/asset_loans_polish.mp4` (5.9 MB, 1170×2532, 61.6 s, 30 fps). Re-recorded
after the club fix — the first take filmed the misaligned club ribbon.

Recorded by `GOLFIN ▸ Loans ▸ Record demo` — every tap is a real widget's `onClick`. Captions are
bound to windows whose frames were decoded and checked one by one before burn-in AND again after
burn-in: the stagger at 14.2–17.2 s (modal open, all three rows, none selected), the bump at
22.7–25.7 s (AIKO selected), the ribbon + dim at 29.2–33.2 s (ON LOAN TO MARTA over the dimmed
portrait), and the club ribbon at 57.2–61.2 s (flush on the club artwork). The pending `…` state
is deliberately NOT captioned — the demo's stub answers in one frame, so it is not visible in this
clip; its evidence is the canonical screenshot and `A8`.

---

## After the red-team PASS — its two non-blocking gaps, closed

The red-team passed the task and recorded two gaps it judged not worth an iteration. Both are the
same shape as the defect Cesar rejected: **a quantity nobody measured.** They are closed rather than
carried, because the club ribbon was 27.05 px out and cleared an entire pipeline on the strength of
"reads flush by eye".

Nothing under test changed — the two additions are assertions in the editor-only probe. Both PASS,
so the red-team's verdict stands on strictly more evidence than it was given.

| Gap it noted | Now |
|---|---|
| "no numeric assertion for the ROSTER ribbon's horizontal flush (club-only A12; roster reads flush by eye + no VLG there)" | **`A13_roster_ribbon_on_portrait`** — `ribbon leftD=0.000 rightD=0.000 topD=0.000; dim leftD=0.000 rightD=0.000` against the portrait (537×1483). It WAS flush; it is now measured to be. |
| "A8 measures only LEND pending (RETURN is verified code-parity)" | **`A14_return_pending`** — driven through the real RETURN button on a real borrowed character with the transport delayed 1.5 s: `label 'RETURN' -> '…' -> 'RETURN'; return True->False->True; cancel True->False->True; spinnerActiveMidFlight=False`. Parity was an argument; this is a measurement. Frame: `screenshots/polish_return_pending_midflight_2026-09-09_18-07-52.png`. |

Its third note — the `MatchArtworkX` VLG coupling — it ruled on explicitly as "not a blocker,
recommended as opportunistic hygiene". Left as-is, documented below, and cheap to add whenever Cesar
wants it.

---

## Answers to the reviewer's forwarded notes

**1. The self-review's `_17-37-*.png` frames are absent from disk — that was me, not a mystery.**
Every probe run rewrites all five captures under new timestamps, so after six runs the folder held
26 PNGs of the same five states. I pruned each superseded set as it was superseded, including the
self-reviewer's, keeping only the frames the report actually cites. Nothing was removed to hide a
result: every run's numbers are in that run's JSON, the reviewer's own re-run reproduced the same
verdicts from scratch, and the pruned frames were byte-different only in timestamp. The reviewer's
`_17-46-*` / `_17-47-01` set is left in place alongside the cited `_17-31/32-*` set so the red-team
can compare two independent runs rather than one.

**2. `MatchArtworkX` couples to `VerticalLayoutGroup.childAlignment = UpperLeft` — stated, not
hidden, and here is why it is not derived instead.** The honest alternative is to read the
artwork's live centre and match it. That cannot be done where the builder runs: `BuildClubDetail`
executes in EDIT mode with the Inventory screen inactive, the layout group has not run, and
`ClubImage` reports its centre at the panel's LEFT EDGE — 268 px out. A fix derived from that read
goes the wrong way, which is exactly how the original 27.05 px error was authored. The two ways to
remove the coupling are (a) force a layout rebuild in the builder before measuring — which is the
edit-mode `rect` read that baked 1367 lines of anchor churn twice in this task — or (b) have the
builder ASSERT the alignment and throw if it is ever changed, turning a silent misplacement into a
loud build failure. (b) is cheap and I did not do it, because the reviewer forwarded this as a note
rather than a blocker and I would rather the red-team rule on an explicit claim than find an
undocumented assumption. **If the red-team wants (b), it is a four-line guard in `BuildClubDetail`
and I will add it.**

**3. `LoanRibbonView` lives on the GameObject it deactivates.** That is pre-existing authoring from
`asset_loans` (`LoanUiBuilder` adds the component to the `LoanRibbon` object and wires `ribbonRoot`
to itself), not something this task chose, and it is why `Clear()` deactivates BOTH objects from the
ribbon's single tail rather than one tail each: the `UiMotionRunner` lives on that same GameObject,
so the moment the tail deactivates it, `UiMotionRunner.OnDisable` settles the dim's fade to its own
final value anyway. `A4` covers the outcome (`sawPartialAlphaWhileActive=True`, then both objects
inactive) and `A10` covers the interrupted case. Re-parenting the component would be a scene change
this task's SPEC forbids.

---

## Files modified or created

Every uncommitted path outside this task's folder (Rule 13). Baseline: HEAD
`f58b91eba75e63507b3acc055c32b832713851f1` at kickoff (see `HEARTBEAT.log`); HEAD is now
`609efe9e628b2fc2e428ed533a14243ff94363ad` because another session committed
`flick_arrow_speed_retune` while this task was in flight.

| File | Change |
|---|---|
| `Assets/Scripts/UI/Loans/LoanModalController.cs` | `PendingSpend` scope around the Lend round-trip (scope opened BEFORE the `_pending` latch, disposed BEFORE the verdict is acted on); `UiSelection.Bump` on the newly selected row; `StaggerRise` on the arrived rows and `FadeInPanel` on the empty state; one frame between `ClearRows()` and the spawn; spinner branch dropped from `SetPending`. |
| `Assets/Scripts/UI/Loans/LoanReturnModalController.cs` | The same `PendingSpend` shape around the Return round-trip; spinner toggle dropped. |
| `Assets/Scripts/UI/Loans/LoanRibbonView.cs` | `Rise(dy: -RiseDy)` + dim `Fade` on the hidden→visible transition only; `Then(Fade, deactivate)` on `Clear`; a logical `_shown` flag so a tick repaint plays nothing and a `Show` landing inside a fade-out still enters; `OnDisable` stops/settles both handles; two `CanvasGroup` fields with a runtime-add fallback. |
| `Assets/Scripts/UI/Loans/LoanBadgeView.cs` | `UiSelection.Indicator` in place of `SetActive`, with `animate` gated on a real state change (and off for a card's first paint); `Clear()` routes through `Apply`. |
| `Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs` | `EnsureGroup` helper; a `CanvasGroup` (alpha 1) authored on `LoanRibbon` and `LentDim`; `ribbonGroup`/`dimGroup` wired; `MatchArtworkX` — the club ribbon + dim take the artwork's horizontal anchoring instead of the panel's centre. |
| `Assets/Scripts/UI/Loans/Editor/LoanPolishProbe.cs` (+ `.meta`) | NEW — the invariant probe. Editor-only. Iter-2: every motion assertion re-expressed value-based and same-frame-anchored; `PRE_clock` added. |
| `Assets/Scenes/ShellScene.unity` | +62 / −6 lines: four `CanvasGroup` components (alpha 1) on the two ribbons and two dims, the four field wirings, and the club `LoanRibbon` + `LentDim` x anchoring (`anchorX 0.5 → 0`, `apX 0 → 268.5`). Zero `m_IsActive`; the only geometry change is the club fix Cesar asked for. |
| `Docs/AI_CONTEXT.md` | Session close-out entry. |

**Not mine — pre-existing at kickoff**, each present in the `DIRTY:` block of the iter-1 baseline in
`HEARTBEAT.log`: ` M Docs/Reports/content_art.txt`, ` M Docs/TellCode.md`,
` M Docs/Versioning/last_uploaded_build.txt`, `?? Docs/Diagnostics/roster_locked_overlay/`,
`?? Docs/Specs/Active/loading_tips/`. Left untouched.

**Not mine — appeared DURING this task**, from a concurrent `club_art_batches` session sharing this
Editor: ` M Assets/Resources/Clubs/Full/Driver-Golfin.png` and
` M Docs/Specs/Active/club_art_batches/STATUS.md`. Neither is in the iter-1 baseline and neither is
touched by anything here; the same session is what moved HEAD from `f58b91eb` to `609efe9e`
mid-task. Left untouched — they belong to that task's commit, not this one.

---

## Deviations

**D-1 — no new `confirmLabel` / `returnLabel` field.** The SPEC offered "add a
`[SerializeField] private TMP_Text? confirmLabel;` … or use `BeginOn`; either is fine, pick one and
say which". Neither was needed: `confirmText` and `returnButtonText` are already serialized on the
two controllers and `LoanUiBuilder` already wires each to
`button.GetComponentInChildren<TextMeshProUGUI>(true)` — which is exactly what `BeginOn` resolves.
`PendingSpend.Begin(button, thatLabel, cancelButton)` is used, so the label is the one the builder
asserted rather than one resolved again at runtime.

**D-2 — the two `CanvasGroup`s were authored surgically, not by re-running `LoanUiBuilder`.** The
builder now creates them (the code is in), but re-running the whole builder to get them reshuffled
669 lines of fileIDs across `LoanModal.prefab` and `LoanReturnModal.prefab` — it recreated the
buttons' `Text (TMP)` children under new fileIDs, which is how scene overrides get orphaned. Both
prefabs were reverted to HEAD and the four components were added with `SerializedObject` on the
live scene objects instead. The scene diff is 56 lines and nothing else moved.

**D-3 — `LoanRibbonView` resolves its `CanvasGroup`s lazily if the serialized reference is null.**
Both are wired in `ShellScene` now, but the ribbon also lives in whatever a future builder run
produces, and a null group would silently cost the entrance rather than announcing itself. A
`CanvasGroup` contributes no geometry, so rest parity holds on either path.

**D-4 — `confirmSpinner` / `spinner` stay serialized and stay on their prefabs, inactive.** The
SPEC asked for exactly this (removing a wired object is a prefab edit this task does not need).
`A8c` proves neither is ever activated.

**D-5 — the badge's state-change trace is driven through `LoanBadgeView.Apply`.** That is the
method `CharacterThumbnailCard.RefreshIcons` calls (line 156), so it is the shipped seam, not a
poke at the indicator. It is called directly because the local `PlayerCharacterData.isLentOut` in a
stubbed editor session is whatever the stubbed reconcile left it as; `A9c` prints that value
(`local isLentOut=True`) so the reading is not silently standing on it. The *real repaint* path is
still asserted, by `A9b`.

---

## Out of scope, confirmed untouched

No `ShimmerHost` site for the recipient list (SPEC § Architecture context decision of record).
No sprite-swapped disabled buttons (D-1 in `asset_loans` stands). No change to `LoanService`,
reconciliation, the server, `LocalizationText.csv`, or Figma. No haptics.
