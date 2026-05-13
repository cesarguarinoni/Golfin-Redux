# SPEC — `loop_v1_2f_putter_p2_in_context`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state. Architect-locked at SPEC_READY 2026-05-13 19:00 JST.

## Goal

Close out the deferred half of Putter P1 now that §2a–§2e have shipped the ball-state lifecycle and AtRest/OB/re-arm wiring. Two pieces:

1. **Auto-toggle putter mode on AtRest** based on the resting surface. Green → Putter; any non-Green (including a missed putt that rolls into Collar/Rough/Fringe) → revert to the player's last non-putter club. The toggle is the bridge between Loop v1's ball-state system and Putter P1's existing `EnterPutterMode`/`ExitPutterMode` plumbing.
2. **In-context green tuning panel.** Per Cesar's P1 carve-out ("in-context tuning lives inside the loop, not beside it"), expose a compact two-slider widget — Green Rolling Resistance + Green Stop Speed — accessible from a gear-icon button in the HUD. Live-apply via `PhysicsLabController.SetSurfaceConfig`. Independent of the existing `DashboardUI` (which stays as the lab debug pane).

## Reference

- **Putter P1 SPEC:** `Docs/Specs/Completed/putter_p1_ui/SPEC.md` — line 499 carve-out: "Camera lock to green / overhead view is a follow-up task. Putter P1 uses whatever camera mode the scene is currently in." Camera waiver resolved here by reusing `ChaseCamera.GroundLevel` (existing P1 behavior on putter entry).
- **§2a SPEC:** `Docs/Specs/Completed/loop_v1_2a_ball_state_machine/SPEC.md` — `OnShotComplete(ShotResult)` carries `EndSurface` directly.
- **§2e SPEC:** `Docs/Specs/Completed/loop_v1_2e_next_shot_handoff/SPEC.md` — AtRest branch in `PhysicsLabController.HandleShotComplete` is where §2f wedges its surface check.
- **Existing tuning UI:** `Assets/Scripts/Physics/Viewer/DashboardUI.cs` — full lab dashboard with surface sliders. §2f does NOT touch this. §2f ships a small new widget aimed at in-loop tuning.
- **No new Figma** — both pieces are mechanics + a minimal new widget. Widget styling matches existing HUD button conventions (action-button row, gear-icon convention from `_actionButtonRowTop`).

## Background — what exists today

Verified by code walk 2026-05-13 18:45 JST.

| File | Role for §2f |
|---|---|
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | `HandleShotComplete(ShotResult)` AtRest branch (post-§2e) rotates yaw to pin, calls `ApplyCameraYaw`, then `CompleteShot + ReArm`. **§2f wedges the auto-switch logic BEFORE `ApplyCameraYaw` so putter's `GroundLevel` mode wins.** Also adds `_lastNonPutterClubIndex` field + `SetClub` interception. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::SetClub(int)` | Already toggles `IsPutt`, fires `OnClubChanged` → `OnClubIndexChanged` → `EnterPutterMode` / `ExitPutterMode`. **No changes to method body.** §2f calls `SetClub(putterIndex)` from inside `HandleShotComplete`. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::EnterPutterMode/ExitPutterMode` | Already swap UI elements, set `ChaseCamera.SetMode(GroundLevel)`, etc. **No changes — §2f reuses these as-is.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::LabClubs` | 4 entries. Putter index = `LabClubs.Length - 1` = 3. **§2f uses `PutterIndex` named constant for clarity.** |
| `Assets/Scripts/Gameplay/Loop/ShotResult.cs` | Already carries `EndSurface : SurfaceType`. **Source signal for the surface check.** |
| `Assets/Scripts/Physics/Core/SurfaceConfig.cs` | `SurfaceCoefficients` has Restitution, TangentFriction, RollingResistance, StopSpeed. Indexed by `(int)SurfaceType`. **§2f's panel reads/writes `_surface[SurfaceType.Green].RollingResistance` and `.StopSpeed` only.** |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::SetSurfaceConfig(SurfaceConfig)` | Already public. **§2f's panel calls this on every slider change.** |
| `Assets/Scripts/Physics/Viewer/DashboardUI.cs` | Big debug pane with Green/Fairway/Rough sliders. **Hard rule: do NOT modify. §2f's panel is independent.** |
| `Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` (P1) | Already displays current club; subscribes to `ClubSelectionBroadcast.OnClubChanged`. **Zero changes — auto-switch fires `SetClub` which raises the broadcast.** |

