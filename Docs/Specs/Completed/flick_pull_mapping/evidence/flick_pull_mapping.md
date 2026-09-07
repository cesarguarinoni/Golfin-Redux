# flick_pull_mapping — live pull measurements

Booted ShellScene -> StartButton -> PLAY -> hole card, then drove the REAL
ClubHandleDragger pointer handlers. Every number read off live rects and the
power gauge's own text in play mode.

| pull | gauge text | PowerNormalized | expected | club y | finger y | delta |
|---|---|---|---|---|---|---|
| touch (0px) | 0% | 0.0000 | 0.00 | -447.83 | -447.83 | +0.00 |
| dead zone (40px) | 0% | 0.0000 | 0.00 | -447.83 | -487.83 | +40.00 |
| half (290px) | 50% | 0.5000 | 0.50 | -737.83 | -737.83 | +0.00 |
| 100% (540px) | 100% | 1.0000 | 1.00 | -987.83 | -987.83 | +0.00 |
| 120% (648px) = the base | 120% | 1.2000 | 1.20 | -1095.83 | -1095.83 | +0.00 |
| past the base (700px) | 120% | 1.2000 | 1.20 | -1095.83 | -1095.84 | +0.01 |

| assertion | verdict | detail |
|---|---|---|
| rest_is_pull120_above_the_base | PASS | rest 648.01px above the base vs FlickPull120Px 648 |
| rest_canvas_y_448 | PASS | rest at canvas y -447.83 (spec: -447.84 = base -1095.84 + 648) |
| club_sits_144_under_the_ball | PASS | ball -303.84 - rest -447.83 = 143.99px |
| label100_canvas_y_988 | PASS | 100% label centred at -987.84 (spec -987.84) |
| label120_canvas_y_1096 | PASS | 120% label centred at -1095.84 (spec -1095.84) |
| label120_is_at_the_cone_base | PASS | 120% label -1095.84 vs cone base -1095.84 |
| label_gap_is_pull120_minus_pull100 | PASS | 108.00px apart vs 648-540 = 108 |
| the_tick_lines_are_gone | PASS | no Tick100/Tick120 GameObject survives under ConeMesh |
| power_at_0px | PASS | touch (0px): gauge '0%', PowerNormalized 0.0000 (expected 0.00) |
| gauge_reads_zero_the_instant_the_club_is_touched | PASS | on pointer-down, before any drag: gauge '0%', power 0.0000 (was 0.318 under the base-relative mapping) |
| power_at_40px | PASS | dead zone (40px): gauge '0%', PowerNormalized 0.0000 (expected 0.00) |
| power_at_290px | PASS | half (290px): gauge '50%', PowerNormalized 0.5000 (expected 0.50) |
| club_meets_finger_at_290px | PASS | half (290px): club -737.83 vs finger -737.83 (0.00px apart) |
| power_at_540px | PASS | 100% (540px): gauge '100%', PowerNormalized 1.0000 (expected 1.00) |
| club_meets_finger_at_540px | PASS | 100% (540px): club -987.83 vs finger -987.83 (0.00px apart) |
| power_at_648px | PASS | 120% (648px) = the base: gauge '120%', PowerNormalized 1.2000 (expected 1.20) |
| club_meets_finger_at_648px | PASS | 120% (648px) = the base: club -1095.83 vs finger -1095.83 (0.00px apart) |
| power_at_700px | PASS | past the base (700px): gauge '120%', PowerNormalized 1.2000 (expected 1.20) |
| club_meets_finger_at_700px | PASS | past the base (700px): club -1095.83 vs finger -1095.84 (0.01px apart) |
| lateral_aim_still_reaches_plus_one_at_100pct | PASS | finetune right 1.000 at maxX 93.1px |
| lateral_aim_still_reaches_minus_one_at_100pct | PASS | finetune left -1.000 |
| putt_base_reads_100pct | PASS | gauge '100%', power 1.0000 at the base |
| putt_hides_the_120_label | PASS | Label120 active=False |
| putt_keeps_the_100_label | PASS | Label100 active=True |

## Log
- hole: 2
- resolution: 1170x2532
- canvas: 1170 x 2532
- scheme: Flick
- gauge_text_bound: PctText
- flick_pull_000_rest_png: Docs/Specs/Active/flick_pull_mapping/screenshots/flick_pull_000_rest.png (3612 KB, md5 0ba1deae)
- flick_pull_540_100pct_png: Docs/Specs/Active/flick_pull_mapping/screenshots/flick_pull_540_100pct.png (3602 KB, md5 251b0cee)
- flick_pull_648_120pct_png: Docs/Specs/Active/flick_pull_mapping/screenshots/flick_pull_648_120pct.png (3598 KB, md5 3874532d)
- flick_pull_putt_base_png: Docs/Specs/Active/flick_pull_mapping/screenshots/flick_pull_putt_base.png (3596 KB, md5 9bdf8b6d)
