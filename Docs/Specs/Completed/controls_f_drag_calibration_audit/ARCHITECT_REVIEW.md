# Architect Review — `controls_f_drag_calibration_audit`

> Written by `golfin-reviewer` subagent (final review pass). Reads `SPEC.md`, `IMPLEMENTER_REPORT.md`, the modified source files, and the broader project context. Final gatekeeper before Cesar sees the work.

**Reviewed:** 2026-05-05 (evening) JST
**Reviewer:** Claude (golfin-reviewer)
**Gate:** This task escalated directly to ARCHITECT_REVIEW (no SELF_REVIEW.md present) because the implementer session lacked Unity MCP and could not run the EditMode test suite. The implementer correctly marked the test-gate checklist item as FAIL and surfaced it as a known gap.

## Verdict

**PASS — with one mandatory pre-approval verification step Cesar must perform manually.**

The architectural work is correct, complete, and matches the spec. The only unverified item is the literal `211/211 PASS, 0 IGNORED` test-runner outcome, which the implementer could not execute (no Unity MCP in their session) and which I (also a no-MCP review session) cannot execute either. **Cesar must run `Window > General > Test Runner > EditMode > Run All` and confirm the green count before moving the task to Completed/.** This is a 30-second click; everything required for it to pass is in place.

If the test suite fails, route back to implementer with the specific failing test names — but the architectural surface, the calibration evidence, and the code structure all predict PASS.

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries respected | PASS | All edits live in `Golfin.Physics` (Core), `Golfin.Physics.Runtime`, `Golfin.Physics.Editor`, and `Golfin.Physics.Tests` — same assemblies touched by `controls_e`. No new asmdefs, no autoref additions, no new cross-asm refs. |
| Layer-1/Layer-2 separation preserved | PASS | `aero_drag_lut.csv` values unchanged (only header updated). New `aero_drag_overlay.csv` is the sole place behavior was changed. The two-layer methodology in `CALIBRATION_METHODOLOGY.md` §1 stays clean. |
| Pattern adherence — mirrors lift overlay structure | PASS | `BlendDragOverlay` is a verbatim structural twin of `BlendOverlay` (only constants and parameter name change: 0.25/0.35/spinParam → 45/55/speed). `LoadDragOverlay` mirrors `LoadLiftOverlay`. `DragOverlay`/`UseDragOverlay` mirror `LiftOverlay`/`UseLiftOverlay`. Symmetry is exactly what the spec asked for. |
| No duplicated logic | PASS | No copy-pasted blocks beyond the unavoidable mirror of `BlendOverlay`. The `LoadLut` private helper is correctly reused. The smoothstep formula is duplicated (intentional — separate seam constants for lift S vs drag v); refactoring to a shared helper would require parameterising both anchor points and a name, and would obscure rather than clarify. |
| Implementation matches spec **intent** (not just letter) | PASS | Spec asked for: a Cd multiplicative overlay, smoothstep at v∈[45,55], opt-in via CSV, irons unaffected, driver into ±10%. Delivery: exactly that. The chosen multipliers (v60=0.920, v70=0.890, v80=0.880) sit within the spec's predicted "around 0.90 at v=80" envelope. |
| Cross-feature impact (other tests still green) | PARTIAL | I verified by inspection that `AerodynamicsTests.MakeLutConfig()` does NOT set `UseDragOverlay = true`, so its 263yd / 199yd / 180yd / 168yd snapshotted carries are unaffected by this task. `AeroCalibrationTripwireTests.MakeLutConfig()` was correctly updated to enable the overlay (see lines 124-129, 135, 139). Iron speeds (46–52 m/s) graze the seam; smoothstep math at v=52.5 yields ≈0.983 effective multiplier — Cd reduction ~1.7% at launch only, decaying as flight progresses. The implementer's Python sim shows iron carries change by <0.2 yd. Plausible and consistent. **Literal Unity test runner pass count not verified — see "Specific gap" below.** |
| Latent issues — null refs, load order | PASS | `LoadDragOverlay` returns `default(CoefficientLut)` if the CSV is missing, and `default.IsValid` is `false`, so the `cfg.UseDragOverlay && cfg.DragOverlay.IsValid` guard in `AeroModel.cs` line 41 short-circuits cleanly. If `aero_drag_overlay.csv` is ever deleted or `use_drag_overlay,1` is removed from `aero.csv`, behavior degrades safely to Layer-1-only. |
| `BlendDragOverlay` returns exactly `fp.One` for `speed ≤ 45` | PASS | Verified by inspection at `AeroModel.cs:120` — `if (speed <= lo) return fp.One;` is the only path executed for that range. No fp arithmetic in the short-circuit means no rounding error possible. |
| CSV header updated on `aero_drag_lut.csv` (Step 11) | PASS | Header now reads "Layer 2 overlay applied: see aero_drag_overlay.csv and CALIBRATION_METHODOLOGY.md §9." Values unchanged. |
| Methodology doc §9 added, §8 closed | PASS | §9 is a faithful drag-side mirror of §3 (lift): architecture, trigger conditions, smoothstep math, worked example, recalibration triggers. §8 closes the open follow-up with the right cross-reference to §9. |
| `[Ignore]` removed from `Aero_Driver_KnownPending_LayerOneAudit` | PASS | `AeroCalibrationTripwireTests.cs:204` now has `[Test]` only. Docstring updated to reflect controls_f resolution. Test source is consistent — `MakeLutConfig` includes the drag overlay, so the test exercises the post-overlay physics. |
| `aero.csv` enables overlay | PASS | New row at line 13: `use_drag_overlay,1,bool,1=Layer-2 drag overlay active 0=overlay disabled`, placed adjacent to `use_lift_overlay,1` per spec. |
| Smoothstep seam smooth (no kink) | PASS | Implementer's 9-point seam table (v=40..60 m/s) shows monotonically increasing carry with rate-of-change decreasing smoothly from +5.59 to +4.90 yd/(m/s). No kink at v=45 or v=55. Math also confirms continuity: at v=45 t=0 and at v=55 t=1, smoothT(0)=0 and smoothT(1)=1 → multiplier = 1.0 and = overlayMultiplier respectively, matching the short-circuit branches exactly. |

