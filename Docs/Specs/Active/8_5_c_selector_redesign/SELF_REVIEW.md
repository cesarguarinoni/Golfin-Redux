# Self-Review — `8_5_c_selector_redesign`

**Reviewer:** golfin-self-reviewer
**Iteration:** 1 (first self-review on this task)
**Date:** 2026-04-30 JST
**Verdict:** **BACK_TO_IMPLEMENTER (FAIL)**

Note on STATUS: STATUS.md is already `READY_FOR_ARCHITECT_REVIEW` because the Implementer ships with FAIL items, which by hook policy routes around SELF_REVIEW directly to architect. I am writing this review anyway per Cesar's request and downgrading STATUS back to `SELF_REVIEW_FAIL` because the screenshot does not demonstrate the production code path and shows visible defects beyond what the Implementer flagged.

---

## Step 1 — Visual diff notes (screenshot only, before consulting spec)

Screenshot reviewed: `screenshots/8_5c_v3_open_delayed_2026-04-30_09-52-39.png`.

What I see, top to bottom:

- **Top-left:** small character portrait (red cap) with two short dark-navy chips beside it reading "PLAYER", "Lv 1", "TURN 1".
- **Top-right:** three dark-navy chips stacked vertically reading "LOMOND", "HOLE 1 - REGULAR", "PAR 5". A white circular gear button sits in the very top-right corner.
- **Center / mid:** golf course with a fairway, a single white ball mid-fairway, a flagstick on the green further up, and an "ACE" logo on the green.
- **Right edge of screen:** four rectangular cards stacked vertically. Each card is portrait orientation (taller than wide), with a white top half (showing a small club icon) and a navy bottom half with white text. From top to bottom:
  1. "WOOD" / "230 yrds"
  2. "IRON" / "180 yrds"
  3. "PUTTER" / "30 yrds"
  4. "STRAIGHT" / "DRIVER 250 yrds" — note this card has THREE lines of text where the others have two; "STRAIGHT" appears as an extra line above "DRIVER".
- **Inter-card gap** between WOOD-IRON and IRON-PUTTER and PUTTER-DRIVER looks visibly tight — the gap is roughly 8-12% of card height. Compared with a 240-tall card and a spec'd 34px gap, that ratio should be ~14%. It is on the small side; possibly correct, possibly slightly compressed.
- **No chevron arrow is visible above the WOOD card.** I see only the gear button (top-right corner) and the "LOMOND" chip — no chevron sprite.
- **No chevron arrow is visible below the DRIVER card.** The bottom edge of DRIVER sits roughly where a normal DRIVER button would be. Below it is empty fairway/grass.
- **Bottom-left:** small green circular GOLFIN button (full opacity, looks normal).
- **Bottom-center:** small white SPIN button (full opacity, looks normal).
- **Bottom-right:** no DRIVER trigger button visible — the card stack visually replaces it. This part matches spec.
- **Other action buttons (SPIN, GOLFIN, and any FADE/DRAW button if present)** appear at full opacity, NOT 50%. I cannot find a FADE/DRAW button in the image at all — possibly off-screen or not part of this layout.

## Step 2 — Compare to spec/reference (no Figma reference image was available locally; comparison is against spec text)

- Spec § Stack: "8px gap between stack and each arrow" — N/A because no arrows are visible.
- Spec § Arrows (top + bottom): "80 × 25 visible chevron, wrapped in a 24px-padding container" + "Top arrow points up", "Bottom arrow points down". **Defect:** neither arrow is visible in the screenshot.
- Spec § State during selection: "SPIN, FADE/DRAW, and the *other* selector button → CanvasGroup.alpha = 0.5". **Defect:** SPIN and GOLFIN look full-opacity. (Implementer correctly flagged this as FAIL because the legacy `Open()` path was used to capture, bypassing `OtherButtonsFader`.)
- Spec § Card content: cards should show one primary line (e.g. "DRIVER" 30px) and one secondary line (e.g. "195.7 yrds" with two-weight typography). **Suspected defect:** the DRIVER card shows three lines including "STRAIGHT". The other three cards are two-line. This suggests something — possibly a shot-mode or aim-style label — is leaking onto the bottom (selected) card. Worth investigating.
- Spec § Position relative to trigger: bottom card's bottom Y aligns with trigger's bottom Y. From the screenshot, the DRIVER card's bottom edge appears to sit roughly where the DRIVER trigger button would have been (bottom-right area, above the SPIN button row). Hard to verify exactly without an overlay reference, but it looks plausible.

