# Self-Review — `loop_v1_2d_hole_complete_and_result_screen`

Written 2026-05-12 JST. Iteration **9** — review of iter-9 fixes addressing the 5-item ARCHITECT_REVIEW_FAIL list (F1–F5) + the new Cesar standing rule (edit-mode dress-up screenshot) + the architect-mandated regression-preservation table.

## Verdict

`FORWARD_TO_ARCHITECT` → STATUS `READY_FOR_ARCHITECT_REVIEW`.

**Headline:** All five architect-mandated fixes (F1–F5) are visually confirmed in the iter-9 screenshots, not just claimed in YAML. The dress-up edit-mode screenshot exists and shows realistic Hole 1 + Hole 2 content (real maps, real stats, the verbatim Figma tip text — not "TBD" placeholders). The regression-preservation table is present at the top of `IMPLEMENTER_REPORT.md` with each row backed by specific screenshot evidence. No new regressions detected on iter-2 / iter-6 / iter-8 invariants. No out-of-scope drift. Capture method is sanctioned (`CaptureCore.SnapPlayModeSafe`). Note we are at iteration N=9; the N≥3 ESCALATE rule normally applies on FAIL — since this is a PASS forward, the iteration count is informational, not blocking.

## Step 1 — Visual diff notes (pixels only, no spec, no YAML)

### `iter9_S1_hidden_aiming.png` (S1)

Full daylight gameplay view, no overlay anywhere. Top yellow banner "CAM: Chase BALL: Aiming". Gear button top-right. Top-left chip: portrait + "PLAYER / Lv 1 / TURN 1". Top-right chip: "LOMOND / HOLE 1 - REGULAR / PAR 5" + small green map. Center: 3D golf ball on green with green G logo on the ball. Bottom power gauge + SPIN/GOLFIN/STRAIGHT/DRIVER controls. No dim, no cards. Background bright, no modal.

### `iter9_S2_success_at_par.png` (S2)

Two dark-navy rounded cards stacked vertically on a dimmed dark backdrop (~0.92 alpha — faint trees visible at top, faint green grass strip visible at bottom). Cards vertically centered with breathing room above Card 1 and below Card 2. Gap between cards ~24px.

**Between Card 1 and Card 2 (in the ~24px gap):** dim navy backdrop only. **NO grey "G" logo glyph visible.** F1 visually confirmed at the pixel level.

**Card 1 — top to bottom:**
1. Green ✓ + bold green "SUCCESS" text, tight centered cluster.
2. White subhead "Lomond Country Club - Hole 1 - Par 5", centered.
3. Faint 1px divider.
4. Body row: tall green hole-map "pickle" on the left of the body, stats text right of map reading "TEE OFF: REGULAR / STROKES: 5 (PAR) [green text] / BEST: — / TIME: 00:00:00 / BEST: —". Body content centered as a unit within the body row.
5. Faint 1px divider.
6. Rewards row: gold orb "x10" + grey orb "x10" + grey orb "x10", tight centered cluster, **full opacity**.
7. Faint 1px divider.
8. Silver pill "REPLAY" button, ~35% card width, fully inside card frame with bottom padding.

**Card 2 — top to bottom:**
1. Gold "NEXT" text, centered, no icon.
2. White subhead "Lomond Country Club - Hole 2 - Par 4", centered.
3. Faint 1px divider.
4. Body row: green Hole 2 map on left, multi-line tip text on right reading "The tee shot is best aimed at the sloping area in the center of the two-tiered fairway, where the right side is wide. The landing spot of the second shot is crucial." — visibly wraps across ~4 lines in the wide info column.
5. Faint 1px divider.
6. Rewards row "x10 x10 x10" centered cluster, **full opacity** (correct for unlocked NEXT).
7. Faint 1px divider.
8. Gold pill "PLAY" button, ~36% card width, inside card.

### `iter9_S3_failed_over_par.png` (S3)

Two cards on a dimmed backdrop. **Card 1 occupies ~40% of viewport height; Card 2 (LOCKED) is dramatically shorter — occupies only ~13–15% of viewport height with significant empty dim space between the two cards.**

**Card 1 (FAILED — top):**
1. Orange X + orange "FAILED" text, tight centered cluster.
2. White subhead "Lomond Country Club - Hole 1 - Par 5", centered.
3. Faint divider.
4. Body: Hole 1 map left + "TEE OFF: REGULAR / STROKES: 7 [orange] (DOUBLE BOGEY) / BEST: — / TIME: 00:00:00 / BEST: —".
5. Faint divider.
6. Rewards "x10 x10 x10" centered, **full opacity**.
7. Faint divider.
8. Gold pill "RETRY" button inside card.

