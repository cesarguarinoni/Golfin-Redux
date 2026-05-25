# stat_to_physics_mapping_audit — Audit the full stat-to-physics lane mapping

> **STATUS:** Queued (drafted 2026-05-25 from `live_stat_provider_wiring` Phase 3 postmortem). Tier 3. Surfaced when the visual gate could not show a HIGH vs LOW carry delta even after the LIVE bus correctly routed character stats end-to-end.

## One-line

Audit the `StatModifierResolver` single-source lane assignments to confirm each character / club / ball stat has a perceptible gameplay effect, document the design intent, and either (a) justify each weakly-felt stat as intentional or (b) propose mapping changes that surface the differences gameplay-side. Output is an Architecture doc + (if needed) a tuned `StatCoefficients` set.

## Why

`live_stat_provider_wiring` (Phase 3) wired the live stat provider into the production codepath correctly — every committed shot now resolves `CharacterStats`, `ClubStats`, `BallStats` from the player's actual selection. The bot-recorded visual gate, however, showed identical 3-stroke EAGLE on Hole 1 with a maxed `Strength` vs default `Strength` build using the same club + ball. Root cause: `StatModifierResolver.cs:22-25` only routes `Club.Power × Ball.Power` into `velocityMultiplier`. `Character.Strength` only feeds `overpower forgiveness` (line 53), which has no observable effect at `power = 1.0` (no overpower applied). So a strength-maxed player and a default player produce identical carry distance whenever they aim cleanly at `power = 1.0`.

This is design-intentional per the "single-source per lane" comment in the resolver — but it surfaces a UX problem: players cannot feel the value of leveling a character if their carry distance is unchanged. A patch was applied in `live_stat_provider_wiring` Phase 4 (F7) adding a small Strength-velocity coupling to unblock the visual gate. That patch is intentional one-off, NOT a design replacement; the full audit lives here.

## Scope

1. **Lane audit:** for each of the 8 lanes in `StatModifierResolver` (velocity, aim cone, spin, lie resist, overpower, putter off-center, gravity well radius, aim cycles), confirm:
   - Which stats feed it (currently single-source per design)
   - The min/max impact at the realistic stat-range extremes (Common 25 vs Supreme 50 stat caps)
   - Whether that impact is *gameplay-perceptible* — minimum bar: shows up in a smoke-bot side-by-side as ≥1 stroke delta, ≥10m carry delta, or ≥0.5° aim spread delta on a representative hole
2. **`BallPhysicsModifiers` audit:** the rebound / roll / wind-cut lanes that flow into `BallSimulation` — same perceptibility bar.
3. **Tuning proposal:** for each weakly-felt lane, either:
   - (a) Justify keeping it weak as design intent (with a doc paragraph explaining why and what the player *can* feel that scales with the stat — e.g. forgiveness only manifests when the player overpowers, which Common-rarity characters do more)
   - (b) Propose a coefficient change that brings it above the perceptibility bar without breaking existing physics tests or hole-completion stats
4. **Cross-cutting questions to answer in writing:**
   - Should `Character.Strength` directly affect velocity (currently no, post-patch only weak coupling)?
   - Should `Character.Recovery` (currently does it affect anything?) feed back into stamina regen between shots?
   - Should `Character.Stamina` be more than a soft scalar (currently `staminaMultiplier` clamps to 1.0 max so it never amplifies, only attenuates)?
   - Should Ball.Power and Ball.Spin compete or stack (currently multiplicative-positive on velocity, additive on spin)?
5. **Test gate:** any coefficient changes must preserve the existing `Golfin.Physics.Tests` baseline (today: 340/337/0/3 after Phase 3). Document a new physics regression test set if proposed changes go in.

## Out of scope

- The `live_stat_provider_wiring` Phase 4 F7 patch itself. That patch is shipping; this audit may revisit it but is not blocked on it.
- Renaming `PhysicsLabController` to a non-lab-confusing name — separate task `physics_lab_controller_rename`.
- The Putter lane stat sources (Putter.Control, Putter.Accuracy, Putter.Weight) — those are 1:1 with putter inputs and don't have the "single-source per character lane" issue. Audit them only if a side-by-side test reveals an issue.

## Hard rules

1. The "single-source per lane" pattern is the design baseline. Any departure must be explicitly documented with a one-paragraph design justification in the output doc.
2. Coefficient changes must not regress hole-completion stats on the current Hole 1 par-5 (must still be completable in ≤7 strokes by a default-stat character with a default driver, default ball).
3. Tests stay green at or above baseline.

## Definition of done

- `Docs/Physics/STAT_LANE_AUDIT.md` written: one section per lane, perceptibility number, design justification, proposed change (if any).
- If coefficient changes are proposed: PR ships them with new physics regression tests + the Coefficients diff documented in `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md`.
- `live_stat_provider_wiring` Phase 4 F7 patch revisited in the doc: kept as-is, rolled back, or refined.
- A line in `Docs/AI_CONTEXT.md` updated noting the audit completed and where the result lives.
