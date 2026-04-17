# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Flat Tee Platforms (Per-Polygon Absolute-Y Tees)

Real golf tees are built-up level pads with small mounds. Today our tee
meshes and the terrain under them follow the DEM, so tees tilt on sloped
ground. This task makes each `zones.tee[]` polygon its own flat absolute-Y
platform with a smooth terrain ramp (skirt) blending back to the natural
surrounding ground.

**Good news:** `zones.tee[]` is already an array of separate polygons —
holes with back/middle/forward tees at different elevations come through
as 2–6 distinct regions, each becoming its own flat platform at its own
height. No upstream pipeline work needed. Verified across all 18 holes
(Hole 1 = 3 regions, Hole 4 = 2, Hole 18 = 6, etc.).

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No pipeline changes.**
**No water / bunker / green / fairway behavior changes.**

---

### Why this has three parts

The import order in `ImportHoleInternal` is:

1. `CreateWaterMeshes` (uses `terrain.SampleHeight` on original DEM)
2. `CreateFlatZoneMeshes` (builds tee / fairway / cart meshes via
   `CDTTriangulate`, which samples terrain on original DEM)
3. `DepressTerrainUnderOverlays` (modifies `heights` for depression +
   shore ramp + new tee platforms)

Tee meshes are therefore built **before** the skirt ramp exists. If we
only flatten the mesh interior and rely on original terrain for the
border's outer vert Y, the border's outer edge floats above the
ramped terrain by up to the full overlay drop (0.4 m).

The existing fairway design sidesteps this via `DepressionInsetMeters
= 0.20 m` (terrain under the outermost 0.2 m of fairway stays at
original elevation, flush with the mesh edge). We can't use the same
trick for the tee — the whole point is a fully level platform, so the
tee polygon gets zero inset.

**Solution: three parts.**

- **Part A — Flat interior + skirt pass** inside
  `DepressTerrainUnderOverlays`. Per tee region: pick an absolute
  `teeY_world`, write the platform cells as absolute normalized
  heights, run a chamfer distance transform, smoothstep-lerp the skirt
  cells back to the original baseline.
- **Part B — Interior vert flattening** in `CreateTeeMeshWithBorder`
  via a new optional `platformY` parameter. Interior verts (inside
  the original tee contour) get `Y = platformY + yOffset`. Border
  verts are left as-is for now; Part C fixes them.
- **Part C — Post-depression border patch.** A new helper
  `PatchTeeMeshBorderVerts` runs after `DepressTerrainUnderOverlays`,
  re-samples terrain for each tee mesh's border outer verts (the
  ones outside the original contour), and rewrites their Y to match
  the now-ramped terrain. Needs a small bookkeeping dictionary so
  we can find the mesh + which verts are border verts per region.

---

### Part A — Depression pass: absolute-Y platform + terrain skirt

#### A.1 — Add constants near the other tuning constants

In the tuning constants region at the top of the class (near
`ShoreDepthMeters`, `TerrainYOffset`, `OverlayDepressionMeters`), add:

```csharp
/// <summary>Horizontal distance over which the tee platform skirt
/// ramps back to natural terrain.</summary>
private const float TeeSkirtMeters = 2.0f;

/// <summary>Populated by DepressTerrainUnderOverlays; consumed by
/// CreateFlatZoneMeshes so tee meshes flatten their interior verts
/// to match the platform Y used when reshaping the terrain.</summary>
private static System.Collections.Generic.Dictionary<int, float>
    _teePlatformYByRegionId;
```

#### A.2 — Remove tees from the shared `depress` mask

Find this block in `DepressTerrainUnderOverlays` (around line 3080):

```csharp
// Tee contours
string zcPath = Path.Combine(exportPath, "zone-contours.json");
if (File.Exists(zcPath))
{
    var data = JsonUtility.FromJson<ZoneContoursFile>(
        File.ReadAllText(zcPath));
    if (data.zones != null && data.zones.tee != null)
        foreach (var region in data.zones.tee)
            if (region.contour != null && region.contour.Length >= 3)
                MarkContourCells(region.contour, depress,
                    hRes, terrainPos, terrainSize);
}
```

