# Self-Review — `loop_v1_2d_hole_complete_and_result_screen`

Written 2026-05-11 (JST). Iteration **6** — review of iter-6 fixes addressing the six items in `CESAR_REJECTION.md` (dividers, rewards centering, ContentSizeFitter, green-square removal, real hole maps, Card 2 info block).

## Verdict

`BACK_TO_IMPLEMENTER` → STATUS `SELF_REVIEW_FAIL`.

**Headline:** Four of the six CESAR items landed (rewards centered, green square removed, buttons inside card, real hole maps loading). **The two text/divider items did not.** The dividers render as wide bright bars that are 3-4× too tall AND positioned such that they slice through header text and stats text — they are actively destroying the readability of Card 1 stats, the FAILED header in S3 Card 1, and the LOCKED header in S3 Card 2. The Card 2 description text in S2 is either invisible or rendered at a fraction of the expected size. Both defects are visible to the naked eye in the iter-6 screenshots and are not subtle calls. Cesar's pixel-perfect standing rule requires this to be sent back.

## Visual diff notes — Step 1: describe what is in the iter-6 screenshots (pixels only, no spec)

### `controls_2d_modal_hidden_aiming_2026-05-11_18-13-30.png` (S1)

Lab HUD baseline: player chip top-left (portrait + "PLAYER / Lv 1 / TURN 1"), hole chip top-right ("LOMOND / HOLE 1 - REGULAR / PAR 5"), gear top-right, "CAM: Chase  BALL: Aiming" banner centered up top, ball widget mid-screen with green G logo, four debug buttons at the bottom corners (SPIN/GOLFIN left, STRAIGHT/DRIVER right), green hole-tee terrain visible. No result modal. Matches "hidden" expectation.

### `controls_2d_modal_success_at_par_2026-05-11_18-13-32.png` (S2)

Two dark navy rounded cards centered on a dim dark-green backdrop. No HUD bleed-through.

**Card 1 — top to bottom:**
1. Green checkmark icon + bold green "SUCCESS" text, tight, centered.
2. White subhead "Lomond Country Club  - Hole 1 - Par 5", centered.
3. **A wide bright white-grey horizontal band** spanning most of the card width. The band is approximately as tall as one line of body text. To the left of the band overlap, a thin green vertical shape — the actual Lomond Hole 1 hole map sprite (correctly identified) rendered as a tall narrow pickle. The TEE OFF / STROKES / BEST / TIME / BEST stats text on the right side **partially overlaps with the bright band** — the upper rows ("TEE OFF: REGULAR", "STROKES: 5 (PAR)" in green) sit above the brightest part, then "BEST: --", "TIME: 00:00:00", "BEST: --" are rendered IN or BEHIND the band, making them low-contrast and hard to read.
4. Another wide bright white-grey horizontal band, similar thickness.
5. Three reward circles with "x10 x10 x10" — first circle yellow/gold, second grey, third white. The cluster is visually centered horizontally — this part landed.
6. A third wide bright white-grey horizontal band.
7. "REPLAY" silver pill button, ~35% card width, smooth rounded ends, fully inside the card frame.

**Card 2 — top to bottom:**
1. Gold "NEXT" text, centered, no icon.
2. White subhead "Lomond Country Club  - Hole 2", centered.
3. Bright horizontal band.
4. Body: green Lomond Hole 2 hole map (correctly identified) tall-narrow on the left. To the right of the map, gold "Par —" text. **Below "Par —", I can just barely make out a tiny line of grey text that's so small/faint as to be essentially illegible** — this appears to be where the description text should be rendering.
5. Another bright horizontal band.
6. Three reward circles "x10 x10 x10", centered cluster.
7. Third bright horizontal band.
8. "PLAY" gold pill button, ~36% card width, rounded ends, fully inside the card frame.

### `controls_2d_modal_failed_over_par_2026-05-11_18-13-34.png` (S3)

Two cards. No HUD bleed-through.

