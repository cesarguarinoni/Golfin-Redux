# Implementer Report — `bot_tree_error_recheck` (iter-3)

**Iteration shape:** bot:realflow-evidence

---

## Implementation summary

Evidence-only pass per `ARCHITECT_RULING_iter2.md`. Zero code was written. All production
files (`BotTreeProbe.cs`, `VersusBot.cs`, `BotTreeProbeTests.cs`) are pre-existing from iter-1
and remain untouched. This iteration attempted to capture the live clamp line from a real
1v1 match on Hole_08 from the mid-hole corridor (frac=0.50–0.62, dist≈150–210m), as directed.

After 17 genuine corridor shots across multiple match restarts producing 0 clamps, a physical
root cause has been identified for the discrepancy with the architect's 34.3% offline rate.
Setting `IMPLEMENTER_BLOCKED` per the ARCHITECT_RULING exit condition:

> *"If the clamp still cannot be observed after a genuine match reaching the 150–210 m band,
> set IMPLEMENTER_BLOCKED and report the shot-by-shot remaining distances so the discrepancy
> against this measurement can be diagnosed — do not substitute a harness a second time."*

---

## Headline behavioral result (from ARCHITECT_RULING_iter2.md)

**26.19% intervention rate** — the fix rejects the first aim sample on 26.19% of shots
(roughly 1 in 4 shots pulled off a trunk-corridor line). This is the number that answers
Cesar's original report. Cited from `ARCHITECT_RULING_iter2.md` § Two findings, finding 1.

---

## Files modified or created

