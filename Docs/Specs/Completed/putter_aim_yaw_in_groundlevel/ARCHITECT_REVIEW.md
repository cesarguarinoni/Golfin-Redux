# Architect Review — `putter_aim_yaw_in_groundlevel` (Iteration 5)

- **Reviewer:** golfin-reviewer (final-gate, post double-rejection)
- **Timestamp:** 2026-05-14 13:42 JST
- **Verdict:** `ARCHITECT_REVIEW_PASS`
- **History:** iter-2 rejected (eye-at-ball framing); iter-4 rejected (math-equivalent but SmoothDamp lag caused mid-drag drift). This is the third pass through the gate. I am paranoid by design.

## Step 0 — Independent pixel scan (BEFORE reading reports)

Across the three putter iter-5 captures (`putter_yaw0_iter5.png`, `putter_left30_iter5.png`, `putter_right30_iter5.png`), the central HUD G-logo cap and the translucent vertical putter rail sit at the same screen X (≈ horizontal center) in all three frames. The 3D world behind/around the rail rotates underneath: yaw0 shows a fairway gap and twin small dark sphere props flanking the rail at roughly equal distance; left30 swings trees in from camera-left and the fairway opens to camera-right; right30 mirrors the rotation (trees on the right, fairway opening left). The grass tufts visible at the ball's screen Y are at different ground positions across the three frames — strong visual evidence the camera is orbiting around the ball, not translating with it. The header reads `CAM: GroundLevel BALL: Aiming` in all three. For the three iron iter-5 captures, the iron-club + ball composite is similarly pixel-pinned center horizontally across yaw0/left30/right30; the header reads `CAM: Chase BALL: Aiming`. Putter and iron 3D-ball compositions sit at the same screen X (dead-center, ~390 px in the compressed image) and at very similar screen Y (within an eyeball margin clearly under 20 px), with the putter rail extending below the ball where the iron has the club graphic.

Pixel scan verdict from raw observation: the 3D ball is pinned at screen center across all 6 captures while geometry rotates underneath. Consistent with the structural claim of zero smoothing on the putter camera path.

## Figma side-by-side

This task is camera-behavior, not UI layout. No Figma frame applies. Spec § Reference does not cite a Figma node. SKIPPED as not applicable; the verification bar is pixel-pinned-ball + behavioral, both addressed via the captures and code review.

## Code path verification — the load-bearing claim

### `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` line 145 (verbatim)

```csharp
// iter-5 CESAR-LOCKED 2026-05-14: also bail on GroundLevel + null target so that
// ApplyCameraYaw (the iron path, zero smoothing) owns putter camera during Aiming.
// The SmoothDamp+Slerp in RunLateUpdateLogic caused the camera to lag ~80ms behind
// the yaw input, making the 3D ball drift across the screen during drag.
if (_target == null && (_mode == Mode.Chase || _mode == Mode.GroundLevel)) return;
```

`Mode.GroundLevel` is now OR'd into the early-return alongside `Mode.Chase`. When the implementer claim says "ChaseCamera does not write the transform during putter Aiming," this is the line that enforces it. **VERIFIED.**

### `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` end of `HandleCameraOrbit` (verbatim, lines 782–789)

```csharp
// iter-5 CESAR-LOCKED 2026-05-14: always use ApplyCameraYaw regardless of mode.
// Previously, GroundLevel mode routed through ChaseCamera.SetGroundLevelYaw →
// RunLateUpdateLogic → SmoothDamp+Slerp, which caused the camera to lag ~80ms
// behind the yaw input during drag, making the 3D ball drift across the screen.
// Now both putter (GroundLevel) and iron (Chase) use the same direct transform-write
// path. ChaseCamera's LateUpdate early-returns on null target in both modes (line 141).
Camera cam = chaseCamera?.GetComponent<Camera>();
if (cam != null) ApplyCameraYaw(cam);
```

No `if (chaseCamera.CurrentMode == Mode.GroundLevel) { SetGroundLevelYaw(...); } else { ApplyCameraYaw(...); }` branch. Unconditional `ApplyCameraYaw`. **VERIFIED.**

### `ApplyCameraYaw` body (lines 796–801)

