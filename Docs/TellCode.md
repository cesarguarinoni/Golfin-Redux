# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Water Shader: Replace URP/Lit with URPWater/Standard

**Goal:** Replace the flat opaque `URP/Lit` water material with the
real `URPWater/Standard` shader that's already in the project. This
gives us animated normals, depth-based coloring, edge fade, and
reflections — proper golf pond water instead of a blue slab.

Only `CreateWaterMaterial()` changes. The water mesh geometry,
positioning, shore slope, and everything else stays exactly as-is.

### Prerequisites

Before running the importer, ensure the URP Renderer Asset has:
- **Depth Texture: ON**
- **Opaque Texture: ON**

These are required for the URPWater shader's depth-based effects
(edge fade, depth coloring, refraction). Without them, water renders
but looks flat/broken.

The renderer asset is likely at:
`Assets/Settings/UniversalRenderPipelineAsset_Renderer.asset`
or similar. Search for `UniversalRenderPipelineAsset` if needed.
Set `m_RequireDepthTexture: 1` and `m_RequireOpaqueTexture: 1`
programmatically, or just flag it in the console log so Cesar
can toggle it in the Inspector.

### Part 1 — Replace `CreateWaterMaterial()` in HoleLiteImporter.cs

Find the existing method:

```csharp
private static Material CreateWaterMaterial(string dataDir)
{
    string matPath = $"{dataDir}/WaterSurface.mat";
    var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (existingMat != null)
        AssetDatabase.DeleteAsset(matPath);

    var mat = new Material(GetLitShader());
    mat.name = "WaterSurface";
    mat.color = new Color(0.18f, 0.40f, 0.58f);  // dark water blue
    mat.SetFloat("_Smoothness", 0.85f);
    mat.SetFloat("_Metallic", 0.05f);

    // Opaque — no alpha clip needed anymore
    mat.SetFloat("_Surface", 0);
    mat.SetFloat("_AlphaClip", 0);

    AssetDatabase.CreateAsset(mat, matPath);
    return mat;
}
```

Replace with:

