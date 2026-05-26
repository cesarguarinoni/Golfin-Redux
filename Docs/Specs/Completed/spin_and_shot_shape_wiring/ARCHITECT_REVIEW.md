# Architect Review — `spin_and_shot_shape_wiring`

> Written by `golfin-reviewer` at 2026-05-26 11:35 JST. Iteration **2** review (iter-1 went BACK_TO_IMPLEMENTER via self-review).

## Verdict

**`ESCALATE_TO_CESAR`** — Two open items are spec-vs-physics conflicts the implementer cannot resolve unilaterally. All code-correctness and process items PASS. Implementer's escalation is sound; routing back would be unproductive looping.

STATUS → `ARCHITECT_REVIEW_ESCALATE`.

## Independent visual scan (pixel-only, no reports read first)

Five "armed" frames (s02, s03, s05, s07, s09, s11) are visually identical: same tee view, same character HUD (Lv 80 Elizabeth, Lomond, Par 5), same DRIVER tile, same two flag indicators on the fairway, only the TURN counter increments 1→2→3→4→5. `ResetToTee()` is unambiguously working. Of the five "landed" frames: s04 CENTER shows the ball at rest on a paved/mulch strip along the right edge of fairway, far from tee, no aim-arc HUD — believable rest position. s06 TOPSPIN shows the ball back AT the tee with the full white aim-arc HUD active ("100% Hit FUL" ring around ball) — this is a post-reset frame, not the actual rest position. s08 BACKSPIN shows the ball deep in left-side foliage off-fairway, no aim arc — believable rest position (the ball overshot left into trees). s10 DRAW shows the ball back at the tee with the aim-arc HUD — post-reset frame. s12 FADE shows the ball back at the tee with the aim-arc HUD — also post-reset (this one is legit, the ball went OOB so the OB handler reset to tee). The captioned MP4 shows clear "Stroke N: LABEL\nspinInput=(x, y)\nspinRate=n/a" overlays at the bottom of each stroke segment; captions render in white text on dark band, easily legible — caption rendering bug from iter-1 is fixed.

## Figma side-by-side

Not applicable — this is a physics-behavior task with a bot-driven visual gate (Q5 lock). No Figma reference, no UI layout claims.

## Bbox verification

Not applicable — no containment claims ("X inside Y") in SPEC or report.

## Scene-mutation audit

`git diff --name-only HEAD -- "*.unity" "*.prefab" "*.asset"` returns **empty**. `git status --short` confirms no scene/prefab/asset files in the working tree. The implementer modified 16 code/CSV/Python files, all under `Assets/Scripts/`, `Assets/Resources/Gameplay/controls.csv`, or `Docs/Scripts/build_bot_video.py`. Hygiene PASS.

## Capture-helper compliance

Bot scenario uses `BotDriver.Capture()` → CaptureCore path. No new `*Context.cs` files under HUD; no `FakeMidAim`/`FakeReset` maintenance burden. Compliant.

## Production-flow capture verification

`BotDriver.FireDriverShot` uses `BeginExternalDrag` / `SetExternalPower` / `EndExternalDrag` on the production `ShotController` (confirmed via `git diff Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs`). The `PendingSpinInput` flows through `CommitFlick` → `ShotInputBuilder.Build`, which is the production code path. NOT `FireDebugShot`. The 5 `[Build]` log lines in `live_stat_log.txt` prove the spin input reached the physics builder (CENTER=(0,0), TOPSPIN=(0,+1), BACKSPIN=(0,-1), DRAW=(-1,0), FADE=(+1,0)). Production-flow capture confirmed.

## Independent verification of body-frame projections

Tee position from `[TeeDiag]`: `(219.43, 11.58, 34.73)`. All 5 strokes share `finalVel=(-89.57, 17.73, -21.34)`, `aimYaw=-2.908rad`. Horizontal velocity magnitude = √(89.57² + 21.34²) = 92.07. Forward unit (XZ) = `(-0.973, 0, -0.232)`. Right body-frame unit = forward × up = `(0.232, 0, -0.973)`.

Re-derived from the canonical `live_stat_log.txt` (4th run, iter-2, slope=0.8 — NOT slope=1.5):