### Auto-switch correctness checks

1. **`EndSurface` on AtRest is reliable.** `BallStateMachine.OnTrajectoryComputed` builds the terminal `BallStateChange` with `terminalSurface = _surfaces.Classify(finalPos.x, finalPos.z)` — the surface classifier authority. `ShotResult.EndSurface` carries the same value. The classifier respects baked zone data when present (`BakedZoneClassifier`), so collar/fringe vs green is correctly distinguished. Verified by code walk.
2. **Auto-switch on InCup is irrelevant.** §2d's `HoleCompleteDriver` owns InCup; the modal closes the hole and the next hole's load fires `ResetForNewHole` which resets to whatever the player had selected. §2f explicitly does NOT auto-switch on InCup.
3. **Auto-switch on OB is irrelevant.** §2e's OB drop places the ball on a non-Water/non-OOB hit. That position's classified surface drives auto-switch on the *next* shot's AtRest. The OB shot itself does NOT trigger auto-switch (terminal state is OB, not AtRest — §2f's switch is gated on AtRest only).
4. **First-shot behavior unchanged.** Tee shots fire from `SurfaceType.Tee`. Auto-switch is gated on `result.TerminalState == AtRest` AND surface comparison. At game start, `CurrentClubIndex` = whatever Inspector default holds (Driver). `_lastNonPutterClubIndex` initialized to `CurrentClubIndex` in `Awake` AFTER configs are loaded so the first auto-exit reverts to Driver, not Iron 7.

## Locked decisions

- **L1 — Auto-enter trigger: Green strict.** `result.EndSurface == SurfaceType.Green` and only that. GreenCollar / fringe / approach are NOT putter triggers. Player chips from those.
- **L2 — Auto-exit trigger: any non-Green AtRest while currently in putter mode.** Most common case: putt rolls off green into collar/fringe. Less common: putt fat-strikes into rough beyond the green. Either way, the next stroke is a chip — revert to the last non-putter club.
- **L3 — Revert target: `_lastNonPutterClubIndex`** cached on every `SetClub` call where `index != PutterIndex`. Initialized in `Awake` to the Inspector-default `CurrentClubIndex` (Driver = 0 in normal Lab config).
- **L4 — Camera: reuse `ChaseCamera.GroundLevel`.** Already what P1's `EnterPutterMode` does. The §2e pin-aim rotation is SKIPPED when the auto-switch flips to putter (GroundLevel mode owns framing, not the orbit yaw).
- **L5 — Re-evaluation on every AtRest.** The check runs in `HandleShotComplete`'s AtRest branch regardless of current club. Idempotent: if `EndSurface == Green` and already in putter mode, no-op (the index equality check short-circuits `SetClub`). Same for non-Green when already in non-putter mode.
- **L6 — Tuning panel is a separate widget**, not a DashboardUI extension. Two sliders only: Green Rolling Resistance (0–0.5, default 0.12) + Green Stop Speed (0–0.2, default 0.05). Live-apply on slider change via `PhysicsLabController.SetSurfaceConfig`. Reset button → re-load `PhysicsConfigLoader.LoadSurfaceConfig()` Green entry only (preserve user edits to other surfaces).
- **L7 — Tuning panel access: gear-icon toggle button** in the existing action-button row area. Hotkey alternative `G` for debug builds. Panel is hidden by default.
- **L8 — No persistence of tuning values across play-mode exit.** Edits are runtime-only; reset on Play→Edit boundary. Persistence is a Loop v2 / settings spec concern. Make this a `[SerializeField] bool _persistEdits = false` so it's explicit.
- **L9 — Tuning panel does NOT touch PuttConfig.** P1's putt-specific roll resistance lives in `PuttConfig`, edited via DashboardUI's `AddPuttSliders`. §2f's panel only touches `SurfaceConfig[Green]`. PuttConfig tuning stays in DashboardUI (lab-only).

## Architecture context

