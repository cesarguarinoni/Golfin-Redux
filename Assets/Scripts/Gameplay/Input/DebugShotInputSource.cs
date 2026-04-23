using UnityEngine;

namespace Golfin.Gameplay.Input
{
    // Inspector-configurable IShotInputSource for test scenes and in-Editor demos.
    public class DebugShotInputSource : MonoBehaviour, IShotInputSource
    {
        public bool    IsTouching            { get; set; }
        public Vector2 TouchPositionPx       { get; set; }
        public Vector2 TouchOriginPx         { get; set; }
        public Vector2 TouchVelocityPxPerSec { get; set; }
    }
}
