# Physics Tuning Changelog

Tracks changes to `StatCoefficients` defaults and `StatCaps` defaults over time.
Any change here that affects gameplay carry/accuracy distances must be verified
against the Hole 1 par-5 completability baseline (≤7 strokes with default character).

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
