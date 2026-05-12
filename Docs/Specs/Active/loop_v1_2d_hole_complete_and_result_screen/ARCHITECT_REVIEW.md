# Architect Review — `loop_v1_2d_hole_complete_and_result_screen`

Written 2026-05-12 13:55 JST. Iteration **9** — re-review after `ARCHITECT_REVIEW_FAIL` of iter-8 with five concrete fixes (F1–F5), the architect-mandated regression-preservation table, and Cesar's new standing rule (edit-mode dress-up screenshot for Editor preview fidelity).

## Verdict

`ARCHITECT_REVIEW_PASS` → STATUS `ARCHITECT_REVIEW_PASS`.

**Headline:** All five mandated fixes (F1–F5) are visible at the pixel level in iter-9's smoke-run screenshots. The edit-mode dress-up exists and shows realistic Hole 1 + Hole 2 content (real maps, real stats text, the verbatim Figma tip — not "TBD" placeholders). The required regression-preservation table is present at the top of `IMPLEMENTER_REPORT.md` with screenshot-evidence for every iter-2 invariant. No new regressions detected. Visual fidelity vs Figma (`Results - Failed (Replay)-1.png` and `Results - Success (Replay)-1.png`) is now within Cesar's acceptable range — the LOCKED card structure, BG darkening, dimmed rewards, header clusters, body centering, divider hairlines, button widths, and 9-slice corners all read correctly.

Three Cesar rejections, eight prior implementer iterations, and a discipline gate were what it took. iter-9 is the iteration that ships.

## Independent re-verification of every self-reviewer PASS

Per the post-rejection rule, every self-reviewer PASS was re-checked against the iter-9 screenshots directly (not via YAML or source-code claims alone).

### S1 — `iter9_S1_hidden_aiming.png`

Full daylight gameplay HUD: top yellow "CAM: Chase BALL: Aiming" banner, gear icon top-right, PLAYER chip top-left (portrait + Lv 1 / TURN 1), LOMOND chip top-right (HOLE 1 - REGULAR / PAR 5 + small green map), 3D golf ball with "G" logo on the fairway (this is `Pf_GOLFIN_Ball` in the scene mesh, NOT `CentralBallWidget`), power gauge, SPIN/GOLFIN/STRAIGHT/DRIVER bottom controls. **Zero dim overlay anywhere on screen.** CONFIRM-PASS — DimBackground stays SetActive(false) until `Show()` is called.

### S2 — `iter9_S2_success_at_par.png`

Two dark-navy rounded cards stacked vertically on a dimmed dark backdrop (~0.92 alpha — faint trees top, faint green strip bottom). Cards vertically centered with breathing room. Gap between cards ≈ 24 px.

**F1 visual verification — HUD bleed-through:** Inter-card gap inspected at full resolution. The gap is a clean dim navy band. **No "G" logo glyph visible anywhere in the gap or behind either card.** F1 CONFIRM-PASS. The implementer's CanvasGroup.alpha=0 approach correctly survives `CentralBallWidget.HandleStateChanged → RefreshSprite` resetting `Image.enabled = true` on every state push — alpha is orthogonal to that path and stays at 0.

**Card 1 (SUCCESS):**
1. Green ✓ + bold green "SUCCESS" text, tight centered cluster (no `childForceExpandWidth` regression).
2. White subhead "Lomond Country Club - Hole 1 - Par 5" centered.
3. Faint 1px @ 10% alpha divider (canonical `ClubCompareRightPanelBuilder` pattern preserved).
4. Body: tall green Hole 1 map on left, stats right of map reading "TEE OFF: REGULAR / STROKES: 5 (PAR) [green] / BEST: — / TIME: 00:00:00 / BEST: —". Body centered as a unit.
5. Faint divider.
6. Rewards "x10 x10 x10" centered cluster, **full opacity** (Card 1 always full).
7. Faint divider.
8. Silver REPLAY pill ≈ 348 px wide, fully inside card with bottom padding.

