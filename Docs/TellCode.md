# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task — Phase K: Hole 1 Terrain Importer (Prototype)

**Goal:** Build a Unity Editor script that reads the UHole export package for Hole 1
and generates a walkable scene with real-world terrain, satellite texture, and anchor markers.

This is the first 3D scene in the project. No gameplay code — just a walk-around prototype
with WASD + mouse look camera.

---

### Input Data

The export package lives at:
```
Tools/UHole/output/lomond-country-club/export/hole-01/
```

It contains:
- `hole-manifest.json` — metadata, bounds, terrain specs, anchor/tile references
- `heightmap.raw` — 129×129 uint16 big-endian heightmap
- `aerial-tiles.json` — 25 GSI photo tile JPEGs with bounds
- `anchors.json` — 4 tee positions in world (lat/lon) and local (x,z meters) coords

**Key manifest values:**
- Terrain resolution: 129×129
- Min elevation: 77.88m, Max elevation: 227.59m (range: 149.71m)
- Terrain width: 960.2m, Terrain length: 992.6m
- Origin: lat 34.9124496, lon 136.4389668 (center of bounds)
- Par 5, 531 yards, stroke index 9

**Anchor local coordinates** (meters from origin, x=east, z=north):
- Back Tee: x=251.42, z=-302.94
- Regular Tee: x=222.76, z=-297.26
- Front Tee: x=197.96, z=-301.79
- Ladies Tee: x=169.36, z=-289.30

Note: No green_center or pin anchors yet — Kai will add those later via the UHole app.

---

### Output Structure

```
Assets/
  Golf/
    Courses/
      lomond-country-club/
        Data/
          hole-01/          ← copy of export package (or symlink)
        Generated/
          Hole_01.unity     ← the scene
```

---

### Step 1: Create the Importer Script

**File:** `Assets/Scripts/Editor/CourseImporter/HoleImporter.cs`
**Namespace:** `Golfin.CourseImport`

An editor-only script with a menu item: `GOLFIN > Import Hole > Hole 01`

The script:

1. **Reads `hole-manifest.json`** from the Data folder using `JsonUtility` or
   manual JSON parsing (the manifest structure is simple enough for either).

2. **Creates a new scene** named `Hole_01` in `Assets/Golf/Courses/lomond-country-club/Generated/`

3. **Creates terrain** from the heightmap:
   ```csharp
   // Create TerrainData
   var terrainData = new TerrainData();
   terrainData.heightmapResolution = 129; // from manifest
   terrainData.size = new Vector3(
       manifest.terrain.terrain_width_m,    // 960.2
       manifest.terrain.max_elevation_m - manifest.terrain.min_elevation_m, // 149.71
       manifest.terrain.terrain_length_m    // 992.6
   );

   // Read heightmap.raw — 129×129 uint16 big-endian
   byte[] rawBytes = File.ReadAllBytes(heightmapPath);
   float[,] heights = new float[129, 129];
   for (int y = 0; y < 129; y++) {
       for (int x = 0; x < 129; x++) {
           int idx = (y * 129 + x) * 2;
           ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]); // big-endian
           heights[y, x] = val / 65535f; // Unity wants 0-1 range
       }
   }
   terrainData.SetHeights(0, 0, heights);

   // Create terrain GameObject
   var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
   terrainGO.name = "TerrainRoot";
   ```

   **IMPORTANT:** Unity terrain is positioned so that (0,0,0) is its corner, not center.
   The manifest origin is the center of the bounds, so offset the terrain:
   ```csharp
   terrainGO.transform.position = new Vector3(
       -manifest.terrain.terrain_width_m / 2f,    // shift left by half width
       manifest.terrain.min_elevation_m,           // base elevation (so 0 in heightmap = 77.88m world)
       -manifest.terrain.terrain_length_m / 2f     // shift back by half length
   );
   ```

   Wait — actually, since anchors use local coords relative to the origin (center of bounds),
   and Unity terrain starts at its transform position, we need the terrain to span from
   `-width/2` to `+width/2` and `-length/2` to `+length/2`. The min_elevation offset
   keeps the absolute elevations correct.

   Actually, for the prototype, set terrain position to:
   ```csharp
   terrainGO.transform.position = new Vector3(
       -manifest.terrain.terrain_width_m / 2f,
       0f,  // we'll use relative heights, min=0
       -manifest.terrain.terrain_length_m / 2f
   );
   ```
   And terrain height (`terrainData.size.y`) = elevation range (149.71m).
   The heightmap values are already normalized 0-1 within that range.

4. **Creates aerial texture** from the tile JPEGs:

   Read `aerial-tiles.json`, load all 25 tile JPEGs, stitch them into one `Texture2D`,
   and apply as terrain splat/layer texture.

   Steps:
   ```csharp
   // Determine tile grid dimensions
   // tiles span x: 115209-115213 (5 cols), y: 51954-51958 (5 rows) = 5×5 grid
   // Each tile is 256×256 pixels → stitched texture is 1280×1280

   var stitched = new Texture2D(1280, 1280, TextureFormat.RGB24, false);

   foreach (var tile in tiles) {
       int col = tile.x - minTileX;  // 0-4
       int row = maxTileY - tile.y;  // flip Y (tile Y increases south, texture Y increases up)
       byte[] jpgBytes = File.ReadAllBytes(tilePath);
       var tileTex = new Texture2D(256, 256);
       tileTex.LoadImage(jpgBytes);
       stitched.SetPixels(col * 256, row * 256, 256, 256, tileTex.GetPixels());
   }
   stitched.Apply();

   // Save as asset
   byte[] pngBytes = stitched.EncodeToPNG();
   File.WriteAllBytes(texturePath, pngBytes);
   AssetDatabase.ImportAsset(texturePath);

   // Apply to terrain as a TerrainLayer
   var layer = new TerrainLayer();
   layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
   layer.tileSize = new Vector2(
       manifest.terrain.terrain_width_m,
       manifest.terrain.terrain_length_m
   );
   terrainData.terrainLayers = new TerrainLayer[] { layer };
   ```

   The tile paths in `aerial-tiles.json` are relative (`../../basemap/gsi-photo-z17/...`).
   Resolve them relative to the export folder.

   **NOTE:** The tile JPEGs are in `Tools/UHole/output/...` which is outside `Assets/`.
   The importer needs to read them from the Tools path, stitch, and save the result
   inside `Assets/`. The manifest path and tile paths should be resolved from the
   project root.

5. **Places anchor markers:**

   For each anchor in `anchors.json`, create a small marker GameObject:
   ```csharp
   foreach (var anchor in anchors) {
       var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
       marker.name = $"Anchor_{anchor.type}";
       marker.transform.position = new Vector3(
           (float)anchor.local.x,
           0f,  // will be adjusted to terrain height
           (float)anchor.local.z
       );
       // Scale: thin tall marker
       marker.transform.localScale = new Vector3(2f, 5f, 2f);

       // Color by type
       var renderer = marker.GetComponent<Renderer>();
       var mat = new Material(Shader.Find("Standard"));
       mat.color = anchor.type.Contains("back") ? Color.blue
                 : anchor.type.Contains("regular") ? Color.green
                 : anchor.type.Contains("front") ? Color.white
                 : anchor.type.Contains("ladies") ? Color.red
                 : Color.yellow;
       renderer.material = mat;

       // Sample terrain height at this position
       float terrainHeight = terrain.SampleHeight(marker.transform.position);
       marker.transform.position = new Vector3(
           marker.transform.position.x,
           terrainHeight,
           marker.transform.position.z
       );

       marker.transform.SetParent(anchorsRoot.transform);
   }
   ```

