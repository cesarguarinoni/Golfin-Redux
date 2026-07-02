# SPEC — stamina_boost_shop (Order 517)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.
> **Tier 3** — two new ScreenManager screens, new CSV data model, new stamina top-up API,
> new nav entry off the roster Boost button.

## Status

`SPEC_READY` — see `STATUS.md`.

## Goal

Greenfield **Shop pillar**, first shop. Build a two-screen `ScreenManager` flow — a
**Shop Selection** list and a **Shop Detail** menu — launched from the (currently inactive)
**Boost button** in the Character Roster and its Compare modal. The player spends **RP**
(`RewardPointsManager.SpendPoints`) on **instant stamina refills**: each shop sells three
tiers (HIGH / MEDIUM / LIGHT BOOST), each a `+X STA` top-up applied to the **selected
character's live Condition pool** (never the tournament pool). First region shipped is
**MIE / Kameyama** with 10 real-restaurant storefronts. This adds the one missing engine
piece — a **clamp-to-max stamina top-up API** on `StaminaRuntimeService`. Daily-Bonus stat
buffs, GPS default-region, and non-MIE regions are **display-only / deferred** in v1.

## Reuse mandate — duplicate-and-modify, NEVER rebuild (Cesar directive 2026-07-02)

Clone the existing **Tournament Selection** screen as the base for both shop screens. Do NOT author
new screen scaffolding from scratch.

- **Selection screen** = duplicate `Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab`
  + `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs`, then re-skin/retarget to shops.
- **Shop card** = duplicate `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab`
  + `Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs`, then re-skin to the Figma card.
- **Detail screen** = same screen scaffold, re-skinned; the menu-row is a NEW prefab built to the same
  standalone-prefab pattern as the card.
- **ScreenManager registration:** add `StaminaShopSelection` + `StaminaShopDetail` to `ScreenId` and the
  show/activate switch EXACTLY like `TournamentSelection` (`Assets/Scripts/UI/ScreenManager.cs` — `:22`
  enum, `:49` serialized ref, `:163` activate).

### Build order — prefabs BEFORE screens (Cesar directive)

When the visual-design pass runs, build the **shop card prefab** and the **menu-row prefab** FIRST, as
standalone Inspector-editable prefabs — clean serialized fields, no hard-coded layout values — so Cesar
can tweak them directly, and confirm each in isolation. THEN assemble the full screens from those prefabs.
Do not bake card/row layout inline into the screen; the prefab is the editable unit.

## Locked design decisions (resolved with Cesar 2026-07-02 — do NOT reopen)

