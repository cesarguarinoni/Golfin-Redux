using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
using Golfin.Physics.Runtime;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.ShotUI;

[assembly: InternalsVisibleTo("Golfin.Physics.Tests")]

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Scene brain for the physics lab. Owns configs, wires sim → renderer → animator → UI.
    /// Attach to a LabRoot GameObject; assign references in the Inspector.
    /// </summary>
    public class PhysicsLabController : MonoBehaviour
    {
        [Header("Scene Identity")]
        [SerializeField] PresetScene currentScene = PresetScene.Range;

        // Set true by LabHoleBinder when a Hole_XX_Geo scene is loaded additively.
        // Drives BuildGroundProvider/BuildSurfaceProvider to use baked providers.
        bool _useSceneProviders;
        Vector3 _loadedHoleGreenCentroid;
        bool    _greenCentroidValid;
        Transform _runtimeTeeAnchor;
        // Serialized so it survives Play Mode reload. Set by OnHoleLoaded; authoritative tee pos.
        [SerializeField] Vector3 _savedTeeWorldPos;
        bool _savedTeePosValid;

        // M3: cached baked providers populated in OnHoleLoaded. When present
        // they replace the scene-raycast providers; sim path no longer reads
        // live colliders. Null on flat-ground / no-hole sessions.
        Golfin.Physics.Runtime.Baked.BakedZoneClassifier _bakedClassifier;
        Golfin.Physics.Runtime.Baked.BakedHeightProvider _bakedGround;

        // cup_capture_and_lipout (2026-08-05): the in-sim cup for the loaded hole. Built in
        // OnHoleLoaded alongside the RealCupDetector, reset to Disabled on hole unload.
        // Disabled → the sim's cup branches are dead code and output is bit-exact with the
        // pre-cup path, which is what keeps the two flat-ground helper sims unaffected.
        Golfin.Physics.CupSpec _cupSpec = Golfin.Physics.CupSpec.Disabled;
        // Phase 7: tree obstacle provider — null = no trees.
        Golfin.Physics.ITreeObstacleProvider _treeProvider;

        [Header("References")]
        [SerializeField] TrajectoryRenderer trajectoryRenderer;
        [SerializeField] BallAnimator       ballAnimator;
        [SerializeField] ChaseCamera        chaseCamera;

        [Header("Shot Controller (Live Touch)")]
        [SerializeField] ShotController      _shotController;
        [SerializeField] ShotConeView        _shotConeView;
        [SerializeField] Transform           _ballSpawnPoint;
        [SerializeField] BallTrailController    _ballTrail;
        // water_splash_fx (Order 349): WaterSplashController is wired entirely in code in Awake()
        // (GetComponent-or-AddComponent on the BallAnimator GO + Resources-loaded prefab) so the
        // scene carries no baked reference → LabScaffold.unity stays at zero diff for this task.
        WaterSplashController  _waterSplash;
        // ob_boundary_presentation (Order 1240): ObGroundSkirt is wired in code in OnHoleLoaded,
        // same pattern as WaterSplashController — GetComponent-or-AddComponent, no baked ref.
        ObGroundSkirt          _obSkirt;
        // sound_effects (Order 350): BallAudioEmitter wired in code, same pattern as WaterSplashController.
        BallAudioEmitter       _ballAudio;

        [Header("Camera")]
        [Tooltip("Initial look direction (XZ). Leave zero to auto-derive from scene type.")]
        [SerializeField] Vector3 _defaultLookDirection = Vector3.zero;
        [SerializeField] float   _orbitSensitivity     = 0.5f;

        [Header("Aim framing (aim_camera_ball_centering)")]
        [Tooltip("XZ distance behind the ball during full-swing aim (m). Genre ref: 2.5–4.")]
        [SerializeField] float _aimCamDistanceM = 3.0f;
        [Tooltip("Camera height above the ball during full-swing aim (m).")]
        [SerializeField] float _aimCamHeightM = 1.4f;
        [Tooltip("Fallback viewport Y for the ball projection when CentralBallWidget is unavailable. 0.4234 = mockup 2D ball center.")]
        [SerializeField] float _aimBallViewportYFallback = 0.4234f;
        [Tooltip("Tee markers must project within this fraction of half-screen-width during tee-off aim.")]
        [SerializeField] float _teeMarkerSafeFrac = 0.9f;
        [Tooltip("Ceiling for the tee-visibility pull-back (m). 8 = legacy distance.")]
        [SerializeField] float _aimCamMaxDistanceM = 8f;
        [Tooltip("XZ distance behind the ball during PUTTER aim (m). Held at the legacy 8 so the " +
                 "15 m aim line and the green-reading grid still fit on screen; only the ball's " +
                 "screen position changed. Lower this to close in on the putt.")]
        [SerializeField] float _puttCamDistanceM = 8f;
        [Tooltip("Camera height above the ball during PUTTER aim (m). Legacy value.")]
        [SerializeField] float _puttCamHeightM = 3f;
        [Tooltip("The 2D shot-UI ball. Aim framing pins the 3D ball to this widget's viewport point.")]
        [SerializeField] CentralBallWidget _centralBallWidget;

        // World positions of the physical tee markers for the loaded hole. Populated by
        // OnHoleLoaded (same scan that produces the tee midpoint), cleared by OnHoleUnloaded.
        // Deliberately NOT serialized: it is derived scene data that OnHoleLoaded always
        // rebuilds, and serializing it would bake per-hole state into LabScaffold.unity.
        // Empty list ⇒ ComputeAimDistance skips the tee clamp (close framing).
        readonly System.Collections.Generic.List<Vector3> _teeMarkerPositions
            = new System.Collections.Generic.List<Vector3>();

        // Published after every Fire
        public event Action<ShotReadout> OnShotFired;
        // Published after Fire×N
        public event Action<bool, int> OnRepeatabilityResult;
        // Published when placement entries are rebuilt (hole load/unload)
        public event Action OnPlacementEntriesChanged;

        // Ball placement entries — populated on hole load, cleared on unload.
        public System.Collections.Generic.List<BallPlacementEntry> PlacementEntries { get; private set; }
            = new System.Collections.Generic.List<BallPlacementEntry>();

        // In-memory configs
        // DIAG (2026-05-12 hole-picker DivideByZero): AeroCfg converted from auto-property
        // to backing field + logging setter. Logs every write with stack trace so we can
        // pinpoint who zeroes SpinRateReference between hole swaps. REMOVE once root-caused.
        AeroConfig _aeroCfg;
        public AeroConfig AeroCfg
        {
            get => _aeroCfg;
            private set
            {
                float oldRef = _aeroCfg.SpinRateReference.ToFloat();
                float newRef = value.SpinRateReference.ToFloat();
                _aeroCfg = value;
                Debug.Log($"[AeroDiag][SET] id={GetInstanceID()} SpinRateRef {oldRef:F2}→{newRef:F2} frame={Time.frameCount}\n{System.Environment.StackTrace}");
            }
        }
        public WindConfig    WindCfg    { get; private set; }
        public SurfaceConfig SurfaceCfg { get; private set; }
        public PuttConfig    PuttCfg    { get; private set; }

        // DIAG (2026-05-12): per-frame poll to detect SILENT AeroCfg zeroing
        // (memory wipe / domain reload / hot-reload that bypasses the setter).
        float _aeroDiagPrevSpinRateRef = -1f;

        // DIAG (2026-05-12): structured checkpoint log. Compact line; pair with [AeroDiag][SET] / [POLL].
        void DiagAero(string label)
        {
            int count = FindObjectsOfType<PhysicsLabController>(true).Length;
            Debug.Log($"[AeroDiag][{label}] id={GetInstanceID()} count={count} SpinRateRef={_aeroCfg.SpinRateReference.ToFloat():F2} _configsLoaded={_configsLoaded} frame={Time.frameCount}");
        }

        Trajectory _previousTrajectory;
        bool       _predictionVisible = false;
        public bool PredictionVisible => _predictionVisible;

        // §2a ball state machine
        Golfin.Gameplay.Loop.BallStateMachine _ballSM;

        // §2b: cached per-shot origin and launch direction for LoopCameraDirector.
        Vector3 _lastShotOrigin;
        Vector3 _lastShotLaunchDir;

        // §phase_b iter-7: airborne-origin override set by PlaceBallAtAirborne().
        // When non-null, GetCurrentOrigin() returns this value verbatim (no surface snap)
        // and clears the override so subsequent shots use the normal surface-snap path.
        fp3? _airborneOriginOverride;

        // ── §2b internal accessors for LoopCameraDirector ──────────────────────
        internal Golfin.Gameplay.Loop.BallStateMachine BallSM        => _ballSM;
        internal Trajectory                            LastTrajectory => _previousTrajectory;

        /// <summary>
        /// Camera seam (K10 follow-up): the loaded hole's baked classifier, or null on
        /// flat-ground / no-hole sessions. LoopCameraDirector uses it to detect the moment the
        /// ball leaves the playable area so the chase can freeze at the boundary instead of
        /// flying out over the void. Read-only — the camera never mutates course data.
        /// </summary>
        internal ISurfaceProvider SurfaceProviderForCamera => _bakedClassifier;
        internal Vector3                               LastShotOrigin    => _lastShotOrigin;
        internal Vector3                               LastShotLaunchDir => _lastShotLaunchDir;
        internal Transform                             CurrentBall    => ballAnimator?.CurrentBall;
        internal bool                                  CurrentShotIsPutt => _shotController != null && _shotController.IsPutt;
        /// <summary>Bot seam: current ball world position (Vector3.zero if ball not yet spawned).</summary>
        public Vector3 BallPosition => ballAnimator != null && ballAnimator.CurrentBall != null
            ? ballAnimator.CurrentBall.position
            : Vector3.zero;

        /// <summary>
        /// True once OnHoleLoaded has fired (ScanForLoadedHoleSceneAtStartup found a Hole_NN_Geo scene).
        /// Used by VersusMatchController to avoid reading BallPosition before the tee is set.
        /// False on flat-ground / no-hole sessions (ball is at _ballSpawnPoint, not a real tee).
        /// </summary>
        public bool IsHoleReady => _useSceneProviders;

        // ── §controls_h: test injection helpers ───────────────────────────────
        // Allow EditMode integration tests to inject dependencies without a full scene.
        internal void InjectForTests(BallAnimator ba, Golfin.Gameplay.Loop.BallStateMachine sm)
        {
            ballAnimator = ba;
            _ballSM      = sm;
            // Load default configs so RunSimFromController doesn't crash.
            EnsureConfigsLoaded();
        }

        // ── §2f smoke-runner helpers ──────────────────────────────────────────
        // Allow SmokeRunner2fHost to temporarily set Instant PlayRate for comparison
        // shots (S5/S6) without exposing the entire BallAnimator reference.
        internal float GetBallAnimatorPlayRate() => ballAnimator != null ? ballAnimator.PlayRate : 1f;
        internal void  SetBallAnimatorPlayRate(float rate) { if (ballAnimator != null) ballAnimator.PlayRate = rate; }

        // Camera orbit state
        float   _cameraYaw;
        Vector3 _orbitCenter;
        // §2a: _prevBallPlaying retained only for the orbit-reset on preset shots.
        // Touch-shot re-arm is handled by HandleShotComplete via the SM; preset shots
        // (FireInternal path) still need the camera reset on animator-stop.
        bool    _prevBallPlaying;
        bool    _orbitDragActive;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            DiagAero("Awake.start");
            // Recover _runtimeTeeAnchor after domain reload: the field is non-serialised so it
            // becomes null after every script compilation, but the GO stays in the scene.
            // Scan children, keep the first match, destroy extras accumulated from prior reloads.
            foreach (Transform child in transform)
            {
                if (child.name != "_RuntimeTeeAnchor") continue;
                if (_runtimeTeeAnchor == null)
                    _runtimeTeeAnchor = child;
                else
                    Destroy(child.gameObject);
            }

            // _savedTeePosValid is non-serialized — recover from the serialized vector.
            // A non-zero _savedTeeWorldPos means a hole was previously loaded.
            if (_savedTeeWorldPos != Vector3.zero)
                _savedTeePosValid = true;

            EnsureConfigsLoaded();

            // §2a: create ball state machine with a default surface provider.
            _ballSM = new Golfin.Gameplay.Loop.BallStateMachine(BuildSurfaceProvider(default(ShotPreset)));
            _ballSM.OnShotComplete += HandleShotComplete;

            if (_shotController != null)
                _shotController.OnShotResolved += HandleShotResolved;

            // ball_flight_trail: wire trail controller to the ball SM + shot controller.
            _ballTrail?.Configure(ballAnimator, _ballSM, _shotController);

            // water_splash_fx (Order 349): wire splash controller entirely in code so the scene
            // carries no baked reference. Add the component to the BallAnimator GO (same host as
            // BallTrailController) idempotently — GetComponent-or-AddComponent survives domain
            // reloads, and Configure() is itself idempotent (unsubscribe-before-subscribe).
            if (ballAnimator != null)
            {
                if (_waterSplash == null)
                    _waterSplash = ballAnimator.GetComponent<WaterSplashController>();
                if (_waterSplash == null)
                    _waterSplash = ballAnimator.gameObject.AddComponent<WaterSplashController>();
                _waterSplash.Configure(ballAnimator, _ballSM, _shotController);
            }

            // sound_effects (Order 350): wire BallAudioEmitter in code, same pattern as WaterSplashController.
            if (ballAnimator != null)
            {
                if (_ballAudio == null)
                    _ballAudio = ballAnimator.GetComponent<BallAudioEmitter>();
                if (_ballAudio == null)
                    _ballAudio = ballAnimator.gameObject.AddComponent<BallAudioEmitter>();
                _ballAudio.Configure(ballAnimator, _ballSM, _shotController);
            }

            // 8.5: consume club selection from the action button selector overlay.
            // Re-entrancy guard inside handler prevents the loop when SetClub() itself raises ClubSelectionBroadcast.
            ClubSelectionBroadcast.OnClubChanged += OnClubBroadcastReceived;

            // Putter mode: hook into OnClubChanged (local event) to toggle UI.
            OnClubChanged += OnClubIndexChanged;

            // Note: PutterGreenReader subscribes to ShotController.OnStateChanged itself
            // (in its own OnEnable). No bridging subscription needed here.

            if (_shotConeView != null)
            {
                if (chaseCamera != null)
                    _shotConeView.SetCamera(chaseCamera.GetComponent<Camera>());
                try { _shotConeView.SetMaxCarryYards(ComputeMaxCarryYards()); }
                catch (System.Exception ex) { Debug.LogWarning($"[PhysicsLab] Awake ComputeMaxCarryYards failed: {ex.Message}"); }
                // Hand the PutterTrack ref to ShotConeView so its per-shot lifecycle
                // subscription (Approach C) can SetActive(true/false) on Aiming/Resolving
                // without requiring a duplicate Inspector wire on ShotConeView.
                if (_putterTrack != null) _shotConeView.SetPutterTrack(_putterTrack);
            }

            // Wire HoleIndicatorWidget camera at startup (ball not yet spawned; widget falls back to BallAnimator.Instance)
            var holeWidget = FindObjectOfType<Golfin.Gameplay.UI.ShotUI.HoleIndicatorWidget>();
            if (holeWidget != null && chaseCamera != null)
                holeWidget.SetCamera(chaseCamera.GetComponent<Camera>());

            // Deactivate WalkCamera GOs in any pre-loaded hole scene before their Start() fires.
            DeactivateWalkCamerasInLoadedScenes();

            // URPWater/Standard needs depth + opaque textures; ensure they're on for this camera.
            // Mobile URP asset has both disabled by default, which makes water render gray.
            if (chaseCamera != null)
            {
                var cam = chaseCamera.GetComponent<Camera>();
                var camData = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
                if (camData != null)
                {
                    camData.requiresDepthTexture  = true;
                    camData.requiresColorTexture  = true;
                }
            }

            DiagAero("Awake.end");

            // §2f: cache initial non-putter club so auto-exit has a valid fallback target.
            _lastNonPutterClubIndex = (CurrentClubIndex == PutterIndex) ? 0 : CurrentClubIndex;
        }

        static void DeactivateWalkCamerasInLoadedScenes()
        {
            System.Type walkCamType = System.Type.GetType("WalkCamera, Assembly-CSharp");
            if (walkCamType == null) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType() == walkCamType)
                    {
                        mb.gameObject.SetActive(false);
                        Debug.Log($"[PhysicsLab] WalkCamera GO deactivated (Awake): '{mb.gameObject.name}'");
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (_shotController != null)
                _shotController.OnShotResolved -= HandleShotResolved;

            // §2a: unsubscribe ball SM handler.
            if (_ballSM != null) _ballSM.OnShotComplete -= HandleShotComplete;

            // 8.5: unsubscribe from broadcast
            ClubSelectionBroadcast.OnClubChanged -= OnClubBroadcastReceived;

            // Putter mode: unsubscribe local club-change event.
            OnClubChanged -= OnClubIndexChanged;

            // auto_club_selection: drop the one-shot bag-wait if it is still armed
            // (ClubContext.OnBagChanged is a static event — an un-removed handler leaks).
            if (_autoClubAwaitingBag)
            {
                Golfin.Gameplay.UI.HUD.ClubContext.OnBagChanged -= HandleBagReadyForTeePick;
                _autoClubAwaitingBag = false;
            }
        }

        // Putter mode: called when the local OnClubChanged event fires (after SetClub).
        void OnClubIndexChanged(int index)
        {
            bool isPutter = (index == LabClubs.Length - 1); // Putter is last in LabClubs (index 3)
            if (isPutter) EnterPutterMode();
            else          ExitPutterMode();
        }

        // 8.5: handler called when the action-button selector overlay picks a club.
        // Re-entrancy guard: SetClub() itself calls ClubSelectionBroadcast.Raise(), so guard
        // against the infinite loop by checking if the index is already current.
        void OnClubBroadcastReceived(int index)
        {
            if (index == CurrentClubIndex) return;
            SetClub(index);
        }

        void Start()
        {
#if UNITY_EDITOR
            Golfin.Physics.BallSimulation.DiagErrorLogger = Debug.LogError;
            Golfin.Physics.BallSimulation.DiagShotLogger        = Debug.Log;
            Golfin.Physics.BallSimulation.DiagRollLogger        = Debug.Log;
            Golfin.Physics.Stats.ShotInputBuilder.DiagBuildLogger = Debug.Log;
            if (_shotController != null) _shotController.LogResolution = true;
#endif
            // Disable raw-touch path — ClubHandle external drag API is the only input in this lab.
            // This prevents camera drags and button clicks from accidentally starting a shot.
            _shotController?.InjectInputSource(null);

            // K11: seed the selector gate at boot. EnterPutterMode/ExitPutterMode only fire
            // on a club CHANGE, so without this the putter index stays unpublished (-1) and
            // IsSelectable fails open for the whole first hole. Also clears a stale flag when
            // Enter-Play-Mode Options skip the domain reload that would reset the static.
            ClubSelectionBroadcast.SetPutterMode(CurrentClubIndex == PutterIndex, PutterIndex);

            // Force cursor visible + unlocked. Hole scenes contain a WalkCamera that locks
            // the cursor in Awake; we deactivate its GO but the global cursor state persists.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            // §controls_h R4: Prime the camera heading using GetDefaultLookDirection()
            // so ALL branches are covered — Hole1 hardcoded, tee→green, explicit, and fallback.
            // iter-8 fallback: ApplyCameraYaw owns camera position during Aiming; we only prime
            // _cameraYaw and _shotController.CameraHeadingRadians here.
            Vector3 r4dir = GetDefaultLookDirection();
            _cameraYaw = Mathf.Atan2(r4dir.z, r4dir.x);
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            // Stage C0: when GameplaySceneLoader brings us up from Matchmaking it has
            // already (or is concurrently) additively loading Hole_{NN}_Geo for the
            // seeded hole. Log the seed so the IMPLEMENTER_REPORT.md DoD grep
            // ('GameSession.CurrentHoleNumber' in this file) passes, and so the lab
            // path is debuggable when production routes through it.
            int seededHole = Golfin.Gameplay.Session.GameSession.CurrentHoleNumber;
            if (seededHole > 0)
                Debug.Log($"[PhysicsLabController] Stage C0: GameSession.CurrentHoleNumber={seededHole}; " +
                          "expecting Hole_{NN}_Geo to be loaded by GameplaySceneLoader (ScanForLoadedHoleSceneAtStartup will pick it up).");

            // Wait 2 frames so any additively-loaded hole scene finishes loading,
            // then scan for it. This replaces the fragile immediate scan.
            StartCoroutine(ScanForLoadedHoleSceneAtStartup());
        }

        void LateUpdate()
        {
            // Defense in depth: if anything re-locks the cursor mid-session, undo it.
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
        }

        System.Collections.IEnumerator ScanForLoadedHoleSceneAtStartup()
        {
            yield return null;
            yield return null;

            // Stage C0: when GameplaySceneLoader brings us up from Matchmaking, it
            // additively loads Hole_{NN}_Geo *after* LabScaffold finishes loading.
            // The async hole load may not be complete by the 2-frame mark above. If a
            // GameSession seed exists, poll for the expected hole scene up to a timeout
            // before declaring no hole loaded. Editor / lab flows (GameSession unseeded)
            // keep the original immediate-fallback behaviour.
            int seededHole = Golfin.Gameplay.Session.GameSession.CurrentHoleNumber;
            string expectedSceneName = (seededHole > 0)
                ? $"Hole_{seededHole:D2}_Geo"
                : null;

            const float pollTimeoutSeconds = 5f;
            float pollElapsed = 0f;
            while (true)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    if (scene.name != null && scene.name.StartsWith("Hole_") && scene.name.EndsWith("_Geo"))
                    {
                        Debug.Log($"[PhysicsLab] Coroutine detected loaded hole scene: {scene.name}");
                        OnHoleLoaded(scene.name);
                        yield break;
                    }
                }

                if (expectedSceneName == null) break;  // no GameSession seed → original behaviour
                if (pollElapsed >= pollTimeoutSeconds) break;

                pollElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.Log("[PhysicsLab] No hole scene loaded at startup — flat-ground fallback.");
            SetupAtTee();
        }

        void Update()
        {
            // DIAG (2026-05-12): detect silent AeroCfg zeroing (e.g. domain reload that bypasses setter).
            float currentSpinRateRef = _aeroCfg.SpinRateReference.ToFloat();
            if (_aeroDiagPrevSpinRateRef >= 0f && currentSpinRateRef != _aeroDiagPrevSpinRateRef)
                Debug.LogError($"[AeroDiag][POLL] id={GetInstanceID()} SILENT CHANGE: SpinRateRef {_aeroDiagPrevSpinRateRef:F2}→{currentSpinRateRef:F2} frame={Time.frameCount} (no setter call between frames — domain reload / memory wipe suspected)");
            _aeroDiagPrevSpinRateRef = currentSpinRateRef;

            // §2a: tick the ball SM before camera orbit so that OnShotComplete fires
            // (and re-arms the controller) before HandleCameraOrbit reads IsExternalDragActive.
            bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;
            _ballSM?.Tick(isPlaying);
            HandleCameraOrbit();
        }

        // ── Putter UI ──────────────────────────────────────────────────────────

        [Header("Putter UI")]
        [SerializeField] private GameObject         _putterTrack;
        [SerializeField] private GameObject         _puttPathRoot;
        [SerializeField] private PutterGreenReader  _putterGreenReader;
        [SerializeField] private GameObject         _actionButtonRowTop;       // SpinButton GO
        [SerializeField] private GameObject         _actionButtonFadeDrawButton; // FadeDrawButton GO (sibling of SpinButton)
        [SerializeField] private CanvasGroup        _ballSelectorCanvasGroup;
        [SerializeField] private CentralBallWidget  _centralBall;
        [SerializeField] private PowerGaugeWidget   _powerGaugeWidget;
        [SerializeField] private HoleIndicatorWidget _holeIndicatorWidget;

        // ── Public accessors (used by PutterGreenReader + other viewers) ──────────

        public IGroundProvider  GetGround()   => BuildGroundProvider();
        public ISurfaceProvider GetSurfaces() => BuildSurfaceProvider(default(ShotPreset));
        // tree_aware_bot (Order 351): read-only exposure of the per-hole tree provider for bot
        // trunk-avoidance. Null on treeless holes / lab flat-ground. Read-side only — no sim change.
        public Golfin.Physics.ITreeObstacleProvider GetTreeProvider() => _treeProvider;

        // cup_capture_and_lipout: read-only accessor so diagnostics/tests can reproduce the
        // exact sim call this controller makes without reaching into private state.
        public Golfin.Physics.CupSpec GetCupSpec() => _cupSpec;

        // ── Public API ─────────────────────────────────────────────────────────

        // ── Putter mode API ────────────────────────────────────────────────────

        private void EnterPutterMode()
        {
            if (_shotConeView   != null) _shotConeView.SetPuttMode(true);
            if (_powerGaugeWidget != null)
            {
                _powerGaugeWidget.SetUnitMode(PowerGaugeWidget.DistanceUnit.Meters);
                _powerGaugeWidget.SetMaxPuttRangeMeters(ComputeMaxPuttRangeMeters());
            }
            if (_holeIndicatorWidget != null) _holeIndicatorWidget.SetUnitMode(HoleIndicatorWidget.DistanceUnit.Meters);
            var clubBtn = UnityEngine.Object.FindObjectOfType<ClubButtonWidget>();
            if (clubBtn != null) clubBtn.SetUnitMode(ClubButtonWidget.DistanceUnit.Meters);
            if (_putterTrack        != null) _putterTrack.SetActive(true);
            AlignPutterTrackToBall();
            if (_puttPathRoot       != null) _puttPathRoot.SetActive(true);
            if (_putterGreenReader  != null) _putterGreenReader.enabled = true;
            if (_actionButtonRowTop != null) _actionButtonRowTop.SetActive(false);
            if (_actionButtonFadeDrawButton != null) _actionButtonFadeDrawButton.SetActive(false);
            if (_ballSelectorCanvasGroup != null)
            {
                _ballSelectorCanvasGroup.alpha          = 0.5f;
                _ballSelectorCanvasGroup.interactable   = false;
                _ballSelectorCanvasGroup.blocksRaycasts = false;
            }
            if (_centralBall != null) _centralBall.SetPuttMode(true);
            // K11: publish putt mode so the club selector can gate its cards off the
            // SAME §2f decision that put us here (see ClubSelectionBroadcast.IsSelectable).
            ClubSelectionBroadcast.SetPutterMode(true, PutterIndex);
        }

        private void ExitPutterMode()
        {
            if (_shotConeView   != null) _shotConeView.SetPuttMode(false);
            if (_powerGaugeWidget != null)
            {
                _powerGaugeWidget.SetUnitMode(PowerGaugeWidget.DistanceUnit.Yards);
                // power_gauge_target_marker: seed the widget's carry fallback from the same
                // per-club authority the club button and map view use. Nothing had ever called
                // SetMaxCarryYards, so the yards readout sat on its 250f default regardless of
                // the selected club. The widget re-reads ClubContext live, so this only matters
                // for contexts where the bus is unpopulated — but a stale 250 is never right.
                int clubDist = Golfin.Gameplay.UI.HUD.ClubContext.SelectedDistance;
                if (clubDist > 0) _powerGaugeWidget.SetMaxCarryYards(clubDist);
            }
            if (_holeIndicatorWidget != null) _holeIndicatorWidget.SetUnitMode(HoleIndicatorWidget.DistanceUnit.Yards);
            var clubBtn = UnityEngine.Object.FindObjectOfType<ClubButtonWidget>();
            if (clubBtn != null) clubBtn.SetUnitMode(ClubButtonWidget.DistanceUnit.Yards);
            if (_putterTrack        != null) _putterTrack.SetActive(false);
            if (_puttPathRoot       != null) _puttPathRoot.SetActive(false);
            if (_putterGreenReader  != null) _putterGreenReader.enabled = false;
            if (_actionButtonRowTop != null) _actionButtonRowTop.SetActive(true);
            if (_actionButtonFadeDrawButton != null) _actionButtonFadeDrawButton.SetActive(true);
            if (_ballSelectorCanvasGroup != null)
            {
                _ballSelectorCanvasGroup.alpha          = 1f;
                _ballSelectorCanvasGroup.interactable   = true;
                _ballSelectorCanvasGroup.blocksRaycasts = true;
            }
            if (_centralBall != null) _centralBall.SetPuttMode(false);
            // K11: mirror of EnterPutterMode's publish.
            ClubSelectionBroadcast.SetPutterMode(false, PutterIndex);
        }

        /// <summary>
        /// Hides the shot-input aiming UI (aim cone/targeting line, power gauge, action buttons cluster,
        /// putter track) so they do not appear behind/over the WIN/LOSE/DRAW persistent banner.
        /// Called by VersusMatchController.MatchEnd() before ShowPersistent(). (DEFECT 1 fix — iter-7)
        ///
        /// iter-10b fix: hide only the shot-input children selectively — do NOT disable ShotUI_Canvas
        /// as a whole, because TurnBannerWidget is also a child of ShotUI_Canvas and must remain
        /// activeInHierarchy so ShowPersistent() can start coroutines. ConeRoot is hidden via both
        /// SetActive(false) AND a CanvasGroup alpha=0 as belt-and-suspenders against 1-frame flash.
        /// In the InCup/Draw path there is no code that re-enables ConeRoot after MatchEnd fires
        /// (no ReArm, no AnnounceTurn), so SetActive(false) is durable.
        /// </summary>
        public void HideShotUI()
        {
            // Hide ConeRoot (ShotConeView) — aim hex, targeting line, club handle, slab.
            // Belt-and-suspenders: SetActive(false) + CanvasGroup.alpha=0 to prevent 1-frame flash.
            if (_shotConeView != null)
            {
                _shotConeView.gameObject.SetActive(false);
                var cg = _shotConeView.GetComponent<UnityEngine.CanvasGroup>();
                if (cg == null) cg = _shotConeView.gameObject.AddComponent<UnityEngine.CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
            }

            // Hide PowerHUD (gauge + timing slab container).
            if (_powerGaugeWidget != null)
                _powerGaugeWidget.gameObject.SetActive(false);

            // Hide ActionButtons_Cluster (Spin, FadeDraw, Club buttons) — hide parent cluster.
            // _actionButtonRowTop is SpinButton which is a child of ActionButtons_Cluster.
            if (_actionButtonRowTop != null && _actionButtonRowTop.transform.parent != null)
                _actionButtonRowTop.transform.parent.gameObject.SetActive(false);
            else if (_actionButtonRowTop != null)
            {
                _actionButtonRowTop.SetActive(false);
                if (_actionButtonFadeDrawButton != null) _actionButtonFadeDrawButton.SetActive(false);
            }

            // Hide PutterTrack and PuttPathRoot.
            if (_putterTrack  != null) _putterTrack.SetActive(false);
            if (_puttPathRoot != null) _puttPathRoot.SetActive(false);

            Debug.Log($"[HideShotUI] Shot-input UI hidden. _shotConeView={(_shotConeView == null ? "NULL" : _shotConeView.gameObject.name + " activeSelf=" + _shotConeView.gameObject.activeSelf)}, _powerGaugeWidget={(_powerGaugeWidget == null ? "NULL" : _powerGaugeWidget.gameObject.activeSelf.ToString())}, _actionButtonRowTop={(_actionButtonRowTop == null ? "NULL" : _actionButtonRowTop.activeSelf.ToString())}");
        }

        // Aligns the putter track's top edge with the ball widget centre at runtime,
        // regardless of canvas resolution. Called each time putter mode is entered.
        private void AlignPutterTrackToBall()
        {
            if (_putterTrack == null || _centralBall == null) return;
            var trackRT = _putterTrack.GetComponent<RectTransform>();
            var ballRT  = _centralBall.GetComponent<RectTransform>();
            if (trackRT == null || ballRT == null) return;
            if (trackRT.parent is not RectTransform parentRT) return;

            var canvas  = parentRT.GetComponentInParent<Canvas>();
            Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                           ? canvas.worldCamera : null;
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, ballRT.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenPt, uiCam, out Vector2 localPt);
            // localPt is in parent's pivot-relative local space (origin at center).
            // anchoredPosition for anchor (0.5,1) is measured from the top edge, so subtract half height.
            trackRT.anchoredPosition = new Vector2(0f, localPt.y - parentRT.rect.height * 0.5f);
        }

        private float ComputeMaxPuttRangeMeters()
        {
            EnsureConfigsLoaded();
            var putter = PutterStats.DefaultPutter;
            var bundle = new Golfin.Physics.Stats.StatBundle(
                putter,
                Golfin.Physics.Stats.BallStats.Neutral,
                Golfin.Physics.Stats.CharacterStats.Neutral,
                fp.FromFloat(100f), fp.FromFloat(100f));
            var (input, ballMods) = Golfin.Physics.Stats.ShotInputBuilder.Build(
                bundle,
                Golfin.Physics.Stats.StatCoefficients.Default,
                Golfin.Physics.Stats.StatCaps.Default,
                fp.One,
                fp.Zero,
                fp.Zero, fp.Zero, fp.Zero,
                seed: 0u,
                baseVelocityOverrideMps: fp.FromFloat(5f)); // PuttBaseVelocityMps
            var traj = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero),
                AeroCfg, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green), SurfaceCfg,
                PuttCfg, ballMods);
            float dx = traj.finalPosition.x.ToFloat();
            float dz = traj.finalPosition.z.ToFloat();
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public void ResetToTee()
        {
            if (_shotController != null) _shotController.CompleteShot();
            SetupAtTee();
        }

        // Canonical club stats for touch-shot play. Values match ShotPresetCatalog reference shots.
        static readonly Golfin.Physics.Stats.ClubStats[] LabClubs =
        {
            new Golfin.Physics.Stats.ClubStats(50, 50, 50, 100, fp.FromFloat(10.9f),  fp.FromFloat(75f),  fp.FromFloat(2686f)), // Driver
            new Golfin.Physics.Stats.ClubStats(50, 50, 50, 100, fp.FromFloat(25.5f),  fp.FromFloat(51f),  fp.FromFloat(6500f)), // Iron 7
            new Golfin.Physics.Stats.ClubStats(50, 50, 50, 100, fp.FromFloat(41.2f),  fp.FromFloat(42f),  fp.FromFloat(9000f)), // Wedge
            new Golfin.Physics.Stats.ClubStats(50, 50, 50, 100, fp.FromFloat(5f),     fp.FromFloat(5f),   fp.FromFloat(0f)),    // Putter
        };
        public static readonly string[] LabClubLabels = { "Driver", "Iron 7", "Wedge", "Putter" };

        // §2f: Named constant for the putter index. Matches LabClubs.Length - 1.
        public static readonly int PutterIndex = LabClubs.Length - 1;

        [Header("Auto club selection (auto_club_selection)")]
        [Tooltip("Auto-pick the best club for each shot (driver on tee, distance-based after). Player can still override per shot.")]
        [SerializeField] bool _autoClubSelectEnabled = true;

        // §2f: Tracks the last non-putter club the player used.
        // Initialized in Awake to whatever the Inspector default CurrentClubIndex is
        // (typically Driver = 0). Updated on every SetClub(index != PutterIndex) call.
        // Used by auto-exit to revert from putter when the ball comes to rest off-green.
        int _lastNonPutterClubIndex = 0;

        public int CurrentClubIndex { get; private set; }
        public event System.Action<int> OnClubChanged;

        public void SetClub(int index)
        {
            if (_shotController == null || index < 0 || index >= LabClubs.Length) return;
            CurrentClubIndex = index;
            // §2f: remember the last non-putter selection for auto-exit.
            if (index != PutterIndex) _lastNonPutterClubIndex = index;
            bool isPutt = index == LabClubs.Length - 1;
            _shotController.IsPutt = isPutt;
            // NOTE: SetClub no longer injects a stat bundle (F1 — lab-vs-prod split).
            // Lab callers must call InjectLabBundleForCurrentClub() explicitly after SetClub().
            // Production callers (BotDriver.PlayHoleToCup, auto-revert) do NOT inject;
            // the StatProviderBus resolves live stats for every committed shot.
            //
            // NOTE stat_to_physics_mapping_audit Q3 (2026-05-25): keep bus club-index in
            // sync so DefaultStatProvider.BuildSwingBundle picks the right per-club defaults
            // on the FALLBACK path (no player stats armed).
            Golfin.Gameplay.Defaults.StatProviderBus.SetCurrentLabClubIndex(index);
            OnClubChanged?.Invoke(index);
            Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.Raise(index);
        }

        /// <summary>
        /// Builds the current-club neutral lab bundle and injects it into ShotController.
        /// Lab callers (lab UI, putter cone smoke, smoke runner, putter green reader bot
        /// scenarios) call this AFTER SetClub() when they want the lab-bundle behavior.
        /// Production-flow callers (BotDriver.PlayHoleToCup, auto-revert) must NOT call
        /// this — the StatProviderBus resolves live stats for committed shots.
        /// </summary>
        public void InjectLabBundleForCurrentClub()
        {
            if (_shotController == null) return;
            int index = CurrentClubIndex;
            bool isPutt = index == LabClubs.Length - 1;
            if (isPutt)
            {
                var putter = new Golfin.Physics.Stats.PutterStats(
                    50, 50, 50, 100,
                    LabClubs[index].LoftDegrees, LabClubs[index].BaseVelocityMps);
                _shotController.InjectStatBundle(new Golfin.Physics.Stats.StatBundle(
                    putter,
                    Golfin.Physics.Stats.BallStats.Neutral,
                    Golfin.Physics.Stats.CharacterStats.Neutral,
                    fp.FromFloat(100f), fp.FromFloat(100f)));
            }
            else
            {
                _shotController.InjectStatBundle(new Golfin.Physics.Stats.StatBundle(
                    LabClubs[index],
                    Golfin.Physics.Stats.BallStats.Neutral,
                    Golfin.Physics.Stats.CharacterStats.Neutral,
                    fp.FromFloat(100f), fp.FromFloat(100f)));
            }
        }

        // ── Setup ──────────────────────────────────────────────────────────────

        void SetupAtTee()
        {
            // _savedTeeWorldPos is [SerializeField] and survives Play Mode reload.
            // Use it when valid so we don't rely on the potentially-stale _ballSpawnPoint GO.
            Vector3 spRaw = _savedTeePosValid ? _savedTeeWorldPos
                          : (_ballSpawnPoint != null ? _ballSpawnPoint.position : Vector3.zero);
            Debug.Log($"[TeeDiag] SetupAtTee: _savedTeePosValid={_savedTeePosValid} _savedTeeWorldPos={_savedTeeWorldPos:F2} _ballSpawnPoint={(_ballSpawnPoint!=null?$"{_ballSpawnPoint.name}@{_ballSpawnPoint.position:F2}":"null")} -> spRaw={spRaw:F2}");
            if (!_savedTeePosValid && _ballSpawnPoint == null) return;

            // PutterGreenReader pulls its BakedZoneClassifier via GetSurfaces() on hole-load
            // (HoleContext.OnChanged); no RefreshProviders / SetBallTransform / SetCamera needed.
            Vector3 sp = spRaw;
            float surfaceY = SurfaceSnap(sp.x, sp.z, sp.y, 6); // 6 = Golfin.Course.SurfaceType.Tee
            Vector3 teePos = new Vector3(sp.x, surfaceY, sp.z);

            _orbitCenter = teePos;

            if (ballAnimator != null) ballAnimator.PlaceAtRest(teePos);
            Debug.Log($"[TeeDiag] SetupAtTee: placed ball at teePos={teePos:F2} (sp={sp:F2}, surfaceY={surfaceY:F2}) ballAnimator.CurrentBall.pos={(ballAnimator!=null && ballAnimator.CurrentBall!=null?ballAnimator.CurrentBall.position.ToString("F2"):"null")}");

            // Update ShotConeView ball transform so targeting line can pivot in Idle state.
            if (_shotConeView != null && ballAnimator != null)
                _shotConeView.SetBallTransform(ballAnimator.CurrentBall);

            // Update HoleIndicatorWidget ball transform after ball is placed
            var holeWidgetForTee = FindObjectOfType<Golfin.Gameplay.UI.ShotUI.HoleIndicatorWidget>();
            if (holeWidgetForTee != null && ballAnimator != null)
                holeWidgetForTee.SetBallTransform(ballAnimator.CurrentBall);

            Vector3 lookDir = GetDefaultLookDirection();
            _cameraYaw = Mathf.Atan2(lookDir.z, lookDir.x);

            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            // Commit the camera transform NOW (2026-05-12 fix). Without this, the camera
            // stays at whatever LabScaffold's serialized default was — typically near
            // Hole 1's tee — until the user click-swipes to trigger HandleCameraOrbit.
            // Symptom: ball appears "under the terrain" on any hole other than Hole 1
            // because the user is looking across the course at the distant new tee.
            // ApplyCameraYaw uses the same math HandleCameraOrbit uses, so this is the
            // same teleport the user gets from a click-swipe — just done up front.
            Camera teeCamForApply = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null;
            if (teeCamForApply != null) ApplyCameraYaw(teeCamForApply);

            // auto_club_selection: stroke 1 explicitly selects the bag's DRIVER rather than
            // trusting "bag index 0 happens to be a driver". Guarded on IsHoleReady so the
            // flat-ground / no-hole fallback path (Start coroutine, PresetScene.Range) keeps
            // today's behaviour exactly. The bag can populate a frame later than this runs,
            // hence the OnBagChanged one-shot inside AutoSelectClubAtHoleStart.
            if (IsHoleReady) AutoSelectClubAtHoleStart();
        }

        // Teleport the ball to a world position (Y resolved via type-aware downward raycast).
        // preferredSurfaceTypeValue: Golfin.Course.SurfaceType int value (1=Green, 4=Bunker, etc.) or null.
        // One-shot placement; subsequent shots continue from wherever the ball lands.
        public void PlaceBallAt(Vector3 worldPos, int? preferredSurfaceTypeValue = null)
        {
            RepositionBallWithLookDir(worldPos, preferredSurfaceTypeValue, GetDefaultLookDirection());
        }

        /// <summary>
        /// Places the ball at an airborne world position (no surface snap) with an
        /// optional initial velocity hint (stored for diagnostics only — velocity is
        /// supplied separately to HandleShotResolvedForTests via ShotInput).
        ///
        /// Used by SurfaceRolloutHarness for above-surface drop tests; production gameplay
        /// never spawns balls airborne, so the placement-snap pipeline is unaffected.
        ///
        /// The override is single-shot: GetCurrentOrigin() consumes it on the very next
        /// HandleShotResolved call, then falls back to the normal surface-snap path.
        /// </summary>
        internal void PlaceBallAtAirborne(Vector3 worldPos)
        {
            // Place the visual ball at the exact airborne position (no SurfaceSnap).
            if (_shotController != null) _shotController.CompleteShot();
            if (ballAnimator != null) ballAnimator.PlaceAtRest(worldPos);

            // Store the override so the sim uses this exact Y instead of snapping to surface.
            _airborneOriginOverride = new fp3(
                fp.FromFloat(worldPos.x),
                fp.FromFloat(worldPos.y),
                fp.FromFloat(worldPos.z));
        }

        // §2e: private helper that owns the ball-placement + camera-yaw logic.
        // PlaceBallAt delegates here with GetDefaultLookDirection(); the OB-drop
        // path calls this directly with the pin-facing direction.
        void RepositionBallWithLookDir(Vector3 worldPos, int? preferredSurfaceTypeValue, Vector3 lookDir)
        {
            if (_shotController != null) _shotController.CompleteShot();

            float y   = SurfaceSnap(worldPos.x, worldPos.z, worldPos.y, preferredSurfaceTypeValue);
            Vector3 pos = new Vector3(worldPos.x, y, worldPos.z);

            _orbitCenter = pos;
            if (ballAnimator != null) ballAnimator.PlaceAtRest(pos);

            // Update ShotConeView ball transform so targeting line can pivot from the new position.
            if (_shotConeView != null && ballAnimator != null)
                _shotConeView.SetBallTransform(ballAnimator.CurrentBall);
            // PutterGreenReader reads ball position via BallPosition each frame — no manual sync needed.

            _cameraYaw = Mathf.Atan2(lookDir.z, lookDir.x);
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            // Commit the camera transform NOW (same fix as SetupAtTee — see notes there).
            Camera placeCamForApply = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null;
            if (placeCamForApply != null) ApplyCameraYaw(placeCamForApply);

            AdjustCameraForDepression(pos);

            // K11 follow-up: a repositioned ball must re-run the §2f decision.
            ReDecideClubAfterReposition(pos);
        }

        /// <summary>
        /// Re-runs the §2f auto-switch after a REPOSITION (OB/water drop, PlaceBallAt).
        ///
        /// DecideTargetClub is otherwise only reachable from the AtRest branch of
        /// HandleShotComplete, so a ball that is MOVED rather than STOPPED kept whatever
        /// club it had: dropped onto the green still holding a wood, dropped off the green
        /// still in putter mode. Since the K11 selector gate reads putt mode, a stale flag
        /// also gated the selector the wrong way round — leaving the player unable to
        /// correct it by hand (putter-only in the rough, putter greyed out on the green).
        ///
        /// Classifies via the SAME baked classifier the sim uses to produce EndSurface, so
        /// this decision and §2f's cannot disagree (the K11 single-rule principle). When no
        /// zones are baked (flat-ground lab presets) the club is left alone — today's
        /// behaviour — rather than forcing a switch off a constant surface.
        ///
        /// Runs before the callers' ReArm(), mirroring the AtRest branch's SetClub→ReArm order.
        /// </summary>
        void ReDecideClubAfterReposition(Vector3 pos)
        {
            // §2f runs only when zones are baked; with none (flat-ground lab presets) the club is
            // left alone — today's behaviour — rather than forcing a switch off a constant surface.
            if (_bakedClassifier != null)
            {
                SurfaceType surface = _bakedClassifier.Classify(fp.FromFloat(pos.x), fp.FromFloat(pos.z));
                int target = PutterModeSurfaceController.DecideTargetClub(
                    currentClubIndex: CurrentClubIndex,
                    putterIndex: PutterIndex,
                    endSurface: surface,
                    lastNonPutterClubIndex: _lastNonPutterClubIndex);

                if (target >= 0)   // target < 0 = idempotent: already on the right club
                {
                    Debug.Log($"[PhysicsLab][§2f] Reposition surface={surface} " +
                              $"auto-switch club {CurrentClubIndex}→{target}");
                    SetClub(target);
                    // PROD path: clear any lab bundle leftover so the bus resolves live stats.
                    _shotController?.ClearStatBundleOverride();
                }
            }

            // auto_club_selection: the new lie gets a fresh pick — including a stroke-and-distance
            // drop back on the tee, which BallIsOnTee() turns into the Driver again. Runs AFTER the
            // §2f block (and unconditionally, including §2f's "no change" case) so the green rule
            // always wins: the helper no-ops while putter mode is on.
            AutoSelectClubForNextShot();
        }

        // ── auto_club_selection ────────────────────────────────────────────────

        // True while we are waiting for the equipped bag to arrive so the TEE pick can run.
        // ClubContextPopulator / LabInventoryStub can populate ClubContext a frame (or more)
        // after SetupAtTee, so the tee pick arms a one-shot OnBagChanged re-run when the bag
        // is still empty. Guarded so we never stack duplicate subscriptions.
        bool _autoClubAwaitingBag;

        /// <summary>
        /// auto_club_selection: pre-selects the club for the NEXT shot from the equipped bag.
        ///
        /// Runs AFTER the §2f decision at every call site, so the green rule always wins:
        /// <see cref="AutoClubSelector"/> returns -1 while §2f has the player in putter mode,
        /// making this a no-op on the green. Off the green it picks the driver on the tee and
        /// the shortest club that still reaches the pin everywhere else — never the driver.
        ///
        /// Commits through the SAME pair the selector overlay's card tap uses
        /// (SelectorOverlayWidget): RequestSelection keeps ClubContext / the live-stat path
        /// correct (Order 762), Raise reaches OnClubBroadcastReceived → SetClub for the lab
        /// index. A bare SetClub would reintroduce the §2f ClubContext gap for full shots.
        /// </summary>
        void AutoSelectClubForNextShot()
        {
            if (!_autoClubSelectEnabled) return;

            var bag = Golfin.Gameplay.UI.HUD.ClubContext.EquippedBag;
            Vector3 ball = BallPosition;
            Vector3 pin  = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
            float   distM = new Vector2(pin.x - ball.x, pin.z - ball.z).magnitude;
            bool    onTee = BallIsOnTee();

            int bagIdx = AutoClubSelector.SelectBestClub(
                distM,
                onTee,
                Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.InPutterMode,
                bag, PutterIndex);

            if (bagIdx < 0 || bag == null || bagIdx >= bag.Count) return;

            // Idempotent: both the bag selection AND the lab club already match.
            if (bagIdx == Golfin.Gameplay.UI.HUD.ClubContext.SelectedIndex
                && bag[bagIdx].LabClubIndex == CurrentClubIndex) return;

            Debug.Log($"[PhysicsLab][auto_club] dist={distM:F1}m tee={onTee} → " +
                      $"bag[{bagIdx}] '{bag[bagIdx].ClubId}' (labIdx={bag[bagIdx].LabClubIndex})");

            Golfin.Gameplay.UI.HUD.ClubContext.RequestSelection(bagIdx);
            Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.Raise(bag[bagIdx].LabClubIndex);
            // PROD path: clear any lab bundle leftover so the bus resolves live stats.
            _shotController?.ClearStatBundleOverride();
        }

        /// <summary>
        /// Hole-start variant of <see cref="AutoSelectClubForNextShot"/>: if the equipped bag
        /// has not been populated yet, re-run once when it arrives (ClubContext.OnBagChanged).
        /// </summary>
        void AutoSelectClubAtHoleStart()
        {
            if (!_autoClubSelectEnabled) return;

            var bag = Golfin.Gameplay.UI.HUD.ClubContext.EquippedBag;
            if (bag == null || bag.Count == 0)
            {
                if (_autoClubAwaitingBag) return;
                _autoClubAwaitingBag = true;
                Golfin.Gameplay.UI.HUD.ClubContext.OnBagChanged += HandleBagReadyForTeePick;
                Debug.Log("[PhysicsLab][auto_club] bag empty at hole start — waiting for ClubContext.OnBagChanged.");
                return;
            }

            AutoSelectClubForNextShot();
        }

        void HandleBagReadyForTeePick()
        {
            Golfin.Gameplay.UI.HUD.ClubContext.OnBagChanged -= HandleBagReadyForTeePick;
            _autoClubAwaitingBag = false;
            AutoSelectClubForNextShot();
        }

        // Update the HUD max-carry yards readout for the current StatBundle/club.
        public void RecomputeMaxCarry()
        {
            if (_shotConeView != null)
                _shotConeView.SetMaxCarryYards(ComputeMaxCarryYards());
        }

        // Auto-derive look direction from scene type when not set in Inspector.
        Vector3 GetDefaultLookDirection()
        {
            // When a hole is loaded via LabHoleBinder, compute direction tee → green centroid.
            if (_useSceneProviders && _ballSpawnPoint != null && _greenCentroidValid)
            {
                Vector3 dir = _loadedHoleGreenCentroid - _ballSpawnPoint.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) return dir.normalized;
            }

            // Legacy Hole1 hardcoded direction (PhysicsLab_Hole1 scene, PresetScene.Hole1).
            if (currentScene == PresetScene.Hole1)
                return new Vector3(-0.967f, 0f, -0.253f);

            if (_defaultLookDirection.sqrMagnitude > 0.001f)
                return new Vector3(_defaultLookDirection.x, 0f, _defaultLookDirection.z).normalized;

            return Vector3.right; // aimYaw=0 = +X default
        }

        public void SetReleaseToFire(bool value)
        {
            var dragger = GetComponentInChildren<ClubHandleDragger>(true);
            if (dragger != null) dragger.ReleaseToFire = value;
        }

        public bool GetReleaseToFire()
        {
            var dragger = GetComponentInChildren<ClubHandleDragger>(true);
            return dragger != null && dragger.ReleaseToFire;
        }

        /// <summary>
        /// §2e smoke runner test seam: overrides _cameraYaw so the next Fire() call
        /// sends the ball in the specified direction (radians, XZ plane, Atan2 convention).
        /// Only call from smoke runners / Editor test tools — never from production code.
        /// </summary>
        internal void SetCameraYawRadians(float yawRadians)
        {
            _cameraYaw = yawRadians;
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;
        }

        /// <summary>
        /// Bot / video-capture seam: simulate one frame of a sideways orbit drag through the
        /// PRODUCTION orbit path — identical to <see cref="HandleCameraOrbit"/>'s write
        /// (<c>_cameraYaw += Δ; ApplyCameraYaw(cam)</c>) AND its Chase-mode gate. Returns true
        /// if the orbit applied (mode == Chase), false if gated out. Used by ObRecoveryCaptureBot
        /// (K10) to prove on video that the aim phase AFTER an OB is draggable rather than wedged:
        /// on HEAD the mode is stuck in OBFreeze so this returns false (can't drag — symptom 3);
        /// with the fix the mode is Chase so the camera pans. Not on any production input path.
        /// </summary>
        internal bool SimulateOrbitDragDegrees(float deltaDegrees)
        {
            if (chaseCamera != null && chaseCamera.CurrentMode != ChaseCamera.Mode.Chase)
                return false; // orbit only works in Chase — the exact gate HandleCameraOrbit uses
            _cameraYaw += deltaDegrees * Mathf.Deg2Rad;
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;
            Camera cam = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null;
            if (cam != null) ApplyCameraYaw(cam);
            return true;
        }

        /// <summary>
        /// Bot seam: fire a shot through the PRODUCTION ShotController path so the full
        /// shot-UI lifecycle runs (Idle → Aiming → Timing → Flicking → Resolving).
        ///
        /// This causes ShotConeView to transition to Resolving, which hides the cone,
        /// ball widget, club handle, and putter track — exactly as a real player shot does.
        /// Club selection and camera heading must be set BEFORE calling this (via SetClub +
        /// SetCameraYawRadians). The StatBundle injected by SetClub drives the velocity.
        ///
        /// power01: 0–1.0 (1.0 = 100% of club base velocity).
        /// accuracy: Green = perfect aim, Yellow = mild degradation, Red = severe.
        ///
        /// Only call from bot / smoke-runner code — never from production paths.
        /// </summary>
        internal void FireViaShotController(float power01, Golfin.Gameplay.Input.DebugShotAccuracy accuracy = Golfin.Gameplay.Input.DebugShotAccuracy.Green, float coneFinetune = 0f)
        {
            if (_shotController == null)
            {
                Debug.LogWarning("[PhysicsLab] FireViaShotController: _shotController is null — falling back to FireInternal with default preset.");
                return;
            }
            // Push current SpinContext value to ShotController before firing.
            // (Golfin.Gameplay.Input cannot reference Golfin.Gameplay.UI directly — circular dep —
            // so PhysicsLabController, which refs both, bridges the two.)
            _shotController.PendingSpinInput = Golfin.Gameplay.UI.HUD.SpinContext.Spin;
            // FireDebugShot drives ShotController through the full production path:
            // Idle → Flicking → Resolving → OnShotResolved → HandleShotResolved → SM.
            // ShotConeView subscribes to OnStateChanged, so it sees Resolving and hides UI.
            // coneFinetune carries fade/draw shaping (applied only when FadeDrawActive).
            _shotController.FireDebugShot(power01, accuracy, coneFinetune);
        }

        // ── Camera orbit ───────────────────────────────────────────────────────

        void HandleCameraOrbit()
        {
            // Block orbit while any action-button selector overlay is open.
            if (Golfin.Gameplay.UI.ShotUI.OtherButtonsFader.AnyOverlayOpen)
            {
                _orbitDragActive = false;
                return;
            }

            if (_shotController != null && _shotController.IsExternalDragActive) return;

            // Orbit only makes sense in Chase mode; Overhead/Ground manage themselves.
            if (chaseCamera != null && chaseCamera.CurrentMode != ChaseCamera.Mode.Chase) return;

            // When ball animation finishes (falling edge of isPlaying), update the orbit center
            // to the resting ball position so subsequent panning orbits around the new position.
            bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;
            if (_prevBallPlaying && !isPlaying)
            {
                if (ballAnimator?.CurrentBall != null)
                    _orbitCenter = ballAnimator.CurrentBall.position;
            }
            _prevBallPlaying = isPlaying;
            if (isPlaying) return;

            // Read the active pointer: touch on device, mouse in the editor. Mouse.current
            // is null on a physical iPhone, so the old mouse-only path returned here every
            // frame and the camera orbit was dead on device (worked in-editor with a mouse).
            // Prefer an active touch; fall back to the mouse. The over-UI gate uses the
            // touch's fingerId so dragging ON the club handle still suppresses the orbit.
            var mouse = Mouse.current;
            var touch = Touchscreen.current != null ? Touchscreen.current.primaryTouch : null;
            bool touchActive = touch != null && touch.press.isPressed;

            if (!touchActive && mouse == null)
            {
                _orbitDragActive = false;
                return;
            }

            bool  pressing;
            float dx;
            bool  overUI;
            if (touchActive)
            {
                pressing = true;
                dx       = touch.delta.x.ReadValue();
                overUI   = EventSystem.current != null &&
                           EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue());
            }
            else
            {
                pressing = mouse.leftButton.isPressed;
                dx       = mouse.delta.x.ReadValue();
                overUI   = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            }

            if (pressing && !_orbitDragActive)
            {
                if (overUI) return;
                _orbitDragActive = true;
            }
            if (!pressing)
            {
                _orbitDragActive = false;
                return;
            }

            if (Mathf.Abs(dx) < 0.5f) return;

            _cameraYaw += dx * _orbitSensitivity * Mathf.Deg2Rad;

            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            Camera cam = chaseCamera?.GetComponent<Camera>();
            if (cam != null) ApplyCameraYaw(cam);
        }

        // Pre-§2b: ChaseCamera owns position only when _target != null (Flying/Rolling).
        // During Aiming when Director has cleared _target, ApplyCameraYaw writes the camera
        // transform directly. Two writers don't conflict because each gates on a different
        // condition (target null vs ball not playing).
        /// <summary>
        /// Editor / smoke-bot seam: frame <paramref name="cam"/> on an explicit ball position and
        /// heading using the PRODUCTION aim framing. Exists so bot scenarios never re-derive the
        /// camera math — <see cref="ApplyCameraYaw"/> is the single implementation. (Before
        /// aim_camera_ball_centering, BotDriver carried its own copy of the legacy 8/3 lines and
        /// silently drifted out of sync, so bot clips framed shots differently than real players saw.)
        /// Sets the same state the production path would hold at rest: orbit centre = ball, yaw =
        /// heading, and the ShotController heading so a subsequent Fire() goes the same way.
        /// Only call from smoke runners / Editor test tools — never from production code.
        /// </summary>
        internal void ApplyAimCameraAt(Camera cam, Vector3 ballPos, float yawRadians)
        {
            if (cam == null) return;
            _orbitCenter = ballPos;
            _cameraYaw   = yawRadians;
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;
            ApplyCameraYaw(cam);
        }

        // `internal` (was private) so BotDriver can reuse the one framing implementation rather than
        // duplicating it. MapViewController reaches this by reflection with Public|NonPublic|Instance,
        // which still binds — internal is NonPublic to reflection.
        internal void ApplyCameraYaw(Camera cam)
        {
            Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));

            // Putter aim: same viewport pin as the full swing, but at its OWN distance/height.
            // (Cesar, 2026-08-10 — the spec had scoped putting out and kept the legacy pose
            // verbatim; the ball then sat ~62% down screen instead of under the 2D ball.)
            // _puttCamDistanceM/_puttCamHeightM default to the legacy 8/3 on purpose: the putt
            // view has to fit the 15 m aim line and the green-reading grid, so this pass changes
            // WHERE the ball sits on screen, not how much green you can see.
            if (CurrentShotIsPutt)
            {
                SolveAimCameraPose(
                    _orbitCenter, lookDir, _puttCamDistanceM, _puttCamHeightM,
                    cam.fieldOfView, GetAimBallViewportY(),
                    out Vector3 puttPos, out Quaternion puttRot);
                cam.transform.SetPositionAndRotation(puttPos, puttRot);
                return;
            }

            // Full-swing aim: pin the 3D ball to the 2D CentralBallWidget's viewport point and
            // close to _aimCamDistanceM, pulling back only as far as tee-marker visibility demands.
            SolveAimCameraPose(
                _orbitCenter,
                lookDir,
                ComputeAimDistance(lookDir, cam),
                _aimCamHeightM,
                cam.fieldOfView,
                GetAimBallViewportY(),
                out Vector3 camPos,
                out Quaternion camRot);

            cam.transform.SetPositionAndRotation(camPos, camRot);
        }

        /// <summary>
        /// Viewport Y (0 = bottom, 1 = top) the 3D ball must project at during full-swing aim,
        /// read from the live <see cref="CentralBallWidget"/> rect.
        ///
        /// Computed in the ROOT CANVAS's own rect space rather than via screen pixels: the canvas
        /// rect maps 1:1 onto the camera viewport for both Screen Space – Overlay and
        /// Screen Space – Camera, so this needs no render-mode branch, and it sidesteps
        /// Screen.height reporting the Game View window size rather than the render height in
        /// Editor play mode. Falls back to <see cref="_aimBallViewportYFallback"/> when the widget
        /// is unwired or not under a canvas.
        /// </summary>
        float GetAimBallViewportY()
        {
            if (_centralBallWidget == null) return _aimBallViewportYFallback;

            RectTransform rect = _centralBallWidget.Rect;
            if (rect == null) return _aimBallViewportYFallback;

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null || canvasRect.rect.height <= 0f) return _aimBallViewportYFallback;

            float localY = canvasRect.InverseTransformPoint(rect.position).y;
            float vy     = (localY - canvasRect.rect.yMin) / canvasRect.rect.height;

            // Guard against a mis-parented / off-canvas widget producing an absurd pitch.
            return (vy > 0.02f && vy < 0.98f) ? vy : _aimBallViewportYFallback;
        }

        /// <summary>
        /// True when the ball is sitting on the tee (stroke 1). Uses the same
        /// "within 1 m of the cached tee midpoint" convention the rest of the tee logic uses —
        /// there is no stroke counter on this controller or on GameSession to key off.
        /// </summary>
        bool BallIsOnTee()
        {
            if (!_savedTeePosValid) return false;

            Vector3 ballPos = ballAnimator?.CurrentBall != null
                ? ballAnimator.CurrentBall.position
                : _orbitCenter;

            float dx = ballPos.x - _savedTeeWorldPos.x;
            float dz = ballPos.z - _savedTeeWorldPos.z;
            return (dx * dx + dz * dz) < 1f;   // 1 m radius
        }

        /// <summary>
        /// Camera distance behind the ball for full-swing aim: <see cref="_aimCamDistanceM"/>,
        /// pulled back only as far as keeping every tee marker on screen requires (tee shots only),
        /// capped at <see cref="_aimCamMaxDistanceM"/>.
        /// </summary>
        float ComputeAimDistance(Vector3 lookDir, Camera cam)
        {
            if (cam == null || !BallIsOnTee() || _teeMarkerPositions.Count == 0)
                return _aimCamDistanceM;

            return SolveAimDistance(
                _orbitCenter, lookDir, _teeMarkerPositions,
                _aimCamDistanceM, _aimCamMaxDistanceM,
                cam.fieldOfView, cam.aspect, _teeMarkerSafeFrac);
        }

        /// <summary>
        /// Pure solver (no scene state — unit-testable): places the camera <paramref name="distanceM"/>
        /// behind and <paramref name="heightM"/> above <paramref name="ballPos"/> along
        /// <paramref name="lookDirXZ"/>, and pitches it so <paramref name="ballPos"/> projects at
        /// viewport (0.5, <paramref name="targetViewportY"/>).
        ///
        /// Derivation: the ball sits atan(h/d) below the camera's horizontal. A point α below the
        /// optical axis projects at viewport Y = 0.5 − 0.5·tan(α)/tan(fovV/2), so the required
        /// offset below the axis is α = atan((1 − 2·vy)·tan(fovV/2)) and pitch = atan(h/d) − α.
        /// Camera position and ball stay colinear in the XZ look direction, so viewport X = 0.5
        /// falls out for free.
        /// </summary>
        internal static void SolveAimCameraPose(
            Vector3 ballPos,
            Vector3 lookDirXZ,
            float   distanceM,
            float   heightM,
            float   verticalFovDeg,
            float   targetViewportY,
            out Vector3    camPos,
            out Quaternion camRot)
        {
            Vector3 lookDir = new Vector3(lookDirXZ.x, 0f, lookDirXZ.z);
            lookDir = lookDir.sqrMagnitude > 1e-8f ? lookDir.normalized : Vector3.forward;

            float d  = Mathf.Max(0.01f, distanceM);
            float h  = heightM;
            float vy = Mathf.Clamp(targetViewportY, 0.02f, 0.98f);

            camPos = ballPos - lookDir * d + Vector3.up * h;

            float tanHalfV  = Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            float thetaOff  = Mathf.Atan((1f - 2f * vy) * tanHalfV);   // rad below view centre
            float pitchDown = Mathf.Atan2(h, d) - thetaOff;            // rad, + = nose down
            float yawDeg    = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;

            camRot = Quaternion.Euler(pitchDown * Mathf.Rad2Deg, yawDeg, 0f);
        }

        /// <summary>
        /// Pure solver (no scene state — unit-testable): smallest camera distance ≥
        /// <paramref name="baseDistanceM"/> that keeps every marker's horizontal projection inside
        /// <paramref name="safeFrac"/> of the half-screen width, capped at <paramref name="maxDistanceM"/>.
        ///
        /// Closed form: a marker at lateral offset L and along-track offset A needs
        /// L / (d + A) ≤ tan(fovH/2)·safeFrac ⇒ d ≥ L / (tan(fovH/2)·safeFrac) − A.
        /// Uses (d + A) as the view depth, which is slightly conservative — the real depth is
        /// (d + A)·cos(pitch) + h·sin(pitch), i.e. larger — so the result never under-pulls.
        /// </summary>
        internal static float SolveAimDistance(
            Vector3 ballPos,
            Vector3 lookDirXZ,
            System.Collections.Generic.IReadOnlyList<Vector3> markerPositions,
            float   baseDistanceM,
            float   maxDistanceM,
            float   verticalFovDeg,
            float   aspect,
            float   safeFrac)
        {
            float d = baseDistanceM;
            if (markerPositions == null || markerPositions.Count == 0) return d;

            Vector3 lookDir = new Vector3(lookDirXZ.x, 0f, lookDirXZ.z);
            lookDir = lookDir.sqrMagnitude > 1e-8f ? lookDir.normalized : Vector3.forward;

            float tanHalfV = Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            float tanHalfH = tanHalfV * Mathf.Max(0.01f, aspect);
            float frac     = Mathf.Clamp(safeFrac, 0.05f, 1f);
            float denom    = tanHalfH * frac;
            // Degenerate FOV/aspect: nothing fits on screen — fall back to the pull-back ceiling.
            if (denom <= 1e-5f) return maxDistanceM;

            Vector3 right = new Vector3(lookDir.z, 0f, -lookDir.x);   // XZ perpendicular

            for (int i = 0; i < markerPositions.Count; i++)
            {
                Vector3 rel     = markerPositions[i] - ballPos;
                float   lateral = Mathf.Abs(Vector3.Dot(rel, right));
                float   along   = Vector3.Dot(rel, lookDir);          // + = ahead of the ball
                float   dNeeded = lateral / denom - along;
                if (dNeeded > d) d = dNeeded;
            }

            return Mathf.Min(d, maxDistanceM);
        }

        // ── Preset firing ──────────────────────────────────────────────────────

        public void Fire(ShotPreset preset)
        {
            _previousTrajectory = null;
            FireInternal(preset);
        }

        public void FireCompare(ShotPreset preset)
        {
            if (_predictionVisible && _previousTrajectory != null)
                trajectoryRenderer.SetGhost(true);
            FireInternal(preset);
        }

        public void FireRepeatability(ShotPreset preset, int count = 5)
        {
            var positions = new fp3[count];
            for (int i = 0; i < count; i++)
                positions[i] = RunSimForCamera(preset).finalPosition;

            bool bitExact = true;
            for (int i = 1; i < count; i++)
            {
                if (positions[i].x.ToFloat() != positions[0].x.ToFloat() ||
                    positions[i].y.ToFloat() != positions[0].y.ToFloat() ||
                    positions[i].z.ToFloat() != positions[0].z.ToFloat())
                { bitExact = false; break; }
            }

            Debug.Log($"[PhysicsLab] Fire×{count}: {(bitExact ? "✓ BIT-EXACT" : "✗ DRIFT DETECTED")}");
            OnRepeatabilityResult?.Invoke(bitExact, count);

            var last = RunSimForCamera(preset);
            if (_predictionVisible) trajectoryRenderer.Draw(last);
            ballAnimator.Play(last);
        }

        public void Clear()
        {
            trajectoryRenderer.Clear();
            _previousTrajectory = null;
        }

        public void TogglePrediction()
        {
            _predictionVisible = !_predictionVisible;
            if (_predictionVisible && _previousTrajectory != null)
                trajectoryRenderer.Draw(_previousTrajectory);
            else
                trajectoryRenderer.Clear();
        }

        public void ReloadConfigs()
        {
            AeroCfg    = PhysicsConfigLoader.LoadAeroConfig();
            WindCfg    = PhysicsConfigLoader.LoadWindConfig();
            SurfaceCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            PuttCfg    = PhysicsConfigLoader.LoadPuttConfig();
        }

        public void ResetToDefaults()
        {
            AeroCfg    = AeroConfig.Default;
            WindCfg    = WindConfig.Calm;
            SurfaceCfg = SurfaceConfig.Default;
            PuttCfg    = PuttConfig.Default;
        }

        public void SetAeroConfig(AeroConfig cfg)       => AeroCfg    = cfg;
        public void SetSurfaceConfig(SurfaceConfig cfg) => SurfaceCfg = cfg;
        public void SetPuttConfig(PuttConfig cfg)       => PuttCfg    = cfg;
        public void SetWindConfig(WindConfig cfg)       => WindCfg    = cfg;

        // ── Touch shot integration ─────────────────────────────────────────────

        void HandleShotResolved(ShotInput input, BallPhysicsModifiers ballMods)
        {
            DiagAero("HandleShotResolved.start");
            fp3 ballOrigin = GetCurrentOrigin(fallbackToInput: input.origin);
            var correctedInput = new ShotInput(ballOrigin, input.velocity, input.maxDuration, input.Spin, input.seed);

            var trajectory = RunSimFromController(correctedInput, ballMods);
            _previousTrajectory = trajectory;

            // ── §controls_h: cache origin + launchDir BEFORE SM transition fires ────
            // Director's ArmChaseForShot reads LastShotOrigin/LastShotLaunchDir on the
            // synchronous Aiming→Flying transition. They MUST be fresh before OnTrajectoryComputed.
            var s0 = trajectory.samples != null && trajectory.samples.Count > 0
                ? trajectory.samples[0].position : correctedInput.origin;
            Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
            Vector3 launchDir = new Vector3(correctedInput.velocity.x.ToFloat(), 0f,
                                             correctedInput.velocity.z.ToFloat()).normalized;
            if (launchDir == Vector3.zero) launchDir = Vector3.right;

            _orbitCenter       = origin;
            _lastShotOrigin    = origin;
            _lastShotLaunchDir = launchDir;

            // ── §controls_h: spawn the new ball BEFORE SM transition fires ──────────
            // BallAnimator.Play() destroys the previous ball Transform and creates a new one.
            // The Director's ArmChaseForShot reads CurrentBall during the synchronous SM
            // transition — it MUST see the post-Play() Transform, not the pre-Play() one
            // that's about to be destroyed.
            if (_predictionVisible) trajectoryRenderer?.Draw(trajectory);
            ballAnimator.Play(trajectory);

            // ── §2a: now feed the SM. Director sees fresh cache + fresh ball. ───────
            _ballSM?.OnTrajectoryComputed(correctedInput.origin, trajectory, AeroCfg.BallRadius);

            if (_shotConeView != null && ballAnimator?.CurrentBall != null)
                _shotConeView.SetBallTransform(ballAnimator.CurrentBall);
            // PutterGreenReader reads ball position via BallPosition each frame — no manual sync needed.

            // Update HoleIndicatorWidget ball transform after shot resolves
            if (ballAnimator?.CurrentBall != null)
            {
                var holeWidgetShot = FindObjectOfType<Golfin.Gameplay.UI.ShotUI.HoleIndicatorWidget>();
                if (holeWidgetShot != null) holeWidgetShot.SetBallTransform(ballAnimator.CurrentBall);
            }

            float carryM = 0f;
            SurfaceType finalSurface = SurfaceType.Fairway;
            if (trajectory.terrainHits != null && trajectory.terrainHits.Count > 0)
            {
                carryM       = XZDist(correctedInput.origin, trajectory.terrainHits[0].Position);
                finalSurface = trajectory.terrainHits[trajectory.terrainHits.Count - 1].Surface;
            }
            float totalM  = XZDist(correctedInput.origin, trajectory.finalPosition);
            float peakY   = 0f;
            float originY = correctedInput.origin.y.ToFloat();
            foreach (var s in trajectory.samples)
            {
                float y = s.position.y.ToFloat() - originY;
                if (y > peakY) peakY = y;
            }
            int bounceCount = 0;
            if (trajectory.terrainHits != null)
                foreach (var h in trajectory.terrainHits)
                    if (!h.IsStop) bounceCount++;

            var readout = new ShotReadout
            {
                PresetDisplayName  = "[Touch Shot]",
                CarryMeters        = carryM,
                TotalMeters        = totalM,
                MaxHeightMeters    = peakY,
                BounceCount        = bounceCount,
                TerminationReason  = trajectory.termination.ToString(),
                FinalSurface       = finalSurface,
                SimDurationSeconds = trajectory.finalTime.ToFloat(),
            };
            OnShotFired?.Invoke(readout);
            LogReadout(readout);
        }

        /// <summary>
        /// §controls_h: Test seam. Exposes the private HandleShotResolved for integration tests
        /// in Golfin.Physics.Tests without requiring a full scene setup. Tests must wire
        /// ballAnimator, _ballSM, and configs before calling this.
        /// </summary>
        internal void HandleShotResolvedForTests(ShotInput input, BallPhysicsModifiers ballMods)
            => HandleShotResolved(input, ballMods);

        /// <summary>
        /// §2a: Called by BallStateMachine when a shot reaches a terminal state (AtRest, InCup, OB).
        /// §2e: extended with pin-aim rotation on AtRest and OB drop + teleport + penalty stroke.
        /// §2d will gate InCup on HoleCompleteDriver modal close (unchanged).
        /// </summary>
        void HandleShotComplete(Golfin.Gameplay.Loop.ShotResult result)
        {
            Debug.Log($"[PhysicsLab][§2a] OnShotComplete: terminal={result.TerminalState}" +
                      (result.OBReason.HasValue ? $" OBReason={result.OBReason.Value}" : "") +
                      $" end={result.EndPosition}");

            // Reset orbit center to final ball position.
            if (ballAnimator?.CurrentBall != null)
                _orbitCenter = ballAnimator.CurrentBall.position;
            // §2b: chaseCamera.SetTarget(null) relocated to LoopCameraDirector.HandleStateChanged
            // on terminal states (AtRest / InCup / OB). No direct chaseCamera call here.

            switch (result.TerminalState)
            {
                case Golfin.Gameplay.Loop.BallState.AtRest:
                {
                    // §2f: surface-based auto-switch. EnterPutterMode handles UI changes
                    // (track, ball selector, action buttons, central ball putter flag);
                    // camera mode is unchanged post-§2f-revert (2026-05-14). Putter uses
                    // Mode.Chase for all states; §2e pin-aim rotation runs for ALL clubs
                    // since putter no longer owns special camera framing.
                    int target = PutterModeSurfaceController.DecideTargetClub(
                        currentClubIndex: CurrentClubIndex,
                        putterIndex: PutterIndex,
                        endSurface: result.EndSurface,
                        lastNonPutterClubIndex: _lastNonPutterClubIndex);

                    if (target >= 0)
                    {
                        Debug.Log($"[PhysicsLab][§2f] AtRest surface={result.EndSurface} " +
                                  $"auto-switch club {CurrentClubIndex}→{target}");
                        SetClub(target);
                        // PROD path: clear any lab bundle leftover so the bus resolves live stats.
                        _shotController?.ClearStatBundleOverride();
                    }

                    // auto_club_selection: pick the club for the NEXT shot from this lie.
                    // Runs AFTER the §2f block so the green rule always wins (no-ops in putter mode).
                    AutoSelectClubForNextShot();

                    // §2e: pin-aim rotation runs uniformly (including putter post-§2f-revert).
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
                    // spin_and_shot_shape_wiring: reset player spin selection for next shot.
                    Golfin.Gameplay.UI.HUD.SpinContext.Reset();
                    _ballSM.ReArm();
                    break;
                }

                case Golfin.Gameplay.Loop.BallState.OB:
                {
                    // K10 ob_recovery_fixes — real-golf drop rule (Cesar ruling 2026-08-05):
                    // boundary OB is STROKE AND DISTANCE (drop at the previous shot origin, so a
                    // first-shot boundary OB goes back on the tee); water keeps the §2e last-dry-
                    // touch behaviour (lateral relief near entry, never nearer the hole). The
                    // aim-toward-pin yaw below is unchanged — a re-tee drop makes ComputeYawTowardPin
                    // fire straight down the fairway once the camera stops fighting it (Part A).
                    bool isWater = result.OBReason.HasValue
                        && result.OBReason.Value == Golfin.Gameplay.Loop.OBReason.Water;
                    // Water needs the classifier to find the hazard margin the ball last crossed;
                    // null on flat-ground sessions, where ResolveByRule falls back to the legacy
                    // last-dry-touch scan.
                    Vector3 dropPos = OBDropResolver.ResolveByRule(
                        _previousTrajectory, _lastShotOrigin, isWater, _bakedClassifier);
                    Vector3 pinPos  = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
                    float   newYaw  = AimRotationHelper.ComputeYawTowardPin(dropPos, pinPos, _cameraYaw);
                    Vector3 lookDir = new Vector3(Mathf.Cos(newYaw), 0f, Mathf.Sin(newYaw));

                    Debug.Log($"[PhysicsLab][§2e] OB drop ({(isWater ? "water/last-dry-touch" : "boundary/stroke+distance")}): " +
                              $"from end={result.EndPosition} to drop={dropPos:F2} yawRad={newYaw:F3} (penalty stroke +1)");

                    // water_splash_fx (Order 349): on a WATER landing, freeze the camera where it is
                    // (it was chasing the ball into the water, so it is already looking at the entry
                    // point) for a beat so the splash VFX plays on screen, THEN drop the ball + re-aim
                    // to the penalty shot. Camera-only hold — the gameplay result (drop position,
                    // penalty stroke) is unchanged, just sequenced after the beat.
                    if (isWater)
                    {
                        StartCoroutine(WaterSplashCameraHold(dropPos, lookDir));
                    }
                    else
                    {
                        // ball_trail_shot_isolation §9: boundary OB gets a brief hold BEFORE
                        // repositioning so the red ribbon (set by BallTrailController on →OB)
                        // renders for a visible beat. Reposition + ReArm happen after the hold;
                        // ReArm fires →Aiming which clears the ribbon — so the aiming phase is
                        // always clean. Mirror the water-path coroutine structure rather than
                        // adding a second synchronous sequence.
                        StartCoroutine(BoundaryOBHold(dropPos, lookDir));
                    }
                    break;
                }

                case Golfin.Gameplay.Loop.BallState.InCup:
                {
                    // §2d owns re-arm via HoleCompleteDriver / RearmAfterHoleComplete on modal close.
                    // No CompleteShot/ReArm here.
                    break;
                }
            }
        }

        // water_splash_fx (Order 349): how long to hold the camera on the water entry so the splash
        // VFX is visible before the ball drops + the camera re-aims to the penalty shot.
        const float WaterOBDwellSeconds = 1.2f;

        // ball_trail_shot_isolation §9: brief pause for boundary OB so the red ribbon renders
        // for a visible beat before RepositionBallWithLookDir + ReArm wipe it. No camera freeze
        // needed (unlike water: there is no VFX to frame, and the chase camera naturally holds
        // near the landing area). 2.0s gives the player a clear read of the red OB feedback;
        // longer than water (1.2s) because water has dramatic VFX so less time is needed —
        // boundary OB has no VFX so the red ribbon alone carries the feedback signal.
        const float BoundaryOBDwellSeconds = 2.0f;

        // Freezes the camera at its current transform (it was chasing the ball into the water, so it is
        // already looking at the entry point) for WaterOBDwellSeconds, letting the splash play on
        // screen, then performs the normal OB drop + spin reset + re-arm. ChaseCamera is disabled
        // during the hold so its LateUpdate (which, with a null target on OB, would re-point the camera
        // back at the shot origin) doesn't fight the frozen transform; it is re-enabled before the re-aim.
        System.Collections.IEnumerator WaterSplashCameraHold(Vector3 dropPos, Vector3 lookDir)
        {
            Camera cam = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : Camera.main;
            bool       chaseWasEnabled = chaseCamera != null && chaseCamera.enabled;
            Vector3    holdPos = cam != null ? cam.transform.position : Vector3.zero;
            Quaternion holdRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            if (chaseCamera != null) chaseCamera.enabled = false;

            float t = 0f;
            while (t < WaterOBDwellSeconds)
            {
                if (cam != null) cam.transform.SetPositionAndRotation(holdPos, holdRot);
                t += Time.deltaTime;
                yield return null;
            }

            if (chaseCamera != null) chaseCamera.enabled = chaseWasEnabled;

            // Normal OB drop + re-aim, deferred until after the splash beat.
            // RepositionBallWithLookDir calls _shotController.CompleteShot() internally.
            RepositionBallWithLookDir(dropPos, preferredSurfaceTypeValue: null, lookDir: lookDir);
            Golfin.Gameplay.UI.HUD.SpinContext.Reset();
            _ballSM.ReArm();
        }

        // ball_trail_shot_isolation §9: holds BoundaryOBDwellSeconds so the red ribbon that
        // BallTrailController painted on →OB is actually visible for a beat, THEN does the
        // normal OB drop + spin-reset + re-arm (which triggers →Aiming and wipes the ribbon).
        // Holds BEFORE RepositionBallWithLookDir so the ribbon stays at the OB landing spot
        // (parented to the ball; repositioning first would drag it to the drop point mid-hold).
        System.Collections.IEnumerator BoundaryOBHold(Vector3 dropPos, Vector3 lookDir)
        {
            yield return new WaitForSeconds(BoundaryOBDwellSeconds);

            // Normal OB drop + re-aim, deferred until after the ribbon beat.
            // RepositionBallWithLookDir calls _shotController.CompleteShot() internally.
            RepositionBallWithLookDir(dropPos, preferredSurfaceTypeValue: null, lookDir: lookDir);
            Golfin.Gameplay.UI.HUD.SpinContext.Reset();
            _ballSM.ReArm();
        }

        // §2d: invoked by HoleCompleteDriver after the modal is dismissed.
        internal void RearmAfterHoleComplete()
        {
            _shotController?.CompleteShot();
            // spin_and_shot_shape_wiring: reset player spin selection for next hole's first shot.
            Golfin.Gameplay.UI.HUD.SpinContext.Reset();
            _ballSM?.ReArm();
        }

        Trajectory RunSimFromController(ShotInput input, BallPhysicsModifiers ballMods)
        {
            var ground  = BuildGroundProvider();
            var surface = BuildSurfaceProvider(default(ShotPreset));
            // cup_capture_and_lipout: production shot path — pass the loaded hole's cup so the
            // sim can capture / lip-out. _cupSpec is Disabled when no hole is loaded.
            return BallSimulation.Simulate(input, ground, AeroCfg, WindCfg, surface, SurfaceCfg, PuttCfg, ballMods, _treeProvider, _cupSpec);
        }

        bool _configsLoaded;
        void EnsureConfigsLoaded()
        {
            // Defensive: under "Reload Domain Only" Enter Play Mode setting, the
            // auto-property AeroCfg's backing field gets reset to default(AeroConfig)
            // across the Edit→Play boundary but plain private bool _configsLoaded does
            // NOT (asymmetric reset, observed 2026-05-12 in hole-picker repro). Without
            // this validation, the guard short-circuits and AeroCfg stays zero-init,
            // causing DivideByZeroException in AeroModel.ComputeAeroForce on first shot.
            // Also retroactively explains the non-Hole-1 ball-spawn bug: the same
            // DivideByZero fired inside ComputeMaxCarryYards in OnHoleLoaded, aborting
            // before SetupAtTee() could run.
            if (_configsLoaded && _aeroCfg.SpinRateReference > fp.Zero) return;
            if (_configsLoaded)
                Debug.LogWarning("[PhysicsLab] EnsureConfigsLoaded: _configsLoaded=true but AeroCfg.SpinRateReference=0 \u2014 reloading (Edit\u2192Play asymmetric-reset recovery).");
            AeroCfg    = PhysicsConfigLoader.LoadAeroConfig();
            WindCfg    = PhysicsConfigLoader.LoadWindConfig();
            SurfaceCfg = PhysicsConfigLoader.LoadSurfaceConfig();
            PuttCfg    = PhysicsConfigLoader.LoadPuttConfig();
            _configsLoaded = true;
        }

        float ComputeMaxCarryYards()
        {
            EnsureConfigsLoaded();
            float velMps   = 75f;
            float pitchRad = 10.9f * Mathf.Deg2Rad;
            var vel = new fp3(
                fp.FromFloat(velMps * Mathf.Cos(pitchRad)),
                fp.FromFloat(velMps * Mathf.Sin(pitchRad)),
                fp.Zero);
            var simInput = new ShotInput(fp3.Zero, vel, fp.FromInt(60));
            var ground   = new FlatGround(fp.Zero);
            var surface  = new ConstantSurfaceProvider(SurfaceType.Fairway);
            var traj = BallSimulation.Simulate(simInput, ground, AeroCfg, WindConfig.Calm,
                surface, SurfaceCfg, PuttCfg, BallPhysicsModifiers.Neutral);
            fp3 landPos = traj.terrainHits != null && traj.terrainHits.Count > 0
                ? traj.terrainHits[0].Position : traj.finalPosition;
            return XZDist(fp3.Zero, landPos) * 1.09361f;
        }

        // wind_affects_gameplay: convert the hole's HUD wind (WindContext) into the physics
        // WindConfig the sim consumes. Pure wiring — no physics model/formula/tuning touched.
        // Steady base wind only (no gusts / no altitude profile) so shots are deterministic and
        // fair in 1v1: both players on the same hole face the identical, time-invariant wind.
        //   speed:     mph -> m/s (x 0.44704).
        //   direction: WindContext.DirectionDegrees is a compass bearing (0=North=+Z, 90=East=+X,
        //              clockwise) treated as the direction the wind blows TOWARD, so the ball drifts
        //              toward the HUD arrow. World-space +X east / +Z north matches WindConfig's frame.
        //   NOTE: the direction SIGN is unverified on hardware (build blocker) -- if the ball drifts
        //   opposite the HUD arrow, negate vx/vz here. That is the one thing to sanity-check on device.
        static WindConfig WindConfigFromContext(float speedMph, float dirDeg)
        {
            if (speedMph <= 0f) return WindConfig.Calm;
            float ms  = speedMph * 0.44704f;
            float rad = dirDeg * Mathf.Deg2Rad;
            float vx  = ms * Mathf.Sin(rad);   // +X east
            float vz  = ms * Mathf.Cos(rad);   // +Z north
            var cfg = WindConfig.Calm;         // inherits AltitudeRefMeters=10 (unused; AltitudeFactor=0)
            cfg.BaseVelocity = new fp3(fp.FromFloat(vx), fp.Zero, fp.FromFloat(vz));
            return cfg;
        }

        // ── Internal ───────────────────────────────────────────────────────────

        void FireInternal(ShotPreset preset)
        {
            var trajectory = RunSimForCamera(preset);
            _previousTrajectory = trajectory;

            var s0 = trajectory.samples != null && trajectory.samples.Count > 0
                ? trajectory.samples[0].position : preset.Origin;
            Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
            Vector3 launchDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));

            // ── §controls_h: same ordering contract as HandleShotResolved ──────────
            // Cache origin + launchDir BEFORE SM transition fires, spawn new ball BEFORE SM.
            _orbitCenter       = origin;
            _lastShotOrigin    = origin;
            _lastShotLaunchDir = launchDir;

            if (_predictionVisible) trajectoryRenderer?.Draw(trajectory);
            ballAnimator.Play(trajectory);

            // Route through Director (SM → Aiming→Flying → ArmChaseForShot) instead of
            // calling chaseCamera directly. Keeps preset path and touch path in sync.
            fp3 origin_fp = new fp3(fp.FromFloat(origin.x), fp.FromFloat(origin.y), fp.FromFloat(origin.z));
            _ballSM?.OnTrajectoryComputed(origin_fp, trajectory, AeroCfg.BallRadius);

            var readout = BuildReadout(preset, trajectory);
            OnShotFired?.Invoke(readout);
            LogReadout(readout);
        }

        // Fire from current ball position in camera heading direction,
        // preserving the preset's speed, pitch, and spin magnitude.
        Trajectory RunSimForCamera(ShotPreset preset)
        {
            fp3 origin = GetCurrentOrigin(fallbackToInput: preset.Origin);

            // Rotate XZ velocity to camera yaw; keep Y (preserves pitch + speed).
            float vx = preset.Velocity.x.ToFloat();
            float vy = preset.Velocity.y.ToFloat();
            float vz = preset.Velocity.z.ToFloat();
            float xzSpeed = Mathf.Sqrt(vx * vx + vz * vz);
            var newVelocity = new fp3(
                fp.FromFloat(xzSpeed * Mathf.Cos(_cameraYaw)),
                fp.FromFloat(vy),
                fp.FromFloat(xzSpeed * Mathf.Sin(_cameraYaw)));

            // Rotate backspin axis to match new heading.
            SpinState spin = preset.Spin;
            if (spin.IsSpinning)
            {
                var newAxis = new fp3(
                    fp.FromFloat(-Mathf.Sin(_cameraYaw)),
                    fp.Zero,
                    fp.FromFloat(Mathf.Cos(_cameraYaw)));
                spin = new SpinState(newAxis, spin.Rate);
            }

            var input   = new ShotInput(origin, newVelocity, fp.FromInt(60), spin);
            var ground  = BuildGroundProvider();
            var surface = BuildSurfaceProvider(preset);
            // cup_capture_and_lipout: preset/lab shot path gets the same cup as production.
            return BallSimulation.Simulate(input, ground, AeroCfg, preset.Wind, surface, SurfaceCfg, PuttCfg, BallPhysicsModifiers.Neutral, _treeProvider, _cupSpec);
        }

        // Returns current ball position snapped to terrain, or fallback if no ball.
        // §phase_b iter-7: if _airborneOriginOverride is set (by PlaceBallAtAirborne),
        // return it verbatim and clear it — no surface snap.
        fp3 GetCurrentOrigin(fp3 fallbackToInput)
        {
            // Airborne-origin path: bypass surface snap, consume override (single-shot).
            if (_airborneOriginOverride.HasValue)
            {
                var o = _airborneOriginOverride.Value;
                _airborneOriginOverride = null;
                return o;
            }

            Vector3 sp;
            if (ballAnimator?.CurrentBall != null)
                sp = ballAnimator.CurrentBall.position;
            else if (_ballSpawnPoint != null)
                sp = _ballSpawnPoint.position;
            else
                return fallbackToInput;

            float y = SurfaceSnap(sp.x, sp.z, sp.y);
            return new fp3(fp.FromFloat(sp.x), fp.FromFloat(y), fp.FromFloat(sp.z));
        }

        static float SurfaceSnap(float x, float z, float defaultY, int? preferredSurfaceTypeValue = null)
            => PlacementSnapHelper.Snap(x, z, defaultY, preferredSurfaceTypeValue);

        // Lifts chase-camera follow height when the ball is in a depression (e.g. bunker).
        // Raycasts at 4 surrounding points; if ball is > 0.5m below terrain rim, raises camera.
        void AdjustCameraForDepression(Vector3 ballPos)
        {
            if (chaseCamera == null) return;

            float maxSurroundY = ballPos.y;
            float[] offsets = { 2f, -2f };
            foreach (float ox in offsets)
            {
                if (UnityEngine.Physics.Raycast(
                    new Vector3(ballPos.x + ox, 500f, ballPos.z), Vector3.down, out RaycastHit hx, 1000f))
                    if (hx.point.y > maxSurroundY) maxSurroundY = hx.point.y;

                if (UnityEngine.Physics.Raycast(
                    new Vector3(ballPos.x, 500f, ballPos.z + ox), Vector3.down, out RaycastHit hz, 1000f))
                    if (hz.point.y > maxSurroundY) maxSurroundY = hz.point.y;
            }

            float depth = maxSurroundY - ballPos.y;
            chaseCamera.FollowHeightOffset = depth > 0.5f ? Mathf.Min(depth, 3f) : 0f;
        }

        IGroundProvider BuildGroundProvider()
        {
            if (_bakedGround != null) return _bakedGround;
            return new FlatGround(fp.Zero);
        }

        ISurfaceProvider BuildSurfaceProvider(ShotPreset preset)
        {
            if (_bakedClassifier != null) return _bakedClassifier;
            SurfaceType surfaceType = preset.HasSurfaceOverride ? preset.SurfaceOverride : SurfaceType.Fairway;
            return new ConstantSurfaceProvider(surfaceType);
        }

        /// <summary>
        /// Loads BakedZoneClassifier (zones.json) + BakedHeightProvider
        /// (heightmap.bytes) for the given hole id (e.g. "Hole_01"). Sets
        /// <see cref="_bakedClassifier"/> + <see cref="_bakedGround"/> on
        /// success; logs and leaves them null on failure (sim falls back
        /// to scene providers).
        /// </summary>
        void TryLoadBakedProviders(string holeId)
        {
            _bakedClassifier = null;
            _bakedGround     = null;
            _treeProvider    = null;

            // Both files live under Assets/Resources/HoleData/<courseSlug>/<holeId>/ so they
            // ship with built players AND survive cross-PC pulls (Tools/UHoleGeo/output/
            // is gitignored — heightmap MUST live in Resources, not the bake-tool's
            // staging folder).
            string courseSlug = ActiveCourseContext.CurrentCourseSlug;
            var zonesAsset = Resources.Load<TextAsset>($"HoleData/{courseSlug}/{holeId}/zones");
            var hmAsset    = Resources.Load<TextAsset>($"HoleData/{courseSlug}/{holeId}/heightmap");
            if (zonesAsset == null)
            {
                Debug.LogWarning($"[PhysicsLab] No baked zones at Resources/HoleData/{courseSlug}/{holeId}/zones — sim will use scene providers.");
                return;
            }
            if (hmAsset == null)
            {
                Debug.LogWarning($"[PhysicsLab] No baked heightmap at Resources/HoleData/{courseSlug}/{holeId}/heightmap — sim will use scene providers.");
                return;
            }

            try
            {
                var data = Golfin.Physics.Runtime.Baked.ZoneData.FromJson(zonesAsset.text);
                _bakedClassifier = new Golfin.Physics.Runtime.Baked.BakedZoneClassifier(data);

                var hm = Golfin.Physics.Runtime.HeightmapLoader.LoadFromBytes(hmAsset.bytes);
                if (hm == null)
                {
                    Debug.LogWarning($"[PhysicsLab] Heightmap parse failed for {holeId}; sim will use scene providers.");
                    _bakedClassifier = null;
                    return;
                }
                _bakedGround = new Golfin.Physics.Runtime.Baked.BakedHeightProvider(hm, _bakedClassifier);
                Debug.Log($"[PhysicsLab] Baked providers wired for {holeId}: "
                        + $"{data.zones.Count} zone groups, "
                        + $"OB mask={(data.obMask != null ? "yes" : "no")}.");

                // Phase 7: load tree obstacles for this hole.
                var treeAsset = Resources.Load<TextAsset>($"HoleData/{courseSlug}/{holeId}/tree_obstacles");
                var instances = Golfin.Physics.Runtime.TreeObstacleLoader.LoadInstances(treeAsset);
                _treeProvider = Golfin.Physics.Runtime.TreeObstacleProvider.Create(instances);
                if (_treeProvider != null)
                    Debug.Log($"[PhysicsLab] Tree obstacles loaded for {holeId}: {instances.Count} trees.");
                else
                    Debug.Log($"[PhysicsLab] No tree_obstacles CSV for {holeId} — tree collision disabled.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PhysicsLab] Baked provider load failed: {e.Message}; sim will use scene providers.");
                _bakedClassifier = null;
                _bakedGround     = null;
            }
        }

        // Called by LabHoleBinder when a Hole_XX_Geo scene is opened additively.
        public void OnHoleLoaded(string sceneName)
        {
            DiagAero($"OnHoleLoaded.start[{sceneName}]");
            _useSceneProviders = true;

            // M3: load baked providers for this hole. holeId is sceneName minus
            // the "_Geo" suffix (e.g. "Hole_01_Geo" → "Hole_01").
            string holeId = sceneName.EndsWith("_Geo")
                ? sceneName.Substring(0, sceneName.Length - 4)
                : sceneName;
            TryLoadBakedProviders(holeId);

            // §2a: refresh SM surface provider now that baked providers are loaded.
            if (_ballSM != null)
                _ballSM.SetSurfaceProvider(BuildSurfaceProvider(default(ShotPreset)));

            // Disable any debug walk camera that ships inside hole scenes.
            // Disable the GO (not just component) so Start() never fires and cursor is never stolen.
            Scene loadedSceneEarly = SceneManager.GetSceneByName(sceneName);
            if (loadedSceneEarly.IsValid())
            {
                System.Type walkCamType = System.Type.GetType("WalkCamera, Assembly-CSharp");
                if (walkCamType != null)
                {
                    foreach (var root in loadedSceneEarly.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb != null && mb.GetType() == walkCamType)
                        {
                            mb.gameObject.SetActive(false);
                            Debug.Log($"[PhysicsLab] WalkCamera GO deactivated: '{mb.gameObject.name}'");
                        }
                    }
                }
            }

            System.Type smType = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");
            if (smType == null)
            {
                Debug.LogWarning("[PhysicsLab] Course.SurfaceMarker not found via reflection — tee detection skipped.");
                SetupAtTee();
                return;
            }

            System.Reflection.FieldInfo stField = smType.GetField("surfaceType");

            var teeGOs     = new System.Collections.Generic.List<GameObject>();
            var greenGOs   = new System.Collections.Generic.List<GameObject>();
            var bunkerGOs  = new System.Collections.Generic.List<GameObject>();
            var fairwayGOs = new System.Collections.Generic.List<GameObject>();
            var waterGOs   = new System.Collections.Generic.List<GameObject>();

            // Search only within the newly loaded scene.
            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            var roots = loadedScene.IsValid() ? loadedScene.GetRootGameObjects()
                                              : new GameObject[0];
            Debug.Log($"[TeeDiag] OnHoleLoaded scan: sceneName={sceneName} IsValid={loadedScene.IsValid()} isLoaded={loadedScene.isLoaded} rootCount={roots.Length}");
            foreach (var root in roots)
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.GetType() != smType) continue;
                    int val = (int)stField.GetValue(mb);
                    if      (val == 6) teeGOs.Add(mb.gameObject);     // Tee
                    else if (val == 1) greenGOs.Add(mb.gameObject);   // Green
                    else if (val == 4) bunkerGOs.Add(mb.gameObject);  // Bunker
                    else if (val == 0) fairwayGOs.Add(mb.gameObject); // Fairway
                    else if (val == 5) waterGOs.Add(mb.gameObject);   // Water
                }
            }

            // Compute green centroid.
            _loadedHoleGreenCentroid = Vector3.zero;
            _greenCentroidValid      = greenGOs.Count > 0;
            foreach (var g in greenGOs) _loadedHoleGreenCentroid += g.transform.position;
            if (_greenCentroidValid) _loadedHoleGreenCentroid /= greenGOs.Count;

            // Find TeeMarker_regular_* GOs directly by name — these are the physical tee marker props.
            var regularMarkers = new System.Collections.Generic.List<Transform>();
            foreach (var root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf("TeeMarker_regular", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        regularMarkers.Add(t);
                }
            }

            // Fall back to SurfaceMarker tee zone GOs if no named markers found.
            Vector3 teePos = Vector3.zero;
            bool teeFound = false;
            // aim_camera_ball_centering: keep the individual marker positions too — the tee-off
            // aim clamp needs the SPREAD, not just the midpoint the rest of this block computes.
            _teeMarkerPositions.Clear();
            if (regularMarkers.Count > 0)
            {
                foreach (var t in regularMarkers) teePos += t.position;
                teePos /= regularMarkers.Count;
                teeFound = true;
                foreach (var t in regularMarkers) _teeMarkerPositions.Add(t.position);
                Debug.Log($"[PhysicsLab] OnHoleLoaded: {sceneName} — tee midpoint from {regularMarkers.Count} TeeMarker_regular_* GOs at {teePos:F2}");
            }
            else if (teeGOs.Count > 0)
            {
                foreach (var g in teeGOs) teePos += g.transform.position;
                teePos /= teeGOs.Count;
                teeFound = true;
                foreach (var g in teeGOs) _teeMarkerPositions.Add(g.transform.position);
                Debug.Log($"[PhysicsLab] OnHoleLoaded: {sceneName} — tee midpoint from {teeGOs.Count} SurfaceMarker tees (fallback) at {teePos:F2}");
            }

            if (teeFound)
            {
                Debug.Log($"[TeeDiag] teeFound=true teePos={teePos:F2} regularMarkers={regularMarkers.Count} teeGOs={teeGOs.Count}");
                // Always scan children first — non-serialized _runtimeTeeAnchor goes null in
                // Edit Mode between operations even without a full domain reload.
                foreach (Transform child in transform)
                {
                    if (child.name != "_RuntimeTeeAnchor") continue;
                    if (_runtimeTeeAnchor == null) _runtimeTeeAnchor = child;
                    else DestroyImmediate(child.gameObject);
                }
                if (_runtimeTeeAnchor == null)
                {
                    var go = new GameObject("_RuntimeTeeAnchor");
                    go.transform.SetParent(transform);
                    _runtimeTeeAnchor = go.transform;
                }
                _runtimeTeeAnchor.position = teePos;
                _ballSpawnPoint = _runtimeTeeAnchor;
                // Persist so PlayMode reload can recover without re-scanning tee markers.
                _savedTeeWorldPos = teePos;
                _savedTeePosValid = true;
                Debug.Log($"[TeeDiag] after assign: _runtimeTeeAnchor.pos={_runtimeTeeAnchor.position:F2} _ballSpawnPoint={(_ballSpawnPoint!=null?_ballSpawnPoint.name:"null")} _savedTeeWorldPos={_savedTeeWorldPos:F2} _savedTeePosValid={_savedTeePosValid}");
            }
            else
            {
                Debug.LogWarning($"[PhysicsLab] OnHoleLoaded: no tee markers found in {sceneName}.");
                Debug.LogWarning($"[TeeDiag] teeFound=FALSE — regularMarkers={regularMarkers.Count} teeGOs={teeGOs.Count} (stale _savedTeeWorldPos={_savedTeeWorldPos:F2} valid={_savedTeePosValid} will be used by SetupAtTee)");
            }

            // SetupAtTee BEFORE anything that might throw, so the ball is always placed.
            SetupAtTee();

            if (_shotConeView != null)
            {
                try { _shotConeView.SetMaxCarryYards(ComputeMaxCarryYards()); }
                catch (System.Exception ex) { Debug.LogWarning($"[PhysicsLab] ComputeMaxCarryYards failed: {ex.Message}"); }
            }

            // Populate ball placement entries for the Place Ball dropdown in the lab UI.
            BuildPlacementEntries(teeFound, teePos, greenGOs, bunkerGOs, fairwayGOs, waterGOs);

            // Copy all lighting settings from the hole scene into LabScaffold so URPWater
            // gets the same environment (skybox, ambient, fog, reflections) it would have
            // when the hole is loaded standalone.
            CopyHoleLighting(SceneManager.GetSceneByName(sceneName));

            // water_splash_fx (Order 349): ShellScene stays additively loaded during gameplay
            // (see GameplaySceneLoader — host + hole load Additive, ShellScene is never unloaded)
            // and carries its own intensity-2 Directional Light. Combined with the hole's own
            // directional light that double-lights the scene from opposing azimuths and flattens
            // the URPWater surface to grey. Switch the shell's directional light off while a hole
            // is loaded so the hole's sun is the only one.
            DisableShellDirectionalLight();

            // Populate HoleContext for HUD widgets (PlayerCardWidget, HoleCardWidget).
            // HoleMetadata lives in Assembly-CSharp; use reflection to avoid a circular asmdef dep
            // (Viewer has autoReferenced:true, so Viewer→Assembly-CSharp would be circular).
            System.Type metaType = System.Type.GetType("Golfin.CourseImport.HoleMetadata, Assembly-CSharp");
            if (metaType != null)
            {
                var holeSceneForMeta = SceneManager.GetSceneByName(sceneName);
                Component meta = null;
                if (holeSceneForMeta.IsValid())
                {
                    foreach (var root in holeSceneForMeta.GetRootGameObjects())
                    {
                        meta = root.GetComponentInChildren(metaType, true);
                        if (meta != null) break;
                    }
                }
                if (meta != null)
                {
                    var fHole = metaType.GetField("holeNumber");
                    var fPar  = metaType.GetField("par");
                    var fYds  = metaType.GetField("championshipYards");
                    Golfin.Gameplay.UI.HUD.HoleContext.HoleNumber        = fHole != null ? (int)fHole.GetValue(meta) : 1;
                    Golfin.Gameplay.UI.HUD.HoleContext.Par               = fPar  != null ? (int)fPar.GetValue(meta)  : 4;
                    Golfin.Gameplay.UI.HUD.HoleContext.ChampionshipYards = fYds  != null ? (int)fYds.GetValue(meta)  : 0;
                    Golfin.Gameplay.UI.HUD.HoleContext.GreenCentroidWorld = _loadedHoleGreenCentroid;

                    // Find Flag GO for pin position — recursive walk, respects inactive children
                    Scene loadedSceneForFlag = SceneManager.GetSceneByName(sceneName);
                    GameObject flagGo = null;
                    if (loadedSceneForFlag.IsValid())
                    {
                        foreach (var root in loadedSceneForFlag.GetRootGameObjects())
                        {
                            var found = FindDescendantByName(root.transform, "Flag");
                            if (found != null) { flagGo = found.gameObject; break; }
                        }
                    }
                    if (flagGo != null)
                    {
                        Golfin.Gameplay.UI.HUD.HoleContext.PinWorld = flagGo.transform.position;
                        Debug.Log($"[PhysicsLab] Flag GO found at {flagGo.transform.position}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[PhysicsLab] No 'Flag' GO found in hole scene; HoleIndicatorWidget will fall back to GreenCentroidWorld. " +
                            "If you're loading Hole_01_Geo and seeing this, the Flag GO IS in the scene file at line ~762188 — " +
                            "check whether OnHoleLoaded fires before the additive scene's GOs are fully registered.");
                        Golfin.Gameplay.UI.HUD.HoleContext.PinWorld = Golfin.Gameplay.UI.HUD.HoleContext.GreenCentroidWorld;
                    }

                    // Populate WindContext from per-hole CSV via reflection (HoleDatabaseLoader is in Assembly-CSharp)
                    int holeNumberLocal = Golfin.Gameplay.UI.HUD.HoleContext.HoleNumber;
                    System.Type loaderType = System.Type.GetType("GolfinRedux.UI.HoleDatabaseLoader, Assembly-CSharp");
                    if (loaderType != null)
                    {
                        var getHoleMethod = loaderType.GetMethod("GetHole", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (getHoleMethod != null)
                        {
                            var holeData = getHoleMethod.Invoke(null, new object[] { holeNumberLocal - 1 });
                            if (holeData != null)
                            {
                                var fSpeed = holeData.GetType().GetField("windSpeedMph");
                                var fDir   = holeData.GetType().GetField("windDirectionDegrees");
                                if (fSpeed != null) Golfin.Gameplay.UI.HUD.WindContext.SpeedMph         = (float)fSpeed.GetValue(holeData);
                                if (fDir   != null) Golfin.Gameplay.UI.HUD.WindContext.DirectionDegrees = (float)fDir.GetValue(holeData);
                                Golfin.Gameplay.UI.HUD.WindContext.Raise();
                                // wind_affects_gameplay: feed the same per-hole wind into the sim so the
                                // committed shot (RunSimFromController -> WindCfg) actually drifts.
                                WindCfg = WindConfigFromContext(
                                    Golfin.Gameplay.UI.HUD.WindContext.SpeedMph,
                                    Golfin.Gameplay.UI.HUD.WindContext.DirectionDegrees);
                                Debug.Log($"[PhysicsLab] Wind: {Golfin.Gameplay.UI.HUD.WindContext.SpeedMph:F1} mph @ {Golfin.Gameplay.UI.HUD.WindContext.DirectionDegrees:F0} deg -> BaseVelocity={WindCfg.BaseVelocity}");
                            }
                            else
                            {
                                Golfin.Gameplay.UI.HUD.WindContext.Reset();
                                WindCfg = WindConfig.Calm;
                                Debug.LogWarning($"[PhysicsLab] HoleDatabaseLoader.GetHole({holeNumberLocal - 1}) returned null; WindContext reset.");
                            }
                        }
                    }
                    else
                    {
                        Golfin.Gameplay.UI.HUD.WindContext.Reset();
                        WindCfg = WindConfig.Calm;
                        Debug.LogWarning("[PhysicsLab] HoleDatabaseLoader type not found; WindContext reset.");
                    }

                    // Fire HoleContext AFTER PinWorld is written
                    Golfin.Gameplay.UI.HUD.HoleContext.Raise();
                    // §2c: reset session state for the new hole. Fires OnTurnChanged so PlayerCardWidget renders fresh "TURN 1".
                    Golfin.Gameplay.Session.GameSession.ResetForNewHole();
                    // §2d: install a real cup detector keyed to this hole's pin position.
                    // Speed gate passed from PuttCfg.CupCaptureSpeed (tunable via putt.csv).
                    // Default 1.5 m/s per USGA lip-out anchor (architect-locked 2026-05-14).
                    if (_ballSM != null)
                    {
                        Vector3 pinW = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
                        var pinFp = new fp3(fp.FromFloat(pinW.x), fp.FromFloat(pinW.y), fp.FromFloat(pinW.z));
                        _ballSM.SetCupDetector(new Golfin.Gameplay.Loop.RealCupDetector(
                            pinFp,
                            Golfin.Gameplay.Loop.RealCupDetector.DefaultCupRadius,
                            PuttCfg.CupCaptureSpeed));
                        Debug.Log($"[PhysicsLab][§2d] RealCupDetector installed at pin={pinW:F3} cupCaptureSpeed={PuttCfg.CupCaptureSpeed.ToFloat():F2} m/s");

                        // cup_capture_and_lipout: build the CupSpec the SIM consumes, from the
                        // same inputs as the detector above. The detector stays as the fallback
                        // (and the bot/test seam); the sim is now the primary authority because
                        // it is the only place the ball's path can actually change at the cup.
                        _cupSpec = new Golfin.Physics.CupSpec(
                            pinFp,
                            Golfin.Gameplay.Loop.RealCupDetector.DefaultCupRadius,
                            PuttCfg.CupCaptureSpeed,
                            PuttCfg.CupDepth,
                            PuttCfg.LipRestitution,
                            PuttCfg.LipSpeedDamping,
                            PuttCfg.LipPopVy);
                        Debug.Log($"[PhysicsLab][cup] In-sim CupSpec enabled: pin={pinW:F3} "
                                + $"radius={_cupSpec.Radius.ToFloat():F3}m depth={_cupSpec.Depth.ToFloat():F3}m "
                                + $"captureSpeed={_cupSpec.CaptureSpeed.ToFloat():F2}m/s "
                                + $"lip=(restitution={_cupSpec.LipRestitution.ToFloat():F2}, "
                                + $"damping={_cupSpec.LipSpeedDamping.ToFloat():F2}, "
                                + $"popVy={_cupSpec.LipPopVy.ToFloat():F2})");
                    }
                }
                else
                {
                    Debug.LogWarning($"[PhysicsLab] OnHoleLoaded: no HoleMetadata found in {sceneName}; HoleContext not updated.");
                }
            }
            else
            {
                Debug.LogWarning("[PhysicsLab] HoleMetadata type not found via reflection; HoleContext not updated.");
            }

            // Wire HoleIndicatorWidget camera/ball (same pattern as _shotConeView wiring in Awake)
            var holeWidget = FindObjectOfType<Golfin.Gameplay.UI.ShotUI.HoleIndicatorWidget>();
            if (holeWidget != null)
            {
                holeWidget.SetCamera(chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null);
                holeWidget.SetBallTransform(ballAnimator != null ? ballAnimator.CurrentBall : null);
            }

            // PutterGreenReader subscribes to HoleContext.OnChanged and calls GetSurfaces()
            // (which returns the new _bakedClassifier) to trigger a rebake automatically.
            // No manual camera/ball sync needed — reader polls BallPosition each frame.

            // ob_boundary_presentation (Order 1240): build/refresh the OB ground skirt.
            // Exactly one ObGroundSkirt component on this GO at all times — GetComponent-or-AddComponent.
            if (_obSkirt == null)
                _obSkirt = GetComponent<ObGroundSkirt>() ?? gameObject.AddComponent<ObGroundSkirt>();
            _obSkirt.Rebuild(chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null);

            DiagAero($"OnHoleLoaded.end[{sceneName}]");
        }

        void BuildPlacementEntries(
            bool teeFound, Vector3 teePos,
            System.Collections.Generic.List<GameObject> greenGOs,
            System.Collections.Generic.List<GameObject> bunkerGOs,
            System.Collections.Generic.List<GameObject> fairwayGOs,
            System.Collections.Generic.List<GameObject> waterGOs)
        {
            PlacementEntries.Clear();

            // Tee — one entry using the same midpoint the scaffold already uses.
            // preferredSurfaceTypeValue=6 (Golfin.Course.SurfaceType.Tee)
            if (teeFound)
                PlacementEntries.Add(new BallPlacementEntry("Tee 1", teePos, 6));

            // Green entries (type 1).
            AddSurfaceEntries(PlacementEntries, greenGOs,   "Green",   1);

            // Bunker entries (type 4).
            AddSurfaceEntries(PlacementEntries, bunkerGOs,  "Bunker",  4);

            // Fairway entries (type 0).
            AddSurfaceEntries(PlacementEntries, fairwayGOs, "Fairway", 0);

            // Water entries — offset 1m toward the green centroid so the ball lands on grass.
            // No preferred type (first-hit wins — we want whatever grass is there).
            for (int i = 0; i < waterGOs.Count; i++)
            {
                Vector3 wPos   = waterGOs[i].transform.position;
                Vector3 target = _greenCentroidValid ? _loadedHoleGreenCentroid : wPos + Vector3.right * 10f;
                Vector3 dir    = target - wPos;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) dir = dir.normalized;
                PlacementEntries.Add(new BallPlacementEntry($"Near Water {i + 1}", wPos + dir * 1f, null));
            }

            // Y is resolved at placement time via SurfaceSnap; do NOT pre-snap here.
            // Pre-snapping caused Bug 1: first-hit race between fringe and green colliders
            // at build time is won non-deterministically. Type-aware snap at placement time fixes it.

            OnPlacementEntriesChanged?.Invoke();
            Debug.Log($"[PhysicsLab] PlacementEntries built: {PlacementEntries.Count} entries.");
        }

        static void AddSurfaceEntries(
            System.Collections.Generic.List<BallPlacementEntry> list,
            System.Collections.Generic.List<GameObject> gos,
            string prefix,
            int? preferredSurfaceTypeValue)
        {
            for (int i = 0; i < gos.Count; i++)
                list.Add(new BallPlacementEntry($"{prefix} {i + 1}", gos[i].transform.position, preferredSurfaceTypeValue));
        }

        void CopyHoleLighting(Scene holeScene)
        {
            if (!holeScene.IsValid() || !holeScene.isLoaded) return;

            var scaffoldScene = SceneManager.GetSceneByName("LabScaffold");

            // Temporarily make hole active so RenderSettings reads its values.
            SceneManager.SetActiveScene(holeScene);
            var skybox              = RenderSettings.skybox;
            var ambientMode         = RenderSettings.ambientMode;
            var ambientSkyColor     = RenderSettings.ambientSkyColor;
            var ambientEquatorColor = RenderSettings.ambientEquatorColor;
            var ambientGroundColor  = RenderSettings.ambientGroundColor;
            var ambientLight        = RenderSettings.ambientLight;
            var ambientIntensity    = RenderSettings.ambientIntensity;
            var fog                 = RenderSettings.fog;
            var fogColor            = RenderSettings.fogColor;
            var fogMode             = RenderSettings.fogMode;
            var fogStartDistance    = RenderSettings.fogStartDistance;
            var fogEndDistance      = RenderSettings.fogEndDistance;
            var fogDensity          = RenderSettings.fogDensity;
            var defaultReflMode     = RenderSettings.defaultReflectionMode;
            var reflectionIntensity = RenderSettings.reflectionIntensity;
            var reflectionBounces   = RenderSettings.reflectionBounces;
            var customReflection    = RenderSettings.customReflectionTexture;
            var sun                 = RenderSettings.sun;

            // Restore LabScaffold as active (keeps new-GO placement correct),
            // then write the hole's settings into it.
            if (scaffoldScene.IsValid())
                SceneManager.SetActiveScene(scaffoldScene);

            RenderSettings.skybox                  = skybox;
            RenderSettings.ambientMode             = ambientMode;
            RenderSettings.ambientSkyColor         = ambientSkyColor;
            RenderSettings.ambientEquatorColor     = ambientEquatorColor;
            RenderSettings.ambientGroundColor      = ambientGroundColor;
            RenderSettings.ambientLight            = ambientLight;
            RenderSettings.ambientIntensity        = ambientIntensity;
            RenderSettings.fog                     = fog;
            RenderSettings.fogColor                = fogColor;
            RenderSettings.fogMode                 = fogMode;
            RenderSettings.fogStartDistance        = fogStartDistance;
            RenderSettings.fogEndDistance          = fogEndDistance;
            RenderSettings.fogDensity              = fogDensity;
            RenderSettings.defaultReflectionMode   = defaultReflMode;
            RenderSettings.reflectionIntensity     = reflectionIntensity;
            RenderSettings.reflectionBounces       = reflectionBounces;
            RenderSettings.customReflectionTexture = customReflection;
            RenderSettings.sun                     = sun;

            DynamicGI.UpdateEnvironment();
            Debug.Log($"[PhysicsLab] Copied lighting from {holeScene.name} into LabScaffold.");
        }

        // water_splash_fx (Order 349): the ShellScene directional light we switch off while a hole
        // is loaded, held so OnHoleUnloaded can restore it when we return to the shell.
        Light _shellDirLightDisabled;

        /// <summary>
        /// Disables the ShellScene's directional light while a hole is loaded. ShellScene is kept
        /// additively loaded during gameplay and ships an intensity-2 Directional Light; the hole
        /// scene ships its own directional light too. With both active the surface is double-lit
        /// from opposing azimuths, which flattens the URPWater reflection to flat grey. We leave the
        /// hole's light as the sole sun and restore the shell light on unload. No-op when there is
        /// no ShellScene (e.g. the standalone lab-rig path).
        /// </summary>
        void DisableShellDirectionalLight()
        {
            if (_shellDirLightDisabled != null) return; // already disabled for the current hole
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l == null || l.type != LightType.Directional || !l.enabled) continue;
                if (l.gameObject.scene.name != "ShellScene") continue;
                l.enabled = false;
                _shellDirLightDisabled = l;
                Debug.Log($"[PhysicsLab] Disabled ShellScene directional light '{l.gameObject.name}' (intensity {l.intensity}) while hole is loaded — prevents double-lighting / grey water.");
                return;
            }
            Debug.Log("[PhysicsLab] DisableShellDirectionalLight: no enabled ShellScene directional light found (ok for the standalone lab-rig path).");
        }

        // Called by LabHoleBinder when the loaded hole scene is closed.
        public void OnHoleUnloaded()
        {
            DiagAero("OnHoleUnloaded.start");

            // water_splash_fx (Order 349): restore the ShellScene directional light we switched off
            // on load, so returning to the shell/home is lit normally again.
            if (_shellDirLightDisabled != null)
            {
                _shellDirLightDisabled.enabled = true;
                Debug.Log("[PhysicsLab] Re-enabled ShellScene directional light on hole unload.");
                _shellDirLightDisabled = null;
            }
            _useSceneProviders   = false;
            _greenCentroidValid  = false;
            _ballSpawnPoint      = null;
            _savedTeePosValid    = false;
            _teeMarkerPositions.Clear();   // aim_camera_ball_centering: no hole ⇒ no tee clamp
            _bakedClassifier     = null;
            _bakedGround         = null;

            // §2a: revert SM surface provider to flat-ground fallback.
            if (_ballSM != null)
                _ballSM.SetSurfaceProvider(BuildSurfaceProvider(default(ShotPreset)));

            Golfin.Gameplay.UI.HUD.HoleContext.Reset();
            // §2c: clear session state on hole unload (defensive — next hole load will reset again,
            // but this guarantees clean state if we go to a no-hole flat-ground fallback).
            Golfin.Gameplay.Session.GameSession.ResetForNewHole();
            // §2d: revert to NullCupDetector for flat-ground fallback.
            if (_ballSM != null)
                _ballSM.SetCupDetector(new Golfin.Gameplay.Loop.NullCupDetector());
            // cup_capture_and_lipout: no hole loaded → no cup. Disabling restores the
            // bit-exact pre-cup sim path on synthetic flat ground.
            _cupSpec = Golfin.Physics.CupSpec.Disabled;

            PlacementEntries.Clear();
            OnPlacementEntriesChanged?.Invoke();

            // Restore LabScaffold as active scene.
            Scene scaffoldScene = SceneManager.GetSceneByName("LabScaffold");
            if (scaffoldScene.IsValid())
                SceneManager.SetActiveScene(scaffoldScene);

            Debug.Log("[PhysicsLab] OnHoleUnloaded — reverted to flat-ground fallback.");
            DiagAero("OnHoleUnloaded.end");
        }

        ShotReadout BuildReadout(ShotPreset preset, Trajectory t)
        {
            float carryM = 0f;
            SurfaceType finalSurface = SurfaceType.Fairway;
            if (t.terrainHits != null && t.terrainHits.Count > 0)
            {
                carryM       = XZDist(preset.Origin, t.terrainHits[0].Position);
                finalSurface = t.terrainHits[t.terrainHits.Count - 1].Surface;
            }
            float totalM  = XZDist(preset.Origin, t.finalPosition);
            float peakY   = 0f;
            float originY = preset.Origin.y.ToFloat();
            foreach (var s in t.samples)
            {
                float y = s.position.y.ToFloat() - originY;
                if (y > peakY) peakY = y;
            }
            int bounceCount = 0;
            if (t.terrainHits != null)
                foreach (var h in t.terrainHits)
                    if (!h.IsStop) bounceCount++;

            return new ShotReadout
            {
                PresetDisplayName  = preset.DisplayName,
                CarryMeters        = carryM,
                TotalMeters        = totalM,
                MaxHeightMeters    = peakY,
                BounceCount        = bounceCount,
                TerminationReason  = t.termination.ToString(),
                FinalSurface       = finalSurface,
                SimDurationSeconds = t.finalTime.ToFloat(),
            };
        }

        static float XZDist(fp3 a, fp3 b)
        {
            float dx = b.x.ToFloat() - a.x.ToFloat();
            float dz = b.z.ToFloat() - a.z.ToFloat();
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void LogReadout(ShotReadout r)
        {
            Debug.Log($"[PhysicsLab] {r.PresetDisplayName}\n" +
                      $"  Carry:   {r.CarryMeters:F1}m ({r.CarryMeters * 1.09361f:F1}yd)\n" +
                      $"  Total:   {r.TotalMeters:F1}m ({r.TotalMeters * 1.09361f:F1}yd)\n" +
                      $"  Peak:    {r.MaxHeightMeters:F1}m\n" +
                      $"  Bounces: {r.BounceCount}\n" +
                      $"  Ended:   {r.TerminationReason} on {r.FinalSurface}\n" +
                      $"  Time:    {r.SimDurationSeconds:F2}s");
        }

        // Helper — recursive descent by name prefix; walks every transform regardless of active state.
        // Matches if the transform's name equals targetName or starts with targetName + "_" (e.g. "Flag_1").
        static Transform FindDescendantByName(Transform parent, string targetName)
        {
            if (parent.name == targetName ||
                parent.name.StartsWith(targetName + "_", System.StringComparison.OrdinalIgnoreCase))
                return parent;
            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var found = FindDescendantByName(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }
    }

    public struct BallPlacementEntry
    {
        public string  Label;
        public Vector3 WorldPos; // XZ are the target; Y is resolved via type-aware raycast at placement time.
        public int?    PreferredSurfaceTypeValue; // Golfin.Course.SurfaceType int, or null = first-hit

        public BallPlacementEntry(string label, Vector3 worldPos, int? preferredSurfaceTypeValue = null)
        {
            Label = label;
            WorldPos = worldPos;
            PreferredSurfaceTypeValue = preferredSurfaceTypeValue;
        }
    }

    public struct ShotReadout
    {
        public string      PresetDisplayName;
        public float       CarryMeters;
        public float       TotalMeters;
        public float       MaxHeightMeters;
        public int         BounceCount;
        public string      TerminationReason;
        public SurfaceType FinalSurface;
        public float       SimDurationSeconds;
    }
}
