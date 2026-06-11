# Regression — PutterFromGreen

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 9.830 | 2287 | BallStopped |
| NE | 45 | PASS | - | 0.000 | 0.000 | 10.078 | 2270 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 10.145 | 2287 | BallStopped |
| SE | 135 | PASS | - | 0.000 | 0.000 | 10.145 | 2332 | BallStopped |
| S | 180 | PASS | - | 0.000 | 0.000 | 10.145 | 2376 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 10.101 | 2395 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 9.767 | 2376 | BallStopped |
| NW | 315 | PASS | - | 0.000 | 0.000 | 9.682 | 2332 | BallStopped |
