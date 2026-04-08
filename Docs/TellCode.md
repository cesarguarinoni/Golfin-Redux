# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fix Fairway Width Shrinkage

**Problem:** The middle fairway corridor is noticeably thinner than the
zone illustration. Two factors cause this:

1. **Chaikin smoothing** in the export pipeline shrinks the contour
   (corner-cutting pulls vertices inward on convex sections)
2. **Fringe ring** uses negative offset (-0.5m inward), eating into
   the fairway from inside

**Fix:** Two changes:

### Change 1: Compensate for Chaikin shrinkage in the export pipeline

In `Tools/UHoleLite/scripts/export-hole.mjs`, in `extractZoneContours`,
after the Chaikin smoothing step and before `ensureCCW`, add a dilation
step that pushes the smoothed contour outward to compensate for the
shrinkage. This only needs to apply to large polygons (fairways) — small
shapes like bunkers and greens aren't affected enough to matter.

Add this function before `extractZoneContours`:

```javascript
/**
 * Offset a closed polygon outward by a distance.
 * At each vertex, compute the average outward normal of its two edges
 * and push along it with miter correction.
 * Assumes CCW winding (outward = left of edge direction).
 */
function offsetPolygon(polygon, distance) {
  const n = polygon.length;
  if (n < 3) return polygon;

  const result = [];
  for (let i = 0; i < n; i++) {
    const prev = (i - 1 + n) % n;
    const next = (i + 1) % n;

    // Edge vectors
    const e1x = polygon[i].x - polygon[prev].x;
    const e1z = polygon[i].z - polygon[prev].z;
    const e1len = Math.sqrt(e1x * e1x + e1z * e1z) || 1;
    const e2x = polygon[next].x - polygon[i].x;
    const e2z = polygon[next].z - polygon[i].z;
    const e2len = Math.sqrt(e2x * e2x + e2z * e2z) || 1;

    // Outward normals (rotate 90° CCW for CCW polygon: (x,z) → (-z,x))
    const n1x = -e1z / e1len;
    const n1z =  e1x / e1len;
    const n2x = -e2z / e2len;
    const n2z =  e2x / e2len;

    // Average normal
    let avgx = n1x + n2x;
    let avgz = n1z + n2z;
    const avglen = Math.sqrt(avgx * avgx + avgz * avgz) || 1;
    avgx /= avglen;
    avgz /= avglen;

    // Miter correction
    const dot = n1x * avgx + n1z * avgz;
    let miter = dot > 0.1 ? distance / dot : distance;
    miter = Math.min(miter, distance * 3); // cap to prevent spikes

    result.push({
      x: parseFloat((polygon[i].x + avgx * miter).toFixed(2)),
      z: parseFloat((polygon[i].z + avgz * miter).toFixed(2)),
    });
  }

  return result;
}
```

Then in `extractZoneContours`, after the smoothPolygon call:

```javascript
contourMeters = smoothPolygon(simplified, smoothPasses);

// Compensate for Chaikin shrinkage on large polygons.
// Each Chaikin pass shrinks the polygon by roughly 0.5-1m on curves.
// Dilate outward proportional to the number of smooth passes.
if (smoothPasses > 0 && pixels.length > 5000) {
  const compensation = smoothPasses * 0.5; // ~0.5m per pass
  contourMeters = offsetPolygon(contourMeters, compensation);
}

contourMeters = ensureCCW(contourMeters);
```

This only applies to regions larger than 5000 pixels (fairways), leaving
bunkers and greens untouched.

### Change 2: NONE — Keep fringe inward

The fringe stays at -0.5m inward. Moving it outward would cause it to
spill into bunkers and other zones. The Chaikin compensation in Change 1
already accounts for both the Chaikin shrinkage AND the 0.5m fringe
eating into the fairway (compensation = passes × 0.5 ≈ 1.5m, which
covers the ~1m Chaikin shrinkage + 0.5m fringe).

---

### Verification

- [ ] Middle fairway corridor matches the zone illustration width
- [ ] Other fairway sections not bloated (compensation is proportional)
- [ ] Fringe ring appears OUTSIDE the fairway (not inside)
- [ ] No gap between fairway edge and fringe ring inner edge
- [ ] Bunkers and greens unchanged (compensation skipped for small shapes)
- [ ] No console errors

### Do NOT

- Change the RDP epsilon or Chaikin passes (stay at 3.0 / 3)
- Touch bunker, green, or water pipelines
- Modify tee or cart path meshes

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes (T_Fairway_Mix, ear-clip triangulation) + fringe ring (semirough, 0.5m inward)
✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: swap, fringe ring, blur removed, alphamap 1024, zone grid 2048
✅ DONE: 2026-04-08 — PNG + SVG zone import in Hole Viewer
✅ DONE: 2026-04-08 — Morphological close + various smoothing attempts
✅ DONE: 2026-04-08 — Re-enable normal maps (0.4 intensity) + aniso filtering (level 16) on all terrain textures
✅ DONE: 2026-04-08 — SDF-based smooth fairway border (replaced by mesh approach)
✅ DONE: 2026-04-08 — Vector contour rasterization (replaced by mesh approach)
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes with smooth edges
✅ DONE: 2026-04-08 — Tee border ring with gradient texture (T_TeeDark_Albedo)
❌ REVERTED: 2026-04-08 — Fairway Chaikin dilation reverted. Uniform offsetPolygon (0.5m/pass) bloated the upper fairway lip into the adjacent bunker while the thin middle corridor remained unfixed. The narrowness appears to be in the source zone illustration, not an artifact of Chaikin smoothing. Needs architect review — possible source data fix or a non-uniform approach.
