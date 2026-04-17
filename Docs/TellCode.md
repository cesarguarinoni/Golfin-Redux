# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Median Platform Height + Dual Cut/Fill Skirt

The tee skirt looks perfect on mild terrain but shows small cliffs at
the outer base of the mound on tees placed on steep slopes. Verified by
sampling: Hole 4 Tee 1 has `maxH = 20.71m` but the surrounding baseline
drops as low as **12.78m** within 2m of the contour — a 7.93m drop that
a 2m-wide smoothstep ramp cannot reach. The outermost ring cell sits
~0.8m above the next cell beyond the skirt, creating the cliff you're
seeing. Other tees on the same course are fine because the local drop
is small.

The right architectural fix is what real courses do: **cut tees into
hillsides** rather than only raising them.

### Design

Two-part change in `FlattenTerrainUnderTees`:

1. **Platform Y becomes the MEDIAN of baseline heights inside the tee
   contour** (not the max). This positions the tee surface at the
   "average" elevation of its footprint. On a slope: uphill parts of
   the polygon are higher than the platform, downhill parts lower.
   Real-world tees work exactly this way.

2. **The skirt ramp becomes a dual cut/fill pass:**
   - **Downhill cells** (where `baseline < platformY`): the ramp goes
     from `platformY` at the tee edge DOWN to `baseline` at the skirt
     radius. MAX-merge raises the terrain up to form the downhill
     mound. (Same as today.)
   - **Uphill cells** (where `baseline > platformY`): the ramp goes
     from `platformY` at the tee edge UP to `baseline` at the skirt
     radius. MIN-merge lowers the terrain down to form the uphill
     cut. (This is new — today we skip these cells because of MAX.)

Net effect: tee sits at median height, the surrounding terrain is cut
and filled to meet it in a single smooth skirt ring. Maximum
differential halves (was `maxH - minBaseline`, becomes `platformY -
minBaseline` which on a symmetric slope is half). 2m skirt now has a
much smaller vertical gap to cover, cliffs disappear on most tees.

No mesh changes. `CreateTeeMeshFlat` continues to `max()` its sampled
verts — since terrain under the tee is flat at `platformY`, the max IS
`platformY`. No change needed there.

### Invariants preserved

- Tee polygon interior stays at a single flat height. ✓
- Cells beyond `skirtRadiusM` stay untouched (baseline). ✓
- `DepressTerrainUnderOverlays` still drops tee interior by 0.40m
  beneath the mesh for z-fight clearance. ✓
- `CreateTeeMeshFlat` produces a flat mesh. ✓
- No new allocation. `baseline` clone already exists.

### What about `skipMask`?

