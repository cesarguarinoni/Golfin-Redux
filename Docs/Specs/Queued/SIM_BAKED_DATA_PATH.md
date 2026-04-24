# SPEC (QUEUED) — Move sim to baked-data path; demote scene providers

**Date:** 2026-04-25
**Status:** Queued — **on standby pending tactical fix outcome**
**Pointer in:** `Docs/AI_CONTEXT.md` "On the Horizon" section
**Estimated effort:** 5–8 days (own design pass before kickoff)
**Activates when:** TERRAIN_REALTEST_FIX hits one of the trigger conditions below.

---

## Activation triggers — when to abandon tactical and pivot to this spec

Pivot to this architectural fix if ANY of these is true after the tactical TERRAIN_REALTEST_FIX runs:

1. **Phase B exhausts its 5-attempt budget without Cesar's manual confirmation passing.** If Code can't get a clean 5/5 manual pass after 5 fix attempts, the scene-coupled architecture is too brittle to repair in place.
2. **Phase A diagnostics reveal the broken-marker generation has multiple distinct sources** (e.g., HoleGeoImporter writes one path, asmdef rebuild produces another, scene save mangles a third). Multiple producers means no single fix is sufficient.
3. **Phase A reveals the bug is in the `_useSceneProviders` runtime wiring AND in marker authoring AND in `GetComponentInParent` traversal.** Three independent bugs in one system means the system is the bug.
4. **Cesar's manual Phase D fails twice on different shot types** after Code claims a fix. Means the tactical fix is whack-a-mole and the bug class is the architecture.
5. **The fix lands but a second hole import (Hole 2 or beyond) immediately reproduces the same fall-through.** Means the bug is reproducible by import, not specific to Hole_01.

If any of these triggers fire, Architect activates this spec immediately:
- Move spec from `Docs/Specs/Queued/` to `Docs/Specs/Active/`.
- Update TellCode.md pointer.
- Code starts Phase A0 (prerequisite check) the same session.

---

## Prerequisite check (Phase A0) — must run BEFORE any code changes

Before starting the architectural change, verify these inputs exist and are usable. If any is missing, that becomes the first sub-task.

1. **Zone polygon JSON** — does `Tools/UHoleGeo/` produce per-zone polygon data per hole? Where is it written? Is the schema sufficient (must include polygon vertices in world coords, zone type, optionally Y offset)? If schema is insufficient, extending it is the first work.
2. **Heightmap binary** — Phase 0 baker's `heightmap.bytes` exists per hole, confirmed. Verify format is documented and loader exists in `Golfin.Physics` (`HeightmapData`, presumably). Re-confirm sample interface matches what `BakedHeightProvider` will need.
3. **Per-zone Y offset data** — current importer applies Y offsets implicitly via mesh geometry (greens depressed 0.0m, bunkers depressed ~1.3m, water absolute-Y, etc.). These offsets need to be made explicit in JSON. Inventory: write to `Docs/DIAG/baked-prereq-YYYYMMDD.md` listing every zone type and its current Y offset behavior from HoleGeoImporter.
4. **Importer file location** — already found in TERRAIN_REALTEST_FIX Phase A1, just confirm path.
5. **Test infrastructure** — real-scene test pattern from TERRAIN_REALTEST_FIX Phase C is reusable. Same load-real-hole pattern, but assertions check baked providers instead of scene providers.

