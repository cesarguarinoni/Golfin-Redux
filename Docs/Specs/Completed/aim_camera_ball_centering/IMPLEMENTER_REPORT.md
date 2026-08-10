# IMPLEMENTER_REPORT — `aim_camera_ball_centering`

**Iteration shape:** `aim_camera:ball_not_centered_and_too_far`
**Date:** 2026-08-10
**Canonical screenshot:** `screenshots/hole1_tee_aim_full.png` (1170×2532)
**Supporting screenshots:** `screenshots/hole1_tee_ball_zoom.png`, `screenshots/hole1_fairway_aim_full.png`, `screenshots/hole1_fairway_ball_zoom.png`, `screenshots/hole1_tee_markerL_zoom.png`, `screenshots/hole1_tee_markerR_zoom.png`

---

## Baseline (preflight)

- HEAD at kickoff: `84a2b1e8797cdd2e836efef82f0456a4ac90c7fc`
- DIRTY at kickoff (`git status --porcelain --untracked-files=all`):
  ```
   M Assets/Scripts/UI/Gacha/GachaCarouselController.cs
   M Assets/Scripts/UI/ModeSelect/ModeCardController.cs
   M Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs
   M Docs/Specs/Completed/shot_ui_translucency_glow/ARCHITECT_REVIEW.md
   M Docs/TellCode.md
  ?? Docs/Specs/Active/aim_camera_ball_centering/
  ```
- Those five paths are **pre-existing** and untouched by this task — they appear verbatim in the kickoff DIRTY block above and none of them is in the "Files modified or created" table below.

---

