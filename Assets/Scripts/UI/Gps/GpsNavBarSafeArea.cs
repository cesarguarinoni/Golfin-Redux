// ─────────────────────────────────────────────────────────────────────────────
// The GPS nav bar and the home indicator.
//
// THE BUG THIS REPLACES. gps_polish §D9 wrapped the bar in a full-screen
// `SafeAreaFitter` (baseline 0) so the icons would clear the home indicator.
// That inset the WHOLE BAR: the bar's bottom edge moved to the top of the
// indicator and 102 px of screen background showed underneath it. Cesar caught
// it on the first device pass — "the bottom nav bar is not anchored to the
// bottom of the screen in GPS" — and he was right.
//
// It survived every gate because at the 1170x2532 Editor reference
// `Screen.safeArea` IS the whole screen, so the inset is zero and nothing moves.
// The gps_polish report even said so in writing. The verification was honest;
// the design was wrong.
//
// WHAT A BOTTOM BAR IS SUPPOSED TO DO. Its background reaches the physical
// bottom edge — a floating bar reads as broken — and only its CONTENT is lifted
// clear of the indicator. The same repo already had the pattern next door:
// `safe_area_top_bar` gives the TOP bar a 141 px baseline precisely so it does
// NOT move on an iPhone 14.
//
// HOW. `GpsNavBar` has pivot y = 0 and sits at anchoredPosition y = 0, so
// growing its height extends it UPWARD and leaves the bottom pinned to the
// screen edge. Its four icon buttons are TOP-anchored (0,1), so they ride the
// rising top edge for free. Only the centre camera button is bottom-anchored,
// and it gets the inset added to its y.
//
// RUNTIME ONLY — NOT [ExecuteAlways], and that is deliberate. An edit-mode
// version would rewrite the bar's height in the open prefab, a later save would
// serialise the GROWN value as the authored one, and the next run would grow it
// again from there. Cumulative asset drift. In the Editor the safe area is the
// whole screen anyway, so there is nothing to do and nothing to risk.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Grows the GPS nav bar downward-pinned by the bottom safe-area inset so its background still
    /// reaches the screen edge while its content clears the home indicator.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class GpsNavBarSafeArea : MonoBehaviour
    {
        /// <summary>The geometry, as a pure function of four numbers.</summary>
        public readonly struct Layout
        {
            /// <summary>The bar's <c>sizeDelta.y</c>.</summary>
            public readonly float Height;

            /// <summary>A BOTTOM-anchored child's <c>anchoredPosition.y</c>.</summary>
            public readonly float BottomChildY;

            public Layout(float height, float bottomChildY)
            {
                Height = height;
                BottomChildY = bottomChildY;
            }
        }

        /// <summary>
        /// Where the bar and its bottom-anchored children sit for a given inset.
        ///
        /// <para>Pure so it can be pinned without a device: everything phone-shaped — is there an
        /// indicator, how tall is it — is gathered by the caller and handed in. A negative or
        /// absent inset returns the authored values unchanged, which is the Editor case and the
        /// reason this cannot move a rest pixel there.</para>
        /// </summary>
        public static Layout For(float authoredHeight, float authoredBottomChildY, float bottomInsetPx)
        {
            float inset = bottomInsetPx > 0f ? bottomInsetPx : 0f;
            return new Layout(authoredHeight + inset, authoredBottomChildY + inset);
        }

        /// <summary>The bottom safe-area inset in screen px; 0 where there is no indicator.</summary>
        public static float BottomInsetPx()
        {
            if (Screen.width == 0 || Screen.height == 0) return 0f;
            return Mathf.Max(0f, Screen.safeArea.y);
        }

        private RectTransform? _bar;
        private RectTransform[] _bottomAnchored = new RectTransform[0];
        private float[] _authoredChildY = new float[0];
        private float _authoredHeight;
        private bool _captured;
        private float _applied = -1f;

        private void Awake()   => Capture();
        private void OnEnable() => Apply();

        private void Update()
        {
            // Poll like SafeAreaFitter does: the inset can change on a rotation or when the app
            // returns from the background, and it is frequently 0 for the first frames.
            float inset = BottomInsetPx();
            if (!Mathf.Approximately(inset, _applied)) Apply();
        }

        /// <summary>Read the AUTHORED values once, before anything has been grown.</summary>
        private void Capture()
        {
            if (_captured) return;
            _bar = transform as RectTransform;
            if (_bar == null) return;

            _authoredHeight = _bar.sizeDelta.y;

            var bottom = new System.Collections.Generic.List<RectTransform>();
            var ys     = new System.Collections.Generic.List<float>();
            foreach (Transform child in _bar)
            {
                if (child is not RectTransform rt) continue;
                // BOTTOM-anchored only. The icon buttons are anchored to the top (0,1) and follow
                // the rising top edge on their own; adding the inset to them too would move them
                // twice.
                if (rt.anchorMin.y != 0f || rt.anchorMax.y != 0f) continue;
                bottom.Add(rt);
                ys.Add(rt.anchoredPosition.y);
            }
            _bottomAnchored  = bottom.ToArray();
            _authoredChildY  = ys.ToArray();
            _captured = true;
        }

        private void Apply()
        {
            Capture();
            if (_bar == null) return;

            float inset = BottomInsetPx();
            Layout l = For(_authoredHeight, 0f, inset);

            _bar.sizeDelta = new Vector2(_bar.sizeDelta.x, l.Height);
            for (int i = 0; i < _bottomAnchored.Length; i++)
            {
                RectTransform rt = _bottomAnchored[i];
                if (rt == null) continue;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                  For(0f, _authoredChildY[i], inset).BottomChildY);
            }
            _applied = inset;
        }
    }
}
