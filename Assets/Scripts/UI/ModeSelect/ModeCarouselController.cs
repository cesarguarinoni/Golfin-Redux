using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.UI.Toast;
using Golfin.UI.Matchmaking;
using GolfinRedux.UI;
using Golfin.Gameplay.Session;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Home carousel controller for the 4-mode horizontal carousel on HomeScreen.
    ///
    /// ITER-6 FIDELITY CHANGES (§6.3):
    ///   Item 9: scroll arrows REMOVED — no arrow buttons on home carousel.
    ///   Item 10: chevron is expand/collapse affordance only (shown on home centered card).
    ///   Item 8: centered card = 764 expanded / 556 collapsed; sides = 677.
    ///   Item 5: PLAY always on centered card (both collapsed and expanded states).
    ///
    /// Navigation: swipe/drag only (arrows removed per Figma).
    /// Snap eases to center (coroutine-Lerp, unscaledDeltaTime, ~0.18s ease-out).
    /// Expand fires AFTER snap settles.
    /// </summary>
    public class ModeCarouselController : MonoBehaviour,
        UnityEngine.EventSystems.IBeginDragHandler,
        UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IEndDragHandler
    {
        [Header("Cards Container")]
        [SerializeField] private RectTransform cardsContainer;
        [SerializeField] private ScrollRect scrollRect;

        [Header("1v1 Matchmaking Modal")]
        [SerializeField] private MatchmakingModalController matchmakingModal1v1;

        [Header("Card Prefab")]
        [SerializeField] private ModeCardController cardPrefab;

        [Header("Initial selection")]
        [Tooltip("Mode id centered when the home carousel opens. Falls back to the first playable mode if empty, unknown, or locked.")]
        [SerializeField] private string _defaultModeId = "practice";

        // NOTE: §6.3 item 9 — arrows REMOVED from home carousel per Figma.
        // These fields are kept for backward-compat but should be left null.
        [Header("Arrow Buttons (REMOVED per §6.3 item 9 — leave null)")]
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;

        [Header("Snap / Expand Animation")]
        [SerializeField] private float _snapDuration     = 0.22f;
        [SerializeField] private float _expandAfterSnap  = 0.05f;
        // Duration of the expand/collapse resize when tapping the tagline (slide reuses _snapDuration).
        [SerializeField] private float _resizeDuration   = 0.2f;

        [Header("Card Sizes (Figma §6.1)")]
        // NOTE: collapsed center = 556w, expanded center = 764w.
        // Side/peek cards are CollapsedNoPlay, so they share the collapsed card's width (556).
        // Their height is content-driven (root ContentSizeFitter, vFit=PreferredSize) — a side
        // card hugs to exactly the collapsed card minus the hidden PLAY button.
        [SerializeField] private float _collapsedCardWidth  = 556f;
        [SerializeField] private float _collapsedCardHeight = 484f;
        [SerializeField] private float _expandedCardWidth   = 764f;
        [SerializeField] private float _expandedCardHeight  = 822f;
        [SerializeField] private float _sideCardWidth       = 556f;
        [SerializeField] private float _sideCardHeight      = 268f;
        [SerializeField] private float _cardGap             = 24f;

        // Circular carousel: 3× virtual array
        private readonly List<ModeCardController> _allCards = new List<ModeCardController>();
        private int _dataCount = 0;
        private int _centeredVirtualIndex = 0;
        private bool _isSnapping = false;
        // Expand preference for the centered card: starts collapsed (+PLAY); tapping the tagline
        // toggles it; the preference carries over when swiping to the next card.
        private bool _centerExpanded = false;
        // Handle to the tagline expand/collapse resize animation (so re-taps restart it cleanly).
        private Coroutine _layoutAnim;

        // Swipe drag support
        private float _dragStartX;
        private float _dragStartContainerX;
        private bool  _isDragging;

        private void Awake()
        {
            // §6.3 item 9: arrows deliberately not wired (Figma has them hidden).
            // The fields remain for inspector safety but clicks are not hooked.
            if (leftArrowButton  != null) leftArrowButton.gameObject.SetActive(false);
            if (rightArrowButton != null) rightArrowButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(RebuildCardsCoroutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            UnwireCards();
            _allCards.Clear();
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private IEnumerator RebuildCardsCoroutine()
        {
            yield return null;
            RebuildCards();
        }

        private void RebuildCards()
        {
            UnwireCards();
            if (cardsContainer != null)
            {
                var toDestroy = new List<GameObject>();
                foreach (Transform child in cardsContainer) toDestroy.Add(child.gameObject);
                foreach (var go in toDestroy) DestroyImmediate(go);
            }
            _allCards.Clear();

            var db = ModesDatabaseCSV.Instance;
            if (db == null)
            {
                Debug.LogWarning("[ModeCarousel] ModesDatabaseCSV.Instance is null.");
                return;
            }
            if (cardPrefab == null)
            {
                Debug.LogError("[ModeCarousel] cardPrefab is null.");
                return;
            }

            var modes = db.GetAllModes();
            _dataCount = modes.Count;
            if (_dataCount == 0) return;

            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var mode in modes)
                {
                    var card = Instantiate(cardPrefab, cardsContainer);
                    // Side/peek cards collapse WITHOUT a PLAY button; the centered card is
                    // expanded below. (CollapsedNoPlay → no PLAY; Expanded → PLAY.)
                    ModeCardState state = mode.locked ? ModeCardState.Locked : ModeCardState.CollapsedNoPlay;
                    // §6.3 item 10: chevron is shown on HOME cards (expand/collapse affordance)
                    card.SetShowChevron(true);
                    // Locked cards can be swiped into the centre, so they must be tappable too.
                    card.SetTapWhenLocked(true);
                    // Set heights before Bind so AnimateHeight uses correct targets on initial Collapsed state
                    card.SetHeights(_collapsedCardHeight, _expandedCardHeight);
                    card.Bind(mode, state);
                    if (pass == 1) card.OnPlayClicked += HandlePlayClicked;
                    card.OnTaglineTapped += HandleTaglineTapped;
                    // Wired on EVERY pass (unlike PLAY): the cards a player taps are the side/peek
                    // instances, which live in passes 0 and 2 as well as 1.
                    card.OnCardTapped += HandleCardTapped;
                    _allCards.Add(card);
                }
            }

            // Center on the configured default mode (Practice) when it is present and playable.
            // The CSV is sorted by its `order` column, so "first playable" alone would land on
            // whichever mode happens to sort first (versus_1v1) rather than the intended default.
            int startIndex = -1;
            if (!string.IsNullOrEmpty(_defaultModeId))
            {
                for (int i = 0; i < _dataCount; i++)
                {
                    if (modes[i].id == _defaultModeId && !modes[i].locked) { startIndex = i; break; }
                }
            }
            if (startIndex < 0)
            {
                for (int i = 0; i < _dataCount; i++)
                {
                    if (!modes[i].locked) { startIndex = i; break; }
                }
            }
            if (startIndex < 0) startIndex = 0;
            _centeredVirtualIndex = _dataCount + startIndex;

            StartCoroutine(InitialSnapAndExpand(_centeredVirtualIndex));
        }

        private IEnumerator InitialSnapAndExpand(int virtualIndex)
        {
            _centeredVirtualIndex = virtualIndex;
            ApplyCardStates();
            UpdateAllCardSizes(_centerExpanded);

            float targetX = ComputeOffsetForVirtualIndex(_centeredVirtualIndex);
            if (cardsContainer != null)
                cardsContainer.anchoredPosition = new Vector2(targetX, 0f);

            yield return null;
            Canvas.ForceUpdateCanvases();

            // Re-center now that layout/CSF has run (center width differs collapsed vs expanded).
            if (cardsContainer != null)
                cardsContainer.anchoredPosition = new Vector2(ComputeOffsetForVirtualIndex(_centeredVirtualIndex), 0f);
        }

        // Re-derive EVERY card's visual state purely from its carousel position. This is the
        // failsafe that keeps PLAY on the center and off the sides even after the 3× virtual-array
        // NormalizeCenterInstant swaps which instance is centered — the bug where paging from a
        // locked card to a playable one left the center without PLAY (or a side card keeping one).
        // PLAY/chevron visibility is then driven by SetCenter(), independent of collapsed/expanded.
        private void ApplyCardStates()
        {
            for (int i = 0; i < _allCards.Count; i++)
            {
                var card = _allCards[i];
                if (card == null) continue;

                bool isCenter = (i == _centeredVirtualIndex);
                ModeCardState st;
                if (card.IsLocked)   st = ModeCardState.Locked;
                else if (isCenter)   st = _centerExpanded ? ModeCardState.Expanded : ModeCardState.Collapsed;
                else                 st = ModeCardState.CollapsedNoPlay;

                card.SetHeights(_collapsedCardHeight, _expandedCardHeight);
                card.SetState(st);
                card.SetCenter(isCenter);   // <- PLAY/chevron failsafe: center always, sides never
            }
        }

        // ── Swipe drag (§6.3 item 9: no arrows; navigation is by horizontal swipe/drag) ──

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_isSnapping || _layoutAnim != null) return;
            _isDragging = true;
            _dragStartX = eventData.position.x;
            _dragStartContainerX = cardsContainer != null ? cardsContainer.anchoredPosition.x : 0f;
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!_isDragging || _isSnapping || cardsContainer == null) return;
            float delta = eventData.position.x - _dragStartX;
            cardsContainer.anchoredPosition = new Vector2(_dragStartContainerX + delta, 0f);
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;

            float delta = eventData.position.x - _dragStartX;
            const float swipeThreshold = 60f;

            if (Mathf.Abs(delta) >= swipeThreshold)
            {
                int next = delta > 0
                    ? _centeredVirtualIndex - 1
                    : _centeredVirtualIndex + 1;
                StartCoroutine(SnapAndExpandCoroutine(next));
            }
            else
            {
                // Snap back to current
                StartCoroutine(SnapAndExpandCoroutine(_centeredVirtualIndex));
            }
        }

        // ── Snap coroutine ────────────────────────────────────────────────────

        private IEnumerator SnapAndExpandCoroutine(int targetVirtual)
        {
            _isSnapping = true;
            // Cancel any in-flight tagline resize so it doesn't fight this slide.
            if (_layoutAnim != null) { StopCoroutine(_layoutAnim); _layoutAnim = null; }

            // Move the center pointer and re-derive ALL states from position: the old center
            // becomes a side card (no PLAY); the new center adopts the remembered expand state.
            _centeredVirtualIndex = targetVirtual;
            ApplyCardStates();

            // Combined slide + resize: lerp every card's size & position AND the container offset
            // together from the (dragged) current layout to the centered target — no instant pop.
            yield return LerpToTargetLayout(_snapDuration);

            // The virtual-array normalize can swap which INSTANCE is centered — re-derive every
            // card's state afterwards so the (possibly new) centered instance still gets PLAY and
            // the side cards stay PLAY-less. This is the failsafe Cesar asked for. (Instant: the
            // swap is to an identical card at the same visual position.)
            NormalizeCenterInstant();
            ApplyCardStates();
            UpdateAllCardSizes(_centerExpanded);
            Canvas.ForceUpdateCanvases();
            if (cardsContainer != null)
                cardsContainer.anchoredPosition = new Vector2(ComputeOffsetForVirtualIndex(_centeredVirtualIndex), 0f);

            _isSnapping = false;
        }

        // ── Unified size + position animation ─────────────────────────────────
        // Smoothly interpolates EVERY card's size and position, plus the container offset, from the
        // current layout to the target layout (derived from the already-applied card states and
        // _centerExpanded). Used by both the tagline expand/collapse and the swipe snap so neither
        // pops. ContentSizeFitter is disabled during the lerp (so our lerped heights stick) and the
        // RectMask2D on each card clips the content that hasn't unfolded yet. Re-enabled on settle.
        private IEnumerator LerpToTargetLayout(float duration)
        {
            int n = _allCards.Count;
            if (n == 0) yield break;

            var fromS = new Vector2[n]; var fromP = new Vector2[n];
            var toS   = new Vector2[n]; var toP   = new Vector2[n];
            var csf   = new ContentSizeFitter[n];

            // 1) Snapshot the CURRENT layout (the visual start — may be mid a prior interrupted anim).
            for (int i = 0; i < n; i++)
            {
                var c = _allCards[i];
                if (c == null || c.rootRect == null) continue;
                fromS[i] = c.rootRect.sizeDelta;
                fromP[i] = c.rootRect.anchoredPosition;
                csf[i]   = c.rootRect.GetComponent<ContentSizeFitter>();
            }
            float fromX = cardsContainer != null ? cardsContainer.anchoredPosition.x : 0f;

            // 2) Resolve the TARGET layout with CSF ON (so heights are content-correct), read it.
            for (int i = 0; i < n; i++) if (csf[i] != null) csf[i].enabled = true;
            UpdateAllCardSizes(_centerExpanded);
            Canvas.ForceUpdateCanvases();
            float toX = ComputeOffsetForVirtualIndex(_centeredVirtualIndex);
            for (int i = 0; i < n; i++)
            {
                var c = _allCards[i];
                if (c == null || c.rootRect == null) continue;
                toS[i] = c.rootRect.sizeDelta;
                toP[i] = c.rootRect.anchoredPosition;
            }

            // 3) Snap back to FROM (no frame rendered in between) and freeze CSF for the lerp.
            for (int i = 0; i < n; i++)
            {
                var c = _allCards[i];
                if (c == null || c.rootRect == null) continue;
                if (csf[i] != null) csf[i].enabled = false;
                c.rootRect.sizeDelta = fromS[i];
                c.rootRect.anchoredPosition = fromP[i];
            }
            if (cardsContainer != null) cardsContainer.anchoredPosition = new Vector2(fromX, 0f);

            // 4) Ease-out lerp of all sizes + positions + container.
            float el = 0f;
            while (el < duration)
            {
                el += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(el / duration);
                float e = 1f - (1f - t) * (1f - t) * (1f - t);   // cubic ease-out
                for (int i = 0; i < n; i++)
                {
                    var c = _allCards[i];
                    if (c == null || c.rootRect == null) continue;
                    c.rootRect.sizeDelta = Vector2.Lerp(fromS[i], toS[i], e);
                    c.rootRect.anchoredPosition = Vector2.Lerp(fromP[i], toP[i], e);
                }
                if (cardsContainer != null)
                    cardsContainer.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, e), 0f);
                yield return null;
            }

            // 5) Settle exactly on target and hand height back to the ContentSizeFitter.
            for (int i = 0; i < n; i++)
            {
                var c = _allCards[i];
                if (c == null || c.rootRect == null) continue;
                c.rootRect.sizeDelta = toS[i];
                c.rootRect.anchoredPosition = toP[i];
                if (csf[i] != null) csf[i].enabled = true;
            }
            if (cardsContainer != null) cardsContainer.anchoredPosition = new Vector2(toX, 0f);
            Canvas.ForceUpdateCanvases();
        }

        private void NormalizeCenterInstant()
        {
            if (_dataCount <= 0) return;
            bool needsJump = false;

            if (_centeredVirtualIndex < _dataCount)
            {
                _centeredVirtualIndex += _dataCount;
                needsJump = true;
            }
            else if (_centeredVirtualIndex >= 2 * _dataCount)
            {
                _centeredVirtualIndex -= _dataCount;
                needsJump = true;
            }

            if (needsJump && cardsContainer != null)
            {
                float x = ComputeOffsetForVirtualIndex(_centeredVirtualIndex);
                cardsContainer.anchoredPosition = new Vector2(x, 0f);
            }
        }

        // ── Size + Position helpers ───────────────────────────────────────────

        private void UpdateAllCardSizes(bool centerExpanded)
        {
            float cursor = 0f;
            for (int i = 0; i < _allCards.Count; i++)
            {
                var card = _allCards[i];
                if (card == null || card.rootRect == null) { cursor += _sideCardWidth + _cardGap; continue; }

                bool isCenter = (i == _centeredVirtualIndex);
                float w, h;
                if (isCenter)
                {
                    w = centerExpanded ? _expandedCardWidth  : _collapsedCardWidth;
                    h = centerExpanded ? _expandedCardHeight : _collapsedCardHeight;
                }
                else
                {
                    w = _sideCardWidth;
                    h = _sideCardHeight;
                }
                card.rootRect.sizeDelta = new Vector2(w, h);
                card.rootRect.anchorMin = new Vector2(0f, 0f);
                card.rootRect.anchorMax = new Vector2(0f, 0f);
                card.rootRect.pivot     = new Vector2(0.5f, 0f);
                card.rootRect.anchoredPosition = new Vector2(cursor + w * 0.5f, 0f);

                cursor += w + _cardGap;
            }
        }

        private float ComputeOffsetForVirtualIndex(int vIndex)
        {
            if (_allCards.Count == 0) return 0f;

            float cursor = 0f;
            for (int i = 0; i < vIndex; i++)
            {
                bool isC = (i == _centeredVirtualIndex);
                float w = isC
                    ? (_allCards[i].State == ModeCardState.Expanded ? _expandedCardWidth : _collapsedCardWidth)
                    : _sideCardWidth;
                cursor += w + _cardGap;
            }

            float centerW;
            if (vIndex < _allCards.Count && _allCards[vIndex] != null)
                centerW = (_allCards[vIndex].State == ModeCardState.Expanded) ? _expandedCardWidth : _collapsedCardWidth;
            else
                centerW = _collapsedCardWidth;

            float cardCenter = cursor + centerW * 0.5f;
            float viewW = GetViewportWidth();
            return -cardCenter + viewW * 0.5f;
        }

        private float GetViewportWidth()
        {
            if (scrollRect != null && scrollRect.viewport != null)
                return scrollRect.viewport.rect.width;
            var parentRt = transform.parent as RectTransform;
            if (parentRt != null) return parentRt.rect.width;
            return 1170f;
        }

        // ── PLAY routing ──────────────────────────────────────────────────────

        private void UnwireCards()
        {
            foreach (var c in _allCards)
            {
                if (c == null) continue;
                c.OnPlayClicked -= HandlePlayClicked;
                c.OnTaglineTapped -= HandleTaglineTapped;
                c.OnCardTapped -= HandleCardTapped;
            }
        }

        private void HandlePlayClicked(ModeCardController card)
        {
            if (card == null) return;

            var db = ModesDatabaseCSV.Instance;
            if (db == null) return;

            var mode = db.GetMode(card.ModeId);
            if (mode == null) return;

            switch (mode.target)
            {
                case "hole_select":
                    if (ScreenManager.Instance != null)
                        ScreenManager.Instance.ShowScreen(ScreenId.HoleSelection);
                    else
                        Debug.LogWarning("[ModeCarousel] ScreenManager not found.");
                    break;

                case "tournaments":
                    // Tournament browse screen (T7). No entry-fee spend here — per-tournament
                    // fees are owned by the signup flow (TournamentSignupModalController).
                    if (ScreenManager.Instance != null)
                        ScreenManager.Instance.ShowScreen(ScreenId.TournamentSelection);
                    else
                        Debug.LogWarning("[ModeCarousel] Tournaments PLAY — ScreenManager not found.");
                    break;

                case "matchmaking_1v1":
                    // 1v1 path: flag the session as versus BEFORE opening matchmaking.
                    GameSession.IsVersus = true;
                    // Pick a random hole (1-18), then open matchmaking modal.
                    // MatchmakingModalController.Open expects a 0-based index.
                    if (matchmakingModal1v1 != null)
                    {
                        int randomHoleIndex = Random.Range(0, 18); // 0-based → hole numbers 1-18
                        matchmakingModal1v1.Open(randomHoleIndex);
                    }
                    else
                    {
                        Debug.LogWarning("[ModeCarousel] 1v1 PLAY — matchmakingModal1v1 not wired in Inspector.");
                    }
                    break;

                default:
                    Debug.LogWarning($"[ModeCarousel] PLAY on '{card.ModeId}' has no route (target='{mode.target}').");
                    break;
            }
        }

        // Tapping a side/peek card slides it into the centre — identical to swiping onto it, so the
        // two gestures land in the same place. Runs the same SnapAndExpandCoroutine the swipe uses,
        // which carries the remembered expand preference and re-derives PLAY/border/title.
        private void HandleCardTapped(ModeCardController card)
        {
            // A swipe that begins and ends over the same card also dispatches a click. The drag is
            // still in flight at this point (OnEndDrag runs AFTER the click), so _isDragging tells
            // us the gesture was a swipe and the swipe owns it.
            if (_isDragging || _isSnapping || card == null) return;

            int index = _allCards.IndexOf(card);
            if (index < 0 || index == _centeredVirtualIndex) return;   // already centred

            StartCoroutine(SnapAndExpandCoroutine(index));
        }

        // Tapping the tagline of the CENTERED (non-locked) card toggles Expanded <-> Collapsed(+PLAY).
        // The preference is remembered and carries over when swiping to the next card.
        private void HandleTaglineTapped(ModeCardController card)
        {
            if (_isDragging || _isSnapping || card == null) return;
            if (_centeredVirtualIndex < 0 || _centeredVirtualIndex >= _allCards.Count) return;
            // The tagline row sits ON TOP of the whole-card tap target, so on a side card it is the
            // hit the player actually gets. Expand/collapse is only meaningful once a card IS the
            // centre, so on a side card this tap means the same thing as any other: slide it in.
            if (_allCards[_centeredVirtualIndex] != card) { HandleCardTapped(card); return; }
            if (card.IsLocked) return;

            _centerExpanded = !_centerExpanded;
            ApplyCardStates();
            // Animate the expand/collapse (size + re-center) instead of an instant pop.
            if (_layoutAnim != null) StopCoroutine(_layoutAnim);
            _layoutAnim = StartCoroutine(LayoutAnimRunner(_resizeDuration));
        }

        private IEnumerator LayoutAnimRunner(float duration)
        {
            yield return LerpToTargetLayout(duration);
            _layoutAnim = null;
        }
    }
}
