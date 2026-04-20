# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Per-Edge Adaptive Tee Skirt (fixes Hole 7 cliff + avoids cart-path wall + avoids hill erasure)

Previous attempt landed the unit fix but exposed two real problems
(documented in `Docs/TEE_SKIRT_INVESTIGATION.md`):

1. **Cart path cliff:** a 30m uniform skirt raises terrain under the
   cart paths; `DepressTerrainUnderOverlays` then cuts 0.4m out of
   that raised terrain → visible 0.4m wall next to the cart path.
2. **Hill topography erased:** a 30m uniform skirt raises the
   uphill/side of the tee too, not just the steep downhill side.

Per-cell adaptive radius is rejected because a prior attempt produced
sawtooth teeth along the skirt's outer boundary (noted in the existing
code comment at ~line 3281).

### The fix — per-edge adaptive radius

Compute `adaptiveM` **per contour edge segment**, not per cell or per
tee. Each skirt cell then uses the `adaptiveM` of the contour edge it's
closest to. Cells along the same edge share the same `adaptiveM` → no
teeth along an edge. Transitions occur at polygon vertices, where the
smoothstep ramp naturally blurs the seam.

This gives:
- Uphill side (drop ~0.5m): 2m skirt → hill shape preserved.
- Downhill side (drop ~7m): ~30m skirt → gentle grade, no cliff.
- Side edges: scaled proportionally.

And it fixes the cart-path cliff because per-edge radii are localized
to the side where drop exists; the skirt doesn't reach across the tee
to lift a cart path 10m away. Belt-and-suspenders: **also add cart
paths to the skipMask** (Step 3).

---

### Step 1 — Replace the uniform `worstAdaptiveM` with per-edge radii

Target: `FlattenTerrainUnderTees` in
`Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`,
lines ~3211–3238.

Replace the block that currently reads:

```csharp
// Worst-case adaptive radius for coarse cull: scan the tee's
// bbox + TeeMaxSkirtMeters neighborhood for the steepest drop.
float worstDrop = 0f;
int neighborhoodCells = Mathf.RoundToInt(TeeMaxSkirtMeters / metersPerCell);
// ... bbox computation ...
for (int z = bboxMinR; z <= bboxMaxR; z++)
    for (int x = bboxMinC; x <= bboxMaxC; x++)
    {
        float drop = maxH - baseline[z, x];
        if (drop > worstDrop) worstDrop = drop;
    }

float worstAdaptiveM = Mathf.Min(TeeMaxSkirtMeters,
    Mathf.Max(TeeSkirtMeters, 1.5f * worstDrop / TeeMaxRampSlope));
int worstAdaptiveCells = Mathf.CeilToInt(worstAdaptiveM / metersPerCell);
```

With per-edge computation:

```csharp
// Per-edge adaptive radius. For each contour segment, sample drop
// outward along the edge normal at 1m increments; take the MAX drop
// observed and derive the edge's skirt radius from it. This gives
// each side of the tee its own radius instead of forcing the uniform
// worst case everywhere.
float elevRange = terrainSize.y; // normalized → world metres

int nContourEdges = region.contour.Length;
float[] edgeAdaptiveM = new float[nContourEdges];
float worstEdgeM = 0f; // for coarse-cull bbox expansion

const float SamplingStepM = 1.0f;
const int SamplingStepsMax = 40; // sample up to 40m outward per edge

for (int ei = 0; ei < nContourEdges; ei++)
{
    int ej = (ei + 1) % nContourEdges;
    float ax = region.contour[ei].x, az = region.contour[ei].z;
    float bx = region.contour[ej].x, bz = region.contour[ej].z;

    float edx = bx - ax, edz = bz - az;
    float elen = Mathf.Sqrt(edx * edx + edz * edz);
    if (elen < 1e-6f) { edgeAdaptiveM[ei] = TeeSkirtMeters; continue; }

    // Outward normal. CCW contour → outward is (edz, -edx) normalized.
    float nx = edz / elen;
    float nzn = -edx / elen;

    // Sanity-check outward direction: probe 0.5m along the normal
    // from the edge midpoint; if inside the polygon, flip.
    float mx = (ax + bx) * 0.5f, mz = (az + bz) * 0.5f;
    if (IsPointInContour(region.contour, mx + nx * 0.5f, mz + nzn * 0.5f))
    {
        nx = -nx; nzn = -nzn;
    }

    // Max drop along this edge's outward normal.
    float maxEdgeDropM = 0f;
    for (int s = 1; s <= SamplingStepsMax; s++)
    {
        float sx = mx + nx * s * SamplingStepM;
        float sz = mz + nzn * s * SamplingStepM;
        int sCol = Mathf.RoundToInt((sx - terrainPos.x) / cellW);
        int sRow = Mathf.RoundToInt((sz - terrainPos.z) / cellH);
        if (sRow < 0 || sRow >= hRes || sCol < 0 || sCol >= hRes) break;
        float dropNorm = maxH - baseline[sRow, sCol];
        if (dropNorm <= 0f) continue; // uphill of platform — ignore
        float dropM = dropNorm * elevRange;
        if (dropM > maxEdgeDropM) maxEdgeDropM = dropM;
    }

    // Formula from the original adaptive design:
    //   dR = clamp(1.5 * dropM / MaxRampSlope, BaseSkirt, MaxSkirt)
    float rM = Mathf.Clamp(
        1.5f * maxEdgeDropM / TeeMaxRampSlope,
        TeeSkirtMeters,
        TeeMaxSkirtMeters);
    edgeAdaptiveM[ei] = rM;
    if (rM > worstEdgeM) worstEdgeM = rM;
}

int worstAdaptiveCells = Mathf.CeilToInt(worstEdgeM / metersPerCell);
```

**NOTE for Code:** `IsPointInContour(ContourPoint[] contour, float x, float z)`
may already exist in this file as a private helper. If not, add:

```csharp
private static bool IsPointInContour(ContourPoint[] c, float x, float z)
{
    bool inside = false;
    for (int i = 0, j = c.Length - 1; i < c.Length; j = i++)
    {
        if (((c[i].z > z) != (c[j].z > z)) &&
            (x < (c[j].x - c[i].x) * (z - c[i].z) /
                 (c[j].z - c[i].z) + c[i].x))
            inside = !inside;
    }
    return inside;
}
```

---

### Step 2 — In the exact-distance pass, pick per-cell `adaptiveM` from the nearest edge

Current code (~lines 3264–3286) already computes `minDistM` as the
distance to the nearest edge. Capture **which** edge is nearest and use
that edge's `adaptiveM`.

Replace:

```csharp
float minDistM = float.MaxValue;
for (int i = 0; i < nContour; i++)
{
    int j = (i + 1) % nContour;
    // ... per-edge distance math ...
    if (d < minDistM) minDistM = d;
}

// Uniform adaptive radius (per-tee worst case from pre-scan).
if (minDistM > worstAdaptiveM) continue;
float adaptiveM = worstAdaptiveM;
```

With:

```csharp
float minDistM = float.MaxValue;
int nearestEdge = 0;
for (int i = 0; i < nContour; i++)
{
    int j = (i + 1) % nContour;
    float ax = region.contour[i].x, az = region.contour[i].z;
    float bx = region.contour[j].x, bz = region.contour[j].z;
    float edx = bx - ax, edz = bz - az;
    float len2 = edx * edx + edz * edz;
    float t2 = len2 > 1e-10f
        ? Mathf.Clamp01(((wx - ax) * edx + (wz - az) * edz) / len2)
        : 0f;
    float px = ax + t2 * edx - wx;
    float pz = az + t2 * edz - wz;
    float d = Mathf.Sqrt(px * px + pz * pz);
    if (d < minDistM) { minDistM = d; nearestEdge = i; }
}

// Per-edge adaptive radius: cells along the same edge share a radius
// → no sawtooth. Radius varies only across contour VERTICES, where
// smoothstep naturally blurs the seam.
float adaptiveM = edgeAdaptiveM[nearestEdge];
if (minDistM > adaptiveM) continue;
```

The rest of the loop (smoothstep `t`, `rampedH`, raise-only guard) is
unchanged.

---

### Step 3 — Add cart paths to the skipMask (defensive)

Target: `FlattenTerrainUnderTees`, ~lines 3109–3140 (right after the
fairway/green skipMask population).

Add:

```csharp
// Cart-path cells: protect from tee-skirt raising. A wide adaptive
// skirt that lifts cart-path terrain produces a 0.4m wall after
// DepressTerrainUnderOverlays cuts the cart-path depression.
string cpPathForSkip = Path.Combine(exportPath, "cart-paths.json");
if (File.Exists(cpPathForSkip))
{
    var cpDataForSkip = JsonUtility.FromJson<CartPathsFile>(
        File.ReadAllText(cpPathForSkip));
    if (cpDataForSkip.cart_paths != null)
        foreach (var cp in cpDataForSkip.cart_paths)
        {
            if (cp.spine != null && cp.spine.Length >= 2)
            {
                float halfW = (cp.width_m > 0 ? cp.width_m : 2.5f)
                              / 2f + 0.30f;
                var sp = BuildSpinePolygon(cp.spine, halfW);
                if (sp != null)
                    MarkWorldContourCells(sp, skipMask,
                        hRes, terrainPos, terrainSize);
            }
            else if (cp.contour != null && cp.contour.Length >= 3)
            {
                MarkContourCells(cp.contour, skipMask,
                    hRes, terrainPos, terrainSize, 0f);
            }
        }
}
```

**NOTE for Code:** `FlattenTerrainUnderTees` runs *before*
`CreateSplineCartPaths`, so `_splineCartPathPolygons` is null at this
point — reconstruct from `cart-paths.json` directly. `BuildSpinePolygon`
is deterministic and matches what `CreateSplineCartPaths` later
builds. If `MarkWorldContourCells` isn't the right helper name, use
whatever marks a `Vector2[]` polygon footprint into a `bool[,]` mask in
world coords (mirror how `DepressTerrainUnderOverlays` ~line 3366 does
it for `cartDepress`).

Then in the exact-distance pass (~line 3246, just after
`if (teeMask[z, x]) continue;`), **re-introduce** the skipMask check:

```csharp
if (teeMask[z, x]) continue;
if (skipMask[z, x]) continue; // cart path / fairway / green
```

