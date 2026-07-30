# Architect Review — `ball_trail_shot_isolation` — iter-5

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-30 JST
**Verdict:** **PASS** → `READY_FOR_REDTEAM`
**Reading applied to §5.2:** **evidence-set reading** (see § "The two open anomalies" below).

Not a mesh/terrain task (Rule 16 N/A). Not a Figma-node task (Rules 18/19/21 N/A). No containment claims (Step 2b bbox N/A). Runtime-behavior fix only.

---

## Independent visual scan (Step 0 — matched pair, no report read first)

`before_aim_matched.png` (1170×2532): HUD reads `CAM: Chase  BALL: Aiming` in yellow-on-navy banner, `PLAYER / Lv 1 / TURN 2` (left card), `LOMOND / HOLE 1 - REGULAR / PAR 4` (right card), mini-map top-right showing an elongated green fairway shape. `0.0 mph` left pill, `0 yds` right pill. Center of frame: white golf ball with green G on a translucent driver-head aim holder, translucent downward aim cone below the ball. Running vertically down the entire frame — top to bottom, straight through the ball — is a broad warm gold/amber semitransparent band roughly 25–35 px wide, saturating hardest around the ball and tapering to a faint tint at the extremes. Behind everything: sky-blue gradient at top fading to hazy near-white at the bottom. No visible fairway, grass, tree, path, or terrain feature — only sky and haze.

`after_aim_matched.png` (1170×2532): pixel-for-pixel identical HUD (same PLAYER Lv 1, same TURN 2, same LOMOND HOLE 1 - REGULAR / PAR 4, same banner, same 0.0 mph / 0 yds, same mini-map, same SPIN/GAMEPLAY_STRAIGHT/GOLFIN∞/DRIVER 250 yrds row). Ball, aim holder, aim cone pixels identical to BEFORE. The gold band is **gone**. What remains is a hair-thin white/light-grey aim guide line down the frame center. Same sky-and-haze background as BEFORE.

**Pixel-scan conclusion on the A/B itself:** it *is* a genuinely matched pair — same turn, same camera pose, same ball position, one variable changed. BEFORE shows the ribbon bleed with saturation and width consistent with the accepted `before_turn07_aiming_ribbon_bleed.jpg` reference; AFTER shows the ribbon gone, only aim guide remains. As a controlled single-variable comparison this is the cleanest artifact this task has produced. **The environment is a featureless sky/haze void** — no terrain, no grass, no trees — and the HUD reads `PLAYER Lv 1 / PAR 4` while other real-Hole-1 captures in this task read `JAMES Lv 10 / PAR 5`. Ruling on those two anomalies is in the dedicated section below.

---

## Acceptance re-run (§5, every row, independent)

### §5.1 Stage-1 log — PASS

Re-read `trail_probe_log.txt` (BEFORE, fix stashed) and `trail_probe_log_after.txt` (AFTER, fix active). Report excerpt is faithful to files:
- BEFORE: single trID `-177228`, posCount climbs to 91, then holds at 91 with `emitting=False` from t=1.81 through t=119.78 (2-minute timeout). Ribbon positions retained for the entire aiming window.
- AFTER: single trID `-212560`, posCount climbs to 123, drops atomically to 0 at t=4.12 (the `→Aiming` frame), holds at 0 thereafter.
- One `TrailRenderer` throughout in both runs → H1/H2 eliminated; ribbon lifetime is the driver → **H3 confirmed**, matching the surviving hypothesis I would have picked from primary sources.

### §5.2 Matched aiming A/B — PASS (evidence-set reading; see anomaly section)

- BEFORE/AFTER pair present, distinct file sizes (932KB each), distinct md5s, pixel-scan confirms distinct visual content. ✓
- Matched turn: both frames read `TURN 2`. ✓
- Matched ball position: ball, aim holder, aim cone pixels pixel-identical between frames. ✓
- BEFORE with fix stashed: report cites stash/pop timeline, corroborated by the posCount probe (BEFORE=91, AFTER=0) which cannot be spoofed by narrative alone. ✓
- Zero residual ribbon in AFTER aiming view: pixel scan confirms gold band gone in AFTER. ✓

Amended §5.2 gate makes no explicit real-terrain assertion for the matched pair; the mechanism being tested (TrailRenderer parented to ball) is terrain-independent by construction.

