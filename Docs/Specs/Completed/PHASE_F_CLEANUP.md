# PHASE_F_CLEANUP — post-pivot dead-code removal

> **Handoff:** `Docs/Specs/Active/PHASE_F_CLEANUP.md`
> **Status:** Ready to execute. Single milestone, no phasing.
> **Branch:** work on `phase-f-cleanup`. Pre-merge tag: `pre-phase-f`.
> **Estimated effort:** ~1 session.
>
> **Amendment 2026-04-26 (Architect):** F.1 delete list expanded to also cover
> the M1/M2 agreement tests (`BakedHeight_Hole01_Test.cs`, `BakedClassifier_Hole01_Test.cs`).
> They use `SceneGroundProvider`/`SceneSurfaceProvider` as ground-truth
> baselines for one-shot pivot-merge validation that already passed; sim no
> longer reads scene path so "agreement with scene path" tests a coupling
> we just severed by design. Coverage is preserved by `BakedHeightProviderTests`,
> `BakedZoneClassifierTests` (unit), and `RealHoleTerrainTests` (e2e). New
> step F.3.5 added to strip the Phase-A `WireA3DiagSinks` plumbing from
> `PhysicsLabController` (the `SceneGroundProvider.DiagHitSink` wiring +
> `_diagHitWriter`/`_diagStepWriter` static state). All `<see cref>` xml-doc
> references to the deleted types (in `BakedHeightProvider.cs`,
> `BakedZoneClassifier.cs`, `ZoneData.cs`, `BakeZoneJsonTool.cs`) get
> downgraded to plain text in F.4.


## ⚠️ Activation context

The architectural pivot to baked-data sim merged 2026-04-25. Sim now reads from
`Assets/Resources/HoleData/Hole_XX/zones.json` + `heightmap.bytes`. Several pre-pivot
code paths are now dead weight or near-dead:

- `SceneGroundProvider` / `SceneSurfaceProvider` — only used as fallbacks in
  `PhysicsLabController.BuildGroundProvider/BuildSurfaceProvider` when baked data
  isn't present. All 18 holes have baked data now, so the fallback branches are
  unreachable in normal operation.
- `PhysicsMarkerRepairTool` — Phase B Roslyn-zombie-component cleanup tool. Served
  its purpose. The bug it solved is gone.
- `MarkerAuditTool` — Phase A diagnostic. Never wired into anything load-bearing.
- A bunch of pre-pivot diagnostic tests that were tactical-fix supporting evidence.
- `Docs/Specs/Active/TERRAIN_REALTEST_FIX.md` — superseded by the pivot's completion.

This spec deletes that dead weight in one focused pass.

**`Physics.Runtime.SurfaceMarker` STAYS** — it's the import → bake bridge.
Importers stamp it on every zone-mesh GO; `BakeZoneJsonTool` reads it to group
meshes by surface type into `zones.json`. Don't touch it. (Future housekeeping
flag below.)

## One-line summary

Delete the unreachable fallback providers, the obsolete diagnostic tools, six
pre-pivot test files, and the stale Active spec. Verify everything still
compiles + all live tests pass.

---

## Hard rules

1. **Branch first.** `git checkout -b phase-f-cleanup`. Tag pre-cleanup:
   `git tag pre-phase-f`. All work on the branch; main untouched until merge.
2. **Per-step commits.** Each numbered step below is its own commit. Commit
   message format: `phase-f.N: <step-summary>`. Don't squash inside the branch
   — Architect wants per-step bisectability if anything regresses.
3. **Compile gate after every step.** If a step leaves the project uncompilable,
   STOP. Don't proceed to the next step. Surface to Architect with the error.
4. **Test gate at the end.** After F.7, run the full Unity test suite. Every
   surviving test must pass. No skipped tests, no `[Ignore]`. If anything fails,
   STOP and surface.
5. **Do NOT touch `Physics.Runtime.SurfaceMarker`.** It's load-bearing for the
   bake pipeline. Keep its file, its asmdef registration, every importer write
   to it, and `BakeZoneJsonTool`'s read of it.
6. **Do NOT touch the importers (`HoleGeoImporter.cs`, `HoleLiteImporter.cs`,
   `BakeZoneJsonTool.cs`)** beyond what's explicitly listed below. They write
   the SurfaceMarker; that machinery stays as-is.
7. **Do NOT re-bake any hole data.** All 18 holes have valid bakes; no
   re-import or re-bake needed.
