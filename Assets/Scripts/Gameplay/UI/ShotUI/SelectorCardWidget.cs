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

        [Tooltip("Scale multiplier applied when this card is highlighted in hold-mode.")]
        [SerializeField] private float _highlightScale = 1.05f;

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

        /// <summary>
        /// Visually highlight (scale to <paramref name="targetScale"/>) or de-highlight (scale to 1).
        /// Called by SelectorOverlayWidget during hold-mode hover tracking.
        /// When called without a targetScale (e.g. from non-router path) falls back to _highlightScale.
        /// </summary>
        public void SetHighlight(bool on, float targetScale = -1f)
        {
            if (targetScale < 0f) targetScale = _highlightScale;
            float scale = on ? targetScale : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// Programmatically invoke the selection callback.
        /// Called by SelectorOverlayWidget.CommitHighlighted().
        /// </summary>
        public void InvokeSelection()
        {
            _onTap?.Invoke();
        }

        void WireButton()
        {
            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onTap?.Invoke());
        }
    }
}
