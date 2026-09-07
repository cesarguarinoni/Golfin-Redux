# §9.9 — Architect decision on the §9.8 handoff (2026-09-07)

> Source: `ARCHITECT_DECISION_9_9.md.docx` (Cesar's Desktop, 2026-09-07 16:20). Committed here
> verbatim because it was never pushed and the decisions it carries gate §9.8's close-out.

Read HANDOFF_9_8_ARCHITECT.md (commit 9ee16fc83). Decisions, in the order the handoff asks them.
1. Club mount — (b). Accept the clip's own wrist; report the difference.
The shaft direction expressed in the hand frame is identical on both rigs — (-0.076, -0.645, 0.760). That IS the correct invariant for "same mount"; it is what makes the two prefabs comparable. Whatever remains in world space at address is Remy's clip rolling the wrist differently from the Y-Bot clip, i.e. exactly the kind of difference §9.8 exists to surface. Do not bake a FromToRotation correction into the socket — that would hide a finding to make a picture look right.

If Cesar wants the club to read correctly in the side-by-side frames, that is a ten-second Inspector edit by hand on PfGolfer_MixamoNative → ClubSlot local rotation, done by eye, and noted in the report as "socket hand-aligned for the render, hand-frame direction still (-0.076, -0.645, 0.760)". Optional; the numbers do not depend on it.
2. Controller duplication — accepted, and correct.
AnimatorController_Golfer_MixamoNative pointing at the Remy clips is the only reading under which §9.8 measures anything. "Same controller" in §9.8 meant same states/transitions, which it is. No revert.
3. forceGripPose = false — accepted.
Satisfies all three prohibitions with zero code. Good.
4. Harness — one small fix before the run
The grip.* block must skip with N/A — rig has no Quaternius finger bones when middle_02_r (etc.) is absent, instead of measuring 70–78 m and throwing. The exceptions are what paused every Mixamo run. Keep ConsoleWindow.SetConsoleErrorPause(false) at harness start. "Launch refused: already in play mode" becomes a thrown error.
5. Finish §9.8 — one run, no more
GOLFIN > Golfer Test > Verify Mixamo-native on Hole 06 (§9.8) once, using the play/poll sequence in the handoff §10. Deliver: golfer_invariants_mixamo.json, worst foot-slide L/R during the swing (Quaternius baseline is 0.4770 / 0.4552 m), and the three frames (address, at-rest, t = 0.6 s after commit) beside the Quaternius ones. Then write the retarget-vs-clips conclusion from those numbers only: if Mixamo-native foot slide is a fraction of 0.477 m and the body holds its address, retargeting is the cause and the roster pipeline is "rig the model in Mixamo, download clips on it"; if it slides the same, the clips are the cause and club-in-hand mocap is the next stop. Either answer closes the test.
6. Then close
Restore the active profile to iOS-Full-GPS, STATUS.md → READY_FOR_SELF_REVIEW, IMPLEMENTER_REPORT.md Findings + the §2 scale note (useFileScale ON, globalScale = 1.328/3.089), Docs/AI_CONTEXT.md.

The scale finding and the pipeline conclusion go into Docs/Design/CHARACTER_3D_REMAKE_OPTIONS.md — Architect does that after the report lands.