- **No new asmdef.** All work in `Golfin.Physics.Viewer` (controller extension + new `PutterModeSurfaceController` static helper + new `GreenTuningPanel` MonoBehaviour).
- **No changes to** `Golfin.Gameplay.Loop`, `Golfin.Physics.Core`, `Golfin.Physics.Stats`, `Golfin.Diagnostics.Runtime`, `Golfin.Gameplay.UI.HUD`, any aero CSV.
- **No new test asmdef.** New tests land in existing `Golfin.Physics.Tests`.
- **Scene changes: ONE.** `LabScaffold.unity` adds a new GameObject hierarchy for the `GreenTuningPanel` widget under the existing Canvas. Use Unity Editor MCP APIs (`gameobject-create`, `gameobject-component-add`, `gameobject-component-modify`, `scene-save`), NEVER raw YAML (controls_g lesson).

## Implementation

### A. `PutterModeSurfaceController` static helper

**Location:** `Assets/Scripts/Physics/Viewer/PutterModeSurfaceController.cs`. Namespace `Golfin.Physics.Viewer`.

Pure surface-→-club decision logic. Test seam.

```csharp
using Golfin.Physics;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2f: pure decision logic for auto-switching club based on AtRest surface.
    /// Returns the target club index, or -1 to mean "no change".
    /// </summary>
    public static class PutterModeSurfaceController
    {
        /// <summary>
        /// Given the current club, the surface at AtRest, and the last non-putter
        /// club index, returns the target club index. Returns -1 if no switch is
        /// needed (idempotent).
        /// </summary>
        /// <param name="currentClubIndex">Current player club index.</param>
        /// <param name="putterIndex">Index of the Putter in LabClubs.</param>
        /// <param name="endSurface">Surface under the ball at AtRest.</param>
        /// <param name="lastNonPutterClubIndex">Cached fallback for auto-exit.</param>
        public static int DecideTargetClub(
            int currentClubIndex, int putterIndex,
            SurfaceType endSurface, int lastNonPutterClubIndex)
        {
            bool onGreen     = endSurface == SurfaceType.Green;
            bool inPutterMode = currentClubIndex == putterIndex;

            if (onGreen && !inPutterMode) return putterIndex;
            if (!onGreen && inPutterMode) return lastNonPutterClubIndex;
            return -1; // no change
        }
    }
}
```

### B. `PhysicsLabController` extensions

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

**B.1 — Add `PutterIndex` named constant + `_lastNonPutterClubIndex` field:**

Near the existing `LabClubs` array declaration:

```csharp
// §2f: Named constant for the putter index. Matches LabClubs.Length - 1.
public static readonly int PutterIndex = LabClubs.Length - 1;

// §2f: Tracks the last non-putter club the player used.
// Initialized in Awake to whatever the Inspector default CurrentClubIndex is
// (typically Driver = 0). Updated on every SetClub(index != PutterIndex) call.
// Used by auto-exit to revert from putter when the ball comes to rest off-green.
int _lastNonPutterClubIndex = 0;
```

**B.2 — Initialize `_lastNonPutterClubIndex` in `Awake`** after configs are loaded and BEFORE the first auto-switch can fire. Place this AFTER `EnsureConfigsLoaded()` and AFTER any initial `SetClub` call. Cleanest: do it at the end of `Awake`:

```csharp
// §2f: cache initial non-putter club so auto-exit has a valid fallback target.
_lastNonPutterClubIndex = (CurrentClubIndex == PutterIndex) ? 0 : CurrentClubIndex;
```

**B.3 — Update `SetClub` to refresh `_lastNonPutterClubIndex`:**

In `SetClub(int index)`, immediately after `CurrentClubIndex = index;`:

```csharp
// §2f: remember the last non-putter selection for auto-exit.
if (index != PutterIndex) _lastNonPutterClubIndex = index;
```

**B.4 — Wedge auto-switch into `HandleShotComplete`'s AtRest branch:**

Inside the existing `case BallState.AtRest:` block (from §2e), the very first thing — BEFORE the §2e pin-aim rotation:

