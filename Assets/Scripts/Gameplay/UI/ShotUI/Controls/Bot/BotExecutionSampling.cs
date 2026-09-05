using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// The two draws every executor makes, in one place (bot_scheme_parity §3.3).
    ///
    /// <para>ONE COPY, BECAUSE THE ORDER OF THE DRAWS IS PART OF THE CONTRACT. The Flick
    /// regression is a golden file over a seeded RNG, so "aim first, then power" is not a style
    /// choice — a second implementation that drew them the other way round would produce a
    /// different sequence from the same seed and pass every test that only checked the ranges.</para>
    /// </summary>
    public static class BotExecutionSampling
    {
        /// <summary>
        /// One standard-normal draw built from two uniforms (Box–Muller).
        ///
        /// <para>Built out of <see cref="BotExecutionContext.Range"/> rather than from
        /// <c>System.Random</c> so that a seeded regression seeds ONE generator and gets every
        /// draw a swing makes — uniform and normal alike — from it.</para>
        /// </summary>
        public static float Gaussian(Func<float, float, float> range)
        {
            // Guarded away from 0: log(0) is -inf, and Random.Range(0,1) is inclusive of 0.
            float u1 = Mathf.Max(1e-7f, range(0f, 1f));
            float u2 = range(0f, 1f);
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        /// <summary>A clamped normal draw: <c>clamp(N(0, sigma), lo, hi)</c>.</summary>
        public static float ClampedNormal(Func<float, float, float> range, float sigma,
                                          float lo, float hi)
            => Mathf.Clamp(Gaussian(range) * Mathf.Max(0f, sigma), lo, hi);

        /// <summary>
        /// Draw this scheme's timing-axis error, rejection-sampled against the trunks when the
        /// bot supplied a sampler.
        ///
        /// <para><paramref name="treeChecked"/> mirrors the pre-cut log field: 1 when the probe
        /// actually ran, 0 when the bot handed us no sampler (a putt, a treeless hole, or the
        /// debug suppression). <paramref name="canopyContacts"/> is -1 when every draw was
        /// trunk-blocked, which is also when <paramref name="clamped"/> comes back true and the
        /// caller must fire the un-perturbed line.</para>
        /// </summary>
        public static float DrawTimingAxis(BotExecutionContext ctx, Func<float> sample,
                                           Func<float, float> yawDegFor,
                                           out int treeChecked, out int canopyContacts,
                                           out bool clamped)
        {
            treeChecked    = 0;
            canopyContacts = 0;
            clamped        = false;

            if (ctx.TreeSampler == null) return sample();

            treeChecked = 1;
            if (ctx.TreeSampler(sample, yawDegFor, out float accepted, out canopyContacts))
                return accepted;

            clamped        = true;
            canopyContacts = -1;
            return 0f;
        }

        /// <summary>
        /// The uniform power fumble, drawn AFTER the aim axis. See the class remarks.
        ///
        /// <para>UNCONDITIONAL — no <c>PowerErrorMax &gt; 0</c> short-circuit. <c>Range(0, 0)</c>
        /// still advances the generator, so a guard that skipped the draw for a zero-power bracket
        /// would desynchronise every subsequent shot from the golden file while returning the same
        /// 0 this line does.</para>
        /// </summary>
        public static float DrawPowerError(BotExecutionContext ctx, in BotExecutionBand band)
            => ctx.Range(-band.PowerErrorMax, band.PowerErrorMax);

        /// <summary>
        /// The <c>2b error</c> line, byte-for-byte as <c>VersusBot</c> has logged it since
        /// <c>versus_bot_difficulty</c>. Its format is the golden file the Flick regression
        /// diffs, so it is written once here and never re-spelled per executor.
        /// </summary>
        public static void LogErrorLine(BotExecutionContext ctx, in BotSwingPlan plan,
                                        float deltaAimDeg, float deltaPow,
                                        int treeChecked, int canopyContacts, bool clamped)
        {
            if (clamped)
                Debug.Log($"{ctx.LogTag} 2b tree re-check: all aim samples trunk-blocked — clamped to pre-2b line");

            Debug.Log($"{ctx.LogTag} 2b error: Δaim={deltaAimDeg:+0.0;-0.0}° " +
                      $"Δpow={deltaPow:+0.000;-0.000} clubNoise={plan.ClubNoiseNote} " +
                      $"treeChecked={treeChecked} canopyContacts={canopyContacts}");
        }
    }
}
