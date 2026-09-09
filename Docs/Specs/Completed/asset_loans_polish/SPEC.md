# SPEC — `asset_loans_polish`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY` (2026-09-09).

## Goal

`asset_loans` shipped working but hand-rolled four things the polish track already has an atom for. This task swaps those four for the shared atoms so the loan surfaces move the way the rest of the game moves: the pending state on LEND / RETURN goes through `PendingSpend`; picking a recipient bumps like every other selectable; the ribbon, dim and card badge arrive with motion instead of snapping; the recipient list arrives with the stagger-rise every other fetched list gets. No new behaviour, no new strings, no scene geometry change. Cesar's ask (2026-09-09): "Is it using all the nice transitions/button presses from our polish phase?" — the answer was "partly", this closes the gap.

## What is already right (do not touch)

- Both modals inherit `ModalController` → `UiMotion.Pop` / backdrop `Fade` in, `Unpop` out. Keep.
- `ButtonPressFeedback` is on every loan button and row (`LoanUiBuilder` lines ~190 / 267 / 735 / 948). Keep.
- Toasts via `ToastController`. Keep.
- `LoanUiBuilder` provenance assertions and the four baked sprites. Keep.

## Architecture context

- **Asmdef:** main assembly only (`Assets/Scripts/UI/Loans/*`, `Assets/Scripts/UI/Polish/*` consumers). No asmdef change, no `Golfin.Social` change.
- **Polish atoms (read these before writing a line — signatures verified 2026-09-09):**
  - `Assets/Scripts/UI/Polish/PendingSpend.cs` — `PendingSpend.Begin(Button? button, TMP_Text? label = null, params Button[] alsoDisable)` / `BeginOn(Button?, params Button[])`, `IDisposable`; label becomes `PendingSpend.PendingLabel` ("…") while in flight. Used by `GiftSendModalController`, `CheckInConfirmModalController`, the shop.
  - `Assets/Scripts/UI/Polish/UiSelection.cs` — `UiSelection.Bump(MonoBehaviour host, Transform? target)` (scale 1 → 1.06 → 1, §D6); `UiSelection.Indicator(MonoBehaviour? host, Component? indicator, bool on, bool animate)` (alpha-driven indicator that keeps 0 px rest parity, §D3).
  - `Assets/Scripts/UI/Polish/UiMotion.cs` — `Fade(CanvasGroup, from, to, dur = FadeDur)`, `Rise(RectTransform, CanvasGroup?, dy = RiseDy, dur = EntryDur, Ease)`, `Bump(RectTransform)`, `Run(host, ref Coroutine?, IEnumerator)`, `Stop(host, ref Coroutine?)`, `Then(inner, after)`. Every routine settles on its final value when stopped (the `Register` contract) — rely on it.
  - `Assets/Scripts/UI/Polish/PaintMotion.cs` — `Golfin.Gps.UI.GpsPaintMotion.StaggerRise(MonoBehaviour host, IList<Transform> rows)` and `FadeInPanel(MonoBehaviour host, GameObject? panel, bool animate)`; `SuppressedByPush` guard. Used by `StoreHistoryScreenController` (~358) and `GachaHistoryScreenController` (~335).
- **Loan code touched:** `Assets/Scripts/UI/Loans/LoanModalController.cs` (`SetPending` ~292, `LoadRecipients` ~216, `OnRowClicked`), `LoanReturnModalController.cs` (`SetPending` ~75), `LoanRecipientRow.cs` (`SetSelected` ~89), `LoanRibbonView.cs` (`Show` / `Clear` ~44–70), `LoanBadgeView.cs` (`Apply` ~38), `Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs` (only to add the two `CanvasGroup`s in §3 if they are not prefab-authored yet).
- **Decision of record from `GameShimmerSites.cs` (game_polish_b, Cesar 2026-09-09):** *"a shimmer is for a wait the player can SEE. Measure the wait before placing one."* The follow list is one request that lands in ~200 ms, so this task places **no `ShimmerHost` site** for the recipient list. The Architect's earlier chat suggestion of a shimmer there was wrong; the `—` placeholder rows stay and the real rows stagger-rise in.

## Implementation

### 1. Pending state → `PendingSpend` (both modals)

`LoanModalController`: delete the spinner branch of `SetPending` and the `confirmSpinner` field's runtime use (leave the serialized field and the prefab object in place, inactive — removing a wired object is a prefab edit this task does not need). In `ConfirmRoutine`:

```csharp
using (PendingSpend.Begin(confirmButton, confirmLabel, cancelButton))
{
    … the existing Lend call and its result handling, unchanged …
}
```

`confirmLabel` is the TMP child of the LEND button — add a `[SerializeField] private TMP_Text? confirmLabel;` wired by `LoanUiBuilder` (or use `BeginOn(confirmButton, cancelButton)`; either is fine, pick one and say which). `UpdateConfirmEnabled` must not re-enable the button while a scope is open: keep `_pending` as the flag it already is and set it true/false around the scope, or read the scope into a field. Behaviour: LEND label reads "…" and both buttons are dead until the server answers; on dispose the label and both `interactable`s restore; a refusal toast shows after restore (as today).

`LoanReturnModalController`: same shape — `PendingSpend.Begin(returnButton, returnLabel, cancelButton)` around the `Return` call; drop the spinner toggle.

### 2. Recipient selection → `UiSelection.Bump`

`LoanModalController.OnRowClicked(row)`: after the existing `SetSelected` fan-out, `UiSelection.Bump(this, row.transform)` on the newly selected row only. `LoanRecipientRow.SetSelected` keeps the sprite swap (that is the fidelity decision, not a polish one). No bump on the initial selection when the modal opens (there is none — nothing is preselected).

