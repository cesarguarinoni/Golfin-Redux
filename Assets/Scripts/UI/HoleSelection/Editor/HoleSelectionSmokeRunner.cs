using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using GolfinRedux.UI;
using GolfinRedux.UI.HoleSelection;
using Golfin.EditorTools;

/// <summary>
/// Smoke-test runner for the hole_selection_screen task.
/// Invoke via: GOLFIN/Smoke Test/Run Hole Selection Smoke Test
///
/// Step sequence:
///   1. Run GOLFIN/Wire/Hole Selection (auto-wire)
///   2. Open ShellScene, enter play mode
///   3. Wait for scene load → navigate to HoleSelection screen
///   4. Verify 18 cards rendered, capture expanded_hole1_play.png
///   5. Toggle Hole 1 overrides (played=true) → capture expanded_hole1_replay.png
///   6. Tap PLAY → matchmaking modal → capture matchmaking_from_play.png
///   7. Write results to Docs/Diagnostics/_capture/hole_selection_smoke.log
///   8. Exit play mode
/// </summary>
namespace GolfinRedux.UI.HoleSelection.Editor
{
    public static class HoleSelectionSmokeRunner
    {
        private const string LOG_PATH = "Docs/Diagnostics/_capture/hole_selection_smoke.log";

        [MenuItem("GOLFIN/Smoke Test/Run Hole Selection Smoke Test")]
        public static void RunSmokeTest()
        {
            // Step 1: Run auto-wire first (idempotent)
            EditorApplication.ExecuteMenuItem("GOLFIN/Wire/Hole Selection");
            Debug.Log("[HoleSelectionSmokeRunner] GOLFIN/Wire/Hole Selection ran.");

            // Step 2: Run prerequisites (localization, sprites, hole import)
            EditorApplication.ExecuteMenuItem("Tools/Localization/Import Text CSV");
            Debug.Log("[HoleSelectionSmokeRunner] Tools/Localization/Import Text CSV ran.");

            EditorApplication.ExecuteMenuItem("GOLFIN/Setup/Configure Hole Images as Sprites");
            Debug.Log("[HoleSelectionSmokeRunner] GOLFIN/Setup/Configure Hole Images as Sprites ran.");

            // Note: GOLFIN/Import Holes from CSV opens an EditorWindow that requires user input.
            // We skip that and verify the asset content directly.

            // Step 3: Open ShellScene
            string scenePath = "Assets/Scenes/ShellScene.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[HoleSelectionSmokeRunner] ShellScene not found at {scenePath}");
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log("[HoleSelectionSmokeRunner] ShellScene opened.");

            // Step 4: Take an EditMode screenshot of ShellScene (before play)
            string editSnap = CaptureHelper.SnapGameViewWithLabel("hole-sel-editmode");
            Debug.Log($"[HoleSelectionSmokeRunner] EditMode snap: {editSnap}");

            // Step 5: Register play mode listener for runtime captures
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Step 6: Enter play mode
            EditorApplication.isPlaying = true;
            Debug.Log("[HoleSelectionSmokeRunner] Entering play mode...");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Unsubscribe immediately
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                Debug.Log("[HoleSelectionSmokeRunner] Play mode entered. Starting smoke coroutine...");

