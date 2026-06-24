# Tournament Screens — Stage 1 (scaffolds + nav) — DONE 2026-06-24

Spec: `Docs/Specs/Active/tournament_screens/SPEC.md` §3 Stage 1. Static/placeholder only, no backend.
Report: `Docs/Specs/Active/tournament_screens/STAGE1_REPORT.md`.

## Plan — all complete
- [x] ScreenManager: `TournamentHoleSelection` + `TournamentLeaderboard` ScreenIds + screen fields + ApplyScreen/music wiring
- [x] New controllers `Assets/Scripts/UI/Tournaments/` (HoleSelection + Leaderboard nav-only) + `TournamentDevEntryButton`
- [x] Compile-check (clean)
- [x] `TournamentHoleSelectionScreen` (dup of HoleSelectionScreen): identity pills, 3 hole cards, podium-icon → Leaderboard, Close inside panel
- [x] `TournamentLeaderboardScreen` (dup of RankingsScreen): identity pills + title (top), podium RP→STROKES, 5 rows, sticky LIVE row, empty-state (inactive), Close as last list card
- [x] Temp entry on ModeSelectionScreen → TournamentHoleSelection (placeholder for T7)
- [x] Nav verified in play mode (real ScreenManager + fade): Selection → Hole ⇄ Leaderboard + Close-backs
- [x] Screenshots @ iPhone 14 1170×2532 → `screenshots/stage1_{hole_selection,leaderboard}.png`
- [x] Implementation Plan: inserted **T8b** tournament_hole_selection_screen before T9
- [x] STAGE1_REPORT.md + STATUS.md updated
- [x] Commit (scoped, NOT pushed)

## Cesar in-session corrections (applied)
- [x] Close buttons inside the content panels (not footers below)
- [x] Identity row reuses rankings filter pills (BackgroundLeague), set to Sliced 9-slice
- [x] "TOURNAMENT LEADERBOARD" moved to the top (nav-bar header), not floating

## Flags for Cesar (in STAGE1_REPORT.md)
- "Selection" = ModeSelectionScreen; temp entry button added (remove when T7 ships)
- Filters disabled on tournament hole selection; leftover rankings InfoArea hidden on leaderboard
- "Drop Arrows" was a no-op (Figma-only)
- BackgroundLeague.png.meta gained a 9-slice border (shared rankings sprite; Simple usages unaffected)
