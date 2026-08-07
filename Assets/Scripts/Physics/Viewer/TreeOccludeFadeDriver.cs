using UnityEngine;
using UnityEngine.Rendering;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Genshin/BOTW-style "see-through window": trees standing between the gameplay camera and the
    /// ball fade to a faint dithered ghost inside a soft cone, so the shot is never hidden by canopy.
    ///
    /// Design (spec tree_occlusion_fade §4.1): globals only. This driver publishes four shader globals
    /// per frame and <c>Custom/Vegetation</c> does the rest per-fragment. There is no per-instance state,
    /// no renderer bookkeeping, no material write and no scene wiring — which is what makes it work with
    /// terrain-batched trees, where a per-occluder MaterialPropertyBlock fade is impossible.
    ///
    /// Hooked on <see cref="RenderPipelineManager.beginCameraRendering"/> rather than a LateUpdate: that
    /// is guaranteed to run after <c>ChaseCamera</c> has moved the camera for this frame, which a
    /// script-execution-order gamble is not.
    ///
    /// No physics gate: canopy has no colliders (TreeObstacleBaker bakes trunk obstacles for the ball,
    /// not leaves), so a Linecast would miss exactly the thing that blocks the view most. The cone is
    /// purely spatial — with nothing inside it, zero pixels change.
    /// </summary>
    public static class TreeOccludeFadeDriver
    {
        // ── Tunables (public statics, TreeWindDriver precedent — tweak from the console on device) ──

        /// Full-fade cone half-angle around the camera->ball ray, degrees.
        ///
        /// 45/60 is Cesar's call (2026-08-07) after seeing the A/B video, replacing the SPEC's 10/16.
        /// The gate is ANGULAR, so a near occluder subtends a huge screen area while still sitting
        /// mostly outside a narrow cone: at 10/16 a trunk a metre or two from the camera filled
        /// two-thirds of the frame and barely faded at all. 45/60 is what actually opens a window on
        /// the lies that hide the shot.
        public static float InnerHalfAngleDeg = 45f;

        /// Half-angle at which the fade has fallen back to zero, degrees. Must exceed
        /// <see cref="InnerHalfAngleDeg"/>; the gap is the soft spatial edge that prevents a hard cut.
        public static float OuterHalfAngleDeg = 60f;

        /// Fraction of fragments removed at full fade. 0.85 leaves a ~15% dithered ghost (Cesar's pick).
        public static float MaxOpacityCut = 0.85f;

        /// Softening band, in metres, on the "in front of the ball" depth test.
        public static float DepthFeatherM = 1.5f;

        /// A fragment must be at least this much nearer than the ball to fade. Keeps the ball itself,
        /// the green behind it and everything past it fully solid.
        public static float BallDistBiasM = 0.5f;

        /// Seconds for the window to ramp 0->1 (and back) when it activates/deactivates.
        public static float RampSeconds = 0.25f;

        /// Exponential rate (per second) at which the published focus chases the raw ChaseCamera focus,
        /// so teleports (drop rule, next-hole reset) never snap the window across the screen in one frame.
        public static float FocusSmoothPerSec = 10f;

        /// Debug kill switch. While true the driver publishes strength 0 every frame, which makes the
        /// shader path a literal no-op — i.e. exact pre-change rendering.
        public static bool Disabled;

        // ── Shader globals ────────────────────────────────────────────────────────────────────────

        static readonly int BallId     = Shader.PropertyToID("_GolfinOccFadeBall");
        static readonly int CamId      = Shader.PropertyToID("_GolfinOccFadeCam");
        static readonly int StrengthId = Shader.PropertyToID("_GolfinOccFadeStrength");
        static readonly int ParamsId   = Shader.PropertyToID("_GolfinOccFadeParams");
        static readonly int BiasId     = Shader.PropertyToID("_GolfinOccFadeBias");

        // ── State ─────────────────────────────────────────────────────────────────────────────────

        static float       _strength;
        static Vector3     _focus;
        static Vector3     _camPos;
        static bool        _hasFocus;
        static int         _lastFrame = -1;

        static ChaseCamera         _chase;
        static float               _nextChaseSearch;
        static LoopCameraDirector  _director;
        static float               _nextDirectorSearch;
        static MapViewController   _mapView;
        static float               _nextMapSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            // Domain-reload rule: statics survive nothing, and with "Enter Play Mode Options" they
            // survive everything. Re-arm from a known state either way.
            _strength      = 0f;
            _focus         = Vector3.zero;
            _camPos        = Vector3.zero;
            _hasFocus      = false;
            _lastFrame       = -1;
            _chase              = null;
            _nextChaseSearch    = 0f;
            _director           = null;
            _nextDirectorSearch = 0f;
            _mapView         = null;
            _nextMapSearch   = 0f;
            Disabled       = false;

            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;

            // Push strength 0 immediately so a stale global from a previous editor run can never leak
            // into the first rendered frame.
            Publish();
        }

        static void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            // Gameplay camera only — scene view, preview and the map-view camera must never drive this.
            if (cam == null) return;

            // beginCameraRendering can fire more than once per frame (several cameras, several passes);
            // integrate the state machine once. The globals are FRAME state, not per-camera state, so it
            // does not matter which camera triggers the tick — only that it lands after ChaseCamera's
            // LateUpdate, which beginCameraRendering guarantees.
            if (Time.frameCount == _lastFrame) { Publish(); return; }
            _lastFrame = Time.frameCount;

            var chase = ResolveChase();

            Vector3 raw  = Vector3.zero;
            bool haveRaw = chase != null && chase.isActiveAndEnabled && TryResolveFocus(chase, out raw);
            bool active  = !Disabled && haveRaw && !IsMapOpen();

            if (active)
            {
                // Cone origin is the GAMEPLAY camera — the one carrying ChaseCamera.
                _camPos   = chase.transform.position;
                _focus    = _hasFocus ? StepFocus(_focus, raw, Time.deltaTime, FocusSmoothPerSec) : raw;
                _hasFocus = true;
            }
            else
            {
                // Hole unloaded / map open / killed: drop the focus latch so the next activation snaps
                // to the real ball instead of sliding in from wherever the last hole left it.
                _hasFocus = false;
            }

            _strength = StepStrength(_strength, active ? 1f : 0f, Time.deltaTime, RampSeconds);
            Publish();
        }

        static void Publish()
        {
            Shader.SetGlobalVector(BallId, new Vector4(_focus.x, _focus.y, _focus.z, 0f));
            Shader.SetGlobalVector(CamId, new Vector4(_camPos.x, _camPos.y, _camPos.z, 0f));
            Shader.SetGlobalFloat (StrengthId, _strength);
            Shader.SetGlobalVector(ParamsId, BuildParams(InnerHalfAngleDeg, OuterHalfAngleDeg, MaxOpacityCut, DepthFeatherM));
            Shader.SetGlobalFloat (BiasId, BallDistBiasM);
        }

        /// Find the gameplay camera by its ChaseCamera component rather than by the MainCamera tag:
        /// during a hole the tag stays on the ShellScene camera, so `Camera.main` is NOT the camera the
        /// player is looking through. Cached; the search only re-runs while the reference is null
        /// (before a hole loads, or after a scene unload destroyed it) and is throttled.
        static ChaseCamera ResolveChase()
        {
            if (_chase == null && Time.unscaledTime >= _nextChaseSearch)
            {
                _nextChaseSearch = Time.unscaledTime + 0.25f;
                _chase = Object.FindFirstObjectByType<ChaseCamera>();
            }
            return _chase;
        }

        /// Resolve what the shot is centred on this frame.
        ///
        /// PREMISE CORRECTION (verified in play mode on Hole 1, 2026-08-07): SPEC §4.1 assumed
        /// `ChaseCamera.CurrentFocus` covers aiming as well as flight because the resting ball sits at
        /// `_shotOrigin`. It does not. LoopCameraDirector only calls SetTarget/ResetToOrigin from
        /// ArmChaseForShot, and deliberately leaves the chase camera dormant before then ("the dormant
        /// camera writes nothing"), so at the tee `_target` is null and `_shotOrigin` is (0,0,0) —
        /// which would have parked the cone on the world origin for the entire aiming phase.
        ///
        /// So: use the chase focus when it is live (flight, armed shots, every terminal mode) and fall
        /// back to the live ball transform while it is not (aiming). Returns false when neither exists,
        /// which drives strength to 0 rather than aiming the cone at (0,0,0).
        static bool TryResolveFocus(ChaseCamera chase, out Vector3 focus)
        {
            // The LIVE BALL is the right focus in every phase — aiming (at rest), flight (moving) and
            // after the shot (at rest again) — so it is tried FIRST.
            //
            // ChaseCamera.CurrentFocus is only a fallback, because it is wrong in two of those three
            // phases: `_target` is null until ArmChaseForShot runs AND again after the terminal
            // SetTarget(null), and in both cases CurrentFocus degrades to `_shotOrigin`. Before the
            // first shot that is (0,0,0); after a shot it is the origin of the shot that just FINISHED,
            // i.e. a point behind the camera. Preferring it put the cone behind the viewer and made the
            // window a no-op at exactly the moment the ball is sitting behind a tree (caught on video,
            // 2026-08-07: focus=(219,11,35) = the tee, camera=(163,13,22) downrange).
            if (_director == null && Time.unscaledTime >= _nextDirectorSearch)
            {
                _nextDirectorSearch = Time.unscaledTime + 0.25f;
                _director = Object.FindFirstObjectByType<LoopCameraDirector>();
            }

            var ball = _director != null ? _director.CurrentBall : null;
            if (ball != null)
            {
                focus = ball.position;
                return true;
            }

            focus = chase.CurrentFocus;
            return focus.sqrMagnitude > 1e-6f;
        }

        static bool IsMapOpen()
        {
            // Same gate the tee-idle glow uses. Resolved by search (not a SerializeField) because this
            // driver is deliberately scene-wiring-free; the search is throttled and only runs while the
            // reference is null (i.e. before the hole loads, or after a scene unload destroyed it).
            if (_mapView == null && Time.unscaledTime >= _nextMapSearch)
            {
                _nextMapSearch = Time.unscaledTime + 0.5f;
                _mapView = Object.FindFirstObjectByType<MapViewController>();
            }
            return _mapView != null && _mapView.IsOpen;
        }

        // ── Pure logic (static-testable; see TreeOccludeFadeDriverTests) ──────────────────────────

        /// Move <paramref name="current"/> toward <paramref name="target"/> at 1/rampSeconds per second,
        /// clamped so it lands exactly on the target and never overshoots.
        public static float StepStrength(float current, float target, float dt, float rampSeconds)
        {
            if (rampSeconds <= 0f) return Mathf.Clamp01(target);
            float step = Mathf.Max(dt, 0f) / rampSeconds;
            return Mathf.Clamp01(Mathf.MoveTowards(current, Mathf.Clamp01(target), step));
        }

        /// Frame-rate-independent exponential approach. Never overshoots for any dt or rate >= 0.
        public static Vector3 StepFocus(Vector3 current, Vector3 target, float dt, float ratePerSec)
        {
            if (ratePerSec <= 0f || dt <= 0f) return current;
            float t = 1f - Mathf.Exp(-ratePerSec * dt);
            return Vector3.LerpUnclamped(current, target, Mathf.Clamp01(t));
        }

        /// Pack the cone/opacity/feather tunables the way the shader reads them:
        /// x = cos(outer), y = cos(inner), z = maxCut, w = depth feather (metres, never zero).
        public static Vector4 BuildParams(float innerDeg, float outerDeg, float maxCut, float featherM)
        {
            // The outer angle must be the wider one or the smoothstep inverts and the window turns
            // inside-out; clamp rather than trust the caller.
            float inner = Mathf.Clamp(innerDeg, 0f, 89.9f);
            float outer = Mathf.Clamp(Mathf.Max(outerDeg, inner + 0.01f), 0f, 89.9f);
            return new Vector4(
                Mathf.Cos(outer * Mathf.Deg2Rad),
                Mathf.Cos(inner * Mathf.Deg2Rad),
                Mathf.Clamp01(maxCut),
                Mathf.Max(featherM, 1e-4f));
        }
    }
}