This reverses the earlier decision to ignore skipMask. With per-edge
radii the "skirt can't bridge across fairway" concern is much smaller
because downhill radii are applied only to the downhill edge, not
forced uniformly on all sides.

---

### Step 4 — Update the debug log

Replace the existing log line (~line 3302):

```csharp
Debug.Log($"[HoleGeoImporter] Tee {region.id}: " +
          $"platform h={maxH:F4}, " +
          $"base skirt={TeeSkirtMeters:F1}m, " +
          $"worst adaptive skirt={worstAdaptiveM:F1}m");
```

With:

```csharp
float minEdgeM = float.MaxValue, maxEdgeM = 0f;
for (int i = 0; i < edgeAdaptiveM.Length; i++)
{
    if (edgeAdaptiveM[i] < minEdgeM) minEdgeM = edgeAdaptiveM[i];
    if (edgeAdaptiveM[i] > maxEdgeM) maxEdgeM = edgeAdaptiveM[i];
}
if (edgeAdaptiveM.Length == 0) { minEdgeM = TeeSkirtMeters; maxEdgeM = TeeSkirtMeters; }
Debug.Log($"[HoleGeoImporter] Tee {region.id}: " +
          $"platform h={maxH:F4}, " +
          $"edge skirt min={minEdgeM:F1}m max={maxEdgeM:F1}m " +
          $"({edgeAdaptiveM.Length} edges)");
```

Asymmetric `min` vs `max` is the success signal.

---

### Verification

1. Reimport Hole 7 Geo.
2. Console: Tee 5 should log something like
   `edge skirt min=2.0m max=30.2m (32 edges)`. Tees on flatter terrain
   (e.g. Tee 4) log `min=2.0m max=2.0m`.
3. Screenshot the 3pm-clockwise side of Tees 3 and 5 — no cliff, gentle
   grade into the downhill.
4. Screenshot the OTHER sides (uphill, lateral) of the same tees — they
   should look similar to before; natural hill shape preserved.
5. Cart path next to any tee on Hole 7: no visible 0.4m wall.
6. Regression — Hole 4 Tee 1 (original motivating case): should still
   grade cleanly. Log shows `max=` around 15–30m.
7. Regression — Hole 1: all tees log `min=2.0m max=2.0m`.
8. Regression — Hole 12: tees near water hazard unchanged.

### Watch for

- **Contour winding.** Code assumes CCW; the midpoint-probe in Step 1
  fixes wrong-winding automatically. If a visibly-steep side logs
  `max=2.0m`, check the probe direction is firing.
- **`SamplingStepsMax = 40` = 40m sample range.** Safe upper bound for
  a golf hole; don't raise without evidence.
- **Edge midpoint sampling only.** UHoleGeo emits ~1.5m segments
  (RDP ε=1.0 + Chaikin), so midpoint is representative. If a future
  contour has long edges, sample at 1/4 and 3/4 too.
- **Cart paths and fairways are both in skipMask now** — same as the
  safe baseline.

### Do NOT change

- The `Lerp(maxH, baseline[z, x], t)` ramp formula (both sides
  normalized → correct).
- `CreateTeeMeshWithInsetBorder` (platformY flatten is coupled to the
  terrain flatten pass; leave it).
- `DepressTerrainUnderOverlays` (0.05m tee depression is correct).
- `TeeMaxRampSlope`, `TeeMaxSkirtMeters`, `TeeSkirtMeters` constants.

### If per-edge produces visible seams at vertices

Unlikely given typical UHoleGeo contours (adjacent edges within ~10°
of each other), but if visible: after the ramp pass, run 2 iterations
of a 3×3 box blur on `heights` constrained to cells where
`coarseDist > 0 && coarseDist <= worstAdaptiveCells`. Hold in reserve;
don't apply preemptively.

❌ REVERTED: 2026-04-20 Per-edge implementation caused every slope to stair-step. Commit 6151e8d7 reverted at b7f70112. Approach needs rethinking — see investigation notes below or in AI_CONTEXT.

---

## Previous Task — Cart Path Junction Endpoint Snapping (Unity-side safety net)

Hole 1 screenshot shows a grass triangle poking into the cart path at a
3-way junction in the middle section. Cause confirmed from
`cart-paths.json` data: at junction `(-234.x, -123.x)`, paths 6 and 7
share an endpoint at `(-234.62, -123.27)` but path 8's nearest endpoint
is at `(-234.19, -123.69)` — **0.60m off**.

Each cart path renders as its own independent ribbon strip with an
endcap at the last spine knot. At a clean junction, three endcaps butt
together; at a drifted junction like this one, the 0.6m gap leaves a
visible grass wedge. The other three 3-way junctions on Hole 1
(`(228.99, 28.88)`, `(33.07, -10.85)`, `(-27.73, 0.14)`) have all
endpoints coincident to 0.00m — those render cleanly already.

### The fix: pre-pass endpoint clustering in `CreateSplineCartPaths`

Before building any splines, walk all spine endpoints (first + last knot
of every `cp.spine`) and cluster any within **0.75m** of each other.
Snap every endpoint in a cluster to the cluster's centroid. Then proceed
with normal spline construction using the snapped coordinates.

This is a **Unity-side safety net** — UHoleGeo will get the proper
skeleton-endpoint cleanup as a follow-up task. For now Unity fixes the
symptom at import time without touching the pipeline. Clusters of size 1
(non-junction endpoints) are skipped → zero effect on dead-end paths.

### Target file

`Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`, single
method: `CreateSplineCartPaths` (near line 5012).

**No changes to:**
- `cart-paths.json` schema or the UHoleGeo export pipeline.
- Any other mesh builder, depression pass, or material.
- `HoleLiteImporter.cs` (Lite pipeline is not active; skip).

### Step 1 — Add the snap helper (private static method in the same class)

Place this method next to `CreateSplineCartPaths` (anywhere in the
class is fine; adjacent is nicest for readability).

```csharp
/// <summary>
/// Snap cart path spine endpoints (first + last knot of each spine)
/// to the centroid of any cluster of endpoints within snapRadiusM of
/// each other. Mutates the spine arrays in place. Interior knots are
/// untouched.
///
/// Fixes visible grass wedges at 3+ way junctions where UHoleGeo's
/// skeleton extraction produces slightly different pixel-center
/// endpoints for paths that logically share a junction point.
/// </summary>
private static void SnapCartPathJunctionEndpoints(
    CartPath[] cartPaths, float snapRadiusM)
{
    if (cartPaths == null || cartPaths.Length == 0) return;

    // Collect all endpoint references as (pathIdx, isLast) pairs.
    // Using index pairs (not direct refs) so we can mutate spine[0]
    // or spine[last] in place after clustering.
    var refs = new List<(int pathIdx, bool isLast)>();
    for (int i = 0; i < cartPaths.Length; i++)
    {
        var cp = cartPaths[i];
        if (cp.spine == null || cp.spine.Length < 2) continue;
        refs.Add((i, false)); // first knot
        refs.Add((i, true));  // last knot
    }

    if (refs.Count == 0) return;

    // Simple O(N²) clustering — N is tiny (2 × number of cart paths,
    // typically ≤ 20). Union-find would be more correct for transitive
    // chains but isn't warranted here.
    float snapSqr = snapRadiusM * snapRadiusM;
    int[] cluster = new int[refs.Count];
    for (int i = 0; i < cluster.Length; i++) cluster[i] = i;

    for (int i = 0; i < refs.Count; i++)
    {
        var (pi, li) = refs[i];
        var ei = li ? cartPaths[pi].spine[cartPaths[pi].spine.Length - 1]
                    : cartPaths[pi].spine[0];
        for (int j = i + 1; j < refs.Count; j++)
        {
            if (cluster[j] != j) continue; // already clustered
            var (pj, lj) = refs[j];
            var ej = lj ? cartPaths[pj].spine[cartPaths[pj].spine.Length - 1]
                        : cartPaths[pj].spine[0];
            float dx = ei.x - ej.x, dz = ei.z - ej.z;
            if (dx * dx + dz * dz <= snapSqr)
                cluster[j] = cluster[i];
        }
    }

    // Group endpoints by cluster id; compute centroid; snap each member.
    var groups = new Dictionary<int, List<int>>();
    for (int i = 0; i < cluster.Length; i++)
    {
        if (!groups.TryGetValue(cluster[i], out var list))
        { list = new List<int>(); groups[cluster[i]] = list; }
        list.Add(i);
    }

    int snappedCount = 0;
    int junctionCount = 0;
    foreach (var kvp in groups)
    {
        var members = kvp.Value;
        if (members.Count < 2) continue; // singleton = not a junction

        float cx = 0f, cz = 0f;
        foreach (int memberIdx in members)
        {
            var (pi, li) = refs[memberIdx];
            var e = li ? cartPaths[pi].spine[cartPaths[pi].spine.Length - 1]
                       : cartPaths[pi].spine[0];
            cx += e.x; cz += e.z;
        }
        cx /= members.Count; cz /= members.Count;

        foreach (int memberIdx in members)
        {
            var (pi, li) = refs[memberIdx];
            int knotIdx = li ? cartPaths[pi].spine.Length - 1 : 0;
            var e = cartPaths[pi].spine[knotIdx];
            float dBefore = Mathf.Sqrt(
                (e.x - cx) * (e.x - cx) + (e.z - cz) * (e.z - cz));
            cartPaths[pi].spine[knotIdx] = new ContourPoint
            {
                x = cx,
                z = cz,
            };
            if (dBefore > 0.001f) snappedCount++;
        }

        junctionCount++;
        Debug.Log($"[HoleGeoImporter] Junction cluster: " +
                  $"{members.Count} endpoints → centroid ({cx:F2}, {cz:F2})");
    }

    if (junctionCount > 0)
        Debug.Log($"[HoleGeoImporter] Cart path junction snap: " +
                  $"{junctionCount} junction(s), {snappedCount} endpoint(s) " +
                  $"moved within {snapRadiusM:F2}m radius.");
}
```

**NOTE for Code:** `ContourPoint` is a struct in this file, so
`cartPaths[pi].spine[knotIdx] = new ContourPoint {...}` replaces the
element in place. `CartPath` and `CartPathsFile` DTO types are defined
elsewhere in the file — if `spine`'s actual type is `List<ContourPoint>`
instead of `ContourPoint[]`, use `spine[knotIdx] = ...` and
`spine.Count` / `spine[spine.Count - 1]` instead. Verify the exact field
shape before compiling.

