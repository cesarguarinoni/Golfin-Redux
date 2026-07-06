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

**Round 1:**
1. **Close buttons inside the panels** (not footers below): hole Close flows after the LOCKED card inside the cards scroll; leaderboard Close is the last card-slot in the ranking list (no row overlap).
2. **Identity row reuses the rankings filter pills** (`BackgroundLeague` sprite) instead of a custom bar — set to **Sliced** with a 9-slice border (`BackgroundLeague.png.meta` border 70/0/70/0) so they don't stretch/break.
3. **"TOURNAMENT LEADERBOARD" moved to the top** instead of floating in the screen body.

**Round 2 — aligned to Cesar's `SELECT HOLE` mockup:**
4. **Real persistent nav bars now shown** on both tournament screens (currency + settings gear + gold banner title + bottom nav). `ScreenManager` adds the two tournament ScreenIds to `showBars`; `PersistentUIManager.HighlightScreen` sets the top-bar center title — **"SELECT HOLE"** / **"TOURNAMENT LEADERBOARD"** — and highlights the bottom-nav tee button. The floating title is gone (it now lives in the nav bar, matching the mockup).
5. **Identity pills are now 2 rows:** full-width **"SPONSORED BY PUMA"** on top; **`KASUMIGASEKI OPEN`** + **`ENDS IN: 1d 5h 25m 05 s`** below. (Timer now carries the `ENDS IN:` label.)
6. **Scroll bars set to AutoHide** — only visible when the list actually overflows (hidden on the hole screen where the cards fit).
7. **Podium Top-3 STROKES on one line** (`NoWrap` + autosize) — was wrapping to two lines.

**Round 3 — spacing/podium fidelity to the Figma renders (`reference/*.png`, nodes 13414-2936 / 13414-5598):**
8. **24px gap between the top pills and the content panel** on both screens — hole screen was overlapping (pills rect was 84px holding 122px of content → resized to 124px so the Content VLG's 24px spacing applies); leaderboard gap was too big (banner spacer 224→198 so the panel sits 24px below the pills).
9. **Leaderboard panel sized like the normal rankings screen** — `RankingsArea` grown 1285→1553 (+ scroll viewport `Bottom97` 776→1028) so the panel fills the screen, and the **player's sticky card moved lower** to just above the bottom nav (matching the normal RankingsScreen / Figma).
10. **Podium Top-3 hierarchy effect** (same as the normal rankings screen) — #1 full scale, **#2/#3 scaled 0.85** with bottom-center pivot.
11. **Podium STROKES centered** in the pill (was right-aligned).
12. Leaderboard list filled to **10 rows (ranks 4–13)** so the taller panel reads like the Figma instead of half-empty (placeholder data; real rows bind in Stage 2).

**Round 4 — leaderboard final polish:**
13. **24px gap between the panel and the sticky card** (was 8px) — panel's bottom raised 16px (RankingsArea 1553→1537 + scroll viewport), keeping the 24px top gap.
14. **Podium STROKES truly centered** — the NameLabel was left-anchored at x=57 (vestigial coin offset) spanning only 182 of the 250px pill; stretched it full-width so the centered text actually centers in the pill.
15. Ranking-row numbers corrected to a clean **4–13** sequence (earlier pass had left the first five at "4").

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
