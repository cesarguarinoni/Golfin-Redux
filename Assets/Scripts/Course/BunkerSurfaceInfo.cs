using UnityEngine;
namespace Golfin.Course
{
    // Attached to bunker GameObjects. Submesh 0 = sand,
    // submesh 1 = lip (sand-to-grass transition). Used by ball
    // physics to determine surface-specific lie/friction.
    public class BunkerSurfaceInfo : MonoBehaviour
    {
        public const int SubmeshSand = 0;
        public const int SubmeshLip  = 1;
    }
}
