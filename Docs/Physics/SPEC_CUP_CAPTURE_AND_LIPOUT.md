# SPEC — In-Sim Cup Capture + Lip-Out (`cup_capture_and_lipout`)

**Priority:** HIGH (Cesar, 2026-08-05) — ball rolls over the hole and keeps rolling even when it should drop in.
**Type:** Physics task. Runs in a direct Claude Code session (NOT the UI subagent pipeline).
**Ban override:** CLAUDE.md / PIPELINE_HARDENING §7 bans edits to `Assets/Scripts/Physics/`. **Cesar explicitly authorizes edits to `Assets/Scripts/Physics/` for this task** — that ban exists to stop UI tasks from drifting into physics; this IS a physics task. All other standing bans stay in force (no `*Gate` scenarios in `Scenarios.cs`, no `M_Splash*.mat` touches, commit `.cs.meta` with every `.cs` — Lesson R).

---

## §0 Symptom (as reported)

The ball rolls over the cup and continues rolling, even when its speed is low enough that it should fall in. There is no falling animation and no reaction from the hole. (It "supposedly" used to at least disappear.)

## §1 Root cause — three stacked problems (all confirmed by code read, 2026-08-05)

1. **`BallSimulation` is cup-blind.** Neither `RunPuttPhase` (`Assets/Scripts/Physics/Core/BallSimulation.cs`, ~line 765) nor `RunRollPhase` (~line 584) knows the cup exists. Grep the file: zero matches for `cup|hole`. The integrator rolls the ball straight across the pin XZ until friction stops it. The ball can NEVER physically fall in.
2. **Cup detection is a post-hoc scan that cannot alter the path.** `BallStateMachine.OnTrajectoryComputed` (`Assets/Scripts/Gameplay/Loop/BallStateMachine.cs`, default-case scan ~lines 176–211) walks the finished `trajectory.samples` with `RealCupDetector.IsInCup(pos, ballRadius, velocity)`. It may flag terminal `InCup`, but it MUST NOT modify the trajectory (documented invariant, header lines 15–20) — and `BallAnimator.Play` (`Assets/Scripts/Physics/Viewer/BallAnimator.cs`) plays the **full** roll-past trajectory. The `InCup` transition only fires on the animator falling edge in `Tick()` — i.e. **after** the player has watched the ball roll over the hole and stop somewhere else.
3. **Overspeed is a silent fly-over by design (v1).** `RealCupDetector` (`Assets/Scripts/Gameplay/Loop/RealCupDetector.cs`, line 62): speed > `CupCaptureSpeed` (1.5 m/s, architect-locked 2026-05-14, Penner 2002 / USGA anchor) → `return false`, ball continues uninterrupted. No lip-out reaction exists anywhere.

**Suspected 4th problem — verify at step 0 (§3):** the detector's height gate (`position.y > _pin.y + ballRadius → reject`, RealCupDetector.cs line 75) compares against the **authored pin Y** (`HoleContext.PinWorld`, installed in `PhysicsLabController.OnHoleLoaded` ~line 1910). Rolling samples sit at `BakedHeightProvider.SampleHeight + ballRadius`. If the baked green height at the pin is even a few mm above the authored pin Y, EVERY rolling sample fails the gate and `InCup` never fires at all — which would explain why the "disappear" behaviour regressed after green height baking. The in-sim design below sidesteps this class of bug entirely (§4.2).

## §2 Design decision (architect-locked with Cesar, 2026-08-05)

Move cup interaction **into the simulation**, where the ball's path is actually decided:

- **Capture** (speed ≤ gate, over open cup): terminate the trajectory at the cup with a synthesized fall-in, so the animation naturally shows the ball dropping below the lip.
- **Lip-out** (speed > gate, crossing the cup mouth): deterministic one-time velocity deflection — ball gets visibly kicked off its line, keeps rolling. (Cesar chose "lip-out deflection" over dampening-only and over full rim-collider physics.)

`RealCupDetector` + the `BallStateMachine` sample scan stay as-is (fallback + bot/test seams). The sim becomes the primary authority.

