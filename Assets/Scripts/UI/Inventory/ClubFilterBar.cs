#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Utilities;

namespace Golfin.Inventory
{
    /// <summary>
    /// Sub-filter bar inside the Club Inventory screen.
    /// Buttons: ALL | DRIVERS | WOODS | IRONS | WEDGES | PUTTERS
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
            // Dividers ignore the HorizontalLayoutGroup entirely (ignoreLayout = true)
            // and are positioned absolutely using RectTransform anchors.
            // With 8 evenly-distributed buttons, dividers sit at x = 1/8, 2/8 ... 7/8.
            int buttonCount = filterButtons.Length; // expected 6
            int dividerCount = buttonCount - 1;

            for (int i = 0; i < dividerCount; i++)
            {
                var divGO = new GameObject("FilterDivider");
                divGO.transform.SetParent(transform, false);

                // Ignore HLG so it doesn't resize or reposition the divider
                var le = divGO.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                // Position at the boundary between button i and i+1
                float xPos = (float)(i + 1) / buttonCount;
                var rt = divGO.GetComponent<RectTransform>();
                rt.anchorMin       = new Vector2(xPos, 0.15f);
                rt.anchorMax       = new Vector2(xPos, 0.85f);
                rt.sizeDelta       = new Vector2(1f, 0f); // 1px wide, height from anchors
                rt.anchoredPosition = Vector2.zero;

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

        /// <summary>Returns null for ALL, or the primary ClubType for the active tab.</summary>
        public ClubType? GetCurrentFilter() => _activeIndex switch
        {
            0 => null,              // ALL
            1 => ClubType.Driver,
            2 => ClubType.Wood,
            3 => ClubType.Iron,
            4 => ClubType.A_Wedge,  // sentinel for unified WEDGES tab — check IsWedgeFilter
            5 => ClubType.Putter,
            _ => null
        };

        /// <summary>True when the active filter is the unified WEDGES tab (covers A/P/S wedges).</summary>
        public bool IsWedgeFilter => _activeIndex == 4;
    }
}
