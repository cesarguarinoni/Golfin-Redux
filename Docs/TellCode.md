# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Per-Cell Adaptive Skirt Radius

### Background — what we learned

Tee 1 on Hole 4 sits on a ridge. On its SW side, the natural DEM drops
**7.93 m over 2 m horizontal** (a 76° natural slope, compressed by the
2 m skirt into rendering as a vertical cliff face — that's what the
"serrated" band was: Unity's terrain shader stretching grass vertically
on a 76° triangle face).

The median-platform attempt reduced that to 5.6 m drop but broke the
Tee 1 ↔ Tee 2 adjacency relationship (they're 4.4 m apart, and giving
them different platform heights created a new cliff between them).

**The real issue is that a fixed 2 m skirt can't handle steep baselines.**
The fix is a **per-cell adaptive skirt radius** that extends exactly as
far as needed to keep the ramp slope walkable (~19°), regardless of
local terrain steepness. On gentle sides the skirt stays at 2 m
(unchanged). On steep sides it extends out to 20–30 m with a very
shallow lift (at most ~1 m above baseline) — invisible cosmetically,
but enough to blend the tee platform into the natural hillside without
a cliff.

Sampled data (Tee 1 SW direction) confirms the adaptive skirt adds at
most ~1.2 m of lift anywhere in its extended region, tapering to
essentially zero at its outer edge. The "mound" does not get visually
bigger — we just grade-smooth the first ~10–25 m of the natural
hillside so the tee merges in without a rendering cliff.

**The platform height stays `maxH`** (revert the median change). This
preserves the Tee 1 / Tee 2 relationship that was working correctly
before.

### Target file
`Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`

### Target function
`FlattenTerrainUnderTees`

### Do NOT change
- `CreateTeeMeshFlat`
- `DepressTerrainUnderOverlays`
- Anything else in the file.
- `TeeSkirtMeters` (= 2.0f) — keep as the minimum/base skirt size.
- The `skipMask` construction (fairway / green exclusions).
- The `baseline` array clone.
- The coarse chamfer distance transform (forward + backward passes).
- The exact-distance pass that computes `minDistM`.
- The platform-raise loop (restore `maxH` computation — see below).
- MAX-merge semantics.

---

### Step 1 — Revert platform height to `maxH`

In the per-region loop, the median-platform attempt is still in place.
Revert it. Find the block starting with `// Platform Y = median of
baseline heights` (around line 3135) and replace with the original
`maxH` version:

```csharp
                // Platform height = maxH of baseline heights inside
                // the tee contour. Using max (not median) keeps
                // adjacent tees at their natural relative heights —
                // Tee 1 & Tee 2 on Hole 4 are 4.4m apart and must
                // behave as one continuous tee complex.
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

---

### Step 2 — Add two new constants at the top of the class

Near the existing `TeeSkirtMeters = 2.0f` declaration (around line 43):

```csharp
/// <summary>Maximum ramp slope (rise/run) on the tee skirt.
/// 0.35 ≈ 19°, a walkable golf mound slope. When natural
/// terrain is steeper than this, the skirt extends outward
/// per-cell so the rendered ramp never exceeds this slope.</summary>
private const float TeeMaxRampSlope = 0.35f;

