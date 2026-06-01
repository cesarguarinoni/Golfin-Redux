# TellCode.md — legacy handoff channel (POINTER FILE)

> **This file is no longer the source of truth for current work.** It exists so that a session that starts by "checking TellCode" is redirected correctly instead of reading stale task blocks. (Before 2026-05-31 the body held a growing pile of superseded DONE/NEXT blocks with no current-state pointer — a session-start would land on month-old tasks and miss the live one. That failure mode is what this rewrite fixes.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Active task:** `green_ship_polish` — green-fidelity ship-blockers. Two ordered passes replacing iter-14/15/16. PASS 1 done; PASS 2 ready to kick.
- **Done:** iter-13 (ridge staircase) commits `71492c37`+`ee4b426c` — *had a regression, fixed in PASS 1*. PASS 1 below.
- **PASS 1: tier-step-fix — ✅ DONE / Cesar-approved 2026-06-01** (`SPEC_TIER_STEP_FIX.md`, commit `13fe08d6`, verification `ARCHITECT_VERIFICATION_TIER_STEP.md`). iter-13's `smoothRidgeBand()` sized its ramp off whole-green relief instead of the tier STEP → H7's 2-tier smeared into one slope. Fix: `tierDrop = |mean(region0) − mean(region1)|`, rest of the function byte-identical. Result: H7 ramp 8.9 m→3.48 m, staircase did NOT return, 14 non-tier holes byte-identical. NOTE: H7 authored tier step is only **18.5 cm** → histogram stays quasi-unimodal even when correct (the step is genuinely gentle, not a dramatic shelf); the ramp-width + over-gate drop (40→8) + Cesar's in-engine orbit are the real proof, not the histogram. **Tier prominence to be RE-JUDGED after PASS 2 seats the green proud** (the current sunken seat masks the read).
- **NEXT — PASS 2: green-seat/seam re-architecture (B1) — SPEC_READY** (`SPEC_GREEN_SEAT_SEAM_B1.md`). Importer-only (`HoleGeoImporter.cs`), built on PASS 1's corrected relH. Replaces the scalar seat with a **terrain-following PLANE** fitted (least-squares) to terrain at the green's contour vertices, sampled at import (`green.json` carries no per-vertex terrain, only scalar `heightDatumY`). Interior = `seatPlane(x,z) + relH` → green follows its ground (no float, no sink), authored relH (incl. 18.5 cm tier) rides on top UNCHANGED. **NOT B2** (do not re-tilt relH to the terrain macro-slope — that overrides authored slope; verified distinct: H7 authored 0.0177 m/m vs terrain ~0.047 m/m). 5 changes: (0) revert the still-dirty adaptive-collar leftovers; (1) plane-fit seat; (2) flag/cup on plane+relH (currently float on old centroid datum); (3) collar inner ring on plane, width stays 0.9 m (small fringe); (4) **weld seam** — collar outer ring = fairway cut = terrain carve, ONE shared ring, coincident verts (cut-polygon-overhang has failed 3×; merged-mesh fallback pre-approved; 2 failed welds → adversarial review, NOT a 3rd cut variation).
- **Acceptance gate (Cesar, hard — ALL must hold):** 1) 2-tier/slopes respected (relH contribution epsilon-identical — the #1 proof), 2) fringe small band, 3) green doesn't float, 4) no green/fairway overlap & carved hole not visible, 5) flag/cup on surface, 6) green reads proud NOT sunken.
- **Cost (PASS 2):** physics re-bake REQUIRED (seat plane moves absolute interior Y; relH SHAPE identical → smooth plane offset, mechanical gate re-baseline). Tier 3. Likely subsumes iter-15/16 (confirm at verify).
- **History of failed attempts (do NOT repeat):** iter-14 adaptive-collar → MOUND + see-through (stopped/reverted, leftovers still in importer, Change 0 reverts). v1 perimeter-MIN flat seat → SUNKEN bowl (no flat scalar works on slope → hence B1's plane). Code's v2 raised-pad draft → not chosen (false-front mound risk).
- **Live specs + evidence:** `Docs/Specs/Active/green_ship_polish/` — `SPEC_GREEN_SEAT_SEAM_B1.md` (PASS 2, ready-to-kick), `SPEC_TIER_STEP_FIX.md` + `ARCHITECT_VERIFICATION_TIER_STEP.md` (PASS 1 done), `SPEC_GREEN_SEAT_REARCH.md` (v1 SUPERSEDED), `SPEC_GREEN_SEAT_REARCH_V2_DRAFT.md` (not chosen), `CESAR_REJECTION.md`, `ITER14_FAIRWAY_SEAM_DIAGNOSTIC.md`+`screenshots/`, `SPEC_ITER13.md` (queue at bottom authoritative).
- **Last updated:** 2026-06-01 18:15 JST (Architect — PASS 2 B1 specced, ready to kick).

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
