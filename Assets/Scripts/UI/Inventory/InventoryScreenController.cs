#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Utilities;

namespace Golfin.Inventory
{
    /// <summary>
    /// Top-level controller for the Inventory screen.
    /// Manages the CLUBS / BAGS / BALLS / ITEMS tab bar and shows/hides
    /// the corresponding content panels.
    ///
    /// Wired by InventoryScreenBuilder. Phase C adds the carousel + detail panel
    /// inside ClubsContent.
    /// </summary>
    public class InventoryScreenController : MonoBehaviour
    {
        [Header("Tab Buttons (CLUBS / BAGS / BALLS / ITEMS)")]
        [SerializeField] private Button[] tabButtons  = null!;

        [Header("Tab Content Panels")]
        [SerializeField] private GameObject[] tabPanels = null!;

        [Header("Tab Active Indicators (underline images, one per tab)")]
        [SerializeField] private Image[] tabIndicators = null!;

        [Header("References — set by builder")]
        [SerializeField] public ClubFilterBar? clubFilterBar;   // accessed by ClubCarouselController (Phase C)

        // Tab colors kept for fallback but gradient logic takes precedence in RefreshTabVisuals

        private int _activeTab = 0;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                if (tabButtons[i] != null)
                    tabButtons[i].onClick.AddListener(() => ShowTab(idx));
            }

            ShowTab(0);   // CLUBS active by default
        }

        // ── Tab management ────────────────────────────────────────────────────

        public void ShowTab(int index)
        {
            _activeTab = index;

            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i] != null)
                    tabPanels[i].SetActive(i == index);

            RefreshTabVisuals();
            Debug.Log($"[InventoryScreen] Tab {index} active.");
        }

        private void RefreshTabVisuals()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;

                var label = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    if (i == _activeTab)
                        TextGradients.ApplyGold(label);
                    else
                        TextGradients.ApplySilver(label);
                }
            }

            // Active indicator underlines (kept for scenes that still have them)
            for (int i = 0; i < tabIndicators.Length; i++)
                if (tabIndicators[i] != null)
                    tabIndicators[i].enabled = (i == _activeTab);
        }

        // ── Accessors ─────────────────────────────────────────────────────────

        public int GetActiveTab() => _activeTab;
    }
}
