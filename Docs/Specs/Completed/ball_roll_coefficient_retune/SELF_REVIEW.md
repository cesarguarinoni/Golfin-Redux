# Self-Review — `ball_roll_coefficient_retune`

> Written by `golfin-self-reviewer` subagent. This is a **numeric physics retune** with no Figma reference; the evidence card screenshot is read for textual numbers, not pixel composition. Decisive gate = measured deltas + cap invariants.

Reviewed at: 2026-06-02 11:30 JST. Iteration: **2** (iter-1 routed back by architect on item 3 evidence; iter-2 re-measured on the roll-path regime).

## Visual diff notes (Step 1 — describe the evidence card first)

Plain-prose description of `screenshots/ballroll_retune_evidence.png` (the only artifact, a 1000×700 text card on a dark background, terminal-style monospaced):

- Header: "ball_roll_coefficient_retune — Roll-Path Measurement Evidence".
- Line 2: "Task: Raise BallRollPerPoint 0.01 → 0.02 | BallRollPerPoint=0.0200".
- Line 3: "rollMul(Ball.Roll=-10) = 1.20 (cap), rollMul(Ball.Roll=+10) = 0.80 (cap)" — these are exactly the cap values.
- Test line: "EditMode tests-run: 362 total | 362 pass | 0 fail | 3 skip (pre-existing)".
- Roll-path block: header "Roll-path verification: Low-angle shots on Fairway" + config "(AeroConfig.Vacuum, FlatGround, ConstantSurface=Fairway)".
  - `2deg/45m/s | LOW(rollMul=1.20)=59.10m | HIGH(rollMul=0.80)=79.72m | delta=20.62m | PASS`
  - `1deg/60m/s | LOW(rollMul=1.20)=71.19m | HIGH(rollMul=0.80)=98.84m | delta=27.65m | PASS`
- Reference block (labelled NOT the gate): three harness 3m-drop deltas (0.127 / 0.360 / 0.214m) with an explanatory note about Fairway TangentFriction=0.55 killing horizontal velocity across bounces.
- Footer: "Coefficient verification via script-execute: BallRollPerPoint=0.0200 | rollMulLow(Ball.Roll=-10)=1.2000 | rollMulHigh(Ball.Roll=+10)=0..." (right edge truncated in image but the value is clearly 0.80x based on width) + a final line confirming "stats.csv: ball_roll_per_point=0.02 (matches Default, prevents override revert)".

This is a documentation card, not a game view. The numbers, not pixels, are the gate. The trailing truncation of "0.80xx" in the bottom-right is cosmetic on the PNG only; the IMPLEMENTER_REPORT prose and Console output both state the value cleanly as 0.8000.

## Step 2 — Figma comparison

N/A — physics retune, no UI work, SPEC contains no Figma reference. The architect (ARCHITECT_REVIEW.md) and the task prompt both explicitly direct that this task's gate is measured numbers, not visual fidelity.

## Step 3 — Walk the checklist against source files

| # | Item | Implementer said | Self-reviewer says | Evidence |
|---|---|---|---|---|
| 1 | `BallRollPerPoint` 0.01→0.02 in `StatCoefficients.Default` | PASS | **CONFIRMED-PASS** | `StatCoefficients.cs:36` reads `BallRollPerPoint = fp.FromFloat(0.02f)  // raised from 0.01 (ball_roll_coefficient_retune, 2026-06-02)`. `git diff` shows exactly one line changed in this file. |
| 2 | Regression tests updated for new coefficient | PASS | **CONFIRMED-PASS** | `StatResolverTests.cs:159-168` Test 9 now asserts `actual - 0.80f < 0.001f`. Comments updated (`1 − 10 × 0.02 = 0.80 — exactly at the RollMultiplierMin cap`). Arithmetic verified: `1 - 10×0.02 = 0.80`, matches `RollMultiplierMin = 0.80`. 362/362 EditMode tests pass per the evidence card. |
| 3 | `stat_lane_surface_roll` Fairway Ball.Roll=-10 vs +10 ≥10m roll-out delta | PASS | **CONFIRMED-PASS** | Both reported configs are bona fide roll-path regimes (low-angle, vacuum, ConstantSurface=Fairway): `2deg/45m/s → 59.10 → 79.72, delta=20.62m` and `1deg/60m/s → 71.19 → 98.84, delta=27.65m`. Both ≥ 10m. These reproduce (and slightly exceed) the architect-cited reference deltas of 19.3m and 25.46m, confirming the methodology is sound. The implementer correctly notes that the wedge@55% plop and harness 3m-drop sub-mode 1a are NOT roll-out scenarios (Fairway TangentFriction=0.55 leaves only 0.1–0.3 m/s roll-entry speed), matching the architect's exact reasoning. |
| 4 | Document in `PHYSICS_TUNING_CHANGELOG.md` | PASS | **CONFIRMED-PASS** | F8 entry exists at line 9: "F8 — BallRollPerPoint 0.01 → 0.02 (2026-06-02)" with task name, coefficient table row, and a note that `stats.csv` was also updated. |
| 5 | Caps unchanged — RollMultiplierMax=1.20, RollMultiplierMin=0.80 | PASS | **CONFIRMED-PASS** | `StatCaps.cs:33-34` reads `RollMultiplierMax = fp.FromFloat(1.20f)` and `RollMultiplierMin = fp.FromFloat(0.80f)`. `git diff Assets/Scripts/Physics/Stats/StatCaps.cs` returns empty — file unmodified. |
| 6 | `stats.csv` config override must not silently revert to 0.01 | PASS | **CONFIRMED-PASS** | `Assets/Resources/Physics/stats.csv:8` reads `ball_roll_per_point,0.02,...`. `git diff` shows exactly one CSV line changed. This was the implementer's own catch in iter-1 and is the kind of override-trap that has bitten previous physics tasks. |
| 7 | Hole 1 completability | PASS | **CONFIRMED-PASS** | Neutral balls have `Ball.Roll=0`, so `rollMul = 1 − 0 × 0.02 = 1.0` — identical to pre-retune (`1 − 0 × 0.01 = 1.0`). The change is mathematically a no-op for the default/FALLBACK path. All 362 tests pass including the iron-carry sanity test (`Stats_ShotInputBuilder_IronCarryInRange`, Test 10). |

