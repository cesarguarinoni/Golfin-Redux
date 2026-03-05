using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// Base controller for modal dialogs.
    /// Handles show/hide animations and backdrop.
    /// </summary>
    public class ModalController : MonoBehaviour
    {
        [Header("Modal Components")]
        [Tooltip("The modal panel to show/hide")]
        public GameObject modalPanel;
        
        [Tooltip("Optional backdrop (dark overlay)")]
        public GameObject backdrop;
        
        [Tooltip("Close button (X or Cancel button)")]
        public Button closeButton;
        
        [Header("Animation")]
        [SerializeField] private bool useAnimation = true;
        [SerializeField] private float animationDuration = 0.2f;
        
        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;

        protected virtual void Awake()
        {
            // Setup canvas group for fade animation
            if (modalPanel != null && useAnimation)
            {
                _canvasGroup = modalPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = modalPanel.AddComponent<CanvasGroup>();
                }
            }
            
            // Wire close button
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
            
            // Start hidden
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
            
            if (backdrop != null)
            {
                backdrop.SetActive(false);
            }
        }

        /// <summary>
        /// Show the modal.
        /// </summary>
        public virtual void Show()
        {
            if (_isVisible) return;
            
            _isVisible = true;
            
            // Show backdrop
            if (backdrop != null)
            {
                backdrop.SetActive(true);
            }
            
            // Show modal panel
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
                
                // Fade in animation
                if (useAnimation && _canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    StartCoroutine(FadeIn());
                }
            }
            
            OnShow();
            
            Debug.Log($"[Modal] {gameObject.name} shown");
        }

        /// <summary>
        /// Hide the modal.
        /// </summary>
        public virtual void Hide()
        {
            if (!_isVisible) return;
            
            _isVisible = false;
            
            // Fade out animation
            if (useAnimation && _canvasGroup != null)
            {
                StartCoroutine(FadeOut());
            }
            else
            {
                HideImmediate();
            }
            
            OnHide();
            
            Debug.Log($"[Modal] {gameObject.name} hidden");
        }

        /// <summary>
        /// Immediately hide without animation.
        /// </summary>
        private void HideImmediate()
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
            
            if (backdrop != null)
            {
                backdrop.SetActive(false);
            }
        }

        /// <summary>
        /// Fade in animation.
        /// </summary>
        private System.Collections.IEnumerator FadeIn()
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / animationDuration);
                yield return null;
            }
            
            _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Fade out animation.
        /// </summary>
        private System.Collections.IEnumerator FadeOut()
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / animationDuration);
                yield return null;
            }
            
            _canvasGroup.alpha = 0f;
            HideImmediate();
        }

        /// <summary>
        /// Called when modal is shown (override for custom behavior).
        /// </summary>
        protected virtual void OnShow()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Called when modal is hidden (override for custom behavior).
        /// </summary>
        protected virtual void OnHide()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Check if modal is currently visible.
        /// </summary>
        public bool IsVisible()
        {
            return _isVisible;
        }
    }
}
