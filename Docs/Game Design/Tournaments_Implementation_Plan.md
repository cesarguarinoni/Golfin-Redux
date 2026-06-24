# Tournament System — Implementation Plan (v1)

> Companion to `Tournaments_GDD.md` (decisions locked 2026-06-22). Build order is **inside-out**: contracts → headless backend → save → play → UI → notifications → content. Logic phases are deterministic and EditMode-testable; UI phases are Figma-gated.

---

## Verified anchors (grounded, not assumed)

- **Save layer** `Golfin.Save`: `SaveDataHost.Instance` (singleton, `MarkDirty()` → debounced 250 ms atomic JSON write), `SaveData` (schemaVersion **2**, holds `rewardPoints`, flat DTOs only), `SaveSchemaMigrator` (`CurrentSchemaVersion=2`, fail-hard on newer).
- **Economy**: `RewardPointsManager.Instance` (RP debit/credit) → persists through `SaveDataHost`.
- **Navigation/UI**: `ScreenManager` (full screens), `ModalController` (overlays), event-driven `Action` binding.
- **Sim**: deterministic batch `BallSimulation.Simulate()`; bots via `bot_difficulty.csv` error-band model — reused conceptually for bot dispersion + reusable for future score verification.
- **Localization**: `LocalizedText` + CSV (`Assets/Localization/`).
- **New namespace**: `Golfin.Tournaments` (new leaf asmdef; depends on Save, Roster/economy, CSV/data, UI).

> Anything UI-fidelity below is **Figma-gated** — confirm page/frame and pull tokens via `get_metadata` + `get_design_context` before the spec is written. Do not guess layout.

---

## Phase A — Contracts & Data *(no gameplay, no UI)*

### T1 · `tournaments_core_contracts` — **FULL PIPELINE** (new asmdef)
New `Golfin.Tournaments` asmdef + the contract surface everything else binds to:
- DTOs: `TournamentDefinition`, `BotFieldConfig`, `PrizeTable` / `PrizeBand`, `HoleResult` (incl. `rngSeed` + `inputLog` slot), `LeaderboardRow`, `TournamentEntry`, `EntryPayment`.
- Enums: `TournamentState`, `EntryType`, `RewardType`, `EntryStatus`.
- `ITournamentClock` + `DeviceUtcClock`.
- `ITournamentBackend` interface (§7 of GDD).
- Deliverable: compiles, no behavior. Verify: asmdef references resolve, no leakage of runtime types into DTOs.

### T2 · `tournaments_csv_loaders` — **TELLCODE**
- Loaders + validators for `tournaments.csv`, `tournament_bot_fields.csv`, `tournament_prizes.csv` → T1 DTOs. Reuse existing CSV-parse utility.
- Ship **sample CSVs** (1–2 tournaments, all 3 prize templates, 1 bot field).
- Verify: malformed-row rejection, UTC parse, holeSet parse (`1-9`, `1,4,7`).
- Depends: T1.

---

## Phase B — Backend Logic *(headless, test-heavy, deterministic)*

### T3 · `tournament_bot_field` — **FULL PIPELINE** (failure-prone math)
- Seeded pre-roll of each bot's card (per-hole strokes + total) from skill-bracket distributions anchored to course par.
- Seeded **pace schedule** per bot (start offset + per-hole timestamps spread across window).
- `ProjectAt(now)` → revealed progress (`thru N`, partial total).
- EditMode tests: determinism (same seed ⇒ same field), reveal **monotonic** in time, **all bots complete by `endUtc`**, distribution sanity vs par.
- Depends: T1.

### T4 · `local_tournament_backend` — **FULL PIPELINE**
Implements `ITournamentBackend`:
- State derivation from `ITournamentClock` (Upcoming→Active→Resolving→Resolved→Archived).
- `Register`: RP debit via `RewardPointsManager`, character lock, dup-entry guard, maxEntrants.
- `GetLeaderboard`: merge projected bots (T3) + local player entry → rank → tie-break ladder (strokes → time → submit-ts); DNF placement.
- `GetResults` / prize resolution: band match (rank + percentile) → reward; `claimed` guard; cancel → RP refund.
- EditMode tests: ranking, tie-breaks, DNF, percentile band scaling across field sizes, RP debit/refund, claim-once.
- Depends: T1, T2, T3.

---

## Phase C — Save Integration

### T5 · `tournament_save_entry` — **FULL PIPELINE** (save schema = risk)
- Add `PersistedTournamentEntry` (flat DTO, incl. `characterId`) + list field to `SaveData`.
- Bump `schemaVersion 2 → 3`; `SaveSchemaMigrator` v2→v3 seeds empty list (preserve fail-hard-on-newer).
- Persist/load wiring; `MarkDirty()` after each hole append.
- EditMode tests: v2→v3 migration, round-trip, debounce coalescing, atomic-write resilience (extend existing `SaveLayerTests`).
- Depends: T1.

