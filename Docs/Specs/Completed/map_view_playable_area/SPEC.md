# SPEC — map_view_playable_area

**Status:** SPEC_READY (awaiting Cesar go)
**Filed:** 2026-08-06 (Architect)
**File:** `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` (single file; tests in `Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs`)
**Absorbs:** K2 `map_view_bottom_anchor` (smoke #5), P-008 (zoom-out feels limited), P-010 (open reframe pop). Do-not-regress: P-006, P-007, P-009.
**Reference screenshot:** `snap_20260807_065944.png` (tee-shot map view; NOTE: Cesar to confirm hole number — Hole 5 assumed below) — hole tile tiny + rotated mid-screen, off-course flat green filling most of the frame, mountain-ring arc visible at top.

---

## 1. Goal (Cesar, 2026-08-06)

> "Map view should only show the playable area, not the outside and the mountain ring."

This is how every reference title does it (Golf Clash, Golf Rival, Ultimate Golf): the hole
overview is framed to the hole itself — tee/ball at the bottom, green at the top, course
corridor filling the screen width — and the camera can never be panned or zoomed to reveal
the world outside the course. We copy that model while salvaging the existing v2
architecture (overlay camera, OB-rect loader, bisection framing solvers, aim write-back,
invariant dumps, capture-driver seams). No rewrite.

**Three changes deliver it:**
1. **Camera framed to the hole axis, not the live aim yaw** — the aim line rotates on
   screen instead of rotating the world (the reference-title behavior).
2. **Fit the whole remaining hole** (ball → green, clipped to the OB rect) instead of only
   `ball + carry` width at the ball's position.
3. **Hard-hide the outside** (MountainBackdrop etc. + far-clip + pan/zoom clamps) so
   off-course world cannot appear no matter what the player does.

---

## 2. What exists today (salvage inventory — keep all of it)

| Piece | Where | Verdict |
|---|---|---|
| Overlay cam, no-RT lifecycle | `BuildRuntimeObjects()` L455 | KEEP as-is (banned patterns stay banned) |
| OB-rect loader + per-hole cache | `TryGetObRect()` L959, `s_obRectCache` L211 | KEEP — verified: all 18 `Resources/HoleData/lomond-country-club/Hole_NN/zones.json` carry `obMask` |
| Bisection solvers | `SolveWidthDist` / `SolveAnchorSlide` inside `FramePlayingAreaWidth()` L1029 | KEEP structure, generalize (§4.2) |
| Bottom anchor | `AnchorBallToBottom()` L1128, `kBottomAnchorFrac` L203 | KEEP as fallback |
| Zoom-out cap | `_zoomOutCapFov` L150, pinch clamp L1530 | KEEP, extend (§4.4) |
| Aim input (finger sets landing) | `TrySetAimFromScreenPoint()` L1560 | KEEP untouched — works identically with a fixed camera (screen→ground ray) |
| Hide/restore machinery | `_hiddenObjects` / `_hiddenBallRenderers` lists L157-166 | KEEP, reuse for environment hide (§4.3) |
| SolidColor background | L479-480 | KEEP |
| Invariant dumps, capture seams | `PrewarmRT()`, `IsOpen`, `AimYawRadians`, JSON dumps | KEEP untouched — `MapViewCaptureDriver` compiles unmodified |

---

## 3. Phase 0 — Diagnose (do FIRST, report before coding)

The width-fill (Order 353b/c) *should* already prevent the screenshot state, and the ball
IS bottom-anchored in the screenshot (viewport y ≈ 0.04 = `kBottomAnchorFrac`) — so on
Hole 5 the code took either the **fallback** path (L946: `TryGetObExtent` returned false →
no zoom cap) or the **degenerate** width-solve path (L1104). Open the map on Hole 5 tee in
the editor and read the existing logs:

- `[MapView v2] Width-fill: halfW=… dist=…` → width-fill ran; report `dist`/`ballY`.
- `[MapView v2] Width-fill: refined solve degenerate…` → solver collapse; report values.
- Neither line → `TryGetObRect` failed; log `courseSlug`/`holeId` actually used
  (suspects: `ActiveCourseContext.CurrentCourseSlug` empty or casing mismatch vs
  `lomond-country-club`).

One paragraph in IMPLEMENTER_REPORT.md: which branch fired and why. The §4 redesign
replaces this code path anyway, but the root cause tells us whether the OB loader needs a
fix too (if it fails, §4 falls back too — the loader fix is then IN scope).

## 4. Phase 1 — The fix

### 4.1 Camera axis = hole axis (decouple from aim yaw)

In `PositionMapCamera()` L799: replace `aimDir = AimDirection2D()` (L812) as the **camera**
axis with `holeAxis = normalize((_flagWorldPos - _ballWorldPos).XZ)` (already computed as
`ballToFlag`, L803). `AimDirection2D()` keeps driving the guide line, landing zone, and
write-back exactly as today — iter-33's rule ("map opens at the player's current aim") is
preserved in the **aim line**, which now rotates on screen like the reference titles,
instead of rotating the camera. Ball at bottom, green at top, always — the §11 invariant
(`ball.screenY > flag.screenY`) becomes unconditionally true.

Delete-not-salvage: the aim-yaw-driven re-frame on drag (camera no longer moves while
aiming — one framing per open, plus pan/pinch).

### 4.2 Frame the remaining hole, clipped to the OB rect

Replace the `mustInclude = {ball, landing}` set (L834) and `FramePlayingAreaWidth`'s
ball-row width fit with a **show-region polygon**:

```
rect     = OB rect from TryGetObRect()               // axis-aligned world XZ
backEdge = (ball · holeAxis) - 15m                    // small margin behind ball
region   = clip(rect, halfplane: along ≥ backEdge)    // ≤5 vertices, plain 2D clip
```

Solve the camera pose with the existing bisection pattern, generalized:
- FOV fixed at `_initialZoom`; tilt: raise `_heroTiltDeg` default 70°→**80°** (steeper =
  less horizon; tune in Inspector — at 80° with the far clip of §4.3 the mountain ring
  cannot enter frame).
- `SolveFitDist`: bisect camera distance until **all region vertices** project inside
  `[kWidthFillMargin, 1-kWidthFillMargin] × [kBottomAnchorFrac, 0.96]` (replaces
  `EdgeSep`'s two-point width test; same 44-iter bisection, same monotonicity).
- `SolveAnchorSlide`: unchanged, but anchor target = the region's **near edge** midpoint
  → the bottom of the hole map sits flush at screen bottom (**this is K2 verbatim** — on
  tee the near edge ≈ ball; mid-hole the ball sits slightly above the edge, correct).
- Keep the 2-pass refine + degenerate guard (L1099-1110) as-is.
- Long-hole reality check: a 460 m hole framed tee→green in portrait will render the
  corridor width well inside the screen edges (length-limited fit, like Golf Clash par 5s).
  That is CORRECT — "fill the width" (Order 353b) yields to "show the whole playable area"
  (this order). Pinch-zoom-in still gets the tight view.
- Runs inside `Open()` before frame 1, as today → P-010 stays fixed-by-construction.

### 4.3 Hard-hide the outside world

New method `HideEnvironmentForMap()` called from `BuildRuntimeObjects()`, restored in
`DestroyRuntimeObjects()` via the existing `_hiddenObjects` list:

- Serialized `string[] _environmentHideNames = { "MountainBackdrop", "Backdrop", "Ring" }`
  — find root-level matches in the active hole scene, disable their Renderers.
  (Verified: `Hole_05_Geo.unity` has `MountainBackdrop`; older Geo scenes carry
  `Backdrop`/`Ring`. All on layer 0, so name-hide beats layer-mask here — no scene edits,
  no layer budget. NOTE: if a hole scene names its shell differently, add to the array.)
- Far clip: `_mapCam.farClipPlane = camDist + rectDiagonal + 50f` instead of the fixed
  2000 — geometry beyond the hole tile stops rendering even where no hide-name matched.
- Background stays SolidColor near-black green (L480): any off-tile sliver reads as map
  matte, like the reference titles' letterboxing.
- **Verify-then-extend:** after the above, if ANY off-tile ground still shows in-frame
  (frame debugger), identify the mesh and add its name to the array — the flat bright-green
  plane in the screenshot is one such suspect (its object name is unknown; do NOT guess —
  frame-debug it).

### 4.4 Clamp pan + zoom to the playable area

- `PanCamera()` L1548: after `move`, clamp `_camFocusPoint` XZ into the OB rect (simple
  `Mathf.Clamp` per axis against rect min/max), then recompute the camera position from the
  focus point. No OB rect → keep unclamped fallback.
- Pinch: `_zoomOutCapFov` already caps zoom-out; ALSO cap in the fallback path (L947 sets
  `_maxZoom` today — change to cap at the fallback fit) so no path can zoom out past its
  fit. P-008 ("zoom-out limited") closes inverted: the DEFAULT view is now the max-out.

## 5. Phase 2 — Tests (extend `MapViewAimingTests.cs`)

EditMode (pure-math seams, same style as `RingCenterAtPct`): polygon clip returns ≤5
verts and contains ball+flag; fit solver output projects all verts in-viewport; pan clamp
keeps focus inside rect. PlayMode/editor manual: Holes 1 (long), 5 (the screenshot repro),
6 (short) — from tee AND from a green-side lie, aim dragged ±90°, pinch to both stops,
two-finger pan to all four edges. **Acceptance: at no point may the mountain ring, the
backdrop, or off-tile ground be visible** (screenshot each hole for the report). Ball
bottom-anchor flush (K2 check: long + short hole). Landing zone/guide line/flag icon all
render as today; SHOOT still closes + writes back aim; invariant JSON still written.

## 6. Out of scope / do not regress

- P-007 (rings/landing on trees — ZTest=Always is intentional), P-009 (distance bands —
  separate task), P-006 (club-carry hydration).
- No scene edits, no zones.json edits, no `MapViewCaptureDriver` edits, no RT/RawImage
  reintroduction (§ banned list at top of file).
- `ShotController` untouched; `HoleCardWidget.OpenViaWidget()` entry unchanged.

## 7. Sequencing

Single-file task, parallel-safe with the Hole 6 fix currently in flight (different files).
Queue behind it anyway if Code's queue is serial — no shared state. Supersedes K2: when
this lands, delete the K2 block from TellCode.md and log it in CURRENT STATE.
