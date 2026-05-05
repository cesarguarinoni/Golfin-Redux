# ARCHITECT_REVIEW — `controls_e_aero_overlay_pass`

**Reviewer:** Human Architect (claude.ai chat) — direct response to `STATUS=IMPLEMENTER_BLOCKED`. Pipeline reviewer subagent did not run for this iteration; the implementer escalated directly via `IMPLEMENTER_REPORT.md § "Open questions for Architect"` and the architect is responding here.
**Reviewed at:** 2026-05-05 JST
**Verdict:** **`ARCHITECT_REVIEW_FAIL`** — three FAIL items, all tightly scoped. No PASS yet; implementer must address all three before re-submitting for self-review.
**Iteration:** 1 (escalated mid-pipeline)

---

## Summary

The implementer correctly identified that the driver cannot be calibrated by the Layer-2 lift overlay because it sits at S=0.08, well inside Bearman-Harvey valid range. That diagnosis is right and respects the architecture. However, the iteration revealed two issues neither the implementer nor the reviewer caused: (1) the architect's tripwire-test target values were wrong on three of four clubs due to a unit-mismatch error in the source PDF (METERS table mistaken for YARDS table — see `Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson K), and (2) the architecture decision on how to handle a known Layer-1-territory miss (the driver) needs to be locked in this task rather than deferred.

With **corrected** Trackman targets, the irons/wedge calibration is over-tuned (m40=0.55 was too aggressive because it was chasing wrong targets that were 8–13 yards too short). With correct targets, the overlay should land closer to m40≈0.85 — a much smaller correction that respects the Bearman-Harvey curve more.

The driver miss is real and Layer-1 territory (drag LUT). It's NOT carved out as a special-case driver subsystem. Instead, the tripwire is **split into two tests**: one for low-S clubs (driver alone for now) with a known-pending `[Ignore]` pointing at `controls_f`, and one for mid/high-S clubs (irons + wedge) at the full ±10% gate. This keeps the architecture unified — same overlay, same blend, same methodology — while honestly exposing what passes and what doesn't.

## Cesar's locked decisions (from chat, 2026-05-05)

1. **Targets:** Use the YARDS table from the Trackman PDF (`https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf`), cross-verified against Maryland Golf Camps article (`https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html`) and Golf Monthly Trackman 2023 citations. Two independent sources confirm the YARDS values.
2. **No driver-specific subsystem.** The architecture stays unified: one overlay file, one blend, one methodology. The driver's miss is acknowledged as a Layer-1 drag-LUT issue out of `controls_e`'s scope.
3. **Split tripwire test.** Driver gets its own test, `[Ignore]`-tagged with reference to `controls_f`. Irons + wedge get one test at the unified ±10% gate. Cesar does NOT want the irons/wedge gate relaxed to ±15% to cover for the driver — "I don't want other clubs braking the 10% rule."
4. **Pipeline lesson K written.** The unit-mismatch failure mode (Mars Climate Orbiter parallel) is now documented in `Docs/Diagnostics/PIPELINE_LESSONS.md`. Architect-side mental ritual updated.
5. **`controls_f_drag_calibration_audit` escalated** P3 → P1, renamed "Drag LUT calibration audit (driver carry blocker)". Architect-side admin; implementer doesn't act on this.

## FAIL items (implementer must address all three)

### FAIL-1 — Correct the Trackman target values in `AeroCalibrationTripwireTests.cs`

The current values (driver 290 / iron7 175 / iron9 145 / pwedge 115) are wrong on three of four clubs. The architect (claude.ai chat) misread the Trackman PDF source by transcribing values from the METERS table while believing they were from the YARDS table. The correct values, verified against the Trackman PDF YARDS row + the Maryland Golf Camps article + the Golf Monthly Trackman 2023 citations, are:

| Club  | Old (wrong) | **Corrected (verified Trackman 2023 PGA Tour)** | Source |
|-------|-------------|--------------------------------------------------|--------|
| Driver | 290 yd | **275 yd** | Trackman PDF YARDS row, "Driver ... 275"; cross-verified Maryland Golf Camps "275 yards" |
| 7-iron | 175 yd | **172 yd** | Trackman PDF YARDS row, "7 Iron ... 172"; cross-verified Maryland Golf Camps "172 yards" |
| 9-iron | 145 yd | **148 yd** | Trackman PDF YARDS row, "9 Iron ... 148"; cross-verified Maryland Golf Camps "148 yards" |
| PW | 115 yd | **136 yd** | Trackman PDF YARDS row, "PW ... 136"; cross-verified Maryland Golf Camps "136 yards" |

