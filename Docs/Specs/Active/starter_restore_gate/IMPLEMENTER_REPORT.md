# Implementer Report — `starter_restore_gate`

> Implemented directly by the architect thread (Claude Code) at Cesar's instruction, **not** via the
> `golfin-implementer` → self-review → reviewer → red-team chain. Code-only task: no prefab, scene,
> CSV, playlife or Figma surface, so there is no canonical screenshot, no Figma-fidelity table and no
> UI-fidelity lint. Evidence is the EditMode sweep plus a play-mode verification run against the live
> save. **The world-check — delete + reinstall on Cesar's iPhone — is still open and is his to run.**

## Implementation summary

The starter picker no longer races the server. `InventorySyncService` now reports **what** its boot
read answered (`LastBootOutcome`: `NotRun` / `Succeeded` / `Failed`) and raises `OnBootFinished` when
it does; a new `StarterGate.Resolve` is the single place the three post-auth routers ask "picker, or
not?", and it either answers instantly (a local starter, or a bot/demo path) or waits for that one
answer. A failed fetch resolves `ServerUnreachable` and shows `AUTH_ERR_OFFLINE` with a retry — it can
never reach the picker (D1). Separately, `InventorySyncService.OnRestored` now fires whenever a merge
actually changed the save, and `InventoryCatalogAdapter` uses it to re-read the four runtime managers
that build their dictionaries once in `Awake`, so a restored roster/bag is visible in the same session
instead of only after the next launch.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/InventorySync/InventorySyncService.cs` | modified — namespace-level `BootOutcome` enum; `LastBootOutcome`, `BootInFlight`, `OnBootFinished`, `OnRestored`; `Boot()` clears the outcome to `NotRun` on every real attempt and sets Succeeded/Failed in the fetch callback; `RestoreFrom` and the stale-merge path raise `OnRestored` when `ApplyAndCount` changed something; `Reset()` clears both new fields. |
| `Assets/Scripts/InventorySync/InventorySyncBehaviour.cs` | modified — `public static RetryBoot()`; `TryBoot` now bails on `BootInFlight` (no double GET) and re-runs a `Failed` boot. |
| `Assets/Scripts/UI/Account/StarterGate.cs` | **created** — `StarterRoute` + `StarterGate.Resolve`, the five ordered rules, with three seams (`NeedsStarterProbe`, `BypassProbe`, `RequestBoot`) so the rules are EditMode-testable without a scene. No timeout, by design (D1). |
| `Assets/Scripts/UI/Account/LoginScreenController.cs` | modified — password + OAuth success now route through `RouteAfterAuth()` → `StarterGate`; `ServerUnreachable` shows `AUTH_ERR_OFFLINE` and arms `_starterRetryPending`, which makes the next LOGIN tap a fetch retry instead of a second sign-in. |
| `Assets/Scripts/UI/Account/CreateUsernameScreenController.cs` | modified — same shape after `UpdateDisplayName` succeeds; the retry tap only re-runs the gate (the name is already claimed). |
| `Assets/Scripts/UI/SplashScreenController.cs` | modified — `RouteAuthenticated` keeps the `HasDisplayName` check first, then `RetryBoot()` + `StarterGate.Resolve`; `ServerUnreachable` borrows the START button's existing `TMP_Text` for `AUTH_ERR_OFFLINE` and the next tap restores the caption and retries. No new prefab, no new string. |
| `Assets/Scripts/CharacterManager.cs` | modified — `public void ReloadFromSave()` (delegates to the existing private `LoadRoster()`). |
| `Assets/Scripts/ClubManager.cs` | modified — `public void RehydrateFromSave()` = `HydrateFrom(save)` + `OnInventoryChanged`; deliberately NOT `InitializeClubs` (one-shot seeding + wedge backfill stay one-shot). |
| `Assets/Scripts/ItemManager.cs` | modified — `public void ReloadFromSave()`; this manager caches, so it needed one. |
| `Assets/Scripts/BallManager.cs` | modified — `public void ReloadFromSave()`; same reason. |
| `Assets/Scripts/InventoryCatalogAdapter.cs` | modified — subscribes `InventorySyncService.OnRestored` in `Awake`, unsubscribes in `OnDestroy`, and calls the four reload methods. |
| `Assets/Scripts/UI/Roster/UI/RosterScreenController.cs` | modified — starter mode subscribes `OnRosterChanged`; a starter that arrives late navigates Home and unsubscribes; unsubscribed in `OnDisable`. |
| `Assets/Scripts/InventorySync/Tests/InventoryBootOutcomeTests.cs` | **created** — 15 tests: outcome states, `OnBootFinished` timing/arity, `OnRestored` fires-only-on-change, stale-merge, retry-reopens-the-question. |
| `Assets/Scripts/InventorySync/Tests/InventorySyncServiceTests.cs` | modified — one field on the existing `FakeTransport` (`OnGetInventory`) so a test can observe state *during* the request. |
| `Assets/Tests/EditMode/StarterGateTests.cs` | **created** — 11 tests covering SPEC §2 rules 1–5 via reflection into Assembly-CSharp (the `UsernameClaimTests` pattern). |
| `Assets/Tests/EditMode/GolfinRedux.Tests.EditMode.asmdef` | modified — one reference added: `Golfin.InventorySync`. |

No prefab, scene, CSV, localization or playlife file was touched — `git status` shows only the paths
above (see § Console output).

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| **EditMode:** `StarterGate` (a) local starter → `Ready` synchronously, no fetch awaited | PASS | `A_local_starter_resolves_Ready_synchronously_without_a_fetch` asserts the single `Ready` answer with `_server.GetCount == 0` and `_bootRequests == 0`. |
| (b) empty save + blob carrying `starter` → `Ready`, `NeedsStarter == false` | PASS | `An_empty_save_waits_and_the_restored_starter_routes_Ready`: one `Ready`, `save.starterCharacterId == "char_ken"`, exactly 1 GET. |
| (c) empty save + `Ok` with no blob → `Ready`, `NeedsStarter == true` | PASS | `An_ok_fetch_with_no_blob_resolves_Ready_with_NeedsStarter_still_true` — the one case that may show the picker. |
| (d) fetch fails → `ServerUnreachable`; retry + success → `Ready` | PASS | `A_failed_fetch_resolves_ServerUnreachable`, `An_already_failed_boot_resolves_ServerUnreachable_without_a_second_fetch`, and `Retry_after_a_failure_resolves_Ready_once_the_server_answers` (models tap → `RetryBoot()` → resolve). |
| (e) `Resolve` before `Boot` started resolves after it finishes, exactly once | PASS | `Resolve_before_any_boot_answers_after_it_finishes_exactly_once` — empty until `Boot()`, then one `Ready`; a second `Reset()+Boot()` leaves `seen.Count == 1`. `Two_pending_resolves_are_both_answered_by_one_boot` covers two waiters. |
| **EditMode:** `CharacterManager.ReloadFromSave` after a projector `Apply` (starter=X, ownedCharacters=[X]) → owned + `OnRosterChanged` once | PASS (play-mode, see deviation 1) | Live play mode against the real save: added `char_olivia` to `save.ownedCharacters`, fired `OnRestored` → `GetAllOwnedCharacters()` 1 → 2, contains `char_olivia` with `isOwned`, `OnRosterChanged` fired exactly **1**; reverted to 1. The EditMode half of the contract (`OnRestored` fires once, only when the merge changed something, and *before* `OnBootFinished`) is `InventoryBootOutcomeTests`. |
| **EditMode:** `ClubManager.RehydrateFromSave` → level matches, `clubOwnershipSeeded` untouched, no second seed | PASS (play-mode, see deviation 1) | Live: `club_driver_golfin_common` persisted lv 11 → set 14 → `OnRestored` → runtime lv **14**, `OnInventoryChanged` fired **1**, `clubOwnershipSeeded` still `True`, club count unchanged at **7** (no re-seed); reverted to 11. |
| **EditMode:** `RosterScreenController` starter mode + `OnRosterChanged` with a starter set → Home; without → stays | PASS (play-mode, see deviation 1) | Live: `ShowScreen(StartingCharacterSelection)`; with `starterCharacterId=""` a roster change left `CurrentScreen = StartingCharacterSelection`; restoring `char_james` and firing again gave `CurrentScreen = Home`. |
| **Device:** delete + reinstall on Cesar's iPhone → Home, never the picker; roster + bag match | **OPEN — Cesar** | Cannot be run from here. This is the world-check and the reason the task exists; the two log lines to look for are `[InventorySync] Restored/merged server inventory (rev N)` **before** `[ScreenManager] ApplyScreen: Home`. |
| **Device:** airplane mode before the gate resolves → `AUTH_ERR_OFFLINE`, no picker; retry → Home | **OPEN — Cesar** | The logic is covered by `A_failed_fetch_resolves_ServerUnreachable` + `Retry_after_a_failure_...`; the on-device surfacing of the message is his to see. |
| **Device:** brand-new account → picker once; pick; delete + reinstall → Home | **OPEN — Cesar** | Same run as above, second account. |
| Existing device with a local starter: no network wait on Login → Home | PASS | Live play mode on Cesar's own signed-in save: `StarterGate.Resolve` returned `[Ready]` **synchronously**, frame delta **0**, with `NeedsStarter=False`. Test (a) asserts the same with a fetch counter. |
| Bot harness + demo path byte-identical | PASS | `A_bypassed_path_resolves_Ready_immediately_with_no_fetch` — `Ready`, 0 fetches, 0 boot requests. `StarterGate.BypassProbe` short-circuits on `BotSessionOverride.Active` (inside the existing `#if UNITY_EDITOR \|\| GOLFIN_BOT_HARNESS` guard), `DemoGate.IsDemo`, and `SendsEnabled == false`, before any service state is read. Splash's bot branch still calls `RouteAuthenticated()` unchanged. |
| Full unfiltered EditMode sweep green (counts vs baseline) | PASS | **2063 total / 2060 passed / 0 failed / 3 skipped.** Baseline was 2037; the 26 added tests are 15 (`InventoryBootOutcomeTests`) + 11 (`StarterGateTests`). Because `tests-run`'s class/assembly filters error out on this setup, a **tripwire run** proved both new classes actually execute: adding one `Assert.Fail` per class gave `2065 total / 2 failed`, naming `Golfin.InventorySync.Tests.InventoryBootOutcomeTests.TRIPWIRE_…` and `GolfinRedux.Tests.EditMode.StarterGateTests.TRIPWIRE_…`; tripwires removed and the sweep re-run green. |
| Zero new hardcoded `.text` literals | PASS | `git diff -U0` over all twelve changed production files, grepped for `^\+.*\.text\s*=\s*"` → no matches. The only strings written to a label are `LocalizationManager.Get("AUTH_ERR_OFFLINE")` (verified live: `"No internet connection. Please try again."`, EN+JA already in `LocalizationText.csv:297`) and the START caption restored from its own stash. |
| Spec deviations flagged | PASS | See § Spec deviations. |

