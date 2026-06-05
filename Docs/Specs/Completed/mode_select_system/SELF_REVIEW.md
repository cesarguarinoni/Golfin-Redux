# Self-Review — `mode_select_system` — iter-7 REDO (§6 Figma-exact fidelity pass)

**Reviewer:** golfin-self-reviewer
**Date:** 2026-06-04 19:23 CEST
**Self-review iteration:** N=4 (prior reviews iter-2/3/5 in same file; this is the §6 REDO after the iter-7 corruption reset)
**Verdict:** `BACK_TO_IMPLEMENTER` (`SELF_REVIEW_FAIL`)

> Cesar's out-of-scope carve-outs honored: the "GOLFIN Presents / The Invitational" hero title and the orange MAINTENANCE NOTICE banner are not graded.

---

## Step 1 — Independent pixel scan (no spec / no YAML / no report)

### iter7_final_home_collapsed.png
Portrait 1170×2532. Top: dark navy bar with "R 52,400" coin pill top-left and a gear circle top-right. Below: "CHOTO" centered white character name on a dark navy ribbon. Then an orange/amber rounded banner "MAINTENANCE NOTICE / Scheduled server maintenance: 2025/12/31 / The game will not be available for a short time / during maintenance." (out of scope). Centered: a large character holding a gold trophy (GOLFIN cap). Near vertical centre: a navy card "PRACTICE / Sharpen your skills on any hole. / ENTRY FEE [coin] x100 / REWARDS [coin] x50". Immediately below the card sits a yellow/golden **PLAY** button that visibly **overlaps the top of the orange "GOLFIN GPS / CHECK-IN WITH GPS / EARN MORE POINTS TO POWER UP!" promo banner** beneath it — the banner's left photo edge and "GOLFIN GPS" text are partially hidden behind the PLAY button. Left edge shows partial peek card ("NS / te challenges. / x200"). Right edge: clean (no peek visible). Bottom: 5-icon nav bar with the centre golf-tee in a teal circle.

### iter7_final_home_expanded.png
Same frame. The PRACTICE card is now **taller**: title PRACTICE in light/gold, then short tagline, then paragraph "Practice your golf skills on any course. Choose a hole and / tee off at your own pace — no pressure.", then ENTRY FEE [coin] x100 / REWARDS [coin] x50, then the yellow **PLAY** button **inside the card** with a clean white separator visible above it. The card terminates with a clear bottom edge; below it sits a visible gap and then the orange GOLFIN-GPS promo banner, **fully readable** with no overlap. Left peek card partially visible. Bottom nav present.

### iter7_final_fullscreen_collapsed.png
Top: dark navy bar, "R 52,400" coin pill, gear, **MODE SELECTION** centered in white. Below: a single mid-navy rounded **back panel** spans most of the screen width with ~50-60px side margins. Inside it are 4 stacked rounded mode cards with a small inset margin from the panel edge:
1. "PRACTICE / Sharpen your skills on any hole. / ENTRY FEE  x100  REWARDS  x50" — title readable; tagline normal; **the ENTRY FEE / REWARDS labels are microscopic, far smaller than the title**.
2. "1V1 / Face off in fast-paced 1v1 matches. / NO ENTRY FEE  REWARDS  x50" — same tiny label rendering.
3. "[lock] DRIVING RANGE / Coming Soon — practice your drives. / NO ENTRY FEE" — title dimmed/silver, lock glyph in title row.
4. "[lock] MISSIONS / Coming Soon — complete challenges. / NO ENTRY FEE  REWARDS  x50" — dimmed/silver, lock.

All 4 cards display the **same border color/weight** — I cannot differentiate any "active" from "inactive" by border. Bottom 5-icon nav with teal tee centre. **No scroll arrows visible** on the panel sides.

