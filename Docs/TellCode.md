# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Cart Path Depression Improvements (3 strategies)

**Problem:** The terrain depression under cart paths creates a visible
cliff/shadow at the road edges (see screenshot). The current system
drops ALL cells inside a spine polygon by a flat 0.40m — this creates
a hard shelf where the road mesh meets undepressed terrain.

**Goal:** Make the cart path edges blend seamlessly with surrounding
grass using three combined strategies:
1. Inward-offset the depression so the drop starts well inside the road
2. Gradual slope from edge to center instead of a flat cliff
3. Paint cart path texture on terrain at road edges

All three changes work together. Changes are **cart-path-only** — do
NOT touch fairway/tee/green/water depression behavior.

---

### Strategy 1 — Deeper inward offset for cart path depression

**File:** `HoleLiteImporter.cs`, method `DepressTerrainUnderOverlays`

In the cart path section (~line 2640), the `BuildSpinePolygon` call
currently uses `halfWidth = (width / 2) - 0.10f`.

Change this so the depression polygon is inset further:

```csharp
// BEFORE:
float halfWidth = (cp.width_m > 0
    ? cp.width_m : 2.5f) / 2f - 0.10f;

// AFTER:
float halfWidth = (cp.width_m > 0
    ? cp.width_m : 2.5f) / 2f - 0.50f;
```

This means the depression starts 0.50m inside the road edge instead
of 0.10m. The road mesh (which uses full half-width) covers this
easily, and the terrain stays undisturbed at the visible road boundary.

Also add a safety clamp — if the road is very narrow, don't go
negative:
```csharp
if (halfWidth < 0.3f) halfWidth = 0.3f;
```

---

### Strategy 2 — Gradual slope instead of flat drop

**File:** `HoleLiteImporter.cs`, method `DepressTerrainUnderOverlays`

Currently, after all contour cells are marked in the `depress` bool
array, a single flat drop is applied:

```csharp
// CURRENT (~line 2670):
if (depress[hz, hx])
{
    heights[hz, hx] = Mathf.Max(0f,
        heights[hz, hx] - dropNormalized);
    depressedCount++;
}
```

Replace this with a **distance-based gradual slope for cart paths**.

The approach: build a **separate** distance field for cart path cells
only, then apply a ramp instead of a flat drop.

Add a new `bool[,] cartDepress` array alongside the existing `depress`
array. Mark cart path cells in `cartDepress` instead of `depress`.

Then, after the main depression loop, add a cart-path-specific
gradient pass:

```csharp
// --- Cart path cells: separate from main depress array ---
bool[,] cartDepress = new bool[hRes, hRes];

// (In the cart path section, mark cartDepress instead of depress)
// ... MarkWorldContourCells(spinePoly, cartDepress, ...)
//     instead of
//     MarkWorldContourCells(spinePoly, depress, ...)

// --- After main depress loop, apply cart path gradient ---
// Step 1: Distance transform on cartDepress (chamfer)
float[,] cartDist = new float[hRes, hRes];
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        cartDist[hz, hx] = cartDepress[hz, hx] ? 0f : 99999f;

// Forward pass
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
    {
        if (hx > 0) cartDist[hz, hx] = Mathf.Min(
            cartDist[hz, hx], cartDist[hz, hx - 1] + 1f);
        if (hz > 0) cartDist[hz, hx] = Mathf.Min(
            cartDist[hz, hx], cartDist[hz - 1, hx] + 1f);
    }
// Backward pass
for (int hz = hRes - 1; hz >= 0; hz--)
    for (int hx = hRes - 1; hx >= 0; hx--)
    {
        if (hx < hRes - 1) cartDist[hz, hx] = Mathf.Min(
            cartDist[hz, hx], cartDist[hz, hx + 1] + 1f);
        if (hz < hRes - 1) cartDist[hz, hx] = Mathf.Min(
            cartDist[hz, hx], cartDist[hz + 1, hx] + 1f);
    }

// Step 2: Find max distance (= center of widest part)
float maxCartDist = 0f;
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (cartDepress[hz, hx] && cartDist[hz, hx] > maxCartDist)
            maxCartDist = cartDist[hz, hx];

if (maxCartDist < 1f) maxCartDist = 1f; // safety

// Step 3: Apply smoothstep ramp — edge gets 0% drop, center gets 100%
for (int hz = 0; hz < hRes; hz++)
{
    for (int hx = 0; hx < hRes; hx++)
    {
        if (!cartDepress[hz, hx]) continue;

        float t = cartDist[hz, hx] / maxCartDist; // 0 at edge → 1 at center
        t = Mathf.Clamp01(t);
        t = t * t * (3f - 2f * t); // smoothstep

        float cellDrop = dropNormalized * t;
        heights[hz, hx] = Mathf.Max(0f,
            heights[hz, hx] - cellDrop);
        depressedCount++;
    }
}
```

