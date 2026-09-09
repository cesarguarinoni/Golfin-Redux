// asset_loans §4.4 — the 44×44 loan badge on a thumbnail card. Shared by the character card and
// the club card (Figma 14182:32786 / 14182:107182 / 14183:109305 / 14183:109319), which are the
// same object at the same offset on both.
#nullable enable
using Golfin.UI.Polish;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans
{
    /// <summary>
    /// The circle at a card's TOP-LEFT that says this asset is out on loan or borrowed.
    ///
    /// <para>
    /// TOP-LEFT, BECAUSE THE Lv PILL OWNS TOP-RIGHT. The existing selected / level-up / stamina
    /// icon stack is untouched — a loan badge that displaced one of those would trade a new signal
    /// for a signal the player already reads.
    /// </para>
    /// </summary>
    public sealed class LoanBadgeView : MonoBehaviour
    {
        [Header("Prefab-authored — 44×44 circle at (8,8), 26×26 glyph centred")]
        [SerializeField] private GameObject? badgeRoot;
        [SerializeField] private Image? glyph;

        [Header("Icons (Assets/Art/RosterScreen)")]
        [SerializeField] private Sprite? iconLoanOutSmall;   // arrow UP — lent
        [SerializeField] private Sprite? iconLoanInSmall;    // arrow DOWN — borrowed

        /// <summary>Whether the badge was visible at the last <see cref="Apply"/>, and whether
        /// there HAS been one. Together they are the "did this change?" that gates the fade: the
        /// first paint of a card is not a change (fading a badge out on every card that has no loan
        /// would animate the whole carousel on arrival), and a repaint that says the same thing is
        /// not one either.</summary>
        private bool _shown;
        private bool _applied;

        public void Clear() => Apply(false, false);

        /// <summary>
        /// Show the badge in one of its two states, or hide it.
        /// </summary>
        /// <param name="lentOut">We own it and it is out.</param>
        /// <param name="borrowed">Somebody lent it to us.</param>
        public void Apply(bool lentOut, bool borrowed)
        {
            bool show = lentOut || borrowed;
            bool changed = _applied && show != _shown;
            _applied = true;
            _shown = show;

            // asset_loans_polish §3 — ALPHA IS THE STATE, not the active flag. `Indicator` leaves
            // the object active at alpha 0, which renders identically to a deactivated badge and
            // keeps the card's rest geometry to the pixel (the badge is anchored top-left with its
            // own rect, so it was never in a layout flow to begin with).
            if (badgeRoot != null)
                UiSelection.Indicator(this, badgeRoot.transform, show, animate: changed);
            if (!show) return;

            if (glyph != null)
            {
                // lentOut wins if both are somehow true — an asset cannot be lent and borrowed at
                // once, and if the two lists ever disagree the OURS reading is the safer one to
                // show, because it is the one that comes with a lock.
                Sprite? sprite = lentOut ? iconLoanOutSmall : iconLoanInSmall;
                if (sprite != null) glyph.sprite = sprite;
                glyph.enabled = sprite != null;
            }
        }
    }
}