```csharp
private static Material CreateWaterMaterial(string dataDir)
{
    string matPath = $"{dataDir}/WaterSurface.mat";
    var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (existingMat != null)
        AssetDatabase.DeleteAsset(matPath);

    // Use the URPWater shader (already in project)
    var waterShader = Shader.Find("URPWater/Standard");
    if (waterShader == null)
    {
        Debug.LogWarning("[HoleLiteImporter] URPWater/Standard shader not found! " +
                         "Falling back to URP/Lit. Check Assets/Art/3D/Props/URPWater/");
        var fallback = new Material(GetLitShader());
        fallback.name = "WaterSurface";
        fallback.color = new Color(0.18f, 0.40f, 0.58f);
        fallback.SetFloat("_Smoothness", 0.85f);
        AssetDatabase.CreateAsset(fallback, matPath);
        return fallback;
    }

    var mat = new Material(waterShader);
    mat.name = "WaterSurface";

    // ── Render queue: transparent (required by URPWater) ──
    mat.renderQueue = 3000;

    // ── Color mode: Colors (not gradient — simpler, cheaper) ──
    mat.SetFloat("_ColorMode", 0);
    mat.EnableKeyword("_COLORMODE_COLORS");

    // Shallow water color (teal-blue, typical golf pond)
    mat.SetColor("_Color", new Color(0.15f, 0.55f, 0.65f, 1f));
    // Deep water color (dark blue-green)
    mat.SetColor("_DepthColor", new Color(0.02f, 0.12f, 0.20f, 1f));
    // Underwater tint
    mat.SetColor("_UnderWaterColor", new Color(0.1f, 0.2f, 0.25f, 0.5f));

    // Depth range (how quickly color transitions from shallow→deep)
    mat.SetFloat("_DepthStart", 0.1f);
    mat.SetFloat("_DepthEnd", 2.0f);

    // Refraction distortion (subtle — it's a pond, not a river)
    mat.SetFloat("_Distortion", 16f);

    // Specular
    mat.SetFloat("_Smoothness", 0.7f);
    mat.SetColor("_SpecColor", new Color(0.9f, 0.9f, 0.9f, 1f));

    // ── Normal map: Single mode (cheapest, one scrolling normal) ──
    mat.SetFloat("_NormalsMode", 0);
    mat.EnableKeyword("_NORMALSMODE_SINGLE");

    // Use the water normal map included with the package
    var waterNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
        "Assets/Art/3D/Props/URPWater/Demo/Textures/Water/T_Water_03_N.tga");
    if (waterNormal != null)
    {
        mat.SetTexture("_NormalMapA", waterNormal);
        // Tiling: (tilingX, tilingY, offsetX, offsetY)
        // Moderate tiling for pond-scale — not too fine, not too coarse
        mat.SetVector("_NormalMapATilings", new Vector4(2f, 2f, 0f, 0f));
        // Speed: slow gentle ripples (X speed, Y speed, X speed2, Y speed2)
        mat.SetVector("_NormalMapASpeeds", new Vector4(0.3f, 0.2f, 0.15f, 0.1f));
        mat.SetFloat("_NormalMapAIntensity", 0.6f);
    }
    else
    {
        Debug.LogWarning("[HoleLiteImporter] T_Water_03_N.tga not found! " +
                         "Water will lack surface ripples.");
    }

    // ── Edge fade: ON (softens where water meets shore) ──
    mat.SetFloat("_EdgeFade", 1);
    mat.EnableKeyword("_EDGEFADE_ON");
    mat.SetFloat("_EdgeSize", 0.5f); // fade over 0.5m at edges

    // ── Foam: OFF (keep it simple for now — can enable in polish pass) ──
    mat.SetFloat("_Foam", 0);
    // Don't enable foam keywords

    // ── Caustics: OFF (needs underwater geometry, not worth it yet) ──
    mat.SetFloat("_Caustics", 0);

    // ── Scattering: OFF ──
    mat.SetFloat("_Scattering", 0);

    // ── Reflections: Probes (uses URP reflection probes, low cost) ──
    mat.SetFloat("_ReflectionMode", 2); // 0=Off, 1=CubeMap, 2=Probes, 3=RealTime
    mat.EnableKeyword("_REFLECTIONMODE_PROBES");
    mat.SetFloat("_ReflectionFresnel", 5f);
    mat.SetFloat("_ReflectionFresnelNormal", 0.2f);
    mat.SetFloat("_ReflectionIntensity", 0.6f);
    mat.SetFloat("_ReflectionDistortion", 0.3f);
    mat.SetFloat("_ReflectionRoughness", 0.15f);

    // ── Waves: OFF (flat pond, no Gerstner displacement) ──
    mat.SetFloat("_DisplacementMode", 0); // 0=Off, 1=Gerstner

    // ── World UV: ON (our mesh UVs are world-position-based) ──
    mat.SetFloat("_WorldUV", 1);
    mat.EnableKeyword("_WORLD_UV");

    AssetDatabase.CreateAsset(mat, matPath);

    // Check URP depth texture requirement
    var pipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
        as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
    if (pipelineAsset != null)
    {
        if (!pipelineAsset.supportsCameraDepthTexture)
            Debug.LogWarning("[HoleLiteImporter] URP Depth Texture is OFF! " +
                "Water edge fade and depth coloring won't work. " +
                "Enable it: Edit > Project Settings > Graphics > URP Asset > General > Depth Texture");
        if (!pipelineAsset.supportsCameraOpaqueTexture)
            Debug.LogWarning("[HoleLiteImporter] URP Opaque Texture is OFF! " +
                "Water refraction/distortion won't work. " +
                "Enable it: Edit > Project Settings > Graphics > URP Asset > General > Opaque Texture");
    }

    return mat;
}
```