## Step 3 — Checklist walk-through

### Layout (static, selector open)

| Item | Implementer | My verdict | Reasoning |
|---|---|---|---|
| 4 cards stacked, selected at bottom | PASS | **CONFIRM-PASS** | Screenshot shows 4 cards with DRIVER at bottom |
| 34px gap between cards | PASS | **CONFIRM-PASS (with caveat)** | Gap looks slightly tight visually but VLG `spacing=34` cited in code; I'll accept the YAML value here because the visual is in the right ballpark |
| Top chevron visible above top card, ~32px gap | PASS | **OVERRIDE-FAIL** | No chevron is visible above the WOOD card in the screenshot. Implementer's PASS was based on container layout math (preferredHeight=73), not pixels. Likely cause: chevron sprite not assigned to the Image, or the arrow GO is inactive, or the chevron is rendering at zero alpha / wrong color |
| Bottom chevron visible below bottom card, ~32px gap | PASS | **OVERRIDE-FAIL** | Same — no chevron visible below DRIVER card |
| Selector right edge aligns with Driver button right edge (x=-58) | PASS | **CONFIRM-PASS** | Cards visibly hug the right edge with a small inset; matches the Driver button anchor |
| Bottom card bottom Y aligns with Driver button bottom Y (y=96) | PASS | **CONFIRM-PASS** | Bottom card sits roughly where the trigger button row is |
| Driver button hidden while selector open | FAIL | **CONFIRM-FAIL** | Implementer was honest: screenshot was taken via legacy `Open()` path which does not invoke `OtherButtonsFader`. The router path is correct in code but unverified visually. Note: the trigger button is in fact NOT visible in the screenshot, but that's because the cards overlap it, not because it's been actively hidden — different mechanism, untested |
| Other 3 buttons (SPIN, FADE/DRAW, GOLFIN) at 50%, non-interactive | FAIL | **CONFIRM-FAIL** | SPIN and GOLFIN visibly at full opacity. FADE/DRAW button is not even visible in the image — its position or existence is unclear |
| Same for Golfin selector (mirrored) | PASS | **UNVERIFIED** | No screenshot of Golfin selector provided. The builder code creates a mirrored overlay, but no visual evidence. Cannot confirm or override; treating as ESCALATE-equivalent — needs a Golfin-side screenshot |

### Hold-mode interaction

All items marked `PASS (code)` by Implementer. **All UNVERIFIED visually** — these are runtime touch-input behaviors that require live touch input or Unity input simulation to test. Reasonable for the architect to verify in playtest. I do not override these to FAIL because the code paths are plausible and Implementer's justifications cite specific functions; but no visual evidence exists.

### Tap-mode (modal) interaction

Same as above — all `PASS (code)`, all visually UNVERIFIED. Architect playtest territory.

### Lab integration

All four items: Implementer marked FAIL with "Runtime interaction not testable via MCP". **CONFIRM-FAIL** — these are not testable from a still screenshot. Needs live playtest by Cesar / architect.

### Visual fidelity

| Item | Implementer | My verdict | Reasoning |
|---|---|---|---|
| Side-by-side diff against Figma node 12942:1079 | FAIL | **CONFIRM-FAIL** | No reference PNG; no diff produced. Spec explicitly requires `diff-selector-vN.png` |
| Card highlight scale 1.05 visible but not jarring | PASS (code) | **UNVERIFIED** | No hover/highlight evident in static screenshot |

### Edge cases

All `PASS (code)` and **UNVERIFIED visually**. Same playtest territory.

### Additional defect not in checklist

**DRIVER card content has an extra "STRAIGHT" line — but this is a positioning bug, not a card-content bug.**

