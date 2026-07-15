# SPEC — gacha_history (Gacha pillar, screen 2 of 3: Gacha History / pull log)

**Status:** SPEC_READY (geometry transcribed from live node 2026-07-08; forks in §8 open).
**Notion:** TBD — file under Phase 07. Gacha (gacha_screen precedent = Order 704). Confirm with Cesar before creating.
**Tier:** 3 — FULL PIPELINE (new screen + scroll list + BagClubCard clone rebind + history data store).
**Delivery:** STAGED, prefab-first, HUMAN-GATED — same flow as `gacha_screen` / `general_shop_ui`. **Stage 0 is prefabs only.** **HARD STOP between every stage:** implementer completes a stage, surfaces a screenshot + one-line "what changed", and WAITS. No stage starts until Cesar says go. Cesar hand-tunes prefabs between stages; later stages read the LIVE prefab and apply minimal diffs — NEVER rebuild or overwrite his manual fixes.

**Kickoff (fenced, copy-ready):**

```
Use the implementer subagent on "gacha_history"
```

---

## 0. What this is

The **Gacha History** screen — a scrollable log of past gacha pulls, opened from the History icon on
the gacha main screen (that icon is a STUB in Order 704; this order lights it up). Each row shows the
pulled reward as a **Bags-style club card** (left) + pull metadata (middle) + currency spent (right),
inside the shipped Rewards Center shell (Rewards bg, frosted blur, shared top bar, tab strip, navbar).

