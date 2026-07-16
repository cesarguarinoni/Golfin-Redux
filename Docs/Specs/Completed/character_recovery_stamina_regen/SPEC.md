# character_recovery_stamina_regen

> **Status:** Queued (filed from `stat_to_physics_mapping_audit`, 2026-05-25). Tier-Redesign.

## One-line

Implement the session-level stamina regeneration mechanic for Character.Recovery — currently a no-op stat with zero physics effect.

## Why

`CharacterStats.Recovery` is defined in the struct and appears in character data CSVs, but has no code path in `StatModifierResolver` or anywhere in the session loop. The `stat_to_physics_mapping_audit` confirmed this is a placeholder. Until Recovery affects gameplay, it's a misleading stat displayed to players.

## Scope

1. Design the session loop: between-hole (or between-stroke) stamina regen based on `Character.Recovery` value.
2. Wire: after each hole/stroke completes, compute `staminaRegen = f(Recovery, elapsedTime)` and apply to `PlayerCharacterData.currentStaminaEnergy`.
3. Ensure the stamina level at hole start reflects the accumulated regen.
4. Visual: show stamina bar changes in the result screen (or between-hole transition) when Recovery is meaningful.

## Hard rules

- Do NOT change the per-shot stamina multiplier logic — that path already works correctly.
- Recovery must have zero effect on single-stroke bot scenarios (stamina regen is between-stroke, not during-stroke).
- Tests must cover: zero-Recovery character has constant staminaEnergy across holes; max-Recovery character regenerates faster.

## Notes

This is Tier-Redesign because it introduces new game loop logic outside `StatModifierResolver`. The per-shot physics resolver does not need changes — all Recovery implementation is in the session/match management layer.
