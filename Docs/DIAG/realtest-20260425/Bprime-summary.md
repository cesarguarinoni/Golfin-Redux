# Phase B'1 Summary — High-Velocity Launch Diagnostic

## Scene Provider Check
SceneGroundProvider returns non-zero Y: True

## Per-shot results (first 30 diag frames focus)

| shot | surface | club | diagFrames | totalTrajFrames | minBallY | termination |
|------|---------|------|-----------|----------------|---------|-------------|
| 1 | Sand | Driver | 0 | 700 | 3.723 | HitOOB |
| 2 | Sand | Driver | 0 | 14401 | -2301.558 | MaxDurationReached |
| 3 | Green | Driver | 0 | 711 | 4.567 | HitOOB |
| 4 | Green | Driver | 1935 | 3427 | 0.000 | BallStopped |
| 5 | Sand | Wedge(ctrl) | 0 | 986 | 4.041 | HitOOB |
| 6 | Green | Putter(ctrl) | 3351 | 3352 | 9.215 | BallStopped |

## Notes
- diagFrames = rows captured by DiagPerStepSink (roll+putt phases only; airborne = 0).
- minBallY = lowest ball Y across entire trajectory. Strongly negative = fall-through.
- If driver shots show diagFrames=0 and strongly negative minBallY, failure is in the airborne integrator.
- See Bprime-shot-N.csv for per-step roll/putt frames and Bprime-shot-N-hits.csv for raycast hit lists.

## Analysis

### Confirmed fall-through: Shot 2

Shot 2 (driver from Sand, aimed +Z) is the definitive failure case:
- minBallY = **-2301.558** — ball fell through the world
- termination = **MaxDurationReached** at 14401 frames (60s) — the airborne integrator ran for the full sim budget without ever detecting ground
- diagFrames = **0** — the roll/putt phase was never entered at all

This means `SimulateAirborne` never set termination = `HitGround`. The condition `ballY <= SampleHeight(x,z)` never fired because `SampleHeight` returned 0 (no collider) at every XZ position along the +Z trajectory. The ball flew up, arced over, descended below Y=0, and kept falling under gravity for 60 full seconds.

### Contrast: Shot 1 (same origin, +X direction)

Shot 1 uses the same bunker origin (-216, 8.89, -86) and same driver velocity, only aimed +X instead of +Z:
- termination = **HitOOB** after 700 frames (~2.9s), minBallY = 3.723
- The terrain exists in the +X direction (OOB collider detected), so the sim found a landing surface and terminated correctly.

The failure is **direction-specific**. In the +Z direction from this bunker, `SampleHeight(x,z)` returns 0 at all XZ positions along the trajectory — there are no colliders there. The course geometry simply doesn't extend far enough in that direction.

### Shot 4: driver from Green aimed -X (180°) — PASSES

Shot 4 produced 1935 roll frames and terminated with BallStopped normally. The -X direction from the green points toward the rest of the course (fairway, etc.), which has full terrain coverage. The ball landed, entered roll phase, and stopped.

This confirms: the bug is not "driver velocity is too high" — it is specifically "driver aimed toward a direction with no terrain coverage." When the course has colliders at the landing XZ coordinates, the sim works fine even with driver velocity.

### Root cause (for Architect)

`SimulateAirborne` detects landing via `ballY <= SampleHeight(ballX, ballZ)`. When `SampleHeight` returns 0 (no collider at the ball's XZ position), this condition never triggers if `ballY > 0`. The ball falls below Y=0, then `ballY <= 0` eventually, but `SampleHeight` is still 0 so `ballY <= groundY` is `(negative) <= 0` = true. Wait — actually no. If `SampleHeight` returns 0 and the ball is at Y=-2301, then `ballY <= 0` should have fired immediately when ball crossed Y=0. So either: (a) the HitGround condition is `ballY <= groundY` (strict, not Y<=0), or (b) there is an additional condition that suppresses it.

**The key diagnostic gap:** DiagPerStepSink is not wired in `SimulateAirborne`. We cannot see the step-by-step airborne path, groundY values at each frame, or when/if the `ballY <= groundY` check fired. We only know the outcome: ball fell to -2301 over 60s without triggering HitGround.

**Recommendation to Architect:** Add airborne-phase DiagPerStepSink in `SimulateAirborne` to capture `(frame, ballY, groundY2arg, deltaY)` per step. This will immediately reveal at which frame `SampleHeight` started returning 0 and why the HitGround condition failed.

### Fix candidates (do not pre-implement — for Architect to spec)
1. Add OOB sentinel: if ball Y drops more than N meters below starting Y during airborne, force HitOOB termination.
2. Add airborne per-step logging and run a second diagnostic to find the exact frame where groundY goes to 0.
3. Extend terrain/OOB collider coverage to all directions from playable zones.
4. In `SimulateAirborne`, if `SampleHeight` returns 0 AND ball is descending AND ball.Y < (startY - threshold), treat as HitOOB.

**STOP — waiting for Architect B'2 spec.**
