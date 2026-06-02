# Architect Review — `club_control_arrow_polarity_fix`

**Reviewer:** golfin-reviewer
**Iteration:** 1
**Timestamp:** 2026-06-02 13:13 JST
**Verdict:** READY_FOR_REDTEAM

---

## Independent visual scan

The canonical screenshot `screenshots/cc_polarity_fix_2026-06-02_13-04-18.png` is a 1920×1080 in-game Game View frame showing a 3D golf scene: a fairway flanked by tall conifer trees on the right with strong directional shadows, a wide grey OB/cart-path stripe on the left, dark green rough at the far-left edge, and a clear blue sky with scattered light cloud. No HUD, no aim arrow, no timing-arrow oscillation evidence, no UI overlay. The image is generic and carries no information about ClubControl→arrow-Hz polarity — that is the expected shape for this task because the SPEC explicitly designates the automated EditMode tests as the decisive gate and the screenshot as supporting evidence only.

## Task-type gate selection

Physics/config + test polarity fix. NOT a UI/Figma task and NOT a mesh/terrain task.
- Figma side-by-side comparison: **N/A** (no Figma reference in SPEC).
- Bbox containment verification: **N/A** (no containment claims).
- Rule 16 mesh-metrics section: **N/A** per user's brief.
- Rule 17 mesh-bake video: **N/A** per user's brief.
- Decisive gate: automated EditMode tests + per-file code/config diff audit.

## Code & config audit (independent)

### 1. `ControlsConfig.Default` values — PASS

Read `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` lines 67–68:
```
BaseArrowSpeedHzAtCC0          = 3.0f,
ArrowSpeedHzPerCC              = -0.025f,
```
Both match SPEC exactly. Other Default fields (`ConeHalfAngleAtAcc0Deg=5f`, `ConeHalfAngleAtAcc100Deg=20f`, `MaxCleanPassesAtCC0=1f`, `CleanPassesPerCC=0.04f`, `MaxTotalPasses=10f`, `DegradationYawDegPerPass=2f`, `PuttArrowSpeedMultiplier=0.5f`) are at baseline values — no stray edits.

### 2. `controls.csv` values + notes — PASS

Read `Assets/Resources/Gameplay/controls.csv` lines 15–16:
```
BaseArrowSpeedHzAtCC0,3.0,arrow cycles/sec at ClubControl=0 — fastest/hardest end of range
ArrowSpeedHzPerCC,-0.025,additive cycles/sec per CC point — negative: higher CC = slower arrow
```
Numeric values agree with `.cs` Default → no runtime revert risk. Notes columns updated per SPEC. CleanPasses CSV row (line 18 `CleanPassesPerCC,0.04,…`) is unchanged.

### 3. `ShotController.TickArrow()` clamp placement — PASS

Read `Assets/Scripts/Gameplay/Input/ShotController.cs` lines 292–316:
- Line 296: `float ccClamped = Mathf.Clamp(cc, 0f, 100f);` — clamp added.
- Line 297: `float arrowHz = _config.BaseArrowSpeedHzAtCC0 + ccClamped * _config.ArrowSpeedHzPerCC;` — arrow-Hz line uses `ccClamped`.
- Line 298: `if (IsPutt) arrowHz *= _config.PuttArrowSpeedMultiplier;` — untouched, putt multiplier path preserved.
- Line 309: `int cleanPasses = Mathf.RoundToInt(_config.MaxCleanPassesAtCC0 + cc * _config.CleanPassesPerCC);` — uses **raw `cc`** (NOT `ccClamped`). Correct per SPEC § "Do NOT clamp the cleanPasses line."
- Line 371 (preview path in `PublishState`): also uses raw `cc`. Untouched.

Clamp scope is exactly correct: arrow-Hz line only.

### 4. Diff surface — PASS (surgical)

`git diff HEAD` on the three production files shows:
- **ControlsConfig.cs:** exactly 2 lines changed (the two SPEC values, lines 67–68). No other field touched.
- **controls.csv:** exactly 2 lines changed (rows 15–16, both value + notes). No other row touched.
- **ShotController.cs:** +1 clamp line (296) and one `cc`→`ccClamped` substitution on the arrow-Hz line (297). Nothing else.

No scene files (`.unity` / `.asset`) modified. No prefabs touched. No new `Button` added — Rule 11 (`ButtonPressFeedback`) N/A.

### 5. Test11 (new regression gate) — PASS

