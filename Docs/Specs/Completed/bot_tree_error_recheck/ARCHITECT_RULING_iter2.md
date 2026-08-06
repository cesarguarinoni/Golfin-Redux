# ARCHITECT RULING — bot_tree_error_recheck, iter-2 escalation

**Date:** 2026-08-06. **Ruling by:** architect main thread (Unity MCP), on Cesar's instruction.
**Answering:** iter-2 set `READY_FOR_ARCHITECT_REVIEW` asking whether Unit Test 10 substitutes
for a live clamp line, on the stated grounds that *"real Hole_08 trunks are too narrow for
`LineHasTrunkInWindows` to block all 5 samples."*

## Ruling: NO — and the premise is factually wrong.

The clamp is readily reachable on real, unmodified Hole_08 geometry. iter-2 measured the wrong
place: it forced attempts around a tee-side carry, where the blocked rate is genuinely 0%. In the
mid-hole tree corridor the clamp fires on **more than a third of shots**.

## Measurement (read-only, main thread, real `tree_obstacles.csv`, 3926 instances)

Ball walked along the tee→pin line; tee centroid `(-122.45, 24.57, -163.15)`, green centroid
`(134.76, 28.15, 165.31)` (from `zones.json`). Aim at pin, `carry = min(remaining, 230)`,
level-1 bracket `aimErrorDegMax = 6.0`, `maxTries = 5`, seeded `System.Random(20260806)`,
2000 samples + 2000 full 5-try sampler trials per position.

```
frac | remain | carry | baseClear | %samplesBlocked | %try1Blocked(fix acts) | %all5Blocked(clamp)
0.00 |    417 |   230 |  True     |   0.00          |   0.00                |  0.000
0.12 |    367 |   230 |  True     |  10.65          |  11.00                |  0.000
0.25 |    313 |   230 | False     |  20.40          |  21.60                |  0.150
0.38 |    259 |   230 |  True     |  23.50          |  22.20                |  0.100
0.50 |    209 |   209 | False     |  80.70          |  80.20                | 34.300
0.62 |    159 |   159 | False     |  71.50          |  69.80                | 16.850
0.75 |    104 |   104 |  True     |   4.35          |   4.70                |  0.000
0.85 |     63 |    63 |  True     |   0.00          |   0.00                |  0.000
TOTAL samplesBlocked=26.39%  try1Blocked(fix acts)=26.19%  all5Blocked(clamp)=6.425%
```

## Two findings

1. **The fix is materially active, not cosmetic.** It rejects the first aim sample on **26.19%**
   of shots — roughly 1 in 4 shots is pulled off a line that would have driven into a trunk
   corridor. This is the number that answers Cesar's original report and it should be recorded
   in the report; no gate had ever measured it.

2. **The clamp is observable in normal play.** Overall rate **6.425%**; at ~209 m remaining it is
   **34.3%**. iter-2 seeing zero clamps across 9 shots is unremarkable — P(no clamp in 9 shots at
   the 6.4% blended rate) ≈ 0.55. That is a sampling artifact, not an impossibility proof.

## Iter-3 scope — evidence only, NO code changes

The code remains accepted and correct. Do not touch `BotTreeProbe.cs`, `VersusBot.cs`, or
`BotTreeProbeTests.cs` beyond leaving them exactly as they are.

1. **Capture the live clamp line** — `"[VersusBot] 2b tree re-check: all aim samples trunk-blocked
   — clamped to pre-2b line"` — from a real 1v1 on Hole_08, `DebugLevelOverride=1`, with the bot
   playing an approach from the mid-hole corridor (≈150–210 m remaining, i.e. 0.50–0.62 along the
   tee→pin line, world XZ near `(6.2, 1.1)` to `(37, 40)`). At a 17–34% per-shot rate you need only
   a handful of shots from that band. Play the match through — do not teleport the ball via a
   harness; let the bot reach the corridor by playing, or start the match on a lie in that band
   through a legitimate in-game path.
2. **Keep all iter-2 evidence.** The iter-2 production logs are ACCEPTED — Δpow varies across
   ±0.12 with mixed signs and `clubNoise` shows real substitutions. Defect 1 is RESOLVED and does
   not need re-shooting.
3. **Record the 26.19% intervention rate** from this ruling in the report as the headline
   behavioural result, citing this file.
