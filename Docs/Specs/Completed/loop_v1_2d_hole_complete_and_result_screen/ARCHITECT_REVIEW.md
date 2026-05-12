# Architect Review — `loop_v1_2d_hole_complete_and_result_screen`

Reviewer: `golfin-reviewer` (final architectural-review gate)
Written: 2026-05-12 JST
Iteration reviewed: **13** (post-CESAR_REJECTION iter-12; iter-12 was previously architect-PASS but Cesar caught LOCKED text outside BG in live play and rejected)

## Independent visual scan (BEFORE reading prior reviews)

The iter-13 screenshot shows two rounded-rectangle cards stacked on a dark background. Card 1 (FAILED, top) is a medium-navy rounded card containing the red "X FAILED" header, white "Lomond Country Club - Hole 1 - Par 4" subhead, a green wavy hole-shape graphic with TEE OFF/STROKES/BEST/TIME stats beside it, a row of three reward pips (yellow/grey/white) with "x10" labels, and a gold "RETRY" button. Card 2 (LOCKED, bottom) is dramatically shorter (roughly one third Card 1's height) and unmistakably darker — a deep near-black navy — and contains a lock icon plus grey "LOCKED" header, a white "Lomond Country Club - Hole 2 - Par 4" subhead, and a dimmed rewards row. The dim/dark fill on Card 2 follows the rounded corners cleanly with no square edges protruding past the BG curve at any of the four corners, and every element of Card 2 (lock header, subhead, rewards) sits visibly inside the rounded BG with comfortable padding above and below. No dividers are visible inside Card 2.

## Figma side-by-side

Reference: `Docs/Reference/Results Screen/Results - Failed (Replay)-1.png` (canonical LOCKED variant).

| Element | Figma reference | iter-13 capture | Comparison |
|---|---|---|---|
| Card 1 BG color | medium navy, slight bluish accent | RGB ~(18,48,78) per implementer sampling | matches |
| Card 2 BG color | clearly darker than Card 1; ~60–70% of Card 1 brightness; reads as muted navy | RGB ~(6,25,43) per implementer sampling; ratio 0.42 vs Card 1 | iter-13 reads slightly DARKER than Figma (Figma ≈0.65, iter-13 ≈0.42), but in the direction Cesar explicitly requested ("actually darken Card 2 significantly"). Acceptable. |
| Card 2 corner rounding on dim | 50px rounded, dim follows curve | 50px rounded, dim follows curve (9-sliced sprite) | matches |
| Lock icon + "LOCKED" position | top of card, centered, padded inside BG | top of card, centered, ~20px below Card2 top edge (Cesar's 144px HLG padding) | matches Figma intent |
| Subhead position | centered below lock header, inside BG | centered below lock header, inside BG | matches |
| Rewards row | 3 pips inside BG, dimmed icons (gold/dim/grey) | 3 pips inside BG, dimmed icons (gold/dim/grey) | matches |
| Dividers in LOCKED Card 2 | none | none | matches (preserves iter-12 Bug A fix) |
| Card 2 height ratio | ~1/3 of Card 1 | ~1/3 of Card 1 (285/855) | matches |
| Container background | blurred gameplay scene composite | flat dark navy (ortho-RT capture isolated) | capture-context difference only, not a layout regression |

Per-element deltas: only the Card 2 darkening is slightly stronger than the Figma reference (≈0.42 vs ≈0.65). Cesar explicitly asked for significant darkening because the prior 0.65 plain-overlay read "subtle"; the iter-13 sprite-driven 0.65 alpha produces a denser fill than the plain rectangle did. This is a tuning preference, not a fail condition.

## Bbox check verification

Self-reviewer's padding-adjusted math:
- LockedHeader corners BL=(72, 889.5) TR=(1098, 1093.5)
- Card2 top = 969
- padding.top = 144
- contentMaxY = 1093.5 − 144 = 949.5
- 949.5 ≤ 969 → visualInside top = TRUE ✓

Re-derived independently: arithmetic is correct. 1093.5 − 144 = 949.5, and 949.5 ≤ 969 by 19.5px. The visible rendered content of LockedHeader (lock icon + "LOCKED" text) sits approximately 19.5px below Card2's top edge — well inside the rounded BG. The naive `inside=False` is a layout-box artifact of Cesar's 144px HLG top padding (HLG sizes the layout box to accommodate the padding, but the rendered children land inside the BG).

Other elements:
- Subhead naive inside=True → trivially visualInside=True ✓
- RewardsRow naive inside=True → trivially visualInside=True ✓

**Concur with self-reviewer.** All three elements pass the padding-adjusted containment check. Visual scan confirms it independently — every element is plainly inside the BG.

## Scene-mutation audit

Orchestrator independently verified `git diff HEAD -- Assets/Scenes/Physics/LabScaffold.unity` shows only a 2-line objectReference null-out, with no `m_IsActive` / `m_AnchoredPosition` / `m_SizeDelta` changes. No regression vs the iter-12 disaster where the capture path baked SetActive(false) into the scene.

Independent re-check of the prefab change scope:
- `Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab` lines 7483–7512 confirm DarkenOverlay Image:
  - `m_Color: {r: 0, g: 0, b: 0, a: 0.65}` (line 7496) ✓
  - `m_Sprite: {fileID: 21300000, guid: 064cba0b0bc85154995fa70dd470817b}` (line 7503) — verified the GUID resolves to `Assets/Art/ResultScreen/Background - HoleCard.png` via `.meta` lookup ✓
  - `m_Type: 1` = Sliced (line 7504) ✓

The fix is isolated to the DarkenOverlay Image component (fileID 4370394886880004617). No other prefab fields touched. No C# changes, no scene changes, no asset changes. Hard constraints from CESAR_REJECTION.md are all satisfied.

**Concur with orchestrator verification.** Scene is clean.

## Implementer-PARTIAL override assessment

Implementer self-graded the LockedHeader containment as PARTIAL (PASS visual / FAIL bbox) and flagged it for architect judgment. Self-reviewer overrode to PASS using the padding-adjusted `visualInside` formula from `tasks/lessons.md`.

Per the new pipeline rule, this override needs specific reasoning. The self-reviewer provided it:
- Naive `inside=False` is driven by LockedHeader's 204px GO height (BL.y=889.5 to TR.y=1093.5).
- The 204px height equals 144px (Cesar's padding.top) + ~60px (lock icon + LOCKED text height).
- HLG places the rendered children at the BOTTOM of the layout box (after padding), so visible content starts at TR.y − padding.top = 949.5.
- Card2 top = 969. 949.5 ≤ 969 with 19.5px margin → visible content is inside.

This reasoning is mathematically sound and matches what the screenshot shows (LOCKED text plainly inside the BG, ~20px down from the top edge). The self-reviewer did not wave it through with hand-waving; they applied the formula explicitly with cited inputs.

**Concur with self-reviewer's override.** The PARTIAL was a correctly-surfaced ambiguity, and the resolution is sound.

## Verdict

**`ARCHITECT_REVIEW_PASS`** — ready for Cesar's final approval.

All four PASS criteria hold:
1. Card 2 is unmistakably darker than Card 1 (ratio 0.42; visually obvious at a glance).
2. Rounded corner clipping is intact — 9-sliced sprite inherits the 50px corner radius; no square protrusion.
3. Padding-adjusted bbox visualInside=TRUE for LockedHeader, Subhead, and RewardsRow.
4. No scene mutations (orchestrator-verified `git diff`; prefab-only change).

The implementer's PARTIAL self-grade on LockedHeader containment was correctly surfaced and correctly resolved by the self-reviewer via the padding-adjusted formula. Cesar's two manual fixes (144px LockedHeader padding + DarkenOverlay placeholder removal) are both preserved.

### Non-blocking note for Cesar

iter-13's Card 2 darkening (RGB ratio ≈0.42 vs Card 1) is slightly more intense than the Figma reference (≈0.65). This is in the direction you asked for ("actually darken Card 2 significantly") after iter-9's 0.65 plain-overlay read too subtle. If after live-play you want a softer dim closer to the Figma intensity, the alpha can be reduced from 0.65 → ~0.45–0.55 on the DarkenOverlay Image — a single-field tweak in the prefab. Not a blocker; flagging for your taste preference.
