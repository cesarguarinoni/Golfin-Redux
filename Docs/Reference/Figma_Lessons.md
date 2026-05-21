# Docs/Reference/Figma_Lessons.md

> **READ THIS BEFORE ANY FIGMA WORK.**
> Last updated: 2026-05-21 JST
> Project: GolfinRedux — file key `5gEAHjl6xAtW8iYY7NMvWd`

---

## A. Critical sandbox constraints

The Figma MCP plugin sandbox has **no outbound network access**:

| Probe | Result |
|---|---|
| `fetch()` | `ReferenceError: 'fetch' is not defined` |
| `XMLHttpRequest` | not defined |
| `figma.createImageAsync(url)` | exists but blocked: `"createImageAsync" is not a supported API` |
| `figma.createImage(bytes)` | **works** — accepts a `Uint8Array` |
| Globals available | `atob`, `btoa` only |

**To get external images into Figma:** delegate to Claude Code (has bash/curl), OR have the user drop images on the Figma page directly. Never claim you'll fetch in the next turn — probe first.

## B. Cloning gotcha

`figma.setCurrentPageAsync(page)` only affects **new node creation**. When you `node.clone()`, the clone parents to the **original's current page**, not the page you set as current. **Always explicitly `targetPage.appendChild(clone)` after cloning across pages.**

## C. Cesar's canonical menu item card (v4.4)

This is the locked-in canonical structure. Mimic it everywhere.


```
Card: 994w × 160h, cornerRadius 32, drop shadow
├─ Inner padding: 16 L / 16 R / 10 T / 10 B (8px grid base)
├─ Container around all items: paddingLeft=16, paddingRight=16, gap=14
└─ Children:
   ├─ menu_<slug> thumb (124×124 default, OR 180×160 full-left-bleed for Store-style)
   ├─ Frame 10 (middle column, 500×130): Tier Badge → Name → Description → STA row
   └─ Frame 9 (right column, 215×121): RP Container (215×56) + BUY (215×56), 9-10px stacked gap
```



**Both RP container and BUY button = 215w × 56h.** Same width is a hard consistency requirement.

For Store-style detail variant (v5 Detail): thumb 180w × 160h at (0,0), middle col shifts to x=196.

## D. Main Buttons component sizing

| Variant | Component ID | Use | Resize to |
|---|---|---|---|
| Gold Small | `2541:11883` | BUY / VIEW | 215×56 (matches RP pill) |
| Silver | `2182:5458` | CANCEL / CLOSE | **360×120** (NOT 80, NOT 180 alone — the Button Container is 180h but the visual sweet spot is 120) |
| Gold (Large) | `2180:1006` | Primary CTA | 360×96 default |

**Always use `.resize(w, h)`, never `.rescale()`** — rescale distorts internal children. Override label via the inner TEXT node named `PRIMARY` or `SECONDARY` (uppercase), after `loadFontAsync`.

## E. Auto-layout pattern for menu panels


```
Panel: VERTICAL auto-layout
  itemSpacing = 24
  padding = 24 all sides
  counterAxisAlignItems = CENTER
Children:
  Header        — layoutAlign = STRETCH
  Scroll Wrap   — layoutAlign = STRETCH, layoutGrow = 1, clipsContent = true
  Separator     — layoutAlign = STRETCH (LINE clone of 12885:88523)
  Cancel        — layoutAlign = INHERIT (centers via counterAxisAlignItems)
```



`layoutGrow=1` on the scroll wrap means it fills remaining vertical space → Separator + Cancel always pin to bottom regardless of panel height. No manual y-positioning needed.

## F. Store-style image (full-left-bleed)

For cards where the image fills the entire left side:

1. Thumb at **x=0, y=0**
2. Resize to **fill the full card height** (no top/bottom gap)
3. `cornerRadius = 0` on thumb
4. `strokes = []` on thumb
5. Card has `clipsContent = true` + `cornerRadius` → rounds image edges naturally
6. **REMOVE any inner border Frame** (would draw a 1px line over the full-bleed image)

Width depends on context: 204w for selection (978×274), 260w for v4.2-style (978×360), 180w for menu items (994×160).

## G. Cards Container with consistent item width

To make all items match a width:
- Set container `paddingLeft = paddingRight = 16`
- Set each item `layoutAlign = "STRETCH"`
- Items auto-resize to (container_width - padding)
- Absolute-positioned children inside items stay put — only the card frame resizes

## H. Component / style IDs (memorize these — frequently used)


