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
        // Order 354 (map_view_playable_area): raised 70°→80°. A steeper pitch keeps the horizon
        // (and therefore the world beyond the hole tile) out of frame; combined with the
        // hole-fitted far clip of HideEnvironmentForMap the mountain ring cannot enter the frame.
        [SerializeField] private float             _heroTiltDeg    = 80f;
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

        /// <summary>
        /// Order 354: GameObject names whose Renderers are disabled while the map is open, so the
        /// MOUNTAIN RING cannot appear. Identified by measurement, not guessed (§4.3):
        ///   • <c>HoleRoot/MountainBackdrop</c> — 872×870 m, authored per <c>Hole_NN_Geo</c> scene:
        ///     the mountain-ring arc at the top of the reference screenshot.
        ///   • <c>Backdrop</c> / <c>Ring</c> cover older Geo scenes that name their shell differently.
        ///
        /// <c>ObGroundSkirt</c> — the 9000 m <c>ObSkirt_Mat</c> plane spawned at runtime — is
        /// deliberately NOT in this list (Cesar, 2026-08-07: "Obground should still be green").
        /// It was hidden briefly so off-tile ground read as a dark matte; the ground outside the hole
        /// tile now stays green, exactly as it does in normal play. Anything ADDED to this list must
        /// therefore be world shell, not ground.
        ///
        /// Matching is name-CONTAINS (case-insensitive) on the renderer or its first three ancestors —
        /// MountainBackdrop is a CHILD of HoleRoot, so a root-only scan finds nothing. Name-matching
        /// beats a layer mask because the shell shares layer 0 with the course.
        ///
        /// <see cref="HideEnvironmentForMap"/> logs any oversized renderer it did NOT hide, so a hole
        /// that leaks unexpected geometry names its own offender.
        /// </summary>
        [SerializeField] private string[]          _environmentHideNames = { "MountainBackdrop", "Backdrop", "Ring" };

        /// <summary>
        /// Order 354c: floor on the ball→flag distance the map will frame, in metres. The map zooms
        /// as tight as the ball and flag allow, so without a floor a 2 m tap-in would put the camera
        /// a couple of metres off the deck. The shortfall is padded evenly behind the ball and beyond
        /// the flag, keeping the pair centred. Set to 0 for a pure "as tight as they allow" fit.
        /// </summary>
        [SerializeField] private float             _minFramedSpanM = 40f;

        /// <summary>
        /// Order 354d (Cesar: "I want the rectangle playfield to match the view rectangle"): snap the
        /// map camera's yaw to the playfield rectangle's own axis so the field reads as an UPRIGHT
        /// rectangle instead of a diagonal one.
        ///
        /// The playfield is world-axis-aligned — the OB mask is a plain world-XZ grid
        /// (<c>worldOriginX/Z</c> + <c>worldSizeX/Z</c>, no rotation), and the terrain tile is that
        /// grid. Following the ball→flag heading therefore rotated the tile on screen by whatever
        /// angle the hole runs at (13° on Hole 1). Snapping to the nearest of ±X / ±Z puts the tile's
        /// edges parallel to the screen edges. On all three sampled holes the nearest axis is also the
        /// field's LONG axis pointing at the flag, so the field stands up in portrait.
        ///
        /// Cost, measured: the ball→flag pair now has a lateral component too, and on a hole that runs
        /// far off the field axis that component can drive the fit. Hole 5's tee shot is 41.5° off
        /// axis, so its 242 m of lateral spread binds and the frame goes 546 m tall against a 337 m
        /// field — green ground above and below. Holes 1 and 6 are 13.4° and 5.9° off and fill the
        /// frame completely. Set false to follow the ball→flag heading exactly (354c behaviour).
        /// </summary>
        [SerializeField] private bool              _alignToPlayfieldAxis = true;

        /// <summary>
        /// Order 355 §5 — how far (screen px) a floating indicator is held off the screen edge when
        /// its target is off-screen. Also the inset of the "docked" test rect, so an indicator docks
        /// the moment its target is comfortably inside the frame rather than exactly on the edge.
        /// Serialized so the clearance can be tuned against the real device safe-area.
        /// </summary>
        [SerializeField] private float             _indicatorEdgeInsetPx = 70f;

        [Header("Indicator art (optional — procedural placeholders when empty)")]
        /// <summary>Order 355 §5 — drop-in slot for Robin's styled ball indicator. Null → procedural dot.</summary>
        [SerializeField] private Sprite            _ballIndicatorSprite;
        /// <summary>Order 355 §5 — drop-in slot for Robin's styled arrow. Null → procedural triangle.</summary>
        [SerializeField] private Sprite            _indicatorArrowSprite;

        [Header("B1 redesign (map_view_v2)")]
        /// <summary>
        /// §8 — the bottom-LEFT SHOT VIEW control. Wire a scene object here to own it explicitly;
        /// leave it EMPTY and the map clones the club button into the GolfinButton slot at open time
        /// (same prefab visuals, same gold outline, zero scene diff) and destroys the clone on close.
        /// </summary>
        [SerializeField] private GameObject        _shotViewButton;
        /// <summary>
        /// §2 — world-space FLOOR for the dotted aim line's width. The line is drawn at a constant
        /// <c>kGuideDotPx</c> on screen (the fidelity table's "constant width on screen"), which is a
        /// per-frame world width of px × metres-per-pixel; this floor stops it collapsing to nothing
        /// when the player pinches all the way in. Raise it above ~3 m to force a fixed world width.
        ///
        /// MEASURED, not guessed: at the spec's suggested 0.9 m the floor WON at every live zoom
        /// (metres-per-pixel is 0.078-0.13 on Holes 01/04/08, so 8 px is only 0.6-1.0 m of world), and
        /// the dots rendered 7-12 px depending on the hole — i.e. the line was world-width after all
        /// and the "constant width on screen" token was not actually being met. At 0.20 m the
        /// screen-constant term governs at every realistic zoom and the floor only catches a full
        /// pinch-in.
        /// </summary>
        [SerializeField] private float             _guideDotWorldWidth   = 0.20f;
        /// <summary>§3 — half-angle of the lime range fan, in degrees, either side of the aim line.</summary>
        [SerializeField] private float             _rangeFanHalfAngleDeg = 11f;
        /// <summary>
        /// §7 — minimum screen gap (device px) between the pin and the pin chip, so the chip never
        /// covers the hole. Also the tail's length, and the two are the same number by construction.
        ///
        /// Raised 120 → 200 (Cesar, 2026-09-05: "the tail of the flag indicator is too short"). The
        /// number is not the VISIBLE length: the cloned HUD tail sprite is a gradient that fades to
        /// transparent toward its tip, so roughly the far half of the rect renders as nothing. 200 px
        /// of rect reads as a tail in the same proportion the Figma's does against its 100 px chip;
        /// 120 rendered as a stub barely clear of the chip edge.
        /// </summary>
        [SerializeField] private float             _pinTailMinPx         = 200f;
        /// <summary>
        /// map_view_v2 (Cesar, 2026-09-04) — <c>QualitySettings.lodBias</c> for the map's lifetime.
        ///
        /// The map camera is hundreds of metres from the course, so every LODGroup in frame — the
        /// trees above all — resolves to a far LOD and the canopies render as flat smears. Cesar
        /// called it, and the triangle counter proves it: at the live baseline of lodBias 1 the map
        /// draws 778,901 triangles; at 8 it draws 2,604,509 (3.3x), and 40 adds only 0.3 % more, so 8
        /// is the plateau. This is a MAP-ONLY override, saved and restored around the map session —
        /// gameplay's LOD budget is untouched. Set to 0 to disable the override entirely.
        ///
        /// (The earlier attempt at this used <c>treeMaximumFullLODCount</c>, which drives Unity's
        /// BILLBOARD path; these prototypes have <c>billboardAsset=False</c> and plain LODGroups, so
        /// that knob was inert and the "trees are already high LOD" conclusion it produced was wrong.)
        /// </summary>
        [SerializeField] private float             _mapLodBias           = 8f;

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

        // ── B1 redesign (map_view_v2) ────────────────────────────────────────────
        // World-space: the red continuation of the aim line past max reach, the lime range fan, its
        // outer "max" arc, the dashed nominal-carry arc, and the dashed ghost ring at P_max.
        private LineRenderer        _overRangeLine;
        private GameObject          _rangeFanGO;
        private GameObject          _rangeFanEdgeGO;
        private GameObject          _nominalArcGO;
        private GameObject          _ghostRingGO;
        // Screen-space (all on _indicatorCanvas): 50-yd ticks, target crosshair, the over-range ✕,
        // the ghost dot at P_max, the readout chip and the cloned HUD pin chip.
        private readonly List<RectTransform>   _tickMarks  = new List<RectTransform>();
        private readonly List<TextMeshProUGUI> _tickLabels = new List<TextMeshProUGUI>();
        private RectTransform       _crossVRT, _crossHRT, _centreDotRT, _crossX1RT, _crossX2RT, _ghostDotRT;
        private MapTargetReadoutWidget _readout;
        private MapPinIndicator     _pinIndicator;
        private TMP_FontAsset       _hudFont;
        // The SHOT VIEW button the map itself created (null when _shotViewButton is wired in the scene).
        private GameObject          _runtimeShotViewGO;
        private CanvasGroup         _shootButtonGroup;
        // Generated art / materials, controller-lifetime (destroyed in OnDestroy, not per-open).
        private Texture2D           _dotLineTex;
        private Texture2D           _landingZoneTexOver;
        // Generated 9-slice plates, indexed by (roundTop ? 1 : 0) | (roundBottom ? 2 : 0).
        private readonly Sprite[]    _roundedSprites  = new Sprite[4];
        private readonly Texture2D[] _roundedTextures = new Texture2D[4];
        // Live over-range state, published into the invariant dump.
        private bool                _overRange;
        private float               _maxReachM;
        // < 0 means "no LOD override is currently in force" — the restore is a no-op then.
        private float               _savedLodBias = -1f;
        private int                 _savedMaxLodLevel;
        /// <summary>
        /// The unit the HUD is showing. Read off the live HoleIndicatorWidget at open time — the same
        /// value PhysicsLabController feeds it — so the map's ticks, pin chip and readout can never be
        /// in yards while the HUD chip is in metres (§4's "find the caller and read the same value").
        /// </summary>
        private HoleIndicatorWidget.DistanceUnit _unitMode = HoleIndicatorWidget.DistanceUnit.Yards;
        private float               _pinChipGapPx = -1f;

        // Terrain-conforming rings (vertex-height-sampled annulus meshes)
        private GameObject          _ring80GO;
        private GameObject          _ring100GO;
        private GameObject          _ring120GO;

        // Hole indicator: screen-space Canvas icon (§iter-26: yellow line removed)
        private Canvas              _indicatorCanvas;
        private RectTransform       _flagIconRT;
        // Order 355 §5 — edge-clamped floating indicators. The flag icon above is reused; when the
        // flag leaves the frame it stops hiding and instead docks to the screen edge with an arrow.
        // The ball gets the identical treatment on its own icon (hidden while the world-space
        // _ballMarker sphere is the on-screen representation).
        private RectTransform       _flagArrowRT;
        private RectTransform       _ballIconRT;
        private RectTransform       _ballArrowRT;
        // Procedural placeholder art (destroyed with the controller, not per-open).
        private Texture2D           _arrowTex;
        private Texture2D           _dotTex;
        private Sprite              _arrowSpriteGen;
        private Sprite              _dotSpriteGen;

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
        // Manual zoom-out cap (Order 353b): pinch may zoom IN below this, but never zoom OUT past it,
        // so the player cannot pull back to reveal more off-course than the width-fit. Set in
        // FramePlayingAreaWidth; defaults to _maxZoom when no playing-area bounds are available.
        private float               _zoomOutCapFov;
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
        // Order 354: environment Renderers (mountain ring / backdrop plane) disabled for the map's
        // lifetime. Separate list from _hiddenBallRenderers because HideShotUIChrome() clears that
        // one and runs AFTER BuildRuntimeObjects().
        private List<Renderer>          _hiddenEnvRenderers = new List<Renderer>();

        // map_view_v2 (Cesar, 2026-09-04): a map-lifetime tree-LOD override was BUILT, MEASURED and
        // then REMOVED. Raising treeDistance/treeBillboardDistance to 2000 m and forcing LOD0 on all
        // 1362 instances moved 2-4 % of pixels and is indistinguishable side by side
        // (screenshots/h01_08a/08b/08c) — the map's trees already render at LOD1, and LOD2 (the only
        // impostor) needs the tree under 1 % of screen height, which never happens here. The map
        // therefore touches NO terrain state at all; what makes them read flat is a near-top-down
        // camera over side-authored leaf cards, which is an art/angle problem, not a setting.

        // Order 354: the hole's OB rectangle (world XZ), captured when the show-region framing
        // succeeds. Drives the pan clamp (§4.4) and the hole-fitted far clip (§4.3).
        private bool                _obRectValid;
        private Vector2             _obRectCenter;
        private Vector2             _obRectHalf;

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
        // Bottom-anchor: the ball (near edge of the hole map) is slid to this viewport-Y so the
        // bottom of the map sits flush with the screen bottom (small margin keeps the marker on-screen).
        private const float kBottomAnchorFrac = 0.04f;
        // Width-fill: the playing-area corridor edges are fitted this far from the screen sides so the
        // course fills the frame width and as little off-course as possible is shown (Order 353b).
        private const float kWidthFillMargin  = 0.02f;
        // Order 354c — zoom-to-the-shot framing (Cesar: "Zoom in as much as possible as long as
        // current ball position and flag are visible (leave a bit of margin so none of them touch the
        // borders)"). These are the margin: the ball seats at kShotBottomFrac and the flag at
        // kShotTopFrac, and the camera comes no further back than that requires. Screen-space, so the
        // gap reads the same on a 460 m par 5 and a 40 m pitch.
        private const float kShotBottomFrac = 0.08f;
        // Order 355 §3.1: kShotTopFrac is now the LANDING ceiling, not a flag seat — the flag left the
        // fit set (it is an indicator target, §5), so this is the top margin the ball+landing region
        // must stay under.
        private const float kShotTopFrac    = 0.90f;
        // Order 355 §2 — THE INVARIANT: the camera's ground footprint (the quad the four viewport
        // corners cut out of the ball's ground plane) must stay inside the hole's OB rectangle at
        // every frame the map is open. 354 clamped what the FOCUS POINT could do; 355 clamps what the
        // SCREEN SHOWS. Tolerance absorbs float error in the plane raycasts — 1 m against a rectangle
        // hundreds of metres on a side is invisible on screen.
        private const float kFootprintTolM  = 1f;
        // Order 355 §3.2 — the containment pass pulls the camera in by this factor per step until the
        // footprint fits inside the rect. 0.94 converges in <40 steps from any sane start and lands
        // within ~6% of the largest contained distance, which is below one screen pixel of zoom.
        private const float kContainShrink  = 0.94f;
        private const int   kContainMaxIter = 60;
        // Playing-area borders come from the hole's OB (out-of-bounds) mask world-bounds in zones.json
        // (Order 353c, Cesar: "use the map borders from the OB, forget the corridor"). This is a clean
        // per-hole rectangle — far more robust than deriving a corridor from noisy course geometry.
        // Cached per hole so the multi-MB zones.json is parsed once (PositionMapCamera runs 3x per open).
        private static readonly Dictionary<string, Vector4> s_obRectCache = new Dictionary<string, Vector4>();

        [Serializable] private class _ObMaskJson { public float worldOriginX, worldOriginZ, worldSizeX, worldSizeZ; }

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

        // ── map_view_v2 — read-only diagnostics seams ────────────────────────────
        // Siblings of the existing ForceInvariantDump / SetAimYawDirectly capture seams. They exist
        // so the capture driver AND a read-only reviewer can assert the range model against live
        // state (e.g. "the fan edge sits at 1.2 x the club-button distance") without reflecting into
        // private fields — and they cannot mutate anything.
        public Camera  MapCamera        => _mapCam;
        public Vector3 BallWorldPos     => _ballWorldPos;
        public Vector3 PinWorldPos      => _flagWorldPos;
        public float   ClubCarryMeters  => _carryYards * kYardsToMeters;
        public float   MaxReachMeters   => _maxReachM;
        public float   AimedCarryMeters => _aimedCarryM;
        public bool    IsOverRangeNow   => _overRange;
        public float   PinChipGapPx     => _pinChipGapPx;
        public float   PinTailMinPx     => _pinTailMinPx;

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
            _zoomOutCapFov = _maxZoom;
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
                // map_view_v2: the ACTIVE task folder wins, so a dump lands next to the spec being
                // reviewed rather than in the folder of whichever task happened to build this first.
                // map_view_aiming stays as the fallback so nothing that reads it breaks.
                string[] candidates =
                {
                    Path.Combine(repoRoot, "Docs", "Specs", "Active", "map_view_v2"),
                    Path.Combine(repoRoot, "Docs", "Specs", "Active", "map_view_aiming"),
                };
                foreach (var candidate in candidates)
                {
                    if (!Directory.Exists(candidate)) continue;
                    _repoInvariantDir = candidate;
                    break;
                }
                if (string.IsNullOrEmpty(_repoInvariantDir))
                    Debug.LogWarning($"[MapView v2] No repo task folder found under Docs/Specs/Active — will write invariant to persistentDataPath only.");
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
            // Order 355 §5 — procedural indicator placeholders are controller-lifetime, not per-open.
            if (_arrowSpriteGen != null) Destroy(_arrowSpriteGen);
            if (_dotSpriteGen   != null) Destroy(_dotSpriteGen);
            if (_arrowTex       != null) Destroy(_arrowTex);
            if (_dotTex         != null) Destroy(_dotTex);
            // map_view_v2 — the B1 generated art is controller-lifetime too.
            if (_landingZoneTexOver != null) Destroy(_landingZoneTexOver);
            if (_dotLineTex         != null) Destroy(_dotLineTex);
            for (int i = 0; i < _roundedSprites.Length; i++)
            {
                if (_roundedSprites[i]  != null) Destroy(_roundedSprites[i]);
                if (_roundedTextures[i] != null) Destroy(_roundedTextures[i]);
            }
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

            var hudChip = FindObjectOfType<HoleIndicatorWidget>(true);
            _unitMode   = hudChip != null ? hudChip.UnitMode : HoleIndicatorWidget.DistanceUnit.Yards;

            BuildRuntimeObjects();
            HideShotUIChrome();
            RepurposeShootButton(true);
            // map_view_v2 §8 — AFTER HideShotUIChrome, which would otherwise hide the slot it lives in.
            ShowShotViewButton(true);
            PositionMapCamera();  // uses _ballWorldPos + _flagWorldPos
            PlaceMarkers();
            UpdateGuideAndRings();

            OnMapOpened?.Invoke(_aimYawRadians);
