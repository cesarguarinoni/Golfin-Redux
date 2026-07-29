# Physics Tuning Changelog

Tracks changes to `StatCoefficients` defaults and `StatCaps` defaults over time.
Any change here that affects gameplay carry/accuracy distances must be verified
against the Hole 1 par-5 completability baseline (≤7 strokes with default character).

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
