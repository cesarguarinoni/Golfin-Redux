# SPEC — `bot_scheme_parity`

**Status:** SPEC_READY (2026-09-05, re-issued 2026-09-06 as ONE stage). All three schemes are DONE (`scheme_pendulum` `501bf5881`, `scheme_needle` `d54468b6c`, `scheme_freeswing` `4ae3307d9`) and `scheme_confirm_popup` is DONE, so the Stage A / Stage B split below is obsolete: build the seam, the `BotSwing` door AND the three scheme executors in one task. Where the text says "Stage A" / "Stage B", read "this task".
**Track:** control schemes — `Docs/CONTROL_SCHEMES_PLAN.md`; takes up the "bot error-model parity" backlog row. Cesar 2026-09-05: "add the ability for bots to use the different systems and switch to the one selected by the player."
**Parent designs:** `Docs/Specs/Completed/versus_bot_difficulty/SPEC.md` (D1: error is execution, never intent — unchanged), `control_scheme_seam` (`8913901a7`).
**One line:** every bot — the 1v1 opponent today, and any smoke / capture / perf / feature-test bot written from now on — swings whatever scheme the player has selected — its handle, bar or needle animate on screen, and its per-shot miss is sampled on that scheme's timing axis and graded by that scheme's own maths — while the bracket difficulty stays calibrated.

---

## 1. Why

- **Fairness:** with `control_scheme_seam` the player's misses are `(ErrorYawRad, TimingMul)` from a grader; the bot's are a uniform `±aimErrorDegMax / ±powerErrorMax`. Under Pendulum a level-1 human has a bimodal miss (JUST or MISS), the bot has a flat one. The two opponents are no longer playing the same game.
- **Presentation:** the bot drives `BeginExternalDrag → SetExternalPower → EndExternalDrag`, so the Flick cone/handle animates for its swing. With another scheme active, `ShotSchemeHost` has Flick's root off — the bot's swing would show nothing, or the wrong widget.
- **The switch:** bots must follow `ShotSchemeHost.ActiveScheme` (the *resolved* scheme, which honours the placeholder-falls-back-to-Flick rule), not the raw pref.

## 2. Non-goals

Bot *decision* pipeline (H1 club/power, H2 safety/layup, H3 slope read, tree-aware re-check) — untouched. Tournament bots (server-scored). Making the bot better or worse: the calibration guard in §5 pins expected miss magnitude to today's brackets. Needle / Free Swing executors (their scheme specs add them, on this seam). Deterministic capture rigs that fire `ShotPreset`s through `PhysicsLabController.Fire()` are not swings and stay as they are (§3.5).

## 3. Design

### 3.1 `BotSwingPlan` + `IBotSchemeExecutor` (Stage A)
`VersusBot.TakeShot()` steps 1–7 already produce the intended `(club, power01, aimYaw, isPutt)` and then run "2b error injection" + step 8 (drag ramp + `EndExternalDrag`). Cut there:

```csharp
public readonly struct BotSwingPlan { int LabClub; float Power01; float AimYawRad; bool IsPutt; float ProbeCarryM; }
public interface IBotSchemeExecutor
{
    ControlScheme Scheme { get; }
    /// Samples this scheme's execution error for the bracket, animates the scheme UI, commits the shot.
    /// Must end with exactly one ShotController commit (or a logged cancel) and log the "[VersusBot] 2b error" line.
    IEnumerator Execute(BotSwingPlan plan, BotExecutionBand band, BotExecutionContext ctx);
}
public readonly struct BotExecutionBand { float AimErrorDegMax; float PowerErrorMax; float ExecSigma01; }   // ← bracket row
public sealed class BotExecutionContext { ShotController Shot; PhysicsLabController Lab; Func<float,float,float> Range; TreeSet Trees; Vector3 Ball; bool DisableTreeRecheck; bool DisableCanopyPreference; }
```
- `VersusBot` resolves the executor from the active scheme root: `ShotSchemeHost.ActiveExecutor` (new getter — the host looks for an `IBotSchemeExecutor` on the active root; if none, returns the Flick executor). `ActiveScheme` already exists on the host.
- **`FlickBotExecutor`** = today's 2b block + step 8 moved verbatim (uniform `Δaim`, `Δpow`, club noise stays in VersusBot before the plan is built, tree-aware rejection sampling, `BeginExternalDrag() → 0.85 s ramp → SetExternalPower → 0.18 s → EndExternalDrag()`). Lives on `SchemeRoot_Flick`. **Byte-identical behaviour**: the existing `versus_bot_difficulty` acceptance (log line format, error ranges, tree re-check) is the regression suite.
- `bot_difficulty.csv` gains a column **`execSigma01`** — the bot's normalised timing-axis error σ for graded schemes (§3.3). Loader (`EnsureDifficultyLoaded`) reads it; missing column → derived at load (§5 formula) and logged, so the CSV can be committed after calibration.

