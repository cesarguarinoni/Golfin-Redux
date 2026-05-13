using Golfin.Physics;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2f: pure decision logic for auto-switching club based on AtRest surface.
    /// Returns the target club index, or -1 to mean "no change".
    /// </summary>
    public static class PutterModeSurfaceController
    {
        /// <summary>
        /// Given the current club, the surface at AtRest, and the last non-putter
        /// club index, returns the target club index. Returns -1 if no switch is
        /// needed (idempotent).
        /// </summary>
        /// <param name="currentClubIndex">Current player club index.</param>
        /// <param name="putterIndex">Index of the Putter in LabClubs.</param>
        /// <param name="endSurface">Surface under the ball at AtRest.</param>
        /// <param name="lastNonPutterClubIndex">Cached fallback for auto-exit.</param>
        public static int DecideTargetClub(
            int currentClubIndex, int putterIndex,
            SurfaceType endSurface, int lastNonPutterClubIndex)
        {
            bool onGreen      = endSurface == SurfaceType.Green;
            bool inPutterMode = currentClubIndex == putterIndex;

            if (onGreen && !inPutterMode) return putterIndex;
            if (!onGreen && inPutterMode) return lastNonPutterClubIndex;
            return -1; // no change
        }
    }
}
