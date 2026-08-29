# Physics Tuning Changelog

Tracks changes to `StatCoefficients` defaults and `StatCaps` defaults over time.
Any change here that affects gameplay carry/accuracy distances must be verified
against the Hole 1 par-5 completability baseline (≤7 strokes with default character).

---

## F14 — Straight-mode aim honours the cone (`AimNudgeRangeRad` removed) (2026-08-28)

**Task:** `shot_aim_parity`
**Reason:** The targeting line and the committed shot used **two different aim formulas**, so the
ball never went where the line pointed. Players read it as "the flick always fires centered".
**Locked by:** Architect decision D1 (Cesar to overrule by tuning `ConeHalfAngleAtAcc100Deg`, not code).

### Symptom

Pull the club handle to the far edge of the cone and the targeting line swings out to ~11° at the
median club (up to 20° at Accuracy 120). Flick, and the ball launches at most **3°** off the camera
heading. At 200 yd the line implies ~38 yd of lateral movement; the ball moves ~10 yd. The aim
input looked broken, so players stopped using it.

### The two formulas

| Consumer | Formula (before) | Full deflection @ median club |
|---|---|---|
| `ShotController.PublishState` → targeting line | `heading + finetune * HalfConeAngleRad()` | **±11.25°** |
| `ShotController.CommitFlick` → the shot (Straight) | `heading + finetune * AimNudgeRangeRad` | **±3.0°** |

Ratio **≈ 3.7×** at the median shipped club (`baseAccuracy` 48 → half-cone ≈ 11°). The 3° nudge
came from `fade_draw_core_wiring` (Order 356) decision D4; the line kept the `SHOT_CONTROLS_DESIGN
§3.3` formula. Nothing tested the two against each other, which is how it shipped.

### Decisions

- **D1 — the shot honours the cone.** Committed aim in Straight mode is now
  `finetune * HalfConeAngleRad()`, matching the line and §3.3. The cone is defined as "aim range
  AND error tolerance" (§3.3/§4) — a cone you cannot aim across has no reason to be wide, and this
  makes Club Accuracy matter for aiming, not just for the safe zone. `AimNudgeRangeRad` is
  **removed** (config field, `Default` initialiser, loader `case`, `controls.csv` row 27).
  *Tuning knob if ±20° at Accuracy 120 feels wide: `ConeHalfAngleAtAcc100Deg` — one number, no code.*
- **D2 — one formula, one place.** New private `ShotController.AimYawFor(float finetune)` is the
  single source of truth; `PublishState` calls it directly, `CommitFlick` calls it and adds
  `degradYaw`. `ShotAimParityTests` is the regression gate.
- **D3 — the aim latch re-opens on a new low.** `PushTouchSample` previously latched on 1 % of
  `Screen.height` of cumulative upward travel (~28 px on a 15 Pro Max) and **never** unlatched
  within a swing, so a thumb wobbling while aiming at the cone base froze the aim silently. It now
  unlatches and re-syncs to the live handle whenever the finger goes below the swing's lowest
  point. `_reversalThreshold` stays 0.01 (the unlatch is what makes it forgiving). A real flick
  never comes back down, so the latch still holds through a genuine upswing.
- **D4 — Fade/Draw line rotates to the locked heading only.** Falls out of D2: `PublishState` now
  returns `FadeDrawLockedAimRad` in Fade/Draw mode instead of rotating with the handle. The line
  previously both rotated *and* bent while the ball only bent. Bend still comes from
  `AimLineBendRenderer.FinetuneX`.

**Putts are unchanged in value** — they already used the half-cone formula on both sides; they
simply route through the same helper now.

### Files changed

