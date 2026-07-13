# Stage 2 kickoff (2026-07-13)

**Stage 1 APPROVED + committed** (`bdd3d78c0`). **Stage 0 card layout is Cesar-tuned and FROZEN.**

## Spawn template (IMPORTANT)
The carousel must spawn cards from the **Cesar-tuned card**, not a rebuilt one. The tuned card lives at
`Assets/Resources/Prefabs/Gacha/_GachaCard_CesarTuned.prefab` (and as the 3 static scene instances under
`GachaTabContent`). As part of Stage 2, **consolidate it into the canonical
`Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab`** (make the canonical prefab BE the tuned card),
then delete the temp `_GachaCard_CesarTuned.prefab`. Spawn all runtime cards from the canonical prefab.
Do NOT re-author the card layout — clone the tuned card exactly.

## Fork #3 — empty-state copy: PLACEHOLDER "No active banners" (EN), JP pending.
Cesar to finalize wording/JP later; use the placeholder + a `// TODO: final empty-state copy + JP` marker.
Fork #4 already resolved: 1 real banner (STANDARD CLUB 1) + a few test rows reusing the same art (vary
nameKey/endUtc/sortOrder) + one `active=false` row to prove the filter.

## Stage 2 deliverables (SPEC §3a catalog, §3c carousel/countdown, §4 Stage 2, §6)
- `gacha_banners.csv` at `Assets/Resources/Data/gacha_banners.csv` — columns LOCKED (D4):
  `bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active`. Author 1 live + test rows + 1 inactive.
- `GachaBannerCatalog` (`Assets/Scripts/UI/Gacha/GachaBannerModel.cs`) mirroring GeneralShopCatalog:
  static, `Resources.Load<TextAsset>("Data/gacha_banners")`, header-skip parse, `Reload()`, malformed rows
  skipped without throwing. `GetLiveBanners()` = `Active && EndUtc > UtcNow`, sorted by SortOrder.
  Art via `Resources.Load<Sprite>("Art/Gacha/Banners/" + ArtSprite)`.
- `GachaCarouselController` — spawn one card per live banner from GetLiveBanners(); horizontal drag/swipe,
  snap-to-center, NO wrap; per-frame falloff by distance-from-center (scale lerp 1.0→~0.78, tint white→~55-60%
  gray) as serialized fields (defaults measured Stage 0). Reuse ClubCarouselController drag/snap (clone-and-modify).
  Dot indicators: dynamic count = live banners, center active.
- Countdown driver: ONE Update-driven ticker on the controller (not per-card coroutines); format
  `ENDS IN: {d}d {h}h {m}m {ss} s` vs device UtcNow; on expiry → RemoveBanner(card), rebuild dots, snap to
  nearest live; zero live → EmptyState ("No active banners"). 
- Rules & rates button → `Application.OpenURL(rulesUrl)` (from CSV).
- Bind each spawned card: nameKey→title, costX1/X10→cost rows, artSprite→ArtImage, endUtc→countdown, rulesUrl→button.

## Tests (§6) + acceptance
- EditMode: catalog parses locked columns; GetLiveBanners excludes active=false + past-endUtc; sorts by sortOrder;
  malformed rows skipped. Countdown formatter: known deltas → exact strings (incl <1h, <1m); expiry boundary.
- Play/real-flow: GACHA tab shows carousel from CSV; swipe snap-to-center no-wrap, falloff on neighbors; dots dynamic;
  countdown ticking; a banner with endUtc ~30s out disappears at 0 + dots re-count; all-expired → empty state;
  PULL still toast-only (balance unchanged); STORE tab unaffected (regression).

Tier 3 FULL PIPELINE. Verify via REAL boot → ShowScreen(GeneralShop) → GACHA. Capture 1170x2532.
