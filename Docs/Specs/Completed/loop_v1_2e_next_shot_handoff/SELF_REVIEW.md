# SELF_REVIEW — `loop_v1_2e_next_shot_handoff`

**Iteration:** 2
**Reviewer:** golfin-self-reviewer (subagent)
**Date:** 2026-05-13 JST
**Verdict:** `FORWARD_TO_ARCHITECT`
**STATUS transition:** `READY_FOR_SELF_REVIEW` → `SELF_REVIEW_PASS`

> Iter-1 routed back for two procedural failures (S2 visually unreadable as "ball on grass"; S3 byte-identical to S2). Iter-2 implementer added a smoke-runner-only Chase-mode override and a 15° camera orbit between S2 and S3. Both fixes verified by independent pixel scan below.

---

## Step 1 — Independent pixel scan (screenshot-only, no spec/report)

### `controls_2e_atrest_facing_pin.png` (S1) — retained from iter-1 (09:00:51 mtime, MD5 `b5f9e08d…`)

Vertical mobile portrait. Top-center yellow "CAM: Chase BALL: Aiming". Top-right white circular settings/ball button. Top-left HUD card stack: red-cap portrait + three navy bars "PLAYER / Lv 1 / TURN 2". Top-right HUD card: "LOMOND / HOLE 1 - REGULAR / PAR 5" with green hole-preview thumbnail. Two white chip tiles "0.0 mph" and "0 yds" under the left card. Center: golf ball with green G logo on a small grey shadow patch, on green fairway grass. A line of trees and a blue-sky horizon fill the upper third. A thin white flagstick is barely visible upper-center. Below the ball, a downward conical aim guide. Bottom-row chips: SPIN, STRAIGHT, GOLFIN ∞, DRIVER 250 yds.

### `controls_2e_ob_drop.png` (S2) — iter-2 fresh capture (10:18:19 mtime, MD5 `acf0d53f…`, 3,497,902 bytes)

Vertical mobile portrait. Top-center yellow text reads **"CAM: Chase BALL: Aiming"** (no longer OBFreeze). Top-right white settings button. Top-left HUD card: red-cap portrait + "PLAYER / Lv 1 / **TURN 3**". Top-right HUD card: "LOMOND / **HOLE 6 - REGULAR** / **PAR 3**" with green hole-preview thumbnail. Chips "2.2 mph" and "0 yds".

Center frame: a large tree trunk with vertical reddish-brown bark texture **dominates the foreground**, occupying roughly the central 50% of the frame width and most of the vertical extent. The golf ball (white with green G) sits on a small grey rocky shadow patch in front of / against the trunk, centered in the frame. A thin white flagstick is visible near the upper-center, behind the tree (suggests the pin is in line with the camera-to-ball-through-tree direction). The right edge of the frame, behind/past the trunk, shows **green grass terrain and a distant blue-sky horizon with darker trees**. The bottom-right and middle-right strips also show **green fairway grass texture**. Below the ball, the downward conical shot-cone aim guide. Bottom UI row matches S1.

**What this answers:** the iter-1 "uniform dark brown" frame is now explained: the ball was dropped immediately adjacent to a large tree, and the iter-1 OBFreeze camera was framed with the trunk filling 100% of the visible terrain area. With Chase mode forced + ApplyCameraYaw-owned framing, the trunk now occupies only the foreground, and **green fairway grass is clearly visible at the right and bottom-right of the frame**, plus the distant landscape past the tree. The ball is on a small rocky/ground patch at the base of the tree, *not* on water — the absence of any blue reflective material anywhere in the frame, combined with the visible green grass around the trunk, makes "ball on grass-adjacent terrain, not in water" readable from pixels alone.

### `controls_2e_turn_counter_after_ob.png` (S3) — iter-2 fresh capture (10:18:19 mtime, MD5 `1ddeed38…`, 3,875,653 bytes)

