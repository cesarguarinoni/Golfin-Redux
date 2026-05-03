# SPEC — `controls_c_diagnosis` — Diagnostic instrumentation for Controls finetuning (item C)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

**Created:** 2026-05-04 07:12 JST
**Architect:** Claude (claude.ai)
**Roadmap:** `Docs/Roadmap.md` §1 — gates §2 (Loop v1)
**Notion:** `C — Controls finetuning (gates Loop v1)` — Status: In Progress, P0 Critical, Order 120

## Status

See `STATUS.md` for current pipeline state.

## Goal

Add **diagnostic instrumentation only** to the shot-resolution + roll/putt-phase code paths so we can capture concrete evidence of the two physics issues that are blocking Loop v1:

- **C.1** Putter shoots ~100 yd instead of putt-range (~3 m).
- **C.2** Ball rolls forever regardless of surface.

Static analysis of the code suggests both wires (`PhysicsLabController.SetClub(3)` injects a `PutterStats` bundle with `IsPutt=true`; `ShotController.CommitFlick` passes `_config.PuttBaseVelocityMps=5f` as override; `ShotInputBuilder.Build` honors override > 0) are correct on paper. So we need run-time evidence to find the actual point of divergence.

This task **does not fix anything**. It adds null-safe, opt-in static loggers that emit a structured one-line snapshot at each well-defined checkpoint in the shot-resolution and roll/putt phases. It does not change any sim behavior, does not change any public API, and must keep the existing 198/198 EditMode test gate green.

After this lands, Cesar fires a putter shot + a long fairway shot in `LabScaffold` with a hole loaded, copies the console output into the implementer report, and the architect writes the C.1 / C.2 fix specs from that evidence.

## Why diagnosis-first (no fix in this task)

Memory: "*Debugging discipline: Weigh 3+ hypotheses ranked by likelihood (include at least one outside-the-box option). State what evidence confirms/denies each. Flag guessing vs. evidence. Don't chain single-hypothesis guesses.*" — and this is exactly the case. Static analysis can't disambiguate between the candidate causes for either issue:

For C.1 (putter going ~100 yd):
- (a) `bundle.IsPutt` evaluates to false at `ShotInputBuilder.Build` time despite `_shotController.IsPutt=true` (e.g. injected bundle was constructed with `ClubStats` instead of `PutterStats`)
- (b) `_config.PuttBaseVelocityMps` is being zeroed by a CSV reload before `CommitFlick` runs
- (c) `BallSimulation.IsPutt(input, surfaces)` returns false because origin-surface classifies as non-putt-eligible (Fairway / Rough), so the shot takes the airborne path; if velocity is genuinely 5 m/s the carry would be < 1 m, so this on its own wouldn't explain 100 yd — but it could compound with (a) or (b)
- (d) `resolved.VelocityMultiplier` is being amplified far beyond the 2.0 cap (unlikely but worth confirming)
- (e) Something in `PhysicsLabController.SetClub` re-entrancy via `ClubSelectionBroadcast` fires `SetClub` with the wrong index after putter selection

For C.2 (ball rolls forever):
- (a) Steady-state arithmetic: with proportional resistance `a = -k·v` and slope-gravity tangent `g·sin(θ)`, the steady-state speed is `g·sin(θ) / k`. For Fairway (`k=0.18`) on a tiny 1° slope: `v_ss ≈ 0.95 m/s` — well above `StopSpeed=0.10`. **Result: ball reaches terminal velocity and never stops on any non-flat surface.** This is a model issue, not a CSV-tuning issue, if confirmed.
- (b) `surfaces.csv` `RollingResistance` values genuinely too low across the board (a tuning issue)
- (c) `ballMods.RollResistanceMultiplier` getting set to near-zero (would require non-Neutral ball stats — `BallStats.Neutral` resolves to multiplier=1, so this only matters once ball stats are non-neutral)
- (d) `coeff.StopSpeed` mis-applied (e.g. compared against linear speed instead of speed²)
- (e) Surface classification keeps returning a low-`k` surface (e.g. `CartPath`, `k=0.06`) even though the ball is on grass

