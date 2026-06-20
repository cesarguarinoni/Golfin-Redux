# SPEC — `map_view_aiming` (Order 352) — **v2 (post-escalation reset)**

> **Tier:** FULL PIPELINE (Tier 3).
> **Status:** RESET 2026-06-19 after iter-15 escalation. v2 architecture (overlay cam, no RT) is **correct and FROZEN** (see §F). **iter-21 REVISION (2026-06-20):** the six remaining issues are all *visual-model* defects, not architecture — fixed by the single-endpoint model in **§6-MODEL** + the extended gate in **§11+**. Do NOT reset; do NOT touch §F.
> **Reference image:** `reference_old_ui.png` (this folder) — the in-game hole indicator with a LINE to the hole is the flag treatment (see §6).
> **Handoff kickoff:** `Use the implementer subagent on "map_view_aiming"`.
> **Step 0 (before any map work):** apply `Docs/PIPELINE_HARDENING.md` to `route_subagent.py` + `.claude/agents/*` so the iteration breaker, real-entry rule, and math-not-pixels gate enforce on THIS run. Then start from §A (entry point) and do nothing else until it passes.

---

## WHY v2 (read before coding)
The pipeline marked iter-15 `ARCHITECT_REVIEW_PASS` while the feature **could not be opened in the real game** and rendered **upside-down with misaligned markers**. Two root causes, both fixed here:
1. **Wrong gate.** Acceptance was a bot-driven video through a *synthetic* button. v2 gate = **world→screen invariant assertions** (numbers, not pixels) + drive through the **real** entry widget (§11). A bot pressing a fake button can no longer certify a feature a human can't open.
2. **Wrong render path.** RT→RawImage on Mac/Metal flips; "fixes" (uvRect) turned the live map upside-down. v2 = **2nd full-screen overlay camera, NO RenderTexture** (§3).

## v2.1 MODEL CORRECTION (Cesar, 2026-06-19) — this OVERRIDES §3/§6 where they conflict
Root cause of the iter-16..18 grey-void / giant-rings / off-field-framing: the carry/landing/ring **model** was wrong, and we kept patching the *rendering*. Corrected model (authoritative):
1. **Carry = the SELECTED CLUB's real carry** for the current shot — NOT a fixed driver carry. `PhysicsLabController.ComputeMaxCarryYards()` is driver-locked (75 m/s, 10.9°); the map must NOT use that for the rings/landing. Source the selected club's carry from the club data (the same per-club distance the club button should show — see `task_6d0326e9` `ClubContext.SelectedDistance`). On a short approach the carry is small → landing sits near the pin, not 100 m past it.
2. **Rings are CONCENTRIC — nested one inside the other — centered on the LANDING SITE, not the ball.** (iter-19 refinement, Cesar 2026-06-19.) They show where the ball lands at 80/100/120% power, drawn as **three THIN concentric rings sharing a common center at the landing zone**: 80% innermost, 100% middle, 120% outermost — nested one inside the other **exactly as in `reference_old_ui.jpg`** (open it and match the nesting). iter-19 drew them as a separated/offset cluster — make them properly concentric. NOT huge full-carry circles centered on the ball; NOT an offset smudge. Ring line weight must be THIN relative to the map.
3. **Camera must be TIGHT — ZOOM IN so NOTHING outside the playing field is visible.** (iter-19 refinement.) iter-19 framing was much better but still showed off-field at the edges. Zoom in further: the playable field fills the frame and the off-field/skybox/dark borders are NOT visible at all. Frame to ball + landing + pin, then tighten until the field edge is at/beyond the viewport.
6. **Hole indicator — yellow flag ICON is ACCEPTED for v1** (Cesar 2026-06-19); the upgrade to the real shot-UI flag widget WITH the line pointing to the hole is a **future task** (`task_` — see follow-up), NOT a v1 blocker.
4. **The shot-UI gameplay ball must NOT appear in the map view.** The live "G" GOLFIN ball from the Shot UI is bleeding through. Exclude it from the map camera's culling mask (and/or the gameplay ball's layer); the map draws its OWN ball marker only.
5. **Open from a REAL ball-at-tee in a real loaded hole.** The 40 m ball-to-pin seen in capture is a lab/default ball position — no tee is 40 m from its pin. The capture/§A must use the real `BeginGameplayLoad` tee (this is also the §A real-entry requirement, still unmet).

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

---

# iter-21 REVISION (2026-06-20) — governing sections below supersede any conflicting earlier text

## §F. FROZEN — working v2 parts, DO NOT REGRESS (Cesar: "don't break the map again")
These are correct as of iter-21. The iter-21 fix touches the overlay-drawing methods of `MapViewController.cs` + the §11 validator + one `controls.csv` field ONLY. Do not modify, refactor, or "improve" any of these:
- Overlay **camera + render path** (no RenderTexture, no `uvRect`, no flip). §3.
- Real `HoleCardWidget` **entry/open/close**. §A, §4.
- **Club carry** source `_maxCarryYards` (124 yd, NOT driver 154). §2.
- **Tight framing** (no off-field grey), **shot-UI ball cull**, **capture via TaggedCamera**. §3, §9.
- Untampered **§11 validator exits 0**, **`Assets/Scripts/Physics/` diff empty**.
A diff that changes camera/render/entry/carry/framing/capture = automatic FAIL.

