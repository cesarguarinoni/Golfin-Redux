# STATUS — `tree_collisions`

**State:** SPEC_READY
**Notion:** Order 348 (P2, Gameplay Polish) · Phase 2 (tree-aware bot) = Order 351
**Spec:** `Docs/Specs/Active/tree_collisions/SPEC.md`
**Prepared:** 2026-06-11 (Architect; design Cesar-approved same day)

## History
- 2026-06-11 — Investigation: three tree placement paths (TreePlacer terrain trees, TreePlacer StandaloneTrees GOs, TreeBrushTool PaintedTrees mixed), unified via per-hole bake into the deterministic sim (ball is not a Unity rigidbody — colliders irrelevant). SPEC written. Locked: damping-only canopy, trunk hard reflect, all sim phases, auto re-bake on scene save, bot tree-awareness deferred to Order 351, dormant packs untouched. Awaiting Cesar kickoff.
