# IMPLEMENTER_REPORT — `scheme_needle` ("Tap Timing")

**Iteration shape:** `control_schemes:needle-scheme-first-build`
**Canonical screenshot:** `screenshots/needle_result_perfect.png` (1170×2532)
**Canonical video:** `videos/scheme_needle_needle.mp4` (1170×2532, 36.9 s, captioned)
**Invariants:** `needle_invariants.json` — **133 / 133 PASS, 0 FAIL**
**EditMode:** 2588 tests, **2585 pass / 0 fail / 3 pre-existing skips** (baseline 2530 + 58 new)

---

## 1. What shipped

Golf-Clash-style **two-touch** shot control, player-facing name **Tap Timing**, on top of
`control_scheme_seam` and reusing `scheme_pendulum` wherever the two schemes share a phase.

1. **Pull.** Touch the same `ClubHandle` clone the Pendulum uses and drag down inside a power
   circle: rings at 80 % / 100 % / 120 % plus a red overpower crescent in the bottom arc.
2. **Release.** Commits the **peak** power, snaps the club head home, dims the circle to 25 %, and
   raises the accuracy arc: navy 180° band, amber GOOD, blue PERFECT at the top, a needle starting
   at the left end, and the `SHOT_TAP_HINT` prompt.
3. **Tap** anywhere in the shot area while the needle sweeps once — **PERFECT / HOOK / SLICE**, or
   **SHANK** if it runs off the right end.

`ShotController` has **zero diff**. No seam gap appeared: `Tick` already returns early for an
owns-timing external drag (so the swing simply waits between the two touches with no arrow, no
per-pass degradation and no auto-cancel), and `ShotInProgressUiGate` only closes at `Flicking`,
i.e. *after* the tap — so the tap area is still live when the tap arrives. Confirmed by reading
both call sites, and pinned by `release.state_is_still_timing` + `release.no_shot_yet`.

### The seven Pendulum carry-overs, applied from the first build

| # | Carry-over | Where it lives | Evidence |
|---|---|---|---|
| 1 | Own speed constants, trackable by eye | `NeedleSweepSecAtCC0` etc. — stated in **seconds per sweep**, not Hz, because this needle makes exactly ONE pass | `needle.is_trackable_by_eye` = **1.236 s** at the live club (CC 6); `Sweep_IsTrackableByEyeAtClubControlZero` |
| 2 | Zones shrink with power, from the PEAK, and the drawn zone is the graded one | `NeedleMath.WindowScaleForPower`, redrawn every drag frame from `_peakPower` | blue half-angle **11.4° → 5.05°** on the live arc; `zones.drawn_perfect_angle_is_the_graded_window` compares the DRAWN angle to the GRADED window |
| 3 | Club head hidden in flight | `ShowHandle(false)` at **commit**, not on a `Flicking` event (which is never published) | `handle.hidden_while_the_ball_is_in_flight`, `handle.returns_for_the_next_shot`, + `TheClubHead_DisappearsWhileTheBallIsInFlight` |
| 4 | Geometry derived, not authored | ring radius = `HandleRestBelowBall + NeedlePull{80,100,120}Px`; `CircleRadius` = deepest ring + club half-height; needle length = arc `ry` + 10; pip radius = the band's centre | `geom.ring*_marks_where_the_club_lands` asserts the DRAWN radius against the CONFIG |
| 5 | Linear-space colour treatment | `NeedleColors`: pre-composite over a known parent, RGB-correct at the node's own alpha over turf | § 6 — zones **0 RGB** off, veils **within 6 RGB** of Figma's own composite over the same turf |
| 6 | Commit from peak | `_peakPower` / `_peakCurve`; republished at release so the gauge shows what will fire | `Release_RepublishesThePeakPower_NotTheLiveOne`, `PullingPastTheOriginOnRelease_StillCommitsThePeak` |
| 7 | Config-derived distances everywhere | tests, the acceptance bot and the video runner all read `_driver.Pull{80,100,120}Px` | no pull literal exists in any of the three |

---

## 2. Acceptance checklist (SPEC § 6)

