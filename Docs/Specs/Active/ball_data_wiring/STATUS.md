IMPLEMENTER_BLOCKED

Built directly by Claude Code (main thread) on Cesar's instruction, not via the subagent chain —
so no SELF_REVIEW / ARCHITECT_REVIEW pass has been run and none is claimed.

SPEC §7 is now CLOSED (2026-09-01, second pass). It was a real defect, but not the predicted one:
layout does not move (Portrait is a pinned 168x261 rect, measured identical with a 200px and a
1000px sprite), but the 1000x1000 thumbnails were a 5.95x downscale with NO mip chain -> aliasing,
on the Balls card (168px), the shot UI centre ball (150px) AND the ball button. Fixed per §7:
200x200 LANCZOS copies named <PascalName>.png, thumbnailSprite repointed for the 18, originals
untouched, §6 re-run (0 NEW / 18 CHANGED / 0 conflicts), published balls v7 -> v8, --check clean.
Worst-case downscale is now 1.19x, aliasing risk 0/20, layout still 168x261 / 170x343.

Two items remain, both needing an UNCONTENDED Unity Editor:
  1. Play-mode Balls carousel + detail panel EN/JA. NOTE: "carousel shows 20 entries" is not
     reachable as specified — BallCarouselController builds from GetAllOwnedBallIds(), so it is
     an inventory view, not a catalog view. It needs the balls GRANTED first.
  2. `Golfin.Gameplay.Tests` assembly run (the only change there is a deleted test method).

DONE (Cesar, "move them so they dont ship"): the 19 orphaned S_Controls_Ball_*.png have been
removed from Resources/. They were byte-identical duplicates of files already in
Assets/Art/Original UI/Ball Sprites/ (the documented home for source art not loaded at runtime),
so there was nowhere to move them TO — the redundant Resources/ copies were deleted and the Art/
masters left untouched. S_Controls_Ball_GOLFIN stays: it is the one that IS referenced (hardcoded
Resources.Load in CentralBallWidget/BallButtonWidget + a LabScaffold GUID ref). Resources/Balls
went 60 -> 41 png, -16.1 MB on disk / -72 MB uncompressed. 20/20 balls still resolve both sprites;
--check still clean at v8. No CSV change, no re-publish.

SEPARATE PRE-EXISTING FINDING, not actioned: a project-wide scan reports 86 broken Image.sprite
references (Bar, ChevronIcon, Map, BG, Handle) across ~15 prefabs in Gacha / Inventory /
HoleSelection / ModeSelect / Tournaments, pointing at GUIDs not in the project. Proven unrelated to
this task (zero prefabs modified here; zero references to any deleted GUID). Worth its own task.

Already live and NOT revertable by a STATUS change: balls published v6 -> v7 -> v8 and texts
v20 -> v21.

See IMPLEMENTER_REPORT.md item I.