### Part 2 — Add `using` directive if missing

The `UniversalRenderPipelineAsset` cast at the end requires:

```csharp
using UnityEngine.Rendering.Universal;
```

Check if it's already at the top of `HoleLiteImporter.cs`. If not,
add it alongside the existing `using` statements.

If this causes a compile error (assembly reference missing), wrap
the URP depth-texture check in `#if` or just remove it and replace
with a simple `Debug.Log` reminder:

```csharp
Debug.Log("[HoleLiteImporter] REMINDER: Ensure URP Depth Texture " +
          "and Opaque Texture are ON for water shader to work properly. " +
          "Edit > Project Settings > Graphics > URP Asset > General");
```

### Part 3 — Remove MeshCollider from water meshes (optional cleanup)

The `CreateWaterMeshes()` method currently calls `AddCleanMeshCollider()`
on water GameObjects. With the transparent shader, the collider is
still useful for gameplay (ball-in-water detection via raycast),
so **keep it**. No change needed.

### Verification

1. Re-import in Unity: GOLFIN > Import Hole (Lite) > Hole 01
2. Check the console:
   - Should see `Created N water contour mesh(es)` (existing log)
   - Should NOT see the "URPWater/Standard shader not found" warning
   - If you see the depth/opaque texture warnings, toggle those ON
     in Project Settings > Graphics > URP Asset
3. Enter Play mode or Scene view:
   - Water should have animated ripples (subtle scrolling normals)
   - Water should have depth-based color (teal near edges, darker in center)
   - Edges should fade softly where water meets terrain (edge fade)
   - Should see some reflection of the skybox at glancing angles
4. Check in Game view (camera close to water surface):
   - Reflections should appear at low angles (Fresnel)
   - No harsh edge where water meets shore (edge fade working)

### If something looks wrong

- **Water is invisible:** Check render queue is 3000 (transparent).
  Also check the water mesh Y position (should be 0.05).
- **Water is black/no reflections:** URP Depth Texture might be off.
  Also check if there's a skybox set (the importer sets Sky-2.mat).
- **Water ripples too fast:** Reduce `_NormalMapASpeeds` values.
- **Water too transparent at edges:** Increase `_EdgeSize` or disable
  edge fade temporarily.
- **Shader compile error:** Make sure URP is properly installed and
  the URPWater package at `Assets/Art/3D/Props/URPWater/` is intact.

### Do NOT

- Change the water mesh geometry or ear-clip triangulation
- Change the shore slope depression code
- Change water.json export or zone contour pipeline
- Remove the `SurfaceMarker` component on water GameObjects
- Remove the `MeshCollider` on water GameObjects
- Enable Gerstner waves (too heavy for small ponds on mobile)
- Enable caustics or foam (deferred to polish pass)

---

## Previous Task — Cart Path: Contour Mesh Overlay with Minimum Width

**Goal:** Replace splatmap-only cart paths with contour mesh overlays
(same system as fairway/water/green/bunker), plus a minimum-width
enforcement of 2.5m (standard golf cart path width). Narrow painted
regions get dilated up to 2.5m instead of being skipped.

Splatmap paint for cart paths stays (zone 8 → layer 6 in `ZoneToLayer`)
— it provides the grass-beneath-asphalt ground. The mesh overlay sits
on top, giving the path physical presence and smooth edges.

### Part 1 — Export Side (`Tools/UHoleLite/scripts/export-hole.mjs`)

Add a new function `extractCartPathContours()` that wraps
`extractZoneContours` with a **minimum-width dilation** step.

