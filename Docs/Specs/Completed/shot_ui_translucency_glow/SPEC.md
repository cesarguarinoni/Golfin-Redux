# SPEC — `shot_ui_translucency_glow`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-07 (Architect). Decisions locked with Cesar 2026-08-07:
glow re-arms on every idle state at tee; ball alpha syncs to the cone's alpha at runtime (single source of truth).

## Goal

Two visual/UX changes to the shot UI, minimal diff, no input/physics changes:

**A — Translucency swap.** The club handle becomes the fully-opaque element (it is what the
player grabs); the ball becomes translucent, matching the cone's alpha at all times. Today it
is the other way around.

**B — Tee-off idle glow.** If the player has not started dragging the handle within 5 s
(tunable) on a **tee shot**, the club handle pulses a gold glow as a "grab this" hint.
Touching any other button resets the countdown; an open modal pauses it (count restarts from
0 on close); the glow re-arms after every swing reset and disarms once the shot fires.
Non-tee strokes never glow.

## Reference

- **Figma frame:** N/A — behavior spec, no new layout. No pixel-fidelity table needed beyond §Fidelity below.
- **Mockups:** `InGame Shot Tests 5/9.png` in the Claude project (Architect archive) show the handle/ball elements; they are Figma mockups, not runtime truth.
- **reference/**: empty by design.

## Fidelity (every element the task touches)

| Element | Object / class | Expected end state |
|---|---|---|
| Club handle | `_clubHandle` RectTransform on `ShotConeView` (dragger: `ClubHandleDragger`, sprite: `ClubHandleSpriteBinder`) | Renders at **alpha 1.0** in all pre-shot states, including Idle where the cone group sits at `ConeIdleAlpha` (0.25). Position/size/scale behavior unchanged. |
| Ball | `CentralBallWidget` (`_image`) | Base alpha **always equals the cone root CanvasGroup's current alpha** (the one `ConeAlphaController` drives): 0.25 at Idle, lerping to 1 during Aiming/Pulling/Timing/Flicking, 0 while Resolving — automatically, because it mirrors the group value each frame. Sprite-selection logic (`RefreshSprite`) untouched. |
| Cone | `ConeMeshGraphic` + `ConeAlphaController` | UNCHANGED. `ConeAlphaController` remains the single writer of the cone group alpha. |
| Glow | New child GO under `_clubHandle` | Gold pulsing halo behind the handle sprite, tee-idle only. `raycastTarget = false`. Never shifts the handle's layout. |

## Architecture context

- **Asmdef boundaries affected:** `Golfin.Gameplay.UI` (all new code lives here). Reads
  `Golfin.Gameplay.Input` (`ShotController`, `ShotState`) and `Golfin.Gameplay.Session`
  (`GameSession.TurnCount`) — verify the asmdef already references Session; add the reference
  if not (Gameplay.UI widgets like `TurnBannerWidget` already display TurnCount, so it almost
  certainly does).
- **Existing code referenced:**
  - `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — owns `_clubHandle`
  - `Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs` — drives cone `CanvasGroup.alpha` from `ShotState`
  - `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` — ball `Image`
  - `Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs` — `OnPointerDown`/`OnPointerUp` (drag start/stop signals)
  - `Assets/Scripts/Gameplay/UI/ShotUI/OtherButtonsFader.cs` — `static bool AnyOverlayOpen` (selector overlays)
  - `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` — `IsOpen` (map modal)
  - `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` — `TurnCount` (== 1 → tee shot)
  - `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` — `ConeIdleAlpha` etc.
- **Manager APIs used:** `ShotController.OnStateChanged (Action<ShotInputState>)`, `ShotController.IsExternalDragActive`.

## Implementation

### Part A — translucency swap

1. **Diagnose first (5 min):** confirm where the handle's current translucency comes from.
   Expected: `_clubHandle` is a child of the cone root whose `CanvasGroup` `ConeAlphaController`
   drives, so the handle inherits the 0.25 idle alpha. If instead the handle `Image` has an
   authored alpha < 1, note it in the report — the fix below covers both.
2. **Handle → 100%:** add a `CanvasGroup` to the `_clubHandle` GameObject with
   `ignoreParentGroups = true`, `alpha = 1` (and `interactable`/`blocksRaycasts` true so
   `ClubHandleDragger` keeps receiving events). If the handle Image itself carries an authored
   alpha, set it to 1. **Do not** reparent the handle — no hierarchy rebuilds.
3. **Ball → cone alpha:** new tiny component `BallConeAlphaMirror` (namespace
   `Golfin.Gameplay.UI.ShotUI`) on the CentralBall GameObject:
   - Serialized ref to the cone root `CanvasGroup` (same one `ConeAlphaController` writes).
   - In `LateUpdate`: `_image.color = new Color(r, g, b, _coneGroup.alpha * _baseAlpha)` —
     multiplicative on the Image's authored RGB, so any future tint/effect on the ball survives.
     `_baseAlpha` serialized, default 1.
   - Reads the group value; NEVER writes it. `ConeAlphaController` stays the single writer.
4. **Debug toggle:** `debugLegacyTranslucency` (Inspector, on `BallConeAlphaMirror` or a small
   shared home — implementer's choice, but ONE flag): when true, handle CanvasGroup
   `ignoreParentGroups = false` and the mirror stops driving the ball alpha (restores authored
   values). Old look must be fully recoverable at runtime.

### Part B — tee idle glow

New component `TeeIdleGlowController` (namespace `Golfin.Gameplay.UI.ShotUI`), attached to the
`_clubHandle` GameObject (wired in the same builder/prefab pass as Part A).

**Glow visual.** Child GO `"HandleGlow"` created in `Awake` (or authored in prefab —
match how the rest of this hierarchy is built): `Image` using the handle's current sprite
(read from the handle's `Image` each time the glow starts, so club switches stay correct),
`raycastTarget = false`, rendered BEHIND the handle sprite (sibling index 0), gold tint,
animated `localScale` 1.0→1.12 and alpha 0.35→0.8, ping-pong sine, period `glowPulsePeriod`,
**unscaled time**. Fade-out ≤ 0.15 s when stopping. Check first whether an existing glow/
outline effect is reusable (rarity glow, button highlight) — reuse over new code if one fits.

**State machine (all checks in `Update`, unscaled dt):**

```
armed   = (GameSession.TurnCount == 1)            // tee shot
          && state == ShotState.Idle              // from ShotController.OnStateChanged
          && !dragging                            // ClubHandleDragger pointer-down..up
          && shotUIVisible                        // same visibility the handle itself has
modal   = OtherButtonsFader.AnyOverlayOpen
          || mapView.IsOpen                       // serialized MapViewController ref
          || <settings/other modal — see NOTE>
if (!armed)      { idleTimer = 0; StopGlow(); return; }
if (modal)       { idleTimer = 0; StopGlow(); return; }   // pause == held at 0 → restarts on close
idleTimer += unscaledDeltaTime;
if (idleTimer >= idleGlowDelay) StartGlow();
```

- `GameSession.TurnCount == 1`: NOTE — verify increment timing (whether TurnCount advances at
  shot commit or at ball-rest). The gate must be true from hole start until the first stroke
  FIRES. If TurnCount only advances at ball-rest, additionally require no entry in
  `GameSession.ShotHistory` for this hole (`ShotHistory.Count == 0`).
- **Drag hooks:** `ClubHandleDragger.OnPointerDown` → notify controller (`OnHandleTouched()`:
  stop glow + zero timer). Cleanest wiring: a `[SerializeField] TeeIdleGlowController` on the
  dragger, null-safe — labs/tests without the glow keep working. `OnPointerUp` needs no hook;
  the state machine re-arms from `ShotState` (swing reset → back to Idle → timer restarts —
  the "re-arm every idle" decision).
- **Other-button reset:** any pointer-down on a HUD action button (Spin, Fade/Draw, Ball,
  Club, map, settings) → `TeeIdleGlowController.NotifyOtherInteraction()` (public static,
  null-safe against no live instance; instance registers itself in `OnEnable`). Call it from
  the shared action-button pointer path — NOTE: `ActionButtonWidget` / `ActionButtonsRoot`
  look like the shared home; if there is no single shared handler, add the one-line call per
  widget rather than inventing a new event bus. Buttons that OPEN modals need no special
  case — the modal branch already holds the timer at 0 until close.
- **Shot fires** (`Timing`/`Flicking`/`Resolving`): armed goes false → glow off, timer 0.
  Next tee (next hole, `ResetForNewHole` → TurnCount 1) re-arms naturally.

**Config:** `idleGlowDelay` default **5.0 s**, `glowPulsePeriod` default **1.2 s**,
`glowColor` default `#FFC94A` (tune), `debugDisableIdleGlow` default false — all serialized
on `TeeIdleGlowController`. Follow the `controls.csv` mirroring convention ONLY if trivially
cheap; Inspector-only is acceptable for v1 (flag in report either way).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item marked `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] Idle at tee: handle visibly 100% opaque while cone + ball sit at `ConeIdleAlpha` (0.25) — screenshot
- [ ] Pull to 50% power: ball alpha has risen with the cone (both →1 during Aiming/Pulling) — screenshot or alpha log
- [ ] Ball sprite-selection fallback chain (`BallContext` → `_defaultThumbnail` → Resources) untouched and working
- [ ] Tee shot, no input: glow starts pulsing at `idleGlowDelay` (measure ±0.5 s)
- [ ] Tap Spin at t≈3 s: no glow at t=5 s; glow appears ≈5 s after the tap
- [ ] Open club selector at t≈4.9 s: no glow while open; glow ≈5 s after close (modal pause+restart)
- [ ] Grab handle mid-glow: glow fades ≤0.15 s; release with no power (swing reset): glow returns after 5 s idle
- [ ] Fire the tee shot; stroke 2 idle 10+ s: NO glow. Next hole tee: glow works again
- [ ] Putter tee stroke (if reachable): glow behavior consistent with the tee gate (TurnCount==1 only)
- [ ] Glow never blocks input: handle drag starts on first touch even while pulsing (`raycastTarget=false` verified)
- [ ] `debugLegacyTranslucency` → old look restored; `debugDisableIdleGlow` → no glow ever; both on → current-build behavior
- [ ] Bot/versus path (`BotDriver` `BeginExternalDrag` — no pointer events): no glow flicker, no NRE with no `TeeIdleGlowController` instance
- [ ] No white-box placeholders visible in the screenshot
- [ ] All `[SerializeField]` references wired in the Inspector
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Scripts/Gameplay/UI/ShotUI/BallConeAlphaMirror.cs` — NEW
- `Assets/Scripts/Gameplay/UI/ShotUI/TeeIdleGlowController.cs` — NEW
- `Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs` — +1 serialized ref, +1 call in `OnPointerDown`
- Action-button pointer path (`ActionButtonWidget` or per-widget) — +`NotifyOtherInteraction()` calls
- CentralBall GO + ClubHandle GO — component additions (builder or prefab, wherever these are constructed)
- NO changes to `ConeAlphaController`, `ShotController`, `ShotConeView` beyond what is listed

## Smoke evidence

Editor Hole 1 test lab run-through of the acceptance list above, plus screenshots at:
idle-tee (swap visible), mid-pull (ball opaque with cone), glow active. Timer behavior is
player-perceived → per Lesson O this needs human-in-the-loop play-and-confirm in the
IMPLEMENTER_REPORT (describe what the glow visibly did for tests 4–8), not just state logs.

## Out of scope (do NOT do these)

- Flick gate / aim lock / cone sizing / arrow timing (see `SHOT_FLICK_FIX_SPEC` — shipped, untouched)
- Any glow on non-tee strokes, putter-specific glow variants
- Reparenting/rebuilding the shot UI hierarchy
- controls.csv schema work beyond (optionally) the four glow params
- Character ghosting / putt translucency (separate Confluence topic)