| Stroke | Rest pos (world) | Δ from tee (X, Z) | Forward (m) | Right (m, +=right) |
|---|---|---|---|---|
| 1 CENTER | (-112.9, 6.3, -44.5) | (-332.33, -79.23) | **341.8** | 0.0 |
| 2 TOPSPIN | (-31.1, 7.1, -25.0) | (-250.53, -59.73) | **257.7** | 0.0 |
| 3 BACKSPIN | (-139.1, 7.0, -50.8) | (-358.53, -85.53) | **368.7** | 0.0 |
| 4 DRAW | (-106.1, 8.0, -9.9) | (-325.53, -44.63) | **327.1** | **-32.1 (LEFT)** |
| 5 FADE land | (37.0, 7.3, -21.6) [OOB] | (-182.43, -56.33) | **190.6** (land) | **+12.5 (RIGHT, at land)** |
| 5 FADE rest | (219.4, 11.5, 34.7) [OB-reset = tee] | (0, 0) | 0 | 0 |

Land-vs-rest split (using `[Land]` and `[Rest]` log pairs):

| Stroke | Carry (Land→Forward) | Total (Rest→Forward) | Rollout |
|---|---|---|---|
| 1 CENTER | 308.7m | 341.8m | 33.1m |
| 2 TOPSPIN (slope=0.8) | 226.6m | 257.7m | 31.1m |
| 3 BACKSPIN (slope=0.8) | 343.3m | 368.7m | 25.4m |
| 4 DRAW | 316.0m | 327.1m | 11.1m |
| 5 FADE | 190.6m (land only) | n/a | n/a |

