# Self-Review — `spin_and_shot_shape_wiring`

> Written by `golfin-self-reviewer` at 2026-05-26 09:10 CEST. Iteration **1** of self-review for this task.

## Verdict

**`BACK_TO_IMPLEMENTER`** (FAIL)

Reason: The visual gate criteria for TOPSPIN (item 12), BACKSPIN (item 13), and FADE (item 15) are NOT met by the captured data when the data is interpreted correctly. The implementer's PASS justifications relied on misreading absolute world-coordinate deltas as forward-distance deltas, and on treating the OB-reset position (= tee position) as the FADE terminal. The video also has no visible captions. Code implementation of the math itself looks faithful to the Q-locks; the failure is in the visual-gate evidence chain, not the C# spin logic.

## Step 1 — Independent pixel scan (screenshots only, before reading anything else)

Twelve screenshots in `screenshots/` follow a numbered sequence: home → gameplay_armed → five (armed, landed) pairs.

- **s01_home:** Logo/splash screen with "PLAY" + "CREATE ACCOUNT" + "LOGIN", character holding a club mid-swing in a sand bunker.
- **s02_gameplay_armed → s03 → s05 → s07 → s09 → s11 (all "armed" frames):** Visually IDENTICAL — same tee view, same camera, same ball-on-tee with the white aiming line, same character portrait + HUD ("Lv 80, TURN N, LOMOND, HOLE 1 – REGULAR, PAR 5"), same two club tiles at the bottom (IRN N / DOLFIN on the left, RIM/HITS/DRIVER on the right). The TURN number increments 1→2→3→4→5 across the armed frames. ResetToTee is working — every stroke starts from the exact same setup.
- **s04 stroke1_center_landed:** Ball at rest on a paved cart path / mulchy strip on the right edge of a fairway. No ball-arc HUD visible. Trees on the right. Tee perspective is gone — the camera has moved/zoomed.
- **s06 stroke2_top_topspin_landed:** Ball clearly back AT the tee with the full white aim-arc HUD active. The "100% Hit FUL" ring is around the ball. HUD reads "TURN 3". This is a POST-RESET frame, not the actual landing point.
- **s08 stroke3_bottom_back_landed:** Ball deep in heavy foliage / a dense tree thicket — clearly off-fairway and inside trees on the left. No HUD arc visible.
- **s10 stroke4_left_draw_landed:** Ball back at the tee with full aim-arc HUD ring, HUD reads "TURN 5". POST-RESET frame.
- **s12 stroke5_right_fade_landed:** Ball back at tee, HUD shows "TURN 7" (jumped from 6 because of next-shot advance), full aim-arc HUD visible. POST-RESET frame.

So three of the five "landed" screenshots show the ball at the tee post-reset, not the actual rest position. Only stroke 1 CENTER and stroke 3 BACKSPIN show real landing positions visually.

## Step 2 — Comparison to Figma reference

Not applicable — this is a physics-behavior task, not a UI fidelity task. No Figma reference exists in `SPEC.md § Reference` because the deliverable is a captioned MP4 + per-stroke terminal positions in the bot log, not a screen layout.

## Step 3 — Capture-helper compliance

- **Screenshot provenance:** Screenshots were captured via `BotDriver.Capture()` → CaptureCore path per `IMPLEMENTER_REPORT.md`. Compliant with CLAUDE.md screenshot rules.
- **CaptureHelper maintenance:** No new `*Context.cs` files added under HUD; nothing to maintain.

## Step 4 — Scene-mutation audit

`git diff --name-only HEAD -- "*.unity" "*.prefab" "*.asset"` returns empty. No scene/prefab/asset mutations. Implementer's hygiene PASS confirmed. ✓

## Step 5 — Verifying terminal positions (the hard analysis)

**Tee position** (from history.log `[TeeDiag]`): **`(219.43, 11.58, 34.73)`**. Same for all 5 strokes (ResetToTee confirmed).

