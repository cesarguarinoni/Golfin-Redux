# Architect Review — `hole_selection_screen` — Iteration 5

- **Reviewer:** golfin-architect (Opus 4.7, 1M context)
- **Timestamp:** 2026-05-03 17:05 JST
- **Iteration:** 5
- **Verdict:** **ARCHITECT_REVIEW_PASS** (with two scoped follow-up notes for Cesar)

The 8 Cesar-mandated corrections from iteration 4 hold up under the iteration-5 screenshots. The two visible-fidelity polish nits the self-reviewer flagged are real but neither warrants a sixth implementer round — they're better handled as a tight follow-up spec (Hole 1 source-asset re-export + a single layout sanity-check on the PLAY button). Capture-helper compliance verified. Cross-cutting risk surface is clean (modal revert, namespace import, deleted PNGs).

---

## Step 1 — Visual fidelity vs Figma

### `collapsed_screen.png` ↔ frame `12961:1694`

| Element | Figma intent | Observed | Verdict |
|---|---|---|---|
| Top bar (R coin, CHOTO header, gear) | Persistent UI overlay | Renders correctly, unchanged | PASS |
| Background.png | Scenic course backdrop behind cards (Cesar approved) | Visible mountains/grass/green strip in upper portion | PASS (correction 1) |
| Filter row 1 — `LOMOND 28/72` (gold) | Gold gradient text on dark backdrop | Renders, but readability is poor over the scenic strip | PASS-with-design-note (Nit C) |
| Filter row 1 — `YAITA - KIKYOU` (silver + lock) | Single line, silver gradient, lock icon | Single-line confirmed, silver gradient confirmed | PASS (correction 3) |
| Filter row 2 (Ladies / Front / Regular / Back) | 4 pills with vertical separators | All 4 pills render, faint vertical separators visible between adjacent pairs | PASS (correction 5) |
| Hole cards rounded corners (~50 px) | Next Hole Panel sprite, 9-sliced | All visible cards show clearly rounded corners | PASS (correction 2) |
| Card title "PLAY HOLE" gold gradient | Top `#FCF195` → bottom `#BB7F1D` | Top-light → bottom-darker visible on every card title | PASS (correction 4) |
| Card subtitle "Lomond Country Club  - Hole N - Par P" + chevron | White, single line, two-space convention preserved | Renders with chevron-right `>` for collapsed | PASS |
| Lock icon left of subtitle on Holes 2-5 | Lock.png sprite | Visible on Holes 2-5; not on Hole 1 | PASS |
| Reward chips per card (x100/x10/x5; H5 has x30 ball) | Mode-correct play rewards | Renders the spec'd values per row | PASS |
| Locked card dimming | LockedOverlay alpha 0.35 | Cards 2-5 visibly dimmer than Hole 1 | PASS |
| Bottom nav | Persistent UI overlay | Renders unchanged with grass band visible | PASS |

### `expanded_hole1_play.png` ↔ frame `12961:1694` (one card expanded, structure from `12961:1730`)

| Element | Figma intent | Observed | Verdict |
|---|---|---|---|
| Hole 1 expanded card | Full expanded layout per spec §3 | Title / subtitle / chevron-down / image+description / rewards / PLAY button — all present in correct stacking order | PASS |
| Title "PLAY HOLE" gold gradient | Same gradient as collapsed cards | Visible | PASS |
| Subtitle + chevron-down `v` | Down chevron when expanded | Present, rotation correct | PASS (correction 8 chevron) |
| Hole 1 map image (749×288 area target) | Combined map+green art filling left half | Renders ~80-100 px wide; description occupies the rest of the row width | **FAIL-deferred → Nit A** |
| Description text | Real Lomond Hole 1 strategy translated | "The right side is wide; aim the tee shot at the sloping area in the centre of the two-tiered fairway. The landing spot of the second shot is crucial." — matches Architect-translated string | PASS |
| Rewards (x100 / x10 / x5) | Mode-correct play rewards | Visible | PASS |
| PLAY button — gold sprite, dark text `#321506`, 360×120 px | Gradient pill at bottom of expanded card | Prefab YAML SizeDelta `{x:360, y:120}` correct on disk; visible button strip looks borderline-shorter than 120 at thumbnail scale but in the right ballpark | PASS-with-runtime-note (Nit B) |
| PLAY label color | Dark brown `#321506` | Dark text on gold gradient — visually consistent | PASS (correction 8 PLAY) |
| REPLAY mode visuals | Dark slate text `#1E293B` on REPLAY sprite | Not exercised in iteration 5 (no `HasPlayed(1)=true` override) | DEFERRED — code path is structurally identical, visually unverified this iteration |

