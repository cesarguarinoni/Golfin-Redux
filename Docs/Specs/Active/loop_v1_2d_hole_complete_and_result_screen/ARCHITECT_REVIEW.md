# Architect Review — `loop_v1_2d_hole_complete_and_result_screen`

Written 2026-05-12 (JST). Iteration **7** — final review after F1 (divider height) + F2 (Card 2 description visibility) fixes following iter-6 SELF_REVIEW_FAIL.

## Verdict

`ARCHITECT_REVIEW_PASS` → STATUS `ARCHITECT_REVIEW_PASS`.

**Headline:** Iter-7 cleanly resolves the two defects iter-6 introduced. The four `childControlHeight=true` flag flips (Card1 VLG, Card2 VLG, Card1 NextHoleInfoCol VLG, Card2 NextHoleInfoCol VLG) plus the divider hardening (`flexibleHeight=0` + `type=Simple` + `preserveAspect=false`) take the dividers from ~35px bright bars destroying surrounding text down to subtle ~2–4px subordinate lines that match Figma's separator style. All four iter-6 regressions are visually recovered: FAILED header readable, LOCKED header readable, stats block readable on both Card 1 instances, and the Card 2 description text renders as 3 wrapped lines below "Par —". All six CESAR_REJECTION (iter-5) items still hold. No new regressions, no out-of-scope drift, capture helpers used correctly.

