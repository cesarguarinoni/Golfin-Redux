#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory
{
    /// <summary>
    /// Sub-filter bar inside the Club Inventory screen.
    /// Buttons: ALL | DRIVERS | WOODS | IRONS | A.WEDGES | P.WEDGES | S.WEDGES | PUTTERS
    ///
    /// Fires OnFilterChanged (null = ALL) which ClubCarouselController (Phase C) listens to.
    /// Button index maps to ClubType enum: index 0 = ALL, index N = (ClubType)(N-1).
    /// </summary>
    public class ClubFilterBar : MonoBehaviour
    {
        [SerializeField] private Button[] filterButtons = null!;

        [Header("Visual State Colors")]
        [SerializeField] private Color activeTextColor   = Color.white;
        [SerializeField] private Color inactiveTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color activeButtonColor   = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color inactiveButtonColor = new Color(1f, 1f, 1f, 0f);

        /// <summary>null = show ALL clubs; non-null = show only that ClubType.</summary>
        public event System.Action<ClubType?>? OnFilterChanged;

        private int _activeIndex = 0;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            for (int i = 0; i < filterButtons.Length; i++)
            {
                int idx = i;
                if (filterButtons[i] != null)
                    filterButtons[i].onClick.AddListener(() => SetFilter(idx));
            }
            UpdateHighlights();
        }

        // ── Filter logic ──────────────────────────────────────────────────────

        public void SetFilter(int index)
        {
            if (index < 0 || index >= filterButtons.Length) return;
            _activeIndex = index;
            UpdateHighlights();
            OnFilterChanged?.Invoke(GetCurrentFilter());
            Debug.Log($"[ClubFilterBar] Filter set to: {(index == 0 ? "ALL" : GetCurrentFilter()!.ToString())}");
        }

        private void UpdateHighlights()
        {
            for (int i = 0; i < filterButtons.Length; i++)
            {
                if (filterButtons[i] == null) continue;
                bool active = (i == _activeIndex);

                var label = filterButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.color = active ? activeTextColor : inactiveTextColor;

                var img = filterButtons[i].GetComponent<Image>();
                if (img != null) img.color = active ? activeButtonColor : inactiveButtonColor;
            }
        }

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>Returns null for ALL, or the active ClubType filter.</summary>
        public ClubType? GetCurrentFilter()
            => _activeIndex == 0 ? (ClubType?)null : (ClubType)(_activeIndex - 1);
    }
}
