# IMPLEMENTER REPORT — rp_balance_sync

**Implemented by:** Claude Code (direct, 2026-08-14). Not a subagent-pipeline task — no UI surface, no Figma node, no screenshot gate; the deliverable is code + tests.
**Baseline:** HEAD `55a198ce2`. Pre-existing dirty at kickoff (NOT touched by this task): `.gitignore`, `Assets/Scenes/ShellScene.unity`, `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs`, `Assets/Scripts/UI/Account/SignUpScreenController.cs`, `Docs/Architecture/UI_HIERARCHY.md`, `Docs/Scripts/daily_report.py`, `_to_delete/*.stale`.

---

## 1. What was wrong, in one line

`PointsService` held the server balance and `RewardPointsManager` held the number the UI reads, and **nothing connected them** — while the only writer, `SetPoints`, refuses whenever `PointsBackendEnabled` is ON. So no server balance could legally reach the nav bar.

## 2. Files changed

| File | Change |
|---|---|
| [RewardPointsManager.cs](Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs) | **§3.1** — adds `ApplyServerBalance(int)`, the one authorized inbound writer: writes `SaveData.rewardPoints`, `MarkDirty()`, fires `OnPointsChanged`. Explicitly NOT behind `AllowLocalOverride`. No-ops when unchanged; rejects negatives; leaves the leaderboard accumulators alone. |
| [ServerBalanceSync.cs](Assets/Scripts/Economy/ServerBalanceSync.cs) *(new)* | **§3.2** — `IServerBalanceSink` + the static binder that routes `PointsService.OnDisplayBalanceChanged` into it, pushing the current value immediately on a late bind. The asmdef-safe seam (`Golfin.Economy` must not see Assembly-CSharp), mirroring `IRewardPointsService`/`RewardPointsServiceAdapter`. |
| [ServerBalanceSyncBehaviour.cs](Assets/Scripts/EconomyRuntime/ServerBalanceSyncBehaviour.cs) *(new)* | **§3.3** — the Assembly-CSharp half: self-bootstraps at `AfterSceneLoad` **only when the flag is ON**, implements the sink by forwarding to `RewardPointsManager`, and triggers refreshes on sign-in, app foreground, entering Home (10s cooldown), and startup-if-already-signed-in. |
| [PointsService.cs](Assets/Scripts/Economy/PointsService.cs) | **§3.4** — adds `PendingEarnTotal`, `DisplayBalance` (= server + pending) and `OnDisplayBalanceChanged`, raised on both a server answer and a queue mutation, and **silent while `HasBalance == false`** (§3.5). Also the spend-ordering fix in §4 below. |
| [PendingOpsQueue.cs](Assets/Scripts/Economy/PendingOpsQueue.cs) | **§3.4** — adds `PendingEarnTotal` (skips non-positive, catalog-fixed amounts) and an `OnChanged` event raised on enqueue/dequeue/remove/clear/load, so the displayed number recomputes when the pending half moves. |
| [AuthService.cs](Assets/Scripts/Auth/AuthService.cs) | **§3.3 sign-in hook** — adds the `SignedIn` static event (see §3 below). |
| [ServerBalanceSyncTests.cs](Assets/Scripts/Economy/Tests/ServerBalanceSyncTests.cs) *(new)* | **§5.2** — 12 EditMode tests for the wire, the pending-earn rule and the unknown-vs-zero rule. |
| [ApplyServerBalanceTests.cs](Assets/Scripts/EconomyRuntime/Tests/ApplyServerBalanceTests.cs) *(new)* | **§5.2** — 6 EditMode tests for `ApplyServerBalance` itself, including the regression guard that it is NOT blocked by the flag-ON guard. |
| [Golfin.EconomyRuntime.Tests.asmdef](Assets/Scripts/EconomyRuntime/Tests/Golfin.EconomyRuntime.Tests.asmdef) *(new)* | Test assembly for the above; reaches `RewardPointsManager` (Assembly-CSharp) by reflection, exactly as `Golfin.TournamentsRuntime.Tests` reaches the tournament adapters. |

## 3. Sign-in hook — what was chosen, and why (spec asked this be flagged)

**There was no existing sign-in event.** `AuthService` establishes the session inside a private `Wrap(...)` closure (password sign-in, sign-up, display-name update, token refresh) and, separately, inside `OnDeepLink` for the OAuth redirect — neither announced anything.

**Chosen: add `public static event Action<AuthSession> SignedIn`**, raised from both places whenever a call actually establishes an authenticated session (`result.HasSession && Session.IsAuthenticated`). It is:
- **minimal** — one event, one guarded raise per path, no change to any existing call site or return value;
- **static** — the balance-sync bridge and `AuthService` bootstrap independently at `AfterSceneLoad`, and a static event removes the ordering race entirely;
- **exception-isolated** — a throwing subscriber can never fail the sign-in that produced it.

**Not polled**, per the spec. The one gap an event cannot cover is a **returning player who is already signed in from the saved session** — no sign-in ever happens for them — so `ServerBalanceSyncBehaviour.Start()` does a single startup refresh when `Session.IsAuthenticated`. That is one request at launch, not a poll.

## 4. One change outside the literal spec text (flagged, not silent)

`PointsService.SpendRoutine` used to call `ApplySpend(data)` **before** `onDone`. That was harmless while nothing consumed the balance — and became a bug the moment this task connected the wire:

