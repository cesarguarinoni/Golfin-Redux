using UnityEngine;

namespace Golfin.Course
{
    public enum SurfaceType
    {
        Fairway,
        Green,
        SemiRough,
        Rough,
        Bunker,
        Water,
        Tee,
        CartPath
    }

    public class SurfaceMarker : MonoBehaviour
    {
        public SurfaceType surfaceType;
    }
}