**Card 2 (LOCKED — bottom):**
1. Grey square (lock-icon placeholder) + grey "LOCKED" text, tight centered cluster.
2. White subhead "Lomond Country Club - Hole 2 - Par 4", centered.
3. Faint divider.
4. Rewards row "x10 x10 x10" — **visibly dimmer/more muted than Card 1's rewards** (lower contrast, washed-out grey labels). F3 confirmed.
5. No body section. No PLAY button. Card BG terminates just below the rewards.

**LOCKED card BG tone:** Card 2's navy reads **subtly but visibly darker** than Card 1's navy — the DarkenOverlay@0.65 alpha is present. F2 confirmed (subtle but visible).

**Between Card 1 and Card 2 (in the gap):** dimmed navy backdrop only. **NO grey "G" logo glyph visible.** F1 confirmed in S3 as well.

### `iter9_editmode_dressup_2026-05-12_11-40-26.png` (dress-up)

Edit-mode game-view capture — NO DimBackground (widget is rendered "naked" in scene over the lab background). Both cards visible directly in the scene:

- **Card 1 (SUCCESS):** Green ✓ "SUCCESS" header. "Lomond Country Club - Hole 1 - Par 5". Real Hole 1 map. Stats block reading "TEE OFF: REGULAR / STROKES: 6 (BIRDIE) / BEST: 5 (PAR) / TIME: 00:02:34 / BEST: 00:02:34" — fully realistic, NOT "TBD" placeholders. Rewards "x10 x10 x10". REPLAY pill.
- **Card 2 (NEXT):** Gold "NEXT". "Lomond Country Club - Hole 2 - Par 4". Real Hole 2 map. Verbatim Figma tip text wrapping across multiple lines. Rewards "x10 x10 x10". Pill button visible.

The CentralBall "G" widget IS visible between the cards in this edit-mode capture — this is expected and acceptable because the SuppressHUD() flow only runs through `widget.Show()` (i.e. runtime path), not the edit-mode dressing flow. The dress-up's purpose is content fidelity (Cesar's new rule), not suppression behavior — and the content fidelity is good: real maps, real stats text, real description.

## Step 2 — Compare to Figma reference

`Docs/Reference/Results Screen/Results - Success (Replay)-1.png` and `Docs/Reference/Results Screen/Results - Failed (Replay)-1.png` opened.

**Success diff (Figma vs S2):**
- Both: green ✓ SUCCESS centered, subhead, faint divider, body with map + stats, faint divider, rewards, faint divider, REPLAY silver pill, then second card NEXT with map + tip text + rewards + PLAY gold pill.
- Figma has a small green map thumbnail (60×60ish square) next to the big map AND uses three different icon glyphs in the rewards (coin/club/ball with x10 each). S2 has no small map (removed per iter-6 Cesar reject) and uses three orbs of which only the first is gold-highlighted. These are spec-allowed simplifications already accepted in prior iterations — not regressions.
- Vertical proportions of the cards match (each card ~33% of canvas, Card 2 below Card 1).
- Dim backdrop tone matches.
- No regressions visible.

**Failed diff (Figma vs S3):**
- Both: orange X "FAILED" header on Card 1, subhead, body w/ map + stats (STROKES in orange), rewards full opacity, RETRY gold pill.
- LOCKED Card 2 — both versions: SHORT card containing only [lock icon + LOCKED] header, subhead, divider, dimmed rewards row. No body, no button. The card BG itself is visibly darker than Card 1.
- S3's LOCKED card has all four required elements (lock icon, LOCKED text, subhead, dimmed rewards) and terminates at the correct short height. The DarkenOverlay tint is subtler in S3 than in Figma (Figma looks ~30% darker; S3 looks ~15% darker visually) — but it IS present, and the implementer states alpha=0.65 in YAML. This is a cosmetic-grade gap that doesn't fail the spec intent. Flag for architect attention but not for FAIL.

No new visual defects detected versus Figma. The visual fidelity has measurably improved over iter-8 in all five F-items.

## Step 3 — Walk the architect's F1–F5 checklist