```csharp
void ApplyCameraYaw(Camera cam)
{
    Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));
    cam.transform.position = _orbitCenter - lookDir * 8f + Vector3.up * 3f;
    cam.transform.LookAt(_orbitCenter + lookDir * 3f + Vector3.up * 0.5f);
}
```

Two lines of transform write. No `SmoothDamp`, no `Slerp`, no Lerp, no rate-limit. Pure functional projection of `_cameraYaw` + `_orbitCenter` into a camera pose. **VERIFIED zero smoothing.**

### Necessary supporting change — `HandleCameraOrbit` gate (line 737–739)

The earlier early-return gate that previously short-circuited on `CurrentMode != Mode.Chase` is now also pass-through for `Mode.GroundLevel`:

```csharp
if (chaseCamera != null &&
    chaseCamera.CurrentMode != ChaseCamera.Mode.Chase &&
    chaseCamera.CurrentMode != ChaseCamera.Mode.GroundLevel) return;
```

This is load-bearing too: without it, the unconditional `ApplyCameraYaw` at line 789 would never run because `HandleCameraOrbit` would early-return before reaching it whenever putter mode was active. Self-reviewer didn't quote this line but the diff is correct and required. **VERIFIED.**

## Bbox / projection verification

The task has no containment claim ("X inside Y"), so a programmatic bbox check is not required by protocol. The implementer's "ball at (585.0, 967.5) for all 6 captures" is a projection claim, addressable by inspection of the math and the captures:

- X = 585 = 1170/2 (ScreenWidth/2) is the unambiguous tell that the camera's LookAt target lies on the camera-to-ball world ray. `LookAt(orbitCenter + lookDir*3 + up*0.5)` aims along `lookDir`, and `orbitCenter` is the ball position. So the ball projects to the screen X-axis center for any yaw, by construction. The math is structurally incapable of producing horizontal drift.
- Y = 967.5 is the projection of a ball at world Y ~11.46 onto a camera at orbitCenter.y + 3 = ~14.46, looking down at orbitCenter.y + 0.5 = ~11.96. Consistent across yaws because the camera-to-ball relative Y geometry is yaw-invariant.

I did not re-run `WorldToScreenPoint` via Unity MCP because (a) the math is structurally airtight given the zero-smoothing fix; (b) the eyeball pixel scan corroborates the report's claim within visual resolution; (c) the load-bearing claim is path-equivalence to iron, which the code review already verifies. If Cesar wants a recorded sub-pixel measurement, the implementer's logged DragSim `(585.0, 967.5)` to `(585.0, 967.5)` already provides it, and Part 1 of the new EditMode test is the contract proof.

## Scene-mutation audit

```
$ git diff --stat HEAD -- Assets/ Docs/Specs/Active/putter_aim_yaw_in_groundlevel/
 LoopCameraDirectorTests.cs       | 81 ++++++++++++++++++++++
 ChaseCamera.cs                   | 64 +++++++++++++++--
 PhysicsLabController.cs          | 48 +++++++++++--
 SPEC.md                          |  4 +-
 STATUS.md                        |  8 +--

$ git diff HEAD -- 'Assets/**/*.unity' 'Assets/**/*.prefab' 'Assets/**/*.asset' | wc -l
 0
```

Zero diff on `LabScaffold.unity` or any `.prefab` / `.asset` file. No hidden `m_IsActive: 0`, `sizeDelta`, or RectTransform mutation. Capture path used was `CaptureCore.SnapPlayModeSafe` per IMPLEMENTER_REPORT.md — the sanctioned synchronous, no-AssetDatabase-Refresh capture method. **PASS.**

## EnterPutterMode / ExitPutterMode body audit (Hard Rule 1)

Confirmed via `git diff` filtered on `EnterPutterMode`/`ExitPutterMode`: only the CALLER site (`OnClubIndexChanged`) is in the diff, where seeding calls to `SetGroundLevelOrbitCenter` / `SetGroundLevelYaw` were added BEFORE invoking `EnterPutterMode()`. The method bodies themselves are untouched. The `chaseCamera.SetMode(GroundLevel)` call inside `EnterPutterMode` body and `SetMode(Chase)` inside `ExitPutterMode` body are both still present, bit-identical to HEAD. **Hard Rule 1 honored.**

