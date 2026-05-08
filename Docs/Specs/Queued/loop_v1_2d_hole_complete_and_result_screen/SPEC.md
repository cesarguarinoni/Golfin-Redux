# SPEC — `loop_v1_2d_hole_complete_and_result_screen`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY <PENDING — set timestamp once Cesar confirms 7 locks below>.

## Goal

Ship a real `ICupDetector` implementation, wire it into `PhysicsLabController.OnHoleLoaded`, gate the existing `HandleShotComplete` re-arm on `AtRest` only, and add a minimal Result Screen modal that fires on `OnShotComplete(terminal=InCup)` showing strokes / par / score-to-par. This closes the foundational loop §2a→§2b→§2c by giving the player a visible end-state when they actually hole out.

## Reference

- **§2c SPEC:** `Docs/Specs/Completed/loop_v1_2c_turn_counter_and_shot_history/SPEC.md` — `HoleSessionDriver` is the precedent this task mirrors for "thin orchestration MonoBehaviour subscribed to BallSM events".
- **§2a SPEC:** `Docs/Specs/Completed/loop_v1_2a_ball_state_machine/SPEC.md` — the OnShotComplete contract this task subscribes to. Cup-scan path verified at `BallStateMachine.cs:166-211` (the `default:` branch of the termination switch in `OnTrajectoryComputed`).
- **Figma frame:** Golfin Game Redux file `5gEAHjl6xAtW8iYY7NMvWd`, node id `12987-4556`. URL: https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/Golfin-Game-Redux?node-id=12987-4556. **Status: PENDING canonical confirmation from Cesar (Lock Q1 below). Once confirmed, Architect runs `Figma:get_design_context` and fills §UI implementation with extracted RectTransform values, font sizes, padding, and colors.**
- **Reference PNGs (visual diff companions):**
  - `Docs/Reference/Results Screen/Results - Success (Replay).png`
  - `Docs/Reference/Results Screen/Results - Success (Replay)-1.png`
  - `Docs/Reference/Results Screen/Results - Failed (Replay).png`
  - `Docs/Reference/Results Screen/Results - Failed (Replay)-1.png`
- **Imported PNG assets** (already in `Assets/Art/ResultScreen/`):
  - `Background - Banner.png` (66 KB)
  - `Background - HoleCard.png` (267 KB)
  - `Button - Play.png` (46 KB)
  - `Button - Replay.png` (40 KB)
  - `Button - Retry.png` (41 KB)
  - `Icon - Check.png` (500 B — Success indicator)
  - `Icon - X.png` (1.3 KB — Failed indicator)
- **Placeholder vs canonical content notes:** The Figma may use placeholder strokes / par / score numbers; spec's data-binding section ignores those literals and binds live values from `GameSession.TurnCount` and `HoleContext.Par`.

## Background — what exists today

Verified by code walk 2026-05-09 at session start.

| File | Role for this task |
|---|---|
| `Assets/Scripts/Gameplay/Loop/ICupDetector.cs` | Interface: `bool IsInCup(fp3 position, fp ballRadius)`. **Implement RealCupDetector against this.** |
| `Assets/Scripts/Gameplay/Loop/NullCupDetector.cs` | Current default. Always returns false. Stays as-is for headless / no-hole sessions. |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | `SetCupDetector(ICupDetector)` runtime swap (line ~55). The cup scan happens in `OnTrajectoryComputed` lines ~166–211 inside the `default:` branch of the termination switch — iterates `trajectory.samples` in order; first sample where `IsInCup(sample.position, ballRadius)` returns true wins; terminal becomes `BallState.InCup` with `terminalPos = sample.position`, `terminalSurface = _surfaces.Classify(sample.x, sample.z)`. **No changes needed to BSM.** |
| `Assets/Scripts/Gameplay/Loop/ShotResult.cs` | Already carries `TerminalState` (AtRest/InCup/OB), `EndPosition` (fp3), and other fields. **Source data; no changes.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::OnHoleLoaded` | After `HoleContext.Raise()` and `GameSession.ResetForNewHole()` (currently around line ~1442), `HoleContext.PinWorld` is populated from the `Flag` GO. **Add: `_ballSM?.SetCupDetector(new RealCupDetector(HoleContext.PinWorld));` immediately after `GameSession.ResetForNewHole()`.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::HandleShotComplete` | Currently always calls `_shotController?.CompleteShot(); _ballSM.ReArm();` regardless of terminal state. Existing inline comment: *"§2d will gate this on result.TerminalState == AtRest later."* **Add a guard: skip the re-arm when `result.TerminalState == BallState.InCup` — the modal owns re-arm.** |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` | Already has `PinWorld` Vector3, populated from Flag GO in OnHoleLoaded. **Source for cup position; no changes.** |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` (§2c) | `TurnCount`, `ShotHistory`, `OnTurnChanged`, `OnHistoryChanged`, `ResetForNewHole()`. **Source for stroke count; no changes.** |
| `Assets/Art/ResultScreen/` | All 7 PNG assets imported. **Source assets for HoleCompleteWidget UI.** |