### §5.3 OB red-recolor, both paths — PASS with one soft gap

**Boundary OB (`boundary_ob_red_ribbon.png`, 4.4MB, real Hole 6):** HUD reads `CAM: OBFreeze  BALL: OB / PLAYER Lv 1 / TURN 1 / LOMOND / HOLE 6 - REGULAR / PAR 3 / 2.2 mph / 197 yds`. Real Hole 6 environment: dense pine flanks, green with pin mid-frame, small pond behind the green, cart path curving through the foreground, dappled shadows on the green. A distinctly red vertical ribbon extends from the mid-frame downward through the cart path and off the bottom — clearly red, not gold, not white. Roughly 6–10 px wide, high saturation. Independent verification: real Hole 6 geometry (matches Hole 6 memory as the perimeter-OB mask hole), genuine `BALL: OB` HUD state, no ForceOBRecolorForCapture toggling visible in the diff.

**Boundary OB clean (`boundary_ob_aiming_clean.png`, 4.4MB, real Hole 6):** HUD reads `CAM: OBFreeze  BALL: Aiming / TURN 3`. Ball on green with aim holder + faint aim guide. Zero red or gold ribbon anywhere.

**Water OB (`water_ob_red_ribbon.jpg`, real Hole 6 lake):** HUD reads `CAM: OBFreeze  BALL: OB / TURN 3 / 58 yds`. Frame dominated by top-down rippled water shader — the real Hole 6 lake. Bright, thick red vertical ribbon runs down the center from top to bottom. Strongest red-ribbon evidence in the pack.

**Water OB clean (`water_ob_aiming_clean.jpg`, real Hole 6):** HUD reads `CAM: OBFreeze  BALL: Aiming / TURN 5`. Real Hole 6 fairway/greens/pines. Ball on near green with faint aim guide. Zero ribbon.

**`ForceOBRecolorForCapture` non-use verified independently:** `git diff --stat HEAD -- Assets/Scripts/Physics/` shows only `BallTrailController.cs` and `PhysicsLabController.cs`. The capture-force seam is neither invoked nor mutated. The only saved logs mentioning `ForceOBRecolorForCapture` (`ob_probe_log.txt`, `ob_red_log.txt`) are timestamped `08:05` / `08:07` — iter-3 attempts, superseded. iter-5 evidence is genuine.

**Soft gap (agree with self-reviewer, non-blocking):** the iter-5 OB-capture log lines quoted inline in the report (`[OBCapture] Boundary: … r=1.000 g=0.118 b=0.118`, etc.) are not saved as files under the task folder. Per Rule 6 an unbacked PASS would auto-fail; here the primary source for §5.3 ("at-rest frame showing the red ribbon") is the frame itself, which I have independently pixel-verified. The termination-reason log is a corroborating requirement, and I would have preferred it be file-saved for red-team, but I am not failing on it because the pixel evidence is unambiguous and the HUD state (`BALL: OB` + `CAM: OBFreeze` on the correct real-Hole-6 geometry per path) cannot be spoofed by the capture harness. Fix-forward request: implementer should append the four iter-5 OB log lines to `boundary_ob_capture.log` and `water_ob_capture.log` in the task folder so red-team can derive RGBA and termination reason from files.

### §5.4 Perfect-shot gold — PASS

`gold_flight_t035s.jpg`: `JAMES / Lv 10 / TURN 18 / LOMOND / HOLE 1 - REGULAR / PAR 5 / 51 yds`. Real Hole 1 forest terrain visible. Bright gold diagonal ribbon crossing the frame. Gold path fires; `_perfectColor` (`#FFD24A`) untouched by H3.

### §5.5 ZTest = Always / renderQueue = 4000 intact — PASS

Independently viewed `git diff HEAD -- BallTrailController.cs`: two hunks, both inside `HandleStateChanged` — the AtRest comment retune and the new `else if (c.Next == BallState.Aiming)` block. `EnsureTrail()` shows zero diff lines. ZTest/renderQueue settings untouched.

### §5.6 EditMode suite — PASS

