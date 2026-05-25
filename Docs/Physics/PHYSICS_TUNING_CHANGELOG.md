# Physics Tuning Changelog

Tracks changes to `StatCoefficients` defaults and `StatCaps` defaults over time.
Any change here that affects gameplay carry/accuracy distances must be verified
against the Hole 1 par-5 completability baseline (≤7 strokes with default character).

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
