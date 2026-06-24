# STAGE 0 — Code Kickoff (paste into a fresh Claude Code session)

> Paste everything below the line into Claude Code on the Mac. Pull first: `git pull` in `/Users/cesar/Documents/GolfinRedux`.

---

You are the **Implementer** (Claude Code) on GOLFIN Redux. Full repo access. This is **Stage 0 of the Tournament Screens build — PREFABS ONLY.**

**Read first (authoritative):**
- `Docs/Specs/Active/tournament_screens/SPEC.md` — read §0 (rules), §1 (reuse map), §2 (tokens), §4 (per-prefab geometry + diffs). Read §5 flags.
- `Docs/Game Design/Tournaments_GDD.md` **§17 Addendum** — the locked rulings.

**THE ONE RULE THAT MATTERS MOST (SPEC §0.1):**
**REUSE, don't recreate.** Every prefab below is a DUPLICATE of an existing Unity prefab/component, modified only by its stated diff. **Never rebuild a hierarchy that already exists.** Example: the leaderboard podium is reused as-is from `RankingsScreen.prefab` — only the RP pill becomes STROKES. The **only** from-scratch asset in all of Stage 0 is the empty-state message. If you think you need to build something fresh, stop and re-check the source prefab first.

**Scope — Stage 0 produces these prefabs ONLY.** No screen wiring, no navigation, no backend, no runtime logic this stage. Static prefabs with **placeholder data baked in** so Cesar can open and edit each one directly in the editor. Output to a new folder `Assets/Prefabs/UI/Tournaments/`. Commit each as a real `.prefab`.

| Prefab (new) | Duplicate this | Diff to apply |
|---|---|---|
| `TournamentHoleCard_Finished.prefab` | `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab` + graft the stat block from `Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab` | FINISHED badge (green `#50C878`); result text "TEE OFF / STROKES: n (PAR green) / TIME / RANK: #N"; arrow hidden; delete the "DOWNLOAD SIZE" node |
| `TournamentHoleCard_Next.prefab` | `HoleCard.prefab` | NEXT badge (gold) + gold PLAY button + strategy-tip placeholder |
| `TournamentHoleCard_Locked.prefab` | `HoleCard.prefab` | Darken overlay + lock icon + LOCKED (grey `#C8C8C8`); collapse to 164px |
| `TournamentRankingRow.prefab` | `Assets/Prefabs/UI/Rankings/RankingsCards.prefab` | RP pill → `"{n} STROKES"`, **drop the coin** |
| `TournamentPlayerStickyRow.prefab` | `Assets/Prefabs/UI/Rankings/RankingsCardUser.prefab` | RP → `"{n} STROKES"` (no coin); rank text `--`; add LIVE badge (`#C04000`, r22, "LIVE" Bold 20 white) |
| `TournamentCloseButton.prefab` | Main Buttons component → variant **Silver-Small Enabled=Yes** | label "CLOSE" |
| `TournamentLeaderboardEmptyState.prefab` | **NONE — build from scratch** (only fresh asset) | title "No finishers yet" / body "Be the first to complete every hole and top the board." |

(The **podium** is NOT a Stage 0 prefab — it lives inside `RankingsScreen.prefab` and is reused in place when the leaderboard screen is cloned in Stage 1, swapping its RP pill → STROKES. Do not extract or rebuild it now.)

**Conventions (SPEC §0, §2):**
- TMP font size = Figma px ÷ 1.4 (Subhead 45→32, Footnote 39→28, Caption_2 33→24, Caption_3 30→21, LIVE 20→14). Gaps/padding = multiples of 8.
- STROKES pill: bg `#001E39`, radius 50, padding 16, `"{n} STROKES"` Rubik Medium white, **no coin**. RP coin (hash `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`) is for prizes/fees ONLY — never on strokes.
- Use real Main Buttons component instances (swap variant via swapComponent, not setProperties).
- Each Figma node is linked in SPEC §4 — pull exact fills with Figma MCP if needed.

**Done when:** all 7 prefabs exist under `Assets/Prefabs/UI/Tournaments/`, each opens and renders standalone in the editor with placeholder data, no console errors. Verify with `git status` that the `.prefab` files actually landed (not just meta). Commit with a timestamped JST message; do not push until Cesar confirms the prefabs look right. Report the actual file list — no synthetic/fabricated claims.

**Do not:** wire screens, add navigation, touch the backend, or build any runtime population this stage. Flag anything ambiguous back to Cesar instead of guessing.
