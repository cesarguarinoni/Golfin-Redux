// asset_loans §4.2 — the LEND modal (Figma 14183:32758 Roster / 14185:34162 Clubs).
// ONE prefab for both: the only difference between the two nodes is the equipped-club warning
// line, which is a toggle, not a second layout.
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
    /// Pick somebody you follow and a duration, and hand them a character or a club.
    ///
    /// <para>
    /// NOTHING IS OPTIMISTIC. The asset does not move locally until the server has said <c>ok</c>
    /// and the refresh that follows has reconciled — the same posture as the level-up modal, and
    /// for the same reason: the server can refuse for five different reasons the client cannot
    /// know about (the recipient unfollowed, they already have one, either side is at its limit),
    /// and a modal that had already moved the asset would have to move it back.
    /// </para>
    /// <para>
    /// ONE IDEMPOTENCY KEY PER TAP, minted when the modal opens and held for the life of it. A
    /// timed-out confirm retried with the same key returns the ORIGINAL loan rather than creating
    /// a second one.
    /// </para>
    /// </summary>
    public sealed class LoanModalController : ModalController
    {
        [Header("Title")]
        [SerializeField] private TextMeshProUGUI? titleText;

        [Header("Duration")]
        [SerializeField] private TextMeshProUGUI? durationLabel;
        [SerializeField] private Button? days1Button;
        [SerializeField] private Button? days3Button;
        [SerializeField] private Button? days7Button;
        [SerializeField] private TextMeshProUGUI? days1Text;
        [SerializeField] private TextMeshProUGUI? days3Text;
        [SerializeField] private TextMeshProUGUI? days7Text;

        [Header("Duration sprites (silver = unselected, gold = selected)")]
        [SerializeField] private Sprite? durationUnselectedSprite;
        [SerializeField] private Sprite? durationSelectedSprite;

        [Header("Terms + warning")]
        [SerializeField] private TextMeshProUGUI? termsText;
        [SerializeField] private GameObject? equippedWarningRoot;
        [SerializeField] private TextMeshProUGUI? equippedWarningText;

        [Header("Recipients")]
        [SerializeField] private TextMeshProUGUI? lendToLabel;
        [SerializeField] private Transform? recipientParent;
        [SerializeField] private GameObject? recipientRowPrefab;
        [SerializeField] private GameObject? emptyStateRoot;
        [SerializeField] private TextMeshProUGUI? emptyStateText;
        [SerializeField] private ScrollRect? recipientScroll;

        [Header("Footer")]
        [SerializeField] private Button? cancelButton;
        [SerializeField] private Button? confirmButton;
        [SerializeField] private TextMeshProUGUI? confirmText;
        [SerializeField] private GameObject? confirmSpinner;

        /// <summary>Placeholder rows drawn while <c>/social/following</c> is in flight. Four is the
        /// number the list shows before it scrolls, so the loading state is the same height as the
        /// state it becomes.</summary>
        private const int PlaceholderRows = 4;

        /// <summary>The default duration (Figma: 3 DAYS pre-selected).</summary>
        private const int DefaultDays = 3;

        private readonly List<LoanRecipientRow> _rows = new List<LoanRecipientRow>();

        private string _kind = LoanDto.KindCharacter;
        private string _refId = "";
        private int _level;
        private bool _clubIsEquipped;

        private int _days = DefaultDays;
        private string? _selectedUserId;
        private string _idempotencyKey = "";
        private bool _pending;

        protected override void Awake()
        {
            base.Awake();

            if (cancelButton  != null) cancelButton.onClick.AddListener(Hide);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (days1Button   != null) days1Button.onClick.AddListener(() => SelectDays(1));
            if (days3Button   != null) days3Button.onClick.AddListener(() => SelectDays(3));
            if (days7Button   != null) days7Button.onClick.AddListener(() => SelectDays(7));
        }

        /// <summary>
        /// Open for one asset.
        /// </summary>
        /// <param name="kind"><c>character</c> or <c>club</c>.</param>
        /// <param name="refId">The catalog row id being lent.</param>
        /// <param name="assetName">Already-localized display name, for the title.</param>
        /// <param name="level">The level this client believes the asset is at. The SERVER's row
        /// wins if the two disagree — this only ever seeds a progress row that does not exist yet
        /// (§1.3), which is the grandfather-once decision made by the right player.</param>
        /// <param name="clubIsEquipped">Show the "this will leave your bag" warning.</param>
        public void Open(string kind, string refId, string assetName, int level,
                         bool clubIsEquipped = false)
        {
            _kind  = kind;
            _refId = refId;
            _level = level;
            _clubIsEquipped = clubIsEquipped;

            _days = DefaultDays;
            _selectedUserId = null;
            _pending = false;
            _idempotencyKey = LoanService.NewKey();

            if (titleText != null)
                titleText.text = string.Format(LocalizationManager.Get("LOAN_MODAL_TITLE"),
                                               (assetName ?? refId ?? "").ToUpperInvariant());

            if (durationLabel != null) durationLabel.text = LocalizationManager.Get("LOAN_DURATION");
            if (days1Text != null) days1Text.text = LocalizationManager.Get("LOAN_DAYS_1");
            if (days3Text != null) days3Text.text = LocalizationManager.Get("LOAN_DAYS_3");
            if (days7Text != null) days7Text.text = LocalizationManager.Get("LOAN_DAYS_7");
            if (lendToLabel != null) lendToLabel.text = LocalizationManager.Get("LOAN_LEND_TO");
            if (confirmText != null) confirmText.text = LocalizationManager.Get("LOAN_BTN_CONFIRM");
            if (emptyStateText != null) emptyStateText.text = LocalizationManager.Get("LOAN_NO_FOLLOWING");

            if (equippedWarningText != null)
                equippedWarningText.text = LocalizationManager.Get("LOAN_WARN_EQUIPPED");
            if (equippedWarningRoot != null)
                equippedWarningRoot.SetActive(clubIsEquipped);

            PaintTerms();
            SelectDays(DefaultDays);
            ShowPlaceholderRows();
            SetPending(false);

            Show();

            TelemetryService.Instance?.RecordSafe("loan_modal_open",
                () => new Dictionary<string, object> { { "kind", _kind }, { "ref_id", _refId } });

            StartCoroutine(LoadRecipients());
        }

        /// <summary>
        /// The terms line, with the share read off a LIVE loan when there is one and off the
        /// server default otherwise.
        ///
        /// <para>
        /// NEVER A HARDCODED 20. The share is a server constant frozen onto each row, and the one
        /// number the player is being asked to agree to must come from the same place the money
        /// does. When no loan is in hand yet the constant mirrored here is the fallback, and it is
        /// named rather than inlined so the mirror is findable if the server's ever moves.
        /// </para>
        /// </summary>
        private void PaintTerms()
        {
            if (termsText == null) return;

            int bp = DefaultLenderShareBp;
            LoanService svc = LoanService.Instance;
            if (svc.Out.Count > 0 && svc.Out[0].LenderShareBp > 0) bp = svc.Out[0].LenderShareBp;
            else if (svc.In.Count > 0 && svc.In[0].LenderShareBp > 0) bp = svc.In[0].LenderShareBp;

            termsText.text = string.Format(LocalizationManager.Get("LOAN_TERMS_FMT"), bp / 100);
        }

        /// <summary>Mirrors <c>loans.py::LOAN_LENDER_SHARE_BP</c>. Used only before any loan row
        /// has been seen; a row's own <c>lender_share_bp</c> always wins.</summary>
        public const int DefaultLenderShareBp = 2000;

        // ── duration ─────────────────────────────────────────────────────────

        private void SelectDays(int days)
        {
            _days = days;
            PaintDayButton(days1Button, days == 1);
            PaintDayButton(days3Button, days == 3);
            PaintDayButton(days7Button, days == 7);
        }

        private void PaintDayButton(Button? button, bool selected)
        {
            if (button == null) return;
            Image? image = button.GetComponent<Image>();
            if (image == null) return;
            Sprite? sprite = selected ? durationSelectedSprite : durationUnselectedSprite;
            if (sprite != null) image.sprite = sprite;
        }

        // ── recipients ───────────────────────────────────────────────────────

        private void ShowPlaceholderRows()
        {
            ClearRows();
            if (emptyStateRoot != null) emptyStateRoot.SetActive(false);
            for (int i = 0; i < PlaceholderRows; i++)
            {
                LoanRecipientRow? row = SpawnRow();
                row?.BindPlaceholder();
            }
            UpdateConfirmEnabled();
        }

        private IEnumerator LoadRecipients()
        {
            ApiResult<List<FollowedUserDto>>? result = null;
            IEnumerator call = LoanService.Instance.Following(r => result = r);
            while (call.MoveNext()) yield return call.Current;

            // The modal may have been closed while the list was in flight.
            if (!IsVisible()) yield break;

            ClearRows();

            // ⚠️ ONE FRAME, BECAUSE `Destroy` IS DEFERRED TO END OF FRAME.
            //
            // The four placeholders are still children of the list for the rest of this frame, and
            // the layout group still counts them. `GpsPaintMotion.StaggerRise` force-rebuilds that
            // layout to learn each new row's REST position — and item 0's beat fires synchronously,
            // in this same frame — so spawning and staggering here reads the first real row's rest
            // slot as the one AFTER four dead placeholders and pins it there. Measured: row 0 at
            // y = -448 (four 112 px slots down, outside the viewport) while rows 1 and 2, whose
            // beats land on later frames, sat correctly at -112 and -224. The list drew with its
            // first name missing.
            //
            // Rows 1..n were only ever right by accident of timing, so the fix is the frame, not a
            // second layout rebuild: let the placeholders actually leave, THEN measure.
            yield return null;
            if (!IsVisible()) yield break;

            List<FollowedUserDto>? users = result != null && result.Success ? result.Data : null;

            if (users == null || users.Count == 0)
            {
                if (emptyStateRoot != null)
                {
                    emptyStateRoot.SetActive(true);
                    // §D4 — the empty card fades in with its data rather than replacing the
                    // placeholder rows in one frame. FadeInPanel only moves alpha, so the
                    // SetActive above is still what puts it on screen.
                    Golfin.Gps.UI.GpsPaintMotion.FadeInPanel(this, emptyStateRoot, animate: true);
                }
                UpdateConfirmEnabled();
                yield break;
            }

            if (emptyStateRoot != null) emptyStateRoot.SetActive(false);

            var arrived = new List<Transform>(users.Count);
            foreach (FollowedUserDto user in users)
            {
                if (user == null || string.IsNullOrEmpty(user.Id)) continue;
                LoanRecipientRow? row = SpawnRow();
                if (row == null) continue;
                row.Bind(user);
                row.Clicked += OnRowClicked;
                arrived.Add(row.transform);
            }

            if (recipientScroll != null) recipientScroll.verticalNormalizedPosition = 1f;

            // §D6 — the list ARRIVED, so it staggers in, exactly as StoreHistoryScreenController
            // staggers its first page (~358). Only these rows: the four `—` placeholders are the
            // state this list is replacing, not a thing that arrives. StaggerRise reads
            // `SuppressedByPush` itself and settles every row at alpha 1 when a push is on, so the
            // guard is not repeated here.
            if (arrived.Count > 0) Golfin.Gps.UI.GpsPaintMotion.StaggerRise(this, arrived);

            UpdateConfirmEnabled();
        }

        private LoanRecipientRow? SpawnRow()
        {
            if (recipientRowPrefab == null || recipientParent == null) return null;
            GameObject go = Instantiate(recipientRowPrefab, recipientParent);
            go.SetActive(true);
            LoanRecipientRow? row = go.GetComponent<LoanRecipientRow>();
            if (row != null) _rows.Add(row);
            return row;
        }

        private void ClearRows()
        {
            foreach (LoanRecipientRow row in _rows)
            {
                if (row == null) continue;
                row.Clicked -= OnRowClicked;
                Destroy(row.gameObject);
            }
            _rows.Clear();
            _selectedUserId = null;
        }

        private void OnRowClicked(LoanRecipientRow row)
        {
            if (_pending || row == null || string.IsNullOrEmpty(row.UserId)) return;

            _selectedUserId = row.UserId;
            foreach (LoanRecipientRow r in _rows)
                if (r != null) r.SetSelected(ReferenceEquals(r, row));

            // §D6 — the row that just BECAME selected bumps; the one that just lost the selection
            // does not. The sprite swap in SetSelected stays as it is: that is the fidelity
            // decision (a baked 3 px stroke, not an Outline), and this is the feel on top of it.
            UiSelection.Bump(this, row.transform);

            UpdateConfirmEnabled();
        }

        // ── confirm ──────────────────────────────────────────────────────────

        private void UpdateConfirmEnabled()
        {
            if (confirmButton != null)
                confirmButton.interactable = !_pending && !string.IsNullOrEmpty(_selectedUserId);
        }

        /// <summary>
        /// The latch, and the rest state that goes with it.
        ///
        /// <para>The IN-FLIGHT half of this now belongs to <see cref="PendingSpend"/>
        /// (transaction_feedback §3): the scope is what disables the two buttons and puts the
        /// ellipsis on LEND, and — more to the point — what puts them BACK on every exit path.
        /// This is left as the reset, which is all <see cref="Open"/> ever asked of it.</para>
        ///
        /// <para><c>confirmSpinner</c> is deliberately still serialized and still on the prefab,
        /// inactive: the shared affordance is the button's own Disabled transition plus the
        /// ellipsis, and deleting a wired object would be a prefab edit this task does not need.</para>
        /// </summary>
        private void SetPending(bool pending)
        {
            _pending = pending;
            if (cancelButton != null) cancelButton.interactable = !pending;
            UpdateConfirmEnabled();
        }

        private void OnConfirm()
        {
            if (_pending || string.IsNullOrEmpty(_selectedUserId)) return;
            StartCoroutine(ConfirmRoutine());
        }

        private IEnumerator ConfirmRoutine()
        {
            ApiResult<LoanMutationDto>? result = null;

            // THE SCOPE OPENS BEFORE THE LATCH, and the order is load-bearing. `PendingSpend`
            // caches CONFIRM's pre-tap `interactable` and hands exactly that back on dispose;
            // setting `_pending` first would run UpdateConfirmEnabled, cache "already disabled",
            // and restore a dead LEND button after a refusal.
            //
            // CANCEL rides along in `alsoDisable`: the lend is being recorded server-side and
            // closing the modal mid-flight would hide a loan that is about to land.
            using (PendingSpend.Begin(confirmButton, confirmText, cancelButton!))
            {
                _pending = true;

                IEnumerator call = LoanService.Instance.Lend(
                    _kind, _refId, _selectedUserId!, _days, _level, _idempotencyKey, r => result = r);
                while (call.MoveNext()) yield return call.Current;
            }

            // Restore FIRST, then act on the verdict (PendingSpend's ordering rule): restoring
            // means putting back what was there before the tap, and everything below overwrites
            // that with the new truth — a toast over a live modal, or a closed one.
            SetPending(false);

            // TRANSPORT FAILURE IS NOT A REFUSAL. Nothing is said about the loan, the modal stays
            // open with the SAME idempotency key, and a second tap either succeeds or replays the
            // one that actually landed. Telling the player "couldn't lend" here would be a guess.
            if (result == null || !result.Success || result.Data == null)
            {
                Toast(Golfin.EconomyRuntime.PointsSpendGate.OfflineMessage);
                yield break;
            }

            LoanMutationDto data = result.Data;

            if (!data.IsOk)
            {
                Toast(LocalizationManager.Get(data.ErrorKey()));
                yield break;
            }

            TelemetryService.Instance?.RecordSafe("loan_lend",
                () => new Dictionary<string, object>
                {
                    { "kind", _kind }, { "ref_id", _refId }, { "days", _days }
                });

            string recipient = data.Loan?.Borrower != null ? data.Loan.Borrower.Name : "";
            string duration  = LocalizationManager.Get(DurationKey(_days));

            // The refresh is what actually moves the asset: it reconciles, which sets isLentOut,
            // pulls the club out of the bag, and repaints both panels and every card.
            LoanSyncBehaviour.RequestRefresh("lend");

            Hide();

            Toast(string.Format(LocalizationManager.Get("LOAN_TOAST_LENT"),
                                LoanSyncBehaviour.AssetName(data.Loan), recipient, duration));
        }

        private static string DurationKey(int days) => days == 1 ? "LOAN_DAYS_1"
                                                     : days == 7 ? "LOAN_DAYS_7"
                                                     : "LOAN_DAYS_3";

        private static void Toast(string message)
        {
            if (!string.IsNullOrEmpty(message))
                ToastController.Instance?.Show(message);
        }

        public override void Hide()
        {
            ClearRows();
            base.Hide();
        }
    }
}
