# Milestone 0 — Branch + Prerequisite check

## Status: PASS

Regression test fails on current architecture as required. Gate cleared to proceed to M1.

## What ran

- **Branch + tag:** `sim-baked-data-path` created from main HEAD (`4ff6a472`); `pre-baked-pivot` tag on same commit. Both pushed to `origin`.
- **Regression test authored** at `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs`:
  - `RegressionTest_DriverFromBunker_DoesNotFallThrough` — Driver (~70 m/s @ 12°) from `Bunker_1` centroid, 8 cardinal directions.
  - `RegressionTest_PutterFromGreen_StaysOnGreen` — Putter (~5 m/s @ 2°) from `Green_1` centroid, 8 cardinal directions.
  - `RegressionTest_DriverFromGreen_StaysOnGreen` — Driver from `Green_1` centroid, 8 cardinal directions.
  - Invariant per trajectory sample: `ball.Y >= SceneGroundProvider.SampleHeight(ball.x, ball.z) - 0.05`.
  - Writes per-direction result tables to `Docs/DIAG/baked-pivot/M0-regression-*.md`.
- **Tests executed via Unity MCP** against the main Unity Editor (port 29830). Total suite: 126 EditMode tests, 124 PASS + 2 FAIL — the 2 failures are the expected BakedPivotRegressionTests FAILs (proof of repro). All other EditMode tests unaffected.
- **Inventory complete:** `M0-uhole-geo-outputs.md`, `M0-heightmap-format.md`, `M0-zone-offsets-inventory.md`.

## Regression test result (baseline on current architecture)

| test | pass | fail | first-fail frames (representative) |
|------|-----:|-----:|-----------------------------------|
| `RegressionTest_DriverFromBunker_DoesNotFallThrough` | 1/8 | **7/8** | 3–19 frames to violate (ball drops below classified ground within 50–300 ms of launch) |
| `RegressionTest_DriverFromGreen_StaysOnGreen` | 6/8 | **2/8** | 233 + 336 frames (ball flies E/SE, lands on rising terrain) |
| `RegressionTest_PutterFromGreen_StaysOnGreen` | 8/8 | 0/8 | n/a — low-velocity putt never leaves the green's classified Y |

**Aggregate: 9/24 directions fall through on current architecture.** Baseline commit message: `m0-regression-baseline: 9/24 directions fail on current architecture`.

- `RegressionTest_DriverFromBunker_DoesNotFallThrough`: **FAIL** (as required)
- `RegressionTest_PutterFromGreen_StaysOnGreen`: **PASS** (no violations)
- `RegressionTest_DriverFromGreen_StaysOnGreen`: **FAIL** (as required)

Detail reports: `M0-regression-DriverFromBunker.md`, `M0-regression-DriverFromGreen.md`, `M0-regression-PutterFromGreen.md`.

## Artifacts

- New (committed on `sim-baked-data-path`):
  - `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs`
  - `Docs/DIAG/baked-pivot/M0-uhole-geo-outputs.md`
  - `Docs/DIAG/baked-pivot/M0-heightmap-format.md`
  - `Docs/DIAG/baked-pivot/M0-zone-offsets-inventory.md`
  - `Docs/DIAG/baked-pivot/M0-regression-DriverFromBunker.md` (test-generated)
  - `Docs/DIAG/baked-pivot/M0-regression-DriverFromGreen.md` (test-generated)
  - `Docs/DIAG/baked-pivot/M0-regression-PutterFromGreen.md` (test-generated)
  - `Docs/DIAG/baked-pivot/MILESTONE_0_DONE.md` (this file)

## Commits

- `109a93e2`: `m0-regression-test: BakedPivotRegressionTests + M0 inventory docs`
- `fc4f1aba`: `m0-milestone-done-BLOCKED: MILESTONE_0_DONE.md pending test run` (superseded by this update)
- Pending: `m0-regression-baseline: 9/24 directions fail on current architecture`

Tag `pre-baked-pivot` at `4ff6a472`. Branch tracks `origin/sim-baked-data-path`.

## Observations flagged for Architect (no action taken)

1. **Some "PASS" results in `DriverFromGreen` pass trivially** because `SceneGroundProvider.SampleHeight(x, z)` returns 0 at out-of-terrain XZ (the void-below-ground case B'1 identified). `ball.Y >= 0 - 0.05` is satisfied when ballY≈0 or above. These aren't *true* passes — they're "the invariant couldn't catch it because groundY is unknown." Under M3's `BakedHeightProvider`, those XZ will return the actual terrain Y, and the ball won't be able to rest below it. So the M3 PASS bar is naturally stricter than the M0 PASS bar. Not a test bug; just how the M0 classifier behaves.
2. **Bunker_1 ground Y @ centroid = 5.668** (visible bunker floor). First-fail frames 3–19 land at `groundY ≈ 5.87–6.79` (rising terrain just outside the bunker polygon) while `ballY` trails by 80–120 mm. This matches the exact bug class the pivot targets: the type-preference Y fix from F-Hotfix handles *placement* but the sim's step advances the ball XZ and the new XZ classifies onto rim terrain that's above ball Y → invariant violated.
3. **DriverFromGreen E/SE fail at frames 233 and 336** — ball has flown ~70 m laterally and is coming down on terrain with SampleHeight 17.9 m (higher than the 10.1 m green surface). These are genuine landings on higher ground, not launch-phase fall-throughs. Baked architecture must handle both launch-from-depressed-surface AND long-carry-landing-on-higher-terrain. Already covered by the M0 invariant; both will be retested with the baked classifier in M3.
4. **Terminations vary wildly** (`MaxDurationReached`, `BallStopped`, `HitOOB`) across directions. Some 14401-sample runs indicate the ball is stuck in a loop (sim hits the 60 s cap). Not critical for M0 — the invariant already flagged the violation before the termination mattered — but it may inform M3/M4 testing.
5. **Main project tree is dirty** (9 modified files + untracked). Pre-existing Cesar work, not introduced by this pivot. Pivot branch is clean at the commit level. Flagging for your attention at merge time.
6. **Local main vs origin/main divergence.** Local main: `4ff6a472`. Origin/main: `081feb9a "Fuck"` + `6c076909 "Stop tracking .mcp.json"` that local doesn't have. Orthogonal to pivot; needs reconciliation before the final merge.
7. **Unity MCP port confusion.** The worktree's `AI-Game-Developer-Config.json` points at `localhost:20679`; Unity is actually on `localhost:29830` (main project config). I now pass the main project path to `unity-mcp-cli run-tool` so it picks up the right config. Workaround, not a fix — the worktree config is stale.

## Next milestone ready: YES

Proceeding to M1 (`BakedZoneClassifier`) on the same branch. No Architect review needed per spec Rule 3.

## Notes for Architect

- The canonical regression bug is definitively reproduced. Bunker_1 centroid + driver velocity in any N/NE/E/SE/S/W/NW direction falls through within 3–20 frames. This is **exactly** Cesar's manual repro ("ball instantly falls through the green/bunker right where launched"). The B'1 fall-through (ball flies 300m into the void) is a SECOND failure mode that M0 also covers (the DriverFromGreen E/SE failures are similar in shape — ball lands on classified terrain higher than itself).
- The putter-on-green case never fails. This matches earlier B1 smoke-test findings: "Putt FROM green PASS. Wedge FROM bunker PASS. Driver FROM green FAIL (sometimes), Driver FROM bunker FAIL (always)." The test correctly captures the asymmetry.
- No speculative fixes attempted. The 2 FAILs are exactly the baseline we want before M3 flips the classifier to baked.
