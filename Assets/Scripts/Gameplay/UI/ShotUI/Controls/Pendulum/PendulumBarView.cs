using UnityEngine;

namespace Golfin.Gameplay.UI.Controls.Pendulum
{
    /// <summary>
    /// The timing bar above the ball (Figma <c>PendulumTrack</c> + bands + <c>PendulumMarker</c>).
    ///
    /// <para>THE BANDS ARE THE WINDOWS, DRAWN — the same contract the lane has with the pull
    /// thresholds. <c>BandJust.width = JustWindow01 × trackWidth</c> and likewise for GOOD, so a
    /// player with 120 Accuracy sees a visibly wider green band and it is wider by exactly the
    /// amount that makes their JUST easier. The Figma widths (100 / 288 of 720) are one sample of
    /// that formula at Accuracy ≈ 60, not authored constants: hard-coding them would draw every
    /// character the same bar while grading them differently, which is the worst of both.</para>
    ///
    /// <para>The marker's CENTRE travels the full track half-width, so m = ±1 puts it at the track
    /// end. That is the same normalisation the bands use, which is what lets the player read
    /// "inside the green band" as "JUST" with no mental correction.</para>
    /// </summary>
    public class PendulumBarView : PendulumFadingView
    {
        [Header("Figma: Scheme — Pendulum (14091:33885)")]
        [SerializeField] private RectTransform _track;
        [SerializeField] private RectTransform _bandGood;
        [SerializeField] private RectTransform _bandJust;
        [SerializeField] private RectTransform _centrePip;
        [SerializeField] private RectTransform _marker;

        [Header("Geometry (Figma node values — canvas px)")]
        [Tooltip("Track width for a full swing. Figma PendulumTrack w=720.")]
        [SerializeField] private float _swingTrackWidth = 720f;
        [Tooltip("Track width for a putt. Figma Putt-frame PendulumTrack w=520 (14091:34095).")]
        [SerializeField] private float _puttTrackWidth  = 520f;

        /// <summary>Half the track width — the marker's travel at |m| = 1, in canvas px.</summary>
        public float HalfTravelPx { get; private set; }

        /// <summary>The bands as DRAWN, in canvas px. Read back by the tests and the acceptance
        /// bot so "the target shrank" is measured off the actual rect rather than recomputed from
        /// the same formula that drew it.</summary>
        public float JustWidthPx => _bandJust != null ? _bandJust.rect.width : 0f;
        public float GoodWidthPx => _bandGood != null ? _bandGood.rect.width : 0f;

        /// <summary>
        /// Size the track and the two bands for this swing's accuracy. Call at Activate and on a
        /// club change; the windows cannot move while a finger is down.
        /// </summary>
        public void ApplyWindows(float justWindow01, float goodWindow01, bool isPutt)
        {
            float trackWidth = isPutt ? _puttTrackWidth : _swingTrackWidth;
            HalfTravelPx = trackWidth * 0.5f;

            if (_track != null)
                _track.sizeDelta = new Vector2(trackWidth, _track.sizeDelta.y);

            // A band of half-window w01 spans w01 of the half-travel EACH SIDE of centre, so its
            // full drawn width is w01 * trackWidth. Clamped to the track so a mis-tuned config
            // draws a full-width band rather than one hanging off both ends.
            SetBandWidth(_bandGood, Mathf.Clamp01(goodWindow01) * trackWidth);
            SetBandWidth(_bandJust, Mathf.Clamp01(justWindow01) * trackWidth);

            if (_centrePip != null)
                _centrePip.anchoredPosition = new Vector2(0f, _centrePip.anchoredPosition.y);
        }

        private static void SetBandWidth(RectTransform band, float width)
        {
            if (band == null) return;
            band.sizeDelta        = new Vector2(width, band.sizeDelta.y);
            band.anchoredPosition = new Vector2(0f, band.anchoredPosition.y);
        }

        /// <summary>Place the marker. <paramref name="m"/> is −1 (left end) … +1 (right end).</summary>
        public void SetMarker(float m)
        {
            if (_marker == null) return;
            _marker.anchoredPosition =
                new Vector2(Mathf.Clamp(m, -1f, 1f) * HalfTravelPx, _marker.anchoredPosition.y);
        }

        public override void HideImmediate()
        {
            base.HideImmediate();
            SetMarker(0f);
        }

        /// <summary>EditMode wiring seam — the same five rects the builder assigns. Without it a
        /// scene-less fixture drives a view whose bands are null, and every band assertion passes
        /// vacuously against 0 (which is exactly how the power-shrink tests first "failed").</summary>
        public void ConfigureForTests(RectTransform track, RectTransform bandGood,
                                      RectTransform bandJust, RectTransform centrePip,
                                      RectTransform marker)
        {
            _track = track; _bandGood = bandGood; _bandJust = bandJust;
            _centrePip = centrePip; _marker = marker;
        }
    }
}