---

## Phase D — Play Integration

### T6 · `tournament_round_flow` — **FULL PIPELINE** (runtime; touches loop + stamina)
> **Blocked on sub-decision S1** (character lock vs swap). Default assumed: locked at registration.
- Drive the existing hole/round loop in "tournament mode": load `holeSet`, restore locked character, **consume stamina per hole** (existing system; block/await regen when empty), record `HoleResult` + per-hole time, advance, persist via T5.
- Resume from `currentHoleIndex`; window-close-mid-round → auto-`Submitted` (DNF if short).
- Verify: bot-recorded video of enter → play 2 holes → quit → relaunch → resume → finish → submit; stamina-exhaustion path.
- Depends: T4, T5.

---

## Phase E — UI *(Figma-gated; bind to `ITournamentBackend`, never rebuild hierarchies)*

| Order | Screen | Class | Notes |
|---|---|---|---|
| **T7** | `tournament_selection_screen` | FULL PIPELINE | filter tabs, cards, state-driven CTA |
| **T8** | `tournament_detail_screen` | FULL PIPELINE | rules, prize table, **character-lock picker**, sign-up/continue |
| **T8b** | `tournament_hole_selection_screen` | FULL PIPELINE | **per-tournament hole list** — Finished / Next / Locked hole cards, identity-pill row (sponsor · league · timer), podium-icon → Leaderboard, silver Close. Entry point into a tournament round. Built in `Docs/Specs/Active/tournament_screens` (Stage 1 = screen scaffolds + nav, static placeholder; Stage 2 = bind to `LocalTournamentBackend`). Reuses the HoleSelection screen + `HoleCard.prefab`. |
| **T9** | `tournament_leaderboard_screen` | FULL PIPELINE | provisional/final banner, projected rows, sticky player row |
| **T10** | `tournament_result_screen` | FULL PIPELINE | rank + prize + **Claim**; sequence after WIN/LOSE banner if reused from 1v1 result pattern |

> **Insertion note (2026-06-24):** **T8b `tournament_hole_selection_screen`** was added between T8 and T9 — the original plan jumped from sign-up (T8) straight to the leaderboard (T9), but a player needs a per-tournament hole-picker to actually enter/continue a tournament round. T8b + T9 are co-built in the `tournament_screens` spec; T8b is the upstream of T9 (podium-icon → Leaderboard, Leaderboard Close → Hole Selection). Nav flow: Selection (T7) → Hole Selection (T8b) ⇄ Leaderboard (T9).

Each: confirm Figma frame → extract tokens → spec to image, not prose. Depends: T4 (+ T6 for live HUD context on T9).

---

## Phase F — Notifications

### T11 · `tournament_home_banner` — **TELLCODE / SURGICAL**
- Extend existing Home notification banner with tournament states; priority ladder: *Prize unclaimed → Results ready → Ends soon & unfinished → Entered → Registration open*. Driven by `ITournamentClock` + entries.
- **NOTE:** confirm the existing banner component before speccing.
- Depends: T4.

---

## Phase G — Content & Localization

### T12 · `tournament_content_authoring` — **SURGICAL / data**
- Author first real tournaments (CSV rows), the 3 prize templates, bot field(s). JP+EN `nameKey`s via existing localization CSV.
- Depends: T2, T4. (Localization scan can fold into Order 353 `localization_audit`.)

---

## Dependency graph / critical path

```
T1 ─┬─► T2 ─► T4 ─┬─► T7,T8,T9,T10 (Figma-gated)
    ├─► T3 ──────┘   └─► T11
    └─► T5 ─┐
            └─► T6 (needs S1)
T4 ─► T12
```

**Critical path:** T1 → T4 → T6 → UI. Backend (A+B+C) is fully deterministic and can land + be proven by EditMode tests before any Figma work exists.

---

## Spec-writing readiness (handoff)

**Can spec now (no external blockers):** T1, T2, T3, T4, T5 — pure logic/data, deterministic, test-gated.

**Blocked:**
- **T6** → ruling on **S1** (character lock vs swap).
- **T7–T10** → Figma frames (point me at existing tournament frames in `5gEAHjl6xAtW8iYY7NMvWd`, or flag that they need designing first).
- **T11** → confirm the Home banner component.

**Recommended first spec:** **T1** (`tournaments_core_contracts`) — unblocks the entire chain and locks the DTO/interface shape (incl. the forward-compat `rngSeed`/`inputLog` slots) before anything depends on it.
