# STAGE 0 REPORT — tournament_screens (prefabs only)

**Built by:** Claude Code (main thread, Unity MCP) — 2026-06-24
**Gate:** Cesar reviews/edits each prefab before Stage 1 (per SPEC §3). Committed, **NOT pushed**.

## What was built — 7 prefabs in `Assets/Prefabs/UI/Tournaments/`

Every prefab is a **duplicate** of its SPEC §1 source (`AssetDatabase.CopyAsset`), then the per-state diff was applied via `PrefabUtility.LoadPrefabContents` — never rebuilt. The only from-scratch element is B4 (SPEC §0.1).

| # | Prefab | Duplicated from | Diff applied (placeholder data baked) |
|---|---|---|---|
| A1 | `TournamentHoleCard_Finished` | `HoleSelection/HoleCard.prefab` | Expanded state on; `TitleExp`→**FINISHED** green `#50C878`; lock/chevron off; rewards+PLAY+inner dividers off; **grafted `StatsBlockText` node from `HoleComplete/HoleCompleteWidget.prefab`** → `TEE OFF: REGULAR / STROKES: 4 (PAR)`(green)`/ TIME: 00:02:34 / RANK: #7` |
| A2 | `TournamentHoleCard_Next` | `HoleCard.prefab` | Native expanded state; `TitleExp`→**NEXT** gold `#EDDB9E`; lock off; strategy-tip placeholder; gold **PLAY** kept |
| A3 | `TournamentHoleCard_Locked` | `HoleCard.prefab` | Collapsed title-only bar; `Title`→**LOCKED** grey `#C8C8C8`; lock icon on; `LockedOverlay` darken on; collapsed divider+rewards off |
| A4 | `TournamentCloseButton` | silver `ReplayButton` instance inside `HoleCompleteWidget.prefab` | Label→**CLOSE**; size 308×120 (Silver-Small); silver `Button - Replay` sprite + `ButtonPressFeedback` kept |
| B2 | `TournamentRankingRow` | `Rankings/RankingsCards.prefab` | RP pill → **STROKES pill**: coin `Icon` off, bg `#001E39`, `"72 STROKES"` fs24 centered |
| B3 | `TournamentPlayerStickyRow` | `Rankings/RankingsCardUser.prefab` | STROKES pill `"80 STROKES"` (no coin); Rank→`--`; **LIVE badge** added (`#C04000`, "LIVE" bold 14 white, top-right) — gold border native to source |
| B4 | `TournamentLeaderboardEmptyState` | **NEW (only from-scratch element)** | Card-color panel `#133453`, VLG centered, title **"No finishers yet"** (32) + muted body (21, `#AAB6C8`) |

## Verification
- `git status` confirms all 14 `.prefab`/`.meta` files landed (no synthetic claims).
- Full hierarchy re-dump confirms every diff above (texts, colors, active states, no-coin pills).
- Both prefabs render standalone — see `screenshots/stage0_screenA_holecards.png` and `screenshots/stage0_screenB_leaderboard.png` (rendered via a throwaway additive capture scene; the open scene was left untouched).
- `ButtonPressFeedback` present on every button (A4 inherited it from `ReplayButton`; A2 PLAY + card tap buttons inherited from `HoleCard`).

## Known Stage-0 simplifications (faithful to "duplicate, minimal diff" — flag for Cesar)
1. **Badges are colored title TEXT, not pill backgrounds.** HoleCard's native badge is colored text, so FINISHED/NEXT/LOCKED follow that. The Figma pill badges (206×60 etc.) are a Stage-1 fidelity refinement.
2. **A1 has the hole map only** (`HoleImage`), not the separate small "green thumb" (94×94.9) the Figma shows alongside the map. Grafting the stat block was the §1 mandate; the second thumbnail is a Stage-1 add.
3. **A2 keeps the native reward slots** (`x10` placeholders) per "native HoleCard styling, no changes beyond badge + PLAY." Drop in review if tournament NEXT cards shouldn't show rewards.
4. Placeholder character/portrait/hole data is the source prefabs' bundled placeholder (GALADRIEL/Shae/Hole_01) — real data binds in Stage 2.

## Cesar review r1 — applied 2026-06-24
- **A1** OK · **A3** OK · **A4** OK (no change).
- **A2** — removed the reward slots (`RewardsRowExp`) + one separator (`Divider (2)`), leaving a single divider before PLAY; PLAY label de-bolded (`FontStyles.Bold`→`Normal`, keeps the design's `Rubik-SemiBold`).
- **B2** — STROKES pill color reverted from `#001E39` to native `#FFFFFF` (the `RPContainer` sprite's own color = identical to the normal rankings pill). Spec §2's `#001E39` token overridden by Cesar.
- **B3** — same pill fix applied (same component; kept consistent with B2) + all row text forced `UpperCase` and the `" - Lv 80"` literal capitalized to `" - LV 80"`.
- **B3 LIVE badge → pill** (Cesar follow-up): was a hard rectangle (sprite-less Image). Generated a reusable **white** stadium sprite `Assets/Art/Tournaments/S_PillWhite.png` (9-slice border = half-height, tintable to any color), assigned it Sliced to the LIVE badge with `pixelsPerUnitMultiplier=4`, tinted `#C04000`. The project's existing pill sprites (`RPContainer`, `LevelUpPillBackground`) are dark navy and won't tint to bright red, hence the new white pill. (Swap to the literal Figma `#C04000` export on request — visually identical.)

## NOT done this stage (correct per §3 guardrails)
No screen wiring, navigation, backend, or runtime population. Podium (B1) is not a Stage-0 prefab — it lives inside `RankingsScreen.prefab` and is a Stage-1 screen-scaffold concern.
