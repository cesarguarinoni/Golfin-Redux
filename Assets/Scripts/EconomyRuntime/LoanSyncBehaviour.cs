// ─────────────────────────────────────────────────────────────────────────────
// asset_loans §2.1 — the bridge between LoanService (Golfin.Social) and the game.
//
// Lives in Assembly-CSharp, not in Golfin.Social, for the same reason
// ServerBalanceSyncBehaviour lives here and not in Golfin.Economy: it touches
// CharacterManager, ClubManager, BagManager, SaveDataHost and ScreenManager, and
// none of those are visible from a leaf asmdef. The RULES (what a loan is, when
// it is live, how the lists split) stay in LoanService so they are testable
// without a scene; the EFFECTS are here.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Auth;
using Golfin.Economy;
using Golfin.Inventory;
using Golfin.Roster;
using Golfin.Save;
using Golfin.Social;
using Golfin.UI.Toast;
using GolfinRedux.UI;
using UnityEngine;

namespace Golfin.EconomyRuntime
{
    /// <summary>
    /// Keeps the roster and the bag equal to what the server says is on loan, and tells the player
    /// when something arrives or comes back.
    ///
    /// INERT WITH THE POINTS BACKEND FLAG OFF. Loans are a server feature end to end — there is no
    /// local loan and no offline lend — so with the flag off this type is never created and the
    /// game is byte-identical to what it was before the feature. Same posture, and the same
    /// bootstrap, as <see cref="ServerBalanceSyncBehaviour"/> next door.
    ///
    /// REFRESH MOMENTS (§2, extended by asset_loans_offers §2):
    ///   • sign-in succeeds / startup while already signed in — the first list of the session;
    ///   • entering HOME, Roster or Inventory — the three screens a loan or an offer is visible
    ///     on, throttled. Home joined the list because the offer pill lives there and an offer
    ///     that arrived while the player was mid-session would otherwise not show until they
    ///     wandered into the Roster;
    ///   • after every LEND / RETURN / ACCEPT / DECLINE / RESCIND — the modals call
    ///     <see cref="RequestRefresh"/> themselves.
    ///
    /// There is deliberately no poll. A loan does not change under the player's feet often enough
    /// to be worth a timer, and the three screens that show one are exactly the ones that refresh.
    /// (A push/notice on offer ARRIVAL is explicitly out of scope — the pill is a Home-entry
    /// affordance, not a notification.)
    /// </summary>
    public sealed class LoanSyncBehaviour : MonoBehaviour, ILoanReconciler
    {
        /// <summary>Floor between screen-entry refreshes. Bouncing Roster↔Inventory is a normal
        /// thing to do and must not become a request per tap. Sign-in and an explicit
        /// post-write refresh are NOT throttled — those are the moments the list actually moved.</summary>
        private const float ScreenRefreshCooldownSeconds = 10f;

        /// <summary>
        /// How many reconciled loan ids the save keeps. The server's window is 14 days.
        ///
        /// <para>
        /// RAISED FROM 50 TO 150 BY asset_loans_offers, because a loan now writes up to THREE
        /// entries instead of one: <c>offered:&lt;id&gt;</c> when it goes out,
        /// <c>accepted:&lt;id&gt;</c> when the answer lands, and the bare id when it ends. At 50
        /// the eviction would start dropping the OLDEST — which is the `offered:` marker — and a
        /// busy lender would stop being told their offers were accepted. 150 keeps the same
        /// "comfortably more than 14 days can hold" margin the original number had.
        /// </para>
        /// </summary>
        private const int MaxReconciledIds = 150;

        private static LoanSyncBehaviour? _instance;

        private float _lastScreenRefresh = float.NegativeInfinity;

        // Collected during a reconcile pass and flushed in ReconcileFinished, so a pass that
        // changes three things fires ONE OnRosterChanged and shows toasts in a predictable order
        // rather than interleaving them with the mutations that caused them.
        private readonly List<string> _pendingToasts = new List<string>();
        private bool _rosterDirty;
        private bool _inventoryDirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!PointsBackendFlag.Enabled) return;   // flag OFF: this type never runs
            if (_instance != null) return;

