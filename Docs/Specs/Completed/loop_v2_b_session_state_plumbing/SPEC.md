# Stage B — Session State Plumbing

**Parent scope:** `Docs/Specs/Active/loop_v2_scope/SPEC.md`
**Audit refs:** `Docs/Architecture/CODE_AUDIT_2026-05-19.md` § P0-4 (GameSession missing fields) + § P1-3 (GameSession in wrong namespace)
**Task type:** TELLCODE (multi-file, established patterns, no asmdef changes)
**Notion:** GOLFIN_Roadmap — new entry (Stage B sub-item of Loop v2)
**Status:** SPEC_READY

---

## Goal

One read-back surface for "what character / bag / hole am I on right now." No subscriber re-resolves singletons. Cross-scene `OnHoleComplete` signal that Stage C's ShellScene Result modal will subscribe to.

After this stage:
- `GameSession` lives in `Golfin.Gameplay.Session` namespace under `Golfin.Gameplay.Loop` asmdef.
- Three new session fields: `CurrentHoleNumber`, `SelectedCharacterId`, `EquippedBagSlot`.
- New `OnHoleComplete` event on `GameSession`, fired by `HoleCompleteDriver` when ball state reaches `InCup`.
- `MatchmakingModalController` seeds the session at "OPPONENT FOUND".
- New `ISessionStore` interface for read-only access (foundation for headless / replay later).
- ~30 subscriber files have their `using` directive updated.

---

## Important refinement (vs. scoping SPEC)

The scoping SPEC said "`ResetForNewHole` clears the three new fields." On reflection, this creates a silly PLAY NEXT flow (re-save then re-seed `SelectedCharacterId`/`EquippedBagSlot`). **Refined design:**

- `ResetForNewHole()` keeps existing semantics from Loop v1 §2c: **per-hole** reset only (`TurnCount=1`, `ShotHistory.Clear()`). Does NOT touch seed fields.
- New `SetCurrentHole(int)` method: re-assigns `CurrentHoleNumber` AND calls `ResetForNewHole()`. PLAY NEXT calls this.
- New `ResetSession()` method: full clear (seed fields + per-hole state). MENU/back-to-Home calls this in Stage D.

Three buckets: session seed (CharacterId/BagSlot) — survives between holes in same session; hole pointer (CurrentHoleNumber) — reassigned per hole; per-hole runtime (Turn/History) — reset per hole.

---

## Pre-flight (implementer logs in IMPLEMENTER_REPORT.md)

1. **Verify blast radius.** Architect counted **30 files** with `using Golfin.Gameplay.UI.HUD`. Confirm before move:
   ```
   grep -rln 'using Golfin.Gameplay.UI.HUD' Assets/Scripts/ | wc -l
   ```
   Plus consumers using fully-qualified names:
   ```
   grep -rln 'Golfin.Gameplay.UI.HUD.GameSession' Assets/Scripts/
   ```

2. **Verify asmdef dependencies.** `Golfin.Gameplay.UI.asmdef` and `Golfin.Physics.Viewer.asmdef` both reference `Golfin.Gameplay.UI` for GameSession today. After move, they need to reference `Golfin.Gameplay.Loop` instead (where Session lives). Check what each asmdef currently references:
   ```
   cat Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef
   cat Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef
   ```
   `Golfin.Physics.Viewer` already references `Golfin.Gameplay.Loop` (`HoleCompleteDriver.cs` already uses `using Golfin.Gameplay.Loop;`) — should not need new asmdef refs. `Golfin.Gameplay.UI` likely already references `Golfin.Gameplay.Loop` too (it consumes BallState etc.). Verify both, log findings.

3. **Verify `ShotRecord` struct ownership.** `ShotRecord` is defined inside `GameSession.cs` today. Moving GameSession moves `ShotRecord` with it. Confirm no asmdef-external code defines or extends `ShotRecord` (only struct literals / readers expected).

---

## Scope

### Files MOVED

- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` → `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs.meta` → `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs.meta` (**preserve the GUID inside the .meta file**, scenes/prefabs do not reference GameSession directly but tests + asmdef compile do depend on type identity; the .meta GUID matters less for static classes, but git-rename via `git mv` is the cleanest path)

### Files CREATED

**`Assets/Scripts/Gameplay/Loop/Session/HoleCompletionData.cs`** — event payload struct:

```csharp
using System;
using Golfin.Gameplay.Loop;

