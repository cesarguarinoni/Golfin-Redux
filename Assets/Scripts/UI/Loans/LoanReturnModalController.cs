// asset_loans §4.2 RETURN — the confirm popup (Figma 14183:107541).
//
// A SIBLING PREFAB RATHER THAN SchemeConfirmModalController's SHELL. That controller's layout is
// three scheme tiles with three caption lines under a title — it is a scheme picker that happens to
// confirm, not a yes/no shell, and reusing it would have meant hiding six of its eight elements.
// This is the two-button confirm the node actually draws.
#nullable enable
using System.Collections;
using Golfin.EconomyRuntime;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using Golfin.UI.Toast;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans
{
    /// <summary>
    /// "Return {asset} to {owner} now? It goes back at Lv {n} — the levels you bought stay with it."
    ///
    /// <para>
    /// THE LEVEL IN THE BODY IS THE PROMISE THE WHOLE FEATURE MAKES, which is why it is rendered
    /// from the loan's CURRENT level rather than from what it was lent at: the borrower is being
    /// told, at the moment they give it back, exactly what they are handing over.
    /// </para>
    /// </summary>
    public sealed class LoanReturnModalController : ModalController
    {
        [SerializeField] private TextMeshProUGUI? titleText;
        [SerializeField] private TextMeshProUGUI? bodyText;
        [SerializeField] private Button? cancelButton;
        [SerializeField] private Button? returnButton;
        [SerializeField] private TextMeshProUGUI? returnButtonText;
        [SerializeField] private GameObject? spinner;

        private LoanDto? _loan;
        private bool _pending;

        protected override void Awake()
        {
            base.Awake();
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
            if (returnButton != null) returnButton.onClick.AddListener(OnReturn);
        }

        /// <summary>Open for one borrowed asset. <paramref name="assetName"/> is already localized.</summary>
        public void Open(LoanDto loan, string assetName)
        {
            if (loan == null) return;
            _loan = loan;
            _pending = false;

            if (titleText != null)
                titleText.text = LocalizationManager.Get(
                    loan.IsClub ? "LOAN_RETURN_TITLE_CLUB" : "LOAN_RETURN_TITLE_CHAR");

            if (bodyText != null)
                bodyText.text = string.Format(
                    LocalizationManager.Get("LOAN_RETURN_CONFIRM"),
                    assetName,
                    loan.Lender != null ? loan.Lender.Name : "PLAYER",
                    loan.Level);

            if (returnButtonText != null)
                returnButtonText.text = LocalizationManager.Get("LOAN_BTN_RETURN");

            SetPending(false);
            Show();
        }

        /// <summary>
        /// The latch's reset. The IN-FLIGHT half is <see cref="PendingSpend"/>'s
        /// (transaction_feedback §3) — it is the scope that disables RETURN and CANCEL, puts the
        /// ellipsis on the RETURN label, and restores all three from every exit path.
        ///
        /// <para><c>spinner</c> stays serialized and stays on the prefab, inactive, for the same
        /// reason as the lend modal's: the shared affordance needs no sprite, and removing a wired
        /// object is a prefab edit this task does not need.</para>
        /// </summary>
        private void SetPending(bool pending)
        {
            _pending = pending;
            if (cancelButton != null) cancelButton.interactable = !pending;
            if (returnButton != null) returnButton.interactable = !pending;
        }

        private void OnReturn()
        {
            if (_pending || _loan == null) return;
            StartCoroutine(ReturnRoutine(_loan));
        }

        private IEnumerator ReturnRoutine(LoanDto loan)
        {
            ApiResult<LoanMutationDto>? result = null;

            // Scope BEFORE latch — see LoanModalController.ConfirmRoutine for why the other order
            // hands back a dead button.
            using (PendingSpend.Begin(returnButton, returnButtonText, cancelButton!))
            {
                _pending = true;

                IEnumerator call = LoanService.Instance.Return(loan.Id, r => result = r);
                while (call.MoveNext()) yield return call.Current;
            }

            // Restore first, then act on the verdict.
            SetPending(false);

            if (result == null || !result.Success || result.Data == null)
            {
                // Transport failure: say nothing about the loan, leave the popup open. The return
                // is idempotent server-side, so a second tap is safe.
                if (!string.IsNullOrEmpty(PointsSpendGate.OfflineMessage))
                    ToastController.Instance?.Show(PointsSpendGate.OfflineMessage);
                yield break;
            }

            if (!result.Data.IsOk)
            {
                ToastController.Instance?.Show(LocalizationManager.Get(result.Data.ErrorKey()));
                yield break;
            }

            TelemetryService.Instance?.RecordSafe("loan_return",
                () => new Dictionary<string, object> { { "loan_id", loan.Id }, { "early", true } });

            // The refresh reconciles: the borrowed instance is removed, the bag is repaired if the
            // club was in one, and the RETURNED_IN toast is raised by the reconciler — NOT here,
            // so a return that lands on another device toasts the same way this one does.
            LoanSyncBehaviour.RequestRefresh("return");

            Hide();
        }
    }
}