**Card 1 — top to bottom:**
1. **"FAILED" header — the word "FAILED" has the bright horizontal divider band running directly through it horizontally**, so the text appears crossed-through / partially obscured. The orange/red color is barely discernible because the bright band is brighter than the text. The orange X icon to the left of "FAILED" is similarly obscured.
2. Subhead "Lomond Country Club  - Hole 1 - Par 5" centered. (Below the FAILED-header-divider mess.)
3. Body: thin green Hole 1 map on left, stats text on right. Same overlap issue as S2 — stats text is interrupted by the second bright band running through it. "TEE OFF: REGULAR", "STROKES: 7 (DOUBLE BOGEY)" in orange visible above; the BEST/TIME/BEST lines are obscured by the band.
4. Bright band.
5. "x10 x10 x10" rewards, centered.
6. Bright band.
7. "RETRY" gold pill button, ~31% card width, rounded ends, inside the card frame.

**Card 2 — top to bottom:**
1. **"LOCKED" header — the word "LOCKED" has the bright horizontal divider band running directly through it horizontally**, slicing through the middle of the letters. The grey lock icon to the left is partially obscured similarly. Header is barely legible.
2. Subhead "Lomond Country Club  - Hole 2" centered.
3. Bright band.
4. Body area: appears empty (locked → no map / no info block, per spec). Just dark navy background + bright band visible.
5. Bright band.
6. "x10 x10 x10" rewards, slightly dimmed (alpha ~0.5) — correct for locked.
7. Bright band.
8. No button (correct — locked state hides PLAY).

Card 2 is visibly darker than Card 1 (DarkenOverlay alpha=0.65). That part is correct.

## Step 2 — Compare to Figma reference (`Docs/Reference/Results Screen/`)

| Element | Figma | Iter-6 screenshot | Match? |
|---|---|---|---|
| Card BG rounded corners | Crisp 50px radius | Crisp (iter-5 9-slice still active) | YES |
| SUCCESS / FAILED / LOCKED header — visibility | Clean, fully readable, gradient gold/orange/grey | S2 SUCCESS readable; **S3 FAILED + LOCKED partially obscured by divider band running through them** | **NO** |
| Stats block readability | Five rows of fully-readable stats text right of the map | Top 2 rows readable, bottom 3 rows partly obscured by divider band running through them | **NO** |
| Divider thickness/intensity | Subtle thin white-ish lines, faint, 1-2px effective visual weight, allow surrounding content to dominate | Wide bright bars roughly equivalent to one line of text in height (~30-40px effective at 1170px width), bright enough to compete with and overwhelm adjacent text | **NO** |
| Card 2 info block (NEXT body) | Map + visible Par + readable multi-line description text right of map (e.g. "The tee shot is best aimed in the sloping area...") | Map + "Par —" (visible) + a tiny illegible line of text where description should be | **NO** |
| Rewards row centering | Tight centered cluster | Tight centered cluster | YES |
| Buttons inside card | All buttons fully within rounded card BG | All buttons inside card frame | YES |
| Real hole maps loading | Real Hole-N art per Figma | Real Hole 1 and Hole 2 art loaded (squeezed to narrow column but the sprite is correct) | PARTIAL — loading works, sizing is suboptimal but acceptable |
| No green square | Green thumbnail removed | No green square visible anywhere | YES |
| Top bar / nav bar / sky photo | Visible in Figma | Excluded per Q3 | OUT-OF-SCOPE (intentional) |

## Step 3 — Walk the CESAR_REJECTION checklist

### Item 1 — Dividers missing → "added"

| Sub-item | Implementer's claim | Visual evidence | Verdict |
|---|---|---|---|
| `Settings/Divider.png` chosen as sprite | Loaded via `AssetDatabase.FindAssets` | Source PNG is a thin ~6-8px gradient fade — fine choice in principle | CONFIRM choice-of-sprite |
| 3 dividers per card (below subhead, below body, below rewards) | `BuildDivider()` × 3 in VLG | 3 bright bands per card visible in S2/S3 — count matches | CONFIRM-PASS on count |
| `preferredHeight=8, minHeight=4` | LayoutElement | **The bands visibly render at ~30-40px tall in the original 1170-wide screenshot, NOT 8px.** They are roughly 4× the claimed height. | **OVERRIDE-FAIL** |
| `Image color=white@alpha=0.35` (subtle) | Inspector value | **The bands render as bright opaque-looking bars that compete visually with adjacent text.** Either alpha didn't apply, or the divider is being composited over a fully-opaque card BG seam, or the sprite is being tinted brighter than 0.35. | **OVERRIDE-FAIL** |
| Dividers do not obstruct content | Implicit | **The "FAILED" header text (S3 Card 1), the "LOCKED" header text (S3 Card 2), and the bottom 3 rows of the stats block (S2 Card 1 AND S3 Card 1) are all directly intersected by a divider band, rendering them partly illegible.** This is not what dividers are supposed to do. | **OVERRIDE-FAIL** |

