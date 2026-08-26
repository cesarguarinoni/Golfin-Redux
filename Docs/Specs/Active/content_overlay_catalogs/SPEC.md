# SPEC — `content_overlay_catalogs`

> **Phase 2.** Extends the Phase-1 overlay from texts to the catalogs that carry *stats* — and
> therefore to the first changes that can touch a player's saved instances.
>
> Plan: `Docs/CONTENT_PIPELINE_PLAN.md` §2 (invariants), §5 (this task), §11 (shop).
> Depends on: `content_overlay_texts` — DONE. `Golfin.Content` exists; do not rebuild it.
>
> NOT `SPEC_KIND: backend` — Unity task, real Game View, screenshot and EditMode gates apply.

## Status

`SPEC_READY`.

## Goal

Overlay `clubs`, `characters`, `items`, `bags`, `balls` and `shop_catalog` on top of their bundled
CSVs, and **clamp every owned instance** against the overlaid definitions. Texts proved the
delivery path; this is the first time a published change can invalidate data a player already has.

## Why this one is different from Phase 1

A wrong string is visible and harmless. A wrong `maxDurability` is neither: it can leave a saved
`PersistedClub` holding `currentDurability` above a ceiling that no longer exists. **Un-clamped
application is the single most likely way this feature corrupts a save**, and it is the reason
this spec is separate rather than a widening of Phase 1.

## Execution order — already correct, but ASSERT it

Measured 2026-08-26:

| Order | Component |
|---|---|
| −1000 | `LocalizationBootstrap` |
| −900 | `ContentService` (overlay ready) |
| −100 | `SaveDataHost` (save loaded) |
| 0 | `ClubDatabaseCSV`, `ClubManager`, `CharacterDatabaseCSV`, `CharacterManager`, `ItemDatabaseCSV`, … |

So at order 0 **both** the overlay and the save are available. That is exactly what clamping needs
and no ordering change is required.

⚠️ **But `ClubDatabaseCSV` and `ClubManager` are BOTH at default 0.** The file comment claims
"runs before ClubManager" and there is no `[DefaultExecutionOrder]` backing it — so the guarantee
comes from somewhere else (project Script Execution Order, or scene component order) or not at
all. **Find out which, and assert it at runtime the way Phase 1 asserted
`LocalizationManager.IsInitialized`** — a manager that hydrates from a not-yet-overlaid database
fails silently and looks like "the overlay didn't work".

## Implementation

### 1. Catalog overlay in each `<X>DatabaseCSV`

Per catalog, in `LoadCSV()` after the bundled row is parsed: ask `ContentService` for an overlay
row by `id`; merge field-by-field; append overlay rows whose `id` is new. Parsing stays in the
existing pure parsers — **extend `ClubCsvParser`'s EditMode tests, do not fork it.**

Request `catalogs=clubs,characters,items,bags,balls,shop_catalog` with each cursor from
`content_version.txt`. Phase 1's per-catalog `since` already handles this; nothing new on the wire.

### 2. Clamping — the heart of this task

Once, in an explicit clamp step after the overlay is applied and the save is loaded. **Not at each
read site.**

| Field | Rule |
|---|---|
| `PersistedClub.currentDurability` | clamp to `[0, maxDurability]` — the `Mathf.Clamp` idiom already exists in `ClubManager.RepairClub` |
| `PersistedClub.currentLevel` | clamp to `[startLevel, maxLevel]` |
| `PersistedCharacter.currentLevel` | same |
| allocated SP (`spentPower`, `spentAccuracy`, …) | re-clamp against `RarityStatCaps` for the row's CURRENT rarity |
| `equippedBagSlot` | if the club's row became `is_active=false`, it STAYS equipped (I6) |

**Refunding is out of scope.** If a rarity downgrade orphans allocated SP, clamp and **log it** —
do not invent a refund, and do not silently discard the delta without a log line. If that turns
out to matter, it is its own decision.

Every clamp writes one `Debug.LogWarning` naming the id, field, old and new value. A clamp that
happens silently is indistinguishable from a bug report six weeks later.

### 3. Tournaments are already safe — do not "fix" them

`PersistedTournamentEntry.snapshot` (`PersistedCharacterSnapshot`) freezes character stats at
sign-up, so a mid-event balance change cannot alter a running entry. Leave it alone. Add a test
that pins this, because it is the kind of thing a later refactor removes without noticing.

