# Regression — DriverFromGreen

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 4.300 | 742 | HitOOB |
| NE | 45 | PASS | - | 0.000 | 0.000 | 8.753 | 3432 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 10.144 | 453 | HitOOB |
| SE | 135 | PASS | - | 0.000 | 0.000 | 10.144 | 325 | HitOOB |
| S | 180 | PASS | - | 0.000 | 0.000 | 10.144 | 3575 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 9.992 | 3716 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 4.515 | 3729 | BallStopped |
| NW | 315 | PASS | - | 0.000 | 0.000 | 0.368 | 2658 | BallStopped |
