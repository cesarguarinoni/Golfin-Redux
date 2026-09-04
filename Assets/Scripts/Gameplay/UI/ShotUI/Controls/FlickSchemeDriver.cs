using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// The shipping scheme's driver — deliberately EMPTY.
    ///
    /// <para><c>ClubHandleDragger</c> already drives <c>ShotController</c> through the
    /// external-drag API and has done since long before schemes existed. Flick therefore needs
    /// no code at all to become "a scheme": it needs an identity so
    /// <see cref="ShotSchemeHost"/> can name the root it lives on, and nothing else. Adding
    /// behaviour here would be the one way to make the flick NOT byte-identical, which is the
    /// single load-bearing requirement of control_scheme_seam.</para>
    /// </summary>
    public class FlickSchemeDriver : MonoBehaviour, IShotSchemeDriver
    {
        public ControlScheme Scheme => ControlScheme.Flick;

        public void Bind(ShotController controller) { }
        public void Activate()   { }
        public void Deactivate() { }
    }
}
