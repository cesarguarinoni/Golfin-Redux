# SPEC — `phase_b_surface_tuning` (Stage 1: Diagnostic Harness)

> **Architect-led diagnostic.** This SPEC builds the harness ONLY. Stage 2 (the actual k-value tuning) is a follow-up SPEC written after the CSV captures land — same staging as `controls_e` → `controls_f`. Do not modify `surfaces.csv` or `putt.csv` in this task.

## Status

See `STATUS.md`. Starting at `SPEC_READY`.

## Goal

Build `SurfaceRolloutHarness` — a Play-Mode diagnostic that drives **321 controlled captures** through the real `PhysicsLabController` shot pipeline on a loaded hole, measuring rollout distance per (surface × landing-speed × spin × sample) combination, plus 6 real driver shots from Holes 1/9/18 tees. Output: two CSVs that quantify the math-vs-game drift Cesar observes (over-rolling on green and on landings). Stage 2 reads those CSVs and writes the tuning SPEC.

**Cesar's framing:** "as automated as possible, as real as possible (in game, not just math)." This is satisfied by:
- **Real, not math:** fires through `PhysicsLabController.HandleShotResolved` → real `BallSimulation` with real baked providers (`BakedZoneClassifier`, `BakedHeightProvider`) loaded from the hole's `Resources/HoleData/<holeId>/`, plus real `BallAnimator` playback and real `BallStateMachine`. NOT `BallSimulation.Simulate` against `FlatGround` + `ConstantSurfaceProvider`. That distinction is the whole point — Phase A's tests already cover the math path.
- **Automated:** one MenuItem invocation runs the full sweep across all 3 holes, writes CSVs to disk, self-destructs. No manual ball placement, no manual firing, no manual readback.

## Architecture context

- **Asmdef:** `Golfin.Physics.Viewer` (harness MonoBehaviour) + `Golfin.Physics.Viewer.Editor` (MenuItem). Both already exist. No asmdef changes.
- **Internal access:** harness needs `PhysicsLabController.BallSM`, `LastTrajectory`, `LastShotOrigin`, `CurrentBall`, `HandleShotResolvedForTests`. All are already `internal` and the harness lives in Viewer asmdef directly so internals are visible.
- **Existing infra leveraged (do NOT rebuild):**
  - `PhysicsLabController.OnHoleLoaded(sceneName)` + `OnHoleUnloaded()` — loads/unloads baked providers
  - `PhysicsLabController.PlaceBallAt(worldPos, surfaceTypeValue?)` — teleports ball, type-aware Y snap
  - `PhysicsLabController.HandleShotResolvedForTests(ShotInput, BallPhysicsModifiers)` — internal test seam, fires through real shot pipeline
  - `PhysicsLabController.GetSurfaces()` + `GetGround()` — return the baked providers for discovery scans
  - `PhysicsLabController.LastTrajectory` — internal, the trajectory of the just-fired shot (has `terrainHits` for first ground contact)
  - `BallStateMachine.OnShotComplete` — primary subscription, fires on terminal state
  - `LabHoleBinder` — load hole scenes additively
  - `CaptureCore.SnapPlayModeSafe(label)` — only if Cesar wants per-shot screenshots (default OFF — CSV is the data)

## Sweep matrix

### Sub-mode 1a — Roll-path sweep (real shot pipeline, all surfaces, all speeds)

| Axis | Values | Count |
|---|---|---|
| Surface | Fairway, Green, GreenCollar, Semirough, Rough, Tee, Sand, BunkerLip, CartPath | **9** |
| Landing speed (horizontal, m/s) | 3, 6, 9, 12, 15, 20, 25 | **7** |
| Spin state | Low (~500 rpm backspin), High (~2700 rpm backspin) | **2** |
| Samples | 1, 2 | **2** |
| **Total** | | **252** |

**Drop geometry:** Each capture spawns the ball at altitude 3.0 m above the target surface center with a velocity vector at 30° below horizontal (so `v_horizontal` is the prescribed sweep value, `v_vertical = v_horizontal × tan(30°) ≈ 0.577 × v_h`). Spin axis: `(-1, 0, 0)` for backspin (matches `RollAndPuttTuningTests.DriverInput()` convention). This is a synthetic drop — not a club-fired shot — but it goes through the real `HandleShotResolvedForTests` path so it touches the same surface providers and state machine.

### Sub-mode 1b — Putt-path sweep (Green, GreenCollar, Fairway-fringe)