> `onDone` is what runs the **local** debit (`RewardPointsManager.SpendPoints` inside `PointsSpendGate`'s `onApproved`). Folding the already-debited server total into the display first, then letting the local debit subtract the same amount again, leaves the counter **one spend too low** until the next refresh.

Fix: `onDone` runs first, then `ApplySpend(data)` in a `finally` (a throwing call site must not strand a stale cache). Net effect with the wire live: local and server agree and `ApplyServerBalance` no-ops. Covered indirectly by the existing `PointsSpendTests` (all green) and directly by the design comment at the call site.

Related, documented but deliberately NOT changed: during a queue replay, `Queue.Dequeue()` and `ApplyEarn(...)` are in the same synchronous block with no `yield` between them, so the displayed balance dips and recovers **within one frame** and is never painted low. When the server *refuses* an op the dip is kept on purpose — the server rejected that earn, so the optimistic local credit for it should come back off.

## 5. Acceptance

### §5.1 / §5.2 — EditMode, verified here

**1190 passed / 0 failed / 3 pre-existing skips, of 1193 discovered.** Run **per assembly** across all 17 EditMode assemblies, because a filtered run reports `FailedTests` only for its own filter. The per-assembly passes sum exactly to 1193, so the sweep is complete with no assembly silently skipped:

| Assembly | Passed | Failed | Assembly | Passed | Failed |
|---|---|---|---|---|---|
| Golfin.Economy.Tests | 53 | 0 | Golfin.Physics.Tests | 357 | 0 (+3 skip) |
| **Golfin.EconomyRuntime.Tests** *(new)* | **6** | **0** | Golfin.Course.Tests | 26 | 0 |
| Golfin.Auth.Tests | 27 | 0 | Golfin.Core.Stamina.Tests | 37 | 0 |
| Golfin.Net.Tests | 18 | 0 | Golfin.HoleCompleteModal.Tests | 16 | 0 |
| Golfin.TournamentsRuntime.Tests | 21 | 0 | Golfin.SceneSnapshot.Tests | 8 | 0 |
| Golfin.Tournaments.Tests | 209 | 0 | GolfinRedux.Tests.EditMode | 36 | 0 |
| Golfin.Save.Tests | 44 | 0 | Golfin.UI.Tests | 5 | 0 |
| Golfin.UI.Shop.Tests | 8 | 0 | Golfin.UI.Rankings.Tests | 17 | 0 |
| Golfin.Gameplay.Tests | 302 | 0 | | | |

The 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips, unrelated to this task.

New tests, mapped to the spec:

| Spec | Test |
|---|---|
| §3.1 not blocked by the flag-ON guard | `FlagOn_ApplyServerBalance_IsNotBlocked` — **the regression guard**: re-adding `AllowLocalOverride` to `ApplyServerBalance` re-creates the original bug and turns this red |
| §3.1 guard still guards *local* writes | `FlagOn_SetPoints_IsStillBlocked` |
| §3.1 fires `OnPointsChanged` + writes SaveData | `FlagOn_ApplyServerBalance_IsNotBlocked` (asserts both), `UnchangedValue_IsANoOp` |
| §3.1 accumulators untouched | `LeaderboardAccumulators_AreUntouched` |
| §3.2 the wire exists at all | `ServerBalance_ReachesTheSink`, `Bind_PushesTheAlreadyKnownBalanceImmediately`, `Unbind_StopsUpdates`, `Bind_Twice_DoesNotDoubleSubscribe` |
| §3.4 pending-earn addition | `PendingEarn_IsAddedToTheServerBalance`, `QueueDrain_LandsOnTheServerTotal_WithoutDoubleCounting`, `RefusedEarn_DropsThePendingCredit`, `CatalogFixedEarn_ContributesNothingToThePendingTotal` |
| §3.5 unknown ≠ 0 | `NoServerAnswer_NeverPushesAnything`, `FailedRefresh_LeavesTheCachedValueAlone`, `FirstAnswerOfZero_IsPushed` |
| §5.4 flag OFF unchanged | `FlagOff_NothingReachesTheSink` (+ the bridge is never even constructed with the flag off) |

### §5.3 / §5.4 — NEEDS CESAR'S MANUAL PASS

Neither can be proven from the Editor: §5.3 needs a live signed-in session against the prod ledger plus a dashboard grant, and §5.4's real bar is "the local loop feels identical", which is a play-through judgement.

1. **§5.3 — the real proof.** Flag ON, signed in as `cesar.guarinoni@gmail.com` (live balance **173**): the nav bar must read **173**, not the old local number. Then grant **+25** in the admin dashboard, background and foreground the app (or return to Home), and the nav bar must become **198 with no restart**.
2. **§5.4 — flag OFF.** Local economy behaves exactly as before; nothing hits the network. The bridge GameObject `[ServerBalanceSync]` must not exist in the hierarchy at all.
3. **Worth a glance while you are in there:** earn RP with the connection off — the counter should go up immediately and stay up (queued earns are added to the displayed total), then not jump when the queue flushes on reconnect.

## 6. Standing-rule self-checks

- No edits under `Assets/Scripts/Physics/` — confirmed.
- No `*Gate` scenarios added to `Scenarios.cs` — the file was already dirty at baseline and was not touched.
- No new `Button` added anywhere, so the `ButtonPressFeedback` rule is not engaged.
- No scene or prefab mutated; `ShellScene.unity` was dirty at baseline and is untouched by this task. Editor left with no scene dirty, not in play mode.
- Every new `.cs` has its `.cs.meta` (Lesson R), including the new `Tests.meta` folder meta.