- `Assets/Scripts/Gameplay/Input/ShotController.cs` — `AimYawFor` (new), `CommitFlick`,
  `PublishState`, `PushTouchSample`, `LogResolution` snapshot (now prints `halfCone` + `finetune`).
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs`, `ControlsConfigLoader.cs`,
  `Assets/Resources/Gameplay/controls.csv` — `AimNudgeRangeRad` removed from all three mirrors
  (see the F13 correction above: `ControlsConfig.Default` is runtime truth, the CSV is
  documentation — but an orphan CSV row would make `ControlsConfigLoader` log an unknown-key
  warning, so the row goes with the field).

### Tests — `Golfin.Gameplay.Tests`, 348 passed / 0 failed / 0 skipped

- **`ShotAimParityTests` (new, 5 tests).** `Straight_PublishedAimEqualsCommittedAim` sweeps
  finetune ∈ {−1, −0.6, 0, 0.35, 1} and asserts the yaw recovered from the committed velocity
  equals the last published `AimYawRadians` *and* equals `heading + f * halfCone`, to 1e-3 rad.
  Plus `FadeDraw_PublishedAimIsLockedHeading`, `Putt_PublishedAimEqualsCommittedAim`,
  `Latch_ReopensWhenFingerGoesLower` (D3), `Latch_HoldsThroughUpswing_NoNewLow` (D3 does not
  weaken the latch). Proven to actually execute with a deliberate `Assert.Fail` tripwire, because
  `tests-run` silently ignores class filters on this project.
- **`FadeDrawWiringTests`** tests 1–2 renamed `StraightMode_HandleRight_AimsRightByHalfCone` /
  `..._HandleLeft_AimsLeftByHalfCone`; expectation changed from `_cfg.AimNudgeRangeRad` to
  `_sc.ConeHalfAngleDeg * Mathf.Deg2Rad`.
- `Golfin.Physics.Tests`: 357 passed / 0 failed / 3 pre-existing documented skips.

### The control scheme, confirmed in play: club left ⇒ ball right

Worth recording because F14 makes it far more visible. The cone handle is the **club's position
relative to the ball**, not a pointer at the target, so the ball leaves on the opposite side —
real golf controls (Cesar, 2026-08-29). Measured on Hole 1 by reproducing `ShotConeView`'s own
world→screen projection:

| stroke | club offset on screen | drawn line lean | ball |
|---|---|---|---|
| club RIGHT | +157.5 px | −249.1 px | LEFT |
| club CENTRED | 0.0 px | −0.001 px | dead ahead |
| club LEFT | −78.3 px | +307.6 px | RIGHT |

The sign is unchanged from HEAD `01daaefb`; F14 only changed the magnitude, so the same control
now moves the ball ±8–11° instead of ±3°. `ShotAimParityDemoRecorder`'s `*_A5` assertions lock
the inverse relationship in so a future change cannot flip it by accident.

### Fade/Draw curve — measured while verifying F14 (not changed by F14)

Cesar asked whether the fade was meant to come out straight. It is not, and it is not broken.
Isolated physics (driver, seed 42, flat ground, `FadeDrawMaxTiltRad` 0.3): a full-power fade
curves **53 yd** (9.46°), full draw to full fade spans **106 yd**. At 0.52 power it curves only
**7.8 yd** — which is why the first verification clip, whose fade stroke was sized at 0.52 to keep
the ball out of trouble, looked dead straight.

**Open, and worth its own task:** the same shot in live play measured **12.6 m (14 yd), 2.65°**
over a 273 m tee shot — ~3.6× less than the model. Wind is not the cause (re-running with Hole
10's 8.7 mph @ 332° still gives 45.6 m). Remaining candidates: the equipped club/ball stats (the
live driver is a `Clubs.csv` player club, not `ClubStats.DefaultDriver`, and backspin rate is what
the tilted axis converts into side force) and terrain roll after landing. Tilt sweep for
reference, at full power: 0.3 → 53 yd; 0.6 → 90 yd; 1.0 → 99 yd but carry drops 291→229 m; 1.5 →
curve falls back to 60 yd with carry down to 170 m. 0.3 sits on the well-behaved part of the
curve.

### Bots and carry baselines: unaffected

The Loop-v2 bot fires via `FireDebugShot()`, which sets `_aimFinetune = 0` in Straight mode. At
finetune 0 both the old and new formulas reduce to `heading + degradYaw` — byte-identical. No
carry baseline moves.

---

## F13 — Low-CC arrow speed retune + arrowHz floor clamp (2026-08-04)

**Task:** `arrow_speed_retune`
**Reason:** The timing arrow was too fast to read at low ClubControl. Retunes the F11 calibration
at the low-CC end and closes the no-floor hazard F11 recorded but deferred.
**Locked by:** Cesar, editor play mode, round 1 ("Speed looks good").

### ⚠️ Correction to the F11 record — which file is load-bearing

F11 states "**File:** `Assets/Resources/Gameplay/controls.csv` (CSV-only; no `ShotController` logic
change)". **That is backwards.** `ControlsConfigLoader.Load()` has **zero call sites** in `Assets/`
or `Packages/`: `ShotController._config` initialises to `ControlsConfig.Default` and the only
`InjectConfig()` callers are test fixtures. `SpinPanelWidget` also reads `ControlsConfig.Default`
directly. F11 only took effect because it *also* edited the C# mirror.

**`ControlsConfig.Default` is runtime truth; `controls.csv` is currently documentation.** Both are
still updated together — the divergence hazard is real and simply has not fired yet (all 30 keys
were value-identical before this change). Whether to wire the loader or delete it is an open
decision for Cesar; if wired, note `ControlsConfigLoader` has no `case` for `RingFrac` and
`SpinPanelWidget` bypasses the injected config.

### Value changes (both mirrors)

| Key | Old (F11) | New | Rationale |
|---|---|---|---|
| `BaseArrowSpeedHzAtCC0` | 3.0 | **2.0** | low-CC arrow unreadably fast; 0.333 s → 0.500 s per pass |
| `ArrowSpeedHzPerCC` | −0.05 | **−0.03** | moves as a **pair** with the base — holds the CC-50 end at 0.5 Hz |
| `MinArrowSpeedHz` | *(did not exist)* | **0.5** | new floor; see hardening below |

Base and slope are **not independently tunable**: lowering the base at slope −0.05 sends the
high-CC end negative (base 2.0 → CC 50 = −0.5 Hz).

### Resulting ladder (derived from the live config, not hand arithmetic)

| CC | swing Hz | s/pass | putt Hz | putt s/pass | putt auto-cancel |
|---|---|---|---|---|---|
| 0 | 2.000 | 0.500 | 1.600 | 0.625 | 6.3 s |
| 25 *(Common cap)* | 1.250 | 0.800 | 1.000 | 1.000 | 10.0 s |
| 50 *(Supreme cap)* | 0.500 | 2.000 | 0.400 | 2.500 | 25.0 s |

**Accepted trade-off:** the CC ladder spread narrows. Common cap → Supreme cap was 1.75→0.5 Hz
(**3.5×**) under F11; it is now 1.25→0.5 Hz (**2.5×**). Restoring that spread was F11's whole
purpose, so this partially walks it back at the low end. Flagged to Cesar before locking and
accepted for feel. If the ladder later reads as flat, the fix is lowering the CC-50 anchor —
**not** raising the base back.

### Hardening — `MinArrowSpeedHz` floor clamp

F11's recorded caveat: *"`arrowHz` has no floor … safe only because caps enforce CC ≤ 50."*
That is a promise made in a different file (`RarityStatCaps`). Now closed in `ShotController`:

`Assets/Scripts/Gameplay/Input/ShotController.cs` — `TickArrow`:
```csharp
arrowHz = Mathf.Max(arrowHz, _config.MinArrowSpeedHz);   // F13
if (IsPutt) arrowHz *= _config.PuttArrowSpeedMultiplier;
```

Without it, past CC = Base/|Slope| (**66.7** at F13 values) `arrowHz` goes negative,
`_arrowProgress` walks backwards, never crosses 1.0, and the shot **never auto-cancels** — a
soft-lock, not merely a slow arrow.

**Clamp order matters.** It is applied **before** the putt multiplier. Applying it after would
raise a high-CC putt back up to the floor and break the invariant that putts are slower than
swings at equal CC (`ShotControllerPuttModeTests.F1_IsPutt_ArrowsSlowedByMultiplier`).

Floor = 0.5 = the calibrated CC-50 speed, so it engages only at **CC > 50** — a **no-op across the
entire reachable range**. Purely a guard against a future cap change.

### Knock-ons (measured, not absorbed silently)

- **Putt compounding** (Order 732 burned on this once at multiplier 0.5 → 4 s cycles): worst case
  is **2.5 s/pass at CC 50 — unchanged from F11**, since the pair holds the CC-50 end fixed. Low
  end 0.625 s. Within the ~2.5–3 s tolerance; no change needed.
- **Auto-cancel window** (`MaxTotalPasses = 10` is a time window in disguise): worst case
  **unchanged** at 20 s swing / 25 s putt at CC 50. The *low* end stretched: CC 0 swing
  3.3 s → **5.0 s**. `MaxTotalPasses` left at 10 (Cesar's call, not taken here).
- **Hole 1 completability: unaffected.** The Loop-v2 bot fires via `ShotController.FireDebugShot()`,
  which bypasses `TickArrow()` entirely.

### Tests — `Golfin.Gameplay.Tests`, 941 passed / 0 failed / 3 pre-existing skips

- `Test09_ArrowDegradation_StartsAfterCleanPasses` and `Test10_MaxTotalPasses_AutoCancelsToIdle`
  **failed** on the new values: both hard-coded `dt = 0.34`, derived from `arrowHz = 3.0`, so a
  tick no longer completed a pass. Fixed by deriving the tick from the config
  (`OnePassDtAtCC0 => 1.02f / ControlsConfig.Default.BaseArrowSpeedHzAtCC0`) rather than
  re-hardcoding — future retunes will not break them again.
- `Test12_ArrowSpeed_FloorClamp_StaysPositiveBeyondStatCaps` **added**: drives CC = 100
  (deliberately past every cap) and asserts the arrow still advances forward. This is the
  regression gate for the soft-lock above.
- Stale hard-coded Hz values in surviving comments (`3.0`, `2.4`, `1.5`) rewritten config-relative.

---

## F12 — DefaultSurface Fairway → Rough (2026-07-29)

**Task:** `surface_classification_ob_rough` (Stage 2)
**File changed:** `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs:73`
  `public const SurfaceType DefaultSurface = SurfaceType.Rough;  // was SurfaceType.Fairway`

