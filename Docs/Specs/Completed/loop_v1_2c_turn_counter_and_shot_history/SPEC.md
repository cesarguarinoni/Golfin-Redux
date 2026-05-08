# SPEC — `loop_v1_2c_turn_counter_and_shot_history`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY 2026-05-07 21:15 JST.

## Goal

First persistent-per-hole state shipped on the §2a/§2b foundation. Drive the existing `GameSession.TurnCount` from real shot completion (today it's a static `= 1` that nobody updates), build a `List<ShotRecord>` history of completed shots, and reset both on hole load. The existing `PlayerCardWidget` already binds the TURN label — this task makes it actually update.

## Reference

- **§2a SPEC:** `Docs/Specs/Completed/loop_v1_2a_ball_state_machine/SPEC.md` — `BallStateMachine.OnShotComplete(ShotResult)` is the trigger this task subscribes to.
- **§2b SPEC:** `Docs/Specs/Completed/loop_v1_2b_camera_transitions/SPEC.md` — `LoopCameraDirector` is the precedent for "thin orchestration MonoBehaviour subscribed to SM events," same pattern this task uses.
- **No Figma references** — the TURN indicator is already in the existing PlayerCard prefab; this task doesn't touch UI hierarchy.
- **No new architecture** — extends existing `Golfin.Gameplay.UI.HUD.GameSession` static bus.

## Background — what exists today

Verified by code walk 2026-05-07 21:00 JST.

| File | Role for this task |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` | 7-line static class. `TurnCount = 1`, `OnTurnChanged` event, `SetTurn(int n)`. Today nobody calls `SetTurn` — `TurnCount` stays at 1 forever. **Extend with shot-history fields + reset.** |
| `Assets/Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs` | Already subscribes to `GameSession.OnTurnChanged` and renders `$"TURN {GameSession.TurnCount}"`. **Zero changes needed.** Once `SetTurn` starts firing, the widget updates automatically. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` | Static bus with `Reset()` and `OnChanged`. **Zero changes needed.** Already called from `PhysicsLabController.OnHoleUnloaded`. |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | Has `event Action<ShotResult> OnShotComplete` (coarse, terminal). **Subscribe target.** |
| `Assets/Scripts/Gameplay/Loop/ShotResult.cs` | Already provides `TerminalState` (AtRest/InCup/OB), `OBReason?`, `EndPosition`. **Source data for ShotRecord.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Already has `HandleShotComplete(ShotResult)` that calls `_shotController.CompleteShot()` + `_ballSM.ReArm()` (line ~1450). **Add session.RecordShot + session.AdvanceTurn calls here.** Already calls `HoleContext.Reset()` from `OnHoleUnloaded` (line ~1879). **Add `GameSession.ResetForNewHole()` call to `OnHoleLoaded` AND `OnHoleUnloaded`.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::HandleShotResolved` (line ~1300) | Caches `_lastShotOrigin` + `_lastShotLaunchDir` for §2b Director. **Source for ShotRecord.Origin.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::CurrentClubIndex` + `LabClubLabels` | Already public. **Source for ShotRecord.ClubLabel.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::OnHoleLoaded` (line ~1700) | Today populates HoleContext from HoleMetadata via reflection (par/yards/etc) and calls `HoleContext.Raise()` at the end. **Add `GameSession.ResetForNewHole()` immediately after Raise so widget renders fresh par + turn=1.** |

## Locked decisions (carry forward from prior chat)

- **L1 — Static class extension to `GameSession`.** No new file. `HoleSession` / `ShotRecord` types live in same `GameSession.cs` as nested types or sibling classes in same namespace.
- **L2 — Same — extend `GameSession`.** Don't proliferate near-identical statics. `GameSession` becomes "ephemeral per-hole session state" generally.
- **L3 — 1.5s auto-delay after AtRest before re-arm + turn advance.** Configurable via SerializeField on a new `HoleSessionDriver` MonoBehaviour. NOT hardcoded into the static.
- **L4 — Yes, OB counts as a turn.** Penalty stroke math is separate (deferred to result-screen / Loop v2 logic). The shot itself happened.
- **L5 — Wire reset before widget re-render.** Call order in `OnHoleLoaded`: existing reflection-driven HoleContext writes happen first → `HoleContext.Raise()` → `GameSession.ResetForNewHole()` (which fires `OnTurnChanged`). Both events fire; widget re-renders cleanly with fresh par + turn=1.

## Architecture context

- **No new asmdef.** All work in `Golfin.Gameplay.UI.HUD` (GameSession extension), `Golfin.Physics.Viewer` (HoleSessionDriver MonoBehaviour + PhysicsLabController call sites).
- **No changes to** `Golfin.Gameplay.Loop` (read-only consumer of OnShotComplete / ShotResult), `Golfin.Physics.Core`, `Golfin.Physics.Stats`, `Golfin.Diagnostics.Runtime`, any aero CSV.
- **No new test asmdef.** New tests land in existing `Golfin.Physics.Tests` or a sibling EditMode test under same asmdef.
- **The `HoleSessionDriver` MonoBehaviour is the §2b-equivalent thin orchestration layer.** Mirrors `LoopCameraDirector` precedent exactly: subscribes to SM events in Awake, drives the static bus, no game-logic of its own.

## Implementation

### A. Extend `GameSession` static bus

**Location:** `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs`. Replace the entire 7-line file with:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    /// <summary>
    /// Per-hole ephemeral session state. Reset on every hole load.
    /// Not persisted across app restarts (Loop v2 / save state spec handles persistence).
    /// </summary>
    public static class GameSession
    {
        // ── Turn counter ──────────────────────────────────────────────────────
        public static int TurnCount = 1;
        public static event System.Action OnTurnChanged;
        public static void SetTurn(int n) { TurnCount = n; OnTurnChanged?.Invoke(); }

        // ── Shot history ──────────────────────────────────────────────────────
        public static readonly List<ShotRecord> ShotHistory = new List<ShotRecord>();
        public static event System.Action OnHistoryChanged;
        public static void RecordShot(ShotRecord record)
        {
            ShotHistory.Add(record);
            OnHistoryChanged?.Invoke();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        /// <summary>
        /// Reset session state for a new hole. Clears history, sets turn back to 1.
        /// Fires both OnTurnChanged and OnHistoryChanged so subscribers re-render cleanly.
        /// </summary>
        public static void ResetForNewHole()
        {
            TurnCount = 1;
            ShotHistory.Clear();
            OnTurnChanged?.Invoke();
            OnHistoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// Append-only record of one completed shot. Built from ShotResult on each
    /// BallStateMachine.OnShotComplete fire.
    /// </summary>
    public readonly struct ShotRecord
    {
        public readonly int    ShotNumber;          // 1-indexed within the hole
        public readonly string ClubLabel;           // "Driver", "Iron 7", "Wedge", "Putter"
        public readonly Vector3 OriginPosition;
        public readonly Vector3 FinalPosition;
        public readonly float   DistanceXZMeters;   // origin → final XZ
        public readonly string  TerminalState;      // "AtRest" / "InCup" / "OB"
        public readonly string  OBReason;           // "Water" / "OutOfBounds" / "ExitedWorldBounds" / null
        public readonly string  FinalSurface;       // best-effort; "Unknown" if not derivable

        public ShotRecord(
            int shotNumber, string clubLabel,
            Vector3 originPosition, Vector3 finalPosition,
            float distanceXZMeters,
            string terminalState, string obReason, string finalSurface)
        {
            ShotNumber       = shotNumber;
            ClubLabel        = clubLabel;
            OriginPosition   = originPosition;
            FinalPosition    = finalPosition;
            DistanceXZMeters = distanceXZMeters;
            TerminalState    = terminalState;
            OBReason         = obReason;
            FinalSurface     = finalSurface;
        }
    }
}
```

### B. New `HoleSessionDriver` MonoBehaviour

**Location:** `Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs`. Namespace `Golfin.Physics.Viewer`.

Mirrors `LoopCameraDirector` precedent: thin orchestration, subscribes to SM events, drives the static bus.

```csharp
using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2c: subscribes to BallStateMachine.OnShotComplete, builds a ShotRecord
    /// from the result + PhysicsLabController context, appends it to GameSession.ShotHistory,
    /// and after a configurable delay calls GameSession.SetTurn(turn + 1).
    ///
    /// Mirrors LoopCameraDirector pattern — thin orchestration, no game logic.
    /// </summary>
    public class HoleSessionDriver : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;
        [Header("Re-arm timing")]
        [SerializeField] float postShotDelaySeconds = 1.5f;

        BallStateMachine _sm;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PhysicsLabController>();
            _sm = controller?.BallSM;
            if (_sm != null) _sm.OnShotComplete += HandleShotComplete;
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;
        }

        void HandleShotComplete(ShotResult result)
        {
            // Build the record from the SM result + controller's cached state.
            var record = BuildShotRecord(result);
            GameSession.RecordShot(record);

            // Schedule the turn advance after a settling delay so the player
            // sees the ball at rest before the UI ticks over.
            StartCoroutine(AdvanceTurnAfterDelay());
        }

        IEnumerator AdvanceTurnAfterDelay()
        {
            yield return new WaitForSeconds(postShotDelaySeconds);
            GameSession.SetTurn(GameSession.TurnCount + 1);
        }

        ShotRecord BuildShotRecord(ShotResult result)
        {
            string clubLabel = "Unknown";
            if (controller != null
                && controller.CurrentClubIndex >= 0
                && controller.CurrentClubIndex < PhysicsLabController.LabClubLabels.Length)
            {
                clubLabel = PhysicsLabController.LabClubLabels[controller.CurrentClubIndex];
            }

            Vector3 origin = controller != null ? controller.LastShotOrigin : Vector3.zero;
            Vector3 finalPos = new Vector3(
                result.EndPosition.x.ToFloat(),
                result.EndPosition.y.ToFloat(),
                result.EndPosition.z.ToFloat());

            float dx = finalPos.x - origin.x;
            float dz = finalPos.z - origin.z;
            float distXZ = Mathf.Sqrt(dx * dx + dz * dz);

            // FinalSurface — best-effort. ShotResult doesn't carry it directly today;
            // attempt to derive from controller.LastTrajectory's last terrain hit.
            string finalSurface = "Unknown";
            var traj = controller?.LastTrajectory;
            if (traj?.terrainHits != null && traj.terrainHits.Count > 0)
                finalSurface = traj.terrainHits[traj.terrainHits.Count - 1].Surface.ToString();

            string obReason = result.OBReason.HasValue ? result.OBReason.Value.ToString() : null;

            return new ShotRecord(
                shotNumber: GameSession.TurnCount,
                clubLabel: clubLabel,
                originPosition: origin,
                finalPosition: finalPos,
                distanceXZMeters: distXZ,
                terminalState: result.TerminalState.ToString(),
                obReason: obReason,
                finalSurface: finalSurface);
        }
    }
}
```

**Inspector wiring:** Add `HoleSessionDriver` component to the same GameObject as `LoopCameraDirector` in `LabScaffold.unity`. Set `controller` reference. Use Unity Editor APIs (`gameobject-component-add` MCP), NOT raw YAML edit (per controls_g lesson).

### C. Hook PhysicsLabController to drive `GameSession.ResetForNewHole`

Two call sites in `PhysicsLabController.cs`:

**1. `OnHoleLoaded(string sceneName)` — ADD after `HoleContext.Raise()` (currently around line ~1875).**

```csharp
// Existing line at ~1875:
Golfin.Gameplay.UI.HUD.HoleContext.Raise();

// ADD immediately below:
// §2c: reset session state for the new hole. Fires OnTurnChanged so PlayerCardWidget renders fresh "TURN 1".
Golfin.Gameplay.UI.HUD.GameSession.ResetForNewHole();
```

**2. `OnHoleUnloaded()` — ADD after `HoleContext.Reset()` (currently around line ~1879).**

```csharp
// Existing line at ~1879:
Golfin.Gameplay.UI.HUD.HoleContext.Reset();

// ADD immediately below:
// §2c: clear session state on hole unload (defensive — next hole load will reset again,
// but this guarantees clean state if we go to a no-hole flat-ground fallback).
Golfin.Gameplay.UI.HUD.GameSession.ResetForNewHole();
```

**Rationale for both call sites:** the loaded path covers the normal new-hole case. The unloaded path handles the edge case of returning to flat-ground fallback (no hole loaded) — without it, the TURN counter would freeze at the last hole's value until another hole loaded.

### D. Verify `PhysicsLabController.HandleShotComplete` does not conflict

Read the existing handler (line ~1450):

```csharp
void HandleShotComplete(Golfin.Gameplay.Loop.ShotResult result)
{
    Debug.Log(...);
    if (ballAnimator?.CurrentBall != null)
        _orbitCenter = ballAnimator.CurrentBall.position;
    _shotController?.CompleteShot();
    _ballSM.ReArm();
}
```

This handler ALREADY calls `_ballSM.ReArm()` immediately on shot complete. **§2c's HoleSessionDriver subscribes to the SAME OnShotComplete event** — both handlers will fire on the same SM event. Order is non-deterministic.

**Critical correctness check:** §2c's `RecordShot` reads `controller.LastTrajectory` — that field is set in `HandleShotResolved` (per §2b), NOT cleared in `HandleShotComplete`, so it stays valid across the ReArm call. Verified safe.

**Critical correctness check 2:** §2c's `RecordShot` reads `controller.LastShotOrigin` — set in `HandleShotResolved` for the shot just completed, also not cleared. Verified safe.

**Critical correctness check 3:** §2c's `RecordShot` uses `GameSession.TurnCount` AS the shot number BEFORE incrementing. So shot 1 records as ShotNumber=1, then SetTurn(2) fires; shot 2 records as ShotNumber=2, then SetTurn(3); etc. Order-of-operations correct.

**No code changes needed in HandleShotComplete.** §2c's driver runs alongside.

### E. Verify TURN label renders

`PlayerCardWidget` already wired (verified in code walk):

```csharp
void OnEnable()
{
    PlayerContext.OnChanged   += Refresh;
    GameSession.OnTurnChanged += Refresh;
    Refresh();
}
// ...
if (_turnText != null) _turnText.text = $"TURN {GameSession.TurnCount}";
```

**No code changes needed to PlayerCardWidget.** Once `GameSession.SetTurn` starts firing, the widget updates.

## Tests

**Location:** `Assets/Scripts/Physics/Tests/HoleSessionDriverTests.cs` (new file). Asmdef: `Golfin.Physics.Tests` (existing, already references `Golfin.Gameplay.Loop` and `Golfin.Gameplay.UI.HUD`).

**Test seam:** `HoleSessionDriver` takes a `PhysicsLabController` reference. Tests can't easily instantiate a full PhysicsLabController without a scene. Solution mirroring §2b precedent: factor the data-extraction logic into a `BuildShotRecordStatic(...)` static helper that takes plain primitives, so tests exercise the static helper directly.

```csharp
// In HoleSessionDriver.cs, expose a static helper that does the math without controller deps:
public static ShotRecord BuildShotRecordStatic(
    int shotNumber, string clubLabel,
    Vector3 origin, Vector3 finalPos,
    string terminalState, string obReason, string finalSurface)
{
    float dx = finalPos.x - origin.x;
    float dz = finalPos.z - origin.z;
    float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
    return new ShotRecord(shotNumber, clubLabel, origin, finalPos, distXZ,
                          terminalState, obReason, finalSurface);
}
```

**Required tests** (minimum 7):

1. **`GameSession_SetTurn_FiresOnTurnChanged`** — assert event fires + TurnCount updates.
2. **`GameSession_RecordShot_AppendsToHistoryAndFiresEvent`** — assert ShotHistory.Count grows + OnHistoryChanged fires.
3. **`GameSession_ResetForNewHole_ClearsHistoryAndResetsTurn`** — populate state, call ResetForNewHole, assert TurnCount=1, ShotHistory.Count=0, both events fired.
4. **`GameSession_ResetForNewHole_FiresEventsEvenWhenAlreadyDefault`** — call ResetForNewHole on fresh state, assert events still fire (defensive — widget re-renders).
5. **`ShotRecord_BuildStatic_ComputesXZDistanceCorrectly`** — origin (0,0,0), final (3,5,4), assert distXZ=5.0 (3-4-5 triangle).
6. **`ShotRecord_BuildStatic_HandlesYDifferenceWithoutAffectingXZDistance`** — origin (0,0,0), final (3,100,4), assert distXZ still=5.0.
7. **`ShotRecord_BuildStatic_PreservesAllFields`** — assert all 8 fields round-trip from constructor args to record.

**Test isolation note:** because `GameSession` is a static class with global state, tests MUST call `GameSession.ResetForNewHole()` in `[SetUp]` to avoid order-dependent failures. Document in the test file's class header.

**Test gate:** **N → N+7 PASS, 0 IGNORED** where N is the current baseline. The original spec wrote 241 → 248 but controls_h iter-8 (closed 2026-05-08) added/removed tests since then. **Implementer runs test gate first, records actual N in IMPLEMENTER_REPORT, then adds 7 new tests for the N+7 target.** If any pre-existing test fails on the baseline run, escalate `IMPLEMENTER_BLOCKED` BEFORE adding new tests — don't "fix" by editing existing tests.

**Architect note 2026-05-08 13:30 JST:** The original 241 → 248 numbers were correct on 2026-05-07; the post-controls_h-iter-8 baseline differs. Confirm and proceed.

## Smoke evidence

Use `CaptureCore.SnapWhenStateReached` from §2b for state-gated captures. NO `WaitForSeconds(N)` for state-dependent moments per controls_g lesson.

**Three captures + one log artifact:**

1. **`controls_2c_turn1_aiming.png`** — fresh hole loaded (Hole_01_Geo additively), capture at first BallState.Aiming. Verify TURN label reads "TURN 1".
2. **`controls_2c_turn2_after_first_shot.png`** — fire driver shot, wait for AtRest, wait postShotDelaySeconds, capture at next Aiming. Verify TURN label reads "TURN 2".
3. **`controls_2c_turn1_after_hole_reload.png`** — after C.2 above, trigger a fresh hole load (cycle the hole), capture at first Aiming. Verify TURN label has reset to "TURN 1" (NOT 2 or 3 — proves ResetForNewHole fired).
4. **`controls_2c_history_log.txt`** — text file dumping `GameSession.ShotHistory` after capture C.2 fires. Should show exactly 1 entry with ShotNumber=1, ClubLabel="Driver", TerminalState="AtRest" (or whatever the shot ended as).

**Filed under** `Docs/Specs/Active/loop_v1_2c_turn_counter_and_shot_history/screenshots/` with `controls_2c_*` prefix.

## Definition of Done

- `GameSession` extended: ShotHistory list + OnHistoryChanged event + RecordShot method + ResetForNewHole method. Existing TurnCount/SetTurn/OnTurnChanged unchanged in signature.
- New `ShotRecord` struct in same namespace.
- New `HoleSessionDriver` MonoBehaviour shipped + Inspector-wired in `LabScaffold.unity` (via Unity Editor MCP, NOT raw YAML).
- `PhysicsLabController.OnHoleLoaded` calls `GameSession.ResetForNewHole()` after `HoleContext.Raise()`.
- `PhysicsLabController.OnHoleUnloaded` calls `GameSession.ResetForNewHole()` after `HoleContext.Reset()`.
- 7 new EditMode tests in `HoleSessionDriverTests.cs`, all PASS. Test gate: **N+7 PASS, 0 IGNORED** where N is the baseline confirmed in IMPLEMENTER_REPORT.
- 3 captures + 1 log file filed under `Docs/Specs/Active/loop_v1_2c_turn_counter_and_shot_history/screenshots/`.
- TURN label visibly updates from "TURN 1" → "TURN 2" between shots and resets on hole reload (load Hole_01, fire 2 shots, see TURN 3, cycle to Hole_06, see TURN 1).

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** if:
  - `LabScaffold.unity` Editor MCP wiring of `HoleSessionDriver` fails (component-add throws, or controller-reference set fails). Implementer tries 2 retries before escalating; architect resolves with alternative wiring path.
  - PASS gate breaks unexpectedly (any of the 241 pre-existing tests starts failing). Most likely root cause: `GameSession.ShotHistory.Clear()` triggering OnHistoryChanged for subscribers that didn't exist before — which shouldn't break anything since nothing else subscribes today, but worth a careful look. Architect investigates.
  - PlayerCardWidget stops rendering at all after the change (regression, not enhancement). Architect investigates whether the OnTurnChanged subscription got broken by the static-class extension.
- **`IMPLEMENTER_PARTIAL`** acceptable if:
  - All code + 6 of 7 unit tests + first 2 captures land clean, but the C.3 hole-reload capture hits friction (additive scene cycling sometimes flaky in lab smoke). Acceptable to ship 2/3 captures + all unit tests; architect closes with note.

## Out of scope

- **Persistence across app restart.** Loop v2 / save state spec. `GameSession` stays purely in-memory.
- **Score-to-par calculation.** §2d (result screen) computes that from ShotHistory. §2c just records the data.
- **Hole-complete detection.** §2d wires `ICupDetector` for real (today: `NullCupDetector` stub). InCup terminal state is recorded by §2c if it ever fires, but no shot will reach InCup until §2d.
- **Penalty stroke math.** OB shots count as turns per L4, but the +1 penalty stroke convention is §2d/Loop v2.
- **OB ball-replacement rules.** Real golf has multiple options (drop at OB position, drop back along flight line, replay from previous spot). §2c records the OB shot but doesn't decide where the next shot fires from — that's `BallStateMachine.ReArm` + future logic.
- **Per-club shot history filtering / replay.** §2c builds the data; UI consumers come later.
- **Pretty TURN counter polish.** Minimal — the existing PlayerCardWidget label already exists; this task just makes it move.
- **Per-stroke replay / undo.** Pure forward log.

## Hard rules for implementer

1. **Do NOT modify** `BallStateMachine.cs`, `BallState.cs`, `ShotResult.cs`, `BallStateChange.cs`, `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, any aero CSV, any test currently in PASS state outside `HoleSessionDriverTests.cs`.
2. **Do NOT modify `PlayerCardWidget.cs`.** It's already correctly wired. Touching it risks breaking the existing render path.
3. **Do NOT modify `LabScaffold.unity` via raw YAML.** Use Unity Editor APIs (`gameobject-create`, `gameobject-component-add`, `gameobject-component-modify`, `scene-save` MCP tools). Per controls_g deviation #3 lesson, raw YAML edits trigger Unity reload popups.
4. **Do NOT use `WaitForSeconds(N)` for state-dependent captures.** State-gate via `SnapWhenStateReached` per controls_g_smoke_followup precedent. The 1.5s `postShotDelaySeconds` IS allowed because it's a deliberate user-facing settling delay, NOT a capture trigger.
5. **Do NOT add InCup handling beyond what falls out automatically.** §2c's RecordShot will record InCup TerminalState if SM ever transitions to it, but §2d wires the real ICupDetector. Don't preempt §2d.
6. **Do NOT proliferate static-bus files.** Extend `GameSession` per L1/L2; do not create `HoleSessionContext`, `ShotHistoryContext`, etc.
7. **Smoke evidence per §2a Lessons M+N + reviewer's controls_g lesson:** file persisted on disk + parallel-path Read verification + content-sanity description.
8. **Bit-exact pre-existing test gate must hold.** Adding 7 tests to baseline N → N+7. If any pre-existing test starts failing, escalate `IMPLEMENTER_BLOCKED` immediately — do NOT "fix" by editing existing tests.