```csharp
case Golfin.Gameplay.Loop.BallState.AtRest:
{
    // §2f: surface-based auto-switch BEFORE §2e camera rotation.
    // If we flip into putter mode, EnterPutterMode sets ChaseCamera to
    // GroundLevel — we must NOT then call ApplyCameraYaw which would
    // override the GroundLevel framing. Hence the early-return path below.
    int target = PutterModeSurfaceController.DecideTargetClub(
        currentClubIndex: CurrentClubIndex,
        putterIndex: PutterIndex,
        endSurface: result.EndSurface,
        lastNonPutterClubIndex: _lastNonPutterClubIndex);

    bool willFlipToPutter   = target == PutterIndex && CurrentClubIndex != PutterIndex;
    bool willFlipFromPutter = target != PutterIndex && target >= 0 && CurrentClubIndex == PutterIndex;

    if (target >= 0)
    {
        Debug.Log($"[PhysicsLab][§2f] AtRest surface={result.EndSurface} " +
                  $"auto-switch club {CurrentClubIndex}→{target} " +
                  $"(willFlipToPutter={willFlipToPutter} willFlipFromPutter={willFlipFromPutter})");
        SetClub(target);
    }

    if (willFlipToPutter)
    {
        // Putter mode owns camera framing (GroundLevel). Skip pin-aim rotation
        // and ApplyCameraYaw. Still re-arm.
        _shotController?.CompleteShot();
        _ballSM.ReArm();
        break;
    }

    // §2e: existing pin-aim rotation path (unchanged).
    Vector3 ballPos = ballAnimator?.CurrentBall != null
        ? ballAnimator.CurrentBall.position
        : _orbitCenter;
    Vector3 pinPos  = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
    float   newYaw  = AimRotationHelper.ComputeYawTowardPin(ballPos, pinPos, _cameraYaw);
    if (!Mathf.Approximately(newYaw, _cameraYaw))
    {
        _cameraYaw = newYaw;
        if (_shotController != null)
            _shotController.CameraHeadingRadians = _cameraYaw;
    }

    Camera cam = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null;
    if (cam != null) ApplyCameraYaw(cam);

    _shotController?.CompleteShot();
    _ballSM.ReArm();
    break;
}
```

**Edge case: willFlipFromPutter.** When ball was on green (putter mode) and the putt sent it to rough, `SetClub(_lastNonPutterClubIndex)` fires `ExitPutterMode` which sets `ChaseCamera.SetMode(Chase)` (the default mode). Then the §2e pin-aim path runs and `ApplyCameraYaw` writes a fresh Chase-mode camera transform. Correct.

### C. `GreenTuningPanel` MonoBehaviour

**Location:** `Assets/Scripts/Physics/Viewer/GreenTuningPanel.cs`. Namespace `Golfin.Physics.Viewer`.