**One factual correction vs KICKOFF_TOMORROW.md:** the kickoff says cup detection happens "during Rolling at line 184". Actually the scan iterates **all** trajectory samples (Flying + Rolling alike) and lives inside `OnTrajectoryComputed`, not in a per-Rolling-tick loop. This affects how RealCupDetector must guard against high-flying samples that happen to be over the cup XZ — see Lock Q4 + Implementation §A height-tolerance.

## Locked decisions

> **STATE BEFORE FIRING:** Cesar confirms locks Q1–Q7 below in chat; Architect updates this section to record final values, sets timestamp on Status, moves folder Queued → Active, then Cesar fires kickoff. Until then this section reads "PENDING".

- **Q1 — Figma node `12987-4556` canonicality:** PENDING — Architect leans "ASK before extract per project rule"; once Cesar confirms canonical, Architect runs `Figma:get_design_context(fileKey=5gEAHjl6xAtW8iYY7NMvWd, nodeId=12987-4556)` and fills implementation §F with extracted values.
- **Q2 — Failed state in §2d scope:** PENDING — Architect lean: **NO**. Ship Success only. No Failed-state UI, no Failed-state trigger logic. Defer to §2e or later when OB / stroke-limit semantics get formalized. The `Icon - X.png` and "Failed" reference PNGs are imported but unused this task.
- **Q3 — `PinWorld` access pattern (constructor inject vs static read):** PENDING — Architect lean: **constructor inject**, take `Vector3 pin` in `RealCupDetector(Vector3 pin)`, convert to fp3 once at construction time, never re-read from `HoleContext.PinWorld`. Pin doesn't move during a hole; this gives determinism + zero coupling between detector and static bus. If Cesar wants a pin-can-move-mid-hole future, switch to static read later.
- **Q4 — Cup-detection geometry (instantaneous-XZ vs guarded-XZ-and-height):** PENDING — Architect lean: **guarded XZ-and-height**. Sample is in cup iff `XZ_distance(sample, pin) < (cupRadius - ballRadius)` AND `sample.y < pin.y + ballRadius` (i.e. ball top at or below green level — filters out high-flying samples that happen to be directly over the pin XZ). The scan still keeps "first sample wins" semantics — no sustain requirement.
- **Q5 — Button → action mapping in §2d:** PENDING — Architect lean: **all three buttons (Play / Replay / Retry) just close the modal and call `BallStateMachine.ReArm()`** in §2d. This keeps §2d truly small. Real flows (Play = next hole, Retry = re-tee current hole, Replay = view shot history) get wired in §2e / Loop v2 along with hole-cycling logic. Button click handlers will be split in code so the next task wires them with real behavior easily.
- **Q6 — `HandleShotComplete` re-arm gate on InCup:** PENDING — Architect lean: **YES, gate it**. Existing handler must skip `_shotController?.CompleteShot(); _ballSM.ReArm();` when `result.TerminalState == BallState.InCup`. The modal's button handler calls these instead. Without this gate, the SM re-arms before the modal even shows, leaving the lab in a weird half-state.
- **Q7 — `HoleSessionDriver` turn-advance on InCup (carry-over from §2c):** PENDING — Architect lean: **leave as-is**. `HoleSessionDriver` advances TurnCount unconditionally after 1.5s. On InCup the modal is already showing, so the background TurnCount tick to N+1 is invisible. When Continue/Play closes the modal and the next hole loads, `GameSession.ResetForNewHole()` resets TurnCount=1 anyway. Adding a gate is a cosmetic follow-up, not §2d-critical. (Documented under **Out of scope**.)

