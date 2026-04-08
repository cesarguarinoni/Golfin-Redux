# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fix Fairway Fringe Blend Direction

**Problem:** The re-stamp makes both fairway AND fringe edges hard. But
the fringe should blend outward into rough — only the fairway→fringe
boundary should be crisp.

**Fix:** Only re-stamp **fairway** pixels after the blur. Remove the
fairway fringe re-stamp. This way:
- Fairway has a hard edge (re-stamped to 100%)
- Fringe was painted as semi-rough before blur, so blur softens
  fringe→rough naturally (outward blend)
- Fairway→fringe boundary stays sharp because fairway is re-stamped

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### What to change

In the step 5b re-stamp block, **remove the fairwayFringeMask branch**.
Keep only the fairway re-stamp:

```csharp
// --- 5b. Re-stamp fairway for crisp edges ---
// Fairway gets hard edges. Fringe blurs outward into rough naturally.
for (int ay = 0; ay < alphaRes; ay++)
{
    for (int ax = 0; ax < alphaRes; ax++)
    {
        int idx = ay * alphaRes + ax;

        if (fairwayMask[idx])
        {
            for (int l = 0; l < layerCount; l++)
                alphamap[ay, ax, l] = 0f;
            alphamap[ay, ax, 0] = 1.0f; // layer 0 = fairway
        }
    }
}
```

---

### Verification

- [ ] Fairway edge is crisp (no bleed inward)
- [ ] Fringe blends softly outward into rough
- [ ] Clear visual: fairway → sharp edge → fringe → soft blend → rough

### Do NOT

- Modify the blur step
- Modify green fringe logic

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Cleaned up failed terrain sheen fixes
✅ DONE: 2026-04-08 — Swapped fairway/fringe textures + rotated fairway grain
✅ DONE: 2026-04-08 — Fairway fringe ring + sharp fairway edges (both sides hard — needs fix)
✅ DONE: 2026-04-08 — Fix fringe blend: only re-stamp fairway, let fringe blur outward
