# SPEC — `loop_v1_2d_hole_complete_and_result_screen`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY 2026-05-09 07:15 JST after Cesar confirmed Q1–Q8.

## Goal

Ship a real `ICupDetector` implementation, wire it into `PhysicsLabController.OnHoleLoaded`, gate the existing `HandleShotComplete` re-arm on `AtRest`/`OB` only, and add a full **Result Screen** matching the Figma design (4 frame variants, 3 functional visual states). The screen renders Card 1 (just-completed hole) and Card 2 (next hole). All real data we have is bound; everything else is placeholder. Top bar and bottom nav are excluded from LabScaffold per Q3 but the hierarchy is structured so they slot in cleanly later.

A debug button on `DebugShotPanel` triggers the result screen with current `GameSession.TurnCount` strokes.

## Reference

### Figma frames (canonical, confirmed Q1)

File: `5gEAHjl6xAtW8iYY7NMvWd` ("Golfin Game Redux", paid plan).

| Node | URL suffix | Variant |
|---|---|---|
| `12988-5223` | `?node-id=12988-5223` | Results — Success, REPLAY, NEXT unlocked. STROKES tied BEST (5 PAR / BEST 5 PAR). |
| `12988-4902` | `?node-id=12988-4902` | Results — Success, REPLAY, NEXT unlocked. STROKES worse than BEST (5 PAR / BEST 4 EAGLE). Same layout as 5223. |
| `12988-5466` | `?node-id=12988-5466` | Results — Failed, **no PB**, RETRY, NEXT LOCKED. STROKES = BEST (6 BOGEY both lines). Rewards row dimmed. |
| `12987-4316` | `?node-id=12987-4316` | Results — Failed, **has PB**, REPLAY (silver), NEXT unlocked. STROKES 6 BOGEY, BEST 4 EAGLE. |

**The two Success frames are functionally one state**; they differ only in BEST data. **The Figma PB-gating logic** is what makes Card 1 button / Card 2 lock state branch on Failed.

### Functional visual states

§2d ships THREE states:
1. **`SUCCESS`** — score ≤ 0. Green ✓ + "SUCCESS" header, green STROKES color, REPLAY button, Card 2 unlocked.
2. **`FAILED_NO_PB`** — score > 0 AND no personal best on this hole. Red ✗ + "FAILED" header (orange gradient), red STROKES color, RETRY button (gold), Card 2 LOCKED (grey "🔒 LOCKED" + darken overlay, rewards dimmed at 50% opacity, no PLAY button).
3. **`FAILED_HAS_PB`** — score > 0 AND personal best exists. Same FAILED header + red STROKES, but REPLAY button (silver, like Success), Card 2 unlocked.

§2d default: **NO PB tracking exists yet**, so the runtime-default for Failed is `FAILED_NO_PB` (Lock Q8). The widget API exposes a `bool hasPersonalBest` parameter so the §2e/save-layer pass can pivot to `FAILED_HAS_PB` without code surgery.

### Reference PNGs (already in repo)

Visual-diff companions: `Docs/Reference/Results Screen/Results - {Success,Failed} (Replay){,-1}.png`.

### Imported PNG assets (already in repo)