**No coefficient value changes.** All surface coefficients (`SurfaceConfig.cs`) are unchanged.
This entry records the behavioral change from the DefaultSurface flip.

### What changed

`BakedZoneClassifier.DefaultSurface` is the surface returned for any point not matched by a
zone polygon or the OB mask. Before: `SurfaceType.Fairway` (RollingResistance 0.18).
After: `SurfaceType.Rough` (RollingResistance 0.45).

### Affected ground — 96.36% of the Default bucket

Measured across 18 holes by `surface_fallthrough_coverage_probe` (DONE `bdb4f1f4d`):

| Default-bucket category | cells | % of Default bucket |
|---|---:|---:|
| authored Rough + semi_rough | 8,286,618 | 68.33% |
| tree zones (no polygon group; see §0 note) | 3,399,017 | 28.03% |
| **total affected by this flip** | **11,685,635** | **96.36%** |
| authored Fairway gaps (0.27% residual) | 32,411 | 0.27% |
| authored ob | 8,525 | 0.07% |

**Trees note (§0 gate, 2026-07-29):** `tree_obstacles.csv` stores point instances only —
no polygon group is baked for tree coverage. 80–90% of tree-zone ground has no trunk collider
(TrunkRadius 0.25–0.35 m vs canopyRadius 3.5 m, ~100× area difference). The ball routinely
comes to rest under canopy on reachable ground. Ground under trees is pine straw/leaf litter/dirt —
Rough is the correct classification and is the intended effect, not a side effect.

