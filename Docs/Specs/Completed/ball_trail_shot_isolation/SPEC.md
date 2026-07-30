# SPEC — `ball_trail_shot_isolation`

**Order:** (Notion, P2, Gameplay Polish)
**Tier:** 2 — TELLCODE (multi-file, established patterns), **but Stage 1 is measure-only**
**Scope:** Presentation only. No sim, no CSV, no asmdef.
**Surfaced by:** Cesar, **in the Unity editor** — "the trail persists between shots; it fades away slowly but you can see the previous shot's trail during the following shot." **Present since trails were first added** — this is long-standing behaviour, not a regression.

> **Provenance correction (2026-07-29).** An earlier revision of this spec attributed the sighting to an "iPhone build 2026-07-27" and built §1.2, §2 and §6 on that. Cesar confirmed he never said that — it was an Architect assumption. He saw it in the editor. The device-only measurement gate derived from it has been struck; see §1.2.

---

## 1. Why this is measure-first

Static reading of the lifecycle says this bug **should not happen**, so shipping a fix off the current reading would be a guess. Do Stage 1 before touching anything.

### Confirmed by inspection

`BallTrailController.EnsureTrail` attaches the `TrailRenderer` to the live ball's **MeshRenderer transform** — a child of the spawned ball GameObject, not a persistent object.

Per-shot ordering, `PhysicsLabController.FireInternal` lines 1046-1055:

```
ballAnimator.Play()          → DestroyInstance() + SpawnInstance()   // old ball + its trail die
BallSM.OnTrajectoryComputed  → Aiming→Flying fires SYNCHRONOUSLY
                             → BallTrailController.HandleStateChanged
                             → EnsureTrail(newBall); _tr.Clear(); _tr.emitting = true
```

The comment in `HandleStateChanged` states the intent explicitly: the ribbon "stays for visual reference until next shot's `BallAnimator.Play()` destroys + respawns the ball."

**Also confirmed:** neither `Assets/Art/3D/Balls/Common/Prefabs/Pf_GOLFIN_Ball.prefab` nor `Assets/Art/3D/Balls/GolfinBall/Pf_Golfin_Ball.prefab` contains a baked `TrailRenderer`. So "the prefab ships its own trail that escapes the controller's config" is **eliminated** — the trail is always runtime-added.

### Two facts that make the reading suspect

1. **`_time = 8f`** — `BallTrailController.cs:34`. On `AtRest`/`InCup` the controller sets `emitting = false` only; the ribbon then lingers a full 8 seconds fading. That is the "fades away slowly" half, and it is current design.
2. **`BallAnimator.DestroyInstance` is editor/device-divergent** — lines 257-261: `DestroyImmediate` under `#if UNITY_EDITOR`, plain deferred `Destroy` in player builds. This divergence is a verifiable code fact and stays on the table as a *player-build* risk.

   **But it does not explain the sighting.** Cesar observed this in the **editor**, where `DestroyImmediate` runs and the old ball — with its child `TrailRenderer` — dies instantly at `Play()`. Deferred destruction is therefore not the mechanism behind what he saw. Any hypothesis that leans on deferred `Destroy` must explain an editor repro on its own terms.

3. **It has always been there.** Cesar reports the behaviour has been present since trails were added — it is not a regression against any recent change. A long-standing, always-on symptom points at *designed* lifetime behaviour (the 8s fade, the missing `ReArm` hook) rather than at a timing race, which would be intermittent.

---

## 2. Stage 1 — measure (no fix yet)

Instrument and reproduce **in the editor** — that is where the symptom was actually observed (§1.2). The earlier "device or player-equivalent build, editor-only is not evidence" gate was derived from the retracted iPhone attribution and is **struck**. Do not spend a pass on a device build.

Log, per shot, with frame numbers:
- ball instance id at `Play()` entry, after `DestroyInstance`, after `SpawnInstance`
- count of live `TrailRenderer` components in the scene, sampled each frame for ~10 frames after a shot fires
- for each live `TrailRenderer`: its instance id, owning GameObject path, `emitting`, `time`, positionCount
- the frame index at which the previous shot's `TrailRenderer` actually ceases to render

### Ranked hypotheses