## §3 Step 0 — reproduce & diagnose BEFORE building (30 min, do not skip)

1. In the lab, fire a slow putt (~1 m/s) straight over the pin on a real baked hole. Confirm the symptom.
2. Add a temporary closest-approach log via the existing `DiagShotLogger`/`DiagRollLogger` seam (BallSimulation.cs line ~34): min XZ distance to pin, `sample.y − (pin.y + ballRadius)`, speed at closest approach.
3. Record in `IMPLEMENTER_REPORT`/commit message whether the post-hoc scan currently fires `InCup` at all, and the measured Y delta. This confirms or clears the §1.4 height-gate suspicion and gives the "before" evidence for the fix.

## §4 Implementation

### §4.1 `CupSpec` struct — NEW file `Assets/Scripts/Physics/Core/CupSpec.cs`

Pure fp value type (Physics.Core has no engine refs — follow `PuttConfig.cs` style):

```csharp
public readonly struct CupSpec
{
    public readonly bool Enabled;
    public readonly fp3  Pin;            // authored pin world position
    public readonly fp   Radius;         // default 0.054 m (regulation 4.25in mouth)
    public readonly fp   CaptureSpeed;   // default 1.5 m/s (PuttConfig.CupCaptureSpeed)
    public readonly fp   Depth;          // default 0.10 m (regulation ≥4in depth)
    public readonly fp   LipRestitution; // default 0.35  (design value — see §4.6)
    public readonly fp   LipSpeedDamping;// default 0.70
    public readonly fp   LipPopVy;       // default 0.30 m/s
    public static CupSpec Disabled { get; }  // Enabled=false
}
```

### §4.2 Sim entry — new overload, bit-exact legacy gate

Add a 10-arg `BallSimulation.Simulate(..., ITreeObstacleProvider trees, in CupSpec cup)` next to the existing 9-arg Phase-7 entry (BallSimulation.cs line 151). The 9-arg overload forwards `CupSpec.Disabled`. **Gate (same pattern as `trees=null`, Phase 6/7 headers):** `cup.Enabled == false` → bit-exact identical output to today. This is the blocking determinism test (§6.3).

Thread `cup` into `RunPuttPhase` and `RunRollPhase` only (airborne chip-ins are out of scope v1 — note in code; the fallback scan still covers a freak flying capture via its height gate).

**No Y gate in-sim.** In roll/putt phases the ball is on the ground by construction (`pos.y = SampleHeight + ballRadius`), so the check is XZ-only against `cup.Pin` — immune to the §1.4 baked-height-vs-pin-Y mismatch.

### §4.3 Per-step cup check (inside the roll/putt integration loops, after position update)

```
distSq = (pos.xz − pin.xz)²   // segment check below for tunneling
speedSq = |vel|²
captureZone = dist < cup.Radius − ballRadius        // effRadius, same geometry as RealCupDetector
lipZone     = dist < cup.Radius                     // cup mouth
if (captureZone && speedSq ≤ CaptureSpeed²)  → CAPTURE (§4.4)
else if (lipZone && speedSq > CaptureSpeed² && !_lipFiredThisCrossing) → LIP-OUT (§4.5)
```

- **Tunneling guard:** putt dt is 1/240 s (`maxPuttSteps = 60 * 240`), so a 1.5 m/s ball moves ~6 mm/step vs a 66 mm capture window — but roll-phase entry speeds can be higher. Use closest-point-on-segment (prev→curr XZ) to pin for the zone tests, not just the endpoint.
- **Lip-out single-fire:** set `_lipFiredThisCrossing` on trigger; clear it once `dist > cup.Radius + 0.02 m`. One impulse per crossing, deterministic.
- Slow graze in the lip ring (speed ≤ gate but outside effRadius): no interaction v1 — rolls past, matches today.

### §4.4 Capture → synthesized fall-in + new termination

