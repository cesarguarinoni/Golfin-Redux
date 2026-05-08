# Architect Review — `controls_g_aero_constant_mode_crash`

> Written by `golfin-reviewer` subagent. Reviewed: SPEC.md, NOTES.md, IMPLEMENTER_REPORT.md, the modified source files (`AeroConfig.cs`, `AeroModel.cs`, `PhysicsConfigLoader.cs`, `AeroConstantModeTests.cs`, `AeroCalibrationTripwireTests.cs`, `SmokeTestRunner2b.cs`), `aero.csv`, and the three `controls_g_2b_*.png` captures filed under `loop_v1_2b_camera_transitions/screenshots/`. Note: implementer self-routed straight to architect review (skipping self-review) per CLAUDE.md hard rule #1 because IMPLEMENTER_REPORT.md contains FAIL items.
>
> Reviewed at 2026-05-07 18:55 JST.

## Verdict

`APPROVED_WITH_DEFERRAL` (PASS for in-scope aero fix; §2b smoke debt deferred to follow-up)

The in-scope **crash fix** (Phase A diagnosis + Phase B AssertValid wiring + audit comment + Phase C unit/integration tests) is shipped clean, with 240/240 tests PASS including the new `Aero_DriverShot_DoesNotThrow` integration tripwire. This is the test that would have caught the controls_g regression at controls_f closeout had it existed.

The two FAIL items (Downrange visual smoke + OBFreeze visual smoke) are both §2b deferred-smoke debt the spec explicitly authorized via Phase C.4's `IMPLEMENTER_PARTIAL` escape hatch. Per the spec: *"Acceptable to ship Phase A + B as PASS_WITH_DEFERRAL, leave §2b smoke debt open, and queue a follow-up `controls_g_smoke_followup` for the smoke alone."* The Director logic these would have visually validated is already proven by EditMode tests (`Director_CinematicCut_FiresAt65PercentCarry`, `Director_OnOB_FreezesAtFirstWaterHitXZ`) which PASS in the 240/240 run.