**Key behavior:** At the outer edge of the (already inset) depression
polygon, drop = 0. At the center of the road, drop = full 0.40m.
Smooth ramp in between. Combined with Strategy 1 (0.50m inset), the
visible road edge has zero terrain disturbance.

---

### Strategy 3 — Paint cart path texture on terrain at road edges

**File:** `HoleLiteImporter.cs`, method `ApplySplatmap`

After the main alphamap loop (which sets each cell to a single layer),
and after the OB blend pass, add a cart-path-edge painting pass.

This paints splatmap layer 6 (`T_RoadAsphalt_Albedo`) on the terrain
for a 2-pixel strip along the outer edge of the cart path zone.

```csharp
// --- Paint cart path texture on terrain at road edges ---
// Load cart path spines to know where roads are
string cpEdgePath = Path.Combine(exportPath, "cart-paths.json");
if (File.Exists(cpEdgePath))
{
    var cpData = JsonUtility.FromJson<CartPathsFile>(
        File.ReadAllText(cpEdgePath));
    if (cpData.cart_paths != null)
    {
        // Build a mask of cart path cells at alphamap resolution
        bool[,] cpMask = new bool[alphaRes, alphaRes];
        Vector3 terrainPos2 = terrainGO.transform.position;
        Vector3 terrainSize2 = terrainData.size;

        foreach (var cp in cpData.cart_paths)
        {
            if (cp.spine != null && cp.spine.Length >= 2)
            {
                float hw = (cp.width_m > 0 ? cp.width_m : 2.5f) / 2f;
                var poly = BuildSpinePolygon(cp.spine, hw);
                if (poly != null)
                {
                    // Mark cells inside the full-width polygon
                    float minX2 = float.MaxValue, maxX2 = float.MinValue;
                    float minZ2 = float.MaxValue, maxZ2 = float.MinValue;
                    foreach (var v in poly)
                    {
                        if (v.x < minX2) minX2 = v.x;
                        if (v.x > maxX2) maxX2 = v.x;
                        if (v.y < minZ2) minZ2 = v.y;
                        if (v.y > maxZ2) maxZ2 = v.y;
                    }

                    int aMinX = Mathf.Clamp(Mathf.FloorToInt(
                        (minX2 - terrainPos2.x) / terrainSize2.x
                        * (alphaRes - 1)), 0, alphaRes - 1);
                    int aMaxX = Mathf.Clamp(Mathf.CeilToInt(
                        (maxX2 - terrainPos2.x) / terrainSize2.x
                        * (alphaRes - 1)), 0, alphaRes - 1);
                    int aMinZ = Mathf.Clamp(Mathf.FloorToInt(
                        (minZ2 - terrainPos2.z) / terrainSize2.z
                        * (alphaRes - 1)), 0, alphaRes - 1);
                    int aMaxZ = Mathf.Clamp(Mathf.CeilToInt(
                        (maxZ2 - terrainPos2.z) / terrainSize2.z
                        * (alphaRes - 1)), 0, alphaRes - 1);

                    for (int ay = aMinZ; ay <= aMaxZ; ay++)
                    {
                        for (int ax = aMinX; ax <= aMaxX; ax++)
                        {
                            float cwx = (float)ax / (alphaRes - 1)
                                * terrainSize2.x + terrainPos2.x;
                            float cwz = (float)ay / (alphaRes - 1)
                                * terrainSize2.z + terrainPos2.z;
                            if (IsInsideContour(cwx, cwz, poly))
                                cpMask[ay, ax] = true;
                        }
                    }
                }
            }
        }

        // Find edge pixels: cpMask=true but has a neighbor that is false
        // Paint those + 1px inward with cart path texture (layer 6)
        const int edgeWidth = 2; // pixels

        // Distance from edge (inside the mask)
        float[,] cpEdgeDist = new float[alphaRes, alphaRes];
        for (int ay = 0; ay < alphaRes; ay++)
            for (int ax = 0; ax < alphaRes; ax++)
                cpEdgeDist[ay, ax] = cpMask[ay, ax] ? 99999f : 0f;

        // Chamfer forward
        for (int ay = 0; ay < alphaRes; ay++)
            for (int ax = 0; ax < alphaRes; ax++)
            {
                if (ax > 0) cpEdgeDist[ay, ax] = Mathf.Min(
                    cpEdgeDist[ay, ax], cpEdgeDist[ay, ax - 1] + 1f);
                if (ay > 0) cpEdgeDist[ay, ax] = Mathf.Min(
                    cpEdgeDist[ay, ax], cpEdgeDist[ay - 1, ax] + 1f);
            }
        // Chamfer backward
        for (int ay = alphaRes - 1; ay >= 0; ay--)
            for (int ax = alphaRes - 1; ax >= 0; ax--)
            {
                if (ax < alphaRes - 1) cpEdgeDist[ay, ax] = Mathf.Min(
                    cpEdgeDist[ay, ax], cpEdgeDist[ay, ax + 1] + 1f);
                if (ay < alphaRes - 1) cpEdgeDist[ay, ax] = Mathf.Min(
                    cpEdgeDist[ay, ax], cpEdgeDist[ay + 1, ax] + 1f);
            }

        // Paint edge strip: full cart path texture at edge, blending inward
        int cpEdgePainted = 0;
        for (int ay = 0; ay < alphaRes; ay++)
        {
            for (int ax = 0; ax < alphaRes; ax++)
            {
                if (!cpMask[ay, ax]) continue;
                float dist = cpEdgeDist[ay, ax];
                if (dist > edgeWidth) continue; // too far inside

                // Blend: 100% cart path at dist=0 (edge), 50% at dist=edgeWidth
                float blend = 1f - (dist / edgeWidth) * 0.5f;

                // Find which layer currently has weight here
                int currentLayer = -1;
                for (int l = 0; l < layerCount; l++)
                {
                    if (alphamap[ay, ax, l] > 0.5f)
                    { currentLayer = l; break; }
                }
                if (currentLayer < 0) currentLayer = 3; // rough fallback

                // Set blend
                for (int l = 0; l < layerCount; l++)
                    alphamap[ay, ax, l] = 0f;
                alphamap[ay, ax, 6] = blend;         // cart path texture
                alphamap[ay, ax, currentLayer] = 1f - blend; // existing texture
                cpEdgePainted++;
            }
        }
        Debug.Log($"[HoleLiteImporter] Cart path edge: painted {cpEdgePainted} splatmap cells");
    }
}
```

