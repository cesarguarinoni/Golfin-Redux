# Regression — DriverFromGreen

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 4.298 | 743 | HitOOB |
| NE | 45 | PASS | - | 0.000 | 0.000 | 8.752 | 3433 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 10.181 | 450 | HitOOB |
| SE | 135 | PASS | - | 0.000 | 0.000 | 10.181 | 326 | HitOOB |
| S | 180 | PASS | - | 0.000 | 0.000 | 10.181 | 3576 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 9.992 | 3718 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 4.515 | 3728 | BallStopped |
| NW | 315 | PASS | - | 0.000 | 0.000 | 0.366 | 2660 | BallStopped |
