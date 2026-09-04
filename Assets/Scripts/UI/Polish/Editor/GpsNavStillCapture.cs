// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §A15 — the GPS bar's selected slot, photographed.
//
// WHY A SEPARATE TOOL. §D7's highlight is ONE component (`NavSlotHighlight`)
// attached by BOTH bars, and the game bar's half is evidenced by stills and two
// clips. The GPS half was evidenced only by reading the code — `golfin-reviewer`
// called that out, and SPEC A15 asks for "the GPS hub selected slot (1)" by name.
// Code-sharing is an argument, not a photograph, and this task has already been
// bitten once by a fix that was provably correct in source and did nothing on
// screen (the fake-null CanvasGroup, § "It shipped broken once").
//
// REAL NAVIGATION, not a harness (PIPELINE_HARDENING rule 2, and the standing
// rule after `gps_profile_pack`): boot -> tap the real StartButton -> invoke the
// real GpsPill.onClick -> invoke the real nav-bar slot's onClick. No
// ShowScreen(), which swaps screens BEHIND the title gate and makes
// CurrentScreen a false positive.
//
// CAPTURE: CaptureCore.SnapPlayModeSafe, the sanctioned path — synchronous,
// no AssetDatabase.Refresh (which would domain-reload and kill this coroutine),
// no pause. Every frame is validated: the file must exist AND differ from the
// previous one, because SnapPlayModeSafe can return a path it never wrote and
// can return byte-identical STALE frames for different states.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Golfin.Diagnostics.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    [InitializeOnLoad]
    public static class GpsNavStillCapture
    {
        const string OutDir   = "Docs/Specs/Active/game_polish_a/screenshots";
        const string ArmedKey = "GpsNavStillCapture.Armed";

        // Entering play mode DOMAIN-RELOADS, which wipes any delegate this class subscribed to
        // before the transition — the first version armed `EditorApplication.update` and the
        // subscription simply ceased to exist, so nothing ever ran. SessionState survives the
        // reload; the static ctor re-subscribes on the other side. Same shape as
        // GamePolishDemoRecorder's ArmedKey.
        static GpsNavStillCapture() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/Game Polish/Capture the GPS nav selected stills", priority = 267)]
        public static void Launch()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GpsNavStill] Already playing — stop first.");
                return;
            }
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GpsNavStill] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            var host = new GameObject("~GpsNavStillCapture");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        class Runner : MonoBehaviour
        {
            string _last = "";

            void Start()
            {
                // Without this the Editor stops rendering when it loses focus and every capture
                // comes back as the splash frame (memory: reference_playmode_capture_runinbackground).
                Application.runInBackground = true;
                StartCoroutine(Go());
            }

            static void Line(string s) => Debug.Log("[GpsNavStill] " + s);

            IEnumerator Go()
            {
                yield return TapNamed("StartButton", 90f);
                yield return new WaitForSecondsRealtime(3f);

                yield return TapNamed("GpsPill", 30f);
                yield return new WaitForSecondsRealtime(3f);
                yield return Snap("d7_gps_bar_hub_selected");

                // A second GPS screen, so the artifact shows the highlight MOVING rather than one
                // slot that might simply be painted that way.
                yield return TapNamed("NavProfileButton", 30f);
                yield return new WaitForSecondsRealtime(3f);
                yield return Snap("d7_gps_bar_profile_selected");

                Line("done");
                EditorApplication.isPlaying = false;
            }

            IEnumerator TapNamed(string name, float seconds)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    Button? b = UnityEngine.Object
                        .FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                        .FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
                    if (b != null)
                    {
                        Line("tapping the real " + name);
                        b.onClick.Invoke();
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: " + name + " never appeared in " + seconds + " s");
            }

            IEnumerator Snap(string label)
            {
                // END OF FRAME, not just "next frame". SnapPlayModeSafe uses
                // ScreenCapture.CaptureScreenshotAsTexture in play mode, which returns NULL unless
                // the backbuffer is readable — and when it does, SnapPlayModeSafe logs a warning,
                // skips the write, and STILL RETURNS THE PATH. That is the whole "phantom path"
                // failure: a filename for a file that was never written.
                yield return new WaitForEndOfFrame();
                string path = CaptureCore.SnapPlayModeSafe(label);

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    Line("FAIL " + label + ": SnapPlayModeSafe returned a path it never wrote (" + path + ")");
                    yield break;
                }
                string md5 = Md5(path);
                if (md5 == _last)
                {
                    Line("FAIL " + label + ": STALE frame — byte-identical to the previous capture");
                    yield break;
                }
                _last = md5;

                Directory.CreateDirectory(OutDir);
                string dest = Path.Combine(OutDir, label + ".png");
                File.Copy(path, dest, true);
                Line("ok " + dest + "  md5=" + md5.Substring(0, 8)
                     + "  screen=" + (GolfinRedux.UI.ScreenManager.Instance != null
                                      ? GolfinRedux.UI.ScreenManager.Instance.CurrentScreen.ToString() : "?"));
            }

            static string Md5(string path)
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                using (var fs = File.OpenRead(path))
                    return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "");
            }
        }
    }
}
