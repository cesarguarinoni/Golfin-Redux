# Loop v1 §2b — Camera Transitions — Architect NOTES

**Status:** PRE_SPEC_ROUND_2 — Cesar locked Q1–Q5 + co-ship; derivative questions below need a second pass.
**Architect (claude.ai), 2026-05-07 07:02 JST → updated 2026-05-07 (round 2)**

Roadmap §2b: `tee → flight → rest → green → cup`. Subscribes to `BallStateMachine.OnStateChanged` shipped in §2a. Memory-flagged as a fan-out candidate.

---

## 1. Code walk — what exists today

| File | Role |
|---|---|
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` (~80 lines, single MonoBehaviour) | Three modes via `enum Mode { Chase, Overhead, GroundLevel }`. `LateUpdate` switches on `_mode`, computes `desiredPos`/`desiredRot`, SmoothDamps position + Slerps rotation. Public API: `SetMode(Mode)`, `SetTarget(Transform)`, `ResetToOrigin(origin, launchDir)`, `FollowHeightOffset` setter. **Knows nothing about ball state, ShotController, SM, or hole structure.** Pure mode renderer. |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | `event Action<BallStateChange> OnStateChanged` (fine, every transition) + `event Action<ShotResult> OnShotComplete` (coarse, one-per-shot at terminal). |
| `Assets/Scripts/Gameplay/Loop/BallState.cs` | Six values: `Aiming`, `Flying`, `Rolling`, `AtRest`, `InCup`, `OB`. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | **9 sites currently mutate `chaseCamera`**, scattered across `Awake`, `SetupAtTee`, `PlaceBallAt`, `OnHoleLoaded`, `HandleCameraOrbit`, `HandleShotResolved`, `HandleShotComplete` (§2a), `FireInternal`, `AdjustCameraForDepression`. **No central camera director today** — every consumer pokes ChaseCamera directly. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs:367` | Manual `CycleCamera(int dir)` button. **Decision below: delete entirely (Cesar: "no manual switch, game controls it").** |

### Today's flow during a touch shot:
1. Putter selected → `SetClub` → `chaseCamera.SetMode(GroundLevel)`.
2. Player flicks → `HandleShotResolved` → `chaseCamera.SetTarget(ball)` + `ResetToOrigin(origin, launchDir)`.
3. Ball in flight → `LateUpdate` follows ball in current mode.
4. `_ballSM.Tick(isPlaying)` runs in `Update` → SM transitions through `Flying → Rolling → AtRest|InCup|OB`.
5. SM fires `OnShotComplete` → `HandleShotComplete` clears `chaseCamera.SetTarget(null)`, re-arms ShotController.
6. Aiming begins again.

Camera is already implicitly state-driven, but dispatch is open-coded across handlers. §2b centralizes + extends.

### Camera-touching code that §2b must coexist with (NOT delete):
- `AdjustCameraForDepression` — sets `FollowHeightOffset` for bunkers. Orthogonal; remains.
- `HandleCameraOrbit` — yaw drag during Aiming. Already correctly gates on `Chase` mode; remains.
- `_shotConeView.SetCamera` and HUD widget camera wires — orthogonal; remain.
- `_puttPathPredictor.SetCamera` — orthogonal; remains.

---

## 2. Locked decisions

### Round 1 (architect leans, Cesar accepted)
- **L1.** New `LoopCameraDirector` MonoBehaviour mediates between SM and ChaseCamera. ChaseCamera stays a pure mode-renderer.
- **L2.** Asmdef placement: stay in `Golfin.Physics.Viewer` for v1.
- **L3.** Subscribe to `OnStateChanged` (fine-grained), not `OnShotComplete` (coarse).
- **L4.** Mode dispatch is pure data (`Dictionary<BallState, Mode>` or equivalent struct).
- **L5.** Existing SetClub putter→GroundLevel coupling stays in `PhysicsLabController.SetClub`.
- **L6.** Director wired in `PhysicsLabController.Awake` next to existing SM wiring.
- **L7.** `HandleShotResolved`'s camera calls (`SetTarget(ball)`, `ResetToOrigin`) and `HandleShotComplete`'s `SetTarget(null)` move from PhysicsLabController to the Director.

