# NOTES.md — Green Topology and Pin Authoring

Architect working notes. Not implementation truth — that's `SPEC.md`.

---

## Decision log

**2026-05-18 chat (Architect ↔ Cesar):**

Five framing questions answered:

1. **PuttView book** — declined (shipping risk to Spain; arrival not guaranteed).
2. **Storage format** — grid (not sparse arrows).
3. **Heightmap reconciliation** — option (b): bake slope into heightmap.bytes.
4. **Pin authoring** — lean (3-5 candidates per green, `defaultPinIndex = 0`).
5. **Coverage** — all 18 holes, no vertical slice.

Three digital data sources explored:

| Source | Lomond coverage | Cost | Verdict |
| --- | --- | --- | --- |
| PuttView paper book | Confirmed (storefront) | $29 + shipping | ❌ Shipping unreliable |
| PuttView Books app | **NOT** in digital library (Loch Lomond Scotland only) | $100/yr | ❌ Wrong Lomond |
| StrackaLine paper book | Confirmed (Cesar) | ~$55 | ❌ Same shipping risk |
| StrackaLine app | Unverified coverage | $99/yr | 🟡 Fallback if Shot Navi fails |
| Shot Navi 3DX | **Confirmed** (course ID 806, elevation refreshed 2023-09-03, map 2024-07-09) | Free + 3-day premium trial | ✅ **Chosen** |

---

## Data acquisition workflow (Phase 4 mechanics) — ✅ COMPLETE 2026-05-18

**Captures landed:** 36 PNGs in `screenshots/`:
- 18 × `lomond_hole_NN_shotnavi_strategy.png` (distance/yardage view)
- 18 × `lomond_hole_NN_shotnavi_heatmap.png` (topographic, rainbow icon active)

**Original capture workflow (kept for reference / future re-captures):**

1. **Install Shot Navi 3DX** from Google Play (global access) or App Store JP (iOS requires Japanese App Store account). Android device recommended.
2. **First launch triggers 3-day premium trial** — note the date. Phase 4 work must complete inside this window.
3. **Search for "ローモンド"** (Lomond) in-app. Course ID 806 should appear at the top.
4. **Per hole, capture two screenshots:**
   - Default Green Strategy view (distance/yardage) → `lomond_hole_NN_shotnavi_strategy.png`
   - Tap rainbow icon to toggle heatmap → `lomond_hole_NN_shotnavi_heatmap.png`
5. **Commit screenshots** to `Docs/Specs/Queued/green_topology_and_pin_authoring/screenshots/`.
6. **Use screenshots as backdrops** in `GreenTopologyEditor` Phase 2 tool (drop both per hole, trace heatmap arrows + pull pin position from strategy flag).

**Tip:** Capture all 18 holes in one focused session on day 1 of the trial. Leaves a 2-day buffer for re-captures if any screenshots are bad framing / wrong zoom level.

## Critical calibration notes (2026-05-18)

- **Shot Navi green-view distances are METERS, not yards.** Course-level distances (scorecard yardages, tee distances) ARE yards. Inside the green-zoom view, the perimeter scale (`0/5/10/15/20`) and inline distance numbers (e.g. `13`, `11`, `16`) are meters. This matches our `cellSize: 0.5` (m) data format — 1 visible Shot Navi grid square = 1 m. When aligning the backdrop in Phase 2 tool, treat the grid as meters.
- **Default pin = Shot Navi flag location.** Each `_strategy` capture shows a white flag glyph on the green; that's the canonical default pin. Read it from the image and store as `pinCandidates[0]` with `defaultPinIndex = 0`. 2-4 alternate candidates authored manually based on visible green topology in the `_heatmap` capture.
- **Lomond character is subtle.** Most heatmap captures show mostly-green coloring with light accents — matches the "balanced with limited undulation" character from Japanese course reviews. Hole 9 (`lomond_hole_09_shotnavi_heatmap.png`) is the visible outlier with yellow/orange. Don't author slope where the heatmap shows uniformly green; that's the data telling us the green is flat.

---

## Known green features (PDF-extracted 2026-05-26)

**Source:** `A4_ホール攻略冊子.pdf` (Lomond 2019 strategy booklet), one page per hole (PDF page N+1 = hole N).

Dimensions are **width × depth in meters**, read off the PDF's `GREEN攻略法` panel. Magnitude calibration per Phase 4 spec.

