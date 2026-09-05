using System.Collections;
using UnityEngine;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>Per-swing overrides. Almost every bot leaves this at its default.</summary>
    public struct BotSwingOptions
    {
        /// <summary>
        /// Swing FLICK whatever the player has selected.
        ///
        /// <para>DETERMINISTIC CAPTURES AND FROZEN BASELINES ONLY, and the call site must say in a
        /// comment WHY. A capture rig or a perf baseline whose numbers are compared against an
        /// older build cannot have its swing change shape because a preference moved; every other
        /// bot must follow the player, which is the entire point of this file.</para>
        /// </summary>
        public bool ForceFlick;

        /// <summary>Override the 0.85 s handle ramp. 0 = leave the context's value alone.</summary>
        public float RampSeconds;
    }

    /// <summary>
    /// THE ONE DOOR EVERY BOT SWINGS THROUGH (bot_scheme_parity §3.5, Cesar 2026-09-05: "this
    /// should include any test bots we use in the future when developing features").
    ///
    /// <para>Before this existed, five bots each hand-rolled
    /// <c>BeginExternalDrag → SetExternalPower → EndExternalDrag</c>, which is Flick's gesture
    /// spelled out longhand. With another scheme selected the flick root is OFF, so those swings
    /// animated nothing — the ball simply left while the pendulum bar sat idle. Routing every bot
    /// through here means a bot written next year against <c>BotSwing</c> swings whatever scheme
    /// the player picked without ever having heard of schemes.</para>
    ///
    /// <para>THE RULE (CLAUDE.md PIPELINE_HARDENING, Docs/AI_CONTEXT.md, and a done-hook grep):
    /// <i>bots swing through <see cref="Play"/> / <see cref="PlayPerfect"/>, never
    /// <c>BeginExternalDrag</c> / <c>EndExternalDrag</c> / <c>CommitFlick</c> directly.
    /// <see cref="BotSwingOptions.ForceFlick"/> requires a comment. A new bot that bypasses this
    /// class fails review.</i></para>
    /// </summary>
    public static class BotSwing
    {
        /// <summary>Swing through the ACTIVE scheme's executor. The default for every bot.</summary>
        public static IEnumerator Play(BotSwingPlan plan, BotExecutionBand band,
                                       BotExecutionContext ctx, BotSwingOptions opt = default)
        {
            ctx = ctx ?? BotExecutionContext.Resolve();
            if (opt.RampSeconds > 0f) ctx.RampSeconds = opt.RampSeconds;
            return ResolveExecutor(opt).Execute(plan, band, ctx);
        }

        /// <summary>
        /// Zero-error convenience for smoke / perf / capture bots: <see cref="BotExecutionBand.Perfect"/>.
        /// The scheme's widget still animates — a perfect bot LOOKS like it swings, which is what a
        /// capture take needs — it simply never misses.
        /// </summary>
        public static IEnumerator PlayPerfect(float power01, float aimYawRad, bool isPutt,
                                              BotExecutionContext ctx, BotSwingOptions opt = default)
            => Play(new BotSwingPlan(labClub: -1, power01: power01, aimYawRad: aimYawRad,
                                     isPutt: isPutt, probeCarryM: 0f),
                    BotExecutionBand.Perfect, ctx, opt);

        /// <summary>
        /// Which executor a swing would use right now. Public so a test can pin the resolution
        /// rule without running a coroutine, and so a bot can log what it is about to swing.
        ///
        /// <para>No host in the scene (EditMode, a lab scaffold, a scene that has not booted the
        /// shot UI) resolves to Flick rather than throwing: a bot must always be able to swing.</para>
        /// </summary>
        public static IBotSchemeExecutor ResolveExecutor(BotSwingOptions opt = default)
        {
            if (opt.ForceFlick) return FlickBotExecutor.Instance;
            var host = Object.FindFirstObjectByType<ShotSchemeHost>();
            return host != null ? host.ActiveExecutor : FlickBotExecutor.Instance;
        }
    }
}
