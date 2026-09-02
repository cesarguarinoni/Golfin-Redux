// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D8 — the loading placeholder.
//
// WHAT IT REPLACES: nothing. That is the point. Before this, a GPS panel waiting
// on its first fetch drew an empty rectangle — the hub's rounds list, the badges
// grid, Top Supporters, Popular Golfers and the vote list all opened blank and
// then popped full. A blank panel is indistinguishable from a BROKEN panel, and
// the fetch is the one moment where the player has no other signal.
//
// WHEN IT MUST NOT APPEAR: on a paint-cache hit. Every one of those screens
// paints from cache first and only then fetches (GpsHubScreenController's
// "paint what is already known BEFORE any request"), so a shimmer on a repaint
// would flash a loading state over numbers that were already correct. The call
// sites gate on "did the cache have anything", never on "is a request running".
//
// NO SHADER, NO MASK MATERIAL. An Image + a RectMask2D and one moving child.
// A shimmer shader would be a new material variant to review, and this is a
// placeholder that lives for at most a few hundred milliseconds.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// A rounded dark block with a highlight band sweeping across it. Authored by
    /// <c>GpsPolishBuilder</c>; shown and hidden by the screen controllers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShimmerBlock : MonoBehaviour
    {
        /// <summary>One full sweep, left edge to right edge.</summary>
        public const float Period = 1.2f;

        [Tooltip("The highlight band. Swept horizontally across this block's width; clipped by " +
                 "the RectMask2D on the block itself.")]
        [SerializeField] private RectTransform? _band;

        private RectTransform? _self;
        private float _phase;

        private void OnEnable()
        {
            // Restart from the left on every show. A block that resumed mid-sweep would have its
            // band frozen wherever the last fetch happened to finish.
            _phase = 0f;
            Step(0f);
        }

        private void Update()
        {
            // Unscaled: a shimmer that stops because a modal paused the game is a frozen
            // loading state, which reads as a hang.
            _phase += Time.unscaledDeltaTime / Period;
            if (_phase >= 1f) _phase -= 1f;
            Step(_phase);
        }

        private void Step(float t)
        {
            if (_band == null) return;
            if (_self == null) _self = transform as RectTransform;
            if (_self == null) return;

            float w    = _self.rect.width;
            float band = _band.rect.width;
            // Starts fully off the left edge and ends fully off the right, so there is no frame
            // where a half-band sits parked at an edge.
            _band.anchoredPosition = new Vector2(Mathf.Lerp(-band, w + band, t), _band.anchoredPosition.y);
        }
    }
}
