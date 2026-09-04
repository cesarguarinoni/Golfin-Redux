#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Utilities;
using Golfin.UI.Polish;

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

        /// <summary>
        /// game_polish_a §D3 — the four tab panels cross-fade instead of snapping.
        ///
        /// <para>The outgoing panel dissolves out while the incoming one dissolves in, over
        /// <c>UiMotion.FadeDur</c>; the two overlap, which is what makes it read as one control
        /// changing rather than two panels taking turns. The FIRST call (from Start) is not
        /// animated — a screen opening should already be on its tab, not arrive and then change to
        /// it (§D7's "no motion on a cold screen", same rule).</para>
        ///
        /// <para>§D6: the tab that was tapped bumps. The bump is on the BUTTON, not the panel, so
        /// it is the control the finger touched that answers.</para>
        /// </summary>
        public void ShowTab(int index)
        {
            bool animate = !_firstTab && index != _activeTab;
            int previous = _activeTab;
            _firstTab = false;
            _activeTab = index;

            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] == null) continue;
                bool show = i == index;

                if (!animate)
                {
                    tabPanels[i].SetActive(show);
                    continue;
                }
                // Only the two panels involved move; the other two were already hidden and
                // fading them from 0 to 0 would start two pointless coroutines per tap.
                if (i == index || i == previous)
                    UiSelection.CrossFade(this, show ? tabPanels[i] : null,
                                                show ? null : tabPanels[i], animate: true);
                else
                    tabPanels[i].SetActive(false);
            }

            RefreshTabVisuals(animate);

            if (animate && index >= 0 && index < tabButtons.Length && tabButtons[index] != null)
                UiSelection.Bump(this, tabButtons[index].transform);

            Debug.Log($"[InventoryScreen] Tab {index} active.");
        }

        /// <summary>The first ShowTab (from Start) paints the rest state and must not animate.</summary>
        private bool _firstTab = true;

        private void RefreshTabVisuals(bool animate = false)
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

            // Active indicator underlines (kept for scenes that still have them).
            // game_polish_a §D3: cross-faded rather than replaced by one sliding indicator —
            // sliding would mean deleting three authored Images and re-anchoring the fourth, which
            // changes the tab bar's REST geometry and breaks A2's 0 px parity. UiSelection.Indicator
            // drives alpha and leaves the Image enabled, which renders identically at rest.
            for (int i = 0; i < tabIndicators.Length; i++)
                UiSelection.Indicator(this, tabIndicators[i], i == _activeTab, animate);
        }

        // ── Accessors ─────────────────────────────────────────────────────────

        public int GetActiveTab() => _activeTab;
    }
}
