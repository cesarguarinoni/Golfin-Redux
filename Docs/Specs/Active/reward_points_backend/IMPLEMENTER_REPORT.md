# Implementer Report — `reward_points_backend`

Two independent pieces of work have landed under this spec. Both reports are kept in full:

- [Part 1 — Slice 1 (Unity infra)](#part-1--slice-1-unity-infra-2026-08-12) — `Golfin.Net` + `PointsService` + pending-ops queue, flag default OFF.
- [Part 2 — Phase A (backend)](#part-2--phase-a-backend-2026-08-12) — migration + router, since applied and deployed (see `STATUS.md`).

Unity-only template sections (Screenshot, Rejection follow-up, Figma fidelity, UI fidelity lint) are deleted per the template's own instruction — this task builds no UI and references no Figma node.

---

# Part 1 — Slice 1 (Unity infra), 2026-08-12

> Scope: **SPEC §4 Slice 1 only.** Additive infrastructure behind a default-OFF flag.
> **`RewardPointsManager` and every one of its call sites are untouched** (proved below).
> Slice 2 (rebalance + re-point + flag flip) and the playlife repo are explicitly out of scope and were not touched.

**Iteration shape:** `points_backend:slice1_infra`

## Implementation summary

Added two new assemblies. `Golfin.Net` is a small PLAYLIFE API client — Bearer attach from the existing
auth session, `{data:…}` envelope unwrap, transient retry, and a single 401 → refresh → replay — built
against an `IHttpTransport` seam so all of that logic is unit-tested without a socket. `Golfin.Economy`
adds `PointsService` (cached server balance + `RefreshBalanceAsync`) and a persistent, idempotency-keyed
FIFO queue of unsent earns in `Application.persistentDataPath`.

Nothing existing calls any of it, and `PointsBackendFlag` defaults OFF, so the shipped game is
behaviourally identical to HEAD.

## Contract verification — derived from the live API, not from the spec text

The SPEC and the deployed service disagreed in two places. Both were resolved against the **running
service** (probed 2026-08-12) and the **deployed source**, and the code follows the service:

| Claim | What the live API actually does | Where it lands |
|---|---|---|
| "`/points/*` + `/health`" under `https://playlife-api.fly.dev/api/v1` | `/health` is **root-mounted**: `GET /health` → 200 `{"status":"ok","version":"0.1.0"}`, while `GET /api/v1/health` → **404** | `Endpoints.RootUrl` + `Endpoints.BaseUrl` are separate; asserted by `Endpoints_MatchTheDeployedRoutes` |
| "Unauthenticated calls return 403" | True for a **missing header** (403 `{"detail":"Not authenticated"}`). An **invalid/expired JWT** returns **401** `{"detail":"Authentication failed: invalid JWT…"}` | 401 is the refresh trigger; **403 is deliberately NOT**, since no refresh fixes a header that was never sent. Both paths tested |
| Error envelope | Errors are `{"detail": …}`, **not** `{data: …}` | `ApiEnvelope.ExtractErrorMessage` |

Response shapes for `/points/balance` and `/points/earn-game` were transcribed from the deployed
`backend/routers/points.py` and `2026_08_12_points_spend_idempotency.sql` (read-only) rather than guessed
— including the fact that the **earn** payload carries no `gift_pts`, which is why `PointsService`
carries the cached gift bucket forward instead of zeroing it.

## Auth reuse — nothing to flag

The kickoff asked me to flag it if token refresh was not exposed by the auth epic. **It is exposed**, so
no flag is needed and no second auth path was written:

- token — `AuthService.Instance.Session.AccessToken` (`Assets/Scripts/Auth/AuthSession.cs:18`)
- signed-in — `AuthSession.IsAuthenticated` (`:26`)
- refresh — `AuthService.RefreshSession(Action<AuthResult>)` (`Assets/Scripts/Auth/AuthService.cs:126`)
  → `ISupabaseAuthClient.RefreshSession` → `POST /auth/v1/token?grant_type=refresh_token`, with the new
  session persisted by `AuthService.Wrap` (`:232`).

`AuthServiceTokenProvider` is a ~30-line adapter over exactly those three members.

## Files created

| Path | Summary |
|---|---|
| `Assets/Scripts/Net/Golfin.Net.asmdef` | New assembly; references `Golfin.Auth`, precompiled `Newtonsoft.Json.dll` (same posture as `Golfin.Save`) |
| `Assets/Scripts/Net/ApiResult.cs` | `ApiResult<T>` + `ApiErrorKind`; carries `Attempts` / `DidRefreshToken` so retry behaviour is assertable |
| `Assets/Scripts/Net/Endpoints.cs` | The only URLs the game knows: `/points/{balance,earn-game,spend,history}` + root-mounted `/health` |
| `Assets/Scripts/Net/HttpMessages.cs` | `HttpRequest` / `HttpResponse` / `IHttpTransport` — the seam that makes `ApiClient` testable |
| `Assets/Scripts/Net/ApiEnvelope.cs` | `{data:…}` unwrap (passes a non-enveloped body through) + `{detail:…}` error extraction |
| `Assets/Scripts/Net/IAuthTokenProvider.cs` | Auth seam + `AuthServiceTokenProvider`, the adapter over the auth epic's session |
| `Assets/Scripts/Net/ICoroutineRunner.cs` | Runner seam + lazy `DontDestroyOnLoad` host, created only on a real fire-and-forget call |
| `Assets/Scripts/Net/UnityWebRequestTransport.cs` | The shipping transport. No retry/auth/unwrap logic — `ApiClient` owns all of it |
| `Assets/Scripts/Net/ApiClient.cs` | Bearer attach, envelope unwrap, transient retry, 401 → refresh → replay-once |
| `Assets/Scripts/Economy/Golfin.Economy.asmdef` | New assembly; references `Golfin.Net` |
| `Assets/Scripts/Economy/PointsBackendFlag.cs` | `PointsBackendEnabled`, **default OFF**: runtime setter → `GOLFIN_POINTS_BACKEND` define → `false` |
| `Assets/Scripts/Economy/PointsDtos.cs` | `PointsBalance` (`total_points` IS the RP) and `PointsEarnResult`, transcribed from the deployed API |
| `Assets/Scripts/Economy/PendingPointsOp.cs` | One queued earn; mints its idempotency GUID once and serialises the `earn-game` body |
| `Assets/Scripts/Economy/PendingOpsStore.cs` | `IPendingOpsStore` + atomic file store (temp → `File.Replace`) + in-memory store |
| `Assets/Scripts/Economy/PendingOpsQueue.cs` | Persistent FIFO with versioned file, corrupt-file recovery, key-less-op rejection, 500-op cap |
| `Assets/Scripts/Economy/PointsService.cs` | `RefreshBalanceAsync` + cached balance + `EnqueueEarn` / `ReplayPendingRoutine`; flag-gated |
| `Assets/Scripts/Economy/Editor/Golfin.Economy.Editor.asmdef` | Editor-only assembly for the toggle |
| `Assets/Scripts/Economy/Editor/PointsBackendMenu.cs` | `GOLFIN > Points Backend > …` — flag toggle, balance probe, queue dump. Console only, no dialogs |
| `Assets/Scripts/Net/Tests/Golfin.Net.Tests.asmdef` | EditMode test assembly |
| `Assets/Scripts/Net/Tests/NetTestDoubles.cs` | Scripted transport / auth provider / runner + a **bounded** coroutine pump |
| `Assets/Scripts/Net/Tests/ApiClientTests.cs` | 18 tests — envelope, Bearer, 401-refresh, retry budgets, status mapping, endpoint URLs |
| `Assets/Scripts/Economy/Tests/Golfin.Economy.Tests.asmdef` | EditMode test assembly |
| `Assets/Scripts/Economy/Tests/PendingOpsQueueTests.cs` | 14 tests — disk round-trip, key stability, FIFO, corrupt/versioned file, request body |
| `Assets/Scripts/Economy/Tests/PointsServiceTests.cs` | 14 tests — the flag gate, balance cache, replay ordering |

Every `.cs` has its generated `.cs.meta` alongside it (Lesson R). **No existing file was modified.**

## Design decisions worth knowing

| Decision | Why |
|---|---|
| Nested coroutines are pumped with `while (inner.MoveNext()) yield return inner.Current;`, never `yield return inner` | Unity auto-nests either form, but only the explicit one also works under a plain `MoveNext()` pump. That is what lets the retry and 401 branches be covered in **EditMode**, with no play mode and no network |
| `ApiClient` / `PointsService` are plain C# singletons, not MonoBehaviours | Constructible in a test. They borrow a lazily-created `DontDestroyOnLoad` host only for fire-and-forget calls — so with the flag OFF, **no GameObject is ever created** |
| 401 refresh and transient retry have **separate** budgets | A shared counter would let a refresh eat the retry budget, and a retry re-arm the refresh — the pair that loops forever. `RefreshDoesNotConsumeTheTransientRetryBudget` and `Unauthorized_SecondConsecutive401DoesNotLoop` pin both halves |
| 403 does **not** trigger a refresh | Live behaviour: 403 means no Authorization header was sent at all. Refreshing cannot fix that |
| The flag gate lives in the **coroutine**, not the `…Async` wrapper | Otherwise a caller reaching the routine directly (a test, a future harness) would bypass the kill switch |
| A server **refusal** (`{awarded:0, reason:…}`, HTTP 200) consumes the queued op | Daily-cap / unknown-action is a definitive answer. Treating it as a failure would retry it on every reconnect, forever |
| Replay **stops** at the first transport failure instead of skipping the op | The server couples earns to avatar XP and evaluates `daily_cap` against what already landed today, so out-of-order replay produces a different level and a different capped set than the player earned |
| An earn response folds in with the **cached** `gift_pts` | The earn payload has no `gift_pts` (earns only touch activity). Writing 0 would under-report RP until the next full refresh |
| Ops loaded without an idempotency key are **dropped** | An unkeyed replay has no double-credit protection. Losing one queued earn beats crediting it twice |
| Editor menu item rather than a debug-panel toggle | The obvious host, `Debug/RewardPointsDebugPanel`, is a `RewardPointsManager` call site — off-limits this slice. For a **device** build the switch is the `GOLFIN_POINTS_BACKEND` define, since Editor PlayerPrefs do not travel to the phone |
| Newtonsoft rather than `JsonUtility` | The payloads are snake_case and `JsonUtility` has no field-name mapping — the same reason `Golfin.Save` already takes this dependency |

## Acceptance checklist (SPEC §4 Slice 1)

| Item | Result | Justification |
|---|---|---|
| New asmdef `Golfin.Net` | PASS | `Assets/Scripts/Net/Golfin.Net.asmdef`. Compiled: `Library/ScriptAssemblies/Golfin.Net.dll` (20,480 B, 15:02). |
| `ApiClient` singleton, UnityWebRequest | PASS | `ApiClient.Instance` (lazy, plain C#) over `UnityWebRequestTransport`. Not a MonoBehaviour so it is testable; the coroutine host is separate and lazy. |
| Bearer attach | PASS | `ApplyAuthHeader` stamps `Authorization: Bearer <token>` **per attempt** (a refresh changes the token mid-call). `Send_AttachesBearerToken` asserts the header; `Send_OmitsBearerWhenSignedOut` asserts it is absent when signed out. |
| `{data}` envelope unwrap | PASS | `ApiEnvelope.TryUnwrap`. `Get_UnwrapsDataEnvelope` (475 RP out of the real balance envelope); `Get_PassesThroughBodyWithNoEnvelope` covers un-enveloped `/health`; `Get_EmptyBodyIsSuccessNotParseFailure`; `Get_MalformedJsonIsParseError`. |
| Retry on 408 / connection failure | PASS | Separate transient budget, `MaxTransientRetries = 2`. `ConnectionFailure_RetriesThenSucceeds` (2 attempts), `ConnectionFailure_ExhaustsRetryBudgetThenFailsNetwork` and `Timeout408_RetriesThenFailsAsTimeout` (both exactly `MaxTransientRetries + 1`). |
| 401 → refresh → retry **once** | PASS | `Unauthorized_RefreshesThenRetriesOnceWithTheNewToken` — 1 refresh, 2 HTTP calls, and the replay carries `Bearer tok-refreshed`, not the stale token. `Unauthorized_RefreshFails_ReturnsUnauthorizedWithoutRetrying` (1 call). `Unauthorized_SecondConsecutive401DoesNotLoop` (2 calls, 1 refresh — no loop). |
| Static `Endpoints`, `/points/*` + `/health` only | PASS | Five members, nothing else. `Endpoints_MatchTheDeployedRoutes` pins all five against the live routes, including the root-mounted `/health`. |
| `ApiResult<T>` | PASS | `ApiResult.cs`, with `ErrorKind` / `Attempts` / `DidRefreshToken`. |
| Reuse the auth epic's session; flag it if refresh is not exposed | PASS | Refresh **is** exposed — `AuthService.RefreshSession` (`AuthService.cs:126`). `AuthServiceTokenProvider` adapts token + refresh; no second auth path exists. See § Auth reuse. |
| `PointsService` in `Golfin.Economy` | PASS | `Assets/Scripts/Economy/PointsService.cs`; `Golfin.Economy.dll` (18,432 B). |
| `RefreshBalanceAsync()` | PASS | Present, callback-shaped to match the auth epic's deliberately coroutine-friendly convention. `RefreshBalance_CachesTotalPointsAsRewardPoints` asserts the URL, the 475 total and the change event. |
| Cached balance | PASS | `LastBalance` / `Balance` / `HasBalance` / `OnBalanceChanged`. `RefreshBalance_ZeroBalanceIsStillKnown` separates "0 RP" from "unknown" (new accounts start at 0 — decision #6); `RefreshBalance_FailureLeavesTheCacheUntouched`. |
| Persistent JSON queue in `Application.persistentDataPath` | PASS | `FilePendingOpsStore.DefaultPath` = `<persistentDataPath>/points_pending_ops.json`, written atomically. `RoundTrip_SurvivesAFullReloadFromDisk` uses a **real temp file** and a fresh queue instance, i.e. it actually crosses the disk boundary. |
| Idempotency GUID per op | PASS | Minted once in `PendingPointsOp.NewEarn`, never regenerated. `IdempotencyKey_IsAUniqueGuidPerEnqueue` (50 unique, all `Guid.TryParse`-able — the server casts to `uuid`), `IdempotencyKey_IsStableAcrossSaveLoadCycles` (5 reloads), `IdempotencyKey_IsUnchangedByAFailedAttempt`. |
| FIFO replay on reconnect/login | PASS | `Replay_SendsOpsOldestFirstAndDrainsTheQueue` asserts the three keys appear **on the wire** in enqueue order. `Replay_StopsAtTheFirstFailureAndKeepsOrder` — head unchanged, #3 never attempted. `Replay_ResumesInOrderOnTheNextConnection` — offline round loses nothing, next round drains in order. |
| Earn ops only in v1 | PASS | `PendingOpKind` has exactly one member (`Earn`). There is no spend op to enqueue by accident. |
| Flag `PointsBackendEnabled`, default OFF | PASS | `PointsBackendFlag.DefaultEnabled = false`; `CompiledDefault_IsOff` asserts const, compiled default, and post-reset runtime value. |
| Flag OFF ⇒ nothing existing calls the new paths | PASS | Two independent proofs. (a) **Nothing references it**: a word-boundary grep for `Golfin.Economy`, `Golfin.Net`, `PointsService`, `PointsBackendFlag`, `ApiClient`, `ApiResult`, `ApiEnvelope`, `PendingOpsQueue`, `PointsBalance`, `PendingPointsOp` across `Assets`, excluding the two new folders → **NONE**; no existing `.asmdef` references either new assembly. (b) **Even if called, it is inert**: `FlagOff_RefreshBalanceMakesNoRequest` (0 transport calls), `FlagOff_EnqueueEarnWritesNothing` (0 store writes), `FlagOff_ReplayIsANoOp` (0 calls, op stays queued). |
| Do NOT touch `RewardPointsManager` or its call sites | PASS | `git status --porcelain --untracked-files=all` shows **every** path from this task as `??` (new). `RewardPointsManager.cs`, `RewardGranter`, `RewardPointsServiceAdapter`, `ClubLevelUpModalController`, `TournamentSignupModalController`, `ModeCardController`, `RewardPointsDebugPanel` are all absent from the diff. |
| Do NOT touch the playlife repo | PASS | Read-only there: `sed`/`grep` on `routers/points.py` and the migration to transcribe response shapes. Zero writes. |
| Slice 2 not started | PASS | No `EarnPoints` reason parameter, no `SpendAsync`, no `IRewardPointsService` async variant, no CSV/rebalance edits, no `DEFAULT_STARTING_POINTS` change. `Endpoints.PointsSpend` exists as a URL constant with no caller. |
| EditMode: queue round-trip | PASS | `PendingOpsQueueTests` — round-trip, empty, missing file, corrupt file, unknown version, malformed-op rejection, cap eviction. |
| EditMode: idempotency-key stability | PASS | Three dedicated tests (above), plus `ToEarnGameJson_*` pinning the body against the deployed `EarnGameRequest` model, including omitting `amount` so catalog-fixed actions take the server's value. |
| EditMode: replay ordering | PASS | Four dedicated tests (above), plus `Replay_IdempotentServerReplayIsAcceptedNotRetriedForever` and `Replay_ServerRefusalConsumesTheOpInsteadOfLoopingForever`. |
| EditMode: ApiClient envelope/401 via mocked transport | PASS | `FakeHttpTransport` / `FakeAuthTokenProvider`; 18 tests. The pump is bounded (10,000 steps) so a runaway-retry regression **fails** rather than hanging the Editor. |
| Suite stays green | PASS | See § Verification. |

## Verification performed

| Check | Result |
|---|---|
| Compilation | **Clean.** All 5 new assemblies built: `Golfin.Net.dll`, `Golfin.Net.Tests.dll`, `Golfin.Economy.dll`, `Golfin.Economy.Tests.dll`, `Golfin.Economy.Editor.dll`. `console-get-logs` filtered to `Error` → `[]`. |
| `Golfin.Net.Tests` | **18 passed / 0 failed.** Run with `includePassingTests` — each test returned by name with `Status: Passed`, not just a count. |
| `Golfin.Economy.Tests` | **28 passed / 0 failed.** |
| **Full EditMode suite** | **1162 total / 1159 passed / 0 FAILED / 3 skipped**, 63.3 s. The 3 skips are the pre-existing Stage-C1 `HoleCompleteDriverTests` `[Ignore]`s, untouched by this task. 1162 − 46 new = 1116 pre-existing. |
| Live API contract probe | `GET /health` → 200 `{"status":"ok","version":"0.1.0"}`; `GET /api/v1/health` → 404; `GET /api/v1/points/balance` → 403 `{"detail":"Not authenticated"}`; with a bogus Bearer → **401** `{"detail":"Authentication failed: invalid JWT…"}`; `POST /points/earn-game` and `/points/spend` → 403 (deployed, not 404). |
| No inbound references | Word-boundary grep across `Assets` excluding the two new folders → NONE, for types and asmdefs both. |
| Working-tree scope | Every path from this task is `??`. The 9 pre-existing `M` paths (`ShellScene.unity`, `Scenarios.cs`, `SignUpScreenController.cs`, `SplashScreenController.cs`, `AI_CONTEXT.md`, `UI_HIERARCHY.md`, `TellCode.md`, 2 bot log files) were **already modified at kickoff** — captured from `git status` at HEAD `a96c7799d` before any edit, and none is touched by this work. |
| Editor left clean | `IsPlaying: false`, `IsCompiling: false`, `IsUpdating: false`; `ShellScene` open with `IsDirty: false`. No play mode entered, no scene opened or saved. |

⚠️ **`tests-run` reports `TotalTests` for the whole EditMode registry even when filtered** (a known quirk of
this tool), so the per-assembly rows above are read from `PassedTests`, and the 18/28 counts match the
authored test counts exactly. The `includePassingTests` run additionally returned each new test **by
name** with `Passed`, which is what actually establishes they executed.

## Manual acceptance — NOT DONE, needs Cesar (device/simulator + a live login)

SPEC §4: *flag ON + logged in → `RefreshBalanceAsync` logs the test account's real server balance.*
This cannot be done from here: it needs a signed-in Supabase session on a device or the running Editor.

**In the Editor** (fastest):
1. Enter play mode and sign in as the test account.
2. `GOLFIN > Points Backend > Enabled (PointsBackendEnabled)` — the item shows a checkmark when ON.
3. `GOLFIN > Points Backend > Log Server Balance Now`.
4. Expect `[PointsBackend] ✅ Server balance for <email>: RP=<n> (activity=…, gift=…, avatar L…/…xp)`.
   The menu item refuses with a clear Console warning if you are not in play mode, the flag is off, or you
   are signed out (an unauthenticated call would just 403).

**On device**, add `GOLFIN_POINTS_BACKEND` to Player Settings → Other Settings → Scripting Define Symbols
before building — PlayerPrefs set in the Editor do not travel to the phone.

⚠️ Note for whoever runs it: the flag toggle **persists in PlayerPrefs**. Turn it back off (or use
`Reset Flag To Compiled Default`) when finished, so the Editor does not sit in a non-default state.

## Known FAIL items

None. Two things are open by design rather than by failure:
- the manual device/Editor acceptance above (needs a live login — Cesar's action);
- Slice 2, which is a separate kickoff and was deliberately not started.

## Flagged for Slice 2 (not acted on)

1. **`/points/earn-game` amount semantics.** The deployed router takes the catalog amount when
   `game_point_actions.pts` is set and otherwise validates the client amount against `max_per_event` /
   `daily_cap`. `PendingPointsOp` omits `amount` when non-positive so catalog-fixed actions cannot have a
   client value imposed. Slice 2 must decide per action which form each earn call site sends.
2. **Catalog is still placeholder.** `STATUS.md` records the live seed as `hole_complete` 10 / cap 500,
   `versus_win` 30 / cap 500, `tournament_prize` client-amount / cap 5000 — pending the Slice-2 mirror of
   the approved `RP_REBALANCE.md` §3. The client sends whatever action string it is given; nothing here
   depends on those numbers.
3. **`daily_cap` refusals are silent to the player.** A capped earn returns HTTP 200 `{awarded:0}`; the
   op is consumed and a warning logged. If a capped earn should surface in the UI, that is a Slice-2
   decision.
4. **Leaderboard accumulators stay local** (SPEC §5) — unchanged and untouched here.

---

# Part 2 — Phase A (backend), 2026-08-12

> Scope: **SPEC §3 Phase A only.** No Unity/GolfinRedux code was touched. Slice 1 and Slice 2 are separate kickoffs.
> Unity-only template sections (Screenshot, Rejection follow-up, Figma fidelity, UI fidelity lint) are deleted per the template's own instruction — this task builds no UI and references no Figma node.
>
> ⚠️ **The "NOT DONE" apply/deploy section below is now HISTORICAL** — the migration was applied to prod and
> `fly deploy` completed on 2026-08-12. See `STATUS.md` (later 6 / later 7 / later 8).

## Implementation summary

Added the server side of the one-shared-RP-value design to the PLAYLIFE FastAPI backend
(`/Users/cesar/Documents/playlife/backend`): a migration that gives `points_transactions` an
idempotency key, an idempotent `earn_pts_v2` (identical avatar-XP-coupled semantics to
`earn_activity_pts`, which is left untouched), a row-locked `spend_pts` that debits activity_pts
before gift_pts and returns a distinct `insufficient` result, and the `game_point_actions` catalog
seeded with clearly-marked PLACEHOLDER amounts. The router gained `POST /points/earn-game` and
`POST /points/spend` in the existing house style (`{data}` envelope, `get_current_user`, service
client); `/balance`, `/earn`, `/history` and `/redeem` are byte-unchanged.

**The migration is WRITTEN but NOT APPLIED** — nothing has run against Supabase, and nothing has
been deployed to fly. See § Apply + deploy.

## Files modified or created

| Path | Change |
|---|---|
| `backend/migrations/2026_08_12_points_spend_idempotency.sql` | **created** — idempotency key + partial unique index, `earn_pts_v2`, `spend_pts`, `game_point_actions` (+ placeholder seed), security grants on every new object, staging verification footer |
| `backend/routers/points.py` | **modified** — added `POST /points/earn-game` and `POST /points/spend`, their request models, the game action label map, the `once_per_user` key namespace, and three private helpers. Existing endpoints untouched |

Paths are relative to `/Users/cesar/Documents/playlife`. Nothing outside that repo changed.

## Design decisions worth knowing (deviations / judgement calls)

| Decision | Why |
|---|---|
| Split spends write the **gift** row with a derived key `md5(key‖':gift')::uuid` | The spec mandates a partial unique index on `(user_id, idempotency_key)`, so a two-bucket spend cannot put the same key on both rows. The derived key is deterministic, so replay still finds both rows and returns the exact split, and it cannot collide with a client-supplied key. |
| `insufficient` returns a JSON payload, **not** an exception / 4xx | The spec asks for a result the client branches on. Raising would be indistinguishable from a server fault to the Unity `ApiClient`, and nothing is written on the insufficient path, so the same key can succeed later once the player has the points. |
| `once_per_user` actions **override** the client's idempotency key with `uuid5(NS, "<user>:<action>")` | Makes "once" atomic via the unique index instead of a racy router pre-check. `golfin_welcome` and `legacy_balance_migration` cannot double-grant even from a fresh install with a new key. |
| `daily_cap` / `max_per_event` enforced **in the router** | `max_per_event` is exact (single value check). `daily_cap` is documented in-code as a best-effort, non-atomic pre-check — it bounds honest clients; the atomic guarantee (no double-credit per key) lives in the RPC. |
| Added `coalesce(...)` around every balance read/write | `profiles.activity_pts / gift_pts / total_points / avatar_*` are nullable (`DEFAULT 0`, **not** `NOT NULL`). Without it, a NULL bucket makes the insufficient-funds comparison evaluate to NULL — the branch is skipped and the debit writes NULL back into the balance. |
| Replay lookups scoped by `type` | Prevents a reused key from returning a different action's amount (or an earn's positive amount) as if it were this call's. A genuinely reused key now trips the unique index and rolls the whole call back loudly, which is the right outcome for a client bug. |

## ⚠️ Pre-existing inconsistency surfaced (NOT fixed — needs Cesar's call)

`add_gift_pts_to_receiver()` (`supabase/migrations/20260409000000_dual_currency_gifts_badges_followers.sql:105`)
credits `gift_pts` **without** adding to `total_points`, and `gifts.py` inserts `gift_pts_awarded`
the same way. Nothing maintains the invariant, so on any account that received a gift,
`total_points < activity_pts + gift_pts`.

Under the one-value decision `total_points` **is** the game's RP balance — so those points are
invisible to the game, and a spend that reaches into `gift_pts` debits a total that never counted
them. The fix is small (add `total_points = total_points + NEW.gift_pts_awarded` to the trigger,
plus a one-time reconciliation UPDATE) but it changes live PLAYLIFE balances, so it is deliberately
out of scope here. Flagged in the migration header with the detection query:

```sql
select id, activity_pts, gift_pts, total_points from public.profiles
 where coalesce(total_points,0) <> coalesce(activity_pts,0) + coalesce(gift_pts,0);
```

Worth resolving before the Slice-2 cutover.

## Acceptance checklist (SPEC §3)

| Item | Result | Justification |
|---|---|---|
| Migration named `backend/migrations/2026_08_12_points_spend_idempotency.sql` | PASS | Created at exactly that path. |
| Mirrors `2026_06_29_points_atomic.sql` conventions | PASS | `create or replace` / `add column if not exists` / `on conflict do nothing` throughout; the same header-rationale → object → SECURITY block → staging-footer layout; identical `revoke … from public, anon, authenticated` + `grant … to service_role` wording adapted per signature. |
| Revoke/grant block on EVERY new function | PASS | Present for `earn_pts_v2(uuid,text,int,text,uuid)` and `spend_pts(uuid,int,text,uuid)`; `game_point_actions` additionally gets RLS-enabled-with-no-policies + `revoke all from anon, authenticated`. |
| `points_transactions.idempotency_key` + partial unique index | PASS | `add column if not exists idempotency_key uuid` plus `points_transactions_user_idem_key` unique on `(user_id, idempotency_key) where idempotency_key is not null`. Nullable, so every existing writer (gifts/score/iap//earn) is unaffected. |
| `earn_pts_v2` = same body/semantics as `earn_activity_pts` incl. avatar coupling | PASS | Same `activity_pts + total_points + avatar_xp` increment, same `while v_xp >= v_level * 500` carry-remainder loop, same conditional level/xp write-back, same ledger insert with `currency='activity'`. Additions are the `FOR UPDATE` lock (taken before the replay check), the replay branch, and null-safety. |
| `earn_pts_v2` idempotent replay, no double credit | PASS | Replay branch returns the original `awarded` with current balances and `replayed: true` before any UPDATE runs; the per-user row lock is taken first so two concurrent same-key calls serialize. Verification steps 1–3 in the footer assert one ledger row and unchanged balances. |
| `earn_activity_pts` left untouched | PASS | Not referenced by the migration; the PLAYLIFE app's `/points/earn` path is unchanged. |
| `spend_pts` row-locked | PASS | `select … from public.profiles where id = p_user_id for update` — same lock discipline as earn, held to commit. |
| `spend_pts` debits activity_pts first, then gift_pts | PASS | `v_from_activity := least(v_activity, p_amount); v_from_gift := p_amount - v_from_activity`. Order is a server-side constant, called out in-file against SPEC §6.2 (資金決済法 / Ken-legal open item). |
| `total_points` kept consistent | PASS | Decremented by the full `p_amount` in the same UPDATE as both buckets. |
| Distinct `insufficient` result | PASS | Returns `status='insufficient'` with `requested`, `shortfall` and all three balances, and writes nothing (footer step 8 asserts zero ledger rows for that key). |
| Negative ledger row(s) with the bucket split | PASS | One `-v_from_activity` row `currency='activity'` and, when the spend crosses into gifts, one `-v_from_gift` row `currency='gift'` — so `/points/history?currency=` stays correct. |
| No `avatar_xp` change on spend | PASS | `spend_pts` never references `avatar_xp` or `avatar_level`; footer step 5 asserts both unchanged. |
| `spend_pts` idempotent | PASS | Replay branch sums the rows keyed by `(p_key, derived gift key)` and returns the exact original split with `replayed: true`; footer steps 6–7 assert one/two rows and unchanged balances. |
| `game_point_actions` table with the specified columns | PASS | `action pk, pts int null, max_per_event int null, daily_cap int null, once_per_user bool default false` — `pts` null vs set is exactly the "client amount, cap-validated" vs "fixed server amount" switch. |
| Seeded with the 5 actions, `once_per_user` where specified | PASS | `hole_complete`, `versus_win`, `tournament_prize`, `golfin_welcome` (once), `legacy_balance_migration` (once). |
| Amounts commented as PLACEHOLDERS | PASS | A ⚠️ block above the seed states they are not the shipping economy, that real values come from the Slice-2 `RP_REBALANCE.md` Cesar approves, and repeats the GPS anchors; every value row also carries an inline `-- PLACEHOLDER` comment. |
| `POST /points/earn-game` in existing style | PASS | `{data}` envelope, `Depends(get_current_user)`, module-level service client, JP ledger labels mirroring `ACTION_LABELS` — resolves catalog-fixed amount else validated client amount, then `rpc("earn_pts_v2", …)`. |
| `POST /points/spend` with explicit insufficient payload | PASS | `rpc("spend_pts", …)` and returns the RPC payload as-is, so `status: "insufficient"` + `shortfall` reach the client at HTTP 200 (branchable, no exception handling in `UnityWebRequest`). |
| `/balance`, `/earn`, `/redeem` untouched | PASS | `git diff --stat` = **222 insertions, 2 deletions**, and both deletions are the module docstring line and the `from fastapi import …` line, each expanded in place. No line inside `get_balance`, `earn_activity_pts`, `get_points_history` or `redeem_points` changed. |
| Tests if the repo has a test setup, else staging verification SQL in the footer | PASS | Backend has **no** Python test setup (no `pytest` in `requirements.txt`, no `tests/`, no `conftest.py`; `test/` is Flutter/Dart). Delivered the footer instead: 11 numbered blocks covering single call, idempotent replay, null-key behaviour, level-up boundary, spend happy path, spend replay, the activity→gift split boundary, insufficient funds, both concurrency loops, catalog seed, and a grant check that `authenticated` cannot execute either function. |

## Verification performed

| Check | Result |
|---|---|
| `python3 -m py_compile routers/points.py` | OK |
| AST scan for duplicate/shadowed defs | Clean — `earn_game_pts`, `spend_pts` distinct from the existing `earn_activity_pts` |
| Endpoint logic driven under a stub harness (fastapi/pydantic/supabase stubbed; backend deps aren't installed on this Mac) | **18/18 checks pass** — catalog-fixed amount beats a client-sent amount; unknown action returns `awarded: 0` with no RPC; variable action 400s without an amount and above `max_per_event`, passes at the boundary; `once_per_user` produces the same deterministic key from two different client keys; daily cap refuses at the boundary and credits below it; non-UUID key 400s on both endpoints; spend passes amount/reason/key through and 400s on zero/negative/blank-reason, truncating a 500-char reason to 200 |
| SQL executed anywhere | **NO** — no local Postgres/psql/docker on this Mac. The staging footer is the verification path. |

Harness (throwaway, not added to the repo — the backend has no test infra to add it to):
`/private/tmp/claude-501/-Users-cesar-Documents-GolfinRedux/d0ed8de0-5497-46a0-af04-53b57f8e0b6b/scratchpad/probe_points_router.py`

## Apply + deploy — NOT DONE, Cesar's call

1. **Verify-first (SPEC §3.3).** In the Supabase SQL editor, confirm the functions this builds beside actually exist in prod:
   ```sql
   select proname from pg_proc p join pg_namespace n on n.oid = p.pronamespace
    where n.nspname='public' and proname in ('earn_activity_pts','apply_score_submit');
   ```
2. **Apply on staging**, run the footer blocks, then apply to prod — paste `backend/migrations/2026_08_12_points_spend_idempotency.sql` into the Supabase SQL editor and Run. Cesar does this, or the Architect drives it through the browser session.
3. **Deploy** — `fly deploy` from `/Users/cesar/Documents/playlife/backend` (app `playlife-api`, region `nrt`), on Cesar's go.
   ⚠️ **`flyctl` is not installed on this Mac at all** (`which flyctl` and `which fly` both find nothing) — so it is not merely unauthenticated. Install (`curl -L https://fly.io/install.sh | sh`) and `fly auth login` before the deploy step.
4. **Order matters:** migration first, deploy second. The new endpoints call RPCs that do not exist until the SQL is applied; deploying first would leave `/points/earn-game` and `/points/spend` returning RPC-not-found. `/balance`, `/earn`, `/history`, `/redeem` are unaffected either way.

## Known FAIL items

None. Every SPEC §3 item is PASS. Two things are open by design, not by failure:
- The migration is unapplied and the code undeployed (both explicitly out of scope — Cesar's/Architect's action).
- The `gift_pts` → `total_points` trigger gap above needs a decision before the Slice-2 cutover.
