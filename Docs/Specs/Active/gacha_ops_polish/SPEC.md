# SPEC — `gacha_ops_polish`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-31 (Architect via Cowork). Spec **D**, the last of `Docs/GACHA_ADMIN_PLAN.md`
> §8. **Needs C (`gacha_client_real_pull`) DONE** and, for §4, the archive that carries C. Four
> small, independent pieces; each is its own commit and can be dropped without hurting the others.
>
> Standing rules: player strings via the importer (EN + JA); dashboard strings via `DICT`;
> PIPELINE_HARDENING §23 for the dashboard piece; no device pass by default.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Close the loop around a working gacha: **odds shown in-app** (the honest-disclosure surface,
built from the same published rows the server rolls from), **a telemetry funnel** so we can see
whether anyone pulls, **a Gold ticket that renders**, and **the RP → ticket shop row** the
`GACHA_BUY` text has been waiting for since Stage 1.

## 1. What is true today (after A–C)

| Piece | State |
|---|---|
| RULES & RATES button | `GachaBannerCard.OnRules` → `Application.OpenURL(rulesUrl)`; hidden when blank (C). `rulesUrl` values are `golfin.example.com` placeholders |
| Overlaid data on the client | `GachaRatesCatalog`, `GachaPoolCatalog`, `TicketTypeCatalog`, banner `PityThreshold / PityMinRarity / GuaranteeMinRarityX10 / FeaturedRefIds` (C §2) |
| Telemetry | `TelemetryService.Instance.RecordSafe(name, () => payload)` (`Assets/Scripts/Telemetry/TelemetryService.cs:142`); hooks in `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs`; dashboard `app/(panels)/telemetry/telemetry-panel.tsx` with `Card` KPIs and the Flick-timing card (`shot_timing_telemetry`) as the precedent for an event-specific card |
| Ticket types | `ticket_types` rows `0 standard`, `1 gold`; `iconSprite` / `iconUrl` blank; the card and the counter fall back to the authored Standard icon (C §3) |
| Ticket shop | server + validator support for `shop_catalog.category = ticket` (B §5.2); `TICKET_SHOP_BUILD = 0` in `lib/buildGates.ts` blocks publishing; `GeneralShopCard.BindTicket` exists (C §4.3) |
| Text modal precedent | `TournamentSignupModal.prefab` rules block (Figma `13892:3254`) — title + scrolling body + CLOSE on a `ModalController` |

## 2. In-app RATES modal

`GachaRatesModalController : ModalController`, `Assets/Scripts/UI/Gacha/`, prefab
`Assets/Prefabs/UI/Modals/GachaRatesModal.prefab`, one scene instance beside the other modals,
static `Instance`. **Build it as a clone of the signup modal's rules shell** (title, scroll body,
CLOSE, same scrim/fade) — no new visual language; screenshot for Cesar, who may hand over a
Figma later.

- `GachaBannerCard.OnRules` → `GachaRatesModalController.Instance.Show(entry)`. The button is
  always visible again. If `rulesUrl` is non-blank the modal shows a **Full rules** link row at
  the bottom (`Application.OpenURL`, gated by `BannerPolicy.IsLinkAllowed` — the banners'
  allowlist, same reasoning: a free-text URL column opened unattended).
- Body, generated from the overlaid catalogs at show time (so a publish changes it at the next
  open, no build):
  1. **Featured** — the banner's `FeaturedRefIds` resolved to name + rarity chip (skip
     unresolvable ids silently; log once).
  2. **Rates by rarity** — one line per tier with `rateBp > 0`, highest first:
     `Legendary  2.00%` (`RarityHelper.GetRarityColor` tint on the name; format `bp / 100`
     with two decimals, `CultureInfo.InvariantCulture`).
  3. **Per-item odds** under each tier — `name  0.67%` = `rateBp/10000 × weight / Σweight(tier)`
     (the spec-A `effectiveOdds` formula; the same number the admin panel shows).
  4. **Guarantees** — `GACHA_RATES_PITY` "Guaranteed {0} or higher within {1} pulls" when
     `PityThreshold > 0`; `GACHA_RATES_GUARANTEE_X10` "Every 10-pull includes at least one {0}"
     when set; `GACHA_RATES_DUPE` "Duplicate clubs and characters are converted to Reward
     Points" when any pool entry has `dupeRp > 0`.
  5. `GACHA_RATES_FOOTER` "Rates apply to every pull on this banner."
- Strings: `GACHA_RATES_TITLE` "RATES & RULES", `GACHA_RATES_FEATURED` "FEATURED",
  `GACHA_RATES_FULL_RULES` "Full rules", plus the four above — EN + JA via the importer. The
  old `GACHA_PITY_A_RANK` / `GACHA_PITY_S_RANK` keys are left untouched (unused; deleting keys is
  a separate cleanup).