Logs will tell us which hypothesis lives. No more guessing.

## Reference

- **No Figma reference** — this is purely instrumentation.
- **Existing diagnostic pattern to mirror:** `BallSimulation.DiagErrorLogger` (already wired in `PhysicsLabController.Start()` to `Debug.LogError`). Same null-safe `static System.Action<string>` pattern, gated under `#if UNITY_EDITOR`. New loggers MUST follow this same shape — null-safe, no allocation when null, only fire when wired.
- **Stat-coupling lessons file:** `Docs/Physics/LESSONS_PHYSICS_AERO.md` (read for context on per-club tolerance philosophy; not directly load-bearing for this task).
- **Surface marker / heightmap rationale:** `Docs/Physics/LESSONS_PHYSICS_SURFACE_MARKERS.md` (relevant if surface classification turns out to be the C.1 culprit).

## Architecture context

**Asmdef boundaries affected:** none. All edits are to existing files in existing assemblies. No new files (besides this spec folder), no asmdef edits.

**Existing code referenced (Implementer reads these end-to-end before starting):**
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — Phase 6 entry, `RunRollPhase`, `RunPuttPhase`, `IsPutt` gate, existing `DiagErrorLogger` pattern (~line 24–34).
- `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` — `Build()` static method; resolves `baseVelMps` from override-vs-bundle, applies overpower forgiveness, builds `velocity` and `Spin`.
- `Assets/Scripts/Gameplay/Input/ShotController.cs` — `CommitFlick()` (~line 208), `GetStatBundle()`, `IsPutt` property, `_config.PuttBaseVelocityMps` read site.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `Start()` (where `DiagErrorLogger` is wired today, ~line 215), `SetClub(int)` (~line 363), `HandleShotResolved` (~line 540), `RunSimFromController` (~line 590).

**No edits to:**
- Any test file (the existing 198 EditMode tests must continue to pass; the bit-exact gate is sacred).
- Any `*.csv` (no tuning changes in this task).
- Any `.asmdef`.
- Any scene file (`*.unity`).
- Any prefab.

**Manager APIs added (NEW, all `internal` or `public static` for cross-assembly visibility):**

| Symbol | Where | Purpose |
|---|---|---|
| `BallSimulation.DiagShotLogger` | `BallSimulation.cs` | One-line per-shot summary at sim entry + termination |
| `BallSimulation.DiagRollLogger` | `BallSimulation.cs` | Throttled per-step snapshot inside `RunRollPhase` and `RunPuttPhase` |
| `ShotInputBuilder.DiagBuildLogger` | `ShotInputBuilder.cs` | Snapshot of inputs + resolved values inside `Build()` |
| `ShotController.LogResolution` (public bool) | `ShotController.cs` | When true, emits a checkpoint log at `CommitFlick` entry |

All four MUST be:
- `#if UNITY_EDITOR` guarded (matches existing `DiagErrorLogger`).
- Null-safe (no allocation when not wired).
- Side-effect-free with respect to sim output (no fp math, no random calls, no state mutation).

## Implementation

### Step 0 — Read and confirm the existing pattern

Open `BallSimulation.cs`. Find the existing block (around line 24–34):

```csharp
#if UNITY_EDITOR
    // Wired by the runtime layer (PhysicsLabController) to UnityEngine.Debug.LogError.
    // Null-safe: if not wired, assertion is silently skipped.
    public static System.Action<string> DiagErrorLogger;

    static void CheckTerrainInvariant(IGroundProvider ground, SurfaceType surface, fp3 pos)
    {
        if (DiagErrorLogger == null) return;
        ...
    }
#endif
```

This is the canonical shape. Mirror it for the new loggers.