## Files modified or created

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | New `[Header("Aim framing")]` tunables + `_centralBallWidget`; `_teeMarkerPositions` cache (filled in `OnHoleLoaded`, cleared in `OnHoleUnloaded`); `ApplyCameraYaw` internals rewritten (signature unchanged, putter path verbatim); new `GetAimBallViewportY` / `BallIsOnTee` / `ComputeAimDistance` + two `internal static` pure solvers. |
| `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` | One-line `public RectTransform Rect => _rect;` accessor (+ doc comment). No behavior change. |
| `Assets/Scripts/Physics/Tests/AimCameraFramingTests.cs` (new) | 17 EditMode tests: projection contract across FOV 50/60/70 × vy 0.40/0.4234/0.50, tee-clamp closed form, cap, no-clamp, along-track term, and an end-to-end "clamped pose keeps markers on screen" case. |
| `Assets/Scripts/Physics/Tests/AimCameraFramingTests.cs.meta` (new) | Unity-generated meta for the above. |
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` | **Pass 2 (Cesar).** `AimChaseCameraAtCup` no longer carries its own copy of the legacy 8/3 framing — it now delegates to `PhysicsLabController.ApplyAimCameraAt`, so there is exactly one aim-framing implementation. |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | **Pass 2 (Cesar).** In-flight follow tightened `_followDistance` 3 → 2, `_followHeight` 1.8 → 1.2 (uniform ×2/3; framing angle unchanged). |
| `Assets/Scenes/Physics/LabScaffold.unity` | Wired `LabRoot._centralBallWidget → CentralBall` (6 lines, 5 of them the new tunables at defaults) **+ pass 2:** serialized `_followDistance: 3 → 2`, `_followHeight: 1.8 → 1.2` on the ChaseCamera (scene values override the C# defaults, so both had to move). Diff verified line-by-line — no `m_IsActive`, `sizeDelta`, or transform churn. |

---

## Acceptance checklist

### 1. EditMode test — solver projects the ball at (0.5, targetVy) — **PASS**
`SolveAimCameraPose_ProjectsBallAtTargetViewportPoint` is a `[Values]` matrix over FOV {50, 60, 70} × vy {0.40, 0.4234, 0.50} at portrait aspect 1170/2532. It drives the real solver, applies the pose to a real `Camera`, and asserts on **Unity's own `WorldToViewportPoint`** (position-trace per Lesson O — not a re-implementation of the trig, and not event dispatch). All 9 combinations pass with `vp.x == 0.5 ± 0.01`, `vp.y == targetVy ± 0.01`, `vp.z > 0`. Two extra tests pin the camera placement (`ball − lookDir·d + up·h`, ball distance 3.31 m) and the yaw mapping (+X lookDir ⇒ yaw 90°).

### 2. EditMode test — tee clamp math — **PASS**
- `SolveAimDistance_WideMarkersPullTheCameraBack`: ±2 m markers ⇒ `d == 2 / (tanHalfH · safeFrac)` exactly (8.33 m), and `> 3` (the base).
- `SolveAimDistance_IsCappedAtMaxDistance`: same markers with `maxDistanceM = 8` ⇒ `d == 8` (test also asserts the unclamped requirement genuinely exceeds the cap, so the fixture can't pass vacuously).
- `SolveAimDistance_NarrowMarkersLeaveTheCloseFraming`: ±0.5 m ⇒ `d == 3.0` (base), with a sanity assert that ±0.5 m really does already fit.
- Plus `NoMarkersMeansNoClamp` (empty list and null) and `MarkersAheadOfTheBallNeedLessPullBack` (along-track offset subtracts 1:1).

**Suite result:** `Golfin.Physics.Tests` EditMode — **1050 tests, 1047 passed, 0 failed, 3 skipped** (the 3 skips are pre-existing `HoleCompleteDriverTests` Stage-C1 skips, unrelated). Full-assembly run, so this is also a no-regression proof for the other 1033 tests.

**Evidence the new tests actually executed** (the MCP `tests-run` tool ignores class/method filters and only reports non-passing rows, so a green summary alone proves nothing): I inverted one assertion to `Assert.AreEqual(999f, dNull)` and re-ran. The suite returned `FailedTests: 1` naming `Golfin.Physics.Tests.AimCameraFramingTests.SolveAimDistance_NoMarkersMeansNoClamp — Expected: 999.0d, But was: 3.0d`. Assertion restored; re-run returned 1047/0/3. 1050 − 1033 baseline = the 17 new cases.

### 3. PlayMode on Hole 1 tee — **PASS** (with a measured caveat, §Deviations D2)
Driven through the **real player entry path**: play mode from `ShellScene` → `SplashScreen/StartButton.onClick` (PLAY) → HomeScreen PRACTICE card `PlayButton.onClick` → `HoleSelectionScreen/HoleCard(Clone)/ExpandedContainer/ActionButton.onClick` (Hole 1 "NEXT"). No synthetic buttons, no direct scene load. Result: `ShellScene + LabScaffold + Hole_01_Geo`, `IsHoleReady=True`, ball at `(219.43, 11.46, 34.73)`.

Live measurement from the actual gameplay camera (resolved via the `ChaseCamera` component, not `Camera.main`):

```
chaseMode=Chase  isPutt=False  onTee=True  markersCached=2
cam.fov=60.00  cam.aspect=0.4621  cam.pos=(225.670, 12.858, 36.224)  cam.euler=(12.31, 256.58, 0)
ComputeAimDistance -> d=6.417 m      GetAimBallViewportY -> 0.5000
camera->ball distance = 6.568 m      (legacy framing was 8.54 m)
*** WorldToViewportPoint(ball) = (0.5000, 0.5000, z=6.568)
    target from live widget    = (0.5000, 0.5000)   dx=0.0000  dy=0.0000
    TeeMarker_regular_L (219.50,11.58,33.24) -> viewport (0.0946, 0.5231) z=6.81  inFront=True
    TeeMarker_regular_R (219.36,11.58,36.23) -> viewport (0.9406, 0.5087) z=6.27  inFront=True
