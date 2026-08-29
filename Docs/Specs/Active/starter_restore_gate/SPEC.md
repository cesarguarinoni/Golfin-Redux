# SPEC — `starter_restore_gate`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
> Filed 2026-08-29 (Architect via Cowork). Cesar's ask, verbatim: *"Starting character should
> also be saved to the backend. I should not have to pick one when updating the game from
> TestFlight even if I deleted the game."*

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

A signed-in player whose account already has a starter character on the server must **never** see
the Starting Character Selection screen again — not after a TestFlight update, not after
delete + reinstall, not on a second device. The picker appears only when the SERVER has answered
and the answer contains no starter.

## Diagnosis (read before touching anything)

The starter is **already** saved to the backend. `content_player_inventory` (DONE 2026-08-26)
writes `starterCharacterId` into the `profiles.golfin_inventory` blob under the `starter` key
(`InventoryCodec.KStarter`), and `InventoryProjector.Apply` fills `save.starterCharacterId` from it
on restore. The server side needs **no change** in this task. Its STATUS.md lists
"the device pass (restore-after-reinstall)" as STILL OPEN — this task is what that pass would
have found.

Two client gaps, both reproducible from the code:

1. **Routing races the restore.** `InventorySyncService.Boot()` is asynchronous
   (`Transport.GetInventory(cb)`), started from `InventorySyncBehaviour.TryBoot()` on
   `AuthService.SignedIn` / `BindWhenReady`. Nothing waits for it. All three post-auth routers read
   `CharacterManager.NeedsStarter` — which is just
   `string.IsNullOrEmpty(SaveDataHost.Instance.Data.starterCharacterId)` — **synchronously**, in
   the same callback as the sign-in success:
   - `LoginScreenController.OnLoginClicked` (`Assets/Scripts/UI/Account/LoginScreenController.cs:137`
     and the OAuth twin at `:185`)
   - `CreateUsernameScreenController` (`:85`)
   - `SplashScreenController.RouteAuthenticated` (`:89`)

   On a fresh install the local save is empty, so `NeedsStarter` is true before the fetch has
   answered, and the picker wins every time. The restore lands a moment later, underneath the
   picker, and nothing re-routes.

2. **Managers do not re-hydrate.** `InventorySyncService.RestoreFrom` mutates the `SaveData`
   object and calls `MarkSaveDirty()`, but `CharacterManager` builds `ownedCharacters` once in
   `Awake → LoadRoster()` (private) and `ClubManager` builds `ownedClubs` once in
   `Awake → InitializeClubs → HydrateFrom(save)` (private). After a restore the in-memory roster
   still says nothing is owned until the next launch. Even with gap 1 fixed, a restored player
   would land on Home with a roster that disagrees with the save.

`ItemManager` / `BallManager` read `SaveDataHost.Instance.Data.itemQuantities` /
`ballQuantities` directly (`ItemManager.cs:71`, `BallManager.cs:75`) — check whether they cache;
if they read through on every call they need nothing.

## Decisions of record (Cesar, 2026-08-29)

- **D1 — fetch failure never shows the picker.** If the boot restore FAILS (`InventoryFetch.Ok ==
  false`) and the local save has no starter, the player is held on the screen they came from with
  the existing offline error text and a retry; the picker is shown only after a SUCCESSFUL fetch
  whose blob carries no `starter` (a new account: `fetch.Ok && string.IsNullOrEmpty(fetch.Json)`,
  or a blob with an empty `starter`).
- Local save already has a starter → route immediately, no waiting. The gate costs nothing on the
  common path (a device that already played).

## Architecture context

- **Asmdef boundaries:** `Golfin.InventorySync` (service + behaviour, no Unity UI refs),
  Assembly-CSharp (`CharacterManager`, `ClubManager`, `SplashScreenController`,
  `LoginScreenController`, `CreateUsernameScreenController`, `RosterScreenController`).
