# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Shipped (do NOT re-report as pending):** `green_ship_polish` (Completed); **Loop v2 core** — A/B/C0/C1 + smoke-bot (Notion Orders 310–340), playable end-to-end since 2026-05-19, umbrella now in `Completed/loop_v2_scope/` (only Stage F animated-polish possibly-open — confirm before reopening); green authoring (`green_slope_authoring_tool` + `green_slope_height_bake`, Completed); **`ball_flight_trail`** — Code implemented (`4249c0da`) + closed to `Completed/ball_flight_trail/`.
- **Shipped since (do NOT re-report as pending):** `mode_select_system` (341), `practice_1v1_matchmaking_split` (342), `tap_feedback_fx`, **`1v1_ingame_ui` Phase 1** (343, `756ab280`), **`1v1_match_flow` Phase 2a** (344, `ec9ee885`), **`versus_bot_hardening`** (345, `4e700ae5`), and **`versus_bot_difficulty` Phase 2b** (Order 346, commit `5bee024c`, closed 2026-06-11 — level→error-band difficulty via `bot_difficulty.csv` (6 brackets, levels 1–240), post-decision injection with no safety re-check, H2-safe club noise via power re-inversion, putt noise suppression, `DebugLevelOverride`; dispersion proven lv1 ±5.5° vs lv180 ±0.38°; full pipeline incl. red-team PASS). All Completed.
- **NEXT / open:** TBD with Cesar — **the 1v1 versus pillar is closed for this version** (banner end-state is final; the only gap, showing the 200 RP win reward, is deferred as `1v1_result_rewards_display`, Order 347, P3 Queued — do NOT spec it unprompted). Candidates: Rankings (next roadmap pillar after Loop v2), the five queued physics-audit follow-up specs (already written in `Docs/Specs/Queued/`), card-editability specs (non-gated), Taiheiyo course import continuation.
- **Also queued:** physics audit follow-ups (`strength_velocity_short_game_scaling`, `club_control_aim_arrow_speed`, `ball_rebound_perceptibility`, `ball_roll_coefficient_retune`, `character_recovery_stamina_regen`), `physics_lab_controller_rename` (P3), `phone_build_smoke_test` (Order 420, blocked on Ken's dev-account issue). Card-editability specs are non-gated and can run ahead.
- **Last updated:** 2026-06-11 (Architect — Cesar descoped 2c: banner is the final end-of-match state for this version; reward display deferred as Order 347 P3. 1v1 pillar CLOSED. Next task TBD with Cesar.)

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
