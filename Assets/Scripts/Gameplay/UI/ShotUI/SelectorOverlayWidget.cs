using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SelectorOverlayWidget : MonoBehaviour
    {
        public enum Kind { Club, Ball }

        public enum ReleaseResult { OnCard, OnArrow, Outside }

        // ── Inspector references ───────────────────────────────────────────────
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _cardsContainer;
        [SerializeField] private GameObject    _cardPrefab;
        [SerializeField] private RectTransform _arrowUpContainer;
        [SerializeField] private RectTransform _arrowDownContainer;
        [SerializeField] private Button        _arrowUp;
        [SerializeField] private Button        _arrowDown;
        [SerializeField] private OutsideClickCatcher _outsideClickCatcher;

        // Position config (set by builder; pivot=(1,0) for clubs, (0,0) for balls)
        [SerializeField] private Vector2 _anchoredPositionForClub = new Vector2(-58f, 96f);
        [SerializeField] private Vector2 _anchoredPositionForBall = new Vector2( 58f, 96f);

        // ── Runtime state ──────────────────────────────────────────────────────
        Kind   _kind;
        bool   _isModalMode;
        int    _highlightedIndex = -1;
        float  _highlightScale   = 1.05f;

        SelectorDragRouter _router;

        List<SelectorCardWidget> _cards = new List<SelectorCardWidget>();
        List<RectTransform>      _cardRts = new List<RectTransform>();

        // Arrow scroll
        Coroutine _arrowScrollCoroutine;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void OnEnable()
        {
            if (_outsideClickCatcher != null)
                _outsideClickCatcher.OnOutsideClick = HandleOutsideClick;

            WireArrows();
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
            _kind      = kind;
            _isModalMode = true;   // legacy direct open is always modal
            _router      = null;

            gameObject.SetActive(true);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(true);

            PositionRoot(kind);
            Populate();
            WireArrows();
        }

        public void Close()
        {
            ClearHighlight();
            gameObject.SetActive(false);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(false);
            if (_arrowScrollCoroutine != null) { StopCoroutine(_arrowScrollCoroutine); _arrowScrollCoroutine = null; }
        }

        // ── Router-facing API ──────────────────────────────────────────────────

        /// <summary>Called by SelectorDragRouter when the user presses the trigger.</summary>
        public void OpenFromRouter(SelectorDragRouter router, float highlightScale = 1.05f)
        {
            _router        = router;
            _highlightScale = highlightScale;
            _isModalMode   = false;

            // Determine kind from pivot/anchor that builder set: pivot.x==1 → Club, 0 → Ball
            _kind = (_root.pivot.x >= 0.5f) ? Kind.Club : Kind.Ball;

            gameObject.SetActive(true);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(false); // modal mode enables this
            PositionRoot(_kind);
            Populate();
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
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(true);
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
            _cards[_highlightedIndex].InvokeSelection();
        }

        // ── Highlight ─────────────────────────────────────────────────────────

        public void SetHighlightAt(int idx)
        {
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].SetHighlight(i == idx, i == idx ? _highlightScale : 1f);
            _highlightedIndex = idx;
        }

        void ClearHighlight() => SetHighlightAt(-1);

        // ── Arrow scroll ──────────────────────────────────────────────────────

        public void ScrollUp()   => Scroll(+1);
        public void ScrollDown() => Scroll(-1);

        /// <summary>
        /// Move the selection by <paramref name="delta"/>. Returns true if the selection
        /// actually moved — false means there is nothing left to land on, which is how
        /// ArrowScrollRoutine knows to stop instead of spinning.
        /// </summary>
        bool Scroll(int delta)
        {
            // Reorder: move selected by delta.
            // Cards are displayed bottom-to-top in inventory order.
            // The bottom card is "selected". Scrolling changes which item is selected.
            if (_kind == Kind.Club)
            {
                // K11: step OVER gated-out clubs rather than clamping onto them — off the
                // green the arrows skip the putter, and on the green (putter-only) there is
                // no eligible neighbour in either direction so the arrows no-op.
                int next = FindNextSelectableClub(ClubContext.SelectedIndex, delta);
                if (next < 0) return false;
                ClubContext.RequestSelection(next);
                ClubSelectionBroadcast.Raise(ClubContext.EquippedBag[next].LabClubIndex);
                // Repopulate to reflect new selected-at-bottom ordering
                Populate();
                return true;
            }
            else
            {
                int next = BallContext.SelectedIndex + delta;
                next = Mathf.Clamp(next, 0, BallContext.OwnedBalls.Count - 1);
                if (next == BallContext.SelectedIndex) return false;
                BallContext.RequestSelection(next);
                Populate();
                return true;
            }
        }

        /// <summary>
        /// First selectable bag index strictly beyond <paramref name="from"/> in direction
        /// <paramref name="delta"/> (±1), or -1 when the walk runs off either end.
        /// </summary>
        int FindNextSelectableClub(int from, int delta)
        {
            var bag = ClubContext.EquippedBag;
            if (bag == null || bag.Count == 0 || delta == 0) return -1;
            for (int i = from + delta; i >= 0 && i < bag.Count; i += delta)
                if (IsClubSelectable(bag[i])) return i;
            return -1;
        }

        /// <summary>The one eligibility rule, shared by Populate and Scroll (see §2f).</summary>
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
            if (kind == Kind.Club)
            {
                _root.anchorMin = _root.anchorMax = new Vector2(1f, 0f);
                _root.pivot     = new Vector2(1f, 0f);
                _root.anchoredPosition = _anchoredPositionForClub;
            }
            else
            {
                _root.anchorMin = _root.anchorMax = new Vector2(0f, 0f);
                _root.pivot     = new Vector2(0f, 0f);
                _root.anchoredPosition = _anchoredPositionForBall;
            }
        }

        // ── Populate ──────────────────────────────────────────────────────────

        void Populate()
        {
            if (_cardsContainer == null || _cardPrefab == null) return;

            // Destroy old cards
            for (int i = _cardsContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(_cardsContainer.GetChild(i).gameObject);

            _cards.Clear();
            _cardRts.Clear();
            _highlightedIndex = -1;

            if (_kind == Kind.Club)
            {
                // Display all clubs; selected is at the bottom (index 0 of the VLG, rendered last = bottom)
                // VLG with childAlignment=LowerCenter + reversed list means selected at bottom.
                var bag = ClubContext.EquippedBag;
                // Build list: non-selected first (top cards), then selected at bottom.
                var order = new List<int>();
                for (int i = 0; i < bag.Count; i++)
                    if (i != ClubContext.SelectedIndex) order.Add(i);
                order.Add(ClubContext.SelectedIndex);

                foreach (int i in order)
                {
                    int captured = i;
                    var entry = bag[i];
                    var go = Instantiate(_cardPrefab, _cardsContainer);
                    go.SetActive(true);
                    var card = go.GetComponent<SelectorCardWidget>();
                    if (card != null)
                    {
                        card.SetClub(entry, () =>
                        {
                            // K11: re-check live at commit time, not just at build time — the
                            // last line of defence behind the card's disabled state.
                            if (!IsClubSelectable(entry)) return;
                            ClubContext.RequestSelection(captured);
                            ClubSelectionBroadcast.Raise(entry.LabClubIndex);
                            if (_router != null)
                                _router.OnModalCommit();
                            else
                                Close();
                        });
                        // K11: gated-out clubs render greyed + non-interactive (not hidden).
                        card.SetSelectable(IsClubSelectable(entry));
                        _cards.Add(card);
                        _cardRts.Add(go.GetComponent<RectTransform>());
                    }
                }
            }
            else
            {
                var balls = BallContext.OwnedBalls;
                var order = new List<int>();
                for (int i = 0; i < balls.Count; i++)
                    if (i != BallContext.SelectedIndex) order.Add(i);
                order.Add(BallContext.SelectedIndex);

                foreach (int i in order)
                {
                    int captured = i;
                    var entry = balls[i];
                    var go = Instantiate(_cardPrefab, _cardsContainer);
                    go.SetActive(true);
                    var card = go.GetComponent<SelectorCardWidget>();
                    if (card != null)
                    {
                        card.SetBall(entry, () =>
                        {
                            BallContext.RequestSelection(captured);
                            if (_router != null)
                                _router.OnModalCommit();
                            else
                                Close();
                        });
                        _cards.Add(card);
                        _cardRts.Add(go.GetComponent<RectTransform>());
                    }
                }
            }
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