## Specific gap (the one unverified item)

**The implementer could not run `Window > General > Test Runner > EditMode > Run All`** — Unity MCP was unavailable in their session. Their evidence for "211/211 PASS" is:

1. Python float simulation shows 4/4 calibration clubs PASS at the new multipliers (driver=249yd, 9.5% error vs 275yd target).
2. controls_e established that the Python sim agrees with Unity fp arithmetic to within ±1 yd.
3. Code review confirms no syntax errors and correct namespace/using directives.
4. The 210 pre-existing tests use `AerodynamicsTests.MakeLutConfig()` which does NOT enable the overlay — so their snapshotted carries are mathematically unchanged.
5. The `Aero_MidHighSpinClubs_WithinTourCarryRange` test (3 irons) was already passing pre-task; the smoothstep ensures overlay effect on irons is ≤2% of Cd at launch and decays during flight.

This evidence is reasonable but is not a substitute for the actual test-runner result. **Cesar: please run the EditMode suite once before moving this folder to Completed/.** Expected outcome: `211 PASS, 0 FAIL, 0 SKIPPED`. If the count differs:

- `211 PASS, 0 SKIPPED` — green-light, move to Completed/.
- `210 PASS + 1 FAIL on Aero_Driver_KnownPending_LayerOneAudit` — the fp arithmetic diverged from Python and the driver lands outside ±10%. Loop back to implementer to nudge multipliers (try v80=0.870 or v70=0.880).
- Any other test failing — the overlay is leaking into Layer-1-valid territory. Set STATUS to `IMPLEMENTER_BLOCKED` per spec § "Mid-task escalation paths."

I am taking this PASS with the explicit caveat above because (a) the implementer cannot redo the test run without MCP, (b) re-bouncing this would be a no-op loop, and (c) Cesar performs final approval anyway and this gives him exactly one extra click as the verification gate.

## Spec deviations worth flagging (minor, non-blocking)

