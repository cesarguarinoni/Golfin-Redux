IMPLEMENTED

Task: tournament_schedule_refresh
Date: 2026-08-17
Baseline: 5c2d24683 (main, clean apart from the pre-existing NuGet/Packages drift)

## What was built

A throttled `TournamentService.RefreshSchedule()` called from
`TournamentSelectionScreenController.OnEnable`, after the existing cached-list rebuild. No new
systems: the fetch, the atomic cache write, the live→cache→CSV precedence, `OnScheduleChanged`,
`MergePreservingEntered` and the art prefetch are all reused from Phase 3, unchanged.
`Golfin.Net` was NOT added to `Golfin.Tournaments.asmdef` — everything lives in
`Assets/Scripts/TournamentsRuntime/` (Assembly-CSharp), as the rest of Phase 3 does.

### Files

| File | Change |
|---|---|
| `Assets/Scripts/TournamentsRuntime/ScheduleRefreshThrottle.cs` | NEW. In-flight guard + cooldown, pure C# (time passed in) so it is unit-testable without a MonoBehaviour or a socket. `DefaultCooldownSeconds = 60`. |
| `Assets/Scripts/TournamentsRuntime/TournamentService.cs` | `RefreshSchedule()` public entry point; `ScheduleRefreshCooldownSeconds` reads the throttle default (one constant, not two); `Awake` now warms through the same entry point; `RefreshScheduleRoutine` wrapped in `try/finally` so every exit releases the guard; new `TryGetTournament(id)`. |
| `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs` | `OnEnable` renders cached first, then calls `RefreshSchedule()`; subscribes to `ModalController.ModalStackEmptied`; `HandleScheduleChanged` defers the rebuild while `OpenModalCount > 0` and flushes on close; `OnDisable` unsubscribes and clears the deferred flag. |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | `Open` and `OnConfirm` use `TryGetTournament` instead of `Backend.GetTournament`, which THROWS `KeyNotFoundException` (the existing `def == null` checks were dead code). `OnConfirm` now toasts and closes instead of dead-ending. |
| `Assets/Scripts/TournamentsRuntime/Tests/ScheduleRefreshTests.cs` | NEW. 16 EditMode tests — §1 throttle, §2 disappearance at the screen level. |

### §3.3 — what the deactivation switch actually broke

Two distinct holes, both closed:

1. **The list yanking out from under an open modal.** `RebuildCards()` destroys every card, including
   the one the signup modal was opened from. `HandleScheduleChanged` now defers while any modal is
   open and flushes on `ModalStackEmptied`.
2. **The dangling id.** Deferring the rebuild does not stop the BACKEND swapping — the player can sit
   on the modal and press CONFIRM. `MergePreservingEntered` does not protect that tournament (the
   player is not entered yet, and correctly so), so `Backend.GetTournament(id)` threw
   `KeyNotFoundException` from inside a button handler. `TryGetTournament` returns null instead; the
   modal toasts "Tournament no longer available." and closes.

### Deliberate deviation from the spec wording

§3.1 says the cooldown starts from "the last successful fetch". It is armed on every attempt that
**settles**, success or failure. A cooldown armed only by success is the retry storm §3.2 forbids:
offline, `UnityWebRequest` fails in milliseconds, so five screen entries would be five requests.
Covered by `AFailedFetchArmsTheCooldownToo`.

## Acceptance

| # | Item | Status |
|---|---|---|
| 1 | Date edit in admin reflected on re-entry, no relaunch | **NEEDS DEVICE** — code path proven (fetch → map → `Apply` → `OnScheduleChanged` → rebuild), end-to-end needs a live admin edit |
| 2 | Deactivated tournament gone from the list | **NEEDS DEVICE** for the live payload; logic covered by `ANonEnteredTournamentIsGoneAfterTheServerStopsSendingIt` |
| 3 | Deactivated tournament the player ENTERED still listed + playable, no `KeyNotFoundException` | **PASS** (tests) — `AnEnteredTournamentSurvivesAPayloadThatDropsIt`, `AnEnteredTournamentThatVanishedStillBuildsItsCardOnTheScreen`, `TryGetTournamentReturnsNullWhereGetTournamentThrows`. **NEEDS DEVICE** for the live admin flip |
| 4 | Airplane mode → cached list, no error UI, one log line | **PASS** by construction — every failure path leaves the schedule untouched; the only log is `RemoteTournamentSource`'s existing single `LogWarning`. **NEEDS DEVICE** to confirm on hardware |
| 5 | Five entries in ten seconds → one request | **PASS** (tests) — `FiveScreenEntriesInTenSecondsProduceExactlyOneRequest`. **NEEDS DEVICE** to confirm the OnEnable wiring end-to-end |
| 6 | New art on refresh reaches the card without a relaunch | **PASS by reuse, unchanged** — `Apply()` → `WarmArt()` → `TournamentArtService.Prefetch(defs)` runs before `OnScheduleChanged`, then `ApplyCardArt` requests any still-missing URL per card. **NEEDS DEVICE** |
| 7 | Full EditMode green, swept per assembly | **PASS** — see below |

### Test sweep (2026-08-17)

Per assembly, EditMode, all `FailedTests: 0`:

```
Golfin.TournamentsRuntime.Tests   89   (52 RemoteSchedule + 16 NEW + 21 Wireup)
Golfin.Tournaments.Tests         210
Golfin.Gameplay.Tests            302
Golfin.Physics.Tests             357   (3 pre-existing intentional skips, Stage C1)
Golfin.Course.Tests               26
Golfin.Economy.Tests              53
Golfin.Save.Tests                 44
Golfin.Core.Stamina.Tests         37
GolfinRedux.Tests.EditMode        36
Golfin.Auth.Tests                 27
Golfin.Net.Tests                  18
Golfin.UI.Rankings.Tests          17
Golfin.HoleCompleteModal.Tests    16
Golfin.UI.Shop.Tests               8
Golfin.SceneSnapshot.Tests         8
Golfin.EconomyRuntime.Tests        6
Golfin.UI.Tests                    5
```

`Golfin.TournamentsRuntime.Tests` reported exactly 89 passed and the file `[Test]` counts are
52 + 16 + 21 = 89, so all 16 new tests provably executed (the "No tests found" flake hit once on the
first call; the retry ran).

Unfiltered full EditMode: **1262 total, 1259 passed, 0 failed, 3 skipped** (the same three
pre-existing `HoleCompleteDriverTests` skips).

## Not done / needs a device

Everything marked NEEDS DEVICE above requires a build plus a live admin edit against
`admin.golfin.world`. Nothing in this change is verifiable in the Editor beyond the unit level,
because the whole point is a real server payload arriving between two screen entries. Specifically
worth checking on hardware:

- Acceptance 1/2/6 with a real admin edit while the app is backgrounded.
- Acceptance 3 with a real Activate→Deactivate flip on a tournament the device account has entered.
- Acceptance 4 in airplane mode — confirm exactly one log line and no visible change.
- The signup-modal case in §3.3: open the modal, have the admin deactivate that tournament, wait for
  a refresh, press CONFIRM → expect the toast and a clean close, not a stuck modal.

No scene or prefab was touched. No Inspector wiring is required.
