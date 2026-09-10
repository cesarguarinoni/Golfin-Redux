// ─────────────────────────────────────────────────────────────────────────────
// asset_loans — the play-mode capture bot.
//
// REAL NAVIGATION, STUBBED SOCKET. The whole client path runs: boot → PLAY →
// the real Characters / Bag nav button's onClick → the real detail panel's
// UpdatePanel → the real LEND button's onClick → the real modal. The ONLY thing
// replaced is the HTTP transport, which answers `/loans` and
// `/social/{id}/following` with canned, server-shaped JSON — because the
// migration is not applied and the API is not deployed yet, so there is nothing
// on the other end of the socket to answer.
//
// That is deliberately NOT a render harness (memory:
// feedback_no_render_harness_use_real_navigation): every widget, controller and
// reconcile step under test is the shipped one, driven the way a player drives
// it. What the stub cannot prove is the SERVER half — the E2E in the SPEC's
// acceptance list is Cesar's, after the migration is applied.
//
// GOLFIN ▸ Loans ▸ Capture Loan States
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Golfin.Net;
using Golfin.Social;
using Golfin.UI.Loans;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans.EditorTools
{
    public static class LoanUiCaptureBot
    {
        /// <summary>
        /// asset_loans_offers — the CURRENT task's folder. The v1 folder is in
        /// <c>Docs/Specs/Completed/asset_loans/</c> now, so writing there would put this pass's
        /// frames next to a closed task's evidence.
        /// </summary>
        public const string OutDir = "Docs/Specs/Active/asset_loans_offers/screenshots";

        /// <summary>
        /// Survives the domain reload that entering play mode causes.
        ///
        /// <para>
        /// ⚠️ A <c>playModeStateChanged</c> SUBSCRIPTION DOES NOT. Entering play mode reloads the
        /// editor domain, which wipes every static field — including the delegate list — so a
        /// handler registered just before <c>EnterPlaymode()</c> is gone by the time
        /// <c>EnteredPlayMode</c> would fire, and the bot silently never runs. (It did, once.)
        /// <c>SessionState</c> is the one editor store that outlives the reload.
        /// </para>
        /// </summary>
        private const string ArmedKey = "LoanUiCaptureBot.Armed";

        [MenuItem("GOLFIN/Loans/Capture Loan States")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Spawn();
                return;
            }

            // runInBackground, or an unfocused Editor stops rendering and every capture comes back
            // as the splash frame (memory: reference_playmode_capture_runinbackground).
            Application.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnIfArmed()
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;
            Spawn();
        }

        private static void Spawn()
        {
            var host = new GameObject("[LoanCaptureBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<LoanCaptureRunner>().Begin();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // the stub transport
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Answers the two loan reads with canned, SERVER-SHAPED JSON and refuses everything else, so a
    /// call this bot did not intend cannot silently succeed.
    ///
    /// <para>
    /// The bodies are the exact envelope <c>routers/loans.py</c> writes — <c>{"data": …}</c> with
    /// snake_case keys — so the DTOs, the envelope unwrapper and <c>LoanService.Apply</c> are all
    /// genuinely exercised rather than bypassed.
    /// </para>
    /// </summary>
    public sealed class LoanStubTransport : IHttpTransport
    {
        public string LoansJson = "{}";
        public string FollowingJson = "[]";
        /// <summary>asset_loans_offers §3.1 — the lend modal's search field.</summary>
        public string SearchJson = "[]";
        public readonly List<string> Seen = new List<string>();

        public IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse)
        {
            Seen.Add($"{request.Method} {request.Url}");
            yield return null;

            string url = request.Url ?? "";
            if (url.Contains("/loans"))
                onResponse(HttpResponse.Status(200, "{\"data\":" + LoansJson + "}"));
            else if (url.Contains("/following"))
                onResponse(HttpResponse.Status(200, "{\"data\":" + FollowingJson + "}"));
            else if (url.Contains("/user/search"))
                onResponse(HttpResponse.Status(200, "{\"data\":" + SearchJson + "}"));
            else if (url.Contains("/user/detail") || url.Contains("/user/update"))
                // The Settings toggle reads and writes the caller's own profile row. Answering
                // the row the toggle expects is what lets the ON state paint from real data
                // rather than from the component's default.
                onResponse(HttpResponse.Status(200,
                    "{\"data\":{\"id\":\"11111111-1111-1111-1111-111111111111\"," +
                    "\"display_name\":\"CRATILO\",\"golfin_loan_offers\":true}}"));
            else
                onResponse(HttpResponse.Status(404, "{\"detail\":\"stub: unhandled " + url + "\"}"));
        }
    }

    /// <summary>A signed-in session that never refreshes. The bot is not testing auth.</summary>
    public sealed class LoanStubAuth : IAuthTokenProvider
    {
        public bool IsAuthenticated => true;
        public string AccessToken => "stub-token";
        public IEnumerator Refresh(Action<bool> onDone) { onDone?.Invoke(true); yield break; }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // the runner
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class LoanCaptureRunner : MonoBehaviour, ICoroutineRunner
    {
        private LoanStubTransport _transport = null!;
        private readonly List<string> _shots = new List<string>();

        public void Begin() => StartCoroutine(Sequence());

        void ICoroutineRunner.Run(IEnumerator routine) => StartCoroutine(routine);

        private static T? FindActive<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null
                                  && !string.IsNullOrEmpty(c.gameObject.scene.name)
                                  && c.gameObject.activeInHierarchy);

        private static T? FindAny<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name));

        private IEnumerator Snap(string label)
        {
            // Assert the screen at the moment of capture. A shot taken after something navigated
            // away is a picture of somewhere else, and the filename would still claim otherwise.
            var mgr = GolfinRedux.UI.ScreenManager.Instance;
            Debug.Log($"[LoanCaptureBot] snapping {label} on screen={(mgr != null ? mgr.CurrentScreen.ToString() : "?")}");

            yield return new WaitForEndOfFrame();
            string path = Golfin.Diagnostics.Runtime.CaptureCore.SnapPlayModeSafe(label);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                Debug.LogError($"[LoanCaptureBot] SnapPlayModeSafe returned a path with no file: '{path}'");
                yield break;
            }
            _shots.Add(path);
            Debug.Log($"[LoanCaptureBot] captured {label} -> {path}");
        }

        // ── the canned payloads ───────────────────────────────────────────────

        private static string Iso(TimeSpan fromNow)
            => DateTime.UtcNow.Add(fromNow).ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz");

        private static string Party(string id, string name, int level)
            => $"{{\"id\":\"{id}\",\"display_name\":\"{name}\",\"avatar_url\":null,\"avatar_level\":{level}}}";

        private static string Loan(string id, string kind, string refId, bool asLender,
                                   int level, TimeSpan left, int days)
        {
            string me = "11111111-1111-1111-1111-111111111111";
            string them = "22222222-2222-2222-2222-222222222222";
            string lender = asLender ? Party(me, "YOU", 12) : Party(them, "KENJI", 34);
            string borrower = asLender ? Party(them, "MARTA", 21) : Party(me, "YOU", 12);
            return "{" +
                $"\"id\":\"{id}\",\"kind\":\"{kind}\",\"ref_id\":\"{refId}\"," +
                $"\"lender\":{lender},\"borrower\":{borrower}," +
                $"\"days\":{days},\"starts_at\":\"{Iso(TimeSpan.FromDays(-1))}\"," +
                $"\"ends_at\":\"{Iso(left)}\",\"ended_at\":null,\"status\":\"active\"," +
                $"\"level\":{level},\"level_at_start\":{level},\"level_at_end\":null," +
                "\"lender_share_bp\":2000,\"rp_to_lender\":0,\"rp_to_borrower\":0}";
        }

        private static string Following() =>
            "[" +
            "{\"following_id\":\"22222222-2222-2222-2222-222222222222\",\"created_at\":null,\"profiles\":" + Party("22222222-2222-2222-2222-222222222222", "MARTA", 21) + "}," +
            "{\"following_id\":\"33333333-3333-3333-3333-333333333333\",\"created_at\":null,\"profiles\":" + Party("33333333-3333-3333-3333-333333333333", "LUCAS", 8) + "}," +
            "{\"following_id\":\"44444444-4444-4444-4444-444444444444\",\"created_at\":null,\"profiles\":" + Party("44444444-4444-4444-4444-444444444444", "AIKO", 47) + "}" +
            "]";

        /// <summary>
        /// A PENDING OFFER row (asset_loans_offers §1.3): `offered`, NO starts_at, NO ends_at,
        /// and its own <c>offer_expires_at</c> clock.
        ///
        /// <para>The null loan timestamps are the point, not fixture laziness: they are what
        /// makes <c>IsLive</c> false and <c>IsPendingOffer</c> true on the same row, which is the
        /// whole predicate split. A placeholder <c>ends_at</c> here would let a bug through by
        /// making the offer accidentally look live.</para>
        /// </summary>
        private static string Offer(string id, string kind, string refId, bool asLender,
                                    int level, TimeSpan toAnswer, int days,
                                    string lenderName = "KENJI", string lenderId = "22222222-2222-2222-2222-222222222222")
        {
            string me = "11111111-1111-1111-1111-111111111111";
            string them = "22222222-2222-2222-2222-222222222222";
            string lender   = asLender ? Party(me, "YOU", 12) : Party(lenderId, lenderName, 34);
            string borrower = asLender ? Party(them, "MARTA", 21) : Party(me, "YOU", 12);
            return "{" +
                $"\"id\":\"{id}\",\"kind\":\"{kind}\",\"ref_id\":\"{refId}\"," +
                $"\"lender\":{lender},\"borrower\":{borrower}," +
                $"\"days\":{days},\"starts_at\":null,\"ends_at\":null,\"ended_at\":null," +
                $"\"offered_at\":\"{Iso(TimeSpan.FromHours(-2))}\"," +
                $"\"offer_expires_at\":\"{Iso(toAnswer)}\",\"answered_at\":null," +
                $"\"status\":\"offered\",\"level\":{level},\"level_at_start\":{level}," +
                "\"level_at_end\":null,\"lender_share_bp\":2000," +
                "\"rp_to_lender\":0,\"rp_to_borrower\":0}";
        }

        /// <summary>The `/user/search` shape — a BARE profiles row, unlike `following`'s embed.</summary>
        private static string SearchResults() =>
            "[" +
            "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"display_name\":\"KENJI\",\"avatar_url\":null,\"avatar_level\":12}," +
            "{\"id\":\"55555555-5555-5555-5555-555555555555\",\"display_name\":\"KENDRA\",\"avatar_url\":null,\"avatar_level\":3}" +
            "]";

        private void SetLoans(string outLoans, string inLoans, string offersIn = "")
        {
            _transport.LoansJson = "{\"out\":[" + outLoans + "],\"in\":[" + inLoans + "]," +
                                   "\"offers_in\":[" + offersIn + "]}";
        }

        private IEnumerator PushLoans()
        {
            ApiResult<LoanListDto>? r = null;
            IEnumerator call = LoanService.Instance.Refresh(x => r = x);
            while (call.MoveNext()) yield return call.Current;
            Debug.Log($"[LoanCaptureBot] loans refreshed: ok={(r != null && r.Success)} " +
                      $"out={LoanService.Instance.Out.Count} in={LoanService.Instance.In.Count}");
            yield return null;
        }

        // ── the sequence ──────────────────────────────────────────────────────

        private IEnumerator Sequence()
        {
            Application.runInBackground = true;

            // Boot: Logo → Splash → Loading → the title gate → Home.
            yield return new WaitForSecondsRealtime(5.0f);
            yield return WaitForHomeToSettle();

            // The app can boot through a PLAY / START gate that ScreenManager does NOT manage
            // (CAPTURE RULE 0). In THIS project's editor session it auto-advances — DevAutoSignIn
            // carries a saved session — so by the time Home has settled the gate is already behind
            // us and there is nothing to tap.
            //
            // ⚠️ AND TAPPING ANYWAY IS WORSE THAN NOT TAPPING. A name search for "Play" finds
            // Home's own PLAY button, which starts a ROUND: the run that did that was loading a
            // hole instead of opening the Roster. So the gate is only tapped when we are NOT on
            // Home, i.e. when a gate is the reason we are stuck.
            var mgr0 = GolfinRedux.UI.ScreenManager.Instance;
            bool onHome = mgr0 != null && mgr0.CurrentScreen == GolfinRedux.UI.ScreenId.Home;
            if (!onHome)
            {
                Button? start = Resources.FindObjectsOfTypeAll<Button>()
                    .FirstOrDefault(b => b != null && !string.IsNullOrEmpty(b.gameObject.scene.name)
                                      && b.gameObject.activeInHierarchy
                                      && (b.name.Contains("Start") || b.name.Contains("Play")));
                if (start != null)
                {
                    Debug.Log($"[LoanCaptureBot] not on Home ({mgr0?.CurrentScreen}) — tapping the boot gate: {start.name}");
                    start.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(2.5f);
                    yield return WaitForHomeToSettle();
                }
                else Debug.LogWarning($"[LoanCaptureBot] stuck on {mgr0?.CurrentScreen} with no gate button to tap.");
            }
            else Debug.Log("[LoanCaptureBot] already on Home — no boot gate to tap.");

            // ── the stub, installed on the REAL service ───────────────────────
            _transport = new LoanStubTransport
            {
                FollowingJson = Following(),
                SearchJson = SearchResults(),
            };
            var client = new ApiClient(_transport, new LoanStubAuth(), this);
            var service = new LoanService(client);
            LoanService.ConfigureForTest(service);

            // `LoanService.Following` reads the caller's own id off UserService and SKIPS the call
            // when it is missing — which is correct in production (hitting /social//following would
            // 404) but meant the first capture run drew the EMPTY state with three recipients
            // available. Seed it, so the populated list is actually exercised.
            UserService.Instance.SetDetailForTest(new UserDetailDto { Id = "11111111-1111-1111-1111-111111111111" });

            var sync = UnityEngine.Object.FindFirstObjectByType<Golfin.EconomyRuntime.LoanSyncBehaviour>();
            if (sync != null) service.Reconciler = sync;
            else Debug.LogWarning("[LoanCaptureBot] LoanSyncBehaviour is not running (points backend flag OFF?) — " +
                                  "the panels will paint but nothing will reconcile.");

            // ── Roster, via the REAL nav button ───────────────────────────────
            var puim = FindActive<Golfin.UI.PersistentUIManager>();
            yield return GoTo(puim != null ? puim.charactersButton : null,
                              GolfinRedux.UI.ScreenId.Roster);

            var carousel = FindActive<Golfin.Roster.CarouselController>();
            string ownedId = Golfin.Roster.CharacterManager.Instance != null
                ? Golfin.Roster.CharacterManager.Instance.GetSelectedCharacterId()
                : "";
            var owned = Golfin.Roster.CharacterManager.Instance?.GetAllOwnedCharacters();
            string secondId = owned != null
                ? owned.Select(c => c.characterId).FirstOrDefault(id => id != ownedId) ?? ownedId
                : ownedId;

            // State A — owned, not lent, NOT the selected character: LEND is live.
            SetLoans("", "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(secondId);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Snap("loan_roster_A_lend_enabled");
            DumpPanelDiagnostics("loan_roster_A_lend_enabled");

            // State B — LENT OUT: ribbon + dim, every button off.
            SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000001", "character", secondId,
                          asLender: true, level: 82, left: TimeSpan.FromHours(52), days: 3), "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(secondId);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Snap("loan_roster_B_on_loan");
            DumpPanelDiagnostics("loan_roster_B_on_loan");

            // ── asset_loans_offers §3.2 — OFFERED (lender) ────────────────────
            //
            // The SAME asset as state B, offered rather than lent: everything locks identically
            // (that is the point of filtering Out on IsLocked) and only the ribbon's sentence and
            // the LEND slot differ.
            SetLoans(Offer("bbbbbbbb-0000-4000-8000-000000000001", "character", secondId,
                           asLender: true, level: 82, toAnswer: TimeSpan.FromHours(46), days: 3), "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(secondId);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Snap("offers_roster_A_offered");
            DumpPanelDiagnostics("offers_roster_A_offered");

            // The RESCIND confirm, opened by the REAL button in the LEND slot.
            var detailOffered = FindActive<Golfin.Roster.CharacterDetailPanel>();
            Button? rescindBtn = FindLendButton(detailOffered);
            if (rescindBtn != null && rescindBtn.interactable)
            {
                rescindBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Snap("offers_roster_B_rescind_confirm");
                CloseAllModals();
                // ModalController.Hide fades out; the next capture must not catch the tail of it.
                yield return new WaitForSecondsRealtime(1.0f);
            }
            else Debug.LogError("[LoanCaptureBot] RESCIND was not interactable on an OFFERED character — " +
                                "§3.2 says it is the one live button in the locked state.");

            // State C — BORROWED: RETURN, Boost hidden, everything else live.
            string borrowedId = FindLockedCharacterId(ownedId, secondId);
            SetLoans("", Loan("aaaaaaaa-0000-4000-8000-000000000002", "character", borrowedId,
                              asLender: false, level: 64, left: TimeSpan.FromHours(5.4), days: 1));
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(borrowedId);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Snap("loan_roster_C_borrowed");
            DumpPanelDiagnostics("loan_roster_C_borrowed");

            // State F — the RETURN confirm, opened by the REAL button.
            var detail = FindActive<Golfin.Roster.CharacterDetailPanel>();
            Button? lendBtn = FindLendButton(detail);
            if (lendBtn != null && lendBtn.interactable)
            {
                lendBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Snap("loan_roster_F_return_confirm");
                CloseAllModals();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            else Debug.LogError("[LoanCaptureBot] the RETURN button was not interactable on a borrowed character.");

            // State D/E — the LEND modal, opened by the REAL button on a lendable character.
            SetLoans("", "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(secondId);
            yield return new WaitForSecondsRealtime(1.0f);

            lendBtn = FindLendButton(detail);
            if (lendBtn != null && lendBtn.interactable)
            {
                lendBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);   // the following list lands
                yield return Snap("loan_roster_D_lend_modal");
                DumpModalDiagnostics("lend_modal");

                // ── asset_loans_offers §3.1 — the SEARCH, driven through the real field ──
                //
                // `onValueChanged.Invoke` after `SetTextWithoutNotify` is how a keystroke reaches
                // the controller without a keyboard: the field's own text is what the player would
                // see, and the notify is what the debounce listens to. Setting `.text` alone would
                // fire the callback too, but with the field mid-assignment — this order is the one
                // the real input path produces.
                var modal = FindActive<LoanModalController>();
                var field = modal != null
                    ? modal.GetComponentInChildren<TMPro.TMP_InputField>(true) : null;
                if (field != null)
                {
                    field.SetTextWithoutNotify("ken");
                    field.onValueChanged.Invoke("ken");
                    // 300 ms debounce + the round trip + StaggerRise's beats.
                    yield return new WaitForSecondsRealtime(2.0f);
                    yield return Snap("offers_lend_modal_search");
                    DumpModalDiagnostics("lend_modal_search");

                    // Nobody by that name — the empty RESULTS state.
                    _transport.SearchJson = "[]";
                    field.SetTextWithoutNotify("zzz");
                    field.onValueChanged.Invoke("zzz");
                    yield return new WaitForSecondsRealtime(1.6f);
                    yield return Snap("offers_lend_modal_no_results");
                    _transport.SearchJson = SearchResults();

                    // Clearing hides RESULTS again and the followed list stands alone.
                    field.SetTextWithoutNotify("");
                    field.onValueChanged.Invoke("");
                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return Snap("offers_lend_modal_cleared");
                }
                else Debug.LogError("[LoanCaptureBot] the lend modal has no TMP_InputField — §3.1's search field is missing.");

                CloseAllModals();
                yield return new WaitForSecondsRealtime(0.5f);

                // The empty state — nobody followed.
                _transport.FollowingJson = "[]";
                lendBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Snap("loan_roster_E_lend_modal_empty");
                CloseAllModals();
                _transport.FollowingJson = Following();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            else Debug.LogError("[LoanCaptureBot] the LEND button was not interactable on an owned, unlent character.");

            // ── Inventory ─────────────────────────────────────────────────────
            // ClubManager lives in the GLOBAL namespace (CharacterManager does NOT — it is Golfin.Roster).
            var clubs = global::ClubManager.Instance?.GetAllOwnedClubs();
            string clubId = clubs != null && clubs.Count > 0 ? clubs[0].clubId : "";

            yield return GoTo(puim != null ? puim.inventoryButton : null,
                              GolfinRedux.UI.ScreenId.Inventory);

            SetLoans("", "");
            yield return PushLoans();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Snap("loan_clubs_A_lend_enabled");

            if (!string.IsNullOrEmpty(clubId))
            {
                SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000003", "club", clubId,
                              asLender: true, level: 40, left: TimeSpan.FromHours(160), days: 7), "");
                yield return PushLoans();
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Snap("loan_clubs_B_on_loan");
            }

            // ── asset_loans_offers §3.3 — Home: the offer pill and its modal ──
            //
            // Back to Home through the REAL nav button, with a pending offer in `offers_in`.
            // The pill paints off LoanService.OffersIn on the OnLoansChanged the refresh raises,
            // and seats itself under the daily pill by reading that pill's live Y.
            string offeredToMe = FindLockedCharacterId(ownedId, secondId);

            yield return GoTo(puim != null ? puim.homeButton : null, GolfinRedux.UI.ScreenId.Home);
            yield return new WaitForSecondsRealtime(1.0f);

            SetLoans("", "", Offer("cccccccc-0000-4000-8000-000000000001", "character", offeredToMe,
                                   asLender: false, level: 80, toAnswer: TimeSpan.FromHours(46), days: 3));
            yield return PushLoans();
            // The pill's own enterDelay is 0.25 s and its slide is 0.45 s — let the announcement
            // finish, or the frame catches it mid-flight off the left edge.
            yield return new WaitForSecondsRealtime(2.0f);
            yield return Snap("offers_home_pill_one");
            DumpPillDiagnostics("one offer");

            // Two offers — the label switches to the count form and drops the name.
            SetLoans("", "",
                Offer("cccccccc-0000-4000-8000-000000000001", "character", offeredToMe,
                      asLender: false, level: 80, toAnswer: TimeSpan.FromHours(46), days: 3)
                + "," +
                Offer("cccccccc-0000-4000-8000-000000000002", "club", "club_driver_x",
                      asLender: false, level: 22, toAnswer: TimeSpan.FromHours(11), days: 1,
                      lenderName: "KENDRA", lenderId: "55555555-5555-5555-5555-555555555555"));
            yield return PushLoans();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Snap("offers_home_pill_many");
            DumpPillDiagnostics("two offers");

            // Back to one, then open the modal through the REAL pill onClick.
            SetLoans("", "", Offer("cccccccc-0000-4000-8000-000000000001", "character", offeredToMe,
                                   asLender: false, level: 80, toAnswer: TimeSpan.FromHours(46), days: 3));
            yield return PushLoans();
            yield return new WaitForSecondsRealtime(1.0f);

            var pill = FindAny<Golfin.UI.Home.LoanOfferPillController>();
            var pillBtn = pill != null ? pill.GetComponent<Button>() : null;
            if (pillBtn != null)
            {
                pillBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Snap("offers_home_offer_modal");
                DumpOfferModalDiagnostics();
                CloseAllModals();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            else Debug.LogError("[LoanCaptureBot] the Home offer pill has no Button — nothing to tap.");

            // No offers — the pill must LEAVE, not linger.
            SetLoans("", "", "");
            yield return PushLoans();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Snap("offers_home_pill_gone");
            DumpPillDiagnostics("no offers");

            // ── asset_loans_offers §3.4 — Settings ▸ User Profile ─────────────
            var settings = FindAny<Golfin.UI.SettingsController>();
            var gear = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b != null && !string.IsNullOrEmpty(b.gameObject.scene.name)
                                  && b.gameObject.activeInHierarchy
                                  && (b.name.Contains("Settings") || b.name.Contains("Gear")));
            if (gear != null)
            {
                gear.onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.0f);

                // Expand USER PROFILE through its own accordion header, the way a player does.
                var profileRow = Resources.FindObjectsOfTypeAll<Golfin.UI.SettingsMenuItem>()
                    .FirstOrDefault(m => m != null && m.gameObject.name == "UserProfileRow");
                if (profileRow != null)
                {
                    profileRow.ToggleExpansion();
                    // expandDuration is 0.3 s and the outer VLG reflows after it.
                    yield return new WaitForSecondsRealtime(1.4f);
                    yield return Snap("offers_settings_toggle_on");
                    DumpSettingsDiagnostics();

                    // Tap the toggle itself — the knob slides and the PUT goes out.
                    var toggle = FindAny<LoanOffersToggle>();
                    var togBtn = toggle != null
                        ? toggle.transform.Find("Toggle")?.GetComponent<Button>() : null;
                    if (togBtn != null)
                    {
                        togBtn.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1.2f);
                        yield return Snap("offers_settings_toggle_tapped");
                        Debug.Log($"[LoanCaptureBot] toggle after tap: IsOn={toggle!.IsOn}");
                    }
                    else Debug.LogError("[LoanCaptureBot] the LOAN OFFERS toggle button is missing.");
                }
                else Debug.LogError("[LoanCaptureBot] UserProfileRow (SettingsMenuItem) not found.");
            }
            else Debug.LogError("[LoanCaptureBot] no settings/gear button found to open Settings.");

            // ── done ──────────────────────────────────────────────────────────
            CopyOut();
            Debug.Log("[LoanCaptureBot] DONE — " + string.Join("\n  ", _shots));
            EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// Tap a nav button and WAIT UNTIL THE SCREEN ACTUALLY CHANGED, retrying the tap.
        ///
        /// <para>
        /// A fixed 2-second sleep is a guess, and it was wrong: the first run's tap landed while a
        /// Home notice modal was up, the screen never changed, and three "Roster" captures are
        /// pictures of Home. Asserting the destination is the difference between a capture and a
        /// picture of somewhere else.
        /// </para>
        /// </summary>
        /// <summary>
        /// Wait until Home has been the current screen, uninterrupted, for two whole seconds.
        ///
        /// <para>
        /// ⚠️ THE BOOT CHAIN IS STILL MOVING AFTER A FIXED SLEEP. Logo → Splash → Loading → Home
        /// runs through FadeController, and a nav tap that lands mid-transition is applied and then
        /// STOMPED when the pending boot transition finishes and applies Home. That is not
        /// hypothetical: run 4 logged "Roster is ON SCREEN (attempt 1)" — with the detail panel
        /// genuinely active at that instant — and then captured three pictures of Home, because
        /// Home arrived after the Roster did.
        /// </para>
        /// <para>
        /// So the bot waits for QUIET, not for a duration: a screen that has not changed for two
        /// seconds is a screen nothing is still on its way to replacing.
        /// </para>
        /// </summary>
        private static IEnumerator WaitForHomeToSettle()
        {
            var mgr = GolfinRedux.UI.ScreenManager.Instance;
            float quietSince = Time.realtimeSinceStartup;
            var last = mgr != null ? mgr.CurrentScreen : GolfinRedux.UI.ScreenId.Logo;
            float deadline = Time.realtimeSinceStartup + 25f;

            while (Time.realtimeSinceStartup < deadline)
            {
                mgr = GolfinRedux.UI.ScreenManager.Instance;
                var now = mgr != null ? mgr.CurrentScreen : last;
                if (now != last) { last = now; quietSince = Time.realtimeSinceStartup; }

                if (now == GolfinRedux.UI.ScreenId.Home
                    && Time.realtimeSinceStartup - quietSince >= 2f)
                {
                    Debug.Log("[LoanCaptureBot] Home has settled — boot transitions are done.");
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning($"[LoanCaptureBot] Home never settled (stuck on {last}) — navigating anyway.");
        }

        private IEnumerator GoTo(Button? navButton, GolfinRedux.UI.ScreenId target)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (attempt == 0 && navButton != null) navButton.onClick.Invoke();
                else
                {
                    // ⚠️ `ShowScreen(x)` RETURNS IMMEDIATELY WHEN `_currentScreen == x`
                    // (ScreenManager line ~351) — so a retry after a transition that set the field
                    // but never completed its fade is a NO-OP, and the retry loop spins while the
                    // frame stays on Home. `instant: true` is the one form that bypasses that
                    // guard. Run 3 shot Home three times with the log cheerfully saying "reached
                    // Roster on attempt 1"; CurrentScreen is not evidence, the ACTIVE SCREEN OBJECT
                    // is (CAPTURE RULE 0, the same class of false positive).
                    GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(target, instant: true);
                }

                float deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (ScreenIsOnScreen(target))
                    {
                        yield return new WaitForSecondsRealtime(1.2f);   // fade-in + panel bind
                        Debug.Log($"[LoanCaptureBot] {target} is ON SCREEN (attempt {attempt + 1}).");
                        yield break;
                    }
                    yield return null;
                }
                Debug.LogWarning($"[LoanCaptureBot] {target} not on screen after attempt {attempt + 1} " +
                                 $"(CurrentScreen={GolfinRedux.UI.ScreenManager.Instance?.CurrentScreen}).");
            }
            Debug.LogError($"[LoanCaptureBot] NEVER got {target} on screen — every capture after " +
                           "this is a picture of somewhere else.");
        }

        /// <summary>
        /// Is the screen ACTUALLY rendered — not merely what ScreenManager believes.
        ///
        /// <para>Asked of the screen's own root GameObject, found by the controller that lives on
        /// it, because "the field says Roster" and "the Roster is on screen" turned out to be
        /// different facts.</para>
        /// </summary>
        private static bool ScreenIsOnScreen(GolfinRedux.UI.ScreenId target)
        {
            if (GolfinRedux.UI.ScreenManager.Instance == null) return false;
            if (GolfinRedux.UI.ScreenManager.Instance.CurrentScreen != target) return false;

            switch (target)
            {
                case GolfinRedux.UI.ScreenId.Roster:
                    return FindActive<Golfin.Roster.CharacterDetailPanel>() != null;
                case GolfinRedux.UI.ScreenId.Inventory:
                    return FindActive<Golfin.Inventory.ClubDetailPanel>() != null;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Hide every loan modal on EVERY screen.
        ///
        /// <para>The lend and return modals are instantiated once per screen (Roster and
        /// Inventory), so hiding "the one that was found first" leaves the other one on screen —
        /// which is exactly how the first capture run ended up with a return popup sitting on top
        /// of the lend modal in three frames.</para>
        /// </summary>
        /// <summary>
        /// Close every loan modal, found by their SHARED BASE TYPE.
        ///
        /// <para>
        /// ⚠️ NOT a list of concrete types. The first version named LoanModalController and
        /// LoanReturnModalController explicitly, so when asset_loans_offers added two more the
        /// rescind popup stayed open UNDER the next three captures — the search frames were
        /// pictures of the rescind dialog on top of the lend modal, and nothing errored. Anything
        /// that derives from ModalController is a modal; enumerating the base is the only form of
        /// this that cannot go stale when a fifth one is added.
        /// </para>
        /// </summary>
        private static void CloseAllModals()
        {
            foreach (var m in Resources.FindObjectsOfTypeAll<Golfin.UI.Modals.ModalController>())
                if (!string.IsNullOrEmpty(m.gameObject.scene.name)) m.Hide();
        }

        /// <summary>
        /// Measured facts about what is on screen, written next to the captures.
        ///
        /// <para>
        /// EVERYTHING HERE IS A NUMBER READ OFF THE LIVE OBJECT, never a look at the frame:
        /// whether the dim is on and at what alpha, which buttons are interactable, whether the
        /// modal panel actually contains its own footer. Memory `feedback_never_eyeball_brightness`
        /// and `feedback_verify_ui_metrics_numerically` — "is it dimmed?" and "does it fit?" are
        /// questions a pixel and a rect answer, and eyes get wrong.
        /// </para>
        /// </summary>
        public void DumpModalDiagnostics(string label)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in Resources.FindObjectsOfTypeAll<LoanModalController>())
            {
                if (string.IsNullOrEmpty(m.gameObject.scene.name) || !m.IsVisible()) continue;
                var panel = m.modalPanel != null ? (RectTransform)m.modalPanel.transform : null;
                if (panel == null) continue;
                sb.Append("panel ").Append(panel.rect.width.ToString("0.#")).Append('x')
                  .Append(panel.rect.height.ToString("0.#")).Append('\n');
                foreach (RectTransform c in panel)
                {
                    var w = new Vector3[4]; c.GetWorldCorners(w);
                    var p = new Vector3[4]; panel.GetWorldCorners(p);
                    bool inside = w[0].y >= p[0].y - 0.5f && w[1].y <= p[1].y + 0.5f;
                    sb.Append("  ").Append(c.name).Append(' ')
                      .Append(c.rect.width.ToString("0.#")).Append('x').Append(c.rect.height.ToString("0.#"))
                      .Append(" active=").Append(c.gameObject.activeSelf)
                      .Append(" insidePanel=").Append(inside).Append('\n');
                }
            }
            Debug.Log($"[LoanCaptureBot] DIAG {label}\n{sb}");
        }

        /// <summary>The dim and every button state on the Roster panel, as numbers.</summary>
        public static void DumpPanelDiagnostics(string label)
        {
            var detail = FindActive<Golfin.Roster.CharacterDetailPanel>();
            if (detail == null) { Debug.Log($"[LoanCaptureBot] DIAG {label}: no detail panel"); return; }

            var sb = new System.Text.StringBuilder();
            foreach (Button b in detail.GetComponentsInChildren<Button>(true))
                sb.Append("  btn ").Append(b.name)
                  .Append(" active=").Append(b.gameObject.activeSelf)
                  .Append(" interactable=").Append(b.interactable).Append('\n');

            foreach (Transform t in detail.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "LentDim" && t.name != "LoanRibbon") continue;
                var img = t.GetComponent<Image>();
                sb.Append("  ").Append(t.name).Append(" active=").Append(t.gameObject.activeSelf)
                  .Append(" alpha=").Append(img != null ? img.color.a.ToString("0.00") : "?")
                  .Append('\n');
            }
            Debug.Log($"[LoanCaptureBot] DIAG {label}\n{sb}");
        }

        /// <summary>A catalog character the player does NOT own — a borrowed one flips exactly that
        /// row from locked to borrowed, which is the whole point of §3's "no carousel change".</summary>
        /// <summary>
        /// Every text and rect in the offer modal's asset row, by NAME.
        ///
        /// <para>Written because the generic modal dump came back empty for this prefab and the
        /// asset NAME rendered as nothing on the first capture — "it looks blank" is not a
        /// diagnosis, and the three candidates (missing object / empty string / zero rect) are
        /// only distinguishable from the numbers.</para>
        /// </summary>
        private static void DumpOfferModalDiagnostics()
        {
            var modal = FindAny<LoanOfferModalController>();
            if (modal == null) { Debug.LogWarning("[LoanCaptureBot] OFFERMODAL: not found"); return; }

            Debug.Log($"[LoanCaptureBot] OFFERMODAL offerId={modal.CurrentOfferId}");
            foreach (var t in modal.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                var rt = (RectTransform)t.transform;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                // characterCount is the decisive one: a TMP with text, a rect and full alpha that
                // still draws nothing has generated NO GEOMETRY, and that is a different bug from
                // one drawn off-screen or at alpha 0. All four are printed so the answer is read
                // rather than guessed.
                t.ForceMeshUpdate();
                Debug.Log($"[LoanCaptureBot] OFFERMODAL {t.name}: text='{t.text}' " +
                          $"len={t.text?.Length ?? -1} size={t.fontSize} " +
                          $"rect={rt.rect.width:F0}x{rt.rect.height:F0} " +
                          $"world=({corners[0].x:F0},{corners[0].y:F0})-({corners[2].x:F0},{corners[2].y:F0}) " +
                          $"chars={t.textInfo.characterCount} " +
                          $"renderedW={t.renderedWidth:F1} renderedH={t.renderedHeight:F1} " +
                          $"crAlpha={t.canvasRenderer.GetAlpha():F2} " +
                          $"enabled={t.enabled} active={t.gameObject.activeInHierarchy} " +
                          $"colour=#{ColorUtility.ToHtmlStringRGBA(t.color)}");
            }
            foreach (var i in modal.GetComponentsInChildren<Image>(true))
                if (i.name == "Portrait")
                    Debug.Log($"[LoanCaptureBot] OFFERMODAL Portrait: sprite=" +
                              $"{(i.sprite != null ? i.sprite.name : "<NONE>")} enabled={i.enabled}");
        }

        /// <summary>
        /// The offer pill's measured Y against the daily pill's — the §3.3 acceptance line ("under
        /// the daily pill when both show, in its slot when not") is a NUMBER, not a look.
        /// </summary>
        private static void DumpPillDiagnostics(string label)
        {
            var offer = FindAny<Golfin.UI.Home.LoanOfferPillController>();
            var daily = FindAny<Golfin.UI.Home.DailyMissionPillController>();
            if (offer == null) { Debug.LogWarning("[LoanCaptureBot] pill dump: no offer pill."); return; }

            Debug.Log($"[LoanCaptureBot] PILL[{label}] " +
                      $"offer.showing={offer.IsShowing} offer.y={offer.CurrentY:F1} " +
                      $"offer.targetY={offer.ComputeTargetY():F1} | " +
                      $"daily.showing={(daily != null ? daily.IsShowing.ToString() : "n/a")} " +
                      $"daily.y={(daily != null ? daily.CurrentY.ToString("F1") : "n/a")} | " +
                      $"gap={(daily != null ? (daily.CurrentY - offer.CurrentY).ToString("F1") : "n/a")} " +
                      $"(expect 162 = 122 pill + 40 gap when both show)");
        }

        /// <summary>LOG OUT and CLOSE must still be on screen under the taller submenu (§3.4).</summary>
        private static void DumpSettingsDiagnostics()
        {
            var toggle = FindAny<LoanOffersToggle>();
            Debug.Log($"[LoanCaptureBot] SETTINGS toggle present={toggle != null} " +
                      $"IsOn={(toggle != null ? toggle.IsOn.ToString() : "n/a")}");

            foreach (string name in new[] { "LogOutRow", "CloseButton", "LoanOffersRow" })
            {
                var go = Resources.FindObjectsOfTypeAll<RectTransform>()
                    .FirstOrDefault(r => r != null && r.name == name
                                      && !string.IsNullOrEmpty(r.gameObject.scene.name));
                if (go == null) { Debug.LogWarning($"[LoanCaptureBot] SETTINGS {name}: NOT FOUND"); continue; }

                var corners = new Vector3[4];
                go.GetWorldCorners(corners);
                bool onScreen = corners[0].y < Screen.height && corners[1].y > 0
                             && go.gameObject.activeInHierarchy;
                Debug.Log($"[LoanCaptureBot] SETTINGS {name}: active={go.gameObject.activeInHierarchy} " +
                          $"worldY={corners[0].y:F0}..{corners[1].y:F0} screenH={Screen.height} " +
                          $"onScreen={onScreen}");
            }
        }

        private static string FindLockedCharacterId(params string[] avoid)
        {
            var cm = Golfin.Roster.CharacterManager.Instance;
            if (cm == null) return avoid.Length > 0 ? avoid[0] : "";
            foreach (var c in cm.GetAllCatalogCharacters())
                if (!c.isOwned && !avoid.Contains(c.characterId)) return c.characterId;
            return avoid.Length > 0 ? avoid[0] : "";
        }

        private static Button? FindLendButton(Golfin.Roster.CharacterDetailPanel? detail)
        {
            if (detail == null) return null;
            foreach (Button b in detail.GetComponentsInChildren<Button>(true))
                if (b.name == "LendButton") return b;
            return null;
        }

        /// <summary>
        /// Copy each capture into the task folder — WITH its CaptureCore provenance sidecar.
        ///
        /// <para>
        /// ⚠️ THE `.png.json` IS NOT OPTIONAL BAGGAGE. It is what says `realPlay: true` about a
        /// frame, and Rule 24 (`enforce_implementer_done.py`) reads it to tell a real-play capture
        /// from a fake-state or edit-mode one. Copying only the PNG left every frame in the task
        /// folder unprovenanced and the gate warned on the canonical one — the frames were
        /// genuine and the evidence that said so had been left behind in
        /// <c>Docs/Diagnostics/_capture/</c>.
        /// </para>
        /// </summary>
        private void CopyOut()
        {
            System.IO.Directory.CreateDirectory(LoanUiCaptureBot.OutDir);
            foreach (string src in _shots)
            {
                string dst = System.IO.Path.Combine(LoanUiCaptureBot.OutDir,
                                                    System.IO.Path.GetFileName(src));
                try { System.IO.File.Copy(src, dst, overwrite: true); }
                catch (Exception e) { Debug.LogWarning($"[LoanCaptureBot] copy failed for {src}: {e.Message}"); }

                string sidecar = src + ".json";
                if (!System.IO.File.Exists(sidecar)) continue;
                try { System.IO.File.Copy(sidecar, dst + ".json", overwrite: true); }
                catch (Exception e) { Debug.LogWarning($"[LoanCaptureBot] sidecar copy failed for {src}: {e.Message}"); }
            }
        }
    }
}
