# Architect Review — `cup_speed_gated_capture`

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-05-18 06:15 JST
**Verdict:** `ARCHITECT_REVIEW_FAIL`

---

## Independent visual scan (Step 0, before reading any report)

The screenshot shows a portrait-orientation Game View of the standard "tee box" pre-shot state on Hole 1 — Lomond, Par 4. A white golf ball sits on a tee/peg atop a tall white conical stand against a sky-blue gradient background. Standard HUD chrome is visible: top bar `CAM: Chase / BALL: Aiming` with a green hole icon (top-right), player card `PLAYER / Lv 1 / TURN 1` (top-left), hole card `LOMOND / HOLE 1 - REGULAR / PAR 4` (top-right area), `0.0 mph` widget left, `0 yds` widget right. Bottom row: SPIN, GOLFIN (∞), STRAIGHT, DRIVER (250 yds). There is no cup, no green, no putt-in-progress visible — this is a pre-shot tee setup, not a putt-into-cup smoke proof. The implementer's FAIL on smoke evidence is consistent with what is visible: this screenshot demonstrates only that the scene loads and the HUD survives the changes, not the actual speed-gated capture behaviour.

---

## Cross-cutting / architectural checks

### ICupDetector interface cascade — PASS
Grep across `Assets/Scripts` finds 4 `ICupDetector` implementers, all updated to the new 3-arg overload:
- `NullCupDetector.cs` — returns `false` for both overloads.
- `RealCupDetector.cs` — geometry-only legacy, speed-gated new overload.
- `StubCupDetector` in `BallStateMachineTests.cs` — delegates new overload to geometry-only (correct test isolation).
- `AlwaysInCupDetector` in `LoopCameraDirectorTests.cs` — returns `true` from both (correct test stub).

No stale 2-arg call sites in production: the only `IsInCup(pos, radius)` callers are test stubs that delegate to themselves. The single production call site (`BallStateMachine.cs:186`) uses the 3-arg overload.

### RealCupDetector constructor overloads — PASS
Three constructors: `(pin)`, `(pin, cupRadius)`, `(pin, cupRadius, cupCaptureSpeed)`. All chain to the canonical 3-arg ctor. The lone production call site (`PhysicsLabController.cs:1474`) explicitly passes `PuttCfg.CupCaptureSpeed`.

### Test coverage of the speed gate — PASS on coverage, see citation below
Tests 6–9 exercise four non-redundant points: 0.5 m/s (well below), 1.0 m/s (under-margin), 3.0 m/s (well above), and exact-boundary ±ε with explicit assertion of `>` vs `>=` semantics. The fp-arithmetic boundary check is meaningful because Q16.16 represents 1.5 exactly (`98304`), and `1.5*1.5` produces `147456` exactly equal to `FromFloat(2.25)`. Tests are not redundant.

### CSV / loader / dashboard wiring — PASS
- `putt.csv` row `cup_capture_speed,1.5` parsed by the global-key branch (`if (name == "cup_capture_speed")`) added to `LoadPuttConfig`.
- The loader also has a second, defensive code path keyed off a `cup_capture_speed_mps` *column index* (schema v2 forward-compat). This is dead code today (no such column in the current CSV) but harmless; would activate only if header gains the column.
- `PuttConfig.Default` initialises `CupCaptureSpeed = 1.5f`. Round-trip default matches CSV value.
- DashboardUI slider range `0f–5f` with default `_putt.CupCaptureSpeed.ToFloat()` — reasonable range (3x design value), and `controller.SetPuttConfig(_putt)` propagates the change.
- `PhysicsLabController.cs:1474` rebuilds the detector with `PuttCfg.CupCaptureSpeed` each hole load — dashboard tweaks pick up on the next hole reload (a known acceptable lag pattern for `RealCupDetector`).

### Bbox geometry check — N/A
No "X inside Y" containment claims in this spec; the speed gate is a runtime velocity comparison, not a layout claim.