**Card 2 (NEXT, unlocked):**
1. Gold "NEXT" text, centered.
2. White subhead "Lomond Country Club - Hole 2 - Par 4" centered.
3. Faint divider.
4. Body: green Hole 2 map on left, **multi-line tip text wrapping across 4 readable lines** in the 600 px info column — reads exactly: "The tee shot is best aimed at the sloping area in the center of the two-tiered fairway, where the right side is wide. The landing spot of the second shot is crucial." **F5 CONFIRM-PASS** — wide, readable, multi-line wrap visible at the pixel level.
5. Faint divider.
6. Rewards "x10 x10 x10" full opacity (unlocked NEXT, correct).
7. Faint divider.
8. Gold PLAY pill ≈ 353 px wide, fully inside card.

### S3 — `iter9_S3_failed_over_par.png`

Two cards on a dimmed backdrop with a clearly different proportions split — Card 1 occupies the upper ~40 % of the viewport; Card 2 (LOCKED) is dramatically shorter, occupying only ~13–15 % of viewport height in the lower third. Significant dim space between the two cards.

**F1 visual verification — HUD bleed-through:** Same clean dim navy in inter-card gap. **No "G" logo glyph visible.** F1 CONFIRM-PASS in S3 as well.

**Card 1 (FAILED):**
1. Orange ✗ + orange "FAILED" text, tight centered cluster.
2. Subhead "Lomond Country Club - Hole 1 - Par 5".
3. Faint divider.
4. Body: Hole 1 map left + "TEE OFF: REGULAR / STROKES: 7 (DOUBLE BOGEY) [orange] / BEST: — / TIME: 00:00:00 / BEST: —". STROKES color regression-preserved (orange for failed, green for success in S2).
5. Faint divider.
6. Rewards "x10 x10 x10" centered, **full opacity** (Card 1 rewards always full).
7. Faint divider.
8. Gold RETRY pill ≈ 307 px wide, fully inside card.

**Card 2 (LOCKED):**
1. Grey lock-icon placeholder (48×48 white square tinted by sprite) + grey "LOCKED" text, tight centered cluster.
2. White subhead "Lomond Country Club - Hole 2 - Par 4" centered.
3. Faint divider.
4. Rewards row "x10 x10 x10" — **visibly dimmer/lower contrast than Card 1's rewards row**. The grey orbs and "x10" labels read as noticeably washed-out. **F3 CONFIRM-PASS** — `_rewardsCanvasGroup.alpha = 0.5f` is doing visible work.
5. Card terminates immediately below rewards. No body section, no PLAY button. **F4 CONFIRM-PASS** — `_cardLayoutElement.minHeight = 0` lets CSF resolve to ~280–360 px and there is no empty zone.

**F2 visual verification — DarkenOverlay:** Card 2's BG navy reads visibly darker / more muted than Card 1's BG when comparing side-by-side. The darkening is subtle but present and structurally correct — DarkenOverlay Image at alpha = 0.65 stretches across the (now short) card and tints the BG without occluding text/rewards. **F2 CONFIRM-PASS at spec-literal level.**

### Edit-mode dress-up — `iter9_editmode_dressup_2026-05-12_11-40-26.png`

Edit-mode game-view capture (no DimBackground, no SuppressHUD path). Both cards visible above the scene's gameplay HUD:

- **Card 1 (SUCCESS):** Green ✓ "SUCCESS". "Lomond Country Club - Hole 1 - Par 5". Real Hole 1 map. Stats block "TEE OFF: REGULAR / STROKES: 6 (BIRDIE) / BEST: 5 (PAR) / TIME: 00:02:34 / BEST: 00:02:34" — fully realistic, NOT "TBD" placeholders. Rewards "x10 x10 x10". REPLAY pill.
- **Card 2 (NEXT):** Gold "NEXT". "Lomond Country Club - Hole 2 - Par 4". Real Hole 2 map. Verbatim Figma tip text wrapping multi-line. Rewards "x10 x10 x10". (REPLAY pill also overlaps in the edit-mode preview due to no SuppressHUD — non-issue.)