Currently empty (Cesar correctly pointed out that Hole 4 tees are
nowhere near a fairway, so skipMask doesn't gate anything there).
It's a no-op. Leave it as-is — don't delete, don't touch.

---

### Implementation

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**Target function:** `FlattenTerrainUnderTees`

There are three small changes to make, all inside the per-region loop.
Find the relevant lines by searching for the comment
`// Peak height from BASELINE`.

#### Change 1: Replace max with median for platform height

Current code (around line 3135):

```csharp
                // Peak height from BASELINE, not the mutating heights array
                float maxH = float.MinValue;
                for (int row = 0; row < hRes; row++)
                    for (int col = 0; col < hRes; col++)
                        if (teeMask[row, col] && baseline[row, col] > maxH)
                            maxH = baseline[row, col];

                if (maxH == float.MinValue) continue;

                // Raise interior to maxH
                for (int row = 0; row < hRes; row++)
                    for (int col = 0; col < hRes; col++)
                        if (teeMask[row, col])
                        {
                            heights[row, col] = maxH;
                            flattenedCount++;
                        }
```

Replace with:

```csharp
                // Platform Y = median of baseline heights inside the contour.
                // Median (not max) means the tee sits at the ~average elevation
                // of its footprint, so we CUT the uphill portion and FILL the
                // downhill portion in the skirt pass below. This is how real
                // golf tees are built on slopes — cut into the hillside — and
                // it keeps the max height differential that the skirt has to
                // bridge much smaller than using the peak elevation.
                var samples = new System.Collections.Generic.List<float>(512);
                for (int row = 0; row < hRes; row++)
                    for (int col = 0; col < hRes; col++)
                        if (teeMask[row, col])
                            samples.Add(baseline[row, col]);

                if (samples.Count == 0) continue;

                samples.Sort();
                float platformY = samples[samples.Count / 2];

                // Flatten interior to platformY
                for (int row = 0; row < hRes; row++)
                    for (int col = 0; col < hRes; col++)
                        if (teeMask[row, col])
                        {
                            heights[row, col] = platformY;
                            flattenedCount++;
                        }
```

#### Change 2: Rename `maxH` → `platformY` in the skirt lerp

Further down in the same loop, around line 3222–3224:

```csharp
                        float t = minDistM / skirtRadiusM;
                        t = t * t * (3f - 2f * t); // smoothstep

                        float rampedH = Mathf.Lerp(maxH, baseline[z, x], t);
```

Change `maxH` to `platformY`:

```csharp
                        float t = minDistM / skirtRadiusM;
                        t = t * t * (3f - 2f * t); // smoothstep

                        float rampedH = Mathf.Lerp(platformY, baseline[z, x], t);
```

#### Change 3: Dual cut-and-fill merge

Immediately below the `rampedH = Mathf.Lerp(...)` line, the current
merge is:

```csharp
                        if (rampedH > heights[z, x])
                        {
                            heights[z, x] = rampedH;
                            skirtedCount++;
                        }
```

Replace with:

```csharp
                        // Dual cut/fill merge:
                        //   Downhill cells (baseline < platformY): the ramp goes
                        //     from platformY at the edge DOWN to baseline at the
                        //     ring edge. MAX raises terrain to form the mound.
                        //   Uphill cells (baseline > platformY): the ramp goes
                        //     from platformY at the edge UP to baseline at the
                        //     ring edge. MIN lowers terrain to form the cut.
                        //
                        // Equivalently: always move heights[z,x] from its current
                        // value (baseline) TOWARD rampedH — that's exactly what
                        // Lerp produces. We can just assign rampedH directly,
                        // because the ramp is always between platformY and
                        // baseline, so it never overshoots in either direction.
                        //
                        // Guard: only write if rampedH differs from current — a
                        // cell that was touched by a previous tee's skirt (with
                        // overlap) keeps whichever write moves heights further
                        // FROM baseline. This avoids two adjacent tees' skirts
                        // fighting and producing a seam.
                        float baselineH = baseline[z, x];
                        bool uphill = baselineH > platformY;

                        if (uphill)
                        {
                            // Cut: new height should be BELOW current (toward platformY)
                            if (rampedH < heights[z, x])
                            {
                                heights[z, x] = rampedH;
                                skirtedCount++;
                            }
                        }
                        else
                        {
                            // Fill: new height should be ABOVE current (toward platformY)
                            if (rampedH > heights[z, x])
                            {
                                heights[z, x] = rampedH;
                                skirtedCount++;
                            }
                        }
```

(The long comment inside is intentional — this is the non-obvious part
of the task. A future reader needs to understand why we're branching.)

#### Change 4 (tiny): update the Debug.Log label

Around line 3235:

```csharp
                Debug.Log($"[HoleGeoImporter] Tee {region.id}: platform h={maxH:F4}, " +
                          $"skirt radius={skirtRadiusCells} cells ({TeeSkirtMeters:F1}m)");
```

Change `maxH` to `platformY`:

```csharp
                Debug.Log($"[HoleGeoImporter] Tee {region.id}: platform h={platformY:F4}, " +
                          $"skirt radius={skirtRadiusCells} cells ({TeeSkirtMeters:F1}m)");
```

That's the entire task. Four small edits in one function.

---

### Verification

Re-import Hole 4:

- [ ] **Tee 1** (big one, on the steep ridge) — the cliffs at the
      base of the mound on the downhill/SW side should be smaller or
      gone. Uphill side now has a cut slope going INTO the terrain
      instead of a flat skirt that couldn't reach.
- [ ] **Tee 2** (small one) — also smoother. The drops were much
      smaller on tee 2, so this should look basically identical to
      before (platformY ≈ median ≈ maxH within a meter).
- [ ] Tee top still flat.
- [ ] Tee mesh still sits flush with terrain at the tee edge (it
      should — interior is flat at platformY, mesh flattens to max of
      sampled verts which is also platformY).

Regression:

- [ ] Hole 1 (3 tees, including the big back tee on a ridge) — check
      all three for smooth mounds.
- [ ] Hole 18 (6 small tees, close together) — the overlap guard in
      Change 3 matters here; check that adjacent tees don't produce
      weird seams between their skirts.
- [ ] Hole 7 — water-adjacent tee, no change expected since tees
      are at their own elevations and skirts don't reach water.
- [ ] Fairways, greens, bunkers, cart paths — unchanged.
- [ ] Debug.Log still reports `platform h=X.XXXX` (new label) and
      reasonable `skirt cells` count (may go up or down depending on
      whether uphill cuts add more eligible cells than the median
      removes).

### Tuning fallbacks (if cliffs persist on the steepest tees)

If you find a tee where the downhill drop is so severe that even
`platformY - minBaseline` exceeds what the 2m skirt can smoothly ramp
to (e.g., Hole 4 Tee 1 might still have ~5m drop on its SW side), do
NOT widen the global `TeeSkirtMeters` — Cesar has explicitly said
bigger mounds look wrong. Instead, flag it and I'll spec an adaptive
per-tee skirt radius as a follow-up.

Expected improvement for Hole 4 Tee 1:
- Before: `maxH = 20.71m`, downhill baseline 12.78m, drop = **7.93m**
- After: `platformY ≈ 18.38m` (median), downhill drop = **5.60m**
- Cliff remaining after 2m smoothstep: was ~0.8m, should be ~0.56m

Still a visible step, but 30% smaller. Combined with the new uphill
cut eliminating the small cliff on THAT side (was ~0m, now properly
smoothed), the overall tee look is much more balanced. If residual
downhill cliff is still bothersome, we'll iterate.

---

✅ DONE: 2026-04-18 — Median platformY, dual cut/fill skirt, maxH → platformY in lerp and Debug.Log. Four edits, one function.

### Do NOT change

- `CreateTeeMeshFlat` — works as-is.
- `skipMask` construction — leave it (no-op but harmless, reusable).
- `baseline` clone — required for the median computation to read
  original terrain.
- `TeeSkirtMeters`, `skirtRadiusCells`, `skirtRadiusM` — geometry
  stays at 2m.
- The exact-distance pass (replaced chamfer days ago — that's still
  the right call).
- `DepressTerrainUnderOverlays` — still drops tee interior 0.40m.
- `MAX`/`MIN` rename in variable names — we're keeping `maxH` local
  name for nothing, it becomes `platformY`. Don't globally search/
  replace `maxH` — it may collide with unrelated code; only change the
  specific lines shown above.

---
