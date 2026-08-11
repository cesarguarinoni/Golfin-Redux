using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Auth;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Splash gate — Phase 2a (account_flow_wiring): PLAY is now the auth entry.
    ///   • PLAY (StartButton): valid session → refresh token → Home (or CreateUsername when no
    ///     display name yet); no/stale session → Login screen.
    ///   • CreateAccountButton (runtime-wired, it had no inspector link) → Sign Up screen.
    ///   • TEMPORARY DEV BYPASS: tapping anywhere on the splash OUTSIDE the buttons keeps the old
    ///     flow (straight to Home, no auth) so the game stays testable while auth is stabilised.
    ///     Remove before release — see RemoveDevBypass note below.
    /// Demo builds (demo_build_slice §3.4) are unchanged: PLAY goes straight into the game.
    /// </summary>
    public class SplashScreenController : MonoBehaviour
    {
        private bool _busy;
        private GameObject _devBypassCatcher;

        public void OnStartClicked()
        {
            if (_busy) return;

            // demo_build_slice §3.4: offline demo — guests go straight into the game.
            if (GolfinRedux.Demo.DemoGate.IsDemo) { GoHome(); return; }

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
            var target = AuthService.Instance.Session.HasDisplayName ? ScreenId.Home : ScreenId.CreateUsername;
            Show(target);
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
                return; // demo: no auth entry, no bypass needed
            }

            WireLinkButtons();
            CreateDevBypassCatcher();
        }

        private void OnDisable()
        {
            if (_devBypassCatcher != null)
            {
                Destroy(_devBypassCatcher);
                _devBypassCatcher = null;
            }
        }

        public void OnLoginLinkClicked()
        {
            if (_busy) return;
            Show(ScreenId.Login);
        }

        /// <summary>
        /// CreateAccountButton and LoginButton ship with NO inspector onClick (verified in ShellScene) —
        /// wire both here. Logs loudly so a miswire is visible in the console instead of a dead button.
        /// </summary>
        private void WireLinkButtons()
        {
            WireChildButton("CreateAccountButton", OnCreateAccountClicked);
            WireChildButton("LoginButton", OnLoginLinkClicked);
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

            // The link buttons' rects are only 160x30 canvas units while the visible label text
            // overflows wider — taps on the visible text missed the rect and fell through to the
            // dev bypass catcher. Expand the raycast area (negative padding grows it) to ~280x80.
            var img = button.targetGraphic as Image;
            if (img != null) img.raycastPadding = new Vector4(-60f, -25f, -60f, -25f);

            Debug.Log($"[Splash] Wired '{childName}' (raycast area expanded).");
        }

        /// <summary>
        /// TEMPORARY DEV BYPASS (account_flow_wiring): an invisible full-screen catcher inserted ABOVE
        /// the raycastable Background/SplashLogo art but BELOW the buttons (at StartButton's sibling
        /// index — the buttons shift up by one and keep winning the raycast). First version sat behind
        /// the Background, which swallowed every tap. Any tap that hits none of the three buttons goes
        /// straight to Home exactly like the pre-auth flow.
        /// RemoveDevBypass: delete this method + its OnEnable/OnDisable calls when auth is mandatory.
        /// </summary>
        private void CreateDevBypassCatcher()
        {
            if (_devBypassCatcher != null) return;

            _devBypassCatcher = new GameObject("DevBypassCatcher_TEMP", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)_devBypassCatcher.transform;
            rt.SetParent(transform, worldPositionStays: false);

            // Place just below the first button: above background art, below all interactive controls.
            var startButton = transform.Find("StartButton");
            rt.SetSiblingIndex(startButton != null ? startButton.GetSiblingIndex() : 0);

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = _devBypassCatcher.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);  // invisible, but raycastable
            img.raycastTarget = true;

            var btn = _devBypassCatcher.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[Splash] DEV BYPASS tap — skipping auth, straight to Home (temporary).");
                GoHome();
            });
        }

        // demo_build_slice §3.4: the demo boots to this Splash gate. Label the guest button "Play"
        // and remove Create Account (offline demo — guests tap Play to go straight into the game;
        // Login is kept). No-op in the full game.
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