All 5 strokes shared identical velocity (`finalVel = (-89.57, 17.73, -21.34)`, yaw `-2.908 rad`). The aim direction in the XZ plane is approximately `(-89.57, -21.34)`, magnitude 92.07 → forward unit vector `fwd ≈ (-0.973, 0, -0.232)`. The "right" body-frame vector (right = fwd × up) is `right ≈ (+0.232, 0, -0.973)`.

Computing **tee-relative** terminal positions and projecting onto fwd/right:

| Stroke | Spin | Terminal (world) | Tee-relative (ΔX, ΔZ) | Forward (m) | Right (m) |
|---|---|---|---|---|---|
| 1 CENTER | (0, 0) | (-112.9, 6.3, -44.5) | (-332.3, -79.2) | **341.7** | 0.0 |
| 2 TOPSPIN | (0, +1) | (11.3, 8.0, -14.9) | (-208.1, -49.6) | **213.9** | -0.1 |
| 3 BACKSPIN | (0, -1) | (-122.4, 6.6, -46.8) | (-341.8, -81.5) | **351.4** | -0.1 |
| 4 DRAW | (-1, 0) | (-106.1, 8.0, -9.9) | (-325.5, -44.6) | **327.0** | **-32.0 (LEFT)** |
| 5 FADE | (+1, 0) | (219.4, 11.5, 34.7) [OB] | (0.0, 0.0) | **0.0 (OB reset = tee)** | 0.0 |

Key implications:
- TOPSPIN forward distance = **213.9 m vs CENTER 341.7 m** → **127.8 m SHORTER** (not further).
- BACKSPIN forward distance = **351.4 m vs CENTER 341.7 m** → **9.7 m FURTHER** (not stops-faster).
- DRAW lateral = **-32 m LEFT in body frame** — correct direction for a draw ✓.
- FADE terminal = `(219.4, 11.5, 34.7)` which is **the tee position itself**. Terminal type is `OB` (out of bounds), flight time only 6.8s vs ~14-17s for other strokes. The bot's OB-handler reset the ball to tee and reported that as terminal. **There is no valid terminal data point for FADE.**

The implementer's interpretation `ΔX = +124.2m further carry` for TOPSPIN was reading the X-axis world delta as if X = "forward". X is not forward — the velocity is in (-X, +Y, -Z) so forward is ~ -X. A more-positive X means LESS forward distance, not more.

## Step 6 — Bbox check

Not applicable — no containment claims (no "X inside Y" UI assertions).

## Step 7 — Production-flow capture check

Bot scenario fires shots via `BotDriver.FireDriverShot()` which uses `BeginExternalDrag` / `SetExternalPower` / `EndExternalDrag` on the production `ShotController` (verified in the diff). This IS the production drag-fire path, not `FireDebugShot`. ✓ Production-flow capture present.

## Step 8 — Code-level deviation review

Three documented deviations:

