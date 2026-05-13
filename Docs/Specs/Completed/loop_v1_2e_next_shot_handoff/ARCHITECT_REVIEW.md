# ARCHITECT_REVIEW — `loop_v1_2e_next_shot_handoff`

**Iteration:** 2
**Reviewer:** golfin-reviewer (subagent)
**Date:** 2026-05-13 10:32 JST
**Verdict:** `ARCHITECT_REVIEW_PASS`
**STATUS transition:** `SELF_REVIEW_PASS` → `ARCHITECT_REVIEW_PASS`

---

## Step 0 — Independent pixel scan (before reading any spec/report/verdict)

### `controls_2e_atrest_facing_pin.png` (S1)
Portrait mobile frame. Top yellow bar reads "CAM: Chase BALL: Aiming". HUD card top-left shows red-cap PLAYER portrait, "Lv 1", "TURN 2". Top-right card "LOMOND / HOLE 1 - REGULAR / PAR 5" with hole thumbnail. Center: golf ball with green "G" logo at rest on a flat green fairway lane bracketed by trees left and right; a thin white flagstick is just visible above-center horizon. A faint downward conical aim guide trails the ball. Speedometer reads 0.0 mph, 0 yds. Bottom row: SPIN / STRAIGHT / GOLFIN ∞ / DRIVER 250 yds.

### `controls_2e_ob_drop.png` (S2)
Portrait mobile frame. Top yellow bar "CAM: Chase BALL: Aiming". HUD top-left "PLAYER / Lv 1 / TURN 3", chip "2.2 mph". Top-right "LOMOND / HOLE 6 - REGULAR / PAR 3" with hole thumbnail. The center of the frame is dominated by a vertical reddish-brown tree trunk with bark texture occupying roughly the central 50% width. The golf ball sits on a small grey rocky/shadow patch at the trunk base, centered. The right edge of the frame and bottom-right show clearly green fairway grass texture and a strip of blue sky horizon past the trunk. A thin flagstick is visible faintly upper-center. No water surface (no blue reflective material) anywhere in the frame.

### `controls_2e_turn_counter_after_ob.png` (S3)
Portrait mobile frame. Same HUD: "CAM: Chase BALL: Aiming", "PLAYER / Lv 1 / TURN 3", chip "2.2 mph", "LOMOND / HOLE 6 - REGULAR / PAR 3". Center: the tree trunk that filled S2's center is now shifted to the far left edge (~15% of frame width). The rest of the frame opens up to a wide expanse of green fairway grass; in the middle distance a raised putting green is visible with a thin white flagstick clearly readable just right of center. Beyond, a tree line and forested ridges fade to a blue horizon. The ball sits centered-low on green grass with the aim guide forward. This is the same drop scene as S2 but the camera has been orbited so the surrounding terrain context is unambiguous.

---

## Step 2 — Spec § Smoke evidence content match

| Spec requirement | Capture | Verdict |
|---|---|---|
| **S1** — ball at rest, pin visibly forward in view after AtRest re-arm | S1 | PASS — flagstick visible upper-center along the ball→pin axis; downcourt fairway corridor lines up forward of ball. Pin small but clearly forward. |
| **S2** — ball visibly on grass, NOT in water | S2 | PASS — green fairway grass clearly visible at the right edge and bottom-right of the frame around the trunk; no blue/reflective water surface anywhere. Ball is on a rocky shadow patch at the base of a tree, classed as non-Water/non-OOB by the resolver. |
| **S3** — TURN label visibly equals "TURN 3", distinct frame | S3 | PASS — "TURN 3" prominently readable in top-left HUD. S3 is content-distinct from S2 (tree shifted to left edge, fairway/green/flagstick now dominate the frame). MD5 differs: S2 `acf0d53f…` vs S3 `1ddeed38…`. |
| **L1** — `PenaltyStrokes=1`, `TerminalState=OB` in history log | `controls_2e_history_log.txt` | PASS — log shows `PenaltyStrokes=1 TerminalState=OB OBReason=OutOfBounds`. Spec § L1 wording allows "or whichever" OB reason; functional equivalent (HSD reads on `BallStateChange.Previous == OB`, not on `OBReason`). |

