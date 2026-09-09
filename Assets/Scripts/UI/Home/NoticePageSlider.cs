// ─────────────────────────────────────────────────────────────────────────────
// notice_panel_slide §2 — the Home notice panel pages like the mode carousel.
//
// WHAT MOVES, and it is the whole decision (SPEC D1): the BOX, not the text.
// Background, title, divider and body travel together, so a page change reads as
// one card leaving and the next arriving rather than three labels re-lettering
// themselves in place. That is why this is two full page boxes under one static
// root and not a RectMask2D over a single set of TMP fields.
//
// THE ROOT DOES NOT MOVE. `NoticePanel` keeps its rect exactly as authored
// (0, -361, 1018x352) because DailyMissionPillController.ComputeTargetY reads
// that rect's anchoredPosition.y and height every time the notice repaints — the
// pill below would follow the box off-screen if the root were the thing sliding.
// Only PageA/PageB anchoredPosition.x is ever written; y is never touched.
//
// TWO BOXES, NOT N. The back box is a scratchpad: it is painted with whichever
// neighbour is about to be seen (drag start, SlideTo) and parked off-canvas at
// +pageSpacing otherwise. 1170 - 509 = 661 > 585 (half the canvas), so a parked
// box is fully outside the 1170-wide canvas with no mask and nothing to clip.
// A three-page notice set needs no third box: only one neighbour is ever visible.
//
// NO SwipeDetector. The scene's old `GolfinRedux.UI.SwipeDetector` on this same
// GameObject was removed by this task — it fired NextNewsPage on release, which
// on top of this component's own commit would have advanced two pages per swipe.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Golfin.Notices;
using Golfin.UI.Polish;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Golfin.UI.Home
{
    /// <summary>
    /// Slides the Home notice box between pages: auto-cycle and Next push the current box out to
    /// the LEFT while the next enters from the RIGHT, Previous does the reverse, and a finger drag
    /// carries both boxes and snaps on release.
    /// <para>
    /// Lives on the notice panel ROOT. Both page boxes keep <c>raycastTarget = true</c> on their
    /// background <c>Image</c>; pointer events bubble from them up to this component, which is why
    /// nothing needs a raycast target of its own here.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoticePageSlider : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Architect defaults (SPEC § Decisions of record). Also the defaults of the
        /// <see cref="ResolveRelease"/> seam, so the table test states them once.</summary>
        internal const float DefaultCommitDistance = 60f;
        internal const float DefaultCommitVelocity = 800f;

        /// <summary>One page box: the moving rect plus the two labels painted into it.</summary>
        [Serializable]
        public sealed class PageBox
        {
            public RectTransform rect;
            public TextMeshProUGUI title;
            public TextMeshProUGUI body;

            public bool IsUsable => rect != null;

            public void Paint(in NoticePage page)
            {
                if (title != null) title.text = page.Title;
                if (body != null) body.text = page.Body;
            }

            public float X
            {
                get => rect != null ? rect.anchoredPosition.x : 0f;
                set { if (rect != null) rect.anchoredPosition = new Vector2(value, 0f); }
            }
        }

        [Header("Page boxes")]
        [SerializeField] private PageBox pageA;
        [SerializeField] private PageBox pageB;

        [Header("Motion")]
        [Tooltip("Canvas px between the two boxes. One canvas width, so the parked box is fully off-screen.")]
        [SerializeField] private float pageSpacing = 1170f;
        [SerializeField] private float snapDuration = 0.28f;

        [Header("Drag")]
        [Tooltip("Canvas px of travel past which a release commits to the neighbour.")]
        [SerializeField] private float commitDistance = DefaultCommitDistance;
        [Tooltip("Canvas px/s of release speed past which a short drag still commits (a flick).")]
        [SerializeField] private float commitVelocity = DefaultCommitVelocity;
        [Tooltip("Resistance applied to a drag with no neighbour to go to (a single notice).")]
        [SerializeField] private float rubberBand = 0.35f;
        [SerializeField] private float rubberBandMax = 80f;

        /// <summary>
        /// Raised when a DRAG committed to a neighbour: +1 next, -1 previous. Fires after the snap
        /// settles, so the box is already where the handler will believe it is.
        /// <para>Never raised by <see cref="SlideTo"/> or <see cref="SetPages"/> — the controller
        /// asked for those and already knows the index.</para>
        /// </summary>
        public event Action<int> OnDragCommitted;

        private RectTransform _root;
        private PageBox _front;
        private PageBox _back;

        private IReadOnlyList<NoticePage> _pages;
        private int _current;

        private Coroutine _motion;
        private bool _snapping;

        private bool _dragging;
        private float _dragStartX;      // canvas px, in this rect's local space
        private float _dragDelta;
        private float _lastLocalX;
        private float _velocity;        // canvas px/s
        private float _lastMoveTime;
        private int _dragSign;          // which neighbour the back box currently holds; 0 = none

        // Snap bookkeeping. Read by the two cached delegates below so a slide allocates nothing
        // beyond the coroutine itself — no closure is built per gesture, none per frame.
        private float _fromFront, _toFront, _fromBack, _toBack;
        private bool _pendingSwap;
        private int _pendingIndex;
        private int _pendingCommit;

        private Action<float> _applySnap;
        private Action _finishSnap;

        /// <summary>A finger is down or a snap is in flight. The auto-cycle countdown holds on
        /// this (SPEC D3) so the box never changes under a thumb.</summary>
        public bool IsBusy => _dragging || _snapping;

        /// <summary>Index of the page currently in the front box.</summary>
        public int Current => _current;

        private int PageCount => _pages != null ? _pages.Count : 0;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake() => EnsureInit();

        /// <summary>
        /// Resolve the boxes and the two cached delegates, once.
        /// <para>Not merely <c>Awake</c>: Unity does not order Awake between GameObjects, and
        /// <c>HomeScreenController.OnEnable</c> (on the ancestor screen object) calls
        /// <see cref="SetPages"/> as Home comes up. Every public entry point starts here so the
        /// first call — whichever it is — finds the component built.</para>
        /// </summary>
        private void EnsureInit()
        {
            if (_applySnap != null) return;
            _root = transform as RectTransform;
            _applySnap = ApplySnap;
            _finishSnap = FinishSnap;
            _front = pageA;
            _back = pageB;
            ParkAtRest();
        }

        /// <summary>
        /// Leaving Home mid-snap must not strand a box off-centre: <see cref="UiMotion.Stop"/>
        /// settles the tween AND runs the completion tail, then both boxes are pinned at rest so
        /// the next entry finds the panel exactly as authored.
        /// <para>The commit event is deliberately dropped here — the handler is being unsubscribed
        /// on the same teardown, and Home re-enters at page 0 regardless.</para>
        /// </summary>
        private void OnDisable()
        {
            _pendingCommit = 0;
            UiMotion.Stop(this, ref _motion);
            _dragging = false;
            _snapping = false;
            _dragSign = 0;
            ParkAtRest();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Replace the page set and paint <paramref name="index"/> into the front box INSTANTLY —
        /// no motion. Every repaint path lands here: a fetch that replaced the notices, a language
        /// switch, Home's own entry. A refresh that arrives mid-snap cancels it and settles at rest.
        /// </summary>
        public void SetPages(IReadOnlyList<NoticePage> pages, int index)
        {
            EnsureInit();
            _pendingCommit = 0;                       // a repaint is not a player commit
            UiMotion.Stop(this, ref _motion);
            _snapping = false;
            _dragging = false;
            _dragSign = 0;

            _pages = pages;
            int count = PageCount;
            _current = count > 0 ? Mathf.Clamp(index, 0, count - 1) : 0;

            if (count > 0 && _front != null) _front.Paint(_pages[_current]);
            ParkAtRest();
        }

        /// <summary>
        /// Animate to <paramref name="index"/>. <paramref name="direction"/> +1 sends the current
        /// box out LEFT and brings the new one in from the RIGHT (auto-cycle, Next); -1 mirrors it
        /// (Previous). Ignored while <see cref="IsBusy"/> — a drag or a snap owns the boxes.
        /// </summary>
        public void SlideTo(int index, int direction)
        {
            EnsureInit();
            int count = PageCount;
            if (count <= 0 || IsBusy || _front == null || _back == null) return;
            if (index < 0 || index >= count || index == _current) return;
            if (!_front.IsUsable || !_back.IsUsable) return;

            int dir = direction >= 0 ? 1 : -1;

            _back.Paint(_pages[index]);
            _back.X = dir * pageSpacing;

            _pendingSwap = true;
            _pendingIndex = index;
            _pendingCommit = 0;
            StartSnap(fromFront: 0f, toFront: -dir * pageSpacing,
                      fromBack: dir * pageSpacing, toBack: 0f);
        }

        // ── Drag ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            EnsureInit();
            if (_root == null || pageSpacing <= 0f) return;

            // A drag started on top of a running snap takes over from rest: settling first is what
            // keeps the two boxes' roles unambiguous (the swap has happened or it has not).
            UiMotion.Stop(this, ref _motion);
            _snapping = false;

            _dragging = true;
            _dragDelta = 0f;
            _velocity = 0f;
            _dragSign = 0;
            _dragStartX = LocalX(eventData);
            _lastLocalX = _dragStartX;
            _lastMoveTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _front == null || !_front.IsUsable) return;

            float localX = LocalX(eventData);
            _dragDelta = localX - _dragStartX;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                // Canvas px/s, measured in the SAME space as the commit distance. eventData.delta
                // is screen px and the canvas is scaled, so it would compare against the wrong
                // threshold on every device but a 1170-wide one.
                float instant = (localX - _lastLocalX) / dt;
                _velocity = Mathf.Approximately(_velocity, 0f)
                    ? instant
                    : Mathf.Lerp(_velocity, instant, 0.7f);
            }
            _lastLocalX = localX;
            _lastMoveTime = Time.unscaledTime;

            int count = PageCount;
            if (count <= 1)
            {
                // Nowhere to go: the box gives a little and springs back (SPEC § one notice).
                _front.X = Mathf.Clamp(_dragDelta * rubberBand, -rubberBandMax, rubberBandMax);
                return;
            }

            // Drag LEFT (negative) reveals the NEXT page entering from the right.
            int sign = _dragDelta < 0f ? 1 : _dragDelta > 0f ? -1 : 0;
            if (sign != 0 && sign != _dragSign) EnsureNeighbour(sign);

            _front.X = _dragDelta;
            if (_dragSign != 0 && _back != null) _back.X = _dragDelta + _dragSign * pageSpacing;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            // A finger that stopped and then lifted is not a flick, and OnDrag stops firing the
            // moment it stops — so staleness, not the last delta, is what says "no throw".
            float velocity = (Time.unscaledTime - _lastMoveTime) > 0.08f ? 0f : _velocity;

            int count = PageCount;
            int result = ResolveRelease(_dragDelta, velocity, count, commitDistance, commitVelocity);

            if (result != 0)
            {
                // A reversing flick (dragged right, thrown left) can resolve against the side the
                // back box was painted for; repaint it rather than slide the wrong notice in.
                if (_dragSign != result) EnsureNeighbour(result);

                _pendingSwap = true;
                _pendingIndex = WrapIndex(_current, result, count);
                _pendingCommit = result;
                StartSnap(_front.X, -result * pageSpacing, _back.X, 0f);
                return;
            }

            _pendingSwap = false;
            _pendingCommit = 0;
            float backRest = _dragSign != 0 ? _dragSign * pageSpacing : pageSpacing;
            StartSnap(_front.X, 0f, _back != null ? _back.X : backRest, backRest);
        }

        // ── Pure seams (EditMode tested — NoticePageSliderTests) ─────────────

        /// <summary>
        /// What a release means: +1 commit to the next page, -1 to the previous, 0 snap home.
        /// Distance OR speed is enough; a single page (or none) can never commit.
        /// </summary>
        internal static int ResolveRelease(float delta, float velocity, int pageCount,
                                           float commitDistance = DefaultCommitDistance,
                                           float commitVelocity = DefaultCommitVelocity)
        {
            if (pageCount <= 1) return 0;
            if (delta <= -commitDistance || velocity <= -commitVelocity) return 1;
            if (delta >= commitDistance || velocity >= commitVelocity) return -1;
            return 0;
        }

        /// <summary>
        /// Step the page index cyclically, matching <c>HomeScreenController.NextNewsPage</c>'s
        /// <c>% count</c>. C#'s <c>%</c> keeps the dividend's sign, so a step off the front of the
        /// list would index at -1 without the <c>+ count</c>.
        /// </summary>
        internal static int WrapIndex(int current, int step, int count)
        {
            if (count <= 0) return 0;
            return ((current + step) % count + count) % count;
        }

        // ── Internals ────────────────────────────────────────────────────────

        private float LocalX(PointerEventData eventData)
        {
            // Local point in the root rect = canvas px, whatever the device resolution or the
            // CanvasScaler's match. ModeCarouselController's screen-px delta is the thing this
            // deliberately does not copy.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _root, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return local.x;
        }

        /// <summary>Paint the back box with the neighbour on <paramref name="sign"/>'s side and
        /// park it there, tracking the drag's current offset.</summary>
        private void EnsureNeighbour(int sign)
        {
            int count = PageCount;
            if (count <= 1 || _back == null || !_back.IsUsable) { _dragSign = 0; return; }

            _dragSign = sign;
            _back.Paint(_pages[WrapIndex(_current, sign, count)]);
            _back.X = _dragDelta + sign * pageSpacing;
        }

        private void StartSnap(float fromFront, float toFront, float fromBack, float toBack)
        {
            _fromFront = fromFront;
            _toFront = toFront;
            _fromBack = fromBack;
            _toBack = toBack;
            _snapping = true;

            // Then() composes the tween's own final value with the role swap into ONE finalizer, so
            // an interrupted or disabled snap still lands both boxes at rest and still swaps.
            UiMotion.Run(this, ref _motion, UiMotion.Then(
                UiMotion.Tween(0f, 1f, snapDuration, _applySnap, Ease.OutCubic),
                _finishSnap));
        }

        private void ApplySnap(float t)
        {
            if (_front != null) _front.X = Mathf.LerpUnclamped(_fromFront, _toFront, t);
            if (_back != null) _back.X = Mathf.LerpUnclamped(_fromBack, _toBack, t);
        }

        private void FinishSnap()
        {
            _snapping = false;

            if (_pendingSwap)
            {
                PageBox swap = _front;
                _front = _back;
                _back = swap;
                _current = _pendingIndex;
                _pendingSwap = false;
            }

            _dragSign = 0;
            ParkAtRest();

            int commit = _pendingCommit;
            _pendingCommit = 0;
            if (commit != 0) OnDragCommitted?.Invoke(commit);
        }

        /// <summary>Front centred, back parked one canvas width to the right. The only resting
        /// arrangement there is.</summary>
        private void ParkAtRest()
        {
            if (_front != null) _front.X = 0f;
            if (_back != null) _back.X = pageSpacing;
        }
    }
}