```csharp
using UnityEngine;
using UnityEngine.UI;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2f: compact in-loop green-tuning widget. Two sliders + a reset button.
    /// Toggled via a gear-icon button in the HUD. Independent of DashboardUI
    /// (which stays as the full lab debug pane).
    /// </summary>
    public class GreenTuningPanel : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;
        [SerializeField] GameObject panelRoot;        // The collapsible content panel
        [SerializeField] Button     toggleButton;     // Gear-icon button that shows/hides panelRoot
        [SerializeField] Slider     rollingResistanceSlider;
        [SerializeField] Slider     stopSpeedSlider;
        [SerializeField] Text       rollingResistanceLabel;
        [SerializeField] Text       stopSpeedLabel;
        [SerializeField] Button     resetButton;

        // Editor-visible so a Cesar can pre-set initial values without entering play mode.
        // L8: NOT persisted across play-mode exit (this is the bool that confirms it).
        [SerializeField] bool _persistEdits = false;

        const float kRollingResistanceMin = 0f;
        const float kRollingResistanceMax = 0.5f;
        const float kStopSpeedMin         = 0f;
        const float kStopSpeedMax         = 0.2f;

        void Awake()
        {
            if (controller == null) controller = FindObjectOfType<PhysicsLabController>();

            if (panelRoot != null) panelRoot.SetActive(false);
            if (toggleButton != null) toggleButton.onClick.AddListener(TogglePanel);
            if (resetButton != null)  resetButton.onClick.AddListener(ResetToDefault);
        }

        void OnEnable()
        {
            // Initialize slider values from current config.
            if (controller == null) return;
            var greenCoef = controller.SurfaceCfg[SurfaceType.Green];
            if (rollingResistanceSlider != null)
            {
                rollingResistanceSlider.minValue = kRollingResistanceMin;
                rollingResistanceSlider.maxValue = kRollingResistanceMax;
                rollingResistanceSlider.SetValueWithoutNotify(greenCoef.RollingResistance.ToFloat());
                rollingResistanceSlider.onValueChanged.AddListener(OnRollingResistanceChanged);
            }
            if (stopSpeedSlider != null)
            {
                stopSpeedSlider.minValue = kStopSpeedMin;
                stopSpeedSlider.maxValue = kStopSpeedMax;
                stopSpeedSlider.SetValueWithoutNotify(greenCoef.StopSpeed.ToFloat());
                stopSpeedSlider.onValueChanged.AddListener(OnStopSpeedChanged);
            }
            UpdateLabels();
        }

        void OnDisable()
        {
            if (rollingResistanceSlider != null) rollingResistanceSlider.onValueChanged.RemoveListener(OnRollingResistanceChanged);
            if (stopSpeedSlider != null)         stopSpeedSlider.onValueChanged.RemoveListener(OnStopSpeedChanged);
        }

        void OnDestroy()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(TogglePanel);
            if (resetButton != null)  resetButton.onClick.RemoveListener(ResetToDefault);
        }

        void TogglePanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
        }

        void OnRollingResistanceChanged(float value)
        {
            if (controller == null) return;
            var cfg   = controller.SurfaceCfg;
            var coef  = cfg[SurfaceType.Green];
            coef.RollingResistance = fp.FromFloat(value);
            cfg.Coefficients[(int)SurfaceType.Green] = coef;
            controller.SetSurfaceConfig(cfg);
            UpdateLabels();
        }

        void OnStopSpeedChanged(float value)
        {
            if (controller == null) return;
            var cfg   = controller.SurfaceCfg;
            var coef  = cfg[SurfaceType.Green];
            coef.StopSpeed = fp.FromFloat(value);
            cfg.Coefficients[(int)SurfaceType.Green] = coef;
            controller.SetSurfaceConfig(cfg);
            UpdateLabels();
        }

        void ResetToDefault()
        {
            if (controller == null) return;
            // L6: only reset the Green entry; preserve user edits to other surfaces.
            var cfg = controller.SurfaceCfg;
            var defaultCfg = SurfaceConfig.Default;
            cfg.Coefficients[(int)SurfaceType.Green] = defaultCfg.Coefficients[(int)SurfaceType.Green];
            controller.SetSurfaceConfig(cfg);

            // Refresh sliders to match.
            var greenCoef = cfg[SurfaceType.Green];
            if (rollingResistanceSlider != null) rollingResistanceSlider.SetValueWithoutNotify(greenCoef.RollingResistance.ToFloat());
            if (stopSpeedSlider != null)         stopSpeedSlider.SetValueWithoutNotify(greenCoef.StopSpeed.ToFloat());
            UpdateLabels();
        }

        void UpdateLabels()
        {
            if (controller == null) return;
            var greenCoef = controller.SurfaceCfg[SurfaceType.Green];
            if (rollingResistanceLabel != null) rollingResistanceLabel.text = $"Roll Resist: {greenCoef.RollingResistance.ToFloat():F3}";
            if (stopSpeedLabel != null)         stopSpeedLabel.text         = $"Stop Speed: {greenCoef.StopSpeed.ToFloat():F3} m/s";
        }
    }
}
```

### D. `LabScaffold.unity` scene wiring

Build the widget hierarchy under the existing Canvas. **Use Unity Editor MCP**, NEVER raw YAML.

Hierarchy (under `Canvas/ShotUI` or sibling):
```
GreenTuningPanel (RectTransform, GreenTuningPanel script)
├── ToggleButton (gear icon, anchored top-right, 60×60)
│   └── ToggleButton/IconImage (Image, gear sprite — placeholder color block OK)
└── PanelRoot (Image background, anchored top-right below toggle, 320×220, initially inactive)
    ├── Title (Text "GREEN TUNING")
    ├── RollResistRow (HorizontalLayoutGroup)
    │   ├── RollResistLabel (Text)
    │   └── RollResistSlider (Slider)
    ├── StopSpeedRow (HorizontalLayoutGroup)
    │   ├── StopSpeedLabel (Text)
    │   └── StopSpeedSlider (Slider)
    └── ResetButton (Button with Text child "Reset")
```

