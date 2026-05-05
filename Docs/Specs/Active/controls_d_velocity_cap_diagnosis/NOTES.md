# NOTES — `controls_d_velocity_cap_diagnosis` — Architect working notes

> **DECISIONS LOCKED 2026-05-05.** SPEC.md written. Implementer reads SPEC.md, NOT this file. NOTES.md retained as diagnosis journal + adversarial-review record.

**Created:** 2026-05-05 JST
**Architect:** Claude (claude.ai)
**Notion:** [`35631e0e-9a36-8133-9734-d5b4418db9f6`](https://www.notion.so/35631e0e9a3681339734d5b4418db9f6) — `C.5 — Velocity cap diagnostic (64 m/s mystery)` — P2 Medium, Order 145, Queued.

## TL;DR

The "64 m/s velocity cap" surfaced by `controls_c_diagnosis` is **not a velocity cap**. It's a **convergence bug in `fpMath.Sqrt`** that round-trips the input to the initial-guess power-of-2 (which for golf-ball-speed dot products lands at 64). A second, separate bug in `fpMath.Cos`/`Sin` (Taylor 7-term, insufficient range reduction) is also visible in the same captures and explains the 10% mismatch between Build's `velMagnitude` and the `|finalVel|` of the constructed velocity vector.

The original task was scoped as "diagnostic instrumentation only, mirrors `controls_c_diagnosis`." Reading the source plus the captured logs makes that step unnecessary — the bug is identifiable from existing evidence. The honest next move is a **fix spec**, not another diagnostic spec. This NOTES doc is the diagnosis report; the SPEC.md it leads to is the fix.

## Empirical evidence (already captured)

From `Docs/Specs/Completed/controls_c_diagnosis/IMPLEMENTER_REPORT.md` (Shot 2, driver, full power):

```
[Build]      isPutt=False override=0.00m/s clubVel=75.00m/s -> baseVelMps=78.14 effectiveFlick=1.200 velMultiplier=1.000
             -> velMagnitude=93.77m/s loft=10.9deg aimYaw=-2.907rad
             finalVel=(-100.20, 17.73, -17.87)
[ShotEntry]  origin=(219.43, 11.46, 34.73)
             vel=(-100.195, 17.733, -17.873) |v|=64.000m/s
```

And Shot 1 (putter, ~50% power):

```
[Build]      finalVel=(-2.18, 0.18, -0.47) velMagnitude=2.05m/s
[ShotEntry]  vel=(-2.185, 0.179, -0.474) |v|=2.000m/s
```

Real-arithmetic checks on the logged vectors:
- Driver: `√(100.20² + 17.73² + 17.87²) = √10672 ≈ 103.3 m/s` — logged as `64.000`.
- Putter: `√(2.18² + 0.18² + 0.47²) = √5.005 ≈ 2.236 m/s` — logged as `2.000`.

The vector components in `[Build]` and `[ShotEntry]` are **identical** — nothing mutates the velocity between the two emit sites. The discrepancy is entirely inside `fpMath.Sqrt(fpMath.Dot(vel, vel))` at the `[ShotEntry]` log call.

## Source-code root-cause analysis

### Bug 1 — `fpMath.Sqrt` convergence early-exit

`Assets/Scripts/Physics/Math/fpMath.cs` lines 7–37:

```csharp
public static fp Sqrt(fp x)
{
    if (x.raw <= 0) return fp.Zero;
    long v = x.raw;
    long n = v << 16;
    if ((v >> 48) != 0) { /* double fallback */ }

    // Initial guess: bit-shift to ~2^(floor(log2(n)/2)+1).
    long r = 1L;
    long tmp = n;
    while (tmp > 3L) { tmp >>= 2; r <<= 1; }   // <-- seeds r as a power of 2

    long prev;
    for (int i = 0; i < 40 && r != 0; i++)
    {
        prev = r;
        r = (r + n / r) >> 1;
        if (r >= prev) { r = prev; break; }    // <-- BUG: early-exit fires on first step UP
    }
    return fp.FromRaw(r);
}
```

**The bug:** The convergence test `if (r >= prev) { r = prev; break; }` assumes Newton-Raphson converges monotonically downward. That's only true when the initial guess is **above** the true sqrt. The seeder loop produces a power-of-2 guess that systematically **undershoots** for non-power-of-4 inputs — so the first iteration steps **upward**, the test fires immediately, and the loop returns `prev` (the initial guess).

**Trace for input fp value 10672** (`x.raw = 10672 × 65536 = 6.99×10⁸`):
- `v = 6.99e8`, `n = v << 16 = 4.58×10¹³`.
- Halving loop: `tmp` starts at `4.58e13` (~2⁴⁵), shifts right by 2 each iteration, runs **22 times** until `tmp ≤ 3`. `r` doubles 22 times: `r = 2²² = 4,194,304`.
- Newton iteration 0: `prev = 4194304`, `r = (4194304 + 4.58e13 / 4194304) >> 1 = (4194304 + 10920000) >> 1 = 7,557,152`.
- `r >= prev` (`7,557,152 >= 4,194,304`) → **break with `r = prev = 4,194,304`**.
- Return `fp.FromRaw(4,194,304)` = `4,194,304 / 2¹⁶ = 2⁶ = 64.0`. ✅ matches captured log.

**Same trace for input fp value 5.005** (putter dot product):
- `v = 5.005 × 65536 ≈ 327,953`, `n = v << 16 ≈ 2.15×10¹⁰`.
- Halving loop: `tmp` halves down from `2³⁴`, runs 17 times. `r = 1 << 17 = 131,072`.
- Newton iter 0: `r = (131072 + 2.15e10 / 131072) >> 1 = (131072 + 164230) >> 1 = 147,651`.
- `r >= prev` → break with `r = prev = 131,072`.
- Return `131,072 / 65,536 = 2.0`. ✅ matches captured log.

**The cap value is fully determined by `2^(⌊log₂(input · 2¹⁶)/2⌋)`**: input ~5 → returns 2; input ~10000 → returns 64; input ~150000 → returns 256; etc. It's a step function that snaps `√x` down to the nearest power of 2 below it.

**Fix shape:** replace the buggy convergence test with one that handles both upward and downward steps. Standard pattern:

```csharp
// Run a fixed number of iterations, no early exit.
for (int i = 0; i < 24; i++)
{
    long next = (r + n / r) >> 1;
    if (next == r) break;       // exact fixed point
    r = next;
}
// One final downward pass to handle the off-by-one case where r oscillates
long rSq = (r * r) >> 16;
if (rSq > v) r--;
return fp.FromRaw(r);
```

Or use `r * r > n` test explicitly instead of the `r >= prev` heuristic.

### Bug 2 — `fpMath.Cos` / `Sin` Taylor accuracy at large angles

`fpMath.cs` lines 53–85: `Cos`/`Sin` use a 7-term Taylor expansion, then `ReduceAngle` reduces input to `[-π, π]`. Taylor at the midpoint of that range is fine, but **error grows toward the bounds**.

For the captured driver shot, `aimYaw = -2.907 rad ≈ -166.6°`. After `ReduceAngle` it stays at `-2.907` (within `[-π, π]`). 7-term Taylor of `cos(-2.907)`:
```
  a   = -2.907
  a²  =  8.451
  a⁴  = 71.42
  a⁶  = 603.6
  cos ≈ 1 - 8.451/2 + 71.42/24 - 603.6/720
      = 1 - 4.226 + 2.976 - 0.838
      = -1.088
```
Real `cos(-2.907) = -0.974`. Taylor returns `-1.088` — **12% over-magnitude**.

This explains the `velMagnitude (93.77)` vs `|finalVel| (≈103.3)` discrepancy in `[Build]`: the constructed velocity vector has component magnitudes inflated by the Taylor error, so `√(dot)` of those components overstates true `|velMag|` by ~10%.

**Fix shape:** Either (a) reduce angle further to `[-π/2, π/2]` using the identity `cos(π−x) = −cos(x)`, then Taylor stays accurate; (b) add more Taylor terms (a⁸, a¹⁰); (c) use a proper minimax polynomial. (a) is the cheapest.

## Blast radius (**this is the important section**)

`fpMath.Sqrt` is called from:
- `fpMath.Normalize` (every vector normalize in the integrator)
- `BallSimulation.cs` (6 sites — `|v|`, drag, stop-checks)
- `AeroModel.cs` (1 — aero drag)
- `HeightmapData.cs` (1 — surface normal)
- `PhysicsLabController.cs` (diagnostic-only, not load-bearing)

`fpMath.Sin/Cos` is called from:
- `ShotInputBuilder.Build` (every shot resolution)
- `WindModel` (every aero step)
- Editor tooling for trajectory preview

**Both fixes change the bit-output of every test that runs sim or builds a shot.** The **bit-exact 203/203 EditMode gate will break**. Surface coefficients, putt coefficients, aero LUTs, and the `RollAndPuttTuningTests` band targets that just landed in `controls_c_fix` are all calibrated against the broken `Sqrt`.

Qualitative effects of fixing:
- **Sqrt fix:** every `|v|` reading goes UP for fast shots (driver was reading 64 instead of 103). This means: aero drag is also higher (drag scales with v), rolling resistance is higher (`dv/dt = -k·v`, so larger `|v|` = larger absolute deceleration). Net effect on driver carry: probably DECREASES slightly because drag rises faster than launch speed. But every CSV target for clubs at high power will need re-validation.
- **Trig fix:** velocity components become accurate, so `|finalVel|` matches `velMagnitude` instead of being ~10% high. Net effect: shot velocities decrease slightly (the over-magnitude was hidden compensation). Carry distances drop slightly. Aim accuracy improves because `cos(yaw)² + sin(yaw)² = 1` will now hold to fp precision.

**These are not regressions — the OLD numbers were wrong by construction.** But the calibration spreadsheet `Docs/Physics/PHYSICS_TUNING_TARGETS.md` was written against those broken numbers, so the carry / putt distances people have been validating against need a re-baseline.

## Path options

### Path A — Honor the Notion plan: instrumentation-only spec

Write a SPEC.md that adds:
- `[VelCap]` log line in `BallSimulation.Simulate` showing `velIn`, `dotIn`, `sqrtOut`.
- `[SqrtIter]` log inside `fpMath.Sqrt` showing initial guess, each iteration's `r`, and termination reason.
- Maybe a few EditMode tests for `fpMath.Sqrt` at known inputs that document the bug as failing tests.

Cesar fires shots, captures logs, files them in IMPLEMENTER_REPORT, then a separate fix spec follows.

**PRO:** matches the original Notion description verbatim.
**CON:** redundant. The diagnosis is complete from existing logs + source code. Adding more logging won't tell us anything we don't already know.

### Path B — Skip diagnostic, single fix spec for both bugs

Write a SPEC.md that:
1. Repairs `fpMath.Sqrt` (proper convergence, no buggy early-exit).
2. Repairs `fpMath.Cos`/`Sin` (range reduction to `[-π/2, π/2]`).
3. Adds a new EditMode test file `fpMathTests.cs` covering Sqrt over `[1e-4, 1e6]` and Cos/Sin over `[-2π, 2π]`, with tolerance bands tight enough to lock the regression.
4. Re-baselines the existing 203 tests (most will simply pass on the same band tolerances; some bit-exact tests may need to be widened to band tests, OR re-snapshotted against the corrected outputs).
5. Re-baselines `Docs/Physics/PHYSICS_TUNING_TARGETS.md` carry/putt numbers.

**PRO:** fastest, single landing.
**CON:** large blast radius in one task. High risk of test churn that masks regressions. Hard to diff.

### Path B-narrow — Fix Sqrt only, defer Trig to a follow-up

Same as B but only Bug 1. Trig becomes a separately scoped Phase B spec.

**PRO:** smaller blast radius. The Sqrt bug is the dramatic one (caps `|v|` at 64); Trig is a quieter ~10% accuracy issue. Splitting lets us validate one change at a time.
**CON:** two re-baselines instead of one (after each fix lands, distances shift). Trig is essentially "free" to fix (one-line `ReduceAngle` change), so deferring it is mostly a sequencing call.

### Path C — Diagnostic spec = test suite, not log instrumentation

Write a SPEC.md that adds `fpMathTests.cs` with assertions like:
```csharp
Assert.AreEqual(103.3f, fpMath.Sqrt(fp.FromFloat(10672)).ToFloat(), 0.5f);   // FAILS (returns 64)
Assert.AreEqual(2.236f, fpMath.Sqrt(fp.FromFloat(5.005)).ToFloat(), 0.05f);  // FAILS (returns 2.0)
Assert.AreEqual(-0.974f, fpMath.Cos(fp.FromFloat(-2.907)).ToFloat(), 0.01f); // FAILS (returns ~-1.088)
```
The failing tests **become** the diagnostic evidence — IMPLEMENTER_REPORT contains the test runner output. No log-emission code needed. Then a fix spec follows that flips the assertions to PASS.

**PRO:** the diagnostic produces a permanent regression suite as a side effect. No throwaway log code. Mirrors the test-driven philosophy of `controls_c_fix`.
**CON:** still two specs, but the first one's deliverable is more durable than Path A's (tests outlive log captures).

## Architect recommendation

**Path B-narrow** for the fastest path to a fixed `|v|`, with Trig as a separate follow-up.

Reasoning:
- The Sqrt bug is the only one Cesar called out in the Notion entry. The Trig finding is a bonus from this analysis pass.
- Sqrt + bit-exact-rebaseline is already a meaty Tier 3 task. Adding Trig doubles the rebaseline surface for marginal benefit.
- The new `fpMathTests.cs` started here can grow to cover Trig in the Phase B follow-up.

If Cesar wants more diagnostic ceremony, **Path C** is the next-best (test-driven diagnosis) and converts cleanly into the fix spec.

Path A (more log instrumentation) I'd actively argue against.

## Open questions for Cesar

1. **Path choice:** A, B, B-narrow, or C? (Recommend B-narrow.)
2. **Bit-exact gate handling:** Once Sqrt is fixed, the existing 203/203 gate breaks. Three sub-options:
   - **(a)** Re-snapshot — update each affected test's expected output to the new (correct) value. Lots of files touched but each delta is tiny and reviewable.
   - **(b)** Widen to band tests — convert affected `Assert.AreEqual` calls to `Assert.That(value, Is.InRange(low, high))` with tolerances. Less precise but more resilient.
   - **(c)** Gate behind a feature flag (`fpMath.UseFixedSqrt = true`) and migrate tests batch-by-batch. Most conservative; longest timeline.
3. **Notion entry rename:** The current title `C.5 — Velocity cap diagnostic (64 m/s mystery)` is now misleading — there's no velocity cap. Rename to `C.5 — fpMath.Sqrt convergence repair` (or similar) for clarity? Folder name follow suit?
4. **Trig fix sequencing:** if Path B-narrow, when does the Trig follow-up land? After Loop v1, alongside Phase B controls fairway tuning, or further out?
5. **Tuning target re-baseline:** `Docs/Physics/PHYSICS_TUNING_TARGETS.md` has carry numbers calibrated against the broken Sqrt. After fix lands, do you want to re-validate against real-world golf reference data (Trackman, USGA), or just document the new numbers as-is and re-tune later if they feel wrong in lab testing?

## Proposed Phase A / Phase B split (assuming Path B-narrow)

**Phase A (this task, controls_d_velocity_cap_diagnosis or rename):** Fix `fpMath.Sqrt` convergence. New `fpMathTests.cs` with Sqrt assertions. Re-baseline existing 203 tests as needed. Update `PHYSICS_TUNING_TARGETS.md` carry numbers to reflect new (correct) physics.

**Phase B (separate, new Notion entry):** Fix `fpMath.Cos/Sin` range reduction. Extend `fpMathTests.cs` with Trig assertions. Re-baseline whatever shifts further.

## Files this task is likely to touch (Path B-narrow)

- `Assets/Scripts/Physics/Math/fpMath.cs` — Sqrt body only.
- `Assets/Scripts/Physics/Tests/fpMathTests.cs` — NEW.
- `Assets/Scripts/Physics/Tests/*.cs` — some subset of the 203 tests will need new expected values; touch only those that fail.
- `Docs/Physics/PHYSICS_TUNING_TARGETS.md` — update carry / putt numbers post-fix.

No asmdef, no scene, no prefab, no CSV.

## Out of scope (regardless of path)

- Trig fix (deferred to Phase B unless Cesar picks Path B-full).
- Adding new physics features.
- Re-tuning gameplay coefficients (CSVs) beyond what the test re-baseline forces.
- Touching `BallSimulation` integrator logic.
- Performance optimization of `fpMath.Sqrt` beyond what's needed to make it correct.

---

## ADVERSARIAL REVIEW (2026-05-05) — verification before SPEC.md lock

Cesar instruction: *"Follow recommendation. Double check everything with other people's implementations online and adversarial review. Be 100% sure of the path before fixing or over engineering."* This section is the verification log.

### Verification 1 — Bug analysis (independent derivation)

**Claim:** The early-exit `if (r >= prev) { r = prev; break; }` fires on iteration 0 because Newton-Raphson always steps UP from a power-of-2 undershoot.

**Proof:** For input N, Newton iteration is `r' = (r + N/r) / 2`. If `r < √N`, then `N/r > √N > r`, so `r' = (r + N/r)/2 > r`. Therefore `r' > prev` ALWAYS holds on iteration 0 when initial guess undershoots. The early-exit triggers, returning `r = prev` (the initial guess). ✅ Confirmed.

**Cap-value calculation:** Halving loop produces `r = 2^k` where `k` is the number of iterations until `tmp ≤ 3`. For input fp value 10672 (driver dot product), `n = 10672 × 2^16 × 2^16 = 4.58×10¹³`, `log₂(n) ≈ 45.4`, halving runs 22 times, `r = 2²² = 4,194,304` raw. fp value = `2²² / 2¹⁶ = 2⁶ = 64.0`. ✅ Matches captured log.

For input fp value 5.005 (putter), `r = 2¹⁷ = 131,072` raw → fp value `2.0`. ✅ Matches captured log.

### Verification 2 — Canonical algorithm comparison (libfixmath)

Fetched `mitsuhiko/libfixmath/libfixmath/fix16_sqrt.c` (the canonical Q16.16 sqrt implementation, MIT-licensed, ~14 years deployed). **Key finding: libfixmath does NOT use Newton-Raphson.** It uses the **digit-by-digit shift-and-subtract method** from Wikipedia's "Methods of computing square roots → Binary numeral system."

libfixmath's algorithm:
1. Pick a starting bit (highest power-of-2 ≤ sqrt of input).
2. For each bit from high to low: test if `result + bit` would keep partial sum ≤ num. If yes, include it.
3. Run main loop twice (two-pass) to avoid 64-bit intermediate values, since libfixmath targets embedded platforms with only 32-bit math.
4. Optional final rounding bit.

This is **deterministic by construction** — no convergence test, no iteration count tuning, no early-exit. Bit count is fixed: 32 bits in, 16 bits out (for Q16.16 → Q16.16 sqrt).

GolfinRedux's `fp.raw` is `long` (verified at `Assets/Scripts/Physics/Math/fp.cs` line 14), so the two-pass split is unnecessary — a **single-pass int64 version** is cleaner and equally correct.

### Verification 3 — Other algorithms surveyed

- **Hacker's Delight (Warren)** — covers integer sqrt with explicit Newton + canonical seeder. Uses `nlz` (number of leading zeros) for the seeder, which is a more principled bit-counting approach than the halving-loop. Still requires a careful convergence test.
- **Wikipedia "Methods of computing square roots"** — documents both Newton (Heron's method) and digit-by-digit. Notes that Newton needs care at integer boundaries (oscillation between `k` and `k+1`).
- **Bombelli's algorithm (arxiv 2406.07751, 2024)** — a faster digit-by-digit variant for multi-precision; out of scope here.
- **Goldschmidt** — multiplicative iteration for FPGA / FPU; not applicable to pure-integer code.

Conclusion: digit-by-digit and Newton are the two canonical options. **Digit-by-digit is more structurally robust** for the GolfinRedux use case (avoids convergence-test bugs entirely).

### Verification 4 — Determinism of `System.Math.Sqrt` on iOS/Android (rejected alternative)

IronWarrior/UnityCrossPlatformDeterministicFloats GitHub repo tests basic float ops across IL2CPP on Windows/Mac/Android/iOS/WebGL. Their finding: "the only non-deterministic results reported were in trig functions contained in `System.Math`." `System.Math.Sqrt` was NOT flagged as non-deterministic.

**However:** GolfinRedux's physics architecture is deliberately integer-only Q16.16 to dodge float-determinism risk on edge platforms (older ARM, denormals, NaN handling). Adding a `System.Math.Sqrt` dependency in core sim path would expand the existing fallback's float-dependency surface. **Rejected** to preserve the project's architectural contract.

(The existing `(v >> 48) != 0` fallback to `System.Math.Sqrt` for very-large-input safety also gets removed in the SPEC — the new digit-by-digit handles inputs up to 2⁶² safely without needing a fallback.)

### Verification 5 — Stale comment in existing code

The existing `fpMath.Sqrt` has a comment:
```csharp
// Starting from r=n requires ~22 halvings to reach sqrt for typical
// golf-ball speeds, but the loop only runs 20 — causing severe under-convergence.
```
But the loop actually runs **40** iterations (`for (int i = 0; i < 40 && r != 0; i++)`). Evidence: someone previously identified the bug AS "iteration count too low" and bumped it from 20 to 40, but the bug is structural (early-exit fires on iteration 0). The fix didn't work; the comment is stale; the actual root cause was missed. **Worth flagging in SPEC** so the next reader doesn't repeat the same misdiagnosis.

### Final algorithm decision

**Port libfixmath's `fix16_sqrt` digit-by-digit shift-and-subtract method, single-pass int64 version**, replacing the entire body of `fpMath.Sqrt`. Specifically:

```csharp
public static fp Sqrt(fp x)
{
    if (x.raw <= 0) return fp.Zero;

    // We want sqrt(x) where x is Q16.16. Result.raw² / 2¹⁶ ≈ x.raw,
    // so result.raw ≈ sqrt(x.raw × 2¹⁶). Compute integer sqrt of (x.raw << 16).
    long n = x.raw << 16;
    long result = 0L;

    // Find highest power-of-4 ≤ n. Start at 2⁶° (largest even-position bit in long).
    long bit = 1L << 60;
    while (bit > n) bit >>= 2;

    // Digit-by-digit shift-and-subtract (Wikipedia / libfixmath).
    while (bit != 0L)
    {
        if (n >= result + bit)
        {
            n      -= result + bit;
            result = (result >> 1) + bit;
        }
        else
        {
            result >>= 1;
        }
        bit >>= 2;
    }

    // Rounding: if true sqrt is closer to result+1 than to result, round up.
    if (n > result) result++;

    return fp.FromRaw(result);
}
```

Properties:
- **Deterministic by construction** (no convergence test).
- **Pure integer** (preserves Q16.16 architecture).
- **Correctness:** for any input `x.raw ∈ [0, 2⁴⁶]` (i.e., fp values up to ~2³⁰ ≈ 1e9), result is the integer-rounded `√x` to fp precision.
- **Bit-exact:** returns the same value on every IL2CPP/Mono platform with int64 support.
- **Performance:** ~32 iterations of simple integer ops; bounded by bit-width of `n`. Equal or faster than the current broken Newton on modern HW.

No `System.Math.Sqrt` dependency. No fallback path. No stale comments.

### Final Path B-narrow lock-in

| Decision | Locked value |
|---|---|
| Path | **B-narrow** (Sqrt only; Trig deferred to Phase B) |
| Algorithm | **libfixmath digit-by-digit port, single-pass int64** |
| Bit-exact gate | **(a) Re-snapshot tests** as needed |
| Notion rename | **Yes** — "C.5 — fpMath.Sqrt convergence repair" |
| Trig follow-up | **After Loop v1** (separate Notion entry, P2) |
| Tuning targets | Document new numbers in `PHYSICS_TUNING_TARGETS.md`; re-validate against Trackman/USGA when convenient (not blocking) |

SPEC.md written from these locks. Folder moved to `Active/`. STATUS=SPEC_READY.
