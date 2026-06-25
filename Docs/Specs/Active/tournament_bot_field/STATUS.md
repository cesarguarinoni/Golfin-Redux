READY_FOR_SELF_REVIEW

# STATUS — tournament_bot_field (T3)

## ✅ Decisions locked (Cesar, 2026-06-25)
- **D0 — PRNG:** ship our own explicit ~15-line PCG/xorshift seeded by the stable hash (NOT System.Random).
- **D1 — strokes distribution:** use the §3 proposed per-bracket meanΔ/stdev values, shipped in tunable `bot_score_brackets.csv`.
- **D2 — identity selection:** honor `BracketWeights` — sample target bracket from weights (seeded), pick a no-repeat roster identity in that tier (nearest-bracket fallback if exhausted); distribution params come from the slot's bracket.
- **D3 — worst-hole cap:** **par+4**, reusing `versusStrokeCapOverPar`. `clamp(strokes, 1, par+4)`. Stroke-bounds test asserts `1 ≤ strokes ≤ par+4`.
- **D4 — provisional ranking:** **ranking shows only FINISHED bots.** T3 `Project` still emits `(thru, revealedStrokes, complete)` unchanged; T4's `GetLeaderboard` filters to `complete == true` (hide any bot with `thru < H`). T3 needs no change for this — just ensure `complete` is correct.

## Staging
- [x] **Stage 1** — stable hash (FNV/xxHash) + explicit PRNG + per-bot seed streams + hash-vector/determinism tests.
- [x] **Stage 2** — `bot_score_brackets.csv` + bracket→strokes roll + identity selection + `RollField` + tests.
- [x] **Stage 3** — pace schedule + `Project` + pace/projection/trickle tests.

## Test results (gate)
47/47 PASS — Golfin.Tournaments.Tests (EditMode), 2026-06-25. See `IMPLEMENTER_REPORT.md` for per-test table.