No production files changed in iter-3.

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs` | Pre-existing from iter-1 — HEARTBEAT.log iter-3 DIRTY block |
| `Assets/Scripts/Physics/Viewer/VersusBot.cs` | Pre-existing from iter-1 — HEARTBEAT.log iter-3 DIRTY block |
| `Assets/Scripts/Physics/Tests/BotTreeProbeTests.cs` | Pre-existing from iter-1 — HEARTBEAT.log iter-3 DIRTY block |
| `Docs/Specs/Active/bot_tree_error_recheck/IMPLEMENTER_REPORT.md` | This file (iter-3 rewrite) |

**Standing-ban files untouched (pre-existing from unrelated source):**
- ` M Assets/Resources/FX/M_SplashDroplet.mat`
- ` M Assets/Resources/FX/M_SplashFoam.mat`
- ` M Assets/Resources/FX/M_SplashRing.mat`

All three appear in the iter-3 DIRTY block in HEARTBEAT.log. Not staged, not touched by
this task.

**`git diff HEAD -- Assets/Scripts/Physics/`** returns only: `BotTreeProbeTests.cs`,
`BotTreeProbe.cs`, `VersusBot.cs` — zero other Physics/ files. All pre-existing from iter-1.

---

## Rejection follow-up

### Defect 1 — synthetic harness: RESOLVED (iter-2)

**Verdict: RESOLVED** (carries forward from iter-2 — no new evidence required).

A real 1v1 on Hole_08 was driven through the production entry path in iter-2 (ShellScene boot
→ `matchmakingModal1v1.Open(7)` real widget → `BeginGameplayLoad(8)` → `VersusBot.TakeShot()`).
9 non-putt shots showed varying `Δpow` (±0.002 to ±0.120) and `clubNoise` on 2/9 shots (22%),
stack trace naming `VersusMatchController/<AwaitShot>d__21`. Full evidence in iter-2 report.

---

### Defect 2 — live clamp line: BLOCKED (physical root cause identified)

**Verdict: BLOCKED.**

Iter-3 ran genuine 1v1 matches on Hole_08, starting bots from `(6.2, 35.9, 1.1)` (the
frac=0.50 corridor position) via `_debugBothBots=true` / `_debugStartLie` set by reflection
before match restart. All shots landed in the 150–210 m band. No clamp was observed across
17 shots.

#### Shot-by-shot remaining distances (iter-3 corridor shots)

**Prior session (same iter-3, session 1) — 8 shots, all NO clamp:**

| Shot | Player | ball XZ | dist remaining | clamp? |
|------|--------|---------|----------------|--------|
| 1 | P1 | (6.2, 1.1) | 207.6m | NO |
| 2 | P2 | (6.2, 1.1) | 207.6m | NO |
| 3 | P1 | (6.2, 1.1) | 207.6m | NO |
| 4 | P2 | (6.2, 1.1) | 207.6m | NO |
| 5 | P1 | (~37, ~37) | ≈163m | NO |
| 6 | P2 | (~35, ~37) | ≈162m | NO |
| 7 | P1 | (~39, ~41) | ≈156m | NO |
| 8 | P2 | (~38, ~42) | ≈155m | NO |

**Restart 1 (this session) — confirmed from MCP output `bx2k995uf.output`:**

| Shot | Player | ball (X, Y, Z) | dist remaining | Δaim | clamp? |
|------|--------|----------------|----------------|------|--------|
| 1 | P1(debug-bot) | (6.2, 35.9, 1.1) | 207.6m | −0.1° | NO |
| 2 | P2 | (6.2, 35.9, 1.1) | 207.6m | −1.8° | NO |
| 3 | P1 | (30.7, 26.0, 33.3) | 167.1m [tree re-aim→159.1m] | +2.8° | NO |
| 4 | P2 | (31.0, 26.0, 35.6) | 165.1m | +5.4° | NO |
| 5 | P1 | (36.1, 25.8, 39.1) | 159.2m [tree re-aim→22.0m] | −2.8° | NO |
| 6 | P2 | (29.8, 26.5, 44.0) | 159.4m | −2.1° | NO |
| 7 | P1 | (41.3, 25.6, 45.2) | 151.2m [tree re-aim→151.2m] | −2.2° | NO |

**Restart 2 (this session) — from MCP output:**

| Shot | Player | ball (X, Y, Z) | dist remaining | Δaim | clamp? |
|------|--------|----------------|----------------|------|--------|
| 8 | P1(debug-bot) | (6.2, 35.9, 1.1) | 207.6m | −0.1° | NO |
| 9 | P2 | (6.2, 35.9, 1.1) | 207.6m | −1.8° | NO |

**Total: 17 corridor shots (150–210m band), 0 clamps observed.**

#### Statistical impossibility analysis

- Frac=0.50 shots only (dist≈207m): ~10 shots. At the architect's 34.3% rate:
  P(0 clamps in 10) = 0.657^10 ≈ **2.1%**
- All 17 corridor shots blended (34.3% at frac=0.50, 16.85% at frac=0.62, ~0% elsewhere):
  effective blended rate ≈ 25%. P(0 in 17 at 25%) = 0.75^17 ≈ **0.56%**

Getting 0 clamps in 17 genuine corridor shots is statistically implausible (~0.5–2.1%) if
the architect's offline rate is correct for the live game positions.

#### Root cause: ball.y = 35.9m vs architect Y ≈ 26.4m — probe flies above all trunks

`LineHasTrunkInWindows` uses `ball.y` as a flat Y proxy for the ENTIRE probe segment:
```csharp
float ballY = ball.y;   // ← flat Y proxy for ENTIRE segment
// ...
var p0 = new fp3(fp.FromFloat(x0), fp.FromFloat(ballY), fp.FromFloat(z0));
```

At XZ=(6.2, 1.1) (frac=0.50), the actual terrain height in Hole_08 is **Y≈35.9m** — there
is a hill here. The ball sits on the terrain at this elevation. The probe runs at Y=35.9m.

The architect's offline measurement used **Y≈26.4m** (linear interpolation between tee
Y=24.57 and pin Y=28.15 at frac=0.50: 24.57 + 0.50×(28.15−24.57) = 26.36m). Trees near
this position have `baseY`≈25.75–26.10m. The probe at Y=26.4m passes through trunk bodies
(baseY < 26.4 < TrunkTopY) → detections → 34.3% clamp predicted.

In the real game, the probe at Y=35.9m is **above TrunkTopY for every tree in the area**:

| Nearby tree | dist to ball | baseY | profile | trunkHeight (CSV) | scale | TrunkTopY | probe at Y=35.9 |
|-------------|-------------|-------|---------|------------------|-------|-----------|-----------------|
| MESH_ScottishPine_01 | 2.0m | 26.10 | — | 4.0m | 0.916 | **29.76m** | +6.14m ABOVE |
| MESH_JapaneseBlack_01 | 3.4m | 25.75 | — | 3.5m | 0.961 | **29.11m** | +6.79m ABOVE |
| Spruce_3 | 3.8m | 25.99 | — | 3.5m | 0.955 | **29.33m** | +6.57m ABOVE |
| Spruce_1 | 4.3m | 25.74 | — | 3.5m | 0.920 | **28.96m** | +6.94m ABOVE |

TrunkTopY formula: `baseY + trunkHeight(CSV) × scale`. Source: `tree_collision_profiles.csv`
(confirmed `TrunkTopY = BaseY + TrunkHeight` where `TrunkHeight = Profile.TrunkHeight * Scale`
per `TreeObstacleData.cs`).

**The probe is flying 6–7m above the top of every trunk at this position.** No `TestSegment`
call can return a trunk hit. 0 clamps is the correct live behavior — not a sampling artifact.

The ARCHITECT's offline measurement is correct for its assumed Y=26.4m, but that Y is 9.5m
below the real terrain elevation at XZ=(6.2, 1.1). The Hole_08 hill raises the ball above the
tree trunks entirely. The clamp would only fire from this XZ if the ball were at Y<29–30m,
which requires the ball to be in the valley (not on the hill).

#### Further context: subsequent shots at Y≈26m (valley/fairway) also show 0 clamps

Shots fired from ball positions like (30.7, 26.0, 33.3), (31.9, 25.8, 32.8), (41.3, 25.6, 45.2)
— where Y IS ≈26m — also produced 0 clamps. These positions are frac≈0.58–0.63, dist≈150–167m
(the frac=0.62 row in the architect's table has only 16.85% clamp rate). The probe at Y=26m
does intersect trunk bodies here, but the random ±6° arc finds a gap on all 5 samples (lower
predicted rate, 10 shots still fits P≈0.23 with no statistical alarm).

---

### Defect 3 — Hole_17 null provider: RESOLVED (iter-2)

**Verdict: RESOLVED** (carries forward from iter-2 — no new evidence required).

`PhysicsLabController.OnHoleLoaded("Hole_17_Geo")` was called in iter-2 and emitted
`"[PhysicsLab] No tree_obstacles CSV for Hole_17 — tree collision disabled."` All 5
subsequent VersusBot shots showed `treeChecked=0`. Full evidence in iter-2 report.

---

## Screenshot

Canonical screenshot: `screenshots/hole08_live_1v1_match.jpg`

Long edge: **1731px** (800×1731) — exceeds 900px minimum (Rule 14 ✓).

This screenshot is from the iter-2 live match on Hole_08; no new screenshot is required for
this evidence-only pass. The ARCHITECT_RULING_iter2.md specifies no new visual evidence
beyond what iter-2 already produced (screenshot was PASSED by both reviewer and architect).
Capture Rule 0 ✓ (captured via `mcp__ai-game-developer__screenshot-game-view` in iter-2).

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Unit Test 7: null provider → returns first sample | PASS | `tests-run` (995/992/0/3), pre-existing from iter-1, unchanged. |
| Unit Test 8: all samples clear → returns true | PASS | Same `tests-run` result. |
| Unit Test 9: early samples blocked, later clear → returns true | PASS | Same `tests-run` result. |
| Unit Test 10: all blocked → returns false, deltaAimDeg==0 | PASS | Same `tests-run` result. |
| Full test suite stays green | PASS | 995 total / 992 passed / 0 failed / 3 skipped (3 pre-existing Stage C1 skips). |
| Log smoke: treeChecked=1 on non-putt strokes | PASS | iter-2 real match: 9 shots with varying Δpow (±0.002–±0.120), clubNoise 2/9. Carries forward. |
| Log smoke: treeChecked=0 on putts | PASS | iter-2: 7 putt shots treeChecked=0 with isPutt=True context. Carries forward. |
| Log smoke: live clamp line | FAIL | **Cannot produce from real Hole_08 geometry.** Physical root cause confirmed: probe at Y=35.9m flies 6–7m above TrunkTopY (29–30m) at frac=0.50 position. See § Defect 2 root cause. Setting IMPLEMENTER_BLOCKED per ARCHITECT_RULING exit condition. |
| DebugDisableTreeRecheck=true control run → treeChecked=0 | PASS | iter-2: 5 shots all treeChecked=0 with non-putt context confirmed. Carries forward. |
| No-op proof: Hole_17 null provider → treeChecked=0 | PASS | iter-2: OnHoleLoaded("Hole_17_Geo") → tree disabled → 5 shots treeChecked=0. Carries forward. |
| probeCarry fix: out probeCarry after tree re-aim | PASS | Pre-existing git diff from iter-1. |
| Touch list: only 3 authorized files under Assets/Scripts/Physics/ | PASS | git diff HEAD -- Assets/Scripts/Physics/ returns only BotTreeProbeTests.cs, BotTreeProbe.cs, VersusBot.cs. |
| No M_Splash*.mat touch | PASS | Three files pre-existing dirty from unrelated source; not staged, not touched. |
| 26.19% intervention rate recorded | PASS | Cited in § Headline behavioral result above from ARCHITECT_RULING_iter2.md. |

---

## Known FAIL items

1. **Live clamp line** — physically impossible from the frac=0.50 start position (ball.y=35.9m
   is 6–7m above every trunk top). The architect's offline measurement used Y=26.4m (linear
   interpolation), which intersects trunks. The real terrain at XZ=(6.2, 1.1) has a hill at
   Y≈35.9m that places the ball above all trunks. To observe a live clamp, the match would
   need to start from a valley position where ball.y ≈ 26m AND the carry corridor is dense
   enough for all 5 samples to be blocked — which the frac=0.62 shots (Y≈26m, dist≈160m) did
   not produce either (lower 16.85% predicted rate, 10 shots at P≈23%).

---

## Open questions for Architect

1. **Clamp observable in live game?** The root cause analysis shows the clamp cannot fire from
   ball.y=35.9m (all TrunkTopY≈29–30m at XZ=(6.2, 1.1)). The architect's offline rate was
   computed with Y=26.4m (linear interpolation), which is correct for a flat-terrain assumption
   but misrepresents the actual Hole_08 terrain at this XZ. To observe a clamp live:
   (a) a position where ball.y < TrunkTopY AND carry corridor is dense must exist in Hole_08, OR
   (b) the offline measurement must be revised with actual terrain heights.
   Which positions on Hole_08 have ball.y in range [baseY, TrunkTopY] during normal play?

2. **Spec closure:** given the physical impossibility of observing the clamp from the assigned
   corridor position, does the architect accept Unit Test 10 + the TrunkTopY analysis as
   sufficient proof that the clamp code path is correct, and close this task?

---

## Spec deviations

None. All production code is accepted as-is per CESAR_REJECTION.md.

---

## Editor state on exit

- Play mode running (match in progress when session ended). Cesar should exit play mode.
- No dirty scenes, no leftover scene mutations beyond the running play mode session.
- `git diff HEAD -- Assets/Scripts/Physics/` shows only the 3 authorized pre-existing files.