`Assets/Art/ResultScreen/`:
- `Background - Banner.png` — for the "BOGEY" mid-game banner overlay (NOT used in §2d's result screen). Belongs to a separate banner-flash UI.
- `Background - HoleCard.png` — large card background (gradient, rounded). Used on both Card 1 and Card 2.
- `Button - Play.png` (gold) — Card 2 PLAY button background.
- `Button - Replay.png` (silver) — Card 1 REPLAY button background.
- `Button - Retry.png` (gold) — Card 1 RETRY button background (Failed-no-PB state).
- `Icon - Check.png` — green ✓ for Success header.
- `Icon - X.png` — red ✗ for Failed header.

### Companion file

`FIGMA_EXTRACT.md` (sibling of this SPEC) holds the React/Tailwind extracts from all 4 nodes verbatim, for implementer reference of exact px values, colors, font weights, and layout details. Implementer reads FIGMA_EXTRACT.md as truth-source for all dimensions; this SPEC summarizes the salient patterns.

## Background — what exists today

Verified by code walk 2026-05-09 at session start.

| File | Role for §2d |
|---|---|
| `Assets/Scripts/Gameplay/Loop/ICupDetector.cs` | Interface `bool IsInCup(fp3 position, fp ballRadius)`. Implement `RealCupDetector`. |
| `Assets/Scripts/Gameplay/Loop/NullCupDetector.cs` | Default. No change. |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | `SetCupDetector(ICupDetector)` exists. Cup scan in `OnTrajectoryComputed` (lines 166–211, `default:` branch) iterates ALL trajectory.samples and returns first `IsInCup==true` as `BallState.InCup`. No change. |
| `Assets/Scripts/Gameplay/Loop/ShotResult.cs` | Carries `TerminalState`, `EndPosition`. No change. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` | Has `HoleNumber`, `Par`, `CourseName` ("LOMOND"), `TeeName` ("REGULAR"), `PinWorld` Vector3. Source for §2d's data binding. No change. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` | §2c. `TurnCount`, `ShotHistory`, `ResetForNewHole()`. Source for stroke count. No change. |
| `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` | Tapping the central ball (in Idle state) calls `_debugPanel.Toggle()`. No change. |
| `Assets/Scripts/Gameplay/UI/ShotUI/DebugShotPanel.cs` | Has Toggle()/SelectAccuracy/OnShoot. **Add a "Hole Out" button** here per Lock §H. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::OnHoleLoaded` | After `HoleContext.Raise()` and `GameSession.ResetForNewHole()` (around line 1442), add `_ballSM?.SetCupDetector(new RealCupDetector(HoleContext.PinWorld))`. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::OnHoleUnloaded` | After `GameSession.ResetForNewHole()`, revert to `NullCupDetector`. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::HandleShotComplete` | Currently always calls `_shotController?.CompleteShot(); _ballSM.ReArm();`. Add gate so InCup skips re-arm; modal owns it via new `internal void RearmAfterHoleComplete()`. |

**Factual correction vs KICKOFF_TOMORROW.md:** the cup scan runs over **all** trajectory samples in `OnTrajectoryComputed` (Flying + Rolling alike), not in a per-Rolling-tick loop. `RealCupDetector` MUST guard against high-flying samples that happen to be over the cup XZ — see Lock Q4.

## Locked decisions

All confirmed by Cesar in chat 2026-05-09:

- **Q1** ✅ Figma nodes 12988-5223, 12988-4902, 12988-5466, 12987-4316 are canonical. Architect extracted all 4. See FIGMA_EXTRACT.md.
- **Q2** ✅ Failed = `score > 0` (any over-par). Future amendment will distinguish bogey/double-bogey for "pass with lesser rewards" — out of §2d scope; tagged for Loop v2.
- **Q3** ✅ `RealCupDetector(Vector3 pin)` constructor-injects pin. No static read at scan time.
- **Q4** ✅ XZ-and-height guarded detection. `IsInCup = (XZdist² < (cupRadius − ballRadius)²) AND (sample.y ≤ pin.y + ballRadius)`. First sample wins.
- **Q5** ✅ All §2d buttons just close modal + call `RearmAfterHoleComplete()`. Real flows (Play=next-hole, Retry=re-tee, Replay=shot-history) belong to §2e+.
- **Q6** ✅ `HandleShotComplete` re-arm only on AtRest/OB. On InCup, modal owns re-arm. Add `internal void RearmAfterHoleComplete()`.
- **Q7** ✅ `HoleSessionDriver` turn-advance left as-is for §2d. Cosmetic-pass TODO logged: gate the 1.5s coroutine on `result.TerminalState == AtRest` so InCup/OB don't tick TURN.
- **Q8** ✅ No-PB default for §2d. Widget API exposes `bool hasPersonalBest` (default false). Failed → `FAILED_NO_PB` state until §2e/save-layer wires real PB.

Cosmetic-pass follow-up tracked separately:
- Gate `HoleSessionDriver.AdvanceTurnAfterDelay()` coroutine on AtRest only.

## Architecture context

- **Asmdef boundaries affected:**
  - `Golfin.Gameplay.Loop` — adds `RealCupDetector.cs`.
  - `Golfin.Physics.Viewer` — adds `HoleCompleteDriver.cs`; modifies `PhysicsLabController.cs` (~10 lines: SetCupDetector wiring at 2 sites, HandleShotComplete gate, new internal accessor).
  - `Golfin.Gameplay.UI.ShotUI` — adds `HoleCompleteWidget.cs` and `HoleCompleteCardWidget.cs` (sub-component for one card); modifies `DebugShotPanel.cs` (adds 1 SerializeField + 1 onClick + 1 handler).
  - `Golfin.Physics.Tests` — adds `RealCupDetectorTests.cs` and `HoleCompleteDriverTests.cs`.
- **No changes to** `BallStateMachine.cs`, `BallState.cs`, `ShotResult.cs`, `BallStateChange.cs`, `BallSimulation.cs`, `Trajectory.cs`, `ICupDetector.cs`, `NullCupDetector.cs`, `HoleContext.cs`, `GameSession.cs`, `HoleSessionDriver.cs`, `LoopCameraDirector.cs`, `PlayerCardWidget.cs`, `CentralBallWidget.cs`, any aero CSV.
- **Card 1 ↔ Card 2 split as separate prefabs** (`HoleCompleteCardWidget`) so Card 2 can later be reused for a "preview-next-hole" mid-game widget.
- **Static-bus reads, not writes:** Driver reads `GameSession.TurnCount` and `HoleContext.{HoleNumber,Par,CourseName,TeeName}`. No writes.

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
    /// load; pin position captured at construction and not re-read.
    /// Determinism rules: pure fp math, no Unity API calls, no Time/Random.
    /// </summary>
    public sealed class RealCupDetector : ICupDetector
    {
        // Regulation cup mouth: 4.25 inch diameter → 0.054 m radius.
        public static readonly fp DefaultCupRadius = fp.FromFloat(0.054f);

        readonly fp3 _pin;
        readonly fp  _cupRadius;

        public RealCupDetector(Vector3 pin) : this(pin, DefaultCupRadius) { }

        public RealCupDetector(Vector3 pin, fp cupRadius)
        {
            _pin = new fp3(fp.FromFloat(pin.x), fp.FromFloat(pin.y), fp.FromFloat(pin.z));
            _cupRadius = cupRadius;
        }

        public bool IsInCup(fp3 position, fp ballRadius)
        {
            // Height gate: ball top must be at or below pin Y (filters flying samples
            // that happen to be over the cup XZ).
            if (position.y > _pin.y + ballRadius) return false;

            fp dx = position.x - _pin.x;
            fp dz = position.z - _pin.z;
            fp distSq = dx * dx + dz * dz;
            fp effRadius = _cupRadius - ballRadius;
            if (effRadius <= fp.Zero) return false; // ball larger than cup
            return distSq < effRadius * effRadius;
        }

        // Test seam.
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

> **Implementer note:** if `fp` operators (`<`, `*`, `-`) don't compile directly, fall back to `fp.FromFloat`/`.ToFloat` round-trips per `BallStateMachine.cs` patterns. Public API stays identical.

### B. Wire `SetCupDetector` in `PhysicsLabController`

**`OnHoleLoaded`** — inside the `if (meta != null)` branch, immediately after the existing `GameSession.ResetForNewHole()` call:

```csharp
// §2d: install a real cup detector keyed to this hole's pin position.
if (_ballSM != null)
{
    _ballSM.SetCupDetector(
        new Golfin.Gameplay.Loop.RealCupDetector(
            Golfin.Gameplay.UI.HUD.HoleContext.PinWorld));
    Debug.Log($"[PhysicsLab][§2d] RealCupDetector installed at pin={Golfin.Gameplay.UI.HUD.HoleContext.PinWorld:F3}");
}
```

**`OnHoleUnloaded`** — after the existing `GameSession.ResetForNewHole()`:

```csharp
// §2d: revert to NullCupDetector for flat-ground fallback.
if (_ballSM != null)
    _ballSM.SetCupDetector(new Golfin.Gameplay.Loop.NullCupDetector());
```

### C. Gate `HandleShotComplete` re-arm + add re-arm accessor

Replace the existing `_shotController?.CompleteShot(); _ballSM.ReArm();` block at the bottom of `HandleShotComplete` with:

```csharp
// §2d: re-arm only on AtRest/OB. InCup → HoleCompleteDriver owns re-arm via modal close.
if (result.TerminalState == Golfin.Gameplay.Loop.BallState.AtRest
    || result.TerminalState == Golfin.Gameplay.Loop.BallState.OB)
{
    _shotController?.CompleteShot();
    _ballSM.ReArm();
}
// else InCup: see RearmAfterHoleComplete().
```

Update the docstring: "...re-arms the shot controller on AtRest/OB; on InCup, re-arm is deferred to HoleCompleteDriver's modal close (§2d)."

Add new internal accessor (below `HandleShotComplete`):

```csharp
// §2d: invoked by HoleCompleteDriver after the modal is dismissed.
internal void RearmAfterHoleComplete()
{
    _shotController?.CompleteShot();
    _ballSM?.ReArm();
}
```

### D. New `HoleCompleteDriver` MonoBehaviour

**Location:** `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs`. Namespace `Golfin.Physics.Viewer`. Mirrors `HoleSessionDriver` (§2c).

```csharp
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2d: subscribes to BallStateMachine.OnShotComplete; on terminal=InCup,
    /// reads strokes/par/course/hole context, computes score, and shows the
    /// HoleCompleteWidget Result Screen. The widget's button handlers call back
    /// via PhysicsLabController.RearmAfterHoleComplete() when dismissed.
    ///
    /// Also exposes ShowForDebug() for the DebugShotPanel "Hole Out" button.
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
            if (widget != null) widget.Hide();
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;
        }

        void HandleShotComplete(ShotResult result)
        {
            if (result.TerminalState != BallState.InCup) return;
            ShowResultScreen(GameSession.TurnCount);
        }

        // Public entrypoint — used by both the real InCup path and DebugShotPanel.
        public void ShowForDebug()
        {
            ShowResultScreen(GameSession.TurnCount > 0 ? GameSession.TurnCount : 1);
        }

        void ShowResultScreen(int strokes)
        {
            if (widget == null)
            {
                Debug.LogWarning("[HoleCompleteDriver] InCup fired but widget reference is null.");
                return;
            }

            int par = HoleContext.Par;
            int score = strokes - par;
            string scoreLabel = ScoreLabelFor(score);
            bool isFailed = score > 0;
            bool hasPersonalBest = false; // Q8 lock — no save layer in §2d.

            var data = new HoleCompleteData(
                strokes:          strokes,
                par:              par,
                score:            score,
                scoreLabel:       scoreLabel,
                isFailed:         isFailed,
                hasPersonalBest:  hasPersonalBest,
                courseName:       HoleContext.CourseName,
                holeNumber:       HoleContext.HoleNumber,
                teeName:          HoleContext.TeeName,
                // Placeholders (Q8, no PB / no time tracking / no rewards economy):
                bestStrokes:      "—",
                bestStrokesLabel: "",
                timeStr:          "00:00:00",
                bestTimeStr:      "—",
                rewardCoinX:      10,
                rewardRepairX:    10,
                rewardBallX:      10,
                nextHoleNumber:   HoleContext.HoleNumber + 1,
                nextHolePar:      0, // unknown in §2d
                nextHoleTipText:  "Next hole tip — TBD"
            );

            widget.Show(data, OnModalClose);
        }

        void OnModalClose()
        {
            if (widget != null) widget.Hide();
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

        // EditMode-test injection helper.
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

### E. New `HoleCompleteWidget` + `HoleCompleteCardWidget` + `HoleCompleteData`

**Location:**
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` — top-level container, owns dim background + 2 cards.
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteCardWidget.cs` — single card prefab (used twice: Card 1 = current hole, Card 2 = next hole).
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteData.cs` — readonly struct payload.

**`HoleCompleteData` (struct):**

```csharp
using System;

namespace Golfin.Gameplay.UI.ShotUI
{
    public readonly struct HoleCompleteData
    {
        // ── Card 1 (current hole, real data) ──────────────────────────────
        public readonly int    Strokes;          // GameSession.TurnCount
        public readonly int    Par;              // HoleContext.Par
        public readonly int    Score;            // strokes - par
        public readonly string ScoreLabel;       // "Birdie" / "Par" / "Bogey" / etc
        public readonly bool   IsFailed;         // score > 0
        public readonly bool   HasPersonalBest;  // Q8: false in §2d
        public readonly string CourseName;       // HoleContext.CourseName ("LOMOND")
        public readonly int    HoleNumber;       // HoleContext.HoleNumber
        public readonly string TeeName;          // HoleContext.TeeName ("REGULAR")

        // ── Card 1 placeholders (Q8: no PB / no time tracking) ────────────
        public readonly string BestStrokes;       // "—"
        public readonly string BestStrokesLabel;  // ""
        public readonly string TimeStr;           // "00:00:00"
        public readonly string BestTimeStr;       // "—"

        // ── Rewards row (placeholder hardcoded x10) ───────────────────────
        public readonly int RewardCoinX;
        public readonly int RewardRepairX;
        public readonly int RewardBallX;

        // ── Card 2 (next hole, placeholder) ───────────────────────────────
        public readonly int    NextHoleNumber;
        public readonly int    NextHolePar;       // 0 = unknown
        public readonly string NextHoleTipText;   // placeholder

        public HoleCompleteData(
            int strokes, int par, int score, string scoreLabel,
            bool isFailed, bool hasPersonalBest,
            string courseName, int holeNumber, string teeName,
            string bestStrokes, string bestStrokesLabel,
            string timeStr, string bestTimeStr,
            int rewardCoinX, int rewardRepairX, int rewardBallX,
            int nextHoleNumber, int nextHolePar, string nextHoleTipText)
        {
            Strokes          = strokes;
            Par              = par;
            Score            = score;
            ScoreLabel       = scoreLabel;
            IsFailed         = isFailed;
            HasPersonalBest  = hasPersonalBest;
            CourseName       = courseName;
            HoleNumber       = holeNumber;
            TeeName          = teeName;
            BestStrokes      = bestStrokes;
            BestStrokesLabel = bestStrokesLabel;
            TimeStr          = timeStr;
            BestTimeStr      = bestTimeStr;
            RewardCoinX      = rewardCoinX;
            RewardRepairX    = rewardRepairX;
            RewardBallX      = rewardBallX;
            NextHoleNumber   = nextHoleNumber;
            NextHolePar      = nextHolePar;
            NextHoleTipText  = nextHoleTipText;
        }
    }
}
```

**`HoleCompleteWidget` (top-level container):**

```csharp
using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// §2d Result Screen. Hidden by default. Shown on hole-complete with
    /// stacked Card 1 (current hole) + Card 2 (next hole).
    ///
    /// Top bar (RP counter, settings, RESULTS title, rankings) and bottom nav
    /// are NOT included in the LabScaffold variant per Q3 — they slot in as
    /// siblings of `_root` in the full implementation. The widget's RectTransform
    /// is sized to fill the viewport between top bar and nav bar regions.
    /// </summary>
    public class HoleCompleteWidget : MonoBehaviour
    {
        [Header("Root (SetActive on Show/Hide)")]
        [SerializeField] GameObject _root;

        [Header("Dim background overlay")]
        [SerializeField] UnityEngine.UI.Image _dimBackground;

        [Header("Cards")]
        [SerializeField] HoleCompleteCardWidget _card1; // current-hole card
        [SerializeField] HoleCompleteCardWidget _card2; // next-hole card

        Action _closeCallback;

        void Awake()
        {
            if (_root != null) _root.SetActive(false);
        }

        public bool IsShowing => _root != null && _root.activeSelf;

        public void Show(HoleCompleteData data, Action onClose)
        {
            if (_root != null) _root.SetActive(true);

            // Card 1 → current-hole variant
            if (_card1 != null)
                _card1.BindCurrentHole(data, OnAnyButtonTap);

            // Card 2 → next-hole variant. Locked when failed-no-PB.
            bool card2Locked = data.IsFailed && !data.HasPersonalBest;
            if (_card2 != null)
                _card2.BindNextHole(data, card2Locked, OnAnyButtonTap);

            _closeCallback = onClose;
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _closeCallback = null;
        }

        void OnAnyButtonTap()
        {
            _closeCallback?.Invoke();
        }
    }
}
```

**`HoleCompleteCardWidget` (single card, two binding modes):**

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// §2d: one card in the Result Screen. Two binding modes:
    ///   - BindCurrentHole(): renders the SUCCESS/FAILED header, stats block,
    ///     rewards row, and the Replay/Retry button.
    ///   - BindNextHole(): renders the NEXT/LOCKED header, optional tip text,
    ///     rewards row, and the Play button (or hidden if locked).
    /// </summary>
    public class HoleCompleteCardWidget : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] GameObject _successHeaderRoot;  // ✓ + SUCCESS
        [SerializeField] GameObject _failedHeaderRoot;   // ✗ + FAILED
        [SerializeField] GameObject _nextHeaderRoot;     // NEXT (gold)
        [SerializeField] GameObject _lockedHeaderRoot;   // 🔒 + LOCKED

        [Header("Subhead")]
        [SerializeField] TMP_Text _subheadText;          // "Lomond Country Club  - Hole 6 - Par 5"

        [Header("Body — Current Hole")]
        [SerializeField] GameObject _currentBodyRoot;
        [SerializeField] Image    _holeThumbnailSmall;   // green tile (placeholder OK)
        [SerializeField] Image    _holeMapLarge;         // map graphic (placeholder OK)
        [SerializeField] TMP_Text _statsBlockText;       // multi-line stats

        [Header("Body — Next Hole")]
        [SerializeField] GameObject _nextBodyRoot;
        [SerializeField] Image    _nextHoleThumbnailSmall;
        [SerializeField] Image    _nextHoleMapLarge;
        [SerializeField] TMP_Text _nextHoleTipText;

        [Header("Rewards row")]
        [SerializeField] CanvasGroup _rewardsCanvasGroup; // dim via .alpha=0.5 when locked
        [SerializeField] TMP_Text    _rewardCoinText;     // "x10"
        [SerializeField] TMP_Text    _rewardRepairText;
        [SerializeField] TMP_Text    _rewardBallText;

        [Header("Buttons (Card 1 only — one is shown at a time)")]
        [SerializeField] Button _replayButton; // silver → current hole, success or failed-with-PB
        [SerializeField] Button _retryButton;  // gold   → current hole, failed-no-PB

        [Header("Buttons (Card 2 only)")]
        [SerializeField] Button _playButton;   // gold   → next hole, when unlocked

        [Header("Locked overlay")]
        [SerializeField] GameObject _darkenOverlay;       // shown only when locked

        Action _onButtonTap;

        public void BindCurrentHole(HoleCompleteData data, Action onButtonTap)
        {
            _onButtonTap = onButtonTap;

            // Header
            SetActive(_successHeaderRoot, !data.IsFailed);
            SetActive(_failedHeaderRoot,   data.IsFailed);
            SetActive(_nextHeaderRoot,     false);
            SetActive(_lockedHeaderRoot,   false);

            // Subhead
            if (_subheadText != null)
                _subheadText.text = $"{ToTitleCase(data.CourseName)} Country Club  - Hole {data.HoleNumber} - Par {data.Par}";

            // Body
            SetActive(_currentBodyRoot, true);
            SetActive(_nextBodyRoot,    false);
            if (_statsBlockText != null)
                _statsBlockText.text = BuildStatsBlock(data);

            // Rewards
            if (_rewardCoinText   != null) _rewardCoinText.text   = $"x{data.RewardCoinX}";
            if (_rewardRepairText != null) _rewardRepairText.text = $"x{data.RewardRepairX}";
            if (_rewardBallText   != null) _rewardBallText.text   = $"x{data.RewardBallX}";
            if (_rewardsCanvasGroup != null) _rewardsCanvasGroup.alpha = 1f;

            // Buttons — exactly one of REPLAY / RETRY visible.
            // REPLAY: success OR (failed AND has PB)
            // RETRY:  failed AND no PB
            bool useRetry = data.IsFailed && !data.HasPersonalBest;
            SetActive(_replayButton != null ? _replayButton.gameObject : null, !useRetry);
            SetActive(_retryButton  != null ? _retryButton.gameObject  : null,  useRetry);
            SetActive(_playButton   != null ? _playButton.gameObject   : null,  false);

            HookButton(_replayButton);
            HookButton(_retryButton);

            // Card 1 never shows the darken overlay.
            SetActive(_darkenOverlay, false);
        }

        public void BindNextHole(HoleCompleteData data, bool locked, Action onButtonTap)
        {
            _onButtonTap = onButtonTap;

            // Header
            SetActive(_successHeaderRoot, false);
            SetActive(_failedHeaderRoot,  false);
            SetActive(_nextHeaderRoot,   !locked);
            SetActive(_lockedHeaderRoot,  locked);

            // Subhead
            if (_subheadText != null)
            {
                string parPart = data.NextHolePar > 0 ? $" - Par {data.NextHolePar}" : "";
                _subheadText.text = $"{ToTitleCase(data.CourseName)} Country Club  - Hole {data.NextHoleNumber}{parPart}";
            }

            // Body — locked shows only header + rewards (no map, no tip).
            SetActive(_currentBodyRoot, false);
            SetActive(_nextBodyRoot,    !locked);
            if (_nextHoleTipText != null)
                _nextHoleTipText.text = data.NextHoleTipText;

            // Rewards row
            if (_rewardCoinText   != null) _rewardCoinText.text   = $"x{data.RewardCoinX}";
            if (_rewardRepairText != null) _rewardRepairText.text = $"x{data.RewardRepairX}";
            if (_rewardBallText   != null) _rewardBallText.text   = $"x{data.RewardBallX}";
            if (_rewardsCanvasGroup != null) _rewardsCanvasGroup.alpha = locked ? 0.5f : 1f;

            // Buttons
            SetActive(_replayButton != null ? _replayButton.gameObject : null, false);
            SetActive(_retryButton  != null ? _retryButton.gameObject  : null, false);
            SetActive(_playButton   != null ? _playButton.gameObject   : null, !locked);
            HookButton(_playButton);

            // Darken overlay shown only when locked.
            SetActive(_darkenOverlay, locked);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        static string BuildStatsBlock(HoleCompleteData d)
        {
            // Match Figma layout:
            //   TEE OFF: REGULAR
            //   STROKES: 5 (PAR)
            //   BEST: 5 (PAR)
            //   TIME: 00:02:34
            //   BEST: 00:02:34
            string strokesLine = $"<b>STROKES:</b> {d.Strokes} ({d.ScoreLabel.ToUpperInvariant()})";
            string bestLine    = string.IsNullOrEmpty(d.BestStrokesLabel)
                                 ? $"<b>BEST:</b> {d.BestStrokes}"
                                 : $"<b>BEST:</b> {d.BestStrokes} ({d.BestStrokesLabel.ToUpperInvariant()})";
            return string.Join('\n',
                $"<b>TEE OFF:</b> {d.TeeName}",
                strokesLine,
                bestLine,
                $"<b>TIME:</b> {d.TimeStr}",
                $"<b>BEST:</b> {d.BestTimeStr}");
        }

        static string ToTitleCase(string courseName)
        {
            if (string.IsNullOrEmpty(courseName)) return courseName;
            // "LOMOND" → "Lomond"
            return char.ToUpperInvariant(courseName[0]) + courseName.Substring(1).ToLowerInvariant();
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null) go.SetActive(on);
        }

        void HookButton(Button btn)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _onButtonTap?.Invoke());
        }
    }
}
```

