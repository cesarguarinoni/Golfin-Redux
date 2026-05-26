# SPEC — `spin_and_shot_shape_wiring`

> **Status:** `SPEC_READY` (Queued, authored 2026-05-26 by architect). Q-locks all confirmed by Cesar same session. Fires next session as full Tier-3 pipeline kickoff.

## One-line

Wire the existing `SpinPanelWidget` 5-position spin input through `ShotController.CommitFlick` and `ShotInputBuilder.Build` so spin.y modulates backspin magnitude (with sign-flip allowed for true topspin) and spin.x tilts the spin axis around the velocity vector to produce in-flight draw/fade via Magnus. Putts remain `SpinState.None`. Visual gate is a 5-stroke bot scenario with captioned video, not a manual playtest.

## Goal

The spin UI has existed since Phase 8 but never reached the physics — `ShotInputBuilder.Build` hardcodes the spin axis to the pure right-vector and derives magnitude solely from `bundle.Club.Value.BaseBackspinRpm × resolved.SpinMagnitudeMultiplier`. After this spec lands:

- `SpinContext.Spin.y > 0` (player pressed top of ball) → reduces backspin and, at the extreme, flips sign to true topspin → ball flies lower, runs further on landing.
- `SpinContext.Spin.y < 0` (bottom of ball) → boosts backspin → ball climbs higher, stops faster.
- `SpinContext.Spin.x != 0` → tilts spin axis orbitally around the velocity vector → ball curves in flight (Magnus). `spin.x < 0` = draw (curves left for a right-handed swing), `spin.x > 0` = fade (curves right).

Putters keep `SpinState.None` (deferred — short-game spin is a separate ticket per Q-lock NOTES §Out of scope).

The audit (`Docs/Physics/STAT_LANE_AUDIT.md`) covered Ball.Spin (the character/ball **multiplier** lane) and classified it `Justified-as-is`. This spec is orthogonal: it wires the **player-input spin** lane that the audit explicitly did not address.

## Q-locks (recorded 2026-05-26 with Cesar)

| Q | Decision |
|---|---|
| Q1 — Tuning starts + home | Start at `magScaleSlope=1.5f`, `maxTiltRad=0.3f` (~17°). Put both in `ControlsConfig.Default` + `controls.csv`. **No Dashboard live-tune in v1** — ControlsConfig isn't dashboard-bound today; CSV-edit + scene-restart tuning loop is acceptable. Flag adding a `CONTROLS` Dashboard section as a follow-up if Cesar wants live tuning during playtest. |
| Q2 — Topspin behavior at max +y | **Sign-flip allowed (Option a).** Slope `1.5` means `spinY=+1 → magScale=-0.5` (topspin at half magnitude — strong feel without exceeding real-world topspin RPMs at driver baseline 2686 RPM × 0.5 = 1343 RPM). `spinY=-1 → magScale=+2.5` (super backspin). Asymmetric on purpose: real golf backspin baseline is much higher than achievable topspin. |
| Q3 — Tilt rotation axis | **Orbital rotation around velocity vector (Option a).** Tilts the backspin axis sideways relative to flight direction → Magnus force gains lateral component → classic golf curl over the whole arc. World-up yaw would feel tennis-ball-wrong. |
| Q4 — Symmetry | **Symmetric** (draw at `spinX=-1` ≡ fade at `spinX=+1` in magnitude). Handedness asymmetry is a future stat-coupling concern, not v1. |
| Q5 — Pipeline + visual gate scope | **TIER 3 + bot-driven visual gate (mod of Option b).** No manual 5-shot playtest. The bot fires 5 driver shots from the same tee on Hole 1 with `ResetToTee()` between, varying `SpinContext.Spin` per stroke. Single captioned video. Cesar approves from video, not manual play. |
| Plumbing | **Parameter pass, not bus-state.** `ShotController.CommitFlick` reads `SpinContext.Spin`, passes `Vector2 spinInput` to `ShotInputBuilder.Build`. Dep direction is UI → Physics (correct), so Lesson W's asmdef-veto workaround does not apply. Cleaner than introducing a `SpinInputBus.Current`. |

## Architecture context

