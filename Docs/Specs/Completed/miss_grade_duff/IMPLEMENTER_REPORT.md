# Implementer Report — `miss_grade_duff`

**Iteration shape:** `shot-seam:graded-miss-still-flies`
**Iteration:** iter-1

## Implementation summary

Every graded miss in every scheme is now a DUFF rather than a 70 % shot: one flat
`MissPowerMul` (0.20, `PuttMissPowerMul` 0.30 on the green) replaces `TimingPowerMulRed` on the
Pendulum MISS, the Needle SHANK, the Free Swing DUFF exit and — new — a Flick whose aim latched
below the drawn red band line, which has moved off the cone base to `TimingBandRedY01` 0.15. The
ball is also **topped**: `ShotIntent.IsMiss` rides the control-scheme seam into
`ShotController`, which hands `ShotInputBuilder.Build` one new optional `launchPitchScale` so a
duff leaves at loft × 0.35 clamped to [2°, loft]. Alongside that, Flick gained the grade pop it
never had (`FlickGradePop` + `FlickMath` + `FlickGradePopBinder`), the power gauge flashes red
with the **resolved** percentage for exactly as long as the pop is up, and the grade vocabulary
across all four schemes collapsed to one real-golf set — PURE / GOOD / HOOK / SLICE / THIN /
DUFF — published through the two-way content importer in EN and JA.

## Files modified or created

