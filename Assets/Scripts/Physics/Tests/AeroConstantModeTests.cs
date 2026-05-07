// AeroConstantModeTests.cs — controls_g_aero_constant_mode_crash (2026-05-07)
// Unit tests for the constant-mode lift branch in AeroModel and AeroConfig.AssertValid.
// Guards against future regressions of the DivideByZeroException at AeroModel.cs line 78
// (spin.Rate / cfg.SpinRateReference) when constant-mode is active.

using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    public class AeroConstantModeTests
    {
        [Test]
        public void Aero_ConstantModeFallback_DoesNotCrashWithDefaultConfig()
        {
            // Arrange: simulate the regression's preconditions — UseLiftLut=false (default),
            // SpinRateReference=300 (default), spinning ball, non-zero velocity.
            var cfg = AeroConfig.Default;
            Assert.IsFalse(cfg.UseLiftLut, "Default.UseLiftLut should be false (sentinel for constant-mode path).");
            Assert.That(cfg.SpinRateReference.ToFloat(), Is.GreaterThan(0f),
                "Default.SpinRateReference must be > 0 to prevent constant-mode divide-by-zero.");

            var velocity = new fp3(fp.FromFloat(60f), fp.FromFloat(20f), fp.Zero);
            var spin     = new SpinState(
                new fp3(fp.Zero, fp.Zero, fp.One),
                fp.FromFloat(300f)); // 300 rad/s ≈ 2860 RPM, typical driver

            // Act + Assert: must not throw (the crash was DivideByZeroException at line 78).
            // Fixed-point (fp) cannot be NaN; the DoesNotThrow assert is the meaningful check.
            Assert.DoesNotThrow(() =>
            {
                AeroModel.ComputeAeroForce(velocity, fp3.Zero, spin, cfg);
            }, "AeroModel.ComputeAeroForce threw unexpectedly in constant-mode with default config.");
        }

        [Test]
        public void Aero_AssertValid_ThrowsOnZeroSpinRateReference()
        {
            var cfg = AeroConfig.Default;
            cfg.SpinRateReference = fp.Zero;
            Assert.Throws<System.InvalidOperationException>(() => cfg.AssertValid(),
                "AssertValid should throw InvalidOperationException when SpinRateReference is 0.");
        }

        [Test]
        public void Aero_AssertValid_PassesOnDefaultConfig()
        {
            var cfg = AeroConfig.Default;
            Assert.DoesNotThrow(() => cfg.AssertValid(),
                "AssertValid should not throw on AeroConfig.Default — all fields are safe.");
        }
    }
}