- **D1 sold:** instant stamina refill, 3 SKUs/shop (HIGH/MEDIUM/LIGHT). No timed buffs, no pool-size increase.
- **D2 pool:** LIVE Condition pool ONLY. The tournament pool is deliberately non-regenerating; never top it up.
- **D3 currency:** RP only, via `RewardPointsManager.SpendPoints(int)`.
- **D4a entry:** roster Boost button (`CharacterDetailPanel` + `CompareController`), both currently inactive.
- **D4b surface:** STANDALONE stamina-shop screens (NOT the first module of the general Order 610 Shop). Extract the purchase transaction into a small reusable seam (`ShopTransaction`) so 610 reuses the guts, not the screen.
- **D5 apply:** per-character, targets the **selected character** the Boost button was opened from. Design UNCHANGED — no character indicator added to the shop UI (Cesar may add later; not now).
- **D6 pricing:** flat RP per SKU, CSV-driven. `baseRate = 5` (see Economy note). Numbers frozen in the seed CSV.
- **D7 daily bonus:** DISPLAY-ONLY label in v1 (render per Figma, zero mechanical effect). Mechanic deferred.
- **D8 region/GPS:** MIE only populated; non-MIE prefectures render locked/empty; GPS deferred, default hardcoded to MIE.
- **D9 map + featured:** "View on Maps" = `Application.OpenURL`; `featured` bool (kageroh only). Both CSV-driven, both in v1.

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd`
- **Selection frame:** `v4.2 Shop Selection (filters outside panel)` — node `13156:1178`
  → `reference/frame_selection_13156.png`
- **Detail frame:** `v4.4 Shop Detail — Bar&Lounge 影牢 - 3 Options` — node `13330:1139`
  → `reference/frame_detail_13330.png`
- **Lesson AK:** Implementer + reviewers MUST re-pull `get_design_context` on BOTH node ids
  at build time for exact tokens/positions. Prose below under-specifies pixels by design.
- **Placeholder vs canonical:**
  - CANONICAL (locked): shop names, categories, taglines, hours, daily-bonus stat+%, signature names,
    the 3 tier labels, and every `+STA` value. These match the Figma / were signed off by Cesar.
  - AUTHORED-DUMMY (in the seed CSVs, tune freely): all `rp_cost` values, `walk_minutes`, `map_url`,
    and addresses except kageroh (Figma) and kamehachi (real). Storefront/hero/menu images are
    placeholder art (Nishikawa dependency) — wire against logical keys in the CSV.
  - The top-bar `50000` RP counter is placeholder; bind to the real RP balance.
- **Seed data (authored this spec — copy verbatim into `Assets/Resources/Data/`):**
  `reference/stamina_shops.csv` (10 shop rows) and `reference/stamina_shop_items.csv` (30 item rows).

## Economy note (for future tuning — not a build step)

RP was derived as `RP = STA × 5 × tierMult × shopMult`, rounded to 5, where
tierMult = {HIGH 0.85, MEDIUM 1.0, LIGHT 1.15} (bulk discount) and
shopMult = {hearty 0.9, standard 1.0, cafe 1.1}. Anchors: practice entry 100 RP, 1v1 win 200 RP,
club upgrades 5–120 RP; drain 8 Condition/hole, tank_base 60, regen 12/hour. The CSV stores the
**final RP integers** — the formula is documentation for re-tuning, not a runtime input.

## Figma Fidelity (enumerate EVERY element — Rule 18)

Implementer + both reviewers reproduce these two tables with PASS/FAIL against the node renders.

### Selection screen (`13156:1178`)

| Element | Figma node | Property → value |
|---|---|---|
| Screen title | `13156` Top UI | "BOOST STAMINA", Rubik SemiBold 51px, white, centered |
| Region filter pill | `13156:1182` | 8 segments HOKKAIDO…KYUSHU; KANSAI active. v1: only KANSAI selectable; others render disabled |
| Prefecture filter pill | `13156:1206` | ALL/MIE/SHIGA/KYOTO/OSAKA/HYOGO/NARA/WAKAYAMA; MIE active. v1: only MIE has data; others disabled/empty |
| Cards container | `13156:1231` | vertical list, 10 cards, 360px tall each, 24px gap, scrollable; right scrollbar `13156:1463` |
| Shop card (×10) | e.g. `13156:1232` | 978×360, radius per node, gradient `#133453→#091B33`, **2px border rgba(62,124,168,·)**, inner border `#0A1D35` |
| — storefront image | `13156:1234` | 260×280 at (30,40), radius, 1.5px border |
| — category•location | `13156:1235` | "COCKTAIL BAR • KAMEYAMA, MIE", Rubik, gradient-white; composed `category • CITY, PREF` |
| — shop name | `13156:1236` | Noto Sans JP Bold 50px, white (handles mixed JP/Latin) |
| — tagline | `13156:1237` | Rubik 26px, `#C7D6EB` |
| — hours + View on Maps | `13156:1238` | "18:00 – 02:00 — 📍 View on Maps"; Maps text is the tappable link (D9) |
| — daily bonus chip | `13156:1243` | gold pill, "Daily Bonus +15% Recovery", `#FAC74D`; **display-only (D7)** |
| — STA range | `13156:1247` | "+20 / +60 STA", green `#73E080`; = min/max item STA (derived, not authored) |
| — RP range chip | `13156:1248` | "R 115~255"; = min/max item rp_cost (derived); RP icon + amount |
| — chevron | `13156:1252` | "›" right, tap target → Detail |
| — FEATURED tag | `13156:1253` | "★ FEATURED" gold, ONLY when `featured=true` (kageroh) |
| Bottom nav bar | `13156:1462` | standard NavBarContainer instance (reuse existing) |

