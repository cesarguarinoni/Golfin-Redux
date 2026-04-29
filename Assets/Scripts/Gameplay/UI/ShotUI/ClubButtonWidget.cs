using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class ClubButtonWidget : ActionButtonWidget
    {
        [SerializeField] private SelectorOverlayWidget _selectorOverlay;
        [SerializeField] private Sprite _defaultPortrait;

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
                _secondaryText.text = $"{ClubContext.SelectedDistance}<size=20><b> yrds</b></size>";
            }
        }

        protected override void OnClick()
        {
            if (_selectorOverlay != null) _selectorOverlay.Open(SelectorOverlayWidget.Kind.Club);
        }
    }
}
