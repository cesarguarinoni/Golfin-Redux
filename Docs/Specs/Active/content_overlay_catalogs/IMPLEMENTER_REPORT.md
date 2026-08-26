# Implementer Report — `content_overlay_catalogs`

**Iteration shape:** `content-pipeline:catalog-overlay-and-clamp`
**Date:** 2026-08-26 · **Baseline:** HEAD `96c9e0d78` (`Docs and Tellcode`)

## Implementation summary

Extended the Phase-1 `Golfin.Content` assembly — it was not rebuilt — so the six data catalogs
(`clubs`, `characters`, `items`, `bags`, `balls`, `shop_catalog`) overlay their bundled CSVs
alongside the texts overlay that already shipped. The merge is field-by-field through one new
`ContentFields` reader that every `<X>DatabaseCSV` now parses through, so a published row is a
sparse patch rather than a replacement row, and unknown columns stay ignored (I4).

The clamp is the part that matters and it is one explicit step per owned collection, not a read-site
guard: `ContentClamp.ClampClubs` runs in `ClubManager.InitializeClubs` and
`ContentClamp.ClampCharacters` in `CharacterManager.LoadRoster`, both at the first point in the boot
where the overlaid catalog and the loaded save are BOTH available. Every field that moves emits one
`Debug.LogWarning` naming id / field / old / new, and the save is marked dirty so the clamped values
persist.

Three things came out of the work that were not in the spec's plan: the DB-before-Manager guarantee
turned out to come from a **committed `.cs.meta` field that nothing re-asserts**; the per-catalog
kill switch turned out to be **half-expressible** on the wire (more than the spec assumed, less than
it wanted); and `CharacterManager.GetMaxLevel` turned out to **ignore the CSV `maxLevel` column
entirely**, which was invisible until an overlay could make it disagree with the rarity table.

---

## Files modified or created

### New — `Golfin.Content` (runtime)

| Path | Change |
|---|---|
| [ContentCatalogs.cs](Assets/Scripts/ContentRuntime/ContentCatalogs.cs) | Created — the seven catalog names in one place, plus the `catalogs=` request list. A typo here is invisible (an unknown name is ignored server-side, not a 400), so it is spelled once. |
| [ContentRow.cs](Assets/Scripts/ContentRuntime/ContentRow.cs) | Created — `ContentRow` (id / is_active / min_build / column bag) and `ContentFields`, the one reader that answers a column from the overlay first and the bundled CSV second. This is the whole merge. |
| [ContentCatalogMapper.cs](Assets/Scripts/ContentRuntime/ContentCatalogMapper.cs) | Created — payload JSON → per-catalog row tables; `ExtractSlices` pulls each catalog's RAW slice for the cache. Keeps "present-and-empty" and "absent" distinct. |
| [ContentCatalogStore.cs](Assets/Scripts/ContentRuntime/ContentCatalogStore.cs) | Created — the static read surface each database consults, plus `RequireReady()`, the Phase-2 analogue of Phase 1's `LocalizationManager.IsInitialized` assert. |
| [ContentClamp.cs](Assets/Scripts/ContentRuntime/ContentClamp.cs) | Created — **the heart of the task.** Pure clamp over `PersistedClub` / `PersistedCharacter`, returning a `ClampEvent` per field moved; `LogAll` writes the warnings. No refunds. |
| [ContentShopWindow.cs](Assets/Scripts/ContentRuntime/ContentShopWindow.cs) | Created — pure, clock-injected evaluation of `startAt` / `endAt` / `saleStartAt` / `saleEndAt`. `endAt` exclusive; unparseable bound drops the row. |
| [ContentSpriteGuard.cs](Assets/Scripts/ContentRuntime/ContentSpriteGuard.cs) | Created — §5's veto. Only guards sprite names the OVERLAY changed, memoised, with one shared warning per vetoed row. |

### Modified — `Golfin.Content` (runtime)

