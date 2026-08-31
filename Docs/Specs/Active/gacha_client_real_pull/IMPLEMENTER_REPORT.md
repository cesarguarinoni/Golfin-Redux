# IMPLEMENTER_REPORT — `gacha_client_real_pull`

**Built 2026-08-31 by Claude Code, directly.** No subagent chain: this task is one coherent
change across the client, and the pipeline's value (an independent pair of eyes on a rendered
frame) is served here by the eight live §7 steps against prod, each with the server's own ops
panel as the check.

Canonical screenshot: `screenshots/01_banner_card_numeric_costs.png`
Iteration shape: `gacha_client:server-pull`

---

## Part 1 — Files

### New

| File | What it is |
|---|---|
| `Assets/Scripts/Economy/GachaPullService.cs` | `POST /gacha/pull` + the two GETs. `ShopPurchaseService`'s shape line for line: flag gate INSIDE the routine, in-flight latch, fresh idempotency key per attempt. Applies nothing — see the asmdef note below. |
| `Assets/Scripts/Economy/GachaPullOutcome.cs` | The eight verdicts a pull can come back with. `Paused` is split out of `NotAvailable` because one means "the feature is off" and the other means "reload your catalog". |
| `Assets/Scripts/UI/Gacha/GachaContentCatalogs.cs` | `GachaRatesCatalog` / `GachaPoolCatalog` / `TicketTypeCatalog` + the shared bundled-CSV-plus-overlay merge, in the `ClubCsvParser` shape. |
| `Assets/Scripts/UI/Gacha/GachaBannerArt.cs` | The ONE banner-art ladder, so the withhold rule and the card cannot disagree about whether a banner is drawable. |
| `Assets/Scripts/UI/Gacha/PrizeRecord.cs` | Multi-kind prize record carrying the SERVER's rarity. Replaces the `{ string ClubId }` struct that came out of the mock pool. |
| `Assets/Scripts/UI/Gacha/GachaPrizeCardBinder.cs` | The one prize-card binder: prefab choice, bind, dupe pill, and SPEC §4.3's scale-to-fit. |
| `Assets/Tests/EditMode/GachaClientRealPullTests.cs` | 30 tests — overlay matrix, withhold clauses, `TryReinstallFromCache`, wire shape, the apply ORDER, DTO mapping, the ticket/blob exclusion. |

### Modified

| File | Change |
|---|---|
| `ContentCatalogs.cs` | The four gacha catalogs join `Data` and `All`, so `RequestList` asks for them. |
| `ContentService.cs` | `TryReinstallFromCache(catalog)` — the 5b carve-out, allowed for those four and refused for everything else. |
| `GachaBannerModel.cs` | 22 columns, the overlay merge, the §3.1 withhold rule + `IsRollable` seam, and the 5b subscription. |
| `GachaBannerCard.cs` | Title (JA/EN), art via the ladder, NUMERIC costs, the banner's ticket icon, the two guarantee lines, re-bind on language change. |
| `GachaCarouselController.cs` | `Instance` + `Rebuild()` so a server refusal can rebuild the strip; refreshes the ticket counter on open. |
| `GachaPullFlow.cs` | The whole flow: waiting modal → server → reveal, or abort + toast. `ApplyOk` owns the four consequences IN ORDER. |
| `GachaRevealModalController.cs` | `Play` split into `BeginWaiting` / `Continue` / `Abort`; rarity comes off the record; scale-to-fit honoured. |
| `GachaPrizesScreenController.cs` | Multi-kind grid — the ten authored slots kept, the right prefab put in each. |
| `GachaHistoryStore.cs` | `GET /gacha/history`, disk mirror, `Prepend` after a pull. Mock deleted. |
| `GachaHistoryRecord.cs` / `GachaHistoryRow.cs` / `GachaHistoryScreenController.cs` | `DupeRp`; `BindGeneric` so character/item/ticket rows RENDER instead of being skipped. |
| `GachaTicketManager.cs` | `SetFromServer` / `RefreshFromServer`. `SpendTickets` and the dev grant deleted. |
| `GachaTabController.cs` | The two dead `OnPullX1/X10` deleted (their button paths never existed). |
| `SaveSchemaMigrator.cs` | Both dev-grant seeds removed. |
| `InventoryProjector.cs` | Tickets out of the blob, both directions. |
| `InventoryGrants.cs` / `InventorySyncService.cs` | `AppliedTicketCount` + `OnTicketGrantsApplied`; `DrainGrants(force:)`. |
| `ShopTransaction.cs` | The `ticket` grant case the B review found missing. |
| `GeneralShopCard.cs` / `GeneralShopModel.cs` | `BindTicket`, `BindForDisplay`, and `ShopCategory.Ticket`. |
| `PointsDtos.cs` / `PointsService.cs` / `Endpoints.cs` | The gacha DTOs, `ApplyEarnedBalance`, the three endpoints. |
| `GachaBannerCard.prefab` | Ten refs wired; the two cost labels re-pointed and widened; two stale `LocalizedText` removed. |
| `LocalizationText.csv` | Nine `GACHA_*` keys, EN + JA. |

