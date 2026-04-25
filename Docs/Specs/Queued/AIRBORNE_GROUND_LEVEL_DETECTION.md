# SPEC (QUEUED) — Airborne ground-level detection (rising-ground tunneling fix)

**Date:** 2026-04-25
**Status:** Queued — activate when triggers fire (see below)
**Pointer in:** `Docs/Specs/Active/SIM_BAKED_DATA_PATH.md` Phase E followup; recorded in `MILESTONE_3_DONE.md` notes
**Estimated effort:** 0.5–1 day implementation + 0.5 day Phase 1–6 bit-exact re-verification
**Prerequisite:** SIM_BAKED_DATA_PATH branch merged to main; M3.5 known-failing fixtures intact

## Activation triggers

Activate this spec when ANY of these is true:

1. **Phase E manual confirmation reveals a fall-through in real play** on one of the 4 known-failing fixtures (DriverFromGreen-E, DriverFromGreen-W; WedgeFromBunkerEdge-{2 directions Code identifies}). Cesar judges visually whether it matters.
2. **Before AI Caddie work begins.** AI Caddie depends on deterministic server-replay; near-tangential ground tunneling is a server-replay correctness bug whether or not players notice it.
3. **Before public testing.** Public players will fire shots in directions our automated tests haven't sampled; the residual 4 directions imply other untested directions across other holes likely have the same bug.
4. **Any new test failure with the same signature** — ball at apex, ground rising faster than ball descending, ball clips below and stays embedded for >3 frames. Same root cause, expand fixtures and fix.

## Background

After the architectural pivot to baked sim providers (SIM_BAKED_DATA_PATH spec, completed 2026-04-25), regression testing revealed 4 of 24 fixtures still fail — but with a fundamentally different failure mode than the pre-pivot bug.

Pre-pivot: ball ignored ground entirely, fell to Y=-2300 (architecture-class bug — eliminated).

Post-pivot: ball clips into rising terrain at shallow incidence at apex of trajectory, then stays monotonically embedded because `SimulateAirborne`'s HitGround edge-detector never fires.