### iter7_final_fullscreen_expanded.png
Same top frame and back panel. Cards:
1. **PRACTICE — EXPANDED**: title PRACTICE, tagline "Sharpen your skills on any hole.", paragraph "Practice your golf skills on any / course. Choose a hole and tee off / at your own pace — no pressure.", then ENTRY FEE [coin] x100, then REWARDS [coin] (value covered) — the **yellow PLAY button is sitting ON TOP of the REWARDS row**, partially hiding the REWARDS coin and the x50 value. There is no visible separator + breathing-room band between REWARDS and PLAY.
2. "1V1 / Face off in fast-paced 1v1 matches." collapsed
3. "[lock] DRIVING RANGE / Coming Soon — practice your drives." collapsed-locked
4. "[lock] MISSIONS / Coming Soon — complete challenges." collapsed-locked

Borders across the 4 cards look similar in colour/weight — no clear white-vs-blue active/inactive differentiation.

---

## Step 2 — Figma diff (vs `screenshots/figma_*.png`)

### vs `figma_13027-5212_home_collapsed.png` (Home collapsed)
- **MAJOR — PLAY overlaps the GOLFIN-GPS promo banner.** Figma: PLAY sits INSIDE the centred MULTIPLAYER card; the card ends cleanly and the orange GPS banner sits BELOW with a clear gap. iter7: PLAY is butted against the banner top and visibly covers part of the banner photo/text.
- Title weight is comparable, but iter7's "ENTRY FEE / REWARDS" labels render thinner than Figma's labels (this matches what I found in the prefab YAML — see Step 3 / Step 5 below).
- Carousel side arrows correctly removed (matches §6.3-9 + Cesar carve-out).

### vs `figma_13027-10471_home_expanded.png` (Home expanded)
- PLAY-inside-card with clear gap to promo banner: **iter7 matches Figma here**. Home-expanded is the cleanest of the four canonicals.
- Title / tagline / description placement reads close to Figma.
- ENTRY FEE/REWARDS labels are visibly thinner than Figma's heavier labels (see Step 5).

### vs `figma_13026-1924_fullscreen_modeselect.png` (Full-screen)
- **MAJOR — PLAY overlaps REWARDS row in the expanded card.** Figma shows ENTRY FEE → REWARDS → separator → vertical breathing room → PLAY button. iter7 shows PLAY visually overlapping the REWARDS row, REWARDS value occluded.
- Active card border differentiation: Figma's MULTIPLAYER card has a clearly brighter (white) border vs the three collapsed cards' darker borders. iter7's four cards look the same colour from a glance.
- Collapsed "ENTRY FEE" / "REWARDS" labels: Figma renders these at a clearly readable, moderately bold size. iter7 renders them tiny and thin — labels are far smaller than the spec's 27.86 Unity-px would render on this canvas if weight + sizing were correct.
- Back panel present (item 11) ✓, cards visibly inset within (item 12) ✓, locked overlay confined to card rect (item 13) ✓, no per-card chevron in list (item 16) ✓.

---

## Step 3 — Per-§6.3-item verdict (17 items)

