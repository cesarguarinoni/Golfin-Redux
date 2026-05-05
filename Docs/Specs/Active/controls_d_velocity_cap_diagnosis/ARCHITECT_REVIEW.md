# ARCHITECT_REVIEW — `controls_d_velocity_cap_diagnosis`

**Reviewer:** golfin-reviewer
**Reviewed at:** 2026-05-05 JST
**Verdict:** ~~`ARCHITECT_REVIEW_PASS`~~ → **`ARCHITECT_REVIEW_FAIL`** (overridden by human Architect 2026-05-05 after escalation)
**Iteration:** 1 (with addendum from claude.ai chat)

---

## Summary

The libfixmath digit-by-digit Sqrt port lands cleanly. Algorithm body is bit-identical
to SPEC § Step 1, the 6 new regression tests cover the bug surface (small + large +
non-power-of-2 + zero + perfect squares + putter/driver guards), and the 4 re-snapshotted
existing-test deltas are physically consistent (driver carry DOWN because broken |v|=64 was
near-true 75 so corrected drag dominates; irons UP because broken Normalize inflated
vHat magnitude by 1.6× and removing that inflation dwarfs the now-correct |v|² drag bump).
Final gate: 209/209 PASS as reported. No CSVs, scenes, prefabs, or asmdefs touched.
Warning section in `PHYSICS_TUNING_TARGETS.md` is verbatim per spec.

The self-reviewer's flagged concern — post-fix iron carries 10–45% above realistic PGA Tour
pro yardages — is real but **out of scope for this spec**. The spec explicitly defers
real-world re-validation to a separate task and adds the warning section in TUNING_TARGETS
exactly to flag this. The warning is present and reads correctly. A tuning follow-up spec
should be scheduled separately (architect-side admin, not blocking this PASS).

---

## Spot checks

### `Assets/Scripts/Physics/Math/fpMath.cs` (lines 5–56)

Verified body matches SPEC § Step 1 verbatim:

- `if (x.raw <= 0) return fp.Zero` guard — present.
- `long n = x.raw << 16` — present.
- `long bit = 1L << 60` seed — present.
- `while (bit > n) bit >>= 2` find-highest-power-of-4 loop — present.
- Digit-by-digit main loop with `result = (result >> 1) + bit` on the include branch
  and `result >>= 1` on the skip branch, with `bit >>= 2` per iteration — present.
- Final `if (n > result) result++` rounding step — present.
- HISTORY comment block warning future readers not to revert — present.

No Newton-Raphson code anywhere in the file. No `prev` variable, no `r >= prev` early
exit, no fixed iteration count. No `System.Math.Sqrt` reference. Lines 57–129 (other
methods) verified untouched against the spec's "no other edits" requirement.

### `Assets/Scripts/Physics/Tests/fpMathTests.cs`

All 6 `[Test]` methods present in `Golfin.Physics.Tests` namespace, using `NUnit.Framework`
and `Golfin.Physics.Math`. Test bodies are byte-identical to SPEC § Step 2 snippets:

- `Sqrt_KnownValues_MatchesRealArithmetic` — 8 input cases incl. non-power-of-2.
- `Sqrt_ZeroAndNegative_ReturnsZero` — 0 and 2 negative inputs.
- `Sqrt_PerfectSquares_ExactToFpPrecision` — 51 perfect squares (0..50²).
- `Sqrt_ProducesMonotonicResults` — 1000 inputs verifying monotone non-decrease.
- `Sqrt_RegressionGuard_DriverShotMatch` — explicit `Assert.AreNotEqual(64.000f, ...)`
  guard on the broken power-of-2 cap. This is exactly the right shape for a regression
  guard.
- `Sqrt_RegressionGuard_PutterShotMatch` — putter dot 5.005 → expected 2.236 m/s.

### `Assets/Scripts/Physics/Tests/AerodynamicsTests.cs`

`Clubs[]` array re-snapshotted with a 6-line comment block (lines 27–33) explaining the
reason and citing this spec. Values:

| Club    | Speed   | New expected | Plausibility |
|---|---|---|---|
| driver  | 75 m/s  | 263 yd       | In SPEC range (250–280); slightly below tour-pro 275 |
| iron7   | 52.5 m/s| 199 yd       | Above tour-pro 175 — flagged in TUNING_TARGETS warning |
| iron9   | 48.5 m/s| 180 yd       | Above tour-pro 145 — flagged in TUNING_TARGETS warning |
| pwedge  | 46 m/s  | 168 yd       | Above tour-pro 115 — flagged in TUNING_TARGETS warning |

Monotonic by club number ✓ (driver > iron7 > iron9 > pwedge). Putter not in array
(would be a follow-up `BallPlacementIntegrationTests` re-snapshot if it had broken,
but per IMPLEMENTER_REPORT it didn't fail — and the SPEC explicitly forbids touching
non-failing tests, so leaving putter alone is correct).

The original "do NOT adjust expected_carry_yd or widen tolerances" warning comment
in the `AssertClubCarriesWithinTolerance` helper is preserved unchanged. Good — that
warning is forward-looking (don't casually adjust); the present re-snapshot has a
documented reason (Sqrt fix) so it's a justified one-time exception.

**Implementer's Deviation 1** (driver 275→263 re-snapshot even though the test was
passing at 275): self-reviewer accepted this and I concur. Keeping driver at the
broken-physics-calibrated 275 yd while irons sit at corrected-physics values would
introduce inconsistency in the shared `Clubs[]` array. The new value is the true
physics output. Acceptable departure from "touch only failing tests."

**Implementer's Deviation 2** (added comment block to `Clubs[]`): also acceptable —
in-test code comment, no behavior change, improves future readability of the
re-snapshot's rationale.

### `Assets/Scripts/Physics/Tests/WindTests.cs` (line 158–190)

Only `Wind_Gust_SeedDeterminism` modified. Threshold change (0.5m → 0.1m) carries a
4-line explanatory comment citing this spec. Observed delta 0.194m sits 1.94× above the
new threshold, so the test still meaningfully validates seed-driven divergence. The
"same seed → bit-exact" half of the test (lines 174–177) is unchanged. Tests 1–4 and 6
all untouched.

### `Docs/Physics/PHYSICS_TUNING_TARGETS.md` (lines 8–29)

Warning section text matches SPEC § Step 6 verbatim — same emoji, same date, same
Phase A reference, same before/after bullet structure, same deferred action item.
Followed by `---` separator and untouched Purpose section. No other content in the
file changed.

### Screenshot at `screenshots/_compressed/lab-state.png`

Sky-and-horizon view with a single small dark dot mid-frame (the ball). Per SPEC § Step 7
this is a sanity capture, not a fidelity check. Lab is in a sane state — no rendering
corruption, no UI artifacts, no debug overlay screaming about errors. PASS.

---

## Architectural soundness

- No asmdef changes. No new `using` directives in `fpMath.cs`. No cross-namespace
  reference shifts.
- The fix is contained to `fpMath.Sqrt` — the contract `fp Sqrt(fp x)` is preserved
  exactly so every caller (`Normalize`, `BallSimulation`, etc.) gets a transparent
  correctness upgrade.
- Determinism contract preserved: pure integer arithmetic, no `System.Math.Sqrt` fallback
  re-introduced. The implementer correctly removed the old fallback rather than burying it
  somewhere else.
- No new logging, no feature flag, no `[Conditional]` toggle — clean unconditional fix
  per spec.

## Latent issues / cross-cutting concerns

- **Carries above tour-pro** — already documented in `PHYSICS_TUNING_TARGETS.md` warning
  section. Architect should schedule a follow-up tuning spec, but **NOT** as part of this
  task. Current state is the explicitly chosen interim: "what the corrected physics
  produces" with a documented action item to re-baseline.
- **Phase B trig fix** — `fpMath.Cos` / `fpMath.Sin` Taylor accuracy at angles near ±π
  is the next bug, deferred per spec. No need to gate this PASS on it.
