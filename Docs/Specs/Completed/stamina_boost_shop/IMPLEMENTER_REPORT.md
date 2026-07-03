# IMPLEMENTER_REPORT — stamina_boost_shop

**Status:** VISUAL + FUNCTIONAL COMPLETE — both screens mounted, data-bound, navigable; demo recorded end-to-end with real assets. Awaiting Cesar review.
**Iteration shape:** shop_ui:figma-fidelity-rebuild
**Built by:** handheld (main thread), not the subagent pipeline, per Cesar directive after 3 from-scratch strikes.
**Canonical screenshot:** `Docs/Specs/Active/stamina_boost_shop/screenshots/detail_final.png`
**Canonical video:** `Docs/Specs/Active/stamina_boost_shop/videos/stamina_boost_shop_demo.mp4` (1170×2532, 22s, captioned; copy in `Docs/Reports/Media/stamina_boost_shop/`)

## Summary
Both shop screens rebuilt from gated, node-exact components that **reuse existing atoms** (navy panel `Background - Next Hole`, `S_PillStadium` two-layer pills, `RPContainer` navy RP pill, `Play Button`/`ButtonCancel` gold/silver buttons, `Reward Points Icon`, `IconStaminaSmall`, `BGCorner20` rounding masks, SDF fonts) — never fabricated flat-fills. Every prefab passed the **UIFidelityLinter** (render-health + node-spec) at `fail == 0` and was pixel-diffed against its Figma node before acceptance. Shop text uses the **÷1.2 TMP convention** consistently.

## Components built (all lint 0/0/0)
| Prefab | Node | Render | Lint JSON |
|---|---|---|---|
| StaminaMenuRow | 13330:1178 | menurow_v7 / _tiers2 | StaminaMenuRow_lint.json |
| StaminaShopHeroCard | 13330:1142 | hero_v3 | StaminaShopHeroCard_lint.json |
| StaminaShopInfoCard | 13330:1153 | info_v3 | StaminaShopInfoCard_lint.json |
| StaminaShopMenuPanel | 13330:1170 | panel_tall | StaminaShopMenuPanel_lint.json |
| StaminaShopCancelButton | 13330:1305 | (in panel_tall) | StaminaShopCancelButton_lint.json |
| StaminaShopCard | 13156:1232 | card_v1 | StaminaShopCard_lint.json |
| StaminaShopRegionPill | 13156:1182 | selection_screen_v1 | StaminaShopRegionPill_lint.json |
| StaminaShopPrefecturePill | 13156:1206 | selection_screen_v1 | StaminaShopPrefecturePill_lint.json |
| StaminaShopDetailScreen (assembly) | 13330:1139 | detail_screen_v1 | StaminaShopDetailScreen_lint.json |
| StaminaShopSelectionScreen (assembly) | 13156 frame | selection_screen_v1 | StaminaShopSelectionScreen_lint.json |

## UI fidelity lint
All prefabs re-linted at `fail == 0` — each `Docs/Diagnostics/_capture/<prefab>_lint.json`. The render-health layer caught (and I fixed) real defects during the build: `PillFill` 9-slice collapse on the menu row (also flagged on OPEN NOW), non-uniform hero-photo stretch (fixed via `AspectRatioFitter` EnvelopeParent), and a scrim false-positive (linter refined to skip extreme-aspect gradient sprites + hairline dividers).

## Figma fidelity (per element, diffed against pulled node renders in `reference/nodes/`)
- **Menu row (13330:1178):** row 992×156, image 124×124 r22, tier pill node-exact (S_PillStadium + ppuMult, recolors HIGH/MED/LIGHT), 16px symmetric gaps, BUY r20 (9-sliced) — PASS.
- **Hero (13330:1142):** cover-fit photo, OPEN NOW/FEATURED two-layer pills, ★/📍 Figma-extracted icons, category/name/address — PASS (name faux-bold, accepted).
- **Info card (13330:1153):** 3 cols + 1.5px dividers, header gradient, values, notes, 📍 underline — PASS.
- **Menu panel (13330:1170):** MENU + gold daily-bonus chip + 3 tier rows + empty + CANCEL — PASS.
- **Card (13156:1232):** storefront r32, FEATURED, category/name/tagline, hours+Maps+pin, daily-bonus chip, STA range, navy RP pill, chevron — PASS (name faux-bold).
- **Filter pills (13156:1182/1206):** 8-segment strips w/ dividers, active segment gold-gradient — PASS.