### 3.2 Scheme executors drive the scheme's own UI (Stage A contract, Stage B first instance)
An executor never fakes a result off-screen. It plays the gesture through the scheme's public driver surface so the player watches a real swing:
- Flick: as today (cone + handle via the external-drag API).
- Pendulum (Stage B): `PendulumSchemeDriver` gains a programmatic entry `IEnumerator DriveBot(float power01, float targetMarker01, float curve01, BotExecutionContext ctx)`: handle pulls down over `rampSeconds` to the lane position for `power01` (`SetExternalPower` each frame, `ownsTiming:true`), the bar sweeps for at least one full sweep, then **commits the frame the live marker passes `targetMarker01`** (tolerance `PendulumBotCommitTol01`, default 0.03; if not hit within `PendulumBotMaxWaitSweeps` = 2, commit at the nearest pass). The grade the player sees (JUST/GOOD/MISS pop) is therefore the real grade of the marker position at commit — no separate bot path through `PendulumMath`. Sampling and grading share `PendulumMath.Grade` with the **bot character's** stats (`ClubAccuracyNorm01`, `CharacterClubControl`, `OverpowerForgiveness01` come from the injected bot bundle exactly as today's `InjectStatBundle`).
- **Needle:** `NeedleSchemeDriver.DriveBot(float power01, float targetNeedle01, BotExecutionContext ctx)`: handle pulls down over `rampSeconds` inside the circle to `power01`, releases (peak), the arc + needle start as for a player, and the executor **taps the frame the live needle passes `targetNeedle01`** (tolerance `NeedleBotCommitTol01`, default 0.03) through the real `NeedleTapCatcher` path; `targetNeedle01 ≥ 1` = a deliberate SHANK (never sampled by the model — a bot never chooses to shank; clamp samples to ±0.98).
- **Free Swing:** `FreeSwingSchemeDriver.DriveBot(float power01, float impactOffsetPx, float tempoRatio, float upSpeedPxPerSec, BotExecutionContext ctx)`: the executor feeds **synthetic samples into the driver's own buffer in real time** (a straight backswing to the lane depth for `power01` over `tB`, then an upstroke of length `L` over `tD = tempoRatio × tB` at `upSpeedPxPerSec`, ending `impactOffsetPx` right of the origin as it crosses the impact line) so the trace draws, the tempo is real, and the driver fires on the crossing exactly as for a thumb. Path is straight (bots never shape a shot; `FadeDraw01 = 0`). The grade/chip the player sees is the driver's own.
- Every `DriveBot` respects `IsPutt` (the scheme's putt rules apply to bots too) and the bot character's injected stat bundle.

### 3.3 Error model per scheme (execution stage, D1 preserved)
| Scheme | Timing axis | Sample | Then |
|---|---|---|---|
| Flick | none (bots never latch) | `Δaim ~ U(±AimErrorDegMax)`, `Δpow ~ U(±PowerErrorMax)` | unchanged |
| Pendulum | marker offset `m` | `m ~ clamp(N(0, ExecSigma01), −1, 1)`; `Δpow ~ U(±PowerErrorMax)` on the intended pull | `PendulumMath.Grade(m, accNorm, halfCone)` → `(ErrorYaw, TimingMul)` produced **by the driver at commit**, not injected |
| Needle | needle offset `n` | `n ~ clamp(N(0, ExecSigma01), −0.98, 0.98)`; `Δpow` as above | `NeedleMath.Grade` at the tap, by the driver |
| Free Swing | impact `xI`, tempo `r` | `xI ~ N(0, ExecSigma01 × FreeSwingImpactMissPx)`; `r = IdealTempo + N(0, ExecSigma01 × 2 × TempoWindow)`; up speed = `2 × DuffSpeed` (bots never duff by design — a duff is a thumb failure, not a skill failure); `Δpow` as above | `FreeSwingMath` at the crossing, by the driver |

Tree-aware re-check (2b) stays: for graded schemes the rejection sampler draws the scheme's timing sample (not `Δaim`) and evaluates the resulting `ErrorYaw` against trunks — generalise `BotTreeProbe.TrySampleTreeAwareAimError` to take a `Func<float> sampleDeltaAimDeg` (Flick: `() => Range(−max, max)`; Pendulum: `() => PendulumMath.Grade(SampleM()).ErrorYawDeg`; Needle: `NeedleMath.Grade(SampleN())`; Free Swing: `FreeSwingMath.ImpactYaw(SampleXI())`). The accepted sample is the one handed to `DriveBot`, so what the player sees is the shot the sampler cleared. Signature change only; the Flick call site's behaviour is identical (test: same RNG seed → same deltas).

### 3.4 Scheme switch
- The bot reads `ShotSchemeHost.ActiveScheme` at the start of **each** `TakeShot()`. A player switching mid-match changes the bot's next swing, same as their own (host defers swaps to Idle, so a swing in progress is never split).
- `VersusMatchController` needs nothing; `TakeShot` is already per-stroke.
- Log line gains `scheme=<name>`: `[VersusBot] TakeShot: shot fired — club=… power=… scheme=Pendulum m=+0.12 grade=GOOD`.

### 3.5 One door for every bot — `BotSwing` (Stage A; Cesar 2026-09-05: "this should include any test bots we use in the future when developing features")
Today five bots drive `ShotController` by hand (`BeginExternalDrag → SetExternalPower → EndExternalDrag`, or `FireDebugShot`): `VersusBot`, `PerfBaselineBot`, `TreeOccludeFadeCaptureBot`, `Scenarios.cs` (the in-flight ClubHandle regression guard), `ObBoundaryCaptureBot` / `ObRecoveryCaptureBot` / `WaterSplashCaptureRig` / `ZoneBakeAfterClipBot` / `LoopV2SmokeBot` / `VersusHudCaptureBot` via `BotDriver.FireShot`. From this spec on there is exactly one way for a bot to swing:

```csharp
public static class BotSwing            // Golfin.Gameplay.UI (next to ShotSchemeHost)
{
    /// Swings through the ACTIVE scheme's executor. Default for every bot.
    public static IEnumerator Play(BotSwingPlan plan, BotExecutionBand band, BotExecutionContext ctx, BotSwingOptions opt = default);
    /// Zero-error convenience for smoke/perf bots: band = BotExecutionBand.Perfect.
    public static IEnumerator PlayPerfect(float power01, float aimYawRad, bool isPutt, BotExecutionContext ctx, BotSwingOptions opt = default);
}
public struct BotSwingOptions { public bool ForceFlick; /* deterministic captures only — must say why in the call site comment */ public float RampSeconds; }
```
- `BotSwing.Play` resolves `ShotSchemeHost.ActiveExecutor` (falls back to the Flick executor when no host is in the scene, e.g. EditMode tests), so a future feature bot written against `BotSwing` swings the selected scheme without knowing schemes exist.
- **Migration in Stage A:** `VersusBot` (§3.1), `PerfBaselineBot` and `TreeOccludeFadeCaptureBot` (both use the external-drag ramp today) move to `BotSwing.PlayPerfect(...)`. `PerfBaselineBot` passes `ForceFlick = true` with the comment `// perf baseline compares against build 2699 numbers; scheme UI cost is measured by scheme_evaluation` — its numbers must not move. `TreeOccludeFadeCaptureBot` follows the scheme (a capture of the occlusion fade does not care which handle moved). `BotDriver.FireShot` and the OB/water/zone rigs fire `ShotPreset`s through `PhysicsLabController.Fire()` — those are not swings and are out of scope; `Scenarios.cs`'s ClubHandle guard is a Flick-specific regression test by definition and stays on the raw API with a comment saying so.
- **Rule, added to `CLAUDE.md` under PIPELINE_HARDENING and to `Docs/AI_CONTEXT.md`:** *"Bots swing through `BotSwing.Play/PlayPerfect`, never `BeginExternalDrag`/`EndExternalDrag`/`CommitFlick` directly. `ForceFlick` requires a comment. A new bot that bypasses `BotSwing` fails review."* Enforce with a grep in `.claude/hooks/enforce_implementer_done.py` (Rule N+1): any file under `Assets/Scripts/**/Bot*/` or named `*Bot.cs` / `*CaptureRig.cs` that calls `BeginExternalDrag(` or `EndExternalDrag(` and is not on the allow-list (`Scenarios.cs`, `ClubHandleDragger.cs`, the scheme drivers) blocks the done-hook.
- `BotExecutionBand.Perfect` = all zeros: Flick executor injects nothing; Pendulum executor targets `m = 0` → JUST every time (the marker-wait still animates, so a perfect bot still looks like it swings).

## 4. Files (expected)
- Stage A: `BotSwing.cs`, `BotSwingOptions` (new, `Golfin.Gameplay.UI`), `PerfBaselineBot.cs` + `TreeOccludeFadeCaptureBot.cs` (migrate), `CLAUDE.md` + `Docs/AI_CONTEXT.md` (rule), `.claude/hooks/enforce_implementer_done.py` (grep rule), `Assets/Scripts/Physics/Viewer/VersusBot.cs` (2b + step 8 → executor call; bracket loader column; `BotSwingPlan` build), `Assets/Scripts/Physics/Viewer/Bot/BotSwingPlan.cs`, `IBotSchemeExecutor.cs`, `FlickBotExecutor.cs` (new — same assembly as `VersusBot`, since it needs `PhysicsLabController`; the host getter returns `MonoBehaviour` + interface, no cross-assembly type leak: put `IBotSchemeExecutor` in `Golfin.Gameplay.UI` next to `IShotSchemeDriver` and have the Physics-side executor implement it — check the reference direction, NOTE in the report), `BotTreeProbe.cs` (sampler delegate), `ShotSchemeHost.cs` (`ActiveExecutor`), `Assets/Resources/Data/bot_difficulty.csv` (+ `execSigma01`), `Assets/Scripts/Physics/Viewer/Bot/Editor/BotClubCalibrationHarness.cs` or a new `BotSchemeCalibrationHarness` (§5).
  ⚠️ CLAUDE.md PIPELINE_HARDENING rule 7 says "ZERO edits to `Assets/Scripts/Physics/`". In practice that has meant the **sim** (`Physics/Core`, `Physics/Runtime`, `HoleSessionDriver`): `Physics/Viewer/VersusBot.cs` has three post-rule commits (`07a92e663`, `b76e2f85f`, `9c059425b`) and the done-hook does not grep that path. Proceed on that reading, keep the VersusBot diff to the 2b/step-8 cut only, and NOTE the reading in the report; if the hook does block, stop and surface — do not route around it.
- Stage B: `PendulumSchemeDriver.DriveBot`, `PendulumBotExecutor.cs`, two `controls.csv` keys (`PendulumBotCommitTol01`, `PendulumBotMaxWaitSweeps`).

## 5. Calibration guard (Stage A harness, Stage B numbers)
`execSigma01` is **one column per bracket, three columns in practice** (`execSigmaPendulum01`, `execSigmaNeedle01`, `execSigmaFreeSwing01` — the three graders have different shapes, one σ cannot calibrate all three), each chosen so that, for the reference bot character/club (level-bracket midpoint, Acc = 60 club, Strength/CC at the bracket's typical roster values — take them from `BotClubCalibrationHarness`'s fixtures), **E|ErrorYaw| under that scheme equals E|Δaim| under Flick = AimErrorDegMax / 2**, and E[1 − TimingMul] ≤ PowerErrorMax / 2. Editor harness `Tools ▸ Golfin ▸ Bots ▸ Calibrate Scheme Sigma`: for each bracket × scheme, bisect σ over 20 000 samples of that scheme's grader until the yaw target matches within 3 %; write the three columns; print the table. Committed CSV values come from the harness in THIS task (all three graders exist). Committed CSV values come from the harness, not by hand. Re-run whenever `PendulumJustWindow*`, `PendulumGoodWindow01` or `PendulumMissYawGain` change (note in `controls.csv` comments).

## 6. Tests (EditMode)
1. **Flick regression:** with a seeded `Range`, `FlickBotExecutor` produces the same `Δaim/Δpow/club` sequence and the same log line as the pre-refactor `VersusBot` for 50 plans (golden file captured before the cut). `versus_bot_difficulty`'s existing tests unchanged and green.
2. `BotTreeProbe.TrySampleTreeAwareAimError(sampler)`: Flick sampler → identical results to the old signature for a seeded RNG; Pendulum sampler → returned deltas are always `Grade(m).ErrorYawDeg` for some sampled `m`.
3. Bracket loader: `execSigma01` present → used; absent → derived + warning; malformed → bracket falls back to Flick executor (never a zero-error bot).
3b. `BotSwing.Play` with no `ShotSchemeHost` in the scene → Flick executor, no exception; `ForceFlick` → Flick executor even with Pendulum active.
4. `ShotSchemeHost.ActiveExecutor`: Flick root → Flick executor; Pendulum root with no executor → Flick executor (Stage A); Pendulum root with executor → that (Stage B).
5. Stage B: `PendulumSchemeDriver.DriveBot` commits exactly once through `CommitExternal` with `|m_commit − target| ≤ tol` (or nearest-pass after 2 sweeps), `IsPutt` clamps power, `FadeDraw01` = 0 for bots.
6. Calibration: harness result for each bracket reproduces E|ErrorYaw| within 3 % of `AimErrorDegMax/2` (test runs 5 000 samples with a fixed seed).

## 7. Acceptance
- Every bot in §3.5 that swings does so through `BotSwing`; the done-hook grep passes; `PerfBaselineBot` numbers unchanged vs its last baseline (`ForceFlick`).
- Stage A: 1v1 vs a level-1 and a level-100 bot with **Flick** selected — indistinguishable from `main` (log diff over 9 holes: identical error ranges, identical tree re-check behaviour). Pendulum selected before Stage B → bot swings Flick (host rule) and the log says `scheme=Flick(fallback)`.
- Stage B: Pendulum selected → the bot's club pulls down the lane, the bar sweeps, the marker is visibly on/off the pip when it commits, the JUST/GOOD/MISS pop shows; level-1 bot misses often, level-100 nearly always JUST; the 9-hole strokes-vs-par of each bracket is within 1 stroke of its Flick run (calibration guard, 3 runs each).
- Switching the scheme mid-match from the gear modal changes the bot's next swing and not the one in progress.
- Full EditMode sweep per assembly, zero new failures.

## 8. Out of scope → backlog
Bot personality (waiting a different number of sweeps by level — purely cosmetic pacing); bots in the Range/Lab capture rigs (they use `FireDebugShot`/presets and are not opponents); server-side tournament bots.
