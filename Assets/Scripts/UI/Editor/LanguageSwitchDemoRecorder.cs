#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Demo recorder for the "language switches repaint in place" fixes
    /// (3e9727653 / c612caba7 / cf3f250f9). Full iPhone-14 1170x2532, ~60s.
    ///
    /// The FIRST switch is driven through the REAL Settings widgets — gear ▸ Language ▸ 日本語 ▸
    /// close — because that is the player path that exposed the bug: Settings is an OVERLAY, so
    /// the screen underneath is never disabled and never re-binds. Later segments toggle the
    /// language directly to keep the clip tight; navigation between them is scripted. What is
    /// under test is the repaint, and every repaint shown happens with the screen already open.
    ///
    /// Output: Docs/Diagnostics/_capture/langswitch/raw.mp4 + captions.json
    /// Usage:  GOLFIN > Localization > Record Language Switch Demo Video
    /// </summary>
    public static class LanguageSwitchDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Diagnostics/_capture/langswitch";
        const string ArmedKey       = "LanguageSwitchDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Localization/Record Language Switch Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[LangSwitchDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            PlayerSettings.runInBackground = true;   // else the Game View stops emitting frames unfocused
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[LangSwitchDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); return; }
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot();
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
                    Debug.LogWarning($"[LangSwitchDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "LanguageSwitchDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            // Constant (not Variable): the Recorder pins Time.captureDeltaTime, so game time
            // advances exactly 1/FrameRate per recorded frame however slowly the editor renders.
            // Paired with WaitForSeconds (NOT ...Realtime) below, scheduled time == video time,
            // which is what makes the caption sidecar line up without hand-fitting.
            settings.FrameRatePlayback = FrameRatePlayback.Constant;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[LangSwitchDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[LanguageSwitchDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<LanguageSwitchDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[LangSwitchDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[LangSwitchDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class LanguageSwitchDemoRunner : MonoBehaviour
    {
        const string OutputDir = "Docs/Diagnostics/_capture/langswitch";

        float _t0;
        readonly List<KeyValuePair<float, string>> _marks = new List<KeyValuePair<float, string>>();

        public void StartDemo() => StartCoroutine(Sequence());

        void Mark(string caption)
        {
            float t = Time.time - _t0;
            _marks.Add(new KeyValuePair<float, string>(t, caption));
            Debug.Log($"[LangSwitchBot] t={t:F2}s  {caption}");
        }

        static void Click(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning($"[LangSwitchBot] not found: {path}"); return; }
            var b = go.GetComponent<Button>();
            if (b == null) { Debug.LogWarning($"[LangSwitchBot] no Button on: {path}"); return; }
            b.onClick.Invoke();
        }

        static void Show(ScreenId id) => ScreenManager.Instance?.ShowScreen(id, true);

        IEnumerator Hold(float s) { yield return new WaitForSeconds(s); }

        IEnumerator Sequence()
        {
            _t0 = Time.time;

            // ── Boot: Logo → Splash → Loading → Home ──────────────────────────
            Mark("Language switches now repaint in place\nno screen re-entry needed");
            yield return Hold(6.0f);
            LocalizationManager.SetLanguage(Language.English);
            yield return Hold(0.5f);

            // ── 1. The reported bug: Main screen mode cards ───────────────────
            Show(ScreenId.Home);
            Mark("Main screen — mode cards in English");
            yield return Hold(2.6f);

            // The REAL player path: the language toggle lives in the Settings OVERLAY.
            Click("PersistentUI/TopBar/SettingsButton");
            yield return Hold(1.0f);
            Mark("Settings is an OVERLAY — the screen behind it stays open");
            Click("SettingsScreen/SettingsPanel/SettingsList/LanguageRow");
            yield return Hold(1.4f);
            Mark("Tapping 日本語 for real");
            Click("SettingsScreen/SettingsPanel/SettingsList/LanguageRow/LanguageSubmenu/JapaneseButton");
            yield return Hold(1.6f);
            Click("SettingsScreen/SettingsPanel/CloseButton");
            yield return Hold(0.4f);
            Mark("Mode cards repainted underneath — the reported bug");
            yield return Hold(3.2f);

            // ── 2. Mode Selection: cards + the top-bar title ──────────────────
            LocalizationManager.SetLanguage(Language.English);
            Click("PersistentUI/BottomNavBar/NavTeeButton");
            yield return Hold(1.8f);
            Mark("Mode Selection — English");
            yield return Hold(2.0f);
            LocalizationManager.SetLanguage(Language.Japanese);
            Mark("Cards AND the top-bar title flip in place");
            yield return Hold(3.0f);

            // ── 3..6 the rest of the repaint fixes ────────────────────────────
            yield return Segment(ScreenId.HoleSelection,
                "Hole Selection — English",
                "LOCKED / NEXT / PLAY + the hole description");

            yield return Segment(ScreenId.Leaderboard,
                "Leaderboard — English",
                "Every rarity label + DIAMOND LEAGUE");

            yield return Segment(ScreenId.TournamentSelection,
                "Tournaments — English",
                "CTAs were never localized at all — now they are");

            yield return Segment(ScreenId.GachaPrizes,
                "Gacha Prizes — English",
                "PULL x10");

            // ── 7. Inventory detail panels (bind on selection) ────────────────
            LocalizationManager.SetLanguage(Language.English);
            Show(ScreenId.Inventory);
            yield return Hold(1.0f);
            Click("BALLSTab");
            yield return Hold(1.4f);
            Mark("Inventory ▸ Balls — English");
            yield return Hold(1.8f);
            LocalizationManager.SetLanguage(Language.Japanese);
            Mark("OWNED / INFO and all five stat names");
            yield return Hold(2.8f);

            LocalizationManager.SetLanguage(Language.English);
            Click("ITEMSTab");
            yield return Hold(1.4f);
            Mark("Inventory ▸ Items — English");
            yield return Hold(1.8f);
            LocalizationManager.SetLanguage(Language.Japanese);
            Mark("RESTORES / INFO / PRO TIP — and the rarity word");
            yield return Hold(2.8f);

            // ── 8. The empty tournament board ─────────────────────────────────
            var svc = Golfin.Tournaments.TournamentService.Instance;
            if (svc != null)
            {
                var defs = svc.Backend.GetTournaments();
                if (defs != null && defs.Count > 0) svc.SelectedTournamentId = defs[0].Id;
            }
            Show(ScreenId.TournamentLeaderboard);
            yield return Hold(1.6f);
            Mark("Empty tournament board used to show fake finishers\nnow it shows the real empty state");
            yield return Hold(3.6f);

            // ── Bookend ───────────────────────────────────────────────────────
            LocalizationManager.SetLanguage(Language.English);
            Show(ScreenId.Home);
            Mark("Back to English");
            yield return Hold(2.4f);

            WriteCaptions(Time.time - _t0);
            Debug.Log("[LangSwitchBot] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        IEnumerator Segment(ScreenId id, string enCaption, string jpCaption)
        {
            LocalizationManager.SetLanguage(Language.English);
            Show(id);
            yield return Hold(1.4f);
            Mark(enCaption);
            yield return Hold(1.8f);
            LocalizationManager.SetLanguage(Language.Japanese);
            Mark(jpCaption);
            yield return Hold(2.8f);
        }

        void WriteCaptions(float duration)
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < _marks.Count; i++)
            {
                float start = _marks[i].Key;
                float end   = (i + 1 < _marks.Count) ? _marks[i + 1].Key : duration;
                string text = _marks[i].Value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
                sb.Append("  {\"start\": ").Append(start.ToString("F3", CultureInfo.InvariantCulture))
                  .Append(", \"end\": ").Append(end.ToString("F3", CultureInfo.InvariantCulture))
                  .Append(", \"text\": \"").Append(text).Append("\"}");
                if (i + 1 < _marks.Count) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("]\n");
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, "captions.json"), sb.ToString());
            Debug.Log($"[LangSwitchBot] Wrote {_marks.Count} captions, duration {duration:F1}s");
        }
    }
}
#endif
