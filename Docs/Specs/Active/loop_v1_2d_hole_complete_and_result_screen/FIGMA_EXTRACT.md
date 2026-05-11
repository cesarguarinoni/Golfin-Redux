# FIGMA_EXTRACT — `loop_v1_2d_hole_complete_and_result_screen`

> Companion to SPEC.md. Holds the salient layout / dimension / color / font tokens distilled from the 4 Figma frames. Implementer reads this for exact pixel values.
>
> Source frames (file `5gEAHjl6xAtW8iYY7NMvWd`):
> - `12988-5223` Results — Success (Replay) — STROKES tied BEST
> - `12988-4902` Results — Success (Replay) — STROKES worse than BEST (data-only difference vs 5223)
> - `12988-5466` Results — Failed (Replay) — no PB, RETRY, NEXT LOCKED
> - `12987-4316` Results — Failed (Replay) — has PB, REPLAY (silver), NEXT unlocked
>
> Figma canvas is 1170 × 2532 (3x density iPhone-Pro-vertical). Implementer maps to Unity canvas reference resolution (likely 390 × 844 logical). Divide Figma px by 3 for logical px, OR use 1170-wide canvas reference. Font sizes ÷ 1.4 = TMP font size per project rule.

---

## Top-level frame structure (1170 × 2532)

```
Frame "Results - {Variant}"
├─ Backgrounds (1170 × 2532, full-frame screenshot of in-game view as bg)
└─ Game Screen Content (full-frame, backdrop-blur-10, bg rgba(0,0,0,0.1))
    ├─ Top UI            (1170 × 313)   ← OUT OF SCOPE for LabScaffold (Q3)
    ├─ Content Container (1170 × 1908)  ← Card 1 + Card 2 stacked, 24px gap
    │   ├─ Mission Card Container 1   ← current hole
    │   └─ Mission Card Container 2   ← next hole (or LOCKED placeholder)
    └─ Nav Bar Container (1170 × 263)   ← OUT OF SCOPE for LabScaffold (Q3)
```

The `Game Screen Content` div uses:
- `backdrop-blur-[10px]` — 10px Gaussian blur on the underlying gameplay view
- `bg-[rgba(0,0,0,0.1)]` — 10% black tint
- `px-[48px]` — 48px horizontal padding
- `gap-[24px]` — 24px between Top UI / Content / Nav

---

## Card layout (both cards same shell)

```
Mission Card Container (978 × auto)
├─ outer:    bg-gradient-to-b from-[#133453] to-[#091B33]
│            border-3 solid white
│            rounded-[50px]
│            drop-shadow-[0px_10px_10px_rgba(0,0,0,0.4)]
└─ inner Pop-Up:
    ├─ border-1 solid #0A1D35
    ├─ rounded-[50px]
    ├─ pt-[24px]
    └─ children:
        ├─ Mission Title         (978 × auto, pb-16, px-16)
        │   ├─ Header (icon + label, gap-16)
        │   └─ Subhead text "Lomond Country Club  - Hole 6 - Par 5"
        ├─ Separator (978 × 0, 2px white line)
        ├─ Mission Content Container (978 × auto, gap-24, px-32, py-24)
        │   ├─ Tutorial / body block
        │   ├─ Separator
        │   └─ Mission Content (rewards)
        ├─ Separator
        └─ Buttons Container (button cluster, pb-24)
```

---

## Card 1 — current hole (always present in §2d)

### Header variants

**SUCCESS**:
- Icon: green ✓ check, 39 × 39 (Assets/Art/ResultScreen/Icon - Check.png)
- Text: "SUCCESS"
- Color: `#50C878` (Rare green)
- Font: Rubik SemiBold, 45px, lh 60px, tracking -0.69

**FAILED**:
- Icon: red ✗ X, 39 × 39 (Assets/Art/ResultScreen/Icon - X.png)
- Text: "FAILED"
- Color: orange gradient top-bottom: `#D16A47` → `#C04000` → `#8E2D00`
- Same font as SUCCESS

