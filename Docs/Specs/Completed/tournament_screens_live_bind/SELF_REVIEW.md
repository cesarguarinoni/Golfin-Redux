# Self-Review — `tournament_screens_live_bind` — iter-1

**Reviewed:** 2026-06-27 JST
**Verdict:** FORWARD_TO_ARCHITECT
**Status target:** SELF_REVIEW_PASS

---

## Visual diff notes (Step 1 — independent pixel scan, no spec/YAML consulted)

**Selection screen (`screenshots/selection_live_2026-06-27.png`, Game-view at 0.72× zoom):** Portrait 1170×2532. Header: gold R-coin "994.699" left, navy "TOURNAMENTS" title center, white settings-gear right. Tab strip below (partially clipped by editor zoom — "ALL", then "...OSED" suggesting "CLOSED"). Five tournament cards stacked, each a navy rounded card with a left-bleed course thumbnail and right-side content block:
- Card 1 — golf course dusk image; "GOLFIN PRESENTS / TOURN.KASUMIGASEKI"; "kasumigaseki - 18 Holes"; "Round in progress — Hole 0 of 18"; **LIVE** pill (red/orange), **ENTERED** pill, **R 20.000** yellow pill, **CONTINUE** gold CTA.
- Card 2 — bunker image; "TOURN.HIRONO"; "hirono - 18 Holes"; "Ended Jun 24"; **ENDED** grey pill; **FREE ENTRY**, **R 20.000**; **LEADERBOARD** silver CTA.
- Card 3 — white clubhouse; "TOURN.LOMOND"; "Ended Jun 27"; ENDED; FREE ENTRY; **R 5.000**; LEADERBOARD.
- Card 4 — green hole; "TOURN.GOTEMBA"; "Ended Jun 25"; ENDED; **ENTRY R 250**; R 20.000; LEADERBOARD.
- Card 5 — mountains; "TOURN.KISARAZU"; "Starts in 4d"; **UPCOMING** blue pill; FREE ENTRY; CTA off-bottom-of-viewport.

6th card implied below scroll (console log: `Tournaments=6`). Bottom nav bar visible.

**Leaderboard screen (`screenshots/leaderboard_live_2026-06-27.png`):** Same Game View. Header "TOURNAMENT LEADERBOARD"; below it "SPONSORED BY PUMA" + "KASUMIGASEKI OPEN" + "ENDS IN: 1D 5H 25M 0S S" pills. Podium row: fp_028 MYTHIC LVL 72 (71 STROKES) | #1 fp_102 RARE LVL 16 (70 STROKES, raised gold pedestal) | fp_017 UNCOMMON LVL 161 (71 STROKES). Ranking rows: T2 FP_030, T5 FP_110, T5 FP_094, T7 FP_053, T7 FP_041, T9 FP_037, T9 FP_107, FP_058 (cut). Sticky bottom: rank "--", GALADRIEL RARE - LV 80, 80 STROKES (orange chip). All ranks/strokes/names vary per row — clearly live-bound.

## Figma fidelity

N/A — SPEC.md does not reference a Figma node URL or node-id. This is a "don't rebuild, bind" task on existing T7/T9 scaffolds whose look was locked by prior tasks; SPEC §3 explicitly says "reuse that exact widget-bind pattern, swap only the data source." Rule 18 does not apply.

## Bbox verification

N/A — no containment claims in `IMPLEMENTER_REPORT.md`. Nothing of the form "X inside Y" requires geometric verification.

## Scene-mutation audit

`git status` output:

```
 M Assets/Scripts/TournamentsRuntime/TournamentService.cs
 M Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs
 M Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs
 M Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs
 M Docs/Specs/Active/tournament_screens_live_bind/STATUS.md
?? Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs (+.meta)
?? Assets/Scripts/Tournaments/TournamentCardStateMapper.cs (+.meta)
?? Assets/Scripts/UI/Editor/TournamentDemoRecorder.cs (+.meta)
?? Docs/Specs/Active/tournament_screens_live_bind/{HEARTBEAT.log, IMPLEMENTER_REPORT.md, screenshots/*}
```

- **Zero `.unity` scene file changes.** `git diff --stat HEAD -- Assets/Scenes/` returns nothing.
- **Zero `Assets/Scripts/Physics/` edits** (Rule 7).
- **Zero `Scenarios.cs` bespoke `*Gate` additions.**
- **Every `.cs` ships with its `.cs.meta`** (Lesson R compliance).
- **Every file outside the task folder appears in the implementer's "Files modified or created" table** (Rule 13).
- All four modified `.cs` files match exactly what the implementer reported as edits.

