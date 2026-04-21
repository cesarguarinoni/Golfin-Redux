using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// One-component-per-zone-mesh marker. Attach this to zone mesh GameObjects so
    /// SceneSurfaceProvider can classify them. All zone meshes that need explicit
    /// surface types must have this component; unmarked areas return Fairway.
    ///
    /// Greens and bunkers that carry legacy GreenSurfaceInfo / BunkerSurfaceInfo
    /// breadcrumbs (from Golfin.Course) are NOT automatically bridged here because
    /// Golfin.Course lives in Assembly-CSharp which Physics.Runtime cannot reference.
    /// Add SurfaceMarker(Green) and SurfaceMarker(Sand) to those zone roots when
    /// integrating Phase 4 with the live scene.
    /// </summary>
    public sealed class SurfaceMarker : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Fairway;
    }

    /// <summary>
    /// Surface classifier backed by scene geometry. Raycasts downward to find the top
    /// zone mesh collider at (x, z); reads SurfaceMarker. Returns Fairway if no marker.
    ///
    /// The raycast is non-deterministic but acceptable: surface classification is a
    /// static property of hole geometry — result is identical every call.
    /// </summary>
    public sealed class SceneSurfaceProvider : ISurfaceProvider
    {
        private const float RaycastFromY  = 500f;
        private const float RaycastLength = 1000f;
        private readonly int layerMask;

        public SceneSurfaceProvider(int layerMask = ~0) { this.layerMask = layerMask; }

        public SurfaceType Classify(fp worldX, fp worldZ)
        {
            var origin = new Vector3(worldX.ToFloat(), RaycastFromY, worldZ.ToFloat());
            if (!UnityEngine.Physics.Raycast(origin, Vector3.down, out var hit, RaycastLength,
                                             layerMask, QueryTriggerInteraction.Collide))
                return SurfaceType.Fairway;

            var marker = hit.collider.GetComponentInParent<SurfaceMarker>();
            return marker != null ? marker.Type : SurfaceType.Fairway;
        }
    }
}