### 3. Ribbon + dim → fade/rise in; badge → `UiSelection.Indicator`

`LoanRibbonView`:
- `ribbonRoot` gets a `CanvasGroup` (prefab-authored via `LoanUiBuilder`, alpha 1 at rest); `lentDim` gets a `CanvasGroup` (alpha 1 at rest; the 0.55 stays on the Image colour, the group only animates 0 → 1).
- `Show(loan, asLender)` when the ribbon was **hidden** before this call: activate, then `UiMotion.Run(this, ref _ribbonMotion, UiMotion.Rise(ribbonRect, ribbonGroup, dy: -RiseDy))` — it drops in from above the panel edge (negative dy: it starts 24 px up and settles at rest; NOTE: check the sign convention in `RiseRoutine` — `Rise` is written for "rise from below"; if `dy` is applied as `rest − dy` pass `+RiseDy`… read the routine, do not guess, and quote the line in the report) and, lender only, `UiMotion.Fade(dimGroup, 0f, 1f)`.
- `Show` when already visible (a re-paint on the panel's tick — the time-left label): update the label only, **no motion**. A tick must never re-trigger the entrance.
- `Clear()` when visible: `UiMotion.Then(UiMotion.Fade(ribbonGroup, 1f, 0f), () => ribbonRoot.SetActive(false))` and the dim the same; when already hidden, no-op.
- `UiMotion.Stop` both handles in `OnDisable` (the `Register` contract settles them at rest), so leaving the screen mid-motion leaves a correct rest state. The `LayoutElement.ignoreLayout` fix on the club panel is untouched.

`LoanBadgeView.Apply(lentOut, borrowed)`: replace the `SetActive` with `UiSelection.Indicator(this, badgeRoot.transform, show, animate: changed)` where `changed` = the visible state differs from the last `Apply` (cache it). The glyph sprite swap stays. Rest-state parity: `Indicator` leaves the object active at alpha 0 — the carousel's icon layout must not shift; the report proves 0 px by comparing a card's rect dump before/after with the badge off.

### 4. Recipient list arrival → `StaggerRise`

`LoanModalController.LoadRecipients`, after the real rows are spawned and bound: collect their `Transform`s and call `Golfin.Gps.UI.GpsPaintMotion.StaggerRise(this, rows)` — exactly what `StoreHistoryScreenController` does at ~358. Guard with `GpsPaintMotion.SuppressedByPush` the same way. The `—` placeholder rows are NOT animated (they are the pre-arrival state), and the empty state goes through `GpsPaintMotion.FadeInPanel(this, emptyStateRoot, animate: true)` instead of `SetActive(true)`.

### 5. No new strings, no new sprites, no scene geometry

Every rect in `asset_loans`' rect self-diff (0.000 px) must still hold; the report re-runs that dump. `LocalizationText.csv` untouched (`export --check` still clean).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] LEND / RETURN pending: label reads `PendingSpend.PendingLabel` while in flight, CANCEL dead, both restore on the answer — captured mid-flight with the transport stubbed to delay 1.5 s (screenshot + `interactable` dump).
- [ ] Tapping a recipient row bumps it (scale trace over ≥ 3 frames peaking > 1.0), the previous selection does not.
- [ ] Ribbon entrance: first `Show` after a reconcile plays Rise (+ dim Fade for the lender); a tick-driven `Show` on a visible ribbon plays nothing (position/alpha trace flat). `Clear` fades out then deactivates.
- [ ] Badge: `Indicator` on/off with `animate` only on a state change; card rect dump identical with the badge off vs. before this task (0 px).
- [ ] Recipient rows stagger-rise on arrival (frame strip); placeholders do not; empty state fades in.
- [ ] `asset_loans` rect self-diff re-run: all Δ still 0.000; `m_IsActive` census unchanged except any `CanvasGroup` additions.
- [ ] `OnDisable` mid-motion leaves ribbon/dim/badge at a correct rest state (leave the screen at t = 0.1 s, come back, dump).
- [ ] EditMode suite: no new failures; `ScrollFeelTests` and `UiMotion*` still green.
- [ ] No new hardcoded literals; `export --check` clean (nothing to import).
- [ ] Console clean; `[SerializeField]` wired (`confirmLabel`/`returnLabel` if added, the two `CanvasGroup`s); deviations flagged.

## Files / hierarchy this task touches

- `Assets/Scripts/UI/Loans/LoanModalController.cs`, `LoanReturnModalController.cs`, `LoanRecipientRow.cs` (only if a `Transform` accessor is needed), `LoanRibbonView.cs`, `LoanBadgeView.cs`
- `Assets/Scripts/UI/Loans/Editor/LoanUiBuilder.cs` — add `CanvasGroup` to `LoanRibbon` and `LentDim` on both panels, wire `confirmLabel` / `returnLabel`
- `Assets/Scenes/ShellScene.unity`, `Assets/Prefabs/UI/Modals/LoanModal.prefab`, `LoanReturnModal.prefab` — only what the builder writes
- `Docs/AI_CONTEXT.md` — at close-out

## Smoke evidence

Editor play mode with the stubbed transport from `LoanUiCaptureBot`: a frame strip (every 2 frames, 0.4 s) of the ribbon entrance on the Roster lender state; the modal pending state mid-flight; the recipient stagger. Rect dumps as above. Human play-and-confirm note on the feel (Lesson O); Cesar signs off on device.

## Out of scope (do NOT do these)

- A `ShimmerHost` site for the recipient list (decision above).
- Sprite-swapped disabled buttons (D-1 in `asset_loans` stands).
- Any change to `LoanService`, reconciliation, the server, strings, or Figma.
- Haptics (Notion 2130, parked).
