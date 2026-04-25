# Phase E — Cesar's manual confirmation (re-run after M5b fix)

**Status:** READY (POST-M5b). First Phase E run (2026-04-25) had shots 2 and 4 fall through; M5b applied the queued signed-distance level-detector fix to `BallSimulation.SimulateAirborne`; all 16 previously-Ignored fixtures now pass; 229/229 EditMode tests green; bit-exact gate held. Cesar re-fires the same 5 manual shots; if all 5 visibly clean, Cesar merges to `main`. Spec: `Docs/Specs/Active/SIM_BAKED_DATA_PATH.md` Phase E + `Docs/DIAG/baked-pivot/MILESTONE_5_DONE.md`.

---

## What you're verifying

The architectural pivot replaces scene-coupled providers with baked `zones.json` + `heightmap.bytes`. The sim no longer raycasts the live scene; the visible Unity scene is purely cosmetic. The original repro ("ball instantly falls through the green/bunker into the void below") is eliminated by construction — the heightmap covers the entire terrain rect, so `SampleHeight` cannot return 0 from a missing collider hit.

A residual sim airborne-handoff bug remains for near-tangential ground crossings (ball flying horizontally into rising terrain at apex) — 4 of 24 regression directions and 11 M4 fixtures are marked `[Ignore]` pending the queued spec at `Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md`. **Two of your 5 manual shots specifically exercise that bug** so you can judge whether it's perceptible in real play.

## Pre-Phase-E setup

1. Switch to the `sim-baked-data-path` branch in your Unity project. From `C:/Users/cesar/GolfinRedux`:
   ```
   git checkout sim-baked-data-path
   ```
   (Or merge it locally if you prefer — `git merge sim-baked-data-path --no-ff` after fixing the local-vs-origin/main divergence.)
2. Open Unity. Wait for Library reimport.
3. Open `Assets/Scenes/Physics/LabScaffold.unity`.
4. `GOLFIN > Physics Lab > Hole Picker`. Load `Hole_01_Geo`.
5. Enter Play mode.

You should see `[PhysicsLab] Baked providers wired for Hole_01: 5 zone groups, OB mask=yes.` in the Console — confirms the sim is using baked data.

## The 5 manual shots

| # | Shot | Origin | Direction | Club | What you're checking |
|---|---|---|---|---|---|
| 1 | **Putt on green** | `Green 1` (placement dropdown) | any | Putter | Ball stays cleanly on the putting surface, rolls naturally, settles. |
| 2 | **Wedge from fairway** | `Fairway 1` (placement dropdown) | toward Green | Driver/Iron, ~50% pull | Ball flies a normal trajectory, lands on green/fairway, settles without falling through. |
| 3 | **Driver from `Green 1` aimed E (90° yaw)** ⚠ FAILING DIRECTION | `Green 1` (placement dropdown) | aim **east / +X axis** | Driver, full power | This is one of the 4 known-failing fixtures. Watch for the ball flying ~70 m and clipping into a rising slope where it visibly tunnels through the terrain (or doesn't — that's the question). The classifier here is correct; the bug is in `SimulateAirborne`'s edge-detector for near-tangential ground crossings. |
| 4 | **Wedge from `Bunker_1` edge aimed SE (135° yaw)** ⚠ FAILING DIRECTION | `Bunker 1` (placement dropdown), then nudge ball ~1.5 m southeast manually if the dropdown puts you at centroid | aim **southeast** | Wedge / Iron at high pitch | Same bug class — long-flight trajectory crosses rising terrain. Watch whether ball visibly punches through ground or just bounces and rolls naturally. |
| 5 | **Bunker escape** | `Bunker 1` edge | aim toward Green | Wedge | Sanity check — ball cleanly clears rim and lands on fairway/green. Validates that the bunker physics work in non-failing directions. |

## What "looks fine" means

- Ball never visibly enters the ground geometry.
- Ball never disappears below the terrain.
- Ball settles in a reasonable spot OR exits OOB cleanly.
- Trajectory looks plausible — no spontaneous re-launches, no infinite bounce, no `MaxDurationReached` log spam.

For shots 3 and 4, the physics simulation MAY:
- Show the ball flying through a hillside for a few frames mid-air (the bug)
- Have the ball hover at the slope surface and never settle (`MaxDurationReached`)
- Just look fine — the bug only manifests at very specific XZ + trajectory combos

If 3 or 4 looks visibly wrong, that's a real player-facing problem and the queued spec should activate immediately. If they look fine to your eye even with the underlying bug present, Φ3 holds.

## Decision tree after the 5 shots

| Outcome | Action |
|---|---|
| All 5 look fine | `git checkout main && git merge sim-baked-data-path --no-ff && git push`. The pivot ships. The queued spec stays queued and activates per its own triggers (AI Caddie / public testing). |
| Shot 1, 2, or 5 fails | A non-airborne bug — pivot is incomplete. Stop, report which shot, Architect specs M5 on the same `sim-baked-data-path` branch (do NOT branch off again — pivot isn't done until this is settled). |
| Shot 3 or 4 fails visibly | The queued spec activates immediately. Architect specs M5 (the signed-distance level-detector fix from `Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md`) on the `sim-baked-data-path` branch. Re-test, then merge. |
| Shot 3 or 4 fails subtly (you notice it but it's not obviously broken) | Your call. Either merge (pivot delivers primary value) or activate queued spec. |

## Branch state

| Item | Value |
|---|---|
| Branch | `sim-baked-data-path` |
| Latest commit | (pending — written when M4 commits) |
| Pre-pivot tag | `pre-baked-pivot` (at `4ff6a472`) |
| Files touched (count) | ~25 |
| Tests | 228 EditMode total: 212 PASS, 16 Skipped, 0 FAIL |
| Test runtime | ~90 s |

## Reference for diagnostic spelunking

If a shot looks wrong:

- **Per-direction fixture results:** `Docs/DIAG/baked-pivot/M0-regression-*.md`, `M0-regression-WedgeFromBunkerEdge.md`
- **Height-agreement histogram:** `Docs/DIAG/baked-pivot/M2-height-agreement.md` (current state: 100/100 within 5 cm, mean 0.45 cm, max 1.6 cm — the architecture itself is solid)
- **Per-step CSV of a known failing shot:** `Docs/DIAG/baked-pivot/M3-failing-shots/DriverFromGreen-E.csv`
- **Queued sim-fix spec:** `Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md`
- **Milestone reports:** `MILESTONE_0_DONE.md` through `MILESTONE_4_DONE.md` in the same directory.
- **The active pivot spec:** `Docs/Specs/Active/SIM_BAKED_DATA_PATH.md`

## Notes for Architect

- M4 completion deviated from spec scope in two ways, both documented in `MILESTONE_4_DONE.md`:
  1. Bunker tests use wedge-from-edge instead of spec'd "driver in 8 directions" (per Issue 1 resolution from M3.5).
  2. Random fairway/rough tests are classifier+provider sanity checks (no shots fired) instead of "50 random shots" — the random shot path triggers the queued-spec airborne bug unpredictably and provided no signal beyond what the marked-Ignored fixtures already capture.
- 16 fixtures are Ignored across BakedPivotRegression + RealHoleTerrainTests, all linked to `AIRBORNE_GROUND_LEVEL_DETECTION.md`.
- Phase E adds shots 3 and 4 specifically to surface the queued bug to Cesar's eye, per Condition 3.