**Delete this entire block.** Tees no longer participate in the shared
flat drop.

The fairway block immediately above stays unchanged.

#### A.3 — Add the tee platform + skirt pass

Insert this **after** the water floor apply block (after
`waterFloorCount` is incremented in the `if (hasWater)` block), and
**before** the shore ramp block that starts with
`if (hasWater && ShoreRadius > 0 && ShoreDepthMeters > 0f)`.

```csharp
// ─── Tee platform pass ────────────────────────────────────────
// Each zones.tee[i] polygon becomes a FLAT platform at its own
// absolute Y (median of terrain under it), with a smooth terrain
// skirt that ramps back to original terrain over TeeSkirtMeters.
//
// Real golf tees are built-up level pads. Without this, tees tilt
// on sloped ground because the tee mesh follows the DEM.
//
// Per-region independence: each tee polygon is processed
// independently, so multi-tee holes (Hole 1, 4, 18...) with back /
// middle / forward tees at different elevations each get their own
// correct height. Region id → Y is recorded in
// _teePlatformYByRegionId for the mesh pass + post-patch to pick up.
int teeFlatCount = 0;
int teeSkirtCount = 0;

// Reset the static bookkeeping each import (don't leak between holes).
_teePlatformYByRegionId = new System.Collections.Generic.Dictionary<int, float>();

{
    string teePath = Path.Combine(exportPath, "zone-contours.json");
    if (File.Exists(teePath))
    {
        var teeData = JsonUtility.FromJson<ZoneContoursFile>(
            File.ReadAllText(teePath));
        if (teeData.zones != null && teeData.zones.tee != null &&
            teeData.zones.tee.Length > 0)
        {
            float overlayDropNorm = OverlayDepressionMeters / elevRange;

            float metersPerCell =
                (terrainSize.x + terrainSize.z) * 0.5f / (hRes - 1);
            int skirtRadiusCells = Mathf.Max(1, Mathf.RoundToInt(
                TeeSkirtMeters / metersPerCell));

            // Capture pre-tee heights so all skirt lerps read from a
            // clean baseline (one tee's platform write never contaminates
            // another tee's skirt).
            float[,] baselineHeights = (float[,])heights.Clone();

            foreach (var region in teeData.zones.tee)
            {
                if (region.contour == null || region.contour.Length < 3)
                    continue;

                // Per-region mask (inset=0 so mesh + platform align exactly)
                bool[,] teeMask = new bool[hRes, hRes];
                MarkContourCells(region.contour, teeMask,
                    hRes, terrainPos, terrainSize, 0f);

                // Median terrain height under the tee, in normalized units
                var samples = new System.Collections.Generic.List<float>(256);
                for (int z = 0; z < hRes; z++)
                    for (int x = 0; x < hRes; x++)
                        if (teeMask[z, x])
                            samples.Add(baselineHeights[z, x]);
                if (samples.Count == 0) continue;

                samples.Sort();
                float teeHeightNorm = samples[samples.Count / 2];
                float teeY_world = terrainPos.y + teeHeightNorm * elevRange;

                _teePlatformYByRegionId[region.id] = teeY_world;

                // Platform target = teeY − overlayDrop (mesh sits 0.4m above
                // for z-fight avoidance, same as fairway convention).
                float platformNorm = Mathf.Clamp01(
                    teeHeightNorm - overlayDropNorm);

                // Write platform cells
                for (int z = 0; z < hRes; z++)
                    for (int x = 0; x < hRes; x++)
                        if (teeMask[z, x])
                        {
                            heights[z, x] = platformNorm;
                            teeFlatCount++;
                        }

                // Chamfer distance transform from this tee's boundary outward
                float[,] dist = new float[hRes, hRes];
                for (int z = 0; z < hRes; z++)
                    for (int x = 0; x < hRes; x++)
                        dist[z, x] = teeMask[z, x] ? 0f : float.MaxValue;

                // Forward pass
                for (int z = 0; z < hRes; z++)
                    for (int x = 0; x < hRes; x++)
                    {
                        if (x > 0)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z, x - 1] + 1f);
                        if (z > 0)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z - 1, x] + 1f);
                        if (x > 0 && z > 0)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z - 1, x - 1] + 1.414f);
                        if (x < hRes - 1 && z > 0)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z - 1, x + 1] + 1.414f);
                    }
                // Backward pass
                for (int z = hRes - 1; z >= 0; z--)
                    for (int x = hRes - 1; x >= 0; x--)
                    {
                        if (x < hRes - 1)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z, x + 1] + 1f);
                        if (z < hRes - 1)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z + 1, x] + 1f);
                        if (x < hRes - 1 && z < hRes - 1)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z + 1, x + 1] + 1.414f);
                        if (x > 0 && z < hRes - 1)
                            dist[z, x] = Mathf.Min(dist[z, x], dist[z + 1, x - 1] + 1.414f);
                    }

                // Apply skirt: smoothstep lerp from platformNorm at the tee
                // edge to baselineHeights[z,x] at skirtRadiusCells.
                // Skip cells already claimed by fairway/cart/water — those
                // have their own intended heights; skirts should only
                // affect untouched terrain.
                for (int z = 0; z < hRes; z++)
                {
                    for (int x = 0; x < hRes; x++)
                    {
                        if (teeMask[z, x]) continue;
                        if (depress[z, x]) continue;       // fairway/green
                        if (cartDepress[z, x]) continue;   // cart path
                        if (waterMask[z, x]) continue;     // water

                        float d = dist[z, x];
                        if (d <= 0f || d > skirtRadiusCells) continue;

                        float t = d / skirtRadiusCells; // 0 at edge, 1 at radius
                        t = t * t * (3f - 2f * t);      // smoothstep
                        float originalNorm = baselineHeights[z, x];
                        float target = Mathf.Lerp(
                            platformNorm, originalNorm, t);

                        // Take min so overlapping skirts on multi-tee holes
                        // never raise a cell above a neighboring platform.
                        if (target < heights[z, x])
                        {
                            heights[z, x] = Mathf.Max(0f, target);
                            teeSkirtCount++;
                        }
                    }
                }
            }
        }
    }
}
```