### Subhead

- Text: `Lomond Country Club  - Hole {N} - Par {P}` (note the double space between "Club" and "-")
- Font: Rubik SemiBold, 39px, lh 54px, tracking -0.24
- Color: white

### Body block (Tutorial)

```
Body
├─ Hole 1 - Green 1     (94 × 94.91, border-3 white, rounded-20)        ← small green tile
├─ Hole 1 - Map 2       (155.61 × 288.5)                                 ← large map graphic
│   └─ shot-path dots (4 ellipses, 7.926 × 7.926, positioned absolutely)
└─ Goals Container      (500w, gap-24, px-48, pt-12)
    └─ Stats text:
       TEE OFF: REGULAR
       STROKES: 5 (PAR)              ← color: green #50C878 if score≤0, orange-gradient if score>0
       BEST:    5 (PAR)
       TIME:    00:02:34
       BEST:    00:02:34
```

Stats text font: Rubik Medium, 30px, lh 36px, tracking -0.5, white (except STROKES value).

### Rewards row

```
Rewards Container (h-72, pl-32, gap-32)
├─ Coin Reward    (icon 42×42 + "x10" text)
├─ Repair Reward  (icon 42×42 + "x10" text)
└─ Ball Reward    (icon 42×42 + "x10" text)
```

"x10" font: Rubik SemiBold, 51px, lh 66px, tracking -1.29, white, center-aligned.

In **FAILED-NO-PB** Card 2, the rewards row uses `opacity-50`.

### Buttons (Card 1, exactly one visible)

**REPLAY (silver)** — for Success and Failed-with-PB:
- 120px tall, px-48
- Bg: linear-gradient `#FFFFFF` → `#D1D5DB` (40%) → `#818EA1` (100%), 180.13°
- Border: 2px `#F7F8F9`
- Text: "REPLAY", Rubik SemiBold 66px, lh 84px, tracking -0.78, color `#1E293B`
- Text shadow: `0 1px 0 rgba(255,255,255,0.3)`
- Inner sheen: top-50% gradient white→transparent, mix-blend-mode hard-light
- (Asset: `Assets/Art/ResultScreen/Button - Replay.png`)

**RETRY (gold)** — for Failed-no-PB:
- 120px tall, px-48
- Bg: linear-gradient `#FCF195` → `#D6AB42` (60%) → `#BB7F1D` (100%), 180.17°
- Border: 2px `#FFE48B`
- Text: "RETRY", Rubik SemiBold 66px, color `#321506`
- (Asset: `Assets/Art/ResultScreen/Button - Retry.png`)

---

## Card 2 — next hole

### Header variants

**NEXT (unlocked)**:
- Text: "NEXT"
- Color: `#EEDC9A` (mission gold)
- Font: Rubik SemiBold 45px, lh 60px, tracking -0.69
- No icon

**LOCKED (failed-no-PB)**:
- Icon: grey lock (40 × 50) — placeholder OK
- Text: "LOCKED"
- Color: `#C8C8C8` (silver)
- Same font

### Subhead

- Text: `Lomond Country Club  - Hole {N+1} - Par {P}`
- Same font/color as Card 1 subhead

### Body block (unlocked only — locked variant skips body)

Same Tutorial layout as Card 1 (small green tile + map graphic + dots), but the right-side Goals Container holds **tip text** instead of stats:

```
The tee shot is best aimed at the Sslopping area in the center of the two tiered fairway, where the right side is wide. The landing spot of the second shot is crucial.
```

Tip text font: Rubik Medium, 30px, lh 36px. Highlighted phrases ("tee shot", "Sslopping area in the center") in `#EEDC9A` mission-gold; rest in white.

### Rewards row

Same as Card 1. **Locked variant uses `opacity-50` and adds an Empty Container (100w) at the start, an Arrow Container (100w, h-46) at the end** — matches the dimmed-no-button look.

### Button (unlocked only)

