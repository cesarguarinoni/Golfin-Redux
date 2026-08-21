#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Tournaments;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Showcase recorder for the tournament entry-refusal pop-up (Figma 13915:2273,
    /// `tournament_entry_denied_modal`). One clip, every refusal state, at full
    /// iPhone-14 1170×2532.
    ///
    /// <para><b>What is REAL and what is DRIVEN.</b> Phase A is the whole point and is
    /// end-to-end real: a Common character is selected, the restricted tournament's
    /// sign-up modal is opened, and the player's own CONFIRM button is clicked — the
    /// pop-up that appears is the production gate refusing a production entry.</para>
    ///
    /// <para>Phase B covers the states a client cannot reach on its own: the server's
    /// `full` and `ineligible` denials, an offline entry attempt, a short balance, and a
    /// tournament that vanished mid-modal. Those are DRIVEN — the recorder calls the same
    /// <c>ShowDenied</c> the network callback calls, with the same composed body — because
    /// producing them for real needs a full field, a forced socket failure and a drained
    /// wallet. The caption on each frame says which is which; nothing here is presented as
    /// a real refusal that was not one.</para>
    ///
    /// Modelled on TournamentBannerDemoRecorder — same RecorderController setup, same
    /// FindButton helper — rather than hand-rolling a capture path.
    ///
    /// Output: Docs/Specs/Active/tournament_entry_denied_modal/videos/raw.mp4
    /// Usage:  GOLFIN > Tournaments > Record Entry-Denied Demo Video
    /// </summary>
    public static class TournamentDeniedDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/tournament_entry_denied_modal/videos";
        const string ArmedKey       = "TournamentDeniedDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Tournaments/Record Entry-Denied Demo Video")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[DeniedDemo] Already in play mode — stop first.");
                return;
            }

            // Deliberately NOT SaveCurrentModifiedScenesIfUserWantsTo(): that puts a modal dialog
            // in front of an unattended run, and saving ShellScene bakes layout churn. The scene's
            // dirty flag is cleared instead — nothing is written and nothing is reverted, and the
            // prefab edits this clip demonstrates live in the PREFAB, not the scene.
            ClearSceneDirtiness();

            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[DeniedDemo] Armed. Entering play mode…");
        }

        static void ClearSceneDirtiness()
        {
            var m = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) return;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isDirty) m.Invoke(null, new object[] { s });
            }
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
                    Debug.LogWarning($"[DeniedDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "TournamentDeniedDemo";
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
            Debug.Log($"[DeniedDemo] Recording → {OutputDir}/raw.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[TournamentDeniedDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<TournamentDeniedDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            if (_recorder == null) return;
            try
            {
                if (_recorder.IsRecording()) _recorder.StopRecording();
                Debug.Log("[DeniedDemo] Recording stopped.");
            }
            catch (Exception e) { Debug.LogWarning($"[DeniedDemo] StopRecorder: {e.Message}"); }
            _recorder = null;
        }
    }

    public class TournamentDeniedDemoRunner : MonoBehaviour
    {
        /// <summary>A Common character — below the floor of any Uncommon+ tournament.</summary>
        const string CommonChar = "char_james";

        const float Hold = 3.2f;

        /// <summary>
        /// Captions are stamped HERE, against real elapsed time since StartRecording(), and written
        /// as the sidecar `build_bot_video.py --mode captionsjson` consumes. Hand-authored timings
        /// were re-timed twice against clips whose length changed underneath them; a sidecar the
        /// recorder writes cannot drift from the clip it describes.
        /// </summary>
        readonly List<(float start, float end, string text)> _captions = new List<(float, float, string)>();
        float _t0;
        float Now => Time.realtimeSinceStartup - _t0;

        void Caption(float start, string text) => _captions.Add((start, Now, text));

        void WriteCaptions()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n  \"captions\": [\n");
            for (int i = 0; i < _captions.Count; i++)
            {
                var c = _captions[i];
                string esc = c.text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
                sb.Append($"    {{\"start\": {c.start:F2}, \"end\": {c.end:F2}, \"text\": \"{esc}\"}}");
                sb.Append(i < _captions.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string path = "Docs/Specs/Active/tournament_entry_denied_modal/videos/captions.json";
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"[DeniedDemo] Wrote {_captions.Count} captions → {path}");
        }

        public void StartDemo()
        {
            _t0 = Time.realtimeSinceStartup;
            StartCoroutine(Sequence());
        }

        static Button FindButton(string buttonName)
            => Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b.gameObject.name == buttonName
                    && !string.IsNullOrEmpty(b.gameObject.scene.name)
                    && b.GetComponentInParent<Canvas>() != null);

        static GolfinRedux.UI.Tournaments.TournamentSignupModalController Modal()
            => Resources.FindObjectsOfTypeAll<GolfinRedux.UI.Tournaments.TournamentSignupModalController>()
                .FirstOrDefault(m => !string.IsNullOrEmpty(m.gameObject.scene.name));

        /// <summary>The restricted tournament the A2 dashboard authored, else any restricted one,
        /// else the first — so a deleted test row degrades the clip rather than breaking it.</summary>
        static TournamentDefinition PickTournament()
        {
            var svc = TournamentService.Instance;
            if (svc?.Backend == null) return null;
            var all = svc.Backend.GetTournaments();
            return all.FirstOrDefault(d => d.Id == "restricted_test_open")
                ?? all.FirstOrDefault(d => d.HasEntryRestrictions)
                ?? all.FirstOrDefault();
        }

        static void Drive(string body, bool closeSignup = false)
        {
            var modal = Modal();
            if (modal == null) return;
            var m = modal.GetType().GetMethod("ShowDenied",
                BindingFlags.Instance | BindingFlags.NonPublic);
            m?.Invoke(modal, new object[] { body, closeSignup });
        }

        static void Dismiss()
        {
            var b = FindButton("DeniedBackButton");
            if (b != null) b.onClick.Invoke();
        }

        /// <summary>
        /// Drive to <paramref name="want"/> and keep re-asserting until the screen manager agrees or
        /// the budget runs out. Logs the screen it actually settled on either way.
        /// </summary>
        static IEnumerator Settle(GolfinRedux.UI.ScreenId want, float budget)
        {
            float t = 0f;
            var sm = GolfinRedux.UI.ScreenManager.Instance;
            while (t < budget)
            {
                if (sm != null && sm.CurrentScreen == want) break;
                sm?.ShowScreen(want);
                yield return new WaitForSecondsRealtime(0.5f);
                t += 0.5f;
                sm = GolfinRedux.UI.ScreenManager.Instance;
            }
            var landed = GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen;
            if (landed != want)
                Debug.LogWarning($"[DeniedDemo] Wanted {want} but the app is on {landed} — the clip " +
                                 "will show the modal over the wrong screen.");
            else
                Debug.Log($"[DeniedDemo] Settled on {landed}.");
            yield return new WaitForSecondsRealtime(1.2f);   // let the fade finish
        }

        IEnumerator Sequence()
        {
            float mark = Now;
            yield return new WaitForSecondsRealtime(4.5f);   // splash → loading → wherever boot lands

            // The boot routing has the last word: an unauthenticated launch lands on ScreenId.Login
            // AFTER the splash resolves, so a single ShowScreen fired at 4.5s gets overridden and the
            // modal ends up floating over the login form. Re-assert until it sticks, and log what the
            // screen ACTUALLY is so the clip is self-auditing rather than quietly wrong.
            yield return Settle(GolfinRedux.UI.ScreenId.TournamentSelection, 8.0f);
            Caption(mark, "Entry refusal is now a pop-up, not a toast\nFigma 13915:2273");

            var def = PickTournament();
            if (def == null)
            {
                Debug.LogError("[DeniedDemo] No tournaments in the schedule — aborting.");
                EditorApplication.ExitPlaymode();
                yield break;
            }
            Debug.Log($"[DeniedDemo] Using '{def.Id}' restricted={def.HasEntryRestrictions} " +
                      $"rarity={def.CharRarityMin}-{def.CharRarityMax} level={def.CharLevelMin}-{def.CharLevelMax} " +
                      $"clubCap={def.ClubRarityMax} fee={def.EntryFeeRP}");

            // A real player action: pick a Common character, which is below the floor.
            mark = Now;
            Golfin.Roster.CharacterManager.Instance?.SelectCharacter(CommonChar);
            yield return new WaitForSecondsRealtime(0.4f);

            yield return Settle(GolfinRedux.UI.ScreenId.TournamentSelection, 3.0f);

            var modal = Modal();
            modal.Open(def.Id);
            yield return new WaitForSecondsRealtime(2.0f);   // hold on the sign-up modal
            Caption(mark, $"Real prod tournament '{def.Id}'\nCommon character — below its {def.CharRarityMin} floor");

            // ═══ PHASE A — REAL: the player's own CONFIRM button refuses the entry ═══
            var confirm = FindButton("ConfirmButton");
            if (confirm == null)
            {
                Debug.LogError("[DeniedDemo] ConfirmButton not found — aborting.");
                EditorApplication.ExitPlaymode();
                yield break;
            }
            mark = Now;
            confirm.onClick.Invoke();
            yield return new WaitForSecondsRealtime(Hold + 1.0f);   // hold on the real refusal
            Caption(mark, "REAL — the player's own CONFIRM button\nNo entry created.");

            var entry = TournamentService.Instance.Backend.GetMyEntry(def.Id);
            Debug.Log($"[DeniedDemo] PHASE A real CONFIRM → entry={(entry == null ? "NONE (refused)" : "CREATED — BUG")}");

            Dismiss();
            yield return new WaitForSecondsRealtime(1.2f);

            // ═══ PHASE B — DRIVEN: the states a client cannot reach on its own ═══
            var unmetMulti = TournamentEligibility.UnmetRequirements(def, 1, 5, new List<int> { 6 });

            var driven = new (string label, string body, bool close, string caption)[]
            {
                ("several requirements", TournamentRulesText.DeniedBody(unmetMulti), false,
                 "Every unmet rule is listed, not just the first"),
                ("server: field full",   TournamentRulesText.DeniedBodyFull(def.MaxPlayers ?? 100), false,
                 "DRIVEN — server 'full': the cap it enforced"),
                ("short balance",        TournamentRulesText.DeniedBodyInsufficient(
                                             def.EntryFeeRP > 0 ? def.EntryFeeRP : 500L, 120L), false,
                 "DRIVEN — short balance: the fee AND what you hold"),
                ("offline",              TournamentRulesText.DeniedBodySimple("tourn.denied.head.offline"), false,
                 "DRIVEN — offline: entry is online-only by design"),
                ("tournament gone",      TournamentRulesText.DeniedBodySimple("tourn.denied.head.unavailable"), true,
                 "DRIVEN — tournament gone: BACK closes the sign-up modal too"),
            };

            foreach (var d in driven)
            {
                if (Modal() == null || !Modal().gameObject.activeInHierarchy) modal.Open(def.Id);
                yield return new WaitForSecondsRealtime(0.6f);

                Debug.Log($"[DeniedDemo] PHASE B driven: {d.label}");
                float dm = Now;
                Drive(d.body, d.close);
                yield return new WaitForSecondsRealtime(Hold);
                Caption(dm, d.caption);

                Dismiss();
                yield return new WaitForSecondsRealtime(1.0f);
            }

            WriteCaptions();
            Debug.Log("[DeniedDemo] Sequence done — exiting play mode.");
            yield return new WaitForSecondsRealtime(0.8f);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
