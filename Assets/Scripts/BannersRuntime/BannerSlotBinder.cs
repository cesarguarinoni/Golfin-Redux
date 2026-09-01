// ─────────────────────────────────────────────────────────────────────────────
// BannersRuntime — BannerSlotBinder
// One small MonoBehaviour that both banner slots carry, so neither screen
// controller learns that a network exists. It draws the served artwork on the
// Image that is already there, gates the Button this task added, and — when
// nothing is live — takes the slot OUT of the layout entirely.
//
// ⚠️ Behaviour of record (Cesar, 2026-08-17), REPLACING SPEC §4.2's ladder:
// "if no banner is present, nothing should be shown and the UI should adapt."
// The bundled sprite is NOT a runtime fallback. It stays in the scene as the
// authoring placeholder so the slot is visible while editing, and it is never
// shown to a player. No live banner = no slot, and what follows closes up.
// Reference frames: Figma 13027-5212 (Home) and 4079-1727 (Rankings).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Golfin.Tournaments;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Banners
{
    /// <summary>
    /// Binds one <see cref="BannerPlacement"/> to the <see cref="Image"/> already sitting in the
    /// scene, and to the <see cref="Button"/> added alongside it.
    /// <para>
    /// The slot is hidden by default and only revealed once real artwork has actually decoded, so
    /// there is no frame in which a player sees stale or placeholder art: no network, an expired
    /// row, a refused URL and a failed download all end with the slot absent.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BannerSlotBinder : MonoBehaviour
    {
        private const string Tag = "[Banners]";

        [Header("Slot")]
        [SerializeField] private BannerPlacement _placement;

        [Tooltip("The Image already in the scene. Its authored sprite is an editing placeholder only — " +
                 "at runtime the slot is either showing served artwork or hidden.")]
        [SerializeField] private Image? _image;

        [Tooltip("The Button added by the game_banners task. Interactable only when a live banner " +
                 "carries an allowlisted link.")]
        [SerializeField] private Button? _button;

        [Header("Layout adaptation")]
        [Tooltip("Optional. Every RectTransform here ABSORBS this slot's height when the slot is " +
                 "hidden, so the screen closes up instead of leaving a banner-shaped gap.\n\n" +
                 "Rankings needs BOTH its panel and the list inside it. The panel keeps its bottom " +
                 "edge; the list grows by the same amount so the panel's deliberate content " +
                 "overflow — which is what holds the pinned YOU card below the panel — is " +
                 "preserved exactly. Growing only the panel swallows that card.\n\n" +
                 "Home leaves this empty: its strip is bottom-anchored with nothing positioned " +
                 "relative to it.")]
        [SerializeField] private RectTransform[] _expandOnHide = new RectTransform[0];

        [Tooltip("Optional. Every RectTransform here MOVES DOWN when the slot is hidden, until its " +
                 "bottom edge rests exactly where the banner's bottom edge was — so the content " +
                 "above closes the gap instead of leaving a banner-shaped hole.\n\n" +
                 "Home sets this to ModeCarouselSection: its strip is bottom-anchored under a " +
                 "screen with NO layout group, so nothing moves on its own. Rankings leaves it " +
                 "empty — ContentArea's VerticalLayoutGroup already closes up.\n\n" +
                 "The distance is MEASURED from the authored geometry, not typed in, so " +
                 "re-positioning either element cannot silently desync it.")]
        [SerializeField] private RectTransform[] _shiftDownOnHide = new RectTransform[0];

        [Tooltip("Canvas px trimmed off the measured drop, per the target's own drawn extent.\n\n" +
                 "Home uses 2. ModeHomeCard carries an Outline with effectDistance (2, -2), and a " +
                 "UI Outline draws four copies at the four sign combinations — so the card paints " +
                 "2px BELOW its RectTransform. An untrimmed drop lands the card's rect 24px above " +
                 "the Tee button but its VISIBLE edge only 22px above it.\n\n" +
                 "Not auto-measured on purpose: the carousel's cards are runtime clones, so a scan " +
                 "for outlines during OnEnable would find nothing and silently fall back to 0.")]
        [SerializeField] private float _shiftDownTrim;

        /// <summary>Authored anchoredPositions of <c>_shiftDownOnHide</c>, so the move is idempotent.</summary>
        private Vector2[]? _shiftBasePositions;

        /// <summary>How far each target must drop, in ITS parent's local units. Measured once.</summary>
        private float[]? _shiftDistances;

        /// <summary>
        /// Each <c>_expandOnHide</c> entry's authored height, captured once before anything touches
        /// it, so the grow/shrink is idempotent no matter how many times <see cref="Apply"/> runs.
        /// </summary>
        private float[]? _expandBaseHeights;

        /// <summary>
        /// Flipped to <c>ignoreLayout</c> when the slot is hidden, so a parent layout group closes
        /// the gap instead of leaving a banner-shaped hole. Resolved lazily — Home's slot has no
        /// layout group parent and needs none.
        /// </summary>
        private LayoutElement? _layoutElement;
        private bool _layoutElementResolved;

        /// <summary>The link the CURRENT banner carries, or null. Re-validated on click regardless.</summary>
        private string? _link;

        private void OnEnable()
        {
            if (_image == null)
            {
                Debug.LogWarning($"{Tag} {name}: no Image assigned; the slot cannot be bound.");
                return;
            }

            BannerService.OnBannersChanged += Apply;
            LocalizationManager.OnLanguageChanged += Apply;

            // Throttled inside the service — entering this screen five times in ten seconds is
            // still one request.
            BannerService.Instance?.Refresh();

            Apply();
        }

        private void OnDisable()
        {
            BannerService.OnBannersChanged -= Apply;
            LocalizationManager.OnLanguageChanged -= Apply;
        }

        /// <summary>
        /// Resolve the placement and draw it. Safe to call at any time; called on screen entry, on
        /// a fetch that changed the set, and on a language switch.
        /// </summary>
        public void Apply()
        {
            if (_image == null) return;

            var service = BannerService.Instance;
            if (service == null || !service.TryGet(_placement, out BannerDefinition banner))
            {
                Hide();
                return;
            }

            _link = banner.LinkUrl;

            // Stay hidden until the artwork has actually decoded. Revealing first would flash the
            // authored placeholder for however long the download takes, and would leave it on
            // screen forever if the download never lands — Request never calls back on failure.
            TournamentArtService.Banners.Request(banner.ImageUrl, sprite =>
            {
                if (_image == null || sprite == null) return;
                _image.sprite = sprite;
                Show();
            });
        }

        /// <summary>Reveal the slot and put it back in the layout.</summary>
        private void Show()
        {
            if (_image == null) return;

            SetIgnoreLayout(false);
            SetExpanded(false);
            SetShiftedDown(false);
            _image.enabled = true;
            _image.raycastTarget = true;

            if (_button != null) _button.interactable = BannerPolicy.IsLinkAllowed(_link);
        }

        /// <summary>
        /// Take the slot out of the UI: nothing drawn, nothing tappable, and — under a layout
        /// group — no space reserved, so the following content moves up.
        /// </summary>
        private void Hide()
        {
            _link = null;

            if (_button != null) _button.interactable = false;
            if (_image != null)
            {
                _image.enabled = false;
                // An invisible slot must not keep swallowing taps meant for what is behind it.
                _image.raycastTarget = false;
            }
            SetIgnoreLayout(true);
            SetExpanded(true);
            SetShiftedDown(true);
        }

        /// <summary>
        /// Drop each <c>_shiftDownOnHide</c> target so its bottom edge lands where the banner's
        /// bottom edge was — literally "let it rest where the banner was".
        /// <para>
        /// The distance is measured ONCE from the authored geometry (target bottom minus slot
        /// bottom), which on Home is the banner's height plus the 24px design gap. Measuring beats
        /// a serialized number: re-sizing the slot or moving the cards cannot desync it.
        /// </para>
        /// </summary>
        private void SetShiftedDown(bool shifted)
        {
            if (_shiftDownOnHide == null || _shiftDownOnHide.Length == 0) return;

            // Length check, not just null — same stale-cache trap as SetExpanded.
            if (_shiftBasePositions == null || _shiftBasePositions.Length != _shiftDownOnHide.Length)
            {
                _shiftBasePositions = new Vector2[_shiftDownOnHide.Length];
                _shiftDistances     = new float[_shiftDownOnHide.Length];

                var slot = _image != null ? (RectTransform)_image.transform : null;
                float slotBottom = 0f;
                if (slot != null)
                {
                    var sc = new Vector3[4];
                    slot.GetWorldCorners(sc);
                    slotBottom = sc[0].y;
                }

                for (int i = 0; i < _shiftDownOnHide.Length; i++)
                {
                    var rt = _shiftDownOnHide[i];
                    if (rt == null) continue;
                    _shiftBasePositions[i] = rt.anchoredPosition;
                    if (slot == null) continue;

                    var tc = new Vector3[4];
                    rt.GetWorldCorners(tc);
                    float worldDrop = tc[0].y - slotBottom;   // how far its bottom sits ABOVE the slot's

                    var parent = rt.parent as RectTransform;
                    float local = parent != null
                        ? parent.InverseTransformVector(new Vector3(0f, worldDrop, 0f)).y
                        : worldDrop;

                    // Trim by the target's own outline/shadow overhang so the gap the player
                    // SEES is the design gap, not the gap between invisible rect edges.
                    _shiftDistances[i] = local - _shiftDownTrim;
                }
            }

            for (int i = 0; i < _shiftDownOnHide.Length; i++)
            {
                var rt = _shiftDownOnHide[i];
                if (rt == null) continue;
                var b = _shiftBasePositions[i];
                rt.anchoredPosition = shifted ? new Vector2(b.x, b.y - _shiftDistances![i]) : b;
            }
        }

        /// <summary>
        /// Give the reclaimed height to <c>_expandOnHide</c> (or take it back).
        /// <para>
        /// The amount is this slot's own height plus the parent layout group's spacing — i.e.
        /// exactly the vertical space the slot stops occupying — so the panel below keeps its
        /// bottom edge instead of sliding up and leaving a banner-shaped gap above the nav bar.
        /// Measured from the live RectTransform rather than hard-coded, so re-authoring the slot
        /// does not silently desync this.
        /// </para>
        /// </summary>
        private void SetExpanded(bool expanded)
        {
            if (_expandOnHide == null || _expandOnHide.Length == 0) return;

            // Length check, not just null: the cache can be captured before the serialized array
            // is applied (a prefab value overriding a scene instance's), and a stale shorter cache
            // would index out of bounds.
            if (_expandBaseHeights == null || _expandBaseHeights.Length != _expandOnHide.Length)
            {
                _expandBaseHeights = new float[_expandOnHide.Length];
                for (int i = 0; i < _expandOnHide.Length; i++)
                    _expandBaseHeights[i] = _expandOnHide[i] != null ? _expandOnHide[i].rect.height : 0f;
            }

            float reclaimed = 0f;
            if (expanded && _image != null)
            {
                reclaimed = ((RectTransform)_image.transform).rect.height;
                var parentGroup = transform.parent != null
                    ? transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>()
                    : null;
                if (parentGroup != null) reclaimed += parentGroup.spacing;
            }

            for (int i = 0; i < _expandOnHide.Length; i++)
            {
                var rt = _expandOnHide[i];
                if (rt == null) continue;
                rt.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical, _expandBaseHeights[i] + reclaimed);
            }
        }

        /// <summary>
        /// <c>ignoreLayout</c> is what makes the parent <see cref="LayoutGroup"/> close the gap.
        /// The <see cref="LayoutElement"/> is added on demand and only when this slot actually sits
        /// in a layout group — Home's does not, and there the <c>Image.enabled</c> flip is the
        /// whole story because nothing is positioned relative to the strip.
        /// </summary>
        private void SetIgnoreLayout(bool ignore)
        {
            if (!_layoutElementResolved)
            {
                _layoutElementResolved = true;
                _layoutElement = GetComponent<LayoutElement>();
                if (_layoutElement == null &&
                    transform.parent != null &&
                    transform.parent.GetComponent<LayoutGroup>() != null)
                {
                    _layoutElement = gameObject.AddComponent<LayoutElement>();
                }
            }

            // The property setter marks the parent layout dirty, so the rebuild is automatic.
            if (_layoutElement != null) _layoutElement.ignoreLayout = ignore;
        }

        /// <summary>
        /// The tap. Wired either from the Button's own <c>onClick</c> (Rankings) or delegated by the
        /// screen controller that already owns the button (Home's
        /// <c>HomeScreenController.OnPromoBannerClicked</c>) — never both, or one tap opens twice.
        /// <para>
        /// The allowlist is re-checked HERE rather than trusting the flag set when the banner was
        /// applied: a refresh can swap the banner between those two moments.
        /// </para>
        /// <para>
        /// gps_hub_entry §1 — an INTERNAL route (<c>golfin://gps</c>) navigates instead of leaving
        /// the app. The branch sits after the same re-check, so the two destinations are gated by
        /// one decision rather than by two that can disagree.
        /// </para>
        /// </summary>
        public void OpenLink()
        {
            string? link = _link;
            if (!BannerPolicy.IsLinkAllowed(link))
            {
                if (!string.IsNullOrEmpty(link))
                    Debug.LogWarning($"{Tag} Refusing to open a link outside the allowlisted hosts.");
                return;
            }

            if (BannerPolicy.TryGetInternalRoute(link, out GolfinRedux.UI.ScreenId screen))
            {
                Debug.Log($"{Tag} Routing banner link for {_placement} \u2192 {screen}.");
                GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(screen);
                return;
            }

            Debug.Log($"{Tag} Opening banner link for {_placement}.");
            Application.OpenURL(link!);
        }
    }
}