Wire all `[SerializeField]` references on the `GreenTuningPanel` script via `gameobject-component-modify`. Anchor toggle to top-right at offset (-20, -20). Anchor PanelRoot below toggle, same right alignment.

**Placeholder asset note:** gear sprite is best-effort — if no asset exists, ship with a TMP "⚙" character or solid square colorblock + label "GREEN" on the button. The widget must function regardless of icon polish.

## Tests

**Location:** `Assets/Scripts/Physics/Tests/PutterModeSurfaceControllerTests.cs` (new file). Asmdef: `Golfin.Physics.Tests` (existing).

**6 required tests:**

1. **`DecideTargetClub_OnGreenAndNotPutter_ReturnsPutterIndex`** — `currentClub=0` (Driver), `putterIndex=3`, `surface=Green`, `lastNonPutter=0` → returns `3`.
2. **`DecideTargetClub_OnGreenAndAlreadyPutter_ReturnsNoChange`** — `currentClub=3`, `putterIndex=3`, `surface=Green`, `lastNonPutter=0` → returns `-1`.
3. **`DecideTargetClub_OffGreenAndInPutter_ReturnsLastNonPutter`** — `currentClub=3`, `putterIndex=3`, `surface=Fairway`, `lastNonPutter=1` → returns `1` (Iron 7).
4. **`DecideTargetClub_OffGreenAndNotPutter_ReturnsNoChange`** — `currentClub=0`, `putterIndex=3`, `surface=Rough`, `lastNonPutter=0` → returns `-1`.
5. **`DecideTargetClub_GreenCollarIsNotGreen_KeepsNonPutter`** — `currentClub=1`, `putterIndex=3`, `surface=GreenCollar`, `lastNonPutter=1` → returns `-1` (collar is NOT green per L1 strict rule).
6. **`DecideTargetClub_AllNonGreenSurfacesTriggerExitFromPutter`** — parameterized over `[Fairway, GreenCollar, Semirough, Rough, Tee, Sand, BunkerLip, CartPath]` — all return `lastNonPutterClubIndex` when `currentClub == putterIndex`. (Water/OOB excluded — those terminate as OB, not AtRest.)

**Test gate:** `N → N+6 PASS, 0 IGNORED` where N is the current baseline (273 per §2e closure, but verify on baseline run). If any pre-existing test breaks, escalate `IMPLEMENTER_BLOCKED` — do NOT "fix" by editing existing tests.

**Test isolation:** new tests only exercise the static helper. No `GameSession` state mutation. No `[SetUp]` needed.

**No tests for `GreenTuningPanel`.** It's UI glue around `SetSurfaceConfig` which is already callable. Coverage falls under smoke evidence.

## Smoke evidence

Four captures + one log artifact filed under `Docs/Specs/Active/loop_v1_2f_putter_p2_in_context/screenshots/` with `controls_2f_*` prefix.

Use `CaptureCore.SnapWhenStateReached` for state-gated captures. NO `WaitForSeconds(N)` for state-dependent moments (Lesson controls_g).

1. **`controls_2f_auto_enter_putter_on_green.png`** — load `Hole_01_Geo` additively. Player has Driver selected. Manually place ball at a Green location (use existing Place Ball dropdown → "Green 1"). Fire a tiny driver shot (low speed) so ball stays on green at AtRest. Capture at first Aiming after AtRest. Required: club button shows "Putter", ChaseCamera is in GroundLevel mode (visible from low angle), PutterTrack visible. Console shows `[§2f] AtRest surface=Green auto-switch club 0→3`.

2. **`controls_2f_auto_exit_to_last_club.png`** — continuing from #1, player is now in putter mode. Hit a putt with too much power so ball rolls off green onto collar/fringe (or load Hole_01 and place at green edge, then putt off). Capture at next Aiming. Required: club button reads whatever the player was using pre-putt (Driver = 0), GroundLevel mode no longer active, Chase mode resumed.