#### A.4 — Update the final log line

Existing (line ~3318):

```csharp
Debug.Log($"[HoleGeoImporter] Terrain depression: {depressedCount}" +
          $" cells lowered by {OverlayDepressionMeters:F2}m" +
          $" (cart path: {cartDepressedCount} cells," +
          $" water floor: {waterFloorCount} cells flattened," +
          $" water shore ramp: {shoreCount} cells)");
```

Replace with:

```csharp
Debug.Log($"[HoleGeoImporter] Terrain depression: {depressedCount}" +
          $" cells lowered by {OverlayDepressionMeters:F2}m" +
          $" (cart path: {cartDepressedCount} cells," +
          $" water floor: {waterFloorCount} cells flattened," +
          $" water shore ramp: {shoreCount} cells," +
          $" tee platforms: {teeFlatCount} cells flattened," +
          $" tee skirts: {teeSkirtCount} cells ramped)");
```

---

### Part B — Flatten interior verts in `CreateTeeMeshWithBorder`

#### B.1 — Add `platformY` parameter

Current signature (line 4026):

```csharp
private static GameObject CreateTeeMeshWithBorder(int id, string zoneName,
    ContourPoint[] contour, Terrain terrain, float terrainBaseY,
    Material mat, float tileSize,
    Material borderMat, float borderWidth, float borderTileSize,
    Golfin.Course.SurfaceType surfaceType)
```

Add a trailing optional `float? platformY = null`:

