# SPEC — `gacha_client_real_pull`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-31 (Architect via Cowork). Spec **C** of `Docs/GACHA_ADMIN_PLAN.md` §8.
> **Needs A (`gacha_admin_catalogs`) and B (`gacha_server_pull`) DONE and deployed** — this is the
> client half of both. Decisions of record: plan §9 (5b overlay + gacha-only re-apply; banner text
> UI-authored, **title only** — no tagline on the card, Cesar 2026-08-31; ticket ledger is the
> truth; dupes → RP; pity may be none).
>
> PIPELINE_HARDENING §21: the live E2E runs from the Editor against prod (Cesar's standing rule:
> no device pass by default). Standing string rule: every new player string goes through
> `LocalizationText.csv` EN + JA → `import_content.py` → publish → `--check` clean.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

The gacha becomes real on the client: banners come from the published catalog (bundled floor +
overlay + the 5b same-session re-apply), the card shows admin-authored title, art and costs and
is **withheld** when anything it needs is missing; PULL calls `POST /api/v1/gacha/pull` and the
reveal shows exactly what the server granted; tickets are read from the server ledger; history is
the server's; the mock pool, the mock history and the dev ticket grant are gone.

## 1. What is true today (verified 2026-08-31, plus what A and B deliver)

| Piece | State |
|---|---|
| `GachaBannerCatalog` (`GachaBannerModel.cs`) | after A: header-indexed, quote-aware; still bundled-only (`Resources.Load("Data/gacha_banners")`), no `ContentCatalogStore`; `GetLiveBanners` = `Active && EndUtc > now`; `Reload()` exists and `GachaCarouselController.OnEnable` calls it |
| `GachaBannerCard.Bind` | title = `entry.NameKey` literal; art `Resources.Load<Sprite>("Art/Gacha/Banners/" + ArtSprite)`; `_costX1Text` / `_costX10Text` wired but unused ("COST" authored label); `_rulesButton` → `Application.OpenURL` |
| `GachaPullFlow.Pull(count)` | `BuildResult` → `GachaMockPrizePool` → `GachaRevealModalController.Instance.Play(prizes, onFinished)` → `GachaPrizesScreenController.SetPendingResult` + `ShowScreen(GachaPrizes)` |
| `PrizeRecord` | `readonly struct { string ClubId }`; `GachaPrizesScreenController.BindCard(BagClubCard, PrizeRecord)` is the shared binder; `ResolveRarity` via `ClubDatabaseCSV` |
| Tickets | `GachaTicketManager` over `SaveData.ticketBalances` (dev grant 10 at `Awake` + two `SaveSchemaMigrator` blocks, paired TODOs); `PersistentUIManager` shows Standard via `OnTicketsChanged`; `InventoryProjector` projects `ticketBalances` into the blob (additive **max** merge) |
| History | `GachaHistoryStore.All` = 12 mock records; `GachaHistoryScreenController.SpawnRow` handles Club (`GachaHistoryRow`) and Ball (`GachaHistoryRowBall`), skips the rest |
| Overlay precedent | `ClubDatabaseCSV.LoadCSV` (`:92–97`): `ContentCatalogStore.RequireReady(nameof(...)) ? ContentCatalogStore.Catalog(ContentCatalogs.Clubs) : null`, patch by id, append, `is_active=false` drops; `ContentCatalogs.All` (`ContentCatalogs.cs:85`) is the `catalogs=` request list — NOTE: it lists nine while `Missions*` consts exist at `:66`; register the four gacha catalogs exactly the way `missions_v1` registered its seven |
| Art by URL | `CatalogArtCache.Cached(url, bundledUrl)` / `Cached(url)` (`Assets/Scripts/CatalogArt/CatalogArt.cs:87/102`), ladder as `ClubDatabaseCSV.cs:235` |
| Same-session refresh | `ContentService.OnCacheRefreshed` (static event, fires after a fetch wrote a NEWER cache); `RemoteContentSource.ReadCache(catalog)` reads the on-disk cache; `ContentCatalogStore.Install(catalog)` installs one catalog |
| Server precedent | `ShopPurchaseService` (`Assets/Scripts/Economy/`): `Instance`/`ConfigureForTest`/`ResetForTest`, `PurchaseAsync` + `PurchaseRoutine` with the `PointsBackendFlag.Enabled` gate INSIDE the routine, in-flight latch, `_client.Post<T>(Endpoints.X, body, cb)`, `PointsService.Instance.ApplySpendResult`, `InventorySyncService.Instance.DrainGrants(done)` |
| From B | `POST /gacha/pull` → §2.4 of spec B (`prizes[]` in reveal order, `ticket_balance`, `pity`, `rp`); `GET /gacha/history`; `GET /gacha/tickets`; statuses `ok, replayed, invalid_count, unknown_banner, not_available/<reason>, pull_cap, cost_changed, insufficient` |
| Shop card | `GeneralShopCard.Bind` → `BindClub/BindBall/BindCharacter/BindItem` + `BindPrice`; the four kinds already render on one prefab family |

