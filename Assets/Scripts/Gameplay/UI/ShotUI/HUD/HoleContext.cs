using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    public static class HoleContext
    {
        public static int    HoleNumber         = 1;
        public static int    Par                = 4;
        public static int    ChampionshipYards  = 0;
        public static string CourseName         = "LOMOND";
        public static string TeeName            = "REGULAR";
        public static Vector3 GreenCentroidWorld;

        public static event System.Action OnChanged;
        public static void Raise() => OnChanged?.Invoke();

        public static void Reset()
        {
            HoleNumber        = 1;
            Par               = 4;
            ChampionshipYards = 0;
            CourseName        = "LOMOND";
            TeeName           = "REGULAR";
            GreenCentroidWorld = Vector3.zero;
            OnChanged?.Invoke();
        }
    }
}