/// <summary>Upper cap on per-cell adaptive skirt radius.
/// Terrain drops beyond this aren't smoothed — we accept the
/// natural cliff, since extending further would modify too
/// much of the course layout. Set generously so normal
/// terrain never hits it; present only to prevent pathological
/// cases from blowing up the skirt across the whole hole.</summary>
private const float TeeMaxSkirtMeters = 60.0f;
```

(The `TeeMaxSkirtMeters = 60` cap is just a safety valve —
real-world worst case for Hole 4 Tee 1 is ~30 m. It should
essentially never be hit; it's there so a buggy contour can't
cause the skirt pass to extend across the whole hole.)

---

### Step 3 — Widen the coarse-cull distance

The current coarse-cull (`if (coarseDist[z, x] > skirtRadiusCells + 2f)
continue;` around line 3197) assumes skirt radius = `TeeSkirtMeters`.
With adaptive radius, the effective skirt in extreme directions is much
wider. Compute the worst-case skirt radius per-tee so the cull doesn't
reject cells we'd otherwise write.

Just above the exact-distance pass loop (before `// Exact-distance
pass.` around line 3189), add:

```csharp
                // Worst-case skirt radius for the coarse cull: drop
                // from maxH to the lowest baseline in a neighborhood
                // around this tee, divided by the maximum ramp
                // slope (× 1.5 = smoothstep's peak slope multiplier).
                float worstDrop = 0f;
                int neighborhoodCells = Mathf.RoundToInt(
                    TeeMaxSkirtMeters / metersPerCell);
                // Scan a bbox around the tee's mask cells.
                int minR = hRes, maxR = -1, minC = hRes, maxC = -1;
                for (int z = 0; z < hRes; z++)
                    for (int x = 0; x < hRes; x++)
                        if (teeMask[z, x])
                        {
                            if (z < minR) minR = z;
                            if (z > maxR) maxR = z;
                            if (x < minC) minC = x;
                            if (x > maxC) maxC = x;
                        }
                int bboxMinR = Mathf.Max(0, minR - neighborhoodCells);
                int bboxMaxR = Mathf.Min(hRes - 1, maxR + neighborhoodCells);
                int bboxMinC = Mathf.Max(0, minC - neighborhoodCells);
                int bboxMaxC = Mathf.Min(hRes - 1, maxC + neighborhoodCells);
                for (int z = bboxMinR; z <= bboxMaxR; z++)
                    for (int x = bboxMinC; x <= bboxMaxC; x++)
                    {
                        float drop = maxH - baseline[z, x];
                        if (drop > worstDrop) worstDrop = drop;
                    }

                // Worst-case adaptive radius in meters (with cap).
                float worstAdaptiveM = Mathf.Min(TeeMaxSkirtMeters,
                    Mathf.Max(TeeSkirtMeters,
                        1.5f * worstDrop / TeeMaxRampSlope));
                int worstAdaptiveCells = Mathf.CeilToInt(
                    worstAdaptiveM / metersPerCell);
```

Then, in the existing exact-distance pass, change the coarse cull:

```csharp
                        // OLD:
                        // if (coarseDist[z, x] > skirtRadiusCells + 2f) continue;
                        // NEW:
                        if (coarseDist[z, x] > worstAdaptiveCells + 2f) continue;
```

---

### Step 4 — Replace fixed `skirtRadiusM` with per-cell adaptive radius

Inside the exact-distance pass, find the lerp section (around line
3221):

```csharp
                        float t = minDistM / skirtRadiusM;
                        t = t * t * (3f - 2f * t); // smoothstep

                        float rampedH = Mathf.Lerp(maxH, baseline[z, x], t);

                        if (rampedH > heights[z, x])
                        {
                            heights[z, x] = rampedH;
                            skirtedCount++;
                        }
```

Replace with:

```csharp
                        // Per-cell adaptive skirt radius.
                        //
                        // dR is chosen so the ramp's *peak* slope
                        // (which occurs at t=0.5 for smoothstep, where
                        // smoothstep' = 1.5) stays at or below
                        // TeeMaxRampSlope. Solving:
                        //   dropAbs × 1.5 / dR ≤ TeeMaxRampSlope
                        //   dR ≥ 1.5 × dropAbs / TeeMaxRampSlope
                        //
                        // For gentle terrain (small drop) this is
                        // smaller than TeeSkirtMeters, so we clamp to
                        // TeeSkirtMeters as the base/minimum skirt
                        // size (unchanged from today on flat ground).
                        //
                        // TeeMaxSkirtMeters is a safety cap; a
                        // pathological polygon can't produce a skirt
                        // spanning the whole hole.
                        float dropAbs = Mathf.Abs(maxH - baseline[z, x]);
                        float adaptiveM = Mathf.Clamp(
                            1.5f * dropAbs / TeeMaxRampSlope,
                            TeeSkirtMeters,
                            TeeMaxSkirtMeters);

                        if (minDistM > adaptiveM) continue; // fine cull

                        float t = minDistM / adaptiveM;
                        t = t * t * (3f - 2f * t); // smoothstep

                        float rampedH = Mathf.Lerp(maxH, baseline[z, x], t);

                        if (rampedH > heights[z, x])
                        {
                            heights[z, x] = rampedH;
                            skirtedCount++;
                        }
