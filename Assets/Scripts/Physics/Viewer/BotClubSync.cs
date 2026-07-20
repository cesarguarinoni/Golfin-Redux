// BotClubSync.cs — Order 762: shared ClubContext sync helper for all bots.
//
// Resolves a desired lab-club index → the nearest available club in the
// equipped bag, then pushes all ClubContext fields so the LIVE stat path
// fires the intended club instead of the driver that ClubContextPopulator
// set at hole load.
//
// Both BotDriver (#if UNITY_EDITOR, lab) and VersusBot (production, 1v1)
// had — or would have — identical inline blocks for this. This helper
// lives in the shared Golfin.Physics.Viewer asmdef (which already
// references Golfin.Gameplay.UI where ClubContext lives) so both bots
// call the same well-tested logic.
//
// NOTE: No #if UNITY_EDITOR — VersusBot is production code and this helper
//       must be present in production builds.
using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.UI.HUD; // ClubContext, ClubEntry

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Syncs <see cref="ClubContext"/> to the equipped bag entry that best
    /// matches the requested lab-club index before a LIVE-path shot is fired.
    /// </summary>
    public static class BotClubSync
    {
        /// <summary>
        /// Pushes the equipped bag entry for <paramref name="desiredLabClubIndex"/>
        /// into <see cref="ClubContext"/> so the LIVE stat provider resolves the
        /// intended club rather than the driver that was set at hole load.
        ///
        /// Lookup is EXACT first; if the bag lacks the requested lab index, falls
        /// back to the nearest available club (largest index ≤ desired, or if
        /// none exists, smallest index &gt; desired). This mirrors real-player
        /// behaviour: a player without a specific club reaches for the closest one
        /// they carry.
        ///
        /// Call this AFTER <c>PhysicsLabController.SetClub(labIndex)</c> and
        /// BEFORE <c>ShotController.ClearStatBundleOverride()</c>.
        /// </summary>
        /// <param name="desiredLabClubIndex">The lab club index the bot selected.</param>
        /// <param name="logTag">Prefix for diagnostic log lines. Pass null to suppress.</param>
        /// <returns>
        /// The actual <c>LabClubIndex</c> resolved from the bag (may differ from
        /// <paramref name="desiredLabClubIndex"/> when the bag lacks an exact match).
        /// Returns <paramref name="desiredLabClubIndex"/> unchanged if the bag is
        /// empty or unresolvable (caller keeps the original SetClub).
        /// </returns>
        public static int SyncToClubContext(int desiredLabClubIndex, string logTag = null)
        {
            List<ClubEntry> bag = ClubContext.EquippedBag;

            if (bag == null || bag.Count == 0)
            {
                if (logTag != null)
                    Debug.LogWarning($"[{logTag}] BotClubSync: EquippedBag empty — ClubContext unchanged, LIVE path may fire the wrong club");
                return desiredLabClubIndex;
            }

            // ── Exact lookup ───────────────────────────────────────────────────
            int bagIdx = bag.FindIndex(e => e.LabClubIndex == desiredLabClubIndex);

            // ── Nearest-available fallback ─────────────────────────────────────
            if (bagIdx < 0)
            {
                int bestBelow = -1, bestBelowIdx = -1;
                int bestAbove = int.MaxValue, bestAboveIdx = -1;
                for (int i = 0; i < bag.Count; i++)
                {
                    int li = bag[i].LabClubIndex;
                    if (li <= desiredLabClubIndex && li > bestBelow) { bestBelow = li; bestBelowIdx = i; }
                    if (li >  desiredLabClubIndex && li < bestAbove) { bestAbove = li; bestAboveIdx = i; }
                }
                bagIdx = bestBelowIdx >= 0 ? bestBelowIdx : bestAboveIdx;
            }

            if (bagIdx < 0)
            {
                if (logTag != null)
                    Debug.LogWarning($"[{logTag}] BotClubSync: could not resolve labClubIndex={desiredLabClubIndex} in bag (count={bag.Count}) — ClubContext unchanged");
                return desiredLabClubIndex;
            }

            ClubEntry entry = bag[bagIdx];

            // ── Push all ClubContext fields (mirrors LabInventoryStub.SelectClubByIndex) ─
            ClubContext.SelectedClubId    = entry.ClubId;
            ClubContext.SelectedIndex     = bagIdx;
            ClubContext.SelectedTypeLabel = entry.TypeLabel;
            ClubContext.SelectedPortrait  = entry.Portrait;
            ClubContext.SelectedDistance  = entry.Distance;
            ClubContext.RaiseSelectedChanged();

            if (logTag != null)
            {
                if (entry.LabClubIndex != desiredLabClubIndex)
                    Debug.Log($"[{logTag}] BotClubSync → '{entry.ClubId}' (bagIdx={bagIdx}, labIdx {desiredLabClubIndex}→{entry.LabClubIndex}, no exact club in bag)");
                else
                    Debug.Log($"[{logTag}] BotClubSync → '{entry.ClubId}' (bagIdx={bagIdx}, labIdx={desiredLabClubIndex})");
            }

            return entry.LabClubIndex;
        }
    }
}
