# SPEC — gacha_screen (Gacha pillar, screen 1 of 3: Gacha main / pull screen)

**Status:** SPEC_READY (decisions locked with Cesar 2026-07-08).
**Tier:** 3 — FULL PIPELINE (new tab content + carousel + real-time countdown + new currency + save-schema change).
**Delivery:** STAGED, prefab-first — **Stage 0 is prefabs only** and gates on Cesar's visual review before any wiring (same flow as `general_shop_ui` / Stores). Cesar hand-tunes prefabs between stages; NEVER overwrite his manual prefab fixes on later stages — read the prefab state first, apply minimal diffs.

**Kickoff (fenced, copy-ready):**

```
Use the implementer subagent on "gacha_screen"
```

---

## 0. What this is

The **GACHA tab** of the shipped Rewards Center hub (Order 610, `GeneralShopScreen`). Order 610
built the 3-tab shell (GACHA | STORE | GIFTS) with STORE live and GACHA/GIFTS grayed. This order
turns the **GACHA tab live**: a swipeable banner carousel with real-time countdowns, ticket-costed
pull buttons (stubbed this phase), a Gacha History entry icon, and a new persisted **Gacha Ticket**
currency surfaced in the persistent top bar on ALL screens.

Two sibling screens follow in later orders (do NOT build them here): **Gacha History**
(`4079:18306`) and **Gacha Prizes/Results** (`13622:2222`).

**Figma source (implementer + reviewers re-pull at step 0 — Lesson AK; token values below are
reconcile-against-node convenience, NOT source of truth):**
- Gacha screen: node **`4065:6730`** (page "Gacha") — render saved at
  `Assets/References/Gacha/gacha_screen_reference_render.png`.
- Banner subtree: `Banner + Buttons` **`4049:10067`** (882×1720) ⊃ `Banner` **`4049:10128`** (882×1424).
- File key: `5gEAHjl6xAtW8iYY7NMvWd`. Figma px = Unity units at 1170×2532 (CanvasScaler Match=0).
- `Docs/Scripts/figma_node_to_spec.py` generates the Rule-21 linter `spec.json` from `4065:6730`
  at implementer step 6e — do NOT hand-author it.

