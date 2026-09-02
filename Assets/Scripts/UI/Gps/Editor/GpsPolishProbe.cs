// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §A1 / §A2 — the gate.
//
// THE JSON IS THE GATE; THE VIDEO IS THE ARTIFACT. A push is 15 frames long, and
// "it looked right" is not a measurement of a 15-frame event. This probe drives
// every pushable transition through the REAL widgets a player taps, samples the
// two content rects and the four chrome CanvasGroups on EVERY frame of every
// push, and writes a per-assertion PASS/FAIL file. fail == 0 is the gate.
//
// REAL NAVIGATION (PIPELINE_HARDENING rule 2). Boot -> tap the real StartButton
// -> Home -> the Home GPS pill -> the hub's own nav slots and the profile's own
// shortcut buttons. Nothing here calls ShowScreen to reach a screen a player
// reaches by tapping.
//
// THREE MODES, and the order they are run in is the argument:
//   baseline  motion OFF, so every navigation takes the untouched fade path and
//             every capture is the screen exactly as HEAD draws it. Run BEFORE
//             the prefab polish pass.
//   polished  same, after the polish pass. baseline vs polished proves the
//             CanvasGroups / safe-area wrapper / step marker moved no rest pixel.
//   push      motion ON. polished vs push is A2: a rest state reached through
//             the animation against the same rest state reached instantly.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.Gps.UI;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.EditorTools
{
    public static class GpsPolishProbe
    {
        const string ArmedKey = "gps_polish.probe.armed";
        const string ModeKey  = "gps_polish.probe.mode";

        const string TaskDir  = "Docs/Specs/Active/gps_polish";
        const string ShotDir  = TaskDir + "/screenshots";
        const string LogPath  = "Docs/Diagnostics/_capture/gps_polish_run.log";
        const string JsonPath = "Docs/Diagnostics/_capture/gps_polish_invariants.json";

        /// <summary>Frame budget for the measured-duration assertion (SPEC A1: "within ±2
        /// frames"). 2 frames at 60 fps, with a little slack for an Editor hitch.</summary>
        const float DurationToleranceSec = 2f / 60f + 0.02f;

        [MenuItem("GOLFIN/Gps/Polish Probe — baseline (motion off, pre-polish)", priority = 240)]
        public static void ArmBaseline() => Arm("baseline");

        [MenuItem("GOLFIN/Gps/Polish Probe — polished (motion off, post-polish)", priority = 241)]
        public static void ArmPolished() => Arm("polished");

        [MenuItem("GOLFIN/Gps/Polish Probe — push (motion on, writes invariants)", priority = 242)]
        public static void ArmPush() => Arm("push");

        public static void Arm(string mode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(ShotDir);
            File.WriteAllText(LogPath, "");
            EditorPrefs.SetString(ModeKey, mode);
            EditorPrefs.SetBool(ArmedKey, true);
            if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
            else Spawn();
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ArmedKey, false)) Spawn();
        };

        static void Spawn()
        {
            EditorPrefs.SetBool(ArmedKey, false);
            var go = new GameObject("__GpsPolishProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        // ═════════════════════════════════════════════════════════════════════

        /// <summary>One measured push, with every assertion the SPEC names.</summary>
        sealed class Record
        {
            public string From = "", To = "", Direction = "";
            public float  W, ExpectedDur, MeasuredDur;
            public float  TargetOffsetAtT0, EndTargetX, EndTargetRestX, EndLeaverX, EndLeaverRestX;
            public float  EndTargetChromeAlphaMin = 1f, EndLeaverChromeAlphaMin = 1f;
            public float  EndTargetContentAlpha = 1f, EndLeaverContentAlpha = 1f;
            public bool   EndBlocksRaycasts;
            public bool   Completed;
            public float  SeamWorstCover = 1f;   // min over frames of max(bgFrom, bgTo)
            public int    Frames;
            public readonly List<string> Fails = new List<string>();
        }

        public sealed class Driver : MonoBehaviour
        {
            readonly StringBuilder _log = new StringBuilder();
            readonly List<Record>  _records = new List<Record>();
            string _mode = "push";
            int _shot;

            void Start()
            {
                // Without this the Editor stops rendering the moment it loses focus and every
                // capture comes back as whatever it drew last — the splash, usually.
                Application.runInBackground = true;
                _mode = EditorPrefs.GetString(ModeKey, "push");
                StartCoroutine(Run());
            }

            IEnumerator Run()
            {
                Line("=== gps_polish probe (" + _mode + ") " + DateTime.UtcNow.ToString("u") + " ===");

                // Motion OFF for the two rest-capture modes: CanPush returns false, every
                // navigation falls through to the untouched fade, and what lands on screen is the
                // screen at rest with nothing this task added having moved.
                UiMotion.Enabled = _mode == "push";
                Line("UiMotion.Enabled = " + UiMotion.Enabled);

                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");
                yield return TapStart();
                yield return Until(() => ScreenManager.Instance!.CurrentScreen == ScreenId.Home, 90f, "Home");
                yield return new WaitForSecondsRealtime(2f);

                yield return TapNamed("GpsPill", "the Home GPS pill");
                yield return Arrive(ScreenId.GpsHub, 3f);
                yield return Shot("hub");

                GameObject? hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
                if (hub == null) { Line("FATAL: no hub in the scene"); yield break; }

                // ── (a) hub -> Profile -> Badges -> back -> back ─────────────
                yield return Go(hub, "NavProfileButton", ScreenId.GpsProfile, "hub nav PROFILE");
                yield return Shot("profile");

                GameObject? prof = GameObject.Find("Canvas/ScreensRoot/GpsProfileScreen");
                yield return GoPath(prof, "ContentContainer/BadgesShortcut", ScreenId.GpsBadges, "profile BADGES");
                yield return Shot("badges");
                yield return GoBackReal(ScreenId.GpsProfile, "badges back");

                yield return GoPath(prof, "ContentContainer/AvatarShortcut", ScreenId.GpsAvatar, "profile AVATAR");
                yield return Shot("avatar");
                yield return GoBackReal(ScreenId.GpsProfile, "avatar back");
                yield return GoBackReal(ScreenId.GpsHub, "profile back");

                // ── (b) the nav-bar sweep ────────────────────────────────────
                yield return Go(hub, "NavGiftButton", ScreenId.GpsGift, "hub nav GIFT");
                yield return Shot("gift");
                yield return GoBackReal(ScreenId.GpsHub, "gift back");

                yield return GoPath(hub, "ContentContainer/ActionTiles/Tile_VOTE", ScreenId.GpsVote, "hub tile VOTE");
                yield return Shot("vote");
                yield return GoBackReal(ScreenId.GpsHub, "vote back");

                // ── ScoreUpload: proves the FADE is still what it gets ───────
                yield return Go(hub, "NavCameraButton", ScreenId.ScoreUpload, "hub nav CAMERA");
                yield return Shot("scoreupload");
                yield return GoBackReal(ScreenId.GpsHub, "score upload back");

                if (_mode == "push") WriteJson();
                Line("=== done: " + _records.Count + " push(es) recorded ===");
            }

            // ═════════════════════════════════════════════════════════════════
            // Navigation + measurement
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Go(GameObject? root, string navChild, ScreenId target, string what)
            {
                Transform? nav = GpsScreenTransition.FindLayer(root, "GpsNavBar");
                Transform? t   = nav != null ? nav.Find(navChild) : null;
                yield return Tap(t, target, what);
            }

            IEnumerator GoPath(GameObject? root, string path, ScreenId target, string what)
            {
                Transform? t = root != null ? root.transform.Find(path) : null;
                yield return Tap(t, target, what);
            }

            /// <summary>
            /// BACK through a real widget. The Badges / Avatar / Gift / Vote screens carry no back
            /// button of their own — the affordance is the shared chrome — so the first ACTIVE
            /// button whose name contains "Back" is tapped and NAMED in the log, rather than
            /// calling GoBack() directly and pretending that was a tap.
            /// </summary>
            IEnumerator GoBackReal(ScreenId target, string what)
            {
                Button? found = null;
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                               FindObjectsSortMode.None))
                {
                    if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                    if (b.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    found = b; break;
                }
                // No back widget: use the NAV BAR slot for the destination, which is a real tap on
                // a real widget and — since gps_polish wired the bar on non-hub screens — the
                // player's actual way out of Badges / Avatar / Gift / Vote.
                if (found == null)
                {
                    GameObject? cur = Obj(ScreenManager.Instance!.CurrentScreen);
                    Transform? bar = GpsScreenTransition.FindLayer(cur, "GpsNavBar");
                    string slot = target == ScreenId.GpsHub     ? "NavHomeButton"
                                : target == ScreenId.GpsProfile ? "NavProfileButton"
                                : target == ScreenId.GpsGift    ? "NavGiftButton"
                                : "NavCameraButton";
                    Transform? st = bar != null ? bar.Find(slot) : null;
                    var sb2 = st != null ? st.GetComponent<Button>() : null;
                    if (sb2 != null && sb2.interactable)
                    {
                        yield return Tap(st, target, what + " via nav slot '" + slot + "'");
                        yield break;
                    }
                    Line("WARN: " + what + " — no Back widget and nav slot '" + slot +
                         "' is dead; using GoBack() (NOT a real tap)");
                    ScreenManager.Instance?.GoBack(target);
                    yield return Arrive(target, 2.5f);
                    yield break;
                }
                yield return Tap(found.transform, target, what + " via '" + found.name + "'");
            }

            IEnumerator Tap(Transform? t, ScreenId target, string what)
            {
                var b = t != null ? t.GetComponent<Button>() : null;
                if (b == null) { Line("WARN: no button for " + what); yield break; }

                ScreenId from = ScreenManager.Instance!.CurrentScreen;
                Line("tapping " + what + " (interactable=" + b.interactable + ") " + from + " -> " + target);

                bool expectPush = UiMotion.Enabled &&
                                  GpsScreenTransition.CanPush(from, target, Obj(from), Obj(target));

                b.onClick.Invoke();

                if (expectPush) yield return Measure(from, target);
                yield return Arrive(target, expectPush ? 1.5f : 3f);
            }

            /// <summary>
            /// Sample every frame of the push. This is the only place the invariants come from —
            /// no assertion below is read off a still.
            /// </summary>
            IEnumerator Measure(ScreenId from, ScreenId to)
            {
                GameObject? fromGo = Obj(from), toGo = Obj(to);
                var r = new Record
                {
                    From = from.ToString(), To = to.ToString(),
                    Direction = GpsScreenTransition.DirectionFor(from, to, push: true).ToString(),
                    ExpectedDur = UiMotion.PushDur,
                };

                RectTransform? toContent   = Rect(toGo,   "ContentContainer");
                RectTransform? fromContent = Rect(fromGo, "ContentContainer");
                CanvasGroup?   toBg        = Group(toGo,   "Background");
                CanvasGroup?   fromBg      = Group(fromGo, "Background");
                CanvasGroup?   toContentCg   = Group(toGo,   "ContentContainer");
                CanvasGroup?   fromContentCg = Group(fromGo, "ContentContainer");

                // Rest X and the t0 offset come from the TWEEN, which sampled them before it
                // moved anything. Reading them here would read the already-staged position and
                // call the off-screen start "rest" — the bug that made every assertion in the
                // first run fire.
                var canvas = toGo != null ? toGo.GetComponentInParent<Canvas>() : null;
                var crt = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
                r.W = crt != null ? crt.rect.width : 1170f;

                // Only the SEAM needs per-frame observation, and it is sign-free: one frame of
                // observer lag cannot manufacture a covered frame that did not happen.
                while (GpsScreenTransition.IsPushing)
                {
                    float cover = Mathf.Max(toBg != null ? toBg.alpha : 1f,
                                            fromBg != null ? fromBg.alpha : 1f);
                    if (cover < r.SeamWorstCover) r.SeamWorstCover = cover;
                    yield return null;
                }

                r.MeasuredDur      = GpsScreenTransition.LastPushElapsed;
                r.Frames           = GpsScreenTransition.LastPushFrames;
                r.Completed        = GpsScreenTransition.LastPushCompleted;
                r.TargetOffsetAtT0 = GpsScreenTransition.LastPushEnterOffset;
                r.EndTargetRestX   = GpsScreenTransition.LastPushTargetRestX;
                r.EndLeaverRestX   = GpsScreenTransition.LastPushLeaverRestX;

                // Rest, one frame after the swap settled.
                yield return null;
                if (toContent   != null) r.EndTargetX = toContent.anchoredPosition.x;
                if (fromContent != null) r.EndLeaverX = fromContent.anchoredPosition.x;
                r.EndTargetChromeAlphaMin = MinChromeAlpha(toGo);
                r.EndLeaverChromeAlphaMin = MinChromeAlpha(fromGo);
                r.EndTargetContentAlpha   = toContentCg   != null ? toContentCg.alpha   : 1f;
                r.EndLeaverContentAlpha   = fromContentCg != null ? fromContentCg.alpha : 1f;
                r.EndBlocksRaycasts = (toContentCg == null || toContentCg.blocksRaycasts)
                                   && (fromContentCg == null || fromContentCg.blocksRaycasts);

                Assert(r);
                _records.Add(r);
                Line("  push " + r.From + "->" + r.To + " dir=" + r.Direction +
                     " frames=" + r.Frames + " dur=" + r.MeasuredDur.ToString("0.000") +
                     " t0off=" + r.TargetOffsetAtT0.ToString("0.#") +
                     " seamCover=" + r.SeamWorstCover.ToString("0.000") +
                     " fails=" + r.Fails.Count);
            }

            static void Assert(Record r)
            {
                if (Mathf.Abs(r.MeasuredDur - r.ExpectedDur) > DurationToleranceSec)
                    r.Fails.Add($"duration {r.MeasuredDur:0.000}s is more than {DurationToleranceSec:0.000}s from {r.ExpectedDur:0.000}s");

                float want = r.Direction == "Forward" ? r.W : -r.W;
                if (Mathf.Abs(r.TargetOffsetAtT0 - want) > 1f)
                    r.Fails.Add($"target content at t0 was {r.TargetOffsetAtT0:0.#} from rest, expected {want:0.#}");

                if (Mathf.Abs(r.EndTargetX - r.EndTargetRestX) > 0.01f)
                    r.Fails.Add($"target content settled at x={r.EndTargetX:0.###}, rest is {r.EndTargetRestX:0.###}");
                if (Mathf.Abs(r.EndLeaverX - r.EndLeaverRestX) > 0.01f)
                    r.Fails.Add($"leaver content settled at x={r.EndLeaverX:0.###}, rest is {r.EndLeaverRestX:0.###}");

                if (r.EndTargetChromeAlphaMin < 0.999f)
                    r.Fails.Add($"target chrome settled at alpha {r.EndTargetChromeAlphaMin:0.###}");
                if (r.EndLeaverChromeAlphaMin < 0.999f)
                    r.Fails.Add($"leaver chrome settled at alpha {r.EndLeaverChromeAlphaMin:0.###}");
                if (r.EndTargetContentAlpha < 0.999f || r.EndLeaverContentAlpha < 0.999f)
                    r.Fails.Add($"content alpha settled at {r.EndTargetContentAlpha:0.###}/{r.EndLeaverContentAlpha:0.###}");

                if (!r.EndBlocksRaycasts)
                    r.Fails.Add("blocksRaycasts was not restored");

                // THE SEAM TEST. Every frame must have at least one background at full-ish
                // opacity, or the composite shows through to whatever is behind the canvas.
                if (r.SeamWorstCover < 0.5f)
                    r.Fails.Add($"background seam: worst frame covered only {r.SeamWorstCover:0.###}");

                if (r.Frames < 2)
                    r.Fails.Add($"only {r.Frames} frame(s) sampled — the push did not animate");
                if (!r.Completed)
                    r.Fails.Add("push did not run to completion (it was snapped by another navigation)");
            }

            void WriteJson()
            {
                int fails = 0;
                foreach (var r in _records) fails += r.Fails.Count;

                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"task\": \"gps_polish\",");
                sb.AppendLine("  \"generated\": \"" + DateTime.UtcNow.ToString("u") + "\",");
                sb.AppendLine("  \"pushDurSec\": " + F(UiMotion.PushDur) + ",");
                sb.AppendLine("  \"fadeDurSec\": " + F(UiMotion.FadeDur) + ",");
                sb.AppendLine("  \"durationToleranceSec\": " + F(DurationToleranceSec) + ",");
                sb.AppendLine("  \"transitions\": " + _records.Count + ",");
                sb.AppendLine("  \"fail\": " + fails + ",");
                sb.AppendLine("  \"records\": [");
                for (int i = 0; i < _records.Count; i++)
                {
                    Record r = _records[i];
                    sb.AppendLine("    {");
                    sb.AppendLine("      \"from\": \"" + r.From + "\", \"to\": \"" + r.To + "\", \"direction\": \"" + r.Direction + "\",");
                    sb.AppendLine("      \"W\": " + F(r.W) + ", \"frames\": " + r.Frames + ",");
                    sb.AppendLine("      \"measuredDurSec\": " + F(r.MeasuredDur) + ", \"expectedDurSec\": " + F(r.ExpectedDur) + ",");
                    sb.AppendLine("      \"targetOffsetAtT0\": " + F(r.TargetOffsetAtT0) + ",");
                    sb.AppendLine("      \"endTargetX\": " + F(r.EndTargetX) + ", \"endTargetRestX\": " + F(r.EndTargetRestX) + ",");
                    sb.AppendLine("      \"endLeaverX\": " + F(r.EndLeaverX) + ", \"endLeaverRestX\": " + F(r.EndLeaverRestX) + ",");
                    sb.AppendLine("      \"endTargetChromeAlphaMin\": " + F(r.EndTargetChromeAlphaMin) + ",");
                    sb.AppendLine("      \"endLeaverChromeAlphaMin\": " + F(r.EndLeaverChromeAlphaMin) + ",");
                    sb.AppendLine("      \"endTargetContentAlpha\": " + F(r.EndTargetContentAlpha) + ",");
                    sb.AppendLine("      \"endLeaverContentAlpha\": " + F(r.EndLeaverContentAlpha) + ",");
                    sb.AppendLine("      \"blocksRaycastsRestored\": " + (r.EndBlocksRaycasts ? "true" : "false") + ",");
                    sb.AppendLine("      \"ranToCompletion\": " + (r.Completed ? "true" : "false") + ",");
                    sb.AppendLine("      \"seamWorstCover\": " + F(r.SeamWorstCover) + ",");
                    sb.Append    ("      \"fails\": [");
                    for (int k = 0; k < r.Fails.Count; k++)
                        sb.Append((k > 0 ? ", " : "") + "\"" + r.Fails[k].Replace("\"", "'") + "\"");
                    sb.AppendLine("]");
                    sb.AppendLine("    }" + (i < _records.Count - 1 ? "," : ""));
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                Directory.CreateDirectory(Path.GetDirectoryName(JsonPath)!);
                File.WriteAllText(JsonPath, sb.ToString());
                File.WriteAllText(Path.Combine(TaskDir, "gps_polish_invariants.json"), sb.ToString());
                Line("invariants -> " + JsonPath + "  fail=" + fails);
            }

            static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

            // ═════════════════════════════════════════════════════════════════
            // Scene helpers
            // ═════════════════════════════════════════════════════════════════

            static GameObject? Obj(ScreenId id)
            {
                string name;
                switch (id)
                {
                    case ScreenId.GpsHub:         name = "GpsHubScreen"; break;
                    case ScreenId.ScoreUpload:    name = "ScoreUploadScreen"; break;
                    case ScreenId.GpsProfile:     name = "GpsProfileScreen"; break;
                    case ScreenId.GpsAvatar:      name = "GpsAvatarScreen"; break;
                    case ScreenId.GpsBadges:      name = "GpsBadgesScreen"; break;
                    case ScreenId.GpsGolfProfile: name = "GpsGolfProfileScreen"; break;
                    case ScreenId.GpsWelcome:     name = "GpsWelcomeScreen"; break;
                    case ScreenId.GpsGift:        name = "GpsGiftScreen"; break;
                    case ScreenId.GpsVote:        name = "GpsVoteScreen"; break;
                    default: return null;
                }
                // Inactive screens are not reachable with GameObject.Find, and the target of a
                // push is inactive at the moment it is looked up.
                Transform? root = GameObject.Find("Canvas/ScreensRoot")?.transform;
                Transform? t = root != null ? root.Find(name) : null;
                return t != null ? t.gameObject : null;
            }

            static RectTransform? Rect(GameObject? go, string child)
                => GpsScreenTransition.FindLayer(go, child) as RectTransform;

            static CanvasGroup? Group(GameObject? go, string child)
            {
                Transform? t = GpsScreenTransition.FindLayer(go, child);
                return t != null ? t.GetComponent<CanvasGroup>() : null;
            }

            static float MinChromeAlpha(GameObject? go)
            {
                float min = 1f;
                foreach (string n in new[] { "Background", "GpsNavBar", "BackPill" })
                {
                    CanvasGroup? cg = Group(go, n);
                    if (cg != null && cg.alpha < min) min = cg.alpha;
                }
                return min;
            }

            // ═════════════════════════════════════════════════════════════════
            // Plumbing
            // ═════════════════════════════════════════════════════════════════

            IEnumerator Arrive(ScreenId id, float settle)
            {
                yield return Until(() => ScreenManager.Instance!.CurrentScreen == id, 30f, id.ToString());
                yield return new WaitForSecondsRealtime(settle);
            }

            IEnumerator Shot(string label)
            {
                _shot++;
                string name = string.Format("{0}_{1:00}_{2}", _mode, _shot, label);
                string path = Path.Combine(ShotDir, name + ".png");
                Directory.CreateDirectory(ShotDir);

                IEnumerator snap = CaptureCore.SnapAtEndOfFrameAndPause(name, path, skipPause: true);
                while (snap.MoveNext()) yield return snap.Current;

                // Assert the FILE, never the return value — SnapPlayModeSafe has logged a path for
                // a file it never wrote (memory: reference_snapplaymodesafe_phantom_path).
                Line("SHOT " + label + " -> " + (File.Exists(path)
                    ? path + " (" + new FileInfo(path).Length / 1024 + " KB)"
                    : "MISSING (" + path + ")"));
            }

            IEnumerator TapStart()
            {
                float deadline = Time.realtimeSinceStartup + 90f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
                    {
                        if (b.name != "StartButton" || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping the real " + b.name);
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(2f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: no StartButton appeared in 90 s");
            }

            IEnumerator TapNamed(string name, string what)
            {
                float deadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
                    {
                        if (b.name != name || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping " + what + " (" + b.name + ", interactable=" + b.interactable + ")");
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: " + what + " ('" + name + "') never appeared");
            }

            IEnumerator Until(Func<bool> done, float seconds, string what)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
                Line((done() ? "ok   " : "TIMEOUT ") + what);
            }

            void Line(string s)
            {
                _log.AppendLine(s);
                Debug.Log("[POLISH-PROBE] " + s);
                File.WriteAllText(LogPath, _log.ToString());
            }
        }
    }
}