```javascript
/**
 * Extract cart path contours with minimum width enforcement.
 * If a region is narrower than minWidthPx, dilate it until it reaches
 * the minimum. This prevents thin hand-painted paths from producing
 * degenerate contours.
 *
 * @param {object} zonesData - zones.json data (grid, source_dimensions)
 * @param {object} terrainMeta - terrain-meta.json data
 * @param {number} minWidthM - minimum path width in meters (default 2.5)
 * @param {number} minPixels - minimum region size in pixels
 * @param {number} rdpEpsilon - RDP simplification epsilon
 * @param {number} smoothPasses - Chaikin smoothing passes
 */
function extractCartPathContours(zonesData, terrainMeta, minWidthM = 2.5, minPixels = 15, rdpEpsilon = 1.0, smoothPasses = 2) {
  const grid = Buffer.from(zonesData.grid, 'base64');
  const w = zonesData.source_dimensions.width;
  const h = zonesData.source_dimensions.height;
  const tw = terrainMeta.terrain_width_m;
  const tl = terrainMeta.terrain_length_m;
  const targetZone = 8; // cart path

  // Meters per pixel
  const mppX = tw / w;
  const mppY = tl / h;
  const mpp = (mppX + mppY) / 2; // average
  const minWidthPx = Math.ceil(minWidthM / mpp);

  // Step 1: Find all cart path pixels, flood-fill into regions
  const visited = new Uint8Array(w * h);
  const regions = [];

  function floodFill(startX, startY) {
    const pixels = [];
    const stack = [[startX, startY]];
    while (stack.length > 0) {
      const [x, y] = stack.pop();
      if (x < 0 || x >= w || y < 0 || y >= h) continue;
      const idx = y * w + x;
      if (visited[idx] || grid[idx] !== targetZone) continue;
      visited[idx] = 1;
      pixels.push([x, y]);
      stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
    return pixels;
  }

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (grid[y * w + x] === targetZone && !visited[y * w + x]) {
        const pixels = floodFill(x, y);
        if (pixels.length >= minPixels) {
          regions.push(pixels);
        }
      }
    }
  }

  // Step 2: For each region, check width and dilate if needed
  // Then run the standard contour pipeline on the (possibly dilated) pixels
  const results = [];

  for (const originalPixels of regions) {
    // Create a local mask for this region
    let pixelSet = new Set();
    for (const [px, py] of originalPixels) {
      pixelSet.add(py * w + px);
    }

    // Estimate width: area / bounding-box-diagonal-length
    // Better approach: use the longer bbox dimension as "length",
    // then width ≈ area / length
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    for (const [px, py] of originalPixels) {
      if (px < minX) minX = px;
      if (px > maxX) maxX = px;
      if (py < minY) minY = py;
      if (py > maxY) maxY = py;
    }
    const bboxW = maxX - minX + 1;
    const bboxH = maxY - minY + 1;
    const longerAxis = Math.max(bboxW, bboxH);
    const estWidthPx = originalPixels.length / longerAxis;

    // Dilate if too narrow
    let currentPixels = originalPixels;
    if (estWidthPx < minWidthPx) {
      const dilateRadius = Math.ceil((minWidthPx - estWidthPx) / 2);
      console.log(`    Cart path region: est width ${(estWidthPx * mpp).toFixed(1)}m < ${minWidthM}m, dilating by ${dilateRadius}px`);

      // Build a mask, dilate it, extract new pixel list
      // Only dilate into non-zone pixels (rough/semi-rough/trees) — never
      // into other features like fairway, green, bunker, water, tee, OB
      const safeZones = new Set([0, 3, 4, 5, 9]); // background, semi-rough, rough, trees, OB
      const dilated = new Set(pixelSet);

      for (let r = 0; r < dilateRadius; r++) {
        const frontier = [];
        for (const key of dilated) {
          const py = Math.floor(key / w);
          const px = key % w;
          const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
          for (const [nx, ny] of neighbors) {
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            const nKey = ny * w + nx;
            if (dilated.has(nKey)) continue;
            // Only expand into safe zones
            if (safeZones.has(grid[nKey])) {
              frontier.push(nKey);
            }
          }
        }
        for (const key of frontier) {
          dilated.add(key);
        }
      }

      // Convert back to pixel array
      currentPixels = [];
      for (const key of dilated) {
        currentPixels.push([key % w, Math.floor(key / w)]);
      }
      pixelSet = dilated;
    }

    // Step 3: Run standard contour pipeline on this region
    const borderPixels = traceBorder(grid, w, h, currentPixels, targetZone);
    // NOTE: traceBorder uses borderSet based on pixelSet internally.
    // Since we may have dilated pixels that don't match the grid value,
    // we need to pass the dilated pixel list directly.
    // traceBorder's 5th param (zoneValue) is unused in the current
    // direction-aware implementation — it uses the pixelSet built from
    // the `pixels` parameter. So this should work as-is.

    // Convert to meters
    let contourMeters = borderPixels.map(([bx, by]) => ({
      x: parseFloat(((bx / (w - 1) - 0.5) * tw).toFixed(2)),
      z: parseFloat(((by / (h - 1) - 0.5) * tl).toFixed(2)),
    }));

    if (contourMeters.length < 3) continue;

    // RDP + Chaikin
    const closed = [...contourMeters, contourMeters[0]];
    let simplified = simplifyPolygon(closed, rdpEpsilon);
    if (simplified.length > 1 &&
        simplified[0].x === simplified[simplified.length - 1].x &&
        simplified[0].z === simplified[simplified.length - 1].z) {
      simplified = simplified.slice(0, -1);
    }
    contourMeters = smoothPolygon(simplified, smoothPasses);
    contourMeters = ensureCCW(contourMeters);

    // Bounding box in local meters
    const normCX = (minX + maxX) / 2 / (w - 1);
    const normCY = (minY + maxY) / 2 / (h - 1);
    const normW = (maxX - minX + 1) / w;
    const normH = (maxY - minY + 1) / h;

    results.push({
      id: results.length + 1,
      pixel_count: currentPixels.length,
      contour: contourMeters,
      center_local: {
        x: parseFloat(((normCX - 0.5) * tw).toFixed(2)),
        z: parseFloat(((normCY - 0.5) * tl).toFixed(2)),
      },
      size_m: {
        x: parseFloat((normW * tw).toFixed(2)),
        z: parseFloat((normH * tl).toFixed(2)),
      },
      center_normalized: {
        x: parseFloat(normCX.toFixed(4)),
        y: parseFloat(normCY.toFixed(4)),
      },
      size_normalized: {
        w: parseFloat(normW.toFixed(4)),
        h: parseFloat(normH.toFixed(4)),
      },
      dilated: estWidthPx < minWidthPx,
    });
  }

  // Sort by size (largest first), re-assign IDs
  results.sort((a, b) => b.pixel_count - a.pixel_count);
  results.forEach((r, i) => { r.id = i + 1; });

  return results;
}
```

