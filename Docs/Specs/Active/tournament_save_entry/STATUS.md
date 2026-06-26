READY — dep met (T1 ✓); can fire in parallel with T4

# STATUS — tournament_save_entry (T5)

> **2026-06-26 — Spec authored** against the real `Golfin.Save` layer (Newtonsoft serializer, flat-DTO mandate, asmdef Tournaments→Save one-way, `SaveDataHost`/`SaveSchemaMigrator` patterns). Boundary: **T5 = persistence only** — no tournament logic (T4), no round loop (T6).

**Task:** Add `PersistedTournamentEntry`/`PersistedHoleResult` flat DTOs + `SaveData.tournamentEntries` + `schemaVersion 2→3` migrator (in `Golfin.Save`); implement `SaveBackedEntryStore : ITournamentEntryStore` (in `Golfin.Tournaments`) that maps `EntryState`⇄persisted DTOs via `SaveDataHost.Instance` + `MarkDirty()`. Swaps T4's in-memory store behind the same seam.

**Tier:** FULL PIPELINE (save schema = risk). Gate = EditMode suite extending `SaveLayerTests` (migration / fail-hard / round-trip / upsert / claim / debounce / restart). No clone table → Rule 8 N/A.

## Dependency / ordering
- **T1 ✓.** Parallel to **T4** (both from T1; meet at T6). Can fire **now**, independent of T4 — the seam (`ITournamentEntryStore`) decouples them. T4 runs with its in-memory store; T5's disk-backed store is wired in at T6 (one-line constructor swap).

## Staging (SPEC §6)
- [ ] **Stage 1** — `Golfin.Save` DTOs + `SaveData.tournamentEntries` + migrator `v2→v3` + migration/fail-hard/round-trip tests *(risk surface)*.
- [ ] **Stage 2** — `SaveBackedEntryStore` adapter (map⇄, upsert, MarkDirty) + tests.

## ⚠ Decisions for Cesar (SPEC §7)
- **D1** persist `inputLog`? Rec: no (v1) — store `rngSeed` only, defer shot logs to server era.
- **D2** claim-state persistence — **OPEN.** Shipped seam is `Load`/`Save(EntryState)` only; `EntryState` has no `claimed` → claim-once is in-memory and a **relaunch could double-claim a prize.** (a) accept in-memory v1 (rec, simplest) vs (b) persist claim state (T4 grows the seam). Lock after T4 Stage 4.
- **D3** DateTime as ISO string (rec, diff-friendly) vs raw `DateTime?` (Newtonsoft handles both).

## Kickoff (deps met — fire any time, parallel-safe with T4)
```
Use the golfin-implementer subagent on "tournament_save_entry"
```