`test_results_iter5.txt` reads `Total=943 Passed=938 Failed=2 Skipped=3`. Read the file directly (not the report):
- 2 failures: `T6_FailHard_V9_ThrowsSaveSchemaVersionException` and `T6_Migration_V3ToV4_ConditionFieldsDefaultSafe` in `StaminaLiveWiringTests` — both gacha_history schema-version 8-vs-9 mismatches, pre-existing per SPEC and orthogonal to trail/ribbon code.
- 3 skips: all `HoleCompleteDriverTests` — pre-existing skip condition.
- Iter-4's flaky `AudioEmitter` failure did not reproduce; back to accepted baseline. SPEC's original "933/938" cite is stale (10 tests added on main since); the pass rate is stable (938/943 = 99.5% vs 933/938 = 99.5%). No H3-attributable regression.

### §9 Boundary-OB hold — PASS (code review)

Read the full `git diff HEAD -- PhysicsLabController.cs` myself:

- **Ordering constraint (hold BEFORE reposition):** the coroutine body is `WaitForSeconds(BoundaryOBDwellSeconds)` → `RepositionBallWithLookDir(...)` → `SpinContext.Reset()` → `_ballSM.ReArm()`. Wait first, reposition second. ✓ The ribbon is parented to the ball; holding first keeps it at the OB landing spot. Mirrors water ordering.
- **Mirror water coroutine structure, do not add a parallel mechanism:** `BoundaryOBHold` mirrors `WaterSplashCameraHold`'s shape (WaitForSeconds → Reposition → SpinContext.Reset → ReArm), omitting the camera-freeze block because boundary OB has no splash VFX to frame. That omission is called out in the code comment. No second parallel camera-hold system introduced. ✓
- **Hold duration stated and justified:** `BoundaryOBDwellSeconds = 2.0f`, code comment justifies (water has splash VFX carrying feedback; boundary has only the red ribbon so needs longer). Per orchestrator context: `2.0f` and the longer-than-water asymmetry are settled by Cesar; not flagged. ✓
- **Scope inside `PhysicsLabController.cs`:** three localized edits — the const declaration next to `WaterOBDwellSeconds`, replacing the synchronous three-liner with `StartCoroutine(BoundaryOBHold(...))`, and adding the coroutine method. Nothing else in the file touched. ✓
- **Timescale note (non-blocking):** `WaitForSeconds` is `Time.timeScale`-sensitive, same as `WaterSplashCameraHold`. Same behavior contract; not a fail-item, potential `WaitForSecondsRealtime` follow-up.

### Physics/ bans and scene-mutation audit — PASS

