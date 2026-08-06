# Shot Flick Fix Spec — Min Flick Speed Gate + Aim Lock on Upswing

**Status:** Ready for implementation (Claude Code)
**Scope:** Shot input controller only. Minimal diff — no refactors, no UI hierarchy changes.
**Design basis:** Confluence shot control spec (2024/9/17): "Minimum Flick Speed: The flick must meet a minimum speed threshold for the swing to register. If the flick is too slow, the swing will be reset" (Neko Golf reference). Aim is defined by the flick path crossing the ball, not continuous tracking during the upswing.

> NOTE: Replace `ShotController` below with the actual class that owns touch phases for the swing (the one handling pull-back → flick → release). Same for method names flagged with NOTE.

---

## Bug 1 — Shot fires on release instead of flick

**Symptom:** Letting go of the finger (no flick) sometimes fires the shot. Suspected causes: (a) no minimum flick speed check, (b) frame stutter during load producing a garbage single-frame velocity that passes as a flick.

### Fix: velocity-gated release with stutter-proof sampling

1. **Sample buffer.** Keep a small ring buffer of the last ~6 touch samples: `(position, Time.unscaledTime)`. Push every frame while the finger is down.
2. **Flick velocity = windowed average, not single-frame delta.** On release, compute velocity from the oldest sample within `flickSampleWindow` (default **0.08 s**) to the release position, using **unscaled time**. This makes a load stutter (one long frame) produce a *low* measured speed instead of a spike, and a real flick still reads correctly.
   - Discard/clamp any sample pair whose `dt > stutterFrameThreshold` (default **0.1 s**) — a hitch frame must never be the sole basis of the velocity.
3. **Gate on release.** In the touch-ended handler (NOTE: wherever the shot currently commits — likely the `TouchPhase.Ended` / pointer-up branch):
   - Require **upward** velocity component `v.y >= minFlickSpeed`.
   - `minFlickSpeed` expressed as **screen-heights per second** (default **1.2**, tune in Inspector) so it's DPI-independent. Convert: `v.y / Screen.height`.
   - **Fail → reset swing, no shot.** Same behavior as the existing "power reset" path (finger released early → power to 0, player pulls back again). NOTE: reuse the existing reset method; don't duplicate.
4. **Optional feedback:** brief "too slow" flash or the existing restricted-shot color on reset. Not required for this fix; add TODO only.

### Inspector params (on ShotController)

| Field | Default | Notes |
|---|---|---|
| `minFlickSpeed` | 1.2 | screen-heights/sec, 0 = gate off |
| `flickSampleWindow` | 0.08 | seconds |
| `stutterFrameThreshold` | 0.1 | seconds; frames longer than this are not trusted for velocity |
| `debugDisableFlickGate` | false | per project rule: new features toggleable |

---

## Bug 2 — Aiming line keeps moving during the up-flick

**Symptom:** Lateral finger drift during the upward flick keeps steering the targeting line, so shots land centered-by-luck or at unwanted angles.

**Decision (Cesar, 2026-08-06):** Lock aim **on upward reversal** — the instant vertical finger movement flips from down/hold to up. Aim = club position at the bottom of the swing.

### Fix: latch `aimLocked` at the swing bottom

1. While the finger is down and pulling back, track vertical movement per frame (from the same sample buffer as Bug 1).
2. **Reversal detection with jitter guard:** set `aimLocked = true` when *cumulative* upward movement since the lowest recorded finger point exceeds `reversalThreshold` (default **0.01** screen-heights ≈ ~20 px on a 1080p-class phone). Cumulative-since-lowest-point means micro-jitter (up 2px, down 2px) never latches, but a real upswing latches within a frame or two.
3. **While `aimLocked`:** stop applying lateral finger movement to the targeting line / cone position. Do **not** hide or snap the line — it just freezes at its last value. The frozen value is the aim used for the shot.
4. **Unlatch** (`aimLocked = false`) whenever the swing resets: min-flick-speed failure (Bug 1), slow-release power reset, or any existing path back to the pull-back state.
5. **Interaction with Bug 1:** the release gate and the aim latch are independent. A too-slow flick that latched aim still resets cleanly (latch cleared on reset).

### Inspector params

| Field | Default | Notes |
|---|---|---|
| `reversalThreshold` | 0.01 | screen-heights of cumulative upward travel |
| `debugDisableAimLock` | false | old behavior (line tracks through upswing) stays available |

---

## Acceptance tests (manual, in the Hole 1 test lab)

1. Pull back, hold still, **lift finger straight off** → no shot, swing resets. Repeat 10×, 0 fires.
2. Pull back, flick up normally → shot fires every time (gate must not eat real flicks; if it does, lower `minFlickSpeed`).
3. Trigger a deliberate hitch (e.g. first shot after scene load, or `System.GC.Collect()` in a debug key) then release without flicking → no fire.
4. Pull back, flick up while drifting the finger left/right → ball direction matches where the line pointed **at the bottom of the swing**, and the line visibly stops moving during the upswing.
5. Very slow upward creep → aim latches (line freezes) but shot does NOT fire on release; swing resets and line unfreezes.
6. Both debug toggles on → behavior identical to current build.

---

## Out of scope

Arrow/timing-circle behavior, cone sizing by Club Accuracy, overpower error, fade/draw bend — untouched. This spec only gates the release and freezes the aim line during the upswing.
