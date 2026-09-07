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

  1. FIXED (Cesar asked mid-task): the tick labels now draw AFTER the club head instead of being
     children of the lane root under it. Five labels lifted in the scene at 0.000000 corner delta,
     and both scheme builders author them that way. NOT yet seen on screen and NOT yet in the
     shipped tiles — the Editor's main thread stopped servicing MCP right after the edit (0.8% CPU,
     no log for 18 min, menu lines last: it reads as a modal dialog waiting on a click). Re-run
     GOLFIN > Capture > Scheme Confirm Tiles once it is free.
  2. T_Pendulum_3 photographs an invisible grade pop, across all three of today's runs. Diagnosed
     (activeInHierarchy is true for a pop that has already faded); the opacity fix I added did NOT
     resolve it, so the capture appears to grab a frame later than the wait returns.

Also worth a look: this run's Pendulum / Needle / Free Swing frames are darker than Flick's because
the bot's ball ended in shade. One-click re-run if you want a brighter set.

Full detail, per-tile crop rects and screenshots in IMPLEMENTER_REPORT.md.