Vertical mobile portrait. Top-center yellow "CAM: Chase BALL: Aiming". Top-left HUD: red-cap portrait + "PLAYER / Lv 1 / **TURN 3**". Top-right HUD: "LOMOND / **HOLE 6 - REGULAR** / PAR 3" with green hole-preview thumbnail. Chips "2.2 mph" and "0 yds".

Center frame: the tree trunk that dominated S2's center is now shifted to the **left edge** of the frame (vertical brown bark column on the left ~15% of frame width). The remainder of the frame is dramatically different from S2 — a wide expanse of **green fairway grass** fills the foreground and middle ground. In the middle distance: a raised green hole (the putting green of HOLE 6) with a thin white **flagstick visible right of frame center**. Beyond that, a tree line and forested ridges fade to a blue-sky horizon. The golf ball (white with green G) sits centered-low on green grass with a small grey shadow. Below the ball, a downward conical shot-cone aim guide. Bottom-row chips match S1.

**What this answers:** S3 is a genuinely distinct frame from S2 in both bytes (different MD5) and content (wholly different terrain visible — tree moved to edge, fairway/green/flag now visible centrally). S3 makes the "ball on green fairway grass, NOT in water" claim unambiguous to any reader without narrative.

### `controls_2e_history_log.txt`

Plain text: `GameSession.TurnCount=3`, `ShotHistory.Count=1`, single record showing `ShotNumber=3 ClubLabel=Driver OriginPosition=(20.00, 7.71, -24.00) FinalPosition=(-111.28, 8.50, -24.00) DistanceXZMeters=131.28 TerminalState=OB OBReason=OutOfBounds FinalSurface=OOB PenaltyStrokes=1`. Same content as iter-1 (log is from the same OB shot, only the captures were redone).

---

## Step 2 — Reference comparison (spec § Smoke evidence)

§2e is mechanics, not UI — no Figma reference required (SPEC § Reference confirms). The behavioral reference is SPEC § Smoke evidence which prescribes load-bearing content per capture.

| Spec-required content | S1 | S2 | S3 |
|---|---|---|---|
| "Camera frame should show the pin visibly forward in view (ball→pin direction)." (S1) | Pin speck upper-center, shot cone forward → **PASS (weak — pin small)**. | n/a | n/a |
| "Ball should be visibly on grass, NOT in water." (S2) | n/a | **PASS** — green grass visible right & bottom-right of frame, no blue/water surface anywhere, ball on small rocky/ground patch at base of tree (not water). The Chase mode forced in smoke runner cleaned the framing so the surface is identifiable from pixels alone. | **PASS (strong)** — wide green fairway grass surrounds the ball; green/flag visible in middle distance; tree trunk visible at frame-left for spatial context. Unambiguous "ball on grass, not in water" reading from pixels alone. |
| "TURN label visibly equals 'TURN 3'." (S3) | n/a | TURN 3 visible (S2 also shows it, but S3 is the load-bearing capture per spec). | **PASS** — TURN 3 visibly readable in top-left card. |
| Three distinct captures (S1, S2, S3) | S1 present, distinct | S2 present, MD5 `acf0d53f…` | S3 present, MD5 `1ddeed38…` — bytes-distinct AND content-distinct (15° camera orbit visibly reframes the scene). **PASS (procedural fix complete).** |
| `controls_2e_history_log.txt` proves `PenaltyStrokes=1, TerminalState=OB` | n/a | n/a | Log shows `PenaltyStrokes=1 TerminalState=OB OBReason=OutOfBounds`. **PASS** (OBReason differs from spec example, but spec L8 reads on `BallStateChange.Previous == OB`, not OBReason — functionally equivalent; explicitly accepted in iter-1). |

---

## Step 3 — Acceptance checklist walk