## 2. Catalog overlay + 5b re-apply

- `ContentCatalogs`: consts `GachaBanners = "gacha_banners"`, `GachaRates`, `GachaPools`,
  `TicketTypes`; added to the request list and `IsKnown`.
- `GachaBannerCatalog.LoadFromCsv` → the club-loader shape: bundled rows parsed (A's parser),
  then `RequireReady(nameof(GachaBannerCatalog)) ? Catalog(GachaBanners) : null`; patch by
  `bannerId`, append overlay-only rows, `is_active=false` drops the row; `RequireReady` false in
  EditMode keeps bundled. `GachaBannerEntry` gains the A columns: `StartUtc` (DateTime, MinValue
  when blank), `PoolId`, `TicketType` (int), `PityThreshold` (int, 0 = none), `PityMinRarity`,
  `GuaranteeMinRarityX10`, `MaxPullsPerPlayer` (int?), `ArtUrl`, `NameEn`, `NameJa`,
  `FeaturedRefIds`. (`taglineEn/Ja` are parsed into nothing — title only.)
- New tiny loaders, same shape, read-mostly: `GachaRatesCatalog` (`poolId → rarity → bp`),
  `GachaPoolCatalog` (`poolId → entries {kind, refId, rarity, weight, quantity, minBuild via
  overlay row}`), `TicketTypeCatalog` (`id → {key, nameEn, nameJa, iconSprite, iconUrl}`) from
  `Resources/Data/{gacha_rates,gacha_pools,ticket_types}.csv` + overlay. One file
  `Assets/Scripts/UI/Gacha/GachaContentCatalogs.cs` is fine.
- **5b re-apply.** `ContentService` gains
  `public bool TryReinstallFromCache(string catalog)` — allowed ONLY for the four gacha catalogs
  (a `static readonly HashSet` with a comment saying why: no owned-state dependency, so the I5
  no-live-swap rule does not apply): `RemoteContentSource.ReadCache(catalog)` → parse the way
  `Awake` does → `ContentCatalogStore.Install(...)` for that one catalog → true. Anything else →
  false + `LogWarning`. `GachaBannerCatalog` subscribes to `ContentService.OnCacheRefreshed` in a
  static ctor / first `EnsureLoaded`, sets `s_refreshPending`; `Reload()` (already called by the
  carousel's `OnEnable`) calls `TryReinstallFromCache` for the four when the flag is set, then
  re-reads. Net effect: a banner published mid-session appears the next time the Rewards Center
  opens after the background refresh landed.

## 3. Card — title, art, costs, withhold

`GachaBannerCard.Bind(entry)`:

- **Title**: `LocalizationManager.CurrentLanguage == Japanese ? NameJa : NameEn`; blank → the
  other; both blank → `LocalizationManager.Get(NameKey)` if the key exists, else `NameKey`
  literal (today's behaviour, for rows nobody has edited). Re-bind on
  `LocalizationManager.OnLanguageChanged` (subscribe `OnEnable`, unsubscribe `OnDisable`).
- **Art**: `CatalogArtCache.Cached(entry.ArtUrl, bundledUrl)` ladder exactly as
  `ClubDatabaseCSV.cs:235` → bundled `Resources` sprite → **null = withheld** (§3.1). Never the
  `Placeholder` sprite, never a blank card.
- **Costs**: `_costX1Text.text = CostX1.ToString()`, `_costX10Text.text = CostX10.ToString()`.
  NOTE: inspect `GachaBannerCard.prefab` — those two fields were wired in Stage 2 "for a numeric
  variant"; confirm which TMP objects they point at and that the authored "COST" label + ticket
  icon stay. Screenshot for Cesar (this is the one visual he has not seen).
- **Ticket icon**: from `TicketTypeCatalog[entry.TicketType].iconSprite` (bundled) via the same
  `Cached(iconUrl, bundled)` ladder; blank → keep the prefab's authored icon (Standard). Only
  Standard has authored art today; Gold falls back to it until an icon is uploaded — acceptable,
  say so in the report.
- **Guarantee lines** (added 2026-08-31 after the A review — the card carries two authored
  lines with placeholder "99 pulls" pills, keys `GACHA_PITY_A_RANK` / `GACHA_PITY_S_RANK`,
  visible in `Docs/Specs/Active/gacha_admin_catalogs/screenshots/rewards_center_after.png`):
  they bind to the row. Line 1 = pity: visible iff `PityThreshold > 0`, text
  `GACHA_CARD_PITY` "Guaranteed {0} or higher within" + pill `{1} pulls` (`GACHA_CARD_PULLS`
  "{0} pulls"), `{0}` = `PityMinRarity` localised name, `{1}` = `PityThreshold`. Line 2 = x10
  guarantee: visible iff `GuaranteeMinRarityX10` set, `GACHA_CARD_GUARANTEE_X10` "Every 10-pull
  includes at least one {0}", no pill. A banner with neither shows neither line and the block
  collapses (check the prefab's layout group; if the lines are absolutely positioned, hide
  without reflow — do not rebuild the card). The third authored line (`GACHA_PRIZE_PREVIEW`)
  stays as it is. The old `GACHA_PITY_*` keys stay in the CSV, unused. Strings EN + JA via the
  importer (§5 gains these three).
- **Rules**: unchanged (`rulesUrl`); an empty `rulesUrl` hides the button (spec D adds the
  in-app modal).

### 3.1 Withhold rule (the invariant) — `GachaBannerCatalog.GetLiveBanners`

A banner is live only when ALL hold: `Active`; `StartUtc ≤ now < EndUtc` (device clock — the
carousel's countdown already ticks `EndUtc`; add the start side, and the countdown label for a
SCHEDULED banner is not shown because the banner is not); `PoolId` resolves and its rate table
sums to 10 000 with **every rarity with `rateBp > 0` having ≥ 1 pool entry whose ref resolves in
this build's DB and is active** (the server's step 8, evaluated locally — clubs via
`ClubDatabaseCSV.GetClub`, balls/characters/items via their DBs and `renderable`, tickets via
`TicketTypeCatalog`); `TicketType` resolves; art resolves (§3). Anything false → withheld,
counted, ONE `LogWarning` per load in the club-loader shape (*"withheld: <bannerId> — <reason>"*).
`_emptyState` ("No active banners") shows when nothing survives — it already exists.

Pure seam for tests: `internal static bool IsRollable(GachaBannerEntry, IRefResolver)` — keep the
DB lookups behind a small interface so EditMode fixtures can drive it.

## 4. The real pull

### 4.1 `GachaPullService` (`Assets/Scripts/Economy/GachaPullService.cs`, `Golfin.Economy`)

Mirror `ShopPurchaseService` line for line in shape: `Instance` over `ApiClient.Instance`,
`ConfigureForTest`, `ResetForTest`, `InFlight`, `PullAsync(bannerId, count, expectedCost, build,
Action<GachaPullOutcome> onDone)` + `PullRoutine`; **`PointsBackendFlag.Enabled` gate inside the
routine** (OFF → `Unavailable`, no network); fresh `idempotency_key` per attempt; body
`{banner_id, count, expected_cost, idempotency_key, build}` (`BuildPullJson` static, tested);
`_client.Post<GachaPullResult>(Endpoints.GachaPull, …)`. DTOs in `PointsDtos.cs`:
`GachaPullResult` (spec B §2.4 verbatim, `rp` nullable), `GachaPrizeDto`, `GachaTicketBalances`,
`GachaHistoryPage`. `Endpoints.GachaPull / GachaHistory / GachaTickets`.

Outcome: `Ok(result)`, `Insufficient(balance)`, `CostChanged(cost)`, `PullCap(limit, used)`,
`NotAvailable(reason)`, `Paused`, `Unavailable` (transport / flag off), `Disabled`.

On `Ok`, in this order, all before `onDone`: `GachaTicketManager.Instance.SetFromServer(ticketType,
ticket_balance)`; if `rp != null` fold the RP balance (NOTE: `PointsService` has
`ApplySpendResult`; find or add the symmetric earn-side fold — a `PointsBalance` update from the
`rp` fields — do not invent a second balance path); `InventorySyncService.Instance.DrainGrants(...)`
so every granted prize is in the bag before the Prizes screen enters; `GachaHistoryStore.Prepend(result)`.

### 4.2 `GachaPullFlow.Pull(count)` — the modal covers the round trip

```
Pull(count):
  entry  = the card's entry (pass it in: Pull(GachaBannerEntry entry, int count); the Prizes
           screen's "pull again" keeps the last entry + count)
  modal  = GachaRevealModalController.Instance
  modal.BeginWaiting()                       // scrim + bag shaking, no cards, SKIP hidden
  GachaPullService.Instance.PullAsync(entry.BannerId, count, count == 1 ? entry.CostX1 : entry.CostX10,
                                      ContentBuildNumber.Current, outcome => {
     Ok          → modal.Continue(prizes, onFinished: () => ShowPrizes(prizes))
     Insufficient→ modal.Abort(); toast GACHA_INSUFFICIENT_TICKETS
     CostChanged → modal.Abort(); GachaBannerCatalog.Reload(); rebind the card; toast GACHA_COST_CHANGED  (second tap pays the shown cost)
     PullCap     → modal.Abort(); toast GACHA_PULL_CAP
     Paused      → modal.Abort(); toast GACHA_PAUSED
     NotAvailable/Disabled → modal.Abort(); GachaBannerCatalog.Reload(); carousel rebuild; toast GACHA_UNAVAILABLE
     Unavailable → modal.Abort(); toast PointsSpendGate.OfflineMessage (existing copy)
  })
```

`GachaRevealModalController`: split `Play(prizes, onFinished)` into `BeginWaiting()` (opens the
modal and starts the idle bag shake loop — the existing first-shake animation, looped) →
`Continue(prizes, onFinished)` (the current sequence from "first card") → `Abort()` (fade out,
cleanup, same path as a force-close). `Play` stays as `BeginWaiting(); Continue(...)` for the
demo recorder. No spinner, no new UI. No modal in scene → today's degrade (Prizes directly).

`BuildResult` and `GachaMockPrizePool` are **deleted**; `SetPendingPullCount` goes with them
(`GachaTabController`'s dead `OnPullX1/OnPullX10` callers — delete those two methods too; they
have been dead since Stage 2).

### 4.3 Prizes — multi-kind

`PrizeRecord` → `readonly struct { string Kind; string RefId; int Quantity; CharacterRarity
Rarity; bool IsDupe; int DupeRp; }` built from `GachaPrizeDto` (rarity parsed by name via the
existing `RarityHelper` parse, default Common + warning). `ResolveRarity` returns
`record.Rarity` — the server's word, no DB lookup.

Shared binder `GachaPrizeCardBinder` (move `BindCard` out of the Prizes screen):

- `club` → `BagClubCard.prefab`, today's binding.
- `ball | character | item | ticket` → **`GeneralShopCard`** (the shop's prefab family, which
  already renders the first three) in display mode: price row + BUY hidden the way the club
  card's action row is hidden (`ActionButtonPaths` precedent), `interactable = false`. New
  `GeneralShopCard.BindTicket(ticketTypeId, quantity)` — icon from `TicketTypeCatalog` (ladder
  as §3), name `nameEn/nameJa`, quantity "×N"; this same method is what the shop's
  `category = ticket` rows (spec B G1-T) will bind with, so `GeneralShopCard.Bind` gains the
  `ticket` category too. NOTE: measure the shop card against the Prizes-screen grid slot and the
  reveal `CardAnchor` (183×410); if it does not fit, scale-to-fit inside the slot and say so with
  a screenshot — do NOT rebuild a card. This reuse is not in a Figma; Cesar may replace it later.
- **Dupe**: `IsDupe` → the card shows a pill **"+{DupeRp} RP"** using the shop card's
  existing `offer`/`popular` pill style (reuse the component, new text via `GACHA_DUPE_RP`
  with `{0}`); on the club card, add the same pill object at the top-right (copy the shop pill's
  RectTransform). Reveal FX tier is still the prize's rarity.
- Prizes screen grid: cards are instantiated per kind into the 10 slots instead of the prefab's
  fixed `BagClubCard` children — NOTE: read `_gridCards` wiring first; the least invasive route is
  to keep the 10 slot transforms and parent the right card prefab under each.

### 4.4 Tickets — ledger is the truth

- `GachaTicketManager`: `SetFromServer(int ticketType, int balance)` (writes `ticketBalances`,
  fires `OnTicketsChanged`); `RefreshFromServer()` → `GET /gacha/tickets` (auth) → `SetFromServer`
  for each; called once after auth at boot (where `PointsService` first fetches its balance — same
  hook) and in `GachaCarouselController.OnEnable`. **`SpendTickets` deleted**; `AddTickets` stays
  (the grant apply still compiles) but nothing new calls it. The dev grant is **removed at all
  three sites** (the paired TODOs: `Awake`, `SaveSchemaMigrator` v6→v7, v7→v8) and
  `GachaTicketTests` updated to expect 0.
- `InventoryProjector`: `ticketBalances` are **no longer projected into the blob and ignored on
  merge** — tickets are server-owned like RP (the max-merge would otherwise resurrect a
  pre-spend balance). Keep the local field as the last-known server value for the counter.
- `InventoryGrants.Apply` `KindTicket`: keep the case (old queued rows), but after a drain that
  applied any ticket grant call `RefreshFromServer()` so the counter converges to the ledger.
- `ShopTransaction.ApplyPurchaseGrant` (found in the B review: it has NO `ticket` case and falls
  to `default → Invalid`, which would show a paid ticket purchase as a failure): add the `ticket`
  case — credit nothing locally (the server already credited the ledger; `grant.id` is null by
  design, B §5.2), call `GachaTicketManager.Instance.RefreshFromServer()`, return success. This
  is what lets spec D set `TICKET_SHOP_BUILD` to this build.

### 4.5 History

`GachaHistoryStore`: `All` ← `GET /gacha/history?limit=100`, raw body mirrored to
`persistentDataPath/gacha_history.json` (`RemoteNoticeSource` shape: atomic `.tmp` + replace,
null on failure keeps what it has), mapped to `GachaHistoryRecord` (one record per prize;
`RewardType` from `kind`, `BannerId`, `TicketType`, `PullCount`, `PulledUtc`; dupes carry
`Quantity = 0` and a `DupeRp` field added to the record). `Refresh()` on
`GachaHistoryScreenController.OnEnable`; `Prepend(GachaPullResult)` after a pull so the log is
current without a refetch. Mock `BuildMock` deleted. `SpawnRow`: character/item/ticket use
`GachaHistoryRow` (the club row) bound by name/icon/rarity from their DBs — NOTE: read the row's
fields; if it is club-specific beyond name/icon/rarity, add a `BindGeneric(name, icon, rarity,
record)` rather than a third prefab.

## 5. Strings (importer path, EN + JA in one commit)

`GACHA_INSUFFICIENT_TICKETS` "Not enough tickets" / "チケットが足りません";
`GACHA_COST_CHANGED` "Price updated — tap again to pull" / "価格が更新されました。もう一度タップしてください";
`GACHA_PULL_CAP` "Pull limit reached for this banner" / "このバナーの上限に達しました";
`GACHA_PAUSED` "Gacha is paused. Please try again later" / "ガチャは一時停止中です。しばらくしてからお試しください";
`GACHA_UNAVAILABLE` "This banner is no longer available" / "このバナーは終了しました";
`GACHA_DUPE_RP` "+{0} RP" / "+{0} RP";
`GACHA_CARD_PITY` "Guaranteed {0} or higher within" / "{0}以上が確定・最大";
`GACHA_CARD_PULLS` "{0} pulls" / "{0}回";
`GACHA_CARD_GUARANTEE_X10` "Every 10-pull includes at least one {0}" / "10連ごとに{0}以上が1枚確定".
`--check` clean for `texts`; zero new hardcoded `.text` literals (grep quoted).

## 6. Tests (EditMode)

- Overlay: `ContentCatalogStore.ConfigureForTest` with a patched `costX1` → `GetLiveBanners`
  shows it; appended banner admitted; `is_active=false` dropped; `RequireReady` false → bundled.
- Withhold: fixtures for each clause of §3.1 (window not started, rates sum ≠ 10 000, Legendary
  rate > 0 with no resolvable Legendary entry, unknown ticket type, art null) → withheld with the
  named reason; the seed banners rollable with a fake resolver that knows the seed refs.
- Title ladder: JA with `nameJa` → JA; JA with blank `nameJa` → EN; both blank → `nameKey`.
- `TryReinstallFromCache`: refuses `clubs`; installs `gacha_banners` from a written cache.
- `GachaPullService`: `BuildPullJson` shape; each status → outcome; flag OFF → no transport, no
  modal continue; latch; `Ok` order (tickets set → RP fold → drain → history) asserted with fakes.
- `PrizeRecord` from DTO: unknown rarity → Common + warning; dupe pill text.
- Tickets: projector excludes tickets from the blob; merge ignores an incoming higher ticket
  value; `GachaTicketTests` at 0 on a fresh save.
- History mapping: one pull with 10 prizes → 10 records newest-first; dupe → `Quantity 0`, `DupeRp`.
- The 15 `GachaStage2Tests` and every reveal-modal test still pass.

## 7. Live E2E (§21) — Editor against prod, Cesar's account, pasted

1. Grant 500 Standard tickets from the admin → counter shows 500 after boot (no relaunch needed:
   `RefreshFromServer` on the Rewards Center).
2. PULL x1 on `banner_standard_club1` → modal waits, cards reveal the server's prize; counter
   450; the club is in the Bag after the drain; History shows it; SQL: pull + prize + grant rows.
3. PULL x10 → 10 cards in server order; a dupe shows "+N RP" and the RP counter moved.
4. Publish `costX1 = 60` in the admin → next x1 tap → `GACHA_COST_CHANGED` toast, card shows 60,
   second tap debits 60. **No build.**
5. Publish a NEW banner (draft → publish) while the Editor is running → background the Editor
   (or wait for the refresh) → re-open the Rewards Center → the banner is there (5b). Deactivate it
   → gone on the next open.
6. Upload art with no text + set `nameJa` → switch language in Settings → title swaps, art same.
   `banner_standard_club1` shows "Guaranteed Legendary or higher within [50 pulls]" + the x10
   Rare line; `banner_test_a` shows neither line; `banner_test_b` shows Rare/30 + Uncommon.
7. Pause from the ops panel → tap PULL → `GACHA_PAUSED`, nothing debited (SQL).
8. Airplane mode in the Editor (block the API host) → banners still show from cache, PULL →
   offline toast, counter unchanged.

Screenshots: card with numeric costs (for Cesar's approval), reveal of a ball/item/ticket prize
on the shop card, dupe pill, Prizes grid mixed kinds, History with a mixed pull.

## 8. Acceptance

- [ ] §7 steps 1–8 run and pasted (SQL + screenshots).
- [ ] §6 EditMode tests green; full unfiltered EditMode sweep green.
- [ ] `GachaMockPrizePool`, `GachaHistoryStore.BuildMock`, `SpendTickets`, the three dev-grant
      sites and `GachaTabController.OnPullX1/OnPullX10` are gone (grep quoted).
- [ ] Strings via the importer; `--check` clean; zero new `.text` literals.
- [ ] A banner whose pool lacks a resolvable entry for a rated tier is withheld in the Editor
      (rename a pool ref in a draft cache) with the warning; restore → back.
- [ ] `ContentCatalogs.RequestList` carries the four gacha catalogs (log line quoted).
- [ ] All `[SerializeField]` refs wired; no Console errors; spec deviations flagged with
      justification.

## Files this task touches

**New** — `Assets/Scripts/Economy/GachaPullService.cs` (+ tests),
`Assets/Scripts/UI/Gacha/GachaContentCatalogs.cs`, `GachaPrizeCardBinder.cs`, EditMode tests.

**Modified** — `GachaBannerModel.cs`, `GachaBannerCard.cs`, `GachaCarouselController.cs`,
`GachaPullFlow.cs`, `GachaRevealModalController.cs`, `GachaPrizesScreenController.cs`,
`GachaHistoryStore.cs`, `GachaHistoryRecord.cs`, `GachaHistoryScreenController.cs`,
`GachaHistoryRow.cs`, `GachaTicketManager.cs`, `GachaTabController.cs`, `SaveSchemaMigrator.cs`,
`InventoryProjector.cs`, `InventoryGrants.cs`, `PointsDtos.cs`, `Endpoints.cs`,
`ContentCatalogs.cs`, `ContentService.cs`, `GeneralShopCard.cs` (+ `BindTicket`),
`GachaBannerCard.prefab` / `GachaPrizesScreen` prefab as needed, `LocalizationText.csv`,
`Docs/AI_CONTEXT.md`, `Docs/TellCode.md`.

**Deleted** — `GachaMockPrizePool.cs`.

## Out of scope (do NOT do these)

- Tagline on the card (Cesar: title only). Any new card design; Figma work.
- In-app RULES & RATES modal, telemetry, `featuredRefIds` on the card, Gold ticket icon art
  (**spec D**).
- Publishing a `category = ticket` shop row / setting `TICKET_SHOP_BUILD` (after this build is
  archived — one-line commit on Cesar's word).
- Mission / tournament ticket grants.
- Any backend or dashboard change (A and B own them; if one is missing, stop and report).
