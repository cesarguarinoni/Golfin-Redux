# SPEC — gacha_prizes (Gacha pillar, screen 3 of 3: Gacha Prizes / pool preview)

**Status:** SPEC_READY (geometry transcribed from node `13622:2222` 2026-07-16).
**Figma:** node **`13622:2222`** ("Tickets_Gacha" / PRIZES), page Gacha, file key `5gEAHjl6xAtW8iYY7NMvWd`.
1170×2532 reference; 1 Figma px = 1 Unity unit @ CanvasScaler 1170×2532, Match=0. Node render at
`reference/gacha_prizes_node_13622-2222.png`.
**Tier:** 3 — FULL PIPELINE. **Delivery:** STAGED, prefab-first, HARD-GATED (same flow as gacha_screen /
gacha_history). Stage 0 = prefabs/static posing only. HARD STOP between stages; Cesar reviews each.

**Kickoff:** `Use the implementer subagent on "gacha_prizes"`

---

## 0. What this is

The **Gacha Prizes** screen — a pool-preview grid showing the 10 possible prize cards for a banner, a
ticket **COST**, a **PULL x10** button, and a **BACK** button. Reached from the gacha main screen
(screen 1). It renders inside the shared Rewards Center shell (bg + shared top bar + nav bar).

**Scope (Cesar, 2026-07-16):**
- **Static prizes grid + PULL/BACK wired.** Build the grid + cost + buttons; BACK returns to the gacha
  main screen; PULL x10 is a **STUB** this order ("coming soon" / no-op log).