## Spec deviations

1. **The two manager-reload acceptance items were verified in PLAY MODE, not EditMode.** `CharacterManager`,
   `ClubManager` and `RosterScreenController` live in Assembly-CSharp, which an asmdef **cannot**
   reference, and all three are MonoBehaviour singletons that additionally need `CharacterDatabaseCSV` /
   `ClubDatabaseCSV` / a real `SaveDataHost` (which loads from `persistentDataPath` — i.e. Cesar's actual
   save). Standing them up in EditMode would have been both fragile and destructive. Instead the live
   play-mode run above exercises the real objects on the real save, mutating only in memory and
   reverting (no `MarkDirty`, so nothing reached disk — roster back to 1 owned, bag back to 7 clubs,
   `starterCharacterId` back to `char_james`, scene left `IsDirty=false`, play mode exited). The
   testable half — that `OnRestored` fires exactly once, only when the merge changed the save, and
   **before** `OnBootFinished` — is covered in EditMode by `InventoryBootOutcomeTests`.
2. **`CharacterManager.ReloadFromSave` does not re-raise `OnRosterChanged`.** The spec says
   "`LoadRoster()` + `OnRosterChanged?.Invoke()`", but `LoadRoster` already invokes it as its last
   statement (`CharacterManager.cs:307` pre-change); adding a second invoke would double-fire every
   roster subscriber. The spec's own instruction "do not duplicate any of it" is what I followed.
   Measured: the play-mode run counted `OnRosterChanged` firing exactly **1** time per `OnRestored`.
