# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — SDF-based Smooth Fairway Border

**Goal:** Replace the jagged pixel-staircase fairway edge with smooth,
organic curves. The fairway fringe ring should be a crisp ~1m semi-rough
border with smooth contours — like real golf courses.

**Approach:** Compute a signed distance field (SDF) from the fairway
edge in the resampled zone grid. Use the SDF to place the fringe ring
and to smooth the fairway/rough boundary. No blur — this is a geometric
smoothing that produces clean curves from jagged pixel input.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### How it works

The current pipeline builds a binary `fairwayMask` then dilates it to
get a `fairwayFringeMask`. Both have pixel staircase edges because
they're derived from rasterized zone grid pixels.

The SDF approach replaces this with:
1. Build binary fairway mask (same as now)
2. Compute distance field: for each alphamap pixel, store its distance
   to the nearest fairway edge pixel. Positive = inside fairway,
   negative = outside.
3. Use the distance value to determine layer assignment:
   - `dist > fringeWidth` → fairway (layer 0 or 7 for mow stripes)
   - `0 < dist <= fringeWidth` → fairway (inside edge — keep as fairway)
   - `-fringeWidth <= dist < 0` → semi-rough fringe (layer 2)
   - `dist < -fringeWidth` → original zone (rough, trees, etc.)

Because the SDF is a continuous float field, the boundary between
regions follows smooth iso-lines instead of pixel staircase edges.

---

### What to change

#### 1. Add SDF computation helper

Add a new static method (before or after `DilateMask`):

```csharp
/// <summary>
/// Compute signed distance field from a binary mask.
/// Positive = inside mask, negative = outside.
/// Uses two-pass chamfer distance (fast approximation).
/// </summary>
static float[] ComputeSDF(bool[] mask, int w, int h)
{
    float[] dist = new float[w * h];
    float INF = w + h; // larger than any real distance

    // Initialize: 0 at edges, +INF inside, -INF outside
    for (int i = 0; i < w * h; i++)
    {
        int x = i % w;
        int y = i / w;
        bool val = mask[i];

        // Check if this pixel is on the edge (has a neighbor with different value)
        bool isEdge = false;
        if (x > 0     && mask[i - 1] != val) isEdge = true;
        if (x < w - 1 && mask[i + 1] != val) isEdge = true;
        if (y > 0     && mask[i - w] != val) isEdge = true;
        if (y < h - 1 && mask[i + w] != val) isEdge = true;

        if (isEdge)
            dist[i] = 0f;
        else
            dist[i] = val ? INF : -INF;
    }

    // Forward pass (top-left to bottom-right)
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            float sign = dist[i] >= 0 ? 1f : -1f;
            float abs = Mathf.Abs(dist[i]);

            if (x > 0)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i - 1]) + 1f);
            if (y > 0)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i - w]) + 1f);
            if (x > 0 && y > 0)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i - w - 1]) + 1.414f);
            if (x < w - 1 && y > 0)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i - w + 1]) + 1.414f);

            dist[i] = sign * abs;
        }
    }

    // Backward pass (bottom-right to top-left)
    for (int y = h - 1; y >= 0; y--)
    {
        for (int x = w - 1; x >= 0; x--)
        {
            int i = y * w + x;
            float sign = dist[i] >= 0 ? 1f : -1f;
            float abs = Mathf.Abs(dist[i]);

            if (x < w - 1)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i + 1]) + 1f);
            if (y < h - 1)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i + w]) + 1f);
            if (x < w - 1 && y < h - 1)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i + w + 1]) + 1.414f);
            if (x > 0 && y < h - 1)
                abs = Mathf.Min(abs, Mathf.Abs(dist[i + w - 1]) + 1.414f);

            dist[i] = sign * abs;
        }
    }

    return dist;
}
```

#### 2. Add tunable fringe width parameter

At the top of the class, add:

```csharp
/// <summary>Width of fairway fringe border in meters.</summary>
public static float FairwayFringeMeters = 1.5f;
```

#### 3. Replace fairway fringe logic in ApplySplatmap

**Remove** the entire `--- 3b. Generate fringe ring around fairway ---`
section (the `fairwayMask`, `dilatedFairway`, `fairwayFringeMask` block).

