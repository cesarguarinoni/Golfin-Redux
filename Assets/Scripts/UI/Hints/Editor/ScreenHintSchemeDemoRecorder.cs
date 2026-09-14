#if UNITY_EDITOR
// Assets/Scripts/UI/Hints/Editor/ScreenHintSchemeDemoRecorder.cs
// scheme_aware_gameplay_hints — the daily-report clip, ONE SCHEME PER TAKE.
//
// Modelled on LoadingTipsDemoRecorder: Unity Recorder, GAME VIEW source, 1170x2532 @ 30fps,
// with a caption sidecar build_bot_video.py --mode captionsjson burns.
//
// ONE HOLE LOAD PER TAKE, EVER. The first version of this file chained four schemes — four
// hole loads, three quit/unload cycles — into one ~4.5-minute recording and locked the Mac
// (2026-09-14): each hole took longer to come up than the last (3.5 s, 3.3 s, 29 s, 85 s), the
// Recorder fell ~3 minutes behind, and the raw held 98 s of a 270 s run. A take is now: fresh
// install → StartButton → Home hints → (Flick take only: Settings › Controls) → PLAY → hole card
// → the shot-view group → optionally the in-game switch to the NEXT scheme, shown up to CONFIRM
// → exit. Under a minute, one LabScaffold load, no unload while recording. Four takes, then the
// ffmpeg concat FILTER joins the four captioned clips (never the concat demuxer —
// reference_ffmpeg_concat_demuxer_first_image_lost).
//
// EVERY TAP IS A REAL WIDGET. The Splash StartButton, the settings gear and the Controls
// accordion row, the Home mode card's PlayButton, the hole card's ActionButton, the hint modal's
// own NextButton, the in-game gear, its scheme SEGMENT button and the SchemeConfirmModal's
// CONFIRM. Nothing here calls ControlSchemeService.Set() or ShowScreen(); the pref a take boots
// with is set BEFORE play mode, exactly as a reinstall on a device with that scheme chosen.
//
// CAPTIONS ARE STAMPED ON THE RECORDER'S CLOCK, NOT WALL TIME. The Recorder's variable-rate
// timestamps advance by Unity's frame delta, which Time.maximumDeltaTime clamps to 0.333 s — a
// 3.5 s hole load is 0.66 s of video. realtimeSinceStartup drifted 1.4 s ahead of the picture by
// the first hint and 2.8 s by the second on the chained take; Time.time (timeScale 1) advances
// by the same clamped delta the frames carry, so a caption opened at Time.time lands on its frame.
//
// GAME VIEW SOURCE, NOT CAMERA — a camera source drops the Overlay HUD under URP. And NOTHING
// calls CaptureCore while the Recorder runs: a backbuffer read mid-recording flips Recorder
// frames on Metal (reference_botvideorecorder_yflip_fix). Stills come out of the finished mp4.
//
// Usage: GOLFIN > Hints > Record scheme demo take — <scheme>, one at a time, Editor otherwise
// idle. Each take writes tasks/scheme_hint_demo/video/raw_<scheme>.mp4 (git-ignored) and
// Docs/Specs/Quick/media/scheme_aware_gameplay_hints/captions_<scheme>.json. Then per take:
//   python3 Docs/Scripts/build_bot_video.py --scenario scheme_hint_demo --mode captionsjson \
//     --raw-mp4 tasks/scheme_hint_demo/video/raw_<scheme>.mp4 \
//     --captions-json Docs/Specs/Quick/media/scheme_aware_gameplay_hints/captions_<scheme>.json \
//     --output-dir Docs/Specs/Quick/media/scheme_aware_gameplay_hints --suffix <scheme> \
//     --title " " --title-seconds 0.01 --caption-fontsize 44
// and join with the concat FILTER:
//   ffmpeg -i a.mp4 -i b.mp4 -i c.mp4 -i d.mp4 -filter_complex "[0:v][1:v][2:v][3:v]concat=n=4:v=1:a=0" out.mp4
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Golfin.Gameplay.UI.Controls;
using Golfin.UI;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Golfin.EditorTools.Hints
{
    public static class ScreenHintSchemeDemoRecorder
    {
        /// <summary>The raw Recorder output. `tasks/*/video/` is git-ignored; the captioned clip
        /// lands in the Quick task's media folder and is what gets committed.</summary>
        public const string RawDir   = "tasks/scheme_hint_demo/video";
        public const string MediaDir = "Docs/Specs/Quick/media/scheme_aware_gameplay_hints";

        const string ArmedKey   = "ScreenHintSchemeDemo.Armed";
        const string TakeKey    = "ScreenHintSchemeDemo.Take";           // (int)ControlScheme of this take
        const string NextKey    = "ScreenHintSchemeDemo.Next";           // (int)ControlScheme to switch to at the end, or -1
        const string RestoreKey = "ScreenHintSchemeDemo.RestoreScheme";  // the pref to put back, or -1
        const string ShellScene = "Assets/Scenes/ShellScene.unity";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // One take per scheme. Each ends by switching to the next one through the in-game gear
        // (so the join reads as one story) — the last take switches to nothing.
        [MenuItem("GOLFIN/Hints/Record scheme demo take — 1 Flick", priority = 330)]
        public static void TakeFlick() => Launch(ControlScheme.Flick, ControlScheme.Pendulum);

        [MenuItem("GOLFIN/Hints/Record scheme demo take — 2 Pendulum", priority = 331)]
        public static void TakePendulum() => Launch(ControlScheme.Pendulum, ControlScheme.Needle);

        [MenuItem("GOLFIN/Hints/Record scheme demo take — 3 Tap Timing", priority = 332)]
        public static void TakeNeedle() => Launch(ControlScheme.Needle, ControlScheme.FreeSwing);

        [MenuItem("GOLFIN/Hints/Record scheme demo take — 4 Free Swing", priority = 333)]
        public static void TakeFreeSwing() => Launch(ControlScheme.FreeSwing, null);

        static void Launch(ControlScheme take, ControlScheme? next)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SchemeHintDemo] Already playing — stop first."); return; }
            Directory.CreateDirectory(RawDir);
            Directory.CreateDirectory(MediaDir);

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            // A FRESH INSTALL with this scheme chosen: every screen hints again.
            ScreenHintStore.Clear();
            int restore = PlayerPrefs.HasKey(ControlSchemeService.PrefKey) ? PlayerPrefs.GetInt(ControlSchemeService.PrefKey) : -2;
            PlayerPrefs.SetInt(ControlSchemeService.PrefKey, (int)take);
            PlayerPrefs.Save();
            ControlSchemeService.ResetCacheForTests();

            Application.runInBackground = true;   // else the Recorder starves while Unity is behind
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetInt(TakeKey, (int)take);
            SessionState.SetInt(NextKey, next.HasValue ? (int)next.Value : -1);
            SessionState.SetInt(RestoreKey, restore);
            EditorApplication.EnterPlaymode();
            Debug.Log("[SchemeHintDemo] Armed take=" + take + (next.HasValue ? " next=" + next.Value : "") + " (hint state cleared). Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Stopping is unconditional and idempotent — gating it on the armed flag is how you
            // get an mp4 with every frame and no moov atom.
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); return; }
            if (state == PlayModeStateChange.EnteredEditMode) { RestoreScheme(); return; }

            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot((ControlScheme)SessionState.GetInt(TakeKey, 0), SessionState.GetInt(NextKey, -1));
            }
        }

        /// <summary>The player's scheme pref, back the way Launch found it — off the play-mode
        /// exit, so a run that died mid-way still restores.</summary>
        static void RestoreScheme()
        {
            int restore = SessionState.GetInt(RestoreKey, -1);
            if (restore == -1) return;
            SessionState.SetInt(RestoreKey, -1);
            if (restore == -2) PlayerPrefs.DeleteKey(ControlSchemeService.PrefKey);
            else PlayerPrefs.SetInt(ControlSchemeService.PrefKey, restore);
            PlayerPrefs.Save();
            ControlSchemeService.ResetCacheForTests();
            Debug.Log("[SchemeHintDemo] control-scheme pref restored (" + (restore == -2 ? "absent" : ((ControlScheme)restore).ToString()) + ").");
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

        static void StartRecorderAndBot(ControlScheme take, int next)
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
                    Debug.LogWarning($"[SchemeHintDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            // Lock render state BEFORE StartRecording — the Y-flip guard, and the frame cap.
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = 30;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "SchemeHintDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{RawDir}/raw_{take}";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            ScreenHintSchemeDemoRunner.RecordStart = Time.time;
            Debug.Log($"[SchemeHintDemo] Recording → {RawDir}/raw_{take}.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[SchemeHintDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ScreenHintSchemeDemoRunner>().StartDemo(take, next >= 0 ? (ControlScheme?)(ControlScheme)next : null);
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[SchemeHintDemo] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[SchemeHintDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class ScreenHintSchemeDemoRunner : MonoBehaviour
    {
        /// <summary>Time.time at StartRecording(). Captions are stamped against it — see the
        /// header: Time.time advances by the same clamped frame delta the Recorder's timestamps
        /// do, so a caption cannot drift ahead of the picture across a hole load.</summary>
        public static float RecordStart;

        const float Hold = 3.4f;

        ControlScheme _take;
        ControlScheme? _next;
        readonly List<(float start, string text)> _caps = new List<(float, string)>();
        readonly List<string> _log = new List<string>();

        static string Label(ControlScheme s) => s switch
        {
            ControlScheme.Pendulum  => "Pendulum",
            ControlScheme.Needle    => "Tap Timing",
            ControlScheme.FreeSwing => "Free Swing",
            _                       => "Flick",
        };

        // ── captions ──────────────────────────────────────────────────────────

        /// <summary>Open a caption; the previous one ends where this begins. Stamped at the moment
        /// the claim BECOMES TRUE. Pre-wrapped at &lt;= 36 chars per line, no `%`
        /// (feedback_portrait_video_captions_prewrap); nothing rendered into a frame names a
        /// person (feedback_no_names_in_video_captions).</summary>
        void Cap(string text)
        {
            foreach (string line in text.Split('\n'))
                if (line.Length > 36 || line.Contains("%")) Debug.LogWarning("[SchemeHintDemo] caption line too long or has a %: " + line);
            _caps.Add((Time.time - RecordStart, text));
            Log("CAP " + text.Replace("\n", " / "));
        }

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
            float end = Time.time - RecordStart;
            for (int i = 0; i < _caps.Count; i++)
            {
                float s = _caps[i].start;
                float e = (i + 1 < _caps.Count) ? _caps[i + 1].start : end;
                sb.Append("    {\"start\": ").Append(s.ToString("F2"))
                  .Append(", \"end\": ").Append(e.ToString("F2"))
                  .Append(", \"text\": \"")
                  .Append(_caps[i].text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n"))
                  .Append("\"}").Append(i + 1 < _caps.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(ScreenHintSchemeDemoRecorder.MediaDir);
            File.WriteAllText(ScreenHintSchemeDemoRecorder.MediaDir + "/captions_" + _take + ".json", sb.ToString());
            File.WriteAllLines(ScreenHintSchemeDemoRecorder.MediaDir + "/demo_record_" + _take + ".log", _log);
            Debug.Log("[SchemeHintDemo] wrote " + _caps.Count + " captions for " + _take + ".");
        }

        void Log(string s)
        {
            string line = (Time.time - RecordStart).ToString("F2").PadLeft(7) + "s  (real " + (Time.realtimeSinceStartup).ToString("F1") + ")  " + s;
            _log.Add(line);
            Debug.Log("[SchemeHintDemo] " + s);
        }

        public void StartDemo(ControlScheme take, ControlScheme? next)
        {
            _take = take; _next = next;
            StartCoroutine(Sequence());
        }

        // ── real widgets ──────────────────────────────────────────────────────

        static ScreenHintModalController Modal => ScreenHintModalController.Instance;
        static bool HintUp => Modal != null && Modal.IsVisible();
        static string HintKey()
        {
            var m = Modal; if (m == null) return "?";
            var t = m.transform.Find("Panel/TipContent/TipText");
            var lt = t != null ? t.GetComponent<LocalizedText>() : null;
            var f = typeof(LocalizedText).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
            return lt != null && f != null ? (string)f.GetValue(lt) : "?";
        }

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

        IEnumerator WaitHint(float timeout, string what)
        {
            float t = 0f;
            while (t < timeout && !HintUp) { t += Time.unscaledDeltaTime; yield return null; }
            Log(HintUp ? "hint up for " + what + " after " + t.ToString("F1") + "s real: " + HintKey() + " n=" + (Modal.Index + 1) + "/" + Modal.Count
                       : "TIMEOUT waiting for the hint (" + what + ")");
        }

        IEnumerator TapNext(string why)
        {
            var b = Modal.transform.Find("Panel/ButtonsRow/NextButton").GetComponent<Button>();
            int before = Modal.Index;
            Press(b);
            Log("NEXT (" + why + "): " + before + " -> " + Modal.Index + " key=" + HintKey());
            yield return null;
        }

        /// <summary>Walk a group to its end with CONTINUE, then CLOSE it — the modal's own button.</summary>
        IEnumerator CloseGroup(string tag)
        {
            int guard = 0;
            while (HintUp && guard++ < 12)
            {
                bool last = Modal.Index == Modal.Count - 1;
                yield return TapNext(last ? tag + " CLOSE" : tag + " continue");
                yield return Settle(last ? 0.6f : 0.45f);
            }
            float t = 0f;
            while (t < 2f && HintUp) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static IEnumerator Settle(float s) { yield return new WaitForSecondsRealtime(s); }

        // ── the in-game switch, shown up to CONFIRM (no quit, no reload — the take ends) ──

        IEnumerator SwitchSchemeInGame(ControlScheme scheme)
        {
            var gear = GameObject.Find("LabRoot/ShotUI_Canvas/SettingsButton")?.GetComponent<Button>();
            var modal = UnityEngine.Object.FindObjectsByType<InGameSettingsModalController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => !string.IsNullOrEmpty(m.gameObject.scene.name));
            if (gear == null || modal == null) { Log("SWITCH: gear or in-game modal missing"); yield break; }

            Press(gear);
            Log("tapped in-game gear");
            yield return Settle(1.4f);

            var segF = typeof(InGameSettingsModalController).GetField("schemeButtons", BindingFlags.Instance | BindingFlags.NonPublic);
            var segments = segF != null ? segF.GetValue(modal) as Button[] : null;
            var seg = segments != null && (int)scheme < segments.Length ? segments[(int)scheme] : null;
            if (seg == null) { Log("SWITCH: segment button missing for " + scheme); yield break; }
            Cap("Switching to " + Label(scheme) + " from the\nin-game gear…");
            yield return Settle(1.2f);
            Press(seg);
            Log("tapped segment " + scheme);
            yield return Settle(1.3f);

            var popup = SchemeConfirmModalController.Instance;
            Log("SchemeConfirmModal visible=" + (popup != null && popup.IsVisible()) + " pending=" + (popup != null ? popup.PendingScheme.ToString() : "-"));
            Cap("…the pop-up explains the new\nscheme, then CONFIRM commits it");
            yield return Settle(3.2f);
            var confirm = popup != null ? popup.transform.Find("Panel/ButtonsRow/ConfirmButton")?.GetComponent<Button>() : null;
            if (confirm != null) { Press(confirm); Log("tapped CONFIRM"); } else Log("SWITCH: CONFIRM button missing");
            float t = 0f;
            while (t < 3f && ControlSchemeService.Current != scheme) { t += Time.unscaledDeltaTime; yield return null; }
            Log("scheme now " + ControlSchemeService.Current + " (expect " + scheme + ")");
            Cap(Label(scheme) + " is selected —\nthe next first hole shows its tip");
            yield return Settle(2.4f);
        }

        // ── the run: one fresh install, one hole ──────────────────────────────

        IEnumerator Sequence()
        {
            Application.runInBackground = true;
            Log("take=" + _take + " next=" + (_next.HasValue ? _next.Value.ToString() : "-") + " scheme=" + ControlSchemeService.Current
                + " state=" + PlayerPrefs.GetString(ScreenHintStore.PrefsKey, "(none)"));

            // Splash gate — the real StartButton.
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

            // Home's two hints — a player closes them before anything else is tappable.
            yield return WaitHint(20f, "Home");
            yield return Settle(0.8f);
            if (_take == ControlScheme.Flick) Cap("Shot-view tips follow the\nselected control scheme");
            yield return Settle(0.8f);
            yield return CloseGroup("home");
            yield return Settle(0.6f);

            // ── Flick take only: Settings › Controls, the reworded tip, the four rows ──
            if (_take == ControlScheme.Flick)
            {
                var pui = FindLive<PersistentUIManager>();
                if (pui != null && pui.settingsButton != null) { Press(pui.settingsButton); Log("tapped settings gear"); }
                yield return Settle(1.0f);
                var settings = FindLive<SettingsController>();
                var controls = settings != null ? settings.controlsSubmenu : null;
                var row = controls != null ? controls.GetComponentInParent<SettingsMenuItem>(true) : null;
                if (row != null)
                {
                    var rowButton = row.GetComponent<Button>();
                    if (rowButton != null) { Press(rowButton); Log("tapped Controls row"); } else { row.Expand(); Log("Controls row Expand()"); }
                }
                else Log("Controls row not found");
                yield return WaitHint(10f, "SettingsControls");
                yield return Settle(0.8f);
                Cap("Settings › Controls: the tip now\nnames all four schemes");
                yield return Settle(Hold + 0.6f);
                yield return CloseGroup("settings_controls");
                yield return Settle(0.5f);
                Cap("Flick is the current scheme —\nthe shipping default");
                yield return Settle(2.2f);
                if (settings != null) { settings.CloseSettings(); Log("settings closed"); }
                yield return Settle(0.8f);
            }

            // ── the one hole ──────────────────────────────────────────────────
            yield return TapWhenLive("PlayButton");
            yield return WaitHint(10f, "HoleSelection");
            yield return Settle(1.0f);
            yield return CloseGroup("holeselection");
            yield return Settle(0.4f);
            yield return TapWhenLive("ActionButton");
            yield return WaitHint(90f, "Gameplay");
            yield return Settle(0.9f);

            int total = Modal != null ? Modal.Count : 0;
            switch (_take)
            {
                case ControlScheme.Flick:
                    Cap("Flick: the first hole opens on\nthe cone tips — 1/" + total + ", as before");
                    yield return Settle(Hold);
                    yield return TapNext("flick 1->2");
                    yield return Settle(0.7f);
                    Cap("2/" + total + " is the cone's aim slide —\nFlick's own instruction");
                    yield return Settle(Hold - 0.4f);
                    break;
                case ControlScheme.Pendulum:
                    Cap("Pendulum: the first hole opens on\nthe Pendulum tip — 1/" + total);
                    yield return Settle(Hold);
                    yield return TapNext("pendulum 1->2");
                    yield return Settle(0.7f);
                    Cap("No cone tips for Pendulum —\nthe shared four follow");
                    yield return Settle(Hold - 0.6f);
                    break;
                case ControlScheme.Needle:
                    Cap("Tap Timing: the needle tip — 1/" + total);
                    yield return Settle(Hold);
                    break;
                default:
                    Cap("Free Swing: the swing-lane tip — 1/" + total);
                    yield return Settle(Hold);
                    Cap("Same physics, same course —\nthe tutorial matches the hand");
                    yield return Settle(Hold);
                    break;
            }
            yield return CloseGroup(_take.ToString().ToLowerInvariant());
            yield return Settle(0.6f);

            if (_next.HasValue) yield return SwitchSchemeInGame(_next.Value);

            WriteCaptions();

            // ExitPlaymode(), NOT `isPlaying = false` — the graceful path is what lets the
            // Recorder's ExitingPlayMode hook finalise the container. No quit, no unload: the
            // take ends on the hole it loaded.
            yield return Settle(0.8f);
            Log("Take done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