```csharp
private static GameObject CreateTeeMeshWithBorder(int id, string zoneName,
    ContourPoint[] contour, Terrain terrain, float terrainBaseY,
    Material mat, float tileSize,
    Material borderMat, float borderWidth, float borderTileSize,
    Golfin.Course.SurfaceType surfaceType,
    float? platformY = null)
```

#### B.2 — Flatten interior vert Y

The function classifies triangles by centroid at line 4089 using
`IsInsideContour(triCx, triCz, originalPoly)`, then remaps border
verts starting at line 4100.

Vert layout in `finalVerts`:
- Indices `0 .. rawVerts.Length - 1` — verts used by tee tris (interior
  + original-contour boundary). In world space, `(x, z)` position.
- Indices `rawVerts.Length .. finalVerts.Count - 1` — duplicated
  border verts (tail), used by border tris. A boundary vert on the
  original contour appears in BOTH groups (original index for the
  tee side, duplicated index for the border side).

**Insert after line 4120** (after `var vertsArr = finalVerts.ToArray();`)
and **before** the centroid subtraction loop (the loop at line 4121
that subtracts `centroid` from each vert):

```csharp
// Flatten interior vert Y to the platform Y. Operate in WORLD space
// (before centroid subtraction) so we can apply an absolute target.
// Verts outside the original contour (dilated border ring proper) are
// left on their CDT-sampled Y; Part C patches them after depression.
//
// A boundary vert on the original contour is technically on the edge,
// but IsInsideContour returns a consistent inside/outside answer for
// both its original index AND its duplicated border-remapped index
// (same (x,z) → same classification), so the seam remains watertight.
if (platformY.HasValue)
{
    float flatY = platformY.Value + yOffset;
    for (int i = 0; i < vertsArr.Length; i++)
    {
        if (IsInsideContour(vertsArr[i].x, vertsArr[i].z, originalPoly))
            vertsArr[i].y = flatY;
    }
}
```

Then the existing centroid subtraction loop (line 4121–4122) runs
unchanged, and the mesh is assembled as today.

#### B.3 — Update the call site in `CreateFlatZoneMeshes`

Current call (around line 3622):

```csharp
var meshGO = CreateTeeMeshWithBorder(
    region.id, "Tee", region.contour,
    terrain, terrainBaseY,
    teeMat, 3f,
    teeBorderMat, 0.5f, 3f,
    Golfin.Course.SurfaceType.Tee);
```

Update to pass the platform Y (if known) — and while we're here, also
record the mesh + interior-vert-index set into a new static dictionary
for Part C's post-patch:

```csharp
float? platformY = null;
if (_teePlatformYByRegionId != null &&
    _teePlatformYByRegionId.TryGetValue(region.id, out var py))
    platformY = py;

var meshGO = CreateTeeMeshWithBorder(
    region.id, "Tee", region.contour,
    terrain, terrainBaseY,
    teeMat, 3f,
    teeBorderMat, 0.5f, 3f,
    Golfin.Course.SurfaceType.Tee,
    platformY);
if (meshGO != null)
{
    meshGO.transform.SetParent(teeRoot.transform);

    // Register for post-depression border patch (Part C)
    var mf = meshGO.GetComponent<MeshFilter>();
    if (mf != null && mf.sharedMesh != null)
    {
        _teeMeshRegistryByRegionId[region.id] = new TeeMeshRegistration
        {
            meshFilter = mf,
            contour = region.contour,
            meshCentroidWorld = meshGO.transform.position,
        };
    }
}
```

**Note:** `_teePlatformYByRegionId` is populated by
`DepressTerrainUnderOverlays`, which runs AFTER
`CreateFlatZoneMeshes`. So on a normal import the dictionary is
empty when we get here, and `platformY` is null → interior verts use
the old CDT-sampled Y (tilted). That's fine: Part C's post-patch
re-runs the interior flatten using the now-computed platform Y, so
the final result is correct.

**Rather than flattening twice, we flatten only in Part C and skip
Part B.2's flatten path** — except that Part B.2 is also needed for a
future world where we reorder the pipeline to run depression first.
Keep B.2 as-is; it's a no-op on normal runs but costs ~nothing.

