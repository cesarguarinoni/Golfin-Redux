# SPEC — `shop_server_purchase`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-27 (Architect via Cowork). Decisions of record, all Cesar's, same day:
> **Claude Code implements BOTH repos** (playlife backend + Unity); characters are buyable on
> **shop cards only** (the locked Roster card is a later quick spec); character/item cards
> **reuse the `GeneralShopCard_Club` hierarchy** (no Figma exists, none is coming for this);
> **add CHARACTERS + ITEMS filter chips**.
>
> Plan context: `Docs/CONTENT_PIPELINE_PLAN.md` §6 step 4d and §11.5 — this is that step.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Make the shop price **authoritative**. Today `ShopTransaction.TryPurchaseCatalogEntry` debits
the ledger for whatever number the client computed (`entry.EffectiveRpCost` from the bundled CSV
+ overlay) and then grants the item **locally**. The server never sees *what* was bought, only
that N RP left the balance with reason `shop_purchase`. A modified client can therefore grant
itself any club at any price, and the admin Shop panel has a red banner saying exactly that.

After this task a purchase is **one server call** — `POST /api/v1/shop/purchase` — that reads
the **published** `shop_catalog` row, computes the price from the **server clock** (listing +
sale windows), debits through the existing `spend_pts`, and writes the item into the **existing
`golfin_pending_grants` queue** in the same transaction. The client applies that grant exactly
the way it already applies admin grants, records the grant id, and acks. The client never
decides a price and never grants itself a shop item again.

Second goal, same task: rows with `category = character | item` — which the admin Shop panel
can already publish — become **visible and buyable** in the STORE tab. Today the client parses
any non-`ball` category as `Club` (`GeneralShopCatalog.ParseCategory`), so a published
character row would render as a broken club card. That is a latent bug this task closes.

## Why this shape, in three sentences

The grants queue already exists, is additive-only, idempotent on both sides, drained at boot,
and knows how to apply all four kinds we sell (`InventoryGrants.ApplyOne` handles club,
character, item, ball). A purchase is "a grant the player paid for", so delivery rides the
queue: if the app dies between debit and apply, the next boot delivers it — a paid-but-missing
item is structurally impossible. And because the debit and the grant insert live in **one
Postgres function**, there is no window where the RP is gone and the grant does not exist.

This is also the pattern the *next* spends copy (character/club level-up, hole unlocks):
server-recorded action → grant/record → client applies. "Move progression to the server" is
this endpoint's shape applied to `LevelUpCosts`, not a different system.

---

## 1. What is true today (read 2026-08-27, not assumed)