Update the four `tourProTargetYd` values in the `Clubs[]` tuple in `AeroCalibrationTripwireTests.cs`. Also update the comment block that lists the targets (lines 10–13 of the file).

The unit-suffix annotation discipline applies: when these values appear in code or comments, write `275f` with a `// yards (Trackman PDF YARDS table)` annotation, NOT just `275f`. Future readers must not be able to mistake the unit.

### FAIL-2 — Re-tune the lift overlay against corrected targets

With the wrong targets, the implementer was driven to m40=0.550 to bring PW (then-target 115) within range. Real PW target is 136, so PW under the v3 overlay (currently 126yd) is actually **−7.4% under** target, not over. The overlay is over-correcting downward.

Re-run the calibration sweep with the corrected targets in FAIL-1. Expected outcome based on architect's back-of-envelope:

| Club | Sim actual @ m40=0.55 | Corrected target | Error (%) | Expected status after re-tune |
|------|----------------------|-------------------|-----------|-------------------------------|
| 7-iron | 165 | 172 | −4.1% | PASS (no re-tune needed) |
| 9-iron | 132 | 148 | −10.8% | FAIL barely; m40 needs to relax to ~0.75–0.85 |
| PW | 126 | 136 | −7.4% | PASS-ish; m40 relax helps further |

Iterate the overlay back upward (less aggressive correction) until 7-iron, 9-iron, and PW all sit within ±10% of the corrected targets. The driver is NOT part of this re-tune — see FAIL-3.

The expected final m40 lands around **0.80–0.90**, much less aggressive than 0.55. This is healthier — the overlay is doing a smaller correction past Bearman-Harvey valid range, preserving more of the Layer-1 curve. If iteration produces an m40 still below 0.70 after correct targets, escalate again because something else is off.

The smoothstep seam check (Step 8 of SPEC) must be re-verified at the new multiplier. Re-document in `IMPLEMENTER_REPORT.md`.

### FAIL-3 — Split the tripwire test into two

Replace the single `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` test with **two** tests in the same file. Cesar's locked preference: the driver's known Layer-1 miss must NOT degrade the iron/wedge gate.

#### Test A — `Aero_MidHighSpinClubs_WithinTourCarryRange`

```csharp
[Test]
public void Aero_MidHighSpinClubs_WithinTourCarryRange()
{
    // 7-iron, 9-iron, PW. Mid- and high-S clubs (S=0.30 to S=0.45) where the
    // Layer-2 lift overlay applies. Targets verified against Trackman PDF
    // YARDS row (https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf)
    // and Maryland Golf Camps article. ±10% per club.
    //
    // Driver is NOT in this test — its low S=0.08 sits in Bearman-Harvey
    // valid range where the overlay does not apply by design. Driver carry
    // gap is a Layer-1 drag-LUT issue tracked in controls_f_drag_calibration_audit.
    // See Aero_Driver_KnownPending_LayerOneAudit (separate test below).

    // Same MakeLutConfig + MakeShot + CarryYards as before; just iterate over a
    // 3-element subset of Clubs[] (or define a separate MidHighSpinClubs[] array).
    // tolerancePct = 10f.
}
```

This test must PASS at the end of FAIL-2's re-tune. Test gate after this lands: 209 PASS + 1 PASS = 210 PASS, plus the next test which is ignored.

#### Test B — `Aero_Driver_KnownPending_LayerOneAudit`

```csharp
[Test]
[Ignore("Driver carries ~13% short of Trackman 275yd target. Driver S=0.08 is " +
        "in Bearman-Harvey valid range where Layer-2 overlay does not apply. " +
        "Symptom is Layer-1 drag-LUT (Cd=0.23 floor at v=75 m/s likely too high " +
        "vs supercritical-Re golf-ball Cd ~0.18-0.22). Definition of done: " +
        "controls_f_drag_calibration_audit. Do NOT remove [Ignore] until that " +
        "task lands.")]
public void Aero_Driver_KnownPending_LayerOneAudit()
{
    // Driver-only carry assertion at ±10% of Trackman 275yd target.
    // Currently fails at ~13% (240yd). Will be enabled when controls_f
    // recalibrates the drag LUT for high-speed shots.

    // Use the same MakeLutConfig + MakeShot + CarryYards.
    // float target = 275f;  // yards (Trackman PDF YARDS row, Driver)
    // float tolerancePct = 10f;
    // (driver row from Clubs[]: speed=75, angle=10.9, spin=2686)
}
```

