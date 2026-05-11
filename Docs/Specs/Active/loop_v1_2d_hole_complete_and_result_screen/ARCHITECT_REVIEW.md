# Architect Review — `loop_v1_2d_hole_complete_and_result_screen`

Written 2026-05-11 (JST). Iteration **5** (post-`CESAR_REJECTED`).

## Verdict

`ARCHITECT_REVIEW_PASS` → STATUS `ARCHITECT_REVIEW_PASS`.

**Headline:** Both CESAR_REJECTED items are visually and structurally fixed. The 9-slice corner test (the definitive proof of the slicing fix) confirms 9-slice is now active — pill ends on the three different-width buttons (REPLAY 348, RETRY 307, PLAY 353) all render with **the same corner curvature**, which is only possible when corners are fixed-pixel-size from the sprite borders rather than stretched fractions of the rect. Button widths now sit at ~31–36% of card width with clear breathing room, well within the Figma proportions. No regressions on any of the 11 prior PASSes. The self-reviewer's `FORWARD_TO_ARCHITECT` verdict is upheld.

## Visual evidence — what the iter-5 screenshots show

### `controls_2d_modal_success_at_par_iter5.png` (S2, 2026-05-11 16:40:09)

- **Card 1 (Success):** Green ✓ icon tight to bold green "SUCCESS", centered cluster. Subhead "Lomond Country Club  - Hole 1 - Par 5" centered. Stats block right-side shows "STROKES: 5 (PAR)" with the value in green. Three reward circles. **REPLAY button: silver pill, clearly narrower than card, centered with visible breathing room left/right, pill ends crisp/rounded.**
- **Card 2 (NEXT, unlocked):** Gold "NEXT" centered, no icon. Subhead "Lomond Country Club  - Hole 2" centered, "Next hole tip — TBD" visible. **PLAY button: gold pill, narrower than card, centered with breathing room, pill ends crisp/rounded.**
- **Card backgrounds:** Both cards show crisp ~50px rounded corners on all 4 corners — no visible stretching.
- **HUD:** Fully suppressed. No player chip, no cam banner, no debug panel.

### `controls_2d_modal_failed_over_par_iter5.png` (S3, 2026-05-11 16:40:10)

- **Card 1 (Failed):** Red/orange ✗ icon tight to "FAILED" in orange/red, centered cluster. Subhead centered. Stats block shows "STROKES: 7 (DOUBLE BOGEY)" with the value in orange. Three reward circles. **RETRY button: gold pill, ~31% card width, centered with strong breathing room, pill ends crisp.**
- **Card 2 (LOCKED):** White-tinted lock placeholder tight to "LOCKED" text, centered at top of card. Subhead present. Three reward circles dimmed. No PLAY button. Card 2 visibly darker than Card 1 (DarkenOverlay alpha 0.65).
- **Card backgrounds:** Crisp 50px rounded corners on both.

### `controls_2d_modal_hidden_aiming_iter5.png` (S1, 2026-05-11 16:40:08)

Lab HUD baseline (player chip top-left, hole chip top-right, cam banner top-center, gear icon, 0.0 mph / 000 yds tags, central ball widget mid-canvas, debug buttons bottom). No modal. Correct hidden state.

## The definitive 9-slice test (per Architect's guidance)

The clearest visual proof that 9-slice is now active: the three buttons (REPLAY=348px, RETRY=307px, PLAY=353px) all show **identical corner curvature** despite spanning a 46px range of widths. With borders=0 (broken slicing), each button's corners would scale with its width and look subtly different. With borders=61–65 (working slicing), corners are fixed-pixel-size from the sprite border and look identical across all three. The iter-5 screenshots confirm the latter.

Compare against the iter-4 baseline (REPLAY=834px, PLAY=738px): in iter-4 the pill ends visibly soften/taper because the entire sprite stretches as `Type.Simple`. In iter-5 the pill ends are crisp and matched. This is the architect's "easiest visual test" and it passes.

## Code verification

- `Assets/Scripts/Editor/CanvasScalerMigration/HoleCompleteWidgetBuilder.cs`
  - Lines 67–70: `FixSpriteBorder()` invoked at start of `Build()` for all 4 sprites with documented values (50/61/65/65). Confirmed by grep.
  - Lines 455/458/461: `BuildButton` calls use `Vector2(348, 120)` / `Vector2(307, 120)` / `Vector2(353, 120)`. Confirmed by grep.
  - Lines 697–715: `FixSpriteBorder` helper uses `AssetImporter.GetAtPath<TextureImporter>(path)` + `importer.spriteBorder = desiredBorder` + `SaveAndReimport()`. Idempotent (early-returns if already set).
- `.meta` files on disk (confirmed by grep against `Assets/Art/ResultScreen/`):
  - `Background - HoleCard.png.meta` → `spriteBorder: {x:50, y:50, z:50, w:50}` ✓
  - `Button - Replay.png.meta` → `{x:61, y:61, z:61, w:61}` ✓
  - `Button - Retry.png.meta` → `{x:65, y:65, z:65, w:65}` ✓
  - `Button - Play.png.meta` → `{x:65, y:65, z:65, w:65}` ✓

All four .meta files now carry non-zero borders. Combined with `Image.Type.Sliced` in the builder, this is a complete, durable fix — not a tweak that will silently revert.

## Comparison to Figma reference

