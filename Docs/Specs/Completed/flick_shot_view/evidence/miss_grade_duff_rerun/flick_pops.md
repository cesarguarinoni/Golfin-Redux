# miss_grade_duff — Flick grade pops + gauge DUFF flash, live in play mode

Driven through the REAL ClubHandleDragger pointer handlers after booting
ShellScene and tapping StartButton -> PLAY -> a hole card.

| band | latch t | grade | key | word | colour | pop | timingMul | isMiss | gauge | png |
|---|---|---|---|---|---|---|---|---|---|---|
| duff | 0.029 | Duff | SHOT_GRADE_DUFF | "DUFF" | #FF5A5A | alpha 1.00 | 0.400 | True | alpha=1.00 arcOverride=#FF3B3B progress=0.220 pct='22%' pctColor=#FF3B3B | `Docs/Specs/Active/flick_shot_view/screenshots/band_duff_792cone.png` |
| thin | 0.392 | Thin | SHOT_GRADE_THIN | "THIN" | #FFEBA6 | alpha 1.00 | 0.861 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/flick_shot_view/screenshots/band_thin_792cone.png` |
| good | 0.519 | Good | SHOT_GRADE_GOOD | "GOOD" | #FFEBA6 | alpha 1.00 | 0.917 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/flick_shot_view/screenshots/band_good_792cone.png` |
| pure | 0.880 | Pure | SHOT_GRADE_PURE | "PURE" | #ADEBAD | alpha 1.00 | 1.000 | False | alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF | `Docs/Specs/Active/flick_shot_view/screenshots/band_pure_792cone.png` |

## Log
- hole: 1
- resolution: 1170x2532
- driver: real ClubHandleDragger IPointerDown/IDrag/IPointerUp events
- pop_found: FlickGradePop
- gauge_found: PowerHUD
- cone_bands: red=0.15 gold=0.45 green=0.85
- duff_a1_latched_timing01: 0.029
- duff_ACCEPTED: latch=0.029 grade=Duff key=SHOT_GRADE_DUFF word='DUFF' color=#FF5A5A popAlpha=1.00 mul=0.400 isMiss=True power=0.55 gauge=[alpha=1.00 arcOverride=#FF3B3B progress=0.220 pct='22%' pctColor=#FF3B3B] png=Docs/Specs/Active/flick_shot_view/screenshots/band_duff_792cone.png (3231 KB, md5 960fc1d0)
- thin_a1_latched_timing01: 0.392
- thin_ACCEPTED: latch=0.392 grade=Thin key=SHOT_GRADE_THIN word='THIN' color=#FFEBA6 popAlpha=1.00 mul=0.861 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/flick_shot_view/screenshots/band_thin_792cone.png (2610 KB, md5 4c07f749)
- good_a1_latched_timing01: 0.519
- good_ACCEPTED: latch=0.519 grade=Good key=SHOT_GRADE_GOOD word='GOOD' color=#FFEBA6 popAlpha=1.00 mul=0.917 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/flick_shot_view/screenshots/band_good_792cone.png (2915 KB, md5 73538b00)
- pure_a1_latched_timing01: 0.880
- pure_ACCEPTED: latch=0.880 grade=Pure key=SHOT_GRADE_PURE word='PURE' color=#ADEBAD popAlpha=1.00 mul=1.000 isMiss=False power=0.55 gauge=[alpha=0.00 arcOverride=none progress=0.550 pct='55%' pctColor=#FFFFFF] png=Docs/Specs/Active/flick_shot_view/screenshots/band_pure_792cone.png (4077 KB, md5 213c3980)
