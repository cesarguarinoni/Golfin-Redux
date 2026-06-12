using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Profile for one tree prefab type: cylinder dimensions and physics coefficients.
    /// All values are at scale=1.0; instance scale multiplies trunk/canopy dimensions.
    /// </summary>
    public sealed class TreeCollisionProfile
    {
        public readonly string PrefabName;
        public readonly fp TrunkRadius;
        public readonly fp TrunkHeight;     // ground → trunk top
        public readonly fp CanopyRadius;
        public readonly fp CanopyTop;       // ground → canopy top
        public readonly fp TrunkRestitution;
        public readonly fp CanopyHitDamping;

        public TreeCollisionProfile(
            string prefabName,
            fp trunkRadius, fp trunkHeight,
            fp canopyRadius, fp canopyTop,
            fp trunkRestitution, fp canopyHitDamping)
        {
            PrefabName       = prefabName;
            TrunkRadius      = trunkRadius;
            TrunkHeight      = trunkHeight;
            CanopyRadius     = canopyRadius;
            CanopyTop        = canopyTop;
            TrunkRestitution = trunkRestitution;
            CanopyHitDamping = canopyHitDamping;
        }
    }

    /// <summary>
    /// One baked tree instance for the sim. baseY is world Y at the tree's ground contact.
    /// scale multiplies trunk/canopy dimensions from the profile.
    /// </summary>
    public sealed class TreeInstance
    {
        public readonly fp WorldX;
        public readonly fp WorldZ;
        public readonly fp BaseY;
        public readonly fp Scale;
        public readonly TreeCollisionProfile Profile;

        // Pre-scaled convenience accessors (avoids repeated multiply in hot-path).
        public fp TrunkRadius   => Profile.TrunkRadius   * Scale;
        public fp TrunkHeight   => Profile.TrunkHeight   * Scale;
        public fp CanopyRadius  => Profile.CanopyRadius  * Scale;
        public fp CanopyTop     => Profile.CanopyTop     * Scale;

        public fp TrunkTopY     => BaseY + TrunkHeight;
        public fp CanopyTopY    => BaseY + CanopyTop;

        public TreeInstance(fp worldX, fp worldZ, fp baseY, fp scale, TreeCollisionProfile profile)
        {
            WorldX  = worldX;
            WorldZ  = worldZ;
            BaseY   = baseY;
            Scale   = scale;
            Profile = profile;
        }
    }

    /// <summary>
    /// Result of a segment-vs-tree test.
    /// </summary>
    public struct TreeHit
    {
        /// <summary>Normalized time within the step [0,1] at which the hit occurs.</summary>
        public fp Frac;
        /// <summary>World-space hit position.</summary>
        public fp3 HitPos;
        /// <summary>Outward XZ normal (unit) pointing away from cylinder axis toward ball.</summary>
        public fp3 NormalXZ;
        /// <summary>True = trunk hit (reflect + restitution). False = canopy (damping only).</summary>
        public bool IsTrunk;
        /// <summary>The profile whose coefficients apply.</summary>
        public TreeCollisionProfile Profile;
    }

    /// <summary>
    /// Interface for the tree obstacle spatial query. Null implementation = no trees.
    /// </summary>
    public interface ITreeObstacleProvider
    {
        /// <summary>
        /// Test a segment [p0→p1] against all nearby trees.
        /// Returns true if the earliest hit is a trunk crossing (isTrunk=true) or
        /// the ball is entering a canopy (isTrunk=false).
        /// Trunk hit: earliest wall-crossing, reflect XZ velocity.
        /// Canopy hit: ball crosses from outside to inside the canopy cylinder → one-time entry impulse (vel *= canopyHitDamping).
        /// </summary>
        bool TestSegment(fp3 p0, fp3 p1, out TreeHit hit);
    }
}