| Path | Change |
|---|---|
| [ContentService.cs](Assets/Scripts/ContentRuntime/ContentService.cs) | Modified — requests all seven catalogs in one round trip, installs each cached catalog into the store at Awake, measures per-catalog boot cost, and treats a requested-but-absent catalog as WITHDRAWN (drops that catalog's cache). Texts path unchanged. |
| [RemoteContentSource.cs](Assets/Scripts/ContentRuntime/RemoteContentSource.cs) | Modified — cache is now **one file per catalog** (`content_clubs.json`, …), each holding a minimal payload envelope so both mappers read it unchanged and a Phase-1 whole-body `content_texts.json` still parses. Added a multi-catalog `FetchRoutine` and `BuildSince`. |
| [Golfin.Content.asmdef](Assets/Scripts/ContentRuntime/Golfin.Content.asmdef) | Modified — added `Golfin.Save` (the clamp mutates `PersistedClub` / `PersistedCharacter`). One-way; Save still references nothing. |

### Modified — Assembly-CSharp (the catalog readers)

| Path | Change |
|---|---|
| [ClubCsvParser.cs](Assets/Scripts/UI/Inventory/ClubCsvParser.cs) | Modified — **extended, not forked** (§1). Added `Parse(csv, overlay)`, made `ParseRow` take a `ContentFields`, added `startLevel` / `isActive` / overlay-provenance fields. Still pure. |
| [ClubDatabaseCSV.cs](Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs) | Modified — wires the clubs overlay, applies §5's sprite veto, exposes `IsLoaded` (the runtime ordering assert) and `GetAvailableClubs()` (the I6 "can be acquired" view). |
| [ClubData.cs](Assets/Scripts/UI/Inventory/ClubData.cs) | Modified — `ClubDataRuntime` gained `startLevel` and `isActive`. |
| [CharacterDatabaseCSV.cs](Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs) | Modified — same overlay + veto treatment; `CharacterDataRuntime` gained `startLevel` / `isActive`; added `IsLoaded` and `GetAvailableCharacters()`. |
| [ItemDatabaseCSV.cs](Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs) · [ItemDataRuntime.cs](Assets/Scripts/UI/Inventory/ItemDataRuntime.cs) | Modified — items overlay + veto + `isActive` + `GetAvailableItems()`. |
| [BallDatabaseCSV.cs](Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs) · [BallData.cs](Assets/Scripts/UI/Inventory/BallData.cs) | Modified — balls overlay + veto + `isActive` + `GetAvailableBalls()`. |
| [BagDatabaseCSV.cs](Assets/Scripts/BagDatabaseCSV.cs) | Modified — bags overlay + veto + `isActive`. Overlay rows are **append-only, always last**, because `GetBagBySlot` is index-based and reordering would repoint every player's saved bag slot. |
| [GeneralShopModel.cs](Assets/Scripts/UI/Shop/GeneralShopModel.cs) | Modified — shop_catalog overlay, §6 window filtering, I6 `is_active` filtering, and a switch from fixed column INDICES to column NAMES (the file has already grown four columns since it was written; an index reader silently mis-assigns everything after an insertion). |

### Modified — Assembly-CSharp (the clamp call sites + asserts)

| Path | Change |
|---|---|
| [ClubManager.cs](Assets/Scripts/ClubManager.cs) | Modified — the club clamp step, `BuildClampDefinitions`, and the `ClubDatabaseCSV.IsLoaded` runtime assert. |
| [CharacterManager.cs](Assets/Scripts/CharacterManager.cs) | Modified — the character clamp step, `BuildCharacterClampDefinitions`, the `CharacterDatabaseCSV.IsLoaded` assert, starter candidates now filtered by `isActive` (I6), and **`GetMaxLevel` now honours the catalog's `maxLevel`** (see Finding 3). |

### Tests

| Path | Change |
|---|---|
| [ContentClampTests.cs](Assets/Scripts/ContentRuntime/Tests/ContentClampTests.cs) | Created — 16 tests. The whole clamp matrix, including the no-refund pin and the "equippedBagSlot is never touched" pin. |
| [ContentShopWindowTests.cs](Assets/Scripts/ContentRuntime/Tests/ContentShopWindowTests.cs) | Created — 17 tests. Inclusive/exclusive edges, fail-closed bounds, sale-outlives-listing, timezone handling. |
| [ContentCatalogMapperTests.cs](Assets/Scripts/ContentRuntime/Tests/ContentCatalogMapperTests.cs) | Created — 17 tests, including the present-and-empty vs absent distinction and the Phase-1-cache upgrade path. |
| [ContentCatalogStoreTests.cs](Assets/Scripts/ContentRuntime/Tests/ContentCatalogStoreTests.cs) | Created — 8 tests pinning the three store states and the ordering assert. |
| [ClubOverlayMergeTests.cs](Assets/Scripts/UI/Inventory/Tests/ClubOverlayMergeTests.cs) | Created — 11 tests driving the REAL `ClubCsvParser` against the SHIPPED 799-row Clubs.csv via the existing reflection helper. |
| [TournamentSnapshotImmunityTests.cs](Assets/Scripts/Tournaments/Tests/TournamentSnapshotImmunityTests.cs) | Created — §3's pin: 4 tests proving a publish cannot reach an entry in flight. |
| [ClubRosterProd.cs](Assets/Scripts/UI/Inventory/Tests/ClubRosterProd.cs) | Modified — `Parse` now binds by SIGNATURE (the new overload would have made a name-only `GetMethod` throw `AmbiguousMatchException`); added overlay builders. |
| [GachaTicketTests.cs](Assets/Scripts/Save/Tests/GachaTicketTests.cs) · [ClubOwnershipTests.cs](Assets/Scripts/Save/Tests/ClubOwnershipTests.cs) · [SaveLayerTests.cs](Assets/Scripts/Save/Tests/SaveLayerTests.cs) | Modified — the 16 stale schema literals (see "Pre-existing failures"). |
| [TournamentServiceWireupTests.cs](Assets/Scripts/TournamentsRuntime/Tests/TournamentServiceWireupTests.cs) | Modified — the 17th pre-existing failure (see below). |
| [StaminaLiveWiringTests.cs](Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs) | Modified — comment only; updated a stale cross-reference to a renamed test. |
| 3 × `.asmdef` | Modified — `Golfin.Content.Tests` +`Golfin.Save`; `Golfin.Inventory.Tests` +`Golfin.Content`; `Golfin.Tournaments.Tests` +`Golfin.Content`. |

**Not mine, pre-existing in the working tree at kickoff** (untouched by this task, cited per Rule 13):
`Assets/Localization/LocalizationManager.cs`, `Assets/Scenes/ShellScene.unity`,
`Assets/Scripts/Net/Endpoints.cs`, `Assets/Scripts/UI/BuildInfo/AppVersion.cs`,
`Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt`,
`Docs/Specs/Active/content_cursor_per_catalog/SPEC.md`, `Docs/Specs/Active/content_overlay_texts/**`,
`Docs/Specs/Queued/content_admin_panels_NOTE.md` (D), `_to_delete/**` (D),
`tasks/quit_transition_demo/**` — all Phase-1 / unrelated work already in the tree at `96c9e0d78`.

---

## Screenshots

- **Canonical screenshot:** `screenshots/clubs_published_stat_and_clamp.png` (1170×2532, real play mode, real player navigation via `NavInventoryButton.onClick`)
- `screenshots/roster_published_maxlevel_ceiling.png` — the character clamp + the Finding-3 fix
- `screenshots/roster_rarity_downgrade_clamp.png` — the same roster BEFORE the Finding-3 fix, kept because the `/39` in it is the evidence for the finding
- **Scene:** `Assets/Scenes/ShellScene.unity` · **Play mode:** Yes

The canonical frame carries five acceptance items at once. Reading the whole frame, not just the
feature: **DRIVER GOLFIN — COMMON — Lv 8 /8 — POWER 95/100 — DURABILITY 40/40 — EQUIPPED / IN BAG
MIREO**, with WOOD GOLFIN and IRON 4 GOLFIN both present in the carousel with their real art.

---

## Acceptance checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Publish a club stat change, relaunch → new value shows | **PASS** | Published `basePower` 49 → 95 on `club_driver_golfin_common`. Canonical screenshot reads **POWER 95/100**. Live probe: `basePower=95`. |
| 2 | `maxDurability` below owned `currentDurability` → clamped, one warning naming id/field/old/new, save persists clamped | **PASS** | Published 60 → 40 against a save holding 60/60. Log: `CLAMPED clubs 'club_driver_golfin_common': maxDurability 60 → 40` and `currentDurability 60 → 40`. Screenshot reads **DURABILITY 40/40**. `save.json` re-read from disk: `dur 40 / 40`. |
| 3 | `maxLevel` below owned `currentLevel` → clamped + logged (club AND character) | **PASS** | Club: `currentLevel 10 → 8`, screenshot reads **Lv 8 /8**. Character: `char_james currentLevel 35 → 20`, screenshot reads **Lv 20 /20**. Both logged. |
| 4 | Rarity downgrade orphaning SP → clamped against `RarityStatCaps` + logged, no refund | **PASS** | `spentStrength 30 → 18`, `spentClubControl 25 → 19` (Common caps 25/25 minus bases 7/6). `totalSPEarned` re-read from disk = **74, unchanged** — nothing refunded. `RarityDowngrade_DoesNotRefundTheOrphanedSp` pins it. |
| 5 | `is_active=false` on an OWNED club → gone from shop, still renders in bag, still equipped | **PASS** | `club_wood_golfin_common` published inactive. `GetAllClubs()=799` / `GetAvailableClubs()=798`. Screenshot: WOOD GOLFIN still in the carousel with real art at Lv 10. `save.json`: `slot 1` — still equipped. Shop: `shop_ball_putt_ace` (inactive) withheld, 1 of 5 rows listed. |
| 6 | A tournament entry in flight is unaffected by a character stat publish (test pins the snapshot) | **PASS** | `TournamentSnapshotImmunityTests` — 4 tests. `TheClampStepDoesNotTouchTournamentSnapshots` clamps the roster row hard (35→20, SP 30→5) and asserts the entry still reads Level 42 / STR 20. Nothing in production was changed. |
| 7 | An overlay row whose sprite does not resolve keeps the BUNDLED row — no `Placeholder` | **PASS** | `club_iron_golfin_common` published with `portraitSprite=S_Menu_DoesNotExist_PHASE2` **and** `basePower=999`. Probe: `basePower=38`, `portrait='S_Menu_Iron_GOLFIN'`, `spriteResolved=True` — the whole row reverted. Appended row `club_ghost_phase2` with an unresolvable sprite: **absent** from the database. Both logged. |
| 8 | Shop window honoured: future `startAt` hidden, past `endAt` hidden, sale price ignored outside its window, unparseable bound drops the row | **PASS** | Live probe, 1 of 5 listed. `iron9_klyro` past `endAt` → hidden. `awedge_fyloe` future `startAt` → hidden. `pwedge_royal` `endAt="next tuesday"` → dropped, fail closed. `driver_gf` listed with `saleWindowOpen=False, hasSale=False, effective=100` (rpCost, **not** saleRpCost 80). 17 unit tests cover the edges. |
| 9 | Per-catalog kill drops that catalog's cache — **or the gap is reported** | **PARTIAL — half implemented, half REPORTED** | See **Finding 2**. The client now drops a requested-but-absent catalog's cache, which closes the "applies forever" hole. The payload still cannot distinguish `is_enabled=false` from an unknown catalog name; that is reported, not guessed. |
| 10 | DB-before-Manager ordering identified and asserted at runtime; log the assert | **PASS** | See **Finding 1**. `ClubDatabaseCSV.IsLoaded` / `CharacterDatabaseCSV.IsLoaded` asserted in both managers (LogError); `ContentCatalogStore.RequireReady` asserted in all six databases. Boot log: `Awake — LocalizationManager already initialised (order OK: LocalizationBootstrap -1000 → ContentService -900)`. |
| 11 | Airplane mode / corrupt cache / missing version file → bundled catalogs, warning, no exception | **PASS** | Existing suites still green (`Content fetch failed (Network, HTTP 0)`, `Could not parse the … payload`, `Skipping unparseable content_version line`). New: `GarbageAndEmptyInput_ReturnUnparsedWithoutThrowing` over 6 malformed bodies; boot-time "cache carries no `<catalog>` catalog" path. Baseline run with all six caches deleted booted clean on bundled data. |
| 12 | Boot time measured before and after (clubs is 799 rows — measure, do not assert) | **PASS** | Measured, three runs — see **Boot cost** below. |
| 13 | Full unfiltered EditMode sweep green; fix the 17 PRE-EXISTING failures | **PASS** | `1692 total · 1689 passed · 0 failed · 3 skipped`. Baseline before any edit was `1615 / 1595 / 17 / 3`. See **Pre-existing failures**. |
| 14 | Screenshot of an admin-published club stat rendering in-game | **PASS** | `screenshots/clubs_published_stat_and_clamp.png`. |
| 15 | Spec deviations flagged at the bottom | **PASS** | See **Deviations**. |

---

## Boot cost (acceptance item 12)

Three real boots of `ShellScene`, same machine, same session. The number is
`ContentService.BootCostMilliseconds` — the entire synchronous Awake.

| Run | Caches on disk | Total | Catalogs' share | clubs |
|---|---|---|---|---|
| Baseline (Phase-1 state) | texts only | **49.82 ms** | 0.17 ms | 0.09 ms |
| Steady state | all 7, at cursor parity (empty) | 60.82 ms* | 4.03 ms | 3.02 ms |
| **Worst case** | all 7 **FULL** — 799 clubs / 501 texts / 638 KB | **102.80 ms** | **42.70 ms** | **40.55 ms** |

\* the steady-state run also carried a 4-row synthetic clubs overlay, so its 3.02 ms is a first-parse
cost, not a parity cost.

**Reading it honestly.** Phase 2 adds **~42.7 ms to the boot critical path in the worst case**, and
40.55 ms of that is clubs alone — 638 KB of JSON through Newtonsoft. The worst case only occurs when
the client's cursor is BEHIND the server: a fresh install, or the first launch after a full
re-publish. At cursor parity the payload is empty and the six catalogs cost 0.17 ms, and I3's export
step resets every cursor to parity on each release, so the 40 ms is genuinely the exceptional path.

It is still 40 ms on the boot path and I am not going to call that free. Named as a follow-up rather
than fixed here, because fixing it means moving the catalog read off the synchronous Awake and
gating six databases on it — a re-entrancy change with its own spec.

---

## The three findings

### Finding 1 — the DB-before-Manager guarantee was a comment, and now it is an assert

The spec asked where the guarantee comes from. It is **none of the places you would look**:

- NOT `[DefaultExecutionOrder]` — neither `ClubDatabaseCSV` nor `ClubManager` has one.
- NOT Project Settings — **this project has no `ProjectSettings/MonoManager.asset` at all.**
- It is the `executionOrder:` field committed into each script's `.cs.meta`:
  `ClubDatabaseCSV.cs.meta` = −90, `ClubManager.cs.meta` = −80, `CharacterDatabaseCSV.cs.meta` = −200,
  `CharacterManager.cs.meta` = −100, `SaveDataHost.cs.meta` = −100.

Those values were written **once**, by the `GOLFIN ▸ Setup ▸ Club Managers` menu item
(`ClubManagerSetup.cs`), and **nothing re-asserts them** — unlike `SaveDataHost`'s, which
`SaveDataHostExecutionOrder.cs` re-applies on every reload via `[InitializeOnLoad]`. A regenerated or
merge-mangled `.meta` silently drops both to 0, where the relative order is undefined, the catalog
reads empty, seeding grants nothing, and the player has no clubs with not one error in the log.

So the invariant is now checked at runtime in three places, exactly as Phase 1 checked
`LocalizationManager.IsInitialized`:
`ClubDatabaseCSV.IsLoaded` / `CharacterDatabaseCSV.IsLoaded` (asserted by their managers), and
`ContentCatalogStore.RequireReady()` (asserted by all six databases, and deliberately quiet when there
is no `ContentService` at all — a physics-lab scene running bundled is correct, not broken).

**Also surfaced, not fixed:** `CharacterManager` and `SaveDataHost` are **both at −100**. That is a
tie, and `CharacterManager.LoadRoster` reads `SaveDataHost.Instance.Data`. It works today by scene
component order. It is a latent ordering hazard of exactly the class this spec exists to close, but
re-ordering a manager is a behaviour change I am not making inside a content spec. Flagged for a
follow-up.

### Finding 2 — the kill-switch gap: half of it is now closed, and the other half is reported

The spec's premise was "this client treats absent as *no update*". Measured against prod on
2026-08-26, the wire says more than that:

```
since=clubs:1  (== the server's own version) → {"clubs":{"version":1,"full":false,"changed":[]}}
catalogs=nosuchcatalog                       → {"catalogs":{}}
```

**"Nothing changed" is PRESENT-AND-EMPTY. ABSENT means the server did not serve that catalog.** Those
are different answers, so the client can act without guessing: a catalog this build explicitly
requested that comes back absent while `enabled:true` is now read as **WITHDRAWN**, and that
catalog's cache is dropped. That closes the hole the spec named — the last good overlay no longer
applies forever.

**What the payload still cannot express, and is therefore reported rather than guessed:** absent
conflates **three** server states — `content_catalogs.is_enabled=false`, a catalog name this server
has never heard of, and a server-side omission bug. All three get the same (safe) treatment, but the
client cannot tell them apart, so it cannot log *which* one happened, and an operator flipping
`is_enabled` gets no positive confirmation the client obeyed.

**What the API would need** (no endpoint/schema change was made — out of scope): either a per-catalog
`"enabled": false` inside the catalog object, or a top-level `"disabled": ["clubs"]` list. Either
one is a one-field addition and the client side is already written to take it.

### Finding 3 — `GetMaxLevel` ignored the catalog, and the overlay is what made that visible

Caught by looking at the whole frame rather than just the feature: the roster showed **`Lv 20 /39`**
after the clamp had brought `char_james` down to the published ceiling of 20.

`CharacterManager.GetMaxLevel` read `GetMaxLevelForRarity(rarity)` unconditionally and **never looked
at the CSV's `maxLevel` column at all.** That was invisible while the bundled CSV agreed with the
rarity table (Common 39, Uncommon 79, …). Phase 2 is what lets them disagree, and the resulting bug
is worth naming: a published `maxLevel` of 20 clamps the SAVE to 20 on load, while the roster keeps
showing `/39` and LEVEL UP keeps selling levels 21–39. The player climbs back above the published
ceiling, is silently clamped down again on the next launch, and has spent the RP each time — forever.

Fixed: `GetMaxLevel` now returns the catalog's `maxLevel` when the row carries one and falls back to
the rarity table otherwise. The clamp and the UI now read the same ceiling.
`screenshots/roster_published_maxlevel_ceiling.png` shows `Lv 20 /20` with LEVEL UP correctly greyed;
`screenshots/roster_rarity_downgrade_clamp.png` is the same screen before the fix, showing `/39`.

Clubs were already correct — the club panel read the DB's `maxLevel` all along (`Lv 8 /8`).

---

## Pre-existing failures (acceptance item 13)

The baseline sweep, run **before any edit**, was `1615 total · 1595 passed · **17 failed** · 3 skipped`.
All 17 were stale assertions, not code bugs, and all 17 are fixed:

- **16 × schema literal.** `SaveSchemaMigrator.CurrentSchemaVersion` is **10**
  (`starting_character_selection`); the tests asserted the literal **9** (Order 761's wedge backfill).
  Fixed by binding to `SaveSchemaMigrator.CurrentSchemaVersion` rather than a new literal — a new
  literal would just go stale on the next migration. The two sentinels
  (`CurrentSchemaVersion_Is9` → `CurrentSchemaVersion_IsMonotonicAndAtLeastV10`) became **floors**
  rather than pins: what is worth catching is the version going BACKWARDS, and a floor catches that
  without breaking on every legitimate bump. `T5_FailHard_V10Json_…` became
  `T5_FailHard_FutureVersionJson_…` and computes `CurrentSchemaVersion + 1`, because "a newer build's
  save" is that by definition and naming `10` stopped being a future version the day v10 shipped.
- **1 × stale character data.** `TournamentServiceWireupTests.Compose_Register_SnapshotHasCorrectStats`
  asserted `char_james` STR 6 / CC 7 / REC 6 / STA 6. The shipped row is **7 / 6 / 5 / 7** — it was
  rebalanced and the test was really asserting "Characters.csv has not changed since 2026-07". Fixed
  by reading the expected values from `CharacterDatabaseCSV` (the same database the provider reads),
  so a balance change — bundled **or published through the overlay** — can no longer make it red,
  while a genuinely broken snapshot still does.

A red suite masks regressions, and this one had been red long enough to hide anything. It is green now
and the shape of these fixes is meant to keep it that way.

---

## Deviations from the spec

1. **§7 is half-implemented rather than fully implemented or fully reported.** The spec offered
   "make them agree, **or** report". The wire turned out to allow more than the spec assumed (absent
   ≠ no update) and less than it wanted (no per-catalog disabled marker), so I did both: implemented
   what the payload can express, reported precisely what it cannot. Detailed in Finding 2.
2. **`CharacterManager.GetMaxLevel` was changed**, which is a behaviour change slightly outside "wire
   up the overlay". It is in scope in the sense that without it the overlay is only half-applied —
   the clamp obeys the publish and the UI does not — and the divergence costs the player RP. Detailed
   in Finding 3.
3. **`GeneralShopModel` was switched from column indices to column names** and given a quote-aware CSV
   splitter. Required by the overlay (which is keyed by name), and the file had already grown four
   columns since the index reader was written.
4. **`startLevel` is now parsed** from Clubs.csv / Characters.csv into the runtime rows. The column
   has shipped all along and was never read. It is used **only as the clamp's lower bound**, with a
   rarity-table fallback; `BuildSpec` / seeding still use the rarity table, so how a NEW club or
   character is granted is unchanged.
5. **Club SP is clamped against the flat `PlayerClubData.MAX_SP_PER_STAT` (20), not `RarityStatCaps`.**
   The spec's SP row says "re-clamp against `RarityStatCaps` for the row's CURRENT rarity"; that is
   the CHARACTER rule. Clubs cap SP flat per stat regardless of rarity, so a club rarity change cannot
   orphan club SP. The clamp still runs to catch negative/corrupt values.
6. **The acceptance run used synthetic local cache files, not a real admin publish.** Publishing to
   the live catalog is an operator action against prod and I did not take it. The caches were written
   in exactly the shape the server produces (verified by curl and by the round-trip test), so the
   client path is fully exercised — but see **Needs manual verification** below.
7. **`ItemManager` / `BallManager` / `BagManager` still seed from `GetAll*`, not `GetAvailable*`.**
   Those are dev-grade "grant everything with test quantities" paths and player inventory is out of
   scope (Phase 4). The `GetAvailable*` accessors exist and are the seam when it is in scope.
8. **Gacha pools were not filtered by `is_active`.** `GachaMockPrizePool` is 10 hard-coded entries —
   there is no catalog-driven pool to filter yet. `GetAvailableClubs()` / `GetAvailableCharacters()`
   are the seam for when there is.

---

## Needs manual on-device verification

Everything below was verified in the Editor against synthetic caches shaped exactly like the server's
output. These are the parts that can only be closed by a real publish on a real device:

1. **A real admin publish, end to end.** Change a club stat in the admin panel → publish → relaunch
   the app on device → confirm the new value. The client half is proven; the admin → API → device leg
   is not, in this task.
2. **The per-catalog kill switch.** Flip `content_catalogs.is_enabled=false` for one catalog and
   confirm the client drops that catalog's cache on the next launch and reverts to bundled. This is
   the direct test of Finding 2, and it also settles whether a disabled catalog really is absent (the
   assumption the WITHDRAWN handling rests on) or something else.
3. **Boot cost on device.** The 40.55 ms clubs figure is Editor-measured on a Mac. A phone's
   JSON-parse throughput is the number that matters for a fresh install.
4. **The upgrade path for an existing player.** A device with a Phase-1 `content_texts.json`
   (whole-body) must keep its text overlay after this build lands. Covered by
   `APhase1WholeBodyTextsCache_StillParses` and by the live cache on this machine, which is exactly
   that shape — but worth one real device.
5. **A shot taken with a published club.** Acceptance item 1 says "in the shot the club takes". The
   Clubs screen shows POWER 95; the physics path reads the same `ClubDataRuntime`, so it follows —
   but it was not driven through an actual shot.
