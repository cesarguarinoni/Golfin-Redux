# RED-TEAM REVIEW — gacha_screen Stage 1 (re-verify single blocker fix)

**Date:** 2026-07-12 10:41 JST
**Scope:** Re-verify ONLY the one blocker from the prior FAIL (cross-referenced test-grant seed sites). All other Stage 1 items were cleared on the prior pass and are not re-litigated.
**Verdict:** ARCHITECT_REVIEW_PASS

## The prior FAIL floor
Prior pass FAILed on exactly one item: the two test-grant seed sites (SaveSchemaMigrator v6→v7 and GachaTicketManager.Awake) did not cross-reference each other, risking a partial ship-revert (revert one → emptied balances silently refill to 10).

## Verification of the fix

### 1. Cross-references present and MUTUALLY findable — GONE (blocker resolved)
- **GachaTicketManager.cs lines 50-53** (over the `gachaTickets == 0 → DEFAULT_STARTING_TICKETS` guard):
  > `TODO: remove this Awake guard when reverting the test grant to 0.`
  > `ALSO revert the paired seed in SaveSchemaMigrator.cs (v6→v7 block, \`data.gachaTickets = 10\`). Both sites must be reverted together — reverting only one leaves emptied balances silently refilling to 10.`
  Names the OTHER site by file + block + exact code line. ✅
- **SaveSchemaMigrator.cs lines 103-107** (over the `data.gachaTickets = 10` migration seed):
  > `TODO: revert test grant to 0 before ship.`
  > `ALSO revert the paired seed in GachaTicketManager.Awake (the \`gachaTickets == 0 → DEFAULT_STARTING_TICKETS\` guard, ~line 51). Both sites must be reverted together — reverting only one leaves emptied balances silently refilling to 10.`
  Names the OTHER site by file + method + guard expression + line. ✅

A dev landing on EITHER TODO is pointed to the other by concrete identifiers (file, block/method, exact expression). "Revert both together" is explicit at both sites, with the failure mode spelled out. Mutually findable — satisfied.

### 2. Edit is comment-only (no logic/behavior change) — CONFIRMED
- `git diff` on SaveSchemaMigrator.cs shows the v6→v7 logic block intact: `if (data.schemaVersion < 7) { data.gachaTickets = 10; data.schemaVersion = 7; }`. `CurrentSchemaVersion = 7` unchanged.
- GachaTicketManager.cs lines 54-58 read directly: guard `if (SaveDataHost.Instance.Data.gachaTickets == 0) { ... = DEFAULT_STARTING_TICKETS; MarkDirty(); }` unchanged; `DEFAULT_STARTING_TICKETS = 10` (line 23) unchanged.
- The only changes are the added comment lines. Behavior identical.

### 3. No regression / no other seed site missed — CONFIRMED
- Grep of all `gachaTickets =` assignments across `Assets/Scripts`: exactly TWO production seed sites (GachaTicketManager.Awake, SaveSchemaMigrator v6→v7) — the two the pair now cross-references. Every other hit is a test fixture (`GachaTicketTests.cs`, `ClubOwnershipTests.cs`), not a ship-revert site. No third grant site was left un-cross-referenced.
- Orchestrator re-ran GachaTicketTests: 11/11 PASS (compile clean, behavior unchanged) — consistent with a comment-only edit.

## Break attempts
- **Partial-revert trap (the original blocker):** attempted to imagine a dev reverting one site. Now blocked — each TODO explicitly names the other site AND the silent-refill consequence. Failed to break.
- **Hidden third seed site:** grepped for any other `gachaTickets =` / `DEFAULT_STARTING_TICKETS` write. Only the two referenced production sites plus test fixtures. Failed to break.
- **Comment claims logic that isn't there:** verified the referenced expressions (`data.gachaTickets = 10`, `gachaTickets == 0 → DEFAULT_STARTING_TICKETS`) actually exist at the cited locations. They do. Failed to break.

Blocker resolved; nothing else regressed. Advancing to Cesar.