| Axis | Values | Count |
|---|---|---|
| Surface | Green, GreenCollar, Fairway (off-green putt edge case) | **3** |
| Putt speed (m/s, horizontal at ball center) | 0.5, 1.0, **1.83 (USGA Stimpmeter)**, 2.5, 3.5, 5.0, 7.0 | **7** |
| Samples | 1, 2, 3 | **3** |
| **Total** | | **63** |

**Putt geometry:** Ball spawns at terrain Y (no altitude) with `IsPutt=true` set on the shot controller before firing. Velocity purely horizontal. The 1.83 m/s row on Green is the canonical Stimpmeter capture — Phase A's math predicts ~3.58 m rollout. If in-game observed value is meaningfully larger, that's Cesar's smoking gun for the over-roll bug.

### Sub-mode 2 — Real driver shots from tees

| Axis | Values | Count |
|---|---|---|
| Hole | 1, 9, 18 | **3** |
| Driver shot from tee | shot A (straight at green), shot B (3° draw) | **2** |
| **Total** | | **6** |

Uses `ShotPresetCatalog` driver preset via `PhysicsLabController.Fire(preset)`. Records the same CSV row format. Validates Mode 1's flat-zone numbers translate to real terrain (slopes, contour, fairway curvature).

### Grand total: **321 captures** (~35–45 min Play Mode runtime)

## Implementation

### File 1: `Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs`

Runtime `MonoBehaviour`. Coroutine-driven. Subscribes to `BallStateMachine.OnShotComplete`. Writes CSV rows immediately on each shot (no buffer-and-flush — survives Unity crash mid-sweep).

```csharp
namespace Golfin.Physics.Viewer
{
    public sealed class SurfaceRolloutHarness : MonoBehaviour
    {
        // Set by SurfaceRolloutMenu before Play Mode entry:
        [SerializeField] string _outputDir;        // Docs/Specs/Active/phase_b_surface_tuning/captures/<timestamp>/
        [SerializeField] bool   _runSubMode1a = true;
        [SerializeField] bool   _runSubMode1b = true;
        [SerializeField] bool   _runSubMode2  = true;
        [SerializeField] int    _holeForSubMode1 = 1; // Hole 1 is the canonical surface-rich hole
        [SerializeField] int[]  _holesForSubMode2 = new[] { 1, 9, 18 };

        IEnumerator Start() { /* yield return RunSweep(); SelfDestruct(); */ }

        IEnumerator RunSweep() { /* 1a then 1b then 2 */ }

        // Discovery: walk the loaded hole's BakedZoneClassifier, find N positions per
        // surface type with at least 2m clean radius (no boundary contamination).
        Dictionary<SurfaceType, List<Vector3>> DiscoverSurfaceCenters(int minRadiusM);

        // One capture: place ball, build ShotInput, fire via HandleShotResolvedForTests,
        // await OnShotComplete, compute metrics, write CSV row.
        IEnumerator CaptureRollPath(SurfaceType surface, Vector3 center,
            float vHorizontal, float spinRpm, int sampleId, StreamWriter csv);

        IEnumerator CapturePuttPath(SurfaceType surface, Vector3 center,
            float vHorizontal, int sampleId, StreamWriter csv);

        IEnumerator CaptureRealShot(int hole, int shotVariant, StreamWriter csv);
    }
}
```

### File 2: `Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs`

```csharp
namespace Golfin.Physics.Viewer.Editor
{
    public static class SurfaceRolloutMenu
    {
        [MenuItem("GOLFIN/Physics/Run Surface Rollout Sweep")]
        public static void Run()
        {
            // 1. Verify LabScaffold is the active scene (warn + abort if not)
            // 2. Create timestamped output dir under Docs/Specs/Active/phase_b_surface_tuning/captures/
            // 3. Find or create the SurfaceRolloutHarness GO with output path baked in
            // 4. EditorApplication.EnterPlaymode()
            // (Harness Start() coroutine handles the rest)
        }
    }
}
```

### CSV format

Two output files, both UTF-8 with `\n` line endings.

**`sweep.csv`** (Sub-modes 1a + 1b — discriminated by `mode` column):

```
mode,hole,surface_target,target_v_horizontal_mps,target_spin_rpm,actual_v_at_contact_mps,first_contact_pos_x,first_contact_pos_y,first_contact_pos_z,end_pos_x,end_pos_y,end_pos_z,end_surface,roll_distance_m,bounce_count,sim_duration_s,sample_id,timestamp_iso
roll,1,Fairway,15.0,2700,14.87,123.45,1.20,67.89,138.12,1.18,67.91,Fairway,14.67,3,2.13,1,2026-05-18T15:42:01+09:00
putt,1,Green,1.83,0,1.81,234.50,2.10,98.20,238.05,2.09,98.21,Green,3.55,0,1.94,1,2026-05-18T15:42:08+09:00
...
```

