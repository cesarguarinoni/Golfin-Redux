using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Profile for one class of bridge part (railing, pier, …): the physics coefficients
    /// applied when the ball strikes it. Mirrors <see cref="TreeCollisionProfile"/>.
    ///
    /// DESIGN-FEEL VALUES, ARCHITECT-TUNABLE — see Assets/Resources/Data/bridge_collision_profiles.csv.
    /// </summary>
    public sealed class BridgeCollisionProfile
    {
        public readonly string PartName;
        /// <summary>Restitution applied to the reflected NORMAL component of XZ velocity.</summary>
        public readonly fp Restitution;
        /// <summary>Fraction of the TANGENTIAL XZ speed retained after the strike (1 = frictionless).</summary>
        public readonly fp TangentDamping;

        public BridgeCollisionProfile(string partName, fp restitution, fp tangentDamping)
        {
            PartName       = partName;
            Restitution    = restitution;
            TangentDamping = tangentDamping;
        }
    }

    /// <summary>
    /// One baked bridge part: a yaw-rotated box, axis-aligned in Y.
    ///
    /// WHY YAW-ONLY. The source colliders can be tilted (hole 12 has two x-tilted bridges,
    /// hole 17 one). Rather than carry a full orientation into the fixed-point hot path, the
    /// baker folds each box's tilt into its own <see cref="BaseY"/>/<see cref="TopY"/> — the
    /// Y span of its eight world corners — so the runtime primitive stays a yaw-rotated AABB.
    /// Cheap, and exactly reproducible step for step.
    ///
    /// CosYaw/SinYaw are BAKED, never trig'd at runtime: fpMath.Sin/Cos are table-driven and
    /// re-deriving them per step would be both slower and a second place for rounding to enter.
    /// </summary>
    public sealed class BridgeBox
    {
        public readonly fp CenterX;
        public readonly fp CenterZ;
        public readonly fp BaseY;
        public readonly fp TopY;
        public readonly fp HalfX;   // half-extent along the box's own local X (after yaw)
        public readonly fp HalfZ;   // half-extent along the box's own local Z (after yaw)
        public readonly fp CosYaw;
        public readonly fp SinYaw;
        public readonly BridgeCollisionProfile Profile;

        /// <summary>Bounding-circle radius in XZ — used for radius-aware grid insertion.</summary>
        public fp RadiusXZ => fpMath.Sqrt(HalfX * HalfX + HalfZ * HalfZ);

        public BridgeBox(fp centerX, fp centerZ, fp baseY, fp topY,
                         fp halfX, fp halfZ, fp cosYaw, fp sinYaw,
                         BridgeCollisionProfile profile)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            BaseY   = baseY;
            TopY    = topY;
            HalfX   = halfX;
            HalfZ   = halfZ;
            CosYaw  = cosYaw;
            SinYaw  = sinYaw;
            Profile = profile;
        }

        /// <summary>World XZ → this box's local XZ frame (rotation by −yaw about the centre).</summary>
        public void ToLocalXZ(fp worldX, fp worldZ, out fp localX, out fp localZ)
        {
            fp dx = worldX - CenterX;
            fp dz = worldZ - CenterZ;
            localX =  dx * CosYaw + dz * SinYaw;
            localZ = -dx * SinYaw + dz * CosYaw;
        }

        /// <summary>This box's local XZ direction → world XZ (rotation by +yaw).</summary>
        public void ToWorldDirXZ(fp localX, fp localZ, out fp worldX, out fp worldZ)
        {
            worldX = localX * CosYaw - localZ * SinYaw;
            worldZ = localX * SinYaw + localZ * CosYaw;
        }
    }

    /// <summary>Result of a segment-vs-bridge-part test. Mirrors <see cref="TreeHit"/> minus IsTrunk.</summary>
    public struct BridgeHit
    {
        /// <summary>Normalized time within the step [0,1] at which the hit occurs.</summary>
        public fp Frac;
        /// <summary>World-space hit position.</summary>
        public fp3 HitPos;
        /// <summary>Outward XZ normal (unit) of the struck face, pointing away from the box.</summary>
        public fp3 NormalXZ;
        /// <summary>The profile whose coefficients apply.</summary>
        public BridgeCollisionProfile Profile;
    }

    /// <summary>
    /// Interface for the bridge obstacle spatial query. Null implementation = no bridges,
    /// which is every hole except 7 / 8 / 9 / 12 / 17.
    /// </summary>
    public interface IBridgeObstacleProvider
    {
        /// <summary>
        /// Test a segment [p0→p1] against all nearby bridge parts. Returns true with the
        /// EARLIEST hit across every candidate box.
        /// </summary>
        bool TestSegment(fp3 p0, fp3 p1, out BridgeHit hit);
    }
}
