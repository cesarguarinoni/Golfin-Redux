#if UNITY_EDITOR
// ─────────────────────────────────────────────────────────────────────────────
// The whole GPS surface, in one take, for the report.
//
// Seven screens and three modals, walked the way a player walks them: Home → the
// GPS pill → Hub → Profile → My Avatar → Badges → Gift (+ send and buy modals) →
// Vote (+ MINE, + create modal) → back to the Hub. Every step is a REAL widget's
// onClick, so the clip is evidence and not a slideshow — if a hub affordance is
// dead, the recording stalls on it rather than cutting past it.
//
// Same construction as RankingsDemoRecorder / TournamentDemoRecorder: Unity
// Recorder over the GAME VIEW at the full 1170x2532. GameView, not a camera —
// under URP a camera source drops the Overlay HUD, which here is the whole top
// bar and the GPS nav bar.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    public static class GpsFlowDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Reports/Media/gps_flow";
        const string ArmedKey       = "GpsFlowDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Gps/Record GPS Flow Video", priority = 230)]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GpsFlowDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GpsFlowDemo] Armed. Entering play mode...");
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
                    Debug.LogWarning($"[GpsFlowDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "GpsFlowDemo";
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
            GpsFlowDemoRunner.RecordStart = Time.realtimeSinceStartup;
            Debug.Log($"[GpsFlowDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[GpsFlowDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GpsFlowDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[GpsFlowDemo] Recording stopped."); }
                catch (Exception e) { Debug.LogWarning($"[GpsFlowDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class GpsFlowDemoRunner : MonoBehaviour
    {
        /// <summary>Bot-clock time at StartRecording(), so every caption below is stamped in
        /// seconds since the first frame — which is exactly what build_bot_video.py's
        /// `--mode captionsjson` expects, and why this clip needs no hand-timed caption list
        /// that would drift the moment a hold changes.</summary>
        public static float RecordStart;

        readonly List<(float start, string text)> _caps = new List<(float, string)>();

        /// <summary>Open a caption. The previous one ends where this begins.</summary>
        void Cap(string text) => _caps.Add((Time.realtimeSinceStartup - RecordStart, text));

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
            float end = Time.realtimeSinceStartup - RecordStart;
            for (int i = 0; i < _caps.Count; i++)
            {
                float s = _caps[i].start;
                float e = (i + 1 < _caps.Count) ? _caps[i + 1].start : end;
                sb.Append("    {\"start\": ").Append(s.ToString("F2"))
                  .Append(", \"end\": ").Append(e.ToString("F2"))
                  .Append(", \"text\": \"").Append(_caps[i].text.Replace("\"", "\\\""))
                  .Append("\"}").Append(i + 1 < _caps.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory("Docs/Reports/Media/gps_flow");
            File.WriteAllText("Docs/Reports/Media/gps_flow/captions.json", sb.ToString());
            Debug.Log("[GpsFlowDemo] wrote " + _caps.Count + " captions.");
        }

        /// <summary>How long a screen holds before the next tap. Long enough that a viewer can
        /// read a panel, and long enough for the live requests behind it to land.</summary>
        const float Hold = 3.2f;
        const float Settle = 1.0f;

        public void StartDemo()
        {
            // Without this the Editor stops rendering the moment it loses focus and the recording
            // becomes a still of whatever it drew last.
            Application.runInBackground = true;
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            // ── boot ─────────────────────────────────────────────────────────
            yield return Until(() => ScreenManager.Instance != null, 30f);
            yield return TapAnywhere("StartButton", 90f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.Home, 90f);
            Cap("Boot \u2192 Home");
            yield return new WaitForSecondsRealtime(2.5f);

            // ── Home → the GPS pill → Hub ────────────────────────────────────
            yield return TapAnywhere("GpsPill", 20f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            // Stamped BEFORE the hold: a Cap() placed after it would open the caption at the
            // moment the NEXT tap happens and show for a fraction of a second.
            Cap("GPS Hub \u2014 live hero + recent rounds");
            yield return new WaitForSecondsRealtime(Hold + 1f);   // the hero and rounds fill in

            GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");

            // ── the PROFILE pillar: Profile → My Avatar → Badges ─────────────
            yield return TapIn(hub, "GpsNavBar/NavProfileButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsProfile, 20f);
            Cap("Profile \u2014 /score/stats + /badges/progress");
            yield return new WaitForSecondsRealtime(Hold + 1f);

            GameObject profile = GameObject.Find("Canvas/ScreensRoot/GpsProfileScreen");
            yield return TapFirstIn(profile, "AvatarShortcut");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsAvatar, 20f);
            Cap("My Avatar \u2014 PLAYLIFE level meets the GOLFIN character");
            yield return new WaitForSecondsRealtime(Hold);

            Show(ScreenId.GpsProfile);
            yield return new WaitForSecondsRealtime(Settle);
            yield return TapFirstIn(profile, "BadgesShortcut");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsBadges, 20f);
            Cap("Badges \u2014 four sections, earned state each");
            yield return new WaitForSecondsRealtime(Hold);

            // ── back to the hub, then GIFT through its own nav slot ──────────
            Show(ScreenId.GpsHub);
            yield return new WaitForSecondsRealtime(Settle + 0.4f);

            yield return TapIn(hub, "GpsNavBar/NavGiftButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsGift, 20f);
            Cap("GIFTS \u2014 gift_pts, discover, the live catalog");
            yield return new WaitForSecondsRealtime(Hold + 2f);   // four requests land here

            GameObject gift = GameObject.Find("Canvas/ScreensRoot/GpsGiftScreen");
            Cap("SEND GIFT \u2014 balance is activity_pts");
            yield return TapIn(gift, "ContentContainer/Golfers/Golfer0/SendGiftButton");
            yield return new WaitForSecondsRealtime(Hold);        // the SEND A GIFT modal
            yield return TapIn(gift, "GiftSendModal/ModalPanel/CancelButtonRow/CancelButton");
            yield return new WaitForSecondsRealtime(Settle);

            Cap("The same modal, purchase mode");
            yield return TapIn(gift, "ContentContainer/BuyGifts/GiftItems/Item0");
            yield return new WaitForSecondsRealtime(Hold);        // the same modal, purchase mode
            yield return TapIn(gift, "GiftSendModal/ModalPanel/CancelButtonRow/CancelButton");
            yield return new WaitForSecondsRealtime(Settle);

            // ── the hub's VOTE tile ──────────────────────────────────────────
            Show(ScreenId.GpsHub);
            yield return new WaitForSecondsRealtime(Settle + 0.4f);

            yield return TapIn(hub, "ContentContainer/ActionTiles/Tile_VOTE");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsVote, 20f);
            Cap("VOTES \u2014 five live votes from /vote/list");
            yield return new WaitForSecondsRealtime(Hold + 2f);   // the list arrives

            GameObject vote = GameObject.Find("Canvas/ScreensRoot/GpsVoteScreen");

            // Scroll the feed so the clip shows more than the first two cards.
            var scroll = vote.transform.Find("ContentContainer/VoteList").GetComponent<ScrollRect>();
            yield return Scroll(scroll, 1f, 0f, 2.4f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Scroll(scroll, 0f, 1f, 1.6f);
            yield return new WaitForSecondsRealtime(0.6f);

            Cap("MINE \u2014 filtered on creator_id");
            yield return TapIn(vote, "ContentContainer/ChipsRow/Chip3");      // MINE
            yield return new WaitForSecondsRealtime(Hold);
            yield return TapIn(vote, "ContentContainer/ChipsRow/Chip2");      // PUBLIC
            yield return new WaitForSecondsRealtime(Settle);

            Cap("CREATE \u2014 question, YES/NO, expiry");
            yield return TapIn(vote, "ContentContainer/ChipsRow/CreateButton");
            yield return new WaitForSecondsRealtime(Hold);                    // CREATE A VOTE
            yield return TapIn(vote, "VoteCreateModal/ModalPanel/CancelButtonRow/CancelButton");
            yield return new WaitForSecondsRealtime(Settle);

            // ── land back on the hub ─────────────────────────────────────────
            Show(ScreenId.GpsHub);
            yield return new WaitForSecondsRealtime(2.2f);

            Cap("Back at the hub");
            yield return new WaitForSecondsRealtime(1.2f);

            WriteCaptions();
            Debug.Log("[GpsFlowDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// A RETURN to a screen already visited, used only where the frame offers no back
        /// affordance of its own. Every FORWARD navigation in this clip is a real onClick — that
        /// is the part the recording is evidence for.
        /// </summary>
        static void Show(ScreenId id) => ScreenManager.Instance?.ShowScreen(id);

        static IEnumerator Scroll(ScrollRect sr, float from, float to, float seconds)
        {
            if (sr == null) yield break;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                sr.verticalNormalizedPosition = Mathf.Lerp(from, to, k);
                yield return null;
            }
            sr.verticalNormalizedPosition = to;
        }

        IEnumerator TapIn(GameObject root, string path)
        {
            Transform t = root != null ? root.transform.Find(path) : null;
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null) { Debug.LogWarning($"[GpsFlowDemo] no button at {path}"); yield break; }
            b.onClick.Invoke();
            yield return null;
        }

        /// <summary>Tap the first of these names that exists anywhere under <paramref name="root"/>.
        /// The Profile screen's shortcuts are `AvatarShortcut` / `BadgesShortcut` (read off the
        /// prefab, after a first take guessed `BadgeShortcut` and silently skipped the screen).</summary>
        IEnumerator TapFirstIn(GameObject root, params string[] names)
        {
            if (root != null)
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                    if (names.Contains(b.gameObject.name))
                    {
                        b.onClick.Invoke();
                        yield return null;
                        yield break;
                    }
            Debug.LogWarning("[GpsFlowDemo] none of [" + string.Join(", ", names) + "] found");
        }

        IEnumerator TapAnywhere(string name, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                               FindObjectsSortMode.None))
                    if (b.name == name && b.gameObject.activeInHierarchy)
                    {
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1f);
                        yield break;
                    }
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Debug.LogWarning($"[GpsFlowDemo] '{name}' never appeared in {seconds}s");
        }

        IEnumerator Until(Func<bool> done, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!done()) Debug.LogWarning("[GpsFlowDemo] timed out waiting for a screen");
        }
    }
}
#endif