3. **The Login/CreateUsername retry branch is armed by a flag, not by `IsAuthenticated` alone.** The
   spec says "if `AuthService.Instance.Session.IsAuthenticated` → `RetryBoot()` then `Resolve` again"
   at the top of `OnLoginClicked`. Taken literally that also hijacks the existing
   CreateUsername → Cancel → Login path, where a signed-in player who types different credentials
   would silently never sign in. The branch is therefore `_starterRetryPending && IsAuthenticated`,
   where `_starterRetryPending` is set only by a `ServerUnreachable` answer on that screen and cleared
   on `OnEnable`/`OnDisable`/a successful route. Same guarantee the spec asked for (the retry tap never
   re-runs `SignInWithPassword`), strictly narrower blast radius.
4. **Two additions the spec did not name, both to prevent a hang or a duplicate request.**
   (a) `InventorySyncService.BootInFlight` + a `TryBoot` guard on it: `TryBoot` is now called from
   three places (bind, sign-in, the gate's nudge) and without this a nudge issued while the first GET
   was still out would fire a second, redundant GET. (b) `StarterGate` rule 4 checks
   `SendsEnabled && IsAuthenticated()` before subscribing: on a path where no boot can ever run,
   waiting would leave the caller's busy state on forever, so it resolves `Ready` (the local save is
   all there is on that path, exactly as before the gate existed). Covered by
   `An_unauthenticated_session_resolves_Ready_rather_than_hanging` and
   `Sends_disabled_resolves_Ready_rather_than_hanging`.
