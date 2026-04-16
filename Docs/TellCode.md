# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Previous Task — Bunker Lip: Bake as Submesh of Bunker CDT (DONE)

Add a sand-to-grass transition "lip" ring around each bunker using
the same submesh pattern used for fairway fringe, tee border, and
green collar. Read `Docs/LESSONS_FRINGE_BORDER_MESHES.md` first — the
four-piece recipe there is mandatory.

### Why

Bunker lip polish was previously deferred because overlapping meshes
Z-fight. The submesh approach (dilated CDT + original contour as
internal constraint + centroid classification + duplicated boundary
verts) is now proven across fairway, tee, and green collars. Same
recipe, new surface.

### Scope

**Both importers** — `HoleGeoImporter.cs` AND `HoleLiteImporter.cs`.
Match parity exactly. `Re-import Current Hole` dispatches to either,
so both must work.

Touch only bunker mesh creation (the method inside `CreateZoneMeshes`
or wherever bunker CDT lives in each importer) and any helper it
calls. Do not touch fairway, tee, green, water, or cart path code.

Applies to BOTH large bunkers (4-ring bowl) and small bunkers (5-ring
shingle overlap v7). Each bunker mesh, regardless of variant, gets
a lip submesh around its outer boundary.

### Implementation

Mirror the fairway/green submesh pattern on the bunker CDT:

1. **Dilate bunker contour outward by `lipWidth`** (start with
   `lipWidth = 0.4f` meters — adjustable). Run CDT on the dilated
   shape, not the original.
2. **Pass original bunker contour as internal CDT constraint** so
   triangle edges align with the sand/lip boundary (no jaggies).
3. **Classify triangles by CENTROID** against the original contour.
   Inside = sand submesh (existing bunker material), outside (in
   dilation ring) = lip submesh.
4. **Duplicate boundary verts** referenced by lip triangles so each
   material gets its own UVs.
5. Set `subMeshCount = 2` and assign
   `sharedMaterials = { bunkerSandMat, lipMat }`.

### Materials & UVs

- **Sand submesh:** keep current bunker material and current UV
  scheme unchanged.
- **Lip submesh:** use `MAT_Bunkers_Dark` — a gradient material where
  the LEFT side of the texture points at the bunker (sand side) and
  the RIGHT side points at the rough (outer side). This is the same
  UV scheme as the tee border — u encodes normalized distance to the
  original contour:

  ```csharp
  // For each lip-submesh vert:
  float dist = Mathf.Sqrt(DistanceSqToContour(v.x, v.z, originalBunkerPoly));
  float u = Mathf.Clamp01(dist / lipWidth);
  // u = 0 at the sand edge (left side of texture, near bunker)
  // u = 1 at the dilated outer boundary (right side of texture, rough)
  float vCoord = (v.x + v.z) / tileSize; // arbitrary per-perimeter tiling
  uv = new Vector2(u, vCoord);
  ```

  `DistanceSqToContour` already exists (used by tee border) — reuse.

**Asset path:** `Assets/Courses/Materials (Shared by courses)/MAT_Bunkers_Dark.mat` — load via `AssetDatabase.LoadAssetAtPath<Material>(...)`. The parent folder name has spaces and parentheses — the path must be exact including those characters.

**Texture tiling note:** the material has `m_Scale: {x: 1, y: 10}` on its `_BaseMap`, meaning the texture tiles 10x along v. The gradient direction is u (left→right, sand→rough). When computing UVs, make sure vCoord tiles in a range that interacts correctly with that 10x scale — a `tileSize` around 2–4 world meters on vCoord should land in a reasonable visible tile frequency. Adjust and eyeball.

### yOffset

- Sand interior: keep current bunker Y (the existing bowl depression
  + mesh lift values). No change.
- Lip: same Y as sand at the boundary — it's the same mesh, they
  share boundary vert Y. That's the whole point of the submesh
  approach. The old deferred "lip ~0.13m above terrain" idea is
  OBSOLETE and does NOT apply here. Do not raise the lip separately.

### Fallback

If dilated CDT fails (degenerate / self-intersecting on tight bunker
shapes, especially the small shingle-overlap bunkers), retry with
original contour and skip lip. Log a warning. A bunker without a
lip is better than a crash.

### Surface Detection — follow green collar pattern

The lip is a DIFFERENT gameplay surface from sand (it's essentially
rough-over-sand transition — different lie, different stance rules).
Follow the same marker pattern we set up for greens:

- Add `BunkerSurfaceInfo` MonoBehaviour (new file,
  `Assets/Scripts/Course/BunkerSurfaceInfo.cs`):

  ```csharp
  using UnityEngine;
  namespace Golfin.Course
  {
      // Attached to bunker GameObjects. Submesh 0 = sand,
      // submesh 1 = lip (sand-to-grass transition). Used by ball
      // physics to determine surface-specific lie/friction.
      public class BunkerSurfaceInfo : MonoBehaviour
      {
          public const int SubmeshSand = 0;
          public const int SubmeshLip  = 1;
      }
  }
  ```

