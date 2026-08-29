#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Gameplay.UI.HUD;
using Golfin.Physics.Viewer;
using Golfin.Physics.Viewer.Editor;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Video + invariant proof for `shot_aim_parity` (2026-08-29).
    ///
    /// Claim under test: **the ball goes where the targeting line points.**
    /// Before the fix `ShotController.PublishState` drew the line at
    /// `heading + finetune * HalfConeAngleRad()` (±11° at the median club) while
    /// `ShotController.CommitFlick` fired the ball at `heading + finetune * AimNudgeRangeRad`
    /// (±3°) — a ~3.7x disagreement that read in-game as "the flick always fires centered".
    ///
    /// Shows, through the REAL player entry path and nothing else:
    ///   boot → PLAY → Hole 1 card → hole load → (recording starts) →
    ///   pull the club handle to the FAR RIGHT edge of the cone, wobble the thumb down
    ///   (the D3 unlatch) and flick → CENTRE → FAR LEFT → arm Fade/Draw and flick right.
    ///
    /// The drag is dispatched as genuine `IPointerDownHandler` / `IDragHandler` /
    /// `IPointerUpHandler` events on the real <see cref="ClubHandleDragger"/> — the same
    /// component a thumb drives (PIPELINE_HARDENING Rule 2). Nothing calls
    /// `ShotController.SetExternalPower` directly, so the cone-local px→finetune mapping,
    /// the peak-power latch, the windowed flick gate and the upswing aim latch are all
    /// exercised for real.
    ///
    /// The gate is a deterministic invariant JSON (PIPELINE_HARDENING Rule 3), NOT a human
    /// reading the video:
    ///   Docs/Diagnostics/_capture/shot_aim_parity_invariants.json
    ///
    /// The two numbers that matter per shot are measured from opposite ends of the system
    /// and never from the same formula:
    ///   * `lineYaw`  — the last published `ShotInputState.AimYawRadians` before the commit.
    ///                  This is the exact field `ShotConeView.UpdateTargetingLine` turns into
    ///                  the on-screen line, read off the event, not recomputed.
    ///   * `ballYaw`  — the bearing of the ball's own world motion over its first ~25 m of
    ///                  flight, sampled off `BallAnimator.Instance.CurrentBall`. No access to
    ///                  ShotInput, no formula.
    /// If those two agree, the line told the truth.
    ///
    /// NOTE on the SIGN (Cesar, 2026-08-29): the handle is the CLUB's position relative to the
    /// ball, not a pointer at the target. Placing the club LEFT of the ball sends the ball RIGHT,
    /// exactly as in real golf, so the targeting line leans OPPOSITE the handle by design. The
    /// `*_A5` assertions below lock that inverse relationship in so a later change cannot quietly
    /// "correct" it.
    ///
    /// RECORDING IS NOT HAND-ROLLED — it goes through the sanctioned engine
    /// <see cref="BotVideoRecorder"/> via CustomOutputPath + ArmDeferred/BeginDeferred, exactly
    /// as PracticeMapDuringShotDemoRecorder does. No stills are taken while the recorder runs
    /// (that is the documented Y-flip trigger); the report's frames are extracted from the mp4
    /// afterwards.
    ///
    /// Output raw: tasks/loop_v2_smoke_bot/shot_aim_parity/video/raw.mp4
    /// Captions:   tasks/loop_v2_smoke_bot/shot_aim_parity/screenshots/history.log
    /// Usage: GOLFIN > ShotUI > Record Shot Aim Parity Demo
    /// </summary>
    public static class ShotAimParityDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ScenarioDir    = "tasks/loop_v2_smoke_bot/shot_aim_parity";
        const string ArmedKey       = "ShotAimParityDemoRecorder.Armed";
        const string ScenarioKey    = "shot_aim_parity";

        internal const string InvariantsPath =
            "Docs/Diagnostics/_capture/shot_aim_parity_invariants.json";

        /// <summary>Four shots plus hole load; BotVideoRecorder's default watchdog is 30s.</summary>
        const int WatchdogSeconds = 240;

        static StringBuilder _log;

        internal static string VideoDir => $"{ScenarioDir}/video";
        internal static string ShotsDir => $"{ScenarioDir}/screenshots";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/ShotUI/Record Shot Aim Parity Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[AimParityDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(VideoDir);
            Directory.CreateDirectory(ShotsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(InvariantsPath));

            // ResetSessionGuard: this harness records exactly ONE clip. The guard exists to stop
            // batch accumulation wedging the GPU; SessionState
            // "LoopV2SmokeBot.RecordedThisEditorSession" was probed False before this launch
            // (no BotVideoRecorder run has completed this Editor session), so the reset is a
            // no-op kept only so a re-run inside the same session still works.
            BotVideoRecorder.ResetSessionGuard();
            LoopV2SmokeBot.Scenario = ScenarioKey;
            BotVideoRecorder.CustomOutputPath = $"{VideoDir}/raw";
            BotVideoRecorder.MaxRecordSecondsSessionOverride = WatchdogSeconds;
            BotVideoRecorder.ArmDeferred();

            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[AimParityDemo] Armed. Entering play mode (deferred recording)...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                Application.runInBackground = true;   // MANDATORY for MCP-driven runs
                BotVideoRecorder.Begin();             // no-op for a deferred arm

                var host = new GameObject("[ShotAimParityBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<ShotAimParityRunner>();
                Debug.Log("[AimParityDemo] Bot spawned. Waiting for hole load...");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                WriteCaptionLog();
            }
        }

        public static void StartRecorder()
        {
            _log = new StringBuilder();
            BotVideoRecorder.BeginDeferred();
        }

        /// <summary>Emit one caption line for build_bot_video.py --mode steps.</summary>
        public static void Step(string text)
        {
            if (_log == null) return;
            float t = Time.realtimeSinceStartup;
            _log.AppendLine($"[t={t.ToString("F3", CultureInfo.InvariantCulture)}] Step: '{text}'");
            Debug.Log($"[AimParityDemo] Step: {text}");
        }

        static void WriteCaptionLog()
        {
            if (_log == null) return;
            Directory.CreateDirectory(ShotsDir);
            File.WriteAllText($"{ShotsDir}/history.log", _log.ToString());
            _log = null;
            Debug.Log($"[AimParityDemo] history.log written under {ShotsDir}");
        }
    }

    /// <summary>
    /// Runtime coroutine driver. Real widget pointer events only.
    ///
    /// ShotController lives in `Golfin.Gameplay.Input`, which is `autoReferenced:false`, so the
    /// predefined editor assembly cannot see the type — every touch of it here goes through
    /// reflection, the same way PracticeMapDuringShotRunner does it.
    /// </summary>
    public class ShotAimParityRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags PI = BindingFlags.Public    | BindingFlags.Instance;
        // Hole 4, not Hole 1. Hole 1 is a tree-lined chute: every stroke of the first cut ran into
        // the tree wall ~50 m off the tee, so the clip showed the ball vanishing into foliage
        // instead of flying (Cesar: "you hit too many trees"). Hole 4 is the most open hole on the
        // course — 133 standalone trees against Hole 1's forest, and the map render shows trees
        // only around the perimeter of a wide field, so a ±8° shot stays in the open on BOTH sides.
        // Tried in order; the first hole card that is present and clickable wins.
        // Hole 10 first: a par 4, so it can absorb four strokes, and its map shows a broad open
        // lower half with the trees pushed to one edge. Hole 4 is more open still but it is a
        // 121-yard par 3 — the first cut there put the driver 195 m downrange and two strokes
        // went OB, which is its own kind of unwatchable. Tried in order; first clickable wins.
        static readonly int[] HolePreference = { 10, 16, 9, 4, 1 };
        int _holeNumber = 10;

        // 0.45, not 0.75: at 0.75 the first two strokes ate most of the hole and strokes 3-4
        // became short approach shots whose flight never cleared the sampler's 25 m window
        // (run 1, 2026-08-29). At 0.45 all four strokes stay mid-iron shots inside the open field.
        const float PeakPower    = 0.45f;

        // ── reflected ShotController surface ──────────────────────────────────
        Component    _sc;
        PropertyInfo _pHeading, _pHalfConeDeg, _pConeFinetune, _pHandleFinetune,
                     _pAimLocked, _pState, _pFadeActive, _pLockedAim;
        Delegate     _stateDelegate;
        EventInfo    _stateEvent;

        // ── last published ShotInputState, read off the event ─────────────────
        FieldInfo _fAim, _fCone, _fDegrading, _fPass, _fState;
        float  _lineYaw, _publishedFinetune;
        bool   _publishedDegrading;
        int    _publishedPass;
        string _publishedState = "?";

        // ── real widget refs ──────────────────────────────────────────────────
        ClubHandleDragger _dragger;
        ShotConeView      _coneView;
        Camera            _worldCam;      // the camera ShotConeView projects the line with
        RectTransform     _coneRect;
        ConeMeshGraphic   _coneGraphic;
        Camera            _uiCam;

        readonly List<Assertion> _asserts = new List<Assertion>();

        class Assertion { public string id, description, expected, actual, verdict; }

        void Assert(string id, string description, object expected, object actual)
        {
            bool ok = string.Equals(expected?.ToString(), actual?.ToString(), StringComparison.Ordinal);
            Add(id, description, expected?.ToString(), actual?.ToString(), ok ? "PASS" : "FAIL");
        }

        void AssertNear(string id, string description, float expected, float actual, float tol)
        {
            bool ok = Mathf.Abs(expected - actual) <= tol;
            Add(id, description,
                $"{F(expected)} (±{F(tol)})", $"{F(actual)}  [Δ={F(Mathf.Abs(expected - actual))}]",
                ok ? "PASS" : "FAIL");
        }

        /// <summary>
        /// Same as AssertNear but for yaws, where −3.06 and +2.88 are 0.34 rad apart, not 5.95.
        /// Hole 1 aims at roughly −π, so every straight-mode comparison here straddles the wrap;
        /// a plain subtraction reported a 5.95 rad "error" on a shot that was in fact exact.
        /// </summary>
        void AssertNearAngle(string id, string description, float expected, float actual, float tol)
        {
            float d = Mathf.Abs(WrapPi(expected - actual));
            Add(id, description,
                $"{F(expected)} (±{F(tol)} rad, wrapped)", $"{F(actual)}  [Δ={F(d)}]",
                d <= tol ? "PASS" : "FAIL");
        }

        void AssertTrue(string id, string description, bool condition, string actual)
            => Add(id, description, "true", actual, condition ? "PASS" : "FAIL");

        void Note(string id, string description, object value)
            => Add(id, description, "(informational)", value?.ToString(), "INFO");

        void Add(string id, string description, string expected, string actual, string verdict)
        {
            _asserts.Add(new Assertion
            {
                id = id, description = description,
                expected = expected ?? "null", actual = actual ?? "null", verdict = verdict
            });
            Debug.Log($"[AimParityDemo] {verdict} {id}: {description} (expected={expected}, actual={actual})");
        }

        static string F(float v) => v.ToString("F5", CultureInfo.InvariantCulture);

        void Start() => StartCoroutine(Sequence());

        // ── boot-flow helpers (same shape as PracticeMapDuringShotRunner) ─────
        static Button FindButton(string goName) => UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == goName);

        static void ClickReal(Button b)
        {
            var ped = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerUpHandler);
            b.onClick.Invoke();
        }

        static string VisibleButtons() => "buttons: " + string.Join(", ", UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(b => b.gameObject.name).Distinct().OrderBy(n => n).Take(25));

        IEnumerator ClickWhenPresent(string goName, float timeout = 90f)
        {
            float t = 0f;
            while (t < timeout)
            {
                var b = FindButton(goName);
                if (b != null) { ClickReal(b); yield break; }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Debug.LogWarning($"[AimParityDemo] TIMEOUT waiting for '{goName}'");
        }

        static bool HoleCardExists(int hole)
        {
            foreach (var c in UnityEngine.Object
                         .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                         .Where(m => m.GetType().Name == "HoleCardController"))
            {
                var pr = c.GetType().GetProperty("HoleNumber");
                if (pr == null || (int)pr.GetValue(c) != hole) continue;
                if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button b && b.interactable)
                    return true;
            }
            return false;
        }

        IEnumerator ClickHoleCard(int hole, float timeout = 60f)
        {
            float t = 0f;
            while (t < timeout)
            {
                foreach (var c in UnityEngine.Object
                             .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                             .Where(m => m.GetType().Name == "HoleCardController"))
                {
                    var p = c.GetType().GetProperty("HoleNumber");
                    if (p == null || (int)p.GetValue(c) != hole) continue;
                    if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button btn)
                    { ClickReal(btn); yield break; }
                }
                yield return new WaitForSecondsRealtime(0.25f); t += 0.25f;
            }
            Debug.LogWarning($"[AimParityDemo] TIMEOUT waiting for hole {hole} card");
        }

        // ── ShotController binding (reflection, incl. the state event) ────────
        bool BindShotController()
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                       .FirstOrDefault(a => a.GetName().Name == "Golfin.Gameplay.Input")
                       ?.GetType("Golfin.Gameplay.Input.ShotController");
            if (t == null) return false;
            _sc = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault() as Component;
            if (_sc == null) return false;

            _pHeading        = t.GetProperty("CameraHeadingRadians", PI);
            _pHalfConeDeg    = t.GetProperty("ConeHalfAngleDeg",     PI);
            _pConeFinetune   = t.GetProperty("ConeFinetune",         PI);
            _pHandleFinetune = t.GetProperty("HandleFinetune",       PI);
            _pAimLocked      = t.GetProperty("IsAimLocked",          PI);
            _pState          = t.GetProperty("State",                PI);
            _pFadeActive     = t.GetProperty("FadeDrawActive",       PI);
            _pLockedAim      = t.GetProperty("FadeDrawLockedAimRad", PI);

            // Subscribe to OnStateChanged without being able to name ShotInputState:
            // instantiate a generic handler to the event's own argument type.
            _stateEvent = t.GetEvent("OnStateChanged");
            if (_stateEvent == null) return false;
            Type handlerType = _stateEvent.EventHandlerType;              // Action<ShotInputState>
            Type stateType   = handlerType.GetGenericArguments()[0];
            MethodInfo mi = GetType()
                .GetMethod(nameof(OnStatePublished), NP)
                .MakeGenericMethod(stateType);
            _stateDelegate = Delegate.CreateDelegate(handlerType, this, mi);
            _stateEvent.AddEventHandler(_sc, _stateDelegate);

            return _pHeading != null && _pHalfConeDeg != null && _pConeFinetune != null
                && _pAimLocked != null && _pState != null;
        }

        /// <summary>Event sink. Reads the published aim straight off the struct the UI draws from.</summary>
        void OnStatePublished<T>(T published)
        {
            if (_fAim == null)
            {
                Type tt = typeof(T);
                _fAim       = tt.GetField("AimYawRadians");
                _fCone      = tt.GetField("ConeFinetuneX");
                _fDegrading = tt.GetField("IsDegrading");
                _fPass      = tt.GetField("PassIndex");
                _fState     = tt.GetField("State");
            }
            object box = published;
            if (_fAim       != null) _lineYaw            = (float)_fAim.GetValue(box);
            if (_fCone      != null) _publishedFinetune  = (float)_fCone.GetValue(box);
            if (_fDegrading != null) _publishedDegrading = (bool)_fDegrading.GetValue(box);
            if (_fPass      != null) _publishedPass      = (int)_fPass.GetValue(box);
            if (_fState     != null) _publishedState     = _fState.GetValue(box)?.ToString();
        }

        float Heading      => (float)_pHeading.GetValue(_sc);
        float HalfConeDeg  => (float)_pHalfConeDeg.GetValue(_sc);
        float AimFinetune  => (float)_pConeFinetune.GetValue(_sc);
        bool  AimLocked    => (bool)_pAimLocked.GetValue(_sc);
        string StateName   => _pState.GetValue(_sc)?.ToString();

        // ── real drag driving ─────────────────────────────────────────────────
        float ConeHeightPx => _coneGraphic != null ? _coneGraphic.HeightPx : 1009f;

        /// <summary>Cone-local point → screen px, the inverse of ClubHandleDragger.ProcessDrag.</summary>
        Vector2 ConeLocalToScreen(float localX, float localY)
        {
            Vector3 world = _coneRect.TransformPoint(new Vector3(localX, localY, 0f));
            return RectTransformUtility.WorldToScreenPoint(_uiCam, world);
        }

        /// <summary>Cone-local point for a given (finetune, power) pair.</summary>
        Vector2 ConeLocalFor(float finetune, float power)
        {
            float h        = ConeHeightPx;
            float localY   = Mathf.Clamp01(1f - power) * h;
            float halfBase = h * Mathf.Tan(HalfConeDeg * Mathf.Deg2Rad);
            float maxX     = halfBase * (1f - localY / h);
            // Overshoot deliberately: ProcessDrag clamps to ±maxX, which is exactly finetune ±1.
            return new Vector2(finetune * maxX * 1.05f, localY);
        }

        PointerEventData _ped;
        Vector2 _lastPointerPos;
        GraphicRaycaster _raycaster;   // supplies PointerEventData.pressEventCamera (read-only prop)

        void PointerDownAt(Vector2 screenPos)
        {
            // pressEventCamera is read-only and derives from pointerPressRaycast.module.eventCamera,
            // so hand the event the real GraphicRaycaster instead of poking the camera in. On an
            // Overlay canvas eventCamera is null, which is exactly what ProcessDrag wants.
            var rr = new RaycastResult { module = _raycaster, screenPosition = screenPos };
            _ped = new PointerEventData(EventSystem.current)
            { position = screenPos, pointerId = 0, button = PointerEventData.InputButton.Left };
            _ped.pointerPressRaycast   = rr;
            _ped.pointerCurrentRaycast = rr;
            _lastPointerPos = screenPos;
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.pointerDownHandler);
        }

        void DragTo(Vector2 screenPos)
        {
            _ped.delta    = screenPos - _lastPointerPos;
            _ped.position = screenPos;
            _lastPointerPos = screenPos;
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.dragHandler);
        }

        void PointerUp()
        {
            ExecuteEvents.Execute(_dragger.gameObject, _ped, ExecuteEvents.pointerUpHandler);
        }

        /// <summary>Interpolated pull-back, one real drag event per rendered frame.</summary>
        IEnumerator DragToLocal(Vector2 fromLocal, Vector2 toLocal, int steps)
        {
            for (int i = 1; i <= steps; i++)
            {
                Vector2 l = Vector2.Lerp(fromLocal, toLocal, i / (float)steps);
                DragTo(ConeLocalToScreen(l.x, l.y));
                yield return null;
            }
        }

        /// <summary>Upward flick: 4 frames of ~0.10 screen-heights each ⇒ well over the 1.2 gate.</summary>
        IEnumerator FlickUp(Vector2 fromLocal)
        {
            float stepPx = Screen.height * 0.10f;
            Vector2 basePos = ConeLocalToScreen(fromLocal.x, fromLocal.y);
            for (int i = 1; i <= 4; i++)
            {
                DragTo(new Vector2(basePos.x, basePos.y + stepPx * i));
                yield return null;
            }
        }

        // ── shot planning: the flight is deterministic, so solve for the power ───
        // PowerGaugeWidget shows the projected carry as `ResolveCarryYards() * PowerNormalized`,
        // i.e. carry is LINEAR in power and the club's rating is ClubContext.SelectedDistance.
        // So instead of guessing a power and re-running (which put the driver 195 m down a
        // 121-yard par 3 and produced two OB banners), pick the distance the stroke SHOULD fly
        // and invert it. Cesar, 2026-08-29: "Preplan your shots. It's deterministic."

        /// <summary>Flat XZ distance ball → pin, in yards.</summary>
        static float DistanceToPinYards()
        {
            Transform b = Ball;
            if (b == null) return 0f;
            Vector3 pin = HoleContext.PinWorld;
            return new Vector2(pin.x - b.position.x, pin.z - b.position.z).magnitude
                 * AutoClubSelector.YardsPerMeter;
        }

        /// <summary>Rated carry of the club currently in hand, yards (the "228 yds" chip).</summary>
        static float ClubCarryYards()
        {
            int d = ClubContext.SelectedDistance;
            return d > 0 ? d : 200f;
        }

        /// <summary>
        /// Power for a stroke that advances roughly <paramref name="fractionOfRemaining"/> of the
        /// way to the pin — never onto the green, never off the end of the hole — and never asks
        /// the club for more than 85 % of its rating.
        /// </summary>
        float PlanPower(string tag, float fractionOfRemaining, out string plan)
        {
            float distYd = DistanceToPinYards();
            float clubYd = ClubCarryYards();
            float targetYd = Mathf.Min(distYd * fractionOfRemaining, clubYd * 0.85f);
            float power = clubYd > 1f ? Mathf.Clamp(targetYd / clubYd, 0.18f, 0.95f) : 0.45f;
            plan = $"pin {distYd:F0}yd, club {clubYd:F0}yd → target {targetYd:F0}yd → power {power:F2}";
            Note($"{tag}_PLAN", "preplanned stroke", plan);
            return power;
        }

        // ── ball measurement (no reflection, no formula) ──────────────────────
        static Transform Ball => BallAnimator.Instance != null ? BallAnimator.Instance.CurrentBall : null;

        static float Bearing(Vector3 from, Vector3 to) => Mathf.Atan2(to.z - from.z, to.x - from.x);

        static float WrapPi(float a)
        {
            while (a >  Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }

        class ShotResult
        {
            public bool  fired;
            public float lineYaw, heading, halfConeDeg, aimFinetune;
            public bool  degrading;
            public int   passIndex;
            public float ballYaw   = float.NaN;   // bearing over the first ~25 m of flight
            public float restYaw   = float.NaN;   // bearing of the final resting place
            public float earlyYaw  = float.NaN;   // bearing while the ball is still near the tee
            public float earlyM;                  // displacement at the early sample
            public float flightM;                 // displacement at the bearing sample
            public float maxHorizM;               // furthest the ball got from the launch point
            public float restM;                   // launch → rest distance
            public float handleScreenDx = float.NaN;  // + = handle right of cone centre, in screen px
            public float lineScreenDx   = float.NaN;  // + = drawn line leans right of the ball, screen px
            public Vector3 launch, rest;

            /// <summary>Total lateral curve of the flight: how far the bearing swings between
            /// the early sample and the resting place. A straight shot's value is the control.</summary>
            public float BendDeg => WrapPi(restYaw - earlyYaw) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// One complete stroke through the real handle: pull to (finetune, PeakPower), optionally
        /// wobble the thumb down mid-pull (the D3 unlatch), flick, then follow the ball.
        /// </summary>
        IEnumerator FireShot(string tag, float finetune, bool withWobble, ShotResult result, float power = PeakPower)
        {
            if (_sessionStalled) yield break;
            Vector3 launch = Ball != null ? Ball.position : Vector3.zero;
            result.launch  = launch;

            // Start BELOW the target power, always. A fixed 0.35 start broke every stroke whose
            // planned power came in under that (S3 at 0.30): the "pull-back" then travelled UP the
            // cone, which is the upswing the aim latch is built to detect, so the aim froze at
            // −0.32 instead of −1.00 and the left-hand shot silently lost its deflection. Power
            // must rise monotonically through the drag for the peak-power latch to mean anything.
            Vector2 startL = ConeLocalFor(0f, Mathf.Max(0.04f, power * 0.30f));
            Vector2 endL   = ConeLocalFor(finetune, power);

            PointerDownAt(ConeLocalToScreen(startL.x, startL.y));
            yield return null;

            if (withWobble)
            {
                // Pull most of the way down with a partial deflection…
                Vector2 midL = ConeLocalFor(finetune * 0.5f, power);
                yield return DragToLocal(startL, midL, 5);

                // …then the thumb wobbles UP past the 1% latch threshold while sliding wider.
                float upPx = Screen.height * 0.025f;
                Vector2 midScreen = ConeLocalToScreen(ConeLocalFor(finetune * 0.9f, power).x, midL.y);
                DragTo(new Vector2(midScreen.x, midScreen.y + upPx));
                yield return null;
                DragTo(new Vector2(midScreen.x, midScreen.y + upPx));
                yield return null;

                Assert($"{tag}_W1", "wobble UP past the threshold latches the aim", true, AimLocked);
                AssertNear($"{tag}_W2", "while latched, sliding wider does NOT steer the aim",
                           finetune * 0.5f, AimFinetune, 0.06f);

                // …and comes back DOWN below the swing's lowest point: D3 must re-open the aim.
                Vector2 lowScreen = ConeLocalToScreen(endL.x, endL.y);
                DragTo(new Vector2(lowScreen.x, lowScreen.y - Screen.height * 0.01f));
                yield return null;

                Assert($"{tag}_W3", "a new lowest point re-opens the aim (D3)", false, AimLocked);
                AssertNear($"{tag}_W4", "and the aim re-syncs to the live handle — no input lost",
                           finetune, AimFinetune, 0.06f);
            }
            else
            {
                yield return DragToLocal(startL, endL, 6);
            }

            // HOLD at full deflection. Two reasons: the SPEC's manual check is "pull to the far
            // edge of the cone, hold, flick", and without the hold the rotated targeting line is
            // on screen for ~3 frames — unreadable in the clip. Kept short because the timing
            // arrow is running: every assertion below still demands IsDegrading == false, so if
            // the hold ever pushes past the clean-pass window the run says so instead of quietly
            // comparing a degraded shot against an undegraded line.
            // Close the loop on the deflection instead of trusting the px→finetune mapping in one
            // shot. Near the apex the cone is only ~50 px wide, so at a low power a few pixels of
            // round-trip error is a large fraction of finetune — the left stroke once reached
            // −0.32 instead of −1.00 and quietly weakened the whole demo. Read the finetune the
            // controller actually published and walk the pointer out until it matches, exactly as
            // a player nudges the handle while watching it.
            for (int i = 0; i < 8; i++)
            {
                float err = finetune - AimFinetune;
                if (Mathf.Abs(err) <= 0.03f) break;
                float halfBase = ConeHeightPx * Mathf.Tan(HalfConeDeg * Mathf.Deg2Rad);
                float maxX = halfBase * (1f - endL.y / ConeHeightPx);
                endL = new Vector2(endL.x + err * maxX * 1.1f, endL.y);
                DragTo(ConeLocalToScreen(endL.x, endL.y));
                yield return null;
            }
            Note($"{tag}_DEFLECT", "deflection after closed-loop correction (target vs achieved)",
                 $"{F(finetune)} vs {F(AimFinetune)}");

            // NO hold. Two attempts at one (0.9 s, then 0.35 s with an early-out) both ran past
            // the clean-pass window: TickArrow adds degradYaw the moment the pass count crosses
            // it, CommitFlick applies it, the targeting line does not carry it, and parity then
            // breaks by design rather than by defect. The player's aiming window really is that
            // short at this character's ClubControl — that is the game's timing pressure, not a
            // bug — so the stroke settles for two frames and flicks, and every assertion below
            // still demands IsDegrading == false so a degraded shot can never be compared
            // against an undegraded line.
            Vector2 holdScreen = ConeLocalToScreen(endL.x, endL.y);
            DragTo(holdScreen);
            yield return null;
            DragTo(holdScreen);
            yield return null;

            // ── which way does the DRAWN line actually lean, and which way is the handle? ──
            // Reproduces ShotConeView.UpdateTargetingLine's own projection (aimDir → world →
            // screen) so this is the line the player sees, not a re-derivation of the formula.
            // Never eyeballed off a frame: the answer is two screen-x numbers.
            Transform ballT = Ball;
            if (_worldCam != null && ballT != null)
            {
                Vector3 aimDir = new Vector3(Mathf.Cos(_lineYaw), 0f, Mathf.Sin(_lineYaw));
                Vector3 bS = _worldCam.WorldToScreenPoint(ballT.position);
                Vector3 tS = _worldCam.WorldToScreenPoint(ballT.position + aimDir * 30f);
                result.lineScreenDx = tS.x - bS.x;
            }
            Vector2 centreScreen = ConeLocalToScreen(0f, endL.y);
            result.handleScreenDx = ConeLocalToScreen(endL.x, endL.y).x - centreScreen.x;

            result.heading     = Heading;
            result.halfConeDeg = HalfConeDeg;
            result.aimFinetune = AimFinetune;
            result.lineYaw     = _lineYaw;
            result.degrading   = _publishedDegrading;
            result.passIndex   = _publishedPass;

            yield return FlickUp(endL);
            PointerUp();

            // ── did it fire? ──────────────────────────────────────────────────
            float w = 0f;
            while (!ShotInProgressUiGate.ShotInProgress && w < 2.5f)
            { yield return null; w += Time.unscaledDeltaTime; }
            result.fired = ShotInProgressUiGate.ShotInProgress;

            // A flick that misses the windowed speed gate resets the swing rather than firing —
            // exactly what happens to a real thumb on a stuttery frame, and what a real player
            // answers by flicking again. Retry once so a dropped frame does not read as a defect.
            if (!result.fired && StateName == "Idle")
            {
                Note($"{tag}_RETRY", "first flick missed the speed gate; re-pulling and flicking again",
                     $"lastFlickSpeed sampled by the gate, state={StateName}");
                PointerDownAt(ConeLocalToScreen(startL.x, startL.y));
                yield return null;
                yield return DragToLocal(startL, endL, 6);
                DragTo(ConeLocalToScreen(endL.x, endL.y));
                yield return null;
                result.heading     = Heading;
                result.halfConeDeg = HalfConeDeg;
                result.aimFinetune = AimFinetune;
                result.lineYaw     = _lineYaw;
                result.degrading   = _publishedDegrading;
                result.passIndex   = _publishedPass;
                yield return FlickUp(endL);
                PointerUp();
                w = 0f;
                while (!ShotInProgressUiGate.ShotInProgress && w < 2.5f)
                { yield return null; w += Time.unscaledDeltaTime; }
                result.fired = ShotInProgressUiGate.ShotInProgress;
            }
            if (!result.fired) yield break;

            // The ball's TRUE launch point is where it sits on the first in-flight frame — the
            // pre-drag read can be stale if the lie was repositioned between strokes.
            if (Ball != null) { launch = Ball.position; result.launch = launch; }

            // ── measure the ball's OWN flight, then pick the bearing sample afterwards ─────
            // Run 1 hard-coded a 25 m trigger and returned NaN for any stroke shorter than that.
            // Sample the whole flight instead and choose the best point once it is over.
            var samples = new List<(float horiz, float bearing)>(1024);
            float flight = 0f;
            while (ShotInProgressUiGate.ShotInProgress && flight < 30f)
            {
                Transform b = Ball;
                if (b != null)
                {
                    Vector3 d = b.position - launch;
                    float horiz = new Vector2(d.x, d.z).magnitude;
                    if (horiz > 0.05f && samples.Count < 1024)
                        samples.Add((horiz, Bearing(launch, b.position)));
                }
                yield return null;
                flight += Time.unscaledDeltaTime;
            }

            if (samples.Count > 0)
            {
                result.maxHorizM = samples.Max(x => x.horiz);
                // Prefer 25 m out (far enough that sampling noise is negligible); on a shorter
                // stroke fall back to the furthest point, provided the ball moved at all.
                float target = Mathf.Min(25f, result.maxHorizM);
                if (result.maxHorizM >= 3f)
                {
                    var usable = samples.Where(x => x.horiz >= 3f).ToList();
                    var pick = usable.OrderBy(x => Mathf.Abs(x.horiz - target)).First();
                    result.ballYaw = pick.bearing;
                    result.flightM = pick.horiz;

                    // EARLY sample: ~8 % of the way out, so the launch heading is read before
                    // any Magnus curve has had room to act. Run 2 measured "bend" from the 25 m
                    // mark on a 46 m shot — 83 % of the flight was already inside the baseline,
                    // so a real curve would have been invisible.
                    float earlyTarget = Mathf.Max(3f, result.maxHorizM * 0.08f);
                    var early = usable.OrderBy(x => Mathf.Abs(x.horiz - earlyTarget)).First();
                    result.earlyYaw = early.bearing;
                    result.earlyM   = early.horiz;
                }
            }

            yield return new WaitForSecondsRealtime(1.2f);
            if (Ball != null)
            {
                result.rest    = Ball.position;
                result.restYaw = Bearing(launch, Ball.position);
                result.restM   = new Vector2(Ball.position.x - launch.x, Ball.position.z - launch.z).magnitude;
            }
        }

        /// <summary>True once the last stroke resolved and the controller is ready for the next.</summary>
        bool _sessionStalled;

        IEnumerator WaitForIdle(float timeout = 35f)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (!ShotInProgressUiGate.ShotInProgress && StateName == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.2f); t += 0.2f;
            }
            // A stroke that never leaves Resolving means the hole session is wedged (a ball that
            // never came to rest, a penalty flow nobody dismissed). Everything measured after that
            // is garbage — a stale PinWorld reads as an 800-yard pin — so stop rather than fill the
            // JSON with NaNs that look like product failures.
            _sessionStalled = true;
            Note("STALL", "hole session never returned to Idle — remaining strokes abandoned",
                 $"state={StateName} after {timeout:F0}s");
            Debug.LogWarning($"[AimParityDemo] STALLED waiting for Idle (state={StateName})");
        }

        void RecordShot(string tag, string label, ShotResult r, float expectedFinetune)
        {
            Note($"{tag}_I1", $"{label}: camera heading (rad)", F(r.heading));
            Note($"{tag}_I2", $"{label}: half-cone (deg)",      F(r.halfConeDeg));
            Note($"{tag}_I3", $"{label}: aim finetune at commit", F(r.aimFinetune));
            Note($"{tag}_I4", $"{label}: line yaw published to the HUD (rad)", F(r.lineYaw));
            Note($"{tag}_I5", $"{label}: ball's own launch bearing (rad)", F(r.ballYaw));
            Note($"{tag}_I6", $"{label}: line−heading (deg)",
                 F(WrapPi(r.lineYaw - r.heading) * Mathf.Rad2Deg));
            Note($"{tag}_I7", $"{label}: ball−heading (deg)",
                 F(WrapPi(r.ballYaw - r.heading) * Mathf.Rad2Deg));
            Note($"{tag}_I8", $"{label}: horizontal distance at the launch-bearing sample (m)",
                 F(r.flightM));
            Note($"{tag}_I8b", $"{label}: furthest the ball got from the launch point (m)", F(r.maxHorizM));
            Note($"{tag}_I8c", $"{label}: launch → rest distance (m)", F(r.restM));
            Note($"{tag}_I8d", $"{label}: early bearing sample taken at (m)", F(r.earlyM));
            Note($"{tag}_I8e", $"{label}: total flight curve, early→rest (deg)", F(r.BendDeg));
            Note($"{tag}_I9", $"{label}: final rest bearing − heading (deg)",
                 F(WrapPi(r.restYaw - r.heading) * Mathf.Rad2Deg));

            if (_sessionStalled && !r.fired)
            {
                Note($"{tag}_SKIPPED", $"{label}: not attempted — the session had already stalled", "n/a");
                return;
            }
            AssertTrue($"{tag}_A0", $"{label}: the flick fired through the real handle",
                       r.fired, r.fired.ToString());
            if (!r.fired) return;

            // The parity claim, measured from opposite ends of the system.
            AssertNearAngle($"{tag}_A1",
                       $"{label}: the ball flies where the LINE pointed (|lineYaw − ballYaw| rad)",
                       r.lineYaw, r.ballYaw, 0.05f);

            // The cone claim: the handle buys the full half-cone, not a 3° nudge.
            AssertNearAngle($"{tag}_A2",
                       $"{label}: line−heading == finetune × halfCone (the cone is honoured)",
                       r.aimFinetune * r.halfConeDeg * Mathf.Deg2Rad,
                       WrapPi(r.lineYaw - r.heading), 0.01f);

            AssertNear($"{tag}_A3", $"{label}: the handle really reached the cone edge",
                       expectedFinetune, r.aimFinetune, 0.08f);

            Note($"{tag}_I10", $"{label}: handle offset on screen (px, + = right of cone centre)",
                 F(r.handleScreenDx));
            Note($"{tag}_I11", $"{label}: drawn line lean on screen (px, + = right of the ball)",
                 F(r.lineScreenDx));
            // INTENDED, and asserted so nobody "fixes" it later (Cesar, 2026-08-29): the handle is
            // the CLUB's position relative to the ball, not a pointer. You place the club LEFT of
            // the ball to send the ball RIGHT — real golf controls. So the drawn line must lean
            // OPPOSITE the handle, and the ball follows the line. Centre stays dead ahead.
            if (Mathf.Abs(expectedFinetune) > 0.5f)
                AssertTrue($"{tag}_A5",
                           $"{label}: the drawn line leans OPPOSITE the handle — club left ⇒ ball " +
                           "right, the intended control scheme",
                           !float.IsNaN(r.lineScreenDx) &&
                           Mathf.Sign(r.lineScreenDx) == -Mathf.Sign(r.handleScreenDx),
                           $"handle={F(r.handleScreenDx)}px line={F(r.lineScreenDx)}px");

            // Parity is only exact inside the clean-pass window; prove we were in it.
            Assert($"{tag}_A4", $"{label}: committed inside the clean-pass window (no degradation)",
                   false, r.degrading);
        }

        IEnumerator Sequence()
        {
            // ── boot through the REAL entry path ──────────────────────────────
            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton", 15f);
            yield return new WaitForSecondsRealtime(2.5f);
            Debug.Log("[AimParityDemo] stage: past Splash → clicking PlayButton");
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);
            Debug.Log("[AimParityDemo] stage: choosing a hole. " + VisibleButtons());
            bool picked = false;
            foreach (int h in HolePreference)
            {
                if (!HoleCardExists(h)) { Debug.Log($"[AimParityDemo] hole {h} card not available"); continue; }
                _holeNumber = h;
                Debug.Log($"[AimParityDemo] stage: clicking Hole {h} card");
                yield return ClickHoleCard(h, 15f);
                picked = true;
                break;
            }
            if (!picked)
            {
                Debug.LogError("[AimParityDemo] no hole card from the preference list was available — aborting.");
                WriteInvariants();
                EditorApplication.ExitPlaymode();
                yield break;
            }

            float t = 0f;
            while (FindButton("HoleMap") == null && t < 120f)
            { yield return new WaitForSecondsRealtime(0.5f); t += 0.5f; }
            Debug.Log($"[AimParityDemo] stage: hole-load wait ended after {t:F1}s. " + VisibleButtons());
            yield return new WaitForSecondsRealtime(4f);

            _dragger = UnityEngine.Object.FindObjectsByType<ClubHandleDragger>(
                           FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (_dragger == null || !BindShotController())
            {
                Debug.LogError("[AimParityDemo] Could not bind ClubHandleDragger / ShotController — aborting.");
                WriteInvariants();
                EditorApplication.ExitPlaymode();
                yield break;
            }

            _coneView    = UnityEngine.Object.FindObjectsByType<ShotConeView>(
                               FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            // Camera.main is NOT the gameplay camera in this project; take the very camera
            // ShotConeView itself projects the targeting line with.
            _worldCam    = _coneView != null
                         ? typeof(ShotConeView).GetField("_worldCamera", NP)?.GetValue(_coneView) as Camera
                         : null;
            _coneRect    = typeof(ClubHandleDragger).GetField("_coneRect", NP)?.GetValue(_dragger) as RectTransform;
            _coneGraphic = typeof(ClubHandleDragger).GetField("_coneGraphic", NP)?.GetValue(_dragger) as ConeMeshGraphic;
            var canvas   = _dragger.GetComponentInParent<Canvas>();
            _raycaster   = canvas != null ? canvas.rootCanvas.GetComponent<GraphicRaycaster>() : null;
            _uiCam       = _raycaster != null ? _raycaster.eventCamera
                         : (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                            ? canvas.worldCamera : null);
            if (_coneRect == null)
            {
                Debug.LogError("[AimParityDemo] ClubHandleDragger._coneRect not readable — aborting.");
                WriteInvariants();
                EditorApplication.ExitPlaymode();
                yield break;
            }

            Note("I0", "screen resolution", $"{Screen.width}x{Screen.height}");
            Note("I0b", "hole played (chosen for open ground, not a tree-lined chute)", _holeNumber);
            Note("I1", "cone height (px)", F(ConeHeightPx));
            Note("I2", "half-cone at this club (deg)", F(HalfConeDeg));
            Note("I3", "drag driver", "real ClubHandleDragger IPointerDown/IDrag/IPointerUp events");

            ShotAimParityDemoRecorder.StartRecorder();
            yield return new WaitForSecondsRealtime(0.5f);

            Assert("A0", "Straight mode at the start (not Fade/Draw)",
                   "Straight", ShotModeContext.Mode.ToString());

            // Stroke order: the FADE goes first, off the tee, at full power. A Magnus curve needs
            // flight time — the physics says a driver at 1.00 power bends 53 yd, at 0.52 only
            // 7.8 yd — and the previous cut fired the fade at 0.52 from mid-fairway, so it came
            // out looking dead straight (Cesar). The tee is the only place on the hole with room
            // for a full-power drive, so that is where the fade belongs.
            const float AdvanceFraction = 0.40f;
            const float TeeShotPower    = 0.95f;

            // ── stroke 1: Fade/Draw armed via the REAL button, full-power tee shot ────
            var fdBtn = UnityEngine.Object
                .FindObjectsByType<FadeDrawButtonWidget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault();
            if (fdBtn != null)
            {
                var btn = fdBtn.GetComponent<Button>();
                if (btn != null) ClickReal(btn);
                yield return new WaitForSecondsRealtime(0.8f);
            }
            Assert("A7", "the real Fade/Draw button armed Fade/Draw mode",
                   "FadeDraw", ShotModeContext.Mode.ToString());

            var fade = new ShotResult();
            float lockedAim = float.NaN;
            if (ShotModeContext.Mode == ShotMode.FadeDraw)
            {
                lockedAim = (float)_pLockedAim.GetValue(_sc);
                Note("A8_I", "locked aim captured at arming (rad)", F(lockedAim));
                Note("S1_PLAN", "preplanned stroke",
                     $"full-power tee shot, power {F(TeeShotPower)} — the physics puts this at " +
                     "~305 yd of carry with ~48 yd of curve");

                ShotAimParityDemoRecorder.Step("Fade/Draw armed — full power\\noff the tee");
                yield return WaitForIdle();
                yield return FireShot("S1", +1f, withWobble: false, fade, power: TeeShotPower);

                Note("S1_I1", "Fade/Draw: line yaw published to the HUD (rad)", F(fade.lineYaw));
                Note("S1_I2", "Fade/Draw: ball launch bearing (rad)",           F(fade.ballYaw));
                Note("S1_I3", "Fade/Draw: handle finetune at commit",           F(fade.aimFinetune));
                Note("S1_I4", "Fade/Draw: total flight curve, early→rest (deg) = the bend",
                     F(fade.BendDeg));
                Note("S1_I5", "Fade/Draw: furthest from launch (m) / rest distance (m)",
                     $"{F(fade.maxHorizM)} / {F(fade.restM)}");

                AssertTrue("S1_A0", "Fade/Draw: the flick fired", fade.fired, fade.fired.ToString());
                if (fade.fired)
                {
                    AssertNearAngle("S1_A1",
                               "Fade/Draw: the line root does NOT rotate with the handle — it sits " +
                               "on the aim locked at arming (D4)",
                               lockedAim, fade.lineYaw, 0.01f);
                    AssertNearAngle("S1_A2",
                               "Fade/Draw: the ball still LAUNCHES along that same locked line",
                               fade.lineYaw, fade.ballYaw, 0.06f);
                    AssertTrue("S1_A3",
                               "Fade/Draw: the handle was fully deflected, so the curve had input",
                               Mathf.Abs(fade.aimFinetune) > 0.85f, F(fade.aimFinetune));
                    // 3°, and no ratio against a "straight control" any more: the control now flies
                    // a completely different distance, so comparing their drifts measured nothing.
                    // The physics reference for this stroke is ~9.5° of total launch→rest angle at
                    // full power, so a bend under 3° means the curve really is not happening.
                    AssertTrue("S1_A4",
                               "Fade/Draw: the ball's path BENDS — early→rest curve exceeds 3°, " +
                               "against a physics reference of ~9.5° at this power",
                               Mathf.Abs(fade.BendDeg) > 3f,
                               $"{F(fade.BendDeg)}deg over {F(fade.maxHorizM)}m");
                }
                ShotAimParityDemoRecorder.Step("Launched on the locked line,\\nthen bent away");
                yield return new WaitForSecondsRealtime(2.0f);

                yield return WaitForIdle();
                if (fdBtn != null)
                {
                    var btn2 = fdBtn.GetComponent<Button>();
                    if (btn2 != null) ClickReal(btn2);
                    yield return new WaitForSecondsRealtime(0.8f);
                }
                Assert("A9", "tapping the same button again returns to Straight",
                       "Straight", ShotModeContext.Mode.ToString());
            }

            // ── stroke 2: club RIGHT of the ball ─────────────────────────────
            ShotAimParityDemoRecorder.Step("Club placed RIGHT of the ball\\n— so the ball goes LEFT");
            yield return WaitForIdle();
            var right = new ShotResult();
            yield return FireShot("S2", +1f, withWobble: true, right,
                                  power: PlanPower("S2", AdvanceFraction, out _));
            RecordShot("S2", "club RIGHT", right, +1f);
            yield return new WaitForSecondsRealtime(1.5f);

            // ── stroke 3: club LEFT of the ball ──────────────────────────────
            ShotAimParityDemoRecorder.Step("Club placed LEFT of the ball\\n— so the ball goes RIGHT");
            yield return WaitForIdle();
            var left = new ShotResult();
            yield return FireShot("S3", -1f, withWobble: false, left,
                                  power: PlanPower("S3", AdvanceFraction, out _));
            RecordShot("S3", "club LEFT", left, -1f);
            yield return new WaitForSecondsRealtime(1.5f);

            // ── stroke 4: club CENTRED ───────────────────────────────────────
            ShotAimParityDemoRecorder.Step("Club CENTRED");
            yield return WaitForIdle();
            var centre = new ShotResult();
            yield return FireShot("S4", 0f, withWobble: false, centre,
                                  power: PlanPower("S4", AdvanceFraction, out _));
            RecordShot("S4", "CENTRE", centre, 0f);
            yield return new WaitForSecondsRealtime(1.5f);

            if (right.fired && centre.fired && left.fired)
            {
                float dR = WrapPi(right.ballYaw  - right.heading)  * Mathf.Rad2Deg;
                float dC = WrapPi(centre.ballYaw - centre.heading) * Mathf.Rad2Deg;
                float dL = WrapPi(left.ballYaw   - left.heading)   * Mathf.Rad2Deg;
                Note("A5_I", "ball−heading for club-RIGHT / CENTRE / club-LEFT (deg)",
                     $"{F(dR)} / {F(dC)} / {F(dL)}");
                AssertTrue("A5", "the three straight strokes separate in the right order",
                           dR > 3f && Mathf.Abs(dC) < 3f && dL < -3f,
                           $"R={F(dR)}deg C={F(dC)}deg L={F(dL)}deg");
                AssertTrue("A6",
                           "full deflection moves the ball FURTHER than the old 3° nudge could " +
                           "reach — the cone is really honoured now",
                           dR > 3.2f && dL < -3.2f, $"R={F(dR)}deg L={F(dL)}deg");
            }

            yield return new WaitForSecondsRealtime(1.5f);
            WriteInvariants();

            if (_stateEvent != null && _stateDelegate != null && _sc != null)
                _stateEvent.RemoveEventHandler(_sc, _stateDelegate);

            EditorApplication.ExitPlaymode();
        }

        void WriteInvariants()
        {
            int fails = _asserts.Count(a => a.verdict == "FAIL");
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"task\": \"shot_aim_parity\",");
            sb.AppendLine("  \"subject\": \"the committed shot uses the same aim yaw the targeting line is drawn from\",");
            sb.AppendLine("  \"method\": \"lineYaw = published ShotInputState.AimYawRadians (the field ShotConeView draws from); ballYaw = bearing of the ball's own world motion over its first 25 m. Two independent ends of the system, never the same formula.\",");
            sb.AppendLine($"  \"screen\": \"{Screen.width}x{Screen.height}\",");
            sb.AppendLine($"  \"fail\": {fails},");
            sb.AppendLine("  \"assertions\": [");
            for (int i = 0; i < _asserts.Count; i++)
            {
                var a = _asserts[i];
                sb.Append("    { ");
                sb.Append($"\"id\": \"{Esc(a.id)}\", ");
                sb.Append($"\"description\": \"{Esc(a.description)}\", ");
                sb.Append($"\"expected\": \"{Esc(a.expected)}\", ");
                sb.Append($"\"actual\": \"{Esc(a.actual)}\", ");
                sb.Append($"\"verdict\": \"{Esc(a.verdict)}\"");
                sb.AppendLine(i == _asserts.Count - 1 ? " }" : " },");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            string path = ShotAimParityDemoRecorder.InvariantsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[AimParityDemo] Invariants written → {path} (fail={fails})");
        }

        static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
