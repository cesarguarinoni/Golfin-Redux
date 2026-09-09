// asset_loans §4.4 — the 44×44 loan badge on a thumbnail card. Shared by the character card and
// the club card (Figma 14182:32786 / 14182:107182 / 14183:109305 / 14183:109319), which are the
// same object at the same offset on both.
#nullable enable
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

        public void Clear()
        {
            if (badgeRoot != null) badgeRoot.SetActive(false);
        }

        /// <summary>
        /// Show the badge in one of its two states, or hide it.
        /// </summary>
        /// <param name="lentOut">We own it and it is out.</param>
        /// <param name="borrowed">Somebody lent it to us.</param>
        public void Apply(bool lentOut, bool borrowed)
        {
            bool show = lentOut || borrowed;
            if (badgeRoot != null) badgeRoot.SetActive(show);
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
