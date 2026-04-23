using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace Golfin.Gameplay.Input
{
    internal static class InputSimulationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // EnhancedTouchSupport must be enabled before Touch.activeTouches is valid.
            // TouchSimulation routes mouse events through the touch pipeline in the editor.
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
#endif
        }
    }
}