6. **Creates a walk-around camera:**

   Add a simple first-person camera controller for WASD + mouse look:

   **File:** `Assets/Scripts/Debug/WalkCamera.cs` (NOT editor-only, needs to run in play mode)
   **Namespace:** `Golfin.Debug`

   ```csharp
   public class WalkCamera : MonoBehaviour
   {
       public float moveSpeed = 20f;
       public float lookSpeed = 2f;
       public float height = 2f;

       private float yaw = 0f;
       private float pitch = 0f;

       void Start()
       {
           Cursor.lockState = CursorLockMode.Locked;
       }

       void Update()
       {
           // Mouse look
           yaw += Input.GetAxis("Mouse X") * lookSpeed;
           pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
           pitch = Mathf.Clamp(pitch, -89f, 89f);
           transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

           // WASD movement
           float h = Input.GetAxis("Horizontal");
           float v = Input.GetAxis("Vertical");
           Vector3 move = transform.right * h + transform.forward * v;
           move.y = 0f;
           transform.position += move.normalized * moveSpeed * Time.deltaTime;

           // Stick to terrain height + offset
           if (Terrain.activeTerrain != null)
           {
               float terrainY = Terrain.activeTerrain.SampleHeight(transform.position);
               transform.position = new Vector3(
                   transform.position.x,
                   terrainY + height,
                   transform.position.z
               );
           }

           // Unlock cursor with Escape
           if (Input.GetKeyDown(KeyCode.Escape))
               Cursor.lockState = CursorLockMode.None;
           if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
               Cursor.lockState = CursorLockMode.Locked;
       }
   }
   ```

   The importer creates a Camera with this component at the back tee position,
   looking toward the green (or north if no green anchor exists).

7. **Creates HoleMetadata component:**

   **File:** `Assets/Scripts/HoleMetadata.cs`
   **Namespace:** `Golfin.CourseImport`

   ```csharp
   public class HoleMetadata : MonoBehaviour
   {
       public string courseId;
       public int holeNumber;
       public int par;
       public int strokeIndex;
       public int championshipYards;
       public string reviewStatus;
   }
   ```

   Attach to a root `HoleRoot` GameObject and populate from the manifest.

8. **Scene hierarchy:**

   ```
   Hole_01 (scene)
   ├── HoleRoot (HoleMetadata component)
   │   ├── TerrainRoot (Terrain + TerrainCollider)
   │   ├── Anchors
   │   │   ├── Anchor_tee_back (Cylinder, blue)
   │   │   ├── Anchor_tee_regular (Cylinder, green)
   │   │   ├── Anchor_tee_front (Cylinder, white)
   │   │   └── Anchor_tee_ladies (Cylinder, red)
   │   └── DebugReferences (empty, for future overlays)
   ├── WalkCamera (Camera + WalkCamera component)
   └── Directional Light
   ```

9. **Saves the scene and all generated assets.**

---

### Step 2: Data Path Configuration

The importer needs to find two paths:
- **Export package:** `Tools/UHole/output/lomond-country-club/export/hole-01/`
- **Tile images:** `Tools/UHole/output/lomond-country-club/basemap/gsi-photo-z17/`

Both are relative to the project root (`GolfinRedux/`). Use `Application.dataPath`
to derive the project root:
```csharp
string projectRoot = Path.GetDirectoryName(Application.dataPath);
string exportPath = Path.Combine(projectRoot, "Tools", "UHole", "output",
    "lomond-country-club", "export", "hole-01");
```

---

### Step 3: JSON Parsing

Unity's `JsonUtility` requires `[Serializable]` classes. Create simple data classes:

**File:** `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs`

```csharp
[System.Serializable]
public class HoleManifest {
    public string course_id;
    public int hole_number;
    public int par;
    public int stroke_index;
    public int championship_yards;
    public ManifestBounds bounds;
    public ManifestOrigin origin;
    public ManifestTerrain terrain;
    public ManifestAerial aerial;
    public string anchors_file;
    public string review_status;
}

[System.Serializable]
public class ManifestBounds {
    public double north, south, east, west;
}

[System.Serializable]
public class ManifestOrigin {
    public double lat, lon;
}

[System.Serializable]
public class ManifestTerrain {
    public string heightmap_file;
    public string format;
    public int resolution;
    public float min_elevation_m;
    public float max_elevation_m;
    public float terrain_width_m;
    public float terrain_length_m;
}

[System.Serializable]
public class ManifestAerial {
    public string tiles_file;
    public int tile_count;
}

// For aerial-tiles.json
[System.Serializable]
public class AerialTilesData {
    public TileBounds hole_bounds;
    public AerialTile[] tiles;
}

[System.Serializable]
public class AerialTile {
    public string path;
    public int z, x, y;
    public TileBounds bounds;
}

[System.Serializable]
public class TileBounds {
    public double north, south, east, west;
}

// For anchors.json — needs manual parsing since it's an array root
// JsonUtility can't deserialize root arrays, so wrap:
[System.Serializable]
public class AnchorData {
    public string type;
    public string label;
    public AnchorLocal local;
}

[System.Serializable]
public class AnchorLocal {
    public float x, z;
}
```

**NOTE:** `JsonUtility` can't deserialize root-level JSON arrays. For `anchors.json`
(which is `[...]`), wrap it: `JsonUtility.FromJson<Wrapper>("{\"items\":" + json + "}")`
or parse manually.

---

### Menu Item

```csharp
[MenuItem("GOLFIN/Import Hole/Hole 01")]
public static void ImportHole01()
{
    ImportHole("lomond-country-club", 1);
}
```

Show a progress bar during import with `EditorUtility.DisplayProgressBar`.

---

### Verification

- [ ] Menu item `GOLFIN > Import Hole > Hole 01` exists and runs
- [ ] Scene `Hole_01` created in `Assets/Golf/Courses/lomond-country-club/Generated/`
- [ ] Terrain visible with real elevation (hills, valleys — not flat)
- [ ] Satellite texture applied to terrain (green fairways visible)
- [ ] 4 colored tee markers placed at correct positions on terrain surface
- [ ] Markers match expected layout (Back=blue furthest from green, Ladies=red closest)
- [ ] WalkCamera works in play mode: WASD movement, mouse look, stays on terrain
- [ ] Camera starts at back tee position, ~2m above ground
- [ ] HoleMetadata component on root with correct values
- [ ] No console errors
- [ ] Re-running the import replaces the existing scene cleanly

### What's NOT in this phase

- ❌ Green center / pin markers (not yet placed in UHole)
- ❌ Surface classification (fairway/rough/bunker polygons)
- ❌ Vegetation or props
- ❌ Gameplay code
- ❌ Multiple hole support (just Hole 1 for now)
- ❌ Lighting polish

---

## Previous Completed Tasks

✅ DONE: 2026-04-01 — Phase J: Bags Inventory Screen (BagCarouselController, BagDetailPanel, BagClubModalController, BagClubCard, BagThumbnailCard, localization keys, CSV updates)

