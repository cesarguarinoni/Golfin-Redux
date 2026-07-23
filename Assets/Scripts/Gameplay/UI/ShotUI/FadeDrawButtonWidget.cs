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
            // Cesar (iter-35): cap the label's auto-size MAX font at 23 so "STRAIGHT" fits the button.
            // The label is runtime-populated (not findable at edit time), so this is set in code.
            if (_primaryText != null)
            {
                _primaryText.enableAutoSizing = true;
                _primaryText.fontSizeMax = 23f;
                if (_primaryText.fontSizeMin <= 0f || _primaryText.fontSizeMin > 23f)
                    _primaryText.fontSizeMin = 8f;
            }

            if (ShotModeContext.Mode == ShotMode.Straight)
            {
                if (_iconImage   != null) _iconImage.sprite = _iconStraight;
                if (_primaryText != null) _primaryText.text = LocalizationManager.Get("GAMEPLAY_STRAIGHT");
            }
            else
            {
                if (_iconImage   != null) _iconImage.sprite = _iconFadeDraw;
                // "/" + line break kept as layout; only the FADE/DRAW words localize.
                if (_primaryText != null)
                    _primaryText.text = $"{LocalizationManager.Get("GAMEPLAY_FADE")}/\n{LocalizationManager.Get("GAMEPLAY_DRAW")}";
            }
            if (_secondaryText != null) _secondaryText.gameObject.SetActive(false);
        }

        protected override void OnClick() => ShotModeContext.Toggle();
    }
}
