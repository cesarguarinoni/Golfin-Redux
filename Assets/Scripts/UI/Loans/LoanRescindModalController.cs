// asset_loans_offers §3.2 RESCIND — the confirm popup.
//
// A SIBLING OF LoanReturnModalController, built from the same shell (title + body + CANCEL +
// gold action), for the same reason that one is not SchemeConfirmModalController: this is a
// two-button yes/no and nothing else. The two are deliberately separate prefabs rather than one
// parameterised popup, because the RETURN body carries a level ("it goes back at Lv 47") and the
// RESCIND body carries a cooldown ("you can't offer them this again for 24h") — the shapes differ
// in what they must say, not just in their strings, and one controller taking both would be a
// pair of mutually exclusive branches wearing a trench coat.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using Golfin.EconomyRuntime;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using Golfin.UI.Toast;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans
{
    /// <summary>
    /// "Take back the offer to {0}? You can't offer them this again for {1}."
    ///
    /// <para>
    /// THE COOLDOWN IS IN THE BODY BECAUSE IT IS THE COST. Rescinding is free in every visible
    /// way — the asset unlocks, nothing is spent — and the one consequence is invisible until the
    /// player tries to re-offer and is refused. Telling them here is the difference between a
    /// deliberate choice and a surprise a day later.
    /// </para>
    /// <para>
    /// NOT A RECALL. This is only ever opened on an OFFERED asset (the detail panels relabel LEND
    /// to RESCIND for exactly that state), and the server answers <c>not_offered</c> on an active
    /// loan — so a race where the recipient accepts between the tap and the request refuses
    /// cleanly rather than yanking gear out of somebody's round.
    /// </para>
    /// </summary>
    public sealed class LoanRescindModalController : ModalController
    {
        [SerializeField] private TextMeshProUGUI? titleText;
        [SerializeField] private TextMeshProUGUI? bodyText;
        [SerializeField] private Button? cancelButton;
        [SerializeField] private Button? rescindButton;
        [SerializeField] private TextMeshProUGUI? rescindButtonText;

        /// <summary>
        /// Mirrors <c>loans.py::LOAN_REOFFER_COOLDOWN_HOURS</c>.
        ///
        /// <para>A MIRROR, AND IT IS NAMED SO IT IS FINDABLE. The number is the server's; nothing
        /// here enforces it. It is duplicated only to write the warning BEFORE any request has
        /// been made — there is no row in hand at this moment to read it off, the way the lender
        /// share is read off a live loan. If the server's ever moves, this is the one line that
        /// has to follow it, and the refusal toast (which formats the server's own
        /// <c>retry_after</c>) will be right either way.</para>
        /// </summary>
        public const int CooldownHours = 24;

        private LoanDto? _loan;
        private bool _pending;

        protected override void Awake()
        {
            base.Awake();
            if (cancelButton  != null) cancelButton.onClick.AddListener(Hide);
            if (rescindButton != null) rescindButton.onClick.AddListener(OnRescind);
        }

        /// <summary>Open for one offered asset. <paramref name="assetName"/> is already localized.</summary>
        public void Open(LoanDto loan, string assetName)
        {
            if (loan == null) return;
            _loan = loan;
            _pending = false;

            if (titleText != null)
                titleText.text = LocalizationManager.Get("LOAN_RESCIND_TITLE");

            if (bodyText != null)
                bodyText.text = string.Format(
                    LocalizationManager.Get("LOAN_RESCIND_CONFIRM_FMT"),
                    loan.Borrower != null ? loan.Borrower.Name : LoanPartyDto.Fallback,
                    // "24h", not LOAN_TIME_H_M_FMT's "24h 0m". The minutes half of that format
                    // exists to make the LAST hours of a running loan readable; on a fixed
                    // whole-hour cooldown it is always zero, and " 0m" reads like a bug.
                    string.Format(LocalizationManager.Get("LOAN_TIME_TO_ANSWER_HOURS_FMT"),
                                  CooldownHours));

            if (rescindButtonText != null)
                rescindButtonText.text = LocalizationManager.Get("LOAN_BTN_RESCIND");

            SetPending(false);
            Show();
        }

        /// <summary>The latch's reset. The IN-FLIGHT half is <see cref="PendingSpend"/>'s — the
        /// scope disables both buttons, puts the ellipsis on RESCIND, and restores all of it from
        /// every exit path.</summary>
        private void SetPending(bool pending)
        {
            _pending = pending;
            if (cancelButton  != null) cancelButton.interactable  = !pending;
            if (rescindButton != null) rescindButton.interactable = !pending;
        }

        private void OnRescind()
        {
            if (_pending || _loan == null) return;
            StartCoroutine(RescindRoutine(_loan));
        }

        private IEnumerator RescindRoutine(LoanDto loan)
        {
            ApiResult<LoanMutationDto>? result = null;

            // Scope BEFORE latch — see LoanModalController.ConfirmRoutine for why the other order
            // hands back a dead button.
            using (PendingSpend.Begin(rescindButton, rescindButtonText, cancelButton!))
            {
                _pending = true;

                IEnumerator call = LoanService.Instance.Rescind(loan.Id, r => result = r);
                while (call.MoveNext()) yield return call.Current;
            }

            SetPending(false);

            if (result == null || !result.Success || result.Data == null)
            {
                // Transport failure: say nothing about the offer, leave the popup open. Rescind
                // is idempotent server-side, so a second tap is safe.
                if (!string.IsNullOrEmpty(PointsSpendGate.OfflineMessage))
                    ToastController.Instance?.Show(PointsSpendGate.OfflineMessage);
                yield break;
            }

            if (!result.Data.IsOk)
            {
                // The interesting refusal here is `not_offered`, and it means the recipient
                // accepted while this popup was open. LOAN_ERR_OFFER_GONE says so; the refresh
                // below then repaints the panel from OFFERED into ON LOAN, which is the truth.
                ToastController.Instance?.Show(
                    LocalizationManager.Get(result.Data.AnswerErrorKey()));
                LoanSyncBehaviour.RequestRefresh("rescind-refused");
                Hide();
                yield break;
            }

            TelemetryService.Instance?.RecordSafe("loan_offer_rescinded",
                () => new Dictionary<string, object> { { "loan_id", loan.Id } });

            // The refresh is what unlocks the asset: it reconciles the now-`rescinded` row through
            // ClearLentOut, which drops the lock and raises LOAN_TOAST_RESCINDED_FMT — NOT here,
            // so a rescind performed on another device toasts the same way this one does.
            LoanSyncBehaviour.RequestRefresh("rescind");

            Hide();
        }
    }
}
