# SPEC — `weekly_rotation_client`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-09-08 (Architect via Cowork). Second half of the weekly rotation
> (`Docs/Economy/MONETIZATION_PLAN.md` §1.2). **Starts only when `weekly_rotation_admin` is DONE
> and its first rotation is published** — every acceptance line below needs live rows.
>
> Standing rules: player strings EN + JA through the importer; no hardcoded `.text`
> literals; **Figma exists** (§1a) — badge atoms and placement come from it, built as clones of
> existing prefab atoms (no new hierarchies beyond the tags and the header row); EditMode sweep
> green; no device pass.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

The game already *obeys* a rotation (windows withhold rows; the server refuses out-of-window
rows and banners). It does not *show* one: a player cannot tell that the STORE changed, when
it will change again, or which cards are this week's. And a rotation that ends while the
STORE is open leaves stale cards until the next launch, because `GeneralShopCatalog`
evaluates windows once at load. This task adds the lineup header with a countdown, `NEW`
tags on the week's cards, a `THIS WEEK` tag on the weekly gacha banner, and the mid-session
roll-over.

---

## 1. What is true today (read 2026-09-08)

| Piece | State |
|---|---|
| `GeneralShopCatalog` (`GeneralShopModel.cs`) | bundled CSV + `shop_catalog` overlay; `Admit` evaluates `ContentShopWindow.Evaluate` ONCE per load (`Reload()` resets); `GetByCategory` sorts by `SortOrder`; `ShopCatalogEntry` has no `RotationId` |
| `GeneralShopScreenController` | `Rebuild()` instantiates cards into `_grid`; `_banner = _grid.Find("WinterSaleBanner")` kept as first sibling (the `store_banner` slot); chips `ALL/CLUBS/BALLS/CHARACTERS/ITEMS/TICKETS` |
| `GeneralShopCard` | `Bind` → per-kind binders; `PriceBox/SaleBG` toggled for a sale; rarity tile via `SetRarityTile` |
| Gacha carousel | `GachaCarouselController.FormatCountdown(TimeSpan)` (static) ticks each card's `_countdownLabel` from `EndUtc` every frame; `GachaBannerEntry` gains nothing for rotations yet; `GachaCsvMerge.PickLocalised(nameEn, nameJa)` is the locale pick |
| `ContentService` | `RefreshNow()` throttled 60 s (gacha_ops_polish 4c); `TryReinstallFromCache` **allowlist = the four gacha catalogs only** (refuses others with a warning at `:663`); `OnCacheRefreshed` |
| Rows after `weekly_rotation_admin` | `rotations` catalog (`rotationId, startUtc, endUtc, nameEn, nameJa, …`); `shop_catalog.rotationId`; `gacha_banners.rotationId` + `pityGroup` |

---

## 1a. Reference — Figma (file `5gEAHjl6xAtW8iYY7NMvWd`, made 2026-09-08)

| What | Page · node | Render |
|---|---|---|
| **`Badge` component set** — variants `Type = New / Sale / Discount / LastChance / ThisWeek / Limited / Owned` | Store · section *Badges & Weekly Rotation* `14163:105273`, set `14163:105261` | `reference/figma_badges_14163-105273.png` |
| `Countdown Pill` component | Store · `14163:105262` | same render |
| `Lineup Header` component (978×72) | Store · `14163:105265` | same render |
| STORE placement demo (header under the banner, cards shifted +84, NEW / LAST CHANCE on card corners, −25 % on the sale price box) | Store · `14163:105654` | `reference/figma_store_demo_14163-105654.png` |
| GACHA placement demo (THIS WEEK beside the ENDS IN pill, `3 LEFT` right-aligned to the banner edge) | Gacha · `14163:106259` | `reference/figma_gacha_demo_14163-106259.png` |

**Badge atom (all variants):** Rubik SemiBold 26, uppercase, 4 % tracking; pill r24; padding
20 / 8; 2 px inside stroke; drop shadow 0/4/8 @ 35 %. Fills are vertical gradients — NEW
`#fcf195→#c9962f` text `#321506` stroke `#ffe48b` (the BUY-button family); SALE / DISCOUNT
`#f0566a→#a8172b` white, stroke `#ffb3bd`; LAST CHANCE `#f7a63a→#b85e0a` text `#321506`, stroke
`#ffd9a3`; THIS WEEK `#5c93e0→#2c5aa0` white, stroke `#bcd3f7`; LIMITED `#9a6cf0→#5b2fa6` white,
stroke `#d6c4fb`; OWNED `#5c6478→#3a4051` text `#dfe4ee`, stroke `#8d97ab`. Countdown pill:
`#091b33`@0.70, stroke `#818ea1` 1 px, gold dot `#eedc9a` 14 px, Rubik Medium 24 white. Header:
978×72, r36, panel gradient `#133453→#091b33`, stroke `#818ea1` 2 px, padding 24, title Rubik
SemiBold 30 `#eedc9a` 2 % tracking, THIS WEEK badge left, countdown pill right.

