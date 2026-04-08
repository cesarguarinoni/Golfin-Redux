# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Sharp Fairway Edges (No Blur Bleeding)

**Problem:** The Gaussian blur in the splatmap pipeline bleeds fairway
into surrounding zones, making edges soft/gradual. Real fairway has a
crisp mowed edge — it shouldn't blend into rough.

**Fix:** After the blur + re-normalize step, re-stamp fairway pixels
back to 100% fairway. This preserves the blur for other zone transitions
(rough↔semi-rough, etc.) but gives fairway a hard edge.

Also re-stamp the fairway fringe pixels so they stay crisp too.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### What to change

In `ApplySplatmap()`, after step 5 (Gaussian blur + re-normalize),
add step 5b:

```csharp
// --- 5b. Re-stamp fairway and fairway fringe for crisp edges ---
// The blur softens all boundaries, but fairway should have sharp,
// manicured edges like a real golf course.
for (int ay = 0; ay < alphaRes; ay++)
{
    for (int ax = 0; ax < alphaRes; ax++)
    {
        int idx = ay * alphaRes + ax;

        if (fairwayMask[idx])
        {
            // Hard fairway: clear all layers, set fairway to 1.0
            for (int l = 0; l < layerCount; l++)
                alphamap[ay, ax, l] = 0f;
            alphamap[ay, ax, 0] = 1.0f; // layer 0 = fairway
        }
        else if (fairwayFringeMask[idx])
        {
            // Hard fairway fringe: clear all, set semi-rough to 1.0
            for (int l = 0; l < layerCount; l++)
                alphamap[ay, ax, l] = 0f;
            alphamap[ay, ax, 2] = 1.0f; // layer 2 = semi-rough
        }
    }
}
```

This goes right before step 6 (Create TerrainLayers and apply).

Note: `fairwayMask` and `fairwayFringeMask` are already computed in
steps 3b. Make sure they're accessible at this point (they should be
since they're local variables in the same method).

---

### Verification

- [ ] Re-import Hole 1
- [ ] Fairway has crisp, sharp edges — no bleeding into rough
- [ ] Fairway fringe (semi-rough border) also crisp
- [ ] Other zone transitions still smooth (rough↔semi-rough, etc.)
- [ ] Green fringe still works
- [ ] No console errors

### Do NOT

- Modify the blur itself (other zones still need it)
- Modify zone meshes or export pipeline

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Cleaned up failed terrain sheen fixes
✅ DONE: 2026-04-08 — Swapped fairway/fringe textures + rotated fairway grain
✅ DONE: 2026-04-08 — Fairway fringe ring (semi-rough border)
✅ DONE: 2026-04-08 — Sharp fairway edges: re-stamp fairway + fringe after blur
