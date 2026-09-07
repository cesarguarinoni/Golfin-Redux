# Implementer Report — `flick_pull_mapping`

**Iteration shape:** `flick_pull:base_relative_power`
**iter-2 (2026-09-07):** Cesar, on the iter-1 frames — *"Remove the 100% and 120% lines, leave only
the labels."* Done: the two drawn rules are gone from the scene (the builder now DELETES them, so a
scene built at iter-1 is cleaned by re-running it rather than left with orphans), the two labels
stay at exactly the heights they had, and the acceptance run asserts the lines are gone from the
live hierarchy rather than merely deactivated. Everything about the pull mapping is unchanged.

## Implementation summary

Flick's power stopped being measured from the cone's **base** and is now measured from the club's
**rest**, which is what the other three schemes have always done. `ClubHandleDragger.ProcessDrag`
computes `pullPx = restY − handleY` and hands it to a new `FlickPullMath.Power` (0 below the shared
40px dead zone, 1.0 at `FlickPull100Px` 540, 1.2 at `FlickPull120Px` 648) — and 648 is the cone's
base, because `FlickHandleStartY01` moved 0.6818 → 0.8182 so that `StartY01 × ConeHeightPx` **is**
`FlickPull120Px`. Touching the club now reads **0%** where it read 31.8%, and the base reads
**120%** where the finger used to run out of cone at 100%.

`ShotConeView` draws the club through `FlickPullMath.ConeLocalYForPower` — the exact inverse — so
the drawn club and the finger coincide, measured at 0.00px at every invertible depth. Two **labels**
— "100%" 108px above the base, "120%" **on** the base, both heights derived from the two config
keys, both parked 16px outside the cone's own edge at that height, the 120% one hidden on a putt,
both fading with the cone's CanvasGroup — are built and wired by a new `FlickConeLabelsBuilder`,
which also wires `ClubHandleDragger._coneView` — no `Find()`. iter-1 drew tick **lines** under them;
Cesar removed them, and the builder now deletes any it finds.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/Input/FlickPullMath.cs` | **created** — the pull→power mapping, its exact inverse, the drawn-club y and the two derived mark heights. Beside `FlickMath`, for the same asmdef reason. |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | modified — `FlickPull100Px` 540 / `FlickPull120Px` 648 added; `FlickHandleStartY01` 0.6818 → 0.8182. |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | modified — two new `case` rows. |
| `Assets/Resources/Gameplay/controls.csv` | modified — the two new keys with rationale; `FlickHandleStartY01` row rewritten. |
| `Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs` | modified — rest-relative pull; new `[SerializeField] _coneView`; `HandleRestYPx` falls back to the same two config keys when unwired. |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` | modified — handle placed by the exact inverse (no longer `Clamp01`, so 120% draws); two label fields + `_labelGapPx`; `ApplyMarkGeometry` / `PlaceLabel` / `RefreshLabelOffsets` / `ConeWidthAt` / `OffsetLabel`; re-applied on putt flip. |
| `Assets/Editor/ShotUI/FlickConeLabelsBuilder.cs` | **created** — builds + wires the two labels and `ClubHandleDragger._coneView`, and **deletes** any `Tick100`/`Tick120` left by iter-1. Idempotent. Menu: `GOLFIN ▸ Build ▸ Flick Cone % Labels + Dragger Wiring (LabScaffold)`. |
| `Assets/Scenes/Physics/LabScaffold.unity` | modified — 2 new GameObjects under `ConeMesh` (`Label100`, `Label120`), the four wired references, and `_handleStartYPx` 540 → 648.0144 (the edit-mode fallback re-derived by the builder). **281 insertions, 1 deletion, zero `m_IsActive: 0`.** |
| `Assets/Scripts/Gameplay/Tests/FlickPullMathTests.cs` | **created** — the SPEC §3.4 table, the round-trip, the putt cap, the drawn-club identities, the three config identities and a retune guard (23 cases). |
| `Assets/Scripts/Gameplay/Tests/ShotConeViewConfigTests.cs` | modified — the two assertions that pinned the OLD rest (540 / 0.6818) restated on the new one. |
| `Assets/Scripts/Gameplay/Tests/ShotLayoutMathTests.cs` | modified — `Flick_HandleRestIsAFractionOfTheCone…` restated: the rest is now the 120% pull, parity is asserted on `FlickPull100Px`, and the touch reading is asserted as **0**. |
| `Assets/Scripts/UI/Editor/FlickPullReflect.cs` | **created** — one reflection window onto `FlickPullMath` for the editor bots (`Golfin.Gameplay.Input` is `autoReferenced:false`). Never a second copy of the arithmetic. |
| `Assets/Scripts/UI/Editor/MissDuffFlickVerify.cs` | modified — target y through the shipped inverse; the press now starts ON the club. Without this it would have aimed 108px short of every power it asked for. |
| `Assets/Scripts/UI/Editor/ShotTimingTelemetryVerify.cs` | modified — same two lines, same reason. |
| `Assets/Scripts/UI/Editor/ShotAimParityDemoRecorder.cs` | modified — `ConeLocalFor` through the shipped inverse. |
| `Assets/Scripts/UI/Editor/FlickShotViewVerify.cs` | modified — the two assertions that pinned the old rest (`handle_rest_556`, travel-vs-Pendulum) restated, so the prior task's instrument does not report FAIL on shipped, accepted geometry. |
| `Assets/Scripts/UI/Editor/FlickPullMappingVerify.cs` | **created** — this task's acceptance run. Menu: `GOLFIN ▸ ShotUI ▸ Verify Flick Pull Mapping`. |
| `Assets/Resources/UI/Controls/Tiles/T_Flick_{1,2,3}.png` | modified — re-captured (§3.5). The other nine restored to their committed bytes; all twelve `.meta` untouched. |
| `Docs/Specs/Completed/scheme_confirm_popup/tiles_manifest.json` | modified — only the three Flick rows replaced, plus `flick_regenerated_utc` / `flick_regenerated_by` saying so. The nine other rows still describe exactly the nine committed PNGs. |

