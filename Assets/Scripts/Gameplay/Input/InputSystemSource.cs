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
            _origin     = _currentPosition;
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
