using UnityEngine;

namespace Golfin.Course
{
    // Attached to green GameObjects. Submesh 0 = putting surface,
    // submesh 1 = collar (first cut). Used by ball physics to
    // determine surface-specific roll/friction.
    public class GreenSurfaceInfo : MonoBehaviour
    {
        public const int SubmeshGreen  = 0;
        public const int SubmeshCollar = 1;
    }
}
