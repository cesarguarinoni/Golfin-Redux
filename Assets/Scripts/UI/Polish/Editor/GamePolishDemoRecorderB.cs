// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §A4 — the seven clips.
//
// GamePolishDemoRecorder's shape verbatim, pointed at this task: arm -> play mode ->
// one RecorderController at the pinned iPhone-14 resolution -> a runner drives real
// widgets -> StopRecording on exit, with a sidecar of segment boundaries on the SAME
// clock that cut_game_polish_clips.py slices and captions.
//
// NO STILLS ARE TAKEN WHILE IT RECORDS. The y-flip has two triggers — a render-state
// change after StartRecording, and ANY ScreenCapture / RenderTexture read while the
// recorder runs — so the per-clip stills are extracted from the MP4 afterwards
// (memory: botvideorecorder_yflip_fix, video_flip_verification).
//
// SEGMENTS ARE OPT-IN, and that is inherited scar tissue rather than caution: a's
// single take of all six wedged twice, because one segment waiting on a server took
// the whole recording down with it and raw.mp4 stayed at 0 bytes. A failure should
// cost one run, not every segment that had already recorded.
//
// WHAT THESE SEVEN CAN HONESTLY SHOW. (a) (c) (g) are pure shell navigation and run
// on local or cached data. (b) (d) (f) each need a live server AND a balance to
// spend. (e) really loads a hole: an earlier note here claimed a hole-complete
// "unloads ShellScene under the take", and that was wrong twice — GameplaySceneLoader
// loads LabScaffold and Hole_NN_Geo ADDITIVELY (ShellScene is never unloaded, and
// hosts the loader itself), and this runner's host is DontDestroyOnLoad regardless.
// Each segment logs whether it reached its subject or bailed, and the sidecar records
// that verdict — a clip that did not get there must not ship captioned as though it did.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.UI.GameplayTransition;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using GolfinRedux.UI.HoleSelection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishDemoRecorderB
    {
        const string OutputDir = "Docs/Specs/Active/game_polish_b/videos";
        const string ArmedKey  = "GamePolishDemoRecorderB.Armed";
        public const string SegmentsKey = "GamePolishDemoRecorderB.Segments";
        static RecorderController? _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Game Polish/Record B — A4 clips (all seven)", priority = 290)]
        public static void LaunchAll() => Launch("all");

        [MenuItem("GOLFIN/Game Polish/Record B — A4 clips (a, c, g: no server needed)", priority = 291)]
        public static void LaunchLocal() => Launch("acg");

        [MenuItem("GOLFIN/Game Polish/Record B — A4 clips (b, e: the two that were missing)", priority = 292)]
        public static void LaunchBE() => Launch("be");

        public static void Launch(string segments)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[DemoB] Already playing — stop first."); return; }
            EditorPrefs.SetString(SegmentsKey, segments);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log($"[DemoB] Armed ({segments}). Entering play mode...");
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
                    Debug.LogWarning($"[DemoB] Could not pin iPhone-14 — recording at {w}x{h}. " +
                                     "A4 wants full size (memory: record_bot_video_full_size).");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "GamePolishDemoB";
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
            Debug.Log($"[DemoB] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[GamePolishDemoBotB]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GamePolishDemoRunnerB>().Begin(Time.realtimeSinceStartup);
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[DemoB] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[DemoB] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    /// <summary>Drives the seven §A4 segments through real widgets and writes the cut sidecar.</summary>
    public class GamePolishDemoRunnerB : MonoBehaviour
    {
        const string OutputDir = "Docs/Specs/Active/game_polish_b/videos";
        const string SegmentsKey = GamePolishDemoRecorderB.SegmentsKey;

        float _t0;
        readonly List<(string id, string caption, float start, float end, bool reached, string note)> _segments
            = new List<(string, string, float, float, bool, string)>();
        readonly StringBuilder _log = new StringBuilder();

        /// <summary>Set false by a segment that could not reach its subject. The sidecar carries
        /// it so a clip that never got there cannot ship captioned as though it did.</summary>
        bool _reached;
        string _note = "";

        public void Begin(float t0)
        {
            _t0 = t0;
            Application.runInBackground = true;   // else the Editor stops rendering when unfocused
            StartCoroutine(Run());
        }

        float Now => Time.realtimeSinceStartup - _t0;

        IEnumerator Run()
        {
            Line("=== game_polish_b A4 demo " + DateTime.UtcNow.ToString("u") + " ===");
            yield return Boot();

            string only = EditorPrefs.GetString(SegmentsKey, "all");
            bool Want(string id) => only == "all" || only.Contains(id);

            if (Want("a"))
                yield return Segment("a_roster_levelup",
                    "Roster level-up: the modal pops, the stat bars fill, the numbers count",
                    RosterLevelUp);

            if (Want("b"))
                yield return Segment("b_shop_purchase",
                    "Shop: a real buy on the STORE tab - the top bar counts DOWN, 6,123 to 6,048",
                    ShopPurchase);

            if (Want("c"))
                yield return Segment("c_rankings_cold",
                    "Rankings: the board arrives - podium reveals 3 to 1, then the rows stagger",
                    RankingsCold);

            if (Want("d"))
                yield return Segment("d_gacha_reveal",
                    "Gacha reveal after the retrofit: the same bag drop, shake and card pop",
                    GachaReveal);

            if (Want("e"))
                yield return Segment("e_hole_complete",
                    "Hole complete: the cards pop in and the rewards count up",
                    HoleComplete);

            if (Want("f"))
                yield return Segment("f_tournament",
                    "Tournament signup modal pops, and the result modal counts its prize",
                    Tournament);

            if (Want("g"))
                yield return Segment("g_mode_select",
                    "Mode Select: the cards stagger in on every entry, and a tap bumps the card",
                    ModeSelect);

            Finish();
        }

        void Finish()
        {
            WriteSidecar();
            Line("=== done — stopping play mode to flush the recording ===");
            EditorApplication.ExitPlaymode();
        }

        IEnumerator Segment(string id, string caption, Func<IEnumerator> body)
        {
            float start = Now;
            _reached = true; _note = "";
            Line($"--- segment {id} @ {start:0.00}s ---");
            yield return body();
            yield return new WaitForSecondsRealtime(0.9f);   // a beat of the settled screen
            float end = Now;
            _segments.Add((id, caption, start, end, _reached, _note));
            Line($"--- segment {id} ends @ {end:0.00}s ({end - start:0.0}s) reached={_reached}" +
                 (_note.Length > 0 ? "  note=" + _note : "") + " ---");
        }

        void Bail(string why)
        {
            _reached = false;
            _note = why;
            Line("BAIL: " + why);
        }

        // ═════════════════════════════════════════════════════════════════════
        // The seven routes
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>(a) Roster → a character → LEVEL UP → allocate → CONFIRM.</summary>
        IEnumerator RosterLevelUp()
        {
            yield return NavSlot("NavCharactersButton", ScreenId.Roster);
            if (ScreenManager.Instance!.CurrentScreen != ScreenId.Roster)
            {
                yield return Show(ScreenId.Roster);
                if (ScreenManager.Instance!.CurrentScreen != ScreenId.Roster) { Bail("could not reach Roster"); yield break; }
            }
            yield return new WaitForSecondsRealtime(1.2f);

            if (!TapFirstNamed("LevelUpButton")) { Bail("no LevelUpButton on Roster"); yield break; }
            yield return new WaitForSecondsRealtime(1.4f);      // the modal pops

            // SCOPED to the modal. There is a PlusButton on each of its four stat rows AND a
            // ShopPlusButton elsewhere in the shell; the first take tapped the latter.
            Transform? modal = OpenModal("LevelUp");
            if (modal == null) { Bail("the level-up modal did not open"); yield break; }

            // THE MODAL HAS ITS OWN "LEVEL UP", and that is the button that spends RP and grants
            // the SP the PlusButtons then allocate. The first take skipped it, so no SP was
            // pending, every PlusButton was correctly non-interactable and CONFIRM stayed dark —
            // a failure that had nothing to do with the balance the bail message blamed.
            for (int lv = 0; lv < 2; lv++)
            {
                if (!TapWithin(modal, "LevelUpButton")) break;
                yield return new WaitForSecondsRealtime(1.1f);   // the level Pop + bar tweens
            }

            for (int i = 0; i < 3; i++)
            {
                if (!TapWithin(modal, "PlusButton")) break;
                yield return new WaitForSecondsRealtime(0.45f);
            }
            yield return new WaitForSecondsRealtime(0.8f);

            if (!TapWithin(modal, "ConfirmButton"))
            {
                Bail($"CONFIRM not interactable (RP {Rp()}) — the modal pop, the level Pop and the " +
                     "stat-bar tweens are still on the clip");
                yield return new WaitForSecondsRealtime(1.5f);
                TapWithin(modal, "CancelButton");
                yield break;
            }
            yield return new WaitForSecondsRealtime(2.5f);      // count-ups + the RP top bar
        }

        /// <summary>
        /// (b) Shop → the rotation tabs → BUY → the `…` wait → RP counts down → Inventory.
        ///
        /// <para>The first take bailed with "no interactable shop CTA at RP 6,139 — nothing on the
        /// catalog is affordable or already owned". That was a claim about a balance for a control
        /// that was not on screen: the catalog is TABBED and `CtaGoldButton` only exists on the
        /// cards the showing tab built, so one probe of one tab cannot support a sentence about
        /// the whole catalog. It walks all three now and reports what each tab actually held.</para>
        /// </summary>
        IEnumerator ShopPurchase()
        {
            yield return NavSlot("NavGachaButton", ScreenId.GeneralShop);
            if (ScreenManager.Instance!.CurrentScreen != ScreenId.GeneralShop)
                yield return Show(ScreenId.GeneralShop);
            if (ScreenManager.Instance!.CurrentScreen != ScreenId.GeneralShop) { Bail("could not reach the shop"); yield break; }
            yield return new WaitForSecondsRealtime(3.2f);      // catalog paint + the card stagger

            string before = Rp();
            Button? buy = null;
            string where = "";
            var tally = new StringBuilder();

            // CtaGoldButton is what BOTH shop card prefabs call it (Club and Ball).
            foreach (string tab in new[] { "DailyTab", "WeeklyTab", "MonthlyTab" })
            {
                TapFirstNamed(tab);                            // already-selected tab: harmless
                yield return new WaitForSecondsRealtime(2.5f); // the tab paints its cards deferred

                int total = 0, live = 0;
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (b == null || !string.Equals(b.name, "CtaGoldButton", StringComparison.Ordinal)) continue;
                    if (!b.gameObject.activeInHierarchy) continue;
                    total++;
                    if (!b.interactable) continue;
                    live++;
                    if (buy == null) { buy = b; where = tab; }
                }
                Line($"  {tab}: {total} BUY button(s) on screen, {live} live");
                tally.Append(tab).Append(' ').Append(live).Append('/').Append(total).Append("  ");
                if (buy != null) break;
            }

            if (buy == null)
            {
                Bail($"no LIVE BUY on any of the three shop tabs at RP {before} ({tally.ToString().Trim()}) — " +
                     "every card on offer is owned or unaffordable. The catalog stagger and the tab " +
                     "switches are still on the clip");
                yield break;
            }

            Line($"tap real 'CtaGoldButton' on {where}  (RP before {before})");
            buy.onClick.Invoke();
            yield return new WaitForSecondsRealtime(4.0f);     // the `…` round trip + the count-down
            Line($"RP after {Rp()}  (was {before})");

            yield return NavSlot("NavInventoryButton", ScreenId.Inventory);
            yield return new WaitForSecondsRealtime(1.2f);
        }

        /// <summary>(c) Rankings — shimmer if cold, podium 3→2→1, rows stagger.</summary>
        IEnumerator RankingsCold()
        {
            yield return Ensure(ScreenId.Home);
            yield return new WaitForSecondsRealtime(0.6f);
            if (!TapFirstNamed("LeaderboardButton"))
                yield return Show(ScreenId.Leaderboard);
            yield return Arrive(ScreenId.Leaderboard, 3.0f);    // the reveal and the stagger
            if (ScreenManager.Instance!.CurrentScreen != ScreenId.Leaderboard) Bail("could not reach Rankings");
        }

        /// <summary>(d) The gacha reveal, after the §D2 retrofit.</summary>
        IEnumerator GachaReveal()
        {
            yield return NavSlot("NavGachaButton", ScreenId.GeneralShop);
            if (ScreenManager.Instance!.CurrentScreen != ScreenId.GeneralShop)
                yield return Show(ScreenId.GeneralShop);
            yield return new WaitForSecondsRealtime(1.5f);

            if (!TapFirstNamed("PullX10Button", "PullX1Button"))
            {
                Bail("no interactable PULL — a pull needs tickets this session does not have. " +
                     "The banner carousel and the shop chrome are still on the clip");
                yield break;
            }
            yield return new WaitForSecondsRealtime(9f);        // bag drop, shake, card pop
        }

        /// <summary>
        /// (e) Hole complete — the result screen §D1.3 gave its own pop.
        ///
        /// <para>THE HOLE IS REALLY LOADED, through the hole card's own action button, and the
        /// result screen is raised by <c>GameSession.MarkHoleComplete</c> — the production
        /// hole-end call, the one <c>HoleCompletionBridge</c> makes when the ball drops. What is
        /// synthesised is the ball reaching the cup, and nothing after it: the payload, the modal
        /// controller, the data assembly, the widget, its pop and its count-ups are all the
        /// production path. `SkyRotationDemoRecorder`'s ACT 2 is the same recipe and is where this
        /// is borrowed from rather than invented.</para>
        ///
        /// <para>The previous bail said this "unloads ShellScene under the take". It does not:
        /// both gameplay scenes load ADDITIVELY and `GameplaySceneLoader` itself lives in
        /// ShellScene — see the file header.</para>
        /// </summary>
        IEnumerator HoleComplete()
        {
            yield return Show(ScreenId.HoleSelection);
            yield return new WaitForSecondsRealtime(2.2f);      // the cards build deferred

            int hole = 0;
            yield return EnterAHole(n => hole = n);
            if (hole <= 0)
            {
                // A fallback into BeginGameplayLoad would put a hole on screen while the caption
                // claimed the player's own entry point. Bail instead.
                Bail("no hole card reached its PLAY button — the hole was never entered through " +
                     "the player's path, so nothing was recorded to caption");
                yield break;
            }

            yield return WaitForHoleGeo(hole);
            yield return WaitForGameplayVisible();
            // SIX seconds on the tee, not two. The recorder runs at FrameRatePlayback.Variable —
            // it captures the frames the app actually renders — and in the seconds right after a
            // hole loads the Editor is still warming shaders and settling, so it renders at
            // roughly a third of speed. Fired at 2 s, the 0.20 s pop reached the clip as TWO
            // distinct frames with the rest duplicated: the motion was real and the clip barely
            // showed it. Waiting for the scene to settle costs six seconds of take and is the
            // difference between a clip that demonstrates the pop and one that asserts it.
            yield return new WaitForSecondsRealtime(6.0f);      // let the hole settle before firing

            // Seed the session the way the production hole-selection path does — without it the
            // result screen reads "Hole 0" and its NEXT HOLE card computes 0+1.
            GameSession.SetCurrentHole(hole);

            GameSession.MarkHoleComplete(
                new HoleCompletionData(BallState.InCup, 3, 0, GameSession.CurrentHoleNumber));
            Line("MarkHoleComplete(InCup, 3 strokes) — the production hole-end call");
            yield return new WaitForSecondsRealtime(6.0f);      // the pop, both cards, the count-ups

            // Look, do not assume: a clip captioned "the cards pop in" must not ship over a frame
            // that still shows the tee.
            HoleCompleteWidget? w = Resources.FindObjectsOfTypeAll<HoleCompleteWidget>()
                .FirstOrDefault(x => x != null && !string.IsNullOrEmpty(x.gameObject.scene.name));
            if (w == null || !w.IsShowing)
            {
                Bail("the result screen never came up after MarkHoleComplete " +
                     (w == null ? "(no HoleCompleteWidget in a loaded scene)" : "(IsShowing false)"));
                yield break;
            }
            Line("result screen is showing — HoleCompleteWidget.IsShowing true");
            yield return new WaitForSecondsRealtime(2.0f);

            // Leave through the production MENU/quit path, so the take ends on the shell and the
            // Editor is not left sitting on a loaded hole.
            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                Line("ExitToScreen(Home) — the production teardown");
                yield return loader.ExitToScreen(ScreenId.Home);
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        /// <summary>
        /// Enter a hole the way a player does: tap a card, which EXPANDS it, and then tap the
        /// PLAY button that expanding revealed.
        ///
        /// <para>The first version polled hole 1's action button directly and gave up after 25 s
        /// with "the card action button never went live". It never would: `actionButton` lives
        /// inside `expandedContainer`, which `SetState` only activates for
        /// <c>HoleCardState.Expanded</c>, so on a collapsed card it is not in the hierarchy at
        /// all — eighteen collapsed cards are indistinguishable from a screen that never finished
        /// building. It also stopped pinning hole 1: which hole is playable is save state, and
        /// the clip's subject is the result screen, not a particular hole.</para>
        /// </summary>
        IEnumerator EnterAHole(Action<int> hole, float timeout = 30f)
        {
            FieldInfo? actionFi = typeof(HoleCardController)
                .GetField("actionButton", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo? tapFi = typeof(HoleCardController)
                .GetField("cardTapButton", BindingFlags.NonPublic | BindingFlags.Instance);

            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                HoleCardController[] cards = FindObjectsByType<HoleCardController>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                // Prefer one the screen already expanded; otherwise expand the first unlocked one.
                HoleCardController? card = null;
                foreach (var c in cards)
                    if (c != null && c.State == HoleCardState.Expanded) { card = c; break; }
                if (card == null)
                    foreach (var c in cards)
                        if (c != null && c.State != HoleCardState.Locked) { card = c; break; }

                if (card != null)
                {
                    if (card.State != HoleCardState.Expanded)
                    {
                        var tap = tapFi?.GetValue(card) as Button;
                        if (tap != null && tap.gameObject.activeInHierarchy && tap.interactable)
                        {
                            Line($"tap real hole card #{card.HoleNumber} — expands it");
                            tap.onClick.Invoke();
                            yield return new WaitForSecondsRealtime(1.4f);   // the expand animates
                        }
                    }

                    var play = actionFi?.GetValue(card) as Button;
                    if (play != null && play.gameObject.activeInHierarchy)
                    {
                        Line($"tap real hole card #{card.HoleNumber} PLAY ({card.Mode})");
                        play.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1.5f);
                        hole(card.HoleNumber);
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }

            var final = FindObjectsByType<HoleCardController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int locked = 0, expanded = 0;
            foreach (var c in final) { if (c == null) continue; if (c.State == HoleCardState.Locked) locked++; if (c.State == HoleCardState.Expanded) expanded++; }
            Line($"no card reached PLAY in {timeout:0}s — {final.Length} cards, {locked} locked, {expanded} expanded");
            hole(0);
        }

        /// <summary>
        /// Wait for a SPECIFIC hole scene. Matching any `Hole_NN_Geo` returns instantly on one
        /// already loaded and lets the sequence race ahead of the transition.
        /// </summary>
        IEnumerator WaitForHoleGeo(int holeNumber, float timeout = 90f)
        {
            string want = $"Hole_{holeNumber:D2}_Geo";
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene sc = SceneManager.GetSceneAt(i);
                    if (sc.isLoaded && sc.name == want)
                    {
                        Line($"'{want}' loaded");
                        yield return new WaitForSecondsRealtime(1f);
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Line($"WaitForHoleGeo({want}) TIMEOUT");
        }

        /// <summary>
        /// Wait for the loading screen to appear and THEN go. Asking once whether a loading
        /// screen is inactive returns true on the first frame and every beat after it elapses
        /// behind the curtain.
        /// </summary>
        IEnumerator WaitForGameplayVisible(float timeout = 40f)
        {
            float up = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < up && ActiveLoadingScreens() == 0)
                yield return new WaitForSecondsRealtime(0.25f);

            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (ActiveLoadingScreens() == 0)
                {
                    Line("gameplay visible — loading screen down");
                    yield return new WaitForSecondsRealtime(0.75f);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Line("loading screen never hid");
        }

        static int ActiveLoadingScreens()
            => FindObjectsByType<LoadingScreenController>(
                   FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

        /// <summary>(f) Tournament signup modal, and the result modal if one has resolved.</summary>
        IEnumerator Tournament()
        {
            yield return Ensure(ScreenId.ModeSelection);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Show(ScreenId.TournamentSelection);
            yield return new WaitForSecondsRealtime(2.5f);      // schedule paint + card stagger

            // TournamentSelectionCard carries BOTH: gold for the primary action, silver for the
            // secondary. Whichever is live on the first card is the one a player would tap.
            if (!TapFirstNamed("CtaGoldButton", "CtaSilverButton"))
            {
                Bail("no interactable tournament CTA — the schedule may be empty or every " +
                     "tournament closed. The card stagger is still on the clip");
                yield break;
            }
            yield return new WaitForSecondsRealtime(2.8f);      // the signup modal pops
            Transform? signup = OpenModal("");
            if (signup != null) TapWithin(signup, "CancelButton", "CloseButton", "TournamentCloseButton");
            else                TapFirstNamed("TournamentCloseButton", "CancelButton");
            yield return new WaitForSecondsRealtime(1.2f);
        }

        /// <summary>(g) Mode Select — the front-door stagger, then a card tap bump.</summary>
        IEnumerator ModeSelect()
        {
            yield return Ensure(ScreenId.Home);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return NavSlot("NavTeeButton", ScreenId.ModeSelection);
            yield return new WaitForSecondsRealtime(2.0f);      // the stagger

            // Leave and come back: the front-door exception is that it staggers on EVERY entry,
            // and one arrival cannot show "every". Home is reached with ShowScreen rather than the
            // nav slot — the first take spent 95 s on that one tap waiting for Home's own fetches,
            // which is 95 s of nothing in the middle of the clip.
            yield return Show(ScreenId.Home);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return NavSlot("NavTeeButton", ScreenId.ModeSelection);
            yield return new WaitForSecondsRealtime(2.0f);      // and it staggers again

            TapFirstNamed("CardTapButton");                     // the §D6 selection bump
            yield return new WaitForSecondsRealtime(1.5f);
        }

        // ═════════════════════════════════════════════════════════════════════
        // plumbing — the a-recorder's, unchanged in behaviour
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

        IEnumerator NavSlot(string slot, ScreenId target)
        {
            Transform? t = FindByName(slot);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null) { Line("no nav slot " + slot); yield break; }
            Line("tap nav " + slot + " -> " + target);
            b.onClick.Invoke();
            yield return Arrive(target, 1.3f);
        }

        /// <summary>
        /// Tap the first ACTIVE, INTERACTABLE button whose name matches EXACTLY, searching the
        /// whole scene. Exact, not substring: the first take asked for "Plus" and hit
        /// ShopPlusButton on another screen, so no stat point was ever allocated and the level-up
        /// modal's CONFIRM stayed correctly disabled — a segment that failed for a reason that had
        /// nothing to do with the modal.
        /// </summary>
        bool TapFirstNamed(params string[] names) => TapWithin(null, names);

        /// <summary>
        /// The same, SCOPED to a subtree. Every control a segment wants lives under a known root
        /// (a modal, a card, a screen), and scoping is what stops a name collision on some other
        /// screen from being tapped instead.
        /// </summary>
        bool TapWithin(Transform? root, params string[] names)
        {
            Button[] pool = root != null
                ? root.GetComponentsInChildren<Button>(true)
                : FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (string want in names)
                foreach (Button b in pool)
                {
                    if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
                    if (!string.Equals(b.name, want, StringComparison.Ordinal)) continue;
                    Line("tap real '" + b.name + "'" + (root != null ? " under " + root.name : ""));
                    b.onClick.Invoke();
                    return true;
                }
            return false;
        }

        /// <summary>The live RP balance, for bail messages that would otherwise be assertions.</summary>
        static string Rp()
        {
            var m = Golfin.Roster.RewardPointsManager.Instance;
            return m == null ? "unknown" : m.GetPoints().ToString();
        }

        /// <summary>The open modal's panel, so a tap can be scoped to it.</summary>
        Transform? OpenModal(string name)
        {
            foreach (ModalController m in FindObjectsByType<ModalController>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (m == null || !m.IsVisible()) continue;
                if (name.Length > 0 && m.gameObject.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
                return m.transform;
            }
            return null;
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
                             "\"reached\": " + (s.reached ? "true" : "false") + ", " +
                             "\"note\": \"" + s.note.Replace("\"", "'") + "\"}" +
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
            Debug.Log("[DemoB] " + s);
            Directory.CreateDirectory("Docs/Diagnostics/_capture");
            File.WriteAllText("Docs/Diagnostics/_capture/game_polish_b_demo.log", _log.ToString());
        }
    }
}