Actually to keep this simple and avoid confusion: **omit Part B.2
entirely**. Keep only the `platformY` parameter addition (B.1) and
the call site wiring (B.3). The interior flatten happens in Part C.
If we ever reorder, we reintroduce B.2.

**Revised instruction: skip Part B.2.** Claude Code, do Part B.1
(signature) and Part B.3 (call site + registry), and implement the
interior flatten in Part C only.

Leaving Part B.2 above as documentation of why the parameter exists.

---

### Part C — Post-depression border patch

#### C.1 — Add the registry types

Near the `_teePlatformYByRegionId` dictionary in the tuning constants
region (A.1), also add:

```csharp
private struct TeeMeshRegistration
{
    public MeshFilter meshFilter;
    public ContourPoint[] contour;   // original (non-dilated) contour
    public Vector3 meshCentroidWorld; // mesh GO world position
}

private static System.Collections.Generic.Dictionary<int, TeeMeshRegistration>
    _teeMeshRegistryByRegionId;
```

And reset it at the start of `DepressTerrainUnderOverlays` alongside
`_teePlatformYByRegionId`:

```csharp
_teePlatformYByRegionId = new System.Collections.Generic.Dictionary<int, float>();
```

**Wait** — we need the registry populated BEFORE
`DepressTerrainUnderOverlays` runs (by `CreateFlatZoneMeshes`) and
consumed AFTER it (by the new `PatchTeeMeshBorderVerts`). So the
registry must be initialized at a point both passes agree on.