Read `ShotControllerTests.cs` lines 196–243. `Test11_ArrowSpeed_MonotonicDecreasingWithCC`:
- Constructs two real `StatBundle`s with distinct ClubControl values (0 and 100) via `Golfin.Physics.Stats.CharacterStats` / `BallStats.Neutral` / `ClubStats.DefaultDriver`.
- Injects each via `_sc.InjectStatBundle(...)`, drives to Timing with `DriveToTiming(170f)`, ticks `dt=0.1f`, captures `ArrowProgress01`.
- Three real asserts: `Assert.Greater(progressCC0, 0f)`, `Assert.Greater(progressCC100, 0f)`, `Assert.Greater(progressCC0, progressCC100)`.
- Math sanity: at CC=0 arrowHz=3.0 → progress=0.30; at CC=100 arrowHz=0.5 → progress=0.05. The third assert is strict (0.30 > 0.05) and would FAIL if polarity were reverted (under old polarity CC=100 would produce 3.0 Hz and the inequality would invert).
- Reset logic between the two measurements (`_input.IsTouching=false` → Tick(0.016f) → cancel-to-Idle) is correct — the controller's flick-cancel path requires velocity below threshold, which a single zero-touch tick provides.

This is a genuine polarity regression gate, not a tautology, and not a no-op.

### 6. Rewritten putt test — PASS

Read `ShotControllerPuttModeTests.cs` lines 130–159. `F1_IsPutt_ArrowsSlowedByMultiplier`:
- Toggles `_sc.IsPutt` between false and true at the same default CC (no stat injection → same `StatBundle` for both).
- Drives to Timing in each mode, ticks `dt=0.1f`, captures `ArrowProgress01`.
- Single strict assert: `Assert.Greater(progressNonPutt, progressPutt, …)`.
- Math: non-putt arrowHz=3.0, putt arrowHz=3.0×0.5=1.5. Over dt=0.1: non-putt=0.30, putt=0.15. The inequality is strict and meaningful.

This is exactly the SPEC-prescribed polarity-independent invariant ("at equal CC, putt arrowHz < non-putt arrowHz"). Not commented out, not weakened-to-always-pass.

### 7. Test09 / Test10 still pass with updated comments — PASS

Read `ShotControllerTests.cs` lines 166–194. Comments updated from `arrowHz=0.5` to `arrowHz=3.0`. `dt` changed from 2.5 → 0.34 (Test09 L176, Test10 L190). Implementer flagged this as a Spec deviation; review judgment: at arrowHz=3.0, dt=0.34 yields `arrowProgress += 1.02` (cleanly 1 pass per tick), which better documents the "1 pass/tick" intent than dt=2.5 (which would yield 7.5 progress, still 1 pass/tick due to the `>= 1f` gating but misleading). The assertions are unchanged and validate the same state-machine behavior (IsDegrading after exhausting clean passes; auto-cancel after MaxTotalPasses). Acceptable.

### 8. No `[Ignore]` / `[Explicit]` on any test — PASS

Grep across both test files for `Ignore` and `Explicit` returns zero matches. Therefore the new Test11 and the rewritten putt test are necessarily among the 360 PASSED in the implementer's rollup (cannot be among the 3 SKIPPED).

### 9. arrowHz positivity across CC ∈ [0, 100] — PASS

Worst-case minimum: at clamped `ccClamped=100`, `arrowHz = 3.0 + 100*(-0.025) = 0.5`. Worst-case maximum: at clamped `ccClamped=0`, `arrowHz = 3.0`. Both > 0. The clamp guarantees the floor of 0.5 Hz even if a buff pushes raw `cc` above 100. Putt multiplier (0.5) reduces the floor to 0.25 Hz but stays positive.

### 10. Rule 13 — files outside spec folder — PASS

`git status --porcelain --untracked-files=all` shows untracked Taiheyo `.meta` files (folder + per-PNG sidecars). Implementer's "Files modified or created" table line 18 names this path and explains origin (AssetDatabase.Refresh of pre-existing PNG assets). Spot-checked `Assets/Courses/Maps/Taiheyo.meta` — contains `folderAsset: yes` boilerplate, genuine Unity auto-generated sidecar. Rule 13 satisfied.

Pre-existing baseline drift (`Docs/Diag/baked-pivot/M0-regression-*.md`, `Docs/Diagnostics/_capture/h07_iter8_*.jpg`, `Tools/GreenSlope/scripts/capture-all-holes.mjs`) appears in HEARTBEAT.log iter-1 baseline block — predates this task, not in scope.