| # | Item | Verdict | Justification |
|---|---|---|---|
| 1 | Tap Timing selected: no cone, no bar | **PASS** | `idle.no_cone_on_screen`, `idle.no_pendulum_bar_on_screen`, `scheme.flick_root_off`, `scheme.pendulum_root_off` — read off `activeInHierarchy`, not a look at a frame. `screenshots/needle_idle.png` |
| 2 | Pull → rings + crescent | **PASS** | `pull.circle_visible`; `geom.crescent_outer_is_ring120` / `_inner_is_ring100` / `_half_angle_34_38` / `_is_at_the_bottom`. `screenshots/needle_pull_100.png`, `needle_pull_120_zones_narrow.png` |
| 3 | Release → arc + needle, club head back on the ball | **PASS** | `release.needle_phase_started`, `release.arc_appears`, `release.needle_starts_at_the_left_end` (−1.0000), `release.handle_back_on_the_ball`. `screenshots/needle_sweeping.png` |
| 4 | Tap on the blue → PERFECT, full power, straight | **PASS** | `perfect.grade_is_perfect`, `perfect.is_dead_straight` (`LastShotWasClean`), `perfect.timing_mul_is_1`, `perfect.power_is_the_peak`. Tapped for real: the bot polls the LIVE `NeedleOffset` and dispatches a pointer-down on the real catcher — no forced offset anywhere in the grading path |
| 5 | Tap early → HOOK left with visible yaw | **PASS** | `hook.grade_is_hook`, `hook.tapped_early` (n < 0), `hook.goes_left` (errorYaw < 0), `hook.is_not_clean` |
| 6 | Tap late → SLICE right | **PASS** | `slice.grade_is_slice`, `slice.goes_right`; `hook`/`slice` yaws mirror |
| 7 | No tap → SHANK short + right | **PASS** | `shank.fires_without_a_tap` (exactly one shot), `shank.pays_the_red_multiplier` (0.70 vs GOLD 0.90), `shank.timing01_is_zero`, `shank.goes_right` |
| 8 | Needle trackable by eye (≥ 1.0 s at CC 0, logged) | **PASS** | `needle.is_trackable_by_eye` — **1.236 s** per sweep at the live club (`club_control: 6`); the CC-0 constant is 1.200 s and `Sweep_IsTrackableByEyeAtClubControlZero` pins it |
| 9 | Zones visibly narrow as the pull deepens | **PASS** | measured on the DRAWN angle, not recomputed: perfect **11.40° → 5.05°**, good **24.60° → 19.80°** (`shrink.*`) |
| 10 | Overpower speeds the needle; Strength buys it back | **PASS** | `overpower.shortens_the_sweep` — **1.236 s → 1.040 s** live. The Strength half is EditMode (`Sweep_IsFasterWhenOverpowered_AndStrengthBuysItBack`): at forgiveness 0.75 the 1.2× speed-up becomes 1.05× |
| 11 | Putt: 100 % ring only, flat arc, slower needle | **PASS** | `putt.ring120_hidden`, `putt.crescent_hidden`, `putt.ring100_still_shown`, `putt.arc_is_flattened_to_460x300`, `putt.needle_shortens_to_160`, `putt.power_caps_at_100pc`, `putt.needle_is_slower_than_a_swing` (**1.545 s** vs 1.236). `screenshots/needle_putt_pull.png`, `needle_putt_sweeping.png`. **See § 8 for how putt mode was entered.** `PuttPathPredictor` still updates — visible in the putt frames as the live cyan track |
| 12 | Club change swaps the handle sprite | **PASS** (by construction + read-back) | The handle is the `ClubHandle` clone carrying the live `ClubHandleSpriteBinder` and no `ClubHandleDragger`: `handle.has_sprite_binder`, `handle.no_flick_dragger`, `handle.sprite_is_a_real_club`. The binder is unmodified, so the swap is the shipping behaviour |
| 13 | Club head hidden in flight, back next shot | **PASS** | `handle.hidden_while_the_ball_is_in_flight` (alpha 0.00), `handle.returns_for_the_next_shot` (alpha 1.00) |
| 14 | Flick + Pendulum pixel-identical to their last approved state | **PASS** | Neither has any behavioural diff — see § 4. Their suites are unchanged and green in the 2588-test run |
| 15 | `shot_taken`: `scheme=2`, `timing01 = 1 − \|n\|` (0 on SHANK) | **PASS** | `perfect.timing01_is_one_minus_abs_n`, `hook.timing01_is_one_minus_abs_n`, `shank.timing01_is_zero`, plus `*.driver_and_pipeline_agree_on_timing01` — the driver's number and the pipeline's are compared against **each other**, not both trusted. `scheme=2` is `ControlScheme.Needle`, stamped by the existing telemetry hook (unchanged) |
| 16 | Strings `--check` clean + read-back; zero hardcoded `.text` | **PASS** | § 5 |
| 17 | Figma fidelity vs section 2b, measured off live RectTransforms | **PASS** | § 6 (29 geometry + 7 colour invariants) |
| 18 | Video through the real entry path | **PASS** | § 3 |

