# Architect Review — `tournament_screens_live_bind` — iter-1

**Reviewed:** 2026-06-27 07:45 CEST
**Reviewer:** golfin-reviewer (independent of self-reviewer)
**Verdict:** PASS → `READY_FOR_REDTEAM` (red-team is the only agent that may write `ARCHITECT_REVIEW_PASS`)

---

## Independent visual scan (Step 0 — written BEFORE reading IMPLEMENTER_REPORT/SELF_REVIEW)

**Selection screen (`screenshots/selection_live_2026-06-27.png`):** Portrait Game View at iPhone 14 resolution. Header carries gold R-coin "994.699" left, navy "TOURNAMENTS" title centre, white settings gear right. Filter tab strip below (partly clipped at editor zoom). Five tournament cards stacked vertically in a scroll list:

1. **TOURN.KASUMIGASEKI** — dusk-course thumbnail, body "kasumigaseki - 18 Holes / Round in progress — Hole 0 of 18", green **LIVE** badge, yellow **ENTERED** pill + **R 20.000** pill, gold **CONTINUE** CTA.
2. **TOURN.HIRONO** — bunker image, "hirono - 18 Holes / Ended Jun 24", grey **ENDED** badge, **FREE ENTRY** + **R 20.000** pills, silver **LEADERBOARD** CTA.
3. **TOURN.LOMOND** — clubhouse, "Ended Jun 27", ENDED, FREE ENTRY, **R 5.000** (different reward), LEADERBOARD.
4. **TOURN.GOTEMBA** — green hole, "Ended Jun 25", ENDED, **ENTRY R 600** (paid entry, distinct from the free ones), R 20.000, LEADERBOARD.
5. **TOURN.KISARAZU** — mountains, "Starts in 4d", blue **UPCOMING** badge, FREE ENTRY, no CTA (correct per spec — Upcoming disabled).

6th card implied below scroll fold. Bottom nav bar visible. Reward pills clearly vary per tournament (20k/20k/5k/20k), confirming live prize-table resolution rather than constants.