| Piece | Where | Behaviour |
|---|---|---|
| Purchase seam | `Assets/Scripts/UI/Shop/ShopTransaction.cs` → `TryPurchaseCatalogEntry` | pre-checks (unknown ref, `ClubManager.IsOwned`, `rpm.GetPoints() < cost`) → `PointsSpendGate.Spend(cost, SpendReasons.ShopPurchase, …)` → on approve `rpm.SpendPoints(cost)` + `ClubManager.GrantClub` / `GrantBall` |
| Spend gate | `Assets/Scripts/EconomyRuntime/PointsSpendGate.cs` | flag OFF → synchronous local; flag ON → `PointsService.SpendAsync` → `POST /points/spend {amount, reason, idempotency_key}`; one process-wide `_inFlight` latch; owns the two refusal toasts |
| Catalog | `Assets/Scripts/UI/Shop/GeneralShopModel.cs` (`GeneralShopCatalog`, `ShopCatalogEntry`, `ShopCategory {Club, Ball}`) | bundled `Resources/Data/shop_catalog.csv` + `shop_catalog` overlay; windows evaluated ONCE at load via `ContentShopWindow.Evaluate`; **`ParseCategory`: anything ≠ "ball" → Club** |
| Cards | `Assets/Scripts/UI/Shop/GeneralShopCard.cs` (`Bind` → `BindClub` / `BindBall`) | templates `Resources/Prefabs/Shop/GeneralShopCard_Club` + `_Ball`; owned state only for clubs |
| Screen | `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs` | chips `ALLChip / CLUBSChip / BALLSChip` under `ContentArea/BarsArea/FilterGroup/CategoryRow`; `HandleBuy` switch on `GeneralPurchaseResult` |
| Server spend | playlife `routers/points.py` `POST /spend` → `public.spend_pts(p_user_id, p_amount, p_reason, p_key)` | row-locked, idempotent by `(user_id, idempotency_key)`, returns `{status: ok\|insufficient, spent, from_activity, from_gift, activity_pts, gift_pts, total_points, replayed}` |
| Published content | `content_rows(catalog, row_id, data jsonb, min_build, is_active, version)`; `content_catalogs(name, published_version, is_enabled)`; global switch `content_settings.content_enabled` (`routers/content.py::_global_enabled`) | `data` values are **strings** (CSV cells) |
| Grants | `golfin_pending_grants(id, user_id, kind ∈ club\|character\|item\|ball\|ticket\|hole, ref_id, amount>0, note, created_by, created_at, applied_at)`; `routers/golfin_inventory.py` GET `/user/golfin-grants` + POST `/user/golfin-grants/ack` | client: `Golfin.InventorySync.InventoryGrants.Apply` (records `SaveData.appliedGrantIds`), drained once per boot by `InventorySyncService.DrainGrants` |
| Admin | `Tools/admin-dashboard/app/(panels)/shop/shop-panel.tsx`; `lib/contentValidate.ts` `SHOP_CATEGORY_TO_CATALOG = {club, ball, item, bag, character}`; `lib/i18n.ts` `sh.notice.*` | category picker + `RefPicker` typeahead already cover characters/items; red "NOT enforced" banner |
| Build number | `Assets/Scripts/ContentRuntime/ContentBuildNumber.cs` → `ContentBuildNumber.Current` | already sent as `build=` on `/content` |

`shop_catalog.csv` today: 4 club rows + 1 ball row, columns
`entryId,category,refId,rpCost,saleRpCost,sortOrder,popular,offer,rarity,startAt,endAt,saleStartAt,saleEndAt`.
There is **no** `stockLimit` / `minPlayerLevel` / `sectionKey` column (plan §11.2 listed them;
`content_panels_gaps` shipped only the four window columns). This spec does not add them.

---

## 2. Backend (playlife) — migration, function, router

### 2.1 Migration `backend/migrations/2026_08_27_golfin_shop_purchase.sql`

Idempotent, header in the house style (what/why/STATUS line, verification block at the end).
Print the **full SQL in the chat message** when done — Cesar pastes it into the Supabase SQL
editor (`WORKFLOW_NOTES.md` SQL rule); the file in the repo is the archive.

```sql
create table if not exists public.golfin_shop_purchases (
  id              uuid        primary key default gen_random_uuid(),
  user_id         uuid        not null,
  entry_id        text        not null,          -- shop_catalog.entryId
  category        text        not null,          -- club | character | item | ball
  ref_id          text        not null,
  amount          integer     not null default 1 check (amount > 0),
  charged_rp      integer     not null check (charged_rp > 0),
  list_rp         integer     not null,
  on_sale         boolean     not null default false,
  build           integer     not null default 0,
  idempotency_key uuid        not null,
  grant_id        uuid        not null,          -- golfin_pending_grants.id
  created_at      timestamptz not null default now(),
  unique (user_id, idempotency_key)
);
create index if not exists golfin_shop_purchases_user_ref_idx
  on public.golfin_shop_purchases (user_id, category, ref_id);
alter table public.golfin_shop_purchases enable row level security;   -- RLS on, NO policies (service_role only), same posture as the grants table
```

`public.golfin_shop_purchase(p_user_id uuid, p_entry_id text, p_build int,
p_expected_rp int, p_key uuid) returns json`, `security definer`, `set search_path = public`,
EXECUTE revoked from `public`/`anon`/`authenticated` (same as `spend_pts`). Body, in order —
**every refusal returns json, only genuine faults raise**:

