# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Active task:** `green_ship_polish` — green-fidelity ship-blockers. iter-13 DONE; iter-14/15/16 now collapsed into ONE root-cause re-architecture track (below).
- **Done:** iter-13 (ridge-slope staircase) — drop-scaled ridge-band smoothstep + 2-tier gate (barrier only on holes 3/7/11/18). Commits `71492c37` + `ee4b426c`.
- **NEXT:** **green-seat re-architecture — SPEC_READY** (`SPEC_GREEN_SEAT_REARCH.md`). Replaces iter-14/15/16. The iter-14 adaptive-collar attempt was STOPPED & reverted (it widened the collar → exposed the flat interior seat as a MOUND + still showed the carved hole through). Root cause confirmed in code+bake data: green pad seated on a single FLAT datum at **centroid** terrain height (`HoleGeoImporter.cs` L2806), while `relH` (the height grid) is the authored, terrain-independent, min-shifted slope shape (`bake-green.mjs` L672/L765). On a green whose terrain falls from centroid→leading edge (H7 ~0.55 m), the low edge floats → wall + carve see-through. **Fix (4 changes, all 4 of Cesar's acceptance points):** (1) seat datum centroid→**perimeter-min terrain** (edge meets ground, no float; interior stays flatDatum+relH so slopes/2-tier untouched); (2) **revert adaptive-collar entirely** (fringe back to small 0.9 m band); (3) **weld fairway cut to collar outer ring** (shared verts, no annulus → no see-through/overlap); (4) one shared ring drives collar outer + fairway cut + terrain carve. **relH NEVER touched.** Cost: physics re-bake REQUIRED (green Y shifts down on low-seat holes; interior SHAPE identical → mechanical gate re-baseline). Tier 3, multi-session, likely subsumes 15/16 (confirm at verify).
- **Acceptance gate (Cesar, hard):** 1) 2-tier/slopes respected, 2) fringe small band, 3) green doesn't float, 4) no green/fairway overlap & carved hole not visible. Solution rejected unless ALL four hold. Single fairway+green mesh pre-approved as fallback if the weld won't hold (surface types must stay distinct).
- **Live spec + evidence:** `Docs/Specs/Active/green_ship_polish/` — `SPEC_GREEN_SEAT_REARCH.md` (ready-to-kick), `ITER14_FAIRWAY_SEAM_DIAGNOSTIC.md` + `screenshots/iter14_*` (root-cause evidence; the grey triangles are the carved fairway hole, per Cesar), `SPEC_ITER14.md` (SUPERSEDED — adaptive collar, do not implement), `SPEC_ITER13.md` (iter-13 record; queue at bottom authoritative).
- **Reminder:** the stopped iter-14 adaptive-collar code is still IN `HoleGeoImporter.cs` (constants L75–86, block L2558–2581, blend L2845–2849) and the working tree has its regenerated meshes — Change 2 reverts all of it. Confirm revert before kicking, or kick the spec (it reverts as step 2).
- **Last updated:** 2026-05-31 16:30 JST (Architect).

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