- **Existing code referenced:**
  - `Assets/Scripts/InventorySync/InventorySyncService.cs` — `Boot(Action? done)`, `BootCompleted`,
    `RestoreFrom`, `Reset()`.
  - `Assets/Scripts/InventorySync/InventorySyncBehaviour.cs` — `TryBoot()`, `OnSignedIn`,
    `BindWhenReady()`.
  - `Assets/Scripts/InventorySync/InventoryProjector.cs:218-230` — fill-if-empty starter/selection
    (unchanged).
  - `Assets/Scripts/CharacterManager.cs` — `NeedsStarter` (`:388`), `LoadRoster()` (`:57`, private),
    `OnRosterChanged`.
  - `Assets/Scripts/ClubManager.cs` — `HydrateFrom(SaveData)` (`:397`, private), `OnInventoryChanged`.
  - `Assets/Scripts/UI/SplashScreenController.cs` — `OnStartClicked` (`_busy`, `RefreshSession` →
    `RouteAuthenticated`).
  - `Assets/Scripts/UI/Account/LoginScreenController.cs` — `SetBusy`, `SetError`,
    `AUTH_ERR_OFFLINE` already in `LocalizationText.csv`.
  - `Assets/Scripts/UI/Roster/UI/RosterScreenController.cs` — `SetStarterMode`.

## Implementation

### 1. `InventorySyncService` — expose the boot outcome