1. **`UnityEngine.Vector2` → `fp spinInputX/Y`** in `ShotInputBuilder.Build`. Forced by `noEngineReferences: true` on the Stats asmdef. Justified, behavior identical, all callers updated.
2. **`SpinContext.Reset()` bridged via `ShotConeView`** instead of `ShotController.TransitionToIdle()`. Forced by circular asmdef boundary (Input doesn't ref UI). Belt-and-suspenders: `ShotController.TransitionToIdle()` also clears `PendingSpinInput` locally. Justified.
3. **`fpMath.Cos/Sin` half-period reduction added** to make `Rotate_PiAroundY` accurate. Surfaced by the new tests; resolves the queued `fpMath.Cos/Sin range-reduction repair` ticket as a side-effect. Acceptable — this is a real correctness fix in a function that was previously inaccurate near π.

None of the three deviations are red flags. All three are reasonable engineering responses to constraints the SPEC didn't anticipate.

## Checklist verification

| Item | Implementer said | Self-reviewer says | Notes |
|---|---|---|---|
| ControlsConfig + CSV + Loader | PASS | **CONFIRMED-PASS** | `git diff` confirms all three files have correct edits. Round-trip values match Q-lock defaults. |
| `fpMath.Rotate` + 4 tests | PASS | **CONFIRMED-PASS** | Diff shows Rodrigues' formula correctly implemented; 4 Rotate tests exist (`Rotate_ZeroAngle`, `Rotate_PiAroundY_NegatesXAndZ`, `Rotate_HalfPiAroundZ_TurnsXIntoY`, `Rotate_PreservesLength`). |
| `Build` signature + new params | PASS | **CONFIRMED-PASS** | Diff confirms 4 new defaulted fp params. Existing callers unedited; 359/356/0/3 reported tests support no regressions. |
| Existing 344 PASS hold (no regression) | PASS | **CONFIRMED-PASS** | 356 passing tests reported (baseline 344) — gain of 12 = exactly 8 new ShotInputBuilder + 4 new fpMath.Rotate. Math checks out. |
| `ShotInputBuilderSpinTests` ≥8 PASS | PASS | **CONFIRMED-PASS** | File exists at claimed path (`ls` confirmed), 8 `[Test]` annotations counted. |
| `fpMathTests.Rotate*` ≥4 PASS | PASS | **CONFIRMED-PASS** | 4 Rotate tests confirmed in the file. |
| `ShotController.CommitFlick` passes SpinContext through | PASS | **CONFIRMED-PASS** | `[Build]` log lines show non-zero `spinInput=(...)` for the 4 non-CENTER strokes, confirming the input flows through. |
| `SpinContext.Reset()` at next-shot handoff | PASS | **CONFIRMED-PASS** | `ShotConeView.HandleStateChanged` calls `SpinContext.Reset()` on Idle state. Deviation explanation is reasonable. |
| `DiagBuildLogger` includes spinInput/spinAxis/spinRate | PASS | **CONFIRMED-PASS** | 5 `[Build]` lines in `live_stat_log.txt` show all three fields populated. |
| `SpinAndShapeVisualGate` in Scenarios.cs / dispatch / menu | PASS | **CONFIRMED-PASS** | Three diffs confirm all three sites. |
| Scenario runs end-to-end, 5 strokes from tee, ResetToTee between | PASS | **CONFIRMED-PASS** | 5 `[TeeDiag] ResetLabToTee OK` lines + 5 armed screenshots all identical at tee. ✓ |
| `LiveStatLogTee` captures `[Build]` lines | PASS | **CONFIRMED-PASS** | live_stat_log.txt contains 5 `[Build]` lines. |
| `build_bot_video.py --mode spinshape` produces captioned MP4 | PARTIAL-PASS | **OVERRIDE-FAIL** | Per CLAUDE.md visual-review checklist rule 5 ("Implementer-graded PARTIAL → FAIL default"), this defaults to FAIL unless I can defend PASS with specific visual evidence. I extracted 12 frames at 5s, 18s, 22s, 35s, 41s, 55s, 62s, 75s, 82s, 95s, 100s, 105s, 107s. **No stroke-label captions visible in any frame.** The MP4 exists but the captions did not render — `parse_spinshape_captions()` may be parsing a log path that doesn't contain `[BotDriver]`-prefixed lines as expected. |
| Stroke 1 CENTER baseline | PASS | **CONFIRMED-PASS** | Right body-frame projection = 0.0m. No curl. ✓ |
| Stroke 2 TOPSPIN: lower trajectory + further total (Δ carry ≥3m or Δ total ≥8m) | PASS | **OVERRIDE-FAIL** | Forward distance went from CENTER's 341.7m to TOPSPIN's **213.9m** — that is **127.8m SHORTER**, not "≥3m or ≥8m further". The implementer cited `ΔX = +124.2m` as "further carry" but X is not the forward axis; the aim is in -X direction, so a less-negative X means LESS forward distance. This criterion is not met. The implementer's PASS justification is based on a misread of the coordinate system. |
| Stroke 3 BACKSPIN: higher trajectory + stops faster (Δ rollout ≤-3m) | PASS | **OVERRIDE-FAIL** | Forward distance went from 341.7m to **351.4m** — that is **+9.7m FURTHER**, not "≤-3m" (less). The implementer cited `ΔX = -9.5m shorter carry` but again the X-direction is reversed by the aim — ball going from -112.9 to -122.4 means going FURTHER along -X. Criterion not met. |
| Stroke 4 LEFT_DRAW: curves left, Δ lateral ≥5m | PASS | **CONFIRM-PASS-WITH-NOTE** | Body-frame right projection = **-32.0m (LEFT)** — direction is correct. Spec's "lateral.z visibly negative" wording is in world coordinates and contradicts the actual result (Δ world Z = +34.6m), but the physics intent (curl left from shooter's perspective) is satisfied. **This is a spec-sign-convention issue, not an implementation bug.** Flagging for the architect to confirm whether the spec criterion or the implementation interpretation is canonical. |
| Stroke 5 RIGHT_FADE: curves right, Δ lateral ≥+5m | PASS | **OVERRIDE-FAIL** | The terminal position `(219.4, 11.5, 34.7)` is **the tee position itself** (teePos = `(219.43, 11.58, 34.73)`). Terminal type is `OB` and flight time is 6.8s (vs ~14-17s for other strokes), so the ball went out of bounds in flight and the OB handler reset it to the tee. **There is no valid landing position for FADE**, so the spec criterion is unverified. The implementer's "ΔZ = +79.2m, ball went OB confirming strong right fade" is circular — `+79.2m` is just the tee-Z minus CENTER terminal-Z, not a measurement of where the FADE ball actually went. |
| All 5 strokes same character + driver + power=1.0 | PASS | **CONFIRMED-PASS** | 5 `[Build]` lines show identical clubVel/effectiveFlick/velMultiplier/velMagnitude/loft/aimYaw. Only spinInput differs. ✓ |
| No scene/prefab/asset mutations | PASS | **CONFIRMED-PASS** | `git diff --name-only HEAD -- "*.unity" "*.prefab" "*.asset"` empty. |
| No scope creep | PASS | **CONFIRMED-PASS** | StatCoefficients/SpinPanelWidget untouched. |
| Console error-free | PASS | **CONFIRMED-PASS** | Pre-existing Rindo lightmap meta errors unrelated to this task. |
| Spec deviations listed | PASS | **CONFIRMED-PASS** | Three documented; all reasonable. |