```
Backgrounds:
  Missions      340:2168
  Inventory     10550:47467
  Rewards       4062:25452     (Store-style)
Top UI          2110:9124      (override TEXT "Title Text" or /title text/i)
Nav Bar         2098:7988
Scrollbar       4002:4157
Pagination arrows 325:2287     (rotation 180 for left)

RP Container clone   4003:9102    (250×65, has TEXT named "RP Amount")

Stat icons (Frame 8):
  Strength      2521:12844
  Club Control  2521:13275
  Recovery      2521:13295
  Stamina       2521:13315

Separator lines:
  Hole-card divider     12885:88523
  Clubs filter line     4213:61756

Library styles:
  Blue panel fill   "S:c4cfe5b1ea297fa8728576e2191d019ccddf1d70,"
```



## I. Canonical color palette


```js
const GOLD_BRIGHT   = {r: 0.98, g: 0.78, b: 0.30};
const WHITE         = {r: 1,    g: 1,    b: 1};
const MUTED         = {r: 0.78, g: 0.84, b: 0.92};
const NAVY_DARK     = {r: 0.04, g: 0.10, b: 0.19};
const STAMINA_GREEN = {r: 0.45, g: 0.88, b: 0.50};
const BORDER_OUTER  = {r: 0.243,g: 0.486,b: 0.659};  // blue accent
const BORDER_INNER  = {r: 0.039,g: 0.114,b: 0.208};  // dark navy
// Card gradient:
const GRAD_TOP      = {r: 0.075,g: 0.204,b: 0.325};
const GRAD_BOT      = {r: 0.035,g: 0.106,b: 0.200};
```



Gold gradient stops (for active text, badges): `[0.988,0.945,0.584]` → `[0.839,0.671,0.259]` → `[0.62,0.43,0.12]`
Silver gradient stops (for inactive text, MEDIUM tier): `[1,1,1]` → `[0.82,0.84,0.88]` → `[0.51,0.56,0.63]`

## J. Filter strip pattern

2-row pill filter (Region + Prefecture or similar):


```
Pill: HORIZONTAL auto-layout
  fillStyleId = BLUE_STYLE_ID
  primaryAxisAlignItems = "SPACE_BETWEEN"   ← distributes segments evenly
  paddingLeft = 28, paddingRight = 28
  cornerRadius = height / 2
Children alternate:
  Segment Frame (hugs text) → cloned separator LINE → Segment → LINE → ...
```



SPACE_BETWEEN with 0-width LINE clones between segments auto-centers everything.

## K. Workflow rules (Cesar's preferences)

1. **Scout before changing.** Always inspect current state via `use_figma` first. Cesar manually fixes things — his fixes become canonical. Never assume your previous build is current.
2. **Clone-and-swap > rebuild from scratch.** When N items need consistent structure with different data, clone one canonical version and swap text/image fills. Auto-layout handles repositioning.
3. **Mimic, don't re-invent.** When asked to "do the same in version X", scout version X's structure and replicate exactly — don't approximate from memory.
4. **8px grid.** Standard paddings/gaps: 8, 16, 24, 32. Avoid 18, 20, 14 unless matching existing canonical structures.
5. **Font loading.** `await figma.loadFontAsync({family, style})` before any `.characters =`. Common: Rubik (Bold/SemiBold/Medium/Regular), Noto Sans JP (Bold/Medium) for Japanese.
6. **Dev Mode screenshot tool** needs the Figma Desktop "Dev Mode MCP Server" preference enabled. If unavailable, inspect via `use_figma` instead.

## L. Cesar's session-locked canonical patterns

Locked-in for this project — don't deviate:

- Hero card 420h with image fill + dark gradient overlay (40-100%) + OPEN NOW badge top-left + FEATURED ribbon top-right + tagline → title → address at bottom
- 3-col Info card: SPACE_BETWEEN auto-layout, vertical-rect dividers, "12 min walk" IS the underlined blue map link inside the LOCATION column
- Daily Bonus pill: gold-tinted (18% opacity) with cloned Recovery stat icon + bold gold text
- Tier badges: HIGH=gold (`GOLD_BRIGHT` fills + gold gradient text), MEDIUM=silver (`{0.85,0.86,0.92}` + silver gradient text), LIGHT=muted (`{0.55,0.62,0.75}` + MUTED solid text)
- Real `Main Buttons` component instances only (resize, not rescale) — no custom button pills

---

## How to use this file

At the start of any Figma-related work this session or later:

```
Filesystem:read_text_file path=C:\Users\cesar\GolfinRedux\Docs\Reference\Figma_Lessons.md
```


Update this file whenever Cesar manually fixes something and the fix should be canonical. Add a dated note at the top of the relevant section.
