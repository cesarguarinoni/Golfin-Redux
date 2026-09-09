#if UNITY_EDITOR
// Assets/Editor/ConsoleSweepRunner.cs
// polish_regressions_0909 — "every shell screen once through real navigation, count of
// errors/warnings per screen".
//
// WHY IT IS NOT THE DESIGN AUDIT. DesignAuditRunner navigates the same screens but its artifact
// is a per-screen JSON dump; the Console is incidental to it, and a count of "0 across the whole
// run" cannot say WHICH screen was quiet. This attributes every log line to the screen that was
// on when it was emitted, which is the only form in which the number is actionable — and the only
// form in which two builds can be diffed.
//
// Deliberately dependency-free (ScreenManager + Application.logMessageReceived and nothing else)
// so the SAME file can be dropped into an older checkout and produce a comparable table.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    public static class ConsoleSweepRunner
    {
        const string ArmedKey = "ConsoleSweepRunner.Armed";
        const string OutDir   = "Docs/Diagnostics/_capture";

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("GOLFIN/Design Audit/Console sweep — per screen", priority = 408)]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ConsoleSweep] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutDir);
            Application.runInBackground = true;
            SessionState.SetString(ArmedKey, "1");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (SessionState.GetString(ArmedKey, "") != "1") return;
            SessionState.SetString(ArmedKey, "");
            var host = new GameObject("[ConsoleSweep]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        class Tally
        {
            public string Screen = "", How = "";
            public int Errors, Warnings, Exceptions;
            public readonly List<string> Samples = new List<string>();
            public int Total => Errors + Warnings + Exceptions;
        }

        class Runner : MonoBehaviour
        {
            Tally _live;
            readonly List<Tally> _all = new List<Tally>();

            void OnEnable()  => Application.logMessageReceived += OnLog;
            void OnDisable() => Application.logMessageReceived -= OnLog;

            void OnLog(string msg, string stack, LogType type)
            {
                if (_live == null) return;
                switch (type)
                {
                    case LogType.Error:     _live.Errors++;     break;
                    case LogType.Assert:    _live.Errors++;     break;
                    case LogType.Exception: _live.Exceptions++; break;
                    case LogType.Warning:   _live.Warnings++;   break;
                    default: return;   // Log — not counted
                }
                // The FIRST distinct line of each kind is what makes a non-zero count actionable.
                string head = (msg ?? "").Split('\n')[0];
                if (head.Length > 150) head = head.Substring(0, 150);
                string tagged = type + ": " + head;
                if (!_live.Samples.Contains(tagged) && _live.Samples.Count < 6) _live.Samples.Add(tagged);
            }

            void Start() => StartCoroutine(Sweep());

            static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

            static Button Find(string name) => Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == name
                                     && !string.IsNullOrEmpty(b.gameObject.scene.name)
                                     && b.isActiveAndEnabled);

            IEnumerator Measure(string screen, string how, Func<IEnumerator> go)
            {
                var t = new Tally { Screen = screen, How = how };
                _live = t;                      // arm BEFORE the navigation, so the cost of ARRIVING counts
                yield return go();
                yield return Wait(3.0f);        // and settle, so late OnEnable work is attributed here
                _live = null;
                _all.Add(t);
                Debug.Log($"[ConsoleSweep] {screen}: err={t.Errors} warn={t.Warnings} exc={t.Exceptions}");
            }

            IEnumerator TapNamed(string button)
            {
                var b = Find(button);
                if (b == null) { Debug.LogWarning($"[ConsoleSweep] button '{button}' not found"); yield break; }
                b.onClick.Invoke();
                yield return Wait(2.0f);
            }

            static bool Show(string screenId)
            {
                var smT = Type.GetType("GolfinRedux.UI.ScreenManager, Assembly-CSharp");
                var idT = Type.GetType("GolfinRedux.UI.ScreenId, Assembly-CSharp");
                if (smT == null || idT == null) return false;
                object sm = smT.GetProperty("Instance")?.GetValue(null);
                if (sm == null) return false;
                if (!Enum.GetNames(idT).Contains(screenId)) return false;   // id absent in this build
                var m = smT.GetMethods().FirstOrDefault(x => x.Name == "ShowScreen" && x.GetParameters().Length >= 1);
                if (m == null) return false;
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                args[0] = Enum.Parse(idT, screenId);
                for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                m.Invoke(sm, args);
                return true;
            }

            IEnumerator Sweep()
            {
                // Boot through the REAL gate.
                float waited = 0f; bool tapped = false;
                while (waited < 60f)
                {
                    var nav = Find("NavHomeButton") ?? Find("NavTeeButton");
                    if (nav != null) break;
                    if (!tapped)
                    {
                        var start = Resources.FindObjectsOfTypeAll<Button>()
                            .FirstOrDefault(b => b.gameObject.name == "StartButton"
                                                 && !string.IsNullOrEmpty(b.gameObject.scene.name)
                                                 && b.gameObject.activeInHierarchy);
                        if (start != null) { start.onClick.Invoke(); tapped = true; }
                    }
                    waited += Time.unscaledDeltaTime; yield return null;
                }
                yield return Wait(3f);

                // ── real navigation: the bottom nav is the player's own path ──
                yield return Measure("Home",          "bottom-nav NavHomeButton",       () => TapNamed("NavHomeButton"));
                yield return Measure("GeneralShop",   "bottom-nav NavGachaButton",      () => TapNamed("NavGachaButton"));
                yield return Measure("ModeSelection", "bottom-nav NavTeeButton",        () => TapNamed("NavTeeButton"));
                yield return Measure("Inventory",     "bottom-nav NavInventoryButton",  () => TapNamed("NavInventoryButton"));
                yield return Measure("Roster",        "bottom-nav NavCharactersButton", () => TapNamed("NavCharactersButton"));
                yield return Measure("SettingsOverlay", "SettingsButton.onClick",       () => TapNamed("SettingsButton"));
                yield return Measure("HomeReturn",    "bottom-nav NavHomeButton",       () => TapNamed("NavHomeButton"));

                // ── the rest of the shell: no player path from a fresh session, so re-seated ──
                foreach (string id in new[]
                {
                    "HoleSelection", "MissionSelection", "TournamentSelection",
                    "TournamentHoleSelection", "TournamentLeaderboard", "Leaderboard",
                    "GachaHistory", "GachaPrizes", "StaminaShopSelection", "StaminaShopDetail",
                })
                {
                    string screenId = id;
                    yield return Measure(screenId, "ShowScreen (no player path from a fresh session)",
                                         () => ShowRoutine(screenId));
                }

                Write();
                yield return Wait(0.5f);
                EditorApplication.ExitPlaymode();
            }

            IEnumerator ShowRoutine(string id)
            {
                if (!Show(id)) Debug.LogWarning($"[ConsoleSweep] ShowScreen({id}) unavailable in this build");
                yield return Wait(2.0f);
            }

            void Write()
            {
                string sha = "unknown";
                try
                {
                    var p = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short HEAD")
                        {
                            WorkingDirectory = Directory.GetParent(Application.dataPath)!.FullName,
                            RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                        }
                    };
                    p.Start(); sha = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(4000);
                }
                catch { }

                var sb = new StringBuilder();
                sb.AppendLine($"console sweep @ {sha}   (GOLFIN/Design Audit/Console sweep — per screen)");
                sb.AppendLine("Every log line is attributed to the screen that was on when it was emitted;");
                sb.AppendLine("the counter is armed BEFORE the navigation, so the cost of arriving counts.");
                sb.AppendLine();
                sb.AppendLine($"{"screen",-26}{"err",5}{"warn",6}{"exc",5}   how");
                sb.AppendLine(new string('-', 92));
                foreach (var t in _all)
                    sb.AppendLine($"{t.Screen,-26}{t.Errors,5}{t.Warnings,6}{t.Exceptions,5}   {t.How}");
                sb.AppendLine(new string('-', 92));
                sb.AppendLine($"{"TOTAL",-26}{_all.Sum(x => x.Errors),5}{_all.Sum(x => x.Warnings),6}{_all.Sum(x => x.Exceptions),5}"
                            + $"   {_all.Count} screen(s)");
                sb.AppendLine();
                foreach (var t in _all.Where(x => x.Samples.Count > 0))
                {
                    sb.AppendLine($"── {t.Screen}");
                    foreach (var s in t.Samples) sb.AppendLine("   " + s);
                    sb.AppendLine();
                }

                string path = Path.Combine(OutDir, $"console_sweep_{sha}.txt");
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"[ConsoleSweep] wrote {path}\n{sb}");
            }
        }
    }
}
#endif
