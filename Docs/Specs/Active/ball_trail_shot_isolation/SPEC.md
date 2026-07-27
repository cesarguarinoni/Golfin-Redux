# SPEC — `ball_trail_shot_isolation`

**Order:** (Notion, P2, Gameplay Polish)
**Tier:** 2 — TELLCODE (multi-file, established patterns), **but Stage 1 is measure-only**
**Scope:** Presentation only. No sim, no CSV, no asmdef.
**Surfaced by:** Cesar, iPhone build 2026-07-27 — "the trail persists between shots; it fades away slowly but you can see the previous shot's trail during the following shot."

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
2. **`BallAnimator.DestroyInstance` is editor/device-divergent** — lines 257-261: `DestroyImmediate` under `#if UNITY_EDITOR`, plain deferred `Destroy` in player builds. Cesar is seeing this **on device**, where destruction is deferred and the editor's instant path does not apply.

---

## 2. Stage 1 — measure (no fix yet)

Instrument and reproduce **on device or in a player-equivalent build**, not editor-only. Editor-only repro attempts are not evidence here — the divergence in §1.2 is precisely why.

Log, per shot, with frame numbers:
- ball instance id at `Play()` entry, after `DestroyInstance`, after `SpawnInstance`
- count of live `TrailRenderer` components in the scene, sampled each frame for ~10 frames after a shot fires
- for each live `TrailRenderer`: its instance id, owning GameObject path, `emitting`, `time`, positionCount
- the frame index at which the previous shot's `TrailRenderer` actually ceases to render

### Ranked hypotheses

| # | Hypothesis | Evidence for | Discriminating measurement |
|---|---|---|---|
| **H1** | Deferred `Destroy` on device keeps the old ball + ribbon alive longer than expected. | Confirmed editor/device divergence (§1.2). Matches that Cesar sees it on iPhone. | Live `TrailRenderer` count > 1 for more than one frame after `Play()`. |
| **H2** | A duplicate / ghost ball instance carries a live trail. | Non-speculative: `BallAnimator.Awake` already ships a ghost-clone sweep for exactly this class of bug ("dozens accumulate"). | Two live `TrailRenderer`s with different owning GameObject paths, one not matching `CurrentBall`. |
| **H3** | `_time = 8f` alone — the ribbon seen is the *previous* shot's, still fading through the aiming phase, and reads as bleeding into the next shot. | Confirmed 8s lifetime + `emitting=false`-only at rest. | Exactly one live `TrailRenderer`, and the stale ribbon disappears the instant the next shot fires. |

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
- [ ] Fire 5 consecutive shots on Hole_01: during each shot's flight, **exactly one** ribbon is visible and it belongs to that shot.
- [ ] The OB red-recolor path still works (shot into the Hole_06 lake flips the whole ribbon red).
- [ ] The perfect-shot gold path still works.
- [ ] Trail still renders over terrain during roll (the `ZTest = Always` / `renderQueue = 4000` behaviour in `EnsureTrail` is intact).
- [ ] EditMode suite green against the 933/938 baseline (2 pre-existing `StaminaLiveWiring` failures are orthogonal — leave them).

---

## 6. Video gate

Real play (`screenshot-game-view` MCP tool / real-user flow — hand-rolled `script-execute` captures are hard-blocked by `.claude/hooks/enforce_capture_tool.py`).

One clip, **before and after**: three consecutive shots on Hole_01 played back to back with minimal aiming delay. BEFORE shows the previous ribbon overlapping the new shot; AFTER shows one ribbon per shot.

**Captured on device or a player-equivalent build**, for the reason in §1.2.

---

## 7. Files touched (expected)

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/BallTrailController.cs` | primary |
| `Assets/Scripts/Physics/Viewer/BallAnimator.cs` | **only if H2**, and call it out before editing |

Anything beyond this — stop and report.

---

## 8. Report

`IMPLEMENTER_REPORT.md` must contain the Stage 1 log excerpt, the hypothesis verdict, the fix chosen and why, test counts before/after, and the video links.

**Derive from the primary source; do not confirm an artifact that asserts it.**
