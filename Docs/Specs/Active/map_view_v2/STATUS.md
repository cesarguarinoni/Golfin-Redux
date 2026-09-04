READY_FOR_SELF_REVIEW

map_view_v2 iter-1. Code + Unity verification both complete.

EditMode 2437/2434 passed, 0 failed (tripwire-proven that the 4 new tests execute).
24 play-mode captures at 1170x2532 across Holes 01/08/04, all through the real HoleCard ->
HoleMap onClick path. 12 invariant dumps; maxReach/clubCarry = 1.2000 on every hole;
MapTargetCarryM written back UNCLAMPED. Clone provenance verified by sprite GUID.
Strings live (texts v37) AND the bundled table rebuilt.

Three defects were found and fixed by this pass; three findings are surfaced that the code
cannot fix inside this spec (over-range target off-frame, pin-chip flip unphotographable,
landing ring larger than the mock). See IMPLEMENTER_REPORT.md.