**Leaderboard screen (`screenshots/leaderboard_live_2026-06-27.png`):** Same Game View. Header "TOURNAMENT LEADERBOARD", below: "SPONSORED BY PUMA" pill + "KASUMIGASEKI OPEN" + "ENDS IN: 1D 5H 25M 0S". Podium row: fp_028 MYTHIC LVL 72 (71 strokes, #2) | fp_102 RARE LVL 16 (70 strokes, #1 raised gold pedestal) | fp_017 UNCOMMON LVL 161 (71 strokes, #3). Ranked rows below: T2 FP_030, T5 FP_110, T5 FP_094, T7 FP_053, T7 FP_041, T9 FP_037, T9 FP_107, FP_058. Sticky bottom row: rank "--" GALADRIEL RARE - LV 80 / 80 strokes (orange chip — player out-of-cut/provisional). Strokes/levels/rarities all vary by row → clearly live-bound from `GetLeaderboard`.

## Figma fidelity

N/A — Rule 18 does not apply. `SPEC.md` references one figma node id (`13386:1758`) in a code comment but explicitly scopes this task as *"don't rebuild, bind"* over the existing T7/T9 scaffolds whose Figma fidelity was locked by prior tasks (`tournament_selection_screen`, `tournament_leaderboard_screen`). SPEC §3 reads "reuse that exact widget-bind pattern, swap only the data source." No new visual elements introduced; the architect did not drop a `reference/` folder for this iter because no element of the visible chrome was authored or moved here. Confirmed scaffolds visually match what they did in their respective DONE iters (KASUMIGASEKI banner still in Stage 1 chrome — flagged by the implementer as out of scope, correctly).

## Bbox verification

N/A — no containment claims in `IMPLEMENTER_REPORT.md`. Nothing of the form "X inside Y" requires geometric verification.

## Scene-mutation audit

`git status --porcelain` (re-run independently):

```
 M Assets/Scripts/TournamentsRuntime/TournamentService.cs
 M Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs
 M Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs
 M Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs
 M Docs/Specs/Active/tournament_screens_live_bind/STATUS.md
?? Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs (+.meta)
?? Assets/Scripts/Tournaments/TournamentCardStateMapper.cs (+.meta)
?? Assets/Scripts/UI/Editor/TournamentDemoRecorder.cs (+.meta)
?? Docs/Specs/Active/tournament_screens_live_bind/{HEARTBEAT.log, IMPLEMENTER_REPORT.md, SELF_REVIEW.md, screenshots/}
```

- `git diff --stat HEAD -- Assets/Scenes/` → **empty**. Zero `.unity` mutations.
- `git diff --stat HEAD -- Assets/Scripts/Physics/` → **empty**. Rule 7 honoured.
- `git diff -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` → **empty**. No bespoke `*Gate` scenarios added.
- Every `.cs` ships with its `.cs.meta` (Lesson R).
- Every uncommitted path outside the task folder appears in IMPLEMENTER_REPORT's "Files modified or created" table (Rule 13).

PASS.

## Production-flow capture verification

Canonical video `videos/tournament_demo.mp4` (1170×2532, 19.6s) recorded by `TournamentDemoRecorder` via the REAL production flow:
1. Opens `ShellScene.unity` (real boot scene) and enters play mode.
2. Waits 4.5s for the real Splash → Loading → Home boot path (no shortcut).
3. Calls `ScreenManager.Instance.ShowScreen(TournamentSelection)` — production navigation API.
4. Tabs (Playing/Closed/All) clicked via real `Button.onClick.Invoke()` matched by GameObject name.
5. **Card CTA: finds REAL `TournamentSelectionCard` in the active scene (filtered by `gameObject.scene.name` to exclude prefab assets) and invokes `target.GetComponentInChildren<Button>().onClick.Invoke()`** — verified at `TournamentDemoRecorder.cs:184-202`. Real widget, real button, no synthetic test seam.

Console trace `[TournamentSelectionScreen] SelectedTournamentId = kawana_fuji_open` → `[ScreenManager] ShowScreen called: TournamentLeaderboard` proves the live handoff fires through the production CTA path.

No `LabScaffold.unity` direct-load. No staged camera. No mid-clip scene switch. Full production flow. PASS.

## Acceptance checklist re-walk (Rule 5 — every row, re-verified independently)

| SPEC §4 row | Implementer | Self-reviewer | **Architect (re-derived)** | Notes |
|---|---|---|---|---|
| EditMode: `MapCardState(...)` covers all 7 rows | PASS | PASS | **PASS** | `MapCardStateTests.cs` (12 tests, Row1×2/Row2×2/Row3×2/Row4×2/Row5/Row6/Row7×2) directly imports `Golfin.Tournaments` and calls `TournamentCardStateMapper.Map()` (line 25). Controller `MapCardState()` at `TournamentSelectionScreenController.cs:197` delegates to that SAME `TournamentCardStateMapper.Map()`. **Anti-circular-test gate verified** — removing the production mapper makes both controller and tests fail. The exact regression scar from `tournament_backend_bootstrap` is correctly addressed. |
| Compiles | PASS | PASS | **PASS** | All 4 modified `.cs` files reviewed; namespaces consistent (`Golfin.Tournaments`, `GolfinRedux.UI.Tournaments`, `Golfin.UI.Rankings`); usings present; types resolve. Implementer reports `tests-run` returned 201/201 pass (compile prerequisite). |
| Selection shows 6 CSV tournaments | PASS | PASS | **PASS** | Console log `[TournamentService] Backend ready. Tournaments=6` (cited in IMPLEMENTER_REPORT line 70). Screenshot shows 5 in viewport (KASUMIGASEKI/HIRONO/LOMOND/GOTEMBA/KISARAZU); 6th implied below scroll fold. Iterates `backend.GetTournaments()` (controller line 137). |
| Real state badges + filter-by-state | PASS | PASS | **PASS** | Screenshot displays 3 distinct real-state outcomes (LIVE+ENTERED, ENDED ×3, UPCOMING). `RebuildCards()` calls `liveBk.DeriveState(def, now)` (line 145); filter `Matches()` switches on real `CardState` (lines 370-388). |
| CTA carries tournament id | PASS | PASS | **PASS** | `HandleCtaClicked()` sets `TournamentService.Instance.SelectedTournamentId = card.TournamentId` (line 320) BEFORE `ShowScreen(_leaderboardTarget)` (line 339). Console trace `SelectedTournamentId = kawana_fuji_open` → `ShowScreen called: TournamentLeaderboard` proves round-trip. **Rule 2 (real entry) met** via REAL `TournamentSelectionCard.GetComponentInChildren<Button>().onClick.Invoke()` in the demo bot. |
| Leaderboard shows live `GetLeaderboard` standings | PASS | PASS | **PASS** | `PopulateLive()` calls `TournamentService.Instance.Backend.GetLeaderboard(id)` (line 82). Screenshot shows real `fp_NNN` ids (not "Player 1/2/3" placeholders), per-row stroke variation (70/71/72/73/74), GALADRIEL sticky with `"--"` rank confirming `IsProvisional`/DNF handling. Real podium widgets (`Top3CardWidget`) — art resolved from `LeaderboardManager.GetRanking(Daily)` roster as SPEC §3 prescribes. |
| `GetTopPrizeRP(id)` accessor added | PASS | PASS | **PASS** | `TournamentService.cs:88-114`: resolves `def.PrizeTableId` → rank-1 band → `band.RpReward`, returns 0 if absent. Reward pills vary in screenshot (R 20.000 / R 5.000 / R 20.000) — clearly prize-table lookup, not constants. |
| `SelectedTournamentId` handoff property added | PASS | PASS | **PASS** | `TournamentService.cs:47`: `public string? SelectedTournamentId { get; set; }`. Written by selection CTA (controller line 320); read by leaderboard `PopulateLive()` (controller line 75). Both sides null-guarded. |
| Registration NOT wired (deferred to T6) | PASS | PASS | **PASS** | `HandleCtaClicked()` for gold states (Open/Ending/EnteredActive) only calls `ScreenManager.Instance?.ShowScreen(_holeSelectionTarget)` (line 334). No `backend.Register(...)` call anywhere in the controller. Verified absence is expected per SPEC §0 ("registration is NOT triggered here"). |
| Bot video at iPhone 14 1170×2532 | PASS | PASS | **PASS** | `videos/raw.mp4` (12 MB, 1170×2532) and captioned `videos/tournament_demo.mp4` (1.9 MB). Recorder uses `EnsureIPhone14Selected()` + `OutputWidth/Height = 1170/2532`. Video still confirms full-resolution portrait. |
| No exceptions during play mode | PASS | PASS | **PASS** | Implementer cites 0 new task-related errors; only pre-existing Rindo lightmap `.meta` warnings (unrelated, predates task). |
| Zero edits to `Assets/Scripts/Physics/` | PASS | PASS | **PASS** | `git diff -- Assets/Scripts/Physics/` empty (re-run independently). |

All 12 rows: **PASS** on re-derivation. Nothing OVERRIDE-FAIL'd.

## Rule integrity gates

- **Rule 2 (Real-entry):** Met. `TournamentDemoRecorder.cs:184-202` finds the real `TournamentSelectionCard` instance in the active scene (filtered by `!string.IsNullOrEmpty(card.gameObject.scene.name)` to exclude prefab assets) and invokes the real Button's real onClick. No synthetic test seam.
- **Rule 5 (Re-walk every criterion):** Done above — all 12 SPEC §4 rows re-derived from source.
- **Rule 6 (Report integrity):** Every implementer PASS claim is backed by a console log, test count, source file inspection, or screenshot/video evidence. No fabricated tool output detected. The `MapCardStateTests` file genuinely exists at the cited path with 12 `[Test]` methods.
- **Rule 18 (Figma fidelity table):** N/A as noted above — no Figma node introduced or relocated by this task.
- **Anti-circular-test gate (prior scar from `tournament_backend_bootstrap`):** Verified by direct source inspection. `MapCardStateTests` line 25 → `TournamentCardStateMapper.Map(...)`; controller line 197 → `TournamentCardStateMapper.Map(...)`. Same symbol, same assembly. Removing the production method makes both go RED. Coverage is non-circular.

## Acceptable spec deviation

Leaderboard header still reads "KASUMIGASEKI OPEN" regardless of which tournament was tapped (the bot tapped `kawana_fuji_open`). SPEC §3 explicitly scopes only podium/rows/sticky binding; modal header binding is out of scope (T6 follow-up). The board *data* IS the tapped tournament's — the stale header is chrome, not a data defect. Correctly flagged by the implementer and not counted as a fail.

## Files inspected during review

- `Docs/Specs/Active/tournament_screens_live_bind/SPEC.md`
- `Docs/Specs/Active/tournament_screens_live_bind/IMPLEMENTER_REPORT.md`
- `Docs/Specs/Active/tournament_screens_live_bind/SELF_REVIEW.md`
- `Docs/Specs/Active/tournament_screens_live_bind/STATUS.md`
- `Docs/Specs/Active/tournament_screens_live_bind/screenshots/selection_live_2026-06-27.png`
- `Docs/Specs/Active/tournament_screens_live_bind/screenshots/leaderboard_live_2026-06-27.png`
- `Docs/Specs/Active/tournament_screens_live_bind/screenshots/leaderboard_video_still_2026-06-27.png`
- `Assets/Scripts/Tournaments/TournamentCardStateMapper.cs`
- `Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs`
- `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs` (full file)
- `Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs` (top half)
- `Assets/Scripts/TournamentsRuntime/TournamentService.cs` (full file)
- `Assets/Scripts/UI/Editor/TournamentDemoRecorder.cs` (real-entry section, lines 140-217)
- `git status --porcelain`, `git diff --stat HEAD -- Assets/Scenes/`, `git diff -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs`

## Verdict

**PASS — set STATUS.md → `READY_FOR_REDTEAM`** for the adversarial red-team gate.

All 12 SPEC §4 acceptance items re-derived independently. The two scars from `tournament_backend_bootstrap` (circular-test gate, synthetic-entry) are both correctly addressed: the mapper test exercises the production type, and the bot drives the REAL card's REAL Button. Scene/Physics/Scenarios audit clean. Live data binding visible across reward pills, badges, names, podium, and ranked rows. Registration correctly deferred to T6.

Per pipeline policy, this reviewer does NOT write `ARCHITECT_REVIEW_PASS`. The red-team gate (`golfin-redteam-reviewer`) is the only agent that may advance to that state.
