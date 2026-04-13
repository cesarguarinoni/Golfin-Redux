# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Taper Strip at T-Junction Endpoints

Full spec is in `Tools/UHoleLite/docs/TASK.md` (both pipeline + Unity
changes are there). Summary:

### Unity side (this file's scope):

1. **Data model:** Add `snapped_endpoints` to `CartPathRegionData` in
   `HoleManifestData.cs` (or wherever that class is defined):
   ```csharp
   public SnappedEndpoints snapped_endpoints;
   [System.Serializable]
   public class SnappedEndpoints { public bool start; public bool end; }
   ```

2. **Taper in CreateSpineStripMesh:** Add `taperStart`/`taperEnd`
   params. In the vertex loop, taper `halfWidth` to 0 over last 3
   points at flagged endpoints. Use `localHalfWidth` for lx/lz/rx/rz.

3. **Caller:** Pass `region.snapped_endpoints.start/end` when calling
   `CreateSpineStripMesh` from `CreateFlatZoneMeshes`.

See TASK.md for exact code snippets.

### Pipeline side:
Revert the pullback block and add `snapped_endpoints` flags to
cart-paths.json. See TASK.md for details.

### Do NOT change:
- `BuildSpinePolygon` (splatmap painting stays full width)
- Terrain depression logic
- Chain merging / junction snapping

---

## Completed Tasks
✅ 2026-04-13 — Taper strip at T-junction endpoints (replaces pullback)
✅ 2026-04-13 — spineExt→spine fix in CreateSpineStripMesh
✅ 2026-04-13 — Node.js residual ramp (60-cell smoothstep) + Unity-side boundary height propagation
✅ 2026-04-13 — Cart path depression: 3-strategy fix
✅ 2026-04-13 — Natural OB↔Rough transition
✅ 2026-04-13 — "Smooth OB" button in UHole Lite
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
