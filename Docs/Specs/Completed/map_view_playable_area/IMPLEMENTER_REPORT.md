# IMPLEMENTER_REPORT — map_view_playable_area (Order 354)

**Iteration shape:** `mapview_framing:off-course-world-visible`
**Implemented:** 2026-08-07 (Claude Code, orchestrator route — Cesar dispatched directly, no subagent chain)
**Baseline:** HEAD `d705f053d` — see `HEARTBEAT.log` for the full kickoff porcelain
**Canonical screenshot:** `screenshots/canonical_hole1_tee_map_open.png` (1170×2532, real-flow play mode)

---

## §3 Phase 0 — DIAGNOSIS (which branch caused the mess)

**Neither of the two branches the spec suspected.** The OB rect loads fine and the width-solve
is *not* degenerate. Both suspected failure modes are ruled out by measurement.

Method: replicated the shipped `TryGetObExtent` + `FramePlayingAreaWidth` math verbatim against
real hole data (OB rect from `zones.json`, tee from `TeeMarker_regular_*`, pin from `Flag_1` in
the `Hole_NN_Geo` scenes) on a camera at the device aspect 1170×2532, FOV 45°, tilt 70°:

| Hole | OB rect | aimN | halfW | fitted width | widthBracketOk | dist | ballY | degenerate | flag VP.y | OB far corner VP.y |
|---|---|---|---|---|---|---|---|---|---|---|
| 01 | 576.2 × 261.2 | (−0.97, −0.23) | 193.8 | **388 m** | true | 404.7 m | 0.040 | **false** | 0.54 | 0.60 |
| 05 | 317.1 × 337.0 | (0.66, −0.75) | 230.4 | **461 m** | true | 481.1 m | 0.040 | **false** | 0.39 | 0.43 |
| 06 | 228.9 × 100.6 | (−0.99, 0.10) | 61.7 | 123 m | true | 128.9 m | 0.040 | false | 0.55 | 0.69 |

Two findings:

1. **The reference screenshot is Hole 1, not Hole 5.** Hole 1's predicted projection is flag at
   viewport-y 0.54 and the OB rect's far corner at 0.60; measured on `snap_20260807_065944.png`
   the flag sits ~910/2000 px from the top (VP.y ≈ 0.545) and the tile's far edge ~790/2000
   (VP.y ≈ 0.60). Hole 5 would have predicted 0.39 / 0.43 — nowhere near. Hole 1's 576×261 tile
   also matches the elongated tile in the frame; Hole 5's is near-square. **Cesar's open question
   in the spec header is answered: Hole 1.**

2. **Root cause is the design of the fit, not a failed branch.** `FramePlayingAreaWidth` fitted the
   corridor width *at the ball's row only*, with nothing constraining what lay beyond the tile's far
   edge. Everything above viewport-y ≈ 0.60 was, by construction, off-tile world — the mountain ring
   and the backdrop plane. It was made worse by `TryGetObExtent`'s `halfW`, which is the **support
   function of the axis-aligned OB box along `rightN`** (`|hx·rightN.x| + |hz·rightN.z|`). When the
   hole axis is rotated relative to the OB box that over-estimates the corridor: Hole 1's true
   corridor is ~261 m but it fitted **388 m**; Hole 5's is ~150 m but it fitted **461 m**. So the
   camera pulled back further than the corridor needed *and* had no far-side constraint at all.

Consequence for scope: the OB loader is **healthy** on all sampled holes, so the loader fix the spec
held in reserve is **not** needed and was not done.

---

## §4 Phase 1 — What was implemented

### §4.1 Camera axis = hole axis (`PositionMapCamera`)
`aimDir` for camera positioning is now `(flag − ball).XZ.normalized` instead of `AimDirection2D()`.
`AimDirection2D()` is untouched and still drives the guide line, landing zone, rings, invariant dump
and the SHOOT write-back. **PASS** — proved in play mode: three consecutive `PositionMapCamera` calls
across an open + re-aim logged a byte-identical pose (`dist=407.4 slide=424.2 camY=1130.8
ballVP=(0.45,0.06) flagVP=(0.46,0.55)`), and `canonical_hole1_tee_map_open.png` vs
`hole1_tee_map_reaimed.png` show the hole tile in an identical position/rotation with only the aim
line swung left. The aim-yaw-driven re-frame on drag is gone as specified.