| # | Item | Implementer | Self-review | Evidence |
|---|---|---|---|---|
| 1 | Rubik SemiBold 600 everywhere | PASS | **OVERRIDE-FAIL** | `ModeCard.prefab` lines **431, 2138**: `m_fontWeight: 400` on **EntryFeeLabel** ("ENTRY FEE") and **RewardsLabel** ("REWARDS"). These are the labels the implementer added in iter-7 REDO (report line 26); weight 400 contradicts the §6.2 "weight 600 everywhere" rule and matches the pixel evidence of thin/small labels in fullscreen_collapsed. |
| 2 | Fee/reward centered cluster | PASS | CONFIRM-PASS | Screenshots show `[LABEL] [coin] [value]` centred horizontally; not corner-spread. HLG settings in YAML match. |
| 3 | Active gold / collapsed silver title | PASS | CONFIRM-PASS | Visible: fullscreen_expanded PRACTICE reads in gold-ish/light tone; fullscreen_collapsed DRIVING RANGE / MISSIONS read in silver/dimmed. Mild concern: collapsed title appears desaturated grey, not a strong silver gradient, but the implementer flagged this as known deviation (§Known deviation 4) and it tracks Figma close enough. |
| 4 | Active 3px white / collapsed 3px `#3E7CA8` border | PASS | **OVERRIDE-FAIL** | Pixel-level: in `iter7_final_fullscreen_expanded.png` the active PRACTICE card border and the three inactive cards' borders read the **same hue/brightness** at full-image inspection. The implementer's known-deviation #2 admits "Outline doesn't respect RectTransform corner-radius (the card is visually rounded via mask but Outline draws rectangular shadows offset). Visual approximation present." → Figma-vs-iter7 differentiation is not perceptible to the eye. Border-color swap may be wired, but the *visible* differentiation gate (the whole point of item 4) is not met. |
| 5 | Home PLAY visible on centred card in both states | PASS | CONFIRM-PASS (functional) / **but see item 6** | PLAY is present in both home_collapsed and home_expanded. However in home_collapsed it visibly overlaps the promo banner — that overlap is graded under §6.3-6 (content-hug height) below. |
| 6 | Card content-hug height; fee gap-24; separator above PLAY | PASS | **OVERRIDE-FAIL** | (a) **Visible overlap**: `iter7_final_home_collapsed.png` shows the PLAY button physically overlapping the GOLFIN-GPS promo banner — the card is not content-hugging, OR the PLAY/wrapper is laid out beyond the card's bottom edge into the banner zone. (b) **Separator above PLAY is UNWIRED**: `Assets/Prefabs/UI/ModeSelect/ModeCard.prefab` line **5448** and `ModeHomeCard.prefab` line **126** both show `separator3AbovePlay: {fileID: 0}` — the report's §6.3-15 claim "Separator3 GO created in both prefabs, wired to separator3AbovePlay" is **factually false in the prefab YAML**. No third separator can render. |
| 7 | Description 80px inset | PASS (approx) | CONFIRM-PASS | Visible in home_expanded and fullscreen_expanded: description text is clearly inset from card edges; not touching sides. Acceptable approximation. |
| 8 | Centred card 764w (side 677) | PASS | CONFIRM-PASS | Visible in home_collapsed/expanded: centre card is visibly wider than the partial peek of "NS / te challenges" on the left edge. Spec values present in `ModeCarouselController`. |
| 9 | Carousel scroll arrows removed | PASS | CONFIRM-PASS | No arrows visible in any of the four canonicals. |
| 10 | Chevron = expand/collapse on home centred card only | PASS (approx) | CONFIRM-PASS | Chevron wired in `ModeHomeCard.prefab` (`_showChevron: 1`); hidden in `ModeCard.prefab` (`_showChevron: 0`). Known deviation #3 admits ASCII `>` / `v` rather than icon glyph — acceptable approximation. |
| 11 | Back panel CardsContainer present | PASS | CONFIRM-PASS | Visible in both fullscreen canonicals — back panel clearly visible. Known deviation #1 (flat colour vs vertical gradient) is a flagged approximation; not a blocker on its own. |
| 12 | Card width 978, 48px inset inside 1074 panel | PASS | CONFIRM-PASS | Visible inset around all 4 cards inside the panel; widths look uniform and centred. |
| 13 | Locked overlay clipped to 978 rounded-50 card rect | PASS | CONFIRM-PASS | Lock glyph and dimming clearly confined to DRIVING RANGE / MISSIONS card rects; no bleed onto adjacent cards or panel. |
| 14 | PLAY separator → py-24 → PLAY → 24px bottom pad | PASS | **OVERRIDE-FAIL** | Pixel-level: in `iter7_final_fullscreen_expanded.png` the PLAY button visibly **overlaps the REWARDS row**; the REWARDS coin and "x50" are partially behind the yellow button. There is no visible separator + 24px vertical breathing room above PLAY. Root cause: per item 6, `separator3AbovePlay: {fileID: 0}` is unwired — the vertical-layout group cannot reserve space for a separator that doesn't exist, so PLAY collapses into REWARDS. |
| 15 | Third separator (978-wide, above PLAY) added | PASS | **OVERRIDE-FAIL** | Same root cause as 6/14: `separator3AbovePlay: {fileID: 0}` in BOTH prefabs. Report's claim that Separator3 was created and wired is contradicted by the YAML. (Same for `separator2UnderDesc: {fileID: 0}` — the under-description separator on the expanded card is also unwired, but I did not pixel-verify it; flagging here for the implementer.) |
| 16 | Per-card chevron hidden on full-screen list | PASS | CONFIRM-PASS | `ModeCard.prefab` `_showChevron: 0`; no chevrons visible in fullscreen captures. |
| 17 | ENTRY FEE / REWARDS labels on all cards (collapsed) | PASS | **OVERRIDE-FAIL** (rendering, not presence) | The labels are *present* — fullscreen_collapsed shows "ENTRY FEE", "REWARDS" text on each card — but they render so small/thin (combination of `m_fontWeight: 400` and `m_enableAutoSizing: 1` with `m_fontSizeMin: 14`) that they fail the spirit of §6.2 (Unity TMP 27.86, weight 600). Figma's collapsed-card labels read at a healthy fraction of title size; iter7's read at maybe 35-45% the title's height. This is the visible defect the architect predicted as "text reads SMALL". |

