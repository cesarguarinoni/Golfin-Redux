IMPLEMENTER_BLOCKED

Built directly by Claude Code (main thread) on Cesar's instruction, not via the subagent chain —
so no SELF_REVIEW / ARCHITECT_REVIEW pass has been run and none is claimed.

Everything in SPEC §10 is verified except three items, all of which need an UNCONTENDED Unity
Editor. This Editor was shared with the GPS-hub and banners sessions throughout (74 `GpsHub*`
script-executes, repeated play-mode entry, and finally an additive load of every `Hole_NN_Geo`
scene), and I did not force play mode off or save any scene.

Open:
  1. Play-mode Balls carousel + detail panel EN/JA. NOTE: "carousel shows 20 entries" is not
     reachable as specified — `BallCarouselController` builds from `GetAllOwnedBallIds()`, so it
     is an inventory view, not a catalog view. It needs the balls GRANTED first.
  2. `Golfin.Gameplay.Tests` assembly run (the only change there is a deleted test method).
  3. SPEC §7 device-resolution thumbnail check — NEEDS CESAR'S EYES. 18 of 20 balls now feed a
     1000x1000 sprite into a carousel that has only ever been shown 200x200 / 178x178.

Already live and NOT revertable by a STATUS change: `balls` published v6 -> v7 and `texts`
v20 -> v21 (SPEC §6 step 3, explicitly instructed).

See IMPLEMENTER_REPORT.md.
