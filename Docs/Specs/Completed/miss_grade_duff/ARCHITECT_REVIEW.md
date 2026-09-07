# Architect Review — `miss_grade_duff`

**Verdict: PASS** (2026-09-07, Architect, Cowork). Three items go to Cesar; one is a tuning number I got wrong in the spec.

## Checked

- Report against SPEC §4: every line measured, not asserted. Duff path proven end-to-end through the real grader → `CommitExternal` → `BallSimulation` (Pendulum/Needle/Free Swing) and through real `ClubHandleDragger` events in play mode (Flick, four bands, four distinct frames). `bot_difficulty.csv` md5 identical after re-running the sigma harness (D4 holds). `ShotAimParityTests` / `ShotControllerFlickGateTests` untouched. 2760/2765 with the two `UiMotion` failures being the known load-flaky pair (96/96 alone).
- Strings went through the importer correctly: PLAN 1 add / 5 change / 0 conflict, published `texts` v43, `--check` clean, bundled table re-imported.
- Deviations 1, 4, 5, 7 are correct calls (`FlickMath` in the Input assembly is the only place one copy of the thresholds can serve both consumers; `SCHEME_POPUP_FLICK_LINE2` and the two TIP rows needed the new words; builders fixed so a re-run cannot resurrect JUST/PERFECT).
- The `ShotCommand` NOTE is answered properly: nothing replays today, the struct is a stub nobody writes. No field added — right. Backlog row filed for `launchPitchScale` riding in the command when server re-simulation is built.
- Scene diff kept to 236/21 by reverting the layout-group churn — good hygiene, worth keeping as the pattern.

## Where the spec was wrong (mine)

`MissPowerMul` is a **velocity** multiplier and carry goes roughly with v², so "0.20 ≈ 20 % of carry" in my CSV note was arithmetic I did not do. The implementer measured it: 0.20 + the flattened launch = **6.0 yd of 329 (1.8 %)**. Sorry — the spec prose set the wrong expectation. The knob is right, the seed is arguably too low; see Cesar item 1.

## Cesar decisions

1. **How far should a duff go?** Shipped: 6 yd on a driver, 4 yd on a wedge — nearly a whiff. A real fat shot goes 10–40 yd. Architect recommends **`MissPowerMul` 0.20 → 0.40** (≈ 20–25 yd on a driver, ~10 yd on a wedge after the flattened launch), leaving `MissLaunchPitchScale` 0.35 and `PuttMissPowerMul` 0.30 (measured 28.6 % of the putt — right where D3 asked). One csv key, no code.
2. **Deactivate the four retired string rows** (`SHOT_GRADE_JUST / PERFECT / SHANK / MISS`) in the admin dashboard — the content pipeline's I6 forbids deletion from the tooling, and the implementer correctly refused to hand-delete published rows. Until then they are dead weight, not player-visible.
3. **Needle perfect zone blue → PURE green** (D8): the flagged one-literal veto. Architect's view: keep green — one grade palette across all four schemes is the point of D7/D8.

## STATUS

`ARCHITECT_REVIEW_PASS`. Cesar's approval → `DONE` → folder to `Docs/Specs/Completed/`. If he takes item 1, Code changes the one csv row in the same commit that closes the task.