| # | Spec line | Implementer claim | My verdict (iter-2) |
|---|---|---|---|
| A1 | `ShotRecord` has new `PenaltyStrokes` field + 9-arg ctor + preserved 8-arg ctor | PASS | **CONFIRM PASS** (unchanged from iter-1). |
| B1 | `OBDropResolver.cs` shipped with `Resolve(Trajectory, Vector3) → Vector3` API | PASS | **CONFIRM PASS** (unchanged). |
| C1 | `AimRotationHelper.cs` shipped with `ComputeYawTowardPin(Vector3, Vector3, float) → float` API | PASS | **CONFIRM PASS** (unchanged). |
| D1 | `HoleSessionDriver.BuildShotRecord` populates `PenaltyStrokes=1` on OB | PASS | **CONFIRM PASS** — log shows `PenaltyStrokes=1`. |
| D2 | `HandleStateChanged` advances TURN by 2 on OB→Aiming via `change.Previous == BallState.OB` | PASS | **CONFIRM PASS** — log shows `TURN=3` after OB (1 start + 1 shot + 1 penalty). |
| D3 | `ComputeNextTurn` shipped + tested | PASS | **CONFIRM PASS**. |
| D4 | `BuildShotRecordStatic` new 8-arg overload + 7-arg preserved | PASS | **CONFIRM PASS**. |
| E1 | `PlaceBallAt` refactored to delegate to `RepositionBallWithLookDir`; public signature unchanged | PASS | **CONFIRM PASS** — git diff confirms 1-line delegate. |
| F1 | `HandleShotComplete.AtRest`: yaw to face pin before `ApplyCameraYaw` | PASS | **CONFIRM PASS** — S1 shows pin upper-center forward of ball; logic matches spec § F verbatim. |
| F2 | `HandleShotComplete.OB`: `OBDropResolver` + `RepositionBallWithLookDir` + `_ballSM.ReArm()` | PASS | **CONFIRM PASS (visual evidence resolved)** — log line `[PhysicsLab][§2e] OB drop: from end=... to drop=(-95.46, 9.77, -24.00)` proves the path executed; S2/S3 show ball on visible grass-adjacent terrain with green fairway in frame, Chase mode active. |
| F3 | `HandleShotComplete.InCup` unchanged | PASS | **CONFIRM PASS** — git diff shows empty `break;`. |
| T1 | 9 new tests, all PASS, gate N+9 PASS, 0 IGNORED | PASS | **CONFIRM PASS** — 273 pass / 0 fail / 0 skipped (baseline 264 + 9 new). Tests verifiable by inspection. |
| S1 | 3 captures + 1 history-log artifact filed | PASS | **CONFIRM PASS (procedural fix verified)** — S2 (`acf0d53f…`) and S3 (`1ddeed38…`) are bytes-distinct AND content-distinct frames. 3 distinct PNGs + 1 .txt confirmed on disk. |
| Sc1 | No scene mutations | PASS | **CONFIRM PASS** — see Step 7. |

**No overrides this iteration.** All 14 items PASS.

---

## Step 4 — Root-cause analysis (for previously-failed defects, now resolved)

**Defect 1 — S2 visual readability (iter-1 OVERRIDE-FAIL):** Resolved. Root cause was OBFreeze camera mode showing the OB cinematic framing rather than the post-drop chase view; the entire frame was filled by an immediately-adjacent tree trunk at near-zero distance, with no surrounding context. The implementer's smoke-runner-only fix (force `ChaseCamera.Mode.Chase` post-OB→Aiming, so `ApplyCameraYaw` owns the framing) reframed the camera to a standard chase pose. The tree is still in the frame (the drop point really is next to a tree on Hole_06), but now green grass surrounds it on the right and bottom-right of the frame, making the surface identifiable from pixels alone. This is a **smoke-runner-only fix**, not a Director code change (see Step 5 below) — confirms spec L7 ("§2e does not touch the Director").

