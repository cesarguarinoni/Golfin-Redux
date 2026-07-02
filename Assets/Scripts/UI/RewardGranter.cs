using System.Collections.Generic;
using UnityEngine;
using Golfin.Roster;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Shared static reward grant utility — extracted from HoleCompleteModalController.GrantRewards
    /// so BOTH hole-complete and versus result paths use one grant implementation (DRY).
    ///
    /// Extraction note (Stage 2, 2026-07-02): the private switch in HoleCompleteModalController was
    /// copied verbatim into Grant(); HoleCompleteModalController.GrantRewards() now calls Grant().
    /// Caller is responsible for guarding: call Grant() only when the outcome warrants a payout
    /// (e.g. Success/Win) and only once per event (guard with a _rewardsGranted bool).
    ///
    /// Supports: Points → RewardPointsManager.EarnPoints
    ///           RepairKit → ItemManager.AddItems("repairkit_common")
    ///           Ball → BallManager.AddBalls("ball_golfin")
    ///
    /// To add a new type (e.g. GachaTicket): extend the switch here ONLY — all callers inherit it.
    /// </summary>
    public static class RewardGranter
    {
        private const string RepairKitDefaultId = "repairkit_common";
        private const string BallDefaultId      = "ball_golfin";

        /// <summary>
        /// Grants all rewards in the list.
        /// Null list is treated as empty (no-op).
        /// Zero-amount rewards are silently skipped.
        /// </summary>
        public static void Grant(List<HoleReward> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                Debug.Log("[RewardGranter] Grant called with empty/null list — nothing to grant.");
                return;
            }

            foreach (var reward in rewards)
            {
                if (reward == null || reward.amount <= 0) continue;

                switch (reward.type)
                {
                    case RewardType.Points:
                        if (RewardPointsManager.Instance != null)
                        {
                            RewardPointsManager.Instance.EarnPoints(reward.amount);
                            Debug.Log($"[RewardGranter] Granted Points ×{reward.amount}");
                        }
                        else
                        {
                            Debug.LogWarning("[RewardGranter] RewardPointsManager.Instance is null — cannot grant Points.");
                        }
                        break;

                    case RewardType.RepairKit:
                        if (ItemManager.Instance != null)
                        {
                            ItemManager.Instance.AddItems(RepairKitDefaultId, reward.amount);
                            Debug.Log($"[RewardGranter] Granted RepairKit ({RepairKitDefaultId}) ×{reward.amount}");
                        }
                        else
                        {
                            Debug.LogWarning("[RewardGranter] ItemManager.Instance is null — cannot grant RepairKit.");
                        }
                        break;

                    case RewardType.Ball:
                        if (BallManager.Instance != null)
                        {
                            BallManager.Instance.AddBalls(BallDefaultId, reward.amount);
                            Debug.Log($"[RewardGranter] Granted Ball ({BallDefaultId}) ×{reward.amount}");
                        }
                        else
                        {
                            Debug.LogWarning("[RewardGranter] BallManager.Instance is null — cannot grant Ball.");
                        }
                        break;

                    default:
                        Debug.LogWarning($"[RewardGranter] Unknown RewardType: {reward.type} — skipped.");
                        break;
                }
            }
        }
    }
}