namespace Golfin.Gameplay.Session
{
    /// <summary>
    /// Payload for GameSession.OnHoleComplete. Lightweight — only session-level
    /// data. UI consumers (Stage C Result Modal) assemble the richer
    /// HoleCompleteData (UI payload) by combining this + HoleContext + HoleData CSV.
    /// </summary>
    public readonly struct HoleCompletionData
    {
        public readonly BallState TerminalState;   // InCup (SUCCESS) or AtRest-stroke-cap (FAILED)
        public readonly int       Strokes;          // total strokes inc. penalties
        public readonly int       PenaltyStrokes;
        public readonly int       HoleNumber;
        public readonly DateTime  CompletedAtUtc;

        public HoleCompletionData(BallState terminalState, int strokes, int penaltyStrokes, int holeNumber)
        {
            TerminalState  = terminalState;
            Strokes        = strokes;
            PenaltyStrokes = penaltyStrokes;
            HoleNumber     = holeNumber;
            CompletedAtUtc = DateTime.UtcNow;
        }
    }
}
```

**`Assets/Scripts/Gameplay/Loop/Session/ISessionStore.cs`** — read interface:

```csharp
namespace Golfin.Gameplay.Session
{
    /// <summary>
    /// Read-only view over session state. Foundation #1 of Loop v2 (interface-first
    /// services). Stage B writers continue to use GameSession's static API; readers
    /// that want testability go through this interface. Implementation in Stage B
    /// is a thin static-bus wrapper; future replay/headless impls swap in here.
    /// </summary>
    public interface ISessionStore
    {
        int    CurrentHoleNumber     { get; }
        string SelectedCharacterId   { get; }
        int    EquippedBagSlot       { get; }
        int    TurnCount             { get; }
    }
}
```

Plus a default impl in the same file or `GameSessionStore.cs`:
```csharp
public sealed class GameSessionStore : ISessionStore
{
    public int    CurrentHoleNumber   => GameSession.CurrentHoleNumber;
    public string SelectedCharacterId => GameSession.SelectedCharacterId;
    public int    EquippedBagSlot     => GameSession.EquippedBagSlot;
    public int    TurnCount           => GameSession.TurnCount;
}
```

### Files EDITED

**`GameSession.cs` (the moved file):**

Change namespace: `Golfin.Gameplay.UI.HUD` → `Golfin.Gameplay.Session`.

Add fields:
```csharp
// ── Session seed (set at Matchmaking found, survives between holes) ──
public static int    CurrentHoleNumber;
public static string SelectedCharacterId = string.Empty;
public static int    EquippedBagSlot;

// ── Cross-scene completion signal (NEW) ──
public static event System.Action<HoleCompletionData> OnHoleComplete;
```

Add methods:
```csharp
/// <summary>Initial seed at "OPPONENT FOUND". Also calls ResetForNewHole.</summary>
public static void SeedSession(int holeNumber, string characterId, int bagSlot)
{
    CurrentHoleNumber   = holeNumber;
    SelectedCharacterId = characterId ?? string.Empty;
    EquippedBagSlot     = bagSlot;
    ResetForNewHole();
}

/// <summary>PLAY NEXT path: re-points to a new hole, resets per-hole state, keeps seed.</summary>
public static void SetCurrentHole(int holeNumber)
{
    CurrentHoleNumber = holeNumber;
    ResetForNewHole();
}

/// <summary>Full session clear. MENU / back-to-Home path in Stage D.</summary>
public static void ResetSession()
{
    CurrentHoleNumber   = 0;
    SelectedCharacterId = string.Empty;
    EquippedBagSlot     = 0;
    ResetForNewHole();
}

