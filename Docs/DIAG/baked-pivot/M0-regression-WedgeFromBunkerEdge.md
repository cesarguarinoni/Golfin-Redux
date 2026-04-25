# Regression — WedgeFromBunkerEdge

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 5.605 | 2430 | BallStopped |
| NE | 45 | PASS | - | 0.000 | 0.000 | 5.705 | 2379 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 5.743 | 1707 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 2.790 | 961 | HitOOB |
| NW | 315 | PASS | - | 0.000 | 0.000 | 3.730 | 948 | HitOOB |
