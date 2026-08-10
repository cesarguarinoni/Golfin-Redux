# SPEC — `auto_club_selection`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state (starts at `SPEC_READY`).

## Goal

The game auto-selects the best-suited club for the player before every shot, the way the multiplayer bot already does for itself (`VersusBot.SelectShotCalibrated` distance-band logic). Rules, confirmed by Cesar 2026-08-10:

1. **Tee shot → always Driver.** (Ball on the tee = stroke 1 or a re-tee after OB; detection reuses the existing `BallIsOnTee()` convention.)
2. **After the tee shot, auto-select never picks the Driver again.** The player MAY still pick it manually — the K11 selector gate is NOT extended to the driver.
3. **Green → Putter is already mandatory** via §2f (`PutterModeSurfaceController.DecideTargetClub`) + the K11 selector gate. This task does not touch that rule; auto-select simply never runs while §2f has the player in putter mode.
4. **Auto-select re-runs on every shot.** A manual selector pick applies only to the shot it was made for; once that shot resolves, the next lie gets a fresh auto-pick.

Player-facing only. `VersusBot` / `BotDriver` keep their own selection code untouched.

## Reference

- No Figma — no new UI. The existing `ClubButtonWidget` / selector overlay simply reflect the auto-picked club through the existing `ClubContext.OnSelectedChanged` events.
- Behavioural reference: `VersusBot.SelectShotCalibrated` (distance bands) — but the PLAYER version selects from the **equipped bag** (`ClubContext.EquippedBag`, per-club `baseDistance` from Clubs.csv), not from `bot_clubs.csv`.

## Architecture context

- **Asmdef boundaries affected:** `Golfin.Physics.Viewer` (PhysicsLabController + new pure selector class) and `Golfin.Gameplay.UI` (ClubContext / ClubEntry). Viewer already references Gameplay.UI (see `BotClubSync.cs` header note) — no new asmdef edges.
- **Existing code referenced:**
  - `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `HandleShotComplete` AtRest branch (~L1483, the §2f `DecideTargetClub` call), `ReDecideClubAfterReposition` (~L882), `SetupAtTee` (~L754), `BallIsOnTee()` (~L1176), `SetClub` (~L698), `CurrentClubIndex`, `PutterIndex`, `_lastNonPutterClubIndex`.
  - `Assets/Scripts/Physics/Viewer/PutterModeSurfaceController.cs` — `DecideTargetClub` (green rule; UNCHANGED).
  - `Assets/Scripts/Gameplay/UI/ShotUI/HUD/ClubContext.cs` — `EquippedBag`, `SelectedIndex`, `RequestSelection(int)`, `ClubEntry`.
  - `Assets/Scripts/Gameplay/UI/ShotUI/ClubSelectionBroadcast.cs` — `Raise(int)`, `InPutterMode`, `IsSelectable` (UNCHANGED — driver is NOT gated).
  - `Assets/Scripts/UI/HUD/ClubContextPopulator.cs` and `Assets/Scripts/UI/HUD/LabInventoryStub.cs` — the two `ClubEntry` builders + `MapClubTypeToLabIndex`.
  - `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs` — commit pattern to copy: `ClubContext.RequestSelection(bagIdx); ClubSelectionBroadcast.Raise(entry.LabClubIndex);` (this is the pair that keeps BOTH the live-stat path (`ClubContext.SelectedClubId`) and the lab club index in sync — see the Order 762 lesson in `BotClubSync.cs`).
  - `Golfin.Gameplay.UI.HUD.HoleContext.PinWorld` — pin position for distance.
- **Manager APIs used:** none new. No BagManager/ClubManager changes.

## Design

### 1. `ClubEntry.Type` (small data addition)

`ClubEntry` today carries only `TypeLabel` (display string) and `LabClubIndex` (0=driver/wood, 1=iron, 2=wedge, 3=putter). Driver and Wood share lab index 0, so lab index alone cannot express "never auto-pick the DRIVER but a Wood is fine".

Add to `ClubEntry` (ClubContext.cs):

```csharp
public ClubType Type = ClubType.Driver;   // NOTE: ClubType lives in Golfin.Inventory (Assembly-CSharp).
```

**NOTE (asmdef check):** `ClubContext.cs` is in `Golfin.Gameplay.UI`, which may NOT reference Assembly-CSharp where `ClubType` lives. If the reference is illegal, fall back to `public bool IsDriver = false;` set by the populators (`ClubContextPopulator.Refresh`, `LabInventoryStub.SeedClubs`) from `rt.type == ClubType.Driver`. Do not guess — check the asmdef and pick the legal variant; the pure selector below is written against `IsDriver`-style predicates either way.

Populate the new field in BOTH builders: `ClubContextPopulator.Refresh()` and `LabInventoryStub.SeedClubs()`.

### 2. New pure class: `AutoClubSelector` (mirrors `PutterModeSurfaceController` style)

New file `Assets/Scripts/Physics/Viewer/AutoClubSelector.cs`, namespace `Golfin.Physics.Viewer`, pure static — unit-testable with no scene:

```csharp
public static class AutoClubSelector
{
    public const float YardsPerMeter = 1.09361f;   // same constant used inline in ViewerTests / PhysicsLabUI

