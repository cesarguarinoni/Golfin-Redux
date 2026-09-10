// asset_loans §2 — the loan economy over the EXISTING ApiClient.
// Plain C# singleton in the shape of GiftService / UserService: constructible in an EditMode test,
// no MonoBehaviour, no offline queue.
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;

namespace Golfin.Social
{
    /// <summary>
    /// What a loan list means to the rest of the game, expressed as a callback so this assembly
    /// never has to know that <c>CharacterManager</c> exists.
    ///
    /// <para>
    /// WHY AN INTERFACE AND NOT A DIRECT CALL. <c>Golfin.Social</c> references
    /// <c>Golfin.Net</c> and <c>Golfin.Localization</c> and nothing else — the managers live in the
    /// main assembly, which references THIS one. Calling them from here would either invert that
    /// edge or force a new asmdef, and both are a much bigger change than the feature warrants.
    /// The main assembly implements this and hands an instance to
    /// <see cref="LoanService.Reconciler"/> at boot; an unset reconciler simply means the loan list
    /// is fetched and nothing is applied, which is exactly the right behaviour in an EditMode test.
    /// </para>
    /// </summary>
    public interface ILoanReconciler
    {
        /// <summary>A live loan where this player is the BORROWER: create or update a runtime-only
        /// borrowed instance at <paramref name="level"/>.</summary>
        void EnsureBorrowed(LoanDto loan);

        /// <summary>A loan that ENDED where this player was the borrower: drop the borrowed
        /// instance, and unselect / unequip it if it was in use.</summary>
        void RemoveBorrowed(LoanDto loan, bool firstTime);

        /// <summary>A live loan where this player is the LENDER: lock the asset locally.</summary>
        void MarkLentOut(LoanDto loan);

        /// <summary>A loan that ENDED where this player was the lender: clear the lock and catch
        /// the level (and its SP) up to <c>level_at_end</c>.</summary>
        void ClearLentOut(LoanDto loan, bool firstTime);

        /// <summary>Called once at the end of a reconcile pass that changed anything, so the
        /// caller can fire its roster/inventory events and mark the save dirty exactly once
        /// instead of once per loan.</summary>
        void ReconcileFinished(bool changed);

        /// <summary>Has this ended loan already been reconciled on this device? Backed by
        /// <c>SaveData.reconciledLoanIds</c>, so the 14-day window never re-toasts.</summary>
        bool WasReconciled(string loanId);

        /// <summary>Remember that an ended loan has been applied.</summary>
        void MarkReconciled(string loanId);
    }

    /// <summary>
    /// The client half of asset lending: reads the caller's loans, performs the two writes a
    /// player can make (LEND, RETURN), and tells the rest of the game what changed.
    ///
    /// <para>
    /// THE SERVER OWNS THE LIST AND NOTHING IS PERSISTED HERE. A loan is not save data — it is a
    /// fact about two accounts, and the moment a client cached one it would be able to disagree
    /// with the server about who is holding what. <see cref="Refresh"/> is called at boot, on
    /// Roster and Inventory entry, and after every write; between those the last list stands, and
    /// offline it simply does not change.
    /// </para>
    /// <para>
    /// IDEMPOTENCY. <see cref="Lend"/> takes a key rather than minting one, for the same reason
    /// <c>GiftService.SendPts</c> does: the key belongs to the logical action (one tap of the LEND
    /// button) and must be held across retries. Minting per attempt would turn one timed-out lend
    /// into two loans.
    /// </para>
    /// </summary>
    public sealed class LoanService
    {
        private static LoanService _instance;

        public static LoanService Instance =>
            _instance ?? (_instance = new LoanService(ApiClient.Instance));

        public static void ConfigureForTest(LoanService service) => _instance = service;
        public static void ResetForTest() => _instance = null;

        private readonly ApiClient _client;

        public LoanService(ApiClient client) { _client = client; }

        /// <summary>Set by the main assembly at boot. Null in tests, and the code path is the same.</summary>
        public ILoanReconciler Reconciler { get; set; }

        // ── state ────────────────────────────────────────────────────────────

        private readonly List<LoanDto> _out    = new List<LoanDto>();
        private readonly List<LoanDto> _in     = new List<LoanDto>();
        private readonly List<LoanDto> _ended  = new List<LoanDto>();
        private readonly List<LoanDto> _offers = new List<LoanDto>();

