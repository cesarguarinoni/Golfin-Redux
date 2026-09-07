# Architect Review — `flick_shot_view`

**Iteration:** 1
**Reviewer:** golfin-reviewer
**Timestamp:** 2026-09-07 18:03 JST
**Verdict:** **PASS** — set `STATUS.md` to `READY_FOR_REDTEAM` (hand to `golfin-redteam-reviewer`).

## Independent visual scan (Step 0, pre-report)

Portrait Hole 2 (Lomond, Par 4) 3D scene. Top-HUD chips render cleanly (JAMES Lv 12 / Turn 1 left;
LOMOND / HOLE 2 - REGULAR / PAR 4 right; mini-map top-right; settings gear top-right corner). Two
chip callouts float below the HUD: `6.4 mph` wind indicator at left, `374 yds` flag distance
centered under a small flag icon with a vertical white aim line dropping toward the ball. A power
gauge dial at right shows `55%` / `125.4 yd`. Centre of frame: a white Golfin-branded ball sits
above ground with two dark practice balls flanking left and right. Directly BELOW the ball a large
translucent green cone tapers downward — its APEX touches the ball with no visible gap; its base
extends near the bottom edge — with a black driver clubhead sprite overlapping the cone's
midsection (the flick handle in mid-pull), and a thin horizontal red band-line just below the club
near the cone's base. Bottom UI row is intact: SPIN (top-left), GOLFIN∞ (bottom-left), STRAIGHT
(top-right, aim), DRIVER 228 yrds (bottom-right, club). No visible timing tick labels in this pull
frame. No cropping/occlusion of the four corner cards by the cone half-base.

That pixel account matches the substance of what this task claims to have moved: ball at 0.38,
apex touching the ball with no gap, cone base on the shared button baseline. It also confirms
Cesar's mid-run overrides landed (apex on ball, not 151 px below).

## Step 1 — Contract and prior verdicts (read in prescribed order)

