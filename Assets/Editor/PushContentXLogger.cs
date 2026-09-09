#if UNITY_EDITOR
// Assets/Editor/PushContentXLogger.cs
// push_arrival_hitch §4d — the per-frame content-X log for ModeSelection -> MissionSelection.
//
// WHAT THE STRIP CANNOT SHOW. The strip proves the arriver is VISIBLE now; it cannot show the
// SHAPE of the travel, and the shape is what fixes 1 and 3 are about. This writes one row per
// frame of the push: the real dt that frame, the arriver's content X, the leaver's content X, and
// the fraction of the full travel each advanced since the previous row. A teleport shows up as a
// single row with a large dFrac; a stutter shows up as the two dFrac columns disagreeing.
//
// "Content" is resolved through LayeredPush.LayerMap, the same definition GamePolishProbe uses,
// so the numbers here and the invariants there are about the same rects.
//
// Dependency-free on purpose (LayeredPush + ScreenManager only) so the SAME file can be dropped
// into an older checkout and produce a comparable table.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    public static class PushContentXLogger
    {
        const string ArmedKey = "PushContentXLogger.Armed";
        const string OutDir   = "Docs/Diagnostics/_capture/push_strips";

        public static void SetLabel(string label) => SessionState.SetString("PushContentXLogger.Label", label);

        [MenuItem("GOLFIN/Game Polish/Strips — per-frame content X log", priority = 265)]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ContentX] already playing — stop first."); return; }
            Directory.CreateDirectory(OutDir);
            Application.runInBackground = true;
            SessionState.SetString(ArmedKey, "1");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            if (SessionState.GetString(ArmedKey, "") != "1") return;
            SessionState.SetString(ArmedKey, "");
            var host = new GameObject("[ContentX]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().Label = SessionState.GetString("PushContentXLogger.Label", "run");
        }

        class Runner : MonoBehaviour
        {
            public string Label = "run";

            static Type LP  => Type.GetType("Golfin.UI.Polish.LayeredPush, Assembly-CSharp");
            static Type SmT => Type.GetType("GolfinRedux.UI.ScreenManager, Assembly-CSharp");
            static Type IdT => Type.GetType("GolfinRedux.UI.ScreenId, Assembly-CSharp");

            static bool IsPushing => (bool)LP.GetProperty("IsPushing").GetValue(null);

            static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

            static Button Find(string n) => Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == n && !string.IsNullOrEmpty(b.gameObject.scene.name) && b.isActiveAndEnabled);

            static GameObject Obj(object id)
            {
                object sm = SmT.GetProperty("Instance")?.GetValue(null);
                var m = SmT.GetMethod("ScreenObject", BindingFlags.Public | BindingFlags.Instance)
                     ?? SmT.GetMethod("GetScreen", BindingFlags.Public | BindingFlags.Instance);
                if (sm != null && m != null) return m.Invoke(sm, new[] { id }) as GameObject;
                // Fallback: LayerMap names the root by convention <Id>Screen.
                string want = id + "Screen";
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    if (t.name == want && !string.IsNullOrEmpty(t.gameObject.scene.name) && t.parent != null
                        && t.parent.name == "ScreensRoot") return t.gameObject;
                return null;
            }

            /// <summary>The screen's FIRST content rect, through LayeredPush.LayerMap — the same
            /// definition GamePolishProbe.FirstContent uses.</summary>
            static RectTransform FirstContent(object id, GameObject go)
            {
                if (go == null) return null;
                var mapM = LP.GetMethod("LayerMap", BindingFlags.Public | BindingFlags.Static);
                object layers = mapM?.Invoke(null, new[] { id });
                if (layers == null) return null;
                object val = layers.GetType().GetProperty("Value")?.GetValue(layers) ?? layers;
                var contentF = val.GetType().GetField("Content");
                if (contentF?.GetValue(val) is string[] names)
                    foreach (string n in names)
                        if (go.transform.Find(n) is RectTransform rt) return rt;
                return null;
            }

            // ── LateUpdate sampling state ────────────────────────────────────
            RectTransform _arriver, _leaver;
            List<string> _rows = new List<string>();
            float _t0, _prevA = float.NaN, _prevL = float.NaN;
            int _f;
            bool _sampling;

            void LateUpdate()
            {
                if (!_sampling) return;
                float ax = _arriver != null ? _arriver.anchoredPosition.x : float.NaN;
                float lx = _leaver  != null ? _leaver.anchoredPosition.x  : float.NaN;
                float w  = _arriver != null && _arriver.rect.width > 1 ? _arriver.rect.width : Screen.width;
                float dA = float.IsNaN(_prevA) ? 0f : (ax - _prevA) / w;
                float dL = float.IsNaN(_prevL) ? 0f : (lx - _prevL) / w;
                _rows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1:F1}\t{2:F1}\t{3:F1}\t{4:F1}\t{5:P1}\t{6:P1}",
                    _f, Time.unscaledDeltaTime * 1000f, (Time.realtimeSinceStartup - _t0) * 1000f, ax, lx, dA, dL));
                _prevA = ax; _prevL = lx; _f++;
            }

            static string PathOf(Transform t)
            {
                if (t == null) return "<null>";
                string s = t.name;
                while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
                return s;
            }

            void Start() => StartCoroutine(Go());

            IEnumerator Go()
            {
                float waited = 0f; bool tapped = false;
                while (waited < 60f)
                {
                    if (Find("NavTeeButton") != null) break;
                    if (!tapped)
                    {
                        var s = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                            b.gameObject.name == "StartButton" && !string.IsNullOrEmpty(b.gameObject.scene.name)
                            && b.gameObject.activeInHierarchy);
                        if (s != null) { s.onClick.Invoke(); tapped = true; }
                    }
                    waited += Time.unscaledDeltaTime; yield return null;
                }
                yield return Wait(3f);

                var tee = Find("NavTeeButton");
                if (tee != null) { tee.onClick.Invoke(); yield return Wait(2.5f); }

                Button play = null;
                foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
                {
                    if (b.gameObject.name != "ActionButton" || string.IsNullOrEmpty(b.gameObject.scene.name)) continue;
                    if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                    if (b.transform.parent != null && b.transform.parent.name == "ExpandedContainer") { play = b; break; }
                }
                if (play == null)
                {
                    foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    {
                        if (t.name != "CardTapButton" || string.IsNullOrEmpty(t.gameObject.scene.name)) continue;
                        if (!t.gameObject.activeInHierarchy) continue;
                        var tap = t.GetComponent<Button>();
                        if (tap != null && tap.interactable) { tap.onClick.Invoke(); break; }
                    }
                    yield return Wait(1.0f);
                    foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
                    {
                        if (b.gameObject.name != "ActionButton" || string.IsNullOrEmpty(b.gameObject.scene.name)) continue;
                        if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                        if (b.transform.parent != null && b.transform.parent.name == "ExpandedContainer") { play = b; break; }
                    }
                }
                if (play == null) { Debug.LogError("[ContentX] no mode-card ActionButton"); EditorApplication.ExitPlaymode(); yield break; }

                object modeId = Enum.Parse(IdT, "ModeSelection");
                object missId = Enum.Parse(IdT, "MissionSelection");
                GameObject modeGo = Obj(modeId), missGo = Obj(missId);
                RectTransform leaver = FirstContent(modeId, modeGo);

                play.onClick.Invoke();

                // The arriver's content only exists once Push has SetActive'd it, so it is resolved
                // inside the loop rather than before the tap.
                RectTransform arriver = null;
                float guard = Time.realtimeSinceStartup + 3f;
                while (!IsPushing && Time.realtimeSinceStartup < guard) yield return null;
                missGo = Obj(missId);

                // Resolve the arriver BEFORE the loop and SAY WHAT WAS RESOLVED. The first run of
                // this tool logged arriverX = 0.0 on every frame while the probe's own record said
                // the arriver starts at +1170 — i.e. it was measuring a rect the push never
                // touches. A number that disagrees with a known-good instrument is a resolution
                // bug until the path proves otherwise, so the path is now in the header.
                missGo = Obj(missId);
                arriver = FirstContent(missId, missGo);
                string arriverPath = PathOf(arriver), leaverPath = PathOf(leaver);
                Debug.Log($"[ContentX] leaver='{leaverPath}' x={(leaver != null ? leaver.anchoredPosition.x : float.NaN)}  " +
                          $"arriver='{arriverPath}' x={(arriver != null ? arriver.anchoredPosition.x : float.NaN)}");
                if (missGo != null)
                    foreach (Transform c in missGo.transform)
                        if (c is RectTransform crt)
                            Debug.Log($"[ContentX]   candidate '{c.name}' id={crt.GetInstanceID()} x={crt.anchoredPosition.x:F1} active={c.gameObject.activeSelf}");

                // EVERY RectTransform in the scene called 'Content' or 'CardsContainer', by
                // instance id. If the one the tween writes is not the one resolved above, it is a
                // duplicate object and the numbers were never going to agree.
                foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
                {
                    if (string.IsNullOrEmpty(rt.gameObject.scene.name)) continue;
                    if (rt.name != "Content" && rt.name != "CardsContainer") continue;
                    if (!PathOf(rt).Contains("MissionSelectionScreen") && !PathOf(rt).Contains("ModeSelectionScreen")) continue;
                    Debug.Log($"[ContentX]   scene rect id={rt.GetInstanceID()} x={rt.anchoredPosition.x:F1} {PathOf(rt)}");
                }
                var offP = LP.GetProperty("LastPushEnterOffset");
                Debug.Log($"[ContentX] LastPushEnterOffset={(offP != null ? offP.GetValue(null) : (object)"<absent>")} " +
                          $"arriverId={(arriver != null ? arriver.GetInstanceID() : 0)} leaverId={(leaver != null ? leaver.GetInstanceID() : 0)}");

                // SAMPLED FROM LateUpdate, not from this coroutine. Coroutines resume in START
                // order, and this one was started before the push's, so reading here would have
                // reported every frame's value one frame stale — which is exactly the doubt that
                // makes an anomalous number unusable. LateUpdate runs after every Update and every
                // coroutine, so what it reads is what the frame actually renders.
                _arriver = arriver; _leaver = leaver;
                _t0 = Time.realtimeSinceStartup; _rows = new List<string>(); _f = 0;
                _prevA = float.NaN; _prevL = float.NaN; _sampling = true;
                while (IsPushing) yield return null;
                _sampling = false;
                var rows = _rows;

                var sb = new StringBuilder();
                sb.AppendLine($"# per-frame content X — ModeSelection -> MissionSelection   [{Label}]");
                sb.AppendLine($"# driven by the REAL mode-card ExpandedContainer/ActionButton.onClick");
                sb.AppendLine($"# contentWidth={(arriver != null ? arriver.rect.width : Screen.width):F0}px  frames={rows.Count}");
                sb.AppendLine($"# arriver rect = {arriverPath}");
                sb.AppendLine($"# leaver  rect = {leaverPath}");
                sb.AppendLine("# dArriver/dLeaver = fraction of the content width travelled SINCE THE PREVIOUS FRAME.");
                sb.AppendLine("# A single large dArriver is fix 1's teleport; dArriver != dLeaver is the parallax.");
                sb.AppendLine("frame\tdt_ms\telapsed_ms\tarriverX\tleaverX\tdArriver\tdLeaver");
                foreach (var r in rows) sb.AppendLine(r);
                string path = Path.Combine(OutDir, $"contentx_{Label}.tsv");
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"[ContentX] wrote {path}\n{sb}");

                yield return Wait(0.5f);
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
#endif
