using UnityEngine;

namespace Golfin.Gameplay.Input
{
    // EditMode-friendly implementation. Tests set fields directly.
    public class SyntheticInputSource : IShotInputSource
    {
        public bool    IsTouching            { get; set; }
        public Vector2 TouchPositionPx       { get; set; }
        public Vector2 TouchOriginPx         { get; set; }
        public Vector2 TouchVelocityPxPerSec { get; set; }
    }
}