1. **Replay.** `select … from golfin_shop_purchases where user_id = p_user_id and
   idempotency_key = p_key`. Found → return the §2.3 `ok` shape rebuilt from that row + its
   grant (re-read `golfin_pending_grants` by `grant_id`) with `replayed: true` and the
   **current** balances from `profiles`. No second debit, no second grant.
2. **Kill switches.** `content_settings.value` for `key = 'content_enabled'` reads false
   (`routers/content.py::_global_enabled` — copy its truthiness rule; a missing row is enabled) **or**
   `content_catalogs.is_enabled = false` for `shop_catalog` → `{"status":"not_listed",
   "reason":"disabled"}`. When the operator has pulled remote content, the server must not
   sell from it either.
3. **Entry.** `content_rows where catalog = 'shop_catalog' and row_id = p_entry_id`.
   Missing → `unknown_entry`. `is_active = false` → `not_listed / inactive`.
   `min_build > p_build` → `not_listed / min_build`.
4. **Windows, server clock.** Same three rules as `ContentShopWindow.Evaluate`, verbatim:
   `startAt` inclusive, `endAt` **exclusive**, an absent/blank bound is unbounded, a
   present-but-unparseable bound **fails closed** (`not_listed / unparseable_bound` — for the
   sale bounds too). Parse with a `begin … exception when others then` block around
   `(data->>'startAt')::timestamptz`; treat the exception as unparseable. Out of listing window
   → `not_listed / window`. Sale is on iff both sale bounds admit `now()`.
5. **Price.** `list := (data->>'rpCost')::int`, `sale := nullif(data->>'saleRpCost','')::int`.
   `list <= 0` or unparseable → `not_listed / invalid_price`. `price := sale` when on-sale AND
   `sale > 0` AND `sale < list`, else `list`. (`HasSale` in `ShopCatalogEntry`, same rule.)
6. **Expected price.** `p_expected_rp is not null and p_expected_rp <> price` →
   `{"status":"price_changed","price":price,"list_rp":list,"on_sale":bool}`. The client
   showed a number; it must not be charged a different one silently.
7. **Category → kind + reference.** `category ∈ club|character|item|ball` else
   `unsupported_category` (`bag` is publishable but not grantable — `InventoryGrants` has no
   bag kind; refusing is correct). Resolve `ref_id := data->>'refId'` in
   `content_rows` catalog `clubs|characters|items|balls` (the `SHOP_CATEGORY_TO_CATALOG` map);
   missing or `is_active = false` → `not_listed / ref_inactive`.
8. **Uniqueness** (club, character only — items and balls stack): refuse `already_owned` if
   any of: a prior `golfin_shop_purchases` row for `(user_id, category, ref_id)`; an
   **unapplied** `golfin_pending_grants` row `(user_id, kind, ref_id)`; the player's
   `profiles.golfin_inventory` blob lists it — clubs under key `"clubs"`, characters under
   `"characters"`, each element either a bare id string or an object whose `"id"` matches
   (`InventoryCodec` wire keys: `KClubs="clubs"`, `KChars="characters"`, `KId="id"`; for a
   character object also require it is not marked un-owned — read the codec for the exact
   field name of the locked flag before writing this, and if it is absent treat presence as
   owned). The blob check is best-effort (client-asserted data); the ledger + grants checks are
   the authoritative ones. Wrap the blob walk in an exception block: a malformed blob must not
   block a sale.
9. **Debit.** `v_spend := public.spend_pts(p_user_id, price, 'shop:' || p_entry_id, p_key)`.
   `status = 'insufficient'` → return it **as-is** (nothing was written). The reason string
   `shop:<entryId>` is what the admin Points panel shows on the ledger row — that alone makes
   every purchase auditable with no admin change.
10. **Grant + ledger, same transaction.** `insert into golfin_pending_grants (user_id, kind,
    ref_id, amount, note, created_by) values (p_user_id, kind, ref_id, 1,
    'shop:' || p_entry_id, 'shop') returning id`; insert the `golfin_shop_purchases` row.
    A plpgsql function is one transaction: if either insert fails, `spend_pts`'s debit rolls
    back with it.

