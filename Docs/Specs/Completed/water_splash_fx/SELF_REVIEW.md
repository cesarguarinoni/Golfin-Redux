# Self-Review — `water_splash_fx` (Order 349)

**Reviewer:** golfin-self-reviewer
**Iteration:** N=1 (no prior `SELF_REVIEW.md`, no `CESAR_REJECTION.md`)
**Timestamp:** 2026-06-12 13:55 CEST
**Verdict:** **BACK_TO_IMPLEMENTER** → set STATUS to `SELF_REVIEW_FAIL`

---

## Visual diff notes (Step 1 — pixel scan first, no spec/report context)

I opened `screenshots/splash_burst_canonical.png` (1170×2532, Rule 14 satisfied) cold and described only what I saw:

- Centre of frame: a white/Golfin-G golf ball sitting on a small grey TEE PEG on a flat tee box of grass, viewed from behind. Trees and fairway recede into the distance.
- Surrounding the ball: a faint translucent circular halo on the grass (gray-tinted ring, roughly ball-shadow diameter).
- Around the ring: a handful of barely-visible pale-blue/cyan specks scattered on the grass.
- Beneath the ball: a soft dark-gray vertical cone fading downward into the grass.
- Two static dark-green spheres flanking the ball at mid-distance (left/right) — appear to be debug markers.
- Standard HUD: portrait + JAMES/Lv 10/TURN 1 (left), LOMOND/HOLE 1 - REGULAR/PAR 5 (right), 0.0 mph pill, 250 yds pill, SPIN/STRAIGHT/GOLFIN/DRIVER buttons.
- **No water visible anywhere in the frame.** No splash plume rising upward, no expanding ripple ring, no water-surface ring, no body of water. Just grass.

I then compared the four screenshots: `armed_no_splash.png` and `oob_control_no_splash.png` are **bit-identical** (same MD5 `18f68d15…`). The "before" arming frame and the "OOB control" frame are literally the same PNG. The `splash_B_burst.png` shows a slightly more scattered set of blue dots than the canonical, confirming particles ARE emitted, but they are extremely sparse and dim against grass — they read as small flecks, not a "splash."

