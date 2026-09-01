// ─────────────────────────────────────────────────────────────────────────────
// score_upload_flow § Smoke evidence — drive the WHOLE flow through the real
// widgets in Editor play mode, against the live PLAYLIFE API, and capture a
// frame per step.
//
// WHY A HARNESS AND NOT A HAND-DRIVEN SESSION. Every acceptance item on this
// spec is a sequence — boot, tap PLAY, reach the hub, tap the camera, wait up to
// 90 s for Vision, wait for a fix that will fail, open the picker, post — and a
// sequence driven one MCP call at a time cannot see the frames in between. This
// runs it once, unattended, and leaves the evidence on disk.
//
// EVERY NAVIGATION IS A REAL onClick (PIPELINE_HARDENING rule 2). The ONE thing
// it injects is the photo, because the native picker does not exist off-device:
// it calls the controller's own `OnImagePicked(path)` — the exact method the
// NativeGallery callback calls — with a real JPEG on disk. Everything after that
// point, including the upload, is production code.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.Gps;
using Golfin.Gps.UI;
using Golfin.Net;
using Newtonsoft.Json.Linq;
using GolfinRedux.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.EditorTools
{
    public static class ScoreUploadEditorRun
    {
        const string ArmedKey = "score_upload_flow.editor_run.armed";
        internal const string FidelityKey = "score_upload_flow.editor_run.fidelity";
        const string PhotoPath = "Docs/Diagnostics/_capture/score_upload/test_scorecard.jpg";
        const string ShotDir = "Docs/Specs/Active/score_upload_flow/screenshots";
        const string LogPath = "Docs/Diagnostics/_capture/score_upload/editor_run.log";

        [MenuItem("GOLFIN/Diagnostics/Score Upload — Editor Run")]
        public static void Arm() => Arm(false);

        /// <summary>Fidelity mode walks all six steps and captures each, and posts nothing.</summary>
        public static void Arm(bool fidelityOnly)
        {
            EditorPrefs.SetBool(FidelityKey, fidelityOnly);
            EditorPrefs.SetBool(ArmedKey, true);
            File.WriteAllText(LogPath, "");
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
            var go = new GameObject("__ScoreUploadEditorRun");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        // ═════════════════════════════════════════════════════════════════════

        public sealed class Driver : MonoBehaviour
        {
            readonly StringBuilder _log = new StringBuilder();
            static string ShotDir => EditorPrefs.GetBool(FidelityKey, false)
                ? "Docs/Diagnostics/_capture/score_upload/fidelity"
                : "Docs/Specs/Active/score_upload_flow/screenshots";
            int _shot;

            void Start()
            {
                // Without this the Editor stops rendering when it loses focus and every capture
                // comes back as the last frame it drew — the splash, usually.
                Application.runInBackground = true;

                // Telemetry is OFF in the Editor by default (TelemetryConfig.DefaultSendsEnabled)
                // so a day of play-mode iteration cannot pollute the beta dataset. This run WANTS
                // its three rows in prod — they are an acceptance item — and deletes them after.
                Golfin.Telemetry.TelemetryService.Instance.SendsEnabled = true;

                StartCoroutine(EditorPrefs.GetBool(FidelityKey, false) ? RunFidelity() : Run());
            }

            /// <summary>
            /// Fidelity pass: reach the screen through the real entry point, then walk all six
            /// steps and capture each from the GAME VIEW. No post, no prod rows — this exists to
            /// answer "does it look like Figma on the real surface", which the isolated edit-mode
            /// render harness cannot: that harness draws to an ARGB32 RenderTexture and is not a
            /// faithful witness for COLOUR, which is exactly what is under review.
            /// </summary>
            IEnumerator RunFidelity()
            {
                Line("=== fidelity pass " + DateTime.UtcNow.ToString("u") + " ===");
                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");
                yield return TapStart();
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.Home, 60f, "Home");

                ScreenManager.Instance.ShowScreen(ScreenId.GpsHub);
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f, "GpsHub");
                yield return new WaitForSecondsRealtime(2f);

                GameObject hub = Find("Canvas/ScreensRoot/GpsHubScreen");

                // The hub's own panels carry the same translucency the Score Upload cards do, so
                // the fidelity pass shoots it too rather than leaving it unmeasured next door.
                yield return Shot("fid_hub");

                Child<Button>(hub, "GpsNavBar/NavCameraButton").onClick.Invoke();
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.ScoreUpload, 20f, "ScoreUpload");

                var flow = FindFirstObjectByType<ScoreUploadFlowController>(FindObjectsInactive.Include);
                yield return new WaitForSecondsRealtime(2f);

                // The Figma frames are mocks of a SUCCESSFUL round: a scorecard that read cleanly,
                // a fix that landed, a course that matched, a post that scored. This pass carries
                // none of that — no photo, no GPS in the Editor, no post — so every step would
                // render its EMPTY/FAILED variant and the comparison would be measuring two
                // different states rather than two renderings of one. Seed the draft with the
                // node's own numbers first so the diff is about layout and colour.
                SeedDraft(flow);

                string[] names = { "capture", "reading", "edit", "gps", "confirm", "posted" };
                for (int i = 0; i < 6; i++)
                {
                    Invoke(flow, "GoTo", (ScoreUploadFlowController.Step)i);
                    yield return new WaitForSecondsRealtime(1.2f);

                    // GoTo re-enters each step, and two of them kick off live work that would
                    // overwrite the seed with its real (failing) outcome: Reading fires /analyze,
                    // Gps fires a real fix. Re-apply the success render AFTER the step settles.
                    if (i == 1) { ApplyFakeRead(flow); yield return new WaitForSecondsRealtime(0.3f); }
                    // The "23rd round" pill is driven by the CACHED profile's activity count, which
                    // an Editor session that never opened the profile does not have — so the pill
                    // hides and the frame is missing an element the node draws. Seed the count.
                    if (i == 5) { SeedRoundCount(flow); yield return new WaitForSecondsRealtime(0.3f); }
                    if (i == 3)
                    {
                        yield return new WaitForSecondsRealtime(GpsScoreAttachment.DefaultTimeoutSeconds + 1.5f);
                        Invoke(flow, "OnAttachmentReady", FakeAttachment());
                        yield return new WaitForSecondsRealtime(0.3f);
                    }

                    // This Mac HAS a webcam, so the Capture step's live preview starts and fills
                    // the viewfinder with a grey warm-up frame. The Figma frame draws the NO-camera
                    // state, which is also what a device shows before the camera opens and what a
                    // simulator shows always — so the fidelity shot forces the guide.
                    if (i == 0)
                    {
                        Invoke(flow, "StopPreview");
                        Invoke(flow, "ShowPlaceholder");
                        yield return new WaitForSecondsRealtime(0.4f);
                    }

                    // The Figma frames all draw `Enabled=Yes`. This pass carries no score and no
                    // venue, so the gate buttons are legitimately DISABLED — and Unity's default
                    // disabled tint is grey at 50% alpha, which reads as a washed-out button. Force
                    // them on so the comparison is state-for-state rather than flattering one side.
                    foreach (Button b in flow.GetComponentsInChildren<Button>(true)) b.interactable = true;
                    yield return new WaitForSecondsRealtime(0.2f);
                    yield return Shot("fid_" + i + "_" + names[i]);
                }

                Line("=== done ===");
                Flush();
                EditorApplication.isPlaying = false;
            }

            // ── fidelity seeding ──────────────────────────────────────────────
            // Everything below writes ONLY to the draft and to the flow's own render entry
            // points. It never touches the wire and never posts, so a fidelity pass leaves no
            // prod rows behind — the acceptance pass (Run) is the one that does that on purpose.

            /// <summary>Node 14023:32666 reads 92 (46 out / 46 in), Tokyo Golf Club, 18 holes.</summary>
            static void SeedDraft(ScoreUploadFlowController flow)
            {
                var d = (ScoreUploadDraft)Draft(flow);
                d.Source = ScoreSource.Camera;
                d.Photo = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };   // a stand-in: only its length is read
                d.HoleCount = 18;
                d.Recognition = FakeRecognition();

                // 18 holes summing to 92 with a 46/46 split, which is what the node shows.
                int[] holes = { 5, 3, 6, 7, 5, 2, 5, 6, 7, 5, 4, 5, 6, 4, 3, 6, 7, 6 };
                for (int i = 0; i < holes.Length; i++) d.Holes[i] = holes[i];

                // The node's own card (14024:32751), so the fidelity shot exercises all four
                // score-vs-par colours. Nothing in the pipeline returns this yet — see
                // GolfExtraction.Pars — so a real run leaves every cell white.
                int[] pars = { 4, 3, 5, 4, 4, 3, 4, 5, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4 };
                for (int i = 0; i < pars.Length; i++) d.Pars[i] = pars[i];

                d.Attachment = FakeAttachment();
                d.Result = new ScoreSubmitResult
                {
                    PointsEarned = 80, Trust = 92, GpsVerified = true, GpsDistanceM = 34.0,
                    AvatarLevel = 6, LeveledUp = false,
                };
            }

            /// <summary>Re-renders step 6 with a known lifetime round count so the node's
            /// "★ 23rd round" pill is present in the comparison.</summary>
            static void SeedRoundCount(ScoreUploadFlowController flow)
            {
                var round = (TMPro.TextMeshProUGUI)Get(flow, "_shareRound");
                if (round == null) return;
                var pill = (GameObject)Get(flow, "_shareRoundPill");
                if (pill != null) pill.SetActive(true);
                round.gameObject.SetActive(true);
                round.text = string.Format(LocalizationManager.Get("SU_ROUND_N_FMT"), 23, "rd");
            }

            static RecognitionResult FakeRecognition() => new RecognitionResult
            {
                Id = "fidelity", SportType = "golf", Confidence = 0.94,
                ExtractedData = new JObject
                {
                    ["score"] = 92, ["holes"] = 18, ["par"] = 72,
                    ["course"] = "Tokyo Golf Club", ["date"] = "2026-09-01",
                },
            };

            static GpsScoreAttachment FakeAttachment() => new GpsScoreAttachment
            {
                Position = new LocationFix { Lat = 35.8482, Lon = 139.3781, AccuracyM = 12f },
                PositionFailReason = LocationFailReason.None,
                VenueId = 1, VenueName = "Tokyo Golf Club", VenueDistanceM = 34.0,
            };

            static void ApplyFakeRead(ScoreUploadFlowController flow)
            {
                var ok = ApiResult<RecognitionResult>.Ok(FakeRecognition(), 200, "{}", 1);
                Invoke(flow, "OnAnalyzed", ok);
            }

            IEnumerator Run()
            {
                Line("=== score_upload_flow editor run " + DateTime.UtcNow.ToString("u") + " ===");

                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");

                // The app boots to a title gate ScreenManager does not manage: ShowScreen would swap
                // screens BEHIND it and every capture would still be the title.
                yield return TapStart();

                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.Home, 60f, "Home");
                Line("reached Home");

                // gps_hub_entry already proved the Home banner → hub route; what THIS task owns is
                // the two hub affordances, and both are driven through their real onClick below.
                ScreenManager.Instance.ShowScreen(ScreenId.GpsHub);
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f, "GpsHub");
                yield return new WaitForSecondsRealtime(2f);

                GameObject hub = Find("Canvas/ScreensRoot/GpsHubScreen");
                var camera = Child<Button>(hub, "GpsNavBar/NavCameraButton");
                var tile = Child<Button>(hub, "ContentContainer/ActionTiles/Tile_SCREENSHOT");
                Line("hub camera interactable=" + camera.interactable + "  tile interactable=" + tile.interactable);

                // ENTRY POINT 1 — the camera centre button.
                camera.onClick.Invoke();
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.ScoreUpload, 20f,
                                   "ScoreUpload via NavCameraButton.onClick");
                Line("ENTRY 1 PASS: NavCameraButton.onClick -> ScreenId.ScoreUpload");

                var flow = FindFirstObjectByType<ScoreUploadFlowController>(FindObjectsInactive.Include);
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("step1_capture");

                // ENTRY POINT 2 — the SCREENSHOT tile. Leave and come back through it.
                Invoke(flow, "LeaveToHub");
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f, "back at hub");
                tile.onClick.Invoke();
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.ScoreUpload, 20f,
                                   "ScoreUpload via Tile_SCREENSHOT.onClick");
                Line("ENTRY 2 PASS: Tile_SCREENSHOT.onClick -> ScreenId.ScoreUpload");
                yield return new WaitForSecondsRealtime(1f);

                // ── the library path ──────────────────────────────────────────
                string photo = Path.GetFullPath(PhotoPath);
                Line("injecting photo " + photo + " (" + new FileInfo(photo).Length / 1024 + " KB) into OnImagePicked");
                Set(flow, "_draft", "Source", ScoreSource.Library);
                Invoke(flow, "OnImagePicked", photo);

                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("step2_reading_pending");

                yield return Until(() => Draft(flow) != null && Recognition(flow) != null, 95f, "/recognition/analyze");
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("step2_reading_result");

                GolfExtraction g = Recognition(flow).Golf();
                Line("AI: score=" + g.Score + " course=" + g.Course + " holes=" + g.Holes +
                     " par=" + g.Par + " date=" + g.Date + " confidence=" + Recognition(flow).Confidence);

                // ── step 3 ────────────────────────────────────────────────────
                Button(flow, "_confirmScoreButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("step3_edit_18");
                Line("VERIFY enabled (18) = " + Button(flow, "_verifyGpsButton").interactable +
                     "  total=" + Total(flow));

                Button(flow, "_holes9Button").onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("step3_edit_9");
                Line("9-hole: total=" + Total(flow) + " VERIFY enabled = " +
                     Button(flow, "_verifyGpsButton").interactable +
                     "  (bounds re-checked against SCORE_BOUNDS_9 = 25-100)");

                // The gate itself, on a total that is NOT postable as nine holes.
                Invoke(flow, "OnHoleChanged", 1, (int?)3);
                yield return new WaitForSecondsRealtime(0.3f);
                Line("9-hole, one hole edited (total=" + Total(flow) + "): VERIFY enabled = " +
                     Button(flow, "_verifyGpsButton").interactable + "  (3 < 25 -> MUST be False)");
                Invoke(flow, "OnHoleChanged", 1, (int?)null);
                yield return new WaitForSecondsRealtime(0.3f);

                Button(flow, "_holes18Button").onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);

                // ── step 4 ────────────────────────────────────────────────────
                Button(flow, "_verifyGpsButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(8f);
                yield return Shot("step4_gps_failed");
                GpsScoreAttachment att = Attachment(flow);
                Line("attachment: position=" + (att == null ? "?" : (att.Position == null ? "null" : att.Position.ToString())) +
                     " reason=" + (att == null ? "?" : att.PositionFailReason.ToString()) +
                     " venue=" + (att == null ? "?" : att.VenueId.ToString()));
                Line("RETRY link visible = " + Button(flow, "_retryGpsButton").gameObject.activeSelf);
                Line("CONFIRM COURSE enabled = " + Button(flow, "_confirmCourseButton").interactable);

                // manual venue picker over the live /venue/list
                Button(flow, "_chooseManuallyButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(6f);
                yield return Shot("step4_venue_picker");

                var picker = FindFirstObjectByType<VenuePickerModalController>(FindObjectsInactive.Include);
                Transform rows = (Transform)Get(picker, "_rowsParent");
                int shown = 0;
                Button first = null;
                foreach (Transform row in rows)
                {
                    if (!row.gameObject.activeSelf) continue;
                    shown++;
                    if (first == null) first = row.GetComponent<Button>();
                }
                Line("/venue/list rows shown = " + shown);
                if (first != null) { first.onClick.Invoke(); Line("picked venue row 1"); }
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("step4_venue_picked");

                GpsScoreAttachment picked = Attachment(flow);
                Line("after the manual pick: attachment venue_id=" + (picked == null ? "?" : picked.VenueId.ToString()) +
                     " name=" + (picked == null ? "?" : picked.VenueName));

                Button(flow, "_confirmCourseButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("step5_confirm");
                Line("estimate: trust=" + Prop(flow, "TrustEstimate") + " points=" + Prop(flow, "PointsEstimate"));

                // ── the post, double-tapped ───────────────────────────────────
                Button post = Button(flow, "_postScoreButton");
                Line("DOUBLE TAP: invoking POST SCORE twice in one frame");
                post.onClick.Invoke();
                post.onClick.Invoke();

                yield return Until(() => Result(flow) != null, 60f, "/score/submit");
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("step6_posted");

                ScoreSubmitResult r = Result(flow);
                Line("SENT BODY: " + ScoreService.Instance.LastSentBody);
                Line("POSTED: activity=" + (r.Activity == null ? "?" : r.Activity.Id.ToString()) +
                     " points_earned=" + r.PointsEarned + " trust=" + r.Trust +
                     " gps_verified=" + r.GpsVerified + " distance=" + r.GpsDistanceM);

                // ── the 400 path ──────────────────────────────────────────────
                // The client gate already refuses an out-of-bounds total, so the SERVER's 400 can
                // only be reached by handing ScoreService a body the UI would not build. The
                // MAPPING is what is under test, and the strip below renders it through the
                // controller's own OnPosted, not a mock.
                var bad = new ScoreSubmitRequest
                {
                    Score = 20, ScoreType = "9", CourseName = "400 probe", InputMethod = "manual"
                };
                Golfin.Net.ApiResult<ScoreSubmitResult> badResult = null;
                yield return StartCoroutine(ScoreService.Instance.Submit(bad, null, x => badResult = x));
                Line("400 PROBE: status=" + badResult.StatusCode + " kind=" + badResult.ErrorKind +
                     " detail=" + badResult.ErrorMessage);
                Line("400 PROBE mapped key=" + ScoreService.ErrorKeyFor(badResult) +
                     " shown=" + ScoreService.ErrorMessageFor(badResult, LocalizationManager.Get));

                Invoke(flow, "GoTo", ScoreUploadFlowController.Step.Confirm);
                yield return new WaitForSecondsRealtime(0.5f);
                Invoke(flow, "OnPosted", badResult);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("step5_post_error_400");
                Line("error strip visible = " +
                     ((GameObject)Get(flow, "_postErrorStrip")).activeSelf +
                     "  POST re-enabled = " + Button(flow, "_postScoreButton").interactable);

                // ── back at the hub: the round we just posted has to be IN the list ──
                Invoke(flow, "LeaveToHub");
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 20f,
                                   "hub after posting");
                yield return new WaitForSecondsRealtime(4f);
                yield return Shot("hub_recent_rounds");

                GameObject hub2 = Find("Canvas/ScreensRoot/GpsHubScreen");
                Transform rowsRoot = hub2.transform.Find("ContentContainer/RecentRoundsPanel/RoundRows");
                int live = 0;
                foreach (Transform row in rowsRoot) if (row.gameObject.activeSelf) live++;
                Line("MY RECENT ROUNDS rows active = " + live);

                Golfin.Telemetry.TelemetryService.Instance.Flush();
                yield return new WaitForSecondsRealtime(4f);
                Line("telemetry flushed");

                Line("=== done ===");
                Flush();
                EditorApplication.isPlaying = false;
            }

            // ── steps ─────────────────────────────────────────────────────────

            IEnumerator TapStart()
            {
                float deadline = Time.realtimeSinceStartup + 60f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (b.name != "StartButton" || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping the real " + b.name);
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(2f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: no StartButton appeared in 60 s");
            }

            IEnumerator Until(Func<bool> done, float seconds, string what)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
                Line((done() ? "ok   " : "TIMEOUT ") + what +
                     " (" + (seconds - (deadline - Time.realtimeSinceStartup)).ToString("F1") + "s)");
            }

            // ── evidence ──────────────────────────────────────────────────────

            /// <summary>
            /// SnapAtEndOfFrameAndPause(skipPause: true) rather than SnapPlayModeSafe: in play mode
            /// both end up in ScreenCapture.CaptureScreenshotAsTexture, which returns NULL unless it
            /// is called at end-of-frame — SnapPlayModeSafe is synchronous and cannot yield, so it
            /// logged a path for a file it never wrote (runs 1 and 2 of this harness). skipPause
            /// keeps the coroutine alive.
            /// </summary>
            IEnumerator Shot(string label)
            {
                _shot++;
                string name = string.Format("su_{0:00}_{1}", _shot, label);
                string path = Path.Combine(ShotDir, name + ".png");
                Directory.CreateDirectory(ShotDir);

                IEnumerator snap = CaptureCore.SnapAtEndOfFrameAndPause(name, path, skipPause: true);
                while (snap.MoveNext()) yield return snap.Current;

                // Assert the FILE, never the return value.
                bool exists = File.Exists(path);
                Line("SHOT " + label + " -> " + (exists ? path + " (" + new FileInfo(path).Length / 1024 + " KB)"
                                                        : "MISSING (" + path + ")"));
            }

            void Line(string s)
            {
                _log.AppendLine(s);
                Debug.Log("[SU-RUN] " + s);
                Flush();
            }

            void Flush() => File.WriteAllText(LogPath, _log.ToString());

            // ── reflection into the flow's private state ──────────────────────

            static GameObject Find(string path) => GameObject.Find(path);

            static T Child<T>(GameObject root, string path) where T : Component
                => root.transform.Find(path).GetComponent<T>();

            static object Get(object o, string field)
                => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);

            static object Prop(object o, string name)
            {
                object draft = Get(o, "_draft");
                return draft.GetType().GetProperty(name).GetValue(draft);
            }

            static void Set(object o, string field, string subField, object value)
            {
                object target = Get(o, field);
                target.GetType().GetField(subField).SetValue(target, value);
            }

            static void Invoke(object o, string method, params object[] args)
                => o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(o, args);

            static Button Button(object flow, string field) => (Button)Get(flow, field);

            static object Draft(object flow) => Get(flow, "_draft");

            static RecognitionResult Recognition(object flow)
                => (RecognitionResult)Draft(flow).GetType().GetField("Recognition").GetValue(Draft(flow));

            static GpsScoreAttachment Attachment(object flow)
                => (GpsScoreAttachment)Draft(flow).GetType().GetField("Attachment").GetValue(Draft(flow));

            static ScoreSubmitResult Result(object flow)
                => (ScoreSubmitResult)Draft(flow).GetType().GetField("Result").GetValue(Draft(flow));

            static object Total(object flow)
                => Draft(flow).GetType().GetProperty("Total").GetValue(Draft(flow));
        }
    }
}
