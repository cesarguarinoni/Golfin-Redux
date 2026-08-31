READY_FOR_ARCHITECT_REVIEW

Iteration 4 — ALL 7 BRIDGES DONE. Holes 7, 8, 9, 12, 17; Stages A + B + C each.

Every instance in the SPEC ground-truth table is transplanted at max |dpos|/dangle/dscale =
0.000000 against the Video source, decked as a SurfaceType.Bridge zone, and obstacle-baked.
Containment: 8/8 perpendicular rolls per bridge, 56 shots, 56 contained.

Found and fixed this iteration (both only reachable through hole 12's x-tilt):
  · the deck slab itself was being filed as a railing on tilted bridges — a solid block the full
    length and width of the walkway;
  · the per-corner deck sample fell through to the deck's mean Y on the footprint boundary, which
    reintroduced the same error.
  Regression proof: holes 7/8/9 (level decks) re-bake BYTE-IDENTICAL through both fixes.

Also: Bridge_part_1 (hole 17) does not seal its own deck — its railing floats 0.10-0.17 m above
the walkway and a 43 mm ball rolled under it, 0 of 8 rolls contained. Repaired with 2 SYNTHESIZED
kerb boxes. That is the only invented geometry in the whole task and it is flagged as such.

Risk 1 measured on all five holes: real but benign. Holes 8/9/17 have under 1.8 m of clearance
(nobody plays a ball under those); holes 7 and 12 have real height but span water, so a ball that
"should" have passed underneath was drowning anyway. Separate cosmetic finding: 7% of hole 12's
second deck sits below the terrain, reading as a shallow trench at the abutment.

Gates: tree drift 18/18 PASS. 22 EditMode assemblies, 1962 passed, 0 failed, 3 pre-existing skips.
All five holes' tree_obstacles.csv and heightmap.bytes md5-identical to the session baseline.

OPEN FOR CESAR — the feel pass: deck coefficients (0.45/0.35/0.12/0.10) and part coefficients
(railing 0.35/0.75, pier 0.45/0.85). Plus two judgement calls flagged in IMPLEMENTER_REPORT:
the hole-17 synthesized kerbs, and hole 12's buried abutment.
