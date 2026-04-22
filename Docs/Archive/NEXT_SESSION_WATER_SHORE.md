# Water Shore Ramp — Adaptive Radius Investigation

**Context for next session:** Tee mound work is done (see `tasks/lessons.md` additions from 2026-04-18 and `HoleGeoImporter.cs::FlattenTerrainUnderTees`). The tee skirt went through: chamfer → exact polygon-edge distance → per-cell adaptive radius. Final fix was adaptive radius (`dR = clamp(1.5 × dropAbs / MaxRampSlope, base, cap)`) that sizes the skirt per-cell to keep ramp slope ≤ 19°. Eliminated "serrated grass" rendering (which was Unity's terrain shader stretching grass on steep triangle faces, not a gradient discontinuity).

User asked whether those lessons apply to the water shore ramp. Answer: **likely yes, conditionally.**

---

## What the water shore ramp already has

In `HoleGeoImporter.cs::DepressTerrainUnderOverlays`, "Shore slope pass" at line ~3472:

- ✅ Exact polygon-edge distance (not chamfer) — lines 3524-3541
- ✅ Coarse chamfer as cull — lines 3486-3505
- ✅ Smoothstep lerp — lines 3547-3548
- ✅ MIN-merge (only lowers terrain toward water) — line 3553

## What it's missing (the tee innovation)

- ❌ Fixed `ShoreRadius = 10 cells ≈ 5m` regardless of local bank steepness (line 19)

Same failure mode as tees: on steep banks, drop from `originalH` to `nearSurfY` compressed into 5m → potentially steep ramp face → rendering artifact (vertical-stretched grass). User confirmed the symptom: *"We smoothed those ridges enough but not as perfectly"* — residual ridges on steep water edges are consistent with fixed-radius-too-small.

## Before changing anything — run the data check

The fix only matters if drops are significant. Relevant number per shore cell:

```
drop = originalH[z,x] - nearSurfY
```

Where `nearSurfY = minTerrainH_of_water_body - 0.05m` (line ~2838 region).

**If max drop on any water body is < 1m:** fixed 5m ramp is fine, skip the fix.
**If max drop is 2-5m:** adaptive radius will visibly improve shore.
**If max drop is > 5m:** adaptive radius essential.

## Sampling script to write (first task next session)

Mirror `Tools/sample-tee-heights.js`. For each water body on each hole:
1. Read `water.json` → get contours
2. Compute `minTerrainH` inside each polygon → `nearSurfY = minTerrainH - 0.05m`
3. For each perimeter vertex, sample `originalH` at 5m outside the contour
4. Report max drop and drop distribution

Lomond has water on Holes 7 and 12 (possibly others — check `water.json` per hole in `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/`).

## Spec stub (pre-written, apply only if data warrants)

If the sampling shows drops ≥ 2m anywhere, the change is almost a copy-paste of the tee adaptive radius:

### Constants (near line 17)

```csharp
/// <summary>Maximum shore ramp slope (rise/run). 0.35 ≈ 19°.
/// When natural bank is steeper, the shore ramp extends outward
/// per-cell so the rendered slope never exceeds this.</summary>
private const float ShoreMaxRampSlope = 0.35f;

/// <summary>Upper cap on per-cell adaptive shore radius (meters).
/// Safety valve — natural banks deeper than this aren't smoothed.</summary>
private const float ShoreMaxRadiusMeters = 40.0f;
```

### Replace the lerp block (lines 3544-3557)

Old:
```csharp
if (minDistM > shoreRadiusM) continue;

float t = minDistM / shoreRadiusM;
t = t * t * (3f - 2f * t); // smoothstep

float originalH = heights[z, x];
float targetH = Mathf.Lerp(nearSurfY, originalH, t);

if (targetH < originalH)
{
    heights[z, x] = Mathf.Max(0f, targetH);
    shoreCount++;
}
```

New:
```csharp
float originalH = heights[z, x];
float dropAbs = Mathf.Abs(originalH - nearSurfY);
float adaptiveM = Mathf.Clamp(
    1.5f * dropAbs / ShoreMaxRampSlope,
    shoreRadiusM,                 // base: existing ShoreRadius × cellSize
    ShoreMaxRadiusMeters);

if (minDistM > adaptiveM) continue;

float t = minDistM / adaptiveM;
t = t * t * (3f - 2f * t); // smoothstep

float targetH = Mathf.Lerp(nearSurfY, originalH, t);

if (targetH < originalH)
{
    heights[z, x] = Mathf.Max(0f, targetH);
    shoreCount++;
}
```

### Coarse cull update (line 3514)

The coarse cull uses `ShoreRadius + 2`. With adaptive, worst-case radius is bigger, so culls must be too. Compute worst-case per water body (max drop across the body × 1.5 / ShoreMaxRampSlope, capped) → convert to cells → use that for coarse cull.

Pattern is the same as the tee worst-case computation (see `FlattenTerrainUnderTees` around the `worstAdaptiveCells` calculation).

## Key differences from the tee fix

- Water shore **MIN-merges** (lowers terrain toward water). Tees MAX-merge (raise toward platform). Direction-flipped but mathematically symmetric.
- Water loops over ALL cells once for all water bodies together. Tees loop per-region. For adaptive radius in water, `dropAbs` must use the `nearSurfY` of the NEAREST water body (which the existing code already tracks in `nearSurfY`).
- `TerrainYOffset` is bound to `ShoreDepthMeters`. Changing shore behavior shouldn't affect this — verify after the change that terrain clamp headroom is still OK.

## Regression checks if applied

- Hole 7 / Hole 12 (or whichever has water): shore transitions smooth, no ridges.
- Water body in gentle terrain: looks identical to before (small drops → adaptive clamps to base `shoreRadiusM`).
- `Debug.Log` counter `water shore ramp: N cells` may increase (adaptive covers more cells on steep banks).
- Check that `ShoreDepthMeters` depression under water still works.
- Verify water surface mesh still sits cleanly on shoreline.

## Open questions to ask user at session start

1. Does Hole 7 (or whichever has water) visibly show the residual ridges? A screenshot of the steepest water bank would confirm whether the fix is worth pursuing.
2. If yes, run the sampling script first, then apply the spec if drops justify it.
3. If no visible ridges, skip — avoid unnecessary risk to shipping water behavior.

## Context files to re-read

- `Docs/TellCode.md` — most recent tee border fix (inside-inset ring + V=0.5 texture fix)
- `tasks/lessons.md` — 2026-04-18 additions: chamfer-vs-exact, serrated-grass-vs-C1, adversarial-review meta-lesson
- `HoleGeoImporter.cs::FlattenTerrainUnderTees` — adaptive radius reference implementation (lines ~3067-3280)
- `HoleGeoImporter.cs::DepressTerrainUnderOverlays` shore pass — target for modification (lines ~3472-3560)

## TL;DR

Lesson from tees (adaptive per-cell radius to keep ramp slope walkable) is directly portable to water shore. Whether it's worth applying depends on actual water bank steepness on this course — run the sampling script first, decide based on max drop, then apply the pre-written spec (or skip if drops are mild). Low risk, high upside on steep banks, zero change on flat banks.