### Step 2 — Call the helper at the top of `CreateSplineCartPaths`

Immediately after loading `cpData`, before the foreach loop that
iterates cart paths. The spot is ~4 lines into the method:

```csharp
var cpData = JsonUtility.FromJson<CartPathsFile>(
    File.ReadAllText(cpPath));
if (cpData.cart_paths == null || cpData.cart_paths.Length == 0) return;

// Junction endpoint snapping — fixes grass wedges at 3+ way junctions
// where UHoleGeo's skeleton extraction gives each path a slightly
// different pixel-center endpoint. 0.75m radius chosen just above the
// observed 0.6m gap on Hole 1 (junction at (-234.x, -123.x), paths 6/7/8).
SnapCartPathJunctionEndpoints(cpData.cart_paths, 0.75f);
```

No other changes to `CreateSplineCartPaths`. The rest of the method
(spline construction, ribbon mesh generation, depression polygon via
`_splineCartPathPolygons`) consumes the already-snapped spines
automatically.

### Verification

1. Reimport Hole 1 Geo.
2. Screenshot the middle-section 3-way junction (the one in the attached
   reference, approximately world `(-234, -123)`).
3. Expect: grass wedge gone, three ribbons meet cleanly.
4. Console log should show (approximately):
   - Four `Junction cluster: ...` lines for Hole 1 (one per junction).
   - The `(-234.x, -123.x)` cluster centroid ≈ average of the three
     drifted endpoints.
   - `Cart path junction snap: 4 junction(s), 1 endpoint(s) moved ...`
     (path 8's endpoint moves ~0.6m, paths 6/7 were already at a common
     point and shift <0.3m to the new centroid — only the 0.6m move
     clears the 0.001m threshold; the three already-clean junctions
     log but with 0 endpoints moved).
5. The three clean junctions on Hole 1 (paths 1/3/4 at `(228.99, 28.88)`,
   paths 2/3 at `(33.07, -10.85)`, paths 4/5 at `(-27.73, 0.14)`) render
   identically to before — no visual regression.
6. Check Holes 2–18: every hole with cart paths should import normally.
   Watch console for unexpected cluster log lines that might indicate
   unrelated paths being wrongly merged at 0.75m.

### Watch for

- **4-way+ junctions:** the clustering handles arbitrary N correctly —
  centroid of N points, no special-case code.
- **Non-junction endpoints:** paths whose first or last knot is a true
  dead-end (no other path within 0.75m) land in singleton clusters and
  are skipped, no mutation.
- **Transitive chains** (A near B, B near C, A far from C): the simple
  O(N²) clustering above does NOT merge these into one cluster. Current
  course data does not exhibit this pattern. If a future hole does,
  upgrade to union-find; for now keep the simpler code.
- **`CartPath` DTO field access:** if `cp.spine` is `ContourPoint[]`,
  the code above compiles as-is. If it's a `[Serializable]` list or
  uses a different field name, adjust accordingly.

### Do NOT

- Modify `CreateSpineStripMesh`, `BuildSpinePolygon`, or the
  ribbon-strip generation.
- Touch `DepressTerrainUnderOverlays` or the cart path depression
  polygon logic — those consume the snapped spines via
  `_splineCartPathPolygons` automatically.
- Make any change to UHoleGeo (`export-hole.mjs`, `classify-zones.mjs`,
  skeleton extraction). Pipeline-side fix is a separate task.
- Hardcode Hole 1 or specific junction coords. The pre-pass runs
  unconditionally on every hole; singleton clusters are skipped.
- Change the 0.75m radius without asking. Chosen just above the observed
  0.6m gap; looser risks merging distinct nearby paths, tighter risks
  missing the target.

### Out of scope (future task)

UHoleGeo-side fix: when extracting spines from the cart-path pixel mask,
detect degree-3+ skeleton nodes and emit a single shared endpoint
coordinate for all paths meeting there. Will land as a separate TellCode
block against `Tools/UHoleGeo/scripts/export-hole.mjs` once the symptom
is confirmed fixed by this Unity-side pass.

✅ DONE: 2026-04-20 SnapCartPathJunctionEndpoints() added as private static method before CreateSplineCartPaths. Called immediately after cpData null check with 0.75m radius. ContourPoint is a class (not struct) so used e.x/e.z mutation in place. CartPathRegionData[] used (not CartPath[]). Commit 6eb1bc9e.

✅ DONE: 2026-04-20 UHoleGeo pipeline fix: missing B-C cart path segment. Root cause: the minSpinePixels=20 filter removed chain[4] (len=15), making junction C a 2-way point, causing chains 3+5 (the B-C link) to merge and disappear. Fix: after building longChains (len>=minSpinePixels), identify 2-way junctions in that set and rescue any short chain (len>=dsFactor*2=6) whose endpoint touches a 2-way junction. This upgrades it to 3-way, preserving the B-C path. Hole 1 now exports 10 cart paths (was 6) including the B-C link as path 6. cart-paths.json copied to both hole-01 and hole-01-geo. Commit abd9f238.

---

## Previous Task — Water Shore Phase 2c: Reorder CreateWaterMeshes After DepressTerrainUnderOverlays

Phase 2b ablation confirmed Hypothesis B: serrations are the depression-cliff at
the water polygon boundary, not the shore ramp. Water mesh samples original
terrain height at contour vertices → mesh floats above the depressed floor
after depression runs → per-cell cliff face exposed at the boundary.

Fix: call `CreateWaterMeshes` **after** `DepressTerrainUnderOverlays` so the
mesh samples the already-depressed terrain. Mesh edge and depressed floor
are then co-planar at the boundary → no cliff → no serrations.

### The change (single file: `HoleGeoImporter.cs`)

In `ImportHoleInternal`, move the `CreateWaterMeshes` call from its current
position (progress 0.59, before `FlattenTerrainUnderTees`) to **immediately
after** `DepressTerrainUnderOverlays(terrainData, terrainGO, exportPath);`
(currently at roughly line 305).

The new order:
```
CreateZoneMeshes          // bunkers
CreateGreenMeshes         // greens
// (water creation removed from here)
FlattenTerrainUnderTees
CreateFlatZoneMeshes      // fairway, tee, cart path
... anchor placement ...
DepressTerrainUnderOverlays   // all terrain mutation done here
CreateWaterMeshes         // NEW POSITION — samples depressed terrain
terrainData.SetHoles(0, 0, holes);
```

Adjust the `EditorUtility.DisplayProgressBar` call label / percentage as
needed (`"Creating water..."` at ~0.58 is fine).

### Verification

1. Reimport Hole 12 Geo.
2. Screenshot the same steep diagonal bank as the Phase 2b ablation.
3. Expect: serrations gone, water mesh edge flush with grass, no cliff.
4. Also check Hole 7 and Hole 13 (the other hillside water bodies from
   Phase-1 sampling) — should also be clean.
5. Check Hole 1 or any flat-water hole to confirm no regression on gentle banks.

### Watch for

- **`waterY` now samples depressed terrain.** `minTerrainH` over contour
  vertices will now be ≈ `surfNorm` instead of the original terrain min.
  This is correct — the water surface should sit at the depressed shore
  height, not above it. But it means the water mesh will be lower than
  before by ~`ShoreDepthMeters` (0.4m). If the water surface looks too
  low relative to the expected real-course level, that's a downstream
  tuning question, not a regression.
- **Underwater floor exposure.** `waterFloorY = minTerrainH - 0.05 - 0.3m`
  stays below `waterY` by 0.35m, so bed is still submerged. Good.
- **No changes needed to the shore ramp.** Leave `ShoreRadius = 10` and
  the adaptive-radius constants alone. The ramp is now doing its job
  (gentle shore softening) without the cliff masking its work.

### Do NOT

- Touch the shore ramp formula, `ShoreMaxRampSlope`, or `ShoreMaxRadiusMeters`.
- Change `CreateWaterMeshes` internals (the sampling logic, `waterY`
  computation, mesh construction). Just move the call site.
- Reorder anything else. Bunkers, greens, tees, fairways, cart paths,
  anchor placement all stay in their current positions.

### Original position

✅ DONE: 2026-04-20 Phase 2c complete. Fix: reverted CreateWaterMeshes to original position (before depression) to restore correct waterY. Added inner collar ramp in DepressTerrainUnderOverlays — reverse chamfer from boundary inward, smoothstep from surfaceNorm (at edge) to waterFloorY (at ShoreRadius cells in). Both sides of polygon boundary now at surfaceNorm — no cliff, no serrations. Verified working on Hole 12.

## Previous Task — Water Shore Phase 2b: Diagnostic Ablation (ShoreRadius=0)

Three Phase 2 attempts all failed (see investigation findings further down). Code
correctly flagged that the adaptive-radius approach is architecturally mismatched
with hillside ponds. Before speccing a new fix, we need to **confirm which component
is actually causing the screenshot artifact**. The tee→water analogy may be wrong
at a more fundamental level than the radius size.

### The key question

Is the serrated grass in the Hole 12 screenshot caused by:
- **(A)** the shore ramp itself — `Lerp(nearSurfY, originalH, t)` forcing boundary
  cells to `surfNorm` while next-cell-in is at `originalH`, creating a steep
  triangle face per cell whose grass billboards get stretched vertically by
  Unity's terrain shader?
- **(B)** the mesh-vs-terrain edge mismatch Code hypothesized — water mesh at
  original terrain height floating above depressed shore cells, exposing a
  rim of near-vertical terrain triangles?
- **(C)** something else entirely — e.g. `SetHoles` edge rasterization, the
  underwater-depth floor (`-0.3m`) exposing too much, etc.?

These have different fixes. We need to know which before we spend another three
rounds on a wrong-shaped solution.

### The ablation

Run a single diagnostic import with the shore ramp **disabled**. All of these
changes are trivial and reversible:

1. In `HoleGeoImporter.cs` (near line 19), change:
   ```csharp
   public static int ShoreRadius = 10;
   ```
   to:
   ```csharp
   public static int ShoreRadius = 0;
   ```
   (The shore-ramp block is already guarded by `ShoreRadius > 0` at roughly line
   3480, so setting this to 0 skips the entire ramp pass — no other code changes
   needed.)

