# M2 — BakedHeightProvider vs SceneGroundProvider divergence

- Hole: Hole_01
- Samples: 100 (seed 7777)
- Polygon bounds: (-287.82, -131.02) → (280.30, 130.60)
- Tolerance: ±0.050 m

- In-scope samples: 100
- Scene-zero samples skipped: 0 (raycast missed; void)
- Within tolerance: 95/100
- Diverged: 5/100
- Max divergence: 0.408 m
- Mean abs divergence: 0.0341 m

## Histogram

- 0–1 cm:   2
- 1–2 cm:   93
- 2–5 cm:   0
- 5–10 cm:  0
- > 10 cm:  5

## Diverging samples (first 4 KB)

| x | z | type | sceneY | bakedY | diff(m) |
|---|---|------|--------|--------|---------|
| -21.98 | 13.60 | Fairway | 9.535 | 9.136 | 0.400 |
| 42.70 | -24.15 | CartPath | 7.181 | 6.773 | 0.408 |
| 12.15 | -0.02 | CartPath | 9.067 | 8.660 | 0.406 |
| 54.31 | 39.17 | Fairway | 11.116 | 10.716 | 0.400 |
| -144.83 | -64.37 | CartPath | 6.112 | 5.706 | 0.406 |

