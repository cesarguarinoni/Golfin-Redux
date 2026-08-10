# SPEC — power_gauge_target_marker

**Status:** SPEC_READY (Cesar go 2026-08-10)
**Filed:** 2026-08-10 (Architect)
**Files:** `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` (write-back only, ~10 lines), `Assets/Scripts/Gameplay/Input/ShotController.cs` (one property + reset), `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs`, `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeGraphic.cs`. Tests: extend `MapViewAimingTests.cs` or a new `PowerGaugeMarkerTests.cs` (EditMode).
**Relationship to map view:** INDEPENDENT of `map_view_strict_crop_indicators` (Order 355). Both touch `MapViewController.cs` but disjoint regions (355 = framing/pan/indicators; this = `CloseImmediate()` write-back). Sequence AFTER 355 if the queue is serial to avoid a same-file merge; technically parallel-safe.

---

## 1. Goal (Cesar, 2026-08-10)

Design decision (locked after the target-locked vs club-relative discussion): power stays
**club-relative** — the flick mechanic and its F13 tuning are untouched — but the landing
target the player places in map view becomes a **marker on the power gauge**: a tick at
the % of club carry that would land the shot on that target. The player executes the
flick toward a visible goal instead of doing mental math. Explicitly NOT Golf Clash-style
target-locked power: no recalibration of the perfect zone, no change to `ComputePower`,
overpower rules, or any `ControlsConfig` value.

## 2. The gap this closes

Today the map's landing point is free-placed (`_aimedCarryM`, iter-32: finger sets heading
AND distance) but on `CloseImmediate()` only the YAW survives —
`_shotController.CameraHeadingRadians = _aimYawRadians` + `WriteBackAimToPhysicsLab`
(MapViewController L514–517). `_aimedCarryM` dies with the map session. The map target
currently has zero power meaning.

## 3. Design

### 3.1 Write-back seam (ShotController + MapViewController)

- `ShotController`: new public property `float MapTargetCarryM { get; set; }`, default
  `-1f` = no target. Stored in METERS, NOT normalized — so a club change after setting the
  target just moves the marker (fraction is recomputed against the new club's carry);
  no reset-on-club-change logic needed, and the marker stays truthful.
- Reset to `-1f` when the shot is committed (same place `PowerNormalized`/flick state is
  consumed at flick-commit → the next shot starts markerless until the player maps again).
  NOTE: implementer picks the exact reset site in the state machine (`TransitionToTiming`'s
  commit path or the post-shot Idle transition) — the rule is "one marker per mapped shot,
  never stale across strokes"; document the chosen site in the report.
- `MapViewController.CloseImmediate()`: alongside the existing yaw write-back, add
  `_shotController.MapTargetCarryM = (_aimedCarryM > 0f) ? _aimedCarryM : -1f;`
  Guard: only when the map session actually had a landing (`_aimedCarryM > 0f` — it is
  `-1f` until first open / reset at each `Open()`, L468). Opening and closing the map
  WITHOUT touching the aim still writes the default club carry — that is correct
  (the map showed the player that landing; it IS the current target).

### 3.2 Marker fraction (pure-math seam, EditMode-testable)

```
frac = MapTargetCarryM / clubCarryM          // clubCarryM = ClubContext.SelectedDistance yds → ×0.9144
markerFrac = Mathf.Clamp(frac, 0.02f, 1.2f)  // 1.2 = overpower ceiling, matches ShotController's Clamp(power, 0, 1.2)
```

- Carry source MUST be `ClubContext.SelectedDistance` — the same authority the map's
  landing default and `HoleCardWidget` HUD use (Fix 1 lineage), NOT
  `PowerGaugeWidget._maxCarryYards`. ⚠️ VERIFIED GAP: nothing calls
  `PowerGaugeWidget.SetMaxCarryYards()` today — `_maxCarryYards` sits at its 250f default,
  so the widget's yards TEXT is already suspect. In scope: wire the widget's max carry from
  `ClubContext.SelectedDistance` at the same place `SetUnitMode(Yards)` is called
  (PhysicsLabController L546), fixing the text and the marker with one source. Report the
  before/after of the yards text.
- `frac > 1.2` (player placed the landing beyond overpower reach — the map draws that
  state red already): pin the marker at 1.2 AND tint it the map's over-power red so
  "unreachable with this club" reads at a glance.

### 3.3 Rendering (PowerGaugeGraphic)

- The gauge is a radial arc: `Progress01` × 360°, 12-o'clock start, overpower past 360° →
  maroon (`ArcColor`). The marker is a thin radial NOTCH at `markerFrac × 360°`: one quad
  from `_innerRadius − 4` to `_outerRadius + 4`, ~2.5° wide, drawn AFTER the fill quads in
  `OnPopulateMesh` so it renders on top. White `#FFFFFF` at default alpha 0.95; over-reach
  state per §3.2. New members: `public float MarkerFrac01 { get; set; }` (< 0 = no marker,
  skip drawing; `SetVerticesDirty` on change) + serialized notch width/overhang/colors.
- `PowerGaugeWidget.HandleStateChanged`: each update, push
  `_gauge.MarkerFrac01 = ComputeMarkerFrac(_shotController.MapTargetCarryM)`.
  Full-swing (Yards mode) ONLY — in putter/Meters mode force `MarkerFrac01 = -1` (map view
  is not the putter targeting tool; the green grid is).
- No new GameObjects, no scene edits — the notch is vertices in the existing graphic, so
  `ShotInProgressUiGate` and the widget's CanvasGroup show/hide cover it for free.

### 3.4 Optional polish (build ONLY if trivial after the above)

`_pctText` gains a second line `→ NN%` (the marker %) in a smaller size. If it needs
layout surgery, SKIP — flag in the report instead. No localization impact (numerals only).

## 4. Out of scope

Target-locked power / perfect-zone remap (explicitly rejected for now — revisit after
playtests); any `ControlsConfig`/flick-curve change; putter mode; bots (`BotDriver` never
reads the gauge); scene edits; new art (notch is procedural).

## 5. Tests

- **EditMode:** `ComputeMarkerFrac` — no target (−1) → no marker; target == carry → 1.0;
  half carry → 0.5; beyond 1.2 → pinned 1.2 + over-reach flag; club change (carry input
  changes, meters constant) → fraction moves accordingly. Reset rule: committed shot →
  `MapTargetCarryM == -1`.
- **Editor manual (report + screenshots):** map a target mid-fairway → close → gauge shows
  notch at the matching %; pull the flick to the notch → ball lands ≈ the map target
  (within the physics scatter — state the observed delta); change club → notch moves;
  shoot → notch gone next stroke until map is reopened; putter → no notch; yards text now
  matches the selected club's carry (the §3.2 wiring fix).

## 6. Verification note for the Architect pass

The landing math (`L = ball + aimDir · carryM`) and the flick power curve
(`ComputePower`) are independent systems — the marker asserts "flick to NN% of club
carry", which is only as honest as `SelectedDistance` ≈ actual carry at 100% power.
P-006 (club carry populated as a stopgap) is STILL OPEN and directly gates how truthful
this marker is. Not a blocker — the marker is exactly as accurate as the map's landing
preview today — but P-006 should be scheduled soon after; note any observed 100%-flick
vs `SelectedDistance` mismatch in the report as P-006 evidence.