**Art extracted (already in repo):**
- `Assets/References/Gacha/gacha_banner_standard_club_1_art.png` — raw banner art source (1691×2776;
  display 882×1424). Per-banner authored art (promo copy like "MAX POWER" / "GET Drivers, Woods,
  Irons" is intentionally part of the art; the LIVE elements are separate nodes — see §2).
- `Assets/References/Gacha/gacha_banner_sub_art_club.png` — secondary image fill inside the banner comp (212×347).
- Ticket icon: **already in project** — `Assets/Art/Original UI/StoreScreen/S_Store_Ticket_02.png`.

---

## 1. Locked decisions (Cesar, 2026-07-08)

- **D1 — Tab, not screen.** Gacha lives inside `GeneralShopScreen` as the GACHA tab's content panel.
  No new `ScreenId`. Tab switch swaps content panels within the existing prefab.
- **D2 — Pulls are STUBBED this phase.** PULL x1 / PULL x10 buttons exist, styled, pressable →
  stub handler (toast "Coming soon" + `Debug.Log`). No ticket deduction, no roll, no navigation.
- **D3 — New currency: Gacha Tickets.** No existing manager. New `GachaTicketManager` singleton
  (mirror `RewardPointsManager`: `Instance`, `GetTickets()`, `SpendTickets(int)`, `AddTickets(int)`,
  `event Action<int> OnTicketsChanged`) + persisted `int gachaTickets` on `SaveData` (additive
  schema bump per the v2→…→vN convention).
- **D4 — CSV schema (LOCKED, in cross-chat memory):**
  `Assets/Resources/Data/gacha_banners.csv` → `bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active`.
  `gacha_rates.csv` (`bannerId,itemId,rarity,weight`) is a LATER order (real pulls) — do not create it now.
- **D5 — Countdown:** live tick vs **device `DateTime.UtcNow`**; `endUtc` authored as ISO-8601 UTC
  (`2026-08-01T00:00:00Z`). At ≤0 the banner is REMOVED from the carousel (dots re-count, snap to
  nearest). All banners expired/inactive → empty-state placeholder in the carousel area.
- **D6 — Carousel:** one art sprite per banner. Side peeks = the SAME banner instances scaled down
  + darkened as they leave center (falloff both sides; brighten/scale up approaching center).
  **No wrap-around. Snap to center.** Dot indicator per banner, center = active.
- **D7 — Rules & rates:** button opens `rulesUrl` (from CSV) **outside Unity** via `Application.OpenURL`.
- **D8 — Ticket counter in the PERSISTENT top bar, all screens** (Cesar req: "replicated in all
  screens"). Add to `PersistentUIManager`'s `topBarPanel`, bound to `GachaTicketManager.OnTicketsChanged`
  — exactly the RP-pill pattern. **Shop+ button** next to it: present, styled, INERT this phase
  (stub log; later opens ticket purchase).
- **D9 — History icon** (top-left under the top bar) present + pressable → stub (log) until the
  `gacha_history` order ships. **Filter icon** (top-right): Figma has it at 0% visible — OMIT the
  live object (or keep inactive); it must not render or raycast.

---

## 2. Element map (from node `4065:6730`; positions px @1170×2532)

| Element | Figma node | Pos / size | Notes |
|---|---|---|---|
| Top UI (shared) | `4049:9016` | 0,0 1170×313 | RP pill; **Tickets counter** `I4049:9016;2443:2601` @464,141 + digit "999" @547,162; **Shop+** `I4049:9016;2443:2603` @652,160 54×54; title "REWARDS CENTER"; Settings |
| Tab strip | (shell) | y357 | GACHA active gold / STORE / GIFTS — already in `GeneralShopScreen` prefab (`TabBar`; GameObjects still carry clone names `DailyTab/WeeklyTab/MonthlyTab`, TMP labels are GACHA/STORE/GIFTS) |
| History icon | `4146:79147` (`Rankings Container`) | 48,262 75×75 | top-LEFT under top bar → Gacha History (stub, D9) |
| Filter icon | `4146:79148` | 1047,262 75×75, **opacity 0** | OMIT (D9) |
| Banner (center) | `4049:10067` ⊃ `4049:10128` | 144,427 882×1424 (+buttons →1720) | see live sub-nodes below |
| Side peeks | `4055:2111` / `4055:2113` | x−260 / x739, 691×1378 | mock of the falloff — at runtime these are the neighbor BannerCards scaled/darkened (D6), NOT separate art |
| Banner title | `4055:1544` | in `Banner Name + !` `4055:1541` (882×99) | "STANDARD CLUB 1" ← `nameKey` |
| Countdown | `4055:2068` | in `Counter` comp | "ENDS IN: 1d 5h 25m 05 s" ← live tick (D5) |
| Rules & rates | `4055:1528` in `Rates` frame | top-right of banner | "!" button + "RULES & RATES" → OpenURL (D7) |
| Pity block | `Pity` frame: texts `4055:2080`/`4055:2075`, counters `4055:2098`/`4055:2102` | lower banner | "Guaranteed A-rank…/S-rank… in at most [99] pulls" — STATIC display v1 (values authored in prefab; live pity counting comes with real pulls) |
| Prize-preview line | `4055:2089` | 144,1825 | "Common/Uncommon characters or clubs may also be obtained." |
| Cost rows | COST `13618:1562`/`13618:1743` + ticket icons `4049:10358`/`4050:1368` (72×80) + `x1` `4049:10359` / `x10` `4050:1369` | y1899–1909 | ticket sprite = `S_Store_Ticket_02` ← `costX1`/`costX10` |
| PULL x1 / PULL x10 | `4050:1361` / `4050:1400` (`Main Buttons` gold, 387×120) | 186,2003 / 597,2003 | → stub (D2) |
| Dot indicators | `4049:10313–10317` | y2219–2221, 12/16px | 5 in mock; runtime count = live banners. Reuse `Assets/Prefabs/UI/Roster/PaginationDot.prefab` if it matches; else node-exact new atom |
| Nav bar (shared) | `4049:9395` | 0,2269 | untouched |

Everything in the Banner is **live nodes** — the composed `BannerCard` is fully extractable.
Only the backdrop art (promo copy + club renders) is the per-banner sprite.

---

## 3. Architecture

### 3a. Data / currency (no UI)
- **`GachaTicketManager`** — `Assets/Scripts/GachaTicketManager.cs` (or alongside
  `RewardPointsManager`; match its folder + namespace). Persist via the existing `Golfin.Save` host:
  `SaveData.gachaTickets` (int, additive migration; existing saves → 0 unless Cesar sets a starter
  grant — fork #2). Same subscribe/unsubscribe discipline as RP (OnEnable/OnDisable, dedupe on Start).
- **`GachaBannerCatalog`** — `Assets/Scripts/UI/Gacha/GachaBannerModel.cs`, mirroring
  `GeneralShopCatalog` exactly (static, `Resources.Load<TextAsset>("Data/gacha_banners")`, header-skip
  parse, `Reload()` hook). Entry: `BannerId, NameKey, ArtSprite, CostX1, CostX10, EndUtc(DateTime,
  parsed with DateTimeStyles.AdjustToUniversal|AssumeUniversal), RulesUrl, SortOrder, Active`.
  `GetLiveBanners()` = `Active && EndUtc > DateTime.UtcNow`, sorted by `SortOrder`.
  Art loaded `Resources.Load<Sprite>("Art/Gacha/Banners/" + ArtSprite)` — move/copy the banner art
  into `Assets/Resources/Art/Gacha/Banners/` at import time (Resources-loadable, like the card
  templates pattern).

### 3b. Tab routing (inside `GeneralShopScreen`)
- New content root **`GachaTabContent`** as a sibling of the STORE content under `ContentArea`
  (verify exact anchor vs `BarsArea` at step 0 — STORE content paths in
  `GeneralShopScreenController.GridPath` show the current layout).
- New **`GachaTabController`** (`Assets/Scripts/UI/Gacha/GachaTabController.cs`, namespace matching
  the Shop scripts' `GolfinRedux.UI.Shop` convention → `GolfinRedux.UI.Gacha`). Wire the `TabBar`:
  GACHA tab → show `GachaTabContent`, hide STORE content (+ FilterGroup chips row — it is
  STORE-specific); STORE tab → inverse; GIFTS stays grayed/inert. Active-tab styling: reuse the
  gold/white treatment (`ChipGold`/`ChipWhite` pattern) already used for chips; match the Figma
  active-tab look at step 0.
- **Default tab on nav open** — fork #1 (recommend GACHA: the bottom-nav slot IS the gacha icon).

### 3c. BannerCard + carousel
- **`GachaBannerCard.prefab`** (`Assets/Resources/Prefabs/Gacha/`) — the composed banner:
  `ArtImage` (sprite slot) + `Title` + `Countdown` + `RulesButton` + `Pity` block + prize-preview
  line + cost rows + `PullX1Button`/`PullX10Button` (real `Main Buttons` clones — never rebuild).
  Bind script `GachaBannerCard.cs` (mirror `GeneralShopCard`: `Bind(entry)`, `OnPullX1/OnPullX10`
  events, countdown text setter). All bound nodes are separate GameObjects so Cesar can nudge them.
- **`GachaCarouselController`** — horizontal drag/swipe, snap-to-center, no wrap. Per-frame falloff
  by distance-from-center `t = clamp01(|xCard−xCenter| / spacing)`: `scale = lerp(1.0, sideScale, t)`,
  brightness tint `color = lerp(white, sideTint, t)` on a card-level CanvasGroup/Image tint.
  Author `sideScale`/`sideTint` as serialized fields with defaults measured from the mock
  (peek 691/882 ≈ **0.78 scale**; tint ~55–60% gray — reconcile at step 0, then Cesar tunes in
  Inspector). Reuse the ball/club carousel drag/snap logic if it fits (`ClubCarouselController`
  family); clone-and-modify, don't inherit awkwardly.
- **Countdown driver** — one `Update`-driven ticker on the controller (not per-card coroutines):
  formats `ENDS IN: {d}d {h}h {m}m {ss} s`; on expiry calls `RemoveBanner(card)` → rebuild dots,
  snap to nearest live banner; zero live banners → `EmptyState` object ("No active banners" — final
  copy fork #3).

### 3d. Top-bar ticket counter (persistent, all screens)
- Extend the persistent top bar (the `topBarPanel` object `PersistentUIManager` controls):
  `TicketIcon` (`S_Store_Ticket_02`) + `TicketCountText` + `ShopPlusButton` at the Figma Top-UI
  geometry (icon @464,141 76×81; digits @547,162; + @652,160 54×54 — reconcile vs the LIVE top-bar
  prefab, which is NOT the Figma Top UI instance; place relative to the existing RP pill spacing).
- `PersistentUIManager`: add `ticketCountText` + `shopPlusButton` refs; subscribe
  `GachaTicketManager.OnTicketsChanged` with the same double-subscribe guard as RP; `shopPlusButton`
  → stub log (D8).

### 3e. Stubs (this phase)
`PULL x1` / `PULL x10` → `ToastController.Instance?.Show("Coming soon")` + log.
`HistoryButton` → log. `ShopPlusButton` → log. No ticket spend anywhere.

---

## 4. Stages (each gates on Cesar)

**Stage 0 — PREFABS ONLY (visual gate).** `GachaBannerCard.prefab` (art bound to the extracted
sprite, all live overlays as editable nodes at node-exact geometry), `GachaTabContent` panel with
one centered card + two side clones posed at the falloff values + 5 dots + history icon, ticket
counter + Shop+ added to the persistent top bar. NO controllers beyond static posing. Screenshot
gate → Cesar fixes small things by hand.

**Stage 1 — Tab + currency.** `GachaTabController` routing (GACHA/STORE swap, GIFTS inert),
`GachaTicketManager` + `SaveData.gachaTickets` + migration + top-bar binding live on all screens,
history/Shop+/pull stubs wired. EditMode tests green (§6).

**Stage 2 — CSV + carousel + countdown.** `gacha_banners.csv` + catalog loader, cards spawned from
`GetLiveBanners()`, swipe/snap/falloff live, dots dynamic, countdown ticking, expiry removal +
empty state, Rules&rates → `Application.OpenURL(rulesUrl)`.

**Stage 3 — polish gate.** Bot-video capture of: open GACHA tab → swipe across banners → countdown
visibly ticking → tap Rules (URL attempt logged in editor) → tap PULL x10 (stub toast). Cesar accept.

---

## 5. Element Reuse Map (Rule 22)

| Element | Reuse |
|---|---|
| 3-tab shell, TabBar, screen registration | `GeneralShopScreen.prefab` + `ScreenId.GeneralShop` (extend, don't fork) |
| CSV catalog pattern | `GeneralShopModel.cs` / `GeneralShopCatalog` (mirror) |
| Currency manager pattern | `RewardPointsManager` (mirror for tickets) |
| Gold PULL buttons | `Main Buttons` gold — same clone base as `GeneralShopCard` BUY |
| Ticket icon | `Assets/Art/Original UI/StoreScreen/S_Store_Ticket_02.png` |
| Dot indicators | `Assets/Prefabs/UI/Roster/PaginationDot.prefab` (verify vs node; else node-exact atom) |
| History icon chip | `Rankings Container` sprite family (in project from Rankings) |
| Carousel drag/snap | `ClubCarouselController` family (clone-and-modify) |
| Toast | `ToastController` |
| Save/migration | `SaveData` + `SaveSchemaMigrator` additive convention |

Rule 21 applies: no null-sprite Images, node-exact geometry, `figma_node_to_spec.py` → linter.
Verify the Figma→TMP divisor per Lesson AK — do NOT assume ÷1.4.

---

## 6. Test gate + acceptance

**EditMode:**
- `GachaTicketManager`: add/spend/insufficient; persists + round-trips through save; migration adds
  `gachaTickets` to old saves without data loss.
- `GachaBannerCatalog`: parses the locked columns; `GetLiveBanners()` excludes `active=false` and
  past-`endUtc` rows; sorts by `sortOrder`; malformed rows skipped without throwing.
- Countdown formatter: known deltas → exact strings (incl. <1h and <1m cases); expiry boundary.

**Integration / play:**
- GACHA tab shows carousel; STORE tab unaffected (regression: STORE buy flow still works).
- Ticket counter visible on Home / Roster / Inventory / Rewards Center and updates on
  `AddTickets` (debug hook).
- Swipe: snap-to-center, no wrap, falloff visible on neighbors.
- Banner with `endUtc` 30s in the future disappears at 0 and dots re-count; all-expired shows empty state.
- PULL buttons → toast only, ticket balance unchanged.

---

## 7. Implementer forks (surface to Cesar; do not resolve silently)

1. **Default tab on nav open** — GACHA (recommended; nav slot is the gacha icon) vs STORE (status quo).
2. **Starter ticket balance** — migrated + fresh saves: 0 (recommended) vs a test grant (e.g. 10).
3. **Empty-state copy** — "No active banners" placeholder final text/JP pending.
4. **v1 CSV rows** — only one banner art exists (`STANDARD CLUB 1`). Ship 1 live row + N test rows
   reusing the same art (recommended for carousel testing), or 1 row only?
5. **Figma→TMP divisor** — verify per Lesson AK at step 0.
6. **Pity numbers** — static "99 pulls" from the mock, or author per-banner in prefab? (CSV pity
   columns deliberately deferred to the rates order.)

---

## 8. Out of scope (named so nothing silently drops)

Real pulls / odds / `gacha_rates.csv` • Gacha History screen (`4079:18306`, next order) •
Gacha Prizes/Results screen (`13622:2222` + pull animation) • ticket purchasing via Shop+ •
GIFTS tab • localization wiring (keys named per `nameKey` convention, wiring = `localization_audit` 353).

## 9. Pipeline

Tier 3 FULL PIPELINE (implementer → self-review → reviewer → red-team → Cesar per stage).
Stage 0 hard-gates on Cesar's visual review. Save-schema change = red-team focus (migration).
Bot video is the default verification gate. On completion move to `Docs/Specs/Completed/gacha_screen/`.