## Architecture context

- **Asmdef boundaries affected:**
  - `Golfin.Gameplay.Loop` — adds `RealCupDetector.cs` (sibling of `NullCupDetector.cs`).
  - `Golfin.Physics.Viewer` — adds `HoleCompleteDriver.cs` (sibling of `HoleSessionDriver.cs`); modifies `PhysicsLabController.cs` (~5 lines total: 1 in OnHoleLoaded for SetCupDetector, ~4 in HandleShotComplete for the InCup gate).
  - `Golfin.Gameplay.UI.ShotUI` (or sibling) — adds `HoleCompleteWidget.cs`.
  - `Golfin.Physics.Tests` — adds `RealCupDetectorTests.cs` and `HoleCompleteDriverTests.cs`.
- **No changes to** `Golfin.Physics.Core`, `Golfin.Physics.Stats`, any aero CSV, `BallStateMachine.cs`, `ShotResult.cs`, `BallState.cs`, `BallStateChange.cs`, `BallSimulation.cs`, `Trajectory.cs`, `ICupDetector.cs`, `NullCupDetector.cs`, `HoleContext.cs`, `GameSession.cs`, `HoleSessionDriver.cs`, `LoopCameraDirector.cs`, `PlayerCardWidget.cs`, any test currently in PASS state outside the two new test files.
- **`HoleCompleteDriver` mirrors §2c `HoleSessionDriver`:** thin orchestration MonoBehaviour, subscribes to `BallStateMachine.OnShotComplete` in Awake, unsubscribes in OnDestroy, no game-logic of its own. The widget owns its own UI.
- **Static-bus reads, not writes:** Driver reads `GameSession.TurnCount` and `HoleContext.Par` on InCup to compute strokes + score-to-par. No writes to either bus from §2d.

## Implementation

### A. New `RealCupDetector` class

**Location:** `Assets/Scripts/Gameplay/Loop/RealCupDetector.cs`. Namespace `Golfin.Gameplay.Loop`.

```csharp
using Golfin.Physics.Math;
using UnityEngine;

namespace Golfin.Gameplay.Loop
{
    /// <summary>
    /// Cup detector backed by a fixed pin world position. Constructed once per hole
    /// load; pin position is captured at construction time and not re-read.
    /// Determinism rules: pure fp math, no Unity API calls, no Time/Random.
    /// </summary>
    public sealed class RealCupDetector : ICupDetector
    {
        // Regulation cup mouth: 4.25 inch diameter → 0.054 m radius.
        public static readonly fp DefaultCupRadius = fp.FromFloat(0.054f);

        readonly fp3 _pin;
        readonly fp  _cupRadius;

        /// <summary>
        /// Default constructor — uses regulation cup radius. Height tolerance is
        /// derived from the ballRadius arg passed to IsInCup at scan time.
        /// </summary>
        public RealCupDetector(Vector3 pin)
            : this(pin, DefaultCupRadius)
        { }

        public RealCupDetector(Vector3 pin, fp cupRadius)
        {
            _pin = new fp3(fp.FromFloat(pin.x), fp.FromFloat(pin.y), fp.FromFloat(pin.z));
            _cupRadius = cupRadius;
        }

        public bool IsInCup(fp3 position, fp ballRadius)
        {
            // Height gate: ball top must be at or below pin Y (filters out flying samples
            // that happen to be directly over the cup XZ). pin Y + ballRadius marks the
            // green-level threshold; sample.y must be ≤ that.
            if (position.y > _pin.y + ballRadius) return false;

            // XZ-distance gate: ball center must be within (cupRadius - ballRadius)
            // of pin XZ. Squared compare avoids the sqrt.
            fp dx = position.x - _pin.x;
            fp dz = position.z - _pin.z;
            fp distSq = dx * dx + dz * dz;
            fp effRadius = _cupRadius - ballRadius;
            if (effRadius <= fp.Zero) return false; // ball larger than cup → never enters
            return distSq < effRadius * effRadius;
        }

        // Test seam — exposes the math without requiring an instance.
        public static bool IsInCupStatic(fp3 position, fp ballRadius, fp3 pin, fp cupRadius)
        {
            if (position.y > pin.y + ballRadius) return false;
            fp dx = position.x - pin.x;
            fp dz = position.z - pin.z;
            fp distSq = dx * dx + dz * dz;
            fp effRadius = cupRadius - ballRadius;
            if (effRadius <= fp.Zero) return false;
            return distSq < effRadius * effRadius;
        }
    }
}
```