**Which badge when (client rules, one at a time per card, first match wins):** LAST CHANCE when
`< 24 h` remain in the row's window · NEW when `rotationId == live` · OWNED as today. DISCOUNT
(`-N%`, `N = round(100 − sale/list×100)`) is independent and sits on the price box whenever the
sale window is on — it replaces the plain SALE variant in-game (SALE exists for a sale without a
computed percentage). LIMITED is gacha-only in this spec (`maxPullsPerPlayer` set → `{remaining} LEFT` in the Limited
style — the word LIMITED is the variant name, not the label, so the pill fits beside ENDS IN +
THIS WEEK inside the 882-wide banner; remaining = `maxPullsPerPlayer − pulls_used` from the pull
response). The Figma demo's dollar prices
are the old mockup — the game shows RP.

**Prefab mapping:** badge = the rarity-tile chip atom re-skinned per variant (one prefab
`ShopBadge` with a `Variant` enum driving fill gradient sprite / text colour — bake the six
gradients with the `make_*_panels.py` script family, do not hand-paint); countdown pill = clone of
the gacha card's `ENDS IN` pill; header = the chip-row panel atom at 978×72.

## 2. Client model

- **`RotationCatalog`** (`Assets/Scripts/UI/Shop/RotationModel.cs`, `Golfin.Shop` beside
  `GeneralShopModel`): the tiny read-mostly loader shape of `GachaRatesCatalog` — bundled
  `Resources/Data/rotations.csv` + `ContentCatalogStore.Catalog(ContentCatalogs.Rotations)`
  overlay patch by `rotationId`, appended rows admitted, `is_active=false` drops,
  `RequireReady` for EditMode, the shared quoted-field parser (name it in the report).
  `ContentCatalogs.Rotations = "rotations"`. Entry: `RotationId, StartUtc, EndUtc, NameEn,
  NameJa`. **`Live(DateTime nowUtc)`** = the active row with `StartUtc ≤ now < EndUtc`, lowest
  `StartUtc` on a tie; **`Next(now)`** = the earliest row with `StartUtc > now`.
- `ShopCatalogEntry.RotationId` (string, blank allowed) parsed from `rotationId`;
  `GachaBannerEntry.RotationId` likewise. Nothing about admission changes — rows are still
  admitted by their own windows, never by the rotation row (two locks, neither trusts the
  other: a rotation row that goes missing must not hide or reveal a shop row).

## 2a. Architecture context — polish atoms (Cesar 2026-09-10: "keep all new modals/screens up to par with our polish pass")

Verified in the repo 2026-09-10: the STORE already paints through the polish pass —
`GeneralShopScreenController.Rebuild` runs `Golfin.Gps.UI.GpsPaintMotion.StaggerRise(this, rows)`
on the first paint of an entry (instant when `SuppressedByPush`, instant on a filter change), and
BUY goes through `PendingSpend.Begin(card.BuyButton, card.BuyLabel)`. Everything this spec adds
rides those atoms (`Assets/Scripts/UI/Polish/`); **no new motion code**, and every atom honours
`UiMotion.Enabled` for free.

| Element | Atom | Exact behaviour |
|---|---|---|
| Lineup header on first paint | `GpsPaintMotion.StaggerRise` | The header is **row 0** of the `rows` list handed to `StaggerRise` (it sits above the cards) so it rises 0.03 s before card 1; nothing separate |
| Header title / countdown change at roll-over | `UiSelection.FadeSwap(this, headerGroup, repaint)` | `CanvasGroup` on `LineupHeader`; the roll-over rebinds title + countdown inside `repaint`. The countdown's once-per-second text tick does NOT fade — plain `.text` (a fade per second is noise) |
| Countdown reaching zero → cards rebuild | existing `Rebuild` first-paint path | Set `_firstCardPaint = true` before the roll-over `Rebuild()` so the new lineup **stagger-rises** like a fresh entry instead of snapping (quote the log line `paint(local) … staggered`) |
| `ShopBadge` appearing on a card | none beyond the card's own rise | The badge is a child of the card; it rises with the card. No independent pop — one motion per card (`design_consistency_audit` rule) |
| `ShopBadge` on the gacha banner card | none | Part of the card's existing bind; the carousel owns motion |
| Roll-over toast | existing `ToastController` | The toast already animates; nothing to add |
| Header tap | no tap | The header is informational — **no `Button`, no `ButtonPressFeedback`**, so `PressFeedbackCoverageTests` stays unchanged (say so in the report) |
| Cold fetch | none | The header binds from the bundled/overlaid catalog synchronously — no network wait, so no `ShimmerHost` / `GameShimmerSites` site (waits under ~200 ms never shimmer) |
| Card BUY on a lineup row | `PendingSpend` (unchanged) | Already the shop's path; a `NEW` badge does not change it |