Then in `exportHole()`, replace the cart path extraction in the
zone-contours section. Find:

```javascript
const cartPaths = extractZoneContours(zonesData, terrainMeta, 8, 15, 1.5, 3);
```

Replace with:

```javascript
const cartPaths = extractCartPathContours(zonesData, terrainMeta, 2.5, 15, 1.0, 2);
// 2.5m min width, 15 min pixels, RDP epsilon 1.0 (preserve narrow shape), 2 Chaikin passes
```

Also add a **separate export file** for cart paths so Unity can import
them independently (like bunkers, greens, water):

```javascript
// --- Build cart-paths.json ---
const cartPathsOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  cart_path_count: cartPaths.length,
  min_width_m: 2.5,
  cart_paths: cartPaths,
};

fs.writeFileSync(
  path.join(exportDir, 'cart-paths.json'),
  JSON.stringify(cartPathsOutput, null, 2),
  'utf-8'
);

if (cartPaths.length > 0) {
  const stats = cartPaths.map(c =>
    `#${c.id}: ${c.contour.length}pts (${c.pixel_count}px${c.dilated ? ', dilated' : ''})`
  ).join(', ');
  console.log(`  Cart path contours: ${stats}`);
}
```

Update the manifest to include `cart_paths_file: 'cart-paths.json'`.

**Keep cart paths in zone-contours.json too** for backward compatibility,
but the Unity importer should read from `cart-paths.json`.

### Part 2 — Unity Side (`Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`)

Add cart path mesh creation in `CreateFlatZoneMeshes()`. After the tee
mesh section, add:

```csharp
// ─── Cart path meshes from cart-paths.json ─────
string cpPath = Path.Combine(exportPath, "cart-paths.json");
if (File.Exists(cpPath))
{
    string cpJson = File.ReadAllText(cpPath);
    var cpData = JsonUtility.FromJson<CartPathsFile>(cpJson);

    if (cpData.cart_paths != null && cpData.cart_paths.Length > 0)
    {
        var cpRoot = new GameObject("CartPaths");
        cpRoot.transform.SetParent(parentRoot);

        var cpMat = CreateTiledMaterial(texDir, "T_RoadAsphalt_Albedo",
            "T_RoadAsphalt_Normal", dataDir, 4f);
        // Override smoothness for asphalt — slightly glossy
        cpMat.SetFloat("_Smoothness", 0.3f);

        foreach (var region in cpData.cart_paths)
        {
            if (region.contour == null || region.contour.Length < 3) continue;

            // Use ear-clip (cart paths are narrow/winding = concave)
            var meshGO = CreateFairwayMesh(
                region.id, region.contour,
                terrain, terrainBaseY,
                cpMat,
                new Vector2(1, 0), // stripe dir doesn't matter for asphalt
                4f); // tile size for asphalt texture

            if (meshGO != null)
            {
                meshGO.name = $"CartPath_{region.id}";
                // Override surface marker
                var marker = meshGO.GetComponent<Golfin.Course.SurfaceMarker>();
                if (marker != null)
                    marker.surfaceType = Golfin.Course.SurfaceType.CartPath;
                meshGO.transform.SetParent(cpRoot.transform);
            }
        }

        Debug.Log($"[HoleLiteImporter] Created {cpData.cart_paths.Length} cart path mesh(es)");
    }
}
```

Alternatively, instead of reusing `CreateFairwayMesh` (which has
mow-stripe UV logic), create a simpler ear-clip mesh without stripe
UVs — just standard world-position-based tiling. You can use
`CreateFlatContourMesh` directly if it works, or make a variant
that uses ear-clip instead of centroid-fan:

```csharp
private static GameObject CreateEarClipContourMesh(int id, string zoneName,
    ContourPoint[] contour, Terrain terrain, float terrainBaseY,
    Material mat, float tileSize, Golfin.Course.SurfaceType surfaceType)
{
    int n = contour.Length;
    if (n < 3) return null;

    float yOffset = 0.015f; // between terrain and other overlays

    // 90° CCW rotation
    Vector3[] worldPts = new Vector3[n];
    for (int i = 0; i < n; i++)
    {
        float wx = contour[i].z;
        float wz = contour[i].x;
        float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
        worldPts[i] = new Vector3(wx, terrainBaseY + th + yOffset, wz);
    }

    // Centroid
    float cx = 0, cy = 0, cz = 0;
    for (int i = 0; i < n; i++)
    { cx += worldPts[i].x; cy += worldPts[i].y; cz += worldPts[i].z; }
    cx /= n; cy /= n; cz /= n;
    Vector3 centroid = new Vector3(cx, cy, cz);

    var verts = new Vector3[n];
    var uvs = new Vector2[n];
    for (int i = 0; i < n; i++)
    {
        verts[i] = worldPts[i] - centroid;
        uvs[i] = new Vector2(worldPts[i].x / tileSize, worldPts[i].z / tileSize);
    }

    var tris = EarClipTriangulate(worldPts);
    if (tris == null || tris.Length < 3) return null;

    var mesh = new Mesh();
    mesh.name = $"{zoneName}_{id}";
    mesh.vertices = verts;
    mesh.triangles = tris;
    mesh.uv = uvs;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"{zoneName}_{id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    go.AddComponent<MeshCollider>().sharedMesh = mesh;

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = surfaceType;

    return go;
}
```

Use it:
```csharp
var meshGO = CreateEarClipContourMesh(
    region.id, "CartPath", region.contour,
    terrain, terrainBaseY, cpMat, 4f,
    Golfin.Course.SurfaceType.CartPath);