### Rolling resistance shift

| Surface | RollingResistance | StopSpeed |
|---|---|---|
| Fairway (old default) | 0.18 | 0.20 |
| Rough (new default) | 0.45 | 0.22 |

Effective RollingResistance for **96.36%** of fallthrough ground: **0.18 → 0.45 (2.5×)**.
Courses play materially harder; roll-out on missed fairways is sharply reduced.
This is the intended effect per the §0 product gate (Cesar, 2026-07-29).

### 0.27% fairway residual — accepted known defect

32,411 cells of genuine authored Fairway fall through the polygon lookup due to mesh boundary
gaps. Post-flip they play as Rough. That is 0.07% of footprint and is a polygon-gap defect,
not a tuning problem. No adjustment made; recorded here for future reference.

### Companion change — Stage 1

`IsObAt` now returns `bool?` (null = outside terrain grid). Points past the terrain edge
that formerly fell to `DefaultSurface = Fairway` (no penalty) now return `SurfaceType.OOB`
(penalty path armed). No surface coefficient impact — this change affects shot termination,
not roll dynamics.

---

## F11 — ClubControl → arrow range recalibration (2026-07-17)

**Task:** `club_control_arrow_range_calibration` (Order 732)
**File:** `Assets/Resources/Gameplay/controls.csv` (CSV-only; no `ShotController` logic change).
**Reason:** The `ShotController.TickArrow` arrow-speed / clean-pass coefficients were calibrated for CC 0–100, but `RarityStatCaps` caps ClubControl at 50 (Supreme) / 25 (Common). The reachable ladder collapsed to a **1.36×** arrow-speed spread (2.375→1.75 Hz, Common cap→Supreme) and 2→3 clean passes — ClubControl felt dead. Measured (arithmetic on the two formulas across CC 0/25/30/35/40/50), then rescaled to restore the designed endpoints on the reachable 0–50 range (Cesar-approved: full rescale).