2. Re-import Hole 12 Geo.

3. Take a screenshot from approximately the same angle as the previous Hole 12
   screenshot (steep diagonal bank into water).

4. Attach the screenshot to the next message back to the architect. **No other
   analysis or code changes in this task.** Just flip the flag, reimport, screenshot,
   report back.

### What each outcome tells us

- **Serrations gone** → shore ramp was causing them (hypothesis A). Next spec
  will likely remove the ramp entirely for steep banks and keep a narrow
  blend only for gentle ones (Code's Option C).
- **Serrations remain, mesh edge now visibly misaligned** → both A and B are
  partial causes. Next spec will reorder (Option A) AND restrict ramp scope.
- **Serrations remain, mesh edge looks fine** → hypothesis C, re-investigate.
  Possibilities include `SetHoles` rasterization, fairway/tee depression at
  shore (cart-path-style flat drop is probably irrelevant since waterMask
  excludes these cells, but worth confirming), or the terrain shader's
  grass billboard LOD behavior on the per-cell terrain slope itself.

### Do NOT

- Re-attempt any of the three Phase 2 approaches.
- Modify `DepressTerrainUnderOverlays`, `CreateWaterMeshes`, or any other
  method in this task.
- Revert the Phase-1 sampling script or the constants that were added for
  Phase 2 (`ShoreMaxRampSlope`, `ShoreMaxRadiusMeters`). Those are harmless
  when `ShoreRadius = 0` because the entire ramp block is skipped.
- Worry about the fact that setting `ShoreRadius = 0` also disables the
  shore on gentler banks (Holes 7, 13). This is a diagnostic — we'll restore
  correct behavior in the follow-up spec.

### After reporting back

Set `ShoreRadius` back to `10` (or revert the change) so no one else gets
confused by a half-configured state. We can always set it back to 0 in the
follow-up spec if that's what the ablation indicated.

---

## Completed Task — Water Shore Adaptive Radius — Phase 1 (Sampling Script)

Hole 12 shows the same serrated-grass artifact on a steep diagonal water
bank that the tee skirt had before its adaptive-radius fix. Cause:
fixed `ShoreRadius = 10 cells ≈ 5m` in `DepressTerrainUnderOverlays`
compresses big drops into a steep ramp face → Unity's terrain shader
stretches grass vertically per-triangle → reads as serrations.

The fix is a direct port of the tee adaptive radius (`dR = clamp(1.5 ×
dropAbs / MaxRampSlope, base, cap)`). But before applying it, we need
data: how big is the worst drop, and what cap (`ShoreMaxRadiusMeters`)
should the spec use? **Phase 1 = sampling script. Phase 2 (apply the
spec) lands in a follow-up TellCode block, after Cesar reviews the
numbers.**

**Target file (new):** `Tools/sample-shore-heights.js`
**No changes** to `HoleGeoImporter.cs`, the UHoleGeo pipeline, or any
exported JSON.

---

### Reference

Fork the existing `Tools/sample-tee-heights.js` — same height-sampling
machinery (uint16be `.raw`, world↔cell math, point-in-polygon). The
shore version differs in three ways:

1. Reads `water.json` instead of `zone-contours.json` → tee bodies.
2. Computes `nearSurfY = minTerrainH_inside_polygon - 0.05f` (the
   formula `DepressTerrainUnderOverlays` uses for the water surface
   level) instead of tee max height.
3. Samples *outside* the polygon at increasing offsets (1m, 2m, 5m,
   10m), to characterise drop-as-function-of-distance — that's what
   determines whether a 5m fixed radius is too short.

---

### Step 1 — Iterate all holes with water

Don't hardcode Hole 12. Walk the export tree and find every hole with
a `water.json`:

```javascript
const exportRoot = 'C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/export';
const holesWithWater = [];
for (let n = 1; n <= 18; n++) {
  const pad = String(n).padStart(2, '0');
  const wpath = `${exportRoot}/hole-${pad}/water.json`;
  if (fs.existsSync(wpath)) holesWithWater.push(n);
}
```

For each hole, load:
- `Tools/UHoleGeo/output/lomond-country-club/holes/NN/heightmap.raw`
  (pad to 2 digits, no leading zero stripped).
- `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/water.json`.

---

### Step 2 — Per-hole terrain dimensions

`sample-tee-heights.js` hardcodes `terrainWidthM = 151.6` /
`terrainLengthM = 127.2` / `elevRangeM = 34.9` for Hole 04. These vary
per hole. Read them from the hole's per-hole metadata.

**NOTE for Code:** I don't know the exact filename / path that holds
per-hole terrain dimensions in this project. Look for:

- `Tools/UHoleGeo/output/lomond-country-club/holes/NN/*.json` (any
  metadata sibling of `heightmap.raw` with `terrain_width_m` /
  `terrain_length_m` / `elev_range_m`-ish fields).
- Failing that, `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/`
  for a `terrain-meta.json`, `hole-meta.json`, or similar.
- Last resort: grep `generate-terrain.mjs` and `export-hole.mjs` for
  where these dimensions are written. The values must already be
  serialised somewhere because `HoleGeoImporter.cs` consumes them.

If absolutely no per-hole metadata exists, fall back to Hole 04's
constants and add a `// TODO: read per-hole terrain dims` comment plus
a script-top warning. Don't silently use the wrong numbers.

---

### Step 3 — Per-water-body drop characterisation

For each water body (`water.json` → `water[i].contour`):

1. Compute `minTerrainH` over all cells **inside** the polygon (point-
   in-polygon over bbox, same pattern as the tee script's interior
   sweep). This mirrors the importer's `nearSurfY` formula:
   `nearSurfY = minTerrainH - 0.05f`.

2. Walk the contour vertices. For each vertex, sample the heightmap
   at four offsets along the **outward normal**:
   `[1m, 2m, 5m, 10m]`. Outward normal at vertex `i` ≈ rotate the
   edge `(p[i+1] - p[i-1])` by 90° CCW, then check sign by testing
   if `vertex + 0.5m × normal` is outside the polygon (flip if not).
   Skip vertex if the sample lands outside the heightmap.

3. For each sampled point, record `drop = h(sample) - nearSurfY`
   (positive = bank rises above water surface, which is the case
   we care about; negative = sample is already below water level,
   discard).

4. Per water body, report:
   - `nearSurfY` (m)
   - Number of contour vertices, number sampled
   - At each offset (1m, 2m, 5m, 10m): min, median, p90, max drop
   - **Adaptive radius needed** at the p90 drop:
     `dR_needed_m = 1.5 × drop_p90 / 0.35` (using the tee fix's 0.35
     `MaxRampSlope`). This is the headline number — it tells us what
     `ShoreMaxRadiusMeters` cap the Phase 2 spec should use.

5. Per hole summary: max drop across all bodies, max `dR_needed`.

6. Course-wide summary at the end: max drop, max `dR_needed`, list of
   `(hole, body_id, drop, dR_needed)` for the top 5 worst spots.

---

### Step 4 — Output format

Console output, plain text, sectioned by hole. Example shape:

```
=== Hole 7 ===
Terrain: 151.6m × 127.2m, elev range 34.9m

  Water body 1 (12,453 px, 78 contour verts):
    nearSurfY = 4.32m
    Outward sampling (78 verts, 76 sampled):
      offset  min     median  p90     max
       1m    -0.10    0.45    1.20    2.10
       2m     0.05    0.92    2.45    3.80
       5m     0.40    1.85    4.10    5.95
      10m    -0.20    2.30    5.20    7.40
    Adaptive radius needed at 5m-offset p90 drop (4.10m):
      dR_needed = 1.5 × 4.10 / 0.35 = 17.6m  ← cap recommendation

=== Hole 12 ===
...

=== COURSE SUMMARY ===
Holes with water: [7, 12, ...]
Max drop course-wide: 7.4m (Hole 7, body 1)
Max dR_needed:        31.7m (Hole 7, body 1)

Top 5 worst spots:
  Hole  7, body 1: drop 7.40m, dR_needed 31.7m
  Hole 12, body 1: drop 5.80m, dR_needed 24.9m
  ...

→ Recommended ShoreMaxRadiusMeters cap for Phase 2 spec: 35m
  (max dR_needed × 1.1 safety margin, rounded up to 5m)
```

The "recommended cap" line at the end is what Phase 2 will read.

---

### Step 5 — Run it

```
node Tools/sample-shore-heights.js
```

Paste the full console output back. Cesar will eyeball it against
the screenshot evidence and the existing `NEXT_SESSION_WATER_SHORE.md`
heuristics:

- **Max drop < 1m course-wide** → skip Phase 2, fixed 5m is fine,
  Hole 12 artifact is something else (re-investigate).
- **Max drop 2–5m** → apply Phase 2 with cap = max `dR_needed` × 1.1.
- **Max drop > 5m** → apply Phase 2, cap as above; this is the case
  Hole 12 likely is.

---

### Verification

- [x] Script runs to completion on all holes with water (no crashes
      on holes without water).
- [x] Hole 12 appears in the report with non-zero drop values
      (matches the screenshot evidence — the steep bank is real).
- [x] Per-hole terrain dimensions are read from real metadata, not
      hardcoded — confirm the dims for at least one non-Hole-04 hole
      look correct (e.g. compare against the Unity terrain in-scene).
- [x] Course summary's recommended cap is a number Cesar can drop
      directly into the Phase 2 spec.

### Do NOT change

- `HoleGeoImporter.cs` — Phase 2 only.
- Any pipeline script (`generate-terrain.mjs`, `export-hole.mjs`,
  `classify-zones.mjs`, `dev-server.mjs`).
- The existing `Tools/sample-tee-heights.js` — fork, don't refactor.

### Out of scope (Phase 2)

- The actual fix in `DepressTerrainUnderOverlays`. Spec is staged in
  `Docs/NEXT_SESSION_WATER_SHORE.md`; Phase 2 TellCode block lands
  after sampling output is reviewed.

---

## Previous Task — Bridge Viewer in UHoleGeo (consume bridges.json)

The Unity side now writes `bridges.json` into each hole's UHoleGeo
export folder (`Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/bridges.json`).
This task adds the UHoleGeo-side viewer so Cesar can paint the cart-path
zone right up to a bridge's anchor endpoints with pixel-accurate visual
feedback — no more screenshot guesswork.

**Target files:**
- `Tools/UHoleGeo/scripts/dev-server.mjs` (add one GET route)
- `Tools/UHoleGeo/app/index.html` (one toggle button in the layer bar)
- `Tools/UHoleGeo/app/app.js` (load + draw + hover + toggle)

**No changes to:** `bridges.json` schema, UHoleGeo export pipeline,
cart-path processing, the Unity `BridgeExporter` or `BridgeAnchor`,
`classify-zones.mjs`, `generate-terrain.mjs`, or `export-hole.mjs`.

This is a **viewer only**. Bridges are authored in Unity and are
read-only in UHoleGeo. Dragging a bridge in UHoleGeo would desync from
Unity — the whole point is that Unity is the source of truth.

---

### Why viewer, not editor

UHoleGeo paints cart paths as a **pixel mask** (`cartPathMask`), not a
spline. "Snap spline endpoint to bridge" is not a thing here. What the
artist actually needs is to **see** the bridge footprint and its two
anchor endpoints on the canvas while painting, so the cart-path mask
can be brushed to meet the anchors cleanly. That's all this task does.

---

### Data flow (already established)

```
Unity scene                                     UHoleGeo canvas
─────────                                       ───────────────
BridgeAnchor component ─► BridgeExporter ─► bridges.json
                                              │
                                              ▼
                              Tools/UHoleGeo/output/{course}/
                                export/hole-XX/bridges.json
                                              │
                                              ▼
                              (this task)  GET /api/bridges
                                              │
                                              ▼
                                       drawCanvas() → visible markers
```

---

### Step 1 — Add `/api/bridges` route in `dev-server.mjs`

Find the `/api/hole-bounds` handler (around line 138). Insert a new
handler immediately after it (before the `/api/fetch-satellite`
handler). The route reads `bridges.json` from the hole's `export/`
folder, not its `holes/` folder — that's where Unity writes it.

```javascript
// --- API: Get bridges (written by Unity BridgeExporter) ---
if (req.method === "GET" && url.pathname === "/api/bridges") {
  const courseId = url.searchParams.get("course") || "lomond-country-club";
  const hole = Number(url.searchParams.get("hole"));
  const pad = String(hole).padStart(2, "0");
  const bridgesPath = path.join(
    root, "output", courseId, "export", `hole-${pad}`, "bridges.json");

  try {
    const data = await readFile(bridgesPath, "utf8");
    sendJson(res, 200, JSON.parse(data));
  } catch {
    // 404 is expected for holes without bridges — not an error
    sendJson(res, 404, { ok: false, message: "bridges.json not found" });
  }
  return;
}
```

That's the entire server-side change. GET only — UHoleGeo never writes
bridges (Unity is authoritative).

Also add bridge loading to `loadCourseData()` so the course payload
carries bridge metadata. Find the per-hole loop inside
`loadCourseData` (the `for (let i = 1; i <= 18; i++)` block, around
line 90). Alongside the existing `try { hole.anchors = ... }` line,
add:

```javascript
try {
  hole.bridges = JSON.parse(
    await readFile(path.join(exportDir, "bridges.json"), "utf8"));
} catch {}
```

Making bridges available in the initial `/api/course` response lets
the hole-nav indicator show which holes have bridges (minor visual
nice-to-have, see Step 4).

---

### Step 2 — Add a "Bridges" toggle button in `index.html`

Find the layer-bar visibility toggles in `app/index.html` (they're
generated in `buildLayerBar()` in app.js; the toolbar itself is in
index.html). Actually the toggle buttons are created dynamically in
`buildLayerBar()` in app.js — no HTML change needed for the button.
**Skip to Step 3; index.html is unchanged.**

---

### Step 3 — Load, draw, hover, and toggle in `app.js`

All of the following changes are in `Tools/UHoleGeo/app/app.js`.

#### 3.1 — New state variables

Add alongside the existing `let showTrees = true;`,
`let showOB = true;`, `let showCartPath = true;` block (around line
30):

```javascript
let bridges = null;         // [{ id, x, y, z, yaw_deg,
                            //    length_forward_m, length_backward_m,
                            //    expected_path_width_m,
                            //    anchor_forward: {x, z},
                            //    anchor_backward: {x, z} }, ...]
let showBridges = true;
let hoveredBridgeIdx = -1;
```

#### 3.2 — Fetch bridges on hole select

In `selectHole(n)`, alongside the existing `await loadZoneGrid(n);`
call, add:

```javascript
await loadBridges(n);
```

New helper next to `loadZoneGrid`:

```javascript
async function loadBridges(holeNumber) {
  try {
    const res = await fetch(
      "/api/bridges?course=" + COURSE_ID + "&hole=" + holeNumber);
    if (res.ok) {
      const data = await res.json();
      bridges = data.bridges || [];
    } else {
      bridges = [];
    }
  } catch {
    bridges = [];
  }
}
```

Bridges that fail to load (404 or missing file) become an empty array,
so the draw code below is safe for holes without bridges.

#### 3.3 — World-meters → canvas coordinates

UHoleGeo stores everything in **normalized [0, 1] canvas coords**. The
bridge file has **Unity world meters**. Convert:

```javascript
// World meters (Unity frame) → normalized canvas coords [0, 1].
// Uses the same pixel-per-meter ratio as placeTees(): the satellite
// image's (0, 0) maps to the terrain's (-width/2, -length/2) corner
// in Unity (terrain is centered on world origin in HoleGeoImporter).
// The mapping is therefore:
//     px = (worldX + terrainWidth/2) / terrainWidth
//     py = (worldZ + terrainLength/2) / terrainLength
// and finally flipped on Y because UHoleGeo canvas Y=0 is north
// (matches the PNG top-down) while Unity +Z is also north, so we
// invert: py = 1 - py.
function worldToNormalized(worldX, worldZ) {
  const tm = currentHole?.terrainMeta;
  if (!tm) return null;
  const tw = tm.terrain_width_m;
  const tl = tm.terrain_length_m;
  const nx = (worldX + tw / 2) / tw;
  const ny = 1 - (worldZ + tl / 2) / tl;
  return { x: nx, y: ny };
}
```

**Verification note for Code:** compare this transform against how
`cart-paths.json` coordinates align with the zone grid inside
`export-hole.mjs`. If `cart-paths.json` contour points look flipped
in the viewer, the Z-inversion in the formula above is the first
place to adjust — drop the `1 -` prefix and retest. Hole 07 Geo is
the best test case because it has both a cart path and a natural
bridge location.

#### 3.4 — Draw bridges in `drawCanvas()`

At the end of `drawCanvas()`, just before `ctx.restore();` (after the
tee-marker drawing block, before the final `ctx.restore()`), add:

```javascript
// Bridge markers — read-only, authored in Unity.
// Footprint rect is drawn rotated by yaw_deg, then anchor endpoints
// as small circles. Hovered bridge gets a thicker outline.
if (showBridges && bridges && bridges.length > 0) {
  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;
  const tm = currentHole?.terrainMeta;
  if (srcW && srcH && tm) {
    const mppX = tm.terrain_width_m / srcW;
    const mppY = tm.terrain_length_m / srcH;

    for (let bi = 0; bi < bridges.length; bi++) {
      const b = bridges[bi];
      const center = worldToNormalized(b.x, b.z);
      const fA = worldToNormalized(b.anchor_forward.x, b.anchor_forward.z);
      const bA = worldToNormalized(b.anchor_backward.x, b.anchor_backward.z);
      if (!center || !fA || !bA) continue;

      const cx = (center.x - 0.5) * srcW * drawScale;
      const cy = (center.y - 0.5) * srcH * drawScale;
      const fAx = (fA.x - 0.5) * srcW * drawScale;
      const fAy = (fA.y - 0.5) * srcH * drawScale;
      const bAx = (bA.x - 0.5) * srcW * drawScale;
      const bAy = (bA.y - 0.5) * srcH * drawScale;

      // Footprint rect: length along Z axis = length_forward + length_backward,
      // width across X = expected_path_width_m. Convert to canvas pixels
      // via the avg m/px ratio.
      const mpp = (mppX + mppY) / 2;
      const lenPx = (b.length_forward_m + b.length_backward_m) / mpp * drawScale;
      const widPx = (b.expected_path_width_m || 2.5) / mpp * drawScale;

      const isHover = bi === hoveredBridgeIdx;
      const stroke = "#c77dff";  // light purple, high contrast on satellite
      const fill   = isHover ? "rgba(199,125,255,0.32)"
                             : "rgba(199,125,255,0.18)";

      // yaw_deg is +Y CW rotation in Unity (left-handed Y-up). Canvas
      // Y grows downward, so the effective rotation in canvas space is
      // the SAME yaw_deg (both systems treat +CW around the vertical
      // axis identically when viewed top-down). Rotate around center.
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(b.yaw_deg * Math.PI / 180);
      ctx.fillStyle = fill;
      ctx.strokeStyle = stroke;
      ctx.lineWidth = isHover ? 2.5 : 1.5;
      ctx.beginPath();
      ctx.rect(-widPx / 2, -lenPx / 2, widPx, lenPx);
      ctx.fill();
      ctx.stroke();
      // Forward-direction tick mark (short line from center toward +Z in
      // local frame; helps disambiguate which end is "forward")
      ctx.strokeStyle = "#ffffff";
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(0, 0);
      ctx.lineTo(0, -lenPx / 2 * 0.9);
      ctx.stroke();
      ctx.restore();

      // Anchor endpoints (NOT rotated — already in world space)
      for (const [ax, ay, label] of [[fAx, fAy, "F"], [bAx, bAy, "B"]]) {
        ctx.beginPath();
        ctx.arc(ax, ay, isHover ? 6 : 4, 0, Math.PI * 2);
        ctx.fillStyle = stroke;
        ctx.fill();
        ctx.strokeStyle = "#000";
        ctx.lineWidth = 1.2;
        ctx.stroke();
        if (isHover) {
          ctx.fillStyle = "#000";
          ctx.font = "bold 8px sans-serif";
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
          ctx.fillText(label, ax, ay);
        }
      }
    }
  }
}
```

#### 3.5 — Hit-test + tooltip

Add a `hitTestBridge` alongside the existing `hitTestTee`:

```javascript
function hitTestBridge(canvasX, canvasY) {
  if (!showBridges || !bridges || bridges.length === 0) return -1;
  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;
  const tm = currentHole?.terrainMeta;
  if (!srcW || !srcH || !tm) return -1;

  const hitRadius = 14;

  for (let i = 0; i < bridges.length; i++) {
    const b = bridges[i];
    const center = worldToNormalized(b.x, b.z);
    if (!center) continue;

    let imgX = (center.x - 0.5) * srcW * drawScale;
    let imgY = (center.y - 0.5) * srcH * drawScale;

    if (canvasRotation !== 0) {
      const rad = canvasRotation * Math.PI / 180;
      const c = Math.cos(rad), s = Math.sin(rad);
      const rx = imgX * c - imgY * s;
      const ry = imgX * s + imgY * c;
      imgX = rx; imgY = ry;
    }

    const cx = imgX * zoomLevel + canvas.width / 2 + panX;
    const cy = imgY * zoomLevel + canvas.height / 2 + panY;

    const dx = canvasX - cx, dy = canvasY - cy;
    if (dx * dx + dy * dy <= hitRadius * hitRadius) return i;
  }
  return -1;
}
```

In the existing `mousemove` handler (where `hitTestTee` is called for
hover cursor updates), also check bridges so the hovered marker
re-renders with its thicker outline. Add right after the
`const teeIdx = hitTestTee(x, y);` line:

```javascript
const bridgeIdx = hitTestBridge(x, y);
if (bridgeIdx !== hoveredBridgeIdx) {
  hoveredBridgeIdx = bridgeIdx;
  drawCanvas();
}
updateBridgeTooltip(bridgeIdx, x, y);
```

And a tooltip mirroring `updateTeeTooltip`:

```javascript
function updateBridgeTooltip(idx, x, y) {
  let tooltip = document.getElementById("bridge-tooltip");
  if (idx < 0) { hideBridgeTooltip(); return; }
  const b = bridges[idx];
  if (!tooltip) {
    tooltip = document.createElement("div");
    tooltip.id = "bridge-tooltip";
    tooltip.className = "tee-tooltip"; // reuse existing style
    document.getElementById("canvas-stage").appendChild(tooltip);
  }
  tooltip.innerHTML =
    "<strong>Bridge: " + (b.id || "?") + "</strong><br>" +
    "yaw " + b.yaw_deg.toFixed(1) + "°, width " +
      (b.expected_path_width_m || 2.5).toFixed(1) + "m<br>" +
    "F: (" + b.anchor_forward.x.toFixed(1) + ", " +
             b.anchor_forward.z.toFixed(1) + ")<br>" +
    "B: (" + b.anchor_backward.x.toFixed(1) + ", " +
             b.anchor_backward.z.toFixed(1) + ")";
  tooltip.style.left = (x + 15) + "px";
  tooltip.style.top = (y - 10) + "px";
  tooltip.hidden = false;
}

function hideBridgeTooltip() {
  const t = document.getElementById("bridge-tooltip");
  if (t) t.hidden = true;
}
```

Also call `hideBridgeTooltip()` alongside the existing
`hideTeeTooltip()` in the canvas `mouseleave` handler.

#### 3.6 — "Bridges" toggle button in the layer bar

In `buildLayerBar()`, find the `<div class="layer-visibility">` block
that generates the Trees / Cart Path / OB toggle buttons. Add a
fourth:

```javascript
'<button id="btn-toggle-bridges" class="is-active-toggle" ' +
  'title="Toggle Bridges visibility">Bridges</button>' +
```

And below, alongside the existing toggle handlers:

```javascript
document.getElementById("btn-toggle-bridges").addEventListener("click", function () {
  showBridges = !showBridges;
  this.classList.toggle("is-active-toggle", showBridges);
  hoveredBridgeIdx = -1;
  hideBridgeTooltip();
  drawCanvas();
});
```

No changes to `LAYER_ZONES` or `filterBrushesByLayer` — bridges aren't
a paintable zone, they're a top-level overlay like the tee markers.

---

### Step 4 — Optional nice-to-have: bridge indicator in hole nav

In `buildHoleNav()`, after the existing `hasBounds` dot, add a small
indicator for holes that have bridges. Find this line:

```javascript
const hasBounds = hole.hasHoleBounds;
```

Right after it, add:

```javascript
const bridgeCount = hole.bridges?.bridges?.length || 0;
```

Then in the `btn.innerHTML` assignment, append a bridge chip after
the par label:

```javascript
btn.innerHTML =
  '<span class="bounds-dot ' + (hasBounds ? 'has-bounds' : 'no-bounds') + '"></span>' +
  "Hole " + hole.number +
  '<span class="par-label">P' + (ch?.par ?? "?") + "</span>" +
  (bridgeCount > 0
    ? '<span class="par-label" style="background:rgba(199,125,255,0.25);' +
      'color:#c77dff">🌉 ' + bridgeCount + '</span>'
    : '');
```

Pure cosmetic — skip if it conflicts with anything in the CSS.

---

### Verification

1. In Unity, place a `BridgeAnchor` on Hole 07 Geo and export. Confirm
   `Tools/UHoleGeo/output/lomond-country-club/export/hole-07/bridges.json`
   exists.
2. Start the UHoleGeo dev server: `node scripts/dev-server.mjs`.
3. Open the app, click Hole 07.
4. Switch to "Overlay" view (so the zone mask is visible).
5. Expect to see:
   - A light-purple rotated rectangle over the bridge location.
   - A short white tick mark pointing "forward" (toward +Z in Unity).
   - Two purple circles with black outlines at `anchor_forward` and
     `anchor_backward`.
6. Hover the bridge rect — outline thickens, circles show "F" and "B"
   labels, tooltip appears with the bridge id, yaw, and both anchor
   world coords.
7. Paint the cart-path zone (zone 8) right up to one of the anchor
   circles. The circle should stay visible over the painted mask.
8. Click the "Bridges" toggle in the layer bar. Markers disappear /
   reappear.
9. Rotate the canvas (Q/E or the rotation buttons). Bridge markers
   rotate with the satellite image.
10. Open Hole 01 (no bridges). No bridge markers. No console errors.
    Toggle button still works.

Coordinate sanity check:
- In Unity, note the bridge's world `(x, z)` from the exporter window.
- In UHoleGeo, open `bridges.json` directly and confirm those values
  match.
- Check the bridge's rendered position on the canvas vs where the
  water + cart path meet on the satellite image. If the marker is
  offset by a consistent amount in one axis, the `worldToNormalized`
  formula needs its Y-flip adjusted (see the verification note in
  Step 3.3).

Regression:
- [ ] Tee markers still drag / draw / tooltip correctly.
- [ ] Cart path painting still works on a hole with a bridge.
- [ ] `Save` still saves zones (bridges are read-only, not touched by
      Save).
- [ ] `Regen Heightmap` still works — `bridges.json` is not read by
      `generate-terrain.mjs` or `export-hole.mjs`.

---

### Do NOT change

- `bridges.json` schema or the Unity exporter (`BridgeAnchor`,
  `BridgeExporter`). Coordinates flow one way only: Unity → UHoleGeo.
- `cart-paths.json` or the cart-path export/vectorization logic in
  `export-hole.mjs`.
- `classify-zones.mjs`, `generate-terrain.mjs`, or any terrain
  pipeline.
- The `LAYER_ZONES` map — bridges aren't a zone, they're an overlay.
- The zone brush, paint modes, undo stack, or smoothing buttons.
- Leaflet / bounds-setting UI.

### Out of scope (future work)

- Editing bridges in UHoleGeo (explicitly rejected — Unity is the
  single source of truth).
- Auto-snapping the cart-path mask to anchor points (could be a
  future "Smooth Cart Path to Anchors" button; not this task).
- Rendering the Unity bridge prefab's mesh (would require asset
  extraction; the rectangle footprint is enough for alignment work).

