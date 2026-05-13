using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2e: computes the camera yaw angle needed to face a target world position
    /// from a ball position. XZ-only (Y is irrelevant for yaw).
    /// </summary>
    public static class AimRotationHelper
    {
        /// <summary>
        /// Returns yaw in radians for camera at ballPos to face pinPos.
        /// Falls back to fallbackYaw if pinPos is Vector3.zero (unset) or the
        /// XZ distance squared is less than 1e-4 (< 1cm) — too small to define
        /// a stable direction.
        /// </summary>
        public static float ComputeYawTowardPin(Vector3 ballPos, Vector3 pinPos, float fallbackYaw)
        {
            if (pinPos == Vector3.zero) return fallbackYaw;
            float dx = pinPos.x - ballPos.x;
            float dz = pinPos.z - ballPos.z;
            if (dx * dx + dz * dz < 0.0001f) return fallbackYaw;
            return Mathf.Atan2(dz, dx);
        }
    }
}
