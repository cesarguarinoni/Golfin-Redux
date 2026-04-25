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

## Phase B — TACTICAL FIX (Architect spec'd 2026-04-25 from Phase A data)

### Phase A summary (definitive findings)

- **A1:** HoleGeoImporter has 12 valid `AddComponent<SurfaceMarker>` call sites that work correctly. The gap is `CreateFlatContourMesh` (HoleGeoImporter.cs:4191) which only adds Course marker. `SyncPhysicsSurfaceMarkers` cannot CREATE markers, only update existing ones.
- **A2:** Hole_01 has 21 of 30 collider GOs with **zero valid Physics markers**, and 27 of 30 GOs have **broken/zombie components** (3 each, indicating the Roslyn migration ran 3 times). Critical zones with zero markers: Green_1, Fairway_1, all Bunkers 1–5 + 7 (only Bunker_6 OK), Tees 2–4, most CartPaths.
- **A4:** All 3 cold-load cycles bit-identical. PhysX is NOT non-deterministic. The bug is purely missing markers; the apparent non-determinism in Cesar's earlier playthroughs was shot-placement variance (ball happened to land on the 9 OK GOs vs the 21 broken ones).
- **Root cause:** Previous Roslyn migration script ran 3 times in Assembly-CSharp context, producing zombie components with wrong m_Script GUIDs instead of valid `Physics.Runtime.SurfaceMarker` components. The 9 GOs that DO have valid markers are the ones that escaped this flow (created via importer code paths that already had `AddComponent<SurfaceMarker>` working).

### Phase B scope (three independent fixes)

#### B1 — Cleanup tool for currently-broken scenes

New Editor tool: `Assets/Scripts/Editor/PhysicsMarkerRepairTool.cs`. Menu: **GOLFIN > Tools > Repair Physics Markers (Hole_01)** and **GOLFIN > Tools > Repair Physics Markers (All Holes)**.

For each loaded hole scene:
1. Walk every GO that has a `Course.SurfaceMarker` (use reflection per `SyncPhysicsSurfaceMarkers` pattern — the asmdef wall still applies).
2. **Remove all broken/missing-script components on that GO.** Use `SerializedObject` + `m_Component` array editing to remove components whose `m_Script` GUID is null. This is the canonical Unity API for this; do NOT use `Undo.DestroyObjectImmediate` on a missing-script component (it can fail silently).
3. **Check for an existing valid `Physics.Runtime.SurfaceMarker`** via `GetComponent<SurfaceMarker>()`. If absent, `AddComponent<SurfaceMarker>()` and set `.Type` from `MapCourseToPhysics()` (reuse the existing mapping logic from `SyncPhysicsSurfaceMarkers.cs`).
4. **If valid marker already exists, just update its `.Type`** (matches old SyncPhysicsSurfaceMarkers behavior).
5. `EditorUtility.SetDirty` per modified GO. `EditorSceneManager.MarkSceneDirty(scene)`. `EditorSceneManager.SaveScene(scene)`.
6. Log per-GO action (REMOVED N broken, ADDED marker / UPDATED marker / OK no change) to console + a summary `Docs/DIAG/realtest-20260425/B1-repair-Hole_XX.txt`.
7. Final summary line: "Repaired N GOs across M scenes. Removed K broken components. Added P new markers. Updated Q existing markers."

**Critical:** Use the editor's normal `gameObject.AddComponent<Golfin.Physics.Runtime.SurfaceMarker>()` from the Editor asmdef (`Golfin.Editor.asmdef` or Assembly-CSharp-Editor) with explicit `using Golfin.Physics.Runtime;`. **Do NOT use Roslyn `script-execute` via MCP for the AddComponent call.** That's what created the zombies last time.

**Verification step in the tool:** After save, re-run the audit logic (call into `MarkerAuditTool` programmatically). Print: "Post-repair Hole_XX: 30/30 GOs have ONE valid Physics marker, 0 broken components." If counts are wrong, log error and abort — do NOT pretend the fix worked.

