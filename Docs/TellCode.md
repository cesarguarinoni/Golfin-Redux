# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Shipped (do NOT re-report as pending):** `green_ship_polish` (Completed); **Loop v2 core** — A/B/C0/C1 + smoke-bot (Notion Orders 310–340), playable end-to-end since 2026-05-19, umbrella now in `Completed/loop_v2_scope/` (only Stage F animated-polish possibly-open — confirm before reopening); green authoring (`green_slope_authoring_tool` + `green_slope_height_bake`, Completed); **`ball_flight_trail`** — Code implemented (`4249c0da`) + closed to `Completed/ball_flight_trail/`.
- **PRIMARY ACTIVE — home mode-selection rework** (2 specs in `Docs/Specs/Active/`):
  - `mode_select_system` — SPEC_READY, FULL PIPELINE. 4 modes (Practice / 1v1 / Driving Range-locked / Missions-locked) over two surfaces: home horizontal carousel + full-screen Mode Select (clone of Hole Select) reached via the bottom-nav tee button. CSV-driven fee+rewards (`modes.csv`), locked-card treatment from holes, RP economy via `RewardPointsManager`. Kickoff: `Use the implementer subagent on "mode_select_system"`.
  - `practice_1v1_matchmaking_split` — SPEC_READY, TELLCODE. Move fake matchmaking OFF Practice (solo, seed+load direct) and ONTO 1v1 (random hole 1–18 + random opponent). Touches shipped C0 seed path. Kickoff: `Use the implementer subagent on "practice_1v1_matchmaking_split"`.
  - Economy-gate UX RESOLVED (no Figma needed): disable PLAY + red `#C04000` fee amount + `ToastController` toast on tap-anyway. Both specs clear to implement.
- **Retired orphan:** `lomond_greens_authoring_batch` → `Completed/` (abandoned auto-PDF-read approach, superseded by `green_slope_authoring_tool`; stale leftover in Active, not real work).
- **Roadmap TODO (Notion):** add **"1v1 in-game UI"** (opponent HUD / turn order / versus scoring) — gated on Cesar's upcoming Figma.
- **Last updated:** 2026-06-03 (Architect — Loop v2 + greens + ball_flight_trail all confirmed shipped; repointed to the mode-selection rework; retired the greens orphan).

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
