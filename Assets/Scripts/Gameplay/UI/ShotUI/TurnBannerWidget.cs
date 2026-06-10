using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Full-width banner (1170×210 on 1170-wide canvas) that slides in horizontally,
    /// holds for a beat, then fades out. Used in 1v1 to announce whose turn it is.
    ///
    /// Usage:
    ///   bannerWidget.Show("YOUR TURN",      fromLeft: true);   // slides in from the left
    ///   bannerWidget.Show("OPPONENT'S TURN", fromLeft: false);  // slides in from the right
    ///
    /// R2-4 fix: replaced vertical drop-down with horizontal swipe.
    ///   • TMP auto-size is forced to settle (ForceMeshUpdate) BEFORE the tween
    ///     starts, eliminating the mid-animation font-size jump.
    ///   • "YOUR TURN" → fromLeft=true, "OPPONENT'S TURN" → fromLeft=false.
    ///
    /// R2-3 fix: label has comfortable horizontal margins (≥80px, Figma 318px each side).
    ///   Enforced by setting the label RectTransform horizontal offsets in Awake.
    ///
    /// Figma spec (node 4094:26052):
    ///   Size:        1170 × 210
    ///   BG gradient: rgba(19,52,83,0.5) → rgba(9,27,51,0.5) (translucent navy)
    ///   Borders:     3px #818EA1 on top and bottom
    ///   Text:        Rubik Medium, 128px, white (#FFFFFF), centred
    ///   Animation:   horizontal slide-in, hold 1.2 s, fade-out (alpha 1→0)
    ///   Text area:   318px padding each side → 534px text width
    /// </summary>
    public class TurnBannerWidget : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] TMP_Text      _label;
        [SerializeField] CanvasGroup   _canvasGroup;
        [SerializeField] RectTransform _rect;

        [Header("Layout")]
        [Tooltip("anchoredPosition.y when the banner is at rest (visible). " +
                 "With top-anchor/top-pivot, negative = below canvas top. " +
                 "Figma: ~664 px from top of 2532 canvas → -664.")]
        [SerializeField] float _restAnchoredY = -664f;

        [Tooltip("Horizontal padding applied to the label RectTransform (left and right). " +
                 "Figma specifies 318px each side for comfortable margins. " +
                 "Runtime Awake() enforces this via offsetMin/offsetMax.")]
        [SerializeField] float _labelHorizontalPadding = 318f;

        [Header("Animation timing (seconds)")]
        [SerializeField] float _slideInDuration  = 0.25f;
        [SerializeField] float _holdDuration     = 1.20f;
        [SerializeField] float _fadeOutDuration  = 0.30f;

        Coroutine _activeRoutine;

        void Awake()
        {
            // Resolve optional self-references.
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_rect        == null) _rect        = GetComponent<RectTransform>();

            // R2-3: enforce horizontal padding on the label so text has comfortable
            // margins (Figma: 318px each side, 534px text area on 1170-wide canvas).
            if (_label != null)
            {
                var labelRect = _label.rectTransform;
                var off       = labelRect.offsetMin;
                off.x = _labelHorizontalPadding;
                labelRect.offsetMin = off;

                var offMax = labelRect.offsetMax;
                offMax.x = -_labelHorizontalPadding;
                labelRect.offsetMax = offMax;
            }
        }

        /// <summary>
        /// Show the banner with <paramref name="text"/>.
        /// <paramref name="fromLeft"/> controls slide direction:
        ///   true  → slides in from the left  (YOUR TURN)
        ///   false → slides in from the right (OPPONENT'S TURN)
        /// If a Show is already in progress, it is cancelled and restarted.
        /// The hosting GameObject is activated automatically.
        /// </summary>
        public void Show(string text, bool fromLeft = true)
        {
            if (_label != null) _label.text = text;

            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            // R4-3 fix (iter-10): Pre-position the rect off-screen BEFORE SetActive(true)
            // to prevent the 1-frame center-flash bug.
            //
            // Root cause: gameObject.SetActive(true) causes Unity to render the banner
            // at its LAST anchoredPosition (x=0, center after previous settle) for one
            // frame BEFORE BannerRoutine can move it to startX (off-screen). That one
            // center frame reads as: "banner at center → jumps left → slides right" —
            // the post-arrival drift Cesar observed for YOUR TURN.
            //
            // Fix: snap the rect to the correct off-screen X while the GameObject is still
            // inactive (RectTransform can be modified while inactive — no layout update
            // fires). Then SetActive(true). The FIRST rendered frame is already off-screen,
            // so the slide begins cleanly with no prior center-flash.
            //
            // Canvas width: resolve eagerly here (same logic as BannerRoutine).
            // If the canvas isn't available yet, fall back to 1170f (design width).
            {
                float resolvedWidth = 1170f;
                var parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    var crt = parentCanvas.GetComponent<RectTransform>();
                    if (crt != null) resolvedWidth = crt.rect.width;
                }
                float preStartX = fromLeft ? -resolvedWidth : resolvedWidth;
                if (_rect != null)
                    _rect.anchoredPosition = new Vector2(preStartX, _restAnchoredY);
            }

            gameObject.SetActive(true);

            // R2-4: Force TMP auto-size to settle BEFORE the tween starts.
            // Without this, TMP recalculates the font size mid-animation, causing a
            // visible size jump during the slide.  ForceMeshUpdate() + one frame of
            // layout ensures the final settled size is locked in before we move the rect.
            if (_label != null)
            {
                _label.ForceMeshUpdate();
                // Lock the settled font size so TMP stops auto-recalculating.
                _label.enableAutoSizing = false;
            }

            _activeRoutine = StartCoroutine(BannerRoutine(fromLeft));
        }

        /// <summary>
        /// Show the banner with <paramref name="text"/> and hold it indefinitely (no auto fade-out).
        /// The banner stays visible until <see cref="Hide"/> is called (or the object is deactivated).
        /// Slide-in animation is identical to <see cref="Show"/>. If a routine is in progress it
        /// is cancelled and restarted. Used for WIN / LOSE / DRAW at match end (Phase 2a).
        /// </summary>
        public void ShowPersistent(string text, bool fromLeft = true)
        {
            if (_label != null) _label.text = text;

            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            // Pre-position off-screen before SetActive(true) to avoid 1-frame center-flash.
            {
                float resolvedWidth = 1170f;
                var parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    var crt = parentCanvas.GetComponent<RectTransform>();
                    if (crt != null) resolvedWidth = crt.rect.width;
                }
                float preStartX = fromLeft ? -resolvedWidth : resolvedWidth;
                if (_rect != null)
                    _rect.anchoredPosition = new Vector2(preStartX, _restAnchoredY);
            }

            gameObject.SetActive(true);

            if (_label != null)
            {
                _label.ForceMeshUpdate();
                _label.enableAutoSizing = false;
            }

            _activeRoutine = StartCoroutine(BannerPersistentRoutine(fromLeft));
        }

        /// <summary>
        /// Immediately hide the banner (deactivates the GameObject).
        /// Stops any in-progress animation.
        /// </summary>
        public void Hide()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);

            if (_label != null)
                _label.enableAutoSizing = true;
        }

        IEnumerator BannerPersistentRoutine(bool fromLeft)
        {
            float canvasWidth = 1170f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null) canvasWidth = canvasRect.rect.width;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1f;

            float startX = fromLeft ? -canvasWidth : canvasWidth;
            if (_rect != null)
                _rect.anchoredPosition = new Vector2(startX, _restAnchoredY);

            // Slide in.
            float t = 0f;
            while (t < _slideInDuration)
            {
                t += Time.deltaTime;
                float norm  = Mathf.Clamp01(t / _slideInDuration);
                float eased = 1f - (1f - norm) * (1f - norm);
                if (_rect != null)
                    _rect.anchoredPosition = new Vector2(Mathf.Lerp(startX, 0f, eased), _restAnchoredY);
                yield return null;
            }

            if (_rect != null)
                _rect.anchoredPosition = new Vector2(0f, _restAnchoredY);

            // Hold indefinitely — no fade-out, no deactivate. Caller must call Hide().
            _activeRoutine = null;
        }

        IEnumerator BannerRoutine(bool fromLeft)
        {
            // Canvas width for off-screen start position (1170px standard).
            float canvasWidth = 1170f;
            // Resolve actual canvas width if available.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var canvasRect = canvas.GetComponent<RectTransform>();
                if (canvasRect != null) canvasWidth = canvasRect.rect.width;
            }

            // ── Initialise ──────────────────────────────────────────────────
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;

            // Snap banner to rest Y, start X off-screen.
            float startX = fromLeft ? -canvasWidth : canvasWidth;

            if (_rect != null)
            {
                _rect.anchoredPosition = new Vector2(startX, _restAnchoredY);
            }

            // ── Slide in ────────────────────────────────────────────────────
            float t = 0f;
            while (t < _slideInDuration)
            {
                t += Time.deltaTime;
                float norm  = Mathf.Clamp01(t / _slideInDuration);
                float eased = 1f - (1f - norm) * (1f - norm);   // ease-out quad
                if (_rect != null)
                {
                    _rect.anchoredPosition = new Vector2(
                        Mathf.Lerp(startX, 0f, eased),
                        _restAnchoredY);
                }
                yield return null;
            }

            // Snap to exact centre.
            if (_rect != null)
                _rect.anchoredPosition = new Vector2(0f, _restAnchoredY);

            // ── Hold ─────────────────────────────────────────────────────────
            yield return new WaitForSeconds(_holdDuration);

            // ── Fade out ─────────────────────────────────────────────────────
            t = 0f;
            while (t < _fadeOutDuration)
            {
                t += Time.deltaTime;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / _fadeOutDuration));
                yield return null;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            // Deactivate after animation so the banner doesn't block raycasts.
            gameObject.SetActive(false);

            // Re-enable auto-sizing for the next Show() call.
            if (_label != null)
                _label.enableAutoSizing = true;

            _activeRoutine = null;
        }
    }
}
