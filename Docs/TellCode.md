# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Active task:** `green_ship_polish` — four ship-blocker green-fidelity issues, fixed one at a time, locked order. All four BLOCK ship.
- **Done:** iter-13 (ridge-slope staircase) — drop-scaled ridge-band smoothstep + 2-tier gate (barrier only on holes 3/7/11/18). Commits `71492c37` + `ee4b426c`.
- **NEXT:** **iter-14 — fairway breaking around the green.** Then iter-15 (raised green ring), iter-16 (off-center raise).
- **Live spec + history:** `Docs/Specs/Active/green_ship_polish/` (`SPEC_ITER13.md` = full iter-13 record incl. amendments; `HANDOFF.md` = next-session brief; queue checklist at bottom of `SPEC_ITER13.md` is authoritative).
- **Last updated:** 2026-05-31 15:10 JST (Architect).

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
