# miss_grade_duff — Flick grade pops + gauge DUFF flash, live in play mode

Driven through the REAL ClubHandleDragger pointer handlers after booting
ShellScene and tapping StartButton -> PLAY -> a hole card.

| band | latch t | grade | key | word | colour | pop | timingMul | isMiss | gauge | png |
|---|---|---|---|---|---|---|---|---|---|---|
| duff | 0.033 | Duff | SHOT_GRADE_DUFF | "DUFF" | #FF5A5A | alpha 1.00 | 0.400 | True | alpha=1.00 arcOverride=#FF3B3B progress=0.220 pct='22%' pctColor=#FF3B3B | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_duff.png` |
| thin | 0.334 | Thin | SHOT_GRADE_THIN | "THIN" | #FFEBA6 | alpha 1.00 | 0.823 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_thin.png` |
| good | 0.514 | Good | SHOT_GRADE_GOOD | "GOOD" | #FFEBA6 | alpha 1.00 | 0.916 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_good.png` |
| pure | 0.910 | Pure | SHOT_GRADE_PURE | "PURE" | #ADEBAD | alpha 1.00 | 1.000 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/miss_grade_duff/screenshots/flick_pure.png` |

## Log
- hole: 1
- resolution: 1170x2532
- driver: real ClubHandleDragger IPointerDown/IDrag/IPointerUp events
- pop_found: FlickGradePop
- gauge_found: PowerHUD
- cone_bands: red=0.15 gold=0.45 green=0.85
- duff_a1_latched_timing01: 0.033
- duff_ACCEPTED: latch=0.033 grade=Duff key=SHOT_GRADE_DUFF word='DUFF' color=#FF5A5A popAlpha=1.00 mul=0.400 isMiss=True power=0.55 gauge=[alpha=1.00 arcOverride=#FF3B3B progress=0.220 pct='22%' pctColor=#FF3B3B] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_duff.png (3168 KB, md5 c08c625c)
- thin_a1_latched_timing01: 0.334
- thin_ACCEPTED: latch=0.334 grade=Thin key=SHOT_GRADE_THIN word='THIN' color=#FFEBA6 popAlpha=1.00 mul=0.823 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_thin.png (2936 KB, md5 4b7570cb)
- good_a1_latched_timing01: 0.514
- good_ACCEPTED: latch=0.514 grade=Good key=SHOT_GRADE_GOOD word='GOOD' color=#FFEBA6 popAlpha=1.00 mul=0.916 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_good.png (3021 KB, md5 ceecd04b)
- pure_a1_latched_timing01: 0.910
- pure_ACCEPTED: latch=0.910 grade=Pure key=SHOT_GRADE_PURE word='PURE' color=#ADEBAD popAlpha=1.00 mul=1.000 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/miss_grade_duff/screenshots/flick_pure.png (3091 KB, md5 9ac1414b)
