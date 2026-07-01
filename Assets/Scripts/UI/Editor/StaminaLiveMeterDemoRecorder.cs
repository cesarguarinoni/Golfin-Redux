#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Roster;
using Golfin.Core.Stamina;
using Golfin.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Continuous-recording showcase for the Stamina live-meter feature.
    /// Produces a REAL MP4 via Unity Recorder (every frame is a fresh rendered frame —
    /// NOT a slideshow of stills). Mirrors the structure of TournamentDemoRecorder exactly.
    ///
    /// Sequence (single continuous recording):
    ///   boot ShellScene → Home → tap Characters nav → Roster screen →
    ///   char already selected (stamina LOW → RED meter) →
    ///   Enable demo accel → meter climbs RED→AMBER→BLUE in real time (~12-18s),
    ///   STR/CC numbers tick up (5/25→6/25, 6/25→7/25), ghost tails shrink →
    ///   switch character (SNAP — no tween) →
    ///   disable demo accel → meter holds steady → exit.
    ///
    /// Output: Docs/Specs/Active/stamina_roster_live_meter/videos/raw_iter3.mp4
    /// Usage:  GOLFIN > Stamina > Record Live-Meter Demo (or call LaunchDemo()).
    /// </summary>
    public static class StaminaLiveMeterDemoRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutputDir      = "Docs/Specs/Active/stamina_roster_live_meter/videos";
        const string RawFileStem    = "raw_iter3";
        const string ArmedKey       = "StaminaLiveMeterDemoRecorder.Armed";

        static RecorderController _recorder;

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Stamina/Record Live-Meter Demo")]
        public static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[StaminaMeterDemo] Already in play mode — stop first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ShellScenePath);
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[StaminaMeterDemo] Armed. Entering play mode — recording will start automatically.");
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

        static void StartRecorderAndBot()
        {
            // LOCK render state BEFORE StartRecording (Y-flip fix per reference_botvideorecorder_yflip_fix)
            bool selected = TryEnsureIPhone14Selected();
            int w = 1170, h = 2532;
            if (!selected)
            {
                PlayModeWindow.GetRenderingResolution(out uint cw, out uint ch);
                if (cw > 0 && ch > 0)
                {
                    w = Mathf.Max(2, (int)cw); h = Mathf.Max(2, (int)ch);
                    if (w % 2 != 0) w--; if (h % 2 != 0) h--;
                    Debug.LogWarning($"[StaminaMeterDemo] Could not pin iPhone-14 — recording at {w}x{h}.");
                }
            }

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name         = "StaminaMeterDemo";
            movie.Enabled      = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = w, OutputHeight = h };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = $"{OutputDir}/{RawFileStem}";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Variable;

            _recorder = new RecorderController(settings);
            _recorder.PrepareRecording();
            _recorder.StartRecording();
            Debug.Log($"[StaminaMeterDemo] Recording started → {OutputDir}/{RawFileStem}.mp4 ({w}x{h} @ 30fps)");

            var host = new GameObject("[StaminaMeterDemoBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<StaminaMeterDemoRunner>().StartDemo();
        }

        static void StopRecorder()
        {
            // Ensure demo accel is off when recording stops
            CharacterDetailPanel.DemoAccelerate = false;
            CharacterDetailPanel.ResetDemoAccel();

            if (_recorder != null)
            {
                try
                {
                    if (_recorder.IsRecording())
                        _recorder.StopRecording();
                    Debug.Log($"[StaminaMeterDemo] Recording stopped — output: {OutputDir}/{RawFileStem}.mp4");
                }
                catch (Exception e) { Debug.LogWarning($"[StaminaMeterDemo] StopRecorder: {e.Message}"); }
                _recorder = null;
            }
        }
    }

    /// <summary>
    /// MonoBehaviour bot that drives the live-meter demo sequence as a coroutine.
    /// Runs inside play mode alongside the running game — no synthetic buttons,
    /// no scene mutations. All navigation via real UI paths.
    /// </summary>
    public class StaminaMeterDemoRunner : MonoBehaviour
    {
        public void StartDemo() => StartCoroutine(Sequence());

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Find any MonoBehaviour by type in active scenes.</summary>
        static T FindActive<T>() where T : MonoBehaviour =>
            FindObjectsOfType<T>().FirstOrDefault(c => !string.IsNullOrEmpty(c.gameObject.scene.name));

        /// <summary>Force a character's displayedEnergy LOW for demo start (display-only, never persists).</summary>
        static void ForceConditionLowForDemo(string characterId)
        {
            var cm  = CharacterManager.Instance;
            if (cm == null) return;
            var pcd = cm.GetCharacterData(characterId);
            if (pcd == null) return;

            // Set conditionUpdatedUtc far in the past so the read-only projection
            // computes LOW condition without touching currentStaminaEnergy.
            // We set currentStaminaEnergy directly to a LOW fraction of max for the demo.
            // This is display-only prep; no AccrueRegen/PersistCondition called.
            float lowEnergy = pcd.maxStaminaEnergy * 0.12f; // ~12% → well below 0.30 threshold → RED
            pcd.currentStaminaEnergy = lowEnergy;
            // Set conditionUpdatedUtc to NOW so the projection starts from this low value
            // (no regen before demo accel kicks in).
            pcd.conditionUpdatedUtc = DateTime.UtcNow;
            Debug.Log($"[StaminaMeterBot] Set {characterId} energy to {lowEnergy:F1}/{pcd.maxStaminaEnergy:F1} (12%) → RED meter for demo start.");
        }

        // ── Demo sequence ─────────────────────────────────────────────────────

        IEnumerator Sequence()
        {
            // ── Phase 0: Boot — wait for ShellScene → Loading → Home ──────────
            // ShellScene takes ~4-5s to boot through logo/splash/loading
            yield return new WaitForSecondsRealtime(5.0f);
            Debug.Log("[StaminaMeterBot] Boot complete — at Home screen.");

            // ── Phase 1: Navigate to Roster via real nav button ───────────────
            // PersistentUIManager.Instance.charactersButton is the REAL player entry point
            var puim = FindActive<Golfin.UI.PersistentUIManager>();
            if (puim != null && puim.charactersButton != null)
            {
                Debug.Log("[StaminaMeterBot] Tapping Characters nav button (real entry point).");
                puim.charactersButton.onClick.Invoke();
            }
            else
            {
                // Fallback: direct ShowScreen (belt-and-suspenders; still real ScreenManager path)
                Debug.LogWarning("[StaminaMeterBot] charactersButton not found — using ScreenManager.ShowScreen(Roster) fallback.");
                GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.Roster);
            }
            yield return new WaitForSecondsRealtime(1.5f); // wait for fade-in + panel bind

            // ── Phase 2: Pick a character + force Condition LOW ───────────────
            // Use CharacterManager to find the first owned character
            var cm = CharacterManager.Instance;
            string targetId = "";
            if (cm != null)
            {
                var owned = cm.GetAllOwnedCharacters();
                if (owned != null && owned.Count > 0)
                    targetId = owned[0].characterId;
            }

            if (string.IsNullOrEmpty(targetId))
            {
                Debug.LogWarning("[StaminaMeterBot] No owned characters found — trying save default.");
                targetId = cm?.GetSelectedCharacterId() ?? "";
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                // Force energy LOW for the demo (display-only, no persist)
                ForceConditionLowForDemo(targetId);
                yield return new WaitForSecondsRealtime(0.1f);

                // Select the character via CarouselController (REAL panel path)
                var carousel = FindActive<CarouselController>();
                if (carousel != null)
                {
                    Debug.Log($"[StaminaMeterBot] Selecting character '{targetId}' via CarouselController.SelectCharacter.");
                    carousel.SelectCharacter(targetId);
                }
                else
                {
                    // Fallback: CharacterManager.SelectCharacter (fires OnCharacterSelected event → panel updates)
                    Debug.LogWarning("[StaminaMeterBot] CarouselController not found — using CharacterManager.SelectCharacter fallback.");
                    cm?.SelectCharacter(targetId);
                }
            }
            yield return new WaitForSecondsRealtime(1.5f); // wait for panel to show RED meter

            Debug.Log($"[StaminaMeterBot] Panel should now show RED meter for '{targetId}'. Holding 2s for viewer...");
            yield return new WaitForSecondsRealtime(2.0f);

            // ── Phase 3: Enable demo accel — meter climbs RED → AMBER → BLUE ─
            // With DemoHoursPerRealSecond=0.5 and thresholds at 0.30 (RED→AMBER) and 0.60 (AMBER→BLUE),
            // starting at ~12% pct: need to climb from 0.12 to 0.60 = +0.48 pct.
            // MaxCondition depends on stamina stat. At 0.5 h/s and RegenPerHour ≈ ~30 pts/hr typical,
            // the climb is fast enough to cross both thresholds within 15-20 seconds.
            CharacterDetailPanel.DemoAccelerate = true;
            Debug.Log("[StaminaMeterBot] Demo accel ON — meter climbing RED→AMBER→BLUE. Recording continuous frames...");

            // Hold for ~18s to show full climb: RED → AMBER (~6s) → BLUE (~12s)
            // STR/CC numbers tick up as conditionPct crosses effective-stat integer boundaries.
            yield return new WaitForSecondsRealtime(18.0f);
            Debug.Log("[StaminaMeterBot] Climb complete (should be BLUE now). Holding 2s at peak...");
            yield return new WaitForSecondsRealtime(2.0f);

            // ── Phase 4: Character switch — demonstrate SNAP (no tween) ───────
            var cm2 = CharacterManager.Instance;
            string secondId = "";
            if (cm2 != null)
            {
                var owned = cm2.GetAllOwnedCharacters();
                if (owned != null && owned.Count > 1)
                    secondId = owned[1].characterId;
                else if (owned != null && owned.Count == 1)
                    secondId = owned[0].characterId; // same char re-select still triggers L5 snap path
            }

            if (!string.IsNullOrEmpty(secondId) && secondId != targetId)
            {
                Debug.Log($"[StaminaMeterBot] Switching to '{secondId}' — this should SNAP the meter (no tween).");
                var carousel2 = FindActive<CarouselController>();
                if (carousel2 != null)
                    carousel2.SelectCharacter(secondId);
                else
                    cm2?.SelectCharacter(secondId);
                yield return new WaitForSecondsRealtime(2.5f); // show post-switch state
            }
            else
            {
                Debug.LogWarning($"[StaminaMeterBot] Only one character owned — cannot show cross-character snap. Skipping switch.");
                yield return new WaitForSecondsRealtime(1.0f);
            }

            // ── Phase 5: Disable demo accel — meter holds steady ──────────────
            CharacterDetailPanel.DemoAccelerate = false;
            CharacterDetailPanel.ResetDemoAccel();
            Debug.Log("[StaminaMeterBot] Demo accel OFF — meter holds at real-regen rate (imperceptible). Holding 3s...");
            yield return new WaitForSecondsRealtime(3.0f);

            // ── Phase 6: Done ─────────────────────────────────────────────────
            Debug.Log("[StaminaMeterBot] Sequence done — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