- Pure seam `GachaRatesText.Build(entry, rates, pool, resolver)` → the lines, EditMode-tested
  (formatting, ordering, the pity/guarantee/dupe conditionals, unresolvable featured id skipped).
- Telemetry: `gacha_rules_open` (§3).

## 3. Telemetry funnel

Five events through `RecordSafe`, all carrying `banner_id`:

| Event | Where | Payload |
|---|---|---|
| `gacha_banner_view` | `GachaCarouselController` when a card becomes the centred one (once per banner per Rewards Center open) | `position`, `live_count` |
| `gacha_pull_tap` | `GachaPullFlow.Pull` entry | `count`, `cost`, `ticket_type`, `balance_before` |
| `gacha_pull_result` | `GachaPullService` outcome | `count`, `status` (ok / insufficient / cost_changed / pull_cap / paused / unavailable / not_available), and on ok: `rarities` (six ints), `dupes`, `pity_forced`, `guarantee_forced`, `latency_ms` |
| `gacha_reveal_skip` | `GachaRevealModalController.OnSkip` | `count`, `cards_shown` |
| `gacha_rules_open` | §2 | — |

Dashboard: a **Gacha funnel** card on the Telemetry panel beside Flick timing — views → taps →
ok results (counts and conversion %), skip rate, insufficient rate, mean latency; 7 d window
with the panel's existing range control. Pure aggregation in `lib/telemetryGacha.ts` with
vitest. `npm run deploy` + §23 proofs.

The server-side pull log (B) remains the source of truth for *what was won*; telemetry is the
*behaviour* view (taps that never became pulls, skips). Do not duplicate prize detail into
telemetry beyond the six-int rarity histogram.

## 4. Gold ticket

- **Bundled placeholder icon**: `Assets/Resources/Art/Tickets/Ticket_Gold.png`, derived from the
  Standard ticket sprite the top bar uses (`PersistentUIManager` `TicketIcon` — read the sprite
  reference off the prefab) by an Editor script that re-tints it gold (`#E5B84A` multiply, alpha
  preserved) and writes the PNG at the same pixel size; import as Sprite (2D and UI), mipmaps
  off. Also copy the Standard one to `Assets/Resources/Art/Tickets/Ticket_Standard.png` so both
  kinds resolve by the same path rule. `ticket_types.csv`: `iconSprite` = `Ticket_Standard` /
  `Ticket_Gold` → **through the importer** (a CSV edit is a proposal: plan → `--apply` → publish
  `ticket_types` → `--check` clean). Cesar replaces the placeholder via the admin upload
  (`iconUrl`) whenever real art exists — never blocks on it (Cesar's standing rule on
  placeholders).
- Admin: `contentArtMutations.ts` `ALLOWED_CATALOGS += "ticket_types"`, `ALLOWED_COLUMNS +=
  "iconUrl"`; the Ticket Types panel gets the upload control; target size = the Standard icon's
  measured pixels (quote them).
- Client: `TicketTypeCatalog` icon ladder (C §3) now resolves Gold; `GeneralShopCard.BindTicket`
  and the reveal show it. The top-bar counter stays Standard-only (no design for a second
  counter — out of scope).

## 4b. `simulate()` guarantee parity (two lines, from the B review)

`lib/gachaOdds.ts` `simulate()` decides the x10 guarantee from the first NINE slots and forces
slot 9; the server (B §3, the spec text) rolls slot 9 normally and re-rolls it only if all TEN
missed. Prize distribution is identical; the **flag rate** is not (≈13.4 % vs ≈10.7 %), so the
admin's "guarantee hits" disagrees with what the server logs. Make `simulate()` follow §3
literally: roll all ten, then if `blockBest < guarantee` re-roll slot 9 from the `≥ guarantee`
subset and set the flag. Update the affected vitest case; the parity note in B's report becomes
moot.

## 4c. Foreground content refresh — the other half of 5b (from the C review)

