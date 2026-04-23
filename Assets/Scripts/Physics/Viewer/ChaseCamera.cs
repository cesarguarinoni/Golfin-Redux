using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Camera controller with three modes for the physics lab.
    /// Attach to the scene's Main Camera.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        public enum Mode { Chase, Overhead, GroundLevel }

        [SerializeField] Mode  startMode = Mode.Chase;
        [SerializeField] float smoothTime = 0.15f;

        Mode      _mode;
        Transform _target;
        Vector3   _shotOrigin;
        Vector3   _launchDir;    // normalized XZ
        Vector3   _velocity;     // for SmoothDamp

        void Awake() => _mode = startMode;

        // ── Public API ─────────────────────────────────────────────────────────

        public Mode CurrentMode => _mode;
        public void SetMode(Mode m)        => _mode   = m;
        public void SetTarget(Transform t) => _target = t;

        public void ResetToOrigin(Vector3 origin, Vector3 launchDir)
        {
            _shotOrigin = origin;
            _launchDir  = new Vector3(launchDir.x, 0f, launchDir.z).normalized;
            if (_launchDir == Vector3.zero) _launchDir = Vector3.forward;
            _velocity   = Vector3.zero;
        }

        // ── Unity loop ─────────────────────────────────────────────────────────

        void LateUpdate()
        {
            // Chase requires a live target; Overhead/Ground work with or without one.
            if (_target == null && _mode == Mode.Chase) return;

            // Use live target position when available, fall back to last shot origin.
            Vector3 focus = _target != null ? _target.position : _shotOrigin;

            Vector3    desiredPos;
            Quaternion desiredRot;

            switch (_mode)
            {
                case Mode.Overhead:
                    desiredPos = focus + Vector3.up * 40f;
                    desiredRot = Quaternion.Euler(90f, 0f, 0f);
                    break;

                case Mode.GroundLevel:
                    desiredPos = _shotOrigin + Vector3.up * 1.6f;
                    Vector3 lookAt = focus != _shotOrigin ? focus : _shotOrigin + _launchDir * 10f;
                    desiredRot = Quaternion.LookRotation(lookAt - desiredPos);
                    break;

                default: // Chase
                    desiredPos = focus - _launchDir * 8f + Vector3.up * 3f;
                    desiredRot = Quaternion.LookRotation(focus - desiredPos);
                    break;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPos,
                                                     ref _velocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot,
                                                   10f * Time.deltaTime);
        }
    }
}
