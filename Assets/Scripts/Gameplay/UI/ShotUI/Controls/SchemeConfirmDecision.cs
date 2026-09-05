namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// The decision the scheme confirm pop-up exists to make (<c>scheme_confirm_popup</c> § 1),
    /// with no Unity in it: does this tap open a pop-up at all, what would CONFIRM commit, and how
    /// many times can it commit it.
    ///
    /// <para><b>Why this is a separate class.</b> The pop-up itself is a
    /// <c>ModalController</c> in Assembly-CSharp, and an assembly-definition test assembly cannot
    /// reference Assembly-CSharp — so a rule kept only inside the MonoBehaviour would be untestable
    /// by construction. The three rules that actually matter live here, in an assembly
    /// <c>Golfin.Gameplay.Tests</c> can reach, and the MonoBehaviour is a thin shell over it:</para>
    /// <list type="number">
    /// <item>tapping the scheme already in use opens nothing;</item>
    /// <item>CONFIRM commits exactly once — a second CONFIRM inside the close animation is a
    ///       no-op, not a second <c>ControlSchemeService.Set</c>;</item>
    /// <item>CANCEL, the close button and a backdrop tap all disarm, so nothing can commit after
    ///       the player has said no.</item>
    /// </list>
    /// </summary>
    public sealed class SchemeConfirmDecision
    {
        /// <summary>True while a pop-up is open and CONFIRM would still commit.</summary>
        public bool Armed { get; private set; }

        /// <summary>The scheme CONFIRM would commit.</summary>
        public ControlScheme Pending { get; private set; }

        /// <summary>The telemetry <c>where</c> CONFIRM would commit with.</summary>
        public string Source { get; private set; }

        /// <summary>
        /// A player tapped <paramref name="tapped"/>.
        /// </summary>
        /// <returns><c>true</c> when the pop-up should open; <c>false</c> when the tap is a no-op
        /// because that scheme is already the live one.</returns>
        public bool Open(ControlScheme current, ControlScheme tapped, string source)
        {
            if (current == tapped) return false;

            Pending = tapped;
            Source  = source;
            Armed   = true;
            return true;
        }

        /// <summary>
        /// The player pressed CONFIRM.
        /// </summary>
        /// <returns><c>true</c> exactly once per <see cref="Open"/>, meaning the caller should now
        /// write the scheme. Any later CONFIRM — a double tap landing inside the modal's 0.2 s
        /// fade-out — returns <c>false</c>.</returns>
        public bool Confirm(out ControlScheme scheme, out string source)
        {
            scheme = Pending;
            source = Source;

            if (!Armed) return false;
            Armed = false;
            return true;
        }

        /// <summary>CANCEL, the close button, a backdrop tap, or the modal being force-disabled.
        /// Idempotent, and after it nothing can commit.</summary>
        public void Cancel() => Armed = false;
    }
}
