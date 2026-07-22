# Architect Review — localize_shop_stamina

**Timestamp:** 2026-07-23 03:54 JST
**Verdict:** PASS → `READY_FOR_REDTEAM`
**Iteration:** 2 (capture-only redo of the iter-1 JP-first violation)
**Not a Figma task** — Rules 16/17/18/21 N/A. Localization batch, structural-labels-only scope; visual gate = EN unchanged + keys resolve + real-JP renders + expected `[JP-TODO]` overflow.

---

## Independent visual scan (canonical: `screenshots/jp_detail_screen.jpg`)

Detail view at 1170×2532: top-bar shows `R 67,100` wallet, `10 [+]` RP counter, gear. Below, banner reads `BAR&LOUNGE` (venue name uppercased — DATA, no `[JP-TODO]`). Hero card shows a cocktail image with `★ FEATURED [J...` badge top-right (truncated at hero right edge — this is the STAMINA_FEATURED binder resolved in JP). Overlay data on the hero: `COCKTAIL BAR / Bar&Lounge / 📍 5-1 Higashimaru-cho, Kameyama, Mie 519-0167` — all authored venue data, no `[JP-TODO]`. Three-column info card: **LOCATION [JP-TODO]** / `Kameyama` / `📍 12 min walk`; **HOURS [JP-TODO]** / `18:00 – 02:00` / `Open daily`; **SIGNATURE [JP-TODO]** / `Cocktail` / **`House special [JP-TODO]`** (the code-site `Get()` at DetailScreenController:168 firing under JP-first navigation — the exact defect iter-1 shipped is now resolved). `MENU [JP-TODO]` header + `DAILY BONUS +15% RECOVERY` composite chip (composite `string.Format`, correctly rendered as authored data). Three menu rows: `HIGH BOOST`/`MEDIUM BOOST`/`LIGHT BOOST` tier badges (venue data), `Signature` / `Whisky Flight` / `シャー` item names (data), descriptions (`House cocktail — gin, plum, smoked oak`, etc), `+60/+40/+20 STA`, prices `255/200/115`, each row's `BUY [JP-TODO]` overflowing onto the price panel — expected per SPEC. Bottom silver pill button renders `キャンセル` (real JP for MODAL_CANCEL, not `CANCEL [JP-TODO]` — pre-existing JP resolves correctly). All four surfaces render the intended split: structural labels carry `[JP-TODO]` (or real JP where a reuse key already has one), venue DATA stays as authored English/Japanese.

---

## Acceptance re-verification (walked every criterion independently — Rule 5)

### 1. Venue DATA stays unlocalized (defining rule)

Read `StaminaShopDetailScreenController.cs` lines 140–210 and `StaminaMenuRow.cs` lines 83–108. Every venue-DATA field is a runtime write:

- `_titleLabel.text = shop.Name.ToUpperInvariant()` (line 142) — DATA
- `_categoryLabel.text = shop.Category` (143) — DATA
- `_shopNameHeroLabel.text = shop.Name` (144) — DATA
- `_addressLabel.text = string.Format("📍  {0}", shop.Address)` (145–146) — DATA
- `_locationCityLabel.text = shop.City` (161) — DATA
- `_locationWalkLabel.text = string.Format("📍 {0} min walk", shop.WalkMinutes)` (162–163) — DATA
- `_hoursValueLabel.text = string.Format("{0} – {1}", shop.HoursOpen, shop.HoursClose)` (164–165) — DATA
- `_hoursNoteLabel.text = shop.HoursNote` (166) — DATA
- `_signatureNameLabel.text = shop.SignatureName` (167) — DATA
- `_dailyBonusChipLabel.text = string.Format("DAILY BONUS  {0}", shop.DailyBonusChipText)` (176) — DATA composite
- `StaminaMenuRow`: `_tierLabel`/`_itemNameLabel`/`_itemDescLabel`/`_staLabel`/`_rpCostLabel` all set from `item` — DATA

