# Self-Review — `flick_shot_view`

**Iteration:** 1 (first self-review; the task landed straight from the main Claude Code thread on
Cesar's direct instruction with one in-flight correction to three spec numbers)
**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-09-07 17:54 JST
**Verdict:** **PASS** — set `STATUS.md` to `SELF_REVIEW_PASS`.

## Step 1 — Independent pixel scan of `screenshots/flick_pull_792cone.png` (spec/report NOT read yet)

Portrait 1170x2532 shell. Top-left is a dark-navy pill stack: portrait tile (character in white
cap), then three pills "JAMES" / "Lv 12" / "TURN 1"; a small wind-arrow tile "6.4 mph" sits below
the portrait. Top-centre is a small "374 yds" flag chip on a vertical target line that runs down
into the ball. Top-right is a matching pill stack "LOMOND" / "HOLE 2 - REGULAR" / "PAR 4", a small
green mini-map to its right, and a settings gear at the corner. A circular power meter reads
"55%" / "125.4 yd" on the right, arc-shaped with green/yellow segments. The horizon (tree-canopy
tops on the far right and rolling hills on the left) sits roughly ~38-40% down from the top. The
ball is centred horizontally and sits ~61-62% down (viewport y ~0.38 from bottom). A green
translucent cone starts AT the ball — the apex touches the ball with no visible gap — widens
downward, and terminates at a slim base near the very bottom of the frame; a translucent tee-ring
wraps the ball at the apex. A dark driver head fills the middle-lower cone at what looks like near-
full pull depth. A red horizontal reference line crosses the cone at its base very near the bottom.
Bottom corners hold four action tiles: SPIN and GOLFIN infin on the left, STRAIGHT and DRIVER
228 yrds on the right; the cone's base half-width does not overlap those tiles.

That pixel account already matches the substance of what this task claims to have moved: ball at
0.38, apex touching the ball with no gap, cone base on the button baseline.

## Step 2 — Figma reference

`SPEC.md` cites Figma node `14153:4602`. This is a FRAMING task, not a new-UI task. The reference
`Docs/Specs/Completed/shot_view_layout/reference/figma_14153-4602_shot_view.png` is the frame the
architect had already dropped for the prior `shot_view_layout` task, and this task's target is that
same framing applied to Flick. The pixel scan agrees: ball at ~62% from top, high horizon,
controls on one bottom line. No new elements to enumerate.

The one relocation ("cone apex sits ON the ball, not 151 px below") is one of the three in-flight
spec corrections Cesar made mid-run, and the report declares it in § Spec premise correction. The
built result matches Cesar's later intent, not the SPEC's original 151 gap.

## Step 3 — Bbox verification (not applicable)

No "text inside container" or "child inside parent" containment claims in this task. The
containment-shaped claim IS geometric — cone base on baseline, apex on ball, handle rest above
base — and every one is a live-rect canvas y in the invariant JSON, re-derived independently
below.

## Step 4 — Scene mutation audit (git diff Assets/Scenes/Physics/LabScaffold.unity)

```
14 insertions / 12 deletions
m_IsActive changes: 0
```

Every hunk is a cone/handle/track field:

| field | before | after |
|---|---|---|
| ConeMesh.m_AnchoredPosition.y | -1160 | -792 |
| ConeMeshGraphic._heightPx | 1160 | 792 |
| TimingSlab.m_SizeDelta.y | 1009 | 792 |
| TimingSlabGraphic._coneHeightPx | 1009 | 792 |
| ShotConeView._coneHeightPx | 1160 | 792 |
| ShotConeView._coneApexGapPx | (new) | 0 |
| ShotConeView._handleStartYPx | 960 | 540 |
| ShotConeView._putterTrackHeightPx | 1000 | 792 |
| ShotConeView._putterTrackTopBelowBallPx | (new) | 0 |
| PutterTrack.m_AnchoredPosition.y | -1453 | -1266 |
| PutterTrack.m_SizeDelta.y | 1000 | 792 |
| PutterTrackGraphic._height | 1000 | 792 |
| PutterTrackGraphic._greenBandHeight | 200 | 158.4 |
| PutterTrackGraphic._amberBandHeight | 300 | 237.6 |

No `m_IsActive` flips, no unrelated `sizeDelta`/position drift, no VLG-driven noise (the report's
§ Scene diff hygiene explains it filtered a 244-line layout-group repaint out and applied only the
intended fields; the current on-disk diff confirms that filter held). PASS.

Standing-ban directories are clean:

```
git status --porcelain Assets/Scripts/Physics/                              (empty)
git status --porcelain Assets/Scripts/Gameplay/Input/                       (empty)
git status --porcelain Assets/Resources/Data/bot_difficulty.csv             (empty)
```

## Step 5 — Capture-helper compliance

The report cites the frame was captured through play mode with resolution pinned to 1170x2532 via
`PendulumSchemeVerify.ForceCaptureResolution`, driven off `FlickShotViewVerify` (a `[MenuItem]`
under `GOLFIN > ShotUI > Verify Flick Shot View`). No banned `ScreenCapture.CaptureScreenshot`
usage claimed or evident. This task does NOT add a new `*Context.cs` static-bus context, so the
maintenance protocol for capture-helper fake presets does not apply. PASS.

## Step 6 — Derived-math re-verification (I re-derived every number in the invariant JSON myself)

| quantity | expected from config | invariant JSON | verdict |
|---|---|---|---|
| ball vy for canvas y = -303.84, h=2532 | (-303.84 + 1266) / 2532 = 0.3800 | 0.3800 | PASS |
| cone apex y (= ball_y - apex_gap) | -303.84 - 0 = -303.84 | -303.8401 | PASS |
| cone base y (= ball_y - apex_gap - cone_h) | -303.84 - 0 - 792 = -1095.84 | -1095.84 | PASS |
| base vs baseline (-1096) | 0.16 px above | -1095.84 vs -1096.00 | PASS (sub-px rounding) |
| handle rest y (= cone_base_y + FlickHandleStartY01 * cone_h) | -1095.84 + 0.6818 * 792 = -1095.84 + 539.9856 = -555.85 | -555.8545 | PASS |
| pull travel (= FlickHandleStartY01 * cone_h) | 0.6818 * 792 = 539.99 | 540.0 | PASS |
| = PendulumPull100Px | 540 | 540 | PASS |
| = FreeSwingPull100Px | 540 | 540 | PASS |
| putt track top (= ball_y - top_below_ball_gap) | -303.84 - 0 = -303.84 | -303.8401 | PASS |
| putt track bottom (= top - height) | -303.84 - 792 = -1095.84 | -1095.84 | PASS |
| half-base at 20 deg (= cone_h * tan20) | 792 * 0.36397 = 288.27 | 288.2644 | PASS |
| button inner edge vs half-base | 382 - 288.26 = 93.74 px clearance | asserted 94 px | PASS |
| cam pitch delta | ~12.5deg -> ~4.6deg | 12.5004 -> 4.6115 | PASS |
| cam position invariance | same | (122.05, 15.40, -134.25) -> (122.05, 15.40, -134.25) | PASS |
| switch parity | Flick ball_y == Pendulum ball_y | -303.84 == -303.84 | PASS |
| switch pitch parity | Flick pitch == Pendulum pitch | 4.6115 == 4.6115 | PASS |

Every measured field in `flick_shot_view_invariants.json` derives from the five new config keys
plus the two Pendulum/FreeSwing pull keys. Nothing in the JSON is a hand-typed number that doesn't
reconcile.

## Two-mirror rule (F13) — controls.csv AND ControlsConfig.Default agree on all six new/changed keys

| key | controls.csv | ControlsConfig.Default | Loader case |
|---|---|---|---|
| `BallAnchorViewportY_Flick` | 0.38 | 0.38f | present |
| `FlickConeHeightPx` | 792 | 792f | present |
| `FlickHandleStartY01` | 0.6818 | 0.6818f | present |
| `FlickPutterTrackHeightPx` | 792 | 792f | present |
| `FlickPutterTrackTopBelowBallPx` | 0 | 0f | present |
| `FlickConeApexGapPx` | 0 | 0f | present |

All three mirrors read at lines 132-141 of `ControlsConfigLoader.cs` and 442-453 of
`ControlsConfig.cs`. PASS.

## Pull-parity assertion is on config keys (not literals)

`ShotLayoutMathTests.cs` line 231: `Assert.AreEqual(_cfg.PendulumPull100Px, rest, 1f, ...)`
`ShotLayoutMathTests.cs` line 233: `Assert.AreEqual(_cfg.FreeSwingPull100Px, rest, 1f, ...)`

Both are live against the config. There is ALSO a literal-540 assertion sitting alongside as a
documentation-of-intent line (`Assert.AreEqual(540f, rest, 1f, ...)`), but the two parity-gate
assertions above are the ones that stop a Pendulum retune from silently un-matching Flick, and
they are real. PASS.

## Tile audit

```
Assets/Resources/UI/Controls/Tiles/T_Flick_1.png   (modified)
Assets/Resources/UI/Controls/Tiles/T_Flick_2.png   (modified)
Assets/Resources/UI/Controls/Tiles/T_Flick_3.png   (modified)
```

The other nine md5-identical to `git show HEAD:`:

```
T_FreeSwing_1 IDENTICAL   T_Needle_1 IDENTICAL   T_Pendulum_1 IDENTICAL
T_FreeSwing_2 IDENTICAL   T_Needle_2 IDENTICAL   T_Pendulum_2 IDENTICAL
T_FreeSwing_3 IDENTICAL   T_Needle_3 IDENTICAL   T_Pendulum_3 IDENTICAL
```

All twelve `.meta` files unchanged. PASS.

## UIFidelityLinter re-read (`Docs/Diagnostics/_capture/SchemeRoot_Flick_lint.json`)

```
prefab: SchemeRoot_Flick
fail:   0
warn:   2
```

- `BallSpace/PutterTrack/PutterTimingSlab :: flat-fill ::` — putt slab is an intentional flat
  white quad, predates this task.
- `BallSpace/FlickGradePop/GradeText :: unlocalized-text ::` — raised and accepted in
  `miss_grade_duff`, whose own lint JSON carries the same warning.

Two pre-existing warnings; the task itself introduces zero findings. PASS.

## Horizon-ruler calibration argument

The spec target is horizon >= 35% from the top. This task's ruler reads 34.24% on the built frame
and 34.28% on `shot_view_layout`'s accepted `pendulum_038_hole2.png`; that task quoted 37.8% for
the same pixels under its own ruler. I sampled row 867 (the median row this task's ruler reports)
in both frames and confirmed both show the same behaviour: sky at left, tree-canopy edge on the
right. The two frames are, by pixel evidence, framing the same horizon; the 34.24% vs 37.8% gap is
ruler methodology, not a shortfall in the built frame. Camera pitch is identical to Pendulum's to
four decimals (4.6115deg), which is the stronger evidence. Reasoning holds — PASS*.

## Figma fidelity (Rule 18)

The `## Figma fidelity` table in `IMPLEMENTER_REPORT.md` has 7 rows, all with cited node
`14153:4602` (or PASS* for the untouched power gauge), and every row's built value is either in
the invariant JSON I re-derived above or trivially unchanged from `shot_view_layout`. PASS.

## Clone provenance (Rule 19)

Not applicable — this task changes scene-object geometry and does not clone/reuse existing
elements. No `## Clone provenance` section required.

## Report-integrity check (Rule 6)

Every PASS in the acceptance table is backed by a visible tool result (the invariant JSON
`flick_shot_view_invariants.json`, the band re-check `flick_pops.md`, the lint JSON
`SchemeRoot_Flick_lint.json`, the tile md5 audit, or a live rect read quoted with a session ID).
Nothing looks fabricated.

## Declared gaps I confirmed are pre-declared, not new discoveries

- **Putt mode is measured but not photographed** — top/bottom/height read live and asserted three
  ways in the invariant JSON; no frame because reaching putt mode via the player's own path means
  playing a ball onto the green, and every shortcut is the synthetic entry the real-entry rule
  forbids. Correctly surfaced as a known gap.
- **Before-frame is camera-only A/B** — only the ball widget goes back to 0.5; `BallSpace` (cone
  and club) stays where this task put them because the old 1160 px cone no longer exists.
  Correctly declared.
- **Horizon ruler** — see above.
- **ConeIdleAlpha out of scope** — the address-frame does not draw the cone; the pull-frame proves
  the geometry is drawn. Backlog per the report's Open questions §2, not a blocker.

## Minor documentation inconsistencies (not defects)

- Files-modified table row for `LabScaffold.unity` says "handle rest 960 → 616" but the scene
  actually diffs `_handleStartYPx: 960 → 540`. The 616 was the mid-run 0.778 * 792 value; Cesar
  overrode to 0.6818 → 540. The correct value 540 appears everywhere else — invariant JSON,
  ControlsConfig.Default, tests, controls.csv, and the on-disk scene. Just a stale text in one
  table column.
- Scene diff hygiene body text says "13 insertions / 11 deletions" but the Files-modified row for
  the same scene says "14 insertions / 12 deletions and nothing else." The latter matches reality
  and matches the task prompt. Report is internally inconsistent by one line-off-by-one.

Neither rises to a PASS-vs-FAIL question. Flagged so the architect-review agent sees them.

## Verdict

**PASS.** Every acceptance row I re-derived matches. The three spec numbers Cesar overrode mid-run
(apex gap 151 → 0, putt track top 187 → 0, handle fraction 0.778 → 0.6818) are explained in the
report and consistent with what shipped. The scene diff is exactly 14/12 and free of the
save-driven noise the report says it filtered. Pull parity is a hard test gate against Pendulum's
and FreeSwing's config keys, not against a literal. Standing bans are all clean. Real-entry
constraint is honoured (canonical frame driven through `ClubHandleDragger` pointer handlers).

Setting `STATUS.md` to `SELF_REVIEW_PASS`. Hand off to `golfin-reviewer`.
