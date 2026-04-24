using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Testable static helper for type-aware surface snapping.
    /// PhysicsLabController delegates SurfaceSnap here; tests call this directly.
    /// </summary>
    public static class PlacementSnapHelper
    {
        // preferredSurfaceTypeValue: Golfin.Course.SurfaceType int (1=Green, 4=Bunker, etc.) or null.
        // Among all downward hits from Y=500, prefers the HIGHEST Y on the preferred surface type.
        // Falls back to the first (closest from above) non-ball hit when no preferred match.
        public static float Snap(float x, float z, float defaultY,
                                 int? preferredSurfaceTypeValue = null,
                                 GameObject excludeBallGO = null)
        {
            var hits = UnityEngine.Physics.RaycastAll(
                new Vector3(x, 500f, z), Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore);

            if (hits.Length == 0) return defaultY;

            // Optionally exclude the ball itself (belt+suspenders; colliders are disabled, but safe).
            GameObject ballGO = excludeBallGO != null ? excludeBallGO
                              : BallAnimator.Instance != null ? BallAnimator.Instance.gameObject
                              : null;

            // Reflect into Golfin.Course.SurfaceMarker (Assembly-CSharp, not directly referenceable).
            System.Type smType = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");
            System.Reflection.FieldInfo stField = smType?.GetField("surfaceType");

            float bestY          = float.NegativeInfinity;
            float fallbackY      = float.NegativeInfinity;
            bool  foundPreferred = false;
            bool  foundFallback  = false;

            foreach (var h in hits)
            {
                if (ballGO != null && h.collider.transform.IsChildOf(ballGO.transform))
                    continue;

                bool isPreferred = false;
                if (preferredSurfaceTypeValue.HasValue && smType != null && stField != null)
                {
                    var marker = h.collider.GetComponentInParent(smType);
                    if (marker != null)
                        isPreferred = (int)stField.GetValue(marker) == preferredSurfaceTypeValue.Value;
                }

                if (isPreferred)
                {
                    // Among preferred-type hits, pick the highest Y (the visible top surface).
                    if (!foundPreferred || h.point.y > bestY)
                    {
                        bestY = h.point.y;
                        foundPreferred = true;
                    }
                }
                else if (!foundFallback)
                {
                    // First non-ball, non-preferred hit (PhysX order = closest from Y=500 down).
                    fallbackY    = h.point.y;
                    foundFallback = true;
                }
            }

            if (foundPreferred) return bestY;
            if (foundFallback)  return fallbackY;
            return defaultY;
        }
    }
}
