using UnityEngine;

namespace Golfin.Course
{
    /// Marks a GameObject as a bridge for the export pipeline.
    /// Attach to the root of a bridge prefab. The exporter captures
    /// world position + yaw rotation + the two anchor endpoints.
    ///
    /// Anchor endpoints are where cart paths should meet the bridge.
    /// They're defined as local offsets along the bridge's local Z axis
    /// (forward) from the bridge's pivot.
    [DisallowMultipleComponent]
    public class BridgeAnchor : MonoBehaviour
    {
        [Tooltip("Optional bridge id. If empty, exporter auto-assigns 1..N.")]
        public string id = "";

        [Tooltip("Distance from pivot along local +Z to the 'far' anchor (meters).")]
        public float lengthForward = 3f;

        [Tooltip("Distance from pivot along local -Z to the 'near' anchor (meters).")]
        public float lengthBackward = 3f;

        [Tooltip("Path width this bridge expects to meet (meters). " +
                 "Informational — UHoleGeo uses it to sanity-check cart width.")]
        public float expectedPathWidth = 2.5f;

        private void OnDrawGizmos()
        {
            Vector3 a = transform.position + transform.forward * lengthForward;
            Vector3 b = transform.position - transform.forward * lengthBackward;
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 2f);
            Gizmos.DrawSphere(b, 2f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position,
                transform.position + transform.forward * (lengthForward + 1f));
        }
    }
}