Looking at the screenshot more carefully: the "STRAIGHT" text is the **STRAIGHT button** (the existing aim-direction action button) bleeding through *behind* the DRIVER selector card. The selector cards are rendering directly on top of the action button cluster instead of beside it. The bottom card overlaps the STRAIGHT button's position, and because the card's white-top / navy-bottom layering doesn't fully occlude it, the "STRAIGHT" label shows through where the DRIVER card's content area starts.

**Root cause:** the selector container's anchor/pivot math is wrong. The bottom card should sit *above* and *beside* the trigger button, with its bottom edge aligned to the trigger's bottom edge — but the cards are stacking centered over the trigger button's position. Likely the `VerticalLayoutGroup` `childAlignment=LowerCenter` is correct but the container's `pivot` is at center (0.5, 0.5) instead of bottom (0.5, 0) or (1, 0) for the right-side selector, so the stack grows in both directions from the trigger position rather than upward only.

**Fix:** in `SelectorOverlayWidget` setup (or `ActionButtonsBuilder`), set the selector container's `pivot = (1, 0)` for the Driver/right-side selector and `pivot = (0, 0)` for the Golfin/left-side selector. The `anchoredPosition` should match the trigger button's bottom-right (or bottom-left) corner. Verify the cards then grow upward from the trigger's bottom edge with no horizontal overlap on the action button cluster.

## Step 5 — Capture-helper compliance

1. **Screenshot provenance:** PASS. The Implementer added `Assets/Scripts/Editor/SelectorScreenshotHelper.cs` which calls `CaptureHelper.SnapGameViewWithLabel(...)`. Compliant with CLAUDE.md § Screenshot rules.
2. **Maintenance protocol for new contexts:** N/A. The diff did not add any new `*Context.cs` files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. Existing contexts (PlayerContext, HoleContext, WindContext, ClubContext, BallContext, ShotModeContext, SpinContext) were already present.

## Concrete fix list for the Implementer

1. **Make the chevron arrows render.** Confirm `ArrowUp` and `ArrowDown` GameObjects are active, the Image components have a sprite assigned (the spec mentions `Icon - Straight.png` as a placeholder if no chevron asset exists), and the color/alpha is opaque. If a chevron PNG is not in `Assets/Art/In-Game UI/`, surface that to architect rather than shipping with invisible arrows. Re-screenshot.

2. **Capture via the actual production path.** Create a screenshot helper that invokes `SelectorDragRouter.OnPointerDown` (or use `EventSystems.ExecuteEvents.Execute<IPointerDownHandler>`) so that `OtherButtonsFader.FadeAllExcept` runs and the trigger button is genuinely hidden via alpha=0. The current screenshot was captured via the legacy `Open()` bypass, which does not exercise the spec'd open-state visuals. Without that, three layout checklist items remain visually unverified.

3. **Fix selector positioning — root cause of the "STRAIGHT" bleed-through.** The selector cards are rendering on top of the action button cluster instead of beside it. The cards stack centered over the trigger position; the bottom card overlaps the STRAIGHT button area, and STRAIGHT's label shows through. Fix the container pivot: for the right-side (Driver) selector, pivot=(1, 0); for left-side (Golfin), pivot=(0, 0). `anchoredPosition` should match the trigger button's bottom corner (bottom-right for Driver, bottom-left for Golfin). The stack should grow upward from the trigger's bottom edge. After the fix, no action buttons should be visible *under* the cards — they should be visible *beside* the cards (and at 50% opacity per the fade fix in item 2).

4. **Provide a Golfin-side selector screenshot.** Open the GOLFIN button and capture; the mirrored overlay's position, card content, and arrow rendering must be visually verified just like the Driver side.

5. **(Stretch) Pull a Figma export of node 12942:1079** via Figma MCP and produce the `diff-selector-vN.png` side-by-side comparison the spec requires. This is currently a hard FAIL on the visual fidelity checklist.

Once items 1–4 are addressed and re-screenshotted, the runtime-interaction items (hold/tap/lab integration) can move forward to architect for playtest verification.

---

**Verdict:** **BACK_TO_IMPLEMENTER (FAIL).**

Setting STATUS to `SELF_REVIEW_FAIL`.
