# SPEC — `map_view_aiming` (Order 352) — **v2 (post-escalation reset)**

> **Tier:** FULL PIPELINE (Tier 3).
> **Status:** RESET 2026-06-19 after iter-15 escalation (`ARCHITECT_ESCALATION.md`). v1 architecture (RenderTexture + bot-video-as-gate) is **withdrawn**. This v2 changes the render path and **replaces the verification gate**. Do NOT resume from iter-15 visuals.
> **Reference image:** `reference_old_ui.png` (this folder) — the in-game hole indicator with a LINE to the hole is the flag treatment (see §6).
> **Handoff kickoff:** `Use the implementer subagent on "map_view_aiming"`.
> **Step 0 (before any map work):** apply `Docs/PIPELINE_HARDENING.md` to `route_subagent.py` + `.claude/agents/*` so the iteration breaker, real-entry rule, and math-not-pixels gate enforce on THIS run. Then start from §A (entry point) and do nothing else until it passes.

---

## WHY v2 (read before coding)
The pipeline marked iter-15 `ARCHITECT_REVIEW_PASS` while the feature **could not be opened in the real game** and rendered **upside-down with misaligned markers**. Two root causes, both fixed here:
1. **Wrong gate.** Acceptance was a bot-driven video through a *synthetic* button. v2 gate = **world→screen invariant assertions** (numbers, not pixels) + drive through the **real** entry widget (§11). A bot pressing a fake button can no longer certify a feature a human can't open.
2. **Wrong render path.** RT→RawImage on Mac/Metal flips; "fixes" (uvRect) turned the live map upside-down. v2 = **2nd full-screen overlay camera, NO RenderTexture** (§3).

## A. FIRST MILESTONE — real entry point (nothing else until this passes)
- `HoleCardWidget` (the real Shot-UI thumbnail) becomes a tappable Button whose onClick calls `MapViewController.Open()`.
- `MapViewController` must be present + enabled in the **real gameplay flow** (Practice AND 1v1), not just `LabScaffold.unity`.
- **NO synthetic "HoleMap" button.** Delete `MapViewCaptureDriver`'s fake button; any capture drives the real `HoleCardWidget` onClick.
- **Gate for A:** the capture bot opens the map by invoking the *real* `HoleCardWidget` button; if it can't, FAIL. (This is the assertion that makes the entry-point bug un-hideable.)

