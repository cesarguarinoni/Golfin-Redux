using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.Pendulum;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// Pendulum's bot side (bot_scheme_parity §3.3): the miss is a marker offset, and the driver
    /// grades it.
    ///
    /// <para>NOTHING IS INJECTED AFTER THE COMMIT. The executor samples an offset <c>m</c>, hands
    /// it to <see cref="PendulumSchemeDriver.DriveBot"/>, and the driver releases when its live
    /// marker reaches it — so the JUST/GOOD/MISS the player watches pop is the real grade of the
    /// bar position they watched. There is no second, bot-only path through <c>PendulumMath</c>
    /// producing a yaw the screen disagrees with.</para>
    ///
    /// <para>A NORMAL, NOT A UNIFORM. Flick's flat <c>±AimErrorDegMax</c> was calibrated against a
    /// scheme with no timing bands at all; on a banded axis a flat draw would put the same weight
    /// on "dead on the pip" as on "the far end of the bar", which no human hand does. The sigma
    /// per bracket is calibrated so E|ErrorYaw| still lands on Flick's <c>AimErrorDegMax / 2</c>
    /// (§5), which is what keeps the 1v1 difficulty where it was.</para>
    /// </summary>
    public sealed class PendulumBotExecutor : IBotSchemeExecutor
    {
        private readonly PendulumSchemeDriver _driver;

        public PendulumBotExecutor(PendulumSchemeDriver driver) { _driver = driver; }

        public ControlScheme Scheme => ControlScheme.Pendulum;

        public IEnumerator Execute(BotSwingPlan plan, BotExecutionBand band, BotExecutionContext ctx)
        {
            ShotController shot = ctx?.Shot;
            if (shot == null || _driver == null)
            {
                Debug.LogWarning("[BotSwing] Pendulum executor: no driver or controller — swing skipped.");
                yield break;
            }

            float power = plan.Power01;
            float m     = 0f;

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

                // Solve sigma against THIS shot's live club and power, not the baked reference.
                // The bot swings the PLAYER'S bag (BotClubSync reads ClubContext.EquippedBag), so a
                // single baked sigma would make the opponent's difficulty a function of the
                // player's equipment. See BotSchemeSigmaCalibrator.CalibrateForLiveShot.
                float sigma = SolveLiveSigma(band, power);
                // The sampled offset and the yaw it would produce, so the trunk probe rejects the
                // marker positions that would bend the ball into a tree — the SAME rejection the
                // Flick sampler does, on this scheme's own axis.
                m = BotExecutionSampling.DrawTimingAxis(
                    ctx,
                    () => BotExecutionSampling.ClampedNormal(ctx.Range, sigma, -1f, 1f),
                    candidate => _driver.GradeForBot(candidate, power).ErrorYawRad * Mathf.Rad2Deg,
                    out int treeChecked, out int canopyContacts, out bool clamped);

                float deltaAimDeg = _driver.GradeForBot(m, power).ErrorYawRad * Mathf.Rad2Deg;
                BotExecutionSampling.LogErrorLine(ctx, plan, deltaAimDeg, deltaPow,
                                                  treeChecked, canopyContacts, clamped);
            }

            // The aim the camera is pointed at is the PLAN's aim: this scheme's error arrives as
            // the driver's own ErrorYawRad inside the ShotIntent, not as a yaw the bot pre-applies.
            // Adding it here as well would double every miss.
            int club = ctx.ApplySwing != null ? ctx.ApplySwing(plan.LabClub, plan.AimYawRad) : plan.LabClub;

            yield return BotSwingGates.WaitForSwingReady(ctx);
            if (shot.State != ShotState.Idle) yield break;

            yield return _driver.DriveBot(power, m, curve01: 0f,
                                          rampSeconds: ctx.RampSeconds,
                                          commitTol01: _driver.BotCommitTol01,
                                          maxWaitSweeps: _driver.BotMaxWaitSweeps);

            Debug.Log($"{ctx.LogTag} TakeShot: shot fired — club={club} power={power:F2} " +
                      $"scheme=Pendulum m={_driver.LastCommittedMarker:+0.00;-0.00} " +
                      $"grade={_driver.LastCommittedGrade}");
        }

        /// <summary>The bracket's expected miss, re-solved for the live club. Falls back to the
        /// CSV column when there is no driver to grade against (an EditMode fixture).</summary>
        private float SolveLiveSigma(in BotExecutionBand band, float power)
        {
            if (_driver == null || band.AimErrorDegMax <= 0f) return band.ExecSigma01;
            float sigma = BotSchemeSigmaCalibrator.CalibrateForLiveShot(
                raw => _driver.GradeForBot(raw, power).ErrorYawRad,
                band.AimErrorDegMax * 0.5f, -1f, 1f, out _);
            return sigma > 0f ? sigma : band.ExecSigma01;
        }
    }
}
