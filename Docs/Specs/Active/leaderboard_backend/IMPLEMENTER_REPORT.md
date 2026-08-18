# IMPLEMENTER REPORT — `leaderboard_backend`

**Iteration shape:** `rankings:backend-provider-swap`
**Date:** 2026-08-18
**Baseline:** HEAD `2e90f1398`, dirty tree recorded in `HEARTBEAT.log` at kickoff.
**Implemented by:** Claude Code main thread (direct implementation, not the subagent chain).

---

## Files modified or created

| File | Status | One-line summary |
|---|---|---|
| `Assets/Scripts/Net/Endpoints.cs` | M | +2 members: `Leaderboard(period)` and `UserGolfinCharacter` (§2), both documented AUTH REQUIRED. |
| `Assets/Scripts/Net/ApiClient.cs` | M | +`Put<T>` — one line onto the existing `SendRoutine` path with the PUT verb (§1 note). |
| `Assets/Scripts/UI/Rankings/LeaderboardDtos.cs` | NEW | `[JsonProperty]` wire DTOs for the §1 payload; timestamps kept as strings so Newtonsoft can't localise them. |
| `Assets/Scripts/UI/Rankings/BackendLeaderboardProvider.cs` | NEW | The §3 provider: per-period snapshot, async `Refresh`, skew-corrected countdown, plus `LeaderboardDiskCache` (atomic `.tmp`+replace, one file per period). |
| `Assets/Scripts/UI/Rankings/LeaderboardProviderPolicy.cs` | NEW | The §4 selection rule as a pure function, so §7's provider-selection item is testable. **Extra file beyond §9 — see Deviations.** |
| `Assets/Scripts/UI/Rankings/GolfinCharacterSync.cs` | NEW | §5 sync: self-bootstrapping MonoBehaviour, PUTs `{character_id, level}` on select/level-up/sign-in, throttled, silent. |
| `Assets/Scripts/UI/Rankings/LeaderboardManager.cs` | M | Provider selection in `Awake` + re-selection on `AuthService.SignedIn`; `EnsureProviderForSession()` is idempotent. |
| `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` | M | The two §4 refresh hooks in `OnEnable` / `OnTabClicked`, via a shared `RequestRefresh`. |
| `Assets/Scripts/UI/Rankings/Tests/BackendLeaderboardTests.cs` | NEW | 35 EditMode tests covering all six §7 EditMode items, reflection-based per the house pattern. |
| `Docs/Specs/Active/leaderboard_backend/STATUS.md` | M | → `AWAITING_CESAR_MANUAL_PASS`. |
| `Docs/Specs/Active/leaderboard_backend/HEARTBEAT.log` | NEW | Kickoff baseline + progress. |
| `Docs/AI_CONTEXT.md` | M | Priority entry updated from "kickoff gated on deploy" to "Unity half implemented, awaiting device pass". |

**Rule 13 (uncommitted paths outside the task folder).** Everything in the table above is mine. The
remaining dirty paths in `git status` — `ShellScene.unity`, `LocalFakeLeaderboardProvider.cs`,
`Golfin.Tournaments.asmdef`, the telemetry/`PlayerIdentity`/`UsernameRules` files, the localization CSVs
and the `beta_telemetry` docs — are **pre-existing**, and are quoted verbatim in the
`=== iter-1 kickoff baseline ===` block of `HEARTBEAT.log`, captured before I touched anything.
In particular `Assets/Scripts/UI/Rankings/LocalFakeLeaderboardProvider.cs` shows as `M` but I did **not**
edit it (§6: retired, not deleted — it is still the bot/signed-out provider), and
`Assets/Scenes/ShellScene.unity` shows as `M` from before this task; I made **zero** scene edits and
never called `scene-save`.

---

## Acceptance checklist — SPEC §7 EditMode

Every item below is backed by a named test in `BackendLeaderboardTests.cs` that I ran and watched pass.