---

## Completed Task — Bridge Placement Tool (Unity → UHoleGeo export)

Cesar places bridge prefabs by hand in a hole scene. This tool captures
their positions/rotations and exports them as `bridges.json` into the
hole's UHoleGeo export folder. UHoleGeo will later consume that file
so cart-path splines can snap to bridge anchor points instead of
guessing from screenshots.

**Target file (new):** `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs`
**Also new:** `Assets/Scripts/Course/BridgeAnchor.cs`
**No `TreePlacer` or `HoleGeoImporter` changes required.**

---

### Design summary

- EditorWindow: **`Window > Trees > Bridge Exporter`** (put it next to
  the Tree Brush so they live in the same menu cluster).
- Artist drops bridge prefabs anywhere under `HoleRoot` — this tool
  doesn't prescribe WHERE in the hierarchy. Detection is by component,
  see Step 1.
- On "Export Bridges for Current Hole", the tool:
    1. Resolves the hole number + Lite/Geo/Flat flavour from the
       active scene name (same logic `TreePlacer.ImportTreesMenuItem`
       uses).
    2. Finds all `BridgeAnchor` components in the scene.
    3. Writes `bridges.json` to
       `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/`
       (or the corresponding Lite / `-flat` folder), and mirrors to
       the sibling pipeline (Geo↔Lite) if that folder exists.
