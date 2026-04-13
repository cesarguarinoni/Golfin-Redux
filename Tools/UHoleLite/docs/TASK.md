# TASK.md — Instructions for Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`

---

## Current Task — Taper Strip at T-Junction Endpoints (replaces pullback)

The pullback approach created GAPS instead of fixing overshoots. The
geometry is: at a ~90° T-junction the strip extends ±halfWidth
**perpendicular** to the approach direction (i.e. along the main path).
Pulling back along the approach just moves the strip away from the
junction. The correct fix is to **taper** the strip width to 0 at
snapped endpoints so it narrows to a point at the junction.

### Part 1 — Pipeline: Revert pullback, add `snapped_endpoints` flags

**File:** `Tools/UHoleLite/scripts/export-hole.mjs`

**Step 1: Remove the pullback block.** Find the section starting with
the comment `// --- Pull back snapped endpoints by halfWidth ---` and
delete the entire block (from that comment through its closing `}`).

**Step 2: Add `snapped_endpoints` flags.** After the orphan snapping
loop (after the "Snap orphan endpoints" `for` loop closes), add a pass
that detects which endpoints are near another spine's interior and
flags them in the cart path data:

```javascript
  // --- Flag snapped endpoints for Unity taper ---
  for (const cp of results) {
    if (!cp.spine || cp.spine.length < 2) continue;
    cp.snapped_endpoints = { start: false, end: false };

    for (const [label, ep] of [['start', cp.spine[0]], ['end', cp.spine[cp.spine.length - 1]]]) {
      for (const other of results) {
        if (other === cp) continue;
        if (cp.parent_region !== other.parent_region) continue;
        if (!other.spine || other.spine.length < 2) continue;

        // Check proximity to interior points (not endpoints) of other spine
        for (let si = 1; si < other.spine.length - 1; si++) {
          const dx = ep.x - other.spine[si].x;
          const dz = ep.z - other.spine[si].z;
          if (Math.sqrt(dx * dx + dz * dz) < minWidthM * 2) {
            cp.snapped_endpoints[label] = true;
            break;
          }
        }
        if (cp.snapped_endpoints[label]) break;
      }
    }
  }
```

This adds a `snapped_endpoints: { start: true/false, end: true/false }`
field to each cart path entry in cart-paths.json.

### Part 2 — Unity: Taper strip width at snapped endpoints

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

**Step 1: Add `snapped_endpoints` to the data model.**
In `HoleManifestData.cs`, find the `CartPathRegionData` class and add:

```csharp
public SnappedEndpoints snapped_endpoints;

[System.Serializable]
public class SnappedEndpoints
{
    public bool start;
    public bool end;
}
```

If `SnappedEndpoints` class already exists, skip this.

**Step 2: Modify `CreateSpineStripMesh` to accept taper flags.**
Change the method signature to add two booleans:

```csharp
private static GameObject CreateSpineStripMesh(
    int id, ContourPoint[] spine, float halfWidth,
    Terrain terrain, float terrainBaseY,
    Material mat, float tileSize,
    Golfin.Course.SurfaceType surfaceType,
    bool taperStart = false, bool taperEnd = false)
```

**Step 3: Apply taper in the vertex generation loop.**
Inside `CreateSpineStripMesh`, right after computing `halfWidth` for
each vertex (before computing lx/lz/rx/rz), add width tapering:

```csharp
// Taper width at snapped endpoints (narrow to 0 over last 3 points)
float localHalfWidth = halfWidth;
const int taperPoints = 3;
if (taperStart && i < taperPoints)
{
    float t = (float)i / taperPoints;
    localHalfWidth = halfWidth * t; // 0 at i=0, full at i=taperPoints
}
else if (taperEnd && i > n - 1 - taperPoints)
{
    float t = (float)(n - 1 - i) / taperPoints;
    localHalfWidth = halfWidth * t; // full at n-1-taperPoints, 0 at n-1
}
```

Then replace `halfWidth` with `localHalfWidth` in the left/right
position calculations:

```csharp
float lx = cx - px * localHalfWidth;
float lz = cz - pz * localHalfWidth;
float rx = cx + px * localHalfWidth;
float rz = cz + pz * localHalfWidth;
```

**Step 4: Pass the flags from the caller.**
In `CreateFlatZoneMeshes`, where `CreateSpineStripMesh` is called for
cart paths, pass the snapped_endpoints flags:

```csharp
bool taperStart = region.snapped_endpoints != null && region.snapped_endpoints.start;
bool taperEnd = region.snapped_endpoints != null && region.snapped_endpoints.end;
meshGO = CreateSpineStripMesh(
    region.id, region.spine, halfWidth,
    terrain, terrainBaseY, cpMat, 4f,
    Golfin.Course.SurfaceType.CartPath,
    taperStart, taperEnd);
```

### What NOT to Change

- `BuildSpinePolygon` — splatmap painting stays full width (it paints
  wider than the mesh anyway with the +0.2f margin)
- Terrain depression — uses `BuildSpinePolygon` which stays unchanged
- Chain merging or junction snapping logic
- `nudgeSpinesFromContours`

### Key Behavior

- **Snapped endpoints:** Strip tapers from full width to 0 over last
  3 spine points → forms a pointed tip that stops at the junction
  without overshooting
- **Free endpoints:** No taper — strip ends at full width (natural end)
- **Splatmap underneath:** Full width, covers the junction area with
  asphalt texture. The main path's strip mesh covers the gap visually
- **Overlap region:** The pointed tip slides under the main path's
  strip mesh (same material, same Y offset → invisible)

### Running

```bash
cd Tools/UHoleLite
node scripts/export-hole.mjs lomond-country-club 18
```

Then in Unity: GOLFIN > Import Hole (Lite) > Hole 18

### Verification

1. Export hole 18, import in Unity
2. Walk to CP#4 junctions — branch should taper to a point at the
   junction, no overshoot, no gap
3. Splatmap asphalt covers the junction area underneath the mesh
4. Run `--all` to verify no regressions on other holes
5. Free path endpoints (CP#1 end, CP#3 start/end) should NOT taper

---

## Completed Tasks
✅ 2026-04-13 — Taper strip at T-junction endpoints: snapped_endpoints flags in pipeline + localHalfWidth taper in Unity
✅ 2026-04-13 — Pull back snapped spine endpoints by halfWidth at T-junctions + spineExt→spine fix in Unity (REVERTED — caused gaps)
✅ 2026-04-13 — Distance-based residual ramp at play/non-play boundary (60-cell smoothstep transition)
