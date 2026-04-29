using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SelectorCardWidget : MonoBehaviour
    {
        [SerializeField] private Button   _button;
        [SerializeField] private Image    _icon;
        [SerializeField] private TMP_Text _primaryText;
        [SerializeField] private TMP_Text _secondaryText;

        Action _onTap;

        public void SetClub(ClubEntry e, Action onTap)
        {
            _onTap = onTap;
            if (_icon != null) _icon.sprite = e.Portrait;
            if (_primaryText != null) _primaryText.text = e.TypeLabel;
            if (_secondaryText != null)
            {
                _secondaryText.richText = true;
                _secondaryText.text = $"{e.Distance}<size=20><b> yrds</b></size>";
            }
            WireButton();
        }

        public void SetBall(BallEntry e, Action onTap)
        {
            _onTap = onTap;
            if (_icon != null) _icon.sprite = e.Thumbnail;
            if (_primaryText != null) _primaryText.text = e.NameLabel;
            if (_secondaryText != null)
            {
                _secondaryText.richText = false;
                _secondaryText.text = e.QuantityDisplay;
            }
            WireButton();
        }

        void WireButton()
        {
            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onTap?.Invoke());
        }
    }
}