### controls.csv changes

| Key | Old | New | Effect at reachable caps |
|---|---|---|---|
| `ArrowSpeedHzPerCC` | −0.025 | **−0.05** | arrow Common cap 1.75 Hz → Supreme 0.5 Hz (**3.5×** spread; was 1.36×) |
| `CleanPassesPerCC` | 0.04 | **0.08** | clean passes Common cap 3 → Supreme 5 (was 2 → 3) |
| `PuttArrowSpeedMultiplier` | 0.5 | **0.8** | putts no longer compound into 4 s cycles; Supreme putt 0.25 Hz/4.0 s → 0.40 Hz/2.5 s (kept < 1.0 so putts stay slower/easier than the swing arrow) |
| `MaxTotalPasses` | 10 | 10 (unchanged, Cesar) | Supreme degradation window 7 → 5 passes |
| `BaseArrowSpeedHzAtCC0` | 3.0 | 3.0 (unchanged) | CC=0 FALLBACK floor |

### Caveat (documented, not fixed here)

`arrowHz` has **no floor**; with the −0.05 slope it goes negative above **CC=60** (3.0 − 100×0.05 = −2.0). Safe only because `RarityStatCaps` enforces CC ≤ 50 (arrowHz 0.5 there). If a future cap ever exceeds ~60, add an `arrowHz` floor in `ShotController` (a logic change → separate order).

### Hole 1 completability

**Unaffected.** The Loop-v2 bot fires via `ShotController.FireDebugShot()`, which bypasses `TickArrow()` entirely — arrow timing has no effect on bot-driven hole completion.

### Tests

- `ShotControllerTests.Test11_ArrowSpeed_MonotonicDecreasingWithCC`: CC=100 → **CC=50** (100 now yields a negative arrowHz under −0.05 and is unreachable; 50 is the real cap, arrowHz 0.5 — same expected values).
- `ShotControllerPuttModeTests.F1_IsPutt_ArrowsSlowedByMultiplier`: comment refreshed for 0.8 (putt 2.4 Hz < non-putt 3.0 Hz; assertion unchanged).

### Still open (felt gate, not yet done)

The spec's felt gate is a side-by-side bot video of the arrow at Common-cap vs Supreme-cap CC. The physics bot bypasses `TickArrow`, so this needs a **new rig**. Not built in this CSV pass — flagged for follow-up.

---

