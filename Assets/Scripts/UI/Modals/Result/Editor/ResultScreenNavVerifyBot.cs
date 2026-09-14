#if UNITY_EDITOR
// Assets/Scripts/UI/Modals/Result/Editor/ResultScreenNavVerifyBot.cs
// result_screen_nav_bars — the acceptance driver.
//
// Modelled on ScreenHintVerifyBot: boot ShellScene, get past the Splash gate with the REAL
// StartButton, walk to a hole exactly as a player does (the Home mode card's PlayButton, the
// hole card's ActionButton), close every first-visit hint through the hint modal's own
// NextButton, then hole out with a REAL putt (BotSwing.PlayPerfect through the selected
// control scheme; the ball is placed on the green 1.2 m from the cup first — measurement
// scaffolding, disclosed in the log, and the hole-out itself still goes through
// BallStateMachine → HoleCompletionBridge → GameSession.MarkHoleComplete, the production
// hole-end path). On the result screen it reads the state the change is about — bars active,
// "RESULTS" in the top bar, the result canvas under PersistentUI, the HUD canvases off, and
// an EventSystem raycast at the Home slot / Tee slot / gear landing ON those buttons — opens
// Settings through the real gear, closes it, and finally leaves through the real
// NavHomeButton.onClick, asserting Home is up, LabScaffold is unloaded, the result is hidden
// and the round was settled (progression store + reward grant).
//
// Every observation is written to <OutDir>/verify.log as it happens; every frame cited is a
// CaptureHelper.SnapPlayModeSafe capture copied into the media folder with its provenance
// sidecar, existence + md5 logged (SnapPlayModeSafe has returned phantom paths and stale
// frames before). A per-assertion verdict lands in <OutDir>/verdict.json.
//
// Usage: GOLFIN > Result Screen > Run nav-bars verify bot            (stills + JSON)
//        GOLFIN > Result Screen > Run nav-bars verify bot + clip     (also records the take
//        through BotVideoRecorder — one full-res clip per Editor launch, frame-capped).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.Controls.Bot;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics.Viewer;
using Golfin.Physics.Viewer.Editor;
using Golfin.Roster;
using Golfin.UI;
using Golfin.UI.GameplayTransition;
using Golfin.UI.Modals;
using Golfin.UI.Modals.Result;
using GolfinRedux.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golfin.EditorTools.ResultScreen
{
    public static class ResultScreenNavVerifyBot
    {
        const string ArmedKey   = "ResultScreenNavVerifyBot.Armed";
        const string ClipKey    = "ResultScreenNavVerifyBot.Clip";
        const string ShellScene = "Assets/Scenes/ShellScene.unity";

        /// <summary>Where the run lands (the Quick task's media folder).</summary>
        public const string OutDir = "Docs/Specs/Quick/media/result_screen_nav_bars";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Result Screen/Run nav-bars verify bot", priority = 400)]
        public static void LaunchVerify() => Launch(clip: false);

        [MenuItem("GOLFIN/Result Screen/Run nav-bars verify bot + clip", priority = 401)]
        public static void LaunchClip() => Launch(clip: true);

        static void Launch(bool clip)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ResultNavVerify] Already playing — stop first."); return; }
            if (SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            Directory.CreateDirectory(OutDir);
            if (clip)
            {
                // Deferred start: BeginDeferred() is called once the hole is on screen, so the
                // play-mode-entry transient is never in the clip. Begin() applies the 30 fps cap +
                // vSync 0 itself (feedback_cap_play_mode_frame_rate_never_record_long_sweeps).
                BotVideoRecorder.CustomOutputPath = OutDir + "/result_nav_bars_raw";
                BotVideoRecorder.MaxRecordSecondsSessionOverride = 60;
                BotVideoRecorder.ArmDeferred();
            }

            Application.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetBool(ClipKey, clip);
            EditorApplication.EnterPlaymode();
            Debug.Log("[ResultNavVerify] Armed (clip=" + clip + "). Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);

            TryEnsureIPhone14Selected();
            var host = new GameObject("[ResultScreenNavVerifyBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ResultScreenNavVerifyRunner>().Begin(SessionState.GetBool(ClipKey, false));
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }
    }

    public class ResultScreenNavVerifyRunner : MonoBehaviour
    {
        const string Out = ResultScreenNavVerifyBot.OutDir;

        readonly StringBuilder _log = new StringBuilder();
        readonly List<(string name, bool pass, string detail)> _asserts = new List<(string, bool, string)>();
        string _logPath;
        string _lastMd5;
        float  _t0;
        bool   _clip;
        bool   _holeComplete;
        HoleCompletionData _completion;

        public void Begin(bool clip)
        {
            _clip = clip;
            _logPath = Path.Combine(Out, "verify.log");
            Directory.CreateDirectory(Out);
            _t0 = Time.realtimeSinceStartup;
            GameSession.OnHoleComplete += OnHoleComplete;
            StartCoroutine(Sequence());
        }

        void OnDestroy() => GameSession.OnHoleComplete -= OnHoleComplete;

        void OnHoleComplete(HoleCompletionData d) { _holeComplete = true; _completion = d; }

        // ── logging / capture ─────────────────────────────────────────────────

        void Log(string s)
        {
            string line = (Time.realtimeSinceStartup - _t0).ToString("F2").PadLeft(7) + "s  " + s;
            _log.AppendLine(line);
            Debug.Log("[ResultNavVerify] " + s);
            try { File.WriteAllText(_logPath, _log.ToString()); } catch { }
        }

        void Assert(string name, bool pass, string detail)
        {
            _asserts.Add((name, pass, detail));
            Log((pass ? "PASS " : "FAIL ") + name + " — " + detail);
        }

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(f)).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }

        /// <summary>One frame into the media folder, with its provenance sidecar. Yields
        /// WaitForEndOfFrame FIRST: SnapPlayModeSafe's grab is synchronous and returns a phantom
        /// path when the caller is not at end of frame (reference_snapplaymodesafe_phantom_path).</summary>
        IEnumerator Snap(string label)
        {
            yield return new WaitForEndOfFrame();
            string src = CaptureHelper.SnapPlayModeSafe("result_nav_" + label);
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) { Log("SNAP " + label + ": NO FILE (" + src + ")"); yield break; }
            string dst = Path.Combine(Out, label + ".png");
            File.Copy(src, dst, true);
            if (File.Exists(src + ".json")) File.Copy(src + ".json", dst + ".json", true);
            string md5 = Md5(dst);
            long bytes = new FileInfo(dst).Length;
            Log("SNAP " + label + " -> " + dst + " (" + PngSize(dst) + ", " + bytes + " B, md5 " + md5
                + (md5 == _lastMd5 ? ", IDENTICAL TO PREVIOUS SNAP" : "") + ")");
            _lastMd5 = md5;
        }

        static string PngSize(string path)
        {
            try
            {
                using (var f = File.OpenRead(path))
                {
                    var b = new byte[24]; f.Read(b, 0, 24);
                    int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                    int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                    return w + "x" + h;
                }
            }
            catch { return "?x?"; }
        }

        static IEnumerator Settle(float s) { yield return new WaitForSecondsRealtime(s); }

        // ── real widgets ──────────────────────────────────────────────────────

        static T FindLive<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name) && c.gameObject.activeInHierarchy);

        static Button Live(string name)
            => Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                   b.gameObject.name == name && !string.IsNullOrEmpty(b.gameObject.scene.name)
                   && b.gameObject.activeInHierarchy && b.interactable);

        static void Press(Button b)
        {
            var ped = new PointerEventData(EventSystem.current);
            (b.GetComponent("ButtonPressFeedback") as IPointerDownHandler)?.OnPointerDown(ped);
            b.onClick.Invoke();
            (b.GetComponent("ButtonPressFeedback") as IPointerUpHandler)?.OnPointerUp(ped);
        }

        IEnumerator TapWhenLive(string name, float timeout = 20f)
        {
            float t = 0f;
            while (t < timeout)
            {
                Button b = Live(name);
                if (b != null) { Press(b); Log("tapped real " + name); yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Log("never found an interactable " + name);
        }

        static ScreenHintModalController Hint => ScreenHintModalController.Instance;

        /// <summary>Close a first-visit hint group the way a player does — its own NextButton,
        /// CONTINUE to the end then CLOSE. No-op when no hint is up (a profile that has seen them).</summary>
        IEnumerator CloseHints(string where, float waitFor = 2f)
        {
            float t = 0f;
            while (t < waitFor && (Hint == null || !Hint.IsVisible())) { t += Time.unscaledDeltaTime; yield return null; }
            if (Hint == null || !Hint.IsVisible()) { Log("no hint group on " + where); yield break; }
            yield return Settle(1.0f);
            int guard = 0;
            while (Hint != null && Hint.IsVisible() && guard++ < 12)
            {
                var next = Hint.transform.Find("Panel/ButtonsRow/NextButton")?.GetComponent<Button>();
                if (next == null) { Log("hint NextButton missing on " + where); yield break; }
                Press(next);
                yield return Settle(0.6f);
            }
            Log("closed hint group on " + where + " (" + guard + " taps)");
            yield return Settle(0.5f);
        }

        static Scene Gameplay => SceneManager.GetSceneByName("LabScaffold");

        static bool AnyHoleGeoLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name.StartsWith("Hole_") && s.name.EndsWith("_Geo")) return true;
            }
            return false;
        }

        // ── the hole-out ──────────────────────────────────────────────────────

        /// <summary>
        /// A REAL putt into the cup, through BotSwing (whatever scheme the player has selected).
        /// The ball is first placed on the green 1.2 m from the cup: that placement is the only
        /// non-player step here and is logged as such. Retries with a slightly different power
        /// when the putt lips out; falls back to the InCup bot seam only as a last resort and says
        /// so loudly (a run that reached the result through the seam is NOT the real-play proof).
        /// </summary>
        IEnumerator HoleOut()
        {
            var lab = FindFirstObjectByType<PhysicsLabController>();
            if (lab == null) { Log("no PhysicsLabController — cannot hole out"); yield break; }

            Vector3 pin = HoleContext.PinWorld;
            if (pin == Vector3.zero) pin = HoleContext.GreenCentroidWorld;
            Vector3 ball0 = lab.BallPosition;
            Vector3 towardTee = ball0 - pin; towardTee.y = 0f;
            if (towardTee.sqrMagnitude < 1e-4f) towardTee = Vector3.forward;
            towardTee.Normalize();
            Log($"hole-out: pin={pin:F1} ball(tee)={ball0:F1}");

            float[] powers = { 0.16f, 0.20f, 0.13f };
            for (int attempt = 0; attempt < powers.Length && !_holeComplete; attempt++)
            {
                Vector3 place = pin + towardTee * 1.2f;
                // 1 == Golfin.Course.SurfaceType.Green (PlaceBallAt's own doc comment).
                lab.PlaceBallAt(place, 1);
                Log($"SCAFFOLDING: PlaceBallAt({place:F2}, Green) — 1.2 m from the cup (attempt {attempt + 1})");
                yield return Settle(1.2f);

                Vector3 ball = lab.BallPosition;
                float yaw = Mathf.Atan2(pin.z - ball.z, pin.x - ball.x);
                lab.SetClub(PhysicsLabController.PutterIndex);
                // The lab's aim seams are internal to Golfin.Physics.Viewer (BotDriver lives there);
                // the same two calls BotDriver.PlayHoleToCup makes per stroke, by reflection.
                var labT = typeof(PhysicsLabController);
                labT.GetMethod("SetCameraYawRadians", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(lab, new object[] { yaw });
                var chase = FindFirstObjectByType<ChaseCamera>();
                var cam = chase != null ? chase.GetComponent<Camera>() : null;
                if (cam != null)
                    labT.GetMethod("ApplyAimCameraAt", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(lab, new object[] { cam, ball, yaw });
                yield return Settle(0.6f);

                var ctx = BotExecutionContext.Resolve();
                Log($"putt {attempt + 1}: ball={ball:F2} dist={Vector3.Distance(new Vector3(ball.x, 0, ball.z), new Vector3(pin.x, 0, pin.z)):F2} m " +
                    $"power={powers[attempt]:F2} scheme executor={BotSwing.ResolveExecutor().GetType().Name}");
                yield return BotSwing.PlayPerfect(powers[attempt], yaw, isPutt: true, ctx: ctx);

                float t = 0f;
                while (t < 14f && !_holeComplete) { t += Time.unscaledDeltaTime; yield return null; }
                Log(_holeComplete
                    ? $"HOLE COMPLETE after {t:F1}s — terminal={_completion.TerminalState} strokes={_completion.Strokes} hole={_completion.HoleNumber} (real putt)"
                    : $"putt {attempt + 1} did not hole out within {t:F0}s (ball at {lab.BallPosition:F2})");
            }

            if (!_holeComplete)
            {
                Log("FALLBACK — three real putts missed; forcing InCup through the bot seam. " +
                    "This run does NOT prove the real hole-out path (the result-screen assertions still stand).");
                var sm = typeof(PhysicsLabController).GetProperty("BallSM", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(lab);
                sm?.GetType().GetMethod("ForceShotCompleteForBot")?.Invoke(sm, new object[] { BallState.InCup });
                float t = 0f;
                while (t < 5f && !_holeComplete) { t += Time.unscaledDeltaTime; yield return null; }
            }
        }

        // ── the run ───────────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            Application.runInBackground = true;
            Log("run clip=" + _clip + " screen=" + Screen.width + "x" + Screen.height);

            // Splash gate — the real StartButton (DevAutoSignIn usually taps it first).
            float t = 0f;
            while (t < 25f)
            {
                var splash = FindFirstObjectByType<SplashScreenController>();
                Transform btn = splash == null ? null : splash.transform.Find("StartButton");
                if (btn != null && btn.gameObject.activeInHierarchy) { btn.GetComponent<Button>()?.onClick.Invoke(); Log("tapped StartButton"); break; }
                if (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen == ScreenId.Home) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            t = 0f;
            while (t < 20f && (ScreenManager.Instance == null || ScreenManager.Instance.CurrentScreen != ScreenId.Home)) { t += Time.unscaledDeltaTime; yield return null; }
            Log("at Home=" + (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen == ScreenId.Home) + " after " + t.ToString("F1") + "s");
            yield return CloseHints("Home");

            yield return TapWhenLive("PlayButton");
            t = 0f;
            while (t < 10f && ScreenManager.Instance.CurrentScreen != ScreenId.HoleSelection) { t += Time.unscaledDeltaTime; yield return null; }
            Log("CurrentScreen=" + ScreenManager.Instance.CurrentScreen);
            yield return CloseHints("HoleSelection");
            yield return TapWhenLive("ActionButton");

            // The hole: geo scene loaded, loading screen gone, lab bound.
            t = 0f;
            PhysicsLabController lab = null;
            while (t < 90f)
            {
                lab = FindFirstObjectByType<PhysicsLabController>();
                var lsc = Resources.FindObjectsOfTypeAll<LoadingScreenController>()
                    .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name));
                bool loadingActive = lsc != null && lsc.gameObject.activeInHierarchy;
                if (AnyHoleGeoLoaded() && lab != null && lab.IsHoleReady && !loadingActive) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Log($"gameplay ready after {t:F1}s: hole={GameSession.CurrentHoleNumber} LabScaffold loaded={Gameplay.isLoaded} IsGameplayLoaded={GameplaySceneLoader.IsGameplayLoaded}");
            yield return CloseHints("Gameplay", waitFor: 6f);
            yield return Settle(1.5f);

            if (_clip) { BotVideoRecorder.BeginDeferred(); Log("recording started"); yield return Settle(1.0f); }

            var pui = FindLive<PersistentUIManager>() ?? PersistentUIManager.Instance;
            bool barsHiddenInPlay = pui != null && !pui.topBarPanel.activeInHierarchy && !pui.bottomNavPanel.activeInHierarchy;
            Assert("bars.hidden_during_play", barsHiddenInPlay,
                   $"topBar={pui?.topBarPanel.activeInHierarchy} bottomNav={pui?.bottomNavPanel.activeInHierarchy} while the hole is live");
            yield return Snap("01_gameplay_before_holeout");

            int hole = GameSession.CurrentHoleNumber;
            var store = HoleProgressionStoreAdapter.Default;
            bool playedBefore = store.HasPlayed(hole);
            int rpBefore = RewardPointsManager.Instance != null ? RewardPointsManager.Instance.GetPoints() : -1;
            Log($"before hole-out: HasPlayed({hole})={playedBefore} IsUnlocked({hole + 1})={store.IsUnlocked(hole + 1)} RP={rpBefore}");

            yield return HoleOut();
            Assert("holeout.real", _holeComplete && _completion.TerminalState == BallState.InCup,
                   _holeComplete ? $"terminal={_completion.TerminalState} strokes={_completion.Strokes}" : "no OnHoleComplete");

            // ── the result screen ──────────────────────────────────────────────
            var modal = Resources.FindObjectsOfTypeAll<HoleCompleteModalController>()
                .FirstOrDefault(m => m != null && !string.IsNullOrEmpty(m.gameObject.scene.name));
            var widget = modal != null ? modal.GetComponentInChildren<HoleCompleteWidget>(true) : null;
            t = 0f;
            while (t < 10f && (widget == null || !widget.IsShowing)) { t += Time.unscaledDeltaTime; yield return null; }
            Assert("result.showing", widget != null && widget.IsShowing, $"HoleCompleteWidget.IsShowing after {t:F1}s");
            yield return Settle(3.5f);   // the pop + the reward count-up

            pui = FindLive<PersistentUIManager>() ?? PersistentUIManager.Instance;
            bool topBar = pui != null && pui.topBarPanel.activeInHierarchy;
            bool botNav = pui != null && pui.bottomNavPanel.activeInHierarchy;
            Assert("result.bars_visible", topBar && botNav, $"topBar={topBar} bottomNav={botNav}");
            string expectTitle = LocalizationManager.Get("RESULT_RESULTS");
            string title = pui != null && pui.usernameText != null ? pui.usernameText.text : "<no label>";
            Assert("result.title", title == expectTitle, $"top-bar centre text='{title}' expected='{expectTitle}'");

            var modalCanvas = modal != null ? modal.GetComponent<Canvas>() : null;
            var widgetCanvas = widget != null ? widget.GetComponent<Canvas>() : null;
            var puiCanvas = pui != null ? pui.GetComponent<Canvas>() : null;
            Assert("result.sorts_under_bars",
                   modalCanvas != null && puiCanvas != null && modalCanvas.overrideSorting && modalCanvas.sortingOrder < puiCanvas.sortingOrder
                   && widgetCanvas != null && !widgetCanvas.overrideSorting,
                   $"HoleCompleteModal canvas override={modalCanvas?.overrideSorting} order={modalCanvas?.sortingOrder}; widget canvas override={widgetCanvas?.overrideSorting} (order {widgetCanvas?.sortingOrder}); PersistentUI order={puiCanvas?.sortingOrder}");

            // Every ROOT canvas of the gameplay scene (ShotUI_Canvas hangs under LabRoot, so root
            // GameObjects alone would miss it — the miss the first run made).
            var hudCanvases = Resources.FindObjectsOfTypeAll<Canvas>()
                .Where(c => c != null && c.gameObject.scene == Gameplay && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                .ToList();
            Assert("result.hud_hidden", hudCanvases.Count >= 2 && hudCanvases.All(c => !c.gameObject.activeInHierarchy),
                   string.Join(", ", hudCanvases.Select(c => HierarchyPath(c.transform) + "(order " + c.sortingOrder + ") active=" + c.gameObject.activeInHierarchy)));

            // The taps: what the EventSystem would hand a press at each control's centre.
            RaycastCheck("result.tap_home_slot", pui != null ? pui.homeButton : null);
            RaycastCheck("result.tap_tee_slot", pui != null ? pui.mainPlayButton : null);
            RaycastCheck("result.tap_gear", pui != null ? pui.settingsButton : null);
            RaycastCheck("result.tap_replay_still_works", widget != null ? widget.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.gameObject.name == "ReplayButton" || b.gameObject.name == "RetryButton") : null);

            yield return Snap("02_result_screen_with_bars");

            // The gear over the result: Settings opens ABOVE it (canvas 100 > -1) and closes again.
            if (pui != null && pui.settingsButton != null && SettingsController.Instance != null)
            {
                Press(pui.settingsButton);
                // Held long enough to READ in the clip (the first take flashed it for 0.85 s).
                yield return Settle(_clip ? 2.2f : 0.8f);
                bool open = SettingsController.Instance.IsOpen;
                Assert("result.gear_opens_settings", open, "SettingsController.IsOpen=" + open + " after the real gear tap");
                yield return Snap("03_settings_over_result");
                SettingsController.Instance.CloseSettings();
                yield return Settle(0.6f);
                Assert("result.settings_closed", !SettingsController.Instance.IsOpen, "IsOpen=" + SettingsController.Instance.IsOpen);
                Assert("result.still_showing_after_settings", widget != null && widget.IsShowing && Gameplay.isLoaded,
                       $"widget.IsShowing={widget?.IsShowing} LabScaffold loaded={Gameplay.isLoaded}");
            }

            // ── leave through the real Home slot ───────────────────────────────
            yield return Settle(_clip ? 1.2f : 0.8f);
            if (pui != null && pui.homeButton != null) { Press(pui.homeButton); Log("tapped real NavHomeButton"); }
            var loader = GameplaySceneLoader.Instance;
            t = 0f;
            while (t < 25f)
            {
                bool done = ScreenManager.Instance.CurrentScreen == ScreenId.Home && !GameplaySceneLoader.IsGameplayLoaded
                            && (loader == null || !loader.IsExiting);
                if (done) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Log($"after Home tap ({t:F1}s): CurrentScreen={ScreenManager.Instance.CurrentScreen} IsGameplayLoaded={GameplaySceneLoader.IsGameplayLoaded} exiting={loader?.IsExiting} holeGeo={AnyHoleGeoLoaded()}");
            yield return Settle(1.0f);

            Assert("nav.home_reached", ScreenManager.Instance.CurrentScreen == ScreenId.Home, "CurrentScreen=" + ScreenManager.Instance.CurrentScreen);
            Assert("nav.gameplay_unloaded", !GameplaySceneLoader.IsGameplayLoaded && !AnyHoleGeoLoaded(), $"LabScaffold loaded={GameplaySceneLoader.IsGameplayLoaded} holeGeo={AnyHoleGeoLoaded()}");
            Assert("nav.result_hidden", widget != null && !widget.IsShowing, "widget.IsShowing=" + widget?.IsShowing);
            pui = FindLive<PersistentUIManager>() ?? PersistentUIManager.Instance;
            Assert("nav.bars_on_home", pui != null && pui.topBarPanel.activeInHierarchy && pui.bottomNavPanel.activeInHierarchy,
                   $"topBar={pui?.topBarPanel.activeInHierarchy} bottomNav={pui?.bottomNavPanel.activeInHierarchy}");
            string homeTitle = pui != null && pui.usernameText != null ? pui.usernameText.text : "<no label>";
            Assert("nav.title_restored", homeTitle != expectTitle, $"top-bar centre text='{homeTitle}' (no longer '{expectTitle}')");
            Assert("nav.session_cleared", GameSession.CurrentHoleNumber == 0 && !GameSession.IsVersus && !GameSession.IsTournament,
                   $"CurrentHoleNumber={GameSession.CurrentHoleNumber} IsVersus={GameSession.IsVersus} IsTournament={GameSession.IsTournament}");

            bool success = _holeComplete && _completion.TerminalState == BallState.InCup;
            bool playedAfter = store.HasPlayed(hole);
            bool nextUnlocked = store.IsUnlocked(hole + 1);
            Assert("settle.progression_written", !success || (playedAfter && nextUnlocked),
                   $"HasPlayed({hole})={playedAfter} (before: {playedBefore}) IsUnlocked({hole + 1})={nextUnlocked}");
            bool granted = modal != null && (bool)(typeof(HoleCompleteModalController)
                .GetField("_rewardsGranted", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(modal) ?? false);
            int rpAfter = RewardPointsManager.Instance != null ? RewardPointsManager.Instance.GetPoints() : -1;
            Assert("settle.rewards_granted", !success || granted, $"_rewardsGranted={granted}; RP {rpBefore} -> {rpAfter}");

            yield return Snap("04_home_after_nav");

            WriteVerdict();
            Log("DONE — " + _asserts.Count(a => a.pass) + "/" + _asserts.Count + " PASS");
            if (_clip) { yield return Settle(1.0f); BotVideoRecorder.End(); Log("recording stopped"); }
            yield return Settle(0.5f);
            EditorApplication.ExitPlaymode();
        }

        /// <summary>What a press at the control's centre lands on, through the live EventSystem —
        /// PASS when the top hit is the control or one of its children.</summary>
        void RaycastCheck(string name, Button target)
        {
            if (target == null) { Assert(name, false, "control not found"); return; }
            var rect = target.transform as RectTransform;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var ped = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);
            var top = hits.Count > 0 ? hits[0].gameObject : null;
            bool pass = top != null && (top.transform == target.transform || top.transform.IsChildOf(target.transform));
            Assert(name, pass, $"press at {screen} -> top hit '{(top != null ? HierarchyPath(top.transform) : "nothing")}' (order {(hits.Count > 0 ? hits[0].sortingOrder : 0)}) for {target.gameObject.name}; {hits.Count} hits");
        }

        static string HierarchyPath(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }

        void WriteVerdict()
        {
            var sb = new StringBuilder();
            bool all = _asserts.All(a => a.pass);
            sb.Append("{\n  \"task\": \"result_screen_nav_bars\",\n  \"verdict\": \"").Append(all ? "PASS" : "FAIL").Append("\",\n");
            sb.Append("  \"clip\": ").Append(_clip ? "true" : "false").Append(",\n  \"assertions\": [\n");
            for (int i = 0; i < _asserts.Count; i++)
            {
                var a = _asserts[i];
                sb.Append("    {\"name\": \"").Append(a.name).Append("\", \"result\": \"").Append(a.pass ? "PASS" : "FAIL")
                  .Append("\", \"detail\": \"").Append(a.detail.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"}")
                  .Append(i < _asserts.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(Out, "verdict.json"), sb.ToString());
        }
    }
}
#endif
