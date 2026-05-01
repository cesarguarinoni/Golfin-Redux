using UnityEngine;
using TMPro;
using UnityEngine.Serialization;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class PowerGaugeWidget : MonoBehaviour
    {
        public enum DistanceUnit { Yards, Meters }

        [SerializeField] private ShotController    _shotController;
        [SerializeField] private PowerGaugeGraphic _gauge;
        [SerializeField] private TMP_Text          _pctText;

        [FormerlySerializedAs("_yardsText")]
        [SerializeField] private TMP_Text          _distanceText;

        [SerializeField] private DistanceUnit _unitMode           = DistanceUnit.Yards;
        [SerializeField] private float        _maxPuttRangeMeters = 25f;

        private CanvasGroup _group;
        private float       _maxCarryYards = 250f;

        public void SetMaxCarryYards(float yards)              => _maxCarryYards         = yards;
        public void SetUnitMode(DistanceUnit u)                => _unitMode              = u;
        public void SetMaxPuttRangeMeters(float m)             => _maxPuttRangeMeters    = m;

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

            int   pct = Mathf.RoundToInt(state.PowerNormalized * 100f);
            if (_pctText != null) _pctText.text = $"{pct}%";

            if (_distanceText != null)
            {
                float distance;
                string suffix;
                if (_unitMode == DistanceUnit.Meters)
                {
                    distance = _maxPuttRangeMeters * state.PowerNormalized;
                    suffix   = "mts";
                }
                else
                {
                    distance = _maxCarryYards * state.PowerNormalized;
                    suffix   = "yd";
                }
                _distanceText.text = $"{distance:F1} {suffix}";
            }
        }
    }
}