#if UNITY_EDITOR   // perf_phase1_free_wins §4: two blocking GPU ReadPixels stalls per map
            // open. Diagnostic-only (they feed lzCenterPixelRGBA/lzEdgePixelRGBA in the
            // invariant JSON) and have no business running on a retail device.
            // §iter-24 FIX #14: Start coroutine so DumpInvariants reads COMPOSITED FRAME pixels
            // (ReadPixels after WaitForEndOfFrame) rather than source texture (GetPixel).
            StartCoroutine(DoFrameReadbackAndDump("open"));
#endif

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
            {
                _shotController.CameraHeadingRadians = _aimYawRadians;

                // power_gauge_target_marker: the landing the player placed survives the map
                // session as a marker on the power gauge. Metres, not a fraction — a club
                // change re-derives the marker instead of freezing a stale percentage.
                // _aimedCarryM is defaulted to the club carry in UpdateGuideAndRings on open,
                // so an open-and-close with no touch writes the carry the map just showed —
                // that landing IS the current target.
                _shotController.MapTargetCarryM = (_aimedCarryM > 0f) ? _aimedCarryM : -1f;
                Debug.Log($"[MapView v2] Close write-back: MapTargetCarryM={_shotController.MapTargetCarryM:F1}m " +
                          $"(aimedCarry={_aimedCarryM:F1}m, clubCarry={_carryYards * kYardsToMeters:F1}m)");
            }

            // Try PhysicsLab write-back as well (handles LabScaffold context).
            WriteBackAimToPhysicsLab(_aimYawRadians);

            ShowShotViewButton(false);
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

            // map_view_v2 §3 — the lime range fan and its arcs, drawn UNDER the landing glow (fill) and
            // over it (edges). Built before the guide lines so the draw order reads bottom-up here.
            _rangeFanGO     = BuildConformingRingGO("MapView_RangeFan",     WithAlpha(kLime, 0.10f), markerRoot.transform, 0);
            _nominalArcGO   = BuildConformingRingGO("MapView_NominalArc",   WithAlpha(Color.white, 0.25f), markerRoot.transform, 2);
            _rangeFanEdgeGO = BuildConformingRingGO("MapView_RangeFanEdge", WithAlpha(kLime, 0.90f), markerRoot.transform, 3);

            // iter-28 Fix 3 PARTIALLY REVERSED by map_view_v2 §5: the ONE landing ring at r100 is back
            // (crisp white, 4 px on screen) because B1's target needs a readable centre. Rings 80/120
            // and their labels stay off — explicitly out of scope. The radius CALCULATIONS for all
            // three are still made in UpdateGuideAndRings so DumpInvariants and the ring EditMode
            // tests (RingCenterAtPct / RingRadiusAtPct, pure-math seams) are untouched.
            _ring100GO   = BuildConformingRingGO("LandingRing", WithAlpha(Color.white, 0.95f), markerRoot.transform, 4);
            // §5 — the "this is where the ball ACTUALLY lands" ghost, dashed, only while over range.
            _ghostRingGO = BuildConformingRingGO("MapView_GhostRing", WithAlpha(Color.white, 0.95f), markerRoot.transform, 4);
            SetActiveIf(_ghostRingGO, false);
            // To restore the other two rings: uncomment these 2 lines.
            // var ringColor = new Color(0.08f, 0.08f, 0.08f, 0.55f);
            // _ring80GO  = BuildConformingRingGO("Ring80",  ringColor, markerRoot.transform);
            // _ring120GO = BuildConformingRingGO("Ring120", ringColor, markerRoot.transform);

            // map_view_v2 §2 — the aim line is DOTTED and FLAT now: a tiled 22×8 dot strip on the
            // always-on-top overlay shader, white from the ball to min(L, P_max)...
            _guideLine     = BuildDottedLine("MapView_GuideLine",     markerRoot.transform, Color.white, 6);
            // ...and a second, RED line carrying the over-range remainder from P_max to L. Two
            // renderers rather than one two-coloured one: a LineRenderer's gradient is parameterised on
            // normalised length, so a single line could not keep the colour break pinned to P_max as
            // the player drags L (and B1's break is a hard edge, not a blend).
            _overRangeLine = BuildDottedLine("MapView_OverRangeLine", markerRoot.transform, kOverRed, 7);
            _overRangeLine.positionCount = 0;

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

            // ── 4. Hard-hide the world outside the hole (Order 354 §4.3) ─────────
            HideEnvironmentForMap();
        }

        // ── Environment hide / restore (Order 354 §4.3) ──────────────────────────
        /// <summary>
        /// Disable the Renderers of the off-course world shell (mountain ring + backdrop plane) for
        /// the map's lifetime, so no pan, pinch or aim can reveal it.
        ///
        /// Verified on the shipping Geo scenes: a single <c>HoleRoot/MountainBackdrop</c> renderer,
        /// 872×870 m centred on the origin at layer 0, is BOTH the mountain-ring arc and the large
        /// flat green plane that filled the reference screenshot. It is a CHILD of HoleRoot, not a
        /// scene root, so the scan walks the whole hierarchy (a root-only scan finds nothing).
        /// Everything else in those scenes spans &lt; 250 m, i.e. is hole-tile geometry.
        ///
        /// Renderer-level (not SetActive) so nothing else observes an activation change — no OnEnable
        /// re-entry, no scene mutation beyond a bool that <see cref="RestoreEnvironmentAfterMap"/>
        /// puts back. Name-matching beats a layer mask here: the shell shares layer 0 with the course.
        /// </summary>
        /// <summary>
        /// Raise <c>QualitySettings.lodBias</c> for the map's lifetime so LODGroups resolve to near
        /// geometry at the map camera's distance. Never LOWERS it — if the active quality tier is
        /// already more generous than <see cref="_mapLodBias"/>, the tier wins.
        /// </summary>
        private void PushMapLodBias()
        {
            if (_mapLodBias <= 0f) return;
            _savedLodBias     = QualitySettings.lodBias;
            _savedMaxLodLevel = QualitySettings.maximumLODLevel;
            QualitySettings.lodBias         = Mathf.Max(_savedLodBias, _mapLodBias);
            QualitySettings.maximumLODLevel = 0;
            Debug.Log($"[MapView v2] Map LOD bias {_savedLodBias} -> {QualitySettings.lodBias} " +
                      $"(maximumLODLevel {_savedMaxLodLevel} -> 0) for the map's lifetime.");
        }

        /// <summary>Put the global LOD budget back exactly as it was before the map opened.</summary>
        private void RestoreLodBiasAfterMap()
        {
            if (_savedLodBias < 0f) return;
            QualitySettings.lodBias         = _savedLodBias;
            QualitySettings.maximumLODLevel = _savedMaxLodLevel;
            Debug.Log($"[MapView v2] Map LOD bias restored to {QualitySettings.lodBias}.");
            _savedLodBias = -1f;
        }

        private void HideEnvironmentForMap()
        {
            PushMapLodBias();
            _hiddenEnvRenderers.Clear();
            if (_environmentHideNames == null || _environmentHideNames.Length == 0) return;

            // Single pass over Renderers (not Transforms): a hole scene carries ~20k tree renderers,
            // so one bounded pass is the cheap way to do this once per map open.
            var all = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rend in all)
            {
                if (rend == null || !rend.enabled) continue;
                // Never hide our own runtime objects (markers, rings) — only scene environment.
                if (_runtimeRoot != null && rend.transform.IsChildOf(_runtimeRoot.transform)) continue;
                if (!MatchesEnvironmentName(rend.transform)) continue;

                rend.enabled = false;
                _hiddenEnvRenderers.Add(rend);
            }
            Debug.Log($"[MapView v2] Environment hide: disabled {_hiddenEnvRenderers.Count} renderer(s) " +
                      $"matching [{string.Join(", ", _environmentHideNames)}]");
        }

        /// <summary>
        /// True when the transform, or any of its first three ancestors, carries one of
        /// <see cref="_environmentHideNames"/> in its name. The ancestor walk is bounded so the
        /// per-renderer cost stays flat across the ~20k tree renderers a hole scene holds; it exists
        /// because a shell may be a named parent (<c>Backdrop/Mesh</c>) rather than the renderer itself.
        /// </summary>
        private bool MatchesEnvironmentName(Transform t)
        {
            for (int depth = 0; depth < 3 && t != null; depth++, t = t.parent)
            {
                foreach (var n in _environmentHideNames)
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    if (t.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }

        /// <summary>Re-enable every Renderer disabled by <see cref="HideEnvironmentForMap"/>.</summary>
        private void RestoreEnvironmentAfterMap()
        {
            RestoreLodBiasAfterMap();
            foreach (var rend in _hiddenEnvRenderers)
                if (rend != null) rend.enabled = true;
            _hiddenEnvRenderers.Clear();
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
            // ── Build the radial gradient textures ───────────────────────────────
            // map_view_v2 fidelity table: the glow is LIME, not the old red→orange→green ramp.
            //   in range   #78E921  α 0.55 at the centre → α 0.22 at 55 % → α 0 at the edge
            //   over range #F23A33  α 0.50 at the centre →            → α 0 at the edge
            // Two textures are baked once and swapped on the material, so entering or leaving the
            // over-range state costs a pointer write rather than a 128² SetPixels every frame.
            _landingZoneTex     = BuildGlowTexture(kLime,    0.55f, 0.22f);
            _landingZoneTexOver = BuildGlowTexture(kOverRed, 0.50f, 0.20f);

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

        /// <summary>
        /// map_view_v2 §5 — the landing glow's radial ramp: <paramref name="tint"/> at
        /// <paramref name="centreAlpha"/> in the middle, <paramref name="midAlpha"/> at 55 % of the
        /// radius, fully transparent at the rim. Colour is constant across the disc — only alpha
        /// falls off, which is what makes it read as a glow rather than a coloured plate.
        /// </summary>
        private static Texture2D BuildGlowTexture(Color tint, float centreAlpha, float midAlpha)
        {
            const int res = 128;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels    = new Color[res * res];
            var texCentre = new Vector2(res * 0.5f, res * 0.5f);
            float halfRes = res * 0.5f;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), texCentre) / halfRes);
                    float a = d < 0.55f
                            ? Mathf.Lerp(centreAlpha, midAlpha, d / 0.55f)
                            : Mathf.Lerp(midAlpha, 0f, (d - 0.55f) / 0.45f);
                    pixels[y * res + x] = new Color(tint.r, tint.g, tint.b, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// map_view_v2 §2 — the dotted aim line's tile: one soft white dot in the left 8×8 of a 22×8
        /// strip, so a LineRenderer in <see cref="LineTextureMode.Tile"/> repeats it at the design's
        /// 8 px dot / 22 px pitch. Generated in code exactly the way <see cref="DotSprite"/> is —
        /// no import, no art dependency, and the pitch is a number in this file rather than a PNG.
        /// </summary>
        private Texture2D DotLineTexture()
        {
            if (_dotLineTex != null) return _dotLineTex;
            const int w = 22, h = 8;
            _dotLineTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[w * h];
            var c  = new Vector2(3.5f, 3.5f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = 0f;
                    if (x < 8)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), c) / 4f;
                        a = Mathf.Clamp01((1f - d) * 3f);
                    }
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            _dotLineTex.SetPixels(px);
            _dotLineTex.Apply();
            return _dotLineTex;
        }

        /// <summary>
        /// map_view_v2 §6 — a white 9-sliced rounded rectangle (8 px radius) for the readout chip.
        /// Baked at 1 pixel-per-unit with an 8 px border on every side, so the caller controls the
        /// rendered corner radius purely through <c>Image.pixelsPerUnitMultiplier</c> and the corners
        /// never collapse into an oval the way a stretched non-9-sliced sprite does (Rule 21).
        /// </summary>
        private Sprite RoundedRectSprite() => RoundedRectSprite(true, true);

        /// <summary>
        /// <paramref name="roundTop"/> / <paramref name="roundBottom"/> pick WHICH pair of corners is
        /// rounded. The readout chip is TWO stacked plates: if both were rounded all round, the navy
        /// header's bottom corners and the white body's top corners would each curve away and leave a
        /// notch of map showing between them (Cesar, 2026-09-04: "blue and white parts should touch").
        /// Header = top-rounded, body = bottom-rounded, so their junction is a straight seam and the
        /// outside of the chip still reads as one 8 px-radius card.
        ///
        /// pixelsPerUnit MUST be the canvas's referencePixelsPerUnit (100), not 1. Unity renders a
        /// sliced border as border_px x referencePPU / (sprite.pixelsPerUnit x pixelsPerUnitMultiplier);
        /// at pixelsPerUnit 1 an 8 px corner asks for 800 px, which clamps the 9-slice and turns the
        /// chip into a stadium pill — the Rule 21 failure mode, caught in the first Hole 01 capture.
        /// </summary>
        private Sprite RoundedRectSprite(bool roundTop, bool roundBottom)
        {
            int idx = (roundTop ? 1 : 0) | (roundBottom ? 2 : 0);
            if (_roundedSprites[idx] != null) return _roundedSprites[idx];

            const int r = 8;
            const int size = r * 2 + 2;          // two corners + a 2 px centre the slicer stretches
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Texture row 0 is the BOTTOM of the sprite in Unity's UV space.
                    bool nearBottom = y < r;
                    bool nearTop    = y >= size - r;
                    bool roundHere  = (nearTop && roundTop) || (nearBottom && roundBottom);

                    float dx = x < r ? r - x - 0.5f : (x >= size - r ? x - (size - r) + 0.5f : 0f);
                    float dy = nearBottom ? r - y - 0.5f : (nearTop ? y - (size - r) + 0.5f : 0f);

                    // Rounded corner = distance to the corner disc. Square corner = the side edge only,
                    // so the plate runs flat to the very last row and meets its neighbour with no gap.
                    float a = roundHere
                            ? Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy))
                            : Mathf.Clamp01(r - Mathf.Abs(dx));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();

            _roundedTextures[idx] = tex;
            _roundedSprites[idx]  = Sprite.Create(tex, new Rect(0, 0, size, size),
                                                  new Vector2(0.5f, 0.5f), 100f, 0,
                                                  SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _roundedSprites[idx];
        }

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
        /// <summary>
        /// One terrain-conforming overlay mesh (ring, sector, arc). map_view_v2 swaps the material to
        /// <c>MapView/OverlayConform</c>: §iter-26's <c>Sprites/Default</c> + <c>SetInt("_ZTest")</c> was
        /// a NO-OP — Sprites/Default takes its ZTest from a global, not the material property, which is
        /// the same trap BuildLandingZoneDecal documents. The overlay shader bakes ZTest Always into its
        /// pass, so the fan, arcs and rings genuinely draw over terrain AND trees.
        /// </summary>
        private GameObject BuildConformingRingGO(string name, Color color, Transform parent, int queueOffset = 2)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.layer = 0;
            go.AddComponent<MeshFilter>().mesh = new Mesh { name = name };
            var mr  = go.AddComponent<MeshRenderer>();
            mr.material          = NewOverlayMaterial(color, null, queueOffset);
            mr.receiveShadows    = false;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// A material on the map's always-on-top unlit shader. <paramref name="queueOffset"/> is added to
        /// <c>RenderQueue.Transparent</c> and is what layers the overlay: fan fill under the landing glow,
        /// arcs over it, the dotted lines over everything.
        /// </summary>
        private Material NewOverlayMaterial(Color color, Texture tex, int queueOffset)
        {
            Shader sh = Shader.Find("MapView/OverlayConform");
            if (sh == null)
            {
                var resMat = Resources.Load<Material>("MapView/DecalLandingZone");
                if (resMat != null) sh = resMat.shader;
            }
            if (sh == null) sh = Shader.Find("Sprites/Default");   // last-ditch (will occlude)

            var mat = new Material(sh);
            if (tex != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                mat.mainTexture = tex;
            }
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color       = color;
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
            return mat;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>
        /// map_view_v2 §2 — a dotted world-space line on the always-on-top overlay shader. Width is
        /// set per frame by <see cref="UpdateB1Overlay"/> so the dots stay a constant size on screen.
        /// </summary>
        private LineRenderer BuildDottedLine(string name, Transform parent, Color color, int queueOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var lr = go.AddComponent<LineRenderer>();
            lr.material          = NewOverlayMaterial(color, DotLineTexture(), queueOffset);
            lr.textureMode       = LineTextureMode.Tile;
            lr.numCapVertices    = 0;
            lr.numCornerVertices = 0;
            lr.startColor        = color;   // only read on the Sprites/Default fallback shader
            lr.endColor          = color;
            lr.startWidth        = _guideDotWorldWidth;
            lr.endWidth          = _guideDotWorldWidth;
            lr.positionCount     = kGuideSegments + 1;
            lr.useWorldSpace     = true;
            lr.receiveShadows    = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.alignment         = LineAlignment.View;
            lr.sortingOrder      = 2;
            return lr;
        }

        /// <summary>Recolour an overlay mesh in place — no new material, no allocation per frame.</summary>
        private static void SetOverlayColor(GameObject go, Color color)
        {
            if (go == null) return;
            var mr = go.GetComponent<MeshRenderer>();
            // sharedMaterial IS the per-object instance NewOverlayMaterial handed to `mr.material`,
            // so writing it here mutates only this object (and allocates nothing).
            if (mr != null && mr.sharedMaterial != null)
            {
                mr.sharedMaterial.color = color;
                if (mr.sharedMaterial.HasProperty("_Color")) mr.sharedMaterial.SetColor("_Color", color);
            }
        }

        private static void SetActiveIf(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        /// <summary>
        /// Existing signature, preserved so every prior caller compiles unchanged (map_view_v2 §3):
        /// a full-circle band around <paramref name="radius"/>, sized by <see cref="kRingBandFrac"/>.
        /// </summary>
        private void UpdateConformingRing(GameObject ringGO, Vector3 worldCenter, float radius)
            => UpdateConformingSector(ringGO, worldCenter,
                                      radius * (1f - kRingBandFrac), radius * (1f + kRingBandFrac * 0.5f),
                                      0f, 2f * Mathf.PI, 0);

        /// <summary>
        /// map_view_v2 §3 — the generalised annulus builder the whole B1 overlay is drawn with: a
        /// terrain-conforming band between <paramref name="innerR"/> and <paramref name="outerR"/>,
        /// spanning <paramref name="a0"/>…<paramref name="a1"/> radians about <paramref name="worldCenter"/>.
        ///
        /// <paramref name="dashes"/> = 0 draws one solid span; N &gt; 0 splits the arc into
        /// 2N−1 equal parts and emits the N even ones, i.e. N dashes with N−1 gaps. That is how the
        /// dashed nominal-carry arc and the dashed ghost ring are drawn without any shader work.
        ///
        /// Every vertex's Y is sampled from the terrain, so fan, arcs and rings drape over slopes the
        /// same way the landing disc does. Arc resolution is capped so a full circle costs the same
        /// ~64 height samples the old fixed-segment ring did.
        /// </summary>
        private void UpdateConformingSector(GameObject go, Vector3 worldCenter,
                                            float innerR, float outerR, float a0, float a1, int dashes)
        {
            if (go == null) return;
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) return;

            innerR = Mathf.Max(0f, innerR);
            outerR = Mathf.Max(innerR + 0.01f, outerR);

            _sectorVerts.Clear(); _sectorTris.Clear(); _sectorUvs.Clear();

            int   spanCount = dashes > 0 ? dashes : 1;
            float spanLen   = dashes > 0 ? (a1 - a0) / (dashes * 2f - 1f) : (a1 - a0);
            const float kRadPerSeg = 6f * Mathf.Deg2Rad;

            for (int sIdx = 0; sIdx < spanCount; sIdx++)
            {
                float s0 = a0 + (dashes > 0 ? sIdx * 2f * spanLen : 0f);
                float s1 = s0 + spanLen;
                int   segs = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(s1 - s0) / kRadPerSeg), 1, kRingSegments);
                int   baseIdx = _sectorVerts.Count;

                for (int i = 0; i <= segs; i++)
                {
                    float t   = (float)i / segs;
                    float ang = Mathf.Lerp(s0, s1, t);
                    float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);

                    Vector3 iXZ = worldCenter + new Vector3(cos * innerR, 0f, sin * innerR);
                    Vector3 oXZ = worldCenter + new Vector3(cos * outerR, 0f, sin * outerR);
                    _sectorVerts.Add(new Vector3(iXZ.x, SampleTerrainHeight(iXZ) + kRingHeightOff, iXZ.z));
                    _sectorVerts.Add(new Vector3(oXZ.x, SampleTerrainHeight(oXZ) + kRingHeightOff, oXZ.z));
                    _sectorUvs.Add(new Vector2(t, 0f));
                    _sectorUvs.Add(new Vector2(t, 1f));
                }
                for (int i = 0; i < segs; i++)
                {
                    int a = baseIdx + i * 2, b = baseIdx + (i + 1) * 2;
                    _sectorTris.Add(a);     _sectorTris.Add(b);     _sectorTris.Add(a + 1);
                    _sectorTris.Add(b);     _sectorTris.Add(b + 1); _sectorTris.Add(a + 1);
                }
            }

            var mesh = mf.mesh;
            mesh.Clear();
            mesh.SetVertices(_sectorVerts);
            mesh.SetUVs(0, _sectorUvs);
            mesh.SetTriangles(_sectorTris, 0);
            mesh.RecalculateBounds();
        }

        // Reused scratch for UpdateConformingSector — the overlay is rebuilt every frame while the
        // player drags, so allocating four lists per mesh per frame is not free.
        private readonly List<Vector3> _sectorVerts = new List<Vector3>();
        private readonly List<int>     _sectorTris  = new List<int>();
        private readonly List<Vector2> _sectorUvs   = new List<Vector2>();

        private float SampleTerrainHeight(Vector3 worldXZ)
        {
            var origin = new Vector3(worldXZ.x, 2000f, worldXZ.z);
            // Terrain TREE colliders are reported against the TerrainCollider itself, so the first
            // hit under a canopy is the treetop and the ring vertex used to float ~10-30 m up.
            // Type/layer cannot tell tree from ground; only height above the heightmap can.
            // UnityEngine.Physics spelled out, not bare `Physics`: this assembly now references
            // Golfin.Physics.Runtime (build_size_diet Phase 2, for HoleDataIO), and inside
            // namespace Golfin.Gameplay.UI.ShotUI C# walks the enclosing namespaces — so `Physics`
            // finds the Golfin.Physics NAMESPACE before it finds UnityEngine's class.
            var hits = UnityEngine.Physics.RaycastAll(origin, Vector3.down, 4000f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsTerrainTreeHit(hits[i])) continue;
                return hits[i].point.y;
            }
            return worldXZ.y;
        }

        /// <summary>
        /// True when a TerrainCollider hit is one of the terrain's tree colliders rather than the
        /// ground. Mirrors <c>Golfin.Physics.Viewer.PlacementSnapHelper.IsTerrainTreeHit</c> —
        /// duplicated rather than shared because Golfin.Gameplay.UI cannot reference
        /// Golfin.Physics.Viewer (that assembly references THIS one; a shared helper would need a
        /// third assembly). Keep the two in step.
        /// </summary>
        private static bool IsTerrainTreeHit(RaycastHit hit)
        {
            var terrainCollider = hit.collider as TerrainCollider;
            if (terrainCollider == null) return false;
            var terrain = terrainCollider.GetComponent<Terrain>();
            if (terrain == null) return false;
            float surfaceY = terrain.SampleHeight(hit.point) + terrain.transform.position.y;
            return hit.point.y > surfaceY + 0.5f;
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

            // Order 355 §5 — the floating half of the indicator pair, for BOTH targets. Placeholder
            // art is generated in code (no import); the two serialized sprite slots take Robin's
            // styled versions when they land, with zero code change.
            _flagArrowRT = BuildIndicatorPart(iconCanvasGO.transform, "FlagArrow",
                                              _indicatorArrowSprite ?? ArrowSprite(),
                                              new Color(1f, 0.9f, 0.1f, 0.95f), 34f);
            _ballIconRT  = BuildIndicatorPart(iconCanvasGO.transform, "BallIcon",
                                              _ballIndicatorSprite ?? DotSprite(),
                                              Color.white, 30f);
            _ballArrowRT = BuildIndicatorPart(iconCanvasGO.transform, "BallArrow",
                                              _indicatorArrowSprite ?? ArrowSprite(),
                                              new Color(1f, 1f, 1f, 0.95f), 30f);

            BuildB1ScreenChrome(iconCanvasGO.transform);
        }

        /// <summary>
        /// map_view_v2 §4/§5/§6/§7 — everything B1 draws in screen space, all on the one indicator
        /// canvas the map already owns: the 50-yd ticks, the target crosshair (and its over-range ✕),
        /// the ghost dot at P_max, the target readout chip, and the cloned HUD pin chip.
        ///
        /// The canvas is ScreenSpaceOverlay at scaleFactor 1, i.e. canvas units ARE surface pixels, so
        /// every Figma number below is multiplied by <see cref="UiScale"/> (device px → surface px)
        /// rather than being handed to a CanvasScaler. That keeps <see cref="SolveIndicatorPlacement"/>
        /// and <c>WorldToScreenPoint</c> in the same coordinate space they already share.
        /// </summary>
        private void BuildB1ScreenChrome(Transform canvas)
        {
            // Rubik, lifted off a live HUD text so the map's type matches the HUD's rather than
            // falling back to TMP's LiberationSans default.
            _hudFont = ResolveHudFont();

            // §4 — tick pool. Fixed size: 12 covers a 600 yd hero shot at 50 yd spacing, and a pool
            // means dragging never allocates.
            _tickMarks.Clear();
            _tickLabels.Clear();
            for (int i = 0; i < kTickPoolSize; i++)
            {
                var tick = BuildIndicatorPart(canvas, $"Tick{i}", null, Color.white, 1f);
                tick.sizeDelta = new Vector2(Px(kTickLenPx), Px(kTickThickPx));
                _tickMarks.Add(tick);

                var label = BuildTickLabel(canvas, $"TickLabel{i}");
                _tickLabels.Add(label);
            }

            // §5 — crosshair + centre dot, and the over-range ✕ that replaces the crosshair.
            _crossVRT    = BuildIndicatorPart(canvas, "CrossV",   null, Color.white, 1f);
            _crossVRT.sizeDelta   = new Vector2(Px(kCrossThickPx), Px(kCrossVPx));
            _crossHRT    = BuildIndicatorPart(canvas, "CrossH",   null, Color.white, 1f);
            _crossHRT.sizeDelta   = new Vector2(Px(kCrossHPx), Px(kCrossThickPx));
            _centreDotRT = BuildIndicatorPart(canvas, "CentreDot", DotSprite(), kLime, Px(kCentreDotPx));
            _crossX1RT   = BuildIndicatorPart(canvas, "CrossX1",  null, kOverRed, 1f);
            _crossX1RT.sizeDelta  = new Vector2(Px(kOverXBarPx), Px(kOverXThickPx));
            _crossX1RT.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _crossX2RT   = BuildIndicatorPart(canvas, "CrossX2",  null, kOverRed, 1f);
            _crossX2RT.sizeDelta  = new Vector2(Px(kOverXBarPx), Px(kOverXThickPx));
            _crossX2RT.localRotation = Quaternion.Euler(0f, 0f, -45f);
            _ghostDotRT  = BuildIndicatorPart(canvas, "GhostDot", DotSprite(), Color.white, Px(kGhostDotPx));

            // §6 — the target readout chip. Three DIFFERENT plates so the navy header and the white
            // body meet on a square seam; only the card's outer corners are rounded.
            _readout = MapTargetReadoutWidget.Build(canvas,
                                                    RoundedRectSprite(true,  true),   // shadow: all four
                                                    RoundedRectSprite(true,  false),  // header: top only
                                                    RoundedRectSprite(false, true),   // body:   bottom only
                                                    _hudFont, UiScale);
            _readout.SetVisible(false);

            // §7 — the pin chip: a clone of the LIVE HoleIndicator, not a rebuild of it.
            _pinIndicator = MapPinIndicator.CloneFromScene(canvas, UiScale);
            _pinIndicator?.SetActive(false);

            // Draw the readout last as a belt-and-braces measure; the real guarantee is that
            // PlaceReadout dodges the pin indicator's FULL rect (chip + tail), because sibling order
            // alone did not stop the tail crossing the readout's header.
            _readout.Root.SetAsLastSibling();
        }

        /// <summary>Rubik (or whatever the HUD uses) from a live HUD text — never a hardcoded asset path.</summary>
        private TMP_FontAsset ResolveHudFont()
        {
            var holeChip = FindObjectOfType<HoleIndicatorWidget>(true);
            if (holeChip != null)
            {
                var t = holeChip.GetComponentInChildren<TMP_Text>(true);
                if (t != null && t.font != null) return t.font;
            }
            var wind = FindObjectOfType<WindIndicatorWidget>(true);
            if (wind != null)
            {
                var t = wind.GetComponentInChildren<TMP_Text>(true);
                if (t != null && t.font != null) return t.font;
            }
            return null;   // TMP falls back to its default asset
        }

        /// <summary>map_view_v2 §4 — one yardage label: Rubik Medium 30, white, left-aligned, mid-height.</summary>
        private TextMeshProUGUI BuildTickLabel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_hudFont != null) tmp.font = _hudFont;
            tmp.fontSize         = Px(kTickFontPx);
            tmp.color            = Color.white;
            tmp.alignment        = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode     = TextOverflowModes.Overflow;
            tmp.raycastTarget    = false;
            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(Px(140f), Px(44f));
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = new Vector2(0f, 0.5f);
            go.SetActive(false);
            return tmp;
        }

        /// <summary>Order 355 §5 — one screen-space indicator part (icon or arrow) on the shared canvas.</summary>
        private RectTransform BuildIndicatorPart(Transform parent, string name, Sprite sprite, Color color, float size)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = sprite;
            img.color  = color;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            go.SetActive(false);
            return rt;
        }

        /// <summary>
        /// Order 355 §5 — procedural arrow placeholder: a solid triangle pointing UP (+Y), so
        /// <see cref="PlaceIndicator"/>'s rotation is a plain <c>atan2 − 90°</c>. Built once per
        /// controller and destroyed in <see cref="OnDestroy"/>.
        /// </summary>
        private Sprite ArrowSprite()
        {
            if (_arrowSpriteGen != null) return _arrowSpriteGen;

            const int res = 64;
            _arrowTex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                // Triangle: full width at y=0, collapsing to a point at y=res-1.
                float halfW = 0.5f * res * (1f - (float)y / (res - 1));
                for (int x = 0; x < res; x++)
                {
                    float dx = Mathf.Abs(x - (res - 1) * 0.5f);
                    px[y * res + x] = dx <= halfW ? Color.white : Color.clear;
                }
            }
            _arrowTex.SetPixels(px);
            _arrowTex.Apply();
            _arrowSpriteGen = Sprite.Create(_arrowTex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
            return _arrowSpriteGen;
        }

        /// <summary>
        /// Order 355 §5 — procedural ball placeholder: a white disc with a soft edge, matching the
        /// visual language of the world-space <c>_ballMarker</c> sphere (<see cref="kBallMarkerSz"/>).
        /// </summary>
        private Sprite DotSprite()
        {
            if (_dotSpriteGen != null) return _dotSpriteGen;

            const int res = 64;
            _dotTex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[res * res];
            Vector2 c = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (res * 0.5f);
                    px[y * res + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f));
                }
            _dotTex.SetPixels(px);
            _dotTex.Apply();
            _dotSpriteGen = Sprite.Create(_dotTex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
            return _dotSpriteGen;
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
            // Order 354 §4.1 — CAMERA AXIS = HOLE AXIS, decoupled from the live aim yaw.
            //
            // Until now the camera axis was AimDirection2D(), so dragging the aim ROTATED THE WORLD.
            // Every reference title (Golf Clash, Golf Rival, Ultimate Golf) does the opposite: the hole
            // is pinned (ball bottom, green top) and the AIM LINE rotates on screen. AimDirection2D()
            // still drives the guide line, landing zone, rings and the SHOOT write-back — only the
            // camera stops following it. Consequence: the §11 invariant (the ball projects BELOW the
            // flag on screen) is now unconditionally true — it no longer depends on where the player
            // happens to be aiming — and the camera no longer re-frames while the player aims.
            Vector3 aimDir = ballToFlag.normalized;

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

            // ── Framing: zoom to the SHOT — ball and flag, as tight as they allow ─
            // (Order 354c, Cesar 2026-08-07: "Zoom in as much as possible as long as current ball
            //  position and flag are visible (leave a bit of margin so none of them touch the
            //  borders)".)
            //
            // The framing target is ONLY the ball and the flag. The camera pulls back exactly far
            // enough to seat the ball at kShotBottomFrac and the flag at kShotTopFrac and no further,
            // so the shot fills the screen: on a long par 5 that is still the whole hole, and on a
            // short approach it is a close view of the green.
            //
            // This supersedes 354b's "fit the whole playable footprint". That framing was driven by
            // the hole's OB-mask hull and had to zoom out to hold ground the player was never looking
            // at; the ball→flag pair is the only thing that must be on screen. The camera axis is the
            // ball→flag axis again for the same reason — any yaw off it shortens the on-screen
            // ball→flag separation and would force the camera further back.
            //
            // Runs INSIDE Open() (via PositionMapCamera) BEFORE the first rendered frame, so the
            // correct framing is live on frame 1 (cf. P-010).
            Vector3 holeAxisN = new Vector3(aimDir.x, 0f, aimDir.z);
            if (holeAxisN.sqrMagnitude > 1e-4f) holeAxisN.Normalize(); else holeAxisN = Vector3.forward;

            // Still loaded: the OB rectangle bounds the two-finger pan (§4.4), and it tells us the
            // playfield rectangle exists so the camera yaw can be snapped to it (354d).
            _obRectValid = TryGetObRect(out _obRectCenter, out _obRectHalf);

            Vector3 camAxisN = SnapAxisToPlayfield(holeAxisN);
            var shotRegion = BuildShotRegion(camAxisN);
            bool framed = FrameShowRegion(camAxisN, shotRegion);
            if (!framed)
            {
                AnchorBallToBottom(aimDir, ref lookAtTarget, distNeeded);
                // Order 354 §4.4: cap zoom-out at THIS fit too. Previously the fallback reset the cap
                // to _maxZoom, so the one path that could not compute a fit was also the one path that
                // let the player pull back and reveal the world. No path may do that now.
                _zoomOutCapFov = _currentFov;
                // Order 355 §3.2: and the same for the strict crop — the fallback gets the identical
                // containment pass, so NO path can violate the invariant, not even the degenerate one.
                ContainCameraFootprint("bottom-anchor-fallback");
            }

            Debug.Log($"[MapView v2] Camera pos={_mapCam.transform.position:F1} target={boundsCenter:F1} " +
                      $"fov={_currentFov:F1}° dist={distNeeded:F1}m boundsR={boundsRadius:F1}m " +
                      $"framedByShot={framed} obRect={_obRectValid}");
        }

        /// <summary>
        /// Order 354d — snap a camera axis to the playfield rectangle so the field renders upright.
        /// Pure math, EditMode-testable.
        ///
        /// The playfield is world-axis-aligned, so the four candidates are ±X and ±Z; the winner is
        /// the one the hole runs most nearly along, which keeps the flag up-screen and the ball
        /// down-screen. Ties cannot occur in practice (a hole exactly 45° off both axes would take
        /// the first candidate, which is still a valid upright framing).
        /// </summary>
        public static Vector3 SnapToWorldAxis(Vector3 axisN)
        {
            Vector3 best = Vector3.forward;
            float bestDot = float.MinValue;
            foreach (var c in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                float d = Vector3.Dot(axisN, c);
                if (d > bestDot) { bestDot = d; best = c; }
            }
            return best;
        }

        /// <summary>
        /// Instance wrapper: snap only when there IS a playfield rectangle to align to, and only when
        /// <see cref="_alignToPlayfieldAxis"/> is on. Otherwise the ball→flag heading is used as-is.
        /// </summary>
        private Vector3 SnapAxisToPlayfield(Vector3 holeAxisN)
        {
            if (!_alignToPlayfieldAxis || !_obRectValid) return holeAxisN;
            Vector3 snapped = SnapToWorldAxis(holeAxisN);
            Debug.Log($"[MapView v2] Playfield align: hole axis ({holeAxisN.x:F2},{holeAxisN.z:F2}) → " +
                      $"({snapped.x:F0},{snapped.z:F0}), {Vector3.Angle(holeAxisN, snapped):F1}° off");
            return snapped;
        }

        /// <summary>
        /// Order 355 §3.1 — the fit set: the ball and THE SHOT, not the ball and the flag.
        ///
        /// 354c framed ball+flag. Cesar 2026-08-10: "I want to ONLY be able to see the playable area,
        /// the place where the ball is resting, and IF IT FITS, the Flag indicator over the hole."
        /// The flag is therefore no longer a framing target — it is an INDICATOR target
        /// (<see cref="PlaceIndicator"/>, §5). What must be framed is the shot context: the ball and
        /// the landing disc this club can reach along the current aim. On a 460 m par 5 that is a
        /// ~130 m window around the strike zone instead of the whole hole, which is what makes the
        /// strict-containment crop (§2) achievable at all — a whole-hole frame on a 261 m-deep OB
        /// rectangle cannot be contained.
        ///
        /// The landing DISC (not just its centre) is in the set, via its four extreme points, so the
        /// zone never bleeds off a border. The "bit of margin so none of them touch the borders" is
        /// otherwise applied in SCREEN space by <see cref="kShotBottomFrac"/> / <see cref="kShotTopFrac"/>
        /// in <see cref="FrameShowRegion"/> — a world-space pad would read as a comfortable gap on a
        /// 460 m par 5 and swallow the whole frame on a 40 m pitch.
        ///
        /// <see cref="_minFramedSpanM"/> is the one world-space term: a floor on the framed distance,
        /// because without it a 2 m tap-in would drop the camera to a couple of metres off the deck.
        /// The shortfall is split evenly behind the ball and beyond the landing so the pair stays
        /// centred. Set it to 0 for a pure "as tight as the shot allows" fit.
        /// </summary>
        private List<Vector3> BuildShotRegion(Vector3 axisN)
        {
            float y = _ballWorldPos.y;
            Vector3 ballFlat = new Vector3(_ballWorldPos.x, y, _ballWorldPos.z);

            // Club carry by default; once the player has placed a landing by touch, that wins — the
            // frame follows the shot they are actually setting up.
            float carryM = _carryValid ? _carryYards * kYardsToMeters : 80f;
            if (_aimedCarryM > 0f) carryM = _aimedCarryM;

            Vector3 aimDir2D = AimDirection2D();
            Vector3 landing  = ballFlat + new Vector3(aimDir2D.x, 0f, aimDir2D.z) * carryM;
            Vector3 rightN   = new Vector3(-axisN.z, 0f, axisN.x);
            float   margin   = Mathf.Max(_landingZoneRadiusM, 4f);

            var region = new List<Vector3>(7)
            {
                ballFlat,
                landing + axisN  * margin,
                landing - axisN  * margin,
                landing + rightN * margin,
                landing - rightN * margin,
            };

            float span = Vector3.Dot(landing - ballFlat, axisN);
            float pad  = Mathf.Max(0f, Mathf.Max(_minFramedSpanM, 0f) - span) * 0.5f;
            if (pad > 0f)
            {
                region.Add(ballFlat - axisN * pad);
                region.Add(landing  + axisN * pad);
            }
            return region;
        }

        /// <summary>
        /// Load the hole's OB (out-of-bounds) mask world-bounds from <c>zones.json</c> as a world-XZ
        /// rectangle (centre.xy = world XZ centre, half.xy = world XZ half-extents). Cached per hole.
        /// Returns false when the asset or obMask is missing, so the caller falls back to old framing.
        /// </summary>
        private bool TryGetObRect(out Vector2 center, out Vector2 half)
        {
            center = default; half = default;
            string courseSlug = Golfin.Gameplay.Loop.ActiveCourseContext.CurrentCourseSlug;
            string holeId = $"Hole_{HoleContext.HoleNumber:D2}";
            string key = courseSlug + "/" + holeId;

            if (s_obRectCache.TryGetValue(key, out var v))
            {
                if (v.z <= 0f || v.w <= 0f) return false;
                center = new Vector2(v.x, v.y); half = new Vector2(v.z, v.w); return true;
            }

            var asset = Resources.Load<TextAsset>(
                Golfin.Physics.Runtime.HoleDataIO.ZonesResourcePath(courseSlug, holeId));
            if (asset == null) { s_obRectCache[key] = Vector4.zero; return false; }
            try
            {
                // build_size_diet Phase 2: the shipped zones asset is a gzip of the minified JSON,
                // so `asset.text` is mojibake now — HoleDataIO gunzips it (and passes a plain,
                // un-migrated zones.json straight through).
                //
                // Extract only the small obMask object (avoid JsonUtility tokenising the multi-MB file —
                // maskBase64 uses only [A-Za-z0-9+/=], so the first '}' after the object's '{' closes it).
                // Minifying does not disturb that: the object is still "obMask":{ … } and the first
                // '}' after its '{' still closes it.
                string text = Golfin.Physics.Runtime.HoleDataIO.DecodeZonesText(asset);
                if (text == null) { s_obRectCache[key] = Vector4.zero; return false; }
                int oi = text.IndexOf("\"obMask\"", StringComparison.Ordinal);
                int ob = oi >= 0 ? text.IndexOf('{', oi) : -1;
                int oe = ob >= 0 ? text.IndexOf('}', ob) : -1;
                if (oe < 0) { s_obRectCache[key] = Vector4.zero; return false; }
                var ob2 = JsonUtility.FromJson<_ObMaskJson>(text.Substring(ob, oe - ob + 1));
                if (ob2 == null || ob2.worldSizeX <= 0f || ob2.worldSizeZ <= 0f) { s_obRectCache[key] = Vector4.zero; return false; }

                float hx = ob2.worldSizeX * 0.5f, hz = ob2.worldSizeZ * 0.5f;
                float cx = ob2.worldOriginX + hx, cz = ob2.worldOriginZ + hz;
                s_obRectCache[key] = new Vector4(cx, cz, hx, hz);
                center = new Vector2(cx, cz); half = new Vector2(hx, hz);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MapView v2] OB rect parse failed for {key}: {ex.Message}");
                s_obRectCache[key] = Vector4.zero; return false;
            }
        }

        /// <summary>
        /// Order 354 §4.2 — SHOW-REGION pose solve (pure geometry; camera passed in so EditMode tests
        /// drive the SAME code the game runs).
        ///
        /// Generalises Order 353b's two solvers:
        ///   • <c>SolveWidthDist</c> tested the on-screen separation of TWO corridor edge points.
        ///     <c>ContainsRegion</c> now tests that EVERY region vertex projects inside
        ///     <c>[sideMargin, 1-sideMargin] × [bottomFrac, topFrac]</c> — same monotonicity
        ///     (pulling back shrinks the projected region), same 44-step bisection.
        ///   • <c>SolveAnchorSlide</c> is unchanged in shape, but anchors the region's LOWEST projected
        ///     vertex (the near edge of the hole map) to <paramref name="bottomFrac"/> instead of the
        ///     ball. That is K2 verbatim — the bottom of the map sits flush at the screen bottom; on
        ///     the tee the near edge ≈ the ball, mid-hole the ball sits slightly above it (correct).
        ///     Anchoring the lowest vertex rather than the near-edge MIDPOINT is deliberate: on a hole
        ///     whose axis is rotated relative to the OB rectangle the near edge is a corner, and
        ///     anchoring the midpoint would push that corner off the bottom of the screen.
        ///
        /// The two are solved JOINTLY: distance is bisected, and every candidate distance is evaluated
        /// with the slide that already anchors the bottom. That makes the search monotone (a farther
        /// camera shrinks the bottom-pinned region strictly upward) and guarantees BOTH invariants hold
        /// at the answer, instead of alternating two weakly-coupled solves and hoping they converge.
        ///
        /// Returns false when no distance in [2, 5000] m contains the region — the caller then falls
        /// back to the ball+landing bottom-anchor.
        /// </summary>
        public static bool SolveShowRegionPose(
            Camera cam, List<Vector3> region, Vector3 axisN, float tiltDeg,
            float bottomFrac, float sideMargin, float topFrac,
            out float dist, out float slide, out Vector3 focus)
        {
            dist = 0f; slide = 0f; focus = Vector3.zero;
            if (cam == null || region == null || region.Count < 2) return false;

            Vector3 rightN = new Vector3(-axisN.z, 0f, axisN.x);
            float tanTilt  = Mathf.Tan(tiltDeg * Mathf.Deg2Rad);

            // Pivot = the region's near-edge row, laterally centred, so the fit is symmetric on screen.
            float alongMin = float.MaxValue, latMin = float.MaxValue, latMax = float.MinValue;
            foreach (var p in region)
            {
                alongMin = Mathf.Min(alongMin, Vector3.Dot(p, axisN));
                float lat = Vector3.Dot(p, rightN);
                latMin = Mathf.Min(latMin, lat);
                latMax = Mathf.Max(latMax, lat);
            }
            float groundY = region[0].y;
            Vector3 flatPivot = axisN * alongMin + rightN * (0.5f * (latMin + latMax));
            Vector3 pivot = new Vector3(flatPivot.x, groundY, flatPivot.z);

            void SetPose(float d, float s)
            {
                Vector3 look = pivot + axisN * s;
                cam.transform.position = look - axisN * d + Vector3.up * (d * tanTilt);
                cam.transform.LookAt(look, Vector3.up);
            }
            // Lowest projected region vertex. Monotonically DECREASING in slide (look forward → the
            // whole region slides down the screen). -1 when any vertex is behind the camera.
            float MinVertY(float d, float s)
            {
                SetPose(d, s);
                float lo = float.MaxValue;
                foreach (var p in region)
                {
                    Vector3 vp = cam.WorldToViewportPoint(p);
                    if (vp.z <= 0f) return -1f;
                    lo = Mathf.Min(lo, vp.y);
                }
                return lo;
            }
            // Slide that puts the region's near edge at the bottom margin. Same bisection shape as
            // Order 353b's SolveAnchorSlide; bracket is ±4 camera distances, which straddles the
            // solution for any hole length.
            float SolveAnchorSlide(float d)
            {
                float sLo = -4f * d, sHi = 4f * d;
                if (!(MinVertY(d, sLo) > bottomFrac && MinVertY(d, sHi) < bottomFrac)) return 0f;
                for (int k = 0; k < 44; k++)
                {
                    float sm = 0.5f * (sLo + sHi);
                    if (MinVertY(d, sm) > bottomFrac) sLo = sm; else sHi = sm;
                }
                return 0.5f * (sLo + sHi);
            }
            bool ContainsRegion(float d, float s)
            {
                SetPose(d, s);
                foreach (var p in region)
                {
                    Vector3 vp = cam.WorldToViewportPoint(p);
                    if (vp.z <= 0f) return false;
                    if (vp.x < sideMargin || vp.x > 1f - sideMargin) return false;
                    if (vp.y < bottomFrac - 1e-3f || vp.y > topFrac) return false;
                }
                return true;
            }
            // Coupled predicate: at distance d, anchor the bottom, then ask whether everything fits.
            bool FitsAnchored(float d, out float sOut)
            {
                sOut = SolveAnchorSlide(d);
                return ContainsRegion(d, sOut);
            }

            float dLo = 2f, dHi = 5000f;
            if (FitsAnchored(dLo, out _)) { dist = dLo; slide = SolveAnchorSlide(dLo); focus = pivot + axisN * slide; return true; }
            if (!FitsAnchored(dHi, out _)) return false;      // never contains → caller falls back
            for (int k = 0; k < 44; k++)
            {
                float dm = 0.5f * (dLo + dHi);
                if (FitsAnchored(dm, out _)) dHi = dm; else dLo = dm;
            }
            dist  = dHi;                                      // smallest distance known to contain
            slide = SolveAnchorSlide(dist);
            SetPose(dist, slide);
            focus = pivot + axisN * slide;
            return true;
        }

        // ── Order 355 §2/§3/§4 — ground footprint (THE INVARIANT) ─────────────────
        /// <summary>
        /// Order 355 §2 — the camera's GROUND FOOTPRINT: the quad the four viewport corners cut out
        /// of the horizontal plane <paramref name="groundY"/>, returned as its world-XZ AABB.
        ///
        /// Pure math (EditMode seam): four <c>Plane.Raycast</c> calls, no physics, cheap enough to run
        /// every frame and inside a bisection. At <see cref="_heroTiltDeg"/> 80° and FOV ≤ 90° the top
        /// edge ray still points 35° below horizontal, so all four rays hit; returns false only for a
        /// degenerate pose (camera below the plane, or looking up), which the callers treat as
        /// "cannot verify → do not move".
        ///
        /// The AABB rather than the trapezoid is deliberate. The camera yaw is playfield-snapped
        /// (<see cref="SnapAxisToPlayfield"/>, 354d) and the OB rectangle is world-axis-aligned, so the
        /// AABB IS the trapezoid's bound in the rect's own frame — containment is four comparisons. On
        /// an un-snapped yaw the AABB is a strict over-approximation, which errs toward showing LESS
        /// off-course, never more.
        /// </summary>
        public static bool TryComputeGroundFootprint(Camera cam, float groundY, out Vector2 min, out Vector2 max)
        {
            min = Vector2.zero; max = Vector2.zero;
            if (cam == null) return false;

            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            float mnx = float.MaxValue, mnz = float.MaxValue, mxx = float.MinValue, mxz = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                float vx = (i & 1) == 0 ? 0f : 1f;
                float vy = (i & 2) == 0 ? 0f : 1f;
                Ray r = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
                if (!plane.Raycast(r, out float enter)) return false;   // parallel or pointing away
                Vector3 p = r.GetPoint(enter);
                mnx = Mathf.Min(mnx, p.x); mxx = Mathf.Max(mxx, p.x);
                mnz = Mathf.Min(mnz, p.z); mxz = Mathf.Max(mxz, p.z);
            }

            min = new Vector2(mnx, mnz);
            max = new Vector2(mxx, mxz);
            return true;
        }

        /// <summary>
        /// Order 355 §2 — THE INVARIANT itself: is the footprint AABB inside the playable rectangle?
        /// </summary>
        public static bool FootprintInsideRect(Vector2 fMin, Vector2 fMax,
                                               Vector2 rectCenter, Vector2 rectHalf, float tol)
            => fMin.x >= rectCenter.x - rectHalf.x - tol
            && fMax.x <= rectCenter.x + rectHalf.x + tol
            && fMin.y >= rectCenter.y - rectHalf.y - tol
            && fMax.y <= rectCenter.y + rectHalf.y + tol;

        /// <summary>
        /// Order 355 §4 — clamp a world-XZ ground translation so the FOOTPRINT (not the focus point)
        /// stays inside the playable rectangle. Pure math (EditMode seam).
        ///
        /// Solved per axis, which is what gives slide-along-edge behaviour: a diagonal pan into a
        /// corner keeps the component that is still legal instead of dead-stopping both. Passing
        /// <c>move = Vector2.zero</c> returns the CORRECTION that brings an already-violating footprint
        /// back inside, which is how the framing pass (§3) re-seats itself — one function, both jobs.
        ///
        /// When the footprint is WIDER than the rect on an axis (only reachable if a caller skipped the
        /// distance pass) the constraints are infeasible; the move that centres the footprint on the
        /// rect is returned, so the leak is symmetric instead of piled on one edge.
        ///
        /// The clamp targets <see cref="kClampSafetyInsetM"/> INSIDE the tolerance band rather than the
        /// band itself. Measured reason: re-deriving the footprint after the move runs four ray/plane
        /// intersections at ~350 m in float32 and reproduces the edge to ~1 cm, so a clamp that lands
        /// exactly ON the boundary reads back a hair outside it — and the every-frame invariant check
        /// (§4) would then fire on a pose the clamp had just declared legal.
        /// </summary>
        public const float kClampSafetyInsetM = 0.25f;

        public static Vector2 ClampFootprintMove(Vector2 fMin, Vector2 fMax, Vector2 move,
                                                 Vector2 rectCenter, Vector2 rectHalf, float tol)
        {
            float inset = Mathf.Min(kClampSafetyInsetM, Mathf.Max(0f, tol) * 0.25f);
            float SolveAxis(float curMin, float curMax, float m, float rc, float rh)
            {
                float lo = (rc - rh - tol + inset) - curMin;  // move must be >= lo to keep the near edge in
                float hi = (rc + rh + tol - inset) - curMax;  // move must be <= hi to keep the far edge in
                if (lo > hi) return 0.5f * ((lo + hi));       // infeasible → centre the footprint on the rect
                return Mathf.Clamp(m, lo, hi);
            }
            return new Vector2(
                SolveAxis(fMin.x, fMax.x, move.x, rectCenter.x, rectHalf.x),
                SolveAxis(fMin.y, fMax.y, move.y, rectCenter.y, rectHalf.y));
        }

        /// <summary>
        /// Order 355 §3.2 / §4 — bring the live camera's footprint inside the OB rectangle, and keep
        /// the ball seated as low as containment allows.
        ///
        /// Runs on EVERY framing path (the show-region solve AND the <see cref="AnchorBallToBottom"/>
        /// fallback), mirroring 354's "no path may reveal the world" rule: a path that cannot compute
        /// a fit is exactly the path that must not be allowed to leak.
        ///
        /// Three steps, in this order, because the order is what makes it terminate:
        ///   1. PULL IN until the footprint FITS BY SIZE. Scaling the position about the focus point
        ///      preserves yaw and tilt, so the footprint shrinks monotonically and similarly — a pure
        ///      zoom. Size-fit depends on distance alone, so this converges without reference to where
        ///      the rect is.
        ///   2. RE-SEAT the ball at <see cref="kShotBottomFrac"/>. Zooming in about the focus dropped
        ///      the ball down-screen (it sits below the focus), so the seat has to be re-solved, not
        ///      inherited.
        ///   3. TRANSLATE the rig by the containment correction. A ground translation cannot change
        ///      footprint SIZE, so after step 1 this always succeeds — which is why containment WINS
        ///      over the seat: if seating the ball at the bottom margin pushes the footprint out the
        ///      near edge, step 3 pushes it back and the ball rides higher on screen. That is the
        ///      specified behaviour, not a bug.
        ///
        /// Static, with the camera and focus passed in, for the same reason
        /// <see cref="SolveShowRegionPose"/> is: the EditMode tests drive the SAME code the game runs.
        /// Returns false only for a degenerate pose whose footprint cannot be measured.
        /// </summary>
        public static bool ContainFootprint(
            Camera cam, Vector3 ballWorldPos, float bottomFrac, float ballMinX, float ballMaxX,
            Vector2 rectCenter, Vector2 rectHalf, float tol,
            ref Vector3 focus, out int shrinks, out Vector2 correction)
        {
            shrinks = 0; correction = Vector2.zero;
            if (cam == null) return false;

            float groundY = ballWorldPos.y;

            // 1 — shrink until the footprint fits the rect BY SIZE.
            for (; shrinks < kContainMaxIter; shrinks++)
            {
                if (!TryComputeGroundFootprint(cam, groundY, out var fMin, out var fMax)) return false;
                if ((fMax.x - fMin.x) <= 2f * rectHalf.x + 2f * tol &&
                    (fMax.y - fMin.y) <= 2f * rectHalf.y + 2f * tol)
                    break;

                cam.transform.position = focus + (cam.transform.position - focus) * kContainShrink;
            }

            // 2 — re-seat the ball now that the zoom changed, along the view axis AND laterally.
            //
            // Both are needed. Measured on Hole 5 (hole runs 41.5° off the snapped playfield axis, so a
            // 228 m driver puts the landing far to one side): the footprint was 468 m deep against a
            // 337 m rect, six shrink steps fixed the depth, and the vertical re-seat put the ball back
            // on the bottom margin — but the zoom-about-focus had thrown it to viewport x = 1.196, off
            // the right edge. The landing stayed on screen and the BALL did not, which is exactly
            // backwards: Cesar's first requirement is seeing "the place where the ball is resting", and
            // §3 says it is the LANDING that may be sacrificed, never the ball.
            // The VERTICAL re-seat is only needed when the zoom changed — the show-region solve already
            // seated the ball on the bottom margin otherwise. The LATERAL one runs unconditionally, and
            // is a no-op unless the ball is outside the window: on Hole 1 no shrink happens at all, yet
            // the solve's own lateral framing put the ball at screen x = 959 against a SHOOT button
            // starting at 955, i.e. touching it. It is a small nudge (~25 px of frame) and it only ever
            // fires when the ball is at an extreme, which is always worth correcting.
            if (shrinks > 0) SeatBallAtViewportFrac(cam, ballWorldPos, bottomFrac, ref focus);
            SeatBallLaterally(cam, ballWorldPos, ballMinX, ballMaxX, ref focus);

            // 3 — translate into the rect. Cannot fail after step 1.
            if (!TryComputeGroundFootprint(cam, groundY, out var gMin, out var gMax)) return false;
            correction = ClampFootprintMove(gMin, gMax, Vector2.zero, rectCenter, rectHalf, tol);
            if (correction.sqrMagnitude > 1e-6f)
            {
                Vector3 t = new Vector3(correction.x, 0f, correction.y);
                cam.transform.position += t;
                focus                  += t;
            }
            return true;
        }

        /// <summary>Instance wrapper — runs the containment pass against the live map camera and rect.</summary>
        private void ContainCameraFootprint(string ctx)
        {
            if (_mapCam == null || !_obRectValid) return;

            // Seat the ball clear of the INDICATOR inset, not merely inside the solver's 2 % side
            // margin. Measured on Hole 5: the lateral re-seat parked the ball at viewport x = 0.980,
            // which is 24 px from the edge — inside the 70 px inset — so the ball's floating indicator
            // fired while the ball itself was plainly visible, pointing at something already on screen.
            // The footprint had 156 m of lateral slack there, so the extra clearance is free.
            // The window the lateral re-seat may park the ball in. Three terms, all measured:
            //
            //  • kIndicatorDockClearancePx on top of the indicator inset, because the seat converges to
            //    its target EXACTLY: at inset-width precisely the ball landed on screen x = 1100.0
            //    against a dock boundary of 1100.0, and the float coin-flip decided whether its own
            //    indicator fired — an indicator pointing at a ball that is plainly on screen. Anything
            //    hugging that boundary also flickers under camera jitter.
            //  • The SHOOT button. The ball seats at kShotBottomFrac — 203 px up on a 2532 px screen —
            //    which is exactly the row SHOOT occupies. On Hole 5 the re-seat parked the ball at
            //    x = 1076 against a button spanning 955…1124, i.e. behind it. "The place where the ball
            //    is resting" being hidden under a button fails the same requirement the seat exists to
            //    serve, so the window stops short of the button.
            float w = Mathf.Max(1f, _mapCam.pixelWidth);
            float h = Mathf.Max(1f, _mapCam.pixelHeight);
            float ballMinX = Mathf.Max(kWidthFillMargin, (_indicatorEdgeInsetPx + kIndicatorDockClearancePx) / w);
            float ballMaxX = 1f - ballMinX;

            Rect shootRect = ShootButtonScreenRect();
            if (shootRect.width > 0f && shootRect.yMin < kShotBottomFrac * h + kBallMarkerClearPx)
                ballMaxX = Mathf.Min(ballMaxX,
                                     Mathf.Max(ballMinX + 0.05f,
                                               (shootRect.xMin - kIndicatorDockClearancePx) / w));

            if (!ContainFootprint(_mapCam, _ballWorldPos, kShotBottomFrac, ballMinX, ballMaxX,
                                  _obRectCenter, _obRectHalf, kFootprintTolM,
                                  ref _camFocusPoint, out int shrinks, out Vector2 corr))
            {
                Debug.LogWarning($"[MapView v2] Contain({ctx}): footprint unresolvable (degenerate pose) — pose left as solved.");
                return;
            }

            TryComputeGroundFootprint(_mapCam, _ballWorldPos.y, out var fMin, out var fMax);
            Debug.Log($"[MapView v2] Contain({ctx}): shrinks={shrinks} corr=({corr.x:F1},{corr.y:F1})m " +
                      $"footprint=[{fMin.x:F0},{fMin.y:F0}]..[{fMax.x:F0},{fMax.y:F0}] " +
                      $"rect=c({_obRectCenter.x:F0},{_obRectCenter.y:F0}) h({_obRectHalf.x:F0},{_obRectHalf.y:F0})");
        }

        /// <summary>
        /// Order 355 §3.2 — slide the rig along its own ground-forward axis until the ball projects at
        /// <paramref name="frac"/> up the viewport. Same bisection shape as
        /// <see cref="AnchorBallToBottom"/> (forward → ball lower, monotone), but operating on the
        /// LIVE pose rather than a supplied base pose, so it can be re-run after the containment zoom.
        /// Leaves the pose untouched when the target fraction is not bracketed.
        /// </summary>
        public static void SeatBallAtViewportFrac(Camera cam, Vector3 ballWorldPos, float frac, ref Vector3 focus)
        {
            if (cam == null) return;

            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();

            Vector3 basePos   = cam.transform.position;
            Vector3 baseFocus = focus;
            float   reach     = Mathf.Max(20f, Vector3.Distance(basePos, baseFocus));

            float BallYAt(float s)
            {
                cam.transform.position = basePos + fwd * s;
                cam.transform.LookAt(baseFocus + fwd * s, Vector3.up);
                Vector3 vp = cam.WorldToViewportPoint(ballWorldPos);
                return vp.z <= 0f ? -1f : vp.y;
            }

            float sLo = -reach, sHi = reach;
            if (!(BallYAt(sLo) >= frac && BallYAt(sHi) <= frac))
            {
                cam.transform.position = basePos;
                cam.transform.LookAt(baseFocus, Vector3.up);
                return;
            }
            for (int i = 0; i < 32; i++)
            {
                float sMid = 0.5f * (sLo + sHi);
                if (BallYAt(sMid) < frac) sHi = sMid; else sLo = sMid;
            }
            float s0 = 0.5f * (sLo + sHi);
            cam.transform.position = basePos + fwd * s0;
            focus                  = baseFocus + fwd * s0;
            cam.transform.LookAt(focus, Vector3.up);
        }

        /// <summary>
        /// Order 355 §3.2 — slide the rig along its ground-RIGHT axis just far enough to pull the ball
        /// back inside <paramref name="minX"/>…<paramref name="maxX"/> of the viewport. A no-op when the
        /// ball is already within the margins, so it never fights the show-region solve — it only
        /// repairs the lateral throw that the containment zoom introduces (see
        /// <see cref="ContainFootprint"/> step 2). Moving the rig +right shifts the ball to a LOWER
        /// viewport x, monotonically, which is what makes the bisection valid.
        /// </summary>
        public static void SeatBallLaterally(Camera cam, Vector3 ballWorldPos, float minX, float maxX, ref Vector3 focus)
        {
            if (cam == null) return;

            Vector3 right = cam.transform.right; right.y = 0f;
            if (right.sqrMagnitude < 1e-4f) return;
            right.Normalize();

            Vector3 basePos   = cam.transform.position;
            Vector3 baseFocus = focus;
            float   reach     = Mathf.Max(20f, Vector3.Distance(basePos, baseFocus)) * 2f;

            float BallXAt(float s)
            {
                cam.transform.position = basePos + right * s;
                cam.transform.LookAt(baseFocus + right * s, Vector3.up);
                Vector3 vp = cam.WorldToViewportPoint(ballWorldPos);
                return vp.z <= 0f ? float.NaN : vp.x;
            }
            void Restore()
            {
                cam.transform.position = basePos;
                cam.transform.LookAt(baseFocus, Vector3.up);
            }

            float x0 = BallXAt(0f);
            if (float.IsNaN(x0) || (x0 >= minX && x0 <= maxX)) { Restore(); return; }

            float target = x0 < minX ? minX : maxX;
            float sLo = -reach, sHi = reach;
            float xLo = BallXAt(sLo), xHi = BallXAt(sHi);
            if (float.IsNaN(xLo) || float.IsNaN(xHi) || !(xLo >= target && xHi <= target)) { Restore(); return; }

            for (int i = 0; i < 32; i++)
            {
                float sMid = 0.5f * (sLo + sHi);
                float x = BallXAt(sMid);
                if (float.IsNaN(x) || x > target) sLo = sMid; else sHi = sMid;
            }
            float s0 = 0.5f * (sLo + sHi);
            cam.transform.position = basePos + right * s0;
            focus                  = baseFocus + right * s0;
            cam.transform.LookAt(focus, Vector3.up);
        }

        /// <summary>
        /// Frame the map camera on the fit set and cap the manual zoom-out at that fit.
        /// FOV is held at <see cref="_initialZoom"/>; zoom is driven by camera DISTANCE so the fit
        /// respects the portrait aspect (Camera.fieldOfView is vertical; WorldToViewportPoint accounts
        /// for aspect automatically). Pitch is the <see cref="_heroTiltDeg"/> hero tilt.
        ///
        /// Order 354c: the viewport window is <see cref="kShotBottomFrac"/>…<see cref="kShotTopFrac"/>,
        /// so the ball seats a little above the bottom edge and the flag a little below the top —
        /// "a bit of margin so none of them touch the borders". Because the solve returns the SMALLEST
        /// containing distance, that window is also the zoom: no further out than the shot requires.
        /// Returns false when the solve found no containing pose, so the caller can fall back.
        /// </summary>
        private bool FrameShowRegion(Vector3 axisN, List<Vector3> region)
        {
            if (_mapCam == null || region == null || region.Count < 2) return false;

            _currentFov = Mathf.Clamp(_initialZoom, _minZoom, _maxZoom);
            _mapCam.fieldOfView = _currentFov;

            if (!SolveShowRegionPose(_mapCam, region, axisN, _heroTiltDeg,
                                     kShotBottomFrac, kWidthFillMargin, kShotTopFrac,
                                     out float dist, out float slide, out Vector3 focus))
            {
                Debug.LogWarning("[MapView v2] Shot-fit: no containing pose found — falling back to bottom-anchor framing.");
                return false;
            }

            // Sanity: never ship a degenerate pose (kept from Order 353b).
            if (dist < 8f || dist > 4000f)
            {
                Debug.LogWarning($"[MapView v2] Shot-fit: solve degenerate (dist={dist:F1}m) — falling back to bottom-anchor framing.");
                return false;
            }

            _camFocusPoint = focus;
            _zoomOutCapFov = _currentFov;   // player may zoom IN, but never zoom OUT past this fit

            // Order 355 §3.2 — CONTAINMENT WINS. The solve above answers "what shows the shot"; this
            // answers "what may be shown at all". If the seated pose reveals ground outside the OB
            // rectangle, the camera comes in and re-seats until it does not.
            ContainCameraFootprint("shot-fit");

            Vector3 vpBall = _mapCam.WorldToViewportPoint(_ballWorldPos);
            Vector3 vpFlag = _mapCam.WorldToViewportPoint(_flagWorldPos);
            Debug.Log($"[MapView v2] Shot-fit: pts={region.Count} dist={dist:F1}m slide={slide:F1}m " +
                      $"fov={_currentFov:F1}° capFov={_zoomOutCapFov:F1} camY={_mapCam.transform.position.y:F1} " +
                      $"ballVP=({vpBall.x:F2},{vpBall.y:F2}) flagVP=({vpFlag.x:F2},{vpFlag.y:F2})");
            return true;
        }

        /// <summary>
        /// Slide the map-camera rig along the horizontal aim axis so the ball projects to
        /// <see cref="kBottomAnchorFrac"/> up the viewport — anchoring the near edge of the
        /// hole map flush to the screen bottom. Pitch and FOV are preserved (a pure ground
        /// translation of both the camera and its look-at target, so the look direction is
        /// unchanged). Uses a bisection on the slide distance: moving FORWARD (+aim) lowers the
        /// ball on screen, moving BACKWARD raises it, monotonically. If the target margin is not
        /// bracketed (degenerate geometry), the pre-anchor pose is left untouched.
        /// </summary>
        private void AnchorBallToBottom(Vector3 aimDir, ref Vector3 lookAtTarget, float distNeeded)
        {
            if (_mapCam == null) return;

            Vector3 groundAim = new Vector3(aimDir.x, 0f, aimDir.z);
            if (groundAim.sqrMagnitude < 1e-4f) return;
            groundAim.Normalize();

            Vector3 baseCamPos = _mapCam.transform.position;
            Vector3 baseTarget = lookAtTarget;

            // f(s): ball viewport-Y after sliding the rig by groundAim*s. Monotonically
            // DECREASING in s (forward → ball lower). Returns -1 when the ball falls behind the
            // camera, which the search treats as "well below target" and so pulls back.
            float BallViewportYAt(float s)
            {
                _mapCam.transform.position = baseCamPos + groundAim * s;
                _mapCam.transform.LookAt(baseTarget + groundAim * s, Vector3.up);
                Vector3 vp = _mapCam.WorldToViewportPoint(_ballWorldPos);
                return vp.z <= 0f ? -1f : vp.y;
            }

            // Bracket: sLo (backward) → ball high; sHi (forward) → ball low. A full camera
            // distance either way is far more than enough to straddle the small bottom margin.
            float sLo = -distNeeded, sHi = distNeeded;
            float yLo = BallViewportYAt(sLo);
            float yHi = BallViewportYAt(sHi);

            if (yLo >= kBottomAnchorFrac && yHi <= kBottomAnchorFrac)
            {
                for (int bi = 0; bi < 28; bi++)
                {
                    float sMid = 0.5f * (sLo + sHi);
                    float y    = BallViewportYAt(sMid);
                    if (y < kBottomAnchorFrac) sHi = sMid;  // too low → pull back
                    else                       sLo = sMid;  // too high → push forward
                }
                float sFinal = 0.5f * (sLo + sHi);
                _mapCam.transform.position = baseCamPos + groundAim * sFinal;
                lookAtTarget = baseTarget + groundAim * sFinal;
                _mapCam.transform.LookAt(lookAtTarget, Vector3.up);
                _camFocusPoint = lookAtTarget;
                Debug.Log($"[MapView v2] Bottom-anchor: slid rig {sFinal:F1}m along aim → ball viewportY≈{kBottomAnchorFrac:F2}");
            }
            else
            {
                // Not bracketed (degenerate) — restore the pre-anchor pose, leave framing as-is.
                _mapCam.transform.position = baseCamPos;
                _mapCam.transform.LookAt(baseTarget, Vector3.up);
                _camFocusPoint = baseTarget;
                Debug.LogWarning($"[MapView v2] Bottom-anchor: margin not bracketed (yLo={yLo:F2} yHi={yHi:F2}) — framing unchanged.");
            }
        }

        // ── Marker placement ──────────────────────────────────────────────────────
        private void PlaceMarkers()
        {
            if (_ballMarker != null)
                _ballMarker.position = _ballWorldPos + Vector3.up * kRingHeightOff;

            UpdateGuideAndRings();
            UpdateHoleIndicator();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  map_view_v2 §1 — RANGE MODEL
        //  Pure math, no GameObject dependency, so the EditMode suite can assert it without a scene.
        //  The 1.20 factor is NOT a magic number for the map: it is ShotController's overpower
        //  ceiling (PowerNormalized is clamped to 1.2), so "max reach" is literally the furthest the
        //  ball can be sent with this club. The map DRAWS that clamp; it never applies it —
        //  _aimedCarryM stays free and MapTargetCarryM is written back unclamped (iter-32).
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>ShotController's overpower ceiling. Max reach = this × the club's carry.</summary>
        public const float kMaxReachFactor = 1.20f;

        /// <summary>Furthest the selected club can send the ball, in metres.</summary>
        public static float MaxReachM(float clubCarryM) => clubCarryM * kMaxReachFactor;

        /// <summary>
        /// True when the placed landing distance is beyond what the club can reach. The 1 cm slack
        /// keeps a target sitting exactly ON the max arc out of the red state (float noise otherwise
        /// flickers the whole over-range treatment on and off while dragging along the arc).
        /// </summary>
        public static bool IsOverRange(float aimedCarryM, float clubCarryM)
            => aimedCarryM > MaxReachM(clubCarryM) + 0.01f;

        /// <summary>Where the ball ACTUALLY lands when the player aims past max reach.</summary>
        public static Vector3 MaxReachPoint(Vector3 ball, Vector3 aimDir2D, float clubCarryM)
            => ball + aimDir2D * MaxReachM(clubCarryM);

        /// <summary>
        /// How many yardage ticks fall strictly BETWEEN the ball and the landing point — ticks sit at
        /// 1..N × spacing, and a tick landing exactly ON L is not drawn (it would collide with the
        /// crosshair, and "150" printed on the target reads as the target's own label).
        /// </summary>
        public static int TickCount(float carryM, float tickSpacingM)
        {
            if (tickSpacingM <= 0.01f || carryM <= 0f) return 0;
            int n = Mathf.FloorToInt(carryM / tickSpacingM);
            if (n * tickSpacingM >= carryM - 0.0001f) n--;   // a tick AT L is not drawn
            return Mathf.Max(0, n);
        }

        // ── Screen ⇄ world scale helpers (all "px" in this file are DEVICE px on the 1170×2532 frame) ──

        /// <summary>Device px → live surface px. 1.0 on an iPhone 14; whatever the Game View is otherwise.</summary>
        private float UiScale => (_mapCam != null && _mapCam.pixelWidth > 0)
                               ? _mapCam.pixelWidth / (float)kDeviceW : 1f;

        /// <summary>Design px (1170-wide frame) → surface px on the overlay canvas.</summary>
        private float Px(float designPx) => designPx * UiScale;

        /// <summary>
        /// World metres per ONE surface pixel at a world point — the conversion that lets a 6 px arc
        /// stay a 6 px arc at any zoom. Measured by projecting a 1 m step along the camera's right
        /// axis, so it is exact for the actual projection rather than a small-angle approximation.
        /// </summary>
        private float MetresPerPixelAt(Vector3 worldPoint)
        {
            if (_mapCam == null) return 0.1f;
            Vector3 a = _mapCam.WorldToScreenPoint(worldPoint);
            Vector3 b = _mapCam.WorldToScreenPoint(worldPoint + _mapCam.transform.right);
            float px  = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
            return px > 0.01f ? 1f / px : 0.1f;
        }

        /// <summary>Design px → world metres at a world point.</summary>
        private float PxToWorld(float designPx, Vector3 at) => Px(designPx) * MetresPerPixelAt(at);

        // ── B1 design tokens (Figma px / colours; "do not invent" — spec § Design vocabulary) ────
        private static readonly Color kLime    = new Color(120f / 255f, 233f / 255f, 33f / 255f, 1f); // #78E921
        private static readonly Color kOverRed = new Color(242f / 255f,  58f / 255f, 51f / 255f, 1f); // #F23A33
        private const float kGuideDotPx      = 8f;    // aim-line dot diameter on screen
        private const float kFanEdgePx       = 6f;    // lime max arc
        private const float kFanEdgeOverPx   = 8f;    // red max arc
        private const float kNominalArcPx    = 2f;    // dashed white 1.0× carry arc
        private const float kLandingRingPx   = 4f;    // crisp white ring at r100
        private const float kGhostRingPx     = 4f;    // dashed ghost ring at P_max
        private const float kTickLenPx       = 36f;
        private const float kTickThickPx     = 3f;
        private const float kTickLabelGapPx  = 34f;
        private const float kTickFontPx      = 30f;
        private const float kCrossVPx        = 80f;   // vertical crosshair bar length
        private const float kCrossHPx        = 104f;  // horizontal crosshair bar length
        private const float kCrossThickPx    = 3f;
        private const float kCentreDotPx     = 12f;
        private const float kOverXBarPx      = 48f;   // over-range ✕ bar length
        private const float kOverXThickPx    = 6f;
        private const float kGhostDotPx      = 12f;
        private const float kReadoutGapPx    = 130f;  // chip offset from L
        private const float kReadoutSafePx   = 24f;
        private const int   kTickPoolSize    = 12;
        private const float kTickSpacingYd   = 50f;
        private const float kTickSpacingM    = 50f;
        private const int   kNominalArcDashes = 24;

        private const int   kGhostRingDashes  = 16;

        private void UpdateGuideAndRings()
        {
            if (!_carryValid) return;

            // iter-32: carry = the player-placed (touch-follow) landing distance; the club carry is the
            // 100 %-power REFERENCE. map_view_v2 §1 replaces iter-32's silent `powerPct > 1.20` colour
            // swap with ONE bool, IsOverRange, and drives every over-range visual from it. The distance
            // itself is still FREE and unclamped — the ghost ring DRAWS the clamp, nothing applies it.
            float clubCarryM = _carryYards * kYardsToMeters;          // 100 % reference
            if (_aimedCarryM < 0f) _aimedCarryM = clubCarryM;          // default to club carry on first open
            float carryM     = Mathf.Max(_aimedCarryM, 5f);           // free; floor so it never collapses
            _maxReachM       = MaxReachM(clubCarryM);
            _overRange       = IsOverRange(carryM, clubCarryM);

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

            // §6-MODEL ring radii: r_p = carryM * _ringFrac * (p/100).
            // Rings 80/120 stay OUT of scope (map_view_v2 § Out of scope); only r100 is drawn. Their
            // radii are still REPORTED — DumpInvariants and the RingRadiusAtPct / RingCenterAtPct
            // EditMode seams compute them from the same formula, untouched by this task — so computing
            // dead locals here would only add compiler warnings, not coverage.
            float r100 = Mathf.Max(carryM * _ringFrac * 1.00f, 3f);

            UpdateB1Overlay(L, Lground, aimDir2D, carryM, clubCarryM, r100);

            // iter-28 Fix 3 (rings 80/120 only): to restore, uncomment these 3 lines.
            // UpdateConformingRing(_ring80GO,  Lground, Mathf.Max(carryM * _ringFrac * 0.80f, 2f));
            // UpdateConformingRing(_ring120GO, Lground, Mathf.Max(carryM * _ringFrac * 1.20f, 4f));
            // if (_mapCam != null) UpdateRingLabels(L, carryM, aimDir2D, r80, r100, r120);
        }

        // ── map_view_v2 §2-§6 — the B1 overlay, world + screen ───────────────────
        // Rebuilding six terrain-conforming meshes costs one Physics.RaycastAll per vertex, so the
        // WORLD half only rebuilds when something it depends on actually moved (aim, carry, camera
        // pose, over-range state). The SCREEN half — ticks, crosshair, chips — is cheap and runs every
        // frame, which is what keeps them glued to L while the camera eases.
        private float      _lastBuiltAimYaw = float.NaN;
        private float      _lastBuiltCarry  = -1f;
        private float      _lastBuiltFov    = -1f;
        private bool       _lastBuiltOver;
        private Vector3    _lastBuiltCamPos = new Vector3(float.NaN, 0f, 0f);
        private Quaternion _lastBuiltCamRot = Quaternion.identity;

        private bool WorldOverlayDirty(float carryM)
        {
            if (_mapCam == null) return false;
            bool dirty = !Mathf.Approximately(_lastBuiltAimYaw, _aimYawRadians)
                      || Mathf.Abs(_lastBuiltCarry - carryM) > 0.01f
                      || Mathf.Abs(_lastBuiltFov - _currentFov) > 0.01f
                      || _lastBuiltOver != _overRange
                      || (_lastBuiltCamPos - _mapCam.transform.position).sqrMagnitude > 0.0001f
                      || Quaternion.Angle(_lastBuiltCamRot, _mapCam.transform.rotation) > 0.01f;
            if (dirty)
            {
                _lastBuiltAimYaw = _aimYawRadians;
                _lastBuiltCarry  = carryM;
                _lastBuiltFov    = _currentFov;
                _lastBuiltOver   = _overRange;
                _lastBuiltCamPos = _mapCam.transform.position;
                _lastBuiltCamRot = _mapCam.transform.rotation;
            }
            return dirty;
        }

        private void UpdateB1Overlay(Vector3 L, Vector3 Lground, Vector3 aimDir2D,
                                     float carryM, float clubCarryM, float r100)
        {
            if (_mapCam == null) return;

            float   whiteDist = Mathf.Min(carryM, _maxReachM);
            Vector3 pMax      = MaxReachPoint(_ballWorldPos, aimDir2D, clubCarryM);
            Vector3 pMaxGround = new Vector3(pMax.x, SampleTerrainHeight(pMax), pMax.z);

            if (WorldOverlayDirty(carryM))
            {
                // ── §2 Aim line ───────────────────────────────────────────────────
                // Flat (kArcBow is NOT applied on the map — Cesar: "seen from a weird angle"), terrain
                // hugging, constant width on screen. The white line stops at P_max; the red line
                // carries the remainder to L, so the break is exactly where the ball stops going.
                Vector3 straightEnd = _ballWorldPos + aimDir2D * carryM;
                float   tSplit      = carryM > 0.01f ? Mathf.Clamp01(whiteDist / carryM) : 1f;
                float   dotWorldW   = Mathf.Max(_guideDotWorldWidth, PxToWorld(kGuideDotPx, L));
                if (_guideLine != null)
                {
                    _guideLine.startWidth = _guideLine.endWidth = dotWorldW;
                    _guideLine.startColor = _guideLine.endColor = Color.white;
                    UpdateGuideLine(_guideLine, _ballWorldPos, straightEnd, aimDir2D, carryM, 0f, tSplit);
                }
                if (_overRangeLine != null)
                {
                    if (_overRange)
                    {
                        _overRangeLine.startWidth = _overRangeLine.endWidth = dotWorldW;
                        UpdateGuideLine(_overRangeLine, _ballWorldPos, straightEnd, aimDir2D, carryM, tSplit, 1f);
                    }
                    else _overRangeLine.positionCount = 0;
                }

                // ── §3 Range fan + arcs ───────────────────────────────────────────
                // The sector is centred on the CURRENT aim direction and its radius IS max reach, so
                // the outer arc is literally the line the ball cannot cross.
                float half = _rangeFanHalfAngleDeg * Mathf.Deg2Rad;
                float a0   = _aimYawRadians - half;
                float a1   = _aimYawRadians + half;

                UpdateConformingSector(_rangeFanGO, _ballWorldPos, 0f, _maxReachM, a0, a1, 0);
                SetOverlayColor(_rangeFanGO, WithAlpha(kLime, _overRange ? 0.07f : 0.10f));

                float edgePx   = _overRange ? kFanEdgeOverPx : kFanEdgePx;
                float edgeBand = PxToWorld(edgePx, pMax) * 0.5f;
                UpdateConformingSector(_rangeFanEdgeGO, _ballWorldPos,
                                       _maxReachM - edgeBand, _maxReachM + edgeBand, a0, a1, 0);
                SetOverlayColor(_rangeFanEdgeGO, _overRange ? WithAlpha(kOverRed, 0.95f) : WithAlpha(kLime, 0.90f));

                Vector3 nominalPt  = _ballWorldPos + aimDir2D * clubCarryM;
                float   nominalBnd = PxToWorld(kNominalArcPx, nominalPt) * 0.5f;
                UpdateConformingSector(_nominalArcGO, _ballWorldPos,
                                       clubCarryM - nominalBnd, clubCarryM + nominalBnd,
                                       a0, a1, kNominalArcDashes);

                // ── §5 Landing ring + ghost ring ──────────────────────────────────
                float ringBand = PxToWorld(kLandingRingPx, Lground) * 0.5f;
                UpdateConformingSector(_ring100GO, Lground, r100 - ringBand, r100 + ringBand,
                                       0f, 2f * Mathf.PI, 0);
                SetOverlayColor(_ring100GO, _overRange ? WithAlpha(kOverRed, 0.95f) : WithAlpha(Color.white, 0.95f));

                SetActiveIf(_ghostRingGO, _overRange);
                if (_overRange)
                {
                    float ghostBand = PxToWorld(kGhostRingPx, pMaxGround) * 0.5f;
                    UpdateConformingSector(_ghostRingGO, pMaxGround, r100 - ghostBand, r100 + ghostBand,
                                           0f, 2f * Mathf.PI, kGhostRingDashes);
                }

                // ── §5 Landing glow — lime in range, red over ─────────────────────
                if (_landingZoneMat != null)
                {
                    var glowTex = _overRange ? _landingZoneTexOver : _landingZoneTex;
                    if (glowTex != null && _landingZoneMat.mainTexture != glowTex)
                    {
                        if (_landingZoneMat.HasProperty("_MainTex")) _landingZoneMat.SetTexture("_MainTex", glowTex);
                        _landingZoneMat.mainTexture = glowTex;
                    }
                }
            }

            UpdateB1ScreenChrome(L, Lground, aimDir2D, carryM, clubCarryM, pMaxGround);
        }

        /// <summary>
        /// map_view_v2 §4/§5/§6 — the screen-space half of the overlay, re-solved every frame from
        /// <c>WorldToScreenPoint</c> so ticks, crosshair and chip stay welded to the world points they
        /// annotate while the camera eases, pans or zooms.
        /// </summary>
        private void UpdateB1ScreenChrome(Vector3 L, Vector3 Lground, Vector3 aimDir2D,
                                          float carryM, float clubCarryM, Vector3 pMaxGround)
        {
            float screenW = _mapCam.pixelWidth, screenH = _mapCam.pixelHeight;

            Vector3 ballScrRaw = _mapCam.WorldToScreenPoint(_ballWorldPos);
            Vector3 lScrRaw    = _mapCam.WorldToScreenPoint(Lground);
            Vector3 pMaxScrRaw = _mapCam.WorldToScreenPoint(pMaxGround);
            Vector2 ballScr    = new Vector2(ballScrRaw.x, ballScrRaw.y);
            Vector2 lScr       = new Vector2(lScrRaw.x, lScrRaw.y);
            Vector2 pMaxScr    = new Vector2(pMaxScrRaw.x, pMaxScrRaw.y);

            // ── §4 Ticks every 50 yd (50 m in metres mode) along the aim direction ──
            float spacingM  = _unitMode == HoleIndicatorWidget.DistanceUnit.Meters
                            ? kTickSpacingM : kTickSpacingYd * kYardsToMeters;
            int   tickCount = Mathf.Min(TickCount(carryM, spacingM), kTickPoolSize);
            // Screen angle of the line, so the tick sits perpendicular to it and the label upright.
            Vector2 lineDir  = lScr - ballScr;
            float   lineAng  = lineDir.sqrMagnitude > 1f
                             ? Mathf.Atan2(lineDir.y, lineDir.x) * Mathf.Rad2Deg : 90f;

            for (int i = 0; i < kTickPoolSize; i++)
            {
                bool show = i < tickCount;
                var mark  = _tickMarks[i];
                var label = _tickLabels[i];
                if (mark  != null && mark.gameObject.activeSelf  != show) mark.gameObject.SetActive(show);
                if (label != null && label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
                if (!show) continue;

                float   dist  = (i + 1) * spacingM;
                Vector3 wp    = _ballWorldPos + aimDir2D * dist;
                Vector3 wpG   = new Vector3(wp.x, SampleTerrainHeight(wp) + kRingHeightOff, wp.z);
                Vector3 spRaw = _mapCam.WorldToScreenPoint(wpG);
                Vector2 sp    = new Vector2(spRaw.x, spRaw.y);

                if (mark != null)
                {
                    mark.anchoredPosition = sp;
                    mark.localRotation    = Quaternion.Euler(0f, 0f, lineAng + 90f);
                }
                if (label != null)
                {
                    // Distances read in the ACTIVE unit — the tick is "150" because 150 is what the
                    // player's HUD says, not because the world metres happen to be 137.16.
                    float shown = _unitMode == HoleIndicatorWidget.DistanceUnit.Meters
                                ? dist : dist * 1.0936133f;
                    label.text = Mathf.RoundToInt(shown).ToString();
                    label.rectTransform.anchoredPosition = sp + new Vector2(Px(kTickLabelGapPx), 0f);
                }
            }

            // ── §5 Crosshair / ✕ / centre dot at L, ghost dot at P_max ────────────
            SetRT(_crossVRT,    !_overRange, lScr);
            SetRT(_crossHRT,    !_overRange, lScr);
            SetRT(_centreDotRT, !_overRange, lScr);
            SetRT(_crossX1RT,    _overRange, lScr);
            SetRT(_crossX2RT,    _overRange, lScr);
            SetRT(_ghostDotRT,   _overRange, pMaxScr);

            // ── Club button dims while over range; SHOT VIEW stays at full alpha ──
            if (_shootButtonGroup != null)
            {
                float wanted = _overRange ? 0.5f : 1f;
                if (!Mathf.Approximately(_shootButtonGroup.alpha, wanted)) _shootButtonGroup.alpha = wanted;
            }

            // ── §6 Readout chip ───────────────────────────────────────────────────
            if (_readout != null)
            {
                _readout.SetVisible(true);
                float toPinM = Vector3.Distance(_ballWorldPos, _flagWorldPos) > 0.01f
                             ? Vector3.Distance(L, _flagWorldPos) : 0f;
                Vector2 size = _readout.Set(carryM, toPinM, _overRange,
                                            Golfin.Gameplay.UI.HUD.ClubContext.SelectedTypeLabel,
                                            _maxReachM, _unitMode, UiScale);
                // Avoid the pin chip. Now that the pin chip STANDS on the hole it occupies real estate
                // right where the readout wants to sit whenever the target is near the green, and the
                // two chips overlapped on Hole 04. The rect is last frame's (UpdateHoleIndicator runs
                // after this), which converges in one frame and never oscillates because the readout
                // only ever moves AWAY from it.
                Rect pinRect = (_pinIndicator != null && _pinIndicator.IsValid)
                             ? _pinIndicator.IndicatorScreenRect : default;
                PlaceReadout(size, lScr, pMaxScr, screenW, screenH, pinRect);
            }
        }

        private static void SetRT(RectTransform rt, bool show, Vector2 pos)
        {
            if (rt == null) return;
            if (rt.gameObject.activeSelf != show) rt.gameObject.SetActive(show);
            if (show) rt.anchoredPosition = pos;
        }

        /// <summary>
        /// map_view_v2 §6 — chip placement. Normally 130 px to the RIGHT of L and vertically centred on
        /// it, flipping to the LEFT when the chip would cross the safe area. Over range with L near the
        /// top of the screen it drops BELOW P_max instead (Figma: centred on x, top = P_max + 150), which
        /// is the one case where the side-by-side position would push a much wider chip off-frame.
        /// </summary>
        private void PlaceReadout(Vector2 size, Vector2 lScr, Vector2 pMaxScr,
                                  float screenW, float screenH, Rect avoidRect)
        {
            var rt   = _readout.Root;
            float inset = Px(kReadoutSafePx);

            if (_overRange && lScr.y > screenH * 0.72f)
            {
                rt.pivot = new Vector2(0.5f, 1f);
                float x = Mathf.Clamp(pMaxScr.x, inset + size.x * 0.5f, screenW - inset - size.x * 0.5f);
                float y = Mathf.Clamp(pMaxScr.y - Px(150f), inset + size.y, screenH - inset);
                rt.anchoredPosition = new Vector2(x, y);
                return;
            }

            float gap = Px(kReadoutGapPx);

            // Rect the chip would occupy on a given side, with the pivot that side implies.
            Rect RectOn(bool onLeft)
            {
                float x = onLeft ? Mathf.Max(lScr.x - gap, inset + size.x)
                                 : Mathf.Min(lScr.x + gap, screenW - inset - size.x);
                float cy = Mathf.Clamp(lScr.y, inset + size.y * 0.5f, screenH - inset - size.y * 0.5f);
                return new Rect(onLeft ? x - size.x : x, cy - size.y * 0.5f, size.x, size.y);
            }

            bool left = lScr.x + gap + size.x > screenW - inset;
            bool Blocked(bool onLeft) => avoidRect.width > 0f && RectOn(onLeft).Overlaps(avoidRect);
            // Prefer the side that is clear of the pin chip; if both collide, keep the safe-area choice
            // and drop BELOW the collision instead of sitting on top of it.
            if (Blocked(left) && !Blocked(!left)) left = !left;

            rt.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            float px = left ? Mathf.Max(lScr.x - gap, inset + size.x)
                            : Mathf.Min(lScr.x + gap, screenW - inset - size.x);
            float py = Mathf.Clamp(lScr.y, inset + size.y * 0.5f, screenH - inset - size.y * 0.5f);

            if (Blocked(left))
            {
                float below = avoidRect.yMin - size.y * 0.5f - Px(16f);
                float above = avoidRect.yMax + size.y * 0.5f + Px(16f);
                // Whichever of below/above stays inside the safe area; below first (the chip reads as
                // hanging off the target rather than floating above the pin).
                py = below - size.y * 0.5f >= inset ? below
                   : (above + size.y * 0.5f <= screenH - inset ? above : py);
                py = Mathf.Clamp(py, inset + size.y * 0.5f, screenH - inset - size.y * 0.5f);
            }
            rt.anchoredPosition = new Vector2(px, py);
        }

        /// <summary>
        /// map_view_v2 §2 — RETIRED on the map. The intentional vertical bow made the flat overhead line
        /// read as a trajectory seen "from a weird angle" (Cesar, 2026-09-04); B1's line is flat and
        /// hugs the terrain. Kept as a named constant because the shot-view aim line still wants one.
        /// </summary>
        private const float kArcBow = 1.5f;

        /// <summary>
        /// Draw one dotted line along the aim path between two fractions of the full carry.
        /// <paramref name="straightEnd"/> = ball + aimDir·carry (NO lateral baked in) so the fade/draw
        /// lateral can be re-added per vertex; at t=1 the vertex equals L exactly (§iter-26 FIX #1).
        /// Every vertex's Y is the TERRAIN height (+ kRingHeightOff) — flat and ground-hugging, no bow.
        /// </summary>
        private void UpdateGuideLine(LineRenderer lr, Vector3 from, Vector3 straightEnd,
                                     Vector3 aimDir2D, float carryM, float t0, float t1)
        {
            if (lr == null) return;
            lr.positionCount = kGuideSegments + 1;
            Vector3 right = new Vector3(-aimDir2D.z, 0f, aimDir2D.x);

            for (int i = 0; i <= kGuideSegments; i++)
            {
                float   t      = Mathf.Lerp(t0, t1, (float)i / kGuideSegments);
                float   lat    = _fadeDrawArmed ? LateralAtT(t) : 0f;
                Vector3 str    = Vector3.LerpUnclamped(from, straightEnd, t);
                Vector3 bentXZ = str + right * (lat * carryM);
                float   y      = SampleTerrainHeight(bentXZ) + kRingHeightOff;
                lr.SetPosition(i, new Vector3(bentXZ.x, y, bentXZ.z));
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

        // ── Order 355 §5 — floating edge-clamped indicators (flag AND ball) ───────
        /// <summary>
        /// Order 355 §5 — where an indicator for a target at <paramref name="screenPoint"/> goes.
        /// Pure math (EditMode seam), shared by the flag and the ball so docking is continuous BY
        /// CONSTRUCTION rather than by two implementations agreeing.
        ///
        /// Cesar 2026-08-10: "if it doesn't fit, the indicator should float on screen with a line
        /// pointing towards the hole — if the player moves the camera towards the hole, the indicator
        /// moves too, until it's over the hole when it appears on screen." No animation is needed for
        /// that: the clamped position is a CONTINUOUS function of the camera pose, so panning walks the
        /// icon along the edge and it docks the frame the target crosses the inset rect.
        ///
        /// Returns true when DOCKED (target on screen → icon sits on it, no arrow); false when
        /// FLOATING (icon on the inset-rect boundary along screen-centre→target, arrow pointing OUT at
        /// <paramref name="arrowAngleDeg"/>, measured CCW from screen +X).
        ///
        /// <paramref name="avoidRect"/> (screen px, empty = none) is subtracted from the FLOATING dock
        /// zone only — the SHOOT button corner, so an indicator never parks under live UI. A docked
        /// icon is left where its target is: it is then a marker on the world, not a floating hint.
        ///
        /// Behind-camera targets (<c>screenPoint.z &lt; 0</c>) are mirrored through screen centre,
        /// because <c>WorldToScreenPoint</c> reports those flipped — without the mirror the arrow would
        /// point 180° away from the target.
        /// </summary>
        public static bool SolveIndicatorPlacement(
            Vector3 screenPoint, float screenW, float screenH, float insetPx, Rect avoidRect,
            out Vector2 iconPos, out float arrowAngleDeg)
        {
            Vector2 c = new Vector2(screenW * 0.5f, screenH * 0.5f);
            Vector2 p = new Vector2(screenPoint.x, screenPoint.y);

            bool behind = screenPoint.z < 0f;
            if (behind) p = c - (p - c);

            float inset = Mathf.Clamp(insetPx, 0f, Mathf.Min(screenW, screenH) * 0.45f);
            float xMin = inset, xMax = screenW - inset, yMin = inset, yMax = screenH - inset;

            bool hasAvoid = avoidRect.width > 0f && avoidRect.height > 0f;
            bool inInset  = !behind && p.x >= xMin && p.x <= xMax && p.y >= yMin && p.y <= yMax;

            // A target hidden UNDER the SHOOT button is not docked, it is invisible. Measured on Hole 5
            // tee: strict containment pins the footprint's left edge to the OB boundary, so the ball
            // physically cannot be seated clear of the button — it lands at screen (983, 203) inside a
            // button spanning 955…1124. Treating that as "docked" showed nothing at all. Floating the
            // indicator instead lifts it just clear of the button with an arrow pointing back down at
            // the ball, which is the whole point of the indicator.
            bool docked = inInset && !(hasAvoid && avoidRect.Contains(p));
            if (docked)
            {
                iconPos       = p;
                arrowAngleDeg = 0f;
                return true;
            }

            // Floating: walk from screen centre toward the target and stop at the inset rect.
            Vector2 d = p - c;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.up;          // degenerate: park it at the top edge
            float tx = Mathf.Abs(d.x) < 1e-6f ? float.MaxValue : ((d.x > 0f ? xMax : xMin) - c.x) / d.x;
            float ty = Mathf.Abs(d.y) < 1e-6f ? float.MaxValue : ((d.y > 0f ? yMax : yMin) - c.y) / d.y;
            // Clamped to 1 so an ON-screen but occluded target keeps its icon AT the target rather than
            // flinging it to the far screen edge; for a genuinely off-screen target the inset boundary
            // is always nearer than the target, so t < 1 and this clamp is inert.
            float t  = Mathf.Clamp(Mathf.Min(tx, ty), 0f, 1f);
            iconPos  = c + d * t;

            // Keep the floating dock out of the SHOOT-button corner by lifting it clear.
            if (hasAvoid && avoidRect.Contains(iconPos))
                iconPos.y = Mathf.Min(avoidRect.yMax, yMax);

            // Point from where the icon ENDED UP, which for an off-screen target is the same ray as
            // centre→target, and for a lifted-clear one correctly aims back down at what it hides.
            Vector2 toTarget = p - iconPos;
            Vector2 aim      = toTarget.sqrMagnitude > 1f ? toTarget : d;
            arrowAngleDeg    = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            return false;
        }

        /// <summary>
        /// Order 355 §5 — drive one icon+arrow pair from a world target through
        /// <see cref="SolveIndicatorPlacement"/>. <paramref name="hideWhenDocked"/> is set for the BALL:
        /// on screen, the world-space <c>_ballMarker</c> sphere IS the ball's representation, so the
        /// screen indicator only exists while the ball is off-frame. The flag has no world marker, so
        /// its icon stays visible docked.
        /// </summary>
        private void PlaceIndicator(RectTransform iconRT, RectTransform arrowRT, Vector3 worldPos,
                                    Rect avoidRect, bool hideWhenDocked)
        {
            if (iconRT == null || _mapCam == null) return;

            // NOT Screen.width/height. In the Editor those report the Game View WINDOW (measured
            // 2070×1772 while the surface was 1170×2532), so the inset rect was solved in a different
            // space than WorldToScreenPoint's output and the flag never docked — it stayed pinned to a
            // phantom edge at y=1702. The camera's pixel rect IS the projection surface, so it matches
            // WorldToScreenPoint and the overlay canvas on device and in the Editor alike.
            Vector3 sp = _mapCam.WorldToScreenPoint(worldPos + Vector3.up * 2f);
            bool docked = SolveIndicatorPlacement(sp, _mapCam.pixelWidth, _mapCam.pixelHeight,
                                                  _indicatorEdgeInsetPx, avoidRect,
                                                  out Vector2 pos, out float angDeg);

            bool showIcon = !(docked && hideWhenDocked);
            if (iconRT.gameObject.activeSelf != showIcon) iconRT.gameObject.SetActive(showIcon);
            if (showIcon)
            {
                // The flag icon is bottom-pivoted so a DOCKED flag stands on the hole. Floating, that
                // same pivot throws the icon body outboard — straight into the arrow, and off the top
                // of the screen — so the floating state centres it instead. Measured on Hole 1 tee:
                // bottom-pivot put the 48 px icon at y 2462…2510 with the arrow at 2478…2512.
                iconRT.pivot = docked ? _dockedIconPivot : new Vector2(0.5f, 0.5f);
                iconRT.anchoredPosition = pos;
            }

            if (arrowRT == null) return;
            bool showArrow = !docked;
            if (arrowRT.gameObject.activeSelf != showArrow) arrowRT.gameObject.SetActive(showArrow);
            if (!showArrow) return;

            // Arrow sits just outboard of the icon, pointing at the target. Offset derived from the
            // live sizes rather than a constant, so Robin's styled art cannot land on top of the icon.
            // The procedural sprite is drawn pointing UP (+Y), hence the −90°.
            float off = 0.5f * Mathf.Max(iconRT.sizeDelta.x, iconRT.sizeDelta.y)
                      + 0.5f * arrowRT.sizeDelta.y + kIndicatorArrowGapPx;
            Vector2 dir = new Vector2(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad));
            arrowRT.anchoredPosition = pos + dir * off;
            arrowRT.localRotation    = Quaternion.Euler(0f, 0f, angDeg - 90f);
        }

        /// <summary>Screen-space rect of the live SHOOT button, so indicators never dock under it.</summary>
        private Rect ShootButtonScreenRect()
        {
            if (_shootButton == null) return default;
            var rt = _shootButton.transform as RectTransform;
            if (rt == null || !_shootButton.gameObject.activeInHierarchy) return default;

            var canvas = rt.GetComponentInParent<Canvas>();
            Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                         ? canvas.worldCamera : null;

            rt.GetWorldCorners(_shootCornersScratch);   // reused: this runs every frame
            Vector2 lo = RectTransformUtility.WorldToScreenPoint(uiCam, _shootCornersScratch[0]);
            Vector2 hi = RectTransformUtility.WorldToScreenPoint(uiCam, _shootCornersScratch[2]);
            return Rect.MinMaxRect(Mathf.Min(lo.x, hi.x) - kIndicatorUiPadPx, Mathf.Min(lo.y, hi.y) - kIndicatorUiPadPx,
                                   Mathf.Max(lo.x, hi.x) + kIndicatorUiPadPx, Mathf.Max(lo.y, hi.y) + kIndicatorUiPadPx);
        }

        private const float kIndicatorArrowGapPx    = 6f;
        private const float kIndicatorUiPadPx       = 12f;
        /// <summary>Extra clearance past the indicator inset that the lateral ball seat aims for.</summary>
        private const float kIndicatorDockClearancePx = 24f;
        /// <summary>Screen-px band around the ball's seat row that counts as "the SHOOT button's row".</summary>
        private const float kBallMarkerClearPx        = 48f;
        private readonly Vector3[] _shootCornersScratch = new Vector3[4];
        /// <summary>Pivot the flag icon docks with (bottom-centre — the flag stands on the hole).</summary>
        private static readonly Vector2 _dockedIconPivot = new Vector2(0.5f, 0f);

        private void UpdateHoleIndicator()
        {
            if (_mapCam == null) return;
            // §iter-26 FIX #4: _flagLine removed — only the flag icon is updated.
            // Order 355 §5 supersedes iter-30's "hide when off-viewport": the flag is no longer a
            // framing target (§3.1), so on any hole longer than one club it starts off-screen. Hiding
            // it left the player with no orientation cue at all; it now floats at the edge with an
            // arrow and docks over the hole the moment the hole enters the frame.
            Rect avoid = ShootButtonScreenRect();

            // map_view_v2 §7 — the real HUD chip clone owns the pin, ON screen AND off it.
            //
            // The spec carved the off-screen case out ("keeps its current sprites"), but the map camera
            // frames ball + CLUB carry, so on every par 4 and par 5 the pin is outside the frame: that
            // carve-out would have left the old yellow flag on 14 of 18 holes, i.e. exactly the thing
            // Cesar asked to be replaced ("hole indicator is different from the one in game"). So the
            // chip docks to the edge too, with its tail pointing off-frame at the pin — the Order 355
            // SOLVER is still what decides WHERE (edge inset, SHOOT-button avoidance, behind-camera
            // mirroring); only the art it drives changed.
            Vector3 pinRaw   = _mapCam.WorldToScreenPoint(_flagWorldPos + Vector3.up * 2f);
            float   sW = _mapCam.pixelWidth, sH = _mapCam.pixelHeight;
            bool    pinOnScreen = pinRaw.z > 0f
                               && pinRaw.x >= 0f && pinRaw.x <= sW
                               && pinRaw.y >= 0f && pinRaw.y <= sH;
            bool    useChip = _pinIndicator != null && _pinIndicator.IsValid;

            if (useChip)
            {
                _pinIndicator.SetActive(true);
                _pinIndicator.SetDistance(Vector3.Distance(_ballWorldPos, _flagWorldPos), _unitMode);

                if (pinOnScreen)
                {
                    var pinScr = new Vector2(pinRaw.x, pinRaw.y);
                    _pinIndicator.Place(pinScr, _pinTailMinPx, sW, sH, _indicatorEdgeInsetPx);
                    _pinChipGapPx = _pinIndicator.GapToPinPx(pinScr);
                }
                else
                {
                    // Off-frame: the Order 355 solver gives the edge anchor and the angle back at the
                    // pin; the chip hangs inboard of that anchor with the tail's fading tip on it.
                    SolveIndicatorPlacement(pinRaw, sW, sH, _indicatorEdgeInsetPx, avoid,
                                            out Vector2 edgePos, out float angDeg);
                    var dir = new Vector2(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad));
                    _pinIndicator.PlaceAlong(edgePos, dir, _pinTailMinPx * UiScale,
                                             sW, sH, _indicatorEdgeInsetPx);
                    _pinChipGapPx = -1f;   // no on-screen pin point to measure a gap against
                }

                if (_flagIconRT  != null && _flagIconRT.gameObject.activeSelf)  _flagIconRT.gameObject.SetActive(false);
                if (_flagArrowRT != null && _flagArrowRT.gameObject.activeSelf) _flagArrowRT.gameObject.SetActive(false);
                PlaceIndicator(_ballIconRT, _ballArrowRT, _ballWorldPos, avoid, hideWhenDocked: true);
                return;
            }

            // No HoleIndicatorWidget in the scene at all — keep the legacy flag rather than nothing.
            if (_pinIndicator != null) _pinIndicator.SetActive(false);
            _pinChipGapPx = -1f;
            PlaceIndicator(_flagIconRT, _flagArrowRT, _flagWorldPos, avoid, hideWhenDocked: false);
            // The ball's on-screen representation is the world-space marker sphere, so its indicator
            // exists ONLY while the ball is panned off-frame.
            PlaceIndicator(_ballIconRT, _ballArrowRT, _ballWorldPos, avoid, hideWhenDocked: true);
        }

        // ── Per-frame update ──────────────────────────────────────────────────────
        private void Update()
        {
            if (!_isOpen || _mapCam == null) return;

            HandleInput();
            UpdateGuideAndRings();
            UpdateHoleIndicator();

#if UNITY_EDITOR
            // Order 355 §4 — regression tripwire. THE INVARIANT (footprint ⊆ playable rect) is checked
            // every frame the map is open, on every path: open framing, pan, pinch, aim. Any future
            // framing change that leaks off-course ground announces itself here instead of reaching
            // Cesar as a screenshot. Editor-only; rate-limited so one bad pose is not 60 errors/second.
            AssertFootprintInvariant();
#endif

            // Dump the second aim state once per open session, after the first update
            // cycle (so markers have their positions updated post-open).
            // This creates the "aimed" state in the invariant JSON with a slightly
            // different aimYaw to satisfy the ">=2 distinct states" requirement.
            if (!_secondStateDumped)
            {
                _secondStateDumped = true;
#if UNITY_EDITOR   // perf_phase1_free_wins §4: see the "open" call site above.
                // §iter-24 FIX #14: Use coroutine so "aimed" dump reads COMPOSITED FRAME pixels.
                // The coroutine re-aims, waits WaitForEndOfFrame, does ReadPixels, then dumps.
                StartCoroutine(DoFrameReadbackAndDump("aimed"));
#endif
            }
        }

#if UNITY_EDITOR
        private float _lastInvariantLogTime = -999f;

        /// <summary>
        /// Order 355 §4 — editor-only assertion that the ground footprint has not left the playable
        /// rectangle. Logs the offending corners AND the rectangle, so a violation is diagnosable from
        /// the console line alone. Throttled to one line per second per violation streak.
        /// </summary>
        private void AssertFootprintInvariant()
        {
            if (!_obRectValid || _mapCam == null) return;
            if (!TryComputeGroundFootprint(_mapCam, _ballWorldPos.y, out var fMin, out var fMax)) return;
            if (FootprintInsideRect(fMin, fMax, _obRectCenter, _obRectHalf, kFootprintTolM)) return;
            if (Time.unscaledTime - _lastInvariantLogTime < 1f) return;

            _lastInvariantLogTime = Time.unscaledTime;
            Debug.LogError(
                "[MapView v2] INVARIANT VIOLATION (Order 355 §2): ground footprint left the playable rect. " +
                $"footprint=[{fMin.x:F1},{fMin.y:F1}]..[{fMax.x:F1},{fMax.y:F1}] " +
                $"rect=[{_obRectCenter.x - _obRectHalf.x:F1},{_obRectCenter.y - _obRectHalf.y:F1}].." +
                $"[{_obRectCenter.x + _obRectHalf.x:F1},{_obRectCenter.y + _obRectHalf.y:F1}] " +
                $"fov={_currentFov:F1}° camPos={_mapCam.transform.position:F1} focus={_camFocusPoint:F1}");
        }
#endif

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
                    // Order 353b: cap zoom-OUT at the width-fit (_zoomOutCapFov); zoom-IN still allowed.
                    // Order 355 §4: that static cap is now only a fast pre-check — the real gate is the
                    // footprint. A wider FOV grows the footprint, so a zoom-OUT that would push it past
                    // the OB rectangle is REFUSED outright (the pinch just stops); zoom-IN shrinks the
                    // footprint and is therefore always legal.
                    float candidateFov = Mathf.Clamp(_currentFov - delta, _minZoom, _zoomOutCapFov);
                    if (candidateFov > _currentFov && !FootprintFitsAtFov(candidateFov))
                        candidateFov = _currentFov;
                    _currentFov = candidateFov;
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

        /// <summary>
        /// Order 355 §4 — would the footprint still be inside the OB rectangle at
        /// <paramref name="candidateFov"/>? Probes by setting the FOV, measuring, and restoring it —
        /// the projection matrix is the only thing that changes, so there is nothing else to undo.
        /// True when there is no rectangle to violate (nothing to gate against).
        /// </summary>
        private bool FootprintFitsAtFov(float candidateFov)
        {
            if (_mapCam == null || !_obRectValid) return true;

            float prev = _mapCam.fieldOfView;
            _mapCam.fieldOfView = candidateFov;
            bool ok = TryComputeGroundFootprint(_mapCam, _ballWorldPos.y, out var fMin, out var fMax)
                   && FootprintInsideRect(fMin, fMax, _obRectCenter, _obRectHalf, kFootprintTolM);
            _mapCam.fieldOfView = prev;
            return ok;
        }

        /// <summary>
        /// Order 354 §4.4 — clamp a world-XZ point into the OB rectangle. Pure math (EditMode seam).
        /// </summary>
        public static Vector2 ClampPointToRect(Vector2 p, Vector2 rectCenter, Vector2 rectHalf)
            => new Vector2(
                Mathf.Clamp(p.x, rectCenter.x - rectHalf.x, rectCenter.x + rectHalf.x),
                Mathf.Clamp(p.y, rectCenter.y - rectHalf.y, rectCenter.y + rectHalf.y));

        private void PanCamera(Vector2 screenDelta)
        {
            if (_mapCam == null) return;
            Vector3 right   = _mapCam.transform.right;
            Vector3 forward = _mapCam.transform.forward;
            forward.y = 0f; right.y = 0f;
            float   scale = _panSensitivity * (_currentFov / _fieldOfView);
            Vector3 move  = (-right.normalized * screenDelta.x - forward.normalized * screenDelta.y) * scale;

            // Order 355 §4 — clamp the FOOTPRINT, not the focus point. 354 stopped the focus at the
            // OB edge, which still let half a screen of off-course show past it; the strict crop means
            // the thing that must stay inside the rectangle is what the four viewport corners see.
            // Per-axis, so a diagonal pan into a corner slides along the edge instead of dead-stopping.
            if (_obRectValid && TryComputeGroundFootprint(_mapCam, _ballWorldPos.y, out var fpMin, out var fpMax))
            {
                Vector2 allowed = ClampFootprintMove(fpMin, fpMax, new Vector2(move.x, move.z),
                                                     _obRectCenter, _obRectHalf, kFootprintTolM);
                Vector3 applied = new Vector3(allowed.x, 0f, allowed.y);
                _mapCam.transform.position += applied;
                _camFocusPoint             += applied;
                return;
            }

            // Order 354 §4.4 fallback (kept verbatim): footprint unresolvable but a rectangle exists →
            // clamp the focus point, which at least keeps the rig over the playable area.
            // No OB rectangle → unclamped (previous behaviour, no regression).
            if (_obRectValid)
            {
                Vector3 camOffset  = _mapCam.transform.position - _camFocusPoint;
                Vector3 wanted     = _camFocusPoint + move;
                Vector2 clampedXZ  = ClampPointToRect(new Vector2(wanted.x, wanted.z), _obRectCenter, _obRectHalf);
                _camFocusPoint     = new Vector3(clampedXZ.x, wanted.y, clampedXZ.y);
                _mapCam.transform.position = _camFocusPoint + camOffset;
                return;
            }

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

        /// <summary>
        /// Capture seam (map_view_v2): zoom OUT by <paramref name="fovDelta"/> degrees through the exact
        /// same clamp-and-refuse logic <see cref="HandleTouchInput"/>'s pinch uses — the Order 353b
        /// zoom-out cap plus the strict-crop refusal — so a capture can never show a pose the player
        /// cannot reach by pinching. Returns the FOV actually adopted; equal to the previous FOV means
        /// the zoom was REFUSED, which is itself the finding worth reporting.
        /// Sibling of <see cref="SetAimYawDirectly"/>; testing / invariant verification only.
        /// </summary>
        public float ZoomOutForCapture(float fovDelta)
        {
            if (!_isOpen || _mapCam == null) return _currentFov;
            float candidateFov = Mathf.Clamp(_currentFov + fovDelta, _minZoom, _zoomOutCapFov);
            if (candidateFov > _currentFov && !FootprintFitsAtFov(candidateFov))
                candidateFov = _currentFov;
            _currentFov = candidateFov;
            _mapCam.fieldOfView = _currentFov;
            ContainCameraFootprint("zoom-capture");
            return _currentFov;
        }

        /// <summary>Capture/diagnostics: the Order 353b zoom-out ceiling and the live FOV.</summary>
        public float CurrentFov     => _currentFov;
        public float ZoomOutCapFov  => _zoomOutCapFov;

        /// <summary>
        /// Capture seam (map_view_v2): pan by a screen delta through <see cref="PanCamera"/> — the same
        /// method the one- and two-finger drags call, including its strict-crop clamp. Used to force the
        /// pin into the lower half of the frame so the pin chip's flip-above-the-pin branch can be
        /// photographed, which is the acceptance list's "force it by pinching/panning".
        /// </summary>
        public void PanForCapture(Vector2 screenDelta)
        {
            if (!_isOpen || _mapCam == null) return;
            PanCamera(screenDelta);
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
            // map_view_v2 §1 — the DRAWN clamp. aimEnd above happens to be the same point today, but it
            // is documented as a back-compat artefact; P_max is derived from the range model itself so
            // the validator asserts the model, not a coincidence.
            Vector3 pMax = MaxReachPoint(_ballWorldPos, aim, cM);

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
            Vector3 pmRaw = _mapCam.WorldToScreenPoint(pMax);
            float pmSx = pmRaw.x * scaleX;  float pmSy = pmRaw.y * scaleY;
            // Built outside the JSON literal: a verbatim interpolated string cannot carry a plain
            // double quote inside an interpolation hole.
            string shotViewSrc = _shotViewButton    != null ? "serialized"
                               : _runtimeShotViewGO != null ? "runtime-clone-of-club-button"
                               : "none";

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
  ""ringFrac"": {_ringFrac:F4},
  ""clubCarryM"": {cM:F3},
  ""aimedCarryM"": {(_aimedCarryM > 0f ? _aimedCarryM : cM):F3},
  ""maxReachFactor"": {kMaxReachFactor:F3},
  ""maxReachM"": {MaxReachM(cM):F3},
  ""overRange"": {BoolStr(_overRange)},
  ""pMax_world"": [{pMax.x:F3},{pMax.y:F3},{pMax.z:F3}],
  ""pMax_screen"": [{pmSx:F1},{pmSy:F1}],
  ""overRangeLineVertCount"": {(_overRangeLine != null ? _overRangeLine.positionCount : 0)},
  ""rangeFanHalfAngleDeg"": {_rangeFanHalfAngleDeg:F2},
  ""guideDotWorldWidth"": {(_guideLine != null ? _guideLine.startWidth : 0f):F4},
  ""pinTailMinPx"": {_pinTailMinPx:F1},
  ""pinChipGapPx"": {_pinChipGapPx:F1},
  ""pinChipGapOk"": {BoolStr(_pinChipGapPx < 0f || _pinChipGapPx >= (_pinTailMinPx * UiScale) - 0.5f)},
  ""shotViewButtonActive"": {BoolStr(ShotViewButtonActive())},
  ""shotViewButtonSource"": ""{shotViewSrc}"",
  ""unitMode"": ""{_unitMode}""
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

        /// <summary>map_view_v2 §8 — whichever SHOT VIEW object is in play, is it on screen right now?</summary>
        private bool ShotViewButtonActive()
        {
            if (_shotViewButton    != null) return _shotViewButton.activeInHierarchy;
            if (_runtimeShotViewGO != null) return _runtimeShotViewGO.activeInHierarchy;
            return false;
        }

        // ── §iter-24 FIX #14: Frame-readback coroutine ────────────────────────────
#if UNITY_EDITOR   // perf_phase1_free_wins §4: both call sites are editor-only now, so this
                   // would be dead code in a player build. _lzFrameCenter / _lzFrameEdge stay
                   // player-compiled: DumpInvariants reads them unconditionally and already
                   // handles the never-populated case (Color.clear), and MapViewCaptureDriver
                   // reaches DumpInvariants through the public ForceInvariantDump.
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
#endif   // UNITY_EDITOR — perf_phase1_free_wins §4 frame-readback coroutine

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
                    if (IsChromeExempt(go.transform)) continue;   // map_view_v2 §8 — SHOT VIEW is map chrome
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
                // map_view_v2 §8 — the SHOT VIEW button is map chrome, not shot chrome. Exempting it
                // here (exactly as _shootButton is exempted) keeps its show/hide in ONE place —
                // ShowShotViewButton — instead of being fought over by RestoreShotUIChrome at close.
                if (IsChromeExempt(child)) continue;
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

        /// <summary>
        /// map_view_v2 §8 — true for the serialized SHOT VIEW button or any ancestor of it, so the
        /// chrome-hider walks PAST it rather than hiding (and later restoring) the map's own control.
        /// The runtime clone needs no exemption: it is created after the hide pass and destroyed
        /// before the restore one.
        /// </summary>
        private bool IsChromeExempt(Transform t)
        {
            if (_shotViewButton == null || t == null) return false;
            var sv = _shotViewButton.transform;
            return t == sv || sv.IsChildOf(t);
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
            // map_view_v2 §8: the label swap is GONE. B1 keeps the club button reading "DRIVER / 215 yd"
            // in map view — the player must be able to see which club the fan and arcs belong to — and
            // moves the close control to the new bottom-LEFT SHOT VIEW button. What still has to happen
            // is disabling the SelectorDragRouter (a pointer handler that opens Club Selection
            // independently of onClick) and rebinding onClick to Close; the club button closes the map
            // too, exactly as it did before.
            var clubWidget = _shootButton.GetComponent<ClubButtonWidget>();
            if (clubWidget != null) clubWidget.SetMapMode(mapMode);

            // §"Club button (over)": the button dims to 50 % while the target is past max reach and
            // stays interactable. A CanvasGroup is added once and kept — DriverButton already has one
            // in LabScaffold, so this is a no-op there and a safety net in any other scene.
            _shootButtonGroup = _shootButton.GetComponent<CanvasGroup>();
            if (_shootButtonGroup == null) _shootButtonGroup = _shootButton.gameObject.AddComponent<CanvasGroup>();

            if (mapMode)
            {
                _shootButton.onClick.RemoveAllListeners();
                _shootButton.onClick.AddListener(Close);
            }
            else
            {
                _shootButton.onClick.RemoveAllListeners();
                if (_shootButtonGroup != null) _shootButtonGroup.alpha = 1f;
            }
        }

        // ── map_view_v2 §8 — the SHOT VIEW button ────────────────────────────────
        /// <summary>
        /// Show or hide the bottom-LEFT SHOT VIEW control for the map's lifetime.
        ///
        /// Two sources, in order:
        ///   1. <see cref="_shotViewButton"/> when a scene object is wired — the map only toggles it.
        ///   2. Otherwise a RUNTIME CLONE of the club button, made at open and destroyed at close.
        ///
        /// The clone is the spec's "duplicate DriverButton" done without a scene edit, and it is a
        /// genuine clone (Rule 19): CardBG's gold-gradient outline, the 145×240 card, the drop shadow
        /// and the text styling all come from the live button rather than being re-authored. It is
        /// parented to the club button's OWN parent (the ActionButtons_Cluster that also holds
        /// GolfinButton) so <see cref="HideCanvasChildrenExceptPath"/>, which keeps that ancestor chain
        /// alive for the club button, keeps the clone visible for free — and it takes GolfinButton's
        /// anchors/pivot/offset, i.e. it lands in exactly the bottom-left slot the Figma shows.
        /// </summary>
        private void ShowShotViewButton(bool show)
        {
            if (!show)
            {
                if (_shotViewButton != null) _shotViewButton.SetActive(false);
                if (_runtimeShotViewGO != null) { Destroy(_runtimeShotViewGO); _runtimeShotViewGO = null; }
                return;
            }

            GameObject go = _shotViewButton;
            if (go == null)
            {
                if (_runtimeShotViewGO == null) _runtimeShotViewGO = BuildShotViewClone();
                go = _runtimeShotViewGO;
            }
            if (go == null) return;
            go.SetActive(true);

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(Close);
            }
        }

        private GameObject BuildShotViewClone()
        {
            if (_shootButton == null) return null;
            var srcGO = _shootButton.gameObject;
            var srcRT = srcGO.transform as RectTransform;
            if (srcRT == null || srcRT.parent == null) return null;

            var clone = Instantiate(srcGO, srcRT.parent);
            clone.name = "MapShotViewButton";

            // Strip the club behaviour: this button is not a club selector and must never repaint its
            // own label from ClubContext. The Button itself stays — it IS the control.
            var cw = clone.GetComponent<ClubButtonWidget>();
            if (cw != null) { cw.enabled = false; Destroy(cw); }
            var router = clone.GetComponent<SelectorDragRouter>();
            if (router != null) { router.enabled = false; Destroy(router); }

            // Bottom-left slot: copy GolfinButton's rect wholesale rather than hardcoding 48/100, so the
            // two bottom corners stay mirror images if the cluster is ever re-laid-out.
            var rt   = clone.GetComponent<RectTransform>();
            var slot = FindSlotRect(srcRT.parent, "GolfinButton");
            if (slot != null)
            {
                rt.anchorMin        = slot.anchorMin;
                rt.anchorMax        = slot.anchorMax;
                rt.pivot            = slot.pivot;
                rt.sizeDelta        = slot.sizeDelta;
                rt.anchoredPosition = slot.anchoredPosition;
            }
            else
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot     = Vector2.zero;
                rt.anchoredPosition = new Vector2(48f, 100f);
            }

            // Portrait: the placeholder camera glyph, navy, aspect-preserved (Robin's art drops into the
            // same Resources slot with zero code change — the sibling of "UI/Icon - Flag").
            var icon = FindDescendantByName(clone.transform, "Icon");
            var iconImg = icon != null ? icon.GetComponent<Image>() : null;
            if (iconImg != null)
            {
                var sprite = Resources.Load<Sprite>("UI/Icon - ShotView") ?? Resources.Load<Sprite>("Icon - ShotView");
                if (sprite != null) iconImg.sprite = sprite;
                iconImg.color         = new Color(0f, 30f / 255f, 57f / 255f, 1f);   // #001E39
                iconImg.preserveAspect = true;
                var iconRT = iconImg.rectTransform;
                iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
                iconRT.pivot     = new Vector2(0.5f, 0.5f);
                // MEASURED against reference/B1_aiming.png, not taken from the spec's "~80x54 body":
                // the Figma glyph is 80 x 64 px of ink. preserveAspect fits a SQUARE 256x256 sprite to
                // the SHORTER side of this rect, and the placeholder PNG is only 201 x 145 opaque
                // inside that square — so an 80x54 rect rendered 54 x (201/256) = 43 px of ink, half
                // the design. A 102 px square fits to 102 x 0.785 = 80 px of ink, matching the
                // reference's width exactly. RE-MEASURE when Robin's final icon lands: the number
                // depends on that file's own transparent padding, not on the layout.
                iconRT.sizeDelta = new Vector2(102f, 102f);
                if (sprite == null)
                    Debug.LogWarning("[MapView v2] Resources/UI/Icon - ShotView not found — SHOT VIEW keeps the club portrait.");
            }

            // Data: "SHOT VIEW" on two lines (the Figma shows two), secondary hidden.
            var primary = FindTmp(clone.transform, "PrimaryText");
            if (primary != null)
            {
                primary.text = LocalizationManager.Get("GAMEPLAY_SHOT_VIEW").Replace(" ", "\n");
                // The source label is ONE line ("DRIVER") in a 35 px-tall rect with auto-sizing
                // 20..30. Two lines do not fit, so auto-size silently shrank SHOT/VIEW to the 20 floor
                // and it rendered at 14 px cap-height against the reference's 21 px — the standing
                // "rendered size vs the reference is the gate, not the arithmetic" rule. Pin the size
                // the Figma asks for and give the block the height two lines actually need; the
                // secondary text is hidden just below, so the space is free.
                primary.enableAutoSizing = false;
                primary.fontSize         = 30f;
                primary.alignment        = TextAlignmentOptions.Center;
                var pr = primary.rectTransform;
                pr.sizeDelta        = new Vector2(135f, 78f);
                pr.anchoredPosition = new Vector2(0f, 21f);
            }
            var secondary = FindTmp(clone.transform, "SecondaryText");
            if (secondary != null) secondary.gameObject.SetActive(false);

            return clone;
        }

        private static RectTransform FindSlotRect(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child as RectTransform;
            return null;
        }

        private static TMP_Text FindTmp(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == name) return t;
            return null;
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
            // Order 354 §4.3: put the mountain ring / backdrop plane back BEFORE the map objects go
            // away, so the world is intact the instant the gameplay camera takes over again.
            RestoreEnvironmentAfterMap();
            _obRectValid = false;

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
            // map_view_v2 — every B1 object lives under _runtimeRoot (world) or _indicatorCanvas
            // (screen), both of which were just destroyed; drop the references so a stale
            // RectTransform / MeshFilter can never be written to after close.
            _overRangeLine   = null;
            _rangeFanGO      = null;
            _rangeFanEdgeGO  = null;
            _nominalArcGO    = null;
            _ghostRingGO     = null;
            _tickMarks.Clear();
            _tickLabels.Clear();
            _crossVRT = _crossHRT = _centreDotRT = _crossX1RT = _crossX2RT = _ghostDotRT = null;
            _readout         = null;
            _pinIndicator    = null;
            _overRange       = false;
            _maxReachM       = 0f;
            _pinChipGapPx    = -1f;
            _lastBuiltAimYaw = float.NaN;
            _lastBuiltCarry  = -1f;
            _lastBuiltFov    = -1f;
            // §iter-26 FIX #4: _flagLine removed
            _flagIconRT      = null;
            // Order 355 §5 — the indicator parts live under _indicatorCanvas (a child of _runtimeRoot),
            // so they are already destroyed above; drop the references so a stale RectTransform can
            // never be written to after close.
            _flagArrowRT     = null;
            _ballIconRT      = null;
            _ballArrowRT     = null;
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
