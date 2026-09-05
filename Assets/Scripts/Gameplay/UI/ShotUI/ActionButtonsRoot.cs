using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class ActionButtonsRoot : MonoBehaviour
    {
        [SerializeField] private ShotController _shotController;
        [SerializeField] private CanvasGroup    _group;

        [Tooltip("The FADE/DRAW In-Game Select Button in this row. Free Swing hides it — the " +
                 "upstroke's own path shapes the shot in that scheme, so a toggle would be a " +
                 "second, contradicting way to ask for the same thing (scheme_freeswing §3.1).")]
        [SerializeField] private FadeDrawButtonWidget _fadeDrawButton;

        /// <summary>Its own CanvasGroup, resolved lazily and added if the prefab has none.</summary>
        private CanvasGroup _fadeDrawGroup;

        /// <summary>Latched, so the hide survives a re-enable of this root — a scheme swap and a
        /// screen transition can both cycle it, and a button that came back visible under Free
        /// Swing would be tappable again.</summary>
        private bool _fadeDrawVisible = true;

        void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            if (_shotController != null) _shotController.OnStateChanged += Handle;
            ApplyFadeDrawVisibility();
        }

        void OnDisable()
        {
            if (_shotController != null) _shotController.OnStateChanged -= Handle;
        }

        void Handle(ShotInputState s)
        {
            bool idle = s.State == ShotState.Idle;
            _group.interactable   = idle;
            _group.blocksRaycasts = idle;
            // The root's group has just re-opened raycasts for the whole row; the hidden button's
            // OWN group has to close them again or the invisible FADE/DRAW would still be tappable
            // between shots, which is exactly when the player's thumb is near it.
            ApplyFadeDrawVisibility();
        }

        /// <summary>
        /// Show or hide the FADE/DRAW button, by OPACITY rather than by <c>SetActive</c>.
        ///
        /// <para>The row is a layout group: deactivating the object makes the group re-centre
        /// what is left, which slides the SPIN button sideways the moment the player picks Free
        /// Swing. Alpha 0 plus <c>blocksRaycasts = false</c> takes the button out of both the
        /// picture and the raycast while leaving its rect exactly where it was — the same lesson
        /// the Figma frames encode, where the hidden toggle's neighbours have not moved. The
        /// acceptance run measures SPIN's x to prove it.</para>
        /// </summary>
        public void SetFadeDrawVisible(bool visible)
        {
            _fadeDrawVisible = visible;
            ApplyFadeDrawVisibility();
        }

        /// <summary>Whether the FADE/DRAW button is currently shown. Read back by the acceptance
        /// run rather than inferred from a pixel.</summary>
        public bool IsFadeDrawVisible => _fadeDrawVisible;

        /// <summary>The button's live CanvasGroup alpha, so a test asserts the ACTUAL opacity
        /// rather than the flag that was supposed to set it.</summary>
        public float FadeDrawAlpha => FadeDrawGroup != null ? FadeDrawGroup.alpha : 1f;

        private CanvasGroup FadeDrawGroup
        {
            get
            {
                if (_fadeDrawGroup != null) return _fadeDrawGroup;
                if (_fadeDrawButton == null) return null;
                _fadeDrawGroup = _fadeDrawButton.GetComponent<CanvasGroup>();
                if (_fadeDrawGroup == null)
                    _fadeDrawGroup = _fadeDrawButton.gameObject.AddComponent<CanvasGroup>();
                return _fadeDrawGroup;
            }
        }

        private void ApplyFadeDrawVisibility()
        {
            var g = FadeDrawGroup;
            if (g == null) return;
            g.alpha          = _fadeDrawVisible ? 1f : 0f;
            g.blocksRaycasts = _fadeDrawVisible;
            g.interactable   = _fadeDrawVisible;
        }

        /// <summary>EditMode wiring seam — a plain MonoBehaviour gets no Awake or OnEnable in
        /// EditMode, so a test that only assigned the fields would drive an object that never
        /// started.</summary>
        public void ConfigureForTests(CanvasGroup group, FadeDrawButtonWidget fadeDrawButton)
        {
            _group          = group;
            _fadeDrawButton = fadeDrawButton;
            _fadeDrawGroup  = null;
            ApplyFadeDrawVisibility();
        }
    }
}