- Truncate integration at the capture step.
- Append fall-in samples at sim dt: Y drops under gravity from capture Y to `pin.y − cup.Depth + ballRadius` (T_fall = √(2·Depth/g) ≈ 0.14 s); XZ lerps from capture point to `pin.xz` over the same window. Pure fp, no Random/Time.
- Append `TerrainHit(t, cupBottom, vIn, fp3.Zero, SurfaceType.Green, IsStop: true)`.
- Return `Trajectory(..., TerminationReason.CupCapture, ...)`. **Add `CupCapture` to `TerminationReason`** (`Assets/Scripts/Physics/Core/Trajectory.cs`, enum line 56 — append at END of enum; existing values are order-sensitive).
- Emit a `[ShotExit] termination=CupCapture` DiagShotLogger line matching the existing exit-log idiom.

### §4.5 Lip-out deflection (deterministic, one-shot)

Decompose horizontal velocity at the trigger step: `n` = normalized XZ vector pin→ball, `vRad = (v·(−n))·(−n)` (component toward pin), `vTan = vXZ − vRad`. Then:

```
vXZ' = −LipRestitution · vRad + vTan      // radial component bounces off the far rim
v'   = (vXZ'.x, LipPopVy, vXZ'.z) scaled so |v'| = LipSpeedDamping · |v|
```