**Likely root causes (to investigate, not prescriptions):**
- The LayoutElement `preferredHeight=8` may be overridden by parent VLG `childForceExpandHeight=true` or by `childControlHeight=false`. Same pattern that caused the iter-4 icon-cluster bug.
- The Image `preserveAspect` may be false, allowing the sprite to vertically stretch to fill whatever height the layout group assigns it.
- The dividers' VLG **siblings** may be running at small heights such that the divider's "spare" allocation grows large.
- Header / stats text may share VLG rows or stack offsets with the divider — divider should be a SEPARATE row in the VLG, not overlapping any content row.

### Item 2 — Rewards row not centered

| Sub-item | Implementer's claim | Visual evidence | Verdict |
|---|---|---|---|
| `childAlignment = MiddleCenter` | YAML | "x10 x10 x10" cluster visibly centered horizontally in S2 Card 1, S2 Card 2, S3 Card 1, S3 Card 2 (with dim) | CONFIRM-PASS |
| `childForceExpandWidth=false, childForceExpandHeight=false` | YAML | Cluster is tight (icons immediately adjacent to their "x10" labels) and centered, not spread edge-to-edge | CONFIRM-PASS |
| Padding `(0,0,0,0)` | YAML | No artificial left/right offset visible | CONFIRM-PASS |

**Item 2 verdict: PASS.**

### Item 3 — Buttons falling outside card

| Sub-item | Implementer's claim | Visual evidence | Verdict |
|---|---|---|---|
| Hardcoded 978×600 removed | YAML / builder | Cards visibly auto-size to the height of stacked content; bottom of card frame falls below buttons in both S2/S3 | CONFIRM-PASS |
| `ContentSizeFitter.verticalFit = PreferredSize` added | YAML | Card height responds to content; no overflow at bottom in any of the four cards across S2/S3 | CONFIRM-PASS |
| REPLAY / RETRY / PLAY fully enclosed by rounded card BG | Visual | All three buttons in S2/S3 sit inside the rounded navy card with visible bottom padding | CONFIRM-PASS |

**Item 3 verdict: PASS.**

### Item 4 — Green square removed

| Sub-item | Implementer's claim | Visual evidence | Verdict |
|---|---|---|---|
| `_holeThumbnailSmall` / `_nextHoleThumbnailSmall` fields removed | Code | No green square visible anywhere in S2 or S3 | CONFIRM-PASS |
| `Placeholder_HoleThumbnailSmall.png` no longer loaded | Code | No flat green tile in either card body | CONFIRM-PASS |

**Item 4 verdict: PASS.**

### Item 5 — Real hole maps loaded

| Sub-item | Implementer's claim | Visual evidence | Verdict |
|---|---|---|---|
| Card 1 shows actual Lomond Hole 1 art | `LoadHoleMap(1)` via AssetDatabase | S2/S3 Card 1: a tall narrow green pickle shape visible left of stats — this matches the actual `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole 1.png` art (verified by direct read of the source PNG) | CONFIRM-PASS |
| Card 2 shows actual Lomond Hole 2 art | `LoadHoleMap(2)` | S2 Card 2: tall green pickle shape visible left of "Par —" text — matches actual Hole 2 PNG | CONFIRM-PASS |
| Log line "H1=True H2=True" confirms sprite load | Console | Cited in IMPLEMENTER_REPORT | CONFIRM-PASS |

Note: the maps render very narrow (squeezed horizontally) because they're constrained to a 156-wide container. Per spec § Asset strategy, this rendering is acceptable for §2d. **Not a fail — just noting.**

