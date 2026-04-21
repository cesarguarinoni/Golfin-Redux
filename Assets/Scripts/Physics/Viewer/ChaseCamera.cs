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

        public void SetMode(Mode m)   => _mode = m;
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
            if (_target == null && _mode != Mode.GroundLevel) return;

            Vector3 desiredPos;
            Quaternion desiredRot;

            switch (_mode)
            {
                case Mode.Chase:
                    desiredPos = _target.position - _launchDir * 8f + Vector3.up * 3f;
                    desiredRot = Quaternion.LookRotation(_target.position - desiredPos);
                    break;

                case Mode.Overhead:
                    desiredPos = _target.position + Vector3.up * 40f;
                    desiredRot = Quaternion.Euler(90f, 0f, 0f);
                    break;

                case Mode.GroundLevel:
                default:
                    desiredPos = _shotOrigin + Vector3.up * 1.6f;
                    Vector3 lookAt = _target != null ? _target.position : _shotOrigin + _launchDir * 10f;
                    desiredRot = Quaternion.LookRotation(lookAt - desiredPos);
                    break;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPos,
                                                     ref _velocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot,
                                                   10f * Time.deltaTime);
        }
    }
}