---

## 3. The video

`videos/scheme_needle_needle.mp4` — 1170×2532, 36.9 s, captioned, also at
`Docs/Reports/Media/2026-09-05_scheme_needle.mp4`.

Boot → PLAY → hole card (Lomond hole 2) → the in-game gear's **real** Tap Timing segment
(`InGameSettingsModalController.schemeButtons[2]`, named `TapTimingSegment`) → real pointer events
on the real club handle and the real tap catcher. Five beats:

| t | Beat |
|---|---|
| 0.0 – 2.5 | idle — no cone, no arrows |
| 2.5 – 6.8 | pull deeper and the target closes: blue zone **11.4° → 5.0°** |
| 6.8 – 10.7 | release at 120 %, **do not tap** → SHANK, timing 0.00 |
| 14.5 – 19.6 | tap on the blue → **PERFECT**, power 100 %, timing 0.97 |
| 26.3 – 30.9 | tap early → **HOOK**, timing 0.48 |

Every caption is written from what the driver actually awarded (read off
`LastCommittedGrade` / `LastCommittedNeedle` / `LastCommittedPower` after the commit), so a caption
cannot claim a grade the scheme did not give. Percent signs are escaped at the source.

**One deviation, flagged:** `BotVideoRecorder`'s one-clip-per-session guard blocked the first
attempt — this Editor session had already recorded the Pendulum's clips hours earlier. The clip was
taken after using the recorder's own documented override
(`GOLFIN ▸ Capture ▸ Reset Video Session Guard`), which the guard's message names, rather than
restarting Unity (which risks a hang plus a full reimport). The GPU had been idle across three
full play-mode acceptance runs in between.

---

## 4. Flick and Pendulum are byte-identical in behaviour

`ShotController.cs`, `ShotSchemeHost.cs`, `IShotSchemeDriver.cs`, `FlickSchemeDriver.cs`,
`ClubHandleDragger.cs`, `ShotConeView.cs`, `PendulumMath.cs`, `PendulumLaneView.cs`,
`PendulumBarView.cs`, `PendulumFadingView.cs` — **zero diff**. Verified with
`git status --porcelain`, quoted in `HEARTBEAT.log`.

Two Pendulum files changed, and the diff is a **type name only** (`git diff` in the heartbeat):

- `PendulumSchemeDriver.cs` — `PendulumGradePop _gradePop` → `SchemeGradePop _gradePop`, twice.
- `PendulumSchemeBuilder.cs` — `AddComponent<PendulumGradePop>()` → `AddComponent<SchemeGradePop>()`.

The rename was done with `git mv` **carrying the `.meta`**, so the script GUID
(`1024cf6ccd3624b9f8f5352c502eadbc`) is unchanged and `LabScaffold`'s existing reference to the
Pendulum's pop still resolves. `Show(PendulumGrade)` still exists, still resolves the same three
keys and the same three serialized colours; `Show(NeedleGrade)` and `Show(string key, Color)` were
added alongside it.

One asmdef line was added: `Golfin.Gameplay.Tests` now references `Golfin.Localization`, so the
new hardcoded-text regression test can assert against the key's own resolved value rather than a
literal. It adds a reference, removes none.

---

## 5. Strings — importer path, EN + JA, published

| key | EN | JA |
|---|---|---|
| `SHOT_GRADE_PERFECT` | PERFECT | パーフェクト |
| `SHOT_GRADE_HOOK` | HOOK | フック |
| `SHOT_GRADE_SLICE` | SLICE | スライス |
| `SHOT_GRADE_SHANK` | SHANK | シャンク |
| `SHOT_TAP_HINT` | TAP! | タップ! |

