using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Drives CanvasGroup.alpha on the cone root based on ShotState.
    // Lerps at ConeFadeInSeconds / ConeFadeOutSeconds rates from ControlsConfig.
    public class ConeAlphaController : MonoBehaviour
    {
        [SerializeField] private ShotController _shotController;
        [SerializeField] private CanvasGroup    _canvasGroup;

        private ControlsConfig _cfg;
        private float          _targetAlpha;

        private void Awake()
        {
            _cfg         = ControlsConfig.Default;
            _targetAlpha = _cfg.ConeIdleAlpha;
        }

        private void Start() => _canvasGroup.alpha = _cfg.ConeIdleAlpha;

        private void OnEnable()
        {
            if (_shotController != null)
                _shotController.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_shotController != null)
                _shotController.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(ShotInputState state)
        {
            _targetAlpha = state.State switch
            {
                ShotState.Idle      => _cfg.ConeIdleAlpha,
                ShotState.Aiming    => 1f,
                ShotState.Pulling   => 1f,
                ShotState.Timing    => 1f,
                ShotState.Flicking  => 1f,
                ShotState.Resolving => 0f,
                _                   => _cfg.ConeIdleAlpha,
            };
        }

        private void Update()
        {
            if (Mathf.Approximately(_canvasGroup.alpha, _targetAlpha)) return;

            float rate = _targetAlpha > _canvasGroup.alpha
                ? 1f / Mathf.Max(_cfg.ConeFadeInSeconds,  0.001f)
                : 1f / Mathf.Max(_cfg.ConeFadeOutSeconds, 0.001f);

            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha, _targetAlpha, rate * Time.deltaTime);
        }
    }
}
