# Architect close-out — golfer_club_grip (2026-09-11)

**Re:** `IMPLEMENTER_REPORT.md` iter-9 / 9b. **Decision: §3.11 is accepted at the gameplay camera. Iter-9b (`ApplyHeldGripPose`) is switched off, not continued. The spec closes.**

## Why this is the end and not another round
- `evidence/final/gameplay_address.png` reads as a golfer at address with the head on the ball. That is the surface the player sees. The close-ups show the clip's fists side by side with the club behind them — true, and invisible at 40 px.
- Iter-9b is the same loop starting again: a close-up rejected → a new procedural pose path → "lead fingers still splay, a few bones touch" after four runs. The wrap in iter-5–8 went the same way. Procedural finger posing on this rig produces claws; that is now a measured fact, not a theory.
- Remy is a stand-in. Every hour on its fingers is discarded when the roster models land. The **club mount** transfers (two-hand average + authored slot pose); a **hand pose** is a per-model asset an artist authors in minutes with the club in hand — it is now a line in the roster brief (`CHARACTER_3D_REMAKE_OPTIONS.md` §7) and a backlog row, not a spec.

## What is accepted from iter-9
`Rig_Hands`/anchors deleted; `RigBuilder.layers = [Rig_Grip]`; the two authored slot poses; `club.faceSquare` gating edge **and** azimuth (the report's argument is right — an edge-only test passes a club mounted backwards); the roll solved as a one-parameter scan (147.55°, not the naive 174°); the harness braces fix that un-hid six rows; foot slide in band under the fixed step; A0 = 0. `budget.tris` stays the one FAIL, out of scope since §9.1.

## Not accepted / to undo
- `heldGripPose` → **false** on `PfGolfer_MixamoNative`; the `ApplyHeldGripPose` path stays in the file under the define (it is off, and `PfGolfer_Test` is untouched) — or drop the iter-9b commits if they are the last ones on the branch and nothing else rides on them. Code chooses whichever is the smaller diff and says which.
- The club position: **the §3.11 authored one** (below both fists, worst clearance 0.0205 m, head on ball 0.0085 m, face square), i.e. the state of the acceptance run, not any of the iter-9b placements.
- Profile: Cesar said stop restoring it on this machine — `iOS-Full-Golfer` stays active; A6 is retired for this task.

## Close-out steps
1. Code: flag off / revert, one confirmation run of the harness (A0, headAtBall, faceSquare, foot slide), `STATUS → READY_FOR_SELF_REVIEW`, commit.
2. Cesar approves on the three gameplay frames.
3. Code: `STATUS → DONE`, move `Docs/Specs/Active/golfer_club_grip/` (and `golfer_3d_test/` if still in Active) to `Docs/Specs/Completed/`, merge `golfer_3d_test` → `main`, push, `AI_CONTEXT.md`.
4. Backlog rows added; `CHARACTER_3D_REMAKE_OPTIONS.md` §7 carries the mount + the artist deliverable.
