# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
>
> **Workflow update (2026-04-21):** Claude Code now drives Unity directly via Unity-MCP (https://github.com/IvanMurzak/Unity-MCP). Tools available: `script-update-or-create`, `script-execute`, `tests-run`, `console-get-logs`, `scene-create`/`open`/`save`, `gameobject-create`/`component-add`/`modify`, `editor-application-set-state`, `screenshot-game-view`/`scene-view`, `package-add`, and more. Specs below include autonomous validation criteria — run them to confirmation rather than reporting "done" and waiting for Cesar to verify.

---

## ACTIVE TASK — Phase 0: Physics Heightmap Baker

### Context

We're building the physics layer (see `Docs/PHYSICS_RESEARCH.md` for full architecture). Phase 4 (surface bounce & roll) needs to sample terrain heights deterministically across platforms — `Terrain.SampleHeight()` is a Unity API that may produce platform-different floats, which breaks determinism.

**Phase 0 solution:** at course import time, bake the imported Unity terrain heightmap into a deterministic fixed-point `long[,]` array and write it to disk as `heightmap.bytes` next to the existing per-hole export files. At runtime (Phase 4), a `HeightProvider` MonoBehaviour loads the bytes and exposes a deterministic `SampleHeight(fpX, fpZ)` API. The runtime loader is Phase 4's concern — **Phase 0 only writes the file.**

### Goal

New editor tool `PhysicsHeightmapBaker` that reads imported `TerrainData` from Unity scenes and writes Q16.16 fixed-point heightmaps to disk, with three menu entry points: current hole / specific hole / all holes.

### New file

`Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs`

Namespace: `Golfin.CourseImport` (matches `HoleGeoImporter`).
Wrap in `#if UNITY_EDITOR ... #endif`.

### Menu items

```
Import > Bake Physics Heightmap > Bake Current Hole
Import > Bake Physics Heightmap > Bake Hole 01
Import > Bake Physics Heightmap > Bake Hole 02
... (through Hole 18)
Import > Bake Physics Heightmap > Bake All Holes
```

Use `[MenuItem("Import/Bake Physics Heightmap/Bake Current Hole", false, 200)]` etc. Priority 200 places the group below the existing `Import > Geo > ...` entries. Group the 18 per-hole items with a single helper method and generate the menu items via 18 explicit `[MenuItem]` attributes (Unity doesn't support dynamic `[MenuItem]`, so just write them out — 20 short wrapper methods is fine).

### Core method signature

```csharp
private static void BakeHole(int holeNumber)
```

Takes a hole number 1..18. Figures out the scene path, opens it if it's not the active scene, finds the `Terrain` component, bakes, writes to disk, closes the scene if it was opened by the bake (leave open if it was already active).

### Scene resolution

Current active scene first:
```csharp
var active = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
```

For `Bake Current Hole`: extract hole number from scene name using the same regex `HoleGeoImporter` uses. Scene names follow `Hole_XX`, `Hole_XX_Geo`, `Hole_XX_Flat`, or `Hole_XX_Geo_Flat`. The Geo pipeline is the active one (per AI_CONTEXT), so for per-hole and all-holes modes, prefer the Geo scene:

```
Assets/Courses/LomondCC/Holes/Hole_{N:D2}_Geo.unity
```

If that path doesn't exist, fall back to `Hole_{N:D2}.unity`, log a warning if neither exists.

### Reading the terrain data

```csharp
var terrainObj = GameObject.Find("Terrain");
// If not found, search all active Terrain components:
if (terrainObj == null)
{
    var t = UnityEngine.Object.FindObjectOfType<Terrain>();
    if (t != null) terrainObj = t.gameObject;
}

var terrain = terrainObj?.GetComponent<Terrain>();
if (terrain == null || terrain.terrainData == null)
{
    Debug.LogError($"[PhysicsHeightmapBaker] Hole {holeNumber}: no Terrain found in scene.");
    return;
}

var tData = terrain.terrainData;
int res = tData.heightmapResolution;        // typically 2049
float[,] heights = tData.GetHeights(0, 0, res, res);  // normalized [0..1]
Vector3 size = tData.size;                  // world meters (x, y=elevRange, z)
```

**Note:** `GetHeights` returns `heights[y, x]` indexing (row-major by Z then X), matching Unity's convention. Preserve this indexing when writing — the reader in Phase 4 will mirror it.

### Fixed-point conversion (Q16.16)

Q16.16 = signed 32-bit integer where the lower 16 bits are fractional. Range: ±32768.0, precision: ~0.0000153 (15 μm). Golf elevation ranges are ~35m max, well inside this.

Store **world-meter heights** (not normalized) so Phase 4 doesn't need to remember the elev range:

```csharp
// Constants
const int Q16_16_SHIFT = 16;
const float Q16_16_SCALE = 65536f; // 2^16

// Conversion
int ToQ16_16(float worldMeters) => (int)Math.Round(worldMeters * Q16_16_SCALE);
float FromQ16_16(int fp) => fp / Q16_16_SCALE;
```

Q16.16 fits in a 32-bit `int`. Use `int[,]` storage (not `long[,]`) — halves the file size vs Q48.16 and is plenty precise for golf.

### File format

Write a single binary file: `<exportPath>/heightmap.bytes` where `<exportPath>` matches the existing convention:

```csharp
string exportRoot = Path.GetFullPath(Path.Combine(
    Application.dataPath, "..",
    "Tools/UHoleGeo/output/lomond-country-club/export"));
string exportPath = Path.Combine(exportRoot, $"hole-{holeNumber:D2}");
```

(If the current scene is `_Flat`, append `-flat` to the folder name — mirror the existing `BridgeExporter` pattern.)

**Binary layout** (little-endian, use `BinaryWriter`):

| Offset | Type | Content |
|---|---|---|
| 0 | 4 bytes | Magic: ASCII `"GHM1"` (Golfin HeightMap v1) |
| 4 | int32 | `version = 1` |
| 8 | int32 | `resolution` (e.g. 2049) |
| 12 | float32 | `sizeX` (world meters along X) |
| 16 | float32 | `sizeZ` (world meters along Z) |
| 20 | float32 | `posX` (terrain transform position X) |
| 24 | float32 | `posY` (terrain transform position Y) |
| 28 | float32 | `posZ` (terrain transform position Z) |
| 32 | int32 | `format = 1` (1 = Q16.16 int32) |
| 36 | resolution² × 4 bytes | heightmap data, row-major `[y, x]`, Q16.16 int32 |

Header is 36 bytes. For a 2049² Q16.16 heightmap: 36 + 2049×2049×4 ≈ **16.8 MB per hole**.

Header fields `posX/Y/Z` and `sizeX/Z` let Phase 4's reader map world coordinates to grid cells without requiring the Unity scene to be open. `posY` specifically lets the reader add terrain Y-offset to get absolute world heights.

### The bake loop

```csharp
var buffer = new int[res, res];
for (int y = 0; y < res; y++)
{
    for (int x = 0; x < res; x++)
    {
        // heights[y, x] is normalized [0, 1]; multiply by size.y to get world meters
        float worldM = heights[y, x] * size.y;
        buffer[y, x] = ToQ16_16(worldM);
    }
}
```

Then write header + buffer via `BinaryWriter`.

### Round-trip validation

After writing, immediately read back and sample 100 random grid points. Assert reconstructed value matches original world-meter height within 0.001m (1mm). Log mismatch count; if > 0, log error.

```csharp
// Re-open file, read header, read data, compare
int mismatches = 0;
var rng = new System.Random(42); // deterministic for logging
using (var fs = File.OpenRead(outPath))
using (var br = new BinaryReader(fs))
{
    // ... skip header, read into int[,] readBack ...
    for (int i = 0; i < 100; i++)
    {
        int y = rng.Next(res), x = rng.Next(res);
        float orig = heights[y, x] * size.y;
        float round = FromQ16_16(readBack[y, x]);
        if (Math.Abs(orig - round) > 0.001f) mismatches++;
    }
}
Debug.Log($"[PhysicsHeightmapBaker] Hole {holeNumber}: wrote {outPath} ({fileSizeMB:F1} MB), round-trip mismatches: {mismatches}/100");
```

### "Bake All Holes" loop

Iterate 1..18. For each:
1. Build scene path (`Hole_{N:D2}_Geo.unity`).
2. If the path doesn't exist, log and skip.
3. `EditorSceneManager.OpenScene(path, OpenSceneMode.Single);`
4. Call `BakeHole(n)`.
5. Don't save the scene (bake is pure-read, no modifications).

Use `EditorUtility.DisplayProgressBar("Baking Physics Heightmaps", $"Hole {n}/18", n / 18f)` between holes. Clear on finish. Wrap the whole thing in try/finally so the progress bar always clears.

At the end, log a summary: total holes baked, total bytes written, total mismatches across all holes.

### Unity-MCP autonomous validation

Claude Code: after writing the file, drive validation yourself via Unity-MCP — don't wait for Cesar to test manually.

1. **Compile check.** `console-get-logs` after `script-update-or-create` — confirm zero compile errors. If there are errors, fix them and re-check, up to 5 iterations. If still broken after 5, report with full log excerpts.

2. **Menu execution test.** With Hole_01_Geo (or whatever scene is currently active if it's a hole scene) open, execute the bake via `script-execute`:
   ```csharp
   // In a fresh C# snippet
   typeof(Golfin.CourseImport.PhysicsHeightmapBaker)
       .GetMethod("BakeHole", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
       .Invoke(null, new object[] { 1 });
   ```
   Check `console-get-logs` for the `[PhysicsHeightmapBaker] Hole 1: wrote ...` line. Confirm mismatches = 0.

3. **File existence check.** Use `filesystem`-equivalent or `script-execute` with `System.IO.File.Exists` to confirm `heightmap.bytes` was created at the expected path.

4. **File size sanity.** Size should be ~16.8 MB for a 2049² map. If it's < 1 MB or > 50 MB, something's wrong — flag it.

5. **Screenshot.** `screenshot-scene-view` after the bake — not for visual verification (bake is invisible), but as a record that the editor was operational. Attach to the done report.

### Known file-size concern — document, do not mitigate preemptively

18 holes × 16.8 MB = ~300 MB of heightmap data on disk. This is flagged in `PHYSICS_RESEARCH.md` Section 6 as a known risk. **Do not** implement downsampling or compression in this task — first we see actual sizes in practice. If after baking all 18 the total exceeds 400 MB or a single hole exceeds 25 MB, flag it in the done report and I'll spec mitigation (likely: downsample to 1025² or add zlib compression) as a follow-up task.

### DO NOT

- Modify `HoleGeoImporter.cs` or `HoleLiteImporter.cs` — this is a separate tool.
- Touch any terrain mesh, overlay, material, or existing importer code.
- Load or parse the baked file at runtime — that's Phase 4.
- Add the bake step to the existing import pipeline — it stays manual/menu-driven so we can iterate on the bake format independently.
- Use `long[,]` — use `int[,]` (Q16.16 in 32 bits is sufficient for golf).
- Add compression, downsampling, or "clever" encoding without asking.
- Save the scene after baking — it's a pure-read operation.

### Autonomous iteration budget

5 attempts max before reporting failure with diagnostics. A failure looks like: compile errors that can't be resolved, bake producing wrong file size, round-trip mismatches > 0 on any hole. Report with: the error, what was tried, the last few `console-get-logs` lines.

### Done report should include

- Path(s) written.
- File sizes (one per hole baked + total).
- Round-trip mismatch count per hole (expected: 0).
- Screenshot (scene view, any angle, just as operational evidence).
- Any anomalies: holes with no Geo scene file, holes where the Terrain component wasn't found, etc.

---

## History Log (completed tasks, most recent first)

- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — `PhysicsHeightmapBaker.cs` created. Menu items: Bake Current Hole / Bake Hole 01-18 / Bake All Holes. Q16.16 fixed-point, binary `heightmap.bytes` with GHM1 header. Hole 1 baked: 16.02 MB, 0/100 round-trip mismatches. File at `Tools/UHoleGeo/output/lomond-country-club/export/hole-01/heightmap.bytes`.

- ✅ **2026-04-20** Phase 2b water shore ablation — set `ShoreRadius=0`, confirmed serrations remain, eliminated ramp as cause (Hypothesis A), confirmed depression-cliff cause (Hypothesis B). `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp in `DepressTerrainUnderOverlays` (reverse chamfer from boundary inward, smoothstep surfaceNorm→waterFloorY over `ShoreRadius` cells). Fixed serrations on Hole 12 steep bank. Water mesh kept in original position; depression handles the boundary continuity.
- ✅ **2026-04-20** Hole Flyover Recorder — new `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs`. Three menu items under `Golfin/Recording/`. Play Mode state machine, `FlyoverCamera` with tag, 4-phase path (drone hover → zoom in → Catmull-Rom cruise → pin orbit), Unity Recorder 5.1.6 API, batch mode across 18 holes, SessionState persistence across domain reloads.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix — `minSpinePixels=20` filter was removing chain[4] (len=15), causing junction C to degrade to 2-way and B-C link to merge. Fix: rescue short chains (len≥`dsFactor*2=6`) whose endpoint touches a 2-way junction in longChains. Hole 1 now exports 10 cart paths (was 6).
- ✅ **2026-04-20** Cart path junction endpoint snapping (Unity) — `SnapCartPathJunctionEndpoints()` in `CreateSplineCartPaths`. 0.75m radius clusters endpoints at N-way junctions, snaps to centroid. Fixes grass wedges on Hole 1 middle junction.
- ✅ **2026-04-20** Linear-slope tee skirt — replaced fixed-radius smoothstep ramp with linear descent at `TeeMaxRampSlope=0.35 m/m`. Writes while `rampH_m > base_m`; terminates where ramp meets terrain. No fixed radius, no outer cliff, C¹-continuous. `TeeSkirtMeters` now unused.
- ❌ **2026-04-20 REVERTED** Per-edge adaptive tee skirt — stair-stepped every slope. Commit 6151e8d7 reverted at b7f70112. Approach abandoned in favor of linear-slope.
- ✅ **2026-04-20** Per-layer terrain tint pass inserted in `ApplySplatmap()` (both Geo and Lite importers). ⚠️ **REVERTED same day** — `diffuseRemapMax` on TerrainLayer had no visible effect. Root cause unknown; knob/render-path may differ. Code reverted to original. Revisit when someone has time to dig into TerrainLayer internals.
- ✅ **2026-04-19** Water Shore Phase 1 sampling — new `Tools/sample-shore-heights.js`. Course-wide max drop 14.07m (Hole 12 body 1), max `dR_needed` 34.7m. Recommended `ShoreMaxRadiusMeters` = 40m. Per-hole terrain dims from `terrain-meta.json`.
- ✅ **2026-04-18** Bridge Viewer in UHoleGeo — `dev-server.mjs` `/api/bridges` GET route + bridges loaded into hole nav data. `app.js`: `loadBridges()`, `worldToNormalized()`, purple rotated footprint + forward tick + anchor circles, `hitTestBridge()` + hover tooltip, "Bridges" layer toggle, bridge count chip in hole nav.
- ✅ **2026-04-18** Bridge Placement Tool (Unity) — `BridgeAnchor` (`Golfin.Course`) marker component with gizmo. `BridgeExporter` EditorWindow at `Window > Trees > Bridge Exporter`. Auto-detects Geo/Lite/Flat from scene name, writes `bridges.json` to UHoleGeo/UHoleLite export folder, mirrors to sibling pipeline.
- ✅ **2026-04-18** Tee border ring UV fix + geometric rebuild — constant V (0.5) eliminated texture twisting on the curved ring. Additionally rebuilt ring as manual quad-strip (outer contour × inset contour by vertex index) instead of CDT-classified triangles, eliminating long diagonal spanning tris. Submesh 0 = CDT surface, submesh 1 = clean N-quad strip.

---

## Reference Docs for Claude Code

- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan, Unity-MCP workflow notes (Section 6.5)
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers (carry distances, stat mappings, surface coefficients)
- `Docs/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe for fringe/border baked into parent mesh
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP (50+ tools reference: https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/default-mcp-tools.md)
