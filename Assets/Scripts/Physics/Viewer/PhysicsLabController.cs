using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.ShotUI;

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
        // Drives BuildGroundProvider/BuildSurfaceProvider to use scene-raycast providers.
        bool _useSceneProviders;
        Vector3 _loadedHoleGreenCentroid;
        bool    _greenCentroidValid;
        Transform _runtimeTeeAnchor;

        [Header("References")]
        [SerializeField] TrajectoryRenderer trajectoryRenderer;
        [SerializeField] BallAnimator       ballAnimator;
        [SerializeField] ChaseCamera        chaseCamera;

        [Header("Shot Controller (Live Touch)")]
        [SerializeField] ShotController _shotController;
        [SerializeField] ShotConeView   _shotConeView;
        [SerializeField] Transform      _ballSpawnPoint;

        [Header("Camera")]
        [Tooltip("Initial look direction (XZ). Leave zero to auto-derive from scene type.")]
        [SerializeField] Vector3 _defaultLookDirection = Vector3.zero;
        [SerializeField] float   _orbitSensitivity     = 0.5f;

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
        public AeroConfig    AeroCfg    { get; private set; }
        public WindConfig    WindCfg    { get; private set; }
        public SurfaceConfig SurfaceCfg { get; private set; }
        public PuttConfig    PuttCfg    { get; private set; }

        Trajectory _previousTrajectory;

        // Camera orbit state
        float   _cameraYaw;
        Vector3 _orbitCenter;
        bool    _prevBallPlaying;
        bool    _orbitDragActive;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            EnsureConfigsLoaded();

            if (_shotController != null)
                _shotController.OnShotResolved += HandleShotResolved;

            if (_shotConeView != null)
            {
                if (chaseCamera != null)
                    _shotConeView.SetCamera(chaseCamera.GetComponent<Camera>());
                _shotConeView.SetMaxCarryYards(ComputeMaxCarryYards());
            }

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
        }

        void Start()
        {
            // Disable raw-touch path — ClubHandle external drag API is the only input in this lab.
            // This prevents camera drags and button clicks from accidentally starting a shot.
            _shotController?.InjectInputSource(null);

            // Wait 2 frames so any additively-loaded hole scene finishes loading,
            // then scan for it. This replaces the fragile immediate scan.
            StartCoroutine(ScanForLoadedHoleSceneAtStartup());
        }

        System.Collections.IEnumerator ScanForLoadedHoleSceneAtStartup()
        {
            yield return null;
            yield return null;

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
            Debug.Log("[PhysicsLab] No hole scene loaded at startup — flat-ground fallback.");
            SetupAtTee();
        }

        void Update() => HandleCameraOrbit();

        // ── Public API ─────────────────────────────────────────────────────────

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

        public void SetClub(int index)
        {
            if (_shotController == null || index < 0 || index >= LabClubs.Length) return;
            bool isPutt = index == LabClubs.Length - 1;
            _shotController.IsPutt = isPutt;
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
            if (_ballSpawnPoint == null) return;
            Vector3 sp = _ballSpawnPoint.position;
            float surfaceY = SurfaceSnap(sp.x, sp.z, sp.y, 6); // 6 = Golfin.Course.SurfaceType.Tee
            Vector3 teePos = new Vector3(sp.x, surfaceY, sp.z);

            _orbitCenter = teePos;

            if (ballAnimator != null) ballAnimator.PlaceAtRest(teePos);

            Vector3 lookDir = GetDefaultLookDirection();
            _cameraYaw = Mathf.Atan2(lookDir.z, lookDir.x);

            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            Camera cam = chaseCamera?.GetComponent<Camera>();
            if (cam != null) ApplyCameraYaw(cam);

            // Putt mode: switch to ground-level camera for close-range view.
            if (_shotController != null && _shotController.IsPutt && chaseCamera != null)
                chaseCamera.SetMode(ChaseCamera.Mode.GroundLevel);
        }

        // Teleport the ball to a world position (Y resolved via type-aware downward raycast).
        // preferredSurfaceTypeValue: Golfin.Course.SurfaceType int value (1=Green, 4=Bunker, etc.) or null.
        // One-shot placement; subsequent shots continue from wherever the ball lands.
        public void PlaceBallAt(Vector3 worldPos, int? preferredSurfaceTypeValue = null)
        {
            if (_shotController != null) _shotController.CompleteShot();

            float y   = SurfaceSnap(worldPos.x, worldPos.z, worldPos.y, preferredSurfaceTypeValue);
            Vector3 pos = new Vector3(worldPos.x, y, worldPos.z);

            _orbitCenter = pos;
            if (ballAnimator != null) ballAnimator.PlaceAtRest(pos);

            Vector3 lookDir = GetDefaultLookDirection();
            _cameraYaw = Mathf.Atan2(lookDir.z, lookDir.x);
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            Camera cam = chaseCamera?.GetComponent<Camera>();
            if (cam != null) ApplyCameraYaw(cam);

            if (_shotController != null && _shotController.IsPutt && chaseCamera != null)
                chaseCamera.SetMode(ChaseCamera.Mode.GroundLevel);

            AdjustCameraForDepression(pos);
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

        // ── Camera orbit ───────────────────────────────────────────────────────

        void HandleCameraOrbit()
        {
            if (_shotController != null && _shotController.IsExternalDragActive) return;

            // Orbit only makes sense in Chase mode; Overhead/Ground manage themselves.
            if (chaseCamera != null && chaseCamera.CurrentMode != ChaseCamera.Mode.Chase) return;

            bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;

            if (_prevBallPlaying && !isPlaying)
            {
                if (ballAnimator?.CurrentBall != null)
                    _orbitCenter = ballAnimator.CurrentBall.position;
                if (chaseCamera != null) chaseCamera.SetTarget(null);
                // Allow firing from current lie without requiring Reset to Tee.
                _shotController?.CompleteShot();
            }
            _prevBallPlaying = isPlaying;
            if (isPlaying) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            bool pressing = mouse.leftButton.isPressed;

            // Don't start a new orbit drag while pointer is over UI.
            if (pressing && !_orbitDragActive)
            {
                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (overUI) return;
                _orbitDragActive = true;
            }
            if (!pressing)
            {
                _orbitDragActive = false;
                return;
            }

            float dx = mouse.delta.x.ReadValue();
            if (Mathf.Abs(dx) < 0.5f) return;

            _cameraYaw += dx * _orbitSensitivity * Mathf.Deg2Rad;
            if (_shotController != null)
                _shotController.CameraHeadingRadians = _cameraYaw;

            Camera cam = chaseCamera?.GetComponent<Camera>();
            if (cam != null) ApplyCameraYaw(cam);
        }

        void ApplyCameraYaw(Camera cam)
        {
            // yaw=0 → +X forward (ShotInputBuilder convention: Vx=cos, Vz=sin)
            Vector3 lookDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));
            cam.transform.position = _orbitCenter - lookDir * 8f + Vector3.up * 3f;
            cam.transform.LookAt(_orbitCenter + lookDir * 3f + Vector3.up * 0.5f);
        }

        // ── Preset firing ──────────────────────────────────────────────────────

        public void Fire(ShotPreset preset)
        {
            _previousTrajectory = null;
            FireInternal(preset);
        }

        public void FireCompare(ShotPreset preset)
        {
            if (_previousTrajectory != null)
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
            trajectoryRenderer.Draw(last);
            ballAnimator.Play(last);
        }

        public void Clear()
        {
            trajectoryRenderer.Clear();
            _previousTrajectory = null;
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
            fp3 ballOrigin = GetCurrentOrigin(fallbackToInput: input.origin);
            var correctedInput = new ShotInput(ballOrigin, input.velocity, input.maxDuration, input.Spin, input.seed);

            var trajectory = RunSimFromController(correctedInput, ballMods);
            _previousTrajectory = trajectory;

            trajectoryRenderer.Draw(trajectory);
            ballAnimator.Play(trajectory);

            if (_shotConeView != null && ballAnimator?.CurrentBall != null)
                _shotConeView.SetBallTransform(ballAnimator.CurrentBall);

            var s0 = trajectory.samples != null && trajectory.samples.Count > 0
                ? trajectory.samples[0].position : correctedInput.origin;
            Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
            Vector3 launchDir = new Vector3(correctedInput.velocity.x.ToFloat(), 0f,
                                             correctedInput.velocity.z.ToFloat()).normalized;
            if (launchDir == Vector3.zero) launchDir = Vector3.right;

            _orbitCenter = origin;

            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(ballAnimator.CurrentBall);
                chaseCamera.ResetToOrigin(origin, launchDir);
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

        Trajectory RunSimFromController(ShotInput input, BallPhysicsModifiers ballMods)
        {
            var ground  = BuildGroundProvider();
            var surface = BuildSurfaceProvider(default(ShotPreset));
            return BallSimulation.Simulate(input, ground, AeroCfg, WindCfg, surface, SurfaceCfg, PuttCfg, ballMods);
        }

        bool _configsLoaded;
        void EnsureConfigsLoaded()
        {
            if (_configsLoaded) return;
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

        // ── Internal ───────────────────────────────────────────────────────────

        void FireInternal(ShotPreset preset)
        {
            var trajectory = RunSimForCamera(preset);
            _previousTrajectory = trajectory;

            trajectoryRenderer.Draw(trajectory);
            ballAnimator.Play(trajectory);

            var s0 = trajectory.samples != null && trajectory.samples.Count > 0
                ? trajectory.samples[0].position : preset.Origin;
            Vector3 origin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
            Vector3 launchDir = new Vector3(Mathf.Cos(_cameraYaw), 0f, Mathf.Sin(_cameraYaw));

            _orbitCenter = origin;

            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(ballAnimator.CurrentBall);
                chaseCamera.ResetToOrigin(origin, launchDir);
            }

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
            return BallSimulation.Simulate(input, ground, AeroCfg, preset.Wind, surface, SurfaceCfg, PuttCfg);
        }

        // Returns current ball position snapped to terrain, or fallback if no ball.
        fp3 GetCurrentOrigin(fp3 fallbackToInput)
        {
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
            if (currentScene == PresetScene.Hole1 || _useSceneProviders)
                return new SceneGroundProvider();
            return new FlatGround(fp.Zero);
        }

        ISurfaceProvider BuildSurfaceProvider(ShotPreset preset)
        {
            if (currentScene == PresetScene.Hole1 || _useSceneProviders)
                return new SceneSurfaceProvider();
            SurfaceType surfaceType = preset.HasSurfaceOverride ? preset.SurfaceOverride : SurfaceType.Fairway;
            return new ConstantSurfaceProvider(surfaceType);
        }

        // Called by LabHoleBinder when a Hole_XX_Geo scene is opened additively.
        public void OnHoleLoaded(string sceneName)
        {
            _useSceneProviders = true;

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
            if (regularMarkers.Count > 0)
            {
                foreach (var t in regularMarkers) teePos += t.position;
                teePos /= regularMarkers.Count;
                teeFound = true;
                Debug.Log($"[PhysicsLab] OnHoleLoaded: {sceneName} — tee midpoint from {regularMarkers.Count} TeeMarker_regular_* GOs at {teePos:F2}");
            }
            else if (teeGOs.Count > 0)
            {
                foreach (var g in teeGOs) teePos += g.transform.position;
                teePos /= teeGOs.Count;
                teeFound = true;
                Debug.Log($"[PhysicsLab] OnHoleLoaded: {sceneName} — tee midpoint from {teeGOs.Count} SurfaceMarker tees (fallback) at {teePos:F2}");
            }

            if (teeFound)
            {
                if (_runtimeTeeAnchor == null)
                {
                    var go = new GameObject("_RuntimeTeeAnchor");
                    go.transform.SetParent(transform);
                    _runtimeTeeAnchor = go.transform;
                }
                _runtimeTeeAnchor.position = teePos;
                _ballSpawnPoint = _runtimeTeeAnchor;
            }
            else
            {
                Debug.LogWarning($"[PhysicsLab] OnHoleLoaded: no tee markers found in {sceneName}.");
            }

            if (_shotConeView != null)
                _shotConeView.SetMaxCarryYards(ComputeMaxCarryYards());

            // Populate ball placement entries for the Place Ball dropdown in the lab UI.
            BuildPlacementEntries(teeFound, teePos, greenGOs, bunkerGOs, fairwayGOs, waterGOs);

            // Copy all lighting settings from the hole scene into LabScaffold so URPWater
            // gets the same environment (skybox, ambient, fog, reflections) it would have
            // when the hole is loaded standalone.
            CopyHoleLighting(SceneManager.GetSceneByName(sceneName));

            SetupAtTee();
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

        // Called by LabHoleBinder when the loaded hole scene is closed.
        public void OnHoleUnloaded()
        {
            _useSceneProviders   = false;
            _greenCentroidValid  = false;
            _ballSpawnPoint      = null;

            PlacementEntries.Clear();
            OnPlacementEntriesChanged?.Invoke();

            // Restore LabScaffold as active scene.
            Scene scaffoldScene = SceneManager.GetSceneByName("LabScaffold");
            if (scaffoldScene.IsValid())
                SceneManager.SetActiveScene(scaffoldScene);

            Debug.Log("[PhysicsLab] OnHoleUnloaded — reverted to flat-ground fallback.");
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
