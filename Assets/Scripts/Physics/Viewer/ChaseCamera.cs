using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Camera controller with six modes for the physics lab.
    /// Attach to the scene's Main Camera.
    ///
    /// Modes added in §2b:
    ///   Downrange  — static position past landing zone, looks back along flight line.
    ///   CupZoom    — tweens from current position to hover above cup (L11: flat circle, hover only).
    ///   OBFreeze   — camera frozen at OB-crossing pivot, rotation tracks ball.
    /// </summary>
    public class ChaseCamera : MonoBehaviour, IModeSetter
    {
        public enum Mode { Chase, Overhead, GroundLevel, Downrange, CupZoom, OBFreeze }

        [SerializeField] Mode  startMode = Mode.Chase;
        [SerializeField] float smoothTime = 0.08f;

        [Header("Chase framing — §controls_h R1")]
        [Tooltip("XZ distance behind ball in Chase mode (metres). Smaller = ball appears larger in frame.")]
        [SerializeField] float _followDistance = 3f;
        [Tooltip("Height above ball in Chase mode (metres).")]
        [SerializeField] float _followHeight = 1.8f;

        Mode      _mode;
        Transform _target;
        Vector3   _shotOrigin;
        Vector3   _launchDir;    // normalized XZ
        Vector3   _velocity;     // for SmoothDamp

        // ── §2b: Downrange state ───────────────────────────────────────────────
        Vector3 _downrangePos;
        Vector3 _downrangeLookAt;

        // ── §2b: CupZoom state ────────────────────────────────────────────────
        Vector3 _cupZoomFocus;
        float   _cupZoomStartTime;
        Vector3 _cupZoomStartPos;

        // ── §2b: OBFreeze state ───────────────────────────────────────────────
        Vector3 _obFreezePivot;

        void Awake() => _mode = startMode;

        // ── Public API ─────────────────────────────────────────────────────────

        public Mode  CurrentMode       => _mode;
        public float FollowHeightOffset { get; set; } = 0f;
        /// <summary>§controls_h R1: serialized follow params, exposed for tests.</summary>
        public float FollowDistance => _followDistance;
        public float FollowHeight   => _followHeight;

        public void SetMode(Mode m)
        {
            // CupZoom: capture entry time and start position for tween.
            if (m == Mode.CupZoom && _mode != Mode.CupZoom)
            {
                _cupZoomStartTime = Time.time;
                _cupZoomStartPos  = transform.position;
            }
            _mode = m;
        }

        public void SetTarget(Transform t) => _target = t;

        public void ResetToOrigin(Vector3 origin, Vector3 launchDir)
        {
            _shotOrigin = origin;
            _launchDir  = new Vector3(launchDir.x, 0f, launchDir.z).normalized;
            if (_launchDir == Vector3.zero) _launchDir = Vector3.forward;
            _velocity   = Vector3.zero;
        }

        // ── §2b: New public API ────────────────────────────────────────────────

        public void SetDownrangeFraming(Vector3 pos, Vector3 lookAt)
        {
            _downrangePos    = pos;
            _downrangeLookAt = lookAt;
        }

        public void SetCupZoomFocus(Vector3 focus) => _cupZoomFocus = focus;

        public void SetOBFreezePivot(Vector3 pivot) => _obFreezePivot = pivot;

        /// <summary>
        /// Extracted from LateUpdate for EditMode test access (same as TickCinematicCut pattern).
        /// SendMessage("LateUpdate") triggers a ShouldRunBehaviour assertion in EditMode;
        /// this overload accepts an explicit deltaTime so tests can drive convergence.
        /// Production path calls FrameCamera(Time.deltaTime).
        /// </summary>
        internal void FrameCamera(float dt) => RunLateUpdateLogic(dt);

        void LateUpdate() => RunLateUpdateLogic(Time.deltaTime);

        void RunLateUpdateLogic(float dt)
        {
            // Pre-§2b behavior: when no target and in Chase mode, do nothing.
            // PhysicsLabController.ApplyCameraYaw owns the camera position during Aiming
            // (when Director has cleared the target on AtRest/InCup/OB).
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

                case Mode.Downrange:
                    desiredPos = _downrangePos;
                    desiredRot = Quaternion.LookRotation(_downrangeLookAt - desiredPos);
                    break;

                case Mode.CupZoom:
                {
                    // Tween from start position to hover above cup over 1 second. L11: hover, don't dive.
                    float t        = Mathf.Clamp01((Time.time - _cupZoomStartTime) / 1.0f);
                    Vector3 hoverPos = _cupZoomFocus + Vector3.up * 2.5f;
                    desiredPos     = Vector3.Lerp(_cupZoomStartPos, hoverPos, EaseOutCubic(t));
                    Vector3 lookDir = _cupZoomFocus - desiredPos;
                    desiredRot     = lookDir != Vector3.zero
                        ? Quaternion.LookRotation(lookDir)
                        : transform.rotation;
                    break;
                }

                case Mode.OBFreeze:
                    // Position frozen at pivot; rotation tracks ball.
                    desiredPos = _obFreezePivot;
                    Vector3 toBall = focus - _obFreezePivot;
                    desiredRot = toBall != Vector3.zero
                        ? Quaternion.LookRotation(toBall)
                        : transform.rotation;
                    break;

                default: // Chase
                    desiredPos = focus - _launchDir * _followDistance + Vector3.up * (_followHeight + FollowHeightOffset);
                    desiredRot = Quaternion.LookRotation(focus - desiredPos);
                    break;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPos,
                                                     ref _velocity, smoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot,
                                                   10f * dt);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    }
}
