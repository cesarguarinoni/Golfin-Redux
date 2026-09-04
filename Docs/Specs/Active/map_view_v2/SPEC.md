# SPEC — `map_view_v2`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` (`SPEC_READY`, 2026-09-04).

## Goal

Rebuild the presentation layer of the overhead map view (`MapViewController`, Order 352 lineage) to Cesar's chosen Figma concept **B1**: a flat, constant-width dotted aim line with 50-yd ticks; a lime **range fan** that shows what the selected club can reach; a landing zone with a readable centre (glow + white ring + crosshair); a HUD-style **target readout chip**; the real in-game **Hole Indicator** at the pin; and the two bottom corners occupied by in-game **Select Buttons** (SHOT VIEW bottom-left, the club button bottom-right). An explicit **over-range state** (red) replaces today's silent line colour change, and shows where the ball will actually land.

What this fixes (Cesar, 2026-09-04): "the trajectory line is thick and seen from a weird angle, no indication when going over the club's possible distance, hole indicator is different from the one in game." Aiming *behaviour* (touch-follow target, write-back to `ShotController.MapTargetCarryM`, camera framing, strict crop, pinch/pan) is **unchanged** — this task is visuals + one new button.

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd`, page **"Map View Redesign — Proposals"**, section of the same name, row 2.
  - Aiming state: frame `B1 — In-game corner buttons`, node `14123:32469`
  - Over-range state: frame `B1 — In-game corner buttons · Over range`, node `14125:32540`
  - SHOT VIEW button (lookalike, for the outline/shadow only): node `14123:32578`
  - Target readout chip: node `14123:32597` (over-range variant `14125:32578`)
- **Reference renders in `reference/`:** `B1_aiming.png` (1170×2532), `B1_over_range.png` (1170×2532), `shot_view_button.png`, `target_readout_chip.png`, `map_bg_clean.png` (the current build's map with UI removed — background only, NOT a target), `Icon - ShotView.png` (256², placeholder glyph for the SHOT VIEW portrait area — drop into `Assets/Resources/UI/` next to `Icon - Flag.png`).
- **Placeholder vs canonical:** every number in the Figma (195 / 215 / 232 / 123 yd, 318 yd on the hole chip, "DRIVER") is mockup data — runtime values come from `ClubContext.SelectedDistance`, `_aimedCarryM` and `HoleContext.PinWorld`. The camera glyph is a placeholder until Robin supplies an icon. Wind rings are NOT in the design.
- **Design vocabulary (do not invent):** Rubik; navy `#001E39`; lime `#78E921`; over-range red `#F23A33`; HUD chip = white box, 8 px radius, navy header (as `WindIndicator` / `HoleIndicator`); Select Button = the `In-Game Select Button` prefab already used by `DriverButton` (145×240, white portrait / navy data, r20, gold gradient outline `#F3ECC2 → #98855B` 3 px inside, drop shadow).

## Figma Fidelity (enumerate EVERY element — Rule 18)

Coordinates are Figma px on the 1170×2532 frame; world-space elements are described by their rule, not px.

