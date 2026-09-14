// ─────────────────────────────────────────────────────────────────────────────
// rankings_list_drag_anywhere / scroll_lists_drag_anywhere — can a drag that starts on EMPTY
// list space scroll the list?
//
// Cesar (2026-09-14): "in order to scroll the list of players right now the user has to
// click exactly on a player and then drag instead of dragging from anywhere in the list."
// Then: "Do the other 13" — the sweep over every list the static audit flagged.
//
// WHY THE EXISTING OVER-SCROLL CLIP CANNOT ANSWER THIS. GamePolishDemoRecorderC drives
// ScrollRect.OnBeginDrag / OnDrag directly — deliberately, because §C2's subject was the
// movement type. That skips the one link this defect lives in: the EventSystem RAYCAST that
// decides which object owns the press. A press on empty list space hits whatever is behind
// the list (or nothing at all), ExecuteEvents.GetEventHandler<IDragHandler> walks up from
// THAT object, finds no ScrollRect, and nothing scrolls. Driving the handler directly would
// PASS a broken list.
//
// SO THIS INSTRUMENT DOES WHAT InputSystemUIInputModule DOES, minus the OS layer: it
// presses at a screen point, asks EventSystem.RaycastAll what is under it, resolves the drag
// owner by bubbling from the top hit exactly as the input modules do, and only THEN
// dispatches initializePotentialDrag / beginDrag / drag×N / endDrag to whatever it resolved.
// If the raycast resolves nothing, nothing is dispatched — which is the bug, reproduced.
//
// TWO PRESSES PER LIST, and the second is the control:
//   empty     the first empty spot the finder can prove is inside the viewport and inside no
//             child: the gap under the first row, the space under the last row, the left
//             margin, or the viewport centre of an empty list. Which one is in the JSON.
//   control   a plain label inside the first row (never inside a Selectable — an InputField
//             owns its own drag) — the "exactly on a player" press that always worked. If
//             THIS one fails the instrument is wrong, not the list.
//
// THE VERDICT IS JSON (PIPELINE_HARDENING rule 3): per press, the top raycast hit, the
// resolved drag handler, content.anchoredPosition before and after, and a pass flag. A list
// too short to scroll passes on the handler alone and says so (`scrollable: false`). The mp4
// is for Cesar; the JSON is the gate. Every list is reached from a real boot through the real
// title StartButton; each Target's `entry` names the widget or the ScreenManager call the
// widget itself makes — never ShowScreen behind the gate.
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
using GolfinRedux.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    public static class ScrollDragAnywhereVerify
    {
        /// <summary>One list under test: how a real player reaches it, and where it is.</summary>
        public sealed class Target
        {
            public string Name  = "";
            public string Title = "";
            /// <summary>Navigates to the list. Sets <see cref="ScrollDragAnywhereRunner.Entry"/>.</summary>
            public Func<ScrollDragAnywhereRunner, IEnumerator> Open = _ => Nothing();
            /// <summary>The ScrollRect, once open.</summary>
            public Func<ScrollRect?> Resolve = () => null;
            /// <summary>Leaves the surface the way a player would (modal Hide etc.).</summary>
            public Action? Close;
            static IEnumerator Nothing() { yield break; }
        }

        public const string RankingsDir = "Docs/Specs/Quick/media/rankings_list_drag_anywhere";
        public const string SweepDir    = "Docs/Specs/Quick/media/scroll_lists_drag_anywhere";

        const string ArmedKey  = "ScrollDragAnywhereVerify.Armed";
        const string LabelKey  = "ScrollDragAnywhereVerify.Label";
        const string RecordKey = "ScrollDragAnywhereVerify.Record";
        const string SweepKey  = "ScrollDragAnywhereVerify.Sweep";
        const string OnlyKey   = "ScrollDragAnywhereVerify.Only";
        static RecorderController? _recorder;

        /// <summary>
        /// Frame cap for the whole play session (BotVideoRecorder's guardrail 3). Without it an
        /// unfocused Editor with runInBackground renders as fast as it can, and a four-minute
        /// sweep — recorded at 1170×2532 on top — pegged the CPU until Cesar had to kill Unity
        /// (2026-09-14). 30 fps is all a 30 fps clip can use, and plenty for a raycast probe.
        /// Set BEFORE StartRecording (a render-state change mid-clip is the y-flip trigger).
        /// </summary>
        const int Fps = 30;
        static int  _savedTargetFps = -1, _savedVSync = 1;
        static bool _capApplied;

        static void ApplyFrameCap()
        {
            _savedTargetFps = Application.targetFrameRate;
            _savedVSync     = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount  = 0;      // vSync would clamp targetFrameRate to display Hz
            Application.targetFrameRate = Fps;
            _capApplied = true;
        }

        static void RestoreFrameCap()
        {
            if (!_capApplied) return;
            Application.targetFrameRate = _savedTargetFps;
            QualitySettings.vSyncCount  = _savedVSync;
            _capApplied = false;
        }

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Game Polish/Verify — drag-anywhere on the Rankings list (record)", priority = 296)]
        public static void LaunchRankingsRecorded() => Launch("after", record: true, sweep: false);

        [MenuItem("GOLFIN/Game Polish/Verify — drag-anywhere on the Rankings list (no video)", priority = 297)]
        public static void LaunchRankingsLogOnly() => Launch("check", record: false, sweep: false);

        // The sweep is JSON + stills by design: fifteen lists is a four-minute play session, and
        // recording that at full size is the run that locked the Editor. Record a SHORT subset
        // through Launch(label, record: true, sweep: true, only: "a,b,c") when a clip is wanted.
        [MenuItem("GOLFIN/Game Polish/Verify — drag-anywhere on EVERY audited list (no video)", priority = 298)]
        public static void LaunchSweepLogOnly() => Launch("check", record: false, sweep: true);

        /// <summary>Arm and enter play mode. <paramref name="label"/> names the output files so a
        /// baseline run and an after-fix run sit side by side.</summary>
        public static void Launch(string label, bool record, bool sweep) => Launch(label, record, sweep, "");

        /// <param name="only">Comma-separated target names to run; empty = every target.</param>
        public static void Launch(string label, bool record, bool sweep, string only)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[DragAnywhere] Already playing — stop first."); return; }
            Directory.CreateDirectory(sweep ? SweepDir : RankingsDir);
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetString(LabelKey, label);
            SessionState.SetBool(RecordKey, record);
            SessionState.SetBool(SweepKey, sweep);
            SessionState.SetString(OnlyKey, only ?? "");
            EditorApplication.EnterPlaymode();
            Debug.Log($"[DragAnywhere] Armed ({label}, record={record}, sweep={sweep}). Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); RestoreFrameCap(); return; }
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);

            string label = SessionState.GetString(LabelKey, "run");
            bool record  = SessionState.GetBool(RecordKey, false);
            bool sweep   = SessionState.GetBool(SweepKey, false);
            string outDir = sweep ? SweepDir : RankingsDir;
            ApplyFrameCap();                        // before StartRecording — never during the clip
            if (record) StartRecorder(outDir, label);

            List<Target> targets = sweep ? Targets.Sweep() : Targets.RankingsOnly();
            string only = SessionState.GetString(OnlyKey, "");
            if (only != "")
            {
                var keep = new HashSet<string>(only.Split(','));
                targets.RemoveAll(t => !keep.Contains(t.Name));
            }

            var host = new GameObject("[ScrollDragAnywhereRunner]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ScrollDragAnywhereRunner>().Begin(targets, label, record, outDir, Time.realtimeSinceStartup);
        }

        // ── recorder (the GamePolishDemoRecorderC / GachaRevealDemoRecorder idiom) ─────────

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? t = asm.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                    if (t == null) continue;
                    var m = t.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
                    return m != null && (bool)m.Invoke(null, null)!;
                }
            }
            catch { /* fall through to the measured size */ }
            return false;
        }

        static void StartRecorder(string outDir, string label)
        {
            bool pinned = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!pinned)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[DragAnywhere] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            string rawPath = Path.Combine(outDir, $"raw_{label}.mp4");
            if (File.Exists(rawPath)) File.Delete(rawPath);

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "ScrollDragAnywhere";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = Path.Combine(outDir, $"raw_{label}");

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            CaptureCore.RecordingActive = true;     // no stills while a clip rolls (y-flip lesson)
            Debug.Log($"[DragAnywhere] Recording → {rawPath} ({w}x{h} @ 30fps)");
        }

        static void StopRecorder()
        {
            CaptureCore.RecordingActive = false;
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[DragAnywhere] Recorder stopped."); }
            catch (Exception e) { Debug.LogWarning($"[DragAnywhere] StopRecorder: {e.Message}"); }
            _recorder = null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // The lists. One row per ScrollRect the static audit flagged
        // (Docs/Scripts/scrollrect_raycast_audit.py) plus the reported one.
        // ═════════════════════════════════════════════════════════════════════
        public static class Targets
        {
            const string ScreensRoot = "Canvas/ScreensRoot/";

            static Target Screen(string name, string title, ScreenId screen, string path,
                                 Func<ScrollDragAnywhereRunner, IEnumerator>? open = null) => new Target
            {
                Name = name, Title = title,
                Open = open ?? (r => r.ShowScreenLikeTheWidget(screen)),
                Resolve = () => ScrollDragAnywhereRunner.ByPath(ScreensRoot + path)?.GetComponent<ScrollRect>(),
            };

            public static List<Target> RankingsOnly() => new List<Target> { Rankings() };

            public static Target Rankings() => new Target
            {
                Name = "rankings", Title = "Rankings",
                Open = r => r.ClickReal(() => ScrollDragAnywhereRunner.ByPath("Canvas/ScreensRoot/HomeScreen/LeaderboardButton")?.GetComponent<Button>(),
                                        "Home ▸ LeaderboardButton.onClick", ScreenId.Leaderboard),
                Resolve = () => ScrollDragAnywhereRunner.ByPath(ScreensRoot + "RankingsScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea")?.GetComponent<ScrollRect>(),
            };

            public static List<Target> Sweep() => new List<Target>
            {
                Rankings(),

                // ── the four clones of the rankings block ─────────────────────────────
                Screen("general_shop", "General Shop (STORE tab)", ScreenId.GeneralShop,
                    "GeneralShopScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea",
                    r => r.OpenShopTab(store: true)),

                new Target
                {
                    Name = "gacha_rates", Title = "Gacha rates modal",
                    Open = r => r.OpenGachaRates(),
                    Resolve = () => GolfinRedux.UI.Gacha.GachaRatesModalController.Instance?.GetComponentInChildren<ScrollRect>(true),
                    Close = () => GolfinRedux.UI.Gacha.GachaRatesModalController.Instance?.Hide(),
                },

                Screen("stamina_shop", "Stamina shop", ScreenId.StaminaShopSelection,
                    "StaminaShopSelectionScreen/SafeArea/CardsPanel/Modal/Bottom97/ScrollArea",
                    r => r.OpenViaRoster<Golfin.Roster.CharacterDetailPanel>("boostButton", "Roster ▸ BoostButton.onClick", ScreenId.StaminaShopSelection)),

                Screen("tournament_selection", "Tournament selection", ScreenId.TournamentSelection,
                    "TournamentSelectionScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea"),

                Screen("tournament_leaderboard", "Tournament leaderboard", ScreenId.TournamentLeaderboard,
                    "TournamentLeaderboardScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea"),

                // ── lists with no raycast graphic at all ───────────────────────────────
                new Target
                {
                    Name = "loan_modal", Title = "Loan modal (recipients)",
                    Open = r => r.OpenLoanModal(),
                    Resolve = () => ScrollDragAnywhereRunner.RosterLoanModal()?.GetComponentInChildren<ScrollRect>(true),
                    Close = () => ScrollDragAnywhereRunner.RosterLoanModal()?.Hide(),
                },

                new Target
                {
                    Name = "item_use", Title = "Item use modal",
                    Open = r => r.OpenItemUse(),
                    Resolve = () => ScrollDragAnywhereRunner.ByPath(ScreensRoot + "InventoryScreen/ContentArea/ItemsContent/ItemUseModal/ModalPanel/ModalContainer/ScrollArea")?.GetComponent<ScrollRect>(),
                    Close = () => ScrollDragAnywhereRunner.ByPath(ScreensRoot + "InventoryScreen/ContentArea/ItemsContent/ItemUseModal")?.GetComponent<ModalController>()?.Hide(),
                },

                new Target
                {
                    Name = "bag_club", Title = "Bag club modal",
                    Open = r => r.OpenBagClub(),
                    Resolve = () => ScrollDragAnywhereRunner.ByPath(ScreensRoot + "InventoryScreen/ContentArea/BagsClubModal/ModalPanel/ModalContainer/ScrollArea")?.GetComponent<ScrollRect>(),
                    Close = () => ScrollDragAnywhereRunner.ByPath(ScreensRoot + "InventoryScreen/ContentArea/BagsClubModal")?.GetComponent<ModalController>()?.Hide(),
                },

                Screen("gps_vote", "GPS vote", ScreenId.GpsVote, "GpsVoteScreen/ContentContainer/VoteList",
                    r => r.OpenGpsVote()),

                new Target
                {
                    Name = "venue_picker", Title = "Venue picker modal",
                    Open = r => r.OpenVenuePicker(),
                    Resolve = () => ScrollDragAnywhereRunner.VenuePicker()?.GetComponentInChildren<ScrollRect>(true),
                    Close = () => ScrollDragAnywhereRunner.VenuePicker()?.Hide(),
                },

                Screen("login", "Login form", ScreenId.Login, "LoginScreen/CardBorder/CardBody/ScrollView"),

                // Login's FORGOT PASSWORD sends an e-mail; the form itself opens from the recovery
                // deep link (LoginScreenController.OnEnable → ShowScreen(ResetPassword)) — same call.
                Screen("reset_password", "Reset-password form", ScreenId.ResetPassword,
                    "ResetPasswordScreen/CardBorder/CardBody/ScrollView"),

                Screen("signup", "Sign-up form", ScreenId.SignUp, "SignUpScreen/CardBorder/CardBody/ScrollView",
                    r => r.OpenFromLogin("_createAccountButton", "Login ▸ CreateAccount.onClick", ScreenId.SignUp)),

                Screen("create_username", "Create-username form", ScreenId.CreateUsername,
                    "CreateUsernameScreen/CardBorder/CardBody/ScrollView"),
            };
        }
    }

    /// <summary>Boots through the real title gate, then for every target: opens it, closes any
    /// first-visit hints, presses twice, writes the verdict JSON + caption sidecar.</summary>
    public class ScrollDragAnywhereRunner : MonoBehaviour
    {
        const int   DragFrames   = 24;
        const float DragStepPx   = 26f;    // 24 × 26 = 624 px of pull — unmistakable at 30 fps
        const float MovedMinPx   = 40f;    // anything under this is jitter, not a scroll
        const float OpenTimeout  = 25f;

        List<ScrollDragAnywhereVerify.Target> _targets = new();
        string _label = "", _outDir = "";
        bool   _record;
        float  _t0;
        readonly StringBuilder _log = new StringBuilder();
        readonly List<Result> _results = new List<Result>();
        readonly List<(float start, float end, string text)> _captions = new();

        /// <summary>Set by the current target's Open — the widget or call that reached the list.</summary>
        public string Entry = "";

        class Case
        {
            public string name = "", spot = "";
            public Vector2 press;
            public string topHit = "<none>";
            public string dragHandler = "<none>";
            public bool handlerIsTarget;
            public float contentYBefore, contentYAfter;
            public bool moved, pass;
            public float tPress, tRelease;
            public string stillRest = "", stillAfter = "";
        }

        class Result
        {
            public string name = "", title = "", entry = "", scrollRect = "", status = "INCONCLUSIVE", note = "";
            public bool scrollable;
            public int rows;
            public float contentH, viewportH;
            public readonly List<Case> cases = new List<Case>();
        }

        public void Begin(List<ScrollDragAnywhereVerify.Target> targets, string label, bool record, string outDir, float t0)
        {
            _targets = targets; _label = label; _record = record; _outDir = outDir; _t0 = t0;
            Application.runInBackground = true;   // else an unfocused Editor stops rendering
            StartCoroutine(Run());
        }

        float Now => Time.realtimeSinceStartup - _t0;

        IEnumerator Run()
        {
            Line($"=== drag-anywhere ×{_targets.Count} label={_label} record={_record} {DateTime.UtcNow:u} ===");
            yield return Boot();

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                var res = new Result { name = t.Name, title = t.Title };
                _results.Add(res);
                Entry = "";
                Line($"--- [{i + 1}/{_targets.Count}] {t.Name}");
                Caption(Now, Now + 1.6f, $"{i + 1}/{_targets.Count}  {t.Title}");

                string? fail = null;
                yield return Guarded(t.Open(this), OpenTimeout, m => fail = m);
                res.entry = Entry;
                if (fail != null)
                {
                    res.note = "open failed: " + fail; Line("  " + res.note);
                    yield return SafeClose(t); WriteVerdict(partial: true); continue;
                }
                yield return new WaitForSecondsRealtime(1.2f);
                yield return DismissScreenHints();
                yield return new WaitForSecondsRealtime(0.8f);

                ScrollRect? sr = null;
                try { sr = t.Resolve(); } catch (Exception e) { res.note = "resolve threw: " + e.Message; }
                if (sr == null || sr.content == null || sr.viewport == null || !sr.gameObject.activeInHierarchy)
                {
                    if (res.note == "") res.note = sr == null ? "scroll rect not found" : "scroll rect inactive";
                    Line("  " + res.note);
                    yield return SafeClose(t); WriteVerdict(partial: true); continue;
                }
                res.scrollRect = FullPath(sr.transform);

                yield return Guarded(Probe(sr, res), 40f, m => { res.note = "probe failed: " + m; });
                Line($"  => {res.status} {res.note}");
                yield return SafeClose(t);
                WriteVerdict(partial: true);   // a recompile mid-play kills this coroutine; keep what finished
                yield return new WaitForSecondsRealtime(0.4f);
            }

            Line("=== summary ===");
            foreach (Result r in _results) Line($"  {r.status,-12} {r.name,-24} {r.note}");
            WriteVerdict();
            if (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen != ScreenId.Home)
                ScreenManager.Instance.ShowScreen(ScreenId.Home);
            yield return new WaitForSecondsRealtime(0.8f);
            EditorApplication.isPlaying = false;
        }

        IEnumerator SafeClose(ScrollDragAnywhereVerify.Target t)
        {
            try { t.Close?.Invoke(); } catch (Exception e) { Line("  close threw: " + e.Message); }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ── the probe ────────────────────────────────────────────────────────

        IEnumerator Probe(ScrollRect sr, Result res)
        {
            Canvas.ForceUpdateCanvases();
            var rows = new List<RectTransform>();
            foreach (Transform c in sr.content) if (c.gameObject.activeInHierarchy) rows.Add((RectTransform)c);
            // Visual order, topmost first — sibling order lies (the shop keeps its banner as child 0
            // and draws it under the last card).
            rows.Sort((a, b) => TopEdge(b).CompareTo(TopEdge(a)));
            res.rows = rows.Count;
            res.contentH = sr.content.rect.height; res.viewportH = sr.viewport.rect.height;
            res.scrollable = sr.vertical && res.contentH > res.viewportH + MovedMinPx;
            Line($"  rows={rows.Count} content.h={res.contentH:0.#} viewport.h={res.viewportH:0.#} " +
                 $"scrollable={res.scrollable} screen={Screen.width}x{Screen.height}");

            // The same camera GraphicRaycaster.eventCamera resolves: none for an overlay canvas,
            // even though ShellScene's root Canvas carries a camera reference it does not use.
            Canvas root = sr.GetComponentInParent<Canvas>().rootCanvas;
            Camera? cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

            if (res.scrollable) { sr.verticalNormalizedPosition = 1f; yield return new WaitForSecondsRealtime(0.4f); Canvas.ForceUpdateCanvases(); }

            (Vector2 empty, string spot)? e = FindEmptySpot(sr, rows, cam, Line);
            if (e == null)
            {
                res.status = "INCONCLUSIVE"; res.note = "no empty spot inside the viewport (every pixel is a child)";
                yield break;
            }
            Line($"  empty spot: {e.Value.spot} -> {e.Value.empty}");
            yield return Press(sr, res, "empty", e.Value.spot, e.Value.empty,
                $"Press on EMPTY list space\\n({e.Value.spot}), then drag up");

            Vector2? control = FindControlSpot(rows, cam);
            if (control != null)
                yield return Press(sr, res, "control", "first row's label", control.Value,
                    "Control: press on a label in the\\nfirst row (the press that always worked)");
            else Line("  (no plain label in the first row — control press skipped)");

            Case? em = res.cases.Find(c => c.name == "empty");
            Case? ct = res.cases.Find(c => c.name == "control");
            bool controlOk = ct == null || ct.pass;
            res.status = !controlOk ? "INCONCLUSIVE" : (em != null && em.pass ? "PASS" : "FAIL");
            if (!controlOk) res.note = "control press did not resolve the list — instrument fault, not a verdict";
            else if (em != null && em.pass && !res.scrollable) res.note = "handler resolved; list too short to scroll";
        }

        /// <summary>
        /// The first spot the finder can PROVE is empty: inside the viewport and inside no active
        /// child rect. Candidates in order: the gap under the first row, the space under the
        /// last row, the left margin beside the first row, the viewport centre when there are no
        /// rows at all. World corners throughout — never a layout number.
        /// </summary>
        static (Vector2, string)? FindEmptySpot(ScrollRect sr, List<RectTransform> rows, Camera? cam, Action<string> log)
        {
            var vp = new Vector3[4]; sr.viewport.GetWorldCorners(vp);
            Rect view = Rect.MinMaxRect(vp[0].x, vp[0].y, vp[2].x, vp[2].y);
            var rects = new List<Rect>(rows.Count);
            foreach (RectTransform r in rows) { var c = new Vector3[4]; r.GetWorldCorners(c); rects.Add(Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y)); }
            log($"  viewport world rect x[{view.xMin:0.#}..{view.xMax:0.#}] y[{view.yMin:0.#}..{view.yMax:0.#}]");
            for (int i = 0; i < Mathf.Min(3, rects.Count); i++)
                log($"  row[{i}] {rows[i].name} x[{rects[i].xMin:0.#}..{rects[i].xMax:0.#}] y[{rects[i].yMin:0.#}..{rects[i].yMax:0.#}]");
            var candidates = new List<(Vector3, string)>();

            if (rects.Count == 0)
                candidates.Add((new Vector3(view.center.x, view.center.y, vp[0].z), "empty list, viewport centre"));
            else
            {
                Rect first = rects[0];
                // the first child that starts BELOW the first row's bottom edge (a grid's second row)
                for (int j = 1; j < rects.Count; j++)
                {
                    if (rects[j].yMax >= first.yMin - 2f) continue;
                    float gap = first.yMin - rects[j].yMax;
                    if (gap >= 8f) candidates.Add((new Vector3(first.center.x, first.yMin - gap * 0.5f, vp[0].z), "gap between the first two rows"));
                    break;
                }
                // two children side by side (a grid): the column gap at the first row's centre
                if (rects.Count > 1 && rects[1].yMax > first.yMin + 2f && rects[1].xMin - first.xMax >= 8f)
                    candidates.Add((new Vector3((first.xMax + rects[1].xMin) * 0.5f, first.center.y, vp[0].z), "gap between the first two columns"));
                float lowest = float.MaxValue; foreach (Rect r in rects) lowest = Mathf.Min(lowest, r.yMin);
                float below = lowest - view.yMin;
                if (below >= 24f) candidates.Add((new Vector3(view.center.x, lowest - Mathf.Min(40f, below * 0.5f), vp[0].z), "space under the last row"));
                float left = float.MaxValue; foreach (Rect r in rects) left = Mathf.Min(left, r.xMin);
                float margin = left - view.xMin;
                if (margin >= 12f) candidates.Add((new Vector3(view.xMin + margin * 0.5f, first.center.y, vp[0].z), "left margin beside the first row"));
            }

            foreach ((Vector3 world, string spot) in candidates)
            {
                bool inView = view.Contains(new Vector2(world.x, world.y));
                int insideChild = -1;
                for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(new Vector2(world.x, world.y))) { insideChild = i; break; }
                log($"  candidate '{spot}' ({world.x:0.#}, {world.y:0.#}) inView={inView} insideChild={(insideChild < 0 ? "no" : rows[insideChild].name)}");
                if (!inView || insideChild >= 0) continue;
                return (RectTransformUtility.WorldToScreenPoint(cam, world), spot);
            }
            return null;
        }

        static float TopEdge(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return c[1].y; }

        /// <summary>A plain label in the first row: a TMP text with no IDragHandler between it and
        /// the row root, so the press is the list's to own (an InputField owns its own drag; a
        /// Button does not, so a label inside a tappable card still counts).</summary>
        static Vector2? FindControlSpot(List<RectTransform> rows, Camera? cam)
        {
            if (rows.Count == 0) return null;
            foreach (TextMeshProUGUI tmp in rows[0].GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (!tmp.raycastTarget || !tmp.gameObject.activeInHierarchy) continue;
                bool ownsDrag = false;   // an InputField / Slider between label and row owns the drag
                for (Transform? p = tmp.transform; p != null && p != rows[0].parent; p = p.parent)
                    if (p.GetComponent<IDragHandler>() != null) { ownsDrag = true; break; }
                if (ownsDrag) continue;
                var rt = (RectTransform)tmp.transform;
                return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            }
            return null;
        }

        /// <summary>
        /// One press, resolved the way the input module resolves it: raycast, bubble up from the
        /// top hit for an IDragHandler, dispatch to THAT. No handler → nothing dispatched.
        /// </summary>
        IEnumerator Press(ScrollRect sr, Result res, string name, string spot, Vector2 point, string caption)
        {
            var c = new Case { name = name, spot = spot, press = point, contentYBefore = sr.content.anchoredPosition.y };
            if (!_record && name == "empty") { yield return new WaitForEndOfFrame(); c.stillRest = Still($"{res.name}_rest"); }
            var es = EventSystem.current;
            var ped = new PointerEventData(es)
            {
                button = PointerEventData.InputButton.Left,
                position = point, pressPosition = point,
            };
            var hits = new List<RaycastResult>();
            es.RaycastAll(ped, hits);
            RaycastResult top = hits.Count > 0 ? hits[0] : default;
            GameObject? handler = top.gameObject != null ? ExecuteEvents.GetEventHandler<IDragHandler>(top.gameObject) : null;
            c.topHit          = top.gameObject != null ? FullPath(top.gameObject.transform) : "<none>";
            c.dragHandler     = handler != null ? FullPath(handler.transform) : "<none>";
            c.handlerIsTarget = handler == sr.gameObject;
            c.tPress = Now;
            Line($"  · {name} press={point} @ {c.tPress:0.00}s");
            Line($"      top hit      : {c.topHit}");
            Line($"      drag handler : {c.dragHandler}  (target={c.handlerIsTarget})");
            // Two windows that never overlap: the press caption runs for the drag, the verdict
            // caption takes over at release (the caption tool stacks overlapping windows).
            Caption(c.tPress, c.tPress + DragFrames / 30f + 0.4f, caption);

            if (handler != null)
            {
                ped.pointerEnter = top.gameObject;
                ped.pointerPressRaycast = top;
                ped.pointerCurrentRaycast = top;
                ped.pointerDrag = handler;
                ExecuteEvents.Execute(handler, ped, ExecuteEvents.initializePotentialDrag);
                // The module begins the drag once the pointer moves past the drag threshold.
                ped.dragging = true;
                ExecuteEvents.Execute(handler, ped, ExecuteEvents.beginDragHandler);
                for (int i = 0; i < DragFrames; i++)
                {
                    ped.delta = new Vector2(0f, DragStepPx);
                    ped.position += ped.delta;
                    ExecuteEvents.Execute(handler, ped, ExecuteEvents.dragHandler);
                    yield return null;
                }
                ExecuteEvents.Execute(handler, ped, ExecuteEvents.endDragHandler);
                ped.dragging = false; ped.pointerDrag = null;
            }
            else
            {
                // Nothing owns the press — hold the same beat so the clip shows a dead drag.
                for (int i = 0; i < DragFrames; i++) yield return null;
            }
            c.tRelease = Now;
            yield return new WaitForSecondsRealtime(1.2f);   // inertia settles

            c.contentYAfter = sr.content.anchoredPosition.y;
            c.moved = Mathf.Abs(c.contentYAfter - c.contentYBefore) >= MovedMinPx;
            c.pass  = c.handlerIsTarget && (c.moved || !res.scrollable);
            if (!_record && name == "empty") { yield return new WaitForEndOfFrame(); c.stillAfter = Still($"{res.name}_after"); }
            Line($"      content.y    : {c.contentYBefore:0.#} -> {c.contentYAfter:0.#}  moved={c.moved}  pass={c.pass}");
            Caption(c.tRelease + 0.4f, c.tRelease + 2.4f,
                c.pass ? (c.moved ? $"Scrolled {Mathf.Abs(c.contentYAfter - c.contentYBefore):0} px"
                                  : "Drag reached the list\\n(too short to scroll)")
                       : (handler == null ? "Nothing under the press owns a drag\\n- the list did not move"
                                          : "Press owned by " + handler.name + "\\n- the list did not move"));
            res.cases.Add(c);

            // Hold the scrolled frame for the verdict caption's whole window, THEN snap back —
            // a caption that outlives the frame it describes is the caption lesson, relearned.
            yield return new WaitForSecondsRealtime(1.3f);
            if (res.scrollable) sr.verticalNormalizedPosition = 1f;    // back to the top for the next press
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // ── openers (public: the Target table calls them) ────────────────────

        /// <summary>The call the opening widget itself makes (mode-carousel card, nav button…).</summary>
        public IEnumerator ShowScreenLikeTheWidget(ScreenId screen)
        {
            Entry = $"ScreenManager.ShowScreen({screen}) — the call the opening widget makes";
            ScreenManager.Instance?.ShowScreen(screen);
            yield return WaitScreen(screen);
        }

        /// <summary>The bottom nav bar is only on the pillar screens; a player is on Home before
        /// tapping it. Same here.</summary>
        IEnumerator EnsureHome()
        {
            if (ScreenManager.Instance?.CurrentScreen == ScreenId.Home) yield break;
            ScreenManager.Instance?.ShowScreen(ScreenId.Home);
            yield return WaitScreen(ScreenId.Home);
        }

        /// <summary>Bottom-nav gacha slot → Rewards Center, then the real STORE / GACHA tab
        /// (GachaTabController wires the clone's WeeklyTab as STORE and DailyTab as GACHA).</summary>
        public IEnumerator OpenShopTab(bool store)
        {
            if (ScreenManager.Instance?.CurrentScreen != ScreenId.GeneralShop)
            {
                yield return EnsureHome();
                yield return ClickReal(() => PersistentUIManager.Instance?.gachaButton, "PersistentUI ▸ gachaButton.onClick", ScreenId.GeneralShop);
                yield return DismissScreenHints();
            }
            var tabs = FindObjectOfType<GolfinRedux.UI.Gacha.GachaTabController>(false);
            string field = store ? "_weeklyTab" : "_dailyTab";
            yield return ClickReal(() => tabs != null ? Field<Button>(tabs, field) : null,
                                   store ? "Rewards Center ▸ STORE tab.onClick" : "Rewards Center ▸ GACHA tab.onClick", null);
            yield return new WaitForSecondsRealtime(1.0f);
        }

        /// <summary>Tap a real Button and wait for the screen it opens.</summary>
        public IEnumerator ClickReal(Func<Button?> find, string entry, ScreenId? expect)
        {
            Button? b = find();
            if (b == null || !b.gameObject.activeInHierarchy) throw new Exception("real button not reachable: " + entry);
            Entry = entry;
            Line("  tapping the real " + entry);
            b.onClick.Invoke();
            if (expect.HasValue) yield return WaitScreen(expect.Value);
            else yield return new WaitForSecondsRealtime(0.8f);
        }

        /// <summary>The Characters slot of the nav bar returns to the PILLAR's last screen
        /// (nav_back_memory) — after a Boost visit that is the stamina shop, not the roster. A
        /// player taps BACK from there; the same ShowScreen(Roster) call is made here.</summary>
        IEnumerator GoRoster()
        {
            yield return EnsureHome();
            yield return ClickReal(() => PersistentUIManager.Instance?.charactersButton, "PersistentUI ▸ charactersButton.onClick", null);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen == ScreenId.Home &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (ScreenManager.Instance?.CurrentScreen != ScreenId.Roster)
            {
                Entry += $" → pillar memory landed on {ScreenManager.Instance?.CurrentScreen}; BACK = ShowScreen(Roster)";
                ScreenManager.Instance?.ShowScreen(ScreenId.Roster);
            }
            yield return WaitScreen(ScreenId.Roster);
        }

        public IEnumerator OpenViaRoster<T>(string buttonField, string entry, ScreenId expect) where T : MonoBehaviour
        {
            yield return GoRoster();
            yield return DismissScreenHints();
            yield return new WaitForSecondsRealtime(0.6f);
            var panel = FindObjectOfType<T>(false);
            yield return ClickReal(() => panel != null ? Field<Button>(panel, buttonField) : null, entry, expect);
        }

        public IEnumerator OpenFromLogin(string buttonField, string entry, ScreenId expect)
        {
            if (ScreenManager.Instance?.CurrentScreen != ScreenId.Login) yield return ShowScreenLikeTheWidget(ScreenId.Login);
            var login = FindObjectOfType<Golfin.UI.Account.LoginScreenController>(false);
            Button? b = login != null ? Field<Button>(login, buttonField) : null;
            if (b != null && b.gameObject.activeInHierarchy) yield return ClickReal(() => b, entry, expect);
            else yield return ShowScreenLikeTheWidget(expect);
        }

        public IEnumerator OpenGachaRates()
        {
            yield return OpenShopTab(store: false);
            Button? rules = null;
            foreach (var card in FindObjectsByType<GolfinRedux.UI.Gacha.GachaBannerCard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                rules = Field<Button>(card, "_rulesButton");
                if (rules != null && rules.gameObject.activeInHierarchy) break;
                rules = null;
            }
            var modal = GolfinRedux.UI.Gacha.GachaRatesModalController.Instance;
            if (modal == null) throw new Exception("no GachaRatesModalController in the scene");
            yield return ClickReal(() => rules, "GachaBannerCard ▸ RULES button.onClick", null);
            yield return WaitVisible(modal);
        }

        public static Golfin.UI.Loans.LoanModalController? RosterLoanModal()
        {
            var panel = FindObjectOfType<Golfin.Roster.CharacterDetailPanel>(false);
            return panel != null ? Field<Golfin.UI.Loans.LoanModalController>(panel, "loanModal") : null;
        }

        public IEnumerator OpenLoanModal()
        {
            if (ScreenManager.Instance?.CurrentScreen != ScreenId.Roster) yield return GoRoster();
            yield return DismissScreenHints();
            yield return new WaitForSecondsRealtime(0.6f);
            var panel = FindObjectOfType<Golfin.Roster.CharacterDetailPanel>(false);
            var modal = RosterLoanModal();
            if (modal == null) throw new Exception("CharacterDetailPanel.loanModal not wired");
            yield return ClickReal(() => panel != null ? Field<Button>(panel, "lendButton") : null, "Roster ▸ LendButton.onClick", null);
            yield return WaitVisible(modal);
        }

        IEnumerator OpenInventoryTab(string tabName)
        {
            if (ScreenManager.Instance?.CurrentScreen != ScreenId.Inventory)
            {
                yield return EnsureHome();
                yield return ClickReal(() => PersistentUIManager.Instance?.inventoryButton, "PersistentUI ▸ inventoryButton.onClick", ScreenId.Inventory);
            }
            yield return DismissScreenHints();
            Button? tab = null;
            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b.name == tabName && HasAncestor(b.transform, "InventoryScreen")) { tab = b; break; }
            yield return ClickReal(() => tab, $"Inventory ▸ {tabName}.onClick", null);
            yield return new WaitForSecondsRealtime(1.0f);
        }

        public IEnumerator OpenItemUse()
        {
            yield return OpenInventoryTab("ITEMSTab");
            var panel = FindObjectOfType<Golfin.Inventory.ItemDetailPanel>(false);
            var modal = panel != null ? Field<Golfin.Inventory.ItemUseModalController>(panel, "useModal") : null;
            if (modal == null) throw new Exception("ItemDetailPanel.useModal not wired / panel inactive");
            yield return ClickReal(() => panel != null ? Field<Button>(panel, "useButton") : null, "Inventory ▸ Items ▸ UseButton.onClick", null);
            yield return WaitVisible(modal);
        }

        public IEnumerator OpenBagClub()
        {
            yield return OpenInventoryTab("BAGSTab");
            var panel = FindObjectOfType<Golfin.Inventory.BagDetailPanel>(false);
            var modal = panel != null ? Field<Golfin.Inventory.BagClubModalController>(panel, "clubModal") : null;
            if (modal == null) throw new Exception("BagDetailPanel.clubModal not wired / panel inactive");
            // The slot cards are spawned; their Button is the real opener (BagDetailPanel wires it).
            Button? slot = null;
            Transform? grid = Field<Transform>(panel!, "clubGridParent");
            if (grid != null)
                foreach (Button b in grid.GetComponentsInChildren<Button>(false))
                    if (HasAncestorContaining(b.transform, "Empty")) { slot = b; break; }   // BagEmptyClubCard → Equip
            if (slot != null)
            {
                yield return ClickReal(() => slot, "Inventory ▸ Bags ▸ empty slot card EQUIP.onClick", null);
            }
            else
            {
                Entry = "BagClubModalController.Open(slot 0, Equip) — the call the slot card makes";
                modal.Open(0, Golfin.Inventory.BagClubModalMode.Equip, null);
            }
            yield return WaitVisible(modal);
        }

        public IEnumerator OpenGpsVote()
        {
            if (ScreenManager.Instance?.CurrentScreen != ScreenId.Home) yield return ShowScreenLikeTheWidget(ScreenId.Home);
            yield return ClickReal(() => ByPath("Canvas/ScreensRoot/HomeScreen/GpsPill")?.GetComponent<Button>(), "Home ▸ GpsPill.onClick", ScreenId.GpsHub);
            yield return DismissScreenHints();
            var hub = FindObjectOfType<Golfin.Gps.UI.GpsHubScreenController>(false);
            yield return ClickReal(() => hub != null ? Field<Button>(hub, "_tileVoteButton") : null, "GpsHub ▸ VOTE tile.onClick", ScreenId.GpsVote);
        }

        public static Golfin.Gps.UI.VenuePickerModalController? VenuePicker()
        {
            var flow = FindObjectOfType<Golfin.Gps.UI.ScoreUploadFlowController>(true);
            return flow != null ? Field<Golfin.Gps.UI.VenuePickerModalController>(flow, "_venuePicker") : null;
        }

        public IEnumerator OpenVenuePicker()
        {
            yield return ShowScreenLikeTheWidget(ScreenId.ScoreUpload);
            yield return DismissScreenHints();
            yield return new WaitForSecondsRealtime(0.8f);
            var flow = FindObjectOfType<Golfin.Gps.UI.ScoreUploadFlowController>(false);
            var picker = VenuePicker();
            if (picker == null) throw new Exception("ScoreUploadFlowController._venuePicker not wired");
            Button? choose = flow != null ? Field<Button>(flow, "_chooseManuallyButton") : null;
            if (choose != null && choose.gameObject.activeInHierarchy && choose.interactable)
                yield return ClickReal(() => choose, "ScoreUpload ▸ CHOOSE MANUALLY.onClick", null);
            else
            {
                Entry = "VenuePickerModalController.Open — the call CHOOSE MANUALLY makes (button not on screen yet)";
                picker.Open(_ => { });
            }
            yield return WaitVisible(picker);
        }

        // ── plumbing ─────────────────────────────────────────────────────────

        /// <summary>
        /// Drives a coroutine (nested yields flattened) so an exception or a hang inside one
        /// target's opener is reported and the sweep continues with the next list.
        /// </summary>
        static IEnumerator Guarded(IEnumerator inner, float timeoutSeconds, Action<string> onFail)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var stack = new Stack<IEnumerator>(); stack.Push(inner);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e) { onFail(e.Message); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                if (Time.realtimeSinceStartup > deadline) { onFail($"timeout after {timeoutSeconds:0}s"); yield break; }
                yield return top.Current;
            }
        }

        IEnumerator WaitScreen(ScreenId id)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen != id &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (ScreenManager.Instance == null || ScreenManager.Instance.CurrentScreen != id)
                throw new Exception($"screen did not open: {id} (current {ScreenManager.Instance?.CurrentScreen})");
            yield return new WaitForSecondsRealtime(1.1f);
        }

        IEnumerator WaitVisible(ModalController modal)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!modal.IsVisible() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!modal.IsVisible()) throw new Exception("modal did not open: " + modal.name);
            yield return new WaitForSecondsRealtime(1.0f);
        }

        static T? Field<T>(object host, string name) where T : class
        {
            for (Type? t = host.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo? f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return f.GetValue(host) as T;
            }
            return null;
        }

        IEnumerator Boot()
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (ScreenManager.Instance == null && Time.realtimeSinceStartup < deadline) yield return null;
            // The title gate: tap the real StartButton, exactly as the probes do.
            deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool tapped = false;
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (b.name == "StartButton" && b.gameObject.activeInHierarchy)
                    { Line("tapping the real StartButton"); b.onClick.Invoke(); tapped = true; break; }
                if (tapped) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            deadline = Time.realtimeSinceStartup + 20f;
            while (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen != ScreenId.Home &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return DismissScreenHints();
        }

        /// <summary>
        /// A first visit opens the screen's hints over the list (screen_hints §3.3) and their
        /// scrim owns every press until they are closed. A player taps CONTINUE / CLOSE through
        /// them; so does this — the REAL gold NextButton, until none is left on screen.
        /// </summary>
        IEnumerator DismissScreenHints()
        {
            for (int i = 0; i < 12; i++)
            {
                Button? next = null;
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (b.name == "NextButton" && b.interactable && HasAncestor(b.transform, "ScreenHintModal"))
                    { next = b; break; }
                if (next == null)
                {
                    if (i > 0) yield return new WaitForSecondsRealtime(0.6f);   // the scrim's fade-out
                    yield break;
                }
                if (i == 0) Caption(Now, Now + 2f, "First-visit hints closed through\\nthe real CONTINUE / CLOSE button");
                Line($"  hint modal: tapping the real {next.name}.onClick (#{i + 1})");
                next.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.9f);
            }
        }

        static bool HasAncestorContaining(Transform t, string fragment)
        {
            for (Transform? p = t; p != null; p = p.parent) if (p.name.Contains(fragment)) return true;
            return false;
        }

        static bool HasAncestor(Transform t, string name)
        {
            for (Transform? p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        void Caption(float start, float end, string text) => _captions.Add((start, end, text));

        string _lastStillMd5 = "";

        /// <summary>
        /// One still through the sanctioned path (CaptureCore.SnapPlayModeSafe, never a hand-rolled
        /// ReadPixels), then the two checks the memory demands of it: the file must exist, and it
        /// must not be byte-identical to the previous still (a stale frame for a new state).
        /// Copied into the media folder under a stable name; "" when the capture failed.
        /// </summary>
        string Still(string tag)
        {
            string src;
            try { src = CaptureCore.SnapPlayModeSafe($"draganywhere_{_label}_{tag}"); }
            catch (Exception e) { Line($"  still {tag}: snap threw {e.Message}"); return ""; }
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) { Line($"  still {tag}: NO FILE ({src})"); return ""; }
            byte[] bytes = File.ReadAllBytes(src);
            string md5;
            using (var h = System.Security.Cryptography.MD5.Create())
                md5 = BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            bool stale = md5 == _lastStillMd5;
            _lastStillMd5 = md5;
            string dir = Path.Combine(_outDir, "stills");
            Directory.CreateDirectory(dir);
            string dst = Path.Combine(dir, $"{_label}_{tag}.png");
            File.Copy(src, dst, overwrite: true);
            Line($"  still {tag}: {(stale ? "STALE (identical to the previous still) " : "")}{dst} ({bytes.Length / 1024} KB)");
            return stale ? "" : dst;
        }

        public static Transform? ByPath(string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != rootName) continue;
                if (slash < 0) return root.transform;
                Transform? t = root.transform.Find(path.Substring(slash + 1));
                if (t != null) return t;
            }
            return null;
        }

        static string FullPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        void WriteVerdict(bool partial = false)
        {
            Directory.CreateDirectory(_outDir);
            int pass = 0, fail = 0, inc = 0;
            foreach (Result r in _results) { if (r.status == "PASS") pass++; else if (r.status == "FAIL") fail++; else inc++; }
            var j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine($"  \"label\": \"{Esc(_label)}\",");
            j.AppendLine($"  \"complete\": {(partial ? "false" : "true")}, \"planned\": {_targets.Count},");
            j.AppendLine($"  \"screen\": [{Screen.width}, {Screen.height}],");
            j.AppendLine($"  \"summary\": {{ \"pass\": {pass}, \"fail\": {fail}, \"inconclusive\": {inc} }},");
            j.AppendLine("  \"targets\": [");
            for (int i = 0; i < _results.Count; i++)
            {
                Result r = _results[i];
                j.AppendLine("    {");
                j.AppendLine($"      \"name\": \"{r.name}\",");
                j.AppendLine($"      \"title\": \"{Esc(r.title)}\",");
                j.AppendLine($"      \"entry\": \"{Esc(r.entry)}\",");
                j.AppendLine($"      \"scrollRect\": \"{Esc(r.scrollRect)}\",");
                j.AppendLine($"      \"rows\": {r.rows}, \"contentH\": {F(r.contentH)}, \"viewportH\": {F(r.viewportH)}, \"scrollable\": {(r.scrollable ? "true" : "false")},");
                j.AppendLine($"      \"status\": \"{r.status}\",");
                j.AppendLine($"      \"note\": \"{Esc(r.note)}\",");
                j.AppendLine("      \"cases\": [");
                for (int k = 0; k < r.cases.Count; k++)
                {
                    Case c = r.cases[k];
                    j.AppendLine("        {");
                    j.AppendLine($"          \"name\": \"{c.name}\", \"spot\": \"{Esc(c.spot)}\",");
                    j.AppendLine($"          \"press\": [{F(c.press.x)}, {F(c.press.y)}],");
                    j.AppendLine($"          \"topHit\": \"{Esc(c.topHit)}\",");
                    j.AppendLine($"          \"dragHandler\": \"{Esc(c.dragHandler)}\",");
                    j.AppendLine($"          \"handlerIsTarget\": {(c.handlerIsTarget ? "true" : "false")},");
                    j.AppendLine($"          \"contentYBefore\": {F(c.contentYBefore)}, \"contentYAfter\": {F(c.contentYAfter)}, \"moved\": {(c.moved ? "true" : "false")},");
                    j.AppendLine($"          \"tPress\": {F(c.tPress)}, \"tRelease\": {F(c.tRelease)},");
                    j.AppendLine($"          \"stillRest\": \"{Esc(c.stillRest)}\", \"stillAfter\": \"{Esc(c.stillAfter)}\",");
                    j.AppendLine($"          \"pass\": {(c.pass ? "true" : "false")}");
                    j.AppendLine("        }" + (k < r.cases.Count - 1 ? "," : ""));
                }
                j.AppendLine("      ]");
                j.AppendLine("    }" + (i < _results.Count - 1 ? "," : ""));
            }
            j.AppendLine("  ]");
            j.AppendLine("}");
            File.WriteAllText(Path.Combine(_outDir, $"drag_anywhere_{_label}.json"), j.ToString());
            if (partial) return;
            WriteCaptions();
            Line($"verdict -> {_outDir}/drag_anywhere_{_label}.json  (pass {pass} / fail {fail} / inconclusive {inc})");
        }

        void WriteCaptions()
        {
            var cj = new StringBuilder();
            cj.AppendLine("{ \"captions\": [");
            for (int i = 0; i < _captions.Count; i++)
            {
                var c = _captions[i];
                cj.AppendLine($"  {{ \"start\": {F(c.start)}, \"end\": {F(c.end)}, \"text\": \"{Esc(c.text)}\" }}" +
                              (i < _captions.Count - 1 ? "," : ""));
            }
            cj.AppendLine("] }");
            File.WriteAllText(Path.Combine(_outDir, $"captions_{_label}.json"), cj.ToString());
        }

        void OnDestroy()
        {
            // Play mode ended under us (a recompile, or a hand on the Play button): keep the
            // caption sidecar for whatever was recorded, so a partial clip is still cuttable.
            if (_results.Count > 0 && _results.Count < _targets.Count) { WriteVerdict(partial: true); WriteCaptions(); }
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        void Line(string s)
        {
            _log.AppendLine(s);
            Debug.Log("[DragAnywhere] " + s);
            Directory.CreateDirectory(_outDir);
            File.WriteAllText(Path.Combine(_outDir, $"drag_anywhere_{_label}.log"), _log.ToString());
        }
    }
}