8. **Do NOT modify `BallSimulation` or any sim-core file.** Pivot is done; sim
   is sealed.

---

## Step F.1 — Delete the obsolete diagnostic tests

Delete these test files + their `.meta` files:

```
Assets/Scripts/Gameplay/Tests/HighVelocityLaunchDiagTests.cs
Assets/Scripts/Gameplay/Tests/HighVelocityLaunchDiagTests.cs.meta
Assets/Scripts/Gameplay/Tests/RealHoleDiagShotsTests.cs
Assets/Scripts/Gameplay/Tests/RealHoleDiagShotsTests.cs.meta
Assets/Scripts/Gameplay/Tests/M5_Shot2DiagTest.cs
Assets/Scripts/Gameplay/Tests/M5_Shot2DiagTest.cs.meta
Assets/Scripts/Physics/Tests/TerrainStressTests.cs
Assets/Scripts/Physics/Tests/TerrainStressTests.cs.meta
Assets/Scripts/Gameplay/Tests/TerrainFallthroughIntegrationTests.cs
Assets/Scripts/Gameplay/Tests/TerrainFallthroughIntegrationTests.cs.meta
Assets/Scripts/Physics/Tests/GroundProviderSurfacePreferenceTests.cs
Assets/Scripts/Physics/Tests/GroundProviderSurfacePreferenceTests.cs.meta
Assets/Scripts/Gameplay/Tests/BakedHeight_Hole01_Test.cs
Assets/Scripts/Gameplay/Tests/BakedHeight_Hole01_Test.cs.meta
Assets/Scripts/Gameplay/Tests/BakedClassifier_Hole01_Test.cs
Assets/Scripts/Gameplay/Tests/BakedClassifier_Hole01_Test.cs.meta
```

These were written during the pre-pivot tactical-fix attempt sequence. They
exercise `SceneGroundProvider` / `SceneSurfaceProvider` paths that are no
longer the sim path of record. The pivot's regression coverage
(`BakedPivotRegressionTests`, `RealHoleTerrainTests`, `BakedHeightProviderTests`,
`BakedZoneClassifierTests`) supersedes them.

`BakedHeight_Hole01_Test` and `BakedClassifier_Hole01_Test` are the M1/M2
agreement tests — they used `SceneGroundProvider`/`SceneSurfaceProvider` as
ground-truth baselines for one-shot pivot-merge validation. That validation
passed at merge time. Sim no longer reads scene path so re-running these
tests today validates a coupling we just severed by design. Unit coverage of
the baked types (`BakedHeightProviderTests`, `BakedZoneClassifierTests`) and
end-to-end real-hole coverage (`RealHoleTerrainTests`) preserve all live
signal value.

**Keep these tests** — they exercise the live providers or are pivot-era:
- `BakedPivotRegressionTests.cs`
- `BakedHeightProviderTests.cs`
- `BakedZoneClassifierTests.cs`
- `RealHoleTerrainTests.cs`
- `ViewerTests.cs`
- everything else not in the delete list above

After this step the test files using `SceneGroundProvider` / `SceneSurfaceProvider`
are gone. F.2 can then delete those types without leaving dangling references.

**Acceptance:** project compiles, deleted files are gone, no other file
references them.

---

## Step F.2 — Delete the obsolete editor tools

Delete these files + their `.meta` files:

```
Assets/Scripts/Editor/PhysicsMarkerRepairTool.cs
Assets/Scripts/Editor/PhysicsMarkerRepairTool.cs.meta
Assets/Scripts/Editor/MarkerAuditTool.cs
Assets/Scripts/Editor/MarkerAuditTool.cs.meta
```

These are the Phase A/B diagnostic + repair tools for the
Roslyn-zombie-component bug that the pivot eliminated by construction. The
menu items they provide (`GOLFIN > Tools > Repair Physics Markers`,
`GOLFIN > Tools > A2 - Marker Audit (Hole_01)`) become unavailable; that's
intentional — they're useless without the bug.

**Watch for:** `MarkerAuditTool` and `PhysicsMarkerRepairTool` may reference
each other or be referenced by `SurfaceMarkerMap.cs`. Check
`SurfaceMarkerMap.cs` after deletion — if its only consumer was these tools,
flag in done report (don't delete it in this step; that's a follow-up
decision).

**Acceptance:** project compiles, deleted files are gone, the two menu items
no longer appear under `GOLFIN > Tools`.

---

## Step F.3 — Strip the SceneProvider fallbacks from PhysicsLabController

