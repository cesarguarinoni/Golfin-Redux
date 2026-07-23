using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class ClubButtonWidget : ActionButtonWidget
    {
        public enum DistanceUnit { Yards, Meters }

        [SerializeField] private SelectorOverlayWidget _selectorOverlay;
        [SerializeField] private Sprite _defaultPortrait;

        private DistanceUnit _unitMode = DistanceUnit.Yards;
        private bool _shootMode; // iter-35: map view repurposes this button to "SHOOT" (close) — hide club name/yards

        public void SetUnitMode(DistanceUnit mode) { _unitMode = mode; Refresh(); }

        /// <summary>
        /// iter-35: map view turns this club button into the "SHOOT"/close control. While in shoot mode
        /// the club label reads "SHOOT", the yards secondary is hidden, and ClubContext-driven Refresh is
        /// suppressed (so a club/bag change can't repaint the label or re-show the yards). MapViewController
        /// calls this from RepurposeShootButton.
        /// </summary>
        public void SetShootMode(bool on)
        {
            _shootMode = on;
            // iter-38 (Cesar): the SHOOT button was OPENING Club Selection — the DriverButton's
            // SelectorDragRouter (pointer handler) opens the club selector on tap, independently of
            // onClick. Disable it while in shoot mode so the button ONLY closes the map; re-enable on exit.
            var router = GetComponent<SelectorDragRouter>();
            if (router != null) router.enabled = !on;
            if (on)
            {
                if (_primaryText   != null) _primaryText.text = LocalizationManager.Get("GAMEPLAY_SHOOT");
                if (_secondaryText != null) _secondaryText.gameObject.SetActive(false);
            }
            else
            {
                if (_secondaryText != null) _secondaryText.gameObject.SetActive(true);
                Refresh();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ClubContext.OnSelectedChanged += Refresh;
            ClubContext.OnBagChanged      += Refresh;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClubContext.OnSelectedChanged -= Refresh;
            ClubContext.OnBagChanged      -= Refresh;
        }

        protected override void Refresh()
        {
            if (_shootMode) return; // map view owns the label + hides the yards while in SHOOT mode
            if (_iconImage != null)
                _iconImage.sprite = ClubContext.SelectedPortrait != null
                    ? ClubContext.SelectedPortrait
                    : _defaultPortrait;
            if (_primaryText != null) _primaryText.text = ClubContext.SelectedTypeLabel;
            if (_secondaryText != null)
            {
                _secondaryText.richText = true;
                if (_unitMode == DistanceUnit.Meters)
                {
                    int mts = Mathf.RoundToInt(ClubContext.SelectedDistance * 0.9144f);
                    _secondaryText.text = $"{mts}<size=20><b> mts</b></size>";
                }
                else
                {
                    _secondaryText.text = $"{ClubContext.SelectedDistance}<size=20><b> yrds</b></size>";
                }
            }
        }

        protected override void OnClick()
        {
            if (_shootMode) return; // map view: this button only closes the map, never opens club selection
            // If SelectorDragRouter is present, it owns the open/close lifecycle.
            // The Button.onClick fires after OnPointerUp, so the router has already handled
            // the interaction. Suppress legacy direct open to avoid double-open.
            if (GetComponent<SelectorDragRouter>() != null) return;
            if (_selectorOverlay != null) _selectorOverlay.Open(SelectorOverlayWidget.Kind.Club);
        }
    }
}
