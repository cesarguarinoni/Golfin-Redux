using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using GolfinRedux.UI;
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
                 "fallback in ModesDatabaseCSV.AddFallbackModes() for 'versus_1v1'. " +
                 "Rebalanced 200 → 20 with RP_REBALANCE.md (2026-08-12).")]
        [SerializeField] int _fallbackReward = 20;

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

            // Stage 2: read WIN reward list from modes.csv (DRY via RewardGranter).
            // Grant rewards on WIN only; lose/draw = 0 rewards granted.
            // BUT: always pass the WIN reward list to the screen so it can show
            // the same slot(s) greyed on lose/draw (SPEC §2/§3: "what you would have gotten").
            List<HoleReward> winRewardList = GetVersusRewardList();
            if (outcome == GameSession.MatchOutcome.P1Win)
            {
                RewardGranter.Grant(winRewardList, Golfin.Economy.PointsActions.VersusWin);
                Debug.Log($"[VersusResultHandler] P1 WIN — granted {winRewardList.Count} reward(s) via RewardGranter.");
            }
            else
            {
                Debug.Log($"[VersusResultHandler] {outcome} — 0 rewards granted; win list passed for greyed display.");
            }

            // Show the result modal after the banner sequence.
            // VersusMatchController already waits 2s before firing MarkMatchComplete;
            // we wait an additional 0.5s so the banner is comfortably visible.
            // Always pass winRewardList — the controller dims the slot(s) on lose/draw.
            StartCoroutine(ShowResultAfterBanner(outcome, p1Strokes, p2Strokes, winRewardList));
        }

        IEnumerator ShowResultAfterBanner(
            GameSession.MatchOutcome outcome,
            int                      p1Strokes,
            int                      p2Strokes,
            List<HoleReward>         rewardList)
        {
            yield return new WaitForSeconds(0.5f);

            if (_resultModal == null)
            {
                Debug.LogWarning("[VersusResultHandler] _resultModal is null — cannot show result modal. " +
                                 "Falling back to home navigation.");
                // Fallback: unload and return home so the game doesn't get stuck. ExitToScreen
                // does the routing this branch always claimed to do but never actually did —
                // the old code unloaded and stopped, leaving the player on the empty shell
                // scene with no screen up — and it does the teardown behind the curtain.
                // ExitToScreen hosts itself on the loader, which matters here: a copy of this
                // handler lives in LabScaffold and is destroyed by the unload.
                var loader = Golfin.UI.GameplayTransition.GameplaySceneLoader.Instance;
                if (loader != null)
                    yield return loader.ExitToScreen(
                        GolfinRedux.UI.ScreenId.Home,
                        () => GameSession.IsVersus = false);
                else
                    GameSession.IsVersus = false;
                yield break;
            }

            // Read live match data from MatchContext (set at matchmaking + during play).
            MatchContext.Player localPlayer    = MatchContext.Players[0];
            MatchContext.Player opponentPlayer = MatchContext.Players[1];
            int holeNumber = GameSession.CurrentHoleNumber;

            Debug.Log($"[VersusResultHandler] Showing result modal — hole={holeNumber} " +
                      $"local={localPlayer.DisplayName} opp={opponentPlayer.DisplayName} " +
                      $"rewardSlots={rewardList.Count}");

            // Stage 2: pass rewardList so the screen controller can bind the reward row.
            _resultModal.ShowResult(outcome, localPlayer, opponentPlayer, holeNumber, rewardList);
        }

        /// <summary>
        /// Stage 2: read the versus_1v1 rewardList (List&lt;HoleReward&gt;) from ModesDatabaseCSV.
        /// Falls back to a single Points×_fallbackReward entry if the database is unavailable.
        /// </summary>
        List<HoleReward> GetVersusRewardList()
        {
            if (ModesDatabaseCSV.Instance != null)
            {
                var mode = ModesDatabaseCSV.Instance.GetMode("versus_1v1");
                if (mode != null && mode.rewardList != null && mode.rewardList.Count > 0)
                    return mode.rewardList;
            }
            // Fallback: single Points reward matching the hardcoded fallback amount.
            Debug.LogWarning("[VersusResultHandler] ModesDatabaseCSV unavailable — using fallback rewardList.");
            return new List<HoleReward> { new HoleReward(RewardType.Points, _fallbackReward) };
        }
    }
}
