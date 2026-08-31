READY_FOR_ARCHITECT_REVIEW

Iteration 3 — SPEC Stage A + B + C on hole 7, plus Cesar's two decisions taken (2026-08-31).

DONE this iteration:
  · Bots + bridges — VersusBot.IsAvoidSurface now includes Bridge. Being absent from
    IsPlayableSurface only stopped bots TARGETING a deck; IsAvoidSurface is what the H2
    hazard/lay-up path reads. Decision 2 is now actually implemented. One edit to VersusBot,
    which the SPEC lists out of scope — authorised explicitly by Cesar.
  · Railing boxes for bridgeLODs.fbx (holes 8 x2, 9) — SPEC Risk 2 closed. NOT as a prefab
    variant: Assets/Packs/ is gitignored, so a variant there would never leave this machine.
    BridgeObstacleBaker falls back to renderer AABBs per railing/pier member, into the tracked
    bridge_obstacles.csv. Verified on the real FBX: 12 boxes, railing inner faces +/-2.26 m
    inside deck edges +/-2.404 m, contained both sides. Hole 7 re-bake byte-identical.
  · Hole 12 explained in IMPLEMENTER_REPORT (no deadlock possible; tilt and Risk 1 are what it
    will actually exercise).
  · Tree drift gate 18/18 PASS. 22 EditMode assemblies, 1962 passed, 0 failed, 3 pre-existing
    skips. All five holes' tree_obstacles.csv and heightmap.bytes md5-identical to baseline.

Committed at the end of iteration 3.

REMAINING: holes 8 / 9 / 12 / 17 — Stage A+B+C each, then the hole-12 tilt + Risk 1 report.
Still open for Cesar: the feel pass on the deck coefficients (0.45/0.35/0.12/0.10) and the part
coefficients (railing 0.35/0.75, pier 0.45/0.85).
