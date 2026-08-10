# SPEC — `aim_camera_ball_centering`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-10 (Architect).

## Goal

During full-swing AIMING, the 3D ball currently projects well below the fixed 2D `CentralBallWidget` sprite and reads tiny (~6 px at ~8.5 m camera distance). Rework the aim-phase camera framing so the 3D ball projects at the SAME screen point as the 2D UI ball (screen-center X, ~57.7% down), and bring the camera close enough that the ball reads clearly — with a tee-shot constraint: the tee markers must remain on screen during tee off. This is the "future game-camera pass" already anticipated in `CentralBallWidget.cs`'s class doc, implemented in the inverse direction the doc guessed (move the camera under the fixed UI anchor, not the UI to the ball).

## Reference

- **UI ball anchor ground truth:** claude-project mockups `InGame Shot Tests 5/9.png` (1170×2532). 2D ball center ≈ (585, 1460) from top-left → **viewport (0.50, 0.4234)** (Unity viewport Y from bottom). Runtime must derive this from the live `CentralBallWidget` rect, not this constant (constant is the fallback only).
- **Competitive framing (visual observation — Golf Clash / Golf Rival / Ultimate Golf tee view):** camera sits low and close (≈2.5–4 m equivalent behind the ball, ≈1–1.5 m up), ball renders large and readable in the lower-center of the screen (~55–65% down), tee surroundings still visible. Our current 8 m / 3 m framing is roughly 2–3× farther than the genre standard.
- **Current code framing (the thing being replaced):** `PhysicsLabController.ApplyCameraYaw(Camera cam)` (~line 1051):
  `pos = _orbitCenter − lookDir·8 + up·3; LookAt(_orbitCenter + lookDir·3 + up·0.5)`
  → pitch ≈ 12.8° down, ball ≈ 20.6° below horizontal → ball ~62% down screen (close to target vertical, but drifts with FOV) at ~8.54 m → ~0.29° angular size. Both offsets are hardcoded magic numbers.

## Figma Fidelity

No UI elements change. One row for the alignment contract:

| Element | Reference | Property → value |
|---|---|---|
| 3D ball projection (full-swing aim) | `InGame Shot Tests 9.png` 2D ball | `WorldToViewportPoint(ballPos)` == CentralBallWidget viewport point ± 0.01 both axes |
| Tee markers (tee shot aim only) | Hole scene `TeeMarker` transforms | all markers' viewport X within [0.05, 0.95], viewport Z > 0 |

## Architecture context

