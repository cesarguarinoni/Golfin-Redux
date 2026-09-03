#if UNITY_EDITOR
// ─────────────────────────────────────────────────────────────────────────────
// The whole GPS surface, in one take, for the report.
//
// Seven screens and three modals, walked the way a player walks them: Home → the
// GPS pill → Hub → Profile → My Avatar → Badges → Gift (+ send and buy modals) →
// Vote (+ MINE, + create modal) → back to the Hub. Every step is a REAL widget's
// onClick, so the clip is evidence and not a slideshow — if a hub affordance is
// dead, the recording stalls on it rather than cutting past it.
//
// Same construction as RankingsDemoRecorder / TournamentDemoRecorder: Unity
// Recorder over the GAME VIEW at the full 1170x2532. GameView, not a camera —
// under URP a camera source drops the Overlay HUD, which here is the whole top
// bar and the GPS nav bar.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    public static class GpsFlowDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Reports/Media/gps_flow";
        const string ArmedKey       = "GpsFlowDemoRecorder.Armed";

        /// <summary>Which clip this run records. Survives the domain reload that entering play
        /// mode causes, which SessionState alone would not on every Unity version.</summary>
        const string ScenarioKey    = "GpsFlowDemoRecorder.Scenario";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Gps/Record — (a+d) whole GPS surface", priority = 230)]
        public static void LaunchA() => LaunchDemo("a");

        /// <summary>(b) — the nav-bar sweep, re-recorded for the continuation: it now shows the
        /// cold-fetch shimmer and the staggered first paint on every screen it visits.</summary>
        [MenuItem("GOLFIN/Gps/Record — (b) nav sweep, cold", priority = 231)]
        public static void LaunchB() => LaunchDemo("b");

        [MenuItem("GOLFIN/Gps/Record — (c) Score Upload step walk", priority = 232)]
        public static void LaunchC() => LaunchDemo("c");

        [MenuItem("GOLFIN/Gps/Record — (d2) gift + vote panel fades", priority = 233)]
        public static void LaunchD2() => LaunchDemo("d2");

        [MenuItem("GOLFIN/Gps/Record — (e) Golf Profile to Welcome to hub", priority = 234)]
        public static void LaunchE() => LaunchDemo("e");

        [MenuItem("GOLFIN/Gps/Record — (f) a live cast", priority = 235)]
        public static void LaunchF() => LaunchDemo("f");

        /// <summary>(g) — gps_checkin: the whole ROUNDS loop, driven through the real widgets.
        /// Location is mocked at TEST Office so the check-in lands inside the radius.</summary>
        [MenuItem("GOLFIN/Gps/Record — (g) Rounds check-in loop", priority = 236)]
        public static void LaunchG() => LaunchDemo("g");

        public static void LaunchDemo(string scenario = "a", bool dryRun = false)
        {
            EditorPrefs.SetString(ScenarioKey, scenario);
            // Written explicitly on EVERY launch — a normal Record can never inherit a stale
            // dry-run flag from an earlier session, and the dry run cannot be cleared by its own
            // launcher (which the first version of this did).
            EditorPrefs.SetBool(DryRunKey, dryRun);
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GpsFlowDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[GpsFlowDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
            }
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = System.Reflection.Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected",
                              System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        /// <summary>Set by the DRY-RUN menu item: walk the scenario with the Recorder OFF.
        /// The point is to separate "the scenario is bad" from "the encoder is bad" — the flow
        /// runs identically, nothing is encoded, and nothing touches VideoToolbox.</summary>
        const string DryRunKey = "GpsFlowDemoRecorder.DryRun";

        [MenuItem("GOLFIN/Gps/Record — DRY RUN (g), no encoder", priority = 237)]
        public static void LaunchGDry() => LaunchDemo("g", dryRun: true);

        static void StartRecorderAndBot()
        {
            if (EditorPrefs.GetBool(DryRunKey, false))
            {
                Debug.Log("[GpsFlowDemo] DRY RUN — Recorder NOT started; no MP4, no VideoToolbox.");
                GpsFlowDemoRunner.RecordStart = Time.realtimeSinceStartup;
                // IDENTICAL to the real path below, minus the Recorder. StartDemo() is what
                // actually runs the scenario — the first version of this only added the component
                // and never called it, so the "dry run" sat on Home and proved nothing.
                var dryHost = new GameObject("[GpsFlowDemoBot]");
                UnityEngine.Object.DontDestroyOnLoad(dryHost);
                dryHost.AddComponent<GpsFlowDemoRunner>().StartDemo();
                return;
            }

            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[GpsFlowDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "GpsFlowDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/raw_{EditorPrefs.GetString(ScenarioKey, "a")}";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            GpsFlowDemoRunner.RecordStart = Time.realtimeSinceStartup;
            Debug.Log($"[GpsFlowDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[GpsFlowDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GpsFlowDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[GpsFlowDemo] Recording stopped."); }
                catch (Exception e) { Debug.LogWarning($"[GpsFlowDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class GpsFlowDemoRunner : MonoBehaviour
    {
        /// <summary>Set by <see cref="OnEarnLog"/> when /points/earn actually credits (f).</summary>
        bool _earned;

        void OnEarnLog(string message, string stack, LogType type)
        {
            // "[GpsVote] vote_cast earn -> +10 (total 6968)" — a ZERO award is the router refusing
            // (daily cap, unknown action), so only a positive one counts as a real cast.
            if (message == null || !message.Contains("vote_cast earn -> +")) return;
            int i = message.IndexOf("-> +", System.StringComparison.Ordinal) + 4;
            int j = i;
            while (j < message.Length && char.IsDigit(message[j])) j++;
            if (j > i && int.TryParse(message.Substring(i, j - i), out int awarded) && awarded > 0)
                _earned = true;
        }

        /// <summary>Bot-clock time at StartRecording(), so every caption below is stamped in
        /// seconds since the first frame — which is exactly what build_bot_video.py's
        /// `--mode captionsjson` expects, and why this clip needs no hand-timed caption list
        /// that would drift the moment a hold changes.</summary>
        public static float RecordStart;

        readonly List<(float start, string text)> _caps = new List<(float, string)>();

        /// <summary>Open a caption. The previous one ends where this begins.</summary>
        void Cap(string text) => _caps.Add((Time.realtimeSinceStartup - RecordStart, text));

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
            float end = Time.realtimeSinceStartup - RecordStart;
            for (int i = 0; i < _caps.Count; i++)
            {
                float s = _caps[i].start;
                float e = (i + 1 < _caps.Count) ? _caps[i + 1].start : end;
                sb.Append("    {\"start\": ").Append(s.ToString("F2"))
                  .Append(", \"end\": ").Append(e.ToString("F2"))
                  .Append(", \"text\": \"").Append(_caps[i].text.Replace("\"", "\\\""))
                  .Append("\"}").Append(i + 1 < _caps.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory("Docs/Reports/Media/gps_flow");
            File.WriteAllText("Docs/Reports/Media/gps_flow/captions.json", sb.ToString());
            Debug.Log("[GpsFlowDemo] wrote " + _caps.Count + " captions.");
        }

        /// <summary>How long a screen holds before the next tap. Long enough that a viewer can
        /// read a panel, and long enough for the live requests behind it to land.</summary>
        const float Hold = 3.2f;
        const float Settle = 1.0f;

        public void StartDemo()
        {
            // Without this the Editor stops rendering the moment it loses focus and the recording
            // becomes a still of whatever it drew last.
            Application.runInBackground = true;
            string scenario = EditorPrefs.GetString("GpsFlowDemoRecorder.Scenario", "a");
            Debug.Log("[GpsFlowDemo] scenario = " + scenario);
            switch (scenario)
            {
                case "b":  StartCoroutine(SequenceB());  break;
                case "c":  StartCoroutine(SequenceC());  break;
                case "d2": StartCoroutine(SequenceD2()); break;
                case "e":  StartCoroutine(SequenceE());  break;
                case "f":  StartCoroutine(SequenceF());  break;
                case "g":  StartCoroutine(SequenceG());  break;
                default:   StartCoroutine(Sequence());   break;
            }
        }

        /// <summary>
        /// Boot through the title gate and land on Home. Shared by every scenario — the app boots
        /// to a Start screen ScreenManager does NOT manage, so a ShowScreen before this leaves the
        /// frame on the title (CLAUDE.md Capture rule 0).
        /// </summary>
        IEnumerator Boot()
        {
            yield return Until(() => ScreenManager.Instance != null, 30f);
            yield return TapAnywhere("StartButton", 90f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.Home, 90f);
            yield return new WaitForSecondsRealtime(2f);
        }

        void Finish()
        {
            WriteCaptions();
            Debug.Log("[GpsFlowDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        // ═════════════════════════════════════════════════════════════════════
        // ═════════════════════════════════════════════════════════════════════
        // (g) — gps_checkin: the ROUNDS loop end to end
        //
        // Every step is a real widget tap on the real screen, in the order a player
        // performs them: hub -> ROUNDS -> CHECK IN -> confirm -> the live card ->
        // CHECK OUT -> confirm -> the receipt. Location is mocked at TEST Office
        // (venue 1993) so the check-in is genuinely inside the radius and the
        // server pays it — nothing here is a staged frame.
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator SequenceG()
        {
            yield return Boot();

            Golfin.Gps.UI.GpsRoundsScreenController.EditorFixOverride =
                new Golfin.Gps.LocationFix { Lat = 35.654103, Lon = 139.779219, AccuracyM = 8f };
            Cap("Rounds \u2014 the check-in loop, end to end");

            yield return TapAnywhere("GpsPill", 20f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            yield return new WaitForSecondsRealtime(Hold);

            GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
            Cap("Nav slot \u2014 ROUNDS");
            yield return TapIn(hub, "NavSafeArea/GpsNavBar/NavRoundsButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsRounds, 20f);
            Cap("Nearby spots, live map \u2014 CHECK IN inside the radius, distance outside it");
            yield return new WaitForSecondsRealtime(Hold + 2.5f);

            Cap("CHECK IN");
            yield return TapAnywhere("ActionButton", 15f);
            yield return new WaitForSecondsRealtime(1.2f);
            Cap("Confirm \u2014 points on check-in and check-out, live GPS accuracy");
            yield return new WaitForSecondsRealtime(Hold);

            yield return TapAnywhere("ConfirmButton", 15f);
            yield return new WaitForSecondsRealtime(2.5f);
            Cap("Live round \u2014 elapsed, points earned, GPS fixes. The list turns to FOOD & DRINK");
            yield return new WaitForSecondsRealtime(Hold + 2f);

            Cap("CHECK OUT");
            yield return TapAnywhere("CheckOutButton", 15f);
            yield return new WaitForSecondsRealtime(1.2f);
            Cap("Confirm the check-out");
            yield return new WaitForSecondsRealtime(Hold);

            yield return TapAnywhere("PrimaryButton", 15f);
            yield return new WaitForSecondsRealtime(2.5f);
            Cap("Receipt \u2014 the server's own elapsed, points and GPS verdict");
            yield return new WaitForSecondsRealtime(Hold + 2f);

            yield return TapAnywhere("SecondaryButton", 15f);   // DONE
            yield return new WaitForSecondsRealtime(2f);
            Cap("Back to the list \u2014 the round is closed and the chip agrees with the rows");
            yield return new WaitForSecondsRealtime(Hold);
        }

        // (b) — the nav-bar sweep, COLD
        //
        // Re-recorded for the continuation. Nothing about the route changed; what
        // changed is what it shows. Every service cache is a per-play-session
        // singleton, so a fresh play mode starts with all five paint caches EMPTY
        // and the first open of each screen is a genuine cold fetch: shimmer up,
        // then the rows stagger in behind it. The clip then RE-VISITS the hub, where
        // the same panels repaint from cache instantly — that contrast is the whole
        // point, and the log carries the paint(cache)/paint(fetch) line for each.
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator SequenceB()
        {
            yield return Boot();
            Cap("Fresh session \u2014 every paint cache is empty");

            yield return TapAnywhere("GpsPill", 20f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            Cap("Hub \u2014 cold: rounds shimmer, then stagger in");
            yield return new WaitForSecondsRealtime(Hold + 1.5f);

            GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");

            Cap("Nav slot 1 \u2014 SCORE UPLOAD");
            yield return TapIn(hub, "NavSafeArea/GpsNavBar/NavCameraButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.ScoreUpload, 20f);
            yield return new WaitForSecondsRealtime(Hold);

            GameObject su = GameObject.Find("Canvas/ScreensRoot/ScoreUploadScreen");
            Cap("Nav slot 2 \u2014 GIFT, cold: supporters + golfers shimmer");
            yield return TapIn(su, "NavSafeArea/GpsNavBar/NavGiftButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsGift, 20f);
            yield return new WaitForSecondsRealtime(Hold + 2f);

            GameObject gift = GameObject.Find("Canvas/ScreensRoot/GpsGiftScreen");
            Cap("Nav slot 3 \u2014 VOTE, cold: two card placeholders");
            yield return TapIn(gift, "NavSafeArea/GpsNavBar/NavVoteButton");
            if (ScreenManager.Instance.CurrentScreen != ScreenId.GpsVote)
                Show(ScreenId.GpsVote);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsVote, 20f);
            yield return new WaitForSecondsRealtime(Hold + 2f);

            GameObject vote = GameObject.Find("Canvas/ScreensRoot/GpsVoteScreen");
            Cap("Nav slot 4 \u2014 PROFILE");
            yield return TapIn(vote, "NavSafeArea/GpsNavBar/NavProfileButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsProfile, 20f);
            yield return new WaitForSecondsRealtime(Hold + 1f);

            GameObject profile = GameObject.Find("Canvas/ScreensRoot/GpsProfileScreen");
            Cap("Badges \u2014 cold: six cells shimmer, then stagger");
            yield return TapFirstIn(profile, "BadgesShortcut");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsBadges, 20f);
            yield return new WaitForSecondsRealtime(Hold + 2f);

            GameObject badges = GameObject.Find("Canvas/ScreensRoot/GpsBadgesScreen");
            Cap("Back to the hub \u2014 WARM: paints from cache, instantly");
            yield return TapIn(badges, "NavSafeArea/GpsNavBar/NavHomeButton");
            if (ScreenManager.Instance.CurrentScreen != ScreenId.GpsHub) Show(ScreenId.GpsHub);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            yield return new WaitForSecondsRealtime(Hold);

            Cap("Gift again \u2014 WARM: no shimmer, no stagger");
            yield return TapIn(hub, "NavSafeArea/GpsNavBar/NavGiftButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsGift, 20f);
            yield return new WaitForSecondsRealtime(Hold + 1f);

            Finish();
        }

        // ═════════════════════════════════════════════════════════════════════
        // (c) — the Score Upload step walk
        //
        // Every advance is a REAL widget onClick, including the 36 stepper taps that
        // build a legal 9-hole card: MANUAL ENTRY -> 9 HOLES -> nine rows of +4 ->
        // VERIFY GPS -> CONFIRM COURSE -> POST SCORE -> Posted. What the clip is
        // evidence for is D4: the step roots CROSS-FADE and the strip's indicator
        // SLIDES between segments instead of jumping, and the Posted total POPS.
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator SequenceC()
        {
            yield return Boot();
            yield return TapAnywhere("GpsPill", 20f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            yield return new WaitForSecondsRealtime(2f);

            GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
            Cap("Step 1 \u2014 CAPTURE");
            yield return TapIn(hub, "NavSafeArea/GpsNavBar/NavCameraButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.ScoreUpload, 20f);
            yield return new WaitForSecondsRealtime(Hold);

            GameObject su = GameObject.Find("Canvas/ScreensRoot/ScoreUploadScreen");

            Cap("MANUAL ENTRY \u2192 step 3, EDIT (the strip indicator slides)");
            yield return TapFirstIn(su, "SourceMANUAL");
            yield return new WaitForSecondsRealtime(Hold);

            Cap("9 HOLES, then nine rows of +4 \u2014 a legal 36");
            yield return TapFirstIn(su, "Seg9");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return StepHoles(su, holes: 9, strokes: 4);
            yield return new WaitForSecondsRealtime(1.2f);

            Cap("VERIFY GPS \u2192 step 4");
            yield return TapFirstIn(su, "VerifyGpsButton");
            yield return new WaitForSecondsRealtime(Hold + 1.5f);

            // THE EDITOR HAS NO GPS. Step 4 comes up "Could not get your location / No golf course
            // nearby" and CONFIRM THIS COURSE is correctly disabled — so the walk takes the same
            // door a player without a fix takes: CHOOSE MANUALLY, pick a venue, then confirm.
            // (The first take pressed CONFIRM anyway, which is exactly the dishonesty `Press`
            // now refuses.)
            if (!Interactable(su, "ConfirmCourseButton"))
            {
                Cap("No GPS fix in the Editor \u2014 CHOOSE MANUALLY");
                yield return TapFirstIn(su, "ChooseManuallyButton");
                yield return new WaitForSecondsRealtime(2f);
                yield return TapFirstVenue(su);
                yield return new WaitForSecondsRealtime(1.5f);
            }

            Cap("CONFIRM COURSE \u2192 step 5");
            yield return TapFirstIn(su, "ConfirmCourseButton");
            yield return new WaitForSecondsRealtime(Hold);

            Cap("POST SCORE \u2014 the CTA draws the wait");
            yield return TapFirstIn(su, "PostScoreButton");
            yield return new WaitForSecondsRealtime(Hold + 2.5f);

            Cap("POSTED \u2014 the server's numbers, and the total pops in");
            yield return new WaitForSecondsRealtime(Hold + 1f);

            Finish();
        }

        /// <summary>Is a named button on screen AND pressable? The venue detour keys off this.</summary>
        static bool Interactable(GameObject root, string name)
        {
            if (root == null) return false;
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
                if (b.gameObject.name == name)
                    return b.gameObject.activeInHierarchy && b.interactable;
            return false;
        }

        /// <summary>Tap the first REAL venue row in the picker — never the authored template,
        /// which is inactive and carries no venue.</summary>
        IEnumerator TapFirstVenue(GameObject su)
        {
            Transform rows = su != null
                ? su.transform.Find("VenuePickerModal/ModalPanel/List/Rows") : null;
            if (rows == null) { Debug.LogWarning("[GpsFlowDemo] no venue rows"); yield break; }

            foreach (Transform child in rows)
            {
                if (child.name.Contains("Template")) continue;
                var b = child.GetComponent<Button>();
                if (b == null || !child.gameObject.activeInHierarchy) continue;
                Press(b, "venue " + child.name);
                yield return null;
                yield break;
            }
            Debug.LogWarning("[GpsFlowDemo] the venue picker offered no row to tap.");
        }

        /// <summary>Tap every visible hole row's + stepper <paramref name="strokes"/> times. The
        /// REAL stepper, on the real row — the totals gate on the draft these write into.</summary>
        IEnumerator StepHoles(GameObject su, int holes, int strokes)
        {
            var rows = su.GetComponentsInChildren<Golfin.Gps.UI.HoleRowView>(true);
            int done = 0;
            foreach (var row in rows)
            {
                if (done >= holes) break;
                if (!row.gameObject.activeInHierarchy) continue;
                foreach (Button b in row.GetComponentsInChildren<Button>(true))
                {
                    if (b.name != "StepperPlus") continue;
                    for (int i = 0; i < strokes; i++) { Press(b, "StepperPlus"); yield return null; }
                    break;
                }
                done++;
                yield return new WaitForSecondsRealtime(0.10f);
            }
            Debug.Log($"[GpsFlowDemo] stepped {done} holes by {strokes}.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // (d2) — the panel fades and the filter cross-fade
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator SequenceD2()
        {
            yield return Boot();
            yield return TapAnywhere("GpsPill", 20f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            yield return new WaitForSecondsRealtime(1.5f);

            GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
            Cap("GIFT, cold \u2014 the three panels fade in with their placeholders");
            yield return TapIn(hub, "NavSafeArea/GpsNavBar/NavGiftButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsGift, 20f);
            yield return new WaitForSecondsRealtime(Hold + 2.5f);

            GameObject gift = GameObject.Find("Canvas/ScreensRoot/GpsGiftScreen");
            Cap("SEND GIFT \u2014 the amount pills bump and cross-fade");
            yield return TapIn(gift, "ContentContainer/Golfers/Golfer0/SendGiftButton");
            yield return new WaitForSecondsRealtime(1.6f);
            yield return TapAmounts(gift);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return TapIn(gift, "GiftSendModal/ModalPanel/CancelButtonRow/CancelButton");
            yield return new WaitForSecondsRealtime(Settle);

            Cap("VOTE \u2014 PUBLIC / MINE cross-fades the list");
            yield return TapIn(gift, "NavSafeArea/GpsNavBar/NavVoteButton");
            if (ScreenManager.Instance.CurrentScreen != ScreenId.GpsVote) Show(ScreenId.GpsVote);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsVote, 20f);
            yield return new WaitForSecondsRealtime(Hold + 2f);

            GameObject vote = GameObject.Find("Canvas/ScreensRoot/GpsVoteScreen");
            for (int i = 0; i < 2; i++)
            {
                yield return TapIn(vote, "ContentContainer/ChipsRow/Chip3");   // MINE
                yield return new WaitForSecondsRealtime(2.0f);
                yield return TapIn(vote, "ContentContainer/ChipsRow/Chip2");   // PUBLIC
                yield return new WaitForSecondsRealtime(2.0f);
            }

            Finish();
        }

        IEnumerator TapAmounts(GameObject gift)
        {
            Transform modal = gift.transform.Find("GiftSendModal");
            if (modal == null) yield break;
            foreach (Button b in modal.GetComponentsInChildren<Button>(true))
            {
                if (!b.name.StartsWith("Amount")) continue;
                Press(b, b.name);
                yield return new WaitForSecondsRealtime(0.55f);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // (e) — Golf Profile -> Welcome -> hub
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator SequenceE()
        {
            yield return Boot();
            Cap("Golf Profile \u2014 the post-signup capture");
            Show(ScreenId.GpsGolfProfile);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsGolfProfile, 20f);
            yield return new WaitForSecondsRealtime(Hold);

            GameObject gp = GameObject.Find("Canvas/ScreensRoot/GpsGolfProfileScreen");

            Cap("Avatar colour \u2014 the disc bumps and the sprites cross-fade");
            yield return TapSwatches(gp);
            yield return new WaitForSecondsRealtime(1.0f);

            Cap("Experience chips \u2014 the same treatment, no tinting");
            yield return TapChips(gp);
            yield return new WaitForSecondsRealtime(1.2f);

            Cap("Skip for now \u2192 Welcome");
            yield return TapFirstIn(gp, "SkipRow");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsWelcome, 20f);
            yield return new WaitForSecondsRealtime(Hold + 1f);

            GameObject wel = GameObject.Find("Canvas/ScreensRoot/GpsWelcomeScreen");
            Cap("GET STARTED \u2192 the hub");
            yield return TapFirstIn(wel, "GetStartedButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            yield return new WaitForSecondsRealtime(Hold);

            Finish();
        }

        IEnumerator TapSwatches(GameObject gp)
        {
            foreach (Button b in gp.GetComponentsInChildren<Button>(true))
            {
                if (!b.name.StartsWith("Colour")) continue;
                Press(b, b.name);
                yield return new WaitForSecondsRealtime(0.7f);
            }
        }

        IEnumerator TapChips(GameObject gp)
        {
            foreach (Button b in gp.GetComponentsInChildren<Button>(true))
            {
                if (!b.name.StartsWith("Chip") && !b.name.StartsWith("Exp")) continue;
                Press(b, b.name);
                yield return new WaitForSecondsRealtime(0.7f);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // (f) — a live cast
        //
        // Burns ONE of the four seeded GOLFIN AI votes on prod, which Cesar
        // approved. The cast is a real onClick on a real card's VOTE button; the
        // controller logs the vote id it casts on, which is what the report names.
        // ═════════════════════════════════════════════════════════════════════

        IEnumerator SequenceF()
        {
            yield return Boot();
            yield return TapAnywhere("GpsPill", 20f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            yield return new WaitForSecondsRealtime(1.5f);

            GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
            yield return TapIn(hub, "ContentContainer/ActionTiles/Tile_VOTE");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsVote, 20f);
            Cap("The live feed \u2014 five votes from /vote/list");
            yield return new WaitForSecondsRealtime(Hold + 2.5f);

            GameObject vote = GameObject.Find("Canvas/ScreensRoot/GpsVoteScreen");

            Cap("The RP in the top bar, before");
            yield return new WaitForSecondsRealtime(1.6f);

            Cap("VOTE \u2014 the button draws the wait");

            // WALK THE CARDS UNTIL ONE ACTUALLY EARNS, and this is not belt-and-braces: a card's
            // VOTE button is enabled from `VotedLocally`, which is per-SESSION memory. A vote this
            // account cast in an EARLIER session therefore looks castable, the server answers "you
            // already voted", and nothing moves — which is exactly what the first take of this
            // clip recorded, under a caption promising a bar fill and an RP count-up. So the cast
            // is confirmed by the EARN, not by the tap.
            _earned = false;
            Application.logMessageReceived += OnEarnLog;
            try
            {
                foreach (var card in vote.GetComponentsInChildren<Golfin.Gps.UI.VoteCardView>(true))
                {
                    if (card == null || !card.gameObject.activeInHierarchy) continue;
                    Button b = card.VoteButton;
                    if (b == null || !b.interactable) continue;

                    Debug.Log("[GpsFlowDemo] trying card '" + card.name + "' vote id=" +
                              (card.Vote != null ? card.Vote.Id : "?") + " question=\"" +
                              (card.Vote != null ? card.Vote.Question : "?") + "\"");
                    if (!Press(b, "VOTE on " + card.name)) continue;

                    float deadline = Time.realtimeSinceStartup + 6f;
                    while (!_earned && Time.realtimeSinceStartup < deadline) yield return null;
                    if (_earned)
                    {
                        Debug.Log("[GpsFlowDemo] CAST LANDED on vote id=" +
                                  (card.Vote != null ? card.Vote.Id : "?"));
                        break;
                    }
                    Debug.LogWarning("[GpsFlowDemo] '" + card.name + "' did not earn " +
                                     "(already voted in an earlier session) — trying the next card.");
                    yield return new WaitForSecondsRealtime(0.6f);
                }
            }
            finally { Application.logMessageReceived -= OnEarnLog; }

            if (!_earned)
                Debug.LogWarning("[GpsFlowDemo] no uncast vote left on this account — " +
                                 "the clip shows no earn.");

            Cap(_earned
                ? "Bars animate old \u2192 new; the top-bar RP counts up"
                : "Every vote on this account was already cast \u2014 no earn to show");
            yield return new WaitForSecondsRealtime(Hold + 4f);

            Cap("Settled");
            yield return new WaitForSecondsRealtime(2f);

            Finish();
        }

        IEnumerator Sequence()
        {
            // ── boot ─────────────────────────────────────────────────────────
            yield return Until(() => ScreenManager.Instance != null, 30f);
            yield return TapAnywhere("StartButton", 90f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.Home, 90f);
            Cap("Boot \u2192 Home");
            yield return new WaitForSecondsRealtime(2.5f);

            // ── Home → the GPS pill → Hub ────────────────────────────────────
            yield return TapAnywhere("GpsPill", 20f);
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f);
            // Stamped BEFORE the hold: a Cap() placed after it would open the caption at the
            // moment the NEXT tap happens and show for a fraction of a second.
            Cap("GPS Hub \u2014 live hero + recent rounds");
            yield return new WaitForSecondsRealtime(Hold + 1f);   // the hero and rounds fill in

            GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");

            // ── the PROFILE pillar: Profile → My Avatar → Badges ─────────────
            yield return TapIn(hub, "NavSafeArea/GpsNavBar/NavProfileButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsProfile, 20f);
            Cap("Profile \u2014 /score/stats + /badges/progress");
            yield return new WaitForSecondsRealtime(Hold + 1f);

            GameObject profile = GameObject.Find("Canvas/ScreensRoot/GpsProfileScreen");
            yield return TapFirstIn(profile, "AvatarShortcut");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsAvatar, 20f);
            Cap("My Avatar \u2014 PLAYLIFE level meets the GOLFIN character");
            yield return new WaitForSecondsRealtime(Hold);

            Show(ScreenId.GpsProfile);
            yield return new WaitForSecondsRealtime(Settle);
            yield return TapFirstIn(profile, "BadgesShortcut");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsBadges, 20f);
            Cap("Badges \u2014 four sections, earned state each");
            yield return new WaitForSecondsRealtime(Hold);

            // ── back to the hub, then GIFT through its own nav slot ──────────
            Show(ScreenId.GpsHub);
            yield return new WaitForSecondsRealtime(Settle + 0.4f);

            yield return TapIn(hub, "NavSafeArea/GpsNavBar/NavGiftButton");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsGift, 20f);
            Cap("GIFTS \u2014 gift_pts, discover, the live catalog");
            yield return new WaitForSecondsRealtime(Hold + 2f);   // four requests land here

            GameObject gift = GameObject.Find("Canvas/ScreensRoot/GpsGiftScreen");
            Cap("SEND GIFT \u2014 balance is activity_pts");
            yield return TapIn(gift, "ContentContainer/Golfers/Golfer0/SendGiftButton");
            yield return new WaitForSecondsRealtime(Hold);        // the SEND A GIFT modal
            yield return TapIn(gift, "GiftSendModal/ModalPanel/CancelButtonRow/CancelButton");
            yield return new WaitForSecondsRealtime(Settle);

            Cap("The same modal, purchase mode");
            yield return TapIn(gift, "ContentContainer/BuyGifts/GiftItems/Item0");
            yield return new WaitForSecondsRealtime(Hold);        // the same modal, purchase mode
            yield return TapIn(gift, "GiftSendModal/ModalPanel/CancelButtonRow/CancelButton");
            yield return new WaitForSecondsRealtime(Settle);

            // ── the hub's VOTE tile ──────────────────────────────────────────
            Show(ScreenId.GpsHub);
            yield return new WaitForSecondsRealtime(Settle + 0.4f);

            yield return TapIn(hub, "ContentContainer/ActionTiles/Tile_VOTE");
            yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsVote, 20f);
            Cap("VOTES \u2014 five live votes from /vote/list");
            yield return new WaitForSecondsRealtime(Hold + 2f);   // the list arrives

            GameObject vote = GameObject.Find("Canvas/ScreensRoot/GpsVoteScreen");

            // Scroll the feed so the clip shows more than the first two cards.
            var scroll = vote.transform.Find("ContentContainer/VoteList").GetComponent<ScrollRect>();
            yield return Scroll(scroll, 1f, 0f, 2.4f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Scroll(scroll, 0f, 1f, 1.6f);
            yield return new WaitForSecondsRealtime(0.6f);

            Cap("MINE \u2014 filtered on creator_id");
            yield return TapIn(vote, "ContentContainer/ChipsRow/Chip3");      // MINE
            yield return new WaitForSecondsRealtime(Hold);
            yield return TapIn(vote, "ContentContainer/ChipsRow/Chip2");      // PUBLIC
            yield return new WaitForSecondsRealtime(Settle);

            Cap("CREATE \u2014 question, YES/NO, expiry");
            yield return TapIn(vote, "ContentContainer/ChipsRow/CreateButton");
            yield return new WaitForSecondsRealtime(Hold);                    // CREATE A VOTE
            yield return TapIn(vote, "VoteCreateModal/ModalPanel/CancelButtonRow/CancelButton");
            yield return new WaitForSecondsRealtime(Settle);

            // ── land back on the hub ─────────────────────────────────────────
            Show(ScreenId.GpsHub);
            yield return new WaitForSecondsRealtime(2.2f);

            Cap("Back at the hub");
            yield return new WaitForSecondsRealtime(1.2f);

            WriteCaptions();
            Debug.Log("[GpsFlowDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// A RETURN to a screen already visited, used only where the frame offers no back
        /// affordance of its own. Every FORWARD navigation in this clip is a real onClick — that
        /// is the part the recording is evidence for.
        /// </summary>
        static void Show(ScreenId id) => ScreenManager.Instance?.ShowScreen(id);

        static IEnumerator Scroll(ScrollRect sr, float from, float to, float seconds)
        {
            if (sr == null) yield break;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                sr.verticalNormalizedPosition = Mathf.Lerp(from, to, k);
                yield return null;
            }
            sr.verticalNormalizedPosition = to;
        }

        IEnumerator TapIn(GameObject root, string path)
        {
            Transform t = root != null ? root.transform.Find(path) : null;
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null) { Debug.LogWarning($"[GpsFlowDemo] no button at {path}"); yield break; }
            if (!Press(b, path)) yield break;
            yield return null;
        }

        /// <summary>
        /// Invoke a button's onClick — but ONLY if a player could have.
        ///
        /// <para>The first take of the Score Upload walk called <c>onClick.Invoke()</c>
        /// unconditionally, which walked straight past VERIFY GPS while it was disabled for an
        /// EMPTY scorecard: the clip reached CONFIRM with every figure showing an em dash and the
        /// server refused the post. A recording that can press a button the player cannot is not
        /// evidence of anything.</para>
        /// </summary>
        static bool Press(Button b, string what)
        {
            if (!b.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[GpsFlowDemo] '{what}' is not on screen — not pressed.");
                return false;
            }
            if (!b.interactable)
            {
                Debug.LogWarning($"[GpsFlowDemo] '{what}' is DISABLED — not pressed. " +
                                 "The step before it did not leave the screen in a state that " +
                                 "allows this tap.");
                return false;
            }
            b.onClick.Invoke();
            return true;
        }

        /// <summary>Tap the first of these names that exists anywhere under <paramref name="root"/>.
        /// The Profile screen's shortcuts are `AvatarShortcut` / `BadgesShortcut` (read off the
        /// prefab, after a first take guessed `BadgeShortcut` and silently skipped the screen).</summary>
        IEnumerator TapFirstIn(GameObject root, params string[] names)
        {
            if (root != null)
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                    if (names.Contains(b.gameObject.name))
                    {
                        Press(b, b.gameObject.name);
                        yield return null;
                        yield break;
                    }
            Debug.LogWarning("[GpsFlowDemo] none of [" + string.Join(", ", names) + "] found");
        }

        IEnumerator TapAnywhere(string name, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                               FindObjectsSortMode.None))
                    if (b.name == name && b.gameObject.activeInHierarchy)
                    {
                        Press(b, name);
                        yield return new WaitForSecondsRealtime(1f);
                        yield break;
                    }
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Debug.LogWarning($"[GpsFlowDemo] '{name}' never appeared in {seconds}s");
        }

        IEnumerator Until(Func<bool> done, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!done()) Debug.LogWarning("[GpsFlowDemo] timed out waiting for a screen");
        }
    }
}
#endif