### §4.2 Frame the remaining hole, clipped to the OB rect
- `BuildShowRegionXZ(obCenter, obHalf, axisXZ, ballXZ)` — **public static, pure math.**
  Sutherland–Hodgman clip of the OB rect by the single half-plane `along ≥ dot(ball,axis) − 15 m`.
  ≤5 vertices by construction.
- `SolveShowRegionPose(cam, region, axisN, tilt, bottomFrac, sideMargin, topFrac, …)` —
  **public static**, generalises the two Order 353b bisections and is the exact method the EditMode
  tests drive (no re-implementation in the tests).
  - `SolveWidthDist`'s two-point edge-separation test → `ContainsRegion`, which requires **every**
    region vertex inside `[0.02, 0.98] × [0.04, 0.96]`. Same monotonicity, same 44-step bisection.
  - `SolveAnchorSlide` keeps its shape but anchors the region's **lowest projected vertex** to
    `kBottomAnchorFrac`.
  - The two are solved **jointly**: distance is bisected and every candidate distance is evaluated
    *after* anchoring the bottom. That makes the search monotone and guarantees both invariants at
    the answer, instead of alternating two weakly-coupled solves and hoping they converge.
- Degenerate guard (`dist < 8 || > 4000`) kept; on failure it falls back to `AnchorBallToBottom`.
- Still runs inside `Open()` before frame 1 → **P-010 stays fixed-by-construction.**

**DEVIATION FROM SPEC, stated explicitly:** §4.2 says the anchor target is the region's *near-edge
midpoint*. I anchor the *lowest projected vertex* instead. On a hole whose axis is rotated relative
to the OB rect (Hole 1: 13°; Hole 5: 48°) the near edge is a **corner**, and anchoring the midpoint
would push that corner off the bottom of the screen — directly contradicting the same sentence's
stated goal ("the bottom of the hole map sits flush at screen bottom") and fighting the containment
constraint. Anchoring the lowest vertex realises that goal exactly. Test:
`FitSolver_ProjectsEveryRegionVertexInsideViewport` asserts `lowest == 0.04 ± 0.01`.

**PASS** — measured through the production statics at device aspect, tee and green-side lie:

| Hole / lie | verts | dist | region VP.x | region VP.y | ball VP | flag VP |
|---|---|---|---|---|---|---|
| 01 / tee | 4 | 194 m | [0.020, 0.944] | [0.040, 0.648] | (0.45, 0.06) | (0.46, 0.58) |
| 01 / approach | 4 | 149 m | [0.020, 0.968] | [0.040, 0.308] | (0.30, 0.06) | (0.30, 0.21) |
| 03 / tee | 4 | 90 m | [0.179, 0.867] | [0.040, 0.960] | (0.55, 0.08) | (0.54, 0.84) |
| 03 / approach | 4 | 63 m | [0.050, 0.980] | [0.040, 0.514] | (0.61, 0.10) | (0.61, 0.33) |
| 05 / tee | **5** | 227 m | [0.020, 0.977] | [0.040, 0.458] | (0.50, 0.06) | (0.50, 0.41) |
| 05 / approach | 3 | 138 m | [0.020, 0.980] | [0.040, 0.264] | (0.47, 0.07) | (0.47, 0.19) |
| 06 / tee | 4 | 62 m | [0.067, 0.980] | [0.040, 0.781] | (0.38, 0.10) | (0.38, 0.63) |
| 06 / approach | 4 | 55 m | [0.042, 0.980] | [0.040, 0.417] | (0.42, 0.10) | (0.42, 0.23) |

Every region vertex is inside the viewport window; the bottom is flush at 0.040 in all 8 cases
(**K2 satisfied on long and short holes**); the ball projects below the flag in all 8.

