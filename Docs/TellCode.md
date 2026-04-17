# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Previous Task — Shore Ramp Absolute Target (Hole 7 Geo Cliff)

Water is no longer hidden — but there's now a ~1.6m vertical cliff at
the upslope water boundary. Shore ramp runs its full 10 cells but the
cliff remains because the ramp can only subtract `ShoreDepthMeters`
(0.4m) — not enough on a slope that's 2m higher than the water surface.

**Root cause in one sentence:** `drop = ShoreDepthMeters * smoothstep(t)`
is a fixed-magnitude subtraction. When the terrain is 2m above waterY,
subtracting 0.4m at the boundary still leaves a 1.6m cliff.

**Fix:** Replace the subtractive ramp with a lerp that targets the water
surface height as an absolute value. At the boundary the ramp should
reach `waterSurfaceNorm` (the water mesh Y in normalized terrain units).
At `ShoreRadius` it should reach the original terrain height. Everything
in between is a smoothstep blend between those two absolute heights.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No pipeline changes.**

---

### Step 1 — Add per-body water SURFACE Y tracking

The previous task added `waterMask` and `waterFloorY` arrays. We now also
need per-cell `waterSurfaceY` for the shore ramp target.

In the water-mask building block, alongside `waterFloorY`:

```csharp
// Per-cell water SURFACE Y in normalized heightmap units.
// Needed by shore ramp so land can lerp to the correct water level
// per body (holes can have multiple bodies at different elevations).
float[,] waterSurfaceY = new float[hRes, hRes];
```

Inside the `foreach (var w in waterData.water)` loop, compute the surface
norm alongside the floor norm:

```csharp
// Water SURFACE Y in world units, then normalize.
// (Surface is at minTerrainH - 0.05m, same as CreateWaterMeshes.)
float surfaceWorldY = minTerrainH - 0.05f;
float surfaceNorm = Mathf.Clamp01(surfaceWorldY / elevRange);
```

In the `for (int z) for (int x)` loop that writes `waterMask` and
`waterFloorY`, also write `waterSurfaceY`:

```csharp
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
        if (bodyMask[z, x])
        {
            waterMask[z, x] = true;
            waterFloorY[z, x] = floorNorm;
            waterSurfaceY[z, x] = surfaceNorm;
        }
```

---

### Step 2 — Joint chamfer: propagate nearest body's surface Y with distance

The existing chamfer distance transform populates `distToWater[z, x]`.
Extend it to also track the surface Y of the nearest water body.

Replace the chamfer distance block in the shore slope pass with:

```csharp
// Joint chamfer: distToWater + nearest-body surfaceY propagation.
// Water cells start with dist=0 and their own surfaceY.
// Non-water cells inherit both from the nearest water neighbor.
float[,] distToWater = new float[hRes, hRes];
float[,] nearestSurfaceY = new float[hRes, hRes];
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
    {
        distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;
        nearestSurfaceY[z, x] = waterSurfaceY[z, x];
    }

// Forward pass
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
    {
        if (x > 0)
        {
            float cand = distToWater[z, x - 1] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z, x - 1]; }
        }
        if (z > 0)
        {
            float cand = distToWater[z - 1, x] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z - 1, x]; }
        }
        if (x > 0 && z > 0)
        {
            float cand = distToWater[z - 1, x - 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z - 1, x - 1]; }
        }
        if (x < hRes - 1 && z > 0)
        {
            float cand = distToWater[z - 1, x + 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z - 1, x + 1]; }
        }
    }
// Backward pass
for (int z = hRes - 1; z >= 0; z--)
    for (int x = hRes - 1; x >= 0; x--)
    {
        if (x < hRes - 1)
        {
            float cand = distToWater[z, x + 1] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z, x + 1]; }
        }
        if (z < hRes - 1)
        {
            float cand = distToWater[z + 1, x] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z + 1, x]; }
        }
        if (x < hRes - 1 && z < hRes - 1)
        {
            float cand = distToWater[z + 1, x + 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z + 1, x + 1]; }
        }
        if (x > 0 && z < hRes - 1)
        {
            float cand = distToWater[z + 1, x - 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z + 1, x - 1]; }
        }
    }
```

---

### Step 3 — Replace the subtractive ramp with an absolute-target lerp