ball angular diameter 0.3725 deg vs legacy 0.2865 deg  ->  1.30x larger
```

Both Figma-fidelity rows hold: ball ≡ widget point to **0.0000** on both axes; both markers' viewport X inside `[0.05, 0.95]` with `z > 0`, and both report `Renderer.isVisible = true`. `screenshots/hole1_tee_ball_zoom.png` (a 360 px crop centred on the projected point, upscaled 2×) shows the white 3D ball sitting inside the "G" of the translucent 2D `CentralBall` sprite.

**Measured marker offsets and resulting d (spec §4 reporting requirement):**

| Marker | World | Lateral (m) | Along-track (m) |
|---|---|---|---|
| `TeeMarker_regular_L` | (219.50, 11.58, 33.24) | −1.474 | +0.278 |
| `TeeMarker_regular_R` | (219.36, 11.58, 36.23) | +1.474 | −0.278 |

Horizontal FOV at vFOV 60 / aspect 0.4621 = **29.88°**, `tanHalfH = 0.2668`, `safeFrac = 0.9` ⇒ `d = 1.474 / 0.2401 + 0.278 = ` **6.417 m** (the R marker binds; the cap at 8 m is *not* reached). So the tee is **not** forced back to ≈8 m — the §4 escalation trigger did **not** fire — but 6.42 m is also nowhere near the 3 m close-up. See §Deviations D2 for the honest read.

### 4. Stroke 2+ (fairway lie) uses the full close-up — **PASS**
Ball repositioned 200 m down the tee→pin line via `PhysicsLabController.PlaceBallAt` — the **production** reposition path (`RepositionBallWithLookDir`, the same one the OB drop and water drop call), not a test-only seam.

```
ball=(24.87, 7.95, -11.63)  distFromTee=200.0 m
BallIsOnTee=False           ComputeAimDistance=3.000
cam.pos=(27.786, 9.347, -10.930)  euler=(25.02, 256.58, 0)
camera->ball = 3.311 m   (legacy 8.54 m; tee 6.57 m)   -> 2.58x larger on screen
WorldToViewportPoint(ball) = (0.5000, 0.5000)   dx=0.0000  dy=0.0000
```

3.311 m and 25.02° pitch match the spec's own derivation (§3: "θ_ball = 25.0°, ball distance 3.31 m") exactly. Before/after at the same lie is the arithmetic above: 8.54 m → 3.31 m. `screenshots/hole1_fairway_aim_full.png` shows the 3D ball rendering as a distinct, clearly readable ball inside the 2D sprite.

### 5. Orbit drag keeps the ball pinned — **PASS**
Swept `_cameraYaw` in 45° steps through a full 360° and called the real `ApplyCameraYaw` at each step (the exact call `HandleCameraOrbit` makes). At all 9 yaws: `viewport = (0.5000, 0.5000)`, `dx = dy = 0.0000`, `dist = 3.311 m`. Yaw restored afterwards.

**Map-view open→close: PARTIAL → treated as FAIL-safe/unverified.** `MapViewController.WriteBackAim` reaches `ApplyCameraYaw` by reflection on the name `"ApplyCameraYaw"` with a `Camera` parameter. I verified the live signature is unchanged (`Void ApplyCameraYaw(UnityEngine.Camera)`, confirmed by reflection lookup) so the reflection call still binds and now restores the *new* framing for free — but I did not open and close the map view in play mode. **Needs manual confirmation** (§Manual verification).

### 6. Putter aim framing byte-identical — **PASS**
The putter branch is the pre-change body copied verbatim. Byte-compared against `HEAD` with indentation stripped:

```
$ git show HEAD:...PhysicsLabController.cs | grep -A4 "void ApplyCameraYaw" | grep cam.transform
            cam.transform.position = _orbitCenter - lookDir * 8f + Vector3.up * 3f;
            cam.transform.LookAt(_orbitCenter + lookDir * 3f + Vector3.up * 0.5f);
