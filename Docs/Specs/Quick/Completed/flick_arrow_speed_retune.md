# Quick spec — Flick timing arrow too fast for low-Club-Control characters (F17)

**Status:** DONE 2026-09-09 — implemented + `Golfin.Gameplay.Tests` 757/0/0 unfiltered; approved by Cesar.
Changelog entry **F17** in `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md`. (Was SPEC_READY 2026-09-09, Architect/Cowork; Cesar decided the numbers.)
**Track:** Gameplay Polish. **Size:** Quick — two constants + a floor, mirrored in three places, one changelog entry.

## Problem (Cesar, 2026-09-09)
"The timing arrows in Flick control are too fast when starting the game with weak characters."

Starter Commons (`Assets/Data/Characters.csv`: James CC 6, Olivia CC 7) run the Flick arrow at
`2.0 − 0.03·CC ≈ 1.8 Hz` — one pass through the cone every **0.55 s**. That is the exact speed
Cesar rejected on the Pendulum marker on 2026-09-05 ("moving way too fast", 1.82 Hz), which got its
own `PendulumBaseHzAtCC0 = 1.0`. Flick kept the F13 base of 2.0. Third pass on this ladder
(F11 → F13 → F17); the previous two moved the base 3.0 → 2.0 for the same complaint.

## Change — `ControlsConfig.Default` is runtime truth; `controls.csv` and the loader are mirrors
Halve the base, keep F13's ladder SHAPE (2.5× spread CC 0 → 50), lower the floor to the new CC-50 speed.

| Key | F13 (today) | **F17** | Where |
|---|---|---|---|
| `BaseArrowSpeedHzAtCC0` | 2.0 | **1.0** | `ControlsConfig.cs` `Default` (~line 363) + `Assets/Resources/Gameplay/controls.csv` line 16 |
| `ArrowSpeedHzPerCC` | −0.03 | **−0.012** | same, line 17 |
| `MinArrowSpeedHz` | 0.5 | **0.4** | same, line 18 |

Resulting ladder (swing; putt = × `PuttArrowSpeedMultiplier` 0.8, unchanged):

| Club Control | Hz | s per pass | putt s per pass |
|---|---|---|---|
| 0 (fallback) | 1.00 | 1.0 | 1.25 |
| 6–7 (starter Commons) | 0.92 | 1.1 | 1.4 |
| 25 (Common cap) | 0.70 | 1.4 | 1.8 |
| 50 (Supreme cap) | 0.40 | 2.5 | 3.1 |

Raw line goes negative past CC = 1.0/0.012 = **83.3** (was 66.7); the floor still guards it. The
floor equals the calibrated CC-50 speed so it stays a no-op on the reachable range, as F13 intended.

Untouched: `MaxCleanPassesAtCC0`, `CleanPassesPerCC`, `MaxTotalPasses`, `DegradationYawDegPerPass`,
`PuttArrowSpeedMultiplier`, the timing slab bands (`TimingBand*Y01`) and every Pendulum/Needle/
FreeSwing constant. The Pendulum already has its own base — do not re-couple them.

## Files
1. `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` — the three `Default` values + refresh the
   trailing F13 comments to F17 (keep the F13 history in the comment, one line each).
2. `Assets/Resources/Gameplay/controls.csv` — lines 16–18: value + prepend `F17 (2026-09-09): …`
   to the note the way F13 did (keep the earlier history in the note).
3. `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` — new **F17** entry at the top, same shape as F13:
   task, files, reason (quote Cesar), the table above, the tests touched, and the
   completability line ("Unaffected — bots bypass `TickArrow`; `BotSwing` schemes carry their own
   sigma, not the arrow").
4. `Docs/AI_CONTEXT.md` — one line under the controls/tuning section.

## Tests (EditMode, `Golfin.Gameplay.Tests`)
- `ShotControllerTests.Test11_ArrowSpeed_MonotonicDecreasingWithCC` — relational, must stay green.
- `ShotControllerTests.Test12_ArrowSpeed_FloorClamp_StaysPositiveBeyondStatCaps` — precondition
  `raw(CC=100) < 0` still holds (1.0 − 1.2 = −0.2); `MinArrowSpeedHz ≤ Base` holds (0.4 ≤ 1.0).
- `ShotControllerTests.OnePassDtAtCC0` derives from the default — no literal to update.
- `ShotControllerPuttModeTests.F1_IsPutt_ArrowsSlowedByMultiplier` — relational; refresh the
  comment numbers (putt 0.8 Hz < swing 1.0 Hz at CC 0).
- `ShotControllerSeamParityTests` / `ShotTimingPowerTests` / `ShotTimingTelemetryTests` inject
  their own config — unaffected.
- Grep `2.0f\|-0.03f\|0.5f` near the three fields and `ClubControlArrowDemoRecorder` for any
  hard-coded expectation of the old ladder; report what you find, fix only literal mirrors.

## Acceptance
- Run the `Golfin.Gameplay.Tests` assembly UNFILTERED (filtered runs mask failures).
- Manual, Cesar on device: new account / starter Common, Flick scheme, driver off the tee —
  the arrow is trackable and PURE is reachable on the first pass; a Supreme (CC 50) still reads
  clearly slower than the starter. Putt on the green: arrow still slower than the swing.

## Out of scope (backlog if wanted)
- The starter's **1 clean pass** before yaw degradation (`round(1 + 6·0.08) = 1`) — a separate
  lever; the slower arrow already gives that first pass twice the wall time.
- Any change to the timing slab band sizes (`TimingBandGreenY01` etc.).
- Pendulum / Needle / Free Swing speeds (each already has its own constants).