### 11. Scene-mutation audit — PASS

No `.unity` or `.asset` files in the diff. Zero scene mutations.

## Test-green verification

Per the user's brief, I should attempt to re-run the EditMode suite to confirm Test11 and the putt test report PASSED individually, not just rely on the rollup.

**Tooling check:** my agent contract scope is `Read`/`Write`/`Edit`/`Glob`/`Grep`/`Bash`/`WebFetch`/`mcp__figma__*` + read-only `script-execute`. I do NOT have `mcp__ai-game-developer__tests-run` — that is the implementer's tool. Direct `curl` to the MCP HTTP endpoint at `localhost:21573` requires session-init via the MCP protocol and is not accessible from raw bash. Per my agent definition's Test Runner Verification section, when MCP test-run is unavailable I fall back to careful static read.

**Static-read substitute:**
- Implementer rollup: `Total=363, Passed=360, Failed=0, Skipped=3, Duration=00:00:32.88`.
- Baseline `ShotControllerTests.cs` had ~10 tests; this iter adds 1 (Test11) → +1 net, consistent with `Total=363` if baseline was 362.
- `Failed=0` is the binding number. Both new tests have `[Test]` attributes and no `[Ignore]`/`[Explicit]` → they either passed or didn't run.
- The 3 skipped tests must be pre-existing (not the new ones), since the new ones have no skip attributes.
- Both new tests are constructed such that they would fail visibly if the polarity were unfixed: Test11's third assert (`progressCC0 > progressCC100`) inverts under old polarity; the putt test's assert (`progressNonPutt > progressPutt`) is independent of polarity but requires the putt multiplier path to still be live. Code inspection confirms both code paths are intact.
- `Failed=0` + tests constructed-to-fail-on-bug + matching surgical diff = internally consistent green rollup.

This closes the soft note the self-reviewer raised. Caveat noted: I could not independently re-run the suite from this agent thread; the verdict rests on the implementer's rollup + static read of test bodies that are constructed to fail-on-bug.

## Cross-cutting checks

- `arrowHz > 0` for all CC ∈ [0,100]: verified (worst case 0.5 Hz at CC=100, 0.25 Hz on putt).
- No new `Button` added (Rule 11 ButtonPressFeedback): N/A.
- No scene mutations.
- No SerializeField wiring changes.
- `.cs` and `.csv` agree on both values.

## Catch list (false-PASS checks)

- `.cs` and `.csv` disagreeing → NOT triggered (both 3.0 and -0.025).
- Stray field edits beyond the two SPEC values → NOT triggered (diff is exactly 2 lines on ControlsConfig.cs, 2 lines on controls.csv, +2/-1 lines on ShotController.cs).
- Clamp on wrong line or missing → NOT triggered (line 296/297 only; cleanPasses line 309 untouched).
- Test11 weakened to tautology → NOT triggered (three real asserts on distinct injected CC values).
- Putt test weakened or commented out → NOT triggered (single strict `>` assert at equal CC).
- `[Ignore]` or `[Explicit]` on new tests → NOT triggered (grep clean).
- Scene mutation introduced via screenshot path → NOT triggered (no `.unity`/`.asset` in diff).
- Files outside spec folder undisclosed (Rule 13) → NOT triggered (Taiheyo .meta paths listed in report table).

## Summary

Every SPEC item verified independently against live code. Diffs are surgical (2 lines each in `.cs` and `.csv`; clamp + variable rename in `ShotController.cs`). The clamp is scoped correctly to the arrow-Hz line only; clean-pass coupling preserves the raw `cc` per SPEC. Both new/modified tests are real regression gates (constructed to fail under the old polarity OR if the putt multiplier path breaks), with no skip attributes. `.cs` and `.csv` agree, eliminating runtime-revert risk. No scene mutations. No stray edits.

The only procedural gap is that I could not independently re-run the EditMode suite from this agent thread to capture per-test PASS lines for Test11 and the putt test — that tooling is scoped to the implementer. The implementer rollup `Failed=0` plus static analysis of the test bodies (constructed-to-fail-on-bug) closes this gap on substance.

This is a clean, well-scoped polarity inversion. Handing off to the red-team gate.

## Verdict

**READY_FOR_REDTEAM** — set STATUS to `READY_FOR_REDTEAM`. The adversarial red-team reviewer is the only agent that may advance to `ARCHITECT_REVIEW_PASS`.
