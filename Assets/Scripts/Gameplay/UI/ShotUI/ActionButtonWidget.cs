using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Gameplay.UI.ShotUI
{
    public abstract class ActionButtonWidget : MonoBehaviour
    {
        [SerializeField] protected Button   _button;
        [SerializeField] protected Image    _iconImage;
        [SerializeField] protected TMP_Text _primaryText;
        [SerializeField] protected TMP_Text _secondaryText;

        protected virtual void OnEnable()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
                _button.onClick.AddListener(TeeIdleGlowController.NotifyOtherInteraction);
            }
            Refresh();
        }

        protected virtual void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
                _button.onClick.RemoveListener(TeeIdleGlowController.NotifyOtherInteraction);
            }
        }

        protected abstract void Refresh();
        protected abstract void OnClick();
    }
}
