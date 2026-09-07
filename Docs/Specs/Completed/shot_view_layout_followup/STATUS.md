DONE

Cesar approved 2026-09-07. Both spec items shipped, plus three things the work turned up on the
way: the club head was covering the lane's own tick labels (a draw-order bug, fixed and then fixed
properly so they still fade with the lane), the tiles were photographing four different lies, and
T_Pendulum_3's missing grade pop turned out to be a real gameplay defect — HandleReverseCancel
measured its hold in wall clock with no stutter guard and would have killed a real player's shot on
a device that hitched mid-flick.

Implemented 2026-09-07 by Claude Code (direct, no subagent chain — Cesar dispatched it in-thread).

Item 1 (cap the pill) is complete and green: 1170x2532 Pendulum LaneHeight 792.16, lane end -1096.00,
Free Swing lane end -1096.00, and a new test proves a 120% pull still publishes 1.2 with the capped
pill in place. EditMode 2731 tests, 2728 passed, 0 failed.

Item 2 (tile crop) is complete and all twelve tiles are committed. On top of the spec, Cesar asked
mid-task for the node's frame — `Tile` 14145:37494: rounded 20px (the code had 32) and a 2px
rgba(255,255,255,0.35) outline (the code had none) — so `FitCrop` also grows a crop back to the tile
aspect on whichever axis is short, and the tile is filled rather than letterboxed. `fails: []`, zero
HUD-chrome nudges, pop-up verify 167/167 at 1170x2532.

Flick's tiles are NOT byte-identical, deliberately: its LAYOUT is untouched, but the radius and the
outline apply to every tile and shipping three schemes framed one way and the fourth another would
be visibly inconsistent. Reversible in one line if you disagree.

Two things remain open, neither introduced by this task and neither fixable in a crop:

  1. FIXED and confirmed on screen: the tick labels draw AFTER the club head instead of being
     children of the lane root under it, so "100%" reads over the club where before there was
     nothing but club. Five labels lifted in the scene at 0.000000 corner delta, both scheme
     builders author them that way, and the shipped tiles show it.

  ALSO FIXED (Cesar: "plan the shot so they are not in the shade"): every Capture commits a shot, so
  the loop was photographing each scheme from wherever the PREVIOUS scheme's ball landed — which
  walked the set into tree shadow. ResetLie() puts the ball back on the tee between SelectScheme and
  Capture (reset_to_tee: ok x4 in the heartbeat), so all four are now shot from the same lit lie.
  2. T_Pendulum_3: FIXED (IMPLEMENTER_REPORT §5 traced it, §6 fixes it). Two causes, both closed:

     6a. HandleReverseCancel measured its 0.12s hold in wall clock with no guard, so one long frame
         could exceed the window on its own — it fired at held=0.333s on a genuine 100% pull. Both
         drivers that carry it (Pendulum and Needle) now skip frames longer than the flick gate's
         own ShotController.StutterFrameSeconds. A stuttering device now loses the CANCEL, not the
         SHOT. Tested both ways in both driver test files; Needle's copy had no test at all before.

     6b. Even so a hand-rolled flick could not commit here — it only moved the failure to
         EvaluateFlickGate, which refuses any sample pair longer than 0.1s, and at the capture's
         ~111ms frames that is every pair. Step 3 now swings through BotSwing.PlayPerfect, the seam
         the codebase already names for capture bots; it resolves ActiveExecutor per CLAUDE.md
         rule 17 and releases with requireFlickGate:false.

     Verified: pop alpha 1.000 (was 0.000), scale 0.979 mid-animation, manifest marker 0.000 (was
     the NaN sentinel), and the tile reads "JUST!".

  3. REGRESSION FIXED (mine, reported by Cesar): lifting the tick labels above the club head in
     98fe518a9 also lifted them out of the lane's fade group, so they showed at rest and through
     the whole ball flight. They now live in a per-scheme LabelSpace container, drawn after the
     handle, whose alpha PendulumFadingView hands down via a new optional _mirrorGroup. Verified
     live: alpha 0.000 at rest, 1.000 during a pull, still above the handle in sibling order.

  4. Not a defect: the action buttons are absent from the TILES by design (scheme_confirm_popup
     §3.2, "No HUD chrome may appear in a tile" — HideChrome hides them per tile and restores
     after). The saved scene has all of them m_IsActive: 1. If they are missing in the Editor now,
     it is a leaked HideChrome from a capture run that died on an MCP timeout; re-entering play
     clears it.

Full detail, per-tile crop rects and screenshots in IMPLEMENTER_REPORT.md.
