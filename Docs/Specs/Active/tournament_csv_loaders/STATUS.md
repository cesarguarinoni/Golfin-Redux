# STATUS — tournament_csv_loaders (T2)

**Task:** Author the three tournament CSVs (`tournaments.csv`, `tournament_prizes.csv`, `tournament_bot_fields.csv`) + a POCO `TournamentCsvLoader` that parses them into the shipped T1 DTOs, with EditMode tests. Data + parsing only.

**Tier:** FULL PIPELINE.

**Updated:** 2026-06-25 JST

## Progress
- [x] GDD §9 (CSV model) + §10 (prizes) pulled; reconciled against the **shipped** T1 DTOs (field names verified on disk).
- [x] CSV-loader pattern grounded: clone `ModesDatabaseCSV` (`Resources.Load<TextAsset>("Data/…")` + header parse) as a POCO; `#`-comment convention from `bot_difficulty.csv`; bracket ids = bot_difficulty minLevels (1/10/25/50/100/180).
- [x] SPEC authored — §0 T1↔CSV reconciliation (6 gaps + recs), §1 loader API, §2 reconciled schemas (column→DTO map), §3 **authored sample data** (6 tournaments matching the Figma cards + 3 prize templates + 3 bot fields), §4 reuse map, §5 EditMode tests, §6 out-of-scope, §7 flags. **Ready for Code handoff.**
- [ ] Stage 1 (impl): *(if §0.1 approved)* amend `TournamentDefinition` + `ResolveDelayMinutes` first; then `TournamentCsvLoader` + 3 CSVs + tests. Implementer.
- [ ] Self-review: header-name mapping (not index), `#`/blank skipping, holeSet/bracketWeights parse, referential-integrity check, all sample rows load.
- [ ] Architect review.

## ⚠ Decisions to resolve (SPEC §0/§7)
- **§0.1 add `ResolveDelayMinutes` to `TournamentDefinition`** (tiny T1 reopen) — REC yes, as T2's first commit.
- v1 = Rank prize bands only (percentile deferred); reward = RP + optional one item.
- `courseId`→`ClubId` mapping; `sponsorKey`/`leagueKey` columns added (extend GDD §9).
- Confirm item-reward ids + courseId values against real inventory/course ids.

## Dependency
Depends on **T1 DONE** ✓ (DTOs). Unblocks **T3 bot_field** (rolls `BotCard`s from `BotFieldConfig` + seed) → **T4 local_backend** → `tournament_screens` Stage 2.