            var go = new GameObject("[LoanSync]");
            _instance = go.AddComponent<LoanSyncBehaviour>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            LoanService.Instance.Reconciler = this;
            AuthService.SignedIn += OnSignedIn;
            ScreenManager.ScreenChanged += OnScreenChanged;
        }

        private void OnDisable()
        {
            AuthService.SignedIn -= OnSignedIn;
            ScreenManager.ScreenChanged -= OnScreenChanged;
            if (LoanService.Instance.Reconciler == (ILoanReconciler)this)
                LoanService.Instance.Reconciler = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            // A returning player is already signed in from the saved session, so no SignedIn event
            // will ever fire for them — without this a loan that started while they were away
            // would not appear until they walked into the Roster.
            if (AuthService.Instance.Session.IsAuthenticated) Refresh("startup");
        }

        // ── refresh triggers ──────────────────────────────────────────────────

        private void OnSignedIn(AuthSession session) => Refresh("sign-in");

        private void OnScreenChanged(ScreenId screen)
        {
            if (screen != ScreenId.Roster && screen != ScreenId.Inventory
                && screen != ScreenId.Home) return;
            if (Time.unscaledTime - _lastScreenRefresh < ScreenRefreshCooldownSeconds) return;
            _lastScreenRefresh = Time.unscaledTime;
            Refresh(screen == ScreenId.Roster ? "roster"
                  : screen == ScreenId.Inventory ? "inventory" : "home");
        }

        /// <summary>
        /// Pull the loan list now. Called by the lend modal and the return popup after a
        /// successful write, and by the two screen entries above.
        ///
        /// <para>Safe at any time: no-ops with the flag off, when signed out, and when the
        /// behaviour is not running at all (which is what "flag off" looks like from a caller).</para>
        /// </summary>
        public static void RequestRefresh(string why)
        {
            LoanSyncBehaviour? instance = _instance;
            if (instance == null)
            {
                Debug.Log($"[LoanSync] {why} refresh skipped — loan sync not running.");
                return;
            }
            instance._lastScreenRefresh = float.NegativeInfinity;   // an explicit ask is never throttled
            instance.Refresh(why);
        }

        private void Refresh(string why)
        {
            if (!PointsBackendFlag.Enabled) return;
            if (!AuthService.Instance.Session.IsAuthenticated)
            {
                Debug.Log($"[LoanSync] Skipping {why} refresh — not signed in.");
                return;
            }

            StartCoroutine(LoanService.Instance.Refresh(result =>
            {
                if (result == null || !result.Success)
                    Debug.LogWarning($"[LoanSync] {why} refresh failed: " +
                                     $"{(result != null ? result.ToString() : "no result")} — keeping the last list.");
                else
                    Debug.Log($"[LoanSync] {why} refresh: {LoanService.Instance.Out.Count} out, " +
                              $"{LoanService.Instance.In.Count} in, {LoanService.Instance.Ended.Count} ended.");
            }));
        }

        // ── the round snapshot (§4.5) ─────────────────────────────────────────

        /// <summary>
        /// Freeze which loans this round is being played with. Called from
        /// <c>GameplaySceneLoader.ApplyPreloadSetup</c> — the one synchronous moment that is
        /// guaranteed to run exactly once per hole load, on every entry path.
        /// </summary>
        public static void SnapshotRoundLoans()
        {
            // ⚠️ MUST NOT TOUCH LoanService IN EDIT MODE. `LoanService.Instance` builds
            // `ApiClient.Instance`, whose constructor creates the `[Golfin.Net]` coroutine host and
            // calls DontDestroyOnLoad — which THROWS outside play mode. `GameplaySceneLoaderTests`
            // drives `ApplyPreloadSetup` from an EditMode test, and this call took that suite down
            // the moment it was added.
            //
            // The flag guard is not just defensive either: loans are a server feature end to end,
            // so with the points backend off there is nothing to snapshot and no reason to
            // construct the stack that would ask.
            if (!Application.isPlaying) return;
            if (!PointsBackendFlag.Enabled) return;

            string? character = CharacterManager.Instance != null
                ? CharacterManager.Instance.GetSelectedCharacterId()
                : null;

            var clubIds = new List<string>();
            BagManager? bags = BagManager.Instance;
            if (bags != null && ClubManager.Instance != null)
            {
                int slot = bags.EquippedBagSlot > 0 ? bags.EquippedBagSlot : 1;
                foreach (PlayerClubData c in bags.GetClubsInBag(slot))
                    if (c != null && !string.IsNullOrEmpty(c.clubId)) clubIds.Add(c.clubId);
            }

            LoanService.Instance.SnapshotRoundLoans(character, clubIds);

            List<string>? ids = LoanService.Instance.UsedLoanIdsForRound();
            if (ids != null)
                Debug.Log($"[LoanSync] Round starts with {ids.Count} borrowed asset(s) — the owners take a cut.");
        }

        // ── ILoanReconciler ───────────────────────────────────────────────────

        public void EnsureBorrowed(LoanDto loan)
        {
            if (loan == null || string.IsNullOrEmpty(loan.RefId)) return;

            if (loan.IsCharacter)
            {
                if (CharacterManager.Instance == null) return;
                bool wasThere = CharacterManager.Instance.IsBorrowed(loan.RefId);
                CharacterManager.Instance.EnsureBorrowed(loan.RefId, loan.Level);
                if (!wasThere) _rosterDirty = true;
            }
            else if (loan.IsClub)
            {
                if (ClubManager.Instance == null) return;
                bool wasThere = ClubManager.Instance.IsBorrowed(loan.RefId);
                ClubManager.Instance.EnsureBorrowed(loan.RefId, loan.Level);
                if (!wasThere) _inventoryDirty = true;
            }
        }

        public void MarkLentOut(LoanDto loan)
        {
            if (loan == null || string.IsNullOrEmpty(loan.RefId)) return;

            NoteOfferTransition(loan);

            if (loan.IsCharacter)
            {
                CharacterManager? cm = CharacterManager.Instance;
                if (cm == null) return;

                if (!cm.IsLentOut(loan.RefId)) _rosterDirty = true;
                cm.SetLentOut(loan.RefId, true);

                // §4.1 refuses lending the SELECTED character, so this should be impossible from
                // this device — but a second device can lend it, and then the player is standing
                // on a character that is no longer theirs to play. Move them off it.
                if (cm.GetSelectedCharacterId() == loan.RefId)
                {
                    string? fallback = cm.FirstSelectableCharacterId();
                    if (!string.IsNullOrEmpty(fallback)) cm.SelectCharacter(fallback!);
                }
            }
            else if (loan.IsClub)
            {
                ClubManager? clubs = ClubManager.Instance;
                if (clubs == null) return;

                if (!clubs.IsLentOut(loan.RefId)) _inventoryDirty = true;
                clubs.SetLentOut(loan.RefId, true);

                // A lent club cannot stay in a bag — the borrower has it.
                PlayerClubData? data = clubs.GetClubData(loan.RefId);
                if (data != null && data.IsEquipped)
                {
                    BagManager.Instance?.RemoveClubFromBag(loan.RefId);
                    _inventoryDirty = true;
                }
            }
        }

        public void RemoveBorrowed(LoanDto loan, bool firstTime)
        {
            if (loan == null || string.IsNullOrEmpty(loan.RefId)) return;

            if (loan.IsCharacter)
            {
                CharacterManager? cm = CharacterManager.Instance;
                if (cm == null) return;
                if (!cm.IsBorrowed(loan.RefId)) return;

                bool wasSelected = cm.GetSelectedCharacterId() == loan.RefId;
                if (cm.RemoveBorrowed(loan.RefId)) _rosterDirty = true;

                if (wasSelected)
                {
                    // The starter is always owned after the FTUE, so there is always something to
                    // fall back to. If there somehow is not, leaving the selection pointing at a
                    // character that is no longer in the roster is the one outcome that breaks
                    // gameplay, so it is logged loudly rather than passed over.
                    string? fallback = cm.FirstSelectableCharacterId();
                    if (!string.IsNullOrEmpty(fallback)) cm.SelectCharacter(fallback!);
                    else Debug.LogError("[LoanSync] A borrowed character was returned while selected " +
                                        "and there is nothing owned to fall back to.");
                }
            }
            else if (loan.IsClub)
            {
                ClubManager? clubs = ClubManager.Instance;
                if (clubs == null) return;
                if (!clubs.IsBorrowed(loan.RefId)) return;

                PlayerClubData? data = clubs.GetClubData(loan.RefId);
                if (data != null && data.IsEquipped) BagManager.Instance?.RemoveClubFromBag(loan.RefId);
                if (clubs.RemoveBorrowed(loan.RefId)) _inventoryDirty = true;
            }

            // AN OFFER THAT ENDED IS NOT A RETURN, and the recipient gets no toast for one.
            // They already know: they tapped DECLINE (the modal toasts), or the lender took it
            // back / it lapsed — in which case the recipient was never told it existed in the
            // first place and "X went back to Y" would be about an asset they never had.
            if (firstTime && !loan.IsOfferEnded)
                _pendingToasts.Add(Format("LOAN_TOAST_RETURNED_IN",
                                          AssetName(loan), Party(loan.Lender)));
        }

        public void ClearLentOut(LoanDto loan, bool firstTime)
        {
            if (loan == null || string.IsNullOrEmpty(loan.RefId)) return;

            // AN OFFER THAT WAS NEVER ACCEPTED HAS NO CATCH-UP TO APPLY, and skipping it is not
            // an optimisation — `ApplyLoanLevelCatchUp` is what grants the SP for levels the
            // BORROWER bought, and on a declined / rescinded / lapsed offer nobody ever held the
            // asset, so there are no such levels. Running it would be a no-op today (level_at_end
            // is null, so it falls back to the owner's own current level) but it would be a no-op
            // by luck rather than by intent, and the first time a future change made
            // `LevelAtEnd` non-null on a terminal offer it would silently grant SP for nothing.
            bool offerEnded = loan.IsOfferEnded;

            int levelAtEnd = loan.LevelAtEnd ?? loan.Level;
            int sp = 0;

            if (loan.IsCharacter)
            {
                CharacterManager? cm = CharacterManager.Instance;
                if (cm == null) return;
                if (cm.IsLentOut(loan.RefId)) _rosterDirty = true;
                cm.SetLentOut(loan.RefId, false);
                if (!offerEnded) sp = cm.ApplyLoanLevelCatchUp(loan.RefId, levelAtEnd);
            }
            else if (loan.IsClub)
            {
                ClubManager? clubs = ClubManager.Instance;
                if (clubs == null) return;
                if (clubs.IsLentOut(loan.RefId)) _inventoryDirty = true;
                clubs.SetLentOut(loan.RefId, false);
                if (!offerEnded) sp = clubs.ApplyLoanLevelCatchUp(loan.RefId, levelAtEnd);
            }

            if (sp > 0)
            {
                _rosterDirty |= loan.IsCharacter;
                _inventoryDirty |= loan.IsClub;
                InventorySync.InventorySyncService.Instance?.MarkDirty();
            }

            if (!firstTime) return;

            // THREE TERMINAL STATES, THREE SENTENCES. A single "your offer ended" would be the
            // cheap version of this and it would be wrong every time: "they said no", "you took
            // it back" and "nobody answered" call for completely different next actions from
            // the lender, and the cooldown applies to the first two but not the third.
            switch (loan.Status)
            {
                case LoanDto.StatusDeclined:
                    _pendingToasts.Add(Format("LOAN_TOAST_DECLINED_FMT",
                                              Party(loan.Borrower), AssetName(loan)));
                    return;
                case LoanDto.StatusRescinded:
                    _pendingToasts.Add(Format("LOAN_TOAST_RESCINDED_FMT", AssetName(loan)));
                    return;
                case LoanDto.StatusOfferExpired:
                    _pendingToasts.Add(Format("LOAN_TOAST_OFFER_EXPIRED_FMT", AssetName(loan)));
                    return;
                default:
                    _pendingToasts.Add(Format("LOAN_TOAST_RETURNED_OUT",
                                              AssetName(loan), loan.RpToLender.ToString()));
                    return;
            }
        }

        // ── the offer → loan transition (asset_loans_offers §2) ───────────────

        /// <summary>Prefix under which an id is remembered as "this went out as an offer".</summary>
        private const string SeenOfferedPrefix = "offered:";

        /// <summary>Prefix under which "we have already told the lender it was accepted" lives.</summary>
        private const string ToldAcceptedPrefix = "accepted:";

        /// <summary>
        /// Notice when an asset this player OFFERED has been accepted, and say so once.
        ///
        /// <para>
        /// THE TRANSITION IS INVISIBLE FROM ONE PAYLOAD. Both an `offered` row and the `active`
        /// row it becomes arrive in <c>out</c> and both mean "locked", so nothing about the
        /// current list says an answer just landed — the lender would watch their ribbon quietly
        /// change wording and never be told. What makes it visible is remembering, on the pass
        /// that first saw the offer, that this id went out as one.
        /// </para>
        /// <para>
        /// Recorded through the SAME <c>SaveData.reconciledLoanIds</c> mechanism the ended-loan
        /// window uses (SPEC §2), under a prefix, so the "exactly once, and across relaunches"
        /// property comes for free rather than being re-derived. Two entries per loan rather
        /// than one is why <see cref="MaxReconciledIds"/> grew — see its remarks.
        /// </para>
        /// </summary>
        private void NoteOfferTransition(LoanDto loan)
        {
            if (loan == null || string.IsNullOrEmpty(loan.Id)) return;

            if (loan.IsPendingOffer())
            {
                MarkReconciled(SeenOfferedPrefix + loan.Id);
                return;
            }

            if (!loan.IsLive()) return;
            if (!WasReconciled(SeenOfferedPrefix + loan.Id)) return;
            if (WasReconciled(ToldAcceptedPrefix + loan.Id)) return;

            MarkReconciled(ToldAcceptedPrefix + loan.Id);
            _pendingToasts.Add(Format("LOAN_TOAST_ACCEPTED_FMT",
                                      Party(loan.Borrower), AssetName(loan)));
        }

        public void ReconcileFinished(bool changed)
        {
            if (_rosterDirty) CharacterManager.Instance?.RaiseRosterChanged();
            if (_inventoryDirty) ClubManager.Instance?.RaiseInventoryChanged();

            _rosterDirty = false;
            _inventoryDirty = false;

            foreach (string message in _pendingToasts)
                ToastController.Instance?.Show(message);
            _pendingToasts.Clear();
        }

        // ── the "already seen" record ─────────────────────────────────────────

        public bool WasReconciled(string loanId)
        {
            if (string.IsNullOrEmpty(loanId)) return true;   // nothing to remember, nothing to toast
            SaveData? data = SaveDataHost.Instance?.Data;
            return data?.reconciledLoanIds != null && data.reconciledLoanIds.Contains(loanId);
        }

        public void MarkReconciled(string loanId)
        {
            if (string.IsNullOrEmpty(loanId)) return;
            SaveDataHost? host = SaveDataHost.Instance;
            if (host == null) return;

            host.Data.reconciledLoanIds ??= new List<string>();
            if (host.Data.reconciledLoanIds.Contains(loanId)) return;

            host.Data.reconciledLoanIds.Add(loanId);
            while (host.Data.reconciledLoanIds.Count > MaxReconciledIds)
                host.Data.reconciledLoanIds.RemoveAt(0);

            host.MarkDirty();
        }

        // ── formatting ────────────────────────────────────────────────────────

        private static string Party(LoanPartyDto? p) => p != null ? p.Name : "PLAYER";

        /// <summary>
        /// The asset's display name for a toast. Falls back to the ref id rather than to an empty
        /// string: "char_kai is back" is ugly, "  is back" is a bug report.
        /// </summary>
        public static string AssetName(LoanDto loan)
        {
            if (loan == null) return "";
            if (loan.IsCharacter)
            {
                var csv = CharacterDatabaseCSV.Instance?.GetCharacter(loan.RefId);
                if (csv != null) return csv.GetLocalizedDisplayName(singleLine: true);
            }
            else if (loan.IsClub)
            {
                var template = ClubDatabaseCSV.Instance?.GetClub(loan.RefId);
                if (template != null && !string.IsNullOrEmpty(template.name)) return template.name;
            }
            return loan.RefId ?? "";
        }

        private static string Format(string key, params string[] args)
            => string.Format(LocalizationManager.Get(key), args);
    }
}