**Replace** with SDF-based fringe computation. Insert after the green
fringe section (step 3), before step 4:

```csharp
// --- 3b. SDF-based fairway fringe ---
bool[] fairwayMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < resampledZones.Length; i++)
    fairwayMask[i] = (resampledZones[i] == 1); // zone 1 = fairway

float[] fairwaySDF = ComputeSDF(fairwayMask, alphaRes, alphaRes);

// Convert fringe width from meters to alphamap pixels
// Terrain size / alphamap resolution = meters per pixel
float metersPerPixel = Mathf.Max(terrainData.size.x, terrainData.size.z) / alphaRes;
float fringePixels = FairwayFringeMeters / metersPerPixel;

// Build fringe mask from SDF: pixels just outside fairway edge
bool[] fairwayFringeMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < alphaRes * alphaRes; i++)
{
    // Outside fairway (negative SDF) but within fringe distance
    if (fairwaySDF[i] < 0f && fairwaySDF[i] >= -fringePixels)
    {
        int zone = resampledZones[i];
        // Only place fringe on rough/semi-rough/trees
        if (zone == 3 || zone == 4 || zone == 5)
            fairwayFringeMask[i] = true;
    }
}

// Also use SDF to smooth the fairway INSIDE edge.
// Pixels that are inside fairway zone but very close to the SDF=0
// boundary get smooth contours because the SDF iso-line at 0 is
// smooth. We override the zone-based assignment in step 4 using
// the SDF value instead of the raw zone grid.
bool[] sdfFairwayMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < alphaRes * alphaRes; i++)
    sdfFairwayMask[i] = (fairwaySDF[i] > 0f);
```

#### 4. Modify the alphamap loop (step 4) to use SDF masks

In the existing alphamap builder loop, change the fairway assignment to
use the SDF-derived masks instead of the raw zone grid:

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
            layer = 2; // fairway fringe → semi-rough (SDF-smoothed)
        else if (sdfFairwayMask[idx])
        {
            // SDF says this pixel is inside the fairway boundary
            // (smooth contour replaces jagged zone grid edge)
            layer = 0; // fairway

            // Mow stripes: alternate light/dark fairway
            float worldX = ((float)ax / (alphaRes - 1)) * terrainSizeX - terrainSizeX / 2f;
            float worldZ = ((float)ay / (alphaRes - 1)) * terrainSizeZ - terrainSizeZ / 2f;
            float proj = worldX * stripeDir.x + worldZ * stripeDir.y;
            int band = Mathf.FloorToInt(proj / MowStripeWidth);
            if (band % 2 != 0)
                layer = 7; // dark fairway stripe
        }
        else
        {
            layer = ZoneToLayer(resampledZones[idx]);
        }

        alphamap[ay, ax, layer] = 1.0f;
    }
}
```

Key change: **`sdfFairwayMask[idx]`** replaces the old
`layer == 0` check from `ZoneToLayer`. The SDF iso-line at 0 is
smooth, so the fairway boundary follows organic curves instead of
pixel staircases. Pixels that were fairway in the zone grid but
outside the SDF=0 curve become rough; pixels that were rough but
inside the SDF=0 curve become fairway. The net area stays the same
but the boundary shape is smoothed.

#### 5. Remove old FairwayFringeRadius usage

The `FairwayFringeRadius` field at the top of the class can stay
(not a breaking change) but is no longer used by the new SDF fringe.
Add a comment:

```csharp
/// <summary>DEPRECATED — replaced by FairwayFringeMeters + SDF.</summary>
public static int FairwayFringeRadius = 2;
```

---

### Verification

- [ ] Re-import Hole 1
- [ ] Fairway border has smooth, organic curves (no pixel staircase)
- [ ] Semi-rough fringe ring visible around fairway (~1.5m wide)
- [ ] Fringe only appears on rough/semi-rough/trees — not on bunkers,
      water, green, or cart path
- [ ] Mow stripes still alternate inside the fairway
- [ ] Green fringe ring still works (unchanged)
- [ ] No console errors
- [ ] Bunkers, water, tee boxes unaffected

### Do NOT

- Apply any Gaussian blur
- Modify zone meshes or export pipeline
- Change terrain layer assignments or textures
- Modify the mask map (MatteMaskMap.png)
- Touch the green fringe logic (step 3)

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
