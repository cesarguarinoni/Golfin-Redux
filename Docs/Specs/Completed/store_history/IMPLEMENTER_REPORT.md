# IMPLEMENTER_REPORT — `store_history`

Baseline: HEAD `1de510966`. Implemented directly by Claude Code at Cesar's instruction
("Read Docs/Specs/Active/store_history/SPEC.md and implement it"), not via the subagent chain.

Canonical screenshot: `screenshots/02_store_history_all.png` (1170×2532)
Canonical video: `videos/store_history.mp4` (56.3 s, 1170×2532, 8.2 MB) — copied to
`Docs/Reports/Media/store_history/store_history.mp4` for the daily report.

---

## What shipped

### Backend — `GET /api/v1/shop/history` (playlife)

`backend/routers/shop.py` gains `history()` + a module-local `_missing_relation`, a structural copy
of `gacha.py::history()` against `golfin_shop_purchases`. One query (a purchase IS one row, so
nothing nests), keyset `before` validated to a 400 naming the parameter, `next_before` only on a
full page, `_missing_relation` → empty. The module docstring now says why this READ degrades where
the POST refuses to.

Six tests in `backend/tests/test_shop_purchase.py` (plus a seventh, `a_real_history_fault_is_still_a_500`,
which is the mirror the gacha suite carries). The module's fake Supabase had `table()` raise on
purpose ("routers.shop must not read table X directly") — it now returns the `_Table` stub copied
from `test_gacha.py`, so the query SHAPE is assertable, not just the JSON.

```
$ python3 -m pytest tests/test_shop_purchase.py -q
39 passed, 1 warning in 0.42s          # 32 before + 7 new
```

**Deployed and verified live** — verified by probe, not by exit code
(`reference_flyctl_401_false_deploy_failure`):

```
$ flyctl status
 Image    │ playlife-api:deployment-01M220GD2ZW514QM9M33FY8V1Y
 app      │ 148e03defe4d38 │ 70 │ nrt │ started
```

`GET /openapi.json` now lists `/api/v1/shop/history` with `limit` default 50 / max 200.

```
$ curl -H "Authorization: Bearer …" "$API/api/v1/shop/history?limit=5"
{"data":{"purchases":[
 {"id":"130d8cee…","entry_id":"shop_item_repairkit_common","category":"item",
  "ref_id":"repairkit_common","amount":1,"charged_rp":75,"list_rp":75,"on_sale":false,
  "build":2790,"created_at":"2026-09-08T06:53:18.590204+00:00"},
 {"id":"57a705dd…","entry_id":"shop_ticket_standard_50","category":"ticket","ref_id":"0",
  "amount":50,"charged_rp":100,"list_rp":100,"on_sale":false,"build":2538,
  "created_at":"2026-08-31T13:04:10.480789+00:00"},
 {"id":"34f97eeb…","entry_id":"shop_ball_putt_ace","category":"ball","ref_id":"ball_putt_ace",
  "amount":1,"charged_rp":35,"list_rp":50,"on_sale":true,"build":2428,
  "created_at":"2026-08-28T14:55:14.758942+00:00"}, …]}}
```

Newest first; `charged_rp` 35 against `list_rp` 50 on a real sale row — which is exactly why the
PRICE line renders `charged_rp`.

| probe | result |
|---|---|
| unauthenticated | `403` |
| `?before=next-tuesday-ish` | `400 {"detail":"before must be an ISO-8601 timestamp"}` |
| `?limit=5` (6 rows exist ⇒ full page) | 5 rows, `next_before=2026-08-28T13:49:36.678494+00:00` |
| `?limit=200` (short page) | 6 rows, `next_before=null` |

⚠️ **The deploy carried one file that is not mine.** `backend/routers/user.py`'s
`golf_profile_prompted` change was uncommitted in the playlife tree at session start. I checked
production BEFORE deploying: `UpdateProfileRequest` already exposed `golf_profile_prompted`, so it
was already live from an earlier dirty-tree deploy and shipping it again is a no-op. It is still
uncommitted in playlife and belongs to whoever wrote it.

