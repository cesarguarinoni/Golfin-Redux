using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    /// <summary>
    /// Static bus for the player's currently-selected club (drives the DRIVER action button + selector).
    /// Populated by ClubContextPopulator (Assembly-CSharp side) which reads BagManager/ClubManager.
    /// Widgets request a selection change via OnSelectionRequested; the populator (Assembly-CSharp side)
    /// listens and calls SelectByIndex() — this is the cross-asmdef return path.
    /// </summary>
    public static class ClubContext
    {
        public static string  SelectedClubId    = "";
        public static string  SelectedTypeLabel = "DRIVER";
        public static int     SelectedDistance  = 0;
        public static Sprite? SelectedPortrait  = null;
        public static System.Collections.Generic.List<ClubEntry> EquippedBag = new();
        public static int     SelectedIndex     = 0;

        public static event Action? OnSelectedChanged;
        public static event Action? OnBagChanged;
        public static event Action<int>? OnSelectionRequested;  // widget → populator

        public static void RaiseSelectedChanged() => OnSelectedChanged?.Invoke();
        public static void RaiseBagChanged()      => OnBagChanged?.Invoke();
        public static void RequestSelection(int idx) => OnSelectionRequested?.Invoke(idx);

        public static void Reset()
        {
            SelectedClubId    = "";
            SelectedTypeLabel = "DRIVER";
            SelectedDistance  = 0;
            SelectedPortrait  = null;
            EquippedBag.Clear();
            SelectedIndex     = 0;
            RaiseBagChanged();
            RaiseSelectedChanged();
        }
    }

    public class ClubEntry
    {
        public string  ClubId       = "";
        public string  TypeLabel    = "";
        public int     Distance     = 0;
        public Sprite? Portrait     = null;
        public int     LabClubIndex = 0;
    }
}