**Item 5 verdict: PASS.**

### Item 6 — Card 2 hole-select info block

| Sub-item | Implementer's claim | Visual evidence | Verdict |
|---|---|---|---|
| `_nextHoleParText` field added (gold "Par N") | Code + visible in S2 | S2 Card 2: "Par —" in gold text right of the Hole 2 map | CONFIRM-PASS |
| `_nextHoleDescText` field added (description) | Code | **S2 Card 2: I see at best one tiny line of low-contrast text below "Par —" that's essentially unreadable. The description is either rendering at a fraction of the expected font size, or rendered behind/inside the divider band that intersects the body row, or both.** Per Figma reference, the NEXT body should have a multi-line readable description block proportional to the card body height. | **OVERRIDE-FAIL** |
| `LookupNextHoleInfo()` reads CSV directly | Code | Implementer claims the CSV lookup miss returns the placeholder "Next Hole Tip - TBD" — that text should still be **visible** as placeholder, not invisible. | **OVERRIDE-FAIL** |
| Approximate hole-select layout (Par + description fields) | Visual | The data fields are there in code but the visual rendering doesn't approach the Figma reference — only "Par —" is legible, the description is missing/tiny. | **OVERRIDE-FAIL** |

**Item 6 verdict: FAIL.**

## Step 3b — Regression check on prior PASSes

| Prior PASS | Iter-6 evidence | Holds? |
|---|---|---|
| Header SUCCESS cluster tight + centered (iter-4) | S2 Card 1: tight green ✓ + "SUCCESS", centered, unobscured | YES |
| Header FAILED cluster tight + centered | **S3 Card 1: the cluster IS tight + centered, but the divider band immediately below the header now bleeds upward into the header row, partially obscuring "FAILED" text — visible degradation vs iter-5** | **REGRESSION** |
| Header LOCKED cluster tight + centered | **S3 Card 2: same — divider bleeds into "LOCKED" header, partially obscuring it** | **REGRESSION** |
| Subhead centered (iter-2 fix) | S2/S3 subheads centered | YES |
| STROKES color tokens green/orange (iter-2) | S2 green "5 (PAR)" / S3 orange "7 (DOUBLE BOGEY)" — visible above the band | YES |
| HUD suppression (iter-2) | S2/S3: no chip, no banner, no debug panel | YES |
| DarkenOverlay alpha=0.65 on locked Card 2 | S3 Card 2 visibly darker than Card 1 | YES |
| Lock icon white-tint placeholder visible (iter-2) | **S3 Card 2: visible BUT now overlapping with divider band** | PARTIAL-REGRESSION |
| Tip text not clipped (iter-2 wordwrap fix) | **S2 Card 2: description is missing or rendered tiny — this regresses iter-2's "tip visible in full" PASS** | **REGRESSION** |
| Stats block fontSize=24 + lineSpacing=4 (iter-2) readable | **S2/S3 Card 1: top 2 stats rows readable; bottom 3 rows obscured by divider band** | **REGRESSION** |
| Button widths 348/307/353 (iter-5) | S2/S3 buttons visibly narrower than card with breathing room | YES |
| Sprite slicing on existing buttons / card BG (iter-5) | Pill ends crisp, card corners crisp | YES |
| S1 hidden state HUD baseline | S1 iter-6: HUD visible | YES |

**Net: 3 hard regressions + 1 partial regression on prior-PASS items**, all caused by the iter-6 divider implementation overlapping content rows in the card VLG.

## Step 4 — Out-of-scope sweep