### Client

Per SPEC §2–§4, and **copied, not generalised** — `GachaHistoryStore` and
`GachaHistoryScreenController` are byte-identical to HEAD, and `GachaHistoryPagingTests` is untouched.

- `Endpoints.ShopHistory`; `ShopHistoryPage` / `ShopPurchaseDto` (snake_case, `created_at` a
  **string** so the disk mirror round-trips the UTC verbatim).
- `ShopPurchaseService.FetchHistoryAsync/Routine` — the flag gate inside the routine, clamp 1..200,
  **null on failure** so an offline open keeps the mirror rather than blanking a real log.
- `StoreHistoryRecord` / `StoreHistoryStore` — the `GachaHistoryStore` shape, `store_history.json`,
  atomic `.tmp`+replace, `Prepend` at the head without touching the mirror. `Map` parses `category`
  through `GeneralShopCatalog.ParseCategory` (widened `private` → `internal`; **no second parser**)
  and SKIPS an unknown category with a warning rather than defaulting it to Club.
- `ShopTransaction` — one `StoreHistoryStore.Prepend(...)` in the `Ok` arm, right after
  `rpm.SpendPoints(outcome.Charged)`. Nothing else in that file changed.
- `StoreHistoryRow` / `StoreHistoryScreenController` — Col1 through `GachaPrizeCardBinder`, five
  meta lines, line 6 and `Col3_Currency` hidden; PageSize 12 / RowsPerFrame 3 / reference-identity
  `PrependCount` / `PaintGate` / `FadeSwap`, chips wired the `GeneralShopScreenController` way with
  `_activeCategory` remembered statically.

### §8 — the "+"-entry grid gap

Fixed ONCE, in `PaintMotion.StaggerRise`: `LayoutRebuilder.ForceRebuildLayoutImmediate(rows[0].parent)`
before the rects/groups are built. No per-screen workaround, no delay frame.