**Defect 2 — S3 byte-identity to S2 (iter-1 OVERRIDE-FAIL):** Resolved. Root cause was two adjacent `SnapPlayModeSafe` calls in the same coroutine frame with no state change between them. The implementer added a 15° camera orbit (rotating the cam transform around the ball's Y axis by 15°) plus a `WaitForSeconds(0.3f)` yield between the two captures. The new S3 differs in both bytes (`1ddeed38…` vs `acf0d53f…`) AND content (tree shifted to frame-left, green fairway and distant pin/green now dominate the central area). The implementer's choice to orbit rather than rely on timestamp metadata is correct — it produces an independently-readable frame instead of a "spot the metadata difference" duplicate.

**Defect 3 — Case 3 narrative was deferred to "unit test only" (iter-1 procedural):** Implementer's iter-2 IMPLEMENTER_REPORT § Visual Verification now contains a principled Case 3 disclosure (lines 87–97) covering: the scenario tried (engineering a trajectory with zero qualifying terrainHits before water), the actual `ShotExit` log evidence (`hits=2` on the real shot — so zero-hit case wasn't reproducible without out-of-scope preset changes), the resolver fallback path tested by `OBDropResolver_FallsBackToOriginWhenNoSafeHit` (PASS), and a log-anchored comparison showing what Case 3 would produce in live play (`drop=(20.00, 7.71, -24.00)` = `_lastShotOrigin`) versus what Case 2 actually produced (`drop=(-95.46, 9.77, -24.00)` ≠ origin → resolver found a qualifying hit). This is principled disclosure, not hand-waving: the implementer named the specific log fields, named the specific code branch, and named the unit test that covers the branch. Per spec § Definition of Done bullet "content-sanity descriptions cover all three cases," this satisfies the spirit — Case 3 has a content-sanity description that is grounded in measurable evidence rather than a live capture. **Accept**.

---

## Step 5 — Capture-helper compliance + smoke-runner-only fix scoping

- **Screenshot provenance:** `CaptureCore.SnapPlayModeSafe(...)` used for all three PNGs (SmokeRunner2eHost.cs lines 176, 298, 329). Sanctioned editor path. **PASS**.
- **Capture-helper maintenance protocol:** §2e adds no new `*Context.cs`. `HoleContext.PinWorld` already exists and is read-only consumed. `CaptureHelper.FakeMidAim`/`FakeReset` not affected. **N/A**.
- **`WaitForSeconds` audit (controls_g lesson):** Used for startup (`WaitForSeconds(5)`) and post-state HUD settle (`WaitForSeconds(1.5f)`, `WaitForSeconds(0.3f)`). Neither is used as a state gate — state transitions are awaited via `while ((!shotComplete || !aimingAfterOB) && elapsed < ShotWait)` event polling loop (lines 266–270). The 0.3s wait after the 15° orbit is a render-settle, not a state gate. **PASS**.
- **Smoke-runner-only Chase-mode override (iter-2 fix) scope verification:**
  - The `camChase.SetMode(ChaseCamera.Mode.Chase)` call is in `SmokeRunner2eHost.RunOBSequence` line 290 only.
  - `git diff` on `PhysicsLabController.cs` shows ZERO new `SetMode` calls in production code. The only `SetMode` calls in the production controller (lines 562, 604) are pre-existing GroundLevel sets in unrelated putt-mode code paths.
  - `LoopCameraDirector.cs` is NOT modified (no entry in `git status`). Director's `ModeMap` Aiming→null mapping that caused the iter-1 OBFreeze framing remains as-is — out-of-scope per L7, surfaced as a known Director-level issue for a future Loop v2 ticket (mentioned obliquely in IMPLEMENTER_REPORT § Visual Verification S2).
  - **Conclusion: smoke-runner-only override does NOT bleed into production paths**. PhysicsLabController's OB branch still relies on `ApplyCameraYaw` (which it has always done); the smoke runner additionally forces Chase mode to ensure ApplyCameraYaw's position actually wins (otherwise OBFreeze geometry would re-override it in `ChaseCamera.LateUpdate`). This is correct test-harness behavior — the override exists ONLY to make the smoke evidence readable. Production gameplay through `LoopCameraDirector` retains the documented OBFreeze→null→leave-unchanged behavior until that Director bug is filed and fixed separately.