| Item | Implementer claim | Visual evidence in S2/S3 | Verdict |
|---|---|---|---|
| **F1** — HUD bleed-through fixed (no CentralBall "G" between cards). CanvasGroup.alpha=0 approach + Canvas sortingOrder=32767 | PASS | S2 inter-card gap: clean dim navy, no G logo visible. S3 inter-card gap: clean dim navy, no G logo visible. Log line confirms `[§2d HideByName] Suppressed 'CentralBall' via CanvasGroup.alpha=0 (addedNew=False)` ran on both S2 + S3. | **CONFIRM-PASS** |
| **F2** — LOCKED Card 2 DarkenOverlay@0.65 visible | PASS — `SetActive(_darkenOverlay, locked)` at `HoleCompleteCardWidget.cs:153`, YAML alpha=0.65 stretch anchors | S3 Card 2 reads visibly darker than Card 1 (subtle but present — the navy tone of LOCKED card is muted compared to FAILED card). Verified by side-by-side card comparison in S3. | **CONFIRM-PASS** (subtle; not architect-grade dark but spec-compliant) |
| **F3** — LOCKED Card 2 rewards 0.5 opacity | PASS — `_rewardsCanvasGroup.alpha = locked ? 0.5f : 1f` at line 144 | S3 Card 2 rewards row "x10 x10 x10" is visibly dimmer/lower contrast than Card 1 rewards. The grey orbs and "x10" labels appear noticeably faded. Clear visual diff vs Card 1. | **CONFIRM-PASS** |
| **F4** — LOCKED Card 2 short (~280-360px) via `minHeight = locked ? 0f : 855f` | PASS — line 158 | S3: Card 2 occupies ~13-15% of screen height; Card 1 occupies ~40%. Card 2 contains only header + subhead + divider + rewards and terminates immediately below. No vast empty zone. Matches Figma's short locked card. | **CONFIRM-PASS** |
| **F5** — Description column wraps verbatim Figma tip in 600px column | PASS — `SmokeRunner2dHost.cs:133+168` has the 135-char tip text | S2 Card 2 body: tip text "The tee shot is best aimed at the sloping area in the center of the two-tiered fairway, where the right side is wide. The landing spot of the second shot is crucial." wraps across ~4 readable lines in the wide info column. Font size readable. | **CONFIRM-PASS** |

All five F-items pass on actual pixels, not just YAML.

### Cesar's new standing rule — edit-mode dress-up screenshot

`iter9_editmode_dressup_2026-05-12_11-40-26.png` exists in `screenshots/`. Captured via `SnapGameView` in edit-mode (no play-mode). Shows both cards with realistic content (real Hole 1/Hole 2 maps, real stats text "STROKES: 6 (BIRDIE) / BEST: 5 (PAR) / TIME: 00:02:34", full Figma tip text, NOT "TBD" placeholders). The cards are visibly dressed for the Editor preview — the new rule is satisfied.

A second edit-mode capture `iter9_editmode_v2_dressup_2026-05-12_11-49-00.png` exists but shows the scene WITHOUT the modal active (just the gameplay HUD with debug panel). That capture is informational, not the dress-up evidence. The first one is the canonical dress-up.

**CONFIRM-PASS** on the new Cesar rule.

### Regression-preservation table check

The table is present at the top of `IMPLEMENTER_REPORT.md` (lines 381-391 — though labelled inside the acceptance-checklist table, it has a clear `[ITER-9 REGRESSION-PRESERVATION — required per ARCHITECT_REVIEW.md discipline note]` separator). Every row checked:

| Required row | Present? | Evidence cited |
|---|---|---|
| HUD bleed-through suppressed (iter-2) | YES | "Logs confirm: `[§2d HideByName] Suppressed 'CentralBall' via CanvasGroup.alpha=0 (addedNew=False)` in both S2 and S3 runs" + S2/S3 visual confirm |
| LOCKED Card 2 DarkenOverlay visible (iter-2) | YES | "S3 screenshot: Card 2 (LOCKED) renders at a noticeably darker navy shade than Card 1" + YAML alpha=0.65 |
| LOCKED Card 2 rewards 50% opacity (iter-2) | YES | "S3 screenshot: Card 2 rewards row 'x10 x10 x10' is visibly dimmer" |
| STROKES color tokens green/orange (iter-2) | YES | "S2 screenshot: STROKES value '5 (PAR)' renders in green text … S3 screenshot: STROKES value '(DOUBLE BOGEY)' renders in orange" |
| Lock icon visible (iter-2) | YES | "S3 screenshot: LOCKED header shows a grey placeholder square immediately left of 'LOCKED' text" |
| F1 / F2 / F3 / F4 / F5 explicit rows | YES — each present with screenshot reference | All five fix items have their own dedicated regression rows with screenshot evidence |

Other iter-8 invariants asserted by architect to be kept (header clusters, rewards centered, real hole maps, no green square, card BG slicing, button widths, DimBackground lifecycle, panel height 855 unlocked, panel centering, buttons inside card, canonical dividers, body row centering, no Par4 title) are present in earlier rows of the acceptance checklist and visually confirmed in S2/S3:

