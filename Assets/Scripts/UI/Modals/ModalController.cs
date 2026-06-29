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

        // ── S2 — global modal stack tracking ─────────────────────────────────
        /// <summary>
        /// Count of currently-visible ModalController instances across the session.
        /// Incremented on Show(), decremented on Hide() and OnDisable() (leak guard).
        /// </summary>
        public static int OpenModalCount { get; private set; }

        /// <summary>
        /// Fired when OpenModalCount transitions from 1 → 0 (all modals closed).
        /// TournamentResultPresenter subscribes to retry pending presentation after 1.0s.
        /// </summary>
        public static event System.Action? ModalStackEmptied;

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
            OpenModalCount++; // S2 — track open modal count

            // R2-5 fix: move the modal to the front of its parent's sibling list so
            // it renders on top of all other screens (e.g. ModeSelectionScreen).
            // In Unity UI, the last sibling in a Canvas renders on top. Without this,
            // a screen that is a later sibling than the modal root will paint over the
            // backdrop, making the modal appear to open over an empty/opaque background.
            transform.SetAsLastSibling();

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

            // S2 — decrement at fade-start (not fade-end); the presenter's 1.0s settle
            // delay comfortably absorbs the 0.2s fade-out.
            OpenModalCount = Mathf.Max(0, OpenModalCount - 1);
            if (OpenModalCount == 0) ModalStackEmptied?.Invoke();

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
                // Use unscaledDeltaTime so the fade works even when timeScale=0
                // (e.g. during pause or after transition animations that halt the clock).
                elapsedTime += Time.unscaledDeltaTime;
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
                elapsedTime += Time.unscaledDeltaTime;
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
        /// S2 leak guard: if this modal is force-deactivated before Hide() completes,
        /// decrement OpenModalCount so the count doesn't strand at 1+.
        /// The _isVisible guard prevents double-decrement.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (_isVisible)
            {
                _isVisible = false;
                OpenModalCount = Mathf.Max(0, OpenModalCount - 1);
                if (OpenModalCount == 0) ModalStackEmptied?.Invoke();
                Debug.Log($"[Modal] {gameObject.name} force-disabled while visible; OpenModalCount={OpenModalCount}");
            }
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