Replace the shore-cell ramp loop:

```csharp
int shoreRadiusCells = ShoreRadius;

for (int z = 0; z < hRes; z++)
{
    for (int x = 0; x < hRes; x++)
    {
        if (waterMask[z, x]) continue;
        if (depress[z, x]) continue;
        if (cartDepress[z, x]) continue;

        float dist = distToWater[z, x];
        if (dist <= 0f || dist > shoreRadiusCells) continue;

        // t = 0 at the water boundary, 1 at shoreRadius.
        float t = dist / shoreRadiusCells;
        t = t * t * (3f - 2f * t); // smoothstep

        // Absolute target: lerp from water surface Y (at boundary)
        // to original terrain height (at shoreRadius). Works for any
        // slope magnitude because we target an absolute Y, not a
        // fixed-magnitude drop.
        float waterY = nearestSurfaceY[z, x];
        float originalH = heights[z, x];
        float targetH = Mathf.Lerp(waterY, originalH, t);

        // Only lower the terrain — never raise it. If the existing
        // height is already below the interpolated target (e.g., a
        // natural low spot next to water), leave it alone.
        if (targetH < originalH)
        {
            heights[z, x] = Mathf.Max(0f, targetH);
            shoreCount++;
        }
    }
}
```

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] No cliff at water boundary — terrain meets water level smoothly
- [ ] Ramp is gradual over the full ShoreRadius width (~3m at 2049 res)
- [ ] Water surface still flat (no seesaw)
- [ ] Full water mesh still visible (no re-regression to hidden half)

Regression check:

- [ ] `Import Hole 01 Geo` (no water) — no errors
- [ ] `Import Hole 12 Geo` (multi-body) — each body's surrounding ramp
      targets THAT body's surface Y, not some average

---

### Do NOT change

- Water mesh construction (from the first port)
- Water floor depression (from the second port)
- Fairway/tee/cart path behavior
- Shore constants (ShoreRadius=10, ShoreDepthMeters=0.4)
- Shore ramp skip conditions (water/depress/cartDepress)

**Note:** With this fix, `ShoreDepthMeters` becomes less directly meaningful
for the ramp (the ramp now targets water surface absolutely, not a fixed
drop). Keep the constant for now — it still controls water floor depth
via the previous task's floor logic.

---

## Previous Task — Flatten Terrain Under Water (Hole 7 Geo Follow-up)

Water rework applied successfully — seesaw gone, shape clean. But on sloped
contours (Hole 7), half the water mesh ends up below the terrain.

**Root cause:** Water Y is `min(shoreTerrain) − 0.05m`. On a slope, the
highest shore may be 1–2m above the lowest. The current depression drops
terrain by a FIXED 0.4m off its original height — not enough to get the
upslope side below the water plane.

**Fix:** Flatten terrain under water to an ABSOLUTE normalized height
(`waterY − underwaterDepth` in world units), not a relative drop off
original height. This guarantees a flat bed below water regardless of
slope.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No pipeline changes.**

---

### Change: Separate water depression from fairway/tee

The previous water port added water contours to the shared `depress` bool
array and let the standard flat-drop loop handle them. Replace that with
a dedicated absolute-height pass.

**Step 1: Undo water's entry in the shared `depress` mask.**

In `DepressTerrainUnderOverlays`, find the water-contour block that was
added between the tee section and the cart path block:

```csharp
// Water contours — use same flat depression as fairway/tee
// but with shore slope ramp applied afterward.
string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        foreach (var w in waterData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, depress,
                    hRes, terrainPos, terrainSize, 0f);
        }
    }
}
```

**Delete this entire block.** Water needs its own mask + pass, not the
shared one.

**Step 2: Add a dedicated water mask, parallel to the others.**

Just after the `cartDepress` block (and before the depression apply loops),
build a water mask AND compute water Y per body:

