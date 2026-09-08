// ─────────────────────────────────────────────────────────────────────────────
// game_polish_c §C1/§C2/§C3 — the sweep's instrument.
//
// WHY A THIRD PROBE AND NOT A MODE ON THE OTHER TWO. Same argument as b's D-8:
// GamePolishProbe belongs to game_polish_a and GamePolishProbeB to b, both DONE
// and approved, and both write to their own task folders. A sibling reuses the
// arming pattern and cannot regress either one's evidence.
//
// WHAT IT MEASURES, and every one of the three is a TABLE WITH A VERDICT PER SITE
// (PIPELINE_HARDENING §22 — enumerate, do not sample):
//
//   C1  every UnityEngine.UI.Button reachable in a running shell, INCLUDING the
//       inactive children of every screen the route visits and the modals the b
//       driver opens. Recorded per path: active, interactable, hasFeedback, the
//       prefab it came from, and where it was first seen. The audit is LIVE
//       because the serialized count cannot be one: it counts template rows,
//       Selectable subclasses and every non-script reference to the Button GUID.
//
//   C2  every ScrollRect, with its five feel fields, its owning controller and
//       whether that controller drives position itself.
//
//   C3  every surface against the iPhone 15 Pro Max safe area. The VERDICT is
//       geometry — each visible, non-full-bleed element's screen rect against the
//       inset bands — not a human reading an overlay. The overlay is drawn on the
//       capture so Cesar can see the same thing the numbers say.
//
// THE SAFE AREA IS REAL WHEN IT CAN BE. `Screen.safeArea` in a plain Game View is
// the whole screen, so a fitter is a no-op and no element can ever be "in" the
// notch. When the Device Simulator is driving (Assets/Editor/DeviceSimulator/
// "Apple iPhone 15 Pro Max.device" — authored by this task, since the Editor
// ships nothing newer than the iPhone 12 Pro Max) Screen.safeArea IS the device's
// and the numbers are the device's. When it is not, the probe MODELS the device's
// insets proportionally on the current view and says `source: modelled` in the
// JSON. It never silently reports a full-screen safe area as "clear".
//
// REST GEOMETRY (§A5). `restgeom` dumps every RectTransform's four world corners
// on every shell screen. Run before the change and after; Docs/Scripts/
// game_polish_c_restgeom.py diffs them. That is the 0-px claim as a NUMBER,
// immune to the live clock and the moving RP balance that make a pixel diff of
// two sessions unreadable (b's parity_diff.txt is the scar).
//
// CAPTURE RULE 0. Every capture goes through CaptureCore, never a hand-rolled
// ScreenCapture or RT read.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishProbeC
    {
        const string ArmedKey = "game_polish_c.probe.armed";
        const string ModeKey  = "game_polish_c.probe.mode";
        const string TagKey   = "game_polish_c.probe.tag";

        const string TaskDir = "Docs/Specs/Active/game_polish_c";
        const string ShotDir = TaskDir + "/screenshots";
        const string OutDir  = "Docs/Diagnostics/_capture";

        // ── the device (§C3) ──────────────────────────────────────────────────
        // iPhone 15 Pro Max, portrait: 1290x2796 px, 59 pt top inset (Dynamic Island)
        // and 34 pt bottom (home indicator) — 177 px and 102 px at @3x.
        public const int   DeviceW        = 1290;
        public const int   DeviceH        = 2796;
        public const float DeviceTopInset = 177f;
        public const float DeviceBotInset = 102f;

        [MenuItem("GOLFIN/Game Polish/Probe C — sweep BEFORE (buttons + scrolls + safe area)", priority = 300)]
        public static void ArmSweepBefore() => Arm("sweep", "before");

        [MenuItem("GOLFIN/Game Polish/Probe C — sweep AFTER (buttons + scrolls + safe area)", priority = 301)]
        public static void ArmSweepAfter() => Arm("sweep", "after");

        [MenuItem("GOLFIN/Game Polish/Probe C — safe area BEFORE (iPhone 15 Pro Max simulator)", priority = 305)]
        public static void ArmSimBefore() => Arm("sweep", "sim_before");

        [MenuItem("GOLFIN/Game Polish/Probe C — safe area AFTER (iPhone 15 Pro Max simulator)", priority = 306)]
        public static void ArmSimAfter() => Arm("sweep", "sim_after");

        [MenuItem("GOLFIN/Game Polish/Probe C — rest geometry BEFORE (A5)", priority = 302)]
        public static void ArmGeomBefore() => Arm("restgeom", "before");

        [MenuItem("GOLFIN/Game Polish/Probe C — rest geometry AFTER (A5)", priority = 303)]
        public static void ArmGeomAfter() => Arm("restgeom", "after");

        [MenuItem("GOLFIN/Game Polish/Probe C — press feedback seen (A2)", priority = 304)]
        public static void ArmPress() => Arm("press", "after");

        [MenuItem("GOLFIN/Game Polish/Probe C — toast fade curve (A6)", priority = 308)]
        public static void ArmToast() => Arm("toast", "after");

        public static void Arm(string mode, string tag)
        {
            Directory.CreateDirectory(OutDir);
            Directory.CreateDirectory(ShotDir);
            EditorPrefs.SetString(ModeKey, mode);
            EditorPrefs.SetString(TagKey, tag);
            EditorPrefs.SetBool(ArmedKey, true);
            File.WriteAllText(LogPath(mode, tag), "");

            // A `sim_*` tag runs on the REAL device safe area. Without it Screen.safeArea is the
            // whole Game View, every SafeAreaFitter in the shell is a no-op, and C3 would be
            // measuring a device that does not exist.
            if (tag.StartsWith("sim")) EnsureSimulator();

            if (EditorApplication.isPlaying) { Spawn(); return; }
            EditorApplication.update -= EnterWhenIdle;
            EditorApplication.update += EnterWhenIdle;
        }

        static string LogPath(string mode, string tag) => $"{OutDir}/game_polish_c_{mode}_{tag}.log";

        /// <summary>
        /// Open the Device Simulator on the iPhone 15 Pro Max.
        ///
        /// <para>The Editor ships eleven device definitions and the newest iPhone among them is
        /// the 12 Pro Max (47 pt notch). §C3 names the 15 Pro Max (59 pt Dynamic Island), so this
        /// task authors that definition at
        /// <c>Assets/Editor/DeviceSimulator/Apple iPhone 15 Pro Max.device</c> — a tracked project
        /// asset the DeviceLoader picks up like any built-in, reusable by every later task.</para>
        ///
        /// <para>Reflection, because <c>SimulatorWindow</c> and <c>DeviceSimulatorMain</c> are
        /// internal. Everything it touches is an EDITOR WINDOW; nothing in the project or the
        /// scene is modified, and <see cref="CloseSimulator"/> puts the layout back.</para>
        /// </summary>
        public static bool EnsureSimulator()
        {
            try
            {
                Type? wt = Type.GetType("UnityEditor.DeviceSimulation.SimulatorWindow, UnityEditor.DeviceSimulatorModule");
                if (wt == null) { Debug.LogWarning("[GamePolishProbeC] no SimulatorWindow type"); return false; }

                EditorWindow win = EditorWindow.GetWindow(wt, false, "Simulator", true);
                object? main = wt.GetProperty("main", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                 ?.GetValue(win);
                if (main == null) { Debug.LogWarning("[GamePolishProbeC] SimulatorWindow.main null"); return false; }

                var devices = (Array?)main.GetType().GetProperty("devices",
                                  BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(main);
                if (devices == null) { Debug.LogWarning("[GamePolishProbeC] no device list"); return false; }

                for (int i = 0; i < devices.Length; i++)
                {
                    object? d = devices.GetValue(i);
                    object? info = d?.GetType().GetField("deviceInfo")?.GetValue(d);
                    var name = info?.GetType().GetField("friendlyName")?.GetValue(info) as string;
                    if (name != DeviceName) continue;
                    main.GetType().GetProperty("deviceIndex",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(main, i);
                    win.Show();
                    win.Focus();
                    win.Repaint();

                    // THE GAME VIEW HAS TO GO, and this cost a ten-minute stalled run to learn.
                    // Unity picks ONE play-mode view; with a docked GameView still open it picks
                    // that, the Simulator renders nothing, and Screen.safeArea is the whole view —
                    // so a `sim` run silently measures no device at all. Closing every GameView
                    // leaves the Simulator as the only candidate. CloseSimulator() puts the Game
                    // View back (feedback_leave_editor_clean).
                    var gv = Type.GetType("UnityEditor.GameView, UnityEditor");
                    if (gv != null)
                        foreach (EditorWindow g in (Resources.FindObjectsOfTypeAll(gv) as EditorWindow[]) ?? new EditorWindow[0])
                            g.Close();

                    Debug.Log($"[GamePolishProbeC] Device Simulator -> '{name}' (index {i}); " +
                              "Game View closed so the simulator is the play-mode view.");
                    return true;
                }
                Debug.LogWarning($"[GamePolishProbeC] '{DeviceName}' not in the simulator's device list — " +
                                 "is Assets/Editor/DeviceSimulator/*.device imported?");
                return false;
            }
            catch (Exception ex) { Debug.LogWarning("[GamePolishProbeC] simulator: " + ex.Message); return false; }
        }

        public const string DeviceName = "Apple iPhone 15 Pro Max";

        /// <summary>Put the Editor layout back (feedback_leave_editor_clean).</summary>
        [MenuItem("GOLFIN/Game Polish/Probe C — close the Device Simulator", priority = 307)]
        public static void CloseSimulator()
        {
            try
            {
                Type? wt = Type.GetType("UnityEditor.DeviceSimulation.SimulatorWindow, UnityEditor.DeviceSimulatorModule");
                if (wt == null) return;
                foreach (EditorWindow w in Resources.FindObjectsOfTypeAll(wt) as EditorWindow[] ?? new EditorWindow[0])
                    w.Close();
                var gv = Type.GetType("UnityEditor.GameView, UnityEditor");
                if (gv != null) EditorWindow.GetWindow(gv, false, "Game", true);
                Debug.Log("[GamePolishProbeC] Device Simulator closed; Game View restored.");
            }
            catch (Exception ex) { Debug.LogWarning("[GamePolishProbeC] closing simulator: " + ex.Message); }
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
            var go = new GameObject("__GamePolishProbeC");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        // ═════════════════════════════════════════════════════════════════════

        sealed class BtnRow
        {
            public string Path = "", Prefab = "", FirstSeen = "";
            public bool EverActive, EverInteractable, HasFeedback;
            public string Exclusion = "";      // "" = player-facing, in scope
        }

        sealed class SrRow
        {
            public string Path = "", Prefab = "", Owner = "", Surface = "";
            public string MovementType = "";
            public float  Elasticity, Deceleration, Sensitivity;
            public bool   Inertia, Horizontal, Vertical;
            public string Exclusion = "";
        }

        sealed class SafeRow
        {
            public string Surface = "";
            public string Verdict = "clear";
            public string View = "", Source = "";
            public int    ContentTop, ContentBot;
            public readonly List<string> TopHits = new List<string>();
            public readonly List<string> BotHits = new List<string>();
            public string Shot = "";
        }

        public sealed class Driver : MonoBehaviour
        {
            readonly StringBuilder _log = new StringBuilder();
            readonly Dictionary<string, BtnRow>  _buttons = new Dictionary<string, BtnRow>();
            readonly Dictionary<string, SrRow>   _scrolls = new Dictionary<string, SrRow>();
            readonly List<SafeRow>               _safe    = new List<SafeRow>();
            readonly List<string>                _pressLog = new List<string>();

            string _mode = "sweep", _tag = "before";
            int _shot;
            GameObject? _overlay;

            // The modelled-or-real safe area, in screen px, resolved once per surface.
            float _topInset, _botInset;
            string _safeSource = "modelled";

            void Start()
            {
                Application.runInBackground = true;
                _mode = EditorPrefs.GetString(ModeKey, "sweep");
                _tag  = EditorPrefs.GetString(TagKey, "before");
                StartCoroutine(Run());
            }

            IEnumerator Run()
            {
                Line($"=== game_polish_c probe ({_mode}/{_tag}) {DateTime.UtcNow:u} ===");
                Line($"view {Screen.width}x{Screen.height}  Screen.safeArea={Screen.safeArea}");
                ResolveSafeArea();
                Line($"safe area source={_safeSource} topInset={_topInset:0.#}px botInset={_botInset:0.#}px " +
                     $"(iPhone 15 Pro Max {DeviceW}x{DeviceH}, {DeviceTopInset:0}px / {DeviceBotInset:0}px)");

                // A `sim` tag that fell back to the modelled inset is worthless AND DANGEROUS: the
                // fitters would be no-ops, every surface would read `clear`, and the run would look
                // like a pass. Say so at the top of the log and in the JSON rather than letting a
                // reader assume the device was driving because the file says sim_.
                if (_tag.StartsWith("sim") && _safeSource != "device")
                    Line("*** FAIL: this is a sim_ run but Screen.safeArea is the whole view — the " +
                         "Device Simulator is NOT driving. Open it on '" + DeviceName + "' and re-run; " +
                         "do NOT read the verdicts below as device measurements. ***");

                yield return Boot();

                switch (_mode)
                {
                    case "restgeom": yield return RestGeom();          break;
                    case "press":    yield return Press();             break;
                    case "toast":    yield return ToastFade();         break;
                    default:         yield return Sweep();             break;
                }
                Line($"=== done: {_mode}/{_tag} ===");
            }

            // ═════════════════════════════════════════════════════════════════
            // §C3 — the safe area this run is measuring against
            // ═════════════════════════════════════════════════════════════════

            /// <summary>
            /// Real when the Device Simulator is driving, modelled otherwise — and the JSON says
            /// WHICH. A plain Game View reports the whole screen as safe, which would make every
            /// surface trivially `clear` and the whole of C3 a rubber stamp.
            /// </summary>
            void ResolveSafeArea()
            {
                Rect sa = Screen.safeArea;
                float top = Screen.height - (sa.y + sa.height);
                float bot = sa.y;
                if (top > 1f || bot > 1f)
                {
                    _safeSource = "device";
                    _topInset = top; _botInset = bot;
                    return;
                }
                _safeSource = "modelled";
                _topInset = Mathf.Round(Screen.height * (DeviceTopInset / DeviceH));
                _botInset = Mathf.Round(Screen.height * (DeviceBotInset / DeviceH));
            }

            // ═════════════════════════════════════════════════════════════════
            // The route — a's, plus the tabs, the settings overlay and the modals
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Sweep()
            {
                yield return Stop("Home");

                yield return NavSlot("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE");
                yield return Stop("ModeSelection");

                yield return ModeCardPlay(ScreenId.HoleSelection);
                yield return Stop("HoleSelection");

                yield return TapPath(ScreenId.HoleSelection, "LeaderboardButton", ScreenId.Leaderboard);
                yield return Stop("Leaderboard");
                yield return Ensure(ScreenId.ModeSelection);

                yield return ModeCardPlay(ScreenId.MissionSelection);
                yield return Stop("MissionSelection");

                yield return Force(ScreenId.TournamentSelection);
                yield return Stop("TournamentSelection");
                yield return Force(ScreenId.TournamentLeaderboard);
                yield return Stop("TournamentLeaderboard");
                yield return Force(ScreenId.TournamentHoleSelection);
                yield return Stop("TournamentHoleSelection");

                yield return Ensure(ScreenId.Home);
                yield return NavSlot("NavGachaButton", ScreenId.GeneralShop, "bottom-nav GACHA");
                yield return Stop("GeneralShop");

                yield return TapPath(ScreenId.GeneralShop, "HistoryChip", ScreenId.GachaHistory);
                yield return Stop("GachaHistory");

                yield return Force(ScreenId.GachaPrizes);
                yield return Stop("GachaPrizes");

                yield return Force(ScreenId.StaminaShopSelection);
                yield return Stop("StaminaShopSelection");

                yield return Ensure(ScreenId.Home);
                yield return NavSlot("NavInventoryButton", ScreenId.Inventory, "bottom-nav INVENTORY");
                for (int t = 0; t < 4; t++)
                {
                    if (t > 0) yield return InventoryTab(t);
                    yield return Stop("Inventory_tab" + t);
                }
                yield return InventoryTab(0);

                yield return Ensure(ScreenId.Home);
                yield return NavSlot("NavCharactersButton", ScreenId.Roster, "bottom-nav CHARACTERS");
                yield return Stop("Roster");

                yield return SettingsOpen();
                yield return Stop("SettingsOverlay");
                yield return SettingsClose();

                yield return Ensure(ScreenId.Home);
                yield return Modals();

                WriteSweep();
            }

            /// <summary>
            /// Open every non-GPS modal through b's driver — activate its inactive ancestors,
            /// Show(), record, Hide(), put the chain back. A HARNESS action, recorded as one: the
            /// buttons and the geometry it exposes are the modal's own either way, and the
            /// alternative is a table with a hole where every modal should be.
            /// </summary>
            IEnumerator Modals()
            {
                Line("--- modals (harness-driven Show(); see the report's table) ---");
                var all = new List<ModalController>(
                    UnityEngine.Object.FindObjectsByType<ModalController>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None));

                foreach (ModalController m in all)
                {
                    if (m == null) continue;
                    string type = m.GetType().Name;
                    if (IsGps(type)) { Line("  skip " + type + " (Gps/, out of scope)"); continue; }

                    List<GameObject> turnedOn = ActivateChain(m);
                    bool threw = false;
                    try { m.Show(); }
                    catch (Exception ex) { threw = true; Line($"  {type}: Show() threw {ex.GetType().Name}"); }
                    if (!threw)
                    {
                        // The widget owns HoleComplete's visibility (§D1.3 of b) — Show() is a
                        // documented no-op there, so force the panel on for the audit.
                        if (m.modalPanel != null && !m.modalPanel.activeSelf) m.modalPanel.SetActive(true);
                        yield return new WaitForSecondsRealtime(0.45f);
                        yield return Stop("Modal_" + type);
                        try { m.Hide(); } catch { /* driven without its real data; see b's note */ }
                        yield return new WaitForSecondsRealtime(0.25f);
                        if (m.modalPanel != null) m.modalPanel.SetActive(false);
                    }
                    RestoreChain(turnedOn);
                    yield return null;
                }
            }

            static bool IsGps(string type) =>
                type is "VenuePickerModalController" or "VoteCreateModalController"
                     or "GiftSendModalController" or "CheckInConfirmModalController"
                     or "RoundCompleteModalController";

            static List<GameObject> ActivateChain(ModalController m)
            {
                var turnedOn = new List<GameObject>();
                var chain = new List<GameObject>();
                for (Transform t = m.transform; t != null; t = t.parent)
                    if (!t.gameObject.activeSelf) chain.Add(t.gameObject);
                for (int i = chain.Count - 1; i >= 0; i--) { chain[i].SetActive(true); turnedOn.Add(chain[i]); }
                return turnedOn;
            }

            static void RestoreChain(List<GameObject> turnedOn)
            {
                for (int i = turnedOn.Count - 1; i >= 0; i--)
                    if (turnedOn[i] != null) turnedOn[i].SetActive(false);
            }

            // ═════════════════════════════════════════════════════════════════
            // One stop on the route: the three tables plus the capture
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Stop(string surface)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                CollectButtons(surface);
                CollectScrolls(surface);
                SafeRow row = CheckSafeArea(surface);
                _safe.Add(row);

                // NO CAPTURE ON A SIMULATOR PASS, and this is a real constraint rather than a
                // preference. CaptureCore's shutter yields on WaitForEndOfFrame and reads the
                // GameView's render texture; with the Device Simulator as the play-mode view there
                // is no GameView rendering, and a sim run hung there for ten minutes without an
                // exception, one surface into the route. So the simulator pass MEASURES — which is
                // the gate (PIPELINE_HARDENING §3: the invariant JSON decides, the picture is for
                // the reader) — and the 1170x2532 pass takes the stills, with the device's bands
                // drawn on them by the overlay.
                if (!_tag.StartsWith("sim"))
                {
                    ShowOverlay();
                    yield return null;
                    yield return Shot(surface, row);
                    HideOverlay();
                }

                Line($"  [{surface}] buttons={_buttons.Count} scrolls={_scrolls.Count} safe={row.Verdict}" +
                     (row.TopHits.Count + row.BotHits.Count > 0
                        ? $"  top={row.TopHits.Count} bottom={row.BotHits.Count}" : ""));
            }

            // ── §C1 ──────────────────────────────────────────────────────────

            void CollectButtons(string surface)
            {
                foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (b == null) continue;
                    string path = PathOf(b.transform);
                    if (path.StartsWith("__")) continue;                 // the probe's own overlay

                    if (!_buttons.TryGetValue(path, out BtnRow? r))
                    {
                        r = new BtnRow { Path = path, FirstSeen = surface, Prefab = PrefabOf(b) };
                        r.Exclusion = Exclusion(path, b, CanvasSize());
                        _buttons[path] = r;
                    }
                    // hasFeedback is re-read every visit: a runtime clone can differ from the row
                    // that was there a screen ago, and the LAST reading is the one that matters.
                    r.HasFeedback      = b.GetComponent<ButtonPressFeedback>() != null;
                    r.EverActive      |= b.gameObject.activeInHierarchy;
                    r.EverInteractable|= b.interactable;
                }
            }

            /// <summary>
            /// DELEGATED, never restated. The builder that adds the component and the test that
            /// guards it forever both ask <see cref="PressFeedbackScope"/>; if this probe kept its
            /// own copy of the rules the audit would eventually report a defect the builder
            /// deliberately skips, or bless one it fixes. One rule set, three callers.
            /// </summary>
            static string Exclusion(string path, Button b, Vector2 canvasSize)
                => PressFeedbackScope.ExclusionFor(b, path, canvasSize);

            static readonly string[] GpsRoots = PressFeedbackScope.GpsRoots;

            // ── §C2 ──────────────────────────────────────────────────────────

            void CollectScrolls(string surface)
            {
                foreach (ScrollRect sr in UnityEngine.Object.FindObjectsByType<ScrollRect>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (sr == null) continue;
                    string path = PathOf(sr.transform);
                    if (_scrolls.ContainsKey(path)) { _scrolls[path] = Snapshot(sr, path, _scrolls[path].Surface); continue; }
                    _scrolls[path] = Snapshot(sr, path, surface);
                }
            }

            static SrRow Snapshot(ScrollRect sr, string path, string surface)
            {
                string owner = "";
                Transform t = sr.transform.parent != null ? sr.transform.parent : sr.transform;
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name.EndsWith("CarouselController"))
                        owner = mb.GetType().Name;
                if (owner == "")
                    foreach (MonoBehaviour mb in sr.GetComponents<MonoBehaviour>())
                        if (mb != null && mb.GetType().Name.EndsWith("CarouselController"))
                            owner = mb.GetType().Name;

                return new SrRow
                {
                    Path = path, Surface = surface, Owner = owner,
                    Prefab = PrefabOf(sr),
                    MovementType = sr.movementType.ToString(),
                    Elasticity = sr.elasticity, Deceleration = sr.decelerationRate,
                    Sensitivity = sr.scrollSensitivity, Inertia = sr.inertia,
                    Horizontal = sr.horizontal, Vertical = sr.vertical,
                    Exclusion = ScrollExclusion(path),
                };
            }

            static string ScrollExclusion(string path)
            {
                foreach (string s in GpsRoots)
                    if (path.Contains("/" + s)) return "Gps/ — out of scope";
                foreach (string s in AuthScreens)
                    if (path.Contains("/" + s)) return "auth screen — Tier 2, not this track (SPEC §C2)";
                return "";
            }

            static readonly string[] AuthScreens =
            { "LoginScreen", "SignUpScreen", "CreateUsernameScreen", "ResetPasswordScreen", "EmailConfirmationScreen" };

            // ── §C3 ──────────────────────────────────────────────────────────

            /// <summary>
            /// A surface is a hit when a VISIBLE element that CARRIES MEANING — text, or something
            /// the player taps — crosses into an inset band. Four filters, each of which was added
            /// because without it the detector reported a defect that is not one:
            ///
            /// <list type="number">
            /// <item>CLIPPED-OUT rows. A list row scrolled past the viewport still has a screen
            /// rect at y = -118; it is behind a RectMask2D and nobody can see it. The rect is
            /// intersected with every clipping ancestor before it is tested, and an element whose
            /// visible rect is empty is not on screen at all. Without this, every scrolling screen
            /// reported a bottom hit for rows the mask had already cut away.</item>
            /// <item>FULL-BLEED backdrops. §C3 says backgrounds stay full-bleed, so a graphic that
            /// covers >=90% of the view on BOTH axes is exempt — measured, not asserted from a
            /// name.</item>
            /// <item>DECORATION. A Home character illustration that runs off the bottom edge is
            /// art, not content: it carries no text and takes no tap. Decorative Images are
            /// LISTED (so nothing is hidden) but do not set the verdict; text and Selectables do.</item>
            /// <item>SHARED CHROME. PersistentUI's top bar and bottom nav are on every screen, and
            /// §C3 already assigns them: the top bar has `safe_area_top_bar`, the nav bar is
            /// deliberately flush (gps_polish EnsureNavBarSafeArea — insetting it floated the bar
            /// and showed background under it). They go in their own bucket, reported once, rather
            /// than making all 32 surfaces read "hit" for the same two objects.</item>
            /// </list>
            /// </summary>
            SafeRow CheckSafeArea(string surface)
            {
                // RE-RESOLVED PER SURFACE, not once at Start. An Editor play-mode view can change
                // size mid-run — a Simulator window losing focus, a Game View reappearing — and a
                // band computed from a screen height that is no longer current puts the notch
                // hundreds of pixels into the middle of the screen. One run reported "band starts
                // 676" on a 2796-tall device, which is how this was found: the insets were the
                // device's and the height was a stale 853.
                ResolveSafeArea();
                var row = new SafeRow
                {
                    Surface = surface,
                    View    = Screen.width + "x" + Screen.height,
                    Source  = _safeSource,
                };
                float H = Screen.height, W = Screen.width;
                float topLine = H - _topInset, botLine = _botInset;

                // A sim run that lost the device mid-route has not measured this surface. Say so
                // instead of scoring it against a modelled band and calling the result a verdict.
                if (_tag.StartsWith("sim") && _safeSource != "device")
                {
                    row.Verdict = "NOT MEASURED — the simulator was not driving (view " + row.View + ")";
                    return row;
                }

                foreach (Graphic g in UnityEngine.Object.FindObjectsByType<Graphic>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (g == null || !g.isActiveAndEnabled) continue;
                    string path = PathOf(g.transform);
                    if (path.StartsWith("__")) continue;
                    if (g is TMP_SubMeshUI) continue;
                    if (g.canvas == null) continue;
                    bool gps = false;
                    foreach (string s in GpsRoots) if (path.Contains("/" + s)) { gps = true; break; }
                    if (gps) continue;
                    if (EffectiveAlpha(g) < 0.05f) continue;

                    var rt = (RectTransform)g.transform;
                    Rect r = VisibleRect(rt, g.canvas);
                    if (r.width < 1f || r.height < 1f) continue;                 // clipped away
                    if (r.width >= W * 0.9f && r.height >= H * 0.9f) continue;   // full-bleed backdrop

                    bool topHit = r.yMax > topLine + 0.5f;
                    bool botHit = r.yMin < botLine - 0.5f;
                    if (!topHit && !botHit) continue;

                    bool chrome = path.StartsWith("PersistentUI");
                    bool meaningful = g is TMP_Text || g.GetComponent<Selectable>() != null ||
                                      g.GetComponentInParent<Selectable>() != null;
                    string kind = chrome ? "chrome" : meaningful ? "content" : "decorative";

                    if (topHit) row.TopHits.Add($"{kind}: {path} [yMax={r.yMax:0}, band starts {topLine:0}]");
                    if (botHit) row.BotHits.Add($"{kind}: {path} [yMin={r.yMin:0}, band ends {botLine:0}]");

                    if (kind == "content") { if (topHit) row.ContentTop++; if (botHit) row.ContentBot++; }
                }

                row.Verdict = row.ContentTop > 0 && row.ContentBot > 0 ? "top+bottom hit"
                            : row.ContentTop > 0 ? "top hit"
                            : row.ContentBot > 0 ? "bottom hit"
                            : "clear";
                return row;
            }

            /// <summary>
            /// The element's rect as the PLAYER sees it: its own screen rect intersected with every
            /// clipping ancestor (RectMask2D, a masked Graphic, a ScrollRect viewport). An empty
            /// result means the element is not on screen even though its transform says otherwise.
            /// </summary>
            static Rect VisibleRect(RectTransform rt, Canvas canvas)
            {
                Rect r = ScreenRect(rt, canvas);
                r = ShrinkToGlyphs(rt, canvas, r);
                for (Transform t = rt.parent; t != null; t = t.parent)
                {
                    bool clips = t.GetComponent<RectMask2D>() != null;
                    if (!clips)
                    {
                        var mask = t.GetComponent<Mask>();
                        if (mask != null && mask.enabled) clips = true;
                    }
                    if (!clips) continue;
                    if (t is not RectTransform crt) continue;
                    r = Intersect(r, ScreenRect(crt, canvas));
                    if (r.width <= 0f || r.height <= 0f) return new Rect(0, 0, 0, 0);
                }
                return r;
            }

            /// <summary>
            /// For a TMP label, the thing that must clear the home indicator is the GLYPHS, not
            /// the RectTransform they sit in.
            ///
            /// <para>This is the whole of C3's first false positive, and it is worth the
            /// paragraph. <c>InventoryScreen/…/InfoSection/InfoText</c> has a 573.6 px-tall rect
            /// holding 106.7 px of text, top-aligned: the box reaches y = 92 on the device — 10 px
            /// inside the 102 px home-indicator band — while the last line of type stops 500 px
            /// above it. Reported off the rect it is a defect on four surfaces; reported off the
            /// rendered bounds it is nothing at all, and "fixing" it would have moved a label that
            /// was never in the way. (Project memory:
            /// text_containment_reference_is_drawn_background — the same lesson from the other
            /// direction.)</para>
            /// </summary>
            static Rect ShrinkToGlyphs(RectTransform rt, Canvas canvas, Rect fallback)
            {
                var tmp = rt.GetComponent<TMP_Text>();
                if (tmp == null) return fallback;
                Bounds b = tmp.textBounds;
                if (b.size.x <= 0.01f || b.size.y <= 0.01f) return fallback;   // not laid out yet

                Camera? cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 lo = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(new Vector3(b.min.x, b.min.y, 0f)));
                Vector2 hi = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(new Vector3(b.max.x, b.max.y, 0f)));
                var glyphs = new Rect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y),
                                      Mathf.Abs(hi.x - lo.x), Mathf.Abs(hi.y - lo.y));
                return Intersect(fallback, glyphs);
            }

            static Rect Intersect(Rect a, Rect b)
            {
                float x0 = Mathf.Max(a.xMin, b.xMin), x1 = Mathf.Min(a.xMax, b.xMax);
                float y0 = Mathf.Max(a.yMin, b.yMin), y1 = Mathf.Min(a.yMax, b.yMax);
                return (x1 <= x0 || y1 <= y0) ? new Rect(0, 0, 0, 0) : new Rect(x0, y0, x1 - x0, y1 - y0);
            }

            static float EffectiveAlpha(Graphic g)
            {
                float a = g.color.a;
                for (Transform t = g.transform; t != null; t = t.parent)
                {
                    var cg = t.GetComponent<CanvasGroup>();
                    if (cg != null) a *= cg.alpha;
                }
                return a;
            }

            static Rect ScreenRect(RectTransform rt, Canvas canvas)
            {
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                Camera? cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                    xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                    yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
                }
                return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            }

            // ── the overlay drawn on the C3 captures ─────────────────────────

            void ShowOverlay()
            {
                if (_overlay != null) return;
                _overlay = new GameObject("__SafeAreaOverlay", typeof(Canvas), typeof(CanvasScaler));
                var c = _overlay.GetComponent<Canvas>();
                c.renderMode  = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 32000;
                Band(_overlay.transform, "TopBand",    new Vector2(0f, 1f), new Vector2(1f, 1f), _topInset, top: true);
                Band(_overlay.transform, "BottomBand", new Vector2(0f, 0f), new Vector2(1f, 0f), _botInset, top: false);
            }

            static void Band(Transform parent, string name, Vector2 aMin, Vector2 aMax, float px, bool top)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = aMin; rt.anchorMax = aMax;
                rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
                rt.sizeDelta = new Vector2(0f, px);
                rt.anchoredPosition = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.color = new Color(1f, 0.18f, 0.18f, 0.30f);
                img.raycastTarget = false;
            }

            void HideOverlay()
            {
                if (_overlay != null) { UnityEngine.Object.Destroy(_overlay); _overlay = null; }
            }

            // ═════════════════════════════════════════════════════════════════
            // §A5 — rest geometry, the 0-px claim as a number
            // ═════════════════════════════════════════════════════════════════

            static readonly ScreenId[] GeomScreens =
            {
                ScreenId.Home, ScreenId.Roster, ScreenId.Inventory, ScreenId.ModeSelection,
                ScreenId.HoleSelection, ScreenId.MissionSelection, ScreenId.TournamentSelection,
                ScreenId.TournamentLeaderboard, ScreenId.TournamentHoleSelection,
                ScreenId.Leaderboard, ScreenId.GeneralShop, ScreenId.GachaHistory, ScreenId.GachaPrizes,
            };

            IEnumerator RestGeom()
            {
                var j = new StringBuilder();
                j.AppendLine("{");
                j.AppendLine("  \"task\": \"game_polish_c\", \"kind\": \"restgeom\", \"tag\": \"" + _tag + "\",");
                j.AppendLine("  \"view\": \"" + Screen.width + "x" + Screen.height + "\",");
                j.AppendLine("  \"screens\": {");

                for (int s = 0; s < GeomScreens.Length; s++)
                {
                    ScreenId id = GeomScreens[s];
                    if (ScreenManager.Instance == null) break;
                    ScreenManager.Instance.ShowScreen(id);
                    yield return Until(() => ScreenManager.Instance!.CurrentScreen == id, 12f, id.ToString());
                    yield return new WaitForSecondsRealtime(2.5f);

                    GameObject? go = Obj(id);
                    j.AppendLine("    \"" + id + "\": {");
                    if (go != null)
                    {
                        var rows = new List<string>();
                        foreach (RectTransform rt in go.GetComponentsInChildren<RectTransform>(true))
                        {
                            if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                            var c = new Vector3[4];
                            rt.GetWorldCorners(c);
                            rows.Add("      \"" + Esc(PathOf(rt)) + "\": [" +
                                     F(c[0].x) + "," + F(c[0].y) + "," + F(c[2].x) + "," + F(c[2].y) + "]");
                        }
                        j.AppendLine(string.Join(",\n", rows));
                    }
                    j.AppendLine("    }" + (s < GeomScreens.Length - 1 ? "," : ""));
                    Line("  restgeom " + id + " captured");
                }

                j.AppendLine("  }");
                j.AppendLine("}");
                string path = $"{OutDir}/game_polish_c_restgeom_{_tag}.json";
                File.WriteAllText(path, j.ToString());
                Line("A5 -> " + path);
            }

            // ═════════════════════════════════════════════════════════════════
            // §A2 — the press is visible
            // ═════════════════════════════════════════════════════════════════

            /// <summary>
            /// Five buttons, one per pillar, that had NO feedback before this task. Each is driven
            /// through the component's own <c>OnPointerDown</c> — the same entry point the
            /// EventSystem uses — and its localScale sampled every frame of the pulse. The
            /// evidence is the sampled minimum: 0.95 of rest.
            /// </summary>
            IEnumerator Press()
            {
                foreach ((ScreenId screen, string path) in PressSites)
                {
                    yield return Ensure(screen);
                    // The Settings list lives on an OVERLAY, not a screen: it is reached by the
                    // real gear button, like a player does.
                    bool settings = path.StartsWith("SettingsScreen");
                    if (settings) yield return SettingsOpen();
                    yield return new WaitForSecondsRealtime(1.2f);

                    Transform? t = ByPath(path);
                    var fb = t != null ? t.GetComponent<ButtonPressFeedback>() : null;
                    if (fb == null)
                    {
                        _pressLog.Add($"{path}: {(t == null ? "NOT FOUND" : "NO ButtonPressFeedback")}");
                        Line("  press: " + path + (t == null ? " not found" : " has no feedback component"));
                        if (settings) yield return SettingsClose();
                        continue;
                    }

                    var btn = t!.GetComponent<Button>();
                    bool interactable = btn == null || btn.interactable;
                    float rest = t.localScale.x, min = rest;
                    fb.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(
                        UnityEngine.EventSystems.EventSystem.current));
                    float end = Time.realtimeSinceStartup + 0.5f;
                    bool shot = false;
                    while (Time.realtimeSinceStartup < end)
                    {
                        min = Mathf.Min(min, t.localScale.x);
                        if (!shot && t.localScale.x <= rest * 0.97f)
                        {
                            shot = true;
                            yield return Shot("press_" + t.name, null);
                        }
                        else yield return null;
                    }
                    float settled = t.localScale.x;
                    // 0.95 of rest at the trough, and back to rest when it ends. The tolerance is
                    // one frame's worth of the pulse (0.12 s total, so ~0.008 of scale per frame
                    // at 60 fps) — a sampler cannot be expected to land exactly on the minimum.
                    string verdict = (min <= rest * 0.962f && min >= rest * 0.938f &&
                                      Mathf.Abs(settled - rest) < 0.002f) ? "PASS" : "FAIL";
                    _pressLog.Add($"{screen} {path}\n    rest={rest:0.####} trough={min:0.####} " +
                                  $"(expected {rest * 0.95f:0.####}) settled={settled:0.####} " +
                                  $"interactable={interactable}  {verdict}" +
                                  (verdict == "FAIL" && !interactable
                                     ? "  <- the button is NOT interactable; ButtonPressFeedback " +
                                       "declines to pulse a button the player cannot press"
                                     : ""));
                    Line("  press " + t.name + ": trough=" + min.ToString("0.####") +
                         " settled=" + settled.ToString("0.####") + " " + verdict);
                    if (settings) yield return SettingsClose();
                }

                                File.WriteAllText($"{OutDir}/game_polish_c_press.txt", string.Join("\n", _pressLog) + "\n");
                Line("A2 -> " + OutDir + "/game_polish_c_press.txt");
            }

            // ═════════════════════════════════════════════════════════════════
            // §A6 — the toast fade, sampled off the RUNNING coroutine
            // ═════════════════════════════════════════════════════════════════

            /// <summary>
            /// Drive the real <c>ToastController.Show()</c> — the public API every caller uses —
            /// and read <c>_canvasGroup.alpha</c> every frame.
            ///
            /// <para>WHY THIS EXISTS ALONGSIDE THE UNIT TEST. <c>ToastFadeParityTests</c>
            /// transcribes <c>UiMotion.FadeRoutine</c>'s arithmetic; it proves the CURVE is
            /// ease-out but it never touches <c>ToastController</c>'s coroutine, so it would keep
            /// passing if the call site were wired to something else entirely. This samples the
            /// alpha a player would actually see and fits it against
            /// <c>Lerp(from, to, EaseOut(t))</c> — evidence from the shipped path rather than a
            /// restatement of the primitive.</para>
            ///
            /// <para>The clock is NOT fixed here, so <c>t</c> is reconstructed from accumulated
            /// unscaled time rather than from the frame index; an Editor hitch mid-fade would
            /// otherwise read as a curve error.</para>
            /// </summary>
            /// <remarks>Named <c>ToastFade</c>, not <c>Toast</c>: from inside
            /// <c>Golfin.UI.Polish.EditorTools</c> the bare identifier <c>Toast</c> binds to the
            /// <c>Golfin.UI.Toast</c> NAMESPACE and the call site will not compile.</remarks>
            IEnumerator ToastFade()
            {
                var toast = Golfin.UI.Toast.ToastController.Instance;
                if (toast == null) { Line("FAIL: no ToastController.Instance in the running shell"); yield break; }

                Type tc = typeof(Golfin.UI.Toast.ToastController);
                var cg = tc.GetField("_canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic)
                           ?.GetValue(toast) as CanvasGroup;
                var inField = tc.GetField("_fadeIn", BindingFlags.Instance | BindingFlags.NonPublic);
                float dur = inField != null ? (float)inField.GetValue(toast)! : 0.3f;
                if (cg == null) { Line("FAIL: ToastController has no CanvasGroup wired"); yield break; }

                Line($"toast fade-in: duration {dur:0.###}s, sampling alpha every frame");
                toast.Show("GAME POLISH C - TOAST FADE", 2.5f);

                var samples = new List<(float T, float Alpha)>();
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 <= dur + 0.05f)
                {
                    samples.Add((Time.unscaledTime - t0, cg.alpha));
                    yield return null;
                }

                // THE SAMPLER'S t0 IS NOT THE FADE'S t0, and assuming it was made the first run of
                // this probe report FAIL on a fade that is demonstrably eased. `Show()` starts a
                // coroutine that runs its first step within the same frame, so by the time this
                // loop takes its first reading the alpha has already moved — it read 0.1886 at
                // what it called t = 0. Fitting against a fixed offset then measures the
                // interleaving of two coroutines, not the curve.
                //
                // So the offset is FITTED: for each candidate shift the worst residual is computed
                // against both curves, and each curve keeps its best. Whichever fits better is the
                // curve the app is running, and the shift that achieved it is reported so a reader
                // can see it is about one frame rather than something suspicious.
                float worstEase = float.MaxValue, worstLinear = float.MaxValue, bestShift = 0f;
                for (float shift = 0f; shift <= 0.05f; shift += 0.001f)
                {
                    float e = 0f, l = 0f;
                    foreach ((float t, float a) in samples)
                    {
                        float u = Mathf.Clamp01((t + shift) / dur);
                        e = Mathf.Max(e, Mathf.Abs(a - UiMotion.EaseOut(u)));
                        l = Mathf.Max(l, Mathf.Abs(a - u));
                    }
                    if (e < worstEase) { worstEase = e; bestShift = shift; }
                    if (l < worstLinear) worstLinear = l;
                }
                bool eased = worstEase < worstLinear && worstEase < 0.06f;

                var sb = new StringBuilder();
                sb.AppendLine("# game_polish_c A6 - toast fade sampled off the running ToastController");
                sb.AppendLine($"# duration {dur:0.###}s, {samples.Count} frames, unfixed clock");
                sb.AppendLine($"# best-fit start offset       = {bestShift:0.####}s " +
                              $"(~{bestShift * 60f:0.#} frames at 60 fps; the sampler cannot see the " +
                              "coroutine's own t0)");
                sb.AppendLine($"# worst |alpha - easeOut(t)| = {worstEase:0.####}   <- best fit");
                sb.AppendLine($"# worst |alpha - linear(t)|  = {worstLinear:0.####}");
                sb.AppendLine($"# verdict: the shipped fade is {(eased ? "EASE-OUT" : "NOT ease-out")} " +
                              $"-> {(eased ? "PASS" : "FAIL")}");
                sb.AppendLine("t\talpha\teaseOut(t+shift)\tlinear(t+shift)");
                foreach ((float t, float a) in samples)
                {
                    float u = Mathf.Clamp01((t + bestShift) / dur);
                    sb.AppendLine($"{t:0.####}\t{a:0.####}\t{UiMotion.EaseOut(u):0.####}\t{u:0.####}");
                }
                File.WriteAllText($"{OutDir}/game_polish_c_toast_fade.txt", sb.ToString());
                Line($"A6 -> {OutDir}/game_polish_c_toast_fade.txt  worstEase={worstEase:0.####} " +
                     $"worstLinear={worstLinear:0.####}  {(eased ? "PASS (eased)" : "FAIL")}");

                yield return Shot("toast_midfade", null);
            }

            /// <summary>
            /// One per pillar, and every one of the five is a row the §C1 table marks **added** —
            /// they had no ButtonPressFeedback at HEAD. Addressed by FULL PATH, not by name:
            /// "LevelUpButton" alone matches four different buttons in this shell and A2 would
            /// then be evidence about whichever one FindObjectsByType happened to return first.
            /// </summary>
            static readonly (ScreenId Screen, string Path)[] PressSites =
            {
                (ScreenId.Roster,           "Canvas/ScreensRoot/RosterScreen/DetailPanel/RightPanel/ButtonsPanel/LevelUpButton"),
                (ScreenId.Inventory,        "Canvas/ScreensRoot/InventoryScreen/TabBar/BAGSTab"),
                (ScreenId.GeneralShop,      "Canvas/ScreensRoot/GeneralShopScreen/ContentArea/BarsArea/TabBar/DailyTab"),
                // NOT MissionSelection's Tab_PRO, which was the first choice and measured
                // trough = 1.00: the selected difficulty tab is non-interactable, and
                // ButtonPressFeedback declines to pulse a button the player cannot press. Correct
                // behaviour, useless evidence — so the site is a filter pill that IS pressable.
                (ScreenId.HoleSelection,    "Canvas/ScreensRoot/HoleSelectionScreen/Content/Filters/FilterRow1/Pill_LOMOND_28_72"),
                (ScreenId.Home,             "SettingsScreen/SettingsPanel/SettingsList/AboutRow"),
            };

            // ═════════════════════════════════════════════════════════════════
            // JSON
            // ═════════════════════════════════════════════════════════════════

            void WriteSweep()
            {
                // ── buttons ──
                var paths = new List<string>(_buttons.Keys);
                paths.Sort(StringComparer.Ordinal);
                int defects = 0, covered = 0, excluded = 0;
                var jb = new StringBuilder();
                jb.AppendLine("{");
                jb.AppendLine("  \"task\": \"game_polish_c\", \"table\": \"C1-buttons\", \"tag\": \"" + _tag + "\",");
                jb.AppendLine("  \"utc\": \"" + DateTime.UtcNow.ToString("u") + "\",");
                jb.AppendLine("  \"buttons\": [");
                for (int i = 0; i < paths.Count; i++)
                {
                    BtnRow r = _buttons[paths[i]];
                    bool playerFacing = r.Exclusion == "";
                    if (!playerFacing) excluded++;
                    else if (r.HasFeedback) covered++;
                    else defects++;
                    jb.AppendLine("    {\"path\": \"" + Esc(r.Path) + "\", \"prefab\": \"" + Esc(r.Prefab) +
                                  "\", \"firstSeen\": \"" + Esc(r.FirstSeen) + "\", \"active\": " + B(r.EverActive) +
                                  ", \"interactable\": " + B(r.EverInteractable) + ", \"hasFeedback\": " + B(r.HasFeedback) +
                                  ", \"playerFacing\": " + B(playerFacing) + ", \"exclusionReason\": \"" + Esc(r.Exclusion) +
                                  "\"}" + (i < paths.Count - 1 ? "," : ""));
                }
                jb.AppendLine("  ],");
                jb.AppendLine("  \"total\": " + paths.Count + ", \"playerFacing\": " + (covered + defects) +
                              ", \"covered\": " + covered + ", \"excluded\": " + excluded +
                              ", \"defects\": " + defects);
                jb.AppendLine("}");
                Write($"{OutDir}/game_polish_c_buttons_{_tag}.json", jb.ToString());
                Line($"C1 -> buttons total={paths.Count} playerFacing={covered + defects} covered={covered} " +
                     $"defects={defects} excluded={excluded}");

                // ── scroll rects ──
                var sp = new List<string>(_scrolls.Keys);
                sp.Sort(StringComparer.Ordinal);
                var js = new StringBuilder();
                js.AppendLine("{");
                js.AppendLine("  \"task\": \"game_polish_c\", \"table\": \"C2-scrollrects\", \"tag\": \"" + _tag + "\",");
                js.AppendLine("  \"reference\": {\"movementType\": \"Elastic\", \"elasticity\": 0.1, \"inertia\": true, " +
                              "\"decelerationRate\": 0.135, \"scrollSensitivity\": 20},");
                js.AppendLine("  \"scrollRects\": [");
                int conform = 0, off = 0, sexcl = 0;
                for (int i = 0; i < sp.Count; i++)
                {
                    SrRow r = _scrolls[sp[i]];
                    bool inScope = r.Exclusion == "";
                    bool ok = r.MovementType == "Elastic" && Mathf.Approximately(r.Elasticity, 0.1f) && r.Inertia &&
                              Mathf.Approximately(r.Deceleration, 0.135f) && Mathf.Approximately(r.Sensitivity, 20f);
                    if (!inScope) sexcl++; else if (ok) conform++; else off++;
                    js.AppendLine("    {\"path\": \"" + Esc(r.Path) + "\", \"prefab\": \"" + Esc(r.Prefab) +
                                  "\", \"surface\": \"" + Esc(r.Surface) + "\", \"controller\": \"" + Esc(r.Owner) +
                                  "\", \"movementType\": \"" + r.MovementType + "\", \"elasticity\": " + F(r.Elasticity) +
                                  ", \"inertia\": " + B(r.Inertia) + ", \"decelerationRate\": " + F(r.Deceleration) +
                                  ", \"scrollSensitivity\": " + F(r.Sensitivity) + ", \"horizontal\": " + B(r.Horizontal) +
                                  ", \"vertical\": " + B(r.Vertical) + ", \"inScope\": " + B(inScope) +
                                  ", \"conforms\": " + B(ok) + ", \"exclusionReason\": \"" + Esc(r.Exclusion) + "\"}" +
                                  (i < sp.Count - 1 ? "," : ""));
                }
                js.AppendLine("  ],");
                js.AppendLine("  \"total\": " + sp.Count + ", \"inScope\": " + (conform + off) +
                              ", \"conforming\": " + conform + ", \"offReference\": " + off + ", \"excluded\": " + sexcl);
                js.AppendLine("}");
                Write($"{OutDir}/game_polish_c_scrollrects_{_tag}.json", js.ToString());
                Line($"C2 -> scrollRects total={sp.Count} inScope={conform + off} conforming={conform} " +
                     $"offReference={off} excluded={sexcl}");

                // ── safe area ──
                var ja = new StringBuilder();
                int hits = 0;
                ja.AppendLine("{");
                ja.AppendLine("  \"task\": \"game_polish_c\", \"table\": \"C3-safearea\", \"tag\": \"" + _tag + "\",");
                ja.AppendLine("  \"device\": \"Apple iPhone 15 Pro Max\", \"deviceRes\": \"" + DeviceW + "x" + DeviceH + "\",");
                ja.AppendLine("  \"view\": \"" + Screen.width + "x" + Screen.height + "\", \"safeAreaSource\": \"" + _safeSource + "\",");
                ja.AppendLine("  \"topInsetPx\": " + F(_topInset) + ", \"bottomInsetPx\": " + F(_botInset) + ",");
                ja.AppendLine("  \"surfaces\": [");
                int notMeasured = 0;
                for (int i = 0; i < _safe.Count; i++)
                {
                    SafeRow r = _safe[i];
                    if (r.Verdict.StartsWith("NOT MEASURED")) notMeasured++;
                    else if (r.Verdict != "clear") hits++;
                    ja.AppendLine("    {\"surface\": \"" + Esc(r.Surface) + "\", \"verdict\": \"" + Esc(r.Verdict) +
                                  "\", \"measuredView\": \"" + Esc(r.View) + "\", \"safeAreaSource\": \"" +
                                  Esc(r.Source) + "\", \"shot\": \"" + Esc(r.Shot) + "\",");
                    ja.AppendLine("     \"topHits\": [" + Join(r.TopHits) + "],");
                    ja.AppendLine("     \"bottomHits\": [" + Join(r.BotHits) + "]}" + (i < _safe.Count - 1 ? "," : ""));
                }
                ja.AppendLine("  ],");
                ja.AppendLine("  \"surfaceCount\": " + _safe.Count + ", \"clear\": " +
                              (_safe.Count - hits - notMeasured) + ", \"hits\": " + hits +
                              ", \"notMeasured\": " + notMeasured);
                ja.AppendLine("}");
                Write($"{OutDir}/game_polish_c_safearea_{_tag}.json", ja.ToString());
                Line($"C3 -> surfaces={_safe.Count} clear={_safe.Count - hits - notMeasured} " +
                     $"hits={hits} notMeasured={notMeasured}");
            }

            static string Join(List<string> xs)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < xs.Count; i++) sb.Append((i > 0 ? ", " : "") + "\"" + Esc(xs[i]) + "\"");
                return sb.ToString();
            }

            void Write(string path, string body)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, body);
            }

            // ═════════════════════════════════════════════════════════════════
            // Navigation plumbing (a's, trimmed to what this sweep needs)
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Boot()
            {
                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");
                yield return TapStart();
                yield return Ensure(ScreenId.Home);
                yield return new WaitForSecondsRealtime(1.5f);
            }

            IEnumerator TapStart()
            {
                float deadline = Time.realtimeSinceStartup + 90f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(
                                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (b.name != "StartButton" || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping the real StartButton");
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(2f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: no StartButton appeared in 90 s");
            }

            IEnumerator NavSlot(string button, ScreenId target, string what)
            {
                Transform? t = FindByName(button);
                if (t == null) { Line("WARN: " + what + " — no '" + button + "'"); yield break; }
                var b = t.GetComponent<Button>();
                if (b == null) { Line("WARN: " + what + " — '" + button + "' has no Button"); yield break; }
                Line("tapping the real " + button + " -> " + target);
                b.onClick.Invoke();
                yield return Arrive(target, 1.5f);
            }

            IEnumerator TapPath(ScreenId from, string button, ScreenId target)
            {
                yield return Ensure(from);
                Transform? t = FindByName(button);
                if (t == null) { Line("WARN: no '" + button + "' on " + from); yield break; }
                var b = t.GetComponent<Button>();
                if (b == null) yield break;
                Line("tapping the real " + button + " " + from + " -> " + target);
                b.onClick.Invoke();
                yield return Arrive(target, 1.5f);
            }

            IEnumerator ModeCardPlay(ScreenId target)
            {
                string want = target == ScreenId.HoleSelection
                    ? GolfinRedux.UI.ModeSelect.ModeSelectScreenController.TargetHoleSelect
                    : GolfinRedux.UI.ModeSelect.ModeSelectScreenController.TargetMissionSelect;

                yield return Ensure(ScreenId.ModeSelection);
                GameObject? modeGo = Obj(ScreenId.ModeSelection);
                var sr = modeGo != null ? modeGo.GetComponentInChildren<ScrollRect>(true) : null;
                Transform? content = sr != null ? sr.content : null;
                if (content == null) { Line("WARN: no mode-card content"); yield break; }

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
                if (chosen == null) { Line("WARN: no card routes to '" + want + "'"); yield break; }

                var cc = chosen.GetComponent<GolfinRedux.UI.ModeSelect.ModeCardController>();
                if (cc != null && cc.State != GolfinRedux.UI.ModeSelect.ModeCardState.Expanded)
                {
                    var tap = chosen.Find("CardTapButton")?.GetComponent<Button>() ?? chosen.GetComponent<Button>();
                    if (tap != null && tap.interactable) tap.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.8f);
                }
                var play = chosen.Find("ExpandedContainer/ActionButton")?.GetComponent<Button>();
                if (play == null || !play.gameObject.activeInHierarchy) { Line("WARN: no ActionButton"); yield break; }
                Line("tapping the real ActionButton on '" + chosen.name + "' -> " + target);
                play.onClick.Invoke();
                yield return Arrive(target, 2f);
            }

            IEnumerator InventoryTab(int index)
            {
                GameObject? inv = Obj(ScreenId.Inventory);
                if (inv == null) yield break;
                Button? tab = null;
                foreach (Button b in inv.GetComponentsInChildren<Button>(true))
                    if (b.transform.parent != null && b.transform.parent.name == "TabBar" &&
                        b.transform.GetSiblingIndex() == index && b.gameObject.activeInHierarchy)
                    { tab = b; break; }
                if (tab == null)
                {
                    var ctrl = inv.GetComponentInChildren<Golfin.Inventory.InventoryScreenController>(true);
                    Line("WARN: inventory tab " + index + " has no button; ShowTab (NOT a tap)");
                    if (ctrl != null) ctrl.ShowTab(index);
                }
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
                    Line("WARN: no SettingsButton widget; OpenSettings() (NOT a tap)");
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

            IEnumerator Force(ScreenId target)
            {
                Line("note: " + target + " — no player path reachable from this harness; " +
                     "ShowScreen() for the rest-state audit only (NOT a tap)");
                ScreenManager.Instance?.ShowScreen(target);
                yield return Arrive(target, 2f);
            }

            IEnumerator Ensure(ScreenId id)
            {
                if (ScreenManager.Instance == null) yield break;
                if (ScreenManager.Instance.CurrentScreen == id) yield break;
                Line("note: route is on " + ScreenManager.Instance.CurrentScreen + ", expected " + id +
                     " — re-seating via ShowScreen (NOT a tap)");
                ScreenManager.Instance.ShowScreen(id);
                yield return Arrive(id, 1.5f);
            }

            IEnumerator Arrive(ScreenId id, float settle)
            {
                yield return Until(() => ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen == id,
                                   20f, id.ToString());
                yield return new WaitForSecondsRealtime(settle);
            }

            IEnumerator Until(Func<bool> done, float seconds, string what)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
                Line((done() ? "ok   " : "TIMEOUT ") + what);
            }

            /// <summary>Resolve a full scene path, inactive objects included.</summary>
            static Transform? ByPath(string path)
            {
                int slash = path.IndexOf('/');
                string rootName = slash < 0 ? path : path.Substring(0, slash);
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager
                             .GetActiveScene().GetRootGameObjects())
                {
                    if (root.name != rootName) continue;
                    if (slash < 0) return root.transform;
                    Transform? t = root.transform.Find(path.Substring(slash + 1));
                    if (t != null) return t;
                }
                return null;
            }

            static Transform? FindByName(string name)
            {
                foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (b.name == name && b.gameObject.activeInHierarchy) return b.transform;
                return null;
            }

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
                    ScreenId.StaminaShopSelection    => "StaminaShopSelectionScreen",
                    _                                => null,
                };
                return name == null ? null : GameObject.Find("Canvas/ScreensRoot/" + name);
            }

            // ── capture ──────────────────────────────────────────────────────

            string _lastHash = "";

            IEnumerator Shot(string label, SafeRow? row)
            {
                _shot++;
                string actual = ScreenManager.Instance != null ? ScreenManager.Instance.CurrentScreen.ToString() : "unknown";
                string name = string.Format("{0}_{1:00}_{2}__{3}", _tag, _shot, label, actual);
                string path = Path.Combine(ShotDir, name + ".png");
                Directory.CreateDirectory(ShotDir);

                IEnumerator snap = CaptureCore.SnapAtEndOfFrameAndPause(name, path, skipPause: true);
                while (snap.MoveNext()) yield return snap.Current;

                if (!File.Exists(path)) { Line("SHOT " + label + " -> MISSING"); yield break; }
                if (row != null) row.Shot = name + ".png";

                string hash = Md5(path);
                bool stale = hash == _lastHash;
                _lastHash = hash;
                Line("SHOT " + label + " [screen=" + actual + "] -> " + path +
                     " (" + new FileInfo(path).Length / 1024 + " KB)" +
                     (stale ? "   *** STALE: byte-identical to the previous capture ***" : ""));
            }

            static string Md5(string path)
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                using (var fs = File.OpenRead(path))
                    return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "");
            }

            // ── plumbing ─────────────────────────────────────────────────────

            /// <summary>The root canvas rect, for the full-screen-catcher rule. Screen.width lies
            /// in play mode (project memory: screen_width_lies_in_editor_playmode), so this reads
            /// the canvas rect the UI is actually laid out in.</summary>
            static Vector2 _canvasSize;
            static Vector2 CanvasSize()
            {
                if (_canvasSize.x > 1f) return _canvasSize;
                foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c != null && c.isRootCanvas && c.transform is RectTransform rt && rt.rect.width > 1f)
                    { _canvasSize = rt.rect.size; break; }
                if (_canvasSize.x <= 1f) _canvasSize = new Vector2(1170f, 2532f);
                return _canvasSize;
            }

            public static string PathOf(Transform t)
            {
                var sb = new StringBuilder(t.name);
                for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
                return sb.ToString();
            }

            /// <summary>
            /// PROVENANCE CANNOT BE READ AT RUNTIME, so this does not pretend to.
            /// <c>PrefabUtility.IsPartOfPrefabInstance</c> returns false for EVERY object in play
            /// mode (project memory: playmode_hides_prefab_instance) — a first version of this
            /// method used it and labelled all 511 buttons "(scene)", which is a confident lie
            /// rather than a missing value. What IS knowable at runtime is whether the object was
            /// spawned from a prefab, because Unity names the clone; the authoring-side table
            /// (game_polish_c_pressfeedback_authoring.tsv, written by the builder in EDIT mode)
            /// carries the real asset paths.
            /// </summary>
            static string PrefabOf(Component c)
            {
                for (Transform t = c.transform; t != null; t = t.parent)
                    if (t.name.EndsWith("(Clone)", StringComparison.Ordinal))
                        return "clone of " + t.name.Substring(0, t.name.Length - 7);
                return "authored (see the builder TSV)";
            }

            static string F(float v) => float.IsNaN(v) ? "null"
                : v.ToString("0.####", CultureInfo.InvariantCulture);
            static string B(bool v) => v ? "true" : "false";
            static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

            void Line(string s)
            {
                _log.AppendLine(s);
                Debug.Log("[GAME-POLISH-C] " + s);
                File.WriteAllText(LogPath(_mode, _tag), _log.ToString());
            }
        }
    }
}