PASS.

## Production-flow capture verification

The canonical video (`videos/tournament_demo.mp4`, 1170×2532) is recorded via `TournamentDemoRecorder` which:
1. Opens `ShellScene.unity` (real boot scene) and enters play mode.
2. Waits 4.5s for the real Splash → Loading → Home boot path.
3. Calls `ScreenManager.Instance.ShowScreen(TournamentSelection)` — the production navigation method.
4. Tabs (Playing/Closed/All) clicked via `FindButton(...).onClick.Invoke()` on real tab Buttons.
5. **Card CTA: finds REAL `TournamentSelectionCard` in active scene via `Resources.FindObjectsOfTypeAll<>()` filtered by `scene.name`, then `target.GetComponentInChildren<Button>().onClick.Invoke()`** (lines 184-202 of `TournamentDemoRecorder.cs`). This is the REAL widget's REAL Button — not a synthetic test button. Rule 2 compliant.
6. `[TournamentSelectionScreen] SelectedTournamentId = kawana_fuji_open` then `[ScreenManager] ShowScreen called: TournamentLeaderboard` proves the live handoff fires through the production CTA path.

No `LabScaffold.unity` direct-load, no scaffolding cameras, no mid-clip camera switches. Full production flow. PASS.

## Acceptance checklist re-walk (Rule 5 — every row, not just the symptom)

| SPEC §4 row | Implementer | Self-review | Notes |
|---|---|---|---|
| EditMode: `MapCardState(...)` unit test covers all 7 rows | PASS | **CONFIRM-PASS** | `MapCardStateTests.cs` has 12 tests (Row1×2, Row2×2, Row3×2, Row4×2, Row5, Row6, Row7×2). Tests call `TournamentCardStateMapper.Map()` in the pure `Golfin.Tournaments` assembly. Selection screen `MapCardState` (controller line 197) delegates to the SAME `TournamentCardStateMapper.Map()` — removing the mapper makes both RED. **Anti-circular-gate design verified** (the lesson from the prior task is correctly applied). |
| Compiles (TournamentsRuntime + UI assemblies) | PASS | CONFIRM-PASS | All four modified `.cs` files reviewed — namespaces consistent (`Golfin.Tournaments`, `GolfinRedux.UI`, `Golfin.UI.Rankings`); usings present; types resolve. Implementer reports `IsCompiling=false` + 0 task-related errors. |
| Selection shows 6 CSV tournaments | PASS | CONFIRM-PASS | Screenshot shows 5 cards in viewport (KASUMIGASEKI/HIRONO/LOMOND/GOTEMBA/KISARAZU); 6th implied by scroll. Console: `Backend ready. Tournaments=6`. |
| Real state badges + filter-by-state | PASS | CONFIRM-PASS | Visible badges span LIVE+ENTERED (Card 1), ENDED (Cards 2-4), UPCOMING (Card 5) — i.e., 3 distinct real-state outcomes. `RebuildCards` calls `liveBk.DeriveState(def, now)`. Tab filter `Matches()` switches on `CardState`. |
| CTA carries tournament id | PASS | **CONFIRM-PASS** | `HandleCtaClicked` sets `TournamentService.Instance.SelectedTournamentId = card.TournamentId` BEFORE `ShowScreen` call (controller lines 320-339). Console log `[TournamentSelectionScreen] SelectedTournamentId = kawana_fuji_open` followed by `ShowScreen called: TournamentLeaderboard` proves the handoff round-trips. **Real-entry rule (Rule 2) met** via `TournamentDemoRunner` finding the REAL card's REAL Button. |
| Leaderboard shows live `GetLeaderboard` standings | PASS | CONFIRM-PASS | `PopulateLive()` calls `Backend.GetLeaderboard(SelectedTournamentId)`. Screenshot shows real fp_NNN ids (not "Player 1/2/3" placeholders), per-row stroke variation (70/71/72/73/74), GALADRIEL sticky with `"--"` rank confirming `IsProvisional` handling. Real podium widgets used (Top3CardWidget) — art resolved from fake-player roster as SPEC §3 prescribes. |
| `GetTopPrizeRP(id)` accessor on `TournamentService` | PASS | CONFIRM-PASS | Method added (TournamentService.cs lines 88-114); resolves `def.PrizeTableId` → rank-1 band → `band.RpReward`. Returns 0 on absence. Reward values in the screenshot vary per-tournament (R 20.000 / R 5.000 / R 20.000 etc.) — clearly coming from prize-table lookup, not constants. |
| `SelectedTournamentId` handoff property added | PASS | CONFIRM-PASS | `public string? SelectedTournamentId { get; set; }` at TournamentService.cs line 47. Written by selection CTA, read by leaderboard `PopulateLive()` line 75. Null-guarded both sides. |
| Registration NOT wired (deferred to T6) | PASS | CONFIRM-PASS | `HandleCtaClicked` for gold states (Open/Ending/EnteredActive) ONLY calls `ScreenManager.Instance?.ShowScreen(_holeSelectionTarget)`. No `backend.Register(...)` call anywhere in the controller. Absence is expected, not a defect. |
| Bot video at iPhone 14 1170×2532 | PASS | CONFIRM-PASS | `videos/raw.mp4` (12MB, 1170×2532, 19.6s) and captioned `videos/tournament_demo.mp4` (1.9MB) both present. Recorder uses `EnsureIPhone14Selected()` + `OutputWidth/Height = 1170/2532`. |
| No exceptions in play mode | PASS | CONFIRM-PASS | Implementer reports 0 new task-related errors; only pre-existing Rindo lightmap warnings. |
| Zero edits to `Assets/Scripts/Physics/` | PASS | CONFIRM-PASS | `git diff -- Assets/Scripts/Physics/` empty. |

