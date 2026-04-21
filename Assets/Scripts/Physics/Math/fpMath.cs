namespace Golfin.Physics.Math
{
    public static class fpMath
    {
        // Babylonian/Newton integer sqrt on the raw long. Deterministic.
        // Used sparingly; OK to be slower than platform sqrt.
        public static fp Sqrt(fp x)
        {
            if (x.raw <= 0) return fp.Zero;
            // Work in Q16.16: result.raw² / 2^16 ≈ x.raw
            // → result.raw ≈ sqrt(x.raw * 2^16) = sqrt(x.raw) * 256
            long v = x.raw;
            long n = v << 16;
            // Guard: if n overflowed (v >> 48 != 0 before shift), use double fallback
            if ((v >> 48) != 0)
            {
                double d = System.Math.Sqrt(x.ToDouble());
                return fp.FromDouble(d);
            }
            // Good initial guess: bit-shift to ~2^(floor(log2(n)/2)+1).
            // Starting from r=n requires ~22 halvings to reach sqrt for typical
            // golf-ball speeds, but the loop only runs 20 — causing severe under-convergence.
            long r = 1L;
            long tmp = n;
            while (tmp > 3L) { tmp >>= 2; r <<= 1; }
            long prev;
            for (int i = 0; i < 40 && r != 0; i++)
            {
                prev = r;
                r = (r + n / r) >> 1;
                if (r >= prev) { r = prev; break; }
            }
            return fp.FromRaw(r);
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
    }
}
