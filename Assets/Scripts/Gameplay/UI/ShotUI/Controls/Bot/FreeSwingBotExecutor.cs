using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.FreeSwing;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// Free Swing's bot side (bot_scheme_parity §3.3). This scheme reads FOUR things off one
    /// gesture, so the error model has to say something about two of them: WHERE the club crosses
    /// the impact line (the aim error) and the down:up TEMPO (the power error).
    ///
    /// <para>THE OTHER TWO ARE FIXED BY DESIGN, NOT SAMPLED. The path is a straight chord, so
    /// <c>FadeDraw01</c> is 0 — bots never shape a shot. And the upstroke is driven at twice the
    /// duff threshold, so a bot never duffs: a duff is a thumb that failed to move, which is a
    /// property of hands and glass rather than of golf, and modelling it would make a level-1 bot
    /// fail in a way no human fails on purpose.</para>
    ///
    /// <para>Tempo is sampled around the IDEAL rather than around the bot's own last swing,
    /// because the ratio is graded against that ideal: a normal centred anywhere else would be a
    /// systematic bias rather than an execution error, i.e. a decision, which D1 forbids.</para>
    /// </summary>
    public sealed class FreeSwingBotExecutor : IBotSchemeExecutor
    {
        /// <summary>Multiple of the duff threshold the bot swings at. See the class remarks.</summary>
        private const float DuffClearance = 2f;

        private readonly FreeSwingSchemeDriver _driver;

        public FreeSwingBotExecutor(FreeSwingSchemeDriver driver) { _driver = driver; }

        public ControlScheme Scheme => ControlScheme.FreeSwing;

        public IEnumerator Execute(BotSwingPlan plan, BotExecutionBand band, BotExecutionContext ctx)
        {
            ShotController shot = ctx?.Shot;
            if (shot == null || _driver == null)
            {
                Debug.LogWarning("[BotSwing] FreeSwing executor: no driver or controller — swing skipped.");
                yield break;
            }

            float power   = plan.Power01;
            float impactPx = 0f;
            float tempo    = _driver.IdealTempo;

            if (!band.IsPerfect)
            {
                // POWER FIRST, deliberately — the opposite of Flick's pinned order (see
                // FlickBotExecutor, whose sequence is a golden file). The tree probe below grades
                // each candidate impact at the power the shot will ACTUALLY fire at, and this
                // scheme's impact window shrinks with power: sampling against the pre-error power
                // would clear a shot nobody takes.
                float deltaPow = BotExecutionSampling.DrawPowerError(ctx, band);
                power = Mathf.Clamp01(power + deltaPow);

                // The impact axis is measured in PIXELS, so the normalised sigma is scaled by the
                // miss range the grader itself uses — sigma 1.0 then means "a full miss wide",
                // the same thing it means on the other two schemes' −1..+1 axes.
                // Solved for the live club — see PendulumBotExecutor.SolveLiveSigma. Solved in
                // NORMALISED units (sigma 1.0 = one full miss width) and scaled to px here, so the
                // number means the same thing it does on the other two schemes' -1..+1 axes.
                float sigmaPx = SolveLiveSigma(band, power) * _driver.ImpactMissPx;
                impactPx = BotExecutionSampling.DrawTimingAxis(
                    ctx,
                    () => BotExecutionSampling.ClampedNormal(ctx.Range, sigmaPx,
                                                             -_driver.ImpactMissPx * 2f,
                                                              _driver.ImpactMissPx * 2f),
                    candidate => _driver.ImpactYawRadForBot(candidate, power) * Mathf.Rad2Deg,
                    out int treeChecked, out int canopyContacts, out bool clamped);

                // Tempo: one window either side is the full GOOD→ramp range, so 2× the window as
                // the sigma scale puts a level-1 bot outside it about as often as it misses the
                // impact line, and a level-100 bot almost never.
                float tempoSigma = band.ExecSigma01 * 2f * _driver.TempoWindowForBot(power);
                tempo = Mathf.Max(0.05f, _driver.IdealTempo +
                                          BotExecutionSampling.ClampedNormal(ctx.Range, tempoSigma,
                                                                             -_driver.IdealTempo * 0.9f,
                                                                              _driver.IdealTempo * 3f));

                float deltaAimDeg = _driver.ImpactYawRadForBot(impactPx, power) * Mathf.Rad2Deg;
                BotExecutionSampling.LogErrorLine(ctx, plan, deltaAimDeg, deltaPow,
                                                  treeChecked, canopyContacts, clamped);
            }

            // The plan's aim, not a pre-applied one: the miss arrives as the driver's own
            // ErrorYawRad inside the ShotIntent. See PendulumBotExecutor.
            int club = ctx.ApplySwing != null ? ctx.ApplySwing(plan.LabClub, plan.AimYawRad) : plan.LabClub;

            yield return BotSwingGates.WaitForSwingReady(ctx);
            if (shot.State != ShotState.Idle) yield break;

            yield return _driver.DriveBot(power, impactPx, tempo,
                                          _driver.DuffSpeedForBot * DuffClearance);

            var v = _driver.LastVerdict;
            Debug.Log($"{ctx.LogTag} TakeShot: shot fired — club={club} power={power:F2} " +
                      $"scheme=FreeSwing impact={v.ImpactPx:+0.0;-0.0}px tempo={v.TempoRatio:F2} " +
                      $"grade={v.Grade}");
        }

        private float SolveLiveSigma(in BotExecutionBand band, float power)
        {
            if (_driver == null || band.AimErrorDegMax <= 0f) return band.ExecSigma01;
            float miss = Mathf.Max(_driver.ImpactMissPx, 1e-3f);
            float sigma = BotSchemeSigmaCalibrator.CalibrateForLiveShot(
                norm => _driver.ImpactYawRadForBot(norm * miss, power),
                band.AimErrorDegMax * 0.5f, -2f, 2f, out _);
            return sigma > 0f ? sigma : band.ExecSigma01;
        }
    }
}