A0 deliverable: `Docs/DIAG/baked-prereq-YYYYMMDD.md` documenting the gaps. If gaps are large (e.g., zone JSON doesn't exist at all), Architect writes a Phase A0.5 to fill them before proceeding to Phase B.

---

## Why this exists

The current sim asks two questions per step — "what surface am I on?" and "what's the ground Y?" — by raycasting the live Unity scene and reading `SurfaceMarker` MonoBehaviour components. The 2026-04-25 fall-through bug was the third or fourth incarnation of the same class of failure: scene authoring is wrong (missing marker, broken script reference, marker on wrong GO in hierarchy, duplicate markers, etc.) and the sim silently picks up the bad data.

This is a brittle contract: sim correctness requires that every zone mesh GO carries a valid Physics marker on the right transform with the right Type field, AND that the importer producing those markers never regresses. Multiple things have to be right, on every import, on every hole.

## What this spec proposes

**Invert the trust hierarchy.** Sim reads from baked deterministic data that the importer pipeline produces directly. The Unity scene is purely visual.

### New components

1. **`BakedZoneClassifier : ISurfaceProvider`**
   - Reads zone polygons from JSON produced by `Tools/UHoleGeo/` and similar contour-mesh outputs (greens, bunkers, fairway, tee, fringe, water, cartpath).
   - Point-in-polygon test (with priority ordering: green > bunker > water > fringe > fairway > tee > cartpath > rough fallback).
   - Deterministic, no scene dependency, no PhysX.
   - Loaded once at hole load; classifies any (x,z) in O(log n) with a spatial index.

2. **`BakedHeightProvider : IGroundProvider`**
   - Reads existing `heightmap.bytes` (Phase 0 baker output) for terrain Y.
   - Adds per-zone Y offsets baked in JSON: `{ "zoneType": "Green", "polygons": [...], "yOffsetFromTerrain": 0.0 }`.
   - Returns `terrainY + zoneOffset(x,z)` if ball is inside any zone polygon; else `terrainY`.
   - Deterministic, no scene dependency.

3. **JSON schema additions** in `Tools/UHoleGeo/` outputs:
   - Per-zone-type Y offset (currently implicit in mesh geometry; needs to be explicit).
   - Spatial index hint (bbox tree or grid) for fast point-in-polygon.

### Demotions

- `SceneSurfaceProvider` and `SceneGroundProvider` become **debug/editor helpers**:
  - PhysicsLab placement-snap can still raycast the scene to "place ball on visible mesh."
  - The sim itself (BallSimulation.Simulate and friends) reads only from baked providers.
- `Physics.Runtime.SurfaceMarker` MonoBehaviour becomes **optional cosmetic metadata**, no longer load-bearing for sim correctness. Could be deleted entirely once nothing references it.

### Importer changes

- `HoleGeoImporter.cs` continues producing visual meshes (no change there).
- New importer step: write `Assets/Resources/HoleData/Hole_XX/zones.json` (or whatever path A0 settles on) with polygons + per-zone Y offsets + spatial index.
- Optional: stop producing `Physics.Runtime.SurfaceMarker` entirely (since sim no longer reads them). Cuts the "broken script reference" bug class at the source. **Default: yes, stop producing them.** Cesar can override if Inspector debugging value justifies keeping them.

## Benefits

- **Determinism.** Sim no longer depends on PhysX raycast order or scene authoring. Same input → same output, on any machine, including a server.
- **Server replay possible.** Required for AI Caddie's player-tendency tracking layer (see userMemories: AI Caddie deferred, decision brain server-side first). Architecturally enables that whole feature.
- **One source of truth.** Importer's JSON is authoritative. Audit/regenerate that one file; visuals follow separately.
- **Eliminates the bug class.** Marker present/missing/broken/wrong-hierarchy/duplicate failures all become impossible by construction.
- **Visual-vs-physical mismatch becomes cosmetic.** If a visible mesh is 2cm off the baked Y, the ball clips into the mesh visually but doesn't fall through anything. Bug becomes a visual polish item, not a gameplay-breaking failure.
- **Aligns with how golf-sim peers do it.** Other golf-physics dev discussions (gamedev.net forums, Perfect Golf workflows) consistently use heightmap-as-truth, mesh-as-decoration. This is the conventional architecture for the genre; the current scene-coupled approach is the unusual choice.

## Costs

- 5–8 day refactor with significant test churn.
- Phases 0–6 of physics already passed against scene providers. Need to port assertions to baked providers (mostly mechanical).
- Need to add the per-zone Y offset to JSON schema and updater for existing 18 holes.
- Spatial index implementation (bbox tree or grid) — needs benchmarking for sub-ms classification.
- Editor-time visual-vs-baked diff tool would be valuable so authoring drift is visible.

## Resolved design questions (defaults that activate without further discussion)

These were open in the original draft. Resolved with sensible defaults so the spec is closer to executable. Cesar can override any when activating, but if no override comes, Code uses these.

1. **JSON location:** `Assets/Resources/HoleData/Hole_XX/zones.json` (Resources folder). Loaded at runtime via `Resources.Load`. Same pattern as existing CSV-first data architecture (per Cesar's project rules). NOT Addressables (more complexity than needed for this); NOT StreamingAssets (Resources is the project convention).
2. **Heightmap with per-zone offsets:** apply at sample time, not bake-in. Keeps zone offsets live-editable in JSON without re-baking heightmap. Performance cost is one extra polygon-containment test per sample, mitigated by spatial index.
3. **Zone-edge transitions:** hard step, same as current fringe/green behavior Cesar already accepted. Apply uniformly to all zone boundaries. No smoothing band.
4. **Keep `SurfaceMarker` MonoBehaviours?** No — delete after migration completes. They're load-bearing in the broken architecture; in the new architecture they're dead weight that could be re-broken. If Cesar wants them back as Inspector debugging metadata later, that's a 1-day add.
5. **Test infrastructure:** both. Real-scene tests (reuse TERRAIN_REALTEST_FIX Phase C pattern) for end-to-end validation, plus unit tests against the JSON+heightmap directly for the new providers. The unit tests are now legitimate because the new providers ARE the source of truth — no synthetic-vs-real mismatch.
6. **Migration path:** re-run importer on all 18 holes to produce JSON. Editor tool one-shot: `GOLFIN > Tools > Bake Zone JSON (All Holes)`. Same pattern as `SyncPhysicsSurfaceMarkers.cs`. Holes that don't have current zone data (Holes 2–18 per yesterday's done report) will produce empty JSON until they get imported normally; that's correct behavior.

## Day 1 readiness checklist (for Architect when activating)

- [ ] Move this spec to `Docs/Specs/Active/SIM_BAKED_DATA_PATH.md`.
- [ ] Update TellCode.md pointer block.
- [ ] Update AI_CONTEXT.md status row to reflect activation.
- [ ] Confirm the trigger that activated this spec (which of the 5 conditions fired; one-line note in Day 1 commit message).
- [ ] Code starts Phase A0 (prerequisite check) immediately — output goes to `Docs/DIAG/baked-prereq-YYYYMMDD.md`.
- [ ] Architect reviews A0 output before greenlighting Phase B (implementation).

## Sequencing notes

- **Do NOT start until tactical fix is merged AND triggered for pivot OR confirmed dead.** Architectural refactor from a broken baseline is twice as hard.
- Schedule before any AI Caddie work begins (deterministic server replay is the prerequisite).
- Schedule before public testing if at all possible — public testers will surface marker authoring bugs at scale and that's exactly what this eliminates.

## Out of scope for this spec

- Rewriting BallSimulation's physics math. Phases 0–6 stay as-is.
- Replacing PhysX for collision detection in non-sim contexts (e.g. character controllers, cart camera). Only the sim's ground/surface lookups move to baked.
- Multiplayer netcode. The deterministic-replay capability this enables is a prerequisite, but the netcode itself is separate work.