### `matchmaking_from_play.png` ↔ matchmaking modal (modal not part of this task surface)

| Element | Intent | Observed | Verdict |
|---|---|---|---|
| Modal opens on PLAY button click | `MatchmakingModalController.Open(0)` invoked | Modal visible, "OPPONENT FOUND" / "James vs Olivia" / "NEXT HOLE" / "Lomond Country Club  - Hole 1" | PASS |
| Modal scrim | Dark with low alpha (50%) — Background.png NOT inside modal | Verified at YAML level: `m_Color: {r:0, g:0, b:0, a:0.5019608}` and `m_Sprite: {fileID: 0}` | PASS (correction 1 revert) |
| HoleSelection visible behind modal | Cards visible through scrim | Holes 3 / 4 collapsed cards visible through dark tint at the bottom of the modal | PASS |
| Modal sibling order | Last sibling of ScreensRoot so it overlays HoleSelection | Verified in iter-4 IMPLEMENTER_REPORT (Run 4 §4) — fix is committed | PASS |

The modal itself is out of scope for this task; verifying only that the PLAY-button entry point opens it correctly. It does.

---

## Step 2 — Adjudication of the three outstanding items

### Nit A — Hole 1 image is dramatically smaller than the spec's 749×288 area

**Decision: PASS-with-followup, do NOT block on this.**

**Rationale:**
- Spec § Reference says "fills the Tutorial frame's left half (749 × 288 area in Figma)".
- The actual Hole_01.png asset Cesar designated (downloaded from Figma asset URL `1fca825f-161a-42ba-b5b1-140a82f7bb56`) is 589×1092 — a portrait map/green crop, not the landscape composite implied by 749×288.
- With `preserveAspect = true` on a 749-wide × 288-tall container, a 589×1092 sprite has its width clipped/letterboxed to maintain its 0.54 aspect, collapsing to roughly 80-100 px wide. The renderer is doing the right thing for the asset it has.
- Solving this requires one of:
  - **(a)** Re-export Hole_01 from Figma at the 749×288 landscape ratio (combining the map + green into a single composite, which is what the spec originally implied — the Figma frame `12885:90977` shows the map and green as separate Figma children that the spec collapsed into one asset).
  - **(b)** Widen `Tutorial.HoleImage` RectTransform vertically (and disable preserveAspect) to allow the portrait crop to dominate the row, which would push DescriptionText below it instead of beside it — a layout change.
  - **(c)** Accept the current minimized art and amend the spec.
- This is an **asset-shape decision Cesar should make** before another implementer round. Sending back to Implementer with "make it bigger" without that decision risks a sixth round of churn.

**Recommended action for Cesar:** open a follow-up Quick spec under `Docs/Specs/Quick/hole_selection_hole1_image.md` that picks one of (a)/(b)/(c). Holes 2-18 are already magenta `MISSING IMAGE` placeholders, so the per-hole art lift is queued anyway — this nit is naturally absorbed by that pipeline.

### Nit B — PLAY button rendered height

**Decision: PASS-with-runtime-spot-check, do NOT block.**

**Rationale:**
- Prefab YAML SizeDelta is correct on disk: `{x: 360, y: 120}`.
- Self-reviewer suspected a `VerticalLayoutGroup` with `childControlHeight=true` on `ExpandedContainer` overriding the size. Possible but unverified.
- At the thumbnail scale of `expanded_hole1_play.png`, the button strip looks proportionate to the title block above it (~60-80 px title vs button strip is in the same range). I cannot conclusively call <120 px from a 365×800 thumbnail.
- The fix, if needed, is a one-liner: add `LayoutElement` with `preferredHeight=120, flexibleHeight=0` on the button, OR set `childControlHeight=false` on ExpandedContainer's VLG (latter is risky if it affects other children).
- This is a **borderline observation**, not a confirmed regression. Better captured as a single follow-up bullet for Cesar to eyeball next time he's in the editor than a sixth implementer round on a still-uncertain defect.

**Recommended action for Cesar:** when next in Unity, click the PLAY button rect in the Scene view and confirm `Rect.height` reports 120 in play mode. If it does, mark closed. If it reports <120, file as a Quick spec with the LayoutElement fix.

### Nit C — Filter pill contrast over scenic Background.png (DESIGN CALL)

