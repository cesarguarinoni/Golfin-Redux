# MEASUREMENTS — gacha_screen (pulled from Figma node 4065:6730, 2026-07-08)

**Authoritative.** Every number here is pulled directly from Figma `get_design_context` on node
`4065:6730` (file key `5gEAHjl6xAtW8iYY7NMvWd`). This OVERRIDES the SPEC §2 token table and all prior
iteration guesses. Full-screen reference render: `reference/gacha_screen_FULL_reference.png`.

## Font divisor = ÷1.3 (Cesar, 2026-07-08). Weights: ONLY SemiBold or Medium — NEVER Bold.
Rubik. Figma px ÷ 1.3 = Unity TMP pt. Applied per element below. `tracking` = letterSpacing (Figma px).

| Element | Figma px | Weight | Unity pt (÷1.3) | Node |
|---|---|---|---|---|
| STANDARD CLUB 1 (banner title) | 60 | SemiBold | **46.2** | 4055:1544 |
| "!" glyph in rules chip | 48 | SemiBold | 36.9 | 4052:490 |
| ENDS IN: … (countdown) | 30 | **Medium** | **23.1** | 4055:2068 |
| RULES & RATES label | 20 | SemiBold | **15.4** | 4055:1528 |
| Guaranteed A/S-rank rows | 30 | **Medium** | **23.1** | 4055:2080 / 4055:2075 |
| "99" (pity counter) | 30 | **Medium** | 23.1 | 4055:2097 / 2101 |
| " pulls" | 20 | SemiBold | 15.4 | 4055:2098 / 2102 |
| Disclaimer (Common/Uncommon…) | 20 | SemiBold | 15.4 | 4055:2089 |
| COST / x1 / x10 | 45 | SemiBold | **34.6** | 13618:1562 / 4049:10359 / 4050:1369 |
| PULL x1 / PULL x10 | 66 | SemiBold | **50.8** | I4050:1361;2180:1003 |
| REWARDS CENTER (title) | 51 | SemiBold | 39.2 | I4049:9016;2443:2607 |
| RP "50000" / Ticket "999" | 39 | SemiBold | **30.0** | 2443:2592 / 2443:2599 |
| Tab labels GACHA/STORE/GIFTS | 30 | **Medium** | 23.1 | 4049:10223 etc |

## Colors
- White text `#FFFFFF`.
- **Navy gradient** (pills, wrap panel, tab bar, rules/history chip base is SILVER — see below): top `#133453` → bottom `#091B33`.
- **Silver gradient** (rules "!" chip, history chip): `#FFFFFF` → `#D1D5DB`(40%) → `#818EA1`, border `#FFFFFF` 1px, radius 8px.
- **Gold gradient** (PULL buttons): `#FCF195`(0.5%) → `#D6AB42`(59.9%) → `#BB7F1D`(99.5%); outer border `#422100` 1px + inner border `#FFE48B` 2px; radius 20px; text `#321506`.
- **Gold tab text** (active GACHA): `#FCF195`→`#D6AB42`→`#BB7F1D`. Inactive STORE/GIFTS: silver `#FFFFFF`→`#D1D5DB`→`#818EA1`.
- **Currency pill bg** (RP + ticket): `#122C47`, border white 1px, rounded-RIGHT 100px only.
- **Shop+**: gold `#FFE680`→`#FFC629`(40%)→`#EFA005`, border `#693D00` 2px + `#FFE48B` 1px, radius 8, 54×54.

## LAYOUT TREE (screen 1170×2532, top-left origin, px = Unity units @ CanvasScaler match 0)