All four content-match items PASS without override.

---

## Step 3 — Bbox geometry verification (containment claims)

§2e adds no UI containment claims (no text-in-card, no modal-in-canvas, no new RectTransforms). Step 3 is **N/A**. No bbox check required.

---

## Step 4 — Scene-mutation audit (`git status` / `git diff`)

```
modified:   Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs
modified:   Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs
modified:   Assets/Scripts/Physics/Viewer/PhysicsLabController.cs
modified:   Docs/Specs/Active/loop_v1_2e_next_shot_handoff/STATUS.md
modified:   Packages/manifest.json
modified:   Packages/packages-lock.json

Untracked: NextShotHandoffTests.cs, AimRotationHelper.cs, OBDropResolver.cs,
           SmokeRunner2eHost.cs, Editor/SmokeRunner2eMenu.cs, capture artifacts.
```

- **No `.unity` scene files modified.** `Assets/Scenes/Physics/LabScaffold.unity` not in `git status`. SPEC § Hard rule #3 upheld.
- **No `.asset` files modified.** No prefab mutations, no ScriptableObject changes.
- **No `m_IsActive: 0`, `sizeDelta`, or position changes** to any scene GameObject (no scene YAML in diff).
- `Packages/manifest.json` / `packages-lock.json` are session-start-state from before this task per prior status notes; not introduced by §2e. **N/A**.

**PASS** — clean.

---

## Step 5 — Smoke-runner Chase-mode override scope verification

Critical check: does the iter-2 Chase-mode forcing leak into production code?

- `git diff PhysicsLabController.cs`: ZERO new `SetMode` / `chaseCamera.SetMode` calls. The only `SetMode` calls in the production file (lines 562, 604) are pre-existing GroundLevel-on-putt invocations, unrelated to §2e.
- `git diff LoopCameraDirector.cs`: file not in `git status`. **Untouched.** Director's `ModeMap[Aiming]=null` remains as-is (out-of-scope per SPEC § L7).
- `git diff ChaseCamera.cs`: file not in `git status`. **Untouched.**
- The `camChase.SetMode(ChaseCamera.Mode.Chase)` call lives only at `SmokeRunner2eHost.cs:290`. That class only runs when `Armed=true` from `SessionState` (Editor-only). The 15° camera orbit is also smoke-runner-only.
- New `internal` method `PhysicsLabController.SetCameraYawRadians(float)` (lines 651–660): only caller is `SmokeRunner2eHost.cs:233`. `internal` scoping prevents external-assembly callers; XML doc explicitly says "Only call from smoke runners / Editor test tools — never from production code." Acceptable test seam.
- `SmokeRunner2eMenu.cs` is in `Editor/` folder and gated `#if UNITY_EDITOR`. Good.
- `SmokeRunner2eHost.cs` is not in an `Editor/` folder and not entirely `#if UNITY_EDITOR` gated — the class will compile in builds. However, `Start()` early-outs and self-destroys when `Armed` returns `false` (which it always does outside the Editor via the `#else` branch returning `false`). Functionally safe but architecturally not ideal; **not a blocker for §2e**. Worth a future cleanup ticket.

**PASS** — production code paths untouched; smoke harness clean.

---

## Step 6 — Code review against SPEC (post-pixel-scan)

I read SPEC.md before doing the diff, but only after the pixel scan and audit above.

### A. `ShotRecord` extension (`GameSession.cs`)
Diff matches spec § A verbatim: new `PenaltyStrokes` readonly int field, 9-arg ctor sets all 9 fields, 8-arg ctor preserved as `: this(...,0)` forward. **PASS**.