- **Asmdef boundaries affected:** NONE. `PhysicsLabController` (Golfin.Physics.Viewer) already has `using Golfin.Gameplay.UI.ShotUI;` — referencing `CentralBallWidget` adds no asmdef edge. Verify before coding; if the Viewer asmdef does NOT reference the ShotUI asmdef, fall back to the serialized-RectTransform option in §Impl-2 and flag in the report.
- **Existing code referenced:**
  - `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `ApplyCameraYaw(Camera)` (~1051), `HandleCameraOrbit` (~990), `AdjustCameraForDepression`, `OnHoleLoaded` tee-marker scan (~1746, builds `regularMarkers`, keeps only midpoint in `_savedTeeWorldPos`), `_orbitCenter`, `_cameraYaw`, `CurrentShotIsPutt`.
  - `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` — do NOT touch. Chase framing (`_followDistance=3`, `_followHeight=1.8`) is the in-flight camera; the null-target early-return guardrail is what hands aim-phase control to `ApplyCameraYaw`.
  - `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` — do NOT touch (state dispatch only).
  - `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` — the 2D ball. Fixed UI anchor per Figma; exposes `_rect` (private). Add a public read-only accessor (§Impl-2).
  - `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` (~2827–2840) — calls `ApplyCameraYaw` on map-view exit to restore the aim camera. Signature must not change; internals-only edit keeps this path consistent for free.
- **Callers of `ApplyCameraYaw` (all must keep working unmodified):** `HandleCameraOrbit`, `RepositionBallWithLookDir`, map-view exit restore, `SmokeRunner2eHost`, `BotDriver`.

## Implementation

### 1. New serialized tunables on `PhysicsLabController` (extend the existing `[Header("Camera")]` block)

```csharp
[Header("Aim framing (aim_camera_ball_centering)")]
[Tooltip("XZ distance behind the ball during full-swing aim (m). Genre ref: 2.5–4.")]
[SerializeField] float _aimCamDistanceM = 3.0f;
[Tooltip("Camera height above the ball during full-swing aim (m).")]
[SerializeField] float _aimCamHeightM = 1.4f;
[Tooltip("Fallback viewport Y for the ball projection when CentralBallWidget is unavailable. 0.4234 = mockup 2D ball center.")]
[SerializeField] float _aimBallViewportYFallback = 0.4234f;
[Tooltip("Tee markers must project within this fraction of half-screen-width during tee-off aim.")]
[SerializeField] float _teeMarkerSafeFrac = 0.9f;
[Tooltip("Ceiling for the tee-visibility pull-back (m). 8 = legacy distance.")]
[SerializeField] float _aimCamMaxDistanceM = 8f;
```

Keep the values Inspector-tunable; do NOT add controls.csv keys in this pass (camera feel iteration is faster in-Inspector; CSV promotion is a follow-up once values lock).

### 2. Ball-anchor viewport query

- Add to `CentralBallWidget`: `public RectTransform Rect => _rect;` (one line, no behavior).
- In `PhysicsLabController`, add `[SerializeField] CentralBallWidget _centralBallWidget;` (wire in Inspector; scene object lives under the shot UI canvas).
- Helper `float GetAimBallViewportY()`:
  - If widget or its rect is null → return `_aimBallViewportYFallback`.
  - Overlay canvas: `Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, rect.position); return sp.y / Screen.height;`
  - NOTE: if the shot UI canvas is Screen Space – Camera, pass the canvas camera instead of null. Check the canvas render mode at runtime (`canvas.renderMode`) rather than assuming.
  - X is not solved for: the current math already projects the ball at viewport X = 0.5 (camera position and look target are colinear with the ball in `lookDir`), and the 2D ball is horizontally centered. Preserve that colinearity.

### 3. Rewrite `ApplyCameraYaw` internals (signature unchanged)

Gate: **putter aim keeps the legacy framing** — if `CurrentShotIsPutt` (or the club is the putter at aim time), run the existing two lines verbatim. Full-swing aim runs the new solver:

```csharp
void ApplyCameraYaw(Camera cam)
{
    Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));

    if (/* putter aim */) { /* legacy 8/3/3/0.5 path, unchanged */ return; }

    float d = ComputeAimDistance(lookDir);            // §4 tee clamp; else _aimCamDistanceM
    float h = _aimCamHeightM;
    float vy = GetAimBallViewportY();

    Vector3 camPos = _orbitCenter - lookDir * d + Vector3.up * h;

    // Solve pitch so the ball lands at viewport Y = vy.
    float tanHalfV   = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    float thetaOff   = Mathf.Atan((1f - 2f * vy) * tanHalfV);          // rad below view center
    float pitchDown  = Mathf.Atan2(h, d) - thetaOff;                    // rad
    float yawDeg     = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;

    cam.transform.position = camPos;
    cam.transform.rotation = Quaternion.Euler(pitchDown * Mathf.Rad2Deg, yawDeg, 0f);
}
```

Derivation sanity (FOV 60, d=3, h=1.4, vy=0.4234): θ_ball = atan(1.4/3) = 25.0°; θ_off = atan(0.1532·0.5774) = 5.06°; pitch = 19.9° down. Ball distance 3.31 m → ~2.6× larger on screen than today. The solver reads `cam.fieldOfView` live, so it is correct for whatever FOV the scene camera actually uses — do not hardcode 60.

- `AdjustCameraForDepression` composes after this exactly as it does today — no change.
- `_orbitCenter` semantics unchanged (current ball lie).

### 4. Tee-marker visibility clamp (`ComputeAimDistance`)

- In `OnHoleLoaded`, alongside the existing `regularMarkers` scan (~line 1746), cache the marker world positions in a new field `List<Vector3> _teeMarkerPositions` (cleared on hole unload). Today only the midpoint survives the scan; keep both.
- "On the tee" test: `_savedTeePosValid && (GetCurrentOrigin XZ distance to _savedTeeWorldPos) < 1.0f` (same convention the tee logic already uses; NOTE: if a cleaner "stroke == 1" flag exists in the session driver, prefer it and cite it in the report).
- Clamp, closed-form (markers are ~coplanar with the ball; no iteration needed):

```csharp
float ComputeAimDistance(Vector3 lookDir)
{
    float d = _aimCamDistanceM;
    if (!BallIsOnTee() || _teeMarkerPositions.Count == 0) return d;

    Camera cam    = chaseCamera.GetComponent<Camera>();
    float tanHalfV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    float tanHalfH = tanHalfV * cam.aspect;
    Vector3 right  = new Vector3(lookDir.z, 0f, -lookDir.x); // XZ perpendicular

    foreach (var m in _teeMarkerPositions)
    {
        Vector3 rel   = m - _orbitCenter;
        float lateral = Mathf.Abs(Vector3.Dot(rel, right));
        float along   = Vector3.Dot(rel, lookDir);           // + = ahead of ball
        // need: lateral / (d + along) <= tanHalfH * safeFrac
        float dNeeded = lateral / (tanHalfH * _teeMarkerSafeFrac) - along;
        d = Mathf.Max(d, dNeeded);
    }
    return Mathf.Min(d, _aimCamMaxDistanceM);
}
```

- Portrait phones have a NARROW horizontal FOV (~30° at vFOV 60, aspect 1170/2532). Expect the clamp to be ACTIVE on the tee — that is the design: the camera gets as close as marker visibility allows on stroke 1, and drops to the full close-up (`_aimCamDistanceM`) from stroke 2 on. If real Hole 1 marker spread forces `d` back to ≈8 m (no improvement on the tee), report the measured marker offsets and the resulting `d` in IMPLEMENTER_REPORT — Architect will then rule on relaxing `_teeMarkerSafeFrac`, marker subset (nearest pair only vs all four colors), or a tee-only FOV bump. Do not decide that unilaterally.

### 5. Explicitly NOT in this pass

- No `ChaseCamera` / `LoopCameraDirector` edits (in-flight framing unchanged).
- No FOV changes, no ball-mesh visual scale change (if the ball still reads too small at the clamped tee distance, that is the flagged escalation in §4 / a Phase-2 option).
- No `CentralBallWidget` behavior change beyond the one-line accessor.
- No putter-aim change.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] EditMode test: given a camera with known FOV/aspect and the solver's output pose, `cam.WorldToViewportPoint(_orbitCenter)` == (0.5, targetVy) ± 0.01 on both axes, for at least FOV 50/60/70 and vy 0.40/0.4234/0.50 (position-trace assertion per Lesson O — not event dispatch).
- [ ] EditMode test: tee clamp — synthetic markers at ±2 m lateral produce `d` = `2 / (tanHalfH · safeFrac)`, capped at `_aimCamMaxDistanceM`; markers at ±0.5 m leave `d` = `_aimCamDistanceM`.
- [ ] PlayMode/manual on Hole 1 tee: 3D ball sits visually inside the 2D CentralBall sprite (screenshot), all tee markers on screen; report measured marker lateral offsets and resulting `d`.
- [ ] Manual: stroke 2+ (fairway lie) uses full close-up distance; ball noticeably larger than pre-change (before/after screenshots at same lie).
- [ ] Manual: orbit drag still works and the ball stays pinned to the UI ball point while yawing; map-view open → close restores the new framing (not the legacy one).
- [ ] Manual: putter aim framing is byte-identical to pre-change.
- [ ] Manual: fire a shot — aim→chase handoff has no jarring pop (chase is 3 m/1.8 m, now closer to the aim pose than before).
- [ ] `AdjustCameraForDepression` still applies (test on a depressed lie if one is reachable, else flag as unverified).
- [ ] All `[SerializeField]` references wired in the Inspector (`_centralBallWidget`).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — new tunables, `GetAimBallViewportY`, `ComputeAimDistance`, `_teeMarkerPositions` cache in `OnHoleLoaded`, rewritten `ApplyCameraYaw` internals (signature unchanged, putter path preserved verbatim).
- `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` — one-line `Rect` accessor.
- `Assets/Scripts/Physics/Tests/` — new EditMode test file for the solver + clamp math (pure-math tests; mirror existing test patterns in `LoopCameraDirectorTests.cs` for fixture style).
- Scene: wire `_centralBallWidget` on the LabRoot PhysicsLabController.

## Smoke evidence

Solver + clamp: EditMode position-trace tests above. Framing feel: human-in-the-loop per Lesson O — load Hole 1, screenshot tee aim (markers + ball-under-UI-ball), screenshot a fairway aim, describe what the camera visually did in IMPLEMENTER_REPORT.md.

## Out of scope (do NOT do these)

- Chase/cinematic/putter camera framing.
- FOV or ball-scale changes (escalation path only, §4).
- Moving or resizing any 2D shot UI element.
- controls.csv promotion of the new tunables.