| Hole | W×H (m) | Feature | Strategic note (JP) | EN translation |
|---|---|---|---|---|
| 1 | 31×30 | Back→front + R→L slope | 右サイドからは見た目よりはやい | Fast from right (faster than it looks) |
| 2 | 25×33 | Back→front, multi-arrow | 右サイドの奥からはやい | Fast from back-right |
| 3 | small | **2-tier** (dashed ridge) | 2段グリーンです。同じ段にのせましょう | 2-tier; aim same tier as pin |
| 4 | 18×33 | Slope to front | 右サイドからはやい | Fast from right |
| 5 | 21×37 | **Minimal slope** + small ridge | 傾斜の少ないグリーン | Little slope — attack with confidence |
| 6 | 26×30 | Slope to front-left | 左サイドからは見た目よりはやい | Faster than it looks from left |
| 7 | 43×29 | **L/R 2-tier** (diagonal ridge) | 左右の2段グリーン | L/R 2-tier; aim same tier |
| 8 | 22×32 | Back→front | 奥からは見た目よりはやい | Faster than it looks from back |
| 9 | 25×38 | **Heavy mounding, multi-direction** | 傾斜やマウンドが多い | Lots of slope and mounding |
| 10 | 38×25 | Front→back gradient on left | 左サイドは手前から奥にはやい | Left side: fast front→back |
| 11 | 22×36 | **Upper tier w/ mounding** | 上の段はマウンドがある | Upper tier has mounding — read carefully |
| 12 | 29×37 | Tier-like ridge | 左上からはやい | Fast from upper-left |
| 13 | 30×40 | Tier in upper-left | 左上からは見た目よりはやい | Faster than looks from upper-left |
| 14 | 33×30 | Back→front + partial back tier | 奥からはやい | Fast from back |
| 15 | 31×37 | Slope down/left | 左サイドからは見た目よりはやい | Faster than looks from left |
| 16 | 24×33 | Slope to front | 奥からはやい | Fast from back |
| 17 | 25×37 | Slope + dashed line | 左奥からはやい | Fast from back-left |
| 18 | 28×40 | **Vertical 2-tier** (horizontal ridge) | 縦長の2段グリーン | Vertical 2-tier; aim same tier |

**Summary statistics:**
- Greens range from ~16 m to 43 m wide, 25 m to 40 m deep
- 4 confirmed 2-tier: 3, 7, 11, 18
- 5 likely-partial tier (dashed line in PDF without explicit "2段" call-out): 5, 12, 13, 14, 17
- 1 minimal-slope: 5
- 1 heavily mounded: 9 (matches Shot Navi heatmap outlier)
- Most-common strategic note pattern: "見た目よりはやい" (faster than it looks) — implies subtle but consistent slope, calibrated 1.5-2%

**Dimensions caveat:** A few dimension labels were clipped in the grid extraction; verify each by zooming the PDF panel directly during Phase 4 tracing.

---

## Risk register

(Mirrored from SPEC.md § Risk register for quick reference.)

- **R1 (medium):** Shot Navi data is GPS-grade, not laser-scan-grade.
- **R2 (low):** 3-day trial window forces Phase 4 timing.
- **R3 (low):** Heightmap reconcile might surface visual seams at fringe boundaries.
- **R4 (low):** Existing physics calibration was tuned against flat greens; slope might shift feel.
- **R5 (low):** Shot Navi quality insufficient → StrackaLine app as $99/yr fallback.

---

## Open questions for future iteration

(Not blocking spec; surface if any of these turn into real problems.)

- **Putting feel calibration after Phase 6** — current `putt.csv` Green k=0.50 was tuned flat. Sloped putts will feel different. Cesar live-play session post-Phase-6 may surface a need for k re-tune. If so, file as `controls_X_putt_recalibration_post_topology`.
- **Per-pin difficulty rating** — Loop v2+ might want "easy / medium / hard" tags per pin candidate for matchmaking variance. Authoring tool could add a difficulty field; trivial extension to schema.
- **Replay determinism with topology** — slope grid + heightmap reconcile produce different ball paths than pre-spec. Any saved replays from before this work will be invalidated. Acceptable since replays aren't shipped yet, but tag this in the save state spec when written.
- **Editor authoring throughput** — if Phase 3 + Phase 4 manual work is slower than estimated, consider building a programmatic "import StrackaLine PDF (if Cesar later acquires the book)" path. Defer until measured pain.
