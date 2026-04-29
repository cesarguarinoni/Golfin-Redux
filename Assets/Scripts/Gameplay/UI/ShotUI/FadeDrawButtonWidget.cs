using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class FadeDrawButtonWidget : ActionButtonWidget
    {
        [SerializeField] private Sprite _iconStraight;
        [SerializeField] private Sprite _iconFadeDraw;

        protected override void OnEnable()
        {
            base.OnEnable();
            ShotModeContext.OnChanged += Refresh;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ShotModeContext.OnChanged -= Refresh;
        }

        protected override void Refresh()
        {
            if (ShotModeContext.Mode == ShotMode.Straight)
            {
                if (_iconImage   != null) _iconImage.sprite = _iconStraight;
                if (_primaryText != null) _primaryText.text = "STRAIGHT";
            }
            else
            {
                if (_iconImage   != null) _iconImage.sprite = _iconFadeDraw;
                if (_primaryText != null) _primaryText.text = "FADE/\nDRAW";
            }
            if (_secondaryText != null) _secondaryText.gameObject.SetActive(false);
        }

        protected override void OnClick() => ShotModeContext.Toggle();
    }
}