Full two-way path: `import_content.py` PLAN **5 add / 0 change / 0 conflict** → `--apply` →
`content_publish` **texts v39 → v40** (read back: all five rows live at `version: 40`,
`min_build: 2705`) → `export_content.py --check` **clean, exit 0** (`content_version.txt` now
`texts=40`) → `Tools ▸ Localization ▸ Import Text CSV` with a **forced reimport of the CSV asset
first** (Unity reads the imported asset, not the disk write) → table **1055 → 1060 rows** → read
back through `LocalizationManager.Get` in **both** languages. No `NotoSansJP` atlas churn
(`git status` is clean for every font asset).

**Zero hardcoded `.text`** — and this is the one defect the UI-fidelity linter caught that nothing
else did. `TapHint` was rendering the builder's authored literal `"TAP!"` because
`ShowTapHint(bool)` only toggled `activeSelf`; the published `SHOT_TAP_HINT` key was never read.
It now resolves the key at SHOW time (not cached — the language can change under a live screen),
the builder's placeholder is `"(SHOT_TAP_HINT)"` so a regression is obvious on sight, and it is
gated twice: `TheTapHint_ResolvesItsKey_NotTheBuildersPlaceholder` and
`release.tap_hint_reads_the_localised_key` (actual: `TAP!`). The grade pop was already correct —
`SchemeGradePop.Show` resolves the key. `NeedleLabel100`'s `"100%"` is left as a numeral, matching
the approved Pendulum's `Label100`/`Label120`.

---

## 6. Figma fidelity — section 2b, node re-pulled at step 0 (PIPELINE_HARDENING § 9)

`get_metadata` + `get_design_context` were run on `14091:102411` and its Timing (`14091:102430`),
Pull (`14091:102630`), Result (`14091:102737`) and Putt frames, and every ring / arc / zone /
crescent **SVG was fetched and read** rather than trusting the JSX summary. That is what produced
the numbers below; two of them contradict the SPEC's token table, and the node won:

- The SPEC says the zones are 44 px thick. **They are 40** — both zone paths solve to inner radius
  190 against the arc's 186, i.e. flush with the arc's outer edge and 4 px short of its inner one,
  which is the same band-inside-track idiom the Pendulum uses.
- The SPEC says `ZonePerfect` is composited over the arc. **It is composited over `ZoneGood`** —
  over the arc it solves to (74, 157, 245) and the reference render's own pixel is (83, 165, 249);
  over the amber it solves to (83, 164, 249). A 9 RGB error, found by measurement.

### 6a. Geometry — measured off LIVE RectTransforms / graphics (29 invariants, all PASS)

