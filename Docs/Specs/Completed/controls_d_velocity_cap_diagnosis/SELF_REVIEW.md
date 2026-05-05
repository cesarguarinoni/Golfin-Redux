# SELF_REVIEW — `controls_d_velocity_cap_diagnosis` (Iteration 2)

**Reviewer:** golfin-self-reviewer
**Iteration:** 2
**Reviewed at:** 2026-05-05 JST
**Verdict:** `FORWARD_TO_ARCHITECT`

---

## Scope of this iteration

Iteration 1 PASSED architect-review on the Sqrt fix itself, then was overridden to `ARCHITECT_REVIEW_FAIL` by the human Architect (claude.ai chat) with a single fail item: add a tripwire test for the Layer-2 aero calibration so the lift-LUT over-prediction is visible in the test runner, not just buried in `PHYSICS_TUNING_TARGETS.md`.

This iteration-2 review focuses narrowly on FAIL-1 from `ARCHITECT_REVIEW.md` § ADDENDUM. The Sqrt fix, fpMathTests, re-snapshots, and warning-section edits from iteration 1 are **not re-reviewed** — they already cleared.

---

## FAIL-1 walk-through

The architect addendum (ARCHITECT_REVIEW.md lines 210–230) specified six requirements for the tripwire. I verified each against `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` (read end-to-end, lines 1–137).