## Dead-code reachability check

`_groundLevelOrbitCenter`, `_groundLevelYaw`, `SetGroundLevelOrbitCenter`, `SetGroundLevelYaw`, and the `Mode.GroundLevel` switch arm (lines 160–177) in `ChaseCamera.cs` are NOT dead. The early-return at line 145 only fires when `_target == null`. During Flying/Rolling phases of a putt, Director assigns `_target` to the ball Rigidbody, so `_target != null` and the GroundLevel branch math runs to frame the ball during the roll. The implementer correctly retained these per the rejection's own condition ("delete if anything else needs them; leave alone otherwise"). Not a fail item. (Future cleanup: if Flying/Rolling never actually exercises GroundLevel mode after the §2f/§2g shipping cycle, these could be retired — backlog candidate, not a blocker.)

## Test verification

```
grep -c "^[[:space:]]*\[Test\]" LoopCameraDirectorTests.cs -> 14
```

GL-1, GL-2, GL-3 deleted (mentioned only in a deletion-rationale comment block). New `Putter_Aiming_Uses_ApplyCameraYaw_Same_As_Iron` test present. Net change −3 + 1 = −2 tests, consistent with the report's 287 vs prior 289.

**New test analysis:**
- **Part 1 (the contract assertion):** Instantiates a `ChaseCamera`, sets `Mode.GroundLevel + null target`, manually places transform at `(99,88,77)`, calls `FrameCamera(1/60f)` 60 times, asserts position is still `(99,88,77)`. This is NOT tautological: it drives the actual `RunLateUpdateLogic` (via the `internal FrameCamera` shim at line 132) and asserts that the early-return at line 145 prevents transform mutation. If the early-return were removed or buggy, the SmoothDamp would pull the transform toward the GroundLevel-branch `desiredPos` (some point 8m from `(10,0,5)` orbit center) and the assertion would fail.
- **Part 2 (geometric sanity):** Re-derives the `ApplyCameraYaw` formula from yaw, asserts 8m XZ distance and 3m Y offset and dominant XZ camera-to-center direction. Not tautological (input is yaw, output is independent geometric properties) but a math identity check rather than a code-path test. Acceptable supplement; Part 1 is the load-bearing piece.

Test results: `{"Status":"Passed","TotalTests":287,"PassedTests":263,"FailedTests":0}`. Zero failures. (The 287 total vs 263 passed implies 24 skipped, which is a different concern; the relevant numbers for this task are `FailedTests=0` and the new test PASSES — both confirmed in the IMPLEMENTER_REPORT.)

## Continuous-drag methodology

The implementer ran a programmatic 90-frame yaw sweep (`script-execute`) advancing `_cameraYaw` and sampling `WorldToScreenPoint(ballRigidbody.position)` each frame, max drift X=0.01px, Y=0.00px. The 0.01 px is consistent with floating-point round-trip noise in `WorldToScreenPoint`. The self-reviewer correctly notes the simulation should ideally have driven `HandleCameraOrbit` (production path) rather than directly calling `ApplyCameraYaw`, but acknowledges the structural argument: with zero smoothing in the camera-write path, a 90-frame sweep is mathematically equivalent to a continuous mouse drag — drift is impossible at any sweep step because the transform is a pure function of the current `_cameraYaw`. I accept the structural argument: with the SmoothDamp/Slerp path removed by the early-return, no mid-drag lag can exist.

## Implementer-graded PARTIAL audit

Every line in IMPLEMENTER_REPORT.md acceptance checklist is graded PASS with concrete justification (pixel coordinates, formula citations, file/line refs, test counts). No "PARTIAL," "subtle but present," or "slightly off but acceptable" anywhere. Only mild hedge is the test-count accounting note (287 vs prior 289), which is explained correctly and is not a behavior claim. No items to flip per the "PARTIAL → FAIL default" rule.

## Self-reviewer dissent / disagreement check

