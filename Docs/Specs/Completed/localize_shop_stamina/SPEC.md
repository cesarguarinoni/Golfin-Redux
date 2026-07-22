# SPEC — `localize_shop_stamina`

> **Authoritative spec.** Implementer reads this and ONLY this. STATUS.md tracks pipeline state.

## Status

`SPEC_READY`.

## Goal

**Batch 5b of the localization sweep** (5a = Gacha + General-Shop core, DONE). Convert the genuinely-static **structural** UI labels in the **Stamina-Boost shop** feature. The audit's `Shop/Gacha` group is split; this batch = the `StaminaShop*` family only. Apply the **code-path-first recipe**.

**Assets in scope (touch ONLY these):**
- `Assets/Prefabs/UI/Shop/StaminaMenuRow.prefab`, `StaminaShopCancelButton.prefab`, `StaminaShopCard.prefab`, `StaminaShopDetailScreen.prefab`, `StaminaShopHeroCard.prefab`, `StaminaShopInfoCard.prefab`, `StaminaShopMenuPanel.prefab`, `StaminaShopPrefecturePill.prefab`, `StaminaShopRegionPill.prefab`, `StaminaShopSelectionScreen.prefab`
- `Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs`

**Out of scope:** all Gacha/General-Shop assets (batch 5a, DONE); every other group.

## ⚠️ Defining triage rule for THIS batch — venue content is DATA, not UI copy

The Stamina-Boost feature surfaces **real-world venue/bar data** (a sponsored-venue concept). This content is **runtime data** and MUST be SKIPPED — it is NOT localizable UI copy:
- **Venue names:** `Bar&Lounge 影牢`, `山崎 Whisky Flight`, `影牢 Cocktail`, any bar/menu-item name.
- **Descriptions:** `Late-night cocktails and Japanese whisky`, `3-glass tasting — Yamazaki, Hibiki, Hakushu…`, `COCKTAILS · JAPANESE WHISKY · LATE NIGHT`, `COCKTAIL BAR · KAMEYAMA, MIE`.
- **Addresses / locality:** `5-1 Higashimaru-chō, Kameyama, Mie 519-016…`, `Kameyama`, `12 min walk`.
- **Hours / prices / amounts:** `18:00 – 02:00`, `200~800`, `+40 STA`, `+20 / +60 STA`, `Daily Bonus +15% Recovery`.

These are populated per-venue at runtime (verify against the controller/data source). Treat them exactly like character/tournament names in earlier batches: SKIP, document.

## Recipe (from batches 1–5a — apply exactly)

