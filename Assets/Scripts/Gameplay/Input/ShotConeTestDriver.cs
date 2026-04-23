using UnityEngine;

namespace Golfin.Gameplay.Input
{
    // Drives ShotController to Pulling state automatically in test scenes.
    // Attach alongside ShotController. Injects DebugShotInputSource and simulates
    // a 170 px pull (≈50 % power) after one frame of idle.
    public class ShotConeTestDriver : MonoBehaviour
    {
        [SerializeField] private ShotController      _controller;
        [SerializeField] private DebugShotInputSource _debugInput;

        // Screen-space origin of the simulated touch (center of 1080×1920 canvas).
        private static readonly Vector2 Origin = new Vector2(540f, 400f);
        private const float PullPx = 170f;

        // Delay before driving, so Idle screenshots can be taken first.
        [SerializeField] private int _delayFrames = 300;

        private int _frame;

        private void Start()
        {
            _controller.InjectInputSource(_debugInput);
            _debugInput.TouchOriginPx   = Origin;
            _debugInput.TouchPositionPx = Origin;
            _debugInput.IsTouching      = false;
        }

        private void Update()
        {
            _frame++;
            int f = _frame - _delayFrames;
            switch (f)
            {
                case 2:
                    // Touch begins → Idle → Aiming
                    _debugInput.IsTouching      = true;
                    _debugInput.TouchPositionPx = Origin;
                    break;
                case 3:
                    // Pull 170 px → Aiming → Pulling
                    _debugInput.TouchPositionPx = new Vector2(Origin.x, Origin.y - PullPx);
                    break;
                // f 4+ holds Pulling state; driver stops changing values
            }
        }
    }
}
