// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D7 — the gate for this slice.
//
// WHY A SIBLING AND NOT MODES ON GamePolishProbe. §D7 says "GamePolishProbe (a) gains modes".
// That probe belongs to game_polish_a, which is DONE, and its Driver is one class whose Route()
// walks a-specific screens and whose output paths are a-specific; adding b's modes inside it
// means editing the gate of a completed, approved task to serve this one. A sibling reuses the
// same arming pattern and the same CaptureCore idiom and cannot regress a's evidence. Deviation
// D-8 in the report.
//
// MODES
//   modals   §D1 / A1 — every game modal opened and closed, with IsVisible and OpenModalCount
//            sampled on the exact frames the SPEC names, a mid-pop frame captured per modal,
//            and a per-assertion PASS/FAIL JSON.
//   shimmer  §D4 / A6 — walk the shimmer screens and record what each site's PaintGate actually
//            decided, plus whether the host was shown.
//   perf     §A13 — GC per frame over the pop of every modal, no captures (a 1170x2532
//            ReadPixels allocates ~100 MB and would swamp the very thing being measured — the
//            gps_polish A13 lesson, inherited).
//
// ON "REAL TRIGGER" (PIPELINE_HARDENING rule 2). Some of these modals are only reachable by
// finishing a 1v1, finishing a tournament, holing out, or spending currency on a gacha pull —
// none of which a harness can manufacture without becoming a second, fake game. So each record
// carries `realWidget` and, when it is false, the REASON. A harness-driven open is measured
// (the pop is a property of ModalController, not of what opened it) but is never reported as a
// tap. That is the a-probe's own rule and it is inherited deliberately.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using UnityEditor;
using UnityEngine;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishProbeB
    {
        const string ArmedKey = "game_polish_b.probe.armed";
        const string ModeKey  = "game_polish_b.probe.mode";

        const string TaskDir  = "Docs/Specs/Active/game_polish_b";
        const string ShotDir  = TaskDir + "/screenshots";
        const string OutDir   = "Docs/Diagnostics/_capture";

        [MenuItem("GOLFIN/Game Polish/Probe B — modals (D1/A1)", priority = 280)]
        public static void ArmModals() => Arm("modals");

        [MenuItem("GOLFIN/Game Polish/Probe B — shimmer (D4/A6)", priority = 281)]
        public static void ArmShimmer() => Arm("shimmer");

        [MenuItem("GOLFIN/Game Polish/Probe B — perf (A13)", priority = 282)]
        public static void ArmPerf() => Arm("perf");

        public static void Arm(string mode)
        {
            EditorPrefs.SetBool(ArmedKey, true);
            EditorPrefs.SetString(ModeKey, mode);
            Debug.Log($"[GamePolishProbeB] armed ({mode}) — entering play mode.");
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            if (!EditorPrefs.GetBool(ArmedKey, false)) return;
            EditorPrefs.SetBool(ArmedKey, false);
            var go = new GameObject("__GamePolishProbeB");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        };

        // ═════════════════════════════════════════════════════════════════════

        /// <summary>One modal, measured.</summary>
        sealed class ModalRecord
        {
            public string Name = "", Path = "", Reason = "";
            public bool   AnimateShow;
            public bool   RealWidget;
            /// <summary>IsVisible() on the SAME frame Show() was called — §D1.2 pins this true.</summary>
            public bool   VisibleOnShowFrame;
            /// <summary>IsVisible() on the SAME frame Hide() was called — pinned false.</summary>
            public bool   VisibleOnHideFrame;
            public int    OpenCountBefore, OpenCountOnShow, OpenCountAfterHide;
            /// <summary>Panel scale sampled one frame into the pop. Between 0.9 and 1 means it
            /// is actually popping; exactly 1 means it snapped.</summary>
            public float  MidPopScale = -1f;
            public string Shot = "";
            /// <summary>Whether the modal's hierarchy had to be activated to measure it. A modal
            /// under a screen root that is not showing has isActiveAndEnabled false, and UiMotion
            /// settles instead of animating — so an un-activated measurement reads "did not pop"
            /// for a modal that pops perfectly well when its screen is on, which is the only way
            /// a player ever sees it.</summary>
            public bool   Activated;
            /// <summary>Show() is overridden as a documented no-op on this controller, so the
            /// base-class visibility assertions do not apply to it.</summary>
            public bool   ShowIsNoOp;
            public readonly List<string> Fails = new List<string>();
        }

        public sealed class Driver : MonoBehaviour
        {
            readonly StringBuilder _log = new StringBuilder();
            readonly List<ModalRecord> _modals = new List<ModalRecord>();
            string _mode = "modals";
            int _shot;

            void Start()
            {
                // Without this the Editor stops rendering when it loses focus and every capture
                // comes back as whatever it drew last.
                Application.runInBackground = true;
                _mode = EditorPrefs.GetString(ModeKey, "modals");
                StartCoroutine(Run());
            }

            IEnumerator Run()
            {
                Line($"=== game_polish_b probe ({_mode}) {DateTime.UtcNow:u} ===");
                yield return new WaitForSecondsRealtime(3f);   // boot + first frames

                switch (_mode)
                {
                    case "modals":  yield return Modals();  break;
                    case "shimmer": yield return Shimmer(); break;
                    case "perf":    yield return Perf();    break;
                    default:        Line("unknown mode " + _mode); break;
                }

                Write();
                Line("=== done: " + _mode + " ===");
                Debug.Log(_log.ToString());
                EditorApplication.isPlaying = false;
            }

            // ── modals ───────────────────────────────────────────────────────

            /// <summary>
            /// Modals whose only real trigger is an event a harness cannot manufacture, and the
            /// reason for each. Anything NOT in here that still opens via Show() is a gap, and
            /// the JSON says so rather than the log quietly omitting it.
            /// </summary>
            static readonly Dictionary<string, string> NoHarnessPath = new Dictionary<string, string>
            {
                ["VersusResultModalController"]     = "opens only after a finished 1v1 match",
                ["TournamentResultModalController"] = "opens only after a tournament resolves server-side",
                ["HoleCompleteModalController"]     = "opens only on holing out; the widget owns visibility (§D1.3)",
                ["GachaRevealModalController"]      = "opens only from a PULL, which spends real currency",
                ["MatchmakingModalController"]      = "opens a live queue against the matchmaking service",
                ["StartingCharacterConfirmModalController"] = "starter flow only; reachable once per save",
                ["InGameSettingsModalController"]   = "lives in the gameplay scene, not the shell",
                ["SchemeConfirmModalController"]    = "opens from the in-gameplay scheme picker",
            };

            IEnumerator Modals()
            {
                var all = new List<ModalController>();
                foreach (ModalController m in
                         UnityEngine.Object.FindObjectsByType<ModalController>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    all.Add(m);

                Line($"found {all.Count} ModalController instances");

                // RECONCILE AGAINST WHAT THE BUILDER FLAGS. A modal that exists in the scene at
                // author time but is not present at RUNTIME — destroyed by a presenter, spawned
                // from a prefab on demand, living in the gameplay scene — is invisible to a probe
                // that only reports what it found. Naming the expected set turns that silence
                // into a line.
                var seen = new HashSet<string>();
                foreach (ModalController m in all) if (m != null) seen.Add(m.GetType().Name);
                foreach (string expected in ExpectedControllers)
                    if (!seen.Contains(expected))
                        Line($"  NOT PRESENT AT RUNTIME: {expected} — " +
                             (RuntimeAbsent.TryGetValue(expected, out string? why) ? why : "REASON UNKNOWN — investigate"));

                foreach (ModalController m in all)
                {
                    if (m == null) continue;
                    string type = m.GetType().Name;

                    // GPS modals belong to gps_polish and are already popping; this is the game's gate.
                    if (IsGps(type)) continue;

                    var r = new ModalRecord
                    {
                        Name = type,
                        Path = PathOf(m.transform),
                        AnimateShow = m.AnimatesShow,
                        RealWidget = false,
                        Reason = NoHarnessPath.TryGetValue(type, out string? why)
                               ? why
                               : "harness-driven Show(); a real trigger exists but is not wired into this probe",
                    };

                    if (!r.AnimateShow) r.Fails.Add("animateShow is FALSE — this modal snaps (§D1.1)");

                    r.ShowIsNoOp = ShowNoOp.TryGetValue(type, out string? noop);
                    if (r.ShowIsNoOp) r.Reason = noop!;

                    // Give it a live hierarchy, or UiMotion will settle instead of animating and
                    // the whole measurement is meaningless.
                    List<GameObject> turnedOn = ActivateChain(m);
                    r.Activated = turnedOn.Count > 0;
                    if (r.Activated)
                    {
                        // WAIT FOR THE FRAME CLOCK TO SETTLE, rather than for a fixed delay.
                        //
                        // Activating a screen root runs its controller's OnEnable — Inventory
                        // rebuilds every tab, Missions re-resolves its loadouts — and that frame
                        // can take longer than PopDur. A tween whose first frame exceeds its
                        // duration correctly lands on its end value in one step, so the probe
                        // reads scale 1.0 and reports "did not pop" for a modal that pops fine.
                        //
                        // A fixed 0.5 s delay was tried and is NOT good enough: it moved the
                        // failure from the first modal on the Inventory branch to the next one,
                        // because which modal pays the rebuild cost depends on FindObjectsByType's
                        // ordering, which is not stable between runs. Waiting for the thing that
                        // actually matters — several consecutive SHORT frames — is both robust
                        // and self-describing.
                        int calm = 0, spun = 0;
                        while (calm < 5 && spun++ < 900)
                        {
                            yield return null;
                            calm = Time.unscaledDeltaTime < UiMotion.PopDur * 0.25f ? calm + 1 : 0;
                        }
                        if (calm < 5)
                            r.Fails.Add($"frame clock never settled after activation (last dt " +
                                        $"{Time.unscaledDeltaTime:F3}s) — the pop measurement is not trustworthy");
                    }

                    r.OpenCountBefore = ModalController.OpenModalCount;

                    // ── Show, and sample on the SAME frame ────────────────────
                    bool threw = false;
                    try { m.Show(); }
                    catch (Exception ex) { threw = true; Line($"  {type}: Show() threw {ex.GetType().Name}: {ex.Message}"); }

                    if (threw)
                    {
                        r.Fails.Add("Show() threw");
                        RestoreChain(turnedOn);
                        _modals.Add(r);
                        continue;
                    }

                    r.VisibleOnShowFrame = m.IsVisible();
                    r.OpenCountOnShow    = ModalController.OpenModalCount;

                    // The visibility contract belongs to ModalController.Show(). A controller that
                    // overrides it as a documented no-op is not in breach of it — it opted out, and
                    // §D1.3 covers what opens it instead.
                    if (!r.ShowIsNoOp)
                    {
                        if (!r.VisibleOnShowFrame)
                            r.Fails.Add("IsVisible() false on the frame Show() ran — §D1.2 pins it true from frame 0");
                        if (r.OpenCountOnShow != r.OpenCountBefore + 1)
                            r.Fails.Add($"OpenModalCount {r.OpenCountBefore} -> {r.OpenCountOnShow}, expected +1");
                    }

                    // ── one frame in: is it actually popping? ─────────────────
                    yield return null;
                    GameObject? panel = PanelOf(m);
                    if (panel != null) r.MidPopScale = panel.transform.localScale.x;
                    if (r.AnimateShow && !r.ShowIsNoOp && panel != null
                        && Mathf.Approximately(r.MidPopScale, 1f))
                        r.Fails.Add($"panel scale is {r.MidPopScale:F4} one frame in — animateShow is set but nothing popped");

                    // ── a frame the reviewer can look at, mid-pop ─────────────
                    yield return Shot(_mode + "_" + type, r);

                    // let the pop finish so the Hide is measured from rest
                    yield return new WaitForSecondsRealtime(UiMotion.PopDur + 0.1f);

                    // ── Hide, and sample on the SAME frame ────────────────────
                    // Guarded like Show: these are real controllers being driven without the data
                    // their real trigger would have supplied, and an OnHide that reaches for it
                    // would otherwise kill the coroutine and take the whole run's JSON with it.
                    try { m.Hide(); }
                    catch (Exception ex)
                    {
                        r.Fails.Add("Hide() threw " + ex.GetType().Name);
                        Line($"  {type}: Hide() threw {ex.GetType().Name}: {ex.Message}");
                    }
                    r.VisibleOnHideFrame = m.IsVisible();
                    if (r.VisibleOnHideFrame)
                        r.Fails.Add("IsVisible() true on the frame Hide() ran — pinned false from frame 0");

                    yield return new WaitForSecondsRealtime(UiMotion.FadeDur + 0.2f);
                    r.OpenCountAfterHide = ModalController.OpenModalCount;
                    if (!r.ShowIsNoOp && r.OpenCountAfterHide != r.OpenCountBefore)
                        r.Fails.Add($"OpenModalCount did not return to {r.OpenCountBefore} (is {r.OpenCountAfterHide})");

                    RestoreChain(turnedOn);

                    Line($"  {type,-42} animateShow={r.AnimateShow} activated={r.Activated} " +
                         (r.ShowIsNoOp ? "showIsNoOp=TRUE " : "") + $"midPopScale={r.MidPopScale:F4} " +
                         $"visibleOnShow={r.VisibleOnShowFrame} openCount={r.OpenCountBefore}->{r.OpenCountOnShow}" +
                         $"->{r.OpenCountAfterHide} fails={r.Fails.Count}" +
                         (r.RealWidget ? "" : "   [harness-driven: " + r.Reason + "]"));

                    _modals.Add(r);
                }
            }

            /// <summary>The fifteen game modals GamePolishBuilder.ApplyModals flags (§D1.1).</summary>
            static readonly string[] ExpectedControllers =
            {
                "LevelUpModalController", "ClubLevelUpModalController", "BagSelectionModalController",
                "BagClubModalController", "ItemUseModalController", "VersusResultModalController",
                "MatchmakingModalController", "TournamentSignupModalController",
                "TournamentResultModalController", "GachaRatesModalController",
                "GachaRevealModalController", "StartingCharacterConfirmModalController",
                "InGameSettingsModalController", "SchemeConfirmModalController",
                "HoleCompleteModalController",
            };

            /// <summary>Why an expected modal is legitimately absent from a running shell.</summary>
            static readonly Dictionary<string, string> RuntimeAbsent = new Dictionary<string, string>
            {
                ["InGameSettingsModalController"] =
                    "prefab-only; it is spawned into the GAMEPLAY scene, and has no ShellScene instance (§D1.1)",
            };

            static long Median(List<long> xs)
            {
                if (xs == null || xs.Count == 0) return 0;
                var copy = new List<long>(xs);
                copy.Sort();
                return copy[copy.Count / 2];
            }

            static long Max(List<long> xs)
            {
                long m = 0;
                for (int i = 0; i < (xs?.Count ?? 0); i++) if (xs![i] > m) m = xs[i];
                return m;
            }

            static bool IsGps(string type) =>
                type is "VenuePickerModalController" or "VoteCreateModalController"
                     or "GiftSendModalController" or "CheckInConfirmModalController"
                     or "RoundCompleteModalController";

            /// <summary>The modal's panel — the thing that pops. Public field on ModalController.</summary>
            static GameObject? PanelOf(ModalController m) => m.modalPanel;

            /// <summary>
            /// Controllers that override Show() as a deliberate no-op, and why. For these the
            /// base class's visibility contract does not apply — asserting it would be scoring
            /// correct, documented behaviour as a failure.
            /// </summary>
            static readonly Dictionary<string, string> ShowNoOp = new Dictionary<string, string>
            {
                ["VersusResultModalController"] = "Show() is a no-op by design; callers must use ShowResult() so live data is bound first",
                ["HoleCompleteModalController"] = "Show() is a no-op by design; HoleCompleteWidget owns visibility via _root.SetActive (§D1.3)",
            };

            /// <summary>
            /// Turn on every inactive ancestor so the modal can actually animate, remembering what
            /// was off so it can be put back. Returns the objects it switched on, outermost first.
            ///
            /// <para>This is a HARNESS action and is recorded as one (`activated` in the JSON). It
            /// does not make the measurement fake — the pop is a property of ModalController and
            /// the CanvasGroup, and the state being restored is the state a player would be in
            /// when they open it. What it does is stop the probe from reporting "did not pop" for
            /// eight modals whose only sin was living on a screen that was not showing.</para>
            /// </summary>
            static List<GameObject> ActivateChain(ModalController m)
            {
                var turnedOn = new List<GameObject>();
                var chain = new List<GameObject>();
                for (Transform t = m.transform; t != null; t = t.parent)
                    if (!t.gameObject.activeSelf) chain.Add(t.gameObject);

                // Outermost first: activating a child of something still off changes nothing.
                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    chain[i].SetActive(true);
                    turnedOn.Add(chain[i]);
                }
                return turnedOn;
            }

            static void RestoreChain(List<GameObject> turnedOn)
            {
                // Innermost first, so nothing is left half-restored if one has been destroyed.
                for (int i = turnedOn.Count - 1; i >= 0; i--)
                    if (turnedOn[i] != null) turnedOn[i].SetActive(false);
            }

            // ── shimmer ──────────────────────────────────────────────────────

            IEnumerator Shimmer()
            {
                // What can be proven here WITHOUT faking a cold fetch: that every declared site
                // has a host, that none is showing at rest, and what each host's blocks are. The
                // PAINT VERDICTS (`paint(cache)` / `paint(fetch)` / shimmer shown) are emitted by
                // the gates themselves into the Console during a real screen visit — this run
                // records the resting truth those lines are read against.
                foreach (string site in GameShimmerSites.All)
                {
                    Golfin.Gps.UI.ShimmerHost? host = null;
                    foreach (GameObject root in UnityEngine.SceneManagement.SceneManager
                                                 .GetActiveScene().GetRootGameObjects())
                    {
                        host = Golfin.Gps.UI.ShimmerHost.Find(root, site);
                        if (host != null) break;
                    }

                    if (host == null) { Line($"  {site,-26} NO HOST — ApplyShimmer has not been run"); continue; }

                    int blocks = host.GetComponentsInChildren<Golfin.Gps.UI.ShimmerBlock>(true).Length;
                    bool active = host.gameObject.activeSelf;
                    Line($"  {site,-26} host={PathOf(host.transform)} blocks={blocks} activeAtRest={active}" +
                         (active ? "   *** ACTIVE AT REST — every rest-parity capture is wrong ***" : ""));
                    yield return null;
                }
            }

            // ── perf ─────────────────────────────────────────────────────────

            IEnumerator Perf()
            {
                var gc = Unity.Profiling.ProfilerRecorder.StartNew(
                             Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
                yield return null;

                // A BASELINE FIRST, AND A MEDIAN — not a max. "GC Allocated In Frame" is the
                // WHOLE frame: the Editor, the shell, every controller's Update. Two things follow.
                //
                // One: the absolute number cannot be compared to A13's 32 B/frame, which is the
                // ISOLATED per-tween budget pinned by the unit tests. What a probe can honestly
                // contribute is the DELTA over an idle frame.
                //
                // Two: it must be a median. The first attempt took the MAX over the idle window,
                // caught a single ~7 MB boot spike in it, and reported every modal as SEVEN
                // MEGABYTES BETTER than baseline — a number that is not merely useless but
                // actively misleading. One outlier frame destroys a max; it barely moves a median.
                yield return new WaitForSecondsRealtime(2f);        // let boot finish first

                var idle = new List<long>();
                float idleUntil = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < idleUntil)
                {
                    yield return null;
                    idle.Add(gc.LastValue);
                }
                long idleMedian = Median(idle);
                Line($"  BASELINE (nothing animating): median whole-frame GC {idleMedian} B, " +
                     $"max {Max(idle)} B, over {idle.Count} frames");
                Line("  Rows are MEDIAN whole-frame GC during the pop and the delta over that baseline.");
                Line("  A13's <= 32 B/frame is the ISOLATED per-tween budget, pinned by UiMotionTests —");
                Line("  a whole-frame figure can neither prove nor disprove it, and is not offered as doing so.");

                foreach (ModalController m in
                         UnityEngine.Object.FindObjectsByType<ModalController>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (m == null || IsGps(m.GetType().Name)) continue;

                    var during = new List<long>();
                    m.Show();
                    float deadline = Time.realtimeSinceStartup + UiMotion.PopDur + 0.1f;
                    while (Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        during.Add(gc.LastValue);
                    }
                    m.Hide();
                    yield return new WaitForSecondsRealtime(UiMotion.FadeDur + 0.1f);

                    long med = Median(during);
                    Line($"  {m.GetType().Name,-42} medianFrameGC={med} B  delta={med - idleMedian:+#;-#;0} B" +
                         $"  max={Max(during)} B  ({during.Count} frames)");
                }
                if (gc.Valid) gc.Dispose();
            }

            // ── plumbing ─────────────────────────────────────────────────────

            IEnumerator Shot(string label, ModalRecord? r)
            {
                _shot++;
                string name = string.Format("{0}_{1:00}_{2}", _mode, _shot, Sanitise(label));
                string path = Path.Combine(ShotDir, name + ".png");
                Directory.CreateDirectory(ShotDir);

                IEnumerator snap = CaptureCore.SnapAtEndOfFrameAndPause(name, path, skipPause: true);
                while (snap.MoveNext()) yield return snap.Current;

                // Assert the FILE, never the return value — SnapPlayModeSafe has logged a path
                // for a file it never wrote (project memory: snapplaymodesafe_phantom_path).
                if (!File.Exists(path))
                {
                    Line("  SHOT " + label + " -> MISSING (" + path + ")");
                    r?.Fails.Add("mid-pop capture missing");
                    yield break;
                }
                if (r != null) r.Shot = path;
                Line($"  SHOT {label} -> {path} ({new FileInfo(path).Length / 1024} KB)");
            }

            static string Sanitise(string s)
            {
                var sb = new StringBuilder(s.Length);
                foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
                return sb.ToString();
            }

            static string PathOf(Transform t)
            {
                string s = t.name;
                while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
                return s;
            }

            void Line(string s) { _log.AppendLine(s); Debug.Log("[ProbeB] " + s); }

            void Write()
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(Path.Combine(OutDir, $"game_polish_b_{_mode}.log"), _log.ToString());
                if (_mode != "modals") return;

                int fails = 0;
                foreach (ModalRecord r in _modals) fails += r.Fails.Count;

                var sb = new StringBuilder();
                sb.Append("{\n  \"task\": \"game_polish_b\",\n  \"gate\": \"D1/A1 modal pop\",\n");
                sb.Append($"  \"recordedUtc\": \"{DateTime.UtcNow:u}\",\n");
                sb.Append($"  \"modals\": {_modals.Count},\n  \"fail\": {fails},\n");
                sb.Append("  \"records\": [\n");
                for (int i = 0; i < _modals.Count; i++)
                {
                    ModalRecord r = _modals[i];
                    sb.Append("    { \"name\": \"").Append(r.Name).Append("\", \"path\": \"").Append(r.Path)
                      .Append("\", \"animateShow\": ").Append(r.AnimateShow ? "true" : "false")
                      .Append(", \"realWidget\": ").Append(r.RealWidget ? "true" : "false")
                      .Append(", \"reason\": \"").Append(r.Reason)
                      .Append("\", \"activated\": ").Append(r.Activated ? "true" : "false")
                      .Append(", \"showIsNoOp\": ").Append(r.ShowIsNoOp ? "true" : "false")
                      .Append(", \"visibleOnShowFrame\": ").Append(r.VisibleOnShowFrame ? "true" : "false")
                      .Append(", \"visibleOnHideFrame\": ").Append(r.VisibleOnHideFrame ? "true" : "false")
                      .Append(", \"openCountBefore\": ").Append(r.OpenCountBefore)
                      .Append(", \"openCountOnShow\": ").Append(r.OpenCountOnShow)
                      .Append(", \"openCountAfterHide\": ").Append(r.OpenCountAfterHide)
                      .Append(", \"midPopScale\": ").Append(r.MidPopScale.ToString("F4", CultureInfo.InvariantCulture))
                      .Append(", \"shot\": \"").Append(r.Shot.Replace("\\", "/"))
                      .Append("\", \"fails\": [");
                    for (int k = 0; k < r.Fails.Count; k++)
                    {
                        if (k > 0) sb.Append(", ");
                        sb.Append('"').Append(r.Fails[k].Replace("\"", "'")).Append('"');
                    }
                    sb.Append("] }").Append(i < _modals.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ]\n}\n");
                string p = Path.Combine(OutDir, "game_polish_b_modals.json");
                File.WriteAllText(p, sb.ToString());
                Line($"wrote {p} — {_modals.Count} modals, fail {fails}");
            }
        }
    }
}
