// ─────────────────────────────────────────────────────────────────────────────
// Golfin.UI.Common — PaginationDotStrip
//
// One page-dot strip, shared by every carousel in the game (Clubs, Balls, Items,
// Bags, Roster). Replaces five near-identical copies of "instantiate one dot per
// page, destroy them all on every rebuild".
//
// WHY IT EXISTS
//   The old code created one dot per page with no upper bound. The dot row is
//   1074px wide, each dot is 16px on a HorizontalLayoutGroup with spacing 6, so it
//   fits 48 dots. At 799 owned clubs that is ceil(799/6) = 134 pages = 2948px of
//   dots crammed into 1074px — they overflowed both edges into an unreadable solid
//   bar. It also destroyed and re-instantiated every dot on every rebuild, so a
//   filter-tab press churned 134 GameObjects.
//
// WHAT IT DOES
//   · Pools the dots: creates at most MaxDots once, then only toggles them.
//   · totalPages <= MaxDots  -> one dot per page, exactly as before. No visual
//     change for any realistic inventory.
//   · totalPages >  MaxDots  -> a fixed window of MaxDots dots that slides to keep
//     the active page centred, with the edge dots shrunk to signal "more beyond".
//     The strip can never outgrow its container.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Common
{
    /// <summary>
    /// Owns the page dots for one carousel. Construct it with the container and the dot
    /// prefab, then call <see cref="Rebuild"/> when the page count changes and
    /// <see cref="Refresh"/> when the current page changes.
    /// </summary>
    public class PaginationDotStrip
    {
        /// <summary>Odd, so the active dot sits dead centre of the window.</summary>
        public const int MaxDots = 7;

        private static readonly Color DefaultActive   = Color.white;
        private static readonly Color DefaultInactive = new Color(1f, 1f, 1f, 0.35f);

        /// <summary>Scale applied to the end dots when there are pages beyond the window.</summary>
        private const float EdgeScale = 0.6f;

        private readonly Transform   _parent;
        private readonly GameObject? _prefab;
        private readonly Color       _activeColor;
        private readonly Color       _inactiveColor;
        private readonly Sprite?     _dotSprite;
        private readonly bool        _adoptExisting;
        private readonly List<Image> _pool = new();

        private bool _adopted;
        private int  _totalPages = 1;
        private int  _currentPage;

        /// <param name="dotPrefab">Cloned per dot. Null falls back to a generated 12x12 white dot.</param>
        /// <param name="adoptExisting">
        /// True when the container already holds hand-authored dot children in the scene (HomeScreen's
        /// Dot1/Dot2/Dot3). They become the first pool entries instead of being destroyed and
        /// replaced, so the authored look and ordering survive.
        /// </param>
        /// <param name="dotSprite">Forced onto every dot when set — a container whose dots would
        /// otherwise render as null-sprite squares needs this (Gacha).</param>
        public PaginationDotStrip(Transform parent,
                                  GameObject? dotPrefab,
                                  bool adoptExisting = false,
                                  Color? activeColor = null,
                                  Color? inactiveColor = null,
                                  Sprite? dotSprite = null)
        {
            _parent        = parent;
            _prefab        = dotPrefab;
            _adoptExisting = adoptExisting;
            _activeColor   = activeColor   ?? DefaultActive;
            _inactiveColor = inactiveColor ?? DefaultInactive;
            _dotSprite     = dotSprite;
        }

        /// <summary>Number of dot objects that actually exist (never more than MaxDots).</summary>
        public int PooledCount => _pool.Count;

        /// <summary>How many dots are visible right now — min(totalPages, MaxDots).</summary>
        public int VisibleCount => Mathf.Min(_totalPages, MaxDots);

        /// <summary>Hide every dot without destroying any — the row collapses to nothing.</summary>
        public void Clear()
        {
            _totalPages = 1;
            _currentPage = 0;
            foreach (var d in _pool)
                if (d != null) d.gameObject.SetActive(false);
        }

        /// <summary>Page count changed: grow the pool if needed and show the right number of dots.</summary>
        public void Rebuild(int totalPages, int currentPage)
        {
            _totalPages  = Mathf.Max(1, totalPages);
            _currentPage = Mathf.Clamp(currentPage, 0, _totalPages - 1);
            if (_parent == null) return;

            AdoptExistingChildrenOnce();

            int want = VisibleCount;
            while (_pool.Count < want)
            {
                var img = CreateDot(_pool.Count);
                if (img == null) break;      // prefab missing AND creation failed — bail, do not loop
                _pool.Add(img);
            }

            // Pooled, never destroyed: surplus dots are switched off, not deleted.
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null)
                    _pool[i].gameObject.SetActive(i < want);

            Refresh(_currentPage);
        }

        /// <summary>
        /// Takes any dot children that already exist in the scene into the pool, once. Without this a
        /// container authored with Dot1/Dot2/Dot3 would end up with those three PLUS freshly created
        /// ones, double-drawing the strip.
        /// </summary>
        private void AdoptExistingChildrenOnce()
        {
            if (_adopted || !_adoptExisting) return;
            _adopted = true;
            for (int i = 0; i < _parent.childCount && _pool.Count < MaxDots; i++)
            {
                var img = _parent.GetChild(i).GetComponent<Image>();
                if (img != null) _pool.Add(img);
            }
        }

        /// <summary>Current page changed: recolour, and slide the window when it is in use.</summary>
        public void Refresh(int currentPage)
        {
            _currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, _totalPages - 1));
            int want = VisibleCount;
            if (want <= 0) return;

            int windowStart = WindowStart();

            for (int i = 0; i < want && i < _pool.Count; i++)
            {
                var dot = _pool[i];
                if (dot == null) continue;

                int page = windowStart + i;
                if (_dotSprite != null && dot.sprite != _dotSprite) { dot.sprite = _dotSprite; dot.enabled = true; }
                dot.color = page == _currentPage ? _activeColor : _inactiveColor;

                // Shrink an edge dot only when it is standing in for pages we are not showing.
                bool moreBefore = i == 0        && windowStart > 0;
                bool moreAfter  = i == want - 1 && windowStart + want < _totalPages;
                float scale = (moreBefore || moreAfter) ? EdgeScale : 1f;
                dot.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>
        /// First page represented by the window. Keeps the active page centred, then clamps so the
        /// window never runs past either end of the page range.
        /// </summary>
        public int WindowStart()
        {
            if (_totalPages <= MaxDots) return 0;
            int half = MaxDots / 2;
            return Mathf.Clamp(_currentPage - half, 0, _totalPages - MaxDots);
        }

        private Image? CreateDot(int index)
        {
            GameObject go;
            if (_prefab != null)
            {
                go = Object.Instantiate(_prefab, _parent);
            }
            else
            {
                go = new GameObject($"Dot_{index}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_parent, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(12f, 12f);
            }
            return go.GetComponent<Image>();
        }
    }
}
