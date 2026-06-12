# Self-Review — `tree_collisions` (iter-8 + iter-8c, post-CESAR_REJECTION)

**Verdict: FORWARD_TO_ARCHITECT**
**Iteration:** N=4 on the rejection cycle (iter-6 self-review PASS → red-team PASS → architect PASS → **Cesar REJECT**; iter-7 → IMPLEMENTER_BLOCKED → Architect adjudication; iter-8 test-tightening + iter-8b/c trunk-clip re-shoots; this review)
**Date:** 2026-06-12 10:18 CEST
**Reviewer:** golfin-self-reviewer

`CESAR_REJECTION.md` exists. I re-walked the entire acceptance checklist against the iter-8c captures — no carry-forward of prior PASS as decisive. The Architect's adjudication in `FINDINGS_iter7_canopy_test.md` § ARCHITECT_DECISION (committed in `325d76b4`) defines the contract for this iter: SIM IS FROZEN, only `TreeCollisionTests.cs` (assertion (b) tightening) + the §9 video may change. That contract is honored.

---

## Visual diff notes (Step 1 — pixels only, before consulting spec/report)

**Canonical screenshot: `screenshots/trunk_atrest_iter8c_run10.png` (1170×2532, 5.0 MB).**

The frame is a portrait composition. The center is dominated by a very large brown tree trunk filling the middle vertical strip. The trunk has visible bark texture and reads as bare wood from about mid-frame downward; the upper third shows green foliage/canopy with branches and leaves. A white Golfin-logo ball sits on a small grey tee marker on green grass directly at the base of this trunk — the ball appears to be on the ground in front of (slightly south of) the trunk's south face. A faint blue ground-aim line stretches from the ball toward the lower-right.

Top-left HUD: portrait of a character with green cap labeled "JAMES / Lv 10 / TURN 2" + a row showing "0.0 mph" and "178 yds". Top-right HUD: "LOMOND / HOLE 1 - REGULAR / PAR 5". Top-right corner: white settings cog. Bottom row: "SPIN" button (top-left of bottom cluster), "STRAIGHT" with up-arrow (top-right), "GOLFIN" + green logo (bottom-left), and a "DRIVER / 0 yds" club selector chip (bottom-right).

The ball is unambiguously at ground level — green grass continues under and around it; it is NOT floating mid-trunk nor lodged in foliage. The trunk behind it is BARE bark — no foliage between camera and trunk at ball height. Framing reads as a normal in-game chase camera; no Downrange/fixed-camera label visible. "TURN 2" + "0.0 mph" confirms this is post-shot at-rest, not a pre-shot pose.

**Video frame walk (`videos/tree_trunk_normal_play_iter8c_normalcam.mp4`, 11.6 MB, 16.3s, 1170×2532; extracted at 1 frame / 2s).**
- f_001 (t=2s): GOLFIN splash w/ caption "Tree Trunk Collision - iter-8c"
- f_004 (t=8s): pre-shot, ball east of trunk, TURN 1, normal chase camera framing — no Downrange tag
- f_005 (t=10s): aiming gauge "18%" overlay, ball at canopy edge
- f_006 (t=12s): mid-flight, ball passing through green canopy (chase cam follows into foliage as expected)
- f_007 (t=14s): TURN 2 settled, ball at trunk base with bare bark behind, normal chase cam centered
- f_008 (t=16s): TURN 2, ball stable at base of bare trunk, no overlay — final at-rest

Video frame sequence matches the canonical still exactly. ZERO fixed-camera evidence anywhere in the clip.

---

## Step 2 — Comparison vs prior rejection evidence

`CESAR_REJECTION.md` Defect 2: "the trunk-strike segment of `videos/tree_collision_gate_visual_gate.mp4` is so camera-buried in foliage that the ball-hits-trunk-and-drops-dead moment is not legible — it reads as canopy-only." The iter-8c canonical clip (16s, single trunk strike) shows trunk impact + at-rest against bare bark legibly across frames f_007 and f_008. Iter-8b used a Downrange override which Cesar then rejected ("use normal chase camera"). iter-8c uses ZERO camera code (verified in diff below) and still produces a legible at-rest bare-bark frame because the ball comes to rest with normal-chase-cam already centered on it.

Defect 1 (slow-mo) is design-level fixed (iter-6 D3 revision committed in 2fb4c2b7) and was already cleared in prior reviews; the iter-8 test tightening reinforces the no-regression guard.

---

## Step 3 — Spec checklist walk