```
Gacha Screen 1170×2532
├─ Background (full-screen rewards bg) + Game Screen Content (backdrop blur 10, bg rgba(0,0,0,0.1), px48, flex-col gap24)
├─ Top UI  y0–313  (node 4049:9016)  ← shared top bar, ticket counter lives here
│   ├─ RP pill (left): #122C47 pill, "50000" 30pt SemiBold, RP coin icon
│   ├─ Ticket Counter (center-right, node 2443:2597, 180×81):
│   │     bg pill #122C47 border white, w138, left42
│   │     "999" 30pt SemiBold white, centered
│   │     ticket icon S_Store_Ticket_02 (74w) at left
│   ├─ Shop+ (node 2443:2603): 54×54 gold gradient, border #693D00 2px, radius8, "+" icon, to the RIGHT of the 999 pill (left188.5 top19)
│   ├─ Settings gear (right, 75×75)
│   └─ "REWARDS CENTER" 39.2pt SemiBold, centered, bottom of top bar
├─ History chip  ABSOLUTE (48, 252)  75×75  (node 4146:79147)
│     = SILVER Rankings chip (reuse) + clock icon 60×60 (reference/figma_history_chip.png)
│     TOP-LEFT, sits just under the currency row / above the tab bar. NO "HISTORY" text label — icon only.
│   (Filter icon 4146:79148 at right = OMIT; it is 0% opacity)
├─ Tab bar  (node 4049:10220)  w1074, navy gradient #133453→#091B33, border 3px white-90%, radius20
│     GACHA (gold gradient text, ACTIVE) | STORE (silver) | GIFTS (silver), each 23.1pt Medium, vertical separators between
│     ← ONLY these 3 tabs. The STORE ALL/POPULAR/OFFERS + ALL/TICKETS/CLUBS rows DO NOT belong on Gacha (Cesar #2).
├─ WRAP PANEL  (node 4049:9123)  w882, navy gradient #133453→#091B33, border 3px rgba(255,255,255,0.9), radius20, pb48
│   └─ Banner + Buttons (node 4049:10067, flex-col gap24, pb24):
│       ├─ Banner (node 4049:10128) — ART FILLS IT, radius20, pt12, flex-col JUSTIFY-BETWEEN (top group pinned top, pity pinned bottom, ALL OVER THE ART):
│       │   ├─ [TOP] group gap10:
│       │   │    ├─ "Banner Name + !" row (h99, gap10, w882):
│       │   │    │    ├─ Name (pt24 px24): "STANDARD CLUB 1" 46.2pt SemiBold white, tracking -1.35. MUST NOT spill past banner left edge — px24 left inset (Cesar #7).
│       │   │    │    └─ "!" (items-end, pt24 px24): SILVER chip 75×75 (reuse Rankings silver chip) + "!" 36.9pt gradient text
│       │   │    └─ Rates row (px24, justify-between):
│       │   │         ├─ LEFT: ENDS IN pill (node 4055:2065) — navy gradient #133453→#091B33, radius50 (9-SLICED), px24 py10:
│       │   │         │        "ENDS IN: 1d 5h 25m 05 s" 23.1pt Medium white
│       │   │         │        ← reuse the TOURNAMENT HOLE SELECTION time pill (Cesar #1)
│       │   │         └─ RIGHT: "RULES & RATES" label 15.4pt SemiBold white, w75, center, 2-line, tracking -1.5
│       │   │                  ← OUTSIDE the "!" button, sits under it, right-aligned (Cesar #6)
│       │   └─ [BOTTOM] Pity group (py2, items-end, gap10) — OVER the banner art's lower (green-field) region (Cesar #7/#8):
│       │        ├─ row gap10:
│       │        │    ├─ Pity Text (flex-1, items-end, 23.1pt Medium white, gap10):
│       │        │    │     "Guaranteed A-rank or higher in at most"
│       │        │    │     "Guaranteed S-rank signal in at most "
│       │        │    └─ Pity Counter (pl12 pr24, gap10): TWO navy pills 158×40 radius50 (reuse Rankings RP pill, Cesar #3):
│       │        │           "99" 23.1pt Medium + " pulls" 15.4pt SemiBold
│       │        └─ Disclaimer: "Common/Uncommon characters or clubs may also be obtained." 15.4pt SemiBold white, w882, center — OVER the art (Cesar #8)
│       ├─ Separator (node 4055:1507) w978, ~2px line — reference/figma_separator.png (Cesar #9: "no separator before buttons" = it's MISSING, ADD it)
│       ├─ Cost row (flex-row gap24, each cell w387, flex-row gap3, items-center, justify-center):
│       │     LEFT cell:  "COST" 34.6pt SemiBold  →  ticket icon S_Store_Ticket_02 (72×80)  →  "x1" 34.6pt SemiBold
│       │     RIGHT cell: "COST" 34.6pt SemiBold  →  ticket icon (72×80)  →  "x10" 34.6pt SemiBold
│       │     ← ORDER is COST → icon → x1 (Cesar #6-costs). Each cost cell is CENTERED OVER its PULL button below it.
│       └─ Buttons (flex-row gap24): TWO gold Main Buttons w387 h120 (reuse real gold BUY button):
│             "PULL x1" / "PULL x10" 50.8pt SemiBold #321506
│             ← 24px gap BETWEEN buttons; ~72px gap from buttons to wrap-panel bottom (panel pb48 + banner+buttons pb24) (Cesar #5)
├─ Carousel dots (node 4049:10312) w978 gap10: 5 dots (12px inactive / 16px active center)
│     ⚠️ CONFLICT: Figma HAS dots; Cesar said "no dots." Following Figma (dots IN) pending Cesar's call — see handoff.
└─ Nav bar (node 2098:7988) bottom, h263 — untouched
```

## Asset export / reuse map (Rule 19 — clone real atoms, don't fabricate)
| Element | Source (reuse/export) |
|---|---|
| ENDS IN pill | Tournament Hole Selection time pill (navy gradient, 9-sliced) — Cesar named it |
| 99-pulls pills (158×40) | Rankings RP pill (navy gradient, 9-sliced) |
| Wrap panel (882, navy, 3px white border) | standard navy content panel used app-wide (shop/roster content panel) |
| Tab bar bg | navy gradient panel, 3px white-90% border, radius20 |
| "!" rules chip (75×75 silver) | Rankings silver chip (reuse) + "!" text |
| History chip (75×75 silver) | Rankings silver chip (reuse) + clock icon `reference/figma_history_chip.png` (export node 4146:78771) |
| PULL x1/x10 (gold) | real gold Main Buttons / GeneralShopCard BUY |
| Ticket + RP pill (#122C47) | RP Amount Container (GUID 25ffeb0c) |
| Ticket icon | `Assets/Art/Original UI/StoreScreen/S_Store_Ticket_02.png` |
| Separator | `reference/figma_separator.png` (export node 4055:1507) |
| Dots | `Assets/Prefabs/UI/Roster/PaginationDot.prefab` (if kept) |

9-sliced sprites REQUIRED for every rounded pill/panel (radius 50 pills, radius 20 panels) so corners
don't collapse (Rule 21 render-health). Set `pixelsPerUnitMultiplier` so corner radius renders correctly.
