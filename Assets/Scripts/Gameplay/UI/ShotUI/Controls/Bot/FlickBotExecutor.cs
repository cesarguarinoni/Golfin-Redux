using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// The shipping scheme's bot side — <c>VersusBot</c>'s "2b error injection" and step 8, moved
    /// here VERBATIM (bot_scheme_parity §3.1).
    ///
    /// <para>BYTE-IDENTICAL IS THE WHOLE POINT OF THIS CLASS. Every 1v1 difficulty number in
    /// <c>bot_difficulty.csv</c> was calibrated against this exact sequence — uniform aim draw
    /// (rejection-sampled against the trunks), uniform power draw, <c>Clamp01</c>, then
    /// <c>BeginExternalDrag</c> → 0.85 s ramp → hold → <c>EndExternalDrag</c>. The draws happen in
    /// that order, the power draw is unconditional, and the log line is spelled the way it has
    /// always been spelled, so a seeded run produces the same deltas and the same text it did
    /// before the cut. <c>versus_bot_difficulty</c>'s acceptance IS this class's regression suite.</para>
    ///
    /// <para>ALSO THE FALLBACK FOR EVERYTHING ELSE. No <c>ShotSchemeHost</c> in the scene (an
    /// EditMode test, a lab scaffold), a scheme whose driver has not shipped, or an explicit
    /// <c>BotSwingOptions.ForceFlick</c> — all of them land here, which is why it is a stateless
    /// singleton rather than a component someone has to remember to author onto a root.</para>
    /// </summary>
    public sealed class FlickBotExecutor : IBotSchemeExecutor
    {
        public static readonly FlickBotExecutor Instance = new FlickBotExecutor();

        public ControlScheme Scheme => ControlScheme.Flick;

        public IEnumerator Execute(BotSwingPlan plan, BotExecutionBand band, BotExecutionContext ctx)
        {
            ShotController shot = ctx?.Shot;
            if (shot == null)
            {
                Debug.LogWarning("[BotSwing] Flick executor: no ShotController — swing skipped.");
                yield break;
            }

            float aimYaw = plan.AimYawRad;
            float power  = plan.Power01;

            // ── 2b: POST-DECISION ERROR INJECTION (D1: after H1/H2/H3, before commit) ──
            // Inject per-shot execution error based on the opponent's level bracket.
            // No safety re-check runs on the perturbed values — they fire straight to commit.
            if (!band.IsPerfect)
            {
                float deltaAimDeg = 0f;
                int   treeChecked = 0;
                int   canopyContacts = 0;
                bool  clamped = false;

                if (band.AimErrorDegMax > 0f || band.PowerErrorMax > 0f)
                {
                    // canopy_avoidance_v2: the sampler the bot handed us is the scored one —
                    // trunk = hard reject, canopy = soft preference. Flick's raw draw IS the aim
                    // delta in degrees, so the yaw mapping is the identity.
                    float max = band.AimErrorDegMax;
                    deltaAimDeg = BotExecutionSampling.DrawTimingAxis(
                        ctx, () => ctx.Range(-max, max), d => d,
                        out treeChecked, out canopyContacts, out clamped);
                }

                float deltaPow = BotExecutionSampling.DrawPowerError(ctx, band);

                aimYaw += deltaAimDeg * Mathf.Deg2Rad;
                power   = Mathf.Clamp01(power + deltaPow);

                BotExecutionSampling.LogErrorLine(ctx, plan, deltaAimDeg, deltaPow,
                                                  treeChecked, canopyContacts, clamped);
            }
            // ── END 2b error injection ──────────────────────────────────────

            int club = ctx.ApplySwing != null ? ctx.ApplySwing(plan.LabClub, aimYaw) : plan.LabClub;

            yield return BotSwingGates.WaitForSwingReady(ctx);
            if (shot.State != ShotState.Idle) yield break;

            // ── Drive the shot via BeginExternalDrag → ramp → EndExternalDrag ──
            shot.BeginExternalDrag();

            float rampSeconds = Mathf.Max(1e-3f, ctx.RampSeconds);
            float rt = 0f;
            while (rt < rampSeconds)
            {
                rt += Time.unscaledDeltaTime;
                shot.SetExternalPower(Mathf.Lerp(0f, power, rt / rampSeconds), 0f);
                yield return null;
            }
            shot.SetExternalPower(power, 0f);

            yield return new WaitForSecondsRealtime(ctx.HoldSeconds);

            shot.EndExternalDrag(ctx.BypassFlickGate);

            Debug.Log($"{ctx.LogTag} TakeShot: shot fired — club={club} power={power:F2} scheme=Flick");
        }
    }
}
