# SPEC — `putter_p1_ui` — Putter Mode UI (Phase 1)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state.

## Goal

When the player switches to the putter, the Shot UI swaps from the standard cone-and-arrow layout to a **putter track + predicted-path line + meter-based readouts**. The touch-pull-flick state machine is unchanged — this is a visual re-skin plus a live-physics-driven prediction of the actual ball path on the green. Three substantive changes vs the standard UI: cone replaced by a fixed-width vertical track; targeting line replaced by a curved polyline tracing the live `BallSimulation` output (with an optional debug heatmap); top action button row hidden, ball selector locked. Plus mechanical unit swaps from yards to meters in the gauge and HoleIndicator.

## Reference

- **Figma frame:** `In-Game - Putting`, id `12951:3529` in file `5gEAHjl6xAtW8iYY7NMvWd` (page `In-game`).
- **Reference for visual diff:** screenshot of the Figma frame above (1170×2532 viewport, exported as PNG by the implementer at start of work). Save to `screenshots/figma-reference.png` and use that as the left-hand pane of the side-by-side diff.
- **Comparison point for "standard mode":** `Docs/Reference/In-game UI/Initial State.png` — most of the top-bar / gauge / button geometry is shared.
- **Placeholder vs canonical content:** all numeric mockups in the Figma (`5 mts`, `50%`, `10 mts`, `25 mts`, `Lv 13`, `TURN 5`, `HOLE 1 - LADY'S`, `PAR 5`) are mockups. **Use real values from runtime** wherever a live signal exists. Cesar 2026-05-01.
- **Canvas reference:** 1170 × 2532, Match=0. **1 Figma px = 1 Unity unit.** No conversion factor (`Docs/Architecture/RUNTIME_BLUEPRINT.md` §1).

## Architecture context