1. **New file `Assets/Scripts/Editor/Physics/RunHarnessMenuItem.cs`** is not in the spec's "Files this task touches" table. The implementer added it as a temporary verification utility (writes harness output to a file). It's an Editor-only `[MenuItem]`, no runtime behavior, no test coupling. The implementer's report flags it for deletion. Recommendation: Cesar deletes it after running Test Runner once, or the implementer deletes it as part of any follow-up touch. Not worth a FAIL bounce; not worth shipping permanently.

2. **Spec says "8/8 clubs PASS" but the harness has 4 clubs.** This is a spec wording error inherited from controls_e (where the calibration set was already 4 clubs). The implementer correctly reported 4/4 PASS and flagged the discrepancy in `IMPLEMENTER_REPORT.md § Spec deviations`. Architect agrees: 4/4 is the actual gate. Future spec edits should drop the "8" reference.

3. **`AeroConfig.cs` comments are slightly redundant.** Line 47 says "DragLut / LiftLut / LiftOverlay / DragOverlay default-constructed" and then lines 52 and 54 add separate `LiftOverlay`/`DragOverlay` defaulted comments. Not wrong; just verbose. Cosmetic only.

4. **`ViewerTest.Viewer_DriverCalm_CarryInExpectedRange` headroom** — implementer notes driver_calm carry rose from 220.2m to 227.6m, still under the 230m upper bound but only 2.4m of headroom. Worth tracking for any future Layer-2 adjustments; future spec authors should consider widening the upper bound or splitting the test.

## Latent issues / things to watch in future

- **The `[45, 55]` seam is asymmetric to driver flight.** Driver spends ~61% of flight above v=55 (full overlay) and ~12% in the seam zone. Late-flight near apex, ball drops to ~30 m/s where overlay = 1.0 (no effect). This is correct behavior and matches the spec's design, but means the overlay's effective magnitude on driver carry is roughly 0.88–0.92× Cd weighted over ~73% of flight time, which lines up with the empirical ~3.5% carry gain (240→249 yd ≈ +3.7%).
- **Trackman re-validation trigger** is documented in §9 (re-run when Trackman publishes a new annual or carry target moves >3yd). Good. But there is no automated alarm — this is a human-monitored trigger. Acceptable for now; consider adding a "last verified date" field to `aero_drag_overlay.csv` in a future cleanup pass so reviewers can see staleness at a glance.
- **No explicit unit test for `BlendDragOverlay` boundary behavior.** The implementer relied on the integrated tripwire test for coverage. Consider adding a focused unit test in a future task: `BlendDragOverlay(44, 0.85) == 1.0`, `BlendDragOverlay(56, 0.85) == 0.85`, `BlendDragOverlay(50, 0.85) == 1.0 + (-0.15) × 0.5 = 0.925`. Not a blocker — controls_e shipped without one too.

## Capture-helper protocol (Step 5 backstop)

Spec describes a pure physics/code task with no scene mutation, no playmode, no screenshots. There is no fake-state extension to verify. Capture-helper compliance is N/A. PASS by inapplicability.

## Lessons captured

For `tasks/lessons.md` if this lands:

- **Two-layer overlay pattern works for both lift and drag.** Mirroring is fast (controls_e → controls_f delta is ~1 day of architect+implementer work, mostly because controls_e already paved the path). Future overlays (e.g., wind-cut, surface-friction at low speed) can use the same template: `<axis>_overlay.csv` + `Use<Axis>Overlay` flag + `Blend<Axis>Overlay` smoothstep helper + a CSV-loaded multiplier table.
- **When implementer lacks Unity MCP, surface a manual verification gate to Cesar — don't pretend.** This task's IMPLEMENTER_REPORT.md was textbook honest about the gap. Architect-review preserves that honesty by routing PASS-with-caveat rather than ESCALATE-to-avoid-deciding or FAIL-to-bounce-no-progress.
- **`AerodynamicsTests` and `AeroCalibrationTripwireTests` have parallel `MakeLutConfig` methods.** They diverged intentionally (one snapshot-tests current physics; the other is the calibration tripwire with fresh Trackman targets). Future overlay additions must update BOTH or explicitly justify why only one is touched. controls_f correctly updated only the tripwire one because the snapshot tests use overlay=false by design.

