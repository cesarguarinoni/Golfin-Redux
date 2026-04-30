# 8.5.D — Central Ball Sprite + Always-On TargetingLine

> **Tier 3.** Visual fidelity + spatial math (line pivots with camera + drag).
> **Created:** 2026-04-30 17:10 JST
> **Owner:** golfin-implementer → self-reviewer → architect
> **Depends on:** 8.5.B (BallContext seeded), 8.5.C (selectors)
> **Reference:** Figma file `5gEAHjl6xAtW8iYY7NMvWd`, frame `In-Game - Shot Tests 10` (`12941:7178`)

---

## Two changes

### 1. TargetingLine — always on AND continuously updated

The line math in `ShotConeView.UpdateTargetingLine` is correct (pivots from `state.AimYawRadians`), but **`_aimYawRadians` in `ShotController` is only computed at fire time** (`CommitFlick`, line 208). During Idle/Aiming/Pulling/Timing it stays at 0, so the published `ShotInputState.AimYawRadians` never reflects live aim. The line appears static.

**Fix:** compute `_aimYawRadians` continuously in `PublishState` (or Tick) so the published state carries live aim every frame, not just at commit. Then add `Idle` to the show-state list in `ShotConeView.UpdateTargetingLine`.

### 2. Central ball — new 2D sprite

A 2D UI Image at a **fixed UI anchor** (Figma position), showing the selected ball's full sprite. Decoupled from world ball position. Visible in the same states as the line.

**Note:** future game-camera work may switch this to track the world ball's screen position. For now (lab + current camera setup), fixed UI anchor matches the Figma layout and avoids breakage when camera doesn't center on ball.

---

## File changes

### A. `ShotController.cs` — compute aim continuously

Currently `_aimYawRadians` is reset to 0 in `TransitionToIdle` and only assigned inside `CommitFlick`. Live aim is never published.

In `PublishState()` (line ~310), compute the live aim before invoking the event:

```csharp
private void PublishState()
{
    if (OnStateChanged == null) return;
    float cc = GetStatBundle().Character.ClubControl;
    int cleanPasses = Mathf.RoundToInt(_config.MaxCleanPassesAtCC0 + cc * _config.CleanPassesPerCC);

    // NEW: compute live aim every frame so the targeting line and any aim-driven UI
    // can pivot during Idle/Aiming/Pulling/Timing. Final committed aim still uses
    // the same formula at CommitFlick (which adds degradation).
    float finetune = DebugFlags.DisableConeFineTune ? 0f : _coneFinetune;
    float liveAim  = CameraHeadingRadians + finetune * HalfConeAngleRad();

    OnStateChanged.Invoke(new ShotInputState(
        State, PowerNormalized, _coneFinetune, _arrowProgress,
        _passIndex, _passIndex >= cleanPasses,
        IsPutt, liveAim, CameraHeadingRadians));
}
```

Note: `CommitFlick` still adds `degradYaw` to its own `_aimYawRadians` calculation (used for the actual physics input). The published state intentionally excludes degradation — the line should follow the player's *intended* aim, not the deviated one.

### B. `ShotConeView.cs` — TargetingLine state list

`UpdateTargetingLine`, line ~222:

```csharp
// BEFORE:
bool show = state.State is ShotState.Aiming
                        or ShotState.Pulling
                        or ShotState.Timing
                        or ShotState.Flicking;
// AFTER:
bool show = state.State is ShotState.Idle
                        or ShotState.Aiming
                        or ShotState.Pulling
                        or ShotState.Timing
                        or ShotState.Flicking;
```

`Resolving` stays excluded — ball is in flight, line would lag.

