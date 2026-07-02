using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Roster;
using GolfinRedux.UI.ModeSelect;
using Golfin.Audio.Events;
using Golfin.UI.Matchmaking;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// ShellScene-resident handler for 1v1 match completion.
    ///
    /// Stage 1: drop auto-navigate-home. After the match banner sequence,
    /// present VersusResultModalController (the new result modal).
    /// NEW MATCH on the modal handles the D3 re-queue via MatchmakingModalController.
    ///
    /// Stage 0 behaviour (auto-home) is removed; RP is still silently granted
    /// until Stage 2 hooks it into the reward-row display.
    ///
    /// Subscribes to GameSession.OnMatchComplete (fired by VersusMatchController
    /// via GameSession.MarkMatchComplete). Lives in Assembly-CSharp so it can reach
    /// both RewardPointsManager (Golfin.Roster) and ModesDatabaseCSV (Assembly-CSharp).
    /// This event-bridge pattern mirrors HoleCompleteModalController subscribing to
    /// GameSession.OnHoleComplete.
    /// </summary>
    public class VersusResultHandler : MonoBehaviour
    {
        [Tooltip("Fallback reward if ModesDatabaseCSV is unavailable. Matches the hardcoded " +
                 "fallback in ModesDatabaseCSV.AddFallbackModes() for 'versus_1v1'.")]
        [SerializeField] int _fallbackReward = 200;

        [Tooltip("VersusResultModalController in ShellScene — wire in Inspector.")]
        [SerializeField] VersusResultModalController _resultModal = null!;

        void OnEnable()
        {
            GameSession.OnMatchComplete += HandleMatchComplete;
            // Push the CSV-keyed stroke cap to GameSession so VersusMatchController
            // (Golfin.Physics.Viewer, which can't reference ModesDatabaseCSV) can read it
            // via the cross-asmdef GameSession bridge.
            PushStrokeCapToGameSession();
        }

        void OnDisable()
        {
            GameSession.OnMatchComplete -= HandleMatchComplete;
        }

        /// <summary>
        /// Read versusStrokeCapOverPar from modes.csv "versus_1v1" row and write it to
        /// GameSession.VersusStrokeCapOverPar. VersusMatchController reads GameSession.
        /// Default 5 if the column is absent.
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

            // Publish match stinger via SfxBus.
            SfxId stingerId = outcome switch
            {
                GameSession.MatchOutcome.P1Win => SfxId.MatchWin,
                GameSession.MatchOutcome.P2Win => SfxId.MatchLose,
                _                             => SfxId.MatchDraw,
            };
            SfxBus.Play(stingerId);

            // Stage 1: grant RP silently now (Stage 2 will show it in the reward row).
            if (outcome == GameSession.MatchOutcome.P1Win)
            {
                int reward = GetVersusReward();
                if (RewardPointsManager.Instance != null)
                {
                    RewardPointsManager.Instance.EarnPoints(reward);
                    Debug.Log($"[VersusResultHandler] P1 WIN — granted {reward} RP (silent, Stage 2 will display).");
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

            // Stage 1: show the result modal after the banner sequence.
            // VersusMatchController already waits 2s before firing MarkMatchComplete;
            // we wait an additional 0.5s so the banner is comfortably visible.
            StartCoroutine(ShowResultAfterBanner(outcome, p1Strokes, p2Strokes));
        }

        IEnumerator ShowResultAfterBanner(GameSession.MatchOutcome outcome, int p1Strokes, int p2Strokes)
        {
            yield return new WaitForSeconds(0.5f);

            if (_resultModal == null)
            {
                Debug.LogWarning("[VersusResultHandler] _resultModal is null — cannot show result modal. " +
                                 "Falling back to home navigation.");
                // Fallback: unload and return home so the game doesn't get stuck.
                GameSession.IsVersus = false;
                if (Golfin.UI.GameplayTransition.GameplaySceneLoader.Instance != null)
                    yield return StartCoroutine(
                        Golfin.UI.GameplayTransition.GameplaySceneLoader.Instance.UnloadGameplay());
                yield break;
            }

            // Read live match data from MatchContext (set at matchmaking + during play).
            MatchContext.Player localPlayer    = MatchContext.Players[0];
            MatchContext.Player opponentPlayer = MatchContext.Players[1];
            int holeNumber = GameSession.CurrentHoleNumber;

            Debug.Log($"[VersusResultHandler] Showing result modal — hole={holeNumber} " +
                      $"local={localPlayer.DisplayName} opp={opponentPlayer.DisplayName}");

            _resultModal.ShowResult(outcome, localPlayer, opponentPlayer, holeNumber);
        }

        /// <summary>
        /// Read the versus_1v1 reward from ModesDatabaseCSV. Falls back to _fallbackReward
        /// (200) if the database is unavailable.
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