Simplest: initialize `_teeMeshRegistryByRegionId` at the TOP of
`CreateFlatZoneMeshes` (it's the first producer). Then `PatchTee...`
reads it.

```csharp
// At the top of CreateFlatZoneMeshes, before the tee region loop:
_teeMeshRegistryByRegionId =
    new System.Collections.Generic.Dictionary<int, TeeMeshRegistration>();
```

#### C.2 — Implement `PatchTeeMeshBorderVerts`

Add this new static helper in the same class, near
`DepressTerrainUnderOverlays`:

```csharp
/// <summary>
/// After DepressTerrainUnderOverlays has reshaped the terrain with flat
/// tee platforms + skirt ramps, re-sample terrain for each tee mesh and:
///   1. Flatten interior verts (inside the original contour) to the
///      platform Y. This is a "second chance" at the interior flatten
///      that Part B could have done earlier — doing it here means the
///      interior flatten is driven by the same data that reshaped the
///      terrain, which is less error-prone.
///   2. Resample terrain for border outer verts (outside the original
///      contour) so the mesh border sits flush on the now-ramped terrain.
/// Operates directly on Mesh.vertices. Expects mesh-local coordinates
/// (centered around mesh GO position).
/// </summary>
private static void PatchTeeMeshBorderVerts(
    GameObject terrainGO)
{
    if (_teeMeshRegistryByRegionId == null ||
        _teeMeshRegistryByRegionId.Count == 0)
        return;

    var terrain = terrainGO.GetComponent<Terrain>();
    if (terrain == null) return;
    float terrainBaseY = terrainGO.transform.position.y;
    const float yOffset = 0.02f; // must match CreateTeeMeshWithBorder

    int patchedInteriorCount = 0;
    int patchedBorderCount = 0;

    foreach (var kv in _teeMeshRegistryByRegionId)
    {
        int regionId = kv.Key;
        var reg = kv.Value;
        var mf = reg.meshFilter;
        if (mf == null || mf.sharedMesh == null) continue;

        // Original contour as a Vector2[] for IsInsideContour
        int nc = reg.contour.Length;
        var originalPoly = new Vector2[nc];
        for (int i = 0; i < nc; i++)
            originalPoly[i] = new Vector2(reg.contour[i].x, reg.contour[i].z);

        var mesh = mf.sharedMesh;
        var verts = mesh.vertices; // in mesh-local space

        // Resolve this region's platform Y (world). If the dictionary is
        // missing an entry (shouldn't happen for a valid tee), fall back
        // to median-sample from the current terrain.
        float platformY_world;
        if (_teePlatformYByRegionId == null ||
            !_teePlatformYByRegionId.TryGetValue(regionId, out platformY_world))
        {
            // Fallback: sample terrain at the mesh GO position
            platformY_world = terrainBaseY +
                terrain.SampleHeight(reg.meshCentroidWorld);
        }

        for (int i = 0; i < verts.Length; i++)
        {
            // Convert mesh-local (x, z) to world (x, z) for contour test
            float wx = verts[i].x + reg.meshCentroidWorld.x;
            float wz = verts[i].z + reg.meshCentroidWorld.z;

            bool inside = IsInsideContour(wx, wz, originalPoly);
            if (inside)
            {
                // Interior vert — flatten to platformY (relative to mesh GO)
                float newLocalY = (platformY_world + yOffset)
                    - reg.meshCentroidWorld.y;
                verts[i].y = newLocalY;
                patchedInteriorCount++;
            }
            else
            {
                // Border outer vert — re-sample terrain at current shape
                float terrH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                float newWorldY = terrainBaseY + terrH + yOffset;
                verts[i].y = newWorldY - reg.meshCentroidWorld.y;
                patchedBorderCount++;
            }
        }

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // Refresh the collider too (AddCleanMeshCollider baked from the
        // original mesh).
        var mc = mf.GetComponent<MeshCollider>();
        if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = mesh; }
    }

    Debug.Log($"[HoleGeoImporter] Patched tee meshes: " +
              $"{patchedInteriorCount} interior verts flattened, " +
              $"{patchedBorderCount} border verts resampled " +
              $"across {_teeMeshRegistryByRegionId.Count} tees.");
}
```

#### C.3 — Call `PatchTeeMeshBorderVerts` from `ImportHoleInternal`

In `ImportHoleInternal`, find the line:

```csharp
// Depress terrain under overlay meshes to prevent z-fighting
DepressTerrainUnderOverlays(terrainData, terrainGO, exportPath);
```

Add immediately after:

```csharp
// Patch tee meshes now that the terrain has been reshaped with flat
// platforms + skirt ramps. Interior verts flatten to platform Y;
// border outer verts re-sample the newly-ramped terrain so the
// collar sits flush.
PatchTeeMeshBorderVerts(terrainGO);
```

---

### Execution order (for Claude Code's benefit)

Do these in order; they have dependencies.

1. **A.1** — constants (`TeeSkirtMeters`, `_teePlatformYByRegionId`),
   then **C.1** constants (`TeeMeshRegistration`,
   `_teeMeshRegistryByRegionId`) next to them.
2. **A.2** — remove tee from shared `depress` mask.
3. **A.3** — add platform+skirt pass in `DepressTerrainUnderOverlays`.
4. **A.4** — update the final log line.
5. **C.2** — implement `PatchTeeMeshBorderVerts`.
6. **C.3** — call `PatchTeeMeshBorderVerts` in `ImportHoleInternal`.
7. **B.1** — add `platformY` parameter to `CreateTeeMeshWithBorder`.
8. **B.3** — update call site in `CreateFlatZoneMeshes`: initialize
   `_teeMeshRegistryByRegionId` at the top of the tee mesh section,
   pass `platformY` (will be null on first pass), and register the
   mesh into the dictionary.

(We're skipping the original Part B.2 flatten loop — the interior
flatten happens in `PatchTeeMeshBorderVerts` instead.)

---

### Verification

Re-import stress-test holes:

- [ ] **Hole 4** — big pro tee and small forward tee at different
      elevations. Expected: two flat platforms at different heights,
      each with its own skirt mound. Walking either should feel level.
- [ ] **Hole 1** — 3 tees, one very big (43×22 m), two small (7×10,
      7×8). Small tees should be visibly raised above the surrounding
      rough on a slope.
- [ ] **Hole 7** — a tee is near the water. Skirt skip logic must not
      touch water cells; no cliff between tee skirt and shore ramp.
- [ ] **Hole 18** — 6 small tees, all similar size. All flat, no
      weird interactions between adjacent skirts.

Visual checks for the border patch:
- [ ] Tee collar (dark border ring) sits **flush** on the skirt ramp,
      no visible gap or floating mesh edge.
- [ ] Collar's inner edge sits on the flat tee surface (no cliff at
      the junction).
