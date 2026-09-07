# SPEC — `flick_shot_view`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.
> Written 2026-09-07 (Architect, Cowork). Project mirror: `claude/FLICK_SHOT_VIEW_SPEC.md`. **Run after `miss_grade_duff` is DONE** (both touch `ShotConeView`/`ConeMeshGraphic`, `controls.csv`, `LabScaffold.unity`).

## Status

`STATUS.md` starts at `SPEC_READY`.

## Goal

Give Flick — the shipping default scheme — the same framing the other three got in `shot_view_layout`: ball at **62 % from the top** (viewport 0.38), camera pitched up, horizon at ~38 %, cone base on the shared bottom baseline. Cesar, 2026-09-07: *"Flick stayed the same as before. Shouldn't we adjust the camera as well?"* — yes. `shot_view_layout` D1 left Flick at 0.5 only because its cone is 1009 px tall; this task shortens the cone to fit, which is a deliberate change to the shipping scheme, not a layout tweak. The `control_scheme_seam` "Flick byte-identical" invariant is **retired by this spec**.

## 1. Decisions

| # | Decision |
|---|---|
| D1 | `BallAnchorViewportY_Flick` **0.5 → 0.38**. Same anchor as the other three; the camera follows via `GetAimBallViewportY` (no camera code). |
| D2 | Cone height **1009 → 641 px** so the base sits on `BottomBaselinePx` (170 → canvas y −1096) with the apex gap under the ball kept at 151: `641 = 1096 − 304 − 151`. New csv key `FlickConeHeightPx`. The 3× club head overhangs the base at full pull exactly as the capped pill does in Pendulum/Free Swing. |
| D3 | The handle's rest position stays the same **fraction** of the cone (785/1009 = 0.778 → 499 px): `FlickHandleStartY01 = 0.778`, replacing the absolute `_handleStartYPx`. Full-power finger travel from rest becomes 499 px (was 785); Pendulum/Free Swing are 540 to 100 %, so the schemes now pull alike. |
| D4 | Bands, slab and arrows are already fractions of the cone (`TimingBandRedY01/GoldY01/GreenY01`, `TimingSlabGraphic.SetConeParams`) — no re-tuning. Cone half-angle unchanged (5°–20°), so the base half-width shrinks 367 → 233 px at 20°; aim resolution at full pull drops proportionally. Accepted; Club Accuracy still maps the same way. |
| D5 | Putter track **1000 → 605 px** (`FlickPutterTrackHeightPx`): track top stays 187 under the ball, bottom on the baseline. Putt anchor is the same 0.38 (putt camera has its own distance/height, `_puttCamDistanceM/HeightM`, untouched). |
| D6 | Short canvases: same clamp as the lane schemes — the ball rises so the cone base never drops below the baseline: `ballY = max(anchor, canvasBottom + baseline + FlickConeHeightPx + 151)`. Flick joins `ResolveBallY` with `schemeHasLane = true`, lane depth = `151 + FlickConeHeightPx`. |
| D7 | Flick's confirm-pop-up tiles (3) are recaptured — geometry changed. `TimingBandRedY01` from `miss_grade_duff` is drawn on the shorter cone unchanged (fraction). |
| D8 | Out of scope: 120 % overpower on the Flick cone (backlog), cone half-angle retune, any `ShotController` change. |

## 2. Numbers (canvas px, 1170 × 2532, origin centre)

| Element | Today | Target |
|---|---|---|
| `CentralBall` y | 0 (vy 0.50) | **−304** (vy 0.38) |
| Cone apex | ball −151 = −151 | ball −151 = **−455** |
| Cone base (`ConeMesh` anchoredPosition) | ball −1160 = −1160 | ball −792 = **−1096** = baseline |
| Cone height | 1009 | **641** |
| Handle rest (cone-local y) | 785 | **499** (0.778 × 641) |
| Handle rest (canvas y) | −375 | **−597** |
| Cone base half-width at 20° / 5° | 367 / 88 | 233 / 56 |
| `PutterTrack` top / bottom | −187 / −1187 | **−491 / −1096**; height 605 |
| `FlickGradePop` | ball +289 | moves with `BallSpace` (no change) |
| Horizon (Lomond hole 2, from `shot_view_layout` measurements) | 25.5 % | ~37.8 % |

## 3. Implementation

### 3.1 Config — `ControlsConfig` + `controls.csv`

```
BallAnchorViewportY_Flick,0.38,flick_shot_view (2026-09-07): 0.5 -> 0.38, same anchor as the other three schemes. The cone is shortened to fit (FlickConeHeightPx).
FlickConeHeightPx,641,flick_shot_view: cone height in canvas px (was a serialized 1009 on ShotConeView/ConeMeshGraphic/TimingSlab). 641 = BottomBaselinePx line − ball(0.38) − 151 apex gap, so the base sits on the shared baseline. Bands/slab/arrows are fractions of it. Cesar tunes; the D6 clamp raises the ball if it does not fit.
FlickHandleStartY01,0.778,flick_shot_view: club-handle rest as a FRACTION of the cone height (was _handleStartYPx 785 absolute = 785/1009). 0.778 × 641 = 499 px of travel from rest to 100 %.
FlickPutterTrackHeightPx,605,flick_shot_view: putter track height (was serialized 1000); top stays 187 under the ball, bottom on the baseline.
FlickConeApexGapPx,151,flick_shot_view: apex-to-ball gap (was implicit in ConeMesh's −1160 anchoredPosition). Kept.
```