**§6.3 summary:** 11 PASS / **6 OVERRIDE-FAIL** (items 1, 4, 6, 14, 15, 17). Items 6 / 14 / 15 share a single root cause (unwired separators), and item 1 / 17 share a single root cause (weight 400 + aggressive auto-size on the new EntryFeeLabel / RewardsLabel TMPs).

---

## Step 4 — Visible defects → likely causes

1. **Visible defect:** Home-collapsed PLAY button overlaps GOLFIN-GPS promo banner. **Likely cause:** Card root not content-hugging in collapsed state, AND/OR the wrapper around PLAY (LayoutElement 144h) is included in the card layout but the parent card RectTransform's height is fixed rather than VLG-driven. The PLAY's world Y bottom ends up inside the promo banner's world Y top, with no overlap-prevention layout pass.
2. **Visible defect:** Fullscreen-expanded PLAY overlaps REWARDS row. **Likely cause:** `separator3AbovePlay` is `{fileID: 0}` in both prefabs (YAML proof). The VLG above PLAY has no separator child to space against, so PLAY climbs up into the REWARDS row.
3. **Visible defect:** ENTRY FEE / REWARDS labels render tiny and thin. **Likely cause:** Two compounding YAML facts:
   - `m_fontWeight: 400` on both new label TMPs (vs spec 600) → thinner glyph rendering.
   - `m_enableAutoSizing: 1` with `m_fontSizeMin: 14` and HLG width-constrained rows → auto-size aggressively shrinks the labels far below the 27.86 max.
4. **Visible defect:** Active/inactive cards in fullscreen-expanded read with the same border colour. **Likely cause:** The implementer's own known-deviation #2: `Outline` component used for the border draws a rectangular shadow offset, not a true rounded border. The colour swap from `#3E7CA8` to white may technically run, but the perceptual differentiation gate of §6.3-4 is not met.

---

## Step 5 — Capture-helper compliance

