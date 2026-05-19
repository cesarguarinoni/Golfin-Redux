# STATUS — loop_v2_c0_matchmaking_to_gameplay_transition

**Status:** SPEC_READY (architect, 2026-05-19)
**Type:** TELLCODE — but Cesar-visual gate is heavier than typical TELLCODE (first end-to-end production playthrough in project history)
**Parent:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (Stage C splits into C0 + C1)
**Root cause:** `Docs/Specs/Queued/ARCHITECT_NOTE_matchmaking_to_gameplay_transition.md` (Stage B finding)
**Notion:** Loop v2 Order 330 (sub-item of 300)

## History
- 2026-05-19 — Stage B surfaced the missing transition: no production code loads any gameplay scene. Modal seeds GameSession then sits at OPPONENT FOUND. Architect note filed.
- 2026-05-19 — Cesar: "Do it." Architect recon found:
  - `GameplayScene.unity` exists but is empty March 2026 stub.
  - `Hole_01.unity` referenced in build settings has zero GUID — file doesn't exist (ghost entry).
  - `Hole_NN_Geo.unity` (1-47MB each, all 18 exist) at `Assets/Golf/Courses/lomond-country-club/Generated/` — production geometry, additively loadable.
  - `LabScaffold.unity` is canonical dev host with full physics stack; `PhysicsLabController.LoadHole(n)` already wires additive geo load.
- 2026-05-19 — SPEC.md written. Three architecture decisions locked: (D1) LabScaffold as gameplay host scene, (D2) additive load, (D3) all 18 holes via existing LoadHole(n). Build settings change is non-trivial (17 new Hole_NN_Geo entries + LabScaffold + remove ghost Hole_01).

## Notes for implementer
- Pre-flight grep is mandatory; PhysicsLabController.Start conflict is the single biggest risk (Risk #5 in SPEC).
- LoadingScreenController generalization (Stage D Part 1 work from scoping SPEC) is folded into C0 — natural fit since the loader is what shows during the scene-load wait.
- 5 new EditMode tests target 305/305. Cesar visual gate is the real gate.
