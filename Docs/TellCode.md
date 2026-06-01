# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Active task:** `green_ship_polish` — green-fidelity ship-blockers. iter-13 shipped but found to have a regression (below); iter-14/15/16 collapsed into a seat/seam re-architecture. Two ordered passes now queued.
- **Done:** iter-13 (ridge-slope staircase) — drop-scaled ridge-band smoothstep + 2-tier gate (holes 3/7/11/18). Commits `71492c37` + `ee4b426c`. **⚠ Has a regression — see PASS 1.**
- **NEXT — PASS 1 (do FIRST): tier-step-fix — SPEC_READY** (`SPEC_TIER_STEP_FIX.md`). Cesar flagged H7 lost its 2-tier (reads as a single slope). Root cause confirmed in code+data: **iter-13's `smoothRidgeBand()` sized its ramp band off `tierDrop = whole-green relief (hMax−hMin)` instead of the tier STEP** (`bake-green.mjs` L446-453). H7: rampWidth = 1.5×0.474/0.08 ≈ 8.9 m band on a green whose shelves are ~12 m apart → band ate both flats → shelves smeared into one ramp (plane-fit: 0.443 m of 0.474 m spread is planar tilt; histogram unimodal = no shelf gap). **Fix: redefine `tierDrop` = |mean(region0) − mean(region1)|** (regionGrid already in scope) so band sizes to the real step; everything else in the function byte-identical (staircase fix preserved, just corrected input). Re-bakes 4 tier holes {3,7,11,18}. Bake-only, no importer, no schema. **Objective gate: H7 relH histogram must become BIMODAL** (+ stay C¹, no staircase return) + Cesar visual sign-off "two shelves". Affects all 4 tier greens (all were suspect; H18 relief 0.512 m widest band).
- **THEN — PASS 2: green-seat/seam re-architecture (B1).** BLOCKED until PASS 1 lands — it changes `relH` (real shelves return), and the seat model must build on corrected heights. Direction LOCKED with Cesar = **(B) terrain-following green**, specifically **(B1)**: seat the interior on a gentle plane fitted to the green's own perimeter TERRAIN (green follows its ground → no float, no sink, small fringe), keep authored `relH` on top UNCHANGED (do NOT re-tilt to full terrain slope = that's B2, rejected — it overrides authored slope). Plane-fit on H7 proved viable (authored ramp 0.017 vs terrain 0.047 m/m). Plus the two mechanical fixes that ride along: flag/cup seated at `greenSeatY + relH(pin)` (L2666/L2688 currently use old centroid datum → float), and **welded seam** (coincident verts, NOT just coincident cut polygon — v1 skipped the real weld; cut-polygon approach has failed 3 ways → merged-mesh or true vertex weld ONLY, no more cut-contour variations). Single merged green+fairway mesh pre-approved as fallback (surface types stay distinct via submesh materials). Spec PASS 2 only AFTER PASS 1 sign-off, against final relH.
- **Acceptance gate (Cesar, hard — applies to PASS 2):** 1) 2-tier/slopes respected, 2) fringe small band, 3) green doesn't float, 4) no green/fairway overlap & carved hole not visible, (+5) flag/cup on surface, (+6) green reads raised/proud not sunken. ALL must hold.
- **History of failed attempts (do NOT repeat):** iter-14 adaptive-collar (widened collar → MOUND + see-through) STOPPED/reverted. v1 rearch perimeter-MIN flat seat → SUNKEN bowl (a flat datum on sloped terrain floats on low side OR sinks on high side — no flat scalar works; hence B1's fitted plane). Code's `SPEC_GREEN_SEAT_REARCH_V2_DRAFT.md` (raised-pad) = NOT chosen (false-front mound risk); B1 chosen instead.
- **Live specs + evidence:** `Docs/Specs/Active/green_ship_polish/` — `SPEC_TIER_STEP_FIX.md` (PASS 1, ready-to-kick), `SPEC_GREEN_SEAT_REARCH.md` (v1 — SUPERSEDED, perimeter-min disproven), `SPEC_GREEN_SEAT_REARCH_V2_DRAFT.md` (Code's raised-pad draft — NOT chosen, B1 chosen), `CESAR_REJECTION.md` (v1 four-observation rejection + diagnostics), `ITER14_FAIRWAY_SEAM_DIAGNOSTIC.md` + `screenshots/` (carve-seam evidence), `SPEC_ITER13.md` (queue at bottom authoritative).
- **Reminder:** stopped adaptive-collar code is still IN `HoleGeoImporter.cs` (constants L75–86, block L2558–2581, blend L2845–2849) + regenerated meshes in the working tree — reverts as part of PASS 2. PASS 1 is bake-only and does not touch the importer.
- **Last updated:** 2026-06-01 17:30 JST (Architect).

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
