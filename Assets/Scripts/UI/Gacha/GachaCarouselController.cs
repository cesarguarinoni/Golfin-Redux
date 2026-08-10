// Assets/Scripts/UI/Gacha/GachaCarouselController.cs
// gacha_screen Stage 2 — §3c Carousel + Countdown driver
// Horizontal drag/swipe, snap-to-center, NO wrap, distance-based scale/alpha falloff.
// ONE Update ticker for countdown and position lerp (not per-card coroutines).
// Dot indicators: dynamic count = live banners, center = active index.
// On expiry: RemoveBanner, rebuild dots, snap to nearest live; zero live → EmptyState.

using System;
using System.Collections.Generic;
using GolfinRedux.UI.Gacha;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Drives the Gacha banner carousel in GachaTabContent.
    /// Attach to the GachaTabContent GameObject.
    /// Spawns one GachaBannerCard per live banner; manages positions, falloff, dots, countdown.
    /// </summary>
    public class GachaCarouselController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        // ── Inspector refs ────────────────────────────────────────────────────

        [Header("Spawning")]
        [SerializeField] private GameObject _cardPrefab;       // GachaBannerCard.prefab
        [SerializeField] private Transform  _dotContainer;     // DotRow
        [SerializeField] private GameObject _dotPrefab;        // reused dot child (cloned for each banner)
        [SerializeField] private Sprite     _dotSprite;        // circular dot sprite (Dot Active.png) — applied to every spawned dot
        [SerializeField] private GameObject _emptyState;       // "No active banners" GO

        [Header("Card Layout")]
        [Tooltip("Horizontal gap between card centres (px).")]
        [SerializeField] private float _cardSpacing  = 800f;
        [Tooltip("Y offset for all card anchored positions.")]
        [SerializeField] private float _cardYOffset  = 42f;

        [Header("Falloff")]
        [Tooltip("Scale of side cards (0–1). 1 = same size as centre.")]
        [SerializeField] private float _sideScale    = 0.78f;
        [Tooltip("Alpha of side cards (0–1). 1 = fully opaque.")]
        [SerializeField] private float _sideAlpha    = 0.45f;

        [Header("Snap / Drag")]
        [Tooltip("Snap lerp speed (per-frame).")]
        [SerializeField] private float _snapSpeed    = 10f;
        [Tooltip("Min drag distance (px) to advance the index.")]
        [SerializeField] private float _dragThreshold = 80f;

        // ── Internal state ────────────────────────────────────────────────────

        private readonly List<GachaBannerCard> _cards     = new();
        private readonly List<GachaBannerEntry> _entries  = new();
        private readonly List<GameObject>        _dots     = new();
        private int   _currentIndex  = 0;
        private float _currentOffset = 0f;   // continuous scroll position (canvas units)
        private float _targetOffset  = 0f;   // snap target scroll (nearest card * spacing)
        private float _dragStartX    = 0f;
        private float _dragStartScroll = 0f; // scroll position when the drag began
        private bool  _isDragging    = false;

        // ── Countdown update interval ──────────────────────────────────────────
        private float _countdownTimer = 0f;
        private const float CountdownInterval = 1f; // update text every second

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            GachaBannerCatalog.Reload();
            RebuildCarousel();
        }

        private void Update()
        {
            // Continuous scroll: ease to the snap target only when not actively dragging.
            if (!_isDragging)
                _currentOffset = Mathf.Lerp(_currentOffset, _targetOffset, Time.deltaTime * _snapSpeed);
            UpdateCardTransforms();

            // Countdown tick
            _countdownTimer -= Time.deltaTime;
            if (_countdownTimer <= 0f)
            {
                _countdownTimer = CountdownInterval;
                TickCountdown();
            }
        }

        // ── Drag handlers ─────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragStartX = eventData.position.x;
            _dragStartScroll = _currentOffset;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            // Cards follow the finger 1:1 (drag right → scroll decreases → cards slide right).
            float delta = eventData.position.x - _dragStartX;
            _currentOffset = _dragStartScroll - delta;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            // Snap to the nearest card from where the scroll landed — smooth ease, no binary index flip.
            int idx = Mathf.Clamp(Mathf.RoundToInt(_currentOffset / _cardSpacing), 0, _cards.Count - 1);
            _currentIndex = idx;
            _targetOffset = idx * _cardSpacing;
            UpdateDots();
        }

        // ── Tap-to-centre ─────────────────────────────────────────────────────

        /// <summary>
        /// Tapping a side banner slides it into the centre — the same result as swiping onto it.
        /// The click bubbles up from the card's graphic (the PULL / RULES buttons handle their own
        /// clicks, so they are unaffected); we hit-test the cards to find which one was hit.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            // A swipe that begins and ends over the same card also dispatches a click. The drag is
            // still in flight here (OnEndDrag runs AFTER the click), so the swipe owns the gesture.
            if (_isDragging || eventData.dragging) return;

            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                var rt = _cards[i].GetComponent<RectTransform>();
                if (rt == null) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, eventData.position, eventData.pressEventCamera))
                    continue;

                if (i == _currentIndex) return;   // already centred
                _currentIndex = i;
                _targetOffset = i * _cardSpacing; // Update()'s lerp eases us there, same as a snap
                UpdateDots();
                return;
            }
        }

        // ── Build / Rebuild ───────────────────────────────────────────────────

        private void RebuildCarousel()
        {
            // Destroy existing cards
            foreach (var c in _cards)
                if (c != null) Destroy(c.gameObject);
            _cards.Clear();
            _entries.Clear();

            var live = GachaBannerCatalog.GetLiveBanners();

            if (live.Count == 0)
            {
                ShowEmptyState(true);
                ClearDots();
                return;
            }

            ShowEmptyState(false);

            foreach (var entry in live)
            {
                _entries.Add(entry);
                var go = Instantiate(_cardPrefab, transform);
                go.name = "BannerCard_" + entry.BannerId;
                SetupCardRefs(go);
                var card = go.GetComponent<GachaBannerCard>();
                card.Bind(entry);
                _cards.Add(card);
            }

            // Clamp current index
            _currentIndex  = Mathf.Clamp(_currentIndex, 0, _cards.Count - 1);
            _currentOffset = _currentIndex * _cardSpacing;
            _targetOffset  = _currentOffset;

            UpdateCardTransforms();
            UpdateDots();
        }

        /// <summary>
        /// Wire all child ref components from the spawned card GO.
        /// Matches GachaBannerCard hierarchy (same as _GachaCard_CesarTuned layout).
        /// </summary>
        private void SetupCardRefs(GameObject go)
        {
            var card = go.GetComponent<GachaBannerCard>();
            if (card == null) return;

            // Use SerializedObject to wire fields so they persist correctly.
            // In runtime we use Unity's GetComponent / Find approach instead.
            // GachaBannerCard.Bind() does its own path-lookup on the GO hierarchy.
            // All field wiring is done via SetField reflection for the prefab refs already wired at author time.
            // For runtime-spawned instances the fields are wired from the prefab; Bind() runs the logic.
        }

        // ── Position / falloff ────────────────────────────────────────────────

        private void UpdateCardTransforms()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                var rt = _cards[i].GetComponent<RectTransform>();
                var cg = _cards[i].GetComponent<CanvasGroup>();
                if (rt == null || cg == null) continue;

                // Position: continuous scroll — card i is at i*spacing minus the scroll position.
                float targetX = i * _cardSpacing - _currentOffset;
                rt.anchoredPosition = new Vector2(targetX, _cardYOffset);

                // Falloff: normalised distance from centre (0 = centre, 1 = one card away)
                float t = Mathf.Clamp01(Mathf.Abs(targetX) / _cardSpacing);
                float scale = Mathf.Lerp(1f, _sideScale, t);
                float alpha = Mathf.Lerp(1f, _sideAlpha, t);

                rt.localScale = new Vector3(scale, scale, 1f);
                cg.alpha = alpha;
            }
        }

        // ── Dot indicators ────────────────────────────────────────────────────

        private void UpdateDots()
        {
            if (_dotContainer == null) return;

            // Remove extra dots
            while (_dots.Count > _cards.Count)
            {
                var d = _dots[_dots.Count - 1];
                _dots.RemoveAt(_dots.Count - 1);
                if (d != null) Destroy(d);
            }

            // Add missing dots
            while (_dots.Count < _cards.Count)
            {
                GameObject dot;
                if (_dotPrefab != null)
                {
                    dot = Instantiate(_dotPrefab, _dotContainer);
                }
                else
                {
                    // Fallback: clone first existing child of DotRow if prefab not set
                    if (_dotContainer.childCount > 0)
                        dot = Instantiate(_dotContainer.GetChild(0).gameObject, _dotContainer);
                    else
                    {
                        dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                        dot.transform.SetParent(_dotContainer, false);
                        var img = dot.GetComponent<Image>();
                        var rt  = dot.GetComponent<RectTransform>();
                        rt.sizeDelta = new Vector2(12f, 12f);
                        img.color = Color.white;
                    }
                }
                dot.SetActive(true);
                _dots.Add(dot);
            }

            // Ensure we have the circular dot sprite (Resources fallback — the controller lives in
            // the scene, so we avoid a serialized ref + scene save). Cached after first load.
            if (_dotSprite == null)
                _dotSprite = Resources.Load<Sprite>("Art/Gacha/GachaDot");

            // Style: active = white/full, inactive = dim
            for (int i = 0; i < _dots.Count; i++)
            {
                if (_dots[i] == null) continue;
                var img = _dots[i].GetComponent<Image>();
                if (img == null) continue;
                if (_dotSprite != null) { img.sprite = _dotSprite; img.enabled = true; }  // ensure circular, not a null-sprite square
                bool active = (i == _currentIndex);
                img.color = active
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private void ClearDots()
        {
            foreach (var d in _dots)
                if (d != null) Destroy(d);
            _dots.Clear();
        }

        // ── Countdown ─────────────────────────────────────────────────────────

        private void TickCountdown()
        {
            var now = DateTime.UtcNow;
            bool anyExpired = false;

            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                if (_cards[i] == null) continue;
                var entry = _entries[i];

                if (entry.EndUtc <= now)
                {
                    // Expired — remove
                    Debug.Log($"[GachaCarousel] Banner '{entry.BannerId}' expired. Removing.");
                    Destroy(_cards[i].gameObject);
                    _cards.RemoveAt(i);
                    _entries.RemoveAt(i);
                    anyExpired = true;
                    continue;
                }

                // Update countdown text
                var remaining = entry.EndUtc - now;
                _cards[i].SetCountdownText(FormatCountdown(remaining));
            }

            if (anyExpired)
            {
                if (_cards.Count == 0)
                {
                    ShowEmptyState(true);
                    ClearDots();
                    return;
                }
                // Clamp index to valid range, re-snap scroll to it, and rebuild dots
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _cards.Count - 1);
                _targetOffset = _currentIndex * _cardSpacing;
                _currentOffset = _targetOffset;
                UpdateDots();
            }
        }

        /// <summary>Format a TimeSpan into "ENDS IN: {d}d {h}h {m}m {ss} s". Public for tests.</summary>
        public static string FormatCountdown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
                return "ENDS IN: 0s";

            int totalSeconds = (int)remaining.TotalSeconds;
            int d  = totalSeconds / 86400;
            int h  = (totalSeconds % 86400) / 3600;
            int m  = (totalSeconds % 3600) / 60;
            int s  = totalSeconds % 60;

            if (d > 0)
                return $"ENDS IN: {d}d {h}h {m}m {s:D2}s";
            if (h > 0)
                return $"ENDS IN: {h}h {m}m {s:D2}s";
            if (m > 0)
                return $"ENDS IN: {m}m {s:D2}s";
            return $"ENDS IN: {s:D2}s";
        }

        // ── Empty state ───────────────────────────────────────────────────────

        private void ShowEmptyState(bool show)
        {
            if (_emptyState != null)
                _emptyState.SetActive(show);
        }
    }
}
