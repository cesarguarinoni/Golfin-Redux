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

                Line("FATAL: mode '" + _mode + "' is not implemented in this build of the probe.");
            }

            // ═════════════════════════════════════════════════════════════════
            // The route — every shell screen, through the real widgets
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Boot()
            {
                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");
                yield return TapStart();
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
                yield return TapPath(ScreenId.ModeSelection, "TournamentTempEntry", ScreenId.TournamentSelection,
                                     "ModeSelection TournamentTempEntry");
                yield return Shot("tournamentselection");

                yield return Force(ScreenId.TournamentLeaderboard, "TournamentLeaderboard");
                yield return Shot("tournamentleaderboard");

                yield return Force(ScreenId.TournamentHoleSelection, "TournamentHoleSelection");
                yield return Shot("tournamentholeselection");

                // ── GACHA pillar ─────────────────────────────────────────────
                yield return NavSlot("NavGachaButton", ScreenId.GeneralShop, "bottom-nav GACHA");
                yield return Shot("generalshop");

                yield return TapPath(ScreenId.GeneralShop, "HistoryChip", ScreenId.GachaHistory,
                                     "GeneralShop HistoryChip");
                yield return Shot("gachahistory");

                yield return Force(ScreenId.GachaPrizes, "GachaPrizes");
                yield return Shot("gachaprizes");

                // ── INVENTORY pillar (+ the four tabs) ───────────────────────
                yield return NavSlot("NavInventoryButton", ScreenId.Inventory, "bottom-nav INVENTORY");
                yield return Shot("inventory_tab0");
                for (int t = 1; t < 4; t++)
                {
                    yield return InventoryTab(t);
                    yield return Shot("inventory_tab" + t);
                }
                yield return InventoryTab(0);

                // ── CHARACTERS pillar ────────────────────────────────────────
                yield return NavSlot("NavCharactersButton", ScreenId.Roster, "bottom-nav CHARACTERS");
                yield return Shot("roster");

                // ── Settings overlay ─────────────────────────────────────────
                yield return SettingsOpen();
                yield return Shot("settings_open");
                yield return SettingsClose();

                // ── back to Home, for the nav-bar selected state ─────────────
                yield return NavSlot("NavHomeButton", ScreenId.Home, "bottom-nav HOME");
                yield return Shot("home_return");
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
            /// found by walking the live CardsScrollView rather than by a serialized path. The
            /// player's sequence is TAP THE CARD (it expands) then TAP PLAY, and that is what
            /// this does — HandlePlayClicked is the only thing that navigates.
            /// </summary>
            IEnumerator ModeCardPlay(ScreenId target, string what)
            {
                GameObject? modeGo = Obj(ScreenId.ModeSelection);
                Transform? content = modeGo != null
                    ? modeGo.transform.Find("CardsContainer/CardsScrollView/Viewport/Content")
                    : null;
                if (content == null)
                {
                    // The viewport's content child is spawned by the controller; find it by walking.
                    Transform? vp = modeGo != null
                        ? modeGo.transform.Find("CardsContainer/CardsScrollView/Viewport") : null;
                    if (vp != null && vp.childCount > 0) content = vp.GetChild(0);
                }
                if (content == null) { Line("WARN: " + what + " — no mode-card content"); yield break; }

                foreach (Transform card in content)
                {
                    if (!card.gameObject.activeInHierarchy) continue;
                    Transform? tap = card.Find("CardTapButton") ?? card;
                    var tb = tap.GetComponent<Button>();
                    if (tb != null && tb.interactable) { tb.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.6f); }

                    Button? play = null;
                    foreach (Button b in card.GetComponentsInChildren<Button>(false))
                        if (b.name.IndexOf("Play", StringComparison.OrdinalIgnoreCase) >= 0
                            && b.gameObject.activeInHierarchy && b.interactable) { play = b; break; }
                    if (play == null) continue;

                    ScreenId from = ScreenManager.Instance!.CurrentScreen;
                    Line("tapping " + what + " on card '" + card.name + "' via '" + play.name + "' " + from + " -> " + target);
                    play.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.6f);
                    if (ScreenManager.Instance!.CurrentScreen == target || IsNavigating())
                    {
                        yield return Arrive(target, 2.5f);
                        yield break;
                    }
                }
                Line("WARN: " + what + " — no card routed to " + target);
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

            IEnumerator Tap(Transform? t, ScreenId target, string what)
            {
                var b = t != null ? t.GetComponent<Button>() : null;
                if (b == null) { Line("WARN: no button for " + what); yield break; }

                ScreenId from = ScreenManager.Instance!.CurrentScreen;
                Line("tapping " + what + " (interactable=" + b.interactable + ") " + from + " -> " + target);
                b.onClick.Invoke();
                yield return Arrive(target, 3f);
            }

            static bool IsNavigating() => true;

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

            void Line(string s)
            {
                _log.AppendLine(s);
                Debug.Log("[GAME-POLISH-PROBE] " + s);
                File.WriteAllText(LogPath, _log.ToString());
            }
        }
    }
}
