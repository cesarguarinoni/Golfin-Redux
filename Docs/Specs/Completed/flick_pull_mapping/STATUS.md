DONE

- 2026-09-07 iter-1 — Flick's power is measured from the club's REST, not the cone base:
  `FlickPullMath` (new, `Golfin.Gameplay.Input`), `FlickPull100Px` 540 / `FlickPull120Px` 648,
  `FlickHandleStartY01` 0.6818 → 0.8182 so the base IS 120%. `ShotConeView` draws the club through
  the exact inverse.
- 2026-09-07 iter-2 — **Cesar: "Remove the 100% and 120% lines, leave only the labels."** Done. The
  two drawn rules are gone from the scene; `FlickConeLabelsBuilder` (renamed from
  `FlickConeTicksBuilder`) deletes any it finds, so an iter-1 scene is repaired by re-running it.
  The two labels keep their heights, their derivation from the two config keys, their putt rule and
  their alpha. `FlickPullMath.Tick*YPx` → `Mark*YPx`. Nothing about the pull mapping changed.
- Live acceptance through the REAL dragger on Lomond hole 2 @1170×2532:
  **24 assertions, 0 fail** (`evidence/flick_pull_mapping_invariants.json`) — including
  `the_tick_lines_are_gone`, which checks the live hierarchy rather than an active flag. Gauge reads
  `0% / 0% / 50% / 100% / 120% / 120%` at 0 / 40 / 290 / 540 / 648 / 700px; club meets finger to
  0.00px; rest −447.83; `100%` label −987.84, `120%` label −1095.84 (= the cone base); label x
  109.09 / 123.77, i.e. 16px outside the cone's edge at each height.
- EditMode **2796 / 2793 pass / 0 fail / 3 pre-existing skips** (re-run after iter-2).
  `bot_difficulty.csv` zero diff. `Assets/Scripts/Physics/` untouched. Three Flick tiles re-captured
  a second time, nine byte-identical to HEAD, all twelve `.meta` untouched.
- Scene diff is **281+/1−, zero `m_IsActive: 0`, and mine only** — a concurrent `selector_carousel`
  session renamed `SelectorOverlayWidget._cardsContainer`, so the save re-serialized two of its
  components; both blocks were restored to HEAD by hand-patch.
- Both iter-1 open questions are closed by the line removal. Two items still need Cesar on device:
  the 0%-at-touch feel and the 120% reach.

- 2026-09-07 — **Cesar approved.** Folder moved to `Docs/Specs/Completed/`.