## Screenshot

- **Canonical screenshot:** `screenshots/flick_pull_648_120pct.png` — 1170×2532. The club's bottom
  edge sitting exactly at the cone's base beside the `120%` label, with the gauge reading `120%`.
  This is the frame that shows the two things the task exists for at once: there **is** a 120%, and
  it is the base.
- **Also captured:** `screenshots/flick_pull_000_rest.png` (gauge `0%` at touch),
  `screenshots/flick_pull_540_100pct.png` (club at the `100%` label's height, gauge `100%`),
  `screenshots/flick_pull_putt_base.png` (putt: `100%` at the base, no `120%` label),
  `screenshots/duff_rerun_flick_{pure,good,thin,duff}.png`.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` → real navigation → Lomond hole 2.
- **Play mode:** Yes — booted through StartButton → PLAY → hole card, then driven through the REAL
  `ClubHandleDragger` `IPointerDown` / `IDrag` / `IPointerUp` handlers. No render harness, no
  synthetic button.
- **Hole loaded:** 2 (Lomond), 1170×2532.

## Acceptance checklist (SPEC §4)

| Item | Result | Justification |
|---|---|---|
| Touch at rest → gauge **0%**; 40px → still 0%; 540px → **100%**; 648px → **120%**, red overpower arc; past the base clamps at 120% | **PASS** | Gauge TEXT read live at each depth: `0%` / `0%` / `50%` @290 / `100%` / `120%` / `120%` @700; `PowerNormalized` 0.0000 / 0.0000 / 0.5000 / 1.0000 / 1.2000 / 1.2000. On pointer-DOWN before any drag the gauge already read `0%` (it read 0.318 under the base-relative mapping). |
| Putt: base reads **100%**, 120% mark hidden | **PASS** | `putt_power_at_base` 1.0000, gauge text `100%`; `Label120.activeSelf=False`, `Label100.activeSelf=True`. See `screenshots/flick_pull_putt_base.png`. |
| Drawn club and finger coincide at 0 / 100 / 120% (screenshot each, marks visible) | **PASS** | club−finger = **0.00px** at 0, 290, 540 and 648px; 0.01px at 700px (the clamp). Three frames captured, both labels legible in each. Deliberate exception: through the 40px dead zone the club stays at rest (40px behind the finger) — the mapping's flat segment, documented in `FlickPullMath.PullPxForPower` and asserted by `AtZeroPower_TheClubIsDrawnAtTheRest_NotAtTheDeadZoneEdge`. |
| Rest at canvas y = base + 648 = **−447.84**; ball −303.84 so the club sits 144px under the ball; 100% mark −987.84; 120% mark −1095.84 | **PASS** | Measured: ball **−303.84**, cone base **−1095.84**, rest **−447.83** (spec −447.84), ball−rest **143.99**, `100%` label centre **−987.84**, `120%` label centre **−1095.84** (= the base to 0.00px). Gap 108.00px = `Pull120 − Pull100`. Label x: 109.09 and 123.77, i.e. 16px outside the cone's edge at each height. |
| Lateral aim at 100% still spans the cone's full width (D5) — `finetune` ±1 reachable | **PASS** | At the 100% depth `maxX` = 93.09px; driving past it reached `HandleFinetune` **+1.000** and **−1.000**. D5's `maxX = halfBase × (1 − handleY/ConeHeightPx)` is untouched. |
| `miss_grade_duff` live band check (PURE / GOOD / THIN / DUFF) re-run | **PASS** | All four re-driven through the real dragger: DUFF `mul 0.400 isMiss=True` gauge flash `#FF3B3B pct='22%'`; THIN 0.823; GOOD 0.916; PURE 1.000 — words/keys/colours all correct. The tool asked for `power=0.55` and the gauge read `progress=0.550 pct='55%'`, i.e. the new inverse hit the target exactly. `evidence/miss_grade_duff_rerun/flick_pops.md`. |
| EditMode counts quoted; parity tests unmodified; `bot_difficulty.csv` zero diff | **PASS** | EditMode **2796 total / 2793 passed / 0 failed / 3 skipped** (re-run after iter-2) (the same three pre-existing `HoleCompleteDriverTests` skips; baseline was 2773/2770/3, +23 from the new fixture). `ShotAimParityTests`, `ShotTimingPowerTests` and `ShotControllerFlickGateTests` are **untouched** (`git status` shows no entry for any of them) and green. `git diff --stat Assets/Resources/Data/bot_difficulty.csv` → empty. |
| Flick tiles recaptured (3), nine byte-identical | **PASS** | Re-shot a second time for iter-2. `md5` of the nine non-Flick tiles is identical to **HEAD** (diff of the hash lists is empty); all twelve `.meta` byte-identical; `git status` on the tiles folder lists exactly `T_Flick_1/2/3.png`. Tile 2 shows both labels and no added rules. |
| Device (Cesar): 0% at touch feels right; 120% reachable without the thumb leaving the glass | **NEEDS CESAR** | Not verifiable here — see § Needs manual verification. |

## Config identity (SPEC §2)

`FlickHandleStartY01 × FlickConeHeightPx == FlickPull120Px ± 1`: **0.8182 × 792 = 648.0144** vs
**648** → 0.0144px. Asserted three ways — `FlickPullMathTests.TheClubsRestFraction_TimesTheConeHeight_IsTheOneTwentyPull`,
`ShotLayoutMathTests`, and the live `rest_is_pull120_above_the_base` assertion. That 0.0144px is
the only place the drawn club and the drawn 100% line disagree, and it is why two assertions in
`FlickPullMathTests` carry a 0.05px tolerance rather than 1e-4.

## Needs manual verification (Cesar / on device)

1. **Feel.** "0% at touch feels right" and "120% reachable without the thumb leaving the glass" are
   device-feel calls the spec assigns to Cesar. What *is* proven here: the 120% point is
   **−1095.84** canvas y against a **−1096** bottom baseline, i.e. the deepest pull ends exactly on
   the shared baseline the action buttons sit on, and the whole travel is 648px from a rest 144px
   under the ball.
2. **The dead-zone catch-up.** Crossing 40px moves the drawn club 40px in one frame. It is the
   mapping's flat segment made visible (no right-inverse of a flat segment is continuous) and the
   alternative would hang the club 40px below an unmoved finger. Worth one look on device.

## Spec deviations

1. **No tick lines — labels only.** SPEC D3 asks for "two tick lines + labels". They were built at
   iter-1 and Cesar removed them on sight: *"Remove the 100% and 120% lines, leave only the
   labels."* The heights, the derivation, the putt rule and the alpha behaviour are all unchanged;
   only the two drawn rules are gone. `FlickConeLabelsBuilder` **deletes** `Tick100`/`Tick120` when
   it finds them, so a scene built at iter-1 is repaired by re-running the builder, and the
   acceptance run asserts no such GameObject survives under `ConeMesh` (`the_tick_lines_are_gone`)
   — deactivating them would not have counted.
2. **Labels are placed outside the cone's EDGE at their own height, not at a fixed +76.** The spec
   says "label x offset as the lane's (+76 right of the axis)". Ported literally it put both labels
   inside the cone body — 186px wide at the 100% height, 216 at the base. The Pendulum's 76 is
   *half a 120-wide lane plus a 16px gap*; a cone is a different width at every height, so the
   **gap** is what ports across. `_labelGapPx = 16` on `ShotConeView`, applied past the cone's live
   half-width (109.09 and 123.77 as measured); in putt mode it is the putter track's 140 instead.
   Club Accuracy moves `ConeHalfAngleDeg` on every club change, so this is recomputed in
   `UpdateConeWidth` — already the one place the cone's drawn width is set.
3. **Four editor bots were updated** (`MissDuffFlickVerify`, `ShotTimingTelemetryVerify`,
   `ShotAimParityDemoRecorder`, `FlickShotViewVerify`). Three inverted the OLD mapping to aim at a
   target power and would have silently aimed 108px short of every power they asked for; the fourth
   asserted the old rest and would have reported FAIL on accepted geometry. They reach the shipped
   mapping through one reflection window, never a second copy of the arithmetic.
4. **`ShotConeViewConfigTests` and `ShotLayoutMathTests` were edited.** Not in the spec's list, but
   each pinned the old 540 / 0.6818 rest by literal. Restated, not deleted or loosened.

## Open questions for Architect

**Both iter-1 questions are closed by the line removal, and neither needs a decision now.**

1. ~~The 100% tick lands 10.8px below the cone's own DUFF band line.~~ **Moot.** There is only one
   line at that height now — the cone's own `TimingBandRedY01` band (0.15 × 792 = 118.8px above the
   base) — and the `100%` label sits beside it at 108px. Worth knowing that the label and that band
   line are 10.8px apart and mean unrelated things (one names a *power*, the other is the *timing*
   line below which a flick is a duff), but they no longer compete visually.
