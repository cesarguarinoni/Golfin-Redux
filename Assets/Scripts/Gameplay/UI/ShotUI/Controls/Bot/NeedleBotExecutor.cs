using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.Needle;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// Needle ("Tap Timing") bot side (bot_scheme_parity §3.3): the miss is where on the arc the
    /// bot taps, and the driver grades the tap.
    ///
    /// <para>±0.98, NOT ±1. A sample at the very end of the arc IS a shank, and a bot never
    /// chooses to shank: running off the end is what happens to a player who did not act, which
    /// is not a thing a skill level should model. Clamping inside the arc is also what keeps the
    /// tap going through the real <c>NeedleTapCatcher</c> rather than through the driver's
    /// timeout path.</para>
    /// </summary>
    public sealed class NeedleBotExecutor : IBotSchemeExecutor
    {
        /// <summary>How close to the arc's end a bot may aim. Past this the swing is a SHANK,
        /// which is a player failing to tap rather than a golfer missing — see the class remarks.</summary>
        private const float MaxSample = 0.98f;

        private readonly NeedleSchemeDriver _driver;

        public NeedleBotExecutor(NeedleSchemeDriver driver) { _driver = driver; }

        public ControlScheme Scheme => ControlScheme.Needle;

        public IEnumerator Execute(BotSwingPlan plan, BotExecutionBand band, BotExecutionContext ctx)
        {
            ShotController shot = ctx?.Shot;
            if (shot == null || _driver == null)
            {
                Debug.LogWarning("[BotSwing] Needle executor: no driver or controller — swing skipped.");
                yield break;
            }

            float power = plan.Power01;
            float n     = 0f;

            if (!band.IsPerfect)
            {
                // POWER FIRST, deliberately — the opposite of Flick's pinned order (see
                // FlickBotExecutor, whose sequence is a golden file). The tree probe below grades
                // each candidate offset at the power the shot will ACTUALLY fire at, and every
                // scheme's accuracy windows shrink with power: sampling against the pre-error
                // power would clear a shot nobody takes. Flick has no such coupling — its aim
                // error is a flat uniform that power does not touch — which is why its order can
                // stay frozen while these three read the way they have to.
                float deltaPow = BotExecutionSampling.DrawPowerError(ctx, band);
                power = Mathf.Clamp01(power + deltaPow);

                // Solved for the live club — see PendulumBotExecutor.SolveLiveSigma.
                float sigma = SolveLiveSigma(band, power);
                n = BotExecutionSampling.DrawTimingAxis(
                    ctx,
                    () => BotExecutionSampling.ClampedNormal(ctx.Range, sigma, -MaxSample, MaxSample),
                    candidate => _driver.GradeForBot(candidate, power).ErrorYawRad * Mathf.Rad2Deg,
                    out int treeChecked, out int canopyContacts, out bool clamped);

                float deltaAimDeg = _driver.GradeForBot(n, power).ErrorYawRad * Mathf.Rad2Deg;
                BotExecutionSampling.LogErrorLine(ctx, plan, deltaAimDeg, deltaPow,
                                                  treeChecked, canopyContacts, clamped);
            }

            // The plan's aim, not a pre-applied one: the miss arrives as the driver's own
            // ErrorYawRad inside the ShotIntent. See PendulumBotExecutor.
            int club = ctx.ApplySwing != null ? ctx.ApplySwing(plan.LabClub, plan.AimYawRad) : plan.LabClub;

            yield return BotSwingGates.WaitForSwingReady(ctx);
            if (shot.State != ShotState.Idle) yield break;

            yield return _driver.DriveBot(power, n, curve01: 0f,
                                          rampSeconds: ctx.RampSeconds,
                                          commitTol01: _driver.BotCommitTol01);

            Debug.Log($"{ctx.LogTag} TakeShot: shot fired — club={club} power={power:F2} " +
                      $"scheme=Needle n={_driver.LastCommittedNeedle:+0.00;-0.00} " +
                      $"grade={_driver.LastCommittedGrade}");
        }

        private float SolveLiveSigma(in BotExecutionBand band, float power)
        {
            if (_driver == null || band.AimErrorDegMax <= 0f) return band.ExecSigma01;
            float sigma = BotSchemeSigmaCalibrator.CalibrateForLiveShot(
                raw => _driver.GradeForBot(raw, power).ErrorYawRad,
                band.AimErrorDegMax * 0.5f, -MaxSample, MaxSample, out _);
            return sigma > 0f ? sigma : band.ExecSigma01;
        }
    }
}