3. **`controls_2f_tuning_panel_open.png`** — gear button visible top-right. Click it (or trigger via test seam). Panel expands showing two sliders + reset button + labels with current Green values. Implementer documents starting values in `IMPLEMENTER_REPORT.md`.

4. **`controls_2f_tuning_live_apply.png`** — drag Rolling Resistance slider from 0.12 down to 0.05 (faster green). Fire a putt of same power as a baseline reference shot. Ball must roll visibly farther than the baseline. Implementer notes the rolled distance delta in `IMPLEMENTER_REPORT.md § Visual Verification`.

5. **`controls_2f_history_log.txt`** — dump `controller.SurfaceCfg[SurfaceType.Green]` four times: at Awake, after dragging the slider in #4, after Reset button press, and after a hole reload. Required final state: Reset restores Green to defaults; hole reload does NOT reset (per L8 — runtime-only, no persistence and no reload-side-effect on the tuning either).

### Visual-fidelity verification (Lesson O)

§2f is visual-fidelity work (club button changes, camera mode swap, slider live-apply on ball physics). Per Lesson O, mode-history captures alone are insufficient. Implementer drives the lab manually for all four cases and writes a content-sanity description in `IMPLEMENTER_REPORT.md § Visual Verification` for each capture. Cesar approves the descriptions during architect review.

## Definition of Done