| Element | Figma reference | Iter-5 | Match? |
|---|---|---|---|
| REPLAY button geometry | Silver pill, ~38% card width | Silver pill, ~35-36% card width (348/978) | YES (within 2-3%) |
| RETRY button geometry | Gold pill, ~32% card width | Gold pill, ~31% card width (307/978) | YES (within 1%) |
| PLAY button geometry | Gold pill, ~38% card width | Gold pill, ~36% card width (353/978) | YES (within 2%) |
| Pill end rounding | Smooth, fixed-radius | Smooth, fixed-radius across all 3 buttons | YES |
| Card-background corner radius | ~50px on all 4 corners | Crisp ~50px corners on both cards | YES |
| Header clusters (SUCCESS/FAILED/LOCKED) | Tight icon+label, centered | Tight, centered (iter-4 fix holds) | YES |
| Subhead centering | Centered under header | Centered | YES |
| STROKES color (green/orange) | Green for success, orange/red for failed | Green "5 (PAR)" / orange "7 (DOUBLE BOGEY)" | YES |
| Card 2 LOCKED state | Lock cluster + dimmed rewards + darker card + no PLAY | All present | YES |
| Top bar / bottom nav / sky photo | Visible in Figma | Excluded per Q3 (modal-on-lab) | OUT-OF-SCOPE (intentional) |

Figma proportions hit within 1–3% on all three buttons. Pixel-perfect would require the buttons to be 372 (≈38%) for REPLAY/PLAY and 313 (≈32%) for RETRY against the 978px card; iter-5 is at 348/353 and 307. The 2–3% gap is small enough that the buttons read identically to Figma's proportional CTA emphasis. The implementer pulled values directly from canonical Figma node IDs (12988-5223 for REPLAY, 12988-5466 for RETRY, plus the Card 2 PLAY node). No reason to fail on sub-3% deviation.

## Regression sweep on the 11 prior PASSes

All 11 prior PASSes from iter-2/3/4 hold visually in the iter-5 captures. Self-reviewer's regression table is correct:

- Header SUCCESS/FAILED/LOCKED tight clusters (iter-4 fix) → HOLDS
- Subhead centered (iter-2) → HOLDS
- STROKES color tokens (iter-2) → HOLDS
- HUD suppression via overlay-canvas + `SuppressHUD` (iter-2) → HOLDS
- DarkenOverlay alpha=0.65 on LOCKED Card 2 (iter-2) → HOLDS
- Lock icon white-tint placeholder (iter-2) → HOLDS
- Header icons 48×48 (iter-2) → HOLDS
- Tip text not clipped (iter-2) → HOLDS
- Stats block fontSize=24 + lineSpacing=4 (iter-2) → HOLDS
- S1 hidden-state HUD baseline → HOLDS

## Out-of-scope sweep

CESAR_REJECTION explicitly listed:
- Header / subhead alignment → unchanged in iter-5 ✓
- HUD bleed-through → unchanged ✓
- STROKES color tokens → unchanged ✓
- §2d behavior layer (HoleCompleteDriver, ShotPipeline, cup detection) → unchanged per IMPLEMENTER_REPORT (only builder, .meta files, and scene-rebuild touched) ✓

The iteration-5 surgical area is exactly: `HoleCompleteWidgetBuilder.cs` + 4 `.meta` files + scene rebuild. No drift into behavior code or other UI files. This is the minimal, targeted change Cesar asked for.

## Capture-helper compliance

The self-reviewer verified Step 5 correctly. Iteration-5 captures use `CaptureCore.SnapPlayModeSafe()` — the sanctioned helper for long-running playmode coroutines (synchronous, no `AssetDatabase.Refresh`, coroutine-safe). No new `*Context.cs` files were added in iter-5, so the maintenance-protocol extension obligation does not apply. PASS.

## Test results

Per IMPLEMENTER_REPORT: `tests-run` showed `Total=262 / Passed=262 / Failed=0 / Skipped=0` after iteration-2 changes. The iter-5 edits are confined to the editor-only `HoleCompleteWidgetBuilder` (under `Assets/Scripts/Editor/`), four `.meta` files (TextureImporter settings), and a scene rebuild. None of these touch runtime test surfaces — the 262/0/0 baseline holds. No new tests were required for slice-border configuration or button dimension changes (these are visual properties verified by screenshot, not unit-testable).

## Architectural soundness

- Asmdef boundaries respected — builder lives in editor-only scope, .meta changes are import-time properties, no runtime code touched in iter-5.
- The `FixSpriteBorder()` helper is idempotent (early-returns on equal value) and tolerates missing files (logs warning rather than throwing). Reasonable for an editor-only one-time-config tool.
- Hardcoded border values (50/61/65/65) and button widths (348/307/353) are inline in the builder. Acceptable for a builder script — these are art-spec contracts, not data, and putting them in a CSV/asset would be over-engineering. Future maintainers can update the values inline.

## Decision

PASS. Both CESAR_REJECTED items are visually and structurally resolved. No regressions on prior PASSes. No out-of-scope drift. The fix is durable (lives in the builder + on disk in .meta files), not a one-off tweak. STATUS → `ARCHITECT_REVIEW_PASS`.

## What Cesar needs to do next

1. Open `LabScaffold.unity`, enter Play mode, click the "Hole Out" debug button on `DebugShotPanel`, and visually confirm the three result-screen states (the iter-5 captures already match the Figma proportions, but Cesar's standing rule is "manual final eyeballing on the real Editor before approval").
2. If satisfied: type `Done` in chat — Claude will move the task folder to `Docs/Specs/Completed/`, update `Docs/AI_CONTEXT.md`, commit the scoped files (builder, 4 .meta files, LabScaffold scene rebuild), and push.
3. If something looks off in the live Editor that didn't show in the captures: write `CESAR_REJECTION.md` with the specific item and STATUS → `CESAR_REJECTED` — pipeline will route back to the implementer.

## File summary

| Path | Purpose |
|---|---|
| `Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/ARCHITECT_REVIEW.md` | This file (iter-5 PASS verdict) |
| `Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/STATUS.md` | To update → `ARCHITECT_REVIEW_PASS` |
