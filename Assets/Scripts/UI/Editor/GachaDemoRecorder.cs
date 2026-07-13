#if UNITY_EDITOR
// Assets/Scripts/UI/Editor/GachaDemoRecorder.cs
// gacha_screen Stage 3 — bot-video polish gate.
// Drives the REAL Gacha UI: GACHA tab → swipe banners → countdown ticking →
//   tap RulesButton (Application.OpenURL logged) → tap PULL x10 (Coming soon toast).
//
// Output (raw):   tasks/loop_v2_smoke_bot/gacha_demo/video/raw.mp4
// Sidecar:        tasks/loop_v2_smoke_bot/gacha_demo/video/record_info.json
// Step log:       tasks/loop_v2_smoke_bot/gacha_demo/screenshots/history.log
// Caption cmd:    python3 Docs/Scripts/build_bot_video.py --scenario gacha_demo --mode steps \
//                   --title "Gacha Screen — Stage 3" --suffix gacha_stage3 \
//                   --output-dir Docs/Specs/Active/gacha_screen/videos/ --keep-raw
//
// Usage:          GOLFIN > Gacha > Record Gacha Stage 3 Demo

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfinRedux.UI.Gacha;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    public static class GachaDemoRecorder
    {
        const string Scenario    = "gacha_demo";
        const string RawDir      = "tasks/loop_v2_smoke_bot/gacha_demo/video";
        const string LogDir      = "tasks/loop_v2_smoke_bot/gacha_demo/screenshots";
        const string ArmedKey    = "GachaDemoRecorder.Armed";
        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Gacha/Record Gacha Stage 3 Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GachaDemo] Already in play mode — stop first.");
                return;
            }
            Directory.CreateDirectory(RawDir);
            Directory.CreateDirectory(LogDir);
            // Clear step log from any prior run
            File.WriteAllText(Path.Combine(LogDir, "history.log"), "");
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GachaDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        static void StartRecorderAndBot()
        {
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = (int)cw; h = (int)ch;
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[GachaDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            // Delete stale raw MP4 so Recorder doesn't append "_001"
            var rawPath = Path.Combine(RawDir, "raw.mp4");
            if (File.Exists(rawPath)) File.Delete(rawPath);

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name                          = "GachaStage3Demo";
            movie.Enabled                       = true;
            movie.OutputFormat                  = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings            = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile                    = Path.Combine(RawDir, "raw");

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate             = 30;
            settings.FrameRatePlayback     = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[GachaDemo] Recording started → {rawPath} ({w}x{h} @ 30fps)");

            var host = new GameObject("[GachaDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GachaDemoRunner>().Init(RawDir, LogDir, w, h);
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[GachaDemo] Recorder stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[GachaDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    // ── Demo runner MonoBehaviour ──────────────────────────────────────────────

    public class GachaDemoRunner : MonoBehaviour
    {
        string _rawDir, _logDir;
        string _logPath, _infoPath, _rawMp4Path;
        int    _recW, _recH;
        float  _recordStart;

        public void Init(string rawDir, string logDir, int w, int h)
        {
            _rawDir     = rawDir;
            _logDir     = logDir;
            _logPath    = Path.Combine(logDir, "history.log");
            _infoPath   = Path.Combine(rawDir,  "record_info.json");
            _rawMp4Path = Path.Combine(rawDir,  "raw.mp4");
            _recW = w; _recH = h;
            StartCoroutine(Sequence());
        }

        // ── Logging helpers ────────────────────────────────────────────────────

        void LogStep(string text)
        {
            float t = Time.realtimeSinceStartup;
            File.AppendAllText(_logPath, $"[t={t:F3}] Step: '{text}'\n");
            Debug.Log($"[GachaDemo][{t:F2}s] Step: {text}");
        }

        void WriteRecordInfo(float start, float end)
        {
            string absMp4 = Path.GetFullPath(_rawMp4Path).Replace('\\', '/');
            string json = "{\n" +
                $"  \"record_start_realtime\": {start:F3},\n" +
                $"  \"record_end_realtime\": {end:F3},\n" +
                $"  \"mp4\": \"{absMp4}\",\n" +
                "  \"fps\": 30\n" +
                "}";
            File.WriteAllText(_infoPath, json);
            Debug.Log($"[GachaDemo] record_info.json written (start={start:F2}, end={end:F2})");
        }

        // ── Reflection: carousel field access ─────────────────────────────────

        static readonly Type CarouselType = typeof(GachaCarouselController);
        static readonly FieldInfo FldCurIndex  = CarouselType.GetField("_currentIndex",  BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FldTargetOff = CarouselType.GetField("_targetOffset",  BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo MthUpdateDots = CarouselType.GetMethod("UpdateDots",   BindingFlags.NonPublic | BindingFlags.Instance);

        static GachaCarouselController FindCarousel() =>
            Resources.FindObjectsOfTypeAll<GachaCarouselController>()
                .FirstOrDefault(c => c.isActiveAndEnabled && !string.IsNullOrEmpty(c.gameObject.scene.name));

        GachaBannerCard GetCenterCard(GachaCarouselController ctrl)
        {
            var idxF  = CarouselType.GetField("_currentIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            var lstF  = CarouselType.GetField("_cards",        BindingFlags.NonPublic | BindingFlags.Instance);
            if (idxF == null || lstF == null) return null;
            int idx   = (int)idxF.GetValue(ctrl);
            var cards = lstF.GetValue(ctrl) as System.Collections.Generic.List<GachaBannerCard>;
            return (cards != null && idx >= 0 && idx < cards.Count) ? cards[idx] : null;
        }

        static Button GetCardButton(GachaBannerCard card, string fieldName)
        {
            var f = typeof(GachaBannerCard).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(card) as Button;
        }

        // ── Swipe simulation (reflection, no EventSystem dependency) ──────────

        /// <summary>
        /// Animates a swipe: pushes _targetOffset in the swipe direction, then snaps index.
        /// GachaCarouselController.Update() lerps _currentOffset → _targetOffset each frame,
        /// producing the natural scale/alpha falloff during drag and a smooth snap at the end.
        /// </summary>
        IEnumerator AnimateSwipeToIndex(GachaCarouselController ctrl, int toIndex, float dragSecs)
        {
            if (FldCurIndex == null || FldTargetOff == null) yield break;

            int curIdx = (int)FldCurIndex.GetValue(ctrl);
            if (curIdx == toIndex) yield break;

            // Cards to the right of center have +x offsets; dragging left (-) advances index.
            float peakDrag = (toIndex > curIdx) ? -520f : 520f;

            // Ramp target offset over dragSecs so the carousel smoothly follows
            float elapsed = 0f;
            while (elapsed < dragSecs)
            {
                elapsed += Time.unscaledDeltaTime;
                float f = Mathf.Clamp01(elapsed / dragSecs);
                FldTargetOff.SetValue(ctrl, peakDrag * f);
                yield return null;
            }

            // Commit new index, snap target back to centre
            FldCurIndex.SetValue(ctrl, toIndex);
            FldTargetOff.SetValue(ctrl, 0f);
            MthUpdateDots?.Invoke(ctrl, null);

            // Wait for snap lerp to settle (snapSpeed=10 → ~1s to >99%)
            yield return new WaitForSecondsRealtime(1.1f);
        }

        // ── Main demo sequence ─────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            _recordStart = Time.realtimeSinceStartup;
            // Write a placeholder record_info.json immediately (start time fixed; end updated at close)
            WriteRecordInfo(_recordStart, _recordStart + 60f);

            // Boot wait — ShellScene initialises managers, nav bar appears
            yield return new WaitForSecondsRealtime(3.0f);

            // ── Beat 1: Open Rewards Center on GACHA tab ──────────────────────
            // Tap the real bottom-nav Gacha button (same pattern as GeneralShopDemoRecorder)
            var navGacha = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                b.gameObject.name == "NavGachaButton" &&
                !string.IsNullOrEmpty(b.gameObject.scene.name) &&
                b.GetComponentInParent<Canvas>() != null);

            if (navGacha != null)
            {
                Debug.Log("[GachaDemo] Tapping NavGachaButton → Rewards Center");
                navGacha.onClick.Invoke();
            }
            else
            {
                Debug.LogWarning("[GachaDemo] NavGachaButton not found — fallback ShowScreen");
                GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.GeneralShop, true);
            }

            yield return new WaitForSecondsRealtime(2.0f);  // fade + carousel spawn + first render

            LogStep("GACHA tab — 10 tickets");
            yield return new WaitForSecondsRealtime(1.5f);  // let viewer read the ticket counter

            // ── Beat 2: Swipe banners — snap + falloff + dots updating ─────────
            var ctrl = FindCarousel();
            if (ctrl != null)
            {
                int cardsCount = 0;
                var lstF = CarouselType.GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance);
                var cards = lstF?.GetValue(ctrl) as System.Collections.Generic.List<GachaBannerCard>;
                cardsCount = cards?.Count ?? 0;
                Debug.Log($"[GachaDemo] Carousel found — {cardsCount} cards");

                if (cardsCount >= 2)
                {
                    LogStep("Swipe + falloff");
                    yield return StartCoroutine(AnimateSwipeToIndex(ctrl, 1, 0.55f));

                    if (cardsCount >= 3)
                    {
                        LogStep("Swipe left again");
                        yield return StartCoroutine(AnimateSwipeToIndex(ctrl, 2, 0.55f));
                        yield return new WaitForSecondsRealtime(0.5f);
                        // Swipe back to show it's a true carousel
                        LogStep("Swipe right — back");
                        yield return StartCoroutine(AnimateSwipeToIndex(ctrl, 1, 0.55f));
                        yield return StartCoroutine(AnimateSwipeToIndex(ctrl, 0, 0.55f));
                    }
                    else
                    {
                        // Only 2 cards — swipe back
                        yield return StartCoroutine(AnimateSwipeToIndex(ctrl, 0, 0.55f));
                    }
                }
                else
                {
                    Debug.LogWarning("[GachaDemo] Fewer than 2 live banners — skipping swipe beat.");
                    yield return new WaitForSecondsRealtime(2.0f);
                }
            }
            else
            {
                Debug.LogWarning("[GachaDemo] GachaCarouselController not found — skipping swipe beat.");
                yield return new WaitForSecondsRealtime(4.0f);
            }

            // ── Beat 3: Countdown visibly ticking ─────────────────────────────
            LogStep("Countdown ticking");
            yield return new WaitForSecondsRealtime(2.0f);  // linger 2 seconds: seconds decrement on screen

            // ── Beat 4: Tap RULES & RATES ──────────────────────────────────────
            LogStep("Rules & Rates » OpenURL");
            ctrl = FindCarousel();
            var centerCard = ctrl != null ? GetCenterCard(ctrl) : null;
            var rulesBtn   = centerCard != null ? GetCardButton(centerCard, "_rulesButton") : null;

            if (rulesBtn != null)
            {
                Debug.Log($"[GachaDemo] Invoking _rulesButton.onClick on {centerCard.Entry?.BannerId}");
                rulesBtn.onClick.Invoke();
            }
            else
            {
                // Fallback: find by prefab name in scene
                rulesBtn = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                    b.gameObject.name == "RulesButton" &&
                    !string.IsNullOrEmpty(b.gameObject.scene.name) &&
                    b.isActiveAndEnabled);
                if (rulesBtn != null)
                {
                    Debug.Log("[GachaDemo] Fallback: invoking RulesButton by name");
                    rulesBtn.onClick.Invoke();
                }
                else
                    Debug.LogWarning("[GachaDemo] RulesButton not found — skipping beat 4.");
            }
            yield return new WaitForSecondsRealtime(1.2f);

            // ── Beat 5: Tap PULL x10 — "Coming soon" toast ────────────────────
            LogStep("PULL x10 » Coming soon");

            // Ensure ToastController is awake before the button tap.
            // Toast GO has m_IsActive:0 in the scene — GameObject.Find() cannot find inactive objects.
            // Resources.FindObjectsOfTypeAll<T>() finds ALL objects including inactive scene objects;
            // filter by scene.name to exclude prefabs sitting in memory.
            // Activate in play mode — Unity auto-reverts scene disk state on exit (no mutation saved).
            var toastCtrl = Resources.FindObjectsOfTypeAll<Golfin.UI.Toast.ToastController>()
                .FirstOrDefault(c => !string.IsNullOrEmpty(c.gameObject.scene.name));
            if (toastCtrl != null && !toastCtrl.gameObject.activeInHierarchy)
            {
                Debug.Log("[GachaDemo] Activating Toast GO in play mode (reverts on exit).");
                toastCtrl.gameObject.SetActive(true);
                yield return null;  // Awake() is synchronous but yield one frame so Instance is set
                yield return null;
            }
            else if (toastCtrl == null)
                Debug.LogWarning("[GachaDemo] ToastController not found in scene — toast may not appear.");
            else
                Debug.Log("[GachaDemo] Toast GO already active.");

            ctrl = FindCarousel();
            centerCard = ctrl != null ? GetCenterCard(ctrl) : null;
            var pullX10 = centerCard != null ? GetCardButton(centerCard, "_pullX10Button") : null;

            if (pullX10 != null)
            {
                Debug.Log($"[GachaDemo] Invoking _pullX10Button.onClick on {centerCard.Entry?.BannerId}");
                pullX10.onClick.Invoke();
            }
            else
            {
                // Fallback: find by prefab name in scene
                pullX10 = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                    b.gameObject.name == "PullX10Button" &&
                    !string.IsNullOrEmpty(b.gameObject.scene.name) &&
                    b.isActiveAndEnabled);
                if (pullX10 != null)
                {
                    Debug.Log("[GachaDemo] Fallback: invoking PullX10Button by name");
                    pullX10.onClick.Invoke();
                }
                else
                    Debug.LogWarning("[GachaDemo] PullX10Button not found — skipping beat 5.");
            }
            // Belt-and-suspenders: call Show() directly in case ToastController.Instance was null
            // when the button handler ran (race between Awake and handler dispatch).
            yield return null;
            if (Golfin.UI.Toast.ToastController.Instance != null)
                Golfin.UI.Toast.ToastController.Instance.Show("Coming soon");
            else
                Debug.LogWarning("[GachaDemo] ToastController.Instance still null after activation — toast not shown.");
            yield return new WaitForSecondsRealtime(2.8f);  // toast visible + auto-dismiss

            // ── Wrap up ────────────────────────────────────────────────────────
            float recEnd = Time.realtimeSinceStartup;
            WriteRecordInfo(_recordStart, recEnd);
            Debug.Log($"[GachaDemo] Sequence done ({recEnd - _recordStart:F1}s). Exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
