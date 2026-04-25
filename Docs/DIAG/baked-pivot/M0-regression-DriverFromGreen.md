# M0 Regression — DriverFromGreen

- Origin GO: `Green_1`
- Centroid (world XZ): (-230.320, -73.275)
- Ground Y at centroid (BakedHeightProvider): 10.120
- Invariant tolerance: 0.050 m
- Provider: BakedHeightProvider + BakedZoneClassifier (M3)

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 4.386 | 710 | HitOOB |
| NE | 45 | PASS | - | 0.000 | 0.000 | 8.992 | 3054 | BallStopped |
| E | 90 | FAIL | 233 | 17.902 | 17.974 | 10.140 | 14401 | MaxDurationReached |
| SE | 135 | FAIL | 336 | 17.891 | 17.967 | 10.140 | 14401 | MaxDurationReached |
| S | 180 | PASS | - | 0.000 | 0.000 | 10.140 | 4295 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 9.992 | 3350 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 4.515 | 3366 | BallStopped |
| NW | 315 | PASS | - | 0.000 | 0.000 | 0.861 | 2293 | BallStopped |
