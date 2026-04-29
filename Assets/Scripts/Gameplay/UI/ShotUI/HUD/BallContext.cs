using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    public static class BallContext
    {
        public static string  SelectedBallId          = "";
        public static string  SelectedNameLabel       = "GOLFIN";
        public static string  SelectedQuantityDisplay = "∞";
        public static Sprite? SelectedThumbnail       = null;
        public static Sprite? SelectedFullSprite      = null;
        public static System.Collections.Generic.List<BallEntry> OwnedBalls = new();
        public static int     SelectedIndex           = 0;

        public static event Action? OnSelectedChanged;
        public static event Action? OnBagChanged;
        public static event Action<int>? OnSelectionRequested;

        public static void RaiseSelectedChanged() => OnSelectedChanged?.Invoke();
        public static void RaiseBagChanged()      => OnBagChanged?.Invoke();
        public static void RequestSelection(int idx) => OnSelectionRequested?.Invoke(idx);

        public static void Reset()
        {
            SelectedBallId          = "";
            SelectedNameLabel       = "GOLFIN";
            SelectedQuantityDisplay = "∞";
            SelectedThumbnail       = null;
            SelectedFullSprite      = null;
            OwnedBalls.Clear();
            SelectedIndex           = 0;
            RaiseBagChanged();
            RaiseSelectedChanged();
        }
    }

    public class BallEntry
    {
        public string  BallId          = "";
        public string  NameLabel       = "";
        public string  QuantityDisplay = "";
        public Sprite? Thumbnail       = null;
        public Sprite? FullSprite      = null;
    }
}