## Cesar's final approval

Cesar fills this section after running `Window > General > Test Runner > EditMode > Run All` once.

- [ ] EditMode test suite reports `211 PASS, 0 SKIPPED` — green-light to move folder to `Docs/Specs/Completed/`.
- [ ] Test suite shows different counts — paste the failing test names below and route back via `CESAR_REJECTION.md` or set STATUS to `IMPLEMENTER_BLOCKED`.
- [ ] Approved.
- [ ] Rejected — reason: <...>

(Optional cleanup: delete `Assets/Scripts/Editor/Physics/RunHarnessMenuItem.cs` after verification — it's a temporary utility per the implementer's note.)

## Test Runner Verification (2026-05-06)

**Attempted by:** `golfin-reviewer` subagent (follow-up pass at Cesar's request).

**Outcome:** UNABLE TO EXECUTE — `mcp__ai-game-developer__tests-run` is not in this subagent's provisioned tool set.

**Diagnosis:** This is NOT a transient transport drop. A transport drop would surface as a runtime error from an existing tool definition; the situation here is that the `mcp__ai-game-developer__*` tool family is not registered in the `golfin-reviewer` agent's tool list at all. The agent definition (`.claude/agents/golfin-reviewer.md`) explicitly limits the reviewer to read/write + Figma MCP, with the rationale "You do NOT have Bash, Edit (Unity scenes), or scene-modification tools. You don't run code; you review it." Both `mcp__ai-game-developer__scene-list-opened` and `mcp__ai-game-developer__tests-run` returned `No such tool available` — the consistent error means the tools were never plumbed in for this agent role, not that the MCP server is down.

**What this means for Cesar:** The manual verification gate documented in the verdict above is still required. The earlier reviewer was correct that the MCP tool was unavailable to it; the framing "MCP transport dropped" was the wrong diagnosis but the conclusion (cannot execute) was correct.

**Recommended path forward:** ONE of the following:

1. **Cesar runs Test Runner manually.** Window > General > Test Runner > EditMode > Run All. ~30 seconds. Expected: `211 PASS, 0 FAIL, 0 SKIPPED`. This is the original recommendation in the verdict above.
2. **Run the implementer subagent with a one-shot test-execution task.** The `golfin-implementer` agent definition does include `mcp__ai-game-developer__*` tools. A trivial task spec ("run EditMode tests, report counts, do not modify code") would clear the gate without needing Cesar's hands.
3. **Update `.claude/agents/golfin-reviewer.md`** to grant `mcp__ai-game-developer__tests-run` (read-only test execution) so future reviewer sessions can self-verify. This is a workflow improvement, not blocking for this task.

**Test Runner Verification: BLOCKED (tooling, not implementation).** The architectural PASS in the verdict above stands; the test-suite green light remains unverified pending one of the three paths above.

| Metric | Required | Observed |
|---|---|---|
| TotalTests | >= 211 | not measured |
| PassedTests | TotalTests - SkippedTests | not measured |
| FailedTests | 0 | not measured |
| SkippedTests | 0 (`[Ignore]` removed) | not measured |

## Test Runner Verification — Confirmed (2026-05-06)

**Run by:** one-shot agent (Bash → MCP HTTP direct call, session `XUHUau_vamOMuN6QxKKbpg`)
**Tool:** `tests-run` via `http://localhost:21573` (unity-mcp-server v0.69.0.0)
**Duration:** 23.2 seconds

| Metric | Required | Observed | Verdict |
|---|---|---|---|
| TotalTests | >= 211 | **211** | PASS |
| PassedTests | 211 | **211** | PASS |
| FailedTests | 0 | **0** | PASS |
| SkippedTests | 0 (`[Ignore]` removed) | **0** | PASS |
| Status | Passed | **Passed** | PASS |

**Acceptance: 211/211 PASS, 0 IGNORED — PASS**

The `Aero_Driver_KnownPending_LayerOneAudit` test (previously `[Ignore]`) now executes and passes. All 211 EditMode tests green. The pre-approval verification gate is cleared.
