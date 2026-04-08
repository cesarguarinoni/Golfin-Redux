# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fairway Fringe Ring

**Goal:** Add a visible transition border around fairway zones, similar
to how greens already have a fringe ring. Fairway should not blur
directly into rough — it should have a manicured edge.

**Approach:** Same dilation technique as the green fringe. Dilate the
fairway mask, paint the ring using the semi-rough texture (layer 2).
This creates a visible intermediate zone between fairway and rough.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### What to change

In `ApplySplatmap()`, after the existing green fringe block (step 3),
add a fairway fringe block:

```csharp
// --- 3b. Generate fringe ring around fairway ---
int fairwayFringeRadius = 2;
bool[] fairwayMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < resampledZones.Length; i++)
    fairwayMask[i] = (resampledZones[i] == 1); // zone 1 = fairway

bool[] dilatedFairway = DilateMask(fairwayMask, alphaRes, alphaRes, fairwayFringeRadius);

bool[] fairwayFringeMask = new bool[alphaRes * alphaRes];
for (int i = 0; i < fairwayFringeMask.Length; i++)
{
    if (dilatedFairway[i] && !fairwayMask[i])
    {
        int zone = resampledZones[i];
        // Only place fairway fringe on rough/semi-rough/trees (not on green, water, bunker, etc.)
        if (zone == 3 || zone == 4 || zone == 5)
            fairwayFringeMask[i] = true;
    }
}
```

Then in step 4 (build raw alphamap), add the fairway fringe check
before the existing fringe check:

```csharp
if (fringeMask[idx])
    layer = 7; // green fringe
else if (fairwayFringeMask[idx])
    layer = 2; // fairway fringe → semi-rough texture
else
    layer = ZoneToLayer(resampledZones[idx]);
```

Green fringe takes priority over fairway fringe (in case they overlap
near the green).

### Tunable

`fairwayFringeRadius = 2` — make this a `public static int` at the top
of the class so it can be tuned:

```csharp
public static int FairwayFringeRadius = 2;
```

---

### Verification

- [ ] Re-import Hole 1
- [ ] Fairway has a visible semi-rough border around it
- [ ] The border separates fairway from rough cleanly
- [ ] Green fringe still works (unaffected)
- [ ] No console errors

### Do NOT

- Modify green fringe logic
- Modify zone meshes or export pipeline
- Modify the mask map fix

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Cleaned up failed terrain sheen fixes
✅ DONE: 2026-04-08 — Swapped fairway/fringe textures + rotated fairway grain
✅ DONE: 2026-04-08 — Fairway fringe ring: semi-rough border around fairway via dilation
