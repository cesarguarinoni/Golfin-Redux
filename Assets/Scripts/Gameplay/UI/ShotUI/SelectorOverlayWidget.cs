using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// The club / ball selector stack, as a 4-slot vertical carousel (selector_carousel).
    ///
    /// <para>THE MODEL. <see cref="ScrollPosition"/> is a continuous position measured in ITEMS.
    /// <c>round(scroll)</c> is the VIRTUAL index sitting in the focus slot — the bottom slot,
    /// nearest the trigger button, the one wearing the halo — and the item it shows is
    /// <c>Mod(round(scroll), N)</c>. Six pool cards (four visible plus one buffer above and one
    /// below) are rebound as the ring turns; nothing is instantiated or destroyed after the pool
    /// is built, which is the whole reason this replaced the old <c>Populate()</c>.</para>
    ///
    /// <para>WHATEVER RESTS IN THE FOCUS SLOT IS SELECTED, and the commit happens at snap START,
    /// not on settle, so the trigger button relabels while the card is still travelling.</para>
    ///
    /// <para>HOLD MODE IS UNCHANGED. <see cref="SelectorDragRouter"/> was not touched:
    /// <see cref="UpdateHoldHover"/>, <see cref="EvaluateRelease"/> and
    /// <see cref="CommitHighlighted"/> still work over <c>_cards</c> / <c>_cardRts</c> — which now
    /// hold the VISIBLE pool cards, rebuilt at the end of every <see cref="Layout"/> pass instead
    /// of once per repopulate. Finger-slide scrolling (<see cref="SelectorCarouselDrag"/>) is a
    /// modal-mode gesture only.</para>
    ///
    /// <para>The snap tween is a local coroutine rather than <c>Golfin.UI.Polish.UiMotion</c>:
    /// that class lives in Assembly-CSharp, which this asmdef does not reference (SPEC
    /// § Architecture context). It copies the three load-bearing properties — unscaled time,
    /// interruption-safe, settled on disable — and the same ease-out cubic.</para>
    /// </summary>
    public class SelectorOverlayWidget : MonoBehaviour
    {
        public enum Kind { Club, Ball }

        public enum ReleaseResult { OnCard, OnArrow, Outside }

        // ── Inspector references ───────────────────────────────────────────────
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _cardsViewport;
        [SerializeField] private RectTransform _focusHalo;
        [SerializeField] private SelectorCarouselDrag _carouselDrag;
        [SerializeField] private GameObject    _cardPrefab;
        [SerializeField] private RectTransform _arrowUpContainer;
        [SerializeField] private RectTransform _arrowDownContainer;
        [SerializeField] private Button        _arrowUp;
        [SerializeField] private Button        _arrowDown;
        [SerializeField] private OutsideClickCatcher _outsideClickCatcher;

        // ── Carousel feel (every one of these is Inspector-tunable — see the report) ──
        [Tooltip("Slot pitch in canvas px: card height 240 + gap 34.")]
        [SerializeField] private float _slotPitch      = 274f;
        [Tooltip("Cards visible in the viewport. The pool is this + 2 (one buffer each side).")]
        [SerializeField] private int   _visibleSlots   = 4;
        [Tooltip("Px between the viewport's bottom edge and the focus slot's bottom edge. The same " +
                 "margin exists at the top; together they are what lets the halo glow overhang.")]
        [SerializeField] private float _viewportMargin = 28f;
        [SerializeField] private float _snapBaseDur    = 0.12f;
        [SerializeField] private float _snapPerItemDur = 0.06f;
        [SerializeField] private float _snapMinDur     = 0.15f;
        [SerializeField] private float _snapMaxDur     = 0.35f;
        [Tooltip("Seconds of release velocity projected forward to pick the fling target.")]
        [SerializeField] private float _flingLookaheadSec = 0.12f;
        [Tooltip("Hard cap on how many items one fling may advance.")]
        [SerializeField] private int   _flingMaxItems  = 3;
        [SerializeField] private float _haloBumpPeak   = 1.06f;
        [SerializeField] private float _haloBumpDur    = 0.10f;
        [Tooltip("How far past the ends a non-wrapping stack (N < 5) may be dragged, in items.")]
        [SerializeField] private float _rubberBandItems = 0.35f;
        [Tooltip("Chevron / hold-mode auto-scroll step duration. Matches SelectorDragRouter's " +
                 "_arrowRepeatInterval so a held arrow reads as one continuous glide.")]
        [SerializeField] private float _arrowStepDur   = 0.15f;

        // Position config (set by builder; pivot=(1,0) for clubs, (0,0) for balls)
        [SerializeField] private Vector2 _anchoredPositionForClub = new Vector2(-58f, 96f);
        [SerializeField] private Vector2 _anchoredPositionForBall = new Vector2( 58f, 96f);

        /// <summary>
        /// The canvas y the FOCUS SLOT's bottom edge must sit at — the shared bottom baseline
        /// (shot_view_layout D2 / §3.3 step 4), which is also where the trigger button's bottom
        /// edge sits.
        ///
        /// <para>The x stays whatever the builder authored — only the baseline is shared, and the
        /// two overlays sit at different horizontal offsets.</para>
        ///
        /// <para>NOTE the semantics: this is NOT the root's y. The root sits
        /// <see cref="FocusSlotOffsetFromRootBottom"/> BELOW it, because the stack puts the down
        /// chevron, the VLG gap and the viewport's bottom margin underneath the focus slot. Storing
        /// the baseline and deriving the root position at open time is what makes the focus card
        /// line up with its trigger button in EVERY frame — authored and runtime — instead of only
        /// the authored one. Before selector_carousel this class stored the root y directly, so at
        /// runtime the selected card floated 68 px above the DRIVER button it belonged to.</para>
        /// </summary>
        public void SetOpenBaselineY(float y)
        {
            _anchoredPositionForClub = new Vector2(_anchoredPositionForClub.x, y);
            _anchoredPositionForBall = new Vector2(_anchoredPositionForBall.x, y);
        }

        /// <summary>
        /// Distance from the overlay root's bottom edge up to the focus slot's bottom edge:
        /// the down-chevron container, the root VLG's gap above it, and the viewport's bottom
        /// margin. Read from the live components rather than hardcoded, because the club and ball
        /// overlays do NOT author the same chevron height (60 vs 73) and a constant would silently
        /// mis-place one of them.
        /// </summary>
        float FocusSlotOffsetFromRootBottom()
        {
            float arrowH = 0f;
            if (_arrowDownContainer != null)
            {
                var le = _arrowDownContainer.GetComponent<LayoutElement>();
                arrowH = (le != null && le.preferredHeight > 0f)
                    ? le.preferredHeight
                    : _arrowDownContainer.rect.height;
            }

            float gap = 0f;
            var vlg = _root != null ? _root.GetComponent<VerticalLayoutGroup>() : null;
            if (vlg != null) gap = vlg.spacing + vlg.padding.bottom;

            return arrowH + gap + _viewportMargin;
        }

        // ── Runtime state ──────────────────────────────────────────────────────
        Kind   _kind;
        bool   _isModalMode;
        int    _highlightedIndex = -1;
        float  _highlightScale   = 1.05f;

        SelectorDragRouter _router;

        readonly List<SelectorCardWidget> _pool    = new List<SelectorCardWidget>();
        readonly List<SelectorCardWidget> _cards   = new List<SelectorCardWidget>();
        readonly List<RectTransform>      _cardRts = new List<RectTransform>();
        SelectorCardWidget _highlightCard;

        float _scroll;
        float _cardHeight = 240f;
        bool  _forceRebind;
        bool  _closeOnSettle;
        int   _pendingCommitItem = -1;
        bool  _viewportErrorLogged;

        Coroutine _arrowScrollCoroutine;
        Coroutine _snapRoutine;
        Coroutine _haloRoutine;
        Func<int, bool> _selectablePredicate;

        // ── Carousel surface used by SelectorCarouselDrag ──────────────────────

        /// <summary>Continuous ring position in items. See the class remarks.</summary>
        public float ScrollPosition => _scroll;

        /// <summary>Canvas px between two slots — the drag converts finger travel with it.</summary>
        public float SlotPitch => _slotPitch;

        int  PoolSize  => Mathf.Max(1, _visibleSlots) + 2;
        int  ItemCount => _kind == Kind.Club
            ? (ClubContext.EquippedBag != null ? ClubContext.EquippedBag.Count : 0)
            : (BallContext.OwnedBalls  != null ? BallContext.OwnedBalls.Count  : 0);

        /// <summary>
        /// The ring only turns at 5+ items. At 4 or fewer the four slots already show everything,
        /// and a ring that short would render the same card twice in one viewport.
        /// </summary>
        bool Wrap => ItemCount >= _visibleSlots + 1;

        int SelectedItemIndex => _kind == Kind.Club ? ClubContext.SelectedIndex : BallContext.SelectedIndex;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void OnEnable()
        {
            if (_outsideClickCatcher != null)
                _outsideClickCatcher.OnOutsideClick = HandleOutsideClick;

            ClubContext.OnBagChanged      += HandleClubDataChanged;
            ClubContext.OnSelectedChanged += HandleClubDataChanged;
            BallContext.OnBagChanged      += HandleBallDataChanged;
            BallContext.OnSelectedChanged += HandleBallDataChanged;

            WireArrows();
        }

        void OnDisable()
        {
            ClubContext.OnBagChanged      -= HandleClubDataChanged;
            ClubContext.OnSelectedChanged -= HandleClubDataChanged;
            BallContext.OnBagChanged      -= HandleBallDataChanged;
            BallContext.OnSelectedChanged -= HandleBallDataChanged;

            CancelSnap();
            StopHaloBump();
            StopArrowScroll();
            // Unity kills coroutines on disable, so settle by hand — the overlay must be at rest
            // the next time it is shown, not parked mid-tween.
            _scroll        = Mathf.Round(_scroll);
            _closeOnSettle = false;
        }

        void WireArrows()
        {
            if (_arrowUp != null)
            {
                _arrowUp.onClick.RemoveAllListeners();
                _arrowUp.onClick.AddListener(ScrollUp);
            }
            if (_arrowDown != null)
            {
                _arrowDown.onClick.RemoveAllListeners();
                _arrowDown.onClick.AddListener(ScrollDown);
            }
        }

        // ── Open / Close (legacy path — kept for ClubButtonWidget / BallButtonWidget direct calls) ──

        public void Open(Kind kind)
        {
            _kind        = kind;
            _isModalMode = true;   // legacy direct open is always modal
            _router      = null;

            gameObject.SetActive(true);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(true);

            PositionRoot(kind);
            OpenCarousel(dragEnabled: true);
            WireArrows();
        }

        public void Close()
        {
            ClearHighlight();
            CancelSnap();
            StopHaloBump();
            if (_carouselDrag != null) _carouselDrag.enabled = false;
            _closeOnSettle = false;
            gameObject.SetActive(false);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(false);
            StopArrowScroll();
        }

        // ── Router-facing API ──────────────────────────────────────────────────

        /// <summary>Called by SelectorDragRouter when the user presses the trigger.</summary>
        public void OpenFromRouter(SelectorDragRouter router, float highlightScale = 1.05f)
        {
            _router         = router;
            _highlightScale = highlightScale;
            _isModalMode    = false;

            // Determine kind from pivot/anchor that builder set: pivot.x==1 → Club, 0 → Ball
            _kind = (_root != null && _root.pivot.x >= 0.5f) ? Kind.Club : Kind.Ball;

            gameObject.SetActive(true);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(false); // modal mode enables this
            PositionRoot(_kind);
            OpenCarousel(dragEnabled: false);   // hold mode: the trigger owns the pointer
            WireArrows();
        }

        public void CloseFromRouter()
        {
            Close();
            _router = null;
        }

        public void EnterModalMode()
        {
            _isModalMode = true;
            ClearHighlight();
            if (_carouselDrag != null) _carouselDrag.enabled = true;
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(true);
        }

        /// <summary>Common open work: build the pool once, park the ring on the live selection.</summary>
        void OpenCarousel(bool dragEnabled)
        {
            CancelSnap();
            StopHaloBump();
            _closeOnSettle     = false;
            _pendingCommitItem = -1;
            EnsurePool();

            int n = ItemCount;
            _scroll      = n > 0 ? Mathf.Clamp(SelectedItemIndex, 0, n - 1) : 0f;
            _forceRebind = true;
            if (_focusHalo != null) _focusHalo.localScale = Vector3.one;

            // The viewport's rect is still zero on the frame the overlay is activated, and
            // Layout() needs its height to decide which cards are visible to hold mode. Play mode
            // only: in the Editor the capture helpers do their own rebuild, and doing it here
            // would dirty the scene just by opening the overlay.
            if (Application.isPlaying && _root != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            Layout();

            if (_carouselDrag != null) _carouselDrag.enabled = dragEnabled;
        }

        // ── Card pool ─────────────────────────────────────────────────────────

        void EnsurePool()
        {
            if (_cardsViewport == null || _cardPrefab == null)
            {
                if (!_viewportErrorLogged)
                {
                    _viewportErrorLogged = true;
                    Debug.LogError($"[SelectorOverlayWidget] {name}: _cardsViewport / _cardPrefab not wired. " +
                                   "Re-run GOLFIN/Build/Build Action Buttons (8.5).", this);
                }
                return;
            }
            if (_pool.Count == PoolSize) return;

            // Clear anything the viewport is already holding (a pool from an older build, or the
            // per-item cards the pre-carousel Populate() left behind in a stale scene).
            for (int i = _cardsViewport.childCount - 1; i >= 0; i--)
            {
                var child = _cardsViewport.GetChild(i);
                if (_focusHalo != null && child == _focusHalo) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else                       DestroyImmediate(child.gameObject);
            }
            _pool.Clear();
            _cards.Clear();
            _cardRts.Clear();
            _highlightCard    = null;
            _highlightedIndex = -1;

            for (int i = 0; i < PoolSize; i++)
            {
                var go = Instantiate(_cardPrefab, _cardsViewport);
                go.name = "Card_" + i;
                go.SetActive(false);
                var card = go.GetComponent<SelectorCardWidget>();
                if (card == null) { DestroyImmediate(go); continue; }

                // The prefab is authored 145x240, the same width as the viewport — keep its own
                // size rather than reading the viewport's rect, which is still 0 on the frame the
                // overlay is first activated.
                var rt = card.Rt;
                rt.anchorMin = rt.anchorMax = Vector2.zero;   // bottom-left of the viewport
                rt.pivot     = Vector2.zero;
                _cardHeight  = rt.sizeDelta.y;
                card.BoundItem = -1;
                _pool.Add(card);
            }

            // ONE tap Action per pool card, created here and reused on every rebind. The card
            // reads its own BoundItem, so no closure captures an index and Layout() stays
            // allocation-free once the pool is warm.
            _tapActions = new Action[_pool.Count];
            for (int i = 0; i < _pool.Count; i++)
            {
                var captured = _pool[i];
                _tapActions[i] = () => HandleCardTapped(captured);
            }
        }

        Action[] _tapActions = Array.Empty<Action>();

        // ── Layout ────────────────────────────────────────────────────────────

        /// <summary>
        /// Place and rebind the six pool cards for the current <see cref="_scroll"/>, then rebuild
        /// the visible-card lists hold mode reads. Called on open, once per frame while dragging or
        /// snapping, and after any data change.
        /// </summary>
        void Layout()
        {
            if (_pool.Count == 0) EnsurePool();
            if (_pool.Count == 0) return;

            int n = ItemCount;
            _cards.Clear();
            _cardRts.Clear();

            if (n <= 0)
            {
                for (int i = 0; i < _pool.Count; i++)
                    if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
                ResolveHighlightAfterLayout();
                _forceRebind = false;
                return;
            }

            bool  wrap   = Wrap;
            int   baseV  = Mathf.FloorToInt(_scroll);
            float height = _cardsViewport != null ? _cardsViewport.rect.height : 0f;
            // A rect that has not been laid out yet would report 0 and leave hold mode with no
            // hoverable cards at all. Fall back to the height the builder authored.
            if (height <= 1f)
                height = _visibleSlots * _slotPitch - (_slotPitch - _cardHeight) + 2f * _viewportMargin;

            for (int j = -1; j <= _visibleSlots; j++)
            {
                if (j + 1 >= _pool.Count) break;   // a short pool means a broken card prefab
                var card = _pool[j + 1];
                int v    = baseV + j;
                bool show = wrap || (v >= 0 && v < n);

                if (card.gameObject.activeSelf != show) card.gameObject.SetActive(show);
                if (!show) continue;

                int item = SelectorCarouselMath.Mod(v, n);
                BindCard(card, j + 1, item);

                float y = SelectorCarouselMath.SlotY(j, _scroll, _slotPitch, _viewportMargin);
                card.Rt.anchoredPosition = new Vector2(0f, y);

                // Hold mode only ever hovers, releases on, or commits a card the player can
                // actually see: the buffer cards are clipped by the RectMask2D, and a clipped
                // card must behave as if it were not there.
                float centreY = y + _cardHeight * 0.5f;
                if (centreY >= 0f && centreY <= height)
                {
                    _cards.Add(card);
                    _cardRts.Add(card.Rt);
                }
            }

            ResolveHighlightAfterLayout();
            _forceRebind = false;
        }

        /// <summary>
        /// Rebind a pool card to <paramref name="item"/>. The text/sprite write only happens when
        /// the card actually changed item (or the data under it did) — <c>SetClub</c> builds an
        /// interpolated string, and doing that six times a frame during a drag is exactly the
        /// per-frame garbage this task exists to remove.
        /// </summary>
        void BindCard(SelectorCardWidget card, int poolIndex, int item)
        {
            bool rebind = _forceRebind || card.BoundItem != item;
            if (_kind == Kind.Club)
            {
                var bag = ClubContext.EquippedBag;
                if (bag == null || item < 0 || item >= bag.Count) return;
                var entry = bag[item];
                if (rebind)
                {
                    card.BoundItem = item;
                    card.SetClub(entry, _tapActions[poolIndex]);
                }
                // K11: gated-out clubs render greyed + non-interactive (not hidden). Re-applied
                // every pass because putter mode can flip under a card that never changed item.
                card.SetSelectable(IsClubSelectable(entry));
            }
            else
            {
                var balls = BallContext.OwnedBalls;
                if (balls == null || item < 0 || item >= balls.Count) return;
                if (rebind)
                {
                    card.BoundItem = item;
                    card.SetBall(balls[item], _tapActions[poolIndex]);
                    card.SetSelectable(true);
                }
            }
        }

        void ResolveHighlightAfterLayout()
        {
            if (_highlightCard == null) { _highlightedIndex = -1; return; }
            _highlightedIndex = -1;
            for (int i = 0; i < _cards.Count; i++)
                if (ReferenceEquals(_cards[i], _highlightCard)) { _highlightedIndex = i; break; }
            if (_highlightedIndex < 0)
            {
                _highlightCard.SetHighlight(false);
                _highlightCard = null;
            }
        }

        // ── Scroll / snap / tween ─────────────────────────────────────────────

        /// <summary>Drag-time scroll write. Applies the rubber-band at the ends of a short stack.</summary>
        public void SetScroll(float value)
        {
            int n = ItemCount;
            if (n <= 0) { _scroll = 0f; Layout(); return; }

            if (!Wrap)
            {
                float max = n - 1;
                if (value < 0f)
                    value = -Mathf.Min(_rubberBandItems, -value * 0.35f);
                else if (value > max)
                    value = max + Mathf.Min(_rubberBandItems, (value - max) * 0.35f);
            }
            _scroll = value;
            Layout();
        }

        /// <summary>Stop a snap in flight WITHOUT jumping — the drag continues from where the
        /// tween had reached. The selection it already committed stands.</summary>
        public void CancelSnap()
        {
            if (_snapRoutine != null) { StopCoroutine(_snapRoutine); _snapRoutine = null; }
            _closeOnSettle = false;   // the player took the wheel again — stay open
        }

        /// <summary>Called by <see cref="SelectorCarouselDrag"/> on release.</summary>
        public void EndDragSnap(float velocityItemsPerSec)
        {
            int n = ItemCount;
            if (n <= 0) return;
            int fallback = Mathf.RoundToInt(_scroll);
            int target = SelectorCarouselMath.ResolveSnapTarget(
                _scroll, velocityItemsPerSec, _flingLookaheadSec, _flingMaxItems,
                n, Wrap, SelectablePredicate, fallback);
            SnapTo(target);
        }

        Func<int, bool> SelectablePredicate =>
            _selectablePredicate ?? (_selectablePredicate = IsItemSelectable);

        bool IsItemSelectable(int item)
        {
            if (_kind != Kind.Club) return true;
            var bag = ClubContext.EquippedBag;
            if (bag == null || item < 0 || item >= bag.Count) return false;
            return IsClubSelectable(bag[item]);
        }

        /// <summary>
        /// Glide the ring to <paramref name="targetVirtual"/>. The selection is committed HERE,
        /// at snap start, so the trigger button relabels while the card is still moving.
        /// </summary>
        public void SnapTo(int targetVirtual, float durOverride = -1f, bool closeOnSettle = false)
        {
            int n = ItemCount;
            if (n <= 0) return;

            CancelSnap();
            CommitSelection(SelectorCarouselMath.Mod(targetVirtual, n));
            _closeOnSettle = closeOnSettle;

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                _scroll = targetVirtual;
                NormaliseScroll();
                Layout();
                if (_closeOnSettle) { _closeOnSettle = false; CloseAfterCommit(); }
                return;
            }
            _snapRoutine = StartCoroutine(SnapRoutine(targetVirtual, durOverride));
        }

        IEnumerator SnapRoutine(int target, float durOverride)
        {
            float start = _scroll;
            float dur   = durOverride > 0f
                ? durOverride
                : SelectorCarouselMath.SnapDuration(target - start, _snapBaseDur, _snapPerItemDur,
                                                    _snapMinDur, _snapMaxDur);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _scroll = Mathf.Lerp(start, target, SelectorCarouselMath.EaseOutCubic(t / dur));
                Layout();
                yield return null;
            }

            _scroll = target;
            NormaliseScroll();
            Layout();
            _snapRoutine = null;
            HaloBump();

            if (_closeOnSettle) { _closeOnSettle = false; CloseAfterCommit(); }
        }

        /// <summary>Re-centre the ring coordinate into [0, n) after every settle so the float
        /// cannot creep out to a range where it loses precision over a long session. Visually a
        /// no-op: every slot's item is taken modulo n.</summary>
        void NormaliseScroll()
        {
            int n = ItemCount;
            if (n <= 0) { _scroll = 0f; return; }
            _scroll = Wrap
                ? SelectorCarouselMath.Mod(Mathf.RoundToInt(_scroll), n)
                : Mathf.Clamp(Mathf.Round(_scroll), 0f, n - 1);
        }

        void CommitSelection(int item)
        {
            int n = ItemCount;
            if (item < 0 || item >= n) return;

            if (_kind == Kind.Club)
            {
                var entry = ClubContext.EquippedBag[item];
                if (!IsClubSelectable(entry)) return;   // K11, last line of defence
                _pendingCommitItem = item;
                ClubContext.RequestSelection(item);
                ClubSelectionBroadcast.Raise(entry.LabClubIndex);
            }
            else
            {
                _pendingCommitItem = item;
                BallContext.RequestSelection(item);
            }
        }

        void CloseAfterCommit()
        {
            if (_router != null) _router.OnModalCommit();
            else                 Close();
        }

        void HandleCardTapped(SelectorCardWidget card)
        {
            int n = ItemCount;
            if (n <= 0 || card == null) return;
            if (!IsItemSelectable(card.BoundItem)) return;   // K11

            int focusItem = SelectorCarouselMath.Mod(Mathf.RoundToInt(_scroll), n);
            if (card.BoundItem == focusItem)
            {
                CommitSelection(card.BoundItem);
                CloseAfterCommit();
                return;
            }

            // A visible non-focus card: glide it into focus, then close. Its virtual index is its
            // pool slot's, which is at most 3 slots away — so "shortest way" needs no search.
            int poolIndex = -1;
            for (int i = 0; i < _pool.Count; i++)
                if (ReferenceEquals(_pool[i], card)) { poolIndex = i; break; }
            if (poolIndex < 0) return;

            int j = poolIndex - 1;   // pool slot 0 is the buffer BELOW the focus slot
            SnapTo(Mathf.FloorToInt(_scroll) + j, closeOnSettle: true);
        }

        // ── Halo ──────────────────────────────────────────────────────────────

        void HaloBump()
        {
            if (_focusHalo == null) return;
            if (!Application.isPlaying || !isActiveAndEnabled) { _focusHalo.localScale = Vector3.one; return; }
            StopHaloBump();
            _haloRoutine = StartCoroutine(HaloBumpRoutine());
        }

        void StopHaloBump()
        {
            if (_haloRoutine != null) { StopCoroutine(_haloRoutine); _haloRoutine = null; }
            if (_focusHalo != null) _focusHalo.localScale = Vector3.one;   // interruption-safe settle
        }

        IEnumerator HaloBumpRoutine()
        {
            float half = Mathf.Max(0.001f, _haloBumpDur * 0.5f);
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(1f, _haloBumpPeak, Mathf.Clamp01(t / half));
                _focusHalo.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(_haloBumpPeak, 1f, Mathf.Clamp01(t / half));
                _focusHalo.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _focusHalo.localScale = Vector3.one;
            _haloRoutine = null;
        }

        // ── Data changes while open ───────────────────────────────────────────

        // Split so a ball-selection change cannot reset the CLUB ring (and vice versa): the two
        // overlays are separate instances of this widget but share both static buses.
        void HandleClubDataChanged() { if (_kind == Kind.Club) HandleDataChanged(); }
        void HandleBallDataChanged() { if (_kind == Kind.Ball) HandleDataChanged(); }

        void HandleDataChanged()
        {
            if (!isActiveAndEnabled) return;
            int n = ItemCount;
            if (n <= 0) { Layout(); return; }

            // Our own commit echoing back must not yank the ring off its tween — it is ALREADY
            // travelling to exactly that item. Note this cannot be conditioned on _snapRoutine:
            // RequestSelection reaches the populator synchronously, so the echo arrives from
            // inside SnapTo's CommitSelection, one line BEFORE the coroutine handle is assigned.
            // _pendingCommitItem is cleared only by the next open or a genuinely different
            // external selection, so a late (async) echo is ignored too.
            if (_pendingCommitItem >= 0 && _pendingCommitItem == SelectedItemIndex) return;

            CancelSnap();
            _pendingCommitItem = -1;
            _scroll      = Mathf.Clamp(SelectedItemIndex, 0, n - 1);
            _forceRebind = true;
            Layout();
        }

        // ── Hold-mode hover update ─────────────────────────────────────────────

        /// <summary>
        /// Called by SelectorDragRouter.OnDrag. Determines which card (if any) the screen-space
        /// pointer position falls inside, highlights it, and triggers arrow auto-scroll.
        /// </summary>
        public void UpdateHoldHover(Vector2 screenPos, float arrowDelay, float arrowInterval)
        {
            // Check arrow zones first
            if (IsOverRect(_arrowUpContainer, screenPos))
            {
                ClearHighlight();
                StartArrowScroll(isUp: true, arrowDelay, arrowInterval);
                return;
            }
            if (IsOverRect(_arrowDownContainer, screenPos))
            {
                ClearHighlight();
                StartArrowScroll(isUp: false, arrowDelay, arrowInterval);
                return;
            }

            StopArrowScroll();

            // Check cards
            int found = -1;
            for (int i = 0; i < _cardRts.Count; i++)
            {
                if (IsOverRect(_cardRts[i], screenPos))
                {
                    found = i;
                    break;
                }
            }
            // K11: a gated-out card never highlights — hovering it must look like hovering nothing,
            // which also keeps CommitHighlighted() from ever having a disabled card to commit.
            if (found >= 0 && !_cards[found].IsSelectable) found = -1;
            SetHighlightAt(found);
        }

        // ── Release evaluation ────────────────────────────────────────────────

        public ReleaseResult EvaluateRelease(Vector2 screenPos)
        {
            if (_arrowUpContainer != null && IsOverRect(_arrowUpContainer, screenPos))
                return ReleaseResult.OnArrow;
            if (_arrowDownContainer != null && IsOverRect(_arrowDownContainer, screenPos))
                return ReleaseResult.OnArrow;

            for (int i = 0; i < _cardRts.Count; i++)
                if (IsOverRect(_cardRts[i], screenPos))
                    // K11: releasing over a gated-out card reads as releasing over nothing,
                    // so the router takes its Outside branch instead of committing.
                    return _cards[i].IsSelectable ? ReleaseResult.OnCard : ReleaseResult.Outside;

            return ReleaseResult.Outside;
        }

        /// <summary>Commit whichever card is highlighted. No-op if none highlighted.</summary>
        public void CommitHighlighted()
        {
            if (_highlightedIndex < 0 || _highlightedIndex >= _cards.Count) return;
            if (!_cards[_highlightedIndex].IsSelectable) return;   // K11
            // Hold-mode release commits and the router closes the overlay immediately — there is
            // no card left on screen to animate, so this deliberately does NOT go through SnapTo.
            _cards[_highlightedIndex].InvokeSelection();
        }

        // ── Highlight ─────────────────────────────────────────────────────────

        public void SetHighlightAt(int idx)
        {
            var target = (idx >= 0 && idx < _cards.Count) ? _cards[idx] : null;
            for (int i = 0; i < _pool.Count; i++)
                _pool[i].SetHighlight(ReferenceEquals(_pool[i], target), _highlightScale);
            _highlightedIndex = idx;
            _highlightCard    = target;
        }

        void ClearHighlight() => SetHighlightAt(-1);

        // ── Arrow scroll ──────────────────────────────────────────────────────

        public void ScrollUp()   => Scroll(+1);
        public void ScrollDown() => Scroll(-1);

        /// <summary>
        /// Move the selection by <paramref name="delta"/>. Returns true if the selection
        /// actually moved — false means there is nothing left to land on, which is how
        /// ArrowScrollRoutine knows to stop instead of spinning.
        ///
        /// <para>Mapping is unchanged from before the carousel: ScrollUp (top chevron) = +1.</para>
        /// </summary>
        bool Scroll(int delta)
        {
            int n = ItemCount;
            if (n <= 0 || delta == 0) return false;

            int here     = Mathf.RoundToInt(_scroll);
            int hereItem = SelectorCarouselMath.Mod(here, n);

            // K11: step OVER gated-out clubs rather than clamping onto them — off the green the
            // arrows skip the putter, and on the green (putter-only) there is no eligible
            // neighbour in either direction, so the walk comes back to the putter and this no-ops.
            // Velocity carries the sign only: with lookahead 0 it just picks the walk direction.
            int target = SelectorCarouselMath.ResolveSnapTarget(
                here + delta, delta, 0f, 0, n, Wrap, SelectablePredicate, here);

            if (SelectorCarouselMath.Mod(target, n) == hereItem) return false;
            SnapTo(target, _arrowStepDur);
            return true;
        }

        /// <summary>The one eligibility rule, shared by Layout and Scroll (see §2f).</summary>
        static bool IsClubSelectable(ClubEntry entry) =>
            entry != null && ClubSelectionBroadcast.IsSelectable(
                entry.LabClubIndex,
                ClubSelectionBroadcast.PutterLabClubIndex,
                ClubSelectionBroadcast.InPutterMode);

        void StartArrowScroll(bool isUp, float delay, float interval)
        {
            if (_arrowScrollCoroutine != null) return;
            _arrowScrollCoroutine = StartCoroutine(ArrowScrollRoutine(isUp, delay, interval));
        }

        void StopArrowScroll()
        {
            if (_arrowScrollCoroutine != null)
            {
                StopCoroutine(_arrowScrollCoroutine);
                _arrowScrollCoroutine = null;
            }
        }

        IEnumerator ArrowScrollRoutine(bool isUp, float delay, float interval)
        {
            yield return new WaitForSecondsRealtime(delay);
            while (true)
            {
                if (!Scroll(isUp ? +1 : -1))
                {
                    // K11: nothing eligible left in this direction (on the green, that is
                    // immediately). Exit rather than spin — and null the handle first, or
                    // StartArrowScroll's non-null guard would block every later hold-scroll.
                    _arrowScrollCoroutine = null;
                    yield break;
                }
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        // ── Outside-click handler ─────────────────────────────────────────────

        void HandleOutsideClick()
        {
            if (_router != null)
                _router.OnModalCancel();
            else
                Close();
        }

        // ── Position ──────────────────────────────────────────────────────────

        void PositionRoot(Kind kind)
        {
            if (_root == null) return;
            Vector2 baseline = kind == Kind.Club ? _anchoredPositionForClub : _anchoredPositionForBall;
            float   x        = kind == Kind.Club ? 1f : 0f;

            _root.anchorMin = _root.anchorMax = new Vector2(x, 0f);
            _root.pivot     = new Vector2(x, 0f);
            // baseline.y is where the FOCUS SLOT must land; drop the root by everything that
            // stacks beneath it so the focus card lines up with its trigger button.
            _root.anchoredPosition = new Vector2(baseline.x, baseline.y - FocusSlotOffsetFromRootBottom());
        }

        // ── Rect hit test ─────────────────────────────────────────────────────

        bool IsOverRect(RectTransform rt, Vector2 screenPos)
        {
            if (rt == null) return false;
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
        }
    }
}