### 4. `is_active = false` — deactivate, never delete (I6)

- Disappears from shop, gacha pools and any "available" list.
- **Stays fully renderable** in the bag / roster of every player who owns one: name, art, stats.
- A deactivated club that is currently equipped stays equipped.

### 5. `min_build` matters here in a way it did not for texts

Texts always render. A club row references a **sprite name** the build may not have, and a missing
sprite resolves to `Placeholder`. `min_build` is filtered server-side, so this mostly takes care of
itself — but add a client-side guard: **if an overlay row's sprite does not resolve, keep the
bundled row rather than showing `Placeholder`.** A silently-wrong club is better than an obviously
broken one, and the operator gets told via the art-coverage path, not the player.

### 6. Shop windows

`shop_catalog` now carries `startAt` / `endAt` / `saleStartAt` / `saleEndAt` (added by
`content_panels_gaps`). The client must honour them: `endAt` is EXCLUSIVE; outside the sale window
`saleRpCost` is ignored; a present-but-unparseable bound **drops the row** (fail closed, matching
`routers/notices.py` `_parse` and the dashboard's own validator).

### 7. Close the kill-switch semantics gap

Found while reviewing Phase 1. The **global** `enabled:false` drops the cache and reverts to
bundled. A **per-catalog** `is_enabled=false` makes that catalog merely *absent* from the payload,
and this client treats absent as "no update" — so the last good overlay keeps applying forever.
`CONTENT_PIPELINE_PLAN.md` §7.4 promises "ignore remote and run bundled until flipped back", which
is true of one kill and not the other.

Make them agree: a catalog explicitly reported disabled must drop **that catalog's** cache. This
needs the server to distinguish "disabled" from "not requested" — if the payload cannot express
that today, **report it rather than guessing**; that has now caught five real gaps.

### 8. Apply at next launch (I5)

Unchanged from Phase 1: fetch → validate → write cache → apply at next `Awake`. Re-parsing the
club DB mid-session with a bag equipped and a round in flight is a re-entrancy problem with no
upside.

## Acceptance checklist

- [ ] Publish a club stat change, relaunch → the new value shows in Clubs and in the shot the club takes
- [ ] Publish `maxDurability` BELOW an owned club's `currentDurability` → clamped on load, one warning naming id/field/old/new, save persists clamped
- [ ] Publish `maxLevel` below an owned club's / character's `currentLevel` → clamped + logged
- [ ] Rarity downgrade orphaning SP → clamped against `RarityStatCaps` + logged, no refund invented
- [ ] `is_active=false` on an OWNED club → gone from shop, still renders in the bag, still equipped
- [ ] A tournament entry in flight is unaffected by a character stat publish (test pins the snapshot)
- [ ] An overlay row whose sprite does not resolve keeps the BUNDLED row — no `Placeholder` in the grid
- [ ] Shop window honoured: future `startAt` hidden, past `endAt` hidden, sale price ignored outside its window, unparseable bound drops the row
- [ ] Per-catalog kill drops that catalog's cache — or the gap is reported with what the payload cannot express
- [ ] DB-before-Manager ordering identified and asserted at runtime; log the assert
- [ ] Airplane mode / corrupt cache / missing version file → bundled catalogs, warning, no exception
- [ ] Boot time measured before and after (clubs is 799 rows — measure, do not assert)
- [ ] Full unfiltered EditMode sweep green. ⚠️ 17 failures are PRE-EXISTING (`GachaTicketTests.CurrentSchemaVersion_Is9` asserts 9, `SaveSchemaMigrator` ships 10, plus two in `ClubOwnershipTests`) — fix those literals as part of this task so the suite is a real signal again, and say so in the report
- [ ] Screenshot of an admin-published club stat rendering in-game
- [ ] Spec deviations flagged at the bottom of the report

## Out of scope

- Live mid-session swap (I5).
- Player inventory server-side (Phase 4), Addressables (§10.3), art URLs (§10.2).
- SP refunds on rarity downgrade — clamp and log; refunding is its own decision.
- `LevelUpCosts` — still `CONTENT_PIPELINE_PLAN.md` §9 open Q2, still unanswered.
- Any endpoint, panel or schema change. If the client needs something the API cannot serve,
  **report it**.