### 2.2 Router `backend/routers/shop.py`, mounted in `main.py` at `/api/v1/shop`

`POST /purchase`, `Depends(get_current_user)`, user id **from the token, never the body**
(same posture as `golfin_inventory.py`). Body:

```json
{"entry_id": "shop_club_iron9_klyro", "idempotency_key": "<uuid>", "build": 2113, "expected_rp_cost": 150}
```

`expected_rp_cost` optional. Validate: key parses as UUID (reuse `points._parse_key` or copy
it), `entry_id` non-blank ≤ 120 chars, `build ≥ 0`. Call the rpc, return `{"data": <json>}`.
**Every business outcome is HTTP 200** — insufficient / price_changed / not_listed /
already_owned are payloads the client branches on, exactly like `/points/spend` and the
`stale` inventory PUT. Only auth (401/403), validation (400) and faults (500) are HTTP errors.
`_missing_relation` courtesy is **not** wanted here: a purchase against an unapplied migration
must fail loudly (500), never "sell" nothing.

### 2.3 Response shapes

```json
{"status":"ok","entry_id":"…","category":"club","ref_id":"club_iron9_klyro",
 "charged":150,"list_rp":200,"on_sale":true,
 "grant":{"id":"<uuid>","kind":"club","ref_id":"club_iron9_klyro","amount":1,"note":"shop:shop_club_iron9_klyro","created_at":"…"},
 "spent":150,"from_activity":150,"from_gift":0,"activity_pts":…,"gift_pts":…,"total_points":…,
 "replayed":false}
{"status":"insufficient","requested":150,"shortfall":…,"activity_pts":…,"gift_pts":…,"total_points":…,"replayed":false}
{"status":"price_changed","price":200,"list_rp":200,"on_sale":false}
{"status":"not_listed","reason":"window|inactive|min_build|disabled|unparseable_bound|invalid_price|ref_inactive"}
{"status":"already_owned","ref_id":"…"}
{"status":"unknown_entry"} · {"status":"unsupported_category","category":"bag"}
```

The `ok` shape **contains every field of `PointsSpendResult`** so the client can fold the
balance with the code it already has.

### 2.4 Tests `backend/tests/test_shop_purchase.py`

Same fake-Supabase style as `test_golfin_inventory.py`. Router-level: 403 unauthenticated;
400 on a non-UUID key / blank entry; each rpc status passes through as 200 with `data`. The
plpgsql logic itself is proven by the migration's verification block + the device pass — do
not port the function to Python to test it.

### 2.5 Deploy + smoke

`flyctl deploy` from the Mac (`~/.fly/bin/flyctl`, not in the device_bash VM — see
`TellCode.md` 2026-08-18 leaderboard note), then: `/health` 200; `/notices`, `/banners`,
`/tournaments/golfin` still 200; `POST /api/v1/shop/purchase` **403 unauthenticated, 401 on a
bad token** (mounted + gated); garbage route 404. Confirm the new image via `flyctl status`,
never the deploy exit code.

### 2.6 Cutover step — close the legacy path (SEPARATE commit, ONLY on Cesar's word)

Once testers are on the build that carries §3, `routers/points.py` `POST /spend` refuses
`reason == "shop_purchase"` with 400 `"shop purchases go through /shop/purchase"`. Until then an
old build still self-grants at the CSV price; enforcement is only as good as the oldest build
in the wild. Do **not** ship this in the same deploy — it would break every installed shop.

---

## 3. Unity — client

### 3.1 `Golfin.Economy` — `ShopPurchaseService` (new file `Assets/Scripts/Economy/ShopPurchaseService.cs`)

Mirror `PointsService` exactly: `Instance` over `ApiClient.Instance`, `ConfigureForTest` /
`ResetForTest`, coroutine + `Async` wrapper, **flag gate inside the routine** so no entry point
can reach the network with `PointsBackendFlag.Enabled` off.