> **NOTE for implementer:** if `fp` doesn't expose direct `<` `>` `*` `-` operators in the patterns shown, fall back to the `fp.FromFloat`/`.ToFloat` round-trip pattern used in `BallStateMachine.cs`. Do NOT change the public API of the class — only the internal arithmetic.

### B. Wire `SetCupDetector` in `PhysicsLabController.OnHoleLoaded`

**Location:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`, inside the `if (meta != null)` branch of `OnHoleLoaded`, immediately after the existing line:

```csharp
// §2c: reset session state for the new hole. Fires OnTurnChanged so PlayerCardWidget renders fresh "TURN 1".
Golfin.Gameplay.UI.HUD.GameSession.ResetForNewHole();
```

**ADD:**

```csharp
// §2d: install a real cup detector keyed to this hole's pin position.
// PinWorld was just written above (Flag GO scan); SetCupDetector is a runtime swap on the SM.
if (_ballSM != null)
{
    _ballSM.SetCupDetector(
        new Golfin.Gameplay.Loop.RealCupDetector(
            Golfin.Gameplay.UI.HUD.HoleContext.PinWorld));
    Debug.Log($"[PhysicsLab][§2d] RealCupDetector installed at pin={Golfin.Gameplay.UI.HUD.HoleContext.PinWorld:F3}");
}
```

**Also handle `OnHoleUnloaded`:** revert to `NullCupDetector` so a flat-ground fallback session doesn't carry the previous hole's pin. Immediately after the existing `GameSession.ResetForNewHole()` call in `OnHoleUnloaded`:

```csharp
// §2d: revert to NullCupDetector for flat-ground fallback (no pin = no cup).
if (_ballSM != null)
    _ballSM.SetCupDetector(new Golfin.Gameplay.Loop.NullCupDetector());
```

### C. Gate `HandleShotComplete` re-arm on `AtRest`/`OB` only

**Location:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::HandleShotComplete`. The existing body ends with:

```csharp
// Re-arm shot controller for the next shot.
_shotController?.CompleteShot();
_ballSM.ReArm();
```

**REPLACE** that final two-line block with:

```csharp
// §2d: re-arm only on AtRest/OB. On InCup, HoleCompleteDriver shows the modal and
// owns re-arm via its button handler.
if (result.TerminalState == Golfin.Gameplay.Loop.BallState.AtRest
    || result.TerminalState == Golfin.Gameplay.Loop.BallState.OB)
{
    _shotController?.CompleteShot();
    _ballSM.ReArm();
}
// else InCup: HoleCompleteDriver handles re-arm via modal close.
```

**Update the existing inline comment** above `HandleShotComplete`:

```csharp
/// <summary>
/// §2a: Called by BallStateMachine when a shot reaches a terminal state (AtRest, InCup, OB).
/// Resets camera target and re-arms the shot controller on AtRest/OB; on InCup,
/// re-arm is deferred to HoleCompleteDriver's modal close (§2d).
/// </summary>
```

**Also add an internal accessor** for the driver to invoke the same re-arm path on modal close:

```csharp
// §2d: invoked by HoleCompleteDriver after the modal is dismissed.
internal void RearmAfterHoleComplete()
{
    _shotController?.CompleteShot();
    _ballSM.ReArm();
}
```

### D. New `HoleCompleteDriver` MonoBehaviour

**Location:** `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs`. Namespace `Golfin.Physics.Viewer`. Mirrors `HoleSessionDriver` precedent.

