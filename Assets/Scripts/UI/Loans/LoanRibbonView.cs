// asset_loans §4.1 — the ON LOAN / BORROWED ribbon over a detail panel's portrait, plus the
// lender-side dim. One component, used by BOTH detail panels, because the two are pixel-identical
// (Figma 14182:32760 / 14182:107177 / 14183:109280 / 14183:109309) and a second copy would be a
// second place for the time formatting to drift.
#nullable enable
using System;
using Golfin.Social;
using Golfin.UI.Polish;
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
    /// <para>
    /// AN OFFERED ASSET DIMS TOO (asset_loans_offers §3.2). It is locked from the moment the
    /// offer goes out, so from the lender's side it is exactly as unavailable as a lent one —
    /// only the sentence differs (OFFERED TO … · 46h to answer), and that is
    /// <see cref="LabelFor"/>'s job, not this method's.
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

        [Header("Motion (asset_loans_polish §3 — authored by LoanUiBuilder, alpha 1 at rest)")]
        [SerializeField] private CanvasGroup? ribbonGroup;
        [SerializeField] private CanvasGroup? dimGroup;

        private Coroutine? _ribbonMotion;
        private Coroutine? _dimMotion;

        /// <summary>
        /// The LOGICAL state, which is not the same question as <c>ribbonRoot.activeSelf</c>.
        ///
        /// <para>A fade-out owns the ribbon's active flag until it finishes, so during those 150 ms
        /// the object is still active while the ribbon is on its way out. Asking the GameObject
        /// would answer "visible" and swallow the entrance of a <see cref="Show"/> that lands in
        /// that window; this flag answers the question the entrance actually asks.</para>
        /// </summary>
        private bool _shown;

        /// <summary>Hide everything. The state for an asset with no loan on it, which is almost
        /// every asset almost all the time.</summary>
        public void Clear()
        {
            if (!_shown)
            {
                // Already hidden — and a no-op it must stay: Clear() is called on every repaint of
                // every un-lent asset, and re-arming a fade there would run one per carousel step.
                return;
            }
            _shown = false;

            CanvasGroup? dim = Group(lentDim, ref dimGroup);
            if (dim != null) UiMotion.Run(this, ref _dimMotion, UiMotion.Fade(dim, dim.alpha, 0f));

            CanvasGroup? rib = Group(ribbonRoot, ref ribbonGroup);
            if (rib == null)
            {
                if (ribbonRoot != null) ribbonRoot.SetActive(false);
                if (lentDim != null) lentDim.SetActive(false);
                return;
            }

            // BOTH objects are deactivated from the RIBBON's tail, not one tail each. The runner
            // that drives these two tweens lives on this GameObject, so the moment the ribbon's
            // tail deactivates it the dim's fade is settled to its own final value anyway — a
            // second tail would only be a second place for the pair to disagree.
            UiMotion.Run(this, ref _ribbonMotion, UiMotion.Then(UiMotion.Fade(rib, rib.alpha, 0f), () =>
            {
                if (lentDim != null) lentDim.SetActive(false);
                if (ribbonRoot != null) ribbonRoot.SetActive(false);
            }));
        }

        /// <summary>Settle both tweens before Unity throws the coroutines away (the
        /// <c>UiMotion.Register</c> contract): leaving the screen mid-entrance leaves the ribbon at
        /// rest, and leaving it mid-Clear leaves it hidden.</summary>
        private void OnDisable()
        {
            UiMotion.Stop(this, ref _ribbonMotion);
            UiMotion.Stop(this, ref _dimMotion);
        }

        /// <summary>
        /// The object's CanvasGroup, preferring the prefab-authored reference.
        ///
        /// <para>The runtime add is the fallback for a panel built before this task — the two
        /// ribbons live in a scene and two prefabs that are only rebuilt when somebody runs
        /// <c>LoanUiBuilder</c>, and a null group there would silently cost the entrance rather
        /// than announcing itself. A CanvasGroup adds no geometry, so rest parity holds either
        /// way.</para>
        /// </summary>
        private static CanvasGroup? Group(GameObject? go, ref CanvasGroup? cached)
        {
            if (cached != null) return cached;
            if (go == null) return null;
            // `== null`, not `??`: GetComponent hands back a fake-null on a missing component.
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cached = cg;
            return cg;
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

            // ENTRANCE ONLY ON HIDDEN -> VISIBLE. Show() is also the tick: the panel repaints this
            // ribbon every second to move the time-left label on, and an entrance re-armed there
            // would drop the bar in from above once a second for the life of the loan.
            bool entering = !_shown;
            _shown = true;

            if (entering)
            {
                // A fade-out in flight owns both objects' active flags (its tail deactivates them).
                // Settle it BEFORE re-activating, or the tail lands after this call and hides the
                // ribbon we just showed.
                UiMotion.Stop(this, ref _ribbonMotion);
                UiMotion.Stop(this, ref _dimMotion);
            }

            if (ribbonRoot != null) ribbonRoot.SetActive(true);
            if (lentDim != null) lentDim.SetActive(asLender);

            if (ribbonIcon != null)
            {
                Sprite? sprite = asLender ? iconLoanOutBig : iconLoanInBig;
                if (sprite != null) ribbonIcon.sprite = sprite;
                ribbonIcon.enabled = sprite != null;
            }

            if (ribbonLabel != null) ribbonLabel.text = LabelFor(loan, asLender);

            if (!entering) return;

            // DOWN, NOT UP. `UiMotion.RiseRoutine` starts the rect at `restY - dy` and lerps to
            // rest — "rect.anchoredPosition = new Vector2(x, restY - dy);" (UiMotion.cs:437) — so
            // the default +RiseDy starts BELOW rest and rises. The ribbon sits at the TOP edge of
            // the portrait: it reads as a bar dropping onto the artwork, which is `restY + 16`,
            // which is dy = -RiseDy.
            var rect = ribbonRoot != null ? ribbonRoot.transform as RectTransform : null;
            if (rect != null)
                UiMotion.Run(this, ref _ribbonMotion,
                             UiMotion.Rise(rect, Group(ribbonRoot, ref ribbonGroup), dy: -UiMotion.RiseDy));

            if (!asLender) return;
            CanvasGroup? dim = Group(lentDim, ref dimGroup);
            if (dim != null) UiMotion.Run(this, ref _dimMotion, UiMotion.Fade(dim, 0f, 1f));
        }

        /// <summary>
        /// The ribbon's sentence — three of them now, picked off the loan's own status.
        ///
        /// <para>
        /// OFFERED READS ITS OWN CLOCK. A pending offer has no <c>ends_at</c> at all (the loan's
        /// days do not start until the recipient accepts), so formatting it through
        /// <see cref="FormatTimeLeft"/> would print "0h 0m" — the loan clock's honest answer to
        /// a question that has not been asked yet. What the lender needs is the OFFER's clock:
        /// how long the recipient still has to answer, which is <c>offer_expires_at</c>.
        /// </para>
        /// <para>
        /// PUBLIC AND STATIC so an EditMode test can pin all three without a scene.
        /// </para>
        /// </summary>
        public static string LabelFor(LoanDto loan, bool asLender)
        {
            if (loan == null) return "";

            if (asLender && loan.IsPendingOffer())
            {
                string hours = string.Format(LocalizationManager.Get("LOAN_TIME_TO_ANSWER_FMT"),
                                             loan.OfferHoursLeft());
                return string.Format(LocalizationManager.Get("LOAN_STATUS_OFFERED_FMT"),
                                     Name(loan.Borrower), hours);
            }

            string other = asLender ? Name(loan.Borrower) : Name(loan.Lender);
            string key = asLender ? "LOAN_STATUS_OUT_FMT" : "LOAN_STATUS_IN_FMT";
            return string.Format(LocalizationManager.Get(key), other,
                                 FormatTimeLeft(loan.TimeLeft()));
        }

        private static string Name(LoanPartyDto? p) => p != null ? p.Name : LoanPartyDto.Fallback;

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