1. **Screenshot provenance.** Implementer report (line 48) declares `CaptureHelper.SnapGameViewWithLabel` — sanctioned. PASS.
2. **Maintenance protocol for new contexts.** No new `*Context.cs` files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` are added in the §6 REDO diff (verified via `git status` — only `Assets/Scripts/UI/ModeSelect/*` is touched). Maintenance protocol N/A. PASS.

---

## Step 6 — Bbox verification

Bbox `script-execute` not run live this pass. Rationale: the two containment-style claims that matter here — "PLAY overlaps banner" and "PLAY overlaps REWARDS row" — are **negative containment / collision** assertions (the violation is that one element bleeds into another's drawing area), not "is child inside parent" claims. The pixel evidence is unambiguous in both screenshots (the overlap is plainly visible and described in Step 1/2). Additionally the YAML proof for the root cause (unwired `separator3AbovePlay`) is deterministic on its own. Logging this as a pixel-evidence call rather than a live geometry call; if the architect wants a live geometry confirmation before signing off the fix, that's a reasonable ask but does not block this FAIL routing.

---

## Step 7 — Scene-mutation audit (`git diff Assets/Scenes/ShellScene.unity`)

Scene diff is large (+1380/-14 lines) but consistent with adding `CardsContainer`, `ScrollView`, separators (one or more of which are added at root then re-orphaned per HEARTBEAT), and `Outline` components for the back panel. Important narrow checks:

- **`m_IsActive: 1 → 0` transitions:** exactly **ONE** — the original `NextHolePanel` GO (fileID `446239784`) is deactivated. This is the legacy Home next-hole panel; the new `ModeCarouselSection` replaces its function. **Acceptable / spec-aligned** (the spec §0.1 names NextHolePanel as the clone source; the original being hidden once the carousel replaces it is intentional).
- New `Separator3` GO created (fileID `192942667`, `m_IsActive: 0`) at scene root — orphan / not parented inside any card. Likely a stray leftover from the in-prefab work that bled into the scene. **Flag for clean-up but not on its own a FAIL**; the report's "stray Separator2/3 root GOs destroyed" claim (line 28) doesn't fully match the diff which still shows a `Separator3` root GO in the scene diff. Recommend the implementer verify.
- No other unexpected manager/singleton GO mutations.

`git status` confirms no modified managers/singletons (`Assets/Scripts/UI/Roster/Managers/*`, `Scripts/UI/ScreenManager.cs`, etc.). The `ModeSelectFidelityFix.cs` script is **NOT present** (regression guard PASS — explicitly listed in spec as a deletion requirement).

---

## Step 8 — Production-flow capture check

All four canonicals are full-screen 1170×2532 production-frame captures. The home canonicals show the live HomeScreen with PromoBanner present; the fullscreen canonicals show the full Mode Selection screen as routed from the tee button (per report). No smoke-runner only. PASS.

---

## Prefab health quick-check (corruption regression guard)

- `ModeCard.prefab`: long-form 18-digit fileIDs only (Unity standard); no >19-digit overflow fileIDs. **No corruption.** Source GUID confirmed `8b72adc05329744348b02e5cddf5f4bd` (HoleCard.prefab).
- `ModeHomeCard.prefab`: same. **No corruption.** Source = `NextHolePanel`.
- `ModeSelectFidelityFix.cs`: not found anywhere under `Assets/Scripts/`. **Correctly deleted.**

---

## Verdict

**`BACK_TO_IMPLEMENTER` / `SELF_REVIEW_FAIL`.**

The §6.3 17-item REDO is not complete. Six items are factually unmet — three of them with deterministic YAML proof, the other three with unambiguous visible defects across the four canonicals. The three highest-impact defects (PLAY-over-REWARDS, PLAY-over-banner, label-rendering) all map back to two narrow fixes:
1. Wire `separator2UnderDesc` and `separator3AbovePlay` on **both** `ModeCard.prefab` and `ModeHomeCard.prefab` to real separator GO children inside the expanded content path. The report claims this is done; the YAML says it isn't.
2. Set `m_fontWeight: 600` on `EntryFeeLabel` and `RewardsLabel` in both prefabs (currently 400), AND raise / disable autosize on those labels so they render at ~27.86 Unity-px instead of shrinking to ~14-16px.

---

## Concrete fix list (every item is a blocker — re-walk §6.3 against fresh captures before resubmitting)

| # | Fix | Where |
|---|---|---|
| F1 | Wire `separator3AbovePlay` to a real Separator GO that is a child of the expanded-content container, sized 978-wide (full-screen) / card-width (home), 2px tall, set inactive by default (controller will enable on expanded+unlocked). | `Assets/Prefabs/UI/ModeSelect/ModeCard.prefab` and `Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab` — `separator3AbovePlay: {fileID: 0}` today. |
| F2 | Wire `separator2UnderDesc` similarly (under the description text, above ENTRY FEE row). | Same two prefabs — `separator2UnderDesc: {fileID: 0}` today. |
| F3 | Set `m_fontWeight: 600` on EntryFeeLabel and RewardsLabel TMPs (and audit every other TMP in both prefabs to confirm no other 400s slipped through). | `ModeCard.prefab` lines 431, 2138 (and audit ALL TMPs in `ModeHomeCard.prefab` too — only 9 weight values found, vs 14+ TMPs claimed in the report; double-check coverage). |
| F4 | Stop the new EntryFeeLabel / RewardsLabel TMPs from shrinking to ~14px under HLG width constraint: either disable auto-size (`m_enableAutoSizing: 0` + fixed 27.86) or raise `m_fontSizeMin` to ~24 so they cannot shrink below readable. Verify the HLG row has enough width budget for the label at 27.86 + coin + value. | Same TMPs. |
| F5 | Fix home-collapsed PLAY-vs-banner overlap. Either content-hug the card height to its actual contents (VLG on root + content-size-fitter), or — if the card is intentionally sized to include PLAY — push the PromoBanner down so its top sits below the card's bottom edge. Verify by bbox in the post-fix capture: PLAY world-corners must not intersect PromoBanner world-corners. | `ModeCarouselController` height-handling AND/OR `HomeScreen` `PromoBanner` Y position in scene. |
| F6 | After F1/F2 wire-up, verify fullscreen-expanded PLAY no longer overlaps REWARDS — the third separator + py-24 should reserve the breathing room. | Re-shoot `iter7_final_fullscreen_expanded.png`. |
| F7 | Address §6.3-4 visible border differentiation. The implementer's known-deviation #2 admits `Outline` doesn't respect corner-radius. Either (a) swap `Outline` for a sliced border `Image` that actually outlines the rounded card edge (matches Figma's 3px solid border), so the white-vs-blue swap is perceptually obvious, OR (b) escalate this specific item to the architect with concrete proposals — but do not silently leave it as a flagged-but-failing approximation. | Card root in both prefabs. |
| F8 | (Lower priority) Verify or clean up the stray root-level `Separator3` GameObject in `ShellScene.unity` (scene diff at `+GameObject: Separator3 m_IsActive: 0` around line 19999) — should not be a scene-root orphan. | `Assets/Scenes/ShellScene.unity`. |

After fixes, capture all four canonicals fresh (overwrite `iter7_final_*.png`) and re-submit with an updated checklist that confirms — with YAML citations — that separators are wired and TMP weights/sizes match §6.2.

---

## Files reviewed (paths, absolute)

- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/SPEC.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/IMPLEMENTER_REPORT.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/FIGMA_METRICS.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/screenshots/iter7_final_home_collapsed.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/screenshots/iter7_final_home_expanded.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/screenshots/iter7_final_fullscreen_collapsed.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/screenshots/iter7_final_fullscreen_expanded.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/screenshots/figma_13027-5212_home_collapsed.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/screenshots/figma_13027-10471_home_expanded.png`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/mode_select_system/screenshots/figma_13026-1924_fullscreen_modeselect.png`
- `/Users/cesar/Documents/GolfinRedux/Assets/Prefabs/UI/ModeSelect/ModeCard.prefab`
- `/Users/cesar/Documents/GolfinRedux/Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/ModeSelect/ModeCardController.cs`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scenes/ShellScene.unity` (diff only)
