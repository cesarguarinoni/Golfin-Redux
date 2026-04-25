using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    public sealed class SceneGroundProvider : IGroundProvider
    {
        private const float RaycastFromY  = 500f;
        private const float RaycastLength = 1000f;

        public fp SampleHeight(fp worldX, fp worldZ)
        {
            var origin = new Vector3(worldX.ToFloat(), RaycastFromY, worldZ.ToFloat());
            var hits = UnityEngine.Physics.RaycastAll(origin, Vector3.down, RaycastLength,
                                                      ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return fp.Zero;

            // Highest hit wins — overlapping zone meshes (e.g. fringe-on-fairway vs green)
            // race on first-hit order in PhysX. The visually-topmost surface is always
            // the one the ball should sit on.
            float bestY = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
                if (hits[i].point.y > bestY) bestY = hits[i].point.y;
            return fp.FromFloat(bestY);
        }

        /// <summary>
        /// Surface-aware override. Among all downward hits at (worldX, worldZ):
        ///   - If any hit's collider carries a SurfaceMarker.Type matching <paramref name="preferred"/>,
        ///     return the highest Y among those preferred hits.
        ///   - Otherwise fall back to max-Y of all hits (same as 2-arg).
        /// This keeps a bunker ball below the surrounding terrain and a green ball below
        /// the fringe-overlap collider, without changing the airborne max-Y path.
        /// </summary>
        public fp SampleHeight(fp worldX, fp worldZ, SurfaceType preferred)
        {
            var origin = new Vector3(worldX.ToFloat(), RaycastFromY, worldZ.ToFloat());
            var hits = UnityEngine.Physics.RaycastAll(origin, Vector3.down, RaycastLength,
                                                      ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return fp.Zero;

            float preferredBestY = float.NegativeInfinity;
            float fallbackBestY  = float.NegativeInfinity;
            bool  foundPreferred = false;

            for (int i = 0; i < hits.Length; i++)
            {
                float y = hits[i].point.y;
                if (y > fallbackBestY) fallbackBestY = y;

                var marker = hits[i].collider.GetComponentInParent<SurfaceMarker>();
                if (marker != null && marker.Type == preferred)
                {
                    if (y > preferredBestY)
                    {
                        preferredBestY = y;
                        foundPreferred = true;
                    }
                }
            }

            return fp.FromFloat(foundPreferred ? preferredBestY : fallbackBestY);
        }
    }
}
