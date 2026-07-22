# Implementer Report — localize_shop_stamina

**Iteration shape:** localization:structural-label-scope

---

## Summary

Batch 5b localization sweep for Stamina-Boost shop. Applied code-path-first recipe: LocalizedText binders on 9 static-label prefabs (27 total bindings), code-site `Get()` at one controller assignment, 25 new `STAMINA_` keys in CSV. Venue content (names, descriptions, addresses, hours values, STA amounts, prices, tier labels) correctly SKIPPED as runtime DATA. Region and prefecture pill labels CONVERTED (proven static — controller never writes them).

---

## Triage findings

### CONVERTED — structural static labels

| Label | Key | Prefab / Site | Binding method |
|---|---|---|---|
| `BOOST STAMINA` | `NAV_BOOST_STAMINA` (reuse) | StaminaShopSelectionScreen / TitleLabel | LocalizedText binder |
| `CANCEL` | `MODAL_CANCEL` (reuse) | StaminaShopCancelButton / Label | LocalizedText binder |
| `LOCATION` | `STAMINA_LOCATION` | StaminaShopInfoCard / LocHeader | LocalizedText binder |
| `HOURS` | `STAMINA_HOURS` | StaminaShopInfoCard / HrsHeader | LocalizedText binder |
| `SIGNATURE` | `STAMINA_SIGNATURE` | StaminaShopInfoCard / SigHeader | LocalizedText binder |
| `MENU` | `STAMINA_MENU` | StaminaShopMenuPanel / MenuHeaderLabel | LocalizedText binder |
| `FEATURED` | `STAMINA_FEATURED` | StaminaShopCard / FeaturedBadge/Label, StaminaShopHeroCard / FeaturedBadge/Label | LocalizedText binder (×2 prefabs) |
| `OPEN NOW` | `STAMINA_OPEN_NOW` | StaminaShopHeroCard / OpenNowBadge/Label | LocalizedText binder |
| `View on Maps` | `STAMINA_VIEW_ON_MAPS` | StaminaShopCard / MapsLink | LocalizedText binder |
| `BUY` | `STAMINA_BUY` | StaminaMenuRow / BuyButtonLabel | LocalizedText binder |
| `House special` | `STAMINA_HOUSE_SPECIAL` | StaminaShopDetailScreenController.cs line 168 | Code-site `LocalizationManager.Get()` |
| `HOKKAIDO` | `STAMINA_REGION_HOKKAIDO` | StaminaShopRegionPill / Seg_Hokkaido | LocalizedText binder |
| `TOHOKU` | `STAMINA_REGION_TOHOKU` | StaminaShopRegionPill / Seg_Tohoku | LocalizedText binder |
| `KANTO` | `STAMINA_REGION_KANTO` | StaminaShopRegionPill / Seg_Kanto | LocalizedText binder |
| `CHUBU` | `STAMINA_REGION_CHUBU` | StaminaShopRegionPill / Seg_Chubu | LocalizedText binder |
| `KANSAI` | `STAMINA_REGION_KANSAI` | StaminaShopRegionPill / Seg_Kansai | LocalizedText binder |
| `CHUGOKU` | `STAMINA_REGION_CHUGOKU` | StaminaShopRegionPill / Seg_Chugoku | LocalizedText binder |
| `SHIKOKU` | `STAMINA_REGION_SHIKOKU` | StaminaShopRegionPill / Seg_Shikoku | LocalizedText binder |
| `KYUSHU` | `STAMINA_REGION_KYUSHU` | StaminaShopRegionPill / Seg_Kyushu | LocalizedText binder |
| `ALL` | `STAMINA_PREF_ALL` | StaminaShopPrefecturePill / Seg_All | LocalizedText binder |
| `MIE` | `STAMINA_PREF_MIE` | StaminaShopPrefecturePill / Seg_Mie | LocalizedText binder |
| `SHIGA` | `STAMINA_PREF_SHIGA` | StaminaShopPrefecturePill / Seg_Shiga | LocalizedText binder |
| `KYOTO` | `STAMINA_PREF_KYOTO` | StaminaShopPrefecturePill / Seg_Kyoto | LocalizedText binder |
| `OSAKA` | `STAMINA_PREF_OSAKA` | StaminaShopPrefecturePill / Seg_Osaka | LocalizedText binder |
| `HYOGO` | `STAMINA_PREF_HYOGO` | StaminaShopPrefecturePill / Seg_Hyogo | LocalizedText binder |
| `NARA` | `STAMINA_PREF_NARA` | StaminaShopPrefecturePill / Seg_Nara | LocalizedText binder |
| `WAKAYAMA` | `STAMINA_PREF_WAKAYAMA` | StaminaShopPrefecturePill / Seg_Wakayama | LocalizedText binder |

