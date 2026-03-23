#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Utilities;

namespace Golfin.Inventory
{
    /// <summary>
    /// Sub-filter bar inside the Club Inventory screen.
    /// Buttons: ALL | DRIVERS | WOODS | IRONS | A.WEDGES | P.WEDGES | S.WEDGES | PUTTERS
    ///
    /// Active tab: gold gradient text. Inactive tabs: silver gradient text.
    /// Thin vertical dividers (rgba 255,255,255,0.3) are injected between buttons on Start.
    ///
    /// Fires OnFilterChanged (null = ALL) which ClubCarouselController listens to.
    /// Button index maps to ClubType enum: index 0 = ALL, index N = (ClubType)(N-1).
    /// </summary>
    public class ClubFilterBar : MonoBehaviour
    {
        [SerializeField] private Button[] filterButtons = null!;

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

            InjectDividers();
            UpdateHighlights();
        }

        // ── Dividers ──────────────────────────────────────────────────────────

        private void InjectDividers()
        {
            // Insert a thin vertical divider Image after each filter button (except the last).
            // Works inside HorizontalLayoutGroup: fixed 1px width via LayoutElement.
            for (int i = filterButtons.Length - 1; i >= 1; i--)
            {
                if (filterButtons[i] == null) continue;

                var divGO = new GameObject("FilterDivider");
                divGO.transform.SetParent(transform, false);
                divGO.transform.SetSiblingIndex(filterButtons[i].transform.GetSiblingIndex());

                var le = divGO.AddComponent<LayoutElement>();
                le.preferredWidth  = 1f;
                le.flexibleWidth   = 0f;
                le.preferredHeight = 24f;

                var img = divGO.AddComponent<Image>();
                img.color         = new Color(1f, 1f, 1f, 0.3f);
                img.raycastTarget = false;
            }
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
                if (label != null)
                {
                    if (active)
                        TextGradients.ApplyGold(label);
                    else
                        TextGradients.ApplySilver(label);
                }
            }
        }

        // ── Accessors ─────────────────────────────────────────────────────────

        /// <summary>Returns null for ALL, or the active ClubType filter.</summary>
        public ClubType? GetCurrentFilter()
            => _activeIndex == 0 ? (ClubType?)null : (ClubType)(_activeIndex - 1);
    }
}
