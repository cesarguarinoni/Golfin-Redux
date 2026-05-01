# Cesar Rejection — putter_p1_ui (Iteration 2)

**Date:** 2026-05-01
**Screenshot:** active-aiming shot provided by Cesar — putter mode, 100%/48.6mts gauge visible

---

## Issue 1 — Putter track ABOVE the ball

`AlignPutterTrackToBall()` calls `ScreenPointToLocalPointInRectangle(parentRT, ...)` which
returns coordinates in parentRT's **pivot-relative** local space (Y=0 at parent center). But
`anchoredPosition` for a rect whose anchor is (0.5, 1) is measured from the **top edge** of the
parent, not its center. The code does `anchoredPosition.y = localPt.y`, which ignores the
anchor offset and places the track pivot far above the ball.

**Root cause in code:** `PhysicsLabController.cs:311` — `trackRT.anchoredPosition = new Vector2(0f, localPt.y);`
**Fix:** `trackRT.anchoredPosition = new Vector2(0f, localPt.y - parentRT.rect.height * 0.5f);`

---

## Issue 2 — PuttPathRoot only on first shot

`PuttPathPredictor._ballTransform` is an Inspector-wired reference that never updates when
the ball repositions after a shot. `HandleShotResolved` updates `_shotConeView.SetBallTransform`
but not the predictor. On second shot, the sim runs from the old (stale) ball position; the
projected canvas points all fail `screen.z < 0` → `pts.Count < 2` → no path drawn.

**Root cause:** No `SetBallTransform()` call to `PuttPathPredictor` in `HandleShotResolved`,
`SetupAtTee`, or `PlaceBallAt`.
**Fix:** Add `public void SetBallTransform(Transform t)` and `public void SetCamera(Camera cam)`
to `PuttPathPredictor`. Call both from PhysicsLabController alongside existing `_shotConeView`
updates.

---

## Issue 3 — PuttPathRoot not pointing toward hole

`PuttPathPredictor._worldCamera` is a static Inspector field; it never receives camera updates
when `PhysicsLabController` changes camera mode or repositions the camera. The predictor may
be projecting world trajectory positions through a misaligned or stale camera transform.

**Fix:** Wire camera updates into the predictor via `SetCamera()` (added in Issue 2 fix).
Call it in `Awake`, `OnHoleLoaded`, and after any `ChaseCamera.SetMode()` call.

---

## Issue 4 — Timing slabs must be rectangular and inside PutterTrack

The arc-shaped `TimingSlabGraphic` is designed for the cone. In putter mode, timing feedback
must be a horizontal rectangular slab that moves vertically inside the PutterTrack's band zones.

**Fix:**
- Add `[SerializeField] RectTransform _putterTimingSlabRT` to `ShotConeView`. Cesar wires
  it to a new child `PutterTimingSlab` GO under `PutterTrack` (Image component, 140×60,
  anchor top-center, pivot center).
- In `UpdateSlab()`: when `_puttMode == true`, skip `_timingSlab` entirely; instead position
  `_putterTimingSlabRT.anchoredPosition.y = -trackHeightPx * (1f - p)` and set `Image.color`
  via `SlabColorFromProgress(p)`.

---

## Previous iteration issues (resolved)
1. ✅ Cone outline: `SetOutlineVisible` now guards `&& !_puttMode`
2. ✅ Club card yrds→mts: `ClubButtonWidget.SetUnitMode(Meters)` wired
3. ✅ `_hasCache` reset on Idle/Resolving
