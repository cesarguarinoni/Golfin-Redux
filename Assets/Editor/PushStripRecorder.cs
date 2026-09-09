#if UNITY_EDITOR
// Assets/Editor/PushStripRecorder.cs
// push_arrival_hitch §4c — the two capture items: the before/after 5-frame strips and the
// A/B parallax clip.
//
// ONE RECORDING PER CONFIGURATION, containing BOTH transitions, at 60 fps so a 250 ms push has
// ~15 real frames in it to choose five from. Extracting the strip from a VIDEO rather than
// snapping stills is deliberate: CaptureCore's still path is documented to hand back a phantom
// path and byte-identical stale frames when driven repeatedly inside one coroutine, and a strip
// whose five frames might silently be the same frame is worse than no strip.
//
// It writes a SIDECAR with each push's start time relative to the recording, because finding a
// 250 ms window by eye in a 40-second clip is exactly the kind of thing that ends up off by a
// beat and captions a claim over frames that do not support it.
//
// Deliberately dependency-free (RecorderController + ScreenManager + LayeredPush) so the SAME
// file can be dropped into an older checkout and produce comparable frames.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    public static class PushStripRecorder
    {
        const string ArmedKey = "PushStripRecorder.Armed";
        const string OutDir   = "Docs/Diagnostics/_capture/push_strips";

        static RecorderController _recorder;
        static string _label;

        [MenuItem("GOLFIN/Game Polish/Strips — record the two pushes (60fps)", priority = 264)]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[PushStrip] already playing — stop first."); return; }
            _label = SessionState.GetString("PushStripRecorder.Label", "run");
            Directory.CreateDirectory(OutDir);
            Application.runInBackground = true;
            SessionState.SetString(ArmedKey, "1");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Set from script-execute before Launch(), so one tool serves before/after/AB.</summary>
        public static void SetLabel(string label) => SessionState.SetString("PushStripRecorder.Label", label);

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (SessionState.GetString(ArmedKey, "") != "1") return;
                SessionState.SetString(ArmedKey, "");
                _label = SessionState.GetString("PushStripRecorder.Label", "run");
                StartRecorder();
                var host = new GameObject("[PushStrip]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<Runner>().Label = _label;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode) StopRecorder();
        }

        static void StartRecorder()
        {
            string raw = Path.Combine(OutDir, _label);
            if (File.Exists(raw + ".mp4")) File.Delete(raw + ".mp4");

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "PushStrip";
            movie.Enabled = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1170, OutputHeight = 2532 };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = raw;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 60;                       // ~15 frames inside a 250 ms push
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[PushStrip] recording 1170x2532 @60fps -> {raw}.mp4");
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); } catch { }
            _recorder = null;
        }

        class Mark { public string Name = "", How = ""; public float Start, End; public float Parallax; }

        class Runner : MonoBehaviour
        {
            public string Label = "run";
            float _t0;
            readonly List<Mark> _marks = new List<Mark>();

            void Start() { _t0 = Time.realtimeSinceStartup; StartCoroutine(Go()); }

            float Now => Time.realtimeSinceStartup - _t0;

            static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

            static Button Find(string n) => Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == n && !string.IsNullOrEmpty(b.gameObject.scene.name) && b.isActiveAndEnabled);

            static Type SmT => Type.GetType("GolfinRedux.UI.ScreenManager, Assembly-CSharp");
            static Type IdT => Type.GetType("GolfinRedux.UI.ScreenId, Assembly-CSharp");

            static void Show(string id)
            {
                object sm = SmT.GetProperty("Instance")?.GetValue(null);
                if (sm == null) return;
                var m = SmT.GetMethods().FirstOrDefault(x => x.Name == "ShowScreen" && x.GetParameters().Length >= 1);
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                args[0] = Enum.Parse(IdT, id);
                for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                m.Invoke(sm, args);
            }

            static float LastParallax()
            {
                var t = Type.GetType("Golfin.UI.Polish.LayeredPush, Assembly-CSharp");
                var p = t?.GetProperty("LastPushParallaxFactor");
                return p == null ? float.NaN : (float)p.GetValue(null);
            }

            IEnumerator Push(string name, string how, Action go)
            {
                yield return Wait(1.2f);          // settle, so the mark is the push and nothing else
                var mk = new Mark { Name = name, How = how, Start = Now };
                go();
                yield return Wait(1.0f);          // the push is 0.25s; 1s brackets it generously
                mk.End = Now;
                mk.Parallax = LastParallax();
                _marks.Add(mk);
                Debug.Log($"[PushStrip] {name}: t={mk.Start:F3}..{mk.End:F3}s parallax={mk.Parallax:F2} ({how})");
            }

            IEnumerator Go()
            {
                // Boot through the REAL gate.
                float waited = 0f; bool tapped = false;
                while (waited < 60f)
                {
                    if (Find("NavTeeButton") != null) break;
                    if (!tapped)
                    {
                        var s = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                            b.gameObject.name == "StartButton" && !string.IsNullOrEmpty(b.gameObject.scene.name)
                            && b.gameObject.activeInHierarchy);
                        if (s != null) { s.onClick.Invoke(); tapped = true; }
                    }
                    waited += Time.unscaledDeltaTime; yield return null;
                }
                yield return Wait(3f);

                // ── PUSH 1 — ModeSelection -> MissionSelection, through the REAL mode card ──
                var tee = Find("NavTeeButton");
                if (tee != null) { tee.onClick.Invoke(); yield return Wait(2.5f); }

                Button play = FindModeCardActionButton();
                if (play != null)
                    yield return Push("ModeSelection__MissionSelection",
                                      "REAL widget: mode card ExpandedContainer/ActionButton.onClick",
                                      () => play.onClick.Invoke());
                else
                {
                    Debug.LogWarning("[PushStrip] no mode-card ActionButton — falling back to ShowScreen");
                    yield return Push("ModeSelection__MissionSelection",
                                      "harness ShowScreen (no ActionButton found — NOT a tap)",
                                      () => Show("MissionSelection"));
                }

                // ── PUSH 2 — GachaPrizes -> GeneralShop ──
                // GachaPrizes has no player path without spending a real pull, so BOTH ends are
                // re-seated and the sidecar says so.
                Show("GachaPrizes");
                yield return Wait(2.5f);
                yield return Push("GachaPrizes__GeneralShop",
                                  "harness ShowScreen (GachaPrizes needs a completed pull — NOT a tap)",
                                  () => Show("GeneralShop"));

                WriteSidecar();
                yield return Wait(0.6f);
                EditorApplication.ExitPlaymode();
            }

            static Button FindModeCardActionButton()
            {
                foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
                {
                    if (b.gameObject.name != "ActionButton") continue;
                    if (string.IsNullOrEmpty(b.gameObject.scene.name)) continue;
                    if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                    // must live under ExpandedContainer on a mode card
                    if (b.transform.parent != null && b.transform.parent.name == "ExpandedContainer") return b;
                }
                // nothing expanded yet — expand the first card, then look again
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t.name != "CardTapButton" || string.IsNullOrEmpty(t.gameObject.scene.name)) continue;
                    if (!t.gameObject.activeInHierarchy) continue;
                    var tap = t.GetComponent<Button>();
                    if (tap != null && tap.interactable) { tap.onClick.Invoke(); break; }
                }
                return null;
            }

            void WriteSidecar()
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"label\": \"{Label}\",");
                sb.AppendLine($"  \"fps\": 60,");
                sb.AppendLine("  \"pushes\": [");
                for (int i = 0; i < _marks.Count; i++)
                {
                    var m = _marks[i];
                    sb.AppendLine("    { \"name\": \"" + m.Name + "\", \"start\": " + m.Start.ToString("F3")
                                + ", \"end\": " + m.End.ToString("F3")
                                + ", \"parallax\": " + (float.IsNaN(m.Parallax) ? "null" : m.Parallax.ToString("F2"))
                                + ", \"how\": \"" + m.How.Replace("\"", "'") + "\" }"
                                + (i < _marks.Count - 1 ? "," : ""));
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");
                string p = Path.Combine(OutDir, Label + ".json");
                File.WriteAllText(p, sb.ToString());
                Debug.Log($"[PushStrip] sidecar -> {p}\n{sb}");
            }
        }
    }
}
#endif
