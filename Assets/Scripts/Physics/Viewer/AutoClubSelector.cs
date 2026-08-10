using System.Collections.Generic;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// auto_club_selection: pure decision logic for pre-selecting the player's club
    /// before every shot. Mirrors <see cref="PutterModeSurfaceController"/>: no scene,
    /// no statics read, primitives + POCOs in, bag index out — unit-testable.
    ///
    /// Rules (Cesar 2026-08-10):
    ///   1. Tee shot  → always the Driver.
    ///   2. Off tee   → NEVER the Driver (the player may still pick it manually; the
    ///                  K11 selector gate is deliberately NOT extended to the driver).
    ///   3. Green     → §2f owns the putter; this selector no-ops in putter mode.
    ///   4. Otherwise → the shortest club in the bag that still reaches the pin.
    /// </summary>
    public static class AutoClubSelector
    {
        /// <summary>Same conversion constant used across the physics/viewer code.</summary>
        public const float YardsPerMeter = 1.09361f;

        /// <summary>
        /// Picks the equipped-bag index the game should pre-select for the next shot.
        /// Returns -1 for "leave selection alone".
        /// </summary>
        /// <param name="distToPinM">Flat XZ distance ball→pin, meters.</param>
        /// <param name="isTeeShot">BallIsOnTee() at decision time.</param>
        /// <param name="inPutterMode">§2f putter mode (ball at rest on Green).</param>
        /// <param name="bag">ClubContext.EquippedBag snapshot.</param>
        /// <param name="putterLabClubIndex">LabClubs index of the putter (PhysicsLabController.PutterIndex).</param>
        public static int SelectBestClub(float distToPinM, bool isTeeShot, bool inPutterMode,
                                         IReadOnlyList<ClubEntry> bag, int putterLabClubIndex)
        {
            // 1. Nothing to pick from.
            if (bag == null || bag.Count == 0) return -1;

            // 2. Green: §2f already forced the putter — never fight it.
            if (inPutterMode) return -1;

            // 3. Tee shot → the bag's Driver, if it has one.
            if (isTeeShot)
            {
                for (int i = 0; i < bag.Count; i++)
                {
                    var e = bag[i];
                    if (e != null && e.IsDriver) return i;
                }
                // No driver in the bag → fall through to the distance rule. Rule 4 excludes
                // drivers by definition, so "no driver" simply means nothing is excluded.
            }

            // 4. Candidates: everything that is neither a driver nor the putter.
            float distYd = distToPinM * YardsPerMeter;

            int bestReach = -1;      // smallest Distance that is >= distYd
            int bestLongest = -1;    // largest Distance overall (overshoot-all fallback)

            for (int i = 0; i < bag.Count; i++)
            {
                var e = bag[i];
                if (e == null) continue;
                if (e.IsDriver) continue;
                if (e.LabClubIndex == putterLabClubIndex) continue;

                // 6. Longest non-driver, for the "nothing reaches" fallback.
                //    Strict > keeps the lowest bag index on a tie (rule 7).
                if (bestLongest < 0 || e.Distance > bag[bestLongest].Distance)
                    bestLongest = i;

                // 5. Shortest club that still reaches. Strict < keeps the lowest index on a tie.
                if (e.Distance >= distYd &&
                    (bestReach < 0 || e.Distance < bag[bestReach].Distance))
                    bestReach = i;
            }

            if (bestReach >= 0) return bestReach;   // 5.
            return bestLongest;                     // 6. (-1 when the candidate set is empty)
        }
    }
}
