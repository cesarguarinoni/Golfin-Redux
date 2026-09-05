using System;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.Controls.Pendulum;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Gameplay.UI.Controls.FreeSwing;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// Turns a Flick difficulty bracket into the timing-axis sigma that makes a graded scheme miss
    /// by the SAME amount (bot_scheme_parity §5).
    ///
    /// <para>THE CALIBRATION GUARD, AND THE WHOLE REASON THIS TASK DOES NOT MOVE 1v1 DIFFICULTY.
    /// A bracket says "this bot's yaw error is uniform ±6°", so its expected miss is 3°. Under
    /// Pendulum the same bot has no yaw error at all until its marker leaves the JUST band, then a
    /// ramp, then a flat 1.5 half-cones — a completely different shape. Bisecting sigma until the
    /// EXPECTED ABSOLUTE YAW matches is what makes "level 1" mean the same thing under all four
    /// schemes, which is the difference between offering the player a choice of control and
    /// offering them a choice of difficulty.</para>
    ///
    /// <para>ONE IMPLEMENTATION, TWO CALLERS. The Editor harness runs it at 20 000 samples and
    /// writes the three CSV columns; the runtime loader runs it at a fraction of that when a
    /// column is missing, so a CSV that has not been re-calibrated yet degrades to "slightly
    /// noisier numbers plus a warning" rather than to a zero-error bot. A second, simpler
    /// approximation for the fallback would be a second thing to keep true.</para>
    ///
    /// <para>Deliberately <c>System.Random</c> and not <c>UnityEngine.Random</c>: calibration must
    /// be reproducible and must not perturb the global generator a live match is drawing its
    /// shots from.</para>
    /// </summary>
    public static class BotSchemeSigmaCalibrator
    {
        /// <summary>The reference bot's club accuracy, normalised the way
        /// <c>ShotController.ClubAccuracyNorm01</c> normalises it (Accuracy / 120). 0.5 is the
        /// Acc-60 club §5 names.</summary>
        public const float ReferenceAccuracyNorm01 = 0.5f;

        /// <summary>The reference bot's Club Control, normalised against the 120 the
        /// <c>*AtCC120</c> config keys are named for.</summary>
        public const float ReferenceClubControlNorm01 = 0.5f;

        /// <summary>The power the calibration is done at. Every scheme's windows shrink with
        /// power, so the sigma depends on it; 0.85 is a full-ish approach, which is the shot a
        /// bot plays most often.</summary>
        public const float ReferencePower01 = 0.85f;

        /// <summary>Samples the runtime loader uses when a CSV column is missing. Two orders below
        /// the harness's 20 000 — enough for a usable number, cheap enough to run at load.</summary>
        public const int FallbackSamples = 1500;

        /// <summary>Seed shared by every evaluation, so bisection sees a DETERMINISTIC function
        /// and converges instead of chasing sampling noise.</summary>
        public const int DefaultSeed = 20260905;

        /// <summary>Relative accuracy the bisection stops at — §5's "within 3 %".</summary>
        public const float TargetTolerance = 0.03f;

        /// <summary>The reference cone half-angle against the shipped tuning, in degrees. Its
        /// own accessor for the same reason <see cref="CalibrateDefault"/> exists: the callers
        /// that report the calibration table cannot name <see cref="ControlsConfig"/>.</summary>
        public static float ReferenceHalfConeDegDefault
            => ReferenceHalfConeRad(ControlsConfig.Default) * Mathf.Rad2Deg;

        public static float ReferenceHalfConeRad(in ControlsConfig cfg)
            => Mathf.Lerp(cfg.ConeHalfAngleAtAcc0Deg, cfg.ConeHalfAngleAtAcc100Deg,
                          ReferenceAccuracyNorm01) * Mathf.Deg2Rad;

        /// <summary>
        /// E|ErrorYaw| in degrees for one scheme at one sigma, over <paramref name="samples"/>
        /// draws of that scheme's own grader.
        /// </summary>
        public static float MeanAbsYawDeg(ControlScheme scheme, float sigma, in ControlsConfig cfg,
                                          int samples, int seed)
        {
            if (samples <= 0) return 0f;
            var   rng      = new System.Random(seed);
            float halfCone = ReferenceHalfConeRad(cfg);
            float acc      = ReferenceAccuracyNorm01;
            float power    = ReferencePower01;
            double sum     = 0d;

            for (int i = 0; i < samples; i++)
            {
                float z = Normal(rng);
                float yawRad;
                switch (scheme)
                {
                    case ControlScheme.Pendulum:
                        yawRad = PendulumMath.Grade(Mathf.Clamp(z * sigma, -1f, 1f),
                                                    acc, power, halfCone, cfg).ErrorYawRad;
                        break;
                    case ControlScheme.Needle:
                        yawRad = NeedleMath.Grade(Mathf.Clamp(z * sigma, -0.98f, 0.98f),
                                                  acc, power, halfCone, cfg).ErrorYawRad;
                        break;
                    case ControlScheme.FreeSwing:
                        float miss = cfg.FreeSwingImpactMissPx;
                        yawRad = FreeSwingMath.ImpactYawRad(
                                     Mathf.Clamp(z * sigma * miss, -2f * miss, 2f * miss),
                                     acc, power, halfCone, cfg);
                        break;
                    default:
                        // Flick is the reference, not a calibration target: its expected miss is
                        // AimErrorDegMax / 2 by construction.
                        return 0f;
                }
                sum += Mathf.Abs(yawRad) * Mathf.Rad2Deg;
            }
            return (float)(sum / samples);
        }

        /// <summary>
        /// Bisect sigma until <see cref="MeanAbsYawDeg"/> hits
        /// <paramref name="targetMeanAbsYawDeg"/> (= the bracket's <c>aimErrorDegMax / 2</c>).
        ///
        /// <para>Monotone in sigma up to a ceiling — every grader saturates at its own flat
        /// "big miss" yaw — so a target above what the scheme can produce returns the ceiling and
        /// reports the achieved value, rather than looping. That is a real answer: it means the
        /// bracket is wilder than the scheme's worst possible timing, which is worth seeing in the
        /// harness table rather than hiding behind a clamp.</para>
        /// </summary>
        public static float Calibrate(ControlScheme scheme, float targetMeanAbsYawDeg,
                                      in ControlsConfig cfg, int samples, int seed,
                                      out float achievedDeg)
        {
            achievedDeg = 0f;
            if (targetMeanAbsYawDeg <= 0f) return 0f;

            const float Hi = 3f;      // sigma 3 puts essentially every draw past the widest band
            float lo = 0f, hi = Hi;

            achievedDeg = MeanAbsYawDeg(scheme, hi, cfg, samples, seed);
            if (achievedDeg < targetMeanAbsYawDeg) return hi;    // saturated: report the ceiling

            float mid = 0f;
            for (int i = 0; i < 60; i++)
            {
                mid = 0.5f * (lo + hi);
                achievedDeg = MeanAbsYawDeg(scheme, mid, cfg, samples, seed);
                if (Mathf.Abs(achievedDeg - targetMeanAbsYawDeg) <= TargetTolerance * targetMeanAbsYawDeg)
                    return mid;
                if (achievedDeg < targetMeanAbsYawDeg) lo = mid; else hi = mid;
            }
            return mid;
        }

        /// <summary>
        /// <see cref="Calibrate"/> against the shipped tuning.
        ///
        /// <para>Exists so a caller that must NOT name <see cref="ControlsConfig"/> can still
        /// calibrate — <c>Golfin.Physics.Viewer</c> (where VersusBot lives) does not reference
        /// <c>Golfin.Gameplay.Config</c>, and adding that reference to load a table would be a
        /// project-wide assembly change bought for one default value.</para>
        /// </summary>
        public static float CalibrateDefault(ControlScheme scheme, float targetMeanAbsYawDeg,
                                             int samples, int seed, out float achievedDeg)
            => Calibrate(scheme, targetMeanAbsYawDeg, ControlsConfig.Default, samples, seed,
                         out achievedDeg);

        // ── The live-shot solve (bot_scheme_parity follow-up, 2026-09-06) ───────
        //
        // WHY A SECOND, RUNTIME CALIBRATION EXISTS AT ALL. A graded scheme's yaw is
        // `m x halfConeRad`, so it scales with the equipped club's Accuracy; Flick's is
        // `+/-aimErrorDegMax` in absolute degrees and scales with nothing. The CSV sigma is solved
        // once, at the reference bot above. That was fine until the acceptance run showed what the
        // bot actually swings: BotClubSync reads ClubContext.EquippedBag — THE LOCAL PLAYER'S BAG.
        // A 1v1 opponent owns no clubs; it swings yours. So a single baked sigma made the
        // OPPONENT'S DIFFICULTY A FUNCTION OF THE PLAYER'S EQUIPMENT — a Supreme driver (Acc 120,
        // 20 deg cone) made the bot miss ~2.6x wider than a Common one (Acc 22, 7.75 deg), at the
        // same bot level. A bot's skill is its bracket, never the player's bag.
        //
        // Re-solving per swing against the LIVE grader fixes that at the root: whatever the club,
        // the expected miss comes back to the bracket's target.

        /// <summary>Fixed standard-normal sample set, drawn once.
        ///
        /// <para>Shared and precomputed so the per-swing solve is DETERMINISTIC (the same club and
        /// power always yield the same sigma — a bot's difficulty must not shimmer between swings)
        /// and cheap: bisection then costs <see cref="LiveSamples"/> grader evaluations per
        /// iteration instead of re-drawing a fresh population each time.</para></summary>
        private static readonly float[] LiveNormals = BuildLiveNormals();

        /// <summary>Population for the per-swing solve. 512 resolves the ~3 % target comfortably
        /// while keeping a whole solve at roughly 10 000 float evaluations — about a millisecond,
        /// once per stroke.</summary>
        public const int LiveSamples = 512;

        private static float[] BuildLiveNormals()
        {
            var rng = new System.Random(DefaultSeed);
            var a = new float[LiveSamples];
            for (int i = 0; i < a.Length; i++) a[i] = Normal(rng);
            return a;
        }

        /// <summary>
        /// Solve the timing-axis sigma that makes THIS shot's expected absolute yaw equal
        /// <paramref name="targetMeanAbsYawDeg"/>, against the live grader.
        /// </summary>
        /// <param name="yawRadForRaw">The scheme's own grader, closed over the live club, power and
        /// cone — so the solve sees exactly the shot that is about to be fired.</param>
        /// <param name="lo">Clamp applied to a raw sample, matching the executor's own clamp.</param>
        /// <returns>The solved sigma, or a saturating ceiling when the target is wider than the
        /// scheme's worst possible mistime (reported through <paramref name="achievedDeg"/>).</returns>
        public static float CalibrateForLiveShot(System.Func<float, float> yawRadForRaw,
                                                 float targetMeanAbsYawDeg,
                                                 float lo, float hi,
                                                 out float achievedDeg)
        {
            achievedDeg = 0f;
            if (yawRadForRaw == null || targetMeanAbsYawDeg <= 0f) return 0f;

            const float Ceiling = 3f;
            float loS = 0f, hiS = Ceiling;

            achievedDeg = LiveMeanAbsYawDeg(yawRadForRaw, hiS, lo, hi);
            if (achievedDeg < targetMeanAbsYawDeg) return hiS;   // saturated — report the ceiling

            float mid = 0f;
            for (int i = 0; i < 24; i++)
            {
                mid = 0.5f * (loS + hiS);
                achievedDeg = LiveMeanAbsYawDeg(yawRadForRaw, mid, lo, hi);
                if (Mathf.Abs(achievedDeg - targetMeanAbsYawDeg) <= TargetTolerance * targetMeanAbsYawDeg)
                    return mid;
                if (achievedDeg < targetMeanAbsYawDeg) loS = mid; else hiS = mid;
            }
            return mid;
        }

        /// <summary>E|yaw| in degrees over the fixed normal population, at one sigma.</summary>
        private static float LiveMeanAbsYawDeg(System.Func<float, float> yawRadForRaw, float sigma,
                                               float lo, float hi)
        {
            double sum = 0d;
            for (int i = 0; i < LiveNormals.Length; i++)
                sum += Mathf.Abs(yawRadForRaw(Mathf.Clamp(LiveNormals[i] * sigma, lo, hi)));
            return (float)(sum / LiveNormals.Length) * Mathf.Rad2Deg;
        }

        /// <summary>Standard normal from a <c>System.Random</c>, Box–Muller. Guarded away from 0
        /// because <c>log(0)</c> is −inf and <c>NextDouble</c> can return exactly 0.</summary>
        private static float Normal(System.Random rng)
        {
            double u1 = Math.Max(1e-12d, rng.NextDouble());
            double u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2));
        }
    }
}
