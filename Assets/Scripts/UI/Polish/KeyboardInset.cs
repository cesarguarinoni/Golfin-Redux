// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D9 — keeping a focused field above the iOS keyboard.
//
// THE DEFECT: Golf Profile's nickname sits at y≈1520 and its handicap at y≈1740
// on a 2532-tall screen, and the Vote CREATE modal's question field is near the
// modal's bottom edge. The iOS keyboard claims roughly the bottom 800 px. All
// three are UNDER it while being typed into, and because
// `shouldHideMobileInput = true` (which is right — see
// CreateUsernameScreenController) there is no OS input bar echoing the text
// either. The player types blind.
//
// WHY THE HEIGHT AND NOT THE RECT. `TouchScreenKeyboard.area` has reported its
// origin at the top on some iOS versions and at the bottom on others, so a
// reading that trusts `area.y` is a coin flip per OS release. The keyboard is
// always flush to the bottom of the screen, so `area.height` alone is enough and
// is the same number under either convention. That is the only value this file
// reads from it.
//
// THE MATH IS PURE AND SEPARATE. `OffsetFor` takes five numbers and returns one;
// it has no Unity dependency beyond Mathf and is pinned by KeyboardInsetTests.
// Everything device-shaped — is there a keyboard, how big is it, where is the
// field — is gathered by the binder and handed in. This is what makes an item
// that can only be SEEN on the phone still be verifiable in the Editor.
//
// IN THE EDITOR IT IS A NO-OP. `TouchScreenKeyboard.visible` is false and
// `area.height` is 0, so `OffsetFor` returns 0 and nothing moves — which is why
// A2's rest parity is untouched by this.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using TMPro;
using UnityEngine;

namespace Golfin.UI.Polish
{
    /// <summary>The offset maths, and the component that applies it to a focused field.</summary>
    public static class KeyboardInset
    {
        /// <summary>Breathing room left between the field's bottom edge and the keyboard, in
        /// canvas px. One row of the GPS 24 px rhythm.</summary>
        public const float Margin = 24f;

        /// <summary>
        /// How far a container must move UP, in CANVAS px, so that a field clears the keyboard.
        ///
        /// <para>All screen-space arguments are measured from the BOTTOM of the screen upwards,
        /// which is the convention <c>RectTransform.GetWorldCorners</c> hands back under an
        /// Overlay canvas. Returns 0 when there is no keyboard, when the field already clears it,
        /// or when the shift would push the field's own top off the screen — in that last case
        /// the largest shift that keeps the field fully visible is returned instead, because
        /// scrolling a field out of the top to escape the keyboard is not a fix.</para>
        /// </summary>
        /// <param name="screenHeightPx">Screen height in pixels.</param>
        /// <param name="keyboardHeightPx">Keyboard height in pixels; 0 when hidden.</param>
        /// <param name="fieldBottomPx">The field's bottom edge, from the bottom of the screen.</param>
        /// <param name="fieldTopPx">The field's top edge, from the bottom of the screen.</param>
        /// <param name="canvasScale">Canvas scale factor — screen px per canvas px.</param>
        /// <param name="marginCanvasPx">Gap to leave under the field, in canvas px.</param>
        public static float OffsetFor(float screenHeightPx, float keyboardHeightPx,
                                      float fieldBottomPx, float fieldTopPx,
                                      float canvasScale, float marginCanvasPx = Margin)
        {
            if (keyboardHeightPx <= 0f) return 0f;

            float scale  = Mathf.Max(0.0001f, canvasScale);
            float margin = marginCanvasPx * scale;

            float needed = keyboardHeightPx + margin - fieldBottomPx;
            if (needed <= 0f) return 0f;

            // Never shift so far that the field's own top leaves the screen.
            float headroom = screenHeightPx - fieldTopPx - margin;
            if (headroom <= 0f) return 0f;

            return Mathf.Min(needed, headroom) / scale;
        }

