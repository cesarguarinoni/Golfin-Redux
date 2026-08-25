using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using Golfin.Roster;
using Golfin.UI;
using GolfinRedux.UI;

namespace Golfin.EditorTools
{
    /// <summary>
    /// Records the starter-character-selection demo video for the pipeline.
    /// Menu: GOLFIN > Roster > Record Starter Selection Demo Video
    /// Writes: Docs/Specs/Active/starting_character_selection/videos/raw.mp4
    ///
    /// Domain-reload fix (iter-8, 2026-08-25): entering play mode triggers a domain
    /// reload which wipes all static fields.  The RecorderController set by LaunchDemo()
    /// becomes null before EnteredPlayMode fires.  Fix: use SessionState (survives domain
    /// reloads within the same Editor session) to pass the "please record this run" flag
    /// across the reload boundary, and reconstruct the controller inside EnteredPlayMode.
    /// </summary>
    [InitializeOnLoad]
    public static class StarterSelectionDemoRecorder
    {
        const string SaveBackedUpKey   = "StarterDemo_SaveBacked";
        const string ShouldRecordKey   = "StarterDemo_ShouldRecord";
        const string OutputFolder      = "Docs/Specs/Active/starting_character_selection/videos";
        const string OutputFile        = "raw";

        static RecorderController _controller;  // lives only within a play-mode session

        static StarterSelectionDemoRecorder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Roster/Record Starter Selection Demo Video")]
        static void LaunchDemo()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[StarterSelectionDemo] Already in play mode — stop first.");
                return;
            }

            // Delete save.json for a true zero-save fresh boot (NeedsStarter=true, no owned chars).
            // Auth lives in PlayerPrefs — NOT deleted. Only save.json is touched.
            string savePath   = Path.Combine(Application.persistentDataPath, "save.json");
            string backupPath = savePath + ".starterDemo.bak";
            if (File.Exists(savePath))
            {
                File.Copy(savePath, backupPath, overwrite: true);
                File.Delete(savePath);
                SessionState.SetBool(SaveBackedUpKey, true);
                Debug.Log("[StarterSelectionDemo] save.json deleted (backed up) — zero-save fresh boot.");
            }
            else
            {
                SessionState.SetBool(SaveBackedUpKey, false);
                Debug.Log("[StarterSelectionDemo] No save.json found — fresh boot already.");
            }

            // Signal that the NEXT EnteredPlayMode should start the recorder.
            // SessionState survives the play-mode domain reload; static fields do not.
            SessionState.SetBool(ShouldRecordKey, true);

