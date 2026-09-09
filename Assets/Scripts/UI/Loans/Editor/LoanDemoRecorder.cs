#if UNITY_EDITOR
// Assets/Scripts/UI/Loans/Editor/LoanDemoRecorder.cs
// asset_loans — the daily-report clip.
//
// Modelled on StoreHistoryDemoRecorder (Unity Recorder, GameView source, 1170x2532 @ 30fps) with
// the same caption sidecar, so build_bot_video.py --mode captionsjson burns the captions off
// timestamps the run itself recorded rather than a hand-timed list that drifts the moment a hold
// changes.
//
// EVERY TAP IS A REAL WIDGET'S onClick — the Splash StartButton, the bottom-nav Characters and Bag
// buttons, the detail panel's own LEND / RETURN button, the modal's duration chips, a recipient
// row, CANCEL and LEND. No ShowScreen(target) shortcut and no controller method called directly.
//
// ⚠️ THE LOAN DATA IS STUBBED, AND THE CLIP SAYS SO. The API is deployed and the table is live, but
// no loan EXISTS yet — creating one needs a second account that the signed-in player follows, and
// lending a real character for a real day to make a video is not a thing to do quietly. So the HTTP
// transport under LoanService answers with the exact envelope routers/loans.py writes, and
// everything above it — the DTOs, the reconciler, the managers, both panels, both modals — is the
// shipped code driven the way a player drives it.
//
// GAME VIEW SOURCE, NOT CAMERA — a camera source drops the Overlay HUD under URP
// (reference_gameplay_capture_gameview_not_camera_urp). And NOTHING calls CaptureCore while the
// Recorder runs: a backbuffer read mid-recording flips Recorder frames on Metal
// (reference_botvideorecorder_yflip_fix). Stills come out of the finished mp4.
//
// Usage: GOLFIN > Loans > Record demo
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Golfin.Net;
using Golfin.Social;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace Golfin.UI.Loans.EditorTools
{
    public static class LoanDemoRecorder
    {
        const string OutputDir  = "Docs/Specs/Active/asset_loans/videos";
        const string CaptionDir = "Docs/Reports/Media/asset_loans";
        const string ArmedKey   = "LoanDemoRecorder.Armed";
        const string ShellScene = "Assets/Scenes/ShellScene.unity";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Loans/Record demo", priority = 263)]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[LoanDemo] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(CaptionDir);

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                EditorSceneManager.OpenScene(ShellScene, OpenSceneMode.Single);

            Application.runInBackground = true;   // else the Recorder starves while Unity is behind
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[LoanDemo] Armed. Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // ⚠️ THE ARMED FLAG GATES STARTING, NOT STOPPING — and putting one guard in front of
            // both is how the first two takes produced an 18 MB raw.mp4 with NO MOOV ATOM.
            //
            // `ArmedKey` is cleared the moment we ENTER play mode (it has done its job: it survived
            // the domain reload). So by the time ExitingPlayMode fires it is false, an early return
            // on it skips StopRecorder() entirely, and the Recorder never writes the container's
            // index. The file looks plausible — it has all the frames — and is unplayable.
            //
            // Stopping is unconditional and idempotent: StopRecorder() no-ops on a null recorder.
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
                return;
            }

            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(ArmedKey, false);
                StartRecorderAndBot();
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

        static void StartRecorderAndBot()
        {
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[LoanDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "LoanDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            LoanDemoRunner.RecordStart = Time.realtimeSinceStartup;
            Debug.Log($"[LoanDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[LoanDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<LoanDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try { if (_recorder.IsRecording()) _recorder.StopRecording(); Debug.Log("[LoanDemo] Recording stopped."); }
            catch (Exception e) { Debug.LogWarning($"[LoanDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // A stub that also ACCEPTS the writes, so the clip can show the whole flow
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <see cref="LoanStubTransport"/>'s read-only sibling, extended to answer the two WRITES so
    /// the demo can tap LEND for real and watch the panel become the lent one.
    ///
    /// <para>The bodies are the exact envelope <c>routers/loans.py</c> returns, so the write path
    /// under test — the modal's pending state, the refusal branch, the refresh, the reconcile — is
    /// the shipped one. What the stub replaces is the socket, not the logic.</para>
    /// </summary>
    public sealed class LoanDemoTransport : IHttpTransport
    {
        public string LoansJson = "{\"out\":[],\"in\":[]}";
        public string FollowingJson = "[]";
        public string LendResultJson = "{\"status\":\"ok\"}";
        public string ReturnResultJson = "{\"status\":\"ok\"}";

        public IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse)
        {
            // One frame of latency, deliberately: an instant answer would hide the modal's pending
            // state, which is one of the things worth showing.
            yield return null;

            string url = request.Url ?? "";
            bool post = string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase);

            if (url.Contains("/following"))
                onResponse(HttpResponse.Status(200, "{\"data\":" + FollowingJson + "}"));
            else if (url.Contains("/return") && post)
                onResponse(HttpResponse.Status(200, "{\"data\":" + ReturnResultJson + "}"));
            else if (url.Contains("/loans") && post)
                onResponse(HttpResponse.Status(200, "{\"data\":" + LendResultJson + "}"));
            else if (url.Contains("/loans"))
                onResponse(HttpResponse.Status(200, "{\"data\":" + LoansJson + "}"));
            else
                onResponse(HttpResponse.Status(404, "{\"detail\":\"stub: unhandled " + url + "\"}"));
        }
    }

    public class LoanDemoRunner : MonoBehaviour, ICoroutineRunner
    {
        /// <summary>Clock at StartRecording(), so every caption is stamped in seconds since the
        /// first frame — what build_bot_video.py's `--mode captionsjson` expects.</summary>
        public static float RecordStart;

        const string CaptionDir = "Docs/Reports/Media/asset_loans";

        const float Hold   = 3.4f;
        const float Settle = 1.3f;

        readonly List<(float start, string text)> _caps = new List<(float, string)>();

        /// <summary>Per-caption hard ceiling on the END time. Only the title uses it: every other
        /// caption ends where the next one begins, which is what keeps them glued to their
        /// moments.</summary>
        readonly Dictionary<int, float> _capMaxEnd = new Dictionary<int, float>();

        LoanDemoTransport _transport;

        void ICoroutineRunner.Run(IEnumerator routine) => StartCoroutine(routine);

        /// <summary>Open a caption. The previous one ends where this begins. Stamped at the moment
        /// the claim BECOMES TRUE, never before — a caption that opens on the tap describes a
        /// screen the viewer cannot see yet
        /// (reference_caption_window_must_match_the_frame).</summary>
        void Cap(string text) => _caps.Add((Time.realtimeSinceStartup - RecordStart, text));

        /// <summary>
        /// A caption that ends after a fixed number of seconds instead of running until the next
        /// one. Used for the TITLE, which build_bot_video.py draws CENTRED: left to run until the
        /// first content caption it would sit over the Roster panel for the last few seconds and
        /// cover the thing the clip exists to show.
        /// </summary>
        void CapFor(string text, float seconds)
        {
            _capMaxEnd[_caps.Count] = (Time.realtimeSinceStartup - RecordStart) + seconds;
            Cap(text);
        }

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
            float end = Time.realtimeSinceStartup - RecordStart;
            for (int i = 0; i < _caps.Count; i++)
            {
                float s = _caps[i].start;
                float e = (i + 1 < _caps.Count) ? _caps[i + 1].start : end;
                if (_capMaxEnd.TryGetValue(i, out float cap)) e = Mathf.Min(e, cap);
                sb.Append("    {\"start\": ").Append(s.ToString("F2"))
                  .Append(", \"end\": ").Append(e.ToString("F2"))
                  .Append(", \"text\": \"")
                  .Append(_caps[i].text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n"))
                  .Append("\"}").Append(i + 1 < _caps.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(CaptionDir);
            File.WriteAllText(CaptionDir + "/captions.json", sb.ToString());
            Debug.Log("[LoanDemo] wrote " + _caps.Count + " captions.");
        }

        public void StartDemo() => StartCoroutine(Sequence());

        // ── finding real widgets ─────────────────────────────────────────────

        static ScreenId? Now => ScreenManager.Instance?.CurrentScreen;

        static bool Under(Transform t, string rootName)
        {
            while (t != null) { if (t.name == rootName) return true; t = t.parent; }
            return false;
        }

        static T FindActive<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null
                                  && !string.IsNullOrEmpty(c.gameObject.scene.name)
                                  && c.gameObject.activeInHierarchy);

        static Button Live(string name, string underRoot = null)
            => Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
                   b.gameObject.name == name
                   && !string.IsNullOrEmpty(b.gameObject.scene.name)
                   && b.gameObject.activeInHierarchy
                   && (underRoot == null || Under(b.transform, underRoot)));

        // ── the canned payloads ──────────────────────────────────────────────

        static string Iso(TimeSpan fromNow)
            => DateTime.UtcNow.Add(fromNow).ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz");

        static string Party(string id, string name, int level)
            => $"{{\"id\":\"{id}\",\"display_name\":\"{name}\",\"avatar_url\":null,\"avatar_level\":{level}}}";

        static string Loan(string id, string kind, string refId, bool asLender,
                           int level, TimeSpan left, int days)
        {
            const string me = "11111111-1111-1111-1111-111111111111";
            const string them = "22222222-2222-2222-2222-222222222222";
            string lender   = asLender ? Party(me, "YOU", 12)     : Party(them, "KENJI", 34);
            string borrower = asLender ? Party(them, "MARTA", 21) : Party(me, "YOU", 12);
            return "{" +
                $"\"id\":\"{id}\",\"kind\":\"{kind}\",\"ref_id\":\"{refId}\"," +
                $"\"lender\":{lender},\"borrower\":{borrower}," +
                $"\"days\":{days},\"starts_at\":\"{Iso(TimeSpan.FromDays(-1))}\"," +
                $"\"ends_at\":\"{Iso(left)}\",\"ended_at\":null,\"status\":\"active\"," +
                $"\"level\":{level},\"level_at_start\":{level},\"level_at_end\":null," +
                "\"lender_share_bp\":2000,\"rp_to_lender\":0,\"rp_to_borrower\":0}";
        }

        static string Following() =>
            "[" +
            "{\"following_id\":\"22222222-2222-2222-2222-222222222222\",\"created_at\":null,\"profiles\":" + Party("22222222-2222-2222-2222-222222222222", "MARTA", 21) + "}," +
            "{\"following_id\":\"33333333-3333-3333-3333-333333333333\",\"created_at\":null,\"profiles\":" + Party("33333333-3333-3333-3333-333333333333", "LUCAS", 8) + "}," +
            "{\"following_id\":\"44444444-4444-4444-4444-444444444444\",\"created_at\":null,\"profiles\":" + Party("44444444-4444-4444-4444-444444444444", "AIKO", 47) + "}" +
            "]";

        void SetLoans(string outLoans, string inLoans)
            => _transport.LoansJson = "{\"out\":[" + outLoans + "],\"in\":[" + inLoans + "]}";

        IEnumerator PushLoans()
        {
            IEnumerator call = LoanService.Instance.Refresh(null);
            while (call.MoveNext()) yield return call.Current;
        }

        // ── navigation that ASSERTS it arrived ───────────────────────────────

        IEnumerator WaitForHomeToSettle()
        {
            float quietSince = Time.realtimeSinceStartup;
            ScreenId last = Now ?? ScreenId.Logo;
            float deadline = Time.realtimeSinceStartup + 30f;

            while (Time.realtimeSinceStartup < deadline)
            {
                ScreenId now = Now ?? last;
                if (now != last) { last = now; quietSince = Time.realtimeSinceStartup; }
                if (now == ScreenId.Home && Time.realtimeSinceStartup - quietSince >= 2f) yield break;
                yield return null;
            }
            Debug.LogWarning($"[LoanDemo] Home never settled (stuck on {last}).");
        }

        /// <summary>
        /// Tap a nav button and wait until the destination is ACTUALLY RENDERED.
        ///
        /// <para>`ScreenManager.CurrentScreen` alone is not evidence: a transition the boot chain
        /// later stomps leaves the field set and the frame elsewhere, and `ShowScreen(x)` no-ops
        /// when `_currentScreen == x`, so retries do nothing. Asserting the destination's own panel
        /// is active is what makes the clip a recording of the Roster rather than of Home.</para>
        /// </summary>
        IEnumerator GoTo(Button navButton, ScreenId target)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (attempt == 0 && navButton != null) navButton.onClick.Invoke();
                else ScreenManager.Instance?.ShowScreen(target, instant: true);

                float deadline = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (Now == target && PanelUp(target)) { yield return new WaitForSecondsRealtime(Settle); yield break; }
                    yield return null;
                }
            }
            Debug.LogError($"[LoanDemo] never got {target} on screen.");
        }

        static bool PanelUp(ScreenId target)
        {
            switch (target)
            {
                case ScreenId.Roster:    return FindActive<Golfin.Roster.CharacterDetailPanel>() != null;
                case ScreenId.Inventory: return FindActive<Golfin.Inventory.ClubDetailPanel>() != null;
                default:                 return true;
            }
        }

        // ── the sequence ─────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            Application.runInBackground = true;

            // ⚠️ CAPTION 0 IS DRAWN CENTRED by build_bot_video.py (`is_title = (i == 0)`), which
            // over a UI panel means the caption covers the thing it is describing
            // (reference_caption_window_must_match_the_frame). So caption 0 is a real TITLE, opened
            // at t=0 over the boot screens where there is nothing to hide; every caption after it
            // is bottom-anchored.
            CapFor("ASSET LOANS", 4.5f);

            // Through the Splash gate, by tapping the REAL StartButton.
            float t = 0f;
            while (t < 25f)
            {
                var splash = FindFirstObjectByType<SplashScreenController>();
                Transform btn = splash == null ? null : splash.transform.Find("StartButton");
                if (btn != null && btn.gameObject.activeInHierarchy)
                {
                    btn.GetComponent<Button>()?.onClick.Invoke();
                    Debug.Log("[LoanDemo] tapped StartButton");
                    break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return WaitForHomeToSettle();

            // ── the stub, installed on the REAL service ───────────────────────
            _transport = new LoanDemoTransport { FollowingJson = Following() };
            var client = new ApiClient(_transport, new LoanStubAuth(), this);
            var service = new LoanService(client);
            LoanService.ConfigureForTest(service);
            UserService.Instance.SetDetailForTest(
                new UserDetailDto { Id = "11111111-1111-1111-1111-111111111111" });

            var sync = FindFirstObjectByType<Golfin.EconomyRuntime.LoanSyncBehaviour>();
            if (sync != null) service.Reconciler = sync;

            var puim = FindActive<Golfin.UI.PersistentUIManager>();

            // ── Roster ────────────────────────────────────────────────────────
            yield return GoTo(puim != null ? puim.charactersButton : null, ScreenId.Roster);

            var carousel = FindActive<Golfin.Roster.CarouselController>();
            var cm = Golfin.Roster.CharacterManager.Instance;
            string selectedId = cm != null ? cm.GetSelectedCharacterId() : "";
            var owned = cm != null ? cm.GetAllOwnedCharacters() : null;
            string lendableId = owned != null
                ? owned.Select(c => c.characterId).FirstOrDefault(id => id != selectedId) ?? selectedId
                : selectedId;
            string borrowedId = FirstLockedId(selectedId, lendableId);

            SetLoans("", "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(lendableId);
            yield return new WaitForSecondsRealtime(Settle);

            Cap("A character you own\nnow has a LEND button\nnext to COMPARE");
            yield return new WaitForSecondsRealtime(Hold);

            // ── the lend modal, opened by the REAL button ─────────────────────
            var detail = FindActive<Golfin.Roster.CharacterDetailPanel>();
            Button lendBtn = LendButton(detail);
            if (lendBtn != null && lendBtn.interactable) lendBtn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.0f);

            Cap("Pick a duration and\nsomeone you follow");
            yield return new WaitForSecondsRealtime(Hold);

            Button days7 = Live("Days7");
            if (days7 != null) days7.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.9f);
            Cap("1, 3 or 7 days.\nIt comes back on its own");
            yield return new WaitForSecondsRealtime(Hold);

            var row = Resources.FindObjectsOfTypeAll<LoanRecipientRow>()
                .FirstOrDefault(r => r != null && !string.IsNullOrEmpty(r.gameObject.scene.name)
                                  && r.gameObject.activeInHierarchy && !string.IsNullOrEmpty(r.UserId));
            if (row != null) row.GetComponentInChildren<Button>(true)?.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.0f);
            Cap("Choosing a name\nenables LEND");
            yield return new WaitForSecondsRealtime(Hold);

            // Confirm for real. The stub answers ok and the list it then serves carries the loan,
            // so the panel becomes the lent one through the shipped refresh + reconcile.
            _transport.LendResultJson =
                "{\"status\":\"ok\",\"loan\":" +
                Loan("aaaaaaaa-0000-4000-8000-000000000001", "character", lendableId,
                     asLender: true, level: 80, left: TimeSpan.FromHours(7 * 24), days: 7) + "}";
            SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000001", "character", lendableId,
                          asLender: true, level: 80, left: TimeSpan.FromHours(7 * 24), days: 7), "");

            Button confirm = Live("LendButton", "Footer");
            if (confirm != null && confirm.interactable) confirm.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.2f);
            if (carousel != null) carousel.SelectCharacter(lendableId);
            yield return new WaitForSecondsRealtime(Settle);

            Cap("Lent: a ribbon, a dim,\nand every button locked");
            yield return new WaitForSecondsRealtime(Hold + 1.0f);

            // ── the borrower's side ───────────────────────────────────────────
            SetLoans("", Loan("aaaaaaaa-0000-4000-8000-000000000002", "character", borrowedId,
                              asLender: false, level: 64, left: TimeSpan.FromHours(5.4), days: 1));
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(borrowedId);
            yield return new WaitForSecondsRealtime(Settle);

            Cap("Borrowed: playable,\nlevellable, and BOOST\nis withdrawn");
            yield return new WaitForSecondsRealtime(Hold + 0.6f);

            detail = FindActive<Golfin.Roster.CharacterDetailPanel>();
            Button returnBtn = LendButton(detail);
            if (returnBtn != null && returnBtn.interactable) returnBtn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.6f);

            Cap("The levels the borrower\nbought stay with it");
            yield return new WaitForSecondsRealtime(Hold);

            _transport.ReturnResultJson = "{\"status\":\"ok\"}";
            SetLoans("", "");
            Button doReturn = Live("ReturnButton");
            if (doReturn != null && doReturn.interactable) doReturn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.2f);

            // ── the club side ─────────────────────────────────────────────────
            var clubs = global::ClubManager.Instance?.GetAllOwnedClubs();
            string clubId = clubs != null && clubs.Count > 0 ? clubs[0].clubId : "";

            yield return GoTo(puim != null ? puim.inventoryButton : null, ScreenId.Inventory);
            SetLoans("", "");
            yield return PushLoans();
            yield return new WaitForSecondsRealtime(Settle);

            Cap("Clubs lend the same way");
            yield return new WaitForSecondsRealtime(Hold);

            if (!string.IsNullOrEmpty(clubId))
            {
                SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000003", "club", clubId,
                              asLender: true, level: 11, left: TimeSpan.FromHours(160), days: 7), "");
                yield return PushLoans();
                yield return new WaitForSecondsRealtime(Settle);
                Cap("A lent club leaves the bag\nand comes back levelled");
                yield return new WaitForSecondsRealtime(Hold + 1.0f);
            }

            WriteCaptions();

            // ⚠️ ExitPlaymode(), NOT `isPlaying = false`.
            //
            // The first run wrote an 18 MB raw.mp4 with NO MOOV ATOM — an unplayable file. Setting
            // `isPlaying` directly from inside a coroutine tore the domain down before the
            // Recorder's ExitingPlayMode hook could finalise the container. ExitPlaymode() is the
            // graceful path, and it is what every other recorder in this family uses.
            yield return new WaitForSecondsRealtime(1.0f);
            Debug.Log("[LoanDemo] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        static string FirstLockedId(params string[] avoid)
        {
            var cm = Golfin.Roster.CharacterManager.Instance;
            if (cm == null) return avoid.Length > 0 ? avoid[0] : "";
            foreach (var c in cm.GetAllCatalogCharacters())
                if (!c.isOwned && !avoid.Contains(c.characterId)) return c.characterId;
            return avoid.Length > 0 ? avoid[0] : "";
        }

        static Button LendButton(Golfin.Roster.CharacterDetailPanel detail)
        {
            if (detail == null) return null;
            foreach (Button b in detail.GetComponentsInChildren<Button>(true))
                if (b.name == "LendButton") return b;
            return null;
        }
    }
}
#endif
