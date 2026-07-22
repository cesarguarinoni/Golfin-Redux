# SPEC — `localize_rankings_tournaments`

> **Authoritative spec.** Implementer reads this and ONLY this. STATUS.md tracks pipeline state.

## Status

`SPEC_READY`.

## Goal

**Batch 4 of the localization sweep** (batches 1–3 DONE: Persistent/Home, Inventory/Bag, Hole/Results). Convert the genuinely-static user-facing labels in the **`Rankings/Tournaments`** group — the rankings screen (period tabs + leaderboard), tournament selection/leaderboard screens, tournament hole cards, result modal — using the **code-path-first recipe**. Audit group = 125 rows across 20 assets; after triage the real actionable set is ~20 distinct static labels (the rest are runtime-set names/rarities/levels/dates/counts/countdowns, per-hole data, placeholders, and one editor builder).

## Recipe (from batches 1–3 — apply exactly)

1. **Code-path-first.** Controller-assigned label → `Get()` at the code site. Static prefab label nothing overwrites → `LocalizedText` binder via `LocalizationEditorHelper.AddLocalizedText`.
2. **Verify the live surface before binding (finding #1).** Rankings/tournament screens instantiate list rows and cards from prefabs, and some screens may be scene GOs. For EACH prefab you bind, cite the controller `Instantiate`/`Show` site proving the live UI uses that instance. If a target is a disconnected scene GO, bind the scene GO or convert the code site — document which. Do NOT assume a binder takes effect.
3. **Never bind a runtime-overwritten label.** Player/character names, rarities, levels, stroke counts, ranks, rewards, dates, countdowns, tournament names, course/hole strings — all set at runtime per entry. SKIP.
4. **Editor/Archive builders are not shipping code.** SKIP `Assets/Scripts/Editor/TournamentResultModalBuilder.cs` entirely.
5. **Reuse/dedup + EN-casing check (batch-3 scar).** Repeated labels share ONE key. Before reusing an existing key, its EN must match the source label EXACTLY incl. casing/punctuation — else mint a new key. **Verified for this batch:** `BTN_START`="PLAY", `SETTINGS_CLOSE`="CLOSE", `UI_LOCKED`="LOCKED" all match. **Use `UI_LOCKED` for "LOCKED" — NOT `BAG_LOCKED`** (its EN is "Locked", the batch-1 regression). Report each reuse's EN-match verdict.
6. **Preserve displayed English exactly**; flag any source typo (e.g. `DIAMOND LEAGE`) rather than fixing it.

## Triage

### CONVERT — static labels (bind on prefab, or Get() at code site). Reuse existing keys (casing verified):

| Label | Key (exists) | Source |
|---|---|---|
| `PLAY` | `BTN_START` | TournamentHoleCard_{Finished,Locked,Next} |
| `LOCKED` | `UI_LOCKED` (**not BAG_LOCKED**) | TournamentHoleCard_Locked |
| `CLOSE` | `SETTINGS_CLOSE` | TournamentCloseButton, TournamentSelectionCard |

### CONVERT — static labels needing NEW keys (verify static first; dedup one key per distinct English; suggested `RANK_`/`TOURN_` prefixes — reuse if an identical-English key already exists):

Period/filter tabs: `DAILY`, `WEEKLY`, `MONTHLY`, `HISTORY` (RankingsScreen) · `ALL`, `OPEN`, `PLAYING`, `CLOSED` (TournamentSelectionScreen).
Status / buttons: `CLAIM`, `ENTERED`, `FREE ENTRY`, `ENTRY`, `OPEN` (selection card badge — dedup with the tab OPEN only if identical display intent, else separate), `FINISHED`, `LIVE`, `NEXT`.
Headers / empty-state: `GOLFIN PRESENTS`, `No finishers yet`, `Be the first to complete every hole and top …` (empty-state body — full string).
Code-site: `SPONSORED BY ` prefix in `TournamentLeaderboardScreenController.cs` (the static "SPONSORED BY" portion; the sponsor name concatenated after stays dynamic — convert just the literal prefix via Get()), `ENTERED`/`FREE ENTRY` in `TournamentSelectionCard.cs`.

### DO NOT CONVERT — document each in `## Triage findings`, touch nothing:

- **Runtime-set per entry:** character/player names (`GALADRIEL`, `FRODO`), rarities (`RARE`, `LEGENDARY` — the card sets rarity from data; do NOT reuse RARITY_* here, they're dynamic), levels (` - Lv 80`, ` - LV 80`, `Lv `), stroke counts (`80 STROKES`, `72 STROKES`), ranks (`RANK #1`), rewards (`12,000 + Trophy`).
- **Dynamic tournament/hole data:** tournament names (`Lomond Open`, `Lomond Championship` incl. the `tourn.lomond` reuse — do NOT bind it, names are dynamic), venue/holes strings (`Lomond Golf Club · 18 Holes`, `Lomond Country Club … Hole N Par M`), date ranges (`Jun 20 — Jun 27`, `Jun 24 – Jun 27 — Ends in 3d 04h`), countdowns (`Resets IN: …`, `RESETS IN: 0s`), tee-off/strokes readouts, per-hole tee-shot descriptions.
- **League name** (`DIAMOND LEAGUE` in RankingsScreenController, `DIAMOND LEAGE` typo placeholder in RankingsScreen.prefab) — the league is dynamic/data-driven; SKIP both and document (avoids the typo entirely). If verification shows the league label is a fixed static string, convert the controller one via Get() and flag the prefab typo — judgement call, document it.
- **Composed fragments:** ` - Lv `, `Lv ` (concatenated with numbers) — SKIP as composed dynamic; the structured-string localization is a later pass.
- **Placeholders / counts:** `Description placeholder`, `x10`.
- **Editor builder:** all of `TournamentResultModalBuilder.cs`.

Follow the evidence: flip any row whose real nature differs, and document.

## JP policy (unchanged)

Reused keys keep existing JP. New keys: EN = exact displayed string, JP = EN + ` [JP-TODO]`. No invented Japanese. JP renders via the Noto TMP fallback.

## Anti-fabrication (batch-3 scar — MANDATORY)

Every EN/JP screenshot pair MUST be byte-distinct real captures via the real play-mode flow (Capture Rule 0). The self-reviewer and both gates will `md5` all screenshots and open JP captures to confirm real Japanese/`[JP-TODO]` (not English, not raw keys, not tofu). A fabricated or duplicated capture = CRITICAL FAIL logged to `review_misses.log`. If a surface is genuinely unreachable, say so — never invent a frame.

## `[JP-TODO]` overflow is EXPECTED

The verbose `[JP-TODO]` placeholder overflows/overlaps in JP mode (real JP will fit). NOT a layout FAIL. Visual gate: (a) EN layout unchanged, (b) keys resolve (no raw KEY on screen), (c) real-JP keys render Japanese.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **Triage findings:** all 125 audit rows verdicted (CONVERTED how / SKIPPED bucket / DEFERRED reason). Primary deliverable.
- [ ] **Live-surface proof:** each bound prefab cites its controller Instantiate/Show site (list rows, cards, modals).
- [ ] **Reuse-casing audit:** each reuse's EN-exact-match verdict; `UI_LOCKED` used (not BAG_LOCKED); no bind to RARITY_*/tourn.lomond (dynamic).
- [ ] **Binders/code:** correct keys (read-back quoted / diffs shown); no binder on a controller-written label; LocalizedText GUID only, no layout mutation.
- [ ] **CSV:** ~20 new keys (EN exact + `[JP-TODO]`); reused keys pre-existing; no duplicate; importer re-run; key count reported; typos flagged.
- [ ] **EN unchanged** captures + **JP smoke** captures (byte-distinct, real) of: rankings screen (tabs), tournament selection screen (filter tabs + a card), tournament leaderboard (+ empty state if reachable), a tournament hole card. Cite each.
- [ ] **Scope:** only the touched Rankings/Tournaments prefabs + touched runtime controllers + CSV + table (+ task folder). NO editor builder, NO scene mutation, NO `Assets/Scripts/Physics/`, NO asmdef. Quote `git status`.
- [ ] Compiles clean; no task-related console errors; HEARTBEAT has iter baseline.
- [ ] Spec deviations flagged.

## Not a Figma task

No Figma node — Rules 16/17/18/21 N/A. Visual gate as above.

## Out of scope / Deferred

Any other group; runtime/dynamic strings; editor builders; placeholders/counts; composed fragments (deferred to a structured-string pass); inventing Japanese; asmdef changes (defer asmdef-gated strings); visual/layout changes; scenes; `Assets/Scripts/Physics/`; `M_Splash*.mat`.

---
