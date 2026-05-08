# Architect Review — `loop_v1_2b_camera_transitions`

**Reviewer:** golfin-reviewer (architectural-review agent)
**Timestamp:** 2026-05-07 14:32 JST
**Verdict:** `ARCHITECT_REVIEW_ESCALATE`

---

## Summary

The 18 PASS items are architecturally sound and the code is shippable in isolation. The 3 FAIL items (live smoke captures for Downrange / putter-stays-GroundLevel / OB freeze) are blocked by an out-of-scope physics regression in `AeroModel.ComputeAeroForce`. The implementer correctly:

- Did not modify any physics core (respects Hard Rule #1).
- Did not bake-claim screenshots (respects Hard Rule #6).
- Reported the blocker honestly with stack trace + diagnosis.
- Asked the right question instead of guessing the call.

However, two facts require Cesar's decision before this can be marked PASS:

1. **The Definition of Done explicitly requires 4–6 visual smoke captures of the new modes (Downrange / CupZoom / OBFreeze).** Test coverage is strong (9/9 EditMode PASS, all three modes exercised via `RecordingModeSetter`), but the spec did NOT pre-authorize substituting EditMode coverage for visual smoke evidence. The on-disk screenshots confirm the Director compiles and wires, not that the new modes render correctly.
2. **The implementer's proposed 1-line physics fix targets the wrong line.** The crash stack says `AeroModel.cs:78`, which is `spin.Rate / cfg.SpinRateReference` in the constant-mode (non-LUT) lift branch. The implementer's proposed guard `if (speed <= fp.Epsilon) return fp3.Zero;` would land at line 29, which is a different `vRel / speed` divide. Applying that guard would not fix the line-78 crash. The real diagnosis points at either (a) `cfg.UseLiftLut` returning false unexpectedly so the constant-mode branch executes when it shouldn't, or (b) `cfg.SpinRateReference` loading as `fp.Zero` from config. Either way, this is a real bug with a non-trivial root cause, not a 1-line guard.

Because the smoke-evidence waiver is a spec interpretation call AND the proposed Quick fix would not actually unblock the smoke captures, Cesar's judgment is needed.

---

## What's solid (the 18 PASS items, audited)

### Architectural soundness

- **`LoopCameraDirector.cs`** — clean MonoBehaviour at `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`. ModeMap is a pure-data `Dictionary<BallState, ChaseCamera.Mode?>` per L4. Subscription lifecycle (Awake/OnDestroy/SetControllerAccessor) properly handles both production and test paths. `Aiming → null` mapping correctly preserves club-driven GroundLevel per L1/L5.
- **`IModeSetter.cs`** + **`IControllerAccessor`** — two test seams. `IModeSetter` matches the spec verbatim. `IControllerAccessor` is an additive seam (not in spec) that lets tests inject a `StubControllerAccessor` instead of a real `PhysicsLabController` MonoBehaviour. This is sound — improves testability without coupling tests to PhysicsLabController internals.
- **`PhysicsLabControllerAdapter`** (internal sealed class) — wraps `PhysicsLabController` as `IControllerAccessor`. Adapter pattern, no behavior change. Production path still goes through `controller` Inspector field; the adapter is only constructed when no `_controllerAccessor` stub is injected.
- **OB pivot computation** — scans `traj.terrainHits` for first `Water` or `OOB` hit, falls back to `change.position + Vector3.up * obFreezeHeightAboveTerrain`. Matches L9 / Q3'a exactly. Test 4 verifies the (25, 5, 5) pivot for a hits=[Fairway@10, Water@(25,0,5)] sequence; Test 5 verifies the fallback path.
- **Cinematic cut driver** — extracted into `public void TickCinematicCut()` so EditMode tests can drive it directly without the `SendMessage("Update")` ShouldRunBehaviour assertion. Sensible deviation from spec which only had `Update()`. Test 7 verifies fire at 70% carry; Test 8 verifies putt skip; Test 9 verifies min-carry skip.
- **`ChaseCamera` extensions** — `Downrange`, `CupZoom`, `OBFreeze` modes added to enum at line 16. `SetMode` mode-entry hook captures `_cupZoomStartTime`/`_cupZoomStartPos` only on entry (avoids re-tweening if mode is set twice). LateUpdate switch handles all 6 modes; existing `Chase` retuned to `5m / 2.5m` per L10 (was 8m / 3m). EaseOutCubic helper present at line 146.
- **PhysicsLabController relocations** — `HandleShotResolved` no longer calls `chaseCamera.SetTarget`/`ResetToOrigin` (replaced with `_lastShotOrigin`/`_lastShotLaunchDir` caching at lines 723-724). `HandleShotComplete` no longer calls `chaseCamera.SetTarget(null)` (commented-out at lines 775-776). `FireInternal` (preset path) keeps its calls at lines 837-841 per spec § Implementation C "Leave alone in FireInternal". 6 internal accessors at lines 81-86 match the spec list.
- **`Golfin.Diagnostics.Runtime` asmdef** — created with `autoReferenced: true`, references `Golfin.Gameplay.Loop`. `Golfin.Physics.Viewer` and `Golfin.Physics.Tests` updated to reference it. Clean asmdef boundary.
- **`CaptureCore`** — factored from editor-side. `SnapAtEndOfFrameAndPause` is the canonical coroutine. `SnapWhenStateReached` is the SM-gated API that closes the §2a OPEN FLAG.
- **`CaptureHelper.cs`** — properly thinned to a wrapper. Editor menu items (`GOLFIN > Capture > ...`) and Fake-State presets all retained; only the RT/Y-flip implementation was extracted. Capture-helper Maintenance protocol (extending FakeMidAim/FakeReset for new HUD contexts) is N/A here — §2b doesn't add a new HUD context, so the protocol clause doesn't trigger.
- **`SmokeTestRunner2a`** — inline `SnapAndPauseAtEndOfFrame` duplicate gone. Line 202: `yield return StartCoroutine(CaptureCore.SnapAtEndOfFrameAndPause(capLabel))`.
- **`TrajectoryRenderer._showInGameplay`** — flag added at lines 17-18, gate at line 45. Editor-or-flag semantics preserve current lab visibility while gating gameplay scaffold.
- **Tests file** — `LoopCameraDirectorTests.cs` cleanly written. `RecordingModeSetter` records all 6 IModeSetter calls. `StubControllerAccessor` is mutable for inter-phase test setup. `DirectorFactory.Create` reduces test boilerplate. 9/9 PASS, 236/236 total — the additive math holds (227 + 9 = 236).

### Spec deviations (both acceptable)

1. **`SnapWhenStateReached(MonoBehaviour owner, ...)`** — adds `owner` first parameter. The spec's signature was non-functional as written: `SnapAtEndOfFrameAndPause` returns `IEnumerator` and requires `StartCoroutine`, which can only be called on a MonoBehaviour. The implementer's signature is the minimal correct fix. ACCEPTED.
2. **Director self-wires in own Awake via `GetComponentInParent<PhysicsLabController>()`** — spec L6 said wire from PhysicsLabController.Awake. Implementer's choice is cleaner separation (PhysicsLabController stays unaware of the Director) and is consistent with L14's "Inspector-wires chaseCamera, gets _ballSM via internal accessor". Behavior-identical. ACCEPTED.

### Test seam correctness

The spec's Hard Rule #7 says "Do NOT skip the `IModeSetter` test seam. Director tests must run without instantiating a Camera GO." Verified: `RecordingModeSetter` is a plain `sealed class`, no MonoBehaviour, no Camera GO. All 9 tests instantiate Director via `new GameObject().AddComponent<LoopCameraDirector>()` and immediately inject the stub setter via `director.SetModeSetter(setter)`. Compliant.

### Capture-helper protocol compliance

The §2a-mandated rule that `SmokeTestRunner2a`'s inline byte-equivalent capture method get unified into runtime-side helper assembly: DONE. The method is gone; CaptureCore is the single source. The SM-gated API closes the second half of the §2a OPEN FLAG.

The Maintenance protocol clause from CLAUDE.md (extending `CaptureHelper.FakeMidAim`/`FakeReset` for new HUD contexts) does not apply — §2b ships zero new static-bus contexts under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. The capture-helper protocol gate is correctly cleared.

---

## What blocks PASS

### Issue 1 — visual smoke evidence is missing for the 3 new modes

The Definition of Done § "Smoke evidence per § above: 4–6 captured frames" requires:

- Driver shot: chase early → downrange after cut → chase on touchdown → settled at rest
- Putter shot on green: stays in GroundLevel throughout
- Shot into water: OBFreeze fires, camera locks at first Water hit XZ

The two on-disk PlayMode screenshots (`2b_1_aiming_*.png`, `2b_chase_mode_active_*.png`) are byte-different but visually near-identical pre-shot tee frames. Neither shows a new-mode camera transition firing. The third capture (`2b_editmode_scene_*.png`) is an editmode scene-state capture, not a runtime camera-mode capture.

The implementer correctly attributes this to `AeroModel` crashing the simulation before the SM can transition out of the Aiming→Flying boundary, and points to PASS coverage in the EditMode tests. This is a defensible position but the spec did not pre-authorize the substitution. **Cesar's call.**

### Issue 2 — proposed Quick fix targets the wrong line

The implementer asks whether to spawn a Quick task with `if (speed <= fp.Epsilon) return fp3.Zero;` at `AeroModel.cs:29`.

Stack trace says the crash is at `AeroModel.cs:78`. Reading line 78:

```csharp
fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
```

This is the **constant-mode (non-LUT) lift branch** (`else` clause at line 76). The divisor is `cfg.SpinRateReference`, NOT `speed`. The implementer's proposed line-29 guard would not fix this crash.

For line 78 to throw, one of:
- `cfg.SpinRateReference` is loading as `fp.Zero` from `aero.csv` — config-load bug.
- `cfg.UseLiftLut` is `false` and we expected it to be `true` — config-flag bug. (Per `controls_e/f`, lift LUT is the default codepath.)
- `cfg.LiftLut.IsValid` is `false` despite the LUT-mode flag — LUT-load bug.

Any of these is a real diagnosis task, not a 1-line guard. Fast-tracking the implementer's proposed fix would leave the actual bug in place and could mask it on future shots. **Cesar should decide whether to:**

- (A) Ship §2b as PASS now, queue a proper diagnosis task (`controls_g_aero_constant_mode_crash` or similar) for the physics regression, and accept EditMode test coverage as the visual smoke substitute for this iteration.
- (B) Leave §2b as ARCHITECT_REVIEW_ESCALATE, spawn the proper diagnosis task, fix the physics regression, then re-run smoke captures and re-route through pipeline for a final PASS.
- (C) Spawn the implementer's proposed Quick task at line 29 even knowing it likely won't help — to prove the diagnosis is incomplete — then escalate physics to a proper task.

Architect lean: **(B)**. The §2b camera work is genuinely complete in isolation, but `controls_e/f` calibrations all hinge on the lift LUT being live. A constant-mode-path crash means something silently fell back off the LUT. That's a config-load regression worth diagnosing properly before any further smoke work, and §2b's smoke gates are a natural forcing function.

---

## Questions for Cesar

1. **Smoke-evidence waiver.** Is 9/9 EditMode test coverage of the new modes (`SetMode(Downrange/CupZoom/OBFreeze)` + framing parameter assertions) acceptable as a substitute for the 4–6 visual smoke captures the spec required? If yes, §2b gets PASS now. If no, see Q2.
2. **Physics regression sequencing.** Do you want to (A) queue a proper diagnosis task for the AeroModel line-78 crash and ship §2b on EditMode coverage, (B) block §2b until the physics is fixed and smoke captures land, or (C) spawn the implementer's proposed Quick fix at line 29 first to confirm it doesn't help, then properly diagnose?
3. **Implementer's proposed line-29 guard.** Even if it doesn't fix the immediate line-78 crash, is the underflow guard at line 29 worth shipping as defense-in-depth? Or do you want all aero divides audited together in the diagnosis task?

---

## Files audited

| File | Status |
|---|---|
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | PASS — architecturally sound |
| `Assets/Scripts/Physics/Viewer/IModeSetter.cs` | PASS — matches spec |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | PASS — three new modes + Chase retune correct |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | PASS — relocations clean, accessors correct |
| `Assets/Scripts/Physics/Viewer/TrajectoryRenderer.cs` | PASS — flag and gate correct |
| `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs` | PASS — factored cleanly, owner-param deviation accepted |
| `Assets/Scripts/Diagnostics/Runtime/Golfin.Diagnostics.Runtime.asmdef` | PASS — references correct |
| `Assets/Scripts/Editor/CaptureHelper.cs` | PASS — thin wrapper as spec required |
| `Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` | PASS — inline duplicate gone |
| `Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef` | PASS — Diagnostics.Runtime added |
| `Assets/Scripts/Physics/Tests/Golfin.Physics.Tests.asmdef` | PASS — Loop + Diagnostics.Runtime added |
| `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` | PASS — 9 tests, all 3 new modes covered |
| `Assets/Scenes/Physics/LabScaffold.unity` | PASS — Director GO + Inspector wiring confirmed via reflection in implementer report |
| `screenshots/2b_1_aiming_*.png` | INSUFFICIENT — pre-shot frame, no new-mode transition shown |
| `screenshots/2b_chase_mode_active_*.png` | INSUFFICIENT — pre-shot frame, no new-mode transition shown |
| `screenshots/2b_editmode_scene_*.png` | OUT OF SCOPE — editmode scene capture, not runtime camera-mode capture |

---

## Verdict reiterated

`ARCHITECT_REVIEW_ESCALATE` — Cesar must decide on the smoke-evidence waiver and the physics sequencing. The camera/orchestration code itself is ready to ship.

---

## ADDENDUM — Human Architect ruling (claude.ai), 2026-05-07 09:20 JST

**Status flipped: `ARCHITECT_REVIEW_ESCALATE` → `ARCHITECT_REVIEW_PASS_WITH_DEFERRAL`.**

Reviewer subagent's diagnosis was independently verified by the human Architect: `AeroModel.cs` line 78 IS `fpMath.Clamp(spin.Rate / cfg.SpinRateReference, ...)` in the constant-mode (non-LUT) lift branch. The implementer's proposed line-29 guard (`vRel / speed`) is a different divide and would not fix the line-78 crash. Reviewer wins on the diagnosis call.

### Q1 — Smoke-evidence waiver: DEFERRAL, not waiver.

9/9 EditMode tests verify dispatch correctness (right mode at right state, right framing math from inputs). They do NOT verify visual correctness (does Downrange compose well behind the landing zone, does CupZoom hover at the right height above the flat circle, does EaseOutCubic read as intentional). Those are genuine visual judgments and §2a's iter-3/iter-4 saga established that we don't accept evidence we can't verify.

But blocking §2b on a pre-existing aero regression is wrong sequencing. Resolution:

**§2b is architecturally PASS + EditMode-verified, with a deferred-smoke OPEN flag.** Visual smoke for Downrange / putter-stays-GroundLevel / OBFreeze deferred to `controls_g_aero_constant_mode_crash` closeout, captured using the new `CaptureCore.SnapWhenStateReached` API §2b just shipped. Clean handoff: controls_g uses §2b's tool to validate §2b's modes.

§2b's pipeline can advance now. §2c can start. The smoke debt closes naturally when controls_g lands.

### Q2 — Physics sequencing: (A) modified.

Ship §2b PASS now with the deferral flag. Queue **`controls_g_aero_constant_mode_crash`** as P0 immediately after.

- NOT (B): blocking §2b on physics is wrong; camera work is conceptually independent.
- NOT (C): wasting a pipeline cycle to confirm a diagnosis the reviewer already nailed via stack-trace reading. Don't disprove correct things experimentally.

**Critical context for controls_g spec:** this regression was latent. §2a's putter shots didn't trigger it because `Putter.spin.Rate ≈ 0` → `IsSpinning = false` → the function returns at line 56 (`if (!spin.IsSpinning) return drag;`) and never enters the lift branch. §2b's driver shots are the first lift-branch executions since controls_f closed two days ago. §2b is doing its job by surfacing the latent bug — don't punish it for finding the bug.

The likely cause (in order):
1. `cfg.UseLiftLut` loading as `false` (config-flag regression — most likely; would silently fall back to constant-mode where SpinRateReference defaults to zero).
2. `cfg.LiftLut.IsValid` returning `false` despite the flag (LUT-load regression).
3. `cfg.SpinRateReference` loading as `fp.Zero` (config-default regression, independent of LUT path).

Per controls_e/f, lift LUT is the canonical path. If we're hitting constant-mode at all, that's the regression. controls_g must:
- Print `cfg.UseLiftLut`, `cfg.LiftLut.IsValid`, `cfg.SpinRateReference` at first lift call
- Trace back to `PhysicsConfigLoader` to identify what changed in load
- Audit all 3 aero divides holistically (lines 29, 63, 78) and add proper guards where defensible
- Re-run controls_e/f gate (211/211 PASS) to confirm fix doesn't regress driver/iron carries

Estimate: half-day to 1 day. Ship before §2c or in parallel — §2c is turn counter logic, doesn't need physics.

### Q3 — Line-29 guard: NO ad-hoc fix; audit holistically in controls_g.

Three reasons:
1. It doesn't fix the actual crash (line 78).
2. Spot-fixing one divide silently masks any future legitimate underflow there.
3. The three divides need three different defenses:
   - **Line 29** (`vRel/speed`): speed-underflow case, already gated by line-26 epsilon but `Sqrt` may underflow further. Defense: tighter epsilon OR consider returning drag-only.
   - **Line 63** (LUT-mode `spinParam = R*Rate/speed`): same speed denominator, gated by same line-26 epsilon. Probably fine; verify in audit.
   - **Line 78** (constant-mode `Rate/SpinRateReference`): denominator is a CONFIG value, not a runtime quantity. Defense is "make sure cfg loads correctly" + maybe a constructor-time assert that `SpinRateReference > 0`. Very different category.

Bundling all three under controls_g is the right shape.

### Spec-deviation acceptance (carry forward from reviewer)

Both implementer deviations accepted as valid:
1. `SnapWhenStateReached(MonoBehaviour owner, ...)` — owner-first signature is the minimal correct fix for coroutine host requirement; spec's 4-arg version was non-functional.
2. Director self-wires in own Awake via `GetComponentInParent<PhysicsLabController>()` — cleaner separation than wiring from PhysicsLabController.Awake; behavior-identical and consistent with L14.

### Closing actions (this addendum)

- STATUS → `ARCHITECT_REVIEW_PASS_WITH_DEFERRAL`.
- Notion §2b entry flipped In Progress → Done, Closed=2026-05-07 (deferred smoke debt tracked in OPEN FLAGS not in Notion Status).
- Notion controls_g entry created at Order 220, P0 — Critical, Phase 02. Loop v1.
- TellCode.md: §2b NEXT pointer flipped to DONE; new §2b deferred-smoke OPEN flag added; new NEXT pointer for controls_g added.
- New spec folder created: `Docs/Specs/Queued/controls_g_aero_constant_mode_crash/`.