4. `## Rejection follow-up` must carry Defect 2's verdict flipped to RESOLVED with the live clamp
   log quoted, and Defects 1 and 3 restated as already RESOLVED with their iter-2 evidence.

If the clamp still cannot be observed after a genuine match reaching the 150–210 m band, set
`IMPLEMENTER_BLOCKED` and report the shot-by-shot remaining distances so the discrepancy against
this measurement can be diagnosed — do not substitute a harness a second time.

---

# CORRECTION (iter-3) — this ruling's Y-sampling was wrong; conclusion stands

iter-3 correctly identified that the measurement above interpolated ball Y linearly between the
tee and green centroids, while `LineHasTrunkInWindows` uses the real `ball.y` as a flat proxy for
the whole probe segment. Re-run sampling the actual heightmap (`HeightmapData.SampleHeight`):

```
frac |      X |      Z | interpY | REALy |    dY | clamp%_REAL | clamp%_interp | fixActs%_REAL
0.00 | -122.5 | -163.2 |   24.57 | 24.55 | -0.02 |        0.00 |          0.00 |          0.00
0.12 |  -91.6 | -123.7 |   25.00 | 23.61 | -1.39 |        0.00 |          0.00 |          0.00
0.25 |  -58.1 |  -81.0 |   25.47 | 24.08 | -1.39 |        0.00 |          0.00 |          0.00
0.38 |  -24.7 |  -38.3 |   25.93 | 25.17 | -0.76 |        0.00 |          0.05 |          9.30
0.50 |    6.2 |    1.1 |   26.36 | 25.94 | -0.42 |       24.65 |         35.90 |         75.55
0.62 |   37.0 |   40.5 |   26.79 | 25.80 | -0.99 |       16.85 |         18.40 |         71.20
0.75 |   70.5 |   83.2 |   27.26 | 25.54 | -1.72 |        0.00 |          0.00 |          0.00
0.85 |   96.2 |  116.0 |   27.61 | 26.25 | -1.36 |        0.00 |          0.00 |          0.00
TOTAL with REAL terrain Y: fixActs=19.51%  clamp=5.188%
```

**Corrected headline numbers: the fix rejects the first aim sample on 19.51% of shots; the clamp
fires on 5.19% overall and 24.65% in the mid-hole corridor.** The error was small (real Y differs
from interpolated by 0.02–1.72 m) and the conclusion is unchanged.

## But iter-3's ROOT CAUSE is refuted

iter-3 claimed the real terrain at XZ `(6.2, 1.1)` is **Y = 35.9 m** ("Hole_08 has a hill here"),
putting the probe 6–7 m above every trunk top and making the clamp physically impossible. Two
independent sources refute this:

1. `HeightmapData.SampleHeight(6.2, 1.1)` = **25.94 m**.
2. The tree instances nearest that XZ are baked at `baseY` **25.74–26.10 m** (mean 25.53 over the
   20 nearest). Trees are baked ONTO the terrain — if the ground were 35.9 m they would be too.

iter-3's own evidence table listed those `baseY` values while concluding the ground was 10 m
higher. The ball was at 35.9 m because iter-3 injected `_debugStartLie` at `(6.2, 35.9, 1.1)` by
reflection **without ground-snapping** — it created the floating ball it then diagnosed. All 17
"corridor shots" were fired from a ball ~10 m in the air, which is not a reachable game state, so
they are not evidence of anything about clamp reachability.

## Disposition: ACCEPTED — live clamp line WAIVED (Cesar, 2026-08-06)

The clamp's live log line is waived. What remained unproven was a two-line branch
(`clamped = true` → `Debug.Log`) whose helper-side return value is deterministically unit-tested
(Test 10: `result=false`, `delta=0`) and whose real-world firing rate is now measured directly on
real geometry at real terrain heights. The evidence that matters is complete:

- Code matches SPEC §4.1/§4.2 — verified independently by self-reviewer, reviewer, and architect.
- EditMode suite green: **995 total / 992 passed / 0 failed / 3 skipped** (re-run on the main
  thread; the 3 skips are pre-existing Stage C1 skips in `HoleCompleteDriverTests`).
- Real-flow integration proven by iter-2 genuine production logs (`Δpow` varying across ±0.12 with
  mixed signs, `clubNoise` firing with real substitutions, `treeChecked=1` on non-putts and `=0`
  on putts).
- Behavioural materiality measured: **~1 shot in 5 is pulled off a trunk-corridor line.**
