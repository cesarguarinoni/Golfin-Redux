using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Marker component on the gameplay HUD gear (ShotUI_Canvas/SettingsButton).
    ///
    /// The gear's behaviour is owned by <c>Golfin.UI.Modals.InGameSettingsModalController</c>,
    /// which holds this Button as a scene-wired reference and adds its own toggle listener.
    /// This component adds no listener of its own — it only guarantees the Button exists and
    /// keeps the GameObject findable/typed for tooling.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SettingsButton : MonoBehaviour
    {
    }
}
