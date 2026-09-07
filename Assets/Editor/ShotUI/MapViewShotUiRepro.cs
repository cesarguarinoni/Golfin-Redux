#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using Golfin.Diagnostics.Runtime;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Reproduces "the shooting UI disappears after map view" (Cesar, 2026-09-07) by driving the
    /// REAL widgets and dumping the active-state of every ShotUI_Canvas child at each step.
    ///
    /// <para>Diagnostic, not a gate: it answers WHICH object is left inactive and WHO turned it
    /// off, rather than confirming a theory read off the source. Hide and restore look balanced
    /// in MapViewController, so the answer is not in that file alone.</para>
    ///
    /// <para>Menu: GOLFIN ▸ ShotUI ▸ Repro Map-View Shot UI.</para>
    /// </summary>
    public static class MapViewShotUiRepro
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey       = "MapViewShotUiRepro.Armed";

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/ShotUI/Verify Scheme Ball + Handle")]
        public static void LaunchVerify()
        {
            SessionState.SetBool("MapViewShotUiRepro.VerifyMode", true);
            Launch();
        }

        [MenuItem("GOLFIN/ShotUI/Repro Map-View Shot UI")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[MapRepro] already playing"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;
            var host = new GameObject("[MapReproBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            var r = host.AddComponent<MapViewShotUiReproRunner>();
            r.Verify = SessionState.GetBool("MapViewShotUiRepro.VerifyMode", false);
            SessionState.SetBool("MapViewShotUiRepro.VerifyMode", false);
        }
    }

    public class MapViewShotUiReproRunner : MonoBehaviour
    {
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        void Note(string s) { Debug.Log("[MapRepro] " + s); }

        static Button FindButton(string n) => UnityEngine.Object
            .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(b => b.gameObject.name == n);

        static void ClickReal(Button b)
        {
            var ped = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerUpHandler);
            b.onClick.Invoke();
        }

        IEnumerator ClickWhenPresent(string n, float timeout = 90f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                var b = FindButton(n);
                if (b != null) { ClickReal(b); yield break; }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT waiting for button " + n);
        }

        static IEnumerable<MonoBehaviour> HoleCards() => UnityEngine.Object
            .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(m => m.GetType().Name == "HoleCardController");

        IEnumerator ClickHoleCard(int hole, float timeout = 30f)
        {
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                foreach (var c in HoleCards())
                {
                    var p = c.GetType().GetProperty("HoleNumber");
                    if (p == null || (int)p.GetValue(c) != hole) continue;
                    if (c.GetType().GetField("actionButton", NP)?.GetValue(c) is Button btn)
                    { ClickReal(btn); yield break; }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT waiting for hole card " + hole);
        }

        /// <summary>
        /// Every loaded object with this name, ACTIVE ONES FIRST. Resources.FindObjectsOfTypeAll
        /// returns inactive objects and objects from every loaded scene, and taking the first hit
        /// made this probe report "ClubHandle activeInHierarchy=False" for a ClubHandle that was
        /// not the one on screen. Ambiguity has to be visible, not silently resolved.
        /// </summary>
        static List<GameObject> FindAll(string name)
        {
            var hits = new List<GameObject>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == name && t.gameObject.scene.IsValid()) hits.Add(t.gameObject);
            hits.Sort((a, b) => b.activeInHierarchy.CompareTo(a.activeInHierarchy));
            return hits;
        }

        static GameObject FindAny(string name)
        {
            var all = FindAll(name);
            return all.Count > 0 ? all[0] : null;
        }

        static string PathOf(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return t.gameObject.scene.name + ":" + s;
        }

        /// <summary>Walk an object's ANCESTOR CHAIN and report who is actually switched off, with
        /// the alpha of every CanvasGroup on the way. Answers "why can I not see it" directly,
        /// instead of leaving it to a diff that cannot see a fault present in both samples.</summary>
        void Chain(string name)
        {
            var hits = FindAll(name);
            if (hits.Count == 0) { Note("CHAIN " + name + ": not found"); return; }
            Note("CHAIN " + name + ": " + hits.Count + " object(s) with this name");
            foreach (var go in hits)
            {
                var sb = new StringBuilder("   " + PathOf(go.transform)
                                           + " activeInHierarchy=" + go.activeInHierarchy);
                for (var t = go.transform; t != null; t = t.parent)
                {
                    var cg = t.GetComponent<CanvasGroup>();
                    if (!t.gameObject.activeSelf) sb.Append("  [OFF: " + t.name + "]");
                    if (cg != null && cg.alpha < 0.999f) sb.Append("  [alpha " + t.name + "=" + cg.alpha.ToString("F2") + "]");
                }
                var img = go.GetComponent<Image>();
                if (img != null) sb.Append("  img.enabled=" + img.enabled + " a=" + img.color.a.ToString("F2"));
                Note(sb.ToString());
            }
        }

        GameObject _canvas;

        /// <summary>Active-state of every direct child of ShotUI_Canvas, as one line per step.</summary>
        string Snapshot()
        {
            var sb = new StringBuilder();
            foreach (Transform c in _canvas.transform)
                sb.Append(c.name + "=" + (c.gameObject.activeSelf ? "on" : "OFF") + " ");
            return sb.ToString();
        }

        /// <summary>
        /// FULL recursive state of the canvas: every descendant's activeSelf, every CanvasGroup
        /// alpha, every Image.enabled and every Behaviour.enabled, keyed by hierarchy path.
        ///
        /// <para>The direct-children snapshot said the scheme root comes back, so whatever the
        /// player is missing is deeper than that or is component-level. Diffing two of these says
        /// exactly which knob is left in the wrong position without guessing where to look.</para>
        /// </summary>
        Dictionary<string, string> DeepState()
        {
            var d = new Dictionary<string, string>();
            void Walk(Transform t, string path)
            {
                string p = path + "/" + t.name;
                var bits = new List<string> { t.gameObject.activeSelf ? "active" : "INACTIVE" };
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) bits.Add("alpha=" + cg.alpha.ToString("F2") + (cg.blocksRaycasts ? "" : " noRay"));
                var img = t.GetComponent<Image>();
                if (img != null) bits.Add("img=" + (img.enabled ? "on" : "OFF"));
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null) bits.Add(mb.GetType().Name + "=" + (mb.enabled ? "on" : "OFF"));
                d[p] = string.Join(" ", bits);
                foreach (Transform c in t) Walk(c, p);
            }
            Walk(_canvas.transform, "");
            return d;
        }

        void DiffState(Dictionary<string, string> before, Dictionary<string, string> after, string label)
        {
            int n = 0;
            foreach (var kv in before)
            {
                if (!after.TryGetValue(kv.Key, out var now)) { Note("GONE " + kv.Key); n++; continue; }
                if (now != kv.Value) { Note("DIFF " + kv.Key + "\n        was: " + kv.Value + "\n        now: " + now); n++; }
            }
            foreach (var kv in after)
                if (!before.ContainsKey(kv.Key)) { Note("NEW  " + kv.Key + " -> " + kv.Value); n++; }
            Note(label + " -> " + n + " difference(s)");
        }

        /// <summary>One open/close of the map, diffed against the state immediately before it.
        /// Each cycle is self-contained so a later one cannot be blamed on an earlier one.</summary>
        IEnumerator Cycle(string label, string closeButtonName)
        {
            Note("---- " + label + " ----");
            var before = DeepState();
            string beforeShallow = Snapshot();

            var mapBtn = FindButton("HoleMap");
            if (mapBtn == null) { Note(label + ": no HoleMap button — SKIPPED"); yield break; }
            ClickReal(mapBtn);
            yield return new WaitForSecondsRealtime(3f);
            Note(label + " open : " + Snapshot());

            var closeBtn = FindButton(closeButtonName)
                        ?? FindButton("MapShotViewButton") ?? FindButton("DriverButton");
            if (closeBtn == null) { Note(label + ": no close control — SKIPPED"); yield break; }
            Note(label + " closing via " + closeBtn.name);
            ClickReal(closeBtn);
            yield return new WaitForSecondsRealtime(3.5f);   // let Update-driven alphas settle
            Note(label + " close: " + Snapshot());
            if (Snapshot() != beforeShallow) Note(label + " !! SHALLOW STATE CHANGED");
            DiffState(before, DeepState(), label);
        }

        /// <summary>Optionally open+close the map, then take a REAL swing through BotSwing (the
        /// sanctioned path — it drives whichever scheme is live), wait for rest, and diff.</summary>
        IEnumerator ShotCycle(string label, bool skipMap = false)
        {
            Note("---- " + label + " ----");
            yield return WaitForIdle();
            var before = DeepState();

            if (!skipMap)
            {
                var mapBtn = FindButton("HoleMap");
                if (mapBtn == null) { Note(label + ": no HoleMap button — SKIPPED"); yield break; }
                ClickReal(mapBtn);
                yield return new WaitForSecondsRealtime(3f);
                var closeBtn = FindButton("MapShotViewButton") ?? FindButton("DriverButton");
                if (closeBtn != null) ClickReal(closeBtn);
                yield return new WaitForSecondsRealtime(3f);
                Note(label + " after map close: " + Snapshot());
            }

            yield return Golfin.Gameplay.UI.Controls.Bot.BotSwing.PlayPerfect(
                0.55f, 0f, false, Golfin.Gameplay.UI.Controls.Bot.BotExecutionContext.Resolve());
            Note(label + " swing issued; waiting for rest");
            yield return WaitForIdle(45f);
            yield return new WaitForSecondsRealtime(3f);

            Note(label + " after shot : " + Snapshot());
            DiffState(before, DeepState(), label);
        }

        /// <summary>Wait until the shot controller is back at Idle (ball at rest).</summary>
        IEnumerator WaitForIdle(float timeout = 45f)
        {
            var sc = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                     .FirstOrDefault(m => m.GetType().Name == "ShotController");
            var pState = sc != null ? sc.GetType().GetProperty("State") : null;
            if (pState == null) { yield return new WaitForSecondsRealtime(2f); yield break; }
            for (float t = 0f; t < timeout; t += 0.25f)
            {
                if (pState.GetValue(sc).ToString() == "Idle") yield break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Note("TIMEOUT waiting for Idle");
        }

        public const string ShotsDir = "Docs/Specs/Quick/mapview_shot_ui/screenshots";

        /// <summary>
        /// Capture at END OF FRAME and copy into the task folder. The recursive property diff
        /// reported "fully restored" three runs running while Cesar was looking at an empty
        /// screen — so the PNG is the evidence and the dump is the hypothesis.
        /// </summary>
        IEnumerator Snap(string label)
        {
            yield return new WaitForEndOfFrame();
            string p = CaptureCore.SnapPlayModeSafe(label);
            if (string.IsNullOrEmpty(p) || !File.Exists(p)) { Note("CAPTURE FAILED " + label); yield break; }
            Directory.CreateDirectory(ShotsDir);
            string dst = Path.Combine(ShotsDir, label + ".png");
            File.Copy(p, dst, true);
            Note("capture " + label + " -> " + dst);
        }

        void Start() => StartCoroutine(Verify ? VerifyFixes() : Run());

        /// <summary>Set by the verify menu item; the same boot, a different body.</summary>
        public bool Verify;

        /// <summary>
        /// Proves the two visual fixes with numbers, per scheme: the 2D ball's rendered alpha
        /// (it must be translucent so the rest ghost and the real ball show THROUGH it) and the
        /// club head's rendered size (it must match Flick's, which the scene scales to 2x).
        /// </summary>
        IEnumerator VerifyFixes()
        {
            yield return BootToHole();
            if (_canvas == null) { EditorApplication.ExitPlaymode(); yield break; }
            var canvasRt = _canvas.GetComponent<RectTransform>();

            foreach (var scheme in new[]{ Golfin.Gameplay.UI.Controls.ControlScheme.Flick,
                                          Golfin.Gameplay.UI.Controls.ControlScheme.Pendulum,
                                          Golfin.Gameplay.UI.Controls.ControlScheme.Needle,
                                          Golfin.Gameplay.UI.Controls.ControlScheme.FreeSwing })
            {
                Golfin.Gameplay.UI.Controls.ControlSchemeService.Set(scheme, "settings");
                yield return new WaitForSecondsRealtime(2.5f);

                string handleName = scheme == Golfin.Gameplay.UI.Controls.ControlScheme.Flick ? "ClubHandle"
                                  : scheme == Golfin.Gameplay.UI.Controls.ControlScheme.Pendulum ? "PendulumHandle"
                                  : scheme == Golfin.Gameplay.UI.Controls.ControlScheme.Needle ? "NeedleHandle"
                                  : "FreeSwingHandle";
                var h = FindAll(handleName).FirstOrDefault(g => g.activeInHierarchy);
                string hs = "handle NOT LIVE";
                if (h != null)
                {
                    var rt = h.GetComponent<RectTransform>();
                    float k = rt.lossyScale.x / canvasRt.lossyScale.x;
                    hs = string.Format("handle rendered {0:F0}x{1:F0} px (scale {2:F2})",
                                       rt.rect.width * k, rt.rect.height * k, rt.localScale.x);
                }
                var ball = FindAll("CentralBall").FirstOrDefault(g => g.activeInHierarchy);
                var bimg = ball != null ? ball.GetComponent<Image>() : null;
                string bs = bimg != null ? string.Format("ball alpha {0:F2}", bimg.color.a) : "ball NOT LIVE";

                Note("VERIFY " + scheme + ": " + hs + " | " + bs);
                yield return Snap("verify_" + scheme);
            }

            PlayerPrefs.DeleteKey(Golfin.Gameplay.UI.Controls.ControlSchemeService.PrefKey);
            PlayerPrefs.Save();
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }

        /// <summary>Shared boot: real path to a loaded hole, canvas cached.</summary>
        IEnumerator BootToHole()
        {
            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton", 25f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);
            foreach (int h in new[] { 2, 1, 10, 4 })
            {
                if (!HoleCards().Any(c => (int)(c.GetType().GetProperty("HoleNumber")?.GetValue(c) ?? -1) == h)) continue;
                yield return ClickHoleCard(h); break;
            }
            for (float t = 0f; FindButton("HoleMap") == null && t < 120f; t += 0.5f)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);
            _canvas = FindAny("ShotUI_Canvas");
        }


        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(5f);
            yield return ClickWhenPresent("StartButton", 25f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ClickWhenPresent("PlayButton");
            yield return new WaitForSecondsRealtime(2.5f);

            int hole = 0;
            foreach (int h in new[] { 2, 1, 10, 4 })
            {
                if (!HoleCards().Any(c => (int)(c.GetType().GetProperty("HoleNumber")?.GetValue(c) ?? -1) == h)) continue;
                hole = h; yield return ClickHoleCard(h); break;
            }
            if (hole == 0) { Note("ABORT no hole card"); EditorApplication.ExitPlaymode(); yield break; }

            for (float t = 0f; FindButton("HoleMap") == null && t < 120f; t += 0.5f)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(4f);

            _canvas = FindAny("ShotUI_Canvas");
            if (_canvas == null) { Note("ABORT no ShotUI_Canvas"); EditorApplication.ExitPlaymode(); yield break; }

            // PIN THE SCHEME. A previous run left the pref on Pendulum and this probe silently
            // tested a different scheme than the one it reported — the frames looked "broken"
            // only because a Pendulum idle legitimately has no cone.
            Golfin.Gameplay.UI.Controls.ControlSchemeService.Set(
                Golfin.Gameplay.UI.Controls.ControlScheme.Flick, "settings");
            yield return new WaitForSecondsRealtime(2f);
            Note("scheme PINNED to " + Golfin.Gameplay.UI.Controls.ControlSchemeService.Current);

            yield return Snap("1_before_map");
            foreach (var n in new[]{"ConeRoot","ConeMesh","ClubHandle","TimingSlab","SchemeRoot_Flick"}) Chain(n);
            var before = DeepState();

            var mapBtn = FindButton("HoleMap");
            if (mapBtn == null) { Note("ABORT no HoleMap"); EditorApplication.ExitPlaymode(); yield break; }
            ClickReal(mapBtn);
            yield return new WaitForSecondsRealtime(3f);
            yield return Snap("2_map_open");

            var closeBtn = FindButton("MapShotViewButton") ?? FindButton("DriverButton");
            Note("closing via " + (closeBtn != null ? closeBtn.name : "NONE"));
            if (closeBtn != null) ClickReal(closeBtn);
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Snap("3_after_close");
            foreach (var n in new[]{"ConeRoot","ConeMesh","ClubHandle","TimingSlab","SchemeRoot_Flick"}) Chain(n);
            DiffState(before, DeepState(), "after close");

            // Cesar: "does not re-appear when moving the club handle". Drive the real handle and
            // see whether the cone/handle come back — that separates "hidden" from "dead input".
            var handle = FindAny("ClubHandle");
            var dragger = handle != null
                ? handle.GetComponent<Golfin.Gameplay.UI.ShotUI.ClubHandleDragger>() : null;
            Note("ClubHandle found=" + (handle != null)
                 + " activeInHierarchy=" + (handle != null && handle.activeInHierarchy)
                 + " dragger=" + (dragger != null));
            if (dragger != null)
            {
                var ped = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.30f) };
                ExecuteEvents.Execute(dragger.gameObject, ped, ExecuteEvents.pointerDownHandler);
                yield return null;
                for (int i = 1; i <= 8; i++)
                {
                    ped.position = new Vector2(Screen.width * 0.5f, Screen.height * (0.30f - 0.02f * i));
                    ExecuteEvents.Execute(dragger.gameObject, ped, ExecuteEvents.dragHandler);
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Snap("4_dragging_handle");
                ExecuteEvents.Execute(dragger.gameObject, ped, ExecuteEvents.pointerUpHandler);
                yield return new WaitForSecondsRealtime(2f);
            }

            PlayerPrefs.DeleteKey(Golfin.Gameplay.UI.Controls.ControlSchemeService.PrefKey);
            PlayerPrefs.Save();
            Note("pref cleared");
            yield return new WaitForSecondsRealtime(1f);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