### §4.3 Hard-hide the outside world
`HideEnvironmentForMap()` from `BuildRuntimeObjects()`, `RestoreEnvironmentAfterMap()` from
`DestroyRuntimeObjects()`, via a dedicated `_hiddenEnvRenderers` list (a separate list is required —
`HideShotUIChrome()` clears `_hiddenBallRenderers` and runs *after* `BuildRuntimeObjects`).

**Two spec premises were wrong and are corrected here — both by measurement, not guessing:**

1. The spec says "find **root-level** matches in the active hole scene". `MountainBackdrop` is a
   **child of `HoleRoot`**, not a scene root — the scene roots are only
   `HoleRoot; WalkCamera; Directional Light`. A root-only scan would have hidden **nothing**. The
   scan walks the renderer plus its first three ancestors.
2. The spec's "flat bright-green plane … object name is unknown; do NOT guess — frame-debug it" is
   **`ObGroundSkirt`** — a **9000 m** plane with material `ObSkirt_Mat`, spawned at **runtime** (it
   exists in no scene file, which is why the edit-mode scan of `Hole_01_Geo`/`Hole_05_Geo` and of
   `LabScaffold.unity` all found only `MountainBackdrop`). I found it the way §4.3 mandates: shipped
   the name array without it, ran the real flow, and read the new
   `Environment hide: off-tile renderer(s) NOT hidden — add to _environmentHideNames:
   ObGroundSkirt [span=9000m ctr=(0,0,0) mat=ObSkirt_Mat]` diagnostic. That diagnostic is now
   permanent: any future hole that leaks off-tile ground names the offender in the log.

`_environmentHideNames = { MountainBackdrop, Backdrop, Ring, ObGroundSkirt }`. It is a **new**
serialized field, so the code default takes effect with **no scene edit**.
Far clip: `camToFocus + rectDiagonal + 50` (`ApplyHoleFittedFarClip`), falling back to `_farClip`
with no OB rect. Background SolidColor untouched.

**PASS** — `Environment hide: disabled 2 renderer(s)`, no survivor warning, and
`canonical_hole1_tee_map_open.png` shows the hole tile alone on the dark map matte: no mountain ring,
no backdrop, no off-tile ground. `before_obgroundskirt_hide.png` is the same frame with only
`MountainBackdrop` hidden — ring gone, skirt still filling the frame — i.e. direct A/B evidence that
both entries are load-bearing.

### §4.4 Clamp pan + zoom
- `PanCamera` clamps `_camFocusPoint` XZ into the OB rect via the new public static
  `ClampPointToRect`, then rebuilds the camera position from the focus so the rig stays rigid.
  Unclamped fallback preserved when there is no OB rect.
- Pinch zoom-out cap: the fallback branch now sets `_zoomOutCapFov = _currentFov` instead of
  `_maxZoom`, so **no** path can zoom out past its own fit. **P-008 closes inverted** — the default
  view IS the zoom-out stop (logged `capFov=45.0`, `_minZoom=30`, so pinch travels 30→45 only).

**PASS (math)** / **pinch + pan gestures need manual on-device verification** — see below.

---

## §5 Acceptance tests

### EditMode — `Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs` (+7 tests)
`mcp__ai-game-developer__tests-run` EditMode, namespace `Golfin.Gameplay.Tests`:
**269 passed, 0 failed, 0 skipped** (whole-suite run: 1007 total, 1003 passed, 1 failed, 3 skipped —
the 1 failure was my own over-tight assertion, since corrected; see below).

| Test | Covers |
|---|---|
| `ShowRegion_ClipProducesAtMostFiveVertices` | ≤5 verts, swept over 11 lies tee→pin |
| `ShowRegion_ContainsBallAndFlag` | region contains ball **and** flag at every lie |
| `ShowRegion_ShrinksMonotonicallyAsTheBallAdvances` | tee ≈ whole rect, strictly shrinking as the ball advances |
| `ShowRegion_BackMarginKeepsGroundBehindTheBall` | near edge is 15 m behind the ball |
| `FitSolver_ProjectsEveryRegionVertexInsideViewport` | all verts in `[0.02,0.98]×[0.04,0.96]`; lowest vert == 0.04 (**K2**) |
| `FitSolver_BallProjectsBelowFlag_RegardlessOfAim` | §11 invariant at 3 lies |
| `PanClamp_KeepsFocusInsideObRect` | inside unchanged; 6 out-of-bounds directions clamped |