CESAR_REJECTION §"Out of scope (do not touch)" listed:
- Header / subhead alignment — alignment unchanged but **header readability is regressed** (divider overlap). Code didn't touch alignment, but the layout change broke it visually.
- HUD bleed-through — clean ✓ unchanged.
- STROKES color tokens — unchanged ✓.
- Sprite slicing on existing buttons / card BG (iter-5 PASS) — unchanged ✓.
- Button widths (iter-5 PASS) — unchanged ✓.
- HoleCompleteDriver / ShotPipeline / cup detection — IMPLEMENTER_REPORT shows `HoleCompleteDriver.cs` edited (added `LoadHoleMap`, `LookupNextHoleInfo`, `LoadLocalizationEN` helpers + map fields wiring). These are data-loading helpers, not behavior changes — they pass real sprites and CSV data into `HoleCompleteData` without altering cup detection / shot pipeline. **Acceptable** scope creep for Item 5 / 6 data plumbing.
- `RealCupDetector`, `BallStateMachine`, `ShotPipeline`, `PhysicsLabController` proper — **unchanged** per files-modified table.

## Step 5 — Capture-helper compliance

1. **Screenshot provenance:** IMPLEMENTER_REPORT cites `CaptureCore.SnapPlayModeSafe("controls_2d_modal_...")` for all three captures. This is the sanctioned helper for long-running playmode coroutines per CLAUDE.md § Screenshots (synchronous, no `AssetDatabase.Refresh`, coroutine-safe). All three files exist on disk at the cited paths with the cited timestamps. **PASS.**

2. **Maintenance protocol for new contexts:** No new `*Context.cs` files added in iter-6. The files-modified table touches `HoleCompleteData.cs` (struct fields), `HoleCompleteCardWidget.cs`, `HoleCompleteDriver.cs`, `HoleCompleteWidgetBuilder.cs`, `SmokeRunner2dHost.cs`, `LabScaffold.unity`, and 4 `.meta` files (from iter-5). No new static-bus context introduced. **N/A → PASS.**

## Step 6 — Iteration awareness

Iteration count: this is self-review **6** overall on this task. However, the CESAR_REJECTION introduced 6 NEW items not previously surfaced by any reviewer. Iter-6 is "round 1" on this specific defect set, not "round 6 of the same unsolvable issue." Per the standing rule, ESCALATE is reserved for genuine architectural judgment calls — these are concrete visual fidelity defects with clear remediation paths. ESCALATE not warranted.

## Decisions

- **Items 2, 3, 4, 5 land.** Rewards centered ✓, ContentSizeFitter ✓, green square removed ✓, real hole maps loaded ✓.
- **Item 1 (dividers) fails on rendering — wrong thickness, wrong opacity/intensity, wrong positioning relative to content rows.** The visible bands are bright bars that intersect adjacent text rows.
- **Item 6 (Card 2 description text) fails on rendering — description text is missing or rendered at a fraction of the expected size.**
- **Regressions on prior PASSes:** FAILED header, LOCKED header, lock icon visibility, tip-text visibility (now description text), stats block readability — all caused by the divider implementation.

**Verdict:** `BACK_TO_IMPLEMENTER`. STATUS → `SELF_REVIEW_FAIL`.

## Concrete fix list for the Implementer

### F1. Dividers — fix rendering height and positioning.

**Symptom:** Divider bands render as wide bright bars roughly 30-40px tall (at 1170px width), bright enough to obscure adjacent text. They intersect FAILED header (S3), LOCKED header (S3), and the bottom 3 stats rows on Card 1 (S2 + S3). Per Figma reference, dividers are subtle thin lines (~1-2px effective visual weight) that separate sections without competing with content.

**Investigate and fix all of:**

1. **Verify the divider is actually rendering at preferredHeight=8.** Inspect the divider GameObject's `RectTransform.sizeDelta` and the effective rendered rect via Unity's RectTransform debug. If `sizeDelta.y > 8`, then the LayoutElement preferredHeight is being overridden — the parent VLG likely has `childForceExpandHeight=true` or `childControlHeight=false`. Apply the same pattern as iter-4 / Item 2 here: `vlg.childForceExpandHeight = false; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childControlWidth = true;` so the LayoutElement actually wins on height. Bake YAML and re-screenshot.
2. **Verify Image.color alpha is actually 0.35.** Inspect the Image component on the divider GO. If alpha is 1.0, the inspector setting didn't apply. Set `imageComp.color = new Color(1f, 1f, 1f, 0.35f)` explicitly in `BuildDivider()`. If the divider sprite itself has baked opacity > 0.35, multiply with the white-translucent color.
3. **Verify Image.preserveAspect=true.** If false, the divider sprite vertically stretches to whatever the LayoutElement assigns. Set `imageComp.preserveAspect = true` so the sprite stays at native aspect ratio (which is roughly 1170:8 = ~146:1 for the source sprite).
4. **Verify the divider is in its OWN VLG row, not sharing a row with text content.** The fact that the band currently overlaps FAILED/LOCKED header text suggests the divider GO is positioned ABOVE or BELOW the header within a row that header text also occupies. The divider should be a strictly separate sibling within the card VLG, between sibling rows for header / subhead / body / rewards / buttons.

