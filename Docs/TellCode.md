# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Smooth Play↔Non-Play Terrain Transition

**Problem:** The terrain has an abrupt height cliff where the play area
meets the non-play area (OB/trees). The current system blends between
raw DEM and Gaussian-blurred DEM, but the blurred DEM still retains its
original elevation. If OB terrain is higher (hills) or lower than the
play surface, there's a visible shelf/cliff at the boundary.

**Goal:** Non-play terrain should START at the same height as the
adjacent play area, then gently slope to its actual DEM height. No
abrupt transitions. Hills beyond the play area should still exist but
their base must begin at play-area level.

**File:** `HoleLiteImporter.cs`, method `CreateTerrain`, in the
`// --- Smooth heightmap outside play area ---` section (~line 430).

---

### What to change

Replace the current blend approach:
```csharp
// CURRENT (simplified):
// blendFactor: 1.0 in play area, ramps to 0.0 over TransitionCells
// heights = Lerp(smoothed, raw, blendFactor)
```

With a **"start at boundary height, slope to DEM"** approach:

**Step-by-step:**

#### 1. Keep the existing distance transform and blur — they're still needed

The `isPlayArea`, `distToPlay`, and `smoothed` arrays stay.

#### 2. Build a "boundary height" field

For each non-play cell, we need to know what height the nearest
play-area boundary cell has. We can propagate this using a similar
chamfer pass:

```csharp
// --- Build boundary height field ---
// For play-area cells: boundaryHeight = own height
// For non-play cells: propagate from nearest play-area cell
float[] boundaryHeight = new float[actualRes * actualRes];
for (int i = 0; i < boundaryHeight.Length; i++)
    boundaryHeight[i] = isPlayArea[i]
        ? heights[i / actualRes, i % actualRes]
        : float.MinValue; // sentinel: not yet reached

// Forward pass — propagate boundary heights outward
for (int z = 0; z < actualRes; z++)
{
    for (int x = 0; x < actualRes; x++)
    {
        int idx = z * actualRes + x;
        if (isPlayArea[idx]) continue; // already set

        // Check neighbors that are closer to play area
        float bestH = float.MinValue;
        float bestD = float.MaxValue;

        void CheckNeighbor(int ni, float cost)
        {
            if (ni < 0 || ni >= boundaryHeight.Length) return;
            float nd = distToPlay[ni] + cost;
            // Pick neighbor with smallest distance to play
            // (= most direct path from boundary)
            if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
            {
                bestD = distToPlay[ni];
                bestH = boundaryHeight[ni];
            }
        }

        if (x > 0) CheckNeighbor(idx - 1, 1f);
        if (z > 0) CheckNeighbor((z - 1) * actualRes + x, 1f);
        if (x > 0 && z > 0) CheckNeighbor((z - 1) * actualRes + (x - 1), 1.414f);
        if (x < actualRes - 1 && z > 0) CheckNeighbor((z - 1) * actualRes + (x + 1), 1.414f);

        if (bestH > float.MinValue)
            boundaryHeight[idx] = bestH;
    }
}
// Backward pass
for (int z = actualRes - 1; z >= 0; z--)
{
    for (int x = actualRes - 1; x >= 0; x--)
    {
        int idx = z * actualRes + x;
        if (isPlayArea[idx]) continue;

        float bestH = boundaryHeight[idx];
        float bestD = (bestH > float.MinValue) ? distToPlay[idx] : float.MaxValue;

        void CheckNeighborB(int ni, float cost)
        {
            if (ni < 0 || ni >= boundaryHeight.Length) return;
            if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
            {
                bestD = distToPlay[ni];
                bestH = boundaryHeight[ni];
            }
        }

        if (x < actualRes - 1) CheckNeighborB(idx + 1, 1f);
        if (z < actualRes - 1) CheckNeighborB((z + 1) * actualRes + x, 1f);
        if (x < actualRes - 1 && z < actualRes - 1) CheckNeighborB((z + 1) * actualRes + (x + 1), 1.414f);
        if (x > 0 && z < actualRes - 1) CheckNeighborB((z + 1) * actualRes + (x - 1), 1.414f);

        if (bestH > float.MinValue)
            boundaryHeight[idx] = bestH;
    }
}

// Fallback: any cell still at sentinel gets normalizedFlat
for (int i = 0; i < boundaryHeight.Length; i++)
    if (boundaryHeight[i] <= float.MinValue)
        boundaryHeight[i] = normalizedFlat;
```

**NOTE:** The local function pattern (`void CheckNeighbor(...)`)
captures variables from the enclosing scope. This is valid C# 7+.
If there are issues, refactor to inline checks.

#### 3. Replace the blend step

Instead of `Lerp(smoothed, raw, blendFactor)`, use:

```csharp
// Step 3: Blend — play area keeps raw, non-play ramps from
// boundary height to smoothed DEM
for (int z = 0; z < actualRes; z++)
{
    for (int x = 0; x < actualRes; x++)
    {
        int idx = z * actualRes + x;

        if (isPlayArea[idx])
        {
            // Play area: keep raw DEM untouched
            continue;
        }

        float dist = distToPlay[idx];
        float bh = boundaryHeight[idx];
        float demH = smoothed[z, x]; // target = blurred DEM

        if (dist < TransitionCells)
        {
            // Smoothstep ramp: 0 at boundary → 1 at TransitionCells
            float t = dist / TransitionCells;
            t = t * t * (3f - 2f * t); // smoothstep
            heights[z, x] = Mathf.Lerp(bh, demH, t);
        }
        else
        {
            // Beyond transition: full smoothed DEM
            heights[z, x] = demH;
        }
    }
}
```

This replaces the existing block:
```csharp
// DELETE this block:
for (int z = 0; z < actualRes; z++)
{
    for (int x = 0; x < actualRes; x++)
    {
        float b = blendFactor[z * actualRes + x];
        heights[z, x] = Mathf.Lerp(smoothed[z, x], heights[z, x], b);
    }
}
```

The `blendFactor` array is no longer needed and can be removed.

#### 4. Update the debug log

```csharp
Debug.Log($"[HoleLiteImporter] Heightmap smoothing applied " +
    $"(radius={SmoothRadius}, transition={TransitionCells} cells, " +
    $"boundary-height propagation enabled)");
```

---

### Key Behavior

- **At play boundary edge (dist=0):** Non-play terrain = play area
  height. Zero height difference. No cliff.
- **In transition zone (0 < dist < 80 cells):** Terrain smoothly
  ramps from play height to actual DEM height via smoothstep.
- **Beyond transition (dist ≥ 80):** Full Gaussian-blurred DEM.
  Hills, valleys, natural terrain restored.
- **Play area:** Completely untouched — raw DEM detail preserved.
- **Special case — hills:** If OB terrain is 5m above play area,
  it will slope up starting from play level. The hill is still there,
  it just grows out of the play surface instead of appearing as a
  cliff.

### Do NOT
- Change `TransitionCells`, `SmoothRadius`, `SmoothSigma` values
- Change the distance transform or Gaussian blur logic
- Change the `isPlayArea` / OB mask logic
- Touch any code outside `CreateTerrain`
- Change depression, splatmap, or mesh overlay code

### Verification
1. Import Hole 01
2. Walk along the fairway edge — terrain should be flush, no cliff
3. Look at OB areas farther away — hills should still be visible
   but rising gradually from play level
4. Play area terrain should be identical to before (raw DEM detail)

---

## Completed Tasks
✅ 2026-04-13 — Smooth play↔non-play terrain transition: boundary-height propagation + smoothstep ramp (no more cliff)
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
