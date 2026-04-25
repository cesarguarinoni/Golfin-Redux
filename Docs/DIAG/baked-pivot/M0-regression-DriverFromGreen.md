# Regression — DriverFromGreen

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 4.386 | 710 | HitOOB |
| NE | 45 | PASS | - | 0.000 | 0.000 | 8.992 | 3054 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 10.144 | 525 | HitOOB |
| SE | 135 | PASS | - | 0.000 | 0.000 | 10.144 | 336 | HitOOB |
| S | 180 | PASS | - | 0.000 | 0.000 | 10.144 | 4296 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 9.992 | 3350 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 4.515 | 3366 | BallStopped |
| NW | 315 | PASS | - | 0.000 | 0.000 | 0.861 | 2293 | BallStopped |