### Deleted

`GachaMockPrizePool.cs` (+ `.meta`).

---

## Part 2 — Acceptance

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | §7 steps 1–8 run and pasted | **PASS (7 of 8; step 5 deliberately partial)** | Part 3. |
| 2 | §6 EditMode tests green; full unfiltered sweep green | **PASS** | `tests-run EditMode`: **2132 total, 2129 passed, 0 failed, 3 skipped** (the three pre-existing `HoleCompleteDriverTests` skips). |
| 3 | Mock pool, `BuildMock`, `SpendTickets`, the three dev-grant sites, `OnPullX1/OnPullX10` gone | **PASS** | `grep -rn` over `Assets/Scripts Assets/Tests` for each of the five symbols returns **0** live references. |
| 4 | Strings via the importer; `--check` clean; zero new `.text` literals | **PASS** | `import_content.py --apply` wrote 9 drafts; published **texts v18 → v19** (9 added / 0 changed / 0 deactivated); `export_content.py --check` → *"clean — no file would change and no catalog has drifted."* The only `.text = "…"` added is `"x" + cost` — a glyph, not a sentence; see Deviation 2. |
| 5 | A banner whose pool lacks a resolvable entry for a rated tier is withheld, with the warning | **PASS (test, not prod)** | `Withheld_WhenARatedTierHasNoResolvableEntry` drives the SHIPPING `IsRollable` with a resolver that cannot resolve the Legendary entry, and asserts the reason names `Legendary`. Not run against prod: doing it there means publishing a broken pool to the live catalog. |
| 6 | `ContentCatalogs.RequestList` carries the four gacha catalogs | **PASS** | Read out of the running Editor: `texts,clubs,characters,items,bags,balls,shop_catalog,level_up_costs,modes,gacha_banners,gacha_rates,gacha_pools,ticket_types` |
| 7 | All `[SerializeField]` refs wired; no Console errors; deviations flagged | **PASS** | Ten refs wired via `SerializedObject` and read back in the prefab diff. Deviations below. |

---

## Part 3 — The live E2E (§7), prod, from the Editor

