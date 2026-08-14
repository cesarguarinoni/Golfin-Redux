# SPEC — rp_balance_sync (server balance → the RP the player sees)

**Status:** SPEC_READY
**Author:** Architect (Cowork session), 2026-08-13, from Cesar's observation: *"the RP counter on the nav bar does not show the current backend RPs."*
**Parent:** `Docs/Specs/Completed/reward_points_backend` (Slices 1–2 + follow-ups). This is the missing inbound half of that cutover.

---

## 1. The gap (diagnosed against the code, 2026-08-13)

Slice 2 made the game **write** to the server (earns enqueue, spends debit server-first) but never made it **read**:

- `PointsService` holds the server balance (`Balance`, `HasBalance`, `OnBalanceChanged`) — and its own comment still reads *"No subscribers exist in Slice 1."* None were added in Slice 2.
- `RewardPointsManager` holds `SaveDataHost.Data.rewardPoints`. **This is what the nav bar (and every other RP display) reads**, via `GetPoints()` / `OnPointsChanged`.
- Nothing bridges them. `grep RefreshBalanceAsync` finds exactly one non-test caller: `Economy/Editor/PointsBackendMenu.cs` (the editor menu item). At runtime the game never asks the server for a balance.
- `RewardPointsManager.SetPoints` — the only writer — is gated by `AllowLocalOverride`, which **refuses when the flag is ON**. So today there is no legal path for a server balance to reach the UI at all.

**Symptom:** with the flag ON, the nav bar shows a stale local number indefinitely. Admin grants via the dashboard (verified working in prod: 123 → 223 → 173) are invisible in game, and so is anything the shared PLAYLIFE/GPS app does to the same balance.

## 2. Goal

The RP the player sees is the RP the server holds — kept current without the player doing anything, and without the nav bar or any other RP consumer being rewritten (they all already listen to `OnPointsChanged`).

## 3. Design

**3.1 An authorized inbound writer.** Add `RewardPointsManager.ApplyServerBalance(int total)`:
- NOT subject to `AllowLocalOverride` — this is the server speaking, not a local override; the guard exists to stop *local* writes from being silently reverted, which is the opposite case.
- Writes `SaveDataHost.Data.rewardPoints` (now a **display cache of the server value**, not a source of truth) + `MarkDirty()`, and fires `OnPointsChanged` so every existing subscriber updates.
- No-ops when the value is unchanged (avoid pointless events/saves).
- Does NOT touch the leaderboard accumulators (`rpDaily/rpWeekly/rpMonthly/lifetimeRpEarned`) — those track *earned*, not *balance*, and are fed by `EarnPoints`.

**3.2 Subscribe it.** `PointsService.OnBalanceChanged` → `ApplyServerBalance`. Wire it where the two singletons can see each other without creating an asmdef cycle (`Golfin.Economy` must not depend on Assembly-CSharp — use the same seam pattern the tournament adapters use, or an `EconomyRuntime` bridge like `PointsSpendGate`). Subscribe in `OnEnable`, unsubscribe in `OnDisable`, per house convention.

**3.3 Refresh at the moments that matter** (all no-ops when the flag is OFF):
- **After sign-in succeeds** — the first balance of the session. NOTE: no auth event was found in `AuthService.cs` during this diagnosis; find the real hook (or add a minimal one) rather than polling. Flag it if it needs a new event.
- **On app resume / foreground** (`OnApplicationPause(false)`) — catches admin grants made while the app was backgrounded, which is exactly the dashboard workflow.
- **On entering Home** — cheap, and it is the screen where the counter is most looked at.
- **After every successful earn/spend** — the RPCs already return the new balances, and `PointsService` already folds spend responses into its cache (see the comment at `PointsService.cs:295`); make sure both paths raise `OnBalanceChanged` so §3.2 propagates them.

**3.4 The pending-queue rule (important).** Queued earns are not on the server yet, so a naive overwrite makes freshly earned points *visibly disappear* until the queue flushes. Rule: **displayed = server balance + sum of pending earn ops**. Simplest correct implementation: apply the server value, then re-add the pending queue's total; recompute whenever the queue changes. Do not skip this — an earn that flickers away is worse than a stale counter.

**3.5 Offline / never-answered.** If the server has never answered this session (`HasBalance == false`), keep showing the cached value — do NOT show 0. `HasBalance` exists precisely to distinguish "0 RP" from "unknown".

## 4. Out of scope
Backend changes; dashboard changes; the pending-ops queue's own retry logic; leaderboard accumulator semantics; any economy value changes.

## 5. Acceptance
1. EditMode suite stays green (full sweep — filtered runs mask failures).
2. New EditMode tests: `ApplyServerBalance` fires `OnPointsChanged` and writes SaveData; it is NOT blocked by the flag-ON guard; pending-earn addition (§3.4) is correct; `HasBalance == false` leaves the cached value alone.
3. **Manual, the real proof:** flag ON, signed in as `cesar.guarinoni@gmail.com` (live balance currently **173**) — the nav bar reads 173, not the old local number. Then grant +25 in the admin dashboard, background/foreground the app (or return to Home), and the nav bar becomes 198 with no restart.
4. Flag OFF: byte-identical behavior to today (local economy, no network).