Evidence the report owes: a frame strip (every 2 frames, 0.5 s) of the roll-over — header
`FadeSwap`, cards stagger-rising, toast — and `UiMotion*` / `ScrollFeelTests` /
`PressFeedbackCoverageTests` green.

## 3. STORE tab

**3.1 Lineup header.** A `LineupHeader` row inserted by the existing shop builder script
under `_grid` **as the second sibling** (after `WinterSaleBanner`, before cards); the
controller finds it by name like `_banner`. Build it to the Figma `Lineup Header` node
(§1a: 978×72, THIS WEEK badge + gold title left, countdown pill right) from the chip-row panel
atom. Cards shift down by the header height + 12 px gap, as in the demo.

- Title = `GachaCsvMerge.PickLocalised(live.NameEn, live.NameJa)`; when there is no live
  rotation but a `Next`, title = the next one's name and the countdown reads
  `SHOP_LINEUP_STARTS_IN`; when neither, the header is **hidden** (SetActive false) — the
  store behaves exactly as today.
- Countdown = `SHOP_LINEUP_ROTATES_IN` with `{0}` = `GachaCarouselController.FormatCountdown(live.EndUtc − now)`.
  Move `FormatCountdown` to a shared static (`Golfin.UI.CountdownFormat`) and have the
  carousel call it — one formatter, no copy. Tick once per second (a coroutine on the screen,
  not per-frame — the shop has no per-frame loop today and does not need one).

**3.2 Badges on cards.** `GeneralShopCard.Bind` drives ONE `ShopBadge` (§1a mapping) at the
card's top-left corner (x +24, y −16 overlapping the rim, as in the demo) per the "which badge
when" rules, and a second `ShopBadge` in the `Discount` variant on the price box (top-right,
overlapping) whenever `HasSale`. Added to BOTH templates (`GeneralShopCard_Club`, `_Ball`) by the
builder, hidden by default. Cards from a *previous* rotation still in window (overlap week, or a
hand-authored row) show no NEW tag. The `-N%` text is `SHOP_CARD_DISCOUNT` with `{0}` = N.