## F10 — BallReboundPerPoint 0.01 → 0.02 (2026-07-17)

**Task:** `ball_rebound_perceptibility` (Order 417)
**Reason:** Measure-first (deterministic sim, flat fairway, neutral, power=1.0): at `BallReboundPerPoint = 0.01` the full ±10 Ball.Rebound swing (reboundMul 0.90→1.10) moved total distance only **~4.8m** — below the 10m perceptibility bar. Doubling to **0.02** maps ±10 to reboundMul **0.80→1.20**, i.e. exactly the existing `ReboundMultiplier{Min,Max}` cap band (**no clamp change, polarity unchanged**), for a **~10.7m** total-distance delta (Driver 10.8m, Iron7 10.6m) — clears the bar. Self-limiting: max stat lands on the 1.20 cap, so it cannot overcorrect. Sibling of F8 (`BallRollPerPoint 0.01 → 0.02`).

### StatCoefficients change

| Field | Old | New |
|---|---|---|
| `BallReboundPerPoint` | `0.01` | `0.02` |

No caps changed (`ReboundMultiplierMin/Max` stay 0.80/1.20).

### Hole 1 completability

**Unaffected — no-op on the default ball.** The default `ball_golfin` has `rebound=0`, so `reboundMul = 1 + 0×0.02 = 1.0` regardless of the coefficient. The completability baseline (default character + default ball) is bit-unchanged; only rebound-stat balls (e.g. `ball_putt_ace`, rebound=−6) see the retune.

### Tests

`Stats_BallRebound_MultiplierCorrect` updated: Ball Rebound +10 → ReboundMultiplier 1.10 → **1.20**.

---

## F9 — Retired resolver-side stamina lane + ClubControl aim-cone term (2026-07-16)

**Task:** `stat_lane_offdesign_retirement` (Order 731)  
**Reason:** Two off-design lanes were deleted from `StatModifierResolver.Resolve`:

1. **Stamina lane (Defect A):** `LiveStatProviderHost.BuildCharacterStats` already bakes stamina degradation via `StaminaModel.EffectiveStat` (with comfort threshold + curve + per-stat `IsDegraded` gate). The resolver was re-applying a cruder raw `current/max` multiplier with no threshold or gate, creating double-application. Deleted; `effStrength` now reads the already-provider-degraded value directly.

2. **ClubControl aim-cone term (Defect B):** `SHOT_CONTROLS_DESIGN.md §6` assigns cone width to Club Accuracy and arrow speed to ClubControl. The resolver's `charControlReduction` was a second stat driving the cone, which is off-design. Deleted; `aimConeReduction` is now single-source from Club Accuracy (ruling 2026-07-16).

### StatCoefficients changes

None. No coefficient values changed.

### Dead coefficients (fields retained, consumers removed)

| Field | Old value | Status |
|---|---|---|
| `StaminaFloorFraction` | `0.20f` | DEAD — field kept for csv loader; consumer deleted |
| `CharClubControlPerPoint` | `0.0035f` | DEAD — field kept for csv loader; consumer deleted |

### FALLBACK path: bit-identical

`DefaultStatProvider` uses `currentStamina=100f, maxStamina=100f` → `staminaFraction=1.0` → deleted stamina lane was a no-op. `CharacterStats.Neutral.ClubControl=0` → deleted `charControlReduction` was 0 × coeff = 0 → no-op. Terminal position proven bit-identical: `finalPosition=(x.raw=0, y.raw=1399, z.raw=13272238)` old = new.

### Behavioural deltas (at condition < 100%)

| Lane | Before | After |
|---|---|---|
| Strength velocity | Double-stamina penalty (resolver AND provider) | Single penalty at provider (designed model with comfort threshold) |
| Aim cone | Club Accuracy + ClubControl | Club Accuracy only |

At 100% condition and on the FALLBACK path: **zero delta**.

---

## F8 — BallRollPerPoint 0.01 → 0.02 (2026-06-02)