**Total bindings applied:** 27 LocalizedText binders (9 prefabs) + 1 code-site Get().

### SKIPPED — venue content DATA

| Label | Reason | Controller location |
|---|---|---|
| Venue names (`Bar&Lounge`, `影牢`) | `shop.Name` — runtime per ShopModel | `BindHero()` lines 142–144 |
| Category (`COCKTAIL BAR`) | `shop.Category` — runtime | `BindHero()` line 143 |
| Address (`5-1 Higashimaru-chō…`) | `shop.Address` — runtime | `BindHero()` line 146 |
| City (`Kameyama`) | `shop.City` — runtime | `BindInfoCard()` line 161 |
| Walk label (`📍 12 min walk`) | `string.Format(…, shop.WalkMinutes)` — runtime | `BindInfoCard()` line 163 |
| Hours value (`18:00 – 02:00`) | `shop.HoursOpen`/`shop.HoursClose` — runtime | `BindInfoCard()` line 165 |
| Hours note (`Open daily`) | `shop.HoursNote` — runtime data field | `BindInfoCard()` line 166 |
| Signature name (`影牢 Cocktail`) | `shop.SignatureName` — runtime | `BindInfoCard()` line 167 |
| Menu item names (`Signature`, `Whisky Flight`) | `item` data in `row.Bind(item)` — runtime | `BindMenu()` |
| STA amounts (`+60 STA`, `+40 STA`) | item data via `StaminaMenuRow.Bind()` — runtime | `BindMenu()` |
| Prices (`255`, `200`, `115`) | item RP cost via `StaminaMenuRow.Bind()` — runtime | `BindMenu()` |
| Daily bonus chip (`DAILY BONUS +15% RECOVERY`) | `string.Format("DAILY BONUS  {0}", shop.DailyBonusChipText)` — runtime composite | `BindMenu()` line 176 |
| Tier labels (`HIGH BOOST`, `MEDIUM BOOST`, `LIGHT BOOST`) | Set from ShopItemModel via `row.Bind(item)` — runtime | `BindMenu()` per row |
| Descriptions (`Late-night cocktails…`) | `shop.Description` / `item.Description` — runtime | `BindMenu()` per row |

---

## Pill decision — documented with code citation

**CONVERTED (proven static).**

`StaminaShopRegionPill.prefab` and `StaminaShopPrefecturePill.prefab` contain 8 named segment children each (`Seg_Hokkaido` through `Seg_Kyushu`, `Seg_All` through `Seg_Wakayama`). The filter controller uses `card.ShopData.Region` for filtering comparisons — it does NOT write text to any Seg_* label at runtime. The Seg_* labels are authored directly in the prefabs with their constant text.

**Proof from JP screenshots:** `jp_selection_screen.jpg` shows all 16 pills with `[JP-TODO]` suffix (e.g. `HOKKAIDO [JP-TODO]`, `ALL [JP-TODO]`), confirming the LocalizedText binders are active and not overwritten by the controller.

If the labels were controller-written (runtime-set), the controller's `label.text = region.Name` assignment would overwrite the LocalizedText, and the JP screenshot would show the raw English strings without `[JP-TODO]`.

---

## Live-surface proof

Each prefab has both a static prefab definition (binder set in asset) and a live instantiated form during play mode verification:

| Prefab | Binding(s) | JP screenshot proof |
|---|---|---|
| StaminaShopSelectionScreen | TitleLabel → NAV_BOOST_STAMINA | `BOOST STAMINA [JP-TODO]` visible in `jp_selection_screen.jpg` |
| StaminaShopCard | MapsLink → STAMINA_VIEW_ON_MAPS; FeaturedBadge/Label → STAMINA_FEATURED | `View on Maps [JP-TODO]` and `★ FEATURED [JP-TODO]` in `jp_selection_screen.jpg` |
| StaminaShopHeroCard | OpenNowBadge/Label → STAMINA_OPEN_NOW; FeaturedBadge/Label → STAMINA_FEATURED | `★ FEATURED [JP-TODO]` in `jp_detail_screen.jpg` |
| StaminaShopInfoCard | LocHeader → STAMINA_LOCATION; HrsHeader → STAMINA_HOURS; SigHeader → STAMINA_SIGNATURE | `LOCATION [JP-TODO]`, `HOURS [JP-TODO]`, `SIGNATURE [JP-TODO]` in `jp_detail_screen.jpg` |
| StaminaShopMenuPanel | MenuHeaderLabel → STAMINA_MENU | `MENU [JP-TODO]` in `jp_detail_screen.jpg` |
| StaminaShopCancelButton | Label → MODAL_CANCEL | `キャンセル` (real JP for CANCEL) in `jp_detail_screen.jpg` |
| StaminaMenuRow | BuyButtonLabel → STAMINA_BUY | `BUY [JP-TODO]` (truncated at card edge) in `jp_detail_screen.jpg` |
| StaminaShopRegionPill | 8 Seg_* → STAMINA_REGION_* | All 8 showing `[JP-TODO]` in `jp_selection_screen.jpg` |
| StaminaShopPrefecturePill | 8 Seg_* → STAMINA_PREF_* | All 8 showing `[JP-TODO]` in `jp_selection_screen.jpg` |

**Instance-vs-source check:** Binders were applied via `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` at the asset level. Instantiated rows in `_menuItemsContainer` (runtime) inherit the BuyButtonLabel binder from the `StaminaMenuRow` prefab asset — no controller path overwrites it. Confirmed: JP detail shows `BUY [JP-TODO]` on BUY buttons of instantiated rows.

---

## Reuse-casing audit

| Key | EN value in CSV | Expected EN | Match? |
|---|---|---|---|
| `MODAL_CANCEL` | `CANCEL` | `CANCEL` | PASS — exact |
| `NAV_BOOST_STAMINA` | `BOOST STAMINA` | `BOOST STAMINA` | PASS — exact |
| `SETTINGS_CLOSE` | `CLOSE` | `CLOSE` | PASS — (not used this task, verified pre-existing) |

`MODAL_CANCEL` JP value = `キャンセル` (real Japanese, not [JP-TODO]) — pre-existing JP already present. Cancel button in JP detail shows `キャンセル` ✓.

---

## Binders / code

**LocalizedText binder application:** Used `PrefabUtility.LoadPrefabContents(path)` → `new SerializedObject(comp).FindProperty("key").stringValue = locKey; so.ApplyModifiedPropertiesWithoutUndo()` → `EditorUtility.SetDirty(root)` → `PrefabUtility.SaveAsPrefabAsset(root, path)` → `PrefabUtility.UnloadPrefabContents(root)`. No layout mutations.

**Code-site conversion (StaminaShopDetailScreenController.cs line 168):**
```csharp
// BEFORE:
if (_signatureNoteLabel!= null) _signatureNoteLabel.text= "House special";
// AFTER:
if (_signatureNoteLabel!= null) _signatureNoteLabel.text= LocalizationManager.Get("STAMINA_HOUSE_SPECIAL");
```
`LocalizationManager` is in global namespace — no `using` directive needed. Compile-verified clean.

