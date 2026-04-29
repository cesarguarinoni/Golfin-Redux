using System;

namespace Golfin.Gameplay.UI.HUD
{
    public enum ShotMode { Straight, FadeDraw }

    public static class ShotModeContext
    {
        public static ShotMode Mode = ShotMode.Straight;
        public static event Action? OnChanged;
        public static void Toggle()
        {
            Mode = Mode == ShotMode.Straight ? ShotMode.FadeDraw : ShotMode.Straight;
            OnChanged?.Invoke();
        }
        public static void Reset() { Mode = ShotMode.Straight; OnChanged?.Invoke(); }
    }
}