- Attach to each bunker GameObject in the importer. No runtime
  wiring yet — just the marker.

### What NOT to Change

- Fairway / tee / green / water / cart path mesh generation
- Splatmap painting under bunkers
- Bunker bowl depression (heightmap `SetHoles` / depression logic)
- Small bunker shingle overlap v7 logic — still ships the 1.13x scale
  + 0.11m lift. The lip is additive to that.
- Ball physics, anything gameplay
- Existing `CDTTriangulate` signature — reuse the optional
  `innerConstraint` param added for fairway. Don't duplicate.

### Verification

Reimport hole 1 (has multiple bunkers including small ones) via both
`Import > Geo > Normal > Import Hole 01 Geo` AND
`Import > Lite > Normal > Import Hole 01 Lite`:

- [ ] Every bunker has a visible dark lip ring around its edge
- [ ] Lip gradient points the right way — darker/sand-adjacent near
      bunker, fading to rough at outer edge
- [ ] No Z-fighting between sand and lip on sloped bunkers
- [ ] Lip boundary follows the bunker contour smoothly (no jaggies)
- [ ] Small bunkers (shingle overlap) don't crash — fallback logs
      warning if CDT fails
- [ ] Large bunkers render correctly with lip
- [ ] Both Geo and Lite produce equivalent output
- [ ] `BunkerSurfaceInfo` component is on each bunker GameObject
- [ ] No console errors or warnings (other than intentional fallback)

---

## Previous Task — Green Collar: Bake as Submesh of Green CDT

Apply the same pattern that solved fairway fringe Z-fighting to greens:
bake the collar (green fringe / first cut around the putting surface)
into the green CDT mesh as a second submesh. Read
`Docs/LESSONS_FRINGE_BORDER_MESHES.md` first — the four-piece recipe
there is mandatory.

### Why

Greens currently render as a contour mesh overlay with no collar.
Adding a collar as a SEPARATE overlay mesh would Z-fight on slopes
(same problem fairway had). The proven fix is dilated CDT + original
contour as internal constraint + centroid classification + duplicated
boundary verts for per-material UVs.

### Scope

**Both importers** — `HoleGeoImporter.cs` AND `HoleLiteImporter.cs`.
Match parity exactly (Lite uses 90° CCW rotation, Geo uses direct
mapping — see lessons doc). `Re-import Current Hole` dispatches to
either, so both must work.

Touch only `CreateGreenMeshes` (and any helper it calls). Do not
touch fairway, tee, bunker, water, or cart path code.

### Implementation

Mirror the fairway submesh pattern in `CreateGreenMeshes`:

1. **Dilate green contour outward by `collarWidth`** (start with
   `collarWidth = 0.6f` meters — adjustable). Run CDT on the dilated
   shape, not the original.
2. **Pass original green contour as internal CDT constraint** so
   triangle edges align with the green/collar boundary (no jaggies).
3. **Classify triangles by CENTROID** against the original contour.
   Inside = green submesh, outside (in dilation ring) = collar submesh.
4. **Duplicate boundary verts** referenced by collar triangles so
   each material gets its own UVs.
5. Set `subMeshCount = 2` and assign
   `sharedMaterials = { greenMat, collarMat }`.

### Materials & UVs

- **Green submesh:** keep current green material and current UV
  scheme. No change.
- **Collar submesh:** new material slot. For now, reuse the fairway
  fringe material (`T_Fairway_Mix` or whatever the fairway fringe
  uses) with simple world-XZ tile UVs:
  `uv = new Vector2(v.x / tileSize, v.z / tileSize)` — same recipe as
  fairway fringe in the lessons doc. We can swap to a dedicated
  "first cut" texture later; for now functional > pretty.

### yOffset & Green Raise

Real greens sit slightly above the surrounding turf (~10–20cm). Since
green + collar are now one mesh, we can't just bump the whole mesh’s
Y (that would raise the collar too). Instead, lift only the green-side
verts with a smooth ramp through the collar so the boundary is a
gentle slope, not a cliff.

Constants:
```csharp
const float GreenRaiseMeters = 0.15f; // putting surface above collar
const float CollarYOffset    = 0.03f; // same as current green yOffset
```

Per-vertex Y after terrain sampling, before mesh assignment:

```csharp
// d = perpendicular distance from vert to the ORIGINAL green contour
//     (positive both inside and outside; use DistanceToContour helper)
// inside  = vert is inside original contour (use IsInsideContour)
//
// Inside the green: full raise.
// In the collar ring: ramp from 0 at outer boundary to full raise
//                     at inner boundary (the original contour).
float raise;
if (inside) {
    raise = GreenRaiseMeters;
} else {
    float t = 1f - Mathf.Clamp01(d / collarWidth); // 1 at contour, 0 at outer
    t = t * t * (3f - 2f * t);                      // smoothstep
    raise = GreenRaiseMeters * t;
}
v.y = terrainY + CollarYOffset + raise;
```