**Figma source — single source of truth for ALL geometry:** node **`4079:18306`** ("Gacha Hitstory
Screen", page Gacha), file key `5gEAHjl6xAtW8iYY7NMvWd`. 1170×2532 reference; 1 Figma px = 1 Unity
unit at CanvasScaler `1170×2532`, Match=0. History row = node `13622:21105` (`Rankings Card`), 2nd
instance `13625:2333`.

**Depends on:** Order 704 (`gacha_screen`) landing first — this screen REUSES the shared top bar
(incl. its ticket counter, added by 704 D8), the GACHA/STORE/GIFTS tab strip + sub-filter row, and
the History/Filter icon row. Build after 704 is Cesar-accepted so those components exist to reuse.

---

## 1. Application rules (READ FIRST — this is why the last screen drifted)

1. **Every geometry value in §2/§3 is transcribed verbatim from node `4079:18306` via
   `get_design_context`. It is EXACT. Apply it verbatim.** Rounding to "about", eyeballing, or
   "reconcile later" is a **FAIL**, not a style choice. There is no "convenience / not source of
   truth" demotion here — the node IS the source of truth and these numbers ARE the node.
2. **Positions do NOT come from `figma_node_to_spec.py`.** That generator extracts width/height but
   **drops x/y and layout gaps/padding** (confirmed bug). Use it ONLY for the Rule-21 linter
   `spec.json` (sizes/sprites/colors/fonts). **All positions, gaps, and paddings come from THIS
   manifest, applied by hand.** If you need to re-confirm a value, re-pull `4079:18306` with
   `get_design_context` — never infer position from the linter output.
3. **Flex vs absolute:**
   - Where §2 says **"VLayout/HLayout gap N, pad …"**, the element is a Figma auto-layout frame →
     Unity `VerticalLayoutGroup`/`HorizontalLayoutGroup` with `spacing = N`, `padding` = the exact
     px, and the stated child alignment. Set child sizes exactly; let the layout place them.
   - Where §2 gives **"abs x,y w×h"**, the element is absolutely positioned → RectTransform anchor
     min=max=(0,1), pivot (0,1) (top-left), `anchoredPosition = (x, −y)`, `sizeDelta = (w,h)`.
     Confirm this top-left convention against one existing absolute element in a shipped screen at
     step 0 before placing the four absolute nodes (scrollbar, 2 arrows, icon row).
4. **Verify the Figma→TMP font divisor at step 0 (Lesson AK) — do NOT assume ÷1.4.** Font px are in
   §10; the divisor is applied uniformly once confirmed.
5. **Reviewer + red-team check POSITION, not just size/sprite/color.** For every element assert the
   resolved anchoredPosition (abs) or the LayoutGroup spacing/padding (flex) equals this manifest
   within ±2px. A perfect-size, correct-sprite element sitting off-position is a FAIL.

---

## 2. Placement manifest (node `4079:18306`, all px @1170×2532)

Layer order = paint order (later = on top).

| # | Node | Name | Geometry | Layout / notes |
|---|---|---|---|---|
| L1 | `4062:25452` | Backgrounds/Rewards | abs 0,0 1170×2532 | full-bleed Rewards image. **REUSE** (same as gacha_screen). |
| L2 | `4079:18001` | Game Screen Content | abs 0,0 1170×2532 | **frosted blur:** backdrop-blur 10px, bg `rgba(0,0,0,0.1)`, overflow-clip. **VLayout gap 24, pad L/R 48**, align center, justify center. **REUSE** treatment. |
| L2.1 | `4146:78434` | Top UI | 1170×313, shrink-0 | shared Rewards Center top bar (RP pill, ticket counter+Shop+, Settings, "REWARDS CENTER"). **REUSE — identical to gacha_screen.** |
| L2.2 | `4079:18003` | Content Container | flex-1 | **VLayout gap 24, pad T/B 10**, align center. |
| L2.2.a | `4079:18005` → `4079:22871` | Filters block | — | **VLayout gap 12**, align end. Holds the two bars below. |
| — | `4079:22873` | Tab strip | w-1074 | **HLayout** justify-between; navy grad `#133453→#091b33`, border-3 white, rounded-20. Segments **GACHA** (gold-gradient text, active) / STORE / GIFTS (silver text) + 24px vertical-line dividers. **REUSE Shop tab strip.** |
| — | `4079:22895` | Sub-filter | w-1074 h-44 | **HLayout**; navy grad, border-3 white(0.9), rounded-20. Segments **ALL** (gold `#ebd170`, active) / TICKETS / CLUBS / CHARACTERS / BALLS / ITEMS, 20px SemiBold, 24px dividers. **REUSE Shop filter row.** Semantics = fork §8.3. |
| L2.2.b | `4079:18028` | **Main Panel** | flex-1 | **VLayout gap 24, pad T/B 24**; navy grad `#133453→#091b33`, border-3 white, rounded-20. |
| — | `4079:18029` | Header | w-full | **HLayout gap 6**, center. |
| — | `4079:18030` | History chip | 50×50 | rounded-8, drop-shadow, VLayout center pad 10. Child `4079:18033` History Icon 36×36. |
| — | `4079:18036` | Title | — | "GACHA HISTORY" — Rubik SemiBold 39/lh54/-0.24, white, center. |
| — | `4079:18037` | Separator | w-978 h-0 | 2px line img. **REUSE `Divider.prefab`.** |
| — | `4079:18038` | **Cards Container** | flex-1, w-1074 | **VLayout align center, pad L/R 48**, overflow-clip, rounded-20. = **scroll viewport** (Unity ScrollRect content). Holds rows + separators. |
| — | `13622:21105` | **Rankings Card (row)** | w-full | **HLayout gap 24**, align center, **pad 24**, overflow-clip. 3 columns → §3. |
| — | `4079:18059`,`4079:18080` | Separator | w-978 | between rows. **REUSE Divider.** |
| — | `13625:2333` | Rankings Card (row 2) | w-full | same as row 1, MYTHIC variant (gold text, PULLS: 10). |
| — | `4079:18084` | Separator | w-978 | below list. **REUSE Divider.** |
| — | `4079:18085`→`18087`→`18088` | CLOSE button | inner h-120, pad L/R 96 | **Main Buttons SILVER**, rounded-20, border-2 `#f7f8f9`, silver grad. "CLOSE" Rubik SemiBold 66/lh84/-0.78, `#1e293b`. Centered. **REUSE silver close button** (`TournamentCloseButton` / `StaminaShopCancelButton` family). |
| L2.3 | `2098:7988` | NavBarContainer | 1170×263 | shared bottom nav. **REUSE.** |
| L2.4 | `4079:18090` | Arrow L | **abs 7,561 30×60** | rotate 180 (points left). scroll hint. |
| L2.5 | `4079:18091` | Arrow R | **abs 1133,561 30×60** | points right. scroll hint. |
| L3 | `4079:18092` | Scrollbar | **abs 1138,519 19×1502**, opacity 25% | Back `rgba(255,255,255,0.5)` rounded-8 + white Indicator (top portion). = Unity ScrollRect vertical scrollbar. **REUSE.** |
| L4 | `4146:79296` | Filters icon row | **abs 48,252 1074-wide** | **HLayout justify-between, pad T/B 10.** |
| — | `RankingsContainer` (History/disabled) | History icon | 75×75 | **greyed/disabled** (darkened gradient) — you are already IN history. rounded-8, shadow. Child History Icon 60×60. |
| — | `4146:79298` | Filter icon | 75×75 | **all gradients 0-alpha (transparent) → OMIT the live object** (no render, no raycast) — same as gacha_screen D9. |

> The two absolute rows (icon row `4146:79296` at y252, scrollbar/arrows) sit OVER the flex column
> by design — place them last, absolutely, per rule §1.3.

---

## 3. The history row (`13622:21105`) + BagClubCard clone

Row = **HLayout gap 24, pad 24, align center, w-full**, three columns:

**COL 1 — Club card** (`13622:21325`, VLayout, **w-181, shrink-0**):
- Card `13622:21326`: w-181, **h-374**, navy grad `#133453→#091b33`, border-1 white, rounded-20,
  **pad 12**, VLayout center. → **CLONE `Assets/Prefabs/UI/Inventory/BagClubCard.prefab`.**
  It already carries `Rarity`, `Level`, six `Stat` rows, `Distance`, `StatRow_Durability` (verified).
  Do NOT rebuild the rarity frame or stat bars — clone and **rebind** to the pulled club.
  - Rarity frame `13622:21327`: 105×204, border `#f3ecc2`, rounded-8. Rarity bg gradient:
    **RARE** `#0b5d3a` via `#1e9e5a`; **MYTHIC** `#8c6a00` via `#e6b800` (per-rarity — use existing
    `RarityHelper`/rarity-bg mapping, do not hardcode). Club render 116×180; dark bottom overlay;
    Texts: rarity letter (`R`/`M`, rarity-color 20px) + `Lv N` (white 20px) top; club name
    (`DRIVER`/`G&F`, Rubik Medium Italic 30/lh36) bottom.
  - Parameters `13622:21340`: 6 rows, each **HLayout gap 8, h-20**: [icon 20×20] [bar: flex, h-10,
    rounded-20, track `#182430`, blue-grad fill] [value 20px white, w-34]. Row 5 = durability (blue +
    orange split bar). Row 6 = Distance: [icon] [value `180 yd`, w-60], no bar.

> **Polymorphic note (fork §8.4):** history can also list characters/balls/items (see sub-filter).
> For those, COL 1 clones the matching thumbnail: `CharacterThumbnailCard` / `BallThumbnailCard` /
> `ItemThumbnailCard`. **This order builds the CLUB variant only** (matches the mock); other types
> are a follow-up. Row controller picks the clone prefab by reward type.

**COL 2 — Metadata** (`13622:21111` → `User Details Container`, **flex-1**, VLayout **pad L/R 16**;
inner `13622:21112` VLayout **gap 6**). Six lines, all Rubik Medium **33/lh39/0.18** unless noted:
- `DRIVER G&F` — club name, white.
- `RARE - Lv 999` — rarity word in rarity color (`RARE`=`#50c878`, `MYTHIC`=`#ffc107`), `- Lv N` white.
- `PULLED 2025/12/28` — white (pull date).
- `04:12:49 AM` — white (pull time).
- `STANDARD CLUBS 1` — white (banner name).
- `PULLS: 1` — white (this pull's count: 1 for x1, 10 for x10).

**COL 3 — Currency spent** (`13622:21123` `WF Icon Button`, **w-180 h-374**, VLayout **gap 16**,
align center, **pad T/B 4**, rounded-8, overflow-clip):
- `TICKET` label — Rubik Medium 33/lh39, white, center.
- Ticket icon `13622:21124` — `S_Store_Ticket_02`, **145×159**.
(v1 currency is always Gacha Tickets → label + icon fixed.)

---

## 4. Element reuse / clone map (Rule 22 — all grounded)

| Element | Reuse / clone base |
|---|---|
| **Left card (club)** | **CLONE `Assets/Prefabs/UI/Inventory/BagClubCard.prefab`** → rebind. |
| Left card (char/ball/item — later) | `Roster/CharacterThumbnailCard`, `Inventory/BallThumbnailCard`, `Inventory/ItemThumbnailCard`. |
| Rewards bg + frosted blur | same objects as `gacha_screen` (`4062:25452` + `4079:18001`). |
| Top bar (RP + ticket counter + Shop+ + Settings + title) | shared Rewards Center top bar (built/extended by Order 704). |
| Tab strip + sub-filter row | Shop filter bars in `GeneralShopScreen.prefab`. |
| History/Filter icon row | Rankings Container icon family (as in `gacha_screen`; set History = disabled state). |
| Separators (w-978) | `Assets/Prefabs/UI/Divider.prefab`. |
| CLOSE button (silver) | `Assets/Prefabs/UI/Tournaments/TournamentCloseButton.prefab` / `Shop/StaminaShopCancelButton.prefab` family. |
| Scroll list | Unity `ScrollRect` + `VerticalLayoutGroup`; vertical scrollbar `4079:18092`. |
| Ticket icon | `Assets/Art/Original UI/StoreScreen/S_Store_Ticket_02.png`. |
| Rarity bg / caps | `RarityHelper` / `RarityStatCaps` (do NOT duplicate). |

Rule 21 applies: no null-sprite Images, node-exact geometry, `figma_node_to_spec.py` → linter only
(NOT positions, per §1.2).

---

## 5. Architecture

- **Screen vs modal → fork §8.1.** Assume a registered screen (`ScreenId.GachaHistory`) opened from
  the gacha History icon; CLOSE pops back to the gacha tab. Confirm before wiring.
- **`GachaHistoryStore`** — persisted pull log. Record: `RewardType (Club/Char/Ball/Item)`, `RewardId`,
  `Rarity`, `Level`, `NameKey`, `PulledUtc (DateTime)`, `BannerNameKey`, `PullBatchSize (1|10)`,
  `Currency (Ticket)`. Newest-first ordering. Append hook is called by the real-pull system (Order
  after this); **that system does not exist yet** → this order ships with a **mock/sample store**
  (2 rows matching the mock) behind an interface, so the screen is fully testable now and the real
  pull writer swaps in later. (Fork §8.2.)
- **`GachaHistoryScreenController`** — loads records from the store, spawns one `GachaHistoryRow`
  per record into the ScrollRect content (with a Divider between rows), drives the sub-filter
  (§8.3), CLOSE → pop. Namespace `GolfinRedux.UI.Gacha`.
- **`GachaHistoryRow`** — `Bind(record)`: picks + clones the reward card prefab (club → BagClubCard),
  rebinds rarity/level/name/stats; fills the 6 metadata lines; sets ticket label/icon. Event-driven,
  OnEnable/OnDisable discipline.

---

## 6. Stages (each hard-gates on Cesar)

**Stage 0 — PREFABS ONLY (visual gate).** `GachaHistoryRow.prefab` = cloned BagClubCard (COL1) +
metadata column (COL2) + ticket column (COL3) at node-exact geometry; History screen shell (bg +
blur + reused top bar + tab/sub-filter bars + main panel + header + 2 sample rows + separators +
silver CLOSE + navbar + scrollbar + icon row) statically posed. NO controllers. Screenshot gate →
Cesar hand-tunes.

**Stage 1 — Screen + store.** Register screen (per §8.1), `GachaHistoryStore` + mock records,
`GachaHistoryScreenController` spawning rows from the store, CLOSE wired, gacha History icon → open
this screen. EditMode tests green (§7).

**Stage 2 — Scroll + filter + polish.** ScrollRect live with scrollbar; sub-filter switches the
visible reward type (§8.3); empty state when a filter has no rows; row recycle if the list is long.

**Stage 3 — polish gate.** Bot-video: open History from gacha → scroll → switch a sub-filter → CLOSE
back to gacha. Cesar accept.

---

## 7. Test gate + acceptance

**EditMode:** store round-trips through save; newest-first ordering; filter predicate per reward
type; row `Bind` maps record → card/metadata/ticket without throwing on each rarity.
**Integration/play:** History opens from gacha icon; club card renders via BagClubCard clone with
correct rarity bg + stats; scroll works; sub-filter narrows list; empty filter → empty state; CLOSE
returns to the gacha tab; top bar / navbar unaffected.
Bot video is the default verification gate.

---

## 8. Forks (surface to Cesar; do not resolve silently)

1. **Screen or modal?** Full `ScreenId.GachaHistory` (has bg + top bar + navbar → leans screen) vs
   `ModalController` overlay (has a CLOSE button → leans modal). Recommend **screen**.
2. **Data source this order:** mock store only (recommended — real pulls don't exist yet) vs defer
   the whole screen until the real-pull/history-writer order.
3. **Sub-filter semantics:** does ALL/TICKETS/CLUBS/CHARACTERS/BALLS/ITEMS filter history rows by
   reward type? What does **TICKETS** filter here (tickets aren't pulled)? It is reused verbatim from
   Shop — confirm which segments are live vs hidden on History.
4. **Non-club reward rows:** build only the club card now (matches mock), or also the
   character/ball/item card variants this order?
5. **Figma→TMP divisor** — verify at step 0 (Lesson AK).

---

## 9. Out of scope

Real pulls / the history-writer hook (separate order) • Gacha main screen `4065:6730` (Order 704) •
Gacha Prizes/Results `13622:2222` • localization wiring (keys per `nameKey`; wiring = 353) •
non-club card variants unless fork §8.4 says otherwise.

---

## 10. Design tokens (from node `4079:18306`)

- White `#FFFFFF`; Grey30 `#B2B2B2`; Game_Dark_Blue `#001E39`; close-text `#1E293B`.
- Navy panel grad `#133453 → #091b33`; panel/tab border white (3px), card border white (1px).
- Rarity frame border `#f3ecc2`; Rare font/bg `#50C878` / `#0b5d3a`·`#1e9e5a`; Mythic
  `#FFC107` / `#8c6a00`·`#e6b800` (use `RarityHelper` mapping).
- Ticket bg `#122c47`; stat bar track `#182430`; sub-filter active gold `#ebd170`.
- **Fonts (verify divisor):** Title2 Rubik SemiBold 66/lh84/-0.78 (CLOSE); Footnote SemiBold
  39/lh54/-0.24 ("GACHA HISTORY"); Headline SemiBold 51/lh66/-1.29 ("REWARDS CENTER"); Caption2
  Medium 33/lh39/0.18 (metadata lines); Caption3 Medium 30/lh36/-0.5 (card club name, italic);
  Caption4 SemiBold 20/lh24/-1.5 (rarity letter, level, stat values, filter labels).

---

## 11. Pipeline

Tier 3 FULL PIPELINE (implementer → self-review → reviewer → red-team → Cesar per stage). Stage 0
hard-gates on Cesar's visual review. **Positional assertions are a required reviewer check** (§1.5).
Bot video is the default verification gate. On completion move to `Docs/Specs/Completed/gacha_history/`.