I pixel-inspected both S2 and S3 directly before signing off — the iter-6 trap (the self-reviewer accepting iter-6 dividers that visually weren't right) has not repeated here. The iter-7 dividers are genuinely thin, not "less awful."

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | Iter-7 changes confined to `HoleCompleteWidgetBuilder.cs` (editor-only, `Assets/Scripts/Editor/CanvasScalerMigration/`) + a scene rebuild of `LabScaffold.unity`. No new asmdef refs introduced. The iter-6 `LookupNextHoleInfo` / `LoadLocalizationEN` helpers (in `Golfin.Physics.Viewer.HoleCompleteDriver`) read `Assets/Data/HoleDatabase.csv` + `Assets/Localization/LocalizationText.csv` via `AssetDatabase` under `#if UNITY_EDITOR` — avoids the `LocalizationManager` cross-asmdef dep that lives in Assembly-CSharp root. Acceptable workaround for editor-time data plumbing in §2d. |
| Pattern adherence | PASS | VLG `childControlHeight=true` paired with explicit `LayoutElement.preferredHeight` is the canonical Unity layout pattern for non-stretching children. Iter-4 used the same family of fixes on `childForceExpandWidth`; iter-7 closes out the same class of bug on the height axis. |
| Duplicated logic | PASS | No duplication. The `BuildDivider()` helper added in iter-6 is reused across all 6 divider sites (3 per card × 2 cards). Hole-map and CSV loading helpers are single-source in `HoleCompleteDriver` + mirrored once in `SmokeRunner2dHost` for capture-time pre-load. |
| Spec intent | PASS | SPEC §E mandates "Render every element shown in the Figma frames — header, subhead, body, rewards, buttons, locked-state darken overlay." Iter-7 puts every element in its correct visual weight: dividers subtle, content dominant. The iter-6 build had every element present-but-broken; iter-7 actually achieves the spec intent. |
| Cross-feature implications | PASS | The two flag flips are localized to result-screen card layout. They do not affect: cup detection, ball state machine, shot pipeline, HUD, top bar (not in scope), nav bar (not in scope), any other screen. |
| Latent bugs (null refs, asset load order) | PASS | The divider builder early-returns on `divSprite == null`; `LoadHoleMap` returns null gracefully for missing hole numbers (and the binding tolerates a null sprite — `_holeMapLarge.sprite = data.HoleMap` is a Unity-safe assignment); `LookupNextHoleInfo` falls back to "TBD" placeholders if HoleDatabase.csv has no row. No order-dependent code introduced. |

## Visual fidelity verdict

Per-element comparison against Figma references (`Docs/Reference/Results Screen/Results - {Success,Failed} (Replay).png`):

| Element | Figma | Iter-7 | Match? |
|---|---|---|---|
| Divider thickness | Thin, subtle white-ish lines, clearly subordinate to text | Thin (~2–4px effective), faint white (alpha ~0.35), subordinate to text on all 6 visible divider rows | YES — **iter-6 regression resolved** |
| Card BG rounded corners (50px) | Crisp on both cards | Crisp (iter-5 9-slice borders=50px still active) | YES |
| Card 1 SUCCESS header | Green ✓ + bold green "SUCCESS", tight centered cluster | Tight cluster, no divider band crossing | YES |
| Card 1 FAILED header | Orange ✗ + bold orange "FAILED", tight centered cluster | Tight cluster, **no band crossing — iter-6 regression resolved** | YES |
| Card 2 NEXT header | Gold "NEXT", centered | Gold "NEXT", centered, no icon | YES |
| Card 2 LOCKED header | Grey lock silhouette + grey "LOCKED" with a visible gap between glyphs | Lock icon + "LOCKED" present, header readable; lock icon glyph sits very close to / slightly overlapping the "O" of "LOCKED" | PARTIAL — pre-existing placeholder-asset issue (white 48×48 rect tint, not a real lock silhouette), unchanged from iter-6. Self-reviewer flagged correctly. Real lock SVG/PNG import is a §2e art task. |
| Subhead centered | Centered under header | Centered (iter-2 fix holds) | YES |
| Stats block (Card 1) | 5 readable rows right of hole-map | All 5 rows readable in S2 (Success) and S3 (Failed) Card 1 — **iter-6 regression resolved** | YES |
| Card 2 info block (NEXT body) | Map left + gold "Par N" + multi-line description | Map + gold "Par —" + 3-line wrapped placeholder description visible — **iter-6 regression resolved** | YES (structural; placeholder data acceptable per Q8) |
| Rewards centered | Tight centered cluster | Tight centered (iter-6 fix holds) | YES |
| Buttons inside card | All buttons inside rounded card BG | REPLAY/RETRY/PLAY all enclosed (iter-6 ContentSizeFitter fix holds) | YES |
| Button widths (348/307/353) | Figma-measured proportions | YAML-confirmed sizes hold from iter-5; 9-slice pill ends crisp | YES |
| Real hole maps | Real per-hole art | Lomond H1 + H2 sprites loaded (narrow vertical render due to 156-wide container — acceptable for §2d per spec) | YES |
| No green square | None | None visible (iter-6 thumbnail removal holds) | YES |
| Locked Card 2 dimmed rewards | Reduced opacity | Visibly dimmer than Card 1 rewards | YES |
| Card 2 darken overlay (locked) | Card 2 visually duller than Card 1 | Card 2 visibly darker (alpha=0.65 from iter-2 holds) | YES |
| No PLAY button on locked Card 2 | Hidden | Hidden | YES |
| HUD bleed-through suppressed | Modal-only | Clean dark backdrop in S2/S3 (no chip, no banner, no debug panel) | YES |
| Top bar / nav bar / sky photo | Visible in Figma | Excluded per Q3 lock | OUT-OF-SCOPE (intentional) |

## Specific verifications I performed (independently, not from the self-review)

Per the rule "when CESAR_REJECTION exists, re-verify every self-reviewer PASS independently" — Cesar rejected at iter-5 and this is the next architect-review pass after that rejection.

1. **Divider thickness (F1) — re-verified by pixel-inspecting both S2 and S3.** In the iter-6 captures (per iter-6 self-review fail), the dividers rendered as ~35px bright bars cutting through "FAILED", "LOCKED", and stats text. In the iter-7 captures, every divider position shows a thin faint horizontal line, clearly subordinate to surrounding content. The implementer's root-cause analysis (`childControlHeight=false` forced VLG to ignore `LayoutElement.preferredHeight=8` and instead distribute via `sizeDelta.y=0` → equal-share) matches both the iter-6 visual evidence and the iter-7 fix result. I do not rubber-stamp this — I checked the pixels. The dividers are thin.

2. **Card 2 description (F2) — re-verified.** In iter-6 the description text was a 0px-tall invisible TMP. In iter-7 S2 Card 2, I can see "Par —" in gold followed by a 3-line wrapped block reading "Next / hole tip / — TBD" in white. The fix (`infoColVLG.childControlHeight=true`) is structurally correct and the pixels confirm it.

3. **All six CESAR_REJECTION (iter-5) items still hold in iter-7.**
   - Dividers visible but subtle: confirmed (was over-corrected in iter-6, now correct).
   - Rewards centered: still tight centered clusters in both Card 1 and Card 2 reward rows on both S2 and S3.
   - Buttons inside card: REPLAY/RETRY/PLAY all visibly within the rounded card BG. ContentSizeFitter holds.
   - No green square: confirmed (no flat green thumbnail anywhere).
   - Real hole maps: Lomond Hole 1 + Hole 2 sprites visible (narrow vertical aspect is a known §2d limit — the 156-wide container constrains a wide map sprite into a tall narrow shape; acceptable per spec and prior architect-pass).
   - Card 2 hole-select info block: "Par —" + description text present (structural fix in place; placeholder strings are §2e data-resolution job).

4. **iter-6 regressions all visually recovered in iter-7.** I cross-checked each — FAILED header (S3 Card 1), LOCKED header (S3 Card 2), stats block on both Card 1 instances, description visibility on S2 Card 2.

## Test results

Per iter-7 IMPLEMENTER_REPORT: existing test baseline holds at `262 / 262 PASS / 0 FAILED / 0 SKIPPED`. The iter-7 surgical area is exactly `HoleCompleteWidgetBuilder.cs` (editor-only) + `LabScaffold.unity` scene rebuild. No new runtime code; no new tests required for layout flag changes (visual-only properties verified by screenshot). Spec test gate (N → N+9) was satisfied in iter-2 and remains satisfied.

## Capture-helper compliance

Self-reviewer Step 5 finding upheld. All three iter-7 captures use `CaptureCore.SnapPlayModeSafe` (sanctioned helper for long-running playmode coroutines, synchronous, no AssetDatabase.Refresh, coroutine-safe). No new `*Context.cs` files added in iter-7, so the maintenance-protocol-for-new-contexts obligation does not apply. PASS.

## Minor observations (not failures)

1. **Lock-icon glyph slightly overlaps "O" in "LOCKED"** (S3 Card 2). Pre-existing placeholder-asset behavior — the lock sprite is a 48×48 white-tinted square, not a real lock silhouette. Self-reviewer flagged correctly. Real art import is a §2e task.

2. **Description placeholder text is the literal "Next hole tip — TBD"** and renders at small font in 3 wrapped lines. The structural rect (148px-tall, word-wrap on) is correct and matches Figma layout. Final typography tuning and real CSV-resolved descriptions are §2e.

3. **Hole maps render as narrow vertical strips** because the container is 156px wide. Pre-existing from iter-6, acceptable per spec § Asset strategy.

4. **S3 stats numerically odd** ("STROKES: 1 (DOUBLE BOGEY)") — the SmokeRunner injects fake `strokes=par+2` test data with a low par for capture sequencing. The label-color binding (orange) is what matters for the visual-state evidence, and that is correct. Not a runtime bug; smoke-test artifact only.

## Decision

`ARCHITECT_REVIEW_PASS`. The iter-7 fixes are structurally sound, visually clean, and surgical. Both defects from iter-6 are demonstrably resolved by direct pixel inspection of the iter-7 captures (not deferred to the self-reviewer's analysis). No regressions, no out-of-scope drift, capture-helper protocol respected. The pipeline is ready for Cesar's final approval.

## What Cesar needs to do next

1. **Open `LabScaffold.unity`, enter Play mode** (foreground Unity before/during so the game loop ticks past the 5s startup wait — see iter-7 IMPLEMENTER_REPORT console output note about `Time.time` background throttling).
2. **Tap the central ball widget** (CentralBallWidget in Aiming state) to open `DebugShotPanel`, then tap the **"Hole Out"** button.
3. **Visually confirm:**
   - Success-at-par variant (run #1 with par matching turn count): green ✓ SUCCESS header, REPLAY silver pill inside card, Card 2 NEXT unlocked with PLAY gold pill, all 6 dividers thin/subtle.
   - Failed-over-par variant (adjust `HoleContext.Par` lower so `strokes > par`): orange ✗ FAILED header, RETRY gold pill, Card 2 LOCKED with darken overlay, dimmed rewards, no PLAY button.
4. **If satisfied:** type `Done` in chat. Claude moves the task folder to `Docs/Specs/Completed/`, updates `Docs/AI_CONTEXT.md`, commits the scoped iter-7 files (`HoleCompleteWidgetBuilder.cs` + `LabScaffold.unity`), and pushes.
5. **If something looks off in live Editor that didn't show in the captures:** write `CESAR_REJECTION.md` with the specific item and STATUS → `CESAR_REJECTED` — pipeline routes back to the implementer.

## Lessons captured

For `tasks/lessons.md` after Cesar approves:

- **Unity VLG height pitfall:** when a `VerticalLayoutGroup` has `childControlHeight=false`, it ignores all children's `LayoutElement.preferredHeight` and instead reads `RectTransform.sizeDelta.y`. For stretch-anchored children (`sizeDelta.y=0`), the VLG then distributes remaining height equally across all "zero-height" children — producing the surprising effect of thin elements rendering as fat bars and vice-versa. Always pair `childControlHeight=true` with explicit `LayoutElement.preferredHeight` for non-stretching children in VLGs. Same lesson applies symmetrically on the width axis via `childControlWidth` (iter-4 fix).

- **Divider sprite safety:** when a divider sprite has zero 9-slice borders, use `Image.Type.Simple` (not `Sliced`). `Sliced` with 0-borders is a no-op in geometry but can introduce stretching artifacts in some Unity versions. Also set `preserveAspect=false` for divider lines that should fill container width regardless of source aspect.

## Cesar's final approval

Cesar fills this section after eyeballing the live Editor one last time.

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: <...>
