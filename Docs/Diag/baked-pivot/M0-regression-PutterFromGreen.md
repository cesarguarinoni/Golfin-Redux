# Regression — PutterFromGreen

- Provider: BakedHeightProvider + BakedZoneClassifier (post-pivot)
- Invariant tolerance: 0.050 m
- Sustained-frame threshold: 3

| dir | yaw | result | violFrame | ballY | groundY | minBallY | samples | termination |
|-----|-----|--------|-----------|-------|---------|----------|---------|-------------|
| N | 0 | PASS | - | 0.000 | 0.000 | 9.754 | 2287 | BallStopped |
| NE | 45 | PASS | - | 0.000 | 0.000 | 10.179 | 2270 | BallStopped |
| E | 90 | PASS | - | 0.000 | 0.000 | 10.183 | 2287 | BallStopped |
| SE | 135 | PASS | - | 0.000 | 0.000 | 10.183 | 2332 | BallStopped |
| S | 180 | PASS | - | 0.000 | 0.000 | 10.183 | 2376 | BallStopped |
| SW | 225 | PASS | - | 0.000 | 0.000 | 10.149 | 2395 | BallStopped |
| W | 270 | PASS | - | 0.000 | 0.000 | 9.758 | 2376 | BallStopped |
| NW | 315 | PASS | - | 0.000 | 0.000 | 9.599 | 2332 | BallStopped |
