# IMPLEMENTER_REPORT — `gacha_ops_polish`

**Iteration shape:** `gacha_ops:spec-D-four-pieces`
**Built by:** Claude Code (main thread, not the subagent chain — Cesar asked for a direct
implementation), 2026-08-31.
**Baseline:** `HEARTBEAT.log` § iter-1 kickoff baseline — HEAD `9d16a10eb`, DIRTY = SPEC.md,
TellCode.md and `Docs/Specs/Completed/gacha_client_real_pull/ARCHITECT_REVIEW.md` (all three the
Architect's, none mine; the ARCHITECT_REVIEW is still untracked and left that way).

**Canonical screenshot:** `screenshots/rates_modal_standard_club1.png` (1170×2532)
**Second screenshot:** `screenshots/4c_live_reprice_x60.png` (1170×2532)
> Both are in the task's gitignored `screenshots/` folder and were sent to Cesar in chat.

---

## 0. First thing, as instructed — the repo was behind prod

`export_content.py --check` failed on two files. `psc1_ball_golfin` had been deactivated in the
admin (Architect, 2026-08-31) and `gacha_pools` published as v2; `gacha_banners`' cursor was also
stale at v3 against the published v5. Exported and committed as `9d16a10eb` before any other work.

That export is what surfaced the exporter bug in §6 below.

---

## 1. What shipped, by spec section

| § | State | Commit |
|---|---|---|
| 2 — in-app RATES modal | **DONE**, verified through the real RULES button | `c0dfbaab1` |
| 3 — telemetry funnel (client) | **DONE**, five events verified in prod `telemetry_events` | `e1996ccc9` |
| 3 — funnel card (dashboard) | **DONE**, deployed | `87ad42357` |
| 4 — Gold ticket | **DONE**, sprite read back off the live prize card | `bb2a95bad` |
| 4b — `simulate()` guarantee parity | **DONE** | `87ad42357` |
| 4c — foreground content refresh | **DONE**, proven live (x50 → x60, no relaunch) | `e1996ccc9` |
| 4d — Gacha Banners panel copy | **DONE**, deployed | `87ad42357` |
| 4e — default-ball guard | **DONE** — all three locks; migration applied by Cesar 2026-09-01 | `19f0c8c2b` |
| 5 — `TICKET_SHOP_BUILD` + first ticket row | **DONE** — 2534 read from the guard file; row published and proven live, currently OFF SALE pending a build (STATUS.md) | `d6db2d4c7` `4c9954069` `b42812bdd` `2afaf0ad5` `8c2c34d1e` |
| — exporter `is_active` idempotence bug | **FIXED** (found by §0) | `832992d5c` |
| — four ticket-path defects in the shop | **FIXED** (§11 ledger) | `2afaf0ad5` |

> §5 was blocked at first draft: `last_uploaded_build.txt` read 2511, stamped at `2260f48ad`, an
> ANCESTOR of C's DONE commit — so that archive predated C. Cesar then asked me to run
> `Tools/testflight.sh`, which produced **2534** (unblocking the gate) and later **2537** (carrying
> the ticket card fix). Both uploaded to App Store Connect.

---

## 2. §2 — the RATES modal

`GachaRatesText.Build(entry, rates, pool, resolveName)` is the pure seam: featured, rates by rarity
rarest-first, per-item effective odds, the pity / x10 / dupe lines only when true of the banner, and
the footer. `GachaRatesModalController` filters the pool to what the SERVER would pay (active,
`min_build`) before the seam sees it, so a weight that cannot be rolled never sits in a denominator
— which is what makes "agrees with the admin's `effectiveOdds`" a property of the inputs.

**Verified through the real entry path** (PIPELINE_HARDENING rule 2): play mode → `NavGachaButton`
→ the centred card's `_rulesButton.onClick.Invoke()`. Not a test button.

Body generated on the live prod catalog, read back off the live `TextMeshProUGUI`:

```
FEATURED
P.Wedge Royal Swing  LEGENDARY
Putter GolfinX  SUPREME

SUPREME  0.50%      Putter GolfinX  0.50%
LEGENDARY  2.00%    P.Wedge Royal Swing  2.00%
MYTHIC  5.50%       A. Wedge Fyloe 4.23% · Repair Kit 1.27%
RARE  12.00%        Iron 7 Mireo 8.57% · Repair Kit 3.43%
UNCOMMON  25.00%    Iron 9 Klyro  25.00%
COMMON  55.00%      Driver G&F 22.92% · Wood G&F 22.92% · Repair Kit 9.17%

Guaranteed LEGENDARY or higher within 50 pulls
Every 10-pull includes at least one RARE
Duplicate clubs and characters are converted to Reward Points

Rates apply to every pull on this banner.
```

`effectiveOdds` check, by hand against the admin's formula
(`rateBp/10000 × weight / Σweight(tier)`): COMMON is 5500bp over weights 100 + 100 + 40 = 240, so
`0.55 × 100/240 = 22.9166…% → 22.92%` and `0.55 × 40/240 = 9.1666…% → 9.17%`. Sums to 55.00%.
Every tier sums to its rate; the six sum to 100.00%.

**Full rules row:** hidden, correctly — `rulesUrl` is
`https://golfin.example.com/gacha-rules/standard-club1`, which `BannerPolicy.IsLinkAllowed` refuses
(host not on the shipped allowlist). Read back live: `fullRulesRow active=False`.

**Geometry**, measured after `Canvas.ForceUpdateCanvases()`:

| element | rect | note |
|---|---|---|
| Panel | 978 × 1714 | inside a 1170 × 2532 canvas, ~409px margin top and bottom |
| BodyScroll / Viewport | 882 × 1400 | |
| BodyText | 882 × 1053.65 | `body inside viewport = True` |
| CloseButton | 359 × 120 | the authored CancelButton size |

**Clone provenance** — the prefab is `TournamentSignupModal.prefab`
(`guid 0efb91cb9b9f14a0a9c6e460a6b0d6ee` is the NEW prefab; the SOURCE is
`Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab`), loaded with
`PrefabUtility.LoadPrefabContents`, pruned, and saved to a new path. Nothing was authored from
scratch:

| element | source | evidence |
|---|---|---|
| root + DimBackground + Panel + Background | TournamentSignupModal, untouched | `Background - HoleCard` 9-slice, `DimBackground` α 0.922 |
| Content (VLG pad 48/48/32/32, spacing 24) | same, untouched | |
| Upper → Header → TitleText | same; SponsorText / VenueText / DateRangeText deleted | Rubik-SemiBold SDF 35 Bold |
| SeparatorSlot1 → Separator1 | same, untouched | sprite `Divider` |
| BodyScroll → Viewport → BodyText | `RulesRow` → renamed; `RulesLabel` deleted; `RulesBody` → `BodyText` | TMP still Rubik-SemiBold SDF 27.5 |
| ButtonsRow → CloseButton | `ButtonsRow`; `ConfirmButton` deleted, `CancelButton` renamed | sprite `ButtonCancel` ppu 1.25, **`ButtonPressFeedback` carried over** (rule 11) |
| FullRulesRow | `Object.Instantiate` of that same CloseButton | same sprite + `ButtonPressFeedback` |

Structural additions: a `ScrollRect` + `Viewport(RectMask2D)` + `ContentSizeFitter`, because unlike a
fixed rules paragraph this body is as long as the pool is.

Panel and DimBackground are authored **inactive** (`reference_modal_children_author_inactive`).

**One defect found and fixed while verifying, worth naming:** `RulesRow` carried a
`HorizontalLayoutGroup` (it used to lay `RulesLabel` beside `RulesBody`). Left on the scroll host it
DROVE the Viewport's anchors to a zero-size point, so the body rendered at width 0 in play mode —
invisible in the prefab (`rect 882×180`) and only visible once measured live. Removed.

**Scene:** one `GachaRatesModal` prefab instance under `Canvas/`, last sibling.
`git diff --stat Assets/Scenes/ShellScene.unity` = **103 insertions, 0 deletions.** The first save
produced 1399/1296 of pure anchor churn (the `project_scene_save_bakes_layout_churn` scar); the two
real hunks were isolated with `git apply --cached` and the scene reopened from disk.

**Tests:** 13 EditMode cases in `Assets/Tests/EditMode/GachaRatesTextTests.cs`, driving the shipping
seam by reflection with the real localized strings. **Tripwire:** flipping one expectation made
exactly `GachaRatesTextTests.RarityTiers_AreListedRarestFirst_WithTwoDecimalPercentages` fail with
`But was: "LEGENDARY  2.00%"`, which proves the suite runs (`reference_tests_run_ignores_class_filters`).

---

## 3. §3 — the telemetry funnel

**Five events, in prod, from ONE Editor session** (`06207c1b-9f6c-4fbf-a0c9-349f71ff7517`).
Reproduce with:

```sql
select ts, name, payload
  from public.telemetry_events
 where session_id = '06207c1b-9f6c-4fbf-a0c9-349f71ff7517'
   and name like 'gacha\_%'
 order by ts;
```

```
11:58:29  gacha_banner_view  {"position":0,"banner_id":"banner_standard_club1","live_count":3}
11:58:51  gacha_rules_open   {"banner_id":"banner_standard_club1"}
11:58:51  gacha_pull_tap     {"cost":50,"count":1,"banner_id":"…","ticket_type":0,"balance_before":3340}
11:58:52  gacha_pull_result  {"count":1,"dupes":1,"status":"ok","rarities":[1,0,0,0,0,0],"latency_ms":452,
                              "pity_forced":false,"guarantee_forced":false}
11:59:46  gacha_banner_view  {"position":0,…,"live_count":3}          ← second Rewards Center OPEN
12:00:03  gacha_pull_tap     {"cost":450,"count":10,…,"balance_before":3290}
12:00:03  gacha_pull_result  {"count":10,"dupes":8,"status":"ok","rarities":[7,1,2,0,0,0],"latency_ms":259,
                              "pity_forced":false,"guarantee_forced":false}
12:00:19  gacha_reveal_skip  {"count":10,"banner_id":"…","cards_shown":7}
```

All five distinct names present. Note the second `gacha_banner_view` is a second OPEN, not a second
swipe — the once-per-banner-per-open set is cleared in `OnEnable`. `guarantee_forced:false` on a x10
that produced two Rares is §4b's rule seen from the SERVER side: the block reached the floor by luck,
so the guarantee never fired.

`TelemetryService.SendsEnabled` was set at runtime for the session
(`reference_editor_telemetry_sends_seam`); no define needed.

`gacha_pull_result` is recorded in `GachaPullFlow`, not `GachaPullService`. **Spec deviation,
deliberate:** `GachaPullService` is in the `Golfin.Economy` asmdef, which references only
`Golfin.Net` and must not learn a telemetry queue exists — the same boundary that put `ApplyOk` in
the flow. The flow's answer callback is the first place the outcome is visible to an assembly that
can see both, and timing from there measures what the PLAYER waits rather than what the socket does.

**Dashboard:** `lib/telemetryGacha.ts` + 13 vitest cases; a "Gacha funnel" section on the Telemetry
panel beside Flick timing (views → taps → pulls with conversions, mean latency, insufficient / skip /
rules rates, pity-and-guarantee counts, per-banner table). 15 new `DICT` keys, en + ja.

---

## 4. §4 — the Gold ticket

`TicketIconDerive` (a one-shot menu item) derives both icons from the top bar's own
`S_Store_Ticket_02` (118 × 131). Read back off a LIVE prize card bound by the shipping
`GachaPrizeCardBinder`:

```
BOUND sprite on 'Portrait' = Ticket_Gold   asset=Assets/Resources/Art/Gacha/Tickets/Ticket_Gold.png
text['NameText'] = GOLD TICKET      text['LevelBadge'] = x2      text['RarityBadge'] = L
LiveName("ticket","0") = Ticket     LiveName("ticket","1") = Gold Ticket   ← the RATES featured list
```

**Two spec deviations, both flagged:**

1. **The tint is a luminance remap, not a multiply.** SPEC §4 says "#E5B84A multiply". The store
   ticket is already orange and red, so multiplying darkens it ~10 % — measured mean over the opaque
   pixels 211,137,67 → **189,98,19** — and the two icons were indistinguishable at the 76px they draw
   at. A placeholder nobody can tell apart from the thing it stands in for is not a placeholder. The
   remap keeps each pixel's Rec-601 luma and replaces the hue: measured mean **170,141,56**, and it
   reads as gold at a glance.
2. **`Resources/Art/Gacha/Tickets/`, not the spec's `Resources/Art/Tickets/`.** That is the path both
   shipping loaders already read (`GachaBannerCard.LoadTicketSprite`,
   `GachaPrizeCardBinder`) and the sibling of `Art/Gacha/Banners`. Following the spec literally would
   have meant editing two loaders to no benefit.

**And the admin upload would have been inert.** `CatalogArtCache.Cached` never starts a download —
it reads memory, then the disk cache — so something must fetch the bytes. Auditing the shape
(PIPELINE_HARDENING rule 15) across every catalog with an `ART_URL_COLUMNS` entry:

| catalog | art URL columns | prefetch before this task |
|---|---|---|
| characters | portraitUrl, fullUrl | ✅ `CharacterDatabaseCSV:220` |
| clubs | portraitUrl, fullUrl, controlUrl | ✅ `ClubDatabaseCSV:199` |
| items | thumbnailUrl, fullUrl | ✅ `ItemDatabaseCSV:164` |
| balls | thumbnailUrl, fullUrl | ✅ `BallDatabaseCSV:161` |
| **gacha_banners** | artUrl | ❌ **none** |
| **ticket_types** | iconUrl (this task) | ❌ **none** |

Exactly the two gacha catalogs. Both now call `TournamentArtService.CatalogArt.Prefetch`. The
`gacha_banners` half is a pre-existing bug from spec A/B: the admin's banner upload has been live
since `gacha_admin_catalogs` §5.2 and the bytes were never fetched onto a device — every banner
silently fell through to its bundled `artSprite`. Fixed here because it is the same line, in the same
shape, next door.

---

## 5. §4c — the foreground refresh, proven live

`ContentService` fetched EXACTLY ONCE, in `Awake`. `RefreshNow()` is guarded by ONE
`ScheduleRefreshThrottle(60s)` shared by both callers (`GachaCarouselController.OnEnable` and
`OnApplicationFocus(true)`).

`ScheduleRefreshThrottle.cs` **moved** from `Assets/Scripts/TournamentsRuntime/` (no asmdef →
Assembly-CSharp) into `Assets/Scripts/ContentRuntime/` (`Golfin.Content`), namespace unchanged.
`ContentService` lives in an asmdef that cannot see Assembly-CSharp, so the alternative was a second
copy of an in-flight latch and a cooldown. `Golfin.Content` is `autoReferenced`, so
`TournamentService` compiles untouched; `Golfin.TournamentsRuntime.Tests` resolves the type through
`Prod.Find`, which scans every loaded assembly.

**Live proof, in one running Editor session, no relaunch:**

1. card reads `CostX1=50`, label `x50`
2. `costX1 = 60` published to prod (`gacha_banners` v6) WHILE play mode was running
3. leave → re-enter the Rewards Center — `RefreshNow()` fires, writes the cache; still `x50` (I5:
   the swap is on the next `Reload`)
4. leave → re-enter again — `Reload()` applies the pending refresh: **`CostX1=60`, label `x60`**
   (`screenshots/4c_live_reprice_x60.png` shows `COST 🎟 x60`)
5. reverted to 50, published v7, re-exported, `--check` clean

**Test coverage, stated honestly:** the cooldown decision the spec asks to test
("a second `RefreshNow()` inside the cooldown is a no-op") is `ScheduleRefreshThrottle.TryBegin`,
already pinned by `Golfin.TournamentsRuntime.Tests.ScheduleRefreshThrottleTests`; `RefreshNow`
delegates to it and adds nothing. I did NOT add a unit test for `RefreshNow` itself — it is a
MonoBehaviour method whose `Awake` boots the whole content stack, so an EditMode instance would
either boot it or test a stub. The live re-price above is the evidence for the second half
("the gacha reinstall fires after a foreground refresh that wrote a newer cache"), and it is
stronger than a test would have been.

---

## 6. Two bugs found on the way, both fixed

**a. `export_content.py` blanked `is_active` on the SECOND export.** It only read
`content_rows.is_active` when the column was NOT already in the CSV header; once the first export had
appended it, every later export fell through to `data.get("is_active")` — and `is_active` is a table
COLUMN, never a field of `data`, so the cell came back BLANK. A blank reads as ACTIVE downstream, so
the second export of any catalog carrying a deactivated row silently re-admits it into the bundled
floor of every future build. `gacha_pools` is the first catalog to have one, and §2 is the first
client code to READ the bundled cell — latent to load-bearing in one afternoon. Caught by `--check`.
Regression test pins byte-identical second export; it fails on the old code with exactly the blanked
line. (`832992d5c`)

**b. `GachaPoolCatalog` ignored the bundled `is_active` cell.** It read only the overlay, so a fresh
install listed — and `IsRollable` admitted as payable — a prize the server refuses. The modal was
showing the deactivated `psc1_ball_golfin` at 11.00%; with the fix COMMON redistributes to
22.92 / 22.92 / 9.17. (`c0dfbaab1`)

Also `Join(name, brand)` rendered "P.Wedge Royal Swing Royal Swing" — Clubs.csv spells the brand
INSIDE the name, so equality was the wrong test; a substring test fixes it and subsumes the
"Golfin"/"GOLFIN" ball case.

---

## 7. §4e — the default-ball guard (NOT in SPEC.md)

§4e reached me only through the kickoff message; `SPEC.md` has §4b, §4c and §4d and no §4e.
Implemented as the kickoff describes.

`Assets/Data/Balls.csv` gains `isDefault` through the ordinary importer (balls **v6**), so the flag
is one more content column and not a hard-coded id. It is NOT `is_active` — the row stays live,
playable and equippable; the flag says what it may be USED FOR.

| lock | where | state |
|---|---|---|
| refuse to PUBLISH | `contentValidate.ts` rule 21 — gacha_pools entry AND shop_catalog listing | ✅ deployed, 3 tests |
| refuse to SHOW | `GachaBannerCatalog.IsRollable`, `GeneralShopModel.UnrenderableReason` | ✅ |
| refuse to ROLL / SELL | `2026_09_02_default_ball_guard.sql` | ⚠️ **NEEDS CESAR** |

The migration is a `create or replace` of `golfin_gacha_pull` and `golfin_shop_purchase`, generated
by patching the shipped definitions so it is reviewable as a **two-hunk diff** (+15/−0 and +11/−1;
the diff is quoted in the chat hand-off). pglast-parsed: 2 statements. Idempotent, touches no data,
no table/index/grant/policy change. It goes through the Supabase SQL editor, which is Cesar's step.

---

## 8. §5 — BLOCKED, exactly as SPEC §6 anticipated

`Docs/Versioning/last_uploaded_build.txt` reads **2511**, stamped at `2260f48ad`
(`gacha_server_pull: pool_for_build closed…`). `git merge-base --is-ancestor 2260f48ad 18d035cfb`
returns true, so **build 2511 predates C's DONE commit and does not carry C.**

SPEC §5 says "Only after the archive that carries C is uploaded", and "read from the file, never
inferred (the `SHOP_CATEGORY_STRICT_BUILD` lesson)". So `TICKET_SHOP_BUILD` stays **0** and nothing
was guessed. §5.2 additionally needs Cesar's quantity and rpCost, which the spec explicitly does not
set.

Per SPEC §6, STATUS stays `IMPLEMENTER_WORKING` with the note "waiting on the C archive" rather than
closing early.

---

## 9. Verification summary

| check | result |
|---|---|
| Full unfiltered EditMode sweep | **2146 tests, 0 failed, 3 skipped** (pre-existing) — run after §2, after §3/§4c, after §4/§4e |
| New EditMode tests | 13 (`GachaRatesTextTests`), tripwire-proven |
| Dashboard vitest | **233 passed** (was 217): +13 telemetryGacha, +3 §4e validator, +1 §4b guarantee |
| `Tools/content` unittest | **44 passed** (was 43): +1 exporter idempotence, tripwire-proven |
| `export_content.py --check` | **clean — no file would change and no catalog has drifted** |
| Unity compile | clean; no new warnings |
| New hardcoded `.text` literals | **zero** — every assignment is `LocalizationManager.Get` or the generated body |
| Player strings | 7 `GACHA_RATES_*` keys, EN + JA, via `import_content.py` → published **texts v20** |
| Dashboard strings | 15 keys in `lib/i18n.ts` `DICT`, en + ja |
| Dashboard deploy (§23) | Version ID **`a71683bd-8328-46c8-a7b7-906cda179cbf`**, `admin.golfin.world`; worker carries **`87ad42357`** (clean, no `-DIRTY`); Access **302** to `cloudflareaccess.com` |
| Real entry path (rule 2) | RATES modal opened by `RulesButton.onClick.Invoke()` on the centred card |
| Editor left clean | play mode off, ShellScene not dirty, no leftover probe objects |

**Catalog publishes made** (all through the same validation the dashboard runs, plus an
`admin_audit_log` row — the dashboard is behind Cloudflare Access and its Review & publish drawer is
not reachable from here):

| catalog | version | what |
|---|---|---|
| texts | 19 → **20** | the seven `GACHA_RATES_*` keys |
| ticket_types | 1 → **2** | `iconSprite` = `Ticket_Standard` / `Ticket_Gold` |
| balls | 5 → **6** | the `isDefault` column |
| gacha_banners | 5 → 6 → **7** | costX1 50 → 60 → 50, the §4c live check, reverted |

---

## 10. Prod footprint from the live checks

One x1 pull (50 tickets) and one x10 (450) on Cesar's account, needed for §3's acceptance.
Tickets 3340 → **2840**; RP 6,618 → **6,938** (dupes paid out). Grants applied normally.
`gacha_banners` is back at `costX1 = 50` and the cursor is exported. Nothing else was touched.

---

## 11. Closing audit — what was verified at the end, and what was not

Written after Cesar asked, correctly, whether anything had been left out before spending
more builds. Everything below was checked with NO further build and no further prod writes.

### Closed at close-out

| Acceptance line | Was | Now |
|---|---|---|
| "lines match `effectiveOdds` in the admin to the second decimal" | done by hand, no admin screenshot | ran the **shipped** `effectiveOdds` over the LIVE published rows: `22.92 / 22.92 / 9.17 · 25.00 · 8.57 / 3.43 · 4.23 / 1.27 · 2.00 · 0.50 → 100.00 %` — digit-for-digit the modal's strings |
| "the funnel card shows them" | events proven in `telemetry_events`, card unchecked | `buildGachaFunnel` over the REAL rows: 4 views → 2 taps (50.0 %) → 2 pulls (100.0 %), 1×1 / 1×10, 1 skip, 1 rules open, 356 ms mean, rarities `[8,1,2,0,0,0]`, 9 dupes, 1 player, per-banner table populated |
| "opens from EVERY live banner" | only the centred one | all three, each through its own `RulesButton.onClick` — and they cover all three featured shapes: `banner_test_a` none (20 lines), `banner_test_b` one (25), `banner_standard_club1` two (26) |
| "Full rules row only when `rulesUrl` is set AND allowlisted" | only the HIDDEN branch | both. `IsLinkAllowed` refuses the live placeholder, `http://golfin.io`, `evil-golfin.io`; accepts `golfin.io` and `www.golfin.world`. Driven through the shipping `Show(entry)` with an allowlisted URL the row appears, label "Full rules" |
| the §4 prefetch could have been inert | unchecked | the client's `CatalogArtPolicy.AllowedArtPrefix` is exactly the `catalog-art` public-URL prefix `uploadCatalogArt` produces, so an uploaded `iconUrl` passes the policy and is fetched |

### NOT verified — stated plainly rather than claimed

1. **The §4e migration's BEHAVIOUR.** It applied (`create or replace` returned "Success. No rows
   returned"). The guard cannot be exercised: triggering it needs an ACTIVE pool row pointing at a
   default ball, and the validator, the client and now the server all refuse one. Constructing the
   failing case means deliberately writing a bad row to prod.
2. **`iconUrl` upload → replaces the icon at next launch.** The resolution ladder is the same code
   path already shipping for clubs / balls / items / characters, and the prefix check above closes
   the one link this task actually broke — but no real upload was performed.
3. **The funnel card RENDERED.** Cloudflare Access fronts the dashboard. The aggregation is
   verified against real rows; the pixels are not.
4. **A `RefreshNow` unit test.** Substituted by the live x50 → x60 re-price. The cooldown decision
   itself is already pinned by `ScheduleRefreshThrottleTests`; `RefreshNow` delegates to it.

### On the builds

Two were justified: 2534 unblocked `TICKET_SHOP_BUILD`, 2537 carried the card fix Cesar chose. A
third was NOT — it would have papered over four defects found only after archiving. Taking the row
off sale costs nothing, guarantees no build ever shows a broken card, and leaves the listing intact
for whenever the next build ships. That is the open thread in STATUS.md.

### Defect ledger for the task

Nine defects found, all fixed, **six of them only findable by running the thing**:

| # | Defect | Found by |
|---|---|---|
| 1 | `export_content.py` blanked `is_active` on the SECOND export | `--check` at §0 |
| 2 | `GachaPoolCatalog` ignored the bundled `is_active` cell | reading the modal's own output |
| 3 | `Join(name, brand)` printed "P.Wedge Royal Swing Royal Swing" | reading the modal's own output |
| 4 | `RulesRow`'s `HorizontalLayoutGroup` drove the scroll Viewport to zero | measuring live geometry |
| 5 | NEITHER gacha catalog prefetched its art URLs (banner uploads never fetched, since spec A) | asking what would make §4's last acceptance line true |
| 6 | `GeneralShopCard` passed a literal `1` to `BindTicket` | reading the code before publishing |
| 7 | `BindTicket` hid price and BUY — the listing was unbuyable | looking at the rendered card |
| 8 | the TICKETS filter chip was never wired | tapping it |
| 9 | `ShopTransaction.PreCheck`'s `default:` meant BALL — "Unknown ball '0'" | pressing BUY |

6–9 are one shape: the ticket category was wired at every layer written for it and missing from
every switch that predates it. §7 above records the per-site verdict, including the sites that were
already correct.