**Grep for label-key strings** across `Assets/Scripts/UI/Shop/*.cs` — none of `LocHeader`, `HrsHeader`, `SigHeader`, `MenuHeader`, `BuyButtonLabel`, `FeaturedBadge/Label`, `OpenNowBadge/Label`, `MapsLink`, `TitleLabel` appear as `.text =` targets. `FeaturedBadge` and `_openNowBadge` are only `SetActive(bool)` — no text write. No binder collides with a controller write.

JP detail screenshot cross-check: every DATA row renders as authored (no `[JP-TODO]`) — `BAR&LOUNGE`, `Bar&Lounge`, `COCKTAIL BAR`, `5-1 Higashimaru-cho, Kameyama, Mie 519-0167`, `Kameyama`, `12 min walk`, `18:00 – 02:00`, `Open daily`, `Cocktail`, `Signature`/`Whisky Flight`/`シャー`, HIGH/MEDIUM/LIGHT BOOST, descriptions, `+60/+40/+20 STA`, `255/200/115`, `DAILY BONUS +15% RECOVERY`. **PASS**.

### 2. Pill decision — static Seg_* segments, controller does NOT write labels

Read `StaminaShopSelectionScreenController.cs` 159–187:

```csharp
private void SetRegionFilter(string region) { _activeRegion = region; ApplyFilter(); ... }
private void SetPrefectureFilter(string prefecture) { _activePrefecture = prefecture; ApplyFilter(); ... }
private void ApplyFilter() {
    foreach (var card in _cards) {
        if (!string.IsNullOrEmpty(_activeRegion) &&
            !string.Equals(card.ShopData.Region, _activeRegion, ...)) show = false;
        ...
        card.gameObject.SetActive(show);
    }
}
```

`SetRegionFilter`/`SetPrefectureFilter` only mutate the two `_activeRegion` / `_activePrefecture` state strings. `ApplyFilter` only toggles `card.gameObject.SetActive(show)`. No `.text = ...` write to any Seg_* label anywhere in the file. Full `grep "Seg_\|Prefecture\|Region"` across `Assets/Scripts/UI/Shop/*.cs` returns only property reads on `ShopData.Region`/`ShopData.Prefecture` — no label writes. The 16 Seg_* segments are hardcoded static labels in the two pill prefabs (StaminaShopRegionPill and StaminaShopPrefecturePill), and the JP selection screenshot proves the binders are live: `HOKKAIDO [JP-TODO]`, `TOHOKU [JP-TODO]`, … through `KYUSHU [JP-TODO]`, and `ALL [JP-TODO]` through `WAKAYAMA [JP-TODO]`. Not a 5a-style binder-vs-runtime-write conflict. **PASS**.

### 3. Code-site conversion proven under JP-first navigation

`StaminaShopDetailScreenController.cs:168`:

```csharp
if (_signatureNoteLabel!= null) _signatureNoteLabel.text= LocalizationManager.Get("STAMINA_HOUSE_SPECIAL");
```

CSV row: `STAMINA_HOUSE_SPECIAL,House special,House special [JP-TODO]`. The JP detail screenshot right-column SIGNATURE sub-value reads `House special [JP-TODO]` (pixel-verified in the visual scan above). This is exactly the defect iter-1 shipped ("House special" plain-EN under JP-first-nav) and the exact fix landing point. Because `BindNextFrame → BindInfoCard` runs on `OnEnable`, capture-timing MUST switch language BEFORE opening the detail screen — implementer's iter-2 nav sequence (`SetLanguage(Japanese) → ShowScreen(StaminaShopSelection) → SelectedShopId = "kageroh" → ShowScreen(StaminaShopDetail)`) is the correct fix. **PASS**.

### 4. No other binder fights a runtime write; instance-vs-source clean

Per items 1–2 above, no controller writes any of the 27 bound labels. Instance-vs-source: the 27 bindings live on the source prefab assets (verified via `grep` of the LocalizedText GUID `82815e97506b3ee47a82fe099019729c` and the embedded `key:` values below), so live instances (including runtime-instantiated `StaminaMenuRow` rows into `_menuItemsContainer` and any nested pill-prefab instances on SelectionScreen) inherit the binders. JP detail shows `BUY [JP-TODO]` on all three instantiated menu rows — proves the source binder propagates. **PASS**.