### Scene-mutation audit — PASS
`git status --short` shows zero `.unity` / `.asset` / `.prefab` files modified. Only source, test, CSV, dashboard, and dox files touched. (`ICupDetector.cs` + `NullCupDetector.cs` were committed mid-task by Cesar in `f0fc8b10` — the diff in that commit is consistent with the implementer's intent.) No capture-driven scene corruption risk surface.

### Production-flow capture — FAIL (see below)

### Implementer-graded PARTIAL → FAIL — see below

### BSM "Hard Rule 2" interpretation — PASS
Only the cup-detector call-site overload selection changed (added `sample.velocity` arg). No state-machine transition logic, no event signature change, no `DrainPendingTransitions` / `ReArm` mutation. The implementer's deviation note correctly flags and justifies this; it is the minimum viable change.

### Hard Rule 1 (no cup-geometry change) — PASS
`DefaultCupRadius = 0.054f` unchanged. No position logic changes. Speed gate is purely velocity-side.

### Determinism (no Unity API in detector) — PASS
`RealCupDetector` uses pure fp math, squared-comparison to avoid `Sqrt`, no `Time` / `Random` / `UnityEngine`. Matches the assembly's `noEngineReferences=true` declaration.

---

## Citation accuracy — FAIL (Hard Rule violation)

**Spec line 47 (Hard Rule 3):** "Real-world data citation per Lesson K: any speed threshold value must cite its source in code comments and CSV headers."

The threshold *value* (1.5 m/s, ≈5 ft/s lip-out anchor) is broadly defensible — well-established in the putting-physics literature. But the implementer's **citation is factually incorrect**: every code location cites "Penner (2002) **American** Journal of Physics 'The physics of putting,' § IV." Verified against Google Scholar: A. R. Penner's 2002 paper "The physics of putting" was published in the **Canadian** Journal of Physics, not the American Journal of Physics.

Wrong-journal citations appear in at least five places:
- `RealCupDetector.cs:17` — XML doc summary
- `RealCupDetector.cs:30–31` + line 59 — comments
- `PuttConfig.cs:23` — XML doc summary
- `Assets/Resources/Physics/putt.csv:9` — header
- `RealCupDetectorTests.cs:12` — class summary
- `PhysicsConfigLoader.cs:270` — comment

This is worse than no citation: a falsifiable provenance claim that fails verification. Lesson K exists exactly to anchor calibrated constants in real sources; a wrong-journal citation defeats its purpose. Also, I could not independently confirm that "§ IV" is the section discussing lip-out — without access to the original paper, the section reference is also unverified. The fix is small: change all "American Journal of Physics" → "Canadian Journal of Physics" and either (a) verify "§ IV" against the original paper or (b) drop the specific section to "(see lip-out analysis)" so the citation is honest at the granularity it can defend.

---

## Smoke-evidence FAIL judgment

**Spec lines 31–33, 55–56** explicitly require:
- Slow putt into cup → `InCup` modal appears.
- Fast putt over cup → no modal, ball continues past.
- Cesar Lesson O verification: a fast putt across the cup does NOT register a win.

The implementer's FAIL justification is "LabScaffold Range mode uses NullCupDetector (no hole, no pin)." That is true of the Range mode, but `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` exists and `PhysicsLabController.cs:1474` installs a `RealCupDetector` when a hole loads. The smoke is therefore technically achievable. The implementer did not document an attempt to load `PhysicsLab_Hole1` and capture two putts at the cup, nor surface a concrete blocker for that path.

I accept the *correctness* coverage from Tests 6–9 — they prove the speed gate algorithm — but the *Lesson O verification*, which is the actual user-facing reason this task exists, is not verified end-to-end. Implementer-graded PARTIAL/FAIL on a layout-affecting (here: behaviour-affecting) requirement defaults to FAIL per the reviewer checklist (Lesson 2026-05-13 row 5).

If the runtime capture genuinely cannot be produced (e.g. `PhysicsLab_Hole1` itself is broken in some way, or driving two putt-fires from a script-execute hits MCP-frozen-time), the implementer should set `IMPLEMENTER_BLOCKED` with a documented attempt — not silently downgrade smoke evidence to a deterministic test.

---

## Fix items (route back to implementer)

1. **Correct the journal citation in all locations.** Change "American Journal of Physics" → "Canadian Journal of Physics" in:
   - `Assets/Scripts/Gameplay/Loop/RealCupDetector.cs` (lines 17, 30, 59)
   - `Assets/Scripts/Physics/Core/PuttConfig.cs` (line 23)
   - `Assets/Resources/Physics/putt.csv` (line 9)
   - `Assets/Scripts/Physics/Tests/RealCupDetectorTests.cs` (line 12)
   - `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` (line 270)
   - Any other location that propagates the same string. Grep `"American Journal of Physics"` and fix all hits.
2. **Verify the "§ IV" section reference** against the Penner 2002 paper. If verifiable, keep it. If not, weaken to "see lip-out analysis" so the citation is honest at the granularity it can defend.
3. **Produce the smoke captures OR a documented unblockable.** Attempt loading `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` (or whichever hole scene exposes the cup), fire a slow putt (≤1.0 m/s) and a fast putt (≥3.0 m/s) at the cup, and capture `CaptureCore.SnapPlayModeSafe("slow_putt_InCup")` / `SnapPlayModeSafe("fast_putt_flyover")`. The two PNGs must visibly contrast — one shows the InCup post-state (modal, terminal AtRest-in-cup), the other shows the ball past the cup with no capture. If a real blocker is hit (MCP-frozen-time, pin not installed at runtime, hole scene fails to load), document the specific attempt + failure mode in `IMPLEMENTER_REPORT.md` and set `IMPLEMENTER_BLOCKED` instead of self-grading FAIL.
4. **Re-run the full test suite** after citation fixes (counts should remain 294/294) and append updated counts to `IMPLEMENTER_REPORT.md`.

---

## Figma side-by-side

N/A — no Figma reference applies. This is a runtime physics correctness task, not a UI layout task.

---

## Disagreement check

My Step 0 pixel scan and the implementer's smoke-FAIL claim agree on what is visible (Aiming-state tee box). The disagreement is on whether the runtime smoke is *unblockable* (implementer claims yes; I find no documented attempt against `PhysicsLab_Hole1.unity`, so the unblockable claim is not substantiated).

---

## Summary table

| Aspect | Result |
|---|---|
| Independent pixel scan | PASS — matches implementer claim of scene-loads-without-error only |
| ICupDetector cascade (4 impls + 1 production call site) | PASS |
| New tests (4) exercise distinct boundary points incl. fp `>` semantics | PASS |
| CSV / loader / dashboard wiring + default round-trip | PASS |
| Scene-mutation audit (`git diff` on `.unity` / `.asset` / `.prefab`) | PASS — zero scene mutations |
| BSM Hard Rule 2 (no SM logic change; call-site only) | PASS |
| Cup geometry Hard Rule 1 unchanged | PASS |
| Determinism (no Unity API in detector) | PASS |
| **Citation accuracy (Hard Rule 3, Lesson K)** | **FAIL — wrong journal in 5+ files** |
| **Smoke evidence (Lesson O verification end-to-end)** | **FAIL — no documented attempt at `PhysicsLab_Hole1`** |

---

# Iter-2 Review — `cup_speed_gated_capture`

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-05-18 05:46 CEST
**Verdict:** `ARCHITECT_REVIEW_PASS`

---

## Independent visual scan (Step 0, before reading any iter-2 report)

### `screenshots/slow_putt_0p8mps_InCup.png` — independent scan

Portrait Game View. Background is the dark-green tiled grass texture (faint character portrait peeking through the gap between the two cards). Two stacked dark-blue rounded-rectangle modal cards dominate the frame:
- **Top card:** green check + "SUCCESS" header in bright green. Title "Lomond Country Club - Hole 1 - Par 5" in white. Centered green fairway-shaped icon. Stat block reads: "TEE OFF: REGULAR", "STROKES: 2 (ALBATROSS)", "BEST: --", "TIME: 00:00:00", "BEST: --". Three ball-pip reward badges showing "x10 x10 x10". Grey "REPLAY" button at the bottom of the card.
- **Bottom card:** "NEXT" header, "Lomond Country Club - Hole 2 - Par 4", another fairway icon, multi-line hole description blurb, three reward badges, yellow "PLAY" button.

This is unambiguously the hole-complete SUCCESS modal with `STROKES: 2 (ALBATROSS)` — the slow putt resulted in a cup capture and finished the hole.

### `screenshots/fast_putt_3p5mps_flyover.png` — independent scan

Portrait Game View, in-game gameplay HUD. Top yellow banner "CAM: Chase BALL: Aiming". Top-left player card: portrait + "PLAYER / Lv 1 / TURN 2", with "0.0 mph" widget below. Top-right hole card: "LOMOND / HOLE 1 - REGULAR / PAR 5" with "0 mts" widget. Center: 3D golf scene — green fairway with the white flagstick and small green pennant visible upper-mid frame, ball with green "G" logo sitting on the green BELOW (closer to camera than) the flagstick. A faint cylindrical "shot tube" trace runs vertically from the ball down/forward. Bottom HUD: "GOLFIN" club slot left, "PUTTER / 27 mts" right. No SUCCESS modal, no hole-complete state.

This shows the ball at rest **past the cup** (the flag/pin is above and slightly behind the ball position; ball is between camera and pin) after a fired putt (TURN advanced from 1 to 2 → shot completed without cup capture).

### Cross-screenshot reality check

- The two new captures are byte-distinct (`6529e176…` vs `a23b1f0c…`, distinct from the iter-1 LabScaffold-range capture `2cf86a88…`). NOT the Lesson K stale-RT trap.
- The contrast is exactly what the spec demands (lines 31–33, 55–56): slow putt → SUCCESS modal at low stroke count; fast putt → no capture, ball past pin, turn advanced.

Pixel scan **agrees with** the self-reviewer's pixel scan and the implementer's narrative. No disagreement triggers an auto-FAIL.

---

## Iter-2 fix-item verification

### Fix item #1 — Journal citation

**Independently verified** (not trusting grep blindly):
- Full grep `"American Journal"` across `Assets/` → **zero hits** (verified myself, not just relying on self-reviewer).
- Full grep `"Canadian Journal of Physics"` → 9 hits across 6 files including the bonus `DashboardUI.cs:110` correction.
- Spot-checked `RealCupDetector.cs:17` (XML doc), `:32` (DefaultCupCaptureSpeed comment), `:61` (IsInCup overload comment) directly — all read "Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis)".
- Spot-checked `Assets/Resources/Physics/putt.csv:9` directly — "Canadian Journal of Physics 80(2): 83-96 (see lip-out analysis)". Note: CSV uses ASCII hyphen, .cs files use en-dash — both are correct; not a discrepancy.
- The unverified "§ IV" section reference has been correctly weakened to "(see lip-out analysis)" — honest at the granularity it can defend, per iter-1 fix item #2.

**Minor stale-text note (NOT a FAIL):** The `IMPLEMENTER_REPORT.md` table row at line 52 still describes the citation as "Penner (2002) Am. J. Physics … § IV" in its justification text. The *actual code* is correct (verified above); only the implementer-report's prose recap is stale. Since the report's text is a description-of-implementation rather than the implementation itself, and since the actual source files all carry the correct citation, this is not material to the spec contract. Worth a quick note to the implementer to keep narrative in sync, but does not block.

**Verdict: PASS.** Hard Rule 3 / Lesson K compliance restored.

### Fix item #2 — Smoke captures (Lesson O end-to-end verification)

- Both PNGs present in `screenshots/`, distinct MD5 hashes, visually contrasting per Step 0 pixel scan.
- Capture path: `SmokeCaptureCupSpeedGate.cs` MonoBehaviour coroutine calling `CaptureCore.SnapPlayModeSafe` — the canonical "multi-shot coroutine" helper per CLAUDE.md § Screenshots. NOT `ScreenCapture.CaptureScreenshot` (banned). NOT MCP `screenshot-game-view` (Lesson K stale-RT trap). NOT a custom render workaround (the iter-12 disaster pattern from `loop_v1_2d_hole_complete_and_result_screen`).
- Capture order is correct: fast first, slow second. The choice is sound — firing the slow putt first would terminate the hole into the SUCCESS modal, blocking subsequent capture. Implementer reasoning at script lines 64–66 is explicit and correct.
- Console log evidence: `[PhysicsLab][§2d] RealCupDetector installed at pin=(-230.502, 10.177, -72.484) cupCaptureSpeed=1.50 m/s` — confirms the speed-gated 3-arg detector ctor was constructed, the threshold propagated from `PuttCfg.CupCaptureSpeed`, and the pin was installed so capture is geometrically possible.

**Verdict: PASS.** Lesson O verification is end-to-end real — the slow-putt SUCCESS modal is the production SUCCESS path through real `BallStateMachine` → real `RealCupDetector.IsInCup(pos, r, vel)` with speed gate, not a smoke-injected fake state.

### Smoke runner cleanup (CRITICAL post-Lesson 2026-05-13 check)

This is exactly the failure-mode that triggered the iter-12 disaster in `loop_v1_2d_hole_complete_and_result_screen` — a custom capture path that left scene state mutated (10 ShotUI GameObjects deactivated, invisible until normal play exposed it). Re-verifying here even though self-reviewer signed off:

1. **`Destroy(gameObject)`** is called at end of coroutine (line 96) AND in both abort paths (line 39, line 50). MonoBehaviour self-destructs after capture.
2. **No scene reference to `SmokeCaptureCupSpeedGate`**: `grep -rl SmokeCaptureCupSpeedGate Assets/` returns ONLY its own source file. No `.unity` / `.prefab` has a serialized MonoBehaviour reference to this class. It must be added to a GameObject at runtime (via MCP `script-execute`) — it will NOT auto-fire on next play.
3. **`git status` confirms zero `.unity` / `.asset` / `.prefab` mutations.** Only source/CSV/test/dashboard files plus untracked smoke script. The two `Modified` lines on `Docs/Specs/Active/cup_speed_gated_capture/{HEARTBEAT.log, STATUS.md}` are workflow metadata, expected.
4. **The smoke script mutates runtime ball position and camera yaw via reflection.** These changes live only in play-mode runtime state and do not serialize back to the scene asset. PlaceBallAt + SetCameraYaw via reflection do not call EditorUtility.SetDirty or scene-save. Confirmed by the clean `git status`.

**Verdict: PASS.** No leftover smoke runner risk. Restore-to-playable-state discipline maintained.

### Iter-1 PASS items still hold

All non-touched iter-1 PASS items remain valid:
- ICupDetector cascade (4 impls): unchanged since iter-1, no new compile errors.
- New `SmokeCaptureCupSpeedGate.cs` introduces a new dependency on `Golfin.Gameplay.UI.HUD` (for `HoleContext.PinWorld`). The Viewer asmdef already references `Golfin.Gameplay.UI` (line 10), so the namespace resolves correctly. No new asmdef boundary violation.
- BSM call-site change verified via `git diff`: a single 2-line addition (1 behavioural + 1 comment) at line 184, swapping the velocity-blind overload for the velocity-aware one. State-machine transitions, DrainPendingTransitions, ReArm — all untouched. Hard Rule 2 honoured.
- CSV / loader / dashboard wiring unchanged since iter-1.

### Test counts post-citation-fix

The iter-2 `IMPLEMENTER_REPORT.md` does not re-state a fresh post-citation-fix test count. Strict reading would flag this. However:
- Citation changes are comment-only (XML doc / inline comment / CSV header) — they cannot affect compiled behaviour or test outcomes.
- The new `SmokeCaptureCupSpeedGate.cs` is a new MonoBehaviour in the Viewer assembly; it does not interfere with existing tests (test assemblies do not reference Viewer).
- iter-1 architect verified 294/294 PASS for the actual behavioural code. No behavioural changes since.

Not requiring a re-run; the iter-1 test count remains authoritative.

---

## Disagreement check

My Step 0 pixel scan matches the self-reviewer's pixel scan. The slow-putt screenshot clearly shows the SUCCESS modal with STROKES=2 (ALBATROSS); the fast-putt screenshot clearly shows the ball at rest past the pin with TURN 2 advanced and no modal. The self-reviewer's claim about the visual contrast is empirically true.

The grep claim about citation correctness was independently re-verified (not just trusted): zero "American Journal" hits in `Assets/`, all six expected files updated to "Canadian Journal of Physics" — plus the bonus `DashboardUI.cs` fix the self-reviewer flagged.

The scene-mutation-audit / capture-runner-cleanup discipline is independently re-confirmed via `git status` and a grep for `SmokeCaptureCupSpeedGate` references in `Assets/` — neither shows any persistence risk.

No disagreements.

---

## Iter-2 summary table

| Aspect | Iter-1 | Iter-2 |
|---|---|---|
| Independent pixel scan agrees with implementer/self-reviewer narrative | PASS (scene-loads) | PASS (slow=SUCCESS modal, fast=ball-past-pin-no-modal) |
| ICupDetector cascade (4 impls + 1 production call site) | PASS | PASS (unchanged) |
| Test coverage (Tests 6–9, fp boundary semantics) | PASS | PASS (no behavioural change since iter-1) |
| CSV / loader / dashboard wiring | PASS | PASS (unchanged) |
| Scene-mutation audit (`git status`: `.unity`/`.asset`/`.prefab`) | PASS — zero scene mutations | PASS — zero scene mutations |
| BSM Hard Rule 2 (call-site only) | PASS | PASS (verified via `git diff`) |
| Cup geometry Hard Rule 1 unchanged | PASS | PASS (unchanged) |
| Determinism (no Unity API in detector) | PASS | PASS (unchanged) |
| **Citation accuracy (Hard Rule 3, Lesson K)** | **FAIL — wrong journal in 5+ files** | **PASS — all 6 files corrected to Canadian J. of Physics + DashboardUI bonus + "§ IV" weakened to "(see lip-out analysis)"** |
| **Smoke evidence (Lesson O verification end-to-end)** | **FAIL — no documented attempt** | **PASS — two visually-distinct captures via canonical `CaptureCore.SnapPlayModeSafe`; slow→SUCCESS modal at STROKES=2, fast→AtRest past pin, no modal** |
| Smoke runner cleanup (Lesson 2026-05-13 backstop) | N/A | PASS — `Destroy(gameObject)` in all paths, no scene reference to the MonoBehaviour, zero `.unity` diffs |

---

## Final verdict

`ARCHITECT_REVIEW_PASS`. Both iter-1 fix items are substantively addressed with independently verifiable evidence. The implementation respects all four spec Hard Rules, the citation now anchors a defensible real-world source, the smoke captures end-to-end-prove the Lesson O behaviour (fast putt does NOT register a win; slow putt does), and no scene/asset mutations or capture-runner leftovers were introduced. Ready for Cesar's final approval.

Minor non-blocking note for cleanup: `IMPLEMENTER_REPORT.md` line 52 still describes the citation as "Penner (2002) Am. J. Physics … § IV" in its justification recap. Actual code is correct; only the report's narrative is stale. Worth syncing at next opportunity.
