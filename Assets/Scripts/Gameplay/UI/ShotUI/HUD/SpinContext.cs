using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    public static class SpinContext
    {
        public static Vector2 Spin = Vector2.zero;
        public static event Action? OnChanged;
        public static void SetSpin(Vector2 v)
        {
            Spin = new Vector2(Mathf.Clamp(v.x, -1f, 1f), Mathf.Clamp(v.y, -1f, 1f));
            OnChanged?.Invoke();
        }
        public static void Reset() { Spin = Vector2.zero; OnChanged?.Invoke(); }
    }
}
