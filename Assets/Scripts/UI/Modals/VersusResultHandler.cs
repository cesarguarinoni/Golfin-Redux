using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Session;
using Golfin.Roster;
using Golfin.UI.GameplayTransition;
using GolfinRedux.UI.ModeSelect;
using Golfin.Audio.Events;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// ShellScene-resident handler for 1v1 match completion (Phase 2a).
    ///
    /// Subscribes to GameSession.OnMatchComplete (fired by VersusMatchController via
    /// GameSession.MarkMatchComplete). This handler lives in Assembly-CSharp so it can
    /// reach both RewardPointsManager (Golfin.Roster) and ModesDatabaseCSV (Assembly-CSharp).
    ///
    /// VersusMatchController (Golfin.Physics.Viewer) MUST NOT call these directly —
    /// asmdef boundary. This event-bridge pattern mirrors HoleCompleteModalController
    /// subscribing to GameSession.OnHoleComplete.
    ///
    /// Rewards:
    ///   P1Win  → grant ModesDatabaseCSV "versus_1v1".rewards (200 RP) + navigate home
    ///   P2Win  → grant 0 RP + navigate home
    ///   Draw   → grant 0 RP + navigate home
    ///
    /// Navigation: StartCoroutine(GameplaySceneLoader.Instance.UnloadGameplay()) tears
    /// down LabScaffold + Hole_NN_Geo additively-loaded scenes and returns to the Shell.
    ///
    /// Placed on a persistent ShellScene GameObject (e.g. the same [Session] root that
    /// hosts HoleCompleteModalController). Safe to coexist — different events.
    ///
    /// Phase 2 note: a dedicated result modal (WIN/LOSE/DRAW card with RP counter) is
    /// deferred to Phase 2b. Phase 2a shows the banner (TurnBannerWidget.ShowPersistent)
    /// from VersusMatchController, waits 2s, fires this event, and navigates home.
    /// </summary>
    public class VersusResultHandler : MonoBehaviour
    {
        [Tooltip("Fallback reward if ModesDatabaseCSV is unavailable. Matches the hardcoded " +
                 "fallback in ModesDatabaseCSV.AddFallbackModes() for 'versus_1v1'.")]
        [SerializeField] int _fallbackReward = 200;

        void OnEnable()
        {
            GameSession.OnMatchComplete += HandleMatchComplete;
            // Push the CSV-keyed stroke cap to GameSession so VersusMatchController
            // (Golfin.Physics.Viewer, which can't reference ModesDatabaseCSV) can read it
            // via the cross-asmdef GameSession bridge. Called here rather than Awake so
            // ModesDatabaseCSV.Instance is available (it is DontDestroyOnLoad from ShellScene).
            PushStrokeCapToGameSession();
        }

        void OnDisable()
        {
            GameSession.OnMatchComplete -= HandleMatchComplete;
        }

        /// <summary>
        /// Read versusStrokeCapOverPar from modes.csv "versus_1v1" row and write it to
        /// GameSession.VersusStrokeCapOverPar. VersusMatchController reads GameSession — this
        /// is the clean asmdef-boundary crossing. Default 5 if the column is absent.
        /// </summary>
        void PushStrokeCapToGameSession()
        {
            int cap = 5; // default per SPEC §11
            if (ModesDatabaseCSV.Instance != null)
            {
                var mode = ModesDatabaseCSV.Instance.GetMode("versus_1v1");
                if (mode != null && mode.versusStrokeCapOverPar > 0)
                    cap = mode.versusStrokeCapOverPar;
            }
            GameSession.VersusStrokeCapOverPar = cap;
            Debug.Log($"[VersusResultHandler] versusStrokeCapOverPar read from CSV = {cap} (written to GameSession).");
        }

        void HandleMatchComplete(GameSession.MatchOutcome outcome, int p1Strokes, int p2Strokes)
        {
            Debug.Log($"[VersusResultHandler] Match complete: outcome={outcome} P1={p1Strokes} P2={p2Strokes}");

            // Order 350: publish match stinger via SfxBus (one event per outcome).
            SfxId stingerId = outcome switch
            {
                GameSession.MatchOutcome.P1Win => SfxId.MatchWin,
                GameSession.MatchOutcome.P2Win => SfxId.MatchLose,
                _                             => SfxId.MatchDraw,
            };
            SfxBus.Play(stingerId);

            // Grant RP for P1 win only.
            if (outcome == GameSession.MatchOutcome.P1Win)
            {
                int reward = GetVersusReward();
                if (RewardPointsManager.Instance != null)
                {
                    RewardPointsManager.Instance.EarnPoints(reward);
                    Debug.Log($"[VersusResultHandler] P1 WIN — granted {reward} RP.");
                }
                else
                {
                    Debug.LogWarning("[VersusResultHandler] RewardPointsManager.Instance is null — cannot grant RP.");
                }
            }
            else
            {
                Debug.Log($"[VersusResultHandler] {outcome} — 0 RP granted.");
            }

            // Reset session flag before unloading so solo state is clean on next entry.
            GameSession.IsVersus = false;

            // Navigate home: unload LabScaffold + Hole_NN_Geo, return to Shell.
            StartCoroutine(UnloadAndReturnHome());
        }

        IEnumerator UnloadAndReturnHome()
        {
            // Brief pause so the match-end banner (ShowPersistent) is visible before unload.
            // VersusMatchController already waits 2s before firing MarkMatchComplete, so
            // this is an additional 0.5s cushion for the RP grant to visually register.
            yield return new WaitForSeconds(0.5f);

            if (GameplaySceneLoader.Instance != null)
            {
                yield return StartCoroutine(GameplaySceneLoader.Instance.UnloadGameplay());
                Debug.Log("[VersusResultHandler] Gameplay unloaded — returned to ShellScene.");
            }
            else
            {
                Debug.LogWarning("[VersusResultHandler] GameplaySceneLoader.Instance is null — cannot auto-navigate home. " +
                                 "Player must navigate manually.");
            }
        }

        /// <summary>
        /// Read the versus_1v1 reward from ModesDatabaseCSV. Falls back to _fallbackReward
        /// (200) if the database is unavailable (e.g. ShellScene is not loaded).
        /// </summary>
        int GetVersusReward()
        {
            if (ModesDatabaseCSV.Instance != null)
            {
                var mode = ModesDatabaseCSV.Instance.GetMode("versus_1v1");
                if (mode != null && mode.rewards > 0)
                    return mode.rewards;
            }
            return _fallbackReward;
        }
    }
}