```

### Part 3 — Data Classes

Add to `HoleManifestData.cs`:

```csharp
[System.Serializable]
public class CartPathsFile
{
    public string schema_version;
    public int hole_number;
    public int cart_path_count;
    public float min_width_m;
    public CartPathRegionData[] cart_paths;
}

[System.Serializable]
public class CartPathRegionData
{
    public int id;
    public int pixel_count;
    public ContourPoint[] contour;
    public AnchorLocal center_local;
    public SizeData size_m;
    public bool dilated;
}
```

Also check that `SurfaceType` enum has a `CartPath` entry. If not, add
it (check `Assets/Scripts/Course/SurfaceMarker.cs` or similar).

### Part 4 — Keep Splatmap

**Do NOT** change `ZoneToLayer` for zone 8. Cart path splatmap paint
stays at layer 6 (T_RoadAsphalt_Albedo). The mesh overlay sits on top.
This gives natural terrain-under-asphalt and a visible edge transition.

### Verification

1. Re-export: `node scripts/export-hole.mjs lomond-country-club 1`
   - Should log `Cart path contours: #N: NNpts (NNpx)` or `dilated`
   - `cart-paths.json` should exist in export dir
2. Re-import in Unity: GOLFIN > Import Hole (Lite) > Hole 01
   - Cart paths should be visible as raised asphalt mesh on terrain
   - Splatmap asphalt still visible underneath (no gap)
   - Walk along path — smooth edges, no jaggies