| Path | Change |
|---|---|
| `ssets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/JapaneseBlackPine/MAT_01JapaneseBlackImposter.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/JapaneseBlackPine/MAT_JapaneseBlackBark.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/JapaneseBlackPine/MAT_JapaneseBlackBark_Var1.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/JapaneseBlackPine/MAT_JapaneseBlackLeaf.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/JapaneseBlackPine/MAT_JapaneseBlackLeaf_Var1.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/Metasequoia/MAT_MetasequoiaBark.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/Metasequoia/MAT_MetasequoiaImposter.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/Metasequoia/MAT_MetasequoiaLeaf.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/ScottishPine/MAT_ScottishPineBark.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/ScottishPine/MAT_ScottishPineImposter01.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Materials/ScottishPine/MAT_ScottishPineLeaf.mat` | **NOT MINE** — dirty at the kickoff baseline (`HEARTBEAT.log`), untouched by this task |
| `Assets/Editor/ShotUI/NeedleSchemeBuilder.cs` | modified — authoring placeholder literal `"PERFECT"` -> `"PURE"`, same reason |
| `Assets/Editor/ShotUI/PendulumSchemeBuilder.cs` | modified — authoring placeholder literal `"JUST!"` -> `"PURE"`, so a builder re-run cannot re-introduce a retired word |
| `Assets/Localization/LocalizationText.csv` | modified — +`SHOT_GRADE_THIN`; 5 rows re-worded EN+JA; then re-exported from the published catalog |
| `Assets/Localization/LocalizationTextTable.asset` | modified — bundled table re-imported from the CSV (1100 rows), so a new key does not render as its raw key |
| `Assets/Resources/Data/content_version.txt` | modified — `texts=42` -> `43` after the publish |
| `Assets/Resources/Gameplay/controls.csv` | modified — four new rows + the `TimingPowerMulRed` re-base note + the header's confirm-tile note |
| `Assets/Scenes/Physics/LabScaffold.unity` | modified — `FlickGradePop` (clone of `PendulumGradePop`, 360x142 at +289) under `SchemeRoot_Flick/BallSpace`, `FlickGradePopBinder` on the root, the three retired pop placeholders -> "PURE", cone `_bandRedY01` 0 -> 0.15 |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | modified — +`TimingBandRedY01` / `MissPowerMul` / `PuttMissPowerMul` / `MissLaunchPitchScale` with seeds; `TimingPowerMulRed`'s comment re-based onto the red line |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | modified — four new CSV cases |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | modified — `TimingPowerMultiplier(out isMiss)` with the re-based ramp, `LastShotWasMiss`, `LastFlickGrade`, `launchPitchScale` through `ResolveAndPublish` from both commit paths |
| `Assets/Scripts/Gameplay/Input/ShotIntent.cs` | modified — `IsMiss` (trailing optional ctor arg, default `false`): the one duff carrier across the seam |
| `Assets/Scripts/Gameplay/Tests/FreeSwingMathTests.cs` | modified — DUFF asserts the duff multiplier + `IsMiss`; new putt-duff, big-SLICE-unchanged, and TempoMul-still-bottoms-at-Red cases |
| `Assets/Scripts/Gameplay/Tests/FreeSwingSchemeDriverTests.cs` | modified — the DUFF commit asserts `MissPowerMul` and `LastVerdict.IsMiss` |
| `Assets/Scripts/Gameplay/Tests/NeedleMathTests.cs` | modified — SHANK asserts the duff multiplier + `IsMiss`; new putt-shank and big-HOOK/SLICE-unchanged cases; key table -> PURE/HOOK/SLICE/DUFF |
| `Assets/Scripts/Gameplay/Tests/NeedleSchemeDriverTests.cs` | modified — the SHANK commit asserts `MissPowerMul` and `LastShotWasMiss` |
| `Assets/Scripts/Gameplay/Tests/PendulumMathTests.cs` | modified — MISS asserts the duff multiplier + `IsMiss`; new putt-duff and JUST/GOOD-unchanged cases; key table -> PURE/GOOD/DUFF |
| `Assets/Scripts/Gameplay/Tests/PendulumSchemeDriverTests.cs` | modified — the MISS commit asserts `MissPowerMul` and `LastShotWasMiss` |
| `Assets/Scripts/Gameplay/Tests/ShotTimingPowerTests.cs` | modified — the red case re-based onto the LINE; new duff / flat-launch / gold-line-unchanged cases; the palette assertion now follows the config |
| `Assets/Scripts/Gameplay/UI/ShotUI/ConeBandPalette.cs` | modified — `BandRedY01` const -> `ControlsConfig` (the F15 D3 pattern); + the three-step grade palette and the gauge flash colour |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingMath.cs` | modified — DUFF exit pays the duff multiplier + `IsMiss`; `TempoMul` and its `TimingPowerMulRed` floor untouched |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FreeSwing/FreeSwingSchemeDriver.cs` | modified — one line: `isMiss: v.IsMiss` into the `ShotIntent` (its grader already took `isPutt`) |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleColors.cs` | modified — the ONE-CONSTANT change: perfect zone `#4DA3FF` -> PURE green `#ADEBAD` (D8, Cesar may veto) |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleMath.cs` | modified — `Shank(..., isPutt)` pays the duff multiplier + `IsMiss`; keys -> PURE/HOOK/SLICE/DUFF; big HOOK/SLICE untouched |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Needle/NeedleSchemeDriver.cs` | modified — two lines: `isMiss: verdict.IsMiss` into the `ShotIntent`, and `IsPutt` into `Shank` |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumMath.cs` | modified — MISS branch pays the duff multiplier, `Verdict.IsMiss`, `Grade(..., isPutt)`, keys -> PURE/GOOD/DUFF |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/Pendulum/PendulumSchemeDriver.cs` | modified — two lines: `isMiss: verdict.IsMiss` into the `ShotIntent`, and `IsPutt` into the grader |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/SchemeGradePop.cs` | modified — two colour groups collapsed to `_pureColor`/`_nearColor`/`_duffColor`, re-seeded from the palette at Awake; new `Show(FlickGrade)`; `DisplaySeconds` exposed for the gauge flash |
| `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeGraphic.cs` | modified — `ArcColorOverride`, so the flash is a red arc rather than the gradient's green-at-11% |
| `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs` | modified — the DUFF flash: holds the gauge open for `SchemeGradePop.DisplaySeconds`, red arc + red resolved %, then hands back to the hide rule |
| `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` | modified — one trailing optional `fp launchPitchScale`; pitch = loft x scale clamped to [2 deg, loft], skipped for putts and for the 0/1 no-ops |
| `Assets/Scripts/Physics/Tests/ShotInputBuilderTests.cs` | modified — new `ShotInputBuilderLaunchPitchTests`: bit-identity for the default AND for an explicit 1.0, the 0.35 case, the 2-degree floor, the loft ceiling, and putt-ignores-it |
| `Docs/TellCode.md` | **NOT MINE** — modified DURING this session by the Architect side (its new block reads "`miss_grade_duff` is with Code now" and retires the shipped `shot_view_layout_followup` pointer). It was NOT dirty at the kickoff baseline. Left untouched rather than discarded. |
| `Assets/Animations.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/3D/Characters/_Test/MixamoNative.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/3D/Characters/_Test/MixamoNative/ANIM_Golf_Drive.fbx.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/3D/Characters/_Test/MixamoNative/ANIM_Golf_DriveSetup.fbx.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/3D/Characters/_Test/MixamoNative/ANIM_Golf_Putt.fbx.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/3D/Characters/_Test/MixamoNative/ANIM_Idle.fbx.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/3D/Characters/_Test/MixamoNative/LICENSE_Mixamo.txt.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/3D/Characters/_Test/MixamoNative/MixamoChar_TPose.fbx.meta` | **NOT MINE** — `golfer_3d_test` leftover, dirty at the kickoff baseline, untouched |
| `Assets/Art/In-Game UI/Halo - Selector.png` | **NOT MINE** — art dropped in before this session, dirty at the kickoff baseline, untouched |
| `Assets/Art/In-Game UI/Halo - Selector.png.meta` | **NOT MINE** — art dropped in before this session, dirty at the kickoff baseline, untouched |
| `Assets/Scripts/Gameplay/Input/FlickMath.cs` | **created** — `FlickGrade` + `Grade(t, cfg)` + `GradeKey`; the single home for the band classification the controller and the pop both read |
| `Assets/Scripts/Gameplay/Input/FlickMath.cs.meta` | **created** — Unity meta for the above (Lesson R: the `.cs.meta` ships with its `.cs`) |
| `Assets/Scripts/Gameplay/Tests/FlickMathTests.cs` | **created** — band table, config-driven boundaries, and the cross-scheme unified-key table |
| `Assets/Scripts/Gameplay/Tests/FlickMathTests.cs.meta` | **created** — Unity meta for the above |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FlickGradePopBinder.cs` | **created** — raises the Flick pop off `OnStateChanged` -> `Resolving`, one pop per shot, nothing for a null grade |
| `Assets/Scripts/Gameplay/UI/ShotUI/Controls/FlickGradePopBinder.cs.meta` | **created** — Unity meta for the above |
| `Assets/Scripts/UI/Editor/MissDuffFlickVerify.cs` | **created** — the SS4 visual acceptance run: real boot, real `ClubHandleDragger` events, four bands, four full-res frames |
| `Assets/Scripts/UI/Editor/MissDuffFlickVerify.cs.meta` | **created** — Unity meta for the above |
| `Docs/Specs/Active/miss_grade_duff/HEARTBEAT.log` | this task's pipeline paperwork / evidence |
| `Docs/Specs/Active/miss_grade_duff/IMPLEMENTER_REPORT.md` | this task's pipeline paperwork / evidence |
| `Docs/Specs/Active/miss_grade_duff/SPEC.md` | this task's pipeline paperwork / evidence |
| `Docs/Specs/Active/miss_grade_duff/STATUS.md` | this task's pipeline paperwork / evidence |
| `Docs/Specs/Active/miss_grade_duff/evidence/flick_pops.md` | this task's pipeline paperwork / evidence |
| `Docs/Specs/Active/selector_carousel/SPEC.md` | **NOT MINE** — another task's spec folder, dirty at the kickoff baseline, untouched |
| `Docs/Specs/Active/selector_carousel/STATUS.md` | **NOT MINE** — another task's spec folder, dirty at the kickoff baseline, untouched |
| `Docs/Specs/Active/selector_carousel/reference/selector_before.png` | **NOT MINE** — another task's spec folder, dirty at the kickoff baseline, untouched |

Every uncommitted path in the working tree is listed above, mine and not-mine alike (Rule 13). The not-mine rows are all present in `HEARTBEAT.log`'s kickoff baseline except `Docs/TellCode.md`, which the Architect side changed mid-session and which is called out explicitly.

**Not in the table because they are gitignored** (`.gitignore:252` `Docs/Specs/**/screenshots/`): the four acceptance frames under `Docs/Specs/Active/miss_grade_duff/screenshots/`. They exist on disk at 1170x2532 and are sent to Cesar directly.

## Screenshot

- **Canonical screenshot:** `screenshots/flick_duff.png` — 1170×2532. The frame that reveals the
  whole feature at once: the red **DUFF** pop over the ball AND the power gauge held open,
  arc overridden red, reading the resolved **11 %** (0.55 pull × 0.20) instead of the 55 % the
  player asked for.
- **Also captured:** `screenshots/flick_thin.png`, `screenshots/flick_good.png`,
  `screenshots/flick_pure.png` — the other three Flick bands, same run, distinct md5s
  (`0cf3243a` / `704831f1` / `07007dd0` / `d3260717`).
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` → the real gameplay load (hole 1, Lomond)
- **Play mode:** Yes — booted through StartButton → PLAY → hole card, then swung by raising
  `ClubHandleDragger`'s own IPointerDown / IDrag / IPointerUp handlers. No render harness, no
  synthetic entry point.
