# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Smooth Tee Skirt Seesaw (Voronoi Spokes)

Tee platforms + skirt + borderless mesh all look great. One remaining
visual: subtle diagonal/wavy banding on the sloped mound around raised
tees. This is the same **Voronoi seesaw pattern** we diagnosed and fixed
on the water shore ramp — the chamfer distance transform walks produce
~1-cell-wide coherent radial spokes in the distance field, which become
visible stripes when we lerp a 1m+ height differential across them.

**The fix is exactly the one we used for the water shore ramp (2026-04-17
task): Gaussian-blur the distance field before the lerp.** See the
lessons note and the `HoleGeoImporter.cs` water shore code for the
proven pattern.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**Scope:** ONE contained change inside `FlattenTerrainUnderTees`, between
the chamfer backward pass and the lerp loop.
**No changes to:** anything outside that window. Tee mesh, tee skirt
radius, platform Y computation, skip mask, `CreateTeeMeshFlat`,
`DepressTerrainUnderOverlays`, or any other system.

---

### The change

In `FlattenTerrainUnderTees` (around lines 3153–3210), the current flow
per tee region is:

1. Build `teeMask` and raise interior to `maxH` (unchanged)
2. Initialize `dist[]` — 0 inside teeMask, MaxValue outside (unchanged)
3. Forward chamfer pass (unchanged)
4. Backward chamfer pass (unchanged)
5. **[NEW] Gaussian blur on `dist[]`** ← insert here
6. Lerp pass: `t = dist/skirtRadiusCells`, smoothstep, lerp maxH→baseline (unchanged)

Insert the blur **immediately after the backward pass closes** (after
the line `dist[z, x] = Mathf.Min(dist[z, x], dist[z + 1, x - 1] + 1.414f);`
and its closing `}`), and **before** the `// Apply outward skirt ramp`
comment and its loop.

```csharp
// ─── Smooth the distance field to kill Voronoi seesaw spokes ───
// The teeMask has 1-cell boundary jaggies from polygon rasterization.
// The chamfer transform propagates those as coherent radial spokes in
// dist[], which become visible diagonal stripes when we lerp a 1m+
// height differential across them.
//
// A separable Gaussian on the continuous distance values averages out
// the per-spoke variation while preserving the overall gradient. Same
// fix we applied to the water shore ramp (see
// LESSONS_FRINGE_BORDER_MESHES.md / Docs/tasks history 2026-04-17).
//
// sigma=2.0 cells (radius=3) gives a ~5-cell effective kernel. The
// skirt is typically 7–14 cells wide (2m / metersPerCell at 2049 res),
// so the blur smooths spokes without softening the overall ramp.
{
    const int blurRadius = 3;
    const float blurSigma = 2.0f;
    int kernelSize = blurRadius * 2 + 1;
    float[] kernel = new float[kernelSize];
    float kernelSum = 0f;
    for (int i = 0; i < kernelSize; i++)
    {
        float dk = i - blurRadius;
        kernel[i] = Mathf.Exp(-(dk * dk) / (2f * blurSigma * blurSigma));
        kernelSum += kernel[i];
    }
    for (int i = 0; i < kernelSize; i++) kernel[i] /= kernelSum;

    // Horizontal pass: dist → tmp
    float[,] tmp = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            float sum = 0f;
            for (int k = 0; k < kernelSize; k++)
            {
                int sx = Mathf.Clamp(x + k - blurRadius, 0, hRes - 1);
                sum += dist[z, sx] * kernel[k];
            }
            tmp[z, x] = sum;
        }
    }

    // Vertical pass: tmp → dist (write back)
    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            float sum = 0f;
            for (int k = 0; k < kernelSize; k++)
            {
                int sz = Mathf.Clamp(z + k - blurRadius, 0, hRes - 1);
                sum += tmp[sz, x] * kernel[k];
            }
            dist[z, x] = sum;
        }
    }
}
```

That's the entire change. Nothing else in the function or the file is
touched.

---