> **Color tokens** (from FIGMA_EXTRACT.md, applied via TMP rich-text or inspector colors):
> - Success green: `#50C878` (header text + STROKES color)
> - Failed orange gradient: `#D16A47` → `#C04000` → `#8E2D00` (header text + STROKES color, gradient via TMP color block or pre-tinted asset)
> - Mission gold: `#EEDC9A` (NEXT header)
> - Locked grey: `#C8C8C8` (LOCKED header)
> - Card BG gradient: `#133453` → `#091B33`
> - Card border white: `#FFFFFF` (3px)
> - Card inner border: `#0A1D35` (1px)

### F. Asset strategy

**Use existing PNGs** in `Assets/Art/ResultScreen/`:
- `Background - HoleCard.png` → both Card 1 and Card 2 background.
- `Button - Replay.png` → REPLAY button background.
- `Button - Retry.png`  → RETRY button background.
- `Button - Play.png`   → PLAY button background.
- `Icon - Check.png`    → Success header check.
- `Icon - X.png`        → Failed header X.

**Stub these (placeholder grey rect or simple shape)** — implementer creates 1-color placeholder Sprite assets in `Assets/Art/ResultScreen/Placeholders/`:
- `Placeholder_HoleThumbnailSmall.png` — solid green rect, 94×94.
- `Placeholder_HoleMap.png` — solid grey rect, 156×288.
- `Placeholder_Separator.png` — solid white 2px line.
- `Placeholder_LockIcon.png` — simple grey lock outline (or use the X icon at 50% opacity).
- `Placeholder_Darken.png` — semi-transparent black rect (50% alpha), full-card size.
- `Placeholder_RewardCoin.png` / `Placeholder_RewardRepair.png` / `Placeholder_RewardBall.png` — simple circles in distinct colors (yellow / silver / white).