**Task:** `ball_roll_coefficient_retune`  
**Reason:** At 0.01, Ball.Roll=±10 swings `rollMul` between 0.90 and 1.10 (only 10% change), producing ~3m terminal-distance delta on Fairway — below the 10m perceptibility bar. Raising to 0.02 fills the cap range (0.80–1.20) at ±10 ball points, producing a ~12m+ delta.

### StatCoefficients changes

| Field | Before | After | Notes |
|---|---|---|---|
| `BallRollPerPoint` | `0.01f` | `0.02f` | Fills RollMultiplierMin/Max (0.80–1.20) at Ball.Roll=±10 |

### StatCaps (unchanged)

| Field | Value | Notes |
|---|---|---|
| `RollMultiplierMax` | `1.20f` | Unchanged |
| `RollMultiplierMin` | `0.80f` | Unchanged |

### Expected rollMul at stat extremes

| Ball.Roll | Formula | Unclamped | Clamped | Effect |
|---|---|---|---|---|
| +10 (max) | 1 − 10 × 0.02 | 0.80 | 0.80 (at min cap) | minimum rolling resistance → longest roll |
| 0 (neutral) | 1 − 0 × 0.02 | 1.00 | 1.00 | neutral |
| -10 (min) | 1 − (−10) × 0.02 | 1.20 | 1.20 (at max cap) | maximum rolling resistance → shortest roll |

### Also updated: `Assets/Resources/Physics/stats.csv`

The loaded config CSV `ball_roll_per_point` row also updated from 0.01 → 0.02 so runtime-loaded config matches the code Default.

### Regression test updated

`StatResolverTests.Stats_BallRoll_ReducesRollingResistance`: expected value changed 0.90 → 0.80 (cap-at-min).

---

## F7 — CharStrengthVelocityPerPoint + VelocityMultiplierMax (2026-05-25)

**Task:** `live_stat_provider_wiring` Phase 4  
**Reason:** `StatModifierResolver` had no Character.Strength lane in velocityMultiplier. Without it, a maxed-Strength build produced identical carry to a default-Strength build (0m delta), making the visual gate (HIGH vs LOW build) impossible to distinguish.

### StatCoefficients changes

| Field | Before | After | Notes |
|---|---|---|---|
| `CharStrengthVelocityPerPoint` | (field did not exist) | `0.004f` | New field — Strength → velocity coupling |
| All other fields | unchanged | unchanged | |

### StatCaps changes

| Field | Before | After | Notes |
|---|---|---|---|
| `VelocityMultiplierMax` | `2.0f` | `2.6f` | Raised ~30% to accommodate Supreme-maxed triple-product (Club.Power=120, Ball.Power=+10, Char.Strength=50) which produces 1.6×1.1×1.2=2.112, exceeding the old 2.0 cap and erasing the HIGH vs LOW delta |

### Expected range at stat extremes

| Build | Club.Power | Ball.Power | Char.Strength | velFromClub | velFromBall | velFromChar | Total multiplier |
|---|---|---|---|---|---|---|---|
| Supreme maxed | 120 | +10 | 50 | 1.60 | 1.10 | 1.20 | 2.112 |
| Default (Common start) | 80 | 0 | ~10 | 1.40 | 1.00 | 1.04 | 1.456 |
| Neutral (FALLBACK) | 50 | 0 | 0 | 1.25 | 1.00 | 1.00 | 1.250 |

### Visual gate delta verified

HIGH build (char_elizabeth lv=119, STR=30) vs LOW build (char_elizabeth lv=80, STR=8), same club + ball:
- HIGH stroke-1 carry: ~442m
- LOW stroke-1 carry: ~416m
- Delta: **26m** (threshold: ≥10m)

### Completability

Hole 1 Playthrough with FALLBACK stats (DefaultDriver, NeutralChar, NeutralBall) runs the same as pre-F7 because Neutral character Strength=0 → velFromChar=1.0. F7 has zero effect on the FALLBACK path.

