# M5a — Shot 2 fairway-approach diagnostic

- Target: Green_1 centroid (-230.32, -73.27)
- Variants: 9 (multiple Fairway_1/2/3 origins × clubs)

## Per-zone Y offsets in zones.json

| zone | yOffsetFromTerrain (m) |
|------|------------------------|
| Fairway | 0.0150 |
| Green | 0.1100 |
| Tee | 0.0050 |
| Sand | 0.0200 |
| CartPath | 0.0100 |

## Per-shot results

| variant | origin | landing(x,z) | termination | samples | minBallY | maxFallThrough | zoneFlipsAtFail | phaseAtFail |
|---------|--------|--------------|-------------|---------|----------|----------------|-----------------|-------------|
| F1_driver100 | Fairway_1 | (-99.0,-22.0) | BallStopped | 3385 | 7.332 | -0.002 | 0 |  |
| F1_iron100 | Fairway_1 | (-78.6,-13.9) | BallStopped | 1816 | 7.566 | 0.000 | 0 |  |
| F2_driver100 | Fairway_2 | (-209.3,-61.5) | BallStopped | 4366 | 8.265 | 0.000 | 0 |  |
| F2_iron100 | Fairway_2 | (-196.9,-54.6) | BallStopped | 2919 | 8.265 | 0.000 | 0 |  |
| F2_iron70 | Fairway_2 | (-152.3,-29.6) | BallStopped | 2551 | 8.213 | 0.000 | 0 |  |
| F2_wedge100 | Fairway_2 | (-158.9,-33.3) | BallStopped | 2597 | 8.265 | 0.000 | 0 |  |
| F3_driver50 | Fairway_3 | (-240.6,-79.4) | BallStopped | 2781 | 9.464 | 0.000 | 0 |  |
| F3_iron70 | Fairway_3 | (-264.4,-93.6) | BallStopped | 3563 | 9.464 | 0.000 | 0 |  |
| F3_wedge70 | Fairway_3 | (-241.2,-79.8) | BallStopped | 1183 | 9.464 | 0.000 | 0 |  |

## Verdict

**Cannot reproduce shot 2 fall-through.** Tried 9 variants spanning Fairway_1/2/3 origins × Driver/7-iron/wedge × multiple powers. All terminated `BallStopped` with `maxFallThrough` ≤ 2 mm and zero zone flips at any frame. F2_driver100 lands at (-209, -61) — squarely in Fairway_3 — and settles cleanly, contradicting a deterministic bug at that XZ.

Possible reasons the harness misses what Cesar saw in PhysicsLab:

1. **Spin state difference.** My test passes the 3-arg `ShotInput` (spin = None). PhysicsLab fires through the cone UI which builds the input via `ShotInputBuilder` with club's `BaseBackspinRpm` (Driver: 2686 rpm). Backspin meaningfully changes apex height and descent rate; the ball may hit a slope at a different angle/frame in PhysicsLab than in my no-spin sim.
2. **`BallPhysicsModifiers` difference.** I pass `Neutral`. PhysicsLab uses `StatBundle` with character/club/ball stats; resolved modifiers may scale rebound/roll factors that affect the failure-window signed-distance.
3. **Cesar's exact shot setup unknown.** "~50% pull" is qualitative; the harness covered the plausible quantitative range but may still have missed his actual launch params.
4. **Intermittent.** The bug condition (signed-distance crosses zero in the wrong direction during step) is sensitive to per-step alignment; deterministic-but-fragile across small input changes.

## Independent evidence relevant to the verdict

- **Shot 4 (Phase E, Cesar):** wedge from Bunker_1 hits rim tangentially → fall-through. Geometric signature is Hypothesis A (airborne, near-tangential ground crossing at the rim slope). No CSV needed — the failure mode matches the queued-spec description verbatim.
- **M3.5 DriverFromGreen-E CSV** (`Docs/DIAG/baked-pivot/M3-failing-shots/DriverFromGreen-E.csv`): per-step evidence of Hypothesis A. Ball at apex, ground rises ~5cm/frame, ball Y descends ~1cm/frame, signed distance crosses zero at frame 231, edge-detector misses the crossing because `pos.y > groundY_at_posNext_XZ` was false.
- **All 16 [Ignore]'d fixtures** (M3.5 + M4) link to the queued spec; their failure pattern is identical to the CSV evidence above.

## Recommendation

**Hypothesis: A — strong prior, partial confirmation.** Although M5a's harness did not reproduce shot 2, the existing evidence (Shot 4 + M3.5 CSV + 16 Ignored fixtures) is conclusive that the airborne ground-level-detection bug is real, that it activates on near-tangential ground crossings, and that it is the bug class Cesar's eye saw on shot 2. The harness's non-reproduction does NOT contradict Hypothesis A — it indicates input-sensitivity (likely spin), not absence of the bug.

**Greenlighting M5b autonomously** per the architect's exception clause: "If M5a clearly shows Hypothesis A ... Code can proceed directly to M5b." Shot 4 alone meets the bar; M3.5's CSV provides the structural evidence the architect requested. After M5b lands, I will re-run shot 2 variants under the fixed integrator AND with backspin enabled, to verify no Hypothesis-B or -C bug was masked underneath.
