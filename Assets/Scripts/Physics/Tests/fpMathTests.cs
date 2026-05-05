using NUnit.Framework;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    public class fpMathTests
    {
        [Test]
        public void Sqrt_KnownValues_MatchesRealArithmetic()
        {
            // Tolerance: 1 LSB of Q16.16 = 1/65536 ≈ 1.5e-5. Use 0.001 to allow for
            // integer-rounding off-by-one without making the test brittle.
            const float tol = 0.001f;

            var cases = new[]
            {
                // (input, expected √input)
                (0.0f,        0.0f),
                (1.0f,        1.0f),
                (4.0f,        2.0f),
                (5.005f,      2.2371f),     // putter dot-product from controls_c_diagnosis
                (16.0f,       4.0f),
                (100.0f,      10.0f),
                (10672.0f,    103.305f),    // driver dot-product from controls_c_diagnosis
                (32768.0f,    181.019f),    // upper Q16.16 exposed range
            };

            foreach (var (input, expected) in cases)
            {
                fp actual = fpMath.Sqrt(fp.FromFloat(input));
                Assert.AreEqual(expected, actual.ToFloat(), tol,
                    $"Sqrt({input}) = {actual.ToFloat()}, expected {expected}");
            }
        }

        [Test]
        public void Sqrt_ZeroAndNegative_ReturnsZero()
        {
            Assert.AreEqual(0.0f, fpMath.Sqrt(fp.Zero).ToFloat(), 0f);
            Assert.AreEqual(0.0f, fpMath.Sqrt(fp.FromFloat(-1.0f)).ToFloat(), 0f);
            Assert.AreEqual(0.0f, fpMath.Sqrt(fp.FromFloat(-100.0f)).ToFloat(), 0f);
        }

        [Test]
        public void Sqrt_PerfectSquares_ExactToFpPrecision()
        {
            // For perfect squares of integers, sqrt should be exact in fp arithmetic.
            for (int i = 0; i <= 50; i++)
            {
                float sq = (float)(i * i);
                fp actual = fpMath.Sqrt(fp.FromFloat(sq));
                Assert.AreEqual((float)i, actual.ToFloat(), 0.0001f,
                    $"Sqrt({sq}) should be {i}");
            }
        }

        [Test]
        public void Sqrt_ProducesMonotonicResults()
        {
            // Sqrt is a monotonic function — for inputs a < b, sqrt(a) ≤ sqrt(b).
            // This catches any algorithm that produces wildly inconsistent results
            // (like the buggy Newton that quantizes to powers of 2).
            fp prev = fp.Zero;
            for (int i = 1; i <= 1000; i++)
            {
                fp current = fpMath.Sqrt(fp.FromFloat(i * 0.1f));
                Assert.GreaterOrEqual(current.raw, prev.raw,
                    $"Sqrt({i * 0.1f}) raw={current.raw} less than Sqrt({(i-1) * 0.1f}) raw={prev.raw}");
                prev = current;
            }
        }

        [Test]
        public void Sqrt_RegressionGuard_DriverShotMatch()
        {
            // Direct regression guard for the bug fixed by this spec. The driver shot
            // captured in controls_c_diagnosis observed |v|=64.000 m/s when the real
            // value was ≈103.305 m/s. If this test ever fails, the bug has returned.
            fp dotProduct = fp.FromFloat(10672.0f);  // 100.20² + 17.73² + 17.87²
            fp speed = fpMath.Sqrt(dotProduct);
            Assert.AreEqual(103.305f, speed.ToFloat(), 0.05f,
                "Sqrt regression: driver-shot |v| should be ~103.3 m/s, got " + speed.ToFloat());
            Assert.AreNotEqual(64.000f, speed.ToFloat(),
                "Sqrt regression: 64.000 m/s is the broken-Newton power-of-2 cap. The bug has returned.");
        }

        [Test]
        public void Sqrt_RegressionGuard_PutterShotMatch()
        {
            // Regression guard for the putter shot from controls_c_diagnosis.
            // Real |v| ≈ 2.236 m/s; broken Newton returned 2.000 m/s.
            fp dotProduct = fp.FromFloat(5.005f);   // 2.18² + 0.18² + 0.47²
            fp speed = fpMath.Sqrt(dotProduct);
            Assert.AreEqual(2.236f, speed.ToFloat(), 0.01f,
                "Sqrt regression: putter-shot |v| should be ~2.236 m/s, got " + speed.ToFloat());
        }
    }
}
