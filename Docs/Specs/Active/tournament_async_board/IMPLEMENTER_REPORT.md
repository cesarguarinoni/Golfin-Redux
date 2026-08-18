# IMPLEMENTER REPORT — `tournament_async_board` (iter-1)

**Iteration shape:** `tournaments:remote-backend-swap`
**Task type:** backend/runtime swap behind an existing seam. **No Figma node, no new prefab, no
scene edit, no mesh** — so Rules 14/16/17/18/19/21 (canonical screenshot, mesh metrics, mesh video,
Figma fidelity table, clone provenance, UI fidelity lint) do not apply. The objective gate for this
task is the EditMode suite in §5 below, run in full.

---

## 1. What was built

Tournaments now run on the server for entry, per-hole submission and the leaderboard, behind the
`ITournamentBackend` seam that was designed for exactly this ("Later: RemoteTournamentBackend
(REST). UI code never changes.").

`RemoteTournamentBackend` **wraps** a `LocalTournamentBackend` rather than forking it: definitions,
state derivation, the entry store and the prize-band ladder are delegated straight back. Only the
four networked concerns are overridden. A fork would have meant two rank ladders and two state
machines drifting apart.

**Asmdef rule honoured.** `Golfin.Net` was NOT added to `Golfin.Tournaments.asmdef` — every new
production file lives in `Assets/Scripts/TournamentsRuntime/` (Assembly-CSharp). Verified below.

---

## 2. Files modified or created

Every uncommitted path outside this spec folder is listed (Rule 13). `git status --porcelain
--untracked-files=all` at close-out is quoted in §7.

### New — `Assets/Scripts/TournamentsRuntime/`

| File | What it is |
|---|---|
| `TournamentNetDtos.cs` | Wire DTOs for all four endpoints + `TournamentNetJson`, the one reader. `DateParseHandling.None` on BOTH the `JsonTextReader` and the serializer; tolerates enveloped and already-unwrapped bodies. |
| `TournamentSubmitQueue.cs` | `PendingHoleSubmit` + persistent FIFO queue of unsent holes. Reuses `Golfin.Economy.IPendingOpsStore` / `FilePendingOpsStore` for the atomic `.tmp`+replace write; own file (`tournament_pending_holes.json`) and own op shape. |
| `RemoteTournamentBackend.cs` | The backend itself, plus `TournamentRegisterOutcome`, `TournamentPlayerRow` (both ranks + the sticky label format) and `TournamentBoardDiskCache` (raw-body mirror, one file per slug). |
| `TournamentBackendPolicy.cs` | Pure `Choose(botOverride, signedIn, isDemo)` → `Local | Remote`, mirroring `LeaderboardProviderPolicy`. |
| `Tests/TournamentAsyncBoardTests.cs` | 54 EditMode tests (§5). |

### Modified

| File | Change |
|---|---|
| `Assets/Scripts/Net/Endpoints.cs` | +4: `TournamentEnter`, `TournamentSubmitHole`, `TournamentEntry`, `TournamentLeaderboard`, all off `/tournaments/golfin/{slug}/…` (+ one private `TournamentGolfin` helper so the prefix exists once). |
| `Assets/Scripts/TournamentsRuntime/TournamentService.cs` | Backend selection: `SetBackend` → `EnsureBackendForSession`; re-evaluates on `AuthService.SignedIn`; `CatchUpWithServer` on sign-in and app resume (flush the hole queue + reconcile every ENTERED tournament). New `BackendKind` / `Remote` accessors. `Compose()` / `ComposeFrom()` signatures untouched — the reflection wire-up guard still resolves them. |
| `Assets/Scripts/Tournaments/ITournamentBackend.cs` | New `ITournamentStateDeriver` interface (`DeriveState`). Additive; `ITournamentBackend` itself is unchanged. |
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` | Implements `ITournamentStateDeriver` (the method already existed); `ResolvePrize` widened `private`→`public static` so the remote path reuses the tie split-pool ladder instead of copying it. No behaviour change. |
| `Assets/Scripts/EconomyRuntime/ServerBalanceSyncBehaviour.cs` | New `public static RequestRefresh(why)` — the pull that reconciles the counter after a SERVER-side debit. No-ops with the flag off / signed out / behaviour not running. |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | On the remote path CONFIRM calls `RegisterAsync` and **skips `RewardPoints.TrySpendAsync` entirely**; insufficient/offline reuse `PointsSpendGate.InsufficientMessage` / `.OfflineMessage`. Local path byte-identical to before. No prefab or visual change. |
| `Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs` | Fill source: cached board renders on `OnEnable`, then `RefreshLeaderboard` repaints if a NEW board landed. Sticky row now comes from the server's `player` object and renders `#{rank} · PRIZE #{prize_rank}` while `bots_active` and the ranks differ. Local path unchanged (`"--"` while provisional). No prefab edits. |
| `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs` | `backend as LocalTournamentBackend` → `as ITournamentStateDeriver`, so entered tournaments keep the `Playing` badge on the remote path instead of silently dropping to the lesser fallback. |
| `Assets/Scripts/TournamentsRuntime/Tests/Golfin.TournamentsRuntime.Tests.asmdef` | +`Golfin.Core.Stamina`, +`Golfin.Economy`, +`Golfin.Net` (test assembly only — the ban is on `Golfin.Tournaments.asmdef`). |
| `Docs/AI_CONTEXT.md` | Session status. |

### Pre-existing, NOT introduced by this task

`Docs/AI_CONTEXT.md`, `Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt`,
`Docs/Specs/Active/fastlane_testflight_pipeline/{IMPLEMENTER_REPORT,STATUS}.md`,
`Docs/Specs/Active/tournaments_server_side/STATUS.md` were already modified at kickoff — quoted
verbatim in the `=== iter-1 kickoff baseline ===` block of `HEARTBEAT.log`:

```
 M Docs/AI_CONTEXT.md
 M Docs/Specs/Active/fastlane_testflight_pipeline/IMPLEMENTER_REPORT.md
 M Docs/Specs/Active/fastlane_testflight_pipeline/STATUS.md
 M Docs/Specs/Active/tournaments_server_side/STATUS.md
 M Docs/TellCode.md
 M Docs/Versioning/last_uploaded_build.txt
```

(`Docs/AI_CONTEXT.md` was already dirty at kickoff AND is edited by this task — both are true.)

---

## 3. Acceptance checklist (SPEC §1–§4)

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Endpoints.cs +4, `{slug}` never a uuid | **PASS** | `Endpoints.TournamentEnter/SubmitHole/Entry/Leaderboard` all build `BaseUrl + "/tournaments/golfin/" + slug + "/…"`. `slug` is `TournamentDefinition.Id`, which `TournamentScheduleMapper` fills from the payload's `slug`. Asserted live in `The_drain_posts_to_the_submit_hole_endpoint_for_the_right_slug`. |
| 2 | `Golfin.Net` NOT added to `Golfin.Tournaments.asmdef` | **PASS** | `git diff Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef` is empty; all four new production files are under `Assets/Scripts/TournamentsRuntime/` (no asmdef → Assembly-CSharp). Verified in §7. |
| 3 | Register → POST enter; server debits; **client must NOT debit** | **PASS** | `RemoteTournamentBackend.EnterRoutine` POSTs and never touches `IRewardPointsService`. The sync `Register` forces the fee to 0 before mirroring. The signup modal's remote branch does **not** call `TrySpendAsync`. Gated by `Register_does_not_touch_IRewardPointsService_even_when_handed_a_fee` (balance unchanged at 10 000 with a 250 fee) plus the control test `The_local_backend_by_contrast_DOES_debit…` — if the two paths ever converge, the control fails. |
| 4 | On success, mirror the entry locally + trigger the `rp_balance_sync` refresh | **PASS** | `EnterRoutine` calls `_local.Register(id, 0L, characterId)` then `InvokeBalanceRefresh()` → `ServerBalanceSyncBehaviour.RequestRefresh("tournament-entry")`. `Register_still_mirrors_the_entry_so_the_gameplay_flow_can_read_it_synchronously` asserts the store write. |
| 5 | `status:"insufficient"` → existing insufficient-funds UX | **PASS** | 200-with-`status:"insufficient"` is parsed as a refusal (`TournamentEnterResponseDto.IsInsufficient`), surfaced as `TournamentRegisterStatus.Insufficient`, and the modal shows `PointsSpendGate.InsufficientMessage` — the same copy the client-side gate uses. Gated by `Insufficient_funds_enter_is_a_200_the_client_must_not_read_as_success`. |
| 6 | Offline → existing "Connection required" toast; entry is online-only | **PASS** | Network/Timeout/NotConfigured/Disabled → `TournamentRegisterStatus.Offline` → `PointsSpendGate.OfflineMessage` ("Connection required"). No entry is mirrored and no queue exists for entries — a queued entry would be an unpaid one. |
| 7 | SubmitHoleResult: local persist FIRST, then enqueue | **PASS** | `_local.SubmitHoleResult(...)` runs before `_queue.Enqueue(...)`. `SubmitHoleResult_persists_locally_first_and_then_queues_the_hole` asserts the store has the hole AND the queue has exactly one op. |
| 8 | Queue mirrors `PendingOpsStore`: FIFO, idempotency GUID per hole | **PASS** | `TournamentSubmitQueue` uses `FilePendingOpsStore`'s atomic `.tmp`+replace; key minted once in `PendingHoleSubmit.New` and immutable thereafter. `Ops_replay_in_strict_FIFO`, `An_op_survives_a_restart_with_its_idempotency_key_intact`, `A_replayed_op_carries_the_SAME_idempotency_key_it_was_minted_with`. |
| 9 | Drop op on `replayed:true` and on 400 | **PASS** | `FlushSubmitQueueRoutine`: 2xx → dequeue (logging when `replayed`); `StatusCode == 400` → warn + dequeue; anything else → persist the attempt count, stop the drain. Gated end-to-end over a scripted `IHttpTransport`: `Replayed_true_is_a_success_and_drops_the_op`, `A_400_is_a_verdict_and_drops_the_op_rather_than_retrying_forever`, `A_transient_failure_keeps_the_op_and_stops_the_drain`. |
| 10 | Replay on reconnect / sign-in / resume | **PASS** | `TournamentService.CatchUpWithServer` is called from `OnSignedIn` and from `OnApplicationPause(false)`; `SubmitHoleResult` also flushes opportunistically. `The_drain_is_strict_FIFO_and_resumes_where_it_stopped` proves a partial drain resumes at the right op. |
| 11 | `GetMyEntry` → local store; reconcile from GET entry, server wins | **PASS** | `GetMyEntry` delegates to the local store (the gameplay flow reads it synchronously mid-round). `ApplyServerEntry` merges by hole number: server strokes win on a shared hole (`The_server_wins_on_a_hole_both_sides_hold`), and a local-only hole still in the queue is preserved rather than erased (`A_local_only_hole_still_waiting_in_the_queue_is_not_erased`). The frozen `CharacterSnapshot` — which the server has no copy of — survives (`The_frozen_character_snapshot_survives_a_reconcile`). |
| 12 | `GetLeaderboard` maps the payload VERBATIM; no client re-ranking | **PASS** | `MapRow` copies Rank/IsTie/DisplayName/CharacterId/Level/Strokes/Thru/IsPlayer/IsDNF; `IsProvisional` = payload `provisional`; `TimeSeconds = 0f`. `Standard_competition_ranking_is_rendered_verbatim_1_2_2_4` (a re-ranking client would produce 1,2,3,4) and `Row_order_is_the_payload_order_not_a_client_sort`. |
| 13 | `RefreshLeaderboard(slug, onDone)` + per-slug disk cache, driven from `OnEnable` | **PASS** | `RefreshLeaderboardRoutine` with a per-slug in-flight guard; `TournamentBoardDiskCache` writes the RAW body atomically AFTER a successful parse (a body this build cannot render never replaces a cache it can), one file per sanitised slug. The screen calls `PopulateLive()` then `RefreshRemoteBoard()` in `OnEnable`. |
| 14 | Sticky row shows `#{rank} · PRIZE #{prize_rank}` while `bots_active` and they differ | **PASS** | `TournamentPlayerRow.FormatRankLabel` — pure and static so the format is gated by a test, not by reading a screenshot. `The_spec_payload_renders_the_spec_label` produces exactly `#14 · PRIZE #3` from SPEC §1's example body; `A_retired_field_payload_renders_the_plain_rank` → `"2"`; equal ranks → plain rank; null rank → `"--"`. |
| 15 | Provider selection: BotSessionOverride / signed-out / DemoGate → Local | **PASS** | `TournamentBackendPolicy.Choose` checks the bot override FIRST (a bot run reports `signedIn == true` with a placeholder token — an auth check alone would aim entry POSTs at prod AND inflate the human-entry count that retires the bot field one-way). Five tests incl. `The_bot_override_wins_over_everything_else` across all four (signedIn × demo) combinations. |
| 16 | `GetResults` uses the server's `prize_rank`; `ClaimPrize` keeps the existing earn-game path with a Phase-5 NOTE | **PASS** | `GetResults` takes `player.prize_rank ?? player.rank` off the served board and resolves the amount through `LocalTournamentBackend.ResolvePrize` (tie split-pool included, not re-implemented). `ClaimPrize` still grants via `IRewardPointsService.Grant` → the `tournament_prize` earn-game action, carrying an explicit `NOTE — PHASE 5 CUTOVER POINT` block naming what Phase 5 replaces. |
| 17 | Signup modal unchanged visually | **PASS** | Only `OnConfirm`'s payment branch changed. No `[SerializeField]`, no prefab, no scene, no layout constant touched — `git diff` on the file is confined to the `using` line and the body of `OnConfirm`. |
| 18 | Out of scope untouched | **PASS** | No Phase-5 resolver/payout, no dashboard editor, no GPS endpoints, no tournament banners, no Rankings screen. `git status` (§7) shows zero files in those areas. |
| 19 | Standing bans respected | **PASS** | Zero edits under `Assets/Scripts/Physics/`; no `*Gate` scenario added to `Scenarios.cs`; nothing baked into `LabScaffold.unity`; `M_Splash*.mat` untouched. No new `UnityEngine.UI.Button` was added, so the `ButtonPressFeedback` pairing rule has nothing to apply to. |
| 20 | Editor left in a clean, playable state | **PASS** | Not playing, not paused, not compiling; `ShellScene` `IsDirty=false`; **no scene was saved at any point**. See §6. |

**No PARTIAL verdicts.** Every row above is PASS or is not applicable to this task type.

---

## 4. Design notes worth a reviewer's attention

1. **`Register`'s fee argument is deliberately ignored on the remote path.** The interface is
   synchronous and has nowhere to put "insufficient funds" or "window closed", so the signup modal
   calls the new `RegisterAsync` instead. The sync seam remains for non-modal callers (dev entry
   button, harnesses) and forces the fee to 0 with a loud warning — never a second debit.

2. **`GetLeaderboard` returns EMPTY, not a local sim, when nothing is cached.** Falling back to the
   local bot sim on the remote path would show phantom bots no other player can see — precisely
   what this phase exists to end. The screen leaves whatever rows are already up on an empty list.

3. **The sticky row reads the server's `player` object, not an `IsPlayer` scan of `entries`.** The
   server excludes DNF and thru-0 rows from `entries` but always sends `player` when entered, so a
   scan would find nothing for exactly the players who most need the row.

4. **Reconcile is a union by hole number, not a replace.** "Server wins on conflict" is honoured for
   any hole both sides hold; a local-only hole is one still in the submit queue, and deleting it
   would erase a hole the player actually played.

5. **`ITournamentStateDeriver` exists because a concrete-type cast would have failed silently.**
   `TournamentSelectionScreenController` cast `backend as LocalTournamentBackend`; under the wrapper
   that returns null and every entered tournament quietly loses its `Playing` badge. Caught by
   reading the call sites, not by a test — worth re-checking for other concrete casts.

---

## 5. Tests — FULL per-assembly EditMode sweep

Run via `tests-run` (EditMode, whole mode — the tool ignores class filters, so this IS the full
per-assembly sweep across every test asmdef in the project).

```
{"Status":"Passed","TotalTests":1426,"PassedTests":1423,"FailedTests":0,
 "SkippedTests":3,"Duration":"00:01:09.0633380"}
```

The 3 skips are pre-existing and unrelated — all three are
`Golfin.Physics.Tests.HoleCompleteDriverTests.*`, skipped with "Stage C1: HandleShotComplete is now
a no-op" messages that predate this task.

### Membership proof (a green run proves nothing about MY tests)

`tests-run` reports only non-passing results, so a pass is invisible. Membership was proved with a
tripwire pass: one `ZZ_Tripwire` added per new class, run, all seven named back by fully-qualified
name, then removed.

```
{"Status":"Failed","TotalTests":1425,"PassedTests":1415,"FailedTests":7,"SkippedTests":3}
  Golfin.Tournaments.WireupTests.RemoteEntryReconcileTests.ZZ_Tripwire          | Failed
  Golfin.Tournaments.WireupTests.RemoteRegisterNoDoubleChargeTests.ZZ_Tripwire  | Failed
  Golfin.Tournaments.WireupTests.StickyRankLabelTests.ZZ_Tripwire              | Failed
  Golfin.Tournaments.WireupTests.TournamentBackendPolicyTests.ZZ_Tripwire      | Failed
  Golfin.Tournaments.WireupTests.TournamentBoardMappingTests.ZZ_Tripwire       | Failed
  Golfin.Tournaments.WireupTests.TournamentNetDtoParseTests.ZZ_Tripwire        | Failed
  Golfin.Tournaments.WireupTests.TournamentSubmitQueueTests.ZZ_Tripwire        | Failed
```

Counts line up exactly: **1372 before this task → 1418 with the first 46 tests → 1425 with the 7
tripwires → 1426 with the 8 drain tests and the tripwires removed.**

### Coverage against SPEC §5

| SPEC §5 EditMode item | Where | Count |
|---|---|---|
| DTO parse of §1 payloads (incl. `player:null`, `rank:null`, insufficient-funds enter) | `TournamentNetDtoParseTests` | 10 |
| Snapshot → `TournamentLeaderboardEntry` mapping verbatim; no client re-ranking | `TournamentBoardMappingTests` | 6 |
| Sticky-row two-rank label (added — the §4 presentation needs a gate) | `StickyRankLabelTests` | 7 |
| Queue: survives restart (disk), replays FIFO | `TournamentSubmitQueueTests` | 6 |
| Queue: drops on `replayed:true` and on 400 (scripted `IHttpTransport`) | `TournamentSubmitDrainTests` | 8 |
| Register: no `IRewardPointsService` debit on the remote path (the seam) | `RemoteRegisterNoDoubleChargeTests` | 6 |
| Provider selection incl. BotSessionOverride → Local | `TournamentBackendPolicyTests` | 5 |
| Cross-device entry reconcile (added — server-wins is a data-loss risk) | `RemoteEntryReconcileTests` | 6 |
| **Full per-assembly EditMode sweep green** | whole mode | **1426 / 0 failed** |

`TournamentSubmitDrainTests` installs a scripted `IHttpTransport` through
`ApiClient.ConfigureForTest` and pumps `FlushSubmitQueueRoutine` by hand — no play mode, no socket —
so the drop rules are gated on the real routine rather than on a paraphrase of it.

The production types live in Assembly-CSharp, which an asmdef cannot reference, so they are reached
by reflection — the same pattern `RemoteScheduleTests` and `BackendLeaderboardTests` already use.

---

## 6. Editor state at close

```
IsPlaying=False  IsPaused=False  IsCompiling=False
ShellScene  dirty=False
```

`ShellScene` was dirty at kickoff (in-memory only — the on-disk file was git-clean) and blocked
`tests-run`. It was **reloaded from disk, not saved**, so no anchor/layout churn was baked in
(`PIPELINE_HARDENING` §14). `git status` confirms no `.unity` file changed.

---

## 7. Close-out `git status --porcelain --untracked-files=all`

```
 M Assets/Scripts/EconomyRuntime/ServerBalanceSyncBehaviour.cs
 M Assets/Scripts/Net/Endpoints.cs
 M Assets/Scripts/Tournaments/ITournamentBackend.cs
 M Assets/Scripts/Tournaments/LocalTournamentBackend.cs
 M Assets/Scripts/TournamentsRuntime/Tests/Golfin.TournamentsRuntime.Tests.asmdef
 M Assets/Scripts/TournamentsRuntime/TournamentService.cs
 M Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs
 M Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs
 M Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs
 M Docs/AI_CONTEXT.md
 M Docs/Specs/Active/fastlane_testflight_pipeline/IMPLEMENTER_REPORT.md   <- pre-existing
 M Docs/Specs/Active/fastlane_testflight_pipeline/STATUS.md               <- pre-existing
 M Docs/Specs/Active/tournaments_server_side/STATUS.md                    <- pre-existing
 M Docs/TellCode.md                                                       <- pre-existing
 M Docs/Versioning/last_uploaded_build.txt                                <- pre-existing
?? Assets/Scripts/TournamentsRuntime/RemoteTournamentBackend.cs(.meta)
?? Assets/Scripts/TournamentsRuntime/TournamentBackendPolicy.cs(.meta)
?? Assets/Scripts/TournamentsRuntime/TournamentNetDtos.cs(.meta)
?? Assets/Scripts/TournamentsRuntime/TournamentSubmitQueue.cs(.meta)
?? Assets/Scripts/TournamentsRuntime/Tests/TournamentAsyncBoardTests.cs(.meta)
?? Docs/Specs/Active/tournament_async_board/
```

Every `.cs` has its `.cs.meta` alongside it (Lesson R). No `.unity`, no `.prefab`, no
`Assets/Scripts/Physics/`, no `M_Splash*.mat`.

---

## 8. Needs Cesar's device pass (SPEC §5 manual list)

None of these can be reached from the Editor — they need two real accounts, a real network drop and
a real second device. All six are unverified by this iteration and are stated as such.

| # | Manual item | Why it needs a device | What to watch |
|---|---|---|---|
| M1 | Two accounts enter the same tournament → identical board (same bots, same reveal) | Needs a second signed-in account against prod | Both devices' boards agree row-for-row, including the organically-revealed bots |
| M2 | Entry debits the fee exactly once, incl. after a mid-enter network drop + retry | Needs a real drop between the POST and its response | RP falls by exactly the fee; the ledger has ONE `spend_pts` row (the server's uuid5 key is what guarantees it) |
| M3 | Airplane-mode round → reconnect → queue flushes, board shows the score | Needs the real airplane toggle + app resume | Holes play normally offline; on reconnect the board picks up every hole, in order |
| M4 | Resume a half-played tournament on a second device (GET entry reconcile) | Needs a second device on the same account | The second device shows the holes already played; finishing there completes the entry |
| M5 | Sticky row shows `#N · PRIZE #M` while bots active; the 10th human entry retires them | Needs 10 real human entries (or a SQL check / a second fetch) | Label format on screen; after retirement bots are gone, ranks compact, and it never reverts |
| M6 | Ended tournament renders the final board with T-ties | Needs a tournament past `end_at + resolve_delay` | Tie rows share a rank and carry the `T` prefix |

Two extra things worth a glance on the same pass, both consequences of this change rather than
SPEC items:

- **Tournaments card badges** — `ITournamentStateDeriver` restored `Playing` on the remote path;
  confirm an entered-but-unfinished tournament still reads PLAYING on the selection screen.
- **The nav-bar RP counter after entry** — the fee now leaves server-side, so the counter updates
  via the `rp_balance_sync` pull rather than a local debit. It should drop within a beat of the
  modal closing, not on the next Home visit.

---

## 9. STATUS

`READY_FOR_ARCHITECT_REVIEW` — implemented, compiled, and the full EditMode sweep is green. The
manual list in §8 is device-only and is Cesar's pass.
