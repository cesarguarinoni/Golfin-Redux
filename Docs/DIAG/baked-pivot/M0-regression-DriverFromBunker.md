# M0 Regression — DriverFromBunker

- Origin GO: `Bunker_1`
- Centroid (world XZ): (-129.780, -34.965)
- Ground Y at centroid (SceneGroundProvider): 5.668
- Invariant tolerance: 0.050 m
- Classifier: SceneGroundProvider (current architecture)

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | FAIL | 19 | 6.795 | 6.890 | 5.688 | 14401 | MaxDurationReached |
| NE | 45 | FAIL | 545 | 10.927 | 10.977 | 5.688 | 7692 | MaxDurationReached |
| E | 90 | FAIL | 4 | 5.929 | 6.007 | 5.688 | 14401 | MaxDurationReached |
| SE | 135 | FAIL | 3 | 5.869 | 5.989 | 5.688 | 14401 | MaxDurationReached |
| S | 180 | FAIL | 6 | 6.048 | 6.130 | 5.688 | 7581 | MaxDurationReached |
| SW | 225 | PASS | - | 0.000 | 0.000 | 4.521 | 629 | HitOOB |
| W | 270 | FAIL | 13 | 6.455 | 6.563 | 5.688 | 3367 | BallStopped |
| NW | 315 | FAIL | 10 | 6.282 | 6.399 | 5.688 | 706 | HitOOB |