| Element | Figma node | Property → value |
|---|---|---|
| Aim line | `14123:32474` | **Dotted**, constant width on screen (≈8 px dot, ≈22 px pitch at the default zoom), white 90 %. Straight ball → L, **no vertical bow** (`kArcBow` not applied in the map), hugs terrain (`SampleTerrainHeight` per vertex + `kRingHeightOff`). Colour never changes with power; over-range is expressed by the extra red segment below. |
| Yardage ticks | `14123:32475`…`32480` | Every 50 yd (50 m in metres mode) from the ball along the aim direction, up to L: 36×3 px white tick perpendicular to the line + Rubik Medium 30 px white label, 34 px to the RIGHT of the tick, vertically centred. Hidden when the tick would be beyond L. |
| Range fan | `14123:32471` | Sector centred on the ball, ±11° (`_rangeFanHalfAngleDeg`, tunable) around the CURRENT aim direction, radius = **max reach = 1.20 × club carry** (§ "Over range" below). Fill lime 10 %. |
| Range fan edge | `14123:32472` | The outer arc of the sector: lime 90 %, 6 px on screen. This IS the "max" line. |
| Nominal-carry arc | `14123:32473` | Dashed white 25 % arc, 2 px, same sector, radius = **1.00 × club carry** (the number on the club button). |
| Landing glow | `14123:32481` | Existing conforming disc (`_landingMesh`) recoloured lime: centre `#78E921` α 0.55 → α 0.22 at 55 % → α 0 at edge. Radius unchanged (`_landingZoneRadiusM`). |
| Landing ring | `14123:32482` | Crisp white ring, 4 px on screen, radius = r100 (`carryM × _ringFrac`) — restore ONE conforming ring via the existing `BuildConformingRingGO`/`UpdateConformingRing` (the iter-28 commented-out code path). Rings 80/120 stay off. |
| Crosshair + dot | `14123:104834`, `104835`, `32485` | Screen-space on the indicator canvas at `WorldToScreenPoint(L)`: vertical 3×80 px + horizontal 104×3 px white; 12 px lime dot at the centre. |
| Target readout chip | `14123:32597` | HUD chip. Header navy, Rubik **Bold 44** white: `"{carry} yd"`. Body white, Rubik **Medium 23** navy: `MAPVIEW_TO_PIN` = `"to pin {0}"`. Anchored 130 px to the RIGHT of L, vertically centred on L; flips to the LEFT of L when it would cross the safe area; 8 px radius; drop shadow (0,4) blur 8 α 0.30. Follows L every frame. |
| Hole Indicator | `14123:32491` | The in-game `HoleIndicator` chip + arrow line (prefab visuals used by `HoleIndicatorWidget`), placed with the line tip at the pin's screen point, distance text = ball → pin in the active unit. Replaces the code-built 48 px yellow `FlagIcon`. When the pin is off-screen it docks to the edge exactly as today (Order 355 `_flagArrowRT` logic) — that path keeps its current sprites. |
| Ball marker | `14123:32490` | Existing `_ballMarker`; visual: 44 px white disc, 4 px navy stroke, soft shadow. (Screen-space dot on the indicator canvas at the ball's screen point is acceptable — it must sit ON the line origin.) |
| SHOT VIEW button | `14123:32578` | **New** instance of the `In-Game Select Button` prefab, bottom-LEFT, same anchors/offsets as `GolfinButton` (x = 48, y = 100 from the bottom-left of the 1170×2532 canvas; mirrors `DriverButton`). Portrait: `Icon - ShotView` sprite, navy, centred (≈80×54 px body). Data: `GAMEPLAY_SHOT_VIEW` on two lines ("SHOT" / "VIEW"), Rubik Medium 30, white. Outline, radius and shadow come from the prefab — do NOT restyle. Active ONLY while the map is open. Tap → `MapViewController.Close()`. |
| Club button | `14123:32586` | The existing `DriverButton` instance, bottom-right, unchanged position. In map mode it keeps its normal content (club name + carry, e.g. "DRIVER / 195 yd") — `SetShootMode` no longer swaps the label to SHOOT. Tap still closes the map (existing `RepurposeShootButton` listener). |
| — over range — | `14125:32540` | |
| Over-range segment | `14125:32546` | From the max-reach point `P_max = ball + aimDir × 1.2·carry` to L: same dot pitch, **red `#F23A33`** 95 %. The white dotted line stops at `P_max`. |
| Range fan edge (over) | `14125:32543` | Turns red `#F23A33`, 8 px. Fan fill drops to lime 7 %. |
| Clamped landing ghost | `14125:32553`, `32554` | White dashed ring (4 px, dash 14/12) + 12 px white dot at `P_max` — "this is where the ball actually lands". |
| Target (over) | `14125:32555`…`32558` | Glow red (α 0.5 → 0), ring red, crosshair replaced by a 48×48 red ✕ (6 px strokes). |
| Readout chip (over) | `14125:32578` | Header **red**, Rubik Bold 44 white: `"{carry} yd  ·  " + MAPVIEW_OUT_OF_RANGE`. Body: `MAPVIEW_MAX_HINT` = `"{club} max {maxCarry} — ball lands at the red line"`. Positioned as the normal chip but, when L is near the top of the screen, sits BELOW `P_max` (Figma: centred x, top = P_max.y + 150). |
| Club button (over) | `14125:32591` | `CanvasGroup.alpha = 0.5`, still interactable. SHOT VIEW stays at full alpha. |

## Architecture context

- **Asmdef boundaries affected:** `Golfin.Gameplay.UI` only (`Assets/Scripts/Gameplay/UI/ShotUI/`). No `Input`, no `Physics` changes.
- **Existing code referenced:**
  - `MapViewController.cs` — `Open()` (carry source, `_carryYards`), `UpdateGuideAndRings()` (L, `_aimedCarryM`, `powerPct`, the `powerPct > 1.20f` red rule), `UpdateGuideLine(...)` (`kArcBow`), `BuildRuntimeObjects()`/`BuildLandingZoneDecal()`/`RebuildLandingMesh()`, `BuildConformingRingGO()`/`UpdateConformingRing()` (iter-28 commented-out rings), `BuildHoleIndicator()` (+ Order 355 `_flagArrowRT` docking), `BuildIndicatorPart()`, `RepurposeShootButton()`, `HideShotUIChrome()`, `PlaceMarkers()`, `Close()`/`CloseImmediate()` (`MapTargetCarryM` write-back — untouched).
  - `ClubButtonWidget.SetShootMode(bool)` — label swap + `SelectorDragRouter` toggle.
  - `HoleIndicatorWidget.cs` — distance/unit formatting (`"{yards:F0} yd"` / `"{meters:F0} mts"`), chip + `_arrowLine` visuals.
  - `WindIndicatorWidget.cs` — the HUD chip look the readout copies.
  - `HoleCardWidget.OpenViaWidget()` — map entry, unchanged.
  - `ShotController.MapTargetCarryM` — untouched; note `PowerNormalized` is clamped to 1.2 (overpower ceiling) — that ceiling is why max reach = 1.2 × carry.
  - `ControlsConfig.Default.RingFrac` — landing ring radius.
- **Existing assets referenced:** `Assets/Scenes/Physics/LabScaffold.unity` (`DriverButton` @ line ≈10777, `GolfinButton` ≈4842, `HoleIndicator` ≈18775, `WindIndicator` ≈18722); `Assets/Resources/UI/Icon - Flag.png` (sibling slot for the new icon); `Resources/MapView/DecalLandingZone` material.
- **Manager APIs used:** `LocalizationManager.Get(string)`, `HoleContext.PinWorld`, `ClubContext.SelectedDistance`, `ClubContext.OnSelectedChanged`.

## Implementation

### 1. Range model (pure math, testable)

Add to `MapViewController` (static, no GO dependency, next to `RingRadiusAtPct`):

```csharp
public const float kMaxReachFactor = 1.20f;   // == ShotController overpower ceiling
public static float MaxReachM(float clubCarryM) => clubCarryM * kMaxReachFactor;
public static bool  IsOverRange(float aimedCarryM, float clubCarryM) => aimedCarryM > MaxReachM(clubCarryM) + 0.01f;
public static Vector3 MaxReachPoint(Vector3 ball, Vector3 aimDir2D, float clubCarryM) => ball + aimDir2D * MaxReachM(clubCarryM);
public static int TickCount(float carryM, float tickSpacingM) => Mathf.FloorToInt(carryM / tickSpacingM); // ticks at 1..N × spacing, strictly < carryM
```

`UpdateGuideAndRings()` keeps computing `powerPct`; replace the `powerPct > 1.20f` colour swap with `bool over = IsOverRange(carryM, clubCarryM)` and drive every over-range visual from that one bool. `_aimedCarryM` stays free/unclamped (iter-32) — the ghost ring shows the clamp, the code does not clamp.

### 2. Aim line

- `UpdateGuideLine`: drop `kArcBow` for the map (`arcY = SampleTerrainHeight(bentXZ) + kRingHeightOff`). Keep `kGuideSegments`.
- Dotted look: `_guideLine.textureMode = LineTextureMode.Tile`, material = unlit transparent with a generated 32×8 RGBA texture (one white circle in the left 8×8, rest clear) — generate in code the way `DotSprite()` does (no import). `_guideLine.startWidth = _guideLine.endWidth = _guideDotWorldWidth` (**new `[SerializeField] float`, default 0.9 m**) — NOTE: world width means the dots grow with zoom-out; if Cesar dislikes it, switch to `_guideLine.material.mainTextureScale` driven by `_currentFov`. Flag in the report which you shipped.
- Two `LineRenderer`s from now on: `_guideLine` (white, ball → `min(L, P_max)`) and **new** `_overRangeLine` (red, `P_max → L`, `positionCount = 0` when not over).

### 3. Range fan + arcs

- New conforming mesh `MapView_RangeFan` (reuse the annulus builder: `UpdateConformingRing` generalised to `(center, innerR, outerR, startAngle, endAngle)` — add the angle parameters with defaults 0..2π so existing callers compile). Fan = inner 0 / outer `MaxReachM`, angles `aimYaw ± _rangeFanHalfAngleDeg`. Material: the ring material (ZTest Always) with lime α 0.10.
- Fan edge = a second annulus `outer = MaxReachM`, band = 6 px-equivalent (`kRingBandFrac` scaled), lime α 0.90; red α 0.95 when over.
- Nominal arc = annulus at `clubCarryM`, band 2 px-equivalent, white α 0.25, dashed: build it as 24 gap/segment pairs across the sector (skip every other 7.5°) — no shader work.
- All three rebuild when `_aimYawRadians` or the club changes (same trigger as `UpdateGuideAndRings`).

### 4. Ticks

Screen-space on the existing indicator canvas (`_indicatorCanvas`): pool of 12 (`Image` 36×3 + `TMP_Text` Rubik Medium 30). Each frame: for `i = 1..TickCount(carryM, spacingM)`, world point `ball + aimDir × i·spacing`, `WorldToScreenPoint`, rotate the tick to the line's screen angle + 90°, label to the right by 34 px. `spacingM = 50 yd` in yards mode, `50 m` in metres mode (unit source: the same setting `HoleIndicatorWidget.SetUnitMode` receives — NOTE: find the caller and read the same value). Hide unused pool entries.

### 5. Landing zone

- Glow: recolour the `_landingZoneTex` gradient to lime per the table (the texture is generated in `BuildLandingZoneDecal` — change the colour ramp only).
- Ring: restore `_ring100GO` via `BuildConformingRingGO("LandingRing", white α 0.95, …)` and the one `UpdateConformingRing(_ring100GO, Lground, r100)` call. Leave `_ring80GO/_ring120GO` and `UpdateRingLabels` commented out.
- Crosshair + dot: three `BuildIndicatorPart` entries on the indicator canvas positioned at `WorldToScreenPoint(L)` every frame. Over range: swap the crosshair for the ✕ (two rotated 48×6 bars, red).
- Ghost: `_ghostRingGO` (dashed white ring, 4 px) + dot at `P_max`, active only when over.

### 6. Target readout chip

New prefab `Assets/Prefabs/Gameplay/HUD/MapTargetReadout.prefab` (or built in code with `BuildIndicatorPart`-style helpers — implementer's call, but the LOOK must match `WindIndicator`: white `Image` r8 + navy header `Image` + two TMP texts). Script `MapTargetReadoutWidget : MonoBehaviour` with `Set(float carryM, float toPinM, bool over, string clubName, float maxReachM, DistanceUnit unit)`; formatting copied from `HoleIndicatorWidget` (extract a `static string FormatDistance(float meters, DistanceUnit u)` helper into `HoleIndicatorWidget` and call it from both — do not duplicate the F0/yd/mts code). `MapViewController` owns one instance on `_indicatorCanvas`, positions it per the fidelity table, hides it when the map is closed.

### 7. Hole Indicator at the pin

Replace the body of `BuildHoleIndicator`'s `FlagIcon` block: instantiate the `HoleIndicator` prefab visuals (chip + `_arrowLine`) under `_indicatorCanvas`, driven by a small `MapPinIndicator` helper: chip anchored so the arrow-line tip sits on `WorldToScreenPoint(pin)`; `_distanceText = FormatDistance(|ball − pin|)`. Keep `_flagArrowRT` + the Order 355 edge-docking; the docked state may keep the current arrow sprite. NOTE: if the HUD's `HoleIndicator` is a scene object rather than a prefab, extract it to a prefab first (one scene edit, listed in the report) — do not copy its hierarchy by hand.

### 8. SHOT VIEW button

- LabScaffold: duplicate `DriverButton` → `MapShotViewButton`, parented next to `GolfinButton`, same RectTransform as `GolfinButton` (bottom-left slot), inactive by default. Remove `ClubButtonWidget` + `SelectorDragRouter` from the duplicate; keep the prefab visuals. Portrait `Image.sprite = Resources UI/Icon - ShotView`, `color = #001E39`, preserve aspect, ≈80×54. Data texts: primary = `LocalizationManager.Get("GAMEPLAY_SHOT_VIEW")` with the space rendered as a line break (or two TMP lines: implementer's call — Figma shows two lines), secondary hidden.
- `MapViewController`: `[SerializeField] GameObject _shotViewButton;` `Open()` → `SetActive(true)` after `HideShotUIChrome()` (make sure the chrome-hider exempts it exactly as it exempts `_shootButton`); `Close()`/`CloseImmediate()` → `SetActive(false)`. `Button.onClick → Close`.
- `RepurposeShootButton(true)`: still disables the `SelectorDragRouter` and rebinds `onClick → Close`, but **no label swap**: call a new `ClubButtonWidget.SetMapMode(bool)` that only toggles the router (keep `SetShootMode` for callers that still need it — grep; if none, delete it). Over range: `_shootButton.GetComponent<CanvasGroup>()` (add one) `alpha = 0.5`.

### 9. Strings (two-way importer — mandatory path)

Add to `Assets/Localization/LocalizationText.csv` (EN + JA in the same commit), then `python3 Tools/content/import_content.py --env-file … --catalogs texts` (PLAN → read verdicts → `--apply`) → publish `texts` from the admin → `export_content.py --check` clean. Never code-only.

| Key | EN | JA |
|---|---|---|
| `GAMEPLAY_SHOT_VIEW` | `SHOT VIEW` | `ショット画面` |
| `MAPVIEW_TO_PIN` | `to pin {0}` | `ピンまで {0}` |
| `MAPVIEW_OUT_OF_RANGE` | `OUT OF RANGE` | `射程外` |
| `MAPVIEW_MAX_HINT` | `{0} max {1} — ball lands at the red line` | `{0}の最大 {1} — ボールは赤い線に着地` |

`{0}`/`{1}` via `string.Format`; the club name is the already-localised name the club button shows. Distances go through `FormatDistance` (existing `yd`/`mts` suffixes — no new unit strings).

### 10. Tests (`MapViewAimingTests.cs`, EditMode)

- `MaxReachM(100) == 120`; `IsOverRange(120.0, 100) == false`, `IsOverRange(120.2, 100) == true`.
- `MaxReachPoint` lies on the aim direction at exactly 1.2 × carry.
- `TickCount(178.3 m, 45.72 m /*50 yd*/) == 3`; `TickCount(45.72, 45.72) == 0` (a tick AT L is not drawn).
- Existing 43 tests unchanged and green.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Fidelity table reproduced row by row with PASS/FAIL against `reference/B1_aiming.png` and `reference/B1_over_range.png` (crop sheets of: target area, bottom corners, hole chip, fan edge).
- [ ] Aim line is dotted, constant width, straight, no vertical bow; hugs terrain on Hole 08's slope (screenshot).
- [ ] Ticks appear at 50/100/150 yd on a driver tee shot and disappear when L is dragged inside 50 yd.
- [ ] Fan follows the aim direction while dragging; fan edge sits at 1.2 × the club-button distance (measure in the invariants dump: add `maxReachM` to `DumpInvariants`).
- [ ] Dragging L past the fan edge: red segment + red edge + ghost ring at `P_max` + red chip; dragging back restores all in one frame. `MapTargetCarryM` written back UNCLAMPED (log line quoted).
- [ ] Hole Indicator chip in the map is the same prefab visuals as the HUD chip (side-by-side crop), distance updates as the ball moves between shots.
- [ ] SHOT VIEW button: hidden in shot view, shown in map view, gold outline identical to DriverButton (crop), tap closes the map; DriverButton shows "DRIVER / <carry>" in map view and also closes it.
- [ ] Localization: 4 keys in the CSV with JA, importer `--check` clean (quote it), zero new hardcoded `.text` literals (grep quoted).
- [ ] EditMode: 4 new tests + existing 43 green (count quoted).
- [ ] No white-box placeholders visible in the screenshot
- [ ] All `[SerializeField]` references wired in the Inspector (`_shotViewButton`, `_guideDotWorldWidth`, `_rangeFanHalfAngleDeg`)
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` — range model statics; dotted/flat guide line + `_overRangeLine`; fan/arc meshes; ticks; ring restore; crosshair/✕/ghost; readout ownership; pin chip; SHOT VIEW show/hide; `DumpInvariants` + `maxReachM`.
- `Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` — `SetMapMode(bool)`; `SetShootMode` retired if unused.
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicatorWidget.cs` — `static FormatDistance` extracted.
- `Assets/Scripts/Gameplay/UI/ShotUI/MapTargetReadoutWidget.cs` — NEW.
- `Assets/Scripts/Gameplay/UI/ShotUI/MapPinIndicator.cs` — NEW (small; may live inside MapViewController if < 60 lines).
- `Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs` — 4 tests.
- `Assets/Scenes/Physics/LabScaffold.unity` — `MapShotViewButton` object + wiring (+ `HoleIndicator` prefab extraction if needed). List every serialized change in the report.
- `Assets/Resources/UI/Icon - ShotView.png` (+ .meta, Sprite import) — from `reference/`.
- `Assets/Localization/LocalizationText.csv` — 4 rows.
- `Docs/AI_CONTEXT.md` — one line.

## Smoke evidence

- `MapViewCaptureDriver` / `MapViewStrictCropDemoRecorder` captures of Hole 01 tee (driver) and Hole 08 approach (iron): aiming state, dragged past max, dragged back. Attach under `screenshots/`.
- Invariants JSON (`map_view_invariants_*.json`) regenerated with the new `maxReachM` field; `validate_invariants.py` still passes.
- **Visual-fidelity (Lesson O):** human play-and-confirm note in the report describing what the line, fan and chip did while dragging — dispatch logs are not enough.

## Out of scope (do NOT do these)

- Wind rings on the target / wind ruler (competitor pattern — backlog).
- Any change to aiming maths, `MapTargetCarryM` semantics, the 1.2 overpower ceiling, camera framing, strict-crop, pinch/pan, or `HoleCardWidget` entry.
- Final SHOT VIEW icon art (Robin) — placeholder ships.
- Distance rings 80/120 and their labels (stay commented out).
- Putting: the map is not opened from a putt today; do not add it.
- Versus/bot-turn behaviour (the existing guard stays).