### Round 2 (Cesar's directives, 2026-05-07)
- **L8. Cinematic camera with mid-flight cut.** Default Flying mode = standard chase ("Follow"). Promote to a multi-stage cinematic per state group:
   - Early flight (0–~65% of carry): Chase from behind ball, follow through air.
   - Late flight (~65–100%): cut to a "downrange" camera positioned past the predicted landing zone, looking back along flight line (PGA 2K23 best practice; EA "Pro" mode minus the bad timing).
   - Open question Q1' below: what triggers the cut, what's the exact framing.
- **L9. OB camera = freeze at OB-limit, rotate to track.** When ball crosses into OB terrain, lock camera position at the crossing point, allow rotation only as ball continues flying away. Fixes the EA "camera loses ball then snaps back" failure mode.
- **L10. Chase framing is too distant.** Current 8m back / 3m up makes ball too small. Tighten — exact numbers in Q3' below.
- **L11. CupZoom = tween (Q2-b)**, with explicit constraint: the "cup" is currently just a flat circle on the green, not a 3D hole. CupZoom must HOVER ABOVE the circle looking down, NOT dive into geometry. Add follow-up: when cup geometry becomes 3D (later visual pass), revisit CupZoom to add a real dive-in moment.
- **L12. No manual camera override.** Director is authoritative. `PhysicsLabUI.CycleCamera` button is **deleted** (Q4' below: confirm or keep as `#if UNITY_EDITOR` lab-only debug).
- **L13. TrajectoryRenderer = lab-debug only**, hide in gameplay (add `_showInGameplay = false` flag or scaffold-driven disable). `ShotConeView` = keep on in gameplay. `PuttPathPredictor` = **separate research+redesign task** (perf measurement + sim-vs-arcade design call), spun out to its own Queued spec; §2b hides it in gameplay scaffold by default until that work lands.
- **L14.** Director is MonoBehaviour (Q5-a). Inspector-wires `chaseCamera`, gets `_ballSM` from PhysicsLabController via internal accessor.
- **L15. Co-ship CaptureHelper consolidation with §2b.** Scope: factor capture core into runtime-side `Golfin.Diagnostics.Runtime` asmdef (consumable from both editor and runtime); add SM-state-gated capture API (`SnapWhenStateReached(BallState target)` or similar); remove inline byte-equivalent copy from `SmokeTestRunner2a`. Closes the §2a OPEN FLAG.

### Mapping table after L8/L9/L11

| BallState | Camera Mode | Notes |
|---|---|---|
| `Aiming` | (unchanged) | Director leaves whatever was set. Club-driven GroundLevel for putter survives. |
| `Flying` | `Chase` (early) → `Downrange` (late) | Cinematic cut at ~65% of horizontal carry. See Q1'. |
| `Rolling` | `Chase` | Director switches back to chase from downrange when ball touches down. |
| `AtRest` | `Chase` | Settles. Could pull-back-and-frame later; v1 = chase. |
| `InCup` | `CupZoom` (NEW) | Hover-above-circle tween; v1 cup is flat. |
| `OB` | `OBFreeze` (NEW) | Camera position locked at OB-crossing point; rotation tracks ball. |

`Overhead` mode survives but is no longer state-mapped (used to be the lean for OB). Could be kept for §2c (turn-counter "summary frame" on hole-end) or deleted in cleanup.

---

## 3. Open questions — round 2 (lock before SPEC)

### Q1'. Cinematic cut trigger + downrange framing

**(a) Cut trigger.** Three options for when chase→downrange transition fires:
   - **(i)** Fixed fraction of horizontal carry (e.g. 65%). Requires predicted carry — known from `Trajectory` (already computed). Predictable.
   - **(ii)** Fixed fraction of flight time. Easier; doesn't need carry distance.
   - **(iii)** State-driven: trigger on a NEW intermediate state like `Apex` or `Descending`. Requires SM extension — bigger scope.

   Architect lean: **(i)** at 65%. Carry is the player-relevant unit; we already have it post-`OnTrajectoryComputed`. Avoids SM changes.

**(b) Downrange framing.** Where is the downrange camera positioned?
   - **(i)** **Behind the landing zone, looking back along flight line** (PGA 2K23 "downrange"). Shows ball flying toward camera, lands in foreground. Canonical golf-game cinematic.
   - **(ii)** **Side cam 90° to flight line**, mid-height. Shows ball arc in profile. Dramatic but harder to read distance.
   - **(iii)** **Tower cam: high + offset behind landing**, "TV broadcast" angle. Most produced; hardest to compose without specific hole knowledge.

   Architect lean: **(i)**. (ii) is a possible v2 add (alternate for variety); (iii) needs per-hole camera authoring we don't have.

**(c) Cinematic on putts?** Probably not — putts are short, downrange cam would feel jarring. Putts stay in `GroundLevel` or a new `PutterChase` mode. Lean: putts skip the cinematic cut entirely (Director checks `isPutt` flag on Flying entry; if putt, no Downrange, just keep current putter framing).

### Q2'. Chase framing retune (L10)

Current chase: `desiredPos = focus - launchDir × 8 + Vector3.up × 3`. Camera FOV is whatever's on the Camera component (default Unity 60°, not set explicitly in ChaseCamera.cs).

   - **(a) Pull camera in closer, FOV unchanged.** New: `-launchDir × 5 + up × 2.5`. Ball appears ~60% larger.
   - **(b) Reduce FOV, distance unchanged.** New: FOV 50° (or 45°). Ball appears similarly larger but with telephoto-compression feel.
   - **(c) Both — closer AND tighter FOV.** Aggressive zoom; risk of clipping or losing context.

   Architect lean: **(a)**. Distance change is what the player will perceive most directly; FOV change adds compression artifacts that read as "weird" on a sports cam. Specific numbers will need iteration in playtest — locking the SPEC at `5m back, 2.5m up, FOV unchanged` as a starting point.

### Q3'. OB-freeze pivot — what's "the OB limit" exactly?

When ball crosses into OB territory, camera freezes WHERE?
   - **(a) The XZ point where the trajectory crossed from non-OB to OB-classified terrain** (geometric). Detected via `BakedZoneClassifier` on each trajectory sample; first OB-classified sample = pivot point.
   - **(b) The point where the ball crossed the world bounds** (`ExitedWorldBounds`). Only meaningful for off-the-map hits; misses water/OOB-marker cases.
   - **(c) The player/tee position — "stay where you are, just rotate."** Easiest to implement; loses spatial context (camera doesn't move toward where ball went OB).

   Architect lean: **(a)**. Most semantically correct. Implementation: scan `Trajectory.terrainHits` for first hit with `Surface == Water` (OB-classified), use that XZ + a fixed Y elevation (e.g. 5m above terrain at that XZ).

### Q4'. PhysicsLabUI.CycleCamera button

Cesar said "no manual switch, game controls it." Two interpretations:
   - **(a) Delete the button outright.** Cleanest. If lab-only debug needs return, add behind `#if UNITY_EDITOR`.
   - **(b) Keep button but Director ignores it / stomps on next state change.** Useless in practice; clutters lab UI.

   Architect lean: **(a) delete**. Removes a ~15-line `CycleCamera` method + button wiring + `CameraLabels` array. Simplifies §2b's surface area.

### Q5'. CaptureHelper consolidation scope

Two parts to land. Confirm both, or split?
   - **(part 1) Asmdef consolidation:** factor `CaptureHelper.SnapAtEndOfFrameAndPause` from editor-only `Golfin.EditorTools` into a new runtime-side helper assembly `Golfin.Diagnostics.Runtime`. Editor side becomes a thin wrapper. Removes the inline byte-equivalent copy from §2a's `SmokeTestRunner2a`.
   - **(part 2) SM-state-gated capture API:** new method `CaptureHelper.SnapWhenStateReached(BallStateMachine sm, BallState target, string label)`. Subscribes to `OnStateChanged`, snaps once when target state is reached, unsubscribes. Future smoke tests get deterministic capture timing.

   Architect lean: **both, in §2b**. Part 1 is the prerequisite (without it, part 2 can't live where the SM lives). Part 2 is the actual gameplay-affecting fix. Adds maybe 0.5 day to §2b spec scope; closes both halves of the §2a OPEN FLAG.

---

## 4. PuttPathPredictor — separate research task (per L13)

Pulled out of §2b. New Queued spec: `Docs/Specs/Queued/puttpath_predictor_perf_and_design/NOTES.md`. Two threads:

- **Perf:** Profiler session on `BallSimulation.Simulate` over 60 frames of active aiming. Already a Putter P1 follow-up flag; just hasn't been actioned.
- **Design:** Sim convention is grid + slope arrows, NOT a full predicted ball line. Predicted-line UX is arcade territory and Cesar's instinct that it "might be too much" matches sim convention. Options for redesign: keep but throttle, replace with grid+arrows, add a target marker at the apex, hybrid (short predicted segment near ball + arrows farther).

§2b's Director hides PuttPathPredictor in any gameplay scaffold by default (lab keeps it on). Real disposition lands when this research spec ships.

---

## 5. Fan-out feasibility verdict

Recommended subtask decomposition (if Tier 3 pipeline is used):

| Subtask | Files touched | Independence |
|---|---|---|
| **2b.1** Director skeleton + SM subscription + state→mode dispatch table | NEW `LoopCameraDirector.cs` + edit `PhysicsLabController.cs` (move 2 sites) | Spine — must land first. |
| **2b.2** Cinematic cut (`Downrange` mode + cut trigger) | edit `ChaseCamera.cs` (new case + downrange-position math) + Director cut logic | **Independent of 2b.1** once dispatch table format is frozen. |
| **2b.3** `CupZoom` mode (hover-above-circle tween) | edit `ChaseCamera.cs` (new case + tween state) | **Independent of 2b.1.** |
| **2b.4** `OBFreeze` mode | edit `ChaseCamera.cs` (new case + freeze pivot) + Director's OB detection + scan trajectory for OB-crossing | **Independent of 2b.1.** |
| **2b.5** Chase framing retune (5m/2.5m default) | edit `ChaseCamera.cs` (one constant change) | Trivial. Could fold into 2b.1. |
| **2b.6** Delete `PhysicsLabUI.CycleCamera` button | edit `PhysicsLabUI.cs` (remove method + button) | Independent. |
| **2b.7** EditMode tests for Director | NEW `LoopCameraDirectorTests.cs` | Independent of 2b.1 if Director exposes `IModeSetter` test seam. |
| **2b.8** TrajectoryRenderer gameplay-hide flag | edit `TrajectoryRenderer.cs` + scaffold-side default | Independent. |
| **2b.9** CaptureHelper part 1 (asmdef consolidation) | NEW `Golfin.Diagnostics.Runtime.asmdef` + move file + thin editor wrapper | Independent of 2b.1–2b.8; touches different asmdefs. |
| **2b.10** CaptureHelper part 2 (SM-gated API) | edit `CaptureHelper.cs` (new method) + use in test | Depends on 2b.9 + §2a's SM. |

**Realistic parallelism:** serial 2b.1 → fan-out (2b.2 ‖ 2b.3 ‖ 2b.4 ‖ 2b.6 ‖ 2b.8 ‖ 2b.9), then serial 2b.7 + 2b.10. ~6-way fan-out after spine.

**Caveat against full parallelism:** ChaseCamera.cs is ~80 lines; six concurrent edits on the same file = merge churn. Recommend two implementer waves: wave 1 = 2b.1 + 2b.5 + 2b.6 + 2b.8 + 2b.9; wave 2 = 2b.2 ‖ 2b.3 ‖ 2b.4 (each gets ChaseCamera to itself for one mode addition); wave 3 = 2b.7 + 2b.10.

---

## 6. Risk profile

- **Determinism:** Camera transforms are visual-only. Tweens can use `Time.deltaTime` freely. Bot-pool headless skips Director entirely. Zero physics-determinism risk.
- **Test gate:** §2a's 227/227 must hold. Director adds tests; doesn't touch BallSimulation, fpMath, configs, or any test-touched source. **Predicted: 227 → ~233 PASS**.
- **CupZoom on flat-circle cup:** without a real 3D hole, the zoom-in moment is somewhat arbitrary visually. v1 ships an intentional hover; visual polish on a follow-up when cup geometry lands.
- **OBFreeze edge case:** if a ball is OB at very low altitude (e.g. shanks into adjacent water), the freeze position is essentially the player position. Not great visually. Acceptable v1; revisit if it feels broken in playtest.
- **Cinematic cut on short shots:** chip + putt distances may not warrant the cut. Cesar's putter-skip-cinematic logic (Q1'-c) covers putts; chips/flop shots might also feel jarring. Mitigation: only fire downrange cut if predicted carry > some threshold (e.g. 30m). Adds complexity; lock in SPEC.

---

## 7. Definition-of-done (preliminary, will harden in SPEC)

- `LoopCameraDirector` MonoBehaviour shipped, Inspector-wired in LabScaffold, subscribed to `_ballSM.OnStateChanged`.
- New ChaseCamera modes shipped: `Downrange`, `CupZoom`, `OBFreeze`.
- Chase framing retuned to 5m back / 2.5m up.
- `PhysicsLabUI.CycleCamera` deleted (or hidden behind `#if UNITY_EDITOR` per Q4').
- `TrajectoryRenderer` gameplay-hide flag added; lab default true, gameplay default false.
- `Golfin.Diagnostics.Runtime` asmdef created; CaptureHelper core moved; SM-gated capture API added.
- 6+ new EditMode tests for Director (state→mode dispatch, putter-skip-cinematic, OB-freeze pivot detection, etc).
- Test gate held: 227/227 pre-existing PASS, 0 IGNORED. New tests additive.
- Smoke evidence: full lab session, drive through 18-hole loop, capture per state via the new SM-gated API, verify each transition fires the right mode.
- Two §2a OPEN FLAGs closed: CaptureHelper consolidation + capture-timing reliability.

---

## 8. Sequencing + sizing

- **Tier:** Tier 3 (full pipeline). New MonoBehaviour, new asmdef, new ChaseCamera modes, threads through SM events, visual fidelity matters.
- **Estimate:** 1–1.5 working days (up from 0.5–1 in round 1 — co-shipping CaptureHelper + cinematic + OBFreeze adds ~half a day each).
- **Critical path:** 2b.1 (Director spine) is the bottleneck; everything else fans out from it. CaptureHelper part 1 (asmdef move) can run in parallel with 2b.1.

---

## 9. Pointers (for the SPEC writer)

- ChaseCamera surface to extend: `ChaseCamera.cs:11` (Mode enum) + `LateUpdate` switch (line 53).
- Director wire-up site: `PhysicsLabController.cs:91-94` (mirrors `_ballSM.OnShotComplete += HandleShotComplete`).
- Calls to relocate from PhysicsLabController to Director: `cs:710-712` (touch shot ResetToOrigin), `cs:765` (HandleShotComplete SetTarget null), `cs:825-828` (preset shot ResetToOrigin via FireInternal — keep scaffold-driven for preset path).
- Existing PhysicsLabUI.CycleCamera to delete: `cs:367-373`.
- Putter→GroundLevel coupling to leave alone: `cs:456-457`.
- Trajectory.terrainHits scan for OB-crossing detection: `Trajectory.cs` (sample fields include `Surface`).
- CaptureHelper editor-only location today: `Assets/Scripts/Editor/CaptureHelper.cs` (per AI_CONTEXT memory).

---

## 10. Spinoff specs created from this NOTES

- `Docs/Specs/Queued/puttpath_predictor_perf_and_design/` — perf measurement + sim-vs-arcade design call. Hides PuttPathPredictor in §2b gameplay scaffold default; real disposition lands here.
