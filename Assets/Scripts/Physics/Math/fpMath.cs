namespace Golfin.Physics.Math
{
    public static class fpMath
    {
        // Digit-by-digit shift-and-subtract square root (Wikipedia "Methods of computing
        // square roots → Binary numeral system"; ported from libfixmath fix16_sqrt.c, MIT).
        //
        // Single-pass int64 version (libfixmath uses two int32 passes for embedded
        // targets; GolfinRedux's fp.raw is long so we don't need that split).
        //
        // Deterministic by construction: no convergence test, no iteration count, no
        // early-exit. Processes a fixed sequence of bit positions, producing the
        // integer-rounded sqrt to fp precision.
        //
        // HISTORY: previous Newton-Raphson implementation had a convergence-test bug
        // that returned the initial-guess power of 2 (typically 64.0 for driver-class
        // inputs, 2.0 for putter-class inputs). The "loop only runs 20" comment was
        // misdiagnosis; bug was structural in `if (r >= prev) break`. Do NOT revert.
        public static fp Sqrt(fp x)
        {
            if (x.raw <= 0) return fp.Zero;

            // We want sqrt(x) where x is Q16.16. Computing integer sqrt of (x.raw << 16)
            // gives result.raw such that result.raw² / 2¹⁶ ≈ x.raw, i.e., result is the
            // Q16.16 representation of √(x as fp).
            long n      = x.raw << 16;
            long result = 0L;

            // Find highest power-of-4 ≤ n. Start at 2⁶⁰ (largest even-position bit
            // that fits in signed long) and halve by 4 until ≤ n.
            long bit = 1L << 60;
            while (bit > n) bit >>= 2;

            // Digit-by-digit loop: for each bit position from high to low, test whether
            // including this bit keeps result² ≤ n. Process pairs of binary digits
            // (one output bit per two input bits, hence bit >>= 2).
            while (bit != 0L)
            {
                if (n >= result + bit)
                {
                    n      -= result + bit;
                    result  = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }
                bit >>= 2;
            }

            // Rounding: if true sqrt is closer to result+1 than to result, round up.
            // (This is the "remainder > divisor" test from long-division sqrt.)
            if (n > result) result++;

            return fp.FromRaw(result);
        }

        // Taylor-series sin/cos. 7 terms — deterministic, adequate for shot-setup time.
        // Angle in radians, reduced to [-π, π] first.
        private static readonly fp PI = fp.FromDouble(System.Math.PI);
        private static readonly fp TwoPI = fp.FromDouble(2.0 * System.Math.PI);

        // Exposed for WindModel (and any future phase) that needs 2π as a deterministic constant.
        public static readonly fp TwoPi = fp.FromDouble(2.0 * System.Math.PI);

        private static fp ReduceAngle(fp a)
        {
            while (a > PI) a = a - TwoPI;
            while (a < -PI) a = a + TwoPI;
            return a;
        }

        public static fp Sin(fp a)
        {
            a = ReduceAngle(a);
            fp a2 = a * a;
            fp a3 = a2 * a;
            fp a5 = a3 * a2;
            fp a7 = a5 * a2;
            return a
                - a3 / fp.FromInt(6)
                + a5 / fp.FromInt(120)
                - a7 / fp.FromInt(5040);
        }

        public static fp Cos(fp a)
        {
            a = ReduceAngle(a);
            fp a2 = a * a;
            fp a4 = a2 * a2;
            fp a6 = a4 * a2;
            return fp.One
                - a2 / fp.FromInt(2)
                + a4 / fp.FromInt(24)
                - a6 / fp.FromInt(720);
        }

        // Phase 2: added for aero model.
        public static fp Dot(fp3 a, fp3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        // Phase 2: added for aero model.
        public static fp3 Cross(fp3 a, fp3 b) => new fp3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x);

        // Phase 2: added for aero model.
        public static fp3 Normalize(fp3 v)
        {
            fp lenSq = Dot(v, v);
            if (lenSq <= fp.Epsilon) return new fp3(fp.Zero, fp.Zero, fp.One);
            return v / Sqrt(lenSq);
        }

        // Phase 2: added for aero model.
        public static fp Clamp(fp value, fp min, fp max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static fp Min(fp a, fp b) => a < b ? a : b;
        public static fp Max(fp a, fp b) => a > b ? a : b;

        public static readonly fp Pi        = fp.FromDouble(System.Math.PI);
        public static readonly fp DegToRad  = fp.FromDouble(System.Math.PI / 180.0);
    }
}