- No heightmap modifications, no mesh generation, no splatmap touches.
  Pure position export. Bridges render in Unity because the prefab is
  already in the scene; UHoleGeo gets the coordinates separately.

---

### Step 1 — `BridgeAnchor` marker component

Create `Assets/Scripts/Course/BridgeAnchor.cs`:

```csharp
using UnityEngine;

namespace Golfin.Course
{
    /// <summary>
    /// Marks a GameObject as a bridge for the export pipeline.
    /// Attach to the root of a bridge prefab. The exporter captures
    /// world position + yaw rotation + the two anchor endpoints.
    ///
    /// Anchor endpoints are the points where cart paths should meet
    /// the bridge. They're defined as local offsets along the bridge's
    /// local Z axis (forward) from the bridge's pivot.
    /// </summary>
    [DisallowMultipleComponent]
    public class BridgeAnchor : MonoBehaviour
    {
        [Tooltip("Optional bridge id. If empty, exporter auto-assigns 1..N.")]
        public string id = "";

        [Tooltip("Distance from pivot along local +Z to the 'far' anchor (meters).")]
        public float lengthForward = 3f;

        [Tooltip("Distance from pivot along local -Z to the 'near' anchor (meters).")]
        public float lengthBackward = 3f;

        [Tooltip("Path width this bridge expects to meet (meters). " +
                 "Informational — UHoleGeo uses it to sanity-check cart width.")]
        public float expectedPathWidth = 2.5f;

        // Editor gizmo so the artist sees the anchor endpoints in
        // Scene view without needing to open the exporter window.
        private void OnDrawGizmos()
        {
            Vector3 a = transform.position + transform.forward * lengthForward;
            Vector3 b = transform.position - transform.forward * lengthBackward;
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.35f);
            Gizmos.DrawSphere(b, 0.35f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position,
                transform.position + transform.forward * (lengthForward + 1f));
        }
    }
}
```