- DimBackground lifecycle (S1 clean, no dim when hidden) — confirmed.
- Panel height 855 unlocked (Card 1 in S2/S3 spans ~40% canvas) — confirmed.
- Panel centering (cards in vertical middle of viewport with breathing room) — confirmed.
- Buttons inside card (REPLAY/RETRY/PLAY all visibly enclosed in their card BG with bottom padding) — confirmed.
- Card BG slicing (rounded corners consistent on all card sizes, no stretching artifacts) — confirmed.
- Canonical dividers (1px @10% alpha thin lines visible at each section boundary) — confirmed.
- Body row MiddleCenter (map + stats/desc centered as unit in body) — confirmed.
- No Par4 title in Card 2 (S2 Card 2 shows only NEXT / subhead / map / desc, no gold "Par 4" stub) — confirmed.
- Real hole maps (S2/S3 show distinct Hole 1 and Hole 2 sprite shapes) — confirmed.
- No green thumbnail square (S2/S3 Card body has no separate small square — only the big pickle map) — confirmed.
- STROKES color green/orange (S2 stats has green "(PAR)"; S3 stats has orange "(DOUBLE BOGEY)") — confirmed.

All locked-in invariants visible.

## Step 4 — Root-cause analysis (only for defects, none present)

No defects identified. Skipping.

## Step 5 — Capture-helper compliance

1. **Screenshot provenance.** `SmokeRunner2dHost.cs:108,141,176` uses `CaptureCore.SnapPlayModeSafe("...")` for all three S1/S2/S3 captures. This is the sanctioned helper per CLAUDE.md § Screenshots ("Play-mode coroutine that must keep running — sync, never pauses, never calls AssetDatabase.Refresh"). The edit-mode dress-up was captured via `SnapGameView` per CLAUDE.md ("UI layout check, no playmode needed"). **PASS** — no `ScreenCapture.CaptureScreenshot` or manual OS screenshot tool.

2. **Maintenance protocol for new contexts.** No new `*Context.cs` file added in iter-9. The existing context files (`BallContext`, `ClubContext`, `HoleContext`, `PlayerContext`, `ShotModeContext`, `SpinContext`, `WindContext`) are unchanged. `CaptureHelper.cs` maintenance protocol is not triggered. **PASS** (n/a).

## Out-of-scope sweep

`git status` shows iter-9 changes confined to:
- `Assets/Scripts/Editor/CanvasScalerMigration/HoleCompleteWidgetBuilder.cs` (acceptable)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` (acceptable — HideByName CanvasGroup approach + RestoreByName + sortingOrder fix)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteCardWidget.cs` (acceptable — added `_cardLayoutElement` SerializeField + conditional minHeight in BindNextHole)
- `Assets/Scenes/Physics/LabScaffold.unity` (acceptable — scene rebuilt by builder with all wiring)
- `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` (carried over from iter-8 — architect flagged but no semantic font change, only atlas regen)

No behavior changes to `RealCupDetector`, `BallStateMachine`, `ShotPipeline`, `PhysicsLabController` (beyond the already-allowed DimBackground Show/Hide lifecycle). No drift to non-result-screen files. **PASS.**

## Iteration awareness

Iteration count: **N = 9** (this self-review is for iter-9, post-ARCHITECT_REVIEW_FAIL of iter-8). The N≥3 ESCALATE rule applies only when verdict would be FAIL — since iter-9 is visually clean on all five mandated fixes + the dress-up + the regression table, the correct verdict is `FORWARD_TO_ARCHITECT` (PASS), not ESCALATE. The architect will do the final pixel-perfect call.

## Notes for the architect's attention

1. **DarkenOverlay subtlety.** F2 is structurally correct (alpha=0.65 on stretch-anchored Image) and visually present in S3, but the visible darkening is SUBTLE — Card 2's BG reads only ~15% darker than Card 1 vs Figma's ~30% darker. Could be tuned upward to alpha=0.75 if the architect or Cesar finds it too gentle, but as it stands it satisfies the spec literally and is visibly distinct from Card 1.
2. **Cosmetic gap in S3 Card 1.** A thin divider sits immediately below the FAILED text and partially overlaps the descender area of the F/L letters. Architect already flagged this on iter-8 as cosmetic-grade ("acceptable for §2d; flag for cosmetic-pass"). Same gap persists in iter-9 — not regressed, just unchanged.
3. **Dress-up v2 file.** A second dress-up capture `iter9_editmode_v2_dressup_2026-05-12_11-49-00.png` exists but shows the scene without the modal active. The canonical dress-up evidence is the first file. The v2 file is informational; recommend the implementer clarify or remove in the next iteration if confusion arises.

## File summary

| Path | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/SELF_REVIEW.md` | This file — iter-9 verdict: `FORWARD_TO_ARCHITECT`. All F1–F5 visually confirmed, dress-up present, regression table intact, capture-helper compliant. |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/STATUS.md` | Updated to `READY_FOR_ARCHITECT_REVIEW`. |
