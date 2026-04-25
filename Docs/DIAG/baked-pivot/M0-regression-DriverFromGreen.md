# M0 Regression — DriverFromGreen

- Origin GO: `Green_1`
- Centroid (world XZ): (-230.320, -73.275)
- Ground Y at centroid (SceneGroundProvider): 10.124
- Invariant tolerance: 0.050 m
- Classifier: SceneGroundProvider (current architecture)

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 4.386 | 710 | HitOOB |
| NE | 45 | PASS | - | 0.000 | 0.000 | 8.978 | 3054 | BallStopped |
| E | 90 | FAIL | 233 | 17.906 | 17.959 | 10.144 | 14401 | MaxDurationReached |
| SE | 135 | FAIL | 336 | 17.894 | 17.967 | 10.144 | 3324 | BallStopped |
| S | 180 | PASS | - | 0.000 | 0.000 | 0.000 | 3424 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 0.000 | 3485 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 0.000 | 3424 | BallStopped |
| NW | 315 | PASS | - | 0.000 | 0.000 | 0.000 | 3324 | BallStopped |
