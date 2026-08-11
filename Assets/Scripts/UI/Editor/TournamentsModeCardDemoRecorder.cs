#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI.ModeSelect;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Showcase recorder for the TOURNAMENTS mode card (task `tournaments_mode_card`).
    /// Cloned from TournamentDemoRecorder / RankingsDemoRecorder — same Unity Recorder
    /// GameView pipeline, full iPhone-14 1170x2532, no RT/stills read while recording
    /// (that is the documented y-flip trigger).
    ///
    /// Drives the REAL player path throughout — the app boots to a Splash/PLAY gate that
    /// ScreenManager does not own, so the sequence CLICKS that button rather than calling
    /// ShowScreen behind it (which would record the splash frame while CurrentScreen lied).
    ///
    /// Sequence:
    ///   Splash PLAY -> Home carousel (Practice centred) -> tap TOURNAMENTS card (slides
    ///   to centre, gold title + white border) -> tap tagline to expand -> PLAY ->
    ///   TournamentSelection (T7) -> bottom-nav Tee -> full-screen Mode Select ->
    ///   tap the Tournaments row (expands: NO ENTRY FEE / Varies by tournament, no coins)
    ///   -> PLAY -> TournamentSelection -> Home in Japanese -> exit.
    ///
    /// Output: Docs/Specs/Active/tournaments_mode_card/videos/raw.mp4
    ///         + captions.json sidecar consumed by
    ///           `Docs/Scripts/build_bot_video.py --mode captionsjson`.
    /// Usage:  GOLFIN > Tournaments > Record Mode Card Demo  (or call LaunchDemo()).
    /// </summary>
    public static class TournamentsModeCardDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/tournaments_mode_card/videos";
        const string ArmedKey       = "TournamentsModeCardDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Tournaments/Record Mode Card Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ModeCardDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[ModeCardDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
            }
        }

        static void StartRecorderAndBot()
        {
            // The Game View size must already be pinned BEFORE StartRecording — changing
            // render state afterwards is one of the two documented y-flip triggers.
            int w = 1170, h = 2532;
            PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
            if (cw > 0 && ch > 0)
            {
                w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                if (w % 2 != 0) w--; if (h % 2 != 0) h--;
            }
            if (w != 1170 || h != 2532)
                Debug.LogWarning($"[ModeCardDemo] Game View is {w}x{h}, not the iPhone-14 1170x2532 preset. "
                               + "Pin it in the Game View dropdown and re-run for a full-size clip.");

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "TournamentsModeCardDemo";
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
            Debug.Log($"[ModeCardDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[TournamentsModeCardDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<TournamentsModeCardDemoRunner>().StartDemo(OutputDir);
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording())
                    _recorder.StopRecording();
                Debug.Log("[ModeCardDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[ModeCardDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class TournamentsModeCardDemoRunner : MonoBehaviour
    {
        string _outputDir;
        float  _t0;
        readonly List<string> _captions = new List<string>();

        public void StartDemo(string outputDir)
        {
            _outputDir = outputDir;
            _t0 = Time.realtimeSinceStartup;
            StartCoroutine(Sequence());
        }

        // ── caption sidecar (consumed by build_bot_video.py --mode captionsjson) ──
        float Now => Time.realtimeSinceStartup - _t0;

        void Say(string text, float seconds)
        {
            float start = Now;
            _captions.Add("{\"start\":" + start.ToString("F3")
                        + ",\"end\":" + (start + seconds).ToString("F3")
                        + ",\"text\":\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}");
        }

        void WriteCaptions()
        {
            try
            {
                Directory.CreateDirectory(_outputDir);
                var sb = new StringBuilder();
                sb.Append("{\"captions\":[").Append(string.Join(",", _captions)).Append("]}");
                File.WriteAllText(Path.Combine(_outputDir, "captions.json"), sb.ToString());
                Debug.Log($"[ModeCardDemo] Wrote {_captions.Count} captions.");
            }
            catch (Exception e) { Debug.LogWarning($"[ModeCardDemo] WriteCaptions: {e.Message}"); }
        }

        // ── lookup helpers (scene-scoped, same pattern as the sibling recorders) ──
        static Button FindButton(string name)
        {
            return Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == name
                    && !string.IsNullOrEmpty(b.gameObject.scene.name)
                    && b.gameObject.activeInHierarchy
                    && b.GetComponentInParent<Canvas>() != null);
        }

        static T Field<T>(object o, string name) where T : class
        {
            var f = o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(o) as T;
        }

        // The home carousel is a 3x virtual array: every mode exists as THREE live card
        // instances. Only the middle-pass instance has OnPlayClicked wired, and expand only
        // acts on whichever instance is currently centred — so "first match wins" silently
        // taps a side card and the step no-ops. These helpers address the right instance.
        static ModeCarouselController Carousel()
        {
            var c = UnityEngine.Object.FindFirstObjectByType<ModeCarouselController>();
            return (c != null && c.gameObject.activeInHierarchy) ? c : null;
        }

        static List<ModeCardController> CarouselCards(ModeCarouselController carousel)
        {
            return typeof(ModeCarouselController)
                .GetField("_allCards", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(carousel) as List<ModeCardController>;
        }

        /// <summary>The middle-pass instance of a mode — the one whose PLAY is wired.</summary>
        static ModeCardController HomeMiddlePassCard(string modeId)
        {
            var carousel = Carousel();
            if (carousel == null) return null;
            var all = CarouselCards(carousel);
            if (all == null || all.Count == 0) return null;
            int n = all.Count / 3;
            for (int i = n; i < 2 * n && i < all.Count; i++)
                if (all[i] != null && all[i].ModeId == modeId) return all[i];
            return null;
        }

        /// <summary>Whichever card instance is currently centred in the home carousel.</summary>
        static ModeCardController HomeCentredCard()
        {
            var carousel = Carousel();
            if (carousel == null) return null;
            var all = CarouselCards(carousel);
            int idx = (int)(typeof(ModeCarouselController)
                .GetField("_centeredVirtualIndex", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(carousel) ?? -1);
            if (all == null || idx < 0 || idx >= all.Count) return null;
            return all[idx];
        }

        /// <summary>
        /// The live TOURNAMENTS card the player would actually tap: the centred carousel
        /// instance on Home, or the single list row on the full-screen Mode Select.
        /// </summary>
        static ModeCardController TournamentsCard()
        {
            if (Carousel() != null)
            {
                var centred = HomeCentredCard();
                if (centred != null && centred.ModeId == "tournaments") return centred;
                return HomeMiddlePassCard("tournaments");
            }
            return UnityEngine.Object
                .FindObjectsByType<ModeCardController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.ModeId == "tournaments" && c.gameObject.activeInHierarchy);
        }

        /// <summary>Invoke a private serialized Button on a card, logging when it is missing.</summary>
        static bool Tap(ModeCardController card, string fieldName, string what)
        {
            if (card == null) { Debug.LogWarning($"[ModeCardDemo] {what}: no card."); return false; }
            var btn = Field<Button>(card, fieldName);
            if (btn == null) { Debug.LogWarning($"[ModeCardDemo] {what}: '{fieldName}' null on {card.name}."); return false; }
            btn.onClick.Invoke();
            Debug.Log($"[ModeCardDemo] {what}: tapped {fieldName} on {card.name} (state={card.State}).");
            return true;
        }

        IEnumerator Sequence()
        {
            // ── Boot through the Splash/PLAY gate the REAL way ──
            // No opening caption here: build_bot_video.py renders the centred title card
            // from --title. Emitting it again would double-draw it (centre + bottom).
            yield return new WaitForSecondsRealtime(5.0f);

            var start = FindButton("StartButton");
            if (start != null) start.onClick.Invoke();
            else Debug.LogWarning("[ModeCardDemo] Splash StartButton not found.");
            yield return new WaitForSecondsRealtime(6.0f);

            // ── Home carousel, Practice centred by default ──
            Say("Home carousel — Practice centred", 2.5f);
            yield return new WaitForSecondsRealtime(2.5f);

            // Tap the middle-pass TOURNAMENTS card: it slides to centre (gold title, white border).
            var middle = HomeMiddlePassCard("tournaments");
            if (middle != null) middle.GetComponent<Button>()?.onClick.Invoke();
            else Debug.LogWarning("[ModeCardDemo] No middle-pass tournaments card.");
            Say("Tap TOURNAMENTS — order 3", 4.0f);
            yield return new WaitForSecondsRealtime(4.0f);

            // Expand via the tagline row — must act on the CENTRED instance (the snap's
            // NormalizeCenterInstant can swap which instance holds the centre).
            var centred = HomeCentredCard();
            Debug.Log($"[ModeCardDemo] Centred card after snap = {(centred == null ? "<null>" : centred.ModeId)}");
            Tap(centred, "taglineButton", "expand");
            yield return new WaitForSecondsRealtime(0.6f);
            Say("NO ENTRY FEE · no coin icons\\nREWARDS: Varies by tournament", 4.5f);
            yield return new WaitForSecondsRealtime(4.5f);

            // ── PLAY from the home carousel (centred instance owns the wired PLAY) ──
            Tap(HomeCentredCard(), "playButton", "home PLAY");
            Say("PLAY → Tournament Selection", 5.0f);
            yield return new WaitForSecondsRealtime(5.0f);
            Debug.Log($"[ModeCardDemo] After home PLAY, CurrentScreen = {GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen}");

            // ── Full-screen Mode Select via the bottom-nav Tee button ──
            var tee = FindButton("NavTeeButton");
            if (tee != null) tee.onClick.Invoke();
            Say("Bottom-nav Tee → Mode Select", 3.5f);
            yield return new WaitForSecondsRealtime(3.5f);

            // Expand the Tournaments row (single instance on the full-screen list).
            Tap(TournamentsCard(), "cardTapButton", "list expand");
            yield return new WaitForSecondsRealtime(0.6f);
            Say("Row expands — full description", 4.5f);
            yield return new WaitForSecondsRealtime(4.5f);

            // ── PLAY from the full-screen list ──
            Tap(TournamentsCard(), "playButton", "list PLAY");
            Say("PLAY again — both routes work", 5.0f);
            yield return new WaitForSecondsRealtime(5.0f);
            Debug.Log($"[ModeCardDemo] After list PLAY, CurrentScreen = {GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen}");

            // ── Japanese pass ──
            LocalizationManager.SetLanguage(Language.Japanese);
            var home = FindButton("NavHomeButton");
            if (home != null) home.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3.0f);

            var jpCard = HomeMiddlePassCard("tournaments");
            if (jpCard != null) jpCard.GetComponent<Button>()?.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.0f);
            // ASCII only: the ffmpeg caption font has no CJK glyphs and would render tofu.
            // The card itself shows the Japanese strings natively.
            Say("Japanese localisation", 5.0f);
            yield return new WaitForSecondsRealtime(5.0f);

            LocalizationManager.SetLanguage(Language.English);
            WriteCaptions();
            Debug.Log("[ModeCardDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
