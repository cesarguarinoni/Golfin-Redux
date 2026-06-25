DONE

# STATUS — tournament_selection_screen (T7)

**Stages 0–1 DONE (Cesar-approved 2026-06-25).** Card + screen shipped, committed, pushed to `main`.
Kept in `Active/` (not moved to `Completed/`) because Stage 2/3 remain — see Deferred below.

## Shipped (Stages 0–1)
- **Stage 0 — card:** `TournamentSelectionCard.prefab` + `.cs`. Reuse: nested instances of the extracted
  `GoldPrimaryButton.prefab` (gold CTA) + silver `TournamentCloseButton`; RP icon + per-club course
  photos; 9-slice stadium pill (`S_PillStadium.png`). Figma 13386:1780 fidelity — gradient bg+radius,
  dark-inside/gold-outline pills (FREE ENTRY + ENTRY/RP-icon/fee + ENTERED), all-caps bold title
  (Rubik-SemiBold), status+countdown date, CTA bottom-right.
- **Conversion:** card geometry 1:1 Figma px; TMP fonts **÷1.3** (spec's ÷1.4 too small, 1:1 too big).
- **Stage 1 — screen:** cloned `RankingsScreen` → `TournamentSelectionScreen.prefab`; tabs
  ALL/OPEN/PLAYING/CLOSED; stripped Rankings leftovers (Top3 podium, user-rank card, GPS banner,
  league/reset row, back arrow); per-club course images; panel height filled to match
  Tournament Hole Selection (bottom y348 ≈ 344, ~4.5 cards); 6 static state cards.
- **Integration:** instanced into `ShellScene` under `ScreensRoot`; `ScreenManager._tournamentSelectionScreen`
  re-pointed; reachable via ModeSelection TOURNAMENTS entry. PersistentUIManager banner "TOURNAMENTS".
- **Filter tabs (Stage 1 preview):** ALL=all, OPEN=Open/Ending/Upcoming, PLAYING=entered, CLOSED=ended.
  Verified counts 6/3/2/1.

Commits: ba701518a (card) → cf3d15509 (÷1.3) → 94158b865 (screen prefab) → fc672f86d (integration)
→ 84541f206 (ENTERED + chassis cleanup) → 28ed0101c (per-club images/silver/scroll/back-arrow)
→ 6679beb05 (panel height) → + filter-tabs commit.

## Deferred (need architect / backend)
- **Stage 2 — bind `ITournamentBackend.GetTournaments()`** (real data, real filter logic against
  `TournamentState`, live countdowns via `ITournamentClock`, SIGN UP → Register RP-debit + character lock).
  **BLOCKED on T2 → T3 → T4** (T1 `tournament_contracts` done; T2-T4 not built). Architect to sequence.
- **Stage 3 — chevron expand-in-place (U1) + sign-up/character-lock modal** (`ModalController`).

## Open flags (SPEC §7) for architect
- UPCOMING/ENDED CTA behavior (currently UPCOMING CTA is a no-op stub).
- Filter semantics confirm against real `TournamentState` (preview maps Upcoming→OPEN).
