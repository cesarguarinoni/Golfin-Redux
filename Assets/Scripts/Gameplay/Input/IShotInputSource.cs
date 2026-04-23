using UnityEngine;

namespace Golfin.Gameplay.Input
{
    public interface IShotInputSource
    {
        bool    IsTouching             { get; }
        Vector2 TouchPositionPx        { get; }  // current position
        Vector2 TouchOriginPx          { get; }  // touch-down origin
        Vector2 TouchVelocityPxPerSec  { get; }  // smoothed
    }
}
