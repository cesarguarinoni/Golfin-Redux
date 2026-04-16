# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Cart Path Depression: Flat Interior + Outward Ramp

Two problems that must BOTH be solved:
1. **Center splotch** — gradient ramp gave 0% drop at edges, terrain
   poked through mesh interior on concave slopes. Fixed by flat drop.
2. **Edge cliff** — flat 40cm drop creates a visible step at the
   path boundary. NEW problem from the flat drop fix.

**Solution: flat drop INSIDE the footprint + gradual ramp OUTSIDE.**

Same pattern as water shore depression: full depression under the
overlay, smoothstep ramp outside it that returns terrain to its
original height over a short distance.

### Implementation

In `HoleGeoImporter.cs`, `DepressTerrainUnderOverlays()`, replace
the ENTIRE cart path depression section (everything from the
`cartDepress` array through the cart path application loop) with:

```csharp
// Cart path: full flat drop inside, outward ramp outside
int cartRampCells = 8; // ramp width in heightmap cells (~1m)
int cartDepressedCount = 0;

// Step 1: Distance transform OUTWARD from cart path boundary
// (distance from nearest cart-path cell, for cells OUTSIDE the path)
float[,] distFromCart = new float[hRes, hRes];
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        distFromCart[hz, hx] = cartDepress[hz, hx] ? 0f : 99999f;

// Forward pass
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
    {
        if (hx > 0) distFromCart[hz, hx] = Mathf.Min(
            distFromCart[hz, hx], distFromCart[hz, hx - 1] + 1f);
        if (hz > 0) distFromCart[hz, hx] = Mathf.Min(
            distFromCart[hz, hx], distFromCart[hz - 1, hx] + 1f);
    }
// Backward pass
for (int hz = hRes - 1; hz >= 0; hz--)
    for (int hx = hRes - 1; hx >= 0; hx--)
    {
        if (hx < hRes - 1) distFromCart[hz, hx] = Mathf.Min(
            distFromCart[hz, hx], distFromCart[hz, hx + 1] + 1f);
        if (hz < hRes - 1) distFromCart[hz, hx] = Mathf.Min(
            distFromCart[hz, hx], distFromCart[hz + 1, hx] + 1f);
    }

// Step 2: Apply depression
for (int hz = 0; hz < hRes; hz++)
{
    for (int hx = 0; hx < hRes; hx++)
    {
        float dist = distFromCart[hz, hx];

        if (cartDepress[hz, hx])
        {
            // INSIDE path: full flat drop
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - dropNormalized);
            cartDepressedCount++;
        }
        else if (dist > 0 && dist <= cartRampCells)
        {
            // OUTSIDE path within ramp zone: smoothstep from
            // full drop (at boundary) to zero drop (at rampCells)
            float t = dist / cartRampCells;
            t = t * t * (3f - 2f * t); // smoothstep
            float rampDrop = dropNormalized * (1f - t);
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - rampDrop);
            cartDepressedCount++;
        }
    }
}
```

### Key Difference from Previous Gradient

The OLD gradient ramped INSIDE the footprint (edge=0%, center=100%)
→ mesh edges sat on un-depressed terrain → center splotch.

The NEW ramp is OUTSIDE the footprint. Inside = 100% flat drop
everywhere. The ramp only applies to cells beyond the path boundary,
gradually returning to undepressed terrain. The mesh sits on fully
depressed terrain everywhere. The terrain around the path gently
slopes down to meet the depressed level instead of a cliff.

### What NOT to Change

- The `cartDepress` mask construction (spline polygon marking)
- `_splineCartPathPolygons` population
- Fairway/tee depression (already working)
- Water shore depression
- Spline mesh generation

### Verification

Reimport the hole with the splotch AND the cliff:

- [ ] No terrain splotch showing through cart path interior
- [ ] No visible cliff at cart path edges
- [ ] Terrain gently slopes into the path boundary
- [ ] Cart path mesh sits cleanly above depressed terrain
- [ ] Other overlays unaffected
- [ ] No console errors

✅ DONE: 2026-04-16 — Flat inside + 8-cell outward smoothstep ramp implemented. Re-import to verify no splotch and no cliff.

---

## Completed Tasks
✅ 2026-04-16 — Cart path flat depression (fixed center splotch BUT created edge cliff — needs outward ramp)
✅ 2026-04-16 — Spline cart path depression footprint (matched to mesh, gradient ramp broke center)
✅ 2026-04-16 — Spline cart path meshes (smoother curves, keeper)
✅ 2026-04-16 — Fringe/border baked into parent CDT mesh as submesh
✅ 2026-04-14 — Water rework complete (6 iterations)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ All earlier tasks
