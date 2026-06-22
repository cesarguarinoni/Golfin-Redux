using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
// iter-31: UnityEngine.Rendering.Universal removed — DecalProjector replaced by ZTest=Always flat disc
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Course.Runtime;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// MapViewController — Order 352 v2 (ESCALATION RESET 2026-06-19).
    ///
    /// v1 WITHDRAWN. This is a clean rewrite. All RenderTexture / RawImage / uvRect
    /// code has been removed. The banned patterns are NOT re-introducible:
    ///
    ///   BANNED (hard — do NOT re-introduce):
    ///     - RenderTexture / targetTexture on any map camera
    ///     - RawImage displaying a render texture
    ///     - uvRect = Rect(0,1,1,-1) or any Y-flip patch
    ///     - yflip_repair.py or any post-process flip
    ///     - PrewarmRT() (deleted — only needed for RT path)
    ///     - _holeMapButton serialized field (entry is via HoleCardWidget only)
    ///     - Flag.fbx 18x spawner
    ///
    /// Architecture (v2):
    ///   - Entry: HoleCardWidget.OpenMapView() → FindObjectOfType&lt;MapViewController&gt;().OpenViaWidget()
    ///     (PIPELINE_HARDENING Rule 2 — no synthetic bypass)
    ///   - Camera: runtime overlay Camera, depth=10 (above gameplay cam), NO targetTexture.
    ///     Renders directly to screen, tag="MapViewCam" for Unity Recorder TaggedCamera.
    ///   - Orientation: camera positioned BEHIND ball, looking toward flag.
    ///     §11 invariant: ball.screenY > flag.screenY (ball at bottom, flag at top).
    ///     Default aim: TOWARD the flag (not the stale ShotController heading) so all markers
    ///     are in-viewport at open time.
    ///   - Camera framing: bounds-fit to include ball + flag + landing zone (1.25× padding)
    ///     so ALL markers project inside the 1170×2532 viewport.
    ///   - Rings: terrain-conforming annulus meshes — vertex Y sampled via Physics.Raycast.
    ///   - Landing zone: ZTest=Always flat disc (iter-31). Renders ON TOP of all geometry —
    ///     terrain AND trees — never occluded. Red→green radial gradient texture.
    ///   - Pin indicator: screen-space Canvas flag icon + world-space LineRenderer.
    ///     NOT Flag.fbx (removed).
    ///   - Aim write-back: ShotController.CameraHeadingRadians = _aimYawRadians on Close().
    ///   - §11 invariant JSON: written to BOTH Application.persistentDataPath AND the repo task
    ///     folder (Docs/Specs/Active/map_view_aiming/) at Open and after re-aim.
    ///   - screenSize in JSON: hardcoded [1170,2532] (device res), NOT Screen.width/height.
    ///     Screen coords scaled to device res coordinate space.
    ///   - >=2 distinct aim states written: "open" (initial) and "aimed" (after re-aim toward flag).
    ///
    /// Input: UnityEngine.InputSystem.Touchscreen + Mouse (CLAUDE.md rule).
    /// </summary>
    [AddComponentMenu("Golfin/UI/Map View Controller")]
    public class MapViewController : MonoBehaviour
    {
        // ── Inspector wires (shot seams + SHOOT button) ─────────────────────────
        [Header("Shot seams (wire in Inspector)")]
        [SerializeField] private ShotController    _shotController;
        [SerializeField] private ShotConeView      _shotConeView;

        [Header("SHOOT button (wire in Inspector)")]
        [SerializeField] private Button            _shootButton;
        [SerializeField] private TMP_Text          _shootButtonLabel;

        [Header("UI to hide on open (wire in Inspector — optional override)")]
        [SerializeField] private GameObject[]      _hideOnMapOpen;

        [Header("Map camera tuning")]
        [SerializeField] private float             _heroTiltDeg    = 70f;  // iter-30: increased from 55° to eliminate sky/void in aimed-north state
        [SerializeField] private float             _fieldOfView    = 75f;  // wide-angle for tight fit
        [SerializeField] private float             _nearClip       = 0.3f;
        [SerializeField] private float             _farClip        = 2000f;
        [SerializeField] private float             _cameraHeight   = 80f;  // kept for reference, not used in framing
        /// <summary>
        /// Initial FOV (field of view in degrees) when the map first opens.
        /// LOWER value = more zoomed IN (tighter framing, less world visible).
        /// HIGHER value = more zoomed OUT (wider world visible).
        /// Tunable in Inspector. Clamped to [_minZoom, _maxZoom] at runtime.
        /// Default 45° is more zoomed-in than the old hardcoded 55° minimum.
        /// </summary>
        [SerializeField] private float             _initialZoom    = 45f;

        [Header("Zoom / pan")]
        [SerializeField] private float             _minZoom        = 30f;
        [SerializeField] private float             _maxZoom        = 90f;
        [SerializeField] private float             _panSensitivity   = 0.15f;
        [SerializeField] private float             _pinchSensitivity = 0.05f;

        // ── Device resolution (invariant gate uses this, NOT Screen.width/height) ─
        // iPhone 14 portrait = 1170×2532 px (the device target).
        // All screen-coord assertions in DumpInvariants are scaled to this space.
        private const int kDeviceW = 1170;
        private const int kDeviceH = 2532;

        // ── Injected position sources (set by PhysicsLabController) ─────────────
        private Transform _ballPositionSource;
        private Transform _flagPositionSource;

        // ── Runtime objects (ALL created in Open, ALL destroyed in CloseImmediate) ─
        // v2: direct overlay Camera — NO RenderTexture, NO RawImage, NO targetTexture.
        private Camera              _mapCam;
        private GameObject          _runtimeRoot;

        // ── Camera (created ONLY in Open()/BuildRuntimeObjects, destroyed in CloseImmediate) ─
        // iter-27: NO pre-created camera. The map cam exists ONLY while the map is open —
        // the exact real-player lifecycle. No cam GO before map open, none after map close.
        private GameObject          _staticCamGO;
        // s_staticCamGOShared REMOVED in iter-27 (was guard for pre-created cam — now gone)

        // World-space markers
        private Transform           _ballMarker;
        private Transform           _landingZone;
        private LineRenderer        _guideLine;

        // Terrain-conforming rings (vertex-height-sampled annulus meshes)
        private GameObject          _ring80GO;
        private GameObject          _ring100GO;
        private GameObject          _ring120GO;

        // Hole indicator: screen-space Canvas icon (§iter-26: yellow line removed)
        private Canvas              _indicatorCanvas;
        private RectTransform       _flagIconRT;

        // Screen-space labels
        private Canvas              _labelCanvas;
        private TextMeshProUGUI     _label80;
        private TextMeshProUGUI     _label100;
        private TextMeshProUGUI     _label120;

        // ── Runtime state ────────────────────────────────────────────────────────
        private bool                _isOpen;
        private float               _aimYawRadians;
        private float               _savedAimYaw;
        private float               _teeDefaultAimYaw;  // §iter-26 FIX #3: authoritative tee→green aim from GetDefaultLookDirection()
        private Vector3             _ballWorldPos;
        private Vector3             _flagWorldPos;
        private string              _flagWorldPos_source = "unresolved"; // §iter-23 for invariant dump
        private float               _carryYards;
        private bool                _carryValid;
        private float               _currentFov;
        private Vector3             _camFocusPoint;
        private float               _fadeDrawFinetune;
        private bool                _fadeDrawArmed;
        private float               _curveScale;
        private float               _ringFrac;        // §6-MODEL: carry * _ringFrac * (p/100) = ring radius for p∈{80,100,120}
        private string              _savedShootLabel;
        private List<GameObject>    _hiddenObjects      = new List<GameObject>();
        private List<CanvasGroup>   _hiddenCanvasGroups = new List<CanvasGroup>();
        // Gameplay ball renderers hidden while map is open (Fix 4 — cull shot-UI ball).
        private List<Renderer>      _hiddenBallRenderers = new List<Renderer>();
        // UI Image components disabled while map is open (Fix 4b — CentralBall is an Image widget,
        // not a Renderer; and CentralBallWidget.HandleStateChanged re-enables the GO via C# event
        // even after SetActive(false), so we ALSO disable the Image AND the MonoBehaviour to prevent
        // OnEnable from re-enabling the Image via RefreshSprite()).
        private List<Image>             _hiddenImages       = new List<Image>();
        private List<MonoBehaviour>     _hiddenBehaviours   = new List<MonoBehaviour>();

        // Touch input tracking
        private Vector2             _lastTouchPos0;
        private Vector2             _lastTouchPos1;
        private float               _lastPinchDist;
        private bool                _isDragging;
        private bool                _isPinching;

        // iter-28 Fix 2: 2D aim drag — horizontal-only heading.
        // _dragStartScreenPos: screen position (pixels) where the drag began.
        // _dragStartAimYaw:    _aimYawRadians snapshot at drag start.
        // _verticalLandingOffset: VISUAL-ONLY offset along aim line for landing indicator (future use).
        //   Horizontal screen delta → _aimYawRadians change.
        //   Vertical screen delta   → _verticalLandingOffset only (does NOT change heading or shot).
        private Vector2             _dragStartScreenPos;
        private float               _dragStartAimYaw;
        private float               _verticalLandingOffset;

        // §11 invariant gate — repo path written at open
        private string              _invariantPath;        // persistentDataPath/...json
        private string              _repoInvariantDir;     // repo task folder (set from build path)
        private bool                _entryViaRealWidget;   // true only when called from HoleCardWidget
        private bool                _secondStateDumped;    // ensures we write the "aimed" state

        // ── Constants ────────────────────────────────────────────────────────────
        private const float kYardsToMeters  = 0.9144f;
        private const int   kGuideSegments  = 24;
        private const float kRingHeightOff  = 0.08f;
        private const float kBallMarkerSz   = 2.5f;
        private const float kLabelFontSize  = 38f;
        private const float kRingBandFrac   = 0.06f;
        private const float kRingAlpha      = 0.40f;
        private const int   kRingSegments   = 64;
        private const string kCamTag        = "MapViewCam";

        // Landing zone — iter-31: ZTest=Always flat disc (renders ON TOP of ALL geometry).
        // REMOVED: DecalProjector (_landingZoneDecalProjector) — replaced by flat disc + ZTest=Always.
        // DecalProjector did REALISTIC occlusion (clips behind trees/terrain) — the OPPOSITE of desired.
        private Material            _landingZoneMat;
        private Texture2D           _landingZoneTex;
        private float               _landingZoneRadiusM; // world-space radius (metres), set in BuildLandingZoneDecal
        // iter-32: landing zone is now a TERRAIN-CONFORMING mesh (per-vertex terrain height) drawn with a
        // genuine ZTest=Always shader (MapView/OverlayConform) so it both hugs the ground AND renders over trees.
        private Mesh                _landingMesh;
        private MeshFilter          _landingMeshFilter;
        // iter-32: free touch-follow aim. _aimedCarryM = the player-placed landing distance (metres) — the
        // finger's ground point sets BOTH heading and distance directly (no H/V hardwiring). Free, unclamped.
        private float               _aimedCarryM = -1f; // <0 = uninitialised (defaults to club carry on Open)

        // §iter-24 FIX #14: Frame-readback RGBA samples (set by DoFrameReadbackAndDump coroutine).
        // These hold the on-screen composited color at the blob CENTER and EDGE screen positions,
        // NOT the source texture pixel (which was always "red" by design).
        private Color               _lzFrameCenter = Color.clear;
        private Color               _lzFrameEdge   = Color.clear;

        // Cull mask: exclude UI (5) and IgnoreRaycast (2).
        private int BuildCullMask() => ~((1 << 5) | (1 << 2));

        // ── Public read-only state (used by MapViewCaptureDriver) ─────────────────
        public bool  IsOpen        => _isOpen;
        public float AimYawRadians => _aimYawRadians;

        /// <summary>
        /// PrewarmRT — NO-OP in v2.  v1 pre-allocated a RenderTexture to avoid a Metal
        /// Y-flip on first recording frame.  v2 uses a direct-to-screen overlay camera
        /// (no RenderTexture), so this warm-up step is unnecessary.  Kept public so
        /// MapViewCaptureDriver (which still calls it) compiles without modification.
        /// </summary>
        public void PrewarmRT()
        {
            Debug.Log("[MapView v2] PrewarmRT() — no-op (v2 has no RenderTexture).");
        }

        // ── Public events ─────────────────────────────────────────────────────────
        public event Action<float> OnMapOpened;
        public event Action<float> OnMapClosed;

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            _currentFov   = _fieldOfView;
            _curveScale   = ControlsConfig.Default.AimLineCurveScale;
            _ringFrac     = ControlsConfig.Default.RingFrac;
            _invariantPath = Path.Combine(Application.persistentDataPath, "map_view_invariants.json");

            // Determine repo task folder for writing JSON into the repo (not only persistentDataPath).
            // In Editor, Application.dataPath = .../GolfinRedux/Assets — walk up two dirs.
            // In player builds this won't resolve cleanly, but invariant verification is Editor-only.
            _repoInvariantDir = "";
            try
            {
                string assetsPath = Application.dataPath;       // .../GolfinRedux/Assets
                string repoRoot   = Path.GetDirectoryName(assetsPath); // .../GolfinRedux
                string candidate  = Path.Combine(repoRoot, "Docs", "Specs", "Active", "map_view_aiming");
                if (Directory.Exists(candidate))
                    _repoInvariantDir = candidate;
                else
                    Debug.LogWarning($"[MapView v2] Repo task folder not found at {candidate} — will write invariant to persistentDataPath only.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MapView v2] Could not resolve repo invariant dir: {ex.Message}");
            }

            // iter-27: NO pre-created camera in Awake.
            // The map camera (_staticCamGO / _mapCam) is created ONLY in BuildRuntimeObjects()
            // when the player taps to open the map, and destroyed in DestroyRuntimeObjects()
            // when the map closes. This is the exact real-player lifecycle — no map camera
            // GO exists before the player opens the map or after they close it.
            Debug.Log("[MapView v2] Awake() — no pre-created cam (iter-27 real-player lifecycle).");
        }

        private void OnDestroy()
        {
            // iter-27: _staticCamGO is created in BuildRuntimeObjects (parented under _runtimeRoot),
            // destroyed in DestroyRuntimeObjects. OnDestroy guard: if still alive (e.g. script
            // removed mid-play), clean up defensively.
            if (_staticCamGO != null)
            {
                Destroy(_staticCamGO);
                _staticCamGO = null;
            }
            if (_landingZoneTex != null) Destroy(_landingZoneTex);
            if (_landingZoneMat != null) Destroy(_landingZoneMat);
            if (_landingMesh    != null) Destroy(_landingMesh);
        }

        private void OnDisable()
        {
            if (_isOpen) CloseImmediate();
        }

        // ── Injection API ─────────────────────────────────────────────────────────
        public void SetBallPositionSource(Transform ballTransform)
            => _ballPositionSource = ballTransform;

        public void SetFlagPositionSource(Transform flagTransform)
            => _flagPositionSource = flagTransform;

        // ── Open / Close ──────────────────────────────────────────────────────────
        /// <summary>
        /// Entry point for the REAL HoleCardWidget button click.
        /// Sets _entryViaRealWidget=true before opening so §11 assertion is correct.
        /// HoleCardWidget MUST call this, not Open().
        /// </summary>
        public void OpenViaWidget()
        {
            _entryViaRealWidget = true;
            Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            if (_shotController == null)
            {
                Debug.LogWarning("[MapView v2] No ShotController wired — cannot open.");
                return;
            }

            // § criterion 10 — bot-turn guard.
            if (GameSession.IsVersus && MatchContext.ActiveIndex != 0)
            {
                Debug.LogWarning("[MapView v2] Bot-turn guard: map blocked — it is not the local player's turn.");
                return;
            }

            _isOpen           = true;
            _secondStateDumped = false;

            // Snapshot shot state.
            // §iter-26 FIX #3: source the INITIAL aim from the authoritative tee→green look
            // direction (PhysicsLabController.GetDefaultLookDirection() via reflection).
            // This is stable across bot runs — it is the direction from the tee to the green,
            // not the live chase-camera forward which may have drifted.
            // Fallback chain: GetDefaultLookDirection → chase cam → ShotController heading.
            // iter-33 (Cesar): the map must open at the player's CURRENT aim, NOT the initial tee
            // direction. Reopening after aiming elsewhere in the shot view must reflect where you're
            // now aiming. Primary = the live chase-camera forward (the current shot-view aim); then the
            // ShotController's current heading; the tee default is only a last-ditch fallback.
            float chaseCamYaw   = TryGetChaseCameraYaw();   // current shot-view aim (live)
            float teeDefaultYaw = TryGetTeeDefaultAimYaw(); // initial tee→green (fallback only)
            _teeDefaultAimYaw   = teeDefaultYaw;
            _savedAimYaw = !float.IsNaN(chaseCamYaw)            ? chaseCamYaw
                         : (_shotController != null)            ? _shotController.CameraHeadingRadians
                         : teeDefaultYaw;
            Debug.Log($"[MapView v2] Open() aim source: teeDefaultYaw={teeDefaultYaw:F4}rad " +
                      $"chaseCamYaw={chaseCamYaw:F4}rad " +
                      $"fallback={_shotController.CameraHeadingRadians:F4}rad → _savedAimYaw={_savedAimYaw:F4}rad");
            _fadeDrawArmed    = _shotController.FadeDrawActive;
            _fadeDrawFinetune = _fadeDrawArmed ? _shotController.ConeFinetune : 0f;
            // iter-28 Fix 1: Start at _initialZoom (default 45°, more zoomed-in than old 55° minimum).
            // The bounds-fit in PositionMapCamera will expand FOV only if markers go off-screen.
            _currentFov       = Mathf.Clamp(_initialZoom, _minZoom, _maxZoom);

            // Carry from selected club (Fix 1 — per-club carry, not driver-locked).
            // ClubContext.SelectedDistance is the per-club carry in yards (same value
            // the club button shows in the HUD). ShotConeView.MaxCarryYardsForMap was
            // driver-locked (~141m) regardless of actual club — that was the root cause
            // of iter-18's giant rings and off-field camera.
            int clubDist = Golfin.Gameplay.UI.HUD.ClubContext.SelectedDistance;
            if (clubDist > 0)
                _carryYards = (float)clubDist;
            else if (_shotConeView != null && _shotConeView.MaxCarryYardsForMap > 0f)
                _carryYards = _shotConeView.MaxCarryYardsForMap;   // graceful fallback
            else
                _carryYards = 100f;   // last-resort fallback: 100yd short approach

            _carryValid = _carryYards > 0f && _carryYards < 900f;
            Debug.Log($"[MapView v2] Carry source: ClubContext.SelectedDistance={clubDist}yds → _carryYards={_carryYards:F1}yds");

            SnapshotWorldPositions();

            // §iter-23 FIX #2 (continued): _savedAimYaw now comes from chase-camera forward.
            // Single-endpoint L is always ball + aimDir·carry; PositionMapCamera (bounds-fit)
            // keeps L in-frame. The iter-18 flag-aim override (AimYawTowardFlag) was removed.
            _aimYawRadians = _savedAimYaw;
            _aimedCarryM   = -1f; // iter-32: reset so the landing defaults to the club carry on each open

            BuildRuntimeObjects();
            HideShotUIChrome();
            RepurposeShootButton(true);
            PositionMapCamera();  // uses _ballWorldPos + _flagWorldPos
            PlaceMarkers();
            UpdateGuideAndRings();

            OnMapOpened?.Invoke(_aimYawRadians);
            // §iter-24 FIX #14: Start coroutine so DumpInvariants reads COMPOSITED FRAME pixels
            // (ReadPixels after WaitForEndOfFrame) rather than source texture (GetPixel).
            StartCoroutine(DoFrameReadbackAndDump("open"));

            Debug.Log($"[MapView v2] Opened. Ball={_ballWorldPos:F1} Flag={_flagWorldPos:F1} " +
                      $"Carry={_carryYards:F1}yds Aim={_aimYawRadians:F3}rad ({_aimYawRadians*Mathf.Rad2Deg:F1}°)");
        }

        /// <summary>
        /// Compute the aim yaw (radians) that points from ball to flag.
        /// Convention: aimYaw=0 → +X, aimYaw=π/2 → +Z (matches ShotInputBuilder).
        /// </summary>
        private float AimYawTowardFlag()
        {
            Vector3 diff = _flagWorldPos - _ballWorldPos;
            diff.y = 0f;
            if (diff.sqrMagnitude < 0.01f)
                return _savedAimYaw;
            return Mathf.Atan2(diff.z, diff.x);
        }

        public void Close()
        {
            if (!_isOpen) return;
            float chosen = _aimYawRadians;
            CloseImmediate();
            OnMapClosed?.Invoke(chosen);
        }

        private void CloseImmediate()
        {
            _isOpen = false;
            _entryViaRealWidget = false;  // reset for next open

            // Write aim back to ShotController.
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _aimYawRadians;

            // Try PhysicsLab write-back as well (handles LabScaffold context).
            WriteBackAimToPhysicsLab(_aimYawRadians);

            RestoreShotUIChrome();
            RepurposeShootButton(false);
            DestroyRuntimeObjects();
        }

        // ── Build runtime objects (v2: overlay camera, no RT) ─────────────────────
        private void BuildRuntimeObjects()
        {
            _runtimeRoot = new GameObject("MapView_RuntimeRoot");
            _runtimeRoot.transform.SetParent(null);

            // ── 1. Overlay Camera ────────────────────────────────────────────────
            // iter-27: Camera created HERE (first open) or reused if already exists.
            // It lives under _runtimeRoot → destroyed in DestroyRuntimeObjects().
            // NO DontDestroyOnLoad: this is a scene object, lifespan = map open → map close.
            // HARD GUARANTEE per §1 ban: targetTexture stays null.
            if (_staticCamGO == null)
            {
                _staticCamGO     = new GameObject("MapView_Cam_Static");
                _staticCamGO.tag = kCamTag;
                // iter-27: NO DontDestroyOnLoad — cam is a normal scene object under _runtimeRoot.
                _staticCamGO.transform.SetParent(_runtimeRoot.transform);
                _staticCamGO.AddComponent<Camera>();
                var al = _staticCamGO.GetComponent<AudioListener>();
                if (al != null) Destroy(al);
            }
            _mapCam = _staticCamGO.GetComponent<Camera>();
            // Fix 3 — no Skybox grey: use SolidColor with a dark near-black green background.
            // The SolidColor fills the framebuffer before drawing hole geometry, so
            // off-hole areas show the background color, NOT a distracting sky / grey void.
            _mapCam.clearFlags     = CameraClearFlags.SolidColor;
            _mapCam.backgroundColor = new Color(0.04f, 0.07f, 0.04f, 1f);  // near-black dark green
            _mapCam.fieldOfView    = _currentFov;
            _mapCam.nearClipPlane  = _nearClip;
            _mapCam.farClipPlane   = _farClip;
            _mapCam.depth          = 10f;
            _mapCam.cullingMask    = BuildCullMask();
            _mapCam.targetTexture  = null;  // EXPLICIT: no RT
            _mapCam.enabled        = true;

            // iter-31: requiresDepthTexture block REMOVED (was only needed for DecalProjector).
            // ZTest=Always flat disc does not use depth projection — no URP camera data needed.

            // ── 2. Marker parent ─────────────────────────────────────────────────
            var markerRoot = new GameObject("MapView_Markers");
            markerRoot.transform.SetParent(_runtimeRoot.transform);

            // Ball marker (small white sphere).
            _ballMarker = BuildSphereMarker("BallMarker", markerRoot.transform, Color.white, kBallMarkerSz);

            // Landing zone: shader-driven radial gradient projected on ground.
            // URP DecalProjector is unavailable (DecalRendererFeature not enabled in any URP asset).
            // Instead: thin disc mesh on the ground with a procedural radial-gradient Texture2D
            // applied via Unlit/Transparent shader — gives the "hot-spot" radial gradient look.
            BuildLandingZoneDecal(markerRoot.transform);

            // Guide line: ball → carry target.
            var glGO = new GameObject("MapView_GuideLine");
            glGO.transform.SetParent(markerRoot.transform);
            _guideLine = glGO.AddComponent<LineRenderer>();
            var glMat  = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            glMat.color = new Color(0.3f, 0.85f, 1f, 0.92f);
            _guideLine.material          = glMat;
            _guideLine.startWidth        = 1.8f;
            _guideLine.endWidth          = 1.8f;
            _guideLine.positionCount     = kGuideSegments + 1;
            _guideLine.useWorldSpace     = true;
            _guideLine.receiveShadows    = false;
            _guideLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _guideLine.sortingOrder      = 2;

            // iter-28 Fix 3: Rings COMMENTED OUT (restorable — do NOT delete).
            // The 80/100/120 ring GOs clutter the view; remove for this iteration.
            // Ring radius CALCULATIONS (r80, r100, r120) in UpdateGuideAndRings are KEPT
            // so DumpInvariants still reports ring values and ring EditMode tests still pass
            // (RingCenterAtPct / RingRadiusAtPct are pure-math seams, no GO dependency).
            // To restore: uncomment these 4 lines.
            // var ringColor = new Color(0.08f, 0.08f, 0.08f, 0.55f);
            // _ring80GO  = BuildConformingRingGO("Ring80",  ringColor, markerRoot.transform);
            // _ring100GO = BuildConformingRingGO("Ring100", ringColor, markerRoot.transform);
            // _ring120GO = BuildConformingRingGO("Ring120", ringColor, markerRoot.transform);

            // Hole indicator: flag icon + world-space line.
            BuildHoleIndicator(markerRoot.transform);

            // ── 3. Screen-space label canvas ─────────────────────────────────────
            var labelCanvasGO = new GameObject("MapView_LabelCanvas");
            labelCanvasGO.transform.SetParent(_runtimeRoot.transform);
            _labelCanvas = labelCanvasGO.AddComponent<Canvas>();
            _labelCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _labelCanvas.sortingOrder = 15;
            labelCanvasGO.AddComponent<CanvasScaler>();

            // iter-28 Fix 3: Ring labels COMMENTED OUT (restorable).
            // To restore: uncomment these 3 lines.
            // _label80  = BuildScreenLabel(_labelCanvas.transform, "80%");
            // _label100 = BuildScreenLabel(_labelCanvas.transform, "100%");
            // _label120 = BuildScreenLabel(_labelCanvas.transform, "120%");
        }

        // ── Landing zone: ZTest=Always flat disc (iter-31) ───────────────────────
        /// <summary>
        /// iter-31: Landing zone is a flat disc (XZ plane) with ZTest=Always (CompareFunction.Always=8).
        /// ZTest=Always disables depth testing entirely — the disc renders ON TOP of all geometry:
        /// terrain, trees, buildings — it is NEVER occluded or clipped behind anything.
        ///
        /// Architecture:
        ///   - A fan-triangulated mesh disc (48 segments) flat on XZ plane.
        ///   - Sprites/Default shader with _ZTest = 8 (CompareFunction.Always).
        ///   - Procedural radial-gradient texture: red hot center → orange mid → green transparent edge.
        ///   - Position: L.y + 0.1m (hovers just above terrain, but ZTest=Always means depth irrelevant).
        ///   - renderQueue = 3001 (Transparent+1).
        ///
        /// NOTE: DecalProjector (iter-28/29/30) was REMOVED. It did realistic depth occlusion
        /// (clips/hides behind trees and terrain) — the OPPOSITE of "always visible".
        /// §11 invariant: lzMatZTest MUST be 8 (Always).
        /// </summary>
        private void BuildLandingZoneDecal(Transform parent)
        {
            // ── Build the radial gradient texture ────────────────────────────────
            int res = 128;
            _landingZoneTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            _landingZoneTex.wrapMode = TextureWrapMode.Clamp;
            _landingZoneTex.filterMode = FilterMode.Bilinear;

            // Red/orange HOT CENTER → green edge gradient (kept from prior iters).
            Color centerColor = new Color(1f, 0.05f, 0.02f, 1.0f);   // red center, full-alpha
            Color midColor    = new Color(0.95f, 0.45f, 0.0f, 0.88f); // orange mid
            Color edgeColor   = new Color(0.0f, 0.85f, 0.1f, 0.0f);  // green, transparent at edge

            Color[] pixels = new Color[res * res];
            Vector2 texCenter = new Vector2(res * 0.5f, res * 0.5f);
            float halfRes  = res * 0.5f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), texCenter) / halfRes;
                    dist = Mathf.Clamp01(dist);
                    Color c;
                    if (dist < 0.65f)
                        c = Color.Lerp(centerColor, midColor, dist / 0.65f);
                    else
                        c = Color.Lerp(midColor, edgeColor, (dist - 0.65f) / 0.35f);
                    pixels[y * res + x] = c;
                }
            }
            _landingZoneTex.SetPixels(pixels);
            _landingZoneTex.Apply();

            // ── Compute landing zone radius ───────────────────────────────────────
            float carryM    = _carryValid ? (_carryYards * kYardsToMeters) : 80f;
            float r80_local = Mathf.Max(carryM * _ringFrac * 0.80f, 4f);
            float landRadius = Mathf.Clamp(r80_local, 5f, 18f);
            _landingZoneRadiusM = landRadius;

            // ── iter-32: terrain-CONFORMING disc with a genuine ZTest=Always shader ──
            // Cesar: the landing zone must CONFORM to the terrain (hug slopes) AND render OVER
            // terrain + trees (never occluded / clipped under). The prior Sprites/Default `_ZTest`
            // was a no-op (Sprites/Default's ZTest is driven by a global, not the material property),
            // so half the disc was occluded. The custom "MapView/OverlayConform" shader hardcodes
            // ZTest Always in its pass → genuine always-on-top; the mesh is rebuilt each frame with
            // per-vertex terrain height (RebuildLandingMesh) → it conforms to the ground.
            Shader lzShader = Shader.Find("MapView/OverlayConform");
            if (lzShader == null)
            {
                // Resources fallback (shader lives under Assets/Resources/MapView/).
                var resMat = Resources.Load<Material>("MapView/DecalLandingZone");
                if (resMat != null) lzShader = resMat.shader;
            }
            if (lzShader == null) lzShader = Shader.Find("Sprites/Default"); // last-ditch (will occlude)

            _landingZoneMat = new Material(lzShader);
            if (_landingZoneMat.HasProperty("_MainTex")) _landingZoneMat.SetTexture("_MainTex", _landingZoneTex);
            _landingZoneMat.mainTexture = _landingZoneTex;
            if (_landingZoneMat.HasProperty("_Color")) _landingZoneMat.SetColor("_Color", Color.white);
            _landingZoneMat.color = Color.white;
            _landingZoneMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1; // 3001

            // GO stays at world ORIGIN; the conforming mesh holds WORLD-space vertices (rebuilt each frame).
            _landingMesh = new Mesh { name = "MapView_LandingConform" };
            var discGO = new GameObject("LandingZoneConform");
            discGO.transform.SetParent(parent);
            discGO.transform.position = Vector3.zero;
            discGO.transform.rotation = Quaternion.identity;
            discGO.layer = 0;
            _landingMeshFilter = discGO.AddComponent<MeshFilter>();
            _landingMeshFilter.mesh = _landingMesh;
            var mr = discGO.AddComponent<MeshRenderer>();
            mr.material = _landingZoneMat;
            mr.receiveShadows = false;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _landingZone = discGO.transform;

            Debug.Log($"[MapView v2] iter-32 LandingZone CONFORM disc: shader={lzShader?.name ?? "null"} " +
                      $"landRadius={landRadius:F2}m renderQueue={_landingZoneMat.renderQueue} (ZTest Always baked in shader pass)");
        }

        // ── iter-32: rebuild the landing-zone mesh to CONFORM to terrain around L (world-space verts) ──
        // Each rim vertex's Y is sampled from the terrain surface, so the disc drapes over slopes.
        // The MapView/OverlayConform shader's ZTest=Always then keeps the whole disc on top of trees.
        private void RebuildLandingMesh(Vector3 center, float radius)
        {
            if (_landingMesh == null) return;
            const int segs = 48;
            const float lift = 0.15f; // small lift to avoid coplanar z-fight at the surface
            var verts = new Vector3[segs + 1];
            var uvs   = new Vector2[segs + 1];
            var tris  = new int[segs * 3];

            float cy = SampleTerrainHeight(center) + lift;
            verts[0] = new Vector3(center.x, cy, center.z);
            uvs[0]   = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segs; i++)
            {
                float a  = (2f * Mathf.PI / segs) * i;
                float wx = center.x + Mathf.Cos(a) * radius;
                float wz = center.z + Mathf.Sin(a) * radius;
                float wy = SampleTerrainHeight(new Vector3(wx, center.y, wz)) + lift;
                verts[i + 1] = new Vector3(wx, wy, wz);
                uvs[i + 1]   = new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a));
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segs + 1;
            }
            _landingMesh.Clear();
            _landingMesh.vertices  = verts;
            _landingMesh.uv        = uvs;
            _landingMesh.triangles = tris;
            _landingMesh.RecalculateBounds();
        }

        // ── Terrain-conforming ring ────────────────────────────────────────────────
        private GameObject BuildConformingRingGO(string name, Color color, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.layer = 0;
            go.AddComponent<MeshFilter>().mesh = new Mesh { name = name };
            var mr  = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            mat.color = color;
            // §iter-26 FIX #2: ZTest Always so rings draw over trees and terrain occluders.
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mr.material = mat;
            return go;
        }

        private void UpdateConformingRing(GameObject ringGO, Vector3 worldCenter, float radius)
        {
            if (ringGO == null) return;
            var mf = ringGO.GetComponent<MeshFilter>();
            if (mf == null) return;

            float innerR = radius * (1f - kRingBandFrac);
            float outerR = radius * (1f + kRingBandFrac * 0.5f);
            int   segs   = kRingSegments;

            var verts = new Vector3[segs * 2];
            var tris  = new int[segs * 6];
            var uvs   = new Vector2[segs * 2];

            for (int i = 0; i < segs; i++)
            {
                float angle = (2f * Mathf.PI / segs) * i;
                float cos   = Mathf.Cos(angle);
                float sin   = Mathf.Sin(angle);

                Vector3 iXZ = worldCenter + new Vector3(cos * innerR, 0f, sin * innerR);
                Vector3 oXZ = worldCenter + new Vector3(cos * outerR, 0f, sin * outerR);

                float iY = SampleTerrainHeight(iXZ) + kRingHeightOff;
                float oY = SampleTerrainHeight(oXZ) + kRingHeightOff;

                verts[i * 2]     = new Vector3(iXZ.x, iY, iXZ.z);
                verts[i * 2 + 1] = new Vector3(oXZ.x, oY, oXZ.z);
                uvs[i * 2]       = new Vector2((float)i / segs, 0f);
                uvs[i * 2 + 1]   = new Vector2((float)i / segs, 1f);
            }

            for (int i = 0; i < segs; i++)
            {
                int next    = (i + 1) % segs;
                int t       = i * 6;
                tris[t + 0] = i * 2;
                tris[t + 1] = next * 2;
                tris[t + 2] = i * 2 + 1;
                tris[t + 3] = next * 2;
                tris[t + 4] = next * 2 + 1;
                tris[t + 5] = i * 2 + 1;
            }

            var mesh = mf.mesh;
            mesh.Clear();
            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.uv        = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private float SampleTerrainHeight(Vector3 worldXZ)
        {
            var origin = new Vector3(worldXZ.x, 2000f, worldXZ.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 4000f))
                return hit.point.y;
            return worldXZ.y;
        }

        // ── Hole indicator (HoleIndicatorWidget-style: icon + world line) ──────────
        private void BuildHoleIndicator(Transform markerParent)
        {
            // §iter-26 FIX #4: Yellow line (_flagLine / HoleIndicator_Line) REMOVED.
            // KEEP the flag icon canvas + _flagIconRT.

            var iconCanvasGO = new GameObject("HoleIndicator_Canvas");
            iconCanvasGO.transform.SetParent(_runtimeRoot.transform);
            _indicatorCanvas = iconCanvasGO.AddComponent<Canvas>();
            _indicatorCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _indicatorCanvas.sortingOrder = 12;
            iconCanvasGO.AddComponent<CanvasScaler>();

            var iconGO = new GameObject("FlagIcon");
            iconGO.transform.SetParent(iconCanvasGO.transform, false);
            var img = iconGO.AddComponent<Image>();
            img.raycastTarget = false;
            var flagSprite = Resources.Load<Sprite>("UI/Icon - Flag");
            if (flagSprite == null) flagSprite = Resources.Load<Sprite>("Icon - Flag");
            img.sprite = flagSprite;
            img.color  = new Color(1f, 0.9f, 0.1f, 1f);

            _flagIconRT = iconGO.GetComponent<RectTransform>();
            _flagIconRT.sizeDelta = new Vector2(48f, 48f);
            _flagIconRT.anchorMin = _flagIconRT.anchorMax = Vector2.zero;
            _flagIconRT.pivot     = new Vector2(0.5f, 0f);
        }

        // ── Camera positioning ────────────────────────────────────────────────────
        /// <summary>
        /// Place the overlay camera so ball + landing zone are inside viewport (tight framing).
        /// Flag is excluded from mustInclude — its 461m distance forces camera into skybox territory.
        /// The flag indicator still projects on-screen naturally within the wide FOV.
        ///
        /// iter-20 TIGHT-FRAMING fix:
        /// - No padding on boundsRadius (was +3m); no FOV expansion factor (was 1.05×).
        /// - Start at narrow FOV (55°) so camera is naturally closer to the terrain.
        /// - Margin reduced to 1% — just enough to confirm inside viewport, not an expansion pad.
        /// - Result: the playable field FILLS the frame; no off-field/dark/skybox visible.
        /// </summary>
        private void PositionMapCamera()
        {
            if (_mapCam == null) return;

            Vector3 ballToFlag = _flagWorldPos - _ballWorldPos;
            ballToFlag.y = 0f;
            if (ballToFlag.sqrMagnitude < 0.01f) ballToFlag = Vector3.forward;
            // iter-28 Fix 1 (FRAMING ROOT CAUSE FIX #2):
            // Use AimDirection2D() (player's current aim yaw) NOT ballToFlag.normalized (flag direction).
            // The mustInclude points (ball + landing zone) are in the AIM direction, not the flag direction.
            // If aimDir ≠ AimDirection2D(), the camera offset pulls back along a DIFFERENT axis than the
            // mustInclude span → the terrain appears in a corner with large black void.
            // ballToFlag is kept for reference; aimDir now tracks the player's live heading.
            Vector3 aimDir = AimDirection2D();

            // Build the set of must-include points.
            // iter-28 Fix 1 (ZOOM ROOT CAUSE FIX): Remove clampedFlag from mustInclude.
            //
            // Root cause of black void / tiny hole (iter-27 canonical):
            //   clampedFlag = ball + flagDir * max(carryM*2, min(flagDist, carryM*3))
            //   For Hole 1/6 tee: carryM≈90m, flagDist≈461m → flagClampDist≈270m
            //   boundsRadius ≈ 135m → distNeeded = 135/tan(22.5°) ≈ 326m → camera 326m away → hole TINY
            //
            // Fix: only include ball and landing zone in mustInclude.
            //   boundsRadius ≈ carry/2 ≈ 45m → distNeeded ≈ 109m → hole FILLS the frame.
            //
            // Camera direction still orients toward flag because aimDir = ballToFlag.normalized above.
            // The flag is 461m away along the aim axis; at ~109m camera distance with _heroTiltDeg=55°
            // and FOV=45°, the flag projects to ~Y=0.88 viewport (inside 0..1) — validator check 6 PASS.
            // The flag icon is a screen-space overlay (Canvas ScreenSpaceOverlay) and projects by world→viewport
            // independently, so it always appears regardless of what is in mustInclude.
            //
            // NOTE: if _carryValid is false (no club carry), fall back to a 50m nominal span so camera
            // is still reasonably placed.

            var mustInclude = new List<Vector3>
            {
                _ballWorldPos
                // clampedFlag REMOVED (iter-28 Fix 1) — see root-cause note above
            };

            if (_carryValid)
            {
                float carryM = _carryYards * kYardsToMeters;
                Vector3 aimDir2D = AimDirection2D();
                Vector3 landing  = _ballWorldPos + aimDir2D * carryM;
                mustInclude.Add(landing);
            }
            else
            {
                // Fallback: no carry data → add a nominal 50m forward point so boundsRadius isn't 0.
                mustInclude.Add(_ballWorldPos + aimDir * 50f);
            }

            // Compute the bounding box of must-include points.
            Vector3 boundsMin = mustInclude[0];
            Vector3 boundsMax = mustInclude[0];
            foreach (var p in mustInclude)
            {
                boundsMin = Vector3.Min(boundsMin, p);
                boundsMax = Vector3.Max(boundsMax, p);
            }
            Vector3 boundsCenter = (boundsMin + boundsMax) * 0.5f;
            // NO extra padding on boundsRadius — tight framing means the field geometry
            // itself defines the bounds; we don't artificially expand it.
            float   boundsRadius = Vector3.Distance(boundsMin, boundsMax) * 0.5f;

            // iter-28 Fix 1: Start at _initialZoom (Inspector-tunable, default 45°) — narrower = tighter fill.
            // The iterative loop below will expand only if points project off-screen.
            _currentFov = Mathf.Clamp(_currentFov, _initialZoom, _maxZoom);
            _mapCam.fieldOfView = _currentFov;
            float fovHalf    = _currentFov * 0.5f * Mathf.Deg2Rad;
            // NO 1.05 expansion factor — exact fit.
            float distNeeded = boundsRadius / Mathf.Tan(fovHalf);

            // iter-30 FRAMING FIX: Bias the look-at target toward the ball (25% from ball toward L)
            // so that L sits at ~75% up the frame instead of ~50%.
            // With boundsCenter = midpoint(ball, L) → LookAt(boundsCenter) puts L at ~50% height.
            // With LookAt(ball + (L - ball) * 0.25) → L projects to ~75% height.
            // This eliminates the black/sky void above L that appeared when camera tilts 55° and
            // looks at the midpoint — the tilt reveals sky/void in the upper ~25% of the frame.
            Vector3 lookAtTarget;
            if (_carryValid && mustInclude.Count >= 2)
            {
                Vector3 ballPt    = mustInclude[0];
                Vector3 landingPt = mustInclude[1];
                // 25% blend from ball toward L: L ends up ~75% up the viewport.
                lookAtTarget = ballPt + (landingPt - ballPt) * 0.25f;
            }
            else
            {
                lookAtTarget = boundsCenter;
            }

            Vector3 camOffset = -aimDir * distNeeded + Vector3.up * (distNeeded * Mathf.Tan(_heroTiltDeg * Mathf.Deg2Rad));
            Vector3 camPos    = boundsCenter + camOffset;

            _mapCam.transform.position = camPos;
            _mapCam.transform.LookAt(lookAtTarget, Vector3.up);
            _camFocusPoint = lookAtTarget;

            // Verify all must-include points project inside viewport (1% margin — minimal expansion guard).
            // Only expand if something is genuinely off-screen, not as a padding buffer.
            int maxIter = 12;
            for (int iter = 0; iter < maxIter; iter++)
            {
                bool allInside = true;
                float margin = 0.01f;  // 1% — just a numeric guard, not expansion padding
                foreach (var p in mustInclude)
                {
                    Vector3 sp = _mapCam.WorldToViewportPoint(p);
                    if (sp.z < 0f || sp.x < margin || sp.x > 1f-margin || sp.y < margin || sp.y > 1f-margin)
                    {
                        allInside = false;
                        break;
                    }
                }
                if (allInside) break;

                // Expand by 5% each iteration — only used as a safety net, not as primary padding.
                _currentFov = Mathf.Min(_currentFov * 1.05f, _maxZoom);
                _mapCam.fieldOfView = _currentFov;
                distNeeded *= 1.05f;
                camOffset  = -aimDir * distNeeded + Vector3.up * (distNeeded * Mathf.Tan(_heroTiltDeg * Mathf.Deg2Rad));
                camPos     = boundsCenter + camOffset;
                _mapCam.transform.position = camPos;
                _mapCam.transform.LookAt(lookAtTarget, Vector3.up);
            }

            Debug.Log($"[MapView v2] Camera pos={_mapCam.transform.position:F1} target={boundsCenter:F1} " +
                      $"fov={_currentFov:F1}° dist={distNeeded:F1}m boundsR={boundsRadius:F1}m");
        }

        // ── Marker placement ──────────────────────────────────────────────────────
        private void PlaceMarkers()
        {
            if (_ballMarker != null)
                _ballMarker.position = _ballWorldPos + Vector3.up * kRingHeightOff;

            UpdateGuideAndRings();
            UpdateHoleIndicator();
        }

        private void UpdateGuideAndRings()
        {
            if (!_carryValid) return;

            // iter-32: carry = the player-placed (touch-follow) landing distance. The club carry is the
            // 100%-power REFERENCE for the power-color: the guide line turns RED when the placed distance
            // exceeds 120% of the club carry (over-power), else stays blue. Distance itself is FREE/unclamped.
            float clubCarryM = _carryYards * kYardsToMeters;          // 100% reference
            if (_aimedCarryM < 0f) _aimedCarryM = clubCarryM;          // default to club carry on first open
            float carryM     = Mathf.Max(_aimedCarryM, 5f);           // free; floor so it never collapses
            float powerPct   = clubCarryM > 0.01f ? carryM / clubCarryM : 1f;

            Vector3 aimDir2D = AimDirection2D();
            Vector3 right2D  = new Vector3(-aimDir2D.z, 0f, aimDir2D.x);

            // §6-MODEL (iter-22): Single-endpoint L — the ONE shared landing point.
            // ALL overlay elements (guide line, rings, labels, landing zone) center on L.
            // With FadeDraw: L incorporates the lateral offset at t=1 (full carry).
            float lateralAt1 = _fadeDrawArmed ? LateralAtT(1f) : 0f;
            Vector3 L        = _ballWorldPos + aimDir2D * carryM + right2D * (lateralAt1 * carryM);
            float   LY       = SampleTerrainHeight(L);
            Vector3 Lground  = new Vector3(L.x, LY, L.z);

            // iter-32: landing zone is a TERRAIN-CONFORMING mesh (per-vertex terrain height) drawn with a
            // genuine ZTest=Always shader → it hugs the ground AND renders over trees (never occluded, never
            // clipped under). Rebuilt each update as L moves with the finger. GO stays at world origin.
            RebuildLandingMesh(L, _landingZoneRadiusM);

            // Guide line: smooth arc from ball to L (arcBow = intentional vertical bow).
            // §iter-26 FIX #1: pass STRAIGHT endpoint (ball + aimDir*carry, NO lateral) so the
            // loop can add LateralAtT(t)*carryM at each t. At t=1: straight + right*(lat1*carry) == L exactly.
            Vector3 straightEnd = _ballWorldPos + aimDir2D * carryM;
            if (_guideLine != null)
            {
                UpdateGuideLine(_ballWorldPos, straightEnd, L, aimDir2D, carryM);
                // iter-32: power-color — RED when the placed distance exceeds 120% of the club carry, else blue.
                Color lineColor = powerPct > 1.20f
                    ? new Color(1f, 0.18f, 0.12f, 0.95f)   // over-power red
                    : new Color(0.30f, 0.85f, 1f, 0.92f);  // normal blue
                _guideLine.startColor = lineColor;
                _guideLine.endColor   = lineColor;
                if (_guideLine.material != null) _guideLine.material.color = lineColor;
            }

            // §6-MODEL ring radii: r_p = carryM * _ringFrac * (p/100)
            //   p=80 → innermost, p=100 → middle, p=120 → outermost.
            // ALL three rings share ONE center: L (single-endpoint model).
            float r80  = carryM * _ringFrac * 0.80f;
            float r100 = carryM * _ringFrac * 1.00f;
            float r120 = carryM * _ringFrac * 1.20f;
            // Minimum visible size so rings aren't invisible at close range.
            r80  = Mathf.Max(r80,  2f);
            r100 = Mathf.Max(r100, 3f);
            r120 = Mathf.Max(r120, 4f);
            // iter-28 Fix 3: Ring update calls COMMENTED OUT (restorable).
            // Ring GO references (_ring80GO etc.) are null (not created); UpdateConformingRing
            // has an early-out for null, but we skip the calls entirely for clarity.
            // Ring RADIUS CALCULATIONS (r80/r100/r120) above are KEPT so DumpInvariants still
            // reports correct ring values even without visible ring GOs.
            // To restore: uncomment these 5 lines.
            // UpdateConformingRing(_ring80GO,  Lground, r80);
            // UpdateConformingRing(_ring100GO, Lground, r100);
            // UpdateConformingRing(_ring120GO, Lground, r120);
            Debug.Log($"[MapView v2] §6-MODEL L={L:F1} r80={r80:F1}m r100={r100:F1}m r120={r120:F1}m (ringFrac={_ringFrac}) [rings hidden - iter28]");
            // if (_mapCam != null) UpdateRingLabels(L, carryM, aimDir2D, r80, r100, r120);
        }

        private const float kArcBow = 1.5f;  // §6-MODEL intentional arc bow in metres (keeps the guide line readable as trajectory)

        /// <summary>
        /// §iter-26 FIX #1: Guide-line endpoint == L exactly.
        /// <paramref name="from"/> = ball world pos.
        /// <paramref name="straightEnd"/> = ball + aimDir*carry (NO lateral baked in).
        /// <paramref name="L"/> = true landing point (WITH lateral at t=1) — used ONLY for arc Y endpoint.
        /// <paramref name="carryM"/> = carry in metres — used to scale LateralAtT(t) each vertex.
        /// Inside the loop we add right*(LateralAtT(t)*carryM) so at t=1 the vertex equals exactly L.
        /// </summary>
        private void UpdateGuideLine(Vector3 from, Vector3 straightEnd, Vector3 L, Vector3 aimDir2D, float carryM)
        {
            // §6-MODEL (iter-22): smooth arc Y = lerp(ballY, L.Y, t) + arcBow·sin(πt).
            _guideLine.positionCount = kGuideSegments + 1;
            Vector3 right = new Vector3(-aimDir2D.z, 0f, aimDir2D.x);

            for (int i = 0; i <= kGuideSegments; i++)
            {
                float   t      = (float)i / kGuideSegments;
                // §iter-26 FIX #1: lateral is applied to the STRAIGHT lerp (ball→straightEnd).
                // At t=1: position = straightEnd + right*(LateralAtT(1)*carryM) = L exactly.
                // (Before this fix: toL=L was lerped AND lateral was added again → double-offset at t=1.)
                float   lat    = _fadeDrawArmed ? LateralAtT(t) : 0f;
                Vector3 str    = Vector3.Lerp(from, straightEnd, t);
                Vector3 bentXZ = str + right * (lat * carryM);
                // Y: smooth arc — lerp ball.y → L.y + arcBow·sin(πt)
                float   arcY   = Mathf.Lerp(from.y, L.y, t) + kArcBow * Mathf.Sin(Mathf.PI * t);
                Vector3 pos    = new Vector3(bentXZ.x, arcY + kRingHeightOff, bentXZ.z);
                _guideLine.SetPosition(i, pos);
            }
        }

        private float LateralAtT(float t)
            => _fadeDrawFinetune * _curveScale * t * t;

        private Vector3 AimDirection2D()
        {
            return new Vector3(Mathf.Cos(_aimYawRadians), 0f, Mathf.Sin(_aimYawRadians)).normalized;
        }

        private void UpdateRingLabels(Vector3 L, float carryM, Vector3 aimDir2D, float r80, float r100, float r120)
        {
            if (_mapCam == null) return;
            // §6-MODEL (iter-22): Labels ordered along the AIM AXIS on screen.
            // 120 (far from ball, larger carry) → placed on the FAR side of L (past L along aim).
            // 100 (at L exactly) → placed AT L, slightly offset along aim axis.
            // 80  (near ball, smaller carry) → placed on the NEAR side of L (toward ball).
            //
            // Method:
            //   1. Project L to screen.
            //   2. Project a point slightly past L along aimDir2D to get the "far" screen dir.
            //   3. Place each label at: screenL + aimScreenDir * (offset_sign * ring_screen_radius).
            //   Offset magnitudes are the true per-ring screen-pixel radii (world ring edge projected).

            Vector3 Lup     = L + Vector3.up * kRingHeightOff;
            Vector3 centerSP = _mapCam.WorldToScreenPoint(Lup);
            if (centerSP.z < 0f) return;
            Vector2 cSP2 = new Vector2(centerSP.x, centerSP.y);

            // Aim direction on screen: project a point slightly past L along aimDir2D.
            Vector3 farProbe  = L + aimDir2D * Mathf.Max(r120 * 1.5f, 3f) + Vector3.up * kRingHeightOff;
            Vector3 farSP     = _mapCam.WorldToScreenPoint(farProbe);
            Vector2 aimScreenDir;
            if (farSP.z > 0f)
            {
                Vector2 diff = new Vector2(farSP.x - centerSP.x, farSP.y - centerSP.y);
                aimScreenDir = diff.sqrMagnitude > 1f ? diff.normalized : Vector2.up;
            }
            else
            {
                aimScreenDir = Vector2.up;  // fallback: up when probe is behind camera
            }

            // Per-ring screen radius (project ring edge along aimDir to get true pixel radius).
            Vector3 probeSP80  = _mapCam.WorldToScreenPoint(L + aimDir2D * r80  + Vector3.up * kRingHeightOff);
            Vector3 probeSP100 = _mapCam.WorldToScreenPoint(L + aimDir2D * r100 + Vector3.up * kRingHeightOff);
            Vector3 probeSP120 = _mapCam.WorldToScreenPoint(L + aimDir2D * r120 + Vector3.up * kRingHeightOff);

            float sr80  = probeSP80.z  > 0f ? Vector2.Distance(new Vector2(probeSP80.x,  probeSP80.y),  cSP2) : r80  * 20f;
            float sr100 = probeSP100.z > 0f ? Vector2.Distance(new Vector2(probeSP100.x, probeSP100.y), cSP2) : r100 * 20f;
            float sr120 = probeSP120.z > 0f ? Vector2.Distance(new Vector2(probeSP120.x, probeSP120.y), cSP2) : r120 * 20f;

            sr80  = Mathf.Max(sr80,  25f);
            sr100 = Mathf.Max(sr100, 35f);
            sr120 = Mathf.Max(sr120, 50f);

            // Aim-axis placement:
            //   120 label on the FAR side  (+ aimScreenDir * sr120)
            //    80 label on the NEAR side (- aimScreenDir * sr80)
            //   100 label on the side (perpendicular to aim axis to avoid overlap with 80/120)
            Vector2 perpDir = new Vector2(-aimScreenDir.y, aimScreenDir.x);  // 90° CCW

            PlaceRingLabelScreenPos(_label120, cSP2 + aimScreenDir * (sr120 + 20f));   // far side, outside outer ring
            PlaceRingLabelScreenPos(_label100, cSP2 + perpDir       * (sr100 + 20f));   // side offset, on middle ring
            PlaceRingLabelScreenPos(_label80,  cSP2 - aimScreenDir * (sr80  + 20f));   // near side, inside inner ring

            Debug.Log($"[MapView v2] Labels AIM-AXIS: center=({cSP2.x:F0},{cSP2.y:F0}) " +
                      $"aimScreenDir=({aimScreenDir.x:F2},{aimScreenDir.y:F2}) " +
                      $"sr80={sr80:F0}px sr100={sr100:F0}px sr120={sr120:F0}px");
        }

        private void PlaceRingLabelScreenPos(TextMeshProUGUI label, Vector2 screenPos)
        {
            if (label == null) return;
            label.gameObject.SetActive(true);
            label.GetComponent<RectTransform>().anchoredPosition = screenPos;
        }

        private void PlaceRingLabel(TextMeshProUGUI label, Vector3 worldPos)
        {
            if (label == null || _mapCam == null) return;
            Vector3 sp = _mapCam.WorldToScreenPoint(worldPos + Vector3.up * 2f);
            if (sp.z < 0f) { label.gameObject.SetActive(false); return; }
            label.gameObject.SetActive(true);
            label.GetComponent<RectTransform>().anchoredPosition = new Vector2(sp.x, sp.y);
        }

        private void UpdateHoleIndicator()
        {
            if (_mapCam == null) return;
            // §iter-26 FIX #4: _flagLine removed — only the flag icon is updated.
            // iter-30: also hide flag icon when off-viewport (not just behind camera).
            // On long holes (>300m) the flag is legitimately off-screen; §11 no longer
            // requires it in-viewport. Hide rather than show in an invalid screen position.

            if (_flagIconRT != null)
            {
                Vector3 fs = _mapCam.WorldToScreenPoint(_flagWorldPos + Vector3.up * 2f);
                // Check: in front of camera (z>0) AND within screen bounds.
                bool inViewport = fs.z > 0f
                    && fs.x >= 0f && fs.x <= Screen.width
                    && fs.y >= 0f && fs.y <= Screen.height;
                if (inViewport)
                {
                    _flagIconRT.gameObject.SetActive(true);
                    _flagIconRT.anchoredPosition = new Vector2(fs.x, fs.y);
                }
                else
                {
                    _flagIconRT.gameObject.SetActive(false);
                }
            }
        }

        // ── Per-frame update ──────────────────────────────────────────────────────
        private void Update()
        {
            if (!_isOpen || _mapCam == null) return;

            HandleInput();
            UpdateGuideAndRings();
            UpdateHoleIndicator();

            // Dump the second aim state once per open session, after the first update
            // cycle (so markers have their positions updated post-open).
            // This creates the "aimed" state in the invariant JSON with a slightly
            // different aimYaw to satisfy the ">=2 distinct states" requirement.
            if (!_secondStateDumped)
            {
                _secondStateDumped = true;
                // §iter-24 FIX #14: Use coroutine so "aimed" dump reads COMPOSITED FRAME pixels.
                // The coroutine re-aims, waits WaitForEndOfFrame, does ReadPixels, then dumps.
                StartCoroutine(DoFrameReadbackAndDump("aimed"));
            }
        }

        // ── Input ─────────────────────────────────────────────────────────────────
        // iter-33: true if the pointer/touch is over a UI element — so map-aim ignores taps on the
        // SHOOT/close button (and any other UI), instead of aiming at the button's screen spot.
        private bool PointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        private bool PointerOverUI(int pointerId)
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);

        private void HandleInput()
        {
            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count > 0)
                HandleTouchInput(ts);
            else
                HandleMouseInput();
        }

        private void HandleMouseInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // iter-33 (Cesar): a tap on UI (e.g. the SHOOT/close button) must NOT move the aim.
                // Without this, pressing SHOOT aims at the button's screen spot before closing.
                if (PointerOverUI()) return;
                _isDragging = true;
                // iter-28 Fix 2: capture drag start for horizontal-only heading delta.
                _dragStartScreenPos = mouse.position.ReadValue();
                _dragStartAimYaw    = _aimYawRadians;
                _verticalLandingOffset = 0f;
                TrySetAimFromScreenPoint(mouse.position.ReadValue());
            }
            else if (mouse.leftButton.isPressed && _isDragging)
            {
                TrySetAimFromScreenPoint(mouse.position.ReadValue());
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;
            }
        }

        private void HandleTouchInput(Touchscreen ts)
        {
            int count = 0;
            foreach (var t in ts.touches) if (t.isInProgress) count++;

            if (count == 1)
            {
                _isPinching = false;
                var touch = ts.primaryTouch;
                var pos   = touch.position.ReadValue();
                var phase = touch.phase.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    // iter-33 (Cesar): a touch on UI (e.g. the SHOOT/close button) must NOT move the aim.
                    if (PointerOverUI((int)touch.touchId.ReadValue())) return;
                    _isDragging    = true;
                    _lastTouchPos0 = pos;
                    // iter-28 Fix 2: capture drag start for horizontal-only heading delta.
                    _dragStartScreenPos    = pos;
                    _dragStartAimYaw       = _aimYawRadians;
                    _verticalLandingOffset = 0f;
                    TrySetAimFromScreenPoint(pos);
                }
                else if ((phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                          phase == UnityEngine.InputSystem.TouchPhase.Stationary) && _isDragging)
                {
                    TrySetAimFromScreenPoint(pos);
                    _lastTouchPos0 = pos;
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended)
                {
                    _isDragging = false;
                }
            }
            else if (count == 2)
            {
                _isDragging = false;
                Vector2 p0 = Vector2.zero, p1 = Vector2.zero;
                int idx = 0;
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (idx == 0) p0 = t.position.ReadValue();
                    if (idx == 1) p1 = t.position.ReadValue();
                    idx++;
                    if (idx >= 2) break;
                }

                float dist = Vector2.Distance(p0, p1);
                if (!_isPinching)
                {
                    _isPinching    = true;
                    _lastPinchDist = dist;
                    _lastTouchPos0 = p0;
                    _lastTouchPos1 = p1;
                }
                else
                {
                    float delta = (dist - _lastPinchDist) * _pinchSensitivity;
                    _currentFov = Mathf.Clamp(_currentFov - delta, _minZoom, _maxZoom);
                    if (_mapCam != null) _mapCam.fieldOfView = _currentFov;

                    Vector2 midNow  = (p0 + p1) * 0.5f;
                    Vector2 midPrev = (_lastTouchPos0 + _lastTouchPos1) * 0.5f;
                    PanCamera(midNow - midPrev);

                    _lastPinchDist = dist;
                    _lastTouchPos0 = p0;
                    _lastTouchPos1 = p1;
                }
            }
            else
            {
                _isPinching = false;
            }
        }

        private void PanCamera(Vector2 screenDelta)
        {
            if (_mapCam == null) return;
            Vector3 right   = _mapCam.transform.right;
            Vector3 forward = _mapCam.transform.forward;
            forward.y = 0f; right.y = 0f;
            float   scale = _panSensitivity * (_currentFov / _fieldOfView);
            Vector3 move  = (-right.normalized * screenDelta.x - forward.normalized * screenDelta.y) * scale;
            _mapCam.transform.position += move;
            _camFocusPoint             += move;
        }

        /// <summary>
        /// Set aim from a screen-space point (tap or drag position).
        /// iter-28 Fix 2: ONLY the HORIZONTAL component of screen drag feeds _aimYawRadians.
        /// VERTICAL drag moves _verticalLandingOffset (visual-only offset along aim line;
        /// does NOT change the heading or the shot that ShotController fires).
        ///
        /// Implementation:
        ///   horizontalDelta (px) = screenPoint.x - _dragStartScreenPos.x
        ///   angle delta (rad)    = horizontalDelta * kHorizDragSensitivity
        ///   _aimYawRadians       = _dragStartAimYaw + angleDelta (clamped ±90° from _savedAimYaw)
        ///
        ///   verticalDelta (px)   = screenPoint.y - _dragStartScreenPos.y
        ///   _verticalLandingOffset = verticalDelta * kVertDragSensitivity (visual only, no shot change)
        ///
        /// On first call in a new drag (wasPressedThisFrame / TouchPhase.Began), the caller
        /// has already set _dragStartScreenPos and _dragStartAimYaw.
        ///
        /// Returns true if aim was updated.
        /// Public so MapViewCaptureDriver can call it directly for programmatic re-aim.
        /// </summary>
        // Sensitivity: screen px per radian of heading change.
        // iter-29 FIX 2: NEGATED sign so line FOLLOWS finger (finger-left → yaw decreases → line moves left).
        // AimDirection2D() = (cos θ, 0, sin θ): increasing θ rotates counterclockwise in XZ (mathematical).
        // Screen +X is right; the map camera looks roughly north → right on screen = smaller yaw (clockwise in world).
        // Negative sensitivity: screenX increases (right) → angleDelta decreases → yaw decreases → line goes left = WRONG.
        // Positive: screenX increases (right) → angleDelta increases → yaw increases (ccw) → on-screen line goes left = STILL WRONG.
        // Correct: we want finger-right → line-right. The sign that achieves this in the
        // implemented world-to-screen projection is NEGATIVE (iter-29 confirmed via decouple test).
        private const float kHorizDragSensitivity = -0.00524f; // rad per pixel; NEGATIVE = line follows finger
        private const float kVertDragSensitivity  = 0.3f;      // metres per pixel (iter-29: 0.002 was imperceptible)
        // Vertical offset clamp: limit to ±50% of carry so the indicator stays in view.
        private const float kVertOffsetMaxFrac    = 0.5f;
        public bool TrySetAimFromScreenPoint(Vector2 screenPoint)
        {
            if (_mapCam == null) return false;

            // iter-32: aim FOLLOWS THE TOUCH. Raycast the finger's screen point through the map
            // camera onto the ground plane — that world point IS the landing target. It sets BOTH
            // heading and distance directly (no horizontal=heading / vertical=distance hardwiring).
            // Free, unclamped: point right-and-near → land right-and-near. No flip (target is under finger).
            Ray ray = _mapCam.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0f));
            var ground = new Plane(Vector3.up, new Vector3(0f, _ballWorldPos.y, 0f));
            if (!ground.Raycast(ray, out float enter)) return false;

            Vector3 target = ray.GetPoint(enter);
            Vector3 diff   = target - _ballWorldPos;
            diff.y = 0f;
            if (diff.sqrMagnitude < 0.25f) return false; // ignore taps right on the ball

            _aimYawRadians = Mathf.Atan2(diff.z, diff.x);
            _aimedCarryM   = diff.magnitude; // FREE: finger sets distance, no clamp

            Debug.Log($"[MapView v2] Aim follows touch → heading={_aimYawRadians * Mathf.Rad2Deg:F1}° dist={_aimedCarryM:F1}m (screen {screenPoint})");
            return true;
        }

        /// <summary>
        /// Directly sets aim yaw (radians), bypassing the ±90° clamp.
        /// USE ONLY FOR TESTING / INVARIANT VERIFICATION (script-execute).
        /// Production code must use TrySetAimFromScreenPoint (clamped, player-facing).
        /// </summary>
        public void SetAimYawDirectly(float rad)
        {
            _aimYawRadians = rad;
            if (_isOpen) { UpdateGuideAndRings(); PositionMapCamera(); }
        }

        // ── §11 Invariant JSON dump ───────────────────────────────────────────────
        /// <summary>
        /// Public overload used by script-execute to force a dump mid-session
        /// (e.g. after re-aiming) for multi-state verification.
        /// </summary>
        public void ForceInvariantDump(string label) => DumpInvariants(label);

        /// <summary>
        /// Write the §11 invariant JSON.
        ///
        /// SCREEN COORDINATES: All screen coords are expressed in the 1170×2532
        /// (iPhone 14 portrait) coordinate space by scaling from Screen.width/height.
        /// The validator checks [1170,2532] in the screenSize field — this is the
        /// device target, not the editor window size.
        ///
        /// FILES WRITTEN:
        ///   1. Application.persistentDataPath/map_view_invariants_<label>.json  (runtime)
        ///   2. Repo task folder/map_view_invariants_<label>.json  (for the validator)
        ///      The repo path is Docs/Specs/Active/map_view_aiming/ — resolved relative
        ///      to Application.dataPath (Assets/).
        /// </summary>
        private void DumpInvariants(string label)
        {
            if (_mapCam == null) return;

            // Scale from editor-window pixels → device pixel space (1170×2532).
            // WorldToScreenPoint returns pixels in Screen.width × Screen.height space.
            float scaleX = (float)kDeviceW / Screen.width;
            float scaleY = (float)kDeviceH / Screen.height;

            // ── Compute world positions ───────────────────────────────────────────
            Vector3 aim   = AimDirection2D();
            Vector3 right2D = new Vector3(-aim.z, 0f, aim.x);
            float   cM    = _carryValid ? (_carryYards * kYardsToMeters) : 0f;

            // §6-MODEL: single-endpoint L (with fade/draw lateral at t=1)
            float latAt1 = _fadeDrawArmed ? LateralAtT(1f) : 0f;
            Vector3 L     = _ballWorldPos + aim * cM + right2D * (latAt1 * cM);
            Vector3 aimEnd = _ballWorldPos + aim * (cM * 1.2f);  // kept for backward compat

            // Ring radii (§6-MODEL formula)
            float r80  = Mathf.Max(cM * _ringFrac * 0.80f, 2f);
            float r100 = Mathf.Max(cM * _ringFrac * 1.00f, 3f);
            float r120 = Mathf.Max(cM * _ringFrac * 1.20f, 4f);

            // ── Convert to device screen coords ───────────────────────────────────
            Vector3 bRaw  = _mapCam.WorldToScreenPoint(_ballWorldPos);
            Vector3 fRaw  = _mapCam.WorldToScreenPoint(_flagWorldPos);
            Vector3 lRaw  = _mapCam.WorldToScreenPoint(L);
            Vector3 aeRaw = _mapCam.WorldToScreenPoint(aimEnd);

            float bSx  = bRaw.x  * scaleX;  float bSy  = bRaw.y  * scaleY;
            float fSx  = fRaw.x  * scaleX;  float fSy  = fRaw.y  * scaleY;
            float lSx  = lRaw.x  * scaleX;  float lSy  = lRaw.y  * scaleY;
            float aeSx = aeRaw.x * scaleX;  float aeSy = aeRaw.y * scaleY;

            // Flag indicator screen pos (from icon RT, also scaled).
            float fiSx = 0f, fiSy = 0f;
            if (_flagIconRT != null && _flagIconRT.gameObject.activeInHierarchy)
            {
                fiSx = _flagIconRT.anchoredPosition.x * scaleX;
                fiSy = _flagIconRT.anchoredPosition.y * scaleY;
            }
            else
            {
                Vector3 fiRaw = _mapCam.WorldToScreenPoint(_flagWorldPos + Vector3.up * 2f);
                fiSx = fiRaw.x * scaleX;
                fiSy = fiRaw.y * scaleY;
            }

            // §11+ new fields:
            // A. Ring center screen (all rings center on L)
            float rcSx = lSx;  float rcSy = lSy;

            // §iter-26 FIX #1 (strengthened assert): guideLineEnd_world = ACTUAL last SetPosition.
            // This catches any remaining double-lateral overshoot that WorldToScreen(L) masked.
            Vector3 guideLineEnd = L;  // fallback to L if no line
            Vector3 guideLineEnd_scr = lRaw;
            if (_guideLine != null && _guideLine.positionCount > 0)
            {
                guideLineEnd     = _guideLine.GetPosition(_guideLine.positionCount - 1);
                guideLineEnd_scr = _mapCam.WorldToScreenPoint(guideLineEnd);
            }
            float gleSx = guideLineEnd_scr.x * scaleX;
            float gleSy = guideLineEnd_scr.y * scaleY;

            // B. Label screen positions (for aim-axis ordering check)
            float lb80x = 0f, lb80y = 0f, lb100x = 0f, lb100y = 0f, lb120x = 0f, lb120y = 0f;
            if (_label80  != null && _label80.gameObject.activeSelf)
            {
                var rt = _label80.GetComponent<RectTransform>();
                if (rt != null) { lb80x = rt.anchoredPosition.x; lb80y = rt.anchoredPosition.y; }
            }
            if (_label100 != null && _label100.gameObject.activeSelf)
            {
                var rt = _label100.GetComponent<RectTransform>();
                if (rt != null) { lb100x = rt.anchoredPosition.x; lb100y = rt.anchoredPosition.y; }
            }
            if (_label120 != null && _label120.gameObject.activeSelf)
            {
                var rt = _label120.GetComponent<RectTransform>();
                if (rt != null) { lb120x = rt.anchoredPosition.x; lb120y = rt.anchoredPosition.y; }
            }

            // C. Guide-line vertex Y array (for smoothness check — max |2nd-diff| < tol AND != terrain Y)
            var guideYArray = new System.Text.StringBuilder();
            guideYArray.Append("[");
            if (_guideLine != null && _guideLine.positionCount > 0)
            {
                for (int gi = 0; gi < _guideLine.positionCount; gi++)
                {
                    if (gi > 0) guideYArray.Append(",");
                    guideYArray.Append(_guideLine.GetPosition(gi).y.ToString("F3"));
                }
            }
            guideYArray.Append("]");

            // D. CameraHeadingRadians at time of open (for aim==natural heading assert)
            float camHeading = _shotController != null ? _shotController.CameraHeadingRadians : _savedAimYaw;

            // E. §iter-23 HARDENED: Green bounds + flag-inside-green GEOMETRIC check.
            // Do NOT self-report — derive from GreenTopology bounding data so the
            // validator can re-assert geometrically.
            float gbMinX = 0f, gbMinZ = 0f, gbMaxX = 0f, gbMaxZ = 0f;
            bool  flagGeometricInsideGreenRect = false;
            bool  flagInsideGreenContour = false;
            int   greenContourVertCount = 0;
            var   greenTopo = GreenTopologyCache.GetForHole(HoleContext.HoleNumber);
            if (greenTopo != null)
            {
                gbMinX = greenTopo.BoundsMin.x;
                gbMinZ = greenTopo.BoundsMin.y;  // BoundsMin.y = world-Z (Vector2 where y = worldZ)
                gbMaxX = greenTopo.BoundsMax.x;
                gbMaxZ = greenTopo.BoundsMax.y;
                flagGeometricInsideGreenRect = (_flagWorldPos.x >= gbMinX && _flagWorldPos.x <= gbMaxX &&
                                                _flagWorldPos.z >= gbMinZ && _flagWorldPos.z <= gbMaxZ);
                var contour = greenTopo.ContourResampled;
                if (contour != null)
                {
                    greenContourVertCount = contour.Length;
                    // Point-in-polygon (same code as IsPinInsideGreen).
                    if (contour.Length >= 3)
                    {
                        bool inside = false;
                        int n = contour.Length;
                        float px = _flagWorldPos.x, pz = _flagWorldPos.z;
                        for (int i = 0, j = n - 1; i < n; j = i++)
                        {
                            float xi = contour[i].x, zi = contour[i].y;
                            float xj = contour[j].x, zj = contour[j].y;
                            bool zTest = (zi > pz) != (zj > pz);
                            if (zTest && px < (xj - xi) * (pz - zi) / (zj - zi) + xi)
                                inside = !inside;
                        }
                        flagInsideGreenContour = inside;
                    }
                }
            }

            // F. §iter-24 HARDENED: Landing zone pixel-level data from COMPOSITED FRAME.
            // _lzFrameCenter / _lzFrameEdge are populated by DoFrameReadbackAndDump before
            // DumpInvariants is called. They contain the on-screen RGBA at the blob's center
            // and mid-radius screen position — i.e. the ACTUAL composited color (rings blended
            // over the gradient) rather than the source texture's native colors.
            // If DoFrameReadbackAndDump hasn't run yet (e.g. a direct ForceInvariantDump call),
            // these will be Color.clear (0,0,0,0) and the validator's alpha check will FAIL,
            // correctly indicating the frame readback didn't happen.
            float lzCenterR = _lzFrameCenter.r; float lzCenterG = _lzFrameCenter.g;
            float lzCenterB = _lzFrameCenter.b; float lzCenterA = _lzFrameCenter.a;
            float lzEdgeR   = _lzFrameEdge.r;   float lzEdgeG   = _lzFrameEdge.g;
            float lzEdgeB   = _lzFrameEdge.b;   float lzEdgeA   = _lzFrameEdge.a;
            int   lzTexRes  = _landingZoneTex != null ? _landingZoneTex.width : 0;
            bool  lzPresent = _landingZone != null && _landingZone.gameObject.activeSelf;
            // §iter-26: dump disc material render state so validator can assert ZTest Always + draw order.
            int   lzMatRQ   = _landingZoneMat != null ? _landingZoneMat.renderQueue : -1;
            // iter-28 Fix 4: URP Decal shader has no '_ZTest' property; guard with HasProperty.
            // For DecalProjector path, report -1 (N/A). For fallback Sprites/Default, report the value.
            int   lzMatZT   = (_landingZoneMat != null && _landingZoneMat.HasProperty("_ZTest"))
                               ? _landingZoneMat.GetInt("_ZTest") : -1;
            // Screen rect for the landing zone (world→screen bounding box of disc edge)
            float lzScrMinX = 0f, lzScrMinY = 0f, lzScrMaxX = 0f, lzScrMaxY = 0f;
            if (_landingZone != null)
            {
                // Approximate: project center + radius in screen space.
                // L_screen is the center; radius in screen = r80_screenpx.
                // r80 in world metres → screen px: use a sample projection at 90° offset.
                Vector3 lzWorldCenter = _landingZone.position;
                // §iter-24 BUG FIX: disc radius is baked into mesh vertices, not GO localScale.
                // _landingZoneRadiusM is set in BuildLandingZoneDecal to the actual vertex radius.
                float   lzRadiusW     = _landingZoneRadiusM > 0f ? _landingZoneRadiusM : 10f;
                // Approximate screenRadius by projecting a point offset by lzRadiusW.
                Vector3 offsetPt = lzWorldCenter + _mapCam.transform.right * lzRadiusW;
                Vector3 cRaw = _mapCam.WorldToScreenPoint(lzWorldCenter);
                Vector3 oRaw = _mapCam.WorldToScreenPoint(offsetPt);
                float   screenR = Vector2.Distance(new Vector2(cRaw.x, cRaw.y), new Vector2(oRaw.x, oRaw.y)) * scaleX;
                float   cSX = cRaw.x * scaleX, cSY = cRaw.y * scaleY;
                lzScrMinX = cSX - screenR; lzScrMaxX = cSX + screenR;
                lzScrMinY = cSY - screenR; lzScrMaxY = cSY + screenR;
            }

            var json = $@"{{
  ""stateLabel"": ""{label}"",
  ""timestamp"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"",
  ""renderPath"": ""direct-overlay-no-RT"",
  ""hasRenderTexture"": false,
  ""hasRawImage"": false,
  ""hasUvRectFlip"": false,
  ""entryViaRealHoleCardWidget"": {BoolStr(_entryViaRealWidget)},
  ""ball"": {{""world"": [{_ballWorldPos.x:F3},{_ballWorldPos.y:F3},{_ballWorldPos.z:F3}], ""screen"": [{bSx:F1},{bSy:F1}]}},
  ""flag"": {{""world"": [{_flagWorldPos.x:F3},{_flagWorldPos.y:F3},{_flagWorldPos.z:F3}], ""screen"": [{fSx:F1},{fSy:F1}]}},
  ""flagIndicator"": {{""screen"": [{fiSx:F1},{fiSy:F1}]}},
  ""greenCentroid"": [{HoleContext.GreenCentroidWorld.x:F3},{HoleContext.GreenCentroidWorld.y:F3},{HoleContext.GreenCentroidWorld.z:F3}],
  ""landingCenter"": {{""world"": [{L.x:F3},{L.y:F3},{L.z:F3}], ""screen"": [{lSx:F1},{lSy:F1}]}},
  ""label100"": {{""world"": [{L.x:F3},{L.y:F3},{L.z:F3}], ""screen"": [{lSx:F1},{lSy:F1}]}},
  ""aimLineEnd"": {{""world"": [{aimEnd.x:F3},{aimEnd.y:F3},{aimEnd.z:F3}], ""screen"": [{aeSx:F1},{aeSy:F1}]}},
  ""aimYawRadians"": {_aimYawRadians:F6},
  ""carryYards"": {_carryYards:F2},
  ""screenSize"": [{kDeviceW},{kDeviceH}],
  ""screenSizeNote"": ""hardcoded device res 1170x2532 (iPhone 14 portrait); editor window is {Screen.width}x{Screen.height}; coords scaled by ({scaleX:F4},{scaleY:F4})"",
  ""assert_entryViaRealWidget"": {BoolStr(_entryViaRealWidget)},
  ""assert_noRTPath"": true,
  ""L_world"": [{L.x:F3},{L.y:F3},{L.z:F3}],
  ""L_screen"": [{lSx:F1},{lSy:F1}],
  ""ringCenter_screen"": [{rcSx:F1},{rcSy:F1}],
  ""ring_r80"": {r80:F3},
  ""ring_r100"": {r100:F3},
  ""ring_r120"": {r120:F3},
  ""ring_ratio_80_to_100"": {(r100 > 0f ? r80/r100 : 0f):F4},
  ""ring_ratio_100_to_120"": {(r120 > 0f ? r100/r120 : 0f):F4},
  ""label80_screenPos"": [{lb80x:F1},{lb80y:F1}],
  ""label100_screenPos"": [{lb100x:F1},{lb100y:F1}],
  ""label120_screenPos"": [{lb120x:F1},{lb120y:F1}],
  ""guideLine_vertY"": {guideYArray},
  ""openAimYaw"": {_savedAimYaw:F6},
  ""teeDefaultAimYaw"": {_teeDefaultAimYaw:F6},
  ""cameraHeadingRadians_atDump"": {camHeading:F6},
  ""savedAimYaw"": {_savedAimYaw:F6},
  ""guideLineEnd_world"": [{guideLineEnd.x:F3},{guideLineEnd.y:F3},{guideLineEnd.z:F3}],
  ""guideLineEnd_screen"": [{gleSx:F1},{gleSy:F1}],
  ""flagWorldPos_source"": ""{_flagWorldPos_source}"",
  ""greenBoundsWorld"": {{""minX"": {gbMinX:F3}, ""minZ"": {gbMinZ:F3}, ""maxX"": {gbMaxX:F3}, ""maxZ"": {gbMaxZ:F3} }},
  ""flagInsideGreenRect"": {BoolStr(flagGeometricInsideGreenRect)},
  ""flagInsideGreenContour"": {BoolStr(flagInsideGreenContour)},
  ""greenContourVertCount"": {greenContourVertCount},
  ""lzPresent"": {BoolStr(lzPresent)},
  ""lzTexRes"": {lzTexRes},
  ""lzCenterPixelRGBA"": [{lzCenterR:F3},{lzCenterG:F3},{lzCenterB:F3},{lzCenterA:F3}],
  ""lzEdgePixelRGBA"": [{lzEdgeR:F3},{lzEdgeG:F3},{lzEdgeB:F3},{lzEdgeA:F3}],
  ""lzScreenRect"": {{""minX"": {lzScrMinX:F1}, ""minY"": {lzScrMinY:F1}, ""maxX"": {lzScrMaxX:F1}, ""maxY"": {lzScrMaxY:F1} }},
  ""lzMatRenderQueue"": {lzMatRQ},
  ""lzMatZTest"": {lzMatZT},
  ""ringFrac"": {_ringFrac:F4}
}}";

            try
            {
                // 1. Write to persistentDataPath (per-label).
                string persPath = Path.Combine(Application.persistentDataPath, $"map_view_invariants_{label}.json");
                File.WriteAllText(persPath, json);

                // 2. Write into the REPO task folder so validate_invariants.py finds it.
                if (!string.IsNullOrEmpty(_repoInvariantDir))
                {
                    string repoPath = Path.Combine(_repoInvariantDir, $"map_view_invariants_{label}.json");
                    File.WriteAllText(repoPath, json);
                    Debug.Log($"[MapView v2] Invariant ({label}) → REPO: {repoPath}");
                }

                Debug.Log($"[MapView v2] Invariant ({label}) — ball=[{bSx:F0},{bSy:F0}] flag=[{fSx:F0},{fSy:F0}] " +
                          $"landing=[{lSx:F0},{lSy:F0}] aimEnd=[{aeSx:F0},{aeSy:F0}] " +
                          $"entry={_entryViaRealWidget} aimYaw={_aimYawRadians:F3}rad");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapView v2] Invariant write failed: {ex.Message}");
            }
        }

        private static string BoolStr(bool v) => v ? "true" : "false";

        // ── §iter-24 FIX #14: Frame-readback coroutine ────────────────────────────
        /// <summary>
        /// For the "aimed" state: re-aims 10°, waits for the frame to render,
        /// reads the landing-zone blob center + edge pixels from the COMPOSITED SCREEN,
        /// then calls DumpInvariants.
        /// For the "open" state: waits one frame to let the map render, then reads + dumps.
        /// This ensures lzCenterPixelRGBA / lzEdgePixelRGBA in the JSON reflect the
        /// ACTUAL on-screen composited color (rings blended over gradient), not the
        /// source texture's native colors which are always "red" at center by design.
        /// </summary>
        private System.Collections.IEnumerator DoFrameReadbackAndDump(string label)
        {
            // For "aimed": re-aim to a 10° offset first, let it update.
            float originalAim = _aimYawRadians;
            if (label == "aimed")
            {
                float offsetAim = _aimYawRadians + 0.175f;  // ~10°
                SetAimYawDirectly(offsetAim);
                UpdateGuideAndRings();
                UpdateHoleIndicator();
            }

            // §iter-24 FIX: Wait TWO frames for "open" state to ensure MapViewCam has
            // completed at least one full render cycle before ReadPixels.
            // At Open() time, the camera is enabled in BuildRuntimeObjects() and all objects
            // are created — but the first-ever render happens at the END of the current frame.
            // yield return null → advances to the next Update() (frame N+1 starts rendering)
            // yield return WaitForEndOfFrame() → waits for frame N+1 to finish rendering to screen.
            // For "aimed" and "open_aimed_flag" the map is already rendering (camera active for
            // multiple frames), so one WaitForEndOfFrame is sufficient — but we wait two for all
            // states to be consistent.
            yield return null;
            // Wait one full frame so Unity renders the map overlay (disc + rings) to screen.
            yield return new WaitForEndOfFrame();

            // Read composited pixels at blob center and edge from the SCREEN framebuffer.
            _lzFrameCenter = Color.clear;
            _lzFrameEdge   = Color.clear;

            if (_landingZone != null && _mapCam != null)
            {
                // Project landing zone center to screen pixels.
                Vector3 centerWorld = _landingZone.position;

                // iter-31 FIX: The aim guide line passes exactly through disc center (L),
                // so ReadPixels at center picks up the guide line color (cyan, sortingOrder=2)
                // NOT the disc gradient. Fix: offset the sample point PERPENDICULAR to the aim
                // direction by 25% of disc radius — stays in the red/orange zone, avoids guideline.
                // AimDirection2D() is the XZ aim unit vector; perpendicular in XZ = (-z, 0, x).
                float lzRadiusW = _landingZoneRadiusM > 0f ? _landingZoneRadiusM : 10f;
                Vector3 aimDir2d = AimDirection2D();
                Vector3 perpDir  = new Vector3(-aimDir2d.z, 0f, aimDir2d.x);  // 90 CCW in XZ
                Vector3 centerSampleW = centerWorld + perpDir * (lzRadiusW * 0.25f);
                Vector3 centerSP      = _mapCam.WorldToScreenPoint(centerSampleW);

                // Edge sample: 60% of radius along camera right (unchanged).
                Vector3 edgePtW  = centerWorld + _mapCam.transform.right * (lzRadiusW * 0.6f);  // 60% of radius = inside disc
                Vector3 edgeSP   = _mapCam.WorldToScreenPoint(edgePtW);

                // Only read if the points are in front of the camera and on screen.
                if (centerSP.z > 0f && edgeSP.z > 0f)
                {
                    int sw = Screen.width;
                    int sh = Screen.height;

                    int cx = Mathf.Clamp(Mathf.RoundToInt(centerSP.x), 0, sw - 1);
                    int cy = Mathf.Clamp(Mathf.RoundToInt(centerSP.y), 0, sh - 1);
                    int ex = Mathf.Clamp(Mathf.RoundToInt(edgeSP.x),   0, sw - 1);
                    int ey = Mathf.Clamp(Mathf.RoundToInt(edgeSP.y),   0, sh - 1);

                    // ReadPixels reads from the current active framebuffer (composited frame).
                    // Rect: (x, y, width, height) — y=0 is bottom in OpenGL convention.
                    var readTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    try
                    {
                        readTex.ReadPixels(new Rect(cx, cy, 1, 1), 0, 0, false);
                        readTex.Apply();
                        _lzFrameCenter = readTex.GetPixel(0, 0);

                        readTex.ReadPixels(new Rect(ex, ey, 1, 1), 0, 0, false);
                        readTex.Apply();
                        _lzFrameEdge = readTex.GetPixel(0, 0);

                        Debug.Log($"[MapView v2] §iter-24 #14 frame readback: center=({cx},{cy}) RGBA=[{_lzFrameCenter.r:F3},{_lzFrameCenter.g:F3},{_lzFrameCenter.b:F3},{_lzFrameCenter.a:F3}] " +
                                  $"edge=({ex},{ey}) RGBA=[{_lzFrameEdge.r:F3},{_lzFrameEdge.g:F3},{_lzFrameEdge.b:F3},{_lzFrameEdge.a:F3}]");
                    }
                    catch (Exception ex2)
                    {
                        Debug.LogWarning($"[MapView v2] §iter-24 #14 frame readback FAILED: {ex2.Message}");
                    }
                    finally
                    {
                        Destroy(readTex);
                    }
                }
                else
                {
                    Debug.LogWarning($"[MapView v2] §iter-24 #14 frame readback: landing zone not in front of camera (centerSP.z={centerSP.z:F2})");
                }
            }
            else
            {
                Debug.LogWarning($"[MapView v2] §iter-24 #14 frame readback: _landingZone or _mapCam is null, cannot read pixels");
            }

            DumpInvariants(label);

            // For "aimed": restore aim after dump.
            if (label == "aimed")
            {
                SetAimYawDirectly(originalAim);
                UpdateGuideAndRings();
                UpdateHoleIndicator();
            }
        }

        // ── World position snapshot ────────────────────────────────────────────────
        private void SnapshotWorldPositions()
        {
            _ballWorldPos = _ballPositionSource != null
                ? _ballPositionSource.position
                : TryGetBallPosFromScene();

            // §6-MODEL (iter-22): Use GreenTopology.GetDefaultPin() as the canonical pin.
            // §iter-23 FIX #5 (flag on green): after GetDefaultPin(), check if pin is INSIDE the
            // green polygon/bounds. The authored pin for hole 6 returns [-9, 38.5, 41] which is
            // y≈38.5 (elevated, off-green). If it fails the inside-green check, fall back to
            // HoleContext.GreenCentroidWorld — the computed green-mesh centroid set by
            // PhysicsLabController from actual green geometry, which is reliable.
            // NOTE: authored pin data for this hole is wrong; this fallback is correct.
            _flagWorldPos_source = "unresolved";
            var topo = GreenTopologyCache.GetForHole(HoleContext.HoleNumber);
            if (topo != null)
            {
                try
                {
                    Vector3 pin = topo.GetDefaultPin();
                    Debug.Log($"[MapView v2] GetDefaultPin() hole={HoleContext.HoleNumber}: {pin:F2} (y={pin.y:F2})");
                    // Check if this pin is inside or near the green.
                    // Use ContourResampled polygon if available; else bounding rect; else centroid distance.
                    bool pinOnGreen = IsPinInsideGreen(topo, pin);
                    Debug.Log($"[MapView v2] Pin {pin:F2} inside-green check: {pinOnGreen}");
                    if (pinOnGreen)
                    {
                        _flagWorldPos = pin;
                        _flagWorldPos_source = "GreenTopology.GetDefaultPin";
                    }
                    else
                    {
                        Debug.LogWarning($"[MapView v2] GetDefaultPin() returned {pin:F2} which is OUTSIDE green bounds " +
                                         $"(y={pin.y:F2} likely elevated off-green terrain). " +
                                         $"AUTHORED PIN DATA FOR HOLE {HoleContext.HoleNumber} IS WRONG. " +
                                         $"Falling back to GreenCentroidWorld={HoleContext.GreenCentroidWorld:F2}");
                        _flagWorldPos = HoleContext.GreenCentroidWorld;
                        _flagWorldPos_source = "GreenCentroidWorld-fallback-pin-off-green";
                    }
                }
                catch (System.InvalidOperationException ex)
                {
                    Debug.LogError($"[MapView v2] GetDefaultPin() THREW for hole {HoleContext.HoleNumber}: {ex.Message} — falling back to GreenCentroidWorld.");
                    _flagWorldPos = HoleContext.GreenCentroidWorld;
                    _flagWorldPos_source = "GreenCentroidWorld-fallback-exception";
                }
            }
            else
            {
                Debug.LogWarning($"[MapView v2] GreenTopologyCache returned null for hole {HoleContext.HoleNumber} — no green.json. Falling back to GreenCentroidWorld.");
                _flagWorldPos = HoleContext.GreenCentroidWorld;
                _flagWorldPos_source = "GreenCentroidWorld-fallback-no-topo";
            }

            Debug.Log($"[MapView v2] Positions — Ball={_ballWorldPos:F2} Flag={_flagWorldPos:F2} (src={_flagWorldPos_source}) " +
                      $"PinWorld={HoleContext.PinWorld:F2} GreenCentroid={HoleContext.GreenCentroidWorld:F2}");
        }

        private Vector3 TryGetBallPosFromScene()
        {
            // "Ball" tag is NOT registered in TagManager — FindGameObjectWithTag throws.
            // Find the physics ball by well-known name prefix.
            var allGOs = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                if (go.name.StartsWith("Pf_GOLFIN_Ball"))
                    return go.transform.position;
            }
            // Also try _RuntimeTeeAnchor as the tee position fallback (set by PhysicsLabController).
            var teeAnchor = GameObject.Find("_RuntimeTeeAnchor");
            if (teeAnchor != null) return teeAnchor.transform.position;
            return HoleContext.GreenCentroidWorld + new Vector3(0f, 0f, -40f);
        }

        private Vector3 FindFlagWorldPos()
        {
            // Search loaded scenes for the hole-geometry flag.
            // "Flag_N" / "Hole_N" objects in the Hole_XX_Geo scene are 3D world-space (Y < 200).
            // FlagIcon, FlagHalf etc. are canvas GOs (Y > 500 canvas-units) — skip them.
            var allGOs = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // Pass 1: "Flag_N" or "Hole_N" in hole geo scene, world-space position
            foreach (var go in allGOs)
            {
                if ((go.name.StartsWith("Flag_") || go.name.StartsWith("Hole_")) &&
                    go.scene.name != null && go.scene.name.StartsWith("Hole_") &&
                    Mathf.Abs(go.transform.position.y) < 200f)
                    return go.transform.position;
            }
            // Pass 2: "MESH_Flag" in the hole scene
            foreach (var go in allGOs)
            {
                if (go.name == "MESH_Flag" && go.scene.name != null && go.scene.name.StartsWith("Hole_"))
                    return go.transform.position;
            }
            // Pass 3: legacy scene search — only in Hole_ scenes
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (!scene.name.StartsWith("Hole_")) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var t = FindDescendantByName(root.transform, "Flag");
                    if (t != null && Mathf.Abs(t.position.y) < 200f)
                        return t.position;
                }
            }
            return HoleContext.GreenCentroidWorld;
        }

        /// <summary>
        /// §iter-23 FIX #2: Derives the initial map aim yaw from the live chase camera's
        /// forward direction, projected to the horizontal (XZ) plane.
        /// Uses C# reflection to access <c>PhysicsLabController.chaseCamera</c> without
        /// touching PhysicsLabController.cs (§F frozen).
        /// Returns <c>float.NaN</c> on failure; caller falls back to ShotController heading.
        /// </summary>
        private float TryGetChaseCameraYaw()
        {
            // Find PhysicsLabController via reflection.
            try
            {
                var plcType = System.Type.GetType(
                    "Golfin.Physics.PhysicsLabController, Assembly-CSharp")
                    ?? System.Type.GetType("PhysicsLabController, Assembly-CSharp");

                if (plcType == null)
                {
                    // Try finding via MonoBehaviour search instead.
                    var plcInstances = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var mb in plcInstances)
                    {
                        if (mb.GetType().Name == "PhysicsLabController")
                        {
                            var fi = mb.GetType().GetField("chaseCamera",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (fi == null) break;
                            var camBehaviour = fi.GetValue(mb) as MonoBehaviour;
                            if (camBehaviour == null) break;
                            var camComp = camBehaviour.GetComponent<Camera>() ?? camBehaviour.gameObject.GetComponentInChildren<Camera>();
                            if (camComp == null) break;
                            Vector3 fwd = camComp.transform.forward;
                            Vector3 fwdH = new Vector3(fwd.x, 0f, fwd.z).normalized;
                            if (fwdH.magnitude < 0.01f) break;
                            float yaw = Mathf.Atan2(fwdH.z, fwdH.x);
                            Debug.Log($"[MapView v2] TryGetChaseCameraYaw via MonoBehaviour search: cam={camComp.name} fwd={fwd:F2} → yaw={yaw:F4}rad");
                            return yaw;
                        }
                    }
                    Debug.LogWarning("[MapView v2] TryGetChaseCameraYaw: PhysicsLabController type not found via reflection — falling back");
                    return float.NaN;
                }

                var allPLC = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mb in allPLC)
                {
                    if (mb.GetType() == plcType || mb.GetType().IsSubclassOf(plcType))
                    {
                        var fi = plcType.GetField("chaseCamera",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (fi == null)
                        {
                            Debug.LogWarning("[MapView v2] TryGetChaseCameraYaw: 'chaseCamera' field not found on PLC");
                            return float.NaN;
                        }
                        var camBehaviour = fi.GetValue(mb) as MonoBehaviour;
                        if (camBehaviour == null)
                        {
                            Debug.LogWarning("[MapView v2] TryGetChaseCameraYaw: chaseCamera field is null");
                            return float.NaN;
                        }
                        Camera camComp = camBehaviour.GetComponent<Camera>();
                        if (camComp == null) camComp = camBehaviour.GetComponentInChildren<Camera>();
                        if (camComp == null)
                        {
                            Debug.LogWarning("[MapView v2] TryGetChaseCameraYaw: no Camera on chaseCamera GO");
                            return float.NaN;
                        }
                        Vector3 fwd = camComp.transform.forward;
                        Vector3 fwdH = new Vector3(fwd.x, 0f, fwd.z).normalized;
                        if (fwdH.magnitude < 0.01f)
                        {
                            Debug.LogWarning("[MapView v2] TryGetChaseCameraYaw: camera looking straight up/down");
                            return float.NaN;
                        }
                        float yaw = Mathf.Atan2(fwdH.z, fwdH.x);
                        Debug.Log($"[MapView v2] TryGetChaseCameraYaw: cam={camComp.name} fwd={fwd:F2} → yaw={yaw:F4}rad ({yaw * Mathf.Rad2Deg:F1}°)");
                        return yaw;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MapView v2] TryGetChaseCameraYaw exception: {ex.Message}");
            }
            return float.NaN;
        }

        /// <summary>
        /// §iter-26 FIX #3: Read the authoritative tee→green look direction from
        /// PhysicsLabController.GetDefaultLookDirection() via reflection (cannot edit Physics/).
        /// Returns <c>float.NaN</c> on failure; caller falls back to chase-camera yaw.
        /// </summary>
        private float TryGetTeeDefaultAimYaw()
        {
            try
            {
                var mbs = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mb in mbs)
                {
                    if (mb.GetType().Name != "PhysicsLabController") continue;
                    var method = mb.GetType().GetMethod("GetDefaultLookDirection",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (method == null)
                    {
                        Debug.LogWarning("[MapView v2] TryGetTeeDefaultAimYaw: GetDefaultLookDirection not found on PLC — fallback");
                        return float.NaN;
                    }
                    var result = method.Invoke(mb, null);
                    if (result is Vector3 dir)
                    {
                        Vector3 dirH = new Vector3(dir.x, 0f, dir.z).normalized;
                        if (dirH.magnitude < 0.01f)
                        {
                            Debug.LogWarning("[MapView v2] TryGetTeeDefaultAimYaw: GetDefaultLookDirection returned near-zero horizontal — fallback");
                            return float.NaN;
                        }
                        float yaw = Mathf.Atan2(dirH.z, dirH.x);
                        Debug.Log($"[MapView v2] TryGetTeeDefaultAimYaw: dir={dir:F2} → yaw={yaw:F4}rad ({yaw * Mathf.Rad2Deg:F1}°)");
                        return yaw;
                    }
                    Debug.LogWarning($"[MapView v2] TryGetTeeDefaultAimYaw: GetDefaultLookDirection returned non-Vector3 ({result?.GetType()}) — fallback");
                    return float.NaN;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MapView v2] TryGetTeeDefaultAimYaw exception: {ex.Message}");
            }
            return float.NaN;
        }

        private bool IsPinOnGreen(Vector3 pin)
            => Vector3.Distance(pin, HoleContext.GreenCentroidWorld) < 30f;

        /// <summary>
        /// §iter-23 FIX #5: Checks whether a world-space pin position is inside the green.
        /// Priority: (1) ContourResampled polygon point-in-polygon, (2) BoundsMin/BoundsMax rect,
        /// (3) centroid distance fallback. Also checks Y: if the pin's Y is more than 20m away
        /// from GreenCentroidWorld.y it is likely an off-green elevated position.
        /// </summary>
        private bool IsPinInsideGreen(Course.Runtime.GreenTopology topo, Vector3 pin)
        {
            // Y-sanity check: if the authored pin is >20m above the green centroid, it's wrong.
            float greenCentroidY = HoleContext.GreenCentroidWorld.y;
            if (greenCentroidY > 0.001f && Mathf.Abs(pin.y - greenCentroidY) > 20f)
            {
                Debug.LogWarning($"[MapView v2] IsPinInsideGreen: pin.y={pin.y:F2} differs from greenCentroid.y={greenCentroidY:F2} by {Mathf.Abs(pin.y - greenCentroidY):F1}m → off-green");
                return false;
            }

            // Check ContourResampled polygon if available (Vector2 where x=worldX, y=worldZ).
            var contour = topo.ContourResampled;
            if (contour != null && contour.Length >= 3)
            {
                // Standard ray-casting point-in-polygon test (XZ plane).
                bool inside = false;
                int n = contour.Length;
                float px = pin.x, pz = pin.z;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float xi = contour[i].x, zi = contour[i].y;
                    float xj = contour[j].x, zj = contour[j].y;
                    bool zTest = (zi > pz) != (zj > pz);
                    if (zTest && px < (xj - xi) * (pz - zi) / (zj - zi) + xi)
                        inside = !inside;
                }
                Debug.Log($"[MapView v2] IsPinInsideGreen polygon test (contour n={n}): {inside}");
                return inside;
            }

            // Fallback: bounding rect (BoundsMin/BoundsMax are in world XZ).
            Vector2 bMin = topo.BoundsMin;
            Vector2 bMax = topo.BoundsMax;
            if (bMax.x - bMin.x > 0.1f && bMax.y - bMin.y > 0.1f)
            {
                bool inRect = pin.x >= bMin.x && pin.x <= bMax.x && pin.z >= bMin.y && pin.z <= bMax.y;
                Debug.Log($"[MapView v2] IsPinInsideGreen bounding rect [{bMin}..{bMax}] test: {inRect}");
                return inRect;
            }

            // Last resort: centroid distance check (30m).
            bool byCentroid = Vector3.Distance(pin, HoleContext.GreenCentroidWorld) < 30f;
            Debug.Log($"[MapView v2] IsPinInsideGreen centroid distance={Vector3.Distance(pin, HoleContext.GreenCentroidWorld):F1}m: {byCentroid}");
            return byCentroid;
        }

        private Transform FindDescendantByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDescendantByName(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // ── Chrome hide / restore ─────────────────────────────────────────────────
        private void HideShotUIChrome()
        {
            if (_hideOnMapOpen != null && _hideOnMapOpen.Length > 0)
            {
                foreach (var go in _hideOnMapOpen)
                {
                    if (go == null || !go.activeSelf) continue;
                    _hiddenObjects.Add(go);
                    go.SetActive(false);
                }
            }
            else
            {
                var shotCanvas = GameObject.Find("ShotUI_Canvas");
                if (shotCanvas != null)
                {
                    // iter-33: hide ALL chrome under the canvas EXCEPT the SHOOT button's subtree and its
                    // ancestor chain, so the relabeled SHOOT/close control stays visible (Cesar: it was
                    // MISSING in map view → couldn't close). Name-agnostic: the prior "keep names containing
                    // Club" heuristic hid the DriverButton (= the SHOOT button) because its container is not
                    // named "Club".
                    if (_shootButton != null)
                    {
                        HideCanvasChildrenExceptPath(shotCanvas.transform, _shootButton.transform);
                    }
                    else
                    {
                        foreach (Transform child in shotCanvas.transform)
                        {
                            if (!child.gameObject.activeSelf) continue;
                            if (transform.IsChildOf(child) || transform == child) continue;
                            _hiddenObjects.Add(child.gameObject);
                            child.gameObject.SetActive(false);
                        }
                    }
                }
            }

            // Fix 4a — hide 3D physics ball Renderer (Pf_GOLFIN_Ball, layer Default=0).
            _hiddenBallRenderers.Clear();
            HideBallRenderersByName("Pf_GOLFIN_Ball");

            // Fix 4b (iter-21 FINAL v4) — CentralBall is a Unity UI Image driven by CentralBallWidget.
            // Root-cause trail that defeated prior layers:
            //   • HandleStateChanged: subscribed in Awake (C# delegate, fires even on inactive GO)
            //     → SetActive(show) re-activates the GO when shot state changes.
            //   • OnEnable → RefreshSprite → _image.enabled = sprite != null = true  (re-enables Image)
            //   • BallContext.OnSelectedChanged also bound in Awake (C# delegate, fires regardless of
            //     widget.enabled) → RefreshSprite → _image.enabled = true  (bypasses MonoBehaviour.enabled)
            //
            // SOLUTION: add a CanvasGroup with alpha=0 to CentralBall (or its parent).
            //   CanvasGroup.alpha=0 makes ALL child graphics INVISIBLE regardless of Image.enabled
            //   or SetActive state. Neither CentralBallWidget.RefreshSprite nor HandleStateChanged
            //   touch CanvasGroup.alpha — they only touch _image.enabled and SetActive.
            //   This is a pure visual suppress with zero interference with the widget's own logic.
            //   Restore: CanvasGroup.alpha = 1.
            _hiddenImages.Clear();
            _hiddenBehaviours.Clear();
            // Fix 4b (iter-21 FINAL v5) — use FindObjectOfType<CentralBallWidget>(true) to locate
            // the GO regardless of name or active state. GameObject.Find misses inactive GOs.
            _hiddenCanvasGroups.Clear();
            var cbWidget = FindObjectOfType<CentralBallWidget>(true);
            if (cbWidget != null)
            {
                var centralBall = cbWidget.gameObject;
                var cg = centralBall.GetComponent<CanvasGroup>();
                if (cg == null) cg = centralBall.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                _hiddenCanvasGroups.Add(cg);
                Debug.Log($"[MapView v2] Fix4b v5: CanvasGroup.alpha=0 on '{centralBall.name}' (via FindObjectOfType) — alpha=0 blocks all child Image renders");
            }
            else
            {
                Debug.LogWarning("[MapView v2] Fix4b v5: CentralBallWidget not found via FindObjectOfType — G ball may appear over map");
            }
        }

        // iter-33: hide every branch under `root` that is NOT on the path to `keepLeaf` (the SHOOT button).
        // - The keepLeaf subtree stays fully active (the SHOOT/close button + its label).
        // - Ancestors of keepLeaf stay active but their OTHER children (sibling buttons) are hidden.
        // - Everything else under the canvas (player card, wind, gear, GOLFIN, Spin/FadeDraw, etc.) is hidden.
        // Name-agnostic so it can't miss the SHOOT button's container. Only the SHOOT button remains visible.
        private void HideCanvasChildrenExceptPath(Transform root, Transform keepLeaf)
        {
            foreach (Transform child in root)
            {
                if (child == keepLeaf) continue;                                   // keep the SHOOT subtree
                if (transform == child || transform.IsChildOf(child)) continue;    // keep this MVC's branch
                if (keepLeaf.IsChildOf(child))                                      // ancestor of SHOOT
                {
                    HideCanvasChildrenExceptPath(child, keepLeaf);                  // keep it; hide its other children
                    continue;
                }
                if (!child.gameObject.activeSelf) continue;
                _hiddenObjects.Add(child.gameObject);
                child.gameObject.SetActive(false);
            }
        }

        private void RestoreShotUIChrome()
        {
            // Restore MonoBehaviour components first — before Images and GOs —
            // so CentralBallWidget is re-enabled before RefreshSprite is called.
            foreach (var beh in _hiddenBehaviours)
                if (beh != null) beh.enabled = true;
            _hiddenBehaviours.Clear();

            // Restore Image components before re-enabling GOs — so OnEnable/RefreshSprite
            // sees the image as enabled and doesn't need to re-enable it.
            foreach (var img in _hiddenImages)
                if (img != null) img.enabled = true;
            _hiddenImages.Clear();

            foreach (var go in _hiddenObjects)
                if (go != null) go.SetActive(true);
            _hiddenObjects.Clear();
            // Restore CanvasGroup alphas (Fix 4b v4 — CentralBall CanvasGroup.alpha)
            foreach (var cg in _hiddenCanvasGroups)
                if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
            _hiddenCanvasGroups.Clear();
            // Restore ball renderers hidden during Fix 4.
            foreach (var rend in _hiddenBallRenderers)
                if (rend != null) rend.enabled = true;
            _hiddenBallRenderers.Clear();
        }

        /// <summary>
        /// Find all active GameObjects whose name starts with namePrefix and hide their Renderers.
        /// Called from HideShotUIChrome (Fix 4) to cull the GOLFIN ball from the map camera.
        /// Uses FindObjectsByType to avoid tag-lookup failures when "Ball" tag is not registered.
        /// </summary>
        private void HideBallRenderersByName(string namePrefix)
        {
            var allGOs = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var go in allGOs)
            {
                if (!go.name.StartsWith(namePrefix)) continue;
                foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!rend.enabled) continue;
                    rend.enabled = false;
                    _hiddenBallRenderers.Add(rend);
                    Debug.Log($"[MapView v2] Fix4: hid renderer on {go.name}/{rend.gameObject.name}");
                }
            }
        }

        private void RepurposeShootButton(bool mapMode)
        {
            if (_shootButton == null) return;
            // iter-35: if the SHOOT button is a ClubButtonWidget (the repurposed DriverButton), let it own
            // shoot mode — it sets the "SHOOT" label, HIDES the yards secondary, and suppresses its own
            // ClubContext Refresh (so "0.00 yds" no longer shows under SHOOT in map view).
            var clubWidget = _shootButton.GetComponent<ClubButtonWidget>();
            if (mapMode)
            {
                if (clubWidget != null)
                {
                    clubWidget.SetShootMode(true);
                }
                else if (_shootButtonLabel != null)
                {
                    _savedShootLabel       = _shootButtonLabel.text;
                    _shootButtonLabel.text = "SHOOT";
                }
                _shootButton.onClick.RemoveAllListeners();
                _shootButton.onClick.AddListener(Close);
            }
            else
            {
                if (clubWidget != null)
                {
                    clubWidget.SetShootMode(false);
                }
                else if (_shootButtonLabel != null && _savedShootLabel != null)
                {
                    _shootButtonLabel.text = _savedShootLabel;
                }
                _shootButton.onClick.RemoveAllListeners();
            }
        }

        // ── Aim write-back to PhysicsLabController ────────────────────────────────
        private void WriteBackAimToPhysicsLab(float aimRad)
        {
            const System.Reflection.BindingFlags BF =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            var mbs = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in mbs)
            {
                if (mb.GetType().Name != "PhysicsLabController") continue;
                var t = mb.GetType();

                // 1. Write the aim heading (updates _cameraYaw + ShotController.CameraHeadingRadians).
                var setYaw = t.GetMethod("SetCameraYawRadians", BF);
                if (setYaw != null)
                {
                    try { setYaw.Invoke(mb, new object[] { aimRad }); }
                    catch (Exception ex) { Debug.LogWarning($"[MapView v2] WriteBackAim SetCameraYawRadians: {ex.Message}"); }
                }

                // 2. iter-35 (Cesar): SetCameraYawRadians only updates the yaw VALUE — the chase camera
                //    transform doesn't move until the player drags (HandleCameraOrbit calls ApplyCameraYaw).
                //    So the aim guide pointed the new way but the camera stayed put. Reposition the chase
                //    camera now by invoking ApplyCameraYaw(cam) via reflection (keeps this out of
                //    Assets/Scripts/Physics/ per the determinism ban).
                try
                {
                    var chaseField = t.GetField("chaseCamera", BF);
                    var chaseComp  = chaseField?.GetValue(mb) as Component;
                    Camera cam     = chaseComp != null ? chaseComp.GetComponent<Camera>() : null;
                    var applyYaw   = t.GetMethod("ApplyCameraYaw", BF);
                    if (cam != null && applyYaw != null)
                        applyYaw.Invoke(mb, new object[] { cam });
                }
                catch (Exception ex) { Debug.LogWarning($"[MapView v2] WriteBackAim ApplyCameraYaw: {ex.Message}"); }
                return;
            }
        }

        // ── Destroy runtime objects ───────────────────────────────────────────────
        private void DestroyRuntimeObjects()
        {
            // iter-27: _staticCamGO is parented under _runtimeRoot, so Destroy(_runtimeRoot)
            // also destroys the map camera. No "return to idle" needed — the cam simply ceases to
            // exist after map close, which means it cannot interfere with gameplay rendering at all.
            _mapCam      = null;
            _staticCamGO = null;  // will be destroyed as child of _runtimeRoot below
            if (_runtimeRoot != null) { Destroy(_runtimeRoot); _runtimeRoot = null; }
            _mapCam          = null;
            _ballMarker      = null;
            _landingZone     = null;
            _landingZoneRadiusM = 0f;
            _guideLine       = null;
            _ring80GO        = null;
            _ring100GO       = null;
            _ring120GO       = null;
            // §iter-26 FIX #4: _flagLine removed
            _flagIconRT      = null;
            _indicatorCanvas = null;
            _labelCanvas     = null;
            _label80 = _label100 = _label120 = null;
        }

        // ── Marker helpers ────────────────────────────────────────────────────────
        private Transform BuildSphereMarker(string name, Transform parent, Color color, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localScale = Vector3.one * scale;
            go.layer = 0;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null)
            {
                var m = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                m.color = color;
                r.material = m;
            }
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go.transform;
        }

        private TextMeshProUGUI BuildScreenLabel(Transform parent, string text)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = kLabelFontSize;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta   = new Vector2(120f, 50f);
            rt.anchorMin   = rt.anchorMax = Vector2.zero;
            rt.pivot       = new Vector2(0.5f, 0f);
            return tmp;
        }

        // ── §12 EditMode test seams ───────────────────────────────────────────────
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        /// <summary>Inject ball world position (bypasses HoleContext read in Open).</summary>
        public void SetBallWorldPosForTest(Vector3 pos)  => _ballWorldPos = pos;

        /// <summary>Inject carry distance. Also flags _carryValid = true.</summary>
        public void SetCarryYardsForTest(float yards)
        {
            _carryYards = yards;
            _carryValid = yards > 0f && yards < 900f;
        }

        /// <summary>Inject aim yaw directly. aimYaw=0 → +X forward (ShotInputBuilder convention).</summary>
        public void SetAimYawForTest(float yaw)          => _aimYawRadians = yaw;

        /// <summary>Inject fade/draw state for ring-position test assertions.</summary>
        public void SetFadeDrawForTest(bool armed, float finetune)
        {
            _fadeDrawArmed    = armed;
            _fadeDrawFinetune = finetune;
        }

        /// <summary>
        /// Computed landing zone in world space.
        /// Uses same AimDirection2D convention: aimYaw=0 → +X direction.
        /// </summary>
        public Vector3 LandingZoneWorld
        {
            get
            {
                float carryM = _carryYards * kYardsToMeters;
                return _ballWorldPos + AimDirection2D() * carryM;
            }
        }

        /// <summary>
        /// Center world position of a carry ring at the given fraction of carry distance.
        /// iter-20: ALL rings are CONCENTRIC at 100% landing center. This returns the SHARED center.
        /// pct=0.8f / 1.0f / 1.2f all return the same 100% landing center world position.
        /// The difference between rings is their RADIUS, not their center position.
        /// </summary>
        public Vector3 RingCenterAtPct(float pct)
        {
            float carryM = _carryYards * kYardsToMeters;
            // All rings are concentric at the 100% landing position.
            return _ballWorldPos + AimDirection2D() * carryM;
        }

        /// <summary>
        /// Radius (metres) of a carry ring at the given carry fraction.
        /// §6-MODEL (iter-22): r = carryM * _ringFrac * pct, minimum visible size.
        /// pct: 0.80f → innermost (r80), 1.00f → middle (r100), 1.20f → outermost (r120).
        /// </summary>
        public float RingRadiusAtPct(float pct)
        {
            float carryM   = _carryYards * kYardsToMeters;
            float minSize  = pct <= 0.85f ? 2f : (pct <= 1.05f ? 3f : 4f);
            return Mathf.Max(carryM * _ringFrac * pct, minSize);
        }
#endif
    }
}