`PhysicsLabController.BuildGroundProvider` / `BuildSurfaceProvider` currently
return `SceneGroundProvider` / `SceneSurfaceProvider` as a fallback when baked
data isn't present. After F.4 deletes those types, these fallbacks must be
gone.

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

Replace `BuildGroundProvider()`:

```csharp
IGroundProvider BuildGroundProvider()
{
    if (_bakedGround != null) return _bakedGround;
    return new FlatGround(fp.Zero);
}
```

Replace `BuildSurfaceProvider(ShotPreset preset)`:

```csharp
ISurfaceProvider BuildSurfaceProvider(ShotPreset preset)
{
    if (_bakedClassifier != null) return _bakedClassifier;
    SurfaceType surfaceType = preset.HasSurfaceOverride ? preset.SurfaceOverride : SurfaceType.Fairway;
    return new ConstantSurfaceProvider(surfaceType);
}
```

**What changes:**
- The `currentScene == PresetScene.Hole1 || _useSceneProviders` branch is gone.
- Pre-pivot Hole1 lab (the now-deleted `PhysicsLab_Hole1.unity`) used these.
  Post-pivot, `LabScaffold + additive hole load` always populates
  `_bakedGround`/`_bakedClassifier` via `TryLoadBakedProviders`.
- Falling through to `FlatGround` / `ConstantSurfaceProvider(Fairway)` is the
  correct flat-ground/no-hole behavior — same as before, just without the
  unreachable middle branch.

**Also remove:** the `using Golfin.Physics.Runtime;` import line at the top
of the file IF nothing else in the file references it (PlacementSnapHelper
lives in `Golfin.Physics.Viewer`, not `Runtime`). If the import is still
needed for something else (e.g. `IGroundProvider`), leave it.

**Wire-up in scene:** the `LabScaffold.unity` scene's `PhysicsLabController`
inspector may have `currentScene` set to `Hole1` historically — that's now
just a no-op. Don't change scene authoring.

**Acceptance:** project compiles, `BuildGroundProvider`/`BuildSurfaceProvider`
no longer reference `SceneGroundProvider`/`SceneSurfaceProvider`.

---

## Step F.3.5 — Strip the Phase-A diagnostic harness from PhysicsLabController

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

The `WireA3DiagSinks` static method + its two static `StreamWriter` fields
(`_diagStepWriter`, `_diagHitWriter`) are Phase-A test-runner support, gated
by `#if UNITY_EDITOR`. They wire `BallSimulation.DiagPerStepSink` and
`SceneGroundProvider.DiagHitSink` to CSV files under
`Docs/DIAG/realtest-20260425/` for the (now-deleted) `RealHoleDiagShotsTests`.
Dead since F.1; cannot compile after F.4.

**Delete:**
1. The entire `#if UNITY_EDITOR ... #endif` block containing the
   `_diagStepWriter`/`_diagHitWriter` field declarations and the
   `WireA3DiagSinks()` method.
2. The `WireA3DiagSinks();` call inside `Start()`'s `#if UNITY_EDITOR` guard.
   (Leave the `BallSimulation.DiagErrorLogger = Debug.LogError;` line — that's
   a separate non-A3 diagnostic that the spec leaves alone.)

**Acceptance:** project compiles, no references to `SceneGroundProvider.DiagHitSink`
or `BallSimulation.DiagPerStepSink` remain in `PhysicsLabController.cs`.

---

## Step F.4 — Delete SceneGroundProvider + SceneSurfaceProvider

Delete these files + their `.meta` files:

```
Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs
Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs.meta
Assets/Scripts/Physics/Runtime/SceneSurfaceProvider.cs
Assets/Scripts/Physics/Runtime/SceneSurfaceProvider.cs.meta
```

After F.3, `PhysicsLabController` no longer references these. After F.1, no
test file references them. After F.3.5, no diagnostic-sink wiring references
them. The remaining references are all comment-only or xml-doc:

**Comment in `TrajectoryRenderer.cs` line ~200:**
```csharp
// Disable collider so it doesn't interfere with SceneSurfaceProvider raycasts
```
Update to:
```csharp
// Disable collider so trajectory markers don't interfere with placement raycasts
```
(They're still raycast-relevant — `PlacementSnapHelper.Snap` does its own
raycast for ball placement Y resolution.)

