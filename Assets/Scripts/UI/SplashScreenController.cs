using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Auth;
using Golfin.InventorySync;
using Golfin.Roster;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Splash gate — Phase 2a (account_flow_wiring): the primary button is now the auth entry.
    ///   • LOGIN (StartButton): valid session → refresh token → Home (or CreateUsername when no
    ///     display name yet); no/stale session → Login screen. Labelled "Play" in demo builds,
    ///     where it skips auth entirely.
    ///   • CreateAccountButton (runtime-wired, it had no inspector link) → Sign Up screen.
    ///   • NO DEV BYPASS. The temporary full-screen tap-catcher that used to send any stray tap
    ///     straight to Home was deleted for the hard sign-in gate (points_cutover_followups item 3,
    ///     Cesar 2026-08-12: no guest mode). It shipped in player builds and, since the RP cutover,
    ///     dropped signed-out players into a game where every server debit 403s. Automated bots use
    ///     the editor-only <see cref="Golfin.Dev.BotSessionOverride"/> instead.
    /// Demo builds (demo_build_slice §3.4) are unchanged: PLAY goes straight into the game.
    /// </summary>
    public class SplashScreenController : MonoBehaviour
    {
        private bool _busy;

        /// <summary>starter_restore_gate: the START label's own text, stashed while the offline
        /// message occupies it. Null means the label is showing its normal caption.</summary>
        private string _startLabelStashed;

        public void OnStartClicked()
        {
            if (_busy) return;

            // starter_restore_gate: a previous tap left AUTH_ERR_OFFLINE on the button. Put the
            // caption back before this tap re-runs the gate.
            RestoreStartLabel();

            // demo_build_slice §3.4: offline demo — guests go straight into the game.
            if (GolfinRedux.Demo.DemoGate.IsDemo) { GoHome(); return; }

#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
            // points_cutover_followups item 1: an armed bot run carries a fake local session. Route
            // it BEFORE RefreshSession — the fake session has no refresh token on purpose, so the
            // normal returning-user path would round-trip, fail, SignOut and land on Login, which is
            // precisely the stall this replaces. Editor/harness builds only.
            if (Golfin.Dev.BotSessionOverride.Active)
            {
                Debug.Log("[Splash] Bot session override active — straight to Home (no auth, backend OFF).");
                RouteAuthenticated();
                return;
            }
#endif

            var auth = AuthService.Instance;
            if (!auth.Session.IsAuthenticated)
            {
                Show(ScreenId.Login);
                return;
            }

            // Returning user: the Supabase access token lives ~1h, so refresh before entering.
            _busy = true;
            auth.RefreshSession(result =>
            {
                _busy = false;
                if (result != null && result.Success)
                {
                    RouteAuthenticated();
                }
                else
                {
                    // Stale/revoked session (or offline): fall back to an explicit login.
                    Debug.Log($"[Splash] Session refresh failed → Login. ({result?.Message})");
                    auth.SignOut();
                    Show(ScreenId.Login);
                }
            });
        }

        public void OnCreateAccountClicked()
        {
            if (_busy) return;
            Show(ScreenId.SignUp);
        }

        /// <summary>Post-auth routing, mirrors LoginScreenController: no username yet → CreateUsername.</summary>
        private void RouteAuthenticated()
        {
            Golfin.UI.Account.AccountUiBridge.SyncUsername();

            // ORDER MATTERS (starter_restore_gate §3): a brand-new account goes to CreateUsername
            // WITHOUT waiting on the inventory fetch — its own gate runs there.
            if (!AuthService.Instance.Session.HasDisplayName)
            {
                Show(ScreenId.CreateUsername);
                return;
            }

            // starter_restore_gate: hold the button until the server has answered "does this
            // account own a starter?". A previous tap may have failed — this is also the retry.
            _busy = true;
            InventorySyncBehaviour.RetryBoot();
            Golfin.UI.Account.StarterGate.Resolve(route =>
            {
                _busy = false;

                if (route == Golfin.UI.Account.StarterRoute.ServerUnreachable)
                {
                    // D1: a failed fetch NEVER shows the picker — an empty save plus silence is
                    // indistinguishable from a new account. Stay here; the next tap retries.
                    Debug.Log("[Splash] Inventory fetch failed → holding on Splash (never the picker).");
                    ShowStartLabelError(LocalizationManager.Get("AUTH_ERR_OFFLINE"));
                    return;
                }

                // starting_character_selection: check for interrupted fresh-save before landing on Home.
                if (CharacterManager.Instance != null && CharacterManager.Instance.NeedsStarter)
                {
                    Debug.Log("[Splash] NeedsStarter=true → StartingCharacterSelection (interrupted or fresh-save boot).");
                    Show(GolfinRedux.UI.ScreenId.StartingCharacterSelection);
                }
                else
                {
                    Show(ScreenId.Home);
                }
            });
        }

        // ── START-button label as the error surface (no new prefab, no new string) ──

        private TMP_Text FindStartLabel()
        {
            var start = transform.Find("StartButton");
            return start == null ? null : start.GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>Borrow the START caption for one message. The auto-localizer is disabled while
        /// it is borrowed, exactly as <see cref="ApplyDemoGate"/> does, or it would re-set "LOGIN"
        /// over the message on the next language refresh.</summary>
        private void ShowStartLabelError(string message)
        {
            var label = FindStartLabel();
            if (label == null) { Debug.LogWarning($"[Splash] {message} (no START label to show it on)"); return; }

            if (_startLabelStashed == null) _startLabelStashed = label.text;
            var loc = label.GetComponent("LocalizedText") as Behaviour;
            if (loc != null) loc.enabled = false;
            label.text = message;
            Debug.Log($"[Splash] {message}");
        }

        private void RestoreStartLabel()
        {
            if (_startLabelStashed == null) return;

            var label = FindStartLabel();
            if (label != null)
            {
                // Only write back a caption we actually took. Re-enabling the localizer is the
                // restore when the label was empty at borrow time (it had not run yet).
                if (_startLabelStashed.Length > 0) label.text = _startLabelStashed;
                var loc = label.GetComponent("LocalizedText") as Behaviour;
                if (loc != null) loc.enabled = true;
            }
            _startLabelStashed = null;
        }

        // K13 (boot_loading_screen_removal): boot goes straight to Home — the Loading screen on
        // this path was pure theater (see git history of this file for the full measurement note).
        // Loading/ScreenId are KEPT for hole-load; if boot gains a real dependency it can re-enter
        // via the existing SetRealProgress plumbing.
        private void GoHome() => Show(ScreenId.Home);

        private void Show(ScreenId id)
        {
            var manager = FindFirstObjectByType<ScreenManager>();
            if (manager != null)
                manager.ShowScreen(id);
            else
                Debug.LogError("ScreenManager not found");
        }

        private void OnEnable()
        {
            if (GolfinRedux.Demo.DemoGate.IsDemo)
            {
                StartCoroutine(ApplyDemoGate());
                return; // demo: no auth entry
            }

            WireLinkButtons();
        }

        /// <summary>
        /// CreateAccountButton ships with NO inspector onClick (verified in ShellScene) — wire it here.
        /// Logs loudly so a miswire is visible in the console instead of a dead button.
        /// (The separate Login link was removed: StartButton is the login entry now.)
        /// </summary>
        private void WireLinkButtons()
        {
            WireChildButton("CreateAccountButton", OnCreateAccountClicked);
        }

        private void WireChildButton(string childName, UnityEngine.Events.UnityAction handler)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                Debug.LogWarning($"[Splash] Child '{childName}' not found — link not wired.");
                return;
            }
            var button = child.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning($"[Splash] '{childName}' has no Button component — link not wired.");
                return;
            }
            button.onClick.RemoveListener(handler); // idempotent across OnEnable cycles
            button.onClick.AddListener(handler);

            Debug.Log($"[Splash] Wired '{childName}'.");
        }

        // demo_build_slice §3.4: the demo boots to this Splash gate. Label the guest button "Play"
        // (it reads "LOGIN" in the full game) and remove Create Account — offline demo, guests tap
        // Play to go straight into the game. No-op in the full game.
        private IEnumerator ApplyDemoGate()
        {
            // Wait one frame so the LocalizedText on the Start label applies first — then override it.
            yield return null;

            var start = transform.Find("StartButton");
            if (start != null)
            {
                var label = start.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    // Disable the auto-localizer so it can't re-set "PLAY" over our "Play".
                    var loc = label.GetComponent("LocalizedText") as Behaviour;
                    if (loc != null) loc.enabled = false;
                    label.text = "Play";
                }
            }

            var create = transform.Find("CreateAccountButton");
            if (create != null) create.gameObject.SetActive(false);
        }
    }
}
