# Cesar's rejection of iteration 2 — `8_3_topbar`

**Date:** 2026-04-28 (after architect-subagent PASS at 17:05 JST)
**Iteration rejected:** 2
**Screenshot reviewed:** `screenshots/2026-04-28_16-41-44.jpg`

## Why this file exists

The architect-subagent passed iteration 2 with verdict `ARCHITECT_REVIEW_PASS`, including ruling "cornerRadius 8 deferred as polish follow-up." Cesar then manually reviewed the screenshot and rejected the result for two issues the architect-subagent's deferral missed.

This file is the canonical record of Cesar's rejection. **If this file is dated AFTER `ARCHITECT_REVIEW.md`, Cesar's rejection wins.** Treat the architect review as superseded by this file.

## What Cesar rejected

1. **Frames need rounded corners (radius 8).** The architect deferred this; Cesar wants it now. Figma has `cornerRadius: 8` on both `In-game Portrait` containers; the rendered frames in iteration 2 are sharp 90° squares, which is a noticeable visual regression from the reference.
2. **Chip stacks touch at the center of the screen with no breathing room.** Figma has a clear ~118px center gap visible in the rendered frame; Unity has zero gap. Cesar wants the gap restored. Note: the true root cause (Unity-vs-Figma size mismatch, ~1.20×) is being investigated separately tomorrow; tonight is a stopgap-only fix.

## What was approved

Everything else from iteration 2 stays. Settings position (-58, -24), player chip Middle Left alignment, hole chip Middle Right alignment, RarityBackground / HoleMapBackground colors and structure, all PASS. Do not touch these in iteration 3.

## What iteration 3 must do

See `SPEC.md` § "Iteration 3 patches" for the full spec. Summary:
- Fix 5: rounded corners (radius 8) on `PortraitContainer` and `HoleMapContainer` via Mask + 9-sliced rounded-rect sprite.
- Fix 6: shorten ChipStack width 298 → 248 with slack opening on the screen-center-facing side.

## What this means for the pipeline

`STATUS.md` should sit at `ARCHITECT_REVIEW_FAIL` (not `ARCHITECT_REVIEW_PASS`) until iteration 3 is implemented and re-reviewed. The route hook will then send the implementer subagent to redo. Do NOT revert STATUS based on the architect's PASS verdict alone — this file overrides it.