- **`SetCameraYawRadians` test seam:** new `internal` method added to PhysicsLabController (lines 651–660). XML doc explicitly says "Only call from smoke runners / Editor test tools — never from production code." Marked `internal` (not public) so production assemblies cannot call it. **PASS** — clean test seam.

---

## Step 6 — Bbox geometry verification

§2e adds no UI containment claims (no text-in-card, no modal-in-canvas, no child-in-parent). Step 6 is **N/A**.

---

## Step 7 — Scene-mutation audit

```
$ git status --short
 M Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs
 M Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs
 M Assets/Scripts/Physics/Viewer/PhysicsLabController.cs
 M Docs/Specs/Active/loop_v1_2e_next_shot_handoff/STATUS.md
 M Packages/manifest.json
 M Packages/packages-lock.json
?? Assets/Scripts/Physics/Tests/NextShotHandoffTests.cs(.meta)
?? Assets/Scripts/Physics/Viewer/AimRotationHelper.cs(.meta)
?? Assets/Scripts/Physics/Viewer/Editor/SmokeRunner2eMenu.cs(.meta)
?? Assets/Scripts/Physics/Viewer/OBDropResolver.cs(.meta)
?? Assets/Scripts/Physics/Viewer/SmokeRunner2eHost.cs(.meta)
?? Docs/Diagnostics/_capture/... (capture artifacts — expected)
?? Docs/Specs/Active/loop_v1_2e_next_shot_handoff/{HEARTBEAT.log, IMPLEMENTER_REPORT.md, SELF_REVIEW.md, screenshots/}
```

- **No `.unity` scene files modified.** `Assets/Scenes/Physics/LabScaffold.unity` does NOT appear in `git status`. SPEC § Hard rule #3 ("Do NOT modify `LabScaffold.unity`") upheld.
- **No `m_IsActive: 0` mutations** — no scene YAML touched.
- **No `sizeDelta` / position changes to GameObjects** — confirmed.
- `Packages/manifest.json` and `packages-lock.json` are session-start-state (recorded in iter-1 review, not introduced by this task). **N/A**.

**PASS**.

---

## Step 8 — Production-flow capture verification

§2e is mechanics (camera yaw, ball teleport, TURN counter advancement), not a layout-affecting UI change. SPEC § Definition of Done relies on Cesar's Lesson-O human gate for live-play verification; smoke-runner captures serve as dispatch evidence + content-sanity reading.

The smoke runner's iter-2 Chase-mode forcing is **smoke-runner-only** (Step 5 above), which means the smoke captures show framing that differs from what gameplay actually shows post-OB (where Director leaves camera in OBFreeze). This is a real concern for Lesson O — Cesar needs to confirm live play matches the *behavior* shown in captures even if the *framing* differs (because in live play the OBFreeze mode would persist).

That said: the **behavior** (ball-on-grass at drop position, TURN=3, PenaltyStrokes=1) is independent of camera mode. The captures verify behavior. The framing concern is captured implicitly by the implementer's narrative ("Director leaves camera in OBFreeze... we force Chase mode here for framing"). This is honest disclosure — the framing fix is a capture-only workaround that Cesar should be told about explicitly during architect review.

