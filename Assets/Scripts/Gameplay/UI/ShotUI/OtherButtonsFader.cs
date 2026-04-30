using UnityEngine;
using System.Collections.Generic;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Sits on ActionButtons_Cluster. Tracks individual action button CanvasGroups
    /// so that opening a selector can fade/disable everything except the triggering button.
    /// </summary>
    public class OtherButtonsFader : MonoBehaviour
    {
        [SerializeField] private List<CanvasGroup> _buttonGroups = new List<CanvasGroup>();

        /// <summary>
        /// True while any action-button selector overlay is open.
        /// PhysicsLabController reads this to block camera orbit input.
        /// </summary>
        public static bool AnyOverlayOpen { get; private set; }

        /// <summary>
        /// Fade all button CanvasGroups except <paramref name="keepGroup"/>.
        /// The kept group is hidden entirely (alpha=0) — its selector card visually replaces it.
        /// </summary>
        public void FadeAllExcept(CanvasGroup keepGroup)
        {
            AnyOverlayOpen = true;
            foreach (var cg in _buttonGroups)
            {
                if (cg == null) continue;
                if (cg == keepGroup)
                {
                    // Trigger button stays visible — user taps it again to close the selector.
                    cg.alpha = 1f;
                    cg.interactable   = true;
                    cg.blocksRaycasts = true;
                }
                else
                {
                    cg.alpha = 0.5f;
                    cg.interactable   = false;
                    cg.blocksRaycasts = false;
                }
            }
        }

        /// <summary>Reset all button CanvasGroups to fully interactive.</summary>
        public void RestoreAll()
        {
            AnyOverlayOpen = false;
            foreach (var cg in _buttonGroups)
            {
                if (cg == null) continue;
                cg.alpha = 1f;
                cg.interactable   = true;
                cg.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// Register a CanvasGroup. Called by the builder so the list stays in sync
        /// with whatever action buttons are built.
        /// </summary>
        public void RegisterGroup(CanvasGroup cg)
        {
            if (cg != null && !_buttonGroups.Contains(cg))
                _buttonGroups.Add(cg);
        }
    }
}
