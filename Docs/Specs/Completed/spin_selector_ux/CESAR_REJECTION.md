# Cesar Rejection #2 — `spin_selector_ux` (Order 354)

> Manual rejection AFTER iter-5 `ARCHITECT_REVIEW_PASS` (self-rev + reviewer + red-team all passed it).
> STATUS → `CESAR_REJECTED`. Logged to `.claude/review_misses.log`.
> This supersedes Rejection #1 (the HIGH-disc-bigger-than-ball defect, 2026-06-16 17:27), which iter-5 *did* fix — but iter-5 regressed the look while fixing it.

## Cesar verbatim

> **1-** Dim should be circular. It seems there is something over the usual ball as well (a white layer?). It should be dimmed where you can't put the spin and the normal ball where you can (so in the case of High, you would see the normal ball. So in short, a circular dim with a cut where you can put the spin.
>
> **2-** Why did it break? And why the screenshots in empty space? I requested to see things in as close as gameplay environment as possible already

## Defects (iter-5 canonical: `screenshots/spin_iter5_HIGH_plus10_final.png`, `..._LOW_minus10_final.png`)

### D-1 — The dim is a SQUARE box, must be CIRCULAR. (HARD FAIL)
`_grayOutRt` is a 600×600 **square** Image whose donut texture dims everything outside the center hole — including the square's corners. Result on screen: a dimmed **rectangular box** around the ball, not a circular dim that follows the ball's round edge. Cesar: *"Dim should be circular."*

**Fix:** the dim must be a circular annulus — fully transparent in the center **cut** (where spin is allowed), dimmed only between the cut radius and the ball's visible edge, and **fully transparent again OUTSIDE the ball radius** so there is no square/box silhouette. No dimmed pixels may appear in the corners of the gray-out element. Either author the dim texture as a circular ring that fades to alpha 0 beyond the ball radius, or clip the dim to a circular mask the size of the ball.

### D-2 — White layer washing over the ball; the cut must show the NORMAL ball. (HARD FAIL)
There is a bright/white overlay over the whole ball (the kept `SpinActiveDisc` ring at alpha 0.35 and/or the donut's inner fill tint). Even inside the cut, the ball looks faded/washed, not the clean normal ball. Cesar: *"something over the usual ball as well (a white layer?) … the normal ball where you can [put spin]."*

**Fix:** inside the cut (the spin-allowed region) the player must see the **pristine, un-tinted ball** — zero overlay alpha, no white ring, no disc fill. Remove or fully clear the `SpinActiveDisc`/white overlay inside the cut. If a delineation of the active disc edge is still wanted, it must be a thin, unobtrusive line, NOT a translucent fill across the ball. At HIGH (+10) the cut ≈ the whole ball, so Cesar should see essentially the normal ball with only a hair of dim at the rim.

### D-3 — Captures taken in empty space (LabScaffold), must be a real loaded hole. (HARD FAIL — standing rule)
iter-5 stills were shot over `Assets/Scenes/Physics/LabScaffold.unity` (flat sky/ground physics lab). Cesar's standing rule (in memory: `feedback_real_world_game_testing`, `feedback_capture_resolution_iphone14`): verify gameplay-facing features via the **real game flow** (boot ShellScene → `GameplaySceneLoader.BeginGameplayLoad`), capture over a **real loaded hole**, at iPhone-14 1170×2532. Never direct-load LabScaffold for review evidence. He explicitly says he already requested this.

**Fix:** re-shoot HIGH (+10) and LOW (−10) over a real loaded hole through the real boot flow. Recipe is in `golfin-implementer.md`.

## Answers to Cesar's questions (architect)

- **"Why did it break?"** iter-5 was fixing Rejection #1 (HIGH disc bigger than the ball). To cap the disc/hole to the sprite edge, the implementer shrank the gray-out to a fixed 600×600 box and capped the cut to the ball radius. Side effects: the soft circular dim from iter-3 became a hard **square**, and the disc-ring/donut overlay started **washing the ball white**. The geometry fix landed (D3 physics contract intact) but it regressed the visual.
- **"Why the screenshots in empty space?"** The implementer captured over the LabScaffold lab scene instead of booting the real game flow. That's the violation in D-3.

## What "correct" looks like (one sentence)
A **circular** dim that hugs the ball: a transparent **cut** in the middle (sized by the spin stat — large at HIGH, small at LOW) revealing the **clean, un-tinted normal ball**, dim only in the ring between the cut and the ball edge, and **nothing** (no dim, no box) outside the ball — all shown over a **real loaded hole**.

## Required evidence on resubmit (Rule 15)
Re-shoot the SAME two states (HIGH +10, LOW −10) at full res **over a real loaded hole**:
- HIGH frame: dim is a thin circular rim at most; the ball reads as the normal ball; no square box; no white wash.
- LOW frame: small circular cut of normal ball in the center, circular dim ring around it, nothing outside the ball circle.
- State the dim shape (circular, alpha 0 outside ball radius) and the cut radius in px numerically, and confirm the scene loaded is a real hole (name it), not LabScaffold.