2. ~~The tick lines are straight chords against curved band lines.~~ **Moot** — there are no tick
   lines.

One thing left to note rather than ask: with no rule drawn, a label names a height that nothing
else marks except the club when it arrives there. It reads cleanly in all three captured states
(see `screenshots/`), and the cone's own band line happens to sit right at the 100% label, but it
is a judgement Cesar may want to revisit once he has swung it on device.

## Working-tree note (Rule 13)

Every uncommitted path outside this task's folder is either in the table above or **not mine**.
Not mine, and dirty in the working tree from a concurrently-running `selector_carousel` task:
`Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs`,
`Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs`,
`Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs`,
`Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarousel{Drag,Math}.cs` (untracked),
`Assets/Scripts/Gameplay/Tests/SelectorCarouselMathTests.cs` (untracked),
`Assets/Art/In-Game UI/Halo - Selector.png` (untracked),
`Docs/Specs/Active/selector_carousel/`. Also pre-existing at this task's baseline (HEARTBEAT
`iter-1 kickoff baseline`, HEAD `2a2ad15e`): the eleven `Assets/Art/3D/Trees(2025)/…/*.mat`,
`Docs/GPS/GPS_BACKLOG.md`, `Docs/TellCode.md`, `Claude outputs/`.

`Assets/Scripts/Physics/` — **zero edits**. `Assets/Resources/Data/bot_difficulty.csv` — **zero
diff**. No `*Gate` scenario added to `Scenarios.cs`. No `M_Splash*.mat` touched. No new `Button`
added anywhere, so Rule 11 does not apply.

## Console output

No errors or warnings attributable to this task appeared during either acceptance run or the tile
capture. The only compiler diagnostics in the project are the pre-existing `CS0618` /
`CS8600` / `CS8619` warnings in unrelated recorder and importer files.

## Evidence

| Artifact | Path |
|---|---|
| Live invariants (24 assertions, `fail_count: 0`) | `evidence/flick_pull_mapping_invariants.json` |
| Live measurements + gauge table | `evidence/flick_pull_mapping.md` |
| `miss_grade_duff` band re-run | `evidence/miss_grade_duff_rerun/flick_pops.md` |
| Tile re-capture run log | `evidence/tiles_rerun/HEARTBEAT_tiles.log` |
