// PHYSICS_MATH_LIB: hand-rolled Q16.16 fixed-point (Part A.alt) — no external package.
// Packages (danielmansson, asik) were not attempted; hand-rolled is sufficient for Phase 1.
namespace Golfin.Physics.Math
{
    // Q16.16 fixed-point wrapped in long for intermediate multiply headroom.
    // Stored as long internally to avoid 32-bit multiply overflow during
    // (a.raw * b.raw) >> 16. Exposed range: ±32768.0, precision ~15μm.
    public readonly struct fp
    {
        private const int FracBits = 16;
        private const long FracScale = 1L << FracBits;
        public readonly long raw;

        private fp(long raw) { this.raw = raw; }

        public static fp FromRaw(long r) => new fp(r);
        public static fp FromInt(int i) => new fp((long)i << FracBits);
        public static fp FromFloat(float f) => new fp((long)System.Math.Round(f * FracScale));
        public static fp FromDouble(double d) => new fp((long)System.Math.Round(d * FracScale));
        public float ToFloat() => (float)raw / FracScale;
        public double ToDouble() => (double)raw / FracScale;

        public static readonly fp Zero = new fp(0);
        public static readonly fp One = new fp(FracScale);

        public static fp operator +(fp a, fp b) => new fp(a.raw + b.raw);
        public static fp operator -(fp a, fp b) => new fp(a.raw - b.raw);
        public static fp operator -(fp a) => new fp(-a.raw);
        public static fp operator *(fp a, fp b) => new fp((a.raw * b.raw) >> FracBits);
        public static fp operator /(fp a, fp b) => new fp((a.raw << FracBits) / b.raw);

        public static bool operator <(fp a, fp b) => a.raw < b.raw;
        public static bool operator >(fp a, fp b) => a.raw > b.raw;
        public static bool operator <=(fp a, fp b) => a.raw <= b.raw;
        public static bool operator >=(fp a, fp b) => a.raw >= b.raw;
        public static bool operator ==(fp a, fp b) => a.raw == b.raw;
        public static bool operator !=(fp a, fp b) => a.raw != b.raw;

        public override bool Equals(object o) => o is fp f && f.raw == raw;
        public override int GetHashCode() => raw.GetHashCode();
        public override string ToString() => ToFloat().ToString("F4");
    }

    public readonly struct fp3
    {
        public readonly fp x, y, z;
        public fp3(fp x, fp y, fp z) { this.x = x; this.y = y; this.z = z; }
        public static fp3 Zero => new fp3(fp.Zero, fp.Zero, fp.Zero);
        public static fp3 operator +(fp3 a, fp3 b) => new fp3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static fp3 operator -(fp3 a, fp3 b) => new fp3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static fp3 operator *(fp3 a, fp s) => new fp3(a.x * s, a.y * s, a.z * s);
        public static fp3 operator /(fp3 a, fp s) => new fp3(a.x / s, a.y / s, a.z / s);
    }
}
