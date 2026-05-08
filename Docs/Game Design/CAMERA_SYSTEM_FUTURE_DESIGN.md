# Camera System — Future Design

> **Status:** design notes, not implementation spec. Architect-authored 2026-05-08 after the controls_h iter-1→6 churn. Captures principles for any future shot-camera work so the next person (or next Claude Code session) doesn't repeat the iter-1→5 mistakes.

## What we shipped (iter-6 baseline, 2026-05-08)

The current camera system after `controls_h_chase_camera_regression` is intentionally minimal:

- **One Chase mode runs the entire shot.** Aiming → Flying → Rolling → AtRest. No mid-flight mode transitions.
- **CupZoom** triggers on InCup (terminal). Tweens from current position to hover above cup.
- **OBFreeze** triggers on OB (terminal). Locks at hazard pivot, rotates to track ball.
- **GroundLevel** is a putter-mode framing variant for green-side play.
- **Single-writer rule.** `ChaseCamera.LateUpdate` is the ONLY code that writes `cam.transform.position`. Everything else writes to ChaseCamera's input fields (`SetTarget`, `SetAimDirection`, `ResetToOrigin`).

The previous design included a `Downrange` cinematic cut at 65% carry that snapped the camera to a static position past the landing zone, then released back to Chase at touchdown. **That design is gone.** It produced violent transitions, fought with Chase math during release, and was the root of five iterations of bugs. The enum value `Mode.Downrange` and its handler in ChaseCamera.LateUpdate still exist as dead code — wire-able if a future cut is reintroduced, but no production path routes into it.

## Why iter-1→5 failed (the lesson)

The cinematic cut tried to do TWO things in one mode: (a) a dramatic camera move at landing, and (b) a static framing that held while the ball rolled. Those are different camera contracts that fight each other. Worse, the implementation mixed two writers for `cam.transform.position` (PhysicsLabController.ApplyCameraYaw and ChaseCamera.LateUpdate), which made every fix subtle and every regression invisible.

Five rules came out of that:

1. **One writer per Transform.** If two systems both write `cam.transform.position`, you have a race. ChaseCamera owns it. Future modes write to ChaseCamera's inputs, not the Transform directly.
2. **One job per mode.** A camera mode has one framing contract. "Cut to landing zone" and "track rolling ball" are different jobs and need different modes (or one mode that smoothly evolves between them — see § Hard transitions below).
3. **No mode is allowed to snap.** Every mode change must blend. SmoothDamp's residual at 0.08s smoothTime cannot cover a 30m position jump in any visually pleasant way. Hard cuts require either (a) a dedicated blend window or (b) approach-anticipation.
4. **Visual fidelity ≠ dispatch evidence.** Mode-history captures (`OnModeChanged`) prove dispatch fired. They say nothing about whether the camera VISUALLY did the right thing. Visual verification requires manual play OR position-trace assertions over multiple frames. (Codified as Pipeline Lesson O.)
5. **Cinematic cuts are easy to over-design.** Most golf games don't cinematic-cut at all. The ones that do (PGA Tour broadcast-style) make it a deliberate user-facing toggle, not always-on. Default to "smooth tracking" unless playtests prove cuts add value.

## Hard transitions — how shipped golf games handle them

Most golf games cut from one camera to another. The transition itself is essentially instantaneous — a true cut. What makes it look intentional rather than jarring is the framing context:

- **The destination camera is positioned with the action already in frame.** The new camera doesn't pan to find the ball — it's pre-positioned so the ball appears already in shot when the cut happens.
- **The destination camera is static (or barely moving) at the moment of cut.** It doesn't need to "catch up" to the action.
- **The cut happens at a meaningful event.** Apex of arc, landing, ball-stop — not arbitrary timing. Players read the cut as "the game is showing me this moment because something happened."

The ONLY camera movement that should look like a "follow" is the LAST move from where the camera ends up after the cut to where the ball settles. Everything else is hard cut to pre-positioned framing.

This is the opposite of what iter-1→5 tried. Those iterations attempted to SmoothDamp a Chase-positioned camera to a Downrange-positioned camera over a fraction of a second. The result looks like a violent slide because that's exactly what it is — a slide, dressed up as a follow. Cuts and follows are visually distinct things; conflating them is what produced the "snaps to ground violently" symptom.

## Design principles for any future Downrange-style camera

Before introducing a new camera mode that involves repositioning during a shot, satisfy ALL of these:

### 1. Each mode has one framing contract

Either it follows the ball OR it stays still. Never both. If you want both behaviors during a single shot phase, that's TWO modes with a transition between them, not one mode with branching logic.

### 2. Pre-position before cut

