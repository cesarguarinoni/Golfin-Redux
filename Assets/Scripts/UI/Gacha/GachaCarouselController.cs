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
    public class GachaCarouselController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // ── Inspector refs ────────────────────────────────────────────────────

        [Header("Spawning")]
        [SerializeField] private GameObject _cardPrefab;       // GachaBannerCard.prefab
        [SerializeField] private Transform  _dotContainer;     // DotRow
        [SerializeField] private GameObject _dotPrefab;        // reused dot child (cloned for each banner)
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
        private float _currentOffset = 0f;   // visual offset added during drag
        private float _targetOffset  = 0f;   // snapped target offset
        private float _dragStartX    = 0f;
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
            // Smooth snap / drag follow
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
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            float delta = eventData.position.x - _dragStartX;
            _targetOffset = delta;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            float delta = eventData.position.x - _dragStartX;

            if (Mathf.Abs(delta) >= _dragThreshold)
            {
                if (delta < 0f)
                    _currentIndex = Mathf.Min(_currentIndex + 1, _cards.Count - 1);
                else
                    _currentIndex = Mathf.Max(_currentIndex - 1, 0);
            }

            _targetOffset = 0f;
            UpdateDots();
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
            _currentOffset = 0f;
            _targetOffset  = 0f;

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

                // Position: card i is at (i - currentIndex) * spacing + drag offset
                float targetX = (i - _currentIndex) * _cardSpacing + _currentOffset;
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

            // Style: active = white/full, inactive = dim
            for (int i = 0; i < _dots.Count; i++)
            {
                if (_dots[i] == null) continue;
                var img = _dots[i].GetComponent<Image>();
                if (img == null) continue;
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
                // Clamp index to valid range and rebuild dots
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _cards.Count - 1);
                _targetOffset = 0f;
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