`ContentService` fetches exactly once, in `Awake` (`RefreshRoutine`, `:152`). C's
`TryReinstallFromCache` + `OnCacheRefreshed` subscription therefore fires only for a publish that
landed BEFORE the launch; a publish while the app is foregrounded waits for the next launch.
Add `public void RefreshNow()` on `ContentService`: guarded by a `ScheduleRefreshThrottle(60.0)`
(the tournament/banner cooldown class, reused verbatim), runs the same `RefreshRoutine`, off the
critical path, no-op while one is in flight. Call it from `GachaCarouselController.OnEnable`
and from `OnApplicationFocus(true)` on the `ContentService` GameObject. Nothing else changes:
the refresh writes caches for every catalog exactly as the boot one does, and only the four
gacha catalogs re-install live (`TryReinstallFromCache`'s allowlist) — every other catalog still
applies at next launch (I5). Test: a second `RefreshNow()` inside the cooldown is a no-op; the
gacha reinstall fires after a foreground refresh that wrote a newer cache. Live check: publish a
`costX1` change while the Editor is running → background/foreground or re-open the Rewards
Center → the card re-prices with no relaunch.

## 4d. Dashboard copy (from the C review)

`lib/i18n.ts:1460` (Gacha Banners panel banner) still says pulls run on the client-side mock.
Replace with (en + ja): *"Publishing makes a banner the next build's bundled floor and the
overlay for installed builds. The server rolls every pull from these rows — a change here is
live on the next pull."* One deploy with the rest of D.

## 5. `TICKET_SHOP_BUILD` + the first ticket shop row

Only after the archive that carries C is uploaded (`Docs/Versioning/last_uploaded_build.txt`):

1. `lib/buildGates.ts` `TICKET_SHOP_BUILD = <that number>` — read from the file, never inferred
   (the `SHOP_CATEGORY_STRICT_BUILD` lesson). One-line commit, `npm run deploy`, proof quoted.
2. From the admin, `+ New row` on Shop: `category = ticket`, `refId = 0` (Standard),
   `quantity = <Cesar>`, `rpCost = <Cesar>`, `minBuild = TICKET_SHOP_BUILD` → publish →
   `export_content.py` → commit the CSV. **Cesar sets quantity and price**; the spec does not.
   Suggested anchor for the conversation only: ECONOMY_MASTER §3 has no ticket line yet — add
   one when the price is chosen (`Docs/Economy/ECONOMY_MASTER.md`, Cowork task).
3. Live check: buy it from the shop in the Editor against prod → ledger row `shop:<entryId>`,
   counter updates, no pending grant row (B §5.2); the card renders via `BindTicket`.

## 6. Sequencing

§2 → §3 → §4 (Unity commits, EditMode sweep after each) → dashboard pieces of §3/§4 in one
deploy → §5 when the archive exists (may be days later; STATUS stays `IMPLEMENTER_WORKING`
with the note "waiting on the C archive" rather than closing early).

## 7. Acceptance

- [ ] RATES modal opens from every live banner; lines match `effectiveOdds` in the admin for
      the same pool to the second decimal (screenshot both); publish a rate change → reopen →
      new numbers, no build; Full rules row only when `rulesUrl` is set and allowlisted.
- [ ] `GachaRatesText.Build` tests green; strings via importer, `--check` clean, zero `.text`
      literals.
- [ ] Five events arrive in `telemetry_events` for one Editor session (SQL pasted); the funnel
      card shows them; vitest green; deployment id + stamp; Access 302.
- [ ] Gold ticket renders on a ticket prize card and in the RATES featured list; the placeholder
      PNGs are committed; `ticket_types` published with the sprite names; admin upload of an
      `iconUrl` replaces it at next launch.
- [ ] §5: `TICKET_SHOP_BUILD` set from the file; first ticket row published, exported,
      committed; the live purchase verified by SQL.
- [ ] Full EditMode sweep green; spec deviations flagged with justification.

## Files this task touches

**New** — `Assets/Scripts/UI/Gacha/{GachaRatesModalController,GachaRatesText}.cs` (+ tests),
`Assets/Prefabs/UI/Modals/GachaRatesModal.prefab`, `Assets/Resources/Art/Tickets/Ticket_{Standard,Gold}.png`,
`Assets/Scripts/UI/Editor/TicketIconDerive.cs`, `Tools/admin-dashboard/lib/telemetryGacha.ts` (+ tests).

**Modified** — `GachaBannerCard.cs`, `GachaCarouselController.cs`, `GachaPullFlow.cs`,
`GachaPullService.cs`, `GachaRevealModalController.cs`, `ShellScene.unity` (modal instance),
`Assets/Resources/Data/ticket_types.csv`, `LocalizationText.csv`,
`Tools/admin-dashboard/lib/{contentArtMutations,buildGates}.ts`,
`app/(panels)/telemetry/telemetry-panel.tsx`, `app/(panels)/ticket-types/*`,
`Assets/Resources/Data/shop_catalog.csv` (after §5 export), `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`.

## Out of scope (do NOT do these)

- A second top-bar counter for Gold tickets; any Figma-level redesign of the card, modal or
  prize cards.
- Deleting the unused `GACHA_PITY_*` / `GACHA_BUY` keys.
- Mission / tournament ticket grants (own quick task, after this).
- Ticket price / quantity decisions (Cesar's, §5.2).
