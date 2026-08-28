# SPEC — `transaction_feedback`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

Filed 2026-08-28 by the Architect (Cowork) after Cesar reported: *"It's weird to change screen and see a
club you bought suddenly appear after a bit."* Two repos: `playlife` (one config line) and `GolfinRedux`.

## Status

See `STATUS.md`. Standard pipeline states (`SPEC_READY` → … → `DONE`). This is a code + config task
with NO Figma node: Rule 18 / Rule 21 do not apply; evidence is screenshots of the pending state plus the
timing log lines (§7).

## 0. Diagnosis (read this first — it bounds the work)

Read on 2026-08-28 against `main`:

1. **Purchase is already pessimistic and applies the grant locally on response.**
   `GeneralShopScreenController.HandleBuy` → `ShopTransaction.PurchaseServerSide` → on `Ok`:
   `rpm.SpendPoints(outcome.Charged)`, `ApplyPurchaseGrant(outcome.Grant)` (→ `ClubManager.GrantClub`
   etc., fires `OnInventoryChanged`), then `card.Bind`. Nothing is re-fetched. **The defect is that the
   round-trip is invisible**: `_purchaseInFlight` latches the second tap silently and the BUY button
   looks untouched until the answer lands. If the player navigates away in that window, the club
   "appears later" wherever they are.
2. **Equip never touches the server.** `ClubManager.EquipClub` mutates, `PersistOwnedClubs`
   (`SaveDataHost.MarkDirty`, debounced local write), fires `OnClubEquipped` → `BagDetailPanel.
   BuildClubGrid` synchronously. `InventorySyncBehaviour` only hooks `SaveDataHost.OnSaved` for a
   write-behind PUT (`InventoryWriteBehind`, 30 s min interval). **Equip is OUT of this task** — no
   reproduction exists; if Cesar supplies one it becomes its own Quick task.
3. **The "few seconds" is the backend cold start.** `playlife/backend/fly.toml`:
   `auto_stop_machines = "stop"`, `min_machines_running = 0` → scale-to-zero. The first request after
   idle boots a FastAPI machine from stopped. For a solo tester that is nearly every session's first
   purchase. `Docs/GPS/GPS_INTEGRATION_REFERENCE.md:39` documents the same.
4. **Not optimistic-with-rollback.** Decision of record (Architect, Cesar accepted 2026-08-28): the
   server can legitimately refuse (`insufficient`, `price_changed`, `already_owned`, `fee_changed`,
   `cost_changed`); showing then yanking a grant is a worse moment than a visible 300 ms wait, and it
   would require every local grant to be reversible. Pessimistic + pending state is the shape.

## 1. Goal

Make every server round-trip that gates a spend **visible at the tapped control from tap until
callback**, and remove the cold start that turns a ~200 ms round-trip into seconds. After this task a
purchase reads as: tap → button goes pending → (≤ ~0.5 s warm) → "Purchased!" + card OWNED, and the
player never sees an item materialise unannounced on another screen.

## 2. Part A — backend: stop scaling to zero (playlife repo)

`playlife/backend/fly.toml`, `[http_service]`:

```toml
  auto_stop_machines = "suspend"      # was "stop"
  auto_start_machines = true
  min_machines_running = 0            # unchanged — near-free stays near-free (Cesar 2026-08-28)
```

- Fly resumes a suspended Machine far faster than it boots a stopped one; idle cost stays ~rootfs-only.
- `fly deploy` from `playlife/backend`, then §7 proof. If `suspend` is refused for this Machine
  shape (Fly restricts suspend on some sizes), fall back to `min_machines_running = 1` and SAY SO in
  the report — do not silently leave `"stop"`.
- Decision of record: `min_machines_running = 1` (~$2/mo) is the pre-beta setting; not now.

## 3. Part B — Unity: one shared pending affordance

New helper, `Assets/Scripts/UI/Polish/PendingSpend.cs` (namespace `Golfin.UI.Polish`, same asmdef as
`ButtonPressFeedback`):

```csharp
/// Marks a control as "waiting on the server" from tap to callback. Disposable so a callback that
/// throws still restores the button. No allocation beyond the scope object.
public sealed class PendingSpend : IDisposable
{
    public static PendingSpend Begin(Button button, TMP_Text label = null, params Button[] alsoDisable);
    // - button.interactable = false (the Button's own Disabled tint is the visual — no new art)
    // - label (if given): caches text, sets "…"  (U+2026; no localization needed)
    // - alsoDisable: e.g. a modal's Cancel/Close while the spend is in flight
    // Dispose(): restores interactable + label. Idempotent.
}
```

No spinner sprite exists in the project and none is to be fabricated (Rule 21 / no flat-fill boxes).
If Cesar wants a rotating icon later, that is an art request to Nishikawa and a follow-up — the helper
grows a `Graphic spinner` parameter then; the call sites do not change.

### 3.1 Call sites (all five — enumerate in the report with a screenshot of each pending state)