    /// <summary>
    /// Picks the equipped-bag index the game should pre-select for the next shot.
    /// Returns -1 for "leave selection alone".
    ///
    /// Inputs are primitives/POCOs so the rule is testable without a scene:
    ///   distToPinM      — flat XZ distance ball→pin, meters.
    ///   isTeeShot       — BallIsOnTee() at decision time.
    ///   inPutterMode    — §2f putter mode (ball at rest on Green). Auto-select must NOT fight §2f.
    ///   bag             — ClubContext.EquippedBag snapshot.
    /// </summary>
    public static int SelectBestClub(float distToPinM, bool isTeeShot, bool inPutterMode,
                                     IReadOnlyList<ClubEntry> bag, int putterLabClubIndex)
}
```

Rule, in order:

1. `bag` null/empty → `-1`.
2. `inPutterMode` → `-1` (green: §2f already forced the putter; never fight it).
3. `isTeeShot` → index of the first entry whose type is Driver (`IsDriver` / `Type == ClubType.Driver`). No driver in the bag → fall through to rule 4 (treat as normal full shot, drivers excluded by definition since none exist).
4. Otherwise build the candidate set: every entry that is NOT a driver and NOT the putter (`entry.LabClubIndex != putterLabClubIndex`). Empty candidate set → `-1`.
5. Convert: `distYd = distToPinM * YardsPerMeter`. Pick the candidate with the **smallest `Distance` (baseDistance yards) that is ≥ `distYd`** (the shortest club that still reaches — same "longest realistic club per range" intent as the bot's bands, expressed against real bag data).
6. If NO candidate reaches (`distYd` > every candidate's `Distance`) → pick the candidate with the **largest `Distance`** (longest non-driver club; matches the bot clamping to max carry).
7. Tie on `Distance` → lowest bag index wins (deterministic).

**NOTE:** `ClubEntry.Distance` is Clubs.csv `baseDistance` in yards — the same number the HUD shows (iter-37) and `power_gauge_target_marker` treats as the per-club carry authority. If on-course carries diverge from baseDistance, that is P-006 territory — report evidence, don't compensate here.

### 3. Integration in `PhysicsLabController`

New serialized toggle + helper:

```csharp
[Header("Auto club selection (auto_club_selection)")]
[Tooltip("Auto-pick the best club for each shot (driver on tee, distance-based after). Player can still override per shot.")]
[SerializeField] bool _autoClubSelectEnabled = true;

