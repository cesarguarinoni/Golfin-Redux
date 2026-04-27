# PHASE_8_SHOT_UI_POLISH — Wire real Figma art into the shot UI

> **Handoff:** `Docs/Specs/Active/PHASE_8_SHOT_UI_POLISH.md`
> **Status:** 8.1 + 8.2 DONE (2026-04-27). 8.2.5 next, then 8.3–8.8.
> **Update 2026-04-27:** Inserted Part 8.2.5 (Club Handle sprite swap) — was missing from the original phasing. The `ClubHandle` Image needs to display the club-type-appropriate sprite from `Assets/Resources/Clubs/Controls/`.
> **Branch:** `phase-8-shot-ui`. Pre-merge tag: `pre-phase-8`.
>
> **Input:** Cesar's Figma (`Docs/Reference/In-game UI/In-game GUI.fig`) + frames in same folder + assets already imported under `Assets/Resources/...` and `Assets/Art/In-Game UI/`. Reference frames PNGs:
> - `Initial State.png` — baseline layout (no aiming yet, no buttons)
> - `Pull Back.png` — mid-pull, club handle visible at apex, cone visible
> - `Timing Arrows.png` — timing state, slabs traveling up cone, color bands visible
> - `Straight Shot.png` — button row visible, Straight mode
> - `Fade.png` — button row visible, Fade/Draw mode
> - `Selector - Ball.png` — ball-selector overlay
> - `Selector - Club.png` — club-selector overlay
> - `In-Game - Shot Tests 9.png` — reference (existing visual standard)

## Goal

Replace placeholder shot-UI visuals with the real Figma art. Add the missing HUD elements (player card, hole card, wind indicator, hole-direction indicator, power gauge, settings, action button row, ball-and-club selectors). No architecture changes — the existing `ShotController` event seam, `ShotConeView` coordinator, and asmdef boundaries are correct as-is. We're updating visuals + adding new presentation widgets that subscribe to existing data.

---

## 🚨 Lessons baked in (read before touching anything visual)

8.1 took 6 commits and a lot of back-and-forth. The pattern: spec under-specified visual fidelity, Code shipped attempt 1 without a side-by-side check against the reference, Cesar pushed back, Code iterated narrowly without re-reading the reference for a fresh comparison. Don't repeat this. Concrete rules below.

### Rule 1 — Reference comparison BEFORE the first commit

No visual element ships its first attempt without a side-by-side screenshot vs the named reference frame, attached to the message that asks Architect to look. If you commit + report "done, please review" without that side-by-side, **your work is rejected on procedural grounds** — the iteration starts there, not at the screenshot you took 30 seconds ago. The screenshot lives at `Docs/Diagnostics/phase-8/{part}/{element}-attempt-{n}.png`.

### Rule 2 — Surface visual ambiguity BEFORE coding it, not after

If the spec doesn't specify a visual property the reference visibly has (gradient, alpha, edge softness, curvature, drop shadow, etc.), STOP and ask Architect. Do not invent a default. Examples Code should have asked about during 8.1 rather than guessing:
  - "Reference cone has a center-to-edge gradient and curved base — the spec just says 'semi-transparent grey'. Should I add the gradient + curvature?"
  - "Cone silhouette in the reference is visibly soft-edged. The spec doesn't mention anti-aliasing. Add a feather strip?"
  - "Bands in the reference are crisp; the cone fill is fuzzy. Should bands have any alpha fade or stay opaque?"

The correct posture: "I see X in the reference that the spec doesn't mention. I'll wait." Not: "I'll ship a plain version and fix it after the bug report."

### Rule 3 — When patching geometry/shader code, dump intermediate values

During 8.1 the cone rendered at 600px instead of 1009px because a `[SerializeField]` value baked into `LabScaffold.unity` overrode the C# default. Code initially blamed the math. The fix: log `_heightPx` at runtime in `OnPopulateMesh` to confirm what value the renderer actually uses. **For any procedural mesh / shader work, the first debug step is to log every input parameter.** Don't trust code defaults to reach the renderer when serialized scenes exist.

### Rule 4 — Anti-aliasing fades follow the perpendicular-to-edge direction

This bit Code in 8.2 (the early version of the cone polish): a horizontal x-axis alpha fade on a diagonal silhouette softens the wrong direction. For any procedural shape with non-axis-aligned edges, the feather is a separate strip whose inner edge sits ON the silhouette (opaque) and outer edge is offset perpendicular to the silhouette direction (transparent). Keep feather widths small (≤3px canvas units) so the visual silhouette doesn't extend past where structural elements (band lines, frame stops) end.

### Rule 5 — Disable visibility-modifying components during visual testing

`ConeAlphaController` drives `CanvasGroup.alpha = 0.25` in `Idle` state. Code took a screenshot in Idle and reported "the cone looks fine" — then Cesar opened the same scene and saw a near-invisible cone. Always disable any `CanvasGroup` / fade controller that would mute the element you're checking, take the screenshot at full alpha, then re-enable. Note this in the diff post.

### Rule 6 — Per-element budget tightened

- **Functional attempts: 2 max.** If the element doesn't compile, doesn't bind to data, doesn't react to state events after 2 attempts — stop and surface.
- **Visual fidelity rounds: 5 max** (was "unbounded within reason"). After 5 visual rounds without convergence, stop, post all 5 screenshots side-by-side with the reference, and tell Architect what you can't get to match. Probable cause: spec missed a property or the asset itself doesn't match the Figma render.
- 8.1's visual loop took 6 rounds. The new ceiling is 5. If you're at round 3 and the gap is still big, that's a strong hint to surface now rather than push to 5.

### Rule 7 — If the work touches a serialized scene, patch the scene

When changing a code default for a `[SerializeField]` field, the running game still uses the value baked into any scene that has the component. Either patch the scene YAML (`LabScaffold.unity`, `ShotConeTest.unity`) or invoke the Unity-MCP equivalent of right-click → Reset on the component. The done report must say which scenes were re-saved and which serialized values were updated.

---

## Scope boundaries

**In scope:**
- Cone outline (semi-transparent grey gradient fill + 3 colored band lines) — **DONE in 8.1**.
- Timing arrow slabs (trapezoidal horizontal bars filling the cone width at slab Y; color cycles red → amber → green per progress) — **DONE in 8.1**.
- Real club-handle sprite from `Assets/Resources/Clubs/Controls/` (selected via current equipped/preset club name).
- Real ball thumbnail at the ball-position center from `Assets/Resources/Balls/Thumbnails/`.
- Player card top-left: portrait + rarity background + USERNAME / Lv N / Turn N rows.
- Hole card top-right: name + hole label + par + hole map thumbnail from `Assets/Art/In-Game UI/HoleMaps/`.
- Settings gear icon (top-right corner).
- Wind indicator (top-left, below player card) — arrow + speed text.
- Hole-direction indicator (always on a fixed horizontal line; tail points toward hole; off-screen clamping; collision rule with wind indicator).
- Power gauge (right side, mid-screen): round dial, fills clockwise green→maroon as power 0→120%, shows `{pct}%` and `{yards} yd`.
- Bottom action row (visible during Aiming/Pulling/Timing): Spin button, Ball button, Mode button (Straight/Fade/Draw), Club button.
- Ball selector overlay (opens on tap-and-hold of Ball button; vertical scrollable strip of equipped balls; closes on lift). Same for Club selector.
- Targeting line restyle using `Assets/Art/In-Game UI/Indicator - Trail.png` 9-slice or stretched.

