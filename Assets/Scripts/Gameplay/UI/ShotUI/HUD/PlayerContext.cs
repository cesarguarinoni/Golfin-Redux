using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    public static class PlayerContext
    {
        public static string DisplayName = "PLAYER";
        public static int    Level       = 1;
        public static Sprite Portrait    = null;

        public static event System.Action OnChanged;
        public static void Raise() => OnChanged?.Invoke();

        public static void Reset()
        {
            DisplayName = "PLAYER";
            Level       = 1;
            Portrait    = null;
            OnChanged?.Invoke();
        }
    }
}
