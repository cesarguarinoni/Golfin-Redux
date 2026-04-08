# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Smooth Zone Boundaries via Vector Contour Rasterization

**Goal:** Eliminate jagged pixel-staircase edges on ALL zone boundaries
(fairway↔rough, fairway↔semi-rough, tee↔rough, etc.) by tracing zone
boundaries as vector contours, smoothing them with Chaikin subdivision,
then rasterizing the smoothed polygons back onto the alphamap.

**Why the SDF approach failed:** The chamfer SDF produces smooth iso-lines
mathematically, but thresholding at `dist > 0` converts back to a binary
mask — so the staircase just moves to a slightly different position. The
fundamental problem is that the zone grid is ~800px, and the boundary
shape itself is jagged. We need to smooth the _geometry_ of the boundary,
not post-process the pixels.

**Approach:** Reuse the proven contour pipeline from bunkers/greens
(traceBorder → RDP → Chaikin) to extract smooth vector polygons for
each zone, then rasterize those smooth polygons onto the 1024×1024
alphamap using scanline fill. Interior pixels stay the same; only the
edge band gets the smooth curve shape from the vector contour.

---

### Part 1: Export pipeline — `Tools/UHoleLite/scripts/export-hole.mjs`

Add fairway contour extraction alongside bunkers and greens.

#### 1a. Extract fairway contours

After the greens extraction block, add fairway extraction:

```javascript
// --- Build fairway-contours.json ---
const fairways = extractZoneContours(zonesData, terrainMeta, 1, 30, 3.0, 3);
// zone 1 = fairway, min 30px, RDP epsilon 3.0 (looser than bunkers),
// 3 Chaikin passes for extra smoothness on the larger shapes

const fairwayOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  fairway_count: fairways.length,
  fairways: fairways,
};

fs.writeFileSync(
  path.join(exportDir, 'fairway-contours.json'),
  JSON.stringify(fairwayOutput, null, 2),
  'utf-8'
);

if (fairways.length > 0) {
  const contourStats = fairways.map(f =>
    `#${f.id}: ${f.contour.length}pts (${f.pixel_count}px)`
  ).join(', ');
  console.log(`  Fairway contours: ${contourStats}`);
}
```

Also add similar extraction for these zones (same block, after fairway):

```javascript
// --- Build zone-contours.json (tee, semi-rough, cart path) ---
const tees = extractZoneContours(zonesData, terrainMeta, 10, 15, 2.0, 2);
const semiRough = extractZoneContours(zonesData, terrainMeta, 3, 30, 3.0, 3);

const zoneContoursOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  zones: {
    tee: tees,
    semi_rough: semiRough,
  },
};

fs.writeFileSync(
  path.join(exportDir, 'zone-contours.json'),
  JSON.stringify(zoneContoursOutput, null, 2),
  'utf-8'
);
```

#### 1b. Update manifest

Add to the manifest object:
```javascript
fairway_contours_file: 'fairway-contours.json',
zone_contours_file: 'zone-contours.json',
```

---

### Part 2: Unity importer — `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

**The core change:** After resampling the zone grid (step 2), instead of
using the raw pixel grid directly for the alphamap, we load the smoothed
vector contours and rasterize them onto the alphamap. This replaces the
jagged zone grid edges with the smooth Chaikin curves.

#### 2a. Add data classes for contour JSON

Add to `HoleManifestData.cs` (or inline in HoleLiteImporter):

```csharp
[System.Serializable]
public class FairwayContoursFile
{
    public int fairway_count;
    public ZoneContourRegion[] fairways;
}

[System.Serializable]
public class ZoneContoursFile
{
    public ZoneContoursZones zones;
}

[System.Serializable]
public class ZoneContoursZones
{
    public ZoneContourRegion[] tee;
    public ZoneContourRegion[] semi_rough;
}

[System.Serializable]
public class ZoneContourRegion
{
    public int id;
    public int pixel_count;
    public ContourPoint[] contour;
    public AnchorLocal center_local;
}

[System.Serializable]
public class ContourPoint
{
    public float x;
    public float z;
}
```

NOTE: `AnchorLocal` already exists. `ContourPoint` is the same shape
as the `{x, z}` objects in the contour arrays from export-hole.mjs.

#### 2b. Add polygon rasterizer (scanline fill)

Add a static helper method to `HoleLiteImporter`:

