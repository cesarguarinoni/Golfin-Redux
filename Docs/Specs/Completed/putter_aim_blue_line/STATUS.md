DONE

Task: putter_aim_blue_line (SPEC Rev 2)
Iteration: 1
Updated: 2026-08-10 (Cesar approved; moved to Completed)

Implemented directly by Claude Code at Cesar's request (Tier 2 / TellCode), not via the
golfin-implementer subagent chain. All §7 DoD items 1–8 verified — see IMPLEMENTER_REPORT.md.

Video: videos/putter_aim_blue_line_clip_hole6.mp4 — production Hole 6, real entry path, real
ShotController (no test seams). Line appears on aim, pivots through a ±35° sweep, vanishes on
the putt. 1170x2532 @ 30fps, 27.4s, captioned, flip- and motion-verified, cup confirmed visible.

Hole 6 rather than Hole 1 because Cesar caught that the cup never appears in the Hole 1 clip.
Measured across all 18 holes: Hole 1's cup disc sits 23.6 mm BELOW its green surface (buried);
holes 2-18 sit 1.3-6.4 mm proud and render. Not caused by the aim line — the disc is absent from
frames where the line and grid are both off. Root cause is HoleGeoImporter seating the cup on
pinSeatY + 1 mm; needs its own task. The Hole 1 clip is kept as the evidence.

Cesar approved 2026-08-10. Remaining, deliberately NOT blocking:
  • colour #7AE9FF and width 0.08 m are provisional (SPEC §7) — both live [SerializeField]s
  • on-device 60 fps confirmation (measured allocation-free in Editor; device capture is a bonus per §8.2)