The destination camera writes its target position BEFORE the SM transition fires. By the time mode change happens, the camera Transform is already at the destination — the cut is just "stop computing pos from old mode, start using new mode's pre-set pos." No SmoothDamp glide between modes.

This requires a "stage the next pose" API on ChaseCamera. Something like:

```csharp
public void StagePose(Vector3 pos, Quaternion rot);  // pre-set the next mode's position
public void CommitMode(Mode m);                       // switch mode AND apply staged pose
```

### 3. Cut at a meaningful moment

Landing impact, apex, ball-stop. Never arbitrary time/distance fractions like "65% of carry" — that's where the iter-1 design went wrong. The 65% threshold meant the cut fired in mid-air with the ball flying past the camera, requiring the camera to do something dynamic (catch up to the landing point), which it can't do without becoming a follow-cam, which conflicts with "stay still after cut."

Better triggers:
- **Apex.** Easy to detect (ball Y derivative changes sign). Cut to wide-aerial showing landing zone in frame. Static framing while ball descends.
- **First terrain hit.** Already detected by trajectory. Cut to ground-level near landing point, ball already rolling toward camera.
- **Ball-stop.** Cut to a result-screen-ish framing of the resting position.

### 4. The destination camera does NOT move during the cut moment

If the cut happens at apex, the destination apex camera is STATIC for the descent + landing. Only after the ball has stopped does the camera optionally move to the next framing (and that movement is its own follow phase, with its own contract).

### 5. The follow phase to ball-stop is the ONE allowed movement

After the cut and the ball-stop sequence, the camera can move smoothly to frame the resting ball. This is the "last movement" Cesar identified. It's allowed because (a) the ball has stopped so the camera isn't chasing motion, (b) the movement reads as "settle into rest framing," and (c) there's no concurrent action to fight with.

## Concrete design sketches (for future implementation, NOT for current work)

### Apex Cam (2D-broadcast style)

- Triggers when ball reaches apex (max altitude during flight).
- Camera staged at: behind ball position, far back enough to frame ball + landing area, looking down the flight line.
- Stays static during descent + landing + initial roll.
- After ball-stop (terminal AtRest), follow-cam transitions to Chase framing of the resting ball over ~0.5s.

Why it'd work: the cut itself is from a moving Chase camera to a static apex camera. The destination camera is pre-positioned and static. The only motion during apex-cam is the ball flying through the frame — exactly what you want to see.

### Side Cam (broadcast-broadcast style)

