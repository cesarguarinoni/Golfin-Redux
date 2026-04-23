# Shot Controls — Design Document

**Status:** Active design (v1)
**Owner:** Cesar (design) / Claude Architect (spec) / Claude Code (impl)
**Last Updated:** 2026-04-23
**Replaces:** `Oringal Shot Controls.docx` (kept for reference); supersedes the Old Control Fixes proposals

---

## 1. Pillars

1. **Flick-based.** No three-click meter. The player drags the club back from the ball and flicks up through it.
2. **Stat-driven, not luck-driven.** Every randomness source is bounded and visibly tied to a club / ball / character stat. The Old Control Fixes doc's recurring complaint — "players don't understand why the ball didn't fly as they wished" — is the failure mode this design exists to prevent.
3. **Cone, not circle.** The aim/error surface is a screen-anchored semi-cone at the bottom of the screen. The ball is rendered in world space at its 3D position. The cone is UI scaffolding for the gesture, not a world object.
4. **Mobile-first, single-finger.** All shot input is single-finger drag-and-flick. Camera rotation is a separate two-finger gesture (pinch/twist). No on-screen virtual joysticks.
5. **Same controller for swing and putt; different mode.** The state machine is shared; constants and visible subsystems differ. We can split into two controllers later if it gets messy.

---

## 2. Visual layout

Screen-anchored, bottom-center. Reference: `Docs/Game Design/In-Game - Shot Tests 5–9.png`.

```
                  [pin]
                    |       <-- targeting line, world-space, anchored at ball
                  [ball]    <-- world-space, 3D
                    .
                    .       <-- (gap; ball lives in world, cone lives in HUD)
                    .
              ___________
             /           \  <-- semi-cone outline, fixed bottom-center
            /  ↑    ↑    \
           /  arrow arrow \  <-- timing arrows traveling UP toward the apex
          /               \
         /     [club]      \  <-- player's drag handle (trapezoid)
        /___________________\
```

**Anchoring summary:**

| Element | Coordinate space | Notes |
|---|---|---|
| Ball | World (3D) | Wherever it lies on the course |
| Targeting line | World | Originates at ball, points along aim heading toward pin / target |
| Pin marker, distance HUD | World→screen overlay | Existing pattern from current screens |
| Cone outline | Screen, fixed bottom-center | Width scales with `Club Accuracy` stat; never moves with camera |
| Club trapezoid | Screen, inside cone | Player's drag handle |
| Timing arrows | Screen, inside cone | Travel up toward cone apex |
| Power % / yards HUD | Screen, top-right | Live readout |

**Why screen-anchored:** Mobile-first ergonomics. Thumb-comfort zone. The cone has nothing to do with the world; it's a gesture surface. World-anchoring it on the ball would require constant repositioning as the camera moves and would shrink/grow with depth.

---

## 3. Input layer (single-finger gesture)

### 3.1 State machine

| State | Enter on | Exit to | Player sees |
|---|---|---|---|
| `Idle` | Shot resolved / scene start | `Aiming` on touch-down inside the touch-receptive area | Cone ghosted (low alpha), visible but de-emphasized |
| `Aiming` | Touch-down inside touch-receptive area | `Pulling` on downward drag past `PullStartThresholdPx`; `Idle` on touch-up | Cone fades in to full opacity (Q15c); no arrows yet; club trapezoid at neutral |
| `Pulling` | Drag past pull threshold | `Timing` on continuous downward drag (immediate); `Idle` on touch-up before threshold | Power HUD live, yard estimate live, club trapezoid moves with finger |
| `Timing` | Pull is committed (`flickMagnitude01 > 0.05`) | `Flicking` on flick-up past `FlickVelocityThresholdPxPerSec`; `Idle` on touch-up below threshold (cancel) | Arrows spawn and travel up the cone; pass count tracked |
| `Flicking` | Flick velocity threshold crossed upward | `Resolving` (immediate, same frame) | Snapshot all state; trigger sim |
| `Resolving` | Flick complete | `Idle` when ball comes to rest | Trajectory plays; input locked |

**Key design rules:**

- **Camera is locked from `Pulling` onward.** Player can rotate freely in `Idle` and `Aiming`. The moment they pull, camera is frozen until `Resolving` completes. Prevents accidental aim drift mid-swing.
- **Cancel = lift before flick threshold.** Costs no turn. Re-arms `Idle`. No undo cost, no animation.
- **Slow-finger-up resets to 0%.** As in the original spec — prevents cheating by dragging up slowly to bleed off power.

### 3.1.1 Touch-receptive area (Q14)

Two overlapping zones; touching either triggers `Aiming`:

1. **Ball sprite hit circle.** A fixed-radius circle at the ball's screen-projected position. Radius = `BallHitZoneRadiusPx` (default 80px). The hit circle is the player's selected ball sprite — visible, tappable, intuitive. Independent of camera distance (always `BallHitZoneRadiusPx` in screen pixels regardless of how far the camera is from the ball).
2. **Bottom-half fallback.** The entire bottom half of the screen (below screen-Y midpoint) is also a valid touch-down zone. Catches mis-taps when the ball sprite is small or partially occluded. The cone lives down here anyway, so the fallback aligns with where the player's thumb naturally rests.

If the player touches in the upper half AND outside the ball hit circle, the touch is treated as camera input (or ignored), not as a shot input.

### 3.1.2 Cone visibility (Q15c — fade in)

- **`Idle`:** Cone is rendered at low alpha (`ConeIdleAlpha`, default 0.25). Tells the player where the affordance lives. Tunable to balance discoverability vs. visual clutter.
- **`Aiming`:** Cone fades from `ConeIdleAlpha` to 1.0 over `ConeFadeInSeconds` (default 0.15s). Tunable during testing.
- **`Pulling` / `Timing` / `Flicking`:** Cone at full opacity.
- **`Resolving`:** Cone fades out over `ConeFadeOutSeconds` (default 0.3s) as the ball flies.

### 3.1.3 Targeting line (Q16b — fixed-length forward)

