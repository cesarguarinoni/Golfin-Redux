# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — TWO-FILE FIX: Smooth Play/Non-Play Boundary

The terrain cliff between play area and OB is caused by a hard
residual switch **upstream** in the Node.js pipeline. The Unity-side
boundary height propagation (already in place) is supplementary.

### File 1 (PRIMARY): `Tools/UHoleLite/docs/TASK.md`

Read and execute the spec in `Tools/UHoleLite/docs/TASK.md`. It
modifies `Tools/UHoleLite/scripts/generate-terrain.mjs` to add a
distance-based residual ramp at the play/non-play boundary.

This is the main fix. After this, the heightmap.raw will already
be smooth at the boundary.

### File 2 (ALREADY DONE): `HoleLiteImporter.cs`

The Unity-side boundary height propagation + smoothstep blend is
already implemented in `CreateTerrain`. It provides additional
smoothing on top of the upstream fix. No changes needed.

### Running (both steps)

1. `cd Tools/UHoleLite && node scripts/generate-terrain.mjs lomond-country-club 1`
2. In Unity: GOLFIN > Import Hole (Lite) > Hole 01

### Verification

Walk along the fairway/rough boundary in Unity — terrain should be
flush, no cliff. Hills in distant OB should still be visible.

---

## Completed Tasks
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