**Asmdef boundaries affected:**
- `Golfin.Physics.Stats` (owns `ShotInputBuilder`) — signature extension, no new refs needed (`Vector2` is `UnityEngine.Vector2`, already in scope via the existing `Build` parameter list using fp types).
- `Golfin.Physics.Math` (owns `fpMath`) — new public helper `Rotate(fp3 v, fp3 k, fp angleRad)` using Rodrigues' formula. Self-contained, no new refs.
- `Assembly-CSharp` / `Golfin.Gameplay.Input` (owns `ShotController`) — reads `SpinContext.Spin` from `Golfin.Gameplay.UI.HUD` (already in Assembly-CSharp, no asmdef boundary).
- `Golfin.Gameplay.Config` (owns `ControlsConfig` + loader) — two new fields + two new CSV rows + two new switch cases.
- `Golfin.Physics.Viewer` editor scope (bot scenarios) — new scenario function + new menu item.
- Editor-only `Golfin.Physics.Viewer.Editor` (bot tees) — optionally extend or add a `ShotSpinLogTee.cs` mirroring `LiveStatLogTee.cs` (or piggyback on bot's `LogStep` history.log; see §5.6).

**Existing code referenced:**
- `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` — primary edit site, lines 81–95 (spin block).
- `Assets/Scripts/Physics/Core/SpinState.cs` — `Rate > fp.Zero` convention: positive Rate = backspin (axis encodes direction). Topspin = negated axis, positive Rate.
- `Assets/Scripts/Physics/Math/fpMath.cs` — has `Cross`, `Normalize`, `Dot`, `Cos`, `Sin`. Missing: axis-angle rotation.
- `Assets/Scripts/Physics/Math/fp.cs` — `fp3` has `+`, `-`, `*` (by `fp`), `/` (by `fp`). No unary minus on `fp3`; use `v * (-fp.One)` or `fp3.Zero - v`.
- `Assets/Scripts/Gameplay/Input/ShotController.cs` lines 222–263 — `CommitFlick` is the call site for `ShotInputBuilder.Build`.
- `Assets/Scripts/Gameplay/UI/ShotUI/SpinContext.cs` — static `Vector2 Spin` (clamped ±1 by `SetSpin`). `SpinPanelWidget._values[]` confirms `(0, +1)` = top-of-ball position (line 29).
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` line 42 — `Default` is where the two new defaults go.
- `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` — append two new switch cases for the CSV keys.
- `Assets/Resources/Gameplay/controls.csv` — append two rows.
- `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` — `LiveStatProviderVisualGateHigh` (line 705) is the structural precedent for the new `SpinAndShapeVisualGate` scenario.
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LiveStatLogTee.cs` — structural precedent for `ShotSpinLogTee.cs` if we choose the file-tee path (§5.6 evaluates).
- `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` lines 76–93 — scenario dispatch switch; needs a new case.
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` — needs a new `GOLFIN/Smoke/Loop v2/Spin And Shape Visual Gate` menu item.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` line 514 — `ResetToTee()` public, used between strokes.
- `Docs/Scripts/build_bot_video.py` — `--mode visualgate` (line 203, 255) parses `[t=…] PreArm:` + `Stroke N:` + `Stroke N terminal=…`. Either add a new mode `--mode spinshape` or extend `visualgate` to pick up spin per stroke. §5.7 evaluates.

**Manager APIs used:**
- `SpinContext.Spin` (read) — `Vector2`, clamped ±1.
- `SpinContext.Reset()` — used by bot scenario between strokes.
- `ShotInputBuilder.Build(..., Vector2 spinInput, ...)` — new signature (last parameter, defaulted to `Vector2.zero` to keep existing tests compiling without edits).
- `PhysicsLabController.ResetToTee()` — bot calls between strokes.
- `BotDriver.LogStep(string)` — bot scenario emits stroke setup labels into `history.log` for `build_bot_video.py` to caption.

## Implementation

### 5.1 — `ControlsConfig` + CSV (additive, 4 line-changes total)

**`Assets/Scripts/Gameplay/Config/ControlsConfig.cs`:**

Add two new fields at the end of the field list (before the `Default` initializer):

```csharp
// Spin input (player-input lane; see spin_and_shot_shape_wiring SPEC §5.3)
public float SpinMagScaleSlope;   // 1.5 = sign-flip allowed at spinY=±1
public float SpinMaxTiltRad;      // 0.3 ≈ 17° max axis tilt at spinX=±1
```

Add two corresponding initializer lines inside `Default`:

```csharp
SpinMagScaleSlope = 1.5f,
SpinMaxTiltRad    = 0.3f,
```

**`Assets/Resources/Gameplay/controls.csv`:**

Append two rows (preserve trailing newline):

```csv
SpinMagScaleSlope,1.5,spin.y magnitude slope (1.5 allows sign-flip topspin at y=+1)
SpinMaxTiltRad,0.3,spin.x max axis-tilt angle in radians (≈17°) at x=±1
```

**`Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs`:**

Append two switch cases inside the `switch (key)` block, in alphabetical-ish position consistent with the existing layout (after `PuttBaseVelocityMps`):

```csharp
case "SpinMagScaleSlope":              cfg.SpinMagScaleSlope              = val; break;
case "SpinMaxTiltRad":                 cfg.SpinMaxTiltRad                 = val; break;
```

### 5.2 — `fpMath.Rotate` (Rodrigues' formula, new public helper)

**`Assets/Scripts/Physics/Math/fpMath.cs`:**

Add a new public static method below the existing `Normalize`:

```csharp
/// <summary>
/// Rotate vector v around unit axis k by angleRad using Rodrigues' formula.
/// k MUST be normalized — caller's responsibility. Returns v rotated.
/// </summary>
public static fp3 Rotate(fp3 v, fp3 k, fp angleRad)
{
    fp c = Cos(angleRad);
    fp s = Sin(angleRad);
    fp oneMinusC = fp.One - c;
    // v_rot = v*c + (k × v)*s + k*(k·v)*(1 - c)
    fp3 cross = Cross(k, v);
    fp dot = Dot(k, v);
    return v * c + cross * s + k * (dot * oneMinusC);
}
```

**Tests** in `Assets/Scripts/Physics/Tests/fpMathTests.cs`:

```csharp
[Test] public void Rotate_ZeroAngle_ReturnsInputVector() { /* assert v_rot ≈ v */ }
[Test] public void Rotate_PiAroundY_NegatesX() { /* (1,0,0) rot π around (0,1,0) → (-1,0,0) */ }
[Test] public void Rotate_HalfPiAroundZ_TurnsXIntoY() { /* (1,0,0) rot π/2 around (0,0,1) → (0,1,0) */ }
[Test] public void Rotate_PreservesLength() { /* |v_rot| ≈ |v| within fp epsilon */ }
```

Tolerance: `Mathf.Abs(actual - expected) < 0.01f` after `.ToFloat()` — `fp.Sin`/`fp.Cos` aren't bit-perfect (per the queued `fpMath.Cos/Sin range-reduction repair` ticket).

### 5.3 — `ShotInputBuilder.Build` (signature extension + spin math)

**`Assets/Scripts/Physics/Stats/ShotInputBuilder.cs`:**

Add a new last parameter to `Build`, defaulted so existing callers compile unchanged:

```csharp
public static (Golfin.Physics.ShotInput input, Golfin.Physics.BallPhysicsModifiers ballMods) Build(
    StatBundle bundle,
    StatCoefficients coeffs, StatCaps caps,
    fp flickMagnitude01,
    fp aimYawRadians,
    fp originX, fp originY, fp originZ,
    uint seed,
    fp baseVelocityOverrideMps = default,
    UnityEngine.Vector2 spinInput = default,        // NEW — spinInput defaults to (0,0)
    fp spinMagScaleSlope = default,                  // NEW — 0 → no scaling (legacy behavior)
    fp spinMaxTiltRad = default)                     // NEW — 0 → no tilt (legacy behavior)
```

Replace the existing spin block (lines 81–94) with:

```csharp
// Spin: backspin around right-vector, modulated by per-shot spin input.
// Putts have no spin (Phase 5 design lock; see spin_and_shot_shape_wiring SPEC §Out of scope).
Golfin.Physics.SpinState spin;
if (bundle.IsPutt)
{
    spin = Golfin.Physics.SpinState.None;
}
else
{
    fp baseRpm       = bundle.Club.Value.BaseBackspinRpm;
    fp baseRadPerSec = baseRpm * fpMath.TwoPi / fp.FromInt(60);
    fp baseSpinMag   = baseRadPerSec * resolved.SpinMagnitudeMultiplier;

    // Starting axis: pure right-vector backspin (legacy, when spinInput=(0,0)).
    fp3 startAxis = new fp3(-sinYaw, fp.Zero, cosYaw);

    // Q2(a): sign-flip allowed. magScale signed; e.g. spinY=+1 with slope=1.5 → -0.5 (topspin).
    fp spinY    = fp.FromFloat(spinInput.y);
    fp spinX    = fp.FromFloat(spinInput.x);
    fp magScale = fp.One - spinY * spinMagScaleSlope;

    // Q3(a): orbital tilt around velocity vector. spinX × maxTilt radians.
    fp3 finalAxis;
    if (spinX != fp.Zero && spinMaxTiltRad != fp.Zero)
    {
        fp3 velocityDir = fpMath.Normalize(velocity);
        fp tiltAngle = spinX * spinMaxTiltRad;
        finalAxis = fpMath.Rotate(startAxis, velocityDir, tiltAngle);
    }
    else
    {
        finalAxis = startAxis;
    }

    // SpinState convention: Rate > 0 = "backspin" (axis encodes the actual direction).
    // For negative magScale (true topspin), negate axis and use |magScale|.
    fp finalRate;
    if (magScale >= fp.Zero)
    {
        finalRate = magScale * baseSpinMag;
    }
    else
    {
        finalAxis = finalAxis * (-fp.One);
        finalRate = (-magScale) * baseSpinMag;
    }

    spin = new Golfin.Physics.SpinState(finalAxis, finalRate);
}
```

**Extend `DiagBuildLogger`** output to include spin info (replace the existing log call):

```csharp
DiagBuildLogger(
    $"[Build] isPutt={bundle.IsPutt} " +
    $"override={overrideStr}m/s clubVel={clubVel}m/s putterVel={putterVel}m/s " +
    $"-> baseVelMps={baseVelMps.ToFloat():F2} " +
    $"effectiveFlick={effectiveFlick.ToFloat():F3} " +
    $"velMultiplier={resolved.VelocityMultiplier.ToFloat():F3} " +
    $"-> velMagnitude={velMagnitude.ToFloat():F2}m/s " +
    $"loft={loftDeg.ToFloat():F1}deg aimYaw={aimYawRadians.ToFloat():F3}rad " +
    $"finalVel=({velocity.x.ToFloat():F2},{velocity.y.ToFloat():F2},{velocity.z.ToFloat():F2}) " +
    $"spinInput=({spinInput.x:F2},{spinInput.y:F2}) " +
    $"spinAxis=({spin.Axis.x.ToFloat():F2},{spin.Axis.y.ToFloat():F2},{spin.Axis.z.ToFloat():F2}) " +
    $"spinRate={spin.Rate.ToFloat():F1}rad/s");
```

### 5.4 — `ShotController.CommitFlick` (read `SpinContext`, plumb through)

**`Assets/Scripts/Gameplay/Input/ShotController.cs`:**

Add `using Golfin.Gameplay.UI.HUD;` to the using block at the top.

Inside `CommitFlick`, just before the `ShotInputBuilder.Build` call, read SpinContext and the new ControlsConfig fields:

```csharp
UnityEngine.Vector2 spinInput = IsPutt ? UnityEngine.Vector2.zero : SpinContext.Spin;
fp spinMagSlope = fp.FromFloat(_config.SpinMagScaleSlope);
fp spinTiltRad  = fp.FromFloat(_config.SpinMaxTiltRad);
```

Pass them through the `Build` call:

```csharp
var (input, ballMods) = ShotInputBuilder.Build(
    bundle,
    StatCoefficients.Default,
    StatCaps.Default,
    fp.FromFloat(flickMag),
    fp.FromFloat(_aimYawRadians),
    fp.Zero, fp.Zero, fp.Zero,
    (uint)UnityEngine.Random.Range(1, int.MaxValue),
    baseVelOverride,
    spinInput,
    spinMagSlope,
    spinTiltRad);
```

**Reset `SpinContext.Spin` to zero on next-shot handoff.** Cesar's prior work (`loop_v1_2e_next_shot_handoff`) re-arms via `HandleShotComplete`/`PlaceBallAt`/`ReArm`. Verify where the next-shot handoff is and add a `SpinContext.Reset()` call there so each shot starts with a clean spin selection. Implementer: locate the re-arm site (likely `PhysicsLabController.HandleShotResolved` or `BallStateMachine` → Aiming transition) and add the reset call. **If the location is non-obvious, flag with a `// TODO[SPEC]` comment and FORWARD_TO_ARCHITECT** — don't guess.

### 5.5 — Tests (EditMode, deterministic, no scene)

**New file** `Assets/Scripts/Physics/Tests/ShotInputBuilderTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Physics.Stats.Tests
{
    public class ShotInputBuilderSpinTests
    {
        // Reusable: build a driver bundle with deterministic stats.
        StatBundle DriverBundle() { /* build via existing helpers; mirror LiveStatProviderHostPlayModeTests */ }

        [Test] public void SpinInput_Zero_ProducesLegacyBackspinAxis()
        {
            // spinInput=(0,0), slope=1.5, tilt=0.3 → axis matches old (-sinYaw, 0, cosYaw)
        }

        [Test] public void SpinInput_PositiveY_ReducesBackspinMagnitude()
        {
            // spinY=+0.5, slope=1.5 → magScale=0.25 → final rate = 0.25× baseline.
        }

        [Test] public void SpinInput_FullPositiveY_FlipsAxisToTopspin()
        {
            // spinY=+1, slope=1.5 → magScale=-0.5 → axis negated, rate = 0.5× baseline magnitude.
        }

        [Test] public void SpinInput_NegativeY_BoostsBackspinMagnitude()
        {
            // spinY=-1, slope=1.5 → magScale=2.5 → rate = 2.5× baseline.
        }

        [Test] public void SpinInput_PositiveX_TiltsAxisOrbitally()
        {
            // spinX=+1, tilt=0.3 → axis rotated 0.3rad around velocity vector.
            // Assert Dot(velocity, finalAxis) ≈ Dot(velocity, startAxis) (tilt preserves projection on velocity).
            // Assert finalAxis ≠ startAxis.
        }

        [Test] public void SpinInput_SymmetricX_ProducesMirroredAxes()
        {
            // spinX=+1 vs spinX=-1 → axes mirror-image across the velocity plane.
        }

        [Test] public void Putt_IgnoresSpinInput()
        {
            // bundle.IsPutt=true, spinInput=(1,1) → spin == SpinState.None.
        }

        [Test] public void SpinAxis_RemainsUnitLength_AfterTilt()
        {
            // |finalAxis| ≈ 1 after Rotate (Rodrigues preserves length).
        }
    }
}
```

Test gate target: baseline 347/344/0/3 (per AI_CONTEXT.md last-updated) **+ ≥8 new spin tests + ≥4 new Rodrigues tests = ≥359 total**. No regressions; existing 344 must still PASS.

### 5.6 — Bot scenario `SpinAndShapeVisualGate`

**Pattern source:** `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` `LiveStatProviderVisualGateHigh` (line 705) and `LiveStatProviderVisualGateLow` (line 749). The new scenario varies `SpinContext.Spin` per stroke instead of varying character build.

**Add to `Scenarios.cs`:**

```csharp
/// <summary>
/// Spin and shot-shape visual gate: fires 5 driver shots from Hole 1 tee with
/// the same character/club/power, varying only SpinContext.Spin between strokes.
/// Bot resets to tee between strokes via PhysicsLabController.ResetToTee().
/// Captioned output video shows clear curl/distance differences across spin positions.
/// </summary>
public static IEnumerator SpinAndShapeVisualGate(BotDriver d)
{
    d.LogStep("=== Spin and Shot-Shape Visual Gate ===");

    // No character-build mutation — use the production-default character.
    // (If a specific build is needed for spin perceptibility, surface as Q-lock in IMPLEMENTER_REPORT.)

    yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
    yield return new WaitForSecondsRealtime(1f);
    yield return d.Capture("home");

    yield return d.Click("PLAY", settleSeconds: 1.5f);
    yield return d.WaitForModalVisible("MatchMakingModal", timeoutSeconds: 15f);
    yield return d.WaitFor(() => d.GetMatchmakingPhase() == "OpponentFound", "matchmaking opponent found", timeoutSeconds: 30f);
    yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
    yield return d.WaitForSceneLoaded("Hole_01_Geo", timeoutSeconds: 40f);
    yield return new WaitForSecondsRealtime(3f);
    yield return d.Capture("gameplay_armed");

    // The 5 spin positions in widget-index order.
    var spinPositions = new[]
    {
        (label: "CENTER",       spin: new Vector2( 0f,  0f)),
        (label: "TOP_TOPSPIN",  spin: new Vector2( 0f, +1f)),
        (label: "BOTTOM_BACK",  spin: new Vector2( 0f, -1f)),
        (label: "LEFT_DRAW",    spin: new Vector2(-1f,  0f)),
        (label: "RIGHT_FADE",   spin: new Vector2(+1f,  0f)),
    };

    for (int i = 0; i < spinPositions.Length; i++)
    {
        var (label, spin) = spinPositions[i];

        // Reset state between strokes (Lesson V — same-start comparisons MUST reset).
        d.ResetLabToTee();                                  // call PhysicsLabController.ResetToTee() via reflection/seam
        Golfin.Gameplay.UI.HUD.SpinContext.SetSpin(spin);
        yield return new WaitForSecondsRealtime(0.5f);      // settle frame

        d.LogStep($"Stroke {i+1}: {label} spinInput=({spin.x:F1},{spin.y:F1})");

        yield return d.Capture($"stroke{i+1}_{label.ToLower()}_armed");

        // Fire driver shot at fixed power (e.g. 1.0). Use the same drag-path the production scenarios use.
        // Mirror LiveStatProviderVisualGateHigh's `FireShot(clubIndex=0, power=1.0)` or equivalent.
        yield return d.FireDriverShot(power: 1.0f);

        // Wait for ball-at-rest (timeout 20s — driver shots take ~10s of flight + rollout).
        yield return d.WaitForBallAtRest(timeoutSeconds: 20f);
        yield return d.Capture($"stroke{i+1}_{label.ToLower()}_landed");
    }

    d.LogStep("=== Spin Gate Complete ===");
    yield return new WaitForSecondsRealtime(1f);
}
```

**`d.ResetLabToTee()`** — if not already exposed, add a BotDriver primitive that finds the active `PhysicsLabController` (via `Object.FindObjectOfType`) and calls `.ResetToTee()`. Editor-only.

**`d.FireDriverShot(float power)`** — if not already exposed in BotDriver primitives (`Hole1Playthrough` uses a per-shot drag-fire pattern via `BeginExternalDrag`/`SetExternalPower`/`EndExternalDrag` per `loop_v2_smoke_bot` deliverables — see `Docs/Architecture/BOT_FRAMEWORK.md` §primitives), add a `FireDriverShot(power)` primitive or reuse the equivalent from `Hole1Playthrough` with `clubIndex=0` (driver). Verify the production `ShotController` drag path is used (NOT `FireDebugShot`) so `SpinContext` actually flows through `CommitFlick`.

**`d.WaitForBallAtRest(timeoutSeconds)`** — already exists per `Hole1Playthrough` pattern (`BallStateMachine.State == AtRest` poll). Reuse.

**Add to `LoopV2SmokeBot.cs`** dispatch switch (after the existing `LiveStatProviderVisualGateLow` case):

```csharp
case "SpinAndShapeVisualGate":
    scenarioRoutine = Bot.Scenarios.SpinAndShapeVisualGate(driver);
    break;
```

**Add to `LoopV2SmokeBotMenu.cs`** a new menu item:

```csharp
[MenuItem("GOLFIN/Smoke/Loop v2/Spin And Shape Visual Gate")]
public static void RunSpinAndShapeVisualGate()
{
    LoopV2SmokeBot.Scenario = "SpinAndShapeVisualGate";
    LoopV2SmokeBot.Armed    = true;
    EditorApplication.isPlaying = true;
}
```

**Log tee:** Reuse `LiveStatLogTee.cs`'s pattern — either extend that file's `LogPrefix` filter to include `[Build]` lines (so `DiagBuildLogger`'s spinInput output is captured) **or** add a new `ShotSpinLogTee.cs` filtering on a fresh `[ShotSpin]` prefix. Lean: **extend** `LiveStatLogTee` to also capture `[Build]` lines (one-line change to the filter, output goes to the same `live_stat_log.txt` per scenario, build_bot_video.py grows one parser). Implementer's call if a cleaner separation is preferred; document choice in `IMPLEMENTER_REPORT.md`.

### 5.7 — `build_bot_video.py` extension

Current `--mode visualgate` parses `PreArm:` + `Stroke N:` + `Stroke N terminal=…`. The spin scenario emits `Stroke N: {LABEL} spinInput=(X,Y)` via `BotDriver.LogStep` (which already flows to `history.log` per the existing bot framework).

Add a new caption mode `--mode spinshape`:

```python
# In build_bot_video.py
def parse_spinshape_captions(log_path, rec_start, rec_end):
    """
    Surface "Stroke N <LABEL> spinInput=(X,Y) → carry Xm, lateral Ym" captions during gameplay.
    Recognizes lines emitted by SpinAndShapeVisualGate:
      [t=T] Stroke <N>: <LABEL> spinInput=(<x>,<y>)
      [t=T] Stroke <N> terminal=<t> endSurface=<s> ball=(x, y, z)   # reuses Hole1Playthrough format
    Computes carry as ball.x - tee.x (or Euclidean if more meaningful for shape display)
    and lateral deviation as ball.z (perpendicular to fairway).
    """
    # mirror parse_visualgate_captions structure; produce captions per stroke
    ...

# Then in main:
ap.add_argument("--mode", choices=["clicks", "visualgate", "spinshape"], default="clicks", ...)
...
if args.mode == "spinshape":
    captions = parse_spinshape_captions(log_path, rec_start, rec_start + duration)
```

Output: single MP4 captioned per-stroke showing spin position label + carry + lateral deviation. Canonical viewing copy lands at `tasks/loop_v2_smoke_bot/SpinAndShapeVisualGate/videos/spin_and_shape_visual_gate_captioned.mp4` (matches `convention_videos_vs_screenshots.md` rule).

## Acceptance checklist

The Implementer fills `IMPLEMENTER_REPORT.md` marking each `PASS`/`FAIL` with a one-sentence justification.

**Code correctness:**

- [ ] `ControlsConfig` has `SpinMagScaleSlope=1.5f` + `SpinMaxTiltRad=0.3f` in `Default`. CSV has both rows. Loader has both switch cases. Round-trip verified by loading + reading back.
- [ ] `fpMath.Rotate` ports Rodrigues' formula. Self-tests at angle=0, π, π/2 around principal axes PASS. Length preserved within fp tolerance.
- [ ] `ShotInputBuilder.Build` signature has the 3 new defaulted params. All existing `Build` callers compile without edits.
- [ ] Existing test gate **344 PASS holds** (no regression).
- [ ] `ShotInputBuilderSpinTests` ≥8 new tests PASS. Per-test: legacy axis at spinInput=0, magnitude scaling at ±0.5, sign-flip at ±1, axis tilt at spinX=±1, symmetry, putt-ignores-spin, axis-unit-length-after-tilt.
- [ ] `fpMathTests.Rotate*` ≥4 new tests PASS.
- [ ] `ShotController.CommitFlick` reads `SpinContext.Spin` (or `Vector2.zero` for putts) and passes through to `Build`.
- [ ] `SpinContext.Reset()` is called at the next-shot handoff site. If site is non-obvious, FORWARD_TO_ARCHITECT (don't guess).
- [ ] `DiagBuildLogger` output includes `spinInput=…`, `spinAxis=…`, `spinRate=…`.

**Bot scenario:**

- [ ] `SpinAndShapeVisualGate` scenario added to `Scenarios.cs`, dispatched in `LoopV2SmokeBot.cs`, menu item in `LoopV2SmokeBotMenu.cs`.
- [ ] Scenario runs end-to-end in editor without errors. 5 strokes fire from same tee position. `ResetToTee()` confirmed between strokes via `[TeeDiag]` log lines.
- [ ] `LiveStatLogTee` (or `ShotSpinLogTee`) captures the per-stroke `[Build]` log lines including spinInput.
- [ ] `build_bot_video.py --mode spinshape` produces a captioned MP4 with one stroke per spin position, label visible per stroke.

**Visual gate (Cesar reviews from bot video):**

- [ ] Stroke 1 CENTER: baseline straight shot, no curl.
- [ ] Stroke 2 TOP_TOPSPIN: visibly lower trajectory than CENTER. Ball rolls noticeably further on landing (Δ carry ≥3m or Δ total ≥8m).
- [ ] Stroke 3 BOTTOM_BACK: visibly higher trajectory than CENTER. Ball stops faster on landing (Δ rollout ≤−3m vs CENTER).
- [ ] Stroke 4 LEFT_DRAW: ball curves left in flight. Final position lateral.z is visibly negative relative to CENTER terminal (Δ lateral ≥5m).
- [ ] Stroke 5 RIGHT_FADE: ball curves right. Δ lateral ≥+5m vs CENTER terminal.
- [ ] All 5 strokes used the same character + driver + power=1.0 (verified from `[Build]` log lines — only `spinInput` differs).

**Hygiene:**

- [ ] No `.unity`/`.prefab`/`.asset` mutations. `git diff` on those file types is empty.
- [ ] No scope creep into Ball.Spin lane (`StatCoefficients.BallSpinPerPoint` untouched), `SpinPanelWidget._values[]` untouched, `_positions[]` untouched.
- [ ] Console error-free during scenario run.
- [ ] Spec deviations (if any) listed at the bottom of `IMPLEMENTER_REPORT.md` with justification.

## Files / hierarchy this task touches

**Modified:**
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` — +2 fields, +2 init lines
- `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` — +2 switch cases
- `Assets/Resources/Gameplay/controls.csv` — +2 rows
- `Assets/Scripts/Physics/Math/fpMath.cs` — +1 method `Rotate`
- `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` — signature +3 params, spin block rewrite, logger output extension
- `Assets/Scripts/Gameplay/Input/ShotController.cs` — read SpinContext + pass through to Build
- `Assets/Scripts/Physics/Tests/fpMathTests.cs` — +≥4 Rotate tests
- `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` — +1 scenario
- `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` — +1 dispatch case
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` — +1 menu item
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LiveStatLogTee.cs` — extend filter to also catch `[Build]` lines (or add new tee file — implementer's call)
- `Docs/Scripts/build_bot_video.py` — +`spinshape` mode + parser function
- Next-shot handoff site (TBD by implementer — `PhysicsLabController.HandleShotResolved` is the lead candidate) — +1 `SpinContext.Reset()` call

**New:**
- `Assets/Scripts/Physics/Tests/ShotInputBuilderTests.cs` — +≥8 spin tests
- `Assets/Scripts/Physics/Viewer/Bot/Editor/ShotSpinLogTee.cs` — only if implementer chooses fresh tee instead of extending LiveStatLogTee

**Pipeline artifacts** (populated through pipeline runs):
- `Docs/Specs/Active/spin_and_shot_shape_wiring/IMPLEMENTER_REPORT.md`
- `Docs/Specs/Active/spin_and_shot_shape_wiring/SELF_REVIEW.md`
- `Docs/Specs/Active/spin_and_shot_shape_wiring/ARCHITECT_REVIEW.md`
- `Docs/Specs/Active/spin_and_shot_shape_wiring/STATUS.md`
- `Docs/Specs/Active/spin_and_shot_shape_wiring/screenshots/` — per-stroke armed + landed stills
- `Docs/Specs/Active/spin_and_shot_shape_wiring/videos/spin_and_shape_visual_gate_captioned.mp4`

## Smoke evidence

Bot-driven, no manual playtest (per Q5 mod).

1. Run `GOLFIN/Smoke/Loop v2/Spin And Shape Visual Gate` from the Editor menu (or via `script-execute` calling `LoopV2SmokeBotMenu.RunSpinAndShapeVisualGate()`).
2. Bot drives through Home → PLAY → matchmaking → Hole 1 scene load → fires 5 strokes from same tee with `ResetToTee()` between.
3. Per-stroke screenshots (armed + landed) land in the task `screenshots/` folder via `BotDriver.Capture()` (CaptureCore path).
4. `LiveStatLogTee` (or `ShotSpinLogTee`) writes `live_stat_log.txt` with each stroke's `[Build]` line including spinInput + spinAxis + spinRate.
5. After play exits, run `python Docs/Scripts/build_bot_video.py --mode spinshape ...` to produce the captioned MP4.
6. Verify Acceptance checklist items 12–17 (visual gate) from the MP4 + landed screenshots.

## Pipeline

**TIER 3 — full pipeline.** Subagent chain: `golfin-implementer` → `golfin-self-reviewer` → `golfin-reviewer` → architect → Cesar visual gate (from bot video, not manual play).

Architect-side notes for the implementer kickoff:
- The new `Build` signature parameters default to legacy behavior — existing tests should compile + PASS without edits. If any do not, surface that BEFORE editing them (`FORWARD_TO_ARCHITECT`).
- The next-shot handoff `SpinContext.Reset()` site is the one unknown — `// TODO[SPEC]` flag + forward if non-obvious is the right move.
- Lesson V (same-start state reset) applies to the bot scenario — `ResetToTee()` between strokes is non-negotiable.
- Lesson W does NOT apply — dep direction is correct here, no bus-state pattern needed.

## Out of scope (deferred)

- **Putt spin** — `SpinState.None` retained. Short-game spin gameplay TBD as a separate ticket.
- **Continuous spin UI** — current 5-position discrete widget stays. Continuous radial input is a polish follow-up.
- **Per-character spin modifiers** — `SpinMagnitudeMultiplier` already routes through character stats (Ball.Spin lane); this spec adds the user-input dimension on top.
- **Dashboard live-tunable spin sliders** — CSV-edit + scene-restart tuning loop for v1. If playtest reveals the slope/tilt values need iteration, filing a `dashboard_controls_section` follow-up is a 1-hr task.
- **Pitch tilt (face open/close)** — only yaw axis tilt for now. Lift-via-tilt for high/low shots is a separate ticket if it ever comes up.
- **Replay determinism extension** — per NOTES, the 2-float spinInput per shot is trivial to seed; if replay coverage becomes a goal, extend the replay record format. Not required for v1.

## Open follow-ups (file at DONE)

- `dashboard_controls_section` (P3 — convenience): add a CONTROLS section to `DashboardUI.cs` with `SpinMagScaleSlope` and `SpinMaxTiltRad` sliders. Requires routing `ControlsConfig` mutations through `ShotController.InjectConfig`.
- `spinpanel_continuous_input` (P3 — UX polish): replace 5-discrete-position `SpinPanelWidget` with continuous radial drag input. Wire through to same `SpinContext.Spin` Vector2 — no physics-side changes.
- `putt_spin_design` (P3 — gameplay design): scope short-game spin if/when Cesar wants it as a feature. Out of scope here.
