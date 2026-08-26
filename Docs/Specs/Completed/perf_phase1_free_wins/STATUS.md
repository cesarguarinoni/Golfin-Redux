DONE

Moved to Completed/ on Cesar's approval, 2026-08-26. EVERY acceptance item passes. Device pass complete on 2316 (pinned sky + pinned yaw, 3 runs per
pose), and Lesson O closed on 2317: Cesar played Hole 08 end to end -- "Smooth as a baby's butt."
(Hole 13 is locked; unrelated to this task.)

Closed out by Cesar: "move it".

RESULTS (primary sample, same measurement point as Phase 0b's before-numbers):
  H08 tee        30.1 -> 60.0 fps | 26.11 -> 14.34 ms | 7,375 -> 3,014 batches | 5.03M -> 2.37M tris
  H06 tee        35.2 -> 60.0 fps | 26.59 -> 14.75 ms   (target was <= 26.59 ms)
  H01 tee        59.8 fps | 1,957 batches
  H08 mid-flight 59.9 fps | 13.71 ms   (never measured before)
  GC/frame       29,030 -> 21,506 B (-26%), identical across all 12 runs

ITEMS: 16 PASS, 17 PASS (both holes), 18 RECORDED, 19 PASS (direct state: 1 camera rendering, both
depth globals are the UnityBlack dummy, depth-normals null), 22 PASS 8/8 on device via
teardown_invariants.json fails=0, 23 PASS (DoFrameReadbackAndDump absent from the IL2CPP binary),
24 PASS. 20 NOT COMPARABLE (old reference shot under an unpinned sky). 21 Editor-only.

THE HARNESS IS NOW REPRODUCIBLE: with sky and yaw pinned, batches and triangles are IDENTICAL across
all three runs of every hole. Phase 0b swung 7,375 vs 6,086 on the same pose -- that was the sky.

TWO CAVEATS, both recorded rather than buried:
 1. Sustained load: after 45 s at thermal Serious, H08 tee falls to 47.5 fps and H06 tee to 40.7.
    H01 and mid-flight hold 60. That is 9a / Phase 2-3, not something Phase 1 claimed.
 2. renderMs from ProfilerRecorder is unreliable (reports ~3.3 ms on 16.7 ms frames). fps and
    frameMs carry every verdict above.

BLOCKER FOR TESTERS, NOT FOR THIS TASK: the flat untextured terrain (report §11.4) is PRE-EXISTING --
proven against real pre-Phase-1 code. It is on screen on every hole. First probe:
m_UseNativeRenderPass: 1, set on BOTH Mobile_Renderer and PC_Renderer.