> Implementer can also create these stubs as Unity primitive Image components with flat colors instead of PNGs — same visual result. Pick whichever is faster.

**No Figma asset downloads required** for §2d. Full-fidelity art import is a follow-up task tied to the full-implementation pass that adds the top bar / nav bar.

### G. Inspector wiring (LabScaffold.unity)

Use Unity Editor MCP only — NO raw YAML edits.

**Hierarchy (under existing main UI Canvas):**

```
LabScaffold.unity
└─ LabRoot (PhysicsLabController)
    └─ Canvas - HUD (existing)
        └─ HoleCompleteWidget [new GO + HoleCompleteWidget script]
            ├─ DimBackground [Image, full-screen, semi-transparent black]
            └─ Root [GameObject; assigned to _root]
                ├─ Card1 [HoleCompleteCardWidget, instance #1]
                │   ├─ Background [Image, HoleCard.png]
                │   ├─ HeaderRow
                │   │   ├─ SuccessHeader [GO; CheckIcon + "SUCCESS" Text]
                │   │   ├─ FailedHeader  [GO; XIcon + "FAILED" Text]
                │   │   ├─ NextHeader    [GO; "NEXT" Text]
                │   │   └─ LockedHeader  [GO; LockIcon + "LOCKED" Text]
                │   ├─ Subhead [TMP_Text]
                │   ├─ CurrentBody [GO with body content]
                │   │   ├─ HoleThumbnailSmall [Image]
                │   │   ├─ HoleMapLarge [Image]
                │   │   └─ StatsBlockText [TMP_Text]
                │   ├─ NextBody [GO, inactive on Card 1]
                │   ├─ RewardsRow [CanvasGroup]
                │   │   ├─ CoinReward [Icon + "x10" TMP_Text]
                │   │   ├─ RepairReward
                │   │   └─ BallReward
                │   ├─ Buttons
                │   │   ├─ ReplayButton [Button + Replay.png BG]
                │   │   ├─ RetryButton  [Button + Retry.png BG]
                │   │   └─ PlayButton   [Button + Play.png BG, hidden on Card 1]
                │   └─ DarkenOverlay [Image, hidden on Card 1]
                └─ Card2 [HoleCompleteCardWidget, instance #2; same hierarchy as Card 1]
```

