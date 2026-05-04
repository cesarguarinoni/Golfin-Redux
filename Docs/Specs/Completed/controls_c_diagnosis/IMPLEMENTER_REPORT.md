# Implementer Report — `controls_c_diagnosis`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

Added four diagnostic loggers (`DiagShotLogger`, `DiagRollLogger` to `BallSimulation.cs`; `DiagBuildLogger` to `ShotInputBuilder.cs`; `LogResolution` bool + `[CommitFlick]` emit to `ShotController.cs`) all under `#if UNITY_EDITOR` guards, null-safe, zero-overhead when unwired. Wired all four to `Debug.Log` / `LogResolution = true` in `PhysicsLabController.Start()` after the existing `DiagErrorLogger` wire. Unity compiled the changes with `ExitCode: 0` (Tundra build, no errors, zero `error CS` matches in the log). No existing sim logic was touched — only additive `#if UNITY_EDITOR` blocks.

Play-mode diagnostic capture (Steps 8 items) could not be completed: the Unity Editor windows are on a separate macOS Space and neither MCP tools (not available in this subagent session) nor macOS screencapture could reach them. Per the spec's own language: "*Cesar fires a putter shot + a long fairway shot… copies the console output into the implementer report.*" Those items are marked FAIL below for routing to architect-review.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Core/BallSimulation.cs` | Modified — added `DiagShotLogger`, `DiagRollLogger`, `RollLogStrideSteps` static fields; `[ShotEntry]` emit at Phase 6 entry; `[ShotExit]` emits at 6 Trajectory return sites; `[RollStep]` emit in `RunRollPhase`; `[PuttStep]` emit in `RunPuttPhase` |
| `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` | Modified — added `DiagBuildLogger` static field; `[Build]` emit before `return (input, resolved.BallPhysics)` |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | Modified — added `LogResolution` public bool field; `[CommitFlick]` emit after `var bundle = GetStatBundle()` |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Modified — added 4-line wire-up block in `Start()` after existing `DiagErrorLogger` line |
| `Docs/Specs/Active/controls_c_diagnosis/HEARTBEAT.log` | Created — activation + progress timestamps |
| `Docs/Specs/Active/controls_c_diagnosis/IMPLEMENTER_REPORT.md` | Created — this file |

## Screenshot

- **Captured at:** N/A — see "Open questions for Architect" below; Unity windows are on another macOS Space; screenshot capture failed
- **Scene loaded:** `Assets/Scenes/LabScaffold.unity` (confirmed via Unity log: `[PhysicsLab] No hole scene loaded at startup — flat-ground fallback.`)
- **Play mode:** No — Unity exited play mode during the domain reload triggered by file changes
- **Hole loaded (if applicable):** Not captured — requires play mode shot cycle

## Acceptance checklist (copy from SPEC.md, fill every line)