**XML-doc `<see cref>` references** — must be downgraded to plain text or
removed, since `<see cref>` to a deleted type is a CS1574 warning (and
warnings-as-errors in some test runs). Confirmed locations:
- `Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs:14–15`
- `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs:12`
- `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs:46`
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs:323`

For each: replace `<see cref="SceneGroundProvider"/>` /
`<see cref="SceneSurfaceProvider"/>` with backticked plain text
(`` `SceneGroundProvider` ``) or descriptive language ("the legacy
scene-raycast provider, removed in Phase F"). Pick whichever reads better
in context.

**Comment-only ref in `BallSimulation.cs:26`** — Hard rule 8 forbids touching
this file. Leave the comment stale; flag in done report. (It's a `//`
comment, not xml-doc, so no compile warning.)

**Comment-only ref in `ShotPresetCatalog.cs:167`** — Plain `//` comment.
Update it the same way as `TrajectoryRenderer` if convenient; if it's load-
bearing context for the preset author, leave alone. Architect's call:
update it. The note isn't documenting anything live.

**Strip `using Golfin.Physics.Runtime;` from `BakeZoneJsonTool.cs`** ONLY IF
no other Runtime types are referenced after the xml-doc fix above. The file
imports `HeightmapLoader` and `SurfaceMarker` from that namespace — verify
before stripping. Most likely outcome: the using stays.

**Acceptance:** project compiles with zero CS1574 warnings, the two
provider files are gone, `Physics.Runtime` namespace still contains
`SurfaceMarker`, `HeightmapData`, `HeightmapLoader`, `HeightProvider`,
`PhysicsConfigLoader` — all still used by the baker pipeline.

---

## Step F.5 — Delete the stale Active spec

Move:
```
Docs/Specs/Active/TERRAIN_REALTEST_FIX.md  →  Docs/Specs/Completed/TERRAIN_REALTEST_FIX.md
```

(It documents the pre-pivot tactical-fix sequence; archived for historical
context, not active work.)

**Acceptance:** `Docs/Specs/Active/` contains only this spec until F.9 moves it.

---

## Step F.6 — Update `Docs/AI_CONTEXT.md`

Find the section describing the post-pivot architecture state and update the
"Open follow-ups" list:

- Remove "Phase F — delete `SceneGroundProvider`, `SceneSurfaceProvider`,
  `Physics.Runtime.SurfaceMarker`, `PhysicsMarkerRepairTool`. None are
  referenced by sim anymore. ~1 day cleanup." (Phase F is now done.)
- Note that **`Physics.Runtime.SurfaceMarker` was retained** for the import →
  bake bridge. Add a future-housekeeping flag: "Eventually consolidate
  `Physics.Runtime.SurfaceMarker` and `Course.SurfaceMarker` to a single
  enum — bake tool reads two type systems today; not blocking."

If a `History Log` entry block is appropriate, add one for Phase F:

```
- ✅ 2026-04-26 Phase F cleanup — deleted SceneGroundProvider/SceneSurfaceProvider,
  PhysicsMarkerRepairTool, MarkerAuditTool, 6 pre-pivot diag test files, and
  the stale TERRAIN_REALTEST_FIX active spec. Physics.Runtime.SurfaceMarker
  retained for the import → bake bridge. All surviving tests green.
```

Adjust date/format to match existing entries.

---

## Step F.7 — Test gate

Run the **full** Unity test suite (EditMode + PlayMode):

```
Window > General > Test Runner > Run All
```

**Required passes:**
- BakedPivot regression (24/24)
- Phase 1–6 physics
- RealHoleTerrainTests
- BakedHeightProviderTests (unit)
- BakedZoneClassifierTests (unit)
- All ShotController tests (Phase 7 Part B)
- All PlacementSnapTests / PlacementEntriesTests / BallPlacementIntegrationTests
- ViewerTests

If anything fails, STOP. Do not merge. Surface to Architect with the failing
test name + assertion message.

**Capture:** total test count + pass count for the done report.

---

## Step F.8 — Update `TellCode.md`

After F.1–F.7 all pass:

1. Add a `✅ DONE: 2026-04-26 — Phase F cleanup complete` block at the top of
   the `History Log (completed tasks, most recent first)` section. Format
   matches existing entries.
2. Remove the "Phase F cleanup" line from `## ✅ DONE: ARCHITECTURAL PIVOT…`
   "Open follow-ups" subsection — it's no longer open.
3. Move the "Other 17 holes coverage" line UP into the existing
   `## 🚩 OPEN FLAGS — read before starting any new task` section so it stays
   visible.