void AutoSelectClubForNextShot()
{
    if (!_autoClubSelectEnabled) return;
    var bag = Golfin.Gameplay.UI.HUD.ClubContext.EquippedBag;
    Vector3 ball = BallPosition;                      // existing accessor
    Vector3 pin  = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
    float distM  = new Vector2(pin.x - ball.x, pin.z - ball.z).magnitude;

    int bagIdx = AutoClubSelector.SelectBestClub(
        distM,
        BallIsOnTee(),
        Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.InPutterMode,
        bag, PutterIndex);

    if (bagIdx < 0 || bagIdx >= bag.Count) return;
    if (bagIdx == Golfin.Gameplay.UI.HUD.ClubContext.SelectedIndex
        && bag[bagIdx].LabClubIndex == CurrentClubIndex) return;   // idempotent

    Debug.Log($"[PhysicsLab][auto_club] dist={distM:F1}m tee={BallIsOnTee()} → bag[{bagIdx}] '{bag[bagIdx].ClubId}' (labIdx={bag[bagIdx].LabClubIndex})");

    // Commit through the SAME pair the selector overlay uses (SelectorOverlayWidget card commit):
    // RequestSelection keeps ClubContext/live-stat path correct (Order 762 lesson),
    // Raise reaches OnClubBroadcastReceived → SetClub for the lab index + putter-mode UI.
    Golfin.Gameplay.UI.HUD.ClubContext.RequestSelection(bagIdx);
    Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.Raise(bag[bagIdx].LabClubIndex);
    _shotController?.ClearStatBundleOverride();       // PROD path: live stats, mirrors §2f branch
}
```

Call sites (three, all AFTER the existing §2f decision so the green rule always wins):

1. **`HandleShotComplete` — AtRest branch (~L1483):** immediately after the existing `DecideTargetClub` block (after its `SetClub(target)` / `ClearStatBundleOverride()`), add `AutoSelectClubForNextShot();`. Because the helper no-ops in putter mode, landing on the green stays pure §2f.
2. **`ReDecideClubAfterReposition` (~L882):** at the end (after the existing decide/SetClub), add the same call — OB/water drops and `PlaceBallAt` teleports get a fresh pick for the new lie (including back-to-tee drops → Driver again via `BallIsOnTee()`).
3. **Hole start:** at the end of `SetupAtTee()` guarded by `IsHoleReady` — or equivalently at the end of `OnHoleLoaded` after the tee is placed — so stroke 1 explicitly selects the bag's Driver rather than trusting "index 0 happens to be a driver". NOTE: `ClubContextPopulator`/`LabInventoryStub` may populate `EquippedBag` a frame later than `SetupAtTee` runs; if the bag is empty at that moment, subscribe once to `ClubContext.OnBagChanged` and re-run the tee auto-pick, then unsubscribe. Flag actual ordering found in the report.

**Timing note:** the §2f block calls `SetClub` directly WITHOUT updating `ClubContext` (known gap — see `LabInventoryStub` Start() comment: only the lab stub bridges broadcast→ClubContext). This task does not fix the putter-mode gap (K11 gates the selector on green anyway), but the auto-select commit pair above must not reintroduce the same gap for full shots — hence RequestSelection + Raise, never bare `SetClub`.

### 4. What this task does NOT change

- `PutterModeSurfaceController` / K11 gate (`ClubSelectionBroadcast.IsSelectable`) — untouched. Driver stays manually selectable off-tee.
- `VersusBot`, `BotDriver`, `BotClubSync`, `bot_clubs.csv` — untouched.
- Flick/F13 power, `ShotController`, stat resolution — untouched. Only which club is pre-selected changes.
- No scene edits (new field is code-serialized with a default; no Inspector wiring required).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] EditMode tests for `AutoClubSelector.SelectBestClub` (new file `Assets/Scripts/Physics/Tests/AutoClubSelectorTests.cs`, mirror `PutterModeSurfaceControllerTests` style) covering AT MINIMUM: tee→driver; tee with no driver in bag→distance rule; off-tee never returns a driver entry even when distYd exceeds every club (longest NON-driver wins); shortest-club-that-reaches picked (e.g. 120yd → Iron not Wood); overshoot-all → longest non-driver; putter never returned off-green; `inPutterMode`→-1; empty bag→-1; meters→yards conversion (e.g. 100m = 109.4yd picks the 110yd club, not the 100yd club).
- [ ] Existing test suites still green: `PutterModeSurfaceControllerTests`, `RepositionClubReDecideTests`, `ClubSelectionGreenGateTests`, `NextShotHandoffTests`.
- [ ] Editor manual: Hole 1 full hole — tee shows DRIVER; after tee shot the HUD club button shows the distance-appropriate club (log line `[PhysicsLab][auto_club]` cites dist + pick); on green §2f putter fires exactly as before; leaving the green resumes auto-pick.
- [ ] Editor manual: manually pick a different club via the selector, fire — next shot's auto-pick overrides the manual choice (re-run-every-shot rule).
- [ ] Editor manual: OB drop → `[PhysicsLab][auto_club]` fires for the drop lie; drop back at tee re-selects Driver.
- [ ] Driver remains selectable in the selector overlay off-tee (no new gating).
- [ ] `ClubContext.SelectedClubId` matches the auto-picked club after each auto-pick (checked via log or debugger) — the live-stat path fires the picked club, not the previous one.
- [ ] `_autoClubSelectEnabled = false` restores today's behaviour byte-for-byte (no auto-pick log lines, §2f unaffected).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/Physics/Viewer/AutoClubSelector.cs` — NEW pure selector.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — toggle field + `AutoSelectClubForNextShot()` + 3 call sites.
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/ClubContext.cs` — `ClubEntry.Type` (or `IsDriver`) field.
- `Assets/Scripts/UI/HUD/ClubContextPopulator.cs` — populate the new field.
- `Assets/Scripts/UI/HUD/LabInventoryStub.cs` — populate the new field.
- `Assets/Scripts/Physics/Tests/AutoClubSelectorTests.cs` — NEW tests.

## Smoke evidence

EditMode test run (all suites above) + the editor manual matrix from the checklist, with the `[PhysicsLab][auto_club]` log lines quoted in `IMPLEMENTER_REPORT.md` for one full Hole 1 play-through (tee → fairway → green → holed). Visual fidelity per Lesson O: the HUD club button content is the player-perceived surface — describe what it showed at each lie.

## Out of scope (do NOT do these)

- Gating the Driver in the selector (K11 extension) — Cesar chose manual-allowed.
- Any bot selection change (`VersusBot`, `BotDriver`, `BotClubSync`).
- Fixing the §2f putter-mode `ClubContext` gap (report it if it bites, don't fix it here).
- Wind/lie/elevation-aware club choice, layup logic, hazard probes — distance-only for v1 (the bot's H2/H3 machinery stays bot-only).
- Power/flick, stat pipeline, Clubs.csv values, UI art, scene edits.
- P-006 (baseDistance vs real carry mismatch) — collect evidence only.
