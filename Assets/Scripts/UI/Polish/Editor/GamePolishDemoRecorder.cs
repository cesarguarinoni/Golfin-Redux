// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §A4 — the six clips Cesar judges the gamble from.
//
// THE RECORDER FAMILY, REUSED. This is GeneralShopDemoRecorder's shape verbatim
// (arm -> EnterPlaymode -> RecorderController with a GameViewInputSettings at the
// pinned iPhone-14 resolution -> a runner MonoBehaviour drives real widgets ->
// StopRecording on exit). Nothing here hand-rolls a capture path: the known
// y-flip has TWO triggers — a render-state change after StartRecording, and ANY
// ScreenCapture / RenderTexture read while the recorder is running — so this
// takes no stills at all, and A4's per-clip stills are extracted from the MP4
// afterwards (memory: botvideorecorder_yflip_fix, video_flip_verification).
//
// ONE SESSION, ONE RECORDING, SIX SEGMENTS. Six separate play sessions would be
// six chances for the Editor to come up on a different screen, and six recorders
// in one session is the arrangement that has historically produced flipped and
// truncated files. Instead the whole route is recorded once and the runner writes
// a sidecar of segment boundaries (on the SAME clock, so the cut is exact);
// Docs/Scripts/cut_game_polish_clips.py slices and captions it.
//
// SEGMENT (f) IS THE ONLY THING THAT TOUCHES THE FLAG. It is turned on
// immediately before that segment and off immediately after, inside a
// try/finally, and the sidecar records the flag state per segment so the clip
// that ships as "OPTION (b)" cannot be confused with one that was not.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GolfinRedux.UI;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishDemoRecorder
    {
        const string OutputDir = "Docs/Specs/Active/game_polish_a/videos";
        const string ArmedKey  = "GamePolishDemoRecorder.Armed";
        static RecorderController? _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Game Polish/Record the A4 demo (six segments, one take)", priority = 265)]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[GamePolishDemo] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GamePolishDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); return; }
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);
            StartRecorderAndBot();
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
                    Debug.LogWarning($"[GamePolishDemo] Could not pin iPhone-14 — recording at {w}x{h}. " +
                                     "A4 wants full size (memory: record_bot_video_full_size).");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "GamePolishDemo";
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
            Debug.Log($"[GamePolishDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[GamePolishDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GamePolishDemoRunner>().Begin(Time.realtimeSinceStartup);
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[GamePolishDemo] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[GamePolishDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    /// <summary>Drives the six segments through real widgets and writes the cut sidecar.</summary>
    public class GamePolishDemoRunner : MonoBehaviour
    {
        const string OutputDir = "Docs/Specs/Active/game_polish_a/videos";

        float _t0;
        readonly List<(string id, string caption, float start, float end, bool flag)> _segments
            = new List<(string, string, float, float, bool)>();
        readonly StringBuilder _log = new StringBuilder();

        public void Begin(float t0)
        {
            _t0 = t0;
            Application.runInBackground = true;   // else the Editor stops rendering when unfocused
            StartCoroutine(Run());
        }

        float Now => Time.realtimeSinceStartup - _t0;

        IEnumerator Run()
        {
            Line("=== A4 demo " + DateTime.UtcNow.ToString("u") + " ===");
            yield return Boot();

            yield return Segment("a_play_pillar",
                "(a) PLAY pillar — push between same-background screens, then the new nav selected state",
                PlayPillar);

            yield return Segment("b_tournaments",
                "(b) Tournaments / Rankings — the three screens that share a backdrop",
                Tournaments);

            yield return Segment("c_gacha_pillar",
                "(c) GACHA pillar — Rewards Center to History and back",
                GachaPillar);

            yield return Segment("d_tabs_and_filters",
                "(d) Tabs cross-fade — Inventory x4, Rankings x4, the gacha log",
                TabsAndFilters);

            yield return Segment("e_settings",
                "(e) Settings — scrim fades, panel pops, two accordion rows open",
                Settings);

            // ── (f) the option Cesar judges, and the ONLY place the flag moves ──
            bool armed = false;
            try
            {
                LayeredPush.AllowBackgroundCrossFade = true;
                armed = true;
                Line("OPTION (b): AllowBackgroundCrossFade = true");
            }
            finally { if (!armed) LayeredPush.AllowBackgroundCrossFade = false; }

            yield return Segment("f_option_b",
                "OPTION (b) — push WITH a background cross-fade. FLAG OFF IN THE BUILD.",
                OptionB, flag: true);

            LayeredPush.AllowBackgroundCrossFade = false;
            Line("OPTION (b) restored: AllowBackgroundCrossFade = " + LayeredPush.AllowBackgroundCrossFade);

            WriteSidecar();
            Line("=== done — stop play mode to flush the recording ===");
            EditorApplication.ExitPlaymode();
        }

        IEnumerator Segment(string id, string caption, Func<IEnumerator> body, bool flag = false)
        {
            float start = Now;
            Line($"--- segment {id} @ {start:0.00}s ---");
            yield return body();
            yield return new WaitForSecondsRealtime(0.8f);   // a beat of the settled screen
            float end = Now;
            _segments.Add((id, caption, start, end, flag));
            Line($"--- segment {id} ends @ {end:0.00}s ({end - start:0.0}s) ---");
        }

        // ═════════════════════════════════════════════════════════════════════
        // The six routes
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator PlayPillar()
        {
            yield return NavSlot("NavTeeButton", ScreenId.ModeSelection);
            yield return ModeCard(ScreenId.HoleSelection);      // PUSH
            yield return Back(ScreenId.ModeSelection);          // PUSH (back)
            yield return ModeCard(ScreenId.MissionSelection);   // PUSH
            yield return Back(ScreenId.ModeSelection);          // PUSH (back)

            // the D7 selected state, cross-fading across all five slots
            yield return NavSlot("NavHomeButton",       ScreenId.Home);
            yield return NavSlot("NavGachaButton",      ScreenId.GeneralShop);
            yield return NavSlot("NavInventoryButton",  ScreenId.Inventory);
            yield return NavSlot("NavCharactersButton", ScreenId.Roster);
            yield return NavSlot("NavTeeButton",        ScreenId.ModeSelection);
        }

        IEnumerator Tournaments()
        {
            yield return Ensure(ScreenId.ModeSelection);
            yield return TapPath(ScreenId.ModeSelection, "TournamentTempEntry", ScreenId.TournamentSelection);
            yield return Show(ScreenId.TournamentLeaderboard);  // PUSH — no player path in this session
            yield return Show(ScreenId.Leaderboard);            // PUSH
            yield return GoBack(ScreenId.TournamentLeaderboard);
            yield return GoBack(ScreenId.TournamentSelection);
        }

        IEnumerator GachaPillar()
        {
            yield return Ensure(ScreenId.Home);
            yield return NavSlot("NavGachaButton", ScreenId.GeneralShop);
            yield return TapPath(ScreenId.GeneralShop, "HistoryChip", ScreenId.GachaHistory);   // PUSH
            yield return Show(ScreenId.GachaPrizes);                                            // PUSH
            yield return GoBack(ScreenId.GachaHistory);
            yield return GoBack(ScreenId.GeneralShop);
        }

        IEnumerator TabsAndFilters()
        {
            yield return Ensure(ScreenId.Home);
            yield return NavSlot("NavInventoryButton", ScreenId.Inventory);
            for (int t = 1; t < 4; t++) { yield return InventoryTab(t); }
            yield return InventoryTab(0);

            yield return Ensure(ScreenId.Leaderboard);
            foreach (string tab in new[] { "WeeklyTab", "MonthlyTab", "HistoryTab", "DailyTab" })
                yield return TapNamed(tab, 1.0f);
        }

        IEnumerator Settings()
        {
            Transform? gear = FindByName("SettingsButton");
            var b = gear != null ? gear.GetComponent<Button>() : null;
            if (b != null) { Line("tapping the real SettingsButton"); b.onClick.Invoke(); }
            else SettingsController.Instance?.OpenSettings();
            yield return new WaitForSecondsRealtime(1.2f);

            // two accordion rows, through their own row buttons
            int opened = 0;
            foreach (Button item in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (opened >= 2) break;
                if (item.GetComponentInParent<SettingsController>() == null) continue;
                if (item.GetComponent<SettingsMenuItem>() == null && item.GetComponentInParent<SettingsMenuItem>() == null) continue;
                Line("tapping settings row '" + item.name + "'");
                item.onClick.Invoke();
                opened++;
                yield return new WaitForSecondsRealtime(1.1f);
            }

            Transform? close = FindByName("CloseButton");
            var cb = close != null ? close.GetComponent<Button>() : null;
            if (cb != null) cb.onClick.Invoke(); else SettingsController.Instance?.CloseSettings();
            yield return new WaitForSecondsRealtime(1.2f);
        }

        IEnumerator OptionB()
        {
            yield return Ensure(ScreenId.ModeSelection);
            yield return TapPath(ScreenId.ModeSelection, "TournamentTempEntry", ScreenId.TournamentSelection);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return GoBack(ScreenId.ModeSelection);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Widgets
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator Boot()
        {
            yield return Until(() => ScreenManager.Instance != null, 30f);
            yield return TapStart();
            if (ScreenManager.Instance!.CurrentScreen != ScreenId.Home)
                ScreenManager.Instance.ShowScreen(ScreenId.Home);
            yield return Until(() => ScreenManager.Instance!.CurrentScreen == ScreenId.Home, 25f);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        IEnumerator NavSlot(string slot, ScreenId target)
        {
            Transform? t = FindByName(slot);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null) { Line("WARN no nav slot " + slot); yield break; }
            Line("tap nav " + slot + " -> " + target);
            b.onClick.Invoke();
            yield return Arrive(target, 1.3f);
        }

        IEnumerator TapPath(ScreenId on, string path, ScreenId target)
        {
            GameObject? go = Obj(on);
            var b = go != null ? go.transform.Find(path)?.GetComponent<Button>() : null;
            if (b == null) { Line("WARN no widget " + path); yield break; }
            Line("tap " + path + " -> " + target);
            b.onClick.Invoke();
            yield return Arrive(target, 1.3f);
        }

        /// <summary>
        /// The card is chosen by its CSV ROUTE, never by trying cards in turn: the 1v1 card's
        /// PLAY opens matchmaking and starts a REAL HOLE, which unloads ShellScene underneath the
        /// take. See GamePolishProbe.ModeCardPlay for the full scar.
        /// </summary>
        IEnumerator ModeCard(ScreenId target)
        {
            string want = target == ScreenId.HoleSelection
                ? GolfinRedux.UI.ModeSelect.ModeSelectScreenController.TargetHoleSelect
                : GolfinRedux.UI.ModeSelect.ModeSelectScreenController.TargetMissionSelect;

            GameObject? go = Obj(ScreenId.ModeSelection);
            var sr = go != null ? go.GetComponentInChildren<ScrollRect>(true) : null;
            Transform? content = sr != null ? sr.content : null;
            if (content == null) { Line("WARN no mode cards"); yield break; }

            Transform? chosen = null;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform card = content.GetChild(i);
                if (!card.gameObject.activeInHierarchy) continue;
                var ctrl = card.GetComponent<GolfinRedux.UI.ModeSelect.ModeCardController>();
                if (ctrl == null || string.IsNullOrEmpty(ctrl.ModeId)) continue;
                var db = GolfinRedux.UI.ModeSelect.ModesDatabaseCSV.Instance;
                var mode = db != null ? db.GetMode(ctrl.ModeId) : null;
                if (mode != null && mode.target == want) { chosen = card; break; }
            }
            if (chosen == null) { Line("WARN no card routes to '" + want + "'"); yield break; }

            // Expand only if it is not already expanded — HandleCardTapped toggles, so an
            // unconditional tap CLOSES the card we just chose (the practice card opens by default).
            var chosenCtrl = chosen.GetComponent<GolfinRedux.UI.ModeSelect.ModeCardController>();
            if (chosenCtrl != null &&
                chosenCtrl.State != GolfinRedux.UI.ModeSelect.ModeCardState.Expanded)
            {
                var tap = chosen.Find("CardTapButton")?.GetComponent<Button>() ?? chosen.GetComponent<Button>();
                if (tap != null && tap.interactable) tap.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.9f);
            }

            var play = chosen.Find("ExpandedContainer/ActionButton")?.GetComponent<Button>();
            if (play == null || !play.gameObject.activeInHierarchy || !play.interactable)
            { Line("WARN chosen card has no active ActionButton"); yield break; }

            Line("tap mode card '" + chosen.name + "' PLAY -> " + target);
            play.onClick.Invoke();
            yield return Arrive(target, 1.3f);
        }

        IEnumerator InventoryTab(int index)
        {
            GameObject? inv = Obj(ScreenId.Inventory);
            if (inv == null) yield break;
            foreach (Button b in inv.GetComponentsInChildren<Button>(true))
            {
                if (b.transform.parent == null || b.transform.parent.name != "TabBar") continue;
                if (b.transform.GetSiblingIndex() != index || !b.gameObject.activeInHierarchy) continue;
                Line("tap inventory tab '" + b.name + "'");
                b.onClick.Invoke();
                break;
            }
            yield return new WaitForSecondsRealtime(1.0f);
        }

        IEnumerator TapNamed(string name, float settle)
        {
            Transform? t = FindByName(name);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b != null) { Line("tap '" + name + "'"); b.onClick.Invoke(); }
            else Line("WARN no widget named " + name);
            yield return new WaitForSecondsRealtime(settle);
        }

        IEnumerator Back(ScreenId target)
        {
            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                if (b.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Line("tap real '" + b.name + "' -> " + target);
                b.onClick.Invoke();
                yield return Arrive(target, 1.3f);
                yield break;
            }
            yield return GoBack(target);
        }

        IEnumerator GoBack(ScreenId target)
        {
            Line("GoBack -> " + target + "  (harness; the BACK push, not a tap)");
            ScreenManager.Instance?.GoBack(target);
            yield return Arrive(target, 1.3f);
        }

        IEnumerator Show(ScreenId target)
        {
            Line("ShowScreen -> " + target + "  (harness; no player path from here)");
            ScreenManager.Instance?.ShowScreen(target);
            yield return Arrive(target, 1.3f);
        }

        IEnumerator Ensure(ScreenId id)
        {
            if (ScreenManager.Instance!.CurrentScreen == id) yield break;
            ScreenManager.Instance.ShowScreen(id);
            yield return Arrive(id, 1.0f);
        }

        IEnumerator Arrive(ScreenId id, float settle)
        {
            yield return Until(() => ScreenManager.Instance!.CurrentScreen == id, 20f);
            yield return new WaitForSecondsRealtime(settle);
        }

        IEnumerator TapStart()
        {
            float deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline)
            {
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (b.name != "StartButton" || !b.gameObject.activeInHierarchy) continue;
                    Line("tap the real StartButton");
                    b.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(2f);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        IEnumerator Until(Func<bool> done, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Transform? FindByName(string name)
        {
            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b.name == name && b.gameObject.activeInHierarchy) return b.transform;
            return null;
        }

        static GameObject? Obj(ScreenId id)
        {
            string? n = id switch
            {
                ScreenId.Home                  => "HomeScreen",
                ScreenId.Roster                => "RosterScreen",
                ScreenId.Inventory             => "InventoryScreen",
                ScreenId.HoleSelection         => "HoleSelectionScreen",
                ScreenId.ModeSelection         => "ModeSelectionScreen",
                ScreenId.MissionSelection      => "MissionSelectionScreen",
                ScreenId.Leaderboard           => "RankingsScreen",
                ScreenId.TournamentSelection   => "TournamentSelectionScreen",
                ScreenId.TournamentLeaderboard => "TournamentLeaderboardScreen",
                ScreenId.GeneralShop           => "GeneralShopScreen",
                ScreenId.GachaHistory          => "GachaHistoryScreen",
                ScreenId.GachaPrizes           => "GachaPrizesScreen",
                _                              => null,
            };
            return n == null ? null : GameObject.Find("Canvas/ScreensRoot/" + n);
        }

        void WriteSidecar()
        {
            var j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine("  \"raw\": \"" + OutputDir + "/raw.mp4\",");
            j.AppendLine("  \"fps\": 30,");
            j.AppendLine("  \"segments\": [");
            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                j.AppendLine("    {\"id\": \"" + s.id + "\", \"caption\": \"" + s.caption.Replace("\"", "'") + "\", " +
                             "\"start\": " + s.start.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
                             "\"end\": " + s.end.ToString("0.###", CultureInfo.InvariantCulture) + ", " +
                             "\"allowBackgroundCrossFade\": " + (s.flag ? "true" : "false") + "}" +
                             (i < _segments.Count - 1 ? "," : ""));
            }
            j.AppendLine("  ]");
            j.AppendLine("}");
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(OutputDir + "/segments.json", j.ToString());
            Line("sidecar -> " + OutputDir + "/segments.json");
        }

        void Line(string s)
        {
            _log.AppendLine($"[{Now:0.00}] {s}");
            Debug.Log("[GamePolishDemo] " + s);
            File.WriteAllText("Docs/Diagnostics/_capture/game_polish_a_demo.log", _log.ToString());
        }
    }
}