```csharp
// Water cells — tracked separately because they get an ABSOLUTE height
// floor (not a relative drop). Necessary for sloped contours where a
// fixed drop off original height leaves the upslope bed above waterY.
bool[,] waterMask = new bool[hRes, hRes];
// Per-cell water Y in normalized heightmap units (height = [0..1])
float[,] waterFloorY = new float[hRes, hRes];
bool hasWater = false;

string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        // We need terrainBaseY for SampleHeight conversion.
        Terrain terrainComp = terrainGO.GetComponent<Terrain>();
        float terrainBaseY = terrainGO.transform.position.y;

        // Underwater floor: 0.3m below water surface.
        // Water surface = terrainBaseY + minShoreTerrainH - 0.05m,
        // so floor = terrainBaseY + minShoreTerrainH - 0.35m.
        const float UnderwaterDepthMeters = 0.3f;

        foreach (var w in waterData.water)
        {
            if (w.contour == null || w.contour.Length < 3) continue;

            // Recompute minTerrainH across contour — same as CreateWaterMeshes.
            // (We can't share it easily because water was built earlier,
            // but this is one float per body, cheap.)
            float minTerrainH = float.MaxValue;
            for (int i = 0; i < w.contour.Length; i++)
            {
                float wx = w.contour[i].x;
                float wz = w.contour[i].z;
                float th = terrainComp.SampleHeight(new Vector3(wx, 0, wz));
                if (th < minTerrainH) minTerrainH = th;
            }
            // Floor Y in world units, then normalized to [0..1] against elevRange.
            float floorWorldY = minTerrainH - 0.05f - UnderwaterDepthMeters;
            // Clamp to ≥ 0 in case terrain Y offset eats the range
            float floorNorm = Mathf.Clamp01(floorWorldY / elevRange);

            // Mark cells inside this water contour with this body's floor Y.
            // Build a local mask for THIS body, then write the Y value to
            // waterFloorY for each cell in the mask.
            bool[,] bodyMask = new bool[hRes, hRes];
            MarkContourCells(w.contour, bodyMask,
                hRes, terrainPos, terrainSize, 0f);

            for (int z = 0; z < hRes; z++)
                for (int x = 0; x < hRes; x++)
                    if (bodyMask[z, x])
                    {
                        waterMask[z, x] = true;
                        waterFloorY[z, x] = floorNorm;
                    }

            hasWater = true;
        }
    }
}
```

**Step 3: Apply the water floor BEFORE the fairway/tee/cart apply loops.**

Immediately after the water mask-building block above (still before the
existing apply loops), add:

```csharp
// Apply water: flatten terrain to an absolute floor (not a relative drop).
// Must run BEFORE fairway/tee/cart apply loops because any fairway that
// overlaps water should keep the fairway drop, not the water floor.
// We'll mask out water cells in the fairway/tee loops below.
int waterFloorCount = 0;
if (hasWater)
{
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            if (waterMask[z, x])
            {
                // Set to absolute floor, not subtract
                heights[z, x] = waterFloorY[z, x];
                waterFloorCount++;
            }
}
```

**Step 4: Skip water cells in the fairway/tee apply loop.**

Find the fairway/tee apply loop:

```csharp
int depressedCount = 0;
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (depress[hz, hx])
        {
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - dropNormalized);
            depressedCount++;
        }
```

Change the condition to skip water cells:

```csharp
int depressedCount = 0;
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (depress[hz, hx] && !waterMask[hz, hx])
        {
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - dropNormalized);
            depressedCount++;
        }
```

Same for the cart path apply loop:

```csharp
int cartDepressedCount = 0;
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (cartDepress[hz, hx] && !waterMask[hz, hx])
        {
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - dropNormalized);
            cartDepressedCount++;
        }
```

**Step 5: Shore slope pass — use existing water mask.**

The shore slope pass already exists from the previous port. It currently
re-reads water.json and builds its own `waterMask`. Simplify: reuse the
mask built in Step 2.

Find the shore slope section (begins with
`string waterShorePath = Path.Combine(exportPath, "water.json");`).

Replace the entire shore slope block (from `string waterShorePath = ...`
through the closing brace of the outer `if (File.Exists(waterShorePath) ...)`)
with:

```csharp
// ─── Shore slope pass: gradual ramp OUTSIDE water contours ─────────
// Uses waterMask built above. Smooth ramp from shoreline
// (full ShoreDepthMeters drop) to surrounding terrain (no drop)
// over ShoreRadius cells.
int shoreCount = 0;
if (hasWater && ShoreRadius > 0 && ShoreDepthMeters > 0f)
{
    // Chamfer distance transform from water boundary.
    float[,] distToWater = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;

    // Forward pass
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
        {
            if (x > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x - 1] + 1f);
            if (z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x] + 1f);
            if (x > 0 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x - 1] + 1.414f);
            if (x < hRes - 1 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x + 1] + 1.414f);
        }
    // Backward pass
    for (int z = hRes - 1; z >= 0; z--)
        for (int x = hRes - 1; x >= 0; x--)
        {
            if (x < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x + 1] + 1f);
            if (z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x] + 1f);
            if (x < hRes - 1 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x + 1] + 1.414f);
            if (x > 0 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x - 1] + 1.414f);
        }

    float shoreDropNorm = ShoreDepthMeters / elevRange;
    int shoreRadiusCells = ShoreRadius;

    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (waterMask[z, x]) continue;
            if (depress[z, x]) continue;
            if (cartDepress[z, x]) continue;

            float dist = distToWater[z, x];
            if (dist <= 0f || dist > shoreRadiusCells) continue;

            float t = 1f - (dist / shoreRadiusCells);
            t = t * t * (3f - 2f * t);
            float drop = shoreDropNorm * t;

            heights[z, x] = Mathf.Max(0f, heights[z, x] - drop);
            shoreCount++;
        }
    }
}
```

**Step 6: Update final Debug.Log.**

Replace the current final log (the one updated in the previous port) with:

```csharp
Debug.Log($"[HoleGeoImporter] Terrain depression: {depressedCount}" +
          $" cells lowered by {OverlayDepressionMeters:F2}m" +
          $" (cart path: {cartDepressedCount} cells," +
          $" water floor: {waterFloorCount} cells flattened," +
          $" water shore ramp: {shoreCount} cells)");
```

---

### Execution order

1. Step 1 (remove old water-in-depress block)
2. Step 2 (build waterMask + waterFloorY)
3. Step 3 (apply water floor)
4. Step 4 (skip water in fairway/tee/cart loops)
5. Step 5 (replace shore slope block)
6. Step 6 (log update)

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] Entire water mesh visible (no hidden-under-terrain half)
- [ ] Water surface still flat (no seesaw regression)
- [ ] Shore ramp smooth (no cliff)
- [ ] No Z-fighting between water mesh edge and terrain

Regression check:

- [ ] `Import Hole 01 Geo` — no water, no errors, no regression
- [ ] `Import Hole 12 Geo` — multiple water bodies, each gets its own floor

---

### Why this approach

- The previous "drop 0.4m off original" works for flat-land water but not
  sloped water bodies. Absolute floor is slope-independent.
- Per-body floorY handles holes with multiple water levels (e.g., a pond
  and a stream at different elevations) — each body gets its own floor
  from its own min shore height.
- Shore ramp stays on original terrain heights (not the floor) because
  ramp cells are OUTSIDE the water mask.

---

### Do NOT change

- `CreateWaterMeshes` (from previous port — water mesh Y is still
  `terrainBaseY + minTerrainH - 0.05f`, unchanged)
- `CreateWaterMaterial` depth settings
- Shore constants (ShoreRadius=10, ShoreDepthMeters=0.4)
- Fairway/tee/green/bunker/cart path logic

---

## Previous Task — Port Water Rework to HoleGeoImporter

Hole 7 Geo shows a seesaw waterline and water edges that don't match the
terrain edges. Cause: HoleGeoImporter still has the OLD per-vertex
terrain-following water code. The 2026-04-14 water rework was applied to
HoleLiteImporter.cs only; HoleGeoImporter.cs never got the port.

This task ports the rework to HoleGeoImporter.cs. The Lite version is the
working reference — match its behavior.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**Reference file:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`
(only for cross-checking — do not edit)
**No pipeline changes.** `water.json` is fine.

---

### Part 1 — Shore constants at top of class

Current (HoleGeoImporter.cs, around lines 18–21):

```csharp
public static int ShoreRadius = 2;
public static float ShoreDepthMeters = 0.1f;
```

Change to:

```csharp
public static int ShoreRadius = 10;
public static float ShoreDepthMeters = 0.4f;

