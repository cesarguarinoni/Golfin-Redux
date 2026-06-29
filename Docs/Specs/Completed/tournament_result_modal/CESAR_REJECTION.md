# CESAR_REJECTION — tournament_result_modal (post ARCHITECT_REVIEW_PASS)

**Date:** 2026-06-29
**Context:** Design + wiring approved on sight by Cesar. One small addition requested before DONE — NOT a defect in the existing work.

## Requested addition (single item)

When the Prize modal is on screen, **darken everything behind it** (a backdrop scrim) and **block all interaction behind the modal** — only the CLAIM button may be interacted with. (May be partially true already via raycast order, but there is currently NO visible dimming — the canonical screenshot shows Home fully bright behind the modal.)

## Implementation guidance (mechanism already exists — reuse, don't reinvent)

- `ModalController` (the base class this modal extends) ALREADY has a built-in `public GameObject backdrop;` field that it auto-`SetActive(true)` on `Show()` and `SetActive(false)` on `Hide()` (see `Assets/Scripts/UI/Modals/ModalController.cs` lines 16-17, 66-68, 89-92, 152-154). It is currently **unwired** on this prefab: `backdrop: {fileID: 0}` (line ~2000 of `TournamentResultModal.prefab`). The Signup clone source is unwired too — that's why nothing dims.
- **Convention to match:** `Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab` has a wired backdrop (`backdrop: {fileID: 5949428240872834097}`). Match its dim Image color/alpha and full-screen RectTransform setup.
- Author a full-screen backdrop child on the Result modal:
  - Full-screen `Image` (stretch anchors 0,0–1,1, zero offsets), semi-transparent black (match HoleCompleteModal's alpha).
  - **`Raycast Target` = ON** so it blocks all clicks to the Home/screen content behind the modal.
  - It must sit **behind** the navy panel but **in front of** everything else (sibling order: backdrop first, panel after, within the modal root that `Show()` `SetAsLastSibling()`s).
  - **Claim-only — do NOT add a click-to-dismiss handler on the backdrop.** Tapping the dim area must do nothing (no Button, no close). CLAIM remains the sole exit (acceptance #5).
- Wire the new backdrop GameObject into the `backdrop` SerializeField via SerializedObject/MCP (not a paste-for-Cesar step).

## Verify
- With the modal open: screen behind is visibly dimmed; clicking anywhere outside CLAIM does nothing (no nav, no dismiss); CLAIM still works.
- Re-capture canonical still over Home at 1170×2532 showing the dim.
- No regression: backdrop `SetActive(false)` on Hide so the screen is fully bright again after claim; `OpenModalCount` unchanged.

Everything else stays as-passed (clone provenance, Figma fidelity, panel size, RANK non-bold, CLAIM containment). This is purely additive.
