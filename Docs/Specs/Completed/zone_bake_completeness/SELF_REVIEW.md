# Self-Review — `zone_bake_completeness`

**Iteration:** iter-4 (implementer's fourth pass; architect has reviewed iter-1, iter-2, and iter-3)
**Reviewer:** golfin-self-reviewer
**Date:** 2026-07-28 22:20 JST
**Verdict:** **FORWARD_TO_ARCHITECT**

---

## Task type

Tier-3 bake-pipeline + physics-classification. No Figma node, no UI containment claims. Acceptance is deterministic probes + real-gameplay behavior. Standard Figma-fidelity / bbox / capture-helper gates do not apply; production-flow capture gate DOES apply and is the primary visual acceptance mechanism.

---

## Step 1 — Independent pixel scan (screenshots read cold, no report/spec)

### `screenshots/h14_after_canonical.png` (1170×2532)

Top-left: character portrait card "JAMES / Lv 10 / TURN 3". Top-right: hole info card "LOMOND / HOLE 14 - REGULAR / PAR 4" with a green mini-map. Under the info panel a flag icon reads "2 mts" and a wind-direction icon reads "0.0 mph".

Center of frame: a large white golf ball with a green "G" logo sits directly in front of a red golf flag ("GOLFIN GOLF CLUB" pennant). Both sit on a manicured lawn (bright saturated green — putting-surface texture) with a light-grey cart path and trees behind. A pale-yellow sand bunker edge is visible at the left. The ball casts a soft shadow onto the surface directly below it.

Bottom UI widgets (all four visible):
- Bottom-left: "SPIN" widget with a ball icon.
- Bottom-left below: "GOLFIN ∞" widget (ball charge).
- Bottom-right upper: "STRAIGHT" widget with an up arrow.
- **Bottom-right lower: "PUTTER" widget with a putter-club icon and the text "27 mts" underneath.**

### `screenshots/h15_after_canonical.png` (1170×2532)

Top-left: "JAMES / Lv 10 / TURN 1". Top-right: "LOMOND / HOLE 15 - REGULAR / PAR 3" with mini-map. Flag icon reads "80 yds", wind reads "0.0 mph".

Middle of frame: a low-camera view down a grassy fairway (chunky, coarser texture than the h14 green — no cart path or trees visible near). A small white golf ball sits mid-frame with a golden aiming line dropping vertically from it. On the right: a green/dark charge dial reads "38%" over "95.0 yd".

Bottom-left: "SPIN" + "GOLFIN ∞" widgets. Bottom-right upper: "STRAIGHT". **Bottom-right lower: "DRIVER" widget with a driver-head icon and "250 yrds" underneath.**

An overlaid caption strip near bottom reads (partial, cut off at left edge): "…15 AFTER — zone_bake_completeness iter-3 / …ee shot: power=0.38, ball settles at z~41 (fairway ring, z<55.4=green boun[dary] / …mulation.IsPutt=False | ShotController.IsPutt=False / …DRIVER (correct for Fairway surface) / …json Fairway polygon now present -> Fairway classification working / …IsPutt=False on fairway confirmed (inverted test case)".

**Pixel-scan conclusion:** the two canonicals show, without inference, exactly the two behavioural results the task must produce — PUTTER auto-selected + "2 mts" to flag on the Hole 14 green, and DRIVER retained + full-power dial with ball still on the fairway ring on Hole 15. Both frames are full-resolution 1170×2532 iPhone 14 canvas, sanctioned production-flow capture path (via `BotVideoRecorder`/`SnapAtEndOfFrameAndPause`).

---

## Step 2 — No Figma reference (Tier-3 bake task)

Not applicable. SPEC references no Figma node; there is no `reference/` folder because the acceptance gate is deterministic probes + gameplay, not visual fidelity to a design. Skipping the Figma-fidelity/reference-diff rules.

---

## Step 3 — SPEC §5 acceptance walk, item by item

| SPEC §5 item | Report claim | Evidence I verified | Verdict |
|---|---|---|---|
| Stage 1 report answers §3's three questions with control comparison | PASS | Report lines 44–76 answer Q1 (`guard1Rej=0 guard2Rej=0 safetyTrips=0` on all 5 holes → H1 dead), Q2 (stale m4-era scenes; extractor correct), Q3 (`BallSimulation.cs:758 IsPuttSurface = Green \|\| GreenCollar` → putter gate confirmed). Q1 counter output is cited from live console. | **CONFIRM-PASS** |
| All 18 holes: every source-raster type above §4.2 threshold present in baked zones.json | PASS | Report §"All-18 re-bake diff" enumerates each hole's before/after types. Reviewed 4 target-defect holes (H02/H12/H14/H15) and H03 additional defect — all types restored. Threshold justified at 1000 cells (largest legit surface Green ~6038; noise `background`~400, `semi_rough` 400–830). | **CONFIRM-PASS** |
| H01 Green (control) → Green/Polygon | PASS | Live console output cited: `(-230.37,-72.60) -> Green/Polygon | expected Green/Polygon -> PASS`. Cannot re-derive without Unity but timestamp+format matches other confirmed §5 probes. | **CONFIRM-PASS** |
| H02 Green → Green/Polygon | PASS | Cited: `(-97.04,137.33) -> Green/Polygon`. | **CONFIRM-PASS** |
| H12 Green → Green/Polygon | PASS | Cited: `(107.52,157.72) -> Green/Polygon`. | **CONFIRM-PASS** |
| H14 Green → Green/Polygon | PASS | Cited: `(-111.55,127.59) -> Green/Polygon`. This is the same coord the bot's H14 shot 4 lands within (bot log t=90.80 Green isStop=True at pos (-111.71, 14.29, 129.21)). Coordinate cross-checks. | **CONFIRM-PASS** |
| H14 Fairway → Fairway/Polygon | PASS | Cited: `(-50.72,72.36) -> Fairway/Polygon`. | **CONFIRM-PASS** |
| H15 Fairway → Fairway/Polygon (inverted case) | PASS | Cited: `(7.71,52.88) -> Fairway/Polygon` using poly[1] centroid; poly[0] centroid (15.27,68.06) falls inside legitimate Green_1 zone per orchestrator-confirmed x[2.78..29.22] z[55.40..81.88]. Bot log t=45.22 Ball settled + t=45.27 IsPutt=False + t=28.67 `surface=Fairway isStop=True` corroborates classification at z<55.4. | **CONFIRM-PASS** |
| §4.2 gate deliberately failed → blocks write | PASS | Cited console (18:12:42 JST): reflection invocation with empty zones on Hole_01 logged 5 `LogError` entries (fairway/green/tee_box/bunker/cart_path all absent from empty output), returned `false`, `BakeOne()` exits before write. Source verified: `BakeZoneJsonTool.cs:80,171,343` — `COMPLETENESS_CELL_THRESHOLD=1000`, `CheckCompletenessGate` invoked pre-write from `BakeOne()`. | **CONFIRM-PASS** |
| 14 unaffected holes byte-identical or every diff explained | PASS with explanation | Report: not byte-identical; polygon counts increased across all 13 nominally-unaffected holes because current Geo scenes have more/updated mesh objects vs m4-era bake; types unchanged. Orchestrator kickoff-context note ("no surface-type churn, obMask byte-stable, monotonic point increases") corroborates. Extra note: H03 CartPath restored (21,717 cells) — legitimate pre-existing bake gap surfaced by re-bake, explained. | **CONFIRM-PASS** |
| EditMode 943/938/2/3 baseline | PASS | Report cites run at 18:12 JST matching baseline; 2 pre-existing StaminaLiveWiring failures + 3 pre-existing HoleCompleteDriverTests skips. Not independently rerun by self-review; matches established baseline used across recent tasks. | **CONFIRM-PASS** (accepted on baseline continuity) |
| §6 videos H14/H15 before+after | PASS ("before" waived by architect at iter-2; "after" real gameplay in iter-3+4) | Both `videos/h15_after.mp4` (15MB captioned) and `videos/h14_after.mp4` (56MB captioned) exist on disk. Raw sources (60MB, 128MB) also exist. Canonical screenshots reviewed in Step 1 confirm the required behaviour. | **CONFIRM-PASS** |

---

## Step 4 — Non-circularity verification (SPEC §6 rule + architect fix #2b)

**Grepped `Assets/Scripts/Physics/Viewer/Bot/ZoneBakeAfterClipBot.cs` for banned calls:**

```
19:    /// NO circular gate: no PlaceBallAt, no forced SetClub(putter), no injected preferredSurface.
313:                // Fire a putt WITHOUT calling SetClub(0) — let the game derive the club from IsPutt=True.
314:                // The bot previously called SetClub(0)=Driver before every shot, overriding auto-selection.
547:        // ── Fire one shot (no SetClub(putter), no PlaceBallAt, no preferredSurface) ──
555:            ctrl.SetClub(clubIndex); // 0 = Driver (NOT putter — putter auto-engages from surface)
```

- **`PlaceBallAt`:** zero matches. The bot never teleports the ball.
- **`preferredSurface` injection:** zero matches. No surface hint fed into the game.
- **`SetClub(putter)`:** only `SetClub(clubIndex)` where `clubIndex=0` (Driver) — the report calls this out at line 555. The putt firing block explicitly comments "Firing putt (NO SetClub)" and bot log line 69 (`Firing putt (NO SetClub): aimYaw=-0.742 power=0.04`) corroborates that the putt is fired with no forced club — the derived putter selection stands.
- **Tap-to-aim event path:** `ClubContext.RequestSelection(putterBagIdx)` + `ClubSelectionBroadcast.Raise(3)` at bot lines 294–295. Cross-checked against production widget: `SelectorOverlayWidget.cs:315-316` uses the **identical two-call pair** in its card-tap `onClick` lambda. This is the real widget path, not a synthetic entry.

**Non-circularity: VERIFIED.** The bot navigates ShellScene→StartButton→Home→ModeCardPlay→HoleSelection→Hole 14 card→`BeginGameplayLoad(14)`, fires 4 real shots to the green via `FireViaShotController`, ball reaches Green via zones.json Green polygon (bot log t=90.80 `TerrainHit: surface=Green isStop=True`), `ShotController.IsPutt=True` is **derived** (t=107.57), then the tap-to-aim event pair is fired, `SelectedTypeLabel=PUTTER` is the result of `ClubContextPopulator.SelectByIndex` running through the real event chain (t=114.57), and canonical capture at t=117.21 shows PUTTER in HUD.

---

## Step 5 — Capture-helper compliance

- **Screenshots** (`s02_h14_settled_a…png`, extracted to `h14_after_canonical.png`): captured via `SnapAtEndOfFrameAndPause` (synchronous, per bot code and the report's iter-4 section). Compliant with CLAUDE.md § Screenshots rules.
- **Videos** (`h14_after_raw.mp4`, `h15_after_raw.mp4`): captured via `BotVideoRecorder` GameView mode at 1170×2532 30fps (report iter-3 iter-4). Compliant with the URP-HUD capture rule (GameView, not camera-source) and the Mac/Metal fix (Arm@EnteredPlayMode pattern).
- **No new `*Context.cs`** introduced by this task, so the `capture_helper` maintenance protocol does not apply.

Compliance: OK.

---

## Step 6 — Bbox geometry check

Not applicable. No containment claims (no "text inside container", no "modal within canvas"). Task is bake/classification/gameplay.

---

## Step 7 — Scene mutation audit

`git diff HEAD -- Assets/Scripts/Physics/` shows exactly one file: `BakedZoneClassifier.cs`, and its diff is the `ClassifyWithProvenance` refactor which is documented in `HEARTBEAT.log` line 4 as **pre-existing `surface_coverage_audit`** work at kickoff baseline. This task added **zero** new edits to `Assets/Scripts/Physics/`. Rule 7 satisfied.

`git diff HEAD -- Assets/Scenes/` returns empty. **Zero scene file mutations.** No `m_IsActive` flips, no RectTransform changes, no position shifts.

New bot files under `Assets/Scripts/Physics/Viewer/Bot/`:
- `ZoneBakeAfterClipBot.cs` — verified wrapped `#if UNITY_EDITOR` (top of file), sits alongside `ObBoundaryCaptureBot`, `VersusHudCaptureBot`, `BotVideoRecorder` family. Editor-only, no iOS-player-build risk.
- `Bot/Editor/ZoneBakeAfterClipMenu.cs` — under `Editor/`, obviously editor-only.

Per architect iter-3 note: "sit alongside the existing capture-bot family… fully `#if UNITY_EDITOR`-wrapped… spirit of Rule 7 holds." Accepted.

**Drift outside task scope (flagged, not blocking):**
- `Docs/Diag/baked-pivot/M0-regression-DriverFromGreen.md` / `PutterFromGreen.md` / `WedgeFromBunkerEdge.md` — small numeric drift (samples ±1, minBallY ±0.037). Consistent with re-bake affecting classification-dependent physics traces. Not attributed in the report's Files-modified table. Kickoff-baseline iter-2 line 24 lists `Docs/Diag/baked-pivot/` as pre-existing DIRTY though, so treated as pre-existing carry.
- `Docs/Scripts/com.golfin.dailyreport.plist` — polling interval 600s→120s. **Not attributable to this task.** Unrelated daily-report tuning. Recommend the architect ask Cesar to revert or attribute at close-out; not blocking self-review since it's operational/docs drift, not code or scene mutation.
- `Assets/Settings/*.asset` + `ProjectSettings/ProjectSettings.asset` — flagged pre-existing at kickoff baseline, still pre-existing. Rule 13 satisfied via HEARTBEAT baseline attribution.

---

## Step 8 — Production-flow capture

Verified. Bot log `zone_bake_h14_green.log` starts with `NavigateToHome: waiting for app startup sequence…` (t=7.98), clicks the Splash `StartButton` (t=8.51), reaches Home (t=11.43), clicks the Practice mode card PLAY (t=13.61), transitions to HoleSelection (t=15.12), taps the Hole 14 card (t=18.19), calls `SeedSession(14,'',1)` + `BeginGameplayLoad(14)` (t=19.72–73), waits for `LabScaffold` + `Hole_14_Geo` scene loads (t=21.26–22.06), then fires 4 real shots via `FireViaShotController`. **This is the production ShellScene→BeginGameplayLoad flow, not a `*Gate` scenario, not `LoadSceneAsync("LabScaffold", Single)`.** Grepping `Scenarios.cs` confirms no new `*Gate` entries were added.

---

## Step 9 — Architect fix-list resolution (iter-1 → iter-3)

| Architect fix | Iter-1/2 status | Iter-3/4 resolution | Verdict |
|---|---|---|---|
| Fix #1: Hole 15 scene fix | Retracted — iter-1 concern was donut-centroid probe artifact; Green_1 IS legit green. | No-op (correctly). | **OK** |
| Fix #2: §6 after clips (real flow) | Iter-2 rejected (teleport stills, `PlaceBallAt`+`SetClub`). | Iter-3 real bot gameplay accepted; H15 fully clean, H14 behavioural fix accepted with putter-widget gap. | **OK** |
| Fix #2c: H14 putter widget via real tap-to-aim | Iter-3 gap identified. | Iter-4: `ClubContext.RequestSelection` + `ClubSelectionBroadcast.Raise(3)` — verified identical to `SelectorOverlayWidget.cs:315-316` card-tap. Bot log t=114.57 `SelectedTypeLabel=PUTTER`. Canonical screenshot at t=117.21 shows PUTTER 27 mts. | **OK** |
| Fix #3: Non-defect spot-probes H06/H11/H17 | Iter-2. | All 6 probes PASS (Fairway/Polygon + Green/Polygon each). | **OK** |

---

## Findings / minor flags for the architect

1. **`Docs/Scripts/com.golfin.dailyreport.plist` polling change (600s→120s)** is not attributable to this task. Recommend architect asks Cesar to revert or commit separately at close-out. Not a self-review blocker.
2. **`Docs/Diag/baked-pivot/M0-regression-*.md`** — minor numeric drift consistent with re-baked zone classification. Baseline attributed at iter-2 kickoff but not re-cited in the current Files-modified table. Recommend adding a one-line note in the report at close-out explaining "M0 regression tables re-ran against new zones.json; drift is float sample-count noise, all rows still PASS." Not a self-review blocker.
3. **Iteration count = 4.** Guidance says N≥3 + FAIL → escalate, but the verdict here is FORWARD, so no escalation. Architect has been in the loop iter-1 through iter-3; iter-4 addresses the single remaining issue architect flagged in iter-3 (putter widget via real tap-to-aim), and the fix has clean primary-source evidence (bot log timestamps + canonical PNG).

---

## Verdict

**FORWARD_TO_ARCHITECT.** Setting STATUS to `SELF_REVIEW_PASS`.

- SPEC §5 acceptance walk: all rows CONFIRM-PASS.
- §6 non-circularity: independently VERIFIED via grep (no `PlaceBallAt`, no `preferredSurface`, no forced `SetClub(putter)`; tap-to-aim uses same event pair as `SelectorOverlayWidget.cs:315-316`).
- Scene mutations: none.
- Rule 7: satisfied (only pre-existing `BakedZoneClassifier.cs` in Physics/, all new bot code editor-only under Bot/).
- Canonical screenshots visually confirm both required end-states (PUTTER 27 mts on H14 green; DRIVER 250 yrds on H15 fairway ring).
- Production-flow capture: verified via bot log (Splash→Home→HoleSelection→BeginGameplayLoad).
- Report integrity: every PASS backed by console output, git-verified file path, or bot-log timestamp. No fabrication detected.

Two minor drift flags (dailyreport.plist, baked-pivot regression tables) surfaced for architect awareness at close-out; neither blocks forward.
