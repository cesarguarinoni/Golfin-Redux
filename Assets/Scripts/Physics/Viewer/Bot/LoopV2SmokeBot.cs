#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Host MonoBehaviour for the Loop v2 smoke bot.
    ///
    /// Mirrors SmokeRunner2fHost lifecycle pattern:
    ///   - SessionState armed flag (clears itself on Start to prevent re-entry)
    ///   - SessionState scenario key selects which Scenarios.* coroutine to run
    ///   - WaitForSecondsRealtime(StartupWait) before anything touches the UI
    ///   - timeScale=1 guard (in case a prior paused state left it at 0)
    ///   - SafeRun wraps the scenario in try/catch; self-destructs on completion
    ///
    /// Launched by LoopV2SmokeBotMenu.cs (Editor/). DO NOT place in scenes manually;
    /// the menu item creates and arms this component at play-mode entry.
    ///
    /// All bot logic lives in BotDriver (primitives) and Scenarios (scenario library).
    /// </summary>
    public class LoopV2SmokeBot : MonoBehaviour
    {
        const string ArmedKey    = "LoopV2SmokeBot.Armed";
        const string ScenarioKey = "LoopV2SmokeBot.Scenario";
        const float  StartupWait = 5f;

        // ── SessionState properties ───────────────────────────────────────────

        public static bool Armed
        {
            get => UnityEditor.SessionState.GetBool(ArmedKey, false);
            set => UnityEditor.SessionState.SetBool(ArmedKey, value);
        }

        public static string Scenario
        {
            get => UnityEditor.SessionState.GetString(ScenarioKey, "");
            set => UnityEditor.SessionState.SetString(ScenarioKey, value);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Start()
        {
            bool sessionArmed = UnityEditor.SessionState.GetBool(ArmedKey, false);
            Debug.Log($"[LoopV2SmokeBot] Start() — Armed={sessionArmed} Scenario={Scenario}");

            if (!sessionArmed)
            {
                Debug.LogWarning("[LoopV2SmokeBot] Not armed — destroying self.");
                Destroy(gameObject);
                return;
            }

            // Clear armed flag immediately to prevent re-entry on domain reload.
            Armed = false;

            // Guard against timeScale=0 from a prior paused state (WaitForSeconds uses scaled time).
            if (Time.timeScale < 0.01f)
            {
                Debug.LogWarning($"[LoopV2SmokeBot] timeScale={Time.timeScale} — forcing to 1.");
                Time.timeScale = 1f;
            }

            StartCoroutine(SafeRun());
        }

        // ── Runner ────────────────────────────────────────────────────────────

        IEnumerator SafeRun()
        {
            Debug.Log($"[LoopV2SmokeBot] Waiting {StartupWait}s (realtime) for startup…");
            yield return new WaitForSecondsRealtime(StartupWait);

            string scenario  = Scenario;
            string captureDir = $"tasks/loop_v2_smoke_bot/{scenario}/screenshots";
            var driver        = new Bot.BotDriver(captureDir);

            driver.LogStep($"=== LoopV2SmokeBot started: scenario='{scenario}' ===");

            System.Exception caught    = null;
            bool            completed  = false;

            // Dispatch to the right scenario coroutine.
            IEnumerator scenarioRoutine = null;
            switch (scenario)
            {
                case "hole1_playthrough":
                    scenarioRoutine = Bot.Scenarios.Hole1Playthrough(driver);
                    break;
                case "settings_round_trip":
                    scenarioRoutine = Bot.Scenarios.SettingsRoundTrip(driver);
                    break;
                case "hole_selection_browse":
                    scenarioRoutine = Bot.Scenarios.HoleSelectionBrowse(driver);
                    break;
                // Stage C1 scenarios:
                case "hole1_play_next":
                    scenarioRoutine = Bot.Scenarios.Hole1PlayNext(driver);
                    break;
                case "hole1_menu":
                    scenarioRoutine = Bot.Scenarios.Hole1Menu(driver);
                    break;
                case "hole1_retry_after_fail":
                    scenarioRoutine = Bot.Scenarios.Hole1RetryAfterFail(driver);
                    break;
                case "hole18_course_cleared":
                    scenarioRoutine = Bot.Scenarios.Hole18CourseCleared(driver);
                    break;
                // Stage E scenario:
                case "hole_selection_entry_to_replay_rewards":
                    scenarioRoutine = Bot.Scenarios.HoleSelectionEntryToReplayRewards(driver);
                    break;
                default:
                    driver.LogStep($"ERROR: Unknown scenario key '{scenario}'");
                    break;
            }

            if (scenarioRoutine != null)
            {
                // Wrap in try-yield-return is not directly possible in C#; use a nested coroutine.
                bool innerDone = false;
                System.Exception innerEx = null;
                yield return StartCoroutine(RunWithCatch(
                    scenarioRoutine,
                    () => innerDone = true,
                    ex  => innerEx  = ex));

                completed = innerDone;
                caught    = innerEx;
            }

            // Final log.
            if (caught != null)
                driver.LogStep($"EXCEPTION: {caught}");

            if (completed)
                driver.LogStep("=== Scenario complete ===");
            else
                driver.LogStep("=== Scenario INCOMPLETE — see above for reason ===");

            driver.FlushLog();

            Debug.Log($"[LoopV2SmokeBot] Done. Exiting play mode.");
            Destroy(gameObject);
            // Exit play mode so the next scenario can be launched via the menu.
            UnityEditor.EditorApplication.ExitPlaymode();
        }

        /// <summary>
        /// Helper coroutine that runs innerRoutine and reports completion/exception.
        /// Try-catch cannot wrap yield in C#, so we use this pattern.
        /// </summary>
        IEnumerator RunWithCatch(IEnumerator inner, System.Action onDone,
            System.Action<System.Exception> onError)
        {
            // We can't wrap yield in try/catch directly, but we CAN catch from a
            // MoveNext call if we drive the enumerator manually.
            while (true)
            {
                bool hasNext;
                try { hasNext = inner.MoveNext(); }
                catch (System.Exception ex) { onError(ex); yield break; }

                if (!hasNext) break;
                yield return inner.Current;
            }
            onDone();
        }
    }
}
#endif
