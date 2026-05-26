# SPEC — `green_authoring_editor_tool`

> Phase 2 of the umbrella `green_topology_and_pin_authoring`. Promoted to Active 2026-05-26 11:15 CEST.
> Umbrella: `Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md`
> Phase 1 (data format + runtime classes) shipped 2026-05-26 in commit `47dd8f6d`.

## Status

`SPEC_READY`. See `STATUS.md`.

## Goal

Build the editor tool that lets Cesar author per-hole green slope grids and pin candidates, using the Lomond strategy PDF and Shot Navi captures as visual reference, and saves the result as `Assets/Resources/HoleData/Hole_NN/green.json` round-trippable through Phase 1's `GreenTopology.LoadFromResources`. Unblocks Phase 3 (procedural baseline pass — Cesar manual) and Phase 4 (PDF tracing — Cesar manual).

## Reference

- **Umbrella SPEC:** `Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md` § Phase 2
- **Phase 1 deliverables:** commit `47dd8f6d` — `Assets/Scripts/Course/Runtime/{GreenTopology.cs, GreenTopologyCache.cs, Golfin.Course.Runtime.asmdef}` + `Assets/Resources/HoleData/Hole_01/green.json` skeleton
- **Authoring source data (Phase 4 input, already in repo):** `Docs/Specs/Queued/green_topology_and_pin_authoring/A4_ホール攻略冊子.pdf` + 36 PNGs in same folder's `screenshots/`
- **Existing hole-picker UX pattern:** `Assets/Scripts/Editor/Physics/PhysicsLabHolePicker.cs` (`Golfin.Physics.Editor`)
- **Existing zones.json reader:** `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` + `BakedZoneClassifier.cs` (`Golfin.Physics.Runtime.Baked`, asmdef `Golfin.Physics.Runtime`)

## Architecture context

**New asmdefs:**
- `Golfin.Editor.GreenAuthoring` (editor-only) at `Assets/Scripts/Editor/GreenAuthoring/`
- `Golfin.Course.Tests` (editor-only, EditMode tests) at `Assets/Scripts/Course/Tests/`

**Existing asmdefs referenced:**
- `Golfin.Course.Runtime` (Phase 1) — read API + types
- `Golfin.Physics.Runtime` — `ZoneData`, `BakedZoneClassifier`, `SurfaceType` (via re-export from `Golfin.Physics.Core`)
- `Golfin.Physics.Core` — `SurfaceType` enum

**Existing asmdefs NOT touched:** Anything under `Golfin.Physics.Viewer` (including `PutterGreenReader.cs`), `Golfin.Editor.CourseImporter` would-be (still Assembly-CSharp-Editor — unchanged), `Golfin.Gameplay.*`.

**Hands-off file list (modifying any of these is an automatic FAIL):**
- `Assets/Scripts/Course/Runtime/GreenTopology.cs`
- `Assets/Scripts/Course/Runtime/GreenTopologyCache.cs`
- `Assets/Scripts/Course/Runtime/Golfin.Course.Runtime.asmdef`
- Any file under `Assets/Scripts/Editor/CourseImporter/`
- Any file under `Assets/Scripts/Physics/Viewer/` (including `PutterGreenReader.cs`)
- `Assets/Resources/HoleData/Hole_NN/heightmap.bytes` (any hole; Phase 5 owns)
- `Assets/Resources/HoleData/Hole_NN/zones.json` (any hole; read-only consumer)

## Locked decisions (Q-locks)

**Q1 — zones.json reader source.** The new tool references `Golfin.Physics.Runtime` and uses `ZoneData.FromJson(File.ReadAllText(...))` + `BakedZoneClassifier(zoneData).GetPolygonAABBsForType(SurfaceType.Green)` to obtain green polygons and AABBs. Do NOT create a `Golfin.Course.Editor` asmdef — the umbrella's reference there was speculative; the canonical reader is runtime-side and already accessible.

**Q2 — Serialization owner.** `Golfin.Editor.GreenAuthoring` defines its own write-side DTO (`GreenJsonWriter.GreenJsonDto`) that matches Phase 1's read DTO field-for-field. Phase 1's `GreenTopology` stays read-only and its private DTO stays private. The round-trip EditMode test (T1 below) is the canonical drift-detector.

