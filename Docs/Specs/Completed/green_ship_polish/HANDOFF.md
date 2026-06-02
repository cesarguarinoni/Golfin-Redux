# HANDOFF — green_ship_polish (next conversation)

**Written:** 2026-05-30 (end of session)
**Status:** iter-13 DONE (issue 4 of 4). Three ship-blockers remain. Task is **Active**, not Completed.

---

## Where we are

`green_ship_polish` = four ship-blocking green-fidelity issues, fixed one at a time. All four BLOCK ship (Cesar's call — not polish, not deferrable).

Closed this session:
- **iter-13 — ridge-slope staircase bumps. DONE.** Fix landed in `Tools/GreenSlope/scripts/bake-green.mjs`: ridge-band smoothstep blend, drop-scaled band width (`rampWidth = clamp(tierDrop / 0.08, 1.0, 0.40·greenPerpWidth)`), and the root-cause **2-tier gate** — ridge barrier applies ONLY to holes 3/7/11/18 (the actual 2-tier greens per the PDF booklet 「２段グリーン」 p4/p8/p12/p19). Every other hole, incl. H14, is single-region: arrows carry the swale, no phantom cliff. Commits `71492c37` + spec `ee4b426c`.

Still OPEN (locked order):
- **iter-14 — fairway breaking around the green (NEXT)**
- **iter-15 — raised green ring (donut/pillow rim)**
- **iter-16 — off-center raise**

Spec + full iter-13 history (incl. both amendments): `Docs/Specs/Active/green_ship_polish/SPEC_ITER13.md`. Queue checklist at the bottom of that file is the source of truth for what's done.

## Closed earlier (context)

- `green_slope_authoring_tool` — DONE (the GreenSlope browser tool; Cesar traces PDF arrows → JSON).
- `green_slope_height_bake` — DONE through iter-12 (arrows→gradient→Poisson height, schema v2, mesh deform, grid-force break, bilinear+mask-dilation boundary fix). In `Completed/`.

## Two cleanup items I could NOT resolve and left for you/Code

1. **Code's DONE commit (`4ab82f18`) moved the whole `green_ship_polish` folder to `Completed/` after closing only iter-13.** I moved it back to `Active/` this session (3 of 4 issues still open). If Code's close-out automation did this, it has the same over-close bug as Lesson AA — worth a look so iter-14/15/16 don't get buried again.
2. **Untracked iter-11 diagnostic artifacts** sitting in the working tree, NOT committed, NOT mine to decide on:
   - `Assets/Scenes/Debug/Hole_07_Geo_Diagnostic.unity` (+ `.meta`, + `Assets/Scenes/Debug.meta`)
   - `Assets/Scripts/Editor/CourseImporter/Debug/` (the 4-variant diagnostic harness)
   These were the iter-11 isolation scene. Decision needed: commit them (keep as a regression tool, per iter-12 open-item 4) or delete. Left untouched.
   - Also uncommitted: a pile of `hole-07-geo` / `hole-14-geo` `.mat` + `TerrainData_*.asset` edits (reimport churn) and the usual NuGet/Packages/pycache noise. Reimport-generated; commit or discard per your call.

## How to start the next conversation

1. Upload `AI_CONTEXT.md` (per the session-start rule).
2. iter-14 (fairway breaking around the green) is NEXT. The defect: fairway mesh visibly cuts/overlaps at the green boundary on slope-having holes. Prior context lives in `ARCHITECT_ESCALATION.md` and the iter-5/iter-8 fairway-cut + skirt work in `Docs/Specs/Completed/green_slope_height_bake/`.
3. **Live importer is `HoleGeoImporter.cs`** — NOT HoleLiteImporter (deprecated, banner header, commit 980cc122). Verify entry point via `grep MenuItem` before touching importer internals.

## One process note for next time

iter 9, 10, 11 were burned chasing the wrong cause of the boundary bead (smoothing the contour / terrain coupling) before iter-12 found it was height-sampling discretization. Lesson that finally took: **verify the root cause in the data (probe `green.json` directly) before speccing a fix** — don't spec from the symptom or from a brief's framing. iter-13's 2-tier gate came from reading the PDF + green-reading convention, not from guessing at H14's geometry. Keep that discipline for iter-14.
