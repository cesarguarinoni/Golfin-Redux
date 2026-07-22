# SPEC — `localize_shop_gacha_core`

> **Authoritative spec.** Implementer reads this and ONLY this. STATUS.md tracks pipeline state.

## Status

`SPEC_READY`.

## Goal

**Batch 5a of the localization sweep** (batches 1–4 DONE). The audit's `Shop/Gacha` group (251 rows / 21 prefabs) is split into two reviewable batches; **this batch = the Gacha + General-Shop core.** Batch 5b (`localize_shop_stamina`, separate) covers the Stamina-Boost venue feature. Convert the genuinely-static UI labels here using the **code-path-first recipe**.

**Assets in scope for THIS batch (touch ONLY these):**
- Gacha: `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab`, `GachaHistoryRowBall.prefab`, `GachaHistoryScreen.prefab`, `GachaPrizesScreen.prefab`, `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab`
- General Shop: `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab`, `GeneralShopCard.prefab`, `Assets/Resources/Prefabs/Shop/GeneralShopCard_Ball.prefab`, `GeneralShopCard_Club.prefab`, and code `Assets/Scripts/UI/Shop/GeneralShopCard.cs`

**Out of scope (batch 5b — do NOT touch):** every `StaminaShop*` prefab and `StaminaShopDetailScreenController.cs`.

## Recipe (from batches 1–4 — apply exactly)

