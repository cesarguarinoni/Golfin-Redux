# STAGE 1 REPORT — tournament_screens (screen scaffolds + nav)

**Built by:** Claude Code (main thread, Unity MCP) — 2026-06-24
**Scope:** SPEC §3 Stage 1 — both full screens, identity-pill row, scroll containers, Stage-0 prefabs instantiated in place, podium-icon → Leaderboard, silver Close buttons, `Selection → Hole Selection → Leaderboard` nav + back. Static/placeholder data only (no backend — that's Stage 2).
**Committed, NOT pushed** (awaiting Cesar confirm).

Canonical screenshots (iPhone 14, 1170×2532, real `ScreenManager.ShowScreen` nav path, over a loaded hole):
- `screenshots/stage1_hole_selection.png`
- `screenshots/stage1_leaderboard.png`

---

## What was built

Both screens are **duplicates** of their reuse-map source (SPEC §1), never rebuilt:

| New screen | Duplicated from (in-scene) | ScreenId |
|---|---|---|
| `TournamentHoleSelectionScreen` | `HoleSelectionScreen` (scene GO) | `TournamentHoleSelection` |
| `TournamentLeaderboardScreen` | `RankingsScreen` (scene GO) | `TournamentLeaderboard` |

Both live under `Canvas/ScreensRoot` and are toggled by `ScreenManager` like every other screen.

### Tournament Hole Selection
- Own background (inherited from HoleSelection). Course/tee **Filters disabled** (not in Figma Screen A — flag below).
- **Identity-pill row** (sponsor · league/tournament · timer) — a row of pills reusing the rankings pill sprite (`BackgroundLeague`): `[PUMA] [GOLFIN OPEN · DIAMOND LEAGUE] [02:14:33]`.
- **Stage-0 prefabs in place** in the cards scroll: `TournamentHoleCard_Finished`, `_Next`, `_Locked` (one of each state).
- **Podium-icon** (reused `LeaderboardButton`) → Tournament Leaderboard.
- **Silver Close** (`TournamentCloseButton`) **inside the cards panel**, after the LOCKED card (flows in the scroll VLG). → back to Selection.
- Strips `HoleSelectionScreenController` (it rebuilds DB cards on enable and would wipe the static tournament cards); replaced with the nav-only `TournamentHoleSelectionScreenController`.

### Tournament Leaderboard
- Own background (inherited from Rankings). **Period tabs (TabBar) disabled**; leftover rankings **InfoArea (league + league-reset) hidden** (redundant rankings artifact — see flag).
- **"TOURNAMENT LEADERBOARD" title** at the top (nav-bar header position) + the shared identity-pill row below it.
- **Podium Top-3** reused from RankingsScreen with **RP pill → STROKES** (no coin): #1 `69`, #2 `72`, #3 `75 STROKES`.
- **Ranking rows** = `TournamentRankingRow` ×5 in the scroll list (STROKES pill, no coin).
- **Sticky "you" row** = `TournamentPlayerStickyRow` (gold border, rank `--`, `80 STROKES`, LIVE badge) replacing `RankingsCardUser`.
- **Empty-state** `TournamentLeaderboardEmptyState` placed inactive in the list area (Stage 2 toggles it when no finishers).
- **Silver Close** as the **last card in the scroll list** (centered in a grid slot, scrolls with the rows, no overlap). → back to Hole Selection.
- Strips `RankingsScreenController` (period tabs / countdown / DB rebuild); replaced with the nav-only `TournamentLeaderboardScreenController`.

### Navigation (verified in play mode via real `ScreenManager` + fade)
```
ModeSelection ──(temp entry)──▶ TournamentHoleSelection ──(podium-icon)──▶ TournamentLeaderboard
      ▲                               │  ▲                                         │
      └────────(Close)────────────────┘  └──────────────(Close)───────────────────┘
```
- `ScreenManager`: new `TournamentHoleSelection` / `TournamentLeaderboard` ScreenIds + screen fields + ApplyScreen wiring; both kept out of the persistent-bars set (the identity pills are the top chrome) but in the menu-music set.
- All three buttons fire `ScreenManager.ShowScreen` through the real controllers' runtime listeners; confirmed post-fade screen landings in play mode (e.g. Leaderboard Close → `TournamentHoleSelection`).

---

## Corrections applied after Cesar review (this session)
1. **Close buttons inside the panels** (not footers below): hole Close flows after the LOCKED card inside the cards scroll; leaderboard Close is the last card-slot in the ranking list (no row overlap).
2. **Identity row reuses the rankings filter pills** (`BackgroundLeague` sprite) instead of a custom bar — set to **Sliced** with a 9-slice border (`BackgroundLeague.png.meta` border 70/0/70/0) so they don't stretch/break.
3. **"TOURNAMENT LEADERBOARD" moved to the top** (header/nav-bar position) instead of floating in the screen body.

---

## Flags / decisions for Cesar
- **"Selection" = ModeSelectionScreen.** The real upstream Tournament Selection screen is Implementation-Plan **T7** (not built). A clearly-labelled **temporary** entry button (`TOURNAMENTS (TEMP)`, `TournamentDevEntryButton`) was added to ModeSelectionScreen so `Selection → Hole Selection` is exercisable now. **Remove when T7 lands.** Hole Close currently returns to `ModeSelection` (placeholder for T7).
- **Filters disabled** on Tournament Hole Selection (course/tee pills aren't in Figma Screen A; a tournament has a fixed hole set).
- **Leftover rankings InfoArea hidden** on the leaderboard (league name + league-reset countdown) — redundant with the identity pills and conceptually a rankings (not tournament) artifact. Easy to re-enable if wanted.
- **"Drop Arrows" was a no-op in Unity** — the left/right Arrows are Figma-only; no Arrow GameObjects exist in either source screen.
- **`BackgroundLeague.png.meta`** gained a 9-slice border (needed for the reused pills). Original rankings League/Reset pills use the sprite as `Simple`, so they're visually unaffected.
- Placeholder data only (FRODO/GALADRIEL/Lomond Hole 1) — real data binds in **Stage 2**.

## Not done this stage (correct per §3)
No backend / `LocalTournamentBackend` binding, no live leaderboard, no entry-progress-driven card states, no empty-state toggling, no finished-card RANK from real tournament rank. All Stage 2.