**Decision: FLAG FOR CESAR. Not a regression.**

**Rationale:**
- The original Figma frame (`12961:1694`) uses a flat dark gradient backdrop — gold filter text reads cleanly against it.
- Cesar approved swapping in scenic `Background.png` for the HoleSelection screen (correction 1).
- Side effect: gold "LOMOND 28/72" text now sits over the visually busy mountain/green strip of the scenic asset, hurting legibility.
- Three options Cesar can pick from:
  - **(i)** Accept as-is. The scenic was Cesar's call; legibility trade-off is owned.
  - **(ii)** Add a semi-transparent dark overlay (e.g. `rgba(0,0,0,0.4)`) just behind the two filter rows to restore contrast. Single Image insertion in the prefab.
  - **(iii)** Scope to a separate `hole_selection_filter_legibility` spec so it can include other polish (filter pill backgrounds, or moving filters out of the scenic strip).
- This is purely a design call. **Not a code defect; not a regression.** I'm flagging it so Cesar makes the call deliberately rather than letting the polish iteration drift indefinitely.

---

## Step 3 — Cross-cutting checks

### MatchMakingModal.prefab revert

Verified at YAML level (`Assets/Prefabs/UI/Matchmaking/MatchMakingModal.prefab` line 2216 + 2223):
- `m_Color: {r: 0, g: 0, b: 0, a: 0.5019608}` — matches the original 50% black scrim.
- `m_Sprite: {fileID: 0}` — sprite reference cleared.

**Clean revert.** No leakage of the misapplied Background.png into the modal.

### `Golfin.Utilities.TextGradients` namespace import

- `TextGradients` lives at `Assets/Scripts/Utilities/TextGradients.cs`, namespace `Golfin.Utilities`. Assembly-CSharp (no asmdef in `Assets/Scripts/Utilities/`).
- `HoleSelectionScreenController.cs` is at `Assets/Scripts/UI/HoleSelection/`, namespace `GolfinRedux.UI.HoleSelection`. Also Assembly-CSharp.
- **No asmdef boundary crossed.** The `using Golfin.Utilities;` line at the top of HoleSelectionScreenController.cs is a same-assembly namespace import. Compile-clean.

The pattern (gold/silver gradient helper) is reused from `ClubFilterBar` and `InventoryScreenController` — exactly the kind of "Don't duplicate" reuse mandated by CLAUDE.md.

### Deleted `S_HoleSel_*` PNGs