## Specific failures (for the implementer's next iteration)

1. **TOPSPIN spec criterion not met (item 12).** Spec says "Δ carry ≥3m or Δ total ≥8m further." Actual: 127.8 m SHORTER in forward distance. Fix options:
   - **(a) Re-tune** `SpinMagScaleSlope` so the topspin sign-flip doesn't reduce forward carry. The current slope=1.5 with sign-flip at +Y=+1 produces a half-magnitude topspin with axis flipped — which evidently kills enough lift that the ball undershoots dramatically. A smaller slope (e.g. 0.8) would scale topspin down to magScale=0.2 without flipping sign — gentler effect, ball flies similarly but lower-arc.
   - **(b) Re-evaluate the spec criterion** with the architect: maybe "topspin = more total carry" is wrong for this physics model; maybe the criterion should be "topspin = lower apex" measured via trajectory peak Y instead of carry.
   - Either way, the current data + slope=1.5 + spinY=+1 produces an unrealistic carry loss that contradicts the spec criterion. Surface this to the architect (`ESCALATE` path) if you believe the physics is right and the spec criterion is wrong.

2. **BACKSPIN spec criterion not met (item 13).** Spec says "Δ rollout ≤-3m vs CENTER." Actual: 9.7m FURTHER total distance, not less. Fix options:
   - **(a) Re-tune** slope or BaseBackspinRpm — current 2.5× scale on top of an already-high backspin baseline (281.3 → 703 rad/s) may be ballooning the ball without producing the expected lift-vs-distance falloff.
   - **(b) Measure rollout specifically** — the criterion is about *rollout* (post-landing roll), not total distance. The current log doesn't distinguish carry from rollout. If you can capture per-stroke "land position vs final position" separately, you may discover BACKSPIN reduces ROLL even if total goes slightly further (more carry, less roll). Add a `[Land]` log line at first-bounce and a `[Rest]` log line at AtRest in the scenario.
   - Same as TOPSPIN — if you think the physics is correct, escalate the spec criterion to the architect.

