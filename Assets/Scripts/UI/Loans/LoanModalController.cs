// asset_loans §4.2 — the LEND modal (Figma 14183:32758 Roster / 14185:34162 Clubs).
// asset_loans_offers §3.1 — v2: a display-name SEARCH above the followed list (Figma
// 14261:109475, field 14261:109851), because anyone can be lent to now.
// ONE prefab for both kinds: the only difference between the two nodes is the equipped-club
// warning line, which is a toggle, not a second layout.
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
    /// Pick anybody and a duration, and OFFER them a character or a club.
    ///
    /// <para>
    /// TWO SECTIONS, ONE SELECTION. RESULTS (whatever the search field found) sits above
    /// PEOPLE YOU FOLLOW (the default suggestions). A row in either can be picked and the
    /// other section clears — <see cref="_selectedUserId"/> is a single field for exactly that
    /// reason, and both sections spawn the same <see cref="LoanRecipientRow"/> so there is one
    /// selected-sprite rule rather than two.
    /// </para>
    /// <para>
    /// LEND IS NOW AN OFFER. The server writes an `offered` row and the recipient decides; the
    /// success toast says "Offered X to Y" rather than "Lent", and the asset locks on the
    /// refresh below exactly as a lent one does.
    /// </para>
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

        [Header("Search (asset_loans_offers §3.1 — Figma 14261:109851)")]
        [SerializeField] private TMP_InputField? searchField;
        [SerializeField] private TextMeshProUGUI? searchPlaceholder;
        [SerializeField] private GameObject? resultsSectionRoot;
        [SerializeField] private TextMeshProUGUI? resultsHeader;
        [SerializeField] private Transform? resultsParent;
        [SerializeField] private GameObject? noResultsRoot;
        [SerializeField] private TextMeshProUGUI? noResultsText;
        [SerializeField] private TextMeshProUGUI? followedHeader;

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

        /// <summary>How long after the last keystroke before the search goes out.</summary>
        private const float SearchDebounceSeconds = 0.3f;

        /// <summary>Followed rows. Separate list from <see cref="_resultRows"/> so a new search
        /// can replace one section without destroying the other — and so the selection can
        /// survive in whichever section it lives in.</summary>
        private readonly List<LoanRecipientRow> _rows = new List<LoanRecipientRow>();

        private readonly List<LoanRecipientRow> _resultRows = new List<LoanRecipientRow>();

        private Coroutine? _searchRoutine;

        /// <summary>
        /// Monotonic id of the most recently ISSUED search.
        ///
        /// <para>THE STALE-RESPONSE GUARD. Typing "k", "ke", "ken" can put three requests in
        /// flight, and they can come back in any order — a slow "k" landing after a fast "ken"
        /// would repaint the list with results for a query the field no longer holds, and the
        /// player would watch their search un-narrow itself. Every response compares its own
        /// ticket against this and drops itself if a newer one has already gone out.</para>
        /// </summary>
        private int _searchTicket;

        private string _kind = LoanDto.KindCharacter;
        private string _refId = "";
        private int _level;
        private bool _clubIsEquipped;

        private int _days = DefaultDays;
        private string? _selectedUserId;
        /// <summary>Telemetry only — `via: search|followed` on <c>loan_offer_sent</c>.</summary>
        private bool _selectedViaSearch;
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

            // onValueChanged, not onEndEdit: the field filters as you type (Figma shows "ken|"
            // with a live result list), and onEndEdit only fires when focus leaves — which on a
            // modal with no other focusable field is never.
            if (searchField != null) searchField.onValueChanged.AddListener(OnSearchChanged);
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

            // ── search, reset to empty on every open ──────────────────────────
            // SetTextWithoutNotify: assigning `.text` would fire onValueChanged and queue a
            // debounce for the empty string on every single open.
            if (searchField != null) searchField.SetTextWithoutNotify("");
            if (searchPlaceholder != null)
                searchPlaceholder.text = LocalizationManager.Get("LOAN_SEARCH_PLACEHOLDER");
            if (resultsHeader != null)
                resultsHeader.text = LocalizationManager.Get("LOAN_SEARCH_RESULTS");
            if (followedHeader != null)
                followedHeader.text = LocalizationManager.Get("LOAN_FOLLOWED_HEADER");
            if (noResultsText != null)
                noResultsText.text = LocalizationManager.Get("LOAN_NO_RESULTS");
            ClearResultRows();
            ShowResultsSection(false);

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

        // ── search (asset_loans_offers §3.1) ─────────────────────────────────

        /// <summary>
        /// A keystroke. Restart the debounce; an empty field hides RESULTS immediately.
        ///
        /// <para>THE EMPTY CASE DOES NOT WAIT. Clearing the field is an instruction ("show me
        /// the followed list again"), not a query — making the player watch 300 ms of stale
        /// results after they emptied the box would read as lag.</para>
        /// </summary>
        private void OnSearchChanged(string query)
        {
            if (_searchRoutine != null) { StopCoroutine(_searchRoutine); _searchRoutine = null; }

            if (string.IsNullOrWhiteSpace(query))
            {
                // Any answer still in flight is now stale by construction.
                _searchTicket++;
                ClearResultRows();
                ShowResultsSection(false);
                UpdateConfirmEnabled();
                return;
            }

            _searchRoutine = StartCoroutine(SearchRoutine(query));
        }

        private IEnumerator SearchRoutine(string query)
        {
            float waited = 0f;
            while (waited < SearchDebounceSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            int ticket = ++_searchTicket;

            ApiResult<List<FollowedUserDto>>? result = null;
            IEnumerator call = LoanService.Instance.SearchUsers(query, r => result = r);
            while (call.MoveNext()) yield return call.Current;

            _searchRoutine = null;

            if (!IsVisible()) yield break;

            // THE STALE GUARD. A newer keystroke has already gone out — this answer describes a
            // query the player has moved past, and painting it would un-narrow their search.
            if (ticket != _searchTicket) yield break;

            // NO SHIMMER, and no placeholder rows either (the GameShimmerSites rule): one ~200 ms
            // request does not earn a loading state, and a skeleton that appears and vanishes on
            // every keystroke is noise. The region simply repaints.
            ClearResultRows();

            // ⚠️ ONE FRAME, BECAUSE `Destroy` IS DEFERRED TO END OF FRAME — the same lesson as
            // LoadRecipients below, and it bites identically here: StaggerRise force-rebuilds the
            // layout to read each row's rest position, and the rows it is measuring past are the
            // ones we just destroyed. Without this the first result pins itself below however
            // many corpses the previous query left.
            yield return null;
            if (!IsVisible() || ticket != _searchTicket) yield break;

            List<FollowedUserDto>? users = result != null && result.Success ? result.Data : null;

            ShowResultsSection(true);

            if (users == null || users.Count == 0)
            {
                // A transport failure and "nobody by that name" render the same, deliberately:
                // the field is a search, the honest answer either way is "no rows", and a toast
                // per keystroke on a flaky connection would be unusable.
                if (noResultsRoot != null) noResultsRoot.SetActive(true);
                UpdateConfirmEnabled();
                yield break;
            }

            if (noResultsRoot != null) noResultsRoot.SetActive(false);

            var arrived = new List<Transform>(users.Count);
            foreach (FollowedUserDto user in users)
            {
                if (user == null || string.IsNullOrEmpty(user.Id)) continue;
                LoanRecipientRow? row = SpawnRow(resultsParent, _resultRows);
                if (row == null) continue;
                row.Bind(user);
                row.Clicked += OnRowClicked;
                // The selection survives a re-search when the same player is still in the list —
                // otherwise typing one more letter would silently disarm the LEND button under a
                // row that is visibly still highlighted.
                row.SetSelected(string.Equals(row.UserId, _selectedUserId, System.StringComparison.Ordinal));
                arrived.Add(row.transform);
            }

            if (arrived.Count > 0) Golfin.Gps.UI.GpsPaintMotion.StaggerRise(this, arrived);

            UpdateConfirmEnabled();
        }

        private void ShowResultsSection(bool shown)
        {
            if (resultsSectionRoot != null) resultsSectionRoot.SetActive(shown);
            if (!shown && noResultsRoot != null) noResultsRoot.SetActive(false);
        }

        private void ClearResultRows()
        {
            foreach (LoanRecipientRow row in _resultRows)
            {
                if (row == null) continue;
                row.Clicked -= OnRowClicked;
                Destroy(row.gameObject);
            }
            _resultRows.Clear();
            // NOTE: `_selectedUserId` is deliberately NOT cleared here. The selection belongs to
            // the modal, not to a section — a player who picked somebody, then typed one more
            // letter, has not changed their mind about who they are lending to.
        }

        private LoanRecipientRow? SpawnRow() => SpawnRow(recipientParent, _rows);

        /// <summary>Spawn one row under <paramref name="parent"/> and track it in
        /// <paramref name="into"/>. Two sections, one row type, one tracking mechanism.</summary>
        private LoanRecipientRow? SpawnRow(Transform? parent, List<LoanRecipientRow> into)
        {
            if (recipientRowPrefab == null || parent == null) return null;
            GameObject go = Instantiate(recipientRowPrefab, parent);
            go.SetActive(true);
            LoanRecipientRow? row = go.GetComponent<LoanRecipientRow>();
            if (row != null) into.Add(row);
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
            _selectedViaSearch = false;
        }

        private void OnRowClicked(LoanRecipientRow row)
        {
            if (_pending || row == null || string.IsNullOrEmpty(row.UserId)) return;

            _selectedUserId = row.UserId;
            // BOTH sections, because the selection is one thing: picking a search result has to
            // visibly un-pick whoever was highlighted in PEOPLE YOU FOLLOW, or the modal shows
            // two selected rows and lends to only one of them.
            _selectedViaSearch = _resultRows.Contains(row);
            foreach (LoanRecipientRow r in _rows)
                if (r != null) r.SetSelected(ReferenceEquals(r, row));
            foreach (LoanRecipientRow r in _resultRows)
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
                // COOLDOWN IS THE ONE REFUSAL THE DTO CANNOT FORMAT FOR ITSELF: its sentence
                // takes the recipient's NAME, which only this modal knows (the server answers
                // with an id and a timestamp). Everything else goes through the shared table.
                Toast(data.Status == LoanMutationDto.StatusCooldown
                          ? CooldownMessage(data)
                          : LocalizationManager.Get(data.ErrorKey()));
                yield break;
            }

            TelemetryService.Instance?.RecordSafe("loan_offer_sent",
                () => new Dictionary<string, object>
                {
                    { "kind", _kind }, { "ref_id", _refId }, { "days", _days },
                    { "via", _selectedViaSearch ? "search" : "followed" }
                });

            string recipient = data.Loan?.Borrower != null ? data.Loan.Borrower.Name : "";

            // The refresh is what actually LOCKS the asset: it reconciles, which sets isLentOut
            // (true for an offered row too), pulls the club out of the bag, and repaints both
            // panels and every card into the OFFERED state.
            LoanSyncBehaviour.RequestRefresh("lend");

            Hide();

            // "Offered X to Y", not "Lent" — the asset has not moved and saying it has would be
            // the one sentence that makes the recipient's DECLINE look like a bug.
            Toast(string.Format(LocalizationManager.Get("LOAN_TOAST_OFFERED_FMT"),
                                LoanSyncBehaviour.AssetName(data.Loan), recipient));
        }

        /// <summary>
        /// "You can offer to {0} again in {1}" — the only refusal that needs two arguments this
        /// modal owns. <c>retry_after</c> is an absolute instant on the wire; the player is told
        /// a duration, because "in 4h" is actionable and "at 04:12 UTC" is not.
        /// </summary>
        private string CooldownMessage(LoanMutationDto data)
        {
            string name = SelectedName();
            System.DateTime? until = data.RetryAfterUtc;
            string left = until.HasValue
                ? LoanRibbonView.FormatTimeLeft(until.Value - System.DateTime.UtcNow)
                : "";
            return string.Format(LocalizationManager.Get("LOAN_ERR_COOLDOWN_FMT"), name, left);
        }

        /// <summary>The display name of the picked row, from whichever section it is in.</summary>
        private string SelectedName()
        {
            foreach (LoanRecipientRow r in _resultRows)
                if (r != null && r.UserId == _selectedUserId) return r.DisplayName;
            foreach (LoanRecipientRow r in _rows)
                if (r != null && r.UserId == _selectedUserId) return r.DisplayName;
            return LoanPartyDto.Fallback;
        }

        private static void Toast(string message)
        {
            if (!string.IsNullOrEmpty(message))
                ToastController.Instance?.Show(message);
        }

        public override void Hide()
        {
            if (_searchRoutine != null) { StopCoroutine(_searchRoutine); _searchRoutine = null; }
            // A response still in flight must not repaint a modal that is closing (and, worse,
            // re-open its RESULTS section behind the backdrop on the next Show).
            _searchTicket++;
            ClearResultRows();
            ShowResultsSection(false);
            ClearRows();
            base.Hide();
        }
    }
}