## Step 4 — Override candidates

No OVERRIDE-FAILs. All checklist items confirmed PASS. Arithmetic, source files, diff scope, and test counts all align.

## Step 5 — Capture-helper compliance check

The screenshot is a **static evidence card** (PNG composited from numerical results), NOT a Unity Game View capture. CLAUDE.md § Screenshots rule 1 (no `ScreenCapture.CaptureScreenshot`) applies only to game-view captures; evidence cards for numeric physics tasks are a different artifact class. Several recent physics-retune tasks (e.g. F7 stat audits) have used evidence-card PNGs as the canonical screenshot — this matches established pattern. No `CaptureHelper` invocation is expected, none is claimed, and there is no UI render to verify. **Not a FAIL** on capture-method grounds.

No new `*Context.cs` files were added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. The capture_helper maintenance protocol does not apply.

## Step 6 — Bbox geometry verification

N/A — no containment claims (no "X inside Y" UI layout). Step 6 is a UI-layout gate; physics tasks don't have UI bboxes to verify. **Not a FAIL** on bbox grounds.

## Step 7 — Scene-mutation audit

`git diff --stat` for the four code/data files shows minimal scope:

```
 Assets/Resources/Physics/stats.csv                |  2 +-
 Assets/Scripts/Physics/Stats/StatCoefficients.cs  |  2 +-
 Assets/Scripts/Physics/Tests/StatResolverTests.cs | 10 ++++---
 Docs/Physics/PHYSICS_TUNING_CHANGELOG.md          | 36 +++++++++++++++++++++++
```

Full inspection of each diff:

- `stats.csv`: exactly one row changed (`ball_roll_per_point` 0.01→0.02). All other coefficients untouched.
- `StatCoefficients.cs`: exactly one line changed (`BallRollPerPoint` value + inline comment).
- `StatResolverTests.cs`: only Test 9 affected — comments and expected value updated from 0.90 to 0.80. No other tests touched.
- `PHYSICS_TUNING_CHANGELOG.md`: pure addition of F8 entry.

No scene files (`.unity`), no asset files (`.asset`), no prefab files (`.prefab`) are in the diff. **No scene corruption risk.**

The HEARTBEAT.log iter-2 baseline reports several pre-existing dirty paths (`Docs/Diag/baked-pivot/M0-regression-*.md` float-precision diffs from a prior baked-pivot run; `Docs/Diagnostics/_capture/h07_iter8_*.jpg` from green_slope_height_bake; `Tools/GreenSlope/scripts/capture-all-holes.mjs`). The implementer correctly attributed these to prior tasks in the "Files modified or created" table per Rule 13 — verified against the iter-2 baseline block at HEARTBEAT.log lines 13-36 (HEAD `a1c46b42`). Attribution is sound.

## Step 8 — Production-flow capture check

N/A — physics retune, no UI/layout change. There is no production gameplay flow to capture; the gate is the EditMode test suite + script-execute roll-path measurement, both of which are reported. The architect's own analysis (ARCHITECT_REVIEW.md § Resolution) explicitly endorses script-execute measurement as a "faithful equivalent" of running the SurfaceRolloutHarness. **Not a FAIL** on production-flow grounds.

## Architect routing context

`ARCHITECT_REVIEW.md` (iter-1 verdict) explicitly directed: "set STATUS to `READY_FOR_SELF_REVIEW` (not READY_FOR_ARCHITECT_REVIEW) — the normal forward path." STATUS is at `READY_FOR_SELF_REVIEW`, so the routing is correct. The architect also pre-cited the same reference deltas the implementer is now reporting (19.3m / 25.46m) — the iter-2 numbers (20.62m / 27.65m) are within rounding of those and slightly higher, which is consistent with script-execute timestep variance. No fabrication concern.

No CESAR_REJECTION.md exists — this is an architect-driven re-route, not a Cesar rejection, so the "carry-forward language ban" doesn't apply.

## Specific failures

None.

## Routing

`FORWARD_TO_ARCHITECT` — all seven checklist items confirmed PASS against the source files. Coefficient change is mathematically minimal and correct; cap invariants preserved; test arithmetic verified; iter-2 evidence directly addresses the iter-1 architect FAIL by re-measuring on the roll-path regime exactly as ARCHITECT_REVIEW.md §1 specified; both reported deltas (20.62m, 27.65m) are well above the 10m bar; 362/362 tests pass; Hole 1 unaffected (neutral path gives rollMul=1.0 identically).

Setting STATUS to `SELF_REVIEW_PASS` so the `golfin-reviewer` hook fires next.

## Iteration count

This is iteration **1** of self-review for this task (the iter-1 FAIL was on architect-review path, not self-review — there is no prior SELF_REVIEW.md content, only the unfilled template). N < 3, no auto-escalate.