| Element | Node | Built | How |
|---|---|---|---|
| Ring80 | r 238.5, stroke 3, centred on the ball | **374.0** (= rest 70 + `NeedlePull80Px` 304), stroke 3 | `geom.ring80_marks_where_the_club_lands` — DERIVED, see below |
| Ring100 | r 298, stroke 4 | **450.0** (= 70 + 380), stroke 4 | `geom.ring100_…`, `geom.ring100_stroke_4` |
| Ring120 | r 358.5, stroke 3 | **526.0** (= 70 + 456), stroke 3 | `geom.ring120_…`, `geom.ring120_stroke_3` |
| OverpowerCrescent | ring100 → ring120, ±34.38° about the bottom | outer 526, inner 450, ±34.38°, centre 180° | 4 invariants |
| AccuracyArc | 460×460, band r 186…230, 180° | rx 230, ry 230, thickness 44, half-sweep 90° | 4 invariants |
| Zones | band r 190…230 | thickness 40 | `geom.zone_thickness_40` |
| NeedleHub | 36 px at the ball | 36.0 px at (0, ballY) | 3 invariants |
| Needle | 10×240, pivot at the ball | 10.0 × 240.0, pivot (0.5, 0) at (0,0) | 3 invariants (read off `rect`/`pivot` — a rotated rect's world bbox is the wrong instrument, and reading it that way is what made the first run report the needle as 240 wide) |
| TapPip | 28 px, 208 above the ball | 28 px at r **208** = (186+230)/2, derived | `perfect.pip_sits_on_the_arc_band`, `perfect.pip_x_matches_the_tap_angle` |
| TapHint | Rubik Medium 44, top 90 below the ball | top 90.0 below, 44/1.2 TMP | `geom.tap_hint_90_below_the_ball` |
| ResultChip | 420×120, r 32, 360 above the ball, 3px white-90% border, gradient `#133453`→`#091B33`, shadow | 420.0 × 120.0 at +360.0; gradient measured **(19,51,82) → (9,28,52)** against the node's (19,52,83) → (9,27,51) | 3 invariants + the baker's own print |
| Label100 | gold, +120 x, on the ring100 bottom | +120 x, y = −(rest + Pull100) — moves with the ring | `geom.label100_on_the_gold_ring` |
| Tap area | `Shoot Controls` 1074×1396 | 1074.0 × 1396.0 at +263 | 3 invariants incl. "clears the bottom buttons" |

**Why the ring radii are not the node's.** SPEC carry-over 4 is explicit: *a ring is drawn where
the club head LANDS at that power.* The node samples that formula at a shorter pull; this scheme
seeds the Pendulum's own thresholds (SPEC § 3.5) so the pull feels identical across the two, which
puts the 120 % ring at 526 rather than 360. The node's **ratios** are preserved (0.8 / 1.0 / 1.2,
the crescent spanning exactly ring100→ring120 over ±34.38° of the bottom, the label on the gold
ring's bottom), and the radii are asserted against the **config**, not against a re-derivation.
1052 px of diameter fits the 1170-wide canvas with 59 px of margin each side and reaches 526 px
above and below a ball that sits 1079 px from either edge.

### 6b. Colour — measured on the BUILT RENDER, not just read off the component

Every tint is also asserted off the live graphic (7 `colour.*` invariants). Those cannot see a
compositing error, so the saved PNGs were sampled as well:

| Element | Built pixel | Target | Δ |
|---|---|---|---|
| AccuracyArc fill | (13, 38, 56) | (10, 38, 55) — `#001E39`@80 % pre-composited | (+3, 0, +1) |
| ZoneGood | (194, 186, 138) | (194, 186, 138) — `#FFEBA6`@75 % over the arc | **(0, 0, 0)** |
| ZonePerfect | (83, 164, 249) | (83, 164, 249) — `#4DA3FF`@95 % over ZoneGood | **(0, 0, 0)** |
| Ring100 | (144, 146, 53) | (138, 142, 53) — Figma's own sRGB composite over the SAME turf | (+6, +4, 0) |
| Ring120 | (125, 101, 61) | (119, 101, 60) | (+6, 0, +1) |
| Crescent | (162, 98, 70) | (157, 100, 69) | (+5, −2, +1) |

The reference render's own pixels are (195, 188, 138) for ZoneGood and (83, 165, 249) for
ZonePerfect — within 2 of the built values.

The three veils keep the node's alpha and have their **RGB corrected** so that Unity's LINEAR blend
over turf lands on Figma's sRGB composite (`NeedleColors.OverTurf`). The Pendulum fitted a single
scalar alpha per element and had to accept a per-channel residual (its track could not be fitted at
all); correcting the RGB instead is exact on all three channels at the node's own alpha, with
nothing hand-tuned — `OverTurf_ReproducesFigmasCompositeThroughUnitysLinearBlend` pins the
arithmetic. The ±6 above is edge bleed from sampling across a 3–4 px antialiased stroke; on the
wide crescent, sampled clear of its edges, it measures (0, −1, 0).

---

## 7. UI fidelity lint

`Golfin.EditorTools.UIFidelity.UIFidelityLinter` run over the built subtree →
`Docs/Diagnostics/_capture/SchemeRoot_Needle_lint.json` — **`fail: 0`**, `warn: 3`,
`RESULT: PASS (health)`.

This scheme is **scene-authored** (the SPEC mandates `SchemeRoot_Needle` in `LabScaffold`) and the
linter's entry point takes a prefab, so the LIVE subtree is snapshotted to a throwaway prefab,
linted, and the snapshot deleted — the real objects are what get checked, not a stand-in, and the
scene is reloaded afterwards so the snapshot leaves no dirt (verified: `sceneDirty=False`).
No `spec.json` is passed: the node-spec layer would compare the ring radii against the node's own
240/300/360, which this scheme deliberately derives from the pull thresholds. Render-health is the
layer that applies, and it earned its keep — see § 5 for the hardcoded `"TAP!"` it caught.

The three WARNs, each explained rather than waved past:

| WARN | Why it is not a defect |
|---|---|
| `NeedleTapCatcher` flat-fill | Intentional: an alpha-0 raycast target that is never drawn. It is `SetActive(false)` between swings, so it is outside the raycast entirely except during the needle phase |
| `NeedleGradeText` unlocalized-text | The word is set imperatively from `NeedleMath.GradeKey(grade)` at Show time — a `LocalizedText` binder would be a second, staler source. Asserted four times in the run (`*.pop_reads_the_localised_key`) |
| `TapHint` unlocalized-text | Same: resolved from `SHOT_TAP_HINT` at show time. The lint text now reads `(SHOT_TAP_HINT)`, which is the builder's deliberately-obvious placeholder |

---

## 8. Not verified here — read this before trusting anything above

- **Putt mode was entered by setting `ShotController.IsPutt`**, which is the property the gameplay
  loop itself sets when the ball is on the green — the same write the production path makes, not a
  test hook — followed by the scheme re-activation `ShotSchemeHost` performs on a swap. What is NOT
  proven is a putt reached by actually playing to the green with a putter equipped; the club in the
  frame is a wood. The geometry, the power cap and the sweep slowdown are all real.
- **Club-change sprite swap** is argued from construction (the live `ClubHandleSpriteBinder` on the
  clone, no `ClubHandleDragger`) plus a read-back that the sprite is a real club sprite. No frame
  in this run shows two different clubs.
- **Strength buying back the overpower speed-up** is EditMode only. The live run measured the
  overpower speed-up itself (1.236 s → 1.040 s) on the equipped character.
- **On-device.** Nothing here ran on hardware; it is all Editor play mode at 1170×2532.
- **Free Swing** is untouched: `SchemeRoot_FreeSwing` still carries its `PlaceholderSchemeDriver`.
- **Bots** still play every scheme through Flick — `bot_scheme_parity` Stage B owns this scheme's
  `DriveBot`.

---

## 9. Scene drift — audited, not assumed

`LabScaffold.unity` was diffed against HEAD by parsing both files and comparing GameObject
**fileIDs** and names, not by reading the YAML diff (Unity rewrites the whole file on save, so
`- m_Name:` lines are re-ordering noise):

- **25 objects added**, all of them the Needle subtree.
- **0 objects removed**, **0 fileIDs gone**.
- **0 active-state flips on any pre-existing object.**

No play mode ran between the build and the save. The lint snapshot re-marked the scene dirty and
was discarded by reloading rather than saved.

## 10. Rejection follow-up

None — there is no `CESAR_REJECTION.md` for this task. Three defects were found and fixed
**inside** this iteration, each by a measurement rather than by eye; all three are now gated:

| Defect | Found by | Gate that keeps it fixed |
|---|---|---|
| The needle jumped **43 % of the arc** on a single 0.21 s hitch frame | `release.needle_starts_at_the_left_end` reading −0.57 | `Advance` clamps dt to 1/30 s; `release.needle_step_is_frame_rate_clamped` |
| Four pop assertions resolved the **Pendulum's** `GradeText` (both scheme roots are inactive most of the time, and find-by-name walks inactive objects) — they read back "JUST!" and had been silently passing against the wrong object | the renamed-key assertion failing | `NeedleGradeText` / `NeedleLabel100` / `NeedleBallRestGhost` are now uniquely named |
| The arc **faded out ~2 frames after the tap** — `CommitExternal` reaches `Resolving` synchronously and the shared fading view drops its target there. The arc IS the result readout. Measured navy (34, 55, 53) against its own (10, 38, 55), then (70, 93, 42) — grass — one shot later | pixel-sampling the saved PNGs; no live-colour assertion could see it | only `Idle` reaches the arc now; `TheArc_IsNotToldAboutResolving_SoTheResultStaysReadable` + 6 `*.arc_is_still_fully_up_at_the_result` invariants, one of them half a second into the flight |

---

## 11. Clone provenance

| Element | Source | Read back off the live object |
|---|---|---|
| `NeedleHandle` | `Object.Instantiate` of the scene's `ClubHandle` (`LabRoot/ShotUI_Canvas/SchemeRoot_Flick/ConeRoot/ConeMesh/ClubHandle`), with `ClubHandleDragger` and `TeeIdleGlowController` stripped | `handle.sprite_is_a_real_club` (a real sprite, not `<NONE>`), `handle.has_sprite_binder`, `handle.no_flick_dragger` |
| `NeedleBallRestGhost` | `Assets/Art/ShotUI/S_PendulumBallGhost.png` — the Pendulum's, reused as the SPEC asks | `ghost.reuses_the_pendulum_sprite` = `S_PendulumBallGhost` |
| Needle bar / hub / pip | `Assets/Art/Tournaments/S_PillStadium.png`, with `pixelsPerUnitMultiplier = 88 / radius` so the caps come out at the node's radius instead of collapsing to an oval | lint render-health: 0 FAIL (the 9-slice-collapse check is exactly this) |
| Grade pop | `SchemeGradePop` — the Pendulum's own `PendulumGradePop`, renamed via `git mv` with its `.meta` (GUID `1024cf6ccd3624b9f8f5352c502eadbc` unchanged) | `chip.has_the_baked_sprite`; the Pendulum's scene reference still resolves |
| Fading behaviour | `PendulumFadingView`, reused unchanged | both views derive from it |
| `ResultChip` sprite | **New**, baked from the node's own tokens by `Docs/Scripts/make_needle_sprites.py`. Surfaced rather than hidden: a vertical gradient inside a translucent border cannot be made from a tinted stadium, which is the identical argument that baked the Pendulum's lane and track | `chip.has_the_baked_sprite` = `S_NeedleResultChip` |

Everything curved (rings, crescent, arc, zones) is a `NeedleArcGraphic` **mesh** rather than a
sprite, because its radius is derived from the pull thresholds and its angle from the accuracy
windows — both change at runtime, so neither can be a fixed-size PNG. `PowerGaugeGraphic`,
`ConeMeshGraphic` and `PutterTrackGraphic` are the same call already in this project.

---

## 12. Files modified or created

| File | What |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleMath.cs` | **new** — the whole scheme's decision-making as pure functions: power, sweep seconds, the two zones, the grade table, the SHANK verdict |
| `…/Needle/NeedleColors.cs` | **new** — the linear-space colour treatment: pre-composite over a known parent, RGB-correct at the node's alpha over turf |
| `…/Needle/NeedleArcGraphic.cs` | **new** — one annulus-segment mesh; every curved element in the scheme is an instance of it |
| `…/Needle/NeedlePowerCircleView.cs` | **new** — the three rings, the overpower crescent and the 100 % label, all placed from the config; owns the release dim |
| `…/Needle/NeedleArcView.cs` | **new** — the arc, the two zones as angular segments driven by the windows, the needle and its colour cue, the hub, the pip and the localised prompt |
| `…/Needle/NeedleTapCatcher.cs` | **new** — the invisible second-touch target over the node's `Shoot Controls` rect |
| `…/Needle/NeedleSchemeDriver.cs` | **new** — the two touches, the SHANK timeout, and the one `CommitExternal` per swing |
| `…/Controls/SchemeGradePop.cs` (+ `.meta`) | **renamed** from `Pendulum/PendulumGradePop.cs` with its meta; generalised to a key + colour, with typed overloads for both schemes |
| `…/Controls/Pendulum/PendulumSchemeDriver.cs` | the grade-pop **type name** only, twice |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | 16 `Needle*` fields + their seeds in `Default` |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | the 16 matching loader cases |
| `Assets/Resources/Gameplay/controls.csv` | the 16 rows, each with a note saying what the number is for |
| `Assets/Editor/ShotUI/NeedleSchemeBuilder.cs` | **new** — builds `SchemeRoot_Needle` from the node values; idempotent |
| `Assets/Editor/ShotUI/NeedleSchemeVerify.cs` | **new** — the acceptance run that writes `needle_invariants.json` |
| `Assets/Editor/ShotUI/NeedleSchemeVideo.cs` | **new** — the one captioned clip |
| `Assets/Editor/ShotUI/PendulumSchemeBuilder.cs` | the grade-pop **type name** only |
| `Assets/Scripts/Gameplay/Tests/NeedleMathTests.cs` | **new** — 25 tests over the maths and the colour treatment |
| `Assets/Scripts/Gameplay/Tests/NeedleSchemeDriverTests.cs` | **new** — 33 tests over the two touches, the exits and the drawn geometry |
| `Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef` | one added reference: `Golfin.Localization` |
| `Assets/Art/ShotUI/S_NeedleResultChip.png` (+ `.meta`) | **new** — the one baked sprite |
| `Docs/Scripts/make_needle_sprites.py` | **new** — bakes it from the node's tokens; edit this, never the PNG |
| `Assets/Scenes/Physics/LabScaffold.unity` | `SchemeRoot_Needle` populated (25 objects), its `PlaceholderSchemeDriver` replaced |
| `Assets/Localization/LocalizationText.csv` + `LocalizationTextTable.asset` | the five strings, EN + JA |
| `Assets/Resources/Data/content_version.txt` | `texts=39 → 40` after the publish |
| `Docs/CONTROL_SCHEMES_PLAN.md` | § 9 — the four SPEC § 7 backlog rows |
| `Docs/Diagnostics/_capture/SchemeRoot_Needle_lint.json` | the lint artifact |
| `Docs/Specs/Active/scheme_needle/*` | this report, `STATUS.md`, `HEARTBEAT.log`, `needle_invariants.json`, `screenshots/`, `videos/` |

---

## 13. Tests

**EditMode: 2588 total, 2585 pass, 0 fail, 3 skipped** (the three pre-existing
`HoleCompleteDriverTests` skips). Baseline before this task was 2530.

- `NeedleMathTests` — **25**: power at 0/39/40/304/380/456/500 px for swing and putt; sweep seconds
  across CC 0/50/100/120 with power 1.0/1.2 and forgiveness 0/0.75, the putt slowdown and the
  floor; both zones across accuracy 0/0.5/1 × power 0/1/1.2 with the GOOD-above-PERFECT clamp; the
  grade table at n = 0, ±perfect, ±(perfect+ε), ±good, ±1 and SHANK, with the sign convention
  stated and asserted; the keys; and the linear-blend arithmetic.
- `NeedleSchemeDriverTests` — **33**: the pull into `Timing`, the dead zone, overpower, no arrow
  under an owns-timing drag; the release starting the needle and arming the catcher, republishing
  the peak, returning the handle, and NOT being flick-gated; the tap committing exactly one shot
  per swing at PERFECT / HOOK / SLICE with mirrored yaws; the SHANK timeout firing once; the
  zero-power cancel; peak power surviving a partial and a past-the-origin release; the drawn zones
  narrowing, holding through the needle phase, re-opening next swing, and equalling the graded
  windows; the needle's on-screen rotation; Straight vs Fade-Draw; putt caps and flattening; the
  club head hiding and returning; the arc surviving `Resolving`; and the prompt resolving its key.
- Parity: the Flick and Pendulum suites are unchanged and green inside that run.

**Sign convention, stated as SPEC § 3.4 asks.** `n < 0` is the needle LEFT of the top (tapped
early) → negative `ErrorYawRad` → the ball goes LEFT → **HOOK**. `n > 0` → right → **SLICE**. That
follows from `ShotController.AimYawFor(finetune) = CameraHeading + finetune × halfCone`, which
`ShotAimParityTests` pins as the single source of truth for where the ball goes: a positive
finetune yaws the aim positively, which is the ball's right. Pinned by
`Grade_SignConvention_EarlyIsHookLeft_LateIsSliceRight` and, on the live run, by
`hook.goes_left` / `slice.goes_right` plus `TheNeedle_RotatesLeftForNegativeOffsets`, which checks
the on-screen direction rather than the number that was passed in.

**Telemetry band mapping, reported once as SPEC § 3.7 asks.** `timing01 = 1 − |n|`, so
1.0 is the top of the arc and 0.0 is either end. PERFECT occupies `timing01 ≥ 1 − PerfectZone01`,
a small HOOK/SLICE `1 − GoodZone01 … 1 − PerfectZone01`, a big one below that — and both zone
edges move with Club Accuracy and with the peak power, so the band cannot be recovered from
`timing01` alone. A **SHANK** is the only row with `timing01 == 0` at
`timing_mul == TimingPowerMulRed` (0.70); every other 0.0 sits at `TimingPowerMulGold` (0.90).