| Item | Result | Justification |
|---|---|---|
| `BallSimulation.DiagShotLogger` field added under existing `#if UNITY_EDITOR` block, mirrors `DiagErrorLogger` shape (null-safe, public static Action<string>) | PASS | Added immediately after `DiagErrorLogger` at line 29 of `BallSimulation.cs`; same shape: `#if UNITY_EDITOR`, `public static System.Action<string>`, XML summary doc, null-check before call |
| `BallSimulation.DiagRollLogger` field added in same block, plus public `RollLogStrideSteps` int (default 24) | PASS | `DiagRollLogger` at line 36, `RollLogStrideSteps = 24` at line 39 of `BallSimulation.cs`; both inside the same `#if UNITY_EDITOR` block |
| `[ShotEntry]` log emits at top of Phase 6 entry `Simulate(...)` overload with originSurface, IsPutt-gate breakdown, ballMods snapshot | PASS | Emit block inserted before `if (IsPutt(input, surfaces))` in the 8-arg Phase 6 overload; logs `originSurface`, `puttGateSpeedOk`, `puttGateAngleOk`, `puttGateEligibleSurface`, all three `ballMods` fields |
| `[ShotExit]` log emits before each `return new Trajectory(...)` in the Phase 6 entry method (count: at least 4 emit sites — water, OOB, stop, max-bounces; airborne-only short-circuit return also counted) | PASS | 5 emit sites added: airborne non-HitGround short-circuit, Water (bounce loop), OOB (bounce loop), BallStopped (bounce loop, speed check), non-HitGround secondary airborne, MaxBouncesExceeded — all 6 direct Trajectory return sites covered |
| `[RollStep]` log emits inside `RunRollPhase` every `RollLogStrideSteps` steps with surface, k, rollMul, stopSpeed, \|gTan\|, \|v\|, stopConsec | PASS | Block inserted in `RunRollPhase` after `normal` computed, before `vel = vel - normal * …`; logs all 7 required fields; throttled with `step > 0 && (step % RollLogStrideSteps) == 0` |
| `[PuttStep]` log emits inside `RunPuttPhase` with the same fields, tagged `[PuttStep]` instead of `[RollStep]` | PASS | Same block structure as `[RollStep]` inserted in `RunPuttPhase` at the equivalent position; tag is `[PuttStep]` |
| `ShotInputBuilder.DiagBuildLogger` field added, `[Build]` log emits at end of `Build()` with full bundle/override/resolved-value snapshot | PASS | `DiagBuildLogger` static field added at top of class under `#if UNITY_EDITOR`; emit block placed immediately before `return (input, resolved.BallPhysics)` logging all 9 required fields (isPutt, override, clubVel, putterVel, baseVelMps, effectiveFlick, velMultiplier, velMagnitude, loft, aimYaw, finalVel) |
| `ShotController.LogResolution` bool field added, `[CommitFlick]` log emits inside `CommitFlick` after `GetStatBundle()` call when `LogResolution=true` | PASS | `LogResolution` public bool field added after `DebugFlags` declaration; emit block under `#if UNITY_EDITOR` with inner `if (LogResolution)` immediately after `var bundle = GetStatBundle();`; logs IsPutt, bundle.IsPutt, Club.HasValue, clubVel, Putter.HasValue, putterVel, PowerNormalized, flickMag, PuttBaseVelocityMps, baseVelOverride, aimYawRadians |
| All four loggers wired to `UnityEngine.Debug.Log` in `PhysicsLabController.Start()`, plus `_shotController.LogResolution = true` set there | PASS | Added 4-line block in `Start()` after existing `DiagErrorLogger = Debug.LogError` line: `DiagShotLogger = Debug.Log`, `DiagRollLogger = Debug.Log`, `ShotInputBuilder.DiagBuildLogger = Debug.Log`, `if (_shotController != null) _shotController.LogResolution = true`; all inside `#if UNITY_EDITOR` |
| EditMode test suite reports `198/198 PASS` after the changes (full Test Runner run, not a subset) | FAIL | Could not run: MCP tools (`mcp__unity__script-execute`) not available in this subagent session; Unity is now in Edit mode after domain reload (compilation succeeded with ExitCode=0, `LogAssemblyErrors (0ms)`) — test run must be performed by Cesar or re-run in the next pipeline stage |
| No new compiler warnings in Unity Console attributable to this task | PASS | Unity log shows `LogAssemblyErrors (0ms)` and zero `error CS` / `warning CS` matches across the 14,378-line log; Tundra build ExitCode=0 |
| No `*.csv`, `*.asmdef`, `*.unity`, `*.prefab`, or test file modified | PASS | Only 4 `.cs` files modified (confirmed by `git diff --name-only`); no CSV, asmdef, scene, prefab, or test files in the diff |
| Diagnostic capture from Shot 1 (putter) is in `IMPLEMENTER_REPORT.md` § "Diagnostic capture" with all expected log tags present | FAIL | Could not capture: Unity windows are on another macOS Space; no MCP play-mode interaction available in this subagent context; Cesar must fire Shot 1 per spec Step 8 and paste `[CommitFlick]`, `[Build]`, `[ShotEntry]`, `[ShotExit]`, `[PuttStep]`/`[RollStep]` output here |
| Diagnostic capture from Shot 2 (driver) is in the same section with `[CommitFlick]`, `[Build]`, `[ShotEntry]`, `[ShotExit]`, and at least one `[RollStep]` line | FAIL | Same reason as Shot 1 above; requires Cesar to fire Shot 2 per spec Step 8 |
| Play-mode screenshot of the lab with Hole 1 loaded and a trajectory rendered is in `screenshots/` | FAIL | Could not capture: Unity windows are on another macOS Space inaccessible via `screencapture -l <wid>`; all screenshot paths failed; macOS 15 deprecated `CGWindowListCreateImage` |
| Spec deviations (if any) are flagged at the bottom of the report with justification | PASS | No deviations from spec in the code changes; deviation in workflow (screenshot/captures blocked) is documented below |

