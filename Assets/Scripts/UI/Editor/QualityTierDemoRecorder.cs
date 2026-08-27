#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.Gameplay.UI.Quality;

namespace Golfin.EditorTools
{
    /// <summary>
    /// quality_tiers (9a) sign-off video. Full iPhone-14 1170x2532, ~45s.
    ///
    /// Driven through the REAL widgets — gear ▸ Graphics ▸ each tier — because the point of the
    /// clip is that the row a player actually taps changes the tier. Modelled on
    /// LanguageSwitchDemoRecorder, which drives the neighbouring Language row the same way.
    ///
    /// Output: Docs/Diagnostics/_capture/qualitytiers/raw.mp4 (+ captions.json), then copied to
    /// Docs/Reports/Media/ at close-out.
    /// Usage: GOLFIN ▸ Quality Tiers ▸ Record Quality Tier Demo Video
    /// </summary>
    public static class QualityTierDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Diagnostics/_capture/qualitytiers";
        const string ArmedKey       = "QualityTierDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Quality Tiers/Record Quality Tier Demo Video")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[TierDemo] stop play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            PlayerSettings.runInBackground = true;   // else the Game View stops emitting frames unfocused
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); return; }
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);
            StartRecorderAndBot();
        }

        static void StartRecorderAndBot()
        {
            int w = 1170, h = 2532;
            PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
            if (cw > 0 && ch > 0 && (cw != 1170 || ch != 2532))
            {
                w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                Debug.LogWarning($"[TierDemo] Game View is {cw}x{ch}, not 1170x2532 — recording at {w}x{h}.");
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "QualityTierDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Constant;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();

            // CaptureCore refuses stills while a Recorder clip is live — a backbuffer read mid-record
            // flips Recorder frames on Metal. Nothing here snaps; the lock is belt-and-braces.
            Golfin.Diagnostics.Runtime.CaptureCore.RecordingActive = true;
            Debug.Log($"[TierDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[QualityTierDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<QualityTierDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            Golfin.Diagnostics.Runtime.CaptureCore.RecordingActive = false;
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[TierDemo] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning("[TierDemo] StopRecorder: " + e.Message); }
            _recorder = null;
        }
    }

    public class QualityTierDemoRunner : MonoBehaviour
    {
        const string OutputDir = "Docs/Diagnostics/_capture/qualitytiers";
        float _t0;
        readonly List<KeyValuePair<float, string>> _marks = new List<KeyValuePair<float, string>>();

        public void StartDemo() => StartCoroutine(Sequence());

        void Mark(string caption)
        {
            float t = Time.time - _t0;
            _marks.Add(new KeyValuePair<float, string>(t, caption));
            Debug.Log($"[TierDemoBot] t={t:F2}s  {caption}");
        }

        static void Click(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) { Debug.LogWarning("[TierDemoBot] not found: " + path); return; }
            var b = go.GetComponent<Button>();
            if (b == null) { Debug.LogWarning("[TierDemoBot] no Button: " + path); return; }
            b.onClick.Invoke();
        }

        static IEnumerator Hold(float s) { yield return new WaitForSeconds(s); }

        const string Row = "SettingsScreen/SettingsPanel/SettingsList/GraphicsRow";

        IEnumerator Sequence()
        {
            _t0 = Time.time;

            // Past the Title/PLAY gate that ScreenManager does not manage.
            yield return Hold(5f);
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b != null && (b.name == "StartButton" || b.name == "PlayButton") && b.gameObject.activeInHierarchy)
                { b.onClick.Invoke(); break; }
            yield return Hold(3f);

            ScreenManager.Instance?.ShowScreen(ScreenId.Home, true);
            LocalizationManager.SetLanguage(Language.English);
            yield return Hold(1.5f);

            Mark("Quality tiers — Low / Mid / High\nresolved from the device, overridable in Settings");
            yield return Hold(2.5f);

            Click("PersistentUI/TopBar/SettingsButton");
            yield return Hold(1.4f);
            Mark("Settings ▸ Graphics");
            Click(Row);
            yield return Hold(2.0f);

            Mark($"Auto resolves this device to {QualityTierService.AutoTier}");
            yield return Hold(2.5f);

            foreach (var (btn, label) in new[] {
                ("HighButton", "High — 0.8 render scale, 2 cascades / 60 m, HDR + bloom"),
                ("MidButton",  "Medium — 0.7 render scale, 1 cascade / 40 m"),
                ("LowButton",  "Low — 0.6 render scale, 1 cascade / 15 m, 30 fps, no tree wind") })
            {
                Click($"{Row}/GraphicsSubmenu/{btn}");
                yield return Hold(0.6f);
                Mark(label + $"\napplied live: {QualityTierService.Current}, {Application.targetFrameRate} fps cap");
                yield return Hold(3.0f);
            }

            Click($"{Row}/GraphicsSubmenu/AutoButton");
            yield return Hold(0.6f);
            Mark("Back to Auto — the device decides");
            yield return Hold(2.5f);

            Click("SettingsScreen/SettingsPanel/CloseButton");
            yield return Hold(1.2f);

            // JP, because the submenu ships localised.
            LocalizationManager.SetLanguage(Language.Japanese);
            Click("PersistentUI/TopBar/SettingsButton");
            yield return Hold(1.2f);
            Click(Row);
            yield return Hold(1.6f);
            Mark("グラフィック — 自動 / 高 / 中 / 低");
            yield return Hold(3.0f);
            LocalizationManager.SetLanguage(Language.English);
            Click("SettingsScreen/SettingsPanel/CloseButton");
            yield return Hold(1.5f);

            // Leave no pinned tier behind.
            PlayerPrefs.DeleteKey(QualityTierService.PrefKey);
            PlayerPrefs.Save();

            WriteCaptions();
            yield return Hold(0.5f);
            EditorApplication.isPlaying = false;
        }

        void WriteCaptions()
        {
            try
            {
                Directory.CreateDirectory(OutputDir);
                // Schema Docs/Scripts/build_bot_video.py --mode captionsjson expects:
                // {"captions":[{"start":..,"end":..,"text":".."}]}. A bare array is NOT accepted —
                // the tool does data.get("captions") and dies on a list.
                var sb = new StringBuilder("{\n  \"captions\": [\n");
                for (int i = 0; i < _marks.Count; i++)
                {
                    float start = _marks[i].Key;
                    float end   = (i + 1 < _marks.Count) ? _marks[i + 1].Key : start + 4f;
                    sb.Append("    {\"start\": ").Append(start.ToString("F2"))
                      .Append(", \"end\": ").Append(end.ToString("F2"))
                      .Append(", \"text\": \"").Append(_marks[i].Value.Replace("\"", "\\\"").Replace("\n", "\\n"))
                      .Append("\"}").Append(i < _marks.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ]\n}\n");
                File.WriteAllText($"{OutputDir}/captions.json", sb.ToString());
                Debug.Log($"[TierDemoBot] captions → {OutputDir}/captions.json ({_marks.Count} marks)");
            }
            catch (Exception e) { Debug.LogWarning("[TierDemoBot] captions: " + e.Message); }
        }
    }
}
#endif
