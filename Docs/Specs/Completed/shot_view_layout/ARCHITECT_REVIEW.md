# Architect Review — `shot_view_layout`

**Verdict: PASS** (2026-09-07, Architect, Cowork). Two items are Cesar's to settle before DONE; neither is a code defect.

## What was checked

- Commits `92985eaa3` + `28904bac8` (`git show --stat`, `ShotLayoutMath.cs` read in full). Pure math, no camera code touched, layout applied from `ShotSchemeHost.Apply` before `Activate` — as specced.
- Measured numbers in the report against SPEC §2: ball −303.84 (vy 0.3800), 120 % handle −1021.84, buttons −1096 / 434, overlays 170, `PowerHUD` centre vy 0.7000, Needle rings unchanged, horizon 25.5 % → 37.8 %. All on target.
- Flick parity: 17 re-parented rects, `maxCornerDelta = 0.000000`; four parity test files unmodified; `AimCameraFramingTests` green. D1 holds at the YAML level too (all `BallSpace` offsets authored at 0).
- Tests: 2724 run / 2721 pass / 0 fail / 3 pre-existing skips; 10 new tests.
- Deviations: all six are justified and improve on the spec (stretch `BallSpace`, `EnsureAndClear` so builders survive re-runs, D3 reading of the clamp approved by Cesar, `NudgeAimCamera` via the iter-35 reflection idiom, `PhysicsLab_Hole1` confirmed legacy with evidence, no test literals to update). The spec was wrong about the club overlay being at 96 (it was 28) — the report says so and the fix (both on the baseline) is what D2 meant.

## Open for Cesar (not blockers to the code)

1. **The lane's tail now hangs 96 px below the shared baseline** (ends at −1191.84, 74 px above the screen edge). Cause: `ClubHalfHeight` went 50 → 150 in the same-day control-scheme polish (3× club head), and the clamp — correctly, per D3 and your "option 4" — guards the finger, not the pill's end. The pill is 120 px wide down the centre and touches no button, but "lane end = button bottoms" (D2) is no longer literally true. Options: (a) accept as is; (b) cap the drawn pill at the baseline (`LaneTailPx` shrinks to whatever fits; the 3× head overhangs the pill's end at full pull); (c) shorten `Pull120Px` by 96 (→ 552, `Pull100Px` 460) so the pill ends on the baseline with the finger 74 px higher — costs 14 % of the pull length just gained. Architect recommends **(b)**: keeps D3's clearance and the new pull length, restores the alignment you asked for, and the head overhanging a rounded pill reads fine.
2. **Confirm tiles for Pendulum / Needle / Free Swing are still the OLD layout** (recapture reverted: the width-driven `MaxCropW = 900` auto-crop clips the 888 px lane). Options in the report: raise `MaxCropW` (lets HUD columns into the tile), crop on the lane alone, or shrink the subject. Architect recommends **crop on the lane alone** — height-driven crop = the lane's bounding box + margin, width whatever fits ≤ 900 — because the tile's job is to show the gesture, not the HUD. Either way this is a small follow-up (`miss_grade_duff` does not touch tiles, so it can ride alongside).

## Carry-forward

- `miss_grade_duff` runs next; its `FlickGradePop` goes under `SchemeRoot_Flick/BallSpace`, which now exists.
- Backlog rows from SPEC §5 were filed at spec time; add the tile-crop follow-up when Cesar picks.