## Clone provenance / element reuse
Every element traces to a concrete reused asset (see the palette list in Summary + per-prefab lint). New Figma-EXPORTED assets (per Cesar's "export, don't crop" rule): `S_ShopHero_BarLounge.jpg`, `S_ShopItem_WhiskyFlight.jpg`, `S_ShopStorefront_Kageroh.jpg` (photos); Cesar-corrected `S_IcoStar.png` / `S_IcoPin.png`. Generated: `S_ScrimBottom.png` (gradient scrim).

## Files modified or created
| Path | What |
|---|---|
| `Assets/Prefabs/UI/Shop/*.prefab` (10) | All shop prefabs (list above) |
| `Assets/Art/Shop/*` (6) | Photos + icons + scrim |
| `Assets/Editor/UIFidelity/UIFidelityLinter.cs` | Detection tool (+ hairline/gradient refinements) |
| `Docs/Scripts/figma_diff.py` | Pixel-diff gate |
| `Assets/Resources/Data/stamina_shops.csv`, `stamina_shop_items.csv` | Seed data (from earlier iters) |
| `Assets/Scripts/UI/Shop/*.cs` | Controllers/models (earlier iters; NOT yet reconciled to new prefabs — see Open items) |
| `Assets/Scenes/ShellScene.unity` | **M = pre-existing killed-agent drift; I did NOT save the scene.** Review/restore before close-out. |

## Open items — RESOLVED
1. **Controller ↔ prefab reconciliation.** DONE. `StaminaShopCard.cs` rewritten to node-exact fields (`_staRangeLabel`/`_rpRangeLabel` derived from `ShopCatalog.GetItemsForShop` min/max, whole-card `_tapButton`); `StaminaMenuRow.cs` gained `_itemImage` + per-tier recolor + auto-width pill. All SerializeFields wired via SerializedObject on the prefabs.
2. **Data binding.** DONE. ShopCatalog (10 shops / 30 items) → cards + menu rows, verified in play mode (see video): 餃子の王将/Royal Host/CoCo/Starbucks/焼肉きんぐ etc. on Selection; 影牢 Signature/山崎 Whisky Flight/自家製ジンジャー on Detail.
3. **Real-flow capture.** DONE. `StaminaShopDemoRecorder` boots ShellScene → Selection → scroll → tap Bar&Lounge (影牢) card → Detail → CANCEL, recorded at 1170×2532 with the persistent top bar + nav overlaid. Captioned demo delivered.
4. **Assets.** DONE. Background = Cesar's `Assets/Art/Shop/Background - Shop.png` on both screens. Storefronts/hero/items: 5 Cesar-provided (kageroh/kamehachi/komeda/ohsho/sushiro) + 3 provided item photos; the other 5 shops use clean category-matched food photos (pasta/dessert/burger/rice) from foodish-api per "internet images for the rest."
5. **ShellScene.** Restored to HEAD earlier (Cesar confirmed no manual edits); shop screens re-mounted in-memory under `ScreensRoot`. Scene NOT yet saved — pending Cesar's go for close-out.

## Residuals (minor, your call)
- **JP name weight** — faux-bold (no true-Bold NotoSansJP SDF); you accepted this on the row.
- **Daily-bonus icon** — I used `IconStaminaSmall` (lightning); Figma uses a recovery-circle icon (imgFrame8). Worth a Figma-export swap in the card + menu-panel chip.
- **Filter-pill spacing** — even-distributed manually; close to Figma space-between but not pixel-exact.
