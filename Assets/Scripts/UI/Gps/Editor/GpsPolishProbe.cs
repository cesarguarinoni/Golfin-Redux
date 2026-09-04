// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §A1 / §A2 — the gate.
//
// THE JSON IS THE GATE; THE VIDEO IS THE ARTIFACT. A push is 15 frames long, and
// "it looked right" is not a measurement of a 15-frame event. This probe drives
// every pushable transition through the REAL widgets a player taps, samples the
// two content rects and the four chrome CanvasGroups on EVERY frame of every
// push, and writes a per-assertion PASS/FAIL file. fail == 0 is the gate.
//
// REAL NAVIGATION (PIPELINE_HARDENING rule 2). Boot -> tap the real StartButton
// -> Home -> the Home GPS pill -> the hub's own nav slots and the profile's own
// shortcut buttons. Nothing here calls ShowScreen to reach a screen a player
// reaches by tapping.
//
// THREE MODES, and the order they are run in is the argument:
//   baseline  motion OFF, so every navigation takes the untouched fade path and
//             every capture is the screen exactly as HEAD draws it. Run BEFORE
//             the prefab polish pass.
//   polished  same, after the polish pass. baseline vs polished proves the
//             CanvasGroups / safe-area wrapper / step marker moved no rest pixel.
//   push      motion ON. polished vs push is A2: a rest state reached through
//             the animation against the same rest state reached instantly.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.Gps.UI;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.EditorTools
{
    public static class GpsPolishProbe
    {
        const string ArmedKey = "gps_polish.probe.armed";
        const string ModeKey  = "gps_polish.probe.mode";

        const string TaskDir  = "Docs/Specs/Active/gps_polish";
        const string ShotDir  = TaskDir + "/screenshots";
        const string LogPath  = "Docs/Diagnostics/_capture/gps_polish_run.log";
        const string JsonPath = "Docs/Diagnostics/_capture/gps_polish_invariants.json";

        /// <summary>Frame budget for the measured-duration assertion (SPEC A1: "within ±2
        /// frames"). 2 frames at 60 fps, with a little slack for an Editor hitch.</summary>
        const float DurationToleranceSec = 2f / 60f + 0.02f;

        [MenuItem("GOLFIN/Gps/Polish Probe — baseline (motion off, pre-polish)", priority = 240)]
        public static void ArmBaseline() => Arm("baseline");

        [MenuItem("GOLFIN/Gps/Polish Probe — polished (motion off, post-polish)", priority = 241)]
        public static void ArmPolished() => Arm("polished");

        [MenuItem("GOLFIN/Gps/Polish Probe — push (motion on, writes invariants)", priority = 242)]
        public static void ArmPush() => Arm("push");

        /// <summary>
        /// A2, WITHIN ONE RUN. The route is walked twice in a single play session — once with
        /// motion ON (every GPS move is a push) and once with it OFF (every move falls through to
        /// the untouched fade, which is the `instant` arrival the SPEC asks to compare against) —
        /// and each screen is captured on both passes.
        ///
        /// <para>WHY WITHIN ONE RUN, and this is the whole reason the mode exists: these screens
        /// render LIVE data and RELATIVE time. Comparing a capture taken now against one taken an
        /// hour ago diffs "2h ago" against "3h ago", a moved RP balance and a ticking clock in the
        /// shared top bar, and reports tens of thousands of differing pixels that have nothing to
        /// do with the animation. Forty seconds apart, in one session, none of that moves.</para>
        /// </summary>
        [MenuItem("GOLFIN/Gps/Polish Probe — parity (A2: animated vs instant, one run)", priority = 243)]
        public static void ArmParity() => Arm("parity");

        /// <summary>
        /// A13, and it is a SEPARATE PASS on purpose.
        ///
        /// <para>The first attempt sampled the profiler counters during the `push` run, and it
        /// broke that run's own gate: turning the Editor profiler on cost one frame of the
        /// GpsVote→GpsHub push 392 ms, which stretched a 0.25 s tween to 0.410 s and failed A1's
        /// duration assertion. The instrument changed the thing it was measuring — the same
        /// lesson as iter-1's probe bug, wearing a different hat. So A1 runs with the profiler
        /// OFF and this mode runs with it ON, and this mode also takes NO SCREENSHOTS: a
        /// 1170x2532 ReadPixels + PNG encode allocates ~100 MB, which swamped every push it
        /// happened to sit next to.</para>
        /// </summary>
        [MenuItem("GOLFIN/Gps/Polish Probe — perf (A13: GC + frame ms, no captures)", priority = 244)]
        public static void ArmPerf() => Arm("perf");

        /// <summary>
        /// A8 — one frame per shimmer site, taken WHILE the placeholder is up.
        ///
        /// <para>Sampling a video for these frames does not work and it is worth saying why: the
        /// cold window is the gap between a screen activating and its fetch answering, which
        /// against this server is 120–260 ms — three to eight frames at 30 fps. Seven timestamps
        /// 200 ms apart across the gift screen's cold window all decoded to the same settled
        /// frame. So this mode does not sample: it polls the site's own <c>ShimmerHost</c> every
        /// frame and captures on the first one where it is genuinely active, and LOGS that fact
        /// beside the file name — the still is provably a cold frame rather than a hopeful one.</para>
        /// </summary>
        [MenuItem("GOLFIN/Gps/Polish Probe — shimmer (A8: a cold frame per site)", priority = 245)]
        public static void ArmShimmer() => Arm("shimmer");

        /// <summary>
        /// The lit nav slot, per screen, as RENDERED COLOUR rather than as a mapping table.
        ///
        /// <para>`GpsNavBarHighlightTests` already pins which slot SHOULD light; this pins that the
        /// pixel actually changed on the real screen — the two failure modes it catches are a
        /// component that never ran (OnEnable not reached on a cloned bar) and a tint that the
        /// Button's own ColorTint transition swallowed.</para>
        /// </summary>
        [MenuItem("GOLFIN/Gps/Polish Probe — nav tint (the lit slot, per screen)", priority = 246)]
        public static void ArmNavTint() => Arm("navtint");

        /// <summary>
        /// The BUY GIFT ITEMS strip, read as TEXT off the live screen.
        ///
        /// <para>gift_items carries one Japanese `name` column, so an English build rendered
        /// Japanese. A screenshot can show the strip is populated; only the string can show WHICH
        /// language it is in, which is why this asserts on the glyphs rather than on the frame.</para>
        /// </summary>
        [MenuItem("GOLFIN/Gps/Polish Probe — gift item names (EN vs JA)", priority = 247)]
        public static void ArmGift() => Arm("gift");

        /// <summary>
        /// The GPS nav bar measured against the GAME nav bar, in one run.
        ///
        /// <para>Both draw `Bottom Bar Background.png` at Image.Type.Simple, so the ONLY thing that
        /// can make them look different is the rect they are drawn into. Cesar's report was
        /// "stretched and does not match game"; this reads both rects rather than comparing two
        /// screenshots by eye.</para>
        /// </summary>
        [MenuItem("GOLFIN/Gps/Polish Probe — nav bar vs the Game bar", priority = 248)]
        public static void ArmNavBar() => Arm("navbar");

        public static void Arm(string mode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(ShotDir);
            File.WriteAllText(LogPath, "");
            EditorPrefs.SetString(ModeKey, mode);
            EditorPrefs.SetBool(ArmedKey, true);

            if (EditorApplication.isPlaying) { Spawn(); return; }

            // ENTER PLAY MODE ONLY WHEN THE EDITOR IS IDLE, and this is a scar, not caution.
            // Calling EnterPlaymode() straight after an AssetDatabase refresh — which every code
            // change causes — starts a play session on a HALF-RESTORED scene: ShellScene came up
            // with 11 of its 25 roots, ScreenManager's GameObject existed but its Awake never ran,
            // and the probe sat for 30 s timing out on a boot that had not happened. It cost three
            // wasted runs before the pattern (always the FIRST arm after a recompile) was visible.
            // Same shape as the delayCall-races-scene-restore scar: poll EditorApplication.update
            // until it is quiet, never fire a one-shot into the middle of a reload.
            EditorApplication.update -= EnterWhenIdle;
            EditorApplication.update += EnterWhenIdle;
        }

        static void EnterWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // …AND the scene must be FULLY restored, not merely open. The compile/update flags
            // clear before the hierarchy is rebuilt, and a play session started in that gap comes
            // up with a PARTIAL scene — ShellScene arrived with 11 of its 25 roots, ScreenManager's
            // GameObject present but its Awake never run, and the probe timed out on a boot that
            // had not happened. The root count is the cheapest honest proxy for "the scene is
            // really back".
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
            var go = new GameObject("__GpsPolishProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        // ═════════════════════════════════════════════════════════════════════

        /// <summary>One measured push, with every assertion the SPEC names.</summary>
        sealed class Record
        {
            public string From = "", To = "", Direction = "";
            public float  W, ExpectedDur, MeasuredDur;
            public float  TargetOffsetAtT0, EndTargetX, EndTargetRestX, EndLeaverX, EndLeaverRestX;
            public float  EndTargetChromeAlphaMin = 1f, EndLeaverChromeAlphaMin = 1f;
            public float  EndTargetContentAlpha = 1f, EndLeaverContentAlpha = 1f;
            public bool   EndBlocksRaycasts;
            public bool   Completed;
            public float  SeamWorstCover = 1f;   // min over frames of max(bgFrom, bgTo)
            public int    Frames;
            public readonly List<string> Fails = new List<string>();
        }

        public sealed class Driver : MonoBehaviour
        {
            readonly StringBuilder _log = new StringBuilder();
            readonly List<Record>  _records = new List<Record>();
            string _mode = "push";

            /// <summary>What the capture files are named after. Normally the mode; the parity mode
            /// walks the route twice and renames the pass instead.</summary>
            string _shotPrefix = "push";
            int _shot;

            // ── A13 · the perf counters ──────────────────────────────────────
            // Sampled ONLY inside the push loop, so what they measure is the push and not the
            // screen's own first-activation frame or the fetches behind it.
            Unity.Profiling.ProfilerRecorder _gcRec;
            Unity.Profiling.ProfilerRecorder _frameRec;
            bool _perf;
            readonly List<(string pair, long allocBytes, double worstMs, int frames)> _perfRows
                = new List<(string, long, double, int)>();

            void Start()
            {
                // Without this the Editor stops rendering the moment it loses focus and every
                // capture comes back as whatever it drew last — the splash, usually.
                Application.runInBackground = true;
                _mode = EditorPrefs.GetString(ModeKey, "push");
                _shotPrefix = _mode;

                // MIRROR THE PAINT DECISIONS INTO THIS RUN'S OWN LOG. The Editor console keeps
                // only the last ~100 entries and a full route emits thousands, so the
                // paint(cache)/paint(fetch) and [Shimmer] lines — which ARE the acceptance
                // evidence for R1 and R5 — were being trimmed away before they could be read.
                // A log that is gone by the time you look at it is not evidence.
                Application.logMessageReceived += OnLog;

                // A13 — real counters, not a code-reading claim. "GC Allocated In Frame" is bytes
                // the managed heap took THIS frame; "Main Thread" is that frame's wall time in ns.
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

            void OnLog(string message, string stack, LogType type)
            {
                if (message == null) return;
                if (message.StartsWith("[Shimmer]") || message.Contains(" paint(")) Line("    " + message);
            }

            void OnDestroy()
            {
                Application.logMessageReceived -= OnLog;
                if (_gcRec.Valid)    _gcRec.Dispose();
                if (_frameRec.Valid) _frameRec.Dispose();
            }

            IEnumerator Run()
            {
                Line("=== gps_polish probe (" + _mode + ") " + DateTime.UtcNow.ToString("u") + " ===");

                if (_mode == "navbar")
                {
                    yield return Boot();
                    yield return SequenceNavBar();
                    Line("=== done: navbar pass complete ===");
                    yield break;
                }

                if (_mode == "gift")
                {
                    yield return Boot();
                    yield return SequenceGift();
                    Line("=== done: gift pass complete ===");
                    yield break;
                }

                if (_mode == "navtint")
                {
                    yield return Boot();
                    yield return SequenceNavTint();
                    Line("=== done: navtint pass complete ===");
                    yield break;
                }

                if (_mode == "shimmer")
                {
                    yield return Boot();
                    yield return SequenceShimmer();
                    Line("=== done: shimmer pass complete ===");
                    yield break;
                }

                if (_mode == "parity")
                {
                    // Pass 1: motion ON — every GPS move is a push.
                    UiMotion.Enabled = true;
                    _shotPrefix = "parity_anim";
                    _shot = 0;
                    Line("--- pass 1: UiMotion.Enabled = true (animated arrivals) ---");
                    yield return Boot();
                    yield return Route();

                    // Pass 2: motion OFF — CanPush is false everywhere, so every arrival is the
                    // untouched boundary fade, which is the instant path A2 compares against.
                    UiMotion.Enabled = false;
                    _shotPrefix = "parity_instant";
                    _shot = 0;
                    Line("--- pass 2: UiMotion.Enabled = false (instant arrivals) ---");
                    yield return Route();

                    UiMotion.Enabled = true;
                    Line("=== done: parity pass complete ===");
                    yield break;
                }

                // Motion OFF for the two rest-capture modes: CanPush returns false, every
                // navigation falls through to the untouched fade, and what lands on screen is the
                // screen at rest with nothing this task added having moved.
                UiMotion.Enabled = _mode == "push" || _mode == "perf";
                Line("UiMotion.Enabled = " + UiMotion.Enabled);

                yield return Boot();
                yield return Route();

                if (_mode == "push") WriteJson();
                if (_mode == "perf") WritePerfJson();
                Line("=== done: " + _records.Count + " push(es) recorded ===");
            }

            // ═════════════════════════════════════════════════════════════════
            // The GPS bar vs the Game bar
            // ═════════════════════════════════════════════════════════════════

            IEnumerator SequenceNavBar()
            {
                yield return new WaitForSecondsRealtime(1.5f);
                yield return BarReport("GAME", GameObject.Find("Canvas/PersistentUI/BottomNavBar")
                                            ?? FindAnywhere("BottomNavBar"));
                yield return TopReport("GAME");
                yield return Shot("navbar_game");

                yield return TapNamed("GpsPill", "the Home GPS pill");
                yield return Arrive(ScreenId.GpsHub, 2.5f);
                yield return new WaitForSecondsRealtime(1.5f);

                GameObject? cur = Obj(ScreenManager.Instance!.CurrentScreen);
                Transform? bar = GpsScreenTransition.FindLayer(cur, "GpsNavBar");
                yield return BarReport("GPS ", bar != null ? bar.gameObject : null);
                yield return TopReport("GPS ");
                yield return Shot("navbar_gps");
            }

            static GameObject? FindAnywhere(string name)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<RectTransform>())
                    if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
                return null;
            }

            /// <summary>
            /// The TOP bar, reported with its instanceID.
            ///
            /// <para>The GPS screens do not own a top bar — `PersistentUIManager` supplies theirs,
            /// which is why `GPS_HUB_TITLE` shows up there at all. So the honest check is not "do
            /// two objects measure the same" but "is it the same object", and an instanceID says
            /// that where a rect can only imply it.</para>
            /// </summary>
            IEnumerator TopReport(string tag)
            {
                GameObject? top = GameObject.Find("Canvas/PersistentUI/SafeArea/TopBar")
                               ?? FindAnywhere("TopBar");
                if (top == null) { Line("TOP " + tag + ": not found"); yield break; }
                var rt = top.GetComponent<RectTransform>();
                var c = new Vector3[4]; rt.GetWorldCorners(c);
                Transform? sa = top.transform.parent;
                var fitter = sa != null ? sa.GetComponent<GolfinRedux.UI.Core.SafeAreaFitter>() : null;
                var sart = sa as RectTransform;
                Line(string.Format(
                    "TOP {0}  id={1}  active={2}  rect={3:F0}x{4:F0}px  topY={5:F0}  " +
                    "wrapper={6} anchorMin={7} anchorMax={8}  safeArea={9}",
                    tag, top.GetInstanceID(), top.activeInHierarchy,
                    Vector3.Distance(c[0], c[3]), Vector3.Distance(c[0], c[1]), c[1].y,
                    fitter != null ? "SafeAreaFitter" : (sa != null ? sa.name : "-"),
                    sart != null ? sart.anchorMin.ToString() : "-",
                    sart != null ? sart.anchorMax.ToString() : "-",
                    Screen.safeArea));
                yield return null;
            }

            /// <summary>The bar's RENDERED rect, in screen px, plus the sprite it draws.</summary>
            IEnumerator BarReport(string tag, GameObject? go)
            {
                if (go == null) { Line("BAR " + tag + ": not found"); yield break; }
                var rt = go.GetComponent<RectTransform>();
                var im = go.GetComponent<UnityEngine.UI.Image>();
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                var cam = go.GetComponentInParent<Canvas>()?.rootCanvas;
                float scale = cam != null ? cam.scaleFactor : 1f;
                float wpx = Vector3.Distance(c[0], c[3]);
                float hpx = Vector3.Distance(c[0], c[1]);
                float natW = im != null && im.sprite != null ? im.sprite.rect.width  : 0f;
                float natH = im != null && im.sprite != null ? im.sprite.rect.height : 0f;
                Line(string.Format(
                    "BAR {0}  sizeDelta={1}  renderedPx={2:F0}x{3:F0}  canvasScale={4:F3}  " +
                    "sprite={5} native={6:F0}x{7:F0}  type={8}  vStretch={9:F2}x  hStretch={10:F2}x",
                    tag, rt.sizeDelta, wpx, hpx, scale,
                    im != null && im.sprite != null ? im.sprite.name : "<none>", natW, natH,
                    im != null ? im.type.ToString() : "-",
                    natH > 0 ? (hpx / scale) / natH : 0f,
                    natW > 0 ? (wpx / scale) / natW : 0f));
                yield return null;
            }

            // ═════════════════════════════════════════════════════════════════
            // Gift item names, in both languages
            // ═════════════════════════════════════════════════════════════════

            IEnumerator SequenceGift()
            {
                yield return TapNamed("GpsPill", "the Home GPS pill");
                yield return Arrive(ScreenId.GpsHub, 2.5f);
                GameObject? hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
                if (hub == null) { Line("FATAL: no hub"); yield break; }

                yield return Go(hub, "NavGiftButton", ScreenId.GpsGift, "hub nav GIFT");
                yield return new WaitForSecondsRealtime(3f);   // let /gifts/items land

                LocalizationManager.SetLanguage(Language.English);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return StripReport("EN");

                LocalizationManager.SetLanguage(Language.Japanese);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return StripReport("JA");

                LocalizationManager.SetLanguage(Language.English);
                yield return new WaitForSecondsRealtime(0.6f);
                yield return StripReport("EN-again");   // the round trip Cesar reported
            }

            /// <summary>Read the three ItemName labels off the LIVE strip.</summary>
            IEnumerator StripReport(string label)
            {
                GameObject? cur = Obj(ScreenManager.Instance!.CurrentScreen);
                Transform? strip = cur != null
                    ? cur.transform.Find("ContentContainer/BuyGifts/GiftItems") : null;
                if (strip == null) { Line("STRIP " + label + ": not found"); yield break; }

                var sb = new StringBuilder("STRIP " + label + "  ");
                bool anyCjk = false;
                for (int i = 0; i < strip.childCount; i++)
                {
                    Transform cell = strip.GetChild(i);
                    if (!cell.gameObject.activeSelf) continue;
                    Transform? t = cell.Find("ItemName");
                    var tmp = t != null ? t.GetComponent<TMPro.TextMeshProUGUI>() : null;
                    if (tmp == null) continue;
                    string v = tmp.text ?? "";
                    foreach (char ch in v)
                        if ((ch >= 0x3040 && ch <= 0x30FF) || (ch >= 0x4E00 && ch <= 0x9FFF)) anyCjk = true;
                    sb.Append('[').Append(v).Append("] ");
                }
                sb.Append(" containsJapanese=").Append(anyCjk);
                Line(sb.ToString());
                yield return Shot("gift_strip_" + label);
            }

            // ═════════════════════════════════════════════════════════════════
            // The lit nav slot, per screen
            // ═════════════════════════════════════════════════════════════════

            static readonly string[] NavSlots =
            {
                "NavHomeButton", "NavRoundsButton", "NavCameraButton",
                "NavGiftButton", "NavProfileButton",
            };

            IEnumerator SequenceNavTint()
            {
                yield return TapNamed("GpsPill", "the Home GPS pill");
                yield return Arrive(ScreenId.GpsHub, 2.5f);
                yield return TintReport("GpsHubScreen");

                GameObject? hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
                if (hub == null) { Line("FATAL: no hub"); yield break; }

                yield return Go(hub, "NavProfileButton", ScreenId.GpsProfile, "hub nav PROFILE");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return TintReport("GpsProfileScreen");

                GameObject? prof = GameObject.Find("Canvas/ScreensRoot/GpsProfileScreen");
                yield return GoPath(prof, "ContentContainer/BadgesShortcut", ScreenId.GpsBadges, "profile BADGES");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return TintReport("GpsBadgesScreen");
                yield return GoBackReal(ScreenId.GpsProfile, "badges back");
                yield return GoBackReal(ScreenId.GpsHub, "profile back");
                yield return new WaitForSecondsRealtime(1f);

                yield return Go(hub, "NavGiftButton", ScreenId.GpsGift, "hub nav GIFT");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return TintReport("GpsGiftScreen");
                yield return GoBackReal(ScreenId.GpsHub, "gift back");
                yield return new WaitForSecondsRealtime(1f);

                yield return GoPath(hub, "ContentContainer/ActionTiles/Tile_VOTE", ScreenId.GpsVote, "hub tile VOTE");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return TintReport("GpsVoteScreen");
                yield return GoBackReal(ScreenId.GpsHub, "vote back");
                yield return new WaitForSecondsRealtime(1f);

                yield return Go(hub, "NavCameraButton", ScreenId.ScoreUpload, "hub nav CAMERA");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return TintReport("ScoreUploadScreen");
            }

            /// <summary>Read all five slot colours off the LIVE bar and say which one is lit.</summary>
            IEnumerator TintReport(string screenName)
            {
                GameObject? cur = Obj(ScreenManager.Instance!.CurrentScreen);
                Transform? bar = GpsScreenTransition.FindLayer(cur, "GpsNavBar");
                if (bar == null) { Line("TINT " + screenName + ": no bar"); yield break; }

                string expected = GpsNavBarHighlight.SlotFor(screenName) ?? "(none)";
                var sb = new StringBuilder("TINT " + screenName + "  expected=" + expected + "  ");
                string litFound = "(none)";
                foreach (string slot in NavSlots)
                {
                    Transform? t = bar.Find(slot);
                    var img = t != null ? t.GetComponent<UnityEngine.UI.Image>() : null;
                    if (img == null) continue;
                    Color c = img.color;
                    bool lit = c.r < 0.5f && c.g > 0.5f && c.b > 0.5f;   // cyan-ish, not white
                    if (lit) litFound = slot;
                    sb.Append(slot.Replace("Nav","").Replace("Button",""))
                      .Append("=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append(' ');
                }
                bool ok = litFound == expected;
                sb.Append(" -> lit=").Append(litFound).Append(ok ? "  PASS" : "  ***FAIL***");
                Line(sb.ToString());
                yield return Shot("navtint_" + screenName);
            }

            // ═════════════════════════════════════════════════════════════════
            // A8 · a cold frame per site
            // ═════════════════════════════════════════════════════════════════

            IEnumerator SequenceShimmer()
            {
                // THE WATCHER IS STARTED BEFORE THE NAVIGATION, not after it, and that is the
                // whole trick. The cold window on the hub was over inside the tap helper's own
                // 1 s wait — the log showed shown->hidden before the poll had even begun. A
                // watcher that runs CONCURRENTLY with the navigation sees the frame the
                // placeholder is actually up.
                StartCoroutine(Watch(ShimmerHost.HubRounds, "hub_rounds"));
                yield return TapNamed("GpsPill", "the Home GPS pill");
                yield return new WaitForSecondsRealtime(4f);

                GameObject? hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
                if (hub == null) { Line("FATAL: no hub"); yield break; }

                yield return Go(hub, "NavProfileButton", ScreenId.GpsProfile, "hub nav PROFILE");
                GameObject? prof = GameObject.Find("Canvas/ScreensRoot/GpsProfileScreen");

                // STRAIGHT THROUGH, with no settle. The Profile screen fetches badges itself
                // (FetchLiveData chains /score/stats, /badges/progress, /score/history), so
                // pausing here would warm BadgeService and the grid would open with a cache hit —
                // correctly showing no placeholder, and giving A8 nothing to photograph. A player
                // who taps BADGES the moment Profile appears beats that fetch, and that is the
                // only moment the badge grid is genuinely cold.
                StartCoroutine(Watch(ShimmerHost.Badges, "badges_grid"));
                yield return GoPath(prof, "ContentContainer/BadgesShortcut", ScreenId.GpsBadges, "profile BADGES");
                yield return new WaitForSecondsRealtime(4f);

                yield return GoBackReal(ScreenId.GpsProfile, "badges back");
                yield return GoBackReal(ScreenId.GpsHub, "profile back");
                yield return new WaitForSecondsRealtime(1f);

                // ONE frame covers both gift sites — they are two panels of one screen and both
                // are cold at the same moment.
                StartCoroutine(Watch(ShimmerHost.Supporters, "gift_supporters_and_golfers",
                                     alsoRequire: ShimmerHost.Golfers));
                yield return Go(hub, "NavGiftButton", ScreenId.GpsGift, "hub nav GIFT");
                yield return new WaitForSecondsRealtime(4f);
                yield return GoBackReal(ScreenId.GpsHub, "gift back");
                yield return new WaitForSecondsRealtime(1f);

                StartCoroutine(Watch(ShimmerHost.VoteList, "vote_list"));
                yield return GoPath(hub, "ContentContainer/ActionTiles/Tile_VOTE", ScreenId.GpsVote, "hub tile VOTE");
                yield return new WaitForSecondsRealtime(4f);
                yield return GoBackReal(ScreenId.GpsHub, "vote back");
            }

            /// <summary>Poll for the site's host to be genuinely active, then capture ONE frame.
            /// Logs whether it ever was — a still with no such line proves nothing.</summary>
            IEnumerator Watch(string site, string label, string? alsoRequire = null,
                              float timeout = 10f)
            {
                float t0 = Time.realtimeSinceStartup;
                float deadline = t0 + timeout;
                while (Time.realtimeSinceStartup < deadline)
                {
                    ShimmerHost? a = Anywhere(site);
                    ShimmerHost? b = alsoRequire != null ? Anywhere(alsoRequire) : null;
                    bool up = a != null && a.gameObject.activeInHierarchy &&
                              (alsoRequire == null || (b != null && b.gameObject.activeInHierarchy));

                    // NO "wait for the push to land" GUARD. The first version had one, and it
                    // blocked exactly the window it was trying to photograph: a screen reached by
                    // a push shows its placeholder in OnEnable — DURING the 0.25 s push — and the
                    // fetch answers ~250 ms later, so the site was already cold-over by the time
                    // the guard let go. Only the hub, reached through the slower boundary fade,
                    // ever got through. A frame of a screen still sliding in with its placeholder
                    // up is the honest evidence; a missed frame is not.
                    if (up)
                    {
                        Line($"SHIMMER {site}{(alsoRequire != null ? " + " + alsoRequire : "")} " +
                             $"ACTIVE at t+{(Time.realtimeSinceStartup - t0) * 1000f:0} ms — capturing");
                        yield return Shot(label);
                        yield break;
                    }
                    yield return null;
                }
                Line($"SHIMMER {site} was NEVER active in {timeout:0} s — no frame captured");
            }

            /// <summary>A shimmer host by site, ANYWHERE in the loaded scenes. The watcher starts
            /// before the navigation, so at that moment the screen it belongs to is not the
            /// current one yet — looking it up under CurrentScreen would find nothing.</summary>
            static ShimmerHost? Anywhere(string site)
            {
                foreach (ShimmerHost h in FindObjectsByType<ShimmerHost>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (h != null && h.Site == site) return h;
                return null;
            }

            /// <summary>Through the title gate and onto Home. The app boots to a Start screen
            /// ScreenManager does not manage, so nothing below may run before this.</summary>
            IEnumerator Boot()
            {
                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");
                yield return TapStart();

                // DO NOT BLOCK ON HOME. The title gate is not always there: a session that has
                // already passed it boots straight onto a screen — this run came up on GpsHub with
                // no StartButton at all, and a 90 s hard wait for Home sat through the whole
                // probe. Route() puts us on Home either way, through the untouched boundary
                // navigation, so this is a short courtesy wait rather than a precondition.
                yield return Until(() => ScreenManager.Instance!.CurrentScreen == ScreenId.Home, 20f, "Home");
                yield return new WaitForSecondsRealtime(2f);
            }

            /// <summary>The route itself: seven screens, every forward move a real widget's
            /// onClick. Walked once by the three single-pass modes and twice by `parity`.</summary>
            IEnumerator Route()
            {
                if (ScreenManager.Instance!.CurrentScreen != ScreenId.Home)
                {
                    // Between the parity passes only. Home <-> GpsHub is a BOUNDARY move, which
                    // this task does not touch, so reaching Home this way changes nothing the
                    // comparison is about.
                    ScreenManager.Instance.ShowScreen(ScreenId.Home);
                    yield return Until(() => ScreenManager.Instance!.CurrentScreen == ScreenId.Home, 20f, "Home");
                    yield return new WaitForSecondsRealtime(1.5f);
                }

                yield return TapNamed("GpsPill", "the Home GPS pill");
                yield return Arrive(ScreenId.GpsHub, 3f);
                yield return Shot("hub");

                GameObject? hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
                if (hub == null) { Line("FATAL: no hub in the scene"); yield break; }

                // ── (a) hub -> Profile -> Badges -> back -> back ─────────────
                yield return Go(hub, "NavProfileButton", ScreenId.GpsProfile, "hub nav PROFILE");
                yield return Shot("profile");

                GameObject? prof = GameObject.Find("Canvas/ScreensRoot/GpsProfileScreen");
                yield return GoPath(prof, "ContentContainer/BadgesShortcut", ScreenId.GpsBadges, "profile BADGES");
                yield return Shot("badges");
                yield return GoBackReal(ScreenId.GpsProfile, "badges back");

                yield return GoPath(prof, "ContentContainer/AvatarShortcut", ScreenId.GpsAvatar, "profile AVATAR");
                yield return Shot("avatar");
                yield return GoBackReal(ScreenId.GpsProfile, "avatar back");
                yield return GoBackReal(ScreenId.GpsHub, "profile back");

                // ── (b) the nav-bar sweep ────────────────────────────────────
                yield return Go(hub, "NavGiftButton", ScreenId.GpsGift, "hub nav GIFT");
                yield return Shot("gift");
                yield return GoBackReal(ScreenId.GpsHub, "gift back");

                yield return GoPath(hub, "ContentContainer/ActionTiles/Tile_VOTE", ScreenId.GpsVote, "hub tile VOTE");
                yield return Shot("vote");
                yield return GoBackReal(ScreenId.GpsHub, "vote back");

                // ROUNDS — added by gps_checkin. The slot was inert when this probe was written,
                // so the screen the task builds had never been through the motion measurement at
                // all; its transition was asserted from the table rather than observed.
                yield return Go(hub, "NavRoundsButton", ScreenId.GpsRounds, "hub nav ROUNDS");
                yield return Shot("rounds");
                yield return GoBackReal(ScreenId.GpsHub, "rounds back");

                // ── ScoreUpload: proves the FADE is still what it gets ───────
                yield return Go(hub, "NavCameraButton", ScreenId.ScoreUpload, "hub nav CAMERA");
                yield return Shot("scoreupload");
                yield return GoBackReal(ScreenId.GpsHub, "score upload back");
            }

            // ═════════════════════════════════════════════════════════════════
            // Navigation + measurement
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Go(GameObject? root, string navChild, ScreenId target, string what)
            {
                Transform? nav = GpsScreenTransition.FindLayer(root, "GpsNavBar");
                Transform? t   = nav != null ? nav.Find(navChild) : null;
                yield return Tap(t, target, what);
            }

            IEnumerator GoPath(GameObject? root, string path, ScreenId target, string what)
            {
                Transform? t = root != null ? root.transform.Find(path) : null;
                yield return Tap(t, target, what);
            }

            /// <summary>
            /// BACK through a real widget. The Badges / Avatar / Gift / Vote screens carry no back
            /// button of their own — the affordance is the shared chrome — so the first ACTIVE
            /// button whose name contains "Back" is tapped and NAMED in the log, rather than
            /// calling GoBack() directly and pretending that was a tap.
            /// </summary>
            IEnumerator GoBackReal(ScreenId target, string what)
            {
                Button? found = null;
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                               FindObjectsSortMode.None))
                {
                    if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                    if (b.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    found = b; break;
                }
                // No back widget: use the NAV BAR slot for the destination, which is a real tap on
                // a real widget and — since gps_polish wired the bar on non-hub screens — the
                // player's actual way out of Badges / Avatar / Gift / Vote.
                if (found == null)
                {
                    GameObject? cur = Obj(ScreenManager.Instance!.CurrentScreen);
                    Transform? bar = GpsScreenTransition.FindLayer(cur, "GpsNavBar");
                    string slot = target == ScreenId.GpsHub     ? "NavHomeButton"
                                : target == ScreenId.GpsProfile ? "NavProfileButton"
                                : target == ScreenId.GpsGift    ? "NavGiftButton"
                                : "NavCameraButton";
                    Transform? st = bar != null ? bar.Find(slot) : null;
                    var sb2 = st != null ? st.GetComponent<Button>() : null;
                    if (sb2 != null && sb2.interactable)
                    {
                        yield return Tap(st, target, what + " via nav slot '" + slot + "'");
                        yield break;
                    }
                    Line("WARN: " + what + " — no Back widget and nav slot '" + slot +
                         "' is dead; using GoBack() (NOT a real tap)");
                    ScreenManager.Instance?.GoBack(target);
                    yield return Arrive(target, 2.5f);
                    yield break;
                }
                yield return Tap(found.transform, target, what + " via '" + found.name + "'");
            }

            IEnumerator Tap(Transform? t, ScreenId target, string what)
            {
                var b = t != null ? t.GetComponent<Button>() : null;
                if (b == null) { Line("WARN: no button for " + what); yield break; }

                ScreenId from = ScreenManager.Instance!.CurrentScreen;
                Line("tapping " + what + " (interactable=" + b.interactable + ") " + from + " -> " + target);

                bool expectPush = UiMotion.Enabled &&
                                  GpsScreenTransition.CanPush(from, target, Obj(from), Obj(target));

                b.onClick.Invoke();

                if (expectPush) yield return Measure(from, target);
                yield return Arrive(target, expectPush ? 1.5f : 3f);
            }

            /// <summary>
            /// Sample every frame of the push. This is the only place the invariants come from —
            /// no assertion below is read off a still.
            /// </summary>
            IEnumerator Measure(ScreenId from, ScreenId to)
            {
                GameObject? fromGo = Obj(from), toGo = Obj(to);
                var r = new Record
                {
                    From = from.ToString(), To = to.ToString(),
                    Direction = GpsScreenTransition.DirectionFor(from, to, push: true).ToString(),
                    ExpectedDur = UiMotion.PushDur,
                };

                RectTransform? toContent   = Rect(toGo,   "ContentContainer");
                RectTransform? fromContent = Rect(fromGo, "ContentContainer");
                CanvasGroup?   toBg        = Group(toGo,   "Background");
                CanvasGroup?   fromBg      = Group(fromGo, "Background");
                CanvasGroup?   toContentCg   = Group(toGo,   "ContentContainer");
                CanvasGroup?   fromContentCg = Group(fromGo, "ContentContainer");

                // Rest X and the t0 offset come from the TWEEN, which sampled them before it
                // moved anything. Reading them here would read the already-staged position and
                // call the off-screen start "rest" — the bug that made every assertion in the
                // first run fire.
                var canvas = toGo != null ? toGo.GetComponentInParent<Canvas>() : null;
                var crt = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
                r.W = crt != null ? crt.rect.width : 1170f;

                // Only the SEAM needs per-frame observation, and it is sign-free: one frame of
                // observer lag cannot manufacture a covered frame that did not happen.
                long   allocBytes = 0;
                double worstMs    = 0;
                int    perfFrames = 0;

                while (GpsScreenTransition.IsPushing)
                {
                    float cover = Mathf.Max(toBg != null ? toBg.alpha : 1f,
                                            fromBg != null ? fromBg.alpha : 1f);
                    if (cover < r.SeamWorstCover) r.SeamWorstCover = cover;

                    if (_perf)
                    {
                        if (_gcRec.Valid)    allocBytes += _gcRec.LastValue;
                        if (_frameRec.Valid) worstMs = Math.Max(worstMs, _frameRec.LastValue * 1e-6);
                        perfFrames++;
                    }
                    yield return null;
                }

                if (_perf)
                    _perfRows.Add((from + "->" + to, allocBytes, worstMs, perfFrames));

                r.MeasuredDur      = GpsScreenTransition.LastPushElapsed;
                r.Frames           = GpsScreenTransition.LastPushFrames;
                r.Completed        = GpsScreenTransition.LastPushCompleted;
                r.TargetOffsetAtT0 = GpsScreenTransition.LastPushEnterOffset;
                r.EndTargetRestX   = GpsScreenTransition.LastPushTargetRestX;
                r.EndLeaverRestX   = GpsScreenTransition.LastPushLeaverRestX;

                // Rest, one frame after the swap settled.
                yield return null;
                if (toContent   != null) r.EndTargetX = toContent.anchoredPosition.x;
                if (fromContent != null) r.EndLeaverX = fromContent.anchoredPosition.x;
                r.EndTargetChromeAlphaMin = MinChromeAlpha(toGo);
                r.EndLeaverChromeAlphaMin = MinChromeAlpha(fromGo);
                r.EndTargetContentAlpha   = toContentCg   != null ? toContentCg.alpha   : 1f;
                r.EndLeaverContentAlpha   = fromContentCg != null ? fromContentCg.alpha : 1f;
                r.EndBlocksRaycasts = (toContentCg == null || toContentCg.blocksRaycasts)
                                   && (fromContentCg == null || fromContentCg.blocksRaycasts);

                Assert(r);
                _records.Add(r);
                Line("  push " + r.From + "->" + r.To + " dir=" + r.Direction +
                     " frames=" + r.Frames + " dur=" + r.MeasuredDur.ToString("0.000") +
                     " t0off=" + r.TargetOffsetAtT0.ToString("0.#") +
                     " seamCover=" + r.SeamWorstCover.ToString("0.000") +
                     " fails=" + r.Fails.Count);
            }

            static void Assert(Record r)
            {
                if (Mathf.Abs(r.MeasuredDur - r.ExpectedDur) > DurationToleranceSec)
                    r.Fails.Add($"duration {r.MeasuredDur:0.000}s is more than {DurationToleranceSec:0.000}s from {r.ExpectedDur:0.000}s");

                float want = r.Direction == "Forward" ? r.W : -r.W;
                if (Mathf.Abs(r.TargetOffsetAtT0 - want) > 1f)
                    r.Fails.Add($"target content at t0 was {r.TargetOffsetAtT0:0.#} from rest, expected {want:0.#}");

                if (Mathf.Abs(r.EndTargetX - r.EndTargetRestX) > 0.01f)
                    r.Fails.Add($"target content settled at x={r.EndTargetX:0.###}, rest is {r.EndTargetRestX:0.###}");
                if (Mathf.Abs(r.EndLeaverX - r.EndLeaverRestX) > 0.01f)
                    r.Fails.Add($"leaver content settled at x={r.EndLeaverX:0.###}, rest is {r.EndLeaverRestX:0.###}");

                if (r.EndTargetChromeAlphaMin < 0.999f)
                    r.Fails.Add($"target chrome settled at alpha {r.EndTargetChromeAlphaMin:0.###}");
                if (r.EndLeaverChromeAlphaMin < 0.999f)
                    r.Fails.Add($"leaver chrome settled at alpha {r.EndLeaverChromeAlphaMin:0.###}");
                if (r.EndTargetContentAlpha < 0.999f || r.EndLeaverContentAlpha < 0.999f)
                    r.Fails.Add($"content alpha settled at {r.EndTargetContentAlpha:0.###}/{r.EndLeaverContentAlpha:0.###}");

                if (!r.EndBlocksRaycasts)
                    r.Fails.Add("blocksRaycasts was not restored");

                // THE SEAM TEST. Every frame must have at least one background at full-ish
                // opacity, or the composite shows through to whatever is behind the canvas.
                if (r.SeamWorstCover < 0.5f)
                    r.Fails.Add($"background seam: worst frame covered only {r.SeamWorstCover:0.###}");

                if (r.Frames < 2)
                    r.Fails.Add($"only {r.Frames} frame(s) sampled — the push did not animate");
                if (!r.Completed)
                    r.Fails.Add("push did not run to completion (it was snapped by another navigation)");
            }

            void WriteJson()
            {
                int fails = 0;
                foreach (var r in _records) fails += r.Fails.Count;

                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"task\": \"gps_polish\",");
                sb.AppendLine("  \"generated\": \"" + DateTime.UtcNow.ToString("u") + "\",");
                sb.AppendLine("  \"pushDurSec\": " + F(UiMotion.PushDur) + ",");
                sb.AppendLine("  \"fadeDurSec\": " + F(UiMotion.FadeDur) + ",");
                sb.AppendLine("  \"durationToleranceSec\": " + F(DurationToleranceSec) + ",");
                sb.AppendLine("  \"transitions\": " + _records.Count + ",");
                sb.AppendLine("  \"fail\": " + fails + ",");
                sb.AppendLine("  \"records\": [");
                for (int i = 0; i < _records.Count; i++)
                {
                    Record r = _records[i];
                    sb.AppendLine("    {");
                    sb.AppendLine("      \"from\": \"" + r.From + "\", \"to\": \"" + r.To + "\", \"direction\": \"" + r.Direction + "\",");
                    sb.AppendLine("      \"W\": " + F(r.W) + ", \"frames\": " + r.Frames + ",");
                    sb.AppendLine("      \"measuredDurSec\": " + F(r.MeasuredDur) + ", \"expectedDurSec\": " + F(r.ExpectedDur) + ",");
                    sb.AppendLine("      \"targetOffsetAtT0\": " + F(r.TargetOffsetAtT0) + ",");
                    sb.AppendLine("      \"endTargetX\": " + F(r.EndTargetX) + ", \"endTargetRestX\": " + F(r.EndTargetRestX) + ",");
                    sb.AppendLine("      \"endLeaverX\": " + F(r.EndLeaverX) + ", \"endLeaverRestX\": " + F(r.EndLeaverRestX) + ",");
                    sb.AppendLine("      \"endTargetChromeAlphaMin\": " + F(r.EndTargetChromeAlphaMin) + ",");
                    sb.AppendLine("      \"endLeaverChromeAlphaMin\": " + F(r.EndLeaverChromeAlphaMin) + ",");
                    sb.AppendLine("      \"endTargetContentAlpha\": " + F(r.EndTargetContentAlpha) + ",");
                    sb.AppendLine("      \"endLeaverContentAlpha\": " + F(r.EndLeaverContentAlpha) + ",");
                    sb.AppendLine("      \"blocksRaycastsRestored\": " + (r.EndBlocksRaycasts ? "true" : "false") + ",");
                    sb.AppendLine("      \"ranToCompletion\": " + (r.Completed ? "true" : "false") + ",");
                    sb.AppendLine("      \"seamWorstCover\": " + F(r.SeamWorstCover) + ",");
                    sb.Append    ("      \"fails\": [");
                    for (int k = 0; k < r.Fails.Count; k++)
                        sb.Append((k > 0 ? ", " : "") + "\"" + r.Fails[k].Replace("\"", "'") + "\"");
                    sb.AppendLine("]");
                    sb.AppendLine("    }" + (i < _records.Count - 1 ? "," : ""));
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                Directory.CreateDirectory(Path.GetDirectoryName(JsonPath)!);
                File.WriteAllText(JsonPath, sb.ToString());
                File.WriteAllText(Path.Combine(TaskDir, "gps_polish_invariants.json"), sb.ToString());
                Line("invariants -> " + JsonPath + "  fail=" + fails);
            }

            /// <summary>A13 — the perf pass's own file. Never mixed into the invariants JSON: the
            /// two are measured under different conditions and a reader must not think one run
            /// produced both.</summary>
            void WritePerfJson()
            {
                long   warmAlloc = 0, firstAlloc = 0, worstAllocPush = 0;
                double worstMs = 0, firstMs = 0;
                int    warmFrames = 0;
                string worstPair = "-", worstMsPair = "-";
                for (int i = 0; i < _perfRows.Count; i++)
                {
                    var row = _perfRows[i];
                    // The FIRST push is warm-up and is excluded: it is the one that creates the
                    // coroutines, adds UiMotionRunner and the on-demand CanvasGroups, and boxes
                    // the enumerators. Every push after it runs on objects that already exist.
                    if (i == 0) { firstAlloc = row.allocBytes; firstMs = row.worstMs; continue; }
                    warmAlloc  += row.allocBytes;
                    warmFrames += row.frames;
                    if (row.allocBytes > worstAllocPush) { worstAllocPush = row.allocBytes; worstPair = row.pair; }
                    if (row.worstMs > worstMs)           { worstMs = row.worstMs;           worstMsPair = row.pair; }
                }
                double perFrame = warmFrames > 0 ? warmAlloc / (double)warmFrames : 0;

                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"task\": \"gps_polish\", \"pass\": \"perf\",");
                sb.AppendLine("  \"generated\": \"" + DateTime.UtcNow.ToString("u") + "\",");
                sb.AppendLine("  \"note\": \"ProfilerRecorder sampled ONLY on the frames a push is running: 'GC Allocated In Frame' (Memory) and 'Main Thread' (Internal). No screenshots are taken on this pass. Editor play mode, profiler enabled, live server behind every screen — so these figures are an upper bound on the whole app during a push, not the tween alone. UiMotionAllocationTests measures the tween loops in isolation.\",");
                sb.AppendLine("  \"pushesSampled\": " + _perfRows.Count + ",");
                sb.AppendLine("  \"firstPushAllocBytes\": " + firstAlloc + ",");
                sb.AppendLine("  \"firstPushWorstFrameMs\": " + firstMs.ToString("0.###", CultureInfo.InvariantCulture) + ",");
                sb.AppendLine("  \"warmFrames\": " + warmFrames + ",");
                sb.AppendLine("  \"warmTotalAllocBytes\": " + warmAlloc + ",");
                sb.AppendLine("  \"warmAllocBytesPerFrame\": " + perFrame.ToString("0.##", CultureInfo.InvariantCulture) + ",");
                sb.AppendLine("  \"worstPushAllocBytes\": " + worstAllocPush + ", \"worstPushAllocPair\": \"" + worstPair + "\",");
                sb.AppendLine("  \"worstFrameMs\": " + worstMs.ToString("0.###", CultureInfo.InvariantCulture) + ", \"worstFramePair\": \"" + worstMsPair + "\",");
                sb.Append    ("  \"perPush\": [");
                for (int i = 0; i < _perfRows.Count; i++)
                {
                    var row = _perfRows[i];
                    sb.Append(i > 0 ? ",\n    " : "\n    ");
                    sb.Append("{\"pair\": \"" + row.pair + "\", \"frames\": " + row.frames +
                              ", \"allocBytes\": " + row.allocBytes +
                              ", \"worstFrameMs\": " + row.worstMs.ToString("0.###", CultureInfo.InvariantCulture) + "}");
                }
                sb.AppendLine("\n  ]");
                sb.AppendLine("}");

                string path = "Docs/Diagnostics/_capture/gps_polish_perf.json";
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, sb.ToString());
                File.WriteAllText(Path.Combine(TaskDir, "gps_polish_perf.json"), sb.ToString());
                Line("perf -> " + path + "  worstFrameMs=" + worstMs.ToString("0.###") +
                     "  allocPerFrame=" + perFrame.ToString("0"));
            }

            static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

            // ═════════════════════════════════════════════════════════════════
            // Scene helpers
            // ═════════════════════════════════════════════════════════════════

            static GameObject? Obj(ScreenId id)
            {
                string name;
                switch (id)
                {
                    case ScreenId.GpsHub:         name = "GpsHubScreen"; break;
                    case ScreenId.ScoreUpload:    name = "ScoreUploadScreen"; break;
                    case ScreenId.GpsProfile:     name = "GpsProfileScreen"; break;
                    case ScreenId.GpsAvatar:      name = "GpsAvatarScreen"; break;
                    case ScreenId.GpsBadges:      name = "GpsBadgesScreen"; break;
                    case ScreenId.GpsGolfProfile: name = "GpsGolfProfileScreen"; break;
                    case ScreenId.GpsWelcome:     name = "GpsWelcomeScreen"; break;
                    case ScreenId.GpsGift:        name = "GpsGiftScreen"; break;
                    case ScreenId.GpsVote:        name = "GpsVoteScreen"; break;
                    // gps_checkin. Missing here made the probe BLIND to the Rounds push:
                    // Obj() returned null, CanPush went false, expectPush went false, and
                    // the leg ran without ever being measured — no record, no failure, no
                    // sign anything was wrong. A screen absent from this switch is silently
                    // unmeasured, not reported as unmeasurable.
                    case ScreenId.GpsRounds:      name = "GpsRoundsScreen"; break;
                    default: return null;
                }
                // Inactive screens are not reachable with GameObject.Find, and the target of a
                // push is inactive at the moment it is looked up.
                Transform? root = GameObject.Find("Canvas/ScreensRoot")?.transform;
                Transform? t = root != null ? root.Find(name) : null;
                return t != null ? t.gameObject : null;
            }

            static RectTransform? Rect(GameObject? go, string child)
                => GpsScreenTransition.FindLayer(go, child) as RectTransform;

            static CanvasGroup? Group(GameObject? go, string child)
            {
                Transform? t = GpsScreenTransition.FindLayer(go, child);
                return t != null ? t.GetComponent<CanvasGroup>() : null;
            }

            static float MinChromeAlpha(GameObject? go)
            {
                float min = 1f;
                foreach (string n in new[] { "Background", "GpsNavBar", "BackPill" })
                {
                    CanvasGroup? cg = Group(go, n);
                    if (cg != null && cg.alpha < min) min = cg.alpha;
                }
                return min;
            }

            // ═════════════════════════════════════════════════════════════════
            // Plumbing
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Arrive(ScreenId id, float settle)
            {
                yield return Until(() => ScreenManager.Instance!.CurrentScreen == id, 30f, id.ToString());
                // The shimmer pass has to start looking on the FIRST frame after arrival — the
                // cold window is a few hundred milliseconds and a settle would sleep through it.
                yield return new WaitForSecondsRealtime(_mode == "shimmer" ? 0f : settle);
            }

            IEnumerator Shot(string label)
            {
                // The perf pass takes none: a full-resolution ReadPixels + PNG encode allocates
                // about 100 MB and would be charged to whichever push it lands beside.
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
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
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

            IEnumerator TapNamed(string name, string what)
            {
                float deadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
                    {
                        if (b.name != name || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping " + what + " (" + b.name + ", interactable=" + b.interactable + ")");
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: " + what + " ('" + name + "') never appeared");
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
                Debug.Log("[POLISH-PROBE] " + s);
                File.WriteAllText(LogPath, _log.ToString());
            }
        }
    }
}
