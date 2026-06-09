# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Shipped (do NOT re-report as pending):** `green_ship_polish` (Completed); **Loop v2 core** — A/B/C0/C1 + smoke-bot (Notion Orders 310–340), playable end-to-end since 2026-05-19, umbrella now in `Completed/loop_v2_scope/` (only Stage F animated-polish possibly-open — confirm before reopening); green authoring (`green_slope_authoring_tool` + `green_slope_height_bake`, Completed); **`ball_flight_trail`** — Code implemented (`4249c0da`) + closed to `Completed/ball_flight_trail/`.
- **Shipped since (do NOT re-report as pending):** `mode_select_system` (Order 341), `practice_1v1_matchmaking_split` (Order 342), `tap_feedback_fx`, and **`1v1_ingame_ui` Phase 1** (Order 343, 2026-06-08, commit `756ab280` — two-player versus HUD: P2 card cloned+flipped from P1, active/inactive 0.50 opacity, relocated minimap, `TurnBannerWidget`, `MatchContext` data layer, all gated behind `GameSession.IsVersus` with solo HUD untouched). All Completed.
- **NEXT / open:** `1v1_match_flow` (Order 344, Queued P2) — 1v1 **Phase 2**: production bot opponent AI (difficulty tied to bot level), turn-flow state machine (alternating shots, control lock, banner per turn), win/tie + winner banner. Spec not yet written; Architect authors when Cesar wants it. Open design calls listed in `Docs/Specs/Completed/1v1_ingame_ui/SPEC.md`.
- **Also queued:** physics audit follow-ups (`strength_velocity_short_game_scaling`, `club_control_aim_arrow_speed`, `ball_rebound_perceptibility`, `ball_roll_coefficient_retune`, `character_recovery_stamina_regen`), `physics_lab_controller_rename` (P3), `phone_build_smoke_test` (Order 420, blocked on Ken's dev-account issue). Card-editability specs are non-gated and can run ahead.
- **Last updated:** 2026-06-08 (Architect — closed `1v1_ingame_ui` Phase 1 to Completed; Notion 343→Done, filed 344 `1v1_match_flow` for Phase 2).

---

## How current work is actually tracked now

1. **Live queue = `Docs/Specs/Active/`** — every pending/in-flight task is a folder there. That directory is authoritative for "what's open," not any inline list in this file.
2. **Per-task handoff = `Docs/Specs/Active/<slug>/HANDOFF.md`** (when present) — the next-session brief for that task.
3. **Completed tasks = `Docs/Specs/Completed/<slug>/`** — each closed task keeps its full record in its own folder.
4. **Session state headline = `Docs/AI_CONTEXT.md`** — upload at session start (project rule).
5. **Pre-2026-05-01 narrative history = `Docs/Archive/TELLCODE_HISTORY.md`.** The 2026-05 consolidated DONE/NEXT narrative that used to live in this file is preserved in **git history** — recover with `git show <pre-2026-05-31-commit>:Docs/TellCode.md` if ever needed.

## Rules (unchanged)

- Do **not** write new active tasks into this file. Specs go in per-task folders under `Docs/Specs/Active/<slug>/SPEC.md`.
- New UI tasks use the multi-agent pipeline at `.claude/agents/` (see `CLAUDE.md` § Multi-Agent Workflow).
- Live course importer is `HoleGeoImporter.cs` (NOT `HoleLiteImporter.cs` — deprecated, banner header, commit 980cc122). Verify via `grep MenuItem` before touching importer internals.