3. Check a hole that has narrow cart paths — verify dilation kicks in
   and the path is at least 2.5m wide

### Do NOT

- Remove cart path from splatmap pipeline (zone 8 → layer 6 stays)
- Change `traceBorder`, `simplifyPolygon`, `smoothPolygon`, `ensureCCW`
- Modify bunker, green, fairway, or water pipeline code
- Change `EarClipTriangulate`
- Skip small cart path regions — dilate them up to 2.5m instead

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes + fringe ring
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ DONE: 2026-04-08 — Tee border ring with gradient texture
✅ DONE: 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ DONE: 2026-04-08 — traceBorder replaced with direction-aware walk + RDP epsilon 3.0→1.0, Chaikin 3→2. BIG DIFF at z=50 eliminated (-5.4→-1.2m). Note: trace was not the root cause — the 22.1% diagnostic was misleading (counted interior border pixels). Real fix was RDP reduction. One BIG DIFF remains at z=-5 (narrow tip, -5.2m).
✅ DONE: 2026-04-09 — Water: replaced rasterized quad + SDF alpha mask with contour mesh overlay. Export uses extractZoneContours (zone 7, epsilon 2.0, 2 Chaikin passes). Unity importer uses ear-clip triangulation + opaque water material. Shore slope depression preserved unchanged.
✅ DONE: 2026-04-09 — Cart Path: contour mesh overlay with min-width enforcement. New extractCartPathContours() with 2.5m min-width dilation. Separate cart-paths.json export. Unity CreateEarClipContourMesh for concave paths. Splatmap layer 6 preserved underneath. Hole 1: 1 cart path region, 392pts, no dilation needed.
✅ DONE: 2026-04-09 — Water Shader: replaced URP/Lit with URPWater/Standard. Animated normals (T_Water_03_N.tga), depth-based coloring, edge fade, probe reflections. Fallback to URP/Lit if shader missing. URP depth/opaque texture warnings logged if OFF.