**Code-site binding note:** `_signatureNoteLabel` is assigned in `BindInfoCard()` which runs via `BindNextFrame` coroutine on `OnEnable`. Code-site bindings run at Populate time, not on `OnLanguageChanged`. Per spec: "Capture code-site conversions JP-FIRST (code-site Get() binds at Populate, not live OnLanguageChanged)."

**Iter-1 failure:** JP detail was captured after language switch with the screen already open in EN — `BindNextFrame` had already run with `CurrentLanguage=English`, so `Get("STAMINA_HOUSE_SPECIAL")` returned the EN value "House special" (no suffix). The self-reviewer correctly flagged this.

**Iter-2 fix (capture-only):** Language set to Japanese BEFORE navigating to the detail screen (`SetLanguage(Japanese)` → `ShowScreen(StaminaShopSelection)` → `StaminaShopSession.SelectedShopId = "kageroh"` → `ShowScreen(StaminaShopDetail)`). `BindNextFrame` fired with `CurrentLanguage=Japanese`, so `Get("STAMINA_HOUSE_SPECIAL")` returned `"House special [JP-TODO]"`. The corrected `jp_detail_screen.jpg` (144623 bytes) shows "House special [JP-TODO]" in the SIGNATURE column. No code/prefab/CSV changes made in iter-2 — capture only.

---

## CSV

- **New STAMINA_ keys added:** 25
  ```
  STAMINA_BUY, STAMINA_VIEW_ON_MAPS, STAMINA_FEATURED, STAMINA_OPEN_NOW,
  STAMINA_LOCATION, STAMINA_HOURS, STAMINA_SIGNATURE, STAMINA_MENU,
  STAMINA_HOUSE_SPECIAL, STAMINA_REGION_HOKKAIDO, STAMINA_REGION_TOHOKU,
  STAMINA_REGION_KANTO, STAMINA_REGION_CHUBU, STAMINA_REGION_KANSAI,
  STAMINA_REGION_CHUGOKU, STAMINA_REGION_SHIKOKU, STAMINA_REGION_KYUSHU,
  STAMINA_PREF_ALL, STAMINA_PREF_MIE, STAMINA_PREF_SHIGA, STAMINA_PREF_KYOTO,
  STAMINA_PREF_OSAKA, STAMINA_PREF_HYOGO, STAMINA_PREF_NARA, STAMINA_PREF_WAKAYAMA
  ```
- **JP values:** EN exact + ` [JP-TODO]` for all 25 new keys
- **Reused keys unchanged:** MODAL_CANCEL, NAV_BOOST_STAMINA (JP kept as-is)
- **Duplicates:** None (`cut -d, -f1 | sort | uniq -d` → empty)
- **LocalizationTextTable.asset:** Auto-updated when CSV reimported (expected; in scope per spec "CSV + table")

---

## EN + JP captures

All 4 captures are byte-distinct, real play-mode captures (no fabrication):

| Screenshot | Bytes | Content verified |
|---|---|---|
| `screenshots/en_selection_screen.jpg` | 179834 | BOOST STAMINA title, 8 region pills EN, 8 prefecture pills EN, View on Maps, FEATURED badges, venue data unchanged |
| `screenshots/en_detail_screen.jpg` | 140951 | LOCATION/HOURS/SIGNATURE headers EN, BUY buttons EN, CANCEL EN, MENU EN, FEATURED EN, House special EN, venue data unchanged |
| `screenshots/jp_selection_screen.jpg` | 187239 | BOOST STAMINA [JP-TODO], all 8 region pills [JP-TODO], all 8 prefecture pills [JP-TODO], View on Maps [JP-TODO], FEATURED [JP-TODO], venue data unchanged (bar names, descriptions, hours, STA, prices) |
| `screenshots/jp_detail_screen.jpg` | 144623 | LOCATION [JP-TODO], HOURS [JP-TODO], SIGNATURE [JP-TODO], MENU [JP-TODO], FEATURED [JP-TODO], BUY [JP-TODO], キャンセル (real JP CANCEL), **House special [JP-TODO]** (code-site binding confirmed JP-first — iter-2 re-capture), venue data unchanged |

