// ─────────────────────────────────────────────────────────────────────────────
// rankings_list_drag_anywhere — can a drag that starts on EMPTY list space scroll the list?
//
// Cesar (2026-09-14): "in order to scroll the list of players right now the user has to
// click exactly on a player and then drag instead of dragging from anywhere in the list."
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
// TWO PRESSES PER RUN, and the second is the control:
//   gap        the vertical gap between the first two rows — pure list space.
//   row_label  the centre of the first row's NameLabel — the "exactly on a player" press
//              that always worked. If THIS one fails the instrument is wrong, not the list.
//
// THE VERDICT IS JSON (PIPELINE_HARDENING rule 3): per press, the top raycast hit, the
// resolved drag handler, content.anchoredPosition before and after, and a pass flag. The mp4
// is for Cesar; the JSON is the gate. The list is reached through the REAL entry — the title
// StartButton, then Home's LeaderboardButton.onClick — never ShowScreen behind the gate.
//
// Re-pointing at another list = another Target row. The audit that found this shape in 13
// other ScrollRects lives in the quick spec (Docs/Specs/Quick/rankings_list_drag_anywhere.md).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Golfin.Diagnostics.Runtime;
using GolfinRedux.UI;
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
        public readonly struct Target
        {
            public readonly string Name;
            public readonly string EntryButtonPath;   // a REAL Button the player taps from Home
            public readonly ScreenId Screen;          // where that tap lands
            public readonly string ScrollRectPath;
            public readonly string RowLabelPath;      // control press, relative to a row root
            public readonly string OutDir;

            public Target(string name, string entryButtonPath, ScreenId screen, string scrollRectPath,
                          string rowLabelPath, string outDir)
            {
                Name = name; EntryButtonPath = entryButtonPath; Screen = screen;
                ScrollRectPath = scrollRectPath; RowLabelPath = rowLabelPath; OutDir = outDir;
            }
        }

        public static readonly Target Rankings = new Target(
            "rankings",
            "Canvas/ScreensRoot/HomeScreen/LeaderboardButton",
            ScreenId.Leaderboard,
            "Canvas/ScreensRoot/RankingsScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea",
            "RankingsCard/Name+Level/NameLabel",
            "Docs/Specs/Quick/media/rankings_list_drag_anywhere");

        const string ArmedKey  = "ScrollDragAnywhereVerify.Armed";
        const string LabelKey  = "ScrollDragAnywhereVerify.Label";
        const string RecordKey = "ScrollDragAnywhereVerify.Record";
        static RecorderController? _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Game Polish/Verify — drag-anywhere on the Rankings list (record)", priority = 296)]
        public static void LaunchRankingsRecorded() => Launch("after", record: true);

        [MenuItem("GOLFIN/Game Polish/Verify — drag-anywhere on the Rankings list (no video)", priority = 297)]
        public static void LaunchRankingsLogOnly() => Launch("check", record: false);

        /// <summary>Arm and enter play mode. <paramref name="label"/> names the output files so
        /// a baseline run and an after-fix run sit side by side.</summary>
        public static void Launch(string label, bool record)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[DragAnywhere] Already playing — stop first."); return; }
            Directory.CreateDirectory(Rankings.OutDir);
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetString(LabelKey, label);
            SessionState.SetBool(RecordKey, record);
            EditorApplication.EnterPlaymode();
            Debug.Log($"[DragAnywhere] Armed ({label}, record={record}). Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); return; }
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);

            string label = SessionState.GetString(LabelKey, "run");
            bool record  = SessionState.GetBool(RecordKey, false);
            if (record) StartRecorder(Rankings.OutDir, label);

            var host = new GameObject("[ScrollDragAnywhereRunner]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ScrollDragAnywhereRunner>().Begin(Rankings, label, record, Time.realtimeSinceStartup);
        }

        // ── recorder (the GamePolishDemoRecorderC / GachaRevealDemoRecorder idiom) ─────────

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? t = asm.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                    if (t == null) continue;
                    var m = t.GetMethod("EnsureIPhone14Selected",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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
    }

    /// <summary>Boots through the real title gate, opens the list through its real entry
    /// button, presses twice, and writes the verdict JSON + caption sidecar.</summary>
    public class ScrollDragAnywhereRunner : MonoBehaviour
    {
        const int   DragFrames   = 24;
        const float DragStepPx   = 26f;    // 24 × 26 = 624 px of pull — unmistakable at 30 fps
        const float MovedMinPx   = 40f;    // anything under this is jitter, not a scroll

        ScrollDragAnywhereVerify.Target _target;
        string _label = "";
        bool   _record;
        float  _t0;
        readonly StringBuilder _log = new StringBuilder();
        readonly List<Case> _cases = new List<Case>();
        readonly List<(float start, float end, string text)> _captions = new();
        string _status = "INCONCLUSIVE";
        string _note   = "";

        class Case
        {
            public string name = "";
            public Vector2 press;
            public string topHit = "<none>";
            public string dragHandler = "<none>";
            public bool handlerIsTarget;
            public float contentYBefore, contentYAfter;
            public bool moved, pass;
            public float tPress, tRelease;
        }

        public void Begin(ScrollDragAnywhereVerify.Target target, string label, bool record, float t0)
        {
            _target = target; _label = label; _record = record; _t0 = t0;
            Application.runInBackground = true;   // else an unfocused Editor stops rendering
            StartCoroutine(Run());
        }

        float Now => Time.realtimeSinceStartup - _t0;

        IEnumerator Run()
        {
            Line($"=== drag-anywhere [{_target.Name}] label={_label} record={_record} {DateTime.UtcNow:u} ===");
            yield return Boot();

            // ── the REAL entry: Home's LeaderboardButton, not ShowScreen ─────────────────
            Transform? entry = ByPath(_target.EntryButtonPath);
            var entryButton = entry != null ? entry.GetComponent<Button>() : null;
            if (entryButton == null || !entryButton.gameObject.activeInHierarchy)
            {
                _note = "entry button not reachable: " + _target.EntryButtonPath;
                Line("  MISSING " + _note);
                yield return Finish();
                yield break;
            }
            Line("tapping the real " + entryButton.name + ".onClick");
            Caption(Now, Now + 2.5f, "Opened through the real\\nHome > Leaderboard button");
            entryButton.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 15f;
            while (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen != _target.Screen &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (ScreenManager.Instance == null || ScreenManager.Instance.CurrentScreen != _target.Screen)
            {
                _note = "screen did not open: " + _target.Screen;
                Line("  " + _note);
                yield return Finish();
                yield break;
            }
            // The cached board paints in OnEnable; the backend refresh may repaint once more.
            yield return new WaitForSecondsRealtime(1.5f);
            yield return DismissScreenHints();
            yield return new WaitForSecondsRealtime(1.5f);

            // ── the list ─────────────────────────────────────────────────────────────────
            Transform? t = ByPath(_target.ScrollRectPath);
            var sr = t != null ? t.GetComponent<ScrollRect>() : null;
            if (sr == null || sr.content == null || sr.viewport == null)
            {
                _note = "scroll rect not found: " + _target.ScrollRectPath;
                Line("  MISSING " + _note);
                yield return Finish();
                yield break;
            }
            Canvas.ForceUpdateCanvases();

            var rows = new List<RectTransform>();
            foreach (Transform c in sr.content) if (c.gameObject.activeSelf) rows.Add((RectTransform)c);
            float contentH = sr.content.rect.height, viewH = sr.viewport.rect.height;
            Line($"  rows={rows.Count} content.h={contentH:0.#} viewport.h={viewH:0.#} " +
                 $"screen={Screen.width}x{Screen.height}");
            if (rows.Count < 2 || contentH <= viewH + MovedMinPx)
            {
                _note = $"not scrollable: rows={rows.Count} content.h={contentH:0.#} viewport.h={viewH:0.#}";
                Line("  " + _note);
                yield return Finish();
                yield break;
            }

            // The same camera GraphicRaycaster.eventCamera resolves: none for an overlay canvas,
            // even though ShellScene's root Canvas carries a camera reference it does not use.
            Canvas root = sr.GetComponentInParent<Canvas>().rootCanvas;
            Camera? cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            var c0 = new Vector3[4]; var c1 = new Vector3[4];
            rows[0].GetWorldCorners(c0);   // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            rows[1].GetWorldCorners(c1);

            // gap: midway between row 0's bottom edge and row 1's top edge, at the row centre.
            Vector3 gapWorld = new Vector3((c0[0].x + c0[3].x) * 0.5f, (c0[0].y + c1[1].y) * 0.5f, c0[0].z);
            Vector2 gap = RectTransformUtility.WorldToScreenPoint(cam, gapWorld);
            Line($"  gap: row0.bottom={RectTransformUtility.WorldToScreenPoint(cam, c0[0]).y:0.#} " +
                 $"row1.top={RectTransformUtility.WorldToScreenPoint(cam, c1[1]).y:0.#} -> press {gap}");

            // control: the first row's name label — the press that always worked.
            Transform? label = rows[0].Find(_target.RowLabelPath);
            Vector2 rowLabel = Vector2.zero;
            if (label != null)
            {
                var lr = (RectTransform)label;
                rowLabel = RectTransformUtility.WorldToScreenPoint(cam, lr.TransformPoint(lr.rect.center));
            }
            else Line("  (no control label at " + _target.RowLabelPath + " — control press skipped)");

            sr.verticalNormalizedPosition = 1f;    // top of the list
            yield return new WaitForSecondsRealtime(0.5f);

            yield return Press(sr, "gap", gap,
                "Press on the GAP between two rows,\\nthen drag up");
            if (label != null)
                yield return Press(sr, "row_label", rowLabel,
                    "Control: press on a player's name\\n(the press that always worked)");

            Case? g = _cases.Find(c => c.name == "gap");
            Case? r = _cases.Find(c => c.name == "row_label");
            bool controlOk = r == null || r.pass;
            _status = !controlOk ? "INCONCLUSIVE" : (g != null && g.pass ? "PASS" : "FAIL");
            if (!controlOk) _note = "control press did not scroll — instrument fault, not a verdict";
            yield return Finish();
        }

        /// <summary>
        /// One press, resolved the way the input module resolves it: raycast, bubble up from
        /// the top hit for an IDragHandler, dispatch to THAT. No handler → nothing dispatched.
        /// </summary>
        IEnumerator Press(ScrollRect sr, string name, Vector2 point, string caption)
        {
            var c = new Case { name = name, press = point, contentYBefore = sr.content.anchoredPosition.y };
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
            Line($"--- {name} press={point} @ {c.tPress:0.00}s");
            Line($"    top hit      : {c.topHit}");
            Line($"    drag handler : {c.dragHandler}  (target={c.handlerIsTarget})");
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
            c.pass  = c.handlerIsTarget && c.moved;
            Line($"    content.y    : {c.contentYBefore:0.#} -> {c.contentYAfter:0.#}  moved={c.moved}  pass={c.pass}");
            Caption(c.tRelease + 0.4f, c.tRelease + 2.4f,
                c.pass ? $"Scrolled {Mathf.Abs(c.contentYAfter - c.contentYBefore):0} px"
                       : (handler == null ? "Nothing under the press owns a drag\\n- the list did not move"
                                          : "Did not scroll"));
            _cases.Add(c);

            // Hold the scrolled frame for the verdict caption's whole window, THEN snap back —
            // a caption that outlives the frame it describes is the caption lesson, relearned.
            yield return new WaitForSecondsRealtime(1.3f);
            sr.verticalNormalizedPosition = 1f;    // back to the top for the next press
            yield return new WaitForSecondsRealtime(0.8f);
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

        static bool HasAncestor(Transform t, string name)
        {
            for (Transform? p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        // ── plumbing ─────────────────────────────────────────────────────────

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
        }

        IEnumerator Finish()
        {
            Line($"=== {_status} {_note} ===");
            WriteVerdict();
            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        void Caption(float start, float end, string text) => _captions.Add((start, end, text));

        static Transform? ByPath(string path)
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

        void WriteVerdict()
        {
            Directory.CreateDirectory(_target.OutDir);
            var j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine($"  \"target\": \"{_target.Name}\",");
            j.AppendLine($"  \"label\": \"{Esc(_label)}\",");
            j.AppendLine($"  \"scrollRect\": \"{Esc(_target.ScrollRectPath)}\",");
            j.AppendLine($"  \"entry\": \"{Esc(_target.EntryButtonPath)}.onClick\",");
            j.AppendLine($"  \"screen\": [{Screen.width}, {Screen.height}],");
            j.AppendLine($"  \"status\": \"{_status}\",");
            j.AppendLine($"  \"note\": \"{Esc(_note)}\",");
            j.AppendLine("  \"cases\": [");
            for (int i = 0; i < _cases.Count; i++)
            {
                Case c = _cases[i];
                j.AppendLine("    {");
                j.AppendLine($"      \"name\": \"{c.name}\",");
                j.AppendLine($"      \"press\": [{F(c.press.x)}, {F(c.press.y)}],");
                j.AppendLine($"      \"topHit\": \"{Esc(c.topHit)}\",");
                j.AppendLine($"      \"dragHandler\": \"{Esc(c.dragHandler)}\",");
                j.AppendLine($"      \"handlerIsTarget\": {(c.handlerIsTarget ? "true" : "false")},");
                j.AppendLine($"      \"contentYBefore\": {F(c.contentYBefore)},");
                j.AppendLine($"      \"contentYAfter\": {F(c.contentYAfter)},");
                j.AppendLine($"      \"moved\": {(c.moved ? "true" : "false")},");
                j.AppendLine($"      \"tPress\": {F(c.tPress)},");
                j.AppendLine($"      \"tRelease\": {F(c.tRelease)},");
                j.AppendLine($"      \"pass\": {(c.pass ? "true" : "false")}");
                j.AppendLine("    }" + (i < _cases.Count - 1 ? "," : ""));
            }
            j.AppendLine("  ]");
            j.AppendLine("}");
            File.WriteAllText(Path.Combine(_target.OutDir, $"drag_anywhere_{_label}.json"), j.ToString());

            var cj = new StringBuilder();
            cj.AppendLine("{ \"captions\": [");
            for (int i = 0; i < _captions.Count; i++)
            {
                var c = _captions[i];
                cj.AppendLine($"  {{ \"start\": {F(c.start)}, \"end\": {F(c.end)}, \"text\": \"{Esc(c.text)}\" }}" +
                              (i < _captions.Count - 1 ? "," : ""));
            }
            cj.AppendLine("] }");
            File.WriteAllText(Path.Combine(_target.OutDir, $"captions_{_label}.json"), cj.ToString());
            Line($"verdict -> {_target.OutDir}/drag_anywhere_{_label}.json");
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        void Line(string s)
        {
            _log.AppendLine(s);
            Debug.Log("[DragAnywhere] " + s);
            Directory.CreateDirectory(_target.OutDir);
            File.WriteAllText(Path.Combine(_target.OutDir, $"drag_anywhere_{_label}.log"), _log.ToString());
        }
    }
}
