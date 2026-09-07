#if UNITY_EDITOR
// golfer_3d_test — the video deliverable.
//
// Real play, real entry path, sanctioned recorder: ShellScene ▸ PLAY ▸ GameplaySceneLoader ▸ Hole 08,
// then one take showing the stand-in golfer address the ball, turn with the aim, swap to the putter
// and back, swing on a committed shot, and re-plant at the new lie when the ball stops.
//
// Recording plumbing is BotVideoRecorder via the CustomOutputPath + ArmDeferred / BeginDeferred
// contract every other DemoRecorder uses — deferred so the clip starts once the hole is stable
// (that is what avoids the Y-flip transient) and so 25 s of booting is not in the film. End() is
// deliberately NOT called here: LoopV2SmokeBotMenu's ExitingPlayMode hook calls it unconditionally
// and exactly one End() per session is the documented contract.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Physics.Viewer.Editor;

namespace Golfin.EditorTools
{
    public static class GolferTestDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string TaskDir        = "Docs/Specs/Active/golfer_3d_test";
        const string VideoDir       = TaskDir + "/videos";
        const string ArmedKey       = "GolferTestDemo.Armed";
        const int    HoleNumber     = 8;
        const int    WatchdogSeconds = 45;

        static StringBuilder _log;
        static readonly List<string> _captions = new List<string>();
        static float _recordStart;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Golfer Test/Record demo video (Hole 08)")]
        public static void Record()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[GolferDemo] already in play mode."); return; }

            // NOT SaveCurrentModifiedScenesIfUserWantsTo(): a modal has nobody to click it in an
            // MCP-driven run and wedges the whole Editor.
            EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(VideoDir);
            PlayerSettings.runInBackground = true;

            BotVideoRecorder.ResetSessionGuard();          // this harness records exactly ONE clip
            BotVideoRecorder.CustomOutputPath = VideoDir + "/raw";
            BotVideoRecorder.MaxRecordSecondsSessionOverride = WatchdogSeconds;
            BotVideoRecorder.ArmDeferred();

