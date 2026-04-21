using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Piecewise-linear lookup table over a single independent variable.
    /// Breakpoints must be sorted ascending by X. Lookups below the first X
    /// clamp to the first Y; lookups above the last X clamp to the last Y.
    /// Linear interpolation between breakpoints.
    /// </summary>
    public readonly struct CoefficientLut
    {
        public readonly fp[] X;
        public readonly fp[] Y;

        public CoefficientLut(fp[] x, fp[] y)
        {
            X = x;
            Y = y;
        }

        public fp Evaluate(fp input)
        {
            int n = X.Length;
            if (input <= X[0]) return Y[0];
            if (input >= X[n - 1]) return Y[n - 1];

            // Linear scan — tables are tiny (≤20 rows).
            int i = 0;
            while (i < n - 1 && X[i + 1] < input) i++;

            fp x0 = X[i];
            fp x1 = X[i + 1];
            fp y0 = Y[i];
            fp y1 = Y[i + 1];

            fp span = x1 - x0;
            if (span <= fp.Epsilon) return y0;
            fp t = (input - x0) / span;
            return y0 + (y1 - y0) * t;
        }

        public bool IsValid => X != null && Y != null && X.Length >= 2 && X.Length == Y.Length;
    }
}
