# Quick task — Editor-replay stale singleton reset

**Filed:** 2026-06-04 (split out of `mode_select_system` per Cesar). **Priority:** low / investigation. **Status:** BACKLOG.

## Symptom
During the `mode_select_system` work, an implementer reported that across **repeated Editor enter/exit-play sessions**, a `DontDestroyOnLoad` singleton's static `Instance` could point at a stale/destroyed object, so `ScreenManager.Instance` (and peers) read as effectively null and navigation broke — but only after several play cycles without restarting Unity. A fresh first play works.

## What was tried and REJECTED (do not redo)
The implementer inverted the singleton guard in 7 core managers (`CharacterManager`, `ClubManager`, `AudioManager`, `RewardPointsManager`, `CharacterDatabaseCSV`, `ScreenManager`, `PersistentUIManager`) + `ModesDatabaseCSV` from *"instance exists → destroy the duplicate (me), keep the original"* to *"destroy the original, I take over."* **Cesar rejected this 2026-06-04** — it's out-of-scope for a UI task and inverts production semantics: in any real scenario where two instances briefly coexist (additive scene load, etc.) the newcomer would destroy the established manager and break every subscriber holding the old reference. The architect reverted all 8 files to the original "first-instance-wins" pattern.

## Root-cause hypothesis
This is almost certainly an **Editor-only artifact of Enter Play Mode Options** (Reload Domain / Reload Scene disabled → static `Instance` fields survive across play sessions as references to destroyed GameObjects). It is **not a player-facing bug** — a shipped build has a single play session.

## Proper fix options (if we decide it's worth addressing)
1. **`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` static reset** — null out each singleton's static `Instance` at the start of every play session so a stale reference never survives. Clean, build-safe, no semantic change. **Preferred.**
2. Re-enable Domain Reload on play (Project Settings → Editor → Enter Play Mode Settings) — simplest, but slows iteration.
3. A shared `SingletonBehaviour<T>` base that centralises the guard + a `RuntimeInitializeOnLoadMethod` reset, migrating the managers onto it.

## Acceptance
- Original "first-instance-wins" guard preserved in all managers (no last-wins inversion).
- Repeated Editor play sessions no longer leave a stale `Instance` (verify nav works on the 3rd+ consecutive play without restarting Unity).
- No change to shipped-build behaviour.

## Out of scope
Anything in `mode_select_system`. This task is independent.
