using System.Collections;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// One control scheme's BOT side (bot_scheme_parity §3.1) — the counterpart of
    /// <see cref="IShotSchemeDriver"/>, which is the same scheme's HUMAN side.
    ///
    /// <para>AN EXECUTOR PLAYS THE REAL UI. It never fakes a result off-screen: it samples this
    /// scheme's execution error, animates the scheme's own widget through that scheme's own
    /// driver, and lets the DRIVER grade the swing and commit it. The grade the player watches
    /// pop over the bot's ball is therefore the honest grade of what they just watched happen,
    /// and there is exactly one grading implementation per scheme rather than one for humans and
    /// a second, drifting one for bots.</para>
    ///
    /// <para>CONTRACT: exactly one <c>ShotController</c> commit per <see cref="Execute"/> (or a
    /// logged cancel), and the <c>"2b error"</c> line on the log for any non-perfect band.</para>
    /// </summary>
    public interface IBotSchemeExecutor
    {
        /// <summary>Which scheme this executor swings. <c>Flick</c> is also the answer for a
        /// scheme whose driver has not shipped: the host keeps the flick root live underneath an
        /// unimplemented scheme, so the bot must swing what the player can actually see.</summary>
        ControlScheme Scheme { get; }

        /// <summary>
        /// Sample this bracket's execution error on this scheme's timing axis, animate the
        /// scheme's UI, and commit the shot.
        /// </summary>
        IEnumerator Execute(BotSwingPlan plan, BotExecutionBand band, BotExecutionContext ctx);
    }
}