| # | Hypothesis | Evidence for | Discriminating measurement |
|---|---|---|---|
| **H1** | Deferred `Destroy` keeps the old ball + ribbon alive longer than expected. | Editor/device divergence is real code (§1.2) — but its observational backing is **retracted**: the sighting was in the editor, on the `DestroyImmediate` path. **Demoted.** | Live `TrailRenderer` count > 1 for more than one frame after `Play()`. |
| **H2** | A duplicate / ghost ball instance carries a live trail. | Non-speculative: `BallAnimator.Awake` already ships a ghost-clone sweep for exactly this class of bug ("dozens accumulate"). | Two live `TrailRenderer`s with different owning GameObject paths, one not matching `CurrentBall`. |
| **H3** | `_time = 8f` alone — the ribbon seen is the *previous* shot's, still fading through the aiming phase, and reads as bleeding into the next shot. | Confirmed 8s lifetime + `emitting=false`-only at rest. **Now the front-runner:** it is the only hypothesis that survives an editor sighting (nothing needs to outlive `DestroyImmediate` — the ribbon is seen *before* the next `Play()`, during aiming) and the only one consistent with "always been there since trails were added" (§1.3) rather than intermittent. | Exactly one live `TrailRenderer`, and the stale ribbon disappears the instant the next shot fires. |

**Report which hypothesis the data supports, with the log excerpt.** If the data supports none of them, say so and stop — do not invent a fourth and fix it in the same pass.

---

## 3. Stage 2 — fix (gated on Stage 1)

Do not pre-build these. Implement only the one Stage 1 selects.

- **If H1** — make the old ribbon die deterministically rather than relying on destruction timing: explicitly `Clear()` and disable the outgoing ball's `TrailRenderer` in `BallTrailController` *before* `Play()` destroys it, or hold a reference to the previous trail and clear it at the `Aiming→Flying` transition. Prefer a fix inside `BallTrailController`; changing `BallAnimator`'s destroy semantics is higher-blast-radius and needs a callout.
- **If H2** — extend the existing ghost sweep to cover the leak path the data identifies. Reuse the `BallAnimator.Awake` sweep rather than adding a second, parallel one.
- **If H3** — retune. Reduce `_time`, and/or `Clear()` the ribbon on `ReArm()` (`BallStateMachine.ReArm` fires `→ Aiming`, which `BallTrailController` currently ignores entirely — that is the natural "shot is over, wipe the ribbon" hook). Note this is a **design change**, not purely a bug fix: the current behaviour deliberately leaves the ribbon up for post-shot reference. **Confirm the intended post-shot ribbon lifetime with Cesar before shipping it.**

Whichever path: the `Aiming→Flying` handler must leave exactly **one** emitting ribbon, owned by the current ball.

---

## 4. Non-goals

- Trail colour / width / gradient / material tuning. The colour-state machine (blue / red-OB / gold-perfect) is shipped and correct — do not touch `_flightColor`, `_obColor`, `_perfectColor`, or `SetRibbonColor`.
- The `ForceOBRecolorForCapture` editor seam.
- `BallAnimator` playback timing, `PlayRate`, or the hit-event stream.
- The OB camera/background work — separate order `ob_boundary_presentation`.

---

## 5. Acceptance