        /// <summary>
        /// LOCKED assets where this player is the lender — running loans AND pending offers.
        ///
        /// <para>
        /// THE UNION IS DELIBERATE AND IT IS WHAT MAKES THE FEATURE CHEAP. Offering an asset
        /// has to lock it (asset_loans_offers decision #3), and every lock in the game already
        /// goes through <see cref="IsLentOut"/> — SelectCharacter, EquipClub, the level-up
        /// gate, both detail panels. Putting offers in this list makes all of them true on the
        /// first refresh with no new branch anywhere; a fourth list would have meant finding
        /// and editing every one of those call sites.
        /// </para>
        /// <para>
        /// Callers that need to tell the two apart ask <see cref="IsOffered"/>, or read
        /// <c>loan.Status</c> — which is what the ribbon does to pick OFFERED over ON LOAN.
        /// </para>
        /// </summary>
        public IReadOnlyList<LoanDto> Out => _out;

        /// <summary>LIVE loans where this player is the borrower. Never carries offers — see
        /// <see cref="OffersIn"/>.</summary>
        public IReadOnlyList<LoanDto> In => _in;

        /// <summary>
        /// Offers made TO this player and not yet answered, newest first.
        ///
        /// <para>
        /// NOT MERGED INTO <see cref="In"/>, and that is the safety property. Everything in
        /// <c>In</c> is handed to <c>EnsureBorrowed</c> and becomes a runtime instance in the
        /// player's roster; an unanswered offer must not, and keeping it in a separate list
        /// means no reconciler branch has to remember to check a status first. This list feeds
        /// the Home pill and the offer modal, and nothing else.
        /// </para>
        /// </summary>
        public IReadOnlyList<LoanDto> OffersIn => _offers;

        /// <summary>Loans that have ENDED inside the server's reporting window, either side.</summary>
        public IReadOnlyList<LoanDto> Ended => _ended;

        /// <summary>Raised after every successful <see cref="Refresh"/>, and after a write that
        /// changed the lists. Panels and cards repaint off this.</summary>
        public event Action OnLoansChanged;

        /// <summary>True once a Refresh has come back. Panels use it to tell "no loans" from
        /// "we have not asked yet" — the second must not paint a RETURN button it will withdraw.</summary>
        public bool HasFetched { get; private set; }

        /// <summary>A fresh idempotency key. One per logical action, reused across retries.</summary>
        public static string NewKey() => Guid.NewGuid().ToString();

        // ── lookups the UI asks ──────────────────────────────────────────────

        /// <summary>Is this asset out of this player's hands — lent OR offered?</summary>
        public bool IsLentOut(string kind, string refId, out LoanDto loan)
            => TryFind(_out, kind, refId, out loan);

        /// <summary>
        /// Is this asset OFFERED (not yet accepted) by this player? A narrowing of
        /// <see cref="IsLentOut"/>, for the two callers that must tell the states apart: the
        /// detail panels, which relabel LEND to RESCIND, and the ribbon, which says OFFERED TO
        /// rather than ON LOAN.
        /// </summary>
        public bool IsOffered(string kind, string refId, out LoanDto loan)
            => TryFind(_out, kind, refId, out loan) && loan.IsPendingOffer();

        public bool IsOffered(string kind, string refId) => IsOffered(kind, refId, out _);

        /// <summary>Is this asset borrowed (this player is the borrower)?</summary>
        public bool IsBorrowed(string kind, string refId, out LoanDto loan)
            => TryFind(_in, kind, refId, out loan);

        public bool IsLentOut(string kind, string refId) => IsLentOut(kind, refId, out _);
        public bool IsBorrowed(string kind, string refId) => IsBorrowed(kind, refId, out _);

        private static bool TryFind(List<LoanDto> src, string kind, string refId, out LoanDto loan)
        {
            loan = null;
            if (string.IsNullOrEmpty(refId)) return false;
            for (int i = 0; i < src.Count; i++)
            {
                LoanDto l = src[i];
                if (l == null) continue;
                if (!string.Equals(l.Kind, kind, StringComparison.Ordinal)) continue;
                if (!string.Equals(l.RefId, refId, StringComparison.Ordinal)) continue;
                loan = l;
                return true;
            }
            return false;
        }

        // ── the round snapshot ───────────────────────────────────────────────

        private List<string> _roundLoanIds = new List<string>();

