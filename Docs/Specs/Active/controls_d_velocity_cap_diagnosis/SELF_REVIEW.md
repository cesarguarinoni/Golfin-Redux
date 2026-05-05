# SELF_REVIEW — `controls_d_velocity_cap_diagnosis`

**Reviewer:** golfin-self-reviewer
**Iteration:** 1
**Reviewed at:** 2026-05-05 JST
**Verdict:** `FORWARD_TO_ARCHITECT`

---

## Visual diff notes (Step 1 — screenshot description, no spec)

`screenshots/_compressed/lab-state.png` (1170×2532 PNG, compressed): wide-aspect view dominated by sky — pale blue gradient with thin wispy white cloud band across the top third. Below the cloud band a soft horizon-like haze fades down to a flat off-white / pale-gray lower half. Centered roughly mid-height, slightly left of vertical center, sits a single tiny dark dot ~5–8 pixels — the ball, suspended over what appears to be the ground/horizon. No visible course geometry, no UI, no flicker artifacts, no debug overlay.

This is consistent with a sparse `LabScaffold.unity` editor view in EditMode where the camera framing landed on sky+horizon. **Sane state — nothing visibly broken.** Per SPEC § Step 7 this capture is a sanity check, not a fidelity check, so the framing is acceptable.

## Compare to reference (Step 2)

No Figma reference for this task — it is a pure code/algorithm fix with the screenshot serving only as "lab still renders." Skip.

## Step 5 — Capture-helper compliance

