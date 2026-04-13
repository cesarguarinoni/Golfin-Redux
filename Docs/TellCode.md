# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Natural OB↔Rough Transition

**Problem:** The OB boundary is visible as a hard seam between two
different grass textures (T_Rough vs T_OOB). Even with the smoothed
OB shape from UHole Lite, the splatmap resolution (1024px) makes the
boundary between two distinct textures visible.

**Goal:** Make the OB↔rough transition look natural — like grass
gradually becoming wilder/less maintained, not a painted line.

**Solution:** Two changes, both in `ApplySplatmap`:

### Part 1 — OB texture = tinted Rough (same grass family)

Instead of a separate OB texture, reuse the **same rough grass
texture** for OB but with different tiling and a subtle darker tint.
Same grass pattern = no visible texture seam at the boundary.

**Changes to the texture arrays** (~line 860):

```csharp
// BEFORE:
"T_OOB_Albedo",         // 8 out of bounds
// ...
"T_OOB_Normal",         // 8 out of bounds
// ...
float[] tileSizes = { 5f, 3f, 6f, 8f, 4f, 3f, 4f, 8f, 8f };

// AFTER:
"T_Rough_Albedo",       // 8 OB — same grass as rough, tinted darker
// ...
"T_Rough_Normal",       // 8 OB — same normal as rough
// ...
float[] tileSizes = { 5f, 3f, 6f, 8f, 4f, 3f, 4f, 8f, 10f };
//                                                      ^^^  was 8f
```

Then, after the TerrainLayer creation loop (~line 978), add a
tint to the OB layer using `diffuseRemapMax`. This tints the
albedo darker without needing a separate texture:

```csharp
// Tint OB layer slightly darker (same grass, less maintained look)
// diffuseRemapMin/Max remap the albedo RGB channels.
// Max < 1.0 = darker. Slightly yellow-green shift = dried grass.
layers[8].diffuseRemapMin = new Vector4(0f, 0f, 0f, 0f);
layers[8].diffuseRemapMax = new Vector4(0.75f, 0.82f, 0.55f, 1f);
// R=0.75 (reduce red), G=0.82 (keep most green), B=0.55 (reduce blue)
// Net effect: slightly darker, more yellow-green = unmaintained grass
```

**NOTE on `diffuseRemapMax`:** This is a `Vector4` property on
`TerrainLayer` that scales the RGB channels of the diffuse texture.
`(1,1,1,1)` = no change. Values < 1 darken that channel. The
different tile size (10f vs 8f for rough) prevents the two layers
from looking perfectly tiled at the same frequency, adding subtle
visual variation even at 100% opacity.

If the tint values don't look right in practice, the user will
adjust them — just get the mechanism in place.

### Part 2 — Soft blend at the boundary

After building the alphamap (after the main `for (ay/ax)` loop
that sets `alphamap[ay, ax, layer] = 1.0f`), add a smoothing
pass that blends the rough↔OB boundary over ~4 splatmap pixels:

