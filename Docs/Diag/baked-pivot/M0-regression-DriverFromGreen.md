# Regression — DriverFromGreen

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 4.298 | 743 | HitOOB |
| NE | 45 | PASS | - | 0.000 | 0.000 | 9.014 | 865 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 10.181 | 1802 | HitOOB |
| SE | 135 | PASS | - | 0.000 | 0.000 | 10.181 | 326 | HitOOB |
| S | 180 | PASS | - | 0.000 | 0.000 | 10.181 | 540 | HitOOB |
| SW | 225 | PASS | - | 0.000 | 0.000 | 9.977 | 643 | HitOOB |
| W | 270 | PASS | - | 0.000 | 0.000 | 4.500 | 740 | HitOOB |
| NW | 315 | PASS | - | 0.000 | 0.000 | 0.867 | 794 | HitOOB |
