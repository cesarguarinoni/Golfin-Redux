# Architect follow-up — physics issues found during `sound_effects` audio testing

Cesar surfaced these while play-testing Hole 4 for the Order 350 audio pass (2026-06-16).
They are **NOT audio bugs** and are **out of scope** for Order 350 — recorded here so they land in
the DONE report for the architect to schedule as separate physics/terrain work.

1. **Ball passed THROUGH the fringe on Hole 4.** During a normal shot the ball went through the
   fringe/border mesh instead of colliding/resting on it. Likely a collider gap or the fringe/border
   submesh not carrying a collider on Hole_04_Geo. Cross-ref: `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md`.

2. **Ball fell THROUGH the terrain when shooting into a bunker on Hole 4.** The ball dropped out of the
   world / below the surface at a bunker on Hole 4 — classic stale-heightmap / missing-collider
   fall-through. Cross-ref the recurring pattern in user memory `project_stale_heightmap_terrain_fallthrough`
   (balls fall through terrain outside baked zones = stale `heightmap.bytes`; re-bake via
   `PhysicsHeightmapBaker` + copy to Resources) and the bunker-lip collider notes in `Docs/Pipeline/`.

Suggested next step for the architect: re-bake Hole_04_Geo's physics heightmap + audit the fringe/border
and bunker colliders, then verify with a bot shot into the Hole-4 bunker.