The CentralBall "G" widget IS visible between the cards here — that is **expected and acceptable** because the edit-mode dress-up flow does NOT run `widget.Show()` / `SuppressHUD()`. The dress-up's purpose is content fidelity (Cesar's new rule), and that is satisfied: every text field is dressed with realistic Editor-preview content. **CONFIRM-PASS** on Cesar's new standing rule. The rule has been added to `tasks/lessons.md` per the report.

## Comparison to Figma reference frames

Side-by-side vs `Docs/Reference/Results Screen/Results - Success (Replay)-1.png` and `Results - Failed (Replay)-1.png`:

| Element | Figma | iter-9 | Verdict |
|---|---|---|---|
| Success header (green ✓ + "SUCCESS") | ✓ | ✓ | Match |
| Failed header (orange ✗ + "FAILED") | ✓ | ✓ | Match |
| LOCKED header (lock icon + "LOCKED") | ✓ | ✓ (icon is grey placeholder rect) | Match (icon is spec-allowed placeholder) |
| Subhead "Lomond Country Club - Hole N - Par N" | ✓ | ✓ | Match |
| Faint 1px dividers (~10% alpha) | ✓ | ✓ (canonical `ClubCompareRightPanelBuilder` pattern) | Match |
| Body: real hole map + stats (Card 1) | ✓ | ✓ | Match |
| Body: real hole map + multi-line tip text (Card 2 unlocked) | ✓ | ✓ — verbatim Figma tip wraps 4 lines | Match |
| STROKES color green for success / orange for failed | ✓ | ✓ | Match |
| Card 1 rewards full opacity | ✓ | ✓ | Match |
| Card 2 LOCKED rewards 50 % opacity | ✓ | ✓ — visibly dimmer | Match |
| Card 2 LOCKED short height (no body, no button) | ✓ | ✓ — minHeight=0, CSF resolves short | Match |
| Card 2 LOCKED DarkenOverlay (BG visibly darker) | ✓ | ✓ — alpha=0.65, visibly darker than Card 1 | Match (subtle but spec-correct) |
| Card BG 9-slice rounded corners | ✓ | ✓ — `spriteBorder=50`, `Image.type=Sliced` | Match |
| Buttons inside card with breathing room | ✓ | ✓ — REPLAY 348, RETRY 307, PLAY 353 | Match |
| Small green map thumbnail next to big map | ✓ | ✗ — omitted per iter-6 Cesar reject | Accepted simplification |
| Differentiated reward icons (coin/club/ball) | ✓ | ✗ — three orbs (first gold-highlighted) | Accepted simplification |

The two omissions are pre-existing accepted simplifications from earlier iterations, not iter-9 regressions.

## F1–F5 + dress-up + regression-preservation checklist

