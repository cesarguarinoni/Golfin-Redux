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
    }
}