Result: outer collar edge sits at terrain + 3cm (matches fairway
fringe), ramps smoothly up across the collar, green proper sits at
terrain + 18cm. No cliff at the green/collar boundary, no terrain
depression needed, splatmap untouched.

**Important:** classify inside/outside by VERT position against the
original contour, not by which submesh the vert belongs to. Boundary
verts (the duplicated pair) should both compute the same Y — they sit
exactly on the contour, so `d ≈ 0` and ramp `t ≈ 1`, giving full raise
on both copies. That keeps the boundary watertight.

### Fallback

If dilated CDT fails (degenerate / self-intersecting on tight green
shapes), retry with original contour and skip collar. Log a warning.
A green without a collar is better than a crash.

### Surface Detection — IMPORTANT (note from Cesar)

The collar is a DIFFERENT gameplay surface (first cut / fringe) from
the green proper — different ball roll, different putting rules.
We need a way at runtime to tell which submesh the ball is on.

**Do NOT solve this in this task — just don't paint us into a corner.**
Leave a clean hook so we can wire it up later. Suggested approach
(implement only the marked-up part now):

- Tag the spawned green GameObject with submesh metadata. Add a tiny
  MonoBehaviour `GreenSurfaceInfo` (new file, in `Assets/Scripts/Course/`):

  ```csharp
  using UnityEngine;
  namespace Golfin.Course
  {
      // Attached to green GameObjects. Submesh 0 = putting surface,
      // submesh 1 = collar (first cut). Used by ball physics to
      // determine surface-specific roll/friction.
      public class GreenSurfaceInfo : MonoBehaviour
      {
          public const int SubmeshGreen  = 0;
          public const int SubmeshCollar = 1;
      }
  }
  ```

- Add `GreenSurfaceInfo` to the green GameObject in `CreateGreenMeshes`.
- Triangle-to-submesh lookup at runtime is straightforward via
  `MeshCollider` raycast → `RaycastHit.triangleIndex` → walk submesh
  triangle ranges. We'll wire that into ball physics later — out of
  scope here.

That's it for surface detection in this task. Just the component and
the constants. No physics changes, no ball code edits.

### What NOT to Change

- Fairway / tee / bunker / water / cart path mesh generation
- Splatmap painting
- Terrain depression
- Ball physics, putting code, anything gameplay
- Existing `CDTTriangulate` signature — if you added an optional
  `innerConstraint` param for fairway, reuse it. Don't duplicate.

### Verification

Reimport hole 1 (a hole with a clearly visible green) via both
`Import > Geo > Normal > Import Hole 01 Geo` AND
`Import > Lite > Normal > Import Hole 01 Lite`:

- [ ] Green has a visible collar ring around it (fairway-fringe-like
      texture, ~0.6m wide)
- [ ] No Z-fighting between green and collar on sloped greens
- [ ] Collar boundary follows the green contour smoothly (no jaggies)
- [ ] Sharp concave green shapes don't crash — fallback logs warning
- [ ] Both Geo and Lite produce equivalent output
- [ ] `GreenSurfaceInfo` component is on each green GameObject
- [ ] No console errors or warnings (other than intentional fallback)
- [ ] Green proper sits visibly raised above surrounding terrain (~15cm)
- [ ] No cliff at green/collar boundary — it ramps smoothly
- [ ] Outer collar edge sits flush with surrounding turf (no gap)

---

## Previous Task — Cart Path Depression: Flat Interior + Outward Ramp

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

✅ DONE: 2026-04-16 — Green collar CDT complete. Dilated CDT (0.6m), internal constraint, centroid classification, boundary vert duplication, GreenSurfaceInfo hook, MAT_Fringe collar with distance UV (light side faces green), 8cm smoothstep raise on putting surface.

✅ DONE: 2026-04-16 — Bunker lip submesh complete. Ring-based lip band (0.4m) baked as submesh 1 using OffsetContourOutward. Inner u=0 (sand side), outer u=1 (rough side). BunkerSurfaceInfo component attached. Both Geo and Lite importers updated for parity.

## Completed Tasks
✅ 2026-04-16 — Bunker lip baked as submesh 1 of bunker mesh (0.4m ring, MAT_Bunkers_Dark, BunkerSurfaceInfo)
✅ 2026-04-16 — Cart path outward smoothstep ramp (8 cells) — flat drop inside + gradual return outside
✅ 2026-04-16 — Cart path flat depression (fixed center splotch BUT created edge cliff — needs outward ramp)
✅ 2026-04-16 — Spline cart path depression footprint (matched to mesh, gradient ramp broke center)
✅ 2026-04-16 — Spline cart path meshes (smoother curves, keeper)
✅ 2026-04-16 — Fringe/border baked into parent CDT mesh as submesh
✅ 2026-04-14 — Water rework complete (6 iterations)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ All earlier tasks