**Placement:** This block goes AFTER the OB rough↔OB blend pass and
BEFORE the `terrainData.terrainLayers = layers` / `SetAlphamaps` lines.

**NOTE:** `BuildSpinePolygon` is a private static method already in
the file (used by `DepressTerrainUnderOverlays`). It's being reused
here at splatmap resolution. `IsInsideContour` is also already
available.

**NOTE:** The `terrainGO` reference is not directly available in
`ApplySplatmap`. You'll need to pass the terrainGO (or terrainPos +
terrainSize) into ApplySplatmap, OR compute terrainPos/terrainSize
from terrainData directly:
```csharp
// terrainPos: terrain is centered, so:
// In ImportLiteHole, terrainGO.position = (-terrainX/2, -ShoreDepthMeters, -terrainZ/2)
// But in ApplySplatmap we don't have terrainGO. We can compute:
Vector3 terrainSize2 = terrainData.size;
// terrainPos needs to match what ImportLiteHole sets. For now, add
// terrainGO as a parameter to ApplySplatmap, or pass exportPath
// and read cart-paths.json. The simplest fix: add `GameObject terrainGO`
// as a parameter to ApplySplatmap and pass it from ImportLiteHole.
```

Choose whichever approach is cleanest — either add `terrainGO` param
to `ApplySplatmap`, or compute the position from the manifest data.

---

### Verification

1. Import Hole 01 (or any hole with cart paths)
2. **At road edges:** Grass meets road flush — no visible cliff or
   shadow line. Terrain color matches road at the boundary thanks
   to the painted asphalt strip.
3. **Under the road:** Terrain gradually slopes down toward center,
   hidden entirely by the mesh overlay.
4. **Fairway/tee edges:** Unchanged — still use the original
   `DepressionInsetMeters (0.20m)` and flat drop.
5. **Splatmap:** 2px strip of cart path texture visible at road edges
   where the mesh doesn't quite cover.

### Do NOT
- Change depression behavior for fairways, tees, greens, or water
- Change the cart path mesh geometry (CreateSpineStripMesh)
- Change cart path width or spine data
- Change any texture files or materials

---

## Completed Tasks
✅ 2026-04-13 — Cart path depression: 3-strategy fix (0.50m inset + smoothstep ramp + 2px splatmap edge paint)
✅ 2026-04-13 — Natural OB↔Rough transition: reuse rough texture with tint + 4px boundary blend
✅ 2026-04-13 — "Smooth OB" button in UHole Lite (vectorize → RDP → Chaikin → rasterize)
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix (OffsetContourOutward + spine polygon)
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-11 — Grid draping iterations, Y=0 origin pattern
✅ 2026-04-10 — Tree placement (mixed mode: terrain + standalone)
✅ 2026-04-10 — Bunker v1-v5 iterations
✅ All earlier tasks (water, bunkers, greens, textures, cart paths, etc.)
