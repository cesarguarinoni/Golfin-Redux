# SPEC — map_view_strict_crop_indicators

**Status:** SPEC_READY (Cesar go 2026-08-10 — Order 355)
**Filed:** 2026-08-10 (Architect, after Cesar reviewed the shipped Order 354 result `5e419e595`)
**File:** `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` (single file; tests in `Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs`)
**Supersedes:** Order 354c's framing TARGET only ("seat ball AND flag"). Everything else 354 built (playfield-upright axis snap, env hide + far clip, zoom-out caps on every path, OB-rect pan bound, SolveShowRegionPose) is KEPT and extended.

---

## 1. Goal (Cesar, 2026-08-10, decisions locked via Q&A)

> "I want to ONLY be able to see the playable area, the place where the ball is
> resting, and if it fits, the Flag indicator over the hole. If it doesn't fit,
> the indicator should float on screen with a line pointing towards the hole —
> if the player moves the camera towards the hole, the indicator moves too,
> until it's over the hole when it appears on screen."

Locked decisions:
1. **Open framing = ball + shot context** (ball + club-carry landing zone), NOT ball+flag. Flag off-screen on long holes from the start → floating indicator.
2. **Strict crop:** every pixel of the viewport is playable area, always — open, pan, and pinch. No matte, no letterbox. Consequence (accepted): on narrow holes the player can never see tee→green in one screen; they pan.
3. **Ball gets the same floating-indicator treatment** when panned off-screen.
4. **Controls unchanged:** one finger = aim (`TrySetAimFromScreenPoint`), two fingers = pan + pinch.

## 2. Definitions

- **Playable rect** = the OB rectangle already loaded by `TryGetObRect()` → `_obRectValid` / `_obRectCenter` / `_obRectHalf` (same authority as Order 353c/354; Cesar: "use the map borders from the OB"). NOTE: this is a world-axis-aligned rectangle — some OOB fringe inside it will show. If Cesar later wants fairway-mask-tight cropping, that is a NEW task needing new data; do not attempt it here.
- **Footprint** = the ground quad seen by the camera: ray through each of the 4 viewport corners intersected with the horizontal plane `y = _ballWorldPos.y`. At `_heroTiltDeg` 80° and FOV ≤ `_maxZoom`, all 4 rays hit the plane (top edge ray elevation ≈ 80° − FOV/2 > 0 below horizontal). Camera yaw is playfield-snapped (`SnapAxisToPlayfield`, 354d), so the footprint trapezoid's AABB is world-axis-aligned — containment vs the rect is 4 cheap comparisons.
- **THE INVARIANT (new):** footprint ⊆ playable rect (small tolerance, `kFootprintTolM = 1f`), at every frame the map is open. 354 clamped what the *focus point* could do; 355 clamps what the *screen shows*.

## 3. Phase A — Framing: shot context under strict containment

In `PositionMapCamera()` (the Order 354c block):

