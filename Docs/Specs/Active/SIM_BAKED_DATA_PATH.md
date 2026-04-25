# SPEC (ACTIVE) — Move sim to baked-data path; demote scene providers

**Date:** 2026-04-25 — activated
**Status:** Active — handoff to Claude Code
**Pointer in:** `Docs/TellCode.md`
**Estimated effort:** 5–8 days
**Activation trigger:** Multiple. Tactical fix has whack-a-moled three distinct failure modes (synthetic test theatre → marker bug → high-velocity launch bug → instant fall-through bug, the last of which Architect failed to diagnose from B'1 data). Triggers #1, #4, and #5 from the original queued spec all fire. Architect explicitly requested by Cesar.

---

## ⚠️ ACTIVATION CONTEXT — read before doing anything

The architecture is being replaced because the scene-coupled provider model has produced a series of bugs over two days, none of which we've definitively fixed. The bug Cesar actually saw — "ball instantly falls through the green/bunker right where launched, into the void below" — was NEVER reproduced by automated tests. B'1 reproduced a different bug (ball flies past terrain bounds and free-falls in +Z direction). Architect spent two messages spec'ing fixes for that wrong bug before Cesar called it out.

The lesson: scene-coupled physics produces emergent failures that our diagnostic tools struggle to faithfully reproduce. The architectural fix eliminates the entire bug class by making the sim read from authoritative baked data (JSON polygons + heightmap.bytes), with the visible Unity scene as decoration.

**This spec is the path forward, not another tactical patch.** No reverting to scene-coupled providers, no "we'll fix the +Z bug later." Heightmap and zone polygons become the source of truth; sim never raycasts the scene again.

---

## Execution rules (Cesar's constraints — non-negotiable)

These rules supersede any conflicting instructions elsewhere in the spec.

### Rule 1: Branch first

Before ANY code change in this spec, Code creates and checks out a new branch:

```
git checkout -b sim-baked-data-path
git push -u origin sim-baked-data-path
```

All commits go on this branch. `main` stays untouched until Cesar approves the merge in Phase E (final gate). Tag the pre-pivot state as well: `git tag pre-baked-pivot` on the current `main` HEAD before branching.

If Code needs to abandon the branch and return to tactical work for any reason, the rollback is `git checkout main` and the branch stays as a record of the attempt.

### Rule 2: Code-driven validation; minimize Cesar's involvement

Cesar does NOT manually test until the **final gate (Phase E)**. Every milestone before that is validated by automated tests in real Hole_01_Geo, written and run by Code.

**The canonical regression test is mandatory** and must be in place from Milestone 1 onward:

> `RegressionTest_DriverFromBunker_DoesNotFallThrough` — additively load Hole_01_Geo, place ball at Bunker_1 centroid, fire driver in 8 cardinal directions (N/NE/E/SE/S/SW/W/NW), assert for every shot that ball.Y at every step never drops more than 0.05m below the surface that the BAKED classifier returns at the ball's XZ. Same test for Green_1 with putter and 3-iron.

This test must FAIL on the current scene-coupled architecture (proves it reproduces Cesar's bug) and PASS on the baked architecture (proves the fix). Code's first deliverable in Milestone 1 is to make this test exist and fail correctly.

If Code cannot get the regression test to fail on current architecture, that's a finding — surface to Architect. Do not proceed.

### Rule 3: Maximum autonomy; structured handoff

Code runs end-to-end through milestones M1 through M4 without human prompting. After each milestone, Code writes `Docs/DIAG/baked-pivot/MILESTONE_N_DONE.md` with these exact fields:

```
# Milestone N — <name>

## Status: PASS | FAIL | BLOCKED

## What ran
- <test name>: <pass/fail>, <duration>, <notes>
- ...

## Artifacts
- <path to file Code wrote/modified>
- ...

## Regression test result
- RegressionTest_DriverFromBunker_DoesNotFallThrough: <PASS/FAIL>
- RegressionTest_PutterFromGreen_StaysOnGreen: <PASS/FAIL>
- RegressionTest_DriverFromGreen_StaysOnGreen: <PASS/FAIL>

## Commits
- <hash>: <one-line summary>
- ...

## Blockers (if BLOCKED)
- <description>

## Next milestone ready: YES | NO

## Notes for Architect (optional)
- <anything that needs review>
```

Architect (in a future session) reads `MILESTONE_N_DONE.md` files directly via filesystem MCP. No need for Cesar to copy-paste outputs.

**Code proceeds autonomously between milestones if Status==PASS and Next milestone ready==YES.** No need to wait for Architect approval between M1→M2→M3→M4. Only stops at:
- Any milestone marked FAIL or BLOCKED.
- The end of M4 (handoff to Cesar for Phase E manual confirmation).
- Hard architectural questions that aren't covered by this spec's resolved design defaults.

### Rule 4: Cesar's final gate (Phase E)

After M4 passes, Code stops and writes `Docs/DIAG/baked-pivot/PHASE_E_READY.md` with:
- Summary of all 4 milestones.
- Branch state (commits, files touched).
- Manual test instructions for Cesar (5 specific shots in PhysicsLab).
- Merge instructions (`git checkout main && git merge sim-baked-data-path --no-ff` after Cesar confirms).

Cesar fires 5 shots manually. If all clean → Cesar merges. If any fails → Cesar reports which one to Architect, who specs M5 (targeted fix on the same branch) without restarting the whole architecture.

### Rule 5: No speculative fixes during diagnostics

Phase A0 (prerequisite check) is read-only. No code changes. Same pattern that worked for tactical Phase A: investigate, document, hand to Architect. If Code thinks "this would be quick to fix while I'm here" — it is recorded in MILESTONE_0_DONE.md "Notes for Architect" and not acted on.

This rule has been violated twice in the past two days. Don't make it three.

---

## Milestones (executable end-to-end by Code)

### Milestone 0 — Branch + Prerequisite check (read-only)

**Goal:** branch created, baseline confirmed broken, inputs inventoried.

Steps:
1. Tag current HEAD: `git tag pre-baked-pivot && git push origin pre-baked-pivot`.
2. Create branch: `git checkout -b sim-baked-data-path && git push -u origin sim-baked-data-path`.
3. **Write the regression test FIRST** (before any architectural code). Place at `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs`. Three tests:
   - `RegressionTest_DriverFromBunker_DoesNotFallThrough` (8 directions from Bunker_1)
   - `RegressionTest_PutterFromGreen_StaysOnGreen` (8 directions from Green_1 centroid)
   - `RegressionTest_DriverFromGreen_StaysOnGreen` (8 directions from Green_1 centroid)
   - Invariant: at every sim step, `ball.Y >= classifiedSurfaceY - 0.05f`. The classifier in this initial version uses `SceneSurfaceProvider` and `SceneGroundProvider` (current architecture) — that's intentional, because the test must fail on current architecture.
4. Run the regression tests on the current code. Expected: AT LEAST ONE shot in `RegressionTest_DriverFromBunker_DoesNotFallThrough` and possibly `RegressionTest_DriverFromGreen_StaysOnGreen` should FAIL (Cesar's manual repro). Record per-direction pass/fail.
5. **If no test fails** — STOP. The regression test isn't reproducing Cesar's bug. Surface to Architect with the per-direction breakdown. Do not proceed to M1.
6. **If at least one test fails** — proceed. The failing case is now the canonical regression we'll fix. Commit the regression tests with the failing baseline noted in commit message: `m0-regression-baseline: <N>/<8> directions fail on current architecture`.
7. Inventory the prerequisite inputs (read-only):
   - Find `Tools/UHoleGeo/scripts/` outputs. List every JSON file produced per hole. Schema of each. Save inventory to `Docs/DIAG/baked-pivot/M0-uhole-geo-outputs.md`.
   - Find `heightmap.bytes` location and format. Confirm `HeightmapData` loader in `Golfin.Physics`. Sample interface. Save to `Docs/DIAG/baked-pivot/M0-heightmap-format.md`.
   - Inventory current per-zone Y offsets (greens depressed how much, bunkers how much, water absolute-Y at what value, cart paths flat-drop how much). Read `HoleGeoImporter.cs` and document the offsets it applies. Save to `Docs/DIAG/baked-pivot/M0-zone-offsets-inventory.md`.
8. Write `MILESTONE_0_DONE.md`. Status: PASS if regression test fails as expected and all inventory complete. FAIL otherwise.

**No production code changes in M0.** Only the new regression test file (which is test code) and three diagnostic markdown files.

### Milestone 1 — `BakedZoneClassifier` (the SurfaceProvider)

**Goal:** new classifier reads zone polygons from baked JSON. Existing tests still pass when the sim still uses the OLD classifier; new classifier is implemented and unit-tested in isolation.

Steps:
1. Define JSON schema. Write `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` with the C# types that mirror the JSON. Schema:
   ```json
   {
     "holeId": "Hole_01",
     "zones": [
       {
         "type": "Green",
         "polygons": [ [[x1,z1],[x2,z2],...], ... ],
         "yOffsetFromTerrain": 0.0
       },
       ...
     ]
   }
   ```
   Priority order for point-in-polygon (highest priority wins): `Green > Sand > Water > GreenCollar > Tee > CartPath > Fairway > Rough (default)`.
2. Write `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` implementing `ISurfaceProvider`. Loads `ZoneData` from JSON via constructor. Point-in-polygon test for each zone in priority order; first match wins. Uses a simple flat scan for now (no spatial index) — performance optimization is M2 if needed.
3. Write `Assets/Scripts/Physics/Tests/BakedZoneClassifierTests.cs` (EditMode):
   - Empty zone data → `Classify` returns `Fairway`.
   - Single Green polygon, point inside → `Green`. Point outside → `Fairway`.
   - Overlapping Green and Bunker, point inside both → `Green` (priority).
   - Boundary point (on polygon edge) — document chosen behavior (in/out) and assert it.
   - Round-trip: `JsonUtility.ToJson` → `JsonUtility.FromJson` → identical classification.
4. Write `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs`:
   - Menu: `GOLFIN > Tools > Bake Zone JSON (All Holes)`.
   - For each Hole_XX_Geo scene, walk the zone GO hierarchy (Greens/, Bunkers/, Fairways/, Tees/, CartPaths_Spline/, Water/). For each GO, extract its mesh's contour polygon by reading the MeshFilter, projecting to XZ.
   - Use the existing `Course.SurfaceMarker.surfaceType` to get the zone type (these are present and valid post-Phase-B fix).
   - Write `Assets/Resources/HoleData/Hole_XX/zones.json`.
5. Run BakeZoneJsonTool on Hole_01. Verify `zones.json` exists, has plausible polygon counts (~1 Green, ~7 Bunkers, ~3 Fairways, ~4 Tees, ~10 CartPaths).
6. Write `Assets/Scripts/Gameplay/Tests/BakedClassifier_Hole01_Test.cs` (PlayMode):
   - Load `zones.json` for Hole_01.
   - Sample classification at 100 random XZ points across the hole bounds.
   - Compare with `SceneSurfaceProvider.Classify` at the same XZ points.
   - Assert agreement is >95% (some boundary disagreement is expected; gross disagreement is a bug).
7. **Regression tests still using SceneSurfaceProvider** — they should continue to fail the same way as M0. The new classifier isn't wired into sim yet.
8. `MILESTONE_1_DONE.md`. PASS if all tests pass and BakedZoneClassifier_Hole01_Test agreement >95%.

### Milestone 2 — `BakedHeightProvider` (the GroundProvider)

**Goal:** new ground provider reads `heightmap.bytes` + applies per-zone Y offsets from JSON.

Steps:
1. Write `Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs` implementing `IGroundProvider`. Constructor takes `HeightmapData` + `ZoneData` (or a `BakedZoneClassifier`). For `SampleHeight(x, z)`:
   - Get terrain Y from heightmap.
   - Classify zone at (x, z) via the classifier.
   - Return `terrainY + zoneOffset` for that zone type.
2. For `SampleHeight(x, z, preferred)` (3-arg) — same as 2-arg in this architecture, since baked data is authoritative; the `preferred` hint is only useful when scene raycast might disagree, which doesn't happen here. Document this clearly in code comments.
3. Unit tests `Assets/Scripts/Physics/Tests/BakedHeightProviderTests.cs`:
   - Heightmap returns 10.0 at XZ, no zones → `SampleHeight` returns 10.0.
   - Heightmap returns 10.0, Bunker zone with offset -1.3 at XZ → returns 8.7.
   - Heightmap returns 10.0, Green zone with offset 0.0 → returns 10.0.
   - Two overlapping zones (priority test) → returns the priority zone's offset applied.
4. Integration test `Assets/Scripts/Gameplay/Tests/BakedHeight_Hole01_Test.cs` (PlayMode):
   - Load Hole_01 heightmap + zones.json.
   - Sample baked height at 100 XZ points across the hole.
   - Compare with `SceneGroundProvider.SampleHeight(x, z)` (max-Y from scene).
   - Allow up to 5cm divergence (scene mesh tessellation + heightmap quantization differ); flag any >5cm divergence and dump those XZ points for review.
5. `MILESTONE_2_DONE.md`. Include the divergence histogram in the artifact list.

**Regression tests still failing on scene providers.** That's still expected.

### Milestone 3 — Switch sim to baked providers (the actual pivot)

**Goal:** `BallSimulation` uses `BakedZoneClassifier` and `BakedHeightProvider`. `SceneGroundProvider` and `SceneSurfaceProvider` become unused by sim. Regression tests now pass.

Steps:
1. Update `PhysicsLabController.BuildGroundProvider` and `BuildSurfaceProvider` (or wherever providers are constructed for sim). When a hole is loaded via `LabHoleBinder`, construct `BakedZoneClassifier` and `BakedHeightProvider` from the hole's `zones.json` + `heightmap.bytes`. Replace scene providers with baked.
2. Keep `SceneGroundProvider` and `SceneSurfaceProvider` in the codebase (Phase F deletes them; not yet). They become editor-only helpers for ball placement (ray-snap to visible mesh on the placement dropdown). Sim does not call them.
3. Update the regression tests to ALSO assert against the baked classifier — i.e., the test invariant becomes `ball.Y >= bakedClassifier.SampleHeight(ball.x, ball.z) - 0.05f`. This is the correctness condition.
4. Run regression tests. Expected: ALL 24 shots (8 directions × 3 tests) pass.
5. Run all existing physics tests (Phase 0–6). Expected: all pass. Bit-exactness for Phase 1–6 specifically is NOT required (heightmap-derived Y values may differ from scene-mesh Y values within tolerance), but no test should fail; if any does, surface to Architect.
6. **If regression tests pass but other tests fail**: this is the test-porting work. Document each failing test in MILESTONE_3_DONE.md "Notes for Architect" and STOP. Architect specs M3.5 (test fixes on the same branch).
7. **If regression tests still fail**: this is a baked-provider correctness bug. Dump per-step CSVs (reuse Phase A diagnostic infrastructure) for the failing shots, save to `Docs/DIAG/baked-pivot/M3-failing-shots/`, and STOP. Architect specs the fix.
8. `MILESTONE_3_DONE.md`. Status PASS only if regression tests pass AND all existing physics tests pass.

### Milestone 4 — Real-conditions test suite + cleanup

**Goal:** comprehensive automated test coverage; project state ready for Cesar's manual confirmation.

Steps:
1. Write `Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs` (the original Phase C suite from tactical spec, ported to baked architecture):
   - `Hole01_Bunker_AllDirections_NoFallThrough` — for each of Bunker_1..7, fire driver in 8 directions, invariant per-step.
   - `Hole01_Green_AllDirections_NoFallThrough` — Green_1 centroid + 8 directions × 3 clubs (putter, 7-iron, driver).
   - `Hole01_Fairway_50RandomShots` — 50 random fairway XZ, varying clubs.
   - `Hole01_Rough_50RandomShots` — 50 random rough XZ, varying clubs.
   - `AllImportedHoles_Smoke_3ShotsFromTee` — every hole with imported geometry.
2. Run Bake Zone JSON for ALL 18 holes (or however many have geometry).
3. Run the full test suite. Expected: 100% pass, zero fall-through frames.
4. Update `Docs/AI_CONTEXT.md`: physics architecture row reflects pivot complete.
5. Write `Docs/DIAG/baked-pivot/PHASE_E_READY.md` with manual test instructions for Cesar (see Rule 4).
6. `MILESTONE_4_DONE.md`. STOP. Wait for Cesar.

---

## Phase E — Cesar's manual confirmation

(Filled in by Code in `PHASE_E_READY.md`. Standard 5 manual shots: putt on green, short rough, chip from collar, fairway driver, **bunker driver in the direction Cesar originally saw fail**.)

If all 5 pass → Cesar runs `git checkout main && git merge sim-baked-data-path --no-ff` and the architectural pivot is complete.

If any fails → Cesar reports to Architect, who specs M5 (targeted fix). Branch stays open until M5 lands and Cesar re-confirms.

---

## What stays from yesterday's work

- B1/B2/B3 marker fixes: KEEP. They were correct work; the markers being clean prevents OTHER bugs even if sim doesn't read them. Course.SurfaceMarker components are still authoritative source for the BakeZoneJsonTool.
- Phase A diagnostic infrastructure (`DiagPerStepEnabled`, `DiagPerStepSink`, etc.): KEEP. Useful for any future debugging.
- The 3-arg `SampleHeight(x, z, preferred)` interface: KEEP. New providers implement it (with same semantics — return ground Y for the preferred zone if applicable; behaviorally simpler in baked architecture).

## What gets deleted

- `SceneGroundProvider` and `SceneSurfaceProvider`: NOT in this spec. They become editor-helpers for placement. Phase F (separate, future spec) deletes them once nothing references them.
- `Physics.Runtime.SurfaceMarker` MonoBehaviour: NOT in this spec. Stays in scenes for now (cosmetic). Phase F deletes it.
- `PhysicsMarkerRepairTool`: NOT in this spec. Becomes a one-time-use tool that's no longer needed; can be deleted in Phase F.

## DO NOT

- Do NOT skip the regression test in M0. It's the contract that proves the fix.
- Do NOT branch from anything other than current `main`. If main is dirty, Code stops and surfaces.
- Do NOT modify `BallSimulation`'s physics math (RK4, surface coefficients, putt classification). Only the providers change.
- Do NOT delete `SceneGroundProvider` or `SceneSurfaceProvider` in this spec. That's Phase F.
- Do NOT skip MILESTONE_N_DONE.md files. That's how Architect picks up state.
- Do NOT proceed to next milestone if current one is FAIL or BLOCKED.
- Do NOT bother Cesar between M0 and M4. Only Phase E requires Cesar.
- Do NOT speculate-fix during M0 prerequisite check.

## Files Code expected to touch

M0:
- New: `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs`
- New: `Docs/DIAG/baked-pivot/M0-*.md`

M1:
- New: `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs`
- New: `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs`
- New: `Assets/Scripts/Physics/Tests/BakedZoneClassifierTests.cs`
- New: `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs`
- New: `Assets/Scripts/Gameplay/Tests/BakedClassifier_Hole01_Test.cs`
- New: `Assets/Resources/HoleData/Hole_01/zones.json`

M2:
- New: `Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs`
- New: `Assets/Scripts/Physics/Tests/BakedHeightProviderTests.cs`
- New: `Assets/Scripts/Gameplay/Tests/BakedHeight_Hole01_Test.cs`

M3:
- Modified: `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (BuildGroundProvider/BuildSurfaceProvider)
- Modified: regression tests (assert against baked, not scene)

M4:
- New: `Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs`
- New: `Assets/Resources/HoleData/Hole_XX/zones.json` for all 18 holes
- Modified: `Docs/AI_CONTEXT.md`
- New: `Docs/DIAG/baked-pivot/PHASE_E_READY.md`