All use Hole 1's **real** OB rect / tee / pin and call the production statics — no local copy of the
algorithm (per the "tests target the production type" rule).

**One test was wrong and I fixed the test, not the code.** My first version asserted the tee clip
removes *nothing*. It removes 7.8% — because Hole 1's axis is 13° off the rect, so one rect corner
genuinely lies behind the tee's back edge. Trimming it is correct (it is ground the player can never
play toward), so the assertion became "tee region > 85% of the rect and strictly shrinking".

### Real-flow play mode (Hole 1, tee) — REAL ENTRY PATH
Driven by the existing `MapViewCaptureDriver` **unmodified** (spec §6): ShellScene → Home →
HoleSelection → real `HoleCardController.actionButton` → hole load → real `HoleMap` Button
`pointerDown/Up + onClick` → `mvc.Open()`. No synthetic entry (PIPELINE_HARDENING Rule 2).
Captured at **1170×2532** (Game View forced to the iPhone-14 preset; PNG dimensions verified).

| Check | Result |
|---|---|
| Mountain ring visible | **NO** — `canonical_hole1_tee_map_open.png` |
| Backdrop / off-tile ground visible | **NO** — same frame |
| Ball flush at bottom, green at top | **YES** — ball VP (0.45, 0.06), flag (0.46, 0.55) |
| Aim drag rotates the line, not the world | **YES** — identical pose logged across open + re-aim; tile pixel-identical between `canonical_…` and `hole1_tee_map_reaimed.png` |
| Landing zone / guide line / flag icon render | **YES** — all three visible in both map frames |
| SHOOT closes + writes back aim | **YES** — `s06_map_closed`, then the shot fires (`s07_ball_airborne`, `s08_ball_landed`) |
| Invariant JSON still written | **YES** — `DumpInvariants` untouched; `gameplay_fix0_luma.json` written by the driver as before |
| **World restored after close** | **YES** — `hole1_map_closed_world_restored.png`: sky, trees, ground skirt and all chrome back |
| `MapViewCaptureDriver` compiles unmodified | **YES** — zero edits; it drove all three runs |

### NEEDS MANUAL / ON-DEVICE VERIFICATION
Everything below is either gesture-driven (no `Touchscreen` in the editor harness) or a hole I could
not drive through the real entry point without editing the capture driver (banned by §6):

1. **Pinch to both stops** — cap math is proven (`capFov=45.0`, `_minZoom=30`) but the two-finger
   gesture itself is untested. Expect: zoom-in works, zoom-out stops at the opening frame.
2. **Two-finger pan to all four edges** — `ClampPointToRect` is unit-tested and wired, but the
   gesture path (`HandleTouchInput` → `PanCamera`) was not exercised. This is the highest-value
   manual check: it is the one path that could still reveal the outside world.
3. **Holes 5 and 6, and green-side lies on all three** — verified **numerically** through the
   production solver (table in §4.2) but only Hole 1 tee was verified **visually**. The capture
   driver hardcodes hole 1.
4. **Aim dragged ±90°** — verified at the driver's single re-aim point, not swept to both extremes.

### Out of scope / not regressed (§6)
P-006, P-007 (rings/landing ZTest=Always — untouched), P-009 — no code touched. No scene edits, no
`zones.json` edits, no `MapViewCaptureDriver` edits, no RT/RawImage reintroduction (the banned list
at the top of the file is intact and unmodified). `ShotController` untouched.

---

## Order 354b — "I want the hole to fit the frame. All holes." (Cesar, 2026-08-07)