// ─── Terrain Y offset — headroom below flat terrain for water bed.
// Must be ≥ ShoreDepthMeters + water surface depth (0.05m) + underwater margin (0.3m)
// so heightmap can represent the full water bed without clamping.
private static float TerrainYOffset => ShoreDepthMeters;
```

---

### Part 2 — Use `TerrainYOffset` for terrain placement

In `ImportHoleInternal`, the terrain object is positioned using
`ShoreDepthMeters`. Change it to use `TerrainYOffset`.

Find:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -ShoreDepthMeters, -terrainZ / 2f);
```

Replace with:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -TerrainYOffset, -terrainZ / 2f);
```

Do not touch `CreateTerrain`'s use of `ShoreDepthMeters` in
`elevRange`/`normalizedFlat` — those compute headroom, which is correct.

---

### Part 3 — Rewrite `CreateWaterMeshes`

Find the method `CreateWaterMeshes` (signature:
`private static void CreateWaterMeshes(TerrainData terrainData, GameObject terrainGO, Transform parentRoot, string exportPath, string dataDir, string projectRoot, bool[,] holes)`).

The method has TWO sections:
1. The per-water-body mesh-building `foreach (var water in waterFile.water)` loop
2. A trailing "Shore slope pass" section that builds `isWater` mask and depresses terrain

**Delete section 2 entirely.** That work moves to `DepressTerrainUnderOverlays`
(Part 4). Keep only the final `File.Copy` of water.json to Assets and the final
`Debug.Log`.

**Rewrite section 1 (the per-water-body loop):**

Currently each iteration samples terrain height per vertex and sets
`wy = terrainBaseY + terrainH - 0.1f` — this creates uneven water surface that
seesaws along sloped shores.

Replace the loop body with flat-CDT construction. Here is the complete
replacement for the entire `foreach` body:

```csharp
foreach (var water in waterFile.water)
{
    if (water.contour == null || water.contour.Length < 3) continue;

    int n = water.contour.Length;

    // 3A. Flat water Y = min terrain height across contour − 0.05m
    float minTerrainH = float.MaxValue;
    for (int i = 0; i < n; i++)
    {
        float wx = water.contour[i].x;  // Geo: no rotation
        float wz = water.contour[i].z;
        float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
        if (th < minTerrainH) minTerrainH = th;
    }
    float waterY = terrainBaseY + minTerrainH - 0.05f;

    // 3B. CDT triangulation — same pattern as fairway/tee.
    // Water doesn't need fine terrain conformance (flat surface), but CDT
    // needs interior Steiner points for clean triangulation of large
    // concave shapes. 2.0m grid spacing is plenty.
    float tileSize = 10f; // world-UV tiling for URPWater shader
    System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
        new Vector2(wx / tileSize, wz / tileSize);

    var (rawVerts, uvs, tris) = CDTTriangulate(
        water.contour, terrain, terrainBaseY, 0f, 2.0f, uvFunc);

    if (rawVerts == null || tris == null || tris.Length < 3)
    {
        Debug.LogWarning($"[HoleGeoImporter] Water {water.id}: CDT failed, skipping");
        continue;
    }

    // 3C. Flatten all vertex Y to waterY (CDT sampled terrain heights;
    // overwrite them so the surface is perfectly flat).
    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i].y = waterY;

    // 3D. Center mesh at centroid (Y=0 origin pattern, same as fairway).
    float cx = 0f, cz = 0f;
    for (int i = 0; i < rawVerts.Length; i++)
    { cx += rawVerts[i].x; cz += rawVerts[i].z; }
    cx /= rawVerts.Length; cz /= rawVerts.Length;
    Vector3 centroid = new Vector3(cx, 0f, cz);

    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i] -= centroid;

    // 3E. Winding check — ensure top faces up.
    if (tris.Length >= 3)
    {
        Vector3 a = rawVerts[tris[0]];
        Vector3 b = rawVerts[tris[1]];
        Vector3 c = rawVerts[tris[2]];
        float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        if (cross > 0)
        {
            for (int t = 0; t < tris.Length; t += 3)
            { int tmp = tris[t]; tris[t] = tris[t + 2]; tris[t + 2] = tmp; }
        }
    }

    var mesh = new Mesh();
    mesh.name = $"Water_{water.id}";
    mesh.vertices = rawVerts;
    mesh.uv = uvs;
    mesh.triangles = tris;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"Water_{water.id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = waterMat;

    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = Golfin.Course.SurfaceType.Water;

    go.transform.SetParent(waterRoot.transform);

    Debug.Log($"[HoleGeoImporter] Water {water.id}: {n} contour verts, " +
              $"{rawVerts.Length} CDT verts, {tris.Length / 3} tris, " +
              $"waterY={waterY:F2}");
}
```

**Notes:**
- Keep the two existing `Debug.Log` lines at method top
  (`terrainBaseY={...}` / `ShoreDepthMeters={...}`). They're useful.
- Keep the `waterRoot`/`terrain`/`terrainBaseY`/`waterMat` setup at top
  of the method — unchanged.
- The old section 2 (shore slope, `isWater` mask, chamfer distance,
  `underwaterDrop`, `terrainData.SetHeights`) is DELETED.

---

### Part 4 — Add water handling to `DepressTerrainUnderOverlays`

`DepressTerrainUnderOverlays` currently handles fairway + tee + cart path.
Add water.

**4A. Add water contours to the `depress` bool[,] array.**

In `DepressTerrainUnderOverlays`, find the tee contour section
(immediately after fairway, uses `zone-contours.json` and the
`data.zones.tee` loop with `MarkContourCells(region.contour, depress, ...)`).

Immediately AFTER the closing brace of that tee block, BEFORE the cart path
section (the `cartDepress` block), insert:

```csharp
// Water contours — use same flat depression as fairway/tee
// but with shore slope ramp applied afterward.
string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        foreach (var w in waterData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, depress,
                    hRes, terrainPos, terrainSize, 0f);
                    // inset=0 — depress right up to the contour edge
        }
    }
}
```

This makes water cells receive the standard `OverlayDepressionMeters` (0.40m)
flat drop in the existing apply loop. No separate water-depression needed.

**4B. Add shore slope pass after the existing apply loop.**

The existing apply loop ends with `depressedCount += cartDepressedCount;` and
then `terrainData.SetHeights(0, 0, heights);` followed by a Debug.Log.

**BEFORE** `terrainData.SetHeights(0, 0, heights);`, insert the shore slope
pass:

```csharp
// ─── Shore slope pass: gradual ramp outside water contours ─────────
// Creates a smooth transition from shoreline (full ShoreDepthMeters drop)
// to surrounding terrain (no drop) over ShoreRadius cells.
// Without this, water edges would cliff against un-depressed terrain.
string waterShorePath = Path.Combine(exportPath, "water.json");
int shoreCount = 0;
if (File.Exists(waterShorePath) && ShoreRadius > 0 && ShoreDepthMeters > 0f)
{
    // 4B-1. Build water-only mask from water contours.
    bool[,] waterMask = new bool[hRes, hRes];
    var waterShoreData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterShorePath));
    if (waterShoreData.water != null)
    {
        foreach (var w in waterShoreData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, waterMask,
                    hRes, terrainPos, terrainSize, 0f);
        }
    }

    // 4B-2. Chamfer distance transform from water boundary (cells not in water).
    float[,] distToWater = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;

    // Forward pass
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
        {
            if (x > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x - 1] + 1f);
            if (z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x] + 1f);
            if (x > 0 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x - 1] + 1.414f);
            if (x < hRes - 1 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x + 1] + 1.414f);
        }
    // Backward pass
    for (int z = hRes - 1; z >= 0; z--)
        for (int x = hRes - 1; x >= 0; x--)
        {
            if (x < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x + 1] + 1f);
            if (z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x] + 1f);
            if (x < hRes - 1 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x + 1] + 1.414f);
            if (x > 0 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x - 1] + 1.414f);
        }

    // 4B-3. Apply ramp OUTSIDE water (full drop at boundary,
    //       zero drop at ShoreRadius). Skip water cells (already
    //       depressed in step 4A) and fairway/tee/cart cells
    //       (already fully depressed — another drop would stack).
    float shoreDropNorm = ShoreDepthMeters / elevRange;
    int shoreRadiusCells = ShoreRadius;

    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (waterMask[z, x]) continue;           // water cell: skip
            if (depress[z, x]) continue;             // fairway/tee/water: skip
            if (cartDepress[z, x]) continue;         // cart path: skip

            float dist = distToWater[z, x];
            if (dist <= 0f || dist > shoreRadiusCells) continue;

            // smoothstep: 1 at boundary, 0 at shoreRadius
            float t = 1f - (dist / shoreRadiusCells);
            t = t * t * (3f - 2f * t);
            float drop = shoreDropNorm * t;

            heights[z, x] = Mathf.Max(0f, heights[z, x] - drop);
            shoreCount++;
        }
    }
}
```

Then update the final Debug.Log to include shore:

```csharp
Debug.Log($"[HoleGeoImporter] Terrain depression: {depressedCount}" +
          $" cells lowered by {OverlayDepressionMeters:F2}m" +
          $" (cart path: {cartDepressedCount} cells," +
          $" water shore ramp: {shoreCount} cells)");
