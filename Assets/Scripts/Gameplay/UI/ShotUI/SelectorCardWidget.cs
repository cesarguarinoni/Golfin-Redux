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

        [Tooltip("CanvasGroup alpha applied when the card is gated out (K11 green gate).")]
        [SerializeField] private float _disabledAlpha = 0.5f;

        Action _onTap;

        CanvasGroup _canvasGroup;
        bool        _selectable = true;

        /// <summary>False when this card is gated out and must not commit (K11 green gate).</summary>
        public bool IsSelectable => _selectable;

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
        /// Grey out and fully disarm this card (K11 green gate). Mirrors the ball-selector
        /// putter-mode precedent in PhysicsLabController.EnterPutterMode — alpha 0.5,
        /// interactable=false, blocksRaycasts=false. The CanvasGroup is added on demand:
        /// cards are runtime clones, so no prefab is dirtied.
        /// </summary>
        public void SetSelectable(bool selectable)
        {
            _selectable = selectable;
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _canvasGroup.alpha          = selectable ? 1f : _disabledAlpha;
            _canvasGroup.interactable   = selectable;
            _canvasGroup.blocksRaycasts = selectable;
        }

        /// <summary>
        /// Programmatically invoke the selection callback.
        /// Called by SelectorOverlayWidget.CommitHighlighted().
        /// </summary>
        public void InvokeSelection()
        {
            if (!_selectable) return;   // K11: gated-out cards commit nothing, on any path
            _onTap?.Invoke();
        }

        void WireButton()
        {
            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => { if (_selectable) _onTap?.Invoke(); });
        }
    }
}
