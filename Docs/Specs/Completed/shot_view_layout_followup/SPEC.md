# SPEC — `shot_view_layout_followup` (Quick)

> Follow-up to `shot_view_layout` (ARCHITECT_REVIEW_PASS, 28904bac8). Two small items Cesar decided 2026-09-07. Written by the Architect (Cowork). Project mirror: `claude/SHOT_VIEW_LAYOUT_FOLLOWUP_SPEC.md`. Can run alongside `miss_grade_duff` — no file overlap except `LabScaffold.unity` if the tile capture dirties it (it should not; capture is read-only).

## Status

`STATUS.md` starts at `SPEC_READY`.

## 1. Cap the pill at the bottom baseline (Cesar: "cap the pill")

**Problem.** `ClubHalfHeight` went 50 → 150 (3× club head) so the drawn Pendulum / Free Swing lane now ends at canvas y −1191.84, 96 px below the shared baseline (−1096) that the action buttons and selector overlays sit on. The clamp correctly guards the finger (D3), so the pill's tail is the thing to cap.

**Change.** `PendulumLaneView` and `FreeSwingLaneView` gain `SetLaneEndCapY(float canvasY)` (canvas-centre space, same space `ShotLayoutController` already works in). In their `Apply/Rebuild`:

```
derived  = deepestTick + _clubHalfHeight + _laneTailPx            // today's LaneHeight
capped   = laneTopY - laneEndCapY                                  // pill may not extend past the baseline
LaneHeight = Mathf.Min(derived, capped)                            // never below deepestTick + a minimum tail (8 px)
```

- `laneTopY` is the lane rect's top in canvas space (Pendulum: the ball; Free Swing: ball + `FollowThroughPx`).
- Floor: `LaneHeight ≥ deepestTick + 8` so the 120 % tick is never on the rounded end.
- **The pull clamp is untouched** — both drivers clamp on `cfg.*Pull120Px` (`PendulumSchemeDriver` ~422, `FreeSwingSchemeDriver` ~548), not on `LaneHeight`. Assert it with a test (below). The 3× club head at full pull overhangs the pill's rounded end by ~100 px — intended.
- `ShotLayoutController.Apply` calls `SetLaneEndCapY(-H/2 + baseline)` on both lane views right after it resolves the baseline (it already holds both view references). Default when never set: `float.NegativeInfinity` → no cap (Editor scenes without the controller are unchanged).
- Putts: same rule (a putt's lane is shorter; the cap is a no-op there — assert).

**Tests** (`ShotLayoutMathTests` or a new `LaneEndCapTests`): 1170×2532 Pendulum → `LaneHeight` = 888 − 96 = **792**, lane end = **−1096**; Free Swing → lane end −1096; 16:9 (H 2080) → lane end = −1040 + 170; a cap above `deepestTick + 8` is ignored (floor wins); `PendulumSchemeDriver` full pull still reaches 120 % with the capped lane (`PullPxForPower(1.2)` unchanged).

## 2. Scheme confirm tiles — height-driven crop, then recapture

**Problem.** `Assets/Editor/ShotUI/SchemeConfirmTilesCapture.FitCrop` derives the crop from the subject box at the tile aspect (314×340 → 0.92) and clamps **width** to `MaxCropW = 900`, so height is capped at ~975 px. With the ball at 0.38 the Pendulum subject (aim bar at ball +128 down to the lane end) is ~1100 px tall → the crop clips the aim bar and the 100 %/120 % labels. Code recaptured and reverted; the three moved schemes' tiles still show the old layout.

**Change.**

1. `FitCrop`: keep the tile aspect but let **height drive** when the subject is taller than it is wide: `h = subject.height·(1+2m)`; `w = max(h·aspect, subject.width·(1+2m))`; clamp `w` to `[MinCropW, MaxCropW]` **and** clamp `h` to a new `MaxCropH = 1300` independently. If `w` hits `MaxCropW` while `h` needs more, **do not** cut `h` — the crop is then taller than the tile aspect and is letterboxed on write.
2. `WriteTile`: fit the crop **inside** `TileW×TileH·Scale` preserving aspect (scale = min), centred, padding filled with the tile background the pop-up already draws behind the image (read the colour from the `SchemeConfirmContent` tile background, do not hard-code a new one). Cesar's 2026-09-05 rule stands: subject-bbox crops, images centred.
3. HUD-chrome exclusion (§3.2 of `scheme_confirm_popup`: "No HUD chrome may appear in a tile") unchanged; the shrink-and-nudge loop still runs on the final crop. With the ball lower the `PowerHUD` (now top-right at vy 0.70) is far from every subject — expect zero nudges; write the per-tile crop rects in the report.
4. Run `GOLFIN ▸ Capture ▸ Scheme Confirm Tiles` on the new layout (after §1 so the capped pill is what gets captured). Commit the Pendulum, Needle and Free Swing tiles. **Flick tiles byte-identical** (D1 of `shot_view_layout`); prove with `git diff --stat` on the Flick tile paths.
5. Update the `controls.csv` header note only if the capture menu path or rule changed (it did not — leave it).

**Acceptance:** side-by-side PNG old vs new for each of the nine recaptured tiles in `screenshots/`; every tile shows the full aim bar / ring / lane including the 120 % label and the club head at rest; no HUD chrome inside any crop (assert list empty); Flick tiles unchanged; pop-up opened in play mode for all four schemes and the tiles read at the in-game size (screenshot each pop-up).

## Out of scope
Whiff / duff (in `miss_grade_duff`), Figma frame updates (backlog), any change to `Pull*Px` or the ball anchor.

## Files
`PendulumLaneView.cs`, `FreeSwingLaneView.cs`, `ShotLayoutController.cs`, `ShotLayoutMathTests.cs` (or new `LaneEndCapTests.cs`), `Assets/Editor/ShotUI/SchemeConfirmTilesCapture.cs`, nine tile PNGs under the confirm pop-up's tile folder, `Docs/AI_CONTEXT.md`.