I then sampled `videos/water_splash_gate_captioned.mp4` at 2 fps. Notable frames:
- Opening title card: **"Loop v2 — Stage F: Button Press Feedback"** (a leftover/mis-labelled title from a different feature).
- Mid-video: a GOLFIN "The Invitational" PLAY/CREATE ACCOUNT/LOGIN screen appears (login flow, unrelated to splash).
- Captioned "Splash burst A — 47 particles (spray + ripple)" frame: a flipped-upside-down view of the tee box (camera orientation issue?) — a body of water IS visible at the top of the frame (which is "below" the tee in real orientation), but the splash effect is firing at the BALL ON THE TEE, not at the water surface visible in the same frame.
- "Splash burst B / Same pool, new world pos" frame: ball on tee, the same translucent gray cone underneath the ball, faint particles, no water in frame.
- "PART C: OOB control — No Water reason — no splash" frame: visually almost identical to burst frames (same translucent cone still visible — that cone must be a non-splash element, e.g. an aim/spawn-point indicator, since it's present even in the "no splash" control).

What the video does NOT show: a real golf ball, launched from a real shot, in flight, terminating ON a water surface, with a splash plume + ripple at the entry point. That end-to-end production path is never captured.

---

## Visual comparison vs reference

No Figma reference exists for this task (TELLCODE VFX, no UI). The only visual reference is the SPEC's prose description of the expected effect: "vertical spray burst (20–35 particles, white-blue, gravity on, 0.6–0.9s), ring/ripple — flat quad or particle with horizontal expansion + fade (~1.2s)."

The captured visual is **not consistent** with that description: I see a sparse handful of barely-visible specks on grass and a translucent ring overlay; I do NOT see a vertical spray burst rising from a water surface, nor an expanding horizontal ripple. The visual reads as a particle effect firing in mid-air over grass, which is exactly what the implementer admits is happening in § Spec deviations: *"Bot fires at TeeMarker position, not a real water hazard."*

---

## Spec checklist walk

| Spec § 5 acceptance | Implementer mark | My override | Reason |
|---|---|---|---|
| Flight ball into water → splash burst + ripple at entry point at moment visual ball arrives | PASS* | **OVERRIDE-FAIL** | The captured video never shows a real ball entering water. The bot calls `FireWaterSplashForTest(splashWorldPos)` at the TEE (`(219.50, 11.58, 33.24)`), not at a water-hazard entry point. SPEC §5 explicitly demands "Flight ball into water" — the entire end-to-end production trigger path (`BallSimulation` → `SurfaceType.Water` termination → `BallStateMachine` → `OnStateChanged` with `OBReason.Water`) is NEVER exercised on video. Only an editor seam call is exercised, and at the wrong location. |
| Rolled-in ball → smaller plop (or single-tier with NOTE) | PASS* | CONFIRM-PASS (with caveat) | Spec's §3 fallback clause permits single-tier shipment when terminal velocity isn't accessible. Implementer's reasoning is correct on the API surface. NOTE: a follow-up to expose terminal incoming velocity through `BallStateChange` is implied. |
| Bot (1v1) water ball → same splash, no extra wiring | PASS | **OVERRIDE-FAIL** | Not actually demonstrated. The bot does not fire a 1v1 water shot; it injects via editor seam. The claim "any shot path through that state machine fires the same handler" is plausible by reading the code, but the acceptance item asks for evidence, not inference. |
| Prefab slot empty → no exceptions, no behavior change | PASS | CONFIRM-PASS | Null guard in `WaterSplashController.PlaySplash()` lines 80–89 is correct; `_nullPrefabLogged` prevents log spam. Test `WaterSplash_NullPrefab_DoesNotThrow` exists in `WaterSplashControllerTests.cs` (line list confirms three tests). |
| Zero gameplay impact: no sim/state-machine/drop files touched | PASS | CONFIRM-PASS | `git diff HEAD -- Assets/Scripts/Physics/Core/BallSimulation.cs Assets/Scripts/Gameplay/Loop/BallStateMachine.cs Assets/Scripts/Physics/Viewer/OBDropResolver.cs` returns empty. Verified. |
| EditMode test `WaterSplash_TriggersOnlyOnWaterOB` fires once on Water OB / never on OOB-or-normal | PASS | **OVERRIDE-FAIL (tautology)** | Read `WaterSplashControllerTests.cs`. The test fixture subclasses `WaterSplashController` as `TrackingWaterSplashController` and adds its OWN `SimulateStateChange` method (lines 38–52) that RE-IMPLEMENTS the trigger predicate (`change.Next == BallState.OB && change.OBReason.Value == OBReason.Water`). The test subscribes `sm.OnStateChanged += ctrl.SimulateStateChange` — bypassing the production `HandleStateChanged` entirely. The tests therefore verify the test's hand-copied predicate, not the controller code that runs in the game. A correct test would call `ctrl.FireWaterSplashForTest(pos)` (the editor seam at line 119) or arrange the real `Configure(...)` flow. As written, the tests would PASS even if `WaterSplashController.HandleStateChanged` had a bug. |
| Bot-recorded full-res video: flight splash + roll-in + OOB control (no splash) | PASS* | **OVERRIDE-FAIL** | Video is full-res (1170×2532, 14.3s) — that part is fine. But: (a) opening title card reads "Loop v2 — Stage F: Button Press Feedback" — wrong feature label; (b) a GOLFIN login screen appears mid-video; (c) "flight splash" is not a flight ball — it's an editor seam call at the tee on grass; (d) "roll-in" is admitted single-tier so there's no distinct roll-in visual; (e) "OOB control" is "don't call the seam and observe stillness" — not a real OOB shot, just nothing happening. The video does not deliver what SPEC §5 last bullet asks for. |

---

## Bbox verification

N/A — VFX/particle task; no containment claim ("text inside X", "modal inside Y") was made. Step 6 not applicable.

---

## Capture-helper compliance check (Step 5)

- **Screenshot provenance:** PASS. `BotDriver.Capture` uses `CaptureCore.SnapPlayModeSafe` (line 89 of `BotDriver.cs`), the sanctioned coroutine-safe path. No `ScreenCapture.CaptureScreenshot` invocation, no per-task workaround.
- **CaptureHelper FakeReset/FakeMidAim maintenance protocol:** N/A — no new `*Context.cs` under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. Task scope is VFX only.

---

## Scene-mutation audit (Step 7)

`git diff HEAD -- Assets/Scenes/Physics/LabScaffold.unity` reveals:

1. **Expected & in scope:** `WaterSplashController` MonoBehaviour added to BallAnimator (`fileID 1075126838`, GUID `b73c2e0c…`); `_splashPrefab` reference set; `_splashClip = {fileID:0}`; cross-ref from `PhysicsLabController._waterSplash`. Fine.
2. **OUT OF SCOPE — drift on `VersusBot`:** two serialized fields default-added to the scene:
   - `_greenReader: {fileID: 0}`
   - `DebugLevelOverride: -1`
   Both happen to match the script defaults, so behavior is unchanged, but their appearance shows the scene was re-saved against a `VersusBot` with new serialized fields. Inert here, but indicative.
3. **OUT OF SCOPE & POTENTIALLY HARMFUL — drift on `HoleCompletionBridge`:** `_strokeCapOverPar: 5` was **removed** from the scene. The script default also happens to be `5` (verified in `HoleCompletionBridge.cs:30`), so the runtime value is unchanged — but a configured serialized value was silently dropped. The next time anyone overrides this in Inspector, the drop pattern will recur and may mask intentional overrides.

Per visual-review checklist rule 4: ANY scene mutation outside the documented fix is a hard FAIL until reverted or explicitly justified. Even if (2) and (3) are behaviorally inert today, they have nothing to do with this task's scope and must not ship in this commit. **Revert the `VersusBot` field-additions and the `_strokeCapOverPar` removal** before resubmitting (or report them deliberately with justification — silent drift is the failure mode).

---

## Rule 13 — unreported uncommitted files outside task folder

`git status --porcelain --untracked-files=all` shows two unreported modifications:

```
 M Packages/manifest.json
 M Packages/packages-lock.json
```

Diff:
```
- "com.ivanmurzak.unity.mcp": "0.80.0",
+ "com.ivanmurzak.unity.mcp": "0.81.0",
```

The Unity MCP plugin self-updated 0.80.0 → 0.81.0. This is NOT listed in `IMPLEMENTER_REPORT.md` § "Files modified or created", in violation of Rule 13 (every uncommitted path outside `Docs/Specs/Active/<task>/` must be reported or restored before transitioning). The kickoff baseline in `HEARTBEAT.log` (iter-1) shows the working tree was clean of these paths at task start, so the drift entered during this session.

Decision: implementer must either (a) revert the manifest bump (preferred — unrelated to task), or (b) add it to the report with a one-line justification AND commit it as a separate "chore(packages): bump MCP 0.80→0.81" change after this task.

---

## Detailed pixel-level issues (with proposed root cause per Step 4)

**Visible defect 1:** Canonical screenshot shows the "splash" effect over a GRASS TEE BOX, with the ball stationary on a tee peg. No water surface is anywhere in the splash impact region.

Likely cause: `WaterSplashGate` scenario in `Scenarios.cs` (lines 2210–2253) calls `wsc.FireWaterSplashForTest(splashPos1)` with `splashPos1` set from `TeeMarker_regular_L` world position `(219.50, 11.58, 33.24)`. The bot author chose the tee location so the chase camera frames the effect, but this means the visual reads as "particles in mid-air over grass" rather than "splash erupting from a water surface." Per Cesar's standing rule (memory `feedback_gameplay_video_use_normal_play`): fix the SHOT (have the bot drive a real ball into a real water hazard with the normal chase camera) rather than fake the position.

**Visible defect 2:** Captured "splash" particles are extremely sparse and dim — barely visible against grass. The spec's §3 design calls for "20–35 particles, white-blue, gravity on, 0.6–0.9s" PLUS a separate ring/ripple child — the canonical shows roughly the right count but the spray reads as faint flecks, not a "burst." Hard to judge whether the effect is genuinely under-spec'd or whether grass + low-contrast camera angle is masking it; the only fair test is to re-capture against a real water surface.

Likely cause: combination of (a) wrong substrate (grass instead of water — kills the visual contrast), (b) possibly an under-tuned start size, opacity, or emission count, (c) downward-facing camera angle blunting any vertical spray. Re-capture over real water will resolve (a) and clarify whether (b)/(c) need follow-up tuning.

**Visible defect 3:** `armed_no_splash.png` and `oob_control_no_splash.png` are bit-identical (same MD5). The OOB control screenshot is a re-copy of the armed pre-splash frame, not a fresh capture from the bot's PART C step. Even if the bot generated a real `splash_C_oob_control_no_splash.png` in `tasks/loop_v2_smoke_bot/water_splash_gate/screenshots/`, the file copied into the task folder is wrong. Inspect that source folder and copy the correct PART C still.

**Visible defect 4:** `water_splash_gate_captioned.mp4` opens with a title card "Loop v2 — Stage F: Button Press Feedback" (a different feature), and includes a GOLFIN login screen mid-video. Looks like the build-bot-video pipeline included stale frames or the wrong scenario's title card. Either trim/re-render, or use a `watersplash`-specific title.

Likely cause: `Docs/Scripts/build_bot_video.py` modification added a `watersplash` parser mode (per IMPLEMENTER_REPORT line 25), but the rendered output still has a "Loop v2 — Stage F" title card. Either the parser mode isn't applied, or the wrong source video was passed in.

---

## Verdict & concrete fix list

**Verdict:** **BACK_TO_IMPLEMENTER** (STATUS → `SELF_REVIEW_FAIL`).

N=1, so escalation isn't called for. The defects are concrete and within the implementer's ability to address. Forwarding this iteration to the reviewer would waste reviewer + red-team + Cesar time on a synthetic-tee capture, scene drift, and tautological tests that any of those gates would (or should) catch.

### Required fixes before re-submit

1. **Capture the splash over real water on a production path.** Make the bot drive a real shot whose ball terminates with `SurfaceType.Water` → `OBReason.Water`, with the normal chase camera (per Cesar's memory `feedback_gameplay_video_use_normal_play`). Pick a hole + tee combo that actually has a water hazard within driver range (Hole 01 Lomond does have water — visible in one of the upside-down captioned frames). Fix the SHOT (low/flat trajectory at the hazard), don't fake the position. The new canonical screenshot must show splash particles emerging from a water surface, with a visible ripple, no grass under the impact point. Replace `screenshots/splash_burst_canonical.png` and re-render the video.

2. **Show three distinct cases in the video** (per SPEC §5 last bullet):
   - PART A: a real flight ball into water → splash at the entry point.
   - PART B: a rolled-in ball reaching water (single-tier shipped is fine per §3 fallback, but the case must be a real ball, not a seam call at the same tee).
   - PART C: a real OOB shot (e.g. trees / OB line) — splash must NOT fire. Demonstrate it on a real ball, not by "wait 2 seconds and don't call the seam."
   The current PART A/B/C are all editor-seam invocations at the tee on grass, which doesn't satisfy the acceptance item.

3. **Replace the tautological EditMode tests in `WaterSplashControllerTests.cs`.** The current tests subscribe `sm.OnStateChanged += ctrl.SimulateStateChange` and `SimulateStateChange` re-implements the trigger predicate — the production `HandleStateChanged` is never invoked. Either (a) wire the real `WaterSplashController.Configure(null, sm, null)` and assert a side-effect (e.g. `wsc.SplashInstanceExists`), or (b) call `wsc.FireWaterSplashForTest(pos)` directly and assert `SplashInstanceExists == true` post-call vs not-called. The test name promises "TriggersOnlyOnWaterOB"; the test must actually exercise the production trigger to deliver that promise.

4. **Revert the unrelated scene drift in `LabScaffold.unity`.** Specifically: remove the `_greenReader: {fileID: 0}` and `DebugLevelOverride: -1` lines added under the `VersusBot` MonoBehaviour block, and restore `_strokeCapOverPar: 5` on the `HoleCompletionBridge` MonoBehaviour. Re-save the scene with ONLY the `WaterSplashController` add + `_waterSplash` cross-ref present in the diff. If Unity refuses to round-trip without re-adding these fields, document the root cause in the report so we know whether it's a Unity quirk or an unrelated `VersusBot` script edit.

5. **Resolve the Packages drift.** Either (a) revert `Packages/manifest.json` + `Packages/packages-lock.json` to `0.80.0` (preferred — out of scope for this task), or (b) add both files to IMPLEMENTER_REPORT's "Files modified or created" table with a one-line justification ("MCP self-updated during session; bump kept because…"). Rule 13 requires one of those two.

6. **Re-capture the OOB control screenshot.** `screenshots/oob_control_no_splash.png` is currently bit-identical to `screenshots/armed_no_splash.png`. Find the PART C still in `tasks/loop_v2_smoke_bot/water_splash_gate/screenshots/` and copy the correct file in.

7. **Fix the captioned video pipeline.** Strip the "Loop v2 — Stage F: Button Press Feedback" title card and the GOLFIN login screen frames. Use a `water_splash_gate`-specific title card. The captioned MP4 should open on the water-splash content, not on stale frames from a previous scenario.

### Items that ARE correct (don't break them in the redo)

- Controller code structure (`WaterSplashController.cs`) mirrors `BallTrailController` pattern correctly: idempotent `Configure`, `OnStateChanged` subscribe/unsubscribe, `OnDestroy` cleanup, null-prefab guard with log-once, single pooled instance via `Clear()+Play()`.
- Trigger condition is exactly right (`change.Next == BallState.OB && change.OBReason.Value == OBReason.Water`).
- Zero diff to `BallSimulation.cs`, `BallStateMachine.cs`, `OBDropResolver.cs` — zero-gameplay-impact verified.
- The `AudioSource.PlayClipAtPoint` substitution (deviation 2 in report) is acceptable for Order 349 given the clip slot is empty; Order 350 will need to decide whether to route through `AudioManager.Instance.PlaySFXAtPosition` (and break the asmdef boundary, or move `AudioManager` into a named asmdef) for proper SFX-mixer routing. Worth flagging in Order 350's SPEC but not blocking here.
- `playOnAwake: 1` on the prefab handles the lazy-instantiate first-play case implicitly. Works, but a defensive `_splashInstance.Play()` after `Instantiate` would remove the silent dependency.

---

## Iteration note

This is N=1. Three concrete classes of issue (capture path is synthetic, scene drift, tautological tests) plus reportable scope drift (Packages bump, duplicated OOB screenshot). All within the implementer's scope to fix. No architectural ambiguity, no spec contradiction.
