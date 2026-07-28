# Regression — WedgeFromBunkerEdge

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 5.605 | 1437 | HitOOB |
| NE | 45 | PASS | - | 0.000 | 0.000 | 5.705 | 4339 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 6.185 | 1330 | HitOOB |
| SE | 135 | PASS | - | 0.000 | 0.000 | 6.337 | 2269 | HitOOB |
| S | 180 | PASS | - | 0.000 | 0.000 | 6.082 | 2932 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 5.287 | 1471 | HitOOB |
| W | 270 | PASS | - | 0.000 | 0.000 | 1.455 | 1062 | HitOOB |
| NW | 315 | PASS | - | 0.000 | 0.000 | 2.766 | 1046 | HitOOB |
