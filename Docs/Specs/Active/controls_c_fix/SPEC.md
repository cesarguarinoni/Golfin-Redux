# SPEC — `controls_c_fix` — C.1 + C.2 fix (Phase A: stop-check repair + Green/GreenCollar/CartPath tuning + 5 validation tests)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. NOTES.md is architect working notes — do NOT use it as the work definition. Reports/reviews go in their own files.

**Created:** 2026-05-05 (Architect session)
**Architect:** Claude (claude.ai)
**Roadmap:** `Docs/Roadmap.md` §1 — gates §2 (Loop v1)
**Notion:** `C.1 + C.2 fix — controls_c_fix (Phase A)` (`35631e0e-9a36-8176-add4-e5bc40877f0f`) — Status: In Progress, P0 Critical, M (1–2 days), Order 125
**Predecessor (DONE):** `Docs/Specs/Completed/controls_c_diagnosis/` — diagnosis that produced the evidence this spec acts on.
**Successor (Phase B, queued):** `controls_c_fairway_rough_tuning` — opens after Phase A's observation tests give us real Fairway/Rough numbers.

## Status

See `STATUS.md` for current pipeline state.

## Goal

Fix the two issues that the diagnosis collapsed into a single root-cause bundle:

- **C.1** "Putter shoots ~100 yd" — turned out to NOT be a velocity-resolution bug. The putter pipeline is correct end-to-end (override 5 m/s → IsPutt=true → captured `velMagnitude=2.05 m/s` at 41% effort). The "100 yd" symptom was actually the rolling-resistance integration `d_max = v₀/k` producing **17.30 m total roll** for a 41% putt on a Green→Fairway transition combined with the broken stop-check (so the ball never visibly stopped, making Cesar assume launch was wrong).
- **C.2** "Ball rolls forever regardless of surface" — root cause is `stopConsecutive` clause 2 (`speedSq <= prevSpeedSq`) failing under fp-rounding noise at very low speeds. Two interacting mechanisms (re-derived from the captured Shot 1 + Shot 2 evidence — `gTan=0.000m/s²` printed yet `stopConsec=0` for the full 75 s on CartPath, which rules out the slope-re-acceleration story we initially proposed in NOTES.md):
  - **(i) Per-component fp16.16 rounding ticks `speedSq` up.** `speedSq = vx² + vy² + vz²` is computed in fp16.16 (LSB ≈ 1.5e-5). When one component rounds down by 1 LSB and another doesn't, the sum can tick UP between steps even on flat ground with no real acceleration. This breaks strict `<=` non-increase.
  - **(ii) When `k·|v|·Dt < 1 LSB`, resistance is a no-op.** At |v|=0.0625 m/s with the OLD CartPath k=0.06 and Dt=1/240, per-step velocity-decrement = `0.06·0.0625/240 = 1.56e-5` ≈ 1 LSB. Most steps round to zero and the ball doesn't slow at all. Fairway in Shot 1 (k=0.18) gave `4.69e-5` ≈ 3 LSBs of resistance per step — beatable some of the time, which is exactly why Shot 1's counter advanced 0→8 sporadically over 336 steps while Shot 2's counter never left 0 for the full 75 s.

  The CSV bumps in Step 3 + Step 4 directly fix mechanism (ii) by raising k so that resistance per step always exceeds the LSB floor at sub-stopSpeed velocities. The tolerance-window epsilon in Step 1 + Step 2 absorbs mechanism (i) — the residual fp/rounding noise that no value of k can eliminate.

Phase A delivers three orthogonal changes that together close C.1 + C.2:

1. **Stop-check repair** — add a tolerance window to clause 2 of the stop-check, applied identically to both `RunRollPhase` and `RunPuttPhase`.
2. **CSV tuning, minimal** — bump Green/GreenCollar/CartPath `RollingResistance` to realistic values. **Do NOT touch any other surface in this task** — Fairway, Rough, Semirough, Sand, BunkerLip, Tee are explicitly Phase B (observation-only here).
3. **Validation tests, new file** — 5 EditMode tests that exercise the fix and produce numbers Cesar can compare against real-golf bands. Bit-exact gate goes from 198 → 203.

After this lands, the Notion entries for **C.5** (velocity cap diagnostic) and the **`controls_c_fairway_rough_tuning`** Phase B follow-up open. Loop v1 (Roadmap §2) opens after Phase A passes Cesar's Active spec smoke-check.

## Why these three changes (and only these three)