**PLAY (gold)**:
- 120 tall, **px-96** (wider than Replay/Retry)
- Same gold gradient as RETRY
- Border: 2px `#FFE48B`
- Text: "PLAY", Rubik SemiBold 66px, color `#321506`
- (Asset: `Assets/Art/ResultScreen/Button - Play.png`)

### Locked overlay

A single `Darken` Image fills the whole card (1170 × full-card-height) with a dim semi-transparent overlay. Sits above the card content, below the LOCKED header (or above all content?). In Figma, it's a child of `Mission Card Container` with `inset-[-3px_calc(0.31%-2.98px)_-3px_calc(-0.31%-3.02px)]` and `imgDarken`. **Implementer can stub** with a 50%-alpha black Image at full card size.

---

## Variant matrix recap

| State | Header | STROKES color | Card 1 button | Card 2 header | Card 2 body | Card 2 button | Card 2 rewards |
|---|---|---|---|---|---|---|---|
| Success | green ✓ SUCCESS | green `#50C878` | REPLAY (silver) | "NEXT" gold | tip text + map | PLAY (gold) | full opacity |
| Failed, no PB | red ✗ FAILED | orange gradient | RETRY (gold) | "🔒 LOCKED" grey | (hidden) | (hidden) | 50% opacity |
| Failed, has PB | red ✗ FAILED | orange gradient | REPLAY (silver) | "NEXT" gold | tip text + map | PLAY (gold) | full opacity |

---

## Color tokens

| Token | Hex | Usage |
|---|---|---|
| White | `#FFFFFF` | Borders, primary text |
| Card BG top | `#133453` | Card gradient start |
| Card BG bottom | `#091B33` | Card gradient end |
| Card inner border | `#0A1D35` | Inner 1px border |
| Success green | `#50C878` | SUCCESS header, success STROKES |
| Failed orange #1 | `#D16A47` | Failed gradient top |
| Failed orange #2 | `#C04000` | Failed gradient mid |
| Failed orange #3 | `#8E2D00` | Failed gradient bottom |
| Mission gold | `#EEDC9A` | NEXT header, tip-text highlights |
| Locked grey | `#C8C8C8` | LOCKED header |
| Replay btn dark | `#1E293B` | REPLAY button text |
| Replay btn border | `#334155` | REPLAY button outer border |
| Replay btn inner | `#F7F8F9` | REPLAY button inner border |
| Replay btn gradient | `#FFFFFF` / `#D1D5DB` / `#818EA1` | Silver gradient |
| Gold btn text | `#321506` | RETRY/PLAY button text |
| Gold btn border outer | `#422100` | RETRY/PLAY outer border |
| Gold btn border inner | `#FFE48B` | RETRY/PLAY inner border |
| Gold btn gradient | `#FCF195` / `#D6AB42` / `#BB7F1D` | Gold gradient |

## Font tokens

All text uses **Rubik** family. Per project rule, divide Figma px by 1.4 for Unity TMP font size.

| Style | Figma px | TMP px | Weight | Tracking | Use |
|---|---|---|---|---|---|
| Title_2 | 66 | 47 | SemiBold | -0.78 | Button label |
| Headline | 51 | 36 | SemiBold | -1.29 | Reward "x10", "RESULTS" |
| Subhead | 45 | 32 | SemiBold | -0.69 | SUCCESS/FAILED/NEXT/LOCKED |
| Footnote | 39 | 28 | SemiBold | -0.24 | Subhead "Lomond Country Club  - ..." |
| Caption_3 | 30 | 21 | Medium | -0.5 | Stats block, tip text |

---

## Figma node IDs reference

If the implementer ever gets Figma MCP access (currently disabled per project memory) and wants to re-extract:

```
fileKey:  5gEAHjl6xAtW8iYY7NMvWd
nodes:    12988-5223  (success, tied best)
          12988-4902  (success, worse than best — same layout)
          12988-5466  (failed, no PB → RETRY + LOCKED)
          12987-4316  (failed, has PB → REPLAY + UNLOCKED)
```
