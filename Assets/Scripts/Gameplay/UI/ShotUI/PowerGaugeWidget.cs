using UnityEngine;
using TMPro;
using UnityEngine.Serialization;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class PowerGaugeWidget : MonoBehaviour
    {
        /// <summary>Meters per yard — carry authority is in yards, the map target is in metres.</summary>
        private const float kYardsToMeters = 0.9144f;

        /// <summary>Overpower ceiling — matches ShotController's Clamp(power, 0, 1.2).</summary>
        public const float MarkerMaxFrac = 1.2f;

        /// <summary>Floor so a near-zero target still shows a visible notch instead of a 0° sliver.</summary>
        public const float MarkerMinFrac = 0.02f;

        /// <summary>Sentinel: no target mapped → no notch drawn.</summary>
        public const float MarkerNone = -1f;

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

        /// <summary>
        /// Full-swing carry authority, in yards. ClubContext.SelectedDistance is the same
        /// per-club value the club button, the HUD card and MapViewController.Open() use
        /// (Fix 1 lineage) — reading it live means the yards text and the marker follow a
        /// club change with no extra push. Falls back to the injected _maxCarryYards when the
        /// bus is unpopulated (pure-lab runs with no bag), which is why PhysicsLabController
        /// still seeds it on exiting putter mode.
        /// </summary>
        private float ResolveCarryYards()
        {
            int selected = ClubContext.SelectedDistance;
            return selected > 0 ? selected : _maxCarryYards;
        }

        /// <summary>
        /// PURE SEAM (EditMode-tested). Marker position as a fraction of club carry.
        /// Returns <see cref="MarkerNone"/> when there is no target or no usable carry.
        /// A target beyond overpower reach pins at <see cref="MarkerMaxFrac"/> and reports
        /// <paramref name="unreachable"/> = true.
        /// </summary>
        /// <param name="targetCarryM">ShotController.MapTargetCarryM — metres, negative = none.</param>
        /// <param name="clubCarryYards">Club carry at 100% power, yards.</param>
        public static float ComputeMarkerFrac(float targetCarryM, float clubCarryYards, out bool unreachable)
        {
            unreachable = false;
            if (targetCarryM <= 0f || clubCarryYards <= 0f) return MarkerNone;

            float clubCarryM = clubCarryYards * kYardsToMeters;
            float frac       = targetCarryM / clubCarryM;
            unreachable      = frac > MarkerMaxFrac;
            return Mathf.Clamp(frac, MarkerMinFrac, MarkerMaxFrac);
        }

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
            // Visible only while the shot is being set up. Hidden at Idle (nothing to show yet)
            // and hidden again from flick-commit onward — the gauge is a control readout, not a
            // flight HUD (Cesar, 2026-08-06).
            bool show = state.State is ShotState.Aiming or ShotState.Pulling or ShotState.Timing;
            _group.alpha          = show ? 1f : 0f;
            _group.blocksRaycasts = show;
            if (!show) return;

            if (_gauge != null)
            {
                _gauge.Progress01 = state.PowerNormalized;
                UpdateMarker();
            }

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
                    distance = ResolveCarryYards() * state.PowerNormalized;
                    suffix   = "yd";
                }
                _distanceText.text = $"{distance:F1} {suffix}";
            }
        }

        /// <summary>
        /// Push the map-target notch to the gauge. Full-swing (Yards) ONLY — in putter/Meters
        /// mode the marker is forced off: the map view is not the putter targeting tool
        /// (the green grid is).
        /// </summary>
        private void UpdateMarker()
        {
            if (_unitMode != DistanceUnit.Yards || _shotController == null)
            {
                _gauge.MarkerUnreachable = false;
                _gauge.MarkerFrac01      = MarkerNone;
                return;
            }

            float frac = ComputeMarkerFrac(_shotController.MapTargetCarryM, ResolveCarryYards(),
                                           out bool unreachable);
            _gauge.MarkerUnreachable = unreachable;   // set first: the frac setter is what repaints
            _gauge.MarkerFrac01      = frac;
        }
    }
}
