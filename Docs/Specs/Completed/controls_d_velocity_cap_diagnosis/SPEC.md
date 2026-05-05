# SPEC — `controls_d_velocity_cap_diagnosis` — Repair `fpMath.Sqrt` convergence (Phase A)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files. Architect's diagnosis journal + adversarial review record at `NOTES.md` (informational, not load-bearing).

**Created:** 2026-05-05 JST
**Architect:** Claude (claude.ai)
**Roadmap:** `Docs/Roadmap.md` §1 (Putter P1) — closing follow-up. Does NOT gate §2 (Loop v1) start; Loop v1 can begin without this landing first.
**Notion:** [`35631e0e-9a36-8133-9734-d5b4418db9f6`](https://www.notion.so/35631e0e9a3681339734d5b4418db9f6) — title to be renamed to `C.5 — fpMath.Sqrt convergence repair (Phase A)` — P2 Medium → flipping to **In Progress** when this spec lands in Active/.

## Status

See `STATUS.md` for current pipeline state.

## Goal

Replace the buggy body of `fpMath.Sqrt` with a deterministic digit-by-digit shift-and-subtract algorithm (ported from libfixmath's `fix16_sqrt`), then re-snapshot the EditMode test gate from 203 against the corrected sqrt outputs.

The current `fpMath.Sqrt` returns the wrong value for almost every non-trivial input — it returns the **initial guess** (a power of 2 below the true sqrt) instead of the actual square root. This caps `|v|` at 64 m/s for driver-class shots, 2 m/s for putter-class shots, and similar power-of-2 quantization elsewhere. Every `√(dot(v,v))` reading in the physics integrator is wrong; aero drag, rolling resistance, and stop-checks have all been operating on quantized speeds.

This spec fixes the algorithm. **The fix WILL change physics output** — bit-exactly different from current behavior. The 203/203 EditMode gate will break on most tests that exercise sim or shot-resolution. Re-snapshotting affected tests is part of this task.

**Out of scope (Phase B):** `fpMath.Cos` / `fpMath.Sin` Taylor accuracy at angles near ±π. Separate ~10% bug; deferred to a follow-up Notion entry to land after Loop v1.

## Why this fix shape (architect lock from `NOTES.md`, adversarial review)

Three options were considered. Decision matrix:

| Option | Code | Robustness | Determinism contract | Risk |
|---|---|---|---|---|
| Fix existing Newton convergence test | 3-line diff | Fragile (off-by-one, integer-boundary oscillation) | Pure integer ✓ | Easy to re-introduce subtle Newton bugs |
| Replace with `System.Math.Sqrt` | 5 lines | Most robust on modern HW | **Breaks** project's "integer-only Q16.16" contract | None practical, theoretical edge-platform concerns |
| **Port libfixmath digit-by-digit (CHOSEN)** | ~25 lines | Structurally robust, no convergence test | Pure integer ✓ | Low (canonical, well-tested algorithm) |

Locked: **port libfixmath**, single-pass int64 version. Reasoning summary:
- libfixmath's `fix16_sqrt` is the canonical Q16.16 sqrt — MIT-licensed, ~14 years deployed, ported into many production embedded systems.
- The algorithm is **deterministic by construction** — no convergence test, no iteration count tuning, no early-exit. It processes a fixed number of bit positions.
- Preserves the project's deliberate "integer-only Q16.16 to dodge cross-platform float non-determinism" choice (existing comment in `Docs/Physics/PHYSICS_RESEARCH.md`-ish + the existing fp library structure).
- libfixmath uses two passes only because pure C on embedded targets often lacks 64-bit ops; GolfinRedux's `fp.raw` is `long` (verified at `Assets/Scripts/Physics/Math/fp.cs:14`), so a single-pass int64 version is cleaner.

Full adversarial review and source-code analysis are in `NOTES.md` § "ADVERSARIAL REVIEW (2026-05-05)". The implementer does NOT need to re-do that analysis — it's a record for future readers.

## Reference

- **libfixmath canonical:** `https://github.com/PetteriAimonen/libfixmath/blob/master/libfixmath/fix16_sqrt.c` (or any of its mirrors). C source, MIT-licensed.
- **Wikipedia:** `https://en.wikipedia.org/wiki/Methods_of_computing_square_roots` — the "Binary numeral system" section describes the algorithm.
- **Existing failed-fix evidence:** `Assets/Scripts/Physics/Math/fpMath.cs` line 22 has a stale comment about "loop only runs 20 — causing severe under-convergence." The loop actually runs 40 iterations, evidence that someone previously misdiagnosed the bug as iteration-count-too-low and bumped it without effect. **Do not chase the iteration count — the bug is structural in the early-exit clause.**
- **Empirical evidence file:** `Docs/Specs/Completed/controls_c_diagnosis/IMPLEMENTER_REPORT.md` § "Diagnostic capture" — has the exact `[Build]` and `[ShotEntry]` log lines that prove the bug for both putter and driver shots.

## Architecture context

**Asmdef boundaries affected:** none. All edits are to existing files in existing assemblies. No new files (besides this spec folder + `fpMathTests.cs`), no asmdef edits.

**Existing code referenced (Implementer reads these end-to-end before starting):**
- `Assets/Scripts/Physics/Math/fpMath.cs` — `Sqrt` body (lines 7–37). The body to be replaced.
- `Assets/Scripts/Physics/Math/fp.cs` — fp struct definition. Confirm `fp.raw` is `long`, `fp.FromRaw(long)` exists, `fp.FromFloat(float)` and `fp.ToFloat()` exist. (All confirmed at architect-time.)
- `Assets/Scripts/Physics/Tests/*.cs` — 13 EditMode test files (203 tests total). The re-snapshot pass touches a subset.
- `Docs/Physics/PHYSICS_TUNING_TARGETS.md` — the calibration spreadsheet. Carry/putt numbers are calibrated against the broken sqrt; needs documentation update post-fix.

**No edits to:**
- Any `.unity`, `.prefab`, or scene file.
- Any `.asmdef`.
- Any `*.csv` (no tuning changes in this task).
- Any code outside `fpMath.Sqrt`, `fpMathTests.cs`, and the affected test files.
- `BallSimulation` integrator logic (only its tests, when their expected values shift).

## Implementation

### Step 0 — Read and confirm the existing situation

Open `Assets/Scripts/Physics/Math/fpMath.cs`. Confirm the current `Sqrt` body matches what's described in `NOTES.md`:
- Babylonian/Newton iteration with bit-shift seeder.
- 40-iteration loop with early-exit `if (r >= prev) { r = prev; break; }`.
- Stale comment about under-convergence.
- `System.Math.Sqrt` fallback for `(v >> 48) != 0`.

Open `Assets/Scripts/Physics/Math/fp.cs`. Confirm:
- `fp.raw` is declared as `public readonly long raw;`.
- `fp.FromRaw(long r)` exists.
- `fp.Zero` exists.

### Step 1 — Replace the body of `fpMath.Sqrt`

Replace the **entire body** of `public static fp Sqrt(fp x)` with the implementation below. Keep the method signature exactly as-is (same name, same access modifier, same parameter type and name, same return type, same namespace).

```csharp
// Digit-by-digit shift-and-subtract square root (Wikipedia "Methods of computing
// square roots → Binary numeral system"; ported from libfixmath fix16_sqrt.c, MIT).
//
// Single-pass int64 version (libfixmath uses two int32 passes for embedded
// targets; GolfinRedux's fp.raw is long so we don't need that split).
//
// Deterministic by construction: no convergence test, no iteration count, no
// early-exit. Processes a fixed sequence of bit positions, producing the
// integer-rounded sqrt to fp precision.
//
// HISTORY: previous Newton-Raphson implementation had a convergence-test bug
// that returned the initial-guess power of 2 (typically 64.0 for driver-class
// inputs, 2.0 for putter-class inputs). The "loop only runs 20" comment was
// misdiagnosis; bug was structural in `if (r >= prev) break`. Do NOT revert.
public static fp Sqrt(fp x)
{
    if (x.raw <= 0) return fp.Zero;

    // We want sqrt(x) where x is Q16.16. Computing integer sqrt of (x.raw << 16)
    // gives result.raw such that result.raw² / 2¹⁶ ≈ x.raw, i.e., result is the
    // Q16.16 representation of √(x as fp).
    long n      = x.raw << 16;
    long result = 0L;

    // Find highest power-of-4 ≤ n. Start at 2⁶⁰ (largest even-position bit
    // that fits in signed long) and halve by 4 until ≤ n.
    long bit = 1L << 60;
    while (bit > n) bit >>= 2;

    // Digit-by-digit loop: for each bit position from high to low, test whether
    // including this bit keeps result² ≤ n. Process pairs of binary digits
    // (one output bit per two input bits, hence bit >>= 2).
    while (bit != 0L)
    {
        if (n >= result + bit)
        {
            n      -= result + bit;
            result  = (result >> 1) + bit;
        }
        else
        {
            result >>= 1;
        }
        bit >>= 2;
    }

    // Rounding: if true sqrt is closer to result+1 than to result, round up.
    // (This is the "remainder > divisor" test from long-division sqrt.)
    if (n > result) result++;

    return fp.FromRaw(result);
}
```

**Do NOT** add helper methods, change the signature, change the namespace, or touch any other code in `fpMath.cs`. Only the `Sqrt` body changes. Other methods (`Sin`, `Cos`, `ReduceAngle`, `Dot`, `Cross`, `Normalize`, `Clamp`, `Min`, `Max`) are out of scope for this task.

**Do NOT** remove the `using` directive at the top of the file. **Do NOT** alter `using Golfin.Physics.Math;` references elsewhere in the project. The signature and behavior contract stays the same — input `fp x`, output `fp` representing `√x`.

### Step 2 — Create `fpMathTests.cs`

Create a new file `Assets/Scripts/Physics/Tests/fpMathTests.cs`. Mirror the structure of existing test files (e.g., `ProjectileMathTests.cs`). The file MUST:

- Be in namespace `Golfin.Physics.Tests`.
- Use `NUnit.Framework`.
- Use `Golfin.Physics.Math` for `fp` and `fpMath`.
- Have a single test class `fpMathTests`.

Add the following test methods (each one a `[Test]`):

#### Test 1 — `Sqrt_KnownValues_MatchesRealArithmetic`

```csharp
[Test]
public void Sqrt_KnownValues_MatchesRealArithmetic()
{
    // Tolerance: 1 LSB of Q16.16 = 1/65536 ≈ 1.5e-5. Use 0.001 to allow for
    // integer-rounding off-by-one without making the test brittle.
    const float tol = 0.001f;

    var cases = new[]
    {
        // (input, expected √input)
        (0.0f,        0.0f),
        (1.0f,        1.0f),
        (4.0f,        2.0f),
        (5.005f,      2.2371f),     // putter dot-product from controls_c_diagnosis
        (16.0f,       4.0f),
        (100.0f,      10.0f),
        (10672.0f,    103.305f),    // driver dot-product from controls_c_diagnosis
        (32768.0f,    181.019f),    // upper Q16.16 exposed range
    };

    foreach (var (input, expected) in cases)
    {
        fp actual = fpMath.Sqrt(fp.FromFloat(input));
        Assert.AreEqual(expected, actual.ToFloat(), tol,
            $"Sqrt({input}) = {actual.ToFloat()}, expected {expected}");
    }
}
```

#### Test 2 — `Sqrt_ZeroAndNegative_ReturnsZero`

```csharp
[Test]
public void Sqrt_ZeroAndNegative_ReturnsZero()
{
    Assert.AreEqual(0.0f, fpMath.Sqrt(fp.Zero).ToFloat(), 0f);
    Assert.AreEqual(0.0f, fpMath.Sqrt(fp.FromFloat(-1.0f)).ToFloat(), 0f);
    Assert.AreEqual(0.0f, fpMath.Sqrt(fp.FromFloat(-100.0f)).ToFloat(), 0f);
}
```

#### Test 3 — `Sqrt_PerfectSquares_ExactToFpPrecision`

```csharp
[Test]
public void Sqrt_PerfectSquares_ExactToFpPrecision()
{
    // For perfect squares of integers, sqrt should be exact in fp arithmetic.
    for (int i = 0; i <= 50; i++)
    {
        float sq = (float)(i * i);
        fp actual = fpMath.Sqrt(fp.FromFloat(sq));
        Assert.AreEqual((float)i, actual.ToFloat(), 0.0001f,
            $"Sqrt({sq}) should be {i}");
    }
}
```

#### Test 4 — `Sqrt_ProducesMonotonicResults`

```csharp
[Test]
public void Sqrt_ProducesMonotonicResults()
{
    // Sqrt is a monotonic function — for inputs a < b, sqrt(a) ≤ sqrt(b).
    // This catches any algorithm that produces wildly inconsistent results
    // (like the buggy Newton that quantizes to powers of 2).
    fp prev = fp.Zero;
    for (int i = 1; i <= 1000; i++)
    {
        fp current = fpMath.Sqrt(fp.FromFloat(i * 0.1f));
        Assert.GreaterOrEqual(current.raw, prev.raw,
            $"Sqrt({i * 0.1f}) raw={current.raw} less than Sqrt({(i-1) * 0.1f}) raw={prev.raw}");
        prev = current;
    }
}
```

#### Test 5 — `Sqrt_RegressionGuard_DriverShotMatch`

```csharp
[Test]
public void Sqrt_RegressionGuard_DriverShotMatch()
{
    // Direct regression guard for the bug fixed by this spec. The driver shot
    // captured in controls_c_diagnosis observed |v|=64.000 m/s when the real
    // value was ≈103.305 m/s. If this test ever fails, the bug has returned.
    fp dotProduct = fp.FromFloat(10672.0f);  // 100.20² + 17.73² + 17.87²
    fp speed = fpMath.Sqrt(dotProduct);
    Assert.AreEqual(103.305f, speed.ToFloat(), 0.05f,
        "Sqrt regression: driver-shot |v| should be ~103.3 m/s, got " + speed.ToFloat());
    Assert.AreNotEqual(64.000f, speed.ToFloat(),
        "Sqrt regression: 64.000 m/s is the broken-Newton power-of-2 cap. The bug has returned.");
}
```

#### Test 6 — `Sqrt_RegressionGuard_PutterShotMatch`

```csharp
[Test]
public void Sqrt_RegressionGuard_PutterShotMatch()
{
    // Regression guard for the putter shot from controls_c_diagnosis.
    // Real |v| ≈ 2.236 m/s; broken Newton returned 2.000 m/s.
    fp dotProduct = fp.FromFloat(5.005f);   // 2.18² + 0.18² + 0.47²
    fp speed = fpMath.Sqrt(dotProduct);
    Assert.AreEqual(2.236f, speed.ToFloat(), 0.01f,
        "Sqrt regression: putter-shot |v| should be ~2.236 m/s, got " + speed.ToFloat());
}
```

That's 6 tests total in the new file. **Do not add Cos / Sin / Trig tests** — those are Phase B.

### Step 3 — Run the full EditMode test suite (FIRST RUN, expected to break)

In the Unity Editor: `Window > General > Test Runner > EditMode > Run All`.

The 6 new `fpMathTests` should ALL pass. The existing 203 tests will likely have failures — count and categorize them.

**Capture the failure list** to `IMPLEMENTER_REPORT.md` § "Test re-snapshot evidence" before doing any test edits. Include:
- Total fail count.
- Per-test-class fail count (e.g., `BallPlacementIntegrationTests: 4 failed of 18`).
- For each failed test: the test name, the asserted-vs-actual values from the log.

This baseline list is what the architect will review to decide whether the failures are "expected re-snapshot territory" or "potential regression."

### Step 4 — Re-snapshot affected tests

For each failing test in Step 3:

1. **Verify the failure is a re-snapshot, not a regression.** A re-snapshot failure looks like: the test expected a specific numeric value (e.g., `Assert.AreEqual(64.0f, ...)` or `Assert.That(carry, Is.InRange(95f, 105f))`), and the actual value is now different but reasonable (e.g., 103.0 vs expected 64.0). A regression looks like: the test now produces NaN, infinity, an exception, or a value with the wrong sign.
2. **For re-snapshot failures:** update the expected value to the new actual. Do NOT widen tolerance bands unless a band test is using a tolerance that no longer fits the corrected value (e.g., `[100, 400]` band that was satisfied by the buggy 64-capped result might no longer be satisfied; in that case, log the band shift in the report — do NOT relax the band on this task).
3. **For genuine regressions (NaN, exception, sign flip, etc.):** STOP. Set STATUS to `IMPLEMENTER_BLOCKED` and surface in `IMPLEMENTER_REPORT.md` "Open questions for Architect." Do NOT try to fix the underlying physics — that's a separate spec.

**Document each test edit** in `IMPLEMENTER_REPORT.md` § "Test re-snapshot evidence":
- Test name.
- Old expected value.
- New expected value.
- One-line justification ("driver carry shifted from X to Y because sqrt now returns true magnitude" or similar).

### Step 5 — Run the full EditMode test suite (SECOND RUN, expected GREEN)

After all re-snapshots: `Window > General > Test Runner > EditMode > Run All`. Expected: 209/209 PASS (203 original + 6 new fpMath tests). If any test still fails, document it and set STATUS to `IMPLEMENTER_BLOCKED`.

### Step 6 — Update `Docs/Physics/PHYSICS_TUNING_TARGETS.md`

The carry/putt numbers in this document are calibrated against the broken sqrt. Add a new section at the top of the document (after any existing header but before the first table):

```markdown
## ⚠ 2026-05-05 — Sqrt fix landed; numbers below need re-validation

Phase A of `controls_d_velocity_cap_diagnosis` repaired the `fpMath.Sqrt`
convergence bug. Every `|v|` reading the simulation produced before this
fix was quantized to a power of 2 (typically 64 m/s for driver-class
shots, 2 m/s for putter-class shots). All carry/putt numbers documented
below were calibrated against that broken behavior.

After the fix:
- Driver `|v|` increased from a capped 64 m/s to its true ~103 m/s.
- Aero drag and rolling resistance now operate on real speeds.
- Net effect on carry/putt distances: shifts; not yet re-validated against
  Trackman/USGA reference data.

**Action item (deferred, not blocking):** re-validate all carry/putt
numbers in this document against real-world reference data. If lab
testing shows distances feel off by more than ~10%, schedule a tuning
spec.

Until that re-validation lands, treat the numbers below as "what the
current physics produces" rather than "what was designed."
```

Do NOT change any other content in this document. The actual carry/putt numbers stay as-is until a separate tuning task re-baselines them with real-world data.

### Step 7 — Capture screenshot evidence (for completeness)

Take a `CaptureHelper.SnapGameView()` screenshot of `LabScaffold.unity` in the editor (no play mode needed). This is just a "lab is in a sane state" sanity check, not a visual-fidelity check. Save to `Docs/Specs/Active/controls_d_velocity_cap_diagnosis/screenshots/lab-state.png`.

(Per project convention from `controls_c_fix` — every spec ships with at least one screenshot showing the rendering side is intact. For this Sqrt-only fix, the lab should look identical to before — that's the point.)

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] `fpMath.Sqrt` body replaced with the libfixmath digit-by-digit port (Step 1 verbatim, comments included).
- [ ] No other code in `fpMath.cs` modified (Sin/Cos/ReduceAngle/Dot/Cross/Normalize/Clamp/Min/Max all unchanged).
- [ ] No `using` directives added or removed.
- [ ] `fpMathTests.cs` created with all 6 specified tests (`Sqrt_KnownValues`, `Sqrt_ZeroAndNegative`, `Sqrt_PerfectSquares`, `Sqrt_ProducesMonotonicResults`, `Sqrt_RegressionGuard_DriverShotMatch`, `Sqrt_RegressionGuard_PutterShotMatch`).
- [ ] All 6 new `fpMathTests` PASS.
- [ ] Test re-snapshot pass complete: every failing existing test categorized as either "re-snapshot" (expected value updated) or "genuine regression" (escalated). Counts in `IMPLEMENTER_REPORT.md`.
- [ ] Final EditMode test gate: 209/209 PASS (203 original + 6 new). If anything less, STATUS goes to `IMPLEMENTER_BLOCKED`.
- [ ] `PHYSICS_TUNING_TARGETS.md` has the new `⚠ 2026-05-05` section at the top, no other content changed.
- [ ] Lab-state screenshot captured to `screenshots/lab-state.png`.
- [ ] No `*.csv`, `*.unity`, `*.prefab`, `*.asmdef` modified.
- [ ] No new compiler warnings in Unity Console attributable to this task.
- [ ] No `System.Math.Sqrt` references introduced anywhere (the existing fallback in the old `Sqrt` body was REMOVED, not added back elsewhere).

## Files / hierarchy this task touches

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Math/fpMath.cs` | Replace `Sqrt` body. No other edits. |
| `Assets/Scripts/Physics/Tests/fpMathTests.cs` | NEW. 6 test methods covering Sqrt. |
| `Assets/Scripts/Physics/Tests/*.cs` (existing) | Re-snapshot failing tests. Touch only those that fail; document each edit. |
| `Docs/Physics/PHYSICS_TUNING_TARGETS.md` | Add `⚠ 2026-05-05 — Sqrt fix landed` section at top. No other edits. |
| `Docs/Specs/Active/controls_d_velocity_cap_diagnosis/screenshots/lab-state.png` | NEW. Lab sanity capture. |

## Out of scope (do NOT do these)

- **Do NOT fix `fpMath.Cos` / `fpMath.Sin`.** That's Phase B. Even though the same captured logs show the trig precision issue, deferring keeps blast radius scoped.
- **Do NOT re-baseline `Docs/Physics/PHYSICS_TUNING_TARGETS.md` carry/putt numbers** to real-world data. Just add the warning section. Real-world re-validation is a separate, deferred task.
- **Do NOT widen test tolerance bands** to make existing tests pass. Re-snapshot the expected values; don't loosen the bounds.
- **Do NOT touch `BallSimulation` integrator logic.** Tests will shift; the integrator stays.
- **Do NOT add a `using System.Math;` import or any other `System.Math` reference** in the new Sqrt body. Pure integer.
- **Do NOT add a `[Conditional]` flag, feature toggle, or runtime switch** to gate the new sqrt behind the old. The fix is unconditional — old behavior was wrong.
- **Do NOT optimize the new Sqrt** beyond what's specified (no micro-optimization of the bit-shift loop, no pre-computed tables, no Newton-ish "fast path" for small inputs). Correctness over performance for this task.
- **Do NOT touch any CSV.** Phase B (Trig) and any future tuning specs handle CSV adjustments.
- **Do NOT add new logging** to `Sqrt`, `BallSimulation`, or anywhere else. The existing diagnostic loggers from `controls_c_diagnosis` are sufficient.
- **Do NOT remove the existing `controls_c_diagnosis` `[ShotEntry]` / `[ShotExit]` / `[Build]` / `[CommitFlick]` / `[RollStep]` / `[PuttStep]` log emissions.** Those are still useful for future diagnosis.

## Pipeline lessons applied

From `Docs/Diagnostics/PIPELINE_LESSONS.md` and `controls_c_fix` retrospective:

- **Lesson F (architect overthinks past Cesar's diagnosis):** Cesar's instruction was "follow recommendation, double check, be 100% sure." Adversarial review confirmed the recommendation. SPEC reflects that locked decision; doesn't relitigate.
- **Lesson G (no thinking-aloud in specs):** scanned, none present.
- **Lesson H (architect verifies claims with sources):** the libfixmath canonical source was fetched and compared. The IronWarrior IL2CPP determinism repo was checked. The Wikipedia algorithm was reviewed. All citations are in `NOTES.md`.
- **Implementer rule from `golfin-implementer.md`:** "Never invent values for things you couldn't verify." If a re-snapshot test produces a value that looks suspicious (e.g., NaN, Infinity, sign-flipped, or wildly out-of-physics-range), surface it as `IMPLEMENTER_BLOCKED` rather than rubber-stamping the new value into the test.

## Mid-task escalation paths

- **If `fpMath.Sqrt` doesn't compile after the body replace:** STOP. The drop-in code in Step 1 was syntax-checked at architect time but Unity may have C# version differences. Set STATUS to `IMPLEMENTER_BLOCKED`, paste the compile error, and ask. Do NOT improvise alternative syntax.
- **If `fpMathTests` PASS but the existing 203 tests show a count of MORE than ~50 failures:** that's a sign the fix changed sim output more than expected. Set STATUS to `IMPLEMENTER_BLOCKED` and surface the count in the report. The architect will decide whether to proceed with re-snapshot or roll back and reconsider.
- **If a test fails with NaN, Infinity, or a sign-flipped value:** STOP. That's not re-snapshot territory; it's a real regression. Set STATUS to `IMPLEMENTER_BLOCKED`.
- **If `PHYSICS_TUNING_TARGETS.md` is not where it's expected** (`Docs/Physics/PHYSICS_TUNING_TARGETS.md`): do not create it or hunt for it elsewhere. Surface in the report. The architect will redirect.

## Notion & roadmap administrivia (architect-side, NOT implementer's responsibility)

The architect (claude.ai chat) will, separately from the implementer pipeline:

- Rename Notion entry `35631e0e-9a36-8133-9734-d5b4418db9f6` from `C.5 — Velocity cap diagnostic (64 m/s mystery)` to `C.5 — fpMath.Sqrt convergence repair (Phase A)`.
- Flip its Status to `In Progress` when this spec moves to Active/.
- Create a new Notion entry under `01. Putter P1` for Phase B (Trig fix), Order 147, Queued, P2.
- Update `Docs/TellCode.md` and `Docs/AI_CONTEXT.md` to reflect this task is in flight.

The implementer just runs the pipeline.
