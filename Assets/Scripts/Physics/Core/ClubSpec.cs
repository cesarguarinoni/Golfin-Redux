using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>One row of Resources/Physics/clubs.csv.</summary>
    public struct ClubSpec
    {
        public string Id;               // "Driver", "Iron7", "PitchingWedge", etc.
        public fp BallSpeedMps;         // ball speed at impact, m/s
        public fp LaunchAngleDeg;       // launch angle above horizontal, degrees
        public fp SpinRateRpm;          // backspin, rpm
        public fp ExpectedCarryYd;      // Trackman average carry, yards
    }
}