$ diff legacy.txt putter.txt  ->  IDENTICAL (ignoring indentation)
```

The gate is `if (CurrentShotIsPutt) { ...legacy...; return; }` placed before any new code runs, so the putter path cannot reach the solver. **Live putt not exercised** — see §Manual verification.

### 7. Aim→chase handoff has no jarring pop — **PARTIAL / unverified**
Arithmetically the gap closed: `ChaseCamera` follows at 3 m / 1.8 m, the new fairway aim pose is 3 m / 1.4 m (was 8 m / 3 m), so the handoff discontinuity shrank from ~5 m to ~0.4 m of height. I did not fire a shot, so the *feel* is unverified. **Needs manual confirmation.**

### 8. `AdjustCameraForDepression` still applies — **PASS (by construction), unverified on a real depressed lie**
Untouched. It writes `chaseCamera.FollowHeightOffset`, a separate channel from the transform `ApplyCameraYaw` writes, and it is called after `RepositionBallWithLookDir` exactly as before (`PhysicsLabController.cs:818`). No depressed lie (bunker) was reached in this session. **Flagged unverified per spec.**

### 9. All `[SerializeField]` references wired — **PASS**
Read back from the saved scene, not from the write call: reopening `LabScaffold.unity` and reflecting on `_centralBallWidget` returns `CentralBall`, whose `Rect` is a 150×150 RectTransform under root canvas `ShotUI_Canvas` (ScreenSpaceOverlay, rect 1170×2532). `GetAimBallViewportY()` returns `0.5000`. The probe was read-only — scene `isDirty=False` before and after.

### 10. Unity Console has no errors — **PASS**
`EditorUtility.scriptCompilationFailed = False`. Console after refresh contains only pre-existing `CS0618`/`CS8632` obsolete-API warnings in unrelated editor scripts (`VersusResultScreenBuilder`, `HoleFlyoverRecorder`, the Inventory builders, …). Zero errors, zero warnings attributable to this task.

---

## Figma fidelity

The spec's contract table is an alignment contract, not a UI layout (no UI element changes).

| Element | Reference | Property → value | Measured (live, Hole 1) | Verdict |
|---|---|---|---|---|
| 3D ball projection, full-swing aim (tee) | `CentralBallWidget` rect | `WorldToViewportPoint(ball)` == widget viewport point ± 0.01 both axes | `(0.5000, 0.5000)` vs target `(0.5000, 0.5000)` — dx 0.0000, dy 0.0000 | **PASS** |
| 3D ball projection, full-swing aim (fairway) | `CentralBallWidget` rect | same | `(0.5000, 0.5000)` — dx 0.0000, dy 0.0000 | **PASS** |
| Tee markers, tee-shot aim | Hole 1 `TeeMarker_regular_*` | all viewport X ∈ [0.05, 0.95], viewport Z > 0 | L `(0.0946, z=6.81)`, R `(0.9406, z=6.27)` | **PASS** |

---

## Deviations from spec

**D1 — `GetAimBallViewportY` computes in root-canvas rect space, not `screenPoint.y / Screen.height`.**
Spec §2 proposed `RectTransformUtility.WorldToScreenPoint(null, rect.position).y / Screen.height` plus a render-mode branch. I instead transform the widget's world position into the root canvas's own rect (`canvasRect.InverseTransformPoint(rect.position).y`, normalized by `rect.height`). Why: (a) it makes the render-mode branch the spec asked for unnecessary — the canvas rect maps 1:1 onto the viewport for both Overlay and Screen-Space-Camera; (b) `Screen.height` reports the **Game View window** size in Editor play mode, not the render height, which would have silently skewed the ratio during exactly the kind of in-editor verification done above. Same intent, strictly more robust. Result verified live: returns 0.5000 against a 1170×2532 `ShotUI_Canvas`.

**D2 — The live widget is at viewport Y 0.5000, not the mockup's 0.4234. Consequence: the ball is centred at 50% down, not ~57.7%.**
Spec §Reference derives 0.4234 from `InGame Shot Tests 9.png` but mandates that runtime derive the value from the **live** widget ("constant is the fallback only"). The live `CentralBall` sits at `anchoredPosition (0,0)` on a canvas whose rect is centred — measured viewport Y **0.5000**. Nothing repositions it at runtime (grepped: no code writes its rect). So the solver pins to 0.5000.
The primary contract — *3D ball lands on the same screen point as the 2D ball* — holds exactly either way, and that is what both screenshots show. But the Goal's secondary phrasing ("~57.7% down") will not be true until the 2D widget itself moves. **I did not move it** (explicitly out of scope: "no 2D shot-UI move/resize"). If the mockup position is the intended one, that is a separate 2D-widget task, and this camera solver will follow it automatically with no code change. **Architect call.**

**D3 — Tee framing lands at 6.42 m, not the 3 m close-up. Reported, not unilaterally fixed (spec §4).**
Numbers in §3 above. The §4 escalation trigger as written ("forces d back to ≈8 m") did not fire — 6.42 m is a real 1.30× improvement and the cap is untouched — but it is also only 25% of the way from 8.54 m to the 3.31 m fairway framing, and the tee zoom crop shows the ball still reading small against the 150 px 2D sprite. The binding constraint is the portrait horizontal FOV: 29.88° total. Levers, in the order I'd rank them, **for the Architect to rule on**: (a) `_teeMarkerSafeFrac` 0.9 → 1.0 gives 5.80 m (markers touch the screen edge); (b) constrain only the nearer marker rather than both; (c) a tee-only FOV bump — the only lever that actually reaches ~3 m, and explicitly out of scope here. I changed nothing.

**D4 — `ComputeAimDistance` takes the `Camera` as a parameter.**
Spec §4 sketched `ComputeAimDistance(Vector3 lookDir)` fetching the camera via `chaseCamera.GetComponent<Camera>()`. `ApplyCameraYaw` already holds the `Camera` it was handed, so passing it through avoids a redundant `GetComponent` and a second null path, and keeps the solver honest when a caller passes a camera that isn't `chaseCamera`'s (e.g. the map-view restore).

**D5 — The tee-marker cache also covers the `SurfaceMarker` fallback branch.**
Spec §4 said to cache alongside the `regularMarkers` scan. `OnHoleLoaded` has two branches that can produce the tee midpoint (named `TeeMarker_regular_*`, else `SurfaceMarker` tee GOs). I populate the cache in **both**, so the clamp isn't silently dead on a hole that only has the fallback. On Hole 1 the named branch wins (2 markers).

**D6 — `_teeMarkerPositions` is deliberately not serialized.**
`_savedTeeWorldPos` next to it *is* serialized. I chose plain (spec's wording: "a new field `List<Vector3>`") to avoid baking per-hole state into `LabScaffold.unity`. Cost: after an Editor domain reload *while sitting on the tee in play mode*, the cache is empty and the clamp silently no-ops (framing goes to the 3 m close-up, markers may leave the frame). Recompiling during play mode is already banned by project convention, and `OnHoleLoaded` rebuilds the cache on every real hole load, so this is editor-only. Documented in a code comment.

**D8 — `ChaseCamera` and `BotDriver` were edited, both explicitly out of scope in the original SPEC.**
SPEC §5 says "No `ChaseCamera` / `LoopCameraDirector` edits (in-flight framing unchanged)" and §Architecture says `ChaseCamera` is "do NOT touch". Cesar directed both in a follow-up: *"Fix botdriver as well. Camera distance is ok, but make it closer for the chase cam so the ball appears bigger then."* `LoopCameraDirector` remains untouched, and no aim-phase tunable changed. Details in §Pass 2.

**D7 — Standing-ban note: this task edits `Assets/Scripts/Physics/`.**
CLAUDE.md PIPELINE_HARDENING rule 7 lists "ZERO edits to `Assets/Scripts/Physics/`" as a standing ban. This SPEC explicitly and repeatedly mandates editing `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`, and the ban's intent is the deterministic sim (`Physics/Core`, `Physics/Runtime`, `Physics/Math`) — none of which is touched here. Zero sim files changed; the edit is confined to the Viewer's camera framing. Flagging rather than assuming.

---

## Pass 2 — Cesar follow-up (2026-08-10): BotDriver + closer chase cam

> *"Fix botdriver as well. Camera distance is ok, but make it closer for the chase cam so the ball appears bigger then."*

Aim distances left untouched per "camera distance is ok" — `_aimCamDistanceM` stays 3.0, `_aimCamHeightM` stays 1.4, tee clamp still lands at 6.42 m. Verified unchanged after this pass (live: tee `cam→ball = 6.568 m`, `viewport (0.5000, 0.5000)`).

### 2a. BotDriver — **PASS**
Rather than copy the new framing into `BotDriver` (which would have re-created the exact drift that caused the problem), I removed the duplication at its root:

- `PhysicsLabController.ApplyCameraYaw` is now `internal` (was private). **`MapViewController`'s reflection still binds** — it queries with `Public | NonPublic | Instance`, and `internal` is NonPublic to reflection. Verified live with those exact flags: `ApplyCameraYaw` resolves to `Void ApplyCameraYaw(UnityEngine.Camera)`.
- New `internal void ApplyAimCameraAt(Camera, Vector3 ballPos, float yawRadians)` seam sets exactly the state the production path holds at rest (`_orbitCenter = ball`, `_cameraYaw = yaw`, `ShotController.CameraHeadingRadians`) and delegates to `ApplyCameraYaw`. Taking the ball explicitly is what makes it safe for the bot — it never has to assume `_orbitCenter` is already current.
- `AimChaseCameraAtCup` is now 4 lines of intent and zero camera math; it bails with a `LogStep` if no controller is present rather than silently framing wrong.

Duplication check: `grep -rn "lookDir \* 8f\|up \* 3f" Assets/Scripts --include=*.cs` returns **exactly one hit** — `PhysicsLabController.cs:1105`, the putter branch. The legacy magic numbers now exist in precisely one place, by design.

### 2b. Closer chase camera — **PASS**
`_followDistance` 3.0 → **2.0**, `_followHeight` 1.8 → **1.2** — a uniform ×2/3 scale, chosen so the *framing angle is unchanged* (30.96° before and after) and only the distance closes. That is the narrowest reading of "make it closer": nothing about the shot's look changes except how big the ball is.

Measured live on Hole 1 by driving the production `RunLateUpdateLogic` through its `FrameCamera(dt)` seam with a live target (the in-flight condition — a non-null target is what makes the Chase branch own the transform; with a null target it early-returns and cedes to `ApplyCameraYaw`), converged over 400 ticks:

```
height above ball = 1.200 m   (serialized _followHeight = 1.2)
XZ distance       = 2.000 m   (serialized _followDistance = 2)
camera->ball      = 2.332 m   OLD = 3.499 m  ->  1.50x larger on screen
ball viewport     = (0.500, 0.500, z=2.33)
framing angle     = 30.96 deg (OLD 30.96 deg — unchanged by design)
```

Screenshot: `screenshots/chase_inflight_2m.png`, 2× crop `screenshots/chase_inflight_ball_zoom.png` — visibly larger than the 3.31 m aim crop. **Caveat on that frame:** I forced the chase branch while the shot UI was still in aim state, so the 2D ball sprite and club handle are still drawn; in a real shot the shot UI hides. The ball size and camera geometry in the frame are real.

Handoff arithmetic: aim 3.31 m → chase 2.33 m, so the launch transition is now a ~1 m push-in (was 8.54 → 3.50, a 5 m rush outward). `smoothTime` on the live LabScaffold ChaseCamera is 0.15 s, which absorbs it. **Feel is still unverified** — see §Manual verification item 3.

Both stay `[SerializeField]`, so dialling them is an Inspector nudge with no rebuild. Keep `_followHeight ≈ 0.6 × _followDistance` to hold the 31° angle.

### 2c. Regression run
`Golfin.Physics.Tests` EditMode after pass 2: **1050 total / 1047 passed / 0 failed / 3 pre-existing skips.**

**One honest note on that run:** the first execution after these edits reported 2 failures — `AudioEmitterTests.MinInterval_SecondBounceWithinInterval_IsSuppressed` (expected 1, got 2) and `Golfin.UI.Tests.GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav` (expected True, got False). An immediate re-run with no code change returned fully green. Both are in subsystems this task never touches (bounce-audio min-interval timing; bottom-nav teardown — not even in the physics assembly), and both are state/timing sensitive, so I am recording them as **flaky, not caused by this change** — but recording them rather than quietly re-running until green.

### 2d. Not changed in pass 2
The four **lab** scenes that also carry a ChaseCamera (`PhysicsLab_Hole1`, `PhysicsLab_Range`, `PhysicsLab_Dashboard`) keep their serialized `3 / 1.8` — saving them re-introduces the unrelated 9-line serialized-default backfill documented below, and they are not the production path (`BeginGameplayLoad` loads `LabScaffold`). They will read as the old framing if opened directly. Say the word and I'll sweep them in a dedicated commit.

---

## Things I found but did not change

- **Hole 1 carries four tee sets** (`back` / `front` / `regular` / `ladies`, 8 markers). Only `regular` is scanned, which is correct — the HUD reads "HOLE 1 - REGULAR" — so the clamp is keyed to the tee actually in play. Noting it because a future "tee selection" feature would need the scan to follow the selected set.
- **`PhysicsLab_Hole1.unity` is stale**: saving it to wire the widget also backfilled 9 lines of serialized defaults from *other* tasks that never re-saved that scene (`ShotConeView._glowController`, five `ShotController` flick tunables, `CentralBallWidget._defaultThumbnail`). I reverted that scene entirely (it is a lab scene, not the production path; `LabScaffold` is what `BeginGameplayLoad` loads) so this task's diff stays clean. The staleness is pre-existing and unrelated.
- **`git` is broken on this machine**: `/usr/bin/git` shims through Xcode and now fails with `You have not agreed to the Xcode license agreements` (exit 69). Worked around with `DEVELOPER_DIR=/Library/Developer/CommandLineTools`. The real fix needs Cesar's password (`sudo xcodebuild -license`, or `sudo xcode-select -s /Library/Developer/CommandLineTools`).

---

## Manual verification still required (cannot be closed from here)

1. **Map view open → close** restores the *new* framing (signature-compat proven; the round trip is not).
2. ~~**Putter aim** on a green — code is byte-identical, but a live putt confirms the gate actually takes the legacy branch.~~ **SUPERSEDED by § Pass 3** — the putter branch is no longer the legacy pose; it now centres the ball. Verified live on the Hole 1 green.
3. **Aim → chase handoff feel** after firing a real shot (no pop).
4. **`AdjustCameraForDepression`** on a bunker/depressed lie.
5. **On-device (iPhone) framing** — everything above is Editor play mode at 1170×2532; `cam.aspect` on device should match, but the tee clamp is aspect-sensitive by construction, so it is worth one device look.
6. **Cesar's eyes on D2 and D3** — those are judgment calls, not defects.

---

## Editor state

~~Left exactly as found: not playing, `ShellScene` (active) + `Hole_06_Geo` (additive), both clean.~~ **CORRECTED — that was wrong, and Cesar caught it.** I found `Hole_06_Geo` open additively at session start, treated it as his working state, and deliberately re-opened it after each play-mode run to "leave the editor as found." The clean-state target is **absolute, not relative**: `ShellScene` only, clean, not playing. A stray `Hole_NN_Geo` at session start is a previous run's leftover, not state to preserve — every generated course scene carries a `WalkCamera` that locks the cursor on play, which is what blocks testing. Recorded in the `feedback_leave_editor_clean` memory as a named anti-pattern. Final state at close: `ShellScene` only, clean, not playing.

`ProjectSettings/Packages/com.unity.probuilder/Settings.json` was auto-touched by Unity on play-mode entry and has been reverted (it re-dirties on every scene open; reverted each time).

---

## Pass 3 — Cesar follow-up AFTER close-out (2026-08-10, commit `5d938c9a8`)

> *"You did not center the camera during putting."*

This post-dates the folder's move to `Completed/`, so the commit — not this folder — is the primary record.

### What changed
SPEC §3/§5 had explicitly scoped putting out and kept the legacy pose verbatim; I implemented that and flagged it. The consequence Cesar saw: during a putt the ball sat ~62% down screen while every other aim state pins it under the 2D ball.

`ApplyCameraYaw`'s `CurrentShotIsPutt` branch now runs the same `SolveAimCameraPose` as the full swing, at its **own** distance/height. `_puttCamDistanceM` / `_puttCamHeightM` default to the legacy **8 m / 3 m deliberately** — the putt view has to fit the 15 m `PutterAimLine` and the green-reading grid, so this pass changes *where the ball sits on screen*, not how much green is visible. Both are `[SerializeField]` if the putt should close in later.

### Measured (live, Hole 1 green, real entry path, `IsPutt=True`, ball 2.63 m from the cup)
```
cam.pos=(-233.74, 13.35, -62.38)  euler=(20.56, 162.26, 0)
camera->ball  = 8.544 m           (legacy stand-off, unchanged)
ball viewport = (0.5000, 0.5000)  target (0.5000, 0.5000)  dx=0.0000 dy=0.0000
cup  viewport = (0.5000, 0.5616)  onScreen=True
```
Pitch 12.8° → 20.56° is what lifts the ball to centre. Setup used the production `SetClub(3)` + `PlaceBallAt` seams (pre-calculated state rather than playing the hole out — Cesar: *"Pre-calculate the shots so you stop burning cycles"*).

### Test rewrite — the first version was a circular gate
My first putt test called `SolveAimCameraPose` directly with hardcoded `8f, 3f`. It never entered `ApplyCameraYaw`, never set `CurrentShotIsPutt`, never read the new fields — **it would have passed whether or not the putter branch changed at all.** Cesar asked to see it, which is what surfaced this. Replaced with:

- `ApplyCameraYaw_PutterBranch_CentersTheBallAtTheLegacyStandoff` — real `PhysicsLabController` + real `ShotController(IsPutt=true)`, driven through `ApplyAimCameraAt`, asserting on Unity's own `WorldToViewportPoint` plus the 8.544 m stand-off.
- `PutterCameraTunables_DefaultToTheLegacyStandoff` — pins the 8/3 defaults so closing the putt camera in is a deliberate act.

**Proven to catch a revert** rather than asserted: reverting the branch to the legacy `LookAt` failed the test by name — `putt viewport Y — legacy pose fails here / Expected: 0.4234 / But was: 0.38211` — and it passed again on restore.

**Suite:** 1111 total, 1107 passed, 3 pre-existing skips. The 1 failure (`GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav`) is intermittent in an untouched subsystem — `AudioEmitterTests.MinInterval_SecondBounceWithinInterval_IsSuppressed` failed on the previous run and passed on this one. Both recorded as flaky rather than re-run until green.

### Video deliverable
`videos/close_camera_and_putt_centering.mp4` — 27 s, 1170×2532, captioned, **flip-verified across all 1058 consecutive frame pairs** (keyframe sampling misses flips). Beats: tee aim 6.57 m → next lie 3.31 m → chase 2.33 m → putt aim centred with the cup directly above. Raw uncaptioned kept alongside; copy in `Docs/Reports/Media/`. `videos/` is gitignored by design, so these are local artifacts.

Honest notes on that clip: the last 9 s were **trimmed** because the raw ended on a `SUCCESS / STROKES: 2 (ALBATROSS)` card — an artifact of teleporting between beats, not real play, and misleading to ship. The mid "next lie" beat landed in trees again (a bad lie pick, not a framing problem). Two full-res clips were recorded this Unity session, both requiring the documented `Reset Video Session Guard` override; a third should wait for a relaunch.

### Still unverified after Pass 3
- **The green-reading grid does not appear in the putt frames.** The blue aim line does show once the putt is struck. Grid visibility is the *entire* justification for holding the putt camera at 8 m, so this needs one real putt (arrived at by play, not teleport) to confirm it is a setup artifact.
- Items 1, 3, 4, 5 in § Manual verification above still stand.