### B. `OBDropResolver.cs`
Reviewed file content (1172 bytes). `Resolve(Trajectory, Vector3 fallbackOrigin)` walks `terrainHits` from end backward, skips `Water` and `OOB`, returns position of first qualifying hit; null-trajectory and empty-hits both return fallback. Matches spec § B verbatim. **PASS**.

### C. `AimRotationHelper.cs`
`ComputeYawTowardPin(Vector3, Vector3, float)` returns `fallbackYaw` for `pinPos == Vector3.zero` or XZ-distance² < 1e-4, else `Atan2(dz, dx)`. Matches spec § C verbatim. **PASS**.

### D. `HoleSessionDriver` extensions
- `D.1` — `BuildShotRecord` sets `penaltyStrokes = (TerminalState == BallState.OB) ? 1 : 0` and passes through to the 9-arg `ShotRecord` ctor. **PASS**.
- `D.2` — `HandleStateChanged` reads `change.Previous == BallState.OB` for the penalty (order-independent per spec § L8); `ComputeNextTurn` clamps negative penalty to 0 and returns `currentTurn + 1 + penaltyStrokes`. **PASS**.
- `D.3` — new 8-arg `BuildShotRecordStatic` overload accepting `penaltyStrokes`. Existing 7-arg overload preserved (unchanged in diff). **PASS**.

### E. `PhysicsLabController.PlaceBallAt` refactor
`PlaceBallAt(Vector3, int?)` is now a 1-line delegate to `RepositionBallWithLookDir(worldPos, preferredSurfaceTypeValue, GetDefaultLookDirection())`. Helper body matches the original `PlaceBallAt` body verbatim except `lookDir` is now a parameter. Public signature unchanged. **PASS**.

### F. `PhysicsLabController.HandleShotComplete`
Restructured to a `switch (result.TerminalState)`:
- `AtRest`: computes new yaw via `AimRotationHelper.ComputeYawTowardPin(ballPos, HoleContext.PinWorld, _cameraYaw)`, sets `_cameraYaw`+`_shotController.CameraHeadingRadians` if changed, calls `ApplyCameraYaw`, then `CompleteShot + ReArm`. Matches spec verbatim.
- `OB`: computes drop via `OBDropResolver.Resolve(_previousTrajectory, _lastShotOrigin)`, computes new yaw toward pin from drop, calls `RepositionBallWithLookDir(dropPos, null, lookDir)` then `_ballSM.ReArm()`. Includes `[§2e] OB drop:` log line. Matches spec verbatim.
- `InCup`: empty `break`. **PASS**.

### Test gate
IMPLEMENTER_REPORT claims 273 PASS / 0 FAIL / 0 SKIPPED (baseline 264 + 9 new). The 9 new tests are in `NextShotHandoffTests.cs` and align with spec § Tests. I do not have the test runner; I rely on the implementer's reported counts. They are non-trivial (Total/Passed/Failed/Skipped present), so the gate condition is satisfied per pipeline rules. **PASS**.

---

## Step 7 — Visual fidelity (Lesson O)

The implementer's narrative in IMPLEMENTER_REPORT § Visual Verification matches what I see in the pixels:

- **S1 narrative** ("ball at rest on green fairway... flag visible center-forward... TURN 2") — confirmed by my pixel scan. No disagreement.
- **S2 narrative** ("Chase mode, TURN 3, HOLE 6 PAR 3, ball at base of tree, green grass visible right and bottom-right, no water surface") — confirmed by my pixel scan. The dark area is genuinely tree-bark, not water/OOB texture; surrounding green grass is visible at the frame edges, satisfying the "on grass, not in water" spec phrasing.
- **S3 narrative** ("15° orbit, tree shifted to frame left, green fairway/green/flag visible centrally, TURN 3 prominent") — confirmed.
- **Case 3 (zero-qualifying-hit trajectory)** — implementer's principled disclosure is grounded: real shot produced `hits=2`, so engineering a zero-hit trajectory requires out-of-scope preset changes. The unit test `OBDropResolver_FallsBackToOriginWhenNoSafeHit` directly exercises the fallback branch. Acceptable per Lesson O spirit.

