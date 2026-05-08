using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2c smoke runner runtime host. Spawned by SmokeRunner2cMenu (Editor) BEFORE play-mode entry.
    /// The hole scene is already loaded additively in edit mode, so ScanForLoadedHoleSceneAtStartup
    /// detects it at play-mode startup and fires OnHoleLoaded + ResetForNewHole automatically.
    ///
    /// Verifies:
    ///   C1 — TURN 1 at first Aiming state after hole load
    ///   C2 — TURN 2 after first shot completes + 1.5s settle delay
    ///   C3 — TURN 1 again after hole reload (proves ResetForNewHole fired)
    ///   C4 — ShotHistory log written to disk
    ///
    /// Self-destructs after completion. DO NOT use in production.
    /// </summary>
    public class SmokeRunner2cHost : MonoBehaviour
    {
        const string HoleScene      = "Hole_01_Geo";
        const float  StartupWait    = 2.5f;  // wait for ScanForLoadedHoleSceneAtStartup + HUD render
        const float  AimRenderWait  = 0.5f;  // let HUD re-render after turn change
        const float  PostShotSettle = 2.0f;  // > postShotDelaySeconds (1.5s)

        void Start() => StartCoroutine(RunSequence());

        IEnumerator RunSequence()
        {
            // Wait for all Awake/Start including ScanForLoadedHoleSceneAtStartup (which yields 2 frames).
            yield return new WaitForSeconds(StartupWait);

            var labController = FindObjectOfType<PhysicsLabController>();
            if (labController == null)
            {
                Debug.LogError("[SmokeRunner2cHost] PhysicsLabController not found — aborting.");
                Destroy(this);
                yield break;
            }

            Debug.Log($"[SmokeRunner2cHost] After startup wait. TurnCount={GameSession.TurnCount}");

            // ── C1: Capture TURN 1 at Aiming ─────────────────────────────────────
            // ScanForLoadedHoleSceneAtStartup should have fired OnHoleLoaded -> ResetForNewHole -> TurnCount=1.
            yield return new WaitForSeconds(AimRenderWait);
            // CaptureCore.SnapPlayModeSafe skips AssetDatabase.Refresh() (which would force
            // a domain reload and kill this coroutine) and returns the written path.
            string c1Path = CaptureCore.SnapPlayModeSafe("controls_2c_turn1_aiming");
            Debug.Log($"[SmokeRunner2cHost] C1 captured: {c1Path} | TurnCount={GameSession.TurnCount}");

            // ── Fire shot 1 (Driver, calm preset) ────────────────────────────────
            // Retry getting BallSM up to 3 times (0.5s apart) in case Awake ordering varies.
            BallStateMachine sm = null;
            for (int retry = 0; retry < 6 && sm == null; retry++)
            {
                sm = labController.BallSM;
                if (sm == null)
                {
                    Debug.LogWarning($"[SmokeRunner2cHost] BallSM null on retry {retry}/5 — waiting 0.5s...");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            if (sm == null)
            {
                Debug.LogError($"[SmokeRunner2cHost] labController.BallSM is null after retries — aborting. " +
                               $"Controller found: {labController != null}");
                Destroy(this);
                yield break;
            }
            bool shotComplete = false;
            System.Action<ShotResult> onComplete = _ => shotComplete = true;
            sm.OnShotComplete += onComplete;

            var preset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "driver_calm");
            if (preset.Id == null)
            {
                Debug.LogError("[SmokeRunner2cHost] 'driver_calm' preset not found.");
                sm.OnShotComplete -= onComplete;
                Destroy(this);
                yield break;
            }

            Debug.Log("[SmokeRunner2cHost] Firing driver_calm...");
            labController.Fire(preset);

            float elapsed = 0f, timeout = 30f;
            while (!shotComplete && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            sm.OnShotComplete -= onComplete;

            if (!shotComplete)
            {
                Debug.LogError("[SmokeRunner2cHost] Shot did not complete within 30s timeout — aborting.");
                Destroy(this);
                yield break;
            }
            Debug.Log($"[SmokeRunner2cHost] Shot 1 complete. TurnCount={GameSession.TurnCount}");

            // ── Wait for postShotDelaySeconds (1.5s) + buffer ─────────────────────
            yield return new WaitForSeconds(PostShotSettle);
            Debug.Log($"[SmokeRunner2cHost] After settle. TurnCount={GameSession.TurnCount}");

            // ── C2: Capture TURN 2 ────────────────────────────────────────────────
            yield return new WaitForSeconds(AimRenderWait);
            string c2Path = CaptureCore.SnapPlayModeSafe("controls_2c_turn2_after_first_shot");
            Debug.Log($"[SmokeRunner2cHost] C2 captured: {c2Path} | TurnCount={GameSession.TurnCount}");

            // ── C4: Dump ShotHistory ──────────────────────────────────────────────
            string histLog = BuildHistoryLog();
            string logDir  = CaptureCore.OutDir;
            System.IO.Directory.CreateDirectory(logDir);
            string logPath = $"{logDir}/controls_2c_history_log.txt";
            System.IO.File.WriteAllText(logPath, histLog);
            Debug.Log($"[SmokeRunner2cHost] C4 history log: {logPath}\n{histLog}");

            // ── Trigger hole unload → reload to test ResetForNewHole ─────────────
            // In play mode, SceneManager events drive PhysicsLabController (not EditorSceneManager).
            // We call OnHoleUnloaded/OnHoleLoaded manually because LabHoleBinder only listens
            // to EditorSceneManager events (which don't fire in play mode).
            Debug.Log("[SmokeRunner2cHost] Triggering OnHoleUnloaded → OnHoleLoaded cycle...");
            labController.OnHoleUnloaded();
            Debug.Log($"[SmokeRunner2cHost] After unload. TurnCount={GameSession.TurnCount}");

            yield return new WaitForSeconds(0.3f);

            // OnHoleLoaded needs the scene name. The scene is still loaded additively in play mode.
            labController.OnHoleLoaded(HoleScene);
            yield return new WaitForSeconds(1.0f);
            Debug.Log($"[SmokeRunner2cHost] After reload. TurnCount={GameSession.TurnCount}");

            // ── C3: Capture TURN 1 after reload ──────────────────────────────────
            yield return new WaitForSeconds(AimRenderWait);
            string c3Path = CaptureCore.SnapPlayModeSafe("controls_2c_turn1_after_hole_reload");
            Debug.Log($"[SmokeRunner2cHost] C3 captured: {c3Path} | TurnCount={GameSession.TurnCount}");

            Debug.Log("[SmokeRunner2cHost] SMOKE RUN COMPLETE.");
            Destroy(this);
        }

        static string BuildHistoryLog()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ShotHistory count={GameSession.ShotHistory.Count} (captured after C2):");
            for (int i = 0; i < GameSession.ShotHistory.Count; i++)
            {
                var r = GameSession.ShotHistory[i];
                sb.AppendLine($"  Entry[{i}]: ShotNumber={r.ShotNumber} Club={r.ClubLabel}" +
                              $" Terminal={r.TerminalState} OBReason={r.OBReason ?? "null"}" +
                              $" Surface={r.FinalSurface}" +
                              $" DistXZ={r.DistanceXZMeters:F1}m" +
                              $" Origin={r.OriginPosition:F2} Final={r.FinalPosition:F2}");
            }
            return sb.ToString();
        }
    }
}