The first pass framed the **OB rectangle** and left ~35% of Hole 1's frame as matte. Cesar rejected
that and asked for the hole to fill the frame on every hole, with only small residual bands.
Three measured changes, all in the same file:

**1. The region is now the hole's PLAYABLE FOOTPRINT, not the OB rect.**
The rect is only the mask's *bounding box*. I decoded `obMask.maskBase64` (bit SET = OB, per
`BakedZoneClassifier.IsObAt`) and measured the in-bounds area: **Hole 1 is 40.3% of its rect,
Hole 5 is 34.9%.** Framing the rect therefore spent most of the screen on ground the player can
never play — the real reason for the dead band. `TryGetPlayableHull` samples every 8th mask cell,
takes the convex hull (`ConvexHullXZ`, 37–46 verts), inflates it 12 m so the OB line stays visible,
and caches per hole. Falls back to the rect if the mask is unusable.

**2. The near edge backs off until the region matches the frame aspect.**
Clipping at `ball − 15 m` leaves a stubby region near the green (80% down Hole 1: 150 m long ×
167 m wide → a portrait frame wastes 60% of its height). `BuildShowRegionXZ` now takes a target
along/lateral ratio (`FrameAlongOverLateralTarget()` = 2.07 for 1170×2532) and slides the back edge
back — never past the playable area's own near edge — until the region matches. The player sees some
ground behind them instead of half an empty screen; the near edge is still flush at the bottom (K2).

**3. Bounded axis search (`_maxMapAxisTiltDeg`, default 25°).**
A corridor that runs diagonally across its own OB rect cannot fill a portrait frame when framed
exactly on the ball→flag axis. `ChooseMapAxis` sweeps ±25° in 1° steps, scores each with
`ScoreMapAxis`, and keeps the best **subject to the ball staying horizontally centred (0.35–0.65)** —
that constraint is what stops it rotating until the ball hugs a screen edge. A 0.0005/degree penalty
means straight holes stay straight (Hole 6 picks 0°). Set the field to 0 to pin the ball→flag axis.

**Bug caught while verifying:** `ScoreMapAxis`'s `ballX01` was mirrored. With the camera looking
along `axisN` and up = +Y, `Camera.transform.right` is `(axis.z, 0, −axis.x)` — the *negative* of the
`rightN` used in the maths. The centring constraint is symmetric so the axis choice was unaffected,
but the reported value was `1 − x` (predicted 0.35, measured 0.64). Now measured from the lateral
max, with a regression test (`ScoreMapAxis_BallX_IsScreenSpace_NotMirrored`) that cross-checks it
against a real `WorldToViewportPoint`.

### Measured result — production statics, device aspect, 3 holes × 3 lies

`fillX`/`fillY` = fraction of the usable viewport window actually covered by the region.

| Hole | lie | axis tilt | fillX | fillY | ball VP | flag VP | ball below flag |
|---|---|---|---|---|---|---|---|
| 1 | tee | −14° | 91% | 89% | (0.64, 0.07) | (0.25, 0.79) | yes |
| 1 | mid | 0° | 92% | 86% | (0.28, 0.18) | (0.31, 0.72) | yes |
| 1 | approach | 0° | 92% | 86% | (0.30, 0.52) | (0.31, 0.72) | yes |
| 5 | tee | −10° | 88% | 100% | (0.63, 0.08) | (0.31, 0.90) | yes |
| 5 | mid | 0° | 98% | 85% | (0.37, 0.20) | (0.39, 0.73) | yes |
| 5 | approach | +1° | 97% | 85% | (0.40, 0.54) | (0.41, 0.73) | yes |
| 6 | tee | 0° | 99% | 97% | (0.42, 0.16) | (0.43, 0.78) | yes |
| 6 | mid | 0° | 99% | 97% | (0.43, 0.49) | (0.43, 0.78) | yes |
| 6 | approach | 0° | 99% | 97% | (0.43, 0.67) | (0.43, 0.78) | yes |