```csharp
/// <summary>
/// Rasterize a smooth polygon onto a byte grid.
/// For each pixel inside the polygon, sets grid[y * w + x] = value.
/// Uses ray-casting (point-in-polygon) test.
/// polyX/polyZ are in the same coordinate space as the grid
/// (alphamap pixel coordinates, already transformed from local meters).
/// </summary>
static void RasterizePolygon(float[] polyX, float[] polyZ, int polyCount,
    byte[] grid, int w, int h, byte value,
    int minAX, int minAY, int maxAX, int maxAY)
{
    // Only test pixels within the polygon's bounding box
    for (int ay = minAY; ay <= maxAY; ay++)
    {
        for (int ax = minAX; ax <= maxAX; ax++)
        {
            // Ray-casting point-in-polygon test
            float px = ax + 0.5f; // pixel center
            float py = ay + 0.5f;
            bool inside = false;
            for (int i = 0, j = polyCount - 1; i < polyCount; j = i++)
            {
                if ((polyZ[i] > py) != (polyZ[j] > py) &&
                    px < (polyX[j] - polyX[i]) * (py - polyZ[i]) /
                         (polyZ[j] - polyZ[i]) + polyX[i])
                {
                    inside = !inside;
                }
            }
            if (inside)
                grid[ay * w + ax] = value;
        }
    }
}
```

#### 2c. Modify ApplySplatmap to use vector contours

After step 2 (resample zone grid), before step 3 (green fringe):

```csharp
// --- 2b. Override zone grid edges with smoothed vector contours ---
// Load fairway contours and rasterize smooth boundary onto resampledZones
string fairwayContoursPath = Path.Combine(exportPath, "fairway-contours.json");
if (File.Exists(fairwayContoursPath))
{
    string fcJson = File.ReadAllText(fairwayContoursPath);
    var fcData = JsonUtility.FromJson<FairwayContoursFile>(fcJson);

    if (fcData.fairways != null)
    {
        // First: clear ALL fairway pixels from the resampled grid.
        // We'll re-fill only what the smooth contour covers.
        // This prevents jagged original pixels from leaking outside
        // the smooth boundary.
        for (int i = 0; i < resampledZones.Length; i++)
        {
            if (resampledZones[i] == 1) // zone 1 = fairway
                resampledZones[i] = 4;  // revert to rough
        }

        foreach (var fw in fcData.fairways)
        {
            if (fw.contour == null || fw.contour.Length < 3) continue;
            int n = fw.contour.Length;

            // Convert contour from local meters to alphamap pixel coords
            // Local meters: origin at terrain center
            // Alphamap: (0,0) = corner, (alphaRes-1, alphaRes-1) = opposite corner
            // Terrain rotation: worldX = local.z, worldZ = local.x
            // So: ax = (worldX + terrainX/2) / terrainX * (alphaRes-1)
            //     ay = (worldZ + terrainZ/2) / terrainZ * (alphaRes-1)
            // Where worldX = contour.z, worldZ = contour.x (90° CCW)

            float[] polyAX = new float[n];
            float[] polyAY = new float[n];
            float terrainX = terrainData.size.x;
            float terrainZ = terrainData.size.z;

            float bminAX = float.MaxValue, bmaxAX = float.MinValue;
            float bminAY = float.MaxValue, bmaxAY = float.MinValue;

            for (int i = 0; i < n; i++)
            {
                float worldX = fw.contour[i].z; // 90° CCW rotation
                float worldZ = fw.contour[i].x;
                float ax = (worldX + terrainX / 2f) / terrainX * (alphaRes - 1);
                float ay = (worldZ + terrainZ / 2f) / terrainZ * (alphaRes - 1);
                polyAX[i] = ax;
                polyAY[i] = ay;
                if (ax < bminAX) bminAX = ax;
                if (ax > bmaxAX) bmaxAX = ax;
                if (ay < bminAY) bminAY = ay;
                if (ay > bmaxAY) bmaxAY = ay;
            }

            int minAXi = Mathf.Max(0, Mathf.FloorToInt(bminAX));
            int maxAXi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAX));
            int minAYi = Mathf.Max(0, Mathf.FloorToInt(bminAY));
            int maxAYi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAY));

            RasterizePolygon(polyAX, polyAY, n,
                resampledZones, alphaRes, alphaRes, 1,
                minAXi, minAYi, maxAXi, maxAYi);
        }

        Debug.Log($"[HoleLiteImporter] Rasterized {fcData.fairways.Length} " +
                  $"smooth fairway contour(s) onto alphamap");
    }
}

// Load tee + semi-rough contours and rasterize
string zoneContoursPath = Path.Combine(exportPath, "zone-contours.json");
if (File.Exists(zoneContoursPath))
{
    string zcJson = File.ReadAllText(zoneContoursPath);
    var zcData = JsonUtility.FromJson<ZoneContoursFile>(zcJson);

    // Tee boxes (zone 10)
    if (zcData.zones?.tee != null)
    {
        // Clear original tee pixels first
        for (int i = 0; i < resampledZones.Length; i++)
            if (resampledZones[i] == 10) resampledZones[i] = 4;

        foreach (var region in zcData.zones.tee)
            RasterizeContour(region, resampledZones, alphaRes, terrainData, 10);
    }

    // Semi-rough (zone 3) — only clear+refill if contours exist
    if (zcData.zones?.semi_rough != null && zcData.zones.semi_rough.Length > 0)
    {
        // NOTE: Don't clear semi-rough pixels globally because the
        // fairway fringe ring also writes to semi-rough layer.
        // Instead, just overlay the smooth contours on top.
        foreach (var region in zcData.zones.semi_rough)
            RasterizeContour(region, resampledZones, alphaRes, terrainData, 3);
    }
}
```