- **Stop-check repair only on clause 2.** Clause 1 (`speedSq < stopThresh`) is the canonical "ball is below stop speed" check and is correct. Clause 2 (`speedSq <= prevSpeedSq`) is a guard against false-positive stops while the ball is rolling **uphill** at sub-stopSpeed (it should NOT count as stopped because it's about to roll back). The bug is mechanism (i) above: per-component fp16.16 rounding ticks `speedSq` up by ~1 LSB at sub-stopSpeed even on flat ground, which breaks strict `<=` non-increase.

  **Three options were weighed (NOTES.md § "stop-check repair candidates"):**
  - Drop clause 2 entirely → loses the uphill safety. Rejected.
  - **Tolerance window** (option 2): `speedSq <= prevSpeedSq + epsilon` where `epsilon = stopSpeed² · 0.05`. Closest to original intent without dropping safety. **CHOSEN.** Sized at 5% of `stopSpeed²` because: (a) at typical stopSpeeds (0.04–0.10), 5% of stopSpeed² is 6.4e-5 to 5e-4 — comfortably above the 1.5e-5 fp16.16 LSB so the tolerance survives fp-rounding to a meaningful value, and (b) genuine uphill re-acceleration on a real golf-course slope (e.g. 2° on a green = `g·sin(2°) ≈ 0.34 m/s²`) bumps `speedSq` per step by `2·v·a·Dt ≈ 1.4e-4` at v=0.05 m/s — about 3× our 5% epsilon at Green stopThresh, so genuine accelerations still fail clause 2 and the uphill safety is preserved. The 1% value originally proposed was too tight (would equal 1 LSB at Green's stopThresh, providing no meaningful tolerance over fp noise).
  - Two-stage stop with slope-aware override → overkill for the symptom. Rejected.

- **CSV tuning narrowly scoped to Green/GreenCollar/CartPath.** Cesar's "you pick the best" instruction (NOTES.md § "Decisions locked") authorised these three values. Other surfaces (Fairway, Rough, Semirough, Sand, BunkerLip, Tee) **are not touched in this task** because we don't yet have captured data for them — the diagnosis only produced evidence on Green→Fairway transition (Shot 1) and airborne→CartPath (Shot 2). Phase A's validation tests will surface real numbers for the un-tuned surfaces; Phase B (`controls_c_fairway_rough_tuning`) tightens those values once observed.

- **5 validation tests, not 1, not 10.** One per behavioural concern: Stimpmeter (putt feel), long-putt (putt distance ceiling), driver→Fairway (observation-only), CartPath stop (the C.2 catastrophic case), stop-check correctness (the structural fix). Tighter band assertions on Green/GreenCollar/CartPath; loose catch-catastrophic-drift on Fairway/Rough.

## Reference

- **Predecessor evidence:** `Docs/Specs/Completed/controls_c_diagnosis/IMPLEMENTER_REPORT.md` § "Diagnostic capture" — both shots' captured logs.
- **Architect review of predecessor:** `Docs/Specs/Completed/controls_c_diagnosis/ARCHITECT_REVIEW.md`.
- **Architect working notes for this task:** `Docs/Specs/Queued/controls_c_fix/NOTES.md` (also moves to `Active/` with the SPEC; informational only — do NOT treat as work definition).
- **Realism source for Green tuning:** USGA Stimpmeter standard (release velocity 6.00 ft/s = **1.829 m/s**; "fast" green roll-out = 12 ft = **3.66 m**; "slow" = 6 ft = 1.83 m). Sources cross-checked: Stanford PH210 Kolkowitz 2007 (citing Holmes 1991 + Werner & Greig 2000) and the Wikipedia Stimpmeter article citing Brede 1990 USGA-validated work.
  - https://en.wikipedia.org/wiki/Stimpmeter
  - http://171.67.100.116/courses/2007/ph210/kolkowitz2/
- **Cart-path k=0.30 source:** **playability-calibrated, NOT literature-calibrated.** No published rolling-friction coefficient for golf-ball-on-concrete-cart-path was found in 6 academic-search passes. Value chosen to stop a 5 m/s post-bounce ball within ~16 m, which is the playable-distance bound; bumped from k=0.06 which produced 100m+ infinite roll-out (Shot 2 evidence). Phase B will tighten if lab observation flags drift.
- **No Figma reference** — this is sim-internal physics; nothing visual to diff.

## Architecture context

**Asmdef boundaries affected:** none. All edits live in:
- `Golfin.Physics` (Core asmdef) — `BallSimulation.cs` stop-check repair.
- Resources CSV files (no asmdef) — `surfaces.csv`, `putt.csv` value tuning.
- `Golfin.Physics.Tests` (Tests asmdef) — new test file.

No asmdef edits, no new asmdefs, no scene/prefab changes.

**Existing code referenced (Implementer reads these end-to-end before starting):**
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — full file. Two stop-check sites at lines **537–552 (`RunRollPhase`)** and **670–682 (`RunPuttPhase`)**. They are literal copies of the same logic. The fix applies identically to both.
- `Assets/Scripts/Resources/Physics/surfaces.csv` (path: `Assets/Resources/Physics/surfaces.csv`) — single line edit (CartPath row).
- `Assets/Resources/Physics/putt.csv` — two line edits (Green row + GreenCollar row).
- `Assets/Scripts/Physics/Core/PuttConfig.cs` — **DO NOT EDIT.** This file holds the C# `PuttConfig.Default` fallback values. Existing tests call `PuttConfig.Default` directly (not the CSV loader) — leaving Default untouched preserves the bit-exact gate. The CSV is loaded at runtime via `PhysicsConfigLoader.LoadPuttConfig()` and overrides Default in the lab/game; this is the only path that picks up the new values.
- `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — confirms `LoadSurfaceConfig` and `LoadPuttConfig` parse `rolling_resistance` from column index 3 / 1 respectively. No changes required.
- `Assets/Scripts/Physics/Tests/PuttTests.cs` — existing patterns to mirror in the new test file (`SplitSurfaceProvider` inner class, helper `IronInput`, NUnit `[Test]` attributes, fp construction).

**Manager APIs added (NEW):** none. This task adds no new public API. The four diagnostic loggers added by `controls_c_diagnosis` (`DiagShotLogger`, `DiagRollLogger`, `DiagBuildLogger`, `LogResolution`) are **kept in place unchanged** — they're useful for verifying the fix in lab.

## Model choice & literature alignment

This section anchors the implementer (and future-you) so nobody second-guesses the modeling choice mid-task.

**Our model is linear viscous damping** — `dv/dt = -k·v + g_tangent` per `BallSimulation.cs:652` and Phase 5 spec. Chosen deliberately in `Docs/Physics/PHYSICS_RESEARCH.md` for clean math + CSV tunability + bit-exact determinism.

**Published academic literature uses Coulomb friction** — `dv/dt = -(5/7)·μ·g + g_tangent`. Every paper found in the literature search uses this form: Penner 2002 "The Run of a Golf Ball," Penner 2003 "The Physics of Golf," Holmes 1991, Werner & Greig 2000, Roh 2010 (ScienceDirect S1877705810003929) reports **green μ_r 0.05–0.1**, Rim 2012 (IEEE 6316601) confirms the same range, and Brown & McPhee 2024 (Springer 10.1007/s12283-024-00481-5) extended this with a variable-friction model noting that *constant Coulomb deceleration underestimates roll distance* — which means our viscous model rolling slightly further than published Coulomb sims is not a bug, it's expected.

**Calibration mapping for k=0.50 (Green):** USGA Stimpmeter releases at v₀=1.829 m/s; fast green = 3.66 m roll. Our viscous integral `d_max = v₀/k` gives `1.829/0.50 = 3.66 m` exactly. The viscous k=0.50 is therefore the **direct viscous-equivalent of Stimp 12 PGA Tour fast green**. Coulomb-equivalent for the same roll distance would be μ ≈ 0.046 — neither value is wrong; they're the same calibration target through different equations.

**Why this matters for the spec:** future-you searching the literature for "k=0.50 putt" will find nothing (Coulomb sims use μ); searching for "viscous damping golf ball roll" will also find nothing (literature is Coulomb). Both are normal — we're using a non-standard model deliberately. **Switching from viscous → Coulomb is OUT OF SCOPE for Phase A.** That would invalidate every existing physics test (≥184 tests use the current model), require re-tuning every surface k value to a new μ value, and is a substantial separate spec.

**Implications for the stop-check fix:**
- Coulomb has finite stop time; viscous asymptotes to zero. **The stop-check is structurally more important under viscous than under Coulomb** — there is no "the ball naturally stops" fallback. Every stuck stop-check is an infinite roll. This is why C.2 manifests this severely.
- The fp-rounding mechanisms documented in C.2 above are also more severe under viscous because the dynamics live longer at sub-stopSpeed velocities.
- Linear viscous + Stimp-12 calibration produces the same TOTAL roll distance as published Coulomb sims by construction. We diverge from literature only on the *shape* of the deceleration curve (asymptotic vs. linear), not on the stopping distance. That's a fair trade for clean math.

## Implementation

### Step 0 — Read the existing stop-check code

Open `Assets/Scripts/Physics/Core/BallSimulation.cs`. The two stop-check blocks to modify are at:

**`RunRollPhase` (lines 537–552):**

```csharp
fp speedSq    = fpMath.Dot(vel, vel);
fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
if (speedSq < stopThresh && speedSq <= prevSpeedSq)
{
    stopConsecutive++;
    if (stopConsecutive >= StopStepsRequired)
    {
        hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
        return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
    }
}
else
{
    stopConsecutive = 0;
}
prevSpeedSq = speedSq;
```

**`RunPuttPhase` (lines 670–682):**

```csharp
fp speedSq    = fpMath.Dot(vel, vel);
fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
if (speedSq < stopThresh && speedSq <= prevSpeedSq)
{
    stopConsecutive++;
    if (stopConsecutive >= StopStepsRequired)
    {
        hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
        return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
    }
}
else stopConsecutive = 0;
prevSpeedSq = speedSq;
```

These are literal copies of each other (the `else` branch is single-line in `RunPuttPhase`, multi-line braced in `RunRollPhase` — that's the only stylistic difference). The fix is identical for both.

### Step 1 — Apply the stop-check tolerance-window fix to `RunRollPhase`

Replace the `RunRollPhase` block (lines 537–552) with:

```csharp
fp speedSq    = fpMath.Dot(vel, vel);
fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
// Phase A C.1+C.2 fix: tolerance window on clause 2.
// At sub-stopSpeed velocities, per-component fp16.16 rounding (LSB ≈ 1.5e-5)
// can tick speedSq UP by 1 LSB even on flat ground with no real acceleration
// — when one velocity component rounds down and another doesn't, vx²+vy²+vz²
// nets a 1-LSB increase. That breaks strict <= non-increase and stalls the
// stop-counter (captured: 75 s on flat CartPath with stopConsec stuck at 0).
// We allow speedSq to tick up by up to 5% of stopSpeed² per step and still
// count the step toward the stop streak. 5% sized so: (a) >> 1 LSB at all
// realistic stopSpeeds (0.04–0.10), and (b) << genuine uphill re-acceleration
// on a 2° real-course slope, which preserves clause 2's uphill safety guard.
fp stopEpsilon = stopThresh * fp.FromFloat(0.05f);
if (speedSq < stopThresh && speedSq <= prevSpeedSq + stopEpsilon)
{
    stopConsecutive++;
    if (stopConsecutive >= StopStepsRequired)
    {
        hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
        return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
    }
}
else
{
    stopConsecutive = 0;
}
prevSpeedSq = speedSq;
```

**Critical:** the `+ stopEpsilon` goes on the right-hand side of `<=`, NOT on the left. (Rewriting as `speedSq - stopEpsilon <= prevSpeedSq` gives the same algebra in real numbers but a different result in fp because of subtraction-underflow at very low magnitudes.)

`fp.FromFloat(0.05f)` is the canonical way to construct a 5% scalar in this codebase (see `BallSimulation.cs:16` `fp.FromFloat(0.5f)` for `RollTransitionThreshold`). Do not introduce a `private static readonly fp StopEpsilonScale` constant — keep the literal inline so the comment + magic-number relationship is obvious at the edit site.

### Step 2 — Apply the same fix to `RunPuttPhase`

Replace the `RunPuttPhase` block (lines 670–682) with the structurally identical:

```csharp
fp speedSq    = fpMath.Dot(vel, vel);
fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
// Phase A C.1+C.2 fix: tolerance window on clause 2 (same fix as RunRollPhase).
// 5% of stopSpeed² absorbs fp-rounding noise without admitting genuine slope
// re-acceleration. See RunRollPhase comment for full derivation.
fp stopEpsilon = stopThresh * fp.FromFloat(0.05f);
if (speedSq < stopThresh && speedSq <= prevSpeedSq + stopEpsilon)
{
    stopConsecutive++;
    if (stopConsecutive >= StopStepsRequired)
    {
        hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
        return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
    }
}
else stopConsecutive = 0;
prevSpeedSq = speedSq;
```

(Preserve the single-line `else` style here to match the surrounding RunPuttPhase code; it's the only stylistic difference from RunRollPhase.)

### Step 3 — Tune `Assets/Resources/Physics/putt.csv`

Open `Assets/Resources/Physics/putt.csv`. Current content:

```
surface,rolling_resistance,stop_speed_mps,notes
Green,0.10,0.04,Stimp ~10 feel; canonical putting-green roll
GreenCollar,0.14,0.05,Slightly slower than green; same family
```

Replace with:

```
surface,rolling_resistance,stop_speed_mps,notes
Green,0.50,0.04,Stimp ~12 PGA Tour fast; v0/k = 1.829/0.50 = 3.66m matches USGA Stimpmeter (Kolkowitz 2007, Wikipedia Stimpmeter)
GreenCollar,0.40,0.05,Slightly slower than green; v0/k = 4.57m, between Stimp ~10 (medium) and ~12 (fast)
```

Only `rolling_resistance` and `notes` change; `stop_speed_mps` stays at the existing values (0.04 / 0.05).

### Step 4 — Tune `Assets/Resources/Physics/surfaces.csv` (CartPath row only)

Open `Assets/Resources/Physics/surfaces.csv`. Find the CartPath row (currently):

```
CartPath,0.70,0.18,0.06,0.08,very bouncy; very low friction
```

Replace it with:

```
CartPath,0.70,0.18,0.30,0.08,very bouncy; very low friction; PLAYABILITY-CALIBRATED (no published academic value for golf-ball-on-concrete); k bumped from 0.06 (which produced 100m+ infinite roll-out in C diagnosis Shot 2) to 0.30 (which stops a 5 m/s post-bounce ball within ~16 m); also raises per-step resistance above the fp16.16 LSB floor so the ball can actually slow at sub-stopSpeed velocities
```

**Do NOT touch any other row.** Specifically:
- Fairway 0.18 — stays.
- Green 0.12 — stays (Green is read by `surfaceCfg` only when ball is *not* on a putt-eligible surface; non-putt fall-through path is rare; tuning this row is Phase B's call).
- GreenCollar 0.15 — stays (same reasoning as Green row).
- Semirough 0.28 — stays.
- Rough 0.45 — stays.
- Tee 0.15 — stays.
- Sand 0.70 — stays.
- BunkerLip 0.55 — stays.
- Water 1.00 — stays.
- OOB 0.50 — stays.

### Step 5 — Add the new EditMode test file

Create new file `Assets/Scripts/Physics/Tests/RollAndPuttTuningTests.cs` containing 5 tests. Use the patterns from `PuttTests.cs` (NUnit `[Test]`, fp construction, `SplitSurfaceProvider` inner class, `ConstantSurfaceProvider`).

The file MUST live in the `Golfin.Physics.Tests` assembly (drop it next to `PuttTests.cs` in `Assets/Scripts/Physics/Tests/`; the existing `Golfin.Physics.Tests.asmdef` picks it up automatically).

**Required structure:**

```csharp
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase A validation tests for `controls_c_fix`. Five tests:
    ///   1. Stimpmeter — Green k=0.50 produces real-golf Stimp 12 distance.
    ///   2. LongPutt   — 5 m/s putter on Green→Fairway transition stays under 45m total.
    ///   3. DriverFairwayRollOut — observation-only, logs roll-out distance for Cesar.
    ///   4. CartPathStop — driver landing on CartPath terminates as BallStopped (was max-bounces).
    ///   5. StopCheckCorrectness — both Roll and Putt phases terminate well under their step caps.
    ///
    /// All five load tuning from CSV (PhysicsConfigLoader.LoadSurfaceConfig/LoadPuttConfig),
    /// so they pick up the Step 3 + Step 4 CSV edits. Tests using SurfaceConfig.Default /
    /// PuttConfig.Default are unaffected (existing 198 tests stay bit-exact).
    /// </summary>
    public class RollAndPuttTuningTests
    {
        // ... (see per-test specs below)
    }
}
```

#### Test 1: `Stimpmeter_Green_RollsTo3to4Meters`

Load `PuttConfig` from CSV. Create a `ConstantSurfaceProvider(SurfaceType.Green)` and `FlatGround(fp.Zero)`. Construct a `ShotInput` with `velocity = (1.83 m/s, 0, 0)` (the Stimpmeter standard release speed). Run `BallSimulation.Simulate` through the Phase 6 entry with the loaded `puttCfg`.

Assert:
- `traj.termination == TerminationReason.BallStopped`.
- `traj.finalPosition.x.ToFloat()` in `[3.0f, 4.5f]` (Stimp 10–12 band).
  - **Theoretical asymptote:** `d_max = v₀/k = 1.83/0.50 = 3.66 m` (the USGA Stimp 12 target).
  - **Realistic target:** ~**3.58 m**, because the integrator stops at `|v| < stopSpeed=0.04` rather than at v=0. Subtracted-tail = `(v_stop)/k ≈ 0.04/0.50 = 0.08 m`, so the ball arrests ~0.08 m short of the asymptote. The `[3.0, 4.5]` band tolerates fp-integration drift around this 3.58 m target.

If the test fails because the value is *outside* the band on either side, that's a meaningful diagnostic — surface in `IMPLEMENTER_REPORT.md` "Open questions for Architect" with the observed value.

#### Test 2: `LongPutt_GreenToFairwayTransition_TotalRollUnder45m`

Use the `SplitSurfaceProvider` pattern from `PuttTests.cs`: Green for `x < 5`, Fairway for `x >= 5`. Load both `surfaceCfg` and `puttCfg` from CSV. Construct a `ShotInput` with `velocity = (5.0 m/s, 0, 0)` (the canonical putter base velocity, full power).

Run the sim. Assert:
- `traj.termination == TerminationReason.BallStopped`.
- `traj.finalPosition.x.ToFloat()` in `[8.0f, 45.0f]` (loose band — real golf full-power putt is ~40 m total; this catches the C.2 "rolls forever" regression catastrophe-fast).

Log the observed distance via `TestContext.WriteLine` so Cesar can read it from the test output.

#### Test 3: `DriverFairwayRollOut_ObservationOnly_TerminatesAndLogs`

This is the **observation-only** test for Phase A. Load `surfaceCfg` from CSV. Use `ConstantSurfaceProvider(SurfaceType.Fairway)` and `FlatGround(fp.Zero)`. Construct a driver-class `ShotInput` (use the `IronInput()` helper from `PuttTests.cs` as a starting template, but with driver-class numbers: **64 m/s**, 11° launch, 2700 rpm backspin).

**On the 64 m/s figure:** `Clubs.csv` defines driver `ballSpeedMps=75.0`, but the captured C diagnosis Shot 2 observed `|v|=64.000 m/s` at `[ShotEntry]` after the unexplained C.5 cap (Build said 93.77 m/s; ShotEntry showed 64.000 m/s exactly). For this test to give numbers comparable to the captured Shot 2 evidence (~296 m total travel pre-fix), use 64 m/s — the post-cap value the sim actually receives today. Once C.5 is diagnosed and fixed in its own spec, this test can be re-tuned to match the new resolved velocity.

Run the sim. Assert:
- `traj.termination == TerminationReason.BallStopped` (the simulation completes; if it fails this, the stop-check is still broken or the ball goes OOB/water).
- `traj.finalPosition` total horizontal distance from origin in `[100.0f, 400.0f]` (lower bound catches a stuck-bounce-loop or aero regression that nukes carry; upper bound catches catastrophic-roll-out — was 296 m before the fix, with most of the tail being broken roll-out).

Log the observed total distance and the post-first-bounce roll-out distance (i.e., distance from the first `IsStop=false` `TerrainHit` to the final position) via `TestContext.WriteLine`. Cesar uses these numbers to decide Phase B Fairway tuning.

If `traj.terrainHits.Count == 0`, log "no bounces (airborne all the way to OOB or world bound)" and let the assertion catch the abnormality via the termination check.

#### Test 4: `CartPathStop_DriverLanding_TerminatesAsBallStopped`

Load `surfaceCfg` from CSV. Use `ConstantSurfaceProvider(SurfaceType.CartPath)` and `FlatGround(fp.Zero)`. Same driver-class input as Test 3 (64 m/s, 11°, 2700 rpm).

Run the sim. Assert:
- `traj.termination == TerminationReason.BallStopped`.
- Sample count `< 60 * 240` (i.e., the sim terminates well under the 60-second roll-phase cap; with k=0.30 a 5 m/s post-bounce velocity decays to stop in roughly 5 s × 240 Hz = 1200 steps + airborne samples; <14400 is a comfortable catch-the-bug bound).

Log the observed total distance and step count via `TestContext.WriteLine`.

#### Test 5: `StopCheckCorrectness_BothPhasesTerminateWellUnderCap`

Two sub-assertions in one test method (both must pass):

(a) Putt sub-test: a 2.0 m/s putt on Green via `PuttConfig` from CSV terminates as `BallStopped` with `samples.Count < 10000` (well under the `60 * 240 = 14400` putt cap; at k=0.50, v=2 decays to stopSpeed=0.04 in ~`ln(2/0.04)/0.50 ≈ 7.83 s` = 1880 steps + 10 stop-required = ~1900 steps; 10000 cap leaves 5x margin).

(b) Roll sub-test: same driver setup as Test 3 on Fairway, terminates as `BallStopped` with `samples.Count < 10000` (well under the `60 * 240 = 14400` roll cap; airborne ~1500 steps + Fairway k=0.18 roll-out from ~6 m/s post-bounce decays to stopSpeed=0.10 in ~`ln(6/0.10)/0.18 ≈ 22.7 s` = 5460 steps; total ~7000 steps, the 10000 cap leaves headroom for slope-driven longer rolls without being so loose that a real bug hides).

Both numbers are deliberately loose; the point is to prove the stop-check fires at all (NOTES.md captured Shot 2 hitting `step=336` without ever incrementing `stopConsecutive` past 0). If either sub-test hits the cap, the stop-check fix is wrong.

Log both observed step counts via `TestContext.WriteLine`.

### Step 6 — Verify the bit-exact gate

Run the full EditMode test suite (`Window > General > Test Runner > Run All`).

Expected result: **203/203 PASS** (198 existing + 5 new).

If any of the 198 existing tests fail, **STOP** — the bit-exact gate is broken. Likely causes if this happens (in priority order):
1. The stop-check fix changed behaviour for an existing test that used `SurfaceConfig.Default` / `PuttConfig.Default`. Check which test failed; if the failure is in `BakedPivotRegressionTests` or `RealHoleTerrainTests`, it's likely that the *old* broken stop-check was masking some other issue (in which case, surface in the report; do NOT roll back the fix).
2. A typo in the fp expression introduced a different value than intended. Diff against the spec snippets above.
3. The CSV edit accidentally touched a different surface (re-verify Step 3 / Step 4 against the spec).

If a new test (1–5) fails, that's the intended diagnostic signal — surface the failure with the observed value in `IMPLEMENTER_REPORT.md` "Open questions for Architect" so the architect can decide whether to tune further or accept the value.

### Step 7 — Capture validation evidence in lab

After tests pass and code compiles cleanly:

1. Open `Assets/Scenes/LabScaffold.unity`.
2. Use the Hole Picker (`GOLFIN > Physics Lab > Hole Picker`) to load Hole 1.
3. Enter Play mode. Wait 5 seconds for hole load + populator wiring.
4. **Re-run the two diagnosis shots** (same setup as `controls_c_diagnosis` Step 8):
   - **Shot 1 (putter on green, 50% power):** place ball on Green 1, cycle to Putter, drag handle to ~50% power, flick. **Expected:** ball comes to rest visibly within ~5 seconds; final position no more than ~5–6 m from origin (Stimpmeter math at k=0.50 putt: 2.5 m/s release × 1/0.50 = 5 m).
   - **Shot 2 (driver on tee, 100% power):** reset to tee, cycle to Driver, drag handle to 100%, flick. **Expected:** ball lands, rolls out, and terminates as `BallStopped` (not `MaxBounces`) within a reasonable distance (the `[ShotExit]` log should show `termination=BallStopped`, not `MaxBounces`).
5. Capture the relevant `[ShotExit]` log lines + the final-position visual into `IMPLEMENTER_REPORT.md` § "Lab validation". Include a screenshot for each shot showing the trajectory + final ball-rest position.

The `controls_c_diagnosis` loggers (`DiagShotLogger`, `DiagRollLogger`, `DiagBuildLogger`, `LogResolution`) are still wired in `PhysicsLabController.Start()` — no setup needed for capture. **Do NOT remove or modify those loggers** in this task.

### Step 8 — Verify the `[ShotEntry]` / `[ShotExit]` invariants from the diagnosis pipeline lesson

Pipeline lesson from the diagnosis: "*`[ShotExit]` absence is itself diagnostic evidence — capture missing termination tag = sim never terminated.*" Verify that BOTH lab shots produce a `[ShotExit]` line (i.e., the stop-check fired). If either shot is missing `[ShotExit]`, the fix did NOT close C.2 — surface in `IMPLEMENTER_REPORT.md` "Open questions for Architect" with the captured logs.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] `RunRollPhase` stop-check (lines 537–552) modified per Step 1: tolerance window `+ stopEpsilon` added to clause 2; comment present (must mention fp-rounding mechanism, NOT slope re-acceleration — that narrative is wrong); `stopEpsilon = stopThresh * fp.FromFloat(0.05f)`
- [ ] `RunPuttPhase` stop-check (lines 670–682) modified per Step 2: same tolerance-window fix, single-line `else` style preserved
- [ ] `Assets/Resources/Physics/putt.csv` Green row updated: `Green,0.50,0.04,...`
- [ ] `Assets/Resources/Physics/putt.csv` GreenCollar row updated: `GreenCollar,0.40,0.05,...`
- [ ] `Assets/Resources/Physics/surfaces.csv` CartPath row updated: `CartPath,0.70,0.18,0.30,0.08,...`
- [ ] No other row in `surfaces.csv` modified (Fairway, Green, GreenCollar, Semirough, Rough, Tee, Sand, BunkerLip, Water, OOB all unchanged)
- [ ] No other file in `Assets/Resources/Physics/` modified (aero.csv, wind.csv, stats.csv, stat_caps.csv, etc.)
- [ ] `PuttConfig.Default` (in `Assets/Scripts/Physics/Core/PuttConfig.cs`) UNCHANGED
- [ ] `SurfaceConfig.Default` (wherever defined) UNCHANGED
- [ ] New file `Assets/Scripts/Physics/Tests/RollAndPuttTuningTests.cs` created with all 5 tests as specified
- [ ] Test 1 `Stimpmeter_Green_RollsTo3to4Meters` PASSES with observed value in `[3.0, 4.5]` band — log the actual observed value
- [ ] Test 2 `LongPutt_GreenToFairwayTransition_TotalRollUnder45m` PASSES with observed value in `[8.0, 45.0]` band — log actual value
- [ ] Test 3 `DriverFairwayRollOut_ObservationOnly_TerminatesAndLogs` PASSES with `BallStopped` termination — log total distance + post-first-bounce roll-out distance
- [ ] Test 4 `CartPathStop_DriverLanding_TerminatesAsBallStopped` PASSES with `BallStopped` termination + `samples.Count < 14400` — log total distance + step count
- [ ] Test 5 `StopCheckCorrectness_BothPhasesTerminateWellUnderCap` PASSES with both sub-assertions green (`samples.Count < 10000` for both) — log both observed step counts
- [ ] EditMode Test Runner reports **203/203 PASS** (full suite, not subset). If any existing test fails, STOP and surface the failure
- [ ] No new compiler warnings in Unity Console attributable to this task
- [ ] No `*.asmdef`, `*.unity`, `*.prefab`, or test file other than `RollAndPuttTuningTests.cs` modified
- [ ] Lab validation Shot 1 (putter, ~50% power) completed; ball comes to rest within ~5 s; `[ShotExit]` log captured with `termination=BallStopped`; screenshot in `screenshots/`
- [ ] Lab validation Shot 2 (driver, 100% power) completed; ball comes to rest; `[ShotExit]` log captured with `termination=BallStopped` (NOT `MaxBounces` — that's the C.2 regression signature); screenshot in `screenshots/`
- [ ] Diagnosis loggers (`DiagShotLogger`, `DiagRollLogger`, `DiagBuildLogger`, `LogResolution` wire in `PhysicsLabController.Start()`) still present and functional
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Scripts/Physics/Core/BallSimulation.cs` — modify two stop-check blocks (lines 537–552 and 670–682) per Step 1 + Step 2. **No other change.**
- `Assets/Resources/Physics/putt.csv` — replace Green and GreenCollar rows per Step 3.
- `Assets/Resources/Physics/surfaces.csv` — replace CartPath row per Step 4.
- `Assets/Scripts/Physics/Tests/RollAndPuttTuningTests.cs` — **NEW FILE**, 5 tests per Step 5.
- `Docs/Specs/Active/controls_c_fix/IMPLEMENTER_REPORT.md` — implementer writes this from the report template.
- `Docs/Specs/Active/controls_c_fix/screenshots/` — two screenshots from Step 7.
- `Docs/Specs/Active/controls_c_fix/STATUS.md` — implementer updates state machine per pipeline conventions.

## Out of scope (do NOT do these)

- **Do NOT tune any surface other than Green / GreenCollar / CartPath.** Fairway, Rough, Semirough, Sand, BunkerLip, Tee, Water, OOB are explicitly Phase B. If the validation tests reveal one of them is too slow or too fast, surface in "Open questions for Architect"; do NOT edit the CSV.
- **Do NOT modify `PuttConfig.Default` or `SurfaceConfig.Default` constants** in C#. The CSVs override Default at runtime; existing tests that use Default rely on the bit-exact gate. Touching Default breaks 198 tests.
- **Do NOT modify `BallSimulation.cs` outside the two stop-check blocks.** Specifically: do not refactor `RunRollPhase` or `RunPuttPhase`, do not change motion-update math, do not touch `IsPutt` gate, do not touch the airborne RK4 integrator, do not touch the bounce-loop `cr` computation. The fix is a 2-line addition + 1-line comparator change in each of the two blocks.
- **Do NOT remove or modify the diagnosis loggers** (`DiagShotLogger`, `DiagRollLogger`, `DiagBuildLogger`, `LogResolution`). They were added by `controls_c_diagnosis` and are useful for verifying this fix in lab.
- **Do NOT change `RollLogStrideSteps`** (default 24). Lab capture in Step 7 uses it as-is.
- **Do NOT add new tests beyond the 5 specified.** "While-I'm-here" extra tests dilute the signal-to-noise of the 198→203 jump and make Phase B harder to scope.
- **Do NOT touch `aero.csv`, `wind.csv`, `stats.csv`, `stat_caps.csv`, `Data/Clubs.csv`, or any other Resources file.**
- **Do NOT touch the C.5 velocity-cap mystery.** That's a separate Notion entry (`35631e0e-9a36-8133-9734-d5b4418db9f6`) with its own diagnostic micro-spec to come.
- **Do NOT touch C.3 / C.4 picker rules.** Surface-aware club-picker is separate work; the surface read it depends on is settled by this fix, but the picker itself is not in scope.
- **Do NOT change** `Docs/Roadmap.md`, `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`, or any other docs in `Docs/`. Architect updates those after architect-review.

## Pipeline lessons applied

From `Docs/Diagnostics/PIPELINE_LESSONS.md` and prior task lessons:

- **Lesson F (architect overthinks past Cesar's diagnosis):** The fix scope is exactly what NOTES.md locked with Cesar (3 CSV values, 1 stop-check fix, 5 tests). Phase A intentionally does NOT tune Fairway/Rough/Sand/etc — that's Phase B's call after we have observed numbers.
- **Lesson G (no thinking-aloud in specs):** Scanned, none present.
- **Lesson H (architect must verify visual claims):** N/A (no Figma).
- **`[ShotExit]` absence is itself diagnostic evidence:** Step 8 makes this explicit — missing `[ShotExit]` in either lab shot = fix didn't close C.2 = report it, don't paper over.
- **The stop-check has TWO clauses, not one:** Captured in NOTES.md. This spec's Step 1 + Step 2 reasoning makes the clause numbering explicit so the implementer can't accidentally fix the wrong one.
- **`screenshot-game-view` MCP returned null on three retries; `CaptureHelper.SnapGameViewWithLabel` worked fine:** For Step 7 lab screenshots, default to `CaptureHelper`. Project-mandated path; `screenshot-game-view` is banned project-wide per session memory.
- **Backfilled checks must never be presented as prior verification.** If the implementer realises mid-task that a step was skipped, surface it explicitly with "missed initially, ran on retry"; do not retroactively reorder events in the report.

## Mid-task escalation paths

- **If the existing 198-test bit-exact gate breaks** (any test in the existing 198 fails after the change): STATUS → `READY_FOR_ARCHITECT_REVIEW`, list which tests failed and which step (1, 2, 3, or 4) was last edited. Do NOT keep iterating; the bit-exact gate is structural and any failure means I (architect) need to look at it.
- **If a new test (1–5) fails outside the assertion band:** That's the intended diagnostic signal. Capture the observed value in `IMPLEMENTER_REPORT.md` "Open questions for Architect"; do NOT loosen the band to make the test pass; do NOT tune the CSV further than spec'd to chase the band. Phase B exists for this exact reason.
- **If lab Shot 1 (putter) doesn't visibly come to rest within ~10 s:** STATUS → `IMPLEMENTER_BLOCKED`. Capture the full `[ShotEntry]` / `[PuttStep]` / `[ShotExit]` log sequence + the surface-classification at the resting position. Either the stop-check fix has a typo, or the ball is bouncing between Green and GreenCollar zones in a way that resets `stopConsecutive` at every transition (which would be a NEW bug surfaced by this fix).
- **If lab Shot 2 (driver) terminates with `MaxBounces` instead of `BallStopped`:** That's the C.2 regression signature returning. STATUS → `IMPLEMENTER_BLOCKED` with the full log capture. Do NOT bump `MaxBounces` or alter the loop cap — fix is wrong, not the cap.
- **If Unity refuses to compile due to a fp construction error in the new test file:** Mirror the construction patterns from `PuttTests.cs` exactly. `fp.FromFloat(1.83f)`, `fp.Zero`, `new fp3(x, y, z)` — these are the canonical patterns. Do NOT introduce `fp.FromDouble` unless it's already used in PuttTests for the same numeric magnitude.
