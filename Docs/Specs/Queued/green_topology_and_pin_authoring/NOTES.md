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

## Known green features (web research 2026-05-18)

Captured from Japanese golf review sites; consume during Phase 3 procedural + Phase 4 refinement.

- **Hole 7** — 2段グリーン (2-tier green). Per JP course guide: *"2段グリーンになっている為、ピン位置と同じ面に乗せることがカギ"* — "Because it's a 2-tier green, getting on the same tier as the pin is the key." Author back tier ~0.5m higher than front; ridge perpendicular to approach axis. **Mark hole 7 as `sourceTag = "manual_refined_v1"` from Phase 3 onward.**
- **Hole 14** — Downhill par 4, valley left, pond right. Hardest hole (HDCP 2). Green specifics not detailed in sources but downhill landing implies elevated tee, possibly elevated green with bunker hazards on miss.
- **General design language** — Designer 川田太三 (Kawada Taizo). 1997 build. *"Becomes tighter as you approach the green"* — strategic green design with smaller targets and surrounding penalties.
- **Course character** — *"起伏の少ないバランスのある"* — "balanced with limited undulation." So greens are NOT wildly contoured (e.g. not Pinehurst No. 2 turtle-back style); subtle but strategic.
- **Grass** — Penn A1 / 007 bent grass mix. High-quality fast greens. Stimp likely 11+, consistent with our current `putt.csv` calibration.
- **Single-green design** — Important. Many JP courses have two greens (alternating seasonally for maintenance); Lomond is one-green per hole, simplifying everything.

**During Phase 4, do another sweep of:**
- `https://shotnavi.jp/gcguide/cdata/cdata_806_0.htm` (Shot Navi's per-hole pages — may have written commentary)
- `https://reserve.golfdigest.co.jp/golf-course/538502/` (GDO course detail page)
- `https://booking.gora.golf.rakuten.co.jp/voice/detail/c_id/240078` (1,384 user reviews — gold mine for per-hole green descriptions)

Add per-hole findings to this NOTES.md as bullet points before Phase 4 tracing.

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
