# Surface Rollout Sweep Results

**Timestamp:** 2026-05-19T06:17:43.8094640+02:00
**Harness git SHA:** 596addd5
**Total captures:** 552
**Holes for Sub-modes 1a+1b:** 1, 9, 18
**Holes for Sub-mode 2:** 1, 9, 18

## Files
- `sweep.csv` — Sub-modes 1a (roll) + 1b (putt) captures
- `real_shots.csv` — Sub-mode 2 driver shots from tees
- `progress.log` — resume checkpoint file

## Config
- Sub-mode 1a: 9 surfaces × 7 speeds × 2 spins × 2 samples per hole = 252 rows/hole
- Sub-mode 1b: 3 surfaces × 7 speeds × 3 samples per hole = 63 rows/hole
- Sub-mode 2: 3 holes × 2 variants = 6 rows

## Iter-7 fixes
- Drop geometry restored: ball spawns at surfaceY+3m with -30° downward velocity.
- PlaceBallAtAirborne() bypass: GetCurrentOrigin() uses exact spawn Y.
- Sample jitter: sample_id=2 offsets spawn +0.1m in +X.
- Spin axis fixed: (0,0,1) for +X shots → upward Magnus lift.
- Draw shot fix: PlaceBallAtAirborne(teePos) before each real shot.
- source_hole column added to sweep.csv.
