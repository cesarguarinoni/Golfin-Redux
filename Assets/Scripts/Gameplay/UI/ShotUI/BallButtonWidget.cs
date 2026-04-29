using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class BallButtonWidget : ActionButtonWidget
    {
        [SerializeField] private SelectorOverlayWidget _selectorOverlay;
        [SerializeField] private Sprite _defaultThumbnail;

        protected override void OnEnable()
        {
            base.OnEnable();
            BallContext.OnSelectedChanged += Refresh;
            BallContext.OnBagChanged      += Refresh;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            BallContext.OnSelectedChanged -= Refresh;
            BallContext.OnBagChanged      -= Refresh;
        }

        protected override void Refresh()
        {
            if (_iconImage != null)
            {
                var sprite = BallContext.SelectedThumbnail
                    ?? _defaultThumbnail
                    ?? Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN");
                _iconImage.sprite = sprite;
            }
            if (_primaryText   != null) _primaryText.text   = BallContext.SelectedNameLabel;
            if (_secondaryText != null) _secondaryText.text = BallContext.SelectedQuantityDisplay;
        }

        protected override void OnClick()
        {
            if (_selectorOverlay != null) _selectorOverlay.Open(SelectorOverlayWidget.Kind.Ball);
        }
    }
}