Lives under `Assets/Scripts/Course/` so it compiles in both editor and
player (same pattern as `SurfaceMarker`).

---

### Step 2 — EditorWindow scaffold

Create `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs`
wrapped in `#if UNITY_EDITOR ... #endif`, namespace
`Golfin.CourseImport`.

```csharp
public class BridgeExporter : EditorWindow
{
    [MenuItem("Window/Trees/Bridge Exporter")]
    public static void ShowWindow()
    {
        var w = GetWindow<BridgeExporter>("Bridges");
        w.minSize = new Vector2(320, 240);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bridge Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        var anchors = FindAnchorsInActiveScene();
        EditorGUILayout.LabelField(
            $"Found {anchors.Count} BridgeAnchor(s) in scene.");

        if (anchors.Count > 0)
        {
            EditorGUILayout.Space();
            foreach (var a in anchors)
            {
                Vector3 p = a.transform.position;
                EditorGUILayout.LabelField(
                    $"  • {(string.IsNullOrEmpty(a.id) ? a.name : a.id)}" +
                    $"  @ ({p.x:F2}, {p.z:F2})  yaw {a.transform.eulerAngles.y:F1}°");
            }
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Add BridgeAnchor to Selected GameObject"))
            AddAnchorToSelected();

        EditorGUILayout.Space();

        GUI.enabled = anchors.Count > 0;
        if (GUILayout.Button("Export Bridges for Current Hole",
                             GUILayout.Height(30)))
            ExportBridgesForCurrentHole(anchors);
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Writes bridges.json to the current hole's UHoleGeo export " +
            "folder (Lite/Geo/Flat auto-detected from scene name). " +
            "UHoleGeo can read this file so cart-path splines snap to " +
            "bridge anchors.",
            MessageType.Info);
    }

    private double lastRepaint;
    private void OnInspectorUpdate()
    {
        if (EditorApplication.timeSinceStartup - lastRepaint > 0.5)
        {
            Repaint();
            lastRepaint = EditorApplication.timeSinceStartup;
        }
    }
}
```

Helper stubs:
- `List<BridgeAnchor> FindAnchorsInActiveScene()`
- `void AddAnchorToSelected()`
- `void ExportBridgesForCurrentHole(List<BridgeAnchor> anchors)`

---

### Step 3 — `FindAnchorsInActiveScene` + `AddAnchorToSelected`

```csharp
private static List<Golfin.Course.BridgeAnchor> FindAnchorsInActiveScene()
{
    var result = new List<Golfin.Course.BridgeAnchor>();
    var activeScene = UnityEditor.SceneManagement.EditorSceneManager
        .GetActiveScene();
    foreach (var root in activeScene.GetRootGameObjects())
        result.AddRange(
            root.GetComponentsInChildren<Golfin.Course.BridgeAnchor>(true));
    return result;
}

private static void AddAnchorToSelected()
{
    var sel = Selection.activeGameObject;
    if (sel == null)
    {
        EditorUtility.DisplayDialog("Add Bridge Anchor",
            "Select a GameObject in the scene first.", "OK");
        return;
    }
    if (sel.GetComponent<Golfin.Course.BridgeAnchor>() != null)
    {
        EditorUtility.DisplayDialog("Add Bridge Anchor",
            "That GameObject already has a BridgeAnchor.", "OK");
        return;
    }
    Undo.AddComponent<Golfin.Course.BridgeAnchor>(sel);
    EditorUtility.SetDirty(sel);
}
```

---

### Step 4 — `ExportBridgesForCurrentHole`

```csharp
[System.Serializable]
private class BridgeDTO
{
    public string id;
    public float x;     // world X, meters
    public float z;     // world Z, meters
    public float y;     // world Y, meters (for reference; UHoleGeo is 2D)
    public float yaw_deg;
    public float length_forward_m;
    public float length_backward_m;
    public float expected_path_width_m;
    public AnchorDTO anchor_forward;
    public AnchorDTO anchor_backward;
}

[System.Serializable]
private class AnchorDTO
{
    public float x;
    public float z;
}

[System.Serializable]
private class BridgesFile
{
    public string schema_version = "1.0.0";
    public int hole_number;
    public string flavour;  // "geo" | "lite" | "geo-flat" | "lite-flat"
    public int bridge_count;
    public BridgeDTO[] bridges;
}

private static void ExportBridgesForCurrentHole(
    List<Golfin.Course.BridgeAnchor> anchors)
{
    var activeScene = UnityEditor.SceneManagement.EditorSceneManager
        .GetActiveScene();
    string sceneName = activeScene.name;
    string scenePath = activeScene.path ?? "";

    bool isGeo = scenePath.IndexOf("_Geo", System.StringComparison.OrdinalIgnoreCase) >= 0
        || sceneName.IndexOf("_Geo", System.StringComparison.OrdinalIgnoreCase) >= 0;
    bool isFlat = scenePath.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0
        || sceneName.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0;

    string baseName = System.Text.RegularExpressions.Regex
        .Replace(sceneName, "(_Geo)?(_Flat)?$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    int holeNumber = -1;
    if (baseName.StartsWith("Hole_") && baseName.Length >= 7)
        int.TryParse(baseName.Substring(5, 2), out holeNumber);

    if (holeNumber < 1 || holeNumber > 18)
    {
        EditorUtility.DisplayDialog("Export Bridges",
            $"Cannot detect hole number from scene '{sceneName}'.\n" +
            "Expected 'Hole_XX', 'Hole_XX_Geo', 'Hole_XX_Flat', " +
            "or 'Hole_XX_Geo_Flat'.", "OK");
        return;
    }

    string flavour = (isGeo ? "geo" : "lite") + (isFlat ? "-flat" : "");
    string toolFolder = isGeo ? "UHoleGeo" : "UHoleLite";
    string holeFolder = isFlat ? $"hole-{holeNumber:D2}-flat"
                               : $"hole-{holeNumber:D2}";
    string exportPath = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(
            Application.dataPath, "..",
            $"Tools/{toolFolder}/output/lomond-country-club/export",
            holeFolder));

    if (!System.IO.Directory.Exists(exportPath))
    {
        EditorUtility.DisplayDialog("Export Bridges",
            $"Export folder not found:\n{exportPath}\n\n" +
            "Has this hole been exported from UHoleGeo yet?", "OK");
        return;
    }

    var dtos = new BridgeDTO[anchors.Count];
    for (int i = 0; i < anchors.Count; i++)
    {
        var a = anchors[i];
        Vector3 p = a.transform.position;
        Vector3 fwd = a.transform.forward;

        Vector3 anchorF = p + fwd * a.lengthForward;
        Vector3 anchorB = p - fwd * a.lengthBackward;

        dtos[i] = new BridgeDTO
        {
            id = string.IsNullOrEmpty(a.id) ? $"bridge_{i + 1}" : a.id,
            x = p.x, y = p.y, z = p.z,
            yaw_deg = NormalizeYaw(a.transform.eulerAngles.y),
            length_forward_m = a.lengthForward,
            length_backward_m = a.lengthBackward,
            expected_path_width_m = a.expectedPathWidth,
            anchor_forward  = new AnchorDTO { x = anchorF.x, z = anchorF.z },
            anchor_backward = new AnchorDTO { x = anchorB.x, z = anchorB.z },
        };
    }

    var file = new BridgesFile
    {
        hole_number = holeNumber,
        flavour = flavour,
        bridge_count = dtos.Length,
        bridges = dtos,
    };

    string outPath = System.IO.Path.Combine(exportPath, "bridges.json");
    string json = JsonUtility.ToJson(file, true);
    System.IO.File.WriteAllText(outPath, json);

    Debug.Log($"[BridgeExporter] Wrote {dtos.Length} bridge(s) to {outPath}");

    // Mirror to the other pipeline (Geo ↔ Lite) if its folder exists.
    string otherTool = isGeo ? "UHoleLite" : "UHoleGeo";
    string otherExportPath = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(
            Application.dataPath, "..",
            $"Tools/{otherTool}/output/lomond-country-club/export",
            holeFolder));
    if (System.IO.Directory.Exists(otherExportPath))
    {
        string mirrorPath = System.IO.Path.Combine(
            otherExportPath, "bridges.json");
        System.IO.File.WriteAllText(mirrorPath, json);
        Debug.Log($"[BridgeExporter] Mirrored to {mirrorPath}");
    }
}

private static float NormalizeYaw(float yawDeg)
{
    yawDeg = yawDeg % 360f;
    if (yawDeg > 180f) yawDeg -= 360f;
    if (yawDeg < -180f) yawDeg += 360f;
    return yawDeg;
}
```

---

### Step 5 — Example JSON output

```json
{
  "schema_version": "1.0.0",
  "hole_number": 7,
  "flavour": "geo",
  "bridge_count": 1,
  "bridges": [
    {
      "id": "bridge_1",
      "x": -184.30,
      "y": 2.45,
      "z": 72.10,
      "yaw_deg": 38.5,
      "length_forward_m": 3.0,
      "length_backward_m": 3.0,
      "expected_path_width_m": 2.5,
      "anchor_forward":  { "x": -182.43, "z": 74.45 },
      "anchor_backward": { "x": -186.17, "z": 69.75 }
    }
  ]
}
```

**Coordinate convention (important for UHoleGeo consumption):**
`x`/`z` are Unity world meters, matching `cart-paths.json`'s
`contour[i].x`/`.z` exactly. UHoleGeo can treat `anchor_forward` /
`anchor_backward` as snap targets for spline endpoints directly — no
coordinate transformation required. `y` is included for future 3D
routing but can be ignored by the current 2D path logic.

---

### Verification