| # | SPEC checklist item | Implementer | Reviewer verdict | Justification |
|---|---|---|---|---|
| 1 | Per-hole CSVs + profiles CSV | PASS | CONFIRM-PASS | Files unchanged vs `2fb4c2b7` checkpoint (`git diff 2fb4c2b7 -- Assets/Resources/HoleData/Hole_*/tree_obstacles.csv` empty). Profiles CSV identical. |
| 2 | Trunk reflect + determinism | PASS | CONFIRM-PASS (provisional on test re-run) | Sim code byte-identical to checkpoint (`git diff 2fb4c2b7 -- BallSimulation.cs` empty). iter-7 PROBE7 fix committed and per Architect adjudication is correct. Implementer cites all related tests PASS in iter-8 (376/379). |
| 3 | Canopy one-time impulse + no slow-mo | PASS | CONFIRM-PASS | Sim code byte-identical. Tightened assertion (b) verified in test diff — see Step 3b below. Probe trace in FINDINGS confirms model. Bot Part B (prior iter) showed 2.6s canopy descent vs 14s+ in v1. |
| 4 | Roll/putt trunk deflect | PASS | CONFIRM-PASS | Sim byte-identical to checkpoint. |
| 5 | Absent CSV → bit-exact phase-6 | PASS | CONFIRM-PASS | Sim byte-identical to checkpoint. |
| 6 | §8 no-slow-mo regression test (tightened) | PASS | CONFIRM-PASS | Test diff matches Architect directive exactly. See Step 3b. |
| 7 | §9 trunk video bare-bark legibility, NORMAL chase cam | PASS | **CONFIRM-PASS** | Canonical still + extracted video frames show ball at ground level against bare trunk bark, normal chase cam framing throughout. ZERO camera-state code in the new scenario (verified in diff). See Step 3c. |
| 8 | Save hook + staleness guard | PASS | CONFIRM-PASS | Code untouched since iter-5. |
| 9 | No change to VersusBot/HUD/RP/UI | PASS | CONFIRM-PASS | `git diff` scope is exactly: `TreeCollisionTests.cs` + `Scenarios.cs` + `LoopV2SmokeBotMenu.cs` (one menu entry) + `LoopV2SmokeBot.cs` (one case branch) + task docs. Nothing else. |
| 10 | Perf overhead measured | PASS* | CONFIRM-PASS* | Unchanged from iter-6 measurement; not retested this iter, but no sim-code path changed. |

### Step 3b — Test diff against Architect directive

`git diff 2fb4c2b7 -- Assets/Scripts/Physics/Tests/TreeCollisionTests.cs` shows (assertion (b) only):

- Scan truncated at first sample with `y < 0.2f` (`groundFloor = 0.2f`) — Architect directive satisfied
- Single drop asserted to lie in canopy band: `Assert.Greater(dampY, trunkTopY)` (3.0m) AND `Assert.LessOrEqual(dampY, canopyTopY)` (9.0m) — Architect directive satisfied
- Ratio asserted ≈ `canopyHitDamping` (0.40) ± `dampTol` (0.15) — i.e. ratio ∈ (0.25, 0.55) — Architect directive satisfied
- Assertion (a) descent-time check UNCHANGED
- Sim code UNTOUCHED
- Long code comment cites the Architect adjudication + the iter-7 stuck-ball-false-pass story

This is a clean, targeted test fix exactly matching the iter-8 directive.

### Step 3c — §9 trunk-clip independent verification

I extracted 8 frames at 1/2s from the canonical iter-8c video and read them in this session:
1. ZERO Downrange/fixed-camera indicator at any frame
2. Pre-shot, mid-flight, and at-rest frames all show normal HUD framing (player portrait top-left, settings cog top-right, ball-centric chase cam)
3. The at-rest frames (f_007, f_008) AND the canonical still all independently show: ball on green grass, bare brown trunk dominant in mid-frame, ground-level contact, "TURN 2" indicator confirming post-shot rest

**My honest call on the at-rest moment:** YES, the iter-8c canonical at-rest frame genuinely shows bare-bark ground-level contact. The ball is on visible green grass with the bare lower trunk filling the right-center of the frame at the same vertical band as the ball. This satisfies Cesar's directive — "just play normally and hit a tree trunk — physics is deterministic" — better than the iter-6 visual_gate and iter-8b Downrange clips both did.