### F. New: `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// 2D UI ball sprite at a fixed UI anchor (per Figma layout).
    /// Sprite source: BallContext.SelectedFullSprite. Visible in the same states
    /// as the targeting line (Idle/Aiming/Pulling/Timing/Flicking).
    ///
    /// Decoupled from world ball position — a future game-camera pass may switch
    /// to projecting the world ball's screen position, but for now this is a
    /// fixed-anchor UI element matching the Figma reference.
    /// </summary>
    public class CentralBallWidget : MonoBehaviour
    {
        [SerializeField] private Image          _image;
        [SerializeField] private RectTransform  _rect;
        [SerializeField] private ShotController _shotController;
        [SerializeField] private Vector2        _sizePx = new Vector2(100f, 100f);

        void Awake()
        {
            if (_rect == null)  _rect  = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();
            if (_rect != null)  _rect.sizeDelta = _sizePx;
        }

        void OnEnable()
        {
            BallContext.OnSelectedChanged += RefreshSprite;
            if (_shotController != null) _shotController.OnStateChanged += HandleStateChanged;
            RefreshSprite();
        }

        void OnDisable()
        {
            BallContext.OnSelectedChanged -= RefreshSprite;
            if (_shotController != null) _shotController.OnStateChanged -= HandleStateChanged;
        }

        void RefreshSprite()
        {
            if (_image == null) return;
            _image.sprite  = BallContext.SelectedFullSprite ?? BallContext.SelectedThumbnail;
            _image.enabled = _image.sprite != null;
        }

        void HandleStateChanged(ShotInputState state)
        {
            bool show = state.State is ShotState.Idle
                                    or ShotState.Aiming
                                    or ShotState.Pulling
                                    or ShotState.Timing
                                    or ShotState.Flicking;
            gameObject.SetActive(show);
        }
    }
}
```

**API verifications before writing:**
- `BallContext.OnSelectedChanged` event — confirm exists. If named differently, adjust.
- `BallContext.SelectedFullSprite` / `SelectedThumbnail` — confirm field names.
- `ShotController.OnStateChanged` + `ShotInputState.State` — confirmed in spec.

If any API differs, fix in code, note in done report. Do NOT invent fields.

### G. Scene wiring — `LabScaffold.unity`

Under `Canvas` (the one hosting `ShotConeView`), create child `CentralBall` GameObject:
- `RectTransform` 100×100
- **Anchor:** based on Figma `Balls` instance at canvas (487, 1245) on a 1170×2532 ref. Convert to anchor + anchoredPosition for canvas-center-pivot setup:
  - Figma center of ball = (487 + 50, 1245 + 50) = (537, 1295)
  - Canvas center = (585, 1266)
  - Offset from center = (537 - 585, 1266 - 1295) = (-48, -29) — note Y inverted in Unity UI vs Figma; if Figma Y grows downward, Unity anchoredPosition Y = -(figmaY - centerY) = -(1295 - 1266) = -29
  - **Final:** `anchor = (0.5, 0.5)`, `pivot = (0.5, 0.5)`, `anchoredPosition = (-48, -29)`
- `Image` component, `Raycast Target` = false
- `CentralBallWidget` component
- Wire `_image`, `_rect` to self, `_shotController` to scene's ShotController

No camera/ball-transform hookup needed (decoupled from world position).

### H. `PhysicsLab_Hole1.unity` — same wiring

Same CentralBall GO setup. No PhysicsLabController hookup needed.

### I. TargetingLine sprite — verify gradient

Figma shows a vertical white line that fades transparent at the top (`Line 1` vector, 48×102, gradient white→0% alpha). Check the existing `_targetingLine` Image's source sprite in LabScaffold:
- If sprite has the gradient: leave as-is
- If sprite is solid white: replace with a 1×N gradient PNG (white at base → transparent at top)

If sprite needs replacement, check `Assets/Art/In-Game UI/` for an existing gradient asset first. If missing, surface to architect — don't ship with solid line.

---

## Acceptance

### Static (screenshot-verifiable)

- [ ] LabScaffold play mode, before any shot input: TargetingLine is **visible** pointing forward from the ball.
- [ ] Central ball sprite visible at the ball's screen position, ~100×100, showing Golfin ball art.
- [ ] Sprite swaps when player picks Putt Ace via the GOLFIN selector.
- [ ] Targeting line has visible gradient fade toward the top (not a solid bar).

### Pivot behavior (THE CRITICAL TEST — line must move)

- [ ] In Idle state (no input): line points forward along camera heading.
- [ ] Drag the club handle left — line pivots left in real time. Drag right — line pivots right.
- [ ] Rotate camera (lab camera yaw) — line stays pointing at the same world target (so it appears to rotate on screen with camera).
- [ ] Both inputs combined: line behavior is consistent (no flicker, no lag beyond one frame).
- [ ] During `Pulling` and `Timing` states: line continues to pivot with finetune drag.
- [ ] `Resolving` state: line hidden.

### Ball position

- [ ] Central ball stays at fixed UI anchor (Figma position) regardless of camera movement.
- [ ] Hidden during `Resolving` (ball in flight).
- [ ] **Future note:** when game-camera work happens, may switch to world-ball-projection. Out of scope here.

### Lab integration

- [ ] Fire a shot. Ball flies. Central ball widget hides. After resolve, returns to new ball position.

---

## Out of scope

- Ball lift / hop animation
- Line color/style variations per shot mode
- 3D ball model swap (still uses existing world-space ball)

---

## Done report

`Docs/Specs/Active/8_5_d_central_ball_targeting_line/IMPLEMENTER_REPORT.md`:
- Files created/modified
- Screenshots: idle state with line+ball, post-Golfin-swap, post-PuttAce-swap, mid-flight (Resolving — line+ball hidden)
- API verifications (matched / adjusted)
- Gradient sprite source (existing or new)
- Per-checklist PASS/FAIL

Set STATUS to `READY_FOR_SELF_REVIEW`.