**Out of scope (defer):**
- Camera UI, scoreboard mid-shot, mini-map, weather effects.
- Per-rarity ball / character full content — we use whatever `BagManager.Instance` exposes today, or fallback to defaults.
- Wiring the ball selector / club selector to actually swap clubs mid-shot — the selectors render and animate, but on-select they call a single `OnClubChanged(clubKey)` / `OnBallChanged(ballKey)` event that `PhysicsLabController` already wires for the picker dropdown. Same hook, new UI.
- Localization — strings stay English-only for v1. JP wiring is its own task.
- Animation polish for the gauge ring (interpolation curves can be lerp now; tween library not allowed).
- Style restyling of the existing `PhysicsLabUI` debug panel — that's a separate dev UI, untouched here.
- ChaseCamera centering of the ball in portrait view in `LabScaffold` — known issue from 8.1, deferred to a separate camera task.

---

## Architecture decisions (Architect-locked)

For each visible element, classify into one of three buckets and use the matching technique. **Code does NOT decide; this list is the bucket assignments.** If an assignment looks wrong during impl, surface to Architect rather than swap silently.

| Element | Bucket | Technique | Notes |
|---|---|---|---|
| Cone outline | **Procedural** | `ConeMeshGraphic` (DONE) | Subdivided mesh, gradient + curvature + perpendicular feather + 3 band lines, all in `ConeBandPalette`. |
| Timing arrow slabs | **Procedural** | `TimingSlabGraphic` (DONE) | Trapezoid sized to cone width at slab Y, color from `ConeBandPalette`. |
| Targeting line | **Sprite** | `Indicator - Trail.png` 9-sliced or stretched | Already an asset; just bind it. |
| Power gauge ring | **Procedural** | New `PowerGaugeGraphic : MaskableGraphic` | Filled circular sweep; gradient green→maroon; OK to start with simpler 4-stop solid color now and tighten later. |
| Power gauge background | **Sprite** | `Indicator - Power.png` | Already an asset. |
| Player card frame, hole card frame, wind/hole indicator frames | **Sprite** | `Indicator - Wind-Hole.png` 9-sliced | Already an asset; navy rounded rect. |
| Action button frames (Spin/Ball/Mode/Club) | **Sprite** | `Button - All.png` 9-sliced | Already an asset; gradient outline preserved. |
| Action button icons (Spin / Fade-Draw / Straight / Settings / Flag) | **Sprite** | `Icon - *.png` series | Already imported. |
| Club handle sprite | **Sprite** | `Clubs/Controls/S_Controls_<type>_<brand>.png` | Selected by current club index from `PhysicsLabController`; hardcoded brand per type in v1 (Part 8.2.5). |
| Club button icon | **Sprite** | `Clubs/Portraits/S_Menu_<type>_<brand>.png` | If size breaks Unity import, generate smaller PNGs alongside; do NOT downscale via `TextureImporter` if quality drops — commit smaller exports. |
| Ball thumbnail (centerpiece + selector + button) | **Sprite** | `Balls/Thumbnails/S_Controls_Ball_<n>.png` | |
| Character portrait | **Sprite** | `Portraits/Mini/<n>.png` | Use Mini; fall back to Thumbnails if Mini doesn't fit at the displayed size. |
| Rarity background tile | **Sprite** | `Rarities/<Rarity>.png` | Behind portrait; tile or scale to portrait frame. |
| Hole map thumbnail | **Sprite** | `In-Game UI/HoleMaps/Lomond - Hole N.png` | Selected by current loaded hole. |
| Username / Lv / Turn / Hole text / Par / yds / mph / % / yards | **TMP** | TextMeshProUGUI | Use Roboto Black or whatever the project's primary TMP asset is — verify by reading any existing inventory screen prefab. |

**Why this split:**
- Cone, slabs, gauge are procedural because they're stat-driven (cone width by Accuracy, slab Y by ArrowProgress, gauge fill by power). A sprite for each would need a full rebuild on every state tick.
- Everything else is hand-authored art with brand identity — sprites are correct.
- Buttons use a shared frame sprite + 9-slice so we don't ship 4 button PNGs that look identical. The icon PNG is layered inside.

---

## Reference paths (verified to exist in repo)

```
Assets/Art/In-Game UI/
  Aiming Cone.png            (reference — cone shipped in 8.1; future parts shouldn't need it)
  Button - All.png            (button frame)
  Icon - DrawFade.png
  Icon - Flag.png
  Icon - Settings.png
  Icon - Spin.png
  Icon - Straight.png
  Indicator - Info.png        (small info chip frame)
  Indicator - Power.png       (power gauge frame)
  Indicator - Trail.png       (targeting line)
  Indicator - Wind-Hole.png   (navy rounded card frame)
  HoleMaps/Lomond - Hole {1..18}.png

Assets/Resources/Clubs/Controls/S_Controls_<type>_<brand>.png
Assets/Resources/Clubs/Portraits/S_Menu_<type>_<brand>.png
Assets/Resources/Balls/Thumbnails/S_Controls_Ball_<n>.png   (also Golfin.png and PuttAce.png shorter names)
Assets/Resources/Portraits/Mini/<n>.png
Assets/Resources/Rarities/{Common,Uncommon,Rare,Mythic,Legendary,Supreme,Mask}.png
```

**Asset-naming gotcha:** In `Balls/Thumbnails/`, `Golfin.png` and `PuttAce.png` are short-name variants. The `S_Controls_Ball_*.png` family is the standard set. Code should resolve by name with fallback: `S_Controls_Ball_<n>.png` first, then `<n>.png`, then a default.

---

## Phasing

Land each part, run tests if applicable, report, wait for Architect ack before next part. **Do not chain parts.**

- **Part 8.1** — Cone restyle + timing arrow slabs. **✅ DONE 2026-04-27.** See `Docs/Diagnostics/CONE_MESH_ITERATION_LOG.md`.
- **Part 8.2** — Power gauge widget. **✅ DONE 2026-04-27.**
- **Part 8.2.5** — Club Handle sprite swap by club type. (~30min) Hardcoded brand-per-type for v1; binder reads from `PhysicsLabController` club index.
- **Part 8.3** — Player card + hole card + settings icon. (~1.5h) Static-data widgets reading from `CharacterManager.Instance` (or fallback) and a new `HoleContext` static accessor.
- **Part 8.4** — Wind indicator + hole-direction indicator with collision. (~2h) Both top-left/top-center area.
- **Part 8.5** — Action button row (Spin / Ball / Mode / Club) + matching frames/icons. (~1.5h)
- **Part 8.6** — Ball selector + Club selector overlays. (~2h)
- **Part 8.7** — Centerpiece ball thumbnail + targeting line restyle. (~30min)
- **Part 8.8** — Final polish pass + tests + screenshots + Cesar smoke test. (~1h)