Caveats I am surfacing for the architect-reviewer:
- The implementer's "PARTIAL" verdict on the bot LogStep (`y=6.84 > 1.5f`) is a SCENARIO-INTERNAL pass/fail message, not a sim-correctness signal. The 1.5m floor was calibrated for flat lab terrain; tree idx=247 sits on the Hole 1 fairway HILLSIDE at y=6.84m terrain height. The roll-step log `surface=Fairway` + `hits=3` + ball XZ near trunk (8m east) is consistent with ball rolling back from trunk and resting on the hillside. This is NOT a foliage lodge — the canonical still corroborates.
- Mid-flight frame f_006 shows the ball briefly inside the canopy with chase cam tracking through foliage. This is exactly what Cesar asked for ("normal chase cam") — by design it can look "buried" mid-flight, but the at-rest moment is what matters and that frame is clean.

---

## Step 4 — Root cause / no-defects-to-explain

No OVERRIDE-FAIL items. Both Cesar-rejected defects are resolved per the standing evidence.

---

## Step 5 — Capture-helper compliance

1. **Screenshot provenance.** The canonical still `trunk_atrest_iter8c_run10.png` was produced via `BotDriver.Capture("trunk_normal_atrest")` inside the bot scenario, which routes through `CaptureCore.SnapPlayModeSafe` (the sanctioned playmode-safe capture path) — this is the same path used by `tree_collision_gate` previously and consistent with the iter-5 bot-recording infrastructure. NOT `ScreenCapture.CaptureScreenshot`, NOT a manual OS screenshot. Compliant.
2. **Maintenance protocol for new contexts.** Diff inspection shows ZERO new `*Context.cs` files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` (or anywhere) in this iter. CaptureHelper extension N/A this iter. Compliant.

---

## Step 6 — Bbox geometry verification

N/A this iter — no containment claim of the form "X inside Y" in the report. The "ball at trunk base" claim is verified pixel-wise (ball + grass + bare bark visible in canonical still + extracted video frames) AND via sim log: `[ShotExit] termination=BallStopped finalPos=(-140.95, 6.84, -54.58) hits=3 surface=Fairway`. Ball XZ = (-140.95, -54.58); trunk XZ = (-132.879, -53.239); xzDist ≈ 8.07m east of trunk — sim-consistent with bounce-back-and-roll on the east-facing fairway slope.

---

## Step 7 — Scene-mutation audit

`git diff 2fb4c2b7 -- Assets/Scenes/Physics/PhysicsLab_Hole1.unity` returns **empty**. Scene byte-identical to the verified iter-7 checkpoint. No `m_IsActive` flips, no `sizeDelta`/position mutations, no GameObject deactivations from any capture path.

The iter-5 try/finally canvas-restore pattern is replicated in the new `TreeTrunkNormalPlay` scenario (verified in diff: line `private static IEnumerator TreeTrunkNormalPlayBody(BotDriver d, System.Action restoreCanvases) { try { … } finally { restoreCanvases(); d.FlushLog(); } }`). ShellScene canvases hidden during recording are unconditionally re-enabled regardless of yield-break / exception.

---

## Step 8 — Production-flow capture check

N/A in the strict UI-layout sense. The deliverable here is a physics-simulation video, not a UI panel layout. The bot scenario IS a production-equivalent flow: it loads `LabScaffold + Hole_01_Geo` additively, waits for `PhysicsLabController.IsHoleReady` (the same gate as normal play), places the ball, fires via `ShotController.BeginExternalDrag/SetExternalPower/EndExternalDrag` (the same input path the player uses), and consumes `BallStateMachine.OnShotComplete` to terminate. Chase camera = unmodified normal chase camera. This satisfies the spirit of Step 8 — no smoke-runner state injection that would bypass production lifecycle.

---

## Files-table reconciliation (Rule 13 backstop)

`git status --porcelain --untracked-files=all` shows:

| Path | In implementer Files-table? | Verdict |
|---|---|---|
| `M Assets/Scripts/Physics/Tests/TreeCollisionTests.cs` | YES | OK |
| `M Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | YES (marked "UNCHANGED iter-8c") — but actually MODIFIED this iter (1 new menu entry) | **MINOR-DISCREPANCY** — Files-table prose says "UNCHANGED iter-8c" but the diff shows +17 lines (new `RunTreeTrunkNormalPlay` menu item). Non-blocking; the change is purely additive wiring for the new scenario and matches the SPEC `tree_trunk_normal_play` deliverable. Flagging for architect awareness. |
| `M Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` | YES (same caveat) | **MINOR-DISCREPANCY** — Files-table prose says "UNCHANGED iter-8c" but +4 lines (case branch). Same justification. |
| `M Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | YES | OK |
| `M Docs/Specs/Active/tree_collisions/*` (task docs) | YES | OK |
| `?? screenshots/s02_trunk_before_iter8b.png` etc. (iter-8b intermediate stills) | YES | OK in-folder; non-canonical |
| `?? screenshots/trunk_atrest_iter8c_run10.png` | YES (canonical) | OK |
| `?? Docs/Videos/tree_collision_gate_stageF_buttons.mp4` | YES (explicitly noted as iter-8b intermediate, superseded by iter-8c canonical) | OK Rule 13 satisfied (reported, not orphaned) |

The two MINOR-DISCREPANCY rows are bookkeeping nits in the Files-table prose, NOT scope drift — both modifications are wiring for the iter-8c-declared new scenario and are in-spec. Not a FAIL.

---

## Live test re-run (gap I am surfacing)

I do NOT have Unity MCP `tests-run` in this subagent context — my tools are Read/Write/Edit/Glob/Grep/Bash (read-only)/Figma MCP only per the agent definition. The implementer cites:

```
TreeCollisionTests: 9/9 PASSED
  TreeCollision_CanopyEntryImpulse_NoSlowMoDescent    PASS  (TIGHTENED iter-8)
  TreeCollision_AirborneTrunkDescending_BallReachesGround  PASS  (iter-7)
  ...
Full EditMode suite: total=379, passed=376, failed=0, skipped=3
```

I am relying on (a) the test diff being a clean, targeted tightening exactly matching the Architect's directive, (b) the FINDINGS_iter7 trace which empirically supports the new heuristic's correctness, (c) the implementer's cited count. **The architect-reviewer / red-team MUST re-run `tests-run` independently** — that's the deciding objective signal and I cannot supply it.

If the live re-run does NOT reproduce 376/379 + the tightened test green, that's a hard FAIL for me too — but my read of the diff and trace is that it WILL reproduce.

---

## Verdict: FORWARD_TO_ARCHITECT

Setting STATUS to `SELF_REVIEW_PASS`.

**Rationale:**
- Sim is frozen and verifiable as such (byte-identical diff against verified iter-7 checkpoint `2fb4c2b7`).
- Test fix matches the Architect's directive exactly (truncate-at-y<0.2 + canopy-band + ratio-tolerance).
- iter-8c trunk clip + at-rest still satisfy Cesar's "normal chase camera, bare trunk, ball at ground" directive. Independent frame-by-frame walk corroborates the report.
- Scene/scope/maintenance gates all clean.

**Items the architect-reviewer should double-check live (not blockers — gaps in my tool surface):**
1. `tests-run EditMode TreeCollisionTests` → expect 9/9 PASS (the iter-7 PROBE7 + iter-8 tightened canopy test).
2. Full EditMode suite → expect 376/379 PASS (3 pre-existing Stage C1 skips).
3. Mesh metrics gate (Rule 16) → N/A this is not a mesh task; the deliverables are sim correctness + a single clip.

The iteration count is N=4 on the rejection cycle. Per the "N ≥ 3 + FAIL → ESCALATE" rule: my verdict is PASS so the rule doesn't fire. If the architect-reviewer's live tests-run disagrees, ESCALATE is the appropriate next move (sim has been frozen across two reviewer cycles now).

---

## Files reviewed this session

| Path | Purpose |
|---|---|
| `Docs/Specs/Active/tree_collisions/STATUS.md` | Confirm READY_FOR_SELF_REVIEW |
| `Docs/Specs/Active/tree_collisions/IMPLEMENTER_REPORT.md` | iter-8c report |
| `Docs/Specs/Active/tree_collisions/CESAR_REJECTION.md` | Defects 1+2 from 2026-06-11 |
| `Docs/Specs/Active/tree_collisions/FINDINGS_iter7_canopy_test.md` | ARCHITECT_DECISION adjudication |
| `Docs/Specs/Active/tree_collisions/SPEC.md` | §3, §4, §8, §9 contracts |
| `Docs/Specs/Active/tree_collisions/HEARTBEAT.log` | iter-8c kickoff baseline |
| `Docs/Specs/Active/tree_collisions/screenshots/trunk_atrest_iter8c_run10.png` | Canonical at-rest still |
| `Docs/Specs/Active/tree_collisions/videos/tree_trunk_normal_play_iter8c_normalcam.mp4` | Canonical iter-8c video (frame-extracted to /tmp/iter8c_frames/) |
| `git diff 2fb4c2b7 -- (sim files)` | Empty — sim frozen |
| `git diff 2fb4c2b7 -- TreeCollisionTests.cs` | Test diff vs Architect directive |
| `git diff HEAD -- Scenarios.cs LoopV2SmokeBot*.cs` | New scenario + wiring |
