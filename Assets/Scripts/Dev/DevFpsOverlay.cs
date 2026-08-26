// DevFpsOverlay.cs — on-screen fps / frame-time / GC / thermal readout for dev builds.
//
// WHY THIS EXISTS
// ---------------
// perf_phase1_free_wins measured everything through PerfBaselineBot, which reports to the device
// console. That is the right shape for automation and the wrong shape for a human holding the
// phone: playing a hole and wanting to know whether it is holding 60 tells you nothing unless you
// are tailing a log on a Mac. This puts the same four numbers on the glass.
//
// SHIPPING
// --------
// Gated on GOLFIN_TESTBUILD exactly like PerfBaselineBot. iOS-Full.asset — the profile the store
// pipeline builds — carries zero scripting defines, so neither UNITY_EDITOR nor GOLFIN_TESTBUILD
// is defined there and this file compiles to nothing.
//
// IT DOES NOT RUN WHILE THE BOT IS MEASURING
// ------------------------------------------
// IMGUI allocates every frame. Leaving this on during a PerfBaselineBot run would inflate the
// gcPerFrameB figure that Phase 1 reports (21,506 B/frame) by an amount that has nothing to do
// with the game. So the overlay uses the same arm signal as the bot, inverted: if
// Documents/perfbot/job.txt is present the launch belongs to automation and the overlay stays off.
// One of the two is on, never both.

#if UNITY_EDITOR || GOLFIN_TESTBUILD
using System;
using System.Runtime.InteropServices;
using Unity.Profiling;
using UnityEngine;

namespace Golfin.Dev
{
    /// <summary>
    /// Self-installing dev HUD: fps, frame ms, GC bytes/frame, iOS thermal state.
    /// No scene or prefab wiring — it spawns itself and draws with IMGUI.
    /// </summary>
    public sealed class DevFpsOverlay : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;   // rebuild the string 4x/sec, not every frame
        const string EnabledKey    = "golfin.dev.fpsOverlay";   // 0 hides it; survives relaunch

        static bool BotIsArmed()
        {
            try
            {
                return System.IO.File.Exists(
                    System.IO.Path.Combine(Application.persistentDataPath, "perfbot", "job.txt"));
            }
            catch { return false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            // Never draw over an automated run — IMGUI allocations would corrupt gcPerFrameB.
            if (BotIsArmed()) return;
            if (PlayerPrefs.GetInt(EnabledKey, 1) == 0) return;

            var go = new GameObject("~DevFpsOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<DevFpsOverlay>();
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int GolfinGetThermalState();
#endif
        static string Thermal()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                switch (GolfinGetThermalState())
                {
                    case 0: return "Nominal";
                    case 1: return "Fair";
                    case 2: return "Serious";
                    case 3: return "Critical";
                    default: return "n/a";
                }
            }
            catch { return "n/a"; }
#else
            return "editor";
#endif
        }

        ProfilerRecorder _gc;
        float  _accum;            // seconds accumulated since the last string rebuild
        int    _frames;           // frames in that window
        string _line1 = "…";
        string _line2 = "";
        GUIStyle _style;
        Texture2D _bg;

        void OnEnable()
        {
            // "GC Allocated In Frame" is the same counter PerfBaselineBot samples, so the number
            // on screen and the number in the device log are directly comparable.
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        void OnDisable()
        {
            if (_gc.Valid) _gc.Dispose();
            if (_bg != null) Destroy(_bg);
        }

        void Update()
        {
            _accum  += Time.unscaledDeltaTime;
            _frames += 1;
            if (_accum < RefreshSeconds) return;

            float fps = _frames / _accum;
            float ms  = (_accum / _frames) * 1000f;
            _accum = 0f; _frames = 0;

            long gc = _gc.Valid ? _gc.LastValue : -1;
            _line1 = $"{fps:F1} fps   {ms:F1} ms";
            _line2 = gc >= 0
                ? $"GC {gc / 1024f:F1} KB/f   {Thermal()}"
                : Thermal();
        }

        void OnGUI()
        {
            if (_style == null)
            {
                _bg = new Texture2D(1, 1);
                _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                _bg.Apply();
                _style = new GUIStyle(GUI.skin.label)
                {
                    // Screen.height is in pixels here; scale so the readout is legible on a phone
                    // as well as in a small Game View.
                    fontSize  = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.013f)),
                    alignment = TextAnchor.UpperLeft,
                    padding   = new RectOffset(10, 10, 6, 6),
                    wordWrap  = false,   // never let the two lines reflow into a clipped third
                };
                _style.normal.textColor = Color.white;
                _style.normal.background = _bg;
            }

            // Left edge, below the wind pill and player card, above the club buttons — the band
            // that is empty grass on every hole. Deliberately not a corner: the corners carry the
            // player card, the gear, the version stamp and the club/spin controls.
            // Size to the actual text so nothing is ever clipped, whatever the screen.
            var content = new GUIContent(_line1 + "\n" + _line2);
            var size    = _style.CalcSize(content);
            float w = size.x + 4f;
            float h = size.y + 4f;
            var r = new Rect(12f, Screen.height * 0.30f, w, h);
            GUI.Label(r, _line1 + "\n" + _line2, _style);
        }
    }
}
#endif