        /// <summary>
        /// Freeze the loans whose assets this round is being played with. Called at HOLE LOAD by
        /// the main assembly, which is the only place that knows which character is selected and
        /// which clubs are in the active bag.
        ///
        /// <para>
        /// THE SNAPSHOT IS TAKEN AT ROUND START ON PURPOSE. A loan that expires mid-round still
        /// splits that round's earn as far as the client is concerned — the server drops it if it
        /// is no longer live by the time the earn lands, so the asymmetry costs the owner a share
        /// they were arguably owed and never over-pays. Recomputing at earn time would instead
        /// drop the split for a round the player genuinely played on borrowed gear.
        /// </para>
        /// </summary>
        public void SnapshotRoundLoans(string selectedCharacterId, IEnumerable<string> bagClubIds)
        {
            var ids = new List<string>();

            if (!string.IsNullOrEmpty(selectedCharacterId)
                && IsBorrowed(LoanDto.KindCharacter, selectedCharacterId, out LoanDto ch))
                ids.Add(ch.Id);

            if (bagClubIds != null)
            {
                foreach (string clubId in bagClubIds)
                {
                    if (string.IsNullOrEmpty(clubId)) continue;
                    if (IsBorrowed(LoanDto.KindClub, clubId, out LoanDto cl) && !ids.Contains(cl.Id))
                        ids.Add(cl.Id);
                }
            }

            _roundLoanIds = ids;
        }

        /// <summary>The frozen snapshot. Null when there is nothing to split, so callers can pass
        /// it straight through and the field is simply omitted from the request.</summary>
        public List<string> UsedLoanIdsForRound()
            => _roundLoanIds != null && _roundLoanIds.Count > 0 ? _roundLoanIds : null;

        /// <summary>Drop the snapshot. Called when a round ends so a Practice tap that never loads
        /// a hole cannot inherit the previous round's loans.</summary>
        public void ClearRoundLoans() => _roundLoanIds = new List<string>();

        // ── reads ────────────────────────────────────────────────────────────

        /// <summary>
        /// GET <c>/loans</c>, then <see cref="Reconcile"/>. The only path that changes
        /// <see cref="Out"/> / <see cref="In"/> / <see cref="Ended"/>.
        ///
        /// <para>A FAILED REFRESH CHANGES NOTHING. The previous lists stand rather than being
        /// cleared, because "we could not reach the server" and "you have no loans" produce
        /// completely different screens and only one of them is true.</para>
        /// </summary>
        public IEnumerator Refresh(Action<ApiResult<LoanListDto>> onResult = null)
            => _client.Get<LoanListDto>(Endpoints.Loans, r =>
            {
                if (r != null && r.Success && r.Data != null)
                {
                    Apply(r.Data);
                    Reconcile();
                    HasFetched = true;
                    OnLoansChanged?.Invoke();
                }
                onResult?.Invoke(r);
            });

        /// <summary>
        /// Split one server payload into the three lists. Public so an EditMode test can drive the
        /// state machine without a transport.
        /// </summary>
        public void Apply(LoanListDto payload)
        {
            _out.Clear();
            _in.Clear();
            _ended.Clear();
            _offers.Clear();
            if (payload == null) return;

            DateTime now = DateTime.UtcNow;

            // OUT is filtered on the LOCKED predicate, so a pending offer stays in it and every
            // existing lock path holds. IN is filtered on LIVE, so an offer can never reach it
            // even if the server one day put one there.
            Sort(payload.Out, _out, now, asBorrower: false, live: l => l.IsLocked(now));
            Sort(payload.In,  _in,  now, asBorrower: true,  live: l => l.IsLive(now));

            // OFFERS never fall through to `_ended`: an offer the recipient never answered
            // reaches THEM as an ordinary disappearance (the pill goes), and reaches the LENDER
            // through `out` as `offer_expired`, which is the side that owns the toast. Adding it
            // to `_ended` here would toast the recipient about an offer they were never told
            // about in the first place.
            if (payload.OffersIn != null)
            {
                foreach (LoanDto l in payload.OffersIn)
                {
                    if (l == null || string.IsNullOrEmpty(l.Id)) continue;
                    if (l.IsPendingOffer(now)) _offers.Add(l);
                }
            }
        }

        /// <summary>The newest pending offer, or null. What the Home pill opens on a tap.</summary>
        public LoanDto NewestOfferIn()
        {
            LoanDto best = null;
            DateTime bestAt = DateTime.MinValue;
            foreach (LoanDto l in _offers)
            {
                if (l == null) continue;
                // `offered_at` rather than list order: the server sorts by `created_at` desc,
                // which is the same thing today, but "newest" is a property of the row and not
                // of how it arrived.
                DateTime at = LoanDto.ParseUtc(l.OfferedAt) ?? DateTime.MinValue;
                if (best == null || at > bestAt) { best = l; bestAt = at; }
            }
            return best;
        }

