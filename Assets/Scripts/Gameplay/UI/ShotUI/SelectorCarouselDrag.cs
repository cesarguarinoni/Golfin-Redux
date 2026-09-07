using UnityEngine;
using UnityEngine.EventSystems;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Finger-slide scrolling for the selector carousel (selector_carousel §4). Lives on the
    /// selector's <c>CardsContainer</c> viewport, beside its <c>RectMask2D</c> and the
    /// transparent hit-area <c>Image</c>.
    ///
    /// <para>MODAL MODE ONLY. <see cref="SelectorOverlayWidget.EnterModalMode"/> enables this;
    /// every open/close path disables it. In hold mode the pointer that opened the overlay is
    /// still down on the trigger button, so the event system would never route a drag here
    /// anyway — the flag is belt-and-braces so a second finger cannot scroll the stack out from
    /// under a hold-mode hover.</para>
    ///
    /// <para>Tap vs slide needs no threshold of its own: once <c>OnBeginDrag</c> fires the input
    /// module has already cleared <c>eligibleForClick</c>, so a slide never also fires a card's
    /// <c>Button.onClick</c>, and a clean tap never reaches these handlers.</para>
    /// </summary>
    public class SelectorCarouselDrag : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private SelectorOverlayWidget _overlay;
        [SerializeField] private RectTransform         _viewport;

        [Tooltip("Seconds of drag history used to measure the release velocity.")]
        [SerializeField] private float _velocityWindowSec = 0.1f;

        const int SampleCount = 8;

        readonly float[] _sampleTime   = new float[SampleCount];
        readonly float[] _sampleScroll = new float[SampleCount];
        int   _sampleHead;      // next write slot
        int   _sampleFilled;
        float _scrollAtStart;
        float _startLocalY;
        bool  _dragging;

        void Awake()
        {
            if (_overlay  == null) _overlay  = GetComponentInParent<SelectorOverlayWidget>();
            if (_viewport == null) _viewport = transform as RectTransform;
        }

        void OnDisable() => _dragging = false;

        public void OnBeginDrag(PointerEventData ev)
        {
            if (_overlay == null || _viewport == null) return;
            if (!TryLocalY(ev, out float localY)) return;

            _overlay.CancelSnap();
            _scrollAtStart = _overlay.ScrollPosition;
            _startLocalY   = localY;
            _sampleHead    = 0;
            _sampleFilled  = 0;
            _dragging      = true;
            PushSample(_scrollAtStart);
        }

        public void OnDrag(PointerEventData ev)
        {
            if (!_dragging || _overlay == null) return;
            if (!TryLocalY(ev, out float localY)) return;

            float pitch = Mathf.Max(1f, _overlay.SlotPitch);
            // Finger DOWN (localY falls) increases scroll — the card above comes down into focus.
            float scroll = _scrollAtStart + (_startLocalY - localY) / pitch;
            _overlay.SetScroll(scroll);
            PushSample(_overlay.ScrollPosition);
        }

        public void OnEndDrag(PointerEventData ev)
        {
            if (!_dragging || _overlay == null) return;
            _dragging = false;
            _overlay.EndDragSnap(MeasureVelocity());
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        bool TryLocalY(PointerEventData ev, out float localY)
        {
            localY = 0f;
            var canvas = _viewport.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, ev.position, cam, out Vector2 local))
                return false;
            localY = local.y;
            return true;
        }

        void PushSample(float scroll)
        {
            _sampleTime[_sampleHead]   = Time.unscaledTime;
            _sampleScroll[_sampleHead] = scroll;
            _sampleHead = (_sampleHead + 1) % SampleCount;
            if (_sampleFilled < SampleCount) _sampleFilled++;
        }

        /// <summary>Items/second over the last <c>_velocityWindowSec</c>; 0 with fewer than 2 samples.</summary>
        float MeasureVelocity()
        {
            if (_sampleFilled < 2) return 0f;

            int last  = (_sampleHead - 1 + SampleCount) % SampleCount;
            float now = _sampleTime[last];

            // Walk back to the oldest sample still inside the window — always at least one step,
            // so a two-sample flick still yields a velocity instead of a divide-by-nothing.
            int oldest = last;
            for (int k = 1; k < _sampleFilled; k++)
            {
                oldest = (_sampleHead - 1 - k + 2 * SampleCount) % SampleCount;
                if (now - _sampleTime[oldest] >= _velocityWindowSec) break;
            }

            float dt = now - _sampleTime[oldest];
            if (dt <= 1e-4f) return 0f;
            return (_sampleScroll[last] - _sampleScroll[oldest]) / dt;
        }
    }
}