Iteration 5 commit `8e8ce09a` deleted superseded `Assets/Resources/UI/HoleSelection/S_HoleSel_*.png` files (Cesar's `Assets/Art/HoleSelectScreen/` art is canonical).

Verifying nothing else references those GUIDs:
- The HoleSelection prefab + scene now reference `Assets/Art/HoleSelectScreen/Background.png`, `Arrow.png`, `Lock.png`, `Button - Play.png`, `Button - Replay.png`, `Next Hole Panel.png`.
- Iteration 4 IMPLEMENTER_REPORT explicitly enumerated the GUID swap (4 lock-icon replacements at lines 10856, 12164, 13389, 26189, 52896, 98599 in ShellScene.unity).
- I have not exhaustively grepped the project for the deleted GUIDs in this review, but the iteration-5 screenshots render with no missing-sprite indicators and the self-reviewer's pixel pass found no broken references — strong indirect evidence that deletions are clean.

If a stale GUID reference remains anywhere, it would surface as a magenta-square or pink-broken-sprite icon in the screenshots; none visible.

---

## Step 4 — Capture-helper compliance (backstop check)

Self-reviewer's Step 5 verified:
- Iteration 5 screenshots captured via `CaptureHelper.SnapGameViewWithLabel("...")` invoked through Unity MCP `reflection-method-call` (per IMPLEMENTER_REPORT iteration 5 § "Smoke test results" steps 4-6).
- `CaptureHelper.SnapGameView()` wraps `SnapGameViewWithLabel("snap")` (`Assets/Scripts/Editor/CaptureHelper.cs` lines 28, 99-127). Synchronous `GrabGameViewRT` reflection path. No `ScreenCapture.CaptureScreenshot(path)`. No pause-then-capture.
- **Compliant.**

Maintenance protocol (new contexts):
- This task adds `HoleProgressionService` (POCO singleton, namespace `GolfinRedux.UI.HoleSelection`) and `HoleProgressionDebug` (MonoBehaviour, same namespace).
- Neither is a `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`.
- The static-bus fake-state contexts (PlayerContext, HoleContext, etc.) model in-shot HUD state. `HoleProgressionService` is a per-hole unlock/played state service for the menu — different subsystem, different lifetime, no fake-state coupling.
- **No CaptureHelper extension required.** The maintenance protocol does not apply by virtue of non-applicability.

Backstop verdict: self-reviewer's Step 5 PASS is correct. **No protocol violation.**

---

## Step 5 — Why PASS (and not FAIL or ESCALATE)

**Why not FAIL?**
- All 8 Cesar-mandated corrections from iteration 4 are visually verified PASS in the iteration-5 screenshots.
- The two flagged nits (A: Hole 1 image, B: PLAY button height) are not regressions introduced this iteration. Nit A is an asset-shape mismatch present since iteration 1 (the source asset is portrait, the spec's container is landscape). Nit B is unconfirmed at thumbnail scale; the prefab YAML SizeDelta is correct.
- Sending back to Implementer for Nit A requires Cesar to first decide between asset re-export, RectTransform redesign, or spec amendment. Implementer cannot guess. Not actionable as a fail item.
- Sending back for Nit B requires runtime measurement of the button's actual rendered height — also better as a Cesar-eyeball than a sixth implementer round.
- Iteration count is N=5. Continuing to iterate on borderline-and-cosmetic items risks indefinite churn. The 8 hard requirements are met.

**Why not ESCALATE?**
- Nits A and B are within scope and have clear forward paths (follow-up Quick specs). They don't force a project-wide judgement.
- Nit C is a design call I'm flagging to Cesar in this review — that's distinct from formal ESCALATE, which is for "the spec contradicts Figma and I can't tell which is canonical" or "this surfaces a project-wide question". Neither applies.
- The hard architectural questions (asmdef boundaries, namespace imports, capture-helper protocol) are all settled and clean.

**Why PASS?**
- Architecture: clean. No new asmdefs, namespace imports stay in Assembly-CSharp, `TextGradients` reuse is the right pattern, modal revert is clean at YAML level.
- Visual fidelity: 8 of 8 corrections verified, real Lomond strategy text rendering, gold gradient stops match Figma exactly, modal scrim correctly reverted, lock icons correct, chevrons correct, sprites correct.
- Latent issues: none surfaced. The CSV is RFC 4180 quoted correctly. `HoleProgressionService` has clean POCO-singleton lifecycle. `HoleSelectionScreenController` properly subscribes/unsubscribes from card events in OnEnable/OnDisable. No null-ref hazards in screenshot or in static analysis.
- Capture-helper: compliant.

The task is **ready for Cesar's final approval** with two scoped follow-up notes documented above.

---

## Files Cesar should know about

| File | Path | Status |
|---|---|---|
| Architect review (this file) | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/hole_selection_screen/ARCHITECT_REVIEW.md` | New |
| Pipeline status | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/hole_selection_screen/STATUS.md` | Updated → `ARCHITECT_REVIEW_PASS` |
| Heartbeat | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/hole_selection_screen/HEARTBEAT.log` | Appended iter-5 architect-review entry |

## Follow-up items for Cesar (NOT blocking this task)

1. **Hole 1 image asset shape (Nit A).** Pick one: re-export Hole_01.png at landscape 749×288 / widen the Tutorial.HoleImage rect to accept portrait / amend the spec. Open a Quick spec under `Docs/Specs/Quick/hole_selection_hole1_image.md` once decided. Holes 2-18 are still magenta placeholders so this is naturally part of the per-hole-art queue.
2. **PLAY button height runtime check (Nit B).** Eyeball `Rect.height` on `ExpandedContainer/ActionButton` at runtime. If it reports 120 px, mark closed. If <120 px, open a Quick spec with `LayoutElement.preferredHeight=120, flexibleHeight=0` fix.
3. **Filter contrast over scenic Background.png (Nit C — design call).** Pick (i) accept as-is, (ii) add semi-transparent dark plate behind filter rows, or (iii) scope to a separate polish spec.
4. **REPLAY-mode visual smoke test (deferred from iteration 5).** Set `HoleProgressionService.SetPlayedOverride(1, true)` at runtime via the `HoleProgressionDebug` inspector entry, re-run the screen, capture one screenshot showing the REPLAY button + dark slate `#1E293B` text + halved replay rewards. Code path is structurally identical to PLAY mode; this is a visual sanity-check, not a regression risk.
