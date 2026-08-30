#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Golfin.Gameplay.Missions;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Capture harness for <c>daily_mission_home_pill</c> — the Home "NEW DAILY MISSION!" pill.
    ///
    /// Cloned from <see cref="PaginationDotsDemoRecorder"/> (the DemoRecorder family), including
    /// its boot poll: ShellScene goes Logo → Splash → Home only once a session exists, and even a
    /// signed-in editor must have <c>StartButton</c> pressed, so the bot polls for the nav bar and
    /// aborts rather than recording the splash.
    ///
    /// ⚠️ TWO PASSES, DELIBERATELY, AND THEY MUST NOT BE MERGED. Stills and video are separate
    /// menu items because a <c>ScreenCapture</c>/RT read taken WHILE the Recorder is running is
    /// one of the two triggers for the vertical-flip bug (memory
    /// <c>reference_botvideorecorder_yflip_fix</c>). The stills pass never records; the video pass
    /// never snaps.
    ///
    /// ⚠️ IT SEEDS <see cref="DailyMissionState"/> RATHER THAN THE SERVER. The pill reads nothing
    /// else — the live fetch's only job is to write that state — so seeding it exercises the same
    /// code path for every branch the acceptance list needs (streak 0 / 5 / 12, claimed, midnight)
    /// without needing five prod accounts in five different conditions. The REAL fetch is proven
    /// separately, and unseeded, by simply booting: the log line below reports what the server
    /// actually said before anything is overwritten.
    ///
    /// The notice is flipped through its REAL chain — `NoticeService._entries` cleared and
    /// `OnNoticesChanged` raised, which is exactly what "the admin published nothing" produces —
    /// so the screenshot proves `HomeScreenController.SetNewsPanelVisible` → `RefreshPlacement`,
    /// not a direct poke at the pill.
    ///
    /// Usage:  GOLFIN > Daily Pill > Capture Stills   /   Record Demo Video
    /// Out:    Docs/Specs/Completed/daily_mission_home_pill/{screenshots,videos}
    /// </summary>
    public static class DailyMissionPillDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string TaskDir        = "Docs/Specs/Completed/daily_mission_home_pill";
        const string ArmedStills    = "DailyPillDemo.ArmedStills";
        const string ArmedVideo     = "DailyPillDemo.ArmedVideo";
        const string ArmedVerify    = "DailyPillDemo.ArmedVerify";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Daily Pill/Capture Stills")]
        public static void LaunchStills() => Arm(ArmedStills);

        [MenuItem("GOLFIN/Daily Pill/Record Demo Video")]
        public static void LaunchVideo() => Arm(ArmedVideo);

        /// <summary>
        /// The deterministic gate (PIPELINE_HARDENING §3 in spirit): per-assertion PASS/FAIL to
        /// `pill_invariants.json`. Placement Y against both Figma frames, the glow's per-frame
        /// allocation, and whether the real tap actually queued `daily_pill_tap`. A human reading
        /// the video is the artefact; this JSON is the gate.
        /// </summary>
        [MenuItem("GOLFIN/Daily Pill/Verify Invariants")]
        public static void LaunchVerify() => Arm(ArmedVerify);

        static void Arm(string key)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[DailyPillDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory($"{TaskDir}/screenshots");
            Directory.CreateDirectory($"{TaskDir}/videos");
            // Prefab provenance must be read HERE, in edit mode: PrefabUtility answers "" for a
            // running instance, so the same query inside play mode reported a false FAIL.
            SessionState.SetString("DailyPillDemo.Prov", ReadProvenance());
            SessionState.SetBool(key, true);
            EditorApplication.EnterPlaymode();
            Debug.Log($"[DailyPillDemo] Armed ({key}). Entering play mode...");
        }

        /// <summary>`pillFlame|cardCollapsedFlame|cardExpandedFlame` prefab asset paths.</summary>
        static string ReadProvenance()
        {
            string Src(GameObject root, string path)
            {
                var t = root != null ? root.transform.Find(path) : null;
                return t != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) : "<missing>";
            }
            var pill = GameObject.Find("Canvas/ScreensRoot/HomeScreen/DailyMissionPill");
            var card = GameObject.Find("Canvas/ScreensRoot/MissionSelectionScreen/Content/DailyMissionCard");
            return string.Join("|", new[]
            {
                Src(pill, "StreakFlame"),
                Src(card, "CollapsedContainer/TitleArea/TitleHRow/DailyStreak"),
                Src(card, "ExpandedContainer/TitleAreaExp/TitleHRowExp/DailyStreakExp"),
            });
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Never gate the STOP path on the armed key — arming is cleared on entry, so gating
            // both branches leaves the recorder's file open and the mp4 without a moov atom.
            if (state == PlayModeStateChange.ExitingPlayMode) { StopRecorder(); return; }
            if (state != PlayModeStateChange.EnteredPlayMode) return;

            bool stills = SessionState.GetBool(ArmedStills, false);
            bool video  = SessionState.GetBool(ArmedVideo, false);
            bool verify = SessionState.GetBool(ArmedVerify, false);
            if (!stills && !video && !verify) return;
            SessionState.SetBool(ArmedStills, false);
            SessionState.SetBool(ArmedVideo, false);
            SessionState.SetBool(ArmedVerify, false);

            if (video) StartRecorder();

            var host = new GameObject("[DailyPillBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            if (verify) host.AddComponent<DailyMissionPillVerifier>().Begin();
            else        host.AddComponent<DailyMissionPillDemoRunner>().Begin(video);
        }

        static bool TryEnsureIPhone14Selected()
        {
            try
            {
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
                return m != null && (bool)m.Invoke(null, null);
            }
            catch { return false; }
        }

        static void StartRecorder()
        {
            bool pinned = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!pinned)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[DailyPillDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "DailyPillDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{TaskDir}/videos/raw";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[DailyPillDemo] Recording → {TaskDir}/videos/raw.mp4 ({w}x{h} @ 30fps)");
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[DailyPillDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[DailyPillDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class DailyMissionPillDemoRunner : MonoBehaviour
    {
        const string TaskDir = "Docs/Specs/Completed/daily_mission_home_pill";

        bool _video;
        float _t0;
        readonly List<KeyValuePair<float, string>> _marks = new List<KeyValuePair<float, string>>();

        public void Begin(bool video) { _video = video; _t0 = Time.time; StartCoroutine(Sequence()); }

        /// <summary>Stamp a caption at the current offset from record start. Consumed by
        /// `build_bot_video.py --mode captionsjson` (Cesar: every video gets captions).</summary>
        void Mark(string caption)
        {
            if (!_video) return;
            _marks.Add(new KeyValuePair<float, string>(Time.time - _t0, caption));
        }

        void WriteCaptions()
        {
            if (!_video || _marks.Count == 0) return;
            try
            {
                Directory.CreateDirectory($"{TaskDir}/videos");
                // The tool does data.get("captions") — a bare array is NOT accepted.
                var sb = new System.Text.StringBuilder("{\n  \"captions\": [\n");
                for (int i = 0; i < _marks.Count; i++)
                {
                    float start = _marks[i].Key;
                    float end   = (i + 1 < _marks.Count) ? _marks[i + 1].Key : start + 4f;
                    sb.Append("    {\"start\": ").Append(start.ToString("F2"))
                      .Append(", \"end\": ").Append(end.ToString("F2"))
                      .Append(", \"text\": \"").Append(_marks[i].Value.Replace("\"", "\\\"").Replace("\n", "\\n"))
                      .Append("\"}").Append(i < _marks.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ]\n}\n");
                File.WriteAllText($"{TaskDir}/videos/captions.json", sb.ToString());
                Debug.Log($"[DailyPillBot] captions → {TaskDir}/videos/captions.json ({_marks.Count} marks)");
            }
            catch (Exception e) { Debug.LogWarning("[DailyPillBot] captions: " + e.Message); }
        }

        static T FindActive<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null
                    && !string.IsNullOrEmpty(c.gameObject.scene.name)
                    && c.gameObject.activeInHierarchy);

        // ── The notice, flipped through its own real chain ───────────────────
        /// <summary>
        /// Clear or restore the live notices exactly the way an empty publish does: wipe
        /// `_entries`, mark the page cache dirty, raise `OnNoticesChanged`. Home's own handler
        /// then hides the panel and calls the pill's RefreshPlacement — which is the wiring the
        /// screenshot is meant to prove, so it must not be short-circuited.
        /// </summary>
        static object _savedEntries;
        static void SetNoticesPresent(bool present)
        {
            var svc = Golfin.Notices.NoticeService.Instance;
            if (svc == null) { Debug.LogWarning("[DailyPillBot] NoticeService missing."); return; }
            var t  = svc.GetType();
            var fe = t.GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
            var fd = t.GetField("_pagesDirty", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = fe?.GetValue(svc) as IList;
            if (list == null) { Debug.LogWarning("[DailyPillBot] _entries not found."); return; }

            if (!present)
            {
                if (_savedEntries == null)
                {
                    var copy = (IList)Activator.CreateInstance(list.GetType());
                    foreach (var e in list) copy.Add(e);
                    _savedEntries = copy;
                }
                list.Clear();
            }
            else if (_savedEntries is IList saved)
            {
                list.Clear();
                foreach (var e in saved) list.Add(e);
            }

            fd?.SetValue(svc, true);
            var ev = t.GetField("OnNoticesChanged", BindingFlags.NonPublic | BindingFlags.Static)
                  ?? t.GetField("OnNoticesChanged", BindingFlags.Public | BindingFlags.Static);
            (ev?.GetValue(null) as Action)?.Invoke();
            Debug.Log($"[DailyPillBot] notices present={present} → pages={svc.Pages.Count}");
        }

        static GameObject Pill() => GameObject.Find("Canvas/ScreensRoot/HomeScreen/DailyMissionPill");

        static void Report(string phase)
        {
            var pill = Pill();
            var rt = pill != null ? (RectTransform)pill.transform : null;
            var notice = GameObject.Find("Canvas/ScreensRoot/HomeScreen/NoticePanel");
            Debug.Log($"[DailyPillBot] {phase}: pill.ap={(rt != null ? rt.anchoredPosition.ToString() : "n/a")} " +
                      $"notice={(notice != null && notice.activeInHierarchy)} " +
                      $"state(streak={DailyMissionState.Streak} claimed={DailyMissionState.Claimed} " +
                      $"hasRecipe={DailyMissionState.HasRecipe} show={DailyMissionState.ShouldShowPill})");
        }

        /// <summary>
        /// One frame, written straight into the task's screenshots folder.
        ///
        /// ⚠️ IT MUST BE THE END-OF-FRAME FORM, NOT `SnapPlayModeSafe`. The synchronous one calls
        /// `ScreenCapture.CaptureScreenshotAsTexture()` mid-Update, where there is no composited
        /// frame yet — it returned null and wrote NOTHING for all 13 stills on the first run while
        /// still handing back a path (memory `reference_snapplaymodesafe_phantom_path`).
        /// `SnapAtEndOfFrameAndPause` yields `WaitForEndOfFrame` first and reads the Game View RT,
        /// and `skipPause: true` keeps this coroutine alive.
        /// </summary>
        IEnumerator Snap(string label)
        {
            if (_video) yield break;   // never snap mid-recording — that is the y-flip trigger
            // Let the Game View's render texture catch up. CaptureCore reads that RT, and one
            // end-of-frame yield was not always enough: several frames came back byte-identical
            // to the PREVIOUS capture (the rollover "before" frame showed no pill even though the
            // log had it settled at rest that same instant).
            for (int i = 0; i < 3; i++) yield return null;
            string dest = $"{TaskDir}/screenshots/{label}.png";
            yield return Golfin.Diagnostics.Runtime.CaptureCore.SnapAtEndOfFrameAndPause(
                label, dest, skipPause: true);
            if (!File.Exists(dest)) { Debug.LogError($"[DailyPillBot] SNAP FAILED for '{label}' — no file at '{dest}'."); yield break; }
            var fi = new FileInfo(dest);
            if (fi.Length < 5000) { Debug.LogError($"[DailyPillBot] SNAP SUSPECT for '{label}' — only {fi.Length} bytes."); yield break; }
            Debug.Log($"[DailyPillBot] snapped → {dest} ({fi.Length} bytes)");
        }

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

        IEnumerator Sequence()
        {
            // ── Phase 0: boot through the REAL gate ───────────────────────────
            Golfin.UI.PersistentUIManager puim = null;
            float waited = 0f; bool tappedStart = false;
            while (waited < 45f)
            {
                puim = FindActive<Golfin.UI.PersistentUIManager>();
                if (puim != null && puim.inventoryButton != null && puim.inventoryButton.gameObject.activeInHierarchy) break;
                if (!tappedStart)
                {
                    var splash = FindActive<GolfinRedux.UI.SplashScreenController>();
                    var startTf = splash != null ? splash.transform.Find("StartButton") : null;
                    var startBtn = startTf != null ? startTf.GetComponent<Button>() : null;
                    if (startBtn != null && startBtn.gameObject.activeInHierarchy)
                    {
                        startBtn.onClick.Invoke(); tappedStart = true;
                        Debug.Log("[DailyPillBot] tapped the real StartButton.");
                    }
                }
                waited += Time.unscaledDeltaTime; yield return null;
            }
            if (puim == null)
            {
                Debug.LogError("[DailyPillBot] BOOT GATE — never reached Home. Sign in once in the Editor and re-run.");
                yield break;
            }
            yield return Wait(3f);

            // What the LIVE fetch actually said, before anything is seeded over it.
            Debug.Log($"[DailyPillBot] LIVE FETCH: known={DailyMissionState.Known} date='{DailyMissionState.Date}' " +
                      $"streak={DailyMissionState.Streak} claimed={DailyMissionState.Claimed} " +
                      $"hasRecipe={DailyMissionState.HasRecipe} → showPill={DailyMissionState.ShouldShowPill}");
            Report("after live fetch"); Mark("Live fetch: today's daily is already CLAIMED\\nso no pill — the real prod answer");
            yield return Snap("home_live_fetch_claimed_no_pill");

            string today = Golfin.UI.Home.DailyMissionPillController.UtcToday();

            // ── 1. Notice SHOWN + streak 5 (Figma 2098:8490) ─────────────────
            SetNoticesPresent(true);
            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(1.2f);
            Report("notice shown, streak 5"); Mark("Unclaimed daily, streak 5 — the pill slides in\\nfrom the left, 24px under the notice"); yield return Snap("home_notice_streak5_en");

            // Mid-enter frame, to show the slide is real and not a pop.
            DailyMissionState.Set(today, 5, claimed: true, hasRecipe: true);
            yield return Wait(0.6f);
            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(0.18f);
            Report("mid-enter"); yield return Snap("home_mid_enter");
            yield return Wait(1.0f);

            // ── 2. Notice HIDDEN + streak 5 (Figma 13994:1935) ───────────────
            SetNoticesPresent(false);
            yield return Wait(1.0f);
            Report("notice hidden, streak 5"); Mark("Notice cleared live — the pill follows it up\\nto y 361, no relaunch"); yield return Snap("home_nonotice_streak5_en");

            // ── 3. Streak 0 — no flame, and the label keeps its x ────────────
            DailyMissionState.Set(today, 0, claimed: false, hasRecipe: true);
            yield return Wait(0.8f);
            Report("streak 0"); Mark("Streak 0 — no flame, and the pill SHORTENS\\nto 481 so it does not end in empty navy"); yield return Snap("home_nonotice_streak0_en");

            // ── 4. Streak 12 — two digits, auto-size ─────────────────────────
            DailyMissionState.Set(today, 12, claimed: false, hasRecipe: true);
            yield return Wait(0.8f);
            Report("streak 12"); Mark("Streak 12 — two digits auto-size into the flame"); yield return Snap("home_nonotice_streak12_en");

            // ── 5. Japanese ──────────────────────────────────────────────────
            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            LocalizationManager.SetLanguage(Language.Japanese);
            yield return Wait(1.0f);
            Report("JA"); Mark("日本語 — HOME_DAILY_PILL from the texts catalog"); yield return Snap("home_nonotice_streak5_ja");
            LocalizationManager.SetLanguage(Language.English);
            yield return Wait(0.6f);

            // ── 6. Claim → the pill leaves ───────────────────────────────────
            SetNoticesPresent(true);
            yield return Wait(0.6f);
            DailyMissionState.MarkClaimed(6);
            yield return Wait(0.15f);
            Report("mid-leave"); Mark("Daily claimed -> the pill leaves"); yield return Snap("home_mid_leave_on_claim");
            yield return Wait(0.6f);
            Report("after claim"); yield return Snap("home_after_claim_no_pill");

            // ── 7. UTC rollover — old out, fetch, new in ─────────────────────
            // Bring yesterday's pill up FIRST, settled, and only then move the clock. Seeding a
            // stale date through Set() lost the "before" frame: the 1s tick fired against the
            // mismatch immediately and the whole leave+refetch was over inside one second.
            DailyMissionState.Set(today, 3, claimed: false, hasRecipe: true);
            yield return Wait(1.4f);
            Report("rollover: yesterday's pill is up"); Mark("UTC rollover: yesterday's pill is up..."); yield return Snap("home_rollover_old_shown");
            // `Date` written DIRECTLY is the seam the spec names: no event, so the pill stays up
            // until its own tick notices midnight has passed.
            DailyMissionState.Date = "2020-01-01";
            // The tick fires within 1s of the date mismatch; catch the old pill on its way out.
            float t0 = 0f;
            var prt0 = Pill() != null ? (RectTransform)Pill().transform : null;
            while (t0 < 2.5f && prt0 != null && prt0.anchoredPosition.x > -300f) { t0 += Time.unscaledDeltaTime; yield return null; }
            Report("rollover: old pill leaving"); Mark("...midnight passes, the old pill leaves,\\nthe client re-fetches"); yield return Snap("home_rollover_old_leaving");

            // The re-fetch after the rollover is REAL and its answer is this account's real
            // state — which is `claimed` (the daily was already played today), so no new pill.
            // That is the correct outcome for THIS account, not the "new day, new pill" case, so
            // the new-pill half is seeded straight after with what an unclaimed day returns.
            yield return Wait(4f);
            Report("rollover: after the real re-fetch (this account HAS claimed today)");
            yield return Snap("home_rollover_after_real_refetch");
            DailyMissionState.Set(today, 4, claimed: false, hasRecipe: true);
            yield return Wait(1.2f);
            Report("rollover: new pill in, new streak"); Mark("...and the new day's pill enters with the new streak"); yield return Snap("home_rollover_new_pill_in");

            // ── 8. Mission Selection — the SAME flame on the daily card ──────
            DailyMissionState.Set(today, 7, claimed: false, hasRecipe: true);
            yield return Wait(0.8f);
            var pillBtn = Pill() != null ? Pill().GetComponent<Button>() : null;
            if (pillBtn != null)
            {
                pillBtn.onClick.Invoke();   // the REAL widget's onClick — never ShowScreen directly
                Debug.Log("[DailyPillBot] tapped the real DailyMissionPill Button.onClick.");
                Mark("Tapping the pill's REAL Button.onClick\\n-> Mission Selection, daily card expanded");
            }
            else Debug.LogError("[DailyPillBot] pill Button not found — tap not exercised.");
            yield return Wait(4f);
            Debug.Log($"[DailyPillBot] after tap: screen={GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen}");
            yield return Snap("missionselection_after_pill_tap");

            // Back to Home through the REAL nav button: an already-announced pill must be there
            // at rest on arrival, with no second entrance to sit through.
            var puimNav = FindActive<Golfin.UI.PersistentUIManager>();
            if (puimNav != null && puimNav.homeButton != null)
            {
                // Mission Selection's fetch just wrote the SERVER's answer over the seed, and on
                // this account today's daily really is claimed — so re-seed unclaimed, or the
                // frame shows a correctly-absent pill and proves nothing about re-entry.
                DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
                puimNav.homeButton.onClick.Invoke();
                // Short: Home's own fetch will re-assert `claimed` a moment later, and the claim
                // being made here is about the frames right after the screen appears.
                yield return Wait(0.35f);
                Report("back on Home — no re-slide");
                Mark("Back from Missions — the pill is just THERE.\nThe slide is an announcement, not a transition");
                yield return Snap("home_reentry_no_slide");
                // ...and a NEW daily still announces itself.
                DailyMissionState.Set("2099-01-02", 2, claimed: false, hasRecipe: true);
                yield return Wait(0.22f);
                Report("new daily — the slide runs again");
                Mark("A NEW daily still slides in");
                yield return Snap("home_new_daily_slides_again");
                DailyMissionState.Set(today, 7, claimed: false, hasRecipe: true);
                yield return Wait(1.0f);
                // Return to Missions for the card shots below.
                var pillBtn2 = Pill() != null ? Pill().GetComponent<Button>() : null;
                if (pillBtn2 != null) pillBtn2.onClick.Invoke();
                yield return Wait(3.5f);
            }

            var card = GameObject.Find("Canvas/ScreensRoot/MissionSelectionScreen/Content/DailyMissionCard");
            if (card != null)
            {
                var mcc = card.GetComponent<GolfinRedux.UI.MissionSelection.MissionCardController>();
                mcc.SetDailyStatus(TimeSpan.FromHours(7.5), 7, claimed: false);
                yield return Wait(0.8f);
                yield return Snap("missioncard_collapsed_flame");

                var tap = card.transform.Find("CardTapButton")?.GetComponent<Button>();
                if (tap != null) tap.onClick.Invoke();
                yield return Wait(1.2f);
                Mark("The SAME StreakFlame prefab on the daily card");
                yield return Snap("missioncard_expanded_flame");
            }
            else Debug.LogError("[DailyPillBot] DailyMissionCard not found.");

            yield return Wait(1f);
            WriteCaptions();
            Debug.Log("[DailyPillBot] DONE.");
            if (_video) EditorApplication.isPlaying = false;   // closes the mp4 cleanly
        }
    }

    /// <summary>
    /// The deterministic pass/fail gate for the pill. Writes
    /// `Docs/Specs/Completed/daily_mission_home_pill/pill_invariants.json`.
    /// </summary>
    public class DailyMissionPillVerifier : MonoBehaviour
    {
        const string TaskDir = "Docs/Specs/Completed/daily_mission_home_pill";
        readonly List<string> _rows = new List<string>();
        int _fail;

        public void Begin() => StartCoroutine(Run());

        void Assert(string name, bool ok, string detail)
        {
            if (!ok) _fail++;
            _rows.Add($"    {{\"assert\": \"{name}\", \"result\": \"{(ok ? "PASS" : "FAIL")}\", \"detail\": \"{detail.Replace("\"", "\\\"")}\"}}");
            Debug.Log($"[DailyPillVerify] {(ok ? "PASS" : "FAIL")} {name}: {detail}");
        }

        static T FindActive<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(c => c != null
                && !string.IsNullOrEmpty(c.gameObject.scene.name) && c.gameObject.activeInHierarchy);

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

        IEnumerator Run()
        {
            Golfin.UI.PersistentUIManager puim = null;
            float waited = 0f; bool tapped = false;
            while (waited < 45f)
            {
                puim = FindActive<Golfin.UI.PersistentUIManager>();
                if (puim != null && puim.inventoryButton != null && puim.inventoryButton.gameObject.activeInHierarchy) break;
                if (!tapped)
                {
                    var splash = FindActive<GolfinRedux.UI.SplashScreenController>();
                    var b = splash != null ? splash.transform.Find("StartButton")?.GetComponent<Button>() : null;
                    if (b != null && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); tapped = true; }
                }
                waited += Time.unscaledDeltaTime; yield return null;
            }
            if (puim == null) { Debug.LogError("[DailyPillVerify] BOOT GATE — never reached Home."); yield break; }
            yield return Wait(3f);

            var pill = GameObject.Find("Canvas/ScreensRoot/HomeScreen/DailyMissionPill");
            Assert("pill_present", pill != null, pill != null ? "found under HomeScreen" : "MISSING");
            if (pill == null) { Write(); yield break; }
            var prt  = (RectTransform)pill.transform;
            var ctrl = pill.GetComponent<Golfin.UI.Home.DailyMissionPillController>();
            var notice = GameObject.Find("Canvas/ScreensRoot/HomeScreen/NoticePanel");
            var nrt = (RectTransform)notice.transform;
            var labelRt = (RectTransform)pill.transform.Find("Label");
            string today = Golfin.UI.Home.DailyMissionPillController.UtcToday();

            // ── Geometry, both Figma frames ─────────────────────────────────
            Assert("pill_size_549x122", Mathf.Approximately(prt.rect.width, 549f) && Mathf.Approximately(prt.rect.height, 122f),
                   $"rect={prt.rect.size} (node 549x122)");

            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(1.2f);
            Assert("rest_x_36", Mathf.Abs(prt.anchoredPosition.x - 36f) < 0.5f, $"x={prt.anchoredPosition.x:F2} (node 36)");
            float expectedWith = nrt.anchoredPosition.y - nrt.rect.height - 24f;
            Assert("y_with_notice_is_24_under_it", notice.activeInHierarchy && Mathf.Abs(prt.anchoredPosition.y - expectedWith) < 0.5f,
                   $"y={prt.anchoredPosition.y:F1} expected={expectedWith:F1} (noticeTop={nrt.anchoredPosition.y:F0} h={nrt.rect.height:F0}); Figma 725 for a 340-tall notice block");

            // Hide the notice through its own chain, then re-check.
            var svc = Golfin.Notices.NoticeService.Instance;
            var fe = svc.GetType().GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = fe.GetValue(svc) as IList; var saved = new List<object>(); foreach (var e in list) saved.Add(e);
            list.Clear();
            svc.GetType().GetField("_pagesDirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(svc, true);
            var ev = svc.GetType().GetField("OnNoticesChanged", BindingFlags.NonPublic | BindingFlags.Static)
                  ?? svc.GetType().GetField("OnNoticesChanged", BindingFlags.Public | BindingFlags.Static);
            (ev.GetValue(null) as Action)?.Invoke();
            yield return Wait(0.6f);
            Assert("y_no_notice_is_361", !notice.activeInHierarchy && Mathf.Abs(prt.anchoredPosition.y + 361f) < 0.5f,
                   $"y={prt.anchoredPosition.y:F1} (Figma 13994:1935 -> -361)");

            // ── The pill hugs its content: no dead space when there is no streak ──
            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(0.9f);
            float wWith = prt.rect.width;
            float lxWith = labelRt != null ? labelRt.anchoredPosition.x : -1f;
            DailyMissionState.Set(today, 0, claimed: false, hasRecipe: true);
            yield return Wait(0.9f);
            float wNo = prt.rect.width;
            float lxNo = labelRt != null ? labelRt.anchoredPosition.x : -1f;
            Assert("pill_width_549_with_flame", Mathf.Abs(wWith - 549f) < 0.5f && Mathf.Abs(lxWith - 92f) < 0.5f,
                   $"width={wWith:F1} labelX={lxWith:F1} (node 549 / 92)");
            Assert("pill_width_481_without_flame", Mathf.Abs(wNo - 481f) < 0.5f && Mathf.Abs(lxNo - 24f) < 0.5f,
                   $"width={wNo:F1} labelX={lxNo:F1} (24+433+24 = 481, label at the 24px pad) — the flame's 58+10 is removed, not left empty");
            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(0.9f);

            // ── The glow must not allocate per frame ────────────────────────
            //
            // Measured as a DIFFERENCE, not an absolute. A whole-screen total is dominated by
            // whatever else Home is doing — it read 68 B/frame on a warm editor and 22 550 on a
            // freshly restarted one, which says nothing about the glow either time. Sampling the
            // same screen with the pill Shown (glow loop running) against Hidden (same Update,
            // glow branch skipped) isolates exactly the code under test.
            long allocShown = 0, allocHidden = 0;
            const int AllocFrames = 180;

            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(1.0f);
            System.GC.Collect(); yield return null;
            long b1 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            for (int i = 0; i < AllocFrames; i++) yield return null;
            allocShown = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - b1;
            bool wasShowing = ctrl.IsShowing;

            DailyMissionState.Set(today, 5, claimed: true, hasRecipe: true);
            yield return Wait(1.0f);
            System.GC.Collect(); yield return null;
            long b2 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            for (int i = 0; i < AllocFrames; i++) yield return null;
            allocHidden = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - b2;

            long glowPerFrame = (allocShown - allocHidden) / AllocFrames;
            Assert("glow_loop_no_per_frame_alloc", glowPerFrame < 512,
                   $"shown {allocShown} B vs hidden {allocHidden} B over {AllocFrames} frames each -> the glow loop's own share is {glowPerFrame} B/frame (Shown={wasShowing}); the absolute totals are whole-screen noise and are not the gate");

            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(1.0f);

            // ── The real tap fires the telemetry event ─────────────────────
            var tel = Golfin.Telemetry.TelemetryService.Instance;
            var qf = tel.GetType().GetField("_queue", BindingFlags.NonPublic | BindingFlags.Instance);
            int qBefore = tel.QueuedCount;
            pill.GetComponent<Button>().onClick.Invoke();
            yield return Wait(1.5f);
            var q = qf.GetValue(tel) as IList;
            bool found = false; string payload = "";
            if (q != null)
                foreach (var e in q)
                {
                    var nameF = e.GetType().GetField("Name");
                    if (nameF != null && (string)nameF.GetValue(e) == Golfin.Telemetry.TelemetryEventNames.DailyPillTap)
                    { found = true; payload = "queued"; }
                }
            Assert("daily_pill_tap_queued", found,
                   found ? $"'{Golfin.Telemetry.TelemetryEventNames.DailyPillTap}' is in TelemetryService._queue ({payload}); queue {qBefore}->{tel.QueuedCount}"
                         : $"NOT in the queue; queue {qBefore}->{tel.QueuedCount} (TelemetryConfig.Enabled={Golfin.Telemetry.TelemetryConfig.Enabled})");
            Assert("tap_opens_mission_selection",
                   GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen == GolfinRedux.UI.ScreenId.MissionSelection,
                   $"screen={GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen}");

            // ── The pill's tap must land on the daily ALREADY OPEN ─────────
            var dailyCardGo = GameObject.Find("Canvas/ScreensRoot/MissionSelectionScreen/Content/DailyMissionCard");
            var dailyCtrl = dailyCardGo != null
                ? dailyCardGo.GetComponent<GolfinRedux.UI.MissionSelection.MissionCardController>() : null;
            float waitExp = 0f;
            while (waitExp < 6f && (dailyCtrl == null || dailyCtrl.State != GolfinRedux.UI.MissionSelection.MissionCardState.Expanded))
            { waitExp += Time.unscaledDeltaTime; yield return null; }
            Assert("pill_tap_expands_daily",
                   dailyCtrl != null && dailyCtrl.State == GolfinRedux.UI.MissionSelection.MissionCardState.Expanded,
                   $"dailyCard.State={dailyCtrl?.State} after {waitExp:F1}s; the request flag is consumed on bind, and it is now {GolfinRedux.UI.MissionSelection.MissionSelectionScreenController.ExpandDailyOnOpen}");
            Assert("expand_request_consumed",
                   !GolfinRedux.UI.MissionSelection.MissionSelectionScreenController.ExpandDailyOnOpen,
                   "ExpandDailyOnOpen is false again, so every other route into Missions still lands on NEXT");

            // ── The streak badge rides the TITLE row, so it is in both states ─
            string ParentOf(string path)
            {
                var t = dailyCardGo != null ? dailyCardGo.transform.Find(path) : null;
                return t != null ? t.parent.name : "<missing>";
            }
            string pc = ParentOf("CollapsedContainer/TitleArea/TitleHRow/DailyStreak");
            string pe = ParentOf("ExpandedContainer/TitleAreaExp/TitleHRowExp/DailyStreakExp");
            Assert("streak_badge_beside_title", pc == "TitleHRow" && pe == "TitleHRowExp",
                   $"collapsed parent='{pc}', expanded parent='{pe}' — both are the title row, so the badge shows in both states");

            // ── Re-entering Home must NOT re-play the slide ─────────────────
            // We are on Mission Selection. Go back through the REAL nav-bar Home button and
            // sample the pill's x EVERY FRAME: an announced pill must already be at rest on the
            // first frame Home draws, never off-screen and never mid-slide.
            var home = puim.homeButton;
            if (home == null) Assert("reentry_no_slide", false, "PersistentUIManager.homeButton is null — could not drive the real route back");
            else
            {
                // Mission Selection's own fetch just overwrote the state with the SERVER's answer,
                // and on this account today's daily is genuinely claimed — so re-seed unclaimed,
                // or the pill is correctly absent and the test measures nothing.
                DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
                home.onClick.Invoke();
                float minX = float.MaxValue, maxX = float.MinValue;
                int sampled = 0; float t = 0f;
                // Sample from the first frame Home draws until Home's OWN fetch lands and has its
                // say. That window is the claim: the pill is at rest the moment the screen
                // appears, rather than sliding in again.
                while (t < 1.6f && !DailyMissionState.Claimed)
                {
                    if (pill.activeInHierarchy)
                    {
                        minX = Mathf.Min(minX, prt.anchoredPosition.x);
                        maxX = Mathf.Max(maxX, prt.anchoredPosition.x);
                        sampled++;
                    }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                bool pinned = sampled >= 3 && Mathf.Abs(minX - 36f) < 0.5f && Mathf.Abs(maxX - 36f) < 0.5f;
                Assert("reentry_no_slide", pinned,
                       $"back on Home via the real nav button: x stayed in [{minX:F1}, {maxX:F1}] across {sampled} frames — rest is 36, off-screen would be {-(prt.rect.width + 36f):F0}; AnnouncedForDate='{Golfin.UI.Home.DailyMissionPillController.AnnouncedForDate}'");
                Assert("reentry_screen_is_home",
                       GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen == GolfinRedux.UI.ScreenId.Home,
                       $"screen={GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen}");
            }

            // ── A NEW daily still announces itself ──────────────────────────
            // Same pill, different date: the announcement is owed again, so the slide must run.
            DailyMissionState.Set("2099-01-02", 2, claimed: false, hasRecipe: true);
            float slideMin = float.MaxValue; float t2 = 0f;
            while (t2 < 1.2f) { slideMin = Mathf.Min(slideMin, prt.anchoredPosition.x); t2 += Time.unscaledDeltaTime; yield return null; }
            Assert("new_daily_still_slides", slideMin < -100f,
                   $"a new date drove x down to {slideMin:F1} before settling — the slide ran; AnnouncedForDate='{Golfin.UI.Home.DailyMissionPillController.AnnouncedForDate}'");
            DailyMissionState.Set(today, 5, claimed: false, hasRecipe: true);
            yield return Wait(1.0f);

            // ── The card's flame is the SAME prefab as the pill's ───────────
            // Read in EDIT mode before entering play — PrefabUtility answers "" for a running
            // instance, which is what made this row a false FAIL on the first verify run.
            var prov = SessionState.GetString("DailyPillDemo.Prov", "").Split('|');
            string pillSrc    = prov.Length > 0 ? prov[0] : "";
            string cardSrc    = prov.Length > 1 ? prov[1] : "";
            string cardExpSrc = prov.Length > 2 ? prov[2] : "";
            Assert("streakflame_shared_prefab",
                   pillSrc.EndsWith("StreakFlame.prefab") && pillSrc == cardSrc && pillSrc == cardExpSrc,
                   $"pill='{pillSrc}' cardCollapsed='{cardSrc}' cardExpanded='{cardExpSrc}'");

            // ── Zero streak hides the badge, on both surfaces ───────────────
            var flame = pill.transform.Find("StreakFlame").GetComponent<Golfin.UI.Common.StreakFlameView>();
            flame.SetStreak(0); yield return null;
            bool hid = !flame.gameObject.activeSelf;
            flame.SetStreak(9); yield return null;
            Assert("streak_zero_hides_flame", hid && flame.gameObject.activeSelf, $"hiddenAt0={hid}, shownAt9={flame.gameObject.activeSelf}");

            // restore the notices we cleared
            list.Clear(); foreach (var e in saved) list.Add(e);
            svc.GetType().GetField("_pagesDirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(svc, true);
            (ev.GetValue(null) as Action)?.Invoke();

            Write();
            yield return Wait(0.5f);
            EditorApplication.isPlaying = false;
        }

        void Write()
        {
            Directory.CreateDirectory(TaskDir);
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n  \"task\": \"daily_mission_home_pill\",\n  \"fail\": ").Append(_fail)
              .Append(",\n  \"assertions\": [\n").Append(string.Join(",\n", _rows)).Append("\n  ]\n}\n");
            File.WriteAllText($"{TaskDir}/pill_invariants.json", sb.ToString());
            Debug.Log($"[DailyPillVerify] {_rows.Count} assertions, {_fail} FAIL -> {TaskDir}/pill_invariants.json");
        }
    }
}
#endif