- **Putter regression guard not exercised by integration tests** — the new `fpMathTests`
  cover the putter case at the unit level, but no `BallPlacementIntegrationTests` putter
  scenario was re-snapshotted because none failed. That's actually a sign the putter
  integration tests didn't have tight numeric assertions to begin with — worth keeping
  in mind for a future putter-tuning task, but not a blocker here.

## Capture-helper compliance (backstop check)

- **Screenshot provenance:** the file at `screenshots/lab-state.png` is the
  CaptureHelper-produced version per Cesar's note in SELF_REVIEW.md (the original was
  via Unity MCP after `CaptureHelper.SnapGameView()` returned a null-RT in EditMode).
  Compliant.
- **Maintenance protocol for new contexts:** no new `*Context.cs` files added under
  `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — pure physics task. N/A — compliant.

Self-reviewer's Step 5 finding is correct.

## Re-snapshot integrity

Spot-checked each re-snapshot for masking:
- No test became trivially passing (no tolerance widened).
- No test deleted, skipped, or `[Ignore]`-tagged.
- All 4 failing tests had numeric expectations updated to match observed actuals
  with reasonable error margins (1.2–8.7%).
- The 3-yd headwind/tailwind gap and 2m crosswind drift assertions in WindTests are
  unchanged — those continue to validate real physics behavior.
- No NaN, Infinity, sign-flip, or exception in any re-snapshot.

No silent regressions detected.

---

## Pipeline-lessons applied this review

- **Lesson H (verify claims with sources):** I confirmed the new Sqrt body matches the
  libfixmath canonical algorithm shape (bit-find loop + digit-by-digit pair-of-bits +
  round-up step) by reading the file rather than trusting the implementer's summary.
- **`controls_c_fix` capture rule (physics-lab at-rest):** N/A — no physics-lab at-rest
  evidence requested. The single editor-mode sanity capture is fine.
- **Self-reviewer trust without rubber-stamp:** I re-read the algorithm and re-checked
  carry plausibility independently rather than just confirming the self-reviewer's
  PASSes. All findings concur.

## Deferred items for the architect (claude.ai chat) to handle separately

1. Schedule a Phase B trig-fix Notion entry (`fpMath.Cos` / `fpMath.Sin` Taylor accuracy).
2. Schedule a tuning re-baseline spec to bring iron/wedge carries down to tour-pro
   ranges (or update the design intent to accept the higher numbers — Cesar's call).
3. Flip Notion `35631e0e-9a36-8133-9734-d5b4418db9f6` from In Progress → Done after Cesar's
   manual approval.

None of these block this task.

---

## ADDENDUM — Human Architect override (claude.ai chat, 2026-05-05)

Cesar escalated `ARCHITECT_REVIEW_PASS` → `ARCHITECT_REVIEW_ESCALATE` after walking the post-fix carry numbers and seeing iron/wedge carries 10–46% above Tour-pro yardages. After adversarial review (web sources: PGA TOUR 2K23 dev blog, Bearman-Harvey 1976 original paper, Cornell SimScience golf physics page, Quora deterministic-physics-engines, libfixmath, IronWarrior IL2CPP determinism repo), the human Architect concurs with the in-pipeline reviewer that the Sqrt fix itself is correct and lands cleanly. The carry shifts are real but stem from a separate, deeper bug in the lift LUT that was being masked by the Sqrt bug — not from any error in this task's implementation.

**However**, the warning section in `PHYSICS_TUNING_TARGETS.md` is judged insufficient as the only signal. Documents are easy to miss; an ignored test in the runner is not. The fix is converted to `ARCHITECT_REVIEW_FAIL` with one tightly-scoped fail item: add a tripwire test.

### Architecture frame (for future reference)

GolfinRedux physics is structured as two layers, formalized after this escalation:

- **Layer 1 — Core physics.** Integrator, fp arithmetic, drag/lift formula shape, surface model. This is canonical — derived from real physics, not designed for feel. The Sqrt fix in this task is a Layer-1 cleanup.
- **Layer 2 — Coefficient overlay (NEW concept, formalized in `controls_e_aero_overlay_pass`).** Where Layer-1 inputs (Bearman-Harvey LUTs, surface k values, etc.) are valid, they are used as-is. Where they extrapolate past the valid range OR diverge from desired Tour-pro outcomes, a separate overlay file applies a documented correction. The overlay is the gameplay layer; the core LUT stays a faithful transcription of real physics.

This frame is not invented for this task; it's the universal pattern in deterministic-physics game development (PGA TOUR 2K23 explicitly: "refine the extremes"; Quora consensus on game physics: "tuned for feel rather than physical realism"). Naming it makes the boundary auditable.

### Single fail item (this task only — small scope-extension)

**FAIL-1: Add tripwire test `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` to `fpMathTests.cs` or a new `AeroCalibrationTripwireTests.cs` file.**

Requirements:
- The test MUST be tagged `[Ignore("Awaiting controls_e_aero_overlay_pass calibration. See ESCALATION_TO_ARCHITECT.md.")]` so it does NOT count against the 209/209 gate but IS visible in the test runner as a known-pending item.
- The test body MUST iterate the existing `AerodynamicsTests.Clubs[]` array (or copy its values into a local array if cross-class access is awkward) and assert each club's LUT-mode simulated carry is within ±10% of the documented Tour-pro target.
- Tour-pro targets to assert against (from PGA TOUR 2K23 / Trackman composite Tour data, citation in test comment):
  - **driver:** 290 yd ±10% (range 261–319)
  - **iron7:** 175 yd ±10% (range 158–193)
  - **iron9:** 145 yd ±10% (range 131–160)
  - **pwedge:** 115 yd ±10% (range 104–127)
- Test method docstring MUST read approximately: *"Tripwire test for the Layer-2 aero calibration. Currently `[Ignore]`-tagged because the lift LUT extrapolates Bearman-Harvey 1976 data past its valid spin-parameter range (S>0.30), causing iron/wedge over-prediction. Will be enabled by `controls_e_aero_overlay_pass`. Definition of done for that spec: this test passes."*
- The new test file (if separate) goes in `Assets/Scripts/Physics/Tests/`. Adding to `fpMathTests.cs` is acceptable but slightly off-topic since it's not an fpMath test — the new file is preferred.
- After adding, re-run the EditMode test suite. The new test must show as **Ignored** (yellow), not Passed or Failed. Total count goes from 209 to 210 with 209 PASS + 1 IGNORED.

**Out of scope for this fail (do NOT do these):**
- Do NOT actually fix the lift LUT in this iteration. The overlay calibration is `controls_e_aero_overlay_pass`'s scope.
- Do NOT add additional regression tests beyond this one tripwire.
- Do NOT modify any other test, CSV, or config.
- Do NOT touch `aero_lift_lut.csv` or `aero_drag_lut.csv`.
- Do NOT remove the `[Ignore]` attribute or change its message format — the message references `controls_e_aero_overlay_pass` by name to make the linkage discoverable.
- Do NOT alter the existing 209 tests further — the re-snapshots from iteration 1 stand.

### Path to PASS after this addendum

1. Implementer adds the single tripwire test per FAIL-1.
2. Re-run the EditMode test suite. Expect: 210 total, 209 PASS, 1 IGNORED.
3. STATUS → `READY_FOR_SELF_REVIEW`. Self-reviewer confirms the tripwire is `[Ignore]`-tagged with the correct message format and references `controls_e_aero_overlay_pass`. Forward to reviewer.
4. Reviewer (golfin-reviewer subagent) confirms structural correctness and PASSes.
5. Cesar approves → DONE.

The lift LUT recalibration is `controls_e_aero_overlay_pass`, scheduled separately and architected as a **corner-case overlay** (preserves Bearman-Harvey as the canonical Layer 1; overlay file applies tuned corrections only past the published-valid range). That spec will be written by the human Architect after this task lands.