- [ ] Stage 1 log is in the report, with the hypothesis verdict and the excerpt that settles it.
- [ ] Fire 5 consecutive shots on Hole_01. **The gate is the aiming phase, not flight:** after each shot completes, the next shot's aiming view shows **zero** residual ribbon from the previous shot. Evidence is a BEFORE/AFTER pair at a matched turn and ball position — BEFORE captured with the fix `git stash`-ed.

  > **Amended 2026-07-30 (Cesar's call).** The original wording required a per-shot *flight* frame showing "exactly one ribbon." Dropped. The reported defect is a ribbon bleeding into the next shot's **aiming** phase, and the chase camera faces the pin while the ribbon extends backward from the ball, so a forward-facing flight frame is a poor witness. The aiming A/B is the proof. "Exactly one emitting ribbon owned by the current ball" remains the design intent in §3 — it is simply no longer gated on a flight screenshot.
- [ ] The OB red-recolor path still works — **a real shot that terminates `TerminationReason.HitOOB`** flips the whole ribbon red. Evidence: the at-rest frame showing the red ribbon, plus a log line proving the termination reason. `ForceOBRecolorForCapture` is **BANNED** as evidence for this row (§4 already lists it as a non-goal).

  > **Note 2026-07-30 — and a retracted correction.** An earlier edit of this row (by me, the orchestrator) claimed the original "shot into the Hole_06 lake" instruction was factually wrong because `SurfaceType.Water` and `SurfaceType.OOB` are distinct. **That reasoning was wrong and is retracted.** `OBReason` (`Assets/Scripts/Gameplay/Loop/OBReason.cs`) has `Water`, `OutOfBounds`, and `ExitedWorldBounds`, and `BallStateMachine.cs:142` sets `OBReason.Water` on a water landing — so the lake *does* raise `BallState.OB` and *does* flip the ribbon red. The original instruction was correct. Iter-3's 60 s of `posCount=0` was an aim/harness failure, not an impossible target.
  >
  > **The real finding, which is more serious.** `PhysicsLabController`'s `case BallState.OB` calls `RepositionBallWithLookDir(...)` then `_ballSM.ReArm()` **synchronously in the same frame** for non-water OB. So the H3 `→Aiming` handler's `_tr.Clear()` wipes the red ribbon in the very frame `SetRibbonColor(_obColor)` set it — the red is never rendered. **Water OB is the exception:** `OBReason.Water` routes through `StartCoroutine(WaterSplashCameraHold(...))`, which defers `ReArm()` past the splash beat, so the red ribbon *is* visible during the hold. Water is therefore the only OB path on which red can currently be observed — and pre-fix, red persisted ~8 s on every OB path. **This is a behavioural regression introduced by the H3 fix and needs Cesar's decision (see § Open questions), not a descope.**
- [ ] The perfect-shot gold path still works.
- [ ] Trail still renders over terrain during roll (the `ZTest = Always` / `renderQueue = 4000` behaviour in `EnsureTrail` is intact).
- [ ] EditMode suite green against the 933/938 baseline (2 pre-existing `StaminaLiveWiring` failures are orthogonal — leave them).

---

## 6. Video gate

Real play (`screenshot-game-view` MCP tool / real-user flow — hand-rolled `script-execute` captures are hard-blocked by `.claude/hooks/enforce_capture_tool.py`).

One clip, **before and after**: three consecutive shots on Hole_01 played back to back with minimal aiming delay. BEFORE shows the previous ribbon overlapping the new shot; AFTER shows one ribbon per shot.

**Captured in the editor.** The earlier "on device or a player-equivalent build" requirement is struck along with the retracted attribution (§1.2).

---

## 7. Files touched (expected)

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/BallTrailController.cs` | primary — H3 `→Aiming` wipe (DONE, accepted) |
| `Assets/Scripts/Physics/Viewer/BallAnimator.cs` | **only if H2** — not applicable, H3 was confirmed |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | **AUTHORIZED 2026-07-30 by Cesar** — boundary-OB hold, see §9 |

Anything beyond this — stop and report.

---

## 9. Boundary-OB hold (added 2026-07-30 — Cesar's decision)

**Problem.** `PhysicsLabController`'s `case BallState.OB` calls `RepositionBallWithLookDir(...)` then `_ballSM.ReArm()` **synchronously in the same frame** for non-water OB. The H3 `→Aiming` handler therefore `Clear()`s the red ribbon in the same frame `SetRibbonColor(_obColor)` set it, so red never renders. Water OB is unaffected because `OBReason.Water` routes through `StartCoroutine(WaterSplashCameraHold(...))`, which defers `ReArm()` past the splash beat.

**Decision.** Give boundary OB (`OBReason.OutOfBounds`, `OBReason.ExitedWorldBounds`) the same courtesy water already gets: **a brief hold so the red ribbon renders for a beat, then reposition + re-arm** (which wipes it). Consistent across all OB paths, preserves the red feedback, and introduces no lingering ribbon during aiming.

**Constraints.**
- Hold **before** `RepositionBallWithLookDir` — the ribbon is parented to the ball, so repositioning first would drag it. Water's coroutine already orders it this way; mirror that.
- Reuse the existing water-hold structure rather than adding a second parallel mechanism. Water keeps its splash-specific camera freeze; boundary OB needs only the timing hold.
- State the chosen hold duration in the report and justify it. **Settled 2026-07-30: `BoundaryOBDwellSeconds = 2.0f`, confirmed by Cesar** after it was surfaced that this is longer than water's 1.2s despite having no VFX on screen. Reviewers must NOT flag the 2.0s value or the longer-than-water asymmetry — it is an accepted product decision, not an oversight.
- This is authorized on `PhysicsLabController.cs` **only** for this hold. The standing "no edits under `Assets/Scripts/Physics/`" ban is waived here exactly as it already was for `BallTrailController.cs` in §7 — nothing else in the file.

**Acceptance.** A real boundary-OB shot shows the red ribbon on screen for the hold, then a clean aiming view with no ribbon. A real water OB still shows red during the splash hold. Neither leaves a ribbon in the next shot's aiming phase.

---

## 8. Report

`IMPLEMENTER_REPORT.md` must contain the Stage 1 log excerpt, the hypothesis verdict, the fix chosen and why, test counts before/after, and the video links.

**Derive from the primary source; do not confirm an artifact that asserts it.**
