# SPEC — Real-conditions terrain fall-through fix

**Date:** 2026-04-25 (updated mid-session)
**Status:** Active — handoff to Claude Code
**Pointer in:** `Docs/TellCode.md`
**Supersedes:** the synthetic-test "Bulletproof terrain" task (yesterday's done report)

## ⚠️ IMPORTANT: read this section before doing anything

**This spec was updated mid-session because Code went off-script.** Before this spec was finalized, Code:
- Skipped Phase A1 (importer investigation).
- Did a partial A2-equivalent (counted Course vs Physics markers in Hole_01: 9 phys vs 30 course).
- Skipped A3 entirely (no per-step CSV diagnostic).
- Made a speculative fix (re-ran the migration tool to bring count to 30/30, scene saved).
- Diagnosed the root cause as "first SaveScene silently failed mid-run."
- Did not fire any verification shot before reporting done.

Cesar then tested manually:
- **First playthrough after Code's fix: clean.**
- **Second playthrough: balls fell through, BOTH starting on green AND in bunker.**

**This means the bug is non-deterministic across loads.** Same scene, same code, same data — sometimes works, sometimes doesn't. Code's data-only fix didn't actually solve it; it might have helped on the margin but the underlying problem is something that varies between scene loads.

**Cesar wants the FULL Phase A diagnostic pass anyway, even though we're likely going to pivot to the architectural fix afterward.** The diagnostics are valuable as a learning artifact about what we got wrong with the current architecture. **Do NOT skip phases. Do NOT speculate-fix during Phase A. Stop and wait for Architect after Phase A is complete.**

This is now the SECOND time in two days Code has acted faster than the spec wanted (yesterday: shipped synthetic tests instead of real-scene tests; today: ran a fix without diagnostics first). **Investigate before fixing. Read the spec end-to-end before starting. When the spec says "stop and wait," stop and wait.**

## Why we're back

Yesterday's fix shipped 111/111 tests green and a 3500-shot stress run with zero fall-throughs. Cesar then loaded Hole_01_Geo in PhysicsLab, fired one shot to the rough and one putt on the green. **Both fell through the terrain.**

Root cause of the test failure: every test used synthetic BoxCollider geometry. None of them additively loaded a real hole scene. The "111/111" was theatre — it proved the type-preference logic in isolation, not that it works in the real scene with real markers, real hierarchy, and the live importer's output.

Compounding evidence: Cesar found that a Tee GO in Hole_01 has THREE `Surface Marker` components — two valid (Course + Physics) and one **broken script reference** literally labelled `Golfin.Physics.Runtime::Golfin.Physics.Runtime.SurfaceMarker` (malformed double-colon namespace string). Some code path in the importer is producing zombie marker components. The scene was NOT re-imported between yesterday's tests and today's failed shots, so the broken markers + missing markers situation is exactly what was in the scene when sim was run.

This spec discards the synthetic-test approach entirely. **Every test must load a real `.unity` scene file additively. No exceptions.**

## Architectural context — read before starting

This is the **tactical fix.** A queued architectural spec at `Docs/Specs/Queued/SIM_BAKED_DATA_PATH.md` will move the sim off scene-coupled providers entirely (read baked zone JSON + heightmap, sim doesn't touch the scene). That work is queued, not in scope here. Do NOT pre-emptively start the architectural change as part of this fix. Do the tactical fix; unblock physics work; the architectural pass happens in its own spec.

## Decisions (authoritative)

1. **HoleGeoImporter is in scope for editing this round.** Previous DO-NOT-TOUCH rule is lifted for this task. The bug almost certainly lives there.
2. **Phase A is read-only diagnostics.** No production code changes. Code stops after Phase A and waits for Architect to write Phase B based on the data.
3. **Phase B and onward are autonomous.** Cesar is at keyboard for Phase A handoff but away after that.
4. **No synthetic geometry in tests.** Every test must additively load a real `.unity` hole scene. Anything using `GameObject.CreatePrimitive`, `new GameObject` for collider, or fake markers is forbidden in this task's test suites.
5. **Cesar's manual confirmation is the final gate.** Tests passing is necessary but not sufficient.

---

## Phase 0 — RESTORE POINT (mandatory first step)

1. `git status` → clean tree. If dirty, `git stash push -u -m "pre-realtest-stash"` and record stash ref.
2. `git tag terrain-realtest-pre-fix` on HEAD. Push tag: `git push origin terrain-realtest-pre-fix`.
3. Create `Docs/BACKUPS/terrain-realtest-YYYYMMDD/`. Copy any file BEFORE editing it. Minimum list grows as Phase B determines:
   - `Assets/Scripts/Physics/Core/BallSimulation.cs`
   - `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs`
   - `Assets/Scripts/Physics/Runtime/SceneSurfaceProvider.cs`
   - `Assets/Scripts/Editor/SyncPhysicsSurfaceMarkers.cs`
   - HoleGeoImporter (path TBD — Code locates in A1)
4. Per-attempt commits: `realtest-attempt-N: <summary>`. Never squash. Every attempt must be revertable.
5. Done report includes tag, stash ref, backup folder, full commit hash list.

Rollback: `git reset --hard terrain-realtest-pre-fix`.

---

## Phase A — DIAGNOSTICS (no fixes — stop and wait when done)

Three parallel investigations. All findings into `Docs/DIAG/realtest-YYYYMMDD/`. After Phase A, **Code commits and stops.** Architect reviews data and writes Phase B.

### A1 — Find the broken marker source

Locate `HoleGeoImporter.cs` in the Unity project (Code: search `Assets/Scripts/` and `Assets/Editor/` for the file by name).

Find every place that adds a `Physics.Runtime.SurfaceMarker`. Search patterns: `AddComponent<SurfaceMarker>`, `AddComponent(typeof(SurfaceMarker))`, `MonoScript.FromMonoBehaviour`, any direct `MonoScript`/`MonoImporter` manipulation, any string containing `Golfin.Physics.Runtime`.

Identify how the broken `Golfin.Physics.Runtime::Golfin.Physics.Runtime.SurfaceMarker` script reference (malformed double-colon namespace) is being written. Hypothesis: hardcoded type string somewhere, or `MonoScript.GetClass()` lookup with wrong asmdef context.

Write findings to `Docs/DIAG/realtest-YYYYMMDD/A1-broken-marker-source.md`:
- File path + line number of each `AddComponent<SurfaceMarker>` call site.
- File path + line number of each occurrence of the literal string `"Golfin.Physics.Runtime"` in the importer.
- Best guess at what produces the malformed double-colon string.

**Do NOT fix yet.** Document only.

### A2 — Loaded-scene marker audit

**Note:** Code already did a partial pass during the off-script work. Result claimed: "30/30 markers post-migration." Re-run anyway to confirm the post-migration state PERSISTED across editor restart.

1. **Close Unity completely. Reopen Unity.** Then load `Hole_01_Geo.unity` additively via `EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)`. The restart catches any "saved-but-not-actually-on-disk" state that the previous migration may have produced.
2. Walk every GO under the hole's root. For each GO that has any `MeshCollider` or `BoxCollider`:
   1. List ALL components on that GO and on every parent up to scene root.
   2. For each component whose name contains "Surface" or whose script reference is missing: log GO path, component type as Unity sees it (use `serializedObject.FindProperty("m_Script").objectReferenceValue == null` to detect missing scripts), `.Type` value if accessible, and parent chain.
3. Specifically count and log:
   - GOs with ZERO valid `Physics.Runtime.SurfaceMarker` in their parent chain
   - GOs with ONE valid Physics marker
   - GOs with MULTIPLE valid Physics markers (duplicate-add bug)
   - GOs with at least one broken/missing-script `SurfaceMarker` component
   - GOs with a Physics marker on a PARENT vs DIRECTLY on the GO carrying the collider (the green-child-collider problem from yesterday's T11)

Save full audit to `Docs/DIAG/realtest-YYYYMMDD/A2-Hole01-marker-audit.txt`. Save summary table to top of file.

**Compare with Code's earlier finding (9 vs 30) and note any divergence in the summary.** If post-restart count is back to 9, then `EditorSceneManager.SaveScene` did silently fail on Code's fix and we have evidence of that. If still 30, then Code's fix persisted to disk — but the bug recurs anyway, meaning data isn't the only problem.

### A3 — Runtime per-step diagnostic on real shots

Goal: capture exactly what `BallSimulation` sees during the failing shots, in the live scene.

**Diagnostic instrumentation** (temporary, behind `#if UNITY_EDITOR` + `BallSimulation.DiagPerStepEnabled` static bool, default false):
- In `RunRollPhase`, `RunPuttPhase`, and the `Simulate` putt-start block: after `surface = surfaces.Classify(...)`, log a CSV row to `BallSimulation.DiagPerStepSink` (static `Action<string>`):
  - `frame, phase, surface, ballX, ballY, ballZ, groundY_2arg, groundY_3arg, preferredFound, nHits, hitColliders[]` where `hitColliders` is a semicolon-joined list of `colliderName|markerType` for every raycast hit.
- Add SAME logging at the airborne→roll handoff point (right before calling `RunRollPhase`) so we capture what the airborne integrator handed over.
- Wire `BallSimulation.DiagPerStepSink` from `PhysicsLabController.Start()` → writes to `Docs/DIAG/realtest-YYYYMMDD/A3-shot-N.csv` (Code picks N per shot).

**Real-scene PlayMode test** `RealHole_DiagShots_Hole01` (PlayMode `[UnityTest]` in `Assets/Scripts/Gameplay/Tests/`):

1. `EditorSceneManager.LoadSceneAsync("Hole_01_Geo", LoadSceneMode.Additive)` — same path PhysicsLab uses. Wait for load complete.
2. Get reference to the active `PhysicsLabController` in the scene. **Assert `_useSceneProviders == true`** after binding. If false, log finding to A3 output and abort the rest of the test (but don't fail — that's a finding, not a test bug).
3. Set `BallSimulation.DiagPerStepEnabled = true`.
4. Fire 4 scripted shots, recording one CSV per shot:
   - **Shot 1: rough → rough.** Place ball at a known rough XZ (find an XZ where `SceneSurfaceProvider.Classify` returns Rough or Semirough). Fire a low-power 7-iron in any direction with ~50% power. Record full trajectory.
   - **Shot 2: putt on green.** Place ball at Green_1 centroid. Fire putter at 60% power, any direction. Record full trajectory.
   - **Shot 3: approach to green.** Place ball at fairway 80yd from pin. Fire wedge at 80% power aimed at Green_1. Record full trajectory.
   - **Shot 4: fairway full shot.** Place ball at fairway tee-side. Fire driver at 90%. Record full trajectory.
5. After all 4 shots, `BallSimulation.DiagPerStepEnabled = false`. Test passes if it ran all 4 shots without exceptions. Pass/fail of fall-through is for Cesar to inspect via the CSVs.

Save CSVs to `Docs/DIAG/realtest-YYYYMMDD/A3-shot-{1..4}.csv`. Format header row.

Also save `Docs/DIAG/realtest-YYYYMMDD/A3-summary.md` with:
- Whether `_useSceneProviders` was true.
- Per-shot: total frames, max `groundY_2arg - ballY` violation (if any), max `groundY_3arg - ballY` violation (if any), surface classification breakdown.

### A4 — Load-determinism test (NEW — added because manual tests show non-deterministic fall-through)

**This is the highest-information test we can run.** Cesar's two playthroughs in the same Unity session showed first run clean, second run fell through. That points at PhysX raycast non-determinism, component iteration order non-determinism, or `_useSceneProviders` race condition. A4 settles which.

**Process:**

1. **Define a fixed shot sequence.** Save to `Docs/DIAG/realtest-YYYYMMDD/A4-shot-coords.json`:
   - Shot 1: putt from Green_1 centroid (whatever fixed XZ Code computes once, then reuses).
   - Shot 2: chip from rough at a fixed XZ near Green_1.
   - Shot 3: bunker shot from Bunker_1 centroid.
   - Shot 4: fairway approach from a fixed fairway XZ ~80yd from pin.
   - Shot 5: tee shot with driver from tee marker.

   For each shot, record fixed `(originX, originZ, club, power, aimYaw)`. Saved JSON is the contract; all cycles must use these exact values.

2. **Cycle 1: Editor restart.** Close Unity completely. Wait 5 seconds. Reopen Unity. Load Hole_01 via picker. Run the 5-shot sequence. For each shot, record:
   - Full per-step CSV (same format as A3).
   - **Per-step raycast hit lists**: at every sim step, the full unsorted list of `(colliderInstanceID, colliderName, hitY, markerType, markerInstanceID)` returned by `RaycastAll` at the ball's XZ. This captures both PhysX's hit ordering AND component identity.
   - Save to `Docs/DIAG/realtest-YYYYMMDD/A4-cycle-1-shot-N.csv` and `A4-cycle-1-shot-N-hits.csv`.

3. **Cycle 2: Editor restart again.** Close Unity completely. Wait 5 seconds. Reopen. Repeat the 5-shot sequence with identical inputs. Save to `A4-cycle-2-*`.

4. **Cycle 3: One more.** Save to `A4-cycle-3-*`.

5. **Diff helper.** Code writes a small C# editor script (`Assets/Scripts/Editor/A4DiffHelper.cs`) that reads all 3 cycles' CSVs and produces `Docs/DIAG/realtest-YYYYMMDD/A4-diff-summary.md`:
   - For each shot, do all 3 cycles produce bit-identical ball trajectories (same ball Y at every step within fp precision)?
   - If not, where do they first diverge? Step number, ballY values, surface classifications.
   - Compare raycast hit lists at the divergence step — are they identical? Same colliders, same Y values, same order? Or different?
   - Did any cycle produce a fall-through (ballY drops below groundY by >0.05m at any step) that other cycles did not?

**Verdict matrix (Code includes in `A4-diff-summary.md`):**

| What A4 shows | Verdict | Recommended path |
|---|---|---|
| All 3 cycles bit-identical, no fall-through | Bug is deterministic, Code's fix held; original failures were user-side variance | Tactical fix viable. Continue Phase B. |
| Cycles diverge at raycast hit list level (different colliders / different Y / different order across cycles) | PhysX `RaycastAll` is non-deterministic across cold scene loads | **Pivot to architectural fix** (matches activation trigger #3 in `Docs/Specs/Queued/SIM_BAKED_DATA_PATH.md`) |
| Raycast hits identical across cycles, but `GetComponentInParent` returns different markers across cycles | Component iteration order non-deterministic with duplicate markers | Tactical fix possible by removing duplicates (→ A1 result becomes critical for understanding why importer produces them) |
| `_useSceneProviders` is `false` in some cycles, `true` in others | Timing race in PhysicsLab `OnHoleLoaded` wiring | Tactical fix possible (await the wiring before sim accepts shots) |
| Fall-through occurs in 1-2 of 3 cycles but not all | Ambiguous — still non-deterministic, leans architectural | Architect reviews CSVs and decides |
| All cycles show fall-through consistently | Different bug than what Cesar manually saw — dig deeper | Architect reviews and re-scopes |

**A4 is the deciding test.** Architect uses its verdict (combined with A1+A2+A3) to write Phase B (if tactical) or to activate the queued architectural spec (if non-deterministic).

### Phase A done report (Code writes after A1+A2+A3+A4 complete)

Single file: `Docs/DIAG/realtest-YYYYMMDD/PHASE_A_DONE.md` with:
- Restore point info.
- Pointers to A1, A2, A3, A4 outputs.
- Code's BEST GUESS at the root cause based on the data, but no fix recommendations — that's Architect's job.
- **A4 verdict explicit**, using the matrix above. One line, no hedging: "Across 3 cold-load cycles of 5 shots each, [outcome]. Recommended path: [tactical | architectural pivot]."
- Commit hash for the Phase A commit.

**STOP HERE. Do not start Phase B. Wait for Architect.**

**Repeat: Phase A is read-only diagnostics.** Do NOT speculate-fix. Do NOT re-run the migration tool. Do NOT edit HoleGeoImporter. Do NOT "clean up" the broken script refs. Do NOT remove duplicate markers. The goal is to capture what is currently broken, in the current state of the project, with maximum diagnostic detail. Modifying anything during Phase A poisons the data.

If you find yourself thinking "this would be quick to fix while I'm here" — stop. Write it down in the done report under "observations Architect should consider for Phase B," then move on.

---

## Phase B — TACTICAL FIX (Architect spec'd after Phase A)

Cannot pre-spec without diagnostic data. The fix likely involves one or more of:
- Editing HoleGeoImporter to stop producing broken script refs.
- Re-running the migration tool (or replacing it) to populate Physics markers correctly across all imported holes.
- Possibly fixing `SceneGroundProvider.SampleHeight(3-arg)` if `GetComponentInParent` traversal is wrong for the actual hierarchy.
- Possibly fixing the airborne→roll handoff if Phase A reveals fall-through happens at that exact transition.

When Architect writes Phase B, it will appear as an updated section in this spec file (search for "## Phase B —"). Code reads the updated spec and proceeds.

---

## Phase C — REAL-CONDITIONS TEST SUITE (Code writes after Phase B fix is in)

**Hard requirements for every test in this suite:**
- Must additively load a real `.unity` hole scene file from disk.
- Must bind via `LabHoleBinder` (same code path PhysicsLab picker uses).
- Must assert `_useSceneProviders == true` after bind.
- Must use the production shot pipeline (`ShotController.ApplyShot` or whatever PhysicsLab uses for live shots) — not direct `BallSimulation.Simulate` calls.
- Must sample trajectory frame-by-frame via the same simulation result the production game uses.
- Failure must dump trajectory CSV + capture `screenshot-game-view`.

**Tests** (PlayMode, in `Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs`):

1. `Hole01_RealRough_50Shots` — load Hole_01_Geo, find 50 random XZ points where `SceneSurfaceProvider.Classify` returns Rough/Semirough, fire wedge shots from each, frame invariant: `ball.Y >= ground.SampleHeight(ball.x, ball.z, surface) - 0.005f`.
2. `Hole01_RealGreen_100Putts` — load Hole_01_Geo, find Green_1 collider bounds, sample 100 random XZ inside it, fire putts at varying yaw and 30–95% power.
3. `Hole01_RealGreen_50Approach` — 50 wedge shots from fairway aimed at Green_1, invariant from first green-frame onward.
4. `Hole01_RealBunker_30Shots` — for each Sand-marked bunker in Hole_01, place ball, fire wedge shots, same invariant.
5. `Hole01_RealFairway_50Shots` — 50 shots from real fairway XZ, same invariant.
6. `AllImportedHoles_Smoke` — for each hole that has imported geometry (detect by presence of zone meshes), 5 shots from tee. Auto-skip holes without imported geometry.

**Pass criteria:** 100% of tests pass with zero fall-through frames across all trajectories.

If any test fails: trajectory CSV + screenshot saved automatically. Code retries the fix (within Phase B's iteration budget).

---

## Phase D — Cesar's manual confirmation (mandatory final gate)

After Phase C is fully green, Code reports done. Cesar then opens LabScaffold, loads Hole_01_Geo via picker, and fires 5 manual shots:
1. Putt on green.
2. Short rough shot.
3. Chip from collar.
4. Fairway full shot.
5. Bunker shot.

All 5 must visibly stay on surface, no fall-throughs. If any fails, Code's test suite is still inadequate — the failure mode wasn't covered by Phase C tests. In that case Code adds a new test that reproduces the failure, then iterates Phase B+C until the new test passes too. Repeat until Cesar's manual pass is clean.

---

## Iteration budget

- Phase A: one shot. Diagnostics either get the data or don't. If A3 hits a Unity API issue (e.g. additive scene load fails in PlayMode test), Code documents the blocker and stops.
- Phase B: 5 fix attempts max (after Architect spec). Same per-attempt commit discipline as before.
- Phase C: 3 iterations on test authoring (real-scene tests are trickier than synthetic; expect setup helper churn).
- Phase D: as many cycles as needed until Cesar confirms.

---

## DO NOT

- Do NOT skip Phase A. Diagnostics first.
- Do NOT proceed to Phase B without Architect's updated spec.
- Do NOT write any synthetic-geometry tests in Phase C. Anything using `CreatePrimitive` or `new GameObject` for a collider is forbidden in this suite.
- Do NOT report "X/Y tests pass" as evidence of correctness. Only Cesar's Phase D sign-off counts.
- Do NOT preemptively start the architectural change (queued spec at `Docs/Specs/Queued/SIM_BAKED_DATA_PATH.md`). Tactical fix only this round.
- Do NOT touch `BallSimulation.SimulateAirborne`'s max-Y use unless Phase A specifically points to it as the bug.
- Do NOT re-bake heightmaps.
- Do NOT skip Phase 0 restore point.
- Do NOT squash per-attempt commits.

---

## Files Code expected to touch

Phase A (diagnostics):
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — add `DiagPerStepEnabled`, `DiagPerStepSink`, per-step CSV emission (all `#if UNITY_EDITOR`)
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — wire `DiagPerStepSink` to file
- New: `Assets/Scripts/Editor/MarkerAuditTool.cs` — runs A2 audit on demand
- New: `Assets/Scripts/Gameplay/Tests/RealHoleDiagShotsTests.cs` — A3 PlayMode test

Phase B: TBD by Architect after Phase A.

Phase C:
- New: `Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs` — full test suite
- Possibly: helper class for additive-load + LabHoleBinder setup

## Phase A done report requirements

- Restore point: tag, stash, backup folder, commit hashes.
- A1: file path, line numbers, root cause hypothesis for broken marker.
- A2: marker audit summary table.
- A3: per-shot CSV pointers + summary table + `_useSceneProviders` value at runtime.
- Code's best-guess root cause (one paragraph).
- Single commit hash for Phase A work.