| # | §7 item | Verdict | Evidence |
|---|---|---|---|
| 1 | DTO parse of the §1 payload (incl. null `character_id`, null `period_end_utc`) | **PASS** | `LeaderboardDtoParseTests` — 6 tests. `Parses_the_spec_payload_through_the_data_envelope` uses the §1 example byte-for-byte; `Null_character_id_becomes_an_empty_string_not_a_crash` asserts the empty-string mapping the widgets already read as "default portrait"; `Null_period_end_utc_yields_MaxValue_so_the_countdown_blanks`. Also `Timestamps_survive_as_raw_strings_not_local_DateTimes`, which is the bug that would have given two players in different zones two different countdowns. |
| 2 | Provider maps payload → `LeaderboardEntry` verbatim (rank/tie NOT recomputed) | **PASS** | `LeaderboardMappingTests` — `Every_field_is_copied_verbatim` checks all 7 fields on both rows; `Server_ties_are_rendered_as_sent_never_recomputed` feeds a 1,2,2,4 board and asserts exactly `{1,2,2,4}` — a client-side re-rank would produce `{1,2,3,4}`, so this test fails loudly if anyone reintroduces ranking here; `Order_is_the_payload_order_not_a_client_sort`. |
| 3 | Countdown end-time math: skewed device clock (±10 min) still yields the server delta | **PASS** | `LeaderboardCountdownTests.Countdown_ignores_device_clock_skew`, parameterised at 0 / +10 / −10 minutes. It performs the *same* subtraction `UpdateCountdownLabel` does (`end − deviceNow`) and asserts the result equals the server's 18h30m in all three cases. |
| 4 | Disk cache round-trip; corrupt cache → null → empty + refresh | **PASS** | `LeaderboardDiskCacheTests` — 8 tests. `Round_trips_the_raw_body_byte_for_byte`, `Writing_leaves_no_tmp_file_behind` (proves the atomic replace completed), `Overwriting_an_existing_cache_replaces_it_atomically` (the `File.Replace` branch), and `A_corrupt_cache_file_yields_an_empty_board_not_a_broken_screen`, which constructs the REAL provider over a truncated file and asserts an empty board + `DateTime.MaxValue` countdown rather than an exception. `A_cached_board_is_on_screen_before_any_fetch` is the airplane-mode open. The fixture saves and restores any real cache the machine already had. |
| 5 | Provider selection: BotSessionOverride → LocalFake; signed-in → Backend | **PASS** | `LeaderboardProviderSelectionTests` — 3 tests, including `A_bot_run_NEVER_reaches_production_even_though_it_looks_signed_in`, which pins the ordering: `BotSessionOverride` installs a fake session so `signedIn` is TRUE during a bot run, and an auth check alone would aim prod requests at a token the server rejects. |
| 6 | Full per-assembly EditMode sweep stays green | **PASS** | See the sweep table below — 18/18 assemblies, 1369 passed / 0 failed / 3 pre-existing skips. |
| + | (not required) §5 sync payload + throttle | **PASS** | `GolfinCharacterSyncPolicyTests` — 6 tests: field names, empty-id rejection, level floor, and that an identical payload is not re-sent while a level-up or character switch is. |

---

## EditMode sweep

Run unfiltered first (executes everything, reports failures from every assembly), then per-assembly for
attribution.

**Unfiltered:** `1372 total · 1369 passed · 0 failed · 3 skipped · 00:01:12`

| Assembly (namespace filter) | Passed | Failed |
|---|---|---|
| Golfin.UI.Rankings.Tests | **52** (17 pre-existing + 35 new) | 0 |
| Golfin.Net.Tests | 18 | 0 |
| Golfin.Auth.Tests | 27 | 0 |
| Golfin.Save.Tests | 44 | 0 |
| Golfin.Economy.Tests | 53 | 0 |
| Golfin.EconomyRuntime.Tests | 6 | 0 |
| Golfin.Tournaments.Tests | 210 | 0 |
| Golfin.TournamentsRuntime.Tests (`Golfin.Tournaments.WireupTests`) | 147 | 0 |
| Golfin.Telemetry.Tests | 17 | 0 |
| Golfin.Physics.Tests | 357 | 0 (+3 pre-existing skips) |
| Golfin.Gameplay.Tests | 302 | 0 |
| Golfin.Course.Tests | 26 | 0 |
| Golfin.Core.Stamina.Tests | 37 | 0 |
| Golfin.SceneSnapshot.Tests | 8 | 0 |
| Golfin.UI.Tests | 5 | 0 |
| Golfin.UI.Shop.Tests (`GolfinRedux.UI.Shop.Tests`) | 8 | 0 |
| Golfin.HoleCompleteModal.Tests | 16 | 0 |
| GolfinRedux.Tests.EditMode | 36 | 0 |
| **Sum** | **1369** | **0** |

The per-assembly sum equals the unfiltered run exactly (1369 + 3 skipped = 1372), which is the check
that no assembly was silently excluded. The 3 skips are the pre-existing `HoleCompleteDriverTests`
Stage-C1 skips, identical to the ones `rp_balance_sync` recorded.

### Tooling note — the suite was very nearly untested

`tests-run`'s `testAssembly` and `testClass` filters return **"No tests found"** for
`Golfin.UI.Rankings.Tests` — including for the *pre-existing* `LeaderboardTests`, so this is not
something this task introduced. Had I trusted that error I would have concluded my new tests could not
run. I proved discovery with a **deliberately failing tripwire test**: adding it moved the discovered
`TotalTests` from 1372 → 1373 and it appeared as a failure under a `testNamespace` filter. So:
**`testNamespace` works, `testAssembly`/`testClass` do not** for this assembly. The tripwire has been
removed (`git status` shows no `_Tripwire.cs`). Related: memory `reference_tests_run_ignores_class_filters`.

Second tooling note: the first `tests-run` after any recompile reliably returns "No tests found"; the
immediate retry succeeds. Retry once before believing that error.

### A real failure the tests caught

The first run came back with 2 failures: the tripwire (expected) and
`Parses_an_already_unwrapped_body_too`. That second one was a bug **in my test**, not in production —
I had derived the unwrapped payload from the enveloped one with `TrimEnd('}')`, which strips *every*
trailing brace, not one, producing invalid JSON. Fixed by writing the unwrapped payload out explicitly
and additionally asserting both forms map to the same row count.

