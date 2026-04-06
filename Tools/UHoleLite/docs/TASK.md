# TASK.md — UHole Lite: Fix Orientation (Take 3)

> Claude Code: Read this file carefully. Previous fixes did not work.
> The terrain is STILL rendering horizontally (landscape, rotated 90°).

---

## Current Task — Fix Terrain 90° Rotation (Definitive)

**Problem:** The terrain renders with the golf hole horizontal instead of vertical.
Two attempts at changing the heightmap indexing did not fix it.

**New approach:** Instead of guessing at heightmap indexing, swap the terrain
width/length AND the tileSize so the long dimension (illustration height) maps
to the correct Unity axis.

### The Key Insight

The illustration is portrait: 530px wide × 637px tall.
The terrain manifest says: `terrain_width_m=523.4` (short), `terrain_length_m=631.2` (long).

Currently:
```csharp
terrainData.size = new Vector3(
    manifest.terrain.terrain_width_m,   // X = 523 (short axis)
    elevRange,
    manifest.terrain.terrain_length_m   // Z = 631 (long axis)
);
```

And the terrain is placed at:
```csharp
position = (-width/2, 0, -length/2) = (-261.7, 0, -315.6)
```

The texture tileSize is:
```csharp
layer.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
// = (523, 631)
```

TerrainLayer.tileSize: `x` = repeat distance along terrain X, `y` = repeat
distance along terrain Z.

If the terrain size is (523, elev, 631), then:
- Terrain X spans 523m (the short axis)
- Terrain Z spans 631m (the long axis)
- Texture U tiles every 523m along X
- Texture V tiles every 631m along Z

The texture is 1024×1235 pixels. Unity maps:
- Texture width (1024px) → U axis → terrain X (523m)
- Texture height (1235px) → V axis → terrain Z (631m)

The illustration width (short) maps to X (short) ✓
The illustration height (long) maps to Z (long) ✓

This SHOULD give a portrait terrain... but it doesn't. The terrain appears
landscape, meaning the content is rendered with X and Z swapped.

### What's Actually Happening

I believe the issue is with how `SetHeights` interprets the array dimensions
relative to `terrainData.size`.

Unity terrain is always SQUARE in heightmap resolution (129×129). The non-square
world dimensions come from `terrainData.size`. But `SetHeights` maps:
- array[0, *] = one edge of terrain
- array[*, 0] = another edge

If the first array dimension maps to the Z axis and the second to X (as Unity
docs suggest), then the heightmap content should be:
- Row 0 (first dimension = 0) corresponds to terrain max Z
- Col 0 (second dimension = 0) corresponds to terrain min X

With `heights[x, res-1-y]` (current code):
- First dim varies with image X (0→128 = left→right of illustration)
- Second dim varies with image Y (flipped, 0=bottom, 128=top)

If first dim = Z axis: illustration left/right maps to Z. That would mean
the illustration's width (short axis) maps to Z (long axis) → landscape!
That's exactly the bug.

### THE FIX

We need to **swap the two changes simultaneously**:

1. **Swap width/length in terrainData.size** — so the long dimension is on X:
```csharp
terrainData.size = new Vector3(
    manifest.terrain.terrain_length_m,  // X = 631 (LONG axis = illustration height)
    elevRange,
    manifest.terrain.terrain_width_m    // Z = 523 (SHORT axis = illustration width)
);
```

2. **Match the heightmap indexing** — image Y → first dim (now X=long), image X → second dim (now Z=short):
```csharp
heights[res - 1 - y, x] = val / 65535f;
// First dim (X/long axis) = res-1-y: image row, flipped (top of image = high X)
// Second dim (Z/short axis) = x: image column
```

3. **Swap terrain position:**
```csharp
terrainGO.transform.position = new Vector3(
    -manifest.terrain.terrain_length_m / 2f,  // X = long axis
    0f,
    -manifest.terrain.terrain_width_m / 2f    // Z = short axis
);
```

4. **Swap tileSize for texture:**
```csharp
layer.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
// This auto-adjusts since we already swapped size
```

5. **Swap anchor coordinates:**
The anchors currently use:
```json
{ "local": { "x": -127.7, "z": 260.1 } }
```
Where x was computed from `(normalized.x - 0.5) * terrain_width_m` and
z from `(normalized.y - 0.5) * terrain_length_m`.

After the swap, we need:
- World X (long axis) = from illustration Y (normalized.y) = the old z value
- World Z (short axis) = from illustration X (normalized.x) = the old x value

So swap anchor x and z, and negate the new X to flip the Y axis:
```csharp
// In PlaceAnchorMarker and CreateWalkCamera:
Vector3 worldPos = new Vector3(-anchor.local.z, 0f, anchor.local.x);
//                              ↑ old z, negated    ↑ old x
```

Wait — this is getting complicated and fragile. Let me simplify.

### SIMPLER APPROACH: Rotate the texture, don't swap terrain axes

Instead of reworking all the coordinate math, we can:

1. **Keep the terrain as-is** (width=X, length=Z, portrait orientation)
2. **Rotate the texture 90° CCW** before applying it, so it matches the
   TerrainLayer UV mapping

But this doesn't fix the heightmap rotation either.

### SIMPLEST APPROACH: Fix it in the export pipeline (Node.js)

The cleanest fix is to **rotate the heightmap and texture 90°** during
export so they're already in the orientation Unity expects.

### ACTUALLY — Let me try the simplest C#-only fix first

Keep everything but **swap ONLY terrainData.size X↔Z** and the terrain position:

```csharp
// In CreateTerrain:
terrainData.size = new Vector3(
    manifest.terrain.terrain_length_m,  // SWAP: long axis on X
    elevRange,
    manifest.terrain.terrain_width_m    // SWAP: short axis on Z
);

// Heights: direct mapping
heights[y, x] = val / 65535f;

// In ImportLiteHole, terrain position:
terrainGO.transform.position = new Vector3(
    -manifest.terrain.terrain_length_m / 2f,   // SWAP
    0f,
    -manifest.terrain.terrain_width_m / 2f     // SWAP
);
```

And in `PlaceAnchorMarker` and `CreateWalkCamera`, swap x and z:
```csharp
Vector3 worldPos = new Vector3(anchor.local.z, 0f, anchor.local.x);
```

And texture tileSize auto-reads from terrainData.size, so no change needed there.

This swaps the world-space layout so the long axis is along X, making the
terrain landscape in terms of the X/Z dimensions. But the content should now
be portrait because the heightmap rows (image Y, long axis) map to the first
SetHeights dimension which maps to... ugh, this circular reasoning isn't working.

### ACTUAL DEFINITIVE APPROACH

Let me stop guessing and make it empirically testable. Here's what to do:

**Change `CreateTerrain()` to try BOTH orientations and log which corner has
the highest elevation. Then compare to the illustration to verify.**

Actually no — let me just do the one thing that will obviously fix it:

**Swap terrainData.size X↔Z AND use `heights[y, x]`:**

The reasoning:
- `heights[y, x]` with y=0..128 in first dim, x=0..128 in second dim
- Unity maps first dim to one axis, second dim to another
- If the terrain appears landscape with `heights[y,x]` and size=(523, elev, 631),
  it means the first dim is mapping to Z (631m) and second to X (523m)
- The image Y (long) is in the first dim → maps to Z → but Z=631 → that's the
  long axis → that should be portrait...

OK I think the issue might be that Unity's Scene view default camera angle
is making it LOOK landscape when it's actually portrait. But you said the text
is sideways, which confirms it's truly rotated.

Let me just write the definitive changes:

### Changes to `HoleLiteImporter.cs`

**1. In `CreateTerrain()`, swap the size dimensions:**

```csharp
terrainData.size = new Vector3(
    manifest.terrain.terrain_length_m,  // X = long axis (was terrain_width)
    elevRange,
    manifest.terrain.terrain_width_m    // Z = short axis (was terrain_length)
);
```

**2. In `CreateTerrain()`, use simple indexing:**
```csharp
heights[y, x] = val / 65535f;
```

**3. In `ImportLiteHole()`, swap the terrain position:**
```csharp
terrainGO.transform.position = new Vector3(
    -manifest.terrain.terrain_length_m / 2f,
    0f,
    -manifest.terrain.terrain_width_m / 2f
);
```

**4. In `PlaceAnchorMarker()`, swap x↔z:**
```csharp
Vector3 worldPos = new Vector3(anchor.local.z, 0f, anchor.local.x);
```

**5. In `CreateWalkCamera()`, swap x↔z:**
```csharp
Vector3 pos = new Vector3(backTee.local.z, 0f, backTee.local.x);
```

### After re-importing, check:

1. Is the terrain portrait (tall along X) in Scene view?
2. Is the illustration text upright?
3. If the image is mirrored, add negation to one axis (e.g., `-anchor.local.z`)

**If the terrain is portrait but upside-down** (green at bottom instead of top),
change the heightmap to `heights[res - 1 - y, x]`.

**If the terrain is portrait but left-right mirrored**, change the heightmap
to `heights[y, res - 1 - x]`.

### Verification

- [ ] Terrain is portrait in top-down Scene view
- [ ] Illustration text reads correctly (upright, not sideways)
- [ ] Fairway runs top-to-bottom (or bottom-to-top — flip is fine, rotation is not)
- [ ] Anchor markers are on/near the terrain (not floating off in space)

### Do NOT

- Modify Node.js export scripts
- Modify HoleImporter.cs
- Change ApplyTexture (tileSize reads from terrainData.size automatically)

---

## Status Log

- 2026-04-06: Steps 1-6 COMPLETE
- 2026-04-06: Orientation fix attempt 1 (heights[y,x]) — still rotated
- 2026-04-06: Orientation fix attempt 2 (heights[x, res-1-y]) — still rotated