- **Hole loaded:** Lomond hole 1 (par 5)

## Figma fidelity

`SPEC.md` mentions Figma only to say the scheme frames are **out of scope** (§5: "Figma: scheme
frames + confirm pop-up frames still say JUST / PERFECT / SHANK — Architect updates them with the
geometry redraw already parked"). This task therefore designs against **no new node**; the two
node ids that appear in the built UI are the ones the existing pop was already built from, and
the rows below are the elements this task touched, A/B'd against the built frames listed above.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Flick grade pop rect | `14091:33996` (Pendulum GradePop — cloned) | 360×142, +289 above the ball | `sizeDelta (360,142)`, `anchoredPosition (0,289)` in `SchemeRoot_Flick/BallSpace`, read back from the RectTransform | PASS |
| PURE word colour | `14091:33996` | `#ADEBAD` | `#ADEBAD` read off `_label.color` live in play mode (`evidence/flick_pops.md`) | PASS |
| GOOD / THIN word colour | `14091:33996` | `#FFEBA6` | `#FFEBA6` on both, read off `_label.color` live | PASS |
| DUFF word colour | `14091:33996` | `#FF5A5A` | `#FF5A5A` read off `_label.color` live | PASS |
| Needle perfect zone fill | `14091:102737` (ResultChip / zones) | `#4DA3FF` at 95 % | `#ADEBAD` at 95 % — **deliberate deviation**, D8's one-constant recolour | PASS\* |
| Gauge DUFF flash | none (no node — new behaviour, D5) | — | arc + `PctText` `#FF3B3B`, `progress01 = 0.110`, `pct = "11%"` | PASS |

`PASS*` is the D8 recolour; see § Spec deviations. The Figma node still shows blue and the
Architect updates it with the parked geometry redraw (SPEC §5).

## UI fidelity lint

Rule 21's usual entry point is `LintPrefab`, and this task creates and changes **zero prefabs** —
the only authored object is a scene GameObject. So the linter's own scene entry point was used:
`UIFidelityLinter.LintRoot(FlickGradePop, "FlickGradePop")`, which exists for exactly this case
("prefabs `LintPrefab` cannot reach"). Render-health ran; there is no `spec.json` because SPEC §5
puts the Figma frames out of scope, so the node-spec layer had nothing to check against.

| Prefab / root | Lint JSON | fail | warn |
|---|---|---|---|
| `FlickGradePop` (scene object, `LabScaffold.unity`) | `Docs/Diagnostics/_capture/FlickGradePop_lint.json` | **0** | 1 |

The single WARN is `unlocalized-text` on `GradeText`: "TMP text \"PURE\" has no LocalizedText
binder." That is by design, and rather than assert it I ran the linter over all four pops:

```
FlickGradePop:     [WARN] GradeText           ::unlocalized-text::  — 0 FAIL, 1 WARN, 0 INFO
FreeSwingGradePop: [WARN] FreeSwingGradeText  ::unlocalized-text::  — 0 FAIL, 1 WARN, 0 INFO
PendulumGradePop:  [WARN] GradeText           ::unlocalized-text::  — 0 FAIL, 1 WARN, 0 INFO
NeedleGradePop:    [WARN] NeedleGradeText     ::unlocalized-text::  — 0 FAIL, 1 WARN, 0 INFO
```

Identical on all four, including the three this task did not author: `SchemeGradePop` resolves
its word imperatively through `LocalizationManager.Get(key)` at Show time rather than through a
binder, precisely so a language switch under a live screen cannot leave the previous language on
the next pop. WARN never changes the `fail` count, so the gate is satisfied on its own terms.

### The gate's own verdict, run and reported rather than worked around

`enforce_implementer_done.py` was invoked by hand against this report (the pipeline hook does not
fire on this session's writes, so it was run deliberately rather than relied on). Every objection
it raised was fixed — the files table now lists all 69 uncommitted paths individually, the
checklist verdicts are bare PASS/FAIL, the baseline block is in its required shape, the test
counts have their own section, and four "pre-existing" claims were re-worded to cite evidence
instead. **One objection remains and cannot be satisfied from here:**

```
UI fidelity lint: could not obtain a FRESH linter run from the live editor for
'FlickGradePop_lint.json' (editor unreachable, prefab not found, or linter error).
(Rule 21 — P2 fail-closed.)
```

The editor is reachable — the linter ran through it four times, output above. The re-run fails
because `_rerun_ui_lint_via_editor` resolves the JSON's stem to `Assets/**/<name>.prefab` and
calls `LintPrefab`, and **there is no `FlickGradePop.prefab`**: all four grade pops are scene
objects in `LabScaffold.unity`, which is why `LintRoot` exists at all. Authoring a lone prefab for
one of the four purely to feed the gate would break the family's authoring convention to satisfy
a check, so it was not done.

Worth the Architect's attention: **Rules 18 and 21 fired here on a detector false positive.**
`spec_references_figma_node` needs the word "figma" plus a `\d{2,}[:-]\d{2,}` token. This SPEC's
only "Figma" is §5's *out-of-scope* line, and the node-id regex matched the DATE `2026-09` inside
`2026-09-07`. Every dated spec that so much as mentions Figma is a Figma-node task by that test.
Two candidate fixes, both small: require the node-id token to sit within a few characters of the
word "figma", and teach `_rerun_ui_lint_via_editor` to fall back to `LintRoot` over a scene object
when no prefab of that name exists.

The stronger gate for this task ran alongside it: every pop's resolved **key**, **word** and
**colour**, and the gauge's **arc override / progress / percentage**, were read back off the live
components in play mode rather than eyeballed — `evidence/flick_pops.md`.

## Test results

`mcp__ai-game-developer__tests-run`, EditMode, whole mode (the runner ignores class filters):

```
Total: 2765   Passed: 2760   Failed: 2   Skipped: 3   Duration: 00:02:34
```

Both failures are `Golfin.UI.Polish.Tests.UiMotion*`, and they are load-flaky rather than mine —
re-running that assembly alone: **Total 96, Passed 96, Failed 0, Skipped 0** in 6.8 s. They are
named as flaky in `AI_CONTEXT.md`'s own 2026-09-07 block. The 3 skips are
`HoleCompleteDriverTests` Stage-C1 skips that carry their own explanatory messages ("Stage C1:
HandleShotComplete is now a no-op…") and belong to a file this task never opened — `git status`
does not list `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs`.

New tests added by this task: `FlickMathTests` (4 fixtures / 11 cases incl. the 8-row band
`TestCase` table), `ShotInputBuilderLaunchPitchTests` (5), plus 6 new cases across the three
grader suites and 4 across `ShotTimingPowerTests`. `ShotAimParityTests` and
`ShotControllerFlickGateTests` are unmodified — `git status` lists neither.

## Acceptance checklist (SPEC §4)

| Item | Result | Justification |
|---|---|---|
| Pendulum MISS ≤ 25 % of the 100 % carry, low flat flight, red pop, gauge flashes red with the resolved % | PASS | Real grader → `CommitExternal` → `BallSimulation`: intended **329.1 yd** at 100 %, landed **30.9 yd** = **9.4 %** (bar is ≤ 25 %); launch pitch 10.90° → **3.82°**; `TimingMul` 0.400; `LastShotWasMiss` true (asserted in `PendulumSchemeDriverTests`). Pop + gauge flash proven on the Flick path in play mode (identical `SchemeGradePop` / `PowerGaugeWidget` code). |
| Needle SHANK same; big HOOK/SLICE still flies at the GOLD multiplier (write the number) | PASS | SHANK: 30.9 yd = 9.4 %, mul 0.400, pitch 3.82°. Big SLICE **unchanged at `TimingPowerMulGold` = 0.900** → 290.9 yd = 88.4 %, `IsMiss` false. |
| Free Swing DUFF same; big HOOK/SLICE unchanged | PASS | DUFF: 30.9 yd = 9.4 %, mul 0.400, pitch 3.82°. Big SLICE at a clean tempo keeps `TempoMul` = **1.000** → 329.1 yd = 100 %, `IsMiss` false; `TempoMul`'s own `TimingPowerMulRed` floor is pinned by a new test. |
| Flick: 0.10 → duff (red flash, ≤ 25 %); 0.15 → 70 %; 0.45 → 90 %; ≥ 0.85 → 100 %. Drawn red line at 0.15 | PASS | Numeric: 0.10 → Duff, mul **0.400**, 30.9 yd = 9.4 %; 0.15 → Thin, mul **0.700**; 0.45 → Good, mul **0.900**; 0.85 and 0.99 → Pure, mul **1.000**. Live: latches 0.028/0.202/0.499/0.881 graded Duff/Thin/Good/Pure at 0.400/0.734/0.912/1.000 (the live run was captured at 0.20; only the DUFF multiplier moved). Drawn line: `ConeBandPalette.BandRedY01` = 0.15 and `ConeMeshGraphic._bandRedY01` re-synced 0 → **0.15** in the scene. |
| Putt miss rolls ~30 % of the intended distance, never stationary | PASS | On `SurfaceType.Green`: clean 100 % putt **48.57 m**, Pendulum putt MISS **13.91 m = 28.6 %**, `PuttMissPowerMul` 0.30, launch pitch **4.00° = unchanged** (D3). Visibly moved: 13.9 m is not stationary. |
| `bot_difficulty.csv`: zero diff after re-running the sigma harness | PASS | Ran `Tools ▸ Golfin ▸ Bots ▸ Calibrate Scheme Sigma` (which REWRITES the file); md5 `03ab630014213ba0694609096f9a697b` before and after, `git diff --quiet` exit 0 — re-run and re-verified again after the 0.40 retune. D4 holds because only power and pitch moved, never yaw. |
| Tournament replay NOTE (§3.2) answered | PASS | Answered in full below under **§ The `ShotCommand` replay NOTE**. Short version: a duff cannot replay wrong because **nothing replays** — `ShotCommand` is a five-field forward-compat stub that production never constructs. |
| Flick shows PURE / GOOD / THIN / DUFF at the four bands (screenshot each); no pop on a rejected flick; no pop on a bot swing | PASS | Four full-res frames, four distinct md5s, keys `SHOT_GRADE_{PURE,GOOD,THIN,DUFF}` read back live. Rejected flick: a rejected flick never reaches `Resolving` (it routes to Idle), and the binder only fires on `Resolving`. Bot swing: `LastFlickGrade` is null whenever the latch is NaN or `ForcePerfectTiming` is on — asserted in `ShotTimingPowerTests`. **Matches the other three schemes**, whose pops are raised by a driver a bot never runs. |
| Every scheme's pop uses the unified words; JUST / PERFECT / SHANK / MISS appear nowhere in the game | PASS | Code: `FlickMathTests.TheRetiredVocabulary_IsReferencedByNoScheme` asserts it over all 15 grades of all four schemes. Scene: the three authoring placeholders that still read "JUST!"/"PERFECT" are now "PURE", and the two builders that would re-write them are fixed. Strings: the five affected rows re-worded EN+JA. **Caveat:** the four retired ROWS could not be deleted — see § Spec deviations. |
| Strings through the importer: PLAN quoted, `texts` published, `--check` clean; `SHOT_GRADE_THIN` present, retired keys gone | PASS | **Was FAIL; closed in the admin on Cesar's instruction (see § The four retired rows, closed).** PLAN: `texts 1 add / 5 change / 1090 same / 0 conflict`. `--apply` wrote 6 drafts; `content_publish` returned **v43**; `export_content.py --check` → **"clean — no file would change and no catalog has drifted"**. `SHOT_GRADE_THIN,THIN,トップ` present and in the bundled table. The four retired keys are now **deactivated** at **texts v44** — `is_active: true -> false`, 0 added / 0 changed / 4 deactivated. |
| Needle perfect zone drawn in PURE green (or the veto noted) | PASS | `NeedleColors.ZonePerfect` now pre-composites `#ADEBAD` at 95 % over `ZoneGood`. Flagged as the one-constant change Cesar may veto (§ Spec deviations). |
| `grep` for new hardcoded `.text` literals (zero) | PASS | `grep -Hn "\.text\s*="` over all 31 changed `.cs` files gives 8 hits. Seven are unchanged by this task (verified line-by-line against `git diff`): two builder `tmp.text = preview` (parameterised), one test placeholder, `SchemeGradePop`'s `LocalizationManager.Get(key)`, and the gauge's three numeric formats, all seven unchanged in `git diff`. The ONE new line is `PowerGaugeWidget:223`, and it is a number: `$"{distance:F1} {(… "mts" : "yd")}"` — byte-for-byte the unit suffixes line 168 beside it already wrote before this task (unchanged in `git diff`). **Zero new WORDS**: every word this task shows resolves from a key. |
| All EditMode tests green; counts quoted; `ShotAimParityTests` / `ShotControllerFlickGateTests` unmodified | PASS | **2765 tests: 2762 pass, 0 fail, 3 skip** after the 0.40 retune (§ Test results). The run before the retune had 2 failures, both `Golfin.UI.Polish.Tests` `UiMotion*`, which pass **96/96 with 0 failures** when that assembly is run alone — load-flaky, and named as such in `AI_CONTEXT.md`'s 2026-09-07 block. The 3 skips carry their own Stage-C1 messages. `git status` lists neither `ShotAimParityTests.cs` nor `ShotControllerFlickGateTests.cs`, so both are untouched. |
| Unity Console clean; deviations flagged | PASS | 100 log entries from the play run, **0 errors / 0 exceptions / 0 asserts**. Deviations below. |

## Known FAIL items

**None.** The one FAIL — *"the four retired keys gone"* — was closed in the admin dashboard after
Cesar handed the browser over (§ The four retired rows, closed). Everything else in this report
passed on the first grading.

## Spec deviations

1. **`FlickMath.cs` lives in `Assets/Scripts/Gameplay/Input/`, not `.../UI/ShotUI/Controls/`.**
   SPEC §6 lists it under `Controls/`; SPEC §3.6 says "next to `TimingPowerMultiplier`'s
   thresholds". Only the second is possible: `Golfin.Gameplay.UI` references
   `Golfin.Gameplay.Input`, so a `FlickMath` in the UI folder could not be read by
   `ShotController` and the "no duplicates" requirement would have forced a second copy of the
   band thresholds. Put in the Input assembly, ONE copy serves both — `TimingPowerMultiplier`
   asks `FlickMath.Grade` whether the latch was a duff.

2. **The four retired string rows are DEACTIVATED, not deleted** — which is the pipeline's own
   definition of the delete. `import_content.py` states it: *"Deactivating in the admin is the
   delete."* Invariant I6 means no tool will ever remove the row; `is_active: false` is what
   retirement looks like, and `ContentCatalogMapper` / `ContentRow.IsActive` handle it end to end.
   Done in the admin at **texts v44**. Side effect worth knowing: this was the FIRST deactivation
   in the `texts` catalog, so the exporter widened `LocalizationText.csv` with an `is_active`
   column — **2201 changed lines, of which 1096 are `,true` appended to untouched rows.**
   `LocalizationTextImporter` reads `cols[0..2]` and ignores the fourth, so the bundled table is
   unaffected: 1100 rows, zero column leakage (verified by asserting no row's English or Japanese
   field equals `"true"`/`"false"`).

3. **`NeedleColors.ZonePerfect` blue → PURE green** — this is D8, done as specified, and it is
   the one-constant change the SPEC asked me to flag for veto. Reverting is one literal.

4. **The confirm pop-up tiles were NOT recaptured**, as instructed — no UI geometry changed. But
   the `SCHEME_POPUP_*_LINE` strings did, in three schemes rather than the two the SPEC names:
   `SCHEME_POPUP_FLICK_LINE2` said "green is perfect timing", and Flick now genuinely pops PURE
   on green and DUFF below the red line, so it names them. Verified the words live only in the
   LINE strings, never in the captured tiles.

5. **`TIP_ACCURACY` and `TIP_TIMING` were also re-worded.** The SPEC says to grep every
   `SCHEME_POPUP_*` / `TIP_*` row for the retired words; these two carry "PERFECT" as an
   ADJECTIVE, not a grade. `TIP_TIMING` genuinely describes the PURE grade so it now says PURE
   (EN + JA); `TIP_ACCURACY` says "FULL ACCURACY", because "pure accuracy" is not English. Its
   JA (完璧な精度) uses 完璧, not the retired パーフェクト, so it was left.

6. **`MissPowerMul` shipped at 0.20, then Cesar took it to 0.40 the same day.** The SPEC's CSV
   note said a duff "dribbles ~20 % of the intended carry". Measured, it does not: 0.20 is a
   **velocity** multiplier and carry is super-linear in launch speed, so at 0.20 the duff was
   **6.0 yd of 329 (1.8 %)** — inside D1's "a few yards" but a long way from the prose. That
   measurement is what the decision was made on; at **0.40** the duff is **30.9 yd (9.4 %)**,
   still half the ≤ 25 % acceptance bar and a far more readable bad shot. `PuttMissPowerMul`
   was NOT moved (Cesar changed one knob), so the putt duff is unchanged at 28.6 %. The note in
   `controls.csv` now records both measurements and says to raise THIS key rather than the pitch
   scale.

   One test moved with it: `Grade_Miss_OnAPuttPaysThePuttDuffMultiplier` asserted
   `PuttMissPowerMul > MissPowerMul`, which held only by accident at 0.30 vs 0.20 and broke at
   0.40. It now asserts what D3 actually asks for — an absolute floor, `PuttMissPowerMul > 0.1`,
   "never stationary" — because the two are independent feel knobs on different clubs and
   ordering them was never the rule. **This is the test catching a real thing**, not being
   bent to fit.

7. **Two extra files.** `PendulumSchemeBuilder` / `NeedleSchemeBuilder` author the pop's
   placeholder text as a C# literal (`"JUST!"` / `"PERFECT"`); left alone, re-running either
   builder would put the retired vocabulary straight back into the scene. Two one-word literals.

## The four retired rows, closed

Cesar handed over the logged-in admin ("the admin is open in chrome and logged in, you take care
of that text"), so the item that was graded FAIL is now done.

`SHOT_GRADE_JUST`, `SHOT_GRADE_MISS`, `SHOT_GRADE_PERFECT` and `SHOT_GRADE_SHANK` were each
opened in *Edit draft row* and had **Active** unticked — the control whose own help text is
*"Turning this off deactivates the row: it leaves shops and pools, and every player who already
owns one keeps it. Nothing is ever deleted."* The publish dialog was read before confirming:

```
0 added   0 changed   4 deactivated   0 reactivated
  deactivated  SHOT_GRADE_JUST      is_active  true -> false
  deactivated  SHOT_GRADE_MISS      is_active  true -> false
  deactivated  SHOT_GRADE_PERFECT   is_active  true -> false
  deactivated  SHOT_GRADE_SHANK     is_active  true -> false
```

Nothing else was in the diff. Published: **"Published texts v44 — 0 added, 0 changed, 4
deactivated"**, then *"Drafts match what is published. There is nothing to publish."* The six live
grade rows (DUFF / GOOD / HOOK / PURE / SLICE / THIN) stayed active throughout.

Repo followed: `export_content.py --catalogs texts` then `--check` → **clean** at v44,
`content_version.txt` `texts=43` → `44`, and the bundled table re-imported (1100 rows, the four
retired keys still present but resolved by nothing).

**One trap for the next person driving this dashboard:** `form_input` on the Active checkbox sets
the DOM property without firing React's onChange, so *"Draft saved"* appears and the draft is
saved UNCHANGED. `SHOT_GRADE_JUST` went through that silently and only showed up because its
State column still read `—` while the row done by a real click read `OFF`. **Click the checkbox;
do not set it.** Every one of the four was verified as `OFF` in the list before publishing.

## The `ShotCommand` replay NOTE (SPEC §3.2)

**Asked:** does `ShotCommand` serialise everything `Build` consumes, or does a duff replay as a
full-loft shot on the opponent's device?

**Found:** the question is moot today, and the gap is far wider than the pitch scale.
`Assets/Scripts/Tournaments/ShotCommand.cs` is a five-field forward-compat stub —
`ShotIndex`, `Power`, `Accuracy`, `ClubId`, `CommittedUtc` — and its own header says *"v1 never
reads it; a future server re-simulates the deterministic sim from the log"*. Of the fourteen
arguments `ShotInputBuilder.Build` consumes it carries **none** verbatim: no `timingMul`, no
`spinInputX/Y`, no `fadeDrawInput`, no `baseVelocityOverrideMps`, no `seed` — and now no
`launchPitchScale`.

**And nothing populates it.** `grep -rn "new ShotCommand(" Assets --include=*.cs` outside tests
returns **zero** hits; every production site passes `new List<ShotCommand>()`, with
`TournamentRoundHandler.cs:101` carrying `// TODO server-replay: capture shots` and
`SaveBackedEntryStore.cs:72` `// D1: inputLog not persisted in v1`. So a duff cannot replay as a
full-loft shot on the receiving side, because **no shot replays at all**.

**What this task owes the future:** when server re-simulation is built, `launchPitchScale` must
ride in the command alongside the five fields already missing. It is one more field on a struct
that needs about six, and adding it now — to a struct nothing writes and nothing reads — would be
a schema change with no consumer to validate it. Recommend a `GPS_BACKLOG.md` row rather than a
speculative field. **No `ShotCommand` change was made.**

## Console output

Zero errors, zero exceptions, zero asserts across the whole play-mode acceptance run (100 log
entries, all `Log`). The tool's own trace:

```
[MissDuff] hole: 1
[MissDuff] resolution: 1170x2532
[MissDuff] driver: real ClubHandleDragger IPointerDown/IDrag/IPointerUp events
[MissDuff] pop_found: FlickGradePop
[MissDuff] gauge_found: PowerHUD
[MissDuff] cone_bands: red=0.15 gold=0.45 green=0.85
[MissDuff] duff_ACCEPTED: latch=0.028 grade=Duff key=SHOT_GRADE_DUFF word='DUFF'
           color=#FF5A5A popAlpha=1.00 mul=0.200 isMiss=True power=0.55
           gauge=[alpha=1.00 arcOverride=#FF3B3B progress=0.110 pct='11%' pctColor=#FF3B3B]
[MissDuff] thin_ACCEPTED: latch=0.202 grade=Thin key=SHOT_GRADE_THIN ... mul=0.734 isMiss=False
[MissDuff] good_ACCEPTED: latch=0.499 grade=Good key=SHOT_GRADE_GOOD ... mul=0.912 isMiss=False
[MissDuff] pure_ACCEPTED: latch=0.881 grade=Pure key=SHOT_GRADE_PURE ... mul=1.000 isMiss=False
```

## Two traps worth recording

- **A scene save baked 21 layout-group rects.** Saving `LabScaffold.unity` after adding one
  object rewrote `DebugPanel`'s rows and both `ChipStack`s from `(0,0)` anchors to computed
  ones — 352 insertions for a 236-line change. The scene was reverted to HEAD and only the 11
  intended hunks re-applied (`git apply` of a filtered patch); the churn hunks were net-zero in
  line count so the offsets held. Final diff: **236 insertions / 21 deletions**, no `m_IsActive: 0`,
  no un-asked-for geometry.
- **`SnapPlayModeSafe` returned a path for a file it never wrote** — the documented failure mode,
  hit on the first run for all three captures. Cause: in play mode it composites via
  `ScreenCapture.CaptureScreenshotAsTexture`, which only works at end-of-frame, and its own
  summary says the caller owns that yield. Fixed by yielding `WaitForEndOfFrame` first; the tool
  now also asserts the file exists, is > 1 KB, and md5s it, so a phantom or a stale frame fails
  loudly instead of being reported as evidence.

## Open questions for Architect

**All three are now answered — none are open.**

1. ~~Is 6 yards the duff Cesar wants?~~ **No — he took `MissPowerMul` to 0.40, measured at
   30.9 yd (9.4 %).** Applied, re-tested, sigma re-verified.
2. ~~The four retired string rows need a deactivate in the admin.~~ **Done — texts v44**,
   0 added / 0 changed / 4 deactivated (§ The four retired rows, closed).
3. ~~The Needle green zone veto.~~ **Not exercised — the zone stays PURE green.**