## §6-MODEL. CANONICAL AIM MODEL — one shared endpoint L (supersedes the ad-hoc per-element formulas)
The six issues exist because guide line / rings / labels / landing zone / flag / open-aim were each computed with separate constants. They are now ALL derived from one point **L**.

**Aim & L:**
- **Open aim = the natural heading the shot already has.** In `Open()`: set `_aimYawRadians = _savedAimYaw` where `_savedAimYaw = _shotController.CameraHeadingRadians` (the value already saved at L341). **DELETE the L368-373 flag-aim override** (`AimYawTowardFlag()` → `_aimYawRadians`) and **do not** reset `_savedAimYaw` to the flag aim. Clamp reference = the natural heading. (Issue #2 — stops aiming into OB.)
- `aimDir` = horizontal unit vector of that heading.
- `carry` = `_maxCarryYards` (yd→world), unchanged.
- **L (shared landing endpoint)** = `ball + aimDir·carry`, plus, when Fade/Draw armed, the lateral term `aimPerp · LateralAtT(1)·carry`. L is the single center for the guide-line end, all rings, all labels, and the landing zone.

**Guide line (issues #3, #4):**
- Smooth curve ball→**L**, 24 verts, t∈[0,1]: x/z = `lerp(ball, L, t)`; **Y = `lerp(ballY, L.Y, t) + arcBow·sin(πt)`** (small bow, `arcBow≈1.5 m`). Reads as a trajectory.
- **Do NOT set Y from `SampleTerrainHeight` per vertex** — that caused the "straight-with-2-bumps" terrain-hugging. Fade/Draw lateral via `LateralAtT(t)` so the curve still **ends exactly at L**.

**Rings (issues #1, #4) — concentric at L:**
- Three concentric rings centered at **L**, radius `r_p = carry · RING_FRAC · (p/100)` for p∈{80,100,120}; `RING_FRAC` = new `controls.csv` / `ControlsConfig` field, default **0.15** (same pattern as `AimLineCurveScale`). → r80<r100<r120, all centered on L. Thin white stroke, drawn ON TOP of the landing zone.

**Labels (issue #1):**
- One per ring at its **far edge along +aim**: `labelPos_p = L + aimDir·r_p`. → stacked along the aim line, **120 (outer, far/top) → 100 → 80 (inner, near/bottom)**, each sitting on its own ring. White. (Exactly Cesar's described layout.)

**Landing zone (issue #6):**
- Red→green **radial-gradient** decal centered at **L**, radius `r80·0.9` (sits inside the inner ring). Red center → green edge, semi-transparent. Drawn BEFORE (under) the rings but **alpha-visible — must occupy visible pixels, not be fully occluded**. (Replaces the white/yellow disc.)

**Flag indicator (issue #5) — POSITION fix only:**
- Source the pin from **`GreenTopology.GetDefaultPin()`** (authored canonical pin, no arg — the loaded hole's instance), feeding `HoleContext.PinWorld` / `_flagWorldPos`. NOT the name-matched "Flag" GO or `GreenCentroid` fallback. Must sit **inside the green bounds**. The **accepted v1 yellow flag icon (v2.1 #6) then sits at the correct point**. Do NOT build the real flag-widget + line-to-hole here — that is future `task_7d4fdd3a`; this pass corrects the pin POSITION only.

**Framing:** keep L and ball on-screen at the natural aim. **Do NOT re-aim to fit framing.**

## §11+. EXTENDED GATE — the six visual requirements as deterministic asserts
The iter-21 lesson: §11 was green while six visual things were wrong because it asserted *weaker* properties. These additions make a green gate MEAN "matches the model." Add to `map_view_invariants.json` + the validator; any failure = hard FAIL.

| Assert | Catches |
|---|---|
| all three `ring.center.screen` == `guideLineEnd.screen` == `L.screen` within tol | rings/line misalignment (#4) |
| `ring.radius_p` ≈ `carry·RING_FRAC·(p/100)` (ratios 0.8:1.0:1.2) within tol | arbitrary radii (#4) |
| label screen positions monotonic along +aim, order **120 far → 100 → 80 near**, each at its ring's far edge | clock-positioned labels (#1) |
| `openAimYaw` == `CameraHeadingRadians` (natural) within tol; NOT == `AimYawTowardFlag()` | aiming at OB (#2) |
| guide-line vertex heights are a smooth curve (max |2nd-difference| < tol) AND **not equal to per-vertex terrain height** | terrain-hugging "2 bumps" (#3) |
| `flagWorldPos` == `GetDefaultPin()` within tol AND inside green polygon bounds | flag in fairway (#5) |
| landing-zone decal present, gradient material (red center→green edge), center alpha>0, occupies ≥N visible px, draw-order below rings | invisible/wrong-color zone (#6) |

If any assert cannot be computed, that is a FAIL (not a skip).

## Convergence note (why this should land in ~1 pass where 6 didn't)
The blind loop is good at mechanical/structural fixes and bad at "match this picture." Anchoring all six to one **L** turns the visual-coherence problem into mechanical wiring, and the §11+ asserts turn "looks right" into numbers the gate fails on. The loop's strength now applies to exactly the thing it kept missing.
