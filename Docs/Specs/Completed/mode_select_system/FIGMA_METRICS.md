# Figma metrics — pre-pulled reference (`mode_select_system`)

**Pulled live from Figma MCP on the architect main thread, 2026-06-04** (file key `5gEAHjl6xAtW8iYY7NMvWd`, auth: Cesar Guarinoni). This exists because the implementer subagent's context could not reach the Figma MCP last run (HEARTBEAT 05:45Z). **Treat this file as the binding fidelity source.** If you need a sub-element not captured here, ask the architect main thread to pull the node id live — do NOT guess (RUNTIME_BLUEPRINT §8).

Reference screenshots (in `screenshots/`):
- `figma_13027-5212_home_collapsed.png` — Home carousel, centered card collapsed
- `figma_13027-10471_home_expanded.png` — Home carousel, centered card expanded
- `figma_13026-1924_fullscreen_modeselect.png` — Full-screen vertical Mode Select

Conversion (RUNTIME_BLUEPRINT §1/§7): Canvas 1170×2532, Match=0 → **1 Figma px = 1 Unity unit**. **Unity TMP fontSize = Figma px ÷ 1.4.**

---

## Typography — IMPORTANT CORRECTION

Per the live Figma styles, **every text element is `Rubik SemiBold`** — there is **no regular/bold mix** (supersedes the SPEC's "some bold, some regular" assumption). The variation is **size only**.

> ⚠️ **Figma "SemiBold" ≠ Unity 1:1 (Cesar 2026-06-04).** Use the **`Rubik-VariableFont_wght SDF` variable font and TUNE the weight axis to visually match the reference screenshots** — do not trust a fixed weight 600 or a baked SemiBold face to render identically. The ÷1.4 sizes below are a STARTING point; eyeball each against `screenshots/figma_*.png` and nudge weight/size until it matches.

> **Explanation/subtitle text = one auto-sizing element for now (Cesar 2026-06-04).** The tagline (collapsed) and description (expanded) are served by a **single TMP element with auto-size enabled** that swaps its string and auto-fits; cap max near the Figma-converted size, let it shrink for the long copy. Two separate collapsed/expanded text objects come in a future task.

| Style token | Used for | Figma px | **Unity TMP (÷1.4)** | lineHeight | letterSpacing | Color |
|---|---|---|---|---|---|---|
| `EN/Subhead` | **Card title** (PRACTICE/MULTIPLAYER/DRIVING RANGE/MISSIONS); full-screen **description** body | 45 | **32.14** | 60 | −0.69 | title `#EEDC9A` (gold "Mission Font") · desc `#FFFFFF` |
| `EN/Footnote` | **tagline/subtitle** (e.g. "1 vs 1 Match"); **ENTRY FEE** / **REWARDS** labels; **reward amount** (x100/x200); home-card **description** body | 39 | **27.86** | 54 | −0.24 | `#FFFFFF` |
| `EN/Title_2` | **PLAY** button label | 66 | **47.14** | 84 | −0.78 | `#321506` |

> PLAY button is a **prefab-wins** element (SPEC) — keep the existing gold-button prefab's own typography; the 66px row is recorded for reference only, do not re-measure it onto the prefab.

### Colors
| Name | Hex | Use |
|---|---|---|
| Mission Font (gold) | `#EEDC9A` | card title text |
| White | `#FFFFFF` | tagline, description, ENTRY FEE/REWARDS, amounts |
| Card gradient top | `#133453` | Mission Card Container bg (top) |
| Card gradient bottom | `#091B33` | Mission Card Container bg (bottom) |
| Card border | `#FFFFFF` 3px solid | card outer border |
| Pop-Up inner border | `#0A1D35` 1px | inner pop-up |
| PLAY text | `#321506` | (prefab-wins) |
| Insufficient-RP fee | `#C04000` | SPEC `spDepletedColor` — ENTRY FEE when `fee>0 && !CanAfford` |

### Card chrome (Mission Card Container)
- Vertical gradient `#133453` → `#091B33`; **3px solid white** border; corner radius **50px**; drop shadow `0px 10px 10px rgba(0,0,0,0.4)`.
- Inner "Pop-Up": border `#0A1D35`, padding-top 24, radius 50.
- Coin reward icon: **42×42** (Figma "Icons/Coin"), 6px gap to the amount.

---

## Surface 2 — Full-screen Mode Select (frame `13026:1924`) — PRIMARY

Layout (absolute, within the 1170×2532 canvas):
- Top UI: y0, h313 (reused, untouched).
- Content Container: x48 y337 w1074 h1908. Cards Container: x96 (48+48) y347 (337+10) w978-wide cards.
- **Card width = 978**, left x = 96. **Vertical gap between cards = 24px.** Scrollbar x1090 y519 w19 h1690. Side ‹ › arrows: left x37 y621, right x1133 y561, each 30×60.

| # | Mode | Node id | Card y (abs) | w×h | Content shown |
|---|---|---|---|---|---|
| 1 | HOLE PRACTICE (collapsed) | `13027:3341` | 329 | 978×268 | title + tagline + 1 row (ENTRY FEE · x100) |
| 2 | MULTIPLAYER (expanded) | `13026:2366` | 621 | 978×968 | title + "1 vs 1 Match" + description¶ + ENTRY FEE + REWARDS + PLAY |
| 3 | DRIVING RANGE (locked) | `13027:4158` | 1613 | 978×268 | title + tagline + ENTRY FEE row |
| 4 | MISSIONS (locked) | `13027:4277` | 1905 | 978×348 | title + tagline + **NO ENTRY FEE** + REWARDS (x200 average) |

Card internal anatomy (978-wide):
- **Mission Title** block: full width, pt24 / pb16, 10px gap. Title text h60 (gold 45). Tagline row h54 (white 39), centered.
- **Separator** line at y164 (full width).
- **Mission Content Container**: py24. Each row h56: `ENTRY FEE`/`REWARDS` label (white 39) + coin icon 42 + amount (white 39). Collapsed = 1 row (h104). Missions = 2 rows (h184). Expanded MULTIPLAYER content = description¶ (white **45**) + separator + ENTRY FEE + REWARDS + separator (h660).
- **PLAY** (expanded only): Buttons Container `13026:2416`, rel x309.5 y824, w359 h144; button height 120 (prefab-wins).
- **NO ENTRY FEE** treatment (Missions): single label, width 270 (`13027:4374`), replaces the ENTRY FEE amount — drive from `entryFee==0`.

---

## Surface 1 — Home carousel (frames `13027:5212` collapsed / `13027:10471` expanded)

- Content Container: x96 y361 w978 h1860.
- **Cross-Promotion banner** (prefab-wins, just reposition): `13027:5229` rel x4 y1608 w**970**×**252** → abs x100 y1969. Same in both frames.
- Carousel side ‹ › arrows: row at y≈1775, left arrow x40 right arrow x1116, each 30×60 (assets `13027:10222`/`10223`).
- The carousel row is wider than the screen (cards peek at edges). Center card is the only one with PLAY.

| State | Center card node | Center w×h | Side (peek) card node | Side w×h | Center content |
|---|---|---|---|---|---|
| Collapsed | `13027:5862` | **556×484** | `13027:5759` / `13027:5967` | 677×268 | title + subtitle + ENTRY FEE + REWARDS + PLAY (no description) |
| Expanded | `13027:10866` | **764×822** | `13027:10492` / `13027:10584` | 677×268 | adds the description¶ above ENTRY FEE/REWARDS |

> ✅ RESOLVED (Cesar 2026-06-04): the collapsed center card (556w) is **intentionally narrower than the expanded center card (764w)** — collapsed < expanded by design. Use the Figma widths as-is. (Side peek cards stay 677×268.)

Home card typography/colors are the **same component styles** as the full-screen table above (title gold 45, labels/amounts white 39). Home **expanded description** is white **39** (vs full-screen description white **45**) — measured separately per SPEC.

---

## Node-id index (for live re-pulls if needed)
- Home collapsed frame: `13027:5212` · center card `13027:5862` · banner `13027:5229`
- Home expanded frame: `13027:10471` · center card `13027:10866`
- Full-screen frame: `13026:1924` · cards `13027:3341` / `13026:2366` / `13027:4158` / `13027:4277`
- Shared sub-components: Mission Title, Separator, Mission Content Container, Rewards Container, Buttons Container/Main Buttons (PLAY), Icons/Coin (42px), Side Arrow.