**3.3 Mid-session roll-over.** In the once-per-second tick: when `live` was non-null and
`now ≥ live.EndUtc`, or `live` was null and `Next.StartUtc ≤ now` → call
`ContentService.RefreshNow()` (throttled; a no-op inside the cooldown is fine — the overlay on
disk already holds the next week's rows because they were published in advance), then
`GeneralShopCatalog.Reload()` + `Rebuild()` and toast `SHOP_LINEUP_UPDATED` through the
existing `ToastController`. Guard: at most once per rotation boundary (remember the id you
rolled to).

**3.4 Live re-apply.** Add `ContentCatalogs.ShopCatalog` and `ContentCatalogs.Rotations` to
`TryReinstallFromCache`'s allowlist, and subscribe the shop screen to `OnCacheRefreshed` the
way `GachaBannerCatalog` does (pending flag → reinstall + `Reload()` + `Rebuild()` on the next
`OnEnable` / tick). Justification for the I5 exception, to be quoted in the report: shop rows
carry no owned-state; the server is authoritative at purchase (`price_changed` / `not_listed`
already handle a row that changed under the player); and the whole point of a rotation is
that a publish lands without a relaunch. Deviation from I5 is deliberate and scoped to these
two catalogs.

## 4. GACHA tab

`GachaBannerCard.Bind`: when `entry.RotationId` is non-blank, show a `ShopBadge` in the
`ThisWeek` variant (`GACHA_TAG_WEEKLY`) to the right of the `ENDS IN` pill, 16 px gap, vertically
centred on it (Figma demo `14163:106259`); when `MaxPullsPerPlayer` is set, a `Limited` variant
**right-aligned to the banner card's inner edge (24 px inset), same row**, reading `GACHA_TAG_LIMITED`
with `{0}` = remaining pulls — never a third pill in the flow, it does not fit. The carousel already
ticks the banner's `EndUtc` and already sorts by `SortOrder` (the weekly banner is 0, first).
The RATES modal already shows the boosted `effectiveOdds`. Nothing else.

## 5. Strings (importer: EN + JA same commit → PLAN → `--apply` → **publish `texts` from the admin** → `--check` clean). No keys retired by this spec.

| Key | EN | JA |
|---|---|---|
| `SHOP_LINEUP_ROTATES_IN` | `New lineup in {0}` | `次のラインナップまで {0}` |
| `SHOP_LINEUP_STARTS_IN` | `Lineup starts in {0}` | `ラインナップ開始まで {0}` |
| `SHOP_CARD_NEW` | `NEW` | `NEW` |
| `SHOP_LINEUP_UPDATED` | `The lineup has changed` | `ラインナップが更新されました` |
| `GACHA_TAG_WEEKLY` | `THIS WEEK` | `今週` |
| `GACHA_TAG_LIMITED` | `{0} LEFT` | `残り{0}回` |
| `SHOP_CARD_DISCOUNT` | `-{0}%` | `-{0}%` |
| `SHOP_CARD_LAST_CHANCE` | `LAST CHANCE` | `ラストチャンス` |

Zero new hardcoded `.text` literals (grep quoted in the report).

## 6. Tests (EditMode)

- `RotationCatalogTests`: overlay patch/append/drop; `Live` picks the window containing now
  and the earliest on overlap; `Next` picks the earliest future; parser round-trips the
  exporter's quoted form.
- `GeneralShopCatalogTests`: `rotationId` parsed; blank tolerated; admission unchanged
  (existing window tests still green).
- `LineupRollover` seam (pure): given (live, next, now) → `{none | rolled(id)}` fires once per
  boundary.
- `CountdownFormat` tests moved with the formatter; carousel tests untouched.
- Full unfiltered EditMode sweep green.

## 7. Acceptance

- [ ] With the published `wk_2026_38` live (or a short test rotation): STORE shows the header
      with its name and a ticking countdown; the 13 lineup cards carry `NEW`; the 5 permanent
      rows do not. Editor screenshot in `screenshots/`.
- [ ] Set a test rotation to end 2 minutes out; leave the STORE open; at the boundary the
      cards rebuild, the toast shows, the header switches to the next rotation (or hides). Log
      lines quoted.
- [ ] Publish a shop price change while the Editor runs → background/foreground or re-open
      the STORE → the card re-prices with no relaunch (the §3.4 allowlist works).
- [ ] No live rotation and none scheduled → header hidden; STORE identical to today (existing
      shop tests green, screenshot diff vs HEAD).
- [ ] GACHA weekly banner shows `THIS WEEK`; a capped banner adds `N LEFT` right-aligned inside the banner edge; other banners show neither.
- [ ] Per-element A/B crops vs `reference/figma_store_demo_14163-105654.png` and `figma_gacha_demo_14163-106259.png` for the header, the four badge placements and the price-box discount; ΔRGB table for the six badge fills.
- [ ] Strings: all 8 keys present EN + JA in `LocalizationText.csv`, importer PLAN verdict quoted, `--apply`, **`texts` published from the admin (version quoted)**, `--check` clean; zero `.text` literals; no retired keys.
- [ ] §2a: roll-over frame strip (header FadeSwap + staggered cards + toast); `_firstCardPaint` log line quoted; `UiMotion*`, `ScrollFeelTests`, `PressFeedbackCoverageTests` green and unchanged in count.
- [ ] EditMode sweep green.

## Files this task touches

**New:** `Assets/Scripts/UI/Shop/RotationModel.cs`, `Assets/Scripts/UI/Shop/LineupRollover.cs`,
`Assets/Scripts/UI/Common/CountdownFormat.cs` (+ tests for each).
**Modified:** `GeneralShopModel.cs`, `GeneralShopScreenController.cs`, `GeneralShopCard.cs`,
the shop builder (Editor) + `GeneralShopScreen.prefab` + the two card templates,
`GachaBannerModel.cs`, `GachaBannerCard.cs`, `GachaCarouselController.cs`,
`Assets/Scripts/ContentRuntime/ContentCatalogs.cs`, `ContentService.cs` (allowlist),
`Assets/Localization/LocalizationText.csv`, `Docs/AI_CONTEXT.md`.

## Out of scope (do NOT do these)

- A "coming next week" teaser grid (would need future rows rendered as non-buyable cards —
  design round first).
- Home-screen pill for the lineup (the Daily pill is the precedent; a second pill needs a decision).
- Push/local notification on rotation day.
- Any admin or backend change (all in `weekly_rotation_admin`).
- The POPULAR / OFFERS curation chips.