**Driver wiring:**

Add `HoleCompleteDriver` component to the same GameObject as `LoopCameraDirector` and `HoleSessionDriver` (the lab's "drivers" GO). Wire:
- `controller` → existing PhysicsLabController GO reference.
- `widget` → the new HoleCompleteWidget GO.

**Top bar / nav bar slots (deferred per Q3):** the parent canvas has implicit space ABOVE and BELOW the HoleCompleteWidget — full-implementation pass adds:
- A `TopUIBar` GO above (sibling of HoleCompleteWidget) for RP counter, settings, RESULTS title, rankings icon.
- A `BottomNavBar` GO below for the 5 round buttons.

Do NOT add these in §2d. Document the empty slots in the hierarchy as comments in the prefab.

### H. DebugShotPanel "Hole Out" button

**Modify `DebugShotPanel.cs`:**

Add at the existing fields:

```csharp
[Header("Debug — §2d Hole Out")]
[SerializeField] private Button _holeOutBtn;
[SerializeField] private Golfin.Physics.Viewer.HoleCompleteDriver _holeCompleteDriver;
```

Update the header comment block to add the new GO:

```
//   DebugShotPanelController [this script]
//   └── DebugPanel [Image]                       ← _panelRoot
//       ├── PowerRow
//       ├── AccuracyRow
//       ├── ShootBtn                              ← _shootBtn
//       └── HoleOutBtn [Button + TextMeshProUGUI] ← _holeOutBtn  (§2d debug: simulates hole-out)
```

Wire in `Start()` (after the existing `_shootBtn?.onClick.AddListener(OnShoot)` line):

```csharp
_holeOutBtn?.onClick.AddListener(OnHoleOutDebug);
```

Add handler:

```csharp
private void OnHoleOutDebug()
{
    if (_holeCompleteDriver == null)
    {
        Debug.LogWarning("[DebugShotPanel] HoleCompleteDriver reference missing.");
        return;
    }
    _holeCompleteDriver.ShowForDebug();
    if (_panelRoot != null) _panelRoot.SetActive(false); // close debug panel before modal shows
}
```

**Inspector wiring:** add a child Button GO under `DebugPanel` named "HoleOutBtn" (sibling of ShootBtn). Wire `_holeOutBtn` and `_holeCompleteDriver` references.

## Tests

**Location:** `Assets/Scripts/Physics/Tests/RealCupDetectorTests.cs` and `HoleCompleteDriverTests.cs`.

### Required tests (minimum 9)

**RealCupDetectorTests (5):**
1. `RealCupDetector_BallInsideCup_ReturnsTrue` — pin (0,0,0), ball (0,-0.01,0), ballR=0.021 → true.
2. `RealCupDetector_BallOutsideCupRadius_ReturnsFalse` — pin (0,0,0), ball (0.1,0,0) → false.
3. `RealCupDetector_BallAboveCup_ReturnsFalse` — pin (0,0,0), ball (0,5,0) → false (height gate).
4. `RealCupDetector_BallAtCupEdge_ConsidersBallRadius` — at exact effective edge → false; at edge minus 0.001 → true.
5. `RealCupDetector_BallLargerThanCup_AlwaysReturnsFalse` — ballR=0.1 (>cupR) → false.

**HoleCompleteDriverTests (4):**
6. `HoleCompleteDriver_OnInCupTerminal_AtPar_ShowsSuccessReplay` — TurnCount=4, Par=4, fire InCup → widget showing, Card 1 success header, replayButton active.
7. `HoleCompleteDriver_OnInCupTerminal_OverPar_ShowsFailedRetryAndLockedNext` — TurnCount=5, Par=4 → widget showing, Card 1 failed header + retryButton active, Card 2 locked header + darken overlay active.
8. `HoleCompleteDriver_OnAtRestTerminal_DoesNotShowModal` — fire AtRest → widget hidden.
9. `HoleCompleteDriver_OnOBTerminal_DoesNotShowModal` — fire OB+Water → widget hidden.

**Optional bonus:**
- `ScoreLabelFor_KnownValues` — table-driven across scores -3..+5.

**Test isolation:** every test calls `GameSession.ResetForNewHole()` and `HoleContext.Reset()` in `[SetUp]`.

**Test seam:** `HoleCompleteDriver.InjectForTests(BallStateMachine, HoleCompleteWidget)` bypasses `GetComponentInParent` lookup.

**Test gate:** **N → N+9 PASS, 0 IGNORED**, baseline confirmed by Implementer in IMPLEMENTER_REPORT before adding tests. Pre-existing test failures → escalate `IMPLEMENTER_BLOCKED`, do not "fix" by editing existing tests.

## Smoke evidence

Per controls_g_smoke_followup precedent + Lesson O. State-gated captures via `CaptureCore.SnapWhenStateReached`. With the debug button in place, the InCup capture is trivial (tap "Hole Out"); no real putt-into-cup needed.

**Captures + log:**

1. `controls_2d_modal_hidden_aiming.png` — fresh hole loaded (Hole_01 additive), at first BallState.Aiming. HoleCompleteWidget root inactive. PlayerCard "TURN 1".
2. `controls_2d_modal_success_at_par.png` — set Par via HoleContext to match TurnCount (e.g., HoleContext.Par=1, TurnCount=1). Tap DebugShotPanel "Hole Out". Capture once widget root active. Verify: green ✓ SUCCESS header, "STROKES: 1 (PAR)" or HOLE-IN-ONE wording, REPLAY button visible (silver), Card 2 NEXT unlocked + PLAY visible.
3. `controls_2d_modal_failed_over_par.png` — set Par=3, force TurnCount=5. Tap "Hole Out". Verify: red ✗ FAILED header, "STROKES: 5 (DOUBLE BOGEY)", RETRY button (gold), Card 2 LOCKED header + dimmed rewards + darken overlay.
4. `controls_2d_holeout_log.txt` — `GameSession.ShotHistory` dump after capture 2.

**Visual-fidelity verification (Lesson O):** IMPLEMENTER_REPORT MUST include a content-sanity description of captures 2 and 3 — describing what numbers/labels/colors/buttons actually appeared, and how the layout compares to the Figma reference PNGs. Mode-history alone is insufficient.

**Filed under** `Docs/Specs/Active/loop_v1_2d_hole_complete_and_result_screen/screenshots/`.

## Definition of Done

- `RealCupDetector` shipped, regulation defaults + height-gated XZ math.
- `PhysicsLabController.OnHoleLoaded` installs `RealCupDetector` after `GameSession.ResetForNewHole()`. `OnHoleUnloaded` reverts to `NullCupDetector`.
- `PhysicsLabController.HandleShotComplete` gates re-arm: AtRest/OB → re-arm; InCup → defer. New `internal void RearmAfterHoleComplete()` accessor added.
- `HoleCompleteDriver` shipped, Inspector-wired in LabScaffold.unity. Public `ShowForDebug()` entrypoint.
- `HoleCompleteWidget` + `HoleCompleteCardWidget` + `HoleCompleteData` shipped. Both cards Inspector-wired with all PNGs + TMP_Text refs. Three visual states verified: Success-Replay, Failed-Retry-Locked, Failed-Replay-Unlocked (the third is testable via fake `hasPersonalBest=true` injection but the §2d runtime always passes false).
- `DebugShotPanel` "Hole Out" button shipped + wired. Tapping it triggers the widget with current TurnCount.
- 9 EditMode tests, all PASS. Test gate: N+9 PASS, 0 IGNORED.
- 3 captures + 1 log file filed.
- Manual play-and-confirm: load Hole_01, fire DebugShotPanel "Hole Out" twice (once at par to see Success, once with Par adjusted to force over-par to see Failed). Verify modal renders correctly, button taps dismiss it, SM returns to Aiming.
- IMPLEMENTER_REPORT content-sanity description per Lesson O.

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** if:
  - LabScaffold.unity Editor MCP wiring fails (component-add or reference-set throws). 2 retries, then escalate.
  - Pre-existing test gate breaks. Most likely: HandleShotComplete re-arm gate cascading into integration tests assuming unconditional re-arm. Architect investigates.
  - `fp` arithmetic operators don't compile in `RealCupDetector`. Implementer rewrites via `fp.FromFloat`/`.ToFloat` round-trips, public API unchanged.
  - Cup detection never fires in smoke putt (rolls past, stops short, height gate too tight). Architect investigates.
- **`IMPLEMENTER_PARTIAL`** acceptable if:
  - Code + 8 of 9 unit tests + first 2 captures land clean, but capture 3 (Failed variant) hits friction (e.g., HoleContext.Par writeable seam needs surgery). Acceptable to ship 2/3 captures + manual play-and-confirm description.

## Out of scope

- **Failed-state pre-amend.** Q2 lock — bogey/double-bogey distinction for "pass with lesser rewards" is Loop v2.
- **Personal-best persistence.** Q8 lock — no save layer in §2d.
- **Time tracking.** Placeholder "00:00:00". Stopwatch from tee-off → hole-out is a follow-up.
- **Real rewards economy.** Hardcoded "x10". RewardPointsManager / inventory grants are §2e+.
- **Real button → action flows.** Q5 lock — REPLAY/RETRY/PLAY all just close + re-arm in §2d.
- **Hole-cycling.** Q5 — Card 2 PLAY does not actually load Hole N+1 in §2d. §2e job.
- **Map graphics + shot-path dots.** Placeholder grey rects. Real map rendering depends on per-hole map asset pipeline.
- **Top bar (RP counter, settings, RESULTS title, rankings icon).** Q3 lock — full-implementation phase. LabScaffold has empty slot above widget.
- **Bottom nav bar (5 round buttons).** Q3 lock — same.
- **Localization (JP/EN).** All strings hardcoded English. Loc pass separate.
- **Result screen animation/transitions.** Hard show/hide. Polish phase.
- **HoleSessionDriver turn-advance gate on InCup.** Q7 lock — cosmetic-pass TODO logged.
- **`Background - Banner.png`** (the BOGEY mid-game flash). Belongs to a separate banner-flash UI not in §2d.

## Hard rules for implementer

1. **Do NOT modify** `BallStateMachine.cs`, `BallState.cs`, `ShotResult.cs`, `BallStateChange.cs`, `BallSimulation.cs`, `Trajectory.cs`, `ICupDetector.cs`, `NullCupDetector.cs`, `HoleContext.cs`, `GameSession.cs`, `HoleSessionDriver.cs`, `LoopCameraDirector.cs`, `PlayerCardWidget.cs`, `CentralBallWidget.cs`, any aero CSV, any test currently in PASS state outside the two new test files.
2. **Do NOT modify `LabScaffold.unity` via raw YAML.** Use Unity Editor APIs only.
3. **Do NOT use `WaitForSeconds(N)` for state-dependent captures.** State-gate via `SnapWhenStateReached`.
4. **Do NOT alter the design.** Render every element shown in the Figma frames — header, subhead, body, rewards, buttons, locked-state darken overlay. Use placeholders for missing data, but DO NOT remove visual elements.
5. **Do NOT add the top bar or bottom nav bar to LabScaffold in §2d.** Q3 lock.
6. **Do NOT pre-bake button→action flows beyond "close + re-arm".** Q5 lock.
7. **Do NOT proliferate static-bus files.** Read from existing `GameSession` and `HoleContext`.
8. **Smoke evidence per Lesson O:** state-gated capture + content-sanity description. Both required.
9. **Bit-exact pre-existing test gate must hold.** N → N+9, no edits to other tests.
10. **Read `FIGMA_EXTRACT.md` for layout truth** — exact px values, colors, font weights are in the React/Tailwind extract there. This SPEC summarizes; FIGMA_EXTRACT.md is canonical for dimensions.
