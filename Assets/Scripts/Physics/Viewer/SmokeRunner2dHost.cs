using System.Collections;
using UnityEngine;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2d screenshot capture host. Spawned by SmokeRunner2dMenu BEFORE play-mode entry.
    /// The hole scene is loaded additively so LabRoot is present in play mode.
    ///
    /// Captures 3 required screenshots:
    ///   S1 — controls_2d_modal_hidden_aiming     (widget hidden, aiming HUD visible)
    ///   S2 — controls_2d_modal_success_at_par    (success: strokes==par, Card2 unlocked)
    ///   S3 — controls_2d_modal_failed_over_par   (failed: strokes>par, Card2 locked)
    ///
    /// Also writes controls_2d_holeout_log.txt with GameSession.ShotHistory dump.
    /// Self-destructs after completion. DO NOT use in production.
    ///
    /// Iter-6: passes real hole-map sprites from Assets/Art/In-Game UI/HoleMaps/
    /// so screenshots show actual map art.
    /// </summary>
    public class SmokeRunner2dHost : MonoBehaviour
    {
        const float StartupWait  = 5.0f;  // wait for all Awake/Start + HUD render + data binding
        const float BetweenWait  = 1.5f;  // between state changes

        /// <summary>
        /// Arm key stored in SessionState so it survives domain reloads triggered by
        /// script-execute compilation (editor-only). Cleared after cleanup.
        /// Guards against accidental auto-run when the component is left serialized in the scene.
        /// </summary>
        const string ArmedKey = "SmokeRunner2dHost.Armed";

        /// <summary>
        /// Legacy static — kept for direct field access in SmokeRunner2dMenu (set = true,
        /// then SessionState is also set for the domain-reload-safe path).
        /// </summary>
        public static bool Armed
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetBool(ArmedKey, false);
#else
                return false;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.SessionState.SetBool(ArmedKey, value);
#endif
            }
        }

        void Start()
        {
#if UNITY_EDITOR
            bool sessionArmed = UnityEditor.SessionState.GetBool(ArmedKey, false);
            Debug.Log($"[SmokeRunner2dHost] Start() — SessionState Armed={sessionArmed}");
#else
            bool sessionArmed = false;
#endif
            if (!sessionArmed)
            {
                Debug.LogWarning("[SmokeRunner2dHost] Not armed — destroying self. " +
                                 "Use GOLFIN > Smoke > Capture 2d HoleComplete Screenshots to launch.");
                Destroy(this);
                return;
            }
            Armed = false; // consume the arm so it can't fire again
            StartCoroutine(RunSequence());
        }

        IEnumerator RunSequence()
        {
            yield return new WaitForSeconds(StartupWait);

            // Find HoleCompleteWidget in the scene hierarchy.
            var widget = FindObjectOfType<HoleCompleteWidget>();
            if (widget == null)
            {
                Debug.LogError("[SmokeRunner2dHost] HoleCompleteWidget not found in scene. " +
                               "Run GOLFIN/Build/Build HoleComplete Widgets (§2d) first.");
                Destroy(this);
                yield break;
            }

            Debug.Log($"[SmokeRunner2dHost] Found HoleCompleteWidget: {widget.name}. IsShowing={widget.IsShowing}");

            // Pre-load hole map sprites (iter-6: load real maps)
            int holeNum  = HoleContext.HoleNumber > 0 ? HoleContext.HoleNumber : 1;
            int nextNum  = holeNum + 1;
            Sprite holeMapSprite     = LoadHoleMapSprite(holeNum);
            Sprite nextHoleMapSprite = LoadHoleMapSprite(nextNum);
            Debug.Log($"[SmokeRunner2dHost] Hole maps: H{holeNum}={holeMapSprite != null} H{nextNum}={nextHoleMapSprite != null}");

            // ── S1: Hidden/Aiming state ───────────────────────────────────────────
            // Widget should be hidden by Awake(). Verify it's hidden.
            if (widget.IsShowing) widget.Hide();
            yield return new WaitForSeconds(BetweenWait);

            string s1Path = CaptureCore.SnapPlayModeSafe("controls_2d_modal_hidden_aiming");
            Debug.Log($"[SmokeRunner2dHost] S1 captured: {s1Path}");

            // ── S2: Success at par ────────────────────────────────────────────────
            int par = HoleContext.Par > 0 ? HoleContext.Par : 4; // fallback Par 4
            var successData = new HoleCompleteData(
                strokes:          par,        // strokes == par → SUCCESS
                par:              par,
                score:            0,
                scoreLabel:       "Par",
                isFailed:         false,
                hasPersonalBest:  false,
                courseName:       string.IsNullOrEmpty(HoleContext.CourseName) ? "LOMOND" : HoleContext.CourseName,
                holeNumber:       holeNum,
                teeName:          string.IsNullOrEmpty(HoleContext.TeeName) ? "REGULAR" : HoleContext.TeeName,
                bestStrokes:      "—",
                bestStrokesLabel: "",
                timeStr:          "00:00:00",
                bestTimeStr:      "—",
                rewardCoinX:      10,
                rewardRepairX:    10,
                rewardBallX:      10,
                nextHoleNumber:   nextNum,
                nextHolePar:      0,
                nextHoleTipText:  "Next hole tip — TBD",
                holeMap:          holeMapSprite,
                nextHoleMap:      nextHoleMapSprite
            );

            widget.Show(successData, () => { });
            yield return new WaitForSeconds(BetweenWait);

            string s2Path = CaptureCore.SnapPlayModeSafe("controls_2d_modal_success_at_par");
            Debug.Log($"[SmokeRunner2dHost] S2 captured: {s2Path}");

            // ── S3: Failed over par ───────────────────────────────────────────────
            widget.Hide();
            yield return new WaitForSeconds(0.3f);

            var failedData = new HoleCompleteData(
                strokes:          par + 2,    // 2 over par → FAILED, no PB → Card2 locked
                par:              par,
                score:            2,
                scoreLabel:       "Double Bogey",
                isFailed:         true,
                hasPersonalBest:  false,      // Card2 locked (no PB)
                courseName:       string.IsNullOrEmpty(HoleContext.CourseName) ? "LOMOND" : HoleContext.CourseName,
                holeNumber:       holeNum,
                teeName:          string.IsNullOrEmpty(HoleContext.TeeName) ? "REGULAR" : HoleContext.TeeName,
                bestStrokes:      "—",
                bestStrokesLabel: "",
                timeStr:          "00:00:00",
                bestTimeStr:      "—",
                rewardCoinX:      10,
                rewardRepairX:    10,
                rewardBallX:      10,
                nextHoleNumber:   nextNum,
                nextHolePar:      0,
                nextHoleTipText:  "Next hole tip — TBD",
                holeMap:          holeMapSprite,
                nextHoleMap:      nextHoleMapSprite
            );

            widget.Show(failedData, () => { });
            yield return new WaitForSeconds(BetweenWait);

            string s3Path = CaptureCore.SnapPlayModeSafe("controls_2d_modal_failed_over_par");
            Debug.Log($"[SmokeRunner2dHost] S3 captured: {s3Path}");

            // ── Write shot history log ────────────────────────────────────────────
            widget.Hide();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ShotHistory count={GameSession.ShotHistory.Count} (§2d smoke run):");
            for (int i = 0; i < GameSession.ShotHistory.Count; i++)
            {
                var r = GameSession.ShotHistory[i];
                sb.AppendLine($"  Entry[{i}]: ShotNumber={r.ShotNumber} Club={r.ClubLabel}" +
                              $" Terminal={r.TerminalState} OBReason={r.OBReason ?? "null"}" +
                              $" Surface={r.FinalSurface}" +
                              $" DistXZ={r.DistanceXZMeters:F1}m" +
                              $" Origin={r.OriginPosition:F2} Final={r.FinalPosition:F2}");
            }
            string histLog = sb.ToString();
            string logDir  = CaptureCore.OutDir;
            System.IO.Directory.CreateDirectory(logDir);
            string logPath = $"{logDir}/controls_2d_holeout_log.txt";
            System.IO.File.WriteAllText(logPath, histLog);
            Debug.Log($"[SmokeRunner2dHost] Shot history log written: {logPath}\n{histLog}");

            Debug.Log($"[SmokeRunner2dHost] §2d CAPTURE COMPLETE.\n  S1={s1Path}\n  S2={s2Path}\n  S3={s3Path}");
            Destroy(this);
        }

        /// <summary>
        /// Load the hole map sprite from Assets/Art/In-Game UI/HoleMaps/Lomond - Hole N.png.
        /// Editor-only (AssetDatabase). Returns null if not found.
        /// </summary>
        static Sprite LoadHoleMapSprite(int holeNumber)
        {
#if UNITY_EDITOR
            string path = $"Assets/Art/In-Game UI/HoleMaps/Lomond - Hole {holeNumber}.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }
    }
}