Add to the service (pure C#, testable with the existing fake transport):

```csharp
public enum BootOutcome { NotRun, Succeeded, Failed }
public BootOutcome LastBootOutcome { get; private set; } = BootOutcome.NotRun;
/// Raised on the main thread after Boot() finishes (success OR failure), after grants drained.
public event Action<BootOutcome>? OnBootFinished;
```

Set `LastBootOutcome` inside the `GetInventory` callback (`Succeeded` when `fetch.Ok`, else
`Failed`), keep `BootCompleted = true` on both (unchanged — pushes must still flow after a failed
fetch, that is a documented invariant), then invoke `OnBootFinished` from the `done` of
`DrainGrants`. `Reset()` sets `LastBootOutcome = NotRun`.

Add a **re-run** path: `Boot()` currently returns early only on auth; `InventorySyncBehaviour.TryBoot`
guards with `_booted && service.BootCompleted`. Add `public void RetryBoot()` on the behaviour
(or make `TryBoot` public) that clears `_booted` and calls `Boot()` again when
`LastBootOutcome == Failed`. Callback threading: `Transport.GetInventory` already completes on
the main thread via `ApiClient` (the existing `RestoreFrom` touches `SaveDataHost` from it) —
confirm and leave a one-line comment, do not add a dispatcher.

### 2. `StarterGate` — one place that answers "picker or not"

New static helper, Assembly-CSharp, `Assets/Scripts/UI/Account/StarterGate.cs`:

```csharp
public enum StarterRoute { Ready /* NeedsStarter is trustworthy */, WaitingForServer, ServerUnreachable }

public static class StarterGate
{
    /// Resolves once: immediately when the local save already has a starter or the boot
    /// already finished; otherwise after the next OnBootFinished. Never resolves twice.
    public static void Resolve(Action<StarterRoute> done);
}
```

Rules, in order:
1. `!CharacterManager.Instance.NeedsStarter` → `Ready` (local starter exists; no wait).
2. `InventorySyncService.Instance.LastBootOutcome == Succeeded` → `Ready`
   (`NeedsStarter` is now the server's answer).
3. `== Failed` → `ServerUnreachable`.
4. `== NotRun` → subscribe `OnBootFinished` once; on `Succeeded` → `Ready`, on `Failed` →
   `ServerUnreachable`. Also call `InventorySyncBehaviour`'s `TryBoot` in case the sign-in event
   fired before the behaviour bound (the `BindWhenReady` window).
5. Editor/bot paths that never sign in (`BotSessionOverride.Active`, `DemoGate.IsDemo`,
   `SendsEnabled == false`) → `Ready` immediately, so the harness and demo are byte-identical.

A safety timeout is **not** part of D1 — the request either answers or the transport's own
timeout fails it; do not add a second clock that could resolve `Ready` on an empty save.

### 3. Callers — replace the three synchronous checks

Each of the three sites becomes: `SetBusy(true)` (Login/CreateUsername) or `_busy = true`
(Splash) → `StarterGate.Resolve(route => …)`:

- `Ready` → existing branch unchanged (`NeedsStarter ? StartingCharacterSelection : Home/CreateUsername`).
- `ServerUnreachable` →
  - Login: `SetBusy(false); SetError(LocalizationManager.Get("AUTH_ERR_OFFLINE"))`. The player is
    still signed in; tapping LOGIN again must NOT re-run `SignInWithPassword` — branch at the top
    of `OnLoginClicked`: if `AuthService.Instance.Session.IsAuthenticated` → `RetryBoot()` then
    `Resolve` again.
  - CreateUsername: same shape with its own `SetError`; the username is already claimed at that
    point, so retry only re-runs the gate.
  - Splash: `_busy = false`; show `AUTH_ERR_OFFLINE` in the START button's existing `TMP_Text`
    child (`SplashScreenController.cs:167` already resolves it) for the tap, restore the label on
    the next tap; START re-runs `RouteAuthenticated`, which re-runs the gate with `RetryBoot()`.
    **No new prefab, no new string** — `AUTH_ERR_OFFLINE` exists in EN + JA.
- `WaitingForServer` is internal to the gate; callers never see it (busy state covers the wait).

Order of operations matters in `RouteAuthenticated`: `HasDisplayName` check stays FIRST (a brand
new account goes to CreateUsername without waiting — its gate runs there).

### 4. Re-hydrate managers after a restore

- `CharacterManager`: add `public void ReloadFromSave()` = `LoadRoster()` + `OnRosterChanged?.Invoke()`.
  `LoadRoster` already re-applies the save overlay, the F8 starter invariant and the selection
  fallback; do not duplicate any of it.
- `ClubManager`: add `public void RehydrateFromSave()` = `HydrateFrom(SaveDataHost.Instance.Data)`
  + `OnInventoryChanged?.Invoke()`. Do **not** re-run `InitializeClubs` — it contains the
  one-shot seeding (`clubOwnershipSeeded`) and the wedge backfill; both must stay one-shot.
- `ItemManager` / `BallManager` / `GachaTicketManager`: only if they cache (see Diagnosis);
  otherwise a NOTE in the report saying they read through.
- Wire: `InventorySyncService.RestoreFrom` returns whether `ApplyAndCount` changed anything;
  when it did, the **behaviour** (Assembly-CSharp side may not be referenced from
  `Golfin.InventorySync` — check the asmdef) raises a new `InventorySyncService.OnRestored`
  event and `InventoryCatalogAdapter` (already Assembly-CSharp, already holds both managers)
  subscribes and calls the two reload methods. Same for the `stale-merge` path in `TryPush` —
  a merge that raised a level on another device should show here too.
- The restore path already `MarkSaveDirty()`s; reload must not save again.

### 5. Starter screen: leave if the answer arrives late

Belt and braces for the one path the gate cannot own (a tester who reaches the picker via a
route that bypasses the three callers, e.g. Reset Starter Choice in `RosterDebugTools`):
`RosterScreenController.SetStarterMode(true)` subscribes `CharacterManager.OnRosterChanged`; if
`!NeedsStarter` fires while in starter mode → `ScreenManager.ShowScreen(Home)` and unsubscribe.
Unsubscribe in `OnDisable` (project convention).

### 6. Strings

None new. `AUTH_ERR_OFFLINE` (EN + JA) is reused. Zero new hardcoded `.text` literals — quote the
grep in the report.

## Out of scope

- Any playlife change. The blob, the endpoint and the `starter` key are correct as shipped.
- Making the blob authoritative / anti-cheat (`content_player_inventory` §6).
- Changing the fill-if-empty starter rule in `InventoryProjector` (a second device that picked a
  different starter keeps its own; the additive merge owns both characters).
- Grants mid-session, the refund-window instrument, REVOKE (still open on `content_player_inventory`).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item PASS/FAIL with a one-sentence justification citing what was measured.

- [ ] **EditMode:** `StarterGate` with a fake transport — (a) local starter set → `Ready` synchronously,
      no fetch awaited; (b) empty save + fetch returns a blob with `starter` → `Ready` and
      `NeedsStarter == false`; (c) empty save + fetch `Ok` with no blob → `Ready` and
      `NeedsStarter == true`; (d) fetch fails → `ServerUnreachable`, then `RetryBoot` + success →
      `Ready`; (e) `Resolve` called before `Boot` started resolves after it finishes, exactly once.
- [ ] **EditMode:** `CharacterManager.ReloadFromSave` after a projector `Apply` of a blob with
      `starter=X` + `ownedCharacters=[X]` → `GetOwnedCharacters()` contains X with `isOwned`,
      `OnRosterChanged` fired once; `ClubManager.RehydrateFromSave` after a blob with a levelled club
      → `GetClub(id).currentLevel` matches, `clubOwnershipSeeded` untouched, no second seed.
- [ ] **EditMode:** `RosterScreenController` in starter mode + `OnRosterChanged` with a starter now
      set → navigates Home; without → stays.
- [ ] **Device (the world-check — this is the bug):** on Cesar's iPhone, delete the app, install from
      TestFlight, sign in with an account that has a starter on the server → lands on Home, NEVER
      on the picker; Roster shows the same owned characters + levels as before the delete; Bag
      shows the same clubs. Log line quoted: `Restored/merged server inventory (rev N)` BEFORE the
      `ShowScreen(Home)` line.
- [ ] **Device:** airplane mode ON after sign-in succeeds but before the gate resolves (or
      `IInventoryTransport` forced to fail in a dev build) → `AUTH_ERR_OFFLINE` shown, picker NOT
      shown; airplane OFF + retry → Home.
- [ ] **Device:** brand-new account → picker shown exactly once; pick; delete + reinstall → Home.
- [ ] Existing device with a starter in the local save: no network wait on the Login → Home path
      (log shows `Ready` before the fetch callback, or no fetch awaited).
- [ ] Bot harness + demo path byte-identical (`BotSessionOverride`/`DemoGate` → `Ready` immediately).
- [ ] Full unfiltered EditMode sweep green (quote counts vs baseline); zero new `.text` literals (grep quoted).
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/InventorySync/InventorySyncService.cs` — `BootOutcome`, `LastBootOutcome`,
  `OnBootFinished`, `OnRestored`; `RestoreFrom` reports changed.
- `Assets/Scripts/InventorySync/InventorySyncBehaviour.cs` — `RetryBoot()`.
- `Assets/Scripts/UI/Account/StarterGate.cs` — new.
- `Assets/Scripts/UI/Account/LoginScreenController.cs`, `CreateUsernameScreenController.cs`,
  `Assets/Scripts/UI/SplashScreenController.cs` — the three call sites.
- `Assets/Scripts/CharacterManager.cs` — `ReloadFromSave()`.
- `Assets/Scripts/ClubManager.cs` — `RehydrateFromSave()`.
- `Assets/Scripts/InventoryCatalogAdapter.cs` — subscribes `OnRestored`, calls both reloads.
- `Assets/Scripts/UI/Roster/UI/RosterScreenController.cs` — late-answer exit.
- Tests beside each (`InventorySync/Tests`, existing manager test assemblies).
- No prefab or scene edits. No playlife edits. No CSV edits.

## Smoke evidence

The device delete + reinstall run above, with the two log lines in order, is the evidence. The
`content_player_inventory` STATUS "restore-after-reinstall device pass" item closes with it —
note that in `Docs/AI_CONTEXT.md`.