After fixing, the dividers should render as thin (≤ 8px tall) faintly visible white-grey lines, clearly subordinate to the surrounding text, with no overlap.

### F2. Card 2 description text — make visible.

**Symptom:** S2 Card 2 shows "Par —" gold text but the description text below ("Next Hole Tip - TBD" placeholder per the CSV-miss fallback) is missing or rendered at a fraction of the expected size. Per Figma reference, the NEXT body description should be a multi-line readable block.

**Investigate and fix all of:**

1. **Verify `_nextHoleDescText` actually has text assigned at runtime.** Inspect the TMP component in the scene after `HoleCompleteCardWidget.BindNextHole()` runs. If `.text == ""` or null, the binding code isn't writing the placeholder string. Check `HoleCompleteDriver.LookupNextHoleInfo()` — if CSV lookup for Hole 2 misses, the fallback path must still write a visible placeholder like "Next hole tip — TBD" into `data.NextHoleDescText`.
2. **Verify the TMP font size.** Implementer-claimed "18pt, white, word-wrap enabled" — if font size is actually 18pt at the 1170-wide canvas with the body container ~700px wide, that should be readable. If the font size is set in points but the canvas scaler is doing something weird, the effective render size may be tiny. Check the rendered text in the screenshot for legibility — currently illegible. Bump fontSize to match the "Next hole tip — TBD" iter-2 sizing (which WAS readable per iter-5 self-review's "S2 Card 2 'Next hole tip — TBD' visible in full" PASS).
3. **Verify the description TMP is not BEHIND a divider band.** The dividers issue (F1) may be the actual root cause — if the description sits in a body row immediately below a divider that's rendering 4× too tall, the divider may be visually covering the description. Fixing F1 may fix F2 as a side effect, but verify.
4. **Verify the description TMP rect height.** If the rect is collapsed to 0 height (e.g. the parent VLG isn't honoring preferred height), the text won't have vertical space to render. Apply `LayoutElement.preferredHeight` matching the expected line count × line height (e.g. 4 lines × 28px ≈ 112).

After fixing, the Card 2 NEXT body should show a clearly readable multi-line description text (placeholder string is fine), at a size proportional to the body area, matching the Figma layout where the description occupies the right column of the body block.

### F3. Verify no regression on header / stats / lock-icon readability.

Once F1 (dividers) is fixed, the FAILED header (S3 Card 1), LOCKED header (S3 Card 2), lock icon, and bottom-3-stats rows on Card 1 should all be fully legible again. Re-screenshot S2 + S3 and verify:

- "SUCCESS" / "FAILED" / "NEXT" / "LOCKED" headers fully unobscured.
- All 5 stats rows on Card 1 visible (TEE OFF / STROKES / BEST / TIME / BEST).
- Lock icon visible alongside "LOCKED" text without divider overlap.

### F4. Re-screenshot S2 + S3 via the same `CaptureCore.SnapPlayModeSafe` path and re-file to `screenshots/`.

Keep the same naming pattern: `controls_2d_modal_success_at_par_2026-05-11_HH-MM-SS.png` and `controls_2d_modal_failed_over_par_2026-05-11_HH-MM-SS.png`. Update IMPLEMENTER_REPORT with the new timestamps and updated content-sanity description that **accurately** matches the new screenshots (no claims that aren't visible in the pixels — Lesson O again).

## File summary

| Path | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/SELF_REVIEW.md` | This file (iter-6 verdict: BACK_TO_IMPLEMENTER) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/STATUS.md` | Updated to `SELF_REVIEW_FAIL` |
