// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D5 — the gate.
//
// The GPS surface's GpsPolishProbe, pointed at the GAME shell. Same argument:
// a push is fifteen frames long, and "it looked right" is not a measurement of a
// fifteen-frame event. This probe drives the shell through the REAL widgets a
// player taps, samples the two content rects and the chrome CanvasGroups on
// EVERY frame of every push, and writes a per-assertion PASS/FAIL file.
// fail == 0 with LayeredPush.AllowBackgroundCrossFade OFF is the gate (A1).
//
// REAL NAVIGATION (PIPELINE_HARDENING rule 2). Boot -> tap the real StartButton
// -> Home -> the real bottom-nav slots -> the real mode cards' PLAY -> the real
// LeaderboardButton / HistoryChip. Where a screen genuinely has no player path
// that this harness can reach (GachaPrizes is only reached by completing a gacha
// PULL, which spends currency), the probe says so IN THE LOG and in the JSON
// record rather than calling ShowScreen and letting it read as a tap.
//
// MODES
//   baseline  motion OFF, pre-change captures of every shell screen. Taken on
//             the FIRST commit, before any runtime change — the A2 comparison
//             has to be against HEAD, not against a "before" that already moved.
//   push      motion ON; drives the direction table and writes the invariants.
//   parity    A2 — the route walked TWICE in one session, animated then instant.
//   perf      A13 — profiler on, NO captures (the gps_polish A13 lesson: a
//             1170x2532 ReadPixels allocates ~100 MB and swamps the push it
//             lands beside; and turning the profiler on inside the measured run
//             stretched a 0.25 s tween to 0.41 s).
//   option_b  D4 — the ONE run with AllowBackgroundCrossFade true, for Cesar's
//             five-second video. Turned on, recorded, turned off.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishProbe
    {
        const string ArmedKey = "game_polish_a.probe.armed";
        const string ModeKey  = "game_polish_a.probe.mode";

        const string TaskDir  = "Docs/Specs/Active/game_polish_a";
        const string ShotDir  = TaskDir + "/screenshots";
        const string LogPath  = "Docs/Diagnostics/_capture/game_polish_a_run.log";
        const string JsonPath = "Docs/Diagnostics/_capture/game_polish_a_invariants.json";

        /// <summary>A1: "within ±2 frames of PushDur". Two frames at 60 fps plus slack for an
        /// Editor hitch — the same budget gps_polish settled on.</summary>
        const float DurationToleranceSec = 2f / 60f + 0.02f;

        [MenuItem("GOLFIN/Game Polish/Probe — baseline (motion off, pre-change)", priority = 260)]
        public static void ArmBaseline() => Arm("baseline");

        [MenuItem("GOLFIN/Game Polish/Probe — push (motion on, writes invariants)", priority = 261)]
        public static void ArmPush() => Arm("push");

        [MenuItem("GOLFIN/Game Polish/Probe — parity (A2: animated vs instant, one run)", priority = 262)]
        public static void ArmParity() => Arm("parity");

        [MenuItem("GOLFIN/Game Polish/Probe — perf (A13: GC + frame ms, no captures)", priority = 263)]
        public static void ArmPerf() => Arm("perf");

        [MenuItem("GOLFIN/Game Polish/Probe — option (b) (D4: flag ON, for the video)", priority = 264)]
        public static void ArmOptionB() => Arm("option_b");

        public static void Arm(string mode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(ShotDir);
            File.WriteAllText(LogPath, "");
            EditorPrefs.SetString(ModeKey, mode);
            EditorPrefs.SetBool(ArmedKey, true);

            if (EditorApplication.isPlaying) { Spawn(); return; }

            // Enter play mode only when the Editor is IDLE and the scene is really back.
            // gps_polish's scar: EnterPlaymode() straight after an AssetDatabase refresh starts a
            // session on a HALF-RESTORED scene — ScreenManager's GameObject present, its Awake
            // never run — and the probe times out on a boot that never happened.
            EditorApplication.update -= EnterWhenIdle;
            EditorApplication.update += EnterWhenIdle;
        }

        static void EnterWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            UnityEngine.SceneManagement.Scene sc =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!sc.IsValid() || !sc.isLoaded || sc.rootCount < 20) return;

            EditorApplication.update -= EnterWhenIdle;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ArmedKey, false)) Spawn();
        };

        static void Spawn()
        {
            EditorPrefs.SetBool(ArmedKey, false);
            var go = new GameObject("__GamePolishProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        // ═════════════════════════════════════════════════════════════════════

        /// <summary>One measured push, with every assertion §D5 names.</summary>
        sealed class Record
        {
            public string From = "", To = "", Direction = "", Widget = "";
            /// <summary>Whether this pair was reached by a player's own widget (A10) or driven by
            /// the harness because the game has no path to it from here. The invariants are a
            /// property of the MECHANISM and worth measuring either way; what must never happen is
            /// a harness-driven pair being reported as a tap.</summary>
            public bool RealWidget;
            public float  W, ExpectedDur, MeasuredDur;
            public float  TargetOffsetAtT0, EndTargetX, EndTargetRestX, EndLeaverX, EndLeaverRestX;
            public float  ChromeAlphaMinOverRun = 1f;      // same-background path: must stay 1
            public float  SeamWorstCover = 1f;             // option_b: max(fromChrome, toChrome) per frame
            public float  EndTargetContentAlpha = 1f, EndLeaverContentAlpha = 1f;
            public bool   EndBlocksRaycasts;
            public bool   Completed;
            public int    Frames;
            public int    ApplyScreenCalls;                // the ScreenChanged event count
            public readonly List<string> Fails = new List<string>();
        }

        public sealed class Driver : MonoBehaviour
        {
            readonly StringBuilder _log     = new StringBuilder();
            readonly List<Record>  _records = new List<Record>();
            string _mode = "baseline";
            string _shotPrefix = "baseline";
            int _shot;

            // A13 counters — sampled ONLY inside the push loop.
            Unity.Profiling.ProfilerRecorder _gcRec, _frameRec;
            bool _perf;
            readonly List<(string pair, long allocBytes, double worstMs, int frames)> _perfRows
                = new List<(string, long, double, int)>();

            // ApplyScreen ran exactly once, at the END (§D5). Counted from the real event.
            int _screenChanged;

            void Start()
            {
                // Without this the Editor stops rendering the moment it loses focus and every
                // capture comes back as whatever it drew last — the splash, usually.
                Application.runInBackground = true;
                _mode = EditorPrefs.GetString(ModeKey, "baseline");
                _shotPrefix = _mode;

                ScreenManager.ScreenChanged += OnScreenChanged;

                _perf = _mode == "perf";
                if (_perf)
                {
                    UnityEngine.Profiling.Profiler.enabled = true;
                    _gcRec    = Unity.Profiling.ProfilerRecorder.StartNew(
                                    Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
                    _frameRec = Unity.Profiling.ProfilerRecorder.StartNew(
                                    Unity.Profiling.ProfilerCategory.Internal, "Main Thread");
                }
                StartCoroutine(Run());
            }

            void OnScreenChanged(ScreenId id) => _screenChanged++;

            void OnDestroy()
            {
                ScreenManager.ScreenChanged -= OnScreenChanged;
                if (_gcRec.Valid)    _gcRec.Dispose();
                if (_frameRec.Valid) _frameRec.Dispose();
            }

            IEnumerator Run()
            {
                Line("=== game_polish_a probe (" + _mode + ") " + DateTime.UtcNow.ToString("u") + " ===");

                if (_mode == "baseline")
                {
                    // Motion OFF: every navigation takes the untouched fade and every capture is
                    // the screen exactly as HEAD draws it.
                    UiMotion.Enabled = false;
                    yield return Boot();
                    yield return Route();
                    UiMotion.Enabled = true;
                    Line("=== done: baseline pass complete ===");
                    yield break;
                }

                if (_mode == "parity")
                {
                    // A2, WITHIN ONE RUN — the gps_polish lesson. These screens render live data
                    // and relative time; comparing a capture taken now against one taken an hour
                    // ago diffs a moved RP balance and a ticking clock in the shared top bar and
                    // reports tens of thousands of differing pixels that have nothing to do with
                    // the animation. Forty seconds apart, in one session, none of that moves.
                    UiMotion.Enabled = true;
                    _shotPrefix = "parity_anim"; _shot = 0;
                    Line("--- pass 1: UiMotion.Enabled = true (animated arrivals) ---");
                    yield return Boot();
                    yield return Route();

                    UiMotion.Enabled = false;
                    _shotPrefix = "parity_instant"; _shot = 0;
                    Line("--- pass 2: UiMotion.Enabled = false (CanPush false everywhere => the " +
                         "untouched fade, which is the instant arrival A2 compares against) ---");
                    yield return Home();
                    yield return Route();

                    UiMotion.Enabled = true;
                    Line("=== done: parity pass complete ===");
                    yield break;
                }

                if (_mode == "option_b")
                {
                    // §D4 — the ONE run with the flag on, for Cesar's five-second clip. Turned on
                    // here, turned off in the finally-shaped tail below, and asserted off again so
                    // a crashed run cannot leave it armed for the next one.
                    LayeredPush.AllowBackgroundCrossFade = true;
                    Line("--- OPTION (b): LayeredPush.AllowBackgroundCrossFade = true (NEVER in a build) ---");
                    yield return Boot();
                    yield return OptionBRoute();
                    LayeredPush.AllowBackgroundCrossFade = false;
                    Line("--- flag restored: AllowBackgroundCrossFade = " +
                         LayeredPush.AllowBackgroundCrossFade + " ---");
                    WriteJson();
                    Line("=== done: option_b pass complete ===");
                    yield break;
                }

                // push / perf — motion ON, the direction table driven for real.
                UiMotion.Enabled = true;
                if (LayeredPush.AllowBackgroundCrossFade)
                {
                    // A1 is produced with the flag OFF. If anything left it armed, say so and fix
                    // it rather than writing a JSON that measures the wrong feature.
                    Line("WARN: AllowBackgroundCrossFade was true at the start of a '" + _mode +
                         "' run — forcing it false; A1 must be measured on the shipped path.");
                    LayeredPush.AllowBackgroundCrossFade = false;
                }
                Line("AllowBackgroundCrossFade = " + LayeredPush.AllowBackgroundCrossFade + " (A1 is the shipped path)");
                yield return Boot();
                yield return Route();
                WriteJson();
                if (_perf) WritePerf();
                Line("=== done: " + _mode + " pass complete ===");
            }

            /// <summary>
            /// §D4's clip: ModeSelection -> TournamentSelection -> back. The pair Cesar named, and
            /// the one that shows the option at its most obvious — the two screens have DIFFERENT
            /// backgrounds (2e5476ee vs 0d425c0a), which is exactly why the shipped path fades
            /// them and this flag does not.
            /// </summary>
            IEnumerator OptionBRoute()
            {
                yield return NavSlot("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE");
                yield return new WaitForSecondsRealtime(1.2f);
                yield return TapPath(ScreenId.ModeSelection, "TournamentTempEntry", ScreenId.TournamentSelection,
                                     "ModeSelection TournamentTempEntry (option b)");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Back(ScreenId.ModeSelection, "option b back");
                yield return new WaitForSecondsRealtime(1.2f);
            }

            // ═════════════════════════════════════════════════════════════════
            // The route — every shell screen, through the real widgets
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Boot()
            {
                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");
                yield return TapStart();

                // DO NOT ASSUME THE TITLE GATE LANDS ON HOME. A session that has already passed
                // the gate resumes on whatever screen it left — this run came up on GpsHub, where
                // the shared bottom nav is hidden (ShowTopBarOnly), so every nav-slot tap in the
                // route found no widget and the whole pass cascaded into nonsense. Home is reached
                // explicitly, through the BOUNDARY navigation this task does not touch, so nothing
                // the probe measures is affected by how it got there.
                yield return Home();
            }

            /// <summary>Normalise to Home. Used at boot and between passes.</summary>
            IEnumerator Home()
            {
                if (ScreenManager.Instance!.CurrentScreen != ScreenId.Home)
                {
                    Line("note: normalising to Home from " + ScreenManager.Instance.CurrentScreen +
                         " via ShowScreen (a BOUNDARY move, unchanged by this task; NOT a tap)");
                    ScreenManager.Instance.ShowScreen(ScreenId.Home);
                }
                yield return Until(() => ScreenManager.Instance!.CurrentScreen == ScreenId.Home, 25f, "Home");
                yield return new WaitForSecondsRealtime(2f);
            }

            IEnumerator Route()
            {
                yield return Shot("home");

                // ── PLAY pillar ──────────────────────────────────────────────
                yield return NavSlot("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE");
                yield return Shot("modeselection");

                yield return ModeCardPlay(ScreenId.HoleSelection, "mode card PLAY -> Practice");
                yield return Shot("holeselection");

                yield return TapPath(ScreenId.HoleSelection, "LeaderboardButton", ScreenId.Leaderboard,
                                     "HoleSelection LeaderboardButton");
                yield return Shot("leaderboard");
                yield return Back(ScreenId.HoleSelection, "leaderboard back");
                yield return Back(ScreenId.ModeSelection, "hole-selection back");

                yield return ModeCardPlay(ScreenId.MissionSelection, "mode card PLAY -> Missions");
                yield return Shot("missionselection");
                yield return Back(ScreenId.ModeSelection, "mission-selection back");

                // ── Tournaments (the 0d42 background group) ──────────────────
                yield return Ensure(ScreenId.ModeSelection);
                yield return TapPath(ScreenId.ModeSelection, "TournamentTempEntry", ScreenId.TournamentSelection,
                                     "ModeSelection TournamentTempEntry");
                yield return Shot("tournamentselection");

                yield return Force(ScreenId.TournamentLeaderboard, "TournamentLeaderboard");
                yield return Shot("tournamentleaderboard");

                yield return Force(ScreenId.TournamentHoleSelection, "TournamentHoleSelection");
                yield return Shot("tournamentholeselection");

                // ── GACHA pillar ─────────────────────────────────────────────
                yield return Ensure(ScreenId.Home);
                yield return NavSlot("NavGachaButton", ScreenId.GeneralShop, "bottom-nav GACHA");
                yield return Shot("generalshop");

                yield return TapPath(ScreenId.GeneralShop, "HistoryChip", ScreenId.GachaHistory,
                                     "GeneralShop HistoryChip");
                yield return Shot("gachahistory");

                yield return Force(ScreenId.GachaPrizes, "GachaPrizes");
                yield return Shot("gachaprizes");

                // ── INVENTORY pillar (+ the four tabs) ───────────────────────
                yield return Ensure(ScreenId.Home);
                yield return NavSlot("NavInventoryButton", ScreenId.Inventory, "bottom-nav INVENTORY");
                yield return Shot("inventory_tab0");
                for (int t = 1; t < 4; t++)
                {
                    yield return InventoryTab(t);
                    yield return Shot("inventory_tab" + t);
                }
                yield return InventoryTab(0);

                // ── CHARACTERS pillar ────────────────────────────────────────
                yield return Ensure(ScreenId.Home);
                yield return NavSlot("NavCharactersButton", ScreenId.Roster, "bottom-nav CHARACTERS");
                yield return Shot("roster");

                // ── Settings overlay ─────────────────────────────────────────
                yield return SettingsOpen();
                yield return Shot("settings_open");
                yield return SettingsClose();

                // ── back to Home, for the nav-bar selected state ─────────────
                yield return NavSlot("NavHomeButton", ScreenId.Home, "bottom-nav HOME");
                yield return Shot("home_return");

                // ── A1's remaining coverage ──────────────────────────────────
                if (_mode == "push" || _mode == "perf") yield return PushSweep();
            }

            /// <summary>
            /// EVERY ordered pair of the direction table, measured.
            ///
            /// <para>Six of the twenty-four have a real widget a player can tap and Route() has
            /// already measured them as taps. The other eighteen do not — TournamentHoleSelection
            /// has no entry point in a session with no active tournament, TournamentLeaderboard
            /// needs a FINISHED one, and GachaPrizes is only reached by completing a gacha PULL,
            /// which spends currency. Those pairs are driven here by ShowScreen / GoBack and
            /// recorded with <c>realWidget: false</c>.</para>
            ///
            /// <para>This is deliberately NOT dressed up as real navigation. The invariants —
            /// travel width, duration, the t0 offset, the settle, chrome alpha, the single
            /// ApplyScreen — are properties of the MECHANISM, and the mechanism is worth measuring
            /// on all twenty-four. A10's "driven from the real widget's onClick" claim then
            /// belongs to the six the JSON marks true, and no reader can mistake which is
            /// which.</para>
            /// </summary>
            IEnumerator PushSweep()
            {
                Line("--- push sweep: every ordered pair of the direction table ---");
                ScreenId[][] groups =
                {
                    new[] { ScreenId.ModeSelection, ScreenId.HoleSelection,
                            ScreenId.MissionSelection, ScreenId.TournamentHoleSelection },
                    new[] { ScreenId.TournamentSelection, ScreenId.TournamentLeaderboard,
                            ScreenId.Leaderboard },
                    new[] { ScreenId.GeneralShop, ScreenId.GachaHistory, ScreenId.GachaPrizes },
                };

                foreach (ScreenId[] g in groups)
                    foreach (ScreenId a in g)
                        foreach (ScreenId b in g)
                        {
                            if (a == b) continue;
                            yield return SweepOne(a, b, forward: true);
                            yield return SweepOne(b, a, forward: false);
                        }
            }

            IEnumerator SweepOne(ScreenId from, ScreenId to, bool forward)
            {
                yield return Ensure(from);
                if (ScreenManager.Instance!.CurrentScreen != from)
                {
                    Line("  sweep " + from + " -> " + to + ": could not seat on " + from + "; skipped");
                    yield break;
                }
                if (!LayeredPush.CanPush(from, to, Obj(from), Obj(to)))
                {
                    Line("  sweep " + from + " -> " + to + ": CanPush false (not a push pair)");
                    yield break;
                }

                _screenChanged = 0;
                if (forward) ScreenManager.Instance.ShowScreen(to);
                else         ScreenManager.Instance.GoBack(to);

                if (LayeredPush.IsPushing)
                    yield return Measure(from, to, forward ? "harness ShowScreen" : "harness GoBack",
                                         realWidget: false, dirForward: forward);
                yield return Arrive(to, 1f);
            }

            // ═════════════════════════════════════════════════════════════════
            // Real-widget taps
            // ═════════════════════════════════════════════════════════════════

            /// <summary>A bottom-nav slot on the SHARED PersistentUI bar — the player's own tap.</summary>
            IEnumerator NavSlot(string slot, ScreenId target, string what)
            {
                Transform? t = FindActive("Canvas/PersistentUI/BottomNavBar/" + slot)
                            ?? FindByName(slot);
                yield return Tap(t, target, what);
            }

            IEnumerator TapPath(ScreenId onScreen, string path, ScreenId target, string what)
            {
                GameObject? go = Obj(onScreen);
                Transform? t = go != null ? go.transform.Find(path) : null;
                yield return Tap(t, target, what);
            }

            /// <summary>
            /// The mode cards are spawned at runtime by ModeSelectScreenController, so they are
            /// found by walking the live ScrollRect content rather than by a serialized path. The
            /// player's sequence is TAP THE CARD (it expands, revealing ExpandedContainer) then
            /// TAP THE ACTION BUTTON — HandlePlayClicked is the only thing that navigates — and
            /// that is exactly what this does.
            ///
            /// <para>Which card carries which route is CSV data (ModesDatabaseCSV `target`), not
            /// something a name can be trusted for, so the cards are tried in order and the one
            /// that actually lands on <paramref name="target"/> wins. A card that routes somewhere
            /// else is not a failure — the run walks back and tries the next one.</para>
            /// </summary>
            IEnumerator ModeCardPlay(ScreenId target, string what)
            {
                Transform? content = ModeCardsContent();
                if (content == null) { Line("WARN: " + what + " — no mode-card content"); yield break; }

                int n = content.childCount;
                for (int i = 0; i < n; i++)
                {
                    yield return Ensure(ScreenId.ModeSelection);
                    content = ModeCardsContent();
                    if (content == null || i >= content.childCount) break;

                    Transform card = content.GetChild(i);
                    if (!card.gameObject.activeInHierarchy) continue;

                    // 1. expand the card (the real CardTapButton)
                    var tap = card.Find("CardTapButton")?.GetComponent<Button>()
                              ?? card.GetComponent<Button>();
                    if (tap != null && tap.interactable) tap.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.8f);

                    // 2. the real PLAY affordance — ModeCard.prefab names it ActionButton and
                    //    parks it under ExpandedContainer, which only exists once expanded.
                    var play = card.Find("ExpandedContainer/ActionButton")?.GetComponent<Button>();
                    if (play == null || !play.gameObject.activeInHierarchy || !play.interactable)
                    {
                        Line("  card '" + card.name + "': no active ActionButton (locked or collapsed) — next");
                        continue;
                    }

                    ScreenId from = ScreenManager.Instance!.CurrentScreen;
                    // A card can route anywhere, so whether this is a push is decided against the
                    // TARGET we are hoping for — and Measure only runs if the tween really starts.
                    bool expectPush = UiMotion.Enabled &&
                                      LayeredPush.CanPush(from, target, Obj(from), Obj(target));
                    Line("tapping " + what + " on card '" + card.name + "' via the real '" +
                         play.name + "' " + from + " -> ?" + (expectPush ? "  [PUSH expected]" : "  [fade]"));
                    _screenChanged = 0;
                    play.onClick.Invoke();

                    if (expectPush && LayeredPush.IsPushing)
                        yield return Measure(from, target, what + " (card '" + play.name + "')", realWidget: true);

                    // The boundary fade is 0.5 s out + 0.5 s in; give it room, then read where we
                    // actually landed rather than assuming.
                    yield return new WaitForSecondsRealtime(1.6f);
                    ScreenId now = ScreenManager.Instance!.CurrentScreen;
                    if (now == target)
                    {
                        Line("  -> landed on " + target + " (real card route)");
                        yield return Arrive(target, 2f);
                        yield break;
                    }
                    Line("  -> card '" + card.name + "' routed to " + now + ", not " + target + "; next card");
                }
                Line("WARN: " + what + " — no card routed to " + target);
            }

            static Transform? ModeCardsContent()
            {
                GameObject? modeGo = Obj(ScreenId.ModeSelection);
                if (modeGo == null) return null;
                // The ScrollRect owns the content rect; asking IT is proof against the container
                // being renamed or re-parented, which a hard-coded path is not.
                var sr = modeGo.GetComponentInChildren<ScrollRect>(true);
                return sr != null ? sr.content : null;
            }

            IEnumerator InventoryTab(int index)
            {
                GameObject? inv = Obj(ScreenId.Inventory);
                var ctrl = inv != null ? inv.GetComponentInChildren<Golfin.Inventory.InventoryScreenController>(true) : null;
                if (ctrl == null) { Line("WARN: no InventoryScreenController"); yield break; }

                // The REAL tab button, not ShowTab(index) — Rule 2 applies inside a screen too.
                Button? tab = null;
                foreach (Button b in inv!.GetComponentsInChildren<Button>(true))
                    if (b.transform.parent != null && b.transform.parent.name == "TabBar")
                    {
                        int i = b.transform.GetSiblingIndex();
                        if (i == index && b.gameObject.activeInHierarchy) { tab = b; break; }
                    }
                if (tab == null) { Line("WARN: inventory tab " + index + " has no button; using ShowTab (NOT a tap)"); ctrl.ShowTab(index); }
                else { Line("tapping inventory tab " + index + " ('" + tab.name + "')"); tab.onClick.Invoke(); }
                yield return new WaitForSecondsRealtime(1f);
            }

            IEnumerator SettingsOpen()
            {
                Transform? gear = FindByName("SettingsButton");
                var b = gear != null ? gear.GetComponent<Button>() : null;
                if (b != null) { Line("tapping the real SettingsButton"); b.onClick.Invoke(); }
                else if (SettingsController.Instance != null)
                {
                    Line("WARN: no SettingsButton widget; using OpenSettings() (NOT a tap)");
                    SettingsController.Instance.OpenSettings();
                }
                yield return new WaitForSecondsRealtime(1.2f);
            }

            IEnumerator SettingsClose()
            {
                Transform? close = FindByName("CloseButton");
                var b = close != null ? close.GetComponent<Button>() : null;
                if (b != null && b.gameObject.activeInHierarchy) b.onClick.Invoke();
                else SettingsController.Instance?.CloseSettings();
                yield return new WaitForSecondsRealtime(1f);
            }

            /// <summary>
            /// BACK through a real widget where one exists, else ScreenManager.GoBack — and the
            /// log SAYS WHICH. A GoBack() call is not a tap and must never be reported as one.
            /// </summary>
            IEnumerator Back(ScreenId target, string what)
            {
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                    if (b.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    yield return Tap(b.transform, target, what + " via the real '" + b.name + "'");
                    yield break;
                }
                Line("note: " + what + " — no active Back widget; ScreenManager.GoBack() (NOT a tap)");
                ScreenManager.Instance?.GoBack(target);
                yield return Arrive(target, 2.5f);
            }

            /// <summary>
            /// A screen with no reachable player path from here. Says so, loudly, and is never
            /// counted as a measured push — the baseline only needs the REST STATE of the screen.
            /// </summary>
            IEnumerator Force(ScreenId target, string what)
            {
                Line("note: " + what + " — no player path reachable from this harness; " +
                     "ShowScreen() for the REST-STATE capture only (NOT a tap, never measured)");
                ScreenManager.Instance?.ShowScreen(target);
                yield return Arrive(target, 2.5f);
            }

            /// <summary>
            /// Put the run back on <paramref name="id"/> when the previous leg did not land where
            /// it meant to, so ONE missed widget cannot cascade into a route of wrong screens
            /// captured under right names. Says so when it has to act.
            /// </summary>
            IEnumerator Ensure(ScreenId id)
            {
                if (ScreenManager.Instance!.CurrentScreen == id) yield break;
                Line("note: route is on " + ScreenManager.Instance.CurrentScreen + ", expected " + id +
                     " — re-seating via ShowScreen (NOT a tap)");
                ScreenManager.Instance.ShowScreen(id);
                yield return Arrive(id, 2f);
            }

            IEnumerator Tap(Transform? t, ScreenId target, string what)
            {
                var b = t != null ? t.GetComponent<Button>() : null;
                if (b == null) { Line("WARN: no button for " + what); yield break; }

                ScreenId from = ScreenManager.Instance!.CurrentScreen;
                bool expectPush = UiMotion.Enabled &&
                                  LayeredPush.CanPush(from, target, Obj(from), Obj(target));
                Line("tapping " + what + " (interactable=" + b.interactable + ") " + from + " -> " + target +
                     (expectPush ? "  [PUSH expected]" : "  [fade]"));

                _screenChanged = 0;
                b.onClick.Invoke();

                if (expectPush) yield return Measure(from, target, what, realWidget: true);
                yield return Arrive(target, expectPush ? 1.5f : 3f);
            }

            /// <summary>
            /// Sample every frame of the push. This is the ONLY place the invariants come from —
            /// no assertion below is read off a still or off a video.
            /// </summary>
            IEnumerator Measure(ScreenId from, ScreenId to, string widget, bool realWidget, bool dirForward = true)
            {
                GameObject? fromGo = Obj(from), toGo = Obj(to);
                var r = new Record
                {
                    From = from.ToString(), To = to.ToString(), Widget = widget,
                    RealWidget = realWidget,
                    Direction = LayeredPush.DirectionFor(from, to, push: dirForward).ToString(),
                    ExpectedDur = UiMotion.PushDur,
                };

                RectTransform? toContent   = FirstContent(to,   toGo);
                RectTransform? fromContent = FirstContent(from, fromGo);

                long gc0 = 0; double worstMs = 0;
                if (_perf && _gcRec.Valid) gc0 = 0;

                // Wait for the tween to actually start, then follow it to completion.
                float guard = Time.realtimeSinceStartup + 3f;
                while (!LayeredPush.IsPushing && Time.realtimeSinceStartup < guard) yield return null;

                while (LayeredPush.IsPushing)
                {
                    if (_perf)
                    {
                        if (_gcRec.Valid)    gc0 += _gcRec.LastValue;
                        if (_frameRec.Valid) worstMs = System.Math.Max(worstMs, _frameRec.LastValue * 1e-6);
                    }
                    yield return null;
                }

                // Everything below is read from the tween's OWN published state, sampled before it
                // moved anything — reading rest X here would read the already-staged off-screen
                // position and call THAT rest, which is the bug that made every assertion in
                // gps_polish's first run fire.
                r.W                     = LayeredPush.LastPushWidth;
                r.MeasuredDur           = LastPushElapsedSafe();
                r.Frames                = LayeredPush.LastPushFrames;
                r.Completed             = LayeredPush.LastPushCompleted;
                r.TargetOffsetAtT0      = LayeredPush.LastPushEnterOffset;
                r.EndTargetRestX        = LayeredPush.LastPushTargetRestX;
                r.EndLeaverRestX        = LayeredPush.LastPushLeaverRestX;
                r.ChromeAlphaMinOverRun = LayeredPush.LastPushChromeAlphaMin;
                r.SeamWorstCover        = LayeredPush.LastPushSeamWorstCover;
                r.EndTargetX            = toContent   != null ? toContent.anchoredPosition.x   : float.NaN;
                r.EndLeaverX            = fromContent != null ? fromContent.anchoredPosition.x : float.NaN;
                r.EndTargetContentAlpha = GroupAlpha(toContent);
                r.EndLeaverContentAlpha = GroupAlpha(fromContent);
                r.EndBlocksRaycasts     = Blocks(toContent) && Blocks(fromContent);
                r.ApplyScreenCalls      = _screenChanged;

                // ── the assertions ──────────────────────────────────────────
                if (!r.Completed) r.Fails.Add("push did not complete (interrupted)");
                if (Mathf.Abs(r.MeasuredDur - r.ExpectedDur) > DurationToleranceSec)
                    r.Fails.Add($"duration {r.MeasuredDur:0.000}s outside {r.ExpectedDur:0.000}s ±{DurationToleranceSec:0.000}");
                if (Mathf.Abs(Mathf.Abs(r.TargetOffsetAtT0) - r.W) > 1f)
                    r.Fails.Add($"t0 offset {r.TargetOffsetAtT0:0.#} is not ±W ({r.W:0.#})");
                if (r.Direction == "Forward" && r.TargetOffsetAtT0 <= 0f)
                    r.Fails.Add("Forward must enter from +W");
                if (r.Direction == "Back" && r.TargetOffsetAtT0 >= 0f)
                    r.Fails.Add("Back must enter from -W");
                if (Mathf.Abs(r.EndTargetX - r.EndTargetRestX) > 0.5f)
                    r.Fails.Add($"target content settled at x={r.EndTargetX:0.##}, rest is {r.EndTargetRestX:0.##}");
                if (Mathf.Abs(r.EndLeaverX - r.EndLeaverRestX) > 0.5f)
                    r.Fails.Add($"leaver content settled at x={r.EndLeaverX:0.##}, rest is {r.EndLeaverRestX:0.##}");
                if (r.EndTargetContentAlpha < 0.999f) r.Fails.Add($"target content alpha {r.EndTargetContentAlpha:0.###} != 1");
                if (r.EndLeaverContentAlpha < 0.999f) r.Fails.Add($"leaver content alpha {r.EndLeaverContentAlpha:0.###} != 1");
                if (!r.EndBlocksRaycasts)             r.Fails.Add("blocksRaycasts not restored");
                if (r.ApplyScreenCalls != 1)          r.Fails.Add($"ApplyScreen ran {r.ApplyScreenCalls}x, expected exactly 1 (at the end)");

                if (LayeredPush.AllowBackgroundCrossFade)
                {
                    // Option (b) only: the seam test. Never both chrome layers below 0.5.
                    if (r.SeamWorstCover < 0.5f)
                        r.Fails.Add($"seam: worst chrome cover {r.SeamWorstCover:0.###} < 0.5");
                }
                else
                {
                    // The shipped path: the two screens share a background sprite, so NOTHING
                    // about the chrome may move on any frame. This is A5's assertion, taken from
                    // inside the tween rather than off a video.
                    if (r.ChromeAlphaMinOverRun < 0.999f)
                        r.Fails.Add($"chrome alpha dropped to {r.ChromeAlphaMinOverRun:0.###} on the same-background path");
                }

                if (_perf) _perfRows.Add((r.From + "->" + r.To, gc0, worstMs, r.Frames));

                _records.Add(r);
                Line($"  measured {r.From} -> {r.To} dir={r.Direction} W={r.W:0.#} " +
                     $"dur={r.MeasuredDur:0.000}s frames={r.Frames} chromeMin={r.ChromeAlphaMinOverRun:0.###} " +
                     $"fails={r.Fails.Count}");
                foreach (string f in r.Fails) Line("    FAIL " + f);
            }

            static float LastPushElapsedSafe() => LayeredPush.LastPushElapsed;

            /// <summary>The screen's FIRST content rect — the one whose travel the invariants are
            /// written against. Read from LayeredPush's own table so the probe cannot measure a
            /// different rect than the push moved.</summary>
            static RectTransform? FirstContent(ScreenId id, GameObject? go)
            {
                if (go == null) return null;
                LayeredPush.Layers? m = LayeredPush.LayerMap(id);
                if (m == null) return null;
                foreach (string n in m.Value.Content)
                {
                    Transform? t = go.transform.Find(n);
                    if (t is RectTransform rt) return rt;
                }
                return null;
            }

            static float GroupAlpha(RectTransform? rt)
            {
                var cg = rt != null ? rt.GetComponent<CanvasGroup>() : null;
                return cg != null ? cg.alpha : 1f;
            }

            static bool Blocks(RectTransform? rt)
            {
                var cg = rt != null ? rt.GetComponent<CanvasGroup>() : null;
                return cg == null || cg.blocksRaycasts;
            }

            // ═════════════════════════════════════════════════════════════════
            // Plumbing
            // ═════════════════════════════════════════════════════════════════

            static GameObject? Obj(ScreenId id)
            {
                string? name = id switch
                {
                    ScreenId.Home                    => "HomeScreen",
                    ScreenId.Roster                  => "RosterScreen",
                    ScreenId.Inventory               => "InventoryScreen",
                    ScreenId.HoleSelection           => "HoleSelectionScreen",
                    ScreenId.ModeSelection           => "ModeSelectionScreen",
                    ScreenId.MissionSelection        => "MissionSelectionScreen",
                    ScreenId.Leaderboard             => "RankingsScreen",
                    ScreenId.TournamentHoleSelection => "TournamentHoleSelectionScreen",
                    ScreenId.TournamentLeaderboard   => "TournamentLeaderboardScreen",
                    ScreenId.TournamentSelection     => "TournamentSelectionScreen",
                    ScreenId.GeneralShop             => "GeneralShopScreen",
                    ScreenId.GachaHistory            => "GachaHistoryScreen",
                    ScreenId.GachaPrizes             => "GachaPrizesScreen",
                    _                                => null,
                };
                return name == null ? null : GameObject.Find("Canvas/ScreensRoot/" + name);
            }

            static Transform? FindActive(string path)
            {
                GameObject? go = GameObject.Find(path);
                return go != null ? go.transform : null;
            }

            static Transform? FindByName(string name)
            {
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (b.name == name && b.gameObject.activeInHierarchy) return b.transform;
                return null;
            }

            IEnumerator Arrive(ScreenId id, float settle)
            {
                yield return Until(() => ScreenManager.Instance!.CurrentScreen == id, 25f, id.ToString());
                yield return new WaitForSecondsRealtime(settle);
            }

            IEnumerator Shot(string label)
            {
                if (_mode == "perf") { Line("SHOT " + label + " skipped (perf pass)"); yield break; }

                _shot++;
                string name = string.Format("{0}_{1:00}_{2}", _shotPrefix, _shot, label);
                string path = Path.Combine(ShotDir, name + ".png");
                Directory.CreateDirectory(ShotDir);

                IEnumerator snap = CaptureCore.SnapAtEndOfFrameAndPause(name, path, skipPause: true);
                while (snap.MoveNext()) yield return snap.Current;

                // Assert the FILE, never the return value — SnapPlayModeSafe has logged a path for
                // a file it never wrote (memory: reference_snapplaymodesafe_phantom_path).
                Line("SHOT " + label + " -> " + (File.Exists(path)
                    ? path + " (" + new FileInfo(path).Length / 1024 + " KB)"
                    : "MISSING (" + path + ")"));
            }

            IEnumerator TapStart()
            {
                float deadline = Time.realtimeSinceStartup + 90f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (b.name != "StartButton" || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping the real " + b.name);
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(2f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: no StartButton appeared in 90 s");
            }

            IEnumerator Until(Func<bool> done, float seconds, string what)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
                Line((done() ? "ok   " : "TIMEOUT ") + what);
            }

            // ═════════════════════════════════════════════════════════════════
            // A1 — the invariants file. fail == 0 is the gate.
            // ═════════════════════════════════════════════════════════════════

            void WriteJson()
            {
                int fail = 0;
                foreach (Record r in _records) fail += r.Fails.Count;

                var j = new StringBuilder();
                j.AppendLine("{");
                j.AppendLine("  \"task\": \"game_polish_a\",");
                j.AppendLine("  \"mode\": \"" + _mode + "\",");
                j.AppendLine("  \"utc\": \"" + DateTime.UtcNow.ToString("u") + "\",");
                j.AppendLine("  \"allowBackgroundCrossFade\": " + (LayeredPush.AllowBackgroundCrossFade ? "true" : "false") + ",");
                j.AppendLine("  \"pushDur\": " + F(UiMotion.PushDur) + ",");
                j.AppendLine("  \"durationToleranceSec\": " + F(DurationToleranceSec) + ",");
                j.AppendLine("  \"measured\": " + _records.Count + ",");
                j.AppendLine("  \"fail\": " + fail + ",");
                j.AppendLine("  \"pushes\": [");
                for (int i = 0; i < _records.Count; i++)
                {
                    Record r = _records[i];
                    j.AppendLine("    {");
                    j.AppendLine("      \"from\": \"" + r.From + "\", \"to\": \"" + r.To + "\", \"direction\": \"" + r.Direction + "\",");
                    j.AppendLine("      \"widget\": \"" + Esc(r.Widget) + "\", \"realWidget\": " + (r.RealWidget ? "true" : "false") + ",");
                    j.AppendLine("      \"W\": " + F(r.W) + ", \"expectedDur\": " + F(r.ExpectedDur) + ", \"measuredDur\": " + F(r.MeasuredDur) + ", \"frames\": " + r.Frames + ",");
                    j.AppendLine("      \"targetOffsetAtT0\": " + F(r.TargetOffsetAtT0) + ",");
                    j.AppendLine("      \"endTargetX\": " + F(r.EndTargetX) + ", \"endTargetRestX\": " + F(r.EndTargetRestX) + ",");
                    j.AppendLine("      \"endLeaverX\": " + F(r.EndLeaverX) + ", \"endLeaverRestX\": " + F(r.EndLeaverRestX) + ",");
                    j.AppendLine("      \"endTargetContentAlpha\": " + F(r.EndTargetContentAlpha) + ", \"endLeaverContentAlpha\": " + F(r.EndLeaverContentAlpha) + ",");
                    j.AppendLine("      \"chromeAlphaMinOverRun\": " + F(r.ChromeAlphaMinOverRun) + ", \"seamWorstCover\": " + F(r.SeamWorstCover) + ",");
                    j.AppendLine("      \"blocksRaycastsRestored\": " + (r.EndBlocksRaycasts ? "true" : "false") + ",");
                    j.AppendLine("      \"applyScreenCalls\": " + r.ApplyScreenCalls + ", \"completed\": " + (r.Completed ? "true" : "false") + ",");
                    j.Append    ("      \"fails\": [");
                    for (int k = 0; k < r.Fails.Count; k++)
                        j.Append((k > 0 ? ", " : "") + "\"" + Esc(r.Fails[k]) + "\"");
                    j.AppendLine("]");
                    j.AppendLine("    }" + (i < _records.Count - 1 ? "," : ""));
                }
                j.AppendLine("  ]");
                j.AppendLine("}");

                Directory.CreateDirectory(Path.GetDirectoryName(JsonPath)!);
                File.WriteAllText(JsonPath, j.ToString());
                Line("A1 -> " + JsonPath + "  measured=" + _records.Count + " fail=" + fail);
            }

            void WritePerf()
            {
                Line("--- A13 ---");
                foreach (var row in _perfRows)
                    Line($"  {row.pair}: alloc={row.allocBytes} B over {row.frames} frames " +
                         $"({(row.frames > 0 ? row.allocBytes / row.frames : 0)} B/frame, IN SITU — this is the " +
                         $"whole app's frame, an UPPER BOUND on the tween), worst frame {row.worstMs:0.##} ms");
            }

            static string F(float v) => float.IsNaN(v) ? "null"
                : v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

            static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

            void Line(string s)
            {
                _log.AppendLine(s);
                Debug.Log("[GAME-POLISH-PROBE] " + s);
                File.WriteAllText(LogPath, _log.ToString());
            }
        }
    }
}
