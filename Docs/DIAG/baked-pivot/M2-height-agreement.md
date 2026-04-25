# M2 — BakedHeightProvider vs SceneGroundProvider divergence

- Hole: Hole_01
- Samples: 100 (seed 7777)
- Polygon bounds: (-287.82, -131.02) → (280.30, 130.60)
- Tolerance: ±0.050 m

- In-scope samples: 100
- Scene-zero samples skipped: 0 (raycast missed; void)
- Within tolerance: 97/100
- Diverged: 3/100
- Max divergence: 0.401 m
- Mean abs divergence: 0.0246 m

## Histogram

- 0–1 cm:   3
- 1–2 cm:   93
- 2–5 cm:   1
- 5–10 cm:  0
- > 10 cm:  3

## Diverging samples (first 4 KB)

| x | z | type | sceneY | bakedY | diff(m) |
|---|---|------|--------|--------|---------|
| -21.98 | 13.60 | Fairway | 9.535 | 9.783 | 0.248 |
| 54.31 | 39.17 | Fairway | 11.116 | 11.489 | 0.373 |
| -144.83 | -64.37 | Fairway | 6.112 | 5.711 | 0.401 |