Against the earlier OB-rect framing (frame coverage 65% / 43% / 81% on holes 1 / 5 / 6 at the tee,
and as low as 34% on an approach lie). Every case still contains the whole playable area, keeps the
bottom flush at 0.040, and keeps the ball below the flag.

Visual, real flow, Hole 1 tee: `screenshots/canonical_hole1_tee_map_open.png` — the hole now runs
corner to corner with only a thin band top and bottom-left.
`screenshots/ob_rect_framing_hole1_tee.png` is the previous OB-rect framing for A/B.

EditMode after 354b: **1010 passed / 0 failed** (13 map tests, up from 7). New coverage: convex-hull
correctness, aspect back-off (extends a stubby region, never past the playable area, leaves a tall
region alone), axis score peaks at the frame ratio, and the ball-x mirror regression.

---

## OPEN ITEM FOR CESAR (one Inspector field)

`_heroTiltDeg` is **serialized as `70`** on the `MapViewController` instance in
`Assets/Scenes/Physics/LabScaffold.unity`. §4.2 asks for 80°; I raised the **code default** to 80,
but a serialized value always wins, so the shipping instance still runs at 70° — confirmed at
runtime (`camY/dist = 1130.8/407.4 = tan(70.2°)`). I did **not** change it because §6 bans scene
edits and §4.2 itself says "tune in Inspector".

This is cosmetic, not a correctness gate: the region fit adapts to whatever tilt is set, and the
canonical screenshot above — taken at **70°** — already shows zero off-tile world, because the far
clip and the environment hide do that job. If you want the steeper look, set `_heroTiltDeg = 80` on
`LabScaffold → …/MapViewController` in the Inspector, or tell me to make the scene edit.

---

## Order 354c — FINAL framing: zoom to the shot, ground stays green (Cesar, 2026-08-07)

> "Obground should still be green. Zoom in as much as possible as long as current ball position
> and flag are visible (leave a bit of margin so none of them touch the borders)"

Two changes, and they simplify the task rather than adding to it.

**1. `ObGroundSkirt` is no longer hidden — the ground outside the tile stays green.**
354's §4.3 hid it so off-tile ground read as a dark matte; Cesar wants the green. Removed from
`_environmentHideNames`, which is now `{ MountainBackdrop, Backdrop, Ring }` — the mountain ring
still goes, the ground does not. `ApplyHoleFittedFarClip` was **removed** with it: a hole-fitted far
clip would have sliced the very ground we now keep and put a black band at the top of the frame.
The serialized `_farClip` (2000 m) is used again; nothing is clipped because at the tighter zoom the
visible ground only reaches ~200 m past the camera. The oversized-survivor diagnostic was removed
too — it would now fire on `ObGroundSkirt` every open and tell the reader to hide the thing we
deliberately keep.

**2. The fit set is the ball and the flag, and nothing else.**
`BuildShotRegion` returns exactly two points. The solve is unchanged — it still returns the
*smallest* containing distance — so "as tight as possible" falls out of it: the ball seats on
`kShotBottomFrac` (0.08) and the flag on `kShotTopFrac` (0.90), and the camera comes no further back
than that requires. The margin is **screen-space** on purpose: a world-space pad would read as a
comfortable gap on a 460 m par 5 and swallow the frame on a 40 m pitch.

The camera axis is the ball→flag axis again, for a reason rather than by reversion: any yaw off it
shortens the on-screen ball→flag separation and would force the camera *further back* — the opposite
of the instruction. That made 354b's whole apparatus dead weight, so it was **deleted**, not left
lying around: the playable hull + `ConvexHullXZ`, `ScoreMapAxis` / `ChooseMapAxis` / `_maxMapAxisTiltDeg`,
`FrameAlongOverLateralTarget`, the polygon clip and the aspect back-off, and their cache. Net −108
lines on the controller vs 354b. (Recoverable from git if the framing brief changes again.)

**One guard added:** `_minFramedSpanM` (serialized, 40 m). The map zooms as tight as the ball and
flag allow, so without a floor a 2 m tap-in would drop the camera a couple of metres off the deck.
The shortfall is padded evenly behind the ball and beyond the flag so the pair stays centred. Set it
to 0 for a pure "as tight as they allow" fit.

