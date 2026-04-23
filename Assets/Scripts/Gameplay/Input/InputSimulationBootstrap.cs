using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace Golfin.Gameplay.Input
{
    internal static class InputSimulationBootstrap
    {
        // DIAGNOSTIC: ALL bootstrap disabled to test whether EnhancedTouchSupport.Enable()
        // at BeforeSceneLoad is the cause of UI buttons + raw mouse reads breaking.
        // If input works with this commented out, we re-enable carefully and at a later phase.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // EnhancedTouchSupport.Enable();
            // UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
        }
    }
}