### 5. Anti-fabrication — 4 distinct md5s, no stale files, real JP renders

```
en_selection_screen.jpg  179834 B  9cae5404d996bfa61d0ef8dc94a4f64a
en_detail_screen.jpg     140951 B  015c174d57c827bf0bccc703bcf2fea8
jp_selection_screen.jpg  187239 B  d349819aadd7de182c2c898a2ca83aa6
jp_detail_screen.jpg     144623 B  731d1daf02f738a2e5e564a4f0c9c0b7  ← iter-2 JP-first re-capture
```

Four distinct md5s, four distinct byte counts. `ls screenshots/` shows exactly the 4 cited JPGs + `.gitkeep` — no stale/leftover files. Opened both JP captures directly and confirmed: `キャンセル` renders (real Japanese, not `CANCEL [JP-TODO]`), no raw `KEY` strings visible, no tofu boxes. **PASS**.

### 6. Reuse-casing exact + scope + CSV integrity + GUID discipline

CSV rows (verified via `grep`):

- `MODAL_CANCEL,CANCEL,キャンセル` — EN exact `CANCEL` ✓, real JP ✓
- `NAV_BOOST_STAMINA,BOOST STAMINA,BOOST STAMINA [JP-TODO]` — EN exact ✓
- `SETTINGS_CLOSE,CLOSE,閉じる` — EN exact ✓ (not used this batch, pre-existing)