```csharp
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2d: subscribes to BallStateMachine.OnShotComplete; on terminal=InCup,
    /// reads strokes (GameSession.TurnCount) and par (HoleContext.Par), computes
    /// score-to-par, and shows the HoleCompleteWidget Success modal. The widget's
    /// button handlers call back into this driver to close the modal + re-arm
    /// via PhysicsLabController.RearmAfterHoleComplete().
    ///
    /// Mirrors HoleSessionDriver (§2c) pattern — thin orchestration, no game logic.
    /// </summary>
    public class HoleCompleteDriver : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;
        [SerializeField] Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget widget;

        BallStateMachine _sm;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PhysicsLabController>();
            _sm = controller?.BallSM;
            if (_sm != null) _sm.OnShotComplete += HandleShotComplete;
            // Widget starts hidden.
            if (widget != null) widget.Hide();
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;
        }

        void HandleShotComplete(ShotResult result)
        {
            if (result.TerminalState != BallState.InCup) return;
            if (widget == null)
            {
                Debug.LogWarning("[HoleCompleteDriver] InCup fired but widget reference is null.");
                return;
            }

            int strokes = GameSession.TurnCount;       // shot just completed; not yet incremented
            int par     = HoleContext.Par;
            int score   = strokes - par;
            string label = ScoreLabelFor(score);

            widget.Show(strokes, par, score, label, OnModalClose);
        }

        // Called by widget when any of Play/Replay/Retry is tapped.
        void OnModalClose()
        {
            if (widget != null) widget.Hide();
            // §2d minimum-viable: all three buttons just re-arm. Real flows in §2e.
            controller?.RearmAfterHoleComplete();
        }

        public static string ScoreLabelFor(int score)
        {
            switch (score)
            {
                case -3: return "Albatross";
                case -2: return "Eagle";
                case -1: return "Birdie";
                case  0: return "Par";
                case  1: return "Bogey";
                case  2: return "Double Bogey";
                case  3: return "Triple Bogey";
                default: return score < 0 ? $"{score}" : $"+{score}";
            }
        }

        // EditMode-test injection helper (mirrors §2b/§2c pattern).
        internal void InjectForTests(BallStateMachine sm, Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget w)
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;
            _sm = sm;
            widget = w;
            if (_sm != null) _sm.OnShotComplete += HandleShotComplete;
        }
    }
}
```

### E. New `HoleCompleteWidget` UI component

**Location:** `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs`. Namespace `Golfin.Gameplay.UI.ShotUI`.

> **PENDING Figma extract** for canonical layout. Once Cesar confirms node `12987-4556` is canonical (Lock Q1), Architect runs `Figma:get_design_context` and fills exact RectTransform values, anchors, font sizes, padding, and color tokens here. Until then, the skeleton below uses placeholder values; implementer should NOT trust the layout numbers in this draft and should wait for the post-extract update.

