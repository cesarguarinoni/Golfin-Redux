REDO_READY

# STATUS — tournament_selection_screen (T7)

> **2026-06-25 — REDO scoped.** iter-1 was stopped by Cesar: it **rebuilt the screen + buttons from scratch** instead of cloning the chassis / reusing the shared buttons (violates SPEC §0/§1, now PIPELINE_HARDENING **Rule 8**). Diagnosis = **both** spec gaps + Code violation (full record: `ARCHITECT_HANDOFF.md`).
> **Architect has now closed the gaps** — `SPEC.md` rewritten card-first with every reuse row pinned to a concrete on-disk handle; salvage list explicit; clone-provenance now gate-enforced. Decisions locked by Cesar: **(1) salvage** Code's sound card C# + nav edits, **(2) card-first** (build the card prefab, then the screen).

**Task:** Tournament Selection screen ("TOURNAMENTS") from Figma `13386:1758` (v7) — browse/pick hub: 4 filter tabs (ALL/OPEN/PLAYING/CLOSED) + scrollable tournament cards across six states, state-driven CTA (SIGN UP / CONTINUE / LEADERBOARD / ENTRY-fee), expand-in-place sign-up (GDD U1).

**Tier:** FULL PIPELINE. **Updated:** 2026-06-25 (redo scoped — ready to re-dispatch).

## Redo plan (SPEC §6 — card-first)
- [ ] **Stage 0a** — extract `Assets/Prefabs/UI/Common/GoldPrimaryButton.prefab` from in-scene `PlayButton` (`ShellScene.unity`, fileID `4123466008247632389`).
- [x] **Stage 0b — DONE (Architect):** 6 course images exported on disk → `Assets/Art/Tournaments/CourseImages/` (`lomond/gotemba/hirono/kasumigaseki/kisarazu.png` + `kawana.jpg`; kisarazu 260×212 + kawana 980×517 cover-fit). **Code's 0b = import as Sprite + assign per card** (SPEC §8 mapping).
- [ ] **Stage 0c** — `TournamentSelectionCard.prefab` (THE focus): nested instances of GoldPrimaryButton + silver `TournamentCloseButton`, RP icon + course-image sprites, badge pill, §3 tokens, per-state visuals; **salvage `TournamentSelectionCard.cs`**. Render standalone → **Cesar visual gate on the CARD.**
- [ ] **Stage 1** — duplicate `RankingsScreen.prefab` → rename `TournamentSelectionScreen` → relabel 4 tabs → instantiate card prefabs (one per state, static) → nav + keep Code's ScreenManager/PersistentUIManager/entry edits. **Cesar visual gate on the SCREEN.**
- [ ] **Stage 2** — bind `ITournamentBackend.GetTournaments()`. **Blocked on T1→T4.**
- [ ] **Stage 3** — expand-in-place (U1) + sign-up modal.

## Salvage (kept from iter-1 working tree)
`TournamentSelectionCard.cs` (state/badge/bind logic) · `ScreenManager.ScreenId.TournamentSelection` · `PersistentUIManager` banner+showBars · `TournamentDevEntryButton` route · `TournamentHoleSelectionScreenController` back-target. **Discard:** bespoke screen scaffold + hand-rolled CTA.

## Concrete reuse handles (verified on disk 2026-06-25) — see SPEC §1
Chassis → `Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` (4-tab row + scroll + panel) · Gold CTA → extract from `PlayButton` · Silver CTA → `Assets/Prefabs/UI/Tournaments/TournamentCloseButton.prefab` · RP icon → `Assets/Art/HomeScreen/Reward Points Icon.png` · Course images → Figma export.

## Decisions to resolve (SPEC §7)
- Chassis pick (RankingsScreen.prefab vs in-scene TournamentHoleSelection) — implementer confirms + cites provenance.
- UPCOMING / ENDED CTA behavior · `PLAYING` vs GDD `Active` (Figma canonical) · EN name font.

## Key node IDs
Screen root `13386:1758` · Filter strip `13386:1761` (tabs 1763/1767/1771/1775) · Cards: Kasumigaseki `13389:1884`, Hirono `13405:1858`, Lomond `13386:1780`, Gotemba `13386:1804`, Kisarazu `13386:1828`, Kawana `13389:1849`.

## Dependency
Stages 0–1 independent (static). Stage 2 needs **T1✓ → T2 → T3 → T4** (`GetTournaments()`).
