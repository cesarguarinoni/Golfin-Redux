# RETIRED — `character_recovery_stamina_regen`

**Retired:** 2026-07-16 16:52 JST (Architect, Cesar-confirmed)
**Disposition:** SUPERSEDED — closed without implementation. Moved `Docs/Specs/Queued/` → `Docs/Specs/Completed/`.

## Why

This spec was filed 2026-05-25 out of `stat_to_physics_mapping_audit` (Order 412) against finding
**F-LANA-REC**, whose premise was:

> "`CharacterStats.Recovery` is defined in the struct but the comment says '(informational; not used
> per-shot)'. At HEAD, Recovery has zero effect on any physics output."

**That premise is no longer true.** The Stamina/Condition Economy (Phases 1–5, shipped
2026-06-29 → 2026-07-03, Notion Order 516 Done) implemented session-level stamina regen and made
Recovery the regen-rate stat:

- `StaminaModel.RegenPerHour(int recoveryStat)` is live.
- Recovery now drives regen rate exactly as this spec's Tier-Redesign proposed.

The spec's own scope ("Recovery needs a session-level stamina regen implementation to be meaningful.
This is a game loop concern, not a per-shot resolver concern.") was therefore delivered by a
different, larger workstream.

## Finding status

**F-LANA-REC → CLOSED.** Reconciled in `Docs/Physics/STAT_LANE_AUDIT.md` § Filed Follow-up Specs
(commit `aa4ad719c`, 2026-07-16).

## No action required

Nothing in this folder should be implemented. If a future session wants to revisit *how much*
Recovery affects regen (as opposed to *whether* it does), that is a new tune against
`StaminaModel.RegenPerHour`, not a revival of this spec.

## Superseded-by

- Stamina/Condition Economy Phases 1–5 — Notion Order 516 (Done)
- `Docs/Physics/STAT_LANE_AUDIT.md` § Filed Follow-up Specs (verified-status column)