Per-step CSV at `Docs/DIAG/baked-pivot/M3-failing-shots/DriverFromGreen-E.csv` shows the pattern at frame 231:
- Ball horizontal velocity high; vertical velocity near zero (apex)
- Ball Y descends ~1 cm/frame
- Ground Y rises ~5 cm/frame (ball flying horizontally into rising terrain)
- Net signed distance (ball.Y − groundY) was positive at frame 230, becomes negative at frame 231
- HitGround condition `posNext.y <= groundY && pos.y > groundY` requires both inequalities — but at frame 231 `pos.y` (the previous frame's ball Y) is now compared to `groundY` (ground at the NEW XZ). When ground rose between frames, the previous-frame ball-Y can be at-or-below the current-frame ground-at-new-XZ even though the ball was correctly above ground at the previous XZ.
- Bug compounds: every subsequent frame `pos.y` and `groundY` both keep updating, the "was above" clause stays false, ball never registers contact.

The bug is not classical tunneling (no missed sub-frame crossing). The ball IS correctly sampled inside ground; the test condition is structurally wrong for the rising-ground case.

## The actual bug: edge-detector vs. level-detector mismatch

`SimulateAirborne` uses an edge-detector for HitGround:
```csharp
fp groundY = ground.SampleHeight(posNext.x, posNext.z);
if (posNext.y <= groundY && pos.y > groundY) { ... }
```

The `pos.y > groundY` clause compares **previous-frame ball-Y** to **current-frame ground-Y at the new XZ**. This is a category error. When the ground rose between frames, this clause is false even when the ball was correctly above ground in the previous frame at the previous XZ.

Conventional CCD literature (Coumans/Bullet, nphysics, Toptal "Game Physics Tutorial Part II") describes this class of issue as a tunneling variant; the canonical fix is a signed-distance comparison sampled at both ends of the integration step.

## Fix recommendation: signed-distance level-detector

Replace the edge-detector with a signed-distance check sampled at BOTH ends of the step:

```csharp
fp groundYprev = ground.SampleHeight(pos.x, pos.z);
fp groundYnext = ground.SampleHeight(posNext.x, posNext.z);
fp signedPrev = pos.y - groundYprev;       // positive = above ground at start
fp signedNext = posNext.y - groundYnext;   // negative = below ground at end

if (signedNext <= fp.Zero && signedPrev > fp.Zero)
{
    fp denom = signedPrev - signedNext;
    fp frac  = denom.raw == 0 ? fp.Zero : signedPrev / denom;
    // Existing interpolation math for hitPos, hitVel, tHit unchanged.
    // ...
    termination = TerminationReason.HitGround;
    break;
}
```

**Why this works for the rising-ground case:**
- At frame 230: ball was above ground at (pos.x, pos.z) → `signedPrev > 0`.
- At frame 231: ball is below ground at (posNext.x, posNext.z) → `signedNext < 0`.
- Condition fires correctly. Linear interpolation finds the within-step crossing.

**Why this preserves bit-exactness for Phase 1–6 tests (most likely):**
- Trajectories where the old edge-detector fired had `pos.y > groundY_at_posNext_XZ` true. For approximately flat terrain (Phase 1–6 vacuum/aero/wind/surface tests), `groundY_at_pos_XZ ≈ groundY_at_posNext_XZ`. The signed-distance fraction `signedPrev / (signedPrev - signedNext)` reduces algebraically to the existing edge-detector's `frac = (pos.y - groundY) / (pos.y - posNext.y)` formula.
- **Bit-exactness is highly likely but NOT guaranteed.** Q16.16 fixed-point arithmetic on `signedPrev / (signedPrev - signedNext)` has different intermediate values than `(pos.y - groundY) / (pos.y - posNext.y)`. Even algebraically equivalent expressions can differ at the last bit due to fp truncation. Phase 1–6 re-verification is mandatory.

**Why this is simpler than alternatives considered:**
- Sub-stepping (Φ2 in M3.5 escalation): breaks Phase 5's fixed-tick determinism premise. Architecturally wrong.
- Conservative advancement (full CCD literature): overkill for a 1D heightmap problem; designed for arbitrary convex collider pairs.
- Just dropping the `pos.y > groundY` clause: introduces frame-0 spurious termination for balls placed exactly at ground level. Wrong.

The signed-distance approach is the smallest change that correctly handles rising ground while preserving the "must have been above ground" semantics. ~5 lines changed in `SimulateAirborne`.

## Implementation plan

1. **Branch from `main`** (post-merge of SIM_BAKED_DATA_PATH): `git checkout -b airborne-ground-level-detection`.
2. **Modify `BallSimulation.SimulateAirborne` HitGround check** as shown above. ~5 lines changed.
3. **Run all existing Phase 1–6 tests.** This is the bit-exact gate. If any test fails:
   - Diff the expected vs actual trajectories to confirm only the last-bit fp arithmetic differs (not a real correctness regression).
   - If the diff is real (terminates at a different step, different bounce count, etc.), STOP and surface to Architect — the fix may need refinement to preserve bit-exactness for slow-varying-ground cases.
   - If the diff is last-bit only and tests need their golden values updated, document and update.
4. **Run BakedPivot regression suite.** All 24 fixtures should now PASS (4 currently-failing should resolve). Remove the `[ConditionalIgnore]` markers on the 4 known-failing fixtures — they must pass unconditionally after this fix.
5. **Run RealHoleTerrainTests.** Should pass at same rate as before.
6. **No manual smoke test required** for this fix specifically (the BakedPivot regression suite is the contract). Cesar can spot-check post-merge.
7. Merge to main, delete branch, close this spec.

## Known-failing fixtures (must pass after this fix)

From M3.5 done report (latest commit on sim-baked-data-path branch, see TellCode.md M3.5 entry for hash):

- `RegressionTest_DriverFromGreen_StaysOnGreen` direction E (one of 8)
- `RegressionTest_DriverFromGreen_StaysOnGreen` direction W (one of 8)
- `RegressionTest_WedgeFromBunkerEdge_StaysOnSurface` 2 of 8 directions (specific directions in the M3.5 done report)

These are marked `[ConditionalIgnore]` (or equivalent) in M4 with comments referencing this spec. After this fix, the markers must be removed and the fixtures must pass unconditionally.

## DO NOT

- Do NOT add sub-stepping or adaptive timestep to fix this. Phase 5 determinism depends on fixed 1/240s ticks.
- Do NOT introduce any CCD library or external physics dependency.
- Do NOT modify `RunRollPhase` or `RunPuttPhase` HitGround logic — they snap to ground every step, so they don't have this bug.
- Do NOT modify the bounce loop's nested `SimulateAirborne` calls' HitGround — same fix applies once and propagates (the bounce loop calls SimulateAirborne which contains the fixed check).
- Do NOT skip the Phase 1–6 bit-exact re-verification gate. If we lose Phase 5 putting determinism, AI Caddie's server-replay capability is at risk.
- Do NOT delete or relax the BakedPivot regression fixtures to make them pass artificially.

## Out of scope

- Other near-tangential collision cases (ball rolling along a steep slope, ball flying parallel to a wall). Different code paths.
- Continuous collision detection for the bounce loop's restitution math. The bounce response itself is fine.
- Ground-classification mismatch at the within-step crossing point (`hitPos` is interpolated linearly between two XZ that may have different surface classifications). Existing behavior handles this.

## Test additions (after fix lands, optional)

Add a stress fixture to lock the rising-ground case in:

`RegressionTest_BallApexIntoRisingGround_DetectsContact`:
- Programmatically place ball on a flat surface with a steep slope ahead.
- Fire at low launch angle so ball arrives at apex over the rising slope.
- Assert HitGround fires at the geometrically correct step (within ±1 frame of the analytical answer).

This catches any regression on this specific bug class.