Remaining estimate: ~8.5–9.5 hours of Code time (8.2.5 adds ~30min).

---

## Visual fidelity protocol (apply at every part)

This is the protocol that 8.1 violated. It is mandatory.

### A. Before writing any code
1. Open the named reference frame for this part (filename listed in each part's section).
2. List visual properties present in the reference: shape, gradient, alpha, edge softness, drop shadow, stroke, corner radius, spacing, font weight, font size relative to parent, icon size relative to button, etc.
3. Cross-check against the spec. For every property in the reference that the spec does NOT mention, **STOP and post one message to Architect listing them**, asking which to honour. Do not start coding until Architect responds.

### B. First implementation
1. Implement per spec + Architect responses.
2. Disable any `CanvasGroup` / fade controller that would mute the element. Note which were disabled.
3. Take screenshot at 1080×1920 (`screenshot-game-view`).
4. Save under `Docs/Diagnostics/phase-8/{part}/{element}-attempt-1.png`.
5. Open both PNGs side-by-side. If your eye picks up any mismatch (size, colour, alignment, opacity, edge quality), fix it before posting. Don't ship attempt 1 to Architect knowing it's wrong.

### C. Diff report (the message to Architect)
Every part's done report includes:
- Final attempt screenshot path.
- Reference frame filename.
- Side-by-side image (overlay or two-up). Use Python + PIL via `start_process`:
  ```python
  from PIL import Image
  ref = Image.open("Docs/Reference/In-game UI/<frame>.png")
  unity = Image.open("Docs/Diagnostics/phase-8/{part}/{element}-attempt-{n}.png")
  # match dimensions, paste side-by-side, save
  ```
  Save to `Docs/Diagnostics/phase-8/{part}/{element}-diff-final.png`.
- Bullet list of remaining deviations with reasoning ("can't replicate X because asset doesn't have Y" / "acceptable because it's only visible at Z zoom" / etc.).
- Confirmation that no `CanvasGroup`/fade was hiding the element during the screenshot. Note which controllers were temporarily disabled.
- Confirmation that any `[SerializeField]` defaults you changed are also patched in scenes that have the component (LabScaffold.unity, ShotConeTest.unity, etc.).

### D. Iteration budgets
- **Functional: 2 attempts max.** If it doesn't compile / bind / react to events after 2 attempts — surface.
- **Visual: 5 rounds max.** If you're at round 3 and the gap is still big, surface earlier; the spec or the assets are likely the problem, not your math.

### E. Reference frames (already in repo)
- `Docs/Reference/In-game UI/Initial State.png` — baseline composition
- `Docs/Reference/In-game UI/Pull Back.png` — pull state with gauge filling
- `Docs/Reference/In-game UI/Timing Arrows.png` — timing slab + bands visible
- `Docs/Reference/In-game UI/Straight Shot.png` — button row, Straight mode
- `Docs/Reference/In-game UI/Fade.png` — button row, Fade/Draw mode
- `Docs/Reference/In-game UI/Selector - Ball.png` — ball selector overlay
- `Docs/Reference/In-game UI/Selector - Club.png` — club selector overlay
- `Docs/Reference/In-game UI/Aiming Cone.png` — cone-only reference

---

## Common rules across all parts

1. **Do not modify** `BallSimulation.cs`, `ShotController.cs` state-machine logic, `ShotInputBuilder.cs`, or anything under `Assets/Scripts/Physics/Core/`. The contract is fixed. New data needed by UI gets added to `ShotInputState` only with explicit Architect approval.
2. **Do not introduce** DOTween / UniTask / any third-party tween library. Use coroutines + Lerp.
3. **Use 9-sliced sprites** wherever a frame or button is reused at different sizes. Set `pixelsPerUnitMultiplier=1` and configure border in TextureImporter.
4. **No `Resources.Load` thrash**: cache loaded sprites in dictionaries. Build the cache once per scene-load.
5. **All new MonoBehaviours** in namespace `Golfin.Gameplay.UI.ShotUI` (existing) or a new `Golfin.Gameplay.UI.HUD` if it makes sense to split. Reuse `Golfin.Gameplay.UI.asmdef`. New asmdef requires Architect approval.
6. **Tap-and-hold gesture**: implement as `IPointerDownHandler` + coroutine that opens the selector after `controlsCfg.SelectorHoldThresholdSec` (add to `controls.csv` with seed 0.25s). Cancel if pointer-up before threshold; commit on pointer-up after threshold.
7. **Selectors are mutually exclusive**: opening the ball selector closes the club selector and vice-versa. Both close on any pointer-up.
8. **Anchor everything** with a `CanvasScaler` Scale With Screen Size at 1080×1920 reference (already on `ShotUI_Canvas`). New widgets respect that.
9. **Per-part commits** with messages `phase-8.{N}: {one-line summary}`.
10. **No texture compression overrides**. Leave imports as default; if a sprite looks bad, that's a re-export job for Cesar.
11. **Log procedural mesh inputs at runtime.** For any procedural mesh / shader work, the first debug step is to log every input parameter via `Debug.Log` in `OnPopulateMesh` (or the equivalent). This catches the serialized-field gotcha (8.1's `_heightPx` was 600 in scene, 1009 in code).
12. **Patch scenes when you change `[SerializeField]` defaults.** LabScaffold + ShotConeTest are the relevant scenes for shot UI. Done report must list scenes re-saved and serialized values updated.

---

## Part 8.1 — Cone restyle + timing arrow slabs ✅ DONE

**Completed:** 2026-04-27. Took 6 commits and ~6 visual iterations. Full record: `Docs/Diagnostics/CONE_MESH_ITERATION_LOG.md`.

**Files shipped:**
- `Assets/Scripts/Gameplay/UI/ShotUI/ConeMeshGraphic.cs` — subdivided mesh with gradient, curved base, perpendicular silhouette feather, 3 hard-edged band lines.
- `Assets/Scripts/Gameplay/UI/ShotUI/ConeBandPalette.cs` — single source of truth for band Y values, band colors, slab colors, fill color.
- `Assets/Scripts/Gameplay/UI/ShotUI/TimingSlabGraphic.cs` — trapezoid slab.
- `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — swapped from `ArrowGraphic` array to single `TimingSlabGraphic`, color from `ConeBandPalette`.
- `Assets/Scenes/Physics/LabScaffold.unity` — patched serialized values (`_heightPx: 1009`, `_edgeFadePx: 2`, `ConeMesh.sizeDelta`, `anchoredPos`).

**Final parameters** (in `ConeMeshGraphic` defaults + scene serialized values):
- `_halfAngleDeg = 12.5°`, `_heightPx = 1009f`, `_strips = 512`, `_curvaturePx = 15f`, `_centerDarkFraction = 0f`, `_edgeFadePx = 2f`.
- Bands: red 0.00, gold 0.45, green 0.85. `BandHalfHeightPx = 2f`. Hard-edged (no top/bottom alpha fade).
- Fill: center 50% black → edges 35% grey.

**Known remaining issue:** silhouette is sub-5px jagged at high zoom. Acknowledged, not blocking. May revisit if it becomes a problem on device.

**Lessons (also baked into the protocol above):**
- Serialized fields override code defaults — always log inputs at runtime.
- Diagonal silhouette anti-aliasing must be perpendicular, not horizontal.
- Keep feather widths small so the cone doesn't visually exceed structural elements.
- Bands are opaque; only the fill is semi-transparent.
- Disable `ConeAlphaController` for visual tests at full alpha.

---

## Part 8.2 — Power gauge widget

**Goal:** Right-side circular gauge that fills clockwise green→maroon as power 0–1.2, displaying `{pct}%` and `{yards} yd` centered.

**Reference position:** `Initial State.png`, `Pull Back.png`, `Timing Arrows.png` — gauge sits at roughly screen-X 87%, screen-Y 27% (right edge, upper-mid). Anchor: top-right with a fixed offset.

### Step A — reference walk-through (do this first)

Open `Initial State.png`. List every visual property of the gauge: ring thickness, ring colors at 0%/50%/100%, background fill (`Indicator - Power.png`), text size + weight + color for `{pct}%`, secondary text size + weight + color for `{yds} yd`, padding around text, exact pixel size of the widget. If the spec misses any, ask Architect before coding (Rule A above).

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeGraphic.cs` — new `MaskableGraphic`. Renders a circular arc fill:
   - Inputs: `_progress01` (0..1.2), `_innerRadius`, `_outerRadius`, `_segmentCount` (e.g. 64).
   - Sweep: starts at 12-o'clock, goes clockwise.
   - Color rule: `color = Color.Lerp(Color.green, MaroonColor, Mathf.Clamp01(progress01 / 1.2f))` — OK to start linear; can replace with multi-stop later.
   - Build mesh as a triangle fan: each segment is two triangles (inner-top, outer-top, outer-bot, inner-bot).
   - **Important:** when `_progress01 > 1.0`, color becomes maroon and continues filling — the gauge does NOT clamp at 360°; the overpower zone (1.0–1.2) wraps slightly past 12-o'clock.
2. `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs` — MonoBehaviour coordinator:
   - References: `PowerGaugeGraphic`, background `Image` (assigns `Indicator - Power.png`), centered `TMP_Text` for `{pct}%`, secondary smaller `TMP_Text` for `{yds} yd`.
   - Subscribes to `ShotController.OnStateChanged` (lookup via injected `_shotController`).
   - On state tick: `gauge.Progress01 = state.PowerNormalized`. Updates text. Hides whole widget in `Idle`, shows from `Aiming` onward.

### Scene edits (via Unity-MCP)

- Add a child `PowerGaugeWidget` GameObject under `ShotUI_Canvas`.
- Anchor: top-right; anchored position `(-180, -460)` from top-right.
- Size: 200×200 px.

### Done report 8.2

Follow the visual fidelity protocol (Section A–E above). Specifically:
- Files added.
- Screenshot of gauge at 0% (empty), 50% (green-ish, half-fill), 100% (yellow/orange transition, full fill), 120% (maroon, slight wrap past start). Note: only achievable in Pulling state by dragging long. Use a debug toggle if needed.
- **Side-by-side comparison** vs `Initial State.png` (gauge at 50% per Figma) + `Pull Back.png` + `Timing Arrows.png`. Save to `Docs/Diagnostics/phase-8/8.2/gauge-diff-final.png`.
- List of any visual properties that didn't match, with reasoning.

---

## Part 8.2.5 — Club Handle sprite swap by club type

**Goal:** The `ClubHandle` GameObject (child of `ShotUI_Canvas`, used by `ClubHandleDragger` for the pull-back gesture) currently displays a placeholder sprite. Swap that sprite to match the currently-selected club type using the real club-control PNGs from `Assets/Resources/Clubs/Controls/`.

**Why this exists:** The shot UI looks broken until this lands — the placeholder handle is the most visually prominent leftover from before Phase 8. The proper Club button (Part 8.5) will eventually drive equipped-club state for the whole HUD; this part is the minimum viable wiring so the handle visual stops being a placeholder while we work on the rest.

**Scope:** Hardcoded brand pick per club type. No `BagManager` integration, no equipped-club lookup, no per-brand variation. The Club button in Part 8.5 (and later the real bag system) will replace this binder's data source — the binder itself stays.

### Step A — reference walk-through

Open `Pull Back.png`. The club handle visible at the cone apex is a `Driver` head (the only club shown across the reference frames). Note: the handle in the reference renders facing into the cone (head pointing down toward the ball). Confirm the existing `ClubHandle` Image in `LabScaffold.unity` already has the correct rotation/anchor/size — we're only swapping `.sprite`, not changing the layout.

Visual properties to verify:
- Sprite size on screen at 1080×1920 reference (check existing scene values; do NOT change them).
- Sprite anchor / pivot (existing scene values; do NOT change).
- Z-order: handle renders above the cone fill but below the centerpiece ball (Part 8.7).

If the spec or scene is missing any property the reference shows, surface to Architect before coding.

### Hardcoded brand-per-type table (v1)

These are the picks for v1. When the Club button + bag wiring lands (Part 8.5+), this table goes away in favour of the equipped-club name from the bag.

| Club index (`PhysicsLabController.LabClubLabels`) | Type | Sprite path |
|---|---|---|
| 0 (`Driver`) | Driver | `Assets/Resources/Clubs/Controls/S_Controls_Driver_GOLFIN.png` |
| 1 (`Iron 7`) | Iron | `Assets/Resources/Clubs/Controls/S_Controls_Iron_GOLFIN.png` |
| 2 (`Wedge`) | Wedge | `Assets/Resources/Clubs/Controls/S_Controls_Wedge_GOLFIN.png` |
| 3 (`Putter`) | Putter | `Assets/Resources/Clubs/Controls/S_Controls_Putter_GOLFIN.png` |

Resource keys (without extension, since `Resources.Load<Sprite>` doesn't take extensions):
- `Clubs/Controls/S_Controls_Driver_GOLFIN`
- `Clubs/Controls/S_Controls_Iron_GOLFIN`
- `Clubs/Controls/S_Controls_Wedge_GOLFIN`
- `Clubs/Controls/S_Controls_Putter_GOLFIN`

**Wood handling:** the lab picker has 4 slots (Driver/Iron/Wedge/Putter), no Wood slot. If/when Wood is added to the lab, the binder's switch defaults to the Driver sprite — flag to Architect to extend the table at that point.

### Files to modify

1. `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — expose the current club index so external subscribers can read it + react to changes. Two-line change:
   - Add field: `public int CurrentClubIndex { get; private set; }`
   - Add event: `public event System.Action<int> OnClubChanged;`
   - In `SetClub(int index)`, before the `return` guard: set `CurrentClubIndex = index;` after validation succeeds, and after the StatBundle injection: `OnClubChanged?.Invoke(index);`

   This is a 4-line additive change — no behavioural difference for any existing caller. Surface to Architect before making it (counts toward the "one possible amendment in 8.5" allowance — except this is in `PhysicsLabController`, not `ShotInputBuilder`, so it's outside the OFF LIMITS list and OK).

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleSpriteBinder.cs` — new MonoBehaviour. Sits next to `ClubHandleDragger` on the same GameObject (or as a sibling under `ClubHandle`). Holds a reference to the `Image` component on the handle.

   Pseudocode shape (do NOT paste verbatim — adapt to project conventions and verify Resources.Load paths at runtime):

   ```csharp
   namespace Golfin.Gameplay.UI.ShotUI
   {
       [RequireComponent(typeof(UnityEngine.UI.Image))]
       public class ClubHandleSpriteBinder : MonoBehaviour
       {
           [SerializeField] private Golfin.Physics.Viewer.PhysicsLabController _labController;
           private UnityEngine.UI.Image _image;
           private Sprite[] _cachedByIndex;  // [4]: driver, iron, wedge, putter

           private static readonly string[] ResourceKeys =
           {
               "Clubs/Controls/S_Controls_Driver_GOLFIN",
               "Clubs/Controls/S_Controls_Iron_GOLFIN",
               "Clubs/Controls/S_Controls_Wedge_GOLFIN",
               "Clubs/Controls/S_Controls_Putter_GOLFIN",
           };

           private void Awake()
           {
               _image = GetComponent<UnityEngine.UI.Image>();
               _cachedByIndex = new Sprite[ResourceKeys.Length];
               for (int i = 0; i < ResourceKeys.Length; i++)
               {
                   _cachedByIndex[i] = Resources.Load<Sprite>(ResourceKeys[i]);
                   if (_cachedByIndex[i] == null)
                       Debug.LogWarning($"[ClubHandleSpriteBinder] Missing sprite at Resources/{ResourceKeys[i]}");
               }
           }

           private void OnEnable()
           {
               if (_labController == null) return;
               _labController.OnClubChanged += HandleClubChanged;
               HandleClubChanged(_labController.CurrentClubIndex); // sync initial state
           }

           private void OnDisable()
           {
               if (_labController == null) return;
               _labController.OnClubChanged -= HandleClubChanged;
           }

           private void HandleClubChanged(int index)
           {
               if (_image == null) return;
               if (index < 0 || index >= _cachedByIndex.Length) index = 0; // fallback to driver
               var s = _cachedByIndex[index];
               if (s != null) _image.sprite = s;
           }
       }
   }
   ```

   Cache rule: load all 4 sprites in `Awake`. Subsequent club changes are O(1) sprite-swap. No `Resources.Load` thrash.

### Scene edits (via Unity-MCP)

- On the existing `ClubHandle` GameObject in `LabScaffold.unity`, add the `ClubHandleSpriteBinder` component.
- Drag the `LabRoot` (or wherever `PhysicsLabController` lives in `LabScaffold`) into the `_labController` slot.
- Save `LabScaffold.unity`.

### Behavior contract

1. On scene start: handle shows the sprite matching `_labController.CurrentClubIndex` (default 0 = Driver).
2. When user cycles the lab UI's CLUB picker (`<` / `>`): handle sprite swaps in the same frame.
3. When ball is in flight / Idle / etc.: handle sprite stays whatever was last set; visibility is controlled elsewhere (`ConeAlphaController` / `ShotConeView`).
4. If a sprite fails to load: log warning, leave previous sprite in place (don't blank the Image).

### Handle scale-with-pull (implement in `ShotConeView.UpdateClubHandle`)

The club handle image should grow as it is pulled away from the cone tip (giving a tactile "stretch" feel) and shrink back as it returns toward the tip during the flick.

**Rule:** `handleScale = Mathf.Lerp(1.0f, MaxHandleScale, 1f - y01)`
- `y01` is the handle's normalised vertical position in the cone, where `1 = at the tip (top)` and `0 = at the base (bottom)`.
- `MaxHandleScale = 1.3f` (noticeable but not distracting — do not exceed 1.5).
- Apply as `_clubHandle.localScale = Vector3.one * handleScale` each frame inside `UpdateClubHandle`.
- Scale is reset to `Vector3.one` in the `Idle` state (when the handle is hidden or snapped to tip).

**Why y01 and not raw pixels:** `ShotConeView` already tracks `_coneHeightPx` and the handle's anchored Y. `y01 = Mathf.Clamp01(handleAnchoredY / _coneHeightPx)` gives 1 at tip and 0 at base; the scale formula follows directly. If `y01` is already computed in `UpdateClubHandle`, reuse it — don't recompute.

**Constraints:**
- Do NOT change the handle's `RectTransform.sizeDelta` (which governs the sprite's canvas footprint). Scale via `localScale` only; this preserves anchoring and allows trivial reset.
- Do NOT add a serialized inspector field for `MaxHandleScale` — bake it as a `const float` in `ShotConeView`. If the Architect wants to tune it later, it moves to `ConeBandPalette` or `ControlsConfig`.

### Done report 8.2.5

Follow the visual fidelity protocol. Specifically:
- Files added (`ClubHandleSpriteBinder.cs`).
- Files modified (`PhysicsLabController.cs` — 4-line additive change; `ShotConeView.cs` — scale-with-pull in `UpdateClubHandle`).
- Scene saved (`LabScaffold.unity` with the binder component).
- Screenshot per club type (4 screenshots: Driver, Iron 7, Wedge, Putter) at the same shot state (e.g. `Aiming` with cone visible, handle at apex).
- **Side-by-side comparison** vs `Pull Back.png` (Driver only — the reference). Verify: handle position unchanged, only the sprite content differs. The other 3 (Iron/Wedge/Putter) are sanity screenshots — no Figma reference exists for them.
- Confirmation that cycling the lab UI's CLUB picker swaps the handle sprite live.
- **Scale verification**: two screenshots — (a) handle at or near tip (y01 ≈ 1) showing scale ≈ 1.0; (b) handle fully pulled (y01 ≈ 0) showing scale ≈ 1.3. Both screenshots should make the size difference clearly visible. Also confirm scale snaps back to 1.0 in Idle.

### Iteration budget 8.2.5

- Functional: 2 attempts. If the binder doesn't subscribe / fire / find sprites after 2 attempts — surface.
- Visual: 2 rounds (this is a sprite swap, not a procedural shape — visual fidelity is bounded by the asset itself).

### Hard constraints 8.2.5

- Do NOT change the handle's RectTransform anchor/pivot/size/rotation. Cesar configured those manually.
- Do NOT add per-brand variation logic. Hardcoded GOLFIN brand only in v1.
- Do NOT touch `ClubHandleDragger.cs`.
- Do NOT add a `ClubKind` enum or any new public types in physics namespaces — keep the data flow as `int index → sprite array lookup`.

---

## Part 8.3 — Player card + Hole card + Settings icon

**Goal:** Three top-of-screen widgets.

### Step A — reference walk-through

Open `Initial State.png`. For each widget, list every visual property: frame style, padding, font weight + size, portrait/thumbnail aspect ratio, rarity background blending, row separator style, alignment. Surface anything ambiguous to Architect before coding.

### 8.3.a — Player card (top-left)

Layout per `Initial State.png`:
- Portrait square, ~140×140 px, top-left at `(40, -40)` from top-left anchor.
- Rarity background fills the portrait square behind the portrait image.
- Right of portrait: 3 stacked navy-rounded text rows (using `Indicator - Wind-Hole.png` 9-sliced as the row background):
  - Row 1: `USERNAME` (bold).
  - Row 2: `Lv {level}`.
  - Row 3: `TURN {turn}`.

**Data sources:**
- Username: `CharacterManager.Instance.CurrentCharacter.DisplayName` — verify field name; use reflection if asmdef boundary blocks. Fallback: `"Player"`.
- Level: `CharacterManager.Instance.CurrentCharacter.Level`. Fallback: `1`.
- Portrait sprite: `Portraits/Mini/<characterKey>.png` via `Resources.Load<Sprite>`.
- Rarity bg: `Rarities/<rarityName>.png`.
- Turn count: a new `GameSession.Instance.TurnCount` static — if it doesn't exist, create a stub in `Golfin.Gameplay.UI.HUD.GameSession.cs` that returns `1` for now and gets wired by Phase C (menu→gameplay integration).

### 8.3.b — Hole card (top-right, left of settings)

Layout per `Initial State.png`:
- Mirror of player card; portrait-equivalent is a 140×140 hole-map thumbnail on the right.
- Three text rows on the left (right-aligned text):
  - Row 1: course name (`LOMOND`).
  - Row 2: `HOLE {n} - {teeName}` (e.g. `HOLE 1 - LADY'S`).
  - Row 3: `PAR {n}`.

**Data sources:**
- Course name: hardcode `"LOMOND"` for v1 (single course). When multi-course lands, pull from a `CourseContext` static.
- Hole number, par, tee name: from a new `HoleContext` static that holds the currently-loaded hole's metadata. Read from the existing `Hole_XX_Geo.unity` scene's metadata GO if there is one; if not, parse from the scene name (`Hole_01_Geo` → `1`) and use `par.csv` or just hardcode par values inline (Architect can amend later).
- Hole map: `Art/In-Game UI/HoleMaps/Lomond - Hole {n}.png`.

**Tee name fallback:** if no tee metadata, show `"REGULAR"`.

### 8.3.c — Settings gear icon

- Top-right corner, `(-50, -40)` from top-right.
- 90×90 white circle with gear icon (`Icon - Settings.png`).
- On tap: opens settings modal (defer modal implementation to a stub; for now just `Debug.Log("[Settings] tapped")`).

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs`
2. `Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs`
3. `Assets/Scripts/Gameplay/UI/ShotUI/SettingsButton.cs`
4. `Assets/Scripts/Gameplay/UI/HUD/HoleContext.cs` — static holder. API: `int HoleNumber`, `int Par`, `string TeeName`, `string CourseName`, `event Action OnChanged`. Wired by `LabHoleBinder` on hole load.
5. `Assets/Scripts/Gameplay/UI/HUD/GameSession.cs` — stub static for turn count.

### Files to modify

1. `Assets/Scripts/Physics/Viewer/LabHoleBinder.cs` — on hole load, populate `HoleContext` (parse hole number from scene name; par from `par.csv` if exists, else fallback table).

### Done report 8.3

Follow the visual fidelity protocol. Specifically:
- Files added/modified.
- Screenshot of full top bar with player card + hole card + settings populated.
- **Side-by-side comparison** vs `Initial State.png`. Sub-elements to verify: portrait + rarity bg sizing, USERNAME/Lv/Turn row alignment, hole map thumbnail aspect ratio, course/hole/par text alignment (right-justified on the hole card), settings gear position + size.
- Confirmation that switching holes via picker updates the hole card correctly.

---

## Part 8.4 — Wind indicator + Hole-direction indicator

**Goal:** Top-left area, below player card.

### Step A — reference walk-through

Open `Initial State.png`. The wind chip and the hole/distance chip are visible. List visual properties of both (chip frame, arrow icon style, flag icon style, text size/weight/color, leader/tail line style if present). Surface anything the spec doesn't cover.

### Wind indicator (fixed position, top-left)

Layout per all reference frames:
- Anchor top-left, position `(40, -240)`.
- Small pill: navy rounded chip with arrow icon + small text `{speed} mph` below.
- Arrow rotation: world-space wind direction projected onto camera-relative XZ; rotate icon by the resulting yaw vs camera heading.

**Data source:** `WindConfig` (from `wind.csv`) — read via a new `WindContext` static; for v1, just sample `WindModel.SampleWind(ballPos, 0, wind)` once per shot setup and store in `WindContext.CurrentSpeedMph` + `WindContext.CurrentYawRadians`.

### Hole-direction indicator (movable, follows hole)

Per Cesar's spec:
- **Always sits on a fixed horizontal Y line** (same Y as the wind indicator, just to the right of it).
- **Tail points toward hole**: the indicator is a navy chip with a rendered tail/leader line. The chip itself sits at the projected pin XZ on screen, but its X is clamped to `[windIndicatorX + windIndicatorWidth + padding, screenWidth - padding]`. If the projected X falls inside that clamp, indicator sits at the projection. If outside, indicator clamps and the tail extends outward toward the off-screen pin.
- **Tail length** scales with how far off-screen the pin is: longer tail = pin further off-screen. Cap at 200px.
- **Collision rule with wind indicator**: if the hole indicator's X clamp-bound would overlap the wind indicator's bounding box, the hole indicator slides UNDER the wind indicator (lower Z-order; same Y). "Under" = z-order, NOT "below in Y". The wind indicator visually occludes the hole indicator when they overlap, and the hole indicator's tail still points correctly.
- **Distance label**: shows `{yards} yds` next to the flag icon (`Icon - Flag.png`).

**Data source:** Pin world position — find `Pin_*` GO in the loaded hole scene (use reflection if asmdef boundary blocks), or a `HoleContext.PinWorldPos` (preferred; populate in `LabHoleBinder.OnHoleLoaded`).

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/WindIndicator.cs`
2. `Assets/Scripts/Gameplay/UI/ShotUI/HoleDirectionIndicator.cs`
3. `Assets/Scripts/Gameplay/UI/HUD/WindContext.cs` — static holder.

### Files to modify

1. `HoleContext.cs` (from 8.3) — add `Vector3 PinWorldPos`.
2. `LabHoleBinder.cs` — populate `WindContext` and `HoleContext.PinWorldPos`.

### Done report 8.4

Follow the visual fidelity protocol. Specifically:
- 3 screenshots: (a) hole on-screen — indicator sits at projected position, no tail visible (tail length = 0); (b) hole off-screen to the right — indicator clamps to right padding, long tail points off-screen-right; (c) hole projected directly behind wind indicator — confirm hole indicator slides under (use a debug toggle to force this case if needed).
- Confirmation that wind speed text and arrow rotation respond to wind config changes.
- **Side-by-side comparison** vs `Initial State.png` (wind chip + flag/distance chip). Verify chip frames, arrow icon size + rotation, flag icon size, distance text alignment.

### Iteration budget 8.4 (functional)

- 2 attempts on the screen-space projection math; the gotcha is when pin is BEHIND the camera (`screen.z < 0`). In that case, mirror the screen X across screen-center before clamping — otherwise the indicator points the wrong way for behind-camera pins.
- 2 attempts on the wind/hole collision Z-ordering. If hard, use sibling index reordering at runtime (`indicator.SetSiblingIndex(0)`).

---

## Part 8.5 — Action button row

**Goal:** Bottom of screen, 4 buttons (Spin, Ball, Mode, Club). Visible in `Aiming`, `Pulling`, `Timing`. Hidden in `Idle`, `Flicking`, `Resolving`.

### Step A — reference walk-through

Open `Straight Shot.png` AND `Fade.png`. The buttons are in a 2-column × 2-row layout: SPIN top-left + GOLFIN bottom-left, MODE top-right + DRIVER bottom-right. List visual properties: frame border (gold gradient), 9-slice settings, icon area sizing in white top half, label TMP weight/size/color in navy bottom half, vertical spacing between top and bottom buttons in each column.

### Layout (confirmed from `Fade.png`)

Two columns, two rows. Left column = Spin (top) + Ball (bottom). Right column = Mode (top) + Club (bottom).

- Anchor bottom-left and bottom-right, two stacked on each side.
- Each button is `Button - All.png` 9-sliced, ~165×250 px.
- Button content split into two halves (per the frame design): white top half (icon area) + navy bottom half (label area, white text).

### Per-button behavior

| Button | Top half (icon) | Bottom half (label) | On tap | On hold |
|---|---|---|---|---|
| Spin | `Icon - Spin.png` | `SPIN` | Open spin pre-stage modal (defer to stub `Debug.Log`) | — |
| Ball | Currently equipped ball thumbnail (`Balls/Thumbnails/<n>.png`) | Ball name + `∞` (or count) | (no tap action; selector opens on hold) | Open Ball Selector overlay (Part 8.6) |
| Mode | `Icon - Straight.png` or `Icon - DrawFade.png` (whichever matches current mode) | Mode name (`STRAIGHT`, `FADE/DRAW`) | Cycle modes: Straight → Fade/Draw → Straight | — |
| Club | Currently equipped club portrait (`Clubs/Portraits/S_Menu_<type>_<brand>.png`) | Club type + `{maxYards} yrds` | (no tap action; selector opens on hold) | Open Club Selector overlay (Part 8.6) |

**Mode button gotcha:** `ShotInputBuilder` already emits Straight when `IsPutt` or when no mode picked. The Mode button just exposes the user-visible toggle. Wire it via a new `ShotController.ShotMode` property (`enum { Straight, FadeDraw }`); plumb to `ShotInputBuilder.Build` as a parameter on the existing build call. **NOTE:** This is the only change to `ShotInputBuilder.cs` allowed in this phase — surface to Architect before making it; we may push this down to Part B (controls finetuning) if it touches stat coupling.

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/ActionButton.cs` — base class (frame + icon + label, tap + hold callbacks).
2. `Assets/Scripts/Gameplay/UI/ShotUI/SpinButton.cs`
3. `Assets/Scripts/Gameplay/UI/ShotUI/BallButton.cs`
4. `Assets/Scripts/Gameplay/UI/ShotUI/ModeButton.cs`
5. `Assets/Scripts/Gameplay/UI/ShotUI/ClubButton.cs`

### Done report 8.5

Follow the visual fidelity protocol. Specifically:
- Files added.
- Screenshot showing all 4 buttons populated with current equipped club + ball.
- Confirmation that tapping Mode toggles between Straight and Fade/Draw and that the icon updates.
- Confirmation that the row hides during Flicking/Resolving.
- **Side-by-side comparison** vs `Straight Shot.png` (Straight mode active) AND `Fade.png` (Fade/Draw mode active). Verify per button: frame size + 9-slice border, icon area sizing in white top half, label TMP weight + size + color in navy bottom half, vertical stack spacing.

---

## Part 8.6 — Ball Selector + Club Selector overlays

**Goal:** When user taps and holds the Ball or Club button, a vertical scrollable strip opens showing all equipped items of that type. User scrolls by sliding finger up/down. On lift, the centered item is selected and the overlay closes.

### Step A — reference walk-through

Open `Selector - Ball.png` and `Selector - Club.png`. List visual properties: item frame style + spacing, chevron up/down arrow size + position, dimmed-vs-highlighted opacity ratio, label TMP size + alignment for `BALL NAME` + `xN`/`∞` rows.

### Behavior

1. **Tap-and-hold** on the Ball button (>= `controlsCfg.SelectorHoldThresholdSec`, seed 0.25s): opens the Ball Selector, anchored to the Ball button's position. Initial: equipped ball is centered; ±1 visible above and below.
2. While finger is down: vertical drag scrolls the strip. Snap-to-item with friction — use a simple `Mathf.Lerp` toward nearest snap point; no spring physics.
3. On finger up: whatever item is currently centered becomes the selection. Fire `BallButton.OnBallChanged(ballKey)` event. Close the overlay.
4. While selector is open, all other UI is non-interactive (block raycasts via a full-screen transparent `GraphicRaycaster` overlay below the selector).
5. Same exact behavior for Club Selector.

### Data source

- Ball selector items: `BagManager.Instance.EquippedBalls` (or fallback: hardcoded list of `Golfin`, `PuttAce` for v1). Each item: thumbnail sprite + name + count.
- Club selector items: `BagManager.Instance.EquippedClubs` (per project memory: bag holds 14 clubs). Fallback: Driver + Putter from `DefaultStatProvider`.

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/BallSelectorOverlay.cs`
2. `Assets/Scripts/Gameplay/UI/ShotUI/ClubSelectorOverlay.cs`
3. `Assets/Scripts/Gameplay/UI/ShotUI/SelectorStripScroller.cs` — shared base class for the scrolling list mechanic.

### Done report 8.6

Follow the visual fidelity protocol. Specifically:
- Files added.
- Screenshot of Ball selector open with 3+ items visible, one centered/highlighted.
- Screenshot of Club selector open similarly.
- Confirmation that finger-up commits the centered item and closes the overlay.
- Confirmation that holding ball-button while club-selector is open closes club and opens ball (mutually exclusive).
- **Side-by-side comparison** vs `Selector - Ball.png` AND `Selector - Club.png`.

---

## Part 8.7 — Centerpiece ball + targeting line restyle

### Step A — reference walk-through

Open `Initial State.png` and `Pull Back.png`. List visual properties of the ball thumbnail (size, alpha, drop shadow if any) and the targeting line (width, alpha gradient, end caps).

### 8.7.a — Centerpiece ball

In `Initial State.png` and `Pull Back.png`, a small ball thumbnail is rendered at the ball's projected screen position (replaces or augments the targeting-line origin). Per Cesar:
- Ball icon at the screen-projected ball position (use the same projection `ShotConeView.UpdateTargetingLine` already does).
- Sprite: `Balls/Thumbnails/<currentBallName>.png`.
- Size: ~80×80 px.
- Always visible during Aiming/Pulling/Timing/Flicking. Hidden in Idle/Resolving.

### 8.7.b — Targeting line restyle

- Replace the placeholder Image with `Indicator - Trail.png` 9-sliced or stretched along the line direction.
- Top of the line should fade out (alpha gradient from 1.0 at ball end to 0.2 at flag end). Use vertex color gradient on a new `MaskableGraphic` if 9-slicing alpha doesn't work cleanly — or just author the trail PNG with the gradient baked in.
- Length: stays at `ControlsConfig.TargetingLineLengthMeters` (existing behavior).

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/CenterpieceBall.cs`

### Done report 8.7

Follow the visual fidelity protocol. Specifically:
- Files added.
- Screenshot showing ball thumbnail at ball position with trail leading toward aim direction.
- **Side-by-side comparison** vs `Initial State.png` and `Pull Back.png`.

---

## Part 8.8 — Polish + tests + screenshots + smoke

### Tests to add (EditMode)

1. `ShotConeView_TimingSlab_ColorAtRedZone_IsRed` — set `state.ArrowProgress01 = 0.1`, verify `_timingSlab.color.r > 0.7 && color.g < 0.3`.
2. `ShotConeView_TimingSlab_ColorAtGreenZone_IsGreen` — same at progress=0.95.
3. `PowerGaugeWidget_AtOverpower_ShowsMaroon` — set `state.PowerNormalized = 1.15`, verify gauge color.
4. `PlayerCardWidget_FallbackWhenNoCharacterManager_ShowsDefaults` — verifies graceful fallback.
5. `HoleDirectionIndicator_OffScreenRight_ClampsToRightEdge` — mock projected screen X = 1500 (off-screen for 1080-wide canvas), assert indicator.X is clamped near 1080 and tail length > 0.
6. `HoleDirectionIndicator_BehindCamera_FlipsToOppositeEdge` — mock `screen.z < 0`, assert indicator clamps to opposite side.

**Run all existing tests** — 198/198 must still pass.

### Manual smoke test (Cesar)

Load LabScaffold + Hole 1. Walk through:
1. Idle — cone ghosted, no buttons, no gauge, top bar visible.
2. Tap-and-pull — cone fades in, gauge fills, club handle slides down with finger.
3. Hold in timing zone — slab travels up cone, color cycles red→amber→green. Bands visible.
4. Tap each button: Spin (logs stub), Mode (cycles Straight/Fade), Settings (logs stub).
5. Tap-and-hold Ball button — selector opens. Drag, lift on different ball. Confirm equipped ball thumbnail updates.
6. Same for Club.
7. Walk to ball position, observe wind/hole indicators stay top-left.
8. Pivot camera to face away from hole — confirm hole indicator clamps to screen edge with tail.
9. Pivot camera so hole is directly behind wind indicator — confirm hole indicator slides under wind.
10. Fire 5 shots, confirm gauge / cone / slab respond consistently.

### Done report 8.8 (final)

- All 8 parts summarized: files added/modified, screenshots, deviations.
- Test gate: 198 + 6 new = 204/204 pass.
- Cesar's smoke list with each item checked.
- **Final full-screen pixel-diff comparison.** Capture one screenshot per reference state (Idle / Pull Back / Timing / Straight Shot / Fade / Selector-Ball / Selector-Club) and compare the WHOLE composition against the corresponding reference frame. Integration check — individual elements may have passed their per-part diff, but the full composition must also hold up (no overlapping anchors, no occlusions Cesar didn't sign off on, no z-order surprises). Attach all 7 side-by-side comparisons.
- Move spec to `Docs/Specs/Completed/PHASE_8_SHOT_UI_POLISH.md`.
- Update `TellCode.md`: remove ROADMAP item A; add `✅ DONE 2026-04-XX Phase 8 Shot UI Polish` to History Log.

---

## Hard rules summary

1. **OFF LIMITS**: `BallSimulation.cs`. Anything in `Physics/Core/`, `Physics/Math/`, `Physics/Stats/StatModifierResolver.cs`, `Physics/Stats/ShotInputBuilder.cs` (one possible amendment in 8.5 — must be approved by Architect first).
2. **Functional budget per part: 2 attempts.** Visual budget per element: 5 rounds. Stop on hit; surface.
3. **No third-party libraries.** uGUI + TMP + procedural meshes only.
4. **No `Resources.Load` thrash** — cache.
5. **No scene auto-saves** other than `LabScaffold.unity` and `ShotConeTest.unity`.
6. **Per-part commits** with messages `phase-8.{N}: {one-line summary}`.
7. **Texture imports** stay default. If a sprite imports too large/small, generate a smaller/larger PNG file alongside; do NOT crank `TextureImporter.maxTextureSize`.
8. **All new MonoBehaviours** in `Golfin.Gameplay.UI` asmdef. New asmdef requires Architect approval.
9. **Visual fidelity protocol is mandatory** — reference walk-through before coding, side-by-side diff before "done", named scenes patched when changing serialized defaults. Skipping any of these is grounds for procedural rejection.

## Reference

- Existing UI files: `Assets/Scripts/Gameplay/UI/ShotUI/*.cs` — read all before starting.
- 8.1 lessons: `Docs/Diagnostics/CONE_MESH_ITERATION_LOG.md`.
- Design doc: `Docs/Game Design/SHOT_CONTROLS_DESIGN.md`.
- Existing test pattern: `Assets/Scripts/Gameplay/Tests/ShotControllerTests.cs`.
- Asset paths confirmed in this spec.
