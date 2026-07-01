#if UNITY_EDITOR
// ─────────────────────────────────────────────────────────────────────────────
// StaminaLiveMeterDemoMenu — Phase 5 demo-accel toggle (editor-only).
//
// Usage: GOLFIN > Stamina > Toggle Live-Meter Demo Accel
//   • Enter Play Mode, open the Roster screen, select a character.
//   • Run this menu item → demo accel turns ON; the Condition meter climbs
//     visibly on screen (0.5 virtual hours per real second ≈ ×1800).
//   • Run again → turns OFF; demo-extra resets; meter stays at projected value.
//
// Hard constraints (Phase 5 §Hard constraints):
//   • ZERO scene / prefab mutation.
//   • DemoAccelerate defaults false — never ships enabled.
//   • Never calls AccrueRegen / PersistCondition.
// ─────────────────────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using Golfin.Roster;

namespace Golfin.Roster.Editor
{
    public static class StaminaLiveMeterDemoMenu
    {
        [MenuItem("GOLFIN/Stamina/Toggle Live-Meter Demo Accel")]
        public static void ToggleDemoAccel()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[StaminaDemo] Enter Play Mode first, then open the Roster panel before toggling.");
                return;
            }

            CharacterDetailPanel.DemoAccelerate = !CharacterDetailPanel.DemoAccelerate;

            if (CharacterDetailPanel.DemoAccelerate)
            {
                Debug.Log($"[StaminaDemo] Demo accelerator ON — {CharacterDetailPanel.DemoHoursPerRealSecond} virtual h/s. " +
                          "Condition meter will visibly climb. Toggle again to stop.");
            }
            else
            {
                // Spec L4: "Toggling OFF resets demoExtra to zero."
                CharacterDetailPanel.ResetDemoAccel();
                Debug.Log("[StaminaDemo] Demo accelerator OFF — demoExtra reset to zero; meter returns to real-time projection (regen imperceptible).");
            }
        }
    }
}
#endif
