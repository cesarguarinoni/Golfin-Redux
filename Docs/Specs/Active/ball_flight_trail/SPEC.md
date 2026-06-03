# SPEC — Ball Flight Trail (state-colored)

**Slug:** `ball_flight_trail`
**Tier:** TELLCODE (visual; one new component + one additive property + one wiring call + one material asset). No BallAnimator edits.
**Status:** SPEC_READY
**Owner handoff:** this file → Claude Code.

---

## Goal

A single mobile-cheap trail ribbon on the in-flight ball that persists through the roll, and changes color by shot state:

- **In flight + rolling →** blue (default).
- **Ball ends OB →** the *entire* existing ribbon flips red at the OB transition.
- **Perfect shot →** the ribbon wears a reward color for the whole shot (decided at launch).
- **OB overrides perfect:** a perfect-tempo shot that still finishes OB flips red.

"Perfect" = a full-swing flick committed **inside the clean-pass window with zero aim degradation** (`_degradationYawRad == 0`). Putts are excluded (see NOTE P).

---

## Why this design

- `BallAnimator` (singleton, `Scripts/Physics/Viewer/BallAnimator.cs`) owns the visual ball: it `Instantiate`s `ballPrefab` (or a fallback sphere) parented to itself and moves `_instance.transform` by lerping along `Trajectory.samples` in `Update()`. **Flight and roll are one continuous sample stream**, so a `TrailRenderer` riding the ball follows the roll automatically — no roll-specific code.
- `Play()` destroys + respawns the ball each shot, so the trail **self-clears between shots**; we lazily ensure the renderer on the live ball rather than touching `BallAnimator`.
- Color signals already exist:
  - OB / flight / roll: `BallStateMachine.OnStateChanged : Action<BallStateChange>` with `Next ∈ {Flying, Rolling, OB, AtRest, InCup}` and `OBReason` populated on OB.
  - Perfect: latched from `ShotController.CommitFlick()` (new additive property, below).
- `PhysicsLabController` holds **both** `[SerializeField] ShotController _shotController` (L51) and creates `_ballSM` in `Awake()` (L180, subscribes `OnShotResolved` at L184). Single clean wiring point.

---

## Changes (4)

### A. `ShotController.cs` — expose "clean shot" (additive, no signature changes)

Add a readable, latched property:

```csharp
/// <summary>True if the most recent committed flick was a full-swing shot with zero aim
/// degradation (committed inside the clean-pass window). Putts are never "clean" for trail
/// purposes (see spec NOTE P). Latched in CommitFlick; read by BallTrailController on Flying.</summary>
public bool LastShotWasClean { get; private set; }
```

In `CommitFlick()`, right after `degradYaw` is computed:

```csharp
float degradYaw = DebugFlags.ForcePerfectAim ? 0f : _degradationYawRad;
LastShotWasClean = !IsPutt && Mathf.Approximately(degradYaw, 0f);   // ← add
```

No other edits here. `OnShotResolved` is unchanged.

### B. New `BallTrailController.cs` — `Scripts/Physics/Viewer/BallTrailController.cs`

`MonoBehaviour`, namespace `Golfin.Physics.Viewer`. Lives on the **same GameObject as `BallAnimator`** (so it persists across ball respawns). Responsibilities:

- Serialized config (Inspector-tweakable):
  - `Color _flightColor` = `#2E9BFF` (blue)
  - `Color _obColor` = `#FF2E2E` (red)
  - `Color _perfectColor` = `#FFD24A` (gold — reward; see NOTE C)
  - `Material _trailMaterial` (assigned to the asset from Change D)
  - `float _time = 8f`, `float _minVertexDistance = 0.3f`, `float _startWidth = 0.09f`