## Known FAIL items

1. **EditMode test suite (198/198)** — MCP tools not available in subagent session; Unity compiled cleanly (ExitCode=0, zero assembly errors). Cesar should run `Window > General > Test Runner > Run All` and confirm 198 pass. If any fail, route back to implementer.

2. **Diagnostic capture Shot 1 (putter)** — Cesar must perform these steps per spec Step 8: enter play mode in LabScaffold, load Hole 1 via GOLFIN > Physics Lab > Hole Picker, place ball on Green 1, select Putter, flick at ~50% power, wait for ball to rest, then filter Unity Console for `[CommitFlick] [Build] [ShotEntry] [ShotExit] [PuttStep]` and paste verbatim in the § "Diagnostic capture" section below.

3. **Diagnostic capture Shot 2 (driver)** — Same process as Shot 1 but reset to tee, select Driver, full power flick.

4. **Play-mode screenshot** — Cesar should take a screenshot via `GOLFIN > Screenshot > Capture Game View` after Shot 2 trajectory is visible, then copy to `Docs/Specs/Active/controls_c_diagnosis/screenshots/`.

## Spec deviations

The ShotExit count has 6 emit sites (not exactly 4 as minimum stated in spec): airborne non-HitGround short-circuit (1), Water (2), OOB (3), BallStopped-in-bounce (4), non-HitGround secondary airborne (5), MaxBouncesExceeded (6). This exceeds the spec minimum of 4 — conformant.

Note: The `[ShotExit]` was NOT added before the `RunPuttPhase(...)` return or the `RunRollPhase(...)` return since those are function calls (not direct `return new Trajectory(...)`), and the spec says to wrap each `return new Trajectory(...)`. This is correct behavior per spec.

## Diagnostic capture

*To be filled by Cesar after firing shots in LabScaffold with Hole 1 loaded per spec Step 8.*

### Shot 1 — Putter on Green

```
[PENDING: Cesar to paste Unity Console output here]
Filter: [CommitFlick], [Build], [ShotEntry], [ShotExit], [PuttStep] or [RollStep]
```

### Shot 2 — Driver on Tee (full power)

```
[PENDING: Cesar to paste Unity Console output here]
Filter: [CommitFlick], [Build], [ShotEntry], [ShotExit], [RollStep]
```

## Console output

Compilation log (from Unity Editor.log, 2026-05-04 07:48):
```
[ScriptCompilation] Requested script compilation because: Assetdatabase observed changes in script compilation related files
CompileScripts: 57.927ms
*** Tundra build success (0.28 seconds), 1 items updated, 1112 evaluated
Reloading assemblies after finishing script compilation.
LogAssemblyErrors (0ms)
```

No `error CS` or `warning CS` entries found anywhere in the 14,378-line log. Zero assembly errors. ExitCode: 0.

## Open questions for Architect

1. **Test runner not run** — this subagent session does not have `mcp__unity__script-execute` available. The Unity Editor is in Edit mode and compiled cleanly. Cesar must run `Window > General > Test Runner > Run All` to confirm 198/198 pass before treating this task as verified. If tests fail, the architect should investigate whether any `#if UNITY_EDITOR` block inadvertently altered a test-visible symbol.

2. **Diagnostic captures blocked** — the spec says "Cesar fires a putter shot + a long fairway shot… copies the console output into the implementer report." All three diagnostic-capture checklist items (Shot 1, Shot 2, screenshot) are marked FAIL because I cannot execute them from this subagent session. Cesar must perform Step 8 manually and paste the output into § "Diagnostic capture" above.

3. **Screenshot path** — per spec, the screenshot should go in `Docs/Specs/Active/controls_c_diagnosis/screenshots/`. The directory has been created.
