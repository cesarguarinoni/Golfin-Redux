# Quick task — shot UI hidden while the shot is in progress

**Requested by Cesar, 2026-08-06 (chat):**
> "Shoot UI buttons and all the shooting UI should only be seen and interactable during shooting.
> Once the shot is in progress, they should not be seen or interactable until the next shot."

**Scope answer (Cesar, same thread):** *"Shot controls only, but also make map view inaccessible
(keep the map image, non interactable until next shot)."*

---

## Definition of "shot in progress"

`ShotController.State` timeline for one shot:

```
Idle → Aiming → Pulling → Timing → Flicking → Resolving → (ball at rest → CompleteShot()) → Idle
                └─────── shot being set up ───────┘   └── shot in progress ──┘
```

`Flicking` is transient — `CommitFlick()` runs straight through to `Resolving` inside the same
call, so the visible transition is `Timing → Resolving`. Both are treated as in-progress.

`ShotController.CompleteShot()` (called by `PhysicsLabController` when the ball settles / is
repositioned) is the "next shot armed" edge that restores the UI.

## In scope — hidden + non-interactable while in progress

| Element (ShotUI_Canvas) | Before | After |
|---|---|---|
| `ActionButtons_Cluster` (Spin, Fade/Draw, Ball, Club) | alpha 1, non-interactable | alpha 0, non-interactable |
| `PowerHUD` (`PowerGaugeWidget`) | shown for every state ≠ Idle → **visible during flight** | shown only Aiming/Pulling/Timing |
| `PutterTrack`, `PuttPathRoot` (putt mode) | stay active through the putt | hidden, restored to their pre-shot active state at re-arm |
| `SelectorOverlay`, `SelectorOverlay_Ball`, `SpinPanel` | (already closed in practice) | force-closed defensively |
| `HoleCard` map button | tappable → opens map view mid-flight | image unchanged, button inert |
| `ConeRoot`, `TargetingLine`, `ClubHandle`, `TimingSlab`, `CentralBall` | already hidden at Resolving | unchanged |

## Out of scope — stay visible during flight

`PlayerCard` / `PlayerCard_P2`, `HoleCard` chips + map image, `WindIndicator`, `HoleIndicator`,
`SettingsButton`, `TurnBanner`.

## Implementation

1. **New** `Assets/Scripts/Gameplay/UI/ShotUI/ShotInProgressUiGate.cs` — subscribes to
   `ShotController.OnStateChanged`, exposes `static bool ShotInProgress`, and owns the elements
   that have no widget of their own (putter track / putt path / overlays / map button).
   Edge-triggered: caches `activeSelf` on hide and restores it on re-arm, so it never
   re-activates a putter track that putt mode had turned off.
2. **`ActionButtonsRoot`** — drive `CanvasGroup.alpha` (0 while in progress) in addition to the
   existing `interactable`/`blocksRaycasts` = `Idle` gate.
3. **`PowerGaugeWidget`** — show predicate `!= Idle` → `is Aiming or Pulling or Timing`.
4. **`HoleCardWidget.OpenMapView()`** — early-return guard on `ShotInProgressUiGate.ShotInProgress`
   (belt-and-braces behind the inert button).
5. **Scene (`LabScaffold.unity`)** — add + wire the gate on `ShotUI_Canvas`; set the `HoleMap`
   Button's *disabled* colour equal to its *normal* colour so `interactable = false` leaves the
   map thumbnail looking identical (Cesar: "keep the map image").

## Verification (done)

Real-flow bot playthrough — `GOLFIN/Smoke/Loop v2/Hole 1 Playthrough (Deferred Record)`, which
boots ShellScene and plays Hole 1 through the player's own entry path. 1170×2532, 68.9 s, 6 strokes.

**Numeric gate** — per-frame sampler over the live scene, printing only on change. Same shape
repeated for every stroke:

```
t=24.63 Timing   |gate=False|clusterA=1.00/int=True |powerA=1.00|coneA=1.00|map=True
t=25.61 Resolving|gate=True |clusterA=0.00/int=False|powerA=0.00|coneA=0.82|map=False
t=25.88 Resolving|gate=True |clusterA=0.00/int=False|powerA=0.00|coneA=0.00|map=False
t=28.13 Idle     |gate=False|clusterA=1.00/int=True |powerA=0.00|coneA=0.25|map=True
```

**Frame gate** — sequential decode (no keyframe sampling) across the launch: frames 56/57/58 show
ball on tee, 96 % gauge, all four buttons; frame 59 (ball launched, chase cam) has buttons, gauge
and central ball already gone in the SAME frame. No flash. Pixel probe on the SPIN card centre:
RGB sum 645 → 330 at frame 59, back to 736 at frame 163 when the next shot arms. The cone/club
handle fade out over ~0.3 s afterwards, which is `ConeAlphaController`'s existing fade, unchanged.

**Map** — thumbnail renders at full saturation during flight (`m_DisabledColor` now equals
`m_NormalColor` = white, so ColorTint is a no-op), while `Button.interactable` reads `False`.

Artifacts: `Docs/Specs/Quick/_attachments/shot_ui_hidden_launch_frames.png` (frames 56–62),
full clip at `Docs/Reports/Media/shot_ui_hidden_while_shot_in_progress.mp4` (gitignored).

## Found in passing — not fixed here

`ActionButtonsRoot` on `ShotUI_Canvas/ActionButtons_Cluster` has `_shotController: {fileID: 0}` in
HEAD, so it has never run: its intended "no club/ball/spin swap mid-swing" gate
(`interactable = blocksRaycasts = (State == Idle)`) has never been live. Confirmed at runtime —
the cluster stayed `alpha=1 interactable=True` right through Aiming/Pulling/Timing.

Left alone deliberately: wiring it changes PRE-shot interaction semantics, and `ShotController`
reads raw input, so pressing the Club button also drives Idle → Aiming — which could break the
press-and-hold selector mid-drag. Filed as its own task. The shot-in-progress hide does not depend
on it; `ShotInProgressUiGate` drives the cluster CanvasGroup directly.