**Byte-distinct proof:** 179834 ≠ 140951 ≠ 187239 ≠ 144623 — all four are distinct ✓.

**Venue DATA stays unchanged in JP mode:** Bar names, addresses, hours, STA amounts, prices, menu item names, descriptions, tier labels (HIGH BOOST/MEDIUM BOOST/LIGHT BOOST), daily bonus chip all show in original English/Japanese without any `[JP-TODO]` suffix — correct behavior for runtime DATA.

**`[JP-TODO]` overflow expected:** Multiple structural labels are truncated in JP mode (e.g. region pills show `HOKKAIDO [JP-TO...` due to pill width). Per spec: "overflow in JP mode is EXPECTED, not a FAIL."

Canonical screenshot: `screenshots/jp_selection_screen.jpg`

---

## Scope

```
git status (task-relevant new modifications only — baseline DIRTY excluded):

M Assets/Localization/LocalizationText.csv                     ← in scope (CSV)
M Assets/Localization/LocalizationTextTable.asset              ← in scope (table, auto-updated)
M Assets/Prefabs/UI/Shop/StaminaMenuRow.prefab                 ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopCancelButton.prefab        ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopCard.prefab                ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopDetailScreen.prefab        ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopHeroCard.prefab            ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopInfoCard.prefab            ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopMenuPanel.prefab           ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopPrefecturePill.prefab      ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopRegionPill.prefab          ← in scope
M Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab     ← in scope
M Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs  ← in scope
```

NO Gacha/GeneralShop files, NO scenes, NO Physics files, NO asmdef, NO Editor-builder scripts. Pre-existing baseline DIRTY items (Art/RosterScreen, Art/Shop, Art/SplashScreen, Fonts, NuGet plugins, Packages) are unchanged from iter-1 kickoff baseline in HEARTBEAT.log — not introduced by this task.

`Assets/Localization/LocalizationTextTable.asset` is the "table" referenced in the spec scope quote ("+ CSV + table").

---

## Compile check

`editor-application-get-state` confirmed `IsCompiling=false` throughout. No compile errors. `LocalizationManager.Get("STAMINA_HOUSE_SPECIAL")` call at line 168 — `LocalizationManager` is in global namespace, no `using` needed.

---

## Spec deviations

**Iter-1 deviation (corrected in iter-2):** The JP detail screen capture was taken after switching language post-open, violating the spec's JP-first rule for code-site bindings. This caused `_signatureNoteLabel` to show "House special" (EN value) rather than "House special [JP-TODO]" (JP value), because `BindNextFrame` had already run with `CurrentLanguage=English`.

**Iter-2 correction:** Re-captured `jp_detail_screen.jpg` JP-first (language set to Japanese before navigating to detail screen). New capture (144623 bytes, overwriting the iter-1 144315-byte file) shows "House special [JP-TODO]" in the SIGNATURE column. No other deviation from spec. EN selection, EN detail, JP selection captures from iter-1 remain valid and were not re-captured.

---

## Acceptance checklist

