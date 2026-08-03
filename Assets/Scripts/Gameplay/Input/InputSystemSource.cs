using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Golfin.Gameplay.Input
{
    public class InputSystemSource : MonoBehaviour, IShotInputSource
    {
        [SerializeField] private InputActionAsset _actionAsset;

        private InputAction _touchPositionAction;
        private InputAction _touchPressAction;

        // Velocity smoothing ring buffer (last N per-frame samples)
        private const int VelBufSize = 5;
        private readonly Vector2[] _velBuf = new Vector2[VelBufSize];
        private int     _velHead;
        private Vector2 _prevPosition;

        // IShotInputSource state
        private bool    _isTouching;
        private Vector2 _origin;
        private Vector2 _currentPosition;
        private Vector2 _smoothedVelocity;

        public bool    IsTouching            => _isTouching;
        public Vector2 TouchPositionPx       => _currentPosition;
        public Vector2 TouchOriginPx         => _origin;
        public Vector2 TouchVelocityPxPerSec => _smoothedVelocity;

        private void Awake()
        {
            var map              = _actionAsset.FindActionMap("Shot", throwIfNotFound: true);
            _touchPositionAction = map.FindAction("Touch",      throwIfNotFound: true);
            _touchPressAction    = map.FindAction("TouchPress", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _touchPositionAction.Enable();
            _touchPressAction.Enable();
            _touchPressAction.started  += HandlePressStarted;
            _touchPressAction.canceled += HandlePressCanceled;
        }

        private void OnDisable()
        {
            _touchPressAction.started  -= HandlePressStarted;
            _touchPressAction.canceled -= HandlePressCanceled;
            _touchPositionAction.Disable();
            _touchPressAction.Disable();
        }

        private void Update()
        {
            var pos = _touchPositionAction.ReadValue<Vector2>();

            if (_isTouching && Time.deltaTime > 0f)
            {
                var rawVel = (pos - _prevPosition) / Time.deltaTime;
                _velBuf[_velHead % VelBufSize] = rawVel;
                _velHead++;
                _smoothedVelocity = AverageBuffer(_velBuf);
            }

            _currentPosition = pos;
            _prevPosition    = pos;
        }

        private void HandlePressStarted(InputAction.CallbackContext _)
        {
            _isTouching = true;

            // Sample the LIVE position at press time — do NOT reuse _currentPosition,
            // which is only written in Update() and therefore holds the previous frame's
            // value. With <Mouse>/position that is harmless (the cursor streams while
            // unpressed, so the cached value is already correct). With
            // <Touchscreen>/primaryTouch/position there is no position before the finger
            // lands — the control holds the last-released touch's position (or 0,0 at
            // launch) — so the cached value is stale and every current-origin delta would
            // carry a spurious offset. The press control and the position control update
            // in the same input event, so ReadValue here returns the true landing point.
            var pos = _touchPositionAction.ReadValue<Vector2>();
            _origin          = pos;
            _currentPosition = pos;   // keep same-frame TouchPositionPx consumers correct (zero delta at press)
            _prevPosition    = pos;   // avoid a spurious velocity spike on the first Update() after press

            Array.Clear(_velBuf, 0, VelBufSize);
            _velHead          = 0;
            _smoothedVelocity = Vector2.zero;
        }

        private void HandlePressCanceled(InputAction.CallbackContext _) => _isTouching = false;

        private static Vector2 AverageBuffer(Vector2[] buf)
        {
            var sum = Vector2.zero;
            foreach (var v in buf) sum += v;
            return sum / buf.Length;
        }
    }
}