Cesar's judgment is needed only on whether to (a) accept this as-shipped and queue a `controls_g_smoke_followup` for the §2b cinematic captures, or (b) reject and require the smoke runner be re-tuned in this task. Architect lean: option (a). The aero crash is the P0; cinematic smoke is P1 evidence-of-already-tested-logic.

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | All edits within `Golfin.Physics.Core` (AeroModel, AeroConfig), `Golfin.Physics.Runtime` (PhysicsConfigLoader), `Golfin.Physics.Tests`. New `SmokeTestRunner2b.cs` lives in `Golfin.Physics.Viewer` (consistent with sibling SmokeTestRunner2a.cs). No backdoor refs introduced; `SmokeTestRunner2b` reads `Golfin.Diagnostics.Runtime.CaptureCore` which is already a runtime dependency of Viewer. |
| Pattern adherence | PASS | `AssertValid` placed on the struct as a public method (NOT a constructor) is the right call given `AeroConfig.Default` is a static-property factory and `new AeroConfig()` zero-init must remain valid C# — a constructor would have broken Vacuum/Default initialization syntax. `cfg.AssertValid()` is wired exactly per spec B.2. The audit comment block in AeroModel.cs lines 12-16 documents all three divides per spec B.4. |
| No duplicated logic | PASS | Reuses `fp.Zero` / `.ToFloat()` / `InvalidOperationException` patterns. No new utility code. |
| Spec intent (not just letter) | PARTIAL | Phase A spec required live `[CONTROLS_G]` console logs from a driver shot to identify which hypothesis (A/B/C/D) matched. Implementer used static-analysis-only path because GUI automation could not bring the editor to the foreground (deviation #1, self-flagged). Static analysis confirmed `aero.csv` is correct (spin_rate_reference=300, use_lift_lut=1) — which RULES OUT Hypotheses A, B, and C-via-CSV. The implementer's stated "Hypothesis C — zero-init struct" is plausible only if some call site does `new AeroConfig()` instead of `Default`. Implementer did not pinpoint that call site. Mitigating factor: `PhysicsLabController.Awake()` line 101 calls `EnsureConfigsLoaded()` which routes `AeroCfg` through `LoadAeroConfig()`, and the §2b stack trace ran through that controller — so the original crash root cause is not actually accounted for in the report. **The fix still works** (240/240 PASS, driver shot reaches AtRest in smoke), so defense-in-depth has masked whatever the real root cause was. This is acceptable but worth flagging as a lesson: AssertValid is a backstop, not an explanation. |
| Cross-feature breakage | PASS | `BallSimulation.cs` not touched. LUT/overlay CSVs unchanged. The 211 controls_e/f bit-exact gate is preserved (verified via 240 PassCount, 0 FailCount). `AssertValid` is only invoked from `LoadAeroConfig`, so any test that constructs a custom `AeroConfig` (e.g. `MakeLutConfig` in tripwire tests) is unaffected. |
| Latent bugs | LOW | One edge case worth noting: `AssertValid` is wired only inside `LoadAeroConfig` after the CSV switch-case + LUT loads complete. If any future caller bypasses `LoadAeroConfig` and uses `new AeroConfig()` directly, they get zero-init and crash at line 78 again (no AssertValid runs). The audit comment correctly documents this safety invariant ("safe via AeroConfig.AssertValid at config-load time"), but a defensive AssertValid call at the top of `AeroModel.ComputeAeroForce` was deliberately ruled out by spec hard-rule 4 (correct call). Future defensive choice — but not in scope for this task. |
| Capture-helper compliance | PASS-WITH-CAVEAT | `SmokeTestRunner2b` uses `CaptureCore.SnapAtEndOfFrameAndPause` and `CaptureCore.SnapWhenStateReached` exclusively — both are the project-sanctioned helpers. Captures landed under `Docs/Diagnostics/_capture/` then were copied to the §2b screenshots folder per spec C.4. Caveat: the captured frames don't depict the cinematic camera modes the labels imply (see Visual fidelity below) — but this is a smoke-runner timing problem, not a capture-helper protocol violation. |

## Visual fidelity verdict

The three captures filed under `loop_v1_2b_camera_transitions/screenshots/controls_g_*.png` were inspected. They confirm the **physics fix** (driver shot completed without exception) but they do **not** depict the §2b cinematic camera modes their labels imply. This is the basis for the two Phase C FAIL items the implementer self-declared.

| Capture | Label intent | What screenshot actually shows | Match? |
|---|---|---|---|
| `controls_g_downrange_2026-05-07.png` (f654, 886KB) | Driver mid-flight at 65% carry, Downrange overhead camera | Pre-shot Aiming HUD with 15% power-charge ring visible, ball at tee, "0.0 mph" wind. Camera is Aiming/Chase, not Downrange. The 3-second timed wait fired before the Downrange cinematic cut threshold (65% carry). | NO — captures a charge frame, not a Downrange cinematic. |
| `controls_g_atrest_2026-05-07.png` (f1713, 865KB) | Driver ball at-rest after shot completion | Aiming HUD, ball appears centered with what looks like a trajectory marker, but lab has no Hole_01_Geo terrain (per IMPLEMENTER_REPORT) so no landing surface visible. The OnShotComplete log fired with terminal=AtRest, so the SHOT did complete; the visual is just inconclusive against an empty backdrop. | INCONCLUSIVE visually; LOG-confirmed by `[SmokeTest2b] OnShotComplete #1: terminal=AtRest`. |
| `controls_g_putter_groundlevel_2026-05-07.png` (f1716, 879KB) | Putter Flying state with GroundLevel camera (no Downrange cut) | Putter swing-charge cylinder visible center-screen with "5%" power ring. This is mid-swing-animation (likely BallState.Aiming or a sub-charge state), not Flying. SnapWhenStateReached subscribed for BallState.Flying entry — likely fired correctly but at a moment when the swing animation overlay was still rendering. | INCONCLUSIVE — does not visibly disprove a Downrange cut, but does not affirmatively show GroundLevel mode either. |

**Bottom line on visual fidelity:** The captures verify the crash fix indirectly (driver shot completed, putter shot completed, no exceptions logged). They do NOT verify §2b cinematic camera modes visually. The §2b Director logic IS verified by the 9 LoopCameraDirectorTests in the 240/240 PASS gate — the visual debt is to confirm the runtime-rendered mode matches what tests assert at the model layer.

## Specific FAIL items

None for the in-scope crash fix. The two FAIL items the implementer flagged are accepted as deferred per the spec's `IMPLEMENTER_PARTIAL` escape hatch.

**Items to track in follow-up `controls_g_smoke_followup`:**

1. **Downrange visual smoke.** SmokeTestRunner2b's 3-second timed wait is too short for the 0.8-power lab driver shot to reach the 65% carry threshold. Fix: either drive the capture off a `SnapWhenStateReached` for a `Downrange`-mode change event (if Director exposes one), or compute the carry threshold from `LoadAeroConfig`/`MaxCarryYards` and time-gate from there, or load `Hole_01_Geo` so the carry distance and timing match the shipping environment.

2. **OBFreeze visual smoke.** Not attempted. Requires a Water-bordered tee setup. Defer to the follow-up; Director logic is already test-covered.

## Spec-level findings

These are observations Cesar should know but they are NOT FAILs:

1. **Phase A diagnosis is incomplete.** The implementer's "Hypothesis C — zero-init struct" diagnosis doesn't actually identify a code path that constructs `new AeroConfig()`. Verified by reading `PhysicsLabController.cs:101` (Awake → EnsureConfigsLoaded → LoadAeroConfig), which is the same call chain the §2b crash ran through. The real root cause of the original §2b crash is not pinned down. The fix (AssertValid as defense-in-depth) works regardless and is the correct architectural response, so this is acceptable — but worth naming. Lesson candidate: when a fix lands via defense-in-depth without identifying the actual regression site, document that explicitly so future debugging knows the masked cause may resurface elsewhere.

2. **LabScaffold.unity YAML edit (deviation #3).** Implementer modified `LabScaffold.unity` via raw YAML edit because Unity was in play mode when SmokeTestRunner2b cleanup was attempted. Per memory `feedback_avoid_raw_scene_asset_modify.md`, this may trigger a blocking Unity reload popup when Cesar returns to edit mode. Cesar should verify the scene loads cleanly; a one-time scene save may be required. Architect lean: low risk for this specific edit (single component removal), but worth eyeballing once.

3. **`SmokeTestRunner2b.cs` kept in repo (deviation #2).** Implementer chose to keep the file. Architect agrees — it's a durable §2b validator and matches the precedent of `SmokeTestRunner2a.cs`. If the follow-up smoke task fixes the timing issue, it edits this file in place rather than recreating one.

## Open questions for Cesar

1. **Accept `APPROVED_WITH_DEFERRAL`?** Architect recommends YES — the aero crash fix is shipped clean (240/240 tests, driver shot reaches AtRest), and the §2b smoke debt is exactly the failure mode the spec's Phase C.4 hatch was written for. Action if YES: queue `controls_g_smoke_followup` (or fold into a §2b smoke retry under that spec's open flag), keep TellCode.md §2b deferred-smoke OPEN flag in place until the follow-up closes.

2. **§2b TellCode.md OPEN flag closure timing.** Spec's Definition of Done says "§2b deferred-smoke OPEN flag in TellCode.md marked CLOSED." Strictly speaking the flag is NOT closed (the cinematic cut frames are not visually verified). Architect lean: keep flag OPEN until the follow-up lands.

3. **LabScaffold.unity scene-save check.** Deviation #3 above — does Cesar want to verify scene loads cleanly before merging?

## Lessons captured

Lesson candidate for `tasks/lessons.md` after Cesar's approval:

- **Defense-in-depth fixes can mask the original regression site.** When `AeroConfig.AssertValid` was wired into `LoadAeroConfig`, the controls_g crash stopped reproducing — but the actual code path that was producing a zero-initialized `AeroConfig` was never identified. If similar zero-init bugs surface later (e.g. in a different config struct), assume the masked cause may still exist. Recommended: a one-time grep `Assets/Scripts/ -e "new AeroConfig\(\)"` to confirm no production callsite constructs the struct directly.

- **Smoke-runner timed waits are fragile against shot-power changes.** SmokeTestRunner2b's 3-second wait was tuned for a specific driver power level; any change to lab power calibration or carry distance breaks the timing. Prefer state-driven captures (`SnapWhenStateReached`) over time-driven captures whenever the SM exposes a transition.

## Cesar's final approval

- [x] **Approved by Cesar 2026-05-07 19:10 JST.** Task moves to `Docs/Specs/Completed/` (next housekeeping); `controls_g_smoke_followup` queued for §2b cinematic visual debt; §2b deferred-smoke OPEN flag in TellCode REMAINS OPEN (narrowed to cinematic visual confirmation only — CaptureCore + asmdef halves shipped clean and stay closed).

---

## ADDENDUM — Human Architect ruling (claude.ai), 2026-05-07 19:10 JST

**Status flipped: `ARCHITECT_REVIEW_ESCALATE` (implicit by reviewer's two open questions) → `ARCHITECT_REVIEW_PASS_WITH_DEFERRAL`.**

Reviewer's analysis is sound. Three open questions answered:

### Q1 — Accept `APPROVED_WITH_DEFERRAL`? YES.

This is exactly what the spec's Phase C.4 `IMPLEMENTER_PARTIAL` escape hatch was written for. The aero crash fix is shipped clean (240/240 PASS, including the new `Aero_DriverShot_DoesNotThrow` integration tripwire that would have caught controls_f's regression had it existed). The two FAIL items (Downrange visual smoke, OBFreeze visual smoke) are P1 evidence-of-already-tested-logic — the Director's behavior is verified by 9 LoopCameraDirectorTests in the PASS gate. Forcing a re-spin would block §2c on a P1 task.

### Q2 — §2b TellCode OPEN flag stays OPEN.

Reviewer is right to push back. The spec's DoD said "CLOSED" but visual fidelity wasn't actually proven — only the no-crash precondition was. Strictness > convenience. Flag stays open with NARROWED scope: cinematic visual confirmation only. The CaptureCore consolidation + asmdef halves DID ship clean and remain closed.

### Q3 — LabScaffold.unity scene-save check.

Five-second eyeball check by Cesar before merging. Cost of skipping is a confusing reload modal next session. Flagged in the closeout.

### Verified by Architect: the masked-root-cause grep

Reviewer's lesson candidate proposed running `grep -rn "new AeroConfig()" Assets/Scripts/`. Architect ran this 2026-05-07 19:08 JST plus a wider variant covering `default(AeroConfig)` and `new AeroConfig\s*\(\s*\)` across the entire `Assets/` tree.

**Result: ZERO hits in either grep.**

This DEEPENS rather than resolves the mystery. The implementer's "Hypothesis C — zero-init struct" diagnosis is empirically incorrect: there's no source-code site that constructs `new AeroConfig()` directly. The masked root cause of the original §2b crash remains unidentified.

Likely candidates (not investigated in this task):
- An `AeroConfig` field cached on a long-lived object and read before `LoadAeroConfig` populated it (race or order-of-init bug).
- Unity serializer round-trip on a struct field zeroing it during scene reload or domain reload.
- A different code path that AssertValid happened to also cover, distinct from the one that originally crashed.

The AssertValid backstop catches all of these at config-load time with a clear error message, so practical risk is contained. But: **if a similar zero-init class of bug appears in another config struct (WindConfig, SurfaceConfig, PuttConfig, StatCoefficients, StatCaps), do not assume “it's a different bug” — assume it may be the same masked mechanism resurfacing.** Lesson written to `tasks/lessons.md`.

### Closing actions (this addendum)

- STATUS → `ARCHITECT_REVIEW_PASS_WITH_DEFERRAL` ✅
- Notion controls_g entry [`35931e0e`](https://www.notion.so/35931e0e9a368163a839d5190f134f0f) flipped In Progress → Done, Closed=2026-05-07.
- New spec folder created: `Docs/Specs/Queued/controls_g_smoke_followup/` with NOTES.md (cinematic visual smoke debt only).
- Notion `controls_g_smoke_followup` entry created at Order 230, P1 — High, Phase 02. Loop v1.
- TellCode.md: controls_g NEXT pointer flipped to DONE; §2b deferred-smoke OPEN flag NARROWED (not closed); new NEXT pointer for `controls_g_smoke_followup`.
- Lesson written to `tasks/lessons.md`: "Defense-in-depth fixes can mask the original regression site" + "Smoke-runner timed waits are fragile against shot-power changes."
- Memory entry: zero-init grep result + lesson summary.
- Cesar manual: scene-save eyeball check on `LabScaffold.unity` before next session.

