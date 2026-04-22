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
            if (UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit,
                                            RaycastLength, ~0,
                                            QueryTriggerInteraction.Collide))
                return fp.FromFloat(hit.point.y);
            return fp.Zero;
        }
    }
}
