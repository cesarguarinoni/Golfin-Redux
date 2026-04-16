# Cart Path Spline Plan

## Problem

Cart paths break on inclinations and look blocky. Root causes:

1. **Spine extracted from pixel skeleton** — staircase artifacts from
   zone grid (~2000px). RDP smoothing can't fully fix a zigzag source.
2. **Coarse vertex spacing** — terrain samples too far apart on slopes,
   mesh cuts through terrain or floats above it.
3. **Width from zone grid** — varies jaggedly pixel-to-pixel.

## Industry Standard

Professional golf games (PerfectParallel/TGC series) and Unity road
tools (EasyRoads3D, Road Architect) all use the same approach:
**spline-based mesh extrusion** with dense terrain-height sampling.

Unity's built-in **Splines package** (com.unity.splines, v2.8+)
provides the core infrastructure: `SplineContainer`, `BezierKnot`,
`SplineUtility.Evaluate()`. Compatible with Unity 6.

## Approach

### What changes

**Export side (Node.js — minimal change):**
Add a `spine` array to `cart-paths.json` alongside the existing
`contour`. The spine is the centerline points we already extract
(from `extractPathSpine`). Currently only used internally for the
strip mesh — now we export it explicitly.

Format:
```json
{
  "spine": [
    { "x": 10.5, "z": -20.3 },
    { "x": 15.2, "z": -25.1 },
    ...
  ]
}
```

**Import side (Unity — main change):**
Replace `CreateSpineStripMesh()` with a new `CreateSplineCartPath()`
that:

1. Creates a `SplineContainer` with `BezierKnot` at each spine point
2. Sets knot tangents to `TangentMode.AutoSmooth` — Bézier curves
   handle the smoothing (no RDP, no Chaikin needed)
3. Evaluates the spline at dense intervals (every 0.5m along the
   path) using `SplineUtility.Evaluate()`
4. At each sample point: offset left/right by `width_m / 2` to get
   strip vertices, sample `terrain.SampleHeight()` for Y
5. Build triangle strip mesh from the vertex pairs
6. Apply cart path material + SurfaceMarker + collider

### Why this is better

| Aspect | Current (spine strip) | Spline-based |
|--------|----------------------|--------------|
| Smoothness | Polyline from pixel skeleton | Bézier curves, inherently smooth |
| Slope conformance | Samples at skeleton intervals (~5-10m) | Samples every 0.5m along spline |
| Width | From zone grid (jagged) | Fixed parameter (clean edges) |
| Blocky corners | RDP + Chaikin on polyline | Catmull-Rom/Bézier auto-tangents |
| Debug visibility | Invisible centerline | Spline visible in Scene view |
| Branches | Separate spine per branch | Separate Spline per branch |

### What stays the same

- Export pipeline structure (`cart-paths.json`)
- Contour data (still exported, used for splatmap painting + depression)
- Depression system (uses contour, not spine)
- Splatmap painting (uses contour polygon)
- Cart path material
- SurfaceMarker component

## Package Dependency

Add to `Packages/manifest.json`:
```json
"com.unity.splines": "2.8.0"
```

Unity 6 (6000.3.9) fully supports Splines 2.8.

## API Usage

```csharp
using UnityEngine.Splines;
using Unity.Mathematics;

// Create spline from exported spine points
var splineGO = new GameObject("CartPath_Spline_1");
var container = splineGO.AddComponent<SplineContainer>();
var spline = container.AddSpline();

var knots = new BezierKnot[spinePoints.Length];
for (int i = 0; i < spinePoints.Length; i++)
{
    // Spine points are in local coords (same as contour)
    // Apply 90° CCW rotation: (x, z) → (z, x)
    float wx = spinePoints[i].z;
    float wz = spinePoints[i].x;
    float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
    knots[i] = new BezierKnot(
        new float3(wx, terrainBaseY + terrainH, wz));
}
spline.Knots = knots;

// Auto-smooth tangents for Catmull-Rom-like curves
for (int i = 0; i < spline.Count; i++)
    spline.SetTangentMode(i, TangentMode.AutoSmooth);

// Evaluate at dense intervals and build strip mesh
float splineLength = SplineUtility.CalculateLength(spline);
int sampleCount = Mathf.Max(2, Mathf.CeilToInt(splineLength / 0.5f));
float halfWidth = widthM / 2f;

var leftVerts = new List<Vector3>();
var rightVerts = new List<Vector3>();

for (int s = 0; s <= sampleCount; s++)
{
    float t = (float)s / sampleCount;
    SplineUtility.Evaluate(spline, t, out float3 pos,
        out float3 tangent, out float3 up);

    float3 right = math.normalize(math.cross(
        math.normalize(tangent), new float3(0, 1, 0)));

    float3 leftPos = pos - right * halfWidth;
    float3 rightPos = pos + right * halfWidth;

    // Re-sample terrain height at each edge vertex
    float leftH = terrain.SampleHeight(
        new Vector3(leftPos.x, 0, leftPos.z));
    float rightH = terrain.SampleHeight(
        new Vector3(rightPos.x, 0, rightPos.z));

    leftVerts.Add(new Vector3(leftPos.x,
        terrainBaseY + leftH + 0.01f, leftPos.z));
    rightVerts.Add(new Vector3(rightPos.x,
        terrainBaseY + rightH + 0.01f, rightPos.z));
}

// Build mesh from vertex pairs (standard triangle strip)
```

## Verification

- [ ] Cart paths follow smooth curves (no staircase edges)
- [ ] Cart paths conform to terrain slopes (no floating/sinking)
- [ ] Consistent width along entire path
- [ ] Spline visible in Scene view for debugging
- [ ] Depression still works (uses contour, unchanged)
- [ ] Splatmap still paints under cart path (uses contour, unchanged)
- [ ] All 18 holes import without errors
- [ ] Cart path branches handled as separate splines

## Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Splines package adds bloat | ~2MB, no runtime overhead (editor-only for us) |
| API changes in future Unity | Splines is Unity's official package, stable API |
| Spine data missing from export | Add `spine` to export; fallback to contour centroid-fan if missing |
| Performance at import time | SplineUtility.Evaluate is O(1) per sample, negligible |