Fields + `Default` seeds + loader cases as in every prior key.

### 3.2 `ShotConeView` reads the config

- `_coneHeightPx`, `_handleStartYPx`, `_putterTrackHeightPx` stop being the source of truth: on `Awake` (where it already does `_coneGraphic.HeightPx = _coneHeightPx`, line ~143) set them from cfg: `_coneHeightPx = cfg.FlickConeHeightPx`, `_handleStartYPx = cfg.FlickHandleStartY01 * _coneHeightPx`, `_putterTrackHeightPx = cfg.FlickPutterTrackHeightPx`. Keep the serialized fields as Inspector fallbacks when no config is loaded (Editor scenes without the loader).
- Sizes that follow: `TimingSlab` rect (`sizeDelta (400, _coneHeightPx)`, line ~208), `_timingSlab.SetConeParams(_coneHeightPx, …)` (line ~382), `PutterTrack` rect height and `_putterTimingSlabRT` range (line ~371) — all already read the fields, verify none has a second literal.
- `ConeMesh` position: `anchoredPosition.y = −(cfg.FlickConeApexGapPx + _coneHeightPx)` set by `ShotConeView` on `Awake` (today −1160 is scene-authored). `PutterTrack` is anchored to the canvas top at −1453 — re-express it as ball-relative (`−(187)` under the ball inside `BallSpace`) so it moves with the anchor; it currently only works because `BallSpace` offsets it.
- `ClubHandleDragger.ConeHeightPx` already reads `_coneGraphic.HeightPx` → no change.

### 3.3 `ShotLayoutController` / `ShotLayoutMath`

Flick becomes a lane scheme for the clamp: `schemeHasLane = true`, `pull120Px + handleRestBelowBall` replaced by `FlickConeApexGapPx + FlickConeHeightPx` (add an overload or a `laneDepth` parameter to `BallYForLane` — keep the existing signature for the other three). The ball anchor path is already generic.

### 3.4 Scene — `LabScaffold.unity`

`ConeMesh` anchoredPosition, `TimingSlab` size, `PutterTrack` anchoring updated to match the runtime values so the Editor view without play mode is honest (the runtime write is the authority). Nothing re-parented — `BallSpace` exists since `shot_view_layout`.

### 3.5 Tests

- `ShotLayoutMathTests`: Flick at 1170×2532 → ball −304, cone base −1096; 16:9 → cone base on −1040 + 170 with the ball raised; 4:3 documented.
- New `ShotConeViewConfigTests` (or extend an existing EditMode suite that instantiates the view): with cfg 641/0.778/605 the cone graphic height, handle rest y and putter track height read back 641 / 499 / 605; with no cfg the serialized fallbacks hold.
- `ShotAimParityTests`, `ShotTimingPowerTests`, `ShotControllerFlickGateTests`: expected to pass **unchanged** — they drive `ShotController` in normalised power, not cone px. If one encodes 1009 or 785, that is the one place a fixture may change; say which.
- Bots: `FlickBotExecutor` drives `SetExternalPower` normalised → prove with the existing bot tests; `bot_difficulty.csv` untouched.

### 3.6 Tiles

Run `GOLFIN ▸ Capture ▸ Scheme Confirm Tiles` for **Flick only** (the height-driven crop from `shot_view_layout_followup` handles the 641 cone). Commit the three Flick tiles; the other nine byte-identical.

## 4. Acceptance

- [ ] Flick, Lomond hole 2, 1170×2532: `CentralBall` y −304 (vy 0.38); horizon measured ≥ 35 % from the top; aim-camera pitch before/after quoted (expect ≈ 12.5° → 4.6° as Pendulum got).
- [ ] Cone apex −455, base −1096 (on the baseline), height 641; handle rest at canvas y −597; cone base does not overlap the action buttons (x ±233 at 20° vs buttons at ±382 inward edge).
- [ ] Full pull reaches 100 % at the base; bands drawn at 0.15 / 0.45 / 0.85 of the 641 cone; PURE / GOOD / THIN / DUFF pops at the four bands still fire (the `miss_grade_duff` live check re-run).
- [ ] Putt: track top −491, bottom −1096; putt slab travels the full track; putt camera unchanged.
- [ ] 16:9 Game View preset: cone base on the baseline, ball raised, no button overlap.
- [ ] Scheme switch at Idle Flick ↔ Pendulum: both frame at 0.38, no camera pop.
- [ ] Flick tiles recaptured (3), other nine byte-identical (`git diff --stat`).
- [ ] EditMode run count quoted; which (if any) Flick fixture changed and why.
- [ ] Device (Cesar): the shorter pull feels right against Pendulum's; 100 % reachable without the thumb leaving the glass; framing matches Figma 14153:4602 by eye.

## 5. Out of scope (backlog)
120 % overpower on the Flick cone; cone half-angle retune for the shorter base; tablet anchor table (shared row).

## 6. Files
`ControlsConfig.cs`, `ControlsConfigLoader.cs`, `controls.csv`, `ShotConeView.cs`, `ShotLayoutController.cs`, `ShotLayoutMath.cs`, `LabScaffold.unity`, `ShotLayoutMathTests.cs`, new `ShotConeViewConfigTests.cs`, three Flick tiles, `Docs/AI_CONTEXT.md`, `Docs/CONTROL_SCHEMES_PLAN.md` (one line: the byte-identical invariant is retired here).
