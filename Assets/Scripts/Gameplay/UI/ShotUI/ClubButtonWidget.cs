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

        public void SetUnitMode(DistanceUnit mode) { _unitMode = mode; Refresh(); }

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
            // If SelectorDragRouter is present, it owns the open/close lifecycle.
            // The Button.onClick fires after OnPointerUp, so the router has already handled
            // the interaction. Suppress legacy direct open to avoid double-open.
            if (GetComponent<SelectorDragRouter>() != null) return;
            if (_selectorOverlay != null) _selectorOverlay.Open(SelectorOverlayWidget.Kind.Club);
        }
    }
}
