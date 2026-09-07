using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// EditMode tests for ShotInputBuilder spin-input wiring.
    /// Verifies the §5.3 spin block added in spin_and_shot_shape_wiring.
    ///
    /// Target: ≥8 new spin tests PASS (all deterministic, no scene).
    /// </summary>
    public class ShotInputBuilderSpinTests
    {
        const float SpinTol = 0.02f; // tolerance for fp-derived rate comparisons

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>Driver bundle with deterministic stats (matches PhysicsLabController.LabClubs[0]).</summary>
        static StatBundle DriverBundle()
        {
            var club = ClubStats.DefaultDriver;
            return new StatBundle(club, BallStats.Neutral, CharacterStats.Neutral,
                fp.FromInt(100), fp.FromInt(100));
        }

        /// <summary>Putter bundle (IsPutt=true).</summary>
        static StatBundle PutterBundle()
        {
            var putter = PutterStats.DefaultPutter;
            return new StatBundle(putter, BallStats.Neutral, CharacterStats.Neutral,
                fp.FromInt(100), fp.FromInt(100));
        }

        /// <summary>Call Build with the given spinInput + slope/tilt; returns the spin state.</summary>
        static SpinState BuildSpin(Vector2 spinInput, float slope = 1.5f, float tiltRad = 0.3f,
            bool usePutter = false)
        {
            var bundle = usePutter ? PutterBundle() : DriverBundle();
            var (shotInput, _) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.One,          // full power
                fp.Zero,         // aimYaw = 0 rad (+X forward)
                fp.Zero, fp.Zero, fp.Zero,
                42u,
                default,
                fp.FromFloat(spinInput.x),   // spinInputX — fp; no UnityEngine ref needed in Build()
                fp.FromFloat(spinInput.y),   // spinInputY
                fp.FromFloat(slope),
                fp.FromFloat(tiltRad));
            return shotInput.Spin;
        }

        // ── Test 1: spinInput=(0,0) → legacy backspin axis ───────────────────

        [Test]
        public void SpinInput_Zero_ProducesLegacyBackspinAxis()
        {
            // With spinInput=(0,0) the axis must match the old formula: (-sinYaw, 0, cosYaw).
            // At aimYaw=0: sinYaw=0, cosYaw=1 → axis = (0, 0, 1).
            SpinState spin = BuildSpin(Vector2.zero);

            Assert.AreEqual(0f, spin.Axis.x.ToFloat(), SpinTol, "Legacy axis x at aimYaw=0");
            Assert.AreEqual(0f, spin.Axis.y.ToFloat(), SpinTol, "Legacy axis y at aimYaw=0");
            Assert.AreEqual(1f, spin.Axis.z.ToFloat(), SpinTol, "Legacy axis z at aimYaw=0");
        }

        // ── Test 2: spinY=+0.5 reduces backspin magnitude ───────────────────

        [Test]
        public void SpinInput_PositiveY_ReducesBackspinMagnitude()
        {
            // spinY=+0.5, slope=1.5 → magScale = 1 - 0.5*1.5 = 0.25.
            // Rate = 0.25 × baseSpinMag.
            SpinState spinBaseline = BuildSpin(Vector2.zero);  // magScale=1
            SpinState spinHalf     = BuildSpin(new Vector2(0f, 0.5f)); // magScale=0.25

            // Rate at spinY=0.5 must be ~25% of baseline.
            float expected = spinBaseline.Rate.ToFloat() * 0.25f;
            Assert.AreEqual(expected, spinHalf.Rate.ToFloat(), expected * 0.05f,
                "spinY=+0.5 rate ≈ 0.25 × baseline");
        }

        // ── Test 3: spinY=+1 flips axis to topspin ──────────────────────────

        [Test]
        public void SpinInput_FullPositiveY_FlipsAxisToTopspin()
        {
            // spinY=+1, slope=1.5 → magScale = 1 - 1.0*1.5 = -0.5 (negative → topspin).
            // Axis must be negated (flipped) vs baseline, Rate = 0.5 × baseline.
            SpinState spinBaseline = BuildSpin(Vector2.zero);
            SpinState spinTop      = BuildSpin(new Vector2(0f, 1f));

            // At aimYaw=0 baseline axis = (0,0,1); topspin axis = (0,0,-1).
            Assert.AreEqual(-spinBaseline.Axis.x.ToFloat(), spinTop.Axis.x.ToFloat(), SpinTol, "Topspin axis x negated");
            Assert.AreEqual(-spinBaseline.Axis.y.ToFloat(), spinTop.Axis.y.ToFloat(), SpinTol, "Topspin axis y negated");
            Assert.AreEqual(-spinBaseline.Axis.z.ToFloat(), spinTop.Axis.z.ToFloat(), SpinTol, "Topspin axis z negated");

            float expectedRate = spinBaseline.Rate.ToFloat() * 0.5f;
            Assert.AreEqual(expectedRate, spinTop.Rate.ToFloat(), expectedRate * 0.05f,
                "Topspin rate = 0.5 × baseline");
        }

        // ── Test 4: spinY=-1 boosts backspin ─────────────────────────────────

        [Test]
        public void SpinInput_NegativeY_BoostsBackspinMagnitude()
        {
            // spinY=-1, slope=1.5 → magScale = 1 - (-1)*1.5 = 2.5.
            // Rate = 2.5 × baseline; axis unchanged.
            SpinState spinBaseline = BuildSpin(Vector2.zero);
            SpinState spinMaxBack  = BuildSpin(new Vector2(0f, -1f));

            float expected = spinBaseline.Rate.ToFloat() * 2.5f;
            Assert.AreEqual(expected, spinMaxBack.Rate.ToFloat(), expected * 0.05f,
                "spinY=-1 rate ≈ 2.5 × baseline");

            // Axis sign unchanged (still backspin).
            Assert.AreEqual(spinBaseline.Axis.x.ToFloat(), spinMaxBack.Axis.x.ToFloat(), SpinTol, "Axis x unchanged at spinY=-1");
            Assert.AreEqual(spinBaseline.Axis.z.ToFloat(), spinMaxBack.Axis.z.ToFloat(), SpinTol, "Axis z unchanged at spinY=-1");
        }

        // ── Test 5: spinX=+1 tilts axis orbitally ────────────────────────────

        [Test]
        public void SpinInput_PositiveX_TiltsAxisOrbitally()
        {
            // spinX=+1, tilt=0.3 → axis rotated 0.3rad around velocity direction.
            // finalAxis must differ from startAxis and remain unit-length.
            SpinState spinBaseline = BuildSpin(Vector2.zero);
            SpinState spinFade     = BuildSpin(new Vector2(1f, 0f));

            // Axes must differ.
            float dx = spinFade.Axis.x.ToFloat() - spinBaseline.Axis.x.ToFloat();
            float dy = spinFade.Axis.y.ToFloat() - spinBaseline.Axis.y.ToFloat();
            float dz = spinFade.Axis.z.ToFloat() - spinBaseline.Axis.z.ToFloat();
            float delta = Mathf.Sqrt(dx*dx + dy*dy + dz*dz);
            Assert.Greater(delta, 0.01f, "spinX=+1 axis must differ from baseline");

            // Final axis must be unit-length (Rodrigues preserves length).
            float lenSq = spinFade.Axis.x.ToFloat() * spinFade.Axis.x.ToFloat()
                        + spinFade.Axis.y.ToFloat() * spinFade.Axis.y.ToFloat()
                        + spinFade.Axis.z.ToFloat() * spinFade.Axis.z.ToFloat();
            Assert.AreEqual(1f, Mathf.Sqrt(lenSq), 0.02f, "Tilted axis is unit-length");
        }

        // ── Test 6: spinX=-1 vs +1 produce mirrored axes ─────────────────────

        [Test]
        public void SpinInput_SymmetricX_ProducesMirroredAxes()
        {
            // Draw (spinX=-1) and Fade (spinX=+1) must produce axes that are mirror images
            // across the velocity plane. At aimYaw=0 the velocity is in the XZ plane,
            // so the y-components of the two axes should be equal in magnitude and opposite in sign.
            SpinState draw = BuildSpin(new Vector2(-1f, 0f));
            SpinState fade = BuildSpin(new Vector2( 1f, 0f));

            Assert.AreEqual(draw.Axis.y.ToFloat(), -fade.Axis.y.ToFloat(), SpinTol,
                "Draw and Fade axis y-components should be opposite (orbital symmetry)");
        }

        // ── Test 7: Putt ignores spinInput ───────────────────────────────────

        [Test]
        public void Putt_IgnoresSpinInput()
        {
            // IsPutt=true → SpinState.None regardless of spinInput.
            SpinState spin = BuildSpin(new Vector2(1f, 1f), usePutter: true);

            Assert.AreEqual(0f, spin.Rate.ToFloat(), SpinTol,
                "Putt with spin input must have Rate=0 (SpinState.None)");
        }

        // ── Test 8: Spin axis remains unit-length after tilt ─────────────────

        [Test]
        public void SpinAxis_RemainsUnitLength_AfterTilt()
        {
            // Rodrigues rotation preserves length; the spin axis must remain unit-length
            // for arbitrary spinX values.
            for (float sx = -1f; sx <= 1f; sx += 0.25f)
            {
                SpinState spin = BuildSpin(new Vector2(sx, 0f));
                float lenSq = spin.Axis.x.ToFloat() * spin.Axis.x.ToFloat()
                            + spin.Axis.y.ToFloat() * spin.Axis.y.ToFloat()
                            + spin.Axis.z.ToFloat() * spin.Axis.z.ToFloat();
                Assert.AreEqual(1f, Mathf.Sqrt(lenSq), 0.02f,
                    $"Axis unit-length at spinX={sx:F2}");
            }
        }
    }

    /// <summary>
    /// miss_grade_duff §3.2 — the DUFF launch flattener, the one new parameter on
    /// <see cref="ShotInputBuilder.Build"/>.
    ///
    /// <para>The load-bearing test is the first one: EVERY caller that does not pass the new
    /// parameter must produce a BIT-IDENTICAL <c>ShotInput</c>. The whole tournament replay path,
    /// every bot, every capture rig and the physics regression baselines run through this
    /// function, and "close enough in Q16.16" is not the same claim.</para>
    /// </summary>
    public class ShotInputBuilderLaunchPitchTests
    {
        static StatBundle DriverBundle() => new StatBundle(
            ClubStats.DefaultDriver, BallStats.Neutral, CharacterStats.Neutral,
            fp.FromInt(100), fp.FromInt(100));

        static StatBundle PutterBundle() => new StatBundle(
            PutterStats.DefaultPutter, BallStats.Neutral, CharacterStats.Neutral,
            fp.FromInt(100), fp.FromInt(100));

        /// <summary>Build at full power, straight, with an explicit launch-pitch scale.</summary>
        static ShotInput Build(StatBundle bundle, fp launchPitchScale)
        {
            var (input, _) = ShotInputBuilder.Build(
                bundle, StatCoefficients.Default, StatCaps.Default,
                fp.One, fp.Zero,
                fp.Zero, fp.Zero, fp.Zero,
                42u,
                // The SEVEN optionals before launchPitchScale: baseVelocityOverrideMps,
                // spinInputX, spinInputY, spinMagScaleSlope, spinMaxTiltRad, fadeDrawInput,
                // fadeDrawMaxTiltRad. Counted out here because getting it wrong silently lands
                // the scale on fadeDrawMaxTiltRad and the test passes against an unchanged pitch.
                default, default, default, default, default, default, default,
                launchPitchScale);
            return input;
        }

        /// <summary>Build the way every pre-miss_grade_duff caller does: without the parameter.</summary>
        static ShotInput BuildLegacy(StatBundle bundle)
        {
            var (input, _) = ShotInputBuilder.Build(
                bundle, StatCoefficients.Default, StatCaps.Default,
                fp.One, fp.Zero,
                fp.Zero, fp.Zero, fp.Zero,
                42u);
            return input;
        }

        static float PitchDeg(ShotInput s)
        {
            var v = new Vector3(s.velocity.x.ToFloat(), s.velocity.y.ToFloat(), s.velocity.z.ToFloat());
            return Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
        }

        static void AssertVelocityBitIdentical(ShotInput a, ShotInput b, string because)
        {
            Assert.AreEqual(a.velocity.x.raw, b.velocity.x.raw, because + " (x)");
            Assert.AreEqual(a.velocity.y.raw, b.velocity.y.raw, because + " (y)");
            Assert.AreEqual(a.velocity.z.raw, b.velocity.z.raw, because + " (z)");
        }

        [Test]
        public void DefaultAndExplicitOne_AreBitIdenticalToTheLegacyCall()
        {
            var legacy = BuildLegacy(DriverBundle());

            AssertVelocityBitIdentical(legacy, Build(DriverBundle(), default),
                "the fp.Zero default is the legacy no-op every other optional here uses");
            AssertVelocityBitIdentical(legacy, Build(DriverBundle(), fp.One),
                "an explicit 1.0 must skip the branch, not multiply by one");
        }

        [Test]
        public void Scale035_FlattensThePitchToLoftTimesTheScale()
        {
            float loftPitch = PitchDeg(BuildLegacy(DriverBundle()));
            float duffPitch = PitchDeg(Build(DriverBundle(), fp.FromFloat(0.35f)));

            Assert.Greater(loftPitch, 2f / 0.35f,
                "fixture: the driver's loft must be high enough that 0.35x clears the 2 deg floor, " +
                "or this test would only be exercising the clamp");
            Assert.AreEqual(loftPitch * 0.35f, duffPitch, 0.15f);
            Assert.Less(duffPitch, loftPitch);
        }

        [Test]
        public void TheScaleNeverDropsBelowTwoDegrees()
        {
            // A pitch at or below zero is a shot the simulation has never been asked to run.
            float pitch = PitchDeg(Build(DriverBundle(), fp.FromFloat(0.001f)));
            Assert.AreEqual(2f, pitch, 0.15f);
        }

        [Test]
        public void TheScaleNeverRaisesThePitchAboveTheClubsOwnLoft()
        {
            float loftPitch = PitchDeg(BuildLegacy(DriverBundle()));
            float scaledUp  = PitchDeg(Build(DriverBundle(), fp.FromFloat(3f)));
            Assert.AreEqual(loftPitch, scaledUp, 0.15f,
                "a duff tops the ball; it can never launch it HIGHER than the club could");
        }

        [Test]
        public void APuttIgnoresTheScaleEntirely()
        {
            // D3: a putter's ~3 degrees of loft has nothing to top, and the 2-degree floor would
            // RAISE a scaled putt rather than flatten it. Bit-identical, not merely close.
            var legacy = BuildLegacy(PutterBundle());
            AssertVelocityBitIdentical(legacy, Build(PutterBundle(), fp.FromFloat(0.35f)),
                "putts are unaffected by MissLaunchPitchScale");
        }
    }
}
