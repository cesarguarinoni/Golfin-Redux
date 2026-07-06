#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Showcase recorder for the Rewards Center / General Shop (Order 610), modelled on
    /// StaminaShopDemoRecorder. Full iPhone-14 1170x2532 MP4 for the daily report.
    ///
    /// Sequence (ShellScene already loaded): boot → Home → tap the real Gacha nav button →
    ///   Rewards Center (STORE) → scroll list down/up → filter CLUBS → BALLS → ALL →
    ///   tap P.WEDGE's BUY (→ OWNED, RP debits) → exit.
    ///
    /// Output: Docs/Specs/Completed/general_shop_ui/videos/raw.mp4
    /// Usage:  GOLFIN > Shop > Record General Shop Demo
    /// </summary>
    public static class GeneralShopDemoRecorder
    {
        const string OutputDir = "Docs/Specs/Completed/general_shop_ui/videos";
        const string ArmedKey  = "GeneralShopDemoRecorder.Armed";
        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Shop/Record General Shop Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[GenShopDemo] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GenShopDemo] Armed. Entering play mode...");
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
                var asm = System.Reflection.Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[GenShopDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "GeneralShopDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[GenShopDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[GenShopDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GeneralShopDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[GenShopDemo] Recording stopped."); }
                catch (Exception e) { Debug.LogWarning($"[GenShopDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class GeneralShopDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        static bool UnderShop(Transform t)
        {
            while (t != null) { if (t.name == "GeneralShopScreen") return true; t = t.parent; }
            return false;
        }

        static Button FindBtn(string name, bool underShop)
        {
            return Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                b.gameObject.name == name &&
                !string.IsNullOrEmpty(b.gameObject.scene.name) &&
                b.GetComponentInParent<Canvas>() != null &&
                (!underShop || UnderShop(b.transform)));
        }

        static Button ChipBtn(string chipName)
        {
            return Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                b.gameObject.name == chipName &&
                !string.IsNullOrEmpty(b.gameObject.scene.name) && UnderShop(b.transform));
        }

        IEnumerator ScrollTo(ScrollRect sr, float from, float to, float dur)
        {
            if (sr == null) yield break;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                sr.verticalNormalizedPosition = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            sr.verticalNormalizedPosition = to;
        }

        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(4.5f);           // boot → Home

            // Open the Rewards Center via the REAL bottom-nav Gacha button.
            var gacha = FindBtn("NavGachaButton", underShop: false);
            if (gacha != null) { Debug.Log("[GenShopDemo] Tapping Gacha nav → Rewards Center"); gacha.onClick.Invoke(); }
            else GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.GeneralShop, true);
            yield return new WaitForSecondsRealtime(2.0f);           // fade + card gen
            yield return new WaitForSecondsRealtime(2.0f);           // show full list

            // scroll the card list down, then back up
            var sr = Resources.FindObjectsOfTypeAll<ScrollRect>().FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.gameObject.scene.name) &&
                s.GetComponentInParent<Canvas>() != null && UnderShop(s.transform));
            yield return ScrollTo(sr, 1f, 0.2f, 2.5f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return ScrollTo(sr, 0.2f, 1f, 2.0f);
            yield return new WaitForSecondsRealtime(0.8f);

            // category filtering: CLUBS → BALLS → ALL
            var clubs = ChipBtn("CLUBSChip");
            if (clubs != null) { Debug.Log("[GenShopDemo] Filter CLUBS"); clubs.onClick.Invoke(); }
            yield return new WaitForSecondsRealtime(2.0f);
            var balls = ChipBtn("BALLSChip");
            if (balls != null) { Debug.Log("[GenShopDemo] Filter BALLS"); balls.onClick.Invoke(); }
            yield return new WaitForSecondsRealtime(2.0f);
            var all = ChipBtn("ALLChip");
            if (all != null) { Debug.Log("[GenShopDemo] Filter ALL"); all.onClick.Invoke(); }
            yield return new WaitForSecondsRealtime(1.5f);

            // buy a not-owned club (P.WEDGE) → BUY flips to OWNED, RP debits
            var buy = Resources.FindObjectsOfTypeAll<GolfinRedux.UI.Shop.GeneralShopCard>()
                .Where(c => !string.IsNullOrEmpty(c.gameObject.scene.name) && c.gameObject.activeInHierarchy &&
                            c.Entry != null && c.Entry.RefId == "club_pwedge_royal")
                .Select(c => c.transform.Find("CtaGoldButton")?.GetComponent<Button>())
                .FirstOrDefault(b => b != null && b.interactable);
            if (buy != null) { Debug.Log("[GenShopDemo] Buy P.WEDGE"); buy.onClick.Invoke(); }
            yield return new WaitForSecondsRealtime(2.5f);           // show OWNED + RP change

            Debug.Log("[GenShopDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