| Surface | File | Wrap | alsoDisable |
|---|---|---|---|
| General shop card BUY | `UI/Shop/GeneralShopScreenController.HandleBuy` (+ `GeneralShopCard` exposes its BUY `Button` + label) | `_purchaseInFlight` latch → `PendingSpend` on the tapped card's button; dispose in `onResult` | — (nav stays free; the grant applies globally when it lands) |
| Stamina shop row BUY | `UI/Shop/StaminaShopDetailScreenController.HandleBuyClicked` (+ `StaminaMenuRow` buy button) | same | — |
| Character level-up CONFIRM | `UI/Roster/UI/LevelUpModalController` (server branch, `ProgressService.LevelUpAsync` ~:496) | wrap from call to result | the modal's close/cancel button(s) |
| Club level-up CONFIRM | `UI/Inventory/ClubLevelUpModalController` (server branch, same shape) | same | same |
| Mode card PLAY (entry fee) | `UI/ModeSelect/ModeCardController` ~:608 `PointsSpendGate.Spend(...)` | wrap the card's play/tap buttons until `onApproved`/`onDenied` | — |

`TournamentSignupModalController` already routes through `PointsSpendGate` with its own denied
handling (`_deniedClosesSignup`); add the same wrap on its confirm button — sixth row if the spend
path is a round-trip in the flag-ON build (verify; if it is synchronous, say so and skip).

Rules:
- The existing `_purchaseInFlight` latches STAY (they guard the double-debit; the affordance is
  additive). Every early-return path of the callback must dispose — use `using var pending = …` or
  try/finally, never a bare field.
- Flag OFF (`GOLFIN_POINTS_BACKEND` undefined): the wrap still runs; it just disposes on the same
  frame. Zero behaviour change in the harness sequence (existing `PointsSpendTests` stay green).
- No new toasts, no new copy. Success/refusal copy is unchanged.
- No scene edits. If a call site's button/label is not reachable from code, expose it via a
  `[SerializeField]` on the existing prefab and wire it (Rule 7 — no white-box placeholders).

## 4. Part C — per-call timing in the transport log

`Assets/Scripts/Net/ApiClient.SendRoutine<T>`: when `LogRequests`, log ONE line per completed request
(after retries/refresh resolve):

```
[ApiClient] POST /api/v1/shop/purchase → 200 in 187 ms
```

Use `Time.realtimeSinceStartup` (or `Stopwatch`) around the transport `Send`; log the path only, never
the body or headers. Add `LogWarning` when elapsed > 1500 ms: `[ApiClient] SLOW … (cold start?)`.
This is the evidence line for §7 and the permanent way to tell "backend slow" from "UI slow".

## 5. Architecture context

- Asmdefs: `Golfin.Net` (ApiClient), UI assemblies owning the five controllers, `Golfin.UI.Polish`.
- Reuse: `PointsSpendGate` (process-wide in-flight guard + copy), `ButtonPressFeedback` (sibling
  pattern for a per-button polish component), `ToastController`.
- Do NOT touch: `ShopTransaction` verdict handling, `ShopPurchaseService`, `ProgressService`,
  `InventorySync*`, `Assets/Scripts/Physics/`.

## 6. Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] `fly.toml` carries `auto_stop_machines = "suspend"`; deploy green; `/health` 200. Quote the
      `fly status` line showing the Machine state after ≥ 5 min idle (`suspended`, not `stopped`).
- [ ] Cold-vs-warm timing quoted from the new `[ApiClient]` lines: first purchase after idle, then a
      second purchase immediately after. Both numbers in the report. (Pre-change baseline with `"stop"`
      also quoted if cheap to get — one extra idle wait.)
- [ ] Each of the five (six) call sites: screenshot of the pending state (button disabled tint + "…")
      captured via `screenshot-game-view` mid-round-trip (Editor: temporarily set the flag ON and
      point at the live API, or throttle with a 2 s fake transport in a test harness — say which).
- [ ] Refusal paths restore the button: `insufficient`, `price_changed`, offline. One screenshot each
      for the shop card is enough; the others by test.
- [ ] EditMode: `PendingSpendTests` — Begin disables + relabels, Dispose restores, double-Dispose is
      a no-op, exception inside the scope still restores. Existing `PointsSpendTests` unchanged/green.
- [ ] Flag-OFF harness sequence byte-identical (existing smoke).
- [ ] Rule 11: no new `Button` components are added (this task disables existing ones); confirm.
- [ ] Rule 13: `git status --porcelain` paths outside the spec folder all listed in the report.

## 7. Smoke evidence

Three `[ApiClient]` log lines pasted verbatim (cold, warm, one refusal) + the five pending-state PNGs
in `screenshots/`. Canonical screenshot: the general-shop card mid-purchase.

## 8. Out of scope (do NOT do these)

- Optimistic apply-then-rollback of any grant (decision of record, §0.4).
- Equip / bag / SP-allocation paths — they never wait on the server (§0.2).
- Spinner art, new copy, localization rows, Figma.
- `min_machines_running = 1` (pre-beta; separate one-line change on Cesar's word).
- UnityWebRequest keep-alive investigation (Android 2020.3.9+ regression) — log lines from §4 will
  say whether it matters; separate task if the warm number is > 400 ms on device.
- Any change to `ShopTransaction` / `ShopPurchaseService` / `ProgressService` verdict flows.
