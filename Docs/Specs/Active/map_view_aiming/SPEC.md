# SPEC — `map_view_aiming` (Order 352)

> **Tier:** FULL PIPELINE (Tier 3) — visual fidelity + runtime world↔screen aim projection.
> **Status:** SPEC_READY 2026-06-18. Scoping complete (6 forks resolved by Cesar). No Figma — reference is the previous-implementation screenshot (`Docs/Specs/Active/map_view_aiming/reference_old_ui.png` — Architect to confirm Cesar drops the image here; otherwise intent-driven from the description below).
> **Handoff kickoff:** `Use the implementer subagent on "map_view_aiming"`.

---

## 0. One-line intent
Tap the hole-map thumbnail in the Shot UI → a full-screen **hero-angle** live view of the real hole, where the player **drags or taps to aim** (with a mocked trajectory + landing zone + power-band rings), then closes back to the normal shot view. Aim set here carries into the live shot.

## 1. Scope decisions (LOCKED — Cesar, 2026-06-18)
| # | Decision |
|---|----------|
| Render source | **Live camera → RenderTexture** (option B). Static-PNG scaling rejected (kickoff). |
| Camera look | **Hero angle** (tilted bird's-eye, like the reference screenshot). **OVERRIDES** the kickoff's earlier "LOCKED ortho top-down" — a live cam projects markers correctly at any tilt, so the drift worry that drove the ortho lock (a *static-PNG* problem) does not apply. |
| Aim interaction | **Drag AND tap** both set aim. |
| Markers | Ball, flag, draggable landing zone, mocked trajectory + power rings. Hazards render for free (real geometry). |
| Zoom / pan | **Pinch-zoom + pan** supported. |
| 1v1 | **Active player only.** Never opens on a bot turn. |
| Sets | **Aim only.** Power still comes from the swing meter; rings are informational. |
| Exit control | In map mode **all** Shot-UI buttons hide except the club-select button, which is **repurposed to a single Close control** (relabel to **"SHOOT"**), returning to the normal shot view. |

## 2. Anchors (verified live, 2026-06-18 — do not re-derive; cite before editing)
- **Entry widget:** `Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs` — currently a *static sprite swapper only* (`_holeMap` Image, `_holeMaps[18]`, `_defaultHoleMap`). **No Button / onClick / RawImage / RT today** → tap-to-open + RT surface are **net-new** here.
- **Camera rig pattern to adapt:** `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs` — spawns a 2nd `Camera` (perspective, `fieldOfView` 55, `nearClipPlane` 0.3, `depth` 10 renders-on-top), bounds-fits from green/renderer bounds, disables `WalkCamera` while active. **For the map: render to a RenderTexture instead of on-top; keep perspective at a hero tilt; do NOT disable the gameplay camera** (the map is an overlay, the shot scene stays loaded behind it).
- **Renderable geometry:** `Assets/Scripts/Physics/Viewer/LabHoleBinder.cs` (HoleGeo) + `Assets/Scripts/Course/Runtime/GreenTopology.cs` — already loaded in the gameplay scene, so a 2nd cam can render it directly.
- **Aim pipeline (write-back target):** `Assets/Scripts/Gameplay/Input/ShotController.cs` — aim is driven by `CameraHeadingRadians` (NOT a public `SetAimYaw`; `_aimYawRadians` is private). Fade/Draw locks via `FadeDrawLockedAimRad` (public). `PowerNormalized` is read-only externally; `SetExternalPower(power, coneFinetune)` exists but is **out of scope** (aim-only). → **Map aim writes back by setting the aim heading**, so the cone + bent aim line follow downstream for free.
- **Carry distance (landing-zone placement):** `ShotConeView._maxCarryYards` / `SetMaxCarryYards(float)` — same club-carry value the power gauge uses (`PowerGaugeWidget._maxCarryYards`). Landing center = carry along aim. **Single source of truth — read the same value, do not hardcode.**
- **Projection reference:** `ShotConeView` uses `_worldCamera.WorldToScreenPoint(...)` + `RectTransformUtility.ScreenPointToLocalPointInRectangle(...)`. The map's input does the inverse (screen tap → world) via a ray through the map cam onto the ground plane (§5).
- **Bent-line curve math to REUSE:** `Assets/Scripts/Gameplay/UI/ShotUI/AimLineBendRenderer.LateralAtT(t)` — `lateral(t) = SignedFinetune · CurveScale · t² · reach`, clamped. `CurveScale` from `ControlsConfig.AimLineCurveScale` (live value 0.55). **Reuse the parametric form for the map's bent guide line, but render it WORLD-SPACE on the ground (new renderer), not the screen-space `AimLineBendRenderer` MaskableGraphic.**
- **Action-button container:** `Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonsBuilder.cs` builds the Spin/FadeDraw/Golfin/Club buttons. Implementer to locate the exact button refs for the hide/relabel seam (§4). **Do NOT re-run the builder** (Lesson AH: builder re-runs bake latent visual changes — the white-top regression).

## 3. Map render & camera
- New `MapViewController` (MonoBehaviour, `Golfin.Gameplay.UI` or the ShotUI namespace already in use). Owns: the map `Camera`, the `RenderTexture`, the overlay `RawImage`, markers, input, and open/close lifecycle.
- **Camera:** perspective, hero tilt (start ~55–65° from horizontal — tune to match the reference framing), positioned + framed to fit the playable hole between ball and green using the `HoleFlyoverRecorder` bounds-fit approach. Renders only relevant layers (terrain, green, hazards, course meshes; exclude the live HUD).
- **RT:** sized to the device screen (full-res, e.g. 1170×2532 reference); recreate on orientation/size change. Disposed on close.
- **Overlay:** full-screen `RawImage` on its own Canvas above the shot HUD; opening sets it active, closing tears it down. Behind it, the shot scene + its camera stay untouched (no camera-fighting — Lesson set from the fade/draw arc).

## 4. UI / buttons in map mode
- On open: hide the entire action-button row + GOLFIN ball button + wind indicator + settings gear + player card (everything except the club button). Use a reversible toggle (CanvasGroup/SetActive on the container) — restore exactly on close.
- The **club-select button** stays, relabeled **"SHOOT"**, its handler swapped to "close map → return to shot view". On close, label + handler revert.
- **NOTE (implementer):** confirm the exact button GameObjects in `ActionButtonsBuilder`/the Shot-UI prefab and toggle them in the scene/prefab WITHOUT re-running the builder.

## 5. Aim interaction (runtime spatial math — the Tier-3 core)
- **Tap:** map RawImage-local point → screen point → `Ray` through the map camera → intersect the **ground plane** (terrain height at that x/z, or a flat plane at tee/ball Y for v1) → world target point. Aim heading = `atan2(target−ball)` on the x/z plane.
- **Drag:** same projection, continuous while the finger moves the landing-zone handle (or anywhere on the map). Drag the landing target *or* tap empty ground — both re-aim.
- **Write-back:** set the aim **heading** that `ShotController` reads (route through the same `CameraHeadingRadians` aim source the live cone uses — implementer to confirm the exact setter/seam; add a minimal public seam if none exists, mirroring `FadeDrawLockedAimRad`'s pattern, rather than reaching into privates). On close, the live shot view reflects the chosen aim.
- **Clamp:** aim heading clamped to the legal aim range already enforced by the live aim pipeline (do not exceed what the normal cone allows).

## 6. Map overlays (mocked — NOT the deterministic sim)
- **Guide line:** world-space line on the ground, ball → landing target. If **Fade/Draw is armed** (`ShotController.FadeDrawActive`), bend it using the `LateralAtT` parametric form (reused, world-space) so the map agrees with the in-game bent line. Otherwise straight.
- **Landing zone:** world-space ground decal/quad centered at `ballPos + aimDir · carryYards` (carry from `_maxCarryYards`, converted yd→world units consistently with the rest of the sim). Foreshortens naturally under the hero tilt.
- **Power-band rings:** 3 concentric ground rings at **80% / 100% / 120%** of carry (fixed spread for v1), labelled like the reference. **NOTE:** ring *spread* tied to club dispersion / Club-Control is a **v1.1 enhancement** — no per-club accuracy field exists today (only the debug `DebugShotAccuracy` enum), so v1 uses fixed % bands. Do not invent a field.
- **Markers:** ball (origin) + flag/pin as world-space markers rendered by the map cam. Hazards (bunkers/water) appear from the real geometry — no separate markers in v1.
- **Heat gradient** (red→green ideal-landing falloff) and a **roll-out extension line** (Golf Rival style) are **deferred to v1.1** unless trivially cheap.

## 7. Out of scope (v1)
Setting power on the map; per-club dispersion rings; heat-gradient; roll-out line; wind/elevation visualisation on the map; opponent display in 1v1; pan/zoom *limits* tuning beyond sane bounds.

## 8. Acceptance criteria
1. Tapping the hole-map thumbnail opens a full-screen hero-angle live render of the **currently loaded real hole** (verified over a real hole via ShellScene→`BeginGameplayLoad`, never LabScaffold).
2. In map mode only the (relabeled **SHOOT**) button is visible; all other Shot-UI chrome is hidden and restored exactly on close.
3. Ball, flag, landing zone, guide line, and 3 power-band rings are visible and sit correctly on the ground under the hero tilt (no marker drift, no screen-space-circle-on-tilted-ground artefact).
4. **Tap** and **drag** both re-aim; the landing zone + guide line track the chosen aim live.
5. Aim chosen on the map **persists to the live shot** — closing the map shows the cone/aim line at the map-chosen heading, and the fired shot launches on that heading.
6. With Fade/Draw armed, the map guide line **bends in the same direction** as the in-game bent line (sign-faithful to 355/356).
7. Pinch-zoom + pan work and reset cleanly on reopen.
8. Never openable on a bot turn in 1v1; active-player only.
9. Zero edits under `Assets/Scripts/Physics/` (determinism tripwire). Aim-only — no power path touched.
10. EditMode tests: screen→ground projection math (tap point → expected world target / heading) and the mocked carry/ring placement (landing center == carry along aim; rings at 80/100/120%). Curve-reuse sign test if Fade/Draw bend is included.

## 9. Capture / visual gate (Tier-3, per fade/draw-arc lessons)
- Capture over a **real loaded hole** at full device res; **normal play only** (no bespoke `*Gate` scenario, no camera-fighting) — all 3 reviewer agents hard-FAIL a video captured via a bespoke scenario.
- Show: open map → tap-aim + drag-aim → (optionally arm Fade/Draw → bent guide line) → SHOOT/close → fire on the chosen heading → ball flies. One continuous normal-play clip.
- Position-trace / assertion evidence for the projection (not just an open/close event log — Lesson O: dispatch ≠ visual fidelity).

## 10. Open implementation notes for the implementer
- Confirm the exact aim-heading write-back seam in `ShotController`/`ShotConeView`; prefer a minimal public setter mirroring `FadeDrawLockedAimRad` over reaching into privates.
- Confirm yd→world unit conversion used elsewhere (so the landing center matches where the ball actually lands at that power).
- Confirm which layers the map cam should cull to render a clean hole (terrain/green/hazards/course; exclude HUD + lab scaffolding).
- Ground-plane for the raycast: terrain-height sample preferred; flat plane at ball Y acceptable for v1 if terrain sampling is costly — flag which you chose.