- Triggers at apex OR at first terrain hit (designer's choice per shot type).
- Camera staged at: 90° to flight line, distance proportional to predicted carry, ground level.
- Static framing — ball flies across frame, camera doesn't move.
- After ball-stop, follow-cam transitions to Chase.

Why it'd work: same as apex cam. Static destination. Action moves through frame, not camera.

### Hole Overview Cam (intro-style)

- Triggers on hole load (no shot yet).
- Camera staged: high aerial over centerline of hole, slowly orbiting to show tee → green relationship.
- Plays for ~3 seconds, then cuts to default Chase framing for first shot.

Why it'd work: this is the "cinematic" the iter-1→5 attempt actually wanted. It's appropriate for hole-load (not in the middle of a shot), it doesn't fight with active gameplay framing, and it sets player context.

## What NOT to design

- **A unified "smart camera" that decides framing dynamically based on shot type.** This was the iter-1→5 trap. It's unbounded complexity; every shot is a new edge case. Mode-per-context is bounded; the user can predict what the camera will do.
- **Mid-flight cinematic cuts that release back to Chase.** Cesar's instinct on iter-5 was right — releasing from a static cinematic to a follow-cam is the worst combination of both contracts. Either commit to a static framing for the rest of the shot, or don't cut at all.
- **Cinematic transitions that depend on tuning a SmoothDamp time and a target position simultaneously.** Three free variables (transition duration, source pose, dest pose) with no failure mode that's easy to reason about. Pick your transitions to be either instant cuts (zero duration) or dedicated blend windows (fixed pre-tuned durations with intermediate poses).

## What to design first if cameras get touched again

1. **Apex Cam, default OFF.** Ship the staging API in ChaseCamera, ship the apex detection in LoopCameraDirector, but leave the mode behind a SerializeField bool. Cesar toggles it on, plays it, decides. If it feels good, default to ON. If it feels gimmicky, leave it off and the API stays available for OBFreeze/CupZoom-style modes.

2. **Hole Overview Cam.** Plays during hole load before first Aiming. Lowest-risk addition because it's NOT in the middle of a shot — no concurrent action to conflict with.

3. **Optional follow-cam zoom-out at apex.** Without a true cut. Chase mode dynamically pulls back as ball altitude rises (`effectiveDistance = baseDistance * (1 + altitudeAboveOrigin/30m)`). Continuous, no mode switch. This is what Cesar requested in chat as "Chase with zooms" but I deferred it to keep iter-6 bulletproof. Lowest-risk camera enhancement.

If any of these need code, they go in their own SPEC. Reference this doc. Cite the relevant § principles.

## Cross-references

- **Iter-1→6 history:** `Docs/Specs/Active/controls_h_chase_camera_regression/`
- **Single-writer fix:** `SPEC_ITER6_AMENDMENT.md` in the same folder
- **Pipeline Lesson O** (dispatch ≠ visual evidence): `Docs/Diagnostics/PIPELINE_LESSONS.md`
- **§2b camera transitions origin:** `Docs/Specs/Completed/loop_v1_2b_camera_transitions/SPEC.md`
- **OBFreeze framing question** (forward flag): in `Docs/TellCode.md` under the §2b deferred-flag block

## Maintenance

Update this doc whenever:
- A new camera mode is shipped — add a § describing what it does and which design principle it satisfies.
- A camera mode is REMOVED or significantly redesigned — add a "lesson learned" line in the relevant principle section.
- A future iteration discovers a new failure mode the principles missed — write it down.

Don't let this doc rot. The whole point is that the next person to touch cameras reads it BEFORE making the same mistakes.

## Research note 2026-05-08 — what shipped golf games actually do

Researched after Cesar asked about apex zoom-out post-iter-8. Findings inform why apex zoom-out was rejected and validate the Apex Cam (hard cut, not zoom) approach in § "Concrete design sketches" above.

### PGA TOUR (EA, recent titles) — three modes the player toggles between

From EA forums player commentary on PGA TOUR camera modes:
- **Follow camera:** always tracks ball, no cuts. Closest to what GOLFIN currently has post-iter-8.
- **Pro camera:** tracks at first, then HARD CUTS to a downrange camera near landing. Players complained the cut happens at ~95% of travel — want it at 70–75% to better see the finish. Player feedback: when the cut moment is right, this mode is preferred.
- **Broadcast camera:** TV-style with multiple cuts. Players reported it "flat out feels broken" — cuts to obscured/distant views often. Cautionary tale: complex camera systems are easy to get wrong.

**Player sentiment:** "Follow takes immersion away too soon" — implies serious players prefer the discrete cut over continuous tracking.

### PGA TOUR 2K23 — player-triggered cuts

2K23 lets the player press spacebar mid-flight to cut to a ball-cam view. Without spacebar, default is continuous follow. **The player decides when to cut.** This is widely praised in player communities.

Design implication: a default-OFF Apex Cam toggle (per item #1 in § "What to design first") aligns with this proven pattern. Player opt-in beats designer-imposed cinematic.

### TV broadcast — multiple physical cameras, hard cuts

From Quora answers on televised golf coverage: TV uses multiple positioned cameras with skilled operators. The director cuts between them at meaningful moments (tee-off, mid-flight establishing shot, near-green, putting). Each camera is a fixed framing; movement during a take is minor (slow zoom or pan, not full repositioning).

Design implication: real golf production NEVER does a continuous zoom-out during flight. They use cuts. The Apex Cam sketch in § "Concrete design sketches" matches this.

### Why apex zoom-out (continuous Chase pulls back as altitude rises) was REJECTED

Architect proposed apex zoom-out 2026-05-08 as a way to give Cesar a "Chase with proposed zooms" enhancement. Research showed this pattern doesn't exist in shipped golf games. Continuous tracking at varying distance is neither what TV broadcast does nor what player-favored game modes do. The pattern that works is hard-cut to a pre-positioned framing — which is exactly what the Future Design doc already specified.

Cesar's call 2026-05-08 ~14:30 JST: don't ship apex zoom-out. Keep cameras as they are post-iter-8. If apex moment is ever added, do it as Apex Cam (hard cut) per item #1 in § "What to design first."

### Three takeaways for future camera work

1. **Default to continuous follow.** It's the right default for casual mobile audiences and minimizes design risk. PGA TOUR's "Follow" mode and Mario Golf's tracking cam both work fine in this register.
2. **Add discrete modes via opt-in toggles.** When you want cinematic, ship it as a player-toggleable setting (Apex Cam, Broadcast Cam) not as automatic mid-flight behavior. The PGA TOUR 2K23 spacebar pattern is the model.
3. **Never compromise between continuous and discrete.** "Smoothly zoom out" or "smoothly cut" or "track-then-cut-then-track" is exactly the kind of compromise that ate iter-1–6. Pick one paradigm per mode. Modes are switchable; modes are not blendable.