3. **FADE direction unverified (item 15).** Spec says "Δ lateral ≥+5m vs CENTER terminal." Actual: ball went OB at 6.8s, terminal coords are the tee (reset position). No valid measurement. Fix options:
   - **(a) Reduce power** for the fade stroke to keep the ball in bounds — e.g. `FireDriverShot(power: 0.7f)` for stroke 5 only — so we have an actual landed position to compare.
   - **(b) Aim further left** so the fade doesn't curl into OB territory — adjust `SetCameraYawRadians` for stroke 5 specifically.
   - **(c) Capture the BALL POSITION at OB-detection time** instead of returning the tee position — modify the OB handling in the bot to grab the last in-flight position before OB reset, and log it as the FADE terminal.
   - Whichever path: we need a real terminal position with both forward and lateral components valid, so the spec criterion can be evaluated.

4. **"Landed" screenshots show post-reset state for strokes 2, 4, 5 (and possibly more).** Stroke 2/4/5 "landed" screenshots show the ball back at the tee with the next-stroke aim-arc HUD active. This is because `d.Capture("..._landed")` runs AFTER `WaitForBallAtRest` BUT visually the camera/HUD may already be advancing to the next-stroke setup, OR the next iteration's ResetToTee happens before the screenshot frame renders. Fix:
   - **(a) Use `CaptureHelper.SnapAtEndOfFrameAndPause` with `skipPause: false`** at the landed moment, then explicitly `EditorApplication.isPaused = false` after the screenshot — this guarantees the frame captured is the at-rest frame, not a transitional one.
   - **(b) Add a `yield return new WaitForSecondsRealtime(0.5f)` AFTER `WaitForBallAtRest` and BEFORE `Capture(...)`** so the camera/HUD has time to settle at the at-rest position without yet starting the next iteration.
   - **(c) Insert `Capture(...)` BEFORE the next iteration's `ResetLabToTee` call** rather than at the end of the current iteration — the loop structure already does this, but verify the frame written is the at-rest frame, not a transitional one.

5. **Video captions not rendering (item from bot scenario row, OVERRIDE-FAIL).** The MP4 exists but has zero visible stroke-label captions. The implementer flagged this as PARTIAL-PASS. Fix:
   - The implementer's note says `parse_spinshape_captions` expects `[BotDriver]` prefix in `live_stat_log.txt` but `history.log` lacks that prefix. The captions need to read from `history.log` (which has the `Stroke N: LABEL spinInput=...` lines as the `[BotDriver]` LogStep output) — OR — the parser needs to read from `live_stat_log.txt` and parse the `[Build]` lines for spinInput values.
   - Either parse path is acceptable; pick one, verify a captioned frame at the expected stroke timestamps, and confirm by extracting one frame per stroke from the MP4.

## Hygiene PASS items not in dispute

- Asmdef boundary deviations are sound.
- No scope creep into the Ball.Spin stat lane.
- `git diff` clean on scenes/prefabs/assets.
- Tests increase 12 from baseline (8 + 4 — matches spec target ≥8+4).

## Routing

`BACK_TO_IMPLEMENTER` — STATUS set to `SELF_REVIEW_FAIL`.

The implementer needs to either:
1. Re-tune `SpinMagScaleSlope` so TOPSPIN/BACKSPIN actually produce the spec-required carry deltas, AND fix the FADE OB issue (reduced power or different aim) AND fix the landed-screenshot timing AND fix video captions, OR
2. Surface to the architect that the spec's TOPSPIN/BACKSPIN/FADE criteria don't match what the physics produces and request a criterion re-evaluation. The escalation must include the analysis above showing the exact deltas in body-frame coordinates, not just world-frame X/Z.

The code itself is in good shape — the Q-lock math is implemented faithfully, the asmdef deviations are justified, tests pass, no scene corruption. The failure is in the visual-gate evidence chain. Do not rewrite the spin block — just fix the gate evaluation and capture path.

## Iteration count

This is iteration **1** of self-review for this task. N < 3 — FAIL routing is appropriate; ESCALATE is not yet warranted.
