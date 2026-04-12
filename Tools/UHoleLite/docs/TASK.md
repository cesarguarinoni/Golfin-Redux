# TASK.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Handoff: `Tools/UHoleLite/docs/TASK.md`

---

## Current Task — Cart Path Branch Detection + Tee Clipping

**File:** `Tools/UHoleLite/scripts/export-hole.mjs`

### Two problems:

1. **Missing branch:** When a cart path forks (Y-junction), flood fill
   finds one connected region. `extractPathSpine` finds the two farthest
   contour vertices and produces one spine. The second branch is lost.

2. **Road under tees:** Cart path pixels overlap with tee zones. The
   spine runs through tee areas, producing a rendered path under the
   tee mesh.

### Fix 1: Skeleton-based spine with branch detection

Replace the current `extractPathSpine` (farthest-pair + left/right
averaging) with a pixel-level thinning approach that naturally finds
branches:

**Step A — Morphological thinning (Zhang-Suen or similar):**

Before converting to meter coordinates, work on the **pixel grid**.
Build a binary mask of the cart path region, then thin it to a
1-pixel-wide skeleton. Zhang-Suen is a classic 2-pass algorithm
that's easy to implement (~40 lines). The skeleton preserves
topology including all branches.

```javascript
function thinSkeleton(mask, w, h) {
  // Zhang-Suen thinning on binary mask
  // mask[y * w + x] = 1 for cart path, 0 otherwise
  // Returns modified mask with skeleton pixels = 1
  let changed = true;
  while (changed) {
    changed = false;
    // Sub-iteration 1
    const toRemove1 = [];
    for (let y = 1; y < h - 1; y++) {
      for (let x = 1; x < w - 1; x++) {
        if (!mask[y * w + x]) continue;
        // Count neighbors, transitions, check conditions...
        // (standard Zhang-Suen conditions)
        // If conditions met, mark for removal
      }
    }
    for (const idx of toRemove1) { mask[idx] = 0; changed = true; }
    // Sub-iteration 2 (different conditions)
    // ...
  }
  return mask;
}
```

Look up the Zhang-Suen algorithm details. It's well documented.

**Step B — Trace skeleton into chains:**

Walk the skeleton pixels. Find **junction pixels** (3+ skeleton
neighbors) and **endpoint pixels** (1 skeleton neighbor). Each
segment between two junctions (or junction+endpoint) is a separate
chain.

```javascript
function traceSkeletonChains(skeleton, w, h) {
  // Find junction and endpoint pixels
  // Walk from each endpoint/junction to the next
  // Returns array of chains, each chain = [{x,y}, ...]
}
```

**Step C — Convert chains to spines:**

Each chain becomes a separate spine. Convert pixel coords to meter
coords, apply RDP simplification and Chaikin smoothing (open polyline).

If a region has multiple chains, emit multiple cart path entries
in the output (each with its own spine + width). They share the
same contour but have separate spines.

### Fix 2: Clip cart path pixels against tee zones

Before running the cart path pipeline, remove any cart path pixels
that overlap with tee zones (zone 10). This prevents the spine
from running through tee areas.

In `extractCartPathContours`, after flood fill and before any
processing:

```javascript
// Remove pixels that overlap with tee zones
currentPixels = currentPixels.filter(([px, py]) => {
  return grid[py * w + px] !== 10; // 10 = tee_box
});
```

Actually, this should happen even earlier — before flood fill.
Create a working copy of the grid where zone 8 pixels that overlap
with zone 10 are cleared:

```javascript
// In extractCartPathContours, before flood fill:
const workGrid = Buffer.from(grid); // copy
for (let i = 0; i < workGrid.length; i++) {
  if (workGrid[i] === 8) {
    // Check if this pixel is also tee in the base grid
    // (zones.json merged grid may have overwritten tee with cart path)
    // Use a neighbor check: if surrounded by tee pixels, clear it
  }
}
```

Wait — the issue is simpler. The cart path zone (8) pixels extend
under tee areas because they were painted that way in UHole Lite.
The fix is: **after flood fill, mask out pixels where the zone grid
has tee (10) in the immediate vicinity**. Or better: check the
actual zone grid value — if it's 8, keep it; if it's 10, skip it.

But flood fill already checks `grid[idx] === targetZone` (8), so
tee pixels (10) shouldn't be included. The problem might be that
the cart path was painted OVER the tee in the GUI, so those pixels
really are zone 8 in the grid.

**Simplest fix:** After extracting the spine chains, clip each chain
against tee zone bounding boxes. Remove spine points that fall
inside any tee contour polygon. Split the chain at clip points
into separate segments.

OR: After skeleton thinning, remove skeleton pixels that fall
inside tee regions (zone 10 in the original grid or in a small
radius around it). This naturally splits the skeleton at tee
crossings, producing separate chains for each side.

```javascript
// After thinning, before chain tracing:
// Mask out skeleton pixels near tee zones
for (let y = 0; y < h; y++) {
  for (let x = 0; x < w; x++) {
    if (!skeleton[y * w + x]) continue;
    // Check if this pixel or nearby pixels are tee zone
    const margin = 2; // pixels
    let nearTee = false;
    for (let dy = -margin; dy <= margin && !nearTee; dy++) {
      for (let dx = -margin; dx <= margin && !nearTee; dx++) {
        const nx = x + dx, ny = y + dy;
        if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
          if (grid[ny * w + nx] === 10) nearTee = true;
        }
      }
    }
    if (nearTee) skeleton[y * w + x] = 0;
  }
}
```

This removes skeleton pixels near tees, naturally breaking the
skeleton into separate chains that stop before tee areas.

### Output format change

Currently each cart path region produces one entry with one spine.
After this change, a region with branches produces **multiple
entries**, each with:
- Its own `spine` array
- The same `width_m`
- Shared `contour` (the outer boundary of the whole region)
- A `parent_region` field indicating they came from the same
  connected region

```javascript
// For each skeleton chain in this region:
results.push({
  id: nextId++,
  pixel_count: currentPixels.length,
  contour: contourMeters,     // shared outer boundary
  spine: chainSpine,          // this chain's centerline
  width_m: minWidthM,
  parent_region: regionId,    // links branches together
  // ... center_local, size_m, etc.
});
```

The Unity importer (`CreateSpineStripMesh`) already handles
individual spine entries — it just gets multiple entries now.

### Verification

1. Re-export Hole 4: `node scripts/export-hole.mjs lomond-country-club 4`
2. `cart-paths.json` should have 2+ entries (one per branch)
3. No spine points inside tee zones
4. Re-import in Unity: both branches render as separate strip meshes
5. Cart paths stop at tee boundaries, don't go under tee meshes

### Do NOT
- Change the contour pipeline (traceBorder, RDP, Chaikin)
- Change the dilation logic
- Change any Unity-side code
- Break the output format for non-branching paths (single spine
  regions should produce identical output to before)
