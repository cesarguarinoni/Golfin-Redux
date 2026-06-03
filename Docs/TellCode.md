# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Shipped:** `green_ship_polish` — ✅ DONE, moved to `Docs/Specs/Completed/green_ship_polish/`. The terrain-apron MATERIAL fix landed with it; full record + do-not-repeat lessons live in that folder. **Do not re-report as pending.**
- **PRIMARY ACTIVE — `loop_v2_scope`** (`Docs/Specs/Active/loop_v2_scope/`). The Select Character → Clubs → Hole → play → result → next/menu glue. Scoping SPEC is SPEC_READY; the 5 open questions are **LOCKED (2026-05-19)** — Result modal = ShellScene-resident (Option B), cross-scene signal `GameSession.OnHoleComplete`. Stages: A singletons-consolidation → B session-state-plumbing → C result-modal (FULL PIPELINE) → D next-hole-autoflow → E hole-selection-entry → F animated-polish. **Next concrete action: fire Stage A** as its own sub-spec `loop_v2_a_singletons_consolidation/` (not yet created). Routing: A/B/D/E TELLCODE, C+F per spec.
- **PARALLEL ACTIVE — `lomond_greens_authoring_batch`** (`Docs/Specs/Active/`). FULL PIPELINE: author dense per-cell `green.json` slope grids for all 18 Lomond greens from the PDF strategy panels (primary), Shot Navi (cross-val), `greens.json` polygon (shape); visual-gate each hole vs the panel. ITER8 pilot on H07 in flight. Kickoff: `Use the golfin-implementer subagent on "lomond_greens_authoring_batch"`.
- **SIDE QUEST (today, 2026-06-03) — `ball_flight_trail`** (`Docs/Specs/Active/ball_flight_trail/SPEC.md`). SPEC_READY. State-colored mobile ball trail: blue flight+roll / whole-ribbon red on OB / gold on a clean (perfect) full-swing flick. 4 changes, no `BallAnimator` edits. Kickoff: `Use the implementer subagent on "ball_flight_trail"`.
- **Last updated:** 2026-06-03 (Architect — corrected stale headline: `green_ship_polish` shipped to Completed; repointed to loop_v2 primary + lomond_greens batch; logged `ball_flight_trail` side quest).

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