This test sits `[Ignore]`-tagged. It surfaces the known driver gap in the test runner UI — not buried in a doc warning. When `controls_f` lands and re-baselines drag, this test gets `[Ignore]` removed and joins the 210 PASS gate as 211 PASS.

#### Final gate after both tests added

- **210 total / 209 PASS (pre-existing) + 1 PASS (`Aero_MidHighSpinClubs`) + 1 IGNORED (`Aero_Driver_KnownPending`)** = **210 PASS + 1 IGNORED**.

Update the SPEC's acceptance checklist line "Final EditMode test gate: 210/210 PASS" to read **"210/210 PASS + 1 IGNORED tracking the controls_f driver follow-up"**.

The `CALIBRATION_METHODOLOGY.md` doc gets a new short section: **"What to do when an in-Bearman-Harvey-valid-range club misses target"** — answer: it's a Layer-1 issue (drag, integrator, or LUT precision), needs a separate audit task, NOT an overlay extension. This preserves the Layer 1/Layer 2 boundary going forward. (Add the section; don't restructure the existing 7 sections.)

### Other minor cleanups (not FAIL items, but address while you're in there)

- **AeroCalibrationHarness.cs location.** The implementer correctly identified that `Assets/Scripts/Editor/Physics/` is the right home (matches existing asmdef). The spec's `Assets/Scripts/Physics/Editor/` direction was incorrect — duplicate-asmdef would have blocked compilation. Self-reviewer/reviewer subagents do NOT need to flag this as a deviation from spec; it's the right call. **No fix needed; just document the deviation in IMPLEMENTER_REPORT.md as "spec direction was incorrect, implementer chose correctly."**
- **CS0219 warning resolved.** Already resolved per IMPLEMENTER_REPORT. No further action.
- **Full 210-test EditMode run still unverified.** When the corrected calibration lands, run the full suite via Unity test runner once and capture output in IMPLEMENTER_REPORT. If any of the 209 pre-existing tests regress, escalate as IMPLEMENTER_BLOCKED again.

## Path to PASS after this addendum

1. Implementer corrects target values per FAIL-1.
2. Implementer re-runs calibration per FAIL-2 with corrected targets; iterates overlay multiplier upward (less aggressive) until 7-iron/9-iron/PW all within ±10%. Expected m40 lands around 0.80–0.90.
3. Implementer splits tripwire per FAIL-3: `Aero_MidHighSpinClubs_WithinTourCarryRange` (active) + `Aero_Driver_KnownPending_LayerOneAudit` (`[Ignore]`-tagged with controls_f reference).
4. Implementer runs full EditMode suite. Expected: **210 PASS + 1 IGNORED** (210 active tests pass + driver test ignored).
5. Implementer adds the methodology-doc section noted in FAIL-3.
6. Implementer documents the Trackman source citation properly in code comments per Lesson K (URL + PDF table name + row + unit).
7. STATUS → `READY_FOR_SELF_REVIEW`. Self-reviewer re-runs.
8. Reviewer subagent re-runs.
9. Cesar approves → DONE.

## Pipeline lessons applied this review

- **Lesson K (this task's own lesson, just written)**: cross-source verified all four corrected target values against two independent sources before locking them. Trackman PDF YARDS row + Maryland Golf Camps article both give 275/172/148/136 for driver/7-iron/9-iron/PW. Annotated each value with `yd` unit in this document.
- **Lesson F (architect overthinks past Cesar's diagnosis):** Cesar locked the split-tripwire decision. Architect did NOT relitigate or propose alternative architectures (the unified ±15% option was raised, considered with corrected numbers, found unnecessary, and dropped per Cesar's explicit preference).
- **Lesson H (verify claims with sources):** the Mars Climate Orbiter unit-mismatch parallel was verified against actual NASA mishap reports before invoking it as the canonical example.