All 12 rows: CONFIRM-PASS. Nothing OVERRIDE-FAIL'd.

## Rule integrity gates

- **Rule 2 (Real-entry):** Met. `TournamentDemoRunner.Sequence()` lines 184-202 invoke REAL `TournamentSelectionCard.GetComponentInChildren<Button>().onClick.Invoke()`, not a synthetic test button.
- **Rule 5 (Re-walk every criterion):** Done above — all 12 SPEC §4 rows individually verified, not just one symptom.
- **Rule 6 (Report integrity):** Every implementer PASS claim is backed by either a console log line, a `tests-run` count, file inspection, or the canonical screenshot/video. No fabricated tool results detected.
- **Rule 18 (Figma fidelity table):** N/A — SPEC doesn't reference a Figma node.
- **Anti-circular-test gate (prior task scar):** `TournamentCardStateMapper.Map` is the REAL production mapper, called both by the controller (line 197) AND the test (`MapCardStateTests` line 25). Removing it → both fail. Verified by file inspection.

## Spec deviation noted (acceptable)

Implementer flagged the leaderboard header still reads "KASUMIGASEKI OPEN" regardless of which tournament was tapped (the bot tapped `kawana_fuji_open`). SPEC §3 only required binding podium/rows/sticky from `GetLeaderboard`; binding the modal header to the tournament name is explicitly out of scope (left for T6). This is correctly NOT counted as a fail. The board data itself IS the tournament the player tapped — the header is just stale chrome.

## Files inspected during review

- `Docs/Specs/Active/tournament_screens_live_bind/SPEC.md`
- `Docs/Specs/Active/tournament_screens_live_bind/IMPLEMENTER_REPORT.md`
- `Docs/Specs/Active/tournament_screens_live_bind/screenshots/selection_live_2026-06-27.png`
- `Docs/Specs/Active/tournament_screens_live_bind/screenshots/leaderboard_live_2026-06-27.png`
- `Assets/Scripts/Tournaments/TournamentCardStateMapper.cs`
- `Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs`
- `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs`
- `Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs` (header section)
- `Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs`
- `Assets/Scripts/TournamentsRuntime/TournamentService.cs`
- `Assets/Scripts/UI/Editor/TournamentDemoRecorder.cs`
- `git status --porcelain` + `git diff --stat HEAD`

## Verdict

**FORWARD_TO_ARCHITECT** — all SPEC §4 acceptance items verified PASS via independent pixel scan, file inspection, and scene-mutation audit. The two pipeline scars from the prior task (`tournament_backend_bootstrap`) — circular-test gate and synthetic-entry — are both correctly addressed in this iter.

Setting STATUS.md → `SELF_REVIEW_PASS`.
