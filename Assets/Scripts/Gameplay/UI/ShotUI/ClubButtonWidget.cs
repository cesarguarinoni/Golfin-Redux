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
        // iter-35's _shootMode is GONE (map_view_v2 §8): the map no longer relabels this button to SHOOT.
        private bool _mapMode;   // map_view_v2: map open — button keeps its club content, only the router is off

        public void SetUnitMode(DistanceUnit mode) { _unitMode = mode; Refresh(); }

        /// <summary>
        /// map_view_v2 §8 — the map no longer relabels this button. B1 keeps the club button showing its
        /// real content ("DRIVER / 215 yd") while the map is open and puts the close/return control in the
        /// new bottom-LEFT SHOT VIEW button, so the player never loses sight of which club is loaded.
        ///
        /// All that is left of the retired iter-35 SetShootMode is the half that still matters: the
        /// SelectorDragRouter (a pointer handler, independent of onClick) must be off while the map owns
        /// the button, or a tap opens Club Selection on top of the map instead of closing it.
        /// MapViewController.RepurposeShootButton rebinds onClick separately.
        /// </summary>
        public void SetMapMode(bool on)
        {
            var router = GetComponent<SelectorDragRouter>();
            if (router != null) router.enabled = !on;
            _mapMode = on;
            if (!on) Refresh();
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
            if (_mapMode) return; // map view: this button only closes the map, never opens club selection
            // If SelectorDragRouter is present, it owns the open/close lifecycle.
            // The Button.onClick fires after OnPointerUp, so the router has already handled
            // the interaction. Suppress legacy direct open to avoid double-open.
            if (GetComponent<SelectorDragRouter>() != null) return;
            if (_selectorOverlay != null) _selectorOverlay.Open(SelectorOverlayWidget.Kind.Club);
        }
    }
}
