IMPLEMENTER_WORKING

Bisect DONE, and it cleared Phase 1: the flat untextured terrain Cesar saw on build 2314 is
PRE-EXISTING. Reproduced in the Editor Game View at HEAD, then re-shot with all four Phase 1
changes reverted, then against real pre-Phase-1 code (a98008f6d checked out for
PhysicsLabController.cs + Mobile_Renderer.asset + Mobile_RPAsset.asset). The near-fairway patch is
bit-identical in all three (141.9, sd 22.44) and flat in all three. Steps 1-4 were not needed.
Details + frames: report §11.4, bisect_step0_*.png.

The flat terrain is its own task, not this one. m_UseNativeRenderPass: 1 (on both Mobile_Renderer
and PC_Renderer) is the obvious first probe for it.

SHIPPED THIS ROUND: drawInstanced removed from ApplyTerrainRenderDefaults. It was instructed either
way, and it is justified twice over -- within noise on device (13.48 vs 13.35 ms, identical
batches/tris) and it measurably flattened DISTANT terrain (mid-rough sd 13.12 vs 22.10). It also
carried a device-only stripping risk: every hole ships m_DrawInstanced: 0, the flag is runtime-only,
and GraphicsSettings m_InstancingStripping is StripUnused.
=> §3 is now the tree-distance normalisation only. Both halves of Phase 0b's (c) are gone.

NEXT (not started): rebuild Dev-iOS with drawInstanced removed, then resume the device pass --
report §11.7 list: Hole 08 runs 1-2 + Holes 01/06 + mid-flight, 3-run medians, Frame Debugger,
the P1_teardown bot job (built, never run), MapView check, shoreline frames, Lesson O playthrough.