- **In-game shot screen:** A short line of fixed length `TargetingLineLengthMeters` (default 30m / ~33yd) projects forward from the ball along the current aim heading. Updates live as the player rotates the camera or fine-tunes within the cone. Bends for fade/draw mode.
- **Map screen (out of v1 scope):** Full-length aim from ball to pin (or to the player's chosen drop target). The map view is the place for full-route visualization.
- **Rationale:** Showing predicted carry-distance live (option Q16c) would let players read distance off the line, removing the skill of estimating power for the club. This is a competitive game; that estimation is part of the skill ceiling. The fixed forward line shows direction only.

### 3.2 Power gauge (the pull)

`flickMagnitude01` = vertical distance pulled from touch-down origin, normalized.

| Pull distance | Power |
|---|---|
| 0 → `MinUsefulPullPx` | 0% (shot won't register) |
| `MinUsefulPullPx` → `Max100PercentPullPx` | linear 0%→100% |
| `Max100PercentPullPx` → `MaxOverpowerPullPx` | linear 100%→120% (overpower zone) |
| Past `MaxOverpowerPullPx` | clamped at 120%; visual feedback that you've maxed |

Overpower forgiveness already lives in `ShotInputBuilder.Build()`:
- High Strength character → effective overshoot is reduced (existing `OverpowerForgivenessFraction` resolver). Hard clamp at 1.2× regardless.
- Low Strength character → full overshoot penalty applied as worse aim error and reduced timing tolerance.

### 3.3 Aim within the cone

Two parts to the aim:

1. **Camera heading.** Coarse aim. Player rotates the camera (two-finger gesture) before touch-down. Defaults to ball→pin straight line at scene entry (decision Q5a).
2. **Cone fine-tune.** Lateral position of the club trapezoid inside the cone rotates the aim heading around the ball.

**Cone width = `Club Accuracy` stat:**
- Low accuracy → narrow cone → narrow rotation range (e.g. ±5°). Less fine control AND less safe-zone buffer.
- High accuracy → wide cone → wide rotation range (e.g. ±20°). More fine control AND wider safe zone.

**Final aim yaw at flick:**
```
finalAimYaw = cameraHeading + coneFinetune + flickDeviation + perPassDegradation
```
Where:
- `coneFinetune` = club trapezoid X-position in cone, mapped to ±halfConeAngle
- `flickDeviation` = angle between flick vector and cone center axis (off-axis flick adds error)
- `perPassDegradation` = RNG-seeded ± up to `(passIndex - cleanPasses) * DegradationYawDegPerPass`

### 3.4 Timing arrows

Arrows travel up the cone. The player flicks when an arrow reaches the apex (= "perfect timing"). Off-time flicks reduce power (existing physics already supports a `flickMagnitude01 < 1.0` reduction).

**Pass model (decision Q2b — multi-pass with degradation):**

- First N passes are "clean" — no accuracy degradation. N scales with Club Control: `N = MaxCleanPassesAtCC0 + (charClubControl * CleanPassesPerCC)`, e.g. 1 pass at CC0 → 5 passes at CC100.
- Pass N+1 onward adds `DegradationYawDegPerPass` to the aim error each pass.
- After `MaxTotalPasses` (e.g. 10), the shot resets and the cone hides — player must re-pull. No turn lost.

Arrow speed scales with Club Control (faster CC → faster arrows, but also more passes; intentional that high-CC players have a busier window — they have margin to spare).

### 3.5 Spin (Pre-stage modal — out of v1 scope, kept as-is)

Existing pattern. Spin button opens a ball-impact-point picker. Selected impact point feeds the spin axis at `ShotInputBuilder.Build` time. Build already accepts a `SpinState`. We hand off to whatever the existing Spin modal produces; if it's not yet wired, default to neutral backspin.

### 3.6 Cancel & re-arm

Lifting the finger in any state before `Flicking` cancels:
- No simulation runs.
- Power gauge resets.
- Cone re-shows in neutral position.
- Player can immediately retry. No turn consumed.

If the player is in `Timing` and no flick happens before `MaxTotalPasses` arrows complete, the shot auto-cancels back to `Idle`.

---

## 4. Putt mode (Q8 decision: same controller, mode flag)

When `IsPutt == true`:

- **No overpower.** Hard clamp at 1.0×. Pulling past `Max100PercentPullPx` does nothing extra.
- **No spin.** `SpinState.None`. Spin button hidden.
- **No fade/draw.** Shot mode hidden, always Straight.
- **Slower power curve.** Putter has a much shorter `Max100PercentPullPx` (or equivalently, a `BaseVelocityMps` of ~5 instead of ~75) so a 100% pull is a 30 m putt, not a 250 m drive.
- **Slower arrow speed.** Putts are about precision, not rhythm. Arrows are slower at all CC levels.
- **No accuracy degradation per pass** (or much milder). Putts should feel calmer.
- **Camera always behind ball, ground-level.** Existing convention. Camera lock from `Pulling` still applies.

If the controller architecture starts straining under the mode flag, split into `PuttController`. We agreed to be prepared to.

---

## 5. Default fallbacks (for development before inventory is wired)

When `BagManager.GetEquippedClub()` or `CharacterManager.GetActive()` returns null:

- Club → `ClubStats.DefaultDriver` (PGA Tour values from `clubs.csv`: 75 m/s, 2686 rpm, 10.9°, Power 50, Accuracy 50)
- Putter → `PutterStats.DefaultPutter` (5 m/s, 4°, Control 50, Accuracy 50)
- Ball → `BallStats.Neutral` (already exists)
- Character → `CharacterStats.Neutral` (already exists)

`DefaultStatProvider` is the single seam between gameplay and inventory. Gameplay never touches BagManager / CharacterManager directly.

---

## 6. Stat → behavior map

Authoritative list. If a stat doesn't appear here, it doesn't affect the shot input layer (it may still affect physics via `StatModifierResolver`).

| Stat | Affects shot input |
|---|---|
| Character Strength | Overpower forgiveness (existing resolver); affects how punishing >100% pulls are |
| Character Club Control | Number of "clean" arrow passes; arrow speed |
| Club Accuracy | Cone width (= aim rotation range AND error tolerance) |
| Club Power | Base velocity (existing `BaseVelocityMps`) |
| Ball Power / Rebound / WindCut / Roll / Spin | Physics-side, not input-side |
| Character Recovery / Stamina | Existing stamina system; not input-side |

---

## 7. Tunable constants

All in `Assets/Resources/Gameplay/controls.csv` (new) — loader follows the existing `PhysicsConfigLoader` pattern.

```
key,value,notes
PullStartThresholdPx,30,minimum drag to enter Pulling
MinUsefulPullPx,40,below this, power is 0%
Max100PercentPullPx,300,pull distance for full 100% power
MaxOverpowerPullPx,360,pull distance for clamped 120% overpower
FlickVelocityThresholdPxPerSec,1500,minimum upward flick velocity to commit
FlickAngleDeviationMaxDeg,30,beyond this, flick reads as outside-cone
ConeHalfAngleAtAcc0Deg,5,cone half-angle for Accuracy=0 club
ConeHalfAngleAtAcc100Deg,20,cone half-angle for Accuracy=100 club
ConeIdleAlpha,0.25,cone opacity in Idle state (ghosted)
ConeFadeInSeconds,0.15,fade-in duration on entering Aiming
ConeFadeOutSeconds,0.30,fade-out duration on entering Resolving
BallHitZoneRadiusPx,80,fixed-pixel radius around ball sprite for touch input
TargetingLineLengthMeters,30,length of the in-game targeting line
BaseArrowSpeedHzAtCC0,0.5,arrow cycles per second at ClubControl=0
ArrowSpeedHzPerCC,0.025,additive cycles/sec per CC point
MaxCleanPassesAtCC0,1,passes before degradation at CC=0
CleanPassesPerCC,0.04,additive passes per CC point (CC=100 → 5 passes)
MaxTotalPasses,10,total before auto-cancel
DegradationYawDegPerPass,2,aim error widening per pass past clean
PuttArrowSpeedMultiplier,0.5,putts get slower arrows
PuttBaseVelocityMps,5,override default putter velocity
```

These are the seed values. Code's iteration budget says ±2 attempts to get them feeling right before flagging for design re-tune.

---

## 8. Debug toggles (per Old Control Fixes convention)

Every new mechanic ships with a debug-off toggle so we can A/B during playtest. Exposed in the lab UI:

- Show cone outline (visual debug)
- Show arrow trail
- Cancel-on-slow-flick on/off
- Single-pass mode (skip degradation system)
- Disable overpower (clamp at 100%)
- Disable cone fine-tune (aim is camera-only)
- Force-perfect timing
- Force-perfect aim

These exist to isolate which mechanic is making a shot feel bad. Default state: all "real" toggles on; debug-helpers off.

---

## 9. Test integration

**v1 lives in `PhysicsLab_Hole1`.**

- `LabRoot` gets a new `ShotController` MonoBehaviour alongside the existing `PhysicsLabController`.
- A new `ShotConeView` UI canvas is added as a child of `LabRoot` (uGUI, bottom-anchored).
- Existing preset-based "Fire" button stays as `[Debug] Fire Preset` for regression.
- New live-touch path is the default — touch the ball, drag, flick.
- Hole 1 lab already has `SceneGroundProvider` and `SceneSurfaceProvider` wired; the ShotController plugs in at the same point as existing `Fire(preset)`.

Code owns the scene + prefab edits via Unity-MCP (`gameobject-create`, `gameobject-component-add`, `scene-save`). Cesar takes over for any aesthetic / styling pass after the functional flow works.

---

## 10. Out-of-scope for v1 (deferred polish)

- **Fade/draw curve preview.** Mode toggle works (Straight/Fade/Draw text), curve math feeds physics, but no aim-time bent-line preview rendered. Add in v2.
- **Overpower visual feedback.** Functional only — no club shake, no cone red flash. Add in v2.
- **Multi-club switching mid-shot.** Assume "current club" comes from BagManager; no in-shot swap UI yet.
- **Map-screen aim handoff.** Camera defaults to ball→pin. Map screen integration is a separate task.
- **Spin modal redesign.** Use existing pattern; redesign is its own future task.
- **Mow stripes / lie-aware aim assist.** Surface coupling exists in physics; no UI hint yet.

---

## 11. Open questions for future design passes

These don't block v1 but should be revisited:

1. **Move-the-cone gesture.** Skipped for v1 (Q8). If playtest shows the camera-only aim feels too coarse, add a lateral cone-drag gesture that rotates the cone (not the camera).
2. **Two-cone visual** (fixed inner aim region + stat-scaled outer error region). Skipped for v1 since cone-affects-both is simpler. Revisit if "wider cone = harder fine aim" complaint surfaces.
3. **Per-club default speeds.** Currently using `clubs.csv` PGA Tour values. Real progression-tuned club CSV is its own task.
4. **Fade/draw curve preview** (see §10).
5. **Spin modal UX redesign.**

---

## 12. Glossary

- **Cone** — the screen-anchored semi-cone UI element at bottom-center of the shot screen. Width = `Club Accuracy`.
- **Club trapezoid** — the player's drag handle inside the cone. Visualized as the clubhead.
- **Pull** — the downward drag from the ball/touch-origin that builds power.
- **Flick** — the upward release that commits the shot.
- **Pass** — one complete travel of an arrow up the cone toward the apex.
- **Clean pass** — a pass within the no-degradation budget (`MaxCleanPasses`).
- **Overpower** — pull past 100%, up to clamped 120%; controlled by Strength.
- **Cone fine-tune** — lateral position of the club inside the cone, rotates aim around the ball.

---

## 13. References

- `Docs/Game Design/Oringal Shot Controls.docx` — original 2024-09-17 design (superseded but useful for context)
- `Docs/Game Design/Old Control Fixes.docx` — issue list from playtests (most addressed by this design)
- `Docs/Game Design/New Controls.docx` — Cesar's notes that fed this doc
- `Docs/Game Design/In-Game - Shot Tests 5–9.png` — visual mockups
- `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` — the contract this design plugs into
- `Assets/Resources/Physics/clubs.csv` — base club values
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
