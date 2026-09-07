# miss_grade_duff — Flick grade pops + gauge DUFF flash, live in play mode

Driven through the REAL ClubHandleDragger pointer handlers after booting
ShellScene and tapping StartButton -> PLAY -> a hole card.

| band | latch t | grade | key | word | colour | pop | timingMul | isMiss | gauge | png |
|---|---|---|---|---|---|---|---|---|---|---|
| duff | 0.028 | Duff | SHOT_GRADE_DUFF | "DUFF" | #FF5A5A | alpha 1.00 | 0.200 | True | alpha=1.00 arcOverride=#FF3B3B progress=0.110 pct='11%' pctColor=#FF3B3B | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_duff.png` |
| thin | 0.202 | Thin | SHOT_GRADE_THIN | "THIN" | #FFEBA6 | alpha 1.00 | 0.734 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_thin.png` |
| good | 0.499 | Good | SHOT_GRADE_GOOD | "GOOD" | #FFEBA6 | alpha 1.00 | 0.912 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_good.png` |
| pure | 0.881 | Pure | SHOT_GRADE_PURE | "PURE" | #ADEBAD | alpha 1.00 | 1.000 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_pure.png` |

## Log
- hole: 1
- resolution: 1170x2532
- driver: real ClubHandleDragger IPointerDown/IDrag/IPointerUp events
- pop_found: FlickGradePop
- gauge_found: PowerHUD
- cone_bands: red=0.15 gold=0.45 green=0.85
- duff_a1_latched_timing01: 0.028
- duff_ACCEPTED: latch=0.028 grade=Duff key=SHOT_GRADE_DUFF word='DUFF' color=#FF5A5A popAlpha=1.00 mul=0.200 isMiss=True power=0.55 gauge=[alpha=1.00 arcOverride=#FF3B3B progress=0.110 pct='11%' pctColor=#FF3B3B] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_duff.png (3347 KB, md5 0cf3243a)
- thin_a1_latched_timing01: 0.202
- thin_ACCEPTED: latch=0.202 grade=Thin key=SHOT_GRADE_THIN word='THIN' color=#FFEBA6 popAlpha=1.00 mul=0.734 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_thin.png (3643 KB, md5 704831f1)
- good_a1_latched_timing01: 0.499
- good_ACCEPTED: latch=0.499 grade=Good key=SHOT_GRADE_GOOD word='GOOD' color=#FFEBA6 popAlpha=1.00 mul=0.912 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_good.png (3394 KB, md5 07007dd0)
- pure_a1_latched_timing01: 0.881
- pure_ACCEPTED: latch=0.881 grade=Pure key=SHOT_GRADE_PURE word='PURE' color=#ADEBAD popAlpha=1.00 mul=1.000 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_pure.png (3747 KB, md5 d3260717)
