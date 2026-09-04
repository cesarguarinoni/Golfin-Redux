namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// The four shot-control schemes being A/B'd on the beta build
    /// (<c>Docs/CONTROL_SCHEMES_PLAN.md</c>). These are INTERNAL names — the player-facing
    /// labels come from <see cref="ControlSchemeService.LabelKey"/>, which is why
    /// <see cref="Needle"/> reads "Tap Timing" on screen.
    ///
    /// <para>The integer values are persisted in PlayerPrefs and stamped on every
    /// <c>shot_taken</c> telemetry row, so they are a WIRE FORMAT: append new schemes, never
    /// renumber the existing ones.</para>
    /// </summary>
    public enum ControlScheme
    {
        /// <summary>The shipping scheme: pull the club handle, flick up on the timing slab.</summary>
        Flick = 0,

        /// <summary>白猫GOLF-style marker sweeping a bar. Driver lands in <c>scheme_pendulum</c>.</summary>
        Pendulum = 1,

        /// <summary>Golf Clash-style needle across an accuracy arc, tap to stop. Shown as
        /// "Tap Timing". Driver lands in <c>scheme_needle</c>.</summary>
        Needle = 2,

        /// <summary>TrueSwing-style free gesture. Driver lands in <c>scheme_freeswing</c>.</summary>
        FreeSwing = 3,
    }
}