**Run order:** Code runs B1 on Hole_01 first. Verifies post-repair audit is clean. Then runs on all 18 holes (most will be no-ops since they have no zone meshes per yesterday's findings).

#### B2 — Importer fix (prevent regression on future imports)

File: `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs:4191` (`CreateFlatContourMesh` function) and `:4196` (`CreateEarClipContourMesh` which delegates to it).

```csharp
// Current (line ~4191):
var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
marker.surfaceType = surfaceType;
return go;

// Fix: also add Physics.Runtime.SurfaceMarker with mapped Type.
var courseMarker = go.AddComponent<Golfin.Course.SurfaceMarker>();
courseMarker.surfaceType = surfaceType;
var physMarker = go.AddComponent<Golfin.Physics.Runtime.SurfaceMarker>();
physMarker.Type = MapCourseToPhysics(surfaceType);
return go;
```

`MapCourseToPhysics` should be a static method on `HoleGeoImporter` (or a shared helper in `Golfin.Editor`) with the same mapping `SyncPhysicsSurfaceMarkers.MapCourseToPhysics` uses. **Extract to a single source of truth** — do NOT duplicate the switch statement in two places. Code's call: put the shared mapper in either `HoleGeoImporter` and have SyncPhysicsSurfaceMarkers call it, OR put it in a new `Assets/Scripts/Editor/CourseImporter/SurfaceMarkerMap.cs` and have both call it. Code picks based on what's cleanest with the asmdef structure.

Also check `HoleLiteImporter.cs` (per A1, has 8 `AddComponent<SurfaceMarker>` call sites). If it has any analogous gap (only-Course-marker creation paths), fix those too. If not, no change.

#### B3 — Migration tool fix (`SyncPhysicsSurfaceMarkers.cs`)

The existing migration tool only updates existing markers. Replace its `SyncMarkersInScene` with logic that does the full repair (same as B1 essentially, but as a callable function instead of a menu item).

Actually: **make B1's PhysicsMarkerRepairTool the real implementation, and have SyncPhysicsSurfaceMarkers either delegate to it or be deleted.** No reason to maintain two near-identical tools. Code's call: delete `SyncPhysicsSurfaceMarkers.cs` and rename `PhysicsMarkerRepairTool` menu items to use the SyncPhysicsSurfaceMarkers menu paths for backward compatibility, OR keep both and have SyncPhysicsSurfaceMarkers call PhysicsMarkerRepairTool. Whatever produces less code.

### Phase B execution order

1. **Phase 0 restore point** (already done by Phase A — use the same `terrain-realtest-pre-fix` tag and `Docs/BACKUPS/terrain-realtest-20260425/` folder. Add new files to backup if needed before editing).
2. Per-attempt commit discipline still applies: commit each of B1, B2, B3 separately (`realtest-attempt-B1: …`, `realtest-attempt-B2: …`, `realtest-attempt-B3: …`) for individual revertability.
3. **B1 first.** Build the repair tool. Run on Hole_01. Verify post-repair audit shows 30/30 valid markers, 0 broken. Save scene.
4. **Manual smoke test before B2/B3.** After B1, fire 1 putt + 1 bunker shot manually using the same diagnostic test infrastructure from Phase A (or just play in PhysicsLab). If they fall through, B1 isn't sufficient and we need to investigate further before continuing. Log result to `Docs/DIAG/realtest-20260425/B1-smoke-test.md`.
5. **B2 next.** Fix HoleGeoImporter. Test by re-importing one hole (Code's choice — a small one, or Hole_01 again) and running the audit on the freshly-imported scene. Verify 30/30 valid markers post-import, 0 broken.
6. **B3 last.** Consolidate migration tooling. Verify the menu items still work end-to-end on Hole_01.
7. **Run B1 on all 18 holes** (final pass, in case B2 surfaces any issues with already-imported scenes that need re-repair).

### Iteration budget

- B1: 3 attempts. The hard part is the missing-script removal API; if `SerializedObject` editing of `m_Component` array doesn't work, fallback options are: (a) `Undo.DestroyObjectImmediate` per component (may fail), (b) GameObject removal + recreation (last resort, breaks references). Document each attempt.
- B2: 2 attempts. Straightforward, just don't break the existing 12 working call sites.
- B3: 1 attempt. Mostly delete + rename.

If B1 exhausts its 3 attempts without producing a clean Hole_01, **STOP and surface to Architect** — this triggers architectural-pivot evaluation (matches activation trigger #1 in the queued spec).

### Phase B done criteria (Code reports done when all of these are true)

1. B1 repair tool exists and runs cleanly on Hole_01. Post-repair audit: 30/30 valid markers, 0 broken.
2. B1 smoke test (1 putt + 1 bunker shot) shows no fall-through.
3. B2 importer fix is in. Re-importing a hole produces 30/30 valid markers post-import.
4. B3 tooling consolidation done.
5. B1 final pass run on all 18 holes; per-hole repair counts logged.
6. All commits made: `realtest-attempt-B1`, `realtest-attempt-B2`, `realtest-attempt-B3`, plus a final `realtest-attempt-B-final` summary commit.
7. Done report appended to TellCode.md with: every commit hash, audit before/after numbers per hole, smoke test result, files modified.

Then Code proceeds to Phase C (real-conditions test suite) and reports done again when Phase C passes.

### Phase B DO NOT

- Do NOT use Roslyn `script-execute` via MCP for any `AddComponent` call. Use proper Editor scripts only.
- Do NOT change `SceneGroundProvider`, `SceneSurfaceProvider`, or `BallSimulation`. They're not the bug. (The 3-arg `SampleHeight` from yesterday stays.)
- Do NOT skip the B1 smoke test. Phase A proved we don't trust "audit numbers look good" alone — we trust real shots.
- Do NOT activate the architectural spec yet. Tactical fix gets a fair shot.
- Do NOT retire the Phase A diagnostic infrastructure. Per-step CSV logging in `BallSimulation` stays in (behind `#if UNITY_EDITOR` + flag) for future debugging.

---

## Phase B' — High-velocity LAUNCH from depressed surface (Architect spec'd 2026-04-25 from B1 smoke test)

### What B1 smoke test revealed (CORRECTED)

- Putt FROM green: **PASS** (slow roll, ball stays on green)
- Wedge FROM bunker: **PASS** (slow launch, ball clears bunker cleanly)
- Driver FROM green: **FAIL sometimes** (depends on aim — fails more often toward green edge, less often toward center)
- Driver FROM bunker: **FAIL always** (Bunker_1, every time)

Markers are correctly populated (B1 fix worked for that). The remaining failure mode is **high-velocity launches FROM a depressed surface (green or bunker)**, NOT airborne landings. The failure is at the launch moment when ball XZ leaves the depressed-zone polygon within 1–2 sim frames.

### Root-cause hypothesis (to be confirmed by B'1 diagnostic)

Ball sits at depressed surface Y (green or bunker, both below surrounding terrain). Driver imparts ~70 m/s velocity. At 1/240s tick, that's ~30cm horizontal travel per frame — enough to exit a 7m bunker in 1–2 frames. After exit, `surfaces.Classify(x, z)` flips from Sand/Green to Fairway/Rough. `SampleHeight(x, z, Fairway)` returns surrounding terrain Y, which is HIGHER than ball's current Y. Ball is now below ground per the new classification. Sim either snaps ball UP onto terrain (visual pop) or fails to detect ground at all (fall-through), depending on which sim path runs.

Why bunker always fails: bunkers small (~7m). Driver exits in 1–2 frames every time.
Why green sometimes fails: greens larger (~20–30m). Aim toward edge → exit in few frames. Aim toward center → ball stays on green long enough for sim to handle the depression-edge transition correctly.

### Phase B' is diagnostic-first (do NOT speculate-fix)

We have ample evidence that speculative fixes blow up in our faces. Run the diagnostic FIRST, then Architect writes the actual fix from the data.

#### B'1 — Reproduce with per-step diagnostic logger ON

The per-step CSV infrastructure from Phase A still exists in `BallSimulation`. Reuse it.

Write a new PlayMode test `Assets/Scripts/Gameplay/Tests/HighVelocityLaunchDiagTests.cs`:

1. Additively load Hole_01_Geo via `LabHoleBinder` (same path PhysicsLab uses). Assert `_useSceneProviders == true` after binding.
2. Set `BallSimulation.DiagPerStepEnabled = true`.
3. Fire 6 scripted shots — all start AT the depressed surface, varying club + aim:
   - **Shot 1: driver from Bunker_1 centroid** aimed straight out (any cardinal direction toward fairway). Captures the always-failing case.
   - **Shot 2: driver from Bunker_1 centroid** aimed toward bunker edge. Should fail same way; control for aim direction.
   - **Shot 3: driver from Green_1 centroid** aimed toward green edge. Should sometimes fail.
   - **Shot 4: driver from Green_1 centroid** aimed toward green center / opposite edge. Should fail less often.
   - **Shot 5: control — wedge from Bunker_1 centroid** any aim. Should pass (matches B1 smoke).
   - **Shot 6: control — putter from Green_1 centroid** any aim. Should pass.
4. Save to `Docs/DIAG/realtest-20260425/Bprime-shot-{1..6}.csv` with raycast hit lists.
5. Set `BallSimulation.DiagPerStepEnabled = false`. Test passes if it ran all 6 shots without exceptions. Do NOT assert pass/fail of fall-through — that's for Architect to inspect.

Also save `Docs/DIAG/realtest-20260425/Bprime-summary.md` with per-shot, focusing on the **first 30 sim frames** (the launch moment, NOT the landing):
- Frame 0: ball position, velocity (full vector), surface classification, ground Y (2-arg), ground Y (3-arg).
- Frames 1–10: same data per frame. Particularly note the frame at which surface classification CHANGES (e.g. Sand → Fairway when ball XZ leaves bunker polygon) and what happens to ball Y vs ground Y at that frame.
- Frame at which airborne phase begins (if applicable — driver launch should be airborne almost immediately).
- Whether `[Terrain]` debug assertion fires at any step (and at which step).
- Final outcome: did the ball settle normally, fall through (ballY went very negative), or other anomaly?

**STOP after B'1. Do not start B'2. Wait for Architect.**

#### B'2 — Fix (Architect spec'd 2026-04-25 from B'1 data — PENDING ONE CESAR QUESTION)

**B'1 finding summary:**
- Shot 2 (driver +Z from Bunker_1) fell to Y=-2301 over 60 seconds. termination=`MaxDurationReached`, `diagFrames=0`, ball never entered roll/putt phase.
- `SimulateAirborne`'s `HitGround` condition (`posNext.y <= groundY && pos.y > groundY`) never fired.
- `SceneGroundProvider.SampleHeight` returns `fp.Zero` when `RaycastAll` finds zero hits — the sentinel-ambiguity bug. `Y=0` is also a legitimate ground value, so the airborne integrator can't distinguish "no terrain" from "terrain at Y=0".
- `WorldBound = 2000` safety check covers X and Z but NOT Y. Ball Y can drop arbitrarily far without termination.
- Direction-specific: same shot in +X direction terminated normally as `HitOOB`. Failure happens when trajectory exits the heightmap collider's XZ bounds.

**Open question (must answer before fix):** Does Cesar's manual repro show ball visually flying off into the distance, or does it visually fall through the green/bunker right where it was launched? B'1's confirmed failure is the former (ball flies +Z into untextured space and free-falls). Cesar's described failure ("ball falls through") sounds like the latter. They might be different bugs, or the same bug seen from different angles.

Fix plan, in two stages:

##### B'2a — Add airborne per-step diagnostic logging (small, do first)

Reuse `BallSimulation.DiagPerStepSink`. In `SimulateAirborne`'s integration loop, after `groundY = ground.SampleHeight(posNext.x, posNext.z)` is computed but before the HitGround check, emit one CSV row when `DiagPerStepEnabled` is true:

```csharp
#if UNITY_EDITOR
if (DiagPerStepEnabled && DiagPerStepSink != null)
{
    DiagStepFrame++;
    DiagPerStepSink($"{DiagStepFrame},air,{posNext.x.ToFloat():F4},{posNext.y.ToFloat():F4},{posNext.z.ToFloat():F4},{groundY.ToFloat():F4},{velNext.y.ToFloat():F4}");
}
#endif
```

Format: `frame,phase,x,y,z,groundY,velY`. Six fields. Same DiagStepFrame counter as roll/putt so the sequence is contiguous.

Re-run `HighVelocityLaunchDiagTests` Shot 2 only (or write `AirborneDiagShot.cs` as a tiny new test). Save full per-step CSV to `Docs/DIAG/realtest-20260425/Bprime-air-shot-2.csv`. Generate a one-page summary `Bprime-air-summary.md` with:
- Frame at which `groundY` first becomes 0 (ball exited terrain bounds).
- Frame at which ball Y crosses 0 going down (potential HitGround trigger frame).
- At that frame: what is `posNext.y` exactly? What is `pos.y` exactly? What is `groundY`? What does the HitGround condition evaluate to?
- Frame at which ball.x or ball.z exceed 2000 (WorldBound trigger that didn't fire).
- Final frame at which the integrator stopped (max-steps, max-duration, or break from a check).

This gives us the exact mechanism. Stop after B'2a. Architect re-reads spec and finalizes B'2b.

##### B'2b — Fix (Architect spec'd from B'2a data)

Fix candidates ranked by likelihood from B'1 evidence:

1. **Sentinel return from SceneGroundProvider.** Change `SampleHeight` (2-arg) to return `fp.FromFloat(-1e6f)` or a documented `NoGround` constant when zero hits. `SimulateAirborne` checks for the sentinel before the HitGround check. If sentinel: treat as OOB (ball is over the void). If normal value: existing HitGround check applies.
2. **Y-axis safety bound.** In `SimulateAirborne`, after computing `posNext`, if `posNext.y < (origin.y - 100)`, force `termination = ExitedWorldBounds` (or new `BallLost` reason) and break. This catches any future regression where the ball escapes ground detection.
3. **Fix HitGround edge case (low priority — only if B'2a shows it).** If the issue is the condition fires but then bounces into another HitGround that fails, the bounce loop in `Simulate` needs hardening. But B'1 said `diagFrames=0`, meaning roll/putt never entered, meaning no bounce loop ran. So this is unlikely.

Likely fix is **#1 + #2 together** — sentinel for clarity, Y-bound as belt-and-suspenders. Architect commits to specifics after B'2a CSV.

**B'2b will require its own smoke test before being declared done.** Same shape as B1: Cesar fires the same set (driver from green, driver from bunker, both directions) and confirms no fall-throughs. Only THEN proceed to Phase C.

### Phase B' DO NOT

- Do NOT touch HoleGeoImporter — B2 already fixed importer markers, that's done.
- Do NOT touch the marker repair tool — B1 already worked correctly for marker placement.
- Do NOT speculate-fix between B'1 and Architect's B'2 spec. The point of B'1 is to get data we don't have yet. Acting on a guess wastes another cycle.
- Do NOT skip the control shots (Shot 3, Shot 4). The diff between failing high-velocity and passing low-velocity shots is the actual diagnostic signal.
- Do NOT modify the Phase A diagnostic infrastructure. Reuse as-is.

### Phase B' iteration budget

- B'1: 1 attempt. Reproduces the failure with logging on. If repro fails (driver-into-green doesn't fall through in the test), STOP and surface — that's a finding too (suggests Cesar's manual repro had specific conditions we need to match).
- B'2: TBD by Architect.

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