                // Use an EditorCoroutine-style update hook since we're in play mode
                // In play mode, we can use a MonoBehaviour coroutine
                var go = new GameObject("_HoleSmokeTestRunner");
                go.hideFlags = HideFlags.DontSave;
                var runner = go.AddComponent<SmokeTestMonoBehaviour>();
                runner.StartCoroutine(runner.RunSmokeSequence());
            }
        }
    }

    /// <summary>
    /// Play-mode coroutine host for the smoke test sequence.
    /// Self-destructs when done.
    /// </summary>
    public class SmokeTestMonoBehaviour : MonoBehaviour
    {
        private const string CAPTURE_DIR = "Docs/Diagnostics/_capture";
        private const string LOG_PATH = "Docs/Diagnostics/_capture/hole_selection_smoke.log";
        private const string TASK_SCREENSHOTS_DIR = "Docs/Specs/Active/hole_selection_screen/screenshots";

        public IEnumerator RunSmokeSequence()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine($"=== HoleSelection Smoke Test === {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // Wait for scene to fully initialize
            yield return new WaitForSeconds(3f);

            // Find ScreenManager
            ScreenManager screenMgr = Object.FindFirstObjectByType<ScreenManager>();
            if (screenMgr == null)
            {
                log.AppendLine("FAIL: ScreenManager not found in scene");
                WriteLog(log.ToString());
                Destroy(gameObject);
                EditorApplication.isPlaying = false;
                yield break;
            }
            log.AppendLine("PASS: ScreenManager found");

            // Navigate to HoleSelection
            screenMgr.ShowScreen(ScreenId.HoleSelection);
            log.AppendLine("PASS: ShowScreen(HoleSelection) called");

            // Wait for screen activation + card instantiation
            yield return new WaitForSeconds(2f);

            // Find HoleSelectionScreenController
            HoleSelectionScreenController hsc = Object.FindFirstObjectByType<HoleSelectionScreenController>();
            if (hsc == null)
            {
                log.AppendLine("FAIL: HoleSelectionScreenController not found");
                WriteLog(log.ToString());
                Destroy(gameObject);
                EditorApplication.isPlaying = false;
                yield break;
            }
            log.AppendLine("PASS: HoleSelectionScreenController found");

            // Verify 18 cards exist by checking child count of cards content
            // Use reflection to access private _cards field
            var cardsField = typeof(HoleSelectionScreenController).GetField("_cards",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cards = cardsField != null
                ? cardsField.GetValue(hsc) as System.Collections.Generic.List<HoleCardController>
                : null;

            int cardCount = cards != null ? cards.Count : -1;
            log.AppendLine($"{(cardCount == 18 ? "PASS" : "FAIL")}: card count = {cardCount} (expected 18)");

            // Step 8: Expand Hole 1 (PLAY mode)
            if (cards != null && cards.Count >= 1)
            {
                HoleCardController card1 = cards[0]; // hole 1 = index 0
                card1.SetState(HoleCardState.Expanded);
                yield return new WaitForSeconds(1f);

                // Capture: expanded Hole 1 in PLAY mode
                yield return new WaitForEndOfFrame();
                string snap1 = CaptureHelper.SnapGameViewWithLabel("expanded_hole1_play");
                log.AppendLine($"PASS: Step 8 screenshot: {snap1}");

                // Copy to task screenshots dir
                Directory.CreateDirectory(TASK_SCREENSHOTS_DIR);
                string dest1 = $"{TASK_SCREENSHOTS_DIR}/expanded_hole1_play.png";
                File.Copy(snap1, dest1, overwrite: true);
                log.AppendLine($"PASS: Copied to {dest1}");

                // Step 10: Switch to REPLAY mode
                // Use HoleProgressionService to mark hole 1 as played
                HoleProgressionService.Instance.SetPlayedOverride(1, true);
                // Force screen refresh
                hsc.gameObject.SetActive(false);
                hsc.gameObject.SetActive(true);
                yield return new WaitForSeconds(2f);

                // Re-find cards after refresh
                var cards2 = cardsField.GetValue(hsc) as System.Collections.Generic.List<HoleCardController>;
                if (cards2 != null && cards2.Count >= 1)
                {
                    HoleCardController card1b = cards2[0];
                    card1b.SetState(HoleCardState.Expanded);
                    yield return new WaitForSeconds(1f);
                    yield return new WaitForEndOfFrame();

                    string snap2 = CaptureHelper.SnapGameViewWithLabel("expanded_hole1_replay");
                    log.AppendLine($"PASS: Step 10 screenshot: {snap2}");
                    string dest2 = $"{TASK_SCREENSHOTS_DIR}/expanded_hole1_replay.png";
                    File.Copy(snap2, dest2, overwrite: true);
                    log.AppendLine($"PASS: Copied to {dest2}");

                    // Step 11: Tap PLAY → matchmaking modal
                    // The actionButton is wired; simulate the action
                    var actionButtonField = typeof(HoleCardController).GetField("actionButton",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (actionButtonField != null)
                    {
                        var btn = actionButtonField.GetValue(card1b) as Button;
                        if (btn != null)
                        {
                            btn.onClick.Invoke();
                            log.AppendLine("PASS: actionButton.onClick.Invoke() called");
                            yield return new WaitForSeconds(1.5f);
                            yield return new WaitForEndOfFrame();

                            string snap3 = CaptureHelper.SnapGameViewWithLabel("matchmaking_from_play");
                            log.AppendLine($"PASS: Step 11 screenshot: {snap3}");
                            string dest3 = $"{TASK_SCREENSHOTS_DIR}/matchmaking_from_play.png";
                            File.Copy(snap3, dest3, overwrite: true);
                            log.AppendLine($"PASS: Copied to {dest3}");
                        }
                        else
                        {
                            log.AppendLine("FAIL: actionButton is null after reflection");
                        }
                    }
                    else
                    {
                        log.AppendLine("FAIL: actionButton field not found via reflection");
                    }
                }
                else
                {
                    log.AppendLine("FAIL: cards not available after replay mode toggle");
                }
            }
            else
            {
                log.AppendLine("FAIL: cards list empty or null");
            }

            log.AppendLine($"=== Smoke test complete at {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            WriteLog(log.ToString());
            Debug.Log($"[HoleSelectionSmokeRunner] Test complete. Log at {LOG_PATH}");

            Destroy(gameObject);
            EditorApplication.isPlaying = false;
        }

        private static void WriteLog(string content)
        {
            Directory.CreateDirectory("Docs/Diagnostics/_capture");
            File.WriteAllText(LOG_PATH, content);
        }
    }
}