- **Mock prize pool.** Cards come from a small mock pool (like gacha_history's mock store). Real
  pulls / odds / ticket spend / reveal animation are **OUT OF SCOPE** (deferred: `gacha_rates` order +
  a `gacha_pull_animation` order).

**Depends on / REUSES (this is ~80% reuse — do NOT rebuild):**
- **gacha_history** (just shipped) — the prize cards ARE the `GachaHistoryRow` club-card family
  (BagClubCard clone: navy `BackgroundClub.png` base, `ItemsScreen/Rim.png` outline, rarity frame,
  6-row stats block). Clone that card; do NOT rebuild.
- **gacha_screen** — shared Top UI (RP pill + ticket counter + Shop+ + Settings), Nav Bar, blurred
  Rewards bg, the navy MainPanel treatment (`Background - Container` sprite, 3px outline, radius 20),
  and the entry button on the gacha main screen.
- **Buttons:** the "Main Buttons" component family — **PULL x10 = GOLD variant**, **BACK = SILVER
  variant** (the silver one is the `TournamentCloseButton` / `Button - Replay` used for gacha_history's
  CLOSE). Ticket icon = `Assets/Art/Shop/S_Store_Ticket_02.png` (as in gacha_history).

---

## 1. Application rules (READ FIRST)

1. Every geometry value in §2 is transcribed from node `13622:2222` via `get_metadata`. Apply it
   verbatim; re-pull `13622:2222` with `get_design_context` at step 0 (Rule 9) to confirm px/font/sprite.
2. **Positions come from THIS manifest.** Use `figma_node_to_spec.py` only for the Rule-21 linter
   spec.json (it drops x/y).
3. **Divisor = ÷1** (Figma px = TMP pt) — same as gacha_history (proven by the shared card/button atoms).
   Confirm at step 0 (Lesson AK).
4. **REUSE, do not rebuild.** Every card/button/bar/icon must clone the real atom (Rule 19 provenance
   table with live `Image.sprite` read-back). If a mandated source can't be found → `IMPLEMENTER_BLOCKED`
   and surface — never hand-roll.
5. Reviewer + red-team check POSITION, not just size/sprite/colour (±2px). VERIFY AT RUNTIME
   (`GetWorldCorners`), never trust `LayoutElement.preferred*` values — they can be ignored by the
   layout and lie (the gacha_history scar: box-model 24/24 hid a rendered 42/6).

---

## 2. Placement manifest (node `13622:2222`, px @1170×2532)

| # | Node | Name | Geometry | Notes |
|---|---|---|---|---|
| L1 | `13622:2223` | Backgrounds | abs 0,0 1170×2532 | blurred Rewards bg. **REUSE** (same as gacha_history). |
| L2 | `13622:2256` | Top UI | abs 0,0 1170×313 | shared top bar (RP pill, ticket counter, Shop+, Settings). **REUSE — identical to gacha_history.** Includes the **"PRIZES"** title. |
| L3 | `13622:2224` | **Content Container** | abs 96,466 978×1670 | the **navy MainPanel** (reuse `Background - Container`, 3px outline, radius 20 — same as gacha_history MainPanel). Holds grid + cost + buttons, centered (96 L/R margin). |
| — | `13622:18302` | Prize row 1 | 978×374 @ y=42 (in panel) | **HLayout, 4 cards.** cards at x=91/296/501/706, each **181×374**, gap **24**. |
| — | `13622:19098` | Prize row 2 | 978×374 @ y=440 | 4 cards, same x's. Row pitch = 398 (374 + **24** gap). |
| — | `13622:19496` | Prize row 3 | 978×374 @ y=838 | **2 cards, centered** — x=296 and x=501. |
| — | `13622:21103` | **Separator** | 978×0 @ y=1236 | full-panel-width thin line BETWEEN the grid (ends ~y1212) and the COST row (y1260). **REUSE `Divider.prefab`** (same as gacha_history). |
| — | (each card) | **Prize card** | 181×374 | **CLONE the gacha_history `GachaHistoryRow` club card** (`Frame 15` 181×374 → rarity frame `Frame 30` 105×204 → stats `Frame 29` 157×120, `Bar` 87×10). Rebind per prize; rarities vary (Common/Rare/Mythic per the render: silver/blue/green/gold frames). |
| — | `13622:2246` | **COST row** | abs 295.5,1260 387×80 | HLayout, center. `COST` text (x57 118×60) + ticket icon `S_Store_Ticket_02` (x178 72×80) + `x10` text (x253 77×60). |
| — | `13622:2250` | **PULL x10 button** | abs 295,1364 388×120 | **GOLD Main Buttons** variant. Label "PULL x10". STUB onClick this order. Add `ButtonPressFeedback`. |
| — | `13622:2251` | **BACK button** | abs 353,1508 272×120 | **SILVER Main Buttons** (= `TournamentCloseButton` family). Label "BACK". onClick → back to gacha main screen. Add `ButtonPressFeedback`. |
| L4 | `13622:2257` | Nav Bar Container | abs 0,2269 1170×263 | shared bottom nav. **REUSE.** |

> **Panel internal layout (VLayout) — from the node, VERIFY AT RUNTIME (GetWorldCorners), not LayoutElement:**
> panel is 1670 tall; **top padding = 42** (row1 at y42), **bottom padding = 42** (BACK ends y1628 → 1670),
> **spacing = 24** between EVERY element (row1→row2→row3→Separator→COST→PULL→BACK all 24; cards within a row
> also 24 horizontal). Top gap MUST equal bottom gap (42). Report measured per-gap numbers.

---

## 3. Reuse / clone map (Rule 22 — ground every atom before building)

| Element | Clone from | Source |
|---|---|---|
| Prize card (×10) | `GachaHistoryRow.prefab` club card (`Col1_ClubCard` subtree) | `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab` |
| Navy panel | `Background - Container` | as gacha_history MainPanel (cite GUID off the live object) |
| Top bar | Rewards Center Top UI | shared, from gacha_screen/gacha_history |
| Nav bar | Nav Bar Container | shared |
| Blurred bg | Rewards blurred bg | as gacha_history L1 |
| PULL x10 button | GOLD "Main Buttons" | find the gold button atom (gacha_screen cost/pull buttons are gold — cite the prefab/sprite) |
| BACK button | SILVER "Main Buttons" = `TournamentCloseButton.prefab` | `Assets/Prefabs/UI/Tournaments/TournamentCloseButton.prefab` |
| Ticket icon | `S_Store_Ticket_02` | `Assets/Art/Shop/S_Store_Ticket_02.png` |

---

## 4. Architecture

- `ScreenId.GachaPrizes` (new) + `_gachaPrizesScreen` SerializeField in `ScreenManager`; register +
  activate; include in `isMenuScreen` + `showBars` (nav + bg bars stay visible, per the render).
- `GachaPrizesScreen.prefab` — the screen shell. **NO scrolling:** the content is a plain static
  `VerticalLayoutGroup` — NO ScrollRect, NO Viewport, NO Scrollbar. All 10 cards + separator + COST +
  PULL + BACK fit inside the 1670-tall panel (unlike gacha_history's scroll list).
- Mock pool: a small `GachaPrizePool` of 10 prize records → controller spawns 10 prize cards (reusing
  the club-card bind). Mirror gacha_history's mock-store pattern; do NOT persist to save.
- Entry: the gacha main screen has a pull/prizes entry point (confirm the exact button in the gacha tab
  at step 0 — likely the stubbed PULL area). Wire it → `ShowScreen(GachaPrizes)`. BACK → `ShowScreen(GeneralShop)`
  (gacha tab). PULL x10 = stub (`Debug.Log`, "coming soon").

---

## 5. Stages (each hard-gates on Cesar)

**Stage 0 — PREFABS ONLY.** `GachaPrizesScreen.prefab` statically posed: bg + blur + reused top bar +
navy panel + 10 prize cards (cloned club card, static mock rarities matching the render's
silver/blue/green/gold) + COST row + gold PULL x10 + silver BACK + nav. NO controllers. Screenshot gate.

**Stage 1 — Screen + wiring.** Register `ScreenId.GachaPrizes`; `GachaPrizesScreenController` spawns the
10 cards from the mock pool; entry from the gacha main screen wired; BACK → gacha tab; PULL x10 stub.
EditMode tests green.

---

## 6. Test gate + acceptance

**EditMode:** mock pool yields 10 records; controller binds each → card without throwing across rarities.
**Integration/play (REAL user flow — drive past PLAY, navigate):** Prizes opens from the gacha entry;
10 prize cards render via the cloned club card with correct rarity frames + stats; COST shows ticket
+ x10; PULL x10 is a visible stub; BACK returns to the gacha tab; top bar / nav unaffected.
**Capture via the `screenshot-game-view` MCP tool** (CLAUDE.md Capture Rule 0 — never hand-roll), 1170×2532.

## 7. Figma fidelity (fill per element vs node `13622:2222`; reference/ render is ground truth)

| Element | Node | Built | Verdict |
|---|---|---|---|
| Prize card size | 181×374 | | |
| Card grid gaps | 24px | | |
| Grid rows (4/4/2) | 3 rows, 10 cards | | |
| COST row | COST + ticket + x10, 387×80 | | |
| PULL x10 button | gold, 388×120 | | |
| BACK button | silver, 272×120 | | |
| Top bar / nav / bg | reused | | |
| "PRIZES" title | weight + size vs node | | |

(Reviewers: font WEIGHT + rendered-size-vs-reference for every text element — standing rule.)

## 8. Out of scope (named so nothing drops)

Real pull execution + ticket spend • odds / `gacha_rates.csv` • pull REVEAL animation (node's "01"
sparkle group `13622:2252` is decorative; the animated reveal is a separate order) • real prize-pool
wiring • localization wiring.

## 9. Pipeline

Tier 3 FULL PIPELINE (implementer → self-review → reviewer → red-team → Cesar per stage). On completion
move to `Docs/Specs/Completed/gacha_prizes/`.
