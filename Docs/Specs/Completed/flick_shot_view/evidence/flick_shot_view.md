# flick_shot_view — live framing measurements

Booted ShellScene -> StartButton -> PLAY -> hole card. Every number read off
live rects in play mode; the BEFORE column is the same session with the ball
widget put back on viewport 0.5 and the aim camera re-posed.

| assertion | verdict | detail |
|---|---|---|
| ball_at_038 | PASS | ball y -303.84 (vy 0.3800) |
| cone_height_792 | PASS | cone height 792.0 |
| cone_apex_touches_the_ball | PASS | apex -303.84 vs ball -303.84 — gap 0.00px |
| cone_base_1096 | PASS | base -1095.84 |
| base_on_baseline | PASS | base -1095.84 vs baseline -1096.00 |
| handle_rest_556 | PASS | handle rest -555.85 |
| pull_travel_matches_pendulum | PASS | rest-to-100% travel 540.0px vs PendulumPull100Px 540 and FreeSwingPull100Px 540 (was 960 on the old 1160 cone) |
| cone_clears_buttons | PASS | half-base@20 288.3 < button inner edge 382.0 |
| horizon_matches_accepted_framing | PASS | horizon 34.24% vs 34.28% on shot_view_layout's accepted pendulum_038_hole2.png measured by this same ruler (that task quotes 37.8% for those pixels) — 18 columns, median row 867 of 2532 |
| horizon_gained_over_the_old_anchor | PASS | 21.72% -> 34.24% (+12.52 points) |
| camera_pitched_up | PASS | 12.500deg -> 4.612deg |
| camera_did_not_move | PASS | (122.05, 15.40, -134.25) -> (122.05, 15.40, -134.25) (the ball anchor is the ONLY thing that changed) |
| putt_track_top_touches_the_ball | PASS | track top -303.84 vs ball -303.84 — gap 0.00px |
| putt_track_bottom_1096 | PASS | track bottom -1095.84 |
| putt_track_height_792 | PASS | track height 792.0 — the same 792 the cone drops |
| switch_same_framing | PASS | Flick -303.84 vs Pendulum -303.84 |
| switch_no_camera_pop | PASS | pitch Flick 4.612 vs Pendulum 4.612 |
| switch_round_trip | PASS | back to Flick -303.84 |

## Log
- hole: 2
- resolution: 1170x2532
- canvas: 1170 x 2532
- scheme: Flick
- flickview_camera_before_vy050_png: Docs/Specs/Active/flick_shot_view/screenshots/camera_before_vy050.png (3780 KB, md5 91c12f35)
- flickview_flick_038_hole2_png: Docs/Specs/Active/flick_shot_view/screenshots/flick_038_hole2.png (3578 KB, md5 7eb2affe)
- pull_handle_y: -852.85
- flickview_pull_55pct_png: Docs/Specs/Active/flick_shot_view/screenshots/flick_pull_792cone.png (3560 KB, md5 26d59888)
- pull_frame_state_after_release: handle back at -555.85