### Why this is low-risk

- **The blur writes back to `dist[]` only.** `teeMask`, `heights`,
  `baseline`, and `skipMask` are all untouched.
- **The lerp loop unchanged.** It still reads `dist[z, x]`, still
  guards `teeMask`, `skipMask`, and `if (rampedH > heights[z, x])`.
- **Tee boundary cells (inside teeMask)** stay at dist=0 after the
  chamfer, but after blur they get a small positive value (pulled up
  by the blur's neighborhood sum from non-zero neighbors). That does
  NOT matter, because the lerp loop has `if (teeMask[z, x]) continue;`
  as its first guard — those cells are never overwritten. The
  platform flat interior remains exactly maxH.
- **Cells beyond `skirtRadiusCells`** were unaffected before (bailed on
  `if (d > skirtRadiusCells) continue;`). After the blur, they're still
  at a larger distance value and still bailed. Baseline terrain
  beyond the skirt is never touched.
- **Platform proven on water.** This exact pattern shipped on
  2026-04-17 for the shore ramp and killed the equivalent stripes.
- **Performance:** ~2049² × 14 ops × 2 passes per tee. On a typical
  hole with 3 tees that's ~300M adds — under a second on modern
  hardware. Negligible compared to overall import time.

---

### Verification

Re-import the hole from the screenshot:

- [ ] Tee top surface still flat (unchanged).
- [ ] Tee edge still crisp (no border ring — unchanged).
- [ ] Skirt mound: **stripes gone**, smooth gradient from tee edge to
      surrounding terrain.
- [ ] Mound gradient still feels right (not over-smoothed into a
      pancake or under-smoothed with residual banding).

Regression spot-checks:
- [ ] Hole 4 — big tee and small forward tee both smooth.
- [ ] Hole 1 — 3 tees, no new artifacts.
- [ ] Hole 18 — 6 small tees; with σ=2 blur the skirts' spokes should
      be gone on every one of them.
- [ ] Fairway, green, water, bunker, cart path — no visual change
      whatsoever on these. This change is tee-only.
- [ ] `Debug.Log` `platform cells` / `skirt cells` counts should be
      essentially unchanged (the lerp's guard conditions haven't
      changed; only the `dist[]` values feeding into them).

Tuning if needed:
- If stripes still faintly visible: raise `blurSigma` to `3.0f` and
  `blurRadius` to `4` (keep `radius = ceil(3*sigma)` relationship).
- If the skirt's tee-boundary edge looks softened (unlikely — the
  `teeMask` guard preserves the platform edge, but the *transition*
  cell at d≈1 might go from 0.98→0.85 lerp → slight rounding of the
  top-of-mound edge): drop `blurSigma` to `1.5f` and `blurRadius` to `2`.

---

### Do NOT change

- `CreateTeeMeshFlat` — tee mesh is perfect.
- The platform raise, the `skipMask` construction, the `baseline` clone.
- `TeeSkirtMeters`, the skirt radius computation.
- The lerp formula (smoothstep), the MAX-over-existing merge.
- `DepressTerrainUnderOverlays` — tees still get 0.40m drop under the
  mesh for z-fight clearance.
- Anywhere else in the file.

---

### Design note

The 2026-04-17 shore ramp fix taught us that chamfer distance is
**correct enough for distance queries** (the values are close to true
Euclidean distance) but **wrong in a structurally coherent way** at
the ~5% level — it produces radial spokes that look terrible when the
target quantity varies along the gradient. Blurring the distance field
is the cheapest, lowest-risk workaround. The "proper" fix (exact
polygon-edge distance like the final water shore ramp) is worth
pursuing only if we run into cases where the blur can't keep up. For
tees with ~1m vertical differential over 2m horizontal, the blur is
more than enough.

---

✅ DONE: 2026-04-18 — Added separable Gaussian blur (sigma=2, radius=3) on dist[] in FlattenTerrainUnderTees, inserted between backward chamfer pass and skirt ramp lerp. No other changes.