### Step 1 — Add `BallSimulation.DiagShotLogger` and `DiagRollLogger`

Inside `BallSimulation` (file `Assets/Scripts/Physics/Core/BallSimulation.cs`), add the two new static fields immediately after the existing `DiagErrorLogger`:

```csharp
#if UNITY_EDITOR
    /// <summary>
    /// Wired by the runtime layer to Debug.Log. Emits a single line at sim entry
    /// (with the result of IsPutt() and the gate inputs) and another at termination.
    /// Null-safe; zero overhead when unwired.
    /// </summary>
    public static System.Action<string> DiagShotLogger;

    /// <summary>
    /// Wired by the runtime layer to Debug.Log. Emits a throttled snapshot every
    /// `RollLogStrideSteps` (default 24 = 10 Hz at the 240 Hz sim rate) from
    /// inside RunRollPhase and RunPuttPhase. Null-safe; zero overhead when unwired.
    /// </summary>
    public static System.Action<string> DiagRollLogger;

    /// <summary>How often (in sim steps) DiagRollLogger fires. 24 = 10 Hz at 240 Hz dt.</summary>
    public static int RollLogStrideSteps = 24;
#endif
```

### Step 2 — Emit shot-entry + termination via `DiagShotLogger`

Inside the Phase 6 entry overload of `BallSimulation.Simulate(...)` — the one that begins with `if (IsPutt(input, surfaces))` — add `#if UNITY_EDITOR` log emission at two points:

**(a) At the very top of the method**, immediately before `if (IsPutt(input, surfaces))`:

```csharp
#if UNITY_EDITOR
    if (DiagShotLogger != null)
    {
        SurfaceType originSurface = surfaces.Classify(input.origin.x, input.origin.z);
        fp speedSq = fpMath.Dot(input.velocity, input.velocity);
        fp speed   = fpMath.Sqrt(speedSq);
        fp vySq    = input.velocity.y * input.velocity.y;
        bool puttGateEligibleSurface =
            originSurface == SurfaceType.Green ||
            originSurface == SurfaceType.GreenCollar ||
            originSurface == SurfaceType.Tee;
        bool puttGateSpeedOk = speed.ToFloat() < 8.0f;
        bool puttGateAngleOk = vySq.ToFloat() <= speedSq.ToFloat() * 0.067f;
        DiagShotLogger(
            $"[ShotEntry] origin=({input.origin.x.ToFloat():F2},{input.origin.y.ToFloat():F2},{input.origin.z.ToFloat():F2}) " +
            $"vel=({input.velocity.x.ToFloat():F3},{input.velocity.y.ToFloat():F3},{input.velocity.z.ToFloat():F3}) " +
            $"|v|={speed.ToFloat():F3}m/s spin={input.Spin.Rate.ToFloat():F1}rad/s " +
            $"originSurface={originSurface} " +
            $"isPuttGate=(speedOk={puttGateSpeedOk}, angleOk={puttGateAngleOk}, surfaceOk={puttGateEligibleSurface}) " +
            $"ballMods=(rebound={ballMods.ReboundMultiplier.ToFloat():F3}, roll={ballMods.RollResistanceMultiplier.ToFloat():F3}, windCut={ballMods.WindCutFraction.ToFloat():F3})");
    }
#endif
```

**(b) Wrap each `return new Trajectory(...)` exit point** in the Phase 6 entry method (there are several: bounce-loop water, bounce-loop OOB, bounce-loop stop, bounce-loop max-bounces, airborne-only short-circuit). Add the same `#if UNITY_EDITOR` block right before each `return`:

```csharp
#if UNITY_EDITOR
    if (DiagShotLogger != null)
    {
        DiagShotLogger(
            $"[ShotExit] termination={<termination_local_or_constant>} " +
            $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
            $"finalT={t.ToFloat():F2}s samples={samplesList.Count} hits={hitsList.Count}");
    }
#endif
```