**Q3 — `editorBackdrop` metadata.** Optional top-level object in `green.json`. Format:

```json
"editorBackdrop": {
  "imageAssetPath": "Docs/Specs/Queued/green_topology_and_pin_authoring/screenshots/lomond_hole_01_shotnavi_strategy.png",
  "imagePoints":  [ { "u": 312, "v": 410 }, { "u": 689, "v": 410 } ],
  "worldPoints":  [ { "x": -10.0, "z": 40.0 }, { "x": -8.0, "z": 40.0 } ]
}
```

Affine transform is recomputed at edit time from the two correspondence pairs (similarity transform: uniform scale + rotation + translation, since 2 points underdetermine a full affine). Runtime ignores the field (JsonUtility tolerates unknown JSON fields without error).

**Q4 — Procedural fill drain axis.** Mean of `−∇heightmap` sampled at the polygon's vertices (negative gradient = downhill direction). Normalized to unit vector. This is the drain axis. Do NOT use polygon's principal-axis as a proxy — physical drainage is what matters.

**Q5 — Save action atomicity.** Write to `green.json.tmp` → `File.Replace("green.json.tmp", "green.json", null)` → `AssetDatabase.ImportAsset("Assets/Resources/HoleData/Hole_NN/green.json")` → `GreenTopologyCache.Invalidate(holeNumber)`. Mirrors `save_layer_reactive_foundation` pattern. If save validation (Q8) fails, abort before writing the .tmp file.

**Q6 — Test asmdef name.** `Golfin.Course.Tests` (new), at `Assets/Scripts/Course/Tests/`. `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `precompiledReferences: ["nunit.framework.dll"]`, `overrideReferences: true`. References: `Golfin.Course.Runtime`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`.

**Q7 — Brush model.** Paint mode brush is a circular kernel: cells within `brushRadiusCells × cellSize` of the cursor receive the painted (dirX, dirZ, magPct) tuple. Falloff = uniform (no Gaussian); cells either inside or outside the radius. Direction is set by drag vector (cursor delta normalized over the last ~5 frames). Magnitude is set by a separate slider. Right-click clears (sets all three to 0).

**Q8 — Save validation.** Reject save (surface a red-text status bar message; do NOT write file) if any of:
- `gridWidth ≤ 0` or `gridHeight ≤ 0`
- `slopeGrid.Length != gridWidth * gridHeight * 3`
- `pinCandidates.Count == 0`
- `defaultPinIndex < 0` or `defaultPinIndex >= pinCandidates.Count`
- Any pin candidate not inside the bounds rect (with 0.5m tolerance for fringe pins)
- `magnitudePercent > 12` anywhere (clamp limit per Phase 6 hard rule 3; authoring error)
- `cellSize ≤ 0`

**Q9 — Procedural fill button behavior.** Runs `GreenAuthoringMath.ComputeProceduralSlopeField(polygon, heightmapSampler)` and replaces the current slope grid in-memory (does NOT auto-save; user reviews then hits Save). Pin candidates are NOT reset by procedural fill — author edits those separately.

**Q10 — Hole picker.** Reuse the SAME `EditorPrefs` key `"Golfin.PhysicsLab.CurrentHole"` from `PhysicsLabHolePicker` so opening the Green Authoring tool defaults to the hole the user was last working on in the lab. Optional: a dropdown 1-18 to switch. No need to additively load the hole scene — the tool reads `zones.json` + `heightmap.bytes` directly from `Resources/HoleData/`.

## Implementation

### File 1 — Asmdef: `Assets/Scripts/Editor/GreenAuthoring/Golfin.Editor.GreenAuthoring.asmdef`

```json
{
    "name": "Golfin.Editor.GreenAuthoring",
    "rootNamespace": "Golfin.Editor.GreenAuthoring",
    "references": [
        "Golfin.Course.Runtime",
        "Golfin.Physics.Runtime",
        "Golfin.Physics.Core"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "autoReferenced": false,
    "noEngineReferences": false
}
```

### File 2 — Test asmdef: `Assets/Scripts/Course/Tests/Golfin.Course.Tests.asmdef`