**`real_shots.csv`** (Sub-mode 2):

```
hole,shot_variant,tee_pos_x,tee_pos_y,tee_pos_z,first_contact_pos_x,first_contact_pos_y,first_contact_pos_z,end_pos_x,end_pos_y,end_pos_z,end_surface,carry_m,roll_m,total_m,bounce_count,sim_duration_s,timestamp_iso
1,straight,12.30,0.50,4.20,235.10,1.20,5.80,253.40,1.18,6.10,Fairway,222.8,18.3,241.1,1,5.42,2026-05-18T15:55:12+09:00
...
```

### Metrics computation (per capture)

- `first_contact_pos` = first non-stop entry in `LastTrajectory.terrainHits` (`!hit.IsStop`). If `terrainHits` is empty (airborne to OOB), record zeros and log a warning row.
- `actual_v_at_contact_mps` = velocity magnitude in the trajectory sample closest in time to the first contact (`LastTrajectory.samples` is time-ordered; find sample with position closest to `first_contact_pos`).
- `end_pos` = `result.EndPosition` from the `ShotResult` payload (in `OnShotComplete`).
- `end_surface` = `result.EndSurface`.
- `roll_distance_m` = horizontal XZ distance from `first_contact_pos` to `end_pos`. Zero if `terrainHits` empty.
- `sim_duration_s` = `result.SimDuration.ToFloat()`.
- `bounce_count` = `result.BounceCount`.

### Discovery phase (auto-find clean surface centers)

To pick where to place the ball for each surface in Sub-mode 1a:

1. Get `ISurfaceProvider` via `controller.GetSurfaces()`.
2. Get hole world bounds (use `BakedZoneClassifier`'s bounds or fall back to scanning `PlacementEntries`).
3. Sample on a 2 m grid across the bounds.
4. Group sample positions by classified surface.
5. For each surface group, run a 2-m-radius integrity check: re-classify 8 neighbor points at `(±2m, 0)` and `(0, ±2m)`. Keep only positions where all 9 classifications match.
6. Pick the position farthest from any boundary (largest min-distance to a re-classification mismatch). One center per surface.
7. If a surface has <1 clean position (e.g. Sand absent from Hole 1), log a warning and skip that surface for the sweep (CSV rows just won't appear for it). Stage 2 SPEC reads the CSV; missing surface = "need to run sweep on a different hole" data point.

### Resume support (mid-sweep crash recovery)

- Before each capture, the harness writes a one-line `pending: <mode> <surface> <speed> <spin> <sample>` to `progress.log`.
- On Start, the harness reads `progress.log` if it exists and skips already-completed `(mode, surface, speed, spin, sample)` tuples.
- This means if Unity hangs on capture #137, you can re-launch the menu and pick up at #138.

### Hole loading sequence

The harness expects `LabScaffold` to be the active scene at MenuItem invocation. It then orchestrates hole load/unload internally:

- Sub-mode 1a + 1b: load Hole 1 via `LabHoleBinder.LoadHole(1)`, run all captures, unload.
- Sub-mode 2: load Hole 1, fire 2 driver shots, unload. Load Hole 9, fire 2, unload. Load Hole 18, fire 2, unload.

`LabHoleBinder` is the existing pattern (see `LabHoleBinder.cs`). Use its public API; do not duplicate hole-loading logic. **NOTE: confirm `LabHoleBinder.LoadHole(int)` exact API surface before coding — if no such public method exists, flag in IMPLEMENTER_REPORT and request architect clarification rather than guessing.**

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] `SurfaceRolloutHarness.cs` lands at `Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs`
- [ ] `SurfaceRolloutMenu.cs` lands at `Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs`
- [ ] MenuItem `GOLFIN/Physics/Run Surface Rollout Sweep` is reachable from Unity menu
- [ ] Both CSVs produced at `Docs/Specs/Active/phase_b_surface_tuning/captures/<timestamp>/sweep.csv` + `real_shots.csv`
- [ ] `sweep.csv` contains at least 252 rows (Sub-mode 1a) + 63 rows (1b) = ≥315 rows (allow up to 9 missing if surfaces absent from Hole 1; log warnings)
- [ ] `real_shots.csv` contains 6 rows
- [ ] All `end_surface` values match `surface_target` for Sub-mode 1a (sanity: ball landed on the surface we aimed at) — log any mismatches; > 5% mismatch rate → FAIL
- [ ] Stimpmeter row (Sub-mode 1b, surface=Green, target_v=1.83) records `roll_distance_m` for Cesar's eyeball — Phase A math predicts ~3.58 m, in-game observation IS the diagnostic
- [ ] `progress.log` correctly records pending + completed rows; resume-skip behavior verified by manually killing Play Mode mid-sweep and re-running
- [ ] No production code touched (only `Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs` and `.../Editor/SurfaceRolloutMenu.cs` are new; nothing else modified)
- [ ] Harness self-destructs after completion (`Destroy(gameObject)` after CSV close)
- [ ] No new EditMode tests required (harness is a diagnostic, not production logic) — gate stays at 294/294
- [ ] No `surfaces.csv` or `putt.csv` changes
- [ ] Spec deviations (if any) flagged with justification

## Files / hierarchy this task touches

- **NEW** `Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs`
- **NEW** `Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs`
- **NEW** `Docs/Specs/Active/phase_b_surface_tuning/captures/<timestamp>/sweep.csv` (output, produced at run time)
- **NEW** `Docs/Specs/Active/phase_b_surface_tuning/captures/<timestamp>/real_shots.csv` (output, produced at run time)
- **NEW** `Docs/Specs/Active/phase_b_surface_tuning/captures/<timestamp>/progress.log` (output, produced at run time)
- **NEW** `Docs/Specs/Active/phase_b_surface_tuning/captures/<timestamp>/README.md` (output: run config, total captures, holes used, timestamp, harness git SHA)

No production code modified. No scene mutations. No asmdef changes.

## Smoke evidence

Implementer runs the menu item once end-to-end on Cesar's machine, captures Unity Console output to `IMPLEMENTER_REPORT.md`, and produces the two CSVs. Cesar visually confirms:

1. CSVs exist at the expected path
2. Row counts match acceptance criteria
3. Stimpmeter row (1.83 m/s on Green) is present with non-zero `roll_distance_m`
4. Sub-mode 2 driver-from-tee rows show plausible carry distances (160–250 m range)

This is a diagnostic tool, not a visual fidelity task — Lesson O does not apply. Numerical CSV correctness IS the evidence.

## Out of scope (do NOT do these)

- Do NOT modify `surfaces.csv` or `putt.csv` — that's Stage 2
- Do NOT tighten Phase A's loose test bands in `RollAndPuttTuningTests.cs` — that's Stage 2
- Do NOT add CLI/batchmode entry — MenuItem only (Cesar's locked decision)
- Do NOT add per-shot screenshots — CSV is the data; screenshots blow up runtime
- Do NOT touch `BallSimulation`, `BallStateMachine`, `SurfaceConfig`, `PuttConfig`, or any other production class
- Do NOT add new EditMode tests — harness is a one-shot tool
- Do NOT skip the Discovery phase by hardcoding world positions — discovery is what makes this "automated" per Cesar's instruction. If a surface lacks a clean center on Hole 1, log + skip; Stage 2 will use a different hole if needed
- Do NOT firehose the Console — INFO log lines should be ≤2 per capture (start + end); progress milestone log every 50 captures
- Do NOT change `LabHoleBinder` or any production hole-loading code

## References

- `RollAndPuttTuningTests.cs` — Phase A validation tests with loose bands `[8, 45]` and `[100, 400]`
- `PhysicsLabController.HandleShotResolvedForTests` (internal test seam) — drives shots through the real pipeline without UI
- `BallStateMachine.OnShotComplete` (event, `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs`)
- `ShotResult` (`Assets/Scripts/Gameplay/Loop/ShotResult.cs`) — fields used: `EndPosition`, `EndSurface`, `BounceCount`, `SimDuration`
- `Trajectory.terrainHits` (`Assets/Scripts/Physics/Core/Trajectory.cs`) — has `Position`, `IsStop`, `Surface` per hit
- `surfaces.csv` current state — `Assets/Resources/Physics/surfaces.csv` (do NOT modify)
- Real-world targets for Stage 2 comparison: Trackman 2024 carry+roll (cited per Lesson K — driver 275 yd carry + 15–30 yd roll on tour fairway, irons proportionally less roll, putts on Stimp-12 green follow `d = v0/k` with k=0.50 predicting 3.66 m for 1.83 m/s release). Sources will be cited inline in Stage 2 SPEC.
- Predecessor `controls_c_fix` Phase A — locked Green k=0.50, CartPath k=0.30; observation-only on Fairway/Rough
- Pipeline classification: **Tier 3 Full Pipeline** (runtime spatial math in discovery phase + multi-file + diagnostic output that gates Stage 2). Subagent chain: implementer → self-reviewer → reviewer → Cesar approval, then Cesar runs the menu and gives Architect the CSV for Stage 2.