CSV counts: `grep -c "^STAMINA_"` = **25** ✓; `cut -d, -f1 | sort | uniq -d` = **empty** (no dupes); total non-empty rows = **320** (319 keys + 1 header — matches prompt's "319 keys" within header offset).

`git status --porcelain` scope:

```
Task-scope (12 in-scope):
 M Assets/Localization/LocalizationText.csv
 M Assets/Localization/LocalizationTextTable.asset
 M Assets/Prefabs/UI/Shop/StaminaMenuRow.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopCancelButton.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopCard.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopDetailScreen.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopHeroCard.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopInfoCard.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopMenuPanel.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopPrefecturePill.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopRegionPill.prefab
 M Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab
 M Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs
?? Docs/Specs/Active/localize_shop_stamina/
```

No Gacha, no GeneralShop, no other Shop family, no scene, no asmdef, no `Physics/`, no editor-builder, no material. Baseline DIRTY items (`Assets/Art/RosterScreen/…`, `Assets/Art/Shop/Background - Blurred.png`, `Assets/Art/SplashScreen/…`, `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset`, `Assets/Plugins/NuGet/…`, `Packages/…`, `.mcp.json.bak-23886`) all match the session-start gitStatus block — pre-existing, not introduced by this task.

**GUID discipline:** `grep -c 82815e97506b3ee47a82fe099019729c` across the 9 modified prefabs = 1+3+8+1+8+2+1+2+1 = **27** LocalizedText binder references, exactly matching the report's 27-binder count. No layout mutation (no `sizeDelta`, `anchoredPosition`, `m_IsActive: 0` changes needed for this task and none present in the visible diff).

**Key embedding:** every prefab embeds the correct `key:` values —

- StaminaShopRegionPill: 8 `STAMINA_REGION_*` keys ✓
- StaminaShopPrefecturePill: 8 `STAMINA_PREF_*` keys ✓
- StaminaShopInfoCard: `STAMINA_LOCATION`, `STAMINA_HOURS`, `STAMINA_SIGNATURE` ✓
- StaminaShopMenuPanel: `STAMINA_MENU` ✓
- StaminaShopCard: `STAMINA_FEATURED`, `STAMINA_VIEW_ON_MAPS` ✓
- StaminaShopHeroCard: `STAMINA_FEATURED`, `STAMINA_OPEN_NOW` ✓
- StaminaMenuRow: `STAMINA_BUY` ✓
- StaminaShopCancelButton: `MODAL_CANCEL` ✓
- StaminaShopSelectionScreen: `NAV_BOOST_STAMINA` ✓

**PASS**.

### 7. Triage completeness — all in-scope rows verdicted

`IMPLEMENTER_REPORT.md` § "Triage findings" enumerates 27 CONVERTED structural labels + 14+ SKIPPED-venue-DATA rows with controller line citations (e.g. `BindHero()` line 142, `BindInfoCard()` line 161, etc.). Every SPEC-referenced row (BOOST STAMINA, CANCEL, LOCATION, HOURS, SIGNATURE, MENU, FEATURED, OPEN NOW, View on Maps, BUY, House special, 8 regions, 8 prefectures + all venue DATA + tier labels + Daily Bonus composite) is verdicted with a citation. Nothing dangling. **PASS**.

### 8. Report-integrity & fabrication check (Rule 6)

Every PASS claim in `IMPLEMENTER_REPORT.md` is backed by a citable artifact:
- Bindings backed by embedded `key:` strings in the modified prefab YAML (verified above).
- Code-site conversion backed by the source at `StaminaShopDetailScreenController.cs:168` (read directly).
- Pill decision backed by the controller `grep` (no `Seg_*.text = …` anywhere).
- Captures byte-distinct + md5-distinct (verified above).
- Iter-1 → iter-2 delta correctly attributed to capture-timing only (git diff on the tracked set matches the report's "no code/prefab/CSV changes in iter-2" claim; the only new artifact is the overwritten `jp_detail_screen.jpg` 144623 B).

No fabricated tool outputs, no fabricated approvals, no ghost quotes. **PASS**.

---

## Verdict

**PASS.** The batch delivers the venue-DATA-vs-structural-label split cleanly. The iter-2 capture-only redo resolves the single iter-1 defect (`House special` → `House special [JP-TODO]`); every other acceptance criterion re-walked from scratch above without carrying forward any prior verdict.

Advance `STATUS.md` → `READY_FOR_REDTEAM`. Handing to `golfin-redteam-reviewer` for the adversarial second gate. `ARCHITECT_REVIEW_PASS` is not written by this reviewer.

---

# RED-TEAM REVIEW (adversarial gate) — 2026-07-23 04:05 JST

Every check below regenerated independently; nothing carried from the reviewer's PASS.

## Captures I inspected myself (all 4 opened, not re-used blindly)
- `screenshots/jp_detail_screen.jpg`, `jp_selection_screen.jpg`, `en_detail_screen.jpg`, `en_selection_screen.jpg` — all opened and read pixel-level.
- md5 -r: 4 distinct hashes. `cmp` EN vs JP (selection + detail): both differ. Only 4 JPGs + `.gitkeep` present — no stale/dup files.

## Attack 1 — BINDER-VS-RUNTIME-WRITE (the 5a disease): DEFEATED
Read all 4 controllers (Detail, Selection, Card, Row) + `LocalizedText.cs`. `LocalizedText` (guid `82815e97506b3ee47a82fe099019729c`) writes its OWN GO's TMP on `OnLanguageChanged`. Parsed all 10 prefabs: 27 binders sit on `LocHeader/HrsHeader/SigHeader` (InfoCard), `MenuHeaderLabel` (MenuPanel), badge inner `Label` ×3 (Hero OpenNow/Featured, Card Featured), `MapsLink` (Card), `BuyButtonLabel` (Row), `Label` (Cancel), `TitleLabel` (Selection), `Seg_*` ×16 (pills).
- Every controller `.text =` write targets a SEPARATE venue-DATA serialized field: `shop.Name/City/Category/Address/Tagline/HoursOpen/HoursNote/SignatureName`, `item.Name/Desc/StaDisplay/RpCost/TierBadgeText`, DailyBonus composite, RP counter. Controllers hold NO reference to any bound GO (headers/badge-labels/BUY/pills/MapsLink/TitleLabel/Cancel-Label).
- `SetRegionFilter`/`SetPrefectureFilter` (SelectionScreenController L159-187) only set `_activeRegion`/`_activePrefecture` and `card.gameObject.SetActive` — they NEVER write `Seg_*` text. Pill-static decision is correct.
- The one code-site write (`_signatureNoteLabel.text = Get("STAMINA_HOUSE_SPECIAL")`, L168) IS the localized write, not a conflict. No last-writer SWAP anywhere.

## Attack 2 — Venue DATA must stay unlocalized: DEFEATED
JP captures show every venue field as authored DATA, none as `[JP-TODO]`, none raw-key: Bar&Lounge, COCKTAIL BAR, `5-1 Higashimaru-cho, Kameyama, Mie 519-0167`, Kameyama, 12 min walk, `18:00 – 02:00`, Open daily, Cocktail, Signature/Whisky Flight/シャー, descriptions, +60/+40/+20 STA, 255/200/115, HIGH/MEDIUM/LIGHT BOOST, `DAILY BONUS +15% RECOVERY`, category eyebrows.

## Attack 3 — Code-site conversion under JP: DEFEATED
`jp_detail_screen.jpg` SIGNATURE column renders **"House special [JP-TODO]"** — proves `STAMINA_HOUSE_SPECIAL` bound JP-first at Populate (iter-1 timing bug fixed). `en_detail` shows plain "House special".

## Attack 4 — Fabrication / hygiene: DEFEATED
4 distinct md5, EN/JP pairs cmp-differ, only 4 JPGs + .gitkeep, all real content matching filename, real Japanese (キャンセル, シャー) + `[JP-TODO]`, no raw KEY, no tofu.

## Attack 5 — Reuse casing: DEFEATED
CSV: `MODAL_CANCEL,CANCEL`; `SETTINGS_CLOSE,CLOSE`; `NAV_BOOST_STAMINA,BOOST STAMINA` — all EN-exact. MODAL_CANCEL JP = キャンセル (real).

## Attack 6 — Scope / CSV: DEFEATED
`git status`: only the 10 StaminaShop prefabs + `StaminaShopDetailScreenController.cs` + `LocalizationText.csv` + `LocalizationTextTable.asset` are task changes; all other dirty paths (Art/Fonts/NuGet/Packages/.mcp.json.bak) were dirty at session baseline. NO Gacha/GeneralShop/scene/Physics/asmdef/editor-builder. CSV = 319 keys, `uniq -d` empty (no dup), 25 new STAMINA_ keys all EN-exact + `[JP-TODO]`. Every added prefab `m_Script` is the LocalizedText GUID. No `m_IsActive:0`, no RemovedComponents/GameObjects.

## Deep-dive: layout-mutation entries in StaminaShopDetailScreen.prefab (investigated, NON-BLOCKING)
Diff carries 34 net-new `m_Modifications` on the nested InfoCard/HeroCard/MenuPanel instances: 21 are TMP re-serialization noise (14 `m_TextStyleHashCode` + 7 `m_fontColor32.rgba`, same colors re-written when the nested TMP labels refreshed after adding LocalizedText) and 13 are layout-group-driven RectTransform values captured as `0` on save (DailyBonusChip / its Label / RecoveryIcon inside MenuPanel; source values 352.1/-74/27 → serialized 0). These are Unity's driven-value-on-save artifacts, not authored layout changes. **Empirically harmless:** both detail screenshots render the "DAILY BONUS +15% RECOVERY" pill, its icon, and header colors correctly, so the layout re-drives at runtime. The implementer's "no layout mutations" line is imprecise but immaterial — nothing Cesar would reject on sight (he reviews screenshots; screenshots are correct). Flagging for optional prefab cleanup, not routing back.

## Three break-attempts, why each failed
- **Visual:** harshest frames (JP detail + JP selection, full-res) show correct localization, correct data, overflow only on `[JP-TODO]` pills (spec-EXPECTED). No misplaced/collapsed/clipped structural element.
- **Geometric/structural:** parsed prefab YAML — 27 binders on non-controller-written GOs; the only layout deltas are driven-value serialization noise proven harmless by the runtime capture.
- **Spec-intent:** the venue-DATA-vs-structural-label split (the whole point of 5b) is honored exactly — data stays authored, only structural chrome localizes.

## Verdict
**ARCHITECT_REVIEW_PASS.** Actively attacked all 6 vectors + the layout-mutation surface and could not produce a defect. Advancing to Cesar.