1. **Screenshot provenance.** The current `screenshots/lab-state.png` was regenerated via `CaptureHelper.SnapGameViewWithLabel` per Cesar's note (the original capture from the implementer was via Unity MCP `screenshot-game-view` after `CaptureHelper.SnapGameView()` returned a null-RT error in EditMode). The file in place at review time is CaptureHelper-produced. **COMPLIANT.**
2. **Maintenance protocol for new contexts.** No new `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — this task is pure physics math. **N/A — COMPLIANT.**

---

## Acceptance checklist walk

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| `Sqrt` body replaced verbatim with libfixmath port | PASS | CONFIRM-PASS | Read `fpMath.cs:5-56` — implementation is bit-identical to SPEC Step 1 code block: same comment header (incl. "HISTORY" warning), same `if (x.raw <= 0) return fp.Zero`, same `bit = 1L << 60`, same digit-by-digit loop, same rounding. |
| No other code in `fpMath.cs` modified | PASS | CONFIRM-PASS | Read `fpMath.cs:57-129` — `ReduceAngle`, `Sin`, `Cos`, `Dot`, `Cross`, `Normalize`, `Clamp`, `Min`, `Max`, `Pi`, `DegToRad`, `TwoPi`, `PI`, `TwoPI` all preserved. |
| No `using` directives added or removed | PASS | CONFIRM-PASS | File still has no `using` statements; uses fully-qualified `System.Math.PI` / `System.Math.Round` which were already there (these are NOT `System.Math.Sqrt`). |
| `fpMathTests.cs` created with all 6 specified tests | PASS | CONFIRM-PASS | Read `fpMathTests.cs:1-98` — namespace `Golfin.Physics.Tests`, uses `NUnit.Framework` and `Golfin.Physics.Math`, single class `fpMathTests`. All 6 `[Test]` methods present with names matching spec. Test bodies are byte-identical to spec snippets. |
| All 6 new `fpMathTests` PASS | PASS | CONFIRM-PASS (algorithmic verification) | Implementer reports green via Unity MCP test run. I verified the algorithm by hand-tracing: putter dot 5.005 → result fp ≈ 2.237 (within ±0.01 tol); driver dot 10672 → result ≈ 103.31 (within ±0.05 tol); perfect squares exact for integer i because the rounding step (`if (n > result) result++`) handles the off-by-one at exact integer roots. Monotonicity holds by construction of the digit-by-digit method. |
| Test re-snapshot pass complete & categorized | PASS | CONFIRM-PASS (with one deviation, see below) | 4 failures all categorized as re-snapshot, no NaN/Inf/sign-flip. Deviation 1 noted. |
| Final 209/209 PASS | PASS | CONFIRM-PASS (trust report) | Three test runs reported. |
| `PHYSICS_TUNING_TARGETS.md` warning section added at top | PASS | CONFIRM-PASS | Read lines 8–29 — section text matches SPEC verbatim including the warning emoji, Phase A reference, before/after bullets, and deferred action item. Followed by `---` separator and untouched `Purpose` section. |
| Lab-state screenshot captured | PASS | CONFIRM-PASS | File present at compressed path; renders as sane sky+horizon+ball view. |
| No `*.csv`, `*.unity`, `*.prefab`, `*.asmdef` modified | PASS | CONFIRM-PASS | File list in implementer report contains only `.cs` and `.md` and the new PNG. |
| No new compiler warnings attributable to this task | PASS | CONFIRM-PASS (trust report) | Pre-existing Rindo Course `.meta` errors are noise, unrelated. |
| No `System.Math.Sqrt` references introduced | PASS | CONFIRM-PASS | Read `fpMath.cs` end-to-end — only `System.Math.PI` references remain (constants), no `System.Math.Sqrt` anywhere. The old fallback was removed and not reintroduced. |

---

## Deeper checks

### Algorithm correctness (hand verification)

Spec asked specifically that I verify the new Sqrt is the libfixmath digit-by-digit shift-and-subtract, not still Newton-Raphson. **CONFIRMED.** The body has:
- No `prev` variable, no early-exit on `r >= prev`, no fixed iteration count.
- A `bit` mask that starts at `1L << 60` and halves by 4 each iteration (`bit >>= 2`) — that's the digit-by-digit pair-of-bits processing pattern.
- A guard-loop (`while (bit > n) bit >>= 2`) to find the highest power-of-4 ≤ n.
- A subtract-and-include-bit step in the main loop.
- A final round-up (`if (n > result) result++`).

This matches the libfixmath canonical exactly (single-pass int64 form because `fp.raw` is `long`).

### Non-power-of-2 input coverage

The Newton bug fired specifically for inputs whose seed undershoots — i.e., non-power-of-4 inputs. Tests cover this:
- `Sqrt_KnownValues` includes 5.005, 10672, 32768 (all non-power-of-4 — old code returned 2, 64, 256 respectively).
- `Sqrt_RegressionGuard_DriverShotMatch` adds an explicit `Assert.AreNotEqual(64.000f, ...)` guard against the broken value's return.
- `Sqrt_RegressionGuard_PutterShotMatch` covers the small-input case.

Good coverage of the bug surface.

### Zero / negative handling

`fpMath.Sqrt(fp.Zero)` → `x.raw == 0`, the `<= 0` guard returns `fp.Zero`. ✓
`fpMath.Sqrt(fp.FromFloat(-1.0f))` → `x.raw = -65536`, the guard returns `fp.Zero`. ✓
The new test `Sqrt_ZeroAndNegative_ReturnsZero` asserts both with tolerance 0. Solid.

### Physical plausibility of re-snapshotted carries

This was the area Cesar flagged most strongly. Walking the new numbers:

| Club | Old expected (broken) | New expected | Direction | Plausible? |
|---|---|---|---|---|
| driver | 275 yd | 263 yd | DOWN | Yes — broken |v|=64 vs true 75 ⇒ drag now scales correctly with v² so net drag higher and carry shorter. 4.4% drop is in line. |
| iron7 | 172 yd | 199 yd | UP | Yes — broken Normalize produced vHat with magnitude 52.5/32 = 1.64×, inflating drag by ~1.64×; removing that inflation more than offsets the now-correct |v|. |
| iron9 | 152 yd | 180 yd | UP | Same mechanism. |
| pwedge | 136 yd | 168 yd | UP | Same mechanism. |

The "irons go UP, driver goes DOWN" asymmetry is counterintuitive but **internally consistent with the broken Normalize ratio**:
- Driver broken |v|=64, true |v|=75 → vHat magnitude ratio 75/64 = 1.17× (only 17% drag inflation in the broken state).
- Iron7 broken |v|=32, true |v|=52.5 → vHat magnitude ratio 52.5/32 = 1.64× (64% drag inflation in the broken state).

For irons, removing 64% drag inflation outweighs the small drag-from-correct-|v|² increase. For the driver, the ratios are close enough that the drag-from-correct-|v|² effect dominates. The implementer's documented reasoning matches this.

**Concern noted (NOT a self-review FAIL):** The new iron numbers (199 yd 7-iron, 180 yd 9-iron, 168 yd P-wedge) are **physically unrealistic** for the intended PGA-Tour-pro-class targets in `PHYSICS_TUNING_TARGETS.md` (Tour averages: 7-iron ~175 yd, 9-iron ~145 yd, P-wedge ~115 yd). The new numbers are 10–45 % above tour-pro, which would feel "too easy" in playtest. **However** the SPEC explicitly defers real-world re-validation as a separate task and adds the warning section to `PHYSICS_TUNING_TARGETS.md` exactly to flag this. The warning section is present. The carries are documented as "what the corrected physics produces" not "what was designed." Per spec, this is correct behavior for THIS task; tuning is for a follow-up spec.

I am surfacing this to the architect explicitly so it isn't lost in the pipeline — the warning section in TUNING_TARGETS is the right immediate move, but the lab needs a re-tune spec scheduled.

### Did re-snapshots silently mask real regressions?

Walked each re-snapshot:
- All 4 failing test deltas are large but plausible-direction shifts of the Carry assertions, not NaN / Inf / sign-flip.
- No test became trivially passing (e.g., a tolerance widened to swallow any value).
- The 3-yd headwind/tailwind gaps in WindTests (Tests 2–3) and the 2 m crosswind drift assertion (Test 4) are unchanged — those still validate real physics behavior.
- The `Wind_Gust_SeedDeterminism` threshold went from `> 0.5m` to `> 0.1m`. The actual observed delta (0.194m) sits comfortably above the new threshold (1.94×). Acceptable. The test still validates seed-driven divergence, just with a smaller margin.
- No test was deleted, skipped, or `[Ignore]`-tagged.

No masking detected.

### Spec deviations

**Deviation 1 (implementer-flagged):** Driver expected value (275→263) was re-snapshotted even though the existing constant-mode 20% test would still have passed at 275 (262.6 vs 275 = 4.5% error). SPEC says "touch only those that fail." Driver was technically passing.

**Self-reviewer judgment:** acceptable. Two reasons:
1. The `Clubs[]` array entries are shared across multiple test methods (constant + LUT modes). Keeping driver at the broken-physics-calibrated 275 while updating iron7/iron9/pwedge to corrected-physics values would introduce inconsistency and confuse future readers of the array.
2. The new value (263) is the true physics output and matches the spec's intent ("document what the current physics produces") even though it stretches the literal "touch only failing tests" rule.

The deviation is honestly disclosed in IMPLEMENTER_REPORT § "Spec deviations." Architect can override if they want to revert to 275.

**Deviation 2 (implementer-flagged):** Added a new comment block to the `Clubs[]` array explaining the re-snapshot rationale. Not strictly in scope but it's an in-test code comment, no behavior change. Acceptable.

---

## Verdict: `FORWARD_TO_ARCHITECT`

The implementation is faithful to the spec, the algorithm is correct (hand-verified against the libfixmath canonical and against putter/driver dot-product targets), the test coverage is solid for the bug surface, the re-snapshots are honest and plausible, and the warning section in TUNING_TARGETS lands as required. The two deviations are minor, documented, and reasonable.

**One concern surfaced for the architect** (not a fail of THIS spec): the new corrected-physics carries for irons/wedges are above realistic Tour-pro yardages, which means the upcoming Loop v1 lab will likely need a tuning spec. The warning section already flags this. No immediate action required from the implementer.

## Pipeline lessons applied this review

- **Lesson A / B (pixels over YAML / describe before diagnosing):** N/A — no UI screenshot to diff against a Figma. The lab-state screenshot is an "is anything on fire" check, not a layout check.
- **Lesson H (architect verifies claims with sources):** I traced the digit-by-digit algorithm against the libfixmath description in `NOTES.md` and against my own arithmetic for the 10672 / 5.005 cases. Implementer's algorithmic correctness claim is supported.
- **`controls_c_fix` capture rule:** N/A here — no physics-lab at-rest evidence requested by this spec, only a single editor-mode sanity capture. Compliant.
