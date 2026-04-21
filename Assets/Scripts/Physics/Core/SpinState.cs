using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Ball spin at the moment of impact. Axis is normalized; rate is rad/s.
    /// Zero spin → use SpinState.None. Check IsSpinning before applying Magnus lift.
    /// </summary>
    public readonly struct SpinState
    {
        public readonly fp3 Axis;   // normalized spin axis
        public readonly fp Rate;    // rad/s, backspin positive

        public SpinState(fp3 axis, fp rate)
        {
            Axis = axis;
            Rate = rate;
        }

        public bool IsSpinning => Rate > fp.Epsilon;

        public SpinState WithRate(fp newRate) =>
            new SpinState(Axis, newRate > fp.Zero ? newRate : fp.Zero);

        public static SpinState None => new SpinState(new fp3(fp.Zero, fp.Zero, fp.One), fp.Zero);
    }
}
