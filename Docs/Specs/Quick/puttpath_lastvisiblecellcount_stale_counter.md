# Quick spec — `puttpath_lastvisiblecellcount_stale_counter`

**Filed:** 2026-05-25, close-out of `puttpath_predictor_perf_and_design` (commit `242f62f7`)
**Origin:** Architect-reviewer's non-gating follow-up note from iter-3 architect review; re-confirmed at iter-4 close-out.

## Problem

`PutterGreenReader.LastVisibleCellCount` is a test seam left over from the iter-1 (arrow-instance) architecture. In iter-1 it counted CPU-side cells that passed distance + frustum culling — a meaningful number used by the smoke-bot assertion `LastVisibleCellCount >= 50`.

The iter-2-redirect render swap (procedural mesh + URP HLSL shader with shader-side `_BallPosition` distance cull) moved culling into the fragment. The counter still exists, still gets incremented in some legacy CPU code path, but now reads **`visible=0`** in normal operation even when the full grid renders cleanly. The iter-3 + iter-4 smoke-bot scenarios all report `visible=0` and the pipeline (self-reviewer + architect-reviewer) had to override that to PASS based on pixel evidence rather than on the assertion.

This works but it's hygiene debt — the assertion is misleading at best and a future-iteration trap at worst (someone in 6 months will see `visible=0` in the log and assume the grid is broken).

## Fix — pick one

**Option A — Remove the counter + the bot assertion.** Simplest. The visual gate (screenshot/video) is the ground truth; the counter was redundant once the shader-side cull arrived. Delete:
- `PutterGreenReader.LastVisibleCellCount` field + any increments
- The `LastVisibleCellCount >= 50` line in `Scenarios.cs` `PutterAimGreenReaderVisible` (and `PutterAimWarpedGridOnTestGreen` if applicable)
- Update IMPLEMENTER_REPORT-style references (none active; folder is in `Completed/`)

**Option B — GPU-readback to count visible fragments.** Heavier. Adds a one-frame `AsyncGPUReadback.Request` on a flag buffer to actually count fragments that passed the shader's distance cull. Useful only if there's a real reason to keep an assertion-friendly cell count.

**Recommended: Option A.** The counter was an iter-1 implementation detail, not a product requirement. The bot already has visual evidence as its gate.

## Acceptance

- `grep -n "LastVisibleCellCount" Assets/` returns no hits.
- Bot scenarios `PutterAimGreenReaderVisible` (Hole 1) and `PutterAimWarpedGridOnTestGreen` (TestGreen) run to completion without the dead assertion logging PARTIAL.
- Tests-run still 334/331/0/3 (or whatever the current baseline; nothing breaks).
- No screenshot/video re-capture needed; existing iter-4 canonical evidence stands.

## Out of scope

- Re-running the full PUTTPATH pipeline (Cesar's already approved iter-4; this is hygiene).
- Touching the shader, the mesh, or any SerializeField.