```csharp
public void PurchaseAsync(string entryId, int expectedRpCost, int build, Action<ShopPurchaseOutcome> onDone);
public static string BuildPurchaseJson(string entryId, int expectedRpCost, int build, string idempotencyKey); // {entry_id, expected_rp_cost, build, idempotency_key}
```

- New DTO `ShopPurchaseResult` in `PointsDtos.cs`: the §2.3 fields (`Status`, `EntryId`,
  `Category`, `RefId`, `Charged`, `ListRp`, `OnSale`, `Price`, `Reason`, `Grant`
  (`Golfin.Economy.asmdef` references only `Golfin.Net`, so `InventoryGrant` is out of reach —
  declare a local `ShopGrantDto {Id, Kind, RefId, Amount}` with the same `JsonProperty` names
  `id, kind, ref_id, amount`), plus the `PointsSpendResult` fields. Helper
  `ToSpendResult()` builds a `PointsSpendResult` from the spend fields.
- `ShopPurchaseOutcome` with `Verdict ∈ Ok, Insufficient, PriceChanged, NotListed,
  AlreadyOwned, Unknown, Unavailable, Disabled` + `Server`, `Api`, `IsOffline` — same shape as
  `SpendOutcome`.
- Fresh `Guid.NewGuid()` per attempt (a retry after `Unavailable` is a new attempt, exactly as
  `SpendRoutine` does — the server's replay guard covers the case where the first one landed).
- **Balance fold, same ordering rule as `SpendRoutine`:** invoke `onDone` (which runs the local
  `rpm.SpendPoints(charged)`) and only then `PointsService.Instance.ApplySpendResult(result.ToSpendResult())`
  in a `finally`. Add that one public method to `PointsService` (wraps the private
  `ApplySpend`). Read the comment block above `SpendRoutine`'s try/finally before touching it.
- Own in-flight latch (`_inFlight`), same semantics as `PointsSpendGate`'s — a double-tapped
  BUY is a no-op, logged.

`Endpoints.cs`: `public static string ShopPurchase => BaseUrl + "/shop/purchase";` with the
auth-posture comment the inventory block uses.

### 3.2 `ShopTransaction.TryPurchaseCatalogEntry` — the rewire

Keep the pre-checks (they answer instantly and never reach the server). Then branch on the
flag, **not** on the gate:

- **Flag OFF** (Editor, harness, `GOLFIN_POINTS_BACKEND` undefined): the existing body,
  unchanged — `PointsSpendGate.Spend` + local grant. This is the offline/dev path and it stays
  byte-for-byte what it is.
- **Flag ON**: `ShopPurchaseService.Instance.PurchaseAsync(entry.EntryId, entry.EffectiveRpCost,
  ContentBuildNumber.Current, outcome => …)`. **No `PointsSpendGate.Spend`, no local price.**
  - `Ok` → `rpm.SpendPoints(outcome.Server.Charged)` (the SERVER's number, never `cost`) →
    `ApplyPurchaseGrant(outcome.Server.Grant)` → `onGranted` → `Success`.
  - `Insufficient` → toast `PointsSpendGate.InsufficientMessage` → `InsufficientRp`.
  - `PriceChanged` → new result `PriceChanged` (carry `outcome.Server.Price` on a new
    `ShopTransaction.LastServerPrice` or an out-param — implementer's call, keep it small).
  - `NotListed` → new result `NotListed`. `AlreadyOwned` → existing `AlreadyOwned`.
  - `Unavailable` / `Unknown` → toast `PointsSpendGate.OfflineMessage` → `SpendDenied`.
  - `Disabled` cannot happen on this branch (flag was checked) — log + `Invalid`.

  The two toast strings are `public const` on `PointsSpendGate`; reuse them so the copy stays
  identical everywhere.

`ApplyPurchaseGrant(grant)` — a **manager-level** apply, not `InventoryGrants.Apply`. That
static applies to raw `SaveData`, which is right at boot (before managers load) and wrong
mid-session (managers hold their own runtime copies and would not see it). Dispatch by
`grant.Kind`:

| kind | call |
|---|---|
| `club` | `ClubManager.Instance.GrantClub(refId)` (existing; unequipped, D5) |
| `character` | `CharacterManager.Instance.UnlockCharacter(refId)` — **new**, see §3.5 |
| `item` | `ItemManager.Instance.AddItems(refId, amount)` (existing, `Assets/Scripts/ItemManager.cs:127`) |
| `ball` | existing private `GrantBall(refId)` (respects -1 unlimited / cap 99) |

Then, **after** the manager call returned without throwing: add `grant.Id` to
`SaveDataHost.Instance.Data.appliedGrantIds` (create the list if null), `MarkDirty()`, and
`InventorySyncService.Instance.MarkDirty()` so the write-behind pushes the new blob. Then ack,
fire-and-forget: `new ApiInventoryTransport().AckGrants(new[]{grant.Id}, _ => {})` (or expose
the service's transport — implementer's call). Ordering is the grants-queue ordering, for the
same reason: apply → record id → ack. If the app dies before the record, the boot drain applies
it (the id is not in the save, so `InventoryGrants.Apply` treats it as new — and for a club or
character its own unique-check makes even that a no-op); if it dies after the record but before
the ack, the boot drain sees a duplicate and re-acks. Nothing is ever applied twice.

### 3.3 Categories

`ShopCategory` gains `Character`, `Item`. `GeneralShopCatalog.ParseCategory` becomes strict:
`club | ball | character | item` map; **anything else returns null and the row is dropped
with a `LogWarning` naming the entryId and the category** (add the count to the existing load
summary log). `bag` is deliberately in the dropped set — the client cannot grant a bag from a
purchase, so listing one would be a card that can only fail. Today's "everything unknown is a
Club" is the bug being fixed; it must not survive as a fallback.

`ShopTransaction` pre-checks for the new categories: `character` → `CharacterDatabaseCSV.Instance.GetCharacter(refId) != null`
and `!CharacterManager.Instance.IsOwned(refId)` (else `AlreadyOwned`); `item` →
`ItemDatabaseCSV.Instance.GetItem(refId) != null` (stackable, no owned check).

### 3.4 Cards — bind characters and items onto the club hierarchy

Both use `_clubTemplate` (`Resources/Prefabs/Shop/GeneralShopCard_Club`). No new prefab, no
Figma. `GeneralShopCard.Bind` dispatches on `entry.Category` → `BindCharacter` / `BindItem`;
the shared tail (`ConstrainName`, `BindPrice`, `WireBuy`) is unchanged.

**`BindCharacter`** (`CharacterDataRuntime` from `CharacterDatabaseCSV.Instance.GetCharacter`):

| Element | Value |
|---|---|
| rarity tile | `SetRarityTile(ch.rarity.ToString())` |
| `tournament_image/Portrait` | `ch.portraitSprite ?? ch.portraitFullSprite` |
| `NameLabel` | `ch.GetLocalizedDisplayName(singleLine: true).ToUpperInvariant()` |
| `DistRow` | **hidden** (`SetActive(false)`) |
| `StatRow_0..3` | Strength / Club Control / Recovery / Stamina — value text + bar; full-scale = the character's rarity cap for that stat from `RarityStatCaps` (existing utility — do not hard-code 60) |
| `StatRow_0..3` label | the row's label child (NOTE: read the child name off the prefab — it is not `Val`) → `STR / CTRL / REC / STA`; use the localisation keys the Roster stat lanes already use if they exist (grep `LocalizationText.csv` for the roster stat labels) rather than new literals |
| `StatRow_4` | **hidden** |
| `HMid` | `RarityLetter(ch.rarity)` |
| `HLevel` | `Lv {ch.startLevel}/{ch.maxLevel}` |
| owned state | `CharacterManager.Instance.IsOwned(refId)` → same disabled "OWNED" treatment `WireBuy` gives an owned club (extend the `owned` expression at `GeneralShopCard.cs:218`) |

**`BindItem`** (`ItemDataRuntime` from `ItemDatabaseCSV.Instance.GetItem`):

| Element | Value |
|---|---|
| rarity tile / `HMid` | item rarity |
| Portrait | the item's thumbnail sprite — `ItemDatabaseCSV` resolves `thumbnailSpriteName` via its `SpriteRef`/`SpritePath` (lines ~87/113); expose the resolved `Sprite` on `ItemDataRuntime` if it is not already there |
| `NameLabel` | localised item name, upper |
| `DistRow/Txt` | `Restores {restorePercent}%` — localised key, EN + JA added to `LocalizationText.csv` |
| `StatRow_0..4`, `HLevel` | **hidden** |
| owned state | never — items stack; BUY always enabled when affordable |

The `rarity` column on the shop row stays the **ball** display override only; characters and
items read rarity from their own catalog rows, like clubs.

### 3.5 `CharacterManager.UnlockCharacter(string characterId)` — new

`Assets/Scripts/CharacterManager.cs`, beside `GrantStarter` (line ~398). Same body **minus**
`starterCharacterId`, minus `SelectCharacter`: lookup in `ownedCharacters` (every catalog
character is already a row there with `isOwned=false` — that is what `GrantStarter` relies on),
set `isOwned = true`, `SyncCharacterToSaveData(characterId)`, `OnRosterChanged?.Invoke()`.
Returns `false` if unknown or already owned. The Roster screen already re-renders on
`OnRosterChanged`, so a bought character appears unlocked with no Roster change.

### 3.6 Screen — chips + results

- `GeneralShopScreen.prefab`: duplicate `BALLSChip` twice under
  `ContentArea/BarsArea/FilterGroup/CategoryRow` → `CHARACTERSChip`, `ITEMSChip`, labels via
  whatever mechanism the existing chip labels use (they are literals in the prefab today — check
  for a localisation binder first; if none, add keys for all five chips EN + JA rather than
  adding two more literals). NOTE: five chips in the row — check the row's layout component;
  if they overflow 1178 px, reduce the label font size on all five, do not restyle the row.
- `WireChip("CHARACTERSChip", ShopCategory.Character)`, `WireChip("ITEMSChip", ShopCategory.Item)`;
  `RestyleChips` gains the two lines.
- `Rebuild`: template = `Ball ? _ballTemplate : _clubTemplate` still holds (characters and
  items use the club template).
- `HandleBuy`: `PriceChanged` → rebind the card with the server price (set
  `entry.RpCost/SaleRpCost` from the response, or simplest: `GeneralShopCatalog.Reload()` +
  `Rebuild()`) and toast `"Price updated"`; `NotListed` → `GeneralShopCatalog.Reload()` +
  `Rebuild()` and toast `"No longer available"`. Both toasts through the existing
  `ToastController`; add EN/JA keys if the surrounding toasts are localised, else match them.
- `GeneralShopDemoRecorder.cs` (Editor) references the chips by name — add the two new ones
  if it enumerates them.

### 3.7 Tests (EditMode)

- `ShopPurchaseServiceTests` beside `PointsSpendTests.cs`: wire shape of
  `BuildPurchaseJson`; each server status → verdict; flag OFF → `Disabled` with no transport
  call; latch rejects an overlapping call; `Ok` folds `total_points` into `PointsService` after
  `onDone`.
- `GeneralShopCatalog`: `character`/`item` parse; `bag` and a typo are dropped, not clubbed.
- `InventoryGrants.Apply` given a grant whose id is already in `appliedGrantIds` → duplicate,
  save untouched (pins the §3.2 guarantee).
- `ContentShopWindowTests` untouched — the server mirrors those rules; list the matrix in the
  migration header so the two stay comparable by eye.

---

## 4. Admin dashboard

- `lib/i18n.ts` `sh.notice.headline/body` (EN + JA): the banner stops saying prices are not
  enforced. New copy: *"Prices are enforced by the server for builds ≥ N."* / *"Purchases on
  build N and later go through `/shop/purchase`, which charges the PUBLISHED price at purchase
  time (server clock, listing + sale windows). Older builds still debit locally until the legacy
  spend path is closed."* Style: amber (`border-amber-500/50 bg-amber-500/10`), not red — it is
  information now, not a warning. N = the build number of the first TestFlight build that
  carries §3; leave it as a constant at the top of `shop-panel.tsx` with a comment.
- `uinv.notice.*` (inventory tab) stays as is — inventory outside purchases is still
  client-asserted.
- No new panel. Purchases are visible today in the Points panel as ledger rows with
  description `shop:<entryId>`; a Purchases tab in the Users drawer is a follow-up if wanted.

---

## 5. Sequencing (Code runs it in this order)

1. playlife: migration file → **paste the SQL in chat** → Cesar applies + pastes verification
   output → router + tests → deploy → §2.5 smoke.
2. Unity: §3.1–3.7 → full unfiltered EditMode sweep (baseline 1761 / 1758 / 0 / 3 after
   `content_player_inventory`) → TestFlight via `fastlane_testflight_pipeline`.
3. Device pass (Cesar): §6 items marked *device*.
4. §2.6 legacy-path closure — separate commit, on Cesar's word only.

## 6. Acceptance

- [ ] **Price is the server's.** Publish a price change in the admin; a client whose bundled
      CSV still shows the old price gets `price_changed`, the card updates, the second tap
      charges the published price. *(device)*
- [ ] **Sale window, server clock.** A row whose sale window closed after the client loaded
      the catalog is charged the list price and the card shows `price_changed` once. *(device)*
- [ ] **Delivery survives death.** Kill the app between the debit landing and the grant
      applying (breakpoint in Editor with flag ON, or airplane mode flipped mid-call on device):
      next boot drains the grant, item present exactly once, RP debited exactly once. *(device)*
- [ ] **Idempotent.** Replaying the same `idempotency_key` returns `replayed: true`, no
      second ledger row, no second grant (SQL verification query in the migration footer).
- [ ] **Insufficient** writes nothing (ledger row count unchanged) and toasts the existing copy.
- [ ] **Unique things stay unique.** Buying an owned club/character (owned via starter, gacha or
      admin grant, not via shop) → `already_owned`; the card shows OWNED before the tap.
- [ ] **Kill switch.** `content_settings.content_enabled = false` → `not_listed / disabled`;
      flip back → sells again. No deploy either way.
- [ ] **Characters and items sell.** Publish one `character` and one `item` row from the admin;
      both render on the club-card hierarchy per §3.4, buy, appear in Roster (unlocked) and Items
      respectively; `bag` and a typo category are dropped with a warning, never rendered as a club.
- [ ] **Chips.** CHARACTERS and ITEMS filter; ALL shows everything; nothing overflows the row.
- [ ] **Flag OFF unchanged.** Editor with `GOLFIN_POINTS_BACKEND` undefined: purchase path
      byte-identical to HEAD (existing `PointsSpendTests` still green, harness sequence unchanged).
- [ ] **Blob + ack.** After a purchase the write-behind PUT carries the item and
      `appliedGrantIds` carries the grant id; `golfin_pending_grants.applied_at` is stamped.
- [ ] `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200 after deploy;
      `/shop/purchase` 403 unauth / 401 bad token.
- [ ] Admin Shop banner reads the §4 copy in EN and JA.
- [ ] Full unfiltered EditMode sweep green; backend suite green.

## Out of scope (deliberate)

- **Stamina Boost Shop** (`ShopTransaction.TryPurchase`, `stamina_shop_items.csv`) — still
  client-priced; not a content catalog. Next.
- **Level-ups, hole unlocks** — same pattern, own spec (`progress_server_side`).
- Locked Roster card BUY; `stockLimit` / `minPlayerLevel` columns; bags; IAP; art URLs.
- Server-side merge or validation of the inventory blob beyond the §2.1 step 8 ownership read.