/// <summary>Fire OnHoleComplete with the given payload. Called by HoleCompleteDriver.</summary>
public static void MarkHoleComplete(HoleCompletionData data) => OnHoleComplete?.Invoke(data);
```

`ResetForNewHole()` unchanged (does NOT touch seed fields — preserves Loop v1 §2c semantics).

**`Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs`** — extend `HandleShotComplete`:

Today, `HandleShotComplete` calls `ShowResultScreen` on `terminal == InCup`. Insert a `GameSession.MarkHoleComplete(...)` call FIRST so the Stage C ShellScene modal (when it lands) receives the event. Existing lab widget keeps showing for now (Stage C migrates that responsibility off).

```csharp
void HandleShotComplete(ShotResult result)
{
    if (result.TerminalState != BallState.InCup) return;

    // NEW: fire cross-scene signal for Stage C Result modal
    var completionData = new HoleCompletionData(
        terminalState:  result.TerminalState,
        strokes:        GameSession.TurnCount,
        penaltyStrokes: ComputePenaltyStrokesFromHistory(),  // sum ShotRecord.PenaltyStrokes
        holeNumber:     GameSession.CurrentHoleNumber > 0 ? GameSession.CurrentHoleNumber : HoleContext.HoleNumber
    );
    GameSession.MarkHoleComplete(completionData);

    // Existing lab path (kept for now; Stage C migrates this off):
    ShowResultScreen(GameSession.TurnCount);
}