Ball continues integrating from there — it visibly kicks sideways/off-line and loses ~30% speed, then friction plays out normally (it may even come back and drop on the rebound if slow enough — that's correct and delightful). The small `LipPopVy` gives the rattle read; roll integration re-projects onto the surface next step (line 824 `vel = vel − normal·(vel·normal)` already handles this — verify the pop survives at least one sample so the animator shows it; if the projection kills it same-step, apply the pop as a Y offset on the emitted sample instead).

### §4.6 Constants & config (Lesson K compliance)

- `CaptureSpeed` 1.5 m/s: already cited (Penner, A.R. (2002), Can. J. Phys. 80(2):83–96; USGA ≈5 ft/s) — reuse `PuttConfig.CupCaptureSpeed`, do not duplicate.
- `Depth` 0.10 m: USGA regulation cup depth ≥ 4 in (0.1016 m) — real-world citation, hard constant.
- `LipRestitution` 0.35 / `LipSpeedDamping` 0.70 / `LipPopVy` 0.30 m/s: **design-feel values, ARCHITECT-TUNABLE**, initial guesses to be verified in the lab (§7). Not physically calibrated — mark them as such in comments (Lesson K: cite what's real, flag what's tuned).
- putt.csv: add global rows `cup_depth_m`, `lip_restitution`, `lip_speed_damping`, `lip_pop_vy_mps`, parsed in `PhysicsConfigLoader` beside the existing `cup_capture_speed` global-row handling (`Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs`, ~line 263). Store them on `PuttConfig` (new fields, defaulted in `PuttConfig.Default`).

### §4.7 `BallStateMachine` — honor the new termination

In `OnTrajectoryComputed`'s termination switch (BallStateMachine.cs ~line 138): add `case TerminationReason.CupCapture:` → `terminalState = BallState.InCup`, `terminalPos/Time = trajectory.finalPosition/finalTime`, `terminalSurface = SurfaceType.Green`. Keep the default-case sample scan unchanged as fallback. Because the trajectory now ENDS at the cup, the existing animator-falling-edge mechanism fires `InCup` at the moment the ball finishes dropping — no timing changes needed downstream (`HoleCompletionBridge`, `LoopCameraDirector`, modal flow all already key off `InCup`).

### §4.8 Caller wiring — `PhysicsLabController`

- Build `_cupSpec` in `OnHoleLoaded` at the exact spot the `RealCupDetector` is installed (~line 1910), from the same inputs: `HoleContext.PinWorld` + `RealCupDetector.DefaultCupRadius` + `PuttCfg.CupCaptureSpeed` (+ new PuttConfig lip fields). Reset to `CupSpec.Disabled` in the flat-ground fallback path (~line 2119, where `NullCupDetector` is restored).
- Pass it in `RunSimFromController` (line 1392, production shot path) and the preset sim path (line ~1523). The two flat-ground helper sims (lines ~610, ~1433) stay 8/9-arg — no cup on synthetic flat ground.
- `HandleShotComplete` `InCup` case (line ~1316): currently empty by design (modal owns re-arm) — leave it. Verify the ball at cup bottom is occluded by the green mesh from the chase camera. If any lip clipping is visible, hide the animator instance on the `InCup` state change (small addition in the same case) — do NOT add scene-side ball hacks.

## §5 Files touched (expected)

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Core/CupSpec.cs` (+.meta) | NEW — cup value type |
| `Assets/Scripts/Physics/Core/Trajectory.cs` | `TerminationReason.CupCapture` (append-only) |
| `Assets/Scripts/Physics/Core/BallSimulation.cs` | 10-arg overload; cup checks in RunPuttPhase/RunRollPhase; fall-in synth; lip-out |
| `Assets/Scripts/Physics/Core/PuttConfig.cs` | CupDepth/Lip* fields + defaults |
| `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` | parse new putt.csv globals |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | `CupCapture` case → `InCup` |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | build/pass `CupSpec`; occlusion check |
| `Assets/Data/.../putt.csv` (wherever putt.csv lives — locate, don't guess) | new global rows |
| `Assets/Scripts/Physics/Tests/CupCaptureSimTests.cs` (+.meta) | NEW — §6 |

## §6 Tests (Golfin.Physics.Tests — new `CupCaptureSimTests.cs`, plus one BallStateMachine test)

1. **Slow putt drops:** flat ground, pin 2 m ahead, putt at 1.0 m/s through pin → `termination == CupCapture`, `finalPosition` within ε of `(pin.x, pin.y − Depth + ballRadius, pin.z)`, last samples descend monotonically.
2. **Fast putt lips out:** 3.0 m/s through pin → no `CupCapture`; velocity after the crossing shows reversed radial sign and `|v'| ≈ 0.70·|v|`; final rest position is off the original line.
3. **Bit-exact legacy gate (BLOCKING):** same input via 9-arg vs 10-arg with `CupSpec.Disabled` → identical sample lists bit-for-bit (the Phase-6/7 gate pattern).
4. **Boundary speed:** capture at exactly 1.5 m/s, reject at 1.5 + ε (mirrors `RealCupDetector_BoundarySpeed_Deterministic`).
5. **Single-fire lip-out:** consecutive in-mouth steps produce exactly one impulse; re-entry after exiting `Radius + 0.02` fires again.
6. **Tunneling guard:** synthetic high-speed roll step that straddles the cup in one dt still triggers the lip zone (segment test).
7. **BallStateMachineTests addition:** trajectory with `termination = CupCapture` → terminal `InCup` without relying on the sample scan.
8. Existing `RealCupDetectorTests` + `RollAndPuttTuningTests` + `PuttTests` stay green untouched.

## §7 Acceptance (Cesar reviews from chat — video, not stills)

Three lab clips (standard capture rules — Unity Recorder/BotVideoRecorder path, MP4s to the task `videos/` folder; **no hand-rolled capture**, CLAUDE.md § Screenshots):

1. Slow putt (~1 m/s): ball reaches cup, drops below the lip on screen, `InCup` fires as the drop finishes, hole-complete modal appears. Ball not visible floating past the hole at any point.
2. Fast putt (~3 m/s): ball crosses the mouth, visibly deflects off-line with a small hop, keeps rolling, NO hole-complete.
3. Mid putt at ~1.4 m/s: captured (just under gate).

Plus: the §3 before/after DiagShotLogger lines, and the §6.3 bit-exact test result quoted in the report.

## §8 Out of scope (backlog)

- Airborne chip-in capture (flying ball lands directly in cup) — v2; needs entry-angle gating.
- Cup cavity in the green mesh / flag pole interaction / lip-in rattle-around-the-rim animation.
- 1v1 bot awareness of lip-out in `VersusBot` shot planning (bot putts are simmed through the same path, so behaviour is consistent — just not strategically anticipated).

## Kickoff

`Read Docs/Physics/SPEC_CUP_CAPTURE_AND_LIPOUT.md and implement. Step 0 (§3) first — post the diagnosis before writing the fix.`
