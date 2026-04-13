# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Fix `spineExt` Bug in CreateSpineStripMesh

### Problem
`CreateSpineStripMesh` in `HoleLiteImporter.cs` references an undefined
variable `spineExt` — a leftover from the removed endpoint extension
feature (step 9, removed earlier). This should be `spine` (the method
parameter).

### Fix
In `CreateSpineStripMesh` (~line 3893+), find-replace ALL occurrences
of `spineExt` → `spine` within that method only. There are ~12 refs:

Tangent calculations:
- `spineExt[0]`, `spineExt[1]` (i==0 case)
- `spineExt[n-1]`, `spineExt[n-2]` (i==n-1 case)
- `spineExt[i+1]`, `spineExt[i-1]` (middle case)

Arc length calculation:
- `spineExt[i-1]` (2 refs for dx/dz2)

If there's a `var spineExt = spine;` or an extension block that creates
`spineExt`, remove it and just use `spine` directly.

### Do NOT Change
- `BuildSpinePolygon` (already correct)
- Any other methods
- Splatmap painting or depression logic

### After Fixing
Run: GOLFIN > Import Hole (Lite) > Hole 18
Verify cart paths render without errors in the console.

NOTE: The pipeline-side pullback fix (in TASK.md) must also be run
before the visual junction issue is fully resolved. Run:
```
cd Tools/UHoleLite
node scripts/export-hole.mjs lomond-country-club 18
```
Then re-import in Unity.

---

## Completed Tasks
✅ 2026-04-13 — Fix spineExt→spine in CreateSpineStripMesh + pullback snapped endpoints by halfWidth at T-junctions
✅ 2026-04-13 — Node.js residual ramp (60-cell smoothstep) + Unity-side boundary height propagation
✅ 2026-04-13 — Unity-side boundary height propagation (smoothstep ramp from play height to blurred DEM)
✅ 2026-04-13 — Cart path depression: 3-strategy fix (0.50m inset + smoothstep ramp + 2px splatmap edge paint)
✅ 2026-04-13 — Natural OB↔Rough transition: reuse rough texture with tint + 4px boundary blend
✅ 2026-04-13 — "Smooth OB" button in UHole Lite (vectorize → RDP → Chaikin → rasterize)
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix (OffsetContourOutward + spine polygon)
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-11 — Grid draping iterations, Y=0 origin pattern
✅ 2026-04-10 — Tree placement (mixed mode: terrain + standalone)
✅ 2026-04-10 — Bunker v1-v5 iterations
✅ All earlier tasks (water, bunkers, greens, textures, cart paths, etc.)