static int ComputePenaltyStrokesFromHistory()
{
    int sum = 0;
    foreach (var rec in GameSession.ShotHistory) sum += rec.PenaltyStrokes;
    return sum;
}
```

**`Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs`** — seed at OPPONENT FOUND.

Find `OpponentScanRoutine` end (where `statusText.text = statusFoundText` lives). Just before fade-out, call:
```csharp
// Stage B: seed GameSession with the resolved hole/char/bag
int seededHole = (_resolvedHoleData != null) ? _resolvedHoleData.holeNumber : (_resolvedIndex + 1);
string charId  = CharacterManager.Instance != null ? CharacterManager.Instance.GetSelectedCharacterId() : string.Empty;
int bagSlot    = BagManager.Instance != null ? BagManager.Instance.EquippedBagSlot : 0;
Golfin.Gameplay.Session.GameSession.SeedSession(seededHole, charId, bagSlot);
```

This will require capturing `_resolvedHoleData` / `_resolvedIndex` on `Open(int holeIndex)` (currently resolved in local scope inside `Open`). Lift them to private fields so `OpponentScanRoutine` can read them.

### Files BULK-UPDATED (using directive only)

All ~30 files in the grep list: replace
```csharp
using Golfin.Gameplay.UI.HUD;
```
with
```csharp
using Golfin.Gameplay.Session;
```

Implementer's preferred approach: a single `sed -i '' 's|using Golfin.Gameplay.UI.HUD|using Golfin.Gameplay.Session|g'` over the list, then compile-verify. If compile fails because some file consumed something from `Golfin.Gameplay.UI.HUD` that did NOT move (unlikely — only GameSession + ShotRecord live there), restore that using and add the new one.

---

## Implementation steps (recommended order)

1. **Pre-flight checks** (above), log results.
2. **Move file** `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` → `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` via `git mv` (preserves history).
3. **Edit moved file**: namespace change + new fields + new methods + `OnHoleComplete` event.
4. **Create** `HoleCompletionData.cs` and `ISessionStore.cs` (+ `GameSessionStore` impl) in the new directory.
5. **Bulk-update** the 30 files' `using` directives.
6. **Edit `HoleCompleteDriver.cs`**: insert `GameSession.MarkHoleComplete(...)` call before `ShowResultScreen`.
7. **Edit `MatchmakingModalController.cs`**: lift `_resolvedHoleData`/`_resolvedIndex` to fields, seed `GameSession` in `OpponentScanRoutine` end.
8. **Delete the old empty directory** `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` if no other files remain in it (likely empty after the move). `git rm` the directory and its `.meta`.
9. **Compile clean.**
10. **Write 6 new EditMode tests** (next section).
11. **Run EditMode test gate.** Should remain green pre-existing + add the 6 new for 300/300 (current gate is 294/294 per Stage A close).
12. **Commit + push.** Message: `loop_v2_b_session_state_plumbing: GameSession namespace move + OnHoleComplete + Matchmaking seed`

---

## Tests (6 new EditMode tests, lives in `Assets/Scripts/Gameplay/Tests/`)

New test file: `Assets/Scripts/Gameplay/Tests/GameSessionTests.cs` (most), plus one in `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs` extension.

1. **`SeedSession_SetsAllThreeFields`** — `SeedSession(5, "char_iron7", 2)` then assert all three getters.
2. **`ResetForNewHole_PreservesSeedFields`** — seed, set turn/history, ResetForNewHole, assert seed fields unchanged but turn=1 and history empty.
3. **`ResetSession_ClearsAllSeedFields`** — seed, ResetSession, assert all three are zero/empty.
4. **`SetCurrentHole_UpdatesPointerWithoutClearingSeed`** — seed(5, "char_a", 2), SetCurrentHole(6), assert CurrentHoleNumber==6 but SelectedCharacterId=="char_a", EquippedBagSlot==2, TurnCount==1.
5. **`OnHoleComplete_FiresOnMarkHoleComplete_WithCorrectPayload`** — subscribe, MarkHoleComplete(data), assert subscriber received same data.
6. **`HoleCompleteDriver_OnInCupTerminal_FiresMarkHoleComplete`** — extend `HoleCompleteDriverTests.cs`. Inject mocked SM, simulate `OnShotComplete(InCup terminal)`, assert `GameSession.OnHoleComplete` fired with correct strokes/holeNumber.

ResetForNewHole-fires-OnTurnChanged-and-OnHistoryChanged was a refinement of an existing test; the existing test in `HoleSessionDriverTests` likely already covers this — verify and extend if needed.

---

## Definition of Done

**Audit grep:**
- [ ] `grep -rln 'using Golfin.Gameplay.UI.HUD' Assets/Scripts/` → zero hits
- [ ] `grep -rln 'Golfin.Gameplay.UI.HUD' Assets/Scripts/` → zero hits (no fully-qualified leftovers)
- [ ] `ls Assets/Scripts/Gameplay/UI/ShotUI/HUD/` → directory does not exist (or empty .meta only)
- [ ] `ls Assets/Scripts/Gameplay/Loop/Session/` → `GameSession.cs`, `HoleCompletionData.cs`, `ISessionStore.cs` (+ .meta files)
- [ ] `grep -n 'public static int    CurrentHoleNumber' Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` → one hit
- [ ] `grep -n 'OnHoleComplete' Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` → at least 2 hits (event decl + MarkHoleComplete invoke)
- [ ] `grep -n 'GameSession.MarkHoleComplete' Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs` → one hit
- [ ] `grep -n 'GameSession.SeedSession' Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` → one hit
- [ ] Project compiles clean
- [ ] EditMode test gate: **300/300 PASS** (294 prior + 6 new)

**Visual smoke** (Cesar's eyeballs):
- [ ] Play from Home → Hole Select → tap a hole card PLAY → matchmaking → OPPONENT FOUND → ball spawns at tee (existing flow). No regression.
- [ ] Console log "Stage B: GameSession seeded" message appears in editor (implementer adds a one-liner debug log behind `#if UNITY_EDITOR`).
- [ ] Fire a putt into the cup → lab `HoleCompleteWidget` still shows (existing behavior preserved; Stage C migrates this).
- [ ] Optional: subscribe a test logger to `GameSession.OnHoleComplete` and verify it fires once per InCup terminal.

---

## Handoff

**Implementer:** Claude Code (TELLCODE).
**Spec:** `Docs/Specs/Active/loop_v2_b_session_state_plumbing/SPEC.md`
**Architect-side close:** STATUS.md → DONE, move folder to `Docs/Specs/Completed/`, flip Notion entry to Done, set Closed date.

---

## Out of scope (deferred)

- **`IHoleProgressionStore`** — lands in Stage C next to its first writer (Result modal SUCCESS path).
- **`GameSession` graduates from static-bus to instance-with-DI** — keeps static API in Stage B for backward compat with the 30 existing subscribers; future stage can introduce instance impl without breaking callers.
- **FAILED state detection** — Stage B fires OnHoleComplete only on `BallState.InCup`. Stroke-cap-reached / time-out FAILED detection lands in Stage C (where the Result modal needs the distinction).
- **Lab `HoleCompleteWidget` retirement** — Stage B keeps the existing lab widget working alongside the new event. Stage C migrates the modal to ShellScene and decides the lab widget's fate.
- **Modal pattern migration** for non-Stage-C modals — audit P1-4 follow-up.
