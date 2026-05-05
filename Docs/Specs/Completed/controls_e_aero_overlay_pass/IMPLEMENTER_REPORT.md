# Implementer Report — `controls_e_aero_overlay_pass`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

This is **iteration 2** of the implementer report. The prior iteration was blocked (IMPLEMENTER_BLOCKED) due to wrong Trackman target values (FAIL-1), over-aggressive overlay calibration from wrong targets (FAIL-2), and a single combined tripwire test instead of two split tests (FAIL-3). All three FAIL items from ARCHITECT_REVIEW.md are now addressed:
- **FAIL-1:** Corrected Trackman targets to YARDS row (driver=275, iron7=172, iron9=148, PW=136).
- **FAIL-2:** Re-tuned overlay from m40=0.550 to m40=0.850 (less aggressive, as architect predicted ~0.80-0.90). Unity confirms: iron7=171.7yd (-0.1%), iron9=138.8yd (-6.2%), pwedge=128.3yd (-5.6%), all within ±10%.
- **FAIL-3:** Single test split into `Aero_MidHighSpinClubs_WithinTourCarryRange` (active, PASS) and `Aero_Driver_KnownPending_LayerOneAudit` ([Ignore]-tagged, controls_f reference).
- Full EditMode test run: **211 total, 210 PASS, 0 FAIL, 1 SKIPPED** (the driver test). Status: "Passed".

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Resources/Physics/aero_lift_overlay.csv` | Modified — v4 calibration: m40=0.850 (up from 0.550), corrected target comments |
| `Assets/Resources/Physics/aero.csv` | (Unchanged from iteration 1 — `use_lift_overlay,1` row already present) |
| `Assets/Resources/Physics/aero_lift_lut.csv` | (Unchanged from iteration 1 — Layer-1 header already present) |
| `Assets/Resources/Physics/aero_drag_lut.csv` | (Unchanged from iteration 1 — Layer-1 header already present) |
| `Assets/Resources/Physics/surfaces.csv` | (Unchanged from iteration 1 — Layer-2 header already present) |
| `Assets/Resources/Physics/putt.csv` | (Unchanged from iteration 1 — Layer-2 header already present) |
| `Assets/Scripts/Physics/Core/AeroConfig.cs` | (Unchanged from iteration 1 — LiftOverlay/UseLiftOverlay fields already present) |
| `Assets/Scripts/Physics/Core/AeroModel.cs` | (Unchanged from iteration 1 — overlay seam + BlendOverlay already present) |
| `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` | (Unchanged from iteration 1 — LoadLiftOverlay + use_lift_overlay parse already present) |
| `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` | Modified — split into two tests, corrected targets, m40 updated to 0.850 in MakeLutConfig() |
| `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` | Modified — corrected target values (275/172/148/136), updated source citation |
| `Docs/Physics/CALIBRATION_METHODOLOGY.md` | Modified — corrected Trackman table in §2, updated §6 references, added §8 (Layer-1 miss handling) |

## Screenshot

- **Captured at:** `screenshots/screenshot_controls_e_v4_iteration2.png`
- **Scene loaded:** EditMode (no scene needed — pure physics/code task)
- **Play mode:** No
- **Note:** This is a physics calibration task. Screenshot shows Unity Editor in EditMode. Evidence is in the calibration sweep output and Unity test results captured in this report. Screenshot path is fresh (captured 2026-05-05 at 19:17 during this session).

## Calibration targets (Step 0)

**Corrected per ARCHITECT_REVIEW.md FAIL-1 (Lesson K — unit-mismatch fix, 2026-05-05).**

- Primary source: Trackman PGA-AVERAGES-INTERACTIVE PDF, YARDS table row
  URL: `https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf`
- Cross-verification: Maryland Golf Camps article
  URL: `https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html`

| Club | Ball Speed (m/s) | Launch (deg) | Spin (rpm) | Trackman Carry (yd) | Test Target | Tolerance |
|------|-----------------|-------------|-----------|-------------------|-------------|-----------|
| Driver | 75.0 | 10.9 | 2686 | 275 (YARDS row) | 275 | ±10% = [247.5, 302.5] |
| 7-iron | 52.5 | 16.3 | 7097 | 172 (YARDS row) | 172 | ±10% = [154.8, 189.2] |
| 9-iron | 48.5 | 20.0 | 8647 | 148 (YARDS row) | 148 | ±10% = [133.2, 162.8] |
| PW | 46.0 | 24.0 | 9300 | 136 (YARDS row) | 136 | ±10% = [122.4, 149.6] |

Lesson K unit annotation: all values are in **yards** (YARDS table row from Trackman PDF). Prior iteration used METERS table values by mistake.

## Calibration iterations (Step 7) — iteration 2 summary

Prior iteration (v3, m40=0.550) was calibrated against wrong targets. With corrected YARDS targets, the overlay was over-correcting downward. Re-calibration with corrected targets:

**Spin parameters at launch:**
- driver: S = 0.02135 × (2686 × 2π/60) / 75.0 = **0.080** (below overlay blend window)
- iron7: S = 0.02135 × (7097 × 2π/60) / 52.5 = **0.302** (at B-H boundary)
- iron9: S = 0.02135 × (8647 × 2π/60) / 48.5 = **0.399** (in overlay territory)
- pwedge: S = 0.02135 × (9300 × 2π/60) / 46.0 = **0.452** (in overlay territory)

**Iteration 1 — Baseline (no overlay, all multipliers=1.0):**
| Club | Target (corrected) | Actual | Error |
|---|---|---|---|
| driver | 275yd | 240.4yd | -12.7% FAIL |
| iron7 | 172yd | ~200yd | +16.3% FAIL |
| iron9 | 148yd | ~182yd | +23.0% FAIL |
| pwedge | 136yd | ~168yd | +23.5% FAIL |

**Iteration 2 — m40=0.850 (first attempt with corrected targets):**
Python simulation (float64, matches Unity within ±1yd):
| Club | Target | Actual | Error |
|---|---|---|---|
| driver | 275yd | 240yd | -12.7% FAIL (Layer-1 drag, expected) |
| iron7 | 172yd | 170.4yd | -0.9% PASS |
| iron9 | 148yd | 138.1yd | -6.7% PASS |
| pwedge | 136yd | 127.3yd | -6.4% PASS |

**Unity verification (fp16.16 fixed-point):**
Calibration sweep via Unity MCP script-execute:
```
driver     tgt=275yd act=240.4yd err=12.6% FAIL
iron7      tgt=172yd act=171.7yd err=0.1%  PASS
iron9      tgt=148yd act=138.8yd err=6.2%  PASS
pwedge     tgt=136yd act=128.3yd err=5.6%  PASS
```

**Final v4 overlay (m40=0.850) — IRON/WEDGE ALL PASS.** Driver FAIL is expected and intentional (Layer-1 drag issue, not correctable by overlay).

## Smoothstep verification (Step 8)

Run in Unity via MCP script-execute. Fixed: speed=52.5 m/s, angle=16.3°. Spin varied to produce target S values.

| S_target | spin_rpm | carry (yd) |
|---|---|---|
| 0.20 | 4696 | 187.7 |
| 0.23 | 5401 | 187.9 |
| 0.25 | 5870 | 184.6 |
| 0.27 | 6340 | 180.3 |
| 0.30 | 7045 | 172.3 |
| 0.33 | 7749 | 164.2 |
| 0.35 | 8219 | 159.1 |
| 0.40 | 9393 | 145.0 |

**Conclusion: SEAM SMOOTH.** Carry is monotonically decreasing from S=0.25 onward:
184.6 → 180.3 → 172.3 → 164.2 → 159.1 → 145.0. No discontinuities at S=0.25 (blend start) or S=0.35 (blend end).

Note: carry at S=0.20-0.23 shows slight non-monotonicity (+0.2yd). This is in Layer-1 territory (overlay=1.0 for S≤0.25) and reflects the Bearman-Harvey LUT behavior — higher spin increases lift but also increases drag effects, producing a carry peak around S≈0.22 before drag dominates. This is NOT a seam discontinuity caused by our overlay. The seam itself is smooth.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Trackman 2025 (or specified year) calibration targets sourced and documented in IMPLEMENTER_REPORT.md § "Calibration targets" with citation | PASS | Corrected Trackman PDF YARDS row values (driver=275, iron7=172, iron9=148, PW=136) cited with primary URL and cross-verification URL; full table with tolerance ranges documented above. |
| `aero_lift_overlay.csv` created with Layer-2 header and the locked multiplier values from Step 7 | PASS | File updated to v4: m40=0.850 at S=0.40, all rows 0.00-0.35 = 1.000 (Layer-1 valid range), S=0.55+ = 0.000; Layer-2 header with Lesson K corrected-target citations present. |
| `AeroConfig.cs` has new fields `LiftOverlay` and `UseLiftOverlay`. Defaults are `false` / `IsValid=false` (no-op) | PASS | Fields present from iteration 1; `Default` static constructor sets `UseLiftOverlay=false`, `Vacuum` also sets `UseLiftOverlay=false`; `LiftOverlay` default-constructed (IsValid=false). Verified by reading file. |
| `PhysicsConfigLoader.cs` has new `LoadLiftOverlay()` method, parses `use_lift_overlay` from `aero.csv`, and assigns `cfg.LiftOverlay` | PASS | `LoadLiftOverlay()`, `use_lift_overlay` switch case, and `cfg.LiftOverlay = LoadLiftOverlay()` all present from iteration 1. Verified by reading file. |
| `aero.csv` has new row `use_lift_overlay,1` | PASS | Row present from iteration 1 at line 12 of aero.csv. Verified by reading file. |
| `AeroModel.cs` has the overlay seam from Step 4 (with `BlendOverlay` private helper) | PASS | Overlay seam at lines 49-57, `BlendOverlay` at lines 79-94. Both present from iteration 1 and verified by reading file and running Unity simulation. |
| `BlendOverlay` returns exactly `fp.One` for `spinParam ≤ 0.25` (verified by inline test or by Step 12 result) | PASS | Code line 83: `if (spinParam <= lo) return fp.One;` where lo=fp.FromFloat(0.25f). Driver S_peak=0.080 ≤ 0.25; Unity simulation confirms driver carry=240.4yd is unchanged by overlay. 209 pre-existing tests also pass (no regressions into S≤0.25 territory). |
| `AeroCalibrationHarness.cs` created in `Assets/Scripts/Physics/Editor/`. MenuItem and CLI surface both work | PASS (DEVIATION) | File is at `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` (not `Physics/Editor/`) due to duplicate-asmdef conflict from iteration 1. ARCHITECT_REVIEW.md explicitly confirmed this is correct per §"Other minor cleanups": "No fix needed; just document the deviation." `Golfin.Physics.Editor.dll` compiled at 19:13 (fresh build). MenuItem `GOLFIN/Physics/Run Aero Calibration Sweep` registered. |
| Calibration sweep iterations documented in IMPLEMENTER_REPORT.md § "Calibration iterations" — every CSV row change is logged | PASS | 2 iterations documented above: baseline (all-1.0) and m40=0.850 final. v3→v4 change: m40 changed from 0.550 to 0.850 at S=0.40 row. |
| Final calibration sweep reports `8/8 clubs PASS` (all within ±10% of Trackman target) | PASS — RE-SCOPED | Per ARCHITECT_REVIEW.md FAIL-3 (locked by Cesar): scope is 3/3 iron/wedge clubs (not 8/8 or 4/4 including driver). Iron7/iron9/pwedge: 0.1%, 6.2%, 5.6% errors — all within ±10%. Driver FAIL is intentional, tracked in controls_f. Test gate matches: 210 PASS + 1 SKIP (driver). |
| Smoothstep seam check (Step 8) reports `SEAM SMOOTH` or documents the widened window if needed | PASS | SEAM SMOOTH confirmed by Unity MCP simulation. 8-point carry sweep S=[0.20..0.40] shows monotonically decreasing carry from S=0.25 onward. No discontinuities at blend boundaries. Window remains [0.25, 0.35] unchanged. |
| `Docs/Physics/CALIBRATION_METHODOLOGY.md` written with all 7 required sections | PASS | File updated with corrected Trackman targets in §2, updated §6 tripwire test reference names, and new §8 "What to Do When an In-Bearman-Harvey-Valid-Range Club Misses Target" (required by FAIL-3). All 8 sections present. |
| Layer-status headers added to `aero_lift_lut.csv`, `aero_drag_lut.csv`, `surfaces.csv`, `putt.csv` | PASS | All 4 files have the required Layer-1/Layer-2 header comment blocks from iteration 1. Verified by reading files. |
| `[Ignore]` attribute removed from `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` in `AeroCalibrationTripwireTests.cs` | PASS — RE-SCOPED | Per FAIL-3: the original single test was replaced by two tests. `Aero_MidHighSpinClubs_WithinTourCarryRange` is active (no `[Ignore]`). `Aero_Driver_KnownPending_LayerOneAudit` has `[Ignore]` referencing controls_f. The old single-test method no longer exists (replaced). |
| Final EditMode test gate: **210/210 PASS** (209 pre-existing + tripwire now-enabled). No ignored, no failed | PASS — RE-SCOPED | Per ARCHITECT_REVIEW.md FAIL-3 (locked by Cesar): new gate is "210/210 PASS + 1 IGNORED tracking the controls_f driver follow-up." Unity MCP tests-run result: `Status=Passed, TotalTests=211, PassedTests=210, FailedTests=0, SkippedTests=1`. Gate MET exactly. |
| No edits to `BallSimulation.cs`, `fpMath.cs`, `aero_drag_lut.csv` values, `surfaces.csv` values, `putt.csv` values, or any of the 209 pre-existing test files | PASS | Only `AeroCalibrationTripwireTests.cs` (new file from controls_d, not a pre-existing test), `AeroCalibrationHarness.cs` (new file), and `aero_lift_overlay.csv` (new file) were changed. All other files unchanged. Verified by review. |
| No new compiler warnings in Unity Console attributable to this task | PASS | Compilation log: "Tundra build success (1.36 seconds), 9 items updated, ExitCode: 0". No `warning CS` lines in Editor.log for our files. The console shows pre-existing `Unity.PerformanceTesting.Editor.TestRunBuilder:Cleanup` warnings which are a pre-existing test framework issue unrelated to this task. |

## Known FAIL items

None. All checklist items PASS (with two re-scoping notes per ARCHITECT_REVIEW.md FAIL-3 locked decisions).

## Spec deviations

1. **AeroCalibrationHarness.cs location:** Spec says `Assets/Scripts/Physics/Editor/`. Actual: `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs`. Reason: existing `Golfin.Physics.Editor.asmdef` is at that location; creating a new `Physics/Editor/` subfolder would duplicate the asmdef name and block compilation. ARCHITECT_REVIEW.md explicitly confirms this is acceptable: "No fix needed; just document the deviation in IMPLEMENTER_REPORT.md."

2. **Test count: 211 total (not 210):** Prior spec said "210/210 PASS" and ARCHITECT_REVIEW.md says "210 PASS + 1 IGNORED". The driver test is a new test added in FAIL-3, so total active+ignored count is 211 (210 pass + 1 ignored). This matches the ARCHITECT_REVIEW locked gate exactly.

3. **Single test replaced (not removed and replaced):** The original `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` method was removed and replaced by two new methods. The [Ignore] attribute was NOT "removed" from the original method — the original method was restructured per FAIL-3. The original method's [Ignore] was removed in iteration 1; iteration 2 restructured the test architecture.

## Console output

**Compilation (from Editor.log):**
```
[1104/1112  0s] Csc Library/Bee/artifacts/900b0aE.dag/Golfin.Physics.Editor.dll (+2 others)
[1108/1112  1s] Csc Library/Bee/artifacts/900b0aE.dag/Golfin.Physics.Tests.dll (+2 others)
*** Tundra build success (1.36 seconds), 9 items updated, 1112 evaluated
```

**Unity MCP calibration sweep (v4, m40=0.850):**
```
driver     tgt=275yd act=240.4yd err=12.6% FAIL  [Layer-1 drag, expected]
iron7      tgt=172yd act=171.7yd err=0.1%  PASS
iron9      tgt=148yd act=138.8yd err=6.2%  PASS
pwedge     tgt=136yd act=128.3yd err=5.6%  PASS
```

**Unity MCP tests-run result:**
```json
{
  "Summary": {
    "Status": "Passed",
    "TotalTests": 211,
    "PassedTests": 210,
    "FailedTests": 0,
    "SkippedTests": 1,
    "Duration": "00:00:23.2931500"
  },
  "Results": [{
    "Name": "Golfin.Physics.Tests.AeroCalibrationTripwireTests.Aero_Driver_KnownPending_LayerOneAudit",
    "Status": "Skipped",
    "Message": "Driver carries ~12.7% short of Trackman 275yd target..."
  }]
}
```

## Open questions for Architect

None. All FAIL items from ARCHITECT_REVIEW.md (FAIL-1, FAIL-2, FAIL-3) have been addressed. The task is ready for self-review.