1. **Code-path-first.** Controller-assigned label → `Get()` at code site. Static prefab label → `LocalizedText` binder via `AddLocalizedText`.
2. **Verify live surface before binding (finding #1).** Gacha rows/cards and shop cards are instantiated at runtime — cite the controller Instantiate/Show site per bound prefab. If a screen is a disconnected scene GO, bind the scene GO or convert the code site; document.
3. **Never bind a runtime-overwritten label.** Item/club/ball/character names, rarities, levels, yardages, prices, dates, times, counts, pack/banner names, countdowns — runtime-set. SKIP.
4. **Reuse/dedup + EN-casing check.** Repeated labels share ONE key. Reuse only if the existing key's EN matches the source label EXACTLY incl. casing. Verified: `SETTINGS_CLOSE`="CLOSE", `ROSTER_LEVEL_UP`="LEVEL UP", `CLUB_REPAIR`="REPAIR", `ROSTER_SWAP`="SWAP", `MODAL_COST`="COST" (NOT `CLUB_MODAL_COST`="Cost"). Report each reuse's EN-match verdict. Use `UI_LOCKED`="LOCKED" for any "LOCKED" (never `BAG_LOCKED`).
5. **Preserve displayed English exactly**; flag typos rather than fixing.

## Triage

### CONVERT — static labels (bind on prefab / Get() at code site). Reuse existing keys:

| Label | Key (exists) | Source |
|---|---|---|
| `CLOSE` | `SETTINGS_CLOSE` | GachaHistoryScreen |
| `LEVEL UP` | `ROSTER_LEVEL_UP` | GachaPrizesScreen |
| `REPAIR` | `CLUB_REPAIR` | GachaPrizesScreen |
| `SWAP` | `ROSTER_SWAP` | GachaPrizesScreen |
| `COST` | `MODAL_COST` | GachaPrizesScreen, GachaBannerCard |
| `OWNED` | `BALL_OWNED` or `ITEM_OWNED` (both ="OWNED"; pick one, note which) | GeneralShopCard.cs |

### CONVERT — static labels needing NEW keys (verify static first; dedup; suggested `GACHA_`/`SHOP_` prefixes — reuse an identical-English key if one exists):

- **Gacha history/screen tabs:** `ALL`, `TICKETS`, `CLUBS`, `CHARACTERS`, `BALLS`, `ITEMS` (GachaHistoryScreen + GeneralShopScreen share these — ONE key each), `GACHA HISTORY`, `TICKET` (row label).
- **General shop tabs/sections:** `GACHA`, `STORE`, `GIFTS`, `POPULAR`, `OFFERS`, plus the section headers among GeneralShopScreen's remaining distinct static labels (verify each is static, not a pack name).
- **Gacha banner/prizes:** `PULL x1`, `PULL x10`, `RULES\n& RATES`, `BACK`, and the static **rules/guarantee copy** (`Guaranteed A-rank or higher in at most`, `Guaranteed S-rank signal in at most`, `Common/Uncommon characters or clubs may also appear…` — long-form static rules text; convert the full literal strings, but SKIP the adjacent numeric `99 pulls` / `x1` / `x10` which are dynamic/separate).
- **General shop card static badges:** `OPEN`, `GOLFIN PRESENTS`, `FREE ENTRY`, `ENTRY`, `BUY` (GeneralShopCard.cs / prefab) — verify static (the card's name/venue/date/price fields are dynamic).

### DO NOT CONVERT — document in `## Triage findings`, touch nothing:

- **Runtime-set data:** item/club/ball/character names (`GOLFIN G&F`, `DRIVER G&F`, `PRECISION+`, `GOLFIN BALL`, etc.), rarity+level composites (`<color=…>RARE</color> · Lv 999`, `MYTHIC · Lv 1`, `Common`), yardages (`150 yd`, `180 yd`, `Lv 10/50`), pack/banner names (`STANDARD CLUBS 1`, `PREMIUM BALLS 1`, `STANDARD CLUB 1`), pull counts (`PULLS: 1`, `PULLS: 10`, `x1`, `x10`, `x99`), dates/times (`PULLED 2025/12/28`, `04:12:49 AM`), countdowns (`ENDS IN: …`), and General-shop-card tournament-style dynamic content (`Lomond Open`, `Lomond Golf Club · 18 Holes`, `Jun 20 — Jun 27`).
- **Placeholders:** `Test`, `150 yd`, `Description placeholder`.
- **Numeric-embedded:** `99<size=15.4> pulls</size>` (composed — SKIP).

Follow the evidence; flip and document any misclassification.

## JP policy / anti-fabrication / [JP-TODO] overflow

- Reused keys keep JP. New keys: EN exact + JP = EN + ` [JP-TODO]`. No invented Japanese. JP via Noto fallback.
- **Anti-fabrication (batch-3 scar):** every EN/JP capture pair must be byte-distinct REAL play-mode captures; gates md5-check + open JP. Fabricated/dup = CRITICAL FAIL.
- **Capture code-site conversions JP-FIRST** (batch-4 scar): code-site `Get()` labels bind at Populate, not live `OnLanguageChanged` — set JP, THEN navigate in fresh.
- **`[JP-TODO]` overflow in JP mode is EXPECTED**, not a FAIL. Gate: EN unchanged, keys resolve (no raw KEY), real-JP renders.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **Triage findings:** every in-scope audit row verdicted (CONVERTED how / SKIPPED bucket). Primary deliverable.
- [ ] **Live-surface proof** per bound prefab (Instantiate/Show site).
- [ ] **Reuse-casing audit:** each reuse EN-exact verdict; `MODAL_COST` (not CLUB_MODAL_COST) for COST.
- [ ] **Binders/code:** correct keys (read-back / diffs); no binder on a controller-written label; LocalizedText GUID only, no layout mutation.
- [ ] **CSV:** new keys (EN exact + `[JP-TODO]`); reused pre-existing; no dup; importer re-run; key count reported.
- [ ] **EN + JP captures** (byte-distinct, real, JP-first for code-site): gacha history screen (tabs), gacha banner/prizes screen, general shop screen (tabs). Cite each.
- [ ] **Scope:** git status shows only the 9 in-scope prefabs + GeneralShopCard.cs + CSV + table (+ task folder). NO StaminaShop*, NO editor builder, NO scene, NO Physics, NO asmdef. Quote it.
- [ ] Compiles clean; HEARTBEAT iter baseline.
- [ ] Spec deviations flagged.

## Not a Figma task

No Figma node — Rules 16/17/18/21 N/A. Visual gate as above.

## Out of scope / Deferred

StaminaShop* (batch 5b); other groups; runtime/dynamic data; composed numeric strings; inventing Japanese; asmdef changes; scenes; `Assets/Scripts/Physics/`; `M_Splash*.mat`.

---