Add the helper that wraps the coordinate transform + rasterize:

```csharp
static void RasterizeContour(ZoneContourRegion region, byte[] grid,
    int alphaRes, TerrainData terrainData, byte zoneValue)
{
    if (region.contour == null || region.contour.Length < 3) return;
    int n = region.contour.Length;

    float terrainX = terrainData.size.x;
    float terrainZ = terrainData.size.z;

    float[] polyAX = new float[n];
    float[] polyAY = new float[n];
    float bminAX = float.MaxValue, bmaxAX = float.MinValue;
    float bminAY = float.MaxValue, bmaxAY = float.MinValue;

    for (int i = 0; i < n; i++)
    {
        float worldX = region.contour[i].z;
        float worldZ = region.contour[i].x;
        float ax = (worldX + terrainX / 2f) / terrainX * (alphaRes - 1);
        float ay = (worldZ + terrainZ / 2f) / terrainZ * (alphaRes - 1);
        polyAX[i] = ax;
        polyAY[i] = ay;
        if (ax < bminAX) bminAX = ax;
        if (ax > bmaxAX) bmaxAX = ax;
        if (ay < bminAY) bminAY = ay;
        if (ay > bmaxAY) bmaxAY = ay;
    }

    int minAXi = Mathf.Max(0, Mathf.FloorToInt(bminAX));
    int maxAXi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAX));
    int minAYi = Mathf.Max(0, Mathf.FloorToInt(bminAY));
    int maxAYi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAY));

    RasterizePolygon(polyAX, polyAY, n,
        grid, alphaRes, alphaRes, zoneValue,
        minAXi, minAYi, maxAXi, maxAYi);
}
```

#### 2d. Remove SDF-based fairway fringe (no longer needed)

The SDF code (step 3b) was an attempt to smooth edges — now that the
vector contours produce smooth boundaries, the SDF is redundant.

**Remove** the entire step 3b block:
- The `fairwayMask` / `ComputeSDF` / `fairwaySDF` / `fringePixels` /
  `fairwayFringeMask` / `sdfFairwayMask` section.

**Replace** with a simple dilation-based fringe (same approach as green fringe):

```csharp
// --- 3b. Fairway fringe ring (dilation-based, smooth edges from vector contours) ---
bool[] fairwayMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < resampledZones.Length; i++)
    fairwayMask[i] = (resampledZones[i] == 1);

float metersPerPixel = Mathf.Max(terrainData.size.x, terrainData.size.z) / alphaRes;
int fairwayFringePx = Mathf.Max(1, Mathf.RoundToInt(FairwayFringeMeters / metersPerPixel));

bool[] dilatedFairway = DilateMask(fairwayMask, alphaRes, alphaRes, fairwayFringePx);

bool[] fairwayFringeMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < alphaRes * alphaRes; i++)
{
    if (dilatedFairway[i] && !fairwayMask[i])
    {
        int zone = resampledZones[i];
        if (zone == 3 || zone == 4 || zone == 5)
            fairwayFringeMask[i] = true;
    }
}
```