### Detail screen (`13330:1139`)

| Element | Figma node | Property → value |
|---|---|---|
| Screen title | `13330:1141` | shop name, Rubik SemiBold 51px |
| Hero card | `13330:1142` | 1074×420 at top 325, radius 50, **3px border `#3E7CA8`**, hero image + bottom gradient `→rgba(5,10,26,0.92)` |
| — Open Now badge | `13330:1145` | green pill rgba(26,128,51,0.95), border `#73E080`, "OPEN NOW" (v1 static/derive from hours) |
| — FEATURED badge | `13330:1148` | gold `#FAC74D`, "★ FEATURED", only if featured |
| — category line | `13330:1150` | "COCKTAILS • JAPANESE WHISKY • LATE NIGHT", gradient white 22px |
| — name | `13330:1151` | Noto Sans JP Bold 58px white |
| — address | `13330:1152` | "📍 <address>", Rubik Medium 22px `rgba(217,229,255,0.95)` |
| Info card (3 col) | `13330:1153` | 1074×200, radius 50, **3px border `#3E7CA8`**, gradient `#133453→#091B33` |
| — LOCATION col | `13330:1156` | "LOCATION" / city (30px bold) / "📍 <n> min walk" (`#8CD1FF` underline) |
| — HOURS col | `13330:1161` | "HOURS" / "18:00 – 02:00" / hours_note |
| — SIGNATURE col | `13330:1166` | "SIGNATURE" / signature_name (Noto Sans JP) / "House special" |
| Menu panel | `13330:1170` | 1074 wide, radius 40, **2px border rgba(62,124,168,0.5)**, gradient, inset shadow, pad 24, gap 24 |
| — MENU header + bonus | `13330:1171` | "MENU" 40px bold; gold "DAILY BONUS +15% RECOVERY" chip (display-only, D7) |
| — menu item row (×3) | `13330:1220/1178/1283` | 994×160, radius 32, **2px border rgba(62,124,168,0.85)**, inner border `#0A1D35` |
| —— item image | `13330:1222` | 124×124 at (18,16), radius 22, 1.5px border |
| —— tier badge | `13330:1392/1401/1287` | HIGH = gold (bg rgba(250,199,77,0.2)/border `#FAC74D`/gold-gradient text); MEDIUM = silver (border `#D9DBEB`/white-gradient text); LIGHT = muted (border `#8C9EBF`/text `#C7D6EB`); 16px |
| —— item name | `13330:1226` | Noto Sans JP Bold 28px white |
| —— descriptor | `13330:1227` | Rubik Regular 18px `#C7D6EB` |
| —— +STA | `13330:1230` | green `#73E080` 22px, e.g. "+60 STA" |
| —— RP container | `13330:1232` | 215×56, bg `#001E39`, radius 43, RP icon + amount 33.6px |
| —— BUY button | `13330:1237` | 215×56, radius 20, gold gradient (rgb 252,241,149→214,171,66→187,127,29), border `#FFE48B`, text `#321506` 39px; bottom border `#422100` |
| Bottom button | `13330:1305` | **"CANCEL"** (NOT generic secondary) — 360×120, silver gradient (#FFF→#D1D5DB→#818EA1), border `#F7F8F9`, text `#1E293B` 66px → returns to Selection |
| Bottom nav bar | `13330:1310` | standard NavBarContainer instance |

## Architecture context

- **Asmdef boundaries:** new shop UI lives under `Assets/Scripts/UI/Shop/` (new folder; add asmdef
  referencing the Roster/Core asmdefs it depends on — mirror how `Assets/Scripts/UI/Roster` is set up).
  Stamina API change is in the existing `StaminaRuntimeService` asmdef.
- **Existing code referenced (read before building):**
  - `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — `boostButton` (`:88`), listener wired
    (`:137`), `OnBoostClicked()` (`:618`, currently only `Debug.Log`), has `currentCharacterId` in scope.
  - `Assets/Scripts/UI/Roster/UI/CompareController.cs` — `compareBoostButton` (`:56`), listener (`:120`,
    currently only `Debug.Log`). Wire to open the shop for that column's character.
  - `Assets/Scripts/StaminaRuntimeService.cs` — owns all stamina mutation; drain (`:102`) and the
    **clamp-to-max regen pattern (`:138`)** to mirror for the new API. Persists + refreshes the roster meter.
  - `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` — `currentStaminaEnergy` (`:65`),
    `maxStaminaEnergy` (`:68`).
  - `Assets/Scripts/LiveStatProviderHost.cs` — reads live energy (`:180`,`:214`); the roster meter
    refresh path the new API must trigger.
  - `RewardPointsManager.SpendPoints(int) → bool` (`Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs:82`)
    — returns false on insufficient funds; must NOT touch lifetime-earned.
  - `ToastController` — reuse for insufficient-RP and success feedback (per `mode_select_system` convention).
  - Screen pattern: **CLONE Tournament Selection** (see Reuse mandate) —
    `TournamentSelectionScreen.prefab` / `TournamentSelectionScreenController.cs` /
    `TournamentSelectionCard.prefab` / `TournamentSelectionCard.cs`; register in `ScreenManager`
    exactly like `ScreenId.TournamentSelection`.
- **Manager APIs used:** `RewardPointsManager.SpendPoints`; `ToastController` toast; `Application.OpenURL` (map).
- **New code (this task):**
  - `StaminaRuntimeService.AddEnergy(PlayerCharacterData pcd, float amount) → float` — clamp-to-max
    top-up (mirror `:138`), persist, fire the same meter-refresh the regen path fires. Returns actual
    energy added (post-clamp). LIVE pool only; never the tournament pool.
  - `ShopTransaction.TryPurchase(int rpCost, System.Action onGranted) → bool` — pre-check + `SpendPoints`;
    on true invoke `onGranted` + success toast; on false → insufficient-RP toast. The reusable seam for Order 610.

## Implementation

Build in this order. Plan Mode first; verify each step before the next.

**1. Data layer.**
- Copy `reference/stamina_shops.csv` + `reference/stamina_shop_items.csv` into `Assets/Resources/Data/`.
- Models: `ShopModel` (all shop columns; `Items` list) and `ShopItemModel` (tier enum HIGH/MEDIUM/LIGHT,
  name, desc, stamina:int, rpCost:int, imgKey). Loader `ShopCatalog` reads both CSVs via the existing
  Resources CSV-parse pattern (same as `LevelUpCosts`/`modes`), joins items→shop on `shop_id`, orders by
  `item_order`. Derived per shop: `StaMin/StaMax` = min/max item stamina; `RpMin/RpMax` = min/max rpCost
  (feed the card's "+20 / +60 STA" and "R 115~255" ranges — do NOT author these separately).
- CSV parser must handle quoted fields containing commas + em-dashes + Japanese (UTF-8). Confirm against
  the `cocoichi`/`kageroh` rows (they contain commas and `—`).

**2. Stamina top-up API (the one engine change).**
- Add to `StaminaRuntimeService`:
  `public float AddEnergy(PlayerCharacterData pcd, float amount)` →
  `float before = pcd.currentStaminaEnergy;`
  `pcd.currentStaminaEnergy = Mathf.Min(pcd.maxStaminaEnergy, pcd.currentStaminaEnergy + amount);`
  persist + fire the SAME refresh the regen path (`:138`) fires; `return pcd.currentStaminaEnergy - before;`
- LIVE pool only. Do NOT touch the tournament pool. Do NOT modify `maxStaminaEnergy`.
- Add an EditMode test mirroring `StaminaLiveWiringTests`: AddEnergy clamps at max, adds exact amount below
  max, persists, and never exceeds `maxStaminaEnergy`.

**3. Shop Selection screen (`StaminaShopSelectionScreen`).**
- CLONED from Tournament Selection (Reuse mandate). Build the **shop card prefab** first (clone of
  `TournamentSelectionCard`), confirm it in isolation, THEN populate the list. Carries the **target
  character id** on open (see step 6).
- Region/prefecture filter strips per `13156` — v1 wires only KANSAI/MIE as active; render the rest
  disabled (greyed, non-interactive). No GPS (D8): default filter = MIE, hardcoded.
- Instantiate one card per `ShopModel` (ordered by `order`) into the scroll list. Card binds:
  storefront img, `category • CITY, PREF`, name, tagline, hours line, Maps link, daily-bonus chip
  (display-only), derived STA range, derived RP range, chevron, FEATURED tag (only if `featured`).
- Card tap (chevron/body) → push `StaminaShopDetailScreen` with this `shopId` + the target character id.
- Maps link tap → `Application.OpenURL(shop.map_url)`.

**4. Shop Detail screen (`StaminaShopDetailScreen`).**
- Same cloned screen scaffold. Build the **menu-row prefab** first (standalone, Inspector-editable,
  same pattern as the card), confirm in isolation, THEN assemble the screen. Opened with `shopId` +
  target character id. Loads the `ShopModel`.
- Bind hero (name, category line, address, FEATURED, Open-Now), 3-column info card
  (LOCATION/HOURS/SIGNATURE), MENU header + daily-bonus chip (display-only).
- Render exactly 3 menu rows in `item_order` (HIGH→MEDIUM→LIGHT) with tier badge styling per the
  fidelity table, name, descriptor, `+STA`, RP amount, BUY button.
- CANCEL button → pop back to Selection.

**5. Purchase flow + edge cases.**
- BUY tap → `ShopTransaction.TryPurchase(item.rpCost, onGranted)`:
  - Pre-check `RewardPointsManager` balance ≥ rpCost. If not: BUY tap shows `ToastController`
    "Not enough RP" and the RP amount renders in red `#C04000` (mirror `mode_select_system` entry-fee
    block UX). No purchase.
  - On sufficient: `SpendPoints(rpCost)`; on true → `onGranted` = `StaminaRuntimeService.AddEnergy(
    targetCharacter, item.stamina)`; then success toast e.g. "+60 STA · <shop name>", refresh the top-bar
    RP counter, and refresh the roster stamina meter (via the AddEnergy refresh hook).
  - **Stamina-full guard:** if the target character's `currentStaminaEnergy >= maxStaminaEnergy`, disable
    all three BUY buttons and show a "Stamina full" state so the player can't waste RP. Re-enable when not full.