```json
{
    "name": "Golfin.Course.Tests",
    "rootNamespace": "Golfin.Course.Tests",
    "references": [
        "Golfin.Course.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "noEngineReferences": false
}
```

### File 3 — `GreenTopologyEditor.cs` (EditorWindow)

`namespace Golfin.Editor.GreenAuthoring`. Menu: `[MenuItem("GOLFIN/Green Authoring/Open Editor")]`. The window is the user-facing surface; design is implementer's call within these requirements:

- **Top bar:** hole-picker dropdown (1-18) wired to `EditorPrefs["Golfin.PhysicsLab.CurrentHole"]`; on change, reload `zones.json` + `heightmap.bytes` + `green.json` (if present) for the selected hole.
- **Center panel:** 2D top-down "green view" rendered via IMGUI (`GUI.DrawTexture` for backdrop; `Handles.DrawAAPolyLine` for polygon outline; `Handles.DrawSolidArc` or custom mesh for slope arrows; coloured cells for grid overlay). Brush cursor follows mouse position projected into world XZ. View pans/zooms.
- **Left sidebar:** mode toggle (Paint Slope / Add Pin / Clear Cells / Procedural Fill); brush radius slider (1-5 cells); slope magnitude scrubber (0-12%); slope direction display (read from drag vector or explicit angle input).
- **Right sidebar:** pin candidate list (label + worldX/Y/Z + radio for default + reorder up/down + delete); save button; load button; status bar (validation messages, last-save timestamp).
- **Backdrop slot:** "Drag PNG here" zone above the green view; on drop, prompts for 2 anchor-point pairs (click on backdrop, then on green polygon) to establish similarity transform. Subsequent backdrops require the user to re-align unless they match a previously-saved backdrop's `imageAssetPath`.
- **Persistence:** all UI state EXCEPT the green data is `EditorPrefs`-backed (zoom, pan, brush radius, mode toggle). Green data lives only in `green.json` (load on hole switch, save on save button).

The implementer is free to use either IMGUI or UI Toolkit. IMGUI is the precedent (PhysicsLabHolePicker, all CourseImporter editors). UI Toolkit is acceptable if implementer prefers it.

### File 4 — `GreenAuthoringMath.cs` (pure C#, no UnityEditor refs)

`namespace Golfin.Editor.GreenAuthoring`. Public static methods:

```csharp
public static class GreenAuthoringMath
{
    /// <summary>
    /// Computes a procedural slope field over the green's bounding rect.
    /// Heuristic per umbrella SPEC § Phase 2 feature 5:
    ///   - 1.5% baseline along drain axis (mean of -∇heightmap at polygon vertices, normalized)
    ///   - False-front bump: cells in front 20% of polygon (by drain axis) with heightmap < (polygon median − 0.3m)
    ///     get magnitude clamped up to 2.5% in drain-axis direction
    ///   - Tier-break ridge: if (heightmap range across polygon > 0.5m), insert a 4% magnitude band
    ///     perpendicular to drain axis at the elevation midpoint, smoothed across ±2 cells
    /// Cells outside the polygon set to (0, 0, 0).
    /// Returns a freshly-allocated float[width*height*3] interleaved (dirX, dirZ, magPct).
    /// </summary>
    public static float[] ComputeProceduralSlopeField(
        IReadOnlyList<Vector2> polygonXZ,
        Vector2 boundsMin,
        Vector2 boundsMax,
        float cellSize,
        int gridWidth,
        int gridHeight,
        System.Func<Vector2, float> heightmapSampler);

    /// <summary>
    /// Solves the similarity transform (uniform scale + rotation + translation) mapping the
    /// two image-space points to the two world-space points. Returns an affine 2x3 matrix
    /// such that worldXZ = M * (imageUV, 1).
    /// Throws ArgumentException if the two image points are coincident.
    /// </summary>
    public static Matrix4x4 SolveSimilarityFrom2Points(
        Vector2 imageA, Vector2 imageB,
        Vector2 worldA, Vector2 worldB);

    /// <summary>
    /// Point-in-polygon test (XZ-plane, ray casting). Polygon ring need not be closed
    /// (last vertex != first); function handles both.
    /// </summary>
    public static bool IsPointInPolygon(Vector2 pointXZ, IReadOnlyList<Vector2> polygonXZ);
}
```

