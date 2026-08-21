#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Inventory;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Demo recorder for the 799-row club roster (club_roster_799).
    /// Produces a single ~40s MP4 at full iPhone-14 1170x2532.
    ///
    /// Proves, in one real-entry pass:
    ///   · the fresh-save starter bag is the 7 Common GOLFIN clubs
    ///   · the type filter bar reaches every type, S.Wedge included (unified WEDGES tab)
    ///   · Placeholder art fallback — cards render, none blank, while art batches land
    ///   · the info_ja ladder BOTH ways: a GOLFIN club shows JA copy in Japanese, and a
    ///     legacy club (blank info_ja) falls back to English for a Japanese player
    ///
    /// Everything is driven through the REAL widgets' onClick — nav bar, inventory tabs and
    /// filter buttons are all reflected out and invoked, never bypassed with ShowScreen/ShowTab.
    ///
    /// ⚠️ TWO THINGS TO KNOW BEFORE RUNNING THIS
    ///
    /// 1. <b>It needs a SIGNED-IN editor.</b> ShellScene boots Logo → Splash → Home only when a
    ///    session is present; the session lives in <b>PlayerPrefs</b> (see AuthSession), NOT in
    ///    save.json, and there is no guest mode (Cesar, 2026-08-12). Signed out, the app parks on
    ///    the Splash LOGIN/CREATE ACCOUNT gate, the nav bar never exists, and every widget lookup
    ///    returns null. The bot polls for the nav bar and ABORTS with a BOOT GATE error rather than
    ///    recording the wrong screen — if you see that error, sign in once in the Editor and re-run.
    ///
    /// 2. <b>It mutates the save.</b> The GOLFIN grants below go through ClubManager.GrantClub,
    ///    which persists. Back up
    ///    ~/Library/Application Support/NEXT INNOVATION PTE_ LTD_/Golfin/save.json first and restore
    ///    it after, or run it on a throwaway profile.
    ///
    /// Output: Docs/Specs/Active/pagination_dots/videos/raw.mp4
    /// Usage:  GOLFIN > Inventory > Record Club Roster Demo Video
    /// </summary>
    public static class PaginationDotsDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/pagination_dots/videos";
        const string ArmedKey       = "PaginationDotsDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Inventory/Record Pagination Dots Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[PaginationDotsDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[PaginationDotsDemo] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // The STOP path must NOT be gated on ArmedKey. Arming is cleared the moment we enter play
            // mode, so gating both branches on it means ExitingPlayMode returns early and
            // StopRecorder never runs — the recorder keeps its file open and the mp4 is written
            // without a moov atom ("moov atom not found", unplayable). Short clips survived because
            // the RecordingSession happened to flush on teardown; a 45s one did not.
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
                var asm = Assembly.Load("Golfin.Physics.Viewer.Bot.Editor");
                var t   = asm?.GetType("Golfin.Physics.Viewer.Editor.GameViewSizeUtil");
                var m   = t?.GetMethod("EnsureIPhone14Selected", BindingFlags.Public | BindingFlags.Static);
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
                    Debug.LogWarning($"[PaginationDotsDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "PaginationDotsDemo";
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
            Debug.Log($"[PaginationDotsDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[ClubRosterDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<PaginationDotsDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder != null)
            {
                try
                {
                    if (_recorder.IsRecording()) _recorder.StopRecording();
                    Debug.Log("[PaginationDotsDemo] Recording stopped.");
                }
                catch (Exception e) { Debug.LogWarning($"[PaginationDotsDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    public class PaginationDotsDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        static T FindActive<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null
                    && !string.IsNullOrEmpty(c.gameObject.scene.name)
                    && c.gameObject.activeInHierarchy);

        /// <summary>Clicks a real Button out of a private Button[] field — never bypasses the handler.</summary>
        static bool ClickRealButton(Component owner, string privateField, int index, string label)
        {
            if (owner == null) { Debug.LogWarning($"[PaginationDotsBot] {label}: owner missing."); return false; }
            var f = owner.GetType().GetField(privateField, BindingFlags.NonPublic | BindingFlags.Instance)
                 ?? owner.GetType().GetField(privateField, BindingFlags.Public | BindingFlags.Instance);
            var arr = f?.GetValue(owner) as Button[];
            if (arr == null || index >= arr.Length || arr[index] == null)
            {
                Debug.LogWarning($"[PaginationDotsBot] {label}: {privateField}[{index}] not found.");
                return false;
            }
            arr[index].onClick.Invoke();
            Debug.Log($"[PaginationDotsBot] {label} — clicked real {privateField}[{index}].onClick.");
            return true;
        }

        static void ReportOwned(string phase)
        {
            var owned = ClubManager.Instance?.GetAllOwnedClubs();
            if (owned == null) { Debug.LogWarning($"[PaginationDotsBot] {phase}: ClubManager.Instance null."); return; }
            Debug.Log($"[PaginationDotsBot] {phase}: owns {owned.Count} clubs -> " +
                      string.Join(", ", owned.Select(c => c.clubId).OrderBy(i => i)));
        }

        static void ReportInfo(string clubId, string phase)
        {
            var t = ClubDatabaseCSV.Instance?.GetClub(clubId);
            if (t == null) { Debug.LogWarning($"[PaginationDotsBot] {phase}: '{clubId}' not in DB."); return; }
            string shown = ClubInfoText.Resolve(t);
            string rung  = (LocalizationManager.CurrentLanguage == Language.Japanese
                            && !string.IsNullOrWhiteSpace(t.infoJa)) ? "JA" : "EN-fallback";
            Debug.Log($"[PaginationDotsBot] {phase}: {clubId} lang={LocalizationManager.CurrentLanguage} " +
                      $"rung={rung} jaLen={t.infoJa.Length} shown=\"{shown.Substring(0, Mathf.Min(48, shown.Length))}…\"");
        }

        static void SelectClub(string clubId)
        {
            var carousel = FindActive<ClubCarouselController>();
            if (carousel != null) carousel.SelectClub(clubId);
            else Debug.LogWarning($"[PaginationDotsBot] ClubCarouselController not found for '{clubId}'.");
        }

        IEnumerator Sequence()
        {
            // ── Phase 0: Boot ─────────────────────────────────────────────────
            // POLL for the nav bar instead of guessing a duration. ShellScene boots through
            // Logo → Splash → (Login gate, if the session is stale) → Home, and the nav bar does
            // not exist until Home is up. A fixed wait silently recorded the login screen once.
            Golfin.UI.PersistentUIManager puim = null;
            float waited = 0f;
            bool tappedStart = false;
            while (waited < 45f)
            {
                puim = FindActive<Golfin.UI.PersistentUIManager>();
                if (puim != null && puim.inventoryButton != null
                    && puim.inventoryButton.gameObject.activeInHierarchy) break;

                // The Splash "tap to start" gate. ScreenManager does NOT drive past it — even with a
                // valid session the player must press StartButton, which is what runs the token
                // refresh and routes to Home. Without this the bot sat on Splash until it timed out.
                if (!tappedStart)
                {
                    var splash = FindActive<GolfinRedux.UI.SplashScreenController>();
                    var startTf = splash != null ? splash.transform.Find("StartButton") : null;
                    var startBtn = startTf != null ? startTf.GetComponent<Button>() : null;
                    if (startBtn != null && startBtn.gameObject.activeInHierarchy)
                    {
                        Debug.Log("[PaginationDotsBot] Splash gate — tapping real StartButton.onClick.");
                        startBtn.onClick.Invoke();
                        tappedStart = true;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
                waited += 0.25f;
            }
            if (puim == null || puim.inventoryButton == null
                || !puim.inventoryButton.gameObject.activeInHierarchy)
            {
                Debug.LogError($"[PaginationDotsBot] BOOT GATE: nav bar never appeared after {waited:F1}s — " +
                               "the app is parked on the Logo/Splash/Login gate. Aborting; the clip would " +
                               "have recorded the wrong screen.");
                EditorApplication.ExitPlaymode();
                yield break;
            }
            Debug.Log($"[PaginationDotsBot] Boot complete after {waited:F1}s — nav bar is live.");
            LocalizationManager.SetLanguage(Language.English);
            ReportOwned("SAVE AS LOADED");
            yield return new WaitForSecondsRealtime(1.2f);

            // ── Clubs: 799 owned = 134 pages, previously 134 dots in a 48-dot row ──
            Debug.Log("[PaginationDotsBot] Tapping Inventory nav button (real entry).");
            puim.inventoryButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.2f);

            var inv = FindActive<InventoryScreenController>();
            ClickRealButton(inv, "tabButtons", 0, "CLUBS tab");
            yield return new WaitForSecondsRealtime(2.4f);

            var car = FindActive<ClubCarouselController>();
            var carT = typeof(ClubCarouselController);
            var rightArrow = carT.GetField("rightArrowButton", BindingFlags.NonPublic | BindingFlags.Instance)
                                 .GetValue(car) as UnityEngine.UI.Button;
            var goTo = carT.GetMethod("GoToPage", BindingFlags.NonPublic | BindingFlags.Instance);
            var pageF = carT.GetField("currentPage", BindingFlags.NonPublic | BindingFlags.Instance);
            var totalF = carT.GetField("totalPages", BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Log($"[PaginationDotsBot] Clubs: totalPages={totalF.GetValue(car)} — the row now shows at most 7 dots.");

            // Step the real arrow so the window visibly slides and the active dot centres.
            for (int i = 0; i < 9; i++)
            {
                rightArrow.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.45f);
            }
            Debug.Log($"[PaginationDotsBot] after arrow steps page={pageF.GetValue(car)}");
            yield return new WaitForSecondsRealtime(0.8f);

            // Middle of the range: both edge dots shrink.
            goTo.Invoke(car, new object[] { 66 });
            yield return new WaitForSecondsRealtime(2.2f);
            // Far end: window clamps, active dot sits at the right.
            goTo.Invoke(car, new object[] { 133 });
            yield return new WaitForSecondsRealtime(2.2f);
            goTo.Invoke(car, new object[] { 0 });
            yield return new WaitForSecondsRealtime(1.2f);

            // ── Filters recompute the page count; the row never grows ──
            var bar = FindActive<ClubFilterBar>();
            foreach (var (idx, name, hold) in new[]
                     { (1, "DRIVERS", 1.6f), (4, "WEDGES", 1.6f), (5, "PUTTERS", 1.6f), (0, "ALL", 1.6f) })
            {
                ClickRealButton(bar, "filterButtons", idx, $"filter {name}");
                yield return new WaitForSecondsRealtime(hold);
            }

            // ── Home notices: the opposite bug — dots that could not grow past 3 ──
            Debug.Log("[PaginationDotsBot] Back to Home for the notice dots.");
            puim.homeButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3.5f);

            Debug.Log("[PaginationDotsBot] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