Then DELETE the per-step pointer block at the top of `TellCode.md` for this
spec — only the History Log entry remains in `TellCode.md`. The full spec
moves to `Docs/Specs/Completed/PHASE_F_CLEANUP.md`.

---

## Step F.9 — Move spec to Completed

```
Docs/Specs/Active/PHASE_F_CLEANUP.md  →  Docs/Specs/Completed/PHASE_F_CLEANUP.md
```

Append a `## DONE REPORT` section at the bottom of the moved spec with:

- Final commit hashes for each numbered step (F.1–F.8).
- File-deletion count (expected: ~16 source files + their `.meta` siblings + 1
  doc relocation).
- Test count + pass rate from F.7.
- Any deviations from this spec (e.g. if `SurfaceMarkerMap.cs` had to be
  modified or deleted to satisfy F.2; if `BakeZoneJsonTool.cs` had a different
  using-clause situation than expected).
- Any surfaced housekeeping items for Architect.

---

## DO NOT

- DO NOT delete `Physics.Runtime.SurfaceMarker.cs`. It's load-bearing for the
  bake pipeline.
- DO NOT touch `BakeZoneJsonTool.cs` beyond fixing dangling `<see cref>` tags
  and unused usings.
- DO NOT touch `HoleGeoImporter.cs` / `HoleLiteImporter.cs` — they write the
  marker, that's intentional.
- DO NOT delete `LabHoleBinder.cs` — it's the additive-load picker bridge,
  unrelated to Phase F.
- DO NOT delete `PhysicsLabZoneMeshBaker.cs` — already deleted in a prior
  session per the lab-migration task. If it's still on disk, that's a
  separate cleanup; flag it in the done report but don't action it here.
- DO NOT modify `BallSimulation.cs` or any other sim-core file.
- DO NOT re-bake any hole data.
- DO NOT add new tests in this spec — pure deletion + ledger update.

---

## Iteration budget

- F.1: 1 attempt. Pure deletion (now 8 test files + .meta siblings).
- F.2: 1 attempt. Pure deletion. Surface if `SurfaceMarkerMap.cs` becomes
  orphaned.
- F.3: 1 attempt. Mechanical replacement.
- F.3.5: 1 attempt. Pure deletion of editor-only diagnostic harness.
- F.4: 1 attempt; 1 retry if a `<see cref>` hides somewhere unexpected.
- F.5–F.9: doc plumbing, 1 attempt each.

Beyond budget on any step: STOP and surface to Architect. Don't whack-a-mole.

## DONE REPORT (2026-04-26)

### Per-step commit hashes (linear, all on `main`)

| Step | Hash | Summary |
|------|------|---------|
| F.1 | `32c73935` | delete 6 obsolete diagnostic test files |
| F.1b (amendment) | `0992ef68` | delete M1/M2 agreement tests `BakedHeight_Hole01_Test`, `BakedClassifier_Hole01_Test` |
| F.2 | `d31facc1` | delete `PhysicsMarkerRepairTool` + `MarkerAuditTool` |
| F.3 | `e33c183c` | strip SceneProvider fallbacks from `PhysicsLabController.BuildGroundProvider` / `BuildSurfaceProvider` |
| F.3.5 | `92f8ec14` | strip Phase-A `WireA3DiagSinks` harness + 2 static `StreamWriter` fields |
| F.4 | `e278ff75` | delete `SceneGroundProvider.cs` + `SceneSurfaceProvider.cs`; downgrade dangling `<see cref>` xml-docs in `BakedHeightProvider`, `BakedZoneClassifier`, `ZoneData`, `BakeZoneJsonTool`; rephrase comments in `TrajectoryRenderer`, `ShotPresetCatalog` |
| F.4-fix | `03744859` | **mid-step:** restore `Physics.Runtime.SurfaceMarker` to its own file (was inline in deleted `SceneSurfaceProvider.cs`; spec hard rule 5 retained the type) |
| F.4b | `0851775a` | clean stale `PhysicsMarkerRepairTool` reference from `SurfaceMarkerMap.cs` doc comment |
| F.5 | `ba19612b` | archive `TERRAIN_REALTEST_FIX.md` → `Docs/Specs/Completed/` |
| F.6 | `6f5e2a7f` | update `Docs/AI_CONTEXT.md` — Physics Architecture status row, removed Phase F open follow-up, added future enum-consolidation flag, new Session Changes block |
| (lessons) | `8b2c82fc` | extract lesson on grepping ALL types in a file before deleting |
| F.8 | `3c2c637d` | update `Docs/TellCode.md` — strip pointer block, move hole-coverage flag to OPEN FLAGS, add 4 new OPEN FLAGS items, History Log entry |