**Recommendation for architect:** flag to Cesar that the OBFreeze→Chase camera mode transition is a known Director-side gap (referenced in IMPLEMENTER_REPORT § Visual Verification S2 and § Spec deviations #2). §2e's `_cameraYaw` mutation is correct; the Director's `ModeMap[Aiming]=null` for the post-OB Aiming state is the root cause of the OBFreeze-stuck-camera in live play. A small follow-up ticket may want to set `ModeMap[Aiming]=Chase` (or `ModeMap[Aiming] = previousModeWasOBFreeze ? Chase : null`) — but that is out-of-scope for §2e per L7.

**PASS** (with surfaced concern for architect/Cesar).

---

## Verdict — `FORWARD_TO_ARCHITECT`

Iter-1's two procedural blockers are resolved:

1. **S2 visual evidence (iter-1 OVERRIDE-FAIL → iter-2 PASS):** The new S2 shows the ball at the base of a tree on Hole_06 with **green fairway grass visibly surrounding the trunk on the right and bottom-right of the frame**. The "uniform dark brown" framing of iter-1 was OBFreeze cinematic mode showing only the trunk; iter-2's smoke-runner-only Chase-mode override produces a standard chase-pose framing where the surface around the ball is identifiable from pixels alone. Reader-side narrative is no longer required to confirm "not in water" — the green grass at the frame edges does that.

2. **S3 distinct from S2 (iter-1 OVERRIDE-FAIL → iter-2 PASS):** The new S3 differs in both bytes (`1ddeed38…` vs `acf0d53f…`) AND content (15° camera orbit shifts the tree from center to left edge, revealing the green fairway, the HOLE 6 putting green, and the distant flagstick that S2 mostly hid behind the trunk). S3 now stands as **independent** evidence of "ball on grass, TURN=3" without leaning on S2.

3. **Case 3 principled disclosure (iter-1 fail-list item #3 → iter-2 PASS):** Implementer's iter-2 narrative names the scenario tried (zero-qualifying-hit trajectory), the actual log evidence (`hits=2`, not zero), the resolver branch the unit test covers, and a log-anchored counter-factual for what Case 3 would produce in live play. This is principled, not hand-wavy.

4. **Smoke-runner-only Chase-mode override is clean:** the `SetMode(Chase)` call lives only in `SmokeRunner2eHost.RunOBSequence`. PhysicsLabController.cs git diff shows ZERO new `SetMode` calls. LoopCameraDirector.cs is not modified. Production gameplay paths are unaffected by the smoke-runner fix. The implementer correctly identified the Director's `ModeMap[Aiming]=null` as out-of-scope per spec L7 and worked around it in the smoke harness only.

5. **Scene file integrity:** `LabScaffold.unity` clean — not in `git status`. No scene YAML mutation.

6. **All 14 acceptance checklist items PASS.** Code matches spec verbatim. Tests pass (273/0/0). Log artifact + 3 distinct captures filed.

### Items for architect attention (surface to Cesar)

- The smoke runner forces Chase mode post-OB→Aiming as a framing workaround. In live play (without the smoke override), the Director leaves the camera in OBFreeze after OB→Aiming because `ModeMap[Aiming]=null` (leave unchanged). This is a Director-side issue out-of-scope per L7 — §2e's `_cameraYaw` mutation is correct, but the Director's mode-map does not promote post-OB Aiming back to Chase. Cesar should decide during architect review whether to (a) accept this as-is and file a follow-up Director ticket, or (b) request a tiny fix to `ModeMap` before §2e closes. The smoke evidence proves behavior is correct; the framing concern is purely about live-play camera experience post-OB drop.

- `SetCameraYawRadians` is an `internal` test seam on PhysicsLabController. It is correctly scoped (not public, XML doc warns "never from production code"). Architect should confirm the seam is acceptable or request it be moved to a `[Conditional]` or `#if UNITY_EDITOR` gate.

### Iteration check

This is iter-2. Verdict is FORWARD (not FAIL). The "if N ≥ 3 and would FAIL → ESCALATE" rule does not apply — iter-2 PASSes on every item.

---

## File summary

| File | Action |
|---|---|
| `Docs/Specs/Active/loop_v1_2e_next_shot_handoff/SELF_REVIEW.md` | overwritten (iter-2 review) |
| `Docs/Specs/Active/loop_v1_2e_next_shot_handoff/STATUS.md` | updated `READY_FOR_SELF_REVIEW` → `SELF_REVIEW_PASS` |