- Repeat purchases allowed (consumable); each Buy is an independent transaction, clamped to max.

**6. Entry wiring (the inactive Boost buttons).**
- `CharacterDetailPanel.OnBoostClicked()`: replace the `Debug.Log` with opening
  `StaminaShopSelectionScreen`, passing `currentCharacterId` as the target character.
- `CompareController` `compareBoostButton` listener (`:120`): same, passing that column's character id.
- Do NOT alter the roster/compare layouts or add any character indicator inside the shop (D5 — design unchanged).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each MUST be `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] Boost button in `CharacterDetailPanel` opens the Selection screen; Compare-modal Boost button opens it for that column's character (no more `Debug.Log` stubs).
- [ ] Selection screen lists all 10 MIE shops from CSV, ordered, with correct name/category/hours/tagline/daily-bonus/FEATURED (kageroh only).
- [ ] Each card's "+min / +max STA" and "R min~max" ranges are DERIVED from that shop's 3 items (not hardcoded).
- [ ] Region/prefecture strips render; only KANSAI/MIE active, others disabled; no GPS prompt.
- [ ] "View on Maps" opens the shop's map_url via the OS browser.
- [ ] Card tap opens the correct Detail screen; CANCEL returns to Selection.
- [ ] Detail shows hero, LOCATION/HOURS/SIGNATURE, daily-bonus chip (display-only), and exactly 3 tier-styled rows (HIGH gold / MEDIUM silver / LIGHT muted) with correct name/desc/+STA/RP.
- [ ] BUY with sufficient RP: SpendPoints deducts rpCost, AddEnergy adds exactly the item's STA (clamped to max), success toast fires, top-bar RP + roster meter refresh.
- [ ] BUY with insufficient RP: no deduction, red price + "Not enough RP" toast.
- [ ] AddEnergy never exceeds `maxStaminaEnergy` and never writes the tournament pool (EditMode test PASS).
- [ ] Stamina-full guard disables BUY when the target character is already at max.
- [ ] Figma Fidelity tables reproduced with PASS against both node renders (borders + tier-badge colors + CANCEL button explicitly checked).
- [ ] Screens are CLONED from Tournament Selection (not rebuilt); shop card + menu-row exist as standalone Inspector-editable prefabs and the screens are assembled from them.
- [ ] No white-box placeholders visible in the screenshots (missing art = documented logical key, not a broken sprite).
- [ ] All `[SerializeField]` references wired in the Inspector.
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Resources/Data/stamina_shops.csv` — NEW (from `reference/`).
- `Assets/Resources/Data/stamina_shop_items.csv` — NEW (from `reference/`).
- `Assets/Scripts/UI/Shop/` — NEW folder + asmdef: `ShopModel`, `ShopItemModel`, `ShopCatalog`,
  `StaminaShopSelectionScreen`(+view/card), `StaminaShopDetailScreen`(+view/menu-row), `ShopTransaction`.