---

## Deviations from the SPEC, and why

Two, both deliberate; neither changes the contract.

**1. One extra new file: `LeaderboardProviderPolicy.cs`.** §9 lists three new production files. §7,
however, requires an EditMode test for *provider selection*, and that decision would otherwise live
inside `LeaderboardManager.Awake` — a MonoBehaviour in Assembly-CSharp, which no test asmdef can
reference and no EditMode test can drive. Extracting the rule to a pure static makes the §7 item real
rather than asserted. This is the same split the codebase already uses twice: `BannerPolicy` beside
`BannerService`, and `ServerBalanceSync` beside `ServerBalanceSyncBehaviour`.

**2. Provider re-selection on `AuthService.SignedIn`.** §4 specifies the choice in
`LeaderboardManager.Awake`. That alone is not sufficient: `LeaderboardManager` is a GameObject in
`ShellScene` (confirmed at line 125059 of the scene file), so `Awake` runs at **boot** — before the auth
gate on a first launch, when the only honest answer is "signed out". A first-launch player would be
pinned to `LocalFakeLeaderboardProvider` for the entire session and would never see the real board. So
selection is now an idempotent `EnsureProviderForSession()` called from `Awake`, from
`AuthService.SignedIn`, and from `RankingsScreenController.OnEnable`; it early-returns when the kind has
not changed, so the backend provider's snapshots are never dropped. `AuthService.SignedIn` is the hook
`rp_balance_sync` landed — no second auth event was invented, per §5.

---

## Spec compliance notes

- **Do NOT re-rank (§1).** Ranking exists nowhere in `BackendLeaderboardProvider`. `MapEntries` copies
  `rank`/`is_tie` and preserves payload order; test item 2 fails if that regresses.
- **Reuse, don't rebuild.** `ApiClient` gained exactly one line; `Endpoints` two members; the disk cache
  is `RemoteBannerSource`'s discipline; the deserializer is `BannerService.Deserialize`'s shape
  (including `DateParseHandling.None`); `PlayerIdentity.DisplayNameOr("YOU")` drives the player's own row.
- **No prefab or scene edits (§4).** None made. `GolfinCharacterSync` self-bootstraps via
  `[RuntimeInitializeOnLoadMethod]`, the same mechanism `ServerBalanceSyncBehaviour` uses.
- **§6 LocalFake retired, not deleted.** Untouched, still selected for bots and signed-out players;
  `fake_players.csv` untouched; its 17 tests still pass.
- **Out of scope (§8).** Nothing in the previous-period popup, leagues, SNS share, fake-pool dashboard,
  tournament leaderboards, or the backend was touched. `ApplyLeagueLabel` is still hardcoded.

Two judgement calls worth naming:

- **The player's local name overrides the server's on the player's own row**, and I apply it to their row
  inside `entries` as well as to the `player` object — otherwise a player who just set a username would
  see the stale server name on the podium and the fresh one on the pinned row, in the same frame. The
  override is applied on *read*, not at map time, because the disk cache is loaded at boot before
  sign-in restores the display name.
- **The refresh in-flight guard lives in the provider and is per period**, not a single bool on the
  screen. §4 says "guard against double-refresh in flight"; a single bool would also block a second tab
  from loading while the first was still in the air.
- **No `PointsBackendFlag` gate.** §4 defines selection as BotSessionOverride/signed-out only, and I
  followed it literally. Worth knowing: turning that flag off for local dev will *not* put the
  leaderboard back on the fakes. Say the word if you want the flag folded into the policy — it is a
  one-line change in `LeaderboardProviderPolicy.Choose`.

---

## What Cesar still has to do — SPEC §7 manual, device

None of these can be done from the editor; all six need a real signed-in device, and four need
conditions I cannot create (a second account, a real earn, airplane mode, a period boundary).

| # | Manual item | Why it needs you |
|---|---|---|
| 1 | Two signed-in accounts see the SAME board on all four tabs | Needs a second real account against prod. |
| 2 | Play a hole → reopen Rankings → all four tabs reflect the earn | Needs a real ledger write through the live earn path. |
| 3 | Player row pinned with correct rank when outside top 100 (fresh account, score 0) | Needs a fresh prod account. |
| 4 | Character switch on device A → device B shows the new portrait after refresh | Needs two devices. |
| 5 | Airplane mode → Rankings opens with the last cached board, no errors | Covered in EditMode by `A_cached_board_is_on_screen_before_any_fetch`, but the real airplane-mode path (a genuine transport failure, not an empty cache) is device-only. |
| 6 | Countdown label matches UTC midnight / Monday / month boundary | Needs a real boundary crossing; the skew math is unit-tested but the label wiring is not. |

Worth watching on item 2: scores are game-action RP only, so an admin grant will correctly **not**
move the board — that is the server filtering by the `game_point_actions` catalog, not a client bug.

---

## Editor state at close

Play mode off, no scenes dirty, only `ShellScene` loaded (as at kickoff), no scene saved at any point,
no leftover cache or tripwire files. `git status` shows exactly the table above plus the pre-existing
kickoff drift.
