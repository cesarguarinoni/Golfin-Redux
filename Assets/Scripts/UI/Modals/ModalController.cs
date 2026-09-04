using Golfin.UI.Polish;
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

        // ── gps_polish §D5 — opt-in pop-in ───────────────────────────────────
        [Tooltip("gps_polish §D5. When true, Show() pops the panel in (scale 0.9 -> 1 with an " +
                 "independent alpha) and fades the backdrop, and Hide() reverses it. DEFAULT FALSE, " +
                 "and that default is the point: every modal in the game inherits this class, and a " +
                 "default of true would have put new motion on the level-up modal, the shop, the " +
                 "tournament gates and the versus result in a task whose whole scope is the GPS " +
                 "surface. Set by GpsPolishBuilder on the three GPS modals and nowhere else.")]
        [SerializeField] private bool animateShow = false;

        private CanvasGroup _canvasGroup;
        private CanvasGroup _backdropGroup;
        private Coroutine _panelMotion;
        private Coroutine _backdropMotion;
        private bool _isVisible = false;

        /// <summary>Whether this modal pops in (gps_polish §D5). Read by tests, which pin the
        /// default at false for every non-GPS modal prefab.</summary>
        public bool AnimatesShow => animateShow;

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
            if (modalPanel != null && (useAnimation || animateShow))
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

            // Guarantee the two things a modal owes the player before anything is shown: the UI
            // behind it is darkened, and it cannot be tapped. SetAsLastSibling above only wins
            // against siblings in the SAME canvas — the persistent top bar / bottom nav live on
            // their own root canvas and used to paint straight over the scrim. See ModalScrim.
            backdrop = ModalScrim.Apply(transform, backdrop, modalPanel);

            // Show backdrop
            if (backdrop != null)
            {
                backdrop.SetActive(true);
            }
            
            // Show modal panel
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);

                if (animateShow)
                {
                    // gps_polish §D5 — pop in. The panel scale and the backdrop alpha are
                    // independent on purpose: the scrim arriving at full speed under a panel that
                    // is still growing is what makes the pop read as "on top of" rather than
                    // "instead of". Both are UiMotion routines, so both settle on their final
                    // value if this modal is closed or force-disabled mid-animation.
                    var panelRect = modalPanel.transform as RectTransform;
                    UiMotion.Run(this, ref _panelMotion, UiMotion.Pop(panelRect, _canvasGroup));
                    var bg = EnsureBackdropGroup();
                    if (bg != null)
                        UiMotion.Run(this, ref _backdropMotion, UiMotion.Fade(bg, 0f, 1f));
                }
                else if (useAnimation && _canvasGroup != null)
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
            if (animateShow && modalPanel != null)
            {
                // gps_polish §D5 — the reverse. HideImmediate is chained off the LONGER of the two
                // routines rather than called here, so the panel is never deactivated out from
                // under its own shrink.
                var panelRect = modalPanel.transform as RectTransform;
                var bg = EnsureBackdropGroup();
                if (bg != null) UiMotion.Run(this, ref _backdropMotion, UiMotion.Fade(bg, bg.alpha, 0f));
                UiMotion.Run(this, ref _panelMotion,
                    UiMotion.Then(UiMotion.Unpop(panelRect, _canvasGroup), HideImmediate));
            }
            else if (useAnimation && _canvasGroup != null)
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
        /// The backdrop's CanvasGroup, created on demand.
        ///
        /// <para>Resolved LAZILY rather than in Awake because the backdrop reference is not stable
        /// across a Show: <see cref="ModalScrim.Apply"/> may CREATE the scrim (a modal authored
        /// without one gets a full-screen scrim built for it) and reassign <see cref="backdrop"/>
        /// every time. A group cached in Awake would belong to an object that no longer exists.</para>
        /// </summary>
        private CanvasGroup EnsureBackdropGroup()
        {
            if (backdrop == null) return null;
            if (_backdropGroup != null && _backdropGroup.gameObject == backdrop) return _backdropGroup;
            _backdropGroup = backdrop.GetComponent<CanvasGroup>();
            if (_backdropGroup == null) _backdropGroup = backdrop.AddComponent<CanvasGroup>();
            return _backdropGroup;
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