#### 2e. Update the alphamap loop (step 4)

Remove the `sdfFairwayMask` branch. The loop now uses the raw
`resampledZones` (which has been overwritten with smooth contour data)
for all zone assignments:

```csharp
for (int ay = 0; ay < alphaRes; ay++)
{
    for (int ax = 0; ax < alphaRes; ax++)
    {
        int idx = ay * alphaRes + ax;
        int layer;

        if (fringeMask[idx])
            layer = 2; // green fringe → semi-rough
        else if (fairwayFringeMask[idx])
            layer = 2; // fairway fringe → semi-rough
        else
        {
            int zone = resampledZones[idx];
            layer = ZoneToLayer(zone);

            // Mow stripes on fairway
            if (zone == 1)
            {
                float worldX = ((float)ax / (alphaRes - 1)) * terrainSizeX - terrainSizeX / 2f;
                float worldZ = ((float)ay / (alphaRes - 1)) * terrainSizeZ - terrainSizeZ / 2f;
                float proj = worldX * stripeDir.x + worldZ * stripeDir.y;
                int band = Mathf.FloorToInt(proj / MowStripeWidth);
                if (band % 2 != 0)
                    layer = 7; // dark fairway stripe
            }
        }

        alphamap[ay, ax, layer] = 1.0f;
    }
}
```

#### 2f. Remove ComputeSDF method

The `ComputeSDF` static method is no longer used. Remove it entirely
to keep the file clean. The `DilateMask` method stays (used by fringe).

---

### Verification

- [ ] Run `node scripts/export-hole.mjs lomond-country-club 1`
  - Should output `fairway-contours.json` and `zone-contours.json`
  - Console shows contour point counts
- [ ] Re-import Hole 1 in Unity (GOLFIN > Import Hole (Lite) > Hole 01)
- [ ] **Fairway boundary has smooth, organic curves** — no pixel staircase
- [ ] Fairway shape is close to the original painted zone (no dramatic
  size change from the smoothing)
- [ ] Semi-rough fringe ring visible around fairway
- [ ] Mow stripes still alternate inside the fairway
- [ ] Green fringe ring still works (unchanged)
- [ ] Tee boxes have smooth edges
- [ ] Bunkers, water, greens unaffected (they use mesh overlays)
- [ ] No console errors
- [ ] Cart paths: if they look OK with the raw zone grid, leave them.
  If jagged, we can add cart_path contours later.

### Do NOT

- Apply any Gaussian blur
- Use alpha blending / fractional splatmap weights
- Modify the bunker, green, or water mesh pipelines
- Touch terrain layers, textures, or mask maps
- Change zone indices or ZoneToLayer mapping

---

### Design rationale

The key insight: bunkers and greens already look great because they go
through the contour pipeline (traceBorder → RDP → Chaikin → mesh).
The fairway and other splatmap-painted zones skip this step and go
straight from pixel grid to alphamap — hence the jaggedness.

Option D applies the same proven contour pipeline to splatmap zones:
1. Export: trace zone boundary → simplify (RDP) → smooth (Chaikin)
2. Import: clear original jagged pixels → rasterize smooth polygon
3. Result: the alphamap now contains smooth curves from vector data
   instead of pixel staircases from the zone grid

The `resampledZones` byte array gets overwritten in-place with the
smooth polygon fill, so all downstream code (fringe rings, mow stripes,
ZoneToLayer) works unchanged — they just get smoother input.

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: swap, fringe ring, blur removed, alphamap 1024, zone grid 2048
✅ DONE: 2026-04-08 — PNG + SVG zone import in Hole Viewer
✅ DONE: 2026-04-08 — Morphological close + various smoothing attempts
✅ DONE: 2026-04-08 — Fairway mow stripes: alternating light/dark bands along tee→green axis
✅ DONE: 2026-04-08 — Re-enable normal maps (0.4 intensity) + aniso filtering (level 16) on all terrain textures
✅ DONE: 2026-04-08 — SDF-based smooth fairway border (chamfer distance, 1.5m fringe, organic curves)
✅ DONE: 2026-04-08 — Vector contour rasterization for smooth zone boundaries (fairway, tee, semi-rough)
