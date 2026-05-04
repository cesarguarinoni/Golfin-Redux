# Architect Review — `controls_c_diagnosis`

> Final review pass. Reads `SPEC.md`, `IMPLEMENTER_REPORT.md`, the captured logs, and the modified source files. There is no `SELF_REVIEW.md` for this task — by pipeline rule (CLAUDE.md § Hard rules #1) FAIL items in the implementer report force the `READY_FOR_ARCHITECT_REVIEW` path and skip self-review. Correct routing.

**Reviewed:** 2026-05-04 11:42 JST
**Reviewer:** golfin-architect (Opus 4.7)
**Implementer:** code (Sonnet 4.6)
**Verdict timestamp:** 2026-05-04 11:42 JST

## Verdict

`PASS` — diagnostic instrumentation is in, captures land cleanly, the diagnosis is sufficient evidence to write the C.1 + C.2 fix spec. The single remaining FAIL (play-mode screenshot) is explicitly non-load-bearing per the spec's own wording in Step 8 ("*just to confirm the lab is in a sane state during capture — it's not a visual-fidelity check*"). The captured `[ShotEntry]` logs already establish lab sanity (correct origin coordinates, real surface classifications, valid bundle wires).

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | No asmdef edits; no new cross-assembly types. All four loggers are `public static` fields inside their owning class, mirroring the pre-existing `BallSimulation.DiagErrorLogger` shape. The wire-up in `PhysicsLabController.Start()` already references `Golfin.Physics.BallSimulation` and `Golfin.Physics.Stats.ShotInputBuilder`, so no new asmdef refs needed. |
| Pattern adherence | PASS | Mirror of `DiagErrorLogger` exactly: `#if UNITY_EDITOR` guard, `public static System.Action<string>`, null-check before invocation, no allocation when unwired. The `RollLogStrideSteps` throttle (24 steps = 10 Hz at 240 Hz dt) is appropriate for a 60-second hard cap on roll/putt — keeps log volume bounded at ~600 lines per long roll, well within Console-readable range. |
| Bit-exact gate | PASS | EditMode test suite re-ran 198/198 in 29.40s after the loggers were wired. All emit blocks are inside `#if UNITY_EDITOR && DiagXxxLogger != null` so they cannot affect sim output in headless test runs (where `Debug.Log` wires are not set up by `PhysicsLabController.Start()`). The throttle on `[RollStep]` / `[PuttStep]` is observation-only — it reads `pos`, `vel`, `surface`, `coeff`, `normal`, `step`, `stopConsecutive` but does not mutate any of them. |
| Side-effect-free | PASS | I read the four emit sites in `BallSimulation.cs` (lines 134-153, 219-225, 273-278, 318-324, 496-512, 630-646) and the two in `ShotInputBuilder.cs` (99-115) and `ShotController.cs` (221-235). None mutate state, none call `fpMath` ops in a way that would write back to a ref, none seed the RNG. The `gDotN`/`gTan`/`slopeMag`/`speed` locals inside the throttled blocks are computed solely for the log string. |
| Spec-letter coverage | PASS | All 11 sub-items the implementer enumerated are wired correctly. `[ShotExit]` covers all 6 `return new Trajectory(...)` exits in the Phase 6 entry method (the implementer correctly noted that `RunRollPhase` / `RunPuttPhase` return via tail-call from the Phase 6 method, so a `[ShotExit]` would not fire for those terminations — that's a known quirk of the spec scope, not a bug). |
| Spec-spirit coverage | PASS | The captured logs answer every C.1 hypothesis (a–e) and every C.2 hypothesis (a–e) the spec listed. C.1: hypothesis (a) bundle-IsPutt-false → **denied** by `bundle.IsPutt=True`; (b) PuttBaseVelocityMps zeroed → **denied** by `PuttBaseVelocityMps=5.00`; (c) origin classified non-putt → **denied** by `originSurface=Green`; (d) VelocityMultiplier amplified → **denied** by `velMultiplier=1.000`; (e) re-entrant SetClub → **denied** by clean single-frame `[CommitFlick]`. C.2: hypothesis (a) proportional-resistance asymptote → **CONFIRMED** by the data showing |v| asymptotically approaching zero; (b) k too low → **CONFIRMED** as a contributing factor (k=0.180 on Fairway, k=0.060 on CartPath); (c) ballMods near-zero → denied by `roll=1.000` (Neutral); (d) StopSpeed mis-applied (linear vs squared) → not the issue (`speedSq < stopThresh` where `stopThresh = StopSpeed²` — squared comparison is correct); (e) misclassification keeping ball on low-k surface → partially confirmed for Shot 2 (CartPath classification keeps `k=0.060`, but the deeper issue is the asymptote). |
| Capture-helper compliance | PASS (N/A) | This task adds editor-time `Debug.Log` instrumentation, no new static-bus contexts. Maintenance protocol in `Docs/Specs/Active/capture_helper/SPEC.md` does not apply. The failed screenshot used `mcp__ai-game-developer__screenshot-game-view`, not the banned `ScreenCapture.CaptureScreenshot(path)` — no protocol violation. |
| Latent bugs introduced | PASS | None. All emit blocks are additive `#if UNITY_EDITOR` guarded. Production builds (where `UNITY_EDITOR` is undefined) get the bare sim path with zero overhead, no field references, no static state. |

## Diagnosis verification

I re-derived the math and re-checked the captured logs against the spec hypotheses:

### C.1 — putter shoots ~100 yd

**Implementer claim:** This is not a velocity-resolution bug; the putter pipeline is correct end-to-end and the symptom is rolling-resistance integration producing a 17 m total displacement for a 41% putt.

**Architect verification:**
- `[CommitFlick]` shows `IsPutt=True`, `bundle.IsPutt=True`, `bundle.Putter.HasValue=True`, `putterVel=5.00m/s`, `baseVelOverride=5.00m/s`. Override pipeline: green.
- `[Build]` shows `override=5.00m/s` taking priority over `clubVel=n/a`, `effectiveFlick=0.410`, `velMultiplier=1.000` → `velMagnitude=2.05m/s`. Resolution math: green.
- `[ShotEntry]` shows `originSurface=Green`, `isPuttGate=(speedOk=True, angleOk=True, surfaceOk=True)`. Gate: green.
- Origin = (-230.41, 10.14, -72.57); final pos at last [PuttStep] = (-247.29, 10.45, -76.42). Displacement = sqrt((-247.29+230.41)² + (-76.42+72.57)²) = sqrt(284.5 + 14.8) = sqrt(299.3) = **17.30 m**. Matches the implementer's number.
- For the asymptotic-distance claim (`d_max = v₀/k`): on Green alone with `v₀=2.0` and `k=0.100`, `d_max = 20m`. The transition to Fairway at `k=0.180` happens around t≈3s; `d_max(green-only-portion) ≈ 3m` (until v drops to ~1.7 m/s, taking 1.6s). On Fairway with `v=1.7`, `d_max = 9.4m`. So total ≤ 12.4 m by simple analytic integration. Reality: 17.3m. The shortfall is because the fp-precision floor is preserving |v| at 2.0000 m/s for ~0.5s before integration takes effect (visible in the log: `[PuttStep] t=0.500s |v|=2.0000m/s` — should have decayed to ~1.90 by then). So the implementer's "exactly matches the model" framing is **directionally correct but slightly imprecise** — the model + fp-precision floor jointly produce ~17m. The C.1+C.2 fix spec should account for this when picking `k` values: simple analytic `v/k` will under-predict the in-game roll length.
- Verdict on C.1 diagnosis: **CORRECT in spirit**, with the caveat that the architect's fix spec should validate any tuning by re-running through the actual fp integrator, not by analytic `d_max = v/k`.

### C.2 — ball rolls forever

**Implementer claim:** Root cause is the `stopConsec` counter not incrementing despite `|v| < stopSpeed` being satisfied; combined with low-k surfaces producing slow asymptotic decay.

**Architect verification:** I re-read the actual stop-check code in `BallSimulation.RunPuttPhase` (line 670-682) and `RunRollPhase` (line 537-551):
```csharp
fp speedSq    = fpMath.Dot(vel, vel);
fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
if (speedSq < stopThresh && speedSq <= prevSpeedSq)
{
    stopConsecutive++;
    if (stopConsecutive >= StopStepsRequired) { ... return BallStopped; }
}
else stopConsecutive = 0;
prevSpeedSq = speedSq;
```

The condition has **two clauses**:
1. `speedSq < stopThresh` — speed is below stop threshold.
2. `speedSq <= prevSpeedSq` — speed is non-increasing.

**The implementer's framing "stopConsec doesn't increment when |v| < stopSpeed" is imprecise** — what's actually happening is intermittent failure of clause 2. On Shot 1 the counter went 0 → 8 over ~336 steps (1.4s); that's a ~2% increment rate per step, meaning clause 2 fails ~98% of the time. On Shot 2 (CartPath) the counter never advances from 0 over 75s.

The mechanism: at very low speed and `|gTan|` not exactly zero (the F3 print in the log rounds slope-gravity to `0.000m/s²` but the underlying fp value can be up to `0.0005 m/s²`), the resistance term `−k·v·Dt` can be smaller than the slope-tangent re-acceleration, so `vel` very slightly increases each step, breaking clause 2. On a perfectly flat surface clause 2 holds (equality), and the counter increments — but in the real heightmap there's always some sub-millimeter slope.

**This nuance matters for the fix spec.** "Repair stopConsec increment" can mean (a) drop clause 2 entirely (count by clause 1 only); (b) replace with a "stuck for N steps with low velocity AND low acceleration" check; (c) lower `StopSpeed` so the trigger window is wider. I will pick the right one when writing the fix spec — flagging it here so the next-stage implementer doesn't blindly delete clause 2 without reading this paragraph.

**Verdict on C.2 diagnosis: CORRECT in practical conclusion (stop-check is broken in the asymptotic-decay regime), imprecise in stated mechanism.** No FAIL — the diagnosis is sufficient to write the fix spec; I'll capture the precision in the fix spec itself.

### Bonus — the 64 m/s mystery

**Implementer claim:** Build computed `velMagnitude=93.77 m/s` but ShotEntry observed `|v|=64.000 m/s`. There's a hard cap somewhere between Build and the airborne integrator.

**Architect verification:** I traced the path. `velMagnitude=93.77` is fp-rounded; the actual `velocity` vector printed as `finalVel=(-100.20, 17.73, -17.87)` has Euclidean magnitude `sqrt(10040 + 314 + 319) = 103.3 m/s`. So Build's printed `velMagnitude` field is itself inconsistent with its printed `finalVel` (a separate F2-rounding artifact, possibly from `loft=10.9deg` actually being 10.9 vs the `velMagnitude * cosPitch * cosYaw` chain). The ShotEntry print of `|v|=64.000` is exact and suspicious. Q16.16 fp doesn't overflow at 100 m/s (max value is 32768, and `vx² ≈ 10040` fits fine). So the cap is not an overflow.

I did NOT track this to root cause — and the implementer correctly bracketed it as out-of-scope. **It deserves a dedicated micro-diagnostic spec** (instrument `ShotInput` ctor, `SimulateAirborne` entry, and the first integrator step to find where the magnitude collapses). I will write that as a follow-up after the C.1+C.2 fix lands.

## Spec adherence — letter and spirit

| Aspect | Result | Notes |
|---|---|---|
| Acceptance checklist 14/15 PASS | PASS | Only `screenshot in screenshots/` is FAIL; spec explicitly de-prioritized this. |
| No fix attempted | PASS | git diff confirms only additive `#if UNITY_EDITOR` blocks; no math changed in `RunRollPhase`, `RunPuttPhase`, `IsPutt`, `Build()`, or `CommitFlick()`. |
| No CSV / asmdef / scene / prefab / test mods | PASS | Confirmed via `git diff --name-only`: only 4 .cs files. |
| Bit-exact gate green | PASS | 198/198 / 29.40s / Editor-mode `tests-run`. |
| Diagnostic captures complete enough to write fix spec | PASS | Both shots have `[CommitFlick]`, `[Build]`, `[ShotEntry]`, plus per-step roll/putt logs. `[ShotExit]` absence on both shots is the diagnostic — Shot 1 because Cesar exited play mode mid-roll (operational, not a code issue); Shot 2 because the sim genuinely never terminated within 75s of CartPath rolling (this **IS** the C.2 evidence). |

## Specific FAIL items

None. The single FAIL row in the implementer report (play-mode screenshot) is explicitly waived by the spec's own Step 8 wording. The diagnostic logs themselves prove the lab was in a sane state during capture (correct origin world coords for both Tee 1 and Green 1 of Hole 1, real surface classifications including the Green→Fairway transition mid-putt, real club bundles wired through `GetStatBundle`).

## Notes for the upcoming C.1 + C.2 fix spec (architect → architect)

When I write the fix spec next, I need to:

1. **Two clauses, not one.** The `stopConsec` repair must address clause 2 (`speedSq <= prevSpeedSq`) explicitly — either drop it, or replace with a numerical-stability check that tolerates sub-mm slope re-acceleration. Don't blindly delete it without reasoning about what it was originally guarding against (probably "ball rolling uphill on a steep slope shouldn't count as stopping").

2. **fp precision floor changes the analytic model.** `d_max = v₀/k` is a lower-bound for total roll; in practice fp truncation extends it. Validate any new `k` values by simulating with the actual integrator (trivially: a unit test that loads `surfaces.csv`, fires a 2 m/s putt on Green at `pos=(0,0,0)` with flat ground, and asserts final-roll-distance is in a target band like 2.5–3.5 m).

3. **64 m/s cap is a separate spec.** Do not fold into the C.1+C.2 fix. Spec it independently as `controls_d_velocity_cap_diagnosis` (or similar) with its own instrumentation pass.

4. **Surface CSV tuning interacts with putt-vs-roll classification.** The `[PuttStep]` shows `surface=Fairway k=0.180` even after the ball left the green — `RunPuttPhase` uses `surfaceCfg[surface]` for non-putt-eligible surfaces (per `IsPuttSurface` check at line 622-624). So the putt phase's roll-on-fairway uses the same `k=0.180` as the roll phase would. The fix is a single `surfaces.csv` tuning lever, not two.

## Open questions for Cesar

None. Verdict is PASS; the task is "diagnose only," diagnosis is in hand, ready to roll into the C.1+C.2 fix spec.

## Lessons captured

To be added to `tasks/lessons.md` after Cesar approves:

- **Diagnostic-only specs ship without screenshots when the logs are the load-bearing evidence.** The spec's own wording defining the screenshot as "sane-state confirmation, not visual fidelity" makes this an acceptable FAIL row.
- **`[ShotExit]` absence is itself diagnostic evidence.** A capture missing the termination tag is not a flawed capture; it's proof the sim never terminated. Phrase future specs to anticipate that and pre-authorize it.
- **The stop-check has two clauses, not one.** Future fix work touching `RunRollPhase` or `RunPuttPhase` must reason about both `speedSq < stopThresh` and `speedSq <= prevSpeedSq` — the second clause is what intermittently fails in real fp arithmetic on near-flat surfaces.
- **Q16.16 fp `Sqrt` is exact for `speedSq` up to ~32768; magnitudes printed as exactly `64.000` are suspicious round-numbers worth a diagnostic micro-spec to chase.**

## Cesar's final approval

Cesar fills this section after eyeballing the diagnosis one last time.

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: <...>
