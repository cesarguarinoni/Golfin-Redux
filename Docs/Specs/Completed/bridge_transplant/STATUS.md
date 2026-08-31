DONE

Approved by Cesar 2026-08-31.

All 7 bridges from the SPEC ground-truth table are transplanted, decked, zoned and obstacle-baked
across holes 7, 8, 9, 12 and 17 — Stages A + B + C each, plus the two decisions Cesar took
(bots avoid bridges; railing boxes authored for the collider-less FBX).

Shipped:
  · SurfaceType.Bridge at classifier priority 95, so a deck outranks the water it spans.
  · Deck as a zone mesh — heightmap.bytes never re-baked on any hole.
  · BridgeObstacleData / Provider / Loader / Baker, mirroring the tree system; `bridges` threaded
    through BallSimulation's flight, roll and putt behind a bridges=null gate proven bit-exact.
  · VersusBot.IsAvoidSurface includes Bridge.
  · Tracked bridge_instances.json catalogs so the transplant survives the repo boundary.

Evidence: every instance at max |dpos|/dangle/dscale = 0.000000 against the Video source;
containment 8/8 per bridge (56 shots, 56 contained); tree drift gate 18/18 PASS; 22 EditMode
assemblies, 1962 passed, 0 failed; all five holes' tree_obstacles.csv and heightmap.bytes
md5-identical to the session baseline.

Commits: 9c059425b (hole 7), 877e609d7 (holes 8/9/12/17).

Carried forward, NOT blocking:
  · Cesar's feel pass — deck 0.45/0.35/0.12/0.10, railing 0.35/0.75, pier 0.45/0.85.
  · Hole 17 ships 2 synthesized kerb boxes (the only invented geometry; Bridge_part_1's railing
    floats above its own deck). Revisit if the art gains a kerb.
  · 7% of hole 12's second deck sits below the terrain, reading as a shallow trench at the
    abutment. Art placement, cosmetic.
  · Risk 1 (nothing passes under a deck) is inherent to the 2.5D height field; measured benign
    on all five holes.