`SolveShowRegionPose`'s guard relaxed from `Count < 3` to `Count < 2` — it takes a point pair now,
not a polygon.

### Measured — real flow, Hole 1 tee, 1170×2532

`Shot-fit: pts=2 dist=216.8m camY=607.1 ballVP=(0.50,0.08) flagVP=(0.50,0.90)`

Ball and flag are both dead-centre horizontally and sit exactly on their margins — the tightest
framing that keeps both off the borders. Compare the same tee shot across the three framings:

| Framing | camera dist | ball VP | flag VP | off-tile ground |
|---|---|---|---|---|
| 354 (OB rect) | 407 m | (0.45, 0.06) | (0.46, 0.55) | dark matte, 35% of frame |
| 354b (playable hull) | 251 m | (0.64, 0.07) | (0.25, 0.79) | dark matte, ~8% |
| **354c (ball+flag)** | **217 m** | **(0.50, 0.08)** | **(0.50, 0.90)** | **green** |

Screenshots: `canonical_hole1_tee_map_open.png` (354c) vs `playable_area_framing_hole1_tee.png`
(354b) vs `ob_rect_framing_hole1_tee.png` (354). World restored on close — the sky band in
`hole1_map_closed_world_restored.png` measures RGB (71, 90, 103), i.e. blue.

### Tests — 1003 passed / 0 failed
The 354b test block was replaced with 5 tests that assert the new contract on the production solver:

| Test | Covers |
|---|---|
| `ShotFit_BallAndFlagBothLandInsideTheMargins` | ball AND flag inside `[0.02,0.98]×[0.08,0.90]`, ball below flag, at 5 lies tee→greenside |
| `ShotFit_IsTight_BallSeatsOnTheBottomMargin` | ball lands ON 0.08 — proves it is not zoomed out further than needed |
| `ShotFit_ZoomsInAsTheBallNearsTheFlag` | camera distance strictly decreases as the ball advances |
| `ShotFit_SolverAcceptsATwoPointFitSet` | the `Count < 2` relaxation |
| `ShotFit_BallProjectsBelowFlag_OnEveryHoleGeometry` | §11 invariant swept over 12 world headings |

---

## Order 354d — playfield upright (Cesar, 2026-08-07)

> "The play field is still diagonally placed. I want the rectangle playfield to match the view rectangle"

**Cause.** The playfield is world-axis-aligned — the OB mask is a plain world-XZ grid
(`worldOriginX/Z` + `worldSizeX/Z`, no rotation) and the terrain tile is that grid. 354c pointed the
camera along the ball→flag heading, so the tile came out rotated on screen by however far the hole
runs off the world axis: **13.4° on Hole 1**.

**Fix.** `SnapToWorldAxis` (public static) snaps the camera yaw to the nearest of ±X / ±Z; the
instance wrapper `SnapAxisToPlayfield` applies it when the OB rect is available and
`_alignToPlayfieldAxis` (serialized, default true) is on. On all three sampled holes the nearest axis
is also the field's LONG axis pointing at the flag, so the field stands up in portrait.

**Verified geometrically, not by eye** — the OB rect's four corners projected through the real solved
pose (a pixel edge-detect was useless here, it just found tree canopy):

| | near-edge Δy | far-edge Δy | side symmetry Δ |
|---|---|---|---|
| 354c (ball→flag axis) | 0.1480 | 0.0808 | 0.5325 |
| **354d (playfield axis)** | **0.0000** | **0.0000** | **0.0258** |

Both playfield edges are now *exactly* horizontal on screen. The residual 0.026 is not rotation —
it is the perspective trapezoid: at `_heroTiltDeg = 70°` a ground rectangle projects with its near
edge wider than its far edge (1.30 vs 0.95 in viewport units). **If you want the field to project as
a true rectangle with no trapezoid, that is the `_heroTiltDeg` dial** — 90° is straight down and
removes it entirely. Ties into the open tilt item below.