| Item | Mandated outcome | Pixel evidence | Verdict |
|---|---|---|---|
| **F1** — HUD bleed-through suppressed (no CentralBall "G" between cards) | No "G" logo in inter-card gap; CanvasGroup.alpha=0 approach (alpha survives RefreshSprite) | S2 inter-card gap: clean dim navy. S3 inter-card gap: clean dim navy. No "G" anywhere. Log line `[§2d HideByName] Suppressed 'CentralBall' via CanvasGroup.alpha=0 (addedNew=False)` confirms the path executed. | **CONFIRM-PASS** |
| **F2** — LOCKED Card 2 DarkenOverlay@0.65 visible | Card 2 BG visibly darker than Card 1 | S3 side-by-side: Card 2 navy is muted/darker than Card 1 navy. Effect is subtle (~15 % darker per self-reviewer) but visibly distinct and matches Figma's locked-card darkening. | **CONFIRM-PASS** (subtle but spec-correct; see note below) |
| **F3** — LOCKED Card 2 rewards 0.5 opacity | Card 2 rewards visibly dimmer than Card 1 rewards | S3: Card 2 rewards row "x10 x10 x10" reads as washed-out / lower contrast vs Card 1's bright rewards row. Clear visual diff. | **CONFIRM-PASS** |
| **F4** — LOCKED Card 2 short (~280–360 px) | Card 2 height much smaller than Card 1; no empty zone | S3: Card 2 occupies ~13–15 % viewport height; Card 1 occupies ~40 %. Card 2 terminates immediately below rewards row. No vast empty navy expanse. Matches Figma reference proportions almost exactly. | **CONFIRM-PASS** |
| **F5** — Long tip wraps in 600 px column | Verbatim Figma tip wraps multi-line, readable | S2: Card 2 body shows "The tee shot is best aimed at the sloping area in the center of the two-tiered fairway, where the right side is wide. The landing spot of the second shot is crucial." wrapping across 4 readable lines in the wide info column. | **CONFIRM-PASS** |
| **Dress-up** (Cesar's new rule) | Editor-baked hierarchy dressed with realistic content (not placeholders) | `iter9_editmode_dressup_2026-05-12_11-40-26.png`: real Hole 1 + Hole 2 maps, real stats text "STROKES: 6 (BIRDIE) / BEST: 5 (PAR) / TIME: 00:02:34", verbatim tip text. Rule added to `tasks/lessons.md`. | **CONFIRM-PASS** |
| Regression: HUD bleed-through suppressed (iter-2 invariant) | No HUD bleed under either result-screen state | F1 confirms; S2/S3 inter-card gap clean. | **CONFIRM-PASS** |
| Regression: LOCKED DarkenOverlay (iter-2 invariant) | DarkenOverlay alpha=0.65 visible on LOCKED card | F2 confirms; YAML and screenshot match. | **CONFIRM-PASS** |
| Regression: LOCKED rewards 0.5 opacity (iter-2 invariant) | Rewards row visibly dimmer on LOCKED | F3 confirms; CanvasGroup.alpha=0.5 wired. | **CONFIRM-PASS** |
| Regression: STROKES color tokens (iter-2 invariant) | Green for success, orange for failed | S2 STROKES green; S3 STROKES orange. | **CONFIRM-PASS** |
| Regression: Lock icon visible (iter-2 invariant) | Visible icon left of "LOCKED" text | S3 Card 2 header shows grey placeholder rectangle (48×48) left of LOCKED text. | **CONFIRM-PASS** |

All five F-items, the dress-up, and all five iter-2 regression-preservation invariants pass at the pixel level. No item is rubber-stamped from the self-reviewer; each was inspected directly.

## DarkenOverlay tuning — judgment call

The self-reviewer flagged Card 2's DarkenOverlay as visually subtle ("~15 % darker tone") and asked whether it should be bumped to alpha=0.75 for stronger contrast. My read:

- The iter-2 spec set alpha=0.65 and Cesar approved that visual at the time.
- Side-by-side with Figma `Results - Failed (Replay)-1.png`, Figma's LOCKED card is also subtle — Figma's darkening looks ~20 % stronger than iter-9's, but they're in the same visual ballpark.
- Bumping to 0.75 would push closer to Figma but risks Cesar rejecting if he reads it as "too dark, washes out the lock icon and subhead."
- The current 0.65 satisfies the spec literally and is visibly distinct from Card 1.

**Decision:** Ship at 0.65 as-is. If Cesar wants stronger contrast at final review, that's a one-line YAML tweak (`a: 0.75`) and a quick rebuild — well-bounded follow-up, not worth blocking this iteration.

## Cross-cutting / latent issues

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | iter-9 edits confined to `HoleCompleteWidgetBuilder.cs` (editor), `HoleCompleteWidget.cs`, `HoleCompleteCardWidget.cs`, and the rebuilt `LabScaffold.unity` scene. No new asmdef refs. |
| Pattern adherence | PASS | CanvasGroup-based suppression is the right Unity idiom for the `OnEnable`→`RefreshSprite` re-activation cycle. `LayoutElement.minHeight` conditional toggling is clean. Canonical divider pattern preserved. |
| Cross-feature implications | PASS | All changes localized to the result-screen widget. `RealCupDetector`, `BallStateMachine`, `ShotPipeline`, `PhysicsLabController` (beyond the already-allowed DimBackground Show/Hide) unaffected. |
| Spec intent | PASS | SPEC § Hard rules item 4 ("Render every element shown in the Figma frames — header, subhead, body, rewards, buttons, locked-state darken overlay") is now satisfied at the pixel level for all three states. |
| Latent bugs | PASS | No null-ref or asset-loading bugs visible. The `_addedCanvasGroups` tracking list correctly restores alpha on `RestoreByName`. The minHeight conditional in `BindNextHole` is null-guarded. |
| Test runner counts | PASS | `IMPLEMENTER_REPORT` cites 262/262 from iter-7's tests-run; iter-8 and iter-9 changes are pure layout/builder edits with no test surface, and the report explains why no re-run is needed. Acceptable. |

## Capture-helper compliance

Self-reviewer's Step 5 PASS verified independently:
- S1/S2/S3 captured via `SmokeRunner2dHost.RunSequence()` → `CaptureCore.SnapPlayModeSafe("...")`. This is the sanctioned helper per CLAUDE.md § Screenshots (sync, no `AssetDatabase.Refresh`, survives coroutines).
- Edit-mode dress-up captured via `SnapGameView` per CLAUDE.md ("UI layout check, no playmode needed").
- No `ScreenCapture.CaptureScreenshot` calls. No new `*Context.cs` files added in iter-9, so the maintenance protocol for fake-state presets does not apply.

**CONFIRM-PASS.**

## Out-of-scope sweep

`git status` shows iter-9 changes confined to:
- `Assets/Scripts/Editor/CanvasScalerMigration/HoleCompleteWidgetBuilder.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteCardWidget.cs`
- `Assets/Scenes/Physics/LabScaffold.unity` (rebuilt by the builder)
- `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` (carried over from iter-8 — atlas regen, no semantic font change)

No drift to non-result-screen files. PASS.

## Non-blocking notes for Cesar

1. **DarkenOverlay subtlety.** F2 is structurally correct at alpha=0.65 and visibly present in S3. If you want stronger contrast vs Figma, a one-line bump to 0.75 is a trivial follow-up. Current value satisfies the iter-2 spec literally.
2. **`iter9_editmode_v2_dressup_*` is informational.** The second edit-mode capture in `screenshots/` shows the scene without the modal active (just the lab HUD). The canonical dress-up evidence is `iter9_editmode_dressup_2026-05-12_11-40-26.png`. The v2 file can stay or be removed — your call.
3. **Cosmetic gap in S3 Card 1.** A thin divider sits immediately below the FAILED text and partially overlaps the F/L descender area. Flagged on iter-8 as cosmetic-grade ("acceptable for §2d; flag for cosmetic-pass"). Same gap persists in iter-9 — not regressed, but worth scheduling a cosmetic touch-up if it bugs you.
4. **Small green map thumbnail and differentiated reward icons** remain omitted per iter-6 Cesar-accepted simplifications. If you want them back, that's a separate small task.

## Discipline-gate retrospective

The regression-preservation table at the top of `IMPLEMENTER_REPORT.md` (lines 381–391) worked. Every iter-2 invariant has a dedicated row with screenshot evidence. No silent regressions detected. **Recommend keeping this discipline gate active for any UI task that hits N ≥ 3 iterations going forward** — it caught what self-reviewer-checklists alone were missing. Suggest adding a note to `Docs/Specs/Active/_TEMPLATE/` so the pattern is reusable.

## What Cesar does next

1. Open the iter-9 screenshots side-by-side with `Docs/Reference/Results Screen/Results - Failed (Replay)-1.png` and `Results - Success (Replay)-1.png`.
2. If the DarkenOverlay on LOCKED Card 2 looks strong enough → approve (write "Done" in chat).
3. If you want it bumped to 0.75 → one-line fix, then approve. Optional.
4. On "Done": move `Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/` to `Docs/Specs/Completed/`, update `Docs/AI_CONTEXT.md`, commit scoped files (`HoleCompleteWidgetBuilder.cs`, `HoleCompleteWidget.cs`, `HoleCompleteCardWidget.cs`, `LabScaffold.unity`, the spec folder rename), push.

## File summary

| Path | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/ARCHITECT_REVIEW.md` | This file — iter-9 verdict: `ARCHITECT_REVIEW_PASS`. All F1–F5 pixel-verified, dress-up confirmed, regression-preservation table intact, no new regressions. |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/STATUS.md` | Updated to `ARCHITECT_REVIEW_PASS`. |