```

**Important:** The variable `cartDepress` is defined inside
`DepressTerrainUnderOverlays` and is in scope at the insertion point — it's
created above the fairway/tee sections. Verify the variable is accessible
where you insert the shore pass. If for any reason the cart path mask is
scoped differently in Geo, drop the `if (cartDepress[z, x]) continue;` line
(worst case: cart-path grass gets an extra shore drop near water, visually
harmless).

---

### Part 5 — Update water material depth settings

In `CreateWaterMaterial`, find:

```csharp
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.3f);
```

Change to:

```csharp
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.8f);
```

This gives the depth-based color gradient room to work with the new 0.4m
shore depression.

---

### Execution order

1. Part 1 (constants)
2. Part 2 (terrain position)
3. Part 5 (material — trivial, do it while you're near the constants area)
4. Part 3 (CreateWaterMeshes rewrite)
5. Part 4 (DepressTerrainUnderOverlays — 4A then 4B)

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] Water surface is perfectly flat (single Y per body, no seesaw)
- [ ] Water edges line up with terrain edges (no dark cliff strip)
- [ ] Shore slopes gradually into water
- [ ] Depth-based color: shallower teal near edges, darker blue toward center
- [ ] No z-fighting between water mesh and terrain
- [ ] Fairways, tees, bunkers, greens, cart paths unaffected

Then regression check with a hole without water:

- [ ] `Import Hole 01 Geo` completes without errors (Hole 1 has no water —
      make sure the water file handling degrades cleanly)

And a hole with multiple water bodies:

- [ ] `Import Hole 12 Geo` — waterways + pond, check both look flat

---

### Do NOT change

- `CreateWaterMaterial` shader selection (URPWater/Standard)
- Any other CreateWaterMeshes setup code (waterRoot, terrain, terrainBaseY,
  waterMat vars, File.Copy at end)
- Fairway/tee/green/bunker/cart path logic
- UHoleGeo export pipeline — `water.json` is fine as-is
- The disabled `if (false && loadedRaw)` boundary propagation block

---

## Completed Tasks

✅ DONE: 2026-04-17 — Absolute-target shore ramp: waterSurfaceY per body, joint chamfer propagates nearestSurfaceY, lerp replaces fixed-drop
✅ DONE: 2026-04-17 — Absolute water floor for sloped contours: per-body floorNorm, waterMask separate from depress, fairway/cart loops skip water cells, shore reuses waterMask
✅ DONE: 2026-04-17 — Water rework ported to HoleGeoImporter: flat CDT, TerrainYOffset, water depression in DepressTerrainUnderOverlays, shore slope ramp, _DepthEnd 0.8
✅ DONE: 2026-04-16 — Flat inside + 8-cell outward smoothstep ramp implemented
✅ DONE: 2026-04-16 — Green collar CDT complete
✅ DONE: 2026-04-16 — Bunker lip submesh complete
✅ 2026-04-16 — Bunker lip baked as submesh 1
✅ 2026-04-16 — Cart path outward smoothstep ramp (8 cells)
✅ 2026-04-16 — Cart path flat depression
✅ 2026-04-16 — Spline cart path depression footprint
✅ 2026-04-16 — Spline cart path meshes
✅ 2026-04-16 — Fringe/border baked into parent CDT mesh as submesh
✅ 2026-04-14 — Water rework complete (HoleLiteImporter only — Geo ported 2026-04-17)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ All earlier tasks
