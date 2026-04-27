using UnityEngine;
using TMPro;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class PowerGaugeWidget : MonoBehaviour
    {
        [SerializeField] private ShotController    _shotController;
        [SerializeField] private PowerGaugeGraphic _gauge;
        [SerializeField] private TMP_Text          _pctText;
        [SerializeField] private TMP_Text          _yardsText;

        private CanvasGroup _group;
        private float       _maxCarryYards = 250f;

        public void SetMaxCarryYards(float yards) => _maxCarryYards = yards;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha          = 0f;
            _group.blocksRaycasts = false;
        }

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
            bool show = state.State != ShotState.Idle;
            _group.alpha          = show ? 1f : 0f;
            _group.blocksRaycasts = show;
            if (!show) return;

            if (_gauge != null)
                _gauge.Progress01 = state.PowerNormalized;

            int   pct   = Mathf.RoundToInt(state.PowerNormalized * 100f);
            float yards = _maxCarryYards * state.PowerNormalized;

            if (_pctText   != null) _pctText.text   = $"{pct}%";
            if (_yardsText != null) _yardsText.text  = $"{yards:F1} yd";
        }
    }
}