Account `cesar.guarinoni@gmail.com` (the Editor's DevAutoSignIn account), API `playlife-api.fly.dev`,
build 2512, `PointsBackendEnabled` ON.

| Step | Result |
|---|---|
| **1. Grant 500 tickets** | **PASS.** Ledger `#0 Ticket 500`, movement `admin_grant +500 → 500`. Counter showed **500** at boot with no relaunch. |
| **2. PULL x1** | **PASS.** Modal waited, the server's prize (DRIVER G&F) revealed, counter **500 → 450**, the club was in the bag after the forced drain, history had it. Server: `x1 −50 Pity 0→1`. |
| **3. PULL x10** | **PASS.** Ten cards in server order, four dupes showed **+40 / +20 / +20 / +20 RP** and RP moved **478 → 578** — exactly +100. Server: `x10 −450 Pity 1→9`, `DUPE RP PAID 100`. |
| **4. Publish costX1 = 60** | **PASS, and this is the one that proves the whole design.** First tap → toast *"Price updated — tap again to pull"*, **nothing debited** (200 → 200). Card re-priced to **x60** (`08_cost_changed_to_60.png`). Second tap debited **60** (200 → 140). **No build.** Reverted to 50 (`gacha_banners` v5). |
| **5. New banner mid-session (5b)** | **PARTIAL — the mechanism is PROVEN, the brand-new row is not.** The mid-session install is live: the store went to `gacha_banners=v4 rows=1` and the card re-priced without a rebuild. Publishing a *new* banner to prod was skipped on purpose — I6 means the row could never be removed, only deactivated, and it would sit in the published catalog forever. The append path has its own test. |
| **6. Language + guarantee lines** | **PASS.** JA: title **スタンダードクラブ 1**, lines **レジェンダリー以上が確定・最大 [50回]** and **10連ごとにレア以上が1枚確定**, cost label **コスト** (`09_card_japanese.png`). `banner_standard_club1` = both lines; `banner_test_a` = **neither**, block collapses (`10_banner_no_guarantee_lines.png`); `banner_test_b` = Rare/30 + Uncommon (read off the live entry). |
| **7. Pause** | **PASS.** Toast *"Gacha is paused. Please try again later"*, tickets **200 → 200**, and the pull log still showed exactly 6 rows — the paused taps wrote nothing. Resumed. |
| **8. Offline** | **PASS.** `RootUrl` pointed at an unresolvable host (a real transport failure, not the flag): banners still rendered from cache, toast *"Connection required"*, tickets **140 → 140**. Restored. |

**The server's own ops panel, after the run:** 6 pulls · 2 300 tickets spent · 1 160 dupe RP paid ·
pity `49 / 50 to Legendary` · the last x10 carrying a **GUARANTEE** badge. Every number the client
showed, the server shows too.

---

## Part 4 — Deviations, flagged

1. **`GachaPullService` applies nothing.** §4.1 asks it to set tickets, fold RP, drain and record
   history on `Ok`. Three of those four live in Assembly-CSharp, which `Golfin.Economy` must not
   reference (the same split as `IServerBalanceSink`). Splitting them — one inside, three outside —
   would put the ORDER in two files, so all four moved to `GachaPullFlow.ApplyOk`, in the spec's
   order, behind an injectable seam a test drives. `GachaPullFlow` is the gacha's `ShopTransaction`.

2. **The cost reads `x50`, not `50`.** §3 writes `CostX1.ToString()`. The authored slot's
   placeholder is `"x1"`, it sits immediately after the ticket icon, and after an icon "x50" reads
   as fifty tickets where a bare "50" reads as a price in nothing. One character; the screenshot is
   in the folder for Cesar to overrule.

3. **The cost fields were re-pointed on the prefab.** Both pointed at the row's `CostText` — the
   authored word "COST", the one label §3 says must NOT be overwritten. They now point at
   `CountLabel`, the slot after the icon. The labels were also widened 80 → 150px with wrapping
   off, because `x450` wrapped to two lines at the authored width (caught in the first capture).

4. **`DrainGrants` gained `force:`.** Its once-per-session latch exists so a bag does not change
   while the player is looking at it. A pull is the player asking for it to change, and without the
   bypass the Prizes screen reads a bag the grant has not reached until the next launch. Nothing
   else passes `true`.

5. **~~The non-club prize card is scaled to 0.19~~ — REJECTED BY CESAR, and replaced.**
   §4.3 said to render a non-club prize on the Rewards-Center shop card and scale it to fit the
   slot. It does not fit (978×274 into 183×410 is a uniform 0.19) and the result was legible as a
   shape, not as text. Cesar's call: *"They should be the same size and shape as club."*

   **Every prize kind now draws on the CLUB card**, through a new
   `BagClubCard.InitializePrize(PrizeView)`. That is not a new design: `GachaHistoryRowBall.prefab`
   has nested a `BagClubCard` and bound ball data into it since gacha_history Stage 1 — portrait,
   name, an "x3" badge, the five stat lanes re-pointed at the ball's stats. This gives that pattern
   a name on the card itself, so four kinds share one shell instead of four hand-bound copies of
   its child paths. Per kind: **ball** → thumbnail, name, `x N` badge, its five stats at max 10;
   **character** → portrait, name, its four stats; **item** → sprite, name, `RESTORES N%` on the
   card's one free-text line, no stat lanes; **ticket** → icon, name, `x N`. The rarity frame is
   the SERVER's rarity in every case. Measured live: every card in a mixed x10 is **181×374 at
   scale 1.00**, identical to a club card. `06_prizes_x10_mixed_kinds.png`.

   `GeneralShopCard.BindTicket` / `BindForDisplay` and `ShopCategory.Ticket` STAY — they are what
   spec B's `category = ticket` shop rows bind with (§4.4), which is a separate surface from the
   prize grid.

6. **Two stale `LocalizedText` components removed** from the pity labels. They were bound to the old
   `GACHA_PITY_A_RANK` / `GACHA_PITY_S_RANK` placeholders and would have repainted over the
   row-bound text on every language change. The keys stay in the CSV, unused, as §3 says.

---

## Part 4b — Three more bugs, found by rebuilding the card

7. **A clone of an inactive scene object is born inactive.** The Prizes screen hands the binder the
   authored club card of the slot as its template, and hides that card BEFORE cloning it — so every
   non-club card was instantiated inactive and its slot rendered EMPTY. Measured: slots 6 and 7 of a
   mixed x10 blank, both the club card and its replacement inactive. `Instantiate` copies
   `activeSelf`; the binder now calls `SetActive(true)` on the clone.

8. **A slot returning to a club left its old card parked inactive** in `_spawnedCards` — which the
   next non-club prize in that slot would then clone from, reproducing bug 7. Those cards are
   destroyed and the dictionary entry dropped.

9. **Two text defects on the rebuilt cards,** both caught by reading the render rather than the
   code: the ball name printed twice (`ball_golfin` is named "Golfin" by brand "GOLFIN", so
   `name\nbrand` read "GOLFIN / GOLFIN" — an equal second line is now dropped), and the item's
   `RESTORES 75%` WRAPPED to three lines in a label sized for "250 yd" (auto-size + no-wrap now
   shrinks it to one line, leaving a club's own "250 yd" pixel-identical because it already fits).

---

## Part 4c — The item description on the card (Cesar, 2026-08-31)

A stat-less prize (item, ticket) left the lower two thirds of the card blank. It now carries the
item's own description — the same copy the **Item screen** prints under ITEM INFO, resolved through
the same ladder (`ITEM_INFO_<ID>` with the Items.csv `info` column as fallback), so the two screens
cannot describe the same item differently. It is already translated EN + JA for all three repair
kits, so it swaps language with everything else. Balls and characters keep their stat lanes and pass
no description; the space is only filled when it would otherwise be empty.

**It took four attempts, and the first three were wrong for the same reason: I kept blaming the
layout.** The label rendered as one long unwrapped line running off the card. In order I tried an
explicit width (came back 1228×14), stretch anchors (rewritten to (0,1) by the VerticalLayoutGroup),
and re-parenting outside the group (still 955×14 — a different number, same shape). The actual cause
was `TextMeshProUGUI.autoSizeTextContainer`, which resizes the component's OWN RectTransform to the
text's preferred size; every width written was correct and then immediately overwritten by TMP.
Measuring the live rect three times is what eventually named it — the second measurement, with a
different parent AND different anchors and still the width of the whole string, is what ruled the
layout out. Lesson for the shape: when a written value comes back as a DERIVED one, suspect the
component that derives it before the container that positions it.

---

## Part 4d — Distance icon and stat alignment (Cesar, 2026-08-31)

Two corrections to the prize card, both verified side by side in a mixed x10:

- **The distance arc is gone from a non-club card.** A repair kit read `⌒ RESTORES 100%`; the arc
  means DISTANCE and nothing that reaches `InitializePrize` has one. The icon is hidden there and
  put back by `RestoreClubRows` for the one kind that does have a distance.
- **A ball's stat lanes now line up with a club's.** StatsPanel is a vertical layout, so hiding the
  (empty) distance row pulled the five bars up by its height and a ball sat one row higher than the
  club beside it in the grid — which is exactly what it is meant to be compared against. The row is
  now KEPT as a blank spacer whenever the card has stat lanes, and only dropped on a card that has
  none to align (item, ticket).

---

## Part 5 — For spec D

- The Gacha Banners panel still says *"Pulls still run on the client-side mock until
  gacha_server_pull ships"*. That is now false twice over. Dashboard copy = out of scope here.
- **Nothing triggers a content refresh mid-session** — `ContentService` fetches once, at `Awake`.
  So 5b takes effect on the launch AFTER a publish, not while the app is foregrounded. The seam is
  correct and proven; the missing piece is a foreground/periodic refresh trigger, which is a
  `ContentService` change and belongs in its own spec.
- `simulate()`'s x10-guarantee slot (carried over from spec B) is still unfixed.