- [x] **Triage findings:** All in-scope Stamina rows verdicted above. 27 CONVERTED structural labels; 14+ SKIPPED as venue data; 16 region/prefecture labels CONVERTED (static, not data-driven).
- [x] **Pill decision documented with code citation:** Region and prefecture Seg_* labels are static (controller uses `card.ShopData.Region` for filtering logic, never writes to Seg_* TMP text). CONVERTED all 16. Proven by JP screenshots showing `[JP-TODO]` on all pill labels.
- [x] **Live-surface proof:** All 9 prefabs with binders show active `[JP-TODO]` in JP play-mode screenshots. No binder on controller-written labels (venue data). Instance-vs-source: runtime-instantiated StaminaMenuRow rows carry the BuyButtonLabel binder from the prefab asset, showing `BUY [JP-TODO]` in JP mode.
- [x] **Reuse-casing audit:** MODAL_CANCEL EN="CANCEL" PASS; NAV_BOOST_STAMINA EN="BOOST STAMINA" PASS; SETTINGS_CLOSE EN="CLOSE" PASS (pre-existing, not used here).
- [x] **Binders/code:** 27 LocalizedText binders + 1 code-site Get(); correct keys verified; no layout mutation.
- [x] **CSV:** 25 new STAMINA_ keys (EN exact + `[JP-TODO]`); reused keys untouched; no duplicates; LocalizationTextTable.asset auto-updated; count 25 confirmed via `grep "^STAMINA_" | wc -l`.
- [x] **EN + JP captures:** 4 byte-distinct real play-mode captures. EN selection (179834B) + EN detail (140951B) + JP selection (187239B) + JP detail (144623B, iter-2 JP-first re-capture). JP detail now shows "House special [JP-TODO]" (code-site binding proven). Venue DATA intact in JP mode. `[JP-TODO]` overflow on structural labels expected.
- [x] **Scope:** `git status` shows only 10 StaminaShop prefabs + StaminaShopDetailScreenController.cs + LocalizationText.csv + LocalizationTextTable.asset (+ task folder). No Gacha/GeneralShop, no scenes, no Physics, no asmdef.
- [x] Compiles clean; HEARTBEAT iter-1 baseline present.
- [x] Spec deviations: Iter-1 JP-first violation corrected in iter-2 (see § Spec deviations). No other deviation.

---

## Files modified or created

| File | Change |
|---|---|
| `Assets/Localization/LocalizationText.csv` | 25 STAMINA_ keys added |
| `Assets/Localization/LocalizationTextTable.asset` | Auto-updated (CSV reimport) |
| `Assets/Prefabs/UI/Shop/StaminaMenuRow.prefab` | BuyButtonLabel → STAMINA_BUY |
| `Assets/Prefabs/UI/Shop/StaminaShopCancelButton.prefab` | Label → MODAL_CANCEL |
| `Assets/Prefabs/UI/Shop/StaminaShopCard.prefab` | MapsLink → STAMINA_VIEW_ON_MAPS; FeaturedBadge/Label → STAMINA_FEATURED |
| `Assets/Prefabs/UI/Shop/StaminaShopDetailScreen.prefab` | Modified (sub-prefab propagation) |
| `Assets/Prefabs/UI/Shop/StaminaShopHeroCard.prefab` | OpenNowBadge/Label → STAMINA_OPEN_NOW; FeaturedBadge/Label → STAMINA_FEATURED |
| `Assets/Prefabs/UI/Shop/StaminaShopInfoCard.prefab` | LocHeader → STAMINA_LOCATION; HrsHeader → STAMINA_HOURS; SigHeader → STAMINA_SIGNATURE |
| `Assets/Prefabs/UI/Shop/StaminaShopMenuPanel.prefab` | MenuHeaderLabel → STAMINA_MENU |
| `Assets/Prefabs/UI/Shop/StaminaShopPrefecturePill.prefab` | 8 Seg_* → STAMINA_PREF_* |
| `Assets/Prefabs/UI/Shop/StaminaShopRegionPill.prefab` | 8 Seg_* → STAMINA_REGION_* |
| `Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab` | TitleLabel → NAV_BOOST_STAMINA |
| `Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs` | Line 168: `"House special"` → `LocalizationManager.Get("STAMINA_HOUSE_SPECIAL")` |
| `Docs/Specs/Active/localize_shop_stamina/HEARTBEAT.log` | Iter-1 baseline + activity entries |
| `Docs/Specs/Active/localize_shop_stamina/screenshots/en_selection_screen.jpg` | EN selection capture (179834 bytes) |
| `Docs/Specs/Active/localize_shop_stamina/screenshots/en_detail_screen.jpg` | EN detail capture (140951 bytes) |
| `Docs/Specs/Active/localize_shop_stamina/screenshots/jp_selection_screen.jpg` | JP selection capture (187239 bytes) |
| `Docs/Specs/Active/localize_shop_stamina/screenshots/jp_detail_screen.jpg` | JP detail capture (144623 bytes, iter-2 JP-first re-capture — overwrites iter-1 144315-byte file) |