        /// <summary>
        /// The live keyboard height in screen px, or 0 in the Editor and on any platform with no
        /// software keyboard. See the header for why only the HEIGHT is read.
        /// </summary>
        public static float KeyboardHeightPx()
        {
            if (!TouchScreenKeyboard.isSupported) return 0f;
            if (!TouchScreenKeyboard.visible) return 0f;
            Rect area = TouchScreenKeyboard.area;
            return Mathf.Max(0f, area.height);
        }
    }

    /// <summary>
    /// Slides one container up while one of its fields is focused, and back down on blur.
    ///
    /// <para>Added at runtime by the screen controller — <see cref="Attach"/> — rather than
    /// authored, so no prefab changes and, since the rest offset is 0, no rest pixel moves.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class KeyboardInsetBinder : MonoBehaviour
    {
        private RectTransform? _content;
        private float _restY;
        private bool  _haveRest;
        private Coroutine? _motion;

        /// <summary>The field currently focused, so <see cref="Update"/> can re-measure when the
        /// keyboard finishes its own slide-up (its height is 0 for the first frames).</summary>
        private RectTransform? _focused;
        private float _applied;

        /// <summary>
        /// Make <paramref name="field"/> lift <paramref name="content"/> above the keyboard while
        /// it is focused. Idempotent per field: the listeners are added once.
        /// </summary>
        public static void Attach(TMP_InputField? field, RectTransform? content)
        {
            if (field == null || content == null) return;

            var binder = content.GetComponent<KeyboardInsetBinder>();
            if (binder == null) binder = content.gameObject.AddComponent<KeyboardInsetBinder>();
            binder.Bind(content);

            RectTransform fieldRect = (RectTransform)field.transform;
            field.onSelect.AddListener(_ => binder.Focus(fieldRect));
            field.onDeselect.AddListener(_ => binder.Blur());
        }

        private void Bind(RectTransform content)
        {
            if (_haveRest) return;
            _content  = content;
            _restY    = content.anchoredPosition.y;
            _haveRest = true;
        }

        private void Focus(RectTransform field)
        {
            _focused = field;
            Apply(Measure(field));
        }

        private void Blur()
        {
            _focused = null;
            Apply(0f);
        }

        private void Update()
        {
            // The keyboard animates in over ~0.25 s and reports height 0 until it has finished,
            // so the offset computed on the onSelect frame is usually 0. Re-measuring while a
            // field is focused is what makes the lift actually happen on a device.
            if (_focused == null) return;
            float want = Measure(_focused);
            if (!Mathf.Approximately(want, _applied)) Apply(want);
        }

        private float Measure(RectTransform field)
        {
            if (_content == null || field == null) return 0f;

            var corners = new Vector3[4];
            field.GetWorldCorners(corners);

            Canvas? canvas = _content.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;

            // Overlay canvases put world corners straight into screen px; a Camera canvas needs
            // the projection, and the GPS screens are all Overlay (ScreenManager's one canvas).
            float bottom = corners[0].y;
            float top    = corners[1].y;

            return KeyboardInset.OffsetFor(Screen.height, KeyboardInset.KeyboardHeightPx(),
                                           bottom, top, scale);
        }

        private void Apply(float offset)
        {
            if (_content == null || !_haveRest) return;
            _applied = offset;

            float from = _content.anchoredPosition.y;
            float to   = _restY + offset;
            if (Mathf.Approximately(from, to)) return;

            UiMotion.Run(this, ref _motion, UiMotion.Tween(from, to, UiMotion.FadeDur, SetY));
        }

        /// <summary>Cached so the tween allocates one delegate at creation, never per frame.</summary>
        private System.Action<float>? _setYCache;

        private System.Action<float> SetY
            => _setYCache ??= y =>
            {
                if (_content != null)
                    _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, y);
            };

        private void OnDisable()
        {
            UiMotion.Stop(this, ref _motion);
            if (_content != null && _haveRest)
                _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, _restY);
            _focused = null;
            _applied = 0f;
        }
    }
}
