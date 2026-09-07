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

  1. At a 100% pull the 3x club head covers the Pendulum lane's own 100%/120% labels. That is the
     live game — a tile is a photograph. It belongs with the club-head scale.
  2. T_Pendulum_3 photographs an invisible grade pop, across all three of today's runs. Diagnosed
     (activeInHierarchy is true for a pop that has already faded); the opacity fix I added did NOT
     resolve it, so the capture appears to grab a frame later than the wait returns.

Also worth a look: this run's Pendulum / Needle / Free Swing frames are darker than Flick's because
the bot's ball ended in shade. One-click re-run if you want a brighter set.

Full detail, per-tile crop rects and screenshots in IMPLEMENTER_REPORT.md.