Both self-reviewer's iter-1 numbers AND implementer's iter-2 numbers are internally consistent — they used different slopes (1.5 in iter-1 data, 0.8 in iter-2 data). The corrected body-frame projections in iter-2 are correct; the iter-1 mistake was reading world-X as "forward".

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | Three deviations (Vector2→fp params, SpinContext.Reset via ShotConeView, fpMath half-period reduction) are all responses to genuine asmdef constraints. None introduces backdoor refs. |
| Pattern adherence | PASS | `SpinAndShapeVisualGate` mirrors `LiveStatProviderVisualGateHigh` structure; `Rotate` follows existing `fpMath` static-helper pattern; CSV/Loader/Config additive change matches existing rows. |
| Duplicated logic | PASS | No duplication. `Rotate` is new (Rodrigues' formula not previously available). `[Land]`/`[Rest]` logging reuses BotDriver.LogStep. |
| Spec intent | PARTIAL | The CODE matches Q-locks 1–5 faithfully. The two open items (TOPSPIN carry, FADE OB) are gaps between Q-lock-specified behavior and visual-gate-criterion expectations — see open questions below. |
| Cross-feature impact | PASS | `Build` signature extension is fully backward-compatible (4 defaulted params). Existing 344 tests PASS unchanged → 356/0/3 confirmed. No regressions in stat/aero/ball pipelines. |
| Latent bugs | LOW | `ShotConeView.HandleStateChanged` bridging `SpinContext.Reset` is a workable but slightly indirect path; if `ShotConeView` is ever decoupled from ShotState transitions the reset would silently break. Belt-and-suspenders mitigation (ShotController.TransitionToIdle also clearing PendingSpinInput) is already in place. Acceptable. |
| Tests | PASS | 356 PASS / 0 FAIL / 3 SKIP — baseline +12 = exactly 8 spin tests + 4 Rodrigues tests as required. |

## Visual fidelity verdict (bot video + per-stroke logs)

| Criterion (from SPEC § Acceptance) | Iter-2 result | Pass/Fail |
|---|---|---|
| Stroke 1 CENTER baseline, no curl | Right body-frame = 0.0m, fwd 341.8m | **PASS** |
| Stroke 2 TOPSPIN: lower trajectory + Δcarry ≥3m or Δtotal ≥8m **further** | Δtotal = **−84.1m shorter** (slope=0.8); slope=1.5 would be −127.8m | **FAIL** (spec-vs-physics; see Q1) |
| Stroke 3 BACKSPIN: higher trajectory + Δrollout ≤−3m | Δrollout = **−7.7m** (slope=0.8); satisfies criterion | **PASS** |
| Stroke 4 LEFT_DRAW: curves left, Δlateral ≥5m left | Body-frame right = **−32.1m** | **PASS** (sign-convention note: spec wrote "lateral.z visibly negative", actual world Δz=+34.6m, but body-frame intent is unambiguously satisfied) |
| Stroke 5 RIGHT_FADE: curves right, Δlateral ≥+5m | Body-frame right at land = **+12.5m**; rest = OB-reset to tee | **PASS at landing / FAIL at terminal** (see Q2) |
| Same character + driver across all 5 strokes (only spin differs) | velMagnitude/clubVel/loft/aimYaw identical across strokes 1–4; stroke 5 power=0.7 instead of 1.0 | PASS-WITH-NOTE (FADE deviates on power; reduced to keep ball nearer fairway, still went OB) |

## Specific FAIL items

These two are the implementer's escalation. Both are genuine spec-vs-physics conflicts the implementer cannot resolve without architect/Cesar judgment. Routing back to implementer would not produce a different answer.

### Q1 — TOPSPIN carry criterion is incompatible with current Magnus physics

**Spec § Acceptance, item 13 says:** TOPSPIN must show "Δ carry ≥3m or Δ total ≥8m **further**" than CENTER.

**Physics reality (verified independently against `AeroModel.cs:88` `liftDir = Cross(spin.Axis, vRelHat)`):** Backspin around the right-vector axis produces upward Magnus lift. Reducing backspin reduces lift → ball drops earlier → shorter carry. Flipping sign to true topspin produces *downward* Magnus force → significantly shorter carry. There is **no value of `SpinMagScaleSlope`** that makes TOPSPIN carry farther than CENTER in this model, because the ground-roll layer (`BallSimulation.RunBouncePhase` / `RunRollPhase`) does not transfer forward angular momentum to forward linear velocity — it just applies friction.

Real-world topspin "runs farther" because of ground-roll spin-velocity coupling, which this physics model does not implement. That's a model design decision, not a tuning miss.

Architect (Cesar) judgment needed. Options the implementer enumerated, my recommendation in **bold**:

- **(a) Change criterion to "lower apex (peak Y) than CENTER"** — physically correct, visually verifiable from the video. Trivial to compute by adding peak-Y logging in BallSimulation. *Recommended* — keeps Q2-lock (sign-flip slope=1.5) intact, matches what the physics actually does, and "lower arc" is intuitive even for non-golfers in a casual mobile game.
- (b) "Δ rollout ≥X further" — untested with slope=1.5; with slope=0.8 rollout was 31.1m vs CENTER 33.1m (slightly *less*). Unlikely to satisfy without (c).
- (c) Add forward-spin → ground-roll coupling to `BallSimulation` — out of scope here, big physics change, separate ticket.
- (d) Accept that topspin = shorter carry, document as intended, remove the "≥8m further" clause. Acceptable fallback if Cesar wants to defer trajectory-aware physics work.

### Q2 — FADE goes OB before producing a clean in-bounds terminal

**Spec § Acceptance, item 15 says:** FADE must show "Δ lateral ≥+5m vs CENTER terminal."

**Physics reality:** At power=0.7, FADE produces +12.5m body-frame right lateral at first-bounce — **the criterion IS satisfied at landing**. But the ball lands on an OOB surface (the rough/edge), the OB handler resets to tee, and the "terminal" position is therefore (tee). The curl direction and magnitude are unambiguous; the measurement convention (terminal-at-rest vs first-bounce) is what trips.

Architect (Cesar) judgment needed. Options, my recommendation in **bold**:

- (a) Drop power to 0.5 for FADE only — risk: less curl, may not satisfy ≥5m criterion; but might still curl >5m at 17° tilt over a short flight.
- (b) Yaw-offset FADE aim left so curl ends in fairway — adds scenario-specific aim adjustment, not a general fix.
- **(c) Accept first-bounce lateral as evidence; amend spec criterion to "Δ lateral ≥+5m vs CENTER at first ground contact (or terminal if in-bounds)."** *Recommended* — the spec intent (does FADE curl right?) is unambiguously satisfied; the +12.5m at land is more evidence than the spec asked for. Amending the criterion wording costs nothing and avoids forcing a contrived in-bounds setup.
- (d) Reduce `SpinMaxTiltRad` to 0.15 — would reduce DRAW lateral too (currently -32.1m → ~-16m at 0.15); still ≥5m but less visually dramatic. Tuning change affects all FADE/DRAW behavior, not just this scenario.

### Note on "landed" screenshot timing (non-blocking observation)

The self-reviewer flagged in iter-1 that s06 TOPSPIN, s10 DRAW, s12 FADE "landed" stills show post-reset (ball back at tee with aim-arc HUD). Iter-2 added a 2-second settle wait and reset-after-capture loop ordering, but the visual evidence shows s06 and s10 are still post-reset frames (s04 CENTER and s08 BACKSPIN landed stills ARE proper at-rest frames). This is a capture-timing imperfection, NOT a hard FAIL: the `[Land]` and `[Rest]` log entries in `live_stat_log.txt` provide canonical position data, and the captioned video is the primary visual gate per Q5. Cesar's visual review should weight the video > stills + body-frame projections > raw stills. Flagging it so it's not overlooked; not blocking ESCALATE.

## Why ESCALATE and not FAIL-back

Per `golfin-reviewer.md`: "ESCALATE only when the spec contradicts the design intent / Cesar specifically needs to make a judgment call." Both Q1 and Q2 fit exactly:

- Q1 is a genuine spec-vs-physics conflict. The implementer's analysis is correct: no slope value satisfies "topspin further". The physics model does not implement the necessary spin-to-ground-roll coupling. This is a design call (amend criterion or expand physics model).
- Q2 is a measurement-convention call (first-bounce vs at-rest). The physics is working as the Q-locks specified; the spec language was just slightly under-specified about OB handling.

Routing back to the implementer with "retune until it works" would loop indefinitely (iter-1 already tried slope=1.5, iter-2 tried slope=0.8 — both fail). The implementer has explored the tuning space and surfaced the right architecture-level question.

The CODE in this submission is solid:
- 16 files modified, ~625 insertions, all sound engineering
- 12 new tests, 0 regressions (356 PASS / 0 FAIL / 3 SKIP)
- 3 documented deviations all justified (Vector2→fp asmdef constraint, SpinContext.Reset via ShotConeView circular-asmdef workaround, fpMath half-period reduction as side-effect fix of a queued ticket)
- Zero scene/prefab/asset mutations
- Production drag-fire path verified; SpinContext flow proven end-to-end in `[Build]` log lines
- Captioned video renders correctly (caption rendering bug from iter-1 fixed)

If the criteria are amended per Q1(a) + Q2(c), this becomes a clean PASS without code changes.

## Open questions for Cesar

**Q1: Amend TOPSPIN criterion to "lower apex" (recommendation a), or accept "shorter carry as intended" (recommendation d), or commit to a physics model expansion for ground-roll spin coupling (recommendation c, big effort)?**

The current physics is doing exactly what the Q2-lock specified (sign-flip allowed at slope=1.5, magScale=-0.5 at +Y=+1). The fact that this produces shorter carry is a consequence of the Magnus model with no ground-roll coupling. Real golf topspin runs farther because of ground spin-velocity transfer that this engine doesn't model. Calling this out so you can decide whether to amend the v1 visual-gate criterion (cheap) or expand the physics model (separate ticket).

My architectural take: option (a) "lower apex" is the right call for v1. Adding a `peakY` metric to BallSimulation is a sub-hour change and gives a clean visual-gate signal. Defer the ground-roll spin coupling to a separate ticket if/when it becomes a gameplay need.

**Q2: Accept first-bounce lateral as FADE evidence (recommendation c), or rerun with reduced power / yaw offset for a clean in-bounds terminal?**

The +12.5m body-frame right at first-bounce IS the curl evidence. The OB-reset terminal is a measurement artifact. Cleanest fix: amend the criterion wording from "vs CENTER terminal" to "vs CENTER at first ground contact (or terminal if in-bounds)." No code rerun needed.

If you'd prefer a clean in-bounds run for the video record, the implementer can rerun with power=0.5 or a yaw offset; that's a 5-minute change but adds an iteration.

**Q3: BACKSPIN measured at slope=0.8 produced Δrollout=-7.7m (satisfies ≤-3m). Spec slope is 1.5. Confirm waive-rerun, or require a fresh slope=1.5 measurement?**

Implementer's recommendation (waive — slope=1.5 would produce *more* rollout reduction, same direction) is architecturally sound. A 5th bot run for confirmation costs ~15 min if you want belt-and-suspenders evidence at the canonical slope.

## Lessons captured (for `tasks/lessons.md` after Cesar resolves)

- **Spec criteria that depend on physics behavior need a physics-feasibility check before the SPEC is authored.** The "topspin = farther carry" criterion implicitly assumed a physics model that includes spin → ground-roll coupling. The current engine doesn't. Visual-gate criteria should be derived from "what the current physics does", not from real-world expectations.
- **Visual-gate body-frame conventions need an explicit projection-axis lock.** The spec wrote "lateral.z visibly negative" but the velocity was aimed in -X/-Z, making world-Z and body-frame-right different signs. Future visual-gate specs should explicitly say "body-frame right" or "world Δz" to remove ambiguity.
- **OB handling needs to preserve first-bounce position for visual-gate purposes.** When a shot goes OOB, the at-rest reset to tee destroys the lateral measurement. The implementer's `[Land]` log line captures the right data; specs that care about lateral should evaluate at-land, not at-rest, for OB cases.

## Cesar's final approval

**Approved 2026-05-26 10:20 CEST.** Decisions:

- [x] **Q1 = (a) lower apex** — amended SPEC item 13 wording (`Stroke 2 TOP_TOPSPIN: visibly lower apex than CENTER in flight; verified from captioned video`). No `peakY` numeric threshold for v1 — Magnus sign-flip direction is mechanical, visual difference is the gate. Numeric threshold deferred to P3 follow-up `ball_simulation_peak_y_logging` if/when a future visual gate wants it.
- [x] **Q2 = (c) accept first-bounce lateral** — amended SPEC item 15 wording (`Δ lateral ≥+5m vs CENTER at first ground contact (or terminal if in-bounds)`). Iter-2 +12.5m body-frame right at land is canonical FADE evidence; OB-reset terminal is a measurement artifact, not a physics failure.
- [x] **Q3 = waive slope=1.5 rerun** — monotonicity argument holds: slope=0.8 produced −7.7m rollout (satisfies ≤−3m); slope=1.5 produces *more* backspin magnitude → steeper descent → more rollout reduction. Direction is locked; magnitude only grows. No 4th rerun.
- [x] **Decision set = (Q1=a) AND (Q2=c) AND (Q3=waive) → task APPROVED with criterion amendments.** No further implementer iteration. STATUS → `DONE`. Folder moves to `Docs/Specs/Completed/`.

**Follow-ups filed at close-out:**
- `bot_landed_capture_timing_fix` (P3) — s06/s10 "landed" stills still show post-reset frames despite iter-2 2-second settle. Bot video + `[Land]`/`[Rest]` logs are canonical, but the stills should match. Likely culprit: capture polling on `[Land]` rather than `[Rest]`, or BallStateMachine transitioning to a non-AtRest state when OB-reset is involved before the capture poll fires.
- `ball_simulation_peak_y_logging` (P3) — add `peakY` tracking to `BallSimulation` so future visual gates can use numeric apex thresholds (would have made Q1 a closed numeric criterion instead of "visually verified").

**Lessons captured:**
- New Lesson X (to be added to `tasks/lessons.md`): **Spec visual-gate criteria must be derived from what the current physics model actually does, not from real-world physical expectations.** The "topspin = farther carry" criterion implicitly assumed ground-roll spin-velocity coupling that the current `BallSimulation` doesn't implement. Future visual-gate SPECs that include numeric thresholds should include a physics-feasibility check on the current model before locking the threshold.
- New Lesson Y (companion): **Visual-gate body-frame conventions need an explicit projection-axis lock.** The SPEC wrote "lateral.z visibly negative" but the velocity was aimed in −X/−Z, making world-Z and body-frame-right different signs. Future visual-gate SPECs should explicitly say "body-frame right" or "world Δz" to remove ambiguity.
- New Lesson Z (companion): **OB handling needs to preserve first-bounce position for visual-gate purposes.** When a shot goes OOB, the at-rest reset to tee destroys the lateral measurement. The implementer's `[Land]` log line captures the right data; specs that care about lateral on OB-prone shots should evaluate at-land, not at-rest.

(Architect to merge Lessons X/Y/Z into `tasks/lessons.md` at close-out.)