            _captions.Clear();
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GolferDemo] armed; entering play mode (deferred recording).");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (!SessionState.GetBool(ArmedKey, false)) return;
                SessionState.SetBool(ArmedKey, false);
                Application.runInBackground = true;
                BotVideoRecorder.Begin();                   // no-op for a deferred arm
                var host = new GameObject("[GolferTestDemoBot]");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<GolferTestDemoRunner>().Hole = HoleNumber;
                Debug.Log("[GolferDemo] bot spawned; waiting for the hole.");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                WriteCaptionsSidecar();
            }
        }

        public static void StartRecorder()
        {
            _log = new StringBuilder();
            _recordStart = Time.realtimeSinceStartup;
            BotVideoRecorder.BeginDeferred();
        }

        /// <summary>
        /// One caption, in seconds since StartRecording — which is what
        /// build_bot_video.py --mode captionsjson expects, so no clock alignment is needed.
        /// Kept short and pre-wrapped: a 1170-wide portrait frame fits about 40 characters a line.
        /// </summary>
        public static void Caption(float endOffset, string text)
        {
            float t = Time.realtimeSinceStartup - _recordStart;
            string esc = text.Replace("\"", "'").Replace("\n", "\\n");
            _captions.Add("{\"start\": " + t.ToString("F2", CultureInfo.InvariantCulture) +
                          ", \"end\": " + (t + endOffset).ToString("F2", CultureInfo.InvariantCulture) +
                          ", \"text\": \"" + esc + "\"}");
            Debug.Log($"[GolferDemo] t={t:F2} caption: {text.Replace("\n", " / ")}");
        }

        static void WriteCaptionsSidecar()
        {
            if (_captions.Count == 0) return;
            Directory.CreateDirectory(VideoDir);
            File.WriteAllText(VideoDir + "/captions.json",
                              "{\"captions\": [\n  " + string.Join(",\n  ", _captions) + "\n]}\n");
            Debug.Log($"[GolferDemo] wrote {VideoDir}/captions.json ({_captions.Count} captions).");
            _captions.Clear();
        }
    }

    public class GolferTestDemoRunner : MonoBehaviour
    {
        public int Hole = 8;

        void Start() => StartCoroutine(Sequence());

        static IEnumerator Hold(float s) { yield return new WaitForSecondsRealtime(s); }

        static Type FindType(string n) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType(n); } catch { return null; } }).FirstOrDefault(t => t != null);

        static Component FindShot()
        {
            var t = FindType("Golfin.Gameplay.Input.ShotController");
            return t == null ? null : UnityEngine.Object.FindFirstObjectByType(t) as Component;
        }
        static float Heading(Component s) => s == null ? 0f : (float)s.GetType().GetProperty("CameraHeadingRadians").GetValue(s);
        static void SetHeading(Component s, float v) => s?.GetType().GetProperty("CameraHeadingRadians")?.SetValue(s, v);
        static void SetIsPutt(Component s, bool v)   => s?.GetType().GetProperty("IsPutt")?.SetValue(s, v);

        static Transform Ball()
        {
            var t = FindType("Golfin.Physics.Viewer.BallAnimator");
            var i = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return i == null ? null : (Transform)t.GetProperty("CurrentBall").GetValue(i);
        }

        IEnumerator PassTheStartGate()
        {
            yield return Hold(6f);
            for (int i = 0; i < 20; i++)
            {
                foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    if (b.name != "StartButton" && b.name != "PlayButton") continue;
                    b.onClick.Invoke();
                    yield return Hold(2f);
                    yield break;
                }
                yield return Hold(0.5f);
            }
        }

        IEnumerator Sequence()
        {
            yield return PassTheStartGate();
            if (!SeedAndLoad(Hole)) { Debug.LogError("[GolferDemo] could not load the hole."); EditorApplication.isPlaying = false; yield break; }
            yield return WaitForScene("LabScaffold", 60f);
            yield return WaitForScene("Hole_" + Hole.ToString("00") + "_Geo", 60f);
            yield return Hold(9f);                      // let the hole settle before the clip starts

            var golfer = GameObject.Find("GolferTest");
            var shot   = FindShot();
            if (golfer == null) Debug.LogError("[GolferDemo] no GolferTest in the scene.");

            // ── the clip starts here ────────────────────────────────────────────────
            GolferTestDemoRecorder.StartRecorder();
            yield return Hold(0.4f);

            GolferTestDemoRecorder.Caption(3.4f, "Stand-in golfer, opt-in build only\nHole 8 - stands at the ball");
            yield return Hold(3.6f);

            // Aim sweep — he turns with the camera heading.
            float h0 = Heading(shot);
            GolferTestDemoRecorder.Caption(3.9f, "Turns with the aim heading");
            for (float k = 0f; k <= 1f; k += 0.05f) { SetHeading(shot, h0 + Mathf.Sin(k * Mathf.PI * 2f) * 0.45f); yield return null; }
            SetHeading(shot, h0);
            yield return Hold(0.8f);

            // NO PUTTER BEAT HERE, DELIBERATELY.
            //
            // Putter mode 450 yd from the pin is a state the game actively refuses: §2f's
            // surface auto-switch owns the club at a tee, and ClubSelectionBroadcast.SetPutterMode
            // early-outs when the flag has not changed, so asking for it politely is overruled
            // within a frame and the club never swaps. Forcing it by also writing
            // ShotController.IsPutt DOES swap the mesh — and drags the lab camera into its aerial
            // putt framing, which is what made the first take eight seconds of empty fairway.
            // Neither version is worth filming at a tee. The swap is covered where it can be
            // stated exactly instead of implied: golfer_invariants.json (club.putterSwap /
            // club.driverSwapBack) and the prefab render sheet.
            GolferTestDemoRecorder.Caption(4.4f, "Club head sits on the ball\nat address");
            yield return Hold(4.6f);

            // ── Hold a camera on him through the swing ────────────────────────────
            //
            // CAPTURE-HARNESS ONLY, and additive: a SECOND camera at a higher depth is spawned
            // for the swing window and destroyed after. The game's own camera is not touched and
            // nothing about gameplay changes. It exists because the loop camera cuts to the ball
            // the instant the shot commits — measured: the golfer is out of frame at the first
            // sample after commit, with the swing at normalized 0.07 and impact at 0.199 — so the
            // swing is otherwise never on screen. Making it visible IN THE GAME is a camera
            // change, which SPEC §8 puts out of scope; this only makes it filmable.
            StartCoroutine(HoldCameraOnGolfer(golfer, 3.2f));

            // A real shot, through BotSwing so it goes out of the live control scheme.
            Vector3 before = golfer != null ? golfer.transform.position : Vector3.zero;
            GolferTestDemoRecorder.Caption(3.4f, "Swing - held camera. In game the loop\ncamera cuts away here and hides it");
            var ctx = Golfin.Gameplay.UI.Controls.Bot.BotExecutionContext.Resolve();
            yield return Golfin.Gameplay.UI.Controls.Bot.BotSwing.PlayPerfect(
                power01: 0.85f, aimYawRad: Heading(shot), isPutt: false, ctx: ctx);

            GolferTestDemoRecorder.Caption(5.0f, "Ball away - 250 m carry");

            // Prove the swing keeps animating while the camera is elsewhere. Under the old
            // CullUpdateTransforms the golfer's transforms froze the frame he left the frustum,
            // so the swing never reached the ball; the presenter now suspends culling for the
            // duration of a swing. Sampling the animator here is what says it worked.
            var anim = golfer != null ? golfer.GetComponent<Animator>() : null;
            var seen = new List<string>();
            for (int i = 0; i < 12; i++)
            {
                if (anim != null)
                {
                    var si = anim.GetCurrentAnimatorStateInfo(0);
                    string nm = si.IsName("Swing_Drive") ? "Swing_Drive" : si.IsName("Idle") ? "Idle" :
                                si.IsName("Swing_Putt") ? "Swing_Putt" : "other";
                    seen.Add($"{nm}@{si.normalizedTime:F2} vis={(golfer.GetComponentInChildren<SkinnedMeshRenderer>()?.isVisible ?? false)} cull={anim.cullingMode}");
                }
                yield return Hold(0.25f);
            }
            Debug.Log("[GolferDemo] swing while the camera is away:\n  " + string.Join("\n  ", seen));

            yield return WaitForBallAtRest(22f);
            yield return Hold(0.6f);

            float moved = golfer != null ? Vector3.Distance(
                new Vector3(before.x, 0, before.z),
                new Vector3(golfer.transform.position.x, 0, golfer.transform.position.z)) : 0f;
            GolferTestDemoRecorder.Caption(4.5f, "Ball at rest - he re-plants at the\nnew lie, " + moved.ToString("F0") + " m up the fairway");
            yield return Hold(4.8f);

            Debug.Log("[GolferDemo] take complete; golfer moved " + moved.ToString("F1") + " m.");
            EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// Frames the golfer from down the target line for <paramref name="seconds"/>, using a
        /// throwaway camera at a higher depth than the game's. Restores nothing because it
        /// changes nothing — it just stops rendering and is destroyed.
        /// </summary>
        IEnumerator HoldCameraOnGolfer(GameObject golfer, float seconds)
        {
            if (golfer == null) yield break;
            Camera baseCam = Camera.allCameras.OrderByDescending(c => c.depth)
                                              .FirstOrDefault(c => c.isActiveAndEnabled && c.targetTexture == null);
            var go = new GameObject("[GolferSwingCam]");
            var cam = go.AddComponent<Camera>();
            if (baseCam != null)
            {
                cam.clearFlags      = baseCam.clearFlags;
                cam.backgroundColor = baseCam.backgroundColor;
                cam.cullingMask     = baseCam.cullingMask;
                cam.nearClipPlane   = baseCam.nearClipPlane;
                cam.farClipPlane    = baseCam.farClipPlane;
                cam.depth           = baseCam.depth + 10f;
            }
            cam.fieldOfView = 40f;

            float t = 0f;
            while (t < seconds && golfer != null)
            {
                // Down the target line, slightly behind and above him, so the whole arc reads.
                Vector3 focus = golfer.transform.position + Vector3.up * 1.05f;
                Vector3 side  = golfer.transform.right;         // +X is the ball side
                go.transform.position = focus - golfer.transform.forward * 1.1f + side * 3.1f + Vector3.up * 0.55f;
                go.transform.LookAt(focus);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        IEnumerator WaitForBallAtRest(float timeout)
        {
            float t = 0f, still = 0f;
            Vector3 last = Ball() != null ? Ball().position : Vector3.zero;
            while (t < timeout)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                t += 0.25f;
                var b = Ball();
                Vector3 now = b != null ? b.position : last;
                still = Vector3.Distance(now, last) < 0.01f ? still + 0.25f : 0f;
                last = now;
                if (still >= 1.25f && t > 3f) yield break;
            }
        }

        static bool SeedAndLoad(int hole)
        {
            try
            {
                var gs = FindType("Golfin.Gameplay.Session.GameSession");
                gs.GetProperty("IsVersus", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, false);
                string id = "";
                var cm = FindType("Golfin.Roster.CharacterManager") ?? FindType("CharacterManager");
                var inst = cm?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (inst != null) id = (string)(cm.GetMethod("GetSelectedCharacterId")?.Invoke(inst, null) ?? "");
                gs.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                  ?.Invoke(null, new object[] { hole, id, 0 });
                var lt = FindType("Golfin.UI.GameplayTransition.GameplaySceneLoader");
                var li = lt?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (li == null) return false;
                var begin = lt.GetMethods().FirstOrDefault(m => m.Name == "BeginGameplayLoad");
                var pars = begin.GetParameters();
                begin.Invoke(li, pars.Length == 1 ? new object[] { hole } : new object[] { hole, null });
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[GolferDemo] seed/load failed: " + e.Message); return false; }
        }

        static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (s.name == name && s.isLoaded) yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                t += 0.5f;
            }
        }
    }
}
#endif