1. `BuildShotRegion(camAxisN)` (L1157): region = **ball + landing** `L = ball + AimDirection2D()·carryM` (+ the landing-zone disc radius as margin). REMOVE the flag from the region — the flag is no longer a framing target, it is an indicator target (§Phase C). Seats stay `kShotBottomFrac` (ball) and ~0.75 viewport for L (reuse the iter-30 25%-blend intent); `kShotTopFrac` becomes the L ceiling, not a flag seat.
2. `FrameShowRegion` / `SolveShowRegionPose` (L1348): after the existing solve, run a **containment pass**:
   - Compute footprint; if all 4 corners inside rect → done.
   - Else bisect camera distance DOWN (footprint shrinks monotonically with dist at fixed tilt/FOV) to the largest contained dist, then re-run the slide solve for the ball seat and clamp the slide so containment holds. Containment WINS over seats: if seating the ball at `kShotBottomFrac` would push the footprint out the near edge, the ball rides higher on screen — correct, not a bug. If it would push the LANDING off-screen (driver carry on a narrow corridor), the landing goes off-screen — the aim line clips at the screen edge and the flag/ball indicator pattern covers orientation.
   - Degenerate guard stays (dist < 8 or > 4000 → fallback `AnchorBallToBottom` — and give the FALLBACK the same containment pass; no path may violate the invariant, mirroring 354's "no path may reveal the world" rule).
3. `_zoomOutCapFov` semantics unchanged, but zoom-out is ALSO dynamically gated (§Phase B) — the static cap is now just a fast pre-check.

## 4. Phase B — Pan + pinch under strict containment

- `PanCamera()` (L1818): replace the focus-point clamp with a **footprint clamp** — apply `move` per-axis (X, then Z, world axes): compute the moved footprint AABB; clamp each axis' translation so the AABB stays inside the rect (slide-along-edge behavior, no dead stop on diagonals). Keep `ClampPointToRect` as the no-OB-rect fallback exactly as today.
- Pinch zoom-out (the `_currentFov` clamp in `HandleTouchInput`): before applying a zoom-OUT delta, compute the footprint at the candidate FOV; if any corner exits the rect, hold FOV (zoom-in always allowed). Cache the per-frame footprint — 4 raycasts against a plane are pure math (`Plane.Raycast`), no physics.
- Editor-only guard: `#if UNITY_EDITOR` assertion in `Update()` that the invariant holds; `Debug.LogError` with the corner + rect values if not. This is the regression tripwire for every future framing change.

## 5. Phase C — Floating indicators (flag + ball)

Shared helper, one code path for both targets so docking is continuous by construction:

```
PlaceIndicator(RectTransform iconRT, RectTransform arrowRT, Vector3 worldPos)
  sp = _mapCam.WorldToScreenPoint(worldPos + Vector3.up * 2f)
  if sp.z < 0: sp = mirror through screen center (behind-camera case)
  docked = sp inside screen rect inset by kIndicatorEdgeInsetPx
  if docked:  icon at sp, arrow hidden            // "over the hole"
  else:       icon at intersection of segment (screenCenter → sp) with the
              inset rect; arrow visible, rotated to atan2(sp - iconPos),
              pointing OUT toward the target      // "floats, line points at it"
```

- **Flag:** extend `UpdateHoleIndicator()` (L1638) — today it hides `_flagIconRT` when off-viewport (iter-30); replace the hide with the clamp+arrow path. The icon itself, its canvas, and the 2m world lift stay as-is.
- **Ball:** new `_ballIconRT` + arrow on the same `_indicatorCanvas` — small white ball sprite (match `kBallMarkerSz` visual language). Driven from the same `PlaceIndicator` in `Update()`. The world-space `_ballMarker` sphere stays; the screen indicator only appears when the ball is off-screen (docked state for the ball = icon hidden, since the world marker is the on-screen representation).
- "Moves until it's over the hole": no animation code needed — the clamped position is a continuous function of the camera pose, so panning toward the hole walks the indicator along the edge and it docks the frame the target enters the inset rect. Add `kIndicatorEdgeInsetPx = 70f` (serialized, tunable) so it clears the SHOOT button corner — skip the dock zone under the SHOOT button rect (offset the clamp rect bottom-right corner) so the indicator never hides behind UI.
- **Arrow asset:** procedural triangle mesh/UI Image built in code like the existing markers — no new art import. NOTE: if Robin later supplies styled icons, they drop into the two serialized sprite fields; build with placeholders now.

## 6. Keep untouched (salvage list)

`SnapAxisToPlayfield` + `_alignToPlayfieldAxis`, `HideEnvironmentForMap`/`RestoreEnvironmentAfterMap` + `_environmentHideNames` + hole-fitted far clip, `TryGetObRect` cache, `TrySetAimFromScreenPoint` + `kHorizDragSensitivity`, landing zone/guide line/rings machinery, SHOOT repurpose, aim write-back, invariant JSON dumps, `MapViewCaptureDriver` seams (compiles unmodified), banned-list at the top of the file.

## 7. Tests

- **EditMode (pure math):** footprint corner computation (given pose/tilt/FOV → 4 plane hits); footprint-AABB per-axis clamp; `PlaceIndicator` clamp math as a static seam — docked-vs-floating classification, edge intersection, behind-camera mirror, continuity (two nearby camera poses → nearby indicator positions).
- **Editor manual (report with screenshots):** Holes 1 (long), 5, 6 (short) × tee + green-side lie: (a) open → only playable area visible, ball seated low, flag indicator floating on long holes / docked on short; (b) pan to all four edges → camera stops with the invariant intact, ball indicator appears when ball leaves screen and points back; (c) pinch both stops → zoom-out refuses at containment; (d) pan toward the hole → flag indicator walks the edge and docks over the hole exactly when it enters view; (e) aim ±90°, SHOOT closes + write-back unchanged; (f) editor invariant assertion silent throughout. Console clean.

## 8. Out of scope / do not regress

P-006, P-007, P-009 (unchanged); fairway-mask-tight cropping (future task, new data); indicator art polish (placeholder procedural sprites); no scene edits; no zones.json edits.
