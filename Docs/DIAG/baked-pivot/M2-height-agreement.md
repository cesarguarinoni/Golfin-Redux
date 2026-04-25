# M2 — BakedHeightProvider vs SceneGroundProvider divergence

- Hole: Hole_01
- Samples: 100 (seed 7777)
- Polygon bounds: (-287.82, -131.02) → (280.30, 130.60)
- Tolerance: ±0.050 m

- In-scope samples: 100
- Scene-zero samples skipped: 0 (raycast missed; void)
- Within tolerance: 99/100
- Diverged: 1/100
- Max divergence: 0.152 m
- Mean abs divergence: 0.0067 m

## Histogram

- 0–1 cm:   68
- 1–2 cm:   29
- 2–5 cm:   2
- 5–10 cm:  0
- > 10 cm:  1

## Diverging samples (first 4 KB)

| x | z | type | sceneY | bakedY | diff(m) |
|---|---|------|--------|--------|---------|
| -130.97 | -42.21 | Sand | 7.242 | 7.091 | 0.152 |

