# M0 Regression — WedgeFromBunkerEdge

- Origin GO: `Bunker_1`
- Centroid (world XZ): (-129.780, -34.965)
- Ground Y at centroid (BakedHeightProvider): 5.668
- Invariant tolerance: 0.050 m
- Provider: BakedHeightProvider + BakedZoneClassifier (M3)

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 5.605 | 2430 | BallStopped |
| NE | 45 | PASS | - | 0.000 | 0.000 | 5.705 | 2379 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 6.185 | 3205 | BallStopped |
| SE | 135 | FAIL | 1190 | 14.830 | 14.891 | 6.337 | 8361 | MaxDurationReached |
| S | 180 | FAIL | 1164 | 11.821 | 11.926 | 6.082 | 8046 | MaxDurationReached |
| SW | 225 | PASS | - | 0.000 | 0.000 | 5.743 | 1707 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 2.790 | 961 | HitOOB |
| NW | 315 | PASS | - | 0.000 | 0.000 | 3.730 | 948 | HitOOB |