Use the actual `TerminationReason` enum value at each return point for `<termination_local_or_constant>`. For `airborne` early returns where `samplesList`/`hitsList` aren't yet built, log `samples=airborne.samples.Count` instead. The Implementer should pattern-match the local variables that are in scope at each return.

**Do NOT touch `SimulateAirborne`'s internals** — its termination is already covered by the Phase 6 entry's airborne short-circuit return. Adding a log there would introduce per-airborne-step overhead and risk interfering with the bit-exact integrator.

### Step 3 — Emit per-step snapshot from `RunRollPhase` and `RunPuttPhase`

Both phases have the same loop body. Add a throttled emit at the **top of each iteration**, after `surface` and `coeff` are computed but before any motion update:

```csharp
#if UNITY_EDITOR
    if (DiagRollLogger != null && step > 0 && (step % RollLogStrideSteps) == 0)
    {
        // slope gravity tangent magnitude (informational only)
        fp gDotN = fpMath.Dot(gravity, normal);
        fp3 gTan = gravity - normal * gDotN;
        fp slopeMag = fpMath.Sqrt(fpMath.Dot(gTan, gTan));
        fp speed    = fpMath.Sqrt(fpMath.Dot(vel, vel));
        DiagRollLogger(
            $"[<PhaseTag>] t={t.ToFloat():F3}s step={step} " +
            $"pos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
            $"surface={surface} k={coeff.RollingResistance.ToFloat():F3} " +
            $"rollMul={ballMods.RollResistanceMultiplier.ToFloat():F3} " +
            $"stopSpeed={coeff.StopSpeed.ToFloat():F3} " +
            $"|gTan|={slopeMag.ToFloat():F3}m/s² " +
            $"|v|={speed.ToFloat():F4}m/s stopConsec={stopConsecutive}");
    }
#endif
```

`<PhaseTag>` = `"RollStep"` in `RunRollPhase`, `"PuttStep"` in `RunPuttPhase`. Use the existing local `normal` variable from the `(ground is HeightmapData hm) ? hm.SampleNormal(...) : ...` line above. **Move the `normal` computation up if it's currently below the proposed insertion point** — both phases compute it before motion update, so this should already be in scope.

**Crucial:** because the log block is inside `#if UNITY_EDITOR` and only emits on the throttled step, and is null-safe via `DiagRollLogger != null`, it does NOT affect sim output in any way. The bit-exact gate is preserved.

### Step 4 — Add `ShotInputBuilder.DiagBuildLogger`

Inside `ShotInputBuilder` (file `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs`), add a public static field at the top of the class:

```csharp
#if UNITY_EDITOR
    /// <summary>
    /// Wired by the runtime layer to Debug.Log. Emits a snapshot of bundle + inputs +
    /// resolved values at the end of Build(). Null-safe; zero overhead when unwired.
    /// </summary>
    public static System.Action<string> DiagBuildLogger;
#endif
```

Then, **immediately before `return (input, resolved.BallPhysics);`** in `Build()`, add:

```csharp
#if UNITY_EDITOR
    if (DiagBuildLogger != null)
    {
        string clubVel    = bundle.Club.HasValue   ? bundle.Club.Value.BaseVelocityMps.ToFloat().ToString("F2")   : "n/a";
        string putterVel  = bundle.Putter.HasValue ? bundle.Putter.Value.BaseVelocityMps.ToFloat().ToString("F2") : "n/a";
        string overrideStr = baseVelocityOverrideMps.ToFloat().ToString("F2");
        DiagBuildLogger(
            $"[Build] isPutt={bundle.IsPutt} " +
            $"override={overrideStr}m/s clubVel={clubVel}m/s putterVel={putterVel}m/s " +
            $"-> baseVelMps={baseVelMps.ToFloat():F2} " +
            $"effectiveFlick={effectiveFlick.ToFloat():F3} " +
            $"velMultiplier={resolved.VelocityMultiplier.ToFloat():F3} " +
            $"-> velMagnitude={velMagnitude.ToFloat():F2}m/s " +
            $"loft={loftDeg.ToFloat():F1}deg aimYaw={aimYawRadians.ToFloat():F3}rad " +
            $"finalVel=({velocity.x.ToFloat():F2},{velocity.y.ToFloat():F2},{velocity.z.ToFloat():F2})");
    }
#endif
```