**Cost, measured — one hole pays for it.** Snapping gives the ball→flag pair a lateral component that
the fit must absorb. Frame vs field, all three holes × three lies:

| Hole | off-axis | tee | mid | approach | field fills frame |
|---|---|---|---|---|---|
| 1 | 13.4° | 549×254 m | 274×127 m | 110×51 m | 100% V / 100% H |
| 5 | **41.5°** | 546×252 m | 273×126 m | 109×50 m | **62% V** / 100% H at the tee, 100/100 after |
| 6 | 5.9° | 186×86 m | 93×43 m | 37×17 m | 100% V / 100% H |

Hole 5 runs 41.5° off its field axis, so its 242 m of lateral spread binds the fit and the tee frame
goes 546 m tall against a 337 m field — green ground above and below (green, not matte, since 354c).
It is the price of an upright field on a strongly diagonal hole; `_alignToPlayfieldAxis = false`
restores the 354c behaviour if you would rather have the tighter frame there.

Real flow, Hole 1 tee: `Playfield align: hole axis (-0.97,-0.23) → (-1,0), 13.4° off` then
`Shot-fit: dist=210.9m ballVP=(0.76,0.08) flagVP=(0.30,0.90)`. Ball bottom-right, flag top-left,
both on their margins — the dogleg now reads as a diagonal aim line inside an upright field.
`canonical_hole1_tee_map_open.png` vs `ballflag_axis_hole1_tee.png` (354c) is the A/B.
World restored on close: sky band RGB (71, 90, 102).

**Tests — 1005 passed / 0 failed**, +2:

| Test | Covers |
|---|---|
| `PlayfieldSnap_PicksTheWorldAxisTheHoleRunsAlong` | ±X/±Z choice for holes 1/5/6 + the cardinal cases; never more than 45° off |
| `PlayfieldSnap_KeepsBallAndFlagInFrame_WithTheLateralSpreadItIntroduces` | both points inside the margins and ball-below-flag on the snapped axis, all 3 holes × 3 lies — including Hole 5's 41.5° worst case |

### Still open
On-device pinch/pan gestures, and `_heroTiltDeg = 70` (see the trapezoid note above — this field now
has a second reason to want tuning). **Holes 5/6 do not need separate visual confirmation for
framing** — the fit depends only on the ball→flag pair and the snapped axis, both covered
numerically above and by `PlayfieldSnap_KeepsBallAndFlagInFrame_…` /
`ShotFit_BallProjectsBelowFlag_OnEveryHoleGeometry` (which sweeps the full circle of hole headings).

---

## Files modified or created

| File | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` | §4.1–§4.4: hole-axis camera, show-region clip + joint fit solve, environment hide/restore + oversize diagnostic, hole-fitted far clip, pan clamp, fallback zoom cap. Replaced `TryGetObExtent` + `FramePlayingAreaWidth`. |
| `Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs` | +7 EditMode tests over the production statics (clip, containment, shrink, back margin, fit, invariant, pan clamp). |
| `Docs/TellCode.md` | Deleted the absorbed K2 `map_view_bottom_anchor` block (§7); updated the SPEC_READY pointer to IMPLEMENTED + the open `_heroTiltDeg` item. |
| `Docs/Specs/Active/map_view_playable_area/{STATUS,IMPLEMENTER_REPORT,HEARTBEAT}.md/.log` | This report + status + baseline. |
| `Docs/Specs/Active/map_view_playable_area/screenshots/**` | Canonical + A/B + restore frames, and the two real-flow runs. |
| `Docs/AI_CONTEXT.md` | Session status. |

`git status --porcelain --untracked-files=all` outside this spec folder shows only the two `Assets/`
files above plus `Docs/TellCode.md`. `Docs/TellCode.md` was **already modified at session start** —
see `HEARTBEAT.log`'s kickoff DIRTY block, which lists ` M Docs/TellCode.md` before any of my edits.
**No scene file is dirty**; `LabScaffold.unity` and `ShellScene.unity` are untouched and the editor
was left in edit mode with only `ShellScene` open and clean.
