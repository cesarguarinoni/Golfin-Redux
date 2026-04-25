# M0 Regression — DriverFromBunker

- Origin GO: `Bunker_1`
- Centroid (world XZ): (-129.780, -34.965)
- Ground Y at centroid (BakedHeightProvider): 5.729
- Invariant tolerance: 0.050 m
- Provider: BakedHeightProvider + BakedZoneClassifier (M3)

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 5.749 | 583 | HitOOB |
| NE | 45 | FAIL | 548 | 10.981 | 11.037 | 5.749 | 7693 | MaxDurationReached |
| E | 90 | FAIL | 11 | 6.401 | 6.493 | 5.749 | 462 | HitOOB |
| SE | 135 | FAIL | 6 | 6.108 | 6.230 | 5.749 | 14401 | MaxDurationReached |
| S | 180 | FAIL | 10 | 6.343 | 6.401 | 5.749 | 7582 | MaxDurationReached |
| SW | 225 | PASS | - | 0.000 | 0.000 | 4.516 | 630 | HitOOB |
| W | 270 | FAIL | 17 | 6.744 | 6.797 | 5.749 | 3350 | BallStopped |
| NW | 315 | FAIL | 13 | 6.516 | 6.612 | 5.749 | 707 | HitOOB |