The drain-axis heuristic must produce a UNIT vector. False-front detection uses the "front" of the polygon defined as cells whose projection onto the drain axis is in the bottom 20% of the polygon's drain-axis range. Tier-break detection is single ridge only (don't try to detect multi-tier in this pass — Cesar refines manually in Phase 4).

### File 5 — `GreenJsonWriter.cs`

`namespace Golfin.Editor.GreenAuthoring`. Public API:

```csharp
public static class GreenJsonWriter
{
    /// <summary>
    /// Serializes the green's authored state to JSON matching Phase 1's schema and writes it
    /// atomically to Assets/Resources/HoleData/Hole_NN/green.json.
    /// Pipeline: validate → ToJson → temp write → File.Replace → AssetDatabase.ImportAsset → GreenTopologyCache.Invalidate.
    /// Returns true on success; emits Debug.LogError + returns false on validation failure or I/O error.
    /// </summary>
    public static bool SaveToResources(
        int holeNumber,
        string sourceTag,
        Vector2 boundsMin, Vector2 boundsMax,
        float cellSize, int gridWidth, int gridHeight,
        float[] slopeGrid,
        IReadOnlyList<(Vector3 world, string label)> pinCandidates,
        int defaultPinIndex,
        EditorBackdropMetadata backdrop /* nullable */);
}

[Serializable]
public class EditorBackdropMetadata
{
    public string imageAssetPath;
    public Vector2[] imagePoints;   // length 2
    public Vector2[] worldPoints;   // length 2
}
```

Internal DTO must match Phase 1's `GreenTopology.GreenJsonDto` field-for-field — verified by the round-trip test T1. The DTO type itself stays private to `GreenJsonWriter` (do not export; do not reach into Phase 1's private types).

### File 6 — `Golfin.Course.Tests/GreenTopologyTests.cs`

