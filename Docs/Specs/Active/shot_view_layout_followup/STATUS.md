READY_FOR_ARCHITECT_REVIEW

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
  2. T_Pendulum_3: FULLY TRACED (IMPLEMENTER_REPORT §5). Advance's HandleReverseCancel fires at
     held=0.333s — the capture's frames are ~111ms and step 3 drags up over three of them — so
     _dragging is already false when OnPointerUp arrives and ReleaseSwing never runs. Shortening
     the gesture only moves the failure to the flick gate, which refuses any sample pair longer
     than _stutterFrameThreshold (0.1s) and so can never pass at this frame rate either. The rig
     cannot produce a committing Pendulum flick through synthetic pointer events. FIX (scoped, not
     done): route step 3's swing through the bot path, which is requireFlickGate:false and is what
     CLAUDE.md rule 17 already mandates. Exit probes kept behind the driver's _logSwings flag.

     Worth a look beyond the capture: HandleReverseCancel measures its 0.12s hold in wall clock
     with no stutter guard, unlike the flick gate beside it. A device that hitches mid-flick would
     kill a real player's shot the same way.

Also worth a look: this run's Pendulum / Needle / Free Swing frames are darker than Flick's because
the bot's ball ended in shade. One-click re-run if you want a brighter set.

Full detail, per-tile crop rects and screenshots in IMPLEMENTER_REPORT.md.
