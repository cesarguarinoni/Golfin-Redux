// ─────────────────────────────────────────────────────────────────────────────
// game_polish_c §A2 / §A3 — the two things in this task a still cannot show.
//
// The sweep's evidence is numbers: a 511-row button table, an 18-row scroll
// table, a per-surface safe-area JSON. Two of its claims are about MOTION and
// nothing in a table settles them — a press pulse is seven frames at 60 fps, and
// "Elastic" is a word until you watch a list run past its last row and come back.
//
//   a_press_feedback   five buttons, one per pillar, each of which had NO
//                      ButtonPressFeedback at HEAD, pressed through the
//                      component's own OnPointerDown.
//   b_overscroll_rankings / c_overscroll_inventory
//                      a real drag past the end of a list, released, so the
//                      Elastic spring is the thing on screen.
//
// THE DRAGS GO THROUGH ScrollRect's OWN DRAG HANDLERS — OnBeginDrag / OnDrag /
// OnEndDrag with a real PointerEventData — not by writing normalizedPosition.
// Writing the position would move the content without ever engaging the movement
// type, so the clip would look identical whether §C2 had run or not: it would be
// a recording of the harness, not of the change. (Same argument as
// bot_scheme_parity §3.5: drive the production entry point or you are testing
// your own scaffolding.)
//
// ONE TAKE, CUT AFTERWARDS. Three play sessions is three chances to come up on a
// different screen, and three RecorderControllers in one session is the
// arrangement that has produced flipped and truncated files before. The whole
// route records once and writes videos/segments.json; Docs/Scripts/
// cut_game_polish_clips.py slices and captions on those boundaries.
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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishDemoRecorderC
    {
        const string OutputDir = "Docs/Specs/Active/game_polish_c/videos";
        const string ArmedKey  = "GamePolishDemoRecorderC.Armed";
        static RecorderController? _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Game Polish/Record C — press + over-scroll clips (A2/A3)", priority = 295)]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[DemoC] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[DemoC] Armed. Entering play mode...");
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

        static void StartRecorderAndBot()
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
                    Debug.LogWarning($"[DemoC] Could not pin iPhone-14 — recording at {w}x{h}. " +
                                     "A2/A3 want full size (memory: record_bot_video_full_size).");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "GamePolishDemoC";
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
            Debug.Log($"[DemoC] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[GamePolishDemoBotC]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GamePolishDemoRunnerC>().Begin(Time.realtimeSinceStartup);
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[DemoC] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[DemoC] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    /// <summary>Drives the three §A2/§A3 segments and writes the cut sidecar.</summary>
    public class GamePolishDemoRunnerC : MonoBehaviour
    {
        const string OutputDir = "Docs/Specs/Active/game_polish_c/videos";

        float _t0;
        bool  _reached;
        string _note = "";
        readonly List<(string id, string caption, float start, float end, bool reached, string note)> _segments = new();
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
            Line("=== game_polish_c A2/A3 demo " + DateTime.UtcNow.ToString("u") + " ===");
            yield return Boot();

            yield return Segment("a_press_feedback",
                "Press feedback: five buttons that had none - each dips to 0.95 and springs back",
                PressFive);

            yield return Segment("b_overscroll_rankings",
                "Rankings list dragged past its last row: Elastic, so it springs back",
                () => OverScroll(ScreenId.Leaderboard,
                    "Canvas/ScreensRoot/RankingsScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea"));

            yield return Segment("c_overscroll_inventory_items",
                "Inventory ITEMS carousel dragged past its last card: the same spring",
                () => OverScroll(ScreenId.Inventory,
                    "Canvas/ScreensRoot/InventoryScreen/ContentArea/ItemsContent/ItemMainSection/ItemCarousel/ScrollView",
                    horizontal: true, inventoryTab: 3));

            WriteSidecar();
            Line("=== done ===");
            EditorApplication.isPlaying = false;
        }

        IEnumerator Segment(string id, string caption, Func<IEnumerator> body)
        {
            _reached = true; _note = "";
            float start = Now;
            Line($"--- {id} @ {start:0.00}s ---");
            yield return body();
            float end = Now;
            _segments.Add((id, caption, start, end, _reached, _note));
            Line($"--- {id} {start:0.00}..{end:0.00}s reached={_reached} {_note} ---");
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // ── A2 ───────────────────────────────────────────────────────────────

        /// <summary>The same five sites the press probe measures, in the same order.</summary>
        static readonly (ScreenId Screen, string Path)[] Sites =
        {
            (ScreenId.Roster,        "Canvas/ScreensRoot/RosterScreen/DetailPanel/RightPanel/ButtonsPanel/LevelUpButton"),
            (ScreenId.Inventory,     "Canvas/ScreensRoot/InventoryScreen/TabBar/BAGSTab"),
            (ScreenId.GeneralShop,   "Canvas/ScreensRoot/GeneralShopScreen/ContentArea/BarsArea/TabBar/DailyTab"),
            (ScreenId.HoleSelection, "Canvas/ScreensRoot/HoleSelectionScreen/Content/Filters/FilterRow1/Pill_LOMOND_28_72"),
            (ScreenId.Home,          "SettingsScreen/SettingsPanel/SettingsList/AboutRow"),
        };

        IEnumerator PressFive()
        {
            int pressed = 0;
            foreach ((ScreenId screen, string path) in Sites)
            {
                yield return Show(screen);
                bool settings = path.StartsWith("SettingsScreen", StringComparison.Ordinal);
                if (settings) { SettingsController.Instance?.OpenSettings(); yield return new WaitForSecondsRealtime(1f); }
                yield return new WaitForSecondsRealtime(0.9f);

                Transform? t = ByPath(path);
                var fb = t != null ? t.GetComponent<ButtonPressFeedback>() : null;
                if (fb == null) { Line("  MISSING " + path); _note = "a site was not reachable"; }
                else
                {
                    // Twice, so a viewer who blinks still sees it.
                    for (int i = 0; i < 2; i++)
                    {
                        fb.OnPointerDown(new PointerEventData(EventSystem.current));
                        yield return new WaitForSecondsRealtime(0.45f);
                    }
                    pressed++;
                    Line($"  pressed {t!.name} on {screen}");
                }
                if (settings) { SettingsController.Instance?.CloseSettings(); yield return new WaitForSecondsRealtime(0.6f); }
            }
            if (pressed < Sites.Length) { _reached = false; _note = $"only {pressed}/{Sites.Length} sites pressed"; }
        }

        // ── A3 ───────────────────────────────────────────────────────────────

        /// <summary>
        /// A real drag: OnBeginDrag, a run of OnDrag frames that push the content well past its
        /// end, then OnEndDrag. Everything after the release is the ScrollRect's own Elastic
        /// spring — which is the whole point of the clip.
        /// </summary>
        IEnumerator OverScroll(ScreenId screen, string path, bool horizontal = false, int inventoryTab = -1)
        {
            yield return Show(screen);
            if (inventoryTab >= 0)
            {
                GameObject? inv = GameObject.Find("Canvas/ScreensRoot/InventoryScreen");
                var ctrl = inv != null ? inv.GetComponentInChildren<Golfin.Inventory.InventoryScreenController>(true) : null;
                foreach (Button b in inv != null ? inv.GetComponentsInChildren<Button>(true) : new Button[0])
                    if (b.transform.parent != null && b.transform.parent.name == "TabBar" &&
                        b.transform.GetSiblingIndex() == inventoryTab && b.gameObject.activeInHierarchy)
                    { b.onClick.Invoke(); break; }
                if (ctrl == null) Line("  (no InventoryScreenController)");
                yield return new WaitForSecondsRealtime(1.2f);
            }
            yield return new WaitForSecondsRealtime(1f);

            Transform? t = ByPath(path);
            var sr = t != null ? t.GetComponent<ScrollRect>() : null;
            if (sr == null) { _reached = false; _note = "scroll rect not found: " + path; Line("  MISSING " + path); yield break; }

            Line($"  {sr.name}: movementType={sr.movementType} elasticity={sr.elasticity} " +
                 $"inertia={sr.inertia} deceleration={sr.decelerationRate}");

            // Park at the far end first, so the drag that follows really is an over-scroll and not
            // just a scroll. Done before the recorded gesture, not as part of it.
            if (horizontal) sr.horizontalNormalizedPosition = 1f;
            else            sr.verticalNormalizedPosition   = 0f;
            yield return new WaitForSecondsRealtime(0.6f);

            var cam = sr.GetComponentInParent<Canvas>()?.worldCamera;
            var rt = (RectTransform)sr.transform;
            Vector3 centre = rt.TransformPoint(rt.rect.center);
            Vector2 screenCentre = RectTransformUtility.WorldToScreenPoint(cam, centre);

            var ped = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = screenCentre,
                pressPosition = screenCentre,
                pointerCurrentRaycast = new RaycastResult { gameObject = sr.gameObject },
                pointerPressRaycast  = new RaycastResult { gameObject = sr.gameObject },
            };
            sr.OnInitializePotentialDrag(ped);
            sr.OnBeginDrag(ped);

            // ~24 frames of pull, then release. The pull is deliberately far past the end so the
            // rubber-band is unmistakable at 30 fps.
            for (int i = 0; i < 24; i++)
            {
                Vector2 step = horizontal ? new Vector2(-26f, 0f) : new Vector2(0f, 26f);
                ped.delta = step;
                ped.position += step;
                sr.OnDrag(ped);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.35f);   // hold at full stretch
            sr.OnEndDrag(ped);
            yield return new WaitForSecondsRealtime(1.6f);    // the spring back
            Line("  released; spring settled");
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
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Show(ScreenId.Home);
        }

        IEnumerator Show(ScreenId id)
        {
            if (ScreenManager.Instance == null) yield break;
            if (ScreenManager.Instance.CurrentScreen != id) ScreenManager.Instance.ShowScreen(id);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (ScreenManager.Instance.CurrentScreen != id && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1.1f);
        }

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
                j.AppendLine("    {");
                j.AppendLine("      \"id\": \"" + s.id + "\",");
                j.AppendLine("      \"caption\": \"" + Esc(s.caption) + "\",");
                j.AppendLine("      \"start\": " + F(s.start) + ",");
                j.AppendLine("      \"end\": " + F(s.end) + ",");
                j.AppendLine("      \"reached\": " + (s.reached ? "true" : "false") + ",");
                j.AppendLine("      \"note\": \"" + Esc(s.note) + "\"");
                j.AppendLine("    }" + (i < _segments.Count - 1 ? "," : ""));
            }
            j.AppendLine("  ]");
            j.AppendLine("}");
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(OutputDir + "/segments.json", j.ToString());
            Line("sidecar -> " + OutputDir + "/segments.json");
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        void Line(string s)
        {
            _log.AppendLine(s);
            Debug.Log("[DemoC] " + s);
            Directory.CreateDirectory("Docs/Diagnostics/_capture");
            File.WriteAllText("Docs/Diagnostics/_capture/game_polish_c_demo.log", _log.ToString());
        }
    }
}
