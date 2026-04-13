# TASK.md — Instructions for Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`

---

## Current Task — Pull Back Snapped Spine Endpoints at T-Junctions

**File:** `Tools/UHoleLite/scripts/export-hole.mjs`
**Function:** `extractCartPathContours` — orphan endpoint snapping section (~line after "Snap orphan endpoints" comment)

### Problem

Cart path strip meshes overshoot at T-junctions. When a branch spine
(e.g. CP#4) snaps its endpoint to a main spine's centerline (e.g. CP#3),
the strip mesh's half-width (1.25m) extends past the main path's far edge.
The spine endpoint itself is correctly placed (within 0.5–0.8m of the
target), but the mesh geometry sticks out 1.25m beyond the junction.

Visually: the branch path extends past the main path instead of meeting
it cleanly.

### Root Cause

The orphan endpoint snapping (`bestDist > 0.5 && bestDist < snapRadius`)
snaps the spine endpoint TO the target spine's centerline. But since the
strip mesh extends ±halfWidth from the center, the strip overshoots by
halfWidth past the target's far edge.

### Solution — Pull back snapped endpoints by halfWidth

After the existing orphan endpoint snapping loop, add a **pullback pass**.
For each endpoint that was snapped (added via unshift/push), walk the
approaching spine backward by `halfWidth` (1.25m) and truncate the spine
so the strip's edge aligns with the target spine's centerline. The overlap
of halfWidth with the main path's strip covers the junction visually.

### Exact Changes

Find the orphan endpoint snapping section (search for the comment
`// --- Snap orphan endpoints to nearest point on other spines ---`).

**After** the entire orphan snapping loop (after the closing `}` of the
`for (let ai = 0; ...)` loop), add this new pullback block:

```javascript
  // --- Pull back snapped endpoints by halfWidth ---
  // At T-junctions, the branch strip extends ±halfWidth from its spine
  // center. If the endpoint sits on the target spine's centerline, the
  // strip overshoots by halfWidth past the far edge. Pull the endpoint
  // back along the approach direction by halfWidth so the strip EDGE
  // (not center) lands at the target centerline. The overlap with the
  // main strip covers the junction area.
  const halfWidth = minWidthM / 2;
  for (const cp of results) {
    if (!cp.spine || cp.spine.length < 3) continue;

    for (const endIdx of [0, cp.spine.length - 1]) {
      const ep = cp.spine[endIdx];

      // Check if this endpoint is near another spine's interior
      let isSnapped = false;
      for (const other of results) {
        if (other === cp) continue;
        if (cp.parent_region !== other.parent_region) continue;
        if (!other.spine || other.spine.length < 2) continue;

        for (let si = 1; si < other.spine.length - 1; si++) {
          const dx = ep.x - other.spine[si].x;
          const dz = ep.z - other.spine[si].z;
          const d = Math.sqrt(dx * dx + dz * dz);
          if (d < halfWidth * 2) {
            isSnapped = true;
            break;
          }
        }
        if (isSnapped) break;
      }

      if (!isSnapped) continue;

      // Pull back: find the point on the spine that is halfWidth
      // away from the endpoint, measured along the spine arc
      if (endIdx === 0) {
        // Pull back from start: remove leading points until we've
        // traveled halfWidth along the spine, then interpolate
        let accumulated = 0;
        let cutIdx = 0;
        for (let i = 0; i < cp.spine.length - 2; i++) {
          const dx = cp.spine[i + 1].x - cp.spine[i].x;
          const dz = cp.spine[i + 1].z - cp.spine[i].z;
          const segLen = Math.sqrt(dx * dx + dz * dz);
          if (accumulated + segLen >= halfWidth) {
            // Interpolate the new start point
            const remaining = halfWidth - accumulated;
            const t = remaining / segLen;
            cp.spine[i] = {
              x: parseFloat((cp.spine[i].x + t * dx).toFixed(2)),
              z: parseFloat((cp.spine[i].z + t * dz).toFixed(2)),
            };
            cutIdx = i;
            break;
          }
          accumulated += segLen;
          cutIdx = i + 1;
        }
        if (cutIdx > 0) {
          cp.spine.splice(0, cutIdx);
        }
      } else {
        // Pull back from end: remove trailing points
        let accumulated = 0;
        let cutIdx = cp.spine.length - 1;
        for (let i = cp.spine.length - 1; i > 0; i--) {
          const dx = cp.spine[i].x - cp.spine[i - 1].x;
          const dz = cp.spine[i].z - cp.spine[i - 1].z;
          const segLen = Math.sqrt(dx * dx + dz * dz);
          if (accumulated + segLen >= halfWidth) {
            const remaining = halfWidth - accumulated;
            const t = remaining / segLen;
            cp.spine[i] = {
              x: parseFloat((cp.spine[i].x - t * dx).toFixed(2)),
              z: parseFloat((cp.spine[i].z - t * dz).toFixed(2)),
            };
            cutIdx = i;
            break;
          }
          accumulated += segLen;
          cutIdx = i - 1;
        }
        if (cutIdx < cp.spine.length - 1) {
          cp.spine.splice(cutIdx + 1);
        }
      }
    }
  }
```

### Also Fix: `spineExt` Reference Bug in Unity

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`
**Function:** `CreateSpineStripMesh`

The function references `spineExt` (a leftover from the removed endpoint
extension feature) but it should reference `spine`. Replace ALL
occurrences of `spineExt` with `spine` inside `CreateSpineStripMesh`:

- `spineExt[1].z` → `spine[1].z`
- `spineExt[1].x` → `spine[1].x`
- `spineExt[0].z` → `spine[0].z`
- `spineExt[0].x` → `spine[0].x`
- `spineExt[n - 1].z` → `spine[n - 1].z`
- `spineExt[n - 1].x` → `spine[n - 1].x`
- `spineExt[n - 2].z` → `spine[n - 2].z`
- `spineExt[n - 2].x` → `spine[n - 2].x`
- `spineExt[i + 1].z` → `spine[i + 1].z`
- `spineExt[i + 1].x` → `spine[i + 1].x`
- `spineExt[i - 1].z` → `spine[i - 1].z`
- `spineExt[i - 1].x` → `spine[i - 1].x`

There should be ~12 references. Just do a find-replace of `spineExt`
→ `spine` within the `CreateSpineStripMesh` method only.

NOTE: If `spineExt` is declared as `var spineExt = spine;` at the top
of the function, remove that line too. If there's a block that creates
`spineExt` by extending endpoints, remove the entire extension block
and just use `spine` directly.

### What NOT to Change

- Do not modify `BuildSpinePolygon` — it already uses `spine` correctly
- Do not modify splatmap painting logic
- Do not modify terrain depression logic
- Do not modify chain merging or junction snapping logic
- Do not change `nudgeSpinesFromContours`
- Do not add junction disc patches or endpoint extensions

### Key Behavior

- **Branch endpoints near a main spine's interior:** Pulled back by
  1.25m so the strip edge lands on the target's centerline
- **Free endpoints (not near any other spine):** Untouched — they
  just end naturally
- **Endpoint-to-endpoint connections:** Untouched — `isSnapped` only
  triggers for interior proximity
- **Splatmap/depression:** `BuildSpinePolygon` reads the shortened
  spine, so painting and depression automatically match the new geometry

### Data Verification (Hole 18)

Before pullback:
- CP#4.start (144.6, -289.2) → 0.8m from CP#3 interior [208/464]
- CP#4.end (180.8, -232.3) → 0.5m from CP#2 interior [155/196]

After pullback (expected):
- CP#4 spine loses ~1.25m of arc length at each end
- CP#4 goes from 22 points to ~20 points
- Strip edges now land on CP#3/CP#2 centerlines instead of overshooting

### Running

```bash
cd Tools/UHoleLite
node scripts/export-hole.mjs lomond-country-club 18
```

Then in Unity: GOLFIN > Import Hole (Lite) > Hole 18

### Verification

1. Export hole 18, import in Unity
2. Walk to CP#4 junctions — strip should meet CP#3/CP#2 cleanly
3. No gap between branch and main path (overlap covers junction)
4. Free path endpoints (CP#1 end, CP#3 start/end) unchanged
5. Run `--all` to verify no regressions on other holes

---

## Completed Tasks
✅ 2026-04-13 — Pull back snapped spine endpoints by halfWidth at T-junctions + spineExt→spine fix in Unity
✅ 2026-04-13 — Distance-based residual ramp at play/non-play boundary (60-cell smoothstep transition)