## 1. Scope decisions (LOCKED)
| # | Decision |
|---|----------|
| Render source | **2nd full-screen overlay Camera, NO RenderTexture.** (CHANGED from v1.) Map cam renders the real hole at a hero tilt, higher depth than the gameplay cam, clears solid/skybox, draws over the live view. No RawImage, no `uvRect`, no Metal-flip surface. |
| Camera look | **Hero angle** (tilted bird's-eye). Orientation correct **at the camera transform** (behind ball, looking toward green; ball/tee renders at the BOTTOM of frame). Verified by the §11 invariant (ball screenY > flag screenY), never by a uvRect hack. |
| Aim interaction | **Drag AND tap** both set aim. |
| Markers | Ball, hole-indicator-with-line (§6), draggable landing zone, mocked guide line + power rings. Hazards render for free. |
| Zoom / pan | **Pinch-zoom + pan**, driven on the map **camera** (position/FOV) — trivial now that there's no RT. |
| 1v1 | **Active player only.** Never opens on a bot turn. |
| Sets | **Aim only.** Power stays on the swing meter; rings informational. |
| Exit | In map mode ALL Shot-UI chrome hides except the club button, **relabeled "SHOOT"**, repurposed to close → return to shot view. Restore exactly on close. |

## 2. Anchors (verified live)
- **Entry widget:** `Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs` — `Image` sprite-swapper, **no Button today** → §A wires it.
- **Camera-rig pattern:** `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs` — 2nd-camera bounds-fit (perspective, FOV 55, nearClip 0.3, depth-above). Adapt to a runtime overlay cam (NOT editor-only, NOT RT).
- **Renderable geometry:** `LabHoleBinder` (HoleGeo) + `GreenTopology` — already loaded.
- **Aim pipeline / write-back:** `ShotController` — aim driven by `CameraHeadingRadians`; no public `SetAimYaw` (add a minimal public seam mirroring `FadeDrawLockedAimRad`, do not touch privates). `PowerNormalized` read-only externally — out of scope (aim-only).
- **Carry:** `ShotConeView._maxCarryYards` / `SetMaxCarryYards` (same value `PowerGaugeWidget` uses). Landing center = carry along aim. Single source of truth.
- **Projection:** `_worldCamera.WorldToScreenPoint(...)` (as `ShotConeView` does) — used for BOTH marker placement and the §11 invariant dump.
- **Curve reuse:** `AimLineBendRenderer.LateralAtT(t)` (`lateral = SignedFinetune·CurveScale·t²·reach`, clamped; `CurveScale` from `ControlsConfig.AimLineCurveScale`, live 0.55). Reuse the parametric form for the world-space guide line.
- **Hole indicator:** `HoleIndicatorWidget` + `Assets/Art/In-Game UI/Icon - Flag.png` — the normal-shot indicator with a LINE to the hole (§6).

## 3. Map render & camera (overlay, no RT)
- `MapViewController` owns a runtime overlay `Camera`: hero tilt (~55–65° from horizontal, tune to reference), bounds-fit ball↔green via the `HoleFlyoverRecorder` approach, depth above the gameplay cam, clears solid/skybox, culls to terrain/green/hazards/course (exclude HUD + lab scaffolding). The gameplay cam stays alive behind it.
- **No RenderTexture, no RawImage.** Orientation is a property of the camera transform — get it right there.
- Pinch-zoom = adjust cam distance/FOV; pan = move the cam target within hole bounds. Reset on reopen.

## 4. UI / buttons in map mode
- On open: reversibly hide the action-button row + GOLFIN + wind + settings + player card (CanvasGroup/SetActive on the container — do NOT re-run `ActionButtonsBuilder`, Lesson AH). Restore exactly on close.
- Club button stays, relabeled **"SHOOT"**, handler swapped to close. Revert on close.

## 5. Aim interaction (runtime spatial math)
- **Tap/Drag:** map-screen point → `Ray` through the overlay cam → ground-plane (terrain height sample preferred; flat plane at ball Y acceptable v1 — flag which) → world target. Heading = `atan2(target−ball)` on x/z.
- **Write-back:** set the aim **heading** `ShotController` reads (minimal public seam). On close, the live cone/aim reflect it and the fired shot launches on it.
- **Clamp** to the legal aim range the live pipeline enforces.

## 6. Map overlays (mocked — NOT the sim) — markers must stay COHERENT
All three of {guide line, landing zone, power rings} share ONE aim direction and origin. The §11 gate asserts they project to a single screen line — this is the fix for iter-15's "three directions."
- **Guide line:** world-space line on the ground, ball→landing target. Bends via `LateralAtT` (world-space) when `FadeDrawActive`, else straight.
- **Landing zone:** **shader-driven radial gradient projected on the ground** (URP Decal Projector or a ground-projector shader) centered at `ball + aimDir·carry`. **NOT a flat textured quad** (iter-15 clipped at the tilt).
- **Power rings:** **projected decal/shader annuli** (translucent, correct width, render OVER terrain, conform to slope) at **80/100/120%** of carry. **NOT flat `LineRenderer`/mesh at sampled height** (iter-15 clipped under terrain). Fixed-% spread v1 (no per-club accuracy field exists; Club-Control spread = v1.1).
- **Hole indicator:** the in-game **`HoleIndicatorWidget` style — icon + LINE pointing to the hole** — projected to the pin's screen position. **NOT** an 18× `Flag.fbx` mesh, **NOT** a bare flag icon dropped on the pin. Fix the pin world pos (`HoleContext.PinWorld`) so it sits on the green.
- **Ball marker:** at `ballWorldPos`, must be in-frame (gate asserts).

## 7. Out of scope (v1)
Power-on-map; per-club dispersion rings; heat-gradient polish beyond the radial shader; roll-out line; wind/elevation viz; opponent display; RenderTexture anything.

## 8. Acceptance criteria (ALL hard-gated; §11 defines the automated checks)
1. Map opens by tapping the **real** `HoleCardWidget` in Practice AND 1v1 (real `BeginGameplayLoad` hole, never LabScaffold-only). [§11-A]
2. Map content is **right-side up** at the source (ball/tee bottom, green top) with **no `uvRect` flip and no RenderTexture** anywhere in the path. [§11 ball/flag screenY invariant]
3. Ball, hole-indicator-with-line, landing zone, guide line, 3 power rings all visible, on the ground, conforming to and rendered OVER terrain. [§11 + visual]
4. Guide line, landing center, and ring labels are **collinear / co-located** on one aim line. [§11 alignment invariant]
5. Hole indicator points at the pin and sits inside the green. [§11 flag invariant]
6. Tap and drag both re-aim; markers track live.
7. Aim chosen on the map persists — fired shot launches on that heading. [§11 write-back invariant]
8. Fade/Draw armed → guide line bends sign-faithful to 355/356.
9. Pinch-zoom + pan work; reset on reopen.
10. Never openable on a bot turn; active-player only.
11. Zero `Assets/Scripts/Physics/` edits. Aim-only.
12. EditMode tests: screen→ground projection, carry/ring placement (center==carry; rings 80/100/120%), curve-bend sign, write-back heading round-trip.

## 9. Capture (artifact, NOT the gate) — fix the CAUSE of flips, don't catch them
**Root cause (corrected 2026-06-19, per Cesar):** flipped frames are NOT a Metal/Recorder fact — Cesar's `HoleFlyoverRecorder` flyovers record through Unity Recorder with no flips. The iter 6–15 flips were **self-inflicted** by the indirection we added: RT→RawImage (RenderTexture sampling origin differs across graphics APIs) → then a `uvRect` flip to "fix" it → then `yflip_repair.py` to patch the output. Remove the indirection and there is nothing to catch.
- One continuous clip of **real play**: tap the real `HoleCardWidget` → map opens → tap-aim + drag-aim → (arm Fade/Draw → bent line) → SHOOT/close → fire on chosen heading → ball flies.
- **Capture via the PROVEN flip-free path** = exactly what `HoleFlyoverRecorder` already does: tag the map overlay camera and point Unity Recorder's **`CameraInputSettings` `TaggedCamera`** input at it. **No RenderTexture, no RawImage, no `uvRect`, no GameView-overlay composite capture, no `yflip_repair.py`.** This is the same mechanism Cesar uses that does not flip.
- **No flip-detector as a gate.** Orientation is already verified by the §11 invariant (`ball.screenY > flag.screenY`) — projected coordinates, not pixels. If a captured frame is ever flipped, that is a **regression signal that an indirection was re-introduced** → fix the capture path, NEVER detect-and-repair. (If frames are ever sampled at all, decode **consecutive** frames; `ffmpeg -ss` keyframe sampling stays banned as structurally blind.)
- The clip is for Cesar to glance at. **It is NOT the pass/fail gate** — §11 is.

## 10. Implementer notes
- Add the minimal public aim-heading seam on `ShotController`.
- Confirm yd→world unit conversion (landing center must match real landing at that power).
- Confirm map-cam culling layers.
- Ground-plane raycast: terrain sample vs flat plane — flag which.

## 11. AUTOMATED VERIFICATION GATE (the real pass/fail — works with no human)
At capture, `MapViewController` (editor/bot build) dumps `map_view_invariants.json` of projected screen coords + world refs at ≥2 aim states. The reviewer + red-team agents assert on it (no eyeballing, no frame-pixel guessing). **Any failed assertion = hard FAIL; PASS is impossible without all passing.**

| Assert | Catches (iter-15 issue) |
|---|---|
| Map opened via the REAL `HoleCardWidget.onClick` (not a synthetic GO) | entry point never wired (#1) |
| `ball.screenY > flag.screenY` (ball lower on screen) AND ball in viewport rect | upside-down map (#2), ball off-frame (#8) |
| `landingCenter.screen`, `label100.screen`, `aimLineEnd.screen` collinear within tol | bands/labels/line in 3 directions (#3) |
| `flagIndicator.screen ≈ WorldToScreenPoint(pinWorld)` AND pinWorld inside green bounds | floating flag (#7) |
| ring/landing materials flagged as projected-decal/shader, depth-test OVER terrain | lines under terrain, clipped quad (#4,#5) |
| `firedHeadingRad ≈ mapSetHeadingRad` | dead aim write-back |
| No `RenderTexture`/`uvRect` in the map path; no banned capture API; no `Assets/Scripts/Physics/` diff | architecture regressions |

Reviewers re-run the **entire §8 list** every pass — not just the last-named symptom. A report making any claim not backed by the JSON or a tool result = automatic FAIL + logged to `.claude/review_misses.log`.