✅ DONE: 2026-03-31 — Phase I2 Item Use → Club Selection Modal
✅ DONE: 2026-03-31 — Phase I1 Items Inventory
✅ DONE: 2026-03-27 — Phase H Balls Inventory
✅ DONE: 2026-03-26 — Phase G Character Compare stat diff labels
✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
✅ DONE: 2026-03-25 — Phase E1 Club Level Up Modal
✅ DONE: 2026-03-26 — Phase E2 Club Repair One-Tap
✅ DONE: 2026-03-26 — Phase E3 Bag Selection Modal
✅ DONE: 2026-03-26 — Phase E3b Bags CSV + Data-Driven Bag Slots
✅ DONE: 2026-03-26 — Phase E4 Bag ↔ Club management
✅ DONE: 2026-03-26 — Phase F Level Up Modal polish (SP allocation UI)
✅ DONE: 2026-03-30 — Fix Club Filter Bar: 8→6 tabs + unified WEDGES
✅ DONE: 2026-03-30 — Fix filter button raycast targets
✅ DONE: 2026-04-06 — Phase K Step 1: HoleImporter.cs, HoleManifestData.cs, HoleMetadata.cs, WalkCamera.cs — terrain from heightmap, stitched satellite texture, tee anchor markers, WASD walk camera (New Input System)
✅ DONE: 2026-04-06 — Phase K Step 2: Aerial texture alignment fix — crop stitched tile grid to hole_bounds before applying to terrain
✅ DONE: 2026-04-06 — Phase K Step 3: North/south axis fix — flip heightmap rows and aerial texture vertically so north=-Z matches UHole anchor convention
✅ DONE: 2026-04-06 — Phase K Step 4: Horizontal flip (negate X for anchors/camera, flip heightmap+texture on X axis) + alignment debug logging
✅ DONE: 2026-04-06 — Phase K Step 4b: Separated texture flips into two explicit passes (vertical then horizontal) for clarity; heightmap keeps heights[res-1-y, res-1-x]
✅ DONE: 2026-04-06 — Phase K Step 5: Reverted all X-axis flips; back to vertical-only flip for heightmap and texture
✅ DONE: 2026-04-06 — Phase K Step 6: Replaced crop-based alignment with UV offset mapping — full stitched texture + tileSize/tileOffset computed from grid geo bounds
✅ DONE: 2026-04-06 — Phase K Step 7: Definitive texture alignment — pixel-by-pixel geo sampling from tiles using same bounds as heightmap, tileSize=terrain, tileOffset=zero
✅ DONE: 2026-04-06 — Phase K Step 8: Fixed texture horizontal flip — reversed U sampling direction (1-u) in ApplyAerialTexture() so satellite features align with anchor positions

---

## Completed Task — Phase K Step 2: Fix Aerial Texture Alignment

**Problem:** The satellite texture is misaligned and oversized relative to the terrain.
The stitched tile grid covers a larger area than the terrain bounds, but the TerrainLayer
maps the texture to fill the entire terrain surface. This causes the aerial photo to be
shifted and scaled incorrectly.

### Root Cause

In `ApplyAerialTexture()`, the tile grid spans from the northwest corner of the
northwest-most tile to the southeast corner of the southeast-most tile. This is a
larger rectangle than the terrain bounds (which come from the hole manifest).

Currently:
```csharp
layer.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
layer.tileOffset = Vector2.zero;
```

This stretches the entire tile grid texture across the terrain, causing misalignment.

### Fix

The texture should map 1:1 to real-world coordinates. The approach:

1. **Compute the tile grid's world-space extent** from the tile bounds in `aerial-tiles.json`:
   ```csharp
   // The full tile grid spans:
   double gridNorth = tiles.Max(t => t.bounds.north);
   double gridSouth = tiles.Min(t => t.bounds.south);
   double gridEast = tiles.Max(t => t.bounds.east);
   double gridWest = tiles.Min(t => t.bounds.west);
   ```

2. **Convert to meters** (same math as UHole export):
   ```csharp
   double centerLat = (gridNorth + gridSouth) / 2.0;
   float gridWidthM = (float)((gridEast - gridWest) * 111320.0 * Math.Cos(centerLat * Math.PI / 180.0));
   float gridHeightM = (float)((gridNorth - gridSouth) * 111320.0);
   ```