- `Configure(BallAnimator anim, BallStateMachine sm, ShotController shot)` — called by PhysicsLabController. Stores refs; unsubscribes then subscribes `sm.OnStateChanged += HandleStateChanged` (idempotent re-wire safe).
- `OnDestroy()` — unsubscribe.
- `HandleStateChanged(BallStateChange c)`:
  - `c.Next == Flying` **and** `c.Previous == Aiming` (shot start): grab `_anim.CurrentBall`, `EnsureTrail(ball)`, `tr.Clear()`, `tr.emitting = true`, then `SetRibbonColor(_shot != null && _shot.LastShotWasClean ? _perfectColor : _flightColor)`.
  - `c.Next == OB`: `SetRibbonColor(_obColor)` (whole ribbon — see recolor note), `tr.emitting = false`.
  - `c.Next == AtRest || c.Next == InCup`: `tr.emitting = false` (leave ribbon colored as-is; it clears on next shot's respawn).
- `EnsureTrail(Transform ball)`: `GetComponentInChildren<TrailRenderer>()`; if null, `AddComponent` on the ball and apply tuning from Change D. Idempotent. Covers both prefab and fallback-sphere paths → **no BallAnimator change needed**.
- `SetRibbonColor(Color c)`: recolor the **entire existing ribbon at once** via a `MaterialPropertyBlock` setting `_BaseColor` (uniform tint applied at draw, so already-laid segments recolor too). Do **not** rely on `startColor`/`endColor` — those only affect newly emitted vertices, and the ball is stationary at OB. Keep tint alpha = 1; the length-wise fade comes from the gradient (Change D).

Add an internal test/bot seam:

```csharp
#if UNITY_EDITOR
internal bool   EmittingForBot    => /* live trail .emitting */ ;
internal Color  RibbonColorForBot => /* current _BaseColor in the MPB */ ;
#endif
```

### C. `PhysicsLabController.cs` — wire it (1 field + 1 line)

- Add `[SerializeField] BallTrailController _ballTrail;` near the other serialized refs (~L51).
- In `Awake()`, **after** `_ballSM = new BallStateMachine(...)` (L180) and the `_shotController` null-check block:

```csharp
_ballTrail?.Configure(ballAnimator, _ballSM, _shotController);
```

No teardown needed in `OnDisable` (controller manages its own subscription via `Configure`/`OnDestroy`).

### D. Material asset + trail tuning

- Create `Assets/Golf/.../Materials/BallTrail.mat` (place beside existing ball/VFX materials — Code picks the canonical folder).
- Shader: **`Universal Render Pipeline/Particles/Unlit`**, Surface = Transparent, Blend = **Alpha** (NOTE: Additive is an easy swap if it reads better over bright fairway). Must respect `_BaseColor` tint (required for the Change B recolor).
- TrailRenderer tuning applied in `EnsureTrail`:
  - `time = _time` (8s — keeps the full ribbon for a long drive; vertex count bounded by `minVertexDistance`).
  - `minVertexDistance = _minVertexDistance` (0.3m → ≤ ~830 verts on a 250m drive).
  - `widthCurve`: `_startWidth` (0.09m) → 0 taper. `numCapVertices = 0`, `numCornerVertices = 0`.
  - `alignment = LineAlignment.View` (billboard, cheapest).
  - `textureMode = LineTextureMode.Stretch`.
  - `colorGradient`: RGB white, alpha **1 → 0** along length (the fade tail; tint comes from `_BaseColor`).
  - `shadowCastingMode = Off`, `receiveShadows = false`, `lightProbeUsage = Off`, `reflectionProbeUsage = Off`.
  - `emitting = false` on creation (turned on at the Flying transition).

---

## Mobile budget

One TrailRenderer, one unlit transparent material, no lights/shadows/probes, View-aligned, ≤ ~830 verts worst case, 0 cap/corner verts. Negligible vs. existing per-frame ball rotation work.

---

## NOTES / decisions to confirm during review

- **NOTE C (color choice):** `_perfectColor` is gold `#FFD24A` for max contrast against both the blue default and red OB (reward read). If you meant a cool *hue* instead, it's a one-field swap — e.g. violet `#B05CFF` or electric cyan `#21E6FF` (cyan is close to the blue default, weaker contrast).
- **NOTE P (putts):** putts skip per-pass degradation entirely (`ShotController.TickArrow`: `if (!SinglePassMode && !IsPutt)`), so `_degradationYawRad` stays 0 and *every* putt would read "perfect." Excluded via `!IsPutt` in Change A so putts use the normal blue. Flip if you want a perfect-putt color.
- **Instant play rate:** `BallAnimator.Play` snaps straight to rest at instant rate → ribbon has ~no length. Cosmetic only; left as-is.
- **OB freeze:** `LoopCameraDirector` freezes the camera on OB; the red ribbon will be on-screen at the freeze. Good — reinforces the OB read.

---

## Acceptance gates

Bot/EditMode-observable (via `_ForBot` seams):
1. On `Aiming→Flying`, live trail `emitting == true` and `RibbonColorForBot == _flightColor` for a degraded shot; `== _perfectColor` for a clean full-swing flick (`FireViaShotController(..., Green)` with clean passes).
2. On a shot that finishes OB, `RibbonColorForBot == _obColor` after the OB transition.
3. Putt shot → `RibbonColorForBot == _flightColor` (never perfect).
4. 16-fairway / unrelated systems untouched; no BallAnimator diff.

Human LOOK pass (aesthetics, can't be asserted): ribbon width/length/fade reads well on a driver, a chip, and a putt over fairway + green; OB flash is unmistakable; gold perfect-shot pops.

---

## Out of scope

- Trail on prediction/ghost trajectories (`TrajectoryRenderer` keeps its own coloring).
- Spin/curve-reactive trail width or particles.
- Per-club or per-surface trail variants.
