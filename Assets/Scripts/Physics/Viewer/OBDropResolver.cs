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

        /// <summary>
        /// Water drop point: the spot where the ball last crossed the hazard margin.
        ///
        /// Walks the precomputed trajectory SAMPLES backwards from the end (which is in the
        /// water) and returns the first sample whose XZ is not over water — i.e. the last
        /// airborne point still over land. That is the "last crossed the edge of the penalty
        /// area" reference real golf drops from.
        ///
        /// Why samples and not <see cref="Resolve"/>'s terrain hits: a ball can carry a long way
        /// over land and splash without ever bouncing again, so the last *terrain hit* can sit
        /// tens of metres behind the crossing (on Hole 6, firing at the lake from x=20 crosses the
        /// margin at x≈1.3 but has no land hit at all, so the old path fell all the way back to
        /// the shot origin — a ~19 m over-penalty). Walking backwards also handles a ball that
        /// skips out of the water and back in: it finds the LAST crossing, which is the correct one.
        ///
        /// The returned point is naturally a touch short of the true margin — the crossing lies
        /// between this sample and the next — so the drop is never nearer the hole. Y is airborne
        /// and irrelevant: the caller's RepositionBallWithLookDir snaps it to the surface.
        /// </summary>
        public static bool TryFindWaterEntry(Trajectory trajectory, ISurfaceProvider provider, out Vector3 pos)
        {
            pos = Vector3.zero;
            var samples = trajectory?.samples;
            if (samples == null || provider == null || samples.Count == 0) return false;

            for (int i = samples.Count - 1; i >= 0; i--)
            {
                var p = samples[i].position;
                float x = p.x.ToFloat();
                float z = p.z.ToFloat();
                if (provider.Classify(fp.FromFloat(x), fp.FromFloat(z)) == SurfaceType.Water) continue;
                pos = new Vector3(x, p.y.ToFloat(), z);
                return true;
            }
            return false;   // never over land at all (e.g. teed off inside the hazard)
        }

        /// <summary>
        /// K10 ob_recovery_fixes — real-golf drop rule (Cesar ruling 2026-08-05).
        /// Boundary OB is STROKE AND DISTANCE: the ball drops at the previous shot
        /// origin (a first-shot boundary OB therefore goes back on the tee).
        ///
        /// Water drops at the hazard margin the ball last crossed (<see cref="TryFindWaterEntry"/>),
        /// which is relief near the entry and never nearer the hole. Without a classifier
        /// (flat-ground / no-hole sessions) or for a flight that was never over land, it falls back
        /// to the original last-dry-touch <see cref="Resolve"/>, so those paths are unchanged.
        /// </summary>
        public static Vector3 ResolveByRule(Trajectory trajectory, Vector3 lastShotOrigin, bool isWater,
                                            ISurfaceProvider provider = null)
        {
            if (!isWater) return lastShotOrigin;
            if (TryFindWaterEntry(trajectory, provider, out var entry)) return entry;
            return Resolve(trajectory, lastShotOrigin);
        }
    }
}