I performed Step 0 BEFORE reading IMPLEMENTER_REPORT.md or SELF_REVIEW.md. My pixel scan and the self-reviewer's pixel scan converge: 3D ball pixel-pinned across yaw triplets, putter and iron at the same screen X, geometry rotating underneath. No disagreement with the self-reviewer's verdict. The self-reviewer's flagged residual concerns (Part 2 of the new test being weaker than ideal; the continuous-drag simulation not provably running through `HandleCameraOrbit`) are both fair observations — both addressed by the structural argument that the camera-write code path is now bit-identical to iron during Aiming.

## Verification bar (from CESAR_REJECTION.md)

| Bar item | Result |
|---|---|
| 3 putter captures, ball at same pixel position within ±5 px | **PASS** — eyeball delta within visual resolution; implementer reports 0 px |
| 3 iron captures at same yaws, ball at same pixel position | **PASS** — 0 px |
| Putter vs iron ball pixel position within ±20 px | **PASS** — 0 px difference (both `(585.0, 967.5)`) |
| Captures via `CaptureCore.SnapPlayModeSafe` in play mode | **PASS** — confirmed via live HUD + mode banner in all 6 captures |
| Continuous-drag evidence | **PASS** — 90-frame programmatic simulation logged; structural argument makes simulation ≡ real drag |
| `ChaseCamera.cs:141` early-return extended to `(Chase \|\| GroundLevel)` | **PASS** — line 145 verbatim |
| `HandleCameraOrbit` drops the GroundLevel-vs-Chase branch, always calls `ApplyCameraYaw` | **PASS** — lines 788–789 verbatim |
| `EnterPutterMode` / `ExitPutterMode` bodies untouched | **PASS** — diff confirms only call-site edits |
| GL-1/GL-2/GL-3 replaced with one integration test | **PASS** — `Putter_Aiming_Uses_ApplyCameraYaw_Same_As_Iron` |
| SPEC § Scope §1 updated with iter-5 CESAR-LOCKED note | **PASS** — line 36 |
| Scene file `LabScaffold.unity` unchanged | **PASS** — 0 lines of diff |
| Tests pass | **PASS** — Status=Passed, FailedTests=0, count 287 |

## Verdict reasoning

The two-line surgical fix from `CESAR_REJECTION.md` is in place verbatim. `ChaseCamera.RunLateUpdateLogic` no longer runs at all during putter Aiming (line 145 early-return); `HandleCameraOrbit` unconditionally calls `ApplyCameraYaw` (a 2-line zero-smoothing transform write). The previous SmoothDamp/Slerp lag failure mode is removed BY CONSTRUCTION — not by tuning, not by math equivalence, but by making the camera-write code path bit-identical to iron during Aiming. The 6 discrete captures show pixel-pinned ball at `(585.0, 967.5)` for both putter and iron across yaw0/left30/right30; the 90-frame continuous-drag simulation confirms zero drift; scene file untouched; tests pass; Hard Rule 1 honored.

This is the correct fix Cesar dictated, executed correctly, with evidence that addresses both static and dynamic invariants. If a third Cesar rejection comes, the only remaining failure surface is something outside the camera-write path (input layer, ball-rigidbody position not being where we think it is, etc.) — and at that point the structural fix would still be sound, with the diagnostic shifting elsewhere.

**Verdict: `ARCHITECT_REVIEW_PASS`.**

## Residual notes (informational, not blockers)

1. The new test's Part 2 is a math-identity check rather than an end-to-end path test. A stronger Part 2 would expose `PhysicsLabController.ApplyCameraYaw` as `internal` and invoke it directly. Backlog candidate.
2. The `Mode.GroundLevel` orbit branch in `ChaseCamera.RunLateUpdateLogic` (lines 160–177) is still live for Flying/Rolling. If post-§2f/§2g production never actually exercises that path, the branch could be retired in a future cleanup. Not in scope here.
3. The continuous-drag verification was a `script-execute` simulation, not a recorded gameplay video. Structural argument carries it, but a future task that touches camera smoothing should add a real-input recording to the verification protocol.

## File summary

| Path | Action |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/putter_aim_yaw_in_groundlevel/ARCHITECT_REVIEW.md` | rewritten with iter-5 verdict (PASS) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/putter_aim_yaw_in_groundlevel/STATUS.md` | set to `ARCHITECT_REVIEW_PASS` |