Measured on all three entry paths (the y of `GridContent`'s first children after the stagger ends):

| path | banner | card 0 | card 1 | card 2 |
|---|---|---|---|---|
| A — top-bar "+" from **Home** | y=0.00 h=252 | **−137.00** | −435.00 | −733.00 |
| B — top-bar "+" from **GachaHistory** | y=0.00 h=252 | **−137.00** | −435.00 | −733.00 |
| C — bottom-nav Gacha → **STORE** | y=0.00 h=252 | **−137.00** | −435.00 | −733.00 |

Identical on all three, spacing a uniform 298 between cards, card 0 flush under the banner. No
card-sized hole. Screenshots `01_`, `05_`, `06_`.

---

## Cesar's two in-flight catches (2026-09-09, mid-implementation)

> "Gacha history repair kit is missing an image. Gacha ticket needs a description under its portrait"

Both were real, both pre-date this task, and both are fixed at the **shared** seam so Store History,
Gacha History, Gacha Prizes and the reveal modal all inherit the fix.

1. **`GachaHistoryRow.BindGeneric` hid Col1.** It called `_clubCard.gameObject.SetActive(false)` for
   every non-club, non-ball kind — Stage-1 behaviour from before `GachaPrizeCardBinder` existed, so
   an item / character / ticket in the pull log rendered with an EMPTY first column and the metadata
   sliding left into it. It now binds Col1 through `GachaPrizeCardBinder`, the same binder the
   reveal modal, the Prizes grid and Store History use. Before/after: the crop in
   `07_gachahistory_repairkit_card_fixed.png` vs the same row in the pre-fix run.

2. **A ticket card had nothing under its portrait.** `BindOtherKind`'s `KindTicket` branch passed no
   `description:` (items do), and a ticket has no stat lanes and no detail line — so the bottom half
   of the card was blank. It now reads `TICKET_INFO_<KEY>` through the same
   key-with-fallback ladder items use. `ticket_types.csv` has no prose column, so a type published
   after this build simply shows nothing rather than a raw key.
   Proof: `08_ticket_description_fixed.png`.

3. **The description was too small when there was little of it** ("text is too small; need autosize
   to be bigger when not as much text as other descriptions"). `BagClubCard.BindDescription` built
   the label with `fontSizeMax = 11`, and TMP's auto-size only ever SHRINKS toward the floor — it
   can never exceed the ceiling. So a two-line ticket description was pinned at the size a nine-line
   repair-kit description needs, leaving most of the card empty. The band is now `[6..22]` and — the
   part that actually matters — it is **re-asserted on every bind** rather than written once in the
   creation branch: this card is re-bound in place, so a slot that first showed an item kept the
   item's band when it later showed a ticket.

   Measured off the live labels after the change:

   | prize | chars | fontSize before | fontSize after |
   |---|---|---|---|
   | Repair Kit (long) | 214 | 11 (at the ceiling) | **10.4** — unchanged, it never needed more |
   | Ticket (short) | 71 | 11 (at the ceiling) | **18.3** |

   Raising the ceiling costs the long copy nothing: auto-size still picks the largest size that
   FITS. Proof: `09_description_autosize.png`.

---

## Acceptance

| # | item | verdict | evidence |
|---|---|---|---|
| 1 | §8 gap, all three entry paths identical | **PASS** | the table above; `01_`/`05_`/`06_` |
| 2 | backend: tests, deploy, live curl, 400, 403 | **PASS** | § Backend above |
| 3 | STORE History chip → Store History, no toast; GACHA chip unchanged | **PASS** | `ACCEPT — STORE tab History chip -> ScreenId.StoreHistory (no toast)` and `ACCEPT — GACHA tab History chip -> ScreenId.GachaHistory (unchanged)`, both driven by the REAL `HistoryChip.onClick.Invoke()` |
| 4 | shell A/B: STORE gold / GACHA white / GIFTS dim, title, chips, CLOSE | **PASS** | `02_` vs `04_`; `Title = "STORE HISTORY"` read off the live TMP |
| 5 | **scrollbar inside MainPanel** | **PASS** | MainPanel world `[(48,297) .. (1122,2049)]`, Scrollbar `[(1106,469) .. (1120,1924)]`, `inside=True`. Not the Figma's x=1138. |
| 6 | rows: newest first, Col1 bound, Col2 lines, line 6 + Col3 hidden | **PARTIAL** | 6 rows, item / ticket / ball all bound and named. **No club or character purchase exists on the dev account**, so those two kinds are covered by code path (one `KindOf` switch, one binder) and by the Gacha History fix above, not by a screenshot. Verdict recorded as PARTIAL rather than PASS. |
| 6b | `PRICE: n RP` = the row's `charged_rp` | **PASS** | row 1 shows `PRICE: 75 RP`; the live API row for `shop_item_repairkit_common` has `"charged_rp":75`. Row 3 shows `35`, `list_rp` 50 — the sale price survives. |
| 7 | prepend after a purchase | **NOT VERIFIED** | needs a real RP purchase on the dev account. Code path is `ShopTransaction` Ok arm → `Prepend` → `OnChanged` → `PrependCount`; the discriminator is covered by `StoreHistoryPagingTests.APurchaseLanding_IsAPrependOfExactlyTheNewRows`. **Manual verification listed below.** |
| 8 | airplane-mode cache paint | **NOT VERIFIED** | manual, listed below |
| 9 | chips filter / gold / fade / remembered | **PASS (partly)** | TICKETS → 1 row (the ticket), CLUBS → 0 rows (this account has bought no clubs — correct, not a defect), ALL → 6. `03_store_history_tickets.png`. The "remembered across a leave-and-return" leg is `static _activeCategory`; not separately screenshotted. |
| 10 | paging > 12 purchases + A13 budget | **NOT VERIFIED** | the dev account has 6 purchases, so the first page never fills. `NextPageEnd` is covered by tests. |
| 11 | `StoreHistoryPagingTests` + `ShopPurchaseServiceTests` green, EditMode sweep | **PASS** | below |
| 12 | `ScreenId.StoreHistory` is LAST; scene diff | **PASS** | below |
| 13 | strings published, export clean, no hardcoded literals, `SHOP_HISTORY_COMING_SOON` unreferenced | **PASS** | below |
| 14 | playbook §7 self-diff | **PASS** | below |
| 15 | no white boxes, all refs wired, no new Console errors | **PASS** | every `[SerializeField]` read back off the saved prefab (below); no `error CS` in the console after any refresh |

### Tests

```
StoreHistoryPagingTests            12/12 PASS
ShopPurchaseServiceTests.History_*  4/4  PASS
ShimmerHostTests                    3/3  PASS   (incl. EveryDeclaredSite_HasAHostInTheScene)
GachaHistoryPagingTests            all   PASS   (untouched suite, still green)

Full EditMode sweep, after every change including Cesar's three:
DONE passed=2923  failed=0  skipped=4
```

⚠️ An earlier sweep reported 143 failures. Every one was cross-test log-bleed, and **132 of them
were caused by my own MCP `ping` polling during the run** — the plugin logs `Tool with Name 'ping'
not found` as an Error, and NUnit's unhandled-log assertion attributes it to whichever test is
running. The run above touched no MCP tool while it ran. Recorded rather than quietly re-run.

### `ScreenId` tail (appended, never inserted — it is serialized)

```
ScreenId last 3: ResetPassword, StartingCharacterSelection, StoreHistory
StoreHistory value = 34
```

### Scene diff

```
$ git diff --stat Assets/Scenes/ShellScene.unity
 Assets/Scenes/ShellScene.unity | 511 +++++++++++++++++++++++++++++++++++++++++
 1 file changed, 511 insertions(+), 0 deletions(-)
```

**511 insertions, ZERO deletions.** Three things and nothing else: the `StoreHistoryScreen` prefab
instance (+ its stripped GameObject/RectTransform), the one `ScreenManager._storeHistoryScreen`
reference, the `ScreensRoot` child-list entry, and the `Shimmer_store_history` host with its three
ShimmerBlock instances.

⚠️ **How that number was reached, because it matters.** Saving ShellScene from the Editor produced
**1417 insertions / 1296 deletions** — 154 RectTransforms whose `m_AnchorMin/Max` reset to (0,0) and
whose `m_AnchoredPosition` zeroed, i.e. the layout churn `project_scene_save_bakes_layout_churn`
warns about. I reverted the file and re-applied only the three (then seven) wanted hunks with
`git apply`, then reloaded the scene in Unity to confirm it comes back **clean** (`dirty=False`) and
that everything resolves:

```
instance = ok active=False sib=18      (GachaHistoryScreen is sib=17)
rect  new: anchMin=(0,0) anchMax=(1,1) pos=(0,0) size=(0,0)   ← identical to GachaHistoryScreen
world rect new  = (0,0) (0,2532) (1170,2532) (1170,0)         ← identical to GachaHistoryScreen
_storeHistoryScreen resolves = True
missing scripts in subtree = 0
cc[2] Shimmer_store_history active=False, host component ok, blocks=3
```

The `Shimmer_store_history` host is in the scene because `GameShimmerSites.All` gained
`store.history` and `ShimmerHostTests.EveryDeclaredSite_HasAHostInTheScene` reads the scene YAML —
declaring the site without placing the host would have failed that gate. It was placed by the
project's own `GOLFIN ▸ Game Polish ▸ Apply — shimmer hosts`, not by hand.

The stale `MatchMakingModal` overrides the spec warned about did NOT appear in the wanted hunks, so
there was nothing to revert out.

### Clone provenance

| element | source | verified |
|---|---|---|
| `Assets/Prefabs/UI/Shop/StoreHistoryScreen.prefab` | `AssetDatabase.CopyAsset` of `Assets/Prefabs/UI/Gacha/GachaHistoryScreen.prefab` | hierarchy dumped and compared; only the root name, the controller component and the Title key differ |
| `Assets/Prefabs/UI/Shop/StoreHistoryRow.prefab` | `AssetDatabase.CopyAsset` of `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab` | `Col1_ClubCard` is still the nested `BagClubCard` prefab instance, guid `5e39901a81c074c4aacbe5d27d1309fd` |
| divider between rows | `Assets/Prefabs/UI/Divider.prefab` | the same asset `GachaHistoryScreenController._dividerPrefab` points at |
| shimmer host | `Assets/Prefabs/UI/Common/ShimmerBlock.prefab` via `GamePolishBuilder.ApplyShimmer` | 3 blocks, 978×100 |

Serialized fields read back off the SAVED assets (not asserted from the writing code):

```
StoreHistoryRow.prefab
  _clubCard       = Col1_ClubCard (BagClubCard)
  _metaLines[0..5]= Line_DRIVER_G_F, Line_RARE___Lv_999, Line_PULLED_2025_12_28,
                    Line_04_12_49_AM, Line_STANDARD_CLUBS_1, Line_PULLS__1   (6/6, none null)
  _currencyColumn = Col3_Currency (GameObject)
StoreHistoryScreen.prefab
  _rowPrefab      = StoreHistoryRow  (Assets/Prefabs/UI/Shop/StoreHistoryRow.prefab)
  _dividerPrefab  = Divider          (Assets/Prefabs/UI/Divider.prefab)
  _scrollContent  = Content (RectTransform)
  _closeButton    = CloseButton (Button)
  GachaHistoryTabStrip._storeIsActive = True
  Title LocalizedText.key = SHOP_HISTORY
```

The row's MetaLines keep their gacha mock object names (`Line_PULLED_2025_12_28` etc.). Renaming
them is cosmetic and the SPEC's "do NOT rebuild, only bind" plus the minimal-diff constraint say
leave them; the `_metaLines` tooltip documents the new mapping. Flagged, not hidden.

### Strings

Six keys for this task, two more for the ticket description. Both batches went through the importer
and were **published**, not left as drafts:

```
$ import_content.py --catalogs texts            # PLAN
  texts   6 add   0 change   1101 same   0 conflict
$ import_content.py --catalogs texts --apply
  Wrote 6 draft(s)
  content_publish(texts) -> version 47
$ … then the two TICKET_INFO_* keys
  texts   2 add   0 change   1107 same   0 conflict
  content_publish(texts) -> version 48
$ export_content.py --check
  texts  v48  1109 rows  unchanged                 ← CLEAN
  version file  20 lines CHANGED                   ← texts=46 -> 48, committed
```

Bundled table rebuilt too — a published row alone still renders as the raw key until
`LocalizationTextTable.asset` carries it (`feedback_always_publish_new_text`). It needed a forced
reimport first, because Unity serves the IMPORTED CSV, not the bytes on disk:

```
rows = 1109
  SHOP_HISTORY              => EN='STORE HISTORY'  JA='ストア履歴'
  SHOP_HISTORY_AMOUNT       => EN='AMOUNT: {0}'    JA='数量: {0}'
  SHOP_HISTORY_ACQUIRED     => EN='ACQUIRED {0}'   JA='入手日 {0}'
  SHOP_HISTORY_SOURCE       => EN='SOURCE: {0}'    JA='入手元: {0}'
  SHOP_HISTORY_SOURCE_STORE => EN='STORE'          JA='ストア'
  SHOP_HISTORY_PRICE        => EN='PRICE: {0} RP'  JA='価格: {0} RP'
  TICKET_INFO_STANDARD      => EN='Spend one for a single pull on any banner that takes a standard ticket.'
  TICKET_INFO_GOLD          => EN='A premium pull ticket — for the banners that call for one.'
```

Zero new hardcoded `.text` literals — every string on the new screen goes through
`LocalizationManager.Get` or the Title's `LocalizedText`:

```
$ grep -n '\.text\s*=' Assets/Scripts/UI/Shop/StoreHistoryRow.cs
   (only) _metaLines[index].text = text;   and   _metaLines[index].text = string.Empty;
   — both fed by SetLine/HideLine, whose callers are all LocalizationManager.Get + string.Format
```

`SHOP_HISTORY_COMING_SOON` is no longer referenced in C#:

```
$ grep -rn "SHOP_HISTORY_COMING_SOON" Assets/Scripts/
(no matches)
```

**Cesar's step, still pending:** deactivate the `SHOP_HISTORY_COMING_SOON` row in the admin. It is
deliberately left in the CSV (invariant I6 — deleting it would make the importer re-append it).

### Playbook §7 self-diff vs `reference/StoreHistory_13509-2978.png`

| element | node | built | verdict |
|---|---|---|---|
| Tab strip GACHA / STORE / GIFTS | STORE gold `#FFD023`, GACHA white, GIFTS 35% | same strip, `_storeIsActive=true` flips the lit segment | match |
| Chip row ALL…ITEMS, ALL gold | `#EBD170` | `ChipGold` `#EBD170`, and now **wired** | match + deviation of record (chips wired) |
| Title | "STORE HISTORY" + history icon | `SHOP_HISTORY` through the cloned `LocalizedText` | match |
| Separator under title | present | unchanged from the clone | match |
| Row Col 1 | 180×274 gold "Rarity Background" art tile | **BagClubCard tile** via `GachaPrizeCardBinder` | **DEVIATION OF RECORD** (Cesar, spec header) |
| Row Col 2 | NAME / AMOUNT / ACQUIRED / SOURCE / PRICE | same five lines, line 6 hidden | match |
| PRICE value | `$3.99` | `PRICE: 75 RP` | **DEVIATION OF RECORD** — Redux is RP-only (`ECONOMY_MASTER` §2); the `$3.99` in the node is mock |
| SOURCE value | `MARKETPLACE` on row 2 | `SOURCE: STORE` always | **DEVIATION OF RECORD** — there is no marketplace |
| Col 3 ticket chip | absent in node | hidden | match |
| Scrollbar | x=1138, outside the panel | inside `MainPanel`'s right edge | **DEVIATION OF RECORD** — measured `inside=True` |
| Empty list | — | panel stays empty, no invented copy | match |
| CLOSE | present | same `CloseButtonArea`, `GoBack(GeneralShop)` | match |
| Side arrows | present in node | not built, as on Gacha History | match (spec) |
| Top bar / nav bar | present | shared bars via `ScreenManager` | match |

Four deviations, all of record, none a fidelity failure.

---

## The clip

`Assets/Scripts/UI/Editor/StoreHistoryDemoRecorder.cs` — the `GeneralShopDemoRecorder` pattern
(Unity Recorder, **GameView** source so the Overlay HUD is not dropped under URP, 1170×2532 @ 30fps)
with `GpsFlowDemoRecorder`'s caption sidecar, so `build_bot_video.py --mode captionsjson` burns the
captions off times the recorder stamped rather than a hand-timed list that drifts when a hold
changes. Nothing calls `CaptureCore` while the Recorder runs — a backbuffer read mid-recording flips
Recorder frames on Metal — so the stills below come out of the finished mp4.

Every tap is a real widget's `onClick`, same as the acceptance walk.

**Captions verified against decoded frames, one per window** — not assumed from the timeline:

| window | caption | the frame shows |
|---|---|---|
| 6.6–9.1 | Rewards Center from the top-bar + | the STORE grid |
| 9.1–14.0 | Cards now rise into their real slots / no gap under the first one | grid, cards flush and evenly spaced |
| 14.0–17.2 | The STORE History chip opens a screen / instead of a coming-soon toast | STORE HISTORY |
| 17.2–20.4 | Your own purchases, from the server / newest first | the six-row log |
| 20.4–29.4 | PRICE is what was charged / so a sale price stays a sale price | `PRICE: 35 RP` rows on screen |
| 29.4–42.0 | The category chips filter the log | TICKETS → one row (30.5, 31.5), BALLS → PUTT ACE only (33.5, 34.5) |
| 42.0–52.6 | In the pull log, every prize kind / now draws its own card | REPAIR KIT with a card at 44.7 |
| 52.6–56.9 | A short description sizes up / to fill the card it sits on | the repair-kit card **with its description block** at 53.2 / 55.0 |

The last one was **retimed**: as first stamped it opened at 47.44, over five seconds of frames
showing club cards with stat bars and no description at all. Caught by sampling the window rather
than trusting the stamp, and fixed by moving the start to where the claim becomes true (the
prize-card caption holds over the scroll instead). Re-burned from the kept raw — no re-record.

Also checked on **consecutive** decoded frames (n=600..603), because keyframe sampling misses a
y-flip: all upright, no flip.

## Capture instrument

`Assets/Scripts/UI/Shop/Editor/StoreHistoryAcceptanceRun.cs` — Editor-only, drives the walk through
**real widget `onClick`** only (PIPELINE_HARDENING §2): the Splash `StartButton`, the real top-bar
`ShopPlusButton`, the real `NavGachaButton`, the Rewards Center's own `HistoryChip`, the chip row,
the tab strip. No `ShowScreen(target)` shortcut anywhere.

Two instrument problems found and fixed rather than worked around:

1. The first run booted into an **empty scene** — an EditMode sweep leaves whatever scene it opened
   last, and play mode there has no ScreenManager and no Splash. The driver now opens ShellScene
   explicitly before entering play mode.
2. `CaptureCore.SnapPlayModeSafe` returned a path for a file it **never wrote** — the known
   `reference_snapplaymodesafe_phantom_path` failure; `ScreenCapture`'s backbuffer read yields
   nothing when the Editor is not frontmost, which is always true when driven over MCP. `Save()` now
   VERIFIES the returned path on disk and falls back to the Game View's own render texture (the same
   surface the sanctioned `screenshot-game-view` tool reads). All six frames are real 1170×2532.

---

## Not verified — needs a manual pass

These need account state or a device condition I cannot create from here. Listed rather than
asserted:

1. **Prepend after a purchase.** Buy something in the STORE, open Store History: the row should be
   at the top before the server answers and stay after (no duplicate), with
   `[StoreHistoryScreenController] prepend 1` in the console.
2. **Airplane mode → the disk mirror draws, no shimmer; back online → the server repaint fades in.**
3. **Paging past 12.** The dev account has 6 purchases, so the first page never fills and the
   scroll-append never fires. `NextPageEnd` is unit-covered.
4. **A club and a character purchase.** The dev account has bought item / ticket / ball only, so
   Col1 is screenshotted for three of the five kinds. The other two go through the same one-line
   `KindOf` switch into the same binder.
5. **The A13 push budget** (< 20 MB / < 50 ms) for the push into StoreHistory.

No device pass is listed: everything above is verifiable in the Editor.

## Deviations and findings

- The `## UI fidelity lint` gate (hook Rule 21) is not applicable here — this screen is a duplicate
  of a shipped prefab, not a node built from atoms, and the linter's node-spec layer needs a
  `spec.json` generated from a Figma node whose layout this task deliberately does NOT follow
  (four deviations of record, including the scrollbar). The objective gate used instead is the
  geometry read-back above (scrollbar-inside-MainPanel, the three §8 y-tables) plus the per-element
  self-diff.
- `GachaHistoryRow.cs`, `GachaPrizeCardBinder.cs` and `BagClubCard.cs` are not in the SPEC's file
  list. They are Cesar's three in-flight catches, above.
- Line 2 of a non-club Gacha History row (the rarity line) is blank — `ResolveRarityLine` returns
  empty for anything but a character. Pre-existing, in the controller the spec says not to touch,
  and not what Cesar flagged. Filed here rather than fixed.
- `Docs/Reports/content_art.txt`, `Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt` and
  `Docs/Specs/Active/notice_panel_slide/` were already dirty at session start and are untouched.
  `Docs/Specs/Active/loading_tips/` appeared during the session from another workstream.
