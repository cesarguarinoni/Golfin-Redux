using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// EditMode tests for fade/draw tilt formula (fade_draw_core_wiring Order 356).
    ///
    /// Verifies:
    ///   1. fadeDrawInput is dominant: fade/draw at ±1 produces larger tilt than spinX at ±1 (D3).
    ///   2. spinX is trim: spinX at ±1 with trimmed SpinMaxTiltRad is a fraction of fadeDrawMaxTiltRad.
    ///   3. Signs are correct: fadeDrawInput +1 tilts same direction as spinX +1 would have.
    ///   4. Combined: fadeDraw + spinTrim add correctly.
    ///   5. Default params = 0 → legacy no-op (preserves existing ShotInputBuilderSpinTests behavior).
    ///   6. Determinism: same seed + same inputs → identical ShotInput.
    /// </summary>
    public class FadeDrawTiltTests
    {
        const float Tol = 0.02f;

        // Config values matching ControlsConfig.Default (fade_draw_core_wiring)
        const float FadeDrawMaxTiltRad = 0.3f;    // dominant
        const float SpinTrimMaxTiltRad = 0.075f;  // trim (D3: ~1/4 of prior 0.3)

        static StatBundle DriverBundle()
        {
            var club = ClubStats.DefaultDriver;
            return new StatBundle(club, BallStats.Neutral, CharacterStats.Neutral,
                fp.FromInt(100), fp.FromInt(100));
        }

        /// <summary>Build a shot with explicit fade/draw + spin trim params.</summary>
        static SpinState BuildSpinWithFadeDraw(
            float fadeDrawInput, float fadeDrawMaxTilt,
            float spinX, float spinMaxTilt,
            uint seed = 42u)
        {
            var bundle = DriverBundle();
            var (shotInput, _) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.One,
                fp.Zero,
                fp.Zero, fp.Zero, fp.Zero,
                seed,
                default,
                fp.FromFloat(spinX),           // spinInputX (trim)
                fp.Zero,                        // spinInputY (untouched)
                fp.FromFloat(1.5f),             // spinMagScaleSlope
                fp.FromFloat(spinMaxTilt),      // spinMaxTiltRad (trim value)
                fp.FromFloat(fadeDrawInput),    // fadeDrawInput
                fp.FromFloat(fadeDrawMaxTilt)); // fadeDrawMaxTiltRad
            return shotInput.Spin;
        }

        // ── Test 1: fadeDrawInput=0, spinX=0 → legacy backspin (no tilt) ────────

        [Test]
        public void BothZero_ProducesUntiledLegacyAxis()
        {
            // At aimYaw=0: legacy axis = (0, 0, 1).
            var spin = BuildSpinWithFadeDraw(0f, FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);
            Assert.AreEqual(0f, spin.Axis.x.ToFloat(), Tol, "No tilt: axis.x=0");
            Assert.AreEqual(0f, spin.Axis.y.ToFloat(), Tol, "No tilt: axis.y=0");
            Assert.AreEqual(1f, spin.Axis.z.ToFloat(), Tol, "No tilt: axis.z=1");
        }

        // ── Test 2: fadeDraw is dominant over spinTrim ────────────────────────────

        [Test]
        public void FadeDraw_ProducesLargerTiltThanSpinTrim()
        {
            // FadeDraw at full deflection should curve more than max sidespin.
            // Measure by comparing the y-component of the axis (which captures the tilt into/out of plane).
            var spinFD   = BuildSpinWithFadeDraw(1f,  FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);
            var spinTrim = BuildSpinWithFadeDraw(0f,  FadeDrawMaxTiltRad, 1f, SpinTrimMaxTiltRad);

            float fdTilt   = Mathf.Abs(spinFD.Axis.y.ToFloat());
            float trimTilt = Mathf.Abs(spinTrim.Axis.y.ToFloat());

            Assert.Greater(fdTilt, trimTilt,
                $"fadeDrawInput=1 tilt ({fdTilt:F4}) must exceed spinX=1 trim tilt ({trimTilt:F4})");
        }

        // ── Test 3: spin trim tilt is approximately 1/4 of fade/draw max tilt ────

        [Test]
        public void SpinTrim_IsApproximatelyQuarterOfFadeDrawMax()
        {
            // The tilt angle is proportional to maxTiltRad, so at aimYaw=0:
            // spinTrimMaxTiltRad / fadeDrawMaxTiltRad ≈ 0.25.
            // The y-component of the axis reflects this ratio (Rodrigues rotation, small angle approx).
            var spinFD   = BuildSpinWithFadeDraw(1f, FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);
            var spinTrim = BuildSpinWithFadeDraw(0f, FadeDrawMaxTiltRad, 1f, SpinTrimMaxTiltRad);

            float fdTilt   = Mathf.Abs(spinFD.Axis.y.ToFloat());
            float trimTilt = Mathf.Abs(spinTrim.Axis.y.ToFloat());
            float ratio    = trimTilt / fdTilt;

            // ratio should be close to SpinTrimMaxTiltRad/FadeDrawMaxTiltRad = 0.075/0.3 = 0.25
            Assert.AreEqual(0.25f, ratio, 0.05f, "Trim tilt / fadeDraw tilt ≈ 0.25 (1/4)");
        }

        // ── Test 4: sign convention — fadeDraw +1 and spinX +1 tilt same axis direction ─

        [Test]
        public void FadeDrawPositive_AndSpinXPositive_TiltSameDirection()
        {
            // Both should produce a positive y-component in the final axis
            // (or both negative — the sign convention must be consistent).
            var spinFD   = BuildSpinWithFadeDraw(1f,  FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);
            var spinTrim = BuildSpinWithFadeDraw(0f,  FadeDrawMaxTiltRad, 1f, SpinTrimMaxTiltRad);

            float signFD   = Mathf.Sign(spinFD.Axis.y.ToFloat());
            float signTrim = Mathf.Sign(spinTrim.Axis.y.ToFloat());

            Assert.AreEqual(signFD, signTrim,
                "fadeDrawInput=+1 and spinX=+1 must tilt the axis in the same direction");
        }

        // ── Test 5: combined fadeDraw + spinTrim adds correctly ────────────────

        [Test]
        public void FadeDrawPlusTrim_CombinesAdditively()
        {
            // tiltAngle = fadeDraw * fdMax + spinX * trimMax
            // At aimYaw=0, the y-component of the final axis scales with tiltAngle.
            // combined.Axis.y should be between pure-FD.Axis.y and pure-trim.Axis.y,
            // specifically fadeDraw=1 + spinX=1 should produce larger tilt than fadeDraw=1 alone.
            var spinFD       = BuildSpinWithFadeDraw(1f, FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);
            var spinCombined = BuildSpinWithFadeDraw(1f, FadeDrawMaxTiltRad, 1f, SpinTrimMaxTiltRad);

            float tiltFD       = Mathf.Abs(spinFD.Axis.y.ToFloat());
            float tiltCombined = Mathf.Abs(spinCombined.Axis.y.ToFloat());

            Assert.Greater(tiltCombined, tiltFD,
                "Combined fadeDraw=1+spinTrim=1 tilt must exceed fadeDraw=1 alone");
        }

        // ── Test 6: defaults (no fadeDraw params) → unaffected by new params ─────

        [Test]
        public void DefaultParams_LegacyNoOp_SpinXOnly()
        {
            // Calling Build WITHOUT fadeDrawInput/fadeDrawMaxTiltRad (defaults=0)
            // should behave exactly like the old spinX path with old SpinMaxTiltRad.
            // This verifies the legacy no-op contract.
            var bundle = DriverBundle();
            float legacyTiltRad = 0.3f; // old value before D3

            var (shotInputLegacy, _) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.One, fp.Zero,
                fp.Zero, fp.Zero, fp.Zero,
                42u,
                default,
                fp.FromFloat(1f),              // spinInputX = +1
                fp.Zero,
                fp.FromFloat(1.5f),
                fp.FromFloat(legacyTiltRad));   // no fadeDrawInput, no fadeDrawMaxTiltRad

            var (shotInputNew, _) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.One, fp.Zero,
                fp.Zero, fp.Zero, fp.Zero,
                42u,
                default,
                fp.FromFloat(1f),              // spinInputX = +1
                fp.Zero,
                fp.FromFloat(1.5f),
                fp.FromFloat(legacyTiltRad),
                fp.Zero,                        // fadeDrawInput = 0 (default)
                fp.Zero);                       // fadeDrawMaxTiltRad = 0 (default)

            // The two calls must produce the same spin axis.
            Assert.AreEqual(shotInputLegacy.Spin.Axis.x.ToFloat(),
                            shotInputNew.Spin.Axis.x.ToFloat(), Tol, "Legacy no-op: axis.x");
            Assert.AreEqual(shotInputLegacy.Spin.Axis.y.ToFloat(),
                            shotInputNew.Spin.Axis.y.ToFloat(), Tol, "Legacy no-op: axis.y");
            Assert.AreEqual(shotInputLegacy.Spin.Axis.z.ToFloat(),
                            shotInputNew.Spin.Axis.z.ToFloat(), Tol, "Legacy no-op: axis.z");
        }

        // ── Test 7: determinism — same seed+inputs → identical ShotInput ─────────

        [Test]
        public void Determinism_SameSeedAndInputs_IdenticalShotInput()
        {
            uint seed = 99u;
            var bundle = DriverBundle();

            var (input1, _) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.FromFloat(0.8f), fp.FromFloat(0.1f),
                fp.Zero, fp.Zero, fp.Zero,
                seed,
                default,
                fp.FromFloat(0.3f),
                fp.Zero,
                fp.FromFloat(1.5f),
                fp.FromFloat(SpinTrimMaxTiltRad),
                fp.FromFloat(0.7f),
                fp.FromFloat(FadeDrawMaxTiltRad));

            var (input2, _) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.FromFloat(0.8f), fp.FromFloat(0.1f),
                fp.Zero, fp.Zero, fp.Zero,
                seed,
                default,
                fp.FromFloat(0.3f),
                fp.Zero,
                fp.FromFloat(1.5f),
                fp.FromFloat(SpinTrimMaxTiltRad),
                fp.FromFloat(0.7f),
                fp.FromFloat(FadeDrawMaxTiltRad));

            Assert.AreEqual(input1.velocity.x, input2.velocity.x, "Determinism: velocity.x");
            Assert.AreEqual(input1.velocity.y, input2.velocity.y, "Determinism: velocity.y");
            Assert.AreEqual(input1.velocity.z, input2.velocity.z, "Determinism: velocity.z");
            Assert.AreEqual(input1.Spin.Axis.x, input2.Spin.Axis.x, "Determinism: spin.axis.x");
            Assert.AreEqual(input1.Spin.Axis.y, input2.Spin.Axis.y, "Determinism: spin.axis.y");
            Assert.AreEqual(input1.Spin.Rate,   input2.Spin.Rate,   "Determinism: spin.rate");
        }

        // ── Test 8: fadeDrawInput negative → opposite tilt direction ────────────

        [Test]
        public void FadeDrawNegative_ProducesOppositeAxisToPositive()
        {
            var spinPos = BuildSpinWithFadeDraw( 1f, FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);
            var spinNeg = BuildSpinWithFadeDraw(-1f, FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);

            // y-component (the tilt direction) must be opposite in sign.
            float yPos = spinPos.Axis.y.ToFloat();
            float yNeg = spinNeg.Axis.y.ToFloat();
            Assert.AreEqual(yPos, -yNeg, Tol,
                "fadeDrawInput=-1 must produce opposite y-axis tilt to +1");
        }

        // ── Test 9: FadeDraw axis unit length preserved ───────────────────────────

        [Test]
        public void FadeDrawTilt_AxisRemainsUnitLength()
        {
            for (float fd = -1f; fd <= 1f; fd += 0.25f)
            {
                var spin = BuildSpinWithFadeDraw(fd, FadeDrawMaxTiltRad, 0f, SpinTrimMaxTiltRad);
                float lenSq = spin.Axis.x.ToFloat() * spin.Axis.x.ToFloat()
                            + spin.Axis.y.ToFloat() * spin.Axis.y.ToFloat()
                            + spin.Axis.z.ToFloat() * spin.Axis.z.ToFloat();
                Assert.AreEqual(1f, Mathf.Sqrt(lenSq), 0.02f,
                    $"Unit length at fadeDrawInput={fd:F2}");
            }
        }
    }
}
