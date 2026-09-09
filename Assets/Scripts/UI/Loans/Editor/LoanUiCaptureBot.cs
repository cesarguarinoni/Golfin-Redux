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
        public const string OutDir = "Docs/Specs/Active/asset_loans/screenshots";

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

        private void SetLoans(string outLoans, string inLoans)
        {
            _transport.LoansJson = "{\"out\":[" + outLoans + "],\"in\":[" + inLoans + "]}";
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
            _transport = new LoanStubTransport { FollowingJson = Following() };
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
        private static void CloseAllModals()
        {
            foreach (var m in Resources.FindObjectsOfTypeAll<LoanModalController>())
                if (!string.IsNullOrEmpty(m.gameObject.scene.name)) m.Hide();
            foreach (var m in Resources.FindObjectsOfTypeAll<LoanReturnModalController>())
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

        private void CopyOut()
        {
            System.IO.Directory.CreateDirectory(LoanUiCaptureBot.OutDir);
            foreach (string src in _shots)
            {
                string dst = System.IO.Path.Combine(LoanUiCaptureBot.OutDir,
                                                    System.IO.Path.GetFileName(src));
                try { System.IO.File.Copy(src, dst, overwrite: true); }
                catch (Exception e) { Debug.LogWarning($"[LoanCaptureBot] copy failed for {src}: {e.Message}"); }
            }
        }
    }
}
