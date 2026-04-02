# Lomond Notes

## Official Source

- Course guide: <https://www.lomond-cc.com/course/>

## Facts Captured from the Official Page

- Course name: `ローモンドカントリー倶楽部`
- English label used in this repo: `Lomond Country Club`
- Address: `2570-3 Ryoocho, Kameyama, Mie 519-0222, Japan`
- Holes: `18`
- Par: `72`
- Grass note: `Bentgrass one-green course`
- Open date on site: `1997-10-08`
- Designer: `Taizo Kawata`

## Data Conflict

The official page contains a yardage inconsistency:

- Course overview says: `7,028 yards`
- Hole-by-hole back-tee totals sum to: `7,024 yards`

The intake app should:

- Preserve both values
- Mark the discrepancy
- Require an operator resolution or note

## Known Hole Values from the Official Page

### OUT

- Hole 1: par 5, hdcp 9, back 531, regular 509, front 488, ladies 458
- Hole 2: par 4, hdcp 3, back 403, regular 391, front 378, ladies 366
- Hole 3: par 4, hdcp 15, back 368, regular 349, front 315, ladies 285
- Hole 4: par 3, hdcp 13, back 138, regular 126, front 124, ladies 106
- Hole 5: par 4, hdcp 1, back 425, regular 383, front 372, ladies 366
- Hole 6: par 3, hdcp 7, back 193, regular 173, front 149, ladies 129
- Hole 7: par 4, hdcp 5, back 430, regular 410, front 375, ladies 335
- Hole 8: par 5, hdcp 17, back 562, regular 480, front 469, ladies 452
- Hole 9: par 4, hdcp 11, back 434, regular 403, front 376, ladies 357

### IN

- Hole 10: par 4, hdcp 10, back 392, regular 368, front 346, ladies 328
- Hole 11: par 3, hdcp 16, back 179, regular 162, front 138, ladies 120
- Hole 12: par 4, hdcp 4, back 435, regular 359, front 348, ladies 269
- Hole 13: par 5, hdcp 8, back 597, regular 559, front 534, ladies 508
- Hole 14: par 4, hdcp 2, back 420, regular 392, front 373, ladies 357
- Hole 15: par 3, hdcp 14, back 184, regular 169, front 146, ladies 121
- Hole 16: par 4, hdcp 18, back 356, regular 327, front 316, ladies 292
- Hole 17: par 4, hdcp 6, back 410, regular 379, front 353, ladies 328
- Hole 18: par 5, hdcp 12, back 567, regular 548, front 521, ladies 502

## Extraction Notes

The official page exposes hole-specific clickable links for holes `1` through `18`. The intake scraper should resolve and download each linked hole-detail image and store it as a raw source artifact before any georeferencing or segmentation.