```

Note that `skirtRadiusM` is no longer referenced inside the per-cell
loop. It can stay declared in the outer scope (used for nothing but
Debug.Log now, which is fine) or be removed — either way.

---

### Step 5 — Update the Debug.Log

Around line 3235:

```csharp
                Debug.Log($"[HoleGeoImporter] Tee {region.id}: " +
                          $"platform h={maxH:F4}, " +
                          $"base skirt={TeeSkirtMeters:F1}m, " +
                          $"worst adaptive skirt={worstAdaptiveM:F1}m");
```

---

### Expected behavior

- **Gentle terrain (e.g., Tee 2 on Hole 4, all of Hole 1):** drop is
  small, `adaptiveM` clamps to `TeeSkirtMeters` = 2 m. Skirt is
  identical to the pre-change behavior. No visible difference.
- **Steep terrain (Tee 1 SW side on Hole 4):** drop is 5–8 m,
  `adaptiveM` extends to 15–30 m. The skirt ramp rides along the
  natural hillside at max ~19° slope, adding at most ~1 m lift above
  baseline at any point. Cliff/serrated rendering disappears.
- **Tee 1 / Tee 2 adjacency:** both use `maxH` platforms (unchanged),
  so they blend smoothly via MAX-merge as they did originally. No new
  cliff between them.

### Regression checklist

- [ ] Hole 4 Tee 1 SW side: no cliff, no serrated band. Ramp looks
      natural, almost like there's no tee mound on that side — just a
      gentle hill merging into the tee platform.
- [ ] Hole 4 Tee 2 looks identical to before (gentle terrain, adaptive
      radius stays at base 2 m).
- [ ] Hole 4 Tee 1 / Tee 2 boundary: single continuous tee complex, no
      new geometric artifacts between them.
- [ ] Hole 1, 7, 12, 18: tees look identical to before (gentle
      terrain in all cases — base 2 m skirt).
- [ ] Fairways, greens, bunkers, cart paths: no change.
- [ ] `Debug.Log` shows `worst adaptive skirt=2.0m` for easy tees,
      higher values only for genuinely steep cases.

### Fallback if the fix overshoots

If in some hole the adaptive skirt extends too far and visibly pulls
terrain into the tee's skirt (e.g., you see a >~1.5 m lift in some
region where it looks wrong), **lower `TeeMaxRampSlope` to 0.5** (~27°,
still walkable but allows steeper ramps). This shortens the adaptive
skirt proportionally. If the opposite — we still see serrated bands —
**raise `TeeMaxRampSlope` to 0.25** (~14°, gentler) which extends the
skirt further.

If a specific tee's skirt hits `TeeMaxSkirtMeters = 60` (visible in
Debug.Log), that's a pathology — flag and we'll investigate the
contour.

---

### Design note (for future me)

Real golf course designers grade tees into hillsides over 15–30 m,
not over 2 m. A steep pad with a 2 m skirt is a structural impossibility
in the real world — the grass face would slide off. The adaptive skirt
matches how real courses look: flat pad, then a very gentle 15–25 m
merge into the hillside, invisible unless you measure it. The earlier
"bigger skirt looks too big" concern was about fixed-radius widening
(a visibly bigger mound everywhere). Per-cell adaptive skirts are
invisible on flat terrain and only extend where physically necessary.

---
