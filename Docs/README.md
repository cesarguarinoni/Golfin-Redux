# Docs/ — Index

> Map of what lives where in the documentation tree.
> Reorganized 2026-04-25.

---

## Top level (everyday-use)

| File | Purpose |
|------|---------|
| `AI_CONTEXT.md` | Project state, pipeline overview, session changelog. Upload at the start of every session. |
| `TellCode.md` | Architect → Claude Code handoff. Read at the start of every Claude Code session. |
| `Tasks.md` | Current task checklist. |
| `Rules.md` | Design constraints, Figma specs, conventions. |
| `README.md` | This file. |

---

## Subfolders

### `Architecture/` — codebase reference
Structural references to the live codebase. Read these when working on UI or system-wide changes.

- `RUNTIME_BLUEPRINT.md` — **Living runtime architecture reference**: namespaces, asmdef boundaries, manager APIs, asset locations, hole-loading flow. Update whenever a session touches manager APIs / asmdef refs / asset paths. Maintenance rule for both Architect and Code is in the doc header.
- `ARCHITECTURE_AUDIT.md` — auto-generated file tree, singletons, events. Regenerate via `Scripts/generate_audit.ps1` (Win) or `Scripts/generate_audit.sh` (Mac/Linux).
- `PATTERNS.md` — recurring patterns across the codebase.
- `UI_HIERARCHY.md` — scene UI paths reference.
- `INVENTORY_REFERENCE.md` — patterns, file locations, APIs for all inventory screens.

### `Physics/` — physics architecture and lessons
The physics layer and everything we learned getting there.

- `PHYSICS_RESEARCH.md` — architecture decisions, library survey, 6-phase implementation plan.
- `PHYSICS_TUNING_TARGETS.md` — canonical physics numbers (carry distances, stat→modifier mappings, RP costs, surface coefficients).
- `LESSONS_PHYSICS_AERO.md` — aero remediation lessons + future tightening options. **Read before touching aero LUTs.**
- `LESSONS_PHYSICS_SURFACE_MARKERS.md` — surface-marker / heightmap rationale.
- `SPEC_PHASE6_STAT_COUPLING.md` — Phase 6 spec.
- `PHASE6_STAT_COUPLING_REPORT.md` — Phase 6 done report.
- `SURFACE_MARKER_FIX_REPORT.md` — surface marker fix done report.

### `Pipeline/` — course pipeline and lessons
How holes are built, plus hard-won lessons from the pipeline work.

- `ADD_HOLE.md` — end-to-end procedure for adding a new hole.
- `BUNKER_RESEARCH.md` — bunker mesh research.
- `BUNKER_V2_SPEC.md` — bunker V2 spec (contour-based mesh).
- `TEE_SKIRT_INVESTIGATION.md` — tee skirt investigation log.
- `LESSONS_FRINGE_BORDER_MESHES.md` — **Read before touching fairway/tee fringe code.** Submesh baking, dilated CDT, Lite vs Geo importer trap.

### `Reference/` — read-only reference material
Source-of-truth design documents and reference PDFs.

- `Golfin - Confluence.pdf` — original GDD (full size).
- `Golfin - Confluence_compressed.pdf` — compressed version for browsing.
- `Golfin - Confluence.txt` — text extraction.
- `Golfin Redux. - Current screens.pdf` — current screens reference.
- `GAME_DESIGN_AGENT.md` — AI agent for evaluating GDD systems.

### `Game Design/` — game-design source-of-truth
Living game design documents, naming conventions, and visual references.

- `GAME_DESIGN_CHANGELOG.md` — design changes from original GDD.
- `ASSET_NAMING_CONVENTION.md` — asset & file naming rules.
- `GAMEPLAY_FORMULAS_PROPOSAL.md` — simplified gameplay formulas.
- `SHOT_CONTROLS_DESIGN.md` — shot control v1 design (authoritative for Phase 7).
- `In-Game - Shot Tests *.png` — shot control mockups.
- `New Levels.xlsx`, `Old Levels.xlsx`, `Old Gameplay Formulas.xlsx` — design spreadsheets.
- `New Controls.docx`, `Old Control Fixes.docx`, `Oringal Shot Controls.docx`, `Fixes.docx` — design notes.

### `Specs/` — task specifications
Specs split from `TellCode.md` when they exceed ~50 lines.

- `Active/` — specs currently being worked on.
- `Queued/` — specs written but not yet handed to Code.
- `Completed/` — archived specs after task is done (long-term reference).

### `Diagnostics/` — in-flight diagnostic outputs
Per-step CSVs, milestone done reports, audit dumps. Most contents are working files for the active task; archive or delete after the task lands.

- `baked-pivot/` — outputs from the 2026-04-25 baked-data sim pivot (M0–M5, full pivot report, Phase E ready report).
- `realtest-20260425/` — outputs from the terrain realtest fix work that preceded the pivot.

### `Backups/` — restore points for risky migrations
Pre-fix snapshots created before destructive changes. Safe to delete once the relevant task is merged and stable.

### `Archive/` — historical / completed phase specs
Completed task specs and superseded plans. Kept for context, not actively maintained.

### `Scripts/` — helper scripts
Utility scripts referenced from `CLAUDE.md` session-startup.

- `generate_audit.ps1` / `generate_audit.sh` — regenerate `Architecture/ARCHITECTURE_AUDIT.md`.
- `compress_screenshots.py` / `compress_screenshots.ps1` — compress screenshots to ≤800px.
- `crop_scene_gray.py` — crop helper.
- `daily_report.py` — Telegram daily report generator (Ken).

### `Golf Courses/` — per-course assets
Course-specific reference material.

- `Lomond/` — Lomond Country Club assets.

---

## Naming conventions

- **Folders:** `PascalCase` (e.g. `Architecture`, `Game Design`, `Specs/Active`).
- **Markdown files:** `SCREAMING_SNAKE_CASE.md` for specs/research/lessons (matches existing convention everywhere).
- **Mixed-case markdown:** `AI_CONTEXT.md`, `TellCode.md`, `Tasks.md`, `Rules.md`, `README.md` are the four/five "everyday-use" files at root and break the convention deliberately so they stand out.

---

## When in doubt

- Adding a new lesson? → `Pipeline/` or `Physics/` depending on topic.
- Adding a new spec? → `Specs/Active/` (move to `Specs/Completed/` when done).
- Generating diagnostic output during a task? → `Diagnostics/<task-name>/`.
- Adding a permanent reference doc? → `Reference/` if static; `Architecture/` if it describes live code.
- One-off helper script? → `Scripts/`.

If a file at top level isn't `AI_CONTEXT.md`, `TellCode.md`, `Tasks.md`, `Rules.md`, or `README.md`, it should probably be in a subfolder.
