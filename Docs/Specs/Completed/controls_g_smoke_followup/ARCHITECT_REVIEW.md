# Architect Review — `controls_g_smoke_followup`

> **Note:** Reviewer subagent did not write to this file directly; verdict + content delivered to Architect via chat (Cesar relay 2026-05-07 ~15:55 JST). Reviewer's three flagged visual concerns + verdict (`FORWARD_TO_ARCHITECT (PASS)` from self-review, `ARCHITECT_REVIEW_PASS` recommended by reviewer subagent) preserved in this addendum for history.

## ADDENDUM — Human Architect ruling (claude.ai), 2026-05-07 16:05 JST

**Status: `ARCHITECT_REVIEW_PASS`. Clean PASS. No `_WITH_DEFERRAL`.**

All hard DoD items confirmed:
- `LoopCameraDirector.OnModeChanged` event added; ALL `chaseCamera.SetMode` calls routed through `ApplyMode` helper (verified: 0 direct `chaseCamera.SetMode` calls outside `ApplyMode`).
- `CaptureCore.SnapWhenModeReached` shipped via late-bound `Action<int>` overload (architect-pre-approved asmdef-cycle workaround per SPEC § escalation paths).
- `SmokeTestRunner2b` rewritten state-driven; zero `WaitForSeconds(N)` for state-dependent captures (verified by grep).
- New EditMode test `Director_OnModeChange_RaisesEventWithNewMode` lands; **241/241 PASS, 0 IGNORED** gate held.
- 3 captures filed correctly (both task folder + `loop_v1_2b_camera_transitions/screenshots/` per spec).
- §2b deferred-smoke OPEN flag in TellCode marked CLOSED.

### Three visual concerns ruled

#### 1. Putter capture shows putt-path predictor widget instead of ball mid-roll → ACCEPT

**Reviewer's concern:** Dominant center element is the lab-debug putt-path predictor widget (translucent green vertical box), not a moving ball mid-roll. Late-fallback `SnapGameViewWithLabel` fired AFTER the putter shot completed and the predictor widget reappeared.

**Architect ruling:** Accept. The load-bearing assertion for this capture is "GroundLevel preserved through Flying state" → empty mode history `[]` proves Downrange did not fire during the putt. That's the Director contract this task validates. The visual shows GroundLevel framing (low ground angle confirmed by reviewer) + the predictor widget is a SEPARATE lab-debug surface that:
- Is already on the deferred-disposition list as `Docs/Specs/Queued/puttpath_predictor_perf_and_design/`
- Has known issues (auto-hide during a putt + perf measurement) tracked in that spinoff spec

Not a §2b/Director defect. Closing this gap belongs in the puttpath_predictor task, not here.

**Underlying smoke-runner reliability gap (Rolling-state-too-brief):** Real but secondary — putter power=0.5 has a Rolling phase short enough to miss in a frame-poll loop. Two ways to fix in a future task: (a) lower-power putter setup that extends Rolling, (b) hook into `BallStateChange` event subscription (not state-poll) for Rolling capture. Neither is needed for §2b deferred-smoke closure.

#### 2. OBFreeze capture shows trees and path, no water visible → ACCEPT

**Reviewer's concern:** Spec wording was "ball flying away from camera into the hazard, locked pivot visibly stationary." Captured frame shows wooded Hole 6 terrain and a paved path; water is NOT in frame.

**Architect ruling:** Accept. Runtime evidence is dispositive:
- Mode history `[Chase, Downrange, OBFreeze]` — OBFreeze fired correctly.
- `ShotExit termination=HitWater finalPos=(-35.08, 7.27, -1.53)` — ball was in lake bounds when terminal state hit.
- OBFreeze is the locked-pivot mode contract from §2b § L9/Q3'a: "first OB sample's XZ + 5m above terrain Y, camera position locked, rotation tracks ball." That contract validated.

The reason water isn't in the framing is camera **yaw orientation** — Director's OBFreeze rotation logic currently tracks the ball *flying away from the locked pivot*, which orients the camera AWAY from the lake (toward the wooded shore). Whether that's the right framing intent is an architectural question, not a smoke-task defect:
- If we want the lake visible in OBFreeze, the camera should rotate to keep BOTH the ball AND the hazard in frame, OR rotate toward the hazard with the ball offset to one side.
- That's a Director framing redesign, not a smoke fix.

Spec § "Out of scope" already excludes per-state animation timing tuning. Camera rotation logic in OBFreeze is the same category. **Logging a forward flag in TellCode** for OBFreeze framing review when post-Loop-v1 visual polish lands.

#### 3. Downrange capture is faint trajectory line, ball-in-flight not crisp → ACCEPT

**Reviewer's concern:** Faint white diagonal line, no clearly visible ball-shape. Mode-history evidence is solid.

**Architect ruling:** Accept. Ball at distance is genuinely small in a Game View capture; the camera is positioned past the landing zone (per Downrange contract), so the ball is necessarily far from camera in frame. Mode history `[Chase, Downrange]` confirms transition fired. The thin trajectory ribbon visible in the capture is the `TrajectoryRenderer` lab-debug overlay confirming flight line direction — that's the secondary visual evidence the reviewer was expected to find. Sufficient.

### Three architect-flagged spec deviations from the implementer report — all accepted

1. **`SnapWhenModeReached` late-bound `Action<int>` signature** instead of typed `(LoopCameraDirector, ChaseCamera.Mode)` — pre-approved per SPEC § escalation paths Q1 (asmdef cycle resolution). Functionally equivalent one-shot pattern.
2. **Putter GroundLevel late-fallback capture** instead of `SnapWhenStateReached(BallState.Rolling)` — Rolling state too brief; load-bearing test (no Downrange in mode history) still holds. See Q1 ruling above for forward-disposition.
3. **OBFreeze heading override** `CameraHeadingRadians = 2.888rad` to bypass terrain ridge at x≈-22 — necessary to reach lake; documented; ball did hit water as required. Acceptable lab-time setup.

### Closing actions (this addendum)

- STATUS → `ARCHITECT_REVIEW_PASS` ✅ (already flipped)
- Notion controls_g_smoke_followup entry [`35931e0e-9a36-81b3`](https://www.notion.so/35931e0e9a3681b3a724ef1e42678928) flipped In Progress → Done, Closed=2026-05-07.
- TellCode.md: `controls_g_smoke_followup` NEXT pointer → DONE block; **§2b deferred-smoke OPEN flag confirmed CLOSED.**
- TellCode.md: NEW OPEN flag added — "OBFreeze camera framing — visible-water question deferred to post-Loop-v1 visual polish."
- TellCode.md: Cross-reference to `Docs/Specs/Queued/puttpath_predictor_perf_and_design/` for putter-predictor visual gap.
- Lesson candidates evaluated — both ("late-fallback capture pattern" + "OBFreeze framing question") deferred to actual-bug-occurrence; no new lessons written this round (lessons already exist for state-driven capture preference).
- §2b umbrella status: `loop_v1_2b_camera_transitions` is now FULLY closed end-to-end (was PASS_WITH_DEFERRAL on 2026-05-07 09:20 JST; deferred smoke debt now resolved).