- `Assets/Scripts/StaminaRuntimeService.cs` — ADD `AddEnergy(...)`.
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — `OnBoostClicked()` opens the shop.
- `Assets/Scripts/UI/Roster/UI/CompareController.cs` — `compareBoostButton` opens the shop.
- Scene/prefabs: two new screen prefabs registered with `ScreenManager`; two card/row prefabs.
- Tests: `Assets/Scripts/Gameplay/Tests/` — AddEnergy clamp/persist EditMode test.

## Smoke evidence

Human-in-the-loop play-and-confirm (Lesson O): load the roster, tap Boost on a character whose Condition
is below max, buy one item at each tier from two different shops, and confirm in `IMPLEMENTER_REPORT.md`:
the RP counter drops by exactly the price, the roster stamina meter rises by the item's STA (clamped),
the success toast names the shop, the insufficient-RP and stamina-full states both trigger, and CANCEL
returns to the list. Attach before/after screenshots of the roster meter + RP counter. Plus the AddEnergy
EditMode test (position/value assertion, not dispatch-only).

## Out of scope (do NOT do these)

- Daily-Bonus **mechanic** (any real stat buff, duration, stacking, daily reset) — v1 label is display-only.
- GPS / device-location default region.
- Any region or prefecture other than KANSAI/MIE; do not author non-MIE shop data.
- Real-money currency, item-currency, or granting non-stamina rewards.
- Tournament-pool top-ups.
- A character picker or any character indicator inside the shop UI (D5 — design unchanged).
- Building an Order 610 generic-shop shell/tab host (only extract the small `ShopTransaction` seam).
- Confirmation modal on purchase (Figma has none — toast only).

## Kickoff

```
Use the implementer subagent on "stamina_boost_shop"
```
