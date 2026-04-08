# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Add Cart Path Contours + Increase Tee Smoothing

**Context:** The vector contour rasterization pipeline is working for
fairway boundaries — smooth curves confirmed. But two zones are still
jagged:

1. **Cart path (zone 8)** — was never added to the contour extraction.
   The export script doesn't extract it and the importer doesn't consume it.
2. **Tee boxes (zone 10)** — contours ARE being extracted and rasterized,
   but the smoothing isn't aggressive enough (small shapes need more
   Chaikin passes to round off the rectangular corners visibly).

---

### Part 1: Export — `Tools/UHoleLite/scripts/export-hole.mjs`

In the `exportHole` function, find the section that writes `zone-contours.json`.
Add cart path extraction and bump tee smoothing:

```javascript
// Cart path (zone 8)
const cartPaths = extractZoneContours(zonesData, terrainMeta, 8, 15, 1.5, 3);
// epsilon 1.5 = preserve the path shape, 3 Chaikin passes = smooth curves
```

Update the `zoneContoursOutput` to include cart paths:

```javascript
const zoneContoursOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  zones: {
    tee: tees,
    semi_rough: semiRough,
    cart_path: cartPaths,
  },
};
```

Also change the tee extraction to use 3 Chaikin passes instead of 2:

```javascript
const tees = extractZoneContours(zonesData, terrainMeta, 10, 15, 1.5, 3);
// was: epsilon 2.0, 2 passes → now: epsilon 1.5, 3 passes
```

Add a log line for cart paths:

```javascript
if (cartPaths.length > 0) {
  const contourStats = cartPaths.map(c =>
    `#${c.id}: ${c.contour.length}pts`
  ).join(', ');
  console.log(`  Cart path contours: ${contourStats}`);
}
```

### Part 2: Data classes — `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs`

Add `cart_path` field to `ZoneContoursZones`:

```csharp
[System.Serializable]
public class ZoneContoursZones
{
    public ZoneContourRegion[] tee;
    public ZoneContourRegion[] semi_rough;
    public ZoneContourRegion[] cart_path;
}
```

### Part 3: Importer — `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

In the `ApplySplatmap` method, find the section that loads `zone-contours.json`
and processes tee + semi-rough. Add cart path processing right after:

```csharp
// Cart paths (zone 8)
if (zcData.zones?.cart_path != null)
{
    // Clear original cart path pixels first
    for (int i = 0; i < resampledZones.Length; i++)
        if (resampledZones[i] == 8) resampledZones[i] = 4; // revert to rough

    foreach (var region in zcData.zones.cart_path)
        RasterizeContour(region, resampledZones, alphaRes, terrainData, 8);

    Debug.Log($"[HoleLiteImporter] Rasterized {zcData.zones.cart_path.Length} " +
              $"smooth cart path contour(s)");
}
```

---

### Steps to verify

1. Re-run export: `node scripts/export-hole.mjs lomond-country-club 1`
   - Console should show cart path contour stats
   - `zone-contours.json` should now have a `cart_path` array
2. Re-import in Unity: GOLFIN > Import Hole (Lite) > Hole 01
3. Check:
   - [ ] Cart path edges are smooth curves (no pixel staircase)
   - [ ] Tee box edges are noticeably smoother than before
   - [ ] Fairway boundaries still smooth (unchanged)
   - [ ] No console errors
   - [ ] Cart path texture (asphalt) still appears correctly

### Do NOT

- Touch fairway contour extraction (already working)
- Modify bunker, green, or water pipelines
- Change zone indices or ZoneToLayer mapping
- Apply any blur

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
✅ DONE: 2026-04-08 — Vector contour rasterization for fairway + tee + semi-rough (smooth zone boundaries)
✅ DONE: 2026-04-08 — Cart path contours + increased tee smoothing (ε1.5, 3 Chaikin passes)