- `PutterModeSurfaceController.cs` shipped with `DecideTargetClub(...) → int` API.
- `PhysicsLabController` extended: `PutterIndex` const, `_lastNonPutterClubIndex` field tracked, AtRest branch in `HandleShotComplete` runs auto-switch BEFORE §2e pin-rotation, willFlipToPutter path skips `ApplyCameraYaw`.
- `GreenTuningPanel.cs` shipped with two sliders + reset, live-applies via `SetSurfaceConfig`, gear-icon toggle.
- `LabScaffold.unity` wired: GreenTuningPanel hierarchy added under Canvas, all `[SerializeField]` references populated via Unity Editor MCP (NOT raw YAML).
- 6 new EditMode tests in `PutterModeSurfaceControllerTests.cs`, all PASS. Test gate: **baseline+6 PASS, 0 IGNORED**.
- 4 captures + 1 surface-config-log artifact filed under `Docs/Specs/Active/loop_v1_2f_putter_p2_in_context/screenshots/`.
- Implementer's content-sanity descriptions in `IMPLEMENTER_REPORT.md § Visual Verification` cover all four cases.
- Cesar manually plays through all four cases and confirms behaviors in live play (Lesson O human gate).

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** if:
  - Auto-switch fires on InCup or OB transitions (regression). The §2f branch is gated on `case BallState.AtRest:` only; if the implementer accidentally hoists it above the switch, fix and re-test. Architect investigates if the gate is correct but auto-switch still leaks.
  - LabScaffold scene MCP wiring fails (component-add throws, or [SerializeField] references can't be set programmatically). Implementer tries 2 retries with logged diagnostics before escalating; architect resolves with alternative wiring path.
  - Any pre-existing test starts failing. Most likely cause: a test indirectly depends on `LabClubs` ordering or `CurrentClubIndex` initial state. Architect investigates.
- **`IMPLEMENTER_PARTIAL`** acceptable if:
  - Captures #1–#3 land clean but #4 (slider live-apply) is flaky due to timing of the slider event vs. fire trigger. Architect closes with note + Cesar verifies live.
  - Gear-icon sprite asset doesn't exist and "⚙" TMP fallback isn't crisp. Acceptable as placeholder; polish ticket for later.

## Out of scope

- **Overhead / top-down green camera.** Cesar locked L4: reuse `GroundLevel`. New overhead mode is future polish.
- **Stimp-meter computation.** No "estimated stimp 9.5" readout. Just raw RollingResistance + StopSpeed values. Stimp math is Loop v2 / polish phase.
- **PuttConfig tuning surfacing.** L9 — PuttConfig stays in DashboardUI. §2f's panel only touches `SurfaceConfig[Green]`.
- **Persistence of tuning edits.** L8 — runtime-only. Settings spec / Loop v2 handles persistence.
- **Per-hole or per-green tuning.** §2f tunes the global `SurfaceConfig[Green]`. Per-hole green variation is Loop v2+.
- **Modifying DashboardUI.** Lab debug pane is untouched. §2f's panel is a peer, not a replacement.
- **Auto-switch on InCup / OB.** L2 — only on AtRest.
- **Switching to specific surface-appropriate clubs** (e.g. Wedge from Sand). Auto-switch is binary putter-vs-last-non-putter. Smart club selection is Loop v2+.
- **Tracking a per-shot club history.** `_lastNonPutterClubIndex` is single-valued, overwritten on every non-putter `SetClub`. No stack/history.
- **Touching `EnterPutterMode` / `ExitPutterMode` internals.** Hard rule — these worked in P1 and must keep working unchanged.
- **Touching `LoopCameraDirector`.** §2f doesn't override Director's mode dispatch; the GroundLevel framing comes through P1's `EnterPutterMode` already.
- **OBFreeze HUD label fix.** Filed as separate Notion ticket (Order 251, P3, XS). NOT in §2f scope.

## Hard rules for implementer

1. **Do NOT modify** `BallStateMachine.cs`, `BallState.cs`, `BallStateChange.cs`, `ShotResult.cs`, `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, `LoopCameraDirector.cs`, `HoleCompleteDriver.cs`, `RealCupDetector.cs`, `DashboardUI.cs`, `PuttPathPredictor.cs`, `ShotConeView.cs`, `ClubButtonWidget.cs`, `PowerGaugeWidget.cs`, `HoleIndicatorWidget.cs`, `CentralBallWidget.cs`, any aero CSV, or any test currently in PASS state outside `PutterModeSurfaceControllerTests.cs`.
2. **Do NOT modify `EnterPutterMode` or `ExitPutterMode` bodies.** Existing P1 behavior must be preserved verbatim.
3. **Do NOT modify `LabScaffold.unity` via raw YAML.** Use Unity Editor MCP APIs only (`gameobject-create`, `gameobject-component-add`, `gameobject-component-modify`, `scene-save`). Per controls_g lesson, raw YAML edits trigger Unity reload popups.
4. **Do NOT use `WaitForSeconds(N)` for state-dependent captures.** State-gate via `CaptureCore.SnapWhenStateReached`.
5. **Do NOT add new asmdef.** All new files live under existing `Golfin.Physics.Viewer` assembly.
6. **Do NOT persist tuning edits across play-mode exit.** L8 — explicit `_persistEdits = false` field, no `EditorPrefs` writes, no `ScriptableObject` updates.
7. **Do NOT auto-switch on InCup or OB.** L1/L2 — only AtRest.
8. **Do NOT introduce a stimp readout, overhead camera, or per-hole green variation.** All in `Out of scope` — those are future polish.
9. **Bit-exact pre-existing test gate must hold.** Adding 6 tests to baseline N → N+6. If any pre-existing test starts failing, escalate `IMPLEMENTER_BLOCKED` immediately — do NOT "fix" by editing existing tests.
10. **Visual fidelity per Lesson O.** Auto-switch (club button label change), camera mode swap, slider live-apply on ball physics — all require live-play verification by the implementer.
11. **Implementer-PARTIAL → FAIL default** per `Docs/Architecture/REVIEW_PIPELINE_FIXES.md`. Reviewers do not soft-pass any PARTIAL items.
12. **Independent pixel scan FIRST** in self-review and architect review per REVIEW_PIPELINE_FIXES — open the capture, write 3–5 sentence description of what's visible, THEN read the IMPLEMENTER_REPORT.
13. **Scene-mutation audit per REVIEW_PIPELINE_FIXES.** Add `LabScaffold.unity` to the diff scope, but verify the only `.unity` change is the GreenTuningPanel hierarchy under Canvas. Any other dirty GameObject (e.g. ShotUI children touched, prefab overrides) = automatic FAIL.
14. **`SmokeRunner` pattern from §2e cleanup.** If implementer adds a smoke-runner host script, wrap class body in `#if UNITY_EDITOR ... #endif` per the §2e post-review rule (`feedback_restore_playable_state.md`).
