using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    public static class WindContext
    {
        public static float SpeedMph         = 0f;
        public static float DirectionDegrees = 0f; // 0=North, 90=East, clockwise; world-space (NOT camera-relative)

        public static event System.Action OnChanged;
        public static void Raise() => OnChanged?.Invoke();

        public static void Reset()
        {
            SpeedMph         = 0f;
            DirectionDegrees = 0f;
            OnChanged?.Invoke();
        }
    }
}