            EditorApplication.isPlaying = true;
        }

        static void StartRecorder()
        {
            // Check SessionState flag (survives domain reload); clear it immediately.
            if (!SessionState.GetBool(ShouldRecordKey, false))
            {
                Debug.Log("[StarterSelectionDemo] StartRecorder: ShouldRecord=false — skipping recorder.");
                return;
            }
            SessionState.SetBool(ShouldRecordKey, false);

            // Build the recorder controller fresh inside EnteredPlayMode (after domain reload).
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRate = 30;

            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name            = "StarterSelectionVideo";
            movieSettings.Enabled         = true;
            movieSettings.OutputFile      = $"{OutputFolder}/{OutputFile}";
            movieSettings.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth  = 1170,
                OutputHeight = 2532,
            };
            movieSettings.VideoBitRateMode = VideoBitrateMode.High;

            settings.AddRecorderSettings(movieSettings);
            _controller = new RecorderController(settings);

            _controller.PrepareRecording();
            _controller.StartRecording();
            Debug.Log($"[StarterSelectionDemo] Recording → {OutputFolder}/{OutputFile}.mp4 (1170x2532 @ 30fps)");
        }

        static void StopRecorder()
        {
            if (_controller == null) return;
            _controller.StopRecording();
            _controller = null;
            Debug.Log("[StarterSelectionDemo] Recording stopped.");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Only spawn recorder + bot when the menu item was used to launch.
                // Normal play-mode entries (for screenshots / manual testing) must not trigger the bot.
                bool shouldRecord = SessionState.GetBool(ShouldRecordKey, false);
                StartRecorder();  // clears the flag if set
                if (shouldRecord)
                {
                    var go = new GameObject("StarterSelectionDemoRunner");
                    go.AddComponent<StarterSelectionDemoRunner>();
                }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
                RestoreSave();
            }
        }

        static void RestoreSave()
        {
            if (!SessionState.GetBool(SaveBackedUpKey, false)) return;
            string savePath   = Path.Combine(Application.persistentDataPath, "save.json");
            string backupPath = savePath + ".starterDemo.bak";
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, savePath, overwrite: true);
                File.Delete(backupPath);
                SessionState.SetBool(SaveBackedUpKey, false);
                Debug.Log("[StarterSelectionDemo] save.json restored from backup.");
            }
        }
    }

    /// <summary>MonoBehaviour that drives the bot sequence inside play mode.</summary>
    public class StarterSelectionDemoRunner : MonoBehaviour
    {
        float _t0;
        readonly List<(float start, float end, string text)> _captions = new();
        float _captionStart;
        string _captionText;

        void Start()
        {
            _t0 = Time.time;
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            // Wait for scene load
            yield return Hold(5.0f);

            // Click PLAY / Start on splash
            Mark("Starting character selection\nNew player first boot");
            var splash = FindActive<SplashScreenController>();
            if (splash != null) splash.OnStartClicked();
            else Debug.LogWarning("[StarterSelectionBot] SplashScreenController not found.");
            yield return Hold(4.0f);

            // Should now be on StartingCharacterSelection
            Mark("Choose your starting character\nbrowse the roster");
            yield return Hold(2.5f);

            // Navigate to Olivia (not owned in this fresh save)
            var carousel = FindActive<CarouselController>();
            if (carousel != null) carousel.SelectCharacter("char_olivia");
            yield return Hold(0.5f);
            Mark("Olivia Guarinoni — tap SELECT to choose her");
            yield return Hold(2.0f);

            // SELECT Olivia → confirm modal opens (showing Olivia)
            Click("Canvas/ScreensRoot/RosterScreen/DetailPanel/RightPanel/SelectButton");
            yield return Hold(1.2f);
            Mark("Confirm starting character\nOlivia Guarinoni");
            yield return Hold(2.5f);

            // BACK from Olivia confirm modal
            Click("Canvas/ScreensRoot/RosterScreen/StartingCharacterConfirmModal/Panel/ButtonsRow/CancelButton");
            yield return Hold(0.8f);
            Mark("Changed your mind — browse again");
            yield return Hold(1.2f);

            // Navigate back to James
            if (carousel != null) carousel.SelectCharacter("char_james");
            yield return Hold(0.5f);
            Mark("James Cartwright — tap SELECT\nto start with him");
            yield return Hold(1.8f);

            // SELECT James → confirm modal opens (showing James)
            Click("Canvas/ScreensRoot/RosterScreen/DetailPanel/RightPanel/SelectButton");
            yield return Hold(1.2f);
            Mark("Confirm James as your starting character\nthis choice cannot be changed later");
            yield return Hold(2.0f);

            // CONFIRM James
            Click("Canvas/ScreensRoot/RosterScreen/StartingCharacterConfirmModal/Panel/ButtonsRow/ConfirmButton");
            yield return Hold(4.0f);

            // Home screen
            Mark("Welcome — James is in your roster\nStart your first game");
            yield return Hold(2.5f);

            // Navigate to Roster to show Olivia locked
            var puim = FindActive<PersistentUIManager>();
            if (puim != null && puim.charactersButton != null)
                puim.charactersButton.onClick.Invoke();
            yield return Hold(1.5f);
            Mark("Roster — James owned\nOlivia and others locked until earned");
            yield return Hold(3.0f);

            WriteCaptions(Time.time - _t0);
            Debug.Log("[StarterSelectionBot] Sequence complete — exiting play mode.");
            EditorApplication.ExitPlaymode();
        }

        // ── helpers ──────────────────────────────────────────────────────────

        IEnumerator Hold(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        void Mark(string text)
        {
            float now = Time.time - _t0;
            if (_captionText != null)
                _captions.Add((_captionStart, now, _captionText));
            _captionStart = now;
            _captionText  = text;
            Debug.Log($"[StarterSelectionBot] t={now:F2}s  {text.Replace("\n", " / ")}");
        }

        void Click(string path)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                Debug.LogWarning($"[StarterSelectionBot] not found: {path}");
                return;
            }
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            if (btn == null || !btn.interactable)
            {
                Debug.LogWarning($"[StarterSelectionBot] button not interactable: {path}");
                return;
            }
            btn.onClick.Invoke();
        }

        static T FindActive<T>() where T : Component
        {
            foreach (var obj in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (obj.gameObject.activeInHierarchy) return obj;
            return null;
        }

        void WriteCaptions(float totalDuration)
        {
            float now = Time.time - _t0;
            if (_captionText != null)
                _captions.Add((_captionStart, now, _captionText));

            var sb = new System.Text.StringBuilder("[\n");
            for (int i = 0; i < _captions.Count; i++)
            {
                var (s, e, t) = _captions[i];
                string escaped = t.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
                sb.Append($"  {{\"start\": {s:F3}, \"end\": {e:F3}, \"text\": \"{escaped}\"}}");
                if (i < _captions.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("]");

            string outPath = Path.Combine(
                Application.dataPath, "..",
                "Docs/Specs/Active/starting_character_selection/videos/captions.json");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[StarterSelectionBot] Wrote {_captions.Count} captions, total {totalDuration:F1}s");
        }
    }
}
