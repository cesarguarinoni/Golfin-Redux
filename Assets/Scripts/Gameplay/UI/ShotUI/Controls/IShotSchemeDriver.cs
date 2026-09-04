using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// One control scheme's input driver. Lives on the root <see cref="ShotSchemeHost"/>
    /// activates for that scheme (control_scheme_seam §3.3).
    ///
    /// <para>A driver turns a gesture into a <see cref="ShotIntent"/> and calls
    /// <c>ShotController.CommitExternal</c>. It publishes live state through the existing
    /// external-drag API (<c>BeginExternalDrag</c> / <c>SetExternalPower</c>), which is what
    /// keeps every <c>OnStateChanged</c> subscriber — PowerGaugeWidget, CentralBallWidget,
    /// ShotInProgressUiGate, MapViewController, PuttPathPredictor, ActionButtonsRoot — working
    /// unmodified under every scheme.</para>
    /// </summary>
    public interface IShotSchemeDriver
    {
        ControlScheme Scheme { get; }

        /// <summary>Hand the driver the controller it drives. Called once, before Activate.</summary>
        void Bind(ShotController controller);

        /// <summary>This scheme is now the player's. Called only with the shot at Idle.</summary>
        void Activate();

        /// <summary>The player switched away. Called only with the shot at Idle, so a driver
        /// never has to unwind a half-finished swing.</summary>
        void Deactivate();
    }
}