        /// <summary>
        /// GET <c>/social/{me}/following</c> — the lend modal's recipient list.
        ///
        /// <para>
        /// <c>myId</c> comes from <c>UserService.Instance.LastDetail.Id</c>, which the GPS screens
        /// already populate at boot. When it is missing the call is not made: hitting
        /// <c>/social//following</c> would 404, and an empty list plus the LOAN_NO_FOLLOWING empty
        /// state is a far better answer than a spurious error toast.
        /// </para>
        /// </summary>
        public IEnumerator Following(Action<ApiResult<List<FollowedUserDto>>> onResult,
                                     int limit = 50)
        {
            string myId = UserService.Instance != null && UserService.Instance.LastDetail != null
                ? UserService.Instance.LastDetail.Id
                : null;

            if (string.IsNullOrEmpty(myId))
            {
                onResult?.Invoke(ApiResult<List<FollowedUserDto>>.Ok(
                    new List<FollowedUserDto>(), 200, null, 0));
                yield break;
            }

            IEnumerator call = _client.Get(
                Endpoints.SocialFollowing(myId, limit, forLoans: true), onResult);
            while (call.MoveNext()) yield return call.Current;
        }

        /// <summary>
        /// GET <c>/user/search?q=…&amp;for_loans=1</c> — the lend modal's search field
        /// (asset_loans_offers §3.1), and the reason anyone can now be lent to.
        ///
        /// <para>
        /// SAME DTO AS <see cref="Following"/>, DIFFERENT WIRE SHAPE. Search returns a bare
        /// profiles row with <c>id</c> / <c>display_name</c> at the top level; following nests
        /// them under <c>profiles</c>. <c>FollowedUserDto</c> maps both, so the modal holds one
        /// selection and one row type across the two sections.
        /// </para>
        /// <para>
        /// An empty or whitespace query is answered LOCALLY with an empty list and no round
        /// trip: the endpoint would happily return "recently active players" for it, and the
        /// modal's contract is that clearing the field hides the RESULTS section entirely.
        /// </para>
        /// </summary>
        public IEnumerator SearchUsers(string query,
                                       Action<ApiResult<List<FollowedUserDto>>> onResult,
                                       int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                onResult?.Invoke(ApiResult<List<FollowedUserDto>>.Ok(
                    new List<FollowedUserDto>(), 200, null, 0));
                yield break;
            }

            IEnumerator call = _client.Get(
                Endpoints.UserSearch(query.Trim(), limit, forLoans: true), onResult);
            while (call.MoveNext()) yield return call.Current;
        }

        // ── writes ───────────────────────────────────────────────────────────

        /// <summary>
        /// POST <c>/loans</c>. <paramref name="idempotencyKey"/> must be held across retries of
        /// the SAME tap — see the class remarks.
        /// </summary>
        public IEnumerator Lend(string kind, string refId, string borrowerId, int days, int level,
                                string idempotencyKey,
                                Action<ApiResult<LoanMutationDto>> onResult)
            => _client.Post(Endpoints.Loans,
                            BuildLendJson(kind, refId, borrowerId, days, level, idempotencyKey),
                            onResult);

        /// <summary>POST <c>/loans/{id}/return</c> — the borrower gives it back early.</summary>
        public IEnumerator Return(string loanId, Action<ApiResult<LoanMutationDto>> onResult)
            => _client.Post<LoanMutationDto>(Endpoints.LoansReturn(loanId), "{}", onResult);

        /// <summary>
        /// POST <c>/loans/{id}/accept</c> — the RECIPIENT takes an offer, and the loan begins.
        ///
        /// <para>NO IDEMPOTENCY KEY, unlike <see cref="Lend"/>, and it does not need one: the
        /// loan id in the path IS the identity of the action, and the server answers <c>ok</c>
        /// to a second accept of a loan this player already accepted. A retried tap cannot
        /// create a second anything.</para>
        /// </summary>
        public IEnumerator Accept(string loanId, Action<ApiResult<LoanMutationDto>> onResult)
            => _client.Post<LoanMutationDto>(Endpoints.LoansAccept(loanId), "{}", onResult);

        /// <summary>POST <c>/loans/{id}/decline</c> — the recipient turns an offer down.</summary>
        public IEnumerator Decline(string loanId, Action<ApiResult<LoanMutationDto>> onResult)
            => _client.Post<LoanMutationDto>(Endpoints.LoansDecline(loanId), "{}", onResult);

        /// <summary>POST <c>/loans/{id}/rescind</c> — the LENDER takes back an unanswered
        /// offer. Not a recall: an accepted loan answers <c>not_offered</c>.</summary>
        public IEnumerator Rescind(string loanId, Action<ApiResult<LoanMutationDto>> onResult)
            => _client.Post<LoanMutationDto>(Endpoints.LoansRescind(loanId), "{}", onResult);