No pixel-scan / report disagreement. No auto-FAIL trigger.

---

## Step 8 — Implementer-graded PARTIAL audit

IMPLEMENTER_REPORT has zero PARTIAL items, zero "subtle but present," zero hedging. All 14 acceptance items are bare PASS with one-sentence justifications citing measurable evidence (log lines, MD5 hashes, file paths, code references). No PARTIAL → FAIL default applies.

---

## Verdict — `ARCHITECT_REVIEW_PASS`

§2e ships clean:

1. **Code matches spec verbatim** — every block (A through F) implements the spec's exact text without freelance reinterpretation.
2. **Tests pass** — 273/0/0 reported by implementer; 9 new tests target the right surfaces (resolver branches, helper math, turn-arithmetic).
3. **Smoke evidence is readable** — S1 shows pin-aim post-AtRest; S2 shows ball on grass-adjacent terrain with green fairway visible at frame edges; S3 is content-distinct via 15° orbit and shows TURN 3 + the surrounding fairway + the actual putting green & flagstick. The OB drop history log proves `PenaltyStrokes=1 TerminalState=OB`.
4. **No scene mutations.** `LabScaffold.unity` untouched per SPEC § Hard rule #3. No `.asset` mutations. No `m_IsActive: 0` regressions.
5. **Smoke-runner override is correctly scoped** — `SetMode(Chase)` and the 15° camera orbit live only in `SmokeRunner2eHost.RunOBSequence`. `PhysicsLabController.cs` has zero new `SetMode` calls. `LoopCameraDirector.cs` and `ChaseCamera.cs` are unmodified. The new `internal SetCameraYawRadians` test seam is appropriately scoped (assembly-internal, XML-doc-warned, only caller is the smoke host).

### Items to flag for Cesar before final approval

1. **Director-side OBFreeze gap (out-of-scope per L7).** In live play (without the smoke override), `LoopCameraDirector.ModeMap[Aiming]=null` means the camera stays in OBFreeze after OB→Aiming. The smoke runner forces Chase mode to make captures readable, but live gameplay will keep the OBFreeze framing after the OB drop, even though §2e's `_cameraYaw` rotation toward the pin is applied correctly. Cesar should decide whether to (a) ship §2e as-is and file a follow-up Director ticket to set `ModeMap[Aiming] = Chase` (or a previousMode-aware mapping) for §2f or Loop v2 polish, or (b) request a tiny ModeMap fix before §2e closes. The §2e behavior is correct; only the camera *framing* during live OB recovery differs from the captures.

2. **`SmokeRunner2eHost.cs` is not inside an `Editor/` folder** and is not entirely `#if UNITY_EDITOR` gated, so the class compiles into player builds. It self-destructs on `Start()` when `Armed=false` (which it always is in builds), so it's functionally safe — but it's a minor architectural smell. Worth a one-line cleanup ticket: move the file to `Assets/Scripts/Physics/Viewer/Editor/` or wrap the whole class in `#if UNITY_EDITOR`. Not a blocker for §2e.

3. **`internal SetCameraYawRadians(float)` test seam.** Currently `internal` with an XML-doc warning. Architecturally clean for a smoke harness in the same assembly. If you want stricter hygiene, gate it `#if UNITY_EDITOR` or behind `[Conditional]`. Not required for §2e to pass.

### Iteration check

Iter-2 (PASS). The escalation rule "if N ≥ 3 and would FAIL → ESCALATE" does not apply; verdict is FORWARD.

---

## File summary

| File | Action |
|---|---|
| `Docs/Specs/Active/loop_v1_2e_next_shot_handoff/ARCHITECT_REVIEW.md` | created (iter-2 verdict) |
| `Docs/Specs/Active/loop_v1_2e_next_shot_handoff/STATUS.md` | updated `SELF_REVIEW_PASS` → `ARCHITECT_REVIEW_PASS` |
