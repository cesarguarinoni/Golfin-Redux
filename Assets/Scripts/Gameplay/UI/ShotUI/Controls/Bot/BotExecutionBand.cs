namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// One row of <c>bot_difficulty.csv</c>, resolved for the opponent's level — HOW BADLY this
    /// bot executes (bot_scheme_parity §3.1).
    ///
    /// <para>The band is the ONLY thing that makes a level-1 bot worse than a level-100 one, and
    /// it is deliberately scheme-shaped: Flick's two uniform half-widths are what the shipping
    /// difficulty was calibrated against and must never move, while the graded schemes need a
    /// SIGMA on their own timing axis because their misses are bimodal (JUST or MISS), not flat.
    /// <see cref="ExecSigma01"/> is the sigma for the ACTIVE scheme, picked from the row's three
    /// per-scheme columns by the bot before the band is built — a single sigma cannot calibrate
    /// three different graders (§5).</para>
    /// </summary>
    public readonly struct BotExecutionBand
    {
        /// <summary>Flick only: half-width of the uniform yaw error, degrees.</summary>
        public readonly float AimErrorDegMax;

        /// <summary>Every scheme: half-width of the uniform power01 error. Power is a DECISION
        /// the hand fumbles the same way whatever widget is on screen, so this one number is
        /// shared rather than re-calibrated per scheme.</summary>
        public readonly float PowerErrorMax;

        /// <summary>Graded schemes only: sigma of the normal draw on that scheme's own timing
        /// axis (marker offset / needle offset / impact px + tempo), normalised so that 1.0 is
        /// the full half-travel of the axis. Calibrated so E|ErrorYaw| matches Flick's
        /// <c>AimErrorDegMax / 2</c> — see <c>BotSchemeCalibrationHarness</c>.</summary>
        public readonly float ExecSigma01;

        public BotExecutionBand(float aimErrorDegMax, float powerErrorMax, float execSigma01)
        {
            AimErrorDegMax = aimErrorDegMax;
            PowerErrorMax  = powerErrorMax;
            ExecSigma01    = execSigma01;
        }

        /// <summary>A bot that never misses. Flick injects nothing; the graded executors target
        /// the dead centre of their axis, so the marker still sweeps and the needle still crosses
        /// — a perfect bot still LOOKS like it swings, which is what a capture rig needs.</summary>
        public static readonly BotExecutionBand Perfect = new BotExecutionBand(0f, 0f, 0f);

        /// <summary>True when nothing at all would be injected. Used to keep the "2b error" line
        /// off the log for smoke/perf/capture bots, whose swings are not a difficulty sample.</summary>
        public bool IsPerfect => AimErrorDegMax <= 0f && PowerErrorMax <= 0f && ExecSigma01 <= 0f;
    }
}