The visual gate HIGH build drives Hole 1 with STR=30 on a driver completing stroke 1 at ~442m carry (fairway). The bot then uses a wedge for approach strokes. Due to a pre-existing limitation of `DefaultStatProvider` (always returns `ClubStats.DefaultDriver` for non-putt shots, overriding actual selected club per-shot stats in the FALLBACK path), the bot's wedge approach in the FALLBACK Hole 1 scenario overshoots. This is independent of F7 and pre-dates this task. Documented in `live_stat_provider_wiring` Phase 4 IMPLEMENTER_REPORT.

### Full audit follow-up

`Docs/Specs/Queued/stat_to_physics_mapping_audit/SPEC.md` — full lane-by-lane review including this F7 patch.

---

## Q3 — DefaultStatProvider club-aware FALLBACK (2026-05-25)

**Task:** `stat_to_physics_mapping_audit`  
**Reason:** `DefaultStatProvider.BuildSwingBundle()` always returned `ClubStats.DefaultDriver` regardless of which club was selected. Non-driver FALLBACK strokes used 75 m/s driver physics (instead of 51 m/s Iron7 or 42 m/s Wedge), causing an 80% overshoot on wedge approach shots. The Hole 1 Playthrough bot scored 8 strokes (seam) as a direct result.

### ClubStats changes

| Field | Before | After | Notes |
|---|---|---|---|
| `ClubStats.DefaultIron7` | (did not exist) | `(power=50, acc=50, lie=50, dur=100, loft=25.5°, vel=51m/s, spin=6500RPM)` | New static — matches LabClubs[1] verbatim |
| `ClubStats.DefaultWedge` | (did not exist) | `(power=50, acc=50, lie=50, dur=100, loft=41.2°, vel=42m/s, spin=9000RPM)` | New static — matches LabClubs[2] verbatim |

### DefaultStatProvider changes

`BuildSwingBundle()` now accepts an optional `clubIndex` parameter (default 0 = Driver). Index mapping:
- 0 → `ClubStats.DefaultDriver` (75 m/s, loft 10.9°, spin 2686 RPM)
- 1 → `ClubStats.DefaultIron7` (51 m/s, loft 25.5°, spin 6500 RPM)
- 2 → `ClubStats.DefaultWedge` (42 m/s, loft 41.2°, spin 9000 RPM)
- 3+ → `ClubStats.DefaultDriver` (safety fallback)

### StatProviderBus changes

Added `CurrentLabClubIndex` property and `SetCurrentLabClubIndex(int)` method. `PhysicsLabController.SetClub(index)` now calls `SetCurrentLabClubIndex` to keep the bus in sync. `Resolve(isPutt=false)` passes `CurrentLabClubIndex` to `DefaultStatProvider.BuildSwingBundle()`.

### StatCoefficients / StatCaps changes

None. This is purely a data-routing fix.

### Expected behavior after fix

| Stroke type | Before | After |
|---|---|---|
| FALLBACK driver stroke (index 0) | 75 m/s (correct) | 75 m/s (unchanged) |
| FALLBACK Iron7 stroke (index 1) | 75 m/s (WRONG — overshoot) | 51 m/s (correct) |
| FALLBACK Wedge stroke (index 2) | 75 m/s (WRONG — 80% overshoot) | 42 m/s (correct) |
| LIVE path (any club) | Unchanged (bus resolves real club stats) | Unchanged |

### Completability verification

Hole 1 Playthrough FALLBACK bot must complete in ≤7 strokes after this fix. See IMPLEMENTER_REPORT.md for bot run evidence.

### Tests added

- `DefaultStatProvider_BuildSwingBundle_Index0_ReturnsDriverStats` — index 0 → Driver 75 m/s
- `DefaultStatProvider_BuildSwingBundle_Index1_ReturnsIron7Stats` — index 1 → Iron7 51 m/s
- `DefaultStatProvider_BuildSwingBundle_Index2_ReturnsWedgeStats` — index 2 → Wedge 42 m/s
- `DefaultStatProvider_BuildSwingBundle_Index3AndAbove_FallsBackToDriver` — out-of-range → Driver
- `StatProviderBus_Resolve_WithNullReturningResolver_UsesCurrentLabClubIndex` — bus routes index to DefaultProvider