**Asmdef boundaries affected:**
- `Golfin.Gameplay.UI` (autoReferenced=true): all new `MaskableGraphic` subclasses + widget MonoBehaviours.
- `Assembly-CSharp` (the catch-all): `PuttPathPredictor` lives here. It needs to read `BagManager` / `BallManager` / `CharacterManager` / `PhysicsLabController` providers, all of which are Assembly-CSharp side. Pushes results directly to the `PuttPathRenderer` MonoBehaviour reference (no static bus — adds latency we don't want for live prediction).
- `Golfin.Physics.Viewer` (`PhysicsLabController` lives here): adds public accessors for ground/surface providers + new `EnterPutterMode` / `ExitPutterMode` methods + new `ComputeMaxPuttRangeMeters` helper.

**Existing code referenced (do NOT modify; just call):**
- `Golfin.Physics.BallSimulation.Simulate(8-arg)` — the deterministic putt-aware sim entry. Read-only.
- `Golfin.Physics.Stats.ShotInputBuilder.Build` — converts `(StatBundle, power01, aimYaw, origin, seed, baseVelOverride)` → `(ShotInput, BallPhysicsModifiers)`. Read-only.
- `Golfin.Gameplay.Config.ControlsConfig.Default.PuttBaseVelocityMps` — seed putt base velocity (5 m/s currently).
- `Golfin.Roster.CharacterManager` / `Golfin.Inventory.BagManager` / `Golfin.Inventory.BallManager` — singletons providing the StatBundle inputs.
- `Golfin.Physics.Stats.DefaultStatProvider.BuildPuttBundle()` — fallback when CharacterManager.Instance is null in lab.

**Existing code modified:**
- `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — add `SetPuttMode(bool)` API + putt-mode gating in handle/cone/targeting-line code paths.
- `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs` — add `DistanceUnit` enum + `_maxPuttRangeMeters` field + unit-aware text rendering.
- `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` — add putt-mode size (150) vs normal-mode size (80) toggle.
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicator.cs` (or `HoleIndicatorWidget` — implementer to verify the exact filename in repo) — add unit-mode flag, mts/yds toggle.
- `Assets/Scripts/Gameplay/Input/ShotDebugFlags.cs` — add `bool PuttPathHeatmap` (default false).
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `OnClubChanged` route to enter/exit putter mode + new `ComputeMaxPuttRangeMeters()` + public provider accessors.
- `Assets/Scripts/Debug/PhysicsLabUI.cs` — add a debug toggle button for `PuttPathHeatmap`.
- `Assets/Scenes/Physics/LabScaffold.unity` — add the new GameObjects under `ShotUI_Canvas` and wire all Inspector refs.

**New files:**
- `Assets/Scripts/Gameplay/UI/ShotUI/PutterTrackGraphic.cs` — `MaskableGraphic` for the vertical track.
- `Assets/Scripts/Gameplay/UI/ShotUI/PuttPathRenderer.cs` — `MaskableGraphic` for the polyline.
- `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` — Assembly-CSharp side MonoBehaviour. **NOTE the path: `Assets/Scripts/UI/HUD/`, not `Assets/Scripts/Gameplay/UI/`.** The `Gameplay.UI` asmdef cannot reference Assembly-CSharp (the same constraint that drove 8.3's PlayerContext+Populator pattern); the predictor needs Assembly-CSharp scope to reach the inventory singletons.

**Manager APIs used:**
- `CharacterManager.Instance.GetSelectedCharacterId()` + `CharacterDatabaseCSV.Instance.GetCharacter(id)` — for character stats.
- `BagManager.Instance.EquippedClubs[putterIndex]` — for `ClubStats`. Implementer: confirm the actual method/property name on BagManager — there's an `EquippedClubs` reference in `Docs/Specs/Completed/PHASE_8_SHOT_UI_POLISH.md` § Part 8.6 but verify against `BagManager.cs` directly.
- `BallManager.Instance.EquippedBall` (similar — verify the actual API).
- All three may be null in `LabScaffold.unity` (it's a sandbox without ShellScene). Fallback path: `DefaultStatProvider.BuildPuttBundle()`.

## Implementation

### Step 0 — reference walk-through (Implementer reads before coding)

Open the Figma frame `12951:3529` and `Docs/Reference/In-game UI/Initial State.png` side by side. The putter mode is the standard mode minus the cone, plus a track, plus a curved path line, plus unit swaps. List on a scratch pad: top bar (unchanged), HoleIndicator (unit swap), center column (track + ball + path + handle, all new positions), gauge (unit swap), bottom buttons (top row hidden, ball selector dimmed).

### Step 1 — Putter track (commit `putter-p1-ui.A`)

**File:** `Assets/Scripts/Gameplay/UI/ShotUI/PutterTrackGraphic.cs`

Subclass `MaskableGraphic`. Inspector fields:
```
[SerializeField] float _width             = 140f;
[SerializeField] float _height            = 1000f;
[SerializeField] float _greenBandHeight   = 200f;
[SerializeField] float _amberBandHeight   = 300f;
[SerializeField] float _bandLineThickness = 4f;
[SerializeField] Color _greenBandColor    = new Color32(0x62, 0x73, 0x52, 0xFF);
[SerializeField] Color _amberBandColor    = new Color32(0x8F, 0x72, 0x40, 0xFF);
[SerializeField] Color _redBandColor      = new Color32(0x7A, 0x3E, 0x3E, 0xFF);
[SerializeField] Color _gradientEdge      = new Color(0f, 0f, 0f, 0.15f);
[SerializeField] Color _gradientCenter    = new Color(0f, 0f, 0f, 0.5f);
```

Pivot (0.5, 1) — top-center, so positioning matches the cone's anchoring convention.

`OnPopulateMesh`:
1. Body: emit a horizontal-gradient quad covering the full RectTransform. Vertex colors: edges = `_gradientEdge`, center = `_gradientCenter`. Use a 5-vertex strip (left-edge top/bottom, center top/bottom, right-edge top/bottom — actually 6 verts, 4 triangles in a 2-quad strip), so the gradient lights up at the center properly. **Y orientation:** with pivot (0.5, 1), the top of the rect is y=0 and the bottom is y=-_height. Adjust vertex Y accordingly.
2. Three border lines: each is a quad spanning full width × `_bandLineThickness`. Y positions:
   - Green line: `y = -_greenBandHeight`
   - Amber line: `y = -(_greenBandHeight + _amberBandHeight)` = `-500`
   - Red line: `y = -_height` (bottom edge)
   Each uses its respective band color.

**Scene wiring (`LabScaffold.unity`):**
- Create `PutterTrack` GameObject under `ShotUI_Canvas` (peer of `ConeRoot`).
- RectTransform: anchor (0.5, 1), pivot (0.5, 1), anchoredPosition (0, -1453), sizeDelta (140, 1000).
- Add `PutterTrackGraphic` component.
- Z-order: behind `CentralBallWidget`, behind `ClubHandle`, in front of canvas background. Place this in the hierarchy so it draws first (top of `ShotUI_Canvas` children). **Actually verify Z-order at end — `ConeRoot` has its own draw order; `PutterTrack` should be a sibling of `ConeRoot` and earlier in the sibling order.**
- Initial state: `PutterTrack.SetActive(false)` (only active when putter mode is on).

### Step 2 — Predicted-path renderer + predictor (commit `putter-p1-ui.B`)

**File 1: `Assets/Scripts/Gameplay/UI/ShotUI/PuttPathRenderer.cs`**

Subclass `MaskableGraphic`. Public API:
```csharp
public void SetPath(System.Collections.Generic.List<UnityEngine.Vector2> canvasPoints,
                    System.Collections.Generic.List<float> speedMps);
public bool  HeatmapMode { get; set; }
public float TopSpeedMps { get; set; }    // for heatmap normalization
public float LineWidthPx { get; set; }    // default 6
```

Internal state: cache the `canvasPoints` and `speedMps` lists, call `SetVerticesDirty()` on update.

`OnPopulateMesh`:
- If fewer than 2 points OR cumulative segment length < 1 px, emit no geometry.
- Else: for each segment from `points[i]` to `points[i+1]`:
  - Compute perpendicular direction `perp = Vector2.Perpendicular(dir).normalized * (LineWidthPx / 2)`.
  - Emit a quad (4 verts, 2 triangles) at `(p0 + perp, p0 - perp, p1 - perp, p1 + perp)`.
  - Vertex colors:
    - **Default mode** (`HeatmapMode = false`): blue `#477EC1` family. Alpha lerps from 1.0 at index 0 to 0.2 at index N-1, by `pointIndex / (pointCount - 1)`.
    - **Heatmap mode** (`HeatmapMode = true`): per-point color from `HeatmapColor(speedMps[i] / TopSpeedMps)`. Use the same green→yellow→red palette as `PowerGaugeGraphic.ArcColor`:
      - `t ≤ 0.5`: lerp green → yellow at `t*2`.
      - `t > 0.5`: lerp yellow → red at `(t-0.5)*2`.
      Alpha = 1.0 throughout.

**Scene wiring:**
- Create `PuttPathRoot` GameObject under `ShotUI_Canvas` (peer of `ConeRoot` and `PutterTrack`).
- RectTransform: anchor stretch-stretch (0,0)–(1,1), offsets all 0.
- Add `PuttPathRenderer` component.
- Z-order: in front of track, in front of central ball, in front of club handle. Should be one of the last siblings in `ShotUI_Canvas`.
- Initial state: `PuttPathRoot.SetActive(false)`.

**File 2: `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` (Assembly-CSharp side)**

```csharp
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.UI.HUD
{
    public class PuttPathPredictor : MonoBehaviour
    {
        [SerializeField] private ShotController     _shotController;
        [SerializeField] private Camera             _worldCamera;
        [SerializeField] private Transform          _ballTransform;
        [SerializeField] private PuttPathRenderer   _renderer;
        [SerializeField] private RectTransform      _canvasRect;

        [Header("Re-prediction thresholds")]
        [SerializeField] private float _aimDeltaThresholdDeg = 0.3f;
        [SerializeField] private float _powerDeltaThreshold  = 0.01f;
        [SerializeField] private float _sampleIntervalSec    = 0.05f;

        // Providers — pulled from PhysicsLabController on enable.
        private IGroundProvider  _ground;
        private ISurfaceProvider _surfaces;

        // Cached state for delta detection.
        private float _lastAimYaw;
        private float _lastPower;
        private bool  _hasCache;

        private void OnEnable() {
            _shotController.OnStateChanged += OnState;
            // Pull providers from the lab controller via public accessors.
            // Implementer adds GetGround() / GetSurfaces() to PhysicsLabController.
            var lab = UnityEngine.Object.FindObjectOfType<Golfin.Physics.Viewer.PhysicsLabController>();
            if (lab != null) {
                _ground   = lab.GetGround();
                _surfaces = lab.GetSurfaces();
            }
        }
        private void OnDisable() {
            if (_shotController != null) _shotController.OnStateChanged -= OnState;
            if (_renderer != null) _renderer.SetPath(null, null);  // hide
            _hasCache = false;
        }

        private void OnState(ShotInputState state) {
            if (state.State == ShotState.Idle || state.State == ShotState.Resolving) {
                _renderer.SetPath(null, null);
                return;
            }

            if (_hasCache) {
                float aimDelta   = Mathf.Abs(Mathf.DeltaAngle(_lastAimYaw * Mathf.Rad2Deg,
                                                              state.AimYawRadians * Mathf.Rad2Deg));
                float powerDelta = Mathf.Abs(state.PowerNormalized - _lastPower);
                if (aimDelta < _aimDeltaThresholdDeg && powerDelta < _powerDeltaThreshold)
                    return;
            }

            Predict(state);
            _lastAimYaw = state.AimYawRadians;
            _lastPower  = state.PowerNormalized;
            _hasCache   = true;
        }

        private void Predict(ShotInputState state) {
            // 1. Build StatBundle (with fallback for lab).
            var bundle = BuildBundleOrFallback();

            // 2. Build ShotInput at live aim/power.
            var origin = _ballTransform.position;
            var (input, ballMods) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.FromFloat(Mathf.Clamp01(state.PowerNormalized)),
                fp.FromFloat(state.AimYawRadians),
                fp.FromFloat(origin.x), fp.FromFloat(origin.y), fp.FromFloat(origin.z),
                seed: 42u,
                baseVelocityOverrideMps: fp.FromFloat(Golfin.Gameplay.Config.ControlsConfig.Default.PuttBaseVelocityMps));

            // 3. Run sim.
            var traj = BallSimulation.Simulate(
                input, _ground,
                AeroConfig.Default, WindConfig.Calm,
                _surfaces, SurfaceConfig.Default,
                PuttConfig.Default, ballMods);

            // 4. Sample and project.
            var pts    = new System.Collections.Generic.List<Vector2>();
            var speeds = new System.Collections.Generic.List<float>();
            float nextSampleT = 0f;
            foreach (var s in traj.samples) {
                float t = (float)s.time.ToFloat();
                if (t < nextSampleT) continue;
                nextSampleT += _sampleIntervalSec;

                Vector3 worldPos = new Vector3((float)s.position.x.ToFloat(),
                                               (float)s.position.y.ToFloat(),
                                               (float)s.position.z.ToFloat());
                Vector3 screen   = _worldCamera.WorldToScreenPoint(worldPos);
                if (screen.z < 0f) continue;
                Vector2 canvas;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out canvas);
                pts.Add(canvas);

                Vector3 vel = new Vector3((float)s.velocity.x.ToFloat(),
                                          (float)s.velocity.y.ToFloat(),
                                          (float)s.velocity.z.ToFloat());
                speeds.Add(vel.magnitude);
            }

            // 5. Compute top-speed for heatmap normalization.
            float topSpeed = bundle.IsPutt
                ? (float)bundle.Putter.Value.BaseVelocityMps.ToFloat()
                : 5f;
            _renderer.TopSpeedMps = topSpeed;
            _renderer.SetPath(pts, speeds);
        }

        private StatBundle BuildBundleOrFallback() {
            // Try the singleton path; fall back to DefaultStatProvider if any singleton is null.
            // Implementer fills this in based on actual BagManager / BallManager / CharacterManager APIs.
            return DefaultStatProvider.BuildPuttBundle();
        }
    }
}
```

**Scene wiring:**
- Add `PuttPathPredictor` MonoBehaviour to `LabRoot` GameObject (peer of `ShotController`).
- Wire all 5 Inspector refs.
- Initial state: `PuttPathPredictor.enabled = false` (only on when putter mode is on).

**Performance gate (mandatory):**
- Run Unity Profiler in Play mode; confirm one prediction call (build + sim + project) is **< 2ms** on the Editor's PC build target. If higher, drop `_sampleIntervalSec` to 0.1s and re-measure. If still > 5ms, surface to architect — we'll add re-prediction frequency capping.
- Document the measured `mean / p95 / max` over 60 frames of active aiming in the done report.

### Step 3 — Gauge + HoleIndicator unit swaps + ShotConeView putt-mode gate (commit `putter-p1-ui.C`)

**File: `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs`** — modifications:

```csharp
public enum DistanceUnit { Yards, Meters }

[SerializeField] private DistanceUnit _unitMode = DistanceUnit.Yards;
[SerializeField] private float _maxPuttRangeMeters = 25f;

[FormerlySerializedAs("_yardsText")]
[SerializeField] private TMP_Text _distanceText;

public void SetUnitMode(DistanceUnit u) => _unitMode = u;
public void SetMaxPuttRangeMeters(float m) => _maxPuttRangeMeters = m;
```

In `HandleStateChanged` (replace the yards block):
```csharp
float distance;
string suffix;
if (_unitMode == DistanceUnit.Meters) {
    distance = _maxPuttRangeMeters * state.PowerNormalized;
    suffix = "mts";
} else {
    distance = _maxCarryYards * state.PowerNormalized;
    suffix = "yd";
}
if (_distanceText != null) _distanceText.text = $"{distance:F1} {suffix}";
```

`[FormerlySerializedAs]` preserves the existing scene wiring — no manual re-link in Inspector.

**File: `Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicator.cs`** (or `HoleIndicatorWidget` — implementer verifies):
- Add same `DistanceUnit` enum + `SetUnitMode(DistanceUnit)` API.
- Toggle the suffix between `mts` and `yds` (or whatever the standard suffix is — match existing precision).

**File: `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs`** — add putt-mode gate:
```csharp
private bool _puttMode;
public void SetPuttMode(bool on) {
    _puttMode = on;
    if (_coneGraphic != null) _coneGraphic.enabled = !on;
    if (_targetingLine != null) _targetingLine.gameObject.SetActive(!on);
    // Slab stays enabled — see open question below.
}
```

In `UpdateClubHandle` (modify to gate X by mode):
```csharp
float xOffset = _puttMode ? 0f : (state.ConeFinetuneX * maxX);
_clubHandle.anchoredPosition = new Vector2(xOffset, handleY);
```

In `UpdateTargetingLine` first line: `if (_puttMode) { _targetingLine.gameObject.SetActive(false); return; }` — early exit. The path renderer takes over.

In `UpdateConeWidth`: skip if `_puttMode == true` (cone graphic is disabled anyway, but skip the property set to avoid re-marking dirty).

**File: `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs`** — add putt-mode size:
```csharp
[SerializeField] private float _normalSize    = 80f;
[SerializeField] private float _puttModeSize  = 150f;
[SerializeField] private RectTransform _rect;

public void SetPuttMode(bool on) {
    if (_rect == null) return;
    float s = on ? _puttModeSize : _normalSize;
    _rect.sizeDelta = new Vector2(s, s);
}
```

### Step 4 — Mode toggle wiring + action button row hide + ball lock (commit `putter-p1-ui.D`)

**File: `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`** — additions:

```csharp
// Public accessors for predictor.
public IGroundProvider  GetGround()   => BuildGroundProvider();
public ISurfaceProvider GetSurfaces() => BuildSurfaceProvider(default);

// Inspector wiring for putter UI.
[Header("Putter UI")]
[SerializeField] private GameObject       _putterTrack;
[SerializeField] private GameObject       _puttPathRoot;
[SerializeField] private Behaviour        _puttPathPredictor;        // MonoBehaviour on Assembly-CSharp side
[SerializeField] private GameObject       _actionButtonRowTop;       // SPIN + FADE-DRAW row
[SerializeField] private CanvasGroup      _ballSelectorCanvasGroup;  // for the GOLFIN ball button
[SerializeField] private CentralBallWidget _centralBall;
[SerializeField] private ShotConeView      _shotConeView;             // already wired above
[SerializeField] private PowerGaugeWidget  _powerGaugeWidget;
[SerializeField] private HoleIndicator     _holeIndicator;            // verify class name

private void OnClubChanged(int clubIndex) {
    // Putter is the canonical "club 3" per ClubSelectionBroadcast convention from 8.2.5.
    // Verify by looking at ClubHandleSpriteBinder._sprites array length (4) + which one is the putter.
    bool isPutter = (clubIndex == 3);
    if (isPutter) EnterPutterMode();
    else          ExitPutterMode();
}

private void EnterPutterMode() {
    _shotConeView.SetPuttMode(true);
    _powerGaugeWidget.SetUnitMode(PowerGaugeWidget.DistanceUnit.Meters);
    _powerGaugeWidget.SetMaxPuttRangeMeters(ComputeMaxPuttRangeMeters());
    if (_holeIndicator != null) _holeIndicator.SetUnitMode(HoleIndicator.DistanceUnit.Meters);
    if (_putterTrack != null) _putterTrack.SetActive(true);
    if (_puttPathRoot != null) _puttPathRoot.SetActive(true);
    if (_puttPathPredictor != null) _puttPathPredictor.enabled = true;
    if (_actionButtonRowTop != null) _actionButtonRowTop.SetActive(false);
    if (_ballSelectorCanvasGroup != null) {
        _ballSelectorCanvasGroup.alpha = 0.5f;
        _ballSelectorCanvasGroup.interactable = false;
        _ballSelectorCanvasGroup.blocksRaycasts = false;
    }
    if (_centralBall != null) _centralBall.SetPuttMode(true);
}

private void ExitPutterMode() {
    _shotConeView.SetPuttMode(false);
    _powerGaugeWidget.SetUnitMode(PowerGaugeWidget.DistanceUnit.Yards);
    if (_holeIndicator != null) _holeIndicator.SetUnitMode(HoleIndicator.DistanceUnit.Yards);
    if (_putterTrack != null) _putterTrack.SetActive(false);
    if (_puttPathRoot != null) _puttPathRoot.SetActive(false);
    if (_puttPathPredictor != null) _puttPathPredictor.enabled = false;
    if (_actionButtonRowTop != null) _actionButtonRowTop.SetActive(true);
    if (_ballSelectorCanvasGroup != null) {
        _ballSelectorCanvasGroup.alpha = 1f;
        _ballSelectorCanvasGroup.interactable = true;
        _ballSelectorCanvasGroup.blocksRaycasts = true;
    }
    if (_centralBall != null) _centralBall.SetPuttMode(false);
}

private float ComputeMaxPuttRangeMeters() {
    // Mirror ComputeMaxCarryYards but for putter on flat green.
    var bundle = DefaultStatProvider.BuildPuttBundle();
    var (input, ballMods) = ShotInputBuilder.Build(
        bundle, StatCoefficients.Default, StatCaps.Default,
        fp.One,           // 100% power
        fp.Zero,          // aim straight
        fp.Zero, fp.Zero, fp.Zero,  // origin
        seed: 0,
        baseVelocityOverrideMps: fp.FromFloat(ControlsConfig.Default.PuttBaseVelocityMps));
    var traj = BallSimulation.Simulate(
        input, new FlatGround(),
        AeroConfig.Default, WindConfig.Calm,
        new ConstantSurfaceProvider(SurfaceType.Green), SurfaceConfig.Default,
        PuttConfig.Default, ballMods);
    return Vector3.Distance(Vector3.zero,
        new Vector3((float)traj.finalPosition.x.ToFloat(),
                    (float)traj.finalPosition.y.ToFloat(),
                    (float)traj.finalPosition.z.ToFloat()));
}
```

Subscribe `OnClubChanged` to `ClubSelectionBroadcast.OnChanged` in `Awake()`. Unsubscribe in `OnDestroy()`. Initial mode: Yards (we open the lab in driver/iron/wedge mode by default).

**File: `Assets/Scripts/Gameplay/Input/ShotDebugFlags.cs`** — add field:
```csharp
public bool PuttPathHeatmap;
```

**File: `Assets/Scripts/Debug/PhysicsLabUI.cs`** — add toggle button under existing Debug Flags foldout:
```csharp
DrawToggle("Putt Path Heatmap", ref _shotController.DebugFlags.PuttPathHeatmap);
```

**Wire `PuttPathPredictor` to read the heatmap flag**: in `Predict()`, set `_renderer.HeatmapMode = _shotController.DebugFlags.PuttPathHeatmap;` before calling `SetPath`.

### Step 5 — Visual diff + done report

Take a play-mode screenshot of `LabScaffold.unity` with Hole 6 loaded, putter selected, ball placed on the green 5-10 meters from the cup. Aim across the slope so the predicted path curves visibly. Save to `screenshots/putter-mode-diff-v1.png`. Compare side-by-side with `screenshots/figma-reference.png`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. Self-reviewer will reject reports with unjustified items.

- [ ] Top bar identical to standard mode (PlayerCard left, HoleCard right, Settings gear at standard positions).
- [ ] HoleIndicator distance text reads `mts` (e.g. `5 mts`) when in putter mode.
- [ ] Cone graphic hidden (`_coneGraphic.enabled == false`) when putter mode active.
- [ ] Putter track 140 wide × 1000 tall, anchored center, top at canvas y=-1453 (verify with screenshot ruler).
- [ ] Track gradient renders correctly: lighter at left/right edges, darker at center.
- [ ] Three band lines visible at heights 200 / 500 / 1000 from track top, in green / amber / red (`#627352` / `#8F7240` / `#7A3E3E`).
- [ ] Putter handle sprite shows correctly (`S_Controls_Putter_VBOOOT 1.png` via `ClubHandleSpriteBinder`).
- [ ] Handle Y slides with power (verify visually: 0% power = handle near top, 100% power = handle near bottom).
- [ ] Handle X locked at 0 in putter mode (no fine-tune drift).
- [ ] Central ball renders at 150×150 in putter mode (vs 80×80 in standard).
- [ ] Power gauge text shows `mts` suffix (e.g. `12.5 mts`).
- [ ] Power gauge max value at 100% power ≈ `ComputeMaxPuttRangeMeters` output (likely ~25m on flat green).
- [ ] Predicted-path line renders as a polyline (multiple segments visible).
- [ ] Predicted-path line curves when aim is not parallel to slope direction (verify on a sloped section of green).
- [ ] Predicted-path line terminates at the predicted stop position (not at screen edge or a fixed length).
- [ ] Default mode (heatmap OFF): line shows blue gradient, alpha 1.0 at ball end → 0.2 at end.
- [ ] Heatmap mode (debug toggle ON): line shows green→yellow→red speed-coded segments.
- [ ] Power=0 case: predicted-path line hides (no degenerate dot).
- [ ] Top action button row (SPIN + FADE-DRAW) hidden in putter mode.
- [ ] Bottom action button row visible in putter mode.
- [ ] Ball selector at 50% alpha, non-interactable, raycasts blocked.
- [ ] Putter selector fully opaque, fully interactable.
- [ ] Switching to a non-putter club exits putter mode (cone reappears, track hides, gauge reverts to yards).
- [ ] No white-box placeholders visible in the screenshot.
- [ ] All `[SerializeField]` references wired in the Inspector.
- [ ] Unity Console has no errors related to this task.
- [ ] Performance: prediction call mean < 2ms over 60 frames of active aiming. (Document `mean / p95 / max` in done report.)
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/PutterTrackGraphic.cs` | NEW — track `MaskableGraphic` |
| `Assets/Scripts/Gameplay/UI/ShotUI/PuttPathRenderer.cs` | NEW — polyline `MaskableGraphic` |
| `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` | NEW (Assembly-CSharp side) — predictor MonoBehaviour |
| `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs` | MODIFY — add `DistanceUnit`, `SetUnitMode`, `SetMaxPuttRangeMeters`, rename `_yardsText` → `_distanceText` with `[FormerlySerializedAs]` |
| `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` | MODIFY — add `_puttModeSize` + `SetPuttMode(bool)` |
| `Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicator.cs` | MODIFY — add `DistanceUnit` enum + `SetUnitMode` (verify class name) |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` | MODIFY — add `_puttMode` field + `SetPuttMode(bool)` API + gate handle X / cone visibility / targeting line |
| `Assets/Scripts/Gameplay/Input/ShotDebugFlags.cs` | MODIFY — add `bool PuttPathHeatmap` |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | MODIFY — public `GetGround` / `GetSurfaces`, `OnClubChanged` subscribe, `EnterPutterMode` / `ExitPutterMode`, `ComputeMaxPuttRangeMeters` |
| `Assets/Scripts/Debug/PhysicsLabUI.cs` | MODIFY — add Putt Path Heatmap toggle button |
| `Assets/Scenes/Physics/LabScaffold.unity` | MODIFY — add `PutterTrack`, `PuttPathRoot`, `PuttPathPredictor` GameObjects; wire all Inspector refs |
| `Docs/Architecture/RUNTIME_BLUEPRINT.md` | MODIFY — add §5 entry on putter mode toggle, predictor placement decision, gauge unit-mode flag |

## Out of scope (do NOT do these)

- **DO NOT** modify `BallSimulation.cs`, anything in `Physics/Core/`, `Physics/Math/`, `Physics/Stats/StatModifierResolver.cs`, or `Physics/Stats/ShotInputBuilder.cs`. We CALL them; we don't touch them.
- **DO NOT** touch the camera. Cesar 2026-05-01: "Leave camera for last. First we use what we have." Camera lock to green / overhead view is a follow-up task. Putter P1 uses whatever camera mode the scene is currently in.
- **DO NOT** invent a new "putter scene" — this is a state-mode toggle in `LabScaffold.unity`, not a new scene.
- **DO NOT** implement the "reset to default ball if all balls lost" semantics — that's a Loop v1 concern. Putter P1 only locks the ball selector visually.
- **DO NOT** introduce third-party libraries (no DOTween, no LineRenderer-extra packages). uGUI + TMP + procedural meshes only.
- **DO NOT** thrash `Resources.Load` in the predictor — cache on `OnEnable`.
- **DO NOT** scope-creep into Phase 2 in-context tuning (that's a Loop v1 task per Roadmap §1).
- **DO NOT** silently invent resolutions to ambiguities — surface to architect via `IMPLEMENTER_REPORT.md` § Open questions. Specifically the three known questions: `putt_mode_club_lock_decision`, `putt_slab_visual_decision`, `putt_predictor_perf`.