- [ ] No z-fighting at either collar edge.

Regression:
- [ ] Fairways, greens, bunkers, cart paths unchanged.
- [ ] Water shore ramp still smooth (Hole 7, 12).
- [ ] Tee anchor markers land on the tee platform surface (they
      sample terrain after depression; should auto-land on the
      flat platform).
- [ ] Trees not placed inside tee platforms (TreePlacer excludes
      overlay zones; verify on a hole with trees near tees).
- [ ] Debug.Log shows non-zero `tee platforms` and `tee skirts`
      counts on holes with tees, plus `Patched tee meshes` line.

Tuning if needed:
- If mounds look **too steep**: raise `TeeSkirtMeters` to 3.0 or 4.0.
- If mounds look **too gradual**: lower `TeeSkirtMeters` to 1.2 or 1.5.
- If **adjacent tees' skirts interfere** unpleasantly on Hole 18:
  the `min of candidate ramp heights` logic should handle it, but if
  it looks wrong, flag as follow-up task (joint distance transform
  across the union of all tee cells).

---

---

✅ DONE: 2026-04-17 — Implemented flat tee platforms (Parts A+B+C) in HoleGeoImporter.cs. Tees removed from shared depress mask; each tee region gets absolute-Y platform + 2m smoothstep skirt; PatchTeeMeshBorderVerts flattens interior verts and resamples border verts post-depression. Committed eeec47e0 and pushed.

---

### Do NOT change

- `CreateFlatZoneMeshes`'s overall structure — only the tee call
  site (add `platformY` arg, register into dictionary).
- `CreateTeeMeshWithBorder`'s triangle classification, border UV
  gradient, centroid subtraction, submesh split, or materials.
  **Only** the new optional parameter is added.
- Green, fairway, bunker, water, cart path passes.
- Zone-contours schema, UHoleGeo pipeline, `zone-contours.json`.
- `MarkContourCells` signature.
- `OverlayDepressionMeters = 0.40f` — reused as the tee-mesh-to-platform
  step. Same convention as fairway.
- Anchor marker placement. They sample terrain post-depression and
  auto-land on the new flat platform — no change needed.
- Tree placement. Tree exclusion polygons already include tee contours.
- `IsInsideContour`, `DistanceSqToContour`, `DilateContour`,
  `CDTTriangulate`, `AddCleanMeshCollider` — all callers use the
  same signatures.

---

### Design notes (for future me / follow-ups)

- **Post-patch vs reorder:** The cleaner architecture would be to
  run `DepressTerrainUnderOverlays` before the mesh-building passes
  so meshes sample the final terrain. That requires refactoring
  `CreateWaterMeshes` to precompute `minTerrainH` from the original
  heights array (it currently uses `terrain.SampleHeight` which would
  return the ramped value). Deferred — post-patch solves the tee
  problem without disturbing water.
- **Boundary vert consistency:** A vert on the original contour has
  an "interior" copy (tee tris) AND a "border" copy (remapped). Both
  test the same way through `IsInsideContour` (same `(x,z)`). If one
  says inside, the other says inside → both get flattened → seam is
  watertight. If the point-in-polygon test is ambiguous on the edge,
  the result is still consistent between copies.
- **Median over mean:** Robust to stray contour-edge cells that may
  be one heightmap cell inside the polygon but actually belong to
  fairway/rough in intent.
- **baselineHeights clone:** Prevents one tee's platform write from
  corrupting another tee's skirt lerp input. Cheap (~16 MB for 2049²).

---