| Requirement | Verdict | Evidence |
|---|---|---|
| Test method name `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime`. | CONFIRM-PASS | Line 109: `public void Aero_AllClubs_WithinTourCarryRange_PerSpinRegime()`. Exact match. |
| `[Ignore("Awaiting controls_e_aero_overlay_pass calibration. See ESCALATION_TO_ARCHITECT.md.")]` — exact string per architect addendum. | CONFIRM-PASS | Line 108: byte-identical. Period after `.md` present, capitalisation matches, message references `controls_e_aero_overlay_pass` and `ESCALATION_TO_ARCHITECT.md` by name. Implementer report confirms test runner emitted the same string verbatim. |
| Iterates `AerodynamicsTests.Clubs[]` (or local copy) and asserts each LUT-mode carry within ±10% of Tour-pro target. | CONFIRM-PASS | Lines 35–46 mirror `AerodynamicsTests.Clubs[]` verbatim with the added `tourProTargetYd` column. Lines 117–126 loop and compute `errPct = abs(actual-target)/target*100` and OR-accumulate into `anyFailed`. `tolerancePct = 10f` on line 115. |
| Tour-pro targets exactly: driver 290, iron7 175, iron9 145, pwedge 115. | CONFIRM-PASS | Lines 42–45: `290f`, `175f`, `145f`, `115f` in that order. PGA TOUR 2K23 / Trackman citation in lines 37–41 comment per architect requirement. |
| Docstring references "Layer-2 aero calibration", "lift LUT extrapolat[ion]", "controls_e_aero_overlay_pass". | CONFIRM-PASS | Two locations: file-header comment lines 4–8 and `<summary>` block lines 23–28 / 100–105. Both name all three concepts and explicitly call out "Bearman-Harvey 1976 data past its valid spin-parameter range (S > 0.30), causing iron/wedge over-prediction". |
| File in `Assets/Scripts/Physics/Tests/` (new file preferred). | CONFIRM-PASS | Path: `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs`. Meta file `AeroCalibrationTripwireTests.cs.meta` exists with GUID `b8a98abae91a4dd9897dd567135ee466`. New file, not an addition to `fpMathTests.cs` (good — tripwire isn't an fpMath test). |
| Final EditMode gate: 210 total, 209 PASS, 1 IGNORED. | CONFIRM-PASS (trust report) | Implementer report: `TotalTests: 210, PassedTests: 209, FailedTests: 0, SkippedTests: 1`. Skipped test name and message confirmed via runner output. |

All seven sub-requirements met.

---

## Tripwire-will-actually-fire-when-unignored check

The most important property of a tripwire is that **un-ignoring it must produce a real failure today**, otherwise the architect's whole point ("an ignored test in the runner is not [easy to miss]") evaporates.

Running the math against iteration-1's documented LUT-mode carries (`Aero_ClubCarries_LutMode_*` re-snapshot section in `IMPLEMENTER_REPORT.md`):

| Club    | Current actual (LUT) | Tour-pro target | err%  | Verdict if `[Ignore]` removed |
|---------|----------------------|-----------------|-------|-------------------------------|
| driver  | 240.4 yd (LUT)       | 290 yd          | 17.1% | FAIL (>10%)                   |
| iron7   | 202.3 yd             | 175 yd          | 15.6% | FAIL (>10%)                   |
| iron9   | 184.3 yd             | 145 yd          | 27.1% | FAIL (>10%)                   |
| pwedge  | 170.1 yd             | 115 yd          | 47.9% | FAIL (>10%)                   |

(Note: the tripwire reruns its own LUT simulation via `MakeLutConfig()` + `BallSimulation.Simulate(...)`, so the actuals it produces will match `Aero_ClubCarries_LutMode_*` runs to bit-precision since the LUT values, AeroConfig defaults, ground, and shot inputs are mirrored exactly. I diffed lines 49–98 against `AerodynamicsTests.MakeLutConfig` / `MakeShot` — the bodies are byte-identical.)

So all four clubs would fail today; `anyFailed = true`; `Assert.IsFalse(true, ...)` fires with the rich per-club table embedded in the assertion message. Loud, actionable failure — not a no-op. ✓

The tripwire is also self-documenting via `UnityEngine.Debug.Log("[AeroCalibrationTripwireTests] Tour-carry tripwire:" + table)` on line 129, which means even when ignored, manually un-skipping the test produces both a structured log line and an assertion message. Good belt-and-braces.

---

## Capture-helper compliance (Step 5)

1. **Screenshot provenance.** No new screenshot was added in iteration 2 — the existing `screenshots/lab-state.png` from iteration 1 stands and was already deemed compliant in the prior SELF_REVIEW.md and ARCHITECT_REVIEW.md. **N/A — COMPLIANT.**
2. **Maintenance protocol for new contexts.** No new `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — iteration 2 is pure test addition. **N/A — COMPLIANT.**

---

## Iteration awareness

This is iteration 2. No previous self-review for iteration 2 exists; iteration-1 SELF_REVIEW.md ended in `FORWARD_TO_ARCHITECT`, then the architect-review FAILed (after human Architect override). Per the iteration-awareness rule (FAIL at N≥3 escalates), iteration 2 with verdict PASS does not need to escalate.

---

## Verdict: `FORWARD_TO_ARCHITECT`

The single FAIL-1 item from the architect addendum is addressed exactly as specified:

- Tripwire test exists at `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs`.
- `[Ignore]` message string is byte-identical to the architect's required wording.
- Tour-pro targets (290/175/145/115 yd, ±10%) are embedded with citation.
- Docstring references all three required concepts (Layer-2, lift LUT extrapolation, `controls_e_aero_overlay_pass`).
- LUT config and shot construction mirror `AerodynamicsTests` exactly, so the test will produce the same actuals as its sibling tests.
- Un-ignoring today fires a real, loud failure on all four clubs (verified by hand from the iteration-1 LUT-mode actuals) — the tripwire is not a no-op.
- Final EditMode gate: 210 total / 209 PASS / 1 IGNORED, matching the architect's path-to-PASS step 2.
- No CSV / scene / prefab / asmdef touched in this iteration.
- No tests beyond the single tripwire added; no other tests modified; no `[Ignore]` shenanigans on existing tests.

Forwarding to architect-review for final approval.

## Pipeline lessons applied this review

- **Lesson H (verify claims with sources):** I read the test file end-to-end and re-derived the un-ignored failure outcome from the iteration-1 LUT actuals rather than trusting the implementer's PASS claim on the tripwire's "will fire" property.
- **`controls_c_fix` capture rule:** N/A — no physics-lab at-rest evidence requested by this iteration. The lab screenshot from iteration 1 stands.
- **Self-reviewer trust without rubber-stamp:** I confirmed `[Ignore]` message exactness (period, capitalisation, file reference) character-by-character rather than approximately, since the architect explicitly required exact string match for discoverability.
