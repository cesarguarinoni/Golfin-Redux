using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2e: computes the drop position when a shot ends OB.
    /// Walks the trajectory's terrain hits from latest to earliest, finds the
    /// first hit whose Surface is neither Water nor OOB, and returns that
    /// position. Falls back to the player's previous shot origin if no safe
    /// hit exists (e.g. tee shot straight into water hazard with no land touch).
    /// </summary>
    public static class OBDropResolver
    {
        public static Vector3 Resolve(Trajectory trajectory, Vector3 fallbackOrigin)
        {
            if (trajectory == null || trajectory.terrainHits == null) return fallbackOrigin;
            var hits = trajectory.terrainHits;
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                var s = hits[i].Surface;
                if (s == SurfaceType.Water || s == SurfaceType.OOB) continue;
                var p = hits[i].Position;
                return new Vector3(p.x.ToFloat(), p.y.ToFloat(), p.z.ToFloat());
            }
            return fallbackOrigin;
        }
    }
}
