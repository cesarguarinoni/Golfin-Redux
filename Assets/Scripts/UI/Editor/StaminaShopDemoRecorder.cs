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
    /// Showcase recorder for the Stamina Boost Shop screens (Order 517), modelled on
    /// TournamentDemoRecorder. Full iPhone-14 1170x2532 MP4 for the pipeline video gate.
    ///
    /// Sequence: (scene already loaded + shop screens mounted in ScreensRoot) →
    ///   Selection (10 cards in panel) → scroll down → scroll up → tap first card →
    ///   Detail (hero + info + 3 menu rows) → tap CANCEL → back to Selection → exit.
    ///
    /// NOTE: does NOT reload/save the scene (the shop mount is in-memory this session).
    /// Output: Docs/Specs/Active/stamina_boost_shop/videos/raw.mp4
    /// Usage:  GOLFIN > Shop > Record Demo Video  (or call LaunchDemo()).
    /// </summary>
    public static class StaminaShopDemoRecorder
    {
        const string OutputDir = "Docs/Specs/Active/stamina_boost_shop/videos";
        const string ArmedKey  = "StaminaShopDemoRecorder.Armed";
        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Shop/Record Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ShopDemo] Already in play mode — stop first.");
                return;
            }
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode(); // uses current in-memory scene (shop mounted) — no save prompt
            Debug.Log("[ShopDemo] Armed. Entering play mode (no scene reload)...");
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
                    Debug.LogWarning($"[ShopDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "ShopDemo";
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
            Debug.Log($"[ShopDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[ShopDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<StaminaShopDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[ShopDemo] Recording stopped."); }
                catch (Exception e) { Debug.LogWarning($"[ShopDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class StaminaShopDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        static Button FindButton(string name)
        {
            return Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == name
                    && !string.IsNullOrEmpty(b.gameObject.scene.name)
                    && b.GetComponentInParent<Canvas>() != null);
        }

        static void Show(string id)
        {
            var idVal = (GolfinRedux.UI.ScreenId)Enum.Parse(typeof(GolfinRedux.UI.ScreenId), id);
            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(idVal, true);
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

            // Deplete the selected character's stamina so BUY renders enabled (realistic "needs a boost").
            try
            {
                var cm  = Golfin.Roster.CharacterManager.Instance;
                var cid = cm != null ? cm.GetSelectedCharacterId() : null;
                var pcd = !string.IsNullOrEmpty(cid) ? cm.GetCharacterData(cid) : null;
                if (pcd != null)
                {
                    pcd.currentStaminaEnergy = Mathf.Max(1, Mathf.RoundToInt(pcd.maxStaminaEnergy * 0.35f));
                    Debug.Log($"[ShopDemo] demo stamina set to 35% ({pcd.currentStaminaEnergy}/{pcd.maxStaminaEnergy}) — BUY enabled");
                }
            }
            catch (Exception e) { Debug.LogWarning("[ShopDemo] stamina setup: " + e.Message); }

            Show("StaminaShopSelection");
            yield return new WaitForSecondsRealtime(1.5f);           // fade + RebuildNextFrame
            yield return new WaitForSecondsRealtime(2.5f);           // show full list

            // scroll the card list down, then back up
            var sr = Resources.FindObjectsOfTypeAll<ScrollRect>()
                .FirstOrDefault(s => !string.IsNullOrEmpty(s.gameObject.scene.name)
                    && s.GetComponentInParent<Canvas>() != null
                    && s.transform.root.name.Contains("Selection") == false
                    && s.GetComponentInParent(typeof(MonoBehaviour)) != null);
            // prefer the shop selection's scroll rect (under CardsPanel)
            sr = Resources.FindObjectsOfTypeAll<ScrollRect>()
                .FirstOrDefault(s => !string.IsNullOrEmpty(s.gameObject.scene.name)
                    && s.GetComponentInParent<Canvas>() != null
                    && FindInParents(s.transform, "StaminaShopSelectionScreen"));
            yield return ScrollTo(sr, 1f, 0.15f, 2.5f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return ScrollTo(sr, 0.15f, 1f, 2.0f);
            yield return new WaitForSecondsRealtime(0.8f);

            // tap the Bar&Lounge (kageroh) card → Detail
            var cards = Resources.FindObjectsOfTypeAll<GolfinRedux.UI.Shop.StaminaShopCard>()
                .Where(c => !string.IsNullOrEmpty(c.gameObject.scene.name) && c.gameObject.activeInHierarchy).ToList();
            var card = cards.FirstOrDefault(c => c.ShopData != null && c.ShopData.Id == "kageroh")
                       ?? cards.FirstOrDefault();
            var cardBtn = card != null ? card.GetComponent<Button>() : null;
            if (cardBtn != null) { Debug.Log("[ShopDemo] Tapping first shop card"); cardBtn.onClick.Invoke(); }
            else Show("StaminaShopDetail");
            yield return new WaitForSecondsRealtime(1.5f);           // fade to detail
            yield return new WaitForSecondsRealtime(4.0f);           // show hero + info + menu rows

            // tap CANCEL → back to Selection
            var cancel = FindButton("StaminaShopCancelButton");
            if (cancel != null) cancel.onClick.Invoke();
            else Show("StaminaShopSelection");
            yield return new WaitForSecondsRealtime(2.0f);

            Debug.Log("[ShopDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        static bool FindInParents(Transform t, string name)
        {
            while (t != null) { if (t.name == name) return true; t = t.parent; }
            return false;
        }
    }
}
#endif