5. **`ItemManager` and `BallManager` DO cache**, contrary to the Diagnosis's "check whether they cache;
   if they read through on every call they need nothing" — both build a runtime dictionary once in
   `Awake` (`ItemManager.InitializeItems`, `BallManager.InitializeBalls`) and every read goes to that
   dictionary. Both got a `ReloadFromSave()`. Re-running their init is safe: neither carries one-shot
   state. **`GachaTicketManager` genuinely reads through** (`GetBalance` → `SaveDataHost.Instance.Data`
   on every call, `GachaTicketManager.cs:82-83`) and was left alone, as the spec allows.
6. **A clamp detail worth knowing for the device pass.** In the play-mode run a restored character
   written at level 42 came back as level **39** — the rarity max for Common
   (`CharacterManager.LoadRoster`'s existing clamp). That is pre-existing behaviour, not introduced
   here, but it means "the roster matches what it was before the delete" is only true up to the
   catalog's own clamps.

## Console output

`git status --porcelain --untracked-files=all` after the work (nothing outside this task, and nothing
outside `Assets/Scripts` + `Assets/Tests` + this spec folder):

```
 M Assets/Scripts/BallManager.cs
 M Assets/Scripts/CharacterManager.cs
 M Assets/Scripts/ClubManager.cs
 M Assets/Scripts/InventoryCatalogAdapter.cs
 M Assets/Scripts/InventorySync/InventorySyncBehaviour.cs
 M Assets/Scripts/InventorySync/InventorySyncService.cs
 M Assets/Scripts/InventorySync/Tests/InventorySyncServiceTests.cs
 M Assets/Scripts/ItemManager.cs
 M Assets/Scripts/UI/Account/CreateUsernameScreenController.cs
 M Assets/Scripts/UI/Account/LoginScreenController.cs
 M Assets/Scripts/UI/Roster/UI/RosterScreenController.cs
 M Assets/Scripts/UI/SplashScreenController.cs
 M Assets/Tests/EditMode/GolfinRedux.Tests.EditMode.asmdef
?? Assets/Scripts/InventorySync/Tests/InventoryBootOutcomeTests.cs
?? Assets/Scripts/InventorySync/Tests/InventoryBootOutcomeTests.cs.meta
?? Assets/Scripts/UI/Account/StarterGate.cs
?? Assets/Scripts/UI/Account/StarterGate.cs.meta
?? Assets/Tests/EditMode/StarterGateTests.cs
?? Assets/Tests/EditMode/StarterGateTests.cs.meta
```

Play-mode verification, verbatim (three `script-execute` runs against the live signed-in save):

```
OnRestored subscribers = 1
   -> GolfinRedux.InventorySync.InventoryCatalogAdapter.OnInventoryRestored
catalogAdapterGO = True
syncBehaviourGO  = True
ownedCharacters(before) = 1
clubs(before) = 7
starterCharacterId = 'char_james'  NeedsStarter=False
candidate(unowned) = 'char_olivia'
AFTER OnRestored: ownedCharacters = 2  contains candidate = True  level = 39  OnRosterChanged fired = 1
REVERTED: ownedCharacters = 1 (baseline 1)

club 'club_driver_golfin_common' persisted lv=11  runtime lv=11
AFTER OnRestored: runtime lv=14  expected 14  OnInventoryChanged fired=1  clubOwnershipSeeded=True  clubCount=7
REVERTED: runtime lv=11
screenManager=True  current=Home
after ShowScreen(StartingCharacterSelection, instant): current=StartingCharacterSelection

start: current=StartingCharacterSelection NeedsStarter=False
no-starter + OnRosterChanged: current=StartingCharacterSelection (expect StartingCharacterSelection)
starter-arrived + OnRosterChanged: current=StartingCharacterSelection (Home may still be mid-fade)
starterCharacterId restored to 'char_james'
   → next frame: current=Home  NeedsStarter=False  starter='char_james'  ownedChars=1  clubs=7

LIVE service: LastBootOutcome=Succeeded BootCompleted=True BootInFlight=False SendsEnabled=True Rev=196
CharacterManager.NeedsStarter=False
StarterGate.Resolve -> [Ready]  answeredSynchronously=True  frame delta=0
Splash StartButton=True  TMP_Text label=True  hasLocalizedText=True
AUTH_ERR_OFFLINE = 'No internet connection. Please try again.'
after RetryBoot(): LastBootOutcome=Succeeded BootInFlight=False   (a succeeded boot is not re-fetched)
```

No errors or new warnings attributable to this task appeared in the Editor console during the run.

## Open questions for Architect

None. The only outstanding work is Cesar's delete + reinstall device pass, which is the acceptance
criterion the spec itself names as the world-check.