```csharp
// --- Smooth rough↔OB boundary (4px blend) ---
// Since both layers use the same base texture (just tinted),
// blending creates a gradual color shift — not a texture seam.
if (obMask != null)
{
    const int blendRadius = 4;

    // Build distance-to-boundary field at alphamap resolution
    // (chamfer distance transform — same pattern as heightmap smoothing)
    float[] obBorderDist = new float[alphaRes * alphaRes];

    // Step 1: Find boundary pixels (rough↔OB adjacency)
    for (int i = 0; i < alphaRes * alphaRes; i++)
        obBorderDist[i] = 99999f;

    for (int ay = 0; ay < alphaRes; ay++)
    {
        for (int ax = 0; ax < alphaRes; ax++)
        {
            int idx = ay * alphaRes + ax;
            bool isOB = alphamap[ay, ax, 8] > 0.5f;
            bool isRough = alphamap[ay, ax, 3] > 0.5f;
            if (!isOB && !isRough) continue;

            // Check 4-neighbors for a rough↔OB transition
            bool border = false;
            if (ax > 0) {
                bool nOB = alphamap[ay, ax-1, 8] > 0.5f;
                bool nRough = alphamap[ay, ax-1, 3] > 0.5f;
                if ((isOB && nRough) || (isRough && nOB)) border = true;
            }
            if (!border && ax < alphaRes-1) {
                bool nOB = alphamap[ay, ax+1, 8] > 0.5f;
                bool nRough = alphamap[ay, ax+1, 3] > 0.5f;
                if ((isOB && nRough) || (isRough && nOB)) border = true;
            }
            if (!border && ay > 0) {
                bool nOB = alphamap[ay-1, ax, 8] > 0.5f;
                bool nRough = alphamap[ay-1, ax, 3] > 0.5f;
                if ((isOB && nRough) || (isRough && nOB)) border = true;
            }
            if (!border && ay < alphaRes-1) {
                bool nOB = alphamap[ay+1, ax, 8] > 0.5f;
                bool nRough = alphamap[ay+1, ax, 3] > 0.5f;
                if ((isOB && nRough) || (isRough && nOB)) border = true;
            }
            if (border) obBorderDist[idx] = 0f;
        }
    }

    // Step 2: Chamfer distance transform (forward + backward)
    for (int ay = 0; ay < alphaRes; ay++)
        for (int ax = 0; ax < alphaRes; ax++) {
            int idx = ay * alphaRes + ax;
            if (ax > 0) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx-1] + 1f);
            if (ay > 0) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx-alphaRes] + 1f);
        }
    for (int ay = alphaRes-1; ay >= 0; ay--)
        for (int ax = alphaRes-1; ax >= 0; ax--) {
            int idx = ay * alphaRes + ax;
            if (ax < alphaRes-1) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx+1] + 1f);
            if (ay < alphaRes-1) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx+alphaRes] + 1f);
        }

    // Step 3: Blend rough↔OB in the transition zone
    for (int ay = 0; ay < alphaRes; ay++)
    {
        for (int ax = 0; ax < alphaRes; ax++)
        {
            int idx = ay * alphaRes + ax;
            float dist = obBorderDist[idx];
            if (dist >= blendRadius) continue; // no blend needed

            bool isOB = alphamap[ay, ax, 8] > 0.5f;
            bool isRough = alphamap[ay, ax, 3] > 0.5f;
            if (!isOB && !isRough) continue; // only blend rough↔OB

            // Smoothstep falloff: 1.0 at boundary → 0.0 at blendRadius
            float t = dist / blendRadius;
            t = t * t * (3f - 2f * t); // smoothstep
            float blendAmount = 1f - t; // 1.0 at boundary, 0.0 at edge

            // Cross-fade: mix in 40% of the other texture at the boundary
            float mixStrength = blendAmount * 0.4f;

            if (isOB)
            {
                alphamap[ay, ax, 8] = 1f - mixStrength;
                alphamap[ay, ax, 3] = mixStrength;
            }
            else // isRough
            {
                alphamap[ay, ax, 3] = 1f - mixStrength;
                alphamap[ay, ax, 8] = mixStrength;
            }
        }
    }
}
```

Place this AFTER the main alphamap loop and BEFORE the
`terrainData.terrainLayers = layers` / `SetAlphamaps` lines.

---

### Verification

1. Import any hole with OB (e.g., Hole 18)
2. **At the boundary:** Gradual color shift from rough grass to
   slightly darker/yellower OB grass — no visible texture seam
3. **Deep in OB:** Fully the tinted grass look
4. **Deep in rough:** Normal rough texture, no change
5. **Grass pattern:** Same base pattern on both sides (since it's
   the same texture), just different tiling frequency and tint
6. **Other zones:** Completely unchanged (blend only touches
   layers 3 and 8)

### Tuning guide for Cesar
If the look isn't quite right after implementation:
- **Too similar:** Lower the `diffuseRemapMax` values (e.g., 0.6, 0.7, 0.4)
- **Too different:** Raise them closer to (1, 1, 1)
- **Blend too wide:** Reduce `blendRadius` from 4 to 2
- **Blend too narrow:** Increase to 6
- **Tile pattern too similar:** Change OB tileSize further from 10f

### Do NOT
- Create or require any new texture files
- Change the OB mask format or UHole Lite code
- Change any other zone's texture or splatmap logic
- Change heightmap smoothing or overlay mesh code

---

## Completed Tasks
✅ 2026-04-13 — Natural OB↔Rough transition: reuse rough texture with tint + 4px boundary blend
✅ 2026-04-13 — "Smooth OB" button in UHole Lite (vectorize → RDP → Chaikin → rasterize)
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix (OffsetContourOutward + spine polygon)
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-11 — Grid draping iterations, Y=0 origin pattern
✅ 2026-04-10 — Tree placement (mixed mode: terrain + standalone)
✅ 2026-04-10 — Bunker v1-v5 iterations
✅ All earlier tasks (water, bunkers, greens, textures, cart paths, etc.)
