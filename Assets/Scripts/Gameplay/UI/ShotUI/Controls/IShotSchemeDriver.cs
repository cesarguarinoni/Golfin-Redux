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

        /// <summary>
        /// False while this scheme is a stand-in (<see cref="PlaceholderSchemeDriver"/>), which is
        /// how <see cref="ShotSchemeHost"/> knows to keep <c>SchemeRoot_Flick</c> live underneath
        /// it so the tester still has a playable game.
        ///
        /// <para>Promoted from a field on the placeholder to a member of the interface by
        /// scheme_pendulum §3.1: with a second REAL driver in the project, the host's old
        /// <c>!(driver is PlaceholderSchemeDriver)</c> type test would have had to grow a case per
        /// scheme. Asking the driver is the version that stays one line for all four.</para>
        /// </summary>
        bool IsImplemented { get; }

        /// <summary>Hand the driver the controller it drives. Called once, before Activate.</summary>
        void Bind(ShotController controller);

        /// <summary>This scheme is now the player's. Called only with the shot at Idle.</summary>
        void Activate();

        /// <summary>The player switched away. Called only with the shot at Idle, so a driver
        /// never has to unwind a half-finished swing.</summary>
        void Deactivate();
    }
}