**Skeleton (placeholder layout values):**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// §2d Result Screen modal. Hidden by default. Shown on hole-complete with
    /// strokes / par / score / score-label readout and three action buttons
    /// (Play, Replay, Retry — all behave identically in §2d, route to §2e+).
    /// </summary>
    public class HoleCompleteWidget : MonoBehaviour
    {
        [Header("Root (toggled active by Show/Hide)")]
        [SerializeField] GameObject _root;

        [Header("Readout")]
        [SerializeField] TMP_Text   _strokesText;
        [SerializeField] TMP_Text   _parText;
        [SerializeField] TMP_Text   _scoreLabelText;     // "Birdie" / "Par" / etc.
        [SerializeField] TMP_Text   _scoreNumericText;   // "-1" / "E" / "+2"
        [SerializeField] Image      _checkIcon;          // Icon - Check.png

        [Header("Backgrounds")]
        [SerializeField] Image      _holeCardBg;         // Background - HoleCard.png
        [SerializeField] Image      _bannerBg;           // Background - Banner.png

        [Header("Buttons")]
        [SerializeField] Button     _playButton;
        [SerializeField] Button     _replayButton;
        [SerializeField] Button     _retryButton;

        Action _closeCallback;

        void Awake()
        {
            if (_playButton   != null) _playButton.onClick.AddListener(OnAnyButtonTap);
            if (_replayButton != null) _replayButton.onClick.AddListener(OnAnyButtonTap);
            if (_retryButton  != null) _retryButton.onClick.AddListener(OnAnyButtonTap);
            if (_root != null) _root.SetActive(false);
        }

        public void Show(int strokes, int par, int score, string scoreLabel, Action onClose)
        {
            if (_root != null) _root.SetActive(true);
            if (_strokesText      != null) _strokesText.text      = strokes.ToString();
            if (_parText          != null) _parText.text          = $"PAR {par}";
            if (_scoreLabelText   != null) _scoreLabelText.text   = scoreLabel.ToUpperInvariant();
            if (_scoreNumericText != null) _scoreNumericText.text = score == 0 ? "E" : (score > 0 ? $"+{score}" : score.ToString());
            _closeCallback = onClose;
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _closeCallback = null;
        }

        public bool IsShowing => _root != null && _root.activeSelf;

        void OnAnyButtonTap()
        {
            _closeCallback?.Invoke();
        }
    }
}
```

### F. Inspector wiring (LabScaffold.unity)

- Add a new GameObject `HoleCompleteWidget` under the existing UI canvas (sibling of PlayerCard / HoleCard widgets). Build the visual hierarchy per Figma extract: root panel, banner image, hole-card background image, strokes/par/score text fields, three button GOs. Use Unity Editor MCP `gameobject-create` + `gameobject-component-add` only — NO raw YAML edits.
- Attach `HoleCompleteWidget` script to that root GO. Wire all `[SerializeField]` references.
- Add `HoleCompleteDriver` component to the same GameObject as `LoopCameraDirector` and `HoleSessionDriver` (the lab's "drivers" GO). Wire `controller` and `widget` references.
- Save scene via `scene-save` MCP.

> **Implementer must wait for the Figma-extract patch before building the visual hierarchy.** The Architect will publish a follow-up SPEC patch with exact RectTransform values, anchors, font assets, font sizes, color hexes, padding, and child layout once Cesar confirms canonicality.

## Tests

**Location:** `Assets/Scripts/Physics/Tests/RealCupDetectorTests.cs` and `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs` (new files). Asmdef: `Golfin.Physics.Tests` (existing).

### Required tests (minimum 8)

**RealCupDetectorTests.cs (5 tests):**

1. **`RealCupDetector_BallInsideCup_ReturnsTrue`** — pin at (0,0,0), ball at (0, -0.01, 0) with ballRadius=0.021 (regulation), assert true.
2. **`RealCupDetector_BallOutsideCupRadius_ReturnsFalse`** — pin at (0,0,0), ball at (0.1, 0, 0) (well outside 5.4cm radius), assert false.
3. **`RealCupDetector_BallAboveCup_ReturnsFalse`** — pin at (0,0,0), ball at (0, 5, 0) (XZ inside, Y way above pin), assert false (height gate filters).
4. **`RealCupDetector_BallAtCupEdge_ConsidersBallRadius`** — pin at (0,0,0), ball at exact effective edge (XZ distance == cupRadius - ballRadius), assert false (strictly less-than test). Ball at edge - 0.001m, assert true.
5. **`RealCupDetector_BallLargerThanCup_AlwaysReturnsFalse`** — pin at (0,0,0), ballRadius=0.1m (larger than 0.054m cup), assert false at any position.

**HoleCompleteDriverTests.cs (3 tests):**

6. **`HoleCompleteDriver_OnInCupTerminal_ShowsModalWithStrokeCount`** — set `GameSession.TurnCount = 3`, `HoleContext.Par = 4`; fake-fire `OnShotComplete` with `TerminalState = InCup`; assert `widget.IsShowing == true` AND `_strokesText.text == "3"` AND `_scoreLabelText.text == "BIRDIE"`.
7. **`HoleCompleteDriver_OnAtRestTerminal_DoesNotShowModal`** — fake-fire `OnShotComplete` with `TerminalState = AtRest`; assert `widget.IsShowing == false`.
8. **`HoleCompleteDriver_OnOBTerminal_DoesNotShowModal`** — fake-fire `OnShotComplete` with `TerminalState = OB, OBReason = Water`; assert `widget.IsShowing == false`.

**Optional bonus test (nice-to-have, not gating):**

9. `ScoreLabelFor_KnownValues` — table-driven check of the `ScoreLabelFor` static for scores -3..+5 returning expected labels.

**Test isolation note:** static buses are global; tests MUST call `GameSession.ResetForNewHole()` and `HoleContext.Reset()` in `[SetUp]` to avoid order-dependent failures. Document in each test file's class header.

**Test seam for HoleCompleteDriver:** use `InjectForTests(BallStateMachine sm, HoleCompleteWidget w)` to bypass the `GetComponentInParent<PhysicsLabController>()` lookup. Tests construct a real `BallStateMachine` (it has no Unity dependencies beyond an `ISurfaceProvider`, easily faked) and a bare `GameObject` with `HoleCompleteWidget` attached.

**Test gate:** **N → N+8 PASS, 0 IGNORED** where N is the current baseline. Implementer runs `Run All Tests` first, records actual N in IMPLEMENTER_REPORT, then adds 8 new tests for N+8 target. Same hard rule as §2c: any pre-existing test starts failing → escalate `IMPLEMENTER_BLOCKED` BEFORE adding new tests.

## Smoke evidence

Per controls_g_smoke_followup precedent + Lesson O. Use `CaptureCore.SnapWhenStateReached` and the `FakeReset` / `FakeMidAim` capture-helper presets.

**Three captures + one log artifact:**

1. **`controls_2d_modal_hidden_aiming.png`** — fresh hole loaded (Hole_01 additively), at first BallState.Aiming. Verify HoleCompleteWidget GO active=false (modal hidden). PlayerCard reads "TURN 1".
2. **`controls_2d_modal_visible_after_holeout.png`** — fire a putt close to the cup that holes out. State-gate the capture on `BallStateMachine.OnShotComplete(InCup)` + 1-frame settle. Verify modal visible, strokes text reads correct value (e.g., "1" for tee-in-one), score label correct (e.g., "EAGLE" for 1 on par 3). MANDATORY play-and-confirm sentence in IMPLEMENTER_REPORT describing what the modal visually shows (Lesson O dispatch-vs-visual).
3. **`controls_2d_modal_dismissed_aiming.png`** — after capture 2, simulate a button tap (any of Play/Replay/Retry); state-gate capture on next BallState.Aiming. Verify modal hidden, ball back at last resting position (the cup).
4. **`controls_2d_holeout_log.txt`** — text dump of `GameSession.ShotHistory` immediately after capture 2. Should show entries with terminal=InCup for the holed shot.

**Visual-fidelity verification (Lesson O):** the modal contents are visual-fidelity-critical. IMPLEMENTER_REPORT must include a content-sanity description of capture 2 — what the strokes / par / score numbers actually look like, what label is displayed, which buttons are visible, whether the layout matches the Figma reference PNGs. Mode-history alone is insufficient for this task.

**Filed under** `Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/screenshots/` with `controls_2d_*` prefix.

## Definition of Done

- `RealCupDetector` shipped under `Golfin.Gameplay.Loop` with regulation defaults + height-gated XZ math.
- `PhysicsLabController.OnHoleLoaded` installs `RealCupDetector` after `GameSession.ResetForNewHole()`. `OnHoleUnloaded` reverts to `NullCupDetector`.
- `PhysicsLabController.HandleShotComplete` gates re-arm: AtRest/OB → re-arm; InCup → defer to driver. New internal `RearmAfterHoleComplete()` accessor added for the driver.
- `HoleCompleteDriver` shipped + Inspector-wired in LabScaffold.unity (via Unity Editor MCP, NOT raw YAML).
- `HoleCompleteWidget` shipped + Inspector-wired with all 7 imported PNGs and TMP_Text references. UI hierarchy matches Figma node `12987-4556` (extracted in post-confirmation pass).
- 8 new EditMode tests, all PASS. Test gate: N+8 PASS, 0 IGNORED.
- 3 captures + 1 log file filed under spec's `screenshots/` folder.
- Manual play-and-confirm: load Hole_01, putt into cup at close range (use `PlaceBallAt` if needed to drop ball ~30cm from pin), verify modal appears with correct strokes/par/score-label, tap any button, verify modal hides and SM is back in Aiming.
- IMPLEMENTER_REPORT includes content-sanity description of the modal visual per Lesson O.

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** if:
  - `LabScaffold.unity` Editor MCP wiring of `HoleCompleteDriver` or `HoleCompleteWidget` GO/components fails. Implementer tries 2 retries before escalating.
  - PASS gate breaks unexpectedly. Most likely root cause: the `HandleShotComplete` re-arm gate change cascading into integration tests that assumed unconditional re-arm. Architect investigates.
  - `fp` arithmetic operators in the spec snippet don't compile (e.g. `fp` doesn't expose `<` or `*` directly). Implementer rewrites the math via `fp.FromFloat` / `.ToFloat` round-trips — public API of `RealCupDetector` MUST stay identical.
  - Cup detection never fires in the smoke putt (e.g., putt always rolls past the cup, or always stops short). Architect investigates whether the height gate is too tight, the cup radius too small for sim resolution, or the putt parameters need adjustment.
- **`IMPLEMENTER_PARTIAL`** acceptable if:
  - All code + 7 of 8 unit tests + first 2 captures land clean, but capture 3 (modal-dismissed) hits friction (button-click simulation in EditMode is awkward without a real EventSystem). Acceptable to ship 2/3 captures + a manual play-and-confirm description for the dismissal step.

## Out of scope

- **Failed-state UI / logic.** Lock Q2 = NO. Imported `Icon - X.png` + Failed reference PNGs are not used in §2d.
- **Real button → action flows.** Lock Q5 = all three buttons just close + re-arm. Play=next-hole, Retry=re-tee, Replay=shot-history wiring lives in §2e+.
- **`HoleSessionDriver` turn-advance gate on InCup.** Lock Q7 = leave as-is. Cosmetic follow-up.
- **Penalty-stroke math.** OB still re-arms immediately per existing behavior; the +1 penalty convention is §2e/Loop v2.
- **Hole-cycling logic** (advancing to next hole on Play). §2e / Loop v2.
- **Result screen animation / transitions.** §2d ships a hard show/hide; bezier-curve fade-ins, particle effects, score-label pop animations all deferred to UI polish phase.
- **Localization** (JP/EN). Strings hardcoded English in §2d. Loc pass is a separate phase.
- **Score persistence across runs.** GameSession is in-memory only; persistence is Loop v2 / save state spec.
- **Multi-character / multi-player score reconciliation.** §2d shows one player's modal.

## Hard rules for implementer

1. **Do NOT modify** `BallStateMachine.cs`, `BallState.cs`, `ShotResult.cs`, `BallStateChange.cs`, `BallSimulation.cs`, `Trajectory.cs`, `ICupDetector.cs`, `NullCupDetector.cs`, `HoleContext.cs`, `GameSession.cs`, `HoleSessionDriver.cs`, `LoopCameraDirector.cs`, `PlayerCardWidget.cs`, any aero CSV, any test currently in PASS state outside the two new test files.
2. **Do NOT modify `LabScaffold.unity` via raw YAML.** Use Unity Editor APIs (`gameobject-create`, `gameobject-component-add`, `gameobject-component-modify`, `scene-save` MCP tools).
3. **Do NOT use `WaitForSeconds(N)` for state-dependent captures.** State-gate via `SnapWhenStateReached` per controls_g_smoke_followup precedent.
4. **Do NOT implement Failed-state UI or trigger logic in §2d.** Lock Q2 = explicit NO.
5. **Do NOT pre-bake button → action flows beyond "close modal + re-arm".** Lock Q5.
6. **Do NOT proliferate static-bus files.** Read from existing `GameSession` and `HoleContext`; do not introduce new statics.
7. **Smoke evidence per Lesson O:** dispatch capture (state-gated screenshot) + content-sanity description in IMPLEMENTER_REPORT (what the modal visually shows). Both are required.
8. **Bit-exact pre-existing test gate must hold.** Adding 8 tests to baseline N → N+8. If any pre-existing test starts failing, escalate `IMPLEMENTER_BLOCKED` immediately — do NOT "fix" by editing existing tests.
9. **Wait for Figma-extract patch before building UI hierarchy.** The skeleton in §E uses placeholder values. Architect will deliver exact RectTransform/font/color values once Cesar confirms node `12987-4556` is canonical. Implementer can ship A/B/C/D and the test stubs first, then loop back for E/F after the patch.
