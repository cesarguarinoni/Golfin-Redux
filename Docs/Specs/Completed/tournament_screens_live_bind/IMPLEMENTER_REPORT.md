# Implementer Report — `tournament_screens_live_bind`

**Iteration shape:** tournaments-live-bind:clean-start

## Implementation summary

Bound the existing Tournament Selection and Leaderboard screen scaffolds to the live `ITournamentBackend` (via `TournamentService.Instance.Backend`). Selection now iterates `GetTournaments()` / `DeriveState()` / `GetMyEntry()` and maps each to a `CardState` via a pure static `TournamentCardStateMapper.Map()` function (EditMode-tested, all 7 SPEC §2 rows). Leaderboard replaces the bot stubs with `GetLeaderboard(SelectedTournamentId)` and binds each `TournamentLeaderboardEntry` to the existing `Top3CardWidget` / `RankingsCardWidget` widgets. Added `SelectedTournamentId` handoff and `GetTopPrizeRP()` accessor to `TournamentService` for T6 reuse.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/TournamentsRuntime/TournamentService.cs` | Modified — added `SelectedTournamentId` property, `_prizeTables` cache, `GetTopPrizeRP(id)` accessor; `Awake()` now calls `loader.LoadPrizeTables()` before `Compose()` |
| `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs` | Modified — full Stage 2 rewrite of `RebuildCards()`: iterates live backend, maps card state via `TournamentCardStateMapper`, adds `TournamentImageEntry` serialized map, `BuildDateLine()`, `ResolveSprite()`, real filter tab logic |
| `Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs` | Modified — added `TournamentId { get; private set; }` property; `BindStatic()` gains optional `string? tournamentId` param |
| `Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs` | Modified — replaced `PopulateBots()` with `PopulateLive()`: reads `GetLeaderboard(SelectedTournamentId)`, binds podium/rows/sticky from live `TournamentLeaderboardEntry` data |
| `Assets/Scripts/Tournaments/TournamentCardStateMapper.cs` | Created — pure `TournamentCardState` enum + `TournamentCardStateMapper.Map(state, entryStatus, nowPastEnd)` in `Golfin.Tournaments` assembly (EditMode-testable) |
| `Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs` | Created — 12 NUnit EditMode tests covering all 7 SPEC §2 rows |
| `Assets/Scripts/UI/Editor/TournamentDemoRecorder.cs` | Created — bot demo recorder following `RankingsDemoRecorder` pattern; menu `GOLFIN > Tournaments > Record Demo Video`; captures 1170×2532 @ 30fps |

## Screenshot

- Canonical screenshot: `screenshots/selection_live_2026-06-27.png`
- Captured at: `screenshots/selection_live_2026-06-27.png` (2070×1912 — long edge 2070px ≥ 900px)
- Supporting: `screenshots/leaderboard_live_2026-06-27.png` (2070×1912)
- Supporting still from video: `screenshots/leaderboard_video_still_2026-06-27.png` (1170×2532)
- Scene loaded: `Assets/Scenes/ShellScene.unity`
- Play mode: Yes
- Hole loaded: N/A (UI-only task)

## Canonical video

Canonical video: `videos/tournament_demo.mp4`

- Raw: `videos/raw.mp4` — 1170×2532, 19.6s, H.264+AAC, 12MB
- Captioned: `videos/tournament_demo.mp4` — same resolution, 1.9MB (compressed), captioned via ffmpeg textfile= idiom
- Sequence recorded: boot → Logo → Splash → Home (4.5s) → TournamentSelection (All tab, 6 live cards, 3s) → Playing tab (2.5s) → Closed tab (2.5s) → All tab → tap first Ended card's real CTA → TournamentLeaderboard (3.5s) → back → exit play mode

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| EditMode: `MapCardState(...)` unit test covers all 7 rows | PASS | `tests-run` output: 201/201 pass in `Golfin.Tournaments.Tests`; MapCardStateTests contributes 12/12 (Row1 ×2, Row2 ×2, Row3 ×2, Row4 ×2, Row5, Row6, Row7 ×2); verified via `mcp__ai-game-developer__tests-run` after exiting play mode |
| Compiles (TournamentsRuntime + UI assemblies) | PASS | `console-get-logs(Error)` after each C# edit returned 0 task-related compile errors; `IsCompiling=false` confirmed before play mode |
| Selection screen shows 6 CSV tournaments | PASS | Console log `[TournamentService] Backend ready. Tournaments=6` at play-mode boot; 6 cards populated in `screenshots/selection_live_2026-06-27.png` |
| Real state badges, real filter-by-state | PASS | `RebuildCards()` calls `backend.DeriveState(def, DateTime.UtcNow)` and maps via `TournamentCardStateMapper.Map()`; filter tabs drive `Matches()` against live `CardState` derived from real states |
| CTA carries the tournament id (`SelectedTournamentId` set before nav) | PASS | Console log at bot tap: `[TournamentSelectionScreen] SelectedTournamentId = kawana_fuji_open` then `[ScreenManager] ShowScreen called: TournamentLeaderboard` — handoff fires through REAL card CTA `btn.onClick.Invoke()` (Rule 2 compliant) |
| Leaderboard shows live `GetLeaderboard` standings | PASS | `PopulateLive()` calls `TournamentService.Instance.Backend.GetLeaderboard(SelectedTournamentId)`; screenshot `leaderboard_live_2026-06-27.png` shows live bot rankings with strokes data and GALADRIEL sticky row with `"--"` rank (IsProvisional) |
| `GetTopPrizeRP(id)` accessor added to `TournamentService` | PASS | Method added; resolves prize table from `_prizeTables` cache (loaded in Awake from `loader.LoadPrizeTables()`); returns rank-1 band's `RpReward`, 0 if absent |
| `SelectedTournamentId` handoff property added to `TournamentService` | PASS | `public string? SelectedTournamentId { get; set; }` added; set in `HandleCtaClicked()` before nav call; read by `TournamentLeaderboardScreenController.PopulateLive()` |
| Registration NOT wired (deferred to T6) | PASS | `HandleCtaClicked()` for gold states (Open/Ending/EnteredActive) calls `ShowScreen(HoleSelection)` only — no `backend.Register()` call |
| Real-entry rule (Rule 2): CTA driven through REAL widget `onClick` | PASS | `TournamentDemoRunner.Sequence()` finds the REAL `TournamentSelectionCard` component in the scene hierarchy and calls `card.GetComponentInChildren<Button>().onClick.Invoke()` — no synthetic/test-only button |
| Bot-recorded video at iPhone 14 1170×2532 | PASS | `ffprobe` confirms `videos/raw.mp4` = 1170×2532 H.264, 19.6s, 12MB; captioned version at `videos/tournament_demo.mp4` = 1.9MB; `[TournamentDemo] Recording → ... (1170x2532 @ 30fps)` logged |
| No exceptions during play mode | PASS | `console-get-logs(Error)` showed 0 new errors related to this task; only pre-existing `.meta` invalid-GUID warnings from Rindo Course lightmaps (unrelated) |
| Zero edits to `Assets/Scripts/Physics/` (Rule 7) | PASS | `git diff HEAD -- Assets/Scripts/Physics/` = 0 lines |

## Known FAIL items

None.

## Spec deviations

- **Leaderboard header "KASUMIGASEKI OPEN":** The leaderboard modal's title text still shows the Stage 1 static string. SPEC §3 only required binding podium/rows/sticky from `GetLeaderboard` — binding the tournament name into the header is not in scope. Left as pre-existing Stage 1 scaffold text for T6 to address.

## Console output

Relevant logs from play mode run (task-generated; no new errors):

```
[TournamentService] Backend ready. Tournaments=6
[TournamentDemo] Recording → Docs/Specs/Active/tournament_screens_live_bind/videos/raw.mp4 (1170x2532 @ 30fps)
[ScreenManager] ApplyScreen: TournamentSelection
[TournamentDemo] Tapping LEADERBOARD on 'kawana_fuji_open'
[TournamentSelectionScreen] SelectedTournamentId = kawana_fuji_open
[ScreenManager] ShowScreen called: TournamentLeaderboard (current: TournamentSelection, instant: False)
[ScreenManager] ApplyScreen: TournamentLeaderboard
[LocalFakeLeaderboardProvider] Loaded 120 fake players.
```

Pre-existing warnings (unrelated to this task):
- `Assets/Scenes/RindoCourseHole09/...lightmap...meta` — invalid GUID (pre-existing, from Rindo Course lightmap assets)

## Open questions for Architect

None.
