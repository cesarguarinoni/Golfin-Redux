ARCHITECT_REVIEW_ESCALATE

Device pass ran and was HALTED by Cesar: the Hole 08 tee frame still looks wrong to him on both the
shipped configuration and the basemap variant, and the visual question is going to the Architect
because it reproduces faster in the Editor than on device.

SETTLED — the performance win is real, measured under a controlled protocol (pinned sky + pinned
yaw, thermal Nominal, build 2314 on iPhone 15 Pro Max):
  Hole 08 tee   30.1 -> 58.1 fps | 26.11 -> 13.35 ms render thread | 7,375 -> 1,848 batches
                5.03 M -> 1.78 M tris | GC 29,030 -> 21,506 B/frame
  Caveat: ONE run, not the 3-run median the protocol demands.

OPEN — see Docs/Reports/perf_baseline_2026-08-26.md §11.4. Neither hypothesis chased this session
explains what Cesar sees: it is not basemapDistance (the two frames differ by mean 2.01/255, one of
them at the authored 1000) and it is not sky variation (both frames share a pinned sky). The one
test never run is the decisive one: Hole 08 tee on a98008f6d (pre-Phase-1) vs HEAD, pinned sky,
identical camera.

PROCESS FINDING worth carrying beyond this task: SkyRandomizer rolls a new sky per app launch, so no
frame comparison in this report -- Phase 0b's included -- was taken under controlled lighting. Now
pinned via PerfBaselineBot.PinSky(). Any future frame A/B without a pinned sky is not evidence.

NOT RUN: runs 1-2, Holes 01/06, mid-flight, 3-run medians, Frame Debugger, the teardown bot job
(built, never executed), MapView device check, shoreline frames, Cesar's playthrough.