1. Open `Hole_07_Geo`. Drop a bridge prefab over the stream.
2. `Window > Trees > Bridge Exporter` → window shows "Found 0
   BridgeAnchor(s)".
3. Select the bridge GameObject → click "Add BridgeAnchor to Selected
   GameObject". Window now shows "Found 1" with its position.
4. Yellow gizmo line runs through the bridge with spheres at the two
   anchor endpoints. Rotate/move the bridge — gizmo tracks.
5. Click "Export Bridges for Current Hole". Console logs:
   - `[BridgeExporter] Wrote 1 bridge(s) to .../hole-07/bridges.json`
   - `[BridgeExporter] Mirrored to .../UHoleLite/.../hole-07/bridges.json`
6. Open the written `bridges.json` — coordinates match the bridge's
   Unity world position, yaw matches Y rotation, anchor endpoints are
   offset along the bridge's local forward.

Regression:
- [ ] `Hole_01_Geo` (no bridges): window shows "Found 0", export button
      disabled, no crash.
- [ ] Rename a scene to `Test_Scene`: export shows a clear dialog, no
      crash.
- [ ] `Hole_07_Geo_Flat`: export lands in `hole-07-flat/bridges.json`
      and mirrors to the Lite flat folder if it exists.

---

### Out of scope (future work, not this task)

- UHoleGeo reading `bridges.json` and routing splines to anchors —
  separate JS-side task when Cesar tackles the UHoleGeo tool.
- Bridge prefab authoring (width variants, material sets, LODs).
- Physics colliders / ball bounce behaviour on bridges.
- Runtime bridge loading for gameplay.

---

### Do NOT change

- `TreePlacer.cs`, `HoleGeoImporter.cs`, `HoleLiteImporter.cs`.
- `cart-paths.json` schema — bridges live in a separate file.
- Any scene hierarchy conventions beyond adding `BridgeAnchor`
  components. Bridges can live anywhere under `HoleRoot` (or even at
  scene root — detection is by component, not by name).

---

## Previous Task — Fix Tee Border Ring Texture Twisting (Constant V)

The inset tee border ring is in place and orientation is correct (light
toward tee surface, dark toward terrain). But the texture shows
distortion/twisting at points along the ring's curve.

**Cause:** In `CreateTeeMeshWithInsetBorder`, the border vert
duplication assigns `v = (src.x + src.z) / borderTileSize`. That's a
world-XZ projection, which jumps around as the ring curves. For a
texture with meaningful V-direction content, that would tile badly on
a closed ring.

**But the texture has no meaningful V content.** `T_TeeDark_Albedo` is
a left-to-right color gradient (green → uniform green → rough-darker)
with only mild noise. V variation is purely decorative. Setting V to a
constant eliminates the twisting without visibly losing anything.

### The change

In `CreateTeeMeshWithInsetBorder` (the mesh builder added in the last
task), in the border vert duplication block, find:

```csharp
float u = 1f - Mathf.Clamp01(dist / borderWidth);
float v = (src.x + src.z) / borderTileSize;
```

Replace with:

```csharp
float u = 1f - Mathf.Clamp01(dist / borderWidth);
// T_TeeDark_Albedo has no meaningful V content — it's a pure L→R
// color gradient (tee-green to rough-darker). World-XZ V causes
// visible texture twisting on the ring's curve. Constant V removes
// the twisting; no visual content is lost because V has none to lose.
float v = 0.5f;
```

That's the entire change. `borderTileSize` stays as a function parameter
(still used by other callers / future-me if we ever swap in a texture
with V-direction content).

### Verification

- [ ] Re-import any tee-bearing hole (Hole 4 is fine).
- [ ] Dark border ring still visible, still oriented correctly (light
      toward tee, dark toward terrain).
- [ ] Texture twisting / wavy distortion at the bottom edge is gone.
- [ ] Gradient still clean from the tee-surface edge of the ring to
      the terrain-adjacent edge.

### Do NOT change

- Anything else in `CreateTeeMeshWithInsetBorder`.
- The U calculation.
- The `borderTileSize` parameter or its callsite.
- Any other mesh builder, material, or system.

---

✅ DONE: 2026-04-18 Constant-V UV fix applied. Additionally fixed geometric crease: rebuilt ring as manual quad-strip (outer contour × inset contour vertex pairs by index) instead of CDT-classified triangles — eliminates long diagonal spanning tris. CDT now only triangulates the inset contour for submesh 0; submesh 1 is a clean N-quad strip with winding auto-checked.

✅ DONE: 2026-04-18 Bridge Placement Tool implemented. BridgeAnchor.cs (Golfin.Course) marker component with gizmo. BridgeExporter.cs EditorWindow at Window > Trees > Bridge Exporter — finds anchors, previews positions, exports bridges.json to UHoleGeo/UHoleLite export folder with auto-detection of Geo/Lite/Flat from scene name, mirrors to sibling pipeline folder.

✅ DONE: 2026-04-19 Water Shore Phase 1 sampling script created at Tools/sample-shore-heights.js. Course-wide max drop 14.07m (Hole 12, body 1), max dR_needed 34.7m. Recommended ShoreMaxRadiusMeters cap for Phase 2 spec: 40m. Holes 7 (8.63m) and 13 (6.62m) also need the fix. Per-hole terrain dims read from terrain-meta.json (not hardcoded).

---

## 🔴 Phase 2 Investigation Findings (2026-04-20) — Unresolved

**Three attempts, all reverted. Code is back to pre-Phase-2 state.**

### What was tried

**Attempt 1 — ErodeMask (2 passes, 4-connected) on bodyMask before writing to waterMask.**
Result: teeth became DEEPER. Erosion shrinks the mask inward; cells removed from the boundary fall into the shore ramp zone at distance≈0, which sets them to `nearSurfY` ≈ floor level. Net effect: more dark exposed cells, not fewer.

**Attempt 2 — Fixed unit mismatch in worstAdaptiveShoreM pre-scan.**
`drop = heights[z,x] - surfNorm` is normalized (0–1). `ShoreMaxRampSlope = 0.35f` is in world m/m. Dividing directly gives ~1.2m radius instead of the intended 40m. Fix: `drop * elevRange`. The formula itself was correct conceptually.
Result: **flattened the entire terrain bank around the water body** — a huge bowl was depressed 40m in all directions. Artifact still present. User rejected.

**Attempt 3 — Combined: unit fix + scanBand increased from 12 cells to ceil(ShoreMaxRadiusMeters/cellSize) cells.**
Same result as Attempt 2. The wide ramp is wrong for hillside ponds.

---

### Root cause analysis (not yet confirmed, needs architect review)

**The shore ramp formula `Lerp(surfNorm, originalH, t)` is wrong for steep banks.**
`surfNorm = (minTerrainH_inside_polygon - 0.05) / elevRange` = the water floor level (lowest point of the entire polygon interior). On Hole 12, the polygon boundary on the uphill side is 14m ABOVE `surfNorm`. The ramp drags that 14m-high terrain down toward `surfNorm` over 40m = a massive unnatural bowl. This is the wrong shape for a hillside pond.

The correct `nearSurfY` for a shore ramp cell should be the water surface height **at that cell's closest polygon edge point**, not the global polygon minimum.

**Structural issue: `CreateWaterMeshes` runs at line 234, `DepressTerrainUnderOverlays` runs at line 305.**
The water mesh samples `terrain.SampleHeight()` on the ORIGINAL (undepressed) terrain. After depression, the mesh floats above the depressed floor. The mesh edge is at the original terrain height at polygon vertices. On the steep uphill side, this creates a visible cliff between the water mesh edge (high) and the depressed terrain just outside (set to surfNorm by the ramp). This is likely the direct cause of the teeth.

**Hypothesis:** If `CreateWaterMeshes` were called AFTER `DepressTerrainUnderOverlays`, the water mesh would conform to the already-depressed terrain. The mesh edge would sample the shore-ramped terrain (already at surfNorm at distance=0). The mesh and terrain would be co-planar at the boundary → no cliff → no teeth.

---

### Proposed fix for architect to spec

**Option A (recommended): Reorder pipeline — call `CreateWaterMeshes` AFTER `DepressTerrainUnderOverlays`.**
- Water mesh samples depressed terrain → mesh edge at surfNorm at boundary → seamless join
- No change to the shore ramp formula needed
- Requires verifying no other side effect of the reorder (anchor placement at line 245 says it should run BEFORE depression; bridges/greens/zones all run before 305 too)
- Clean, zero new parameters

**Option B: Fix the shore ramp `nearSurfY` to use per-cell water surface, not global polygon minimum.**
On a sloped water body, surfNorm should be the height of the polygon contour at the nearest edge point, not the minimum over the whole interior. This requires projecting each cell to its nearest polygon edge and sampling the terrain AT that edge point to get the local surface level. More complex, but makes the ramp physically correct.

**Option C: Use a narrow blend only (2–3 cells) at the polygon boundary, not a wide ramp.**
On a steep bank, the right transition is a 1-cell feather, not a 40m ramp. Cap the shore ramp at `min(worstAdaptiveShoreM, 3 cells)` for cells where `originalH - surfNorm > some_threshold`. Terrain away from the immediate boundary is left alone.

---

### Do NOT retry in Code without architect spec
The shore ramp adaptive radius system is architecturally mismatched with hillside water bodies. Any further attempts without a clear re-spec will produce new variants of the same artifacts.

✅ DONE: 2026-04-18 Bridge Viewer in UHoleGeo implemented. dev-server: /api/bridges GET route + bridges loaded into hole nav data. app.js: loadBridges() fetches on hole select; worldToNormalized() converts Unity world meters to canvas coords; drawCanvas() draws purple rotated footprint rect + forward tick + anchor endpoint circles; hitTestBridge() + tooltip on hover; "Bridges" toggle in layer bar; bridge count chip in hole nav.

✅ DONE: 2026-04-20 Phase 2b ablation complete. ShoreRadius=0 result: serrations remain and are worse (individual heightmap cell pillars fully exposed at water boundary). Hypothesis A ELIMINATED — shore ramp is not the cause. Confirms Hypothesis B: abrupt depression cliff at water polygon boundary. Water mesh samples original terrain height → floats above depressed floor → exposed cliff face per cell. Architect recommended fix: Option A (reorder CreateWaterMeshes to run AFTER DepressTerrainUnderOverlays). ShoreRadius restored to 10.
