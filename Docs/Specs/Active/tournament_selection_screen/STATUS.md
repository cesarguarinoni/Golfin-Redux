# STATUS — tournament_selection_screen (T7)

**Task:** Build the Tournament Selection screen ("TOURNAMENTS") from Figma `13386:1758` (v7) — the browse/pick hub: filter tabs (ALL/OPEN/PLAYING/CLOSED) + scrollable tournament cards across six states, state-driven CTA (SIGN UP / CONTINUE / LEADERBOARD / ENTRY-fee), expand-in-place sign-up (GDD U1). Delivered prefab-first in stages.

**Tier:** FULL PIPELINE (new screen + visual fidelity + state matrix).

**Updated:** 2026-06-25 JST

## Progress
- [x] Figma frame confirmed `13386:1758` ("Tournament Selection v7"); full get_metadata geometry (filter strip, 6 cards 978×360, all node IDs).
- [x] Card tokens extracted (get_design_context, online) for the canonical Lomond OPEN card `13386:1780` — gradient/border/radius, badge, eyebrow/name/club fonts, FREE-ENTRY pill, RP amount, gold Sign-Up button.
- [x] Reuse sources grounded: HoleSelection scaffold (clone, as `tournament_screens` did), Rankings TabBar pattern, shared gold primary button, silver `TournamentCloseButton`, RP icon, `PersistentUIManager`, `ModalController`.
- [x] Reference render saved → `reference/tournament_selection_screen.png`.
- [x] SPEC authored — layout + node links, literal tokens (§3), per-state matrix (§4), nav + new `ScreenId.TournamentSelection` (§5), 4-stage plan (§6), flags (§7). **Stages 0–1 ready for Code handoff now (no backend).**
- [ ] Stage 0 — card state prefabs + 4-tab TabBar (extract per-state badge hex). Implementer.
- [ ] Stage 1 — static screen scaffold + nav + replace ModeSelection TEMP entry. **Cesar visual gate.**
- [ ] Stage 2 — bind `ITournamentBackend.GetTournaments()` (state-driven CTA + filters + countdowns + Register flow). **Blocked on T1→T4.**
- [ ] Stage 3 — expand-in-place (U1) + sign-up/character-lock modal + polish.

## Decisions to resolve (SPEC §7)
- UPCOMING / ENDED CTA behavior (disabled / Notify / LEADERBOARD).
- Filter semantics per `TournamentState`.
- Tab label `PLAYING` vs GDD `Active` → Figma canonical; reconcile GDD.
- Name font Noto Sans JP Bold for EN names.

## Key node IDs
- Screen root `13386:1758` · Filter strip `13386:1761` (tabs 1763/1767/1771/1775) · Cards: Kasumigaseki `13389:1884`, Hirono `13405:1858`, Lomond `13386:1780`, Gotemba `13386:1804`, Kisarazu `13386:1828`, Kawana `13389:1849`.

## Dependency
Stages 0–1 independent (static, like `tournament_screens`). Stage 2 needs **T1 `tournament_contracts` → T2 → T3 → T4** (`GetTournaments()`).
