namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// WHAT a bot decided to hit, before any execution error (bot_scheme_parity §3.1).
    ///
    /// <para>THIS IS THE D1 LINE. <c>versus_bot_difficulty</c> decision D1 says the bot's miss is
    /// EXECUTION, never intent: club choice, lay-up, slope read and tree re-aim all happen first
    /// and produce this plan, and only then does a scheme executor sample the hand-shake that
    /// spoils it. Everything in here is the shot the bot MEANT to play — an executor may perturb
    /// the aim and the power it fires with, but it never re-decides any of these.</para>
    ///
    /// <para>A struct, and readonly, because a plan crosses an assembly boundary into
    /// <see cref="IBotSchemeExecutor"/> and back out through a log line: nothing downstream is
    /// allowed to edit the intent it was handed and quietly report a different shot.</para>
    /// </summary>
    public readonly struct BotSwingPlan
    {
        /// <summary>Lab club index — 0 driver, 1 iron7, 2 wedge, 3 putter. Already
        /// club-noise-shifted: the noise is a DECISION error and belongs to the bot, not to the
        /// scheme (which is why it is drawn before the plan is built and named here only so the
        /// "2b error" log line can stay one line).</summary>
        public readonly int LabClub;

        /// <summary>Intended power 0..1.2 (the same scale <c>ShotController.SetExternalPower</c>
        /// takes). The executor's power error is applied on top of this.</summary>
        public readonly float Power01;

        /// <summary>Intended aim, radians, world yaw. The executor's scheme-specific error yaw is
        /// added to this and the SUM is what the camera is finally pointed at.</summary>
        public readonly float AimYawRad;

        /// <summary>True when the shot is a putt. Every scheme's putt rules apply to bots too —
        /// no overpower, no curve, the slower timing element.</summary>
        public readonly bool IsPutt;

        /// <summary>The selected club's modelled carry in metres — the landing window the tree
        /// probe checks, NOT the cup distance. Updated by lay-up and tree re-aim before the plan
        /// is built (VersusBot §9).</summary>
        public readonly float ProbeCarryM;

        /// <summary>Human-readable note for the "2b error" log line: <c>"none"</c>, or
        /// <c>"driver→iron7"</c>. Carried on the plan rather than logged separately so the line
        /// the golden-file regression diffs stays ONE line, byte-for-byte, after the cut.</summary>
        public readonly string ClubNoiseNote;

        public BotSwingPlan(int labClub, float power01, float aimYawRad, bool isPutt,
                            float probeCarryM, string clubNoiseNote = "none")
        {
            LabClub       = labClub;
            Power01       = power01;
            AimYawRad     = aimYawRad;
            IsPutt        = isPutt;
            ProbeCarryM   = probeCarryM;
            ClubNoiseNote = clubNoiseNote ?? "none";
        }
    }
}
