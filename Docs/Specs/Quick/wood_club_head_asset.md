# Quick spec — Wood club head asset uses Driver model

## Problem
Selecting the Wood preset/club in the lab (and likely anywhere the Wood
club-head is referenced) shows the Driver club-head mesh/sprite instead of a
distinct Wood mesh.

## Source
Spotted by Cesar during play verification of
`controls_h_chase_camera_regression` on 2026-05-08. Filed as Quick to keep
that task scoped to the chase-camera regression.

## Likely root cause
Asset wiring in one of:
- `Assets/Data/Clubs.csv` — Wood row points at the Driver sprite/mesh path.
- The `Wood` ClubData / `ClubDataRuntime` field references the wrong sprite.
- The Wood preset prefab in the physics lab references the Driver model.

## What to do
1. Pick a Wood preset and reproduce the bug in lab playmode.
2. Inspect the Wood club row in `Clubs.csv` and compare its sprite/mesh column
   against the Driver row. They should not be identical.
3. If a distinct Wood asset exists in `Assets/Resources/`, fix the CSV / prefab
   reference. If no distinct asset exists, file a follow-up note that an asset
   needs to be authored.
4. Re-verify by selecting Wood in the lab — the club head should look visually
   distinct from Driver.

## Out of scope
Driver / Iron / Wedge / Putter wiring (only check Wood).