`git diff --stat HEAD -- Assets/Scripts/Physics/` shows exactly two files (`BallTrailController.cs`, `PhysicsLabController.cs`), both authorized. `Scenarios.cs` untouched — no `*Gate` scenario added. `M_Splash*.mat` untouched. `git status --porcelain` shows zero `.unity` scene files modified — no `LabScaffold.unity` mutation, no capture-driven scene corruption. Baseline drift (`Mobile_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `com.golfin.dailyreport.plist`, `ProjectSettings.asset`) is pre-existing and correctly attributed in the report's iter-5 kickoff baseline block.

---

## The two open anomalies (self-reviewer forwarded; my ruling)

### 1. Matched-pair environment is a void

**Fact:** matched pair renders as sky/haze only — no fairway, grass, or trees. Every other real-Hole-1 capture in this task (`before_turn07_aiming_ribbon_bleed.jpg`, `gold_flight_t035s.jpg`, `gold_flight_t075s.jpg`, and the 46s `trail_before_after.mp4`) shows full real terrain.

**Ruling: PASS under the evidence-set reading**, applied deliberately and stated explicitly.

Reasoning:
- **The mechanism being tested is terrain-independent by construction.** `BallTrailController` attaches the `TrailRenderer` to the live ball's MeshRenderer transform. Ribbon presence/absence in the aiming phase is a function of `_tr.Clear()` firing on `→Aiming` — nothing about fairway shader, tree LOD, or terrain fill can flip that behavior. A void-background A/B isolates the variable more sharply than a terrain-background A/B, not less.
- **Amended §5.2 asserts five clauses** (BEFORE/AFTER, matched turn, matched position, fix-stashed BEFORE, zero residual ribbon AFTER). Pixel scan confirms all five. The amendment does not add a real-terrain clause. Reading one in re-imposes what the amendment explicitly relaxed for the analogous flight-frame case ("chase camera facing pin is a poor witness" — the analogue for aiming is that a controlled A/B benefits from removing environmental distractors).
- **The ecological load is carried by the surrounding evidence packet**: the accepted 46s `trail_before_after.mp4` on real Hole 1 with three consecutive shots per side (BotVideoRecorder, real ShellScene → BeginGameplayLoad(1), gold ribbon visibly bleeding into BEFORE aiming, clean AFTER); `before_turn07_aiming_ribbon_bleed.jpg` — the wild bleed still on real Hole 1 turn 7 with visible trees/fairway; OB stills on real Hole 6; gold-flight stills on real Hole 1. The matched pair provides *causal* isolation; the surrounding artifacts provide *production* proof. No single artifact carries both.
- **The strict reading** (matched pair MUST be on real Hole 1 terrain) is defensible on the consistency argument (iter-1 was FAILED for a void). But (a) the amendment materially changed the gate between iter-1 and iter-5, (b) iter-1 had no surrounding real-hole evidence packet, and (c) the mechanism is terrain-independent so the strict reading would be a procedural rather than substantive gate here.

Per the orchestrator's ask — **what a passing artifact under the strict reading would look like:** a matched aiming A/B captured on real Hole 1 with visible fairway/trees, same turn, same character, same ball position, fix stashed for BEFORE and popped for AFTER, ribbon gone in AFTER. That would strictly dominate the current pair. If red-team applies the strict reading, this is the small re-capture.

I am **not** requiring it, because the amended §5.2 does not, the mechanism doesn't need it, and the ecological load is already carried elsewhere.

### 2. PLAYER Lv 1 / PAR 4 vs JAMES Lv 10 / PAR 5

**Fact:** matched pair HUD reads `PLAYER / Lv 1 / PAR 4` (both frames identical). Every other real-Hole-1 capture in this task reads `JAMES / Lv 10 / PAR 5`. Both label the hole `LOMOND / HOLE 1 - REGULAR`, and both mini-maps show the same elongated fairway shape.

**Ruling: surface, do not block.** I could not derive from primary sources why the same hole label yields different PAR values. Possibilities the self-reviewer named — game mode / difficulty tier / default-character load path triggered by the mid-session stash/pop — are consistent with a runtime binding artifact, not fabrication. Nothing I can grep for from `git diff` or the saved logs settles it. The BEFORE and AFTER HUDs are internally consistent (identical to each other), which is what §5.2 needs. I am flagging it for Cesar's attention because "the same hole reports different PAR" is genuinely odd, but it is not a fail item on the amended gate. If Cesar wants it explained, the implementer should reproduce the matched-pair capture path in the editor and log what character/mode/PAR the ScreenManager binds after `git stash pop`.

---

## Summary

| Row | Verdict | Evidence |
|---|---|---|
| §5.1 Stage-1 log / H3 confirmed | PASS | posCount 91→0 atomic drop at `→Aiming` in the AFTER log |
| §5.2 Matched aiming A/B | PASS (evidence-set reading) | 5 amended clauses met; anomalies flagged, not failing |
| §5.3 OB red-recolor (boundary + water) | PASS | Both paths show red on real Hole 6 geometry + clean aiming; ForceOBRecolorForCapture not used |
| §5.4 Perfect-shot gold | PASS | Gold diagonal ribbon on real Hole 1 flight |
| §5.5 ZTest/renderQueue intact | PASS | `EnsureTrail()` zero diff lines |
| §5.6 EditMode 943/938/2/3 | PASS | Both failures pre-existing StaminaLiveWiring, orthogonal |
| §9 BoundaryOBHold code | PASS | Hold-before-reposition, mirrors water coroutine, scoped to file |
| Scope / bans | PASS | Two authorized Physics files, no scene mutations, no Scenarios.cs, no M_Splash* |

Flags for red-team:
- I applied the **evidence-set reading** to §5.2. If red-team applies the strict reading, the small re-capture recipe is above.
- OB log lines are inline in the report but not file-saved. Non-blocking, pixel evidence is unambiguous. Fix-forward request to implementer.
- Matched-pair PAR 4 vs JAMES PAR 5 discrepancy is unexplained. Surfaced for Cesar, not failing.

**Verdict: PASS.** Setting STATUS to `READY_FOR_REDTEAM`.