        /// <summary>Public so an EditMode test can pin the wire shape without a transport — the
        /// same seam <c>GiftService.BuildSendJson</c> uses. Field names are snake_case because they
        /// mirror <c>loans.py::LendRequest</c>.</summary>
        public static string BuildLendJson(string kind, string refId, string borrowerId,
                                           int days, int level, string key)
            => JsonConvert.SerializeObject(
                new LendBody
                {
                    kind            = kind,
                    ref_id          = refId,
                    borrower_id     = borrowerId,
                    days            = days,
                    level           = level,
                    idempotency_key = key,
                },
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        private sealed class LendBody
        {
            public string kind;
            public string ref_id;
            public string borrower_id;
            public int    days;
            public int    level;
            public string idempotency_key;
        }

        // ── reconciliation ───────────────────────────────────────────────────

        /// <summary>
        /// Push the current lists at the game: create borrowed instances, lock lent-out ones, and
        /// apply the end-of-loan catch-up exactly once per loan.
        ///
        /// <para>
        /// "EXACTLY ONCE" IS THE HARD PART, and it is why <see cref="ILoanReconciler.WasReconciled"/>
        /// exists. The server reports ended loans for 14 days so an offline client still learns
        /// about them — which means every single refresh for two weeks sees the same ended loan
        /// and would re-toast, and (worse) re-apply the level catch-up, on every Roster entry.
        /// The <c>firstTime</c> flag is what separates "apply and tell the player" from "this is
        /// just the list again".
        /// </para>
        /// <para>
        /// Idempotent by construction otherwise: <c>EnsureBorrowed</c> and <c>MarkLentOut</c> are
        /// both "make it so", not "do it again".
        /// </para>
        /// </summary>
        public void Reconcile()
        {
            ILoanReconciler r = Reconciler;
            if (r == null) return;

            bool changed = false;

            foreach (LoanDto l in _in)
            {
                r.EnsureBorrowed(l);
                changed = true;
            }

            foreach (LoanDto l in _out)
            {
                r.MarkLentOut(l);
                changed = true;
            }

            foreach (LoanDto l in _ended)
            {
                bool first = !r.WasReconciled(l.Id);

                // Which side WE were on. A loan can only be one or the other for a given player,
                // and the two lists it arrived in already said which — but an ended loan is in
                // neither, so the side is re-derived from the payload rather than remembered.
                bool weBorrowed = IsEndedBorrow(l);

                if (weBorrowed) r.RemoveBorrowed(l, first);
                else            r.ClearLentOut(l, first);

                if (first)
                {
                    r.MarkReconciled(l.Id);
                    changed = true;
                }
            }

            r.ReconcileFinished(changed);
        }

        /// <summary>
        /// Which side of an ENDED loan this player was on.
        ///
        /// <para>
        /// The server splits the payload into <c>out</c> and <c>in</c> before we ever see it, so
        /// the answer is already known at parse time — <see cref="Apply"/> records it here rather
        /// than making <see cref="Reconcile"/> guess from ids it would have to compare against a
        /// user id this assembly does not hold.
        /// </para>
        /// </summary>
        private readonly HashSet<string> _endedAsBorrower = new HashSet<string>(StringComparer.Ordinal);

        private bool IsEndedBorrow(LoanDto l) => l != null && _endedAsBorrower.Contains(l.Id);

        /// <summary>Record the side while the payload still says which list a row came from.</summary>
        private void NoteSide(LoanDto l, bool asBorrower)
        {
            if (l == null || string.IsNullOrEmpty(l.Id)) return;
            if (asBorrower) _endedAsBorrower.Add(l.Id);
            else            _endedAsBorrower.Remove(l.Id);
        }

        /// <summary>
        /// Split one side's rows into current and ended, recording the side as it goes.
        ///
        /// <para><paramref name="stillCurrent"/> is the predicate that differs between the two
        /// sides: LOCKED for the lender (a pending offer is current), LIVE for the borrower (it
        /// is not). Passed in rather than branched on <paramref name="asBorrower"/> so the call
        /// site says which predicate it means.</para>
        /// </summary>
        private void Sort(List<LoanDto> src, List<LoanDto> current, DateTime now, bool asBorrower,
                          Func<LoanDto, bool> live)
        {
            if (src == null) return;
            foreach (LoanDto l in src)
            {
                if (l == null || string.IsNullOrEmpty(l.Id)) continue;
                NoteSide(l, asBorrower);
                if (live(l)) current.Add(l);
                else _ended.Add(l);
            }
        }
    }
}
