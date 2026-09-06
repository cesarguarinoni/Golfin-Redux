# ARCHITECT_REVIEW — `scheme_needle` (Tap Timing)

**Verdict:** PASS. Built `d54468b6c`, closed `7369ecb18`; folder already in Completed.
**Reviewed:** 2026-09-05 against the commit, not the report.

## Verified in the codebase
- `ShotController.cs`: zero diff (`git diff HEAD~2 --stat`). The driver uses `BeginExternalDrag(ownsTiming:true)` / `CancelExternalDrag` / `CommitExternal` only (`NeedleSchemeDriver.cs` l.237/288/362) — the seam held with no additions, which is what Spec 0 was for.
- The seven Pendulum carry-overs are present in the first build (own `NeedleSweepSec*` constants in seconds-per-sweep, `WindowScaleForPower` from the peak, club hidden at commit, derived ring radii, `NeedleColors` pre-compositing, peak-power commit, config-derived distances). `MaxNeedleStepSeconds = 1/30` clamps the hitch-frame jump the acceptance run caught.
- `PendulumGradePop` → `SchemeGradePop` rename is type-name-only in the Pendulum driver and builder.

## Three findings worth keeping
1. The arc must NOT be told about `Resolving` — `CommitExternal` reaches it synchronously and the shared fading view would drop the result readout two frames after the tap. Recorded as a rule for Free Swing's analyzer chip (spec header).
2. Find-by-name across inactive scheme roots silently resolved the Pendulum's `GradeText`; per-scheme unique names are now the convention (`Needle*`). Free Swing follows it.
3. Putt mode was entered by setting `ShotController.IsPutt` (the production write) — not by playing to a green with a putter. Same caveat as Pendulum; covered by Cesar's device pass.

## Outstanding
On-device pass (Cesar). Bot `DriveBot` for this scheme = `bot_scheme_parity` Stage B. Nothing blocks `scheme_freeswing`.
