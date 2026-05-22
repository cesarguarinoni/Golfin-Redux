# STATUS — `loop_v2_f_button_press_feedback`

| Field | Value |
|---|---|
| Current state | **PART_A_SHIPPED / PART_B_SPEC_READY** |
| Created | 2026-05-22 ~09:30 CET |
| Architect | claude.ai |
| Implementer | Claude Code (Part B only) |
| Pipeline (Part A) | SURGICAL — shipped by Architect |
| Pipeline (Part B) | TELLCODE — Unity MCP scene/prefab edits |

## Timeline

- **2026-05-22 ~09:30 CET** — Pre-flight audit during Stage E preflight already confirmed zero `instant: true` offenders. Architect wrote `ButtonPressFeedback.cs` as a SURGICAL file, wrote this SPEC, and prepared the Part B attach table.
- **2026-05-22 — TBD** — Part B handed off to Code via TELLCODE.

## Part A change-set (for git scoping)

- `Assets/Scripts/UI/ButtonPressFeedback.cs` (new file)

## Part B change-set (anticipated)

- Prefab/scene edits via Unity MCP `add_component` on ~10 button surfaces (no code files touched).

## Open notes

- The smoke bot from Stage E Part B will naturally exercise most of these buttons, providing the visual gate for both Stage E and Stage F in a single PlayMode run.
- If any bottom-nav button names don't match the SPEC list (e.g. `NavRosterButton` vs `NavCharactersButton`), Implementer reports the actual name and we adjust.
