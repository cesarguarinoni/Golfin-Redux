using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Test-seam interface for LoopCameraDirector. ChaseCamera implements this;
    /// tests use RecordingModeSetter. Allows Director tests to verify mode transitions
    /// without instantiating a Camera GameObject.
    /// </summary>
    public interface IModeSetter
    {
        ChaseCamera.Mode CurrentMode { get; }
        void SetMode(ChaseCamera.Mode mode);
        void SetTarget(Transform t);
        void ResetToOrigin(Vector3 origin, Vector3 launchDir);
        void SetDownrangeFraming(Vector3 pos, Vector3 lookAt);
        void SetCupZoomFocus(Vector3 focus);
        void SetOBFreezePivot(Vector3 pivot);
        /// <summary>
        /// Arms (or disarms) a clamp on the Chase-mode camera position along the shot axis.
        /// When active, the camera stops advancing once the ball's projected progress reaches
        /// clampPoint, but continues rotating to track the ball.
        /// Cleared automatically by ResetToOrigin so it never leaks into the next shot.
        /// </summary>
        void SetChaseClamp(Vector3 clampPoint, bool active);
    }
}