1. **Code-path-first.** Controller-assigned label → `Get()` at code site. Static prefab label → `LocalizedText` binder via `AddLocalizedText`.
2. **Verify the live surface + instance-vs-source (findings #1 + 5a scar).** CRITICAL here: `StaminaShopRegionPill.prefab` and `StaminaShopPrefecturePill.prefab` each show ONE placeholder region/prefecture name, but are almost certainly **instantiated per region/prefecture at runtime** (the controller sets each pill's label). If so, the pill's label is **runtime-set → SKIP** (do NOT bind the pill prefab's label). Determine, per the controller, whether the region/prefecture filter buttons are (a) data-driven pill instances (SKIP the label) or (b) a fixed static bar of hardcoded buttons on `StaminaShopSelectionScreen` (CONVERT those). Cite the code. Do NOT bind a label a controller writes.
3. **Never bind a runtime-overwritten label** (venue data per rule above, region/prefecture pill labels if data-driven).
4. **Reuse/dedup + EN-casing.** Verified reuses: `MODAL_CANCEL`="CANCEL", `SETTINGS_CLOSE`="CLOSE", `NAV_BOOST_STAMINA`="BOOST STAMINA". Report each reuse's EN-exact verdict.
5. **Preserve displayed English exactly**; flag typos.

## Triage

### CONVERT — static STRUCTURAL labels (bind on prefab / Get() at code site). Reuse existing:

| Label | Key (exists) | Source |
|---|---|---|
| `BOOST STAMINA` | `NAV_BOOST_STAMINA` | StaminaShopSelectionScreen |
| `CANCEL` | `MODAL_CANCEL` | StaminaShopCancelButton, StaminaShopMenuPanel |

### CONVERT — static structural labels needing NEW keys (verify static, not venue data). Suggested `STAMINA_` prefix; dedup repeats:

Section/structural headers: `LOCATION`, `HOURS`, `SIGNATURE`, `MENU`, `FEATURED`, `OPEN NOW`, `MEDIUM BOOST` (tier label — verify static vs data), `View on Maps`, `Open daily`, `BUY`.

Region/prefecture filter labels **ONLY IF verification shows they are STATIC hardcoded buttons** (not data-driven pill instances): the region set `HOKKAIDO, TOHOKU, KANTO, CHUBU, KANSAI, CHUGOKU, SHIKOKU, KYUSHU` and prefecture set `ALL, MIE, SHIGA, KYOTO, OSAKA, HYOGO, NARA, WAKAYAMA`. **If they are data-driven pill instances, SKIP them all and document** (the pill-label localization would then be a code-site `Get()` on a region-name→key map, which is a DEFERRED structured-data task, not this batch). Decide from the controller; do not guess.

`DAILY BONUS +15% RECOVERY` / `Daily Bonus +15% Recovery`: the `+15% RECOVERY` is data-ish. If the whole string is one static authored label, convert the full literal; if the percentage is composed at runtime, SKIP. Verify.

### CONVERT — code string → `Get()`

- `StaminaShopDetailScreenController.cs` `"House special"` → `Get()` with a new key IF it's a static label; if it's a per-venue menu descriptor set from data, SKIP. Verify.

### DO NOT CONVERT — venue content DATA (per the ⚠️ rule above) + document in `## Triage findings`:

All venue names, descriptions, addresses, localities, hours values, prices, STA amounts, menu items, and any per-venue string. Plus counts/placeholders. And region/prefecture pill labels if data-driven (per rule 2).

Follow the evidence; flip and document.

## JP policy / anti-fabrication / capture-timing / overflow

- Reused keys keep JP. New keys: EN exact + JP = EN + ` [JP-TODO]`. No invented Japanese. JP via Noto fallback.
- **Anti-fabrication:** every EN/JP capture pair byte-distinct real play-mode captures; gates md5 + open JP. Fabricated/dup = CRITICAL FAIL. Keep the screenshots folder clean (no stale/duplicate files).
- **Capture code-site conversions JP-FIRST** (code-site Get() binds at Populate, not live OnLanguageChanged).
- **`[JP-TODO]` overflow in JP mode is EXPECTED**, not a FAIL. Gate: EN unchanged, keys resolve (no raw KEY), real-JP renders.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **Triage findings:** every in-scope Stamina row verdicted (CONVERTED how / SKIPPED-venue-data / SKIPPED-runtime-pill / DEFERRED). Primary deliverable — the venue-data-vs-structural-label split is the whole point.
- [ ] **Pill decision documented with code citation:** region/prefecture labels either CONVERTED (proven static) or SKIPPED (proven data-driven), with the controller line.
- [ ] **Live-surface proof** per bound prefab (Instantiate/Show site); no binder on a controller-written label; instance-vs-source checked (5a scar).
- [ ] **Reuse-casing audit:** MODAL_CANCEL/SETTINGS_CLOSE/NAV_BOOST_STAMINA EN-exact verdicts.
- [ ] **Binders/code:** correct keys (read-back/diffs); LocalizedText GUID only; no layout mutation.
- [ ] **CSV:** new `STAMINA_` keys (EN exact + `[JP-TODO]`); reused pre-existing; no dup; importer re-run; count reported.
- [ ] **EN + JP captures** (byte-distinct, real, JP-first for code-site): stamina selection screen (title + region/prefecture bar), a stamina venue card, the venue detail screen (LOCATION/HOURS/SIGNATURE structure), the menu panel. Cite each. Prove venue DATA stays as-is and only structural labels localize.
- [ ] **Scope:** git status shows only the 10 StaminaShop prefabs + StaminaShopDetailScreenController.cs + CSV + table (+ task folder). NO Gacha/GeneralShop, NO other group, NO scene/Physics/asmdef/editor-builder. Quote it.
- [ ] Compiles clean; HEARTBEAT iter baseline.
- [ ] Spec deviations flagged.

## Not a Figma task

No Figma node — Rules 16/17/18/21 N/A. Visual gate as above.

## Out of scope / Deferred

Venue content data; region/prefecture pill labels if data-driven (deferred structured-data pass); other groups; inventing Japanese; asmdef changes; scenes; `Assets/Scripts/Physics/`; `M_Splash*.mat`.

---
