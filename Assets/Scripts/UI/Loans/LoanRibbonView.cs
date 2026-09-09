// asset_loans §4.1 — the ON LOAN / BORROWED ribbon over a detail panel's portrait, plus the
// lender-side dim. One component, used by BOTH detail panels, because the two are pixel-identical
// (Figma 14182:32760 / 14182:107177 / 14183:109280 / 14183:109309) and a second copy would be a
// second place for the time formatting to drift.
#nullable enable
using System;
using Golfin.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans
{
    /// <summary>
    /// The 537×72 bar across the top of a detail panel's Left (portrait) panel, and the full-panel
    /// dim underneath it.
    ///
    /// <para>
    /// THE RIBBON AND THE CARD BADGE ARE THE ONLY STATUS CARRIERS (Cesar, 2026-09-09) — there is
    /// deliberately no icon beside the name and no third indicator to keep in sync.
    /// </para>
    /// <para>
    /// THE DIM IS LENDER-ONLY. A lent asset is not yours to use right now, and the dim is what says
    /// so at a glance; a BORROWED asset is fully playable, so dimming it would be a lie about the
    /// one thing the borrower most needs to know.
    /// </para>
    /// </summary>
    public sealed class LoanRibbonView : MonoBehaviour
    {
        [Header("Ribbon (prefab-authored — 537×72, top of the Left panel)")]
        [SerializeField] private GameObject? ribbonRoot;
        [SerializeField] private Image? ribbonIcon;
        [SerializeField] private TextMeshProUGUI? ribbonLabel;

        [Header("Icons (Assets/Art/RosterScreen)")]
        [SerializeField] private Sprite? iconLoanOutBig;   // arrow UP — ours, gone out
        [SerializeField] private Sprite? iconLoanInBig;    // arrow DOWN — somebody else's, come in

        [Header("Lender dim (full Left panel, UNDER the ribbon)")]
        [SerializeField] private GameObject? lentDim;

        /// <summary>Hide everything. The state for an asset with no loan on it, which is almost
        /// every asset almost all the time.</summary>
        public void Clear()
        {
            if (ribbonRoot != null) ribbonRoot.SetActive(false);
            if (lentDim != null) lentDim.SetActive(false);
        }

        /// <summary>
        /// Paint the ribbon for one loan.
        /// </summary>
        /// <param name="loan">The live loan. Null clears.</param>
        /// <param name="asLender">True when WE lent it out (ON LOAN + dim); false when we borrowed
        /// it (BORROWED, no dim).</param>
        public void Show(LoanDto? loan, bool asLender)
        {
            if (loan == null) { Clear(); return; }

            if (ribbonRoot != null) ribbonRoot.SetActive(true);
            if (lentDim != null) lentDim.SetActive(asLender);

            if (ribbonIcon != null)
            {
                Sprite? sprite = asLender ? iconLoanOutBig : iconLoanInBig;
                if (sprite != null) ribbonIcon.sprite = sprite;
                ribbonIcon.enabled = sprite != null;
            }

            if (ribbonLabel != null)
            {
                string other = asLender ? Name(loan.Borrower) : Name(loan.Lender);
                string key = asLender ? "LOAN_STATUS_OUT_FMT" : "LOAN_STATUS_IN_FMT";
                ribbonLabel.text = string.Format(LocalizationManager.Get(key),
                                                 other, FormatTimeLeft(loan.TimeLeft()));
            }
        }

        private static string Name(LoanPartyDto? p) => p != null ? p.Name : "PLAYER";

        /// <summary>
        /// "2d 4h" once a day or more is left, "5h 12m" below that.
        ///
        /// <para>
        /// PUBLIC AND STATIC so an EditMode test can pin the boundary without a scene. The two
        /// formats exist because a loan is 1, 3 or 7 days: days-and-hours is the useful reading for
        /// most of its life and minutes are noise, but in the last day the hour count alone stops
        /// being enough to decide whether to squeeze in one more round.
        /// </para>
        /// <para>
        /// Floors rather than rounds, so the number never claims more time than there is.
        /// </para>
        /// </summary>
        public static string FormatTimeLeft(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;

            if (left.TotalDays >= 1d)
                return string.Format(LocalizationManager.Get("LOAN_TIME_D_H_FMT"),
                                     (int)left.TotalDays, left.Hours);

            return string.Format(LocalizationManager.Get("LOAN_TIME_H_M_FMT"),
                                 (int)left.TotalHours, left.Minutes);
        }
    }
}