`namespace Golfin.Course.Tests` (matching the asmdef's `rootNamespace`). Three mandatory tests:

**T1 — Round-trip schema fidelity (the CRITICAL test):**
- Construct a synthetic state (8×8 grid, 192 floats, deterministic non-zero values like `dirX=i*0.1, dirZ=j*0.1, magPct=(i+j)%12`; 3 pin candidates with distinct labels).
- Call `GreenJsonWriter.SaveToResources(...)` with hole number `99` (out-of-range, written to a temp Resources path; teardown deletes).
- Call `GreenTopology.LoadFromResources(99)` → assert not null.
- For all (i, j): assert sampled slope at cell center matches the constructed grid byte-equal via `TrySampleSlope`.
- Assert pin candidates and labels match author-supplied order.
- Assert `DefaultPinIndex == 0`.

**T2 — Out-of-bounds returns false:**
- Load a fixture `green.json` (use Hole_01 phase1_skeleton).
- Sample 4 corners shifted outside bounds by `2 * cellSize` → assert `TrySampleSlope` returns false and outputs zero.

**T3 — Pin candidates returned in author-supplied order:**
- Load Hole_01 (3 placeholder pins).
- Assert `GetPinCandidates()[0].label == "skeleton-center"`, `[1] == "skeleton-front-right"`, `[2] == "skeleton-back-left"` (per Hole_01 skeleton).

Implementer may add more tests freely (math helpers in `GreenAuthoringMath` are great test candidates — point-in-polygon, similarity transform, procedural fill on a constructed input).

**Test cleanup:** T1 writes to `Hole_99` — must delete `Assets/Resources/HoleData/Hole_99/` in `[TearDown]`. Don't leak fixture data.

### Subagent pipeline expectations

- **Implementer (`golfin-implementer`):** ship files 1-6, run EditMode tests, fill `IMPLEMENTER_REPORT.md` with PASS/FAIL on every Acceptance checklist item below.
- **Self-reviewer (`golfin-self-reviewer`):** independent grep + test re-run + asmdef-graph verification (no asmdef-direction violations); fills `SELF_REVIEW.md`; forwards to architect-reviewer if all 7 checklist items pass.
- **Architect-reviewer (`golfin-reviewer`):** cross-cuts (asmdef hygiene, Lesson R `.meta` check, hands-off file list audit via `git diff --stat`), fills `ARCHITECT_REVIEW.md`. APPROVE / FAIL / ESCALATE.
- **Cesar final gate:** opens the tool, authors a slope grid on Hole 1 (paint a few cells + add one pin + Save), reopens, confirms data survives the round-trip visually.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

1. [ ] `Golfin.Editor.GreenAuthoring.asmdef` created with the exact JSON above (verbatim references, autoReferenced:false, Editor-only platform).
2. [ ] `Golfin.Course.Tests.asmdef` created with the exact JSON above.
3. [ ] `GreenTopologyEditor.cs` opens via `GOLFIN/Green Authoring/Open Editor` menu without compile errors and without console errors.
4. [ ] Hole picker defaults to last `EditorPrefs["Golfin.PhysicsLab.CurrentHole"]` value (verify by setting Lab to Hole 5, opening the tool, screenshot showing 5 selected).
5. [ ] Loading Hole 1 displays the green polygon outline derived from `zones.json` (screenshot showing visible polygon).
6. [ ] Paint mode places non-zero cells in the slope grid (screenshot before + after a brush stroke).
7. [ ] Procedural fill button populates non-zero cells in baseline pattern (screenshot showing arrow field across the green).
8. [ ] At least one pin candidate added via UI; appears in pinCandidates list with editable label.
9. [ ] Save validates and writes `green.json` atomically (verify `green.json.tmp` does not persist after success).
10. [ ] `GreenTopology.LoadFromResources(1)` after save returns non-null with the authored data (asserted in IMPLEMENTER_REPORT via reflection-driven probe or test output).
11. [ ] `GreenTopologyCache.Invalidate(1)` called from save path (grep + IMPLEMENTER_REPORT call-site citation).
12. [ ] Three mandatory tests T1-T3 pass; total test count ≥ baseline + 3.
13. [ ] EditMode full-suite test gate: ≥ baseline + 3 PASS, 0 IGNORED. (Baseline at session start was 356/0/3 per AI_CONTEXT 2026-05-26 10:20 CEST.)
14. [ ] No file modified outside the new asmdef boundaries (verify via `git diff --name-only` itemized in IMPLEMENTER_REPORT).
15. [ ] All new `.cs` and `.asmdef` files have committed `.meta` siblings (Lesson R).
16. [ ] Hole_01 skeleton survives — `Assets/Resources/HoleData/Hole_01/green.json` is overwriten ONLY if the implementer chose to test save on Hole 1; if so, IMPLEMENTER_REPORT documents the new content and round-trip-verifies it loads cleanly. Otherwise skeleton stays intact byte-for-byte.

## Out of scope (do not creep)

- **Phase 3 work:** the tool ships; the 18 procedural fills + pin candidates are Cesar's manual Phase 3 task. Implementer ships the TOOL, not the data.
- **Heightmap reconciliation:** Phase 5 owns. Phase 2 reads `heightmap.bytes` only as a procedural-fill input; never writes.
- **Multi-tier ridge detection beyond a single ridge:** complex tier shapes (e.g. hole 7's diagonal ridge) are authored manually in Phase 4 paint mode. Procedural heuristic targets only the most common single-ridge case.
- **Pin position rotation / day-of-pin support:** authored set per green is the deliverable; runtime rotation is Loop v2+ feature.
- **`PutterGreenReader.cs` swap:** Phase 7 will move its slope source from mesh-baked to `GreenTopologyCache`. Phase 2 does not touch it.
- **`Resources.UnloadAsset` for the editor-time `green.json` reads:** editor isn't perf-sensitive; let the GC handle it.

## Open question / amendment slot

None at SPEC writing. If pre-flight discovers an asmdef-direction blocker (Lesson W class), implementer files IMPLEMENTER_BLOCKED with the discovered constraint and proposed workaround before changing scope.

## Reference paths

- This SPEC: `Docs/Specs/Active/green_authoring_editor_tool/SPEC.md`
- Reports: `IMPLEMENTER_REPORT.md`, `SELF_REVIEW.md`, `ARCHITECT_REVIEW.md` (templates pre-populated)
- Status: `STATUS.md`
- Captures: `screenshots/`, `videos/`
- Umbrella SPEC: `Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md`
- Phase 1 commit: `47dd8f6d`