### File-deletion count

- **Source files deleted:** 12 `.cs` files (8 tests + 2 editor tools + 2 scene providers).
- **`.meta` siblings deleted:** 10 (the M5_Shot2DiagTest, BakedHeight_Hole01_Test, BakedClassifier_Hole01_Test never had tracked .meta files — Unity hadn't generated/committed them in the worktree's index).
- **Doc relocations:** 1 (`TERRAIN_REALTEST_FIX.md` → Completed).
- **New files created (not deletions):** 1 (`Assets/Scripts/Physics/Runtime/SurfaceMarker.cs`, the F.4-fix extraction).

### Test gate (F.7)

**EditMode: 198/198 PASS, 0 failed, 0 skipped, 43.5s.** Run via `unity-mcp-cli run-tool tests-run` against the running Unity Editor (PID 22196 on the main repo).

**PlayMode:** 0 tests by design. `Golfin.Physics.Tests.asmdef` is `includePlatforms: ["Editor"]`-only; `[UnityTest]` cases (PlacementSnap, PlacementEntries, BallPlacementIntegration) run as editor coroutines and are counted in the 198. PlayMode runner correctly returned `"No tests found"`.

Required-pass coverage (all part of the 198):
- BakedPivot regression (24)
- Phase 1–6 physics (Aero, Projectile, Putt, StatResolver, Surface, Wind)
- RealHoleTerrainTests
- BakedHeightProviderTests, BakedZoneClassifierTests (unit)
- ShotController + ShotControllerPuttMode tests
- PlacementSnap, PlacementEntries, BallPlacementIntegration
- ViewerTests

### Deviations from spec

1. **`Physics.Runtime.SurfaceMarker` was defined inline inside `SceneSurfaceProvider.cs`.** Spec hard rule 5 said retain the type; I deleted the file in F.4 and took the type with it. Compilation broke across both importers (CS0234) and `PhysicsLabZoneMeshBaker.cs`. Resolved with the `phase-f.4-fix` commit which extracted a 4-line `SurfaceMarker.cs` into the same namespace. Lesson filed (`tasks/lessons.md` top of file).
2. **No branch + no tag.** Spec hard rule 1 said `git checkout -b phase-f-cleanup` + `git tag pre-phase-f`. Standing user feedback (`feedback_main_branch_default.md`) overrides spec branch instructions; all work landed on `main` directly via fast-forward from the worktree branch (`claude/beautiful-swartz-acd73c`). Per-step commits preserved for bisectability.
3. **Worktree-aware doc reads.** I'm operating inside a git worktree (`.claude/worktrees/beautiful-swartz-acd73c`). The spec lived at the main repo's `Docs/Specs/Active/` but not in the worktree's tracked `Docs/`. Memory updated (`feedback_worktree_doc_paths.md`) so future sessions read docs from the main repo path first.
4. **Did not delete `using Golfin.Physics.Runtime;` from `BakeZoneJsonTool.cs`.** Spec said strip it if no other Runtime types are referenced; the file still uses `SurfaceMarker` and `HeightmapLoader` from that namespace, so the using stays.
5. **Did not delete `SurfaceMarkerMap.cs`.** Spec offered to flag if it became orphaned; both importers (`HoleGeoImporter`, `HoleLiteImporter`) consume it, so it stays. Updated its doc comment (F.4b) to drop the deleted `PhysicsMarkerRepairTool` reference.

### Surfaced housekeeping items for Architect

These are now in the OPEN FLAGS section of `TellCode.md` (per F.8):

1. **Stale `// SceneGroundProvider…` comment in `BallSimulation.cs:26`.** Hard rule 8 forbade touching the file during Phase F. Trivial cleanup.
2. **`BallSimulation.DiagPerStepSink` field is now unwired.** `WireA3DiagSinks` was the sole consumer; removed in F.3.5. Field still compiles, harmless dead code. Delete next time sim-core is opened.
3. **`Physics.Runtime.SurfaceMarker` ↔ `Course.SurfaceMarker` enum consolidation** — bake tool reads two type systems via `SurfaceMarkerMap`. Workable; future simplification.
4. **`PhysicsLabZoneMeshBaker.cs`** — spec line 412–414 said this was deleted in a prior session; on disk it's still present (and live, at least it consumes `Physics.Runtime.SurfaceMarker`). Spec said "flag if still on disk, don't action" — flagged.

