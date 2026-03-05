using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.UI
{
    /// <summary>
    /// Individual settings menu item with expand/collapse behavior.
    /// Manages the accordion animation for submenu content.
    /// </summary>
    public class SettingsMenuItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private GameObject submenuContainer;
        [SerializeField] private RectTransform arrowIcon;
        
        [Header("Animation Settings")]
        [SerializeField] private float expandDuration = 0.3f;
        [SerializeField] private float collapseDuration = 0.2f;
        [SerializeField] private AnimationCurve expandCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        private bool _isExpanded = false;
        private float _targetHeight = 0f;
        private float _currentHeight = 0f;
        private float _animationProgress = 0f;
        private RectTransform _submenuRect;
        private LayoutElement _layoutElement;
        private float _baseRowHeight = 80f; // Base height of the row without submenu
        
        public bool IsExpanded => _isExpanded;
        public event System.Action<SettingsMenuItem> OnExpanded;
        public event System.Action<SettingsMenuItem> OnCollapsed;

        private void Awake()
        {
            // Get or add LayoutElement for dynamic height control
            _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement != null)
            {
                _baseRowHeight = _layoutElement.preferredHeight;
            }

            if (submenuContainer != null)
            {
                _submenuRect = submenuContainer.GetComponent<RectTransform>();
                if (_submenuRect != null)
                {
                    _targetHeight = _submenuRect.rect.height;
                    _currentHeight = 0f;
                    _submenuRect.sizeDelta = new Vector2(_submenuRect.sizeDelta.x, 0f);
                }
                submenuContainer.SetActive(false);
            }

            if (button != null)
            {
                button.onClick.AddListener(ToggleExpansion);
            }
        }

        /// <summary>
        /// Toggle the expansion state of this menu item.
        /// </summary>
        public void ToggleExpansion()
        {
            if (_isExpanded)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        /// <summary>
        /// Expand the submenu with animation.
        /// </summary>
        public void Expand()
        {
            if (_isExpanded) return;
            
            _isExpanded = true;
            _animationProgress = 0f;
            
            if (submenuContainer != null)
            {
                submenuContainer.SetActive(true);
            }
            
            OnExpanded?.Invoke(this);
            
            Debug.Log($"[SettingsMenuItem] Expanded: {gameObject.name}");
        }

        /// <summary>
        /// Collapse the submenu with animation.
        /// </summary>
        public void Collapse()
        {
            if (!_isExpanded) return;
            
            _isExpanded = false;
            _animationProgress = 0f;
            
            OnCollapsed?.Invoke(this);
            
            Debug.Log($"[SettingsMenuItem] Collapsed: {gameObject.name}");
        }

        private void Update()
        {
            if (_submenuRect == null) return;

            // Animate expansion/collapse
            float targetProgress = _isExpanded ? 1f : 0f;
            float duration = _isExpanded ? expandDuration : collapseDuration;
            
            if (!Mathf.Approximately(_animationProgress, targetProgress))
            {
                _animationProgress = Mathf.MoveTowards(_animationProgress, targetProgress, Time.deltaTime / duration);
                
                float curveValue = expandCurve.Evaluate(_animationProgress);
                _currentHeight = _targetHeight * curveValue;
                _submenuRect.sizeDelta = new Vector2(_submenuRect.sizeDelta.x, _currentHeight);
                
                // Update LayoutElement height to notify layout system
                if (_layoutElement != null)
                {
                    _layoutElement.preferredHeight = _baseRowHeight + _currentHeight;
                }
                
                // Rotate arrow
                if (arrowIcon != null)
                {
                    float rotation = Mathf.Lerp(0f, 90f, _animationProgress);
                    arrowIcon.localRotation = Quaternion.Euler(0f, 0f, -rotation);
                }
                
                // Hide submenu when fully collapsed
                if (_animationProgress == 0f && submenuContainer != null)
                {
                    submenuContainer.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Force collapse without animation (used when another item is opened).
        /// </summary>
        public void ForceCollapse()
        {
            _isExpanded = false;
            _animationProgress = 0f;
            _currentHeight = 0f;
            
            if (_submenuRect != null)
            {
                _submenuRect.sizeDelta = new Vector2(_submenuRect.sizeDelta.x, 0f);
            }
            
            // Reset LayoutElement height
            if (_layoutElement != null)
            {
                _layoutElement.preferredHeight = _baseRowHeight;
            }
            
            if (arrowIcon != null)
            {
                arrowIcon.localRotation = Quaternion.identity;
            }
            
            if (submenuContainer != null)
            {
                submenuContainer.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-find references if not assigned
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }
#endif
    }
}