Read `SPEC.md` (98 lines), `SELF_REVIEW.md` (239 lines), then `IMPLEMENTER_REPORT.md` (300 lines).
No reference render is required for a framing task (`SPEC.md` cites Figma node `14153:4602`; the
canonical render `Docs/Specs/Completed/shot_view_layout/reference/figma_14153-4602_shot_view.png`
already sits in that completed task's folder and defines the framing this task applies to Flick).

## Step 2 — Task classification

Not a mesh/terrain task (Rule 16 does not gate). Figma-node UI task by classification (Rule 18
gates), but the node is a FRAMING reference already accepted for the same target; every element
already shipped by `shot_view_layout` on the three lane schemes. This iteration extends the
framing to Flick and shortens the cone/track — geometry moves, not new UI. Rule 18 § Figma
fidelity table below.

## Step 3 — Re-derivation from primary sources (NEVER from the report)

### a. Config values — the two mirrors F13 requires

Parsed `Assets/Resources/Gameplay/controls.csv` AND matched the `ControlsConfig.Default {…}` block
programmatically (script pasted at end of my session log):

| key | controls.csv | ControlsConfig.Default | Loader case | match |
|---|---|---|---|---|
| `BallAnchorViewportY_Flick` | `0.38` | `0.38f` | present @ L132 | OK |
| `FlickConeHeightPx` | `792` | `792f` | present @ L137 | OK |
| `FlickHandleStartY01` | `0.6818` | `0.6818f` | present @ L138 | OK |
| `FlickPutterTrackHeightPx` | `792` | `792f` | present @ L139 | OK |
| `FlickPutterTrackTopBelowBallPx` | `0` | `0f` | present @ L140 | OK |
| `FlickConeApexGapPx` | `0` | `0f` | present @ L141 | OK |
| `PendulumPull100Px` (parity anchor) | `540` | `540f` | present | OK |
| `FreeSwingPull100Px` (parity anchor) | `540` | `540f` | present | OK |

F13 two-mirror rule: **PASS**. No key drifts between the CSV and the seeded default.

### b. Geometry — re-derived from the config, checked against the invariant JSON

Origin-centre, H = 2532. My re-derivation:

| quantity | formula | my value | JSON value | match |
|---|---|---|---|---|
| ball y | `(vy − 0.5) × H` = `(0.38 − 0.5) × 2532` | −303.8400 | −303.8400 | PASS |
| cone apex y | `ball_y − apex_gap` = `−303.84 − 0` | −303.8400 | −303.8401 | PASS |
| cone base y | `apex_y − cone_h` = `−303.84 − 792` | −1095.8400 | −1095.8400 | PASS |
| baseline y | `−(H/2 − 170)` = `−(1266 − 170)` | −1096.0000 | −1096.0000 | PASS |
| base vs baseline | | 0.16 px above | 0.16 px above | PASS |
| handle rest y | `base + FlickHandleStartY01 × cone_h` = `−1095.84 + 0.6818 × 792` | −555.8544 | −555.8545 | PASS |
| pull travel | `FlickHandleStartY01 × cone_h` = `0.6818 × 792` | 539.9856 | 540.0 | PASS |
| pull == PendulumPull100Px | `540` | 540 | 540 | PASS |
| pull == FreeSwingPull100Px | `540` | 540 | 540 | PASS |
| cone half-base @ 20° | `cone_h × tan20°` = `792 × 0.36397` | 288.2644 | 288.2644 | PASS |
| cone half-base @ 5° | `cone_h × tan5°` = `792 × 0.08749` | 69.2910 | 69.2910 | PASS |
| button clearance | `382 − 288.26` | 93.74 px clear | 94 px asserted | PASS |
| putt track top y | `ball_y − putt_gap` = `−303.84 − 0` | −303.8400 | −303.8401 | PASS |
| putt track bottom y | `top − putt_h` = `−303.84 − 792` | −1095.8400 | −1095.8400 | PASS |

**Every quantity in the JSON is derived from the five new config keys plus the two Pendulum/Free
Swing pull keys.** Nothing in the JSON is a hand-typed number that doesn't reconcile.

### c. UIFidelityLinter re-read

`Docs/Diagnostics/_capture/SchemeRoot_Flick_lint.json` — `fail: 0`, `warn: 2`. Both warnings are
pre-existing and re-inherited from `miss_grade_duff`:
- `BallSpace/PutterTrack/PutterTimingSlab :: flat-fill` (intentional flat white quad, predates
  this task).
- `BallSpace/FlickGradePop/GradeText :: unlocalized-text` (raised and accepted in
  `miss_grade_duff`).

Task-introduced findings: **0**. PASS.

### d. Standing bans

```
git status --porcelain -- Assets/Scripts/Physics/                              (empty)
git status --porcelain -- Assets/Scripts/Gameplay/Input/                       (empty)
git status --porcelain -- Assets/Resources/Data/bot_difficulty.csv             (empty)
```

**Note:** the session-start `gitStatus` snapshot at the top of this conversation showed
`Assets/Scripts/Gameplay/Input/ShotController.cs` and `ShotIntent.cs` as `M`. My live check at
2026-09-07 18:03 JST shows both clean — those must have been reverted between the snapshot and my
run, or the snapshot was stale. On-disk truth is clean. PASS.

### e. Scene mutation audit

```
git diff --numstat -- Assets/Scenes/Physics/LabScaffold.unity
    14      12      Assets/Scenes/Physics/LabScaffold.unity
```

Every hunk is a cone/handle/track field. **`m_IsActive` change count in the scene diff: 0.** All
changed lines:

| field | before | after |
|---|---|---|
| ConeMesh.m_AnchoredPosition.y | −1160 | −792 |
| ConeMeshGraphic._heightPx | 1160 | 792 |
| TimingSlab.m_SizeDelta.y | 1009 | 792 |
| TimingSlabGraphic._coneHeightPx | 1009 | 792 |
| ShotConeView._coneHeightPx | 1160 | 792 |
| ShotConeView._coneApexGapPx | (new) | 0 |
| ShotConeView._handleStartYPx | 960 | 540 |
| ShotConeView._putterTrackHeightPx | 1000 | 792 |
| ShotConeView._putterTrackTopBelowBallPx | (new) | 0 |
| PutterTrack.m_AnchoredPosition.y | −1453 | −1266 |
| PutterTrack.m_SizeDelta.y | 1000 | 792 |
| PutterTrackGraphic._height | 1000 | 792 |
| PutterTrackGraphic._greenBandHeight | 200 | 158.4 |
| PutterTrackGraphic._amberBandHeight | 300 | 237.6 |

Zero collateral drift, zero VLG repaint noise (the report's filter held). PASS.

### f. Tile audit — MD5 all twelve tiles against `git show HEAD:`

| tile | png status | meta diff lines |
|---|---|---|
| T_Flick_1 | CHANGED | 0 |
| T_Flick_2 | CHANGED | 0 |
| T_Flick_3 | CHANGED | 0 |
| T_Pendulum_1..3 | IDENTICAL | 0 each |
| T_FreeSwing_1..3 | IDENTICAL | 0 each |
| T_Needle_1..3 | IDENTICAL | 0 each |

Exactly `T_Flick_1/2/3.png` changed; the other nine byte-identical; all twelve `.meta` unchanged
(the "new PNG imports as a Texture" trap is avoided). PASS.

### g. Test diff audit

```
git diff --stat -- Assets/Scripts/Gameplay/Tests/
  Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs | 86 +++++-+++
  1 file changed, 73 insertions(+), 13 deletions(-)
```

- `ShotConeViewConfigTests.cs` (+ `.meta`) — new, untracked (correct).
- `ShotAimParityTests`, `ShotTimingPowerTests`, `ShotControllerFlickGateTests` — **zero diff**.
- Grep `\<785\>|\<960\>|\<1009\>|\<1160\>` across `Assets/Scripts/Gameplay/Tests/`: **one hit**,
  a doc-comment in `ShotConeViewConfigTests.cs` L16 (`scene-authored <c>-1160</c>`). Zero
  assertions on old literals. PASS.

### h. Pull-parity assertion binds to config, not to a literal

`Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs` L231-234:

```csharp
Assert.AreEqual(_cfg.PendulumPull100Px, rest, 1f,
    "a thumb travels the same distance to 100% in Flick as in Pendulum");
Assert.AreEqual(_cfg.FreeSwingPull100Px, rest, 1f, "and in Free Swing");
```

Both assertions read live off `_cfg.*`, not the number 540. A Pendulum retune that silently
un-matched Flick would fail the build. There is a documentation-of-intent line
`Assert.AreEqual(540f, rest, 1f, …)` alongside; the two parity-gate assertions above are the
real gate. PASS.

### i. Canonical screenshot floor

`screenshots/flick_pull_792cone.png` is 1170 × 2532, 3.5 MB. Long edge 2532 >> 900 px floor
(Rule 14). PASS.

### j. Bbox verification (Rule)

No "text inside container" containment claim in this task. The containment-shaped claim IS
geometric: cone base on baseline (0.16 px above, sub-pixel), apex on ball (identical to
4 decimals), handle rest inside the cone (identical to 4 decimals). All three re-derive above
from primary source. PASS.

## Step 4 — Acceptance list re-run (SPEC § 4)

Rule 5 requires the ENTIRE acceptance list re-verified. Every row below re-derived from a primary
source, not from the report.

| SPEC row | My re-verification | Verdict |
|---|---|---|
| Flick, Lomond hole 2, 1170×2532: `CentralBall` y −304 (vy 0.38) | ball y = (0.38 − 0.5) × 2532 = **−303.84**, viewport 0.3800; canonical PNG is 1170 × 2532 | PASS |
| Horizon ≥ 35 % from top | 34.24 % on this task's ruler vs 34.28 % on `shot_view_layout`'s accepted `pendulum_038_hole2.png` (same ruler, quoted 37.8 % under that task's own ruler). Same-ruler substitution against an accepted frame is sound; camera pitch identical to Pendulum's to 4 decimals is the stronger proof; see § Horizon-ruler judgement below | PASS* |
| Aim-camera pitch ≈ 12.5° → 4.6° | JSON `aim_cam_pitch_before/after` = 12.5004° → 4.6115°; camera position IDENTICAL `(122.05, 15.40, −134.25)` in both — the ball anchor is the only thing that changed | PASS |
| Cone apex −455 (**superseded by Cesar mid-run to 0-gap**) | apex on ball, gap 0, per Cesar: *"The cone's top point should reach the ball, not leave empty space."* Apex y −303.84 = ball y −303.84; canonical frame visually confirms | PASS (corrected target) |
| Cone base −1096 on baseline, height 641 → **corrected to 792** | 792 = 1096 − 304 − 0 (the 0-gap decision propagates). Base −1095.84 vs baseline −1096.00 (0.16 px sub-pixel) | PASS (corrected target) |
| Handle rest at canvas y −597 → **corrected to −556 (pull parity)** | −555.85 = −1095.84 + 0.6818 × 792; pull travel 540 == `PendulumPull100Px` == `FreeSwingPull100Px` (all read live from `_cfg`) | PASS (corrected target) |
| Cone base does not overlap action buttons | half-base@20° = 792 × tan20° = 288.26 vs button inner edge 382.0 → 94 px clear | PASS |
| Full pull reaches 100 % at the base | `ClubHandleDragger.ProcessDrag` reads `power = 1 − handleY/ConeHeightPx` off `_coneGraphic.HeightPx`, which `ShotConeView.Awake` sets from the config — so 100 % at the base by construction, at any height. Canonical pull frame reads 55 % on the gauge (matches the pull-y in the JSON: `pull_handle_y = -852.85`, which is 0.55 × 792 above the base) | PASS |
| Bands at 0.15 / 0.45 / 0.85 of cone | `evidence/miss_grade_duff_rerun/flick_pops.md` logs `cone_bands: red=0.15 gold=0.45 green=0.85`, bands are fractions of `SetConeParams(_coneHeightPx, …)` so they moved with the cone | PASS |
| PURE / GOOD / THIN / DUFF pops still fire | Re-checked in `flick_pops.md`: latch times 0.029 / 0.392 / 0.519 / 0.880 → grades Duff / Thin / Good / Pure; four screenshots `band_{duff,thin,good,pure}_792cone.png` present (2.6–4.1 MB each) | PASS |
| Putt: track top −491, bottom −1096 → **top corrected to 0-gap, bottom unchanged** | Top on ball at −303.84 (gap 0, per Cesar); bottom −1095.84 on baseline; height 792 = same as the cone drop | PASS (corrected target) |
| Putt camera unchanged | `git status --porcelain Assets/Scripts/Physics/` empty; `_puttCamDistanceM/HeightM` untouched | PASS |
| 16:9 Game View: base on baseline, ball raised, no overlap | `ShotLayoutMathTests.Flick_OnAShortScreen_…` asserted at H=2080 and H=1560; base on baseline, ball raised above authored anchor | PASS |
| Scheme switch at Idle Flick ↔ Pendulum: same framing, no camera pop | JSON `switch_*`: Flick −303.84, Pendulum −303.84, back to Flick −303.84; pitch 4.6115° in all three — zero-degree pop | PASS |
| Flick tiles recaptured (3), other 9 byte-identical | MD5-verified above: T_Flick_1/2/3 CHANGED; other 9 IDENTICAL; 12 metas unchanged; A/B `confirm_tiles_flick_before_after.png` shows shorter cones with apex-on-ball on the new tiles | PASS |
| EditMode count quoted; which Flick fixture changed | 2773 tests, last sweep 2770 passed / 0 failed / 3 pre-existing skips. `ShotLayoutMathTests` is the only test file diffed; two order-dependent flakes exist elsewhere in the suite (Pendulum driver marker-freeze, UI-motion CountUp), both green in isolation and green in the final clean sweep; not in this task's code path; NOT claimed as fixed | PASS |
| `ShotAimParityTests` / `ShotTimingPowerTests` / `ShotControllerFlickGateTests` untouched | git diff HEAD -- Tests/ lists ShotLayoutMathTests.cs alone; no literal-1009/1160/785/960 assertion in any test | PASS |
| Bots drive normalised power; `bot_difficulty.csv` untouched | `git status --porcelain` on `bot_difficulty.csv` empty | PASS |
| Device (Cesar) | Physical-device feel is Cesar's judgement post-approval; not blocking here per project's "no device pass by default" standing rule | Not applicable |

## Step 5 — Figma fidelity (Rule 18)

`SPEC.md` cites Figma node `14153:4602` (*In-Game – Shot Tests*). Every measured value below is
mine, re-read from my re-derivation (§ 3.b) or from the invariant JSON I already verified against
the config primary source.

| Element | Figma node | Figma value | Built value (my measure) | Result |
|---|---|---|---|---|
| Ball height on screen | 14153:4602 | 62 % from the top (viewport 0.38) | `CentralBall` y = −303.84 (I re-derived (vy−0.5)·H from `BallAnchorViewportY_Flick=0.38` in `controls.csv`) → viewport 0.3800 | PASS |
| Horizon | 14153:4602 | high — sky and fairway both above the ball | 34.24 % from top on this task's ruler; same ruler reads 34.28 % on `shot_view_layout`'s accepted `pendulum_038_hole2.png` (that task quoted 37.8 % under its own ruler). Same-ruler comparison to an accepted frame is sound; corroborated by camera-pitch identity (below) | PASS* |
| Aim-camera pitch | derived from Figma ball height | pitched-up framing | Before 12.5004° → after 4.6115°; camera position invariant `(122.05, 15.40, −134.25)` in both. Identical to Pendulum's 4.6115° to 4 decimals | PASS |
| Cone apex ↔ ball | 14153:4602 | cone reaches the ball | apex −303.84 ≡ ball −303.84 (gap 0.00 px). Canonical `flick_pull_792cone.png` visually confirms apex touches ball with no visible gap | PASS |
| Cone base ↔ baseline | 14153:4602 | on the shared bottom line | base −1095.84 vs baseline −1096.00 (0.16 px above, sub-pixel). Canonical shows base near screen bottom, above the four corner cards | PASS |
| Cone base vs action buttons | 14153:4602 | must not overlap | half-base@20° = 288.26 px vs button inner edge 382 px → 93.74 px clear. Canonical shows no cone-under-buttons overlap | PASS |
| Bottom control row | 14153:4602 | one bottom line | Four cards at bottom corners intact, unmoved by this task | PASS |
| Power gauge | 14153:4602 | top-right, clear of aim bar | Unchanged (viewport 0.70, from `shot_view_layout`) | PASS* |

`PASS*` = unchanged by this task and verified as "still where `shot_view_layout` left it".

## Horizon-ruler judgement (the question the prompt names)

**Is 34.24 % against a target of ≥ 35 % a moved goalpost, or a ruler calibration?**

The stronger evidence answers the question directly and does not depend on which ruler you use:
the aim-camera pitch after this task's change is 4.6115° with the camera position IDENTICAL to
Pendulum's `(122.05, 15.40, −134.25)`, and Pendulum's pitch after `shot_view_layout` is also
4.6115° (to 4 decimals). Same camera pose ⇒ same horizon on the same hole. The Pendulum framing
was accepted; the Flick framing is that same framing.

The horizon-percentage number is what a specific detector reads on a specific PNG; that reading
depends on the detector (canopy-edge vs sky-column choice, exposure, median row). The same
detector reading 34.24 % on this task's frame and 34.28 % on `shot_view_layout`'s ALREADY-ACCEPTED
Pendulum frame (which that task quoted at 37.8 % on its own detector) proves the two frames are
framing the same horizon and that this detector runs ~3.5 pts below the other. The substitution
is sound; the ≥ 35 % threshold in SPEC was written against a detector that was never bound to a
build.

The trap I'm watching for is a detector that's been silently retuned to hit the number. It hasn't
been: this task's detector reads BELOW the target and the report SAYS SO. It anchors the reading
against a prior accepted frame the reviewer chain has already looked at. Sound.

## Declared gaps — judgement

- **Putt frame not photographed, geometry measured.** Reaching putt mode via the real player path
  means playing a ball onto the green — the only shortcuts (`PhysicsLabController.EnterPutterMode`
  by reflection, the debug panel) are the synthetic entries the real-entry rule (Rule 2) forbids.
  Six geometry assertions in the JSON pin the track's top (−303.84), bottom (−1095.84) and
  height (792) live off the rect. The right call. Not a blocker.
- **"Before" frame is a camera-only A/B.** Only the ball widget goes back to 0.5; `BallSpace`
  (cone + club) cannot go back because the old 1160 px cone no longer exists in the scene. The
  camera pitch and horizon numbers in the A/B are honest; the 2D controls in that frame are not
  a full snapshot of the old layout. Declared plainly. Acceptable.
- **`ConeIdleAlpha` = 0.25 but cone not drawn at address.** The report proves the mesh renders
  in the pull frame with a pixel sample; the Idle gate lives outside this task's scope
  (`ConeAlphaController` / `ApplyDebugFlags` → `SetOutlineVisible`). Backlog per Open Questions
  §2. Not a blocker for this framing task; a height change cannot hide a cone.

## Previously-flagged doc errors (self-review) — confirmed fixed

- Files-modified row: `handle rest 960 → 540` — matches the on-disk scene diff. FIXED.
- Scene diff hygiene body: `14 insertions / 12 deletions` — matches `git diff --numstat`. FIXED.

## Report-integrity check (Rule 6)

Every PASS in the acceptance table is backed by a visible tool result (the invariant JSON, the
band re-check, the lint JSON, the tile MD5 audit, the scene diff, the config source). Nothing
looks fabricated. The report is explicit about the three spec numbers Cesar overrode mid-run and
the trade for pull parity (31.8 % power at touch on the shorter pull — asserted in the test at
`Assert.AreEqual(0.3182f, 1f - _cfg.FlickHandleStartY01, 1e-4f, …)`). PASS.

## Verdict

**PASS.** Every acceptance row I re-derived from primary sources reconciles. F13 two-mirror rule
holds across `controls.csv` + `ControlsConfig.Default` + `ControlsConfigLoader` for all six new
keys and the two parity anchors. Scene diff is exactly 14 insertions / 12 deletions with ZERO
`m_IsActive` flips. Standing bans clean. Tile audit clean. Pull-parity assertion binds to the
config, not to a literal. UIFidelityLinter fail=0. The horizon substitution is sound (identical
camera pose ⇒ identical horizon; the % delta is a detector property, calibrated against an
accepted prior frame). The three Cesar mid-run corrections (apex 0-gap, putt top 0-gap, handle
fraction 0.6818 for pull parity) are consistent with what shipped and explicitly consented-to in
the report's § Spec premise correction.

Setting `STATUS.md` to `READY_FOR_REDTEAM`. Hand off to `golfin-redteam-reviewer`.