3. **Compute the offset** of the terrain's NW corner relative to the tile grid's NW corner:
   ```csharp
   // The terrain is centered at origin (0,0,0). Its NW corner in world space:
   float terrainWestX = -manifest.terrain.terrain_width_m / 2f;  // same as terrain position.x
   float terrainNorthZ = manifest.terrain.terrain_length_m / 2f; // north = +Z (top)

   // Read the hole bounds from manifest (these define the terrain extent)
   // The tile grid's NW corner in the same local coordinate system:
   // grid NW lat/lon -> local meters from the origin
   double originLat = manifest.origin.lat;
   double originLon = manifest.origin.lon;
   float gridNWLocalX = (float)((gridWest - originLon) * 111320.0 * Math.Cos(originLat * Math.PI / 180.0));
   float gridNWLocalZ = -(float)((gridNorth - originLat) * 111320.0); // negative because north = higher lat but UHole uses z = -dLat
   ```
   
   Wait — need to be careful about the z-axis convention. Check how anchors use local coords:
   `local.z = -dLatM` (from UHole's `latLonToMetersOffset`). So north (higher lat) = more negative z.
   
   But Unity terrain is placed at `position.z = -terrain_length_m / 2`. The terrain's north edge
   is at `z = +terrain_length_m / 2` in Unity world space... actually no. The terrain starts at
   its transform position and extends in +X and +Z. So:
   - Terrain west edge: `x = -width/2` (transform.position.x)
   - Terrain south edge: `z = -length/2` (transform.position.z)
   - Terrain east edge: `x = +width/2`
   - Terrain north edge: `z = +length/2`

   The UHole convention: `local.z = -(lat - originLat) * 111320`. So higher lat = more negative z.
   That means **north = -Z** in the local coord system.

   So the terrain's layout is:
   - south edge (lower lat) = `z = +length/2` (terrain extends in +Z from its position)
   - north edge (higher lat) = `z = -length/2` (terrain position)
   
   Wait, this is getting confusing. Let me think about this differently.

### Simpler Approach: Crop the texture to terrain bounds

Instead of computing offsets, **crop the stitched texture** to exactly match the terrain
bounds before applying it. This guarantees 1:1 alignment.

1. Stitch all tiles into the full grid texture (as currently done)
2. Compute which pixel rectangle of the stitched texture corresponds to the terrain bounds
3. Crop to that rectangle
4. Apply the cropped texture with `tileSize = terrain size`

```csharp
// After stitching all tiles into `stitched` texture...

// Tile grid world bounds
double gridNorth = tiles.Max(t => t.bounds.north);
double gridSouth = tiles.Min(t => t.bounds.south);
double gridEast = tiles.Max(t => t.bounds.east);
double gridWest = tiles.Min(t => t.bounds.west);

// Hole bounds from manifest (= terrain bounds)
double holeNorth = tilesData.hole_bounds.north;
double holeSouth = tilesData.hole_bounds.south;
double holeEast = tilesData.hole_bounds.east;
double holeWest = tilesData.hole_bounds.west;

// Normalized positions of hole bounds within the tile grid
float uMin = (float)((holeWest - gridWest) / (gridEast - gridWest));  // left edge
float uMax = (float)((holeEast - gridWest) / (gridEast - gridWest));  // right edge
float vMin = (float)((gridNorth - holeNorth) / (gridNorth - gridSouth)); // top edge (note: texture Y is flipped)
float vMax = (float)((gridNorth - holeSouth) / (gridNorth - gridSouth)); // bottom edge

// Convert to pixel coords in the stitched texture
// Note: texture pixel (0,0) is bottom-left in Unity, but we built it with
// row 0 = north (top). So vMin corresponds to the top of the image.
// In Unity texture space, y=0 is bottom, y=height is top.
// When we stitched: row = maxY - tile.y, so row=0 = northernmost tile = top of image = high y in texture.
// So texture y=texHeight corresponds to north, y=0 corresponds to south.
int cropLeft = Mathf.RoundToInt(uMin * texWidth);
int cropRight = Mathf.RoundToInt(uMax * texWidth);
int cropBottom = Mathf.RoundToInt((1f - vMax) * texHeight); // vMax = south edge, bottom of image
int cropTop = Mathf.RoundToInt((1f - vMin) * texHeight);    // vMin = north edge, top of image

int cropW = cropRight - cropLeft;
int cropH = cropTop - cropBottom;

// Clamp
cropLeft = Mathf.Max(0, cropLeft);
cropBottom = Mathf.Max(0, cropBottom);
cropW = Mathf.Min(cropW, texWidth - cropLeft);
cropH = Mathf.Min(cropH, texHeight - cropBottom);

// Extract cropped pixels
var croppedPixels = stitched.GetPixels(cropLeft, cropBottom, cropW, cropH);
var cropped = new Texture2D(cropW, cropH, TextureFormat.RGB24, false);
cropped.SetPixels(croppedPixels);
cropped.Apply();

// Use `cropped` instead of `stitched` for saving
```

Then the `TerrainLayer` can use:
```csharp
layer.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
layer.tileOffset = Vector2.zero;
```

Because the texture now exactly matches the terrain extent.

### Also check: Terrain north/south orientation

The heightmap rows may be inverted relative to the terrain. Verify:
- Row 0 of the heightmap = north (highest lat) or south?
- Unity terrain pixel [0,0] = which corner?

Unity `TerrainData.SetHeights(0, 0, heights)` treats `heights[0,0]` as the **northwest** corner
(x=0, z=max). Verify the UHole export writes row 0 = north. If it writes row 0 = south,
the heightmap needs to be flipped vertically.

Same for the aerial texture — make sure north is at the top.

If the heightmap and texture are both flipped the same way, they'll match each other
but might be upside-down on the terrain. Compare the terrain features (hills, valleys)
to the satellite image to verify orientation.

### Verification

- [ ] Aerial texture aligns with terrain features (roads, fairways, buildings match)
- [ ] Texture covers only the hole area, not extra surrounding tiles
- [ ] Terrain orientation matches real-world north (compare to satellite reference)
- [ ] Anchor markers are on the correct fairway features when viewed from above
- [ ] No texture stretching or rotation artifacts
- [ ] Re-import still works cleanly

### Do NOT change

- The UHole export scripts or data
- Anchor placement or transform computation
- WalkCamera or HoleMetadata
- Scene hierarchy structure

---

## Current Task — Phase K Step 3: Fix North/South Axis Alignment

**Problem:** The aerial texture and heightmap are misaligned because of a north/south
axis conflict between the UHole export convention and Unity's terrain system.

### The Conflict

**UHole exports:**
- Heightmap row 0 = north (highest latitude)
- `local.z = -(lat - originLat) * 111320` → **north = negative Z**
- Anchor tee_back has `local.z = -135.89` (south of origin in real world, but this
  means negative Z = south... wait, back tee IS south of the green, so negative Z = south
  and positive Z = north? No: the formula is `z = -(lat - origin) * 111320`. Back tee has
  higher lat than green (34.9152 vs 34.9137 for CPs near green), so `z = -(positive) = negative`.
  So higher lat = more negative Z. **North = negative Z in UHole.**)

**Unity terrain:**
- `Terrain.transform.position` is the terrain's origin corner
- Terrain extends in **+X** and **+Z** from that corner
- Currently placed at `(-width/2, 0, -length/2)`, extending to `(+width/2, 0, +length/2)`
- `SetHeights(0, 0, heights)`: `heights[0,0]` = the corner at terrain position (min X, max Z)
  = the **northwest** corner of the terrain

### What's Happening

The heightmap row 0 (north) is being placed at `heights[0,0]` which Unity puts at max Z.
Since terrain extends from `-length/2` to `+length/2`, max Z = `+length/2`.
So **north = +Z** in the terrain.

But UHole says **north = -Z** in its local coords.

So the heightmap terrain has north at +Z, but the anchors have north at -Z. They're flipped.

### The Fix

The simplest fix: **flip the heightmap rows** when loading, so that row 0 (north in UHole)
maps to `heights[res-1, x]` (south edge of terrain in Unity). This makes the terrain
match UHole's convention where north = -Z.

Alternatively, flip the anchor Z coords. But flipping the heightmap is cleaner since
it's one change in one place.

**In `CreateTerrain()`**, flip the row order:

```csharp
for (int y = 0; y < res; y++)
{
    for (int x = 0; x < res; x++)
    {
        int idx = (y * res + x) * 2;
        ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]);
        // Flip vertically: UHole row 0 = north, but Unity heights[0,x] = max Z.
        // We want north at -Z (to match UHole local coords), so put row 0
        // at heights[res-1-y, x] (which maps to min Z = south edge... no, 
        // heights[res-1, x] = min Z... 
    }
}
```

Actually, let me think about this more carefully.

Unity `heights[row, col]`:
- `row` goes from 0 to res-1
- `row=0` corresponds to terrain position Z + terrainData.size.z (the **far** edge, max Z)
- `row=res-1` corresponds to terrain position Z (the **near** edge, min Z)  
- So `heights[0,x]` = max Z = `+length/2` and `heights[res-1,x]` = min Z = `-length/2`

UHole heightmap:
- `rawRow=0` = north (highest lat)
- North should be at **-Z** (per UHole's `z = -dLat` convention)
- So north (rawRow=0) should map to min Z = `-length/2` = `heights[res-1, x]`

Therefore: `heights[res - 1 - y, x] = rawValue[y, x]`

```csharp
for (int y = 0; y < res; y++)
{
    for (int x = 0; x < res; x++)
    {
        int idx = (y * res + x) * 2;
        ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]);
        heights[res - 1 - y, x] = val / 65535f;
    }
}
```

**For the aerial texture**, the same flip applies. Currently the stitched texture has
north at the top (high Y in texture space). When applied to the terrain with the flipped
heightmap, we need the texture's north to also map to -Z. 

The terrain layer maps texture V=0 to terrain min Z, V=1 to terrain max Z.
If north is at min Z (-length/2), then texture V=0 should be north = top of aerial image.

But Unity textures have Y=0 at bottom. So texture Y=0 = south, Y=max = north.
TerrainLayer UV: U goes along terrain X, V goes along terrain Z.
V=0 = min Z (which is now north after our flip) and V=1 = max Z (south).

So we need texture V=0 = north = top of image. But texture Y=0 is bottom.
So V=0 maps to Y=0 which is bottom of image. We need bottom of image = north.

**That means we need to flip the aerial texture vertically too** (so north is at the
bottom of the image, Y=0).

In `ApplyAerialTexture()`, after cropping, flip the texture:

```csharp
// Flip vertically so north is at Y=0 (matching terrain V=0 = min Z = north)
var flippedPixels = new Color[cropW * cropH];
for (int row = 0; row < cropH; row++)
{
    for (int col = 0; col < cropW; col++)
    {
        flippedPixels[row * cropW + col] = croppedPixels[(cropH - 1 - row) * cropW + col];
    }
}
cropped.SetPixels(flippedPixels);
cropped.Apply();
```

### Summary of Changes

1. **`CreateTerrain()`**: Flip heightmap rows: `heights[res-1-y, x] = rawValue[y, x]`
2. **`ApplyAerialTexture()`**: Flip the cropped texture vertically before saving
3. **No changes** to anchor coords or UHole export — the Z axis convention stays as-is

### Verification

- [ ] Looking down from above in Unity Scene view, the aerial photo features
      (clubhouse, roads, fairways) align with the terrain contours (hills, valleys)
- [ ] Anchor markers are on the correct features (tees on the tee area, green on green)
- [ ] Walking in +Z moves you south (toward tees), -Z moves you north (toward green)
- [ ] The terrain shape matches the satellite imagery — hills are where hills should be
- [ ] Re-import still works cleanly

### Do NOT change

- UHole export scripts or data
- Anchor local coordinate values
- Scene hierarchy structure
- WalkCamera or HoleMetadata

---

## Current Task — Phase K Step 4: Horizontal Flip + Texture Alignment Fix

Two remaining issues before the prototype is approved.

### Issue 1: Horizontal flip

The aerial map and anchor points need to be flipped on the X axis so the Unity scene
visually matches the official hole map when viewed from above. Currently, the hole is
mirrored compared to the official illustration.

**Root cause:** UHole uses `local.x = dLonM` (east = +X). The official map has its own
artistic orientation where the tees are lower-right and green is upper-left. In Unity's
top-down view, the scene is geographically correct but doesn't match the official map
perspective.

**Fix in `HoleImporter.cs`:** Negate the X coordinate when placing anchors and when
positioning the walk camera. This mirrors the scene to match the official map orientation.

```csharp
// In PlaceAnchorMarker():
Vector3 worldPos = new Vector3(-anchor.local.x, 0f, anchor.local.z);  // negate X

// In CreateWalkCamera():
Vector3 pos = new Vector3(-backTee.local.x, 0f, backTee.local.z);  // negate X
```

**Also flip the aerial texture horizontally** in `ApplyAerialTexture()`.
After cropping (and after the existing vertical flip), add a horizontal flip:

```csharp
// Flip horizontally to match mirrored X axis
var hFlippedPixels = new Color[cropW * cropH];
for (int row = 0; row < cropH; row++)
{
    for (int col = 0; col < cropW; col++)
    {
        hFlippedPixels[row * cropW + col] = currentPixels[row * cropW + (cropW - 1 - col)];
    }
}
cropped.SetPixels(hFlippedPixels);
cropped.Apply();
```

**Also flip the heightmap on the X axis** in `CreateTerrain()`:

```csharp
heights[res - 1 - y, res - 1 - x] = val / 65535f;  // flip both Y (existing) and X (new)
```

This ensures terrain elevation, satellite texture, and anchor positions are all
consistently mirrored.

### Issue 2: Texture-to-terrain alignment

The satellite texture is still slightly offset from the terrain contours. The crop
calculation may have a sub-tile error.

**Debug approach:** Add a debug log that prints:
- The tile grid geo bounds (gridNorth/South/East/West)
- The hole bounds (holeNorth/South/East/West)
- The normalized crop values (uMin, uMax, vNorthNorm, vSouthNorm)
- The pixel crop rect (cropLeft, cropBottom, cropW, cropH)
- The full texture size vs cropped size

Compare the crop bounds to the terrain bounds — they should match exactly.
If there's a systematic offset, it's likely because:

1. The tile grid has gaps (missing tiles in the grid that shift positions), OR
2. The crop is computing from tile index bounds rather than geo bounds, OR  
3. The terrain position and texture UV mapping have a half-pixel offset

**Verify the crop is correct** by checking:
- Does `holeWest` fall between `gridWest` and `gridEast`? (should be yes)
- Is `uMin` between 0 and 1? (should be yes)
- Does `cropLeft / texWidth` ≈ `uMin`? (should match closely)

If the crop rect looks correct but the texture still doesn't align, the issue is in
how Unity maps the TerrainLayer texture to the terrain mesh. Try adjusting the
`tileOffset` to compensate:

```csharp
// If the texture appears shifted by some fraction, try:
layer.tileOffset = new Vector2(offsetX, offsetZ);  // small values like 0.01-0.05
```

But first, log the debug values and check the crop math.

### Verification

- [ ] Top-down view in Unity matches the official hole map orientation
      (green upper area, tees lower area, same left-right arrangement)
- [ ] Anchor markers match their official map positions
- [ ] Aerial texture features (bunkers, tree lines, fairway edges) align with
      terrain contours (hills and valleys)
- [ ] Walking from back tee toward green follows the expected path
- [ ] No texture stretching or obvious seam artifacts

### Do NOT change

- UHole export scripts or anchor data
- The affine transform or control points
- Scene hierarchy structure
- HoleMetadata fields

---

## Current Task — Phase K Step 5: Definitive Orientation Fix

**COMPREHENSIVE ANALYSIS — READ THIS FULLY BEFORE CODING**

I traced the full coordinate chain from UHole export through Unity rendering with
actual numbers. Here is the definitive answer.

### The Data (from Hole 1 export)

UHole `latLonToMetersOffset` computes: `x = dLonM` (east=+X), `z = -dLatM` (north=-Z)

Anchor local coords from UHole:
- Green center: `x = -232.54, z = +60.05` (west of origin, south of origin)
- Back tee:     `x = +232.54, z = -60.05` (east of origin, north of origin)

Unity Scene view (top-down, default): +X = right on screen, +Z = up on screen.

So WITHOUT any coordinate modification:
- Green at `(-232, +60)` = **left side, upper area** = top-left ✓
- Back tee at `(+232, -60)` = **right side, lower area** = bottom-right ✓

**This already matches the official map orientation!** Green top-left, tees bottom-right.

### What went wrong

The `-anchor.local.x` negation (added in Step 4) FLIPPED the correct orientation.
It moved green to top-RIGHT and tees to bottom-LEFT — which is the mirror image.

### The Fix — Remove ALL flips, then add back only what's needed

**STEP A: Anchors and camera — use raw local coords, NO negation:**

```csharp
// In PlaceAnchorMarker():
Vector3 worldPos = new Vector3(anchor.local.x, 0f, anchor.local.z);  // NO negation

// In CreateWalkCamera():
Vector3 pos = new Vector3(backTee.local.x, 0f, backTee.local.z);  // NO negation
```

**STEP B: Heightmap — flip Y only (row order), NOT X (column order):**

UHole heightmap row 0 = north (high lat). We need north to map to negative Z
(which is Unity heights row `res-1`). So flip rows only:

```csharp
heights[res - 1 - y, x] = val / 65535f;  // flip Y only, NO X flip
```

**STEP C: Aerial texture — flip vertically only, NOT horizontally:**

The stitched texture has north at the top (high Y in Unity tex space). The terrain
has north at min Z. TerrainLayer V=0 maps to min Z (north). Texture Y=0 is bottom.
So we need north at Y=0 = vertical flip.

NO horizontal flip.

```csharp
// ONLY vertical flip:
var vFlipped = new Color[cropW * cropH];
for (int row = 0; row < cropH; row++)
{
    for (int col = 0; col < cropW; col++)
    {
        vFlipped[row * cropW + col] = croppedPixels[(cropH - 1 - row) * cropW + col];
    }
}
cropped.SetPixels(vFlipped);
cropped.Apply();

// DO NOT add a horizontal flip
```

### Summary of ALL changes to HoleImporter.cs

1. `CreateTerrain()`: Change `heights[res-1-y, res-1-x]` to `heights[res-1-y, x]`
2. `ApplyAerialTexture()`: Remove the horizontal flip pass entirely. Keep ONLY the vertical flip.
3. `PlaceAnchorMarker()`: Change `new Vector3(-anchor.local.x, ...)` to `new Vector3(anchor.local.x, ...)`
4. `CreateWalkCamera()`: Change `new Vector3(-backTee.local.x, ...)` to `new Vector3(backTee.local.x, ...)`

### Why This Works

- UHole's convention: east=+X, south=+Z (because z = -dLat, and south = lower lat = negative dLat = positive z)
- Unity Scene view: +X=right, +Z=up
- Result: east=right, south=up... wait.

Actually let me reconsider. In Unity top-down: +Z = up on screen. UHole: south = +Z.
So south is UP on screen. That means the hole will appear rotated 180° from compass north.
But that's fine because the OFFICIAL MAP also shows the hole with its artistic orientation,
not compass north. And the official map has green at top, tees at bottom, which matches
green=(-X,+Z)=top-left in Unity.

The key insight: we're NOT trying to match compass orientation. We're trying to match
the official map illustration. And the raw UHole coords already do that.

### Verification

- [ ] Green marker appears at top-left area of terrain (matching official map)
- [ ] Tee markers appear at bottom-right area of terrain (matching official map)
- [ ] OB should be on the right side of the fairway (matching official map)
- [ ] Aerial texture features align with terrain elevation contours
- [ ] Clubhouse visible on the right/lower side of terrain (matching real geography)

### Do NOT

- Add any coordinate negation to anchors
- Flip the heightmap on X axis
- Flip the texture horizontally
- Change UHole export code

---

## Current Task — Phase K Step 9: Remove Diagnostic Markers

**The texture-terrain alignment fix is confirmed.** The diagnostic showed that Unity's
`SetHeights` uses `heights[x_index, z_index]` (not `[z, x]` as the docs imply).
The fix: `heights[res-1-x, res-1-y]` where y=raw row (north-south) and x=raw col (east-west).

### Changes

1. **`export-hole.mjs`**: Remove the diagnostic NW corner bump code (the `for` loop
   that sets heightmap cells to maxElev). Keep everything else.

2. **`HoleImporter.cs`**: Remove the diagnostic red square code (the `for` loop
   that sets outputPixels to Color.red). Keep everything else.
   The heightmap indexing `heights[res-1-x, res-1-y]` is CORRECT — do NOT change it.

3. Re-export: `node scripts/export-hole.mjs 1`
4. Re-import in Unity: GOLFIN > Import Hole > Hole 01

### Verification

- [ ] No red square visible on terrain
- [ ] No artificial elevation bump visible
- [ ] Aerial texture features align with terrain contours
- [ ] Anchor markers sit on correct features

### Do NOT

- Change the heightmap indexing (it's correct now)
- Change the texture sampling
- Change anchor coordinates

**Problem:** The aerial texture is horizontally flipped relative to the anchor positions
in Unity. The anchors land on the correct hole features (verified in the UHole alignment
tool), but the satellite image underneath is mirrored left-to-right.

**Root cause:** The anchor world coordinates now come directly from basemap tile
geo-bounds (from the v2 alignment tool). The texture is generated from the same tiles.
But the texture sampling or the heightmap X-axis mapping doesn't match.

The anchors use: `local.x = (lon - originLon) * metersPerDegLon` (east = +X).
In Unity, +X = right on screen. So east should be on the right.

The texture is sampled with `U=0 = west, U=1 = east`. In Unity's TerrainLayer,
`U=0` maps to terrain min X (left). So texture left = west = left on screen. This
should match. But if the texture appears flipped, the sampling loop might have
U going in the wrong direction.

**Fix:** In `ApplyAerialTexture()`, check the pixel sampling loop. The line:
```csharp
double lon = holeWest + u * (holeEast - holeWest);
```
should produce west at U=0 (left of texture) and east at U=1 (right of texture).
If the texture appears flipped, the fix is to reverse U:
```csharp
double lon = holeEast - u * (holeEast - holeWest);
```

OR the heightmap X axis may be reversed. Check `CreateTerrain()` — the current code
uses `heights[res-1-y, x]`. If the heightmap col 0 = west but Unity terrain min X
is on the left and the terrain is positioned at `-width/2`, then col 0 should map
to the left edge. Verify this matches the texture orientation.

**Debug approach:** Place a known asymmetric anchor (e.g., green_center at the
northwest corner of the hole) and check which side of the Unity terrain it appears on.
Then check which side of the texture the corresponding aerial feature (green) appears on.
If they're on opposite sides, the texture U-direction is inverted.

**The simplest fix:** Just flip the U sampling in the texture loop:
```csharp
// Change:
float u = (float)px / (texRes - 1);
// To:
float u = 1f - (float)px / (texRes - 1);
```

This reverses which side of the texture maps to which side of the terrain.

### Verification

- [ ] Green marker (top-right area in official map) sits on the green in the aerial texture
- [ ] Tee markers (bottom-left area) sit on the tee boxes in the aerial texture
- [ ] Clubhouse in aerial photo matches terrain elevation flat area
- [ ] OB boundary on the correct side of the fairway

### Do NOT

- Change anchor coordinates
- Change UHole export or alignment tool
- Change heightmap row/col indexing (Step 5 is correct)

---

## Current Task — Phase K Step 6: Fix Texture-to-Terrain Offset

**Problem:** The aerial texture is shifted relative to the terrain elevation. Looking from
above, satellite features (roads, buildings, fairway edges) don't line up with the
corresponding terrain contours (hills, valleys). The orientation is now correct (Step 5
fixed that), but there's a positional offset.

### Diagnosis

The terrain and texture both derive from the same hole bounds, but they use different
data sources (DEM tiles for terrain, photo tiles for texture). The offset likely comes
from the texture crop calculation not exactly matching the terrain's geographic extent.

The terrain covers exactly `hole_bounds` (from the manifest). The texture is cropped
from a larger tile grid. If the crop rectangle is off by even a few pixels, the texture
shifts by meters on the terrain.

### Fix: Bypass cropping — compute texture UV mapping directly

Instead of cropping the stitched texture and hoping it aligns, compute the exact
TerrainLayer tileSize and tileOffset to map the FULL stitched tile grid texture
onto the terrain correctly. This is mathematically precise.

The approach:

1. **Keep the full stitched texture** (don't crop at all)
2. **Compute the tile grid's world-space size in meters:**
   ```csharp
   double gridNorth = tiles.Max(t => t.bounds.north);
   double gridSouth = tiles.Min(t => t.bounds.south);
   double gridEast = tiles.Max(t => t.bounds.east);
   double gridWest = tiles.Min(t => t.bounds.west);
   double centerLat = (gridNorth + gridSouth) / 2.0;
   float gridWidthM = (float)((gridEast - gridWest) * 111320.0 * System.Math.Cos(centerLat * System.Math.PI / 180.0));
   float gridHeightM = (float)((gridNorth - gridSouth) * 111320.0);
   ```

3. **Set tileSize to the grid's world size** (the texture covers this many meters):
   ```csharp
   layer.tileSize = new Vector2(gridWidthM, gridHeightM);
   ```

4. **Compute tileOffset** to position the grid correctly relative to the terrain.
   The terrain starts at `(-terrainWidth/2, -terrainLength/2)` in world space.
   The tile grid's SW corner (min lon, min lat) in local meters is:
   ```csharp
   // origin = center of hole bounds
   double originLat = (holeBounds.north + holeBounds.south) / 2.0;
   double originLon = (holeBounds.east + holeBounds.west) / 2.0;
   
   // Grid SW corner in local coords (same convention as UHole: x=dLon, z=-dLat)
   float gridSWx = (float)((gridWest - originLon) * 111320.0 * System.Math.Cos(originLat * System.Math.PI / 180.0));
   float gridSWz = -(float)((gridSouth - originLat) * 111320.0);
   // gridSWz is the Z position of the grid's south edge in local coords
   ```

   The terrain's SW corner is at `(-terrainWidth/2, -terrainLength/2)` in Unity.
   But wait — the terrain position has north at min Z (due to our Y-flip convention).
   
   Actually, let's think about this differently. The TerrainLayer UV system:
   - U=0 maps to terrain X=0 (which is terrain position.x = -width/2)
   - V=0 maps to terrain Z=0 (which is terrain position.z = -length/2)
   - The texture tiles across the terrain surface
   
   `tileOffset` shifts the texture. A positive offset moves the texture in the +U/+V direction.
   
   Since the vertical flip puts north at texture Y=0 / terrain min Z, and we're NOT
   cropping anymore, we need to figure out where the hole bounds sit within the tile grid
   and offset accordingly.
   
   Fraction of tile grid that's west of the hole's west edge:
   ```csharp
   float fracWest = (float)((holeWest - gridWest) / (gridEast - gridWest));
   float fracSouth = (float)((holeSouth - gridSouth) / (gridNorth - gridSouth));
   // After vertical flip, fracSouth becomes the offset from texture top
   float fracNorth = (float)((gridNorth - holeNorth) / (gridNorth - gridSouth));
   ```
   
   The offset in meters:
   ```csharp
   float offsetX = -fracWest * gridWidthM;
   float offsetZ = -fracNorth * gridHeightM; // fracNorth because of vertical flip
   layer.tileOffset = new Vector2(offsetX, offsetZ);
   ```

This is getting complicated with the flip. Let me simplify.

### Simpler approach: just apply the vertical flip to the full stitched texture and compute offset

1. Stitch all tiles into the full grid (as now)
2. Apply vertical flip to the FULL stitched texture (not cropped)
3. Save the full texture
4. Compute tileSize = grid world size in meters
5. Compute tileOffset from the difference between the grid's corner and the terrain's corner

```csharp
// After stitching and applying vertical flip to full texture...

// Grid world size
float gridWidthM = (float)((gridEast - gridWest) * 111320.0 * System.Math.Cos(centerLat * System.Math.PI / 180.0));
float gridHeightM = (float)((gridNorth - gridSouth) * 111320.0);

layer.tileSize = new Vector2(gridWidthM, gridHeightM);

// The terrain surface goes from (-terrainW/2, -terrainL/2) to (+terrainW/2, +terrainL/2).
// At terrain corner (-terrainW/2, -terrainL/2), the UV is (0,0).
// We need UV(0,0) to map to the grid's NW corner (after flip: north at Y=0).
// 
// The grid's NW corner in local space:
//   x_nw = (gridWest - originLon) * metersPerDegLon  (negative, since grid extends west of origin)
//   z_nw = -(gridNorth - originLat) * metersPerDegLat  (negative, since north = -Z)
//
// The terrain's NW corner (min X, min Z):
//   x_terrain_nw = -terrainWidth/2
//   z_terrain_nw = -terrainLength/2
//
// The offset is the difference:
float metersPerDegLon = (float)(111320.0 * System.Math.Cos(centerLat * System.Math.PI / 180.0));
float metersPerDegLat = 111320f;

float gridNWx = (float)((gridWest - originLon) * metersPerDegLon);
float gridNWz = -(float)((gridNorth - originLat) * metersPerDegLat);
float terrainNWx = -terrainData.size.x / 2f;
float terrainNWz = -terrainData.size.z / 2f;

// UV offset in meters: how much to shift texture so grid NW aligns with terrain NW
float offsetX = terrainNWx - gridNWx;
float offsetZ = terrainNWz - gridNWz;

layer.tileOffset = new Vector2(offsetX, offsetZ);
```

Read `originLat` and `originLon` from the manifest, or compute from hole bounds:
```csharp
double originLat = (tilesData.hole_bounds.north + tilesData.hole_bounds.south) / 2.0;
double originLon = (tilesData.hole_bounds.east + tilesData.hole_bounds.west) / 2.0;
```

### Implementation

Replace the entire crop logic in `ApplyAerialTexture()` with:
1. Stitch tiles into full grid texture (keep existing code)
2. Flip vertically (keep existing code, but on full texture, not cropped)
3. Save the full texture (no cropping)
4. Compute tileSize and tileOffset as shown above
5. Apply to TerrainLayer

Remove all crop-related code (uMin, uMax, vNorthNorm, vSouthNorm, cropLeft, etc.).

The manifest reference is needed — pass it into `ApplyAerialTexture()` or extract
origin lat/lon from tilesData.hole_bounds.

### Verification

- [ ] Aerial texture roads align with terrain elevation dips where roads should be
- [ ] Bunkers (white patches in aerial) align with terrain depressions
- [ ] Tree shadows in aerial photo align with terrain ridges
- [ ] Clubhouse building in aerial aligns with flat terrain area
- [ ] Anchor markers sit on the correct aerial features (tees on tee boxes, green on green)

### Do NOT

- Change anchor coordinates or heightmap orientation (Step 5 fixed those correctly)
- Change UHole export code
- Add any X negation

---

## Current Task — Phase K Step 7: Definitive Texture Alignment

**PROBLEM:** The aerial texture is still offset from the terrain contours. The `tileOffset`
approach didn't fix it because `TerrainLayer.tileOffset` behavior is unreliable/underdocumented.

**SOLUTION:** Eliminate the offset entirely. Stitch ONLY the pixels that map to the hole
bounds, then apply with `tileSize = terrain size` and `tileOffset = zero`. This guarantees
perfect 1:1 alignment because the texture covers exactly the same geographic area as the terrain.

### The Approach: Render texture pixel-by-pixel from geo coordinates

Instead of stitching tiles into a grid and trying to crop/offset, generate the aerial
texture by sampling tile pixels using the SAME geo-coordinate loop as the heightmap.
This guarantees both use identical geographic mapping.

**Replace the entire `ApplyAerialTexture` method with this logic:**

```csharp
private static void ApplyAerialTexture(TerrainData terrainData, AerialTilesData tilesData,
    HoleManifest manifest, string exportPath, string dataDir, string projectRoot)
{
    var tiles = tilesData.tiles;
    if (tiles == null || tiles.Length == 0) return;

    // Load all tile textures into a lookup
    // Key: "x_y" tile indices, Value: Color[] pixels (256x256)
    var tilePixels = new System.Collections.Generic.Dictionary<string, Color[]>();
    foreach (var tile in tiles)
    {
        string tilePath = Path.Combine(exportPath,
            tile.path.Replace("/", Path.DirectorySeparatorChar.ToString()));
        tilePath = Path.GetFullPath(tilePath);
        if (!File.Exists(tilePath)) continue;

        var tileTex = new Texture2D(256, 256);
        tileTex.LoadImage(File.ReadAllBytes(tilePath));
        tilePixels[$"{tile.x}_{tile.y}"] = tileTex.GetPixels();
        Object.DestroyImmediate(tileTex);
    }

    // Output texture resolution (match terrain heightmap for simplicity,
    // or use a higher res for better quality)
    int texRes = 512; // higher than 129 for visual quality

    // Hole bounds = terrain bounds
    double holeNorth = manifest.bounds.north;
    double holeSouth = manifest.bounds.south;
    double holeEast = manifest.bounds.east;
    double holeWest = manifest.bounds.west;

    var outputTex = new Texture2D(texRes, texRes, TextureFormat.RGB24, false);
    var outputPixels = new Color[texRes * texRes];
    Color defaultColor = new Color(0.1f, 0.3f, 0.1f);

    for (int py = 0; py < texRes; py++)
    {
        for (int px = 0; px < texRes; px++)
        {
            // Map output pixel to geographic coordinates
            // SAME logic as heightmap: row 0 = north, but we need to match
            // the terrain's orientation after the row flip.
            //
            // In the terrain (after row flip):
            //   Unity heights[0, x] = max Z = south
            //   Unity heights[res-1, x] = min Z = north
            //
            // TerrainLayer UV:
            //   V=0 → terrain min Z (north)
            //   V=1 → terrain max Z (south)
            //   U=0 → terrain min X (west)
            //   U=1 → terrain max X (east)
            //
            // Texture pixel (px=0, py=0) is bottom-left in Unity = (U=0, V=0) = (west, north)
            // So pixel row py=0 (bottom) = V=0 = north
            //    pixel row py=texRes-1 (top) = V=1 = south

            float u = (float)px / (texRes - 1);  // 0=west, 1=east
            float v = (float)py / (texRes - 1);  // 0=north (bottom of tex), 1=south (top of tex)

            double lon = holeWest + u * (holeEast - holeWest);
            double lat = holeNorth - v * (holeNorth - holeSouth);  // v=0→north, v=1→south

            // Find which tile contains this lat/lon
            Color sampled = defaultColor;
            foreach (var tile in tiles)
            {
                if (lon >= tile.bounds.west && lon < tile.bounds.east &&
                    lat <= tile.bounds.north && lat > tile.bounds.south)
                {
                    string key = $"{tile.x}_{tile.y}";
                    if (!tilePixels.ContainsKey(key)) break;

                    // Fractional position within tile
                    float fx = (float)((lon - tile.bounds.west) / (tile.bounds.east - tile.bounds.west));
                    float fy = (float)((tile.bounds.north - lat) / (tile.bounds.north - tile.bounds.south));
                    int tileCol = Mathf.Clamp((int)(fx * 256), 0, 255);
                    int tileRow = Mathf.Clamp((int)(fy * 256), 0, 255);

                    // Tile pixels from LoadImage are bottom-to-top in Unity
                    // tileRow 0 = north = top of image = high Y in Unity pixel array
                    int pixelIdx = (255 - tileRow) * 256 + tileCol;
                    sampled = tilePixels[key][pixelIdx];
                    break;
                }
            }

            outputPixels[py * texRes + px] = sampled;
        }
    }

    outputTex.SetPixels(outputPixels);
    outputTex.Apply();

    // Save texture
    string texturePath = $"{dataDir}/aerial_hole01.png";
    string fullTexPath = Path.Combine(projectRoot, texturePath);
    EnsureDirectory(Path.GetDirectoryName(fullTexPath));
    File.WriteAllBytes(fullTexPath, outputTex.EncodeToPNG());
    Object.DestroyImmediate(outputTex);

    AssetDatabase.ImportAsset(texturePath);
    var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
    if (importer != null)
    {
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    // TerrainLayer: texture covers terrain exactly, no offset needed
    var layer = new TerrainLayer();
    layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    layer.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
    layer.tileOffset = Vector2.zero;

    string layerPath = $"{dataDir}/TerrainLayer_Aerial.asset";
    AssetDatabase.CreateAsset(layer, layerPath);
    terrainData.terrainLayers = new TerrainLayer[] { layer };
}
```

### Key Points

1. The texture is rendered pixel-by-pixel using the same geographic bounds as the heightmap
2. `V=0` (texture bottom, py=0) = north = min Z = same as heightmap after row flip
3. `U=0` (texture left, px=0) = west = min X = same as heightmap col 0
4. Each pixel looks up which tile contains its lat/lon and samples from that tile
5. `tileSize = terrain size`, `tileOffset = zero` — because the texture matches exactly
6. No stitching, no cropping, no offset math, no flip passes

### Update the method signature

The method now needs the manifest for bounds. Update the call:

```csharp
// Change from:
ApplyAerialTexture(terrainData, tilesData, exportPath, dataDir, projectRoot);
// To:
ApplyAerialTexture(terrainData, tilesData, manifest, exportPath, dataDir, projectRoot);
```

### Tile pixel indexing note

`Texture2D.GetPixels()` returns pixels in bottom-to-top, left-to-right order.
So `pixels[0]` = bottom-left of image. For a satellite tile:
- Image top = north, image bottom = south
- `pixels[0]` = south-west corner
- `pixels[255*256 + 255]` = north-east corner
- Row index in pixel array: `(255 - tileRow) * 256 + tileCol` where tileRow 0 = north

### Verification

- [ ] Aerial texture features align EXACTLY with terrain elevation contours
- [ ] Roads in texture sit in terrain depressions/valleys
- [ ] Bunkers in texture align with terrain shapes
- [ ] Anchor markers sit on the correct aerial features
- [ ] No visible texture shift in any direction
- [ ] Texture resolution is adequate (512x512 for a ~625m terrain)

### Do NOT

- Change anchor coordinates or heightmap code (Step 5 is correct)
- Use tileOffset (set it to zero)
- Add any coordinate flipping/negation
- Change UHole export code