`baseVelMps`, `effectiveFlick`, `velMagnitude`, `velocity`, `loftDeg` are all locals already in scope.

### Step 5 — Add `ShotController.LogResolution` flag + emit at `CommitFlick`

Inside `ShotController` (file `Assets/Scripts/Gameplay/Input/ShotController.cs`), add a public field near the existing `DebugFlags` declaration (~line 207):

```csharp
/// <summary>When true, emits a one-line snapshot at CommitFlick entry naming the bundle, override, and gate inputs.</summary>
public bool LogResolution;
```

Then in `CommitFlick()`, **immediately after the line** `var bundle = GetStatBundle();`:

```csharp
#if UNITY_EDITOR
    if (LogResolution)
    {
        string clubVel    = bundle.Club.HasValue   ? bundle.Club.Value.BaseVelocityMps.ToFloat().ToString("F2")   : "n/a";
        string putterVel  = bundle.Putter.HasValue ? bundle.Putter.Value.BaseVelocityMps.ToFloat().ToString("F2") : "n/a";
        UnityEngine.Debug.Log(
            $"[CommitFlick] IsPutt={IsPutt} bundle.IsPutt={bundle.IsPutt} " +
            $"bundle.Club.HasValue={bundle.Club.HasValue} clubVel={clubVel}m/s " +
            $"bundle.Putter.HasValue={bundle.Putter.HasValue} putterVel={putterVel}m/s " +
            $"PowerNormalized={PowerNormalized:F3} flickMag={flickMag:F3} " +
            $"PuttBaseVelocityMps={_config.PuttBaseVelocityMps:F2} " +
            $"baseVelOverride={baseVelOverride.ToFloat():F2}m/s " +
            $"aimYawRadians={_aimYawRadians:F3}rad");
    }
#endif
```

`flickMag`, `baseVelOverride` are already locals at that point. Do NOT change anything else in `CommitFlick`.

### Step 6 — Wire all four loggers in `PhysicsLabController.Start()`

Inside `PhysicsLabController.Start()` (file `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`, ~line 215), after the existing `Golfin.Physics.BallSimulation.DiagErrorLogger = Debug.LogError;` line, add:

```csharp
#if UNITY_EDITOR
    Golfin.Physics.BallSimulation.DiagShotLogger        = Debug.Log;
    Golfin.Physics.BallSimulation.DiagRollLogger        = Debug.Log;
    Golfin.Physics.Stats.ShotInputBuilder.DiagBuildLogger = Debug.Log;
    if (_shotController != null) _shotController.LogResolution = true;
#endif
```

This makes the lab session emit logs by default. Outside the lab (production game), the loggers stay null and the runtime is unaffected.

### Step 7 — Verify bit-exact test gate

Run the full EditMode test suite (`Window > General > Test Runner > Run All`) **before** wiring the loggers in `PhysicsLabController.Start()` (so the loggers are null) AND **after** wiring them. Both runs MUST report `198/198 PASS`. The unit tests don't go through `PhysicsLabController`, so the log emissions aren't triggered during the test suite. If any test fails, the bit-exact gate is broken — STOP and surface in the report.

### Step 8 — Capture diagnostic evidence

After tests pass and code compiles cleanly:

1. Open `Assets/Scenes/LabScaffold.unity`.
2. Use the Hole Picker (`GOLFIN > Physics Lab > Hole Picker`) to load Hole 1.
3. Enter Play mode. Wait 5 seconds for hole load + populator wiring.
4. **Shot 1 (putter on green):**
   - Place ball on Green 1 via the lab's Place Ball dropdown.
   - Cycle club to Putter (last entry).
   - Drag the club handle to ~50% power and flick.
   - Wait for the ball to come to rest (or for sim to terminate).
5. **Shot 2 (driver on tee, full power):**
   - Reset to tee.
   - Cycle club to Driver.
   - Drag club handle to 100% power and flick.
   - Wait for the ball to come to rest.
6. Capture the entire Unity Console output (filter to `[ShotEntry]`, `[Build]`, `[CommitFlick]`, `[RollStep]`, `[PuttStep]`, `[ShotExit]`) and paste verbatim into `IMPLEMENTER_REPORT.md` § "Diagnostic capture" (a new section — see acceptance checklist).
7. **Take a play-mode screenshot** of the lab with Hole 1 loaded and the trajectory of Shot 2 visible. This is just to confirm the lab is in a sane state during capture — it's not a visual-fidelity check.

The actual fix for C.1 / C.2 happens in a **separate spec** the architect writes from those captured logs. **This spec is done when the logs are captured.**

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] `BallSimulation.DiagShotLogger` field added under existing `#if UNITY_EDITOR` block, mirrors `DiagErrorLogger` shape (null-safe, public static Action<string>)
- [ ] `BallSimulation.DiagRollLogger` field added in same block, plus public `RollLogStrideSteps` int (default 24)
- [ ] `[ShotEntry]` log emits at top of Phase 6 entry `Simulate(...)` overload with originSurface, IsPutt-gate breakdown, ballMods snapshot
- [ ] `[ShotExit]` log emits before each `return new Trajectory(...)` in the Phase 6 entry method (count: at least 4 emit sites — water, OOB, stop, max-bounces; airborne-only short-circuit return also counted)
- [ ] `[RollStep]` log emits inside `RunRollPhase` every `RollLogStrideSteps` steps with surface, k, rollMul, stopSpeed, |gTan|, |v|, stopConsec
- [ ] `[PuttStep]` log emits inside `RunPuttPhase` with the same fields, tagged `[PuttStep]` instead of `[RollStep]`
- [ ] `ShotInputBuilder.DiagBuildLogger` field added, `[Build]` log emits at end of `Build()` with full bundle/override/resolved-value snapshot
- [ ] `ShotController.LogResolution` bool field added, `[CommitFlick]` log emits inside `CommitFlick` after `GetStatBundle()` call when `LogResolution=true`
- [ ] All four loggers wired to `UnityEngine.Debug.Log` in `PhysicsLabController.Start()`, plus `_shotController.LogResolution = true` set there
- [ ] EditMode test suite reports `198/198 PASS` after the changes (full Test Runner run, not a subset)
- [ ] No new compiler warnings in Unity Console attributable to this task
- [ ] No `*.csv`, `*.asmdef`, `*.unity`, `*.prefab`, or test file modified
- [ ] Diagnostic capture from Shot 1 (putter) is in `IMPLEMENTER_REPORT.md` § "Diagnostic capture" with all expected log tags present (`[CommitFlick]`, `[Build]`, `[ShotEntry]`, `[ShotExit]`, plus either `[PuttStep]` if putt path took or `[RollStep]` if airborne path took)
- [ ] Diagnostic capture from Shot 2 (driver) is in the same section with `[CommitFlick]`, `[Build]`, `[ShotEntry]`, `[ShotExit]`, and at least one `[RollStep]` line
- [ ] Play-mode screenshot of the lab with Hole 1 loaded and a trajectory rendered is in `screenshots/`
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Scripts/Physics/Core/BallSimulation.cs` — add 3 static fields, 2 emit sites at sim entry/exit, 2 emit sites inside roll/putt phases. **Only additions; no existing logic touched.**
- `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` — add 1 static field, 1 emit site at end of `Build()`. **Only additions.**
- `Assets/Scripts/Gameplay/Input/ShotController.cs` — add 1 public bool field, 1 emit site after `var bundle = GetStatBundle();` in `CommitFlick()`. **Only additions.**
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — add 4-line wire-up block in `Start()` next to existing `DiagErrorLogger` wire. **Only additions.**

## Out of scope (do NOT do these)

- **Do NOT fix C.1 or C.2.** This is a diagnosis-only task. Any "while we're here" fix attempt is a hard FAIL.
- **Do NOT change** `surfaces.csv`, `putt.csv`, `controls.csv`, or any other CSV.
- **Do NOT change** `BallSimulation` sim logic — including `RunRollPhase`'s motion-update math, `RunPuttPhase`'s motion-update math, the bounce-loop `cr` computation, `IsPutt`'s gate conditions, or the airborne RK4 integrator.
- **Do NOT change** `ShotInputBuilder.Build`'s velocity-resolution math, overpower-forgiveness math, or spin construction.
- **Do NOT change** `ShotController.CommitFlick`'s control flow — only insert the log block at the specified point.
- **Do NOT add** any new asmdef, scene, or prefab.
- **Do NOT change** any existing test (the bit-exact gate is sacred; if a test fails after instrumentation, the instrumentation is wrong).
- **Do NOT add** logging anywhere not specified in this spec. We want a focused, predictable log volume so the architect can read the captures cleanly.
- **Do NOT add** runtime UI for the loggers (no inspector field on `PhysicsLabUI`, no debug panel button). The lab wires them on at `Start` unconditionally in editor; that's enough for diagnosis.
- **Do NOT log inside `SimulateAirborne`.** The airborne RK4 path runs at 240 Hz for up to 60 s — even a throttled log there risks console flooding and adds non-trivial overhead per shot. The Phase 6 entry's `[ShotEntry]` and `[ShotExit]` already capture airborne termination.

## Pipeline lessons applied

From `Docs/Diagnostics/PIPELINE_LESSONS.md`:

- **Lesson F (architect overthinks past Cesar's diagnosis):** Cesar's diagnosis-first instruction in the Notion entry is treated as ground truth. This spec is instrumentation only; it does not relitigate whether C.1 / C.2 are real (Cesar already saw them in lab).
- **Lesson G (no thinking-aloud in specs):** scanned, none present.
- **Lesson H (architect must verify visual claims):** N/A (no Figma).
- **Implementer rule from `golfin-implementer.md`:** "Never invent values for things you couldn't verify." If the diagnostic capture in Step 8 has missing log tags (e.g. no `[PuttStep]` for the putter shot because the IsPutt gate failed), surface that in `IMPLEMENTER_REPORT.md` "Open questions for Architect" rather than retrying with adjusted parameters. The architect needs to see the exact failure mode.

## Mid-task escalation paths

- **If EditMode tests fail after instrumentation:** STATUS → `READY_FOR_ARCHITECT_REVIEW`, list which tests failed and which log emit site was last edited. Do NOT keep iterating on the instrumentation hoping for a green run; the bit-exact gate is structural and any failure means I (architect) need to look at it.
- **If diagnostic capture in Step 8 throws exceptions or the lab crashes mid-shot:** STATUS → `IMPLEMENTER_BLOCKED`. Capture the exception stack trace in `IMPLEMENTER_REPORT.md` "Open questions for Architect" and stop.
- **If IsPutt gate denies the putter shot in Shot 1 (origin surface != Green/GreenCollar/Tee even though ball is visibly on green):** **this is exactly the diagnostic evidence we need.** Capture the `[ShotEntry]` log showing the actual `originSurface` value. Mark all checklists items related to log presence PASS. Surface the finding in the report's "Implementation summary" — that's the diagnosis the architect was hunting for, not a failure.
